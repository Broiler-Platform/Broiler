# PDF Phase 0 Status

**Status date:** 2026-08-22  
**Phase state:** Repository-controlled groundwork complete; Phase 0 exit remains
blocked on the external legal, standards-access, jurisdiction, history-audit, and
approval items below.

This record separates work the repository can prove from approvals that
engineering cannot self-grant.

## Repository-controlled work

- [x] Define component scope, dependency direction, delivery stages, and exclusions.
- [x] Define request/result/transaction direction and resolve conflicts in old ADRs.
- [x] Define default-deny resources, encryption rejection, active-content, metadata,
  privacy, and non-redaction policy.
- [x] Define units, pagination ownership, scripts/fonts, and platform gates.
- [x] Establish a feature/claim matrix.
- [x] Establish a versioned IP/licensing and standards register.
- [x] Establish approved-source and similarity-review controls.
- [x] Establish an empty, rights-aware corpus manifest and schema; no old fixture is
  presumed reusable.
- [x] Remove all obsolete external-process PDF CLI code and tests.
- [x] Correct documents that still describe the obsolete architecture.
- [x] Add automated guards against reintroducing legacy or misplaced PDF code.
- [x] Run and record the Documents and affected CLI test baselines.

## Validation record

| Date | Command / check | Result |
|---|---|---|
| 2026-08-22 | `dotnet test Broiler.Documents/Broiler.Documents.slnx -c Release --no-restore` | Passed: 363 tests across seven projects; 0 failed, 0 skipped |
| 2026-08-22 | `dotnet build src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj -c Release --no-restore` | Passed with 0 errors; existing repository warnings remain |
| 2026-08-22 | Parse both corpus JSON documents and run `git diff --check` | Passed; only Git line-ending notices reported |
| 2026-08-22 | Search active CLI/current architecture documentation for retired process tokens | No active occurrences |

## External decisions required for Phase 0 exit

- [ ] Name the qualified legal reviewer and target implementation/distribution
  jurisdictions.
- [ ] Approve the first implementation slice's exact ISO 32000 scope and lawful
  standards access.
- [ ] Decide the scope and obligations of the Adobe ISO 32000-1 public patent
  license; investigate relevant third-party declarations/claims.
- [ ] Clear each included filter/codec tuple, including exact JPEG processes,
  entropy modes, component/precision combinations, APP14/`ColorTransform`, and
  any LZW or fax work.
- [ ] Clear selected font formats/tables, Unicode data, URI standards, and any
  generated normative tables.
- [ ] Approve source-use, contributor-provenance, conformance wording, trademark,
  and non-endorsement policies.
- [ ] Complete the repository-history redistribution audit; document authority or
  apply the project's approved removal/rewrite policy to restricted material.
- [ ] Approve every corpus artifact's origin, license, redistribution, and privacy
  status before it is committed or used in distributed tests.

No unchecked item above is implied to be approved by this document. Phase 1 may
begin with architecture-only work only if the project explicitly accepts that
implementation and public capability claims remain blocked by the applicable
register rows.
