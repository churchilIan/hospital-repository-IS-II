namespace HospitalRepository.Domain.Entities;

using System;
using System.Collections.Generic;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; protected set; }

    protected void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}

public enum ProviderRole
{
    Doctor,
    Nurse,
    Pharmacist,
    LaboratoryScientist,
    Administrator,
    ClinicalOfficer
}

public enum EncounterStatus
{
    Planned,
    InProgress,
    Finished,
    Cancelled
}

public enum ExchangeStatus
{
    Pending,
    Succeeded,
    Failed
}


public sealed class Institution : Entity
{
    private Institution() { }

    public Institution(string name, string code, string? address = null)
    {
        Update(name, code, address);
    }

    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Address { get; private set; }

    // Geographical information for national surveillance
    public string? County { get; private set; }
    public string? SubCounty { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<Provider> Providers { get; private set; } = new List<Provider>();
    public ICollection<Patient> Patients { get; private set; } = new List<Patient>();

    public void Update(string name, string code, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Institution name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Institution code is required.", nameof(code));

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

        Touch();
    }

    public void UpdateLocation(
        string? county,
        string? subCounty,
        double? latitude,
        double? longitude)
    {
        if ((latitude.HasValue && !longitude.HasValue) ||
            (!latitude.HasValue && longitude.HasValue))
        {
            throw new ArgumentException(
                "Both latitude and longitude must be provided together.");
        }

        if (latitude.HasValue && (latitude < -90 || latitude > 90))
            throw new ArgumentOutOfRangeException(nameof(latitude));

        if (longitude.HasValue && (longitude < -180 || longitude > 180))
            throw new ArgumentOutOfRangeException(nameof(longitude));

        County = string.IsNullOrWhiteSpace(county) ? null : county.Trim();
        SubCounty = string.IsNullOrWhiteSpace(subCounty) ? null : subCounty.Trim();
        Latitude = latitude;
        Longitude = longitude;

        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }
}



public sealed class Provider : Entity
{
    private Provider() { }

    public Provider(
        string givenName,
        string familyName,
        string registrationNumber,
        ProviderRole role,
        Guid institutionId)
    {
        if (string.IsNullOrWhiteSpace(givenName))
            throw new ArgumentException("Given name is required.", nameof(givenName));

        if (string.IsNullOrWhiteSpace(familyName))
            throw new ArgumentException("Family name is required.", nameof(familyName));

        if (string.IsNullOrWhiteSpace(registrationNumber))
            throw new ArgumentException("Registration number is required.", nameof(registrationNumber));

        GivenName = givenName.Trim();
        FamilyName = familyName.Trim();
        RegistrationNumber = registrationNumber.Trim();
        Role = role;
        InstitutionId = institutionId;
    }

    public string GivenName { get; private set; } = string.Empty;

    public string FamilyName { get; private set; } = string.Empty;

    public string RegistrationNumber { get; private set; } = string.Empty;

    public ProviderRole Role { get; private set; }

    public Guid InstitutionId { get; private set; }

    public bool IsActive { get; private set; } = true;

    // Links this healthcare professional to an Identity login account.
    public string? UserId { get; private set; }

    public Institution? Institution { get; private set; }

    public void LinkUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        if (!string.IsNullOrWhiteSpace(UserId))
            throw new InvalidOperationException("This provider already has a linked user account.");

        UserId = userId;
    }
}


public sealed class Patient : Entity
{
    private Patient() { }

    public Patient(string medicalRecordNumber, string givenName, string familyName, DateOnly dateOfBirth, string sex, Guid managingInstitutionId, string? nationalIdentifier = null, string? masterPatientId = null)
    {
        if (string.IsNullOrWhiteSpace(medicalRecordNumber)) throw new ArgumentException("Medical record number is required.", nameof(medicalRecordNumber));
        if (string.IsNullOrWhiteSpace(givenName)) throw new ArgumentException("Given name is required.", nameof(givenName));
        if (string.IsNullOrWhiteSpace(familyName)) throw new ArgumentException("Family name is required.", nameof(familyName));
        MedicalRecordNumber = medicalRecordNumber.Trim();
        GivenName = givenName.Trim();
        FamilyName = familyName.Trim();
        DateOfBirth = dateOfBirth;
        Sex = sex.Trim();
        ManagingInstitutionId = managingInstitutionId;
        NationalIdentifier = string.IsNullOrWhiteSpace(nationalIdentifier) ? null : nationalIdentifier.Trim();
        MasterPatientId = string.IsNullOrWhiteSpace(masterPatientId) ? $"MPI-{Guid.NewGuid():N}" : masterPatientId.Trim().ToUpperInvariant();
    }

