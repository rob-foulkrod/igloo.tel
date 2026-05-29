# Igloo.Telemetry — Code Review

**Scope:** `src/Igloo.Telemetry/*`, `Igloo.Telemetry.csproj`, `.github/workflows/ci-publish.yml`, `.azdo/ci-publish.yaml`, `README.md`.
**Focus:** Demo intent is _publish a NuGet to a feed and consume it back_, so packaging and CI get the most attention. Code is a secondary review — flagging things that would embarrass us in a class setting, not bikeshedding.

Severity legend: **(blocker)** must fix before publishing • **(important)** fix before students copy this as a pattern • **(nice)** polish.

---

## 1. Packaging (`Igloo.Telemetry.csproj`)

### 1.1 Wrong ASP.NET Core dependency — **(blocker)**
```xml
<PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" Version="2.3.0" />
```
`Microsoft.AspNetCore.Http.Abstractions` 2.x is the **legacy .NET Framework / .NET Core 2.x** abstractions package. It is deprecated for in-process ASP.NET Core libraries since 3.0. Pulling it into a `net10.0` library:

- Drags in old `Microsoft.AspNetCore.Http.Features` 2.x and `Microsoft.AspNetCore.Hosting.*` 2.x types that can collide with the in-box `Microsoft.AspNetCore.App` shared framework.
- Will produce type-forwarding / ambiguous-reference warnings (or worse) in some consumers.

For a `net10.0` ASP.NET Core library, the correct pattern is a framework reference, not a NuGet package:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```
Then drop the `Microsoft.AspNetCore.Http.Abstractions` PackageReference entirely. `HttpContext`, `RequestDelegate`, `IApplicationBuilder`, `UseMiddleware<T>` all come from the shared framework.

> Note: a `FrameworkReference` on `Microsoft.AspNetCore.App` makes this an **ASP.NET Core library** — consumers must be ASP.NET Core apps. That's already the case for the middleware, so it's the right call.

### 1.2 Missing required NuGet metadata — **(important)**
For any real publish (NuGet.org, Azure Artifacts, GitHub Packages) the following are expected and surfaced in the gallery UI:

```xml
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<PackageProjectUrl>https://github.com/<owner>/igloo.tel</PackageProjectUrl>
<RepositoryUrl>https://github.com/<owner>/igloo.tel.git</RepositoryUrl>
<RepositoryType>git</RepositoryType>
<Copyright>© Igloo Events</Copyright>
```

Without a license expression, NuGet.org rejects the upload outright and Azure Artifacts shows a warning banner.

### 1.3 No SourceLink / symbol package — **(important)**
For a debuggable shipped package, add:

```xml
<PublishRepositoryUrl>true</PublishRepositoryUrl>
<EmbedUntrackedSources>true</EmbedUntrackedSources>
<IncludeSymbols>true</IncludeSymbols>
<SymbolPackageFormat>snupkg</SymbolPackageFormat>
<ContinuousIntegrationBuild Condition="'$(TF_BUILD)' == 'true' or '$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
```
plus
```xml
<PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />
```
This gives students a step-debuggable experience when they install the package back into the consumer.

### 1.4 Hard-coded `<Version>` — **(nice)**
`<Version>1.0.0</Version>` is fine for the first push, but the CI scripts shell out to `grep '<Version>'` to read it (see §4). A tiny indirection avoids the brittle regex:

```xml
<VersionPrefix>1.0.0</VersionPrefix>
<VersionSuffix Condition="'$(VersionSuffix)' == '' and '$(Configuration)' == 'Debug'">dev</VersionSuffix>
```
Then CI can simply pass `/p:VersionPrefix=… /p:VersionSuffix=pre.$(BuildId)`.

### 1.5 `GenerateDocumentationFile` without warning controls — **(nice)**
`<GenerateDocumentationFile>true</GenerateDocumentationFile>` is set, but several public surfaces lack XML docs (e.g. `IglooMetrics` constructor, `IglooEventIds` fields). That floods the build with CS1591. Either:

- Document every public symbol, or
- `<NoWarn>$(NoWarn);CS1591</NoWarn>` on just this library and document selectively.

### 1.6 README packaging path — **(nice)**
```xml
<None Include="..\..\README.md" Pack="true" PackagePath="\" />
```
`PackagePath="\"` works on Windows but the idiomatic, cross-platform form is `PackagePath=""` (root of the package). Functionally identical today; cleaner under SDK style.

---

## 2. Code

### 2.1 `IglooTelemetryMiddleware` — unused dependency — **(important)**
```csharp
public IglooTelemetryMiddleware(
    RequestDelegate next,
    IglooMetrics metrics,            // injected
    ILogger<IglooTelemetryMiddleware> logger)
```
`_metrics` is stored but never read in `InvokeAsync`. Either:

- Actually record per-request metrics (e.g. a `Counter<long>` for requests and a `Histogram<double>` for duration), or
- Delete the parameter. Carrying an unused dependency is misleading documentation.

