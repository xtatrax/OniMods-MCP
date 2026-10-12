# OniMods contributor instructions

- Follow the user's current task and preserve unrelated work.
- Never commit credentials, private saves, runtime captures, local configuration, or game assemblies.
- Configure game paths with a git-ignored Directory.Build.props using the example file.
- Keep gameplay paused for observation and planning. Use short advance/pause/verify loops.
- Sandbox/debug actions require explicit authorization for the active test world.
- Livestream integrations are optional; configure the room explicitly and never send messages without authorization.
- Steam publishing is disabled in this fork. Do not reuse upstream Workshop targets or enable inherited write routes.
- Read docs/autonomous-iteration.md for maintenance evidence and validation boundaries.
- Run applicable host tests; distinguish host tests, reference builds and actual game runtime evidence.
