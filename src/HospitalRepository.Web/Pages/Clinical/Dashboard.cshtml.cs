
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace HospitalRepository.Web.Pages.Clinical;

[Authorize(Roles = "Provider,ClinicalOfficer")]
public class DashboardModel : PageModel
{
    public string StaffName { get; set; } = string.Empty;

    public string StaffRole { get; set; } = string.Empty;

    public string InstitutionId { get; set; } = string.Empty;

    public void OnGet()
    {
        // Get the logged-in staff member's name.
        StaffName = User.Identity?.Name ?? "Healthcare Staff";

        // Get the staff member's assigned role.
        StaffRole = User.FindFirst(ClaimTypes.Role)?.Value
                    ?? User.FindFirst("role")?.Value
                    ?? "Healthcare Staff";

        // Get the institution associated with the staff account.
        InstitutionId = User.FindFirst("InstitutionId")?.Value
                        ?? User.FindFirst("institutionId")?.Value
                        ?? string.Empty;
    }
}