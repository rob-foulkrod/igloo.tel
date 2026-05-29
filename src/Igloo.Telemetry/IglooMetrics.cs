using System.Diagnostics.Metrics;

namespace Igloo.Telemetry;

/// <summary>
/// Custom metrics for the Igloo Events domain using System.Diagnostics.Metrics.
/// Compatible with OpenTelemetry, Prometheus, and Azure Monitor exporters.
/// </summary>
public sealed class IglooMetrics : IDisposable
{
    /// <summary>The meter name — register with OTel builder via <c>AddMeter(IglooMetrics.MeterName)</c>.</summary>
    public const string MeterName = "Igloo.Events";

    private readonly Meter _meter;

    // ── Counters ──────────────────────────────────────────────────────────────

    /// <summary>Total number of events created since startup.</summary>
    public Counter<long> EventsCreated { get; }

    /// <summary>Total number of events deleted since startup.</summary>
    public Counter<long> EventsDeleted { get; }

    /// <summary>Total number of successful registrations.</summary>
    public Counter<long> RegistrationsCreated { get; }

    /// <summary>Total number of rejected registrations (capacity exceeded or validation error).</summary>
    public Counter<long> RegistrationsRejected { get; }

    // ── Histograms ────────────────────────────────────────────────────────────

    /// <summary>
    /// Tracks how full events are at registration time as a percentage (0–100).
    /// Useful for p50/p95 capacity utilization dashboards.
    /// </summary>
    public Histogram<double> CapacityUtilizationPercent { get; }

    // ── Request metrics ───────────────────────────────────────────────────────

    /// <summary>HTTP request duration in milliseconds, tagged by route.</summary>
    public Histogram<double> RequestDuration { get; }

    // ── Observable Gauges ─────────────────────────────────────────────────────

    private Func<int>? _totalEventsProvider;
    private Func<int>? _totalRegistrationsProvider;

    public IglooMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        EventsCreated = _meter.CreateCounter<long>(
            "igloo.events.created",
            unit: "{events}",
            description: "Total number of events created.");

        EventsDeleted = _meter.CreateCounter<long>(
            "igloo.events.deleted",
            unit: "{events}",
            description: "Total number of events deleted.");

        RegistrationsCreated = _meter.CreateCounter<long>(
            "igloo.registrations.created",
            unit: "{registrations}",
            description: "Total successful registrations.");

        RegistrationsRejected = _meter.CreateCounter<long>(
            "igloo.registrations.rejected",
            unit: "{registrations}",
            description: "Total rejected registrations (capacity or validation).");

        CapacityUtilizationPercent = _meter.CreateHistogram<double>(
            "igloo.events.capacity_utilization",
            unit: "%",
            description: "Capacity utilization at registration time (0–100%).");

        RequestDuration = _meter.CreateHistogram<double>(
            "igloo.http.request.duration",
            unit: "ms",
            description: "HTTP request duration in milliseconds.");

        // Observable gauges pull current state on each collection cycle
        _meter.CreateObservableGauge(
            "igloo.events.total",
            () => _totalEventsProvider?.Invoke() ?? 0,
            unit: "{events}",
            description: "Current total number of events in the system.");

        _meter.CreateObservableGauge(
            "igloo.registrations.total",
            () => _totalRegistrationsProvider?.Invoke() ?? 0,
            unit: "{registrations}",
            description: "Current total number of registrations in the system.");
    }

    /// <summary>
    /// Register a callback so the gauge can pull live counts from your service layer.
    /// Call this from your DI setup after services are configured.
    /// </summary>
    public void RegisterProviders(Func<int> totalEvents, Func<int> totalRegistrations)
    {
        _totalEventsProvider = totalEvents;
        _totalRegistrationsProvider = totalRegistrations;
    }

    /// <summary>Records a capacity utilization sample and increments the registration counter.</summary>
    public void RecordRegistration(int eventId, int capacity, int registeredCount)
    {
        RegistrationsCreated.Add(1, new KeyValuePair<string, object?>("igloo.event.id", eventId));

        if (capacity > 0)
        {
            var utilization = (registeredCount / (double)capacity) * 100.0;
            CapacityUtilizationPercent.Record(
                utilization,
                new KeyValuePair<string, object?>("igloo.event.id", eventId));
        }
    }

    /// <summary>Records a rejected registration with a reason tag.</summary>
    public void RecordRejection(int eventId, string reason) =>
        RegistrationsRejected.Add(1,
            new KeyValuePair<string, object?>("igloo.event.id", eventId),
            new KeyValuePair<string, object?>("igloo.rejection.reason", reason));

    public void Dispose() => _meter.Dispose();
}
