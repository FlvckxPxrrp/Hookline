namespace Hookline.Application.Dtos
{
    public class EventSummaryDto
    {
        public Guid Id { get; set; }
        public string Source { get; set; } = default!;
        public string EventType { get; set; } = default!;
        public string Status { get; set; } = "Received";
        public DateTime ReceivedAt { get; set; }
    }
}
