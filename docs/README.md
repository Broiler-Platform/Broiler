# Broiler documentation

This directory contains documentation for the aggregate Broiler repository:
cross-component architecture, integration contracts, and work that must be
coordinated across more than one component. Component-owned design, API,
conformance, and implementation work belongs with that component.

## Authoritative documents

| Document | Purpose |
| --- | --- |
| [Root roadmap](ROADMAP.md) | The unfinished cross-repository work and its exit gates |
| [WebRTC implementation roadmap](webrtc-roadmap.md) | The cross-component architecture, security boundary, native-backend decision, phased delivery, conformance, interoperability, platform, and release gates for peer connections, data channels, and real-time media |
| [Broiler Code roadmap](broiler-code-roadmap.md) | The phased plan for a shared C#/.NET IDE, multi-project editing, diagnostics, and .NET/Android/WebAssembly builds |
| [Broiler.VM roadmap](../Broiler.VM/docs/roadmap.md) and [status](../Broiler.VM/docs/roadmap.status.md) | The new NativeAOT-compatible bytecode execution **core**: its profile contract, verification and resource boundary, lifecycle, static composition model, release gates, and authoritative current evidence ledger. Language profiles are separate components and are not planned there |
| [JavaScript modernization roadmap](../Broiler.JS/docs/roadmap/Modernization.md) | Current cross-track execution authority for JavaScript performance, assembly/AOT boundaries, package decomposition, compile-ahead, context isolation, Workers, profile-led optimization, and the JavaScript built-in VM profile |
| [JavaScript modernization delivery roadmap](../Broiler.JS/docs/roadmap/ModernizationDelivery.md) | **Subordinate delivery/reference view, not a sequencing or state authority.** Groups multi-phase waves, handoffs, and first increments under the authoritative modernization and owning plan/status gates |
| [Performance campaign crosswalk](../Broiler.JS/docs/roadmap/Roadmap.md) | **Moved into the `Broiler.JS` submodule on 2026-08-07.** Preserves the IL campaign, Octane/probe history, optimization catalogue, and phase crosswalk. Where sequencing conflicts, the modernization roadmap and the owning phase plan/status pair take precedence |
| [JavaScript concurrency plan](../Broiler.JS/docs/roadmap/Concurrency.md) | Component-owned MOD-M5–MOD-M7 gates for compile-ahead, optimizer/cache ownership, independent contexts, Worker agents, resource caps, and the separate shared-memory decision |
| [WPT rendering gaps](wpt-rendering-gaps.md) | Index for the worst-scoring WPT pixel mismatches, the capability each one is missing, and its owning component. **Split by verdict on 2026-08-13** into [not fixed](wpt-rendering-gaps-open.md), [won't fix](wpt-rendering-gaps-wont-fix.md) and [fixed](wpt-rendering-gaps-fixed.md); the index carries the shared warnings, the reproduction commands, and a table of every test |
| [WPT timeout causes](wpt-timeout-causes.md) | What the WPT `Timeout` category actually turns out to be — the per-iteration full-document pass that produced six of the seven causes in the 2026-08-16 run, which clusters are fixed, and the two whose cause is established but unfixed. A timeout is invisible to the pixel suites, since the run is aborted before anything renders |
| [Real-world website render tests](real-world-render-tests.md) | The public-site visual corpus, its live and recorded-DOM comparison lanes, metrics, CI workflow, and reproducible local command |
| [Privacy test pages](privacy-test-pages.md) | The DuckDuckGo privacy test page corpus: how a page's own `results` global is read back with `Broiler.Cli --evaluate-page` **and from the same page in Chromium**, what "carried out" means, how the two engines are compared probe by probe, the tracked baseline and what counts as a regression against it, and the CI workflow |
| [Privacy test page gaps](privacy-test-page-gaps.md) | What the two-engine comparison found: the probes Chromium answers and Broiler does not, grouped by the capability behind them, with the issues opened from each group and the probes no engine answers (and why those are not Broiler's) |
| [MediaWiki and the Vector 2022 skin](mediawiki-vector-rendering.md) | Why `https://www.mediawiki.org/` did not look like the same page in the reference browser, and the twenty-odd engine defects behind it — a `calc()` breakpoint that made 25 `@media` blocks malformed, floated flex items taken out of flow, a margin collapse spent twice, bold and italic that resolved to the regular face. 38.9 % → 82.7 % of pixels, with the remaining error accounted for: what it is made of, what is worth chasing in it, and what is not |
| [Capture diagnostics](cli-capture-diagnostics.md) | `Broiler.Cli --diagnostic-dir`: the JavaScript failure log, the archive of every page/script/sub-resource a capture touched, and the digest that ranks what failed and which platform features were missing |
| [Browser load-window pump](browser-load-window-pump.md) | Why the desktop browser hung on pages `Broiler.Cli` rendered: the render pump asked "are any timers queued" (never false on a page holding an interval) where the drains ask "is anything due in the load window", and re-parsed the whole document per tick on the UI thread |
| [Pooled-connection aborts](browser-connection-pool-aborts.md) | Why a browser window throws `IOException`/`SocketError.OperationAborted` (Windows: 995, "aborted because of either a thread exit or an application request") about a minute after a page loads, what it costs (nothing), and the `HttpClient`-per-navigation bug that used to add one per load |
| [Broiler Code architecture](architecture/broiler-code.md) | Source of truth for Broiler Code's capability boundary, workspace and diagnostic model, trust boundary, and build-worker contract |
| [Broiler Code budgets and support matrix](architecture/broiler-code-budgets.md) | The frozen performance budgets, the traces they are measured against, the recorded baselines, and what is still unmeasured |
| [SDK-project mutation matrix](architecture/broiler-code-project-mutations.md) | Which project constructs Broiler Code may edit, read losslessly, or only evaluate |
| [HtmlBridge architecture](architecture/htmlbridge.md) | Current bridge assemblies, ownership boundaries, and public seams |
| [Browser WebAssembly architecture](architecture/browser-webassembly.md) | Current browser-host, rendering, input, and support decisions |
| [Android application architecture](architecture/android.md) | Proposed Android host topology, platform baseline, and ownership boundaries |
| [Multithreading analysis and roadmap](architecture/multithreading.md) | Cross-component integration and historical host measurements. JavaScript-local implementation and acceptance are delegated to the component concurrency plan above |

Completed migration plans, delivery logs, and investigation journals remain
available in Git history. They are not the current backlog; durable decisions
and unresolved outcomes have been consolidated here or moved to the owning
component.

## Component ownership

The component roadmaps are the source of truth for component-local work:

- [Broiler.CSS](../Broiler.CSS/docs/roadmap.md)
- [Broiler.DOM](../Broiler.DOM/docs/roadmap.md)
- [Broiler.Documents](../Broiler.Documents/docs/roadmap.md)
- [Broiler.Graphics](../Broiler.Graphics/docs/roadmap.md)
- [Broiler.HTML](../Broiler.HTML/docs/roadmap.md)
- [Broiler.Input](../Broiler.Input/docs/roadmap.md)
- [Broiler.VM](../Broiler.VM/docs/roadmap.md) — new, planned NativeAOT-compatible bytecode
  execution core; a host for language profiles rather than a language, with a public
  source-level, closed-world profile contract and no runtime discovery. JavaScript and
  WebAssembly are the intended first profiles, each a separate component with its own roadmap;
  [current status](../Broiler.VM/docs/roadmap.status.md) is tracked separately from the plan
- [Broiler.JS](../Broiler.JS/docs/roadmap/) — **legacy component**, and the engine every
  current consumer uses. Indexed component-owned plans for modernization, the IL performance
  campaign and phases, assemblies, concurrency, and component capability work. It is not part
  of Broiler.VM's graph, gates, or evidence. Start with
  [`Modernization.md`](../Broiler.JS/docs/roadmap/Modernization.md) for cross-track order
  and [`Component.md`](../Broiler.JS/docs/roadmap/Component.md) for non-performance work
- [Broiler.JS performance acceptance](../Broiler.JS/docs/roadmap/Measurement.md) — the
  fail-closed evidence-class, A/A, candidate/control, source-identity, resource, and
  semantic gate for JavaScript-engine and harness performance claims
- [Broiler.Regex](../Broiler.JS/Broiler.Regex/docs/roadmap.md)
- [Broiler.Layout](../Broiler.Layout/docs/roadmap.md)
- [Broiler.Media](../Broiler.Media/docs/roadmap.md)
- [Broiler.UI](../Broiler.UI/docs/roadmap.md)

The component READMEs describe supported surfaces and standalone validation
commands. A root document should link to those component records instead of
copying their implementation checklists.

## Documentation rules

- Keep a roadmap item only while an outcome remains open. Remove completed
  implementation histories once any durable decision has been folded into an
  architecture or support document.
- Give every roadmap item an owner, current evidence, next action, and objective
  exit gate. A checked historical task is not release or conformance evidence.
- Put API and dependency rules beside the component that enforces them.
- Put generated test reports and comparison images under `tests/` or the ignored
  `artifacts/` directory, not under `docs/`.
- Record current support honestly. Local smoke evidence does not imply
  cross-browser, accessibility, hardware, security, or production support.

## Test evidence

Machine-consumed baselines belong beside their harnesses. Chromium reference
locks and focused conformance summaries are under
[`tests/m2-conformance`](../tests/m2-conformance/); durable WPT expected
failures are under [`tests/wpt-baseline`](../tests/wpt-baseline/). Generated WPT
and visual comparison output should remain reproducible from scripts and
workflows rather than being maintained as prose delivery journals — and should
not be committed at all, which is why the April 2026 WPT result snapshots and
the browser WebAssembly phase-0 baselines were retired. A committed baseline
that no harness compares against is a prose journal in a binary format.
