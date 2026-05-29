# Igloo.Telemetry

Structured telemetry, distributed tracing, and custom metrics for Igloo Events applications.  
Built on `System.Diagnostics` (OpenTelemetry-compatible) with first-class **Azure Application Insights** support.

---

## What's Inside

| Type | Purpose |
|---|---|
| `IglooActivitySource` | `ActivitySource` for distributed tracing — one source per operation |
| `IglooMetrics` | Custom counters, histograms, and observable gauges via `System.Diagnostics.Metrics` |
| `IglooLoggers` | Source-generated, zero-allocation `ILogger` extension methods |
| `IglooEventIds` | Stable numeric event IDs for queryable log search |
| `IglooTelemetryMiddleware` | ASP.NET Core middleware that enriches traces and flags slow requests |
| `IglooTelemetryExtensions` | One-liner DI + pipeline registration |

---

## Quick Start

### 1 — Install

```xml
<PackageReference Include="Igloo.Telemetry" Version="1.0.0" />
```

### 2 — Register services (`Program.cs`)

**Local / console output only:**
```csharp
builder.Services.AddIglooTelemetry();
```

**With Azure Application Insights (recommended for Azure):**
```csharp
// Install: dotnet add package Azure.Monitor.OpenTelemetry.AspNetCore
builder.Services.AddOpenTelemetry()
    .UseAzureMonitor()            // reads APPLICATIONINSIGHTS_CONNECTION_STRING automatically
    .AddIglooInstrumentation();   // registers Igloo ActivitySource + Meter

builder.Services.AddIglooTelemetry();
```

Set the connection string in your environment (or App Service → Configuration):
```
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...
```
No code changes needed between local and Azure — the env var is the only switch.

### 3 — Add middleware

```csharp
app.UseIglooTelemetry();   // after UseRouting, before MapControllers
```

---

## Usage in Services / Controllers

### Distributed tracing

```csharp
using var activity = IglooActivitySource.StartCreateEvent(evt.Title);
// ... do work ...
activity?.SetStatus(ActivityStatusCode.Ok);
```

### Metrics

```csharp
// Inject IglooMetrics via DI
public EventsController(IglooMetrics metrics) { ... }

// Record a registration with capacity utilization
_metrics.RecordRegistration(eventId, capacity, registeredCount);

// Record a rejection with a reason
_metrics.RecordRejection(eventId, "capacity_full");
```

After startup, wire up live counts once services are ready:
```csharp
var metrics = app.Services.GetRequiredService<IglooMetrics>();
var events  = app.Services.GetRequiredService<IEventService>();
var regs    = app.Services.GetRequiredService<IRegistrationService>();
metrics.RegisterProviders(
    totalEvents:        () => events.GetAll().Count(),
    totalRegistrations: () => regs.GetTotalCount());
```

### Structured logging

```csharp
// Zero-allocation — uses LoggerMessage source generation
_logger.EventCreated(evt.Id, evt.Title, evt.Location, evt.Capacity);
_logger.RegistrationCreated(reg.Id, emailDomain, eventId, registered, capacity);
_logger.CapacityFull(eventId, capacity);
```

These surface in Application Insights as **Traces** with stable `EventName` fields,
making them easy to query with KQL:
```kusto
traces
| where customDimensions["EventName"] == "igloo.registration.rejected"
| summarize count() by tostring(customDimensions["Reason"]), bin(timestamp, 1h)
```

---

## Application Insights Blade Guide

| What you see | Where |
|---|---|
| Request traces enriched with `igloo.service`, `igloo.user` | **Transaction search** |
| Slow requests (> 2 s) | **Failures → Exceptions** |
| Custom counters (`igloo.events.created`, etc.) | **Metrics explorer** |
| Capacity utilization histogram | **Metrics explorer → igloo.events.capacity_utilization** |
| Live event/registration counts | **Live Metrics** |
| Structured log queries | **Logs (KQL) → traces table** |
