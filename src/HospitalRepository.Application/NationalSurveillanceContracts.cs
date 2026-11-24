using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HospitalRepository.Application;

public interface INationalSurveillanceService
{
    Task<NationalSurveillanceDto> GetOverviewAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? county = null,
        string? institutionCode = null,
        string? diagnosis = null,
        CancellationToken cancellationToken = default);
}

public sealed record NationalSurveillanceDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalEncounters,
    int DiagnosedEncounters,
    int ReportingHospitals,
    IReadOnlyCollection<DiagnosisSurveillanceDto> Diagnoses,
    IReadOnlyCollection<DiseaseBurdenDto> DiseaseBurden,
    IReadOnlyCollection<InstitutionSurveillanceDto> Institutions,
    IReadOnlyCollection<MonthlySurveillanceDto> MonthlyTrend,
    IReadOnlyCollection<HospitalDiagnosisComparisonDto> HospitalDiagnosisComparison,
    string DataNote,
    IReadOnlyCollection<DiseaseMonthlyTrendDto> DiseaseMonthlyTrends = null!,
    IReadOnlyCollection<CountySurveillanceDto>? Counties = null
);

public sealed record DiagnosisSurveillanceDto(string Diagnosis, int EncounterCount, int ReportingHospitals);
public sealed record DiseaseBurdenDto(string Diagnosis, int EncounterCount, decimal PercentageOfDiagnosedEncounters, int ReportingHospitals);
public sealed record InstitutionSurveillanceDto(string InstitutionName, string InstitutionCode, int EncounterCount, int DiagnosedEncounters, string? County = null, string? SubCounty = null, double? Latitude = null, double? Longitude = null);
public sealed record MonthlySurveillanceDto(string Month, int TotalEncounters, int DiagnosedEncounters, decimal? PercentageChange, bool IsUnusualIncrease);
public sealed record HospitalDiagnosisComparisonDto(string Diagnosis, string InstitutionName, string InstitutionCode, int EncounterCount);
public sealed record DiseaseMonthlyTrendDto(string Diagnosis, IReadOnlyCollection<MonthlyDiseaseTrendPointDto> MonthlyData);
public sealed record MonthlyDiseaseTrendPointDto(string Month, int EncounterCount, decimal? PercentageChange, bool IsUnusualIncrease, IReadOnlyCollection<HospitalDiseaseContributionDto> HospitalBreakdown);
public sealed record HospitalDiseaseContributionDto(string InstitutionName, string InstitutionCode, int EncounterCount);
public sealed record CountySurveillanceDto(string County, int RegisteredHospitals, int ReportingHospitals, int TotalEncounters, int DiagnosedEncounters, IReadOnlyCollection<CountyDiseaseSurveillanceDto> Diseases);
public sealed record CountyDiseaseSurveillanceDto(string Diagnosis, int EncounterCount, decimal PercentageOfCountyDiagnosedEncounters, int ReportingHospitals);
