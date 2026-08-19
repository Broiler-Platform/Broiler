#!/usr/bin/env python3
"""Measure a Broiler build's headroom against Google Search's bot-check watchdog.

Google serves an interstitial before search results ("Please click here if you are
not redirected within a few seconds") whose JavaScript solves a challenge in an
obfuscated bytecode VM, sets the `SG_SS` cookie and only then navigates to the
results.  That VM times itself: once per few dispatches it samples the clock, and
a sample longer than `1 << 14` = 16384 ms counts as a stall.  On the *second*
stall it overwrites its own interpreter-context pointer with the boolean `true`,
and the next opcode that dereferences it dies with

    TypeError: Cannot get property length of undefined

which is reported against `JSUndefined.cs` and an anonymous `vmNN.js` frame.  The
engine is not at fault at the throw site; it is simply slow enough that the page
gave up on it.  See docs/google-search-post-consent-challenge.md.

This script measures the margin.  It fetches (or reads) the interstitial, rewrites
the VM's own timing check through an `eval` hook so each sample is reported, replays
the page under `Broiler.Cli --capture-image`, and prints the slowest slice against
the 16384 ms budget.

Usage:
    python scripts/measure-google-challenge-headroom.py
    python scripts/measure-google-challenge-headroom.py --configuration Debug
    python scripts/measure-google-challenge-headroom.py --page saved-challenge.html

Google does not always serve the interstitial.  When it does not, the script says
so and exits 2; save a page that does and pass it with --page.
"""

from __future__ import annotations

import argparse
import http.server
import re
import shutil
import socketserver
import subprocess
import sys
import tempfile
import threading
import urllib.parse
import urllib.request
from pathlib import Path

# The VM's own threshold: it stalls on `<sample> >> 14 > 0`.
WATCHDOG_MS = 1 << 14

REPO_ROOT = Path(__file__).resolve().parent.parent

# Installed ahead of the page's own scripts.  The VM is built by an inline script as
# a joined string array and handed to an indirect `eval`, so replacing `window.eval`
# is enough to see — and rewrite — its source before it compiles.  Both rewrites
# preserve the VM's semantics exactly: `__uv` and `__probe` are called for effect
# and the comparison they sit in keeps its original operands.
INSTRUMENTATION = """<script>
(function(){
  var realEval = window.eval;
  var slices = 0, samples = 0, maxSlice = 0, patched = 0, vmStart = 0, solvedAt = 0;
  window.__uv = function(){ slices++; };
  window.__probe = function(delta){ samples++; if (delta > maxSlice) maxSlice = delta; return 0; };
  // W(a) navigates away once the challenge is solved; window.pr short-circuits that so the
  // measurement survives to be reported, and tells us the challenge actually succeeded.
  window.pr = function(){ solvedAt = Date.now(); };
  window.__report = function(tag){
    console.log("CHALLENGE " + tag +
                " patched=" + patched +
                " solved=" + (solvedAt ? 1 : 0) +
                " slices=" + slices +
                " samples=" + samples +
                " maxSliceMs=" + maxSlice.toFixed(1) +
                " vmWallMs=" + (vmStart ? ((solvedAt || Date.now()) - vmStart) : -1));
  };
  window.eval = function(source){
    var text = (typeof source === "string") ? source : String(source);
    if (text.indexOf("document.hidden==0") < 0) return realEval(source);
    var rewritten = text
      .replace(/&&document\\.hidden==0,([A-Za-z_$][\\w$]*)\\.([A-Za-z_$][\\w$]*)==4\\)/,
               "&&document.hidden==0,(window.__uv(),$1.$2)==4)")
      .replace(/,([A-Za-z_$][\\w$]*)\\)>>14>0/,
               ",$1)>>14>(window.__probe($1),0)");
    patched = (rewritten !== text) ? 1 : 0;
    vmStart = Date.now();
    return realEval(rewritten);
  };
  setTimeout(function(){ window.__report("interim"); }, 2000);
  setTimeout(function(){ window.__report("final"); }, 20000);
})();
</script>
"""


def fetch_challenge(query: str) -> str:
    url = f"https://www.google.com/search?q={urllib.parse.quote(query)}"
    request = urllib.request.Request(url, headers={
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Broiler/1.0",
        "Accept": "text/html,application/xhtml+xml",
    })
    with urllib.request.urlopen(request, timeout=30) as response:
        return response.read().decode("utf-8", "replace")


