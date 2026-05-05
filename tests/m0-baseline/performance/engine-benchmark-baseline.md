# Engine Benchmark Baseline

- Generated: 2026-05-05T09:21:02.7253631Z

| Metric | Unit | Mean | Median | Min | Max | Notes |
|---|---|---:|---:|---:|---:|---|
| `js.startup` | ms | 2.914 | 2.513 | 0.823 | 7.642 | Create a fresh JSContext and evaluate a trivial expression |
| `js.micro` | ms | 3.560 | 3.788 | 1.823 | 7.085 | Evaluate a hot arithmetic loop in one context |
| `html.parse` | ms | 3.861 | 1.706 | 1.365 | 13.499 | HtmlContainer.SetHtml on the baseline sample document |
| `html.layout` | ms | 5.048 | 3.508 | 3.172 | 14.508 | HtmlContainer.SetHtml + PerformLayout on the baseline sample document |
| `html.paint` | ms | 325.048 | 264.327 | 257.519 | 838.356 | HtmlContainer.PerformPaint after layout on the baseline sample document |
| `html.raster` | ms | 286.414 | 285.767 | 284.115 | 294.534 | Render and PNG-encode the baseline sample document |
| `bridge.dom-call` | ns/op | 2238.079 | 1763.250 | 1717.100 | 5241.700 | Repeated document.getElementById().getAttribute() calls |
| `bridge.mutation` | ns/op | 818681.583 | 795240.000 | 693577.800 | 1084894.600 | Repeated textContent mutations through the DOM bridge |
