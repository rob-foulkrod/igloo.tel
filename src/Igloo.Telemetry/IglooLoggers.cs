using Microsoft.Extensions.Logging;

namespace Igloo.Telemetry;

/// <summary>
/// High-performance compile-time log actions for the Igloo Events domain.
/// Uses <c>LoggerMessage.Define</c> — zero-allocation, structured, and queryable in
/// Application Insights as named traces with stable message templates.
/// </summary>
public static partial class IglooLoggers
{
    // ── Event management ──────────────────────────────────────────────────────

    [LoggerMessage(
        EventId   = IglooEventIds.EventCreatedId,
        EventName = "igloo.event.created",
        Level     = LogLevel.Information,
        Message   = "Event created: [{EventId}] {Title} at {Location} (capacity {Capacity})")]
    public static partial void EventCreated(
        this ILogger logger, int eventId, string title, string location, int capacity);

    [LoggerMessage(
        EventId   = IglooEventIds.EventUpdatedId,
        EventName = "igloo.event.updated",
        Level     = LogLevel.Information,
        Message   = "Event updated: [{EventId}] {Title}")]
    public static partial void EventUpdated(this ILogger logger, int eventId, string title);

    [LoggerMessage(
        EventId   = IglooEventIds.EventDeletedId,
        EventName = "igloo.event.deleted",
        Level     = LogLevel.Information,
        Message   = "Event deleted: [{EventId}]")]
    public static partial void EventDeleted(this ILogger logger, int eventId);

    [LoggerMessage(
        EventId   = IglooEventIds.EventNotFoundId,
        EventName = "igloo.event.not_found",
        Level     = LogLevel.Warning,
        Message   = "Event not found: [{EventId}]")]
    public static partial void EventNotFound(this ILogger logger, int eventId);

    // ── Registration ──────────────────────────────────────────────────────────

    [LoggerMessage(
        EventId   = IglooEventIds.RegistrationCreatedId,
        EventName = "igloo.registration.created",
        Level     = LogLevel.Information,
        Message   = "Registration created: [{RegistrationId}] attendee {EmailDomain} for event [{EventId}] ({Registered}/{Capacity} seats taken)")]
    public static partial void RegistrationCreated(
        this ILogger logger, int registrationId, string emailDomain, int eventId, int registered, int capacity);

    [LoggerMessage(
        EventId   = IglooEventIds.RegistrationRejectedId,
        EventName = "igloo.registration.rejected",
        Level     = LogLevel.Warning,
        Message   = "Registration rejected for event [{EventId}]: {Reason}")]
    public static partial void RegistrationRejected(this ILogger logger, int eventId, string reason);

    [LoggerMessage(
        EventId   = IglooEventIds.CapacityFullId,
        EventName = "igloo.event.capacity_full",
        Level     = LogLevel.Warning,
        Message   = "Event [{EventId}] is at full capacity ({Capacity} seats)")]
    public static partial void CapacityFull(this ILogger logger, int eventId, int capacity);
}
