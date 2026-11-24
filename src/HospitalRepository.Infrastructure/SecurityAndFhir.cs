
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;

using FhirResource = Hl7.Fhir.Model.Resource;
using Hl7.Fhir.Serialization;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using HospitalRepository.Application;
using HospitalRepository.Domain.Entities;

namespace HospitalRepository.Infrastructure;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "HospitalRepository";
    public string Audience { get; set; } = "HospitalRepository.Client";
    public string Key { get; set; } = "development-only-change-me-to-a-long-secret-key";
    public int ExpirationMinutes { get; set; } = 60;
}

public sealed class FirelyServerOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? BearerToken { get; set; }
}

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    public LoginResponse CreateToken(
        ApplicationUser user,
        IReadOnlyCollection<string> roles)
    {
        var settings = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(settings.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        if (user.InstitutionId.HasValue)
        {
            claims.Add(new Claim(
                "InstitutionId",
                user.InstitutionId.Value.ToString()));
        }

        claims.AddRange(
            roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(settings.Key));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            now,
            expires,
            credentials);

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires,
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles,
            user.InstitutionId);
    }
}

public sealed class UserService(
    UserManager<ApplicationUser> userManager,
    JwtTokenService tokenService,
    PlatformDbContext db) : IUserService
{
    public async Task<(bool Succeeded, string? Error, string UserId)> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Only allow normal user roles through public registration.
        var allowedRoles = new[] { "Provider", "Patient" };

        var role = string.IsNullOrWhiteSpace(request.Role)
            ? "Provider"
            : request.Role.Trim();

        if (!allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            return (
                false,
                "This role cannot be assigned through public registration.",
                string.Empty);
        }

        // Every provider must belong to a hospital.
        if (role.Equals("Provider", StringComparison.OrdinalIgnoreCase)
            && !request.InstitutionId.HasValue)
        {
            return (
                false,
                "Please select a hospital for this provider.",
                string.Empty);
        }

        // Verify that the selected hospital exists and is active.
        if (request.InstitutionId.HasValue)
        {
            var institutionExists = await db.Institutions.AnyAsync(
                x => x.Id == request.InstitutionId.Value && x.IsActive,
                cancellationToken);

            if (!institutionExists)
            {
                return (
                    false,
                    "The selected hospital does not exist or is inactive.",
                    string.Empty);
            }
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            InstitutionId = request.InstitutionId,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            return (
                false,
                string.Join("; ", result.Errors.Select(x => x.Description)),
                string.Empty);
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);

        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return (
                false,
                string.Join("; ", roleResult.Errors.Select(x => x.Description)),
                string.Empty);
        }

        return (true, null, user.Id);
    }

    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null ||
            !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return null;
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();

        return tokenService.CreateToken(user, roles);
    }

    public async Task<(bool Succeeded, string? Error, string UserId)> CreateInstitutionAdminAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Validate the selected hospital.
        if (!request.InstitutionId.HasValue)
        {
            return (
                false,
                "Please select a hospital.",
                string.Empty);
        }

        var institution = await db.Institutions.FirstOrDefaultAsync(
            x => x.Id == request.InstitutionId.Value && x.IsActive,
            cancellationToken);

        if (institution == null)
        {
            return (
                false,
                "The selected hospital does not exist or is inactive.",
                string.Empty);
        }

        // Create the hospital administrator.
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            InstitutionId = institution.Id,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            return (
                false,
                string.Join("; ", result.Errors.Select(x => x.Description)),
                string.Empty);
        }

        // Assign the fixed InstitutionAdmin role.
        var roleResult = await userManager.AddToRoleAsync(
            user,
            "InstitutionAdmin");

        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return (
                false,
                string.Join("; ", roleResult.Errors.Select(x => x.Description)),
                string.Empty);
        }

        return (true, null, user.Id);
    }

    // Create a healthcare professional's login account
    // and corresponding Provider record.
    public async Task<(bool Succeeded, string? Error, string UserId)> CreateHealthcareStaffAsync(
        RegisterHealthcareStaffRequest request,
        Guid institutionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.GivenName) ||
            string.IsNullOrWhiteSpace(request.FamilyName) ||
            string.IsNullOrWhiteSpace(request.RegistrationNumber))
        {
            return (
                false,
                "All healthcare staff fields are required.",
                string.Empty);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        // Do not allow invalid roles or hospital administrators
        // to be registered through this method.
        if (!Enum.IsDefined(typeof(ProviderRole), request.Role) ||
            request.Role == ProviderRole.Administrator)
        {
            return (
                false,
                "Invalid healthcare staff role.",
                string.Empty);
        }

        // Confirm that the hospital exists and is active.
        var institution = await db.Institutions.FirstOrDefaultAsync(
            x => x.Id == institutionId && x.IsActive,
            cancellationToken);

        if (institution == null)
        {
            return (
                false,
                "The hospital does not exist or is inactive.",
                string.Empty);
        }

        // Check that the professional registration number is unique.
        var registrationExists = await db.Providers.AnyAsync(
            x => x.RegistrationNumber == request.RegistrationNumber.Trim(),
            cancellationToken);

        if (registrationExists)
        {
            return (
                false,
                "A provider with this registration number already exists.",
                string.Empty);
        }

        // Clinical officers receive their own Identity role.
        // Other healthcare professionals use the Provider role.
        var identityRole = request.Role == ProviderRole.ClinicalOfficer
            ? "ClinicalOfficer"
            : "Provider";

        // UserManager and this service use the same DbContext.
        // This transaction covers the Identity account and Provider record.
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Create the login account.
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = $"{request.GivenName.Trim()} {request.FamilyName.Trim()}",
                InstitutionId = institutionId,
                EmailConfirmed = true
            };

            var userResult = await userManager.CreateAsync(
                user,
                request.Password);

            if (!userResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                return (
                    false,
                    string.Join("; ", userResult.Errors.Select(x => x.Description)),
                    string.Empty);
            }

            // Assign the Identity role.
            var roleResult = await userManager.AddToRoleAsync(
                user,
                identityRole);

            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                return (
                    false,
                    string.Join("; ", roleResult.Errors.Select(x => x.Description)),
                    string.Empty);
            }

            // Create the Provider record.
            var provider = new Provider(
                request.GivenName,
                request.FamilyName,
                request.RegistrationNumber,
                request.Role,
                institutionId);

            // Link the Provider record to the login account.
            provider.LinkUser(user.Id);

            db.Providers.Add(provider);

            await db.SaveChangesAsync(cancellationToken);

            // Commit both records together.
            await transaction.CommitAsync(cancellationToken);

            return (true, null, user.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            return (
                false,
                $"Healthcare staff registration failed: {ex.Message}",
                string.Empty);
        }
    }
}

