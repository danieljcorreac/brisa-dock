# Working on Brisa

Windows C#/WinUI 3 application. Open `Brisa.slnx` or use the commands below. Read `docs/ARCHITECTURE.md`, `docs/LAYOUT.md` and the UI guidance in `src/Brisa.WinUI/AGENTS.md` before changing relevant code.

## Commands

- Isolated build: `pwsh -File scripts/Build-Brisa.ps1`.
- Core checks: `dotnet run --project tests/Brisa.Checks -c Release`.
- Sample-data UI: `pwsh -File scripts/Start-Brisa.ps1 -Review`.
- Distribution: `pwsh -File scripts/Test-Repository.ps1`, then `pwsh -File scripts/New-ReleasePackage.ps1`.
- Deployment sources live in `scripts/deployment`; prepared packages copy them to `maintenance/`.

Project skills live in `.agents/skills/brisa-development`, `brisa-verification` and `brisa-release`. Load only the relevant one.

## Invariants

Preserve user data, account connections, consent, item order and monitor preference. The personal installation is `local/app`; builds belong in `artifacts`. Review fixtures use `qa-data`, never the personal installation's `data`.

Reuse `Ui`, `QuotaMeter`, `PanelAnchor` and the existing organization controls. Keep panel anchors stable, quota refreshes bounded and unchanged values motionless. Respect reduced motion and contrast. Do not add widgets as a side effect of cleanup or visual polish.

Hardware disconnects, global DPI/audio changes, suspension, Explorer restarts and audible screen-reader verification need a coordinated user session. Simulated checks do not prove physical behavior.

Package a clean build, never a personal installation. Exclude accounts, private paths, logs, backups and keys. Do not change Windows execution policies to run scripts.

## Structure and language

Keep file names, identifiers, comments, developer diagnostics and documentation in English. User-facing Spanish UI text and localized sample data are intentional; do not translate the product UI as an unrelated refactor. Feature directories organize the WinUI partial class without changing the app's persisted identifiers or assembly identity.

Keep the repository root for solution/build configuration and community documents. Use `scripts/` for tooling, `scripts/deployment/` for distributable maintenance sources, and feature folders under `src`. Do not restore legacy output directories or unused template pages.

## Git and publication

`main` is stable; `develop` integrates changes. Use scoped branches and record meaningful changes in the changelog. Never invent history or test results.

Follow the user's current authorization before creating remotes, uploading, tagging, publishing or changing visibility. Personal distribution uses the private `danieljcorreac/brisa-dock` repository; never make it public without explicit authorization. Do not store publication credentials in the repository.
