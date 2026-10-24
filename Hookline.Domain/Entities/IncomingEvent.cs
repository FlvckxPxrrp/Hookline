namespace Hookline.Domain.Entities
{
    public class IncomingEvent
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

        public List<DeliveryAttempt> DeliveryAttempts { get; set; } = new();
    }
}
