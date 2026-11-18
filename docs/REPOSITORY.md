# Remote repository setup

Personal distribution repository: [`danieljcorreac/brisa-dock`](https://github.com/danieljcorreac/brisa-dock), private. Source starts from one reviewed initial snapshot; prior local history is archived outside the repository.

Setup and verification:

1. Create the private repository and push only the reviewed `main` and `develop` branches. Do not push archived development refs.
2. Set `develop` as the default contribution branch; retain `main` as stable.
3. Apply rules available for this account and visibility: pull requests, resolved conversations and `Windows / verify`; block force pushes and deletion of `main`/`develop`.
4. Do not require an external approval that blocks the sole maintainer. Verify actual availability and configuration on GitHub.
5. Enable private vulnerability reporting if available; templates do not configure it.
6. Review Actions limits/budget and permissions: read-only by default, write access confined to the manual release job. Never execute contributor code through `pull_request_target` with publication permissions.
7. Enable Issues with the supplied templates. Normal PRs target `develop`.
8. Before making the repository public, repeat source/history, license and screenshot review. Creating a private remote does not authorize making it public.

Publish binary packages as Release assets, not Git files. No credentials, account caches, personal settings or build output belong in the source history.
