# Changelog

Changes describe their effect for users and contributors. Tagged versions come from `main`; entries marked as local candidates are not published Releases.

## [Unreleased]

No additional changes recorded.

## [0.17.6] - 2026-11-19

### Changed
- Publish the source and releases for public downloads and fork-based contributions.
- Require pull requests and successful Windows verification on main and develop, including administrator changes.
- Retrieve update metadata and packages through anonymous HTTPS without GitHub CLI, accounts or tokens.
- Report unavailable releases and temporary GitHub rate limits clearly.
- Bind newly prepared release metadata to the exact stable source commit.

## [0.17.5] - 2026-11-18

First personal GitHub release. Earlier versions below were local development candidates.

### Added
- Ship the existing customizable dock, shortcuts, current widgets, multi-monitor preferences, account usage integrations and local recovery as a reviewed initial source snapshot.
- Check the fixed GitHub repository for stable releases at most once a day, notify about newer versions, and download a verified package for review before updating.
- Support private release access through the existing GitHub CLI session without reading or persisting its token in Brisa.
- Add headless installer layout/busy-state checks and remote-update corruption, metadata and version tests.

### Fixed
- Navigate upward to the last tray command when no command is focused.
- Keep setup progress visible when Windows reduces animations and use system text colors for errors in high contrast.

## [0.17.4] - 2026-11-18

Local candidate; no remote Release has been published.

### Fixed
- Anchor the notification-area quick menu to the Shell icon's screen rectangle and monitor, independently of the dock's monitor or DPI.
- Preserve keyboard access and dismiss the quick menu on Escape or focus loss.

### Changed
- Redesign setup with Brisa branding, rounded cards and controls, a clearer destination/options layout and installation status feedback.
- Add safe setup previews and an isolated cross-monitor tray placement check.

## [0.17.3] - 2026-11-18

Local candidate; no remote Release has been published.

### Changed
- Apply the approved dock-and-three-modules icon to the application, notification area and setup executable.
- Export eleven transparent Windows icon frames from 16 to 256 pixels and include the runtime icon in regular builds as well as published packages.

## [0.17.2] - 2026-11-18

Local candidate; no remote Release has been published.

### Fixed
- Round shortcut-list hover and selection surfaces, inset nested controls and give unpin actions more edge spacing.
- Keep drag handles compact and centered, with row height measured from content rather than a fixed estimate.

## [0.17.1] - 2026-11-18

Local candidate; no remote Release has been published.

### Changed
- Group Core and WinUI code by domain and feature, with a solution file for navigation.
- Move all operational scripts out of the root and use English file names, developer diagnostics and repository documentation.
- Put distributable maintenance scripts under `maintenance/`, with an English installer launcher.
- Separate the personal installation (`local/app`) from generated builds and release assets (`artifacts`).
- Remove unused template pages and unverified architecture profiles; archive legacy local builds outside the repository.
- Align build, setup, update, rollback, uninstall and verification paths with the new layout.

## [0.17.0] - 2026-11-18

Previous local preparation candidate; not a published GitHub Release.

### Added
- Development, contribution, security and licensing documentation, with sample-data screenshots.
- Project skills for development, verification and release preparation.
- Publication audits, isolated builds and verified packages.
- CI and manually triggered draft Releases with curated notes.
- Per-user installer with an embedded validated package.

## [0.16.6] - 2026-11-18

Local baseline; not presented as an existing GitHub Release.

### Fixed
- Label stale quota readings after transient failures and discard them after authorization rejection or account changes.
- Respect complete server retry delays.
- Bound Codex diagnostics and stabilize CPU sampling across closely spaced requests.
- Avoid identical settings writes and reject backups with duplicate fields.

### Verified
- Logic, update and recovery checks in isolation.
- Extended sample-data endurance run; this does not substitute for physical or continuous real-provider verification.
