
using Hookline.Application.Services;
using Hookline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Hookline.Infrastructure.BackgroundJobs
{
    public class RabbitMqConsumerWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnection _rabbitConnection;
        private readonly IWebhookDeliveryService _deliveryService;
        private readonly ILogger<RabbitMqConsumerWorker> _logger;

        private const string QueueName = "webhook_delivery_queue";

        public RabbitMqConsumerWorker(
        IServiceScopeFactory scopeFactory,
        IConnection rabbitConnection,
        IWebhookDeliveryService deliveryService,
        ILogger<RabbitMqConsumerWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _rabbitConnection = rabbitConnection;
            _deliveryService = deliveryService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RabbitMQ Консьюмер запущен. Ожидание сообщений...");

            using var channel = await _rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10,
                global: false, cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false,
                autoDelete: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);

                    var payload = JsonSerializer.Deserialize<OutboxPayload>(messageJson);

                    if (payload == null || payload.EventId == Guid.Empty)
                    {
                        _logger.LogError("Получено невалидное сообщение из RabbitMQ");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, stoppingToken);
                        return;
                    }

                    _logger.LogInformation("Получено сообщение из RabbitMQ для события {EventId}",
                        payload.EventId);

                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var evt = await db.IncomingEvents
                        .Include(e => e.DeliveryAttempts)
                        .FirstOrDefaultAsync(e => e.Id == payload.EventId, stoppingToken);

                    if (evt == null)
                    {
                        _logger.LogWarning("Событие {EventId} не найдено в БД. Возможно, оно было удалено.",
                            payload.EventId);

                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    if (evt.Status == "Delivered")
                    {
                        _logger.LogInformation("Событие {EventId} уже доставлено ранее. Пропускаем.", evt.Id);
                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    evt.Status = "Processing";
                    evt.LockedUntil = DateTime.UtcNow.AddMinutes(5);

                    var subscribers = await db.Subscribers
                        .Where(e => e.Source == evt.Source && e.IsActive)
                        .ToListAsync(stoppingToken);

                    if (subscribers.Count == 0)
                    {
                        evt.Status = "Delivered"; // Некому отправлять
                        await db.SaveChangesAsync(stoppingToken);
                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    bool allSucceess = true;

                    foreach (var sub in subscribers)
                    {
                        var result = await _deliveryService.DeliveryAsync(sub.Url, evt.Payload, stoppingToken);

                        if (!result.IsSuccess) allSucceess = false;

                        var attemptCount = await db.DeliveryAttempts.CountAsync
                        (a => a.EventId == evt.Id && a.SubscriberId == sub.Id, stoppingToken) + 1;


                        db.DeliveryAttempts.Add(new Domain.Entities.DeliveryAttempt
                        {
                            Id = Guid.NewGuid(),
                            EventId = evt.Id,
                            SubscriberId = sub.Id,
                            AttemptNumber = attemptCount,
                            Status = result.IsSuccess ? "Delivered" : "Failed",
                            ResponseCode = result.StatusCode,
                            Error = result.Error,
                            CreatedAt = DateTime.UtcNow,
                            CompletedAt = DateTime.UtcNow
                        });
                    }
                    if (allSucceess)
                    {
                        evt.Status = "Delivered";
                        evt.LockedUntil = null;
                    }
                    else
                    {
                        evt.Status = "Pending"
                            evt.NextRetryAt = DateTime.UtcNow.AddMinutes(1);
                        evt.LockedUntil = null;
                    }

                    await db.SaveChangesAsync(stoppingToken);

                    await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    _logger.LogInformation("Сообщение для события {EventId} успешно обработано и подтверждено.", evt.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Критическая ошибка при обработке сообщения из RabbitMQ");
                    // Если произошла системная ошибка (БД недоступна), возвращаем сообщение в очередь
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true, stoppingToken);
                }
            };

            await channel.BasicConsumeAsync
                (queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);

        }

        private class OutboxPayload
        {
            public Guid EventId { get; set; }
        }
    }
}
