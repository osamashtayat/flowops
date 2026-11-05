@AGENTS.md

# Claude Code specifics

The import above is the shared project instruction set; it applies in full. (A `CLAUDE.md` in the repo stops Claude Code from reading `AGENTS.md` on its own, so it's imported here explicitly.)

- **Rules:** `.claude/rules/*.md` load automatically. `architecture.md`, `code-quality.md` and `security.md` always apply; `backend.md`, `database.md`, `frontend.md`, `ui-design.md` and `testing.md` load when you touch matching files.
- **Skills:** start a backlog task with `/implement-feature <TASK-ID>`; check finished work with `/verify-feature <TASK-ID>`; review a diff with `/review-change`; review screens with `/review-ui`.
- **Agents:** delegate to `backend-engineer`, `frontend-engineer` or `test-engineer` for focused implementation; use `architecture-reviewer`, `security-reviewer` and `ui-ux-reviewer` (read-only) before merging risky work. Don't spin up every agent for trivial changes.
- Use the task list for multi-step work and finish with the report format in `docs/AGENT_HANDOFF.md` §5.
