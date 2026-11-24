
using HospitalRepository.Domain.Entities;

namespace HospitalRepository.Application;

public sealed record RegisterHealthcareStaffRequest(
    string Email,
    string Password,
    string GivenName,
    string FamilyName,
    string RegistrationNumber,
    ProviderRole Role
);

public sealed record InstitutionDto(
    Guid Id,
    string Name,
    string Code,
    string? Address,
    string? County,
    string? SubCounty,
    double? Latitude,
    double? Longitude,
    bool IsActive);
public sealed record CreateInstitutionRequest(
    string Name,
    string Code,
    string? Address,
    string? County,
    string? SubCounty,
    double? Latitude = null,
    double? Longitude = null);
public sealed record ProviderDto(Guid Id, string GivenName, string FamilyName, string RegistrationNumber, ProviderRole Role, Guid InstitutionId, string? InstitutionName, bool IsActive);
public sealed record CreateProviderRequest(string GivenName, string FamilyName, string RegistrationNumber, ProviderRole Role, Guid InstitutionId);

public sealed record PatientIdentifierDto(string System, string Value, string? Type);

public sealed record PatientDto(
    Guid Id,
    string MedicalRecordNumber,
    string MasterPatientId,
    string? NationalIdentifier,
    string GivenName,
    string FamilyName,
    DateOnly DateOfBirth,
    string Sex,
    string? Phone,
    Guid ManagingInstitutionId,
    string? ManagingInstitutionName,
    IReadOnlyCollection<PatientIdentifierDto> Identifiers
);

public sealed record CreatePatientRequest(
    string MedicalRecordNumber,
    string GivenName,
    string FamilyName,
    DateOnly DateOfBirth,
    string Sex,
    Guid ManagingInstitutionId,
    string? NationalIdentifier,
    string? Phone,
    string? IdentifierSystem,
    string? IdentifierValue,
    string? MasterPatientId = null
);

public sealed record EncounterDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid InstitutionId,
    string InstitutionName,
    Guid? ProviderId,
    string? ProviderName,
    DateTime StartUtc,
    DateTime? EndUtc,
    string Reason,
    string? Diagnosis,
    string? Procedure,
    EncounterStatus Status
);

public sealed record CreateEncounterRequest(
    Guid PatientId,
    Guid InstitutionId,
    Guid? ProviderId,
    DateTime StartUtc,
    string Reason,
    string? Diagnosis,
    string? Procedure,
    EncounterStatus Status = EncounterStatus.Finished
);

public sealed record LaboratoryResultDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid? EncounterId,
    string Code,
    string Display,
    decimal? NumericValue,
    string? TextValue,
    string? Unit,
    DateTime EffectiveUtc,
    string Status
);

public sealed record CreateLaboratoryResultRequest(
    Guid PatientId,
    Guid? EncounterId,
    string Code,
    string Display,
    decimal? NumericValue,
    string? TextValue,
    string? Unit,
    DateTime EffectiveUtc,
    string Status = "final"
);

public sealed record MedicationDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid? EncounterId,
    string MedicationCode,
    string MedicationName,
    string Dosage,
    string? Route,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status
);

public sealed record CreateMedicationRequest(
    Guid PatientId,
    Guid? EncounterId,
    string MedicationCode,
    string MedicationName,
    string Dosage,
    string? Route,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status = "active"
);

// Patient clinical intelligence response.
public sealed record PatientClinicalAnalysisDto(
    Guid PatientId,
    string PatientName,
    int TotalResults,
    int DistinctTests,
    int RepeatedTests,
    DateTime? LatestResultUtc,
    IReadOnlyCollection<LabTestTrendDto> Trends,
    IReadOnlyCollection<ClinicalObservationDto> Observations,
    string ClinicalDisclaimer
);

// Summary of results for the same test code and unit.
public sealed record LabTestTrendDto(
    string Code,
    string Display,
    string? Unit,
    int ResultCount,
    decimal? EarliestNumericValue,
    decimal? LatestNumericValue,
    decimal? NumericChange,
    DateTime? EarliestDateUtc,
    DateTime? LatestDateUtc,
    string Trend
);

