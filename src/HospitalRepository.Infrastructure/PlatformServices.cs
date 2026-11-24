
using Microsoft.EntityFrameworkCore;
using HospitalRepository.Application;
using HospitalRepository.Domain.Entities;

namespace HospitalRepository.Infrastructure;

public sealed class InstitutionService(
    PlatformDbContext db,
    IAuditService audit,
    IInstitutionGeocodingService? geocoding = null) : IInstitutionService
{
    public async Task<IReadOnlyCollection<InstitutionDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.Institutions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new InstitutionDto(
                x.Id,
                x.Name,
                x.Code,
                x.Address,
                x.County,
                x.SubCounty,
                x.Latitude,
                x.Longitude,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<InstitutionDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await db.Institutions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return item is null
            ? null
            : new InstitutionDto(
                item.Id,
                item.Name,
                item.Code,
                item.Address,
                item.County,
                item.SubCounty,
                item.Latitude,
                item.Longitude,
                item.IsActive);
    }

    public async Task<InstitutionDto> CreateAsync(
        CreateInstitutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await db.Institutions.AnyAsync(
            x => x.Code == code,
            cancellationToken))
        {
            throw new InvalidOperationException(
                "An institution with this code already exists.");
        }

        ValidateManualCoordinates(request.Latitude, request.Longitude);

        // Manually entered coordinates take priority. If neither is supplied,
        // look up the location automatically from the address and county details.
        var coordinates = request.Latitude.HasValue
            ? new InstitutionCoordinates(request.Latitude.Value, request.Longitude!.Value, "Manually entered")
            : await TryFindCoordinatesAsync(
                request.Address,
                request.SubCounty,
                request.County,
                cancellationToken);

        var institution = new Institution(
            request.Name,
            code,
            request.Address);

        institution.UpdateLocation(
            request.County,
            request.SubCounty,
            coordinates?.Latitude,
            coordinates?.Longitude);

        db.Institutions.Add(institution);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "Create",
            nameof(Institution),
            institution.Id.ToString(),
            "Success",
            cancellationToken: cancellationToken);

        return new InstitutionDto(
            institution.Id,
            institution.Name,
            institution.Code,
            institution.Address,
            institution.County,
            institution.SubCounty,
            institution.Latitude,
            institution.Longitude,
            institution.IsActive);
    }

    public async Task<InstitutionDto?> UpdateAsync(
        Guid id,
        CreateInstitutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var institution = await db.Institutions
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (institution is null)
            return null;

        var code = request.Code.Trim().ToUpperInvariant();

        if (await db.Institutions.AnyAsync(
            x => x.Id != id && x.Code == code,
            cancellationToken))
        {
            throw new InvalidOperationException(
                "Another institution already uses this code.");
        }

        ValidateManualCoordinates(request.Latitude, request.Longitude);

        var locationChanged =
            !string.Equals(institution.Address, request.Address, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(institution.County, request.County, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(institution.SubCounty, request.SubCounty, StringComparison.OrdinalIgnoreCase);

        var manualCoordinatesSupplied = request.Latitude.HasValue;

        institution.Update(
            request.Name,
            code,
            request.Address);

        if (manualCoordinatesSupplied)
        {
            // Explicit administrator coordinates override automatic lookup.
            institution.UpdateLocation(
                request.County,
                request.SubCounty,
                request.Latitude,
                request.Longitude);
        }
        else if (locationChanged)
        {
            // Re-geocode when the address or administrative area changes.
            // If no match is found, clear the old coordinates rather than
            // keeping a marker at a potentially incorrect previous location.
            var coordinates = await TryFindCoordinatesAsync(
                request.Address,
                request.SubCounty,
                request.County,
                cancellationToken);

            institution.UpdateLocation(
                request.County,
                request.SubCounty,
                coordinates?.Latitude,
                coordinates?.Longitude);
        }

        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "Update",
            nameof(Institution),
            institution.Id.ToString(),
            "Success",
            cancellationToken: cancellationToken);

        return new InstitutionDto(
            institution.Id,
            institution.Name,
            institution.Code,
            institution.Address,
            institution.County,
            institution.SubCounty,
            institution.Latitude,
            institution.Longitude,
            institution.IsActive);
    }

    public async Task<bool> SetActiveStatusAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var institution = await db.Institutions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (institution == null)
            return false;

        if (isActive)
            institution.Activate();
        else
            institution.Deactivate();

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ValidateManualCoordinates(double? latitude, double? longitude)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new ArgumentException(
                "Both latitude and longitude must be provided together, or both left empty.");
        }

        if (latitude.HasValue &&
            (latitude.Value < -90 || latitude.Value > 90 ||
             longitude!.Value < -180 || longitude.Value > 180))
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                "Latitude must be between -90 and 90, and longitude must be between -180 and 180.");
        }
    }

    private async Task<InstitutionCoordinates?> TryFindCoordinatesAsync(
        string? address,
        string? subCounty,
        string? county,
        CancellationToken cancellationToken)
    {
        if (geocoding is null)
            return null;

        try
        {
            return await geocoding.FindCoordinatesAsync(
                address,
                subCounty,
                county,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            // External geocoding service unavailable: allow hospital registration
            // or update to continue without coordinates.
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Geocoding request timed out: allow the operation to continue.
            return null;
        }
    }
}

