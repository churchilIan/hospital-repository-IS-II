using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HospitalRepository.Application;

namespace HospitalRepository.Web.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = "AuditorOrAdministrator")]
public sealed class AuditController(IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 100, CancellationToken cancellationToken = default) => Ok(await audit.ListAsync(limit, cancellationToken));
}
