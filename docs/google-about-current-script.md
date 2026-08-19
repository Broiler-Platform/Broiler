# `about.google` — the `TypeError` on the first script

Navigating to Google's "About" link — `https://www.google.com/intl/de/about.html`, which redirects
to `https://about.google/` — was reported as

```
{TypeError: Cannot get property src of undefined}

at Item in …\Broiler.JavaScript.Runtime\JSUndefined.cs:line 41
at native in inline-0:line 14
at native in inline-0:line 14
at native in inline-0:line 14
at native in inline-0:line 14
at native in inline-0:line 14

    at Item:…\Broiler.JavaScript.Runtime\JSUndefined.cs:41,1
    at native:inline-0:14,0
```

Two separate defects are visible in that report, and it is worth saying up front which is which: the
`TypeError` is real and is ours, and the five repeated frames are not five frames.

## What `inline-0:14` is

`inline-0` is the first script the document executes, not the first `<script>` element it contains
(`ScriptLabel`, `src/Broiler.HtmlBridge.Core/Scripting/ScriptLabel.cs`). On this page the first
`<script>` is a JSON-LD data block, which nothing executes, so `inline-0` is the second element:

```
https://www.gstatic.com/marketing-cms/reviewed-scripts/gtm/gtm.js?id=GTM-WQZB4J&cookieCategory=2A
```

Google's tag-manager loader. Its line 14 is

```js
const params = new URL(document.currentScript.src).searchParams;
```

`document.currentScript` was not bound on the bridge's `document`, so it read `undefined` and the
`.src` dereference threw. Reproduce it with

```sh
dotnet run --project src/Broiler.Cli -- --capture-image https://www.google.com/intl/de/about.html \
  --output about.png --diagnostic-dir diag
```

and read `diag/javascript-errors.log`; `DocumentCurrentScriptTests` pins the same shape offline.

## It took the page's analytics with it

Aborting on line 14 skips lines 15 and 16, which are the loader's `const id` and `const cookieCategory`
declarations. Those `const`s are what the rest of the file closes over, so the next failure in the
same capture was

```
Event listener error: Cannot access 'id' before initialization
    at glueCookieNotificationBarLoaded in inline-0:line 21
    at Rj in inline-3:line 137
```

— the cookie bar, on load, calling back into a function whose bindings are in their temporal dead
zone because the statement that would have initialised them never ran. One missing property, two
reported failures, and no `dataLayer` on the page.

The whole capture went from 18 JavaScript failures to 3, and from 23 recorded sub-resources to 43:
with the loader running, GTM fetches its tag manager and the page gets further than it used to.

## `document.currentScript` has to name the right element

The bridge already tracked "which `<script>` is running" as `CurrentScriptIndex`, for
`document.write`'s insertion point. It was wrong on this page, and on most pages.

Each host holds two lists that look parallel and are not: the program texts it will evaluate, and
the document's `<script>` elements. The bucket lists hold only what executes; the element list holds
every `<script>` the parser built. Pairing them by position — which is what both hosts did — is only
correct when the two sets coincide. Here the first element is a JSON-LD block, so the first script
that ran was attributed to the JSON-LD, whose `src` is absent. A wrong element is worse than no
element: `document.currentScript.src` would have answered with the data block's missing `src` rather
than the loader's URL.

`ScriptElementMap` (`src/Broiler.HtmlBridge.Core/Scripting/ScriptElementMap.cs`) is the mapping, and
it classifies exactly as the extractors do when they fill the buckets: a data block — a `type` that
is neither a JavaScript MIME essence nor `module` — is not executed, a module is in neither classic
bucket, and `defer` picks between the two ahead of `async`. `ScriptEngine.RunPageScripts` and
`CaptureService.ExecuteScriptsWithDom` both use it, and both now also set the index across the
deferred bucket, which never set it at all — so through every `<script defer>` on a page
`currentScript` was `null` and `document.write` appended to `<body>`.

Two cases stay approximate, and both name a neighbouring script rather than a non-script:

* A source the host could not resolve — blocked by CSP, or a fetch that failed — is absent from the
  bucket but present in the element list. Nothing in the parsed elements says so.
* A host that hoists its `async` scripts to the end of the classic bucket rather than leaving them
  in document order pairs them differently from this list. `RenderingPipeline` does
  (`Scripts.Concat(AsyncScripts)`); `CaptureService` does not.

## The five frames are one frame, rendered five times

`JSException.JSStackTrace` both renders an exception's JavaScript frames and *collects* them: the
walker appends each frame to the exception's own trace list as it prints it, and that list is what
keeps the frames printable by `Exception.StackTrace` once the context that threw is gone. So the read
has to collect once and render thereafter, and it did neither — every read walked the live frames and
appended them again. An exception whose stack a host renders for several sinks reported the whole
stack once per sink.

The asymmetry in the report is the proof: the indented half is `error.stack`, a string captured once,
and it shows **one** `at native:inline-0:14,0`. The half above it is the list, re-read after it had
grown. There was never a recursion.

The repeats are also misdated. A frame is walked at its *current* position, so only the first copy
carries the line it threw at; each later copy carries wherever that frame had unwound to. A function
that failed at line 3 and rethrew from its `catch` at line 4 came back as one line-3 frame followed
by four line-4 frames — the handler rendered as if it were a call site. The reported frames are all
`line 14` only because a program-level frame stays where it is.

The fix is in the `Broiler.JS` submodule, so it waits under `patches/` until a maintainer applies
it. Its commit subject is **"Collect a JavaScript stack once, render it as often as asked"**; look
for that rather than for a patch number, which is recycled:

```sh
git -C Broiler.JS log --oneline --grep 'Collect a JavaScript stack once'
git -C Broiler.JS merge-base --is-ancestor <sha> HEAD && echo "live on CI"
```

## Found on the way, not fixed

* `new URL('data:text/javascript,…').protocol` answers `''`. `.href` is right, and the loader only
  needs `searchParams`, so nothing on this page depends on it — but a non-special scheme should still
  report its protocol.
* Three failures remain on a capture of this page: `TypeError: undefined is not a function` from the
  page's own `console.error`, a survey trigger that reports `{}`, and an unhandled rejection of
  `[object Object] is not iterable`. None of them is this bug, and none was investigated here.
