using Hookline.Application.Services;
using Hookline.Domain.Entities;
using Hookline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hookline.Infrastructure.BackgroundJobs
{
    public class WebhookDeliveryWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WebhookDeliveryWorker> _logger;

        public WebhookDeliveryWorker(IServiceScopeFactory scopeFactory, 
            ILogger<WebhookDeliveryWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;           
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();

                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var deliveryService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

                    var events = await db.IncomingEvents
                        .Where(e => (e.Status == "Received") 
                        || (e.Status == "Pending" && e.NextRetryAt <= DateTime.UtcNow)
                        || (e.Status == "Processing" && e.LockedUntil < DateTime.UtcNow))
                        .OrderBy(e => e.ReceivedAt)
                        .Take(10)
                        .ToListAsync(stoppingToken);

                    if (events.Any())
                    {
                        foreach (var evt in events)
                        {
                            var updateRows = await db.IncomingEvents
                                .Where(e => e.Id == evt.Id && (e.Status == "Received"
                                || (e.Status == "Pending" && e.NextRetryAt <= DateTime.UtcNow)
                                || (e.Status == "Processing" && e.LockedUntil < DateTime.UtcNow)))
                                .ExecuteUpdateAsync(setters => setters
                                .SetProperty(e => e.Status, "Processing")
                                .SetProperty(e => e.NextRetryAt, (DateTime?)null)
                                .SetProperty(e => e.LockedUntil, DateTime.UtcNow.AddMinutes(5)),
                                stoppingToken);

                            if (updateRows == 0)
                            {
                                _logger.LogInformation("Событие {EventId} уже забрал другой воркер", evt.Id);
                                continue;
                            }                              
                            
                            var subscribers = await db.Subscribers.Where(s=>s.Source == evt.Source && s.IsActive).ToListAsync();

                            if (subscribers.Count == 0)
                            {
                                evt.Status = "Delivered";
                                evt.LockedUntil = null;
                                await db.SaveChangesAsync(stoppingToken);
                                continue;
                            }

                            bool allSuccess = true;
                            string overallError = "";

                            foreach (var sub in subscribers)
                            {
                                var result = await deliveryService.DeliveryAsync(sub.Url, evt.Payload, stoppingToken);

                                string attemptStatus;

                                if (result.IsSuccess)
                                {
                                    attemptStatus = "Delivered";
                                }
                                else
                                {
                                    attemptStatus = "Failed";
                                    allSuccess = false;
                                    overallError = result.Error ?? $"HTTP {result.StatusCode}";
                                }

                                var previousAttemptsCount = await db.DeliveryAttempts
                                    .CountAsync(a => a.EventId == evt.Id && a.SubscriberId == sub.Id, stoppingToken);

                                var currentAttemptNumber = previousAttemptsCount + 1;

                                var attempt = new DeliveryAttempt
                                {
                                    Id = Guid.NewGuid(),
                                    EventId = evt.Id,
                                    SubscriberId = sub.Id, 
                                    AttemptNumber = currentAttemptNumber,
                                    Status = attemptStatus,
                                    ResponseCode = result.StatusCode,
                                    Error = result.Error,
                                    CreatedAt = DateTime.UtcNow,
                                    CompletedAt = DateTime.UtcNow
                                };
                                db.DeliveryAttempts.Add(attempt);
                            }

                            if (allSuccess)
                            {
                                evt.Status = "Delivered";
                                evt.LockedUntil = null;
                                _logger.LogInformation("Событие {EventId} успешно доставлено всем ({Count}) подписчикам.", 
                                    evt.Id, subscribers.Count);
                            }
                            else
                            {
                                evt.Status = "Pending";
                                evt.NextRetryAt = DateTime.UtcNow.AddMinutes(1);
                                evt.LockedUntil = null;

                                _logger.LogWarning("Событие {EventId} доставлено не всем. Ошибка: " +
                                    "{Error}. Повтор через 1 мин.", evt.Id, overallError);
                            }

                            await db.SaveChangesAsync(stoppingToken);
                        }
                        
                        _logger.LogInformation("Взял в работу {Count} событий", events.Count);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в воркере");
                }

                await Task.Delay(3000, stoppingToken);
            }
        }
    }
}
