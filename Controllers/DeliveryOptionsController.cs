using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SerilogDemo.Data;
using SerilogDemo.DTOs;

namespace SerilogDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeliveryOptionsController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly ILogger<DeliveryOptionsController> _logger;

    public DeliveryOptionsController(EcommerceDbContext context, ILogger<DeliveryOptionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all available delivery options.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeliveryOptionDto>>> GetDeliveryOptions([FromQuery] string? courier = null)
    {
        _logger.LogInformation("Fetching delivery options. Courier filter: {Courier}", courier ?? "none");

        var query = _context.DeliveryOptions
            .Where(d => d.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(courier))
        {
            query = query.Where(d => d.CourierName.ToLower() == courier.ToLower());
        }

        var options = await query
            .OrderBy(d => d.CourierName)
            .ThenBy(d => d.Price)
            .Select(d => new DeliveryOptionDto(
                d.Id,
                d.CourierName,
                d.Name,
                d.Description,
                d.Price,
                d.EstimatedDaysMin,
                d.EstimatedDaysMax
            ))
            .ToListAsync();

        _logger.LogInformation("Returning {Count} delivery options", options.Count);
        return Ok(options);
    }

    /// <summary>
    /// Get a specific delivery option by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeliveryOptionDto>> GetDeliveryOption(Guid id)
    {
        _logger.LogInformation("Fetching delivery option {DeliveryOptionId}", id);

        var option = await _context.DeliveryOptions.FindAsync(id);

        if (option is null || !option.IsActive)
        {
            _logger.LogWarning("Delivery option {DeliveryOptionId} not found", id);
            return NotFound(new { message = $"Delivery option with ID {id} not found" });
        }

        _logger.LogInformation("Returning delivery option {DeliveryOptionId}: {Name}", id, option.Name);
        return Ok(new DeliveryOptionDto(
            option.Id,
            option.CourierName,
            option.Name,
            option.Description,
            option.Price,
            option.EstimatedDaysMin,
            option.EstimatedDaysMax
        ));
    }

    /// <summary>
    /// Get available courier companies.
    /// </summary>
    [HttpGet("couriers")]
    public async Task<ActionResult<IEnumerable<string>>> GetCouriers()
    {
        _logger.LogInformation("Fetching available courier companies");

        var couriers = await _context.DeliveryOptions
            .Where(d => d.IsActive)
            .Select(d => d.CourierName)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        _logger.LogInformation("Returning {Count} courier companies", couriers.Count);
        return Ok(couriers);
    }
}
