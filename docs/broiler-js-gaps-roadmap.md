# Broiler.JS gaps

- **Status:** Active
- **Scope:** Missing, incomplete, unsupported, or observably incorrect JavaScript behavior
- **Last reconciled:** 2026-08-25
- **Evidence basis:** Repository-wide Markdown audit plus the current component revisions

This document consolidates JavaScript gaps recorded anywhere in the Broiler repository, not
only under `Broiler.JS`. It therefore includes core ECMAScript behavior and JavaScript-visible
host, DOM, CSSOM, SVG, worker, and browser APIs. The implementation owner may be Broiler.JS,
HtmlBridge, Broiler.DOM, Broiler.CSS, or another component.

Execution speed, allocation, startup, tiering, caching, boxing, benchmark scores, and other
performance-only work are out of scope. Web Performance API defects remain in scope when the
problem is missing or incorrect observable behavior rather than speed.

This is a coordination roadmap, not a replacement for the owning documents or current failure
manifests. Where this document and an older investigation disagree, the current known-gap test,
current failure manifest, current component roadmap, and current source revision take priority.

## The set

This is the hub. Items live in one of four documents by status, so "what is left?" and "what did
we already settle?" are separate questions with separate answers:

| Document | Holds |
|---|---|
| [**open**](broiler-js-gaps-open.md) | Real gaps not started, and surfaces needing an explicit product decision |
| [**in progress**](broiler-js-gaps-in-progress.md) | Tracks part-landed, with the named steps still owed |
| [**closed**](broiler-js-gaps-closed.md) | Fixed, and retired-as-not-reproducing — with the evidence for each |
| [**won't fix**](broiler-js-gaps-wont-fix.md) | Landed-but-still-listed, deliberate deviations, product boundaries |

**One item, one document.** When an item is fixed or retired it is *removed* from **open** or
**in progress** rather than struck through in place, and its full record — root cause, what landed,
the evidence — moves to **closed**. So **open** answers "what is left?" with nothing else mixed in,
and no item has two entries that can drift apart. Where fixing something leaves a smaller thing
behind, only the remainder stays in **open**, stated on its own without the history.

## Where each track stands

Status is per item, not per track: a track's fixed items are in **closed** while its remaining
ones are in **open** or **in progress**. Each document keeps the track headings, so an item can be
found either by status or by track.

**"What is left" is the only thing this table states.** It deliberately does not list what has been
fixed — that grows without bound and goes stale the moment it is not updated, and it is already
recorded, with root cause and evidence, in [closed](broiler-js-gaps-closed.md). Read a row as the
work remaining on that track.

| # | Track | What is left | Lives in | Required outcome |
|---:|---|---|---|---|
| 0 | Conformance evidence | The pinned-corpus CI run, and product decisions for three `$262` hooks | in progress · closed | Test failures and timeouts are trustworthy |
| 1 | Core language and built-ins | A skipped nested-call statement in async functions, the Annex B remainder and the cross-realm cases; a `for await` fix awaiting its patch | open · in progress · closed | Supported Test262 language clusters are clean |
| 2 | RegExp | Matcher performance, then non-Unicode routing, then retiring the translator | in progress · closed | ECMAScript syntax and matching semantics use a complete backend |
| 3 | Scripts, tasks, and modules | Live import bindings; the ordered host task model; three capability decisions | open · in progress · closed | Parsing and task ordering match observable browser behavior |
| 4 | Workers and shared memory | The excluded Worker capabilities, concurrent-context safety, cross-agent shared memory | open | Claimed agent capabilities are complete and deterministic |
| 5 | Essential browser JavaScript APIs | Navigation semantics; storage and networking; Trusted Types | open · closed | A tested support matrix replaces accidental omissions |
| 6 | DOM, CSSOM, and SVG from JavaScript | Members onto the interface prototypes; canonical Shadow DOM; live SVG DOM; three capability decisions (`document.all`, per-tag SVG interfaces, Font Loading) | open · closed | Script-visible objects and algorithms meet their claimed standards |
| 7 | Graphics, media, and advanced APIs | Every surface — large capability decisions first | open | Each surface is implemented or explicitly excluded |
| 8 | Portable/Native-AOT profile | Everything past the numeric seed, and the profile decision that gates it | open | Optional profile decision and, if approved, a truthful capability set |

Tracks 1 and 2 can proceed in parallel once track 0 makes their results trustworthy. Tracks 3
through 7 share host and DOM dependencies and must use one published support matrix rather than
silently exposing partial globals.

## Status and closure rules

- **Confirmed gap:** currently reproduced, present in a current failure manifest, or retained by
  an explicit known-gap regression.
- **Coverage gap:** the runner or host cannot yet provide trustworthy conformance evidence.
- **Capability decision:** an absent platform surface that needs an explicit implement, defer, or
  unsupported-product decision.
- **Retest:** suspected, historical, or unreproduced behavior that is not asserted as a current
  defect.
- **Deliberate exclusion:** a documented profile or product boundary; it is not a Full-profile
  engine defect unless Broiler advertises the excluded capability.

A confirmed gap closes only when:

1. a minimal repository regression fails before and passes after the change;
2. the focused pinned Test262 or WPT path and the affected full shard pass;
3. the failure is removed from its manifest only after CI confirmation;
4. unsupported cases continue to fail deterministically rather than partially succeeding; and
5. the owning status document and publishable compliance evidence are reconciled together.

## Sources of truth

- [Broiler.JS known compliance gaps](../Broiler.JS/docs/compliance/known-gaps.md)
- [Broiler.JS component roadmap](../Broiler.JS/docs/roadmap/Component.md)
- [Broiler.JS compliance dashboard](../Broiler.JS/docs/compliance/dashboard.md)
- [Broiler.Regex implementation status](../Broiler.JS/Broiler.Regex/Broiler.Regex/README.md)
- [Broiler.Regex roadmap](../Broiler.JS/Broiler.Regex/docs/roadmap.md)
- [JavaScript concurrency plan](../Broiler.JS/docs/roadmap/Concurrency.md) and
  [status](../Broiler.JS/docs/roadmap/Concurrency.status.md)
- [Privacy-page API inventory](privacy-test-page-gaps.md)
- [HTML5 JavaScript exceptions](html5test-exceptions.md)
- [Open WPT rendering and API gaps](wpt-rendering-gaps-open.md)
- [Current xUnit suite status](xunit-suite-status.md)
- [DOM bridge roadmap](../Broiler.DOM/docs/roadmap.md)

Do not copy changing pass/fail totals into this roadmap. Link the exact result artifact or update
the dashboard instead.

## Completion gate

This roadmap is complete when:

1. every confirmed item is fixed or converted into an explicit, reviewed product exclusion;
2. supported Test262 and applicable WPT modes produce trustworthy, reproducible results;
3. no runner shim or shape-only stub is required to claim a supported JavaScript feature;
4. every unsupported global or method has deterministic detection and failure behavior;
5. the retest queue is empty or contains only dated, explicitly deferred investigations; and
6. the component roadmaps, support matrix, known-gap inventory, and compliance dashboard agree.
