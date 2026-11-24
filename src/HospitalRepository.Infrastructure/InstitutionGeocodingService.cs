
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using HospitalRepository.Application;

namespace HospitalRepository.Infrastructure;

public sealed class InstitutionGeocodingService(
    HttpClient httpClient,
    IMemoryCache cache) : IInstitutionGeocodingService
{
    private static readonly SemaphoreSlim RequestLock = new(1, 1);
    private static DateTimeOffset LastRequestUtc = DateTimeOffset.MinValue;

    public async Task<InstitutionCoordinates?> FindCoordinatesAsync(
        string? address,
        string? subCounty,
        string? county,
        CancellationToken cancellationToken = default)
    {
        var parts = new[] { address, subCounty, county, "Kenya" }
            .Where(x => !string.IsNullOrWhiteSpace(x));

        var searchText = string.Join(", ", parts);

        if (string.IsNullOrWhiteSpace(searchText) ||
            (string.IsNullOrWhiteSpace(county) &&
             string.IsNullOrWhiteSpace(subCounty)))
        {
            return null;
        }

        var cacheKey = "institution-geocode:" +
            searchText.Trim().ToLowerInvariant();

        if (cache.TryGetValue(
            cacheKey, out InstitutionCoordinates? cached))
        {
            return cached;
        }

        await RequestLock.WaitAsync(cancellationToken);

        try
        {
            // Respect Nominatim's public service limit:
            // no more than one request per second.
            var elapsed = DateTimeOffset.UtcNow - LastRequestUtc;
            var remaining = TimeSpan.FromSeconds(1) - elapsed;

            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, cancellationToken);

            LastRequestUtc = DateTimeOffset.UtcNow;

            var url =
                "https://nominatim.openstreetmap.org/search" +
                "?format=jsonv2" +
                "&limit=1" +
                "&countrycodes=ke" +
                "&addressdetails=1" +
                "&q=" + Uri.EscapeDataString(searchText);

            using var response = await httpClient.GetAsync(
                url, cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document = await JsonDocument.ParseAsync(
                stream, cancellationToken: cancellationToken);

            var results = document.RootElement;

            if (results.ValueKind != JsonValueKind.Array ||
                results.GetArrayLength() == 0)
            {
                return null;
            }

            var result = results[0];

            if (!result.TryGetProperty("lat", out var latElement) ||
                !result.TryGetProperty("lon", out var lonElement))
            {
                return null;
            }

            if (!double.TryParse(
                    latElement.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var latitude) ||
                !double.TryParse(
                    lonElement.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var longitude))
            {
                return null;
            }

            // Ensure the returned coordinates are within Kenya's
            // approximate geographical bounding area.
            if (latitude < -5.0 || latitude > 5.5 ||
                longitude < 33.5 || longitude > 42.5)
            {
                return null;
            }

            var displayName =
                result.TryGetProperty("display_name", out var display)
                    ? display.GetString()
                    : null;

            var coordinates = new InstitutionCoordinates(
                latitude,
                longitude,
                displayName);

            // Cache successful lookups for 30 days.
            cache.Set(
                cacheKey,
                coordinates,
                TimeSpan.FromDays(30));

            return coordinates;
        }
        finally
        {
            RequestLock.Release();
        }
    }
}
