# OniMods contributor instructions

## Required public-branch companion commits

For future publishable code, test and documentation changes, also create a sanitized local commit on `fork_public` in the same task. Read and follow [the public synchronization procedure](docs/fork-public-sync_JP.md) before editing or committing. Exclude personal, local and environment-specific information; preserve the public identity and publishing guards. Never merge private development history into the public branch. This standing instruction authorizes local companion commits, not remote pushes or publication. Report explicitly when no public change applies or synchronization remains blocked.

- Follow the user's current task and preserve unrelated work.
- Never commit credentials, private saves, runtime captures, local configuration, or game assemblies.
- Configure game paths with a git-ignored Directory.Build.props using the example file.
- Keep gameplay paused for observation and planning. Use short advance/pause/verify loops.
- Sandbox/debug actions require explicit authorization for the active test world.
- Livestream integrations are optional; configure the room explicitly and never send messages without authorization.
- Steam publishing is disabled in this fork. Do not reuse upstream Workshop targets or enable inherited write routes.
- Read docs/autonomous-iteration.md for maintenance evidence and validation boundaries.
- Run applicable host tests; distinguish host tests, reference builds and actual game runtime evidence.
