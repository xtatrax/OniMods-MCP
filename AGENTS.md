# OniMods contributor instructions

## Development-first commits and selective public changes

Use `xt-dev-gpt` for ordinary implementation, testing, documentation and commits. Before editing, verify the current branch and worktree; do not use `fork_public` as the default development checkout. Preserve existing uncommitted work when switching.

Reflect only changes required by the public fork on `fork_public`: public-specific compatibility, identity/privacy/publishing safeguards, fixes to defects present in the public version, required dependencies of an accepted public change, or changes explicitly requested for public inclusion. A change being publishable does not by itself require public synchronization. Ordinary development features, experiments and gameplay records remain on `xt-dev-gpt` unless selected for public inclusion.

Read [the public synchronization procedure](docs/fork-public-sync_JP.md) when applying a selected public change. Sanitize it and commit it locally with neutral metadata; never merge private development history. This authorizes necessary local public commits, not remote pushes or publication. Report whether public reflection was needed, performed or blocked.

- Follow the user's current task and preserve unrelated work.
- Never commit credentials, private saves, runtime captures, local configuration, or game assemblies.
- Configure game paths with a git-ignored Directory.Build.props using the example file.
- Keep gameplay paused for observation and planning. Use short advance/pause/verify loops.
- Sandbox/debug actions require explicit authorization for the active test world.
- Livestream integrations are optional; configure the room explicitly and never send messages without authorization.
- Steam publishing is disabled in this fork. Do not reuse upstream Workshop targets or enable inherited write routes.
- Read docs/autonomous-iteration.md for maintenance evidence and validation boundaries.
- Run applicable host tests; distinguish host tests, reference builds and actual game runtime evidence.
