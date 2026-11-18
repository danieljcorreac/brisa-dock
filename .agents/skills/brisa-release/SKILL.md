---
name: brisa-release
description: Prepare Brisa versions, changelog notes, verified Windows packages and draft Releases. Use for packaging or release work, not ordinary UI edits.
---

Read `docs/RELEASING.md` and `docs/REPOSITORY.md`. Version is in Directory.Build.props; notes come from the matching CHANGELOG.md section. Never invent old commits or describe an unpublished package as a published Release.

Use `scripts/Test-Repository.ps1` and `scripts/New-ReleasePackage.ps1`. Build in a fresh directory, inspect the package manifest, keep third-party notices and verify the installer payload. Never package an installation that contains user data.

SHA-256 provides integrity against a known hash, not a commercial editor signature. Do not place signing keys, tokens or account state in source or assets. Do not weaken Windows policy to run an unsigned package.

Keep CI read-only and publishing confined to the manually invoked release job. PR code must not execute with publishing secrets. A draft must not be silently made public or overwrite an existing release.

Local preparation, creating a remote, pushing, tagging and publishing are separate actions. Follow the current user authorization at each boundary; do not infer visibility changes or final publication from local packaging alone. After remote approval, verify the repository, branch rules and first CI run instead of assuming they are configured by files alone.
