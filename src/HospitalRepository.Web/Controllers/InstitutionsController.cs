
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HospitalRepository.Application;

namespace HospitalRepository.Web.Controllers;

public sealed record UpdateInstitutionStatusRequest(bool IsActive);

[ApiController]
[Route("api/institutions")]
[Authorize]
public sealed class InstitutionsController(IInstitutionService institutions) : ControllerBase
{
    // GET: api/institutions
    // List all hospitals
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        return Ok(await institutions.ListAsync(cancellationToken));
    }

    // GET: api/institutions/{id}
    // Get one hospital
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await institutions.GetAsync(id, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    // POST: api/institutions
    // Register a new hospital
    [HttpPost]
    [Authorize(Policy = "AdministratorOnly")]
    public async Task<IActionResult> Create(
        CreateInstitutionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await institutions.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    // PUT: api/institutions/{id}
    // Update hospital details
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdministratorOnly")]
    public async Task<IActionResult> Update(
        Guid id,
        CreateInstitutionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await institutions.UpdateAsync(id, request, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    // PATCH: api/institutions/{id}/status
    // Activate or deactivate a hospital
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "AdministratorOnly")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateInstitutionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await institutions.SetActiveStatusAsync(
            id,
            request.IsActive,
            cancellationToken);

        return result ? NoContent() : NotFound();
    }
}
