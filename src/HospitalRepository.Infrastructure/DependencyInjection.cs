
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HospitalRepository.Application;
using HospitalRepository.Domain.Entities;

namespace HospitalRepository.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
            "Host=localhost;Port=5432;Database=hospital_repository;Username=postgres;Password=postgres";

        services.AddDbContext<PlatformDbContext>(
            options => options.UseNpgsql(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<PlatformDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<FirelyServerOptions>(configuration.GetSection("FirelyServer"));

        services.AddScoped<JwtTokenService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IInstitutionService, InstitutionService>();
        services.AddScoped<IProviderService, ProviderService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IClinicalService, ClinicalService>();
        services.AddScoped<INationalSurveillanceService, NationalSurveillanceService>();
        services.AddScoped<IFhirService, FhirService>();

        // Register memory cache for geocoding results.
        services.AddMemoryCache();

        // Register the hospital location lookup service.
        services.AddHttpClient<IInstitutionGeocodingService, InstitutionGeocodingService>(
            client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "HospitalRepository/1.0 (National Health Information Exchange)");

                client.Timeout = TimeSpan.FromSeconds(15);
            });

        // Existing Firely Server HTTP client.
        services.AddHttpClient("FirelyServer", (serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<FirelyServerOptions>>()
                .Value;

            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(
                    options.BaseUrl.TrimEnd('/') + "/");
            }

            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    public static async Task SeedIdentityAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[]
        {
            "Administrator",
            "InstitutionAdmin",
            "Provider",
            "Patient",
            "ClinicalOfficer",
            "Auditor"
        })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var email = "admin@hospitalrepository.local";

        if (await userManager.FindByEmailAsync(email) is null)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Platform Administrator",
                EmailConfirmed = true
            };

            var created = await userManager.CreateAsync(user, "Admin1234");

            if (created.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Administrator");
            }
        }
    }

    public static async Task SeedHospitalRepositoryAsync(
        this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<PlatformDbContext>();

        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE IF EXISTS patients ADD COLUMN IF NOT EXISTS \"MasterPatientId\" varchar(100);");

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE patients SET \"MasterPatientId\" = 'MPI-' || replace(\"Id\"::text, '-', '') WHERE \"MasterPatientId\" IS NULL;");

        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE IF EXISTS patients ALTER COLUMN \"MasterPatientId\" SET NOT NULL;");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS ix_patients_master_patient_id ON patients (\"MasterPatientId\");");

        var east = await db.Institutions
            .SingleOrDefaultAsync(x => x.Code == "EHC-01");

        if (east is null)
        {
            east = new Institution(
                "Eastlands Community Hospital",
                "EHC-01",
                "12 Umoja Avenue");

            db.Institutions.Add(east);
        }

        var west = await db.Institutions
            .SingleOrDefaultAsync(x => x.Code == "WMC-01");

        if (west is null)
        {
            west = new Institution(
                "Westlands Medical Centre",
                "WMC-01",
                "45 Ring Road");

            db.Institutions.Add(west);
        }

        await db.SaveChangesAsync();

        const string mpi = "MPI-KE-10001";

        if (!await db.Patients.AnyAsync(
            x => x.MasterPatientId == mpi &&
                 x.ManagingInstitutionId == east.Id))
        {
            db.Patients.Add(new Patient(
                "EHC-MRN-1001",
                "Amina",
                "Otieno",
                new DateOnly(1992, 4, 18),
                "female",
                east.Id,
                "NAT-KE-10001",
                mpi));
        }

        if (!await db.Patients.AnyAsync(
            x => x.MasterPatientId == mpi &&
                 x.ManagingInstitutionId == west.Id))
        {
            db.Patients.Add(new Patient(
                "WMC-MRN-7788",
                "Amina",
                "Otieno",
                new DateOnly(1992, 4, 18),
                "female",
                west.Id,
                "NAT-KE-10001",
                mpi));
        }

        await db.SaveChangesAsync();
    }
}