// An observation from recorded data, not a medical diagnosis.
public sealed record ClinicalObservationDto(
    string Level,
    string Title,
    string Description,
    DateTime? ObservedAtUtc
);

public sealed record FhirValidationResult(
    bool IsValid,
    string? ResourceType,
    string? ResourceId,
    IReadOnlyCollection<string> Issues
);

public sealed record FhirExchangeResult(
    bool Succeeded,
    string? ResourceType,
    string? ResourceId,
    string? ResponseBody,
    string? Error,
    Guid LocalRecordId
);

public sealed record FhirResourceDto(
    Guid Id,
    string ResourceType,
    string? ResourceId,
    Guid? PatientId,
    ExchangeStatus Status,
    string? LastError,
    DateTime? SynchronizedAtUtc
);

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role = "Provider",
    Guid? InstitutionId = null
);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    Guid? InstitutionId
);

public sealed record AuditLogDto(
    Guid Id,
    DateTime CreatedAtUtc,
    string? ActorUserId,
    string Action,
    string EntityName,
    string? EntityId,
    string Outcome,
    string? Path,
    string? CorrelationId,
    string? IpAddress
);

public interface IInstitutionService
{
    Task<IReadOnlyCollection<InstitutionDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<InstitutionDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InstitutionDto> CreateAsync(CreateInstitutionRequest request, CancellationToken cancellationToken = default);
    Task<InstitutionDto?> UpdateAsync(Guid id, CreateInstitutionRequest request, CancellationToken cancellationToken = default);
    Task<bool> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public interface IProviderService
{
    Task<IReadOnlyCollection<ProviderDto>> ListAsync(Guid? institutionId = null, CancellationToken cancellationToken = default);
    Task<ProviderDto> CreateAsync(CreateProviderRequest request, CancellationToken cancellationToken = default);
}

public interface IPatientService
{
    Task<IReadOnlyCollection<PatientDto>> SearchAsync(string? query, CancellationToken cancellationToken = default);
    Task<PatientDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PatientDto> CreateAsync(CreatePatientRequest request, CancellationToken cancellationToken = default);
}

public interface IClinicalService
{
    Task<IReadOnlyCollection<EncounterDto>> ListEncountersAsync(Guid? patientId = null, CancellationToken cancellationToken = default);
    Task<EncounterDto> CreateEncounterAsync(CreateEncounterRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LaboratoryResultDto>> ListLaboratoryResultsAsync(Guid? patientId = null, CancellationToken cancellationToken = default);
    Task<LaboratoryResultDto> CreateLaboratoryResultAsync(CreateLaboratoryResultRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MedicationDto>> ListMedicationsAsync(Guid? patientId = null, CancellationToken cancellationToken = default);
    Task<MedicationDto> CreateMedicationAsync(CreateMedicationRequest request, CancellationToken cancellationToken = default);

    // Generate an analysis of the patient's recorded laboratory results.
    Task<PatientClinicalAnalysisDto?> AnalysePatientAsync(Guid patientId, CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task WriteAsync(
        string action,
        string entityName,
        string? entityId,
        string outcome,
        string? path = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<AuditLogDto>> ListAsync(int limit = 100, CancellationToken cancellationToken = default);
}

public interface IFhirService
{
    Task<FhirValidationResult> ValidateAsync(string json, CancellationToken cancellationToken = default);
    Task<FhirExchangeResult> ExchangeAsync(string json, Guid? patientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<FhirResourceDto>> ListLocalAsync(CancellationToken cancellationToken = default);
}

public interface IUserService
{
    Task<(bool Succeeded, string? Error, string UserId)> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error, string UserId)> CreateInstitutionAdminAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error, string UserId)> CreateHealthcareStaffAsync(RegisterHealthcareStaffRequest request, Guid institutionId, CancellationToken cancellationToken = default);
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}


