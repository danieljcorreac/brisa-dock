# Contributing to Brisa

Brisa is preparing its first publication. Remote repository setup remains pending owner approval.

## Environment

Use Windows 11 x64, Git, PowerShell 7 and the SDK pinned in `global.json`. Install official Windows/WinUI development tools as required. WinApp CLI is optional; do not weaken execution policies or Windows protections.

```powershell
dotnet build Brisa.slnx -c Release -p:Platform=x64
dotnet run --project tests/Brisa.Checks -c Release
pwsh -File scripts/Build-Brisa.ps1
pwsh -File tests/Update.Checks.ps1
pwsh -File tests/Deployment.Checks.ps1
pwsh -File scripts/Test-Repository.ps1
```

Update and deployment checks default to `artifacts/build` and operate only in generated `qa` directories. They do not need a personal installation or account. Read [verification](docs/TESTING.md) before hardware tests.

## Branches and pull requests

- `main`: stable code and release tags.
- `develop`: next-version integration and normal contribution target.
- `feature/<topic>` and `fix/<topic>`: scoped changes from `develop`.
- `release/<version>`: optional stabilization while other development continues; no new features.
- `hotfix/<topic>`: urgent fixes from `main`, also merged into `develop`.

Use descriptive English commit/PR titles, such as `fix: preserve focus when reordering shortcuts`. Add meaningful user-visible or maintenance changes under `Unreleased` in `CHANGELOG.md`. Move them to a dated version before packaging; do not manufacture older history.

Describe the problem, resulting behavior and actual verification. Attach sample-data screenshots for UI changes. For performance claims, include duration, load, version and limitations. Distinguish simulated behavior from observed UI and physical tests.

## Organization and scope

Follow [the layout guide](docs/LAYOUT.md). Keep developer-facing material in English; preserve existing localized UI text. Reuse shared presentation and navigation controls. Do not rebuild quota bars or make network calls on every visual tick.

New widgets, providers, permissions and synchronization require a separate scoped proposal. Never trigger real account failures or disconnect user hardware to test recovery.

## Privacy and licensing

Do not commit tokens, session caches, user settings, private paths or account screenshots. Original contributions use the project's MIT license; third-party assets need attribution and appropriate terms. Follow [SECURITY.md](SECURITY.md) for vulnerability reports.
