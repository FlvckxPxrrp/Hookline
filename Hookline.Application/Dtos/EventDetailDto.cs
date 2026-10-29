using Hookline.Domain.Entities;

namespace Hookline.Application.Dtos
{
    public class EventDetailDto
    {
        public Guid Id { get; set; }
        public string Source { get; set; } = default!;
        public string ExternalId { get; set; } = default!;
        public string EventType { get; set; } = default!;
        public string Headers { get; set; } = default!;
        public string Payload { get; set; } = default!;
        public DateTime ReceivedAt { get; set; }
        public DateTime? NextRetryAt { get; set; }
        public DateTime? LockedUntil { get; set; }
        public string Status { get; set; } = "Received";

        public List<DeliveryAttemptDto> Attempts { get; set; } = new();
    }
}
