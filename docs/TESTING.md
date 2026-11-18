# Verification

## Automated checks

```powershell
dotnet run --project tests/Brisa.Checks -c Release
pwsh -File scripts/Build-Brisa.ps1
pwsh -File tests/Update.Checks.ps1
pwsh -File tests/Deployment.Checks.ps1
pwsh -File scripts/Test-Repository.ps1
pwsh -File tests/Distribution.Checks.ps1
```

The solution also builds with `dotnet build Brisa.slnx -c Release -p:Platform=x64`; runtime-specific publishing stays in the per-project build script.

Update and deployment checks default to `artifacts/build`; pass `-SourceDirectory` and, for update checks, `-BaselineDirectory` to use another isolated package. They must not depend on `local/app`.

Codex protocol checks use the check executable as a simulated service. They need no account and never start conversations. Claude failures use fictitious credentials and an in-process HTTP handler.

Update and rollback tests create `qa` directories, file locks and inert metadata. They do not register the user's applications. Deployment tests may create Shell shortcuts only in their isolated directories.

After `scripts/New-ReleasePackage.ps1`, run `tests/Distribution.Checks.ps1 -RequireRelease` to verify installer/ZIP hashes and embedded payload validation. A child Windows PowerShell with an empty PATH verifies self-contained installation without global .NET, user registration or personal data import.

## Interface

Use `pwsh -File scripts/Start-Brisa.ps1 -Review`. It launches the isolated build with `--check-visual --review-fixtures`. `--review-claude` combines with `--review-usage-error`, `--review-usage-empty` or `--review-usage-expired`; `--review-backups`, `--review-health` and `--review-app-manager` open other panels.

For README screenshots, add `--review-docs --review-app-manager` or `--review-docs --review-appearance` when launching the test executable directly. `--review-light` starts the light theme. These flags only act in fixture mode, set isolated sample data and do not read accounts. Capture the test window without the desktop or floating tooltips.

`--endurance` records local measurements every ten seconds in test mode. Report duration, open pages and whether real services were used. Never publish account caches or configurations.

Review a normal state, a relevant empty/error state and a second theme/scale for UI changes. An accessibility tree alone does not prove visual quality. Quota bars must not restart animation for identical percentages.

## Coordinated physical checks

Monitor/audio disconnection, global DPI changes, suspension, Explorer restart and audible Narrator verification need an agreed user session. Policies and geometry can be checked in isolation; do not describe those checks as physical verification.

## Tray and setup review

`BrisaDock.exe --check-tray-placement` registers a temporary notification icon and opens a separate menu beside its Shell rectangle. Its hidden diagnostic owner is placed on the secondary monitor when present. The report verifies that the menu stays in the anchor monitor's work area while the owner stays unchanged. It does not move the user's dock or change display settings. `--review-tray-menu` leaves the isolated menu open with inert commands for visual inspection.

Setup supports `--review`, `--review-error`, `--review-success`, `--review-busy`, `--review-primary` and `--review-secondary` for safe visual checks with an example path. Preview mode never runs the installer. It records its own DPI and control bounds in `brisa-setup-review.json` beside the test executable. The normal installation flow remains available only outside preview mode.

## Headless release checks

`Brisa-Setup.exe --check-layout` validates the supported window sizes and installation busy-state controls without showing a window or sending input. This complements earlier visual observations; it does not replace screenshots or physical accessibility checks.

`dotnet run --project tests/Brisa.Checks -c Release -- github-update-probe <installed-version> <isolated-cache>` exercises the production metadata/download verifier against the fixed repository. It never installs or opens the dock. A newer published stable release must exist to exercise downloading. The regular checks use inert HTTP/archive fixtures for invalid metadata, downgrades, prereleases, incorrect digests, truncated downloads and extraction cleanup.
