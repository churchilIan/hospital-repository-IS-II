
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using HospitalRepository.Application;
using HospitalRepository.Infrastructure;

namespace HospitalRepository.Web.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IUserService users,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        // Public registration is strictly for patients.
        var patientRequest = request with
        {
            Role = "Patient",
            InstitutionId = null
        };

        var result = await users.RegisterAsync(
            patientRequest,
            cancellationToken);

        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, new
            {
                userId = result.UserId,
                message = "Patient account registered successfully."
            })
            : BadRequest(new { error = result.Error });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        // Validate the user's credentials and generate JWT.
        var result = await users.LoginAsync(request, cancellationToken);

        if (result is null)
        {
            return Unauthorized(new
            {
                error = "Invalid email or password."
            });
        }

        // Retrieve the actual user account.
        var user = await userManager.FindByIdAsync(result.UserId);

        if (user is null)
        {
            return Unauthorized(new
            {
                error = "User account could not be found."
            });
        }

        // Build the claims for browser authentication.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        // Add the user's assigned roles.
        var roles = await userManager.GetRolesAsync(user);

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Include the hospital association where available.
        if (user.InstitutionId.HasValue)
        {
            claims.Add(new Claim(
                "InstitutionId",
                user.InstitutionId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        // Issue the browser authentication cookie.
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true
            });

        // Return the original JWT login response.
        return Ok(result);
    }
}
