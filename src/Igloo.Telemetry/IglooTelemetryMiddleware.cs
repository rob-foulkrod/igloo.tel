using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Igloo.Telemetry;

/// <summary>
/// ASP.NET Core middleware that enriches the current distributed trace activity
/// with Igloo-specific HTTP attributes and records per-request metrics.
///
/// Register via <c>app.UseIglooTelemetry()</c> (provided in the DI extensions).
/// In Application Insights this data appears on the request dependency telemetry.
/// Slow requests (> 2 s) emit a warning that surfaces in the <b>Logs (traces)</b>
/// table — query with <c>traces | where severityLevel == 2</c>.
/// </summary>
public sealed class IglooTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IglooMetrics _metrics;
    private readonly ILogger<IglooTelemetryMiddleware> _logger;
    private readonly IglooTelemetryOptions _options;

    public IglooTelemetryMiddleware(
        RequestDelegate next,
        IglooMetrics metrics,
        ILogger<IglooTelemetryMiddleware> logger,
        IOptions<IglooTelemetryOptions> options)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;

        // Stamp every request with the Igloo service identity tag
        activity?.SetTag("igloo.service", "igloo-events-web");

        // Enrich the trace with the authenticated user when configured.
        // Username (often an email/UPN) is PII — opt-in only.
        if (_options.IncludeUserName)
        {
            var userName = context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userName))
                activity?.SetTag("igloo.user", userName);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            // Tag the status code so App Insights can slice by HTTP result
            activity?.SetTag("http.response.status_code", context.Response.StatusCode);

            // Record request duration for the igloo.http.request.duration histogram
            _metrics.RequestDuration.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("http.route", context.GetEndpoint()?.DisplayName));

            // Log slow requests (> 2 s) — these appear in App Insights Logs (traces table),
            // NOT the Failures blade (which shows requests with success == false).
            if (sw.Elapsed.TotalSeconds > 2)
            {
                _logger.LogWarning(
                    "Slow request: {Method} {Path} took {ElapsedMs}ms (HTTP {StatusCode})",
                    context.Request.Method,
                    context.Request.Path,
                    sw.ElapsedMilliseconds,
                    context.Response.StatusCode);
            }
        }
    }
}
