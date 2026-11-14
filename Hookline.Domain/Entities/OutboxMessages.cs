namespace Hookline.Domain.Entities
{
    public class OutboxMessages
    {
        public Guid Id { get; set; }
        public string MessageType { get; set; }
        public string Payload { get; set; }
        public bool Processed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
