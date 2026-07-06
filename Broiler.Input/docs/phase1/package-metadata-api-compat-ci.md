# Broiler.Input Phase 1 Package Metadata, API Compatibility, And CI Matrix

**Status:** Implemented  
**Date:** 2026-07-02

## Package Metadata

`Broiler.Input/Directory.Build.props` applies component-local package metadata:

- `Authors`;
- `Company`;
- `PackageLicenseExpression`;
- `RepositoryType`;
- `PackageTags`;
- `EnableNETAnalyzers`; and
- `AnalysisLevel`.

Runtime projects keep their own `PackageId` and `Description`. Test foundation
projects are explicitly not packable.

## API Compatibility

The public runtime API baseline is:

```text
Broiler.Input/docs/phase1/api-baseline.txt
```

`Broiler.Input.Contract.Tests` compares exported public runtime types against
that baseline. API changes must update the baseline intentionally.

## Dependency Compatibility

The contract runner scans every `*.csproj` under `Broiler.Input` and fails if
any project contains a `PackageReference`.

It also verifies that `Broiler.Input` does not reference Windows assemblies.

## CI Matrix

The intended component CI matrix is:

| Job | Command | Platform |
|---|---|---|
| Build component | `dotnet build Broiler.Input\Broiler.Input.slnx` | Windows |
| Contract tests | `dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj --no-build` | Windows |
| No package references | Covered by contract tests | Windows |
| API baseline | Covered by contract tests | Windows |

No repository-level CI file is added in this slice because the user requested no
changes outside the new component.