public sealed class FhirService(
    PlatformDbContext db,
    IHttpClientFactory httpClientFactory,
    IOptions<FirelyServerOptions> options,
    IAuditService audit) : IFhirService
{
    private static readonly HashSet<string> SupportedResourceTypes =
        new(StringComparer.Ordinal)
        {
            "Patient",
            "Observation",
            "Encounter",
            "MedicationRequest",
            "MedicationStatement",
            "Practitioner",
            "Organization"
        };

    public Task<FhirValidationResult> ValidateAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Task.FromResult(
                new FhirValidationResult(
                    false,
                    null,
                    null,
                    new[] { "A FHIR JSON payload is required." }));
        }

        try
        {
            var parser = new FhirJsonParser();
            var resource = parser.Parse<FhirResource>(json);
            var issues = new List<string>();

            if (!SupportedResourceTypes.Contains(resource.TypeName))
            {
                issues.Add(
                    $"Resource type '{resource.TypeName}' is not enabled by this platform.");
            }

            return Task.FromResult(
                new FhirValidationResult(
                    issues.Count == 0,
                    resource.TypeName,
                    resource.Id,
                    issues));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                new FhirValidationResult(
                    false,
                    TryGetResourceType(json),
                    null,
                    new[] { $"FHIR JSON parsing failed: {ex.Message}" }));
        }
    }

    public async Task<FhirExchangeResult> ExchangeAsync(
        string json,
        Guid? patientId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(json, cancellationToken);

        var local = new FhirResourceRecord(
            validation.ResourceType ?? "Unknown",
            validation.ResourceId,
            patientId,
            json);

        db.FhirResourceRecords.Add(local);
        await db.SaveChangesAsync(cancellationToken);

        if (!validation.IsValid)
        {
            local.MarkFailed(string.Join(" ", validation.Issues));
            await db.SaveChangesAsync(cancellationToken);

            await audit.WriteAsync(
                "FHIR.Validate",
                nameof(FhirResourceRecord),
                local.Id.ToString(),
                "Failed",
                cancellationToken: cancellationToken);

            return new FhirExchangeResult(
                false,
                validation.ResourceType,
                validation.ResourceId,
                null,
                string.Join(" ", validation.Issues),
                local.Id);
        }

        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            const string error =
                "Firely Server is not configured. Set FirelyServer:BaseUrl before exchanging resources.";

            local.MarkFailed(error);
            await db.SaveChangesAsync(cancellationToken);

            await audit.WriteAsync(
                "FHIR.Exchange",
                nameof(FhirResourceRecord),
                local.Id.ToString(),
                "Failed",
                cancellationToken: cancellationToken);

            return new FhirExchangeResult(
                false,
                validation.ResourceType,
                validation.ResourceId,
                null,
                error,
                local.Id);
        }

        try
        {
            var client = httpClientFactory.CreateClient("FirelyServer");

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                validation.ResourceType);

            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/fhir+json");

            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/fhir+json"));

            if (!string.IsNullOrWhiteSpace(options.Value.BearerToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        options.Value.BearerToken);
            }

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                local.MarkFailed(
                    $"Firely Server returned {(int)response.StatusCode}: {body}");

                await db.SaveChangesAsync(cancellationToken);

                await audit.WriteAsync(
                    "FHIR.Exchange",
                    nameof(FhirResourceRecord),
                    local.Id.ToString(),
                    "Failed",
                    cancellationToken: cancellationToken);

                return new FhirExchangeResult(
                    false,
                    validation.ResourceType,
                    validation.ResourceId,
                    body,
                    local.LastError,
                    local.Id);
            }

            var remoteId = TryGetResourceId(body) ?? validation.ResourceId;

            local.MarkSucceeded(remoteId);
            await db.SaveChangesAsync(cancellationToken);

            await audit.WriteAsync(
                "FHIR.Exchange",
                nameof(FhirResourceRecord),
                local.Id.ToString(),
                "Success",
                cancellationToken: cancellationToken);

            return new FhirExchangeResult(
                true,
                validation.ResourceType,
                remoteId,
                body,
                null,
                local.Id);
        }
        catch (Exception ex)
        {
            local.MarkFailed(ex.Message);
            await db.SaveChangesAsync(cancellationToken);

            await audit.WriteAsync(
                "FHIR.Exchange",
                nameof(FhirResourceRecord),
                local.Id.ToString(),
                "Failed",
                cancellationToken: cancellationToken);

            return new FhirExchangeResult(
                false,
                validation.ResourceType,
                validation.ResourceId,
                null,
                ex.Message,
                local.Id);
        }
    }

    public async Task<IReadOnlyCollection<FhirResourceDto>> ListLocalAsync(
        CancellationToken cancellationToken = default) =>
        await db.FhirResourceRecords
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .Select(x => new FhirResourceDto(
                x.Id,
                x.ResourceType,
                x.ResourceId,
                x.PatientId,
                x.Status,
                x.LastError,
                x.SynchronizedAtUtc))
            .ToListAsync(cancellationToken);

    private static string? TryGetResourceType(string json)
    {
        try
        {
            return JsonDocument.Parse(json)
                .RootElement.TryGetProperty("resourceType", out var value)
                    ? value.GetString()
                    : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetResourceId(string json)
    {
        try
        {
            return JsonDocument.Parse(json)
                .RootElement.TryGetProperty("id", out var value)
                    ? value.GetString()
                    : null;
        }
        catch
        {
            return null;
        }
    }
}
