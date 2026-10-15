namespace Hookline.Domain.Entities
{
    public class DeliveryAttempt
    {
        public Guid Id {  get; set; }
        public Guid EventId { get; set; }
        public Guid SubscriberId { get; set; }
        public int AttemptNumber { get; set; }
        public string Status { get; set; } = "Pending";
        public int? ResponseCode { get; set; }
        public string? ResponseBody { get; set; }
        public string? Error { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt{ get;set; }
        public DateTime? NextRetryAt { get; set; }

        public IncomingEvent Event { get; set; } = default!;
        public Subscriber Subscriber { get; set; } = default!;
    }
}
