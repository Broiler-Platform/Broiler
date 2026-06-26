# Engine Benchmark Baseline

- Generated: 2026-06-25T06:50:22.4995461Z
- Gated metrics: `bridge.mutation`, `html.raster`, `js.startup`

| Metric | Unit | Mean | Median | Min | Max | Notes |
|---|---|---:|---:|---:|---:|---|
| `js.startup` | ms | 1.995 | 1.567 | 1.399 | 4.780 | Create a fresh JSContext and evaluate a trivial expression |
| `js.micro` | ms | 6.798 | 6.590 | 6.251 | 8.786 | Evaluate a hot arithmetic loop in one context |
| `html.parse` | ms | 6.703 | 1.464 | 1.039 | 79.927 | HtmlContainer.SetHtml on the baseline sample document |
| `html.layout` | ms | 2.649 | 2.650 | 2.182 | 3.203 | HtmlContainer.SetHtml + PerformLayout on the baseline sample document |
| `html.paint` | ms | 173.376 | 159.404 | 134.744 | 365.222 | HtmlContainer.PerformPaint after layout on the baseline sample document |
| `html.raster` | ms | 190.515 | 184.886 | 161.776 | 223.452 | Render and PNG-encode the baseline sample document |
| `css.parse` | ns/op | 98091.583 | 103087.500 | 64564.000 | 131882.000 | Parse the CSS Phase 0 stylesheet with the renderer parser |
| `css.selector-match` | ns/op | 111219.075 | 127242.750 | 57582.400 | 204416.100 | Run repeated Selectors Level 4 matches through querySelectorAll |
| `css.computed-style` | ns/op | 231812.683 | 211465.400 | 173949.800 | 373220.600 | Resolve and read a cached computed style through the bridge |
| `css.invalidation` | ns/op | 385127.067 | 383600.200 | 280820.400 | 439472.000 | Invalidate class-dependent style and recompute it through the bridge |
| `css.renderer-style-apply` | ms | 1.333 | 1.248 | 1.132 | 2.712 | Parse HTML and apply the renderer cascade for a style-heavy document |
| `bridge.dom-call` | ns/op | 5030.546 | 4735.825 | 3592.050 | 7270.600 | Repeated document.getElementById().getAttribute() calls |
| `bridge.mutation` | ns/op | 1099027.367 | 1108983.400 | 921150.400 | 1260281.000 | Repeated textContent mutations through the DOM bridge |
| `bridge.serialize` | ms | 6.870 | 1.942 | 1.300 | 43.705 | Attach and serialize the bridge-owned DOM |
| `bridge.render-handoff` | ms | 151.188 | 148.874 | 133.719 | 175.953 | Serialize the bridge-owned DOM and reparse it for raster rendering |
| `bridge.typed-render-handoff` | ms | 7.779 | 6.656 | 3.300 | 16.614 | Hand the canonical DOM directly to layout without serialization or reparsing |
