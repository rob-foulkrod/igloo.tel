namespace Igloo.Telemetry;

/// <summary>
/// Configuration options for <see cref="IglooTelemetryMiddleware"/>.
/// Register via <c>builder.Services.Configure&lt;IglooTelemetryOptions&gt;(...)</c>.
/// </summary>
public sealed class IglooTelemetryOptions
{
    /// <summary>
    /// When <c>true</c>, the authenticated user's name is stamped on each trace span
    /// as the <c>igloo.user</c> tag. Defaults to <c>false</c> because the username
    /// (often an email/UPN) is PII.
    /// </summary>
    public bool IncludeUserName { get; set; } = false;
}