public sealed class ProviderService(PlatformDbContext db, IAuditService audit) : IProviderService
{
    public async Task<IReadOnlyCollection<ProviderDto>> ListAsync(
        Guid? institutionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Providers
            .AsNoTracking()
            .Include(x => x.Institution)
            .AsQueryable();

        if (institutionId.HasValue)
            query = query.Where(x => x.InstitutionId == institutionId.Value);

        return await query
            .OrderBy(x => x.FamilyName)
            .ThenBy(x => x.GivenName)
            .Select(x => new ProviderDto(
                x.Id, x.GivenName, x.FamilyName,
                x.RegistrationNumber, x.Role, x.InstitutionId,
                x.Institution!.Name, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProviderDto> CreateAsync(
        CreateProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Institutions.AnyAsync(
            x => x.Id == request.InstitutionId && x.IsActive,
            cancellationToken))
        {
            throw new InvalidOperationException(
                "The selected institution does not exist or is inactive.");
        }

        if (await db.Providers.AnyAsync(
            x => x.RegistrationNumber == request.RegistrationNumber.Trim(),
            cancellationToken))
        {
            throw new InvalidOperationException(
                "A provider with this registration number already exists.");
        }

        var provider = new Provider(
            request.GivenName,
            request.FamilyName,
            request.RegistrationNumber,
            request.Role,
            request.InstitutionId);

        db.Providers.Add(provider);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "Create", nameof(Provider), provider.Id.ToString(),
            "Success", cancellationToken: cancellationToken);

        var institution = await db.Institutions
            .AsNoTracking()
            .SingleAsync(x => x.Id == provider.InstitutionId, cancellationToken);

        return new ProviderDto(
            provider.Id, provider.GivenName, provider.FamilyName,
            provider.RegistrationNumber, provider.Role,
            provider.InstitutionId, institution.Name, provider.IsActive);
    }
}

public sealed class PatientService(PlatformDbContext db, IAuditService audit) : IPatientService
{
    public async Task<IReadOnlyCollection<PatientDto>> SearchAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var patientQuery = db.Patients
            .AsNoTracking()
            .Include(x => x.ManagingInstitution)
            .Include(x => x.Identifiers)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();

            patientQuery = patientQuery.Where(x =>
                x.MedicalRecordNumber.Contains(term) ||
                x.MasterPatientId.Contains(term) ||
                (x.NationalIdentifier != null && x.NationalIdentifier.Contains(term)) ||
                x.GivenName.Contains(term) ||
                x.FamilyName.Contains(term) ||
                x.Identifiers.Any(i => i.Value.Contains(term)));
        }

