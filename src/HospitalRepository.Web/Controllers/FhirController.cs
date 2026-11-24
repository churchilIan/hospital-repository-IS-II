using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HospitalRepository.Application;

namespace HospitalRepository.Web.Controllers;

[ApiController]
[Route("api/fhir")]
[Authorize]
public sealed class FhirController(IFhirService fhir) : ControllerBase
{
    public sealed record FhirPayloadRequest(string Json, Guid? PatientId);

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(FhirPayloadRequest request, CancellationToken cancellationToken) => Ok(await fhir.ValidateAsync(request.Json, cancellationToken));

    [HttpPost("exchange")]
    [Authorize(Roles = "Administrator,InstitutionAdmin,Provider,LaboratoryScientist,Pharmacist")]
    public async Task<IActionResult> Exchange(FhirPayloadRequest request, CancellationToken cancellationToken)
    {
        var result = await fhir.ExchangeAsync(request.Json, request.PatientId, cancellationToken);
        return result.Succeeded ? Ok(result) : UnprocessableEntity(result);
    }

    [HttpGet("resources")]
    public async Task<IActionResult> ListLocal(CancellationToken cancellationToken) => Ok(await fhir.ListLocalAsync(cancellationToken));
}
