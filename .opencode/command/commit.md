---
description: Review, stage, and commit the current session's intended changes.
agent: build
---

Create a Git commit for the work completed in this session.

Before committing:
- Inspect `git status --short`, `git diff`, and `git diff --cached`.
- Inspect `git log --oneline -10` to match the repository's commit-message style.
- Stage only the files relevant to this session. Do not stage unrelated user changes.
- Run the most relevant available validation, or explain when none is configured.

Commit with a concise imperative title and a body that describes the completed work. If the user supplies `$ARGUMENTS`, use them as the requested commit-message focus. Do not amend, force-push, or create an empty commit unless explicitly requested.

After a successful commit, report its short hash, title, body summary, and whether the worktree is clean.
