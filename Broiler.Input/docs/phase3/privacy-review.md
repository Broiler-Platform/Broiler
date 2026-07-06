# Broiler.Input Phase 3 Foreground And Background Privacy Review

**Status:** Approved for Phase 4 prerequisites  
**Date:** 2026-07-03

## Decisions

- Foreground-only input is the default for semantic and raw registration paths.
- Background Raw Input requires an explicit acknowledgement flag and target
  window.
- System-key messages are not consumed by default.
- Raw physical device IDs are opaque and do not expose device interface paths.
- Diagnostics must not log raw keyboard text, raw mouse movement timelines, or
  native device paths by default.

## Remaining Release Gates

Before a stable release, hardware tests still need to prove:

- two physical keyboards or mice are distinguishable in raw mode;
- background registration cannot be enabled by default options;
- system shortcuts continue to reach Windows when not explicitly consumed; and
- no raw input diagnostic output includes sensitive payload content.
