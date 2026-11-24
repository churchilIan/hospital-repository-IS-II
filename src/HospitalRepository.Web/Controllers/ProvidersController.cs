
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HospitalRepository.Application;

namespace HospitalRepository.Web.Controllers;

[ApiController]
[Route("api/providers")]
[Authorize(Roles = "Administrator,InstitutionAdmin")]
public sealed class ProvidersController(IProviderService providers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? institutionId,
        CancellationToken cancellationToken)
    {
        // Hospital Administrators can only view their own hospital's staff.
        if (User.IsInRole("InstitutionAdmin"))
        {
            var institutionClaim = User.FindFirst("InstitutionId")?.Value;

            if (!Guid.TryParse(institutionClaim, out var ownInstitutionId))
            {
                return Forbid();
            }

            institutionId = ownInstitutionId;
        }

        var result = await providers.ListAsync(
            institutionId,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateProviderRequest request,
        CancellationToken cancellationToken)
    {
        // Hospital Administrators must use their assigned hospital.
        if (User.IsInRole("InstitutionAdmin"))
        {
            var institutionClaim = User.FindFirst("InstitutionId")?.Value;

            if (!Guid.TryParse(institutionClaim, out var ownInstitutionId))
            {
                return Forbid();
            }

            // Override any InstitutionId supplied by the browser.
            request = request with
            {
                InstitutionId = ownInstitutionId
            };
        }

        try
        {
            var result = await providers.CreateAsync(
                request,
                cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
