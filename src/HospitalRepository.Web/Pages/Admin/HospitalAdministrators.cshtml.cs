
using HospitalRepository.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HospitalRepository.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class HospitalAdministratorsModel : PageModel
{
    private readonly IUserService _userService;
    private readonly IInstitutionService _institutionService;

    public HospitalAdministratorsModel(
        IUserService userService,
        IInstitutionService institutionService)
    {
        _userService = userService;
        _institutionService = institutionService;
    }

    public List<SelectListItem> Hospitals { get; set; } = new();

    [BindProperty]
    public RegisterUserRequest Input { get; set; } = new(
        Email: "",
        Password: "",
        DisplayName: "",
        Role: "InstitutionAdmin",
        InstitutionId: null
    );

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadHospitalsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(
        CancellationToken cancellationToken)
    {
        await LoadHospitalsAsync(cancellationToken);

        var result = await _userService.CreateInstitutionAdminAsync(
            Input,
            cancellationToken);

        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
            return Page();
        }

        SuccessMessage = "Hospital administrator account created successfully.";

        Input = new RegisterUserRequest(
            Email: "",
            Password: "",
            DisplayName: "",
            Role: "InstitutionAdmin",
            InstitutionId: null
        );

        return Page();
    }

    private async Task LoadHospitalsAsync(
        CancellationToken cancellationToken)
    {
        var institutions = await _institutionService.ListAsync(cancellationToken);

        Hospitals = institutions
            .Where(x => x.IsActive)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.Name} ({x.Code})"
            })
            .ToList();
    }
}
