namespace Hookline.Domain.Entities
{
    public class IncomingEvent
    {
        public Guid Id { get; set; }
        public string Source { get; set; }
        public string ExternalId { get; set; }
        public string EventType { get; set; }
        public string Headers { get; set; }
        public string Paylad { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string Status { get; set; }
    }
}
