using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace CraftingPlanner.Api.Services;

/// <summary>
/// The extra guard rails for the public demo, switched on by the <c>DemoMode</c> setting.
/// A copy run locally never has them, so the local app behaves exactly as built.
/// </summary>
/// <remarks>
/// A demo that anyone can edit and nobody logs in to needs three things the local app does
/// not: a way back to clean data, a limit on how hard one visitor can hit the API, and a way
/// to tell visitors apart behind the host's proxy. Section 7 of docs/DESIGN.md records the
/// risks each one answers.
/// </remarks>
public static class DemoMode
{
    /// <summary>The name of the rate-limiting policy applied to the API.</summary>
    public const string ApiPolicy = "api";

    /// <summary>How many API requests one visitor may make per minute.</summary>
    public const int RequestsPerMinute = 120;

    /// <summary>The largest request body accepted, in bytes. The biggest real request is a recipe of 20 lines, well under this.</summary>
    public const long LargestRequestBody = 64 * 1024;

    /// <summary>Checks whether demo mode is on.</summary>
    /// <param name="configuration">The app's settings.</param>
    /// <returns>True when the <c>DemoMode</c> setting is true.</returns>
    public static bool IsOn(IConfiguration configuration) => configuration.GetValue<bool>("DemoMode");

    /// <summary>Deletes the database file, so the app starts with the sample data again.</summary>
    /// <param name="databaseFile">Path of the database file.</param>
    /// <param name="logger">Where to note what happened.</param>
    public static void ResetDatabase(string databaseFile, ILogger logger)
    {
        var removed = 0;
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var file = databaseFile + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
                removed++;
            }
        }

        logger.LogInformation("Demo mode: removed {Count} database file(s); starting with the sample data.", removed);
    }

    /// <summary>Registers the services demo mode needs: the rate limiter and proxy awareness.</summary>
    /// <param name="builder">The app builder.</param>
    public static void AddServices(WebApplicationBuilder builder)
    {
        // Behind the host's proxy, the real visitor address arrives in a header. Trusting any
        // proxy is acceptable here because only the host's proxy can reach the app at all.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Each visitor gets a fixed window of requests per minute on the API. Pages and
        // pictures are not counted; they are cheap and the browser caches them.
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ApiPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = RequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Refusals use the same Problem Details format as every other rejection.
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(new
                    {
                        type = "https://tools.ietf.org/html/rfc6585#section-4",
                        title = "Too many requests",
                        status = StatusCodes.Status429TooManyRequests,
                        detail = $"This demo allows {RequestsPerMinute} API requests a minute per visitor. Wait a moment and try again.",
                    }),
                    cancellationToken);
            };
        });

        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = LargestRequestBody);
    }

    /// <summary>Adds the demo-mode steps to the request pipeline. Call before the endpoints are mapped.</summary>
    /// <param name="app">The app.</param>
    public static void Use(WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseRateLimiter();
    }
}
