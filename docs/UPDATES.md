# GitHub updates

Open **Ajustes → Instalación y actualizaciones** to check immediately, review release notes, download and validate the latest stable package, then choose **Actualizar y reiniciar**. The existing maintenance helper preserves data and the previous application version. Recovery stays available from the same page.

Daily checking is enabled by default for managed installations and can be disabled on this page. Checks wait thirty seconds after startup, use a thirty-second deadline and never overlap. Both successful and failed checks are throttled across restarts. Downloads only start on request and have a ten-minute deadline. Visual fixtures do not perform background release checks.

While the repository is private, install the official [GitHub CLI](https://cli.github.com/) and sign in with `gh auth login` using an account with access to `danieljcorreac/brisa-dock`. Brisa invokes a fixed, read-only `gh api` request; it never runs `gh auth token`, saves a token, changes permissions or initiates a login. Without GitHub CLI, anonymous HTTPS requests work when the repository is public. An inaccessible private release produces a clear error, not an invented successful check.

Only non-draft, non-prerelease versions newer than the installed three-part version are offered. The package must have the exact versioned Windows x64 ZIP name, a numeric GitHub asset identifier, a bounded size and the SHA-256 digest supplied by GitHub. Download URLs are derived from the fixed repository and identifier rather than followed from release notes. The download digest, archive version, safe paths and every package manifest hash are verified before the existing update review is shown. Interrupted or invalid downloads are removed from their unique temporary directory. A prepared download can be reused during the current session.

GitHub account/repository control is the update trust boundary. These packages have no commercial Windows signature; SHA-256 does not defend against a compromised maintainer account. The updater never silently replaces the running application, offers a downgrade, or installs arbitrary links from release notes. Do not make the repository public just to bypass private authentication.

Metadata and download behavior follow the [GitHub release asset API](https://docs.github.com/en/rest/releases/assets) and [GitHub CLI API command](https://cli.github.com/manual/gh_api).
