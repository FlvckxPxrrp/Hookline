using Hookline.Application.Dtos;
using Hookline.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Validations;

namespace Hookline.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StatisticsController(AppDbContext context) =>
            _context = context;

        [HttpGet]
        public async Task<ActionResult<StatisticsDto>> Statistics()
        {
            var totalEvents = await _context.IncomingEvents.CountAsync();

            var deliveredEvents = await _context.IncomingEvents
                .Where(x => x.Status == "Delivered").CountAsync();

            var failedEvents = await _context.IncomingEvents
                .Where(x => x.Status == "Failed").CountAsync();

            var pendingEvents = await _context.IncomingEvents
                .Where(x => x.Status == "Pending").CountAsync();

            var successRate = (deliveredEvents / totalEvents) * 100;

            var result = new StatisticsDto
            {
                TotalEvents = totalEvents,
                DeliveredEvents = deliveredEvents,
                FailedEvents = failedEvents,
                PendingEvents = pendingEvents,
                SuccessRate = successRate
            };

            return result;
        }
    }
}
