
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HospitalRepository.Infrastructure;

namespace HospitalRepository.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class DashboardModel : PageModel
{
    private readonly PlatformDbContext _db;

    public DashboardModel(PlatformDbContext db)
    {
        _db = db;
    }

    public int TotalHospitals { get; set; }
    public int TotalPatients { get; set; }
    public int TotalProviders { get; set; }
    public int TotalUsers { get; set; }

    public async Task OnGetAsync()
    {
        TotalHospitals = await _db.Institutions.CountAsync();

        TotalPatients = await _db.Patients.CountAsync();

        TotalProviders = await _db.Providers.CountAsync();

        TotalUsers = await _db.Users.CountAsync();
    }
}
