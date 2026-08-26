# Changelog

All notable changes to the Broiler component packages are documented here. The
format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the
packages use [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Packages
are versioned in lockstep during the preview.

## [Unreleased]

### Fixed

- `scrollIntoView` moves the block axis in a vertical writing mode. In the main
  repository, so it is live.

  The block/inline to physical axis mapping was already right; what was missing
  sat under it. The scrollable-overflow measurement only looked toward larger
  physical coordinates — a descendant's right/bottom edge against the padding
  box's left/top — which finds nothing for an axis that runs the other way. A
  `vertical-rl` block axis runs right-to-left and its content overflows to the
  left, so `scrollWidth` came back equal to `clientWidth`: no extent, therefore
  no scroll range, therefore every block-axis `scrollIntoView` clamped to zero.

  A reversed axis now measures both ways and takes the larger, rather than
  simply measuring the other way, because the layout is not consistent about
  which side it mirrors — `direction: rtl` in a horizontal writing mode lays
  the overflow out to the right while `vertical-rl` lays it out to the left.
  A forward axis is measured exactly as before.

  `scrollWidth`, `scrollHeight` and the sign of the scroll range are corrected
  with it, so a `vertical-rl` or `sideways-rl` container reports the extent and
  the negative range a browser reports.

- A frame's scroll state, mutated by script, reaches the serialized `srcdoc`.
  In the main repository, so it is live.

  A nested browsing context's document is severed from the main tree, and the
  serialization pre-pass that records scroll offsets walks the main document
  element — so it never reached a frame. The offset itself was always recorded
  (the frame's `scrollTop` read back what `scrollIntoView` had set); it simply
  never reached the markup, so a capture showed every frame at its initial
  scroll position. The pass now runs once per materialised content document.

  The visual-viewport scale is deliberately not applied inside a frame: pinch
  zoom scales the frame's box as a whole, so scaling the offset it scrolled
  within itself would count the same zoom twice.

- `document.elementFromPoint` descends into an SVG, and an SVG shape reports a
  real `getBoundingClientRect`. Unlike the engine fixes above this is in the
  main repository, so it is live.

  An SVG child is not in the CSS box tree, so nothing below the `<svg>` root
  had a rect: every shape measured `0,0,0,0`, and hit testing — which skips an
  element with an empty rect — could not descend past the root, so
  `elementFromPoint` over a `<rect>` returned the `<svg>`.

  A shape's rect now comes from its own geometry attributes, composing the
  viewport's rendered origin, the `viewBox` transform and the accumulated
  `translate()` of the ancestor `<g>` chain. `getBoundingClientRect` and hit
  testing go through the same resolution, so they cannot disagree about where a
  shape is, and a group's rect is the union of its children's — which is what
  having shape rects at all makes possible.

  `rect`, `image`, `foreignObject`, `circle`, `ellipse`, `line`, `polyline` and
  `polygon` resolve exactly. `path` and `use` report no rect, as before, rather
  than a wrong one; a transform list containing anything but `translate()`
  leaves its subtree untranslated; `preserveAspectRatio` is modelled at its
  default. Each of those is pinned by its own test.

- Import attributes are enforced rather than parsed and dropped. Delivered as
  `patches/0004-js-import-attribute-enforcement.patch` against `Broiler.JS`,
  so it is **not live until the patch is applied**.

  `with { type: 'json' }` — the portable form, and the only one a browser
  accepts on a JSON module — was accepted and ignored, and so was
  `with { flavour: 'nonsense' }`. Nothing read the parsed attributes, the
  `export … from` forms discarded theirs, and the loader was passed only the
  specifier.

  An unknown attribute key and a duplicate key are now early `SyntaxError`s on
  a static declaration, where the keys are literals. An unknown module type,
  and a `type` that does not match the module it resolves to, are load-time
  `TypeError`s, because only the module can settle them. A dynamic `import()`
  reports both as `TypeError`s, since its keys are a runtime value — and it
  now validates its options object properly (the options an object, `with` an
  object, every value a string). Each error kind and message matches a browser.

  `type: 'css'` is reported as a module type this engine does not implement
  rather than as a typo, so a page can tell the two apart.

  One divergence is deliberate: a `.json` module imported with no attribute
  still loads. On the web the attribute defends against a server returning
  JSON where script was expected, which cannot arise in a host whose locally
  resolved key is itself the type — and the same host serves `require`, which
  has no attributes at all.

- `import d from './data.json'` is the parsed value rather than `undefined`.
  Delivered as `patches/0002-js-json-module-default-export.patch` against
  `Broiler.JS`, so it is **not live until the patch is applied**.

  One wrapper served both callers — `module.exports = <json>` — which is what
  CommonJS `require` wants and exactly wrong for ESM: a default import reads
  `.default` off the parsed value and finds nothing, so every JSON import was
  `undefined` and JSON modules were unusable. A file whose whole content is
  `null` threw on load instead, because the exports setter refuses null.

  The two specifications disagree about what a JSON file exports and one
  object cannot satisfy both, so the module now stores the ES namespace —
  `{ default: value }`, the one export ES2025 gives a JSON module — and the
  CommonJS view unwraps it. That is also what makes arrays, numbers, strings,
  booleans and `null` work, none of which can carry a `default` property.

  One behaviour is deliberately removed: `import { a } from './x.json'` used to
  read `a` off the parsed object and is now `undefined`. A JSON module has no
  named exports; the spec makes the form a link error, browsers and Node both
  reject it, and raising the link error here would need whole-module analysis
  the engine does not do.

- A method call whose argument is a method call is no longer silently skipped
  inside an `async` function or a generator. Delivered as
  `patches/0001-js-nested-member-call-clobbers-outer-call.patch` against
  `Broiler.JS` — the push to that repository is outside this environment's
  GitHub scope — so it is **not live until the patch is applied**, and no
  main-repo fallback is possible for it.

  `trace.push(items.join('+'))` did not run. No error was raised and the
  statement after it ran normally, so the failure was silent;
  `console.log(list.join(', '))` is the same shape.

  A member call's receiver and resolved method live in two temps taken from a
  per-function pool, and the arguments are compiled before those temps are
  acquired, so a nested call in the arguments is handed the same two back.
  Ordinary code survives that because both values are on the evaluation stack
  by the time an argument runs. A generator or async body does not: the
  rewrite hoists any block-valued operand's statements out to statement level,
  and a nested member call compiles to exactly such a block — so its two
  assignments land between the outer call's assignments and its invocation,
  and the outer call goes to the inner callee.

  The guard that was supposed to prevent this looked in the source for an
  `await` or a `yield`, which is how the bug was first found but not what
  causes it. It now asks what the operands compiled to: an operand that is a
  bare parameter or a constant emits no statements and cannot be hoisted, so
  the pool stays safe; anything else gets locals of its own. Ordinary
  functions still pay nothing.

- `for await…of` no longer deadlocks the agent when the iterator's `next()`
  hands back a promise that is not already settled. The fix is upstream in
  `Broiler.JS` and the pinned pointer carries it, so it is live.

  Async iteration unwrapped the result of `next()` with a blocking wait on the
  one thread allowed to run a context's JavaScript. That works only while the
  promise is already settled, which is why every shape the engine's suite
  covered passed: an array, an async generator, an iterator returning
  `Promise.resolve(record)`. The moment `next()` returns the ordinary
  `something.then(…)`, the job that would settle it can never run, because the
  queue that runs it drains on the way out of the execution the thread is stuck
  inside. The agent hung until the process was killed — which is why it never
  showed up as a failing test.

  The step is now three pieces with the state machine's own `await` between
  them, so nothing blocks. Seven regressions come with it, each of which hung
  rather than failed before.

### Added

- `import.meta`, which was a SyntaxError ("import.meta not supported").
  Delivered as `patches/0003-js-import-meta.patch` against `Broiler.JS`, so it
  is **not live until the patch is applied**.

  It carries `url` — the module's own absolute URL, so a transitively imported
  module reports its own rather than the entry point's — and is otherwise an
  empty, extensible, null-prototype object created once per module, so
  `import.meta === import.meta` and a module can hang its own state off it.
  Outside module code it stays an early SyntaxError, which is what a
  `try { eval('import.meta') }` feature-detect expects to see.

  `import.meta.resolve` is deliberately absent. The module resolver is
  existence-based — it probes for the file and answers null when nothing is
  there — while `resolve` is specified to hand back a URL whether or not
  anything is at it, so building it on today's resolver would throw where a
  browser answers. A page can feature-detect the absence; it cannot detect the
  wrongness.

- `ReadableStream` async iteration: `values()` and `@@asyncIterator`, so
  `for await (const chunk of response.body)` works. These were written with the
  stream but held back on the `for await` fix above, because an iterator over a
  stream returns exactly the unsettled shape that used to hang the agent. With
  that fix live they are installed, and seven regressions in
  `ReadableStreamTests` cover chunk order, lock release, early exit cancelling
  the source, `preventCancel`, an errored stream throwing into the loop, and a
  locked stream rejecting — each against Chromium's measured answer.

- `ReadableStream` (with its default reader and controller), `FileReader`,
  `ProgressEvent`, and `Blob.prototype.stream()`.

  Two of the File API slice's capability decisions, taken together because they
  are one: `stream()` was left out precisely because it returns a
  `ReadableStream`, and the engine had only a partial one — the shape-only
  object `response.body` handed back, carrying a `getReader` whose reader had
  `read`, `cancel` and `releaseLock` and nothing else. There was no `closed`, no
  `tee`, no `cancel` on the stream, and no constructor, so
  `new ReadableStream(...)` was a `ReferenceError` — the kind that aborts the
  script rather than the statement. `FileReader` was absent outright, so the
  standard way a page turns a dropped file into text, a preview data URL or an
  `ArrayBuffer` was a `ReferenceError` too.

  The stream is written in JavaScript, as an embedded asset beside the
  content-rendering polyfills, because the specification is: a queue, a list of
  pending read requests and a pull signal that must not re-enter. `blob.stream()`,
  a fetch body and a page's own `new ReadableStream` all hand back the same
  interface now. A fetch body reports its `bodyUsed` from the underlying source's
  `pull` rather than from a wrapper, so the stream a page holds still has no own
  properties, and `text()`/`json()`/`clone()` refuse a body that is disturbed
  *or* locked.

  Not implemented, and detectably so: `pipeTo`/`pipeThrough` (they need a
  `WritableStream`), BYOB readers (they need a byte-stream controller), and async
  iteration — that last one blocked on the engine rather than the stream, since
  `for await` never completes here even over a plain array. Installing the hook
  would turn `for await (const chunk of response.body)` from a `TypeError` a
  script survives into a capture that never settles.

- Three of `navigator`'s object-valued surfaces: `storage` (`StorageManager`),
  `permissions` (`Permissions`/`PermissionStatus`) and `userAgentData`
  (`NavigatorUAData`).

  Each is a whole API rather than a value, so each needed its own decision:
  whether a present object answers a page's `'x' in navigator` detection *more*
  misleadingly than absence does — the test that kept `speechSynthesis` and
  `navigator.bluetooth` out. These three pass it because the question the
  interface exists to answer is one Broiler can answer truthfully.

  `navigator.storage` reports quota-managed storage — IndexedDB, the Cache API,
  the origin private file system — of which Broiler implements none, so the
  honest estimate is `{usage: 0, quota: 0}`. That is the same pair the
  already-present `navigator.webkitTemporaryStorage` reports for the same
  question; the two disagreed only by one of them being absent. `getDirectory()`
  is deliberately not on it, because the origin private file system's
  feature-detect is exactly `'getDirectory' in navigator.storage`.

  Every permission query answers `"denied"`, which is the state
  `Notification.permission` already reports and the honest one: Broiler grants no
  permission-gated capability and has no surface to prompt on. Chromium answers
  `"prompt"` — a promise of a dialog that never comes here.

  `navigator.userAgentData` is derived entirely from the one
  `BroilerUserAgent.Value` string, so the structured identity and the string
  cannot disagree.

  `navigator.connection`, `.mediaDevices` and `.mediaCapabilities` stay absent,
  now as recorded decisions rather than omissions: `NetworkInformation` has no
  "not known" state, so any value would be an invention, and the two media
  surfaces belong with the rest of media.

- Form-associated custom elements: `static formAssociated`, `attachInternals()`,
  and the `ElementInternals`, `ValidityState` and `CustomStateSet` interfaces.

  This is the last of the three capabilities the Custom Elements slice named and
  left out. `attachInternals()` was undefined, so the line every such
  component's constructor opens with — `this.internals_ =
  this.attachInternals()` — was a `TypeError` that took the constructor down,
  and with it the upgrade of every instance on the page. A component could sit
  inside a form; it could not *be* a control.

  The members live on the prototypes with per-instance state in a weak table, so
  an instance carries no own properties — the shape `Range`, `Selection` and
  `Blob` established. Every form-related member refuses on an element whose
  definition did not declare `formAssociated`, rather than answering an empty
  value: answering `null` for `form` there would say "this control has no form"
  where the truth is "this is not a control".

  **`setFormValue` is not a shape-only stub, and making that true needed the
  form entry list.** `new FormData(form)` enumerated the *wrapper's* own string
  properties, so it produced the element object's members — `tagName`,
  `innerHTML` and the rest — instead of the form's fields. That is also where a
  browser reads a form-associated custom element's submission value. The entry
  list is built properly now, with the specified exclusions, and a `FormData`
  submission value contributes its own entries.

  `formAssociatedCallback`, `formDisabledCallback` (which an ancestor
  `<fieldset disabled>` triggers as much as the element's own attribute) and
  `formResetCallback` all fire. `formStateRestoreCallback` deliberately never
  does: it reports a value restored by session history or an autofill pass, and
  this engine performs neither.

- Customized built-in elements — the `extends` option, the `is` value, and the
  `document.adoptNode` and `adoptedCallback` pair.

  The Custom Elements slice deliberately left both out and said so: `define`
  rejected an `extends` option with a `NotSupportedError` rather than accepting
  it and ignoring it, and `adoptedCallback` never fired because there was no
  document-adoption path to fire it from. So the idiom for keeping a native
  control's behaviour and adding to it — `class Fancy extends
  HTMLButtonElement` — lost its component at the `define` call, which takes the
  rest of the script with it.

  Each per-tag interface global (`HTMLButtonElement`, `HTMLInputElement`, all of
  them) is now a real constructor rather than an unconditional throw, because a
  customized class reaches one through `super()`. It is still not directly
  constructible: without a `new.target`, or through a class no definition names,
  it throws the same `Illegal constructor` it always did. The interface a
  `super()` goes through is passed to the registry rather than inferred, which is
  what makes the two ways of mismatching a class and its definition report the
  way a browser reports them instead of silently building the wrong element.

  An element's **is value is not its `is` attribute**, and both halves are
  measured: an element parsed from `<button is="fancy-b">` has both, while `new
  FancyButton()` and `createElement('button', {is: 'fancy-b'})` produce one whose
  `getAttribute('is')` is `null` — and which still serializes as `<button
  is="fancy-b">`, because HTML §13.3 writes the is value out so the markup
  re-parses into the same element. An `is` naming nothing defined is kept too, so
  a later `define` upgrades it.

  `document.adoptNode` had no implementation at all, and `importNode` is not a
  substitute: importing copies, so the node a page holds afterwards is a
  different one. Adoption moves the node itself, which is why it is the operation
  a custom element can observe. Both directions are heard — adoption publishes on
  the document a node moves *to*, so every document this bridge mints is
  subscribed, not only the page's.

  Two fixes fell out of the same work. A class statically inherits
  `@@hasInstance` from the interface it extends, so `class Fancy extends
  HTMLButtonElement` reported *every* `<button>` on the page as one of its own
  instances; a subclass now gets the ordinary prototype-chain answer. And
  `connectedCallback` tested for the page's document specifically, so an element
  inserted into a frame's or a `createHTMLDocument`'s tree was never connected.

- The File API data surfaces: `Blob`, `File`, `FileList`,
  `URL.createObjectURL`/`revokeObjectURL`, and a file input's `files`.

  All of the interfaces were undefined, so the bare name was a `ReferenceError`
  — the kind that aborts the script rather than the statement. `Blob` is reached
  by ordinary pages and not only by upload code: it is how a page builds a
  downloadable payload (`URL.createObjectURL(new Blob([csv], {type:
  'text/csv'}))`), how it posts binary through `fetch`, and what
  `response.blob()` is supposed to return.

  `input.files` read `undefined` on a file input *and* on every other control,
  where a browser gives a `FileList` and `null` respectively — so the standard
  guard `if (input.files && input.files.length)` was a `TypeError` on the very
  input it is written for. The list is empty, because there is no file
  selection, which is exactly what a browser reports for an input nobody has
  touched.

  `File` really extends `Blob` through the prototype chain, and `FileList` joined
  the existing indexed-collection machinery rather than getting a second one.

  Three behaviours are worth knowing because reasoning gets them wrong: the parts
  argument is a Web IDL sequence, which deliberately does not accept a string, so
  `new Blob('abc')` throws rather than making a three-byte blob; a `type`
  carrying a character outside printable ASCII is discarded entirely rather than
  kept or escaped; and `slice` gives its result an empty type rather than
  inheriting the source's.

  `Blob.prototype.stream()` is deliberately absent rather than stubbed: it
  returns a `ReadableStream`, and this engine already carries one partial stream
  — the object `response.body` hands back — that a second copy should not be
  written against.

- `window.getSelection()` and `document.getSelection()`, the `Selection`
  interface behind them, and `StaticRange`.

  None of the three existed, so `window.getSelection` read `undefined` and the
  bare `Selection` and `StaticRange` were `ReferenceError`s — the kind that
  aborts the script rather than the statement. That reaches ordinary pages and
  not only editors: the copy-to-clipboard idiom every page shares is
  `sel.removeAllRanges(); sel.addRange(range)`, and
  `window.getSelection().toString()` is how a page reads what was picked.

  Broiler has no user input and so no *user* selection — which is exactly the
  state a browser is in on a freshly loaded page: `rangeCount` `0`, `type`
  `"None"`, `anchorNode` `null`. Everything a script then does to it —
  `addRange`, `collapse`, `extend`, `setBaseAndExtent`, `selectAllChildren`,
  `containsNode`, `deleteFromDocument` — has an answer that does not depend on a
  user, and that scripted half is implemented. What stays absent is the half
  that does not: nothing populates the selection on its own, no
  `selectionchange` fires, and the selection is not painted.

  The selection holds the page's own range object, so `sel.getRangeAt(0) === r`
  after `addRange(r)` and a later edit of that range moves the selection. A node
  or range in another tree is silently ignored rather than rejected — and a
  detached tree counts, so collapsing into an element built but not yet inserted
  does nothing — while an out-of-range offset or a doctype in that same argument
  still throws. A frame's document and window get their own selection; a
  `createHTMLDocument` result has no browsing context and so answers `null`, as
  a browser does.

  `Selection.modify()` and `getComposedRanges()` are deliberately absent rather
  than stubbed: the first moves the selection by character/word/line and needs a
  text-segmentation model this engine does not have, the second is shadow-tree
  composition. A page feature-detecting either takes its fallback, where a stub
  would claim a movement that silently does nothing.

  `StaticRange` shares `AbstractRange`'s boundary attributes with `Range`,
  holding four values captured at construction where a range holds a live
  boundary — so a tree mutation moves one and not the other.

- `AbstractRange` and `Range` are real DOM interfaces, with the five operations
  that were missing from them and the exceptions their arguments owe the caller.

  `Range` was not a global at all, so `typeof Range` was `"undefined"` and both
  `new Range()` and `r instanceof Range` were `ReferenceError`s — the kind that
  aborts the whole script, not just the line that asked. A range's
  `constructor.name` was `"Object"`, and its 29 members were its own properties,
  so `Range.prototype.setStart` had nothing to be. Both interfaces are now
  registered, `new Range()` builds a range over the document, and every member
  lives on a prototype: a range's boundaries are held in a weak table keyed by the
  range object, so a prototype method finds its own state from its receiver and
  the instance is left with no own properties at all, as in a browser. Calling one
  on a foreign receiver is a `TypeError` rather than a wrong answer. The boundary
  attributes go on `AbstractRange`, which is where a browser has them.

  `comparePoint`, `isPointInRange`, `intersectsNode`, `createContextualFragment`
  and `detach` did not exist and now do.

  The argument checks that were absent are in. An offset past the container's
  length raises `IndexSizeError` instead of being clamped into the node — the
  clamp was the quiet failure of the set, since it left the range pointing
  somewhere else and surfaced later as a wrong extraction. A missing or non-`Node`
  argument raises `TypeError` instead of returning `undefined`; `selectNode` on a
  parentless node raises `InvalidNodeTypeError` instead of doing nothing;
  `selectNodeContents(doctype)` raises a `DOMException` instead of escaping as a
  bare `Error` with a .NET stack trace; `compareBoundaryPoints` distinguishes an
  unknown comparison method (`NotSupportedError`) and a source range in another
  tree (`WrongDocumentError`) from a legitimate `0`; and `insertNode` no longer
  puts a doctype inside a paragraph.

  Every expectation is a browser's measured answer over one probe corpus rather
  than a reading of the specification, which is what pins two that reasoning gets
  wrong: `setStart(node, -1)` is an `IndexSizeError` and not a `TypeError`,
  because Web IDL turns `-1` into `4294967295` first, and by the same conversion
  `compareBoundaryPoints(3.7, r)` is accepted while `4` is rejected.

- The PDF read-preview integration candidate: Broiler Writer can open PDFs on
  Windows and Linux.

  The Writer used to hard-code its codec catalog and its two file-dialog filter
  arrays, so every head got exactly the same formats and adding one to the shared
  core added it everywhere — including the Android and WebAssembly Writers, whose
  package-size, memory, trimming and AOT gates PDF has not passed. Composition
  roots now build a `WriterDocumentFormats` instead: `CreateDefault()` is the RTF,
  DOCX, HTML and Markdown set every head has always had, and the Windows and Linux
  heads add `PdfDocumentCodec` to their own. Nothing else changes for the heads
  that do not ask for it, and `PdfDeliveryGuardTests` fails the build if the codec
  is named anywhere but those two roots.

  Registration carries a capability, separately from what the codec implements.
  PDF is registered for opening only: its writer exists and passes its own tests,
  but PDF export has its own release gate, so there is no PDF save filter and the
  save dispatch — which reads the same set the filters come from — has no PDF
  entry, whether the user picks the format or types the extension.

  The desktop open path moved onto `DocumentCodecCatalog.SelectAndRead` over a
  `DocumentInput`, so probing and reading observe the same bytes without a second
  buffer, and the read's own limits decide how much of a file enters memory rather
  than `File.ReadAllBytes` materializing it first and measuring after. Two reads
  that used to replace the open document no longer can: a rejected one, which
  carries a placeholder rather than content, and one that recovered no text at all
  and knows it is incomplete — a scanned PDF with no text layer is that shape,
  where "empty" is a report about the reader rather than about the file. Both
  leave the editor alone and say why in the status bar. A partial read that did
  recover text is committed and reported as partial, never as a clean open.

- The Phase 1 §6.1 document contracts in `Broiler.Documents`.

  `DocumentInput` is the load-bearing one. Choosing a codec means reading a
  prefix and then reading the document means reading that prefix again — a
  rewind on a file, and impossible on a pipe or a browser upload, where the
  usual fix is to buffer the whole source before probing and so turn a bounded
  read into an unbounded one. `DocumentInput` holds the probed prefix and
  replays it ahead of the rest, so a non-seekable source is probed and read
  exactly once. Ownership is explicit (a caller's stream is not closed unless it
  says so), and materialization is memory-only and always bounded: nothing opens
  a temporary file or consults a temp directory, which keeps it usable in a
  memory-only WebAssembly host and keeps document bytes out of any location with
  no agreed retention policy.

  `DocumentReadRequest`/`DocumentWriteRequest` carry the input, typed options,
  limits and cancellation with exactly one owner per value — cancellation moved
  off the PDF options onto the request for that reason. `DocumentCodec` gains
  request and async entry points; the stream methods stay, and are what a codec
  implements, so every existing codec keeps working through the new contract
  unchanged. No adapter uses `.Result` or `.Wait()` in either direction.

  `DocumentResultStatus` and `DocumentDestinationState` moved out of the PDF
  package onto the shared results, where they were always format-neutral, and
  every codec now reports them. Status and severity answer different questions:
  a read can carry a dozen warnings and be complete, or carry none and be
  missing a page, so hosts branch on the status rather than on a diagnostic
  count. `DocumentCodecCatalog.SelectAndRead` is the one path that probes and
  reads through the same replayable input, and diagnostics can now carry an
  optional source location.

- A required `Documents And PDF` CI workflow — the first pull-request-triggered
  workflow in the repository. Every other one is `workflow_dispatch` or
  tag-push, so the Documents solution had no CI at all and its suites were only
  ever a local result. It builds and tests the solution on Windows and Linux and
  runs the Graphics and Media image suites, which are console runners rather
  than xunit projects: `dotnet test` exits 0 against them having run nothing, so
  they are invoked with `dotnet run` where the exit code is the failure count.
  The xunit half is checked against a floor on executed tests, because a run that
  silently executes almost nothing should fail rather than report success. No
  independent oracle is wired in — qpdf, PDFium, Poppler, MuPDF and veraPDF each
  need a licence review and a tool-manifest row first — and the CLI/Writer host
  jobs activate at the roadmap's Phase 5/7 boundaries along with the paths that
  would trigger them.
- A [PDF construct inventory](Broiler.Documents/docs/pdf-construct-inventory.md):
  exactly which PDF constructs the codec reads, writes, recognizes without
  interpreting, and rejects, each with the file that implements it. It exists to
  scope the IP-001 qualified review, which asks whether a *concrete
  implementation* falls within Adobe's ISO 32000-1 patent licence — a question
  that had no definite subject until the base slice existed and now does. It
  cites clauses without reproducing standard text, and marks the clause column
  provisional, since lawful standards access is still an open Phase 0 item.

- `Broiler.Documents.Pdf`, a base PDF codec: logical text import from ISO 32000-1
  files and a deterministic PDF 1.7 writer. It is not a published capability —
  the package is not packed, and the only applications registering it are the
  Windows and Linux Writer heads, for opening, as the read-preview integration
  candidate described above — and the
  [feature matrix](Broiler.Documents/docs/pdf-feature-matrix.md) remains the
  authority for what it may be described as.

  The reader covers PDF syntax and object stores, classic cross-reference tables,
  cross-reference streams, object streams, hybrid `/XRefStm`, incremental `/Prev`
  chains and a reported scan-based recovery path; the catalog and page tree with
  inherited attributes; effective version resolution with `/Extensions`
  inventoried but never enabling behavior; `Info` metadata normalized to a fixed
  allowlist; content interpretation covering the text and graphics state, all
  show-text operators, Form XObjects, inline images and marked-content
  `ActualText`; simple and composite font mappings through encodings,
  `/Differences` and `ToUnicode` CMaps; geometric assembly into columns, lines,
  paragraphs and lists; and link annotations admitted by a shared URI policy that
  performs no I/O. The writer resolves layout once into an immutable page list
  that the serializer consumes without re-measuring, and emits new files using
  the standard font names with WinAnsi encoding and no embedded program, so its
  output is byte-identical across runs and reads no clock, machine name, locale
  or installed font.

  What it deliberately does not do is the more interesting half. Only Flate,
  ASCIIHex, ASCII85 and RunLength are implemented — everything with an open
  IP-register row (LZW, JPEG, CCITT, JPEG 2000, JBIG2), along with embedded font
  programs, image extraction and encryption, is *recognized* and skipped with its
  own stable diagnostic, so a host can tell a policy decision from a corrupt
  file. Each arrives later by composing a reviewed implementation into
  `PdfCodecServices`, with no change to the parser, the interpreter or the
  writer; see
  [PDF extension points](Broiler.Documents/docs/pdf-extension-points.md). The
  package therefore carries no third-party runtime dependency and bundles no
  font, glyph list, metric file or codec asset: its encoding tables and metric
  model are authored here, and Flate calls the runtime.

  Encryption is settled from the trailers before any content-bearing object is
  resolved, so an encrypted document is rejected without a single
  decrypt-dependent object having been interpreted. Every budget is checked
  before the allocation it guards, and exhausting one rejects the document rather
  than truncating it. Diagnostics carry a code and a reason and never document
  text, a metadata value or a path.

- Fifteen platform surfaces a page probes and Broiler had none of. Each was an
  absence a script could not survive rather than one it could branch on: reading
  `performance.memory.usedJSHeapSize`, calling `video.canPlayType(type)` or
  `navigator.getGamepads()`, iterating `navigator.plugins` — all threw, and a
  throw aborts the whole function that asked, not the statement that asked. The
  answers below are what the engine can honestly give, which for most of them is
  a negative one stated in the interface's own vocabulary.
  - `performance.memory` and `console.memory` — one `MemoryInfo`, read from the
    GC that is actually executing the page's script and rounded to 100 KiB.
    Chrome bucketizes the same numbers for the same reason: an exact heap size is
    a high-entropy value a page can sample repeatedly to identify a browser.
  - The document's `PerformanceNavigationTiming` entry, which the Performance
    Timeline getters now have something to return — including
    `nextHopProtocol`, which names the HTTP version Broiler's loaders actually
    negotiate (`Broiler.Layout.Net.BroilerHttpProtocol`, now pinned on both
    clients rather than assumed) and is empty for a document that had no network
    hop. The entry carries no timing marks: Broiler's loader does not record
    them, and zeroes would let a page difference two of them into a plausible
    `0 ms` that no feature test could tell from "not measured".
  - `screen.orientation`, derived from the screen's own shape so it cannot
    contradict the `width`/`height` beside it.
  - `Notification`, whose permission is settled at `denied` — the terminal
    state, and the true one, since there is no surface a notification could be
    shown on. `default` would instead invite a page to wait on a prompt that
    cannot appear.
  - `MediaSource` and `HTMLMediaElement.canPlayType()` on `<video>`/`<audio>`,
    answering `false` and `""` for every type. Those are the specified values for
    "cannot be rendered", which is the state of an engine with no media playback
    pipeline wired to its HTML layer, and they are what a player needs in order
    to take its fallback rather than wait on a video that will never arrive.
  - `navigator.javaEnabled()`, `navigator.getBattery()`,
    `navigator.getGamepads()`, `navigator.plugins` / `navigator.mimeTypes` /
    `navigator.pdfViewerEnabled`, `navigator.requestMediaKeySystemAccess()`, and
    the legacy `navigator.webkitTemporaryStorage` /
    `webkitPersistentStorage.queryUsageAndQuota` pair, which report the
    quota-managed storage Broiler has none of.
  - Found by the privacy test pages' Chromium comparison, which is what turned
    "Broiler produced nothing here" into a list with a known expected shape; see
    `docs/privacy-test-page-gaps.md` for the six absences in the same group that
    are deliberately **not** stubbed, among them WebRTC and the generic sensors.
- The desktop applications use the machine's clipboard. Broiler.UI has always had
  a clipboard *port* — `IUiClipboardHost`, which every text control copies and
  pastes through — but on Windows and Linux nothing was plugged into it: the
  Browser and Writer hosts answered from a private string, so copying in Broiler
  and pasting into a terminal produced whatever had been copied before, and text
  copied anywhere else could not be pasted in. (Android and the WebAssembly
  Writer were already wired to `ClipboardManager` and the browser's clipboard
  events; Code on Windows already had the Win32 one.)
  - `Broiler.App.LinuxX11Clipboard` — the X11 CLIPBOARD and PRIMARY selections.
    X11 has no clipboard daemon holding a string: the application that copied
    last owns a selection and every paste is a request answered by that owner, so
    this owns one on a display connection of its own and answers
    `SelectionRequest` from the head's existing loop. It offers and accepts
    `UTF8_STRING`, `STRING` and `TEXT`, answers `TARGETS`, reads a chunked `INCR`
    transfer from owners that send one, and gives the selection up when the
    process exits rather than leaving other applications to find a dead owner.
    PRIMARY goes with CLIPBOARD, so a middle-click paste into a terminal gets the
    same text Ctrl+V does. It needed no Broiler.Graphics change: draining the
    window's own queue for this would swallow the focus, resize and close events
    the surface is waiting for.
  - The Win32 clipboard moved from the Code head to `Broiler.App` and is now
    shared by Browser, Writer and Code alike rather than existing once and being
    reachable only from Code.
  - Broiler Code on Linux consequently reports `clipboard: native` from
    `--services` and enables Cut, Copy and Paste, where it previously declared
    the service unavailable because "X11 selection ownership is not implemented".
  - Where a machine has no clipboard to offer — no X display — the commands
    report themselves unavailable. There is deliberately still no in-process
    buffer standing in for one: that is the thing that made copy and paste look
    like they worked while interoperating with nothing.

- `Broiler.UI.Edit` — an editing context menu on the single-line edit, plus the
  clipboard shortcuts that were missing next to `Ctrl+C`/`X`/`V`: `Ctrl+Insert`
  copies, `Shift+Insert` pastes and `Shift+Delete` cuts, the chords several
  editors and terminals still send. Right-clicking the field — or pressing
  `Shift+F10` or the menu key — offers Undo, Cut, Copy, Paste, Delete and Select
  All, each enabled against the state the menu opened with: Paste only when the
  clipboard holds text a single-line field would actually insert, and a password
  field offers neither Cut nor Copy, the rule that already made `Copy()` refuse.
  A right-click inside the selection keeps it, since that is what Cut and Copy
  act on, while a click outside moves the caret there first. The menu is drawn by
  the control the way the ComboBox draws its drop-down rather than composed from a
  `UiMenu`, so an edit still costs one assembly and no host has to hand a menu to
  every field it creates; while it is open the edit holds the input capture, so
  the text behind it cannot be typed into, and it appears in the semantic tree as
  an expanded menu. A host that would rather show a platform menu runs the same
  commands through `StandardEdit.InvokeContextMenuCommand`.
- `Broiler.Layout` — CSS Images 3 §5.5 `object-fit` and §5.6 `object-position`,
  which nothing read: every replaced element was stretched to its content box, so
  `object-fit-contain-svg-001i`, `-fill-svg-001i` and `-none-svg-001i` rendered to
  byte-identical PNGs. `IR.ObjectFitPlacement` resolves the concrete object size and
  where it sits, clipping to the content box when it overflows, and
  `IR.CssPositionValue` reads the whole `<position>` grammar — including the three-
  and four-component edge-offset forms (`top 25% left 25%`), which paint dropped
  silently and which `background-position` and gradient layers now share. `css/css-images`
  goes 234 → 262 of 460 reftests with no losses; the 42 `object-fit-*i` tests go 21 → 42.
  `ImageIntrinsics` reports the intrinsic aspect ratio and whether its size is real
  alongside them, since an SVG with only a `viewBox` has a ratio and no size.
- `Broiler.Wpt` — a WPT `-print` reftest now renders the paint its own `@page`
  rule puts on the sheet (CSS Paged Media 3 §7): the page background over the
  whole page box, margins included, and the page's border and padding on the box
  the margins leave, with the flow inset by them. Independent of the
  `BROILER_WPT_PAGED_PRINT` lever, because a page paints whether or not the flow
  is paginated — `css-page/page-background-image-print` states outright that its
  background should print and not show on screen. A page whose root element
  generates no box paints nothing at all, and `visibility` applies in the page
  context. `css/css-page` goes 133 → 138 of 224 reftests, nothing lost; documents
  that declare no page paint render byte-identically to before. (137 of those came
  without the one-line `Broiler.HTML` call site below; that is upstream and pinned
  now, so `page-box-002-print` passes and the 138th is in.)
- `Broiler.Layout` — CSS 2.1 §11.1.2 `clip`, the legacy rectangular clip on an
  absolutely positioned element. Resolved into the `clip-path: inset()` that names
  the same operation (`IR.ClipRect`, in `ComputedStyleBuilder`, where the used
  border box is known), so nothing downstream needs a second clip; a real
  `clip-path` supersedes it, per CSS Masking 1 §7. Together with the `Broiler.HTML`
  change that stops an empty `inset()` being dropped as if it were no clip
  (upstream and pinned), `css/CSS2/visufx` goes 6 → 50 of 51 reftests and
  `css-masking/clip` gains two, none lost.
- `Broiler.Wpt` — inline scripts in an XHTML test are unwrapped from their XML
  CDATA section before execution. `<![CDATA[` is a syntax error, so every script
  in such a document was lost — including the functions an `onload` attribute goes
  on to call, which left the test rendering its pre-script state.
  `css/CSS2` goes 4863 → 4908 of 6216 reftests, nothing lost.
- `Broiler.Layout` — `Engine.CanvasBackdrop`, the colour a translucent canvas
  background (CSS 2.1 §14.2) is composited against when the surface underneath it
  already carries paint. Thread-static and null by default, so a render that does
  not set it is unchanged. Read by a one-line `Broiler.HTML` change, upstream and
  pinned (`1bf117a`).

- `Broiler.JS` — the programs that have
  no script of their own (`eval`, and the `Function` constructor's body) can be written to disk,
  each under the name its stack frames use: `vm16.js` in a trace is `vm16.js` in the dump
  directory. Numbering those programs made a trace through a module loader attributable, but a
  name is only half of it — knowing a `b is not defined` came from `vm16.js:1,14` still does not
  say what `vm16.js` *is*, and a payload a loader evaluated exists nowhere on disk to go and look
  at. Off unless `BROILER_JS_DUMP_PROGRAMS` names a directory, because page script is page
  content and writing it on every render should be a deliberate act; the directory is also
  settable directly, as the other compiler switches are. A failure to write is swallowed — a
  diagnostic must never be able to break the execution it observes.

- `Broiler.HtmlBridge` — the Canvas 2D context has pixels. It owns a
  `Broiler.Graphics` `BBitmap` the size of the canvas and rasterises into it through
  `BCanvas`, so `getImageData`, `putImageData`, `createImageData` and
  `canvas.toDataURL` — all previously absent, so all previously a `TypeError` —
  report what was actually drawn. `fillRect`, `clearRect`, `strokeRect`, the path
  operations and `fillText` were empty method bodies before this: the context kept
  the drawing state and painted nothing, a Phase 6 leftover from removing a command
  recorder no renderer ever read.
  - The pixel API was deliberately left *absent* rather than stubbed while that was
    true, because zeroed pixels would have turned an honest `TypeError` — which
    every feature detector reads as "no canvas readback" — into a false claim of
    support. A real backing store is what retires that reasoning, and the same care
    is why `toDataURL` of a type with no encoder falls back to `image/png` (as HTML
    requires) rather than pretending: `image/webp` and `image/vnd.ms-photo` still
    correctly report themselves unsupported.
  - `getContext('2d')` returns the same context object every call, as the spec
    requires. That was invisible while the context held nothing worth keeping and
    decisive once it held a bitmap — every call was handing back a blank canvas.
  - `canvas.width`/`canvas.height` are reflected, and assigning either resets the
    bitmap to transparent black and the context to its default state — so
    `canvas.width = canvas.width`, the idiomatic way to clear a canvas, clears it.
  - `globalCompositeOperation` is a real accessor that applies the separable blend
    modes through a `BCanvas` blend layer and ignores an operator it cannot
    composite. It used to be nothing at all: the assignment merely created an own
    property on an extensible object, so the value read back because it had been
    stored, not because anything blended.
  - `ctx.canvas` is the canvas element rather than a fresh empty object, and the
    `HTMLCanvasElement` members are installed only on a `<canvas>` — `getContext`
    was previously on every element and answered `null` from a tag check inside,
    which made the call right and the name wrong.
  - `CanvasRenderingContext2D` and `ImageData` join the DOM interface-constructor
    globals. Neither is a node, so they answer `instanceof` from the members that
    define the interface rather than from `nodeType`; `ImageData` is additionally
    constructible, as HTML defines it.
  - The bitmap is script-visible, not yet page-visible: nothing outside the binding
    reads it, so a `<canvas>` still lays out as an empty replaced box and paints
    nothing into the page. A page that draws a chart and shows it still renders
    blank; one that reads its pixels back, or serialises them through `toDataURL`,
    now gets the real image.
  - Measured on html5test.com: 126/555 → 141/555, its 2D Graphics section 2/25 →
    17/25, with no row regressing. Still absent, and still honestly absent:
    `drawImage`, gradients and patterns, `clip`, `ellipse`, `setLineDash`, `Path2D`,
    `toBlob`, and any transform beyond the identity. See
    `docs/html5test-exceptions.md`.
- A CI runner for the [DuckDuckGo privacy test pages](https://github.com/duckduckgo/privacy-test-pages),
  the public corpus that asks a browser to exercise client-side storage, the
  request paths a page can open, the fingerprinting surface, and HTTPS upgrades.
  Each page runs its own probes and publishes them in a `results` global, which
  is what the suite reads — so it reports what the page computed rather than a
  rendering of it. `python scripts/run-privacy-test-pages.py`; weekly and
  manual-dispatch `Privacy Test Pages` workflow; `docs/privacy-test-pages.md`.
  - `Broiler.Cli --evaluate-page <URL> --evaluate <EXPR> --output <FILE.json>`
    is the general mechanism it is built on: the page's scripts run through the
    DOM bridge as a capture would, and then the given expressions are evaluated
    against the same JavaScript context. Pending timers and promises are drained
    *between* expressions, so one expression can start a page's own run and the
    next can read the global it filled. `--evaluate-html-output` additionally
    writes the post-script DOM.
  - What it measures is **coverage** — which probes Broiler carries out at all —
    and not a privacy score: these pages are written to describe a browser, so
    the values they collect are a description of what a page can observe.
    `tests/privacy-test-pages/baseline.json` records the per-probe outcome, and
    only a probe that used to produce a value and no longer does, or a page that
    stops running, fails a run. The corpus is live, so tests added and removed
    upstream are reported and never fail one.
  - First baseline, over nine of the Privacy Protections pages: **97 of 173
    probes carried out**. Nearly all of that is the 124-probe fingerprinting
    page (97/124); the request- and storage-driven pages are at 0 today, because
    their probes depend on sub-resource loads, workers, sockets and frames
    completing and reporting back. Three further pages are deliberately outside
    the corpus — `referrer-trimming` and `storage-partitioning` cannot report
    from a single-page evaluation, and `click-to-load` never settles.

### Changed

- A codec handed another codec's option type now returns a structured
  `document.options.invalid` rejection instead of silently ignoring it and
  applying its own defaults — settings the caller never chose. The plain shared
  option type is still always accepted.
- `DocumentReadOptions.DecodeEmbeddedObjects` and `DocumentWriteOptions.AsciiOnly`
  stop implying behavior that does not exist. Neither had a single consumer:
  the RTF reader skipped pictures and objects whichever way the first was set,
  and the writer always escaped non-ASCII whatever the second said. Both are now
  documented as what they are, and a codec asked for the unimplemented behavior
  reports `document.capability.not-composed` — the RTF reader escalating its
  embedded-object note to that code only when the caller asked for decoding
  *and* the document actually contains one. `RtfReadOptions`/`RtfWriteOptions`
  name the RTF-specific settings.

  `MaxGroupDepth` and `MaxBinBytes` were expected to move to an `RtfLimits`
  alongside them and did not: DOCX enforces both — style-inheritance depth and
  part size — so they already have the second consumer the containment rule
  wants and belong where they are. Moving them would have forced DOCX to
  duplicate them.

- `DocumentReadOptions`, `DocumentWriteOptions`, `DocumentReadResult` and
  `DocumentWriteResult` are no longer sealed, so a codec can carry typed options
  and results through the shared `DocumentCodec` signature. Existing behavior and
  members are unchanged.
- The `Privacy Test Pages` workflow files what a run found as GitHub issues, the
  way the WPT runners do, instead of leaving it in an artifact that expires in 30
  days. Three of them, because ranking them together buries the two small ones
  under the large one: the regressions (the only finding that describes a change
  this repository made), the pages this run could not measure at all (whose zero
  gaps otherwise read like the zero of a page in full parity), and the probes
  Chromium answers and Broiler does not. `run-privacy-test-pages.py` decides what
  is worth filing and writes the bodies, so the rules live with the runner that
  measured them; `--github-output` carries the counts and flags to the workflow.
  - The gaps issue leads with thrown-error signatures rather than with the probe
    list. A missing API is reported once per probe that touches it, so
    normalizing the quoted values, URLs and numbers out of each message regroups
    a page of repetitions back into the one finding it is.
  - Unlike the WPT runners, which open a fresh issue per run, this suite refreshes
    the issue already open for a kind — title, body, and a comment linking the run
    — matching on a hidden marker in the body (`scripts/lib/github-issue.js`). The
    WPT suites are dispatched by a human who then reads them; this one runs weekly
    and unattended over a backlog that changes slowly, and a second copy every
    Tuesday would bury the one somebody is working from. A `--update-baseline` run
    files nothing: its comparison describes the baseline being replaced.

- `Broiler.JS` (patch, pending upstream) — `new undefined()` and `new null()` report
  `undefined is not a constructor` / `null is not a constructor` instead of
  `cannot create instance of …`. No browser used the old wording and neither did
  the rest of the engine — `JSFunction`, `JSSymbol`, `JSGenerator`, `JSReflect` and
  `JSPromisePrototype` all already said "is not a constructor"; these two sites were
  the only holdouts. It matters because the throw itself is usually *correct*: it is
  what a feature probe such as html5test's
  `new (window.RTCPeerConnection || …)(null)` gets from an engine without WebRTC, and
  a wording no browser produces makes a right answer read as an engine fault.
  `undefined is not a function` is deliberately unchanged — that one already matches.

- `Broiler.Browser.Core` and `Broiler.Writer` — the UI hosts no longer keep a
  private clipboard string for when the shell wired no platform accessor. A
  fallback that only this process can see makes copy and paste appear to work
  while interoperating with nothing on the machine, which is why the Code
  heads already refused to carry one; with the desktop shells now wiring real
  clipboards, the fallback's only remaining effect would be to hide a host
  that has none. A host without an accessor reports no text, and the editing
  commands show themselves unavailable — the single-line edit's context menu
  greys Paste out on such a host rather than offering a no-op.

- `Broiler.HtmlBridge` — a script names itself in the stack traces of the errors it
  raises. `JSContext.Eval` takes the location it reports in stack frames and every
  host passed none, so every frame of every script read `vm.js`: an exception at
  `vm.js:3,159` could be any of a dozen scripts on a real page, one of which is a
  megabyte of minified vendor code, and nothing in the trace narrowed it down. The
  labels the hosts already used for their profiling entries and error messages —
  `inline-{n}`, `deferred-{n}`, `module-{key}`, per bucket in document order — are now
  the evaluation's location too, so the trace, the profiling timeline and the logged
  message all name the same thing. `CaptureService` reports which script failed at all
  now, where it used to log a bare "Script execution error". Because the location is
  part of the code-cache key, `ScriptCompileAhead` gained a per-source overload
  (`ScriptCompileUnit`): compiling every source under one location while evaluating
  under per-script ones would have filed each entry where the evaluation does not look
  and silently lost the compile/evaluate overlap.

- `Broiler.Layout` / `Broiler.HTML.Image` — a page's display list is now replayed
  into disjoint horizontal strips of the target surface at once, and the managed
  rasterizer no longer walks pixels its clip is certain to reject. The raster
  stage is 1.76-3.49x faster per page of the roadmap corpus at four cores
  (`paint` 1323.7 -> 461.4 ms), and about half of that is the clip narrowing
  rather than the threads: a document taller than its viewport was rasterising
  text and boxes that nothing could see. Output is byte-identical to the
  single-tile render at any tile count, and the full WPT run reports the same
  verdict and the same pixel-match percentage on every test. Set
  `BROILER_RASTER_TILES=1` to replay on the calling thread only, which a host
  that already runs several renders at once should do.
- `Broiler.Media.Image.Managed` — PNG expand-to-RGBA and JPEG dequantize/IDCT and
  colour conversion now run across threads (2.08–2.61x on a 1024x1024 JPEG,
  1.22–1.29x on the PNG equivalent, at four cores). Output is byte-identical to
  the single-threaded decoder at any thread count — the sequential path is the
  same code with one band, not a separate implementation. Inflate, unfilter and
  Huffman decode are unchanged: they carry real sequential dependencies. Set
  `BROILER_IMAGE_DECODE_THREADS=1` to decode on the calling thread only, which a
  host that already runs several decodes at once should do.

### Fixed

- `innerHTML` dropped every text and comment child. Its read side filtered the
  child list to elements, so `<div>ab<b>c</b>d<!--k--></div>` read back as
  `"<b>c</b>"` and a div holding nothing but text read back as the empty string
  — while that same element's `textContent`, `childNodes` and `outerHTML` all
  reported the text correctly. `outerHTML` never had the bug because it hands
  the whole subtree to the serializer in one call rather than re-serializing
  children one at a time, and the document-level serialization the renderer uses
  is that same call, which is why rendering never saw it. The filter is a
  leftover from the era when a text child was a string on its parent's element
  record rather than a node.

- `response.blob()` handed back a look-alike rather than a `Blob`: a plain object
  carrying `size`, `type`, `text()` and `arrayBuffer()` and nothing else, so
  `constructor.name` was `"Object"`, there was no `slice`, and
  `(await response.blob()) instanceof Blob` could not even be asked. The stub was
  invisible only because the interface it imitated did not exist to be compared
  against; it now mints a real one.

- The `:is()` aliases no longer match every element. `:matches()`, `:any()`,
  `:-webkit-any()` and `:-moz-any()` are its historical spellings, and all four
  sat in the CSS selector matcher's recognized-but-unmodelled set, fell through
  its lenient default arm, and matched **every** element. The cascade reaches the
  same matcher, so `:-webkit-any(h1) { color: red }` painted the whole page rather
  than the headings — a rendering bug as much as a `querySelector` one. Measured
  against a browser: only the `-webkit-` spelling is still accepted, and it
  behaves exactly like `:is()`; the other three were removed from the platform, so
  they match nothing. An unknown *vendor-prefixed* functional pseudo-class still
  matches everything, which stays the matcher's deliberate policy for extensions
  it does not model.

- `Range.compareBoundaryPoints` had `START_TO_END` and `END_TO_START` swapped.
  DOM §4.5 makes `START_TO_END` compare *this* range's end against the source's
  start and `END_TO_START` this range's start against the source's end; each was
  reading the other pair, so two of the four comparisons answered the wrong sign.
  `cloneRange` also minted its copy against the main document whatever the
  original's root was, so a range created in a frame's document cloned into the
  containing page's.

- Custom Elements work. There was no implementation: `customElements` was
  undefined and `HTMLElement` threw `Illegal constructor`, so
  `class X extends HTMLElement` followed by `customElements.define(…)` failed on
  the bare name — which aborts the whole script, not the statement that named it.
  A component page therefore rendered as whatever its markup said before any
  component built itself.

  `customElements` is now a real `CustomElementRegistry` with `define`, `get`,
  `getName`, `whenDefined` and `upgrade`; `HTMLElement` is constructible from a
  defined class, so `new X()` and `document.createElement('x-thing')` both
  produce a real element carrying the class's own methods; elements parsed
  before their definition are upgraded in place, keeping their identity and
  children; and the `connectedCallback`, `disconnectedCallback` and
  `attributeChangedCallback` reactions run synchronously, as a browser runs
  them.

  Name validation follows HTML §4.13.1, including the hyphenated SVG and MathML
  names that are reserved. Customized built-ins (the `extends` option and `is=`
  attribute), form-associated custom elements and `adoptedCallback` are not in
  this slice and are not faked — `define` rejects an `extends` option rather
  than accepting it and quietly doing nothing.

- `element.attributes` is a live `NamedNodeMap`, and an attribute is one `Attr`
  node with a live value. It was a fresh plain object per read: naming
  `NamedNodeMap` was a `ReferenceError` (which aborts the whole script, not just
  the expression), `el.attributes === el.attributes` was false, and
  `el.attributes.id` was `undefined` where a qualified name is a supported
  property name. The fourth fault made pages throw rather than answer wrongly —
  `length` was live while the indices were fixed at build time, so a map held
  across a `setAttribute` reported the new count with nothing at the new index
  and the idiomatic `for (i = 0; i < m.length; i++) m[i].name` read
  `undefined.name`.

  Attribute nodes became real objects to match: one node per element and name
  across every access path, whose `value` reads through to the element and
  writes back, and which detaches on removal — keeping its last value, losing
  its `ownerElement`, and letting a re-add mint a new node.

  Beyond its own scope, this corrected the collection prototypes' members from
  non-enumerable to enumerable on `NodeList` and `HTMLCollection` as well, which
  is what Web IDL specifies and a browser has: `for (var k in el.childNodes)`
  now yields `item`, `forEach` and the rest beside the indices.

- DOM objects report the interface they implement. Every element, every
  attribute and the document itself answered `constructor.name` of `"Object"`,
  with `Object.getPrototypeOf(el) === Object.prototype` — so anything keyed on
  the constructor (debugging output, logging, type dispatch) read the wrong
  thing. They now name their interface, and the interfaces inherit along the
  chain Web IDL gives them (`HTMLDivElement → HTMLElement → Element → Node →
  EventTarget`). That last part is the one that is not cosmetic: extending an
  interface prototype is the ordinary polyfill idiom, and
  `Element.prototype.matches = …` now reaches every element where the assignment
  used to go to an object nothing inherited from.

  The per-tag interface names are Chromium's measured answers to
  `document.createElement(tag).constructor.name` over every HTML tag, which is
  what makes the three-way fallback right: a tag with its own interface names it,
  a known tag without one is `HTMLElement`, and a tag that is neither known nor a
  valid custom element name is `HTMLUnknownElement`. A hyphenated name such as
  `x-foo` is an `HTMLElement` even when nothing defined it.

  The same measurement corrected a live error: `plaintext` was grouped with
  `listing`/`pre`/`xmp`, so `document.createElement('plaintext') instanceof
  HTMLPreElement` answered `true` where a browser answers `false`.

  Still reported as before: SVG elements answer the base `SVGElement` rather than
  `SVGRectElement` and the rest, and `element.attributes` answers `"Object"` —
  neither interface set is registered, and the engine's own members remain own
  properties of each wrapper rather than living on the prototypes.

- An invalid selector fails instead of matching the wrong element.
  `document.querySelector('div:::bogus')` returned a real `<div>` — the matcher
  read it as `div` — and `querySelectorAll('[')` matched four elements, so an
  unparsable selector did not produce no answer, it produced a plausible wrong
  one that no caller can detect. All five scripted entry points
  (`querySelector`, `querySelectorAll`, `matches`, `closest`, and the document,
  frame and `DocumentFragment` forms) now throw the `SyntaxError` DOM §4.2.6
  requires. Selectors that carry a **pseudo-element** were the same failure by
  another route: `querySelector('::before')` returned the `<html>` element,
  where a pseudo-element selects a box and so matches nothing; those now answer
  no element, in both the `::` and legacy one-colon spellings, while the cascade
  goes on applying `::before` rules. A pseudo-element that is not the subject
  (`div::before p`) is a syntax error, as it is in a browser.

  A well-formed but unrecognized pseudo (`:nope`, `::bogus`) is deliberately
  accepted rather than rejected: rejecting one needs a list of every pseudo the
  engine supports, and turning an unknown name into an exception would break a
  page that merely asked for a pseudo Broiler lacks.

- `setAttribute` rejects a name a browser rejects. Every invalid name was
  written through silently — `@click`, `foo bar`, `1abc` and `-x` all became
  attributes no browser will create — and the one name that did fail, the empty
  string, threw a bare `Error` with no `name` or `code` for a caller to branch
  on rather than a `DOMException`. It now throws `InvalidCharacterError`, as do
  `toggleAttribute` and `setAttributeNS`; `getAttribute`, `hasAttribute` and
  `removeAttribute` keep accepting anything, which is where a browser draws the
  line. Colons stay valid, so `setAttribute('xlink:href', …)` and
  `setAttribute('v-on:click', …)` are unaffected — the XML `Name` production
  admits them, and rejecting them would have broken inline SVG.

- A frame's `document` answers the same object model as the document containing
  it. An `<iframe>`'s `contentDocument` — and every `createDocument` /
  `createHTMLDocument` result — kept its own older accessors, so from one page the
  two documents disagreed about what a document is: `d.forms.constructor.name`
  was `"Array"` against the parent's `"HTMLCollection"`, `d.forms === d.forms` was
  false, `namedItem` and named access did not exist, a held collection's `length`
  did not move when the tree changed, and `anchors`, `embeds`, `plugins`,
  `doctype`, `dir` and `designMode` were absent outright, so `d.embeds.length` in
  a frame threw the `TypeError` the main document had stopped giving. Both
  documents are now served by one implementation. The query methods came with it,
  each typed as DOM assigns — `querySelectorAll` stays a static `NodeList`, the
  one collection specified as a snapshot, while `getElementsByTagName` /
  `ByClassName` are live `HTMLCollection`s and `getElementsByName` a live
  `NodeList`. A frame's tree also gained the DOCTYPE node its `doctype` accessor
  needs: the frame parse returned the `<html>` element alone, so a resource
  declaring a DOCTYPE built a tree that never carried one.

- `image-set()` picks an image instead of painting nothing. It was recognised as
  an image value and never resolved, so the layer reached two readers that each
  understand only part of what the function can hold — the background loader
  looks for `url(`, the paint walker looks for `gradient(` — and whichever kind
  of image it named, nothing was drawn. The selection is now resolved into the
  value before either of them sees it: resolution units (`x`/`dppx`, `dpi`,
  `dpcm`, `calc()`), an omitted resolution as 1x, `type()` filtering, and the
  smallest resolution that reaches the display, largest when none does, first on
  a tie. The selected resolution does not yet scale the image's intrinsic size.

- A CSS `transform` rule applies to an SVG element. The renderer reads an
  element's transform off the markup and sees no stylesheet, so the cascaded
  value is projected onto the SVG presentation attribute — `transform-box` and
  `transform-origin` were, and `transform` itself was not, so
  `rect { transform: rotate(90deg) }` did nothing at all. A declaration that
  parses no transform function is skipped rather than written, which is what
  makes an invalid one fall back to the `transform` attribute instead of
  destroying it (CSS Transforms 1 §3).

- A rotated or skewed element is painted. `transform: rotate(45deg)` on a green
  square produced a **blank page** — and took its whole subtree with it, so a page
  that rotates a wrapper lost everything inside it. The raster canvas maps a point
  per axis, so translation and axis-aligned scale fold into its own state and a
  rotation does not; those fell through to a compat backend that on a headless host
  is an inert stub, and the fall-through also stops every draw the group encloses
  from reaching the canvas at all. Such a transform now opens a layer the contents
  draw into untransformed, resampled through the matrix on the way out — so every
  primitive keeps the arithmetic it already has. Ancestor clips are applied to the
  transformed result rather than to the content on its way in, which is where
  CSS Transforms 1 §3 puts them.

- An SVG used as an image keeps the intrinsic aspect ratio its `viewBox` gives it
  even when it declares `preserveAspectRatio="none"`, and reports an absolute
  `width` or `height` it states even when the other one is missing. SVG 2 §8.2
  derives the intrinsic sizing properties from `width`/`height`/`viewBox` alone —
  `preserveAspectRatio` governs how the content is fitted once the viewport size
  is known, which is a later question than what size the image asks to be. Reading
  `none` as "no intrinsic ratio" made every such image size to the whole
  background positioning area, so a `background-size: contain` that should paint a
  16×256 strip stretched across the full 768×256 box instead. Measured over the
  full WPT reftest suite: 18 776 → 18 844 passing, +68 / −0.

- `display: contents` on the root element no longer takes the page's canvas with
  it. The root element is blockified (CSS Display 3 §2.3) and the rule was
  implemented, but it was applied to the anonymous document box the parse creates
  rather than to the `<html>` box beneath it — so the real root element was
  spliced out like any other wrapper, and the canvas background, which is the root
  element's, went with it.

- A `<link rel="stylesheet" href="data:text/css,…">` now contributes its rules to
  the cascade and the CSSOM, and fires `load` rather than `error`. The sheet is in
  the URL, so there is nothing to fetch — but the href went to the resource loader
  like any other, and that loader dispatches `file` and `http(s)` only, so the
  sheet came back empty. The renderer's own loader has always decoded `data:`
  URIs, which is what hid it: such a sheet *painted*, while `getComputedStyle`
  and the link's load event both reported it as not loaded.
  A page that builds the link from script and waits on `link.onload` before doing
  its next step therefore waited forever. `@import url(data:text/css,…)` was
  already decoded in process; both call sites now go through that one seam.

- The desktop browser paints a page's load window while it runs, instead of only
  its final state. Settling the load window on the load worker took the page's
  callbacks out of the message pump, which is what stopped www.google.com hanging
  the window — but it settled *silently*, and every intermediate frame went with
  it. Acid3 advances its score one test per `setTimeout`, all of it inside the
  load window, so it stopped counting up and arrived already finished; a heavy
  page showed nothing at all until it was done, which for mediawiki.org meant a
  blank window for the whole 33 s load.
  - `InteractiveSession.SettleLoadWindow` now reports the document after every
    batch, as a thunk rather than a string, so a host still painting the previous
    frame declines this one for free.
  - The browser keeps one frame in flight, releases it only once it has actually
    been painted, and holds frame work to a quarter of the settle's running time.
    The parse — the expensive half, because it re-fetches the document's
    stylesheets and web fonts every time — stays on the load worker; the UI thread
    swaps in a finished container and lays it out, and still runs no script. So a
    cheap document animates (Acid3: 57 paints during load, where there had been
    none) and an expensive one buys a few early frames rather than a hundred
    (mediawiki.org first paints at 16.6 s of a 41 s load; www.google.com is
    unchanged at 29.6 s and paints three times on the way).
  - A `file://` navigation no longer runs on the UI thread. `LoadUrl` started the
    load with a bare call, and an async method runs on the calling thread until it
    first suspends — which the load only does if the fetch does. Reading a local
    file never yields, so the fetch, the scripts and the whole settle ran inside
    the UI thread's `NavigateTo`: the exact freeze the settle exists to avoid,
    still there for local pages. It starts with `Task.Run` now.

- Work a frame defers runs in the frame's browsing context. Every document shares
  one JavaScript context here and the queue drain runs from C# with no context
  pushed, so a callback woke up in the **main** one: a frame's `setTimeout`,
  `setInterval`, `requestAnimationFrame` and `requestIdleCallback` callbacks saw
  the *parent's* `document`, `location` and `window`. Looking up one of the
  frame's own elements returned null, and the callback then either threw — caught
  by the drain and logged, leaving no trace in the page at all — or quietly
  mutated the wrong document. The frame's synchronous script was always correct,
  which made the failure look like "frames don't run scripts" rather than what it
  was; deferring to a timer is how most framed script does anything, so this was
  most of what a frame could do.
  - A callback is now bound to the browsing context that registered it and
    re-enters it when the queue drains — including a callback registered from
    inside another frame callback, which is the shape of every self-rescheduling
    loop. The rAF timestamp and the `IdleDeadline` are forwarded through the
    switch rather than dropped.
  - Only frames pay for it. A main-window registration is left unwrapped, so
    `setTimeout` — which busy pages call constantly — keeps its allocation-free
    path and no per-tick save/restore.

- An idle callback is handed an `IdleDeadline`. `requestIdleCallback` was a bare
  alias of `setTimeout`, which invokes its callback with no arguments at all, so
  the parameter every caller destructures was `undefined` and the first thing they
  do with it — `deadline.timeRemaining()` — threw. That is not a peripheral API:
  MediaWiki's ResourceLoader *evaluates its module implementations* inside an idle
  callback, looping until `timeRemaining()` reaches 0, and its key-value store
  walks `localStorage` in another.
  - `timeRemaining()` reports a 50 ms budget — Background Tasks §2.2's cap —
    measured from the moment the callback is entered, so a page draining a queue
    makes progress and then yields. A budget pinned at 0 would be worse than the
    throw for the common `while (deadline.timeRemaining() > n) { … }` loop: it
    reschedules for ever without shifting a single item.
  - `didTimeout` is true when the page passed a `{ timeout }`, because a headless
    drain has no idle period the callback could have been run in earlier — it is
    running because that deadline arrived.
  - The second argument is an options dictionary, not a delay. Read through the
    `setTimeout` adapter it came out `NaN` and was clamped to 0, so a page's
    timeout was silently discarded; it is read out of the dictionary now.
  - `cancelIdleCallback` still cancels: the handle comes from the timer id space,
    which is what makes it the same thing `clearTimeout` acts on.
  - Both are mirrored onto a nested browsing context's `window`, beside the
    animation-frame pair they were the only scheduling API missing next to. The
    bare name always resolved — it is a context global and every document shares
    the one context — so what was `undefined` is the qualified read, which is the
    spelling pages actually use: the standard feature test is
    `window.requestIdleCallback ? … : fallback`, and inside a frame `window` **is**
    the sub-window. A framed page therefore took its no-native path, and one that
    called `window.requestIdleCallback(cb)` unguarded got a TypeError.

- `img.src` is a string. `HTMLImageElement` carried nothing but `width`/`height`,
  so every other IDL attribute in the interface read back as `undefined` — not the
  empty string a missing content attribute reflects as — and the first string
  method a caller reached for threw on it. `www.mediawiki.org`'s start page died
  exactly there: MultimediaViewer walks the page's thumbnails, hands each one's
  `src` to `mw.util.parseImageUrl`, and that function opens with `url.match( … )`.
  Because MediaWiki serves its modules as one `load.php` bundle, the throw took
  the thumbnail walk and everything queued behind it down with it.
  - `src` and `currentSrc` are resolved URLs, as they are in a browser, and the
    absolute one even when the content attribute is relative. `currentSrc` reports
    the empty string when there is no `src` to settle on, so the
    `img.currentSrc || img.src` idiom reaches its second operand rather than a
    page URL manufactured out of an absent attribute.
  - `alt`, `srcset`, `sizes`, `useMap`, `isMap`, `crossOrigin`, `referrerPolicy`,
    `decoding`, `loading` and `fetchPriority` reflect their content attributes in
    both directions. Writing one used to set a plain JS property on the wrapper
    that no content attribute — and so no layout, cascade or serialization — ever
    saw.

- `sessionStorage` exists. Only `localStorage` was ever registered, and because
  `window` **is** the global object, the bare `sessionStorage` a page writes was a
  `ReferenceError` rather than an undefined property — which aborts the entire
  script, not the statement that read it. `www.mediawiki.org` serves its modules
  as one `load.php` bundle, so that single identifier took ResourceLoader, the
  Vector skin and every module queued behind them down with it, and the only
  symptom was `ReferenceError: sessionStorage is not defined` on a page that had
  quietly lost all of its script-driven behaviour. The two areas are separate
  stores, as they are in a browser, and a same-origin frame now sees its parent's
  areas rather than empty ones of its own.
  - The `Storage` object grew the rest of its interface with them: `length` and
    `key(n)`, which is how a page walks an area it did not write itself, and
    named-property access in both directions — `storage.foo = 1` is now the same
    item as `setItem('foo', '1')`, where a directly-assigned property used to be
    invisible to `getItem`, `length` and `key()` alike. The methods are
    non-enumerable, so `Object.keys(storage)` and `for (var k in storage)` yield
    the stored keys alone rather than four methods among them.
  - `Storage` is registered as an interface global, because
    `typeof Storage !== 'undefined'` is the canonical Web Storage feature test: a
    page that fails it takes its no-storage path however complete the areas
    themselves are. Like the DOM interface globals it answers `instanceof` from
    the object's shape.
  - The CLI's script-only capture path (no DOM) had a second, thinner copy of the
    same storage stub, reachable only as `window.localStorage`; it now shares the
    one implementation and registers both areas unqualified as well.

- Nothing in the tree changed for column fragmentation of a box that has content
  in it, and that is the result rather than an omission. It was built — each piece
  a copy of the box carrying the part of its subtree in that piece, a straddling
  child cut in turn, text left alone because a piece is a real box and the words of
  a box are not copied onto it — and a controlled render confirms it does exactly
  what `box-decoration-break: slice` asks for across a column set. Five
  configurations were then measured against paged `css/css-break` at 92 of 204,
  average 87.45%, and the best of them holds the count and loses 0.01 of a point,
  with `css-break/fieldset-004` down 88.5% → 84.4% against `fieldset-001` up
  77.7% → 78.0% and still failing; the one configuration that would let
  `fieldset-001` reach the cut costs six tests. The whole of it was reverted and
  the numbers are recorded in `docs/wpt-rendering-gaps-open.md`, because the cut is
  not the missing piece on its own: the single-child descent and the deep-fragment
  path are the two workarounds built in its absence, and all three have to move
  together.

- `Broiler.Layout` (with **pending patches** `0004`/`0005` for `Broiler.CSS` and
  `Broiler.HTML`) — a `<legend>` is a block box, a fieldset's first one is
  rendered on the fieldset's block-start border, an `aspect-ratio` no longer sizes
  a box below its own content, and the flow-relative minimum and maximum sizes
  reach layout. HTML's rendering section makes a legend a block box and neither
  user-agent source said so, so a legend's `width`, `height` and `padding` did
  nothing at all — that is the one-line core, and it needs a patch in each
  submodule because the two sources feed different paths into layout. This
  repository's half is the placement: a fieldset's first legend is not laid out in
  its content (HTML §15.5.13) but belongs to the block-start border, its margin
  box centred on it, so a legend taller than the border stands proud of the border
  box and the content begins below it. The placement acts on a block-level legend
  only, so it is inert against the pinned pointers. Making the legend a block
  exposed two adjacent gaps the same tests rest on, and both are fixed here: CSS
  Sizing 4 §5.1's automatic content-based minimum, which stops a preferred aspect
  ratio from sizing a box shorter than its content (the transferred ratio height
  was overwriting the content height outright); and `min-block-size`,
  `max-block-size` and their inline counterparts, which parsed and were then
  dropped by layout, so the physical `min-`/`max-` longhands now consult whichever
  flow-relative longhand names the same axis under the box's writing mode.
  Measured with the submodules at their pinned pointers, under
  `BROILER_WPT_PAGED_PRINT=1`: `css/css-break` holds at 92 of 204 with the average
  87.38% → 87.45% and `break-inside-avoid-min-block-size-1` 82.0% → 96.3%;
  `css/css-sizing` holds at 74 of 112 with both `aspect-ratio/fieldset-element-*`
  tests reaching exactly 100%; `css/css-page` (135 paged, 142 default),
  `css/CSS2`, `css/css-backgrounds` and `css/css-values` are unchanged
  test-for-test. With the patches applied, `fieldset-004` 84.4% → 88.5% and
  `fieldset-003` 91.3% → 92.1%. `fieldset-001` goes 78.0% → 77.7% and still fails:
  its column set has to cut a box with content in it, and the column pass walks
  into the fieldset and redistributes its children — stopping that descent is the
  right rule and costs four tests (measured 92 → 88), so it stays.

- `Broiler.Layout` — a box taller than a column set continues in the next column
  instead of staying whole in the first one. The engine filled a column set by
  moving whole boxes into it, which covers a column set made of several blocks and
  not the shape most of the fragmentation corpus is written in: one block, taller
  than the column set, whose decoration is the thing under test.
  `FindMultiColumnFragmentParent` also answered null for a column set with a single
  child — fewer than two boxes, nothing to distribute — so a one-child
  multi-column box was not columnised at all, however tall that child was. That
  rule was right while the only thing a column set could do was move boxes; it
  stops being right the moment a box can be cut. `SliceTallFragments` now divides
  a fragment taller than the column into a run of column-tall pieces, and the
  decoration is sliced rather than repeated (`box-decoration-break`'s initial
  value): the border and its rounded corners belong to the two outer ends of the
  run and the joins between are square and open. Only a box with no content of its
  own is cut, because a slice here is a real box rather than a paint instruction;
  and a background image, gradient or box shadow keeps the whole box, because
  those are positioned against the box they are on and slicing paints them once
  per piece instead of once across the run. Under `BROILER_WPT_PAGED_PRINT=1`
  `css/css-break` goes 91 → 92 of 204, average 87.11% → 87.38%: `borders-002`,
  `out-of-flow-in-multicolumn-014` and `table/table-border-007` gained, and
  `fieldset-002` (86.4% → 98.7%), `borders-003` through `-006` and
  `rounded-clipped-border` all move a long way without reaching the gate.
  `out-of-flow-in-multicolumn-019` and `overflowing-block-003` are lost, both of
  which were passing on the pixel budget while rendering content that already ran
  out of the bottom of the column set. `css/css-page` holds at 135 paged and 142
  default with `fixedpos-011-print` 97.0% → 98.3% and 95.2% → 97.0%; paged
  `css/CSS2`, `css/css-backgrounds` and `css/css-values` do not move.

- `Broiler.Layout` — a `position: fixed` box is on every page of a paged render,
  and it lands where its insets say. Three bugs met in one family of tests. CSS
  Paged Media makes the page area the fixed-positioning containing block, so a
  fixed box repeats per page rather than appearing once in the document;
  `FragmentTreeBuilder` now emits the extra appearances, one page further down
  each, as fragments and never boxes — a fixed box is out of flow, contributes no
  height, and the page count is unchanged. Second, `PositionAbsoluteBox` — the
  post-layout pass that re-resolves `right`/`bottom` once the used size is known —
  ran for `absolute` and not for `fixed`, and the earlier pass recovers a height
  only from an explicit non-percentage `height`, so a fixed box sized by its
  content and anchored with `bottom` was anchored by its *top* edge to the
  viewport's bottom: one box-height low, which in a paged render is the top of the
  next page. Third, that same first pass placed a bottom-anchored `absolute` box
  at its containing block's bottom edge while its height was still unknown, so the
  subtree laid out below that edge and `LayoutEnvironment.ActualSize` — a running
  maximum — kept the overshoot after the box was corrected; a document 36px too
  tall is a whole extra blank page. It now stays at its static position until the
  height is known. Under `BROILER_WPT_PAGED_PRINT=1` `css/css-page` goes 134 → 135
  of 224, average 77.21% → 77.67%, `fixedpos-009-print` gained and none lost; nine
  more `fixedpos-*` go from passing at 99.2%–99.9% to exactly 100%, eight of them
  on the default unpaginated path too. Paged `css/css-break` (91), `css/CSS2` (96),
  `css/css-backgrounds` (424) and `css/css-values` (104) do not move, and the
  default run holds at 142. Two tests slip slightly, both the fix correctly
  repeating a box that is misplaced for an unrelated reason: `fixedpos-011`
  (multicol not column-filling inside the fixed box) and `page-margin-005`
  (percentage `@page` margins).

- `Broiler.Wpt` / `Broiler.Layout` — a paged render lays the flow out once per
  distinct page area instead of once per document, so a document whose named
  `@page` rules size the page differently divides against each page's own area —
  where its floats wrap and where its breaks fall — and not against the
  unconditional one. Each run of consecutive pages sharing a name is taken from
  the pass that used its area, which is sound because a page-name change forces a
  break (CSS Paged Media 3 §5.3): a run always starts at a page boundary in every
  pass, so its content divides against its own area alone. The *order* of the runs
  is a property of the document and is read from the flow in document order; only
  each run's length is a property of the page. Reading both off a scan of the
  laid-out bands was tried and is wrong twice over — content overflowing its page
  area invents runs the document never asked for, and it derives the page count
  from fragment bounds when the content height settles it, which cost nine tests
  in one measured run. Alongside it, `ApplyForcedPageBreakBefore` no longer steps
  over a float: a float declares no break of its own, but a `break-after: page` on
  the sibling before it ends the page the float would otherwise sit on. Measured
  with the submodules at their pinned pointers, this moves exactly one test —
  `page-name-unnamed-trailing-001-print`, 96.4% → 100% — because every test the
  capability was built for turns out to be blocked behind something else
  (`position: fixed` not repeating per page, `page-orientation` not rotating,
  `vw`/`vh` not resolving against the first page's area, page-box percentages not
  resolved per named page, `auto` margins on named pages); those are enumerated in
  `docs/wpt-rendering-gaps-fixed.md`. Under `BROILER_WPT_PAGED_PRINT=1`
  `css/css-page` goes 133 → 134 of 224, average 77.19% → 77.21%, none lost; paged
  `css/css-break` (91), `css/CSS2` (96), `css/css-backgrounds` (424) and
  `css/css-values` (104) do not move, and the default unpaginated run is unchanged
  at 142.

- `Broiler.Wpt` / `Broiler.Layout` — a paged render prints each page on the box
  the *name that page carries* resolves to, instead of guessing one name for the
  whole document. The guess arrived with the earlier named-page fix and is right
  for the unpaginated path, which renders a single sheet; a paged render knows its
  pages one at a time and should never have taken it. `page-name-unnamed-trailing-001`
  uses exactly one name — `landscape`, on its middle page — so the guess put that
  page's `margin: 20px` on all of them, giving a 260px page area and a fourth page
  where its reference (two names, so no guess applied) had 300px and three.
  `ComputedStyle` now carries `page` into the fragment IR, because a laid-out
  fragment does not otherwise remember it, and `RenderPaged` reads the name off
  the first fragment to start on each page — CSS Paged Media 3 §5.3 makes that
  box's used name the page's name, and a later box on the same page cannot rename
  it. When every page names the same rule the flow is laid out once more against
  that rule's area (`page-name-table-001` is a single page on
  `@page square { size: 5in }` and needs it); a document with mixed names still
  divides its flow against the unconditional area, so only the boxes differ.
  `page-size-010-print` gains: under `BROILER_WPT_PAGED_PRINT=1` `css/css-page`
  goes 135 → 136 of 224, average 77.53% → 78.27%, `css/css-break` unchanged at 90,
  none lost, default unpaginated unchanged. The test this was opened for goes from
  `SizeMismatch` at 0.0% to 96.4% `MissingContent` and still fails — the residual
  is per-page *layout*, which this is not.

- `Broiler.Layout` — a page-name change breaks inside an out-of-flow subtree.
  `CarriesThePageFlow` walked the whole ancestor chain and answered no for
  anything inside a `position: absolute` or `fixed` box, conflating two rules: an
  out-of-flow box does not carry its *own* page name into its parent's flow, but
  its children are stacked in its own block flow and a name change between two of
  them is still a page break. `css-page/page-name-003-print` states that and now
  passes. It trades exactly one-for-one against `page-name-abspos-002-print`,
  which asserts the opposite on near-identical markup — printed to PDF, Chromium
  breaks in both cases, passing the first and failing the second against its own
  reference, so Broiler now lands the same way round and that test is filed in
  `docs/wpt-rendering-gaps-wont-fix.md` with the page counts. The score does not
  move (paged `css/css-page` stays at 135 of 224, `css/css-break` at 90, default
  unpaginated unchanged); this is kept for the rule it removes.

- `Broiler.HTML` (**pending patch**, `patches/0003`) — a block-level image keeps
  its page name. `CorrectImgBoxes` implements a block-level replaced element by
  wrapping the image in an anonymous block and demoting the image itself to
  `display: inline`, so it paints as an inline replaced word inside a block
  wrapper; the geometry is right, but the wrapper is now the block-level box the
  element generates and CSS Paged Media 3 §3.4 hangs a page name on a
  block-level box and nothing else. The name stayed behind on the demoted
  inline, so `<img style="display:block; page:b">` read as staying on its
  ancestor's page and a following `div { page: b }` forced a break that should
  not be there. `css-page/page-name-img-001` and `-002` are the control — there
  the image really is inline and its name really must be ignored — and all four
  now pass at 100%. Only paged rendering reads a page name, so the default
  unpaginated render is unchanged test-for-test across `css/css-page`,
  `css/css-break`, `css/css-backgrounds` and `css/css-values`; under
  `BROILER_WPT_PAGED_PRINT=1` `css/css-page` goes 132 → 134 of 224, average
  76.45% → 77.36%, none lost.

- `Broiler.Wpt` — a paged render prints each page on the box it is actually
  printed on, and emits only the pages the comparison asks for. Two things were
  missing and the first is not a page box at all: a print reftest often needs a
  page it does not want to compare, and says so with
  `<meta name="reftest-pages">` — `page-orientation-on-landscape-001-print`
  spells it out in the markup it renders ("Page 1. Not compared. Just bumps
  testing to page 2."), and its reference is a one-page document, so emitting
  both of the test's pages compares two pages against one and fails on the size
  alone. `WptReftestPages` reads the declaration (single pages, lists, `2-4`
  ranges) and `RenderPaged` emits only what it names. The second is `@page
  :first`, which describes exactly one page and could not be drawn while every
  page shared a box. The flow is still laid out against a single page area, so
  only page one's box may differ — sound for these tests because each forces its
  own break. Under `BROILER_WPT_PAGED_PRINT=1` `css/css-page` goes 128 → 132 of
  224, average 73.69% → 76.45%, four tests changing state and none lost;
  `css/css-break` does not move and the default unpaginated run is
  byte-identical.

- `Broiler.Wpt` — a paged render no longer stamps a page for a document that
  generates none. A root element with no box generates no page, so the sheet
  keeps none of its own paint; `RenderDecorated` has gated that on
  `GeneratesPageContent` since the page paint landed, but `RenderPaged` never
  asked, so `css-page/root-element-display-none-print` had a hotpink,
  red-bordered first page stamped against a deliberately blank reference. Under
  `BROILER_WPT_PAGED_PRINT=1` `css/css-page` goes 127 → 128 of 224, average
  73.24% → 73.69%, one test changing state and none lost; the default
  unpaginated run is untouched.

- `Broiler.Wpt` — `@page { margin: auto }` centres the page area instead of
  leaving every margin at zero. `auto` is a value of the margin property and the
  shorthand parser read it as a failure to parse, rejecting the whole
  declaration; the longhands did the same. Resolution now happens once per axis
  after every declaration is seen: with no `auto` the area plus its margins is
  the box and a declared `size` gives way (the over-constrained case
  `page-size-013-print` states, its reference writing the same page as
  `size: 300px 400px; margin: 50px`), and with an `auto` the box stands while the
  `auto` sides take the remainder — halved between two, taken whole by one, and
  signed, so an area larger than its box hangs off the edges rather than
  clamping. This closes no reftest: the unpaginated render consults the page box
  only when the `@page` paints, and none of the `page-margin-*` tests do. Under
  `BROILER_WPT_PAGED_PRINT=1`, which does use it, `css/css-page` goes 72.97% →
  73.24% average at an unchanged 127 of 224 passing, and the default run is
  byte-identical test-for-test.

- `Broiler.CSS` (**pending patch**, `patches/0002`) — a media query can tell it
  is being printed. `EvaluateMediaType` matched `screen` and `all`
  unconditionally, so `@media print` never applied to a document being printed,
  and Media Queries 4's `width`/`height` features evaluated against whatever
  surface the renderer allocated instead of the page area. `CssPagedMedia`
  carries both, thread-static and inert unless pinned, so a continuous render is
  byte-identical to before. The page area it carries is the *initial* one rather
  than the one `@page` declares, because a `@page` rule may itself sit inside a
  media query — `css-page/media-queries-001-print` declares
  `@page { size: 10in; margin: 2in }` and then asserts a query matching only
  between 4in and 5in wide and 2in and 3in tall. `Suspend()` keeps the context
  out of a nested browsing context, whose frame has its own viewport. Together
  with the already-pending absolute-length patch (`0001`, which that test needs
  because it states its assertion in inches) `css/css-page` goes 142 → 143 of 224
  reftests, 88.37% → 88.83% average, `css/css-break` unmoved, exactly one test
  changing state. The main-repo call sites sit behind a `BROILER_CSS_PAGED_MEDIA`
  file-existence probe, so the repo builds and renders identically against the
  pinned submodule pointer.

- `Broiler.Wpt` — the sheet takes the named page the document puts its content
  on. `WptPageBox` read only the unconditional `@page`, so
  `css-page/page-name-table-001-print` — a table on `page: square`, a
  `@page square` sizing the sheet 5in and painting it `#eee`, an unconditional
  `@page` painting red — rendered at the default size under red where its
  reference is a 5in `#eee` square, and scored 0.0%. The runner renders one sheet
  and that sheet is page one, so it now takes the box of the page the flow starts
  on: `EnumerateAppliedPageBlocks` yields the unconditional rules and then layers
  the used named rule over them, and both the geometry and the `@page` decoration
  follow because both read that one enumerator. **Exactly one** used name is the
  whole of the guard — a document naming two pages needs a per-page box that one
  surface cannot carry, so none is taken (`page-margin-auto-print` names six and
  is untouched), a named rule nothing uses is still ignored, and a pseudo-class
  selector is never read. `css/css-page` goes 141 → 142 of 224 reftests with the
  average 87.92% → 88.37%, `css/css-break` does not move, exactly one test
  changed state, and the golden-image score is unchanged.

- `Broiler.Wpt` — a `@page` rule's flow-relative margins and padding are read.
  CSS Paged Media 3 §3.2 lets the page box carry `margin-inline-start` and its
  seven siblings, and neither half of the runner's `@page` model understood them:
  `WptPageBox` switched on the physical margin longhands alone, so a
  flow-relative one left the margin at zero, and a flow-relative padding reached
  the decoration probe — a bare `<div>` whose writing mode is the initial one and
  whose containing block is not the page, so it resolved to the wrong side and
  took its percentage against the wrong box. `WptPageAxes` now resolves the
  page's writing mode — its own declaration where it makes one, the root
  element's otherwise, which is exactly what `css-page/page-box-008-print` and
  `-009-print` disagree about on purpose — and maps each flow-relative side to a
  physical one, with percentages taken against the page-box dimension that side
  runs along. The two tests go 4.0% → 6.7% and 67.0% → 79.8% against their own
  references, `css/css-page` 87.86% → 87.93% average with 140 of 223 passing
  either way, and `css/css-break` does not move: nothing regressed, and the
  golden-image score for every affected test is unchanged. What still separates
  them from their references is the sheet, not the ring — an unpaginated `-print`
  render keeps the runner's viewport instead of the page the document declares;
  see
  [`docs/wpt-rendering-gaps-open.md`](docs/wpt-rendering-gaps-open.md#a--print-document-renders-on-the-viewport-not-on-the-page-it-declares).

- A grid container is as wide as the columns it actually has. The shrink-to-fit inline size of
  a grid summed only the tracks named in `grid-template-columns`: implicit columns contributed
  nothing, `grid-auto-columns` was never consulted, and a grid with no template at all fell
  back to measuring inline content — 0, for a grid of empty boxes. So
  `<div style="display: inline-grid; grid-auto-columns: 15px; border: 1px solid">` holding one
  child at `grid-column: 3 / span 4` painted a 2px border and nothing else, where it is six
  15px tracks wide. Details in
  [`docs/wpt-reftests.md`](docs/wpt-reftests.md#a-grid-built-from-implicit-columns-was-as-wide-as-its-border).
  - The definite-width pass was already right, so the intrinsic path stopped answering
    separately: item collection and auto-placement are shared between the two, and the column
    count the size is built from is the one the real pass will resolve. Tracks past the
    template are sized from `grid-auto-columns`, and gaps are charged across all the columns
    rather than the template's. `grid-auto-columns` defaults to `auto`, whose size is its
    items', so a grid that does not declare a definite one is answered exactly as before.
  - Fixing the width exposed a placement bug behind it: a grid whose every track is a definite
    fixed length now runs the real track pass even when an item is a nested grid, flex or
    table container, so that child lands in the columns its `grid-column` asks for instead of
    being stretched across the container by the fallback. The gate it relaxes exists to
    distrust an item's measured height when a row is auto-sized — which cannot arise when no
    track takes its size from a measurement.
  - **On the reftest scoreboard this is 4 tests worse, and that is the change working.**
    Over 5 140 reftests in `css-grid`, `css-flexbox`, `css-sizing`, `css-tables`,
    `css-display`, `css-inline`, `css-writing-modes` and `css-position`: **2 494 → 2 490
    passing**, with exactly 10 tests moving at all. The four that flip —
    `css-grid/subgrid/repeat-auto-fill-002/-003/-004` and `orthogonal-writing-mode-005` — were
    passing because the test *and its reference* both collapsed to a 2px border on a blank
    page and so matched each other. Both sides render at their true width now, and the
    disagreement that was always underneath them is filed as its own gap: a subgrid resolves
    neither `repeat(auto-fill, <line-names>)` nor the name-plus-index lines the tests place
    items with. The `css-grid/grid-lanes` subset, which the gap entry named as the thing not
    to regress, is unchanged at 195 of 869 passing.

- A box with an `aspect-ratio` and a definite height is now that shape, and a `border` width may
  be written in a physical unit. Two unrelated defects, both found by asking why a whole WPT
  reftest family was failing uniformly rather than by working down the run's biggest-problems
  list. Over the whole reftest corpus (27 327 tests) they are **18 644 → 18 886 passing, +244 won
  against 2 lost**. Details in
  [`docs/wpt-reftests.md`](docs/wpt-reftests.md#a-definite-height-and-an-aspect-ratio-did-not-make-a-square).
  - **`aspect-ratio` only transferred one way.** The engine took an `auto` *height* from a width
    that had already filled the containing block, but never the inverse, so
    `<div style="height: 100px; aspect-ratio: 1/1">` painted a viewport-wide 100px band where
    every engine paints a 100px square. The transfer now supersedes the block-level stretch-fit,
    the CSS2.1 §10.3.7 inset equation and the float/abspos shrink-to-fit alike — each of those
    answers "how wide is a box with no width of its own?", which a ratio plus a definite height
    already answers. `min-height`/`max-height` clamp before the transfer and
    `min-width`/`max-width` after it, both in the box `box-sizing` names; the clamp order is now
    right for the grid-item caller that already existed too.
  - **`aspect-ratio: 1 / 2` parsed as no ratio at all** — the value is split on whitespace and a
    lone `/` token carries no number, so the whole declaration was rejected. `1/2` worked and
    `1 / 2` did not, and the spaced spelling is the one most of these tests use.
  - **`border: 72pt solid red` painted a 3px black line.** `CssLengthParser.ParseToPixels` left
    out CSS Values 3 §5.2's absolute units — `pt`, `pc`, `in`, `cm`, `mm`, `Q` — and answered
    `NaN`, which its callers read as "not a length". The `border` shorthand therefore filed the
    width under *colour*, dropping both the width and the declared colour; the longhand
    (`border-left-width: 72pt`) was right the whole time. A media query such as `(min-width: 8in)`
    and a container query with an absolute length were evaluating as invalid for the same reason.
    This one is **`patches/0001-resolve-the-absolute-length-units-in-parsetopixels.patch`** —
    `CssLengthParser` is in the `Broiler.CSS` submodule, whose remote is outside a
    this-repository session's push scope — and is listed in
    `scripts/apply-pending-wpt-patches.sh`, so the pixel suites exercise it on top of the pinned
    pointer. It is worth **+220 of the 244** on its own.
  - Both losses are `*-print` tests, and both are false passes ending rather than regressions:
    each states a `border` in inches on the test *and* on its reference, so neither side painted
    a border and the two matched. They render their borders now, and what separates them is the
    fragmentation the tests were written to measure, which needs the paged render to judge.

- WPT tests that need a user gesture run again. A test asks for one through
  `test_driver.bless(intent, action)`, which the conformance runner shims — there is no user in a
  headless run and nothing checks activation, so the shim just runs the action. But the shim went in
  *ahead* of the page's scripts and installed itself only if the name was free, and
  `/resources/testdriver.js` — inlined from the checkout like any other external script — then
  assigned `window.test_driver` wholesale. Upstream's `bless` appends a "This test requires user
  interaction" button and waits for a WebDriver click routed through `test_driver_internal`, whose
  in-tree implementation file is empty, so the promise never settled: **the action never ran, and the
  button was rendered into the screenshot.** The shim is now its own injected source, assigns
  unconditionally, and is appended to every `testdriver*.js` the runner inlines, so it wins whatever
  the page loads and in whichever order. The reftest suite goes **880 → 910 of 1258** across the
  directories holding a reftest that loads testdriver.js in a partial checkout, nothing regressed —
  `fullscreen/rendering` from 3/6 to 6/6, the rest customizable-`select`, `interestfor` and
  `css/css-shadow/part`. See
  [`docs/wpt-reftests.md`](docs/wpt-reftests.md#testdriverjs-overwrote-the-shim-that-drives-it).

- `https://www.mediawiki.org/` now renders like the same page in the reference browser: 38.9 % of
  pixels matched a Chromium capture of identical bytes at 1024×768 before this work, 82.7 % after,
  and the page is vertically aligned with the reference to the pixel. Everything behind that is a general engine defect that the Vector 2022 skin
  happened to expose; the full account, including what the remaining error is made of and what
  in it is not worth chasing, is in
  [`docs/mediawiki-vector-rendering.md`](docs/mediawiki-vector-rendering.md).
  - **Style.** A media feature value may be a math function (Media Queries 4 §2.4.1), and
    `calc(1120px - 1px)` did not parse — so 25 of the page's 76 `@media` blocks were malformed
    and their rules never cascaded, including the entire narrow-viewport branch that applies at
    1024px. `calc()` also gained the product tier it never had. The `list-style` shorthand is
    expanded (the longhands already worked), `:visited` no longer matches every `<a href>`, and a
    `display: none` child no longer makes its parent look like an inline container.
  - **Layout.** `float` and `clear` have no effect on a flex or grid item (CSS Flexbox §3), and a
    floated one was being taken out of flow — Vector's logo is a `display: flex` link whose icon
    and wordmark are both floated, so it laid out as nothing. Line boxes now avoid floats on both
    sides (CSS2.1 §9.5). A floated `display: table`/`flex`/`grid` box keeps its own width instead
    of the block algorithm's. `max-width: calc(100% - …)` against an indefinite basis is ignored
    rather than collapsing to zero (CSS Sizing 3 §5.1). A collapsed top margin is spent once —
    an empty box handing its margins to the next sibling had them applied twice — and the record
    that makes that work also makes the collapse transitive through a wrapper. An inline child's
    horizontal margins count toward max-content width, and an inline replaced element's vertical
    margins are part of its line (CSS2.1 §10.8.1); together those are why a thumbnail was scaled
    down to fit a figure 6px too narrow for it. A `vertical-align: middle` image no longer drives
    the line's baseline down by its own height.
  - **Text and images.** Generic font families resolve against the machine's installed fonts
    instead of one bundled face; `font-weight: bold` and `font-style: italic` now draw in the
    bold and italic faces rather than the regular one; `font-size` in `rem`/`em` is no longer 4/3
    too large; a bitmap drawn at a size other than its own is filtered instead of point-sampled;
    and an outset `box-shadow` is clipped out of its own border box (CSS Backgrounds 3 §7.1)
    rather than filling the element it is cast by.
  - **Scripting.** A `<script src>` whose URL carries `&amp;` is now entity-decoded, which is
    what MediaWiki's whole ResourceLoader bootstrap hung on — without it none of the skin's
    JavaScript ran at all. `document.readyState` and `readystatechange`, the `history` object,
    `PerformanceObserver`, `requestIdleCallback`, and `MediaQueryList`'s event listeners were
    each missing and each threw out of a module the skin loads.
  - Six of the fixes are in the `Broiler.CSS` and `Broiler.HTML` submodules, which this session
    could not push to, so they are patch files under [`patches/`](patches/README.md) and are
    listed in `scripts/apply-pending-wpt-patches.sh` for the pixel suites.
  - `mediawiki` joins the real-world render corpus, so the figure is reproducible with
    `python scripts/run-real-world-render-tests.py --sites mediawiki`.

- Broiler now says who is asking. `HttpClient` sends **no** `User-Agent` unless one is
  configured, and a request carrying none is not one every server will answer: Wikimedia's
  User-Agent policy replies `403 Forbidden` before content negotiation, before the redirect,
  before the page is even looked up. So `https://www.mediawiki.org/wiki/MediaWiki` failed
  instantly in both the CLI and the browser window — "Capture failed: Response status code
  does not indicate success: 403 (Forbidden)" — with nothing about the page involved. Any
  non-empty token is accepted there; what the policy will not have is silence. Full account in
  [`docs/mediawiki-user-agent-403.md`](docs/mediawiki-user-agent-403.md).
  - **It was every loader, not one.** Each of Broiler's loaders builds its own `HttpClient`, so
    each was independently unidentified, and a header set only where the failure was reported
    buys a page that loads and then fails again per resource. With just the document fixed, a
    `--diagnostic-dir` capture of that page still lost
    `load.php?modules=startup` — the bootstrap that pulls in every other module on the page — to
    the same `403`. The header now comes from one constant,
    `Broiler.Layout.Net.BroilerUserAgent`, applied at the document (`Broiler.Cli`), navigation
    (`Broiler.Browser.Core`), external-script (`Broiler.HtmlBridge.Core`), and
    stylesheet/sub-resource/`fetch()`/XHR (`Broiler.HtmlBridge.Dom`) loaders alike, plus the
    engine-baseline tool. A capture of the page goes from an instant failure to 13 resources and
    0 failures.
  - `Broiler.Layout` is the home because it is the one assembly every loader already reaches —
    including the `<link>`, `<img>` and web-font loaders inside the `Broiler.HTML` submodule,
    which reference it for `OfflineSubresources`. Their identical one-line fix ships as
    `patches/0002-say-who-is-asking.patch`; until it is applied and the pointer bumped, a build
    against the pinned pointer still renders that page unstyled and pictureless, because
    `HtmlRender` fetches a page's sheets through the submodule's loader rather than the bridge's.
  - `navigator.userAgent` reads the same constant instead of a second literal of the same string,
    so a page comparing what it was told with what its own `fetch()` reports gets one answer. The
    value it reports is unchanged.
  - Not only mediawiki.org: `tests/real-world-sites/sites.json` already tracked
    `https://en.wikipedia.org/wiki/Web_browser`, which refused an unidentified request for the
    same reason.

- `Broiler.HtmlBridge.Dom` — `document.currentScript` was not bound, so it read `undefined`
  and the idiomatic dereference threw. Google's tag-manager loader, served on
  `about.google` — where `google.com`'s "About" link leads — opens with
  `new URL(document.currentScript.src).searchParams` on its 14th line, so the missing
  property was `TypeError: Cannot get property src of undefined` four lines into the page's
  first script. The next two lines are that script's `const id` and `const cookieCategory`, so
  aborting there also left the cookie bar's callback reading `id` in its temporal dead zone
  and the page's analytics never initialised: one missing property, two reported failures.
  A capture of that page went from 18 JavaScript failures to 3.
  - **Which element it names is half the fix.** The bridge tracked the running script as an
    index into the document's `<script>` elements — for `document.write`'s insertion point —
    by pairing the *n*-th executed script with the *n*-th element. Those two lists only
    coincide when the document has no data block, no module and no `defer`, so on a page
    whose first `<script>` is a JSON-LD block (which is most of them) the first script that
    ran was attributed to the JSON-LD, whose `src` is absent. `ScriptElementMap` counts only
    the elements each bucket runs, classifying exactly as the extractors do.
  - The deferred bucket never set the index at all, so through every `<script defer>` on a
    page `currentScript` was `null` and `document.write` appended to `<body>` instead of
    writing at the script's position. Both hosts now set it across that bucket too.

- `Broiler.JS` — reading an exception's stack changed it. `JSException.JSStackTrace` both
  renders the JavaScript frames and *collects* them into the exception's own list, which is
  what keeps them printable once the context that threw is gone — so it has to collect once
  and render thereafter, and it did neither. An exception whose stack a host renders for
  several sinks reported the whole stack once per sink, and since a frame is walked at its
  *current* position, only the first copy carried the line it threw at: a function that
  failed at line 3 and rethrew from its `catch` came back as one line-3 frame followed by
  four line-4 ones, naming the handler as if it were a call site. It arrived in the
  `about.google` report above as five identical `at native in inline-0:line 14` lines over a
  JavaScript-side `stack` that correctly showed the one frame there was — the two halves of
  one report disagreeing is the tell. Waiting on a maintainer under `patches/`; find it by
  its commit subject, "Collect a JavaScript stack once, render it as often as asked".

- `Broiler.Cli` — a capture written as HTML (`--url`) compiled the document's data blocks as
  JavaScript, reporting a syntax error for content that was never JavaScript. The DOM path
  (`--capture-image`) has skipped them since `ScriptMimeType` was introduced and this one had
  not, so the same page produced a different failure through each mode: `about.google`'s
  JSON-LD block failed as `Unexpected token Colon: : at 1, 12`.

- `Broiler.JS` — `String(Math.pow(2, -25))` answered
  `2.980232238769531e-8`, which reads back as a *different* double, so a page that
  round-tripped a value through a string got a different value back than it put in.
  `Number::toString` owes the shortest string that reads back as the same Number, and
  .NET's `"R"` specifier — documented as unreliable — drops the seventeenth
  significant digit for this value. The short form is now verified by parsing and
  widened to 17 digits only where it fails, so everything that already round-tripped
  keeps its shortest form (`String(0.1)` is still `"0.1"`). Found by differentially
  fuzzing the engine against V8, along with `"abc".lastIndexOf("")` answering 2
  instead of 3 — the position clamps into `[0, length]`, not `[0, length - 1]`, and a
  negative position answered "not found" for the empty string, which is always found.

- `Broiler.HtmlBridge.Dom` — `location.replace(url)` was `TypeError: undefined is not a
  function`. The bridge defined the Location's URL components and none of its methods, so
  `assign`, `replace` and `reload` were all missing — and a missing method is not something a
  page feature-detects around: the TypeError is raised at the call and unwinds the rest of the
  calling function. Google Search reaches it on the branch its bot-detection bootstrap takes
  when `window.prs` is absent and the `SG_SS` cookie is already set. The three methods exist
  now and **do not navigate**: a capture renders the document it was given, and
  `--follow-first-link` is the explicit opt-in for going elsewhere, so each records the request
  into the diagnostics bundle and returns — which is what a browser that blocks a navigation
  does, and unlike a throw it leaves the calling script running. The URL is deliberately left
  alone with it: the document did not change, so neither did its URL. Two neighbours in the
  same object are fixed with them — `location` stringified to `[object Object]` rather than to
  its href (`"" + location`, `` `${location}` ``, `String(location)` all wrong), and `port` was
  undefined for every URL rather than empty on a scheme's default port and the number
  otherwise. `location.href = url` is the same operation under a different spelling (HTML
  §7.10.5 — the setter performs *location-object navigate*) and the one pages reach for most;
  it was a plain data property, so the write stuck and nothing else happened: the page believed
  it had left, the capture never knew it had been asked, and the URL then disagreed with the
  document still in hand. It is an accessor now, answering the document's own URL and routing
  the write where `assign()` goes. A frame's Location gets all of it too, being built
  separately.

- `Broiler.JS` (`8564eee2`) — a closure that a directly-evalled function created was
  handed none of the eval's bindings, so one level of nesting decided whether a name
  resolved: `f = eval("0,function(){ return function(){ return b; }; }")` gave `f()`
  the caller's `b` and `f()()` a `ReferenceError`. The eval's overlay is withdrawn when
  it returns, and the snapshot the *outer* function carries was not consulted when the
  inner one was built — although it is the only trace of that scope left. Google Search's
  bot-detection VM is exactly that shape, evaluating its opcode handlers with
  `function(X){return eval(X)}(src)` and building a closure inside them on nearly every
  step, so the challenge died on a `g is not defined` and the page rendered as the
  interstitial. Two neighbours dropped the same scope and are fixed with it: a function
  invoked from a builtin's callback (`Array.prototype.map`, `Set`/`Map.prototype.forEach`,
  a JSON reviver or replacer) had neither its eval bindings nor its captured `with` chain
  re-established, so the same closure worked when JavaScript called it and threw when a
  builtin did; and `typeof` answered `"undefined"` for a name the very next read produced
  a value for, because its non-throwing resolver never consulted the capture.

- `Broiler.Layout` — an `<img>` whose source came from `srcset` loaded nothing at all.
  The engine read the `src` attribute and nothing else, so a responsive image and every
  `<picture>` had no source and painted the missing-image border. HTML §4.8.4.3's *select
  an image source* now runs — "parse a srcset attribute", "parse a sizes attribute" and
  the `<picture>`/`<source>` walk with its `media` and `type` gates — and the candidate's
  pixel density is carried through to sizing, because it is as much of the algorithm's
  output as the URL: a `w` descriptor is not a size but a divisor, so
  `<img srcset="x.png 100w" sizes="400px">` lays a 100-pixel-wide bitmap out 400px wide,
  and a `2x` candidate is laid out at half the pixels it decodes to. The density is 1 for
  every image that did not come from a candidate list, which leaves the sizing path
  unchanged for them. `clamp()` in a `sizes` entry is a known deviation: the CSS length
  parser evaluates only `calc()`, `min()` and `max()`, so such an entry is treated as a
  parse error until that gains one.

- `Broiler.Layout` — an inline **replaced** element was aligned on its line by the ascent
  of a font it does not draw. CSS 2.1 §10.8 puts an atomic inline's baseline at its bottom
  margin edge, and only the `inline-block` half of that was implemented, so every image on
  a line was placed the same ~13px below the line's top whatever its height — images of
  two different heights came out sharing a top edge instead of a baseline. The line-box
  *height* code had always assumed the other rule (it extends a line below a tall image by
  the strut's descent precisely because the image's bottom is the baseline), so the two
  halves disagreed with each other rather than only with the spec.

- `Broiler.Layout` — an `<iframe>`/`<frame>` `src`, or an `<object data>`, that was
  document-relative *and* carried a query or a fragment loaded nothing: the whole URL was
  joined onto the containing directory, so the file it looked for was literally named
  `page.html?a=1` and the frame painted empty with no error. The root-relative branch had
  always stripped them — only the path names a file — and the two branches now share one
  helper, so they cannot disagree about the same URL depending on whether it begins with a
  slash. A `src` of only a query or a fragment addresses the containing document and
  still resolves to nothing.

- `Broiler.Layout` — an absolutely-positioned or fixed child of a **row** flex container
  was never laid out, and so never painted. `PerformFlexRowLayout` replaces the ordinary
  block-flow child loop wholesale, and that loop is the only thing that calls
  `PerformLayout` on a child; the flex path walks the same list but skips out-of-flow
  children, correctly excluding them from flex layout (CSS Flexbox §4.1) and then never
  laying them out anywhere else. The visible effect was not a misplaced box but a missing
  one: `body { display: flex }` holding a fixed backdrop and an absolutely-positioned
  panel painted neither, which is a common enough shape that it made a WPT test and its
  own reference agree with each other on a blank page. Out-of-flow children are now laid
  out after the flex lines, from the container's content-box start corner as their static
  position. Column containers were unaffected — they fall through to block flow, which
  already laid them out.

- `Broiler.Layout` — `flex-grow` did nothing at all along a column flex container's main
  axis. A column container has no flex algorithm here; its children stack through ordinary
  block flow, which never resolves flexible lengths, so a lone `flex-grow: 1` item in a
  100px-tall container stayed at its content height and a `height: 100%` child of it
  measured zero. The positive half of §9.7 now runs over that stack once the children are
  laid out, when — and only when — the container's main size is definite (a specified
  `height`, then clamped by `min-height`/`max-height`; a `min-height` alone leaves the main
  size content-based, and items correctly do not flex). Each grown item is laid out again
  at its target height rather than resized in place, so percentage-height descendants
  resolve against the flexed size. Shrinking is deliberately not implemented yet: it would
  need CSS Flexbox §4.5's automatic minimum size to avoid squashing content past its
  min-content size.

- `Broiler.Layout` — a CSS grid whose children are *all* absolutely positioned resolved no
  grid areas for them. The definite-track pass declined outright when no in-flow item was
  placed, so no track was sized and every abspos child fell back to the grid container's
  padding box instead of the grid area CSS Grid §9 makes its containing block. Two smaller
  defects sat behind it: the padding box's block extent was read from the container's
  track-derived height rather than its used height, so an `auto` grid line resolved against
  a box hundreds of pixels too short whenever the container had a definite height; and in
  `rtl` the resolved area was mirrored correctly and the item then placed at its *left*
  edge, where CSS2.1 §10.3.7's static position is the inline-start — the right — one.

- `Broiler.HtmlBridge` — `window.top` did not exist, so the unqualified `top` a page
  writes raised `ReferenceError: top is not defined`. Because `window` *is* the global
  object here, an unregistered member is not an undefined property but a reference
  error, and that aborts the entire `<script>` rather than the one statement — taking
  every function it would have defined and every listener it would have registered with
  it. `top` was the one member of the `self`/`parent`/`top` trio never registered on the
  main window: sub-documents got one from `SubWindowBinding`, and `WindowContextManager`
  dutifully saved and restored a global that had never been defined in the first place,
  so after the first frame ran the main document's `top` was `undefined` instead of
  missing. A framing check (`if (top != self)`) is among the first things a page's
  boilerplate runs, which is where this surfaced: google.com's One-Google-bar bundle died
  on it. A top-level window's `top` is now that window, as its `parent` and `self` already
  were, and it is bound to the global object so `top.foo()` reaches page globals the way
  `parent.foo()` does.

- `Broiler.JS`/`Broiler.Regex` (patch, awaiting a maintainer — see `patches/README.md`) —
  the pattern parser could not tell the end of a pattern from a U+0000 inside one. Its
  cursor reports "past the last character" by returning `'\0'` from `Peek()`, and every
  end-of-pattern test compared against that sentinel, so a pattern that merely *contained*
  a NUL appeared to end there. google.com's start page compiles a "no letters" character
  class whose first range runs from NUL to space, and it failed at the very first atom
  with `Unterminated character class`. The NUL is decoded by the time the parser sees it
  because the page builds the class with `new RegExp(<string>)` — the string literal's
  `\0` is consumed by the *string* grammar and never reaches the pattern grammar — so the
  spelling a regex literal preserves, `[\0-…]`, parsed correctly all along; the two are
  different inputs and only the decoded one was broken. It was not confined to character
  classes either: a NUL as an atom, as a range end, in a group name or inside `\p{…}`
  truncated the parse the same way. Every end-of-pattern test now goes through an explicit
  `AtEnd`. Separating the two also fixed the mirror-image bug at the real end of input: a
  pattern ending in a lone `\` had no guard on the path that reads the escaped character,
  so it ran off the end of the string and raised `IndexOutOfRangeException` in non-Unicode
  mode, where callers catch `RegexSyntaxException`. Nothing a page renders changes today —
  `JSRegExp.TryBuildBroilerForGaps` catches the parse failure and falls back to the .NET
  translator, so this pattern was mis-routed rather than fatal — but the exception was
  real, and a NUL-bearing pattern that *does* need Broiler.Regex's semantics now reaches
  it instead of silently falling back.

- `Broiler.Layout` — a layout pass that started while another one was still running over
  the same box tree threw `ArgumentException` ("An item with the same key has already been
  added") out of `CssLineBox.AssignRectanglesToBoxes`, which `CssBox.PerformLayout` caught
  as a layout error, so the block silently lost the rest of its lines and everything below
  it was placed from a half-finished pass. Layout calls back into the host while it flows —
  text measurement runs for every word, and an image load can complete on the same stack —
  and a host that re-enters `PerformLayout` from one of those callbacks lands inside an
  in-flight `CreateLineBoxes`. Because `CreateLineBoxes` empties `CssBox.LineBoxes` when it
  starts, the inner pass drops the line the outer pass is still filling and leaves its own
  assigned lines in the list; the outer pass then walks that list and assigns the first of
  them a second time. Projecting a line's rectangles onto its boxes is idempotent by
  nature, so it now overwrites rather than inserts, and the values written are the ones the
  surviving pass just recomputed. Seen in the browser on pages such as html5test.com, and
  not in a single-shot capture, which never re-enters.

- `Broiler.JS` (patch, awaiting a maintainer — see `patches/README.md`) — a closure created by a
  direct `eval` lost the eval site's bindings the moment the eval returned, so
  `eval("(function(){ return b; })")` threw `b is not defined` when the function it returned was
  called — even though `eval("b")` at the same spot read the same binding fine. A direct eval's
  scope is *lexical*: the closure keeps those bindings afterwards. They were made reachable by an
  overlay installed for the duration of the eval and withdrawn on return, which is right for code
  the eval runs and wrong for code the eval creates. A function created by directly-evalled code
  now captures them — as one created inside a `with` block already captured its with-chain — and
  re-establishes them for the duration of a call. The live bindings are captured rather than their
  values, so a later write by the enclosing function is visible to the closure and a write by the
  closure reaches the caller's binding instead of a fresh global. Consulted only after every
  ordinary scope has failed, on the read and write paths alike, so nothing that resolves today
  resolves differently. This is what made google.com's module loader
  (`function(e){return eval(e)}("0,function(){b(2,57,1,w)}")`, the result stored and invoked later)
  fail with `b is not defined`.

- `Broiler.JS` — `eval` and the
  `Function` constructor's body were all compiled as `vm.js`, one name for every program that
  has no script of its own. A stack trace through a module loader is then unreadable, because
  its frames cannot be attributed: `vm.js:1,14` and `vm.js:5060,25473` give no way to tell
  whether that is one program or two, and which it is decides whether the failing function was
  defined where it was called or somewhere else entirely. They are numbered now, the way
  devtools shows `VM123` — `vm1.js`, `vm2.js`, one name per compiled program. A script that
  already has a name keeps it; only the fallback changed. Re-evaluating identical source may
  reuse a cached compilation, and its name with it, which is right: the name still identifies
  exactly one piece of code.

- `Broiler.HtmlBridge` — CSS Font Loading (css-font-loading-3): `FontFace` and the
  `document.fonts` `FontFaceSet` did not exist, so `document.fonts` was undefined and
  `document.fonts.load(…)` a TypeError. That does not stop at the font code asking for it — on
  google.com the *first* inline script is a font preloader whose entire body is one
  `document.fonts.load` loop, so it died on its first statement. Both halves ship together:
  a set without the constructor is the shape that hid the `AbortSignal` gap, and
  `fonts.add(new FontFace(…))` needs both names. Broiler resolves fonts synchronously against
  what it has when it lays text out, so from a page's side nothing is ever in flight —
  `status` is `loaded`, `ready` is an already-resolved (and stable) promise, `check()` is true,
  and `load()` resolves rather than rejecting. The failure mode that matters is a promise that
  never settles, which would strand any page waiting behind `document.fonts.ready` before it
  renders. An absent or empty font shorthand is a `SyntaxError` as the spec requires; beyond
  that the shorthand is not parsed, so a malformed but non-empty font resolves rather than
  being rejected over a diagnostic Broiler cannot actually produce.

- `Broiler.HtmlBridge` — the CSSOM `CSS` namespace object (`CSS.supports()`, `CSS.escape()`)
  did not exist. An unqualified `CSS.supports(…)` is therefore a `ReferenceError`, which
  aborts the whole calling script rather than the one line; google.com's main bundle calls it
  unguarded, and it is where that bundle stopped once `AbortSignal` let it get that far.
  `escape()` implements the CSSOM algorithm exactly, including the parts that are easy to get
  wrong — a leading digit becomes a hexadecimal escape *with* its trailing space, a lone
  hyphen is escaped while `--` is not, and NULL is replaced rather than escaped. `supports()`
  answers from the CSS engine's own `@supports` evaluator, so a page gets one consistent
  answer whether it asks through the method or writes the rule; it is deliberately *not*
  implemented by round-tripping the declaration through a detached element's `style`, because
  Broiler's CSSOM stores declarations without validating them and that technique would answer
  "supported" to everything. The evaluator is exposed by a pending `Broiler.CSS` patch (see
  `patches/README.md`) and reached by name, so until that lands `supports()` reports `false` —
  the conservative direction, where a page takes the fallback it already carries.

- `Broiler.HtmlBridge` — `AbortSignal` (DOM §3.2) did not exist as an interface. The signal
  was an object literal built inside `AbortController`, so everything reached *through the
  controller* worked and the gap was invisible from that side — but the name itself was
  undefined, and a script that so much as mentions `AbortSignal` gets a `ReferenceError`,
  which aborts the whole script rather than the one line. google.com's main bundle does
  exactly that, and it is where the bundle stopped once the parser fix let it run at all.
  It is now a real constructor with a real prototype: `aborted`, `reason` and `onabort`
  stay *own* properties of each signal because the host reads them directly off the object,
  while the methods moved to the prototype, which is what makes `instanceof AbortSignal`
  (and `instanceof EventTarget`) true and lets the statics exist. Added with it:
  `AbortSignal.abort()`, `AbortSignal.timeout()` — whose reason is a `TimeoutError`, not an
  `AbortError`, so code that distinguishes "cancelled" from "took too long" can — and
  `AbortSignal.any()`, which is already aborted if any input is, so composing signals cannot
  miss an abort that happened first. Constructing one directly throws, as the spec requires.

- `Broiler.JS` (patch, awaiting a maintainer — see `patches/README.md`) — the parser
  rejected `!c++ && 1`. A postfix `++`/`--` belongs to the *operand* of a prefix unary
  operator (`!c++` is `!(c++)`, the grammar reaching it through
  `UpdateExpression : LeftHandSideExpression ++`, below
  `UnaryExpression : ! UnaryExpression`), but the postfix was taken only when no prefix
  operator had been parsed, leaving the `++` of `!c++` in the token stream. Invisible
  when the expression ends there — `!c++` and `(!c++)` both parsed — and fatal as soon
  as anything follows, since the stray token made the next operator unexpected. Every
  operator class was affected under every prefix operator: `!c++ || 1`, `!c++ + 1`,
  `!c++ === false`, `!c++ ? 1 : 2`, `-c++ && 1`, `~c++ && 1`, `typeof c++ && 1`,
  `void c++ && 1`. One syntax error rejects an entire script, and `!c++ && …` is the
  ordinary minified spelling of a run-once guard, so this kept google.com's 1.1 MB main
  bundle from compiling at all — it failed at line 466 over a single `++`, leaving
  nothing the page's largest script defines in existence. ASI is unaffected: the postfix
  still may not cross a line terminator, so `!c\n++d` stays two statements.

- `Broiler.HtmlBridge` — `btoa` and `atob`, the base64 pair on
  `WindowOrWorkerGlobalScope` (HTML §8.3), which the bridge did not provide at all. An
  unqualified `atob(…)` was a `ReferenceError`, and that aborts the whole script that
  called it rather than the one call — the failure mode google.com's script loader hit.
  Both work on *binary strings* rather than text: `btoa` treats each code point as one
  byte and throws `InvalidCharacterError` above U+00FF instead of mangling it, and
  `atob` returns a string built the same way, so the two round-trip arbitrary bytes.
  Decoding follows Infra's *forgiving-base64*, written out rather than delegated to
  `Convert.FromBase64String` because the two disagree exactly where pages notice: ASCII
  whitespace is stripped from anywhere, unpadded input decodes, and the one-character
  tail that cannot encode a byte is rejected rather than silently truncated.

- `Broiler.HtmlBridge` — `Element.dataset`, the HTML `DOMStringMap` view over an
  element's `data-*` attributes (HTML §3.2.6.6), which did not exist at all. A missing
  object is not a quiet failure here: reading through it throws, and a thrown error
  aborts the whole `<script>`. google.com's async-request module — `inline-6`, the code
  that runs *when a search is issued* — reads `b.dataset.ved` and writes ids back
  through the same map, so its absence stopped that script with `Cannot set property
  eqid of undefined` and took every later statement and listener with it. The map is a
  `Proxy` over the element's attributes rather than a snapshot object: a page may read
  an attribute the markup carries, overwrite it, **or invent one no attribute backs
  yet**, and a snapshot serves the first two while silently dropping the third onto a
  throwaway copy. Nothing is cached, so `getAttribute` and `dataset` cannot disagree;
  names map both ways (`dataset.fooBar` ↔ `data-foo-bar`); and `in`, `delete`,
  `Object.keys` and `JSON.stringify` all work, the latter two because the proxy answers
  `getOwnPropertyDescriptor` as well as `ownKeys` — answering only `ownKeys` enumerates
  as empty. Built on first read and memoized, so a document's thousands of elements do
  not each allocate a map that nothing asks for, and `el.dataset === el.dataset` holds.

- `Broiler.HtmlBridge` — `window` is the global object, as it is in a browser, so a
  page's `window.x = …` and its unqualified `x` name one property. They were separate
  objects, and identifier resolution consults only the global, so `window.google = _g`
  left the bare `google` a `ReferenceError` — which aborts the whole `<script>`, not
  just the statement. google.com bootstraps exactly that way *within one script*
  (`window.google=_g` in one IIFE, `google.sn='webhp'` in the next), so its namespace
  was never finished and every later script that named it died the same way: five
  `google is not defined` errors from one broken script, and a homepage with none of
  its script-driven content. The window→global mirror covered the between-scripts
  half by copying members across afterwards; nothing could cover the within-one-script
  half, because no host runs between the write and the read. The mirror and its public
  re-run `SyncWindowMembersOntoGlobal` are kept and return immediately when the two
  objects are one. Three things the split had hidden came with it: `frames` was
  registered twice with different shapes (a live getter on the window, a static
  snapshot on the global) and is now the accessor alone; `GetWindowOrigin` walked
  `parent` to inherit an `about:blank` document's origin and only terminated because
  the top window's `parent` used to be the bare global, so a top-level `about:blank`
  page now recursed 33 707 frames into a stack overflow; and the top window's origin
  can no longer be read back out of its `location`, because `RunWithWindowContext`
  swaps that to a frame's while the frame's scripts run — which is when a frame calls
  `parent.postMessage` — so it comes from the document through a new
  `IMessagingHost.PageOrigin` seam.
- `Broiler.HtmlBridge` — `fetch` resolves its input against the document's base URL
  (Fetch §5.4) instead of handing the string to `HttpClient` untouched. A relative
  request URI with no `BaseAddress` is not a request at all: `PrepareRequestMessage`
  throws `InvalidOperationException("An invalid request URI was provided…")` before
  anything reaches the wire. google.com hits it on every page view, beaconing timing to
  `/gen_204?atyp=i&…` through `navigator.sendBeacon`, which delegates to this `fetch`;
  so does any `XMLHttpRequest` opened on a path, because the XHR polyfill calls
  `fetch(this._url)`. It adopts the same `UrlResolver` that `Response.redirect`
  already used, which also makes `response.url` the resolved URL as the spec requires.
  A target that resolves against nothing is reported as an error `Response`, like any
  other failed fetch, rather than thrown — throwing would abort the calling script over
  one beacon.
- `Broiler.Layout` — `position: relative` now offsets an inline-level box. CSS 2.1
  §9.4.3's offset is visual, so it has to reach the box's words; `PerformLayout`
  applied it for every box it lays out, but an inline-level box is laid out by
  `CreateLineBoxes` and never goes through `PerformLayout` — so neither an inline
  `<span>` nor an inline `<img>` moved at all. **+73 reftests, none lost**, over a
  16 059-test sweep: `css/css-writing-modes` +68 (419 → 487 of 1139),
  `CSS2/box-display` +3, `CSS2/positioning` +1, `CSS2/visuren` +1. The family that
  gains is
  `abs-pos-non-replaced-v{lr,rl}-*`, whose *references* place their swatch with
  `position: relative`. Vertical containers are excluded — their words sit in the
  engine's rotated space, so a physical `left`/`top` arrives turned a quarter turn
  and needs a per-writing-mode mapping first.
- `Broiler.Layout` — a replaced element whose width is a percentage no longer
  ignores its stated height. CSS 2.1 §10.4 uses the intrinsic ratio to fill in a
  dimension left `auto`, not to overrule one the author stated, but the
  percentage-width branch of `MeasureImageSize` set the derive-the-height flag
  unconditionally — so `<img width="100%" height="50">` came out as tall as it was
  wide. **+89 reftests, none lost**, over a 16 059-test sweep of every directory
  that sizes a replaced element: `css/CSS2/backgrounds` +43 (204 → 247 of 339),
  `normal-flow` +22, `borders` +13, `positioning` +10. The tests were right all
  along — the bug was in the reference documents they are compared against, which
  draw their coloured band exactly that way.
- `Broiler.Documents` — the DOCX reader walked only the direct `w:p` children of
  `w:body`, so a document whose content lived inside a layout table (the shape CV
  and letterhead templates use) opened completely empty in Broiler.Writer. Block
  content is now walked recursively: tables, structured document tags, accepted
  revisions, and `mc:AlternateContent`.
- `Broiler.Documents` — the DOCX reader ignored `word/styles.xml`, so template
  documents (whose paragraphs carry no direct formatting at all) read as
  undifferentiated body text. Named paragraph and character styles now resolve
  through `w:docDefaults` and the `w:basedOn` chain, and `w:rFonts` theme
  references resolve against `word/theme/theme1.xml`.

### Added

- `Broiler.Documents.Model` — `InlineStyle.Capitalization` (`none`/`all caps`/
  `small caps`), extending the ADR 0014 inline style set. Capitalization is a
  display property: the text keeps the casing the author typed, so an
  open-and-save no longer rewrites it. Round-trips as DOCX `w:caps`/
  `w:smallCaps`, RTF `\caps`/`\scaps`, and CSS `text-transform`/`font-variant`.
- `Broiler.UI.RichEdit` — draws capitalization, synthesizing small caps by
  drawing letters typed in lower case as capitals at a reduced size, plus
  `RichEditCommand.AllCaps`/`SmallCaps` and formatting-code tokens
  `[All Caps ON]`, `[Small Caps ON]`, and `[Caps OFF]`.
- `Broiler.Documents` — DOCX read diagnostics: `docx.read.summary`,
  `docx.document.empty`, `docx.table.flattened`, `docx.block.unsupported`,
  `docx.limit.depth`, `docx.part.headerfooter`, `docx.styles.missing`,
  `docx.styles.unknown`, `docx.styles.cycle`, and `docx.styles.depth`.
- `Broiler.Cli` — `--convert-doc` prints every read diagnostic and the character
  count, not just a diagnostic count.
- `Broiler.Writer` — the status bar calls out a document that read as no content,
  and `BROILER_WRITER_DOCUMENT_LOG=1` writes the read diagnostics to stderr.

## [0.1.0-preview.1] — first preview

First packaged preview of the Broiler component libraries. **APIs are unstable**
and this preview is for evaluation, testing, and contribution — not production.

### Added

- NuGet packaging for the reusable component libraries (the `Broiler.Writer` and
  browser applications are not packaged):
  - `Broiler.DOM` — `Broiler.Dom`, `Broiler.Dom.Html`
  - `Broiler.CSS` — `Broiler.CSS`, `Broiler.CSS.Dom`
  - `Broiler.Layout`
  - `Broiler.Graphics` — core plus Direct2D / Linux / OpenGL / Vulkan backends
  - `Broiler.HTML` — renderer libraries
  - `Broiler.JS` — engine plus `Broiler.DateTime`, `Broiler.Regex`, Unicode data
  - `Broiler.Media` — core, audio, image, video (+ managed implementations)
  - `Broiler.Input` — device contracts and platform backends
  - `Broiler.UI` — retained-mode toolkit (control contracts + Standard implementations)
  - `Broiler.Documents` — model, RTF, DOCX, HTML, Markdown codecs
- Convenience meta-packages: `Broiler.Media.All`, `Broiler.Input.All`, `Broiler.UI.All`.
- Each package ships an icon, README, Apache-2.0 license expression, XML
  documentation, a symbol package (`.snupkg`), and SourceLink metadata.

### Notes

- Packages are licensed under Apache-2.0. `Broiler.HTML` and `Broiler.JS` include
  `THIRD_PARTY_NOTICES.md` (HTML Renderer BSD-3-Clause; Yantra JS Apache-2.0).
- Public release to NuGet.org is gated on the per-component `HUMAN_REVIEW.md`
  records; `Broiler.JS` review is still pending.

[Unreleased]: https://github.com/Broiler-Platform/Broiler/compare/nuget-v0.1.0-preview.1...HEAD
[0.1.0-preview.1]: https://github.com/Broiler-Platform/Broiler/releases/tag/nuget-v0.1.0-preview.1
