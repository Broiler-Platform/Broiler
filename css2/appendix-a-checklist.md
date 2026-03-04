# Appendix A — Aural Style Sheets

Detailed checklist for CSS 2.1 Appendix A. This appendix defines properties
for aural presentation of documents by speech synthesizers.

> **Spec file:** [`aural.html`](aural.html)

> **Verification note:** The html-renderer is a visual rendering engine. Aural
> style sheet properties (`aural`/`speech` media) are intentionally not
> implemented. The CSS parser recognises `@media` blocks and stores rules keyed
> by media type, but only `screen` (mapped to `all`) is actively consumed.
> All items below are marked as verified (reviewed); none apply to visual output.

---

## A.1 The Media Types 'aural' and 'speech'

- [x] `aural` media type (CSS 2.0 — deprecated) *(not applicable — visual renderer)*
- [x] `speech` media type (replaces `aural`) *(not applicable — visual renderer)*
- [x] Properties apply to `aural`/`speech` media groups *(not applicable — visual renderer)*

## Volume Properties

- [x] `volume: <number>` — volume level (0–100) *(not applicable — visual renderer)*
- [x] `volume: <percentage>` — relative to inherited volume *(not applicable — visual renderer)*
- [x] `volume: silent` — no sound *(not applicable — visual renderer)*
- [x] `volume: x-soft` — equivalent to 0 *(not applicable — visual renderer)*
- [x] `volume: soft` — equivalent to 25 *(not applicable — visual renderer)*
- [x] `volume: medium` — equivalent to 50 (default) *(not applicable — visual renderer)*
- [x] `volume: loud` — equivalent to 75 *(not applicable — visual renderer)*
- [x] `volume: x-loud` — equivalent to 100 *(not applicable — visual renderer)*
- [x] Inherited: yes *(not applicable — visual renderer)*

## Speaking Properties

- [x] `speak: normal` — normal spoken rendering (default) *(not applicable — visual renderer)*
- [x] `speak: none` — element not spoken (but may be rendered visually) *(not applicable — visual renderer)*
- [x] `speak: spell-out` — spelled letter by letter *(not applicable — visual renderer)*
- [x] Inherited: yes *(not applicable — visual renderer)*

## Pause Properties

- [x] `pause-before: <time> | <percentage>` — pause before speaking element *(not applicable — visual renderer)*
- [x] `pause-after: <time> | <percentage>` — pause after speaking element *(not applicable — visual renderer)*
- [x] `pause` shorthand — before and after values *(not applicable — visual renderer)*
- [x] Percentage values relative to `speech-rate` *(not applicable — visual renderer)*
- [x] Inherited: no *(not applicable — visual renderer)*

## Cue Properties

- [x] `cue-before: <uri> | none` — auditory icon before element *(not applicable — visual renderer)*
- [x] `cue-after: <uri> | none` — auditory icon after element *(not applicable — visual renderer)*
- [x] `cue` shorthand — before and after cue URIs *(not applicable — visual renderer)*
- [x] Inherited: no *(not applicable — visual renderer)*

## Mixing Properties

- [x] `play-during: <uri> [mix || repeat]? | auto | none` — background sound during speech *(not applicable — visual renderer)*
- [x] `mix` — mix with inherited play-during sound *(not applicable — visual renderer)*
- [x] `repeat` — repeat sound if shorter than element duration *(not applicable — visual renderer)*
- [x] `auto` — continue parent's background sound *(not applicable — visual renderer)*
- [x] `none` — silence the background *(not applicable — visual renderer)*
- [x] Inherited: no *(not applicable — visual renderer)*

## Spatial Properties

- [x] `azimuth: <angle> | keywords | behind | leftwards | rightwards` *(not applicable — visual renderer)*
- [x] `azimuth` keywords: `left-side`, `far-left`, `left`, `center-left`, `center`, `center-right`, `right`, `far-right`, `right-side` *(not applicable — visual renderer)*
- [x] `behind` modifier — mirror azimuth behind the listener *(not applicable — visual renderer)*
- [x] `leftwards` / `rightwards` — relative shift *(not applicable — visual renderer)*
- [x] `elevation: <angle> | below | level | above | higher | lower` *(not applicable — visual renderer)*
- [x] Inherited: yes *(not applicable — visual renderer)*

## Voice Characteristic Properties

- [x] `speech-rate: <number> | x-slow | slow | medium | fast | x-fast | faster | slower` *(not applicable — visual renderer)*
- [x] Inherited: yes *(not applicable — visual renderer)*
- [x] `voice-family: [[<specific-voice> | <generic-voice>],]* [<specific-voice> | <generic-voice>]` *(not applicable — visual renderer)*
- [x] Generic voices: `male`, `female`, `child` *(not applicable — visual renderer)*
- [x] Inherited: yes *(not applicable — visual renderer)*
- [x] `pitch: <frequency> | x-low | low | medium | high | x-high` *(not applicable — visual renderer)*
- [x] Inherited: yes *(not applicable — visual renderer)*
- [x] `pitch-range: <number>` — variation in pitch (0–100) *(not applicable — visual renderer)*
- [x] Inherited: yes *(not applicable — visual renderer)*
- [x] `stress: <number>` — stress marking height (0–100) *(not applicable — visual renderer)*
- [x] Inherited: yes *(not applicable — visual renderer)*
- [x] `richness: <number>` — voice richness / brightness (0–100) *(not applicable — visual renderer)*
- [x] Inherited: yes *(not applicable — visual renderer)*

## Speech Properties

- [x] `speak-punctuation: code | none` *(not applicable — visual renderer)*
  - [x] `code` — punctuation spoken literally *(not applicable — visual renderer)*
  - [x] `none` — punctuation rendered naturally (default) *(not applicable — visual renderer)*
  - [x] Inherited: yes *(not applicable — visual renderer)*
- [x] `speak-numeral: digits | continuous` *(not applicable — visual renderer)*
  - [x] `digits` — spoken as individual digits ("1", "2", "0", "0") *(not applicable — visual renderer)*
  - [x] `continuous` — spoken as number ("one thousand two hundred") *(not applicable — visual renderer)*
  - [x] Inherited: yes *(not applicable — visual renderer)*

## Table Speaking

### A.11.1 Speaking Headers

- [x] `speak-header: once | always` *(not applicable — visual renderer)*
  - [x] `once` — speak header once before associated cells *(not applicable — visual renderer)*
  - [x] `always` — speak header before every associated cell *(not applicable — visual renderer)*
  - [x] Inherited: yes *(not applicable — visual renderer)*

---

[← Back to main checklist](css2-specification-checklist.md)
