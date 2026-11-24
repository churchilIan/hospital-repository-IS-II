
using HospitalRepository.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace HospitalRepository.Web.Pages.Institution;

[Authorize(Roles = "InstitutionAdmin")]
public class DashboardModel : PageModel
{
    private readonly IInstitutionService _institutionService;

    public DashboardModel(IInstitutionService institutionService)
    {
        _institutionService = institutionService;
    }

    public InstitutionDto? Hospital { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Get the hospital ID from the logged-in administrator's claims.
        var institutionIdClaim = User.FindFirst("InstitutionId")?.Value;

        if (!Guid.TryParse(institutionIdClaim, out var institutionId))
        {
            return;
        }

        // Retrieve the hospital associated with this administrator.
        Hospital = await _institutionService.GetAsync(
            institutionId,
            cancellationToken);
    }
}