    public string MedicalRecordNumber { get; private set; } = string.Empty;
    public string? NationalIdentifier { get; private set; }
    public string MasterPatientId { get; private set; } = string.Empty;
    public string GivenName { get; private set; } = string.Empty;
    public string FamilyName { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }
    public string Sex { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public Guid ManagingInstitutionId { get; private set; }
    public Institution? ManagingInstitution { get; private set; }
    public ICollection<PatientIdentifier> Identifiers { get; private set; } = new List<PatientIdentifier>();

    public void SetPhone(string? phone) { Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(); Touch(); }
    public void AddIdentifier(string system, string value, string? type = null)
    {
        if (string.IsNullOrWhiteSpace(system) || string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Identifier system and value are required.");
        Identifiers.Add(new PatientIdentifier(Id, system.Trim(), value.Trim(), type?.Trim()));
        Touch();
    }
}

public sealed class PatientIdentifier : Entity
{
    private PatientIdentifier() { }
    public PatientIdentifier(Guid patientId, string system, string value, string? type)
    {
        PatientId = patientId;
        System = system;
        Value = value;
        Type = type;
    }
    public Guid PatientId { get; private set; }
    public string System { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string? Type { get; private set; }
    public Patient? Patient { get; private set; }
}

public sealed class ClinicalEncounter : Entity
{
    private ClinicalEncounter() { }
    public ClinicalEncounter(Guid patientId, Guid institutionId, Guid? providerId, DateTime startUtc, string reason, EncounterStatus status = EncounterStatus.Finished)
    {
        PatientId = patientId;
        InstitutionId = institutionId;
        ProviderId = providerId;
        StartUtc = startUtc.ToUniversalTime();
        Reason = reason.Trim();
        Status = status;
    }
    public Guid PatientId { get; private set; }
    public Guid InstitutionId { get; private set; }
    public Guid? ProviderId { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime? EndUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? Diagnosis { get; private set; }
    public string? Procedure { get; private set; }
    public EncounterStatus Status { get; private set; }
    public Patient? Patient { get; private set; }
    public Institution? Institution { get; private set; }
    public Provider? Provider { get; private set; }

    public void Complete(string? diagnosis, string? procedure)
    {
        Status = EncounterStatus.Finished;
        EndUtc = DateTime.UtcNow;
        Diagnosis = diagnosis?.Trim();
        Procedure = procedure?.Trim();
        Touch();
    }
}

public sealed class LaboratoryResult : Entity
{
    private LaboratoryResult() { }
    public LaboratoryResult(Guid patientId, Guid? encounterId, string code, string display, decimal? numericValue, string? textValue, string? unit, DateTime effectiveUtc, string status)
    {
        PatientId = patientId;
        EncounterId = encounterId;
        Code = code.Trim();
        Display = display.Trim();
        NumericValue = numericValue;
        TextValue = string.IsNullOrWhiteSpace(textValue) ? null : textValue.Trim();
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        EffectiveUtc = effectiveUtc.ToUniversalTime();
        Status = status.Trim();
    }
    public Guid PatientId { get; private set; }
    public Guid? EncounterId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Display { get; private set; } = string.Empty;
    public decimal? NumericValue { get; private set; }
    public string? TextValue { get; private set; }
    public string? Unit { get; private set; }
    public DateTime EffectiveUtc { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Patient? Patient { get; private set; }
    public ClinicalEncounter? Encounter { get; private set; }
}

public sealed class MedicationRecord : Entity
{
    private MedicationRecord() { }
    public MedicationRecord(Guid patientId, Guid? encounterId, string medicationCode, string medicationName, string dosage, string? route, DateOnly? startDate, DateOnly? endDate, string status)
    {
        PatientId = patientId;
        EncounterId = encounterId;
        MedicationCode = medicationCode.Trim();
        MedicationName = medicationName.Trim();
        Dosage = dosage.Trim();
        Route = string.IsNullOrWhiteSpace(route) ? null : route.Trim();
        StartDate = startDate;
        EndDate = endDate;
        Status = status.Trim();
    }
    public Guid PatientId { get; private set; }
    public Guid? EncounterId { get; private set; }
    public string MedicationCode { get; private set; } = string.Empty;
    public string MedicationName { get; private set; } = string.Empty;
    public string Dosage { get; private set; } = string.Empty;
    public string? Route { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Patient? Patient { get; private set; }
    public ClinicalEncounter? Encounter { get; private set; }
}

public sealed class FhirResourceRecord : Entity
{
    private FhirResourceRecord() { }
    public FhirResourceRecord(string resourceType, string? resourceId, Guid? patientId, string payloadJson)
    {
        ResourceType = resourceType.Trim();
        ResourceId = string.IsNullOrWhiteSpace(resourceId) ? null : resourceId.Trim();
        PatientId = patientId;
        PayloadJson = payloadJson;
        Status = ExchangeStatus.Pending;
    }
    public string ResourceType { get; private set; } = string.Empty;
    public string? ResourceId { get; private set; }
    public Guid? PatientId { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public ExchangeStatus Status { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? SynchronizedAtUtc { get; private set; }
    public Patient? Patient { get; private set; }

    public void MarkSucceeded(string? resourceId)
    {
        ResourceId = string.IsNullOrWhiteSpace(resourceId) ? ResourceId : resourceId;
        Status = ExchangeStatus.Succeeded;
        LastError = null;
        SynchronizedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void MarkFailed(string error)
    {
        Status = ExchangeStatus.Failed;
        LastError = error.Trim();
        Touch();
    }
}

public sealed class AuditLog : Entity
{
    private AuditLog() { }
    public AuditLog(string? actorUserId, string action, string entityName, string? entityId, string outcome, string? path, string? correlationId, string? ipAddress)
    {
        ActorUserId = actorUserId;
        Action = action.Trim();
        EntityName = entityName.Trim();
        EntityId = entityId;
        Outcome = outcome.Trim();
        Path = path;
        CorrelationId = correlationId;
        IpAddress = ipAddress;
    }
    public string? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string? Path { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? IpAddress { get; private set; }
}
