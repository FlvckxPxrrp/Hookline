namespace Hookline.Application.Dtos
{
    public class StatisticsDto
    {
        public int TotalEvents { get; set; }
        public int DeliveredEvents { get; set; }
        public int FailedEvents { get; set; }
        public int PendingEvents{ get; set; }
        public double SuccessRate { get; set; }
    }
}
