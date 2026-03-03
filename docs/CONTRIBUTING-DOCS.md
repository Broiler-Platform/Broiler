# Contributing Documentation

> Guidelines for creating and maintaining project documentation in the
> Broiler repository. Following these conventions keeps the documentation
> navigable and consistent as the project grows.

## Adding a New Document

1. Choose the correct location:
   - **`docs/`** — guides, reference material, reports.
   - **`docs/roadmap/`** — phased development plans.
   - **`docs/adr/`** — architecture decision records.
2. Register the document in the appropriate index (see
   [Registration](#registration) below).
3. Include a **Related Documents** section at the end of the file (see
   [Related Documents Section](#related-documents-section) below).

## Registration

Every new document **must** be listed in its directory index so readers can
discover it:

| Document location | Index to update |
|-------------------|-----------------|
| `docs/*.md` | [`docs/README.md`](README.md) — add a row in the matching category table |
| `docs/roadmap/*.md` | [`docs/roadmap/README.md`](roadmap/README.md) — add a row with status |
| `docs/adr/*.md` | [`docs/adr/README.md`](adr/README.md) — add a row with ADR number, title, and status |

## Related Documents Section

Every document must end with a **Related Documents** section that links to
documents covering overlapping or complementary topics. This ensures a
reader can navigate between related material within two clicks.

```markdown
## Related Documents

- [Document Title](relative-path.md) — one-line description of relationship
```

## Roadmap Template

Use this template when creating a new roadmap in `docs/roadmap/`:

```markdown
# Roadmap: <Title>

> **Scope:** One-sentence summary of what this roadmap covers.

## Background

Describe the motivation for this work.

## Goals

Numbered list of high-level goals.

---

## Phase 1 — <Phase Title>

**Goal:** One-sentence phase goal.

- [ ] Task 1
- [ ] Task 2

**Acceptance criteria:**
- Criterion 1.

---

## Milestones and Timeline

| Phase | Description | Estimated Effort |
|-------|-------------|-----------------|
| 1     | Phase Title | X days           |

---

## Related Tests

| Test File | Project | Validates Phase |
|-----------|---------|-----------------|
| `ExampleTests.cs` | `Project.Tests` | Phase 1 — description |

---

## Related Documents

- [Related Doc](../relative-path.md) — relationship description
```

## ADR Template

ADRs follow the format established in `docs/adr/`. Use the next sequential
number and this structure:

```markdown
# ADR-NNN: <Title>

## Status

Proposed | Accepted | Deprecated | Superseded by [ADR-NNN](NNN-title.md)

## Context

What is the issue or question being addressed?

## Decision

What has been decided?

## Rationale

Why was this decision made? What alternatives were considered?

## Consequences

What are the positive and negative results of this decision?
```

After creating the ADR, add an entry to [`docs/adr/README.md`](adr/README.md).

## Style Guidelines

- Use **ATX headings** (`#`, `##`, `###`).
- Wrap prose at ~72–80 characters for readability in plain-text editors.
- Use **tables** for structured data (inventories, comparisons, timelines).
- Use **fenced code blocks** with a language tag for commands and snippets.
- Keep file names lowercase with hyphens (`my-new-guide.md`).

## Related Documents

- [Documentation Index](README.md) — top-level index of all project documentation
- [Testing Guide](testing-guide.md) — practical test-running instructions
- [Contributing Tests](CONTRIBUTING-TESTS.md) — guidelines for adding and organising tests
