
using Microsoft.EntityFrameworkCore;
using HospitalRepository.Application;

namespace HospitalRepository.Infrastructure;

public sealed class NationalSurveillanceService(PlatformDbContext db)
    : INationalSurveillanceService
{
    public async Task<NationalSurveillanceDto> GetOverviewAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? county = null,
        string? institutionCode = null,
        string? diagnosis = null,
        CancellationToken cancellationToken = default)
    {
        var endUtc = toUtc ?? DateTime.UtcNow;
        var startUtc = fromUtc ?? endUtc.AddMonths(-12);

        if (startUtc >= endUtc)
        {
            throw new ArgumentException(
                "The start date must be earlier than the end date.");
        }

        // Retrieve institution metadata first so county and hospital filters
        // can be applied consistently to every surveillance calculation.
        var institutions = await db.Institutions
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Code,
                x.County,
                x.SubCounty,
                x.Latitude,
                x.Longitude
            })
            .ToListAsync(cancellationToken);

        var locationFilteredInstitutions = institutions
            .Where(x =>
                (string.IsNullOrWhiteSpace(county) ||
                 string.Equals(x.County, county.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(institutionCode) ||
                 string.Equals(x.Code, institutionCode.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var selectedInstitutionIds = locationFilteredInstitutions
            .Select(x => x.Id)
            .ToHashSet();

        // Retrieve clinical encounters within the reporting period.
        var encounterRecords = await db.ClinicalEncounters
            .AsNoTracking()
            .Where(x => x.StartUtc >= startUtc && x.StartUtc < endUtc)
            .Select(x => new
            {
                x.InstitutionId,
                x.StartUtc,
                x.Diagnosis
            })
            .ToListAsync(cancellationToken);

        // Apply location and diagnosis filters before calculating any metrics.
        encounterRecords = encounterRecords
            .Where(x => selectedInstitutionIds.Contains(x.InstitutionId))
            .Where(x => string.IsNullOrWhiteSpace(diagnosis) ||
                        string.Equals(x.Diagnosis?.Trim(), diagnosis.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var totalEncounters = encounterRecords.Count;

        // Keep only encounters with a recorded diagnosis.
        var diagnosedRecords = encounterRecords
            .Where(x => !string.IsNullOrWhiteSpace(x.Diagnosis))
            .Select(x => new
            {
                x.InstitutionId,
                x.StartUtc,
                Diagnosis = x.Diagnosis!.Trim()
            })
            .ToList();

        var diagnosedEncounters = diagnosedRecords.Count;

        // Count hospitals that reported encounters during the period.
        var reportingHospitals = encounterRecords
            .Select(x => x.InstitutionId)
            .Distinct()
            .Count();

        // Overall diagnosis distribution.
        var diagnosisData = diagnosedRecords
            .GroupBy(x => x.Diagnosis, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DiagnosisSurveillanceDto(
                group.First().Diagnosis,
                group.Count(),
                group.Select(x => x.InstitutionId).Distinct().Count()))
            .OrderByDescending(x => x.EncounterCount)
            .ToList();

        // National disease burden analysis.
        var diseaseBurden = diagnosisData
            .Select(diagnosis => new DiseaseBurdenDto(
                diagnosis.Diagnosis,
                diagnosis.EncounterCount,
                diagnosedEncounters == 0
                    ? 0
                    : Math.Round(
                        (decimal)diagnosis.EncounterCount /
                        diagnosedEncounters * 100,
                        2),
                diagnosis.ReportingHospitals))
            .OrderByDescending(x => x.PercentageOfDiagnosedEncounters)
            .ToList();

        // Use only hospitals matching the selected location filters for
        // the map, hospital statistics and county summaries.
        var institutionsForDisplay = locationFilteredInstitutions;

        // Create a lookup of all institutions by ID.
        var institutionsById = institutions.ToDictionary(x => x.Id);

        // Hospital reporting statistics.
        // Hospitals without encounters are included with zero counts.
        var institutionData = institutionsForDisplay
            .Select(institution =>
            {
                var hospitalRecords = encounterRecords
                    .Where(x => x.InstitutionId == institution.Id)
                    .ToList();

                return new InstitutionSurveillanceDto(
                    institution.Name,
                    institution.Code,
                    hospitalRecords.Count,
                    hospitalRecords.Count(x =>
                        !string.IsNullOrWhiteSpace(x.Diagnosis)),
                    institution.County,
                    institution.SubCounty,
                    institution.Latitude,
                    institution.Longitude);
            })
            .OrderByDescending(x => x.EncounterCount)
            .ThenBy(x => x.InstitutionName)
            .ToList();

        // Compare recorded diagnoses across participating hospitals.
        var hospitalDiagnosisComparison = diagnosedRecords
            .Join(
                institutions,
                record => record.InstitutionId,
                institution => institution.Id,
                (record, institution) => new
                {
                    record.Diagnosis,
                    institution.Name,
                    institution.Code
                })
            .GroupBy(x => new
            {
                Diagnosis = x.Diagnosis.ToUpperInvariant(),
                x.Name,
                x.Code
            })
            .Select(group => new HospitalDiagnosisComparisonDto(
                group.First().Diagnosis,
                group.Key.Name,
                group.Key.Code,
                group.Count()))
            .OrderBy(x => x.Diagnosis)
            .ThenByDescending(x => x.EncounterCount)
            .ToList();

        // ---------------------------------------------------------
        // COUNTY-LEVEL SURVEILLANCE ANALYSIS
        // ---------------------------------------------------------

        // Group registered hospitals by their assigned county.
        // Hospitals without a county assignment are not included
        // in county analysis until their location is completed.
        var countyGroups = institutionsForDisplay
            .Where(x => !string.IsNullOrWhiteSpace(x.County))
            .GroupBy(
                x => x.County!.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key)
            .ToList();

        var countyData = new List<CountySurveillanceDto>();

        foreach (var countyGroup in countyGroups)
        {
            var countyInstitutions = countyGroup.ToList();

            var countyInstitutionIds = countyInstitutions
                .Select(x => x.Id)
                .ToHashSet();

            // All encounters submitted by hospitals assigned
            // to this county during the selected period.
            var countyEncounters = encounterRecords
                .Where(x => countyInstitutionIds.Contains(x.InstitutionId))
                .ToList();

            var countyDiagnosedRecords = diagnosedRecords
                .Where(x => countyInstitutionIds.Contains(x.InstitutionId))
                .ToList();

            var countyReportingHospitals = countyEncounters
                .Select(x => x.InstitutionId)
                .Distinct()
                .Count();

            // Group the county's diagnosed encounters by disease.
            var countyDiseases = countyDiagnosedRecords
                .GroupBy(
                    x => x.Diagnosis,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var diseaseHospitalCount = group
                        .Select(x => x.InstitutionId)
                        .Distinct()
                        .Count();

                    var diseasePercentage =
                        countyDiagnosedRecords.Count == 0
                            ? 0
                            : Math.Round(
                                (decimal)group.Count() /
                                countyDiagnosedRecords.Count * 100,
                                2);

                    return new CountyDiseaseSurveillanceDto(
                        group.First().Diagnosis,
                        group.Count(),
                        diseasePercentage,
                        diseaseHospitalCount);
                })
                .OrderByDescending(x => x.EncounterCount)
                .ToList();

            countyData.Add(new CountySurveillanceDto(
                countyGroup.Key,
                countyInstitutions.Count,
                countyReportingHospitals,
                countyEncounters.Count,
                countyDiagnosedRecords.Count,
                countyDiseases));
        }

        // ---------------------------------------------------------
        // EXISTING MONTHLY SURVEILLANCE ANALYSIS
        // ---------------------------------------------------------

        var firstMonth = new DateTime(
            startUtc.Year,
            startUtc.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var lastMonth = new DateTime(
            endUtc.Year,
            endUtc.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var months = new List<DateTime>();

        for (var month = firstMonth;
             month <= lastMonth;
             month = month.AddMonths(1))
        {
            months.Add(month);
        }

        // Existing overall monthly encounter trend.
        var monthlyTrend = new List<MonthlySurveillanceDto>();

        int? previousDiagnosedCount = null;

        foreach (var month in months)
        {
            var monthRecords = encounterRecords
                .Where(x =>
                    x.StartUtc.Year == month.Year &&
                    x.StartUtc.Month == month.Month)
                .ToList();

            var monthlyDiagnosed = monthRecords.Count(
                x => !string.IsNullOrWhiteSpace(x.Diagnosis));

            decimal? percentageChange = null;

            if (previousDiagnosedCount.HasValue &&
                previousDiagnosedCount.Value > 0)
            {
                percentageChange = Math.Round(
                    ((decimal)(monthlyDiagnosed -
                               previousDiagnosedCount.Value) /
                     previousDiagnosedCount.Value) * 100,
                    2);
            }

            var unusualIncrease =
                previousDiagnosedCount.HasValue &&
                previousDiagnosedCount.Value >= 5 &&
                monthlyDiagnosed >= 10 &&
                percentageChange >= 50;

            monthlyTrend.Add(new MonthlySurveillanceDto(
                month.ToString("yyyy-MM"),
                monthRecords.Count,
                monthlyDiagnosed,
                percentageChange,
                unusualIncrease));

            previousDiagnosedCount = monthlyDiagnosed;
        }

        // ---------------------------------------------------------
        // EXISTING DISEASE-SPECIFIC MONTHLY TRENDS AND ALERTS
        // ---------------------------------------------------------

        var diseaseMonthlyTrends = new List<DiseaseMonthlyTrendDto>();

        var groupedDiseases = diagnosedRecords
            .GroupBy(
                x => x.Diagnosis,
                StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count());

        foreach (var diseaseGroup in groupedDiseases)
        {
            var diseaseName = diseaseGroup.First().Diagnosis;
            var diseaseRecords = diseaseGroup.ToList();

            var monthlyDiseaseData =
                new List<MonthlyDiseaseTrendPointDto>();

            int? previousDiseaseCount = null;

            foreach (var month in months)
            {
                var monthDiseaseRecords = diseaseRecords
                    .Where(x =>
                        x.StartUtc.Year == month.Year &&
                        x.StartUtc.Month == month.Month)
                    .ToList();

                var monthlyDiseaseCount = monthDiseaseRecords.Count;

                decimal? diseasePercentageChange = null;

                if (previousDiseaseCount.HasValue &&
                    previousDiseaseCount.Value > 0)
                {
                    diseasePercentageChange = Math.Round(
                        ((decimal)(monthlyDiseaseCount -
                                   previousDiseaseCount.Value) /
                         previousDiseaseCount.Value) * 100,
                        2);
                }

                // Existing unusual-increase alert thresholds.
                var diseaseUnusualIncrease =
                    previousDiseaseCount.HasValue &&
                    previousDiseaseCount.Value >= 5 &&
                    monthlyDiseaseCount >= 10 &&
                    diseasePercentageChange >= 50;

                // Identify hospitals contributing to this diagnosis.
                var hospitalBreakdown = monthDiseaseRecords
                    .GroupBy(x => x.InstitutionId)
                    .Select(group =>
                    {
                        if (!institutionsById.TryGetValue(
                                group.Key,
                                out var institution))
                        {
                            return null;
                        }

                        return new HospitalDiseaseContributionDto(
                            institution.Name,
                            institution.Code,
                            group.Count());
                    })
                    .Where(x => x != null)
                    .Select(x => x!)
                    .OrderByDescending(x => x.EncounterCount)
                    .ToList();

                monthlyDiseaseData.Add(
                    new MonthlyDiseaseTrendPointDto(
                        month.ToString("yyyy-MM"),
                        monthlyDiseaseCount,
                        diseasePercentageChange,
                        diseaseUnusualIncrease,
                        hospitalBreakdown));

                previousDiseaseCount = monthlyDiseaseCount;
            }

            diseaseMonthlyTrends.Add(
                new DiseaseMonthlyTrendDto(
                    diseaseName,
                    monthlyDiseaseData));
        }

        // ---------------------------------------------------------
        // RETURN NATIONAL AND COUNTY-LEVEL RESULTS
        // ---------------------------------------------------------

        return new NationalSurveillanceDto(
            startUtc,
            endUtc,
            totalEncounters,
            diagnosedEncounters,
            reportingHospitals,
            diagnosisData,
            diseaseBurden,
            institutionData,
            monthlyTrend,
            hospitalDiagnosisComparison,
            "Statistics represent recorded clinical encounters, not confirmed disease incidence or unique patients. Diagnosis entries are currently free text. Disease burden percentages represent the share of diagnosed encounters. County statistics are grouped using the registered county of each hospital, not the patient's residence. Hospitals without a county assignment are excluded from county analysis. Unusual increases are statistical investigation indicators, not confirmed outbreaks.",
            diseaseMonthlyTrends,
            countyData
        );
    }
}
