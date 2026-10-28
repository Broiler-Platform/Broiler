# Broiler Office demo for Windows x64

This is an **unsigned demonstration prerelease**, prepared for evaluation.
Extract the entire archive and open `Start-Broiler-Office.cmd` or
`launcher/Broiler.Launcher.Windows.exe`. The launcher identifies itself as a demo.

Browser, Writer, Cell and Plate have independent payloads under `products/`.
Use Launch to run a portable product, or the arrow menu to install that product
for your user account or for all users (Windows administrator approval).
Keep `packages/` beside the manifest: demo installation uses these local archives.
Checksums detect damaged bytes; they do not authenticate the publisher.
Use a bundle obtained directly from the demonstrator. There are no automatic
downloads of unsigned demo packages. Production signature verification is a
separate final-release gate.

Installations use LocalAppData/Broiler/Office for user scope and
ProgramData/Broiler/Office for machine scope. Cancellation is available before
activation, and before handing a machine operation to Windows UAC. User documents
and launcher settings are retained. Already-installed versions appear in the menu.

BOSS is included under `products/boss/`, with its matching Writer web assets.
Use Start BOSS from any product's arrow menu or Tools → BOSS, then Open BOSS to
open the verified web client in your browser. BOSS hosts Writer only. The shared
action works independently of the selected desktop product. Tools → BOSS also
offers status, logs, scoped installation, and Stop for an instance owned by this
launcher. Existing services are reused and cannot be stopped by this launcher.
By default BOSS stays running after the launcher closes. Its logs are in
LocalAppData/Broiler/Office/boss. Settings has Products, Installation, BOSS and
Updates sections. Products offers explicit Auto-detect/Custom modes, arguments,
preferred installed scope and action visibility. Installation shows fixed roots
and the preferred install-menu order; every operation still names its scope.
The BOSS section configures its custom path, loopback listener URL/port, startup
timeout, logging arguments, browser opening and lifetime. Updates configures the
channel, startup checks and interval, and provides Check now with check history.
Save applies all sections. Back, Cancel and Escape discard unsaved preferences.
The page scrolls and supports keyboard navigation. No update is installed by
changing settings. Stop an owned BOSS before applying a new path/listener.
Alternatively, run `products/boss/Start-BOSS.cmd` and open
http://localhost:5555/.
Service deployment is optional; see `products/boss/service/README.md`. Its installer
copies the isolated BOSS component, rather than the other desktop products.

Recovery commands from the launcher directory:

```powershell
./Broiler.Launcher.Windows.exe --office-repair writer --package-directory C:/path/to/extracted/bundle
./Broiler.Launcher.Windows.exe --office-rollback writer
./Broiler.Launcher.Windows.exe --office-unregister writer
```

Unregister removes registration and retains payloads/data for recovery. Linux,
ARM64 and production signing are outside this demo's scope.
Build identity is in `BUILD-INFO.json`; runtime package dependencies are in
`SBOM.cdx.json`. Component licenses are in `licenses/`.

To update an installed product, choose Tools → Use local update bundle and select
`office-release.json` in a newer full extracted Office bundle. Eligible cards show
Update user/all; the arrow menu offers each installed scope explicitly. Close the
product before updating. BOSS updates appear under Tools → BOSS and restart an
instance owned by this launcher, with rollback if readiness fails. External BOSS
services require their administrator's intervention.

Tools → Check for Office updates also checks published GitHub Office prereleases.
Drafts are excluded. Remote demo updates open the release page: download/extract
the full bundle, then select it locally. Unsigned payloads are never downloaded
and installed automatically. Tools → Download newer launcher supports manual
launcher replacement after closing the current copy. Cancel current update is
available until activation or administrator handoff.
