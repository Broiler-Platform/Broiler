# Broiler.UI — UX Guidelines Conformance Roadmap

**Status:** Proposed
**Date:** 2026-07-07
**Scope:** Design quality and user-experience conformance only. This document
plans *how Broiler.UI becomes an ideal user interface* against the major
UI/UX standards. It does **not** re-specify assembly boundaries, OS-neutrality
rules, or control contracts — those live in the
[Broiler.UI Component Roadmap](broiler-ui-component.md) and are treated here as
an implemented substrate. This is a layer *on top of* that architecture, not a
replacement for it.

---

## 1. Executive decision

The Broiler.UI control set is structurally complete: Window, Panel, Label,
Button, Edit, CheckBox, RadioButton, ToggleButton, Slider, ProgressBar,
ScrollView, ListView, ComboBox, TabView, Menu, ImageView, Dialog, Tooltip, plus
Toolbar, FileDialog, and RichEdit all exist as abstraction/`.Standard` pairs.
What is *not* yet in place is the **design system and experience layer** that
turns a functional widget toolkit into a UI that measurably satisfies the
recognized usability, accessibility, and platform-design standards.

**Decision:** stand up a platform-neutral **Broiler design system** — a
versioned token model, a finite visual-state contract per control, a motion
system, and an accessibility conformance program — and drive every standard
control to conform to it under enforceable release gates. Conformance is
defined against named external standards (section 3), verified by automated and
manual gates (section 20), and staged so each phase leaves the toolkit shippable.

The north star: **a Broiler.UI application should pass a WCAG 2.2 AA audit, a
Nielsen heuristic evaluation, and a platform-HIG review with no toolkit-level
blockers**, in light, dark, and high-contrast themes, in LTR and RTL, at 100%–300%
scale, keyboard-only and with a screen reader.

---

## 2. Current-state findings

Grounded in the current tree (not aspirational):

1. **Theming is a 4-color record.** `StandardThemeTokens`
   (`Broiler.UI/Broiler.UI.Standard/StandardThemeTokens.cs`) exposes only
   `Background`, `Foreground`, `Accent`, `FocusRing` with one `Default`. There is
   no typography scale, spacing scale, radius, elevation, semantic color roles,
   dark theme, or high-contrast theme. **This is the single largest UX gap.**
2. **System-preference plumbing already exists but is under-consumed.**
   `UiSystemSettings` (`Broiler.UI/Broiler.UI/UiSystemSettings.cs`) already
   carries `ContrastPreference`, `TextScale`, `ReducedMotion`, and
   `FlowDirection`. The pipes are there; the tokens and controls do not yet
   respond to them systematically.
3. **A semantic/accessibility tree exists** (`StandardSemanticSnapshot`,
   `UiSemanticNode`, `IUiAccessibilityHost`) but role/name/state coverage,
   relationships, and live-region support per control are unaudited against
   WCAG/UIA expectations.
4. **`StandardControlPaint` bypasses the theme tokens.** It holds ~14 hardcoded
   light-mode colors plus radius constants (`ControlRadius=6`, `PillRadius`) and
   is *not* driven by `StandardThemeTokens`. Dark mode therefore does not exist,
   and high-contrast is only an enum hook (`UiContrastPreference`). Unifying paint
   under the token model is the mechanical core of Phase A.
5. **No focus-traversal engine.** `StandardFocusScope` only does `TryFocus`; there
   is no session-level Tab/Shift-Tab order, no `TabIndex`, no directional/arrow
   grouping, and no focus trapping in modals (only ad-hoc logic inside Toolbar and
   RichEdit). This is a **WCAG 2.1.1/2.1.2/2.4.3 blocker** and a first-class item.
6. **The "animation scheduler" is a bare interval tick-pump**
   (`StandardAnimationScheduler`), not a transition/easing system. Motion tokens
   need an actual transition engine behind them.
7. **Layout is Stack/Dock/Overlay panels only** — no grid, no flex, and no
   margin/alignment model on base `UiElement`. Adaptive layout (section 14) needs
   a grid/flex panel and a margin/alignment contract.
8. **No localization/culture/resource story** in the toolkit; RTL is modeled
   (`UiFlowDirection`, per-control text direction) but unverified in Standard
   controls.