        return await patientQuery
            .OrderBy(x => x.FamilyName)
            .ThenBy(x => x.GivenName)
            .Take(100)
            .Select(MapPatient)
            .ToListAsync(cancellationToken);
    }

    public async Task<PatientDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var patient = await db.Patients
            .AsNoTracking()
            .Include(x => x.ManagingInstitution)
            .Include(x => x.Identifiers)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return patient is null ? null : ToDto(patient);
    }

    public async Task<PatientDto> CreateAsync(
        CreatePatientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Institutions.AnyAsync(
            x => x.Id == request.ManagingInstitutionId && x.IsActive,
            cancellationToken))
        {
            throw new InvalidOperationException(
                "The managing institution does not exist or is inactive.");
        }

        if (await db.Patients.AnyAsync(
            x => x.MedicalRecordNumber == request.MedicalRecordNumber.Trim(),
            cancellationToken))
        {
            throw new InvalidOperationException(
                "A patient with this medical record number already exists.");
        }

        if (!string.IsNullOrWhiteSpace(request.IdentifierSystem) &&
            !string.IsNullOrWhiteSpace(request.IdentifierValue) &&
            await db.PatientIdentifiers.AnyAsync(
                x => x.System == request.IdentifierSystem.Trim() &&
                     x.Value == request.IdentifierValue.Trim(),
                cancellationToken))
        {
            throw new InvalidOperationException(
                "A patient with this identifier already exists.");
        }

        var patient = new Patient(
            request.MedicalRecordNumber,
            request.GivenName,
            request.FamilyName,
            request.DateOfBirth,
            request.Sex,
            request.ManagingInstitutionId,
            request.NationalIdentifier,
            request.MasterPatientId);

        patient.SetPhone(request.Phone);

        if (!string.IsNullOrWhiteSpace(request.IdentifierSystem) &&
            !string.IsNullOrWhiteSpace(request.IdentifierValue))
        {
            patient.AddIdentifier(request.IdentifierSystem, request.IdentifierValue);
        }

        db.Patients.Add(patient);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "Create", nameof(Patient), patient.Id.ToString(),
            "Success", cancellationToken: cancellationToken);

        await db.Entry(patient)
            .Reference(x => x.ManagingInstitution)
            .LoadAsync(cancellationToken);

        return ToDto(patient);
    }

    private static PatientDto ToDto(Patient p) =>
        new(
            p.Id,
            p.MedicalRecordNumber,
            p.MasterPatientId,
            p.NationalIdentifier,
            p.GivenName,
            p.FamilyName,
            p.DateOfBirth,
            p.Sex,
            p.Phone,
            p.ManagingInstitutionId,
            p.ManagingInstitution?.Name,
            p.Identifiers.Select(i =>
                new PatientIdentifierDto(i.System, i.Value, i.Type)).ToArray());

    private static readonly System.Linq.Expressions.Expression<Func<Patient, PatientDto>> MapPatient =
        p => new PatientDto(
            p.Id,
            p.MedicalRecordNumber,
            p.MasterPatientId,
            p.NationalIdentifier,
            p.GivenName,
            p.FamilyName,
            p.DateOfBirth,
            p.Sex,
            p.Phone,
            p.ManagingInstitutionId,
            p.ManagingInstitution!.Name,
            p.Identifiers.Select(i =>
                new PatientIdentifierDto(i.System, i.Value, i.Type)).ToArray());
}

