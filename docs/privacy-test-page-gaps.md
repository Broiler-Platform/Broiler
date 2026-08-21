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
`e272398`, on the `Privacy Test Pages` workflow. Nine pages, all nine compared.

| | |
| --- | --- |
| Probes Chromium answered | 116 |
| Broiler answered the same probe | 47 |
| **Gaps — Chromium answered, Broiler did not** | **69** (40 of them Broiler throwing) |
| Answered by neither engine | 132 |
| Answered only by Broiler | 0 |

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
diagnosis does not rest on the pages' own reporting. Issue #1755 indexes them.

| Probes | Capability | Issue |
| ---: | --- | --- |
| 4 | `fetch()`'s thenable is not a promise | #1745 |
| 6 | Web Audio absent | #1746 |
| 6 | WebGL absent | #1747 |
| 10 | `navigator` identity and hardware properties | #1748 |
| 13 | Window/screen geometry and `BarProp` | #1749 |
| 3 | `measureText` font-insensitive; no `crypto.subtle`, no `OffscreenCanvas` | #1750 |
| 6 | HTTP sub-resource, iframe and navigation attempts | #1753 |
| 21 | Single-API absences on the fingerprinting page (15 since resolved) | #1755 |
| — | Storage: IndexedDB, Cache API, service workers, CookieStore | #1751 |
| — | Request paths: WebSocket, EventSource, SharedWorker | #1752 |
| — | Corpus: four pages measure nothing in either engine | #1754 |

### `fetch()` does not return a promise — 4 probes (#1745)

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

### Web Audio is absent — 6 probes (#1746)

No `AudioContext`, `webkitAudioContext`, `OfflineAudioContext` or
`webkitOfflineAudioContext`. Chromium answers all six audio probes; Broiler
throws `ReferenceError` on the first reference.

### WebGL is absent — 6 probes (#1747)

`canvas.getContext('webgl')` returns `null` and `WebGLRenderingContext` does not
exist, so the probes throw `TypeError: Cannot get property … of null` rather
than taking a fallback path. The 2D context works.

### `navigator` is missing its identity and hardware surface — 10 probes (#1748)

`appCodeName`, `appName`, `appVersion`, `product`, `productSub`, `webdriver`,
`deviceMemory`, `hardwareConcurrency`, `maxTouchPoints` are all `undefined`;
`vendor` is `""`. `userAgent` answers, so this is a gap in the rest of the
interface. `navigator.connection`, `.permissions`, `.storage`, `.mediaDevices`,
`.mediaCapabilities` and `.userAgentData` are also absent, and their probes
throw `Cannot get property … of undefined`.

### Window and screen geometry, and the `BarProp` objects — 13 probes (#1749)

`screenX`, `screenY`, `screenLeft`, `screenTop`, `devicePixelRatio`,
`offscreenBuffering`, `screen.availLeft`, `screen.availTop` are `undefined`, and
the six `window.<bar>.visible` reads throw because `locationbar`, `menubar`,
`personalbar`, `scrollbars`, `statusbar` and `toolbar` do not exist.
`devicePixelRatio` is the one with consequences past this suite: canvas sizing
arithmetic against `undefined` yields `NaN`.

### Canvas text measurement, SubtleCrypto, OffscreenCanvas — 3 probes (#1750)

`measureText` returns the same width for every font family — a monospace face, a
proportional one and a font that is not installed all measure `580.32` for the
same string — so the font-detection probe reports nothing where Chromium finds
seven faces. `crypto` exists but `crypto.subtle` does not, and `OffscreenCanvas`
is undefined.

### Everything else on the fingerprinting page — 21 probes (#1755)

Single-API absences, each throwing where Chromium answers: `Notification`,
`RTCPeerConnection`, `MediaSource`, `performance.memory`, `console.memory`,
`screen.orientation`, `navigator.getBattery()`, `navigator.getGamepads()`,
`navigator.javaEnabled()`, `navigator.requestMediaKeySystemAccess`,
`HTMLVideoElement.canPlayType()`,
`PerformanceNavigationTiming.nextHopProtocol`, and the storage-quota shims.

**What was implemented.** Fifteen of them now resolve. Eleven answer:
`Notification.permission` (`denied` — the terminal state, since there is no
surface to show one on), `MediaSource.isTypeSupported()` and
`HTMLVideoElement.canPlayType()` (`false` and `""` — the vocabularies' own
"cannot be rendered", which is what an engine with no playback pipeline wired to
its HTML layer can honestly say), `performance.memory` and `console.memory` (one
object, read from the GC that is actually running the page's script, rounded to
100 KiB so the low bits carry no entropy),
`PerformanceNavigationTiming.nextHopProtocol` via a real
`performance.getEntries()`, `screen.orientation`, `navigator.javaEnabled()`,
`navigator.getBattery()`, and both storage-quota shims. Four more stop throwing
without moving the probe: `navigator.plugins`, `navigator.mimeTypes`,
`navigator.getGamepads()` and `navigator.requestMediaKeySystemAccess` answer with
an empty collection, which is the specified answer and which the suite classifies
as `empty` — the measurement cannot tell an empty answer from no answer, so their
baseline entries moved from `value` (they had been *throwing*, and a caught error
is stored where a value belongs) to `empty`. That reclassification is also why the
page's "tests carried out" count falls from 97 to 93 while eleven more probes
gained real answers.

**What was deliberately left.** Six are not shims to write:

