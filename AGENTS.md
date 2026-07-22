# AI Collaboration Log (Claude Code / Antigravity / LM Studio)

This repo is worked on by multiple AI agents (Claude Code, Antigravity, and an
LM Studio local model used as a helper worker). They hand off context through a
shared, append-only log file — not through this file directly.

**Shared log:** `C:\Users\by365\ai-log\log-TeamProj.txt`

## Protocol

1. **Before starting work** in this repo, read the last 3-5 entries in the log
   above to see what the other agents did most recently (directory touched,
   task, outcome, commit hash).
2. **After finishing a unit of work** (a task, a commit, or a session), append
   one entry to the log using the format defined at the top of that file:
   ```
   [YYYY-MM-DD HH:MM +09:00] agent=<Claude Code|Antigravity|LM Studio:<model>>
   dir: <modified directory/directories>
   task: <one-line task description>
   summary:
     - <bullet>
   commit: <hash + short message, or "uncommitted">
   ```
3. Never edit or delete existing entries in the log — append only.
4. If a subtask was delegated to the LM Studio local model (via
   `C:\Users\by365\ai-log\tools\lm-studio-worker.ps1`), record that in the log
   with `agent=LM Studio:<model-name>` and note what it was asked and the
   outcome that was actually used.

## Unity tooling

- **Unity MCP가 미연결(unconnect)이면 Unity CLI로 작업한다.** 즉
  `mcp__unity__*` 툴(Pipeline 패키지 기반)이 붙어 있지 않거나 `unity status`가
  `STATUS_NO_INSTANCES`를 반환하면, Unity 관련 작업은 Unity CLI(`unity.exe`,
  `unity-cli` skill / `mcp__unity__unity_run`)로 수행한다.
- 이 규칙은 Claude Code / Antigravity / 기타 AI 툴 모두에 적용된다.

## Project notes

- Unity project. Main active work area right now: `Assets/Scripts/Test/Entity`
  (Entity/Monster AI: state machine + components) and `Assets/Scenes/Test`.
- Commit messages in this repo already tend to note the main edited directory
  (e.g. "main edit directory: Script - test - entity") — keep doing that, it
  mirrors the log format above.