9. **Interaction plumbing otherwise exists** — hit testing, unified input
   (pointer/touch/pen/keyboard/IME), command dispatch, a semantic tree
   (`UiSemanticNode`/role/state), clipboard/cursor/drag host ports, and 18 ADRs
   (esp. `0006` layout, `0008` accessibility, `0010` theme/visual-state) — so most
   UX work is *policy and tokens over existing mechanics*.
10. **No design-system documentation, heuristic baseline, or contrast test suite.**
    A demo-facing UX changelog ([new-ui-design.md](new-ui-design.md)) describes an
    aspirational Fluent-like look, but nothing enforceable stands behind it.

**Implication:** the effort is ~60% *design system + conformance*, ~20%
*net-new mechanics* (focus-traversal engine, transition engine, live regions,
grid/flex layout), ~20% *per-control state/semantics hardening*. It is sequenced
token-layer first because every later item depends on it. Token, focus, and motion
work extend existing ADRs (`0010`, `0008`, `0006`) rather than replacing them.

---

## 3. Guiding standards (the "major UI/UX guidelines")

Conformance targets, named explicitly so gates can cite them:

| Domain | Standard | How it is used here |
|---|---|---|
| Usability heuristics | **Nielsen–Molich 10 heuristics**; ISO 9241-110 dialogue principles | Heuristic audit checklist; every control reviewed against all 10 |
| Accessibility | **WCAG 2.2 Level AA** (target), AAA where cheap; WAI-ARIA Authoring Practices; EN 301 549 / Section 508 | Automated + manual a11y gates; semantic-tree mapping |
| Visual design system | **Fluent 2** (primary, Windows-first), **Material Design 3**, **Apple HIG**, **GNOME/KDE HIG** | Token model and component specs align to Fluent 2; cross-checked so nothing is Windows-only |
| Interaction/motion | Material motion system; Apple HIG motion; `prefers-reduced-motion` | Motion token set with duration/easing + reduced-motion fallbacks |
| Color science | WCAG contrast (4.5:1 text / 3:1 large & UI), APCA (informational) | Contrast validator in CI |
| Layout/perception | Gestalt principles; 8pt spacing grid; touch-target minimums (WCAG 2.5.8 24px, Fluent/Apple 44px, Material 48dp) | Spacing scale + minimum hit-target enforcement |
| Internationalization | Unicode UAX; CLDR; W3C i18n; bidi (UAX #9) | RTL mirroring, locale formatting, pseudo-localization tests |

These are **not** all mutually identical; where they conflict (e.g. corner
radius, target size, elevation), Fluent 2 is the default because Windows is the
first deployment, and the token model keeps the others reachable by swapping a
token set rather than rewriting controls.

---

## 4. Experience pillars and quality model

Every deliverable maps to one of six pillars:

1. **Consistency** — one token vocabulary, one state model, one interaction
   grammar across all controls (Nielsen #4; internal + external consistency).
2. **Clarity & hierarchy** — typographic scale, spacing rhythm, and color roles
   that make structure legible at a glance (Gestalt; visual hierarchy).
3. **Feedback & status** — visible system status for every action, hover, focus,
   loading, success, and error state (Nielsen #1).
4. **Forgiveness** — error prevention, confirmation for destructive actions,
   undo, and clear recovery (Nielsen #3, #5, #9).
5. **Accessibility & inclusion** — perceivable, operable, understandable, robust
   for keyboard, screen reader, low-vision, motor, and cognitive needs (WCAG POUR).
6. **Adaptivity** — responds to theme, contrast, motion, scale, density,
   direction, and viewport without redesign.

Each control carries a **conformance scorecard** across these six pillars; a
control is "ideal" only when all six pass their gates (section 20).

---

## 5. The design token system (foundation)

Replace the 4-color `StandardThemeTokens` with a layered, immutable, versioned
token model. Tokens are platform-neutral data resolved per session/subtree (the
existing `StandardThemeResolver` is the resolution point).

**Three-tier token architecture:**

- **Primitive tokens** — raw values: a color ramp (e.g. neutral 0–100, brand
  hues), a type ramp, a numeric spacing/radius scale. No semantics.
- **Semantic (alias) tokens** — role-based: `Surface`, `SurfaceRaised`,
  `TextPrimary`, `TextSecondary`, `TextDisabled`, `AccentDefault`,
  `AccentHover`, `AccentPressed`, `Border`, `BorderFocus`, `Success`, `Warning`,
  `Danger`, `Info`, `Selection`, `Overlay/Scrim`. Controls consume **only**
  semantic tokens.
- **Component tokens** — optional per-control overrides (e.g. `Button.Padding`,
  `Edit.MinHeight`) that default to semantic tokens.

**Token categories to define:**

| Category | Contents |
|---|---|
| Color | Full semantic role set above, per theme; state variants (rest/hover/pressed/disabled/selected) |
| Typography | Family, and a **type scale** (Caption, Body, BodyStrong, Subtitle, Title, LargeTitle, Display) with size/line-height/weight/tracking |
| Spacing | 8pt-based scale (2, 4, 8, 12, 16, 20, 24, 32, 40, 48…) as named steps |
| Radius | None, Small, Medium, Large, Circular |
| Elevation | Flat, Raised, Overlay, Popup — shadow/border hints (no OS shadow API) |
| Border/stroke | Hairline, Default, Thick; focus-ring width |
| Motion | Duration steps (instant/fast/normal/slow) + easing curves (standard/decelerate/accelerate/emphasized) |
| Z-index / bands | Content, Scrollbar, Popup, Modal, Tooltip, DragAdorner (align with subwindow manager) |
| Density | Comfortable / Compact multipliers over spacing + control min-heights |
| Focus | Ring color, width, offset, style |

**Theme sets to ship:** Light, Dark, High-Contrast Light, High-Contrast Dark —
each a complete semantic token set. `UiSystemSettings.ContrastPreference` and OS
dark-mode signal (via host) select the active set; `TextScale` multiplies the
type ramp; `ReducedMotion` swaps motion durations to ~0.

**Rules:** controls never hardcode a color/size; token sets are immutable and
versioned; a missing token is a build-time error, not a runtime fallback; theme
switch invalidates visuals only (no relayout unless metrics changed).

---

## 6. Color, contrast, and theming conformance

- Every semantic text/background pairing meets **WCAG 2.2 AA**: 4.5:1 body,
  3:1 large text and non-text UI (borders, icons, focus rings, control
  boundaries — 1.4.11).
- Ship a **contrast validator** that runs over every theme set × state in CI and
  fails the build on any violation. APCA reported as advisory.
- **Never encode meaning by color alone** (1.4.1): pair status color with icon,
  text, or shape (error field gets icon + message, not just a red border).
- High-contrast themes honor system colors and remove decorative elevation.
- Disabled states remain distinguishable but are exempt from contrast minimums
  per WCAG; still verify they are perceivable.
- Selection, hover, and focus are visually distinct from each other and from
  rest.

---

## 7. Typography and content

- Implement the type scale from tokens; controls reference named roles, never
  point sizes.
- Respect `TextScale` up to **200%** (WCAG 1.4.4) without loss of content or
  function; verify reflow/no-clipping to **400%** where practical (1.4.10).
- Minimum body size guidance and adequate line-height (~1.4–1.5 body).
- Text trimming uses ellipsis with the full value available via tooltip/semantics.
- **Microcopy guidelines** (a short content style doc): sentence case for UI
  labels, imperative for buttons ("Save", not "Submit form now"), specific and
  human error messages, no dead-ends. Nielsen #2 (match system to real world),
  #9 (help users recover).

---

## 8. Spacing, layout, and density

- One **8pt spacing scale**; all control padding/margins/gaps come from it.
- Alignment to a consistent grid; consistent internal control metrics
  (button height, edit height, list row height) from tokens.
- **Density modes** (Comfortable default, Compact) as a token multiplier —
  enterprise/data-dense UIs (Nielsen #7, flexibility/efficiency).
- Gestalt-driven grouping: proximity and shared containers for related controls;
  `UiPanel` policies expose sensible default gaps.
- **Touch targets:** enforce a minimum interactive size — WCAG 2.5.8 baseline
  (24×24), Fluent/Apple recommended (44×44) as the comfortable default — with
  spacing exceptions documented. Applies to buttons, checkboxes, list rows,
  slider thumbs, menu items, scrollbar thumbs, close affordances.

---

## 9. Motion and animation

- Motion tokens (duration + easing) drive all transitions: hover, press, focus,
  expand/collapse, popup open/dismiss, progress, selection.
- Motion is **purposeful** (orients, shows relationship, confirms action), never
  decorative-only, and never blocks interaction.
- **`prefers-reduced-motion` / `UiSystemSettings.ReducedMotion`** replaces
  animation with instant state changes or a minimal cross-fade; indeterminate
  progress becomes a non-animated busy state (WCAG 2.3.3).
- No motion faster than the flashing threshold (WCAG 2.3.1); no auto-playing
  motion longer than 5s without a pause affordance (2.2.2).
- All animation runs on the existing session clock / `StandardAnimationScheduler`
  (no per-control OS timers), so it is deterministic and testable.

---

## 10. Component state model and interaction grammar

Define one **finite visual-state contract** every interactive control
implements, wired to tokens:

`Rest → Hover → Pressed/Active → Focused (visible) → Selected/Checked →
Disabled → ReadOnly → Invalid → Busy/Loading`

- States compose predictably (focused+hover, selected+disabled) with a defined
  precedence.
- **Focus-visible policy:** a keyboard-focus ring appears for keyboard/AT
  interaction and is suppressed for pointer activation (matches modern
  `:focus-visible`), always meeting the 3:1 non-text contrast and 2.4.11 (focus
  not obscured) / 2.4.13 (focus appearance) of WCAG 2.2.
- One interaction grammar: Enter/Space activation semantics per role, arrow-key
  navigation within composites (list, menu, tabs, radio group, slider), Escape
  to dismiss/cancel, Home/End, type-ahead where appropriate — consistent across
  controls (Nielsen #4; ARIA Authoring Practices patterns).
- Consistent hover/press affordance so every interactive element *looks*
  interactive and every non-interactive element does not (Nielsen #6,
  recognition over recall).

**Focus-traversal engine (net-new, high priority).** The toolkit currently has no
session-level tab order. Build a focus-traversal manager providing:

- **Tab / Shift-Tab** order from a `TabIndex` plus natural tree order;
- **directional / arrow-key groups** for composites (a radio group, toolbar, menu,
  or list is one tab stop with internal arrow navigation) per the ARIA roving-
  tabindex pattern;
- **focus trapping** inside modal windows/dialogs, with **focus restoration** to
  the invoker on dismissal (WCAG 2.4.3, 3.2.x);
- skip/bypass for repeated blocks (2.4.1); and
- focus never landing on hidden/disabled/offscreen elements.

Without this, keyboard operability and screen-reader use fail at the toolkit
level, so it is scheduled in Phase B alongside focus-visible.

---

## 11. Accessibility conformance program

Beyond the token/contrast work, drive semantics to WCAG 2.2 AA:

- **Roles & names:** every control exposes correct role, accessible name
  (from content, label association, or explicit name), value, and description via
  the semantic tree; audit each `UiSemanticNode` mapping against expected
  ARIA/UIA roles.
- **States & properties:** expanded, selected, checked (incl. mixed),
  disabled, readonly, required, invalid, busy, modal, current, and value bounds
  reported and kept live.
- **Relationships:** labelled-by, described-by, controls, owns, and error-message
  association (WCAG 1.3.1, 3.3.1/3.3.3 for forms).
- **Live regions / announcements:** status, progress, validation, and toast/
  notification changes announced politely/assertively (WCAG 4.1.3).
- **Keyboard operability:** everything operable keyboard-only, no traps (2.1.1/
  2.1.2), logical tab order, visible focus, documented shortcuts, and skip/
  bypass where a UI has repeated blocks (2.4.1).
- **Forms:** labels, instructions, error identification with text, error
  suggestion, and error prevention for legal/financial/data-loss actions (3.3.x).
- **Target size, contrast, motion, orientation, reflow** as covered above.
- Ship an **Accessibility Conformance Statement (VPAT-style)** per stable
  release, evidence-backed.

---

## 12. Feedback, errors, and status (Nielsen #1, #3, #5, #9)

- **Visible system status:** loading/busy states, progress for long operations,
  optimistic feedback for instant ones.
- **Empty, loading, error, and success states** are first-class for data
  controls (ListView, ComboBox, dialogs): every data surface specifies all four.
- **Error prevention:** confirm destructive/irreversible actions; disable invalid
  submissions with an explanation, not silent failure; constrain inputs.
- **Recovery:** human-readable errors that say what happened and how to fix it;
  provide undo where feasible; never blame the user.
- **Notifications/toasts** with correct politeness level and dismissal semantics
  (align with the subwindow manager and live regions).

---

## 13. Internationalization, RTL, and localization

- **RTL mirroring** driven by `UiSystemSettings.FlowDirection`: layout,
  alignment, icon direction, scroll, and caret behavior mirror; bidi text
  (UAX #9) renders correctly in Edit/Label/RichEdit.
- Locale-aware number/date formatting supplied by the application/formatter,
  never hardcoded.
- **Pseudo-localization** and long-string tests in CI (German/Finnish-length,
  accented, RTL) to catch clipping and truncation.
- Access-key/mnemonic conflict diagnostics per window.
- No text baked into images; all user-visible strings externalizable.

---

## 14. Responsiveness, adaptivity, and DPI

- Controls relayout correctly across viewport sizes and `UiPanel` policies
  (wrap/dock/grid) so chrome adapts rather than clips.
- **Reflow at 320px-equivalent / 400% zoom** without horizontal scroll for
  content (WCAG 1.4.10) where the app opts in.
- Crisp rendering at fractional DPI (125%, 150%, 175%) — geometry rounds at the
  Graphics host boundary; focus rings, borders, and 1px strokes stay clean.
- Orientation-independent (WCAG 1.3.4) where the host supports rotation.

---

## 15. Control-set completeness

An ideal UI toolkit needs the vocabulary users expect. The current set is strong
(20+ controls incl. RichEdit) but missing common patterns. Prioritize, then add
as `Ui*` / `Standard*` pairs following the architecture roadmap's rules — each
born at maturity Level 2+ (tokenized, stateful, accessible):

- **Notification / Toast** — required by the feedback work (section 12); today
  there is no distinct control. High priority.
- **NumericUpDown / Spinner**, **DatePicker / TimePicker**, **ColorPicker** — form
  completeness.
- **TreeView**, **DataGrid / Table** (with header, sort, selection,
  virtualization) — data-dense apps.
- **Expander / Accordion**, **SplitPane / SplitView**, **StatusBar** — layout and
  navigation completeness.

New controls do not gate the "ideal" claim for the *existing* set, but the
notification/toast control is a dependency of Phase E and should land there.

**Layout primitives** (blocks adaptive UI, section 14): add a **Grid** and/or
**Flex** panel policy and a **margin/alignment** contract on the base element;
Stack/Dock/Overlay alone cannot express responsive chrome.

## 16. Iconography and imagery

- A consistent icon set with a defined grid, stroke weight, and optical sizing;
  icons are theme- and contrast-aware and meet 3:1 non-text contrast when
  meaningful.
- Icon-only controls always carry an accessible name and, where practical, a
  tooltip.
- Decorative imagery is marked non-semantic (empty/absent accessible name) so it
  is skipped by AT.

---

## 17. Design-system documentation and governance

Deliverables that make the system usable and durable:

- **Design system reference** (`Broiler.UI/docs/design-system/`): token catalog,
  color roles with contrast data, type scale, spacing, motion, per-control specs
  with all states rendered, do/don't guidance, and accessibility notes per
  control.
- **Interaction & keyboard reference:** the shortcut/navigation grammar per
  control (single source of truth).
- **Heuristic audit template** (Nielsen 10) and a recurring evaluation cadence.
- **Usability testing plan:** lightweight moderated tests on the browser chrome
  and demo apps at phase boundaries; findings feed the backlog.
- **Content/microcopy style guide.**
- A **maturity scorecard** (section 18) tracked per control.

---

## 18. UX maturity model

Rate each control 0–4; "ideal" = Level 4 across all six pillars.

- **L0 Functional** — works, but hardcoded visuals, no state model, minimal
  semantics. *(current state for most controls.)*
- **L1 Tokenized** — consumes semantic tokens; light/dark themes; correct role +
  name.
- **L2 Conformant** — full visual-state model, focus-visible, WCAG AA contrast,
  keyboard-complete, high-contrast theme.
- **L3 Adaptive** — reduced-motion, text-scale to 200%, RTL, density, live
  regions, empty/error/loading states.
- **L4 Ideal** — passes heuristic + a11y + HIG review with evidence; usability-
  tested; documented in the design system.

---

## 19. Delivery roadmap

Phases are additive; each leaves the toolkit shippable. Lettered to avoid
collision with the architecture roadmap's numeric phases.

### Phase A — Token model and theming foundation  *(in progress)*
**Objective:** replace the 4-color theme with the three-tier token system.

- Define primitive/semantic/component tokens (section 5) as immutable records.
- Ship Light, Dark, High-Contrast Light, High-Contrast Dark token sets.
- Wire `StandardThemeResolver` to select sets from `UiSystemSettings`
  (contrast, dark-mode via host) and apply `TextScale`.
- Build the **contrast validator** and run it over every set × role × state.
- Migrate `StandardControlPaint` and each control to consume semantic tokens
  (mechanical, per-control).

**Landed:**

- `StandardThemeTokens` expanded from 4 colors to a full semantic palette (18
  color roles + radii + `IsDark`/`Name`), with `required` roles so an incomplete
  preset fails to compile. Presets: `Light`, `Dark`, `HighContrastLight`,
  `HighContrastDark`, plus `Select(contrast, dark)` mapping `UiSystemSettings`.
  The legacy 4-color constructor is preserved.
- `StandardControlPaint` single-sourced onto the active token set via
  `ApplyTheme(...)`, so all 18 controls re-color with **zero control-level churn**
  (they already read the `StandardControlPaint` roles).
- `StandardContrast` WCAG luminance/ratio utility (seed of the CI contrast gate);
  `StandardThemeTokensTests` asserts every preset's text/accent pairs meet AA
  (4.5:1) and focus rings meet 3:1.
- Win32 demo honors `--dark` / `--high-contrast` / `BROILER_UI_THEME`, and its
  chrome (`DemoColors`) now derives from the active theme.
- **Live re-theming:** `UiColorScheme` (neutral) added to `UiSystemSettings`;
  `IStandardThemedControl` + `StandardThemeController` re-derive every control's
  role colors across a session's tree and invalidate, so a running UI switches
  light ↔ dark ↔ high-contrast without rebuilding. All 18 themeable controls
  implement it; `OnAccent` adopted where they had hardcoded `White`. The demo
  toggles live with **Ctrl+D**. Covered by `StandardThemeControllerTests`.

**Remaining for the exit gate:**

- Live switching resets app-set color overrides on the switched controls (the
  re-skin replaces them). A future refinement can track explicit overrides or move
  to render-time getter-fallback so overrides always win. `TextScale` application
  is Phase C.
- Promote the contrast validator to a CI gate over every set × role × state and
  add a token-usage lint (no raw colors/sizes in controls). `StandardScrollView`,
  `StandardPanel` (transparent) and `StandardFileDialog` (intentional retro skin)
  remain outside the token set by design — document or revisit.

**Exit gate:** no control references a raw color/size; all four themes pass the
contrast validator in CI; theme switch is visual-only and allocation-light.

### Phase B — Visual-state model, focus-visible, and interaction grammar
**Objective:** one consistent, accessible interaction layer.

- Implement the finite visual-state contract (section 10) for every interactive
  control, driven by tokens.
- Implement focus-visible policy and the 2.4.11/2.4.13-compliant focus ring.
- Normalize keyboard navigation per ARIA pattern across composites.
- Enforce minimum touch-target metrics.

**Exit gate:** every interactive control renders all defined states; keyboard-only
traversal is complete and trap-free; focus ring meets 3:1 and is never obscured;
target-size test passes.

### Phase C — Typography, spacing, density, and motion
**Objective:** clarity, rhythm, and purposeful motion.

- Apply the type scale and 8pt spacing scale throughout.
- Add Comfortable/Compact density.
- Introduce motion tokens; route all transitions through the session clock; wire
  `ReducedMotion`.

**Exit gate:** type/spacing come only from tokens; `TextScale` 200% has no
clipping/loss; reduced-motion removes all animation; density switch is clean.

### Phase D — Accessibility conformance
**Objective:** WCAG 2.2 AA across the toolkit.

- Audit and complete role/name/value/state/relationship coverage per control.
- Add live-region announcements for status/progress/validation/notifications.
- Complete form semantics (labels, required, invalid, error association,
  error prevention).
- Integrate an automated a11y checker into CI; run screen-reader manual passes.

**Exit gate:** automated a11y suite green; manual screen-reader script passes for
each control; keyboard + AT verified in light/dark/high-contrast; draft VPAT.

### Phase E — Feedback, errors, and data-state completeness
**Objective:** status visibility and forgiveness (Nielsen).

- Standardize empty/loading/error/success states for data controls and dialogs.
- Add destructive-action confirmation, undo hooks, and human error messaging.
- Standardize notification/toast semantics with live regions.

**Exit gate:** every data surface and dialog specifies all four states;
destructive flows are confirmable/recoverable; heuristic audit finds no
status/error blockers.

### Phase F — Internationalization, RTL, and adaptivity
**Objective:** works everywhere.

- Complete RTL mirroring and bidi across text controls.
- Add pseudo-loc + long-string CI tests; DPI/reflow/orientation tests.

**Exit gate:** full RTL parity; pseudo-loc has no clipping; fractional-DPI and
zoom-to-400% render cleanly.

### Phase G — Documentation, evaluation, and stabilization
**Objective:** freeze and prove "ideal".

- Publish the design-system reference, interaction reference, and content guide.
- Run Nielsen heuristic evaluation + moderated usability tests on the browser
  chrome and demos; close findings.
- Publish per-control maturity scorecards and the Accessibility Conformance
  Statement.
- Migrate the browser chrome / demo apps to demonstrate the system end-to-end.

**Exit gate:** section 21 definition of done met.

---

## 20. Testing and release gates

Automated, run in CI as gates (behavioral assertions primary, pixels secondary):

- **Contrast validator** — every theme × role × state ≥ WCAG AA.
- **Token-usage lint** — fail on any hardcoded color/size in a control.
- **Automated accessibility scan** — roles/names/states/relationships present;
  no keyboard traps; target-size minimums.
- **State-render snapshots** — deterministic render-list snapshot per control ×
  state × theme (light/dark/HC) × LTR/RTL.
- **Text-scale & reflow** — 100/150/200% with no clipping/loss.
- **Reduced-motion** — asserts no animation scheduled when the flag is set.
- **Pseudo-localization & RTL** — layout integrity under expansion and mirroring.

Manual/periodic gates: screen-reader scripts (per control), Nielsen heuristic
audit (per phase), moderated usability sessions (phases E and G), and a
platform-HIG review before stabilization.

---

## 21. Definition of done — "ideal user interface"

Broiler.UI meets this roadmap when:

- the 4-color theme is replaced by a versioned three-tier token system with
  Light, Dark, and High-Contrast themes, all passing the contrast validator;
- every standard control reaches **Level 4** on the maturity model across all six
  pillars, or is explicitly excluded from the claim;
- a Broiler.UI application **passes a WCAG 2.2 AA audit** with no toolkit-level
  blockers, keyboard-only and with a screen reader, in every theme;
- a **Nielsen heuristic evaluation** and a **platform-HIG review** find no
  toolkit-level blockers;
- `TextScale` (to 200%), `ReducedMotion`, `ContrastPreference`, and RTL
  (`FlowDirection`) are honored end-to-end with test evidence;
- density, motion, focus-visible, target-size, and data-state completeness are
  implemented and gated;
- the design-system reference, interaction reference, content guide, per-control
  scorecards, and an Accessibility Conformance Statement are published; and
- the browser chrome / demo apps demonstrate the full system in real use.

---

## 22. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Token migration touches every control at once | Large, risky diff | Semantic-token indirection first; migrate control-by-control behind unchanged visuals |
| "Fluent-first" bakes in Windows assumptions | Cross-platform friction | Tokens only, no OS API; cross-check Material/Apple/GNOME; ship a non-Fluent token set as proof |
| Contrast/a11y gates block delivery late | Schedule risk | Land validators in Phase A/D early; make them advisory→blocking on a scheduled date |
| Standards conflict (radius, target size, elevation) | Bikeshedding | Fluent 2 is the documented default; deviations require an ADR |
| Semantic-tree gaps discovered late | A11y rework | Audit mapping in Phase D against a per-role checklist before coding announcements |
| Motion/perf regressions | Jank, battery | All motion on session clock; per-idle-frame allocation gates from the architecture roadmap remain in force |
| RTL/bidi correctness in RichEdit | Text corruption | Reuse Edit bidi gates; dedicated bidi + pseudo-loc snapshot suite |
| Design system drifts from code | Docs rot | Generate token/spec docs from the token source where possible; scorecards reviewed per release |

---

## 23. Relationship to existing roadmaps

- **[Broiler.UI Component Roadmap](broiler-ui-component.md)** — owns architecture,
  assemblies, OS-neutrality, and control contracts. This document assumes that
  substrate and adds the experience layer. Where a UX requirement needs a
  contract change (e.g. a new semantic property), it is raised as an ADR there.
- **[new-ui-design.md](new-ui-design.md)** — an aspirational visual changelog for
  a demo; this roadmap supersedes it as the enforceable specification and should
  fold its intent into the Phase A/C token work.
- **RichEdit** work (see memory/roadmaps) inherits the token, a11y, motion, and
  RTL requirements here; treat RichEdit as a first-class control in every phase.