| Absent | Why it is not stubbed |
| --- | --- |
| `RTCPeerConnection` | Needs an ICE/DTLS/SRTP stack, not an interface object. It sits with the other request paths in #1752, and the probe waits on a real ICE candidate that no stub can produce. |
| `AmbientLightSensor`, `Gyroscope`, `Magnetometer` | No sensor hardware path. Chromium's own answer here is an error object, so these are not gaps against the reference either. |
| `speechSynthesis` | No speech engine; `getVoices()` would return an empty list, and the interface's own detection is `'speechSynthesis' in window`, which a present-but-mute object answers wrongly. |
| `navigator.bluetooth` | Same: a `getAvailability()` that resolves `false` makes `'bluetooth' in navigator` true, which is the more misleading of the two answers. |
| `chrome.loadTimes()` | Chrome-proprietary. Defining a `chrome` global is an identity claim, not a capability. |
| `window.openDatabase` | WebSQL, removed from the web platform; Chromium no longer answers this probe either. |

### HTTP sub-resources, iframes and navigations never report back — 6 probes (#1753)

The four upgrade and redirect pages are the only ones outside fingerprinting
with gaps, and they fail the same way. Each asks the page to reach a plain-HTTP
URL — as a sub-resource, in an iframe, or as a navigation — and to record what
happened; Chromium records the URL it attempted, Broiler records nothing:

| Page | Probe | Chromium |
| --- | --- | --- |
| HTTPS upgrades | `upgrade-subrequest` | `"http://good.third-party.site/reflect-headers"` |
| HTTPS upgrades | `upgrade-iframe` | `"http://good.third-party.site/privacy-protections/https-upgrades/frame.html"` |
| HTTPS upgrades | `upgrade-navigation` | `"http://good.third-party.site/privacy-protections/https-upgrades/frame.html"` |
| HTTPS upgrade loop protection | `upgrade-navigation` | `"http://good.third-party.site/privacy-protections/https-loop-protection/http-only.html?start"` |
| AMP loop protection | `rewrite-amp` | `"http://good.third-party.site/privacy-protections/amp-loop-protection/amp-only.html?amp=1&start"` |
| Storage blocking | `browser cache` | `"884"` |

These are the *attempt*, not the upgrade policy: the value is what the page
asked for, recorded when the load settles. So the gap is that the attempt never
completes or never calls back, not that Broiler upgrades differently — and
whether Broiler should upgrade plain HTTP at all is a product decision this
suite does not take.

### Storage and request paths — the two pages behind them (#1751, #1752)

Direct feature detection, rather than the pages' probe values:

| Present | Absent |
| --- | --- |
| `localStorage`, `sessionStorage`, `document.cookie`, `fetch`, `XMLHttpRequest`, `navigator.sendBeacon`, `Worker` | `indexedDB`, `caches`, `navigator.serviceWorker`, `cookieStore`, `navigator.storage`, `WebSocket`, `EventSource`, `SharedWorker`, `RTCPeerConnection` |

`indexedDB` and `WebSocket` bound whole classes of site — offline storage and
anything live — well beyond what this corpus measures.

## What the comparison ruled out

Not every empty cell is a gap, and this is the half of the result that was not
available before:

- **132 probes were answered by neither engine**, and no probe was answered only
  by Broiler. Those 132 are not evidence about Broiler and are reported
  separately so they cannot be mistaken for a backlog.
- **The storage-blocking page measures nothing as configured.** The corpus asks
  for `?retrive`, which reads back what a previous `?store` visit wrote; the
  suite loads each page once in a fresh context, so nothing was written.
  Chromium's payload for that URL is as empty as Broiler's. Its twenty `empty`
  probes have been carried as if they were twenty platform gaps; they are a
  corpus-definition problem (#1754), and the real storage gaps are the ones in
  the table above.
- **Four pages cannot be measured in CI at all.** Held open for their full
  60-second timeout, Chromium still answered nothing on request-blocking (23
  probes), blocking-behaviour (29), GPC (5) or surrogates (7), and the
  diagnostics say why in each case: the tracker WebSocket the request pages wait
  on is refused at the handshake (`wss://bad.third-party.site/block-me/web-socket`
  → HTTP 40x) and a `fetch` is aborted, so those pages never commit their
  values; Chrome does not implement Global Privacy Control, so its five probes
  are legitimately null; and the surrogates page needs a blocker extension to
  substitute the tracker script it loads — Playwright's Chromium has none, and
  the raw `google-analytics.com/analytics.js` request is CORS-refused. **68 of
  the 132 "neither" probes are these four pages** (#1754). No number Broiler
  produces on them, today or after the gaps above are closed, would be comparable to
  anything.
- **Broiler-only answers were checked, not celebrated.** In the run before the
  error-shape fix, five probes looked like Broiler answering where Chromium did
  not; every one was Broiler's own thrown error. After the fix there are none.

## Caveats

The reference is headless Chromium under automation, so a few of its values
reflect that (`navigator.webdriver` is `true`, permission states differ). The
comparison is about whether a probe was *answered*, not about the value, so this
does not affect a gap — but do not read the reference column as "what a user's
Chrome would say".

The corpus is live. Probe ids appear, are renamed and disappear upstream without
a Broiler change, so a group's count here is a snapshot; the workflow's
`gaps.md` is the current one.

## Reproducing it

```sh
python scripts/run-privacy-test-pages.py --pages fingerprinting
```

That captures the Chromium reference and runs Broiler over the same page, and
writes `gaps.md` beside the full report under `artifacts/privacy-test-pages/`.
The Chromium half needs the pinned Playwright under `tests/wpt` (`npm ci`, then
`npx playwright install chromium`); `--no-reference` runs Broiler alone.
