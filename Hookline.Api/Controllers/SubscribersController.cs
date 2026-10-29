using Hookline.Application.Dtos;
using Hookline.Application.Services;
using Hookline.Domain.Entities;
using Hookline.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.WebSockets;

namespace Hookline.Api.Controllers
{
    [ApiController]
    [Route("api/subscribers")]
    public class SubscribersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IUrlValidator _urlValidator;

        public SubscribersController
            (AppDbContext context, IUrlValidator urlValidator)
        {
            _context = context;
            _urlValidator = urlValidator;
        }

        [HttpPost]
        public async Task<ActionResult<bool>> CreateSubscriber([FromBody] CreateSubscriberDto dto)
        {
            if (!await _urlValidator.IsSafe(dto.Url))
                return BadRequest(new { error = "Url is not safe or invalid" });

            var subscriber = new Subscriber
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Url = dto.Url,
                Secret = dto.Secret,
                Source = dto.Source,
                IsActive = true, // По умолчанию активен
                CreatedAt = DateTime.UtcNow
            };

            _context.Subscribers.Add(subscriber);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSubscriber), new { id = subscriber.Id }, new { id = subscriber.Id });
        }

        [HttpGet]
        public async Task<ActionResult<List<SubscriberResponseDto>>> GetAllSubscriber()
        {
            var subscribers = await _context.Subscribers
                .AsNoTracking()
                .Select(s => new SubscriberResponseDto
                {
                    Name = s.Name,
                    Url = s.Url,
                    Source = s.Source,
                    IsActive = s.IsActive,
                    CreatedAt = s.CreatedAt
                }).ToListAsync();

            return Ok(subscribers);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SubscriberResponseDto>> GetSubscriber(Guid id)
        {
            var subscriber = await _context.Subscribers.
                AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

            if (subscriber == null)
                return NotFound(new { error = "Subscriber is not found" });

            return Ok(subscriber);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SubscriberResponseDto>> UpdateSubscriber
            (Guid id, [FromBody] UpdateSubscriberDto dto)
        {
            var subscriber = await _context.Subscribers.FindAsync(id);

            if (subscriber == null)
                return NotFound(new { error = "subscriber is not found" });

            subscriber.Id = id;
            subscriber.Name = dto.Name;
            subscriber.Url = dto.Url;
            subscriber.Secret = dto.Secret;
            subscriber.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<bool> DeleteSubscriber(Guid id)
        {
            var subscriber = await _context.Subscribers.FindAsync(id);
            if (subscriber == null)
                return false;

            subscriber.IsActive = false;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}