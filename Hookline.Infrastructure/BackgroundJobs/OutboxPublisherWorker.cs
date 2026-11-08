using Hookline.Application.Services;
using Hookline.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Hookline.Infrastructure.BackgroundJobs
{
    public class OutboxPublisherWorker : BackgroundService                                          
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnection _rabbitConnection;
        private readonly ILogger<OutboxPublisherWorker> _logger;

        private const string ExchangeName = "webhook_exchange";
        private const string QueueName = "webhook_delivery_queue";
        private const string RoutingKey = "webhook.new";

        public OutboxPublisherWorker(IServiceScopeFactory scopeFactory,
        IConnection rabbitConnection, ILogger<OutboxPublisherWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _rabbitConnection = rabbitConnection;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox Publisher запущен.");

            using var channel = await _rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Direct,
            durable: true, 
            autoDelete: false,
            cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: RoutingKey,
                cancellationToken: stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();

                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var messages = await db.OutboxMessages
                    .Where(m => !m.Processed)
                    .OrderBy(m => m.CreatedAt)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                    if(messages.Count > 0)
                    {
                        foreach (var message in messages)
                        {
                            var body = Encoding.UTF8.GetBytes(message.Payload);
                            await channel.BasicPublishAsync(
                                exchange: ExchangeName,
                                routingKey: RoutingKey,
                                body: body,
                                cancellationToken: stoppingToken);

                            message.Processed = true;
                            message.ProcessedAt = DateTime.UtcNow;
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Опубликовано {Count} сообщений в RabbitMQ.", messages.Count);
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при публикации сообщений в RabbitMQ.");

                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
