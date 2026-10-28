# M6: Office settings and integration

The existing Settings navigation now opens one settings page with Products,
Installation, BOSS and Updates sections. Each section scrolls independently;
Save/Cancel stay outside the scroll viewport. Windows enforces a 640 × 480 DIP
minimum client size. Controls use accessible names, standard keyboard interaction
and the published Broiler.UI preview.24 checkbox, combo box and scroll view.
No new NuGet publication is required.

## Settings behavior

- Product discovery uses an explicit Auto-detect/Custom selection. Auto-detect
  retains an inactive custom path; selecting Custom never changes back merely
  because its path matches discovery. Browse explicitly selects Custom. Save
  requires an existing absolute custom executable and valid argument quoting.
- Each product selects its preferred installed scope. Discovery tries a valid
  receipt in that scope, then the other scope. Custom overrides still take
  precedence. The visible Update button follows the preferred scope when both
  have candidates; scoped arrow-menu commands remain explicit.
- Product install, update and BOSS actions can be hidden independently. Hiding an
  action changes its presentation, not administrator privileges or registration.
- Installation shows computed fixed user/machine roots and the preferred scope.
  The preference orders install menu entries; every command still names the
  actual scope before administrator handoff. Optional shortcut creation is not
  implemented in this demo and is explicitly identified on the page.
- BOSS offers custom/automatic discovery, loopback HTTP URL including port,
  startup timeout, browser-on-ready, keep-running-on-exit and computed log root.
  Additional arguments support validated `--Logging:LogLevel:<category> <level>`
  pairs. Binding, content-root and Kestrel overrides are rejected; use the
  structured URL field. Arguments are passed as individual process tokens.
  Changed process/listener settings apply on the next start. An already owned
  process retains its original listener until stopped; external services remain
  externally managed.
- Updates offers stable/preview, startup checks, 1–168 hour interval, Check now,
  last attempt/success and diagnostics. Check now uses the draft channel and
  refreshes dashboard candidates without saving preferences or installing.
  Cache/backoff state survives restarts; failed checks retain last successful
  check history. No automatic installation is introduced.

All sections edit a draft. Save validates and atomically persists all preferences;
failure stays on the page, selects the relevant section and keeps every edit.
Back, Cancel and Escape reload saved settings and discard edits. Settings cannot
open during an installation, update or BOSS operation; the dashboard explains how
to finish/cancel that operation first.

## Migration and compatibility

Settings schema version is 2. Files without a schema version migrate in memory
with defaults; existing Browser/Writer/Cell/Plate enum names and IDs do not change.
Custom modes/paths and existing argument strings are retained. Arguments keep
the existing documented double-quote/backslash grammar; they are parsed into
tokens at launch rather than being converted to shell commands.

Missing and partially null groups recover with defaults. Invalid scope/channel,
interval or BOSS options recover predictably and surface a load diagnostic.
Malformed/unreadable JSON retains the existing error/default behavior, preserving
the original file until an explicit Save. Atomic replacement keeps a `.bak`.
Unknown root, product, BOSS and update fields, plus unknown product entries, are
preserved through cloning/saving. Newer schema versions can be read but cannot
be overwritten by this launcher.

## Verification and remaining acceptance

Automated coverage includes legacy/partial-null/future settings, unknown-field
preservation, all new preference round trips, invalid paths/listeners/arguments,
failed-save draft retention, discard, scope preference/fallback, action hiding,
busy navigation, draft-channel Check now, check intervals/history and layout at
480 × 360, 840 × 560 and 1280 × 800 DIP viewports. BOSS process tests verify the
validated logging tokens reach the runner.

Manual acceptance still includes actual desktop editing, keyboard/screen-reader
interaction at high DPI, UAC grant/deny and cross-user machine updates. Automated
hidden startup checks do not prove graphical usability. Linux and ARM64 remain
deferred. Production signing/key provisioning stays at the final release gate.
No commits, pushes, GitHub Actions runs or release publication are performed by
this milestone.

## Completed local acceptance

- **176 tests passed** in each of the production and explicit demo configurations.
- Windows launcher build passed with **zero warnings/errors**; the complete demo.4
  build published all six components and all **11 release assets** passed checksum
  and exact-file verification.
- The actual packaged settings page rendered in all four sections, at both scroll
  ends, at **100% and 200% DPI** with a 640 × 480 DIP viewport. The generated
  images were inspected for layout and footer separation. This uses the image
  renderer; native font rendering, input and accessibility still need desktop
  acceptance. The release workflow retains the render report and images.
- Full archive checks passed: all component manifests/ZIPs, five hidden desktop
  starts, BOSS HTTP/readiness/lifecycle checks, and the update contract fixture.
- A separate **demo.3 → demo.4** run passed actual Writer and BOSS updates, owned
  BOSS restart/readiness with validated logging arguments, retained previous
  payloads, exact receipt rollback and unchanged machine scope.

Demo:
`artifacts/office-demo-m6-1/package/assets/Broiler-Office-0.1.0-demo.4-win-x64.zip`.
Extract and open `Start-Broiler-Office.cmd`, then Settings. To demonstrate an
upgrade, use a managed demo.3 installation and choose the new bundle's manifest
under Tools → Use local update bundle.

Evidence:
`artifacts/office-demo-m6-1/smoke-final/`,
`artifacts/office-demo-m6-smoke.log`,
`artifacts/office-demo-m6-1/update-real/office-update-results.json` and
`artifacts/office-demo-m6-update-real.log`.
The artifact is explicitly marked as a working-tree demo build.
