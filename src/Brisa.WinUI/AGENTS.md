# Brisa WinUI

Read the root AGENTS.md and docs/LAYOUT.md first. WinApp CLI is optional; use the existing SDK and approved computer-use tools if it is unavailable. Do not install global tooling as a side effect.

Build through scripts/Build-Brisa.ps1 into an isolated artifacts directory. Never mix self-contained and framework-dependent outputs or publish over a running personal installation.

Keep App/MainWindow entry points at the project root. Add behavior to the relevant Features, Dock, Platform, UI or Diagnostics folder. Partial MainWindow feature files intentionally share the Brisa.WinUI namespace; folder organization must not rename persisted settings or the executable.

Reuse Ui, QuotaMeter and PanelAnchor. Preserve panel position through navigation, effective Windows/application scaling, focus, contrast and reduced motion.

Label stale usage as stale; never invent current percentages. Do not rebuild widgets every tick or trigger network queries for presentation changes. Preserve connection choices and consent when restoring user data.

Use --check-visual --review-fixtures for UI verification. Do not connect personal accounts or trigger global Windows changes to inject test failures. Report observed results and limitations honestly.
