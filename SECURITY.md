# Security

## Supported versions

Only the current development candidate is maintained during initial preparation. The first published Release will establish stable-version support; older-version support is not promised.

## Reporting a vulnerability

Do not post credentials, session caches, personal configurations or account-exposing reproductions in public issues. Enable GitHub private vulnerability reporting after repository creation if the account and repository support it. Until then, use an existing private channel with the maintainer; no security email is published.

Include the version, affected component, impact and a reproduction with fictitious data. There is no guaranteed response deadline.

## Boundaries

Brisa does not store account tokens in its settings. Direct Claude Desktop usage access requires consent and decrypts the local session in memory for a fixed read-only destination. Background diagnostics avoid retaining complete external exception messages.

Initial packages have no commercial Windows code signature. Checksums detect changes against a known hash but do not authenticate a publisher. Remote updates trust the fixed GitHub repository over HTTPS and require the asset SHA-256 digest supplied by GitHub, a matching archive version and all internal manifest hashes. This protects integrity, not against a compromised maintainer account. Updates use anonymous HTTPS only; Brisa never discovers credentials, invokes GitHub CLI or reads its session. Drafts, prereleases, downgrades, oversized archives and incomplete downloads are rejected. Updates are reviewed before replacing the running version.
