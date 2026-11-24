using HospitalRepository.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HospitalRepository.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class InstitutionsModel : PageModel
{
    private readonly IInstitutionService _institutions;

    public InstitutionsModel(IInstitutionService institutions)
    {
        _institutions = institutions;
    }

    public IReadOnlyCollection<InstitutionDto> Institutions { get; set; }
        = Array.Empty<InstitutionDto>();

    [BindProperty]
    public InstitutionInput Input { get; set; } = new();

    public class InstitutionInput
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? County { get; set; }
        public string? SubCounty { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadInstitutions(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        if (!CoordinatesAreValid(Input.Latitude, Input.Longitude))
        {
            ModelState.AddModelError(string.Empty, "Enter both latitude and longitude, or leave both fields empty.");
        }

        if (!ModelState.IsValid)
        {
            await LoadInstitutions(cancellationToken);
            return Page();
        }

        try
        {
            var request = new CreateInstitutionRequest(
                Input.Name, Input.Code, Input.Address, Input.County, Input.SubCounty,
                Input.Latitude, Input.Longitude);

            await _institutions.CreateAsync(request, cancellationToken);
            TempData["Success"] = "Hospital registered successfully.";
            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadInstitutions(cancellationToken);
            return Page();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadInstitutions(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        Guid id, string name, string code, string? address, string? county,
        string? subCounty, double? latitude, double? longitude,
        CancellationToken cancellationToken)
    {
        if (!CoordinatesAreValid(latitude, longitude))
        {
            TempData["Error"] = "Enter both latitude and longitude, or leave both fields empty.";
            return RedirectToPage();
        }

        try
        {
            var request = new CreateInstitutionRequest(
                name, code, address, county, subCounty, latitude, longitude);

            var result = await _institutions.UpdateAsync(id, request, cancellationToken);
            if (result is null) return NotFound();

            TempData["Success"] = "Hospital details updated successfully.";
            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage();
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostChangeStatusAsync(
        Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await _institutions.SetActiveStatusAsync(id, isActive, cancellationToken);
        if (!result) return NotFound();

        TempData["Success"] = isActive
            ? "Hospital activated successfully."
            : "Hospital deactivated successfully.";
        return RedirectToPage();
    }

    private static bool CoordinatesAreValid(double? latitude, double? longitude)
    {
        if (latitude.HasValue != longitude.HasValue) return false;
        if (!latitude.HasValue) return true;
        return latitude.Value >= -90 && latitude.Value <= 90
            && longitude!.Value >= -180 && longitude.Value <= 180;
    }

    private async Task LoadInstitutions(CancellationToken cancellationToken)
    {
        Institutions = await _institutions.ListAsync(cancellationToken);
    }
}
