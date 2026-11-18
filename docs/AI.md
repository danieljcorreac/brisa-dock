# Usage connections and privacy

## Claude

Choose Claude Desktop direct access or the existing Claude Code bridge. Direct access requires explicit consent. Brisa decrypts the local Desktop session only in memory and sends it only to `https://api.anthropic.com/api/oauth/usage` for a read-only usage request. It does not start conversations, renew the session or save the token in Brisa settings.

The Claude Code statusLine bridge writes a local `claude-usage.json` snapshot. This file may be older than the Desktop service's current reading; the UI identifies its source and freshness.

Displayed quota groups depend on the upstream response and include five-hour, weekly and Fable limits when available. Authentication rejection and account changes discard previous-account readings. Transient failures may retain a clearly labelled old snapshot; server retry delays are respected.

## ChatGPT / Codex

The ChatGPT widget reads the plan limits returned by the installed Codex executable through `account/rateLimits/read`. It does not count chat messages or show API spending, and never starts an agent conversation. Use the existing Codex sign-in; if automatic executable discovery is unavailable, select the installed `codex.exe` in connection options.

## Scheduling and local state

Providers have separate polling budgets and prevent duplicate in-flight queries. Automatic direct-provider checks normally run every five minutes; manual refresh is limited to once every thirty seconds. Rate-limit responses can defer the next attempt further. Local bridge file checks use a separate file-only interval.

Brisa settings preserve source selection and consent, not account tokens. Local history stores quota percentages and reset information. Never attach settings, account caches, notes or logs wholesale to a public issue.

Provider protocols and UI labels may change. Fixtures and diagnostics must use sample data and must not induce a real account failure to test recovery.
