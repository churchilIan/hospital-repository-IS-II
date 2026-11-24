using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HospitalRepository.Application;

namespace HospitalRepository.Web.Controllers;

[ApiController]
[Route("api/patients")]
[Authorize]
public sealed class PatientsController(IPatientService patients) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken) => Ok(await patients.SearchAsync(q, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await patients.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,InstitutionAdmin,Provider, ClinicalOfficer")]
    public async Task<IActionResult> Create(CreatePatientRequest request, CancellationToken cancellationToken)
    {
        var result = await patients.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
}
