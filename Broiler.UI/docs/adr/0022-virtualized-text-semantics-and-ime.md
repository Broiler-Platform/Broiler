# ADR 0022 - Virtualized Text Semantics and Bounded IME Queries

**Status:** Approved for Broiler Code Phase 0  
**Date:** 2026-08-05

## Context

Broiler.UI's text-input contract and its accessibility semantics both assume, in
places, that a control can hand over its whole text. For an edit box that is
fine. For a source editor it is not: the Phase 0 fixture is a 100,000-line,
10.5 MB document, and Phase 1's exit gate names exactly that size.

Two paths are affected:

- **IME.** A composition-aware input method asks for surrounding text to place
  candidate windows and to reconvert. `AndroidInputCoordinator` and
  `BroilerInputConnection` currently satisfy such requests from a whole-text
  view. On a multi-megabyte buffer, an IME keystroke would materialize the
  document.
- **Accessibility.** ADR 0008's semantic bridge exposes text value. A screen
  reader that receives a 10 MB string per query is not usable, and the
  marshalling cost alone would exceed any interaction budget.

Neither is a Broiler Code problem specifically. Both are contract gaps that a
large document exposes.

## Decision

### Bounded surrounding-text queries

The neutral text-input contract gains **bounded** surrounding-text queries: a
request names a range relative to the caret and a maximum length, and the
implementation returns at most that much text plus the offsets it actually
covers. There is no operation that returns the whole buffer.

Requests that would exceed the bound are satisfied with a truncated range and
honest offsets rather than refused, because an IME that gets less context still
composes correctly, and one that gets an error does not.

### Migration, with Writer as the gate

`StandardEdit`, `StandardRichEdit`, `AndroidInputCoordinator`, and
`BroilerInputConnection` migrate to the bounded contract. The compatible shape
is chosen so existing controls keep working: a small buffer answers a bounded
query by returning everything it has.

Writer regression tests are the gate on this migration. The migration is not
complete because the new contract compiles; it is complete because Writer's
existing IME, clipboard, and editing behaviour is unchanged.

### `UiSemanticRole.CodeEditor`

A new semantic role, with a virtualized text-range query and action contract:

- read a bounded range by character offset, returning the text and the range
  actually covered;
- read and set caret and selection by offset;
- navigate by line, with the line count available without materializing lines;
  and
- apply an edit to a range.

Every range operation names the snapshot version it was composed against and is
**rejected when stale**, exactly as edit transactions are. A screen reader
holding a range across an edit gets a rejection it can retry, not text from a
document that has moved.

No operation on this role returns the whole document.

### Testing without the bridges

The virtualized contract is tested independently of any platform accessibility
bridge: bounded range reads, caret and selection round-trips, edits, line
navigation, and stale-range rejection. This is deliberate — the platform bridges
do not exist yet, and a contract that is only exercised through them cannot be
verified until Phase 7.

A passing contract test is **not** a screen-reader support claim. Real
screen-reader support remains gated on the platform bridges in Phase 7, and on
validation with named screen readers.

## Consequences

- IME composition on a multi-megabyte source buffer costs a bounded query rather
  than a document copy.
- The accessibility contract can be built and tested in Phase 1, before the
  bridges exist, and does not have to be redesigned when they arrive.
- Existing controls and Writer are unaffected in behaviour, and the Writer
  regression suite is what proves it.
- Broiler.Input and Broiler.UI both change. This is a coordinated migration
  rather than a Broiler Code-local addition, and is called out as such so it is
  scheduled rather than discovered.

## Follow-up

Phase 1 implements the bounded contract, migrates the four named types, and adds
the virtualized-range tests. Phase 7 implements the platform bridges and does the
screen-reader validation that a support claim requires.
