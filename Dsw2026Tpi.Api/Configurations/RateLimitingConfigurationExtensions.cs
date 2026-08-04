using Dsw2026Tpi.CrossCutting.Models;
using Dsw2026Tpi.CrossCutting.Resources;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace Dsw2026Tpi.Api.Configurations;

public static class RateLimitingConfigurationExtensions
{
    public const string AdminAuthenticationPolicy = "admin-authentication";
    public const string PatientAuthenticationPolicy = "patient-authentication";
    public const string AppointmentBookingPolicy = "appointment-booking";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("RateLimiting").Get<RateLimitingSettings>() ?? new RateLimitingSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");

                logger.LogWarning(
                    "Solicitud rechazada por rate limiting. Path: {Path}. Method: {Method}. PartitionKey: {PartitionKey}",
                    context.HttpContext.Request.Path,
                    context.HttpContext.Request.Method,
                    GetUserOrIpPartitionKey(context.HttpContext));

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                var response = JsonSerializer.Serialize(new ErrorResponse(nameof(ErrorCodes.RATE_LIMIT_EXCEEDED), ErrorCodes.RATE_LIMIT_EXCEEDED));
                await context.HttpContext.Response.WriteAsync(response, cancellationToken);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                BuildLimiter(GetUserOrIpPartitionKey(httpContext), settings.General));

            options.AddPolicy(AdminAuthenticationPolicy, httpContext =>
                BuildLimiter(GetIpPartitionKey(httpContext), settings.AdminAuthentication));

            options.AddPolicy(PatientAuthenticationPolicy, httpContext =>
                BuildLimiter(GetIpPartitionKey(httpContext), settings.PatientAuthentication));

            options.AddPolicy(AppointmentBookingPolicy, httpContext =>
                BuildLimiter(GetUserOrIpPartitionKey(httpContext), settings.AppointmentBooking));
        });

        return services;
    }

    private static RateLimitPartition<string> BuildLimiter(string partitionKey, RateLimitPolicySettings settings)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.PermitLimit,
                Window = TimeSpan.FromMinutes(settings.WindowInMinutes),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    private static string GetUserOrIpPartitionKey(HttpContext httpContext)
    {
        return httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue(ClaimTypes.Name)
            ?? GetIpPartitionKey(httpContext);
    }

    private static string GetIpPartitionKey(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
    }

    private sealed class RateLimitingSettings
    {
        public RateLimitPolicySettings AdminAuthentication { get; init; } = new() { PermitLimit = 5, WindowInMinutes = 1 };
        public RateLimitPolicySettings PatientAuthentication { get; init; } = new() { PermitLimit = 10, WindowInMinutes = 1 };
        public RateLimitPolicySettings AppointmentBooking { get; init; } = new() { PermitLimit = 5, WindowInMinutes = 1 };
        public RateLimitPolicySettings General { get; init; } = new() { PermitLimit = 100, WindowInMinutes = 1 };
    }

    private sealed class RateLimitPolicySettings
    {
        public int PermitLimit { get; init; }
        public int WindowInMinutes { get; init; }
    }
}
