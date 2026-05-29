using System.Diagnostics;
using System.Reflection;

namespace Igloo.Telemetry;

/// <summary>
/// Central ActivitySource for Igloo distributed tracing.
/// Provides factory methods for creating typed activities across the event/registration domain.
/// </summary>
public static class IglooActivitySource
{
    private static readonly AssemblyName AssemblyName = typeof(IglooActivitySource).Assembly.GetName();

    /// <summary>The canonical name used to register the ActivitySource in OpenTelemetry pipelines.</summary>
    public static readonly string SourceName = AssemblyName.Name!;

    /// <summary>The package version, useful for OpenTelemetry resource attributes.</summary>
    public static readonly string SourceVersion = AssemblyName.Version?.ToString() ?? "1.0.0";

    // One ActivitySource per library — consumers add this name to their OTel builder
    internal static readonly ActivitySource Source = new(SourceName, SourceVersion);

    // ── Event domain ──────────────────────────────────────────────────────────

    /// <summary>Starts an activity scoping a single event lookup.</summary>
    public static Activity? StartGetEvent(int eventId) =>
        Source.StartActivity("igloo.event.get", ActivityKind.Internal)?
              .AddIglooTag("igloo.event.id", eventId);

    /// <summary>Starts an activity scoping creation of a new event.</summary>
    public static Activity? StartCreateEvent(string title) =>
        Source.StartActivity("igloo.event.create", ActivityKind.Internal)?
              .AddIglooTag("igloo.event.title", title);

    /// <summary>Starts an activity scoping an event update.</summary>
    public static Activity? StartUpdateEvent(int eventId) =>
        Source.StartActivity("igloo.event.update", ActivityKind.Internal)?
              .AddIglooTag("igloo.event.id", eventId);

    /// <summary>Starts an activity scoping deletion of an event.</summary>
    public static Activity? StartDeleteEvent(int eventId) =>
        Source.StartActivity("igloo.event.delete", ActivityKind.Internal)?
              .AddIglooTag("igloo.event.id", eventId);

    // ── Registration domain ───────────────────────────────────────────────────

    /// <summary>Starts an activity scoping a registration attempt.</summary>
    /// <param name="eventId">The event being registered for.</param>
    /// <param name="attendeeEmailDomain">The domain portion of the attendee email (caller extracts — do not pass the full address).</param>
    public static Activity? StartRegister(int eventId, string attendeeEmailDomain) =>
        Source.StartActivity("igloo.registration.create", ActivityKind.Internal)?
              .AddIglooTag("igloo.event.id", eventId)
              .AddIglooTag("igloo.registration.email.domain", attendeeEmailDomain);

    /// <summary>Starts an activity scoping a capacity check.</summary>
    public static Activity? StartCapacityCheck(int eventId, int capacity, int registered) =>
        Source.StartActivity("igloo.event.capacity_check", ActivityKind.Internal)?
              .AddIglooTag("igloo.event.id", eventId)
              .AddIglooTag("igloo.event.capacity", capacity)
              .AddIglooTag("igloo.event.registered", registered)
              .AddIglooTag("igloo.event.remaining", capacity - registered);

    // ── Helpers ───────────────────────────────────────────────────────────────
}

internal static class ActivityExtensions
{
    internal static Activity AddIglooTag(this Activity activity, string key, object? value)
    {
        activity.SetTag(key, value);
        return activity;
    }
}
