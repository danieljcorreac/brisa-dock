---
name: brisa-verification
description: Verify Brisa changes with Core checks, isolated installer tests, native UI review and bounded endurance runs. Use when validating fixes or preparing review evidence.
---

Use `docs/TESTING.md` for commands and distinguish pure checks, observed UI, endurance and physical hardware. Do not turn a simulated message into a claim of a physical test.

Run relevant checks after the change. Broaden only for new failures or unresolved concerns. Use `--check-visual --review-fixtures` in a separate build for UI work; never transfer qa-data into the habitual installation. Keep account failures simulated and use fictitious screenshot data.

For visual changes, inspect a normal state, an empty/error state and a relevant second theme or scale. Look for clipping, focus, stable panel anchors and bars that do not animate on identical values. Use the available approved computer-use tool for native input and screenshots.

For installation changes, test an isolated destination, interrupted copying, altered packages, previous-version recovery and data preservation. Do not alter a real installation to inject failure.

Save local evidence only under ignored qa/artifacts directories. Report test counts, timing and limitations accurately. Coordinate global display/audio changes, suspension, Explorer restart or audible screen-reader checks with the user.
