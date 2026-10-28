namespace Hookline.Application.Dtos
{
    public class SubscriberResponseDto
    {
        public string Name { get; set; } = default!;
        public string Url { get; set; } = default!;      
        public string Source { get; set; } = default!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
