namespace Hookline.Application.Dtos
{
    public class UpdateSubscriberDto
    {
        public string Name { get; set; } = default!;
        public string Url { get; set; } = default!;
        public string Secret { get; set; } = default!;
        public string Source { get; set; } = default!;
        public bool IsActive { get; set; }
    }
}
