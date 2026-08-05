# SDK-project mutation matrix

- **Status:** Frozen for Phase 0
- **Owner:** Broiler Code workspace
- **Recorded:** 2026-08-05
- **Source of truth for decisions:** [Broiler Code architecture](broiler-code.md)

Broiler Code edits standard `.slnx`, `.sln`, and SDK-style `.csproj` files and
never invents a second project format. That means it will be asked to modify
files containing constructs it does not fully understand, and the failure mode
to avoid is not "refuses to edit" — it is **rewriting a construct it only
partly understood**. This matrix freezes what may be touched, so Phase 2 never
has to make that call under pressure.

## The four classes

| Class | The workspace may | The UI shows |
| --- | --- | --- |
| **Lossless** | Read, and round-trip byte-identically when unchanged | Normally |
| **Editable** | Read, and apply a minimal structural diff | Normally, with edit affordances |
| **Evaluated-only** | Read the declaration; know its effect only from a trusted worker evaluation | Read-only, with the evaluated value shown as derived |
| **Unsupported** | Read and preserve verbatim; never modify | Read-only, with an explicit diagnostic naming the construct |

Two rules apply across all four:

1. **A no-op save is byte-identical.** Whitespace, attribute order, comments,
   XML declaration, encoding, BOM, and line endings all survive. This is not a
   nicety: a project file is under version control, and an IDE that reformats it
   on open makes every future diff unreadable.
2. **A supported edit produces a minimal diff.** Adding one item adds one line.
   The workspace never reserializes a whole document to change part of it.

## Matrix

### Project structure

| Construct | Class | Why |
| --- | --- | --- |
| `<Project Sdk="Microsoft.NET.Sdk">` attribute form | Editable | The SDK identity is a single well-known attribute |
| `<Sdk Name= Version=/>` element form, `<Import Sdk=/>` | Lossless | Understood and preserved; changing it changes which targets run, so Phase 2 does not offer it |
| Custom or third-party SDKs | Evaluated-only | What the SDK contributes is knowable only by evaluating it, and evaluating it runs its code |
| Unknown top-level elements | Unsupported | Preserved verbatim; the workspace has no model for them |
| XML comments, processing instructions, entity declarations | Lossless | Round-tripped by position, never regenerated |

### Properties

| Construct | Class | Why |
| --- | --- | --- |
| Unconditional scalar property in an unconditional group | Editable | Single value, single site, unambiguous |
| Property with a `Condition` | Evaluated-only | Whether it applies depends on the target framework, configuration, and RID being evaluated |
| Property referencing another property (`$(X)`) | Evaluated-only | The declared text is the truth; the value is not |
| `TargetFramework` (single) | Editable | The one property the IDE must be able to change to be useful |
| `TargetFrameworks` (plural) | Editable as a list | Adding or removing one moniker is a minimal, well-understood edit |
| Property set in `Directory.Build.props` or an import | Evaluated-only | The declaring file may be outside the workspace grant |
| Property inside a `<Choose>`/`<When>` block | Unsupported | Modelling the branch correctly requires evaluating the condition |

### Items

| Construct | Class | Why |
| --- | --- | --- |
| Explicit `Include` with a literal path | Editable | One path, one item, no expansion |
| Implicit SDK globs (`**/*.cs`) | Evaluated-only | The item set is a property of the filesystem, not of the file; adding a source file must not add an item element |
| Explicit wildcard `Include` (`Schemas\*.json`) | Evaluated-only | Same reason; the workspace shows the expansion but edits the glob only on explicit request |
| `Remove` and `Update` metadata | Editable | Well-defined operations on a known item |
| Conditional `ItemGroup` or item | Evaluated-only | Which items exist depends on the evaluation, as `SampleReports.App` demonstrates by conditioning out one of two files declaring the same type |
| Linked item (`Include=".." Link=".."`) | Editable | The link is explicit and both paths are literal. Identity follows the file's **location**, not its link: the same file linked into two projects is one workspace item with one buffer, and keying on the display path gives it two that disagree about whether it is dirty |
| Item whose `Include` resolves outside the granted roots | Unsupported | Read-only, with a capability diagnostic. It is not a modelling limit; the file is not the user's to grant here |
| Generated items added by targets | Evaluated-only | They exist only after evaluation, and only the worker can see them |

