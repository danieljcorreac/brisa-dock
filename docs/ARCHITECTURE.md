# Architecture

- `src/Brisa.Core`: configuration, models, state rules, usage providers, calendar processing and calculations independent of WinUI. Related code is grouped by domain.
- `src/Brisa.WinUI`: dock, feature panels, shared UI, diagnostics and Windows/Shell/audio adapters. `App` and `MainWindow` are the entry points.
- `src/Brisa.Bridge`: Claude Code statusLine bridge that writes local usage snapshots.
- `tools/Brisa.Setup`: per-user setup with an embedded, validated archive and no account credentials.
- `tests/Brisa.Checks`: logic checks and simulated upstream providers.
- `tests/*.Checks.ps1`: isolated installation, update, rollback and distribution verification.
- `scripts`: build and release orchestration; deployment sources live in `scripts/deployment`.

See [layout](LAYOUT.md) for the feature directories and generated files.

`SettingsStore` owns local state. `SettingsBackup` validates portable backups and preserves the installation's connections during restore. Updates and rollback handle application payloads separately from user data.

`Ui`, `QuotaMeter` and `PanelAnchor` centralize presentation and navigation positioning. Background sources cannot duplicate in-flight work; usage providers have separate polling budgets. Old snapshots never become invented zero-percent readings.

Native adapters handle desktop reservation, the notification area and shortcuts. The preferred monitor is distinct from a temporary recovery monitor. Native-message simulations do not prove real hardware reconnection behavior.

The notification-area quick menu has its own `TrayMenuWindow`. Its screen anchor comes from `Shell_NotifyIconGetRect`, with a cursor fallback, and `TrayMenuLayout` fits it to the anchor monitor. Opening the menu never uses or repositions the dock's XAML root. Selecting a dock-related command explicitly reveals the dock and opens the associated panel there.

Setup keeps its entry point, presentation (`SetupWindow`/`SetupControls`) and existing installation backend separate. Owner-drawn controls retain native button/checkbox accessibility and keyboard behavior; Windows supplies the outer rounded window corners. Safe preview states exercise layout without installing files.
