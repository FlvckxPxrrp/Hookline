using System.Linq.Expressions;

namespace Hookline.Domain.Entities
{
    public class Subscriber
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Url { get; set; } = default!;
        public string Secret { get; set; } = default!;
        public string Source { get; set; } = default!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt {  get; set; }

        public List<DeliveryAttempt> DeliveryAttempts { get; set; } = new();
    }
}
