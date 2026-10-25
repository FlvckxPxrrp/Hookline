namespace Hookline.Application.Dtos
{
    public class DeliveryAttemptDto
    {
        public Guid Id { get; set; }
        public int AttemptNumber { get; set; }
        public string Status { get; set; } = "Pending";
        public int? ResponseCode { get; set; }
        public string? Error { get; set; }        
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
