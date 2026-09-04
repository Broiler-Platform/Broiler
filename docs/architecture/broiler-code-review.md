# Broiler Code Human Review workspace

- **Status:** MVP-0 delivered on Windows and Linux
- **Owner:** Broiler Code, with the platform release and assurance owners consulted
- **Recorded:** 2026-09-04

Broiler Code's first useful release is not an IDE feature. It is the tool that
makes the platform's own claim about itself checkable.

Broiler says *"AI-generated code is human reviewed."* Today that is a sentence in
a README, backed by twelve component `HUMAN_REVIEW.md` files that attest to whole
components at a point in time and go quietly out of date as the code moves. MVP-0
replaces the sentence with a measurement:

> Every source file has a known review state relative to a concrete revision of
> its content.

That is a much stronger claim, and unlike the first one it is falsifiable.

Companion records: the [Broiler Code architecture](broiler-code.md) and the
[Broiler Code roadmap](../broiler-code-roadmap.md).

## What the product is

A three-pane review workspace inside the existing shell:

```text
┌──────────────────────┬──────────────────────────────────┬──────────────────────────────┐
│ Solution Explorer    │ Runtime/JsObject.cs              │ Human Review                 │
│                      │                                  │                              │
│ ▼ Broiler.JS         │ public JsValue Get(...)          │ Status: reviewed             │
│   JsObject.cs        │ {                                │ Reviewer: Enrico             │
│     reviewed         │     ...                          │ Reviewed at: 2026-09-04      │
│   JsArray.cs         │ }                                │ Reviewed at revision a31f0e2 │
│     reviewed, then   │                                  │                              │
│     modified         │                                  │ Notes  1 open of 2           │
│   JsString.cs        │                                  │  Why is the conversion done  │
│     needs review     │                                  │  before Get()?    line 143   │
└──────────────────────┴──────────────────────────────────┴──────────────────────────────┘
```

The left pane is the existing Solution Explorer, with a review badge on every
file somebody has recorded something about; a file nobody has touched is left
unmarked rather than labelled, because most of a codebase is in that state and
badging all of it would say nothing.

The middle is the existing editor. The right is new: it shows what is recorded
about the file on screen and what is still open on it, and carries the field a
note is typed into.

Everything is written through named commands under a **Review** menu — the four
decisions, Clear Review, Add Note and Review Coverage — so the menu and the
pane's note field drive one path and report one enabled state. There are no
keyboard shortcuts yet.

## The decisions

### Staleness is decided by content, not by a commit

This is the load-bearing decision and it is the one that inverts what most
people expect.

A commit-based rule — *"reviewed at `a31f0e2`; `HEAD` touches this file, so the
review is stale"* — is wrong in both directions. It calls a review stale after a
rebase, a cherry-pick, a squash or a branch switch, none of which changed a line
the reviewer read; and it calls a review current after a revert-and-reapply,
which did. A reviewer whose approvals expire for reasons unrelated to the code
stops trusting the tool, and a tool nobody trusts is a tool nobody uses.

So a review records the SHA-256 of the reviewed content, and staleness is a hash
comparison. The commit SHA is recorded too, as **provenance**: it answers "which
revision was this read at?" for an auditor and lets a reviewer pull up the exact
diff. It never decides anything. That is why `IRevisionProvider` is an interface
with a no-op default — a browser or Android host has a workspace and no
repository, and a review recorded there is worth exactly as much as one recorded
on a desktop.

Two normalizations are applied before hashing, and both exist because the
alternative produces false staleness on files nobody edited:

| Normalized | Why |
| --- | --- |
| Line endings, to LF | This repository holds CRLF files, LF files, and files mixed within themselves. An editor that rewrites one whole file normalizes them all — every byte changes and not one token. |
| A leading byte-order mark | Adding or dropping one is a save-time decision of whichever tool wrote the file last. |

Whitespace and case are deliberately **not** normalized. Trailing whitespace can
end a raw string literal, and re-indented code is code a reviewer has not read in
that form.

The algorithm is recorded in the hash itself (`sha256-nlf:…`). A record this
build cannot verify reports `Unknown`, never `Stale` — telling a reviewer their
approval expired because the tool was upgraded is how a warning gets trained out
of people.

### Notes hang on code, not on line numbers

Line numbers move under every edit above them, so a tool that stored only line
numbers would report every note against the wrong code after the first
insertion. A note therefore stores the text it was written against; where that
text sits now is recomputed on load.

| Result | When | What the pane shows |
| --- | --- | --- |
| `Anchored` | The recorded text is still at the recorded line | The line |
| `Moved` | It occurs exactly once elsewhere | Its new line |
| `Ambiguous` | It occurs more than once | The recorded line, and says so |
| `Orphaned` | It is gone | The recorded line, and says so |

The nearest-occurrence tie-break a fuzzier tool would use is refused on purpose.
Duplicated code is exactly where a reviewer's question matters most, and picking
the closest of four identical blocks would answer a question about one of them
with a note about another.

