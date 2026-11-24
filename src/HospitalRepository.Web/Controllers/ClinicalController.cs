
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HospitalRepository.Application;

namespace HospitalRepository.Web.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class ClinicalController(IClinicalService clinical) : ControllerBase
{
    // List clinical encounters for a patient.
    [HttpGet("encounters")]
    public async Task<IActionResult> ListEncounters(
        [FromQuery] Guid? patientId,
        CancellationToken cancellationToken)
    {
        return Ok(
            await clinical.ListEncountersAsync(patientId, cancellationToken)
        );
    }

    // Create a new clinical encounter.
    [HttpPost("encounters")]
    [Authorize(Roles = "Administrator,InstitutionAdmin,Provider,ClinicalOfficer")]
    public async Task<IActionResult> CreateEncounter(
        CreateEncounterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await clinical.CreateEncounterAsync(
            request,
            cancellationToken
        );

        return StatusCode(StatusCodes.Status201Created, result);
    }

    // List laboratory results for a patient.
    [HttpGet("laboratory-results")]
    public async Task<IActionResult> ListLaboratoryResults(
        [FromQuery] Guid? patientId,
        CancellationToken cancellationToken)
    {
        return Ok(
            await clinical.ListLaboratoryResultsAsync(patientId, cancellationToken)
        );
    }

    // Create a laboratory result.
    [HttpPost("laboratory-results")]
    [Authorize(Roles = "Administrator,InstitutionAdmin,Provider,LaboratoryScientist")]
    public async Task<IActionResult> CreateLaboratoryResult(
        CreateLaboratoryResultRequest request,
        CancellationToken cancellationToken)
    {
        var result = await clinical.CreateLaboratoryResultAsync(
            request,
            cancellationToken
        );

        return StatusCode(StatusCodes.Status201Created, result);
    }

    // List medication records for a patient.
    [HttpGet("medications")]
    public async Task<IActionResult> ListMedications(
        [FromQuery] Guid? patientId,
        CancellationToken cancellationToken)
    {
        return Ok(
            await clinical.ListMedicationsAsync(patientId, cancellationToken)
        );
    }

    // Create a medication record.
    [HttpPost("medications")]
    [Authorize(Roles = "Administrator,InstitutionAdmin,Provider,Pharmacist")]
    public async Task<IActionResult> CreateMedication(
        CreateMedicationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await clinical.CreateMedicationAsync(
            request,
            cancellationToken
        );

        return StatusCode(StatusCodes.Status201Created, result);
    }

    // Generate clinical analysis from a patient's laboratory history.
    [HttpGet("patients/{patientId:guid}/clinical-analysis")]
    public async Task<IActionResult> AnalysePatient(
        Guid patientId,
        CancellationToken cancellationToken)
    {
        var result = await clinical.AnalysePatientAsync(
            patientId,
            cancellationToken
        );

        if (result is null)
            return NotFound(new { message = "Patient not found." });

        return Ok(result);
    }
}
