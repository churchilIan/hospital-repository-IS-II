using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using HospitalRepository.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hospital Repository",
        Version = "v1",
        Description = "Secure healthcare exchange APIs using HL7 FHIR R4."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT bearer token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});


const string SmartScheme = "Smart";

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
          ?? new JwtOptions();

var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwt.Key));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = SmartScheme;
        options.DefaultAuthenticateScheme = SmartScheme;
        options.DefaultChallengeScheme = SmartScheme;
        options.DefaultForbidScheme = SmartScheme;
    })
    .AddPolicyScheme(SmartScheme, "Cookie or JWT", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();

            // API requests must use JWT authentication.
            if (context.Request.Path.StartsWithSegments("/api"))
                return JwtBearerDefaults.AuthenticationScheme;

            // Requests carrying a Bearer token use JWT.
            if (authorization.StartsWith(
                "Bearer ", StringComparison.OrdinalIgnoreCase))
                return JwtBearerDefaults.AuthenticationScheme;

            // Normal browser pages use authentication cookies.
            return CookieAuthenticationDefaults.AuthenticationScheme;
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "HospitalRepository.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdministratorOnly", policy => policy.RequireRole("Administrator"));
    options.AddPolicy("AuditorOrAdministrator", policy => policy.RequireRole("Auditor", "Administrator"));
});

var app = builder.Build();

app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = exception is ArgumentException or InvalidOperationException ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        error = context.Response.StatusCode == StatusCodes.Status400BadRequest ? "ValidationError" : "ServerError",
        message = app.Environment.IsDevelopment() ? exception?.Message : "An unexpected error occurred."
    });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<HospitalRepository.Web.RequestAuditMiddleware>();
app.MapRazorPages();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "hospital-repository", utc = DateTime.UtcNow }));

if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex) { app.Logger.LogWarning(ex, "Database migration skipped because PostgreSQL was not available."); }
}

if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
{
    try { await app.Services.SeedIdentityAsync(); }
    catch (Exception ex) { app.Logger.LogWarning(ex, "Identity seed skipped because the database was not available."); }
    try { await app.Services.SeedHospitalRepositoryAsync(); }
    catch (Exception ex) { app.Logger.LogWarning(ex, "Hospital Repository demo seed skipped because the database was not available."); }
}

app.Run();

public partial class Program { }
