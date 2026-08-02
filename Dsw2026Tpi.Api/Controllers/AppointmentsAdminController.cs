using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2026Tpi.Api.Controllers;

[Route("api/appointments")]
[Authorize(Policy = Policies.AdminPolicy)]
public class AppointmentsAdminController : AppController
{
    private readonly IAppointmentService _service;

    public AppointmentsAdminController(IAppointmentService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByDate(
        [FromQuery] DateOnly? date,
        [FromQuery] int pageSize,
        [FromQuery] int pageIndex)
    {
        var response = await _service.GetByDate(date, pageSize, pageIndex);
        return Ok(response);
    }

    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] int pageSize,
        [FromQuery] int pageIndex,
        [FromQuery] Guid? specialtyId,
        [FromQuery] Guid? doctorId,
        [FromQuery] long? dni,
        [FromQuery] DateOnly? date)
    {
        var response = await _service.Search(pageSize, pageIndex, specialtyId, doctorId, dni, date);
        return Ok(response);
    }

    [HttpPut("{id:guid}/attend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Attend([FromRoute] Guid id)
    {
        await _service.Attend(id);
        return Ok("ok");
    }

    [HttpPut("{id:guid}/no-show")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkNoShow([FromRoute] Guid id)
    {
        await _service.MarkNoShow(id);
        return Ok("ok");
    }
}