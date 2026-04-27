# Agent Instructions

Repository guidance for agentic work is centralized in [CLAUDE.md](CLAUDE.md).

Before changing files in this repo:

1. Read [CLAUDE.md](CLAUDE.md).
2. Treat it as the source of truth for repository structure, workflow,
   verification, tool usage, configuration, security, testing, infrastructure,
   API contracts, and documentation conventions.
3. When it names Claude-specific tools or skills, use the closest equivalent in
   your agent runtime. If no equivalent exists, preserve the intent as closely
   as possible and state the limitation in your response.

For delegated or parallel agent work, pass along the same instruction: read and
follow [CLAUDE.md](CLAUDE.md) before making changes.

Keep this file thin. Put repository guidance changes in [CLAUDE.md](CLAUDE.md).
Update `AGENTS.md` only when this delegation behavior changes.
