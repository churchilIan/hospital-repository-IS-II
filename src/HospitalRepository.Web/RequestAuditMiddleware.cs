using System.Security.Claims;
using HospitalRepository.Application;

namespace HospitalRepository.Web;

public sealed class RequestAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuditService audit)
    {
        await next(context);
        if (!context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/api/auth")) return;
        try
        {
            var actor = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var outcome = context.Response.StatusCode is >= 200 and < 400 ? "Success" : "Failed";
            await audit.WriteAsync("API.Request", "HttpRequest", null, outcome, context.Request.Path, context.TraceIdentifier, context.Connection.RemoteIpAddress?.ToString(), actor, context.RequestAborted);
        }
        catch
        {
            // Audit persistence must not replace the original API response.
        }
    }
}
