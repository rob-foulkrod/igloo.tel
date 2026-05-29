using Microsoft.Extensions.Logging;

namespace Igloo.Telemetry;

/// <summary>
/// Strongly-typed log event IDs for the Igloo Events domain.
/// Use with <see cref="ILogger"/> to emit structured, queryable telemetry.
/// In Application Insights these surface as customEvents / traces with stable names.
///
/// The <c>*Id</c> constants are the single source of truth for numeric IDs.
/// <see cref="IglooLoggers"/> references them so the two can never drift.
/// </summary>
public static class IglooEventIds
{
    // ── Event management (1000–1099) ──────────────────────────────────────────
    public const int EventCreatedId   = 1001;
    public const int EventUpdatedId   = 1002;
    public const int EventDeletedId   = 1003;
    public const int EventNotFoundId  = 1004;

    public static readonly EventId EventCreated   = new(EventCreatedId,  "igloo.event.created");
    public static readonly EventId EventUpdated   = new(EventUpdatedId,  "igloo.event.updated");
    public static readonly EventId EventDeleted   = new(EventDeletedId,  "igloo.event.deleted");
    public static readonly EventId EventNotFound  = new(EventNotFoundId, "igloo.event.not_found");

    // ── Registration (1100–1199) ──────────────────────────────────────────────
    public const int RegistrationCreatedId  = 1101;
    public const int RegistrationRejectedId = 1102;
    public const int CapacityFullId         = 1103;

    public static readonly EventId RegistrationCreated  = new(RegistrationCreatedId,  "igloo.registration.created");
    public static readonly EventId RegistrationRejected = new(RegistrationRejectedId, "igloo.registration.rejected");
    public static readonly EventId CapacityFull         = new(CapacityFullId,          "igloo.event.capacity_full");
}
