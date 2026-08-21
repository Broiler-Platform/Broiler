# Privacy test page gaps: what Chromium answers and Broiler does not

This is the reading of the first two-engine runs of the
[privacy test page suite](privacy-test-pages.md), which drives every page
through Chromium (Playwright) and through Broiler from the same manifest and the
same expressions, and compares the two `results` payloads probe by probe.

It exists to answer one question the suite could not answer before: **which of
its empty cells are Broiler's to fix.** Live numbers belong in each run's
`gaps.md`; this page records what the first comparison found, how it was
verified, and what was opened from it.

> Coverage, not privacy. A probe Chromium answers and Broiler does not is a
> platform gap. Agreement means Broiler carried the probe out — never that the
> value it produced is the right privacy answer for a shipping browser.

## The run

Chromium `147.0.7727.15` through Playwright `1.59.1`, against Broiler at commit
`47a60e5`, on the `Privacy Test Pages` workflow. Nine pages, all nine compared.

| | |
| --- | --- |
| Probes Chromium answered | 116 |
| Broiler answered the same probe | 47 |
| **Gaps — Chromium answered, Broiler did not** | **69** (40 of them Broiler throwing) |
| Answered by neither engine | 133 |

The gaps are almost entirely on the fingerprinting page (63 of 69), because it
is the page whose probes are synchronous reads of the platform surface. The
request- and storage-driven pages contribute few gaps for a different reason,
covered under [what the comparison ruled out](#what-the-comparison-ruled-out).

## Two things the comparison had to learn first

Both were found by disbelieving a flattering number, and both are fixed in the
runner:

**A caught error is not an answer.** These pages run each probe in a `try` and
store the error object where the result belongs, so
`{"name": "ReferenceError", "message": "webkitOfflineAudioContext is not
defined"}` is, to a naive reader, a perfectly good value. The first comparison
reported 92 probes in parity on the fingerprinting page; 40 of them were Broiler
throwing. The comparison now recognizes that shape and counts it as unanswered
on either side.

**A page that has not started yet holds very still.** Several pages seed one
entry per probe the moment they start and fill the values in as third-party
requests complete. A capture that stops when the results array stops changing
therefore stops after ~2 s with a reference full of nulls — the worst possible
failure here, because every probe Broiler misses then reads as *"Chromium could
not do this either."* The capture now refuses to settle until at least one probe
has answered, and waits out the page's own timeout otherwise.

## The gaps, by the capability behind them

Each group was reproduced directly against `Broiler.Cli --evaluate-page`, so the
diagnosis does not rest on the pages' own reporting.

### `fetch()` does not return a promise — 4 probes

`fetch`'s `then` invokes the callback and returns **itself**, so a chained
`.then` receives the original `Response` again:

```js
fetch(url).then(r => r.json()).then(d => /* d is the Response, not the body */)
```

`await` works; the `.then(…).then(…)` form does not. The four `headers - *`
probes on the fingerprinting page read a reflect endpoint in exactly that form,
so they report nothing while the request itself succeeds. This is the one gap in
the list that is a *bug* rather than an unimplemented feature, and the only one
whose blast radius is every page that chains off `fetch`.

### Web Audio is absent — 6 probes

No `AudioContext`, `webkitAudioContext`, `OfflineAudioContext` or
`webkitOfflineAudioContext`. Chromium answers all six audio probes; Broiler
throws `ReferenceError` on the first reference.

### WebGL is absent — 6 probes

`canvas.getContext('webgl')` returns `null` and `WebGLRenderingContext` does not
exist, so the probes throw `TypeError: Cannot get property … of null` rather
than taking a fallback path. The 2D context works.

### `navigator` is missing its identity and hardware surface — 10 probes

`appCodeName`, `appName`, `appVersion`, `product`, `productSub`, `webdriver`,
`deviceMemory`, `hardwareConcurrency`, `maxTouchPoints` are all `undefined`;
`vendor` is `""`. `userAgent` answers, so this is a gap in the rest of the
interface. `navigator.connection`, `.permissions`, `.storage`, `.mediaDevices`,
`.mediaCapabilities` and `.userAgentData` are also absent, and their probes
throw `Cannot get property … of undefined`.

### Window and screen geometry, and the `BarProp` objects — 13 probes

`screenX`, `screenY`, `screenLeft`, `screenTop`, `devicePixelRatio`,
`offscreenBuffering`, `screen.availLeft`, `screen.availTop` are `undefined`, and
the six `window.<bar>.visible` reads throw because `locationbar`, `menubar`,
`personalbar`, `scrollbars`, `statusbar` and `toolbar` do not exist.
`devicePixelRatio` is the one with consequences past this suite: canvas sizing
arithmetic against `undefined` yields `NaN`.

### Canvas text measurement, SubtleCrypto, OffscreenCanvas — 3 probes

`measureText` returns the same width for every font family — a monospace face, a
proportional one and a font that is not installed all measure `580.32` for the
same string — so the font-detection probe reports nothing where Chromium finds
seven faces. `crypto` exists but `crypto.subtle` does not, and `OffscreenCanvas`
is undefined.

### Everything else on the fingerprinting page — 21 probes

Single-API absences, each throwing where Chromium answers: `Notification`,
`RTCPeerConnection`, `MediaSource`, `performance.memory`, `console.memory`,
`screen.orientation`, `navigator.getBattery()`, `navigator.getGamepads()`,
`navigator.javaEnabled()`, `navigator.requestMediaKeySystemAccess`,
`HTMLVideoElement.canPlayType()`,
`PerformanceNavigationTiming.nextHopProtocol`, and the storage-quota shims.

### Storage and request paths — the two pages behind them

Direct feature detection, rather than the pages' probe values:

| Present | Absent |
| --- | --- |
| `localStorage`, `sessionStorage`, `document.cookie`, `fetch`, `XMLHttpRequest`, `navigator.sendBeacon`, `Worker` | `indexedDB`, `caches`, `navigator.serviceWorker`, `cookieStore`, `navigator.storage`, `WebSocket`, `EventSource`, `SharedWorker`, `RTCPeerConnection` |

`indexedDB` and `WebSocket` bound whole classes of site — offline storage and
anything live — well beyond what this corpus measures.

## What the comparison ruled out

Not every empty cell is a gap, and this is the half of the result that was not
available before:

- **133 probes were answered by neither engine.** They are not evidence about
  Broiler and are reported separately so they cannot be mistaken for a backlog.
- **The storage-blocking page measures nothing as configured.** The corpus asks
  for `?retrive`, which reads back what a previous `?store` visit wrote; the
  suite loads each page once in a fresh context, so nothing was written.
  Chromium's payload for that URL is as empty as Broiler's. Its twenty `empty`
  probes have been carried as if they were twenty platform gaps; they are a
  corpus-definition problem, and the real storage gaps are the ones in the table
  above.
- **Broiler-only answers were checked, not celebrated.** Where Broiler produced
  a value and Chromium did not, the value was Broiler's own thrown error, which
  is why the comparison now reads those as unanswered.

## Caveats

The reference is headless Chromium under automation, so a few of its values
reflect that (`navigator.webdriver` is `true`, permission states differ). The
comparison is about whether a probe was *answered*, not about the value, so this
does not affect a gap — but do not read the reference column as "what a user's
Chrome would say".

The corpus is live. Probe ids appear, are renamed and disappear upstream without
a Broiler change, so a group's count here is a snapshot; the workflow's
`gaps.md` is the current one.