A natural addition that justifies the dependency:
```csharp
_metrics.RequestDuration.Record(sw.Elapsed.TotalMilliseconds,
    new KeyValuePair<string,object?>("http.route", context.GetEndpoint()?.DisplayName));
```

### 2.2 Doc comment is factually wrong — **(important)**
Both the middleware XML doc and README claim slow requests "stand out in the **Failures blade**". Application Insights' **Failures** blade shows requests with `success == false` and dependency failures. A `LogWarning` lands in the **traces** table under **Logs** (or **Performance** if duration-based). Either change the wording or actually mark the request as a failure (e.g. `activity?.SetStatus(ActivityStatusCode.Error)` with the slow-request reason).

### 2.3 PII tagging in middleware — **(important for "don't do anything dumb")**
```csharp
var userName = context.User?.Identity?.Name;
if (!string.IsNullOrEmpty(userName))
    activity?.SetTag("igloo.user", userName);
```
Username (often an email/UPN) is PII and will be persisted in every distributed trace span. For a sample that students will copy, at minimum:

- Gate it behind an option (`IglooTelemetryOptions.IncludeUserName`), or
- Hash it (`SHA256` → short hex), or
- Use a stable surrogate (`oid` claim).

### 2.4 OTel attribute naming — **(nice)**
Several tags use bare names instead of namespaced ones, which contradicts OpenTelemetry semantic conventions (custom attributes should be vendor-namespaced):

| Current | OTel-conformant |
|---|---|
| `event.id`, `event.title`, `event.capacity`, `event.registered`, `event.remaining` | `igloo.event.id`, `igloo.event.title`, … |
| `registration.email.domain` | `igloo.registration.email.domain` |
| `http.response.status_code` | ✅ correct (already a semconv key) |

This matters because consumers ingesting multiple libraries' telemetry will hit attribute-name collisions otherwise.

### 2.5 Duplicated event-ID source of truth — **(important)**
`IglooEventIds` defines numeric IDs **and** `IglooLoggers` repeats the same numbers inside `[LoggerMessage(EventId = 1001, …)]`. They are guaranteed to drift. Pick one:

- Keep `IglooLoggers` as the source of truth (the source generator already creates the `EventId`), or
- Keep `IglooEventIds` and reference it: `EventId = IglooEventIds.EventCreated.Id`. (Allowed in `[LoggerMessage]` since the generator evaluates the constant at compile time.)

### 2.6 `IglooMetrics` public fields — **(important)**
```csharp
public readonly Counter<long> EventsCreated;
public readonly Counter<long> EventsDeleted;
…
```
Public mutable-shape (technically `readonly`, but field) types in a published API foreclose future encapsulation and break binary compat if you ever switch to a property. Idiomatic .NET:

```csharp
public Counter<long> EventsCreated { get; }
```
Same payload, future-proof.

### 2.7 `IglooMetrics.RegisterProviders` is not thread-safe — **(nice)**
The observable-gauge callbacks may fire on a measurement thread the instant `Meter` is created, while `RegisterProviders` may be called later. The `?.Invoke() ?? 0` null-coalesce avoids NREs, but if you want a clean story, accept the providers in the constructor (with DI):

```csharp
public IglooMetrics(IOptions<IglooMetricsOptions> options) { … }
```
or expose the registration as `Volatile.Write`/`Interlocked.Exchange`.

### 2.8 `IglooActivitySource.Source` is never disposed — **(nice)**
For a process-lifetime static this is harmless, but Roslyn's CA2000/CA1063 analyzers will whine and students may copy that pattern into a non-static context. Add a brief XML remark noting it's intentionally a process-lifetime singleton.

### 2.9 `StartRegister(int eventId, string attendeeEmail)` — **(important)**
The method accepts a raw email but only tags the domain. The full email is still passed across the call boundary and may live in a heap allocation that ends up in a memory dump or in a future logging change. Safer signature:

