using Hookline.Application.Dtos;
using Hookline.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Hookline.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EventsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]      
        public async Task<ActionResult<PagedResult<EventSummaryDto>>> GetEvents(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
            CancellationToken ct =default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) page = 10;
            if(pageSize>100) pageSize = 100;

            var totalCount = await _context.IncomingEvents.CountAsync(ct); //для фронта

            var items = await _context.IncomingEvents
                .AsNoTracking()
                .OrderByDescending(e=>e.ReceivedAt)
                .Skip((page-1) * pageSize)
                .Take(pageSize)
                .Select(e=> new EventSummaryDto
                {
                    Id = e.Id,
                    Source = e.Source,
                    EventType = e.EventType,
                    Status = e.Status,
                    ReceivedAt = e.ReceivedAt,
                }).ToListAsync();

            var result = new PagedResult<EventSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<List<EventSummaryDto>>> GetEventsById(
            [FromBody] Guid id,
            CancellationToken ct)
        {
            var evt = await _context.IncomingEvents
                .AsNoTracking()
                .Include(e => e.DeliveryAttempts)
                .FirstOrDefaultAsync(e => e.Id == id, ct);

            if (evt == null)
                return NotFound(new { error = "Event not found" });

            var dto = new EventDetailDto
            {
                Id = evt.Id,
                Source = evt.Source,
                ExternalId = evt.ExternalId,
                EventType = evt.EventType,
                Headers = evt.Headers,
                Payload = evt.Payload,
                ReceivedAt = evt.ReceivedAt,
                Status = evt.Status,
                NextRetryAt = evt.NextRetryAt,
                Attempts = evt.DeliveryAttempts.Select(a => new DeliveryAttemptDto
                {
                    Id = a.Id,
                    AttemptNumber = a.AttemptNumber,
                    Status = a.Status,
                    ResponseCode = a.ResponseCode,
                    Error = a.Error,
                    CreatedAt = a.CreatedAt,
                    CompletedAt = a.CompletedAt
                }).ToList()
            };

            return Ok(dto);
        }

        [HttpPost("{id}/retry")]
        public async Task<ActionResult<EventSummaryDto>> RetryEvent(Guid id, CancellationToken ct)
        {
            var evt = await _context.IncomingEvents.FirstOrDefaultAsync(e => e.Id == id, ct);

            if (evt == null) return NotFound(new { error = "Event not Found" });

            evt.Status = "Pending";
            evt.NextRetryAt = DateTime.UtcNow;
            evt.LockedUntil = null;

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
