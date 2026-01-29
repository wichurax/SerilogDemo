using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SerilogDemo.Data;
using SerilogDemo.DTOs;

namespace SerilogDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentOptionsController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly ILogger<PaymentOptionsController> _logger;

    public PaymentOptionsController(EcommerceDbContext context, ILogger<PaymentOptionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all available payment options.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentOptionDto>>> GetPaymentOptions()
    {
        _logger.LogInformation("Fetching payment options");

        var options = await _context.PaymentOptions
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new PaymentOptionDto(
                p.Id,
                p.Name,
                p.Description,
                p.Icon
            ))
            .ToListAsync();

        _logger.LogInformation("Returning {Count} payment options", options.Count);
        return Ok(options);
    }

    /// <summary>
    /// Get a specific payment option by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentOptionDto>> GetPaymentOption(Guid id)
    {
        _logger.LogInformation("Fetching payment option {PaymentOptionId}", id);

        var option = await _context.PaymentOptions.FindAsync(id);

        if (option is null || !option.IsActive)
        {
            _logger.LogWarning("Payment option {PaymentOptionId} not found", id);
            return NotFound(new { message = $"Payment option with ID {id} not found" });
        }

        _logger.LogInformation("Returning payment option {PaymentOptionId}: {Name}", id, option.Name);
        return Ok(new PaymentOptionDto(
            option.Id,
            option.Name,
            option.Description,
            option.Icon
        ));
    }
}
