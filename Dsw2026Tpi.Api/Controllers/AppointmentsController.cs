using System.Security.Claims;
using Dsw2026Tpi.Api.Configurations;
using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dsw2026Tpi.Api.Controllers;

[Route("api/appointments")]
[Authorize(Policy = Policies.PatientPolicy)]
public class AppointmentsController : AppController
{
    private readonly IAppointmentService _service;

    public AppointmentsController(IAppointmentService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [EnableRateLimiting(RateLimitingConfigurationExtensions.AppointmentBookingPolicy)]
    public async Task<IActionResult> Create([FromBody] AppointmentModel.Request request)
    {
        var response = await _service.Create(request, GetAuthenticatedEmail());
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("patient")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByPatient([FromQuery] long dni)
    {
        var response = await _service.GetByPatient(dni, GetAuthenticatedEmail());
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel([FromRoute] Guid id)
    {
        await _service.Cancel(id, GetAuthenticatedEmail());
        return Ok("ok");
    }

    private string GetAuthenticatedEmail()
    {
        return User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
    }
}
