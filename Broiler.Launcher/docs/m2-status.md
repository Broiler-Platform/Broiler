# M2: Windows product installation

M3 update: the user deferred production signing to the last release gate. The
explicit unsigned demo build and its complete local packages now support M2
demonstrations without keys; see [M3 delivery](m3-status.md). Normal production
builds retain the signature requirement described below.

M2 implements the Windows x64 installation engine and its launcher integration.
Linux installation/runtime acceptance remains deferred. This is an implementation
record, not a claim that production Office packages or signing keys already exist.

## Delivered behavior

- Managed inventory enumerates user and machine scopes independently. Receipts
  distinguish Installed, Not installed, and Invalid states. Missing entry points,
  mismatched records, unsafe paths and unprotected machine files are not Ready.
- Discovery preserves explicit custom overrides, then selects a managed user
  installation, managed machine installation, and finally existing local,
  workspace and PATH candidates. Scope and version accompany managed resolution.
  Windows receipts cannot become Linux candidates.
- Install for user and Install for all become enabled when a compatible signed
  release manifest is available and the selected scope is not already installed.
  Existing installs are labeled with their scope/version. Invalid installs require
  an explicit repair command. BOSS startup remains a later milestone.
- Installation snapshots the complete component ZIP, checks signed size/SHA-256,
  extracts within limits, and activates a complete version through an atomic
  active-record replacement. It never overwrites an active product directory.
- Each product/scope has a file lock. Cancellation before activation cleans the
  operation's stage and preserves the existing active version. The short commit
  boundary is non-cancellable. A crash before active-record replacement can leave
  an unreferenced complete directory, which inventory ignores.
- The menu offers Cancel installation, and card labels report phase/percentage.
  The selected product's launch/install controls and keyboard launch are blocked
  during its operation; other products remain available. Completions/progress are
  posted through the session dispatcher, followed by inventory refresh.
- Complete bundled archives are preferred. If an archive is absent, the launcher
  downloads that exact signed release asset from the Broiler GitHub repository,
  with a bounded size, timeout, integrity checks and cancellation. It does not
  query for newer releases; release selection/update checks belong to M5.

## Installation and activation layout

```text
%LOCALAPPDATA%/Broiler/Office/             # user scope
%ProgramData%/Broiler/Office/              # machine scope
  products/<id>/versions/<semver>/<install-id>/
    <complete component publish closure>
    .office-receipt.json
  inventory/<id>.json                     # Current + Previous receipts
  locks/<id>.lock
  staging/<operation-id>/                 # removed after this operation
  cache/downloads/<sha256>.zip            # user scope download cache only
```

The additional random installation ID allows same-version repair without touching
running files. Versioned payloads and the previous receipt are retained for
rollback. Settings and documents stay outside version directories. A failed
activation write leaves the old record intact; any unreferenced version is not
discovered as installed. Unregister removes only the active registration and
deliberately retains files and user data. Automatic disk reclamation is separate
from this recovery-safe removal operation.

## Signing and M3 packaging handoff

The user confirmed that no release key exists yet and requested signing support
with an empty trust store. Accordingly,
`src/Broiler.Launcher.Core/ReleaseTrust/office-release-keys.json` is `{}`. Both
production installation paths remain unavailable until a key and signed manifest
are supplied. There is no unsigned-package or environment-variable bypass.

Prepare a release RSA key of at least 3072 bits outside the repository. Keep its
private half in release signing infrastructure. Add only its SubjectPublicKeyInfo
public PEM to the embedded JSON, keyed by a stable key ID:

```json
{ "office-2026": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----" }
```

Rebuild the launcher after changing trusted keys. Rotation supports multiple
embedded key IDs; remove a retired key in a subsequent launcher release. Packages
cannot supply their own verification key. Algorithm: RSA-PSS with SHA-256, over
the exact UTF-8 manifest bytes. Do not reformat the manifest after signing.

Expected bundle layout:

```text
Broiler-Office-<version>-win-x64/
  office-release.json
  office-release.signature.json
  packages/<component-asset-name>.zip
  launcher/<launcher publish output>
```

The launcher searches its own directory, or its parent when deployed in a folder
named `launcher`. `--package-directory` accepts an explicit absolute source for
administrator/user CLI use. Input metadata is bounded to 1 MiB for the manifest
and 16 KiB for the signature. Schema version 1 example:

