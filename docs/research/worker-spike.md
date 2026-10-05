# Worker spike: wrapper, trust, Auto Mode guards, ARM64 baseline

Spike for issue #11 (chart #1), ahead of the dispatcher (#5). Run on pi5 (Linux arm64), 2026-10-04, with **HERDR 0.9.3** and **Claude Code 2.1.289**.

Setup: one throwaway HERDR tab `factory-spike-11` (`w1:t6` / pane `w1:p7`), created with `--no-focus` and closed at the end. A throwaway test dir had its own `.claude/settings.json` with a logging hook on every event, a guard on `PreToolUse`, and a statusline that logs `rate_limits`. A throwaway clone of task-guide was used for builds and worktrees. All of it lives in the session scratchpad (`.../scratchpad/spike11/`). No other pane was touched, and no global settings were edited.

Docs used: [permission-modes](https://code.claude.com/docs/en/permission-modes) (auto mode, thresholds, time limits), [permissions › workspace trust](https://code.claude.com/docs/en/permissions#project-allow-rules-and-workspace-trust), and [hooks](https://code.claude.com/docs/en/hooks).

## TL;DR

| # | Question | Answer |
|---|---|---|
| 1 | Does HERDR recognise Claude started through a wrapper? | **Yes**, three ways: `exec` wrapper, child-process wrapper, and `herdr agent start --kind claude` with a `claude` shim first on `PATH`. Status and the `--session-id` value were reported in all three cases. |
| 2 | How do you pre-trust a folder? | `~/.claude.json` → `projects["<path>"].hasTrustDialogAccepted: true`. **For a git worktree, the key is the main checkout's root.** If the factory clone is trusted once, every worktree of it starts with no dialog (tested). |
| 3a | Does the PreToolUse hook deny hold in Auto Mode? | **Yes.** `exit 2` blocked the command before it ran. The classifier never saw it. |
| 3b | What does Auto Mode do when it refuses? | It denies the call with no prompt, tells the model the reason, and the `PermissionDenied` hook fires. The session keeps working. |
| 3c | Can Auto Mode stall on a prompt? | **Yes, in specific cases.** An `ask` rule produced a prompt that waited with no timeout (watched for 80+ s). The `Notification` hook (`permission_prompt`) fired about 6 s later, and HERDR showed `blocked`. A `PermissionRequest` hook that returns `deny` stops the prompt from ever showing (tested). |
| 3d | Does the classifier use quota? | Not measurable here. This account uses **server-side classifier review**: the transcript has `serverClassifierRequest` markers and no separate classifier calls. The test session's whole cost estimate was about $0.16, about 1% of the 5-hour window. |
| 4 | ARM64 baseline | npm: **pass**. dotnet: **139 Api tests fail on the host** because they can read a root-only secret file. **All 728 pass** with `Secrets__EnvFile=/nonexistent/envfile`. About 50 s for dotnet, 36 s for vitest, 8 s for the build. |

## 1. Wrapper recognition

| Launch | HERDR `agent` / status | Session id reported | Notes |
|---|---|---|---|
| `pane run` → `worker-exec.sh` (`exec claude "$@"`) | `claude`, `blocked` at the trust dialog, then `idle`/`working`/`done` | yes, equal to `--session-id` | Foreground process is `claude` |
| `pane run` → `worker-child.sh` (runs `claude`, then writes `$?`) | `claude`, `idle` | yes | Foreground shows `[worker-child.sh, claude]`. The wrapper logged `exit=0` after `/exit`. |
| `herdr agent start spike11 --kind claude --pane P -- <args>`, with a `claude` shim script first on the pane's `PATH` | `claude`, `idle`, `interactive_ready: true`, name `spike11` | yes | `agent start` runs the bare name `claude`, so a `PATH` shim makes it run the factory wrapper. The shim logged its exit code. |

- The session id comes from Phil's HERDR integration hook in `~/.claude/settings.json` (`SessionStart`). A worker with its own `CLAUDE_CONFIG_DIR` won't load it (see herdr-sessions.md §1). The factory already knows the id because it passes `--session-id`. If it wants HERDR to know it as well, the worker's own `SessionStart` hook can call `herdr pane report-agent-session`.
- `/exit` gave `SessionEnd` with `reason: prompt_input_exit`, the pane went back to `bash`, and `agent list` dropped it.
- **Recommendation for #5:** use `herdr agent start --kind claude` with a factory `claude` shim on the slot's `PATH` (set with `tab create --env PATH=...`). It's the simplest option and gives you readiness, a name, and an exit code. `pane run` + child wrapper also works as a fallback.

## 2. Workspace trust

Observed:
- An untrusted dir showed the trust dialog. HERDR reported `blocked`. **No hooks ran before trust was accepted** (not even `SessionStart`), so only HERDR state or a screen read can see a session stuck here.
- Accepting the dialog wrote `projects["<dir>"].hasTrustDialogAccepted = true` to `~/.claude.json` (or `$CLAUDE_CONFIG_DIR/.claude.json` for a separate config dir; that part is from the docs, not tested).
- **Subdir of a trusted non-repo dir:** no dialog. Claude added a `projects` entry with `false`, and the dir was still trusted by inheritance.
- **Nested git repo under a trusted dir:** the dialog shows. A worktree placed under the trusted test dir still prompted.
- **Worktree:** accepting the dialog in worktree `tg-wt` wrote the key for the **main checkout** (`.../spike11/task-guide`), not for the worktree path. A second worktree (`wt2`, outside any trusted dir) then started with **no dialog**. This matches the docs: "In a worktree, it uses the main checkout's root."

What this means for the factory:
- Trust `/mnt/data/factory/repos/task-guide` (the factory clone) **once**. Then every per-issue worktree is trusted.
- There are two ways to do it. (a) Phil starts `claude` once in the clone under the worker config and accepts the dialog. (b) The factory sets the key in the worker's `$CLAUDE_CONFIG_DIR/.claude.json`, which is what the docs describe for doing it by hand. (a) is a single action and needs no code that edits Claude's state file. I recommend (a).
- Dispatcher guard: after launch, if HERDR says `blocked` and the screen shows `Accessing workspace`, stop, send `esc`, and raise `needs-phil`. Never auto-accept.

Not done: I did not hand-edit `~/.claude.json`. The spike left 3 `projects` entries for the throwaway paths in `~/.claude.json` (`.../spike11/autotest`, `.../autotest/sub`, `.../spike11/task-guide`). They are harmless. Phil can remove them, or leave them.

## 3. Auto Mode and guards

Started with `--permission-mode auto --model sonnet --effort low`. Auto mode needs Sonnet 4.6+ or Opus 4.6+; **Haiku is not supported**. The status bar showed `⏵⏵ auto mode on`. No auto-mode entry warning appeared, but Phil's `~/.claude.json` has already seen it (`hasSeenAutoModeEntryWarning`). **A fresh worker config dir may show that warning once (untested).**

Note: `defaultMode: "auto"` is ignored in project `.claude/settings.json`. Use the CLI flag (or user-level/`--settings`).

Turn 1 asked for three single tool calls.

| Step | What happened | Hooks seen |
|---|---|---|
| `echo FACTORY_BLOCK_TEST` | **Blocked by the factory hook** (`exit 2`, stderr reason shown to the model). It did not run, and the model did not retry. | `PreToolUse` only. No `PermissionDenied`, no `PostToolUse`. |
| `rm -rf "$SPIKE_JUNK"` (var set in the tab env and not visible to the classifier) | The hook allowed it, then **the classifier denied it** as `[Unverifiable Deletion Target]`. No prompt; the target dir survived. The status line showed `bash denied by auto mode · [...] · /permissions`. | `PreToolUse`, then `PermissionDenied` with `reason`. |
| Read a file outside the working dir | Ran with no prompt. The docs say the first outside read prompts once per config, and Phil's config has answered already (`hasSeenAutoModeOutsideReadPrompt`). **A fresh worker config would show this prompt once.** | `PreToolUse`, `PostToolUse` |

Turn 2 added `permissions.ask: ["Bash(echo SPIKE_ASK*)"]` to the project settings. It hot-reloaded and no restart was needed.

| Step | What happened |
|---|---|
| `echo SPIKE_ASK_AUTODENY` | A prompt was due. The factory `PermissionRequest` hook returned `{"decision":{"behavior":"deny","message":...}}`, so **no prompt showed** and the call was denied. |
| `echo SPIKE_ASK_STALL` | **A real prompt ("Do you want to proceed?") showed and waited.** It had no countdown; I watched it for 80+ s. HERDR said `blocked`. The `Notification` hook fired with `notification_type: permission_prompt` about 6 s after the prompt. `esc` interrupted the whole turn. |

When Auto Mode still prompts in a terminal (from the docs, plus what was seen above):
- `ask` rules (seen, no timeout).
- Critical-path `rm` (`rm -rf /`, `~`). This one has a 2-minute countdown and is then auto-denied.
- First Read/Grep/Glob outside the working dirs, once per config (the answer is saved).
- Writes that a symlink resolves to a protected path or outside the working dir.
- **Repeated blocks:** 3 classifier blocks in a row, or 20 in total, pause auto mode and **switch back to prompting** (docs; not reproduced).
- MCP tools marked `requiresUserInteraction`.
- Trust dialog at startup (before any hook).

**Guard design for #5:**
1. `PreToolUse` guard, `exit 2`. **Holds in auto mode** (tested). It runs before the classifier.
2. **`PermissionRequest` hook that always returns `deny`** with a message like "no human available; put it in result.json". This turns every would-be prompt into a denial, including the repeated-block fallback (assumed; not reproduced), so an unattended worker never stalls on a permission dialog. Tested for an `ask` rule. Log each one; they are attention signals.
3. `PermissionDenied` hook: log classifier denials (tool, input, `reason`). Several in a row means the worker is stuck or the classifier lacks context. A good escalation signal.
4. `Notification` hook: `permission_prompt` / `elicitation_dialog` / `idle_prompt` → the factory treats the slot as `blocked`. This backstops anything step 2 misses. HERDR `blocked` is the backstop for things that happen before hooks (trust dialog).
5. Don't use `ask` rules in worker settings. Use deny rules or the guard instead.

**Quota:** `/usage` read 49% (5-hour) / 15% (week) before and 53% / 16% after. That is useless as a delta, because this orchestrating Opus session ran at the same time. The test session's statusline `rate_limits` went from 50 to 51%, and its estimated cost was $0.16 for 2 short turns with 6 tool calls. The transcript carries `serverClassifierRequest` ids. That means the classifier verdict comes back **inside the main model request (server-side review)**, not as separate classifier calls. So there is no extra round-trip billing on this plan, but **I can't tell whether server-side review adds to plan usage**. Measuring that needs a quiet window and a longer run.

## 4. ARM64 baseline (throwaway clone, warm NuGet/npm caches)

| Command | Result | Wall clock |
|---|---|---|
| `dotnet test task-guide.slnx` (fresh clone, first build) | **FAIL**: Domain 249, Application 142, Storage 162, Infrastructure 36 pass; **Api 139/139 fail** | 50 s |
| same, with `Secrets__EnvFile=/nonexistent/envfile` | **PASS**: 728/728 (Api tests take 28 s) | 45 s |
| `npm ci` (src/TaskGuide.Web) | pass | 3 s |
| `npm test` (vitest) | pass: 328 tests in 30 files | 36 s |
| `npm run build` (tsc + vite) | pass | 8 s |

Api failure cause: `Program.cs` loads `/run/vault-t2-fs/envfiles/task-guide` with `optional: true`. On the pi5 host that FUSE file **exists but is root-only (`-r--------`)**, so it throws `UnauthorizedAccessException`. The code comment assumes the file is missing outside the container. That's a real task-guide issue, either in the code (catch the access error, or have tests set `Secrets:EnvFile`) or in the environment. It hits every worker on pi5.

Timeouts: about 1 minute for a full warm test run. Allow about 5 minutes for a cold restore or install, plus margin for concurrent load. `BASH_DEFAULT_TIMEOUT_MS=600000` is plenty. Not measured: a cold NuGet cache, or a second worker running at the same time.

## Implications for the dispatcher (#5)

- Launch: `tab create --env PATH=<factory-shim>:... --env FACTORY_RUN_DIR=...`, then `agent start issue-<n> --kind claude --pane P -- --session-id <uuid> --permission-mode auto --model sonnet --settings worker-settings.json "<short prompt>"`. The shim records the exit code.
- Worker env must include `Secrets__EnvFile=/nonexistent/envfile` until task-guide is fixed.
- Trust the factory clone once, and keep worktrees as worktrees of it. Never auto-accept a trust dialog.
- Worker settings need hooks on `PreToolUse` (guard + audit), `PermissionRequest` (always deny), `PermissionDenied` (log), `Notification` (stall signal), `SessionStart/Stop/StopFailure/SessionEnd`, and a statusline for quota. All of these worked from a settings file; only the `--settings` flag path is untested.
- Stall rule: `Notification permission_prompt|elicitation_dialog` or HERDR `blocked` lasting more than N minutes → `esc`, then `needs-phil` with a screen snapshot.
- `esc` on a prompt cancels the whole turn, not just the tool call. The nudge afterwards must say what to do next.

## Not verified / uncertainty

- Behaviour with a separate `CLAUDE_CONFIG_DIR`: first-run onboarding/login, the auto-mode entry warning, and the outside-read prompt. Phil's config had all three answered already.
- The repeated-block fallback (3 or 20 blocks) and whether the `PermissionRequest` deny hook catches it. Assumed from the docs, not reproduced.
- The classifier's quota cost. It is server-side here, and its share of usage can't be separated out.
- Hooks via `--settings <file>` (I tested project `.claude/settings.json`). They should behave the same.
- Cold-cache build timings, and builds running while another worker is busy.

## Open questions for Phil

1. **Trust the factory clone by hand once** (you start `claude` in it under the worker config and accept), instead of the factory writing `hasTrustDialogAccepted` into the worker's `.claude.json`? *Recommend: by hand once. All worktrees then inherit it.*
2. **Auto-deny every permission prompt** with a `PermissionRequest` hook, so workers never wait on a dialog (what would have been a prompt becomes a logged denial, plus a note in `result.json`)? *Recommend: yes. It's the only way Auto Mode is truly unattended.*
3. **task-guide Api tests fail on the pi5 host** (root-only vault file). Should the factory log a task-guide issue for the fix (catch `UnauthorizedAccessException` or have tests set `Secrets:EnvFile`), and set `Secrets__EnvFile=/nonexistent/envfile` in worker env meanwhile? *Recommend: both.*
4. **Launch via `herdr agent start` + `PATH` shim** rather than `pane run` + wrapper? *Recommend: yes. You get readiness, a name and the exit code.*
5. Leftover `~/.claude.json` `projects` entries for the 3 throwaway spike paths: leave them, or do you want to remove them yourself? *Recommend: leave. They are harmless.*
