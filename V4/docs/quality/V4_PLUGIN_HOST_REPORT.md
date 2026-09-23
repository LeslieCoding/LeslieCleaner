# V4 Plugin Isolation Host — Implementation and Verification Report

Date: 2026-08-13  
Specification: `PROMPT/Cleaner/LeslieCleaner_Development_Bible/Main/24_Plugin_System_Architecture.md`

## Outcome

The V4 plugin execution boundary is implemented as a dedicated child process. The main process validates packages and communicates with the host through authenticated, versioned, newline-delimited JSON messages over a current-user named pipe. It contains no reflection-based plugin loader. Reflection and `AssemblyLoadContext` exist only in `LeslieCleaner.PluginHost`.

## Implemented scope

- `LeslieCleaner.PluginSdk`: API version `1.0`, minimal `IPlugin` / `IPluginContext` lifecycle contract, execution and capability request records, permission token, and wire protocol records.
- `LeslieCleaner.PluginHost`: isolated executable; authenticated named-pipe server; initialize, execute, and stop lifecycle; one collectible dependency context per host; parent-process monitoring; protocol/frame validation; permission mediation.
- `PluginRuntime`: main-process proxy; one disposable process per active plugin; startup, execution, stop, cancellation, deadlines, restart after failure, forced process-tree termination, and telemetry.
- Least privilege: the runtime intersects approved state permissions with manifest permissions. The default token contains zero permissions. Capability requests not present in the token return `PERMISSION_DENIED`. Granted permissions still do not become ambient authority: unsupported privileged operations return `CAPABILITY_NOT_SUPPORTED`.
- Resource protection: process-level working set, total CPU time, and thread count are sampled during commands. Exceeding a configured budget terminates the entire host process and returns `RESOURCE_LIMIT_EXCEEDED`. Operation timeout also forcibly terminates the process.
- Crash isolation: the sample plugin uses `Environment.FailFast`; this can terminate only its child host, never the LeslieCleaner caller. A subsequent invocation creates a fresh host.
- DI extension: `AddLeslieCleanerPluginRuntime()` registers `IPluginRuntime` without modifying UI composition.
- Solution projects: SDK, host, real sample plugin, and isolated integration-test project were added to `LeslieCleaner.V4.sln`.

## Files delivered

- `src/LeslieCleaner.PluginSdk/LeslieCleaner.PluginSdk.csproj`
- `src/LeslieCleaner.PluginSdk/PluginContracts.cs`
- `src/LeslieCleaner.PluginSdk/PluginProtocol.cs`
- `src/LeslieCleaner.PluginHost/LeslieCleaner.PluginHost.csproj`
- `src/LeslieCleaner.PluginHost/Program.cs`
- `src/LeslieCleaner.Core/Abstractions/IPluginRuntime.cs`
- `src/LeslieCleaner.Core/Models/PluginRuntimeModels.cs`
- `src/LeslieCleaner.Infrastructure/Plugins/PluginRuntime.cs`
- `src/LeslieCleaner.Infrastructure/Plugins/PluginRuntimeRegistration.cs`
- `src/LeslieCleaner.SamplePlugin/LeslieCleaner.SamplePlugin.csproj`
- `src/LeslieCleaner.SamplePlugin/SamplePlugin.cs`
- `src/LeslieCleaner.Tests/Plugins/PluginRuntimeIntegrationTests.cs`
- `src/LeslieCleaner.PluginHost.IntegrationTests/*`

## Verification evidence

| Gate | Result | Evidence |
|---|---:|---|
| SDK + host Release build | PASS | `LeslieCleaner.PluginSdk` and `LeslieCleaner.PluginHost` built with 0 warnings / 0 errors. |
| Plugin-only Infrastructure Release build | PASS | `PluginRuntime`, registration, manager, and state store built with 0 warnings / 0 errors. |
| Normal Infrastructure Release build | PASS | After the shared security-module errors were resolved, Infrastructure built with 0 warnings / 0 errors (XML warnings temporarily suppressed only for unrelated shared code). |
| Full solution build | NOT RUN TO COMPLETION | Parent task requested immediate stop before another long command. |
| Successful real-plugin execution test | NOT EXECUTED | Test source exists and compiles after the final fully-qualified sample type fix, but no completed test-run evidence was produced. |
| Crash isolation and recovery test | NOT EXECUTED | Test source exists; no completed runtime result was produced. |
| Timeout termination test | NOT EXECUTED | Test source exists; no completed runtime result was produced. |
| Zero-permission rejection test | NOT EXECUTED | Test source exists; no completed runtime result was produced. |

Passed automated test count: **0**. This is reported explicitly rather than treating successful compilation as behavioral proof.

## Remaining verification

Run the plugin integration project in Release configuration, then run the complete solution build and two requested full-suite passes. The four plugin tests must show: distinct host PID and echo response; `HOST_CRASHED` followed by successful recovery; `EXECUTION_TIMEOUT` with non-running telemetry; and `PERMISSION_DENIED` under an empty token.

## Design limitations

- Resource budgets are enforced by active telemetry plus forced process-tree termination, rather than Windows Job Object quotas. This satisfies the requested verifiable fallback, but Job Objects would provide kernel-level memory/CPU enforcement.
- Permission mediation currently denies all privileged operations unless a future host-owned broker implements them. Plugins have no direct privileged LeslieCleaner service objects; however, operating-system sandboxing is process isolation plus least-privilege token mediation, not an AppContainer.
- The default production host path expects the host next to the application output. Packaging/copy integration still requires full-solution verification.
