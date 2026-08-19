# `mediawiki.org` — the instant `403 Forbidden`

Opening `https://www.mediawiki.org/wiki/MediaWiki` in either entry point failed before anything was
parsed:

```
$ dotnet run --project src/Broiler.Cli -- --url https://www.mediawiki.org/wiki/MediaWiki --output mw.html
Capture failed: Response status code does not indicate success: 403 (Forbidden).
```

Nothing about the page is involved. The request never carried a `User-Agent`.

## Why a missing header is not a mild version of an unrecognised one

`HttpClient` sends **no** `User-Agent` at all unless one is configured — it is not a header .NET
fills in. Wikimedia's [User-Agent policy][policy] refuses a request that carries none, and refuses
it early: the status is decided before content negotiation, before the redirect, before the page is
looked up. Any non-empty token is accepted, so this is not a matter of naming the right browser:

| `User-Agent` sent | `https://www.mediawiki.org/wiki/MediaWiki` |
| --- | --- |
| *header absent* | `403` |
| `Broiler/1.0` | `200` |
| `Mozilla/5.0 (Windows NT 10.0; Win64; x64) Broiler/1.0` | `200` |
| a current Chrome token | `200` |

Reproduce the boundary without Broiler at all:

```sh
curl -s -o /dev/null -w '%{http_code}\n' -H 'User-Agent:' https://www.mediawiki.org/wiki/MediaWiki   # 403
curl -s -o /dev/null -w '%{http_code}\n' -A 'Broiler/1.0'  https://www.mediawiki.org/wiki/MediaWiki   # 200
```

## It was every loader, not one

The document is the request that produced the reported message, and fixing only that one produces a
page that loads and then fails a second time per resource — each of Broiler's loaders builds its own
`HttpClient`, so each was independently unidentified. After the document was fixed, a capture with
`--diagnostic-dir` reported:

```
Diagnostics: 2 JavaScript failure(s), 12 resource(s)

## Resources that failed to load

- `https://www.mediawiki.org/w/load.php?lang=en&modules=startup&only=scripts&raw=1&skin=vector-2022`
  — Response status code does not indicate success: 403 (Forbidden).
```

`load.php?modules=startup` is the bootstrap that pulls in every other module on the page, so the
whole skin's JavaScript was gone for the same reason the page had been. The photographs are on
`upload.wikimedia.org`, which applies the same policy, and so are the `<link>` stylesheets.

The fix is therefore one constant — `Broiler.Layout.Net.BroilerUserAgent` — applied at every
construction site, rather than a header set where the bug was noticed:

| Loader | Where | Fetches |
| --- | --- | --- |
| `CaptureService.CreateHttpClient` | `src/Broiler.Cli` | the CLI's document (and any followed link) |
| `BrowserApp.CreatePageHttpClient` | `src/Broiler.Browser.Core` | every browser-window navigation |
| `ScriptExtractionService.SharedHttpClient` | `src/Broiler.HtmlBridge.Core` | external `<script src>` |
| `ResourceLoader.SharedClient` | `src/Broiler.HtmlBridge.Dom` | stylesheets, sub-resources, `fetch()`, XHR |
| `Program.HttpClient` | `src/Broiler.Engines.Baseline` | test262 sources and dependency metadata |
| `StylesheetLoadHandler.SharedHttpClient` | `Broiler.HTML` (submodule) | `<link rel=stylesheet>` on the render path |
| `ImageDownloader.SharedHttpClient` | `Broiler.HTML` (submodule) | `<img>` |
| `HtmlContainerInt.TryLoadRemoteFont` | `Broiler.HTML` (submodule) | web fonts |

`Broiler.Layout` is the home because it is the one assembly all of them already reach — including
the three inside the `Broiler.HTML` submodule, which reference it for `OfflineSubresources`. That
keeps the submodule side of the fix to one line per loader.

### The same string script is told

`navigator.userAgent` already reported `Mozilla/5.0 (Windows NT 10.0; Win64; x64) Broiler/1.0` from
a literal of its own. It now reads the constant, so a page that compares what it was told with what
its own `fetch()` reports gets one answer. The value is unchanged; only the number of copies is.

## What the header does not fix

The three submodule loaders are delivered as a patch under `patches/` (the submodule remote is
outside this session's push scope — see `patches/README.md`), so until a maintainer applies it and
bumps the pointer, a build against the pinned pointer still fetches `<link>` sheets, images and web
fonts unidentified. Concretely, on `mediawiki.org` that is the difference between a styled render
and a bare document flow — `HtmlRender` fetches the page's stylesheets through the submodule's
`StylesheetLoadHandler`, not through the bridge's `ResourceLoader`.

The document, the external scripts, `fetch()` and XHR are all in the main repository and are fixed
without the patch, which is what makes the reported failure go away.

## It was not only mediawiki.org

`tests/real-world-sites/sites.json` already tracks `wikipedia-browser`
(`https://en.wikipedia.org/wiki/Web_browser`), which answers `403` to an unidentified request for
exactly the same reason. Of the eight sites in that suite it is the only other one that does; the
rest were served anyway, which is why the policy had not been noticed.

[policy]: https://meta.wikimedia.org/wiki/User-Agent_policy