public sealed class ClinicalService(PlatformDbContext db, IAuditService audit) : IClinicalService
{
    public async Task<IReadOnlyCollection<EncounterDto>> ListEncountersAsync(
        Guid? patientId = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.ClinicalEncounters
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Institution)
            .Include(x => x.Provider)
            .AsQueryable();

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        return await query
            .OrderByDescending(x => x.StartUtc)
            .Take(200)
            .Select(x => new EncounterDto(
                x.Id,
                x.PatientId,
                x.Patient!.GivenName + " " + x.Patient.FamilyName,
                x.InstitutionId,
                x.Institution!.Name,
                x.ProviderId,
                x.Provider == null
                    ? null
                    : x.Provider.GivenName + " " + x.Provider.FamilyName,
                x.StartUtc,
                x.EndUtc,
                x.Reason,
                x.Diagnosis,
                x.Procedure,
                x.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<EncounterDto> CreateEncounterAsync(
        CreateEncounterRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePatientAsync(request.PatientId, cancellationToken);
        await EnsureInstitutionAsync(request.InstitutionId, cancellationToken);

        if (request.ProviderId.HasValue &&
            !await db.Providers.AnyAsync(
                x => x.Id == request.ProviderId &&
                     x.InstitutionId == request.InstitutionId &&
                     x.IsActive,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The provider does not belong to the selected institution or is inactive.");
        }

        var encounter = new ClinicalEncounter(
            request.PatientId,
            request.InstitutionId,
            request.ProviderId,
            request.StartUtc,
            request.Reason,
            request.Status);

        if (!string.IsNullOrWhiteSpace(request.Diagnosis) ||
            !string.IsNullOrWhiteSpace(request.Procedure))
        {
            encounter.Complete(request.Diagnosis, request.Procedure);
        }

        db.ClinicalEncounters.Add(encounter);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "Create", nameof(ClinicalEncounter), encounter.Id.ToString(),
            "Success", cancellationToken: cancellationToken);

        return (await ListEncountersAsync(null, cancellationToken))
            .Single(x => x.Id == encounter.Id);
    }

    public async Task<IReadOnlyCollection<LaboratoryResultDto>> ListLaboratoryResultsAsync(
        Guid? patientId = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.LaboratoryResults
            .AsNoTracking()
            .Include(x => x.Patient)
            .AsQueryable();

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        return await query
            .OrderByDescending(x => x.EffectiveUtc)
            .Take(200)
            .Select(x => new LaboratoryResultDto(
                x.Id,
                x.PatientId,
                x.Patient!.GivenName + " " + x.Patient.FamilyName,
                x.EncounterId,
                x.Code,
                x.Display,
                x.NumericValue,
                x.TextValue,
                x.Unit,
                x.EffectiveUtc,
                x.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<LaboratoryResultDto> CreateLaboratoryResultAsync(
        CreateLaboratoryResultRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePatientAsync(request.PatientId, cancellationToken);

        if (request.EncounterId.HasValue &&
            !await db.ClinicalEncounters.AnyAsync(
                x => x.Id == request.EncounterId &&
                     x.PatientId == request.PatientId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The encounter does not exist for this patient.");
        }

        var result = new LaboratoryResult(
            request.PatientId,
            request.EncounterId,
            request.Code,
            request.Display,
            request.NumericValue,
            request.TextValue,
            request.Unit,
            request.EffectiveUtc,
            request.Status);

        db.LaboratoryResults.Add(result);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "Create", nameof(LaboratoryResult), result.Id.ToString(),
            "Success", cancellationToken: cancellationToken);

        return (await ListLaboratoryResultsAsync(null, cancellationToken))
            .Single(x => x.Id == result.Id);
    }

    public async Task<IReadOnlyCollection<MedicationDto>> ListMedicationsAsync(
        Guid? patientId = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.MedicationRecords
            .AsNoTracking()
            .Include(x => x.Patient)
            .AsQueryable();

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .Select(x => new MedicationDto(
                x.Id,
                x.PatientId,
                x.Patient!.GivenName + " " + x.Patient.FamilyName,
                x.EncounterId,
                x.MedicationCode,
                x.MedicationName,
                x.Dosage,
                x.Route,
                x.StartDate,
                x.EndDate,
                x.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<MedicationDto> CreateMedicationAsync(
        CreateMedicationRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePatientAsync(request.PatientId, cancellationToken);

        if (request.EncounterId.HasValue &&
            !await db.ClinicalEncounters.AnyAsync(
                x => x.Id == request.EncounterId &&
                     x.PatientId == request.PatientId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The encounter does not exist for this patient.");
        }

        var medication = new MedicationRecord(
            request.PatientId,
            request.EncounterId,
            request.MedicationCode,
            request.MedicationName,
            request.Dosage,
            request.Route,
            request.StartDate,
            request.EndDate,
            request.Status);

        db.MedicationRecords.Add(medication);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "Create", nameof(MedicationRecord), medication.Id.ToString(),
            "Success", cancellationToken: cancellationToken);

        return (await ListMedicationsAsync(null, cancellationToken))
            .Single(x => x.Id == medication.Id);
    }

    // Analyse a patient's existing laboratory results.
    public async Task<PatientClinicalAnalysisDto?> AnalysePatientAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patient = await db.Patients
            .AsNoTracking()
            .Where(x => x.Id == patientId)
            .Select(x => new
            {
                x.Id,
                x.GivenName,
                x.FamilyName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (patient is null)
            return null;

        var results = await db.LaboratoryResults
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderBy(x => x.EffectiveUtc)
            .ToListAsync(cancellationToken);

        var patientName = patient.GivenName + " " + patient.FamilyName;

        var distinctTests = results
            .Select(x => NormalizeTestCode(x.Code, x.Display))
            .Distinct()
            .Count();

        var testGroups = results.GroupBy(x => new
        {
            Code = NormalizeTestCode(x.Code, x.Display),
            Unit = NormalizeUnit(x.Unit)
        }).ToList();

        var repeatedTests = testGroups
            .Count(group => group.Count() > 1);

        var trends = new List<LabTestTrendDto>();
        var observations = new List<ClinicalObservationDto>();

        foreach (var group in testGroups)
        {
            var orderedResults = group
                .OrderBy(x => x.EffectiveUtc)
                .ToList();

            var numericResults = orderedResults
                .Where(x => x.NumericValue.HasValue)
                .ToList();

            decimal? earliestValue = null;
            decimal? latestValue = null;
            decimal? numericChange = null;
            DateTime? earliestDate = null;
            DateTime? latestDate = null;

            string trendDescription;

            if (numericResults.Count >= 2)
            {
                var earliest = numericResults.First();
                var latest = numericResults.Last();

                earliestValue = earliest.NumericValue;
                latestValue = latest.NumericValue;
                earliestDate = earliest.EffectiveUtc;
                latestDate = latest.EffectiveUtc;
                numericChange = latestValue - earliestValue;

                if (numericChange > 0)
                    trendDescription = "Increasing";
                else if (numericChange < 0)
                    trendDescription = "Decreasing";
                else
                    trendDescription = "No numerical change";
            }
            else if (numericResults.Count == 1)
            {
                earliestValue = numericResults[0].NumericValue;
                latestValue = numericResults[0].NumericValue;
                earliestDate = numericResults[0].EffectiveUtc;
                latestDate = numericResults[0].EffectiveUtc;
                trendDescription = "Only one numerical result recorded";
            }
            else
            {
                trendDescription = "No numerical results available";
            }

            var firstResult = orderedResults[0];

            trends.Add(new LabTestTrendDto(
                firstResult.Code,
                firstResult.Display,
                firstResult.Unit,
                orderedResults.Count,
                earliestValue,
                latestValue,
                numericChange,
                earliestDate,
                latestDate,
                trendDescription));

            if (orderedResults.Count > 1)
            {
                var description =
                    $"{firstResult.Display} has been recorded {orderedResults.Count} times.";

                if (numericResults.Count >= 2)
                {
                    description +=
                        $" The recorded numerical values changed from {earliestValue} " +
                        $"to {latestValue} {firstResult.Unit ?? ""}. " +
                        $"The calculated change is {numericChange}.";
                }
                else
                {
                    description +=
                        " There are not enough numerical results to calculate a numerical trend.";
                }

                description +=
                    " This describes the recorded data and does not establish clinical significance.";

                observations.Add(new ClinicalObservationDto(
                    "Information",
                    "Repeated laboratory test",
                    description,
                    orderedResults.Last().EffectiveUtc));
            }
        }

        if (results.Count == 0)
        {
            observations.Add(new ClinicalObservationDto(
                "Information",
                "No laboratory results recorded",
                "There are currently no laboratory results recorded for this patient. " +
                "Clinical analysis will become available when results are recorded.",
                null));
        }

        return new PatientClinicalAnalysisDto(
            patient.Id,
            patientName,
            results.Count,
            distinctTests,
            repeatedTests,
            results.Count == 0
                ? null
                : results.Max(x => x.EffectiveUtc),
            trends.OrderByDescending(x => x.LatestDateUtc).ToArray(),
            observations.OrderByDescending(x => x.ObservedAtUtc).ToArray(),
            "This analysis summarises recorded laboratory data. " +
            "An increase or decrease does not by itself indicate improvement, deterioration, " +
            "or a diagnosis. Results must be interpreted by a qualified healthcare professional " +
            "using appropriate test-specific reference ranges and the patient's clinical context.");
    }

    private static string NormalizeTestCode(string? code, string? display)
    {
        var value = string.IsNullOrWhiteSpace(code) ? display : code;
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string NormalizeUnit(string? unit)
    {
        return (unit ?? string.Empty).Trim().ToUpperInvariant();
    }

    private async Task EnsurePatientAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!await db.Patients.AnyAsync(
            x => x.Id == id,
            cancellationToken))
        {
            throw new InvalidOperationException("The patient does not exist.");
        }
    }

    private async Task EnsureInstitutionAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!await db.Institutions.AnyAsync(
            x => x.Id == id && x.IsActive,
            cancellationToken))
        {
            throw new InvalidOperationException(
                "The institution does not exist or is inactive.");
        }
    }
}

public sealed class AuditService(PlatformDbContext db) : IAuditService
{
    public async Task WriteAsync(
        string action,
        string entityName,
        string? entityId,
        string outcome,
        string? path = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(new AuditLog(
            actorUserId,
            action,
            entityName,
            entityId,
            outcome,
            path,
            correlationId,
            ipAddress));

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AuditLogDto>> ListAsync(
        int limit = 100,
        CancellationToken cancellationToken = default) =>
        await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(x => new AuditLogDto(
                x.Id,
                x.CreatedAtUtc,
                x.ActorUserId,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Outcome,
                x.Path,
                x.CorrelationId,
                x.IpAddress))
            .ToListAsync(cancellationToken);
}
