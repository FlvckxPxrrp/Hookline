using Hookline.Application.Services;
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
                        .Where(e => e.Status == "Received")
                        .OrderBy(e => e.ReceivedAt)
                        .Take(10)
                        .ToListAsync(stoppingToken);

                    if (!events.Any())
                        continue;

                    foreach (var evt in events)
                    {
                        string testUrl = "https://webhook.site/67ee01dc-0073-4bb7-a924-fe6350e258e6";

                        var result = await deliveryService.DeliveryAsync(testUrl, evt.Payload, stoppingToken);

                        if (result.IsSuccess)
                        {
                            evt.Status = "Delivered";
                            _logger.LogInformation("Событие {EventId} успешно доставлено.", evt.Id);
                        }
                        else
                        {
                            if (result.StatusCode >= 400 && result.StatusCode < 500 && result.StatusCode != 429)
                            {
                                evt.Status = "Dead";
                                _logger.LogWarning("Событие {EventId} получило 4xx ({Code}). Отправляем в Dead.", evt.Id, result.StatusCode);
                            }
                            else
                            {
                                evt.Status = "Pending";
                                _logger.LogWarning("Событие {EventId} не доставлено: {Error}. Вернули в Pending.", evt.Id, result.Error);
                            }
                        }
                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Взял в работу {Count} событий", events.Count);

                        await Task.Delay(3000, stoppingToken);
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
