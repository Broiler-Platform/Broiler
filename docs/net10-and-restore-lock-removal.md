# .NET 10 targets and NuGet restore lock removal

Updated 2026-10-29.

Removed all 12 source `packages.lock.json` files in Writer, Cell and VM. Removed
lock-file generation flags, runtime-specific lock paths and locked-restore CI
steps. Web producer and verification scripts no longer require or generate
restore lock files. Explicit central dependency versions remain in place.

VM package admission now reads resolved versions and SHA-512 hashes from
`obj/project.assets.json`. Its allow-list, source mapping and hash validation
continue to run. Its temporary admission probe uses ordinary restore, and the
hash-tampering negative control still fails as intended. Historical lock-format
witness files remain test data; they do not enable restore locks.

Changed eight Unicode library/generator/test projects from
`net8.0;net10.0` to the single `net10.0` target. Its feed-consumer verification
also targets only .NET 10. A source XML scan found no remaining .NET 8 targets
and no remaining source NuGet restore lock files.

The Roslyn source generator's .NET Standard 2.0 target and the Code compatibility
test fixture's .NET Standard target remain; neither is a .NET 8 application target.
SDK/workload cohort selection and Office release-asset integrity manifests were
not removed as NuGet restore locks.

Validation:

- Unicode Properties, Emoji and CLDR tests: 114 passed on .NET 10.
- VM package admission tests: 6 passed after ordinary solution restore.
- Web producer/deployment contract tests: 25 passed.
- Writer and Cell ordinary restores passed without recreating lock files.
- The CLDR generator Release build passed without warnings.
- Changed workflows passed Actionlint; changed XML files parse successfully.

No commits or publication were performed.
