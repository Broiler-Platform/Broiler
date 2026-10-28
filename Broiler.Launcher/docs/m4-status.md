# M4: start and manage BOSS

M4 implements the shared BOSS action for the Windows demo. Signing remains a final
production-release gate, and Linux runtime validation remains deferred. No new UI,
Graphics or other NuGet publication is required for this milestone.

## Launcher experience

- Every product's arrow menu offers Start BOSS independently of whether that
  desktop product is installed. The action starts the shared Writer web server;
  it does not imply that BOSS hosts Browser, Cell or Plate.
- Once verified ready, the card action becomes Open BOSS. Tools → BOSS also
  provides Start, Open in browser, Stop owned BOSS, Refresh status and View log.
- If BOSS is missing, Tools → BOSS offers explicit Install for user and Install
  for all actions, using M2's installation service and the M3 local demo bundle.
  Installation status/cancellation and administrator handoff follow the existing
  installer rules. A machine-installed binary is still started as the normal user.
- Starting, ready/running, already running, missing, port conflict, failed and
  stopping states update the shared actions/status. A repeated start reuses a
  ready instance. Requests within a controller are serialized; a per-user/port
  file lock prevents simultaneous launcher instances racing their initial start.
- An existing healthy instance is reused without ownership, including an
  administrator-managed service. Stop is enabled only for a child started by the
  current launcher. No service installation, firewall rule or external process
  termination is performed by this action.

Discovery respects an explicit BOSS custom path, then managed user/machine
receipts, the isolated portable BOSS component, local output, workspace paths and
PATH. Discovery checks for the published web bundle and, for a DLL invocation,
the matching runtime configuration. Installed state is obtained from the shared
receipt inventory rather than inferred from an executable's presence.

## Readiness and lifecycle

The default listener is `http://127.0.0.1:5555/`. M4 accepts loopback HTTP root
URLs only. Native/DLL launch arguments are passed as tokens with explicit URLs
and content root. Process creation uses no shell and no administrator elevation.

Readiness checks require:

1. `/healthz` returns OK.
2. `/api/info` identifies BOSS and Broiler Office Standalone Server.
3. `/` supplies a published import map.
4. Every imported framework/root JavaScript module is served as JavaScript.

Responses, per-probe time and overall startup time are bounded. Redirects and
HTTP proxies are disabled for loopback probing. A healthy unrelated listener,
missing modules, a missing shell, malformed/oversized responses or a timeout do
not count as a successful BOSS start. The controller does not open a browser for
an unready endpoint. Browser-opening failures leave a healthy BOSS process alive.

For a new child, `/api/info.launcherInstanceId` must match the random instance ID
assigned at launch. This ID is diagnostic, not an authorization token. A private
named pipe with CurrentUserOnly access and an independent random control token
handles graceful shutdown. The token is passed through the child environment,
not through command-line arguments, HTTP endpoints, receipts or log messages.
The server rejects invalid control tokens. After the bounded graceful-stop
deadline, only the controller's own child may be terminated as a fallback.

The BOSS server's launcher integration is opt-in. Ordinary console launches and
administrator services without the launcher environment retain their existing
behavior. A mismatched older server build that lacks lifecycle identity cannot
be mistaken for a newly owned ready process; use the matching Office bundle.

## Logs and exit behavior

Launcher-owned BOSS writes its own log under
`%LOCALAPPDATA%/Broiler/Office/boss/boss-<timestamp>-<instance>.log`. It has no
parent-owned stdout/stderr redirection pipe. Logging for a launcher-owned server
uses the file provider instead of inherited console providers, so disposal of the
launcher process handle does not break subsequent server logging. Each server log
has bounded entries and a 10 MiB write limit. Logs are retained for diagnosis.

By default, BOSS keeps running when the launcher closes. A subsequent launcher
can reuse it but does not acquire its Stop permission. `KeepRunningOnLauncherExit`
can opt into stopping the currently owned server at exit. Startup cancellation,
early process exit, runtime errors, failed readiness and startup timeout clean up
only a child spawned by that operation and retain its diagnostic log path.

## Configuration and CLI

The settings model/persistence now preserve a Boss section. The M6 settings UI
will expose these fields; for now merge this section with existing settings JSON,
retaining Apps and other values:

```json
{
  "Boss": {
    "UseAutoDetect": true,
    "ExecutablePath": null,
    "ListenUrl": "http://127.0.0.1:5555/",
    "StartupTimeoutSeconds": 30,
    "OpenBrowserOnStart": false,
    "KeepRunningOnLauncherExit": true
  }
}
```

Timeouts must be between 1 and 120 seconds. Custom paths have explicit precedence;
an invalid custom path does not silently select another installation. Changing a
listener setting while an owned instance is running requires stopping that
instance before the new setting is applied. Existing settings files without a
Boss section, or with a null section, receive defaults; settings cloning preserves
independent copies.

```powershell
./Broiler.Launcher.Windows.exe --boss-start
./Broiler.Launcher.Windows.exe --boss-status
```

These commands return status/URI/ownership/log information. Starting from CLI
keeps the server alive under the default exit policy. Stop is a control of the
owning live launcher, not a command that can terminate an unrelated CLI/service
instance. Previously detached console/service instances can be stopped through
their own administrator/console deployment controls.

## Validation and demo artifact

- 132 launcher tests passed in each production/demo configuration. Tests cover
  concurrent/repeated starts, cross-controller startup locking, external-instance
  reuse, port conflicts, readiness identity/modules/bounds, refused connections,
  missing binaries/assets, custom DLLs, early exit, timeout cleanup, graceful and
  forced owned shutdown, persistent/stop-on-exit policies, browser failures,
  listener changes, settings migration and shared UI actions/install targeting.
- A real BOSS process passed owned start/reuse, rejection of an invalid pipe token,
  graceful shutdown, log persistence and keep-running behavior after controller
  disposal. A second controller reused it and could not stop it.
- Complete real component ZIPs were installed into isolated user/machine fixture
  roots, discovered through receipts and started/stopped successfully. These
  fixture roots deliberately do not test ProgramData/UAC permissions; real
  machine-install ACL acceptance remains a manual Windows check from M2.
- Full extracted Office archive checks passed: all five desktop processes passed
  hidden startup, and BOSS passed health, identity, web shell, runtime assets and
  routing checks. Hidden startup does not replace a human rendering/interaction
  check.

Updated local demo:
`artifacts/office-demo-m4-1/package-final/assets/Broiler-Office-0.1.0-demo.2-win-x64.zip`.
Extract and open Start-Broiler-Office.cmd, choose Start BOSS from an arrow menu,
then Open BOSS. Full smoke results and lifecycle logs are retained under
`artifacts/office-demo-m4-1/smoke-final/`.

The Office release workflow now runs the real-process lifecycle smoke and retains
its results/logs alongside the archive smoke report. This task did not commit,
push, run GitHub Actions or publish a release. The local bundle is marked as a
working-tree build. M5 update handling and M6 settings UI remain the next roadmap
milestones; signing still does not block the demo.
