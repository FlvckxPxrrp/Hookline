using System.Globalization;

namespace Hookline.Application.Dtos
{
    public class CreateSubscriberDto
    {
        public string Name { get; set; } = default!;
        public string Url { get; set; } = default!;
        public string Secret { get; set; } = default!;
        public string Source { get; set; } = default!;
    }
}
