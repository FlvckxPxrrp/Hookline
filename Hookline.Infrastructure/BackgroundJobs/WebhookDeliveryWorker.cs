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
                            string testUrl = "https://webhook.site/67ee01dc-0073-4bb7-a924-fe6350e258e6";

                            var updateRows = await db.IncomingEvents
                                .Where(e => e.Id == evt.Id && e.Status == "Received"
                                || (e.Status == "Pending" && e.NextRetryAt <= DateTime.UtcNow)
                                || (e.Status == "Processing" && e.LockedUntil < DateTime.UtcNow))
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

                            var result = await deliveryService.DeliveryAsync(testUrl, evt.Payload, stoppingToken);

                            string newEventStatus;
                            DateTime? nextRetryAt = null;
                            string? attemptStatus;

                            if (result.IsSuccess)
                            {
                                newEventStatus = "Delivered";
                                attemptStatus = "Delivered";
                            }
                            else if (result.StatusCode >= 400 && result.StatusCode < 500 && result.StatusCode != 429)
                            {
                                newEventStatus = "Dead";
                                attemptStatus = "Failed";
                                _logger.LogWarning("Событие {EventId} отклонено сервером (Код: {Code}). Ретраи отключены",
                                    evt.Id, result.StatusCode);
                            }
                            else
                            {
                                newEventStatus = "Pending";
                                attemptStatus = "Failed";

                                nextRetryAt = DateTime.UtcNow.AddMinutes(1);

                                _logger.LogWarning("Событие {EventId} не доставлено: {Error}. Повтор через 1 мин.", 
                                    evt.Id, result.Error);
                            }

                            var previousAttemptsCount = await db.DeliveryAttempts
                                .CountAsync(a => a.EventId == evt.Id, stoppingToken);
                            var currentAttemptNumber = previousAttemptsCount + 1;

                            var attempt = new DeliveryAttempt
                            {
                                Id = Guid.NewGuid(),
                                EventId = evt.Id,
                                SubscriberId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                                AttemptNumber = currentAttemptNumber,
                                Status = attemptStatus,
                                CreatedAt = DateTime.UtcNow,
                                CompletedAt = DateTime.UtcNow,
                                NextRetryAt = nextRetryAt,
                            };

                            db.DeliveryAttempts.Add(attempt);
                            evt.Status = newEventStatus;                         

                            await db.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation("Взял в работу {Count} событий", events.Count);

                            await Task.Delay(3000, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в воркере");
                }
            }
        }
    }
}
