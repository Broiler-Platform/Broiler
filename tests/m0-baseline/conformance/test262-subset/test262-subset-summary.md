# Test262 Subset Baseline

- Generated: 2026-05-05T09:26:21.5207738Z
- Repository: `tc39/test262` @ `d0c1b4555b03dd404873fd6422a4b5da00136500`
- Total: 6
- Passed: 4
- Failed: 2
- Pass rate: 66.7 %
- Baseline comparison: no regression (baseline 4 passed → current 4 passed)

| Test | Result | Notes |
|---|---|---|
| `Promise.resolve is a function` | PASS |  |
| `JSON.stringify uses toJSON return values` | FAIL | Expected [false] but got {} |
| `Map constructor without iterable does not call set` | PASS |  |
| `Set constructor with empty iterable does not call add` | PASS |  |
| `Array.isArray recognises proxied arrays` | FAIL | Array.isArray(new Proxy([], {})) must return true |
| `String.prototype.trim works for booleans` | PASS |  |