`ReviewAnchor.Symbol` is carried and never required. The review model has to work
on a host composing only the portable classifier, and on JSON, Markdown and shell
scripts that have no symbols at all. When a semantic service is present it fills
the field in and it is used for display and search — never to decide placement.

### Review notes are not code comments

The two are different kinds of statement and the tool keeps them apart.

| | Answers | Lives in | Lifetime |
| --- | --- | --- | --- |
| Code comment | *Why does this work this way?* | The source file | As long as the code |
| Review note | *What does a human still have to check here?* | `.broiler-review/` | Until it is answered |

```csharp
// ECMA-262 requires ToPrimitive before numeric conversion.
var primitive = value.ToPrimitive();
```

belongs in the source. Whereas

> ⚠ Verify against ECMA-262 §7.1.1. The implementation looks correct, but I have
> not checked the `Symbol.toPrimitive` edge cases.

does not: putting it there leaves a stale question reading like documentation
long after it is answered, and makes every review action a source diff.

### Records are committed, one file per source file

Records live at `.broiler-review/<source path>.review.json`. They are **not**
gitignored, deliberately — a review record that is not committed proves nothing
to anyone but its author. Once they are in the history, human review becomes part
of the project's history:

```text
Commit A   JsObject.cs added                → unreviewed
Commit B   JsObject.cs reviewed             → review record committed
Commit C   JsObject.cs modified             → the review goes stale
Commit D   reviewed again                   → record updated
```

The source tree is mirrored rather than flattened into a single `reviews.json`.
Two reviewers working on different components then touch different files, and
their branches merge without a conflict — which one shared index would make
impossible on any repository with more than one reviewer.

The JSON is hand-written rather than serialized by reflection, because the format
is a wire format with three requirements a serializer does not meet: renaming a
C# property must not change the file; writing an unchanged record must be
byte-identical, or opening a file dirties its record and fills a reviewer's
commits with diffs they did not make; and the result has to be readable in a
pull-request diff, which is after all the point of committing it. Non-ASCII is
left unescaped so a German or Japanese note stays readable there.

A record that becomes empty is deleted rather than written, so the review
directory stays a mirror of what has actually been looked at instead of
accumulating a placeholder for every file anyone ever opened.

### What the tool refuses to do

The fastest way to make this record worthless is to let something mark a file
reviewed without a human reading it. So:

- **A dirty document cannot be marked reviewed.** A review names the exact
  content a human read; unsaved text is content nobody else can fetch, so a hash
  of it could never be checked by CI, by a second reviewer, or by the same
  reviewer tomorrow. The command is *disabled with the reason*, not failed after
  the click. Clearing a review is the one exemption — it records nothing about
  content.
- **A review cannot be recorded without a reviewer name**, and whitespace does
  not count as one. Enforced in `FileReview.WithDecision`, not only in the shell,
  so the rule reaches a script and the CI tool as well as the menu. An approval
  with nobody's name on it is not evidence of anything.
- **`FileReview.WithDecision` hashes the content itself** rather than accepting a
  hash, and `ReviewedContentHash` is settable only inside the review assembly, so
  no consumer can mint an approval for content nothing read. What the type cannot
  check is that the content it was handed is really the file's — that is the
  caller's to get right, and why the controller takes it from the buffer and
  refuses a dirty document.
- **There is no `broiler-review mark` command.** Recording a review from a script
  is precisely the hole the rest of this list closes.
- **A resolution requires an answer.** "Resolved" with no text records that
  somebody clicked a button, not that anybody found out.

### Coverage excludes stale approvals

`ReviewCoverageTotals.VerifiedPercent` counts only files approved **and**
unchanged since. A stale approval is reported in its own column and does not
count, because nobody has confirmed the current content. This keeps the number
from ratcheting upward and staying there as the code moves out from under it, and
it is the reading that makes the number worth publishing beside the machine-checked
ones:

```text
Correctness                  Human verification
Test262    99.99 %           Source review    <n> %
WPT        83 %
```

The four buckets — verified, stale, flagged, unreviewed — are exhaustive and
disjoint and sum to the total. A coverage number whose parts do not add up
invites the reader to assume the flattering reading.

### The denominator says which files

Counting is over the workspace's own source, not over the files that already have
records — a percentage over reviewed files would be 100% by construction.

Nested checkouts are the subtle part. Each component carries its own copies of the
components it depends on so that it still builds standalone, so `Broiler.Graphics`
exists on disk many times over; its `HUMAN_REVIEW.md` alone appears nineteen
times. Counting the copies would inflate both halves of the fraction and make a
component's percentage depend on how many other components happen to vendor it.

They are collapsed by identity, not by skipping directories, because skipping
cannot tell a nested checkout from a component's own project directory —
`Broiler.CSS/src/Broiler.CSS/` and `Broiler.HTML/Broiler.CSS/src/Broiler.CSS/`
have the same name at different depths and only one is a copy. A file's identity
is its path from the last `Broiler.`-named segment onward, so both reduce to
`Broiler.CSS/…` and fold together. The least-nested path wins, which is always
the component's own checkout. Sorting by path instead would get this exactly
backwards: `Broiler.B` precedes `Broiler.D`, so every `Broiler.DOM` file would be
attributed to `Broiler.Browser` and `Broiler.DOM` would vanish from the report.

