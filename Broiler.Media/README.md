# Broiler.Media

Broiler.Media is the planned decode-first media component for Broiler.

This folder contains the Phase 1 component scaffold. Phase 0 records are kept in
`docs/`; Phase 1 adds the runtime and test project graph plus the first public
abstraction contracts. Concrete decoders are intentionally still placeholders
until the later roadmap phases move or implement real codec behavior.

## Component constraints

- Target .NET 10 only.
- Do not add third-party runtime dependencies.
- Keep abstraction assemblies platform-neutral, safe-code compatible,
  trimming-friendly, and AOT-friendly.
- Put OS-dependent code in OS-specific implementation projects only.
- Use `LibraryImport` or `DllImport` for native OS entry points. The first
  OS-specific implementation target is Windows.
- Do not add hidden global codec registration or module-initializer side effects.
- Do not modify Graphics, HTML, UI, Input, or other existing components during
  Phase 0.

## Runtime projects

- `Broiler.Media`
- `Broiler.Media.Audio`
- `Broiler.Media.Audio.Managed`
- `Broiler.Media.Video`
- `Broiler.Media.Video.MediaFoundation`
- `Broiler.Media.Image`
- `Broiler.Media.Image.Managed`

Matching dependency-free console test projects live beside the runtime projects.

## Phase 0 contents

- [Phase 0 Record](docs/phase-0.md)
- [Graphics Image API Inventory](docs/api/graphics-image-api-inventory.md)
- [Image Baseline Record](docs/baselines/image-baseline-record.md)
- [ADR 0001: Component Topology And Consumption Policy](docs/adr/0001-component-topology-and-consumption-policy.md)
- [ADR 0002: Buffer Ownership And Limits](docs/adr/0002-buffer-ownership-and-limits.md)
- [ADR 0003: Image Pixel And Alpha Format](docs/adr/0003-image-pixel-and-alpha-format.md)
- [ADR 0004: Compatibility Window](docs/adr/0004-compatibility-window.md)
- [ADR 0005: Windows Media Foundation Borrowed HWND](docs/adr/0005-windows-media-foundation-borrowed-hwnd.md)
