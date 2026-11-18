# Releasing

## Branches and versioning

`main` is stable; `develop` integrates work. Use an optional `release/<version>` branch only when stabilization overlaps other development. Merge hotfixes into both branches.

`Directory.Build.props` is the single version source. Add meaningful changes under `Unreleased`, then move them into a dated `X.Y.Z` section before packaging. Never claim unpublished local candidates are existing GitHub Releases.

## Local preparation

```powershell
pwsh -File scripts/Test-Repository.ps1
dotnet run --project tests/Brisa.Checks -c Release
pwsh -File scripts/New-ReleasePackage.ps1
pwsh -File tests/Distribution.Checks.ps1 -RequireRelease
```

The package script builds in a fresh `artifacts/release-build-*` directory, copies deployment sources into `maintenance/`, includes notices, creates the root `Install-Brisa.cmd`, validates the manifest, embeds the ZIP into a self-contained setup executable and verifies its payload without installing.

Outputs go to `artifacts/releases/<version>`: ZIP, setup EXE, curated `RELEASE-NOTES.md` and `SHA256SUMS.txt`. An existing version directory is never silently overwritten. Archive it explicitly before creating another local candidate. `-PackageDirectory` can reuse a separately verified clean build; never point it at a personal installation.

## GitHub draft workflow

Only after remote setup and authorization, invoke the manual `Draft release` workflow on `main` with the exact version. It checks the version/changelog, audits source, runs checks, builds fresh assets, rechecks the stable branch head and creates a tag at that exact commit plus a **draft** Release. Existing tags are never overwritten. Inspect partial remote state before retrying a failed release.

Review the draft and its downloaded assets before final publication. There is no automatic final publish step, no personal access token in source and no signing secret.

## Integrity and limitations

SHA-256 confirms integrity against a known value; it does not authenticate an unsigned publisher. Do not disable Windows protections or change execution policy to run packages. Initial signing is intentionally unpaid and no commercial certificate is provided.

CI files do not configure branch protection or prove a GitHub run passed. The updater checks stable releases from the fixed repository at most daily, delegates private authentication to GitHub CLI and verifies the GitHub SHA-256 digest plus archive version and manifest before review. See [updates](UPDATES.md).
