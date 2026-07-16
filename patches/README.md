# Submodule patches

Patches for the `MaiRat/Broiler.*` submodules whose remotes are outside this session's
GitHub scope (`git push` → 403). Each captures a submodule change validated by building the
parent in place; a maintainer applies the patch to the submodule, pushes it, and bumps the
gitlink pointer in the parent. Until then the submodule pointer is unchanged and any active
fallback lives in the main repo (noted per patch).

Apply a patch with:

```sh
cd <Submodule>
git am < ../patches/NNNN-<slug>.patch     # or: git apply
```

## Native dialog/backdrop track — display slice (2026-07-16)

Two patches make CSS `<dialog>` **display** native, so the bridge's `display:block` pre-bake
in `src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/Dialogs.cs`
(`InsertDialogBackdrops`) can be deleted. **Apply in order — 0002 depends on 0001.**

### 0001-css-fix-not-attr-selector.patch → `Broiler.CSS`

General selector-matcher bug fix (not dialog-specific). `CssSelectorMatcher.MatchesCompound`
stripped every `[attr]` selector from the compound *before* extracting pseudo-classes, so a
nested attribute selector inside `:not()` / `:is()` / `:where()` (e.g. the `[open]` in
`:not([open])`) was hoisted into a top-level **positive** filter and left an empty `:not()`,
**inverting** the match — `dialog:not([open])` matched *open* dialogs. The fix reorders
`ProcessPseudoClasses` (bracket-aware `ExtractPseudos` + recursive matcher) to run before the
attribute strip. Includes a regression test (`Matches_Not_With_Nested_Attribute_Selector`),
covering `:not`/`:is` with nested attribute selectors and the empty-value boolean-attribute
(`open=""`) case. Full css WPT corpus unchanged (36 fails); 215 `Broiler.CSS.Dom.Tests` pass
(the 2 `CssDomArchitectureTests` failures are pre-existing/environmental).

### 0002-html-native-dialog-ua-display.patch → `Broiler.HTML`

Adds native UA `<dialog>` display rules to `CssDefaults.DefaultStyleSheet`:
`dialog { display: block }` + `dialog:not([open]) { display: none }` (and drops `dialog` from
the blanket `template, dialog, [hidden] { display: none }`). Replaces the bridge's
`display:block` bake and also fixes bare non-modal `<dialog open>` (the bridge only handled
modal dialogs). **Requires 0001** — without the `:not([attr])` fix, `dialog:not([open])` is
inverted and hides open dialogs.

### Follow-up once 0001 + 0002 are applied and the pointers bumped

Delete the `display:block` pre-bake in `InsertDialogBackdrops` (the `if
(!InlineStyle(dialog).ContainsKey("display")) InlineStyle(dialog)["display"] = "block";`
block). It is the **active main-repo CI fallback** and must stay until the patches land (CI
clones submodules by pointer, so it renders open dialogs until the UA rule is live). Removing
it was validated end-to-end: with 0001 + 0002 applied and the bake removed,
`NativeModalDialogAnchorWptTests` passes and the css-anchor-position corpus stays 33/6.

Not covered by this slice (later dialog/backdrop track work): UA box **chrome**
(border/padding/background) — blocked on an author-vs-UA origin-precedence gap
(`anchor-position-top-layer-003/004/006`); modal centering / `position:fixed`; native
`::backdrop` box generation; native top-layer paint. See the native dialog/backdrop track
section in `docs/roadmap/htmlbridge-complexity-reduction-roadmap.md`.
