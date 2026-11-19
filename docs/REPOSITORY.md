# Repository and contribution flow

Public repository: [`danieljcorreac/brisa-dock`](https://github.com/danieljcorreac/brisa-dock). The initial source snapshot starts from one reviewed commit; prior local development history remains archived outside the repository.

- `develop` is the default contribution branch. Normal feature/fix PRs target it.
- `main` contains stable code and release tags. Release PRs promote verified changes from `develop`.
- Both branches require pull requests, resolved conversations and the GitHub Actions `verify` check, bound to the GitHub Actions application and up-to-date with the base branch.
- Rules apply to administrators too. Force pushes and branch deletion are blocked. No external approval is required for the sole maintainer to merge their own verified PR.
- Public visibility grants reading and forking, not write access. External contributors submit PRs from their forks; only maintainers with write access can merge them.
- Actions default to read-only. The manually invoked release job has scoped contents-write permissions and runs only from `main`. Never run contributor code with publishing secrets through `pull_request_target`.
- Private vulnerability reporting is enabled. Issues and PR templates are maintained in `.github`.

Publish binary packages as Release assets, not Git files. No credentials, account caches, personal settings, old development refs or build outputs belong in source history. Recheck actual GitHub configuration after changing repository settings; files alone do not configure protections.
