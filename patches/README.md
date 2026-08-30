# Submodule patches awaiting upstream

Changes that belong in a `Broiler-Platform/Broiler.*` submodule but could not be pushed
from the session that wrote them: those remotes are outside the session's GitHub scope, so
`git push` returns 403. Each patch is captured here for a maintainer to apply upstream; the
submodule pointers in this repository are deliberately **not** bumped, because CI clones each
submodule by pointer and a pointer whose commit does not exist upstream would break it.

Apply with `git am` inside the target submodule, push, then bump the gitlink here.

This directory is a backlog, not an archive: delete a patch once its fix is upstream, and
renumber from `0001` against whatever is left. A `patches/NNNN` reference in an older commit
message or document is therefore almost always dangling — name the **commit subject** instead.

| Patch | Target submodule | Commit subject |
|---|---|---|
| `0001-media-break-graphics-dependency-cycle.patch` | `Broiler.Media` | Break the Media/Graphics dependency cycle: Media depends on nothing |
| `0002-graphics-implement-hwnd-video-output-contract.patch` | `Broiler.Graphics` | Implement the Media borrowed-HWND contract instead of being referenced by it |

## 0001 + 0002 — breaking the Broiler.Media ↔ Broiler.Graphics cycle

**These two are one change and must land together.** 0001 declares the contract, 0002
implements it. Applying either alone leaves that component unbuildable against the other.

`Broiler.Media.Video.MediaFoundation` referenced `Broiler.Graphics.Windows` to name the
concrete `HwndVideoOutput` presentation target it borrows, while `Broiler.Graphics`
referenced `Broiler.Media.Image` and `Broiler.Graphics.Direct2D` referenced
`Broiler.Media.Video` in the other direction. The MSBuild project graph stayed acyclic, so
nothing failed to build and the cycle was easy to miss — but it was real, and it was paid for
at the repository layer:

- the two components were **mutually recursive git submodules** (`Broiler.Media/.gitmodules`
  listed `Broiler.Graphics` and vice versa);
- each resolved the other through its **own nested mirror** rather than the canonical
  checkout, so a single `dotnet build src/Broiler.HtmlBridge.Dom` compiled
  `Broiler.Media.Image` twice — from `Broiler.Media/src/` and from
  `Broiler.Graphics/Broiler.Media/src/` — emitting two same-named assemblies, one of which
  won the copy to the output directory. Broiler.Media ADR 0001 forbids exactly this;
- the mirrors' pins **drifted**: `Broiler.Media`'s nested `Broiler.Graphics` sat on a
  pre-`src/` layout commit while the canonical checkout had moved on;
- neither component could be versioned or released without the other.

The fix inverts the contract rather than the ownership. A new Windows-only contracts
assembly, `Broiler.Media.Video.Windows`, declares `IHwndVideoOutput` — the borrower's view of
a presentation target. `Broiler.Graphics.Windows.HwndVideoOutput` implements it and keeps
owning window creation, resize, visibility and destruction exactly as Broiler.Media ADR 0005
requires; `Broiler.Media.Video.MediaFoundation` consumes the interface and names no graphics
type. Broiler.Media becomes a leaf with no Broiler dependency, and its `Broiler.Graphics`
submodule is dropped. The reasoning is recorded as Broiler.Media ADR 0006, added by 0001.

### Applying

1. `git am` 0001 in `Broiler.Media`, push, bump this repository's `Broiler.Media` gitlink.
2. `git am` 0002 in `Broiler.Graphics`, push, bump this repository's `Broiler.Graphics` gitlink.
   Bump that component's own `Broiler.Media` gitlink too, to a commit containing
   `Broiler.Media.Video.Windows` — its relative project references resolve to its nested
   `Broiler.Media` mirror, so a standalone `Broiler.Graphics` build needs it.

Order matters only in that step 2's gitlink bump needs step 1's commit to exist upstream.
**Root builds tolerate either order**, including step 2 without that inner gitlink bump: the
`Directory.Build.targets` redirect described below points Graphics' Media references at the
canonical top-level checkout. Verified — with both patches applied and the inner gitlink left
un-bumped, `Broiler.Graphics.Direct2D` builds clean with the redirect and fails with
`CS0234: The type or namespace name 'Windows' does not exist in the namespace
'Broiler.Media.Video'` without it.

### Breaking change

`HwndVideoTargetChangeKind` and `HwndVideoTargetChangedEventArgs` move from the
`Broiler.Graphics.Windows` namespace to `Broiler.Media.Video.Windows`. No in-tree consumer
outside `HwndVideoOutput` itself existed; an external consumer of those two type names needs
a `using` change.

### No main-repo fallback is needed

Nothing in this repository names the moved types or the removed project reference, and no
submodule pointer changes here, so the parent builds unchanged with the patches unapplied.

### Verified before capture (both patches applied in the working tree)

| Suite | Result |
|---|---|
| `Broiler.Media.Tests` (architecture) | 13/13 |
| `Broiler.Media.Video.MediaFoundation.Tests` | 12/12 |
| `Broiler.Media.{Video,Image,Audio,Image.Managed,Audio.Managed}.Tests` | all pass |
| `Broiler.Graphics.Tests` | 99/99 |
| `Broiler.Graphics.Windows.Tests` | 5/5 new; 6 pre-existing failures are `DllNotFoundException: d3d11.dll` (Windows natives absent on Linux) |
| `Broiler.HtmlBridge.Dom`, `Broiler.Layout`, `Broiler.Playback`, `Broiler.Wpt` | build clean |

### The nested-mirror duplicate, fixed alongside

`Broiler.Graphics` still carries a `Broiler.Media` submodule — it must, to build standalone —
and its relative project references resolve into that nested mirror. So an aggregate build
used to compile `Broiler.Media` twice, from two source trees, emitting same-named assemblies.

`Directory.Build.targets` now redirects all five of those references to the canonical
top-level checkout, in the same explicit style as the two `Broiler.HTML` blocks already there.
Verified by rebuilding `src/Broiler.HtmlBridge.Dom` from clean: the nested mirror is left with
no `obj/` at all, and every `Broiler.Media.Image.dll` in the workspace now derives from the one
canonical compilation.

This is orthogonal to the patches — it holds with them applied or not — but it is what makes
the apply order above forgiving.

Still open, and out of scope here: the same nested-mirror pattern appears **331 times**
workspace-wide (`Broiler.Browser`, `Broiler.Writer`, `Broiler.UI` and others each carry their
own mirrors and reference them the same way). Only the `Broiler.Graphics` → `Broiler.Media`
edge was measured to duplicate a compile in a root build, so only it is redirected. Whether the
rest are reached by root solutions has not been established.
