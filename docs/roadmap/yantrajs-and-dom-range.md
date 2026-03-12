# Roadmap: YantraJS Engine Improvements & DOM Range Operations

**Date:** 2026-03-12  
**Current Acid3 Score:** 83/100  
**Previous:** [acid3-compliance-v5.md](../acid3-compliance-v5.md)

---

## Executive Summary

17 Acid3 subtests remain failing. This roadmap categorizes them into three tiers:

| Tier | Category | Tests | Effort | Score Impact |
|------|----------|-------|--------|-------------|
| **1** | DOM Range operations | 9, 12, 13 | Medium–High | +3 |
| **2** | DOM/CSS feature gaps | 0, 2, 4–5, 46, 72, 80 | Medium | +7 |
| **3** | YantraJS engine limitations | 84, 88, 89, 90, 93 | High (engine core) | +5 |
| — | Infrastructure/env-only | 64, 69 | N/A (not fixable in current test env) | +2 |

**Maximum achievable score:** 95/100 (100/100 requires YantraJS engine patches + HTTP test env)

---

## Tier 1: DOM Range Operations (Tests 9, 12, 13)

### Current State

The Range API in `DomBridge.Traversal.cs` implements:
- ✅ `createRange()`, `cloneRange()`, `selectNode()`, `selectNodeContents()`
- ✅ `setStart()`, `setEnd()`, `setStartBefore()`, `setEndAfter()`, etc.
- ✅ `cloneContents()`, `deleteContents()`, `toString()`
- ✅ `insertNode()`, `surroundContents()`, `compareBoundaryPoints()` (basic)
- ⚠️ `extractContents()` — basic implementation, fails on cross-node partial extraction
- ❌ Range boundary-point adjustment on DOM mutations
- ❌ Text node splitting with boundary-point updates

### Test 9: `extractContents()` with Partial Text Nodes

**Acid3 code:**
```javascript
// Structure: <h1>Hello <em>Wonderful</em> Kitty</h1><p>How are you?</p>
r.setStart(t2, 6);  // t2 = "Wonderful" — start at offset 6 ("ful")
r.setEnd(p, 0);     // end at start of <p>

var f = r.extractContents();
// Expected fragment:
//   <h1><em>ful</em> Kitty</h1>  ← cloned h1+em wrapper, "ful" text, original t3 moved
//   <p></p>                      ← cloned empty p
```

**What the spec requires:**
1. Find the *common ancestor* of start and end containers
2. For the *start path*: clone ancestor nodes down to the start container; split the start text node at `startOffset`; the *extracted* portion ("ful") goes into the cloned subtree; the *retained* portion ("Wonder") stays in the original
3. *Fully contained* siblings between start and end paths are **moved** (not cloned) to the fragment
4. For the *end path*: clone ancestor nodes down to end container; move/clone children before `endOffset`
5. Update the range to collapse to the extraction point

**Current bug:** The `extractContents()` implementation in lines 752–935 of `DomBridge.Traversal.cs` has partial cross-node support but:
- Does not correctly split text nodes at partial offsets when the start container is a text node *inside* a nested element (the `em > "Wonderful"` case)
- The `ExtractStartPath` / `ExtractEndPath` helpers may not clone the full ancestor chain correctly
- The fragment structure doesn't match spec expectations (cloned wrappers vs moved nodes)

**Implementation tasks:**

