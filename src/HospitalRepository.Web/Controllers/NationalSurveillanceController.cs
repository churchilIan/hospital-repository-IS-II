
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using HospitalRepository.Application;
using HospitalRepository.Infrastructure;

namespace HospitalRepository.Web.Controllers;

[ApiController]
[Route("api/surveillance")]
[Authorize(Roles = "Administrator,Auditor,InstitutionAdmin,Provider,ClinicalOfficer")]
public sealed class NationalSurveillanceController(
    INationalSurveillanceService surveillance,
    PlatformDbContext db) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? county,
        [FromQuery] string? institutionCode,
        [FromQuery] string? diagnosis,
        CancellationToken cancellationToken)
    {
        try
        {
            // Administrators and auditors retain national access.
            var hasNationalAccess =
                User.IsInRole("Administrator") ||
                User.IsInRole("Auditor");

            if (!hasNationalAccess)
            {
                // Hospital staff must have an InstitutionId claim
                // in their authenticated JWT.
                var institutionClaim =
                    User.FindFirst("InstitutionId")?.Value;

                if (!Guid.TryParse(
                    institutionClaim,
                    out var institutionId))
                {
                    return Forbid();
                }

                // Confirm that the hospital exists and is active.
                var ownInstitutionCode = await db.Institutions
                    .AsNoTracking()
                    .Where(x =>
                        x.Id == institutionId &&
                        x.IsActive)
                    .Select(x => x.Code)
                    .SingleOrDefaultAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(ownInstitutionCode))
                {
                    return Forbid();
                }

                // Always override the requested hospital code.
                // This prevents hospital staff from selecting
                // another hospital through the URL.
                institutionCode = ownInstitutionCode;
            }

            var result = await surveillance.GetOverviewAsync(
                fromUtc,
                toUtc,
                county,
                institutionCode,
                diagnosis,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
