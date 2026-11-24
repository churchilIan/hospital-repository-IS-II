using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HospitalRepository.Web.Pages.Patients;

[Authorize(Roles = "Provider,ClinicalOfficer,Administrator,InstitutionAdmin")]
public class DashboardModel : PageModel
{
    public void OnGet()
    {
    }
}