```json
{
  "schemaVersion": 1,
  "officeVersion": "1.0.0",
  "channel": "stable",
  "tag": "office-v1.0.0",
  "commit": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "minimumLauncherVersion": "0.1.0-preview.1",
  "components": [
    {
      "id": "writer",
      "version": "1.0.0",
      "rid": "win-x64",
      "asset": "Broiler-writer-1.0.0-win-x64.zip",
      "size": 123456,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "archive": "zip",
      "entryPoint": "Broiler.Writer.Windows.exe",
      "invocation": "native",
      "publishMode": "self-contained"
    }
  ]
}
```

The example size/hash/commit are placeholders. The release reader also accepts a
Windows launcher component in the manifest, but M2 does not install/replace the
launcher itself. Supported install product IDs are browser, writer, cell, plate
and boss. Native executable and dotnet DLL entry-point names are fixed by catalog.
Linux/mixed-RID manifests are not yet accepted. M3 must emit a Windows manifest
for this Windows-first release.

Sign with PowerShell 7 and an existing private key:

```powershell
./Broiler.Launcher/eng/sign-office-manifest.ps1 `
    -Manifest C:/release/office-release.json `
    -PrivateKeyPath C:/secure/office-release-private.pem `
    -KeyId office-2026
```

This creates `office-release.signature.json` with schemaVersion, keyId and base64
signature. It does not create/publish a key or release, and it rejects output
paths that would overwrite the manifest or private key.

## Machine installation boundary

The Windows head relaunches its own apphost through UAC with a narrow
`--office-machine` request. The helper accepts a known product and source/archive
paths, never a destination override or command to execute. It independently
verifies the signed manifest. Untrusted source bytes are copied into a private
administrator/SYSTEM-only stage and hashed there. Read/execute access for users
is granted only to the validated extracted payload before activation. This keeps
unverified source snapshots private as well as preventing write tampering.

Machine roots are fixed to ProgramData/Broiler/Office. Existing roots/ancestors
must be administrator/SYSTEM-owned and must not grant ordinary users write,
delete, ownership or permission-change rights. New roots have protected ACLs.
The helper rejects links/reparse points and unsupported elevated token ownership
policy. Inventory also verifies ownership/permissions of machine records,
version receipts and entry points, so an ordinary user cannot forge a machine
installation under a writable ProgramData subtree.

Normal-user downloading remains cancellable before UAC. Once the administrator
handoff begins, cancellation is disabled until the helper finishes or approval
is denied; the launcher does not abandon or kill an elevated transaction. UAC
denial leaves installation unchanged. Helper failures report an exit code and
direct the user to check the signed package and machine-root permissions.

## Recovery commands

From the Windows launcher directory (all user-scope operations):

```powershell
./Broiler.Launcher.Windows.exe --office-install writer --package-directory C:/release
./Broiler.Launcher.Windows.exe --office-repair writer --package-directory C:/release
./Broiler.Launcher.Windows.exe --office-rollback writer
./Broiler.Launcher.Windows.exe --office-unregister writer
```

Repair stages a fresh complete version and refuses a downgrade when the current
receipt is valid. Rollback validates the previous version before activation.
Unregister is idempotent and preserves payloads/settings/documents. No command
terminates a running product or silently changes installation scope.

From an already elevated terminal, machine recovery uses the same narrow helper:

```powershell
./Broiler.Launcher.Windows.exe --office-machine repair writer C:/release
./Broiler.Launcher.Windows.exe --office-machine rollback writer
./Broiler.Launcher.Windows.exe --office-machine unregister writer
```

## Validation and remaining acceptance

Automated tests use temporary directories, in-memory fixture signing keys and
mock HTTP responses; they do not install real products or elevate the test host.
Coverage includes complete payload/receipt installation, both scopes and
precedence, custom overrides, stale/spoofed receipts, downgrade rejection,
archive path/link/collision rejection, signature/hash validation, cancellation,
concurrent locks, failed activation, rollback, unregister, download verification,
broker denial, and UI conflict/cancellation/refresh behavior.

Completed validation: 96 tests passed in Release-Windows; the Windows solution
built with zero warnings/errors. The PowerShell signing tool passed an actual
sign/verify round-trip with a disposable fixture key, which was removed after the
check. Existing discovery/settings regression tests use an empty managed
inventory so local installed products cannot affect their outcomes. Whitespace
checks passed. No Broiler.UI or Broiler.Graphics changes/publication are required
for M2.

Before production acceptance, the final release gate must supply the embedded
public key and signed product packages. On a Windows test machine, demonstrate an actual
user install, UAC grant/deny, machine read/execute versus write ACLs under a second
standard user, real published-product launch, and graphical progress/cancel/error
handling. Those real privilege/UI checks cannot be replaced by the fixture tests.

API references: [.NET RSA verification](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rsa.verifydata)
and [directory creation with security](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemaclextensions.createdirectory).