## Ownership

| Concern | Owner | Boundary |
| --- | --- | --- |
| Review record, staleness, note anchoring, coverage arithmetic | `Broiler.Code.Review` | References `Broiler.Code.Workspaces` and nothing else. No Broiler.UI, no Roslyn, no git, no platform. |
| Pane, explorer badges, commands, revision provider | `Broiler.Code.Core` | The seam that knows about both a workspace and a screen |
| Coverage reporting and the CI check | `Broiler.Code.Review.Cli` | No display; shares the model with the editor |
| Composing the pane | `Broiler.Code.Windows`, `.Linux` | The review controls are optional, so a head that supplies none is unaffected |

`Broiler.Code.Review`'s isolation is asserted, not described:
`CodeEditorArchitectureTests.The_Review_Model_Depends_Only_On_The_Workspace`
fails if a reference is added. The two most tempting additions are the two that
would break it — a UI reference, so a pane could render a status directly, which
would stop CI computing coverage with no display; and a git package, so staleness
could be decided from a commit, which would make a review expire on a rebase.

Storage goes through `IWorkspaceStorage` throughout, so a review can only touch
the roots the user granted, and the same store works over a desktop filesystem,
Android's Storage Access Framework and a browser directory handle without a
second implementation.

## The command line

`broiler-review` runs the same evaluation where there is no display, so the
record is checkable by somebody other than the person who wrote it.

```bash
dotnet run --project src/Broiler.Code.Review.Cli -- coverage --root . --markdown coverage.md --json coverage.json
```

```bash
dotnet run --project src/Broiler.Code.Review.Cli -- check --root . --changed changed.txt
```

`check` restricts itself to the files a pull request touched when `--changed` is
given; without it, every pre-existing stale review in the repository would be
reported on every pull request and the one file the change actually invalidated
would be invisible among them.

Two defaults are deliberately permissive, because a gate that fails on day one is
a gate that gets deleted on day one: `check` annotates and exits zero unless
`--fail-on-stale` is passed, and `coverage` enforces no minimum unless
`--minimum` is given. Files that were **never** reviewed are never reported by
`check` at all — the coverage number is how those are reported.

CI wiring is [`.github/workflows/human-review.yml`](../../.github/workflows/human-review.yml).

## What MVP-0 does not do

Stated rather than left to be discovered:

- **The pull-request check does not cover submodules.** `git diff` in the
  superproject reports a submodule as one gitlink path, never the files inside
  it, so a stale review at `Broiler.CSS/src/…` can only be matched by treating a
  changed submodule as a whole-directory change — which is what `check` does.
  The annotation therefore lands on the file, but a pull request that bumps a
  pointer flags every stale review in that component rather than the ones its
  bump actually invalidated. `coverage` counts them all correctly.
- **Only Question notes can be written.** `Concern`, `Todo` and `Observation`
  exist in the record format, are read back, and are rendered — but the pane has
  no way to choose one, so nothing in the product creates them.
- **A note cannot be answered or deleted from the editor.**
  `ResolveNoteAsync` and `RemoveNoteAsync` exist and are tested; no command
  reaches them. Until one does, the open-note count is one-way and the rule that
  a resolution requires an answer is a rule nothing can exercise.
- **There are no keyboard shortcuts.** The commands carry access keys for the
  menu; no key binding invokes one directly.
- **Note text is one line.** `UiEdit` is single-line and `UiRichEdit` is a
  formatted document, not a plain-text box. The record format already carries
  multi-line text, so a multi-line control needs no migration.
- **The review splitter is a grip and nothing else.** `UiSplitter.Value` is read
  by nobody here — the Solution Explorer's splitter has been inert the same way
  since it was composed, and pane width comes from the head's `PreferredSize`.
  Applying `Value` to pane layout is one change for both splitters.
- **Symbol anchors are never populated.** Nothing composes a semantic service
  into the shell yet. The field, the format and the display path all exist.
- **Only the current document's badge follows an edit live.** Every file is
  evaluated against its real content when the workspace opens, closed ones
  included; after that, only the file being looked at is re-evaluated. It is the
  only one whose text can have changed, and re-hashing every open document on
  every tab switch would put the cost of a large file on a gesture that should be
  instant. A file edited outside the editor keeps its badge until the next
  workspace load.
- **Android and browser heads compose no review pane yet.** The controls are
  optional precisely so they can adopt it when they are ready.
- **The twelve component `HUMAN_REVIEW.md` files are untouched.** They record
  whole-component attestations with reviewer contacts and conditions, in at least
  two different field vocabularies (`- **Commit:**` versus
  `- **Reviewed revision:**`). Folding them into per-file records would be a
  migration with a policy question inside it — whether a component-level approval
  implies a per-file one — and the answer is a human's, not a tool's.
