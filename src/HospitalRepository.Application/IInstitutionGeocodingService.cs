
namespace HospitalRepository.Application;

public sealed record InstitutionCoordinates(
    double Latitude,
    double Longitude,
    string? DisplayName);

public interface IInstitutionGeocodingService
{
    Task<InstitutionCoordinates?> FindCoordinatesAsync(
        string? address,
        string? subCounty,
        string? county,
        CancellationToken cancellationToken = default);
}
