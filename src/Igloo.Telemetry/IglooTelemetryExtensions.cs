using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Igloo.Telemetry;

/// <summary>
/// Extension methods for registering Igloo telemetry in an ASP.NET Core application.
///
/// <para><b>Minimal setup (console / local dev):</b></para>
/// <code>
/// builder.Services.AddIglooTelemetry();
/// // ...
/// app.UseIglooTelemetry();
/// </code>
///
/// <para><b>With Application Insights (recommended for Azure):</b></para>
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .UseAzureMonitor()          // picks up APPLICATIONINSIGHTS_CONNECTION_STRING
///     .AddIglooInstrumentation(); // adds Igloo ActivitySource + Meter
///
/// builder.Services.AddIglooTelemetry();
/// // ...
/// app.UseIglooTelemetry();
/// </code>
///
/// <para>
/// Set <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> in your environment or
/// App Service configuration — <c>UseAzureMonitor()</c> reads it automatically.
/// No code changes needed between local and cloud.
/// </para>
/// </summary>
public static class IglooTelemetryExtensions
{
    // ── Service registration ──────────────────────────────────────────────────

    /// <summary>
    /// Registers <see cref="IglooMetrics"/> as a singleton in the DI container.
    /// Call this regardless of whether you are using Application Insights.
    /// </summary>
    public static IServiceCollection AddIglooTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<IglooMetrics>();
        services.AddOptions<IglooTelemetryOptions>();
        return services;
    }

    // ── OpenTelemetry builder extensions ─────────────────────────────────────

    /// <summary>
    /// Adds Igloo's <see cref="IglooActivitySource"/> and <see cref="IglooMetrics"/> meter
    /// to an existing <see cref="OpenTelemetry.IOpenTelemetryBuilder"/>.
    ///
    /// Chain this after <c>.UseAzureMonitor()</c> or any other OTel exporter:
    /// <code>
    /// builder.Services.AddOpenTelemetry()
    ///     .UseAzureMonitor()
    ///     .AddIglooInstrumentation();
    /// </code>
    /// </summary>
    public static OpenTelemetry.IOpenTelemetryBuilder AddIglooInstrumentation(
        this OpenTelemetry.IOpenTelemetryBuilder builder)
    {
        builder
            .WithTracing(tracing => tracing
                .AddSource(IglooActivitySource.SourceName))
            .WithMetrics(metrics => metrics
                .AddMeter(IglooMetrics.MeterName));

        return builder;
    }

    // ── Middleware ────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds the <see cref="IglooTelemetryMiddleware"/> to the request pipeline.
    /// This enriches distributed traces with Igloo-specific HTTP attributes,
    /// records per-request duration metrics, and logs slow requests (&gt; 2 s)
    /// as warnings (visible in App Insights Logs / <c>traces</c> table).
    /// </summary>
    public static IApplicationBuilder UseIglooTelemetry(this IApplicationBuilder app) =>
        app.UseMiddleware<IglooTelemetryMiddleware>();
}
