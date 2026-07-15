# Broiler Application Installation and Update Roadmap

> **Status:** Proposed architecture and delivery plan
> **Scope:** Broiler Browser and Broiler Writer desktop applications
> **Current platforms:** Windows x64 and Linux x64
> **Future platform:** macOS, after a native application, graphics backend, and input backend exist
> **Owner:** TBD
> **Last updated:** 2026-07-15

---

## Contents

- [Outcome](#1-outcome)
- [Definition of done](#2-definition-of-done)
- [Verified repository baseline](#3-verified-repository-baseline)
- [Architectural decisions](#4-architectural-decisions)
- [Version, channel, and artifact contract](#5-version-channel-and-artifact-contract)
- [Native package and update matrix](#6-native-package-and-update-matrix)
- [Windows plan](#7-windows-plan)
- [Linux plan](#8-linux-plan)
- [macOS plan](#9-macos-plan-gated-on-the-application-port)
- [Generic portable-to-profile installation](#10-generic-portable-to-profile-installation)
- [Update discovery and manifest design](#11-update-discovery-and-manifest-design)
- [Self-update transaction](#12-self-update-transaction)
- [Native update delegation and fallback](#13-native-update-delegation-and-fallback)
- [Security and trust model](#14-security-and-trust-model)
- [Release pipeline](#15-release-pipeline)
- [Test and acceptance matrix](#16-test-and-acceptance-matrix)
- [Phased implementation roadmap](#17-phased-implementation-roadmap)
- [Sequencing](#18-sequencing)
- [Risks and mitigations](#19-risks-and-mitigations)
- [Open maintainer decisions](#20-open-maintainer-decisions)
- [Non-goals](#21-non-goals-for-the-first-implementation)
- [Primary references](#22-primary-references)

---

## 1. Outcome

Broiler should support two complementary distribution paths:

1. An operating-system-native package for each supported desktop platform.
   The native package manager is the normal installation and update owner.
2. A generic, self-managed distribution for direct downloads. When an official
   portable build is launched, it offers to install a copy for the current user
   and then keeps that copy updated without administrator rights.

The same signed Broiler release metadata drives both paths. The generic updater
is also the discovery and recovery layer for native packages, but it must never
silently overwrite files owned by Windows Installer, APT, DNF, Flatpak, Snap,
an app store, or another package manager.

The simplest safe feed is a small, workflow-generated one-file signed channel
envelope hosted on GitHub. The GitHub Releases API is a discovery/check-only
fallback unless it can establish the latest signed channel-control sequence.
Do not hand-edit the production version file or scrape the releases HTML. In
particular, GitHub's latest-release endpoint excludes prereleases, so it cannot
discover Broiler's current Preview channel by itself.

The intended first delivery sequence is:

1. deterministic and signed Windows/Linux portable artifacts;
2. generic per-user installation and atomic full-package updates;
3. signed Windows MSI plus WinGet;
4. Debian/Ubuntu DEB plus an APT repository;
5. Fedora/RHEL RPM plus a DNF repository;
6. macOS DMG/Sparkle and optional PKG after a macOS application exists;
7. MSIX, Microsoft Store, Flatpak, Snap, AppImage, Homebrew, and other
   secondary channels as demand justifies them.

This document is a roadmap, not a claim that these installers already exist.

---

## 2. Definition of done

The installation system is complete for a platform when all of the following
are true:

- A new user can install Browser or Writer without understanding .NET publish
  output or manually arranging files.
- A native package has a documented native update path.
- A direct-download build can offer a non-elevated per-user installation.
- Every installation records which component owns its updates.
- An N-1 installation can update to N without losing settings or documents.
- A failed, interrupted, corrupt, or untrusted update leaves the previous
  version runnable.
- Running applications, insufficient disk space, offline operation, proxies,
  read-only media, and concurrent update attempts fail safely.
- Release assets, signed-envelope metadata, and native packages are verified.
- Uninstall removes application binaries and integration but preserves user
  documents and, by default, user settings.
- Browser and Writer can be installed, updated, and removed independently.
- The exact release commit has passed the existing human-review and provenance
  gates before production signing, and the verified artifacts receive a
  separate final approval before any public channel envelope is advanced.

---

## 3. Verified repository baseline

### 3.1 Applications and target platforms

| Product | Project | Current target | Current RID |
|---|---|---|---|
| Browser | <code>src/Broiler.Browser.Windows</code> | .NET 10, Win32/Direct2D | <code>win-x64</code> |
| Browser | <code>src/Broiler.Browser.Linux</code> | .NET 10, X11/OpenGL | <code>linux-x64</code> |
| Writer | <code>src/Broiler.Writer.Windows</code> | .NET 10, Win32/Direct2D | <code>win-x64</code> |
| Writer | <code>src/Broiler.Writer.Linux</code> | .NET 10, X11/OpenGL | <code>linux-x64</code> |
| Writer WebAssembly | <code>src/Broiler.Writer.WebAssembly</code> | Browser/server deployment | Not a desktop package |

There is no macOS application project, graphics backend, input backend, RID,
workflow, bundle metadata, or signing configuration. macOS packaging is a
planned lane whose implementation is blocked on the application port.

The Linux requirements baseline calls for <code>linux-arm64</code> as a
compile/publish target, but the current app projects and workflows only use
<code>linux-x64</code>. ARM64 is therefore a backlog item, not a currently
available artifact.

### 3.2 Existing release workflows

| Workflow | Current output | Important gap |
|---|---|---|
| <code>.github/workflows/draft-release.yml</code> | Windows Browser single-file ZIPs | Browser only; draft timestamp tags |
| <code>.github/workflows/writer-draft-release.yml</code> | Windows/Linux Writer ZIP or tar.gz | Separate product workflow; draft timestamp tags |
| <code>.github/workflows/broiler-preview-package.yml</code> | Combined Browser/Writer folders for Windows/Linux | Versionless names; both apps publish into one directory |
| <code>.github/workflows/nuget-packages.yml</code> | Library NuGet packages | Applications intentionally excluded |

The workflows are useful prototypes, but they do not yet provide:

- one canonical application version;
- published SemVer release tags;
- stable product IDs or artifact names;
- an install receipt, immutable artifact-kind marker, or update-owner record;
- checksums, signed update envelopes/payload signatures, Authenticode,
  repository signatures,
  notarization, SBOMs, or artifact provenance;
- native installers or package repositories;
- a release/update envelope;
- an updater, launcher, rollback slot, or update tests.

The existing library version <code>0.1.0-preview.1</code> in
<code>eng/Broiler.Packaging.props</code> is not currently stamped into the
desktop application release contract. Existing application release tags
contain a branch, timestamp, and workflow run number, so clients cannot compare
them as product versions.

### 3.3 Constraints that affect the design

- Browser currently resolves assemblies from <code>AppContext.BaseDirectory</code>.
  A portable self-install must copy and verify the complete release payload,
  not just the executable that happened to be launched.
- The combined preview workflow and the single-file per-app workflows disagree
  about product granularity. Update selection cannot safely infer a product
  from those current file names.
- Windows Browser is declared <code>asInvoker</code>. The generic installer
  should remain per-user and non-elevated; native installers own elevation.
- Linux self-contained output still needs host libraries for .NET, EGL/OpenGL,
  X11/XCB, Vulkan, ICU, OpenSSL, and related system services. DEB/RPM packages
  can declare dependencies; portable builds need startup diagnostics.
- Linux CI uses noninteractive/offscreen launches. Installation and update UI
  must be suppressed for help, offscreen, CI, and explicitly noninteractive
  modes.
- Browser favorites are already kept outside the binary under the user's
  application-data directory. Writer documents are user-selected content.
  Neither belongs in an application update or rollback.
- Windows Browser already shows a preview safety dialog. First-run installation
  must be integrated into one onboarding sequence rather than stacking
  unrelated dialogs.
- WebAssembly deployment is a server/static-site concern. A desktop
  self-updater must not run in the WebAssembly build.
- Release packaging remains subject to the root license/provenance audit and
  exact-commit <code>HUMAN_REVIEW.md</code> approvals.

---

## 4. Architectural decisions

### 4.1 One product ID per application

Use stable product identities:

| Purpose | Browser | Writer |
|---|---|---|
| Internal product ID | <code>broiler-browser</code> | <code>broiler-writer</code> |
| Reverse-DNS ID | <code>org.broiler.Browser</code> | <code>org.broiler.Writer</code> |
| Windows package ID | <code>BroilerPlatform.BroilerBrowser</code> | <code>BroilerPlatform.BroilerWriter</code> |
| Linux package name | <code>broiler-browser</code> | <code>broiler-writer</code> |

Browser and Writer may share release automation and initially move in lockstep,
but their native packages, install roots, receipts, shortcuts, update state,
and uninstall entries remain independent. A future <code>broiler-suite</code>
package may depend on both; it should not own shared mutable application files.

### 4.2 One update owner per installation

Separate two concepts that cannot safely be represented by one flag:

- every official artifact embeds an immutable <code>ArtifactKind</code>;
- every installed copy has an <code>UpdateOwner</code> in its protected install
  receipt.

The artifact kinds are:

- <code>portable-archive</code>
- <code>msi</code>
- <code>msix</code>
- <code>microsoft-store</code>
- <code>deb</code>
- <code>rpm</code>
- <code>arch-package</code>
- <code>flatpak</code>
- <code>snap</code>
- <code>appimage</code>
- <code>mac-direct</code>
- <code>mac-pkg</code>
- <code>mac-app-store</code>
- <code>developer</code>

Copying a <code>portable-archive</code> payload into the profile does not mutate
its embedded kind. The verified installation transaction creates a receipt
whose owner is <code>broiler-self-managed</code>; a loose portable copy remains
<code>manual/check-only</code>. Do not infer either field from an executable
path. If an installed copy's receipt is missing, corrupt, or inconsistent with
the embedded artifact kind, fail safe to repair/check-only mode and do not
apply an update until ownership is restored.

The complete ownership table is:

| Artifact/install state | Update owner | Broiler behavior |
|---|---|---|
| Loose portable archive | Manual/check-only | Offer profile installation and release information; never overwrite the loose download |
| Verified profile installation | Broiler self-managed | Stage full payloads in version directories and atomically activate |
| MSI | Windows Installer/WinGet or enterprise manager | Discover, then hand off the same signed MSI |
| MSIX/App Installer or Microsoft Store | App Installer/Store | Route only; never replace package files |
| DEB | APT/dpkg transaction under system policy | Discover, then hand off the same DEB through APT or the graphical installer |
| Fedora/RHEL RPM | DNF/RPM transaction under system policy | Discover, then hand off the same signed RPM through DNF/PackageKit |
| openSUSE RPM | Zypper/RPM transaction under system policy | Use the separately built openSUSE package and Zypper/YaST |
| Arch package/AUR | Pacman/AUR tooling | Notify or hand off; never encourage a partial system upgrade |
| Flatpak | Flatpak repository/software center | Route only |
| Snap | Snapd/Store | Route only |
| AppImage | Broiler AppImage adapter | Verify the signed full AppImage, replace it atomically after exit, and retain one previous file; do not also embed AppImageUpdate/zsync ownership |
| Loose direct macOS app on a DMG/download/translocated path | Manual/check-only | Offer verified relocation; never let Sparkle replace a mounted/read-only/translocated source |
| Verified relocated direct macOS app | Sparkle | After a receipt records the writable supported app location, Sparkle replaces the complete signed app bundle |
| Mac PKG | Installer/MDM | Hand off the verified PKG with authorization |
| Mac App Store | Mac App Store | Route only; Store build contains no Sparkle updater |
| Homebrew Cask for the direct app | Sparkle, declared with <code>auto_updates</code> in the cask | Homebrew installs/removes; Sparkle alone updates the app payload |
| Nix/Nixpkgs/flake | Nix profile/system generation | Notify or hand off; immutable store remains Nix-owned |
| Developer build | None | No install prompt or automatic update |

Machine and enterprise policy always wins. A broken native channel may offer an
explicit migration to a separate self-managed per-user installation only when
policy permits it. Migration is never silent.

### 4.3 Native first, generic always available

The website should recommend the native package when the running OS and
distribution are known. The portable archive remains available for:

- evaluation without administrative installation;
- unsupported Linux distributions that meet runtime dependencies;
- machines where package repositories are blocked;
- recovery when a native update channel is temporarily unavailable;
- contributors and testers who deliberately select a preview channel.

### 4.4 Full packages before deltas

Version 1 of the updater downloads a complete compressed payload. Delta updates
save bandwidth but multiply failure, signing, base-version, and rollback cases.
Add deltas only after full-package updates have production evidence, and always
retain the full artifact as a fallback.

### 4.5 No always-running update service in version 1

Check after the application becomes usable, at most once per 24 hours with
jitter, and when the user selects Check for updates. Download in the
background only with user consent/settings. Apply on restart or explicit
approval. A resident service, scheduled task, or system daemon is a later,
separately justified feature.

---

## 5. Version, channel, and artifact contract

### 5.1 Canonical version

Application releases use Semantic Versioning. During the preview, Browser and
Writer use a lockstep application version even though they remain separate
products.

Recommended tags:

- <code>app-v0.1.0-preview.2</code> for a published preview;
- <code>app-v1.0.0</code> for a stable release.

Draft build tags and branch/timestamp releases are not update candidates.
Git commit SHA and workflow run ID remain provenance fields, not versions.

### 5.2 Channels

| Channel | GitHub release state | Client behavior |
|---|---|---|
| Stable | Published, not prerelease | Receives stable versions only |
| Preview | Published prerelease | Receives newer previews and the corresponding stable release |
| Nightly | Published prerelease or separate retention feed | Explicit opt-in; short retention; no stability promise |

Changing channels is explicit. Stable never moves to Preview or Nightly
automatically. Downgrades require a separate advanced action and are blocked
when a security floor applies.

GitHub's latest-release endpoint is not sufficient for the current preview:
it returns a published, non-prerelease, non-draft release and uses release
creation time, not a SemVer comparison. The update client therefore parses and
compares the version in signed Broiler metadata.

### 5.3 Native version projections

The release workflow derives native versions from the canonical SemVer and
tests ordering before publication:

| System | Example projection for <code>0.1.0-preview.2</code> |
|---|---|
| Display/SemVer | <code>0.1.0-preview.2</code> |
| MSI ProductVersion | <code>0.1.2</code>, using a release-allocated numeric build that increases for every preview and stable release in the 0.1 line |
| MSIX | Deterministic four-field numeric package version using the same monotonic release allocation |
| Debian | <code>0.1.0~preview.2-1</code> |
| RPM EVR | <code>0:0.1.0-0.preview.2.1</code> |
| macOS short version | <code>0.1.0</code> plus preview display text |
| macOS bundle version | Monotonically increasing integer |

Windows Installer only compares the first three version fields. The release
workflow, not an ad hoc developer choice, must allocate a monotonic native
build for every public update. For example, if Preview 2 maps to
<code>0.1.2</code>, the later stable release in that same major/minor line must
map to at least <code>0.1.3</code>, even though its display SemVer is
<code>0.1.0</code>. The signed manifest is the mapping between display SemVer
and the native installer version.

### 5.4 Runtime and architecture matrix

Initial:

- <code>win-x64</code>
- <code>linux-x64</code>

Next:

- <code>linux-arm64</code>, as already required by the Linux baseline

Future macOS:

- <code>osx-arm64</code>
- <code>osx-x64</code>, or one tested universal app bundle

Architecture selection must use OS/runtime APIs, never the browser user-agent
or file naming alone. An updater selects exactly one matching product, channel,
RID, architecture, artifact kind, and installed update owner.

### 5.5 Public asset names

Use lowercase, versioned, immutable names:

    broiler-browser-0.1.0-preview.2-win-x64-portable.zip
    broiler-writer-0.1.0-preview.2-linux-x64-portable.tar.xz
    broiler-browser-0.1.0-preview.2-win-x64.msi
    broiler-browser_0.1.0~preview.2-1_amd64.deb
    broiler-browser-0.1.0-0.preview.2.1.x86_64.rpm
    broiler-browser-0.1.0-preview.2-osx-universal.dmg
    broiler-update-envelope-v1.json
    SHA256SUMS
    SHA256SUMS.sig

Never replace an asset after publication. Publish a new version for every
change, including packaging-only fixes.

### 5.6 Portable payload layout

Public portable output should be self-contained. Framework-dependent artifacts
may remain CI/developer downloads, but they should not be selected by the
automatic updater.

Each portable artifact contains:

    Broiler Browser/
      Broiler.Bootstrap[.exe]
      distribution.json
      payload-manifest.json
      payload-manifest.json.sig
      payload/
        Broiler.Browser.Windows.exe
        supporting assemblies and native libraries
      LICENSE
      THIRD_PARTY_NOTICES.md
      README.txt

The bootstrap is the stable entry point. It launches the active version,
coordinates first-run installation, and starts the post-exit updater. The
signed payload manifest lists every file, size, mode where relevant, and
SHA-256; it also covers <code>distribution.json</code>.

Single-file app output may still be offered, but it must not be the only
updatable layout. Broiler's current assembly-resolution behavior makes a
verified directory bundle the lower-risk initial contract.

---

## 6. Native package and update matrix

| Platform/family | First-class native format | Normal update mechanism | Broiler fallback |
|---|---|---|---|
| Windows | Signed MSI; listed in WinGet | MSI major upgrade, WinGet, enterprise deployment | Download and launch the signed replacement MSI |
| Debian/Ubuntu | DEB plus signed APT repository | APT, software center, unattended-upgrades | Hand the verified DEB to APT/native UI |
| Fedora/RHEL | Signed RPM plus signed DNF repository | DNF, PackageKit, dnf-automatic | Hand the verified RPM to DNF/native UI |
| openSUSE | Separate signed RPM plus Zypper repository | Zypper or YaST | Hand off to Zypper/YaST |
| macOS direct, future | Signed/notarized app in DMG | Sparkle appcast | Sparkle or open the verified replacement DMG |
| macOS managed, future | Signed/notarized PKG | Re-run PKG, MDM/Jamf/Munki | Open the verified PKG and request authorization |

Secondary formats and stores are fully documented below, but are not on the
first critical path.

---

## 7. Windows plan

### 7.1 Recommended path: MSI plus WinGet

Create separate WiX-based MSI packages for Browser and Writer.

Requirements:

- per-user is the default consumer scope;
- optional per-machine installation is a deliberate installer choice;
- a stable UpgradeCode exists per product and scope;
- each major upgrade receives a new ProductCode;
- upgrades remain in the original per-user/per-machine context;
- full MSI major upgrades are the default servicing model;
- downgrade detection and a clear newer-version message are built in;
- Apps and Features entries, repair, uninstall, Start menu shortcuts, icons,
  protocols, and optional file associations are tested;
- user settings and documents are outside the MSI component tree;
- MSI and every executable payload are Authenticode-signed and timestamped.

Windows Installer can also service an install through minor upgrades or MSP
patches. These are valid possibilities, but they add component, baseline,
sequencing, supersedence, and repair-source complexity. Keep them deferred until
measured package size makes them worthwhile. Microsoft recommends installing
the full product package for a major upgrade.

Publish a WinGet manifest for each released MSI. WinGet supports an explicit
package upgrade and an all-packages upgrade, but it is not Broiler's background
agent. The package may show the exact WinGet action or open a terminal with
user consent; it must not run an unexpected elevated command at startup.
Map canonical SemVer, MSI ProductVersion, and Apps and Features DisplayVersion
explicitly in the WinGet manifest, then test WinGet detection on N-1, Preview to
Stable, and Stable to next-patch transitions so the numeric MSI projection
cannot cause a perpetual upgrade or downgrade loop.

### 7.2 MSI fallback behavior

If the signed Broiler feed reports a newer MSI:

1. Ask WinGet/Windows Installer whether the installed context and product are
   recognizable.
2. Prefer WinGet or the configured enterprise mechanism.
3. If that channel is unavailable or lagging, download the exact MSI asset from
   the signed manifest.
4. Verify manifest signature, length, SHA-256, Authenticode chain, timestamp,
   product identity, upgrade code, scope compatibility, and version.
5. Ask the user to close all Broiler instances.
6. After elevation, re-evaluate machine policy, securely reopen the download,
   copy it into a new administrator-owned private staging location, flush it,
   and reverify identity, signature, and hash there. A standard user must not be
   able to write, rename, or delete the exact path Windows Installer consumes.
7. Launch that privileged immutable MSI copy in the same installation context
   and let Windows Installer
   perform the transaction.
8. Confirm the installed version after restart.

The fallback never unpacks files into the MSI directory itself.

### 7.3 Other Windows possibilities

| Mechanism | Update behavior | Roadmap position |
|---|---|---|
| MSIX/MSIXBundle plus <code>.appinstaller</code> | On-launch/background checks, prompts, activation blocking, alternate update URIs | Strong second channel after compatibility testing |
| Microsoft Store MSIX | Store owns automatic/manual updates | Later consumer channel |
| Microsoft Store listing for MSI/EXE | Store listing does not take ownership of existing MSI/EXE updates | Discovery only unless Store package is MSIX |
| ClickOnce | Hosted manifest and automatic updates for supported .NET desktop app types | Not preferred for Broiler's shared cross-platform architecture |
| WiX Burn/EXE bootstrapper | Can install prerequisites and chain MSI packages | Optional suite/prerequisite bootstrapper |
| Chocolatey | Repository package; user/admin runs Chocolatey upgrade | Community/secondary |
| Scoop | User-scoped manifest and update | Community/secondary |
| Intune/Configuration Manager | Enterprise policy and deployment | Supported handoff, not Broiler-owned |
| Portable ZIP | Loose copy is check-only; profile installation uses the Broiler self-managed updater | Required generic fallback |

For MSIX, installed files are package-owned and read-only. Broiler must route to
App Installer or Store and must not self-patch the package directory. A direct
download should link to a downloadable <code>.appinstaller</code> file; the
browser-triggered <code>ms-appinstaller:</code> protocol is disabled by default
on current consumer systems for security reasons.

---

## 8. Linux plan

Linux is divided by distribution family because one package cannot correctly
express every dependency name and policy.

### 8.1 Debian and Ubuntu: DEB plus APT

Produce separate <code>broiler-browser</code> and
<code>broiler-writer</code> DEBs, initially for amd64 and later arm64.

The packages:

- install application binaries under <code>/usr/lib/broiler/&lt;product&gt;</code>;
- install launchers under <code>/usr/bin</code>;
- install desktop files, icons, MIME metadata, and AppStream metadata under the
  standard shared-data paths;
- declare the tested native library dependencies for each supported baseline;
- do not grant broad evdev access or install a setuid helper;
- write user settings only through XDG per-user paths;
- contain immutable native artifact-kind metadata and an install receipt naming
  the native update owner.

Publish them in a signed APT repository:

- generate Packages and Release metadata;
- publish a signed InRelease file;
- scope the repository key with Signed-By;
- ship a small, separately consented repository/keyring bootstrap package or
  provide explicit setup instructions;
- plan key rotation through the keyring package;
- never silently add a system repository from the running application.

APT then supplies manual updates, graphical software-center updates, and
optional unattended-upgrades. A standalone downloaded DEB is only a one-time
installer until the repository is configured.

The fallback may download a verified DEB and offer to open the native software
installer or use an explicit APT transaction. It never calls
<code>dpkg</code> alone when dependency resolution is required, and never
copies into <code>/usr</code>.

### 8.2 Fedora and RHEL family: RPM plus DNF

Produce signed RPMs and a DNF/YUM repository with signed metadata.

Requirements:

- separate Browser and Writer package names;
- dependencies expressed using the supported Fedora/RHEL package vocabulary;
- package and repository signatures verified;
- desktop/AppStream integration;
- clean DNF install, upgrade, downgrade-block, repair/reinstall, and remove;
- support for manual <code>dnf upgrade</code>, PackageKit/software centers, and
  optional <code>dnf-automatic</code>.

The fallback hands the verified RPM to DNF or PackageKit. Do not invoke raw RPM
for a normal dependency-bearing upgrade.

openSUSE also uses RPM but needs a separately tested build/repository because
dependency names and packaging policy differ. Its native owner is Zypper/YaST.

### 8.3 Other Linux possibilities

| Format/channel | Update behavior | Policy |
|---|---|---|
| Arch <code>.pkg.tar.zst</code> or AUR | Pacman/system upgrade or AUR helper | Later; never self-write package files or encourage partial upgrades |
| Flatpak/Flathub | Repository updates; software center may update automatically; OSTree deltas | Best later universal/sandboxed desktop channel |
| Snap Store | Snapd automatic refresh and channels/tracks | Later; installed content is read-only and Snap owns updates |
| AppImage | Broiler verifies and atomically replaces the complete AppImage after exit; one previous file is retained | Good portable companion after AppImage integration is tested; do not also enable AppImageUpdate/zsync ownership |
| Nix/Nixpkgs/flake | Nix profile or system generation update | Community/secondary; immutable-store ownership |
| Generic tar.xz | Broiler per-user self-install and updater | Required initial generic Linux path |

Flatpak and Snap require a portal/confinement review for files, networking,
graphics, input, and Browser behavior. Their own updater is authoritative.
AppImage can technically embed zsync update information, but the official
Broiler build deliberately uses the Broiler adapter as its only update owner.
If a later community AppImage enables AppImageUpdate, give it a distinct
artifact identity and make Broiler check-only in that build.

### 8.4 Linux portable dependency handling

A self-contained .NET archive does not make all system dependencies portable.
Before prompting for installation, the Linux bootstrap should probe and report:

- glibc and required .NET host libraries;
- ICU, OpenSSL, Kerberos/GSSAPI, CA certificates, and time-zone data;
- EGL/OpenGL and X11/XCB for the current window path;
- Vulkan loader/driver only when that backend is requested;
- display-session state and unsupported Wayland-only situations;
- input permissions without logging keys, raw device paths, or unique hardware
  identifiers.

Native packages declare dependencies; generic archives provide actionable
diagnostics and a link to the existing Linux hardening notes.

---

## 9. macOS plan, gated on the application port

### 9.1 Required platform work first

Do not publish an empty packaging promise. Before installer work:

- add a macOS graphics/window backend and input backend;
- add Browser and Writer macOS app projects;
- define <code>osx-arm64</code> and either <code>osx-x64</code> or a universal
  bundle;
- produce valid app bundles with reverse-DNS IDs and incrementing
  CFBundleVersion values;
- run on real supported macOS versions and Apple Silicon hardware;
- acquire Developer ID credentials and a protected notarization workflow.

### 9.2 Recommended consumer path: app in a DMG plus Sparkle

For an ordinary desktop app, distribute a Developer ID signed and notarized app
inside a notarized DMG. The DMG is a delivery container, not an updater.

On direct launch, offer to copy the app into
<code>~/Applications</code> for the current user. A deliberate all-user
installation goes to <code>/Applications</code>.

Until the complete bundle is verified in a supported writable Applications
location and a receipt records <code>UpdateOwner=sparkle</code>, the loose app is
manual/check-only. In particular, Sparkle must not try to replace an app on a
mounted DMG, a read-only path, or a translocated launch location. Choosing to
keep that copy portable preserves this check-only state.

Use Sparkle 2 for the direct-distribution build:

- an appcast describes releases;
- EdDSA signatures validate update archives;
- Developer ID signing and notarization remain mandatory;
- the feed and release notes may also be signed;
- updates can use DMG, ZIP, archive, or installer-package payloads;
- deltas are optional after the full-update path is proven;
- key rotation is rehearsed before release.

Sparkle can share the canonical Broiler release data, but its appcast is a
platform adapter generated from that data.

### 9.3 PKG possibility

A signed and notarized flat PKG is the operating-system-native installer option
for machine-wide resources, privileged helpers, or enterprise deployment.

PKG updates work by running a newer package, through user authorization or
MDM/Jamf/Munki. Sparkle can deliver installer packages, but package updates add
authorization, delta, and key-rotation constraints. Use PKG only when Broiler
needs files outside the app bundle; do not choose it merely because it looks
more installer-like.

Sign the app with Developer ID Application and a PKG with Developer ID
Installer. Enable the hardened runtime, use secure timestamps, notarize with
current Apple tooling, and staple tickets where supported.

### 9.4 Other macOS possibilities

| Channel | Update behavior | Policy |
|---|---|---|
| Mac App Store | Store owns updates | Separate Store build; no Sparkle self-updater |
| Homebrew Cask | The cask declares <code>auto_updates</code>; Sparkle alone updates the direct app payload while Brew installs/removes it | Secondary discovery/installation path |
| Manual DMG | User replaces app | Recovery path only once Sparkle exists |
| Enterprise MDM/Jamf/Munki | Organization owns rollout | Supported native handoff |
| ZIP app bundle | Manual while loose; Sparkle only after verified relocation | Less friendly than a DMG; preserve signing and bundle structure |

---

## 10. Generic portable-to-profile installation

### 10.1 When to ask

Evaluate explicit launch controls before looking at another installation:

- <code>--portable</code> or a <code>broiler.portable</code> marker means run this
  payload in place with check-only update behavior; it suppresses destination
  redirection and the install prompt;
- <code>--no-install-prompt</code> suppresses first-run UI for this launch and
  runs the payload in place/check-only unless it is already an installed copy;
- <code>--install-for-user</code> is explicit consent to inspect/install/import
  into the per-user destination and overrides the portable marker for this
  launch;
- <code>--no-update</code> disables network update checks regardless of the
  selected location.

Then inspect the expected per-user destination and its receipt for the same
product to determine the choice shown; inspection alone never mutates or
redirects either copy:

- if a valid self-managed installation is the same version or newer on the
  selected channel, offer to open that bootstrap with the original arguments
  (or repair it) and retain a Continue portable choice;
- if the portable payload is newer, offer an explicitly confirmed import
  through the normal verified staging/update transaction, preserving the
  existing receipt, rollback state, settings, and channel policy; a previously
  configured automatic-update policy may apply only through that installed
  updater, never merely because the portable file was launched;
- if the portable payload is older, do not downgrade the installed copy unless
  a separately authorized downgrade is explicit and compatible with the
  security floor and data schema; continuing to run the loose copy remains
  available and check-only;
- if the receipt is corrupt or ownership is ambiguous, offer repair or continue
  portable in check-only mode; never overwrite <code>active.json</code>.

Show the prompt only when all conditions are true:

- this is an official <code>ArtifactKind=portable-archive</code> distribution;
- its embedded product metadata and payload manifest are valid;
- no package-manager receipt owns the running payload;
- the user has not selected a permanent portable choice;
- the launch is interactive and not help, offscreen, CI, test, or automation;
- the executable is not already inside the self-managed install root.

Never prompt for a developer build, WebAssembly build, native package, Store
package, Flatpak, or Snap.

Future direct macOS builds reuse the three-choice UX but follow section 9. If
the user accepts relocation, copy the complete signed <code>mac-direct</code>
bundle to <code>~/Applications</code> and record
<code>UpdateOwner=sparkle</code>. If they keep the loose copy, record the
preference in user configuration and remain manual/check-only. macOS direct
builds do not masquerade as <code>portable-archive</code> or use the
Windows/Linux <code>active.json</code> transaction.

### 10.2 Prompt

Coordinate the preview safety notice and installation choice in one first-run
flow. Suggested choices:

- **Install for me (recommended):** copy the complete verified application into
  the user profile, create selected integration, enable update checks, launch
  the installed copy, and leave the download untouched.
- **Keep using portable:** run in place for now and ask again on a later
  interactive launch.
- **Always use this copy as portable:** write the local marker or preference
  and do not ask again.

When another valid installation exists, adapt the first choice to **Open the
installed copy** or **Update the installed copy** as appropriate. Opening,
repairing, or importing still requires the explicit choice (or the
<code>--install-for-user</code> command); simply launching the download never
changes the installed copy.

Shortcut, file-association, protocol, desktop-entry, and launch-on-login
choices must be explicit and reversible. Launch-on-login is not recommended by
default.

### 10.3 Per-user locations

| Platform | Application payload | Settings/state/cache |
|---|---|---|
| Windows | <code>%LOCALAPPDATA%/Broiler/Apps/&lt;product&gt;</code> | Roaming settings under <code>%APPDATA%/Broiler</code>; local update/cache state under <code>%LOCALAPPDATA%/Broiler</code> |
| Linux | <code>$XDG_DATA_HOME/broiler/apps/&lt;product&gt;</code>, defaulting under <code>~/.local/share</code> | <code>$XDG_CONFIG_HOME</code>, <code>$XDG_STATE_HOME</code>, and <code>$XDG_CACHE_HOME</code> |
| macOS, future direct build | Complete signed app at <code>~/Applications/Broiler Browser.app</code> or Writer equivalent; Sparkle owns updates | <code>~/Library/Application Support/Broiler</code> and <code>~/Library/Caches/Broiler</code> |

On Linux, optional command links go in <code>~/.local/bin</code>, and desktop
files/icons go under the appropriate XDG data directories. Do not modify shell
startup files automatically merely to add that directory to PATH.

### 10.4 Copy transaction

For Windows/Linux portable archives, the running download never copies itself
directly over a live installation. The macOS app-bundle/Sparkle transaction is
specified separately in section 9.

1. Resolve and validate the per-user destination.
2. Acquire a product-wide installation lock.
3. Re-read and revalidate any receipt, active pointer, installed version, and
   channel under that lock; return to the existing-install rules in section
   10.1 if state changed after the prompt.
4. Confirm available disk space for download/staging plus the retained version.
5. Create a private staging directory under the destination volume.
6. Copy or extract the complete payload.
7. Reject absolute paths, parent traversal, device files, unexpected links,
   oversized files, duplicate paths, and case-collision paths.
8. Verify the detached payload-manifest signature, then verify every declared
   file against <code>payload-manifest.json</code>.
9. Flush the staged files and write a journal entry that marks the version
   prepared but not active.
10. Atomically rename staging to the immutable version directory.
11. Install or verify the stable bootstrap without changing the active version.
12. Write and atomically publish the installation receipt.
13. Write <code>active.json</code> to a temporary file, flush it, and atomically
    publish it as the final commit step. It may point only to the version
    directory already published in step 10.
14. Create user-approved integration through platform adapters.
15. Launch the installed bootstrap with all original arguments and a
    one-time completion token.
16. Exit the downloaded instance.

Do not delete or modify the original download. If relaunch fails, show the
installed location and continue to allow the portable copy to run.

On restart, recovery may remove an uncommitted staged/version directory or
finish integration, but it must never synthesize an active pointer to a missing
version.

This Windows/Linux transaction creates
<code>UpdateOwner=broiler-self-managed</code> in the receipt. It does not and
cannot rewrite the payload's embedded
<code>ArtifactKind=portable-archive</code>. Subsequent launches require those two
facts to be a permitted pair.

Windows Browser currently accepts an initial URL and Linux Browser has its own
option parsing; all original arguments must survive relaunch. Windows Writer
must gain argument handling before document-open relaunch can be promised.

### 10.5 Self-managed Windows/Linux on-disk layout

    <install-root>/
      Broiler.Bootstrap[.exe]
      active.json
      install-receipt.json
      update-state.json
      versions/
        0.1.0-preview.2/
          payload files
        0.1.0-preview.3/
          payload files
      staging/
      logs/

Version directories are immutable after verification. <code>active.json</code>
is a tiny atomic pointer containing product, version, and relative payload
entry point. The stable bootstrap reads it and starts the app.

Retain the current and previous known-good versions. Delete older versions only
after a successful health window and never while a process is using them.

### 10.6 Repair and uninstall

Ship a self-managed Windows/Linux repair/uninstall command through the stable bootstrap and
register it in the platform's application list where practical. Repair verifies
the receipt, active pointer, bootstrap, payload manifest, and user integration;
it never guesses ownership or downloads an update without the normal trust
checks.

Uninstall acquires the product lock, requests running processes to close,
removes shortcuts/file associations/protocols/desktop files created by that
receipt, deletes all self-managed version and staging directories, and finally
removes the bootstrap and receipt through a post-exit helper. Preserve user
documents and settings by default; offer a separate explicit data-removal
choice. A failed or cancelled uninstall remains recoverable and must not touch
native-package, Store, or another Broiler product's files.

The future macOS direct app uses normal app-bundle removal plus a separately
offered settings cleanup; its Sparkle receipt/integration rules are delivered in
Phase 6 rather than by this bootstrap uninstaller.

---

## 11. Update discovery and manifest design

### 11.1 Discovery options

| Option | Benefit | Limitation | Decision |
|---|---|---|---|
| Hand-edited JSON in the default branch | Very simple | Drift, mutable history, caching, human error | Do not use as the production source |
| GitHub release HTML scraping | No API design | Brittle, localized, not a machine contract | Reject |
| GitHub Releases REST API | Structured release/assets; public access | Unauthenticated rate limit; latest excludes previews; GitHub availability dependency | Required fallback/discovery source |
| Signed generated channel envelope on GitHub Pages or a dedicated updates branch | Small, cacheable, channel-aware, independent of API rate limit | Needs publication/key discipline | Recommended primary source |
| Full TUF repository | Strong threshold keys, expiry, rollback/freeze protection, mirrors | More operational complexity | Revisit for multiple mirrors/high assurance |

Recommended design:

1. Every GitHub release contains an immutable signed release envelope.
2. The release workflow generates, rather than hand-edits, one small
   <code>updates/v1/&lt;channel&gt;.signed.json</code> envelope containing the payload,
   key ID, and signature. Publishing one object avoids a transient mismatch
   between adjacent JSON and signature files. If detached signatures are ever
   retained, publish immutable versioned pairs and make clients retry a
   mismatched pair; never overwrite either half in place.
3. The envelope is served from a stable GitHub Pages URL or a dedicated protected
   updates branch.
4. If the envelope is unavailable, query the GitHub Releases API and verify
   release-attached envelopes. API fallback may authorize installation only
   when it can establish the highest unexpired signed channel-control sequence
   (including pause, revocation, and security-floor state) at least as new as
   the locally cached sequence. Otherwise it is check-only: show release
   information and a manual release-page link, but do not download or apply.
5. If both are unavailable, keep running and expose a link to the releases page.
6. Never scrape the HTML releases page to decide or install an update.

Use conditional HTTP requests and cache ETag/Last-Modified values. GitHub
allows public unauthenticated release reads but limits unauthenticated REST
requests by source IP, so check no more than daily and respect rate-limit and
Retry-After headers. Do not embed a GitHub secret in a desktop client.

### 11.2 Example signed channel payload

The following is the decoded payload inside the one-file signed envelope. The
signature covers the envelope's defined payload bytes before parsing. The
production schema may use canonical JSON or a base64 payload, but verification
must never depend on reserializing untrusted JSON.

~~~json
{
  "schemaVersion": 1,
  "channel": "preview",
  "sequence": 18,
  "publishedAt": "2026-07-15T18:00:00Z",
  "expiresAt": "2026-08-15T18:00:00Z",
  "keyId": "broiler-update-2026-a",
  "products": {
    "broiler-browser": {
      "version": "0.1.0-preview.2",
      "releaseUrl": "https://github.com/Broiler-Platform/Broiler/releases/tag/app-v0.1.0-preview.2",
      "minimumBootstrapVersion": "0.1.0-preview.1",
      "minimumUpdaterVersion": "0.1.0-preview.1",
      "minimumAllowedVersion": "0.1.0-preview.1",
      "rollout": {
        "percentage": 100,
        "seed": "app-v0.1.0-preview.2"
      },
      "bootstrapArtifact": {
        "component": "bootstrap",
        "artifactKind": "portable-archive",
        "version": "0.1.0-preview.2",
        "rid": "win-x64",
        "os": "windows",
        "architecture": "x64",
        "archiveFormat": "zip",
        "payloadRoot": "bootstrap",
        "entryPoint": "Broiler.Bootstrap.exe",
        "minimumOsVersion": "10.0.19045",
        "url": "https://github.com/Broiler-Platform/Broiler/releases/download/app-v0.1.0-preview.2/broiler-browser-bootstrap-0.1.0-preview.2-win-x64.zip",
        "size": 2345678,
        "sha256": "hex-encoded-sha256"
      },
      "artifacts": [
        {
          "component": "application",
          "artifactKind": "portable-archive",
          "rid": "win-x64",
          "os": "windows",
          "architecture": "x64",
          "archiveFormat": "zip",
          "payloadRoot": "payload",
          "entryPoint": "Broiler.Browser.Windows.exe",
          "minimumOsVersion": "10.0.19045",
          "url": "https://github.com/Broiler-Platform/Broiler/releases/download/app-v0.1.0-preview.2/broiler-browser-0.1.0-preview.2-win-x64-portable.zip",
          "size": 123456789,
          "sha256": "hex-encoded-sha256",
          "payloadManifestSha256": "hex-encoded-sha256"
        },
        {
          "component": "application",
          "artifactKind": "msi",
          "rid": "win-x64",
          "os": "windows",
          "architecture": "x64",
          "archiveFormat": "msi",
          "minimumOsVersion": "10.0.19045",
          "nativeVersion": "0.1.2",
          "url": "https://github.com/Broiler-Platform/Broiler/releases/download/app-v0.1.0-preview.2/broiler-browser-0.1.0-preview.2-win-x64.msi",
          "size": 123456789,
          "sha256": "hex-encoded-sha256"
        }
      ]
    }
  }
}
~~~

The OS version above is an illustrative machine-comparable value, not the final
Broiler support commitment; Phase 0 freezes the real baseline. Linux candidates
also carry bounded <code>libcFamily</code> and <code>minimumLibcVersion</code>
fields. Archive format, component, payload root, and entry point are bounded by
schema and cross-checked against the signed payload manifest; native packages
do not accept an entry point. The real payload supports Browser and Writer plus
all active RIDs. It contains data, not shell commands. Updater behavior is
chosen by a hard-coded enum and platform adapter, so a compromised feed cannot
inject an arbitrary command line.

### 11.3 Manifest rules

- Unknown schema versions fail closed for installation but may still show the
  release page.
- Unknown optional fields are ignored; unknown required capabilities reject the
  candidate.
- Product ID, channel, RID, OS, architecture, libc where relevant, artifact
  kind, and local update owner must match.
- Archive format, payload root, and entry point use bounded schema enums and
  must agree with the signed payload manifest and fixed platform adapter.
- Versions are parsed by one tested SemVer implementation.
- Draft releases are never candidates.
- Preview/nightly releases require matching channels.
- Sequence and highest-seen version are persisted to detect rollback.
- Metadata expires. An expired feed can report that checking failed, but cannot
  authorize a new installation.
- Minimum bootstrap and updater versions are independent of the application
  version. They trigger the separately signed bootstrap-artifact path, not an
  attempt to parse unsupported instructions.
- Minimum allowed version provides an emergency security floor. Enforcement UX
  and offline grace require a documented release decision.
- Staged rollout uses a random local installation ID hashed with the rollout
  seed. It sends no stable machine identifier to the server.

---

## 12. Self-update transaction

### 12.1 Components

Proposed shared repository structure:

| Component | Responsibility |
|---|---|
| <code>src/Broiler.AppLifecycle</code> | Product/version model, SemVer, manifest verification, policy, install receipt, staging, state |
| <code>src/Broiler.AppLifecycle.Tests</code> | Unit, property, malicious-input, and state-machine tests |
| Platform adapters in Windows/Linux apps | Paths, locks, shortcuts, desktop integration, process discovery, native handoff |
| <code>Broiler.Bootstrap</code> per platform/product | Stable launcher, active pointer, recovery, post-exit activation |
| Release scripts under <code>scripts/release</code> | Package, manifest, hash, signature, SBOM, and validation generation |
| <code>eng/Versions.props</code> | Canonical application version and derived build metadata |
| <code>packaging/</code> | MSI, DEB, RPM, future macOS, Store, and repository definitions |

Keep the core platform-neutral. It must not contain WiX, APT, DNF, shell, COM,
or macOS-specific behavior.

### 12.2 Check and selection

1. Start the current app immediately; do not block normal startup on the network.
2. After the UI is usable, check cached policy and the 24-hour+jitter interval.
3. Fetch the signed channel envelope with bounded timeouts and conditional headers.
4. Fall back to GitHub Releases API only under the signed channel-control rule
   in section 11.1; otherwise fallback remains check-only.
5. Verify raw metadata before parsing decisions.
6. Select the exact product/channel/RID/artifact-kind candidate allowed by the
   local update owner.
7. Compare SemVer, security floor, OS baseline, and updater capability.
8. Apply rollout eligibility.
9. Show release notes and the responsible update mechanism.

Manual Check for updates bypasses the time interval but not signature,
channel, compatibility, or rate-limit safety.

### 12.3 Download and stage

1. Acquire a product update lock.
2. Recheck free space and configured maximum sizes.
3. Download into the install-root staging area with TLS, timeouts, and bounded
   redirects.
4. Resume only when the server validator and expected artifact identity match.
5. Verify byte length and SHA-256.
6. Verify the signed Broiler envelope/payload signature and native code/package
   signatures.
7. Extract defensively into a random private staging directory on the install
   volume, never directly into the final version path.
8. Verify every extracted file and executable mode.
9. Run a no-network staged smoke check where feasible.
10. Flush the staged files, write and flush a ready journal entry, and then
    atomically rename staging to the immutable version directory.
11. Atomically record that published version as staged, never active.

### 12.4 Activate after exit

1. Ask the user to restart, or apply when all processes for that product exit.
2. The app starts the external bootstrap/helper and exits.
3. The helper obtains the install/update lock, confirms no process still uses
   the active payload, and revalidates the receipt, old pointer, staged journal,
   and target directory.
4. It writes and flushes a pending transaction containing old and new pointers.
5. It writes the new <code>active.json</code> to a temporary file, flushes it, and
   atomically publishes it as the commit step.
6. It launches the new app with a one-time health token and original arguments.
7. The app reports startup success only after configuration migration and main
   window readiness.
8. The previous version remains available through the health window.

Windows cannot replace a running executable reliably, which is why activation
is delegated to a stable external bootstrap. Linux can atomically switch a
symlink, but using the same active-pointer model keeps behavior consistent.

### 12.5 Failure and rollback

- Download, verification, or extraction failure deletes only staging.
- Activation failure restores the previous active pointer.
- Restart recovery purges incomplete staging, completes only a flushed ready
  rename, and restores the previous pointer from the pending transaction when
  the new pointer or target is missing/corrupt.
- Repeated early crashes cause the bootstrap to offer one automatic rollback.
- Rollback changes application binaries only. It never rolls back user
  documents.
- Data migrations must be backward tolerant for at least one retained version,
  or create an explicit backup before an irreversible schema migration.
- A security-floor release may prohibit rollback to a known-vulnerable version;
  this exception requires release notes and an offline recovery artifact.
- Keep structured local logs with versions and error codes, but no URLs visited,
  document content, typed text, raw device identity, or authentication data.

### 12.6 Bootstrap updates

The stable launcher itself eventually needs updates. Use a deliberately rare
two-step protocol:

1. the old trusted bootstrap verifies a signed new bootstrap package;
2. a small copied helper runs after exit and swaps the bootstrap using two slots;
3. the old bootstrap remains as a recovery file until the new one has launched
   an app successfully;
4. the signed envelope's independent minimum-bootstrap and minimum-updater
   fields are advanced only after the transition path is proven from every
   supported old bootstrap/updater.

Never require an old client to interpret a manifest schema it does not support.

---

## 13. Native update delegation and fallback

~~~mermaid
flowchart TD
    A["Signed update metadata reports a newer version"] --> P{"Machine and enterprise policy permits action?"}
    P -->|"No"| O["Report managed restriction; do not update or sidegrade"]
    P -->|"Yes"| B{"Who owns this installation?"}
    B -->|"Self-managed / Broiler AppImage"| C["Download, verify, stage, atomically activate, retain rollback"]
    B -->|"Loose portable"| M["Check only, or offer verified profile installation"]
    B -->|"MSI / DEB / RPM / PKG"| D["Prefer the registered native manager or repository"]
    D --> E{"Native transaction available?"}
    E -->|"Yes"| F["Ask consent, close apps, hand off update, verify result"]
    E -->|"No or feed lagging"| Q{"Policy permits direct-package fallback?"}
    Q -->|"No"| O
    Q -->|"Yes"| G["Download the matching signed native package"]
    G --> H["Verify and open the same native installer"]
    H --> I{"Native installation still impossible?"}
    I -->|"Yes"| R{"Policy permits per-user sidegrade?"}
    R -->|"No"| O
    R -->|"Yes"| J["Offer explicit side-by-side per-user self-managed migration"]
    I -->|"No"| F
    B -->|"Store / MSIX / Flatpak / Snap"| K["Route to platform owner; do not alter package files"]
    K --> L{"Platform cannot update?"}
    L -->|"Yes"| R
    L -->|"No"| F
~~~

Fallback means one of two safe operations:

1. deliver the same native package to its native transaction engine; or
2. with explicit consent, create a separate user-owned installation and explain
   how to remove the stale native one.

It never means overlaying new files into <code>Program Files</code>,
<code>/usr</code>, an MSIX package directory, a Flatpak deployment, a Snap
mount, or a Mac App Store bundle.

"Unavailable" means a genuine transport/tool/repository failure, not an APT
hold or pin, DNF versionlock, Pacman ignore rule, Windows Installer policy,
Store/MDM restriction, or managed-deployment decision. Machine policy can
suppress direct-package fallback, per-user sidegrade, or both.

For every handoff that crosses an elevation boundary, fixed trusted adapter
code first re-evaluates current machine policy. It then securely opens the
download without following links, copies those bytes into a newly created
administrator/root-owned private staging location, flushes them, and re-verifies
the signed Broiler envelope, exact length and SHA-256, product identity, target
version, scope, and every applicable platform signature on that privileged
copy. The native transaction engine receives only this path, which lower-
privilege processes cannot write, rename, or delete; alternatively, a
platform-proven deny-write/delete handle may be held until the engine has
consumed the bytes. Re-verification without controlling the consumed bytes is
not sufficient. This rule applies to MSI, DEB, RPM, and PKG, not only Windows.
A local DEB is authenticated by the signed Broiler envelope/hash because
ordinary APT archive authentication covers repository metadata;
RPM/PKG/platform signatures are still verified where available. Remove the
privileged staging copy after the transaction. Never pass a manifest-provided
command to an elevated shell.

Native adapters are fixed code, not manifest-provided commands:

| Kind | Safe handoff |
|---|---|
| MSI | WinGet or signed MSI major upgrade in the same context |
| MSIX | App Installer/Store update UI |
| DEB | APT or graphical package installer |
| RPM | DNF/PackageKit; Zypper/YaST for openSUSE build |
| PKG | Installer/MDM with authorization |
| Mac direct | Sparkle only for a verified relocated app; loose DMG/download remains manual/check-only |
| Flatpak | Software center or <code>flatpak update</code> guidance |
| Snap | Snap refresh notification/guidance |
| Arch/AUR | Full Pacman/system upgrade or the configured AUR workflow |
| AppImage | Broiler's fixed full-file replacement adapter; AppImageUpdate is disabled in the official build |
| Homebrew Cask | Sparkle for a cask declared <code>auto_updates</code>; Brew remains install/uninstall tooling |
| Nix | Nix profile/system generation update |
| Loose portable | Check-only or verified profile installation; never in-place overwrite |
| App stores | Store product/update page |

---

## 14. Security and trust model

### 14.1 Required trust layers

| Layer | Purpose |
|---|---|
| HTTPS | Transport confidentiality and ordinary server authentication |
| Signed Broiler envelope/metadata | Authorizes product, version, artifact, size, hash, channel, and policy |
| SHA-256 and payload manifest | Detects corruption and validates every extracted file |
| Platform signing | Authenticode; MSI/MSIX signing; RPM/repository GPG; Apple Developer ID/notarization |
| Package-manager repository metadata | Lets APT/DNF/Flatpak/Snap/Store verify their own transactions |
| Release provenance/SBOM | Ties assets to source, dependencies, workflow, and reviewed commit |

HTTPS and a checksum stored beside the download are not sufficient by
themselves if the hosting account is compromised. Embed one or more update
verification public keys in the application and keep signing keys separate
from public hosting credentials.

### 14.2 Signing-key operations

- Keep the root/update signing key offline or in a protected signing service.
- Use a narrower online release key when automation is required.
- Embed current and next public keys before a rotation.
- Practice normal rotation and emergency revocation.
- Separate update-metadata keys from Authenticode, Apple, and repository keys.
- Require protected-environment approval for public Stable/Preview signing.
- Pin third-party release-workflow actions to reviewed full commit SHAs.
- Never place a long-lived private key or GitHub token in the desktop client.
- Timestamp platform signatures so they remain verifiable after certificate
  expiry where platform rules permit.

For a later multi-mirror or high-assurance system, adopt TUF-style root,
targets, snapshot, and timestamp roles with threshold keys. The version-1
manifest already includes sequence and expiry so that migration remains
possible.

### 14.3 Mandatory input defenses

The updater treats network and archive content as hostile:

- bound metadata, file count, individual file size, total extracted size, and
  compression ratio;
- reject path traversal, absolute paths, alternate streams where relevant,
  device files, hard links, unsafe symbolic links, and case collisions;
- allow only HTTPS artifact URLs and a documented redirect/domain policy;
- never execute from the download cache before verification;
- verify before extraction when the archive format permits, then verify every
  extracted file;
- use private directories and restrictive permissions;
- never construct a shell command from manifest strings;
- use argument arrays and hard-coded package-adapter verbs;
- protect state files with atomic writes and strict parsing;
- prevent downgrade/replay with channel, sequence, highest-seen version, expiry,
  and the optional security floor;
- rate-limit retries and respect proxy, metered-network, and offline settings.

### 14.4 Privacy

- Update checks send only ordinary HTTP request metadata needed to fetch a
  public channel file.
- Do not require authentication or a device account.
- Rollout uses a random local installation ID and sends no persistent identifier.
- No update telemetry is enabled by default.
- Any future crash/update analytics are separately opt-in and documented.
- Logs exclude browsing history, document names/content, typed input, cookies,
  tokens, full home-directory paths, and raw hardware identifiers.

### 14.5 Preview governance

The updater increases supply-chain reach and must not bypass Broiler's existing
preview controls:

- exact-commit human review is release-blocking;
- licenses and third-party notices ship in every artifact;
- known security limits appear in release notes and first-run UX;
- a draft GitHub release is staging, never an update candidate;
- the signed channel envelope advances only after final approval and downloaded-
  artifact verification;
- revoking a bad build is done by a new signed sequence/release, not by silently
  replacing a published asset.

---

## 15. Release pipeline

Replace the three divergent application draft workflows with one reusable
application-release pipeline while keeping draft releases as the review stage.

### 15.1 Pipeline stages

1. **Validate input**
   - version is canonical SemVer;
   - tag, channel, source commit, and changelog agree;
   - product/RID matrix is explicit;
   - native-version projections are monotonic.
2. **Build in isolation**
   - Browser and Writer publish to separate directories;
   - each RID builds on its native runner where required;
   - self-contained output is deterministic enough for repeatable validation.
3. **Test**
   - unit/integration tests;
   - Windows and Linux app smoke tests;
   - install/update fixture tests;
   - Linux native-dependency/offscreen tests;
   - pre-package metadata and schema validation.
4. **Authorize production signing**
   - complete exact-commit human review, license/provenance checks, and protected
     release-environment approval for the specific version, channel, product/RID
     matrix, and source commit;
   - unlock production signing/notarization/repository keys only for that
     approved release input; this is not permission to publish.
5. **Sign executable payloads**
   - finalize the app payload/bundle first;
   - Authenticode-sign and timestamp Windows executables and libraries;
   - Developer ID-sign complete future macOS app bundles;
   - perform any other inner-payload signing that changes bytes.
6. **Assemble**
   - stable payload layout;
   - licenses/notices/readme;
   - icons and desktop metadata;
   - generate the payload manifest only after inner signing is final;
   - sign <code>payload-manifest.json</code> and place its detached signature
     beside it before creating any portable archive;
   - generate SBOM and provenance inputs.
7. **Package**
   - portable archive;
   - MSI;
   - DEB and RPM;
   - future platform adapters.
8. **Finalize containers and release metadata**
   - sign MSI/MSIX/RPM and other native package containers;
   - notarize/staple future macOS app/container outputs in the required
     platform order;
   - generate and sign staged APT/DNF/other repository metadata from the final
     native packages;
   - perform no byte-changing step after a final artifact hash is computed;
   - compute final artifact lengths and SHA-256 values;
   - generate and sign checksums and the release envelope from those final
     bytes;
   - run non-mutating native-package lint/validation on the final outputs.
9. **Create draft release**
   - upload immutable, versioned assets;
   - attach known limitations and upgrade notes;
   - do not update any public channel envelope.
10. **Verify downloaded assets**
   - download from the draft/release service;
   - re-check signatures, sizes, hashes, install, launch, update, uninstall;
   - compare source commit/provenance.
11. **Final publish approval**
    - approve the exact downloaded artifact hashes and verification evidence;
    - confirm the signing/notarization/repository-key audit and rollout plan;
    - this approval does not reopen or mutate the already signed artifacts.
12. **Publish**
     - publish the GitHub release;
     - publish native repositories/manifests;
     - wait for package-channel availability where needed;
     - publish the higher-sequence one-file signed channel envelope last.
13. **Observe**
    - staged rollout;
    - monitor update failures through explicit user reports/opt-in metrics;
    - pause by publishing a new signed channel sequence if needed.

Enable immutable releases if supported by repository policy, but do not rely on
hosting immutability as the only security control.

### 15.2 Repository deliverables

Proposed files/directories:

    eng/Versions.props
    eng/distribution/update-envelope-v1.schema.json
    eng/distribution/artifact-kinds-and-update-owners.json
    packaging/windows/
    packaging/linux/deb/
    packaging/linux/rpm/
    packaging/macos/                 # future
    scripts/release/
    src/Broiler.AppLifecycle/
    src/Broiler.AppLifecycle.Tests/
    .github/workflows/application-release.yml

The exact project split may change during Phase 0, but one shared lifecycle
core and platform adapters are non-negotiable boundaries.

---

## 16. Test and acceptance matrix

### 16.1 Unit and property tests

- SemVer ordering, including preview-to-stable transitions.
- Native version projection and monotonic ordering.
- Product/channel/RID/artifact-kind/update-owner selection.
- Unknown/duplicate/oversized manifest fields.
- Signature verification and key rotation.
- Expiry, sequence rollback, freeze, and minimum-version rules.
- Rollout bucketing stability without network identifiers.
- Install/update state-machine crash recovery at every write boundary.
- Safe path validation on case-sensitive and case-insensitive file systems.
- Receipt corruption, permitted artifact-kind/update-owner pairs, and fail-safe
  repair/check-only behavior.

### 16.2 Integration tests

Use a local HTTPS test server and ephemeral signing keys to test:

- unchanged ETag/304 responses;
- timeout, DNS, proxy, TLS, redirect, 403, 404, 429, and server errors;
- truncated/resumed downloads;
- wrong size/hash/signature/product/RID;
- malicious ZIP/tar entries and decompression bombs;
- expired and replayed manifests;
- unavailable primary envelope with API fallback, cached higher sequence,
  channel pause/revocation, and mandatory check-only behavior;
- channel switches and staged rollouts;
- full bootstrap upgrade from every supported updater generation.

### 16.3 End-to-end platform tests

For every applicable native or self-managed format:

- clean per-user install;
- clean per-machine install where offered;
- launch from shortcut, file, URL, and terminal as applicable;
- N-1 to N update;
- same-version repair/reinstall;
- newer-version downgrade block;
- loose portable N-1/N/N+1 versus an existing self-managed receipt, including
  launch-existing, import-through-updater, and downgrade-block outcomes;
- update while one or several app instances run;
- cancellation before/after download and before activation;
- power/process termination at each transaction stage;
- insufficient disk and read-only destination;
- native repository unavailable, then Broiler fallback;
- APT hold/pin, DNF versionlock, Pacman ignore, Windows Installer policy, and
  MDM/Store restrictions suppress prohibited fallbacks/sidegrades;
- a low-privilege process attempts to swap a staged native package across the
  elevation boundary and post-elevation verification rejects it;
- native-manager install followed by confirmation that self-patching is disabled;
- explicit migration from native to self-managed;
- uninstall with settings preserved;
- optional remove-settings action;
- self-managed repair and interrupted uninstall recovery, including integration
  cleanup and concurrent-process handling;
- Browser and Writer side by side with independent versions/state;
- noninteractive/offscreen launch with no prompts.

Platform matrix:

| Phase | Required |
|---|---|
| Initial Windows | Supported Windows x64 baseline, standard user, admin/all-user MSI case |
| Initial Linux | Ubuntu 24.04 x64, Ubuntu 22.04 x64 compatibility, Debian 12 x64 |
| RPM phase | Current supported Fedora and selected RHEL-compatible baseline |
| ARM phase | Linux arm64 publish plus real hardware smoke before support claim |
| macOS phase | Supported Intel/Apple Silicon matrix and Gatekeeper/notarization tests |

### 16.4 Security release tests

- Authenticode/Apple/GPG verification from a clean trust store.
- Revoked/expired/wrong signing certificate cases.
- Old updater to rotated key transition.
- Tampered manifest and tampered platform package.
- Release asset replacement detection.
- Arbitrary-command and URL injection attempts.
- Local low-privilege user cannot modify another user's or machine install.
- Package manager database remains consistent after every fallback path.

---

## 17. Phased implementation roadmap

### Phase 0 - Freeze contracts

**Goal:** make releases machine-comparable before implementing an updater.

Deliverables:

- approve product IDs and separate Browser/Writer ownership;
- add canonical application SemVer and native-version projections;
- choose minimum supported OS/RID matrix;
- choose update signature algorithm/library and key custody;
- freeze artifact kinds, update owners, allowed conversion pairs, and the
  install-receipt schema;
- freeze portable payload layout and artifact names;
- decide the one-file signed-envelope format, stable channel URL, and canonical
  GitHub repository identity;
- freeze machine-comparable OS/libc constraints and independent
  bootstrap/updater versions;
- validate native-version projections against Apps and Features, WinGet, APT,
  DNF, and future bundle-version ordering;
- reconcile <code>linux-arm64</code> roadmap requirements with actual CI;
- write threat model and data-migration policy.

Exit:

- two independently built runs produce the same logical manifest;
- a script can reject non-monotonic versions and malformed artifacts;
- no current timestamp tag is treated as a client update.

### Phase 1 - Deterministic signed portable releases

**Goal:** turn the existing ZIP/tar prototypes into trusted release inputs.

Deliverables:

- one reusable application-release workflow;
- isolated Browser/Writer publish directories;
- self-contained per-product Windows/Linux portable bundles;
- complete license/provenance content;
- payload manifests, SHA-256, signed channel/release envelopes, payload
  signatures, SBOM, and provenance;
- published SemVer Preview release;
- generated signed release envelope and preview channel envelope;
- GitHub API fallback with ETag/rate-limit and signed-control/check-only behavior.

Exit:

- a standalone verifier selects and validates every artifact without running it;
- draft assets pass download-and-reverify tests;
- the public channel envelope cannot point to a draft or unapproved commit.

### Phase 2 - Portable first-run user installation

**Goal:** direct downloads offer a safe copy into the user profile.

Deliverables:

- shared AppLifecycle core and tests;
- embedded immutable ArtifactKind/build metadata plus a protected UpdateOwner
  receipt for installed copies;
- Windows and Linux path/integration adapters;
- three-choice first-run UX coordinated with the preview notice;
- portable marker and noninteractive flags;
- full-payload staging, verification, atomic install, receipt, and relaunch;
- existing-install version/channel arbitration using the normal update
  transaction, never direct active-pointer replacement;
- argument forwarding;
- self-managed repair/uninstall command with integration cleanup and settings
  preserved by default;
- settings/binaries separation audit.

Exit:

- launching an official portable Browser or Writer can install and relaunch the
  complete app without elevation;
- cancel/keep-portable paths are permanent or reversible as promised;
- N-1/N/N+1 portable launches against an existing installation cannot silently
  downgrade, switch channel, or replace ownership;
- repair and interrupted uninstall leave either a launchable installation or a
  cleanly removable one;
- no prompt appears in CI/offscreen/native-package builds.

### Phase 3 - Atomic Broiler self-updater

**Goal:** self-managed Windows/Linux installs update and roll back safely.

Deliverables:

- daily+jitter and manual update checks;
- signature/version/channel/RID selection;
- bounded downloader and defensive extractor;
- immutable version directories and stable bootstrap;
- post-exit activation, health token, rollback, and retention;
- updater settings and local privacy-safe logs;
- full-package updates and bootstrap key-rotation path;
- local HTTPS failure-injection suite.

Exit:

- N-1 to N succeeds on Windows and Linux;
- termination at every transaction boundary leaves N-1 or N runnable;
- corrupt/untrusted updates never execute;
- previous known-good rollback is proven.

### Phase 4 - Windows native delivery

**Goal:** provide the first OS-native consumer installation.

Deliverables:

- signed Browser and Writer MSI packages;
- per-user default and tested per-machine option;
- full major-upgrade model and downgrade block;
- WinGet manifests and submission automation;
- Apps and Features/repair/uninstall/integration;
- MSI-aware update handoff and signed-MSI fallback;
- Authenticode certificate rotation runbook.

Exit:

- clean install, N-1 to N, repair, uninstall, WinGet upgrade, direct-MSI
  fallback, and scope preservation pass on clean Windows systems;
- self-updater cannot mutate MSI-owned files.

### Phase 5 - Linux native delivery

**Goal:** cover the primary Linux distribution families.

Deliverables:

- DEB packages plus signed APT repository/keyring bootstrap;
- signed RPM packages plus signed DNF repository;
- desktop, MIME, icon, and AppStream metadata;
- declared/probed native dependencies;
- APT/DNF/PackageKit update handoff and same-format fallback;
- repository key-rotation and outage runbooks;
- arm64 publish lane, with support claim gated on hardware validation.

Exit:

- repository install and N-1 to N pass on each supported distribution;
- unattended/native GUI update behavior is documented;
- Broiler never overwrites files under package-manager ownership.

### Phase 6 - macOS enablement and delivery

**Goal:** add macOS only after a real application port exists.

Dependencies:

- native macOS Browser/Writer projects, graphics/input backends, and tests.

Deliverables:

- signed/notarized app bundles and DMGs;
- user-copy flow to <code>~/Applications</code>;
- Sparkle 2 appcast generated from Broiler release metadata;
- EdDSA, Developer ID, notarization, stapling, and key rotation;
- optional signed PKG for machine/enterprise needs;
- Mac App Store decision and separate Store build if pursued;
- Homebrew Cask metadata.

Exit:

- Gatekeeper accepts clean installs;
- Sparkle N-1 to N and rollback/recovery pass on supported Intel/Apple Silicon
  systems;
- Store/PKG builds never compete with Sparkle for ownership.

### Phase 7 - Secondary stores and universal formats

**Goal:** expand reach after ownership routing is proven.

Candidate order:

1. MSIX plus App Installer;
2. Flatpak/Flathub;
3. AppImage with Broiler-owned atomic full-file replacement and no competing
   AppImageUpdate/zsync owner;
4. Microsoft Store;
5. Snap;
6. Arch/AUR, Chocolatey, Scoop, Nix, and other community channels.

Each channel must pass its confinement, file/network portal, graphics/input,
update ownership, signing, and uninstall tests before being advertised.

### Phase 8 - Operational hardening

**Goal:** make updates safe to operate at scale.

Deliverables:

- staged rollout and pause controls;
- emergency security floor policy;
- signing compromise and key-recovery drills;
- repository/CDN outage fallback;
- signed channel-control mirroring for pause/revocation and check-only behavior
  when fallback cannot prove the newest control sequence;
- opt-in aggregate failure metrics if approved;
- support bundle with redaction;
- delta-update experiment with full-package fallback;
- TUF evaluation if mirrors or threat requirements grow;
- retention/EOL policy for old channels and updater generations.

Exit:

- a simulated bad release can be paused/recovered without asset replacement;
- key compromise, repository outage, and rollback/freeze exercises have written
  evidence and owners.

---

## 18. Sequencing

~~~mermaid
flowchart LR
    P0["Phase 0: contracts"] --> P1["Phase 1: signed portable releases"]
    P1 --> P2["Phase 2: user self-install"]
    P2 --> P3["Phase 3: atomic self-update"]
    P3 --> P4["Phase 4: MSI + WinGet"]
    P3 --> P5["Phase 5: DEB/APT + RPM/DNF"]
    M["macOS application/backend port"] --> P6["Phase 6: DMG/Sparkle + optional PKG"]
    P3 --> P6
    P4 --> P7["Phase 7: stores/universal formats"]
    P5 --> P7
    P6 --> P7
    P7 --> P8["Phase 8: operational hardening"]
~~~

Phases 4 and 5 can run in parallel after Phase 3. Phase 6 cannot begin merely
because packaging work is available; it depends on the macOS application port.

---

## 19. Risks and mitigations

| Risk | Consequence | Mitigation |
|---|---|---|
| Two update owners act on one install | Corrupt files/package database | Immutable artifact kind plus protected update-owner receipt; ambiguous state is repair/check-only |
| Current release tags are not versions | Wrong/latest selection | Canonical SemVer tags and signed manifest |
| GitHub API unavailable/rate-limited | Checks fail | Signed static index, ETag/cache, daily jitter, release-page link |
| GitHub/repository account compromised | Malicious asset | Pinned update signatures plus platform signing and anti-rollback |
| Running Windows binary cannot be replaced | Partial update | Stable external bootstrap, version directories, post-exit activation |
| Combined Browser/Writer payload collisions | Wrong assemblies/version | Separate product publish/install roots; suite only as meta-package |
| Linux dependency diversity | Launch failures | Formal distro matrix, native dependency declarations, portable probes |
| Preview data schema changes | Rollback damages settings | One-version backward tolerance or backup before irreversible migration |
| Signing key lost/compromised | Updates stop or become unsafe | Current+next keys, rotation drill, offline root/recovery process |
| Native repository lags GitHub release | User sees version but manager cannot fetch it | Publish repos before pointer; same-format signed-package fallback |
| Staged rollout becomes tracking | Privacy loss | Random local ID, no transmission, no default telemetry |
| macOS scope is promised too early | Unshippable roadmap claim | Explicit application/backend gate |
| Delta update complexity | Broken upgrades | Full artifacts first and always available |

---

## 20. Open maintainer decisions

The roadmap recommends defaults, but these require explicit maintainer approval
in Phase 0:

1. Confirm <code>Broiler-Platform/Broiler</code> as the canonical public release
   and update repository; fix remaining clone/repository references.
2. Confirm separate Browser and Writer packages with an optional suite
   meta-package.
3. Select the signed-envelope/payload-signature implementation and key custody
   model.
4. Choose GitHub Pages versus a protected updates branch for the signed channel
   index.
5. Choose WiX version and whether per-machine MSI is a separate package or an
   option in one package.
6. Select hosted APT and RPM repository infrastructure and supported
   distribution versions.
7. Decide whether Preview users automatically receive the matching Stable
   release.
8. Define security-floor enforcement and offline grace.
9. Decide the supported-version retention window and oldest updater that must
   remain upgradeable.
10. Decide when Linux arm64 becomes a supported runtime rather than build-only.
11. Decide whether macOS uses universal bundles and whether PKG has a real
    privileged/enterprise requirement.
12. Decide whether opt-in update failure metrics are acceptable.

None of these blocks writing tests and schemas, but product identity, version
projection, signing, and feed location must be frozen before public updater
code ships.

---

## 21. Non-goals for the first implementation

- silent elevation;
- replacing native package-manager files directly;
- an always-running update service;
- binary delta patches;
- peer-to-peer distribution;
- automatic channel changes;
- silent native-to-portable migration;
- automatic deletion of user settings/documents;
- macOS packaging before a macOS app exists;
- treating WebAssembly deployment as a desktop self-update;
- using release-page HTML as an update protocol.

---

## 22. Primary references

- GitHub Releases REST API:
  https://docs.github.com/en/rest/releases/releases
- GitHub immutable releases and artifact attestations:
  https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases
  and
  https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations
- GitHub REST conditional requests and rate limits:
  https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api
  and
  https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api
- Windows Installer major upgrades and patching:
  https://learn.microsoft.com/en-us/windows/win32/msi/major-upgrades
  and
  https://learn.microsoft.com/en-us/windows/win32/msi/patching
- WinGet upgrade:
  https://learn.microsoft.com/en-us/windows/package-manager/winget/upgrade
- MSIX App Installer automatic updates:
  https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview
- Debian APT archive authentication:
  https://manpages.debian.org/testing/apt/apt-secure.8.en.html
- RPM signing and DNF:
  https://rpm.org/docs/4.20.x/man/rpmsign.8.html
  and
  https://dnf.readthedocs.io/en/latest/command_ref.html
- Flatpak repositories:
  https://docs.flatpak.org/en/latest/repositories.html
- Snap updates:
  https://snapcraft.io/docs/managing-updates
- AppImage updates:
  https://docs.appimage.org/packaging-guide/optional/updates.html
- Apple direct packaging/notarization:
  https://developer.apple.com/documentation/xcode/packaging-mac-software-for-distribution
  and
  https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution
- Sparkle 2:
  https://sparkle-project.org/documentation/
- Homebrew Cask:
  https://docs.brew.sh/Cask-Cookbook
- XDG Base Directory Specification:
  https://specifications.freedesktop.org/basedir-spec/latest/
- The Update Framework overview:
  https://theupdateframework.io/docs/overview/
