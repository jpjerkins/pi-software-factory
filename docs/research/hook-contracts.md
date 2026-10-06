# Claude Code hook contracts (for `factory hook <event>`)

Source: https://code.claude.com/docs/en/hooks.md, read 2026-10-05 (raw markdown, not a summary).
Covers only the six events the factory wires in `WorkerSettingsFile`.

## Common

- Command hooks get one JSON object on **stdin**. Common fields: `session_id`, `transcript_path`,
  `cwd`, `hook_event_name`, `permission_mode` (not on every event). Unknown fields must be ignored.
- The hook process inherits the parent environment, so `FACTORY_RUN_DIR` / `FACTORY_RUN_ID` are visible.
- **stdout must contain only the JSON object** (starts with `{`, ends with `}`), or it is treated as plain text.
- **Exit 2** blocks on events that can block. **Any other non-zero exit, a crash, or a hook that
  can't start is non-blocking: the action proceeds.** A timed-out PreToolUse hook does not block either.
  → A guard must catch everything and fail closed with exit 2 (or deny JSON), never exit 1.
- Stderr on exit 0 goes to the debug log only.

## PreToolUse (can block)

- Input adds `tool_name`, `tool_input`, `tool_use_id`.
- `tool_input` by tool:
  - Bash: `command`, `description`, `timeout`, `run_in_background`
  - Write: `file_path`, `content`
  - Edit: `file_path`, `old_string`, `new_string`, `replace_all`
  - Read: `file_path`, `offset`, `limit`
  - Glob: `pattern`, `path` (optional) · Grep: `pattern`, `path` (optional), `glob`, ...
  - NotebookEdit / MultiEdit: not in the doc's table; assume `notebook_path` / `file_path` and treat
    any unknown tool that carries a path-like field conservatively.
- For Write, Edit and Read, `file_path` is **always absolute**: `~` and relative paths are expanded before hooks run.
  This is not stated for Bash commands, Glob or Grep.
- Deny: exit 0 with
  `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"..."}}`.
  The reason is shown to Claude. Exit 2 does the same (stderr becomes the reason) and cannot be overridden.
- Allow / no opinion: exit 0 with empty stdout, so the normal permission flow applies.
- Deny and ask rules are still evaluated whatever the hook returns.

## PermissionRequest (exit 2 is NOT honored)

- Input: `tool_name`, `tool_input`, optional `permission_suggestions` (no `tool_use_id`).
- Fires when Claude Code would show a permission prompt, or would auto-deny a call that can't prompt.
- Deny **only** via JSON:
  `{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"deny","message":"..."}}}`.
  `interrupt: true` (deny only) stops Claude. Exit 2 without the JSON leaves the prompt up.

## Stop (can block)

- Input adds `stop_hook_active`, `last_assistant_message`, `background_tasks`, `session_crons`.
- `stop_hook_active` is true when Claude is already continuing because of a stop hook.
- Block: `{"decision":"block","reason":"..."}`. The reason is shown to Claude. Exit 2 with stderr also works.
- Built-in cap: after **8 consecutive** stop-hook continuations, Claude Code ends the turn anyway.
  The count resets each time Claude calls a tool. `CLAUDE_CODE_STOP_HOOK_BLOCK_CAP` raises it.
- Does not fire on user interrupt. API errors fire `StopFailure` instead (not wired).

## Notification (side effects only)

- Input adds `message`, optional `title`, `notification_type` (`permission_prompt`, `idle_prompt`, ...).
- It cannot block, and exit code and output are ignored apart from `terminalSequence`.

## SessionEnd (side effects only)

- Input adds `reason`: `clear`, `resume`, `logout`, `prompt_input_exit`, `other`.
- Output is discarded. **Default timeout is 1.5 s.** A per-hook `timeout` in settings raises it (budget up to 60 s).

## PermissionDenied (auto mode only)

- Fires only when **auto mode's classifier** denies a call. It does not fire for PreToolUse denials,
  deny rules or manual denials.
- Input adds `tool_name`, `tool_input`, `tool_use_id`, `reason`.
- The only output is `hookSpecificOutput.retry`. Exit code is ignored. The factory just logs it.

## Factory decisions that follow

1. pre-tool-use fails closed: any exception or missing `FACTORY_RUN_DIR` → deny JSON **and** exit 2.
2. permission-request always emits the deny JSON (exit 0). Exit 2 would do nothing.
3. stop blocks until `result.json` is valid, and **ignores `stop_hook_active`**. Writing the file needs a
   tool call, which resets the 8-cap, so a worker that keeps trying is never cut off. A worker that
   refuses ends after 8 blocks, and `OutcomeDetection` treats that as no result.
4. session-end must finish in well under 1.5 s, or `WorkerSettingsFile` sets `timeout` on that hook.
