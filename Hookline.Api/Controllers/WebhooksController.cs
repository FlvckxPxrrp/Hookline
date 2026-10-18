using Hookline.Application.Services;
using Hookline.Domain.Entities;
using Hookline.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Hookline.Api.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class WebhooksController : ControllerBase
    {
        private readonly ILogger<WebhooksController> _logger;
        private readonly AppDbContext _context;
        private readonly ISignatureValidator _signatureValidator;

        public WebhooksController(ILogger<WebhooksController> logger, AppDbContext context,
            ISignatureValidator signatureValidator) 
        {
            _logger = logger;
            _context = context;
            _signatureValidator = signatureValidator;
        }
        
        [HttpPost("{source}")]
        public async Task<ActionResult> Receive(string source, CancellationToken ct)
        {
            Request.EnableBuffering();

            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(ct);

            Request.Body.Position = 0;

            var headers = Request.Headers.ToDictionary(h=>h.Key, h => h.Value.ToString());

            var externalId = headers.TryGetValue("X-GitHub-Delivery", out var deliveryId)
                ? deliveryId
                : Guid.NewGuid().ToString();

            var eventType = headers.TryGetValue("X-GitHub-Event", out var evtType)
                ? evtType
                : "unknow";

            if (!_signatureValidator.IsValid(source, rawBody, headers))
            {
                _logger.LogWarning("Отклонен вебхук от {Source}: неверная подпись", source);

                return Unauthorized(new { error = "Invalid signature" });
            }

            var incomingEvent = new IncomingEvent
            {
                Id = Guid.NewGuid(),
                Source = source,
                ExternalId = externalId,
                EventType = eventType,
                Headers = JsonSerializer.Serialize(headers),
                Payload = rawBody,
                ReceivedAt = DateTime.UtcNow,
                Status = "Received"
            };

            _context.IncomingEvents.Add(incomingEvent);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Webhook received: source={Source}, eventType={EventType}, externalId={ExternalId}, eventId={EventId}",
            source, eventType, externalId, incomingEvent.Id);

            return Accepted(new {eventId = incomingEvent.Id});
        }        
    }
}
