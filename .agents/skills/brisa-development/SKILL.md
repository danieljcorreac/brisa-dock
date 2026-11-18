---
name: brisa-development
description: Develop Brisa's C# and WinUI dock, panels and Windows adapters while preserving its shared design and local data. Use for implementation changes in this repository.
---

Read `AGENTS.md` and `docs/ARCHITECTURE.md` from the repository root. The application is `src/Brisa.WinUI`; deployment tooling lives in `scripts/deployment`, and the solution is `Brisa.slnx`.

Use `scripts/Build-Brisa.ps1` for an isolated build. Do not publish over a running installation. Windows is needed for WinUI and native verification; Core checks can be run separately.

Put pure state and geometry in Brisa.Core when that makes behavior independently verifiable. Reuse Ui, QuotaMeter and PanelAnchor for presentation. Keep panel position through internal navigation, keep identical quota values still, and respect the current accessibility policy.

Local settings and account access are user state. Preserve source selection, consent, widget order and monitor preference. A new widget, provider or permission needs its own scoped product decision; do not add one as part of unrelated polish.

Read `docs/TESTING.md` before choosing verification. Describe the resulting behavior and actual evidence, not just that a build succeeded.