- [ ] **T1.1** Implement spec-compliant `extractContents()` per [DOM Living Standard §Range.extractContents](https://dom.spec.whatwg.org/#dom-range-extractcontents):
  - [ ] Find common ancestor container (existing `FindCommonAncestor`)
  - [ ] Handle case: start container == end container (text split or child slice)
  - [ ] Handle cross-container case with proper ancestor path cloning
  - [ ] Split start text node: keep prefix in original, put suffix in cloned wrapper
  - [ ] Move fully-contained intermediate siblings to fragment
  - [ ] Split end text node: move prefix to cloned wrapper, keep suffix in original
  - [ ] Collapse range to start after extraction
- [ ] **T1.2** Add regression test: `Range_ExtractContents_Cross_Node_Partial_Text`
- [ ] **T1.3** Validate test 9 passes with the Acid3 harness

**Estimated effort:** 4–6 hours

---

### Test 12: Range Boundary-Point Adjustment on `insertNode()` with Text Splitting

**Acid3 code:**
```javascript
// <p> has children: t1="12345", t2="ABCDE"
r.setStart(p.firstChild, 2);  // start in "12345" at offset 2
r.setEnd(p.firstChild, 3);    // end in "12345" at offset 3 (selects "3")

r.insertNode(p.lastChild);    // insert t2 at range start → splits t1
// After: p.childNodes = ["12", t2="ABCDE", "345"]
// Range should contain "ABCDE" at start (per spec mutation rules)
```

**What the spec requires:**
1. `insertNode()` splits the start text node at `startOffset` (already implemented)
2. After split: range `startContainer` should be the *parent* of the split, `startOffset` should point to the *inserted node*'s index
3. The inserted node becomes part of the range
4. `endContainer` / `endOffset` must adjust for the split text mutation

**Current bug:** The `insertNode()` at line 962 correctly splits the text node but does **not update the range boundary points** after the split. The `startContainer` still references the original (now truncated) text node, and `endOffset` is stale.

**Implementation tasks:**

- [ ] **T1.4** After `insertNode()` text splitting, update range boundary points:
  - [ ] Set `startContainer` to the parent of the split
  - [ ] Set `startOffset` to the index of the inserted node
  - [ ] If `endContainer` was the same text node, adjust `endOffset` to account for the split
  - [ ] Recalculate `collapsed` state
- [ ] **T1.5** Add regression test: `Range_InsertNode_Updates_Boundaries_After_TextSplit`
- [ ] **T1.6** Validate test 12 passes with the Acid3 harness

**Estimated effort:** 2–3 hours

---

### Test 13: Range Boundary-Point Adjustment on DOM `removeChild()`

**Acid3 code:**
```javascript
// <body> has child: <p>12345</p>
r.setStart(p.firstChild, 2);  // start in text "12345" at offset 2
r.setEnd(doc.body, 1);        // end at body child index 1

doc.body.removeChild(p);      // remove <p> from DOM
// Range should collapse: start=body,0  end=body,0
```

**What the spec requires:**
Per [DOM Living Standard §Removing steps for Ranges](https://dom.spec.whatwg.org/#concept-range-bp):
- When a node is removed, if the range's start or end container is a descendant of the removed node, the boundary point must be set to (parent, index-of-removed-node)
- If the start or end container is a sibling after the removed node, its offset decrements
- After adjustment, if start > end, collapse to start

**Current state:** `removeChild()` in `DomBridge.JsObjects.cs` simply removes the child from the parent's `Children` list and nulls the `Parent` reference. It has **no awareness of active Range objects** — there is no range registration or mutation callback system.

**Implementation tasks:**

- [ ] **T1.7** Add a range registration system:
  - [ ] Track active `Range` objects in the `DomBridge` (e.g., `List<WeakReference<RangeState>>`)
  - [ ] Each Range stores its boundary points in a shared `RangeState` object
  - [ ] When `removeChild()` / `insertBefore()` / `appendChild()` mutate the DOM, iterate registered ranges and adjust boundary points per spec
- [ ] **T1.8** Implement boundary-point adjustment for `removeChild`:
  - [ ] If `startContainer` is a descendant of the removed node: set `startContainer = parent`, `startOffset = index`
  - [ ] If `endContainer` is a descendant of the removed node: same
  - [ ] If `startContainer == parent` and `startOffset > index`: decrement
  - [ ] Same for `endContainer`/`endOffset`
  - [ ] Recalculate `collapsed`
- [ ] **T1.9** Add regression test: `Range_Collapses_When_Ancestor_Removed`
- [ ] **T1.10** Validate test 13 passes with the Acid3 harness

**Estimated effort:** 4–6 hours

---

## Tier 2: DOM/CSS Feature Gaps (Tests 0, 2, 4–5, 46, 72, 80)

### Test 0: `:last-child` Pseudo-Class + `pre-wrap` (CSS)

**Issue:** `getComputedStyle().whiteSpace` returns `normal` instead of `pre-wrap` for the new last child after the original last child is removed. The CSS rule `#instructions:last-child { white-space: pre-wrap }` isn't re-evaluated after DOM mutation.

**Tasks:**
- [ ] **T2.1** Ensure `BuildComputedStyleObject()` re-evaluates CSS selectors against the *current* DOM state (not cached)
- [ ] **T2.2** Verify `:last-child` matching in `MatchesSelector()` checks `el.Parent.Children[^1]`

**Estimated effort:** 2–3 hours

---

### Test 2: NodeIterator DOM Mutation During Iteration

**Issue:** NodeIterator expectation count is off by one, suggesting the iterator doesn't handle DOM mutations (node removals/insertions) during traversal.

**Tasks:**
- [ ] **T2.3** Implement NodeIterator pre-removal steps per spec: when a node is removed that's the reference node, advance the reference to the next/previous node
- [ ] **T2.4** Add mutation awareness to `BuildNodeIterator()` — register the iterator for removal notifications similar to Range registration

**Estimated effort:** 3–4 hours

---

### Tests 4–5: NodeIterator/TreeWalker Object Identity

**Issue:** `assertEquals(i.nextNode(), document.getElementsByTagName('h1')[0])` fails because the JSObject returned by `nextNode()` and the one from `getElementsByTagName()` are different JS objects wrapping the same DOM element. The `!=` comparison checks reference identity.

**Root cause:** Both paths call `ToJSObject()` which uses `_jsObjectCache` (a `Dictionary<DomElement, JSObject>`). The cache should ensure identity. However, `document.getElementsByTagName()` in `Registration.cs` iterates `_elements` (flat list) while `GetDocumentOrderNodes()` traverses `.Children` — if the same DomElement is in both, the cache should return the same JSObject.

**Tasks:**
- [ ] **T2.5** Investigate whether `_elements` ordering matches document order for `document.write()` elements
- [ ] **T2.6** Ensure `document.getElementsByTagName()` returns elements in document order (tree traversal) rather than `_elements` insertion order
- [ ] **T2.7** Verify `ToJSObject()` cache is being used correctly in all code paths

**Estimated effort:** 2–3 hours

---

### Test 46: `@media` Viewport Queries

**Issue:** CSS `@media all and (min-height: 1em)` checks require a viewport model. Broiler currently has no viewport dimension concept, so viewport-dependent media queries always fail.

**Tasks:**
- [ ] **T2.8** Add viewport dimensions to `DomBridge` (default: 0×0 for Acid3's iframe-based test document)
- [ ] **T2.9** Implement basic media query evaluation in `BuildComputedStyleObject()` for `min-height`, `max-height`, `min-width`, `max-width`
- [ ] **T2.10** Support `element.style` changes affecting the viewport (Acid3 sets `style="height: 100px; width: 100px"` on the iframe)

**Estimated effort:** 4–6 hours

---

### Test 72: Dynamic `<style>` Affecting Image Height in Sub-Documents

**Issue:** Sub-document `doc.images[0].height` returns `undefined` instead of the value from the stylesheet. Sub-documents created via `doc.open()/write()/close()` need their style blocks to affect `getComputedStyle()` for elements within that sub-document.

**Tasks:**
- [ ] **T2.11** Ensure `BuildSubDocument()` collects style blocks from the sub-document and builds a CSS model
- [ ] **T2.12** Implement `images[n].height` property that resolves via `getComputedStyle()` against the sub-document's styles
- [ ] **T2.13** Handle dynamic style text changes (`ownerNode.firstChild.data = "..."`) — re-parse styles

**Estimated effort:** 4–6 hours

---

### Test 80: `document.links` Collection Ordering

**Issue:** `document.links[1]` can't find the dynamically-created `linktest` `<a>` element. The `_elements` flat list doesn't include dynamically-created elements (via `createElement` + `appendChild`).

**Tasks:**
- [ ] **T2.14** Change `document.links` in `Registration.cs` to use DOM tree traversal (like `CollectMatching`) instead of `_elements` iteration — but ensure this doesn't regress other tests
- [ ] **T2.15** Alternatively, ensure `appendChild()` / `insertBefore()` add elements to `_elements` if not already present

**Estimated effort:** 2–3 hours

---

## Tier 3: YantraJS Engine Limitations (Tests 84, 88, 89, 90, 93)

These failures are in the YantraJS JavaScript engine itself (the `yantra-1.2.295/` vendored fork). Fixes require modifying engine internals.

### Test 84: `(-0).toExponential(4)` → `"-0.0000e+0"` instead of `"0.0000e+0"`

**Root cause:** `JSNumberPrototype.ToExponential()` at line 114 of `JSNumberPrototype.cs` uses `nv.ToString(format)` which in .NET preserves the negative sign for `-0.0`. ECMAScript specifies that negative zero should format as `"0.0000e+0"` (positive format).

**Fix:**
- [ ] **T3.1** In `ToExponential()`, add check: `if (JSNumber.IsNegativeZero(nv)) nv = 0.0;` before formatting

**File:** `yantra-1.2.295/YantraJS.Core/Core/Number/JSNumberPrototype.cs:114`  
**Estimated effort:** 30 minutes

---

### Test 88: `\u002b` Unicode Escape Not Considered Parse Error in Identifiers

**Issue:** `\u002b` is U+002B (`+`), which is not a valid identifier character. YantraJS's `FastScanner` should reject it as a parse error when used in an identifier context, but instead it silently accepts it (or crashes differently).

**Root cause:** `FastScanner.cs` at line 624+ handles `\u` escape sequences in identifiers but doesn't validate that the decoded character is actually a valid `IdentifierPart` per ECMAScript.

**Fix:**
- [ ] **T3.2** In `FastScanner.ScanUnicodeCodePointEscape()` or the identifier scanning logic, validate that the decoded code point passes `Character.IsIdentifierStart()` / `Character.IsIdentifierPart()` check
- [ ] **T3.3** If validation fails, throw a parse error (SyntaxError)

**File:** `yantra-1.2.295/YantraJS.Core/FastParser/FastScanner.cs:624`  
**Estimated effort:** 2–3 hours

---

### Test 89: Orphaned Bracket `]` in Regular Expression Literal

**Issue:** YantraJS doesn't treat an orphaned `]` (not inside a character class) as a parse error in a regex literal. ECMAScript requires this to be a SyntaxError in strict mode or when not in a character class context.

**Root cause:** `FastScanner.ReadCommentsOrRegExOrSymbol()` at line 868 doesn't track character class depth correctly.

**Fix:**
- [ ] **T3.4** In the regex scanning loop, track `classMarker` state (already exists in the commented-out `Scanner.cs` code at line 1268)
- [ ] **T3.5** When a `]` is encountered outside a character class context, and the pattern is not otherwise valid, raise a SyntaxError

**File:** `yantra-1.2.295/YantraJS.Core/FastParser/FastScanner.cs:868`  
**Estimated effort:** 2–3 hours

---

### Test 90: Regex Backreference `/(\3)(\1)(a)/` Matching

**Issue:** The regex `/(\3)(\1)(a)/` should match `'cat'` (because `\3` forward-references group 3 which matches `a`, and `\1` backreferences group 1 which matched the empty string from the forward reference). YantraJS delegates to .NET's `System.Text.RegularExpressions.Regex` which handles backreferences differently.

**Root cause:** .NET's regex engine treats forward references to unmatched groups as errors or non-matching, while ECMAScript treats them as matching the empty string.

**Fix:**
- [ ] **T3.6** In `JSRegExp.CreateRegex()`, pre-process backreferences: replace forward references (referencing groups not yet defined at that point) with empty-string matchers
- [ ] **T3.7** This requires analyzing group numbers vs position in the pattern — complex regex rewriting

**File:** `yantra-1.2.295/YantraJS.Core/Core/RegExp/JSRegExp.cs:437`  
**Estimated effort:** 4–8 hours

---

### Test 93: `FunctionExpression` Semantics — Named Function in Expression Context

**Issue:** ECMAScript specifies that `function Identifier(...)` in expression context creates a binding for `Identifier` only within the function body, not in the enclosing scope. YantraJS leaks the binding.

**Root cause:** The parser/compiler in `FastParser` treats named function expressions the same as function declarations, adding the name to the enclosing scope.

**Fix:**
- [ ] **T3.8** In the parser's function expression handling, ensure the function name is bound only in the function's own scope (not the enclosing scope)
- [ ] **T3.9** This likely requires changes to `FastParser.cs` scope management

**File:** `yantra-1.2.295/YantraJS.Core/FastParser/`  
**Estimated effort:** 4–6 hours

---

## Infrastructure / Environment-Only Failures

### Test 64: `object.data` URI Resolution (`file://` vs `http://`)

**Issue:** `obj1.data` doesn't resolve to an absolute `http://` URL because the test runs with a `file://` base URI. This is a test-environment issue.

**Workaround:** Run the Acid3 test with `http://acid3.acidtests.org/` as the base URL instead of `file://`.

- [ ] **T4.1** Update `Acid3_Phase6_Score_Validation` test to use `http://acid3.acidtests.org/` as URL

**Estimated effort:** 15 minutes

---

### Test 69: External Iframe Loading (Timeout/Retry)

**Issue:** The test loads external files (`linktest.html`) via iframe `src` and uses a retry mechanism. Broiler's `CaptureService` doesn't have a real HTTP server, so external iframe loading times out.

**Workaround:** Would require an in-process HTTP server serving the Acid3 support files.

- [ ] **T4.2** Consider adding a lightweight test HTTP server (e.g., `HttpListener`) to serve files from `acid/acid3/` during tests
- [ ] **T4.3** Alternatively, document as unfixable in offline test environment

**Estimated effort:** 4–6 hours (if implementing HTTP server) or N/A (if documenting)

---

## Implementation Priority & Schedule

### Phase A: Quick Wins (Score +2–3, 1–2 days)

| Task | Test | Effort | Impact |
|------|------|--------|--------|
| T3.1 | 84 | 30 min | +1 |
| T4.1 | 64 | 15 min | +1 |
| T2.1–T2.2 | 0 | 2–3 hr | +1 |

### Phase B: DOM Range Core (Score +3, 2–3 days)

| Task | Test | Effort | Impact |
|------|------|--------|--------|
| T1.1–T1.3 | 9 | 4–6 hr | +1 |
| T1.4–T1.6 | 12 | 2–3 hr | +1 |
| T1.7–T1.10 | 13 | 4–6 hr | +1 |

### Phase C: DOM/CSS Features (Score +5–7, 3–5 days)

| Task | Test | Effort | Impact |
|------|------|--------|--------|
| T2.3–T2.4 | 2 | 3–4 hr | +1 |
| T2.5–T2.7 | 4–5 | 2–3 hr | +2 |
| T2.8–T2.10 | 46 | 4–6 hr | +1 |
| T2.11–T2.13 | 72 | 4–6 hr | +1 |
| T2.14–T2.15 | 80 | 2–3 hr | +1 |

### Phase D: YantraJS Engine Patches (Score +4, 3–5 days)

| Task | Test | Effort | Impact |
|------|------|--------|--------|
| T3.2–T3.3 | 88 | 2–3 hr | +1 |
| T3.4–T3.5 | 89 | 2–3 hr | +1 |
| T3.6–T3.7 | 90 | 4–8 hr | +1 |
| T3.8–T3.9 | 93 | 4–6 hr | +1 |

---

## Architecture Notes

### Range Mutation Observer Pattern

The most significant architectural change needed is a **mutation callback system** for Ranges and NodeIterators. When DOM mutations occur (removeChild, insertBefore, appendChild, splitText), all registered Ranges and NodeIterators must be notified so they can adjust their boundary points.

**Proposed design:**

```
DomBridge
├── _activeRanges: List<WeakReference<RangeState>>
├── _activeIterators: List<WeakReference<IteratorState>>
├── NotifyChildRemoved(parent, child, index)
├── NotifyChildInserted(parent, child, index)
└── NotifyTextSplit(originalNode, newNode, offset)

RangeState
├── StartContainer, StartOffset
├── EndContainer, EndOffset
├── Collapsed
└── AdjustForMutation(type, parent, child, index)

IteratorState
├── ReferenceNode
├── PointerBeforeReferenceNode
└── AdjustForRemoval(node)
```

All DOM mutation methods (`appendChild`, `removeChild`, `insertBefore`, `replaceChild`, `splitText`) would call the appropriate `Notify*` method, which iterates registered observers and calls their adjustment logic.

### YantraJS Vendored Fork Strategy

The YantraJS engine is vendored at `yantra-1.2.295/`. Modifications should:
1. Be clearly commented with `// BROILER-PATCH:` prefix
2. Be minimal and surgical — avoid refactoring unrelated code
3. Include unit tests in the Broiler test suite (not in YantraJS's own test projects)
4. Be documented in this roadmap with exact file/line references

---

## Success Criteria

- [ ] Acid3 score ≥ 90/100 (Phase A + B + C)
- [ ] Acid3 score ≥ 95/100 (+ Phase D)
- [ ] All existing 507 CLI tests continue to pass
- [ ] Each fix has a dedicated regression test
- [ ] Visual rendering shows score and bucket colors matching reference
- [ ] CI workflow (`acid3-regression.yml`) validates score on each push

---

## References

- [DOM Living Standard — Range](https://dom.spec.whatwg.org/#interface-range)
- [DOM Living Standard — Removing Steps](https://dom.spec.whatwg.org/#concept-node-remove)
- [ECMAScript 2024 — Number.prototype.toExponential](https://tc39.es/ecma262/#sec-number.prototype.toexponential)
- [ECMAScript 2024 — Regular Expressions](https://tc39.es/ecma262/#sec-patterns)
- [Acid3 Test Source](http://acid3.acidtests.org/)
- [acid3-compliance-v5.md](../acid3-compliance-v5.md) — Current compliance status