### References

| Construct | Class | Why |
| --- | --- | --- |
| `ProjectReference` to a project inside the workspace | Editable | The graph edge the IDE most needs to manage |
| `ProjectReference` outside the granted roots | Unsupported | Read-only with a capability diagnostic |
| `PackageReference` with a literal version | Editable | Adding one is a minimal diff. Phase 0 does not promise a package-management UI |
| `PackageReference` with a version from `Directory.Packages.props` | Evaluated-only | The version lives in another file, possibly outside the grant |
| `Reference` to a file path | Lossless | Preserved; changing it changes what the compiler binds against with no way to verify from the declaration |
| `FrameworkReference` | Evaluated-only | Its content comes from the SDK |

### Solutions

| Construct | Class | Why |
| --- | --- | --- |
| `.slnx` project entries | Editable | The format is declarative and its schema is stable |
| `.slnx` solution folders and files | Editable | Structural, no evaluation involved |
| `.sln` project entries | Editable | Supported, because a user's existing solution is usually `.sln` |
| `.sln` GUIDs | Lossless | Preserved exactly. Regenerating one silently detaches per-user state and source-control history |
| `.sln` configuration platform mappings | Lossless | Read and preserved; Phase 2 does not offer to edit them |
| Solution-level build dependencies | Evaluated-only | Their effect is a property of the build order, not of the file |

### Multi-targeting

Multi-targeting is called out separately because it is where "read the project
file" and "know what the compiler sees" diverge most sharply.

| Question | Answer |
| --- | --- |
| Which target frameworks exist? | Read from the file. `TargetFrameworks` is Editable |
| What does each one define, reference, and compile? | Evaluated-only, per target framework, from a trusted worker |
| Which one does the editor show diagnostics for? | An explicit user selection, named in the UI. Never an implicit "first one" |
| May the workspace assume the frameworks share a compile item set? | No. `SampleReports.Core` compiles an extra file for `netstandard2.0` only |

## What the fixture pins down

Every class above is present in
[`tests/broiler-code-phase0/fixture`](../../tests/broiler-code-phase0/fixture/),
so Phase 2 has a concrete instance to test against rather than a description:

| Class | Where |
| --- | --- |
| Multi-targeting with per-framework defines | `SampleReports.Core.csproj`, `net10.0;netstandard2.0` |
| Conditional item | `Compat/CompilerFeatureShims.cs`, netstandard2.0 only |
| Conditional item selecting between two files declaring the same type | `ReportRunner.Broken.cs` / `ReportRunner.Corrected.cs` |
| Linked file outside every project cone | `shared/BuildStamp.cs` |
| Explicit wildcard | `<None Include="Schemas\*.json" />` |
| Embedded resource | `Resources/report-template.txt` |
| Non-code content | `appsettings.json` |
| Project reference | `SampleReports.App` → `SampleReports.Core` |
| Solution folders and solution items | `SampleReports.slnx` |
| Evaluated-only toolchain pin | `global.json` |
| Evaluated-only restore configuration | `NuGet.config` |
| Generated compile inputs | `SampleReports.Web`'s `[JSExport]` source generator |
| Publish profiles carrying target settings | `SampleReports.Web/Properties/PublishProfiles` |

The measured consequence is recorded: evaluating `SampleReports.App` yields
**3 compile items** — not the 4 `.cs` files on disk — because one is
conditioned out. A workspace that enumerated the directory instead of reading
the evaluated item list would compile both files declaring `ReportRunner`, and
the Phase 0 Roslyn harness reproduced exactly that failure before it was
corrected to read `Compile` items from the evaluated graph.

## Escalation rule

When a construct is not in this matrix, it is **Unsupported** until it is added
here. A construct is promoted only with a fixture case and a round-trip test.
Guessing is what this document exists to prevent, and an unlisted construct is
precisely the case where guessing is most tempting and least safe.