def is_challenge_page(html: str) -> bool:
    # The interstitial carries the SG cookie name its script sets, and the inline VM
    # whose throttling check this script rewrites.
    return "SG_SS" in html and "document.hidden==0" in html


def instrument(html: str) -> str:
    head = re.search(r"<head[^>]*>", html, re.IGNORECASE)
    if not head:
        raise SystemExit("page has no <head> to install the eval hook into")
    return html[: head.end()] + INSTRUMENTATION + html[head.end() :]


def serve(directory: Path) -> tuple[socketserver.TCPServer, int]:
    class Handler(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *a, **kw):
            super().__init__(*a, directory=str(directory), **kw)

        # The page beacons to /gen_204 and asks for assets this directory does not
        # have; those are expected and say nothing about the measurement.
        def log_message(self, *_a):
            pass

    class Quiet(socketserver.TCPServer):
        allow_reuse_address = True

    server = Quiet(("127.0.0.1", 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server, server.server_address[1]


def run_capture(url: str, configuration: str, workdir: Path) -> list[str]:
    diagnostics = workdir / "diagnostics"
    command = [
        "dotnet", "run", "--project", str(REPO_ROOT / "src" / "Broiler.Cli"),
        "-c", configuration, "--",
        "--capture-image", url,
        "--output", str(workdir / "capture.png"),
        "--diagnostic-dir", str(diagnostics),
    ]
    subprocess.run(command, cwd=REPO_ROOT, check=False,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=900)
    log = diagnostics / "console.log"
    return log.read_text("utf-8", "replace").splitlines() if log.exists() else []


def parse_report(lines: list[str]) -> dict[str, str] | None:
    best = None
    for line in lines:
        marker = line.find("CHALLENGE ")
        if marker < 0:
            continue
        fields = dict(
            part.split("=", 1)
            for part in line[marker + len("CHALLENGE "):].split()
            if "=" in part
        )
        # The later report supersedes the earlier one.
        best = fields
    return best


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--page", type=Path,
                        help="a saved interstitial to replay instead of fetching one")
    parser.add_argument("--query", default="broiler",
                        help="search term to request when fetching (default: broiler)")
    parser.add_argument("--configuration", default="Release",
                        help="build configuration to measure (default: Release)")
    parser.add_argument("--keep", action="store_true",
                        help="keep the working directory and print its path")
    args = parser.parse_args()

    html = args.page.read_text("utf-8", "replace") if args.page else fetch_challenge(args.query)

    if not is_challenge_page(html):
        print("The page served is not the bot-check interstitial "
              "(no SG_SS cookie and no VM throttling check in it).")
        print("Nothing to measure. Retry, or save a page that is one and pass --page.")
        return 2

    workdir = Path(tempfile.mkdtemp(prefix="broiler-challenge-"))
    try:
        site = workdir / "site"
        site.mkdir()
        (site / "challenge.html").write_text(instrument(html), "utf-8")

        server, port = serve(site)
        try:
            lines = run_capture(f"http://127.0.0.1:{port}/challenge.html",
                                args.configuration, workdir)
        finally:
            server.shutdown()

        report = parse_report(lines)
        if report is None:
            print("The capture produced no measurement. Console output was:")
            print("\n".join(lines) or "  (empty)")
            return 1
        if report.get("patched") != "1":
            print("The eval hook saw the VM but could not rewrite its timing check; "
                  "Google has re-spun the obfuscation and the patterns in this script "
                  "need updating.")
            return 1

        max_slice = float(report["maxSliceMs"])
        headroom = WATCHDOG_MS / max_slice if max_slice > 0 else float("inf")
        print(f"configuration : {args.configuration}")
        print(f"challenge     : {'solved' if report.get('solved') == '1' else 'NOT solved'}")
        print(f"VM wall time  : {report['vmWallMs']} ms")
        print(f"slices        : {report['slices']} (clock samples: {report['samples']})")
        print(f"slowest slice : {max_slice:.1f} ms")
        print(f"watchdog      : {WATCHDOG_MS} ms per slice, two stalls poison the VM")
        print(f"headroom      : {headroom:.1f}x")
        if headroom < 10:
            print("\nLess than 10x margin. A heavier challenge, a slower machine or a "
                  "debug build can cross the threshold, and the page then fails with "
                  "'TypeError: Cannot get property length of undefined'.")
        return 0
    finally:
        if args.keep:
            print(f"\nworking directory: {workdir}")
        else:
            shutil.rmtree(workdir, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
