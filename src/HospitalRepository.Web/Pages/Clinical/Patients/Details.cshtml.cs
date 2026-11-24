
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HospitalRepository.Web.Pages.Clinical.Patients
{
    [Authorize(Roles = "Provider,ClinicalOfficer,Administrator,InstitutionAdmin")]
    public class DetailsModel : PageModel
    {
        public Guid PatientId { get; set; }

        public IActionResult OnGet(Guid id)
        {
            if (id == Guid.Empty)
            {
                return NotFound();
            }

            PatientId = id;

            return Page();
        }
    }
}
