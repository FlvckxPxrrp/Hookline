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

                    var events = await db.IncomingEvents
                        .Where(e => e.Status == "Received")
                        .OrderBy(e => e.ReceivedAt)
                        .Take(10)
                        .ToListAsync(stoppingToken);

                    if (!events.Any())
                        continue;

                    foreach(var item in events)
                    {
                        item.Status = "Processing";
                    }
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Взял в работу {Count} событий", events.Count);

                    await Task.Delay(3000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в воркере");
                }
            }
        }
    }
}