```csharp
public static Activity? StartRegister(int eventId, string attendeeEmailDomain)
```
…and let the caller compute the domain. (Or rename the parameter to `attendeeEmailDomain` and document the caller's responsibility — but a wrong-shaped parameter is an attractive nuisance.)

---

## 3. Solution & repo hygiene

### 3.1 Placeholder project GUID — **(nice)**
`igloo.tel.sln` uses `{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}`. Visual Studio will tolerate it forever, but `dotnet sln` and some tooling assume uniqueness across solutions. Regenerate once (`dotnet new sln`, then `dotnet sln add`).

### 3.2 `bin/` and `obj/` appear in the workspace tree — **(important)**
The workspace listing shows `bin/Debug` and `obj/` inside `Igloo.Telemetry/`. There's no visible `.gitignore`. Confirm one exists at the repo root; the standard is `dotnet new gitignore`. Shipping `obj/project.assets.json` in source control is a frequent classroom mistake.

### 3.3 No test project — **(important for a sample)**
For a NuGet that students will distribute, at least one smoke test (`Igloo.Telemetry.Tests`) that:

- Asserts `IglooActivitySource.SourceName` matches the assembly name,
- Asserts `AddIglooTelemetry()` registers `IglooMetrics` as a singleton,
- Asserts `AddIglooInstrumentation()` registers the source and meter (via `TracerProvider`/`MeterProvider` resolution).

A 50-line test project doubles the credibility of the package.

---

## 4. CI / Publish pipelines

### 4.1 ADO pipeline runs `pack` twice — **(blocker)**
`.azdo/ci-publish.yaml` has two `DotNetCoreCLI@2 / command: pack` steps. The first runs **before** the "Compute package version" step sets `PACKAGE_VERSION`, so it packs with an empty env var and falls back to the csproj `<Version>`. The output of the first pack is then thrown away because the second pack writes to `$(Build.ArtifactStagingDirectory)/nupkg`. Reorder and delete the first pack:

```yaml
- bash: |
    # compute PACKAGE_VERSION
  displayName: Compute package version

- task: DotNetCoreCLI@2
  displayName: Pack
  inputs:
    command: pack
    packagesToPack: $(packageProject)
    configuration: $(buildConfiguration)
    nobuild: true
    versioningScheme: byEnvVar
    versionEnvVar: PACKAGE_VERSION
    outputDir: $(Build.ArtifactStagingDirectory)/nupkg
```

### 4.2 Version-extraction regex is fragile — **(nice)**
Both pipelines do:
```bash
grep '<Version>' $(packageProject) | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/'
```
This breaks if the csproj is reformatted across multiple lines or if MSBuild conditions are added. Cleaner:
```bash
BASE=$(dotnet msbuild $PACKAGE_PROJECT -getProperty:Version)
```
`-getProperty` exists in the .NET 8+ SDK and is the supported way to read evaluated MSBuild properties without parsing XML.

### 4.3 `.NET 10` SDK version pin is too loose — **(nice)**
Both files request `10.x`. For class reproducibility, pin a band you've actually tested:
```yaml
dotnet-version: '10.0.x'
```
Better: commit a `global.json` and let `setup-dotnet`/`UseDotNet@2` honor it. That keeps the pipeline file in lockstep with what students run locally.

### 4.4 No tests, no symbol push — **(important)**
Neither pipeline runs `dotnet test` or pushes the `.snupkg`. Even without symbols today, leave the placeholder step in so students see where it belongs. For GitHub Packages, snupkg push is the same `dotnet nuget push` command — symbols are picked up automatically from the same output directory.

### 4.5 GitHub workflow — `--store-password-in-clear-text` — **(nice)**
This is required on Linux runners by `dotnet nuget add source`, so it's actually correct, but worth a one-line comment explaining _why_ so students don't propagate it to their personal-machine NuGet.config.

### 4.6 Path filter ignores pipeline edits — **(nice)**
Both pipelines only trigger on `src/Igloo.Telemetry/**`. Add the workflow file itself so changes to CI are verified:

```yaml
paths:
  include:
    - src/Igloo.Telemetry/**
    - .azdo/ci-publish.yaml
```
(GitHub equivalent under `on.push.paths`.)

### 4.7 No `dotnet nuget push --api-key` for Azure Artifacts — context
The ADO pipeline relies on `NuGetAuthenticate@1` + `DotNetCoreCLI@2 push` with `nuGetFeedType: internal`. That's correct for Azure Artifacts. No change needed, just confirming the pattern is right.

---

## 5. README

- The KQL example uses `customDimensions["EventName"]` correctly. ✅
- The "Failures blade" claim is wrong (see §2.2).
- "Built on `System.Diagnostics` (OpenTelemetry-compatible)" — accurate, but the csproj _does_ pull OTel hosting transitively, so be explicit that the consumer ends up with an OTel dep.
- A small **"Versioning & publishing" section** would make the demo intent obvious to students: how the CI computes the version, where the package lands, and how to consume the GitHub Packages feed (the `nuget.config` snippet with `<packageSources>` and `<packageSourceCredentials>`).

---

## Recommended fix order for the demo

1. **Fix the ASP.NET Core dependency** (§1.1) — currently the most "dumb" thing in the package.
2. **Add license + repo metadata + SourceLink** (§1.2, §1.3) — unblocks any real feed.
3. **Collapse the duplicated `pack` step in ADO** (§4.1) — the pipeline is misleading as-is.
4. **Remove or use the unused `IglooMetrics` in middleware** (§2.1).
5. **Add `.gitignore`, regenerate sln GUID, add a smoke-test project** (§3.x).
6. **Cosmetic / API tidies** (§2.4–2.9, §1.4–1.6, §4.x).

Items 1–3 are the only ones I'd block a class demo on; the rest sharpen the lesson.
