
using HospitalRepository.Application;
using HospitalRepository.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HospitalRepository.Web.Pages.Institution;

[Authorize(Roles = "InstitutionAdmin")]
public class StaffModel : PageModel
{
    private readonly IProviderService _providerService;
    private readonly IUserService _userService;

    public StaffModel(
        IProviderService providerService,
        IUserService userService)
    {
        _providerService = providerService;
        _userService = userService;
    }

    public IReadOnlyCollection<ProviderDto> StaffMembers { get; set; }
        = Array.Empty<ProviderDto>();

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public StaffInput Input { get; set; } = new();

    public class StaffInput
    {
        public string GivenName { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string RegistrationNumber { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public ProviderRole Role { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetInstitutionId(out var institutionId))
            return Forbid();

        StaffMembers = await _providerService.ListAsync(
            institutionId, cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetInstitutionId(out var institutionId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(Input.GivenName) ||
            string.IsNullOrWhiteSpace(Input.FamilyName) ||
            string.IsNullOrWhiteSpace(Input.RegistrationNumber) ||
            string.IsNullOrWhiteSpace(Input.Email) ||
            string.IsNullOrWhiteSpace(Input.Password))
        {
            ErrorMessage = "Please fill in all the required fields.";
            StaffMembers = await _providerService.ListAsync(
                institutionId, cancellationToken);
            return Page();
        }

        try
        {
            var request = new RegisterHealthcareStaffRequest(
                Input.Email.Trim(),
                Input.Password,
                Input.GivenName.Trim(),
                Input.FamilyName.Trim(),
                Input.RegistrationNumber.Trim(),
                Input.Role
            );

            var result = await _userService.CreateHealthcareStaffAsync(
                request,
                institutionId,
                cancellationToken
            );

            if (!result.Succeeded)
            {
                ErrorMessage = result.Error ?? "Staff registration failed.";
            }
            else
            {
                SuccessMessage =
                    "Staff account and provider record created successfully.";

                Input = new StaffInput();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        StaffMembers = await _providerService.ListAsync(
            institutionId, cancellationToken);

        return Page();
    }

    private bool TryGetInstitutionId(out Guid institutionId)
    {
        var claim = User.FindFirst("InstitutionId")?.Value;

        return Guid.TryParse(claim, out institutionId);
    }
}
