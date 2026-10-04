# Driving interactive Claude Code sessions in HERDR

Research for issue #10 (chart #1). Checked on pi5 (Linux arm64), 2026-10-04, against **HERDR 0.9.3** and **Claude Code 2.1.289**.

Sources: `herdr --help`, every command group's help, `herdr --skill` (HERDR's own agent guide), `herdr api schema --json`, the installed Claude integration hook, HERDR docs at [herdrdev/herdr v0.9.3 docs](https://github.com/herdrdev/herdr/tree/v0.9.3/docs) ([socket API](https://raw.githubusercontent.com/herdrdev/herdr/v0.9.3/docs/next/website/src/content/docs/socket-api.mdx), [agents](https://raw.githubusercontent.com/herdrdev/herdr/v0.9.3/docs/next/website/src/content/docs/agents.mdx), [session state](https://raw.githubusercontent.com/herdrdev/herdr/v0.9.3/docs/next/website/src/content/docs/session-state.mdx), [persistence](https://raw.githubusercontent.com/herdrdev/herdr/v0.9.3/docs/next/website/src/content/docs/persistence-remote.mdx)), Claude Code docs ([hooks](https://code.claude.com/docs/en/hooks), [statusline](https://code.claude.com/docs/en/statusline), [permission-modes](https://code.claude.com/docs/en/permission-modes), [interactive-mode](https://code.claude.com/docs/en/interactive-mode)), the [OpenRig repo](https://github.com/mvschwarz/openrig), and one throwaway HERDR tab.

Earlier notes still apply where they don't depend on `-p`: credentials and worktrees from [headless-worker-safety.md](headless-worker-safety.md), quota sources from [quota-reading.md](quota-reading.md).

## TL;DR

- **HERDR is scriptable enough.** It has a CLI that wraps a JSON socket API (`~/.config/herdr/herdr.sock`, newline-delimited JSON, with an event stream). It can create tabs/panes with a cwd and env vars, run commands, send text and keys, read the screen, wait for output, start a recognised agent (`agent start --kind claude`), prompt it and wait for it to settle. The assumption in #10 holds.
- **HERDR's idea of Claude's state is screen scraping.** Its docs say Claude Code state comes from "what they draw on screen". The installed Claude integration only reports the session id (for resume after a HERDR restart). Use HERDR state as a hint, not as truth.
- **Truth comes from factory-owned hooks writing files.** `SessionStart`, `Stop`, `StopFailure`, `Notification` and `PreToolUse` hooks in the worker's own config write events to `/mnt/data/factory/runs/...`. The worker ends each task by writing a `result.json` (done / needs_input / blocked / failed). A `Stop` hook refuses to let the turn end without it.
- **One Claude process per issue, in a fixed pool of HERDR "slot" panes.** Each slot is a shell pane. Per issue, the factory `cd`s the slot into the issue worktree and starts a fresh `claude` there. `/clear` is not enough between issues because the project dir (worktree) changes.
- **Start workers with `--permission-mode dontAsk` explicitly.** Since 2.1.283 the built-in interactive default is **auto mode** (an LLM classifier). `dontAsk` works interactively: anything not allowed is denied, never prompted, and `AskUserQuestion` is denied too. So a worker can't freeze on a dialog. Questions go into `result.json`.
- **Quota during a run: the statusline JSON `rate_limits` field is documented** (`five_hour`/`seven_day` `used_percentage` + `resets_at`). A factory statusline script in the worker config can write it to a file on every assistant message. Before a run, `claude -p "/usage"` (zero tokens) is still the best source; typing `/usage` into a worker pane and scraping it is a last resort.
- **Usage limit hit mid-task:** interactive Claude waits and continues on its own after a 5-hour reset (on by default since 2.1.234). This matches the chart rule "a running task is never interrupted". It does **not** auto-wait for resets more than 24h out (weekly); that case shows a menu, which the factory must detect.

## 1. HERDR control surface

### Model

Server (one per named session) → workspaces (`w1`) → tabs (`w1:t1`) → panes (`w1:p1`). IDs are opaque and never reused. A pane may contain a recognised **agent**, with a status of `idle | working | blocked | done | unknown`. Live agents can have a unique name (`[a-z][a-z0-9_-]{0,31}`).

Every pane gets `HERDR_ENV=1`, `HERDR_SOCKET_PATH`, `HERDR_WORKSPACE_ID`, `HERDR_TAB_ID` and `HERDR_PANE_ID` in its env. That includes hooks running inside Claude in that pane, so **hooks can tell which pane they're in**.

### Commands (all return JSON unless noted)

| Need | Command |
|---|---|
| List | `herdr workspace list`, `tab list --workspace W`, `pane list`, `agent list`, `api snapshot`, `session list --json` |
| Create | `tab create --workspace W --cwd PATH --label TEXT --env K=V --no-focus` (returns `.result.root_pane.pane_id`); `pane split <pane> --direction right\|down --cwd --env --no-focus`; `workspace create --cwd --label --env` |
| Run a shell command | `pane run <pane> "<cmd>"` (text + Enter) |
| Send raw input | `pane send-text`, `pane send-keys <pane> esc ctrl+c ...` |
| Read screen | `pane read <pane> --source visible\|recent\|recent-unwrapped --lines N` (**plain text, not JSON**); `agent read` adds `--source detection` |
| Wait for text | `pane wait-output <pane> --match TEXT\|--regex RE --timeout MS` |
| Process info | `pane process-info --pane P` (foreground argv, pids, cwd) |
| Start an agent | `agent start <name> --kind claude --pane P [--timeout MS] -- <claude args>` (pane must be an idle shell; returns when Claude is ready, or `agent_not_ready` if it is blocked at startup) |
| Prompt an agent | `agent prompt <name> "<text>" --wait --timeout MS` (bracketed paste + Enter; refuses with `agent_blocked` if a dialog is open; `--wait` waits for `idle`/`done`/`blocked`) |
| Wait on state | `agent wait <name> --until blocked\|idle\|done --timeout MS` |
| Close | `pane close P`, `tab close T` (only things the factory created) |
| Report state | `pane report-agent`, `pane report-agent-session`, `pane report-metadata` (titles, state labels, sidebar tokens) |
| Notify Phil in the TUI | `notification show <title> --body ... --sound request` |
| Worktrees | `worktree create/open/list/remove` (HERDR's own; the factory should keep using its own `git worktree`, see §2) |

Socket API: `herdr api schema --json` prints the full JSON Schema (protocol 22). The methods match the CLI (`pane.split`, `agent.start`, `agent.prompt`, `pane.read`, ...). **`events.subscribe`** keeps the connection open and pushes events such as `pane.agent_status_changed`, `pane.output_matched`, `pane.created/closed/exited`, `tab.closed`. If a subscriber falls behind, it gets `events_lost` and is disconnected, so the dispatcher must reconnect and re-read state. The docs mention no auth; the socket is protected by file permissions only.

### Detecting exit

- **Tested:** when the shell in a pane exits, the pane closes by itself, and a tab with only that pane closes too. After that, `pane get` returns `pane_not_found`.
- There is **no exit code** in the API. `pane.exited` carries only ids.
- When `claude` exits back to the shell, the pane stays and the agent is released. Detect this with `agent list` (agent gone), `pane process-info` (foreground is `bash`, not `claude`), or the `SessionEnd` hook (below). For an exit status, run Claude through a tiny wrapper that writes `$?` to the run dir.

### Experiment (one throwaway tab, removed)

1. `tab create --workspace w1 --cwd <scratchpad> --label factory-research-test --env FACTORY_TEST=hello --no-focus` → tab `w1:t5`, pane `w1:p6`. No other pane was resized or touched.
2. `pane run w1:p6 'echo hi $FACTORY_TEST; echo DONE-MARK'` → output `hi hello`. **`--env` works.**
3. Gotcha: `wait-output --match DONE-MARK` matched the **echoed command line**, not the output. Use `--regex '^...$'` or a marker that does not appear literally in the command (e.g. built by `printf`).
4. `pane get` showed `agent: null`, `agent_status: unknown` for a plain shell.
5. `pane run w1:p6 'exit 3'` → about 2 s later the pane and tab were gone. Nothing left to clean up.

Not run: `agent start`, anything in other panes, `session`/`server`/`config`/`integration` mutations.

### Things to avoid

- `herdr server stop`, `herdr update`, `server reload-config`, `session stop default`: these kill or change every pane, including Phil's and this dispatcher.
- Running bare `herdr` from a script: it attaches a TUI.
- Targeting the "focused" pane: always pass explicit pane ids or agent names.
- `herdr integration install/uninstall claude`: edits `~/.claude/settings.json`.

### HERDR state for Claude

- Claude's status comes from **screen heuristics** in HERDR's agent manifests ([agents doc](https://raw.githubusercontent.com/herdrdev/herdr/v0.9.3/docs/next/website/src/content/docs/agents.mdx)). The docs warn new Claude UI may be misread as `idle` until the manifest updates. `blocked` is strict (an approval or question UI).
- The installed integration (`~/.claude/hooks/herdr-agent-state.sh`, v10) only runs on `SessionStart` and reports the session id and transcript path. It does **not** report working/idle.
- A reporting integration's state takes precedence over the screen. So a factory hook *could* call `herdr pane report-agent ... --state working|idle|blocked` to make HERDR's sidebar accurate. That's optional and nice to have.
- **Workers with their own `CLAUDE_CONFIG_DIR` won't load the HERDR hook** (it lives in `~/.claude/settings.json`). They'll show as `claude` by screen detection but without a session id, so HERDR won't auto-resume them after a server restart. For workers that is arguably better: the factory decides about resume.
- Panes **do not survive a HERDR server restart**. Layout comes back as fresh shells. Agents with a reported session id are resumed with `claude --resume <id>` (`[session] resume_agents_on_restore`, on by default).

## 2. Starting a worker: own session vs Phil's pool

Constraint: Claude's project root is the directory it started in. Settings, `CLAUDE.md`, the trust state and the file-tool scope all follow it. A per-issue worktree (chart decision) means **a new `claude` process per issue**. A long-lived Claude session can't move into the next worktree.

| Option | How | Verdict |
|---|---|---|
| **A. Factory-owned slot panes** | The dispatcher creates N tabs `factory-slot-1..N` in a `factory` workspace, each with factory env (`--env`). Per issue: `pane run <slot> "cd <worktree>"`, then `agent start issue-<n> --kind claude --pane <slot> -- <flags> "<prompt>"`. On finish: exit Claude (`/exit`), and the slot is a shell again. | **Recommended.** Deterministic, ids known, env controlled. |
| **B. Phil opens shell panes as slots** | Same as A, but Phil creates/labels the panes and the factory adopts any pane labelled `factory-slot-*` that is an idle shell. | Fine as a variant. The factory must check env/cwd itself, since Phil's pane env is his own. |
| **C. Phil opens Claude sessions; the factory hands them work** | The factory types the task into an already running Claude. | **Not recommended.** Wrong cwd (not the worktree), Phil's config/credentials/permissions, no factory hooks, can't enforce `dontAsk`. Only useful for "Phil, please look at this" handoffs. |

"Fixed pool" maps onto **N slots** = the max concurrency (1 in v1). Phil can watch any slot live in the HERDR TUI and type into it if he wants.

Launch sketch (inside the slot, cwd = worktree):

```bash
/opt/factory/bin/worker-launch   # wrapper: sets env, runs claude, writes exit code
# which runs, roughly:
env CLAUDE_CONFIG_DIR=/mnt/data/factory/claude-home \
    FACTORY_RUN_DIR=/mnt/data/factory/runs/<issue>/<run-id> \
    GH_CONFIG_DIR=/mnt/data/factory/empty-gh GIT_CONFIG_GLOBAL=/mnt/data/factory/worker.gitconfig \
    GIT_TERMINAL_PROMPT=0 DISABLE_AUTOUPDATER=1 \
  claude --session-id <uuid> --model sonnet \
    --permission-mode dontAsk \
    --settings /mnt/data/factory/worker-settings.json --strict-mcp-config \
    "Read $FACTORY_RUN_DIR/prompt.md and follow it."
echo $? > $FACTORY_RUN_DIR/exit_code
```

- Use `herdr agent start` with the wrapper only if HERDR recognises the wrapper as `claude` (it checks the foreground process; the wrapper `exec`ing `claude` should work, **untested**). Otherwise use `pane run` + `agent wait`.
- A prompt passed on the command line is submitted on its own. Keep it short and put the real task in `prompt.md`, so nothing depends on pasting large text.
- **Workspace trust dialog:** a new worktree dir will show "do you trust this folder?" in interactive mode, and the worker would sit `blocked` at startup. OpenRig handles this by pre-writing trust for the workspace before launch. The factory needs the same, by pre-accepting trust for the worktree path in the worker's config dir. *Exact key not verified here; it needs a spike.* An alternative is to keep all worktrees under one trusted parent, if trust is inherited (also unverified).
- First run of a new `CLAUDE_CONFIG_DIR` needs onboarding/login once (Phil, by hand, or `CLAUDE_CODE_OAUTH_TOKEN`).

## 3. Telling done / needs Phil / stuck / crashed

Layered signals, most trusted first:

1. **`result.json` written by the worker** (same exit contract as `headless-worker-safety.md` §6: `status` done | needs_input | blocked | failed, plus summary, question with options/recommendation/reasoning, commits, tests). `prompt.md` / `start-lane` says: "finish by writing `$FACTORY_RUN_DIR/result.json`".
2. **Factory hooks** in `/mnt/data/factory/claude-home/settings.json`. Each appends one JSON line to `$FACTORY_RUN_DIR/events.jsonl` with `HERDR_PANE_ID`, `session_id` and a timestamp:
   - `SessionStart` (source `startup|resume|clear|compact`): records `session_id` and `transcript_path`. It can also return `additionalContext` (e.g. issue number).
   - `Stop` (turn ended, has `last_assistant_message`): if `result.json` is missing or invalid, return `decision: "block"` with reason "write result.json per the contract". Respect `stop_hook_active` to avoid loops. Allow a few blocks at most, then let it stop and mark the run `failed: no result`.
   - `StopFailure` (`rate_limit`, `authentication_failed`, `server_error`, `billing_error`, ...): turn died on an API error.
   - `Notification` (`permission_prompt`, `idle_prompt`, `elicitation_dialog`, `quota_auto_resume_fired|stale|disabled`, ...): the session is waiting on something or quota auto-resume changed.
   - `PreToolUse`: the guard (§6), plus a heartbeat line per tool call.
   - `SessionEnd` (reason `prompt_input_exit|clear|other|...`): Claude is exiting.
3. **HERDR** `pane.agent_status_changed` events (`blocked` = a dialog is on screen) and `pane.closed/exited`. Use these as hints and for the "crashed" case.
4. **Screen read** (`agent read --source recent-unwrapped`): last resort, and to attach a snippet to a `needs-phil` item.

Classification:

| State | Rule |
|---|---|
| **done** | `result.json` status `done` **and** `Stop` seen. The factory then exits Claude and verifies commits/tests itself. |
| **needs Phil** | `result.json` status `needs_input`/`blocked`; or HERDR `blocked` / `Notification` `permission_prompt`/`elicitation_dialog` lasting > 5 min (shouldn't happen under `dontAsk`); or the usage-limit menu (weekly reset, see §4). |
| **waiting for quota** | `StopFailure rate_limit` or `Notification quota_auto_resume_*`; the screen shows `Usage limit reached · continuing automatically at …`. Not stuck: leave it. |
| **stuck** | Status `working`, but no hook event, transcript growth or screen change for X min (start at 20 min; a Pi `dotnet test` can be slow). Or idle with no `result.json` after the `Stop` retries. Action: `send-keys esc`, then one nudge prompt; if still nothing, `needs-phil`. |
| **crashed** | `SessionEnd` or Claude gone from `agent list` / `process-info` with no `result.json`; non-zero `exit_code` from the wrapper; or the pane vanished. Keep the worktree (chart rule), mark failed. |

Wall-clock limit: no `--max-turns` / `--max-budget-usd` interactively (they are `-p` only). The dispatcher enforces a timeout itself: `esc` (interrupt the turn), then `/exit`, then `ctrl+c` twice.

## 4. Quota in interactive sessions

- **Statusline `rate_limits` is documented** ([statusline](https://code.claude.com/docs/en/statusline#rate-limit-usage)): `rate_limits.five_hour.used_percentage` (0–100), `.resets_at` (epoch s), same for `seven_day`. Only for Pro/Max, only after the first API response, each window may be absent, a window is dropped once `resets_at` passes. No per-model fields. The script runs at session start, on every new assistant message, after `/compact`, on mode change, at a window's `resets_at`, and every `refreshInterval` s if set (debounced 300 ms). It also gets `session_id`, `transcript_path`, `cwd`, cost and context fields.
  - **Plan:** the worker config's `statusLine` is a factory script that appends `{ts, pane, session_id, five_hour, seven_day}` to `/mnt/data/factory/state/quota.jsonl` (de-duplicated) and prints a short line. Live, free, account-wide. It replaces the `-p` `rate_limit_event` from [quota-reading.md](quota-reading.md). This is exactly what OpenRig does (its "context collector" statusline writes rate-limit data to a state file).
- **`/usage` before a run:** `claude -p "/usage"` costs zero tokens (quota-reading.md, run 1). It isn't a worker (no model call), so it doesn't break the "no headless workers" decision. *Phil to confirm (Q2).* The alternative is to type `/usage` into an idle slot and scrape the panel. That's fragile; last resort only.
- **Limit hit mid-run:** interactive Claude waits and continues after the reset on its own (`autoContinueAtUsageLimit`, default on, v2.1.234+). It re-arms at most twice. It **does not auto-wait** if the reset is > 24 h away (the weekly limit) or for an Opus/Sonnet-only limit while on another model. Then it opens a usage-limit menu once, which looks `blocked`. The factory should treat that as "waiting for weekly reset": press `esc`, record it, and resume later with `claude --resume <session_id>` in the same slot and worktree. This also fits the chart rule "never interrupt at the floor; run until done or hard limit".
- Phil's own `~/.claude/settings.json` statusline is not touched.

## 5. Between issues

- **New `claude` process per issue** (§2). Exit with `/exit` (Ctrl+D also works, but needs a second press within 800 ms). Confirm with `SessionEnd` and `process-info`. Then `cd` to the next worktree and launch again.
- `/clear` starts a new session id in the same process (`SessionStart source=clear`). It is fine for a **follow-up in the same worktree**, e.g. the separate review session or fix round, *if* the same model is wanted. But a review must be a separate session that doesn't share the worker's context (chart: "a session never reviews its own work"). A fresh process per role is simplest and also lets the factory switch Sonnet → Opus with `--model`.
- After a question to Phil: keep the worktree; later run `claude --resume <session_id>` in a slot, with the answer as the prompt. The slot need not stay occupied while Phil thinks.
- The slot pane itself is long-lived. If it vanishes (crash, HERDR restart), the dispatcher recreates it.

## 6. Permissions and credentials in interactive mode

- **Interactive default is now auto mode** (v2.1.283+ built-in default). Workers must start with `--permission-mode dontAsk` (or `permissions.defaultMode: "dontAsk"` in the worker settings, ideally both). In `dontAsk`: allow-listed and read-only actions run, `PreToolUse`-approved calls run, everything else is **denied without a prompt**, `ask` rules become denies, and `AskUserQuestion` is denied. The session never waits for input, which is what an unattended slot needs. The status bar shows `⏵⏵ don't ask on`, so Phil can see it at a glance. `dontAsk` is not in the Shift+Tab cycle.
- Same three layers as before: narrow allow list + **factory `PreToolUse` guard** (exit 2 blocks before rules are evaluated; logs every call) + **no credentials in reach**. The allow list and deny list from `headless-worker-safety.md` carry over unchanged.
- Denials: in `-p` they appeared as `permission_denials`. Interactively, record them from `PreToolUse` (the guard sees every attempt). An optional `PostToolUse`/transcript scan can catch rule-level denials. *Untested whether any hook fires on a `dontAsk` denial; spike.*
- **Credentials:** a HERDR pane runs as `philj` with the HERDR server's environment. Option B (soft isolation by env: empty `GH_CONFIG_DIR`, factory `GIT_CONFIG_GLOBAL`, no `GH_TOKEN`, deny rules + guard) works by setting env in the launch wrapper. Option A (separate `factory-worker` user) still works: the wrapper runs `sudo -u factory-worker -i …` inside the slot. That needs a sudoers rule, and HERDR env (`HERDR_PANE_ID`, socket) has to be passed through explicitly if hooks want it. The worker user also can't reach Phil's HERDR socket unless granted. Both are Phil decisions; chart/#4 already lean B first, then A.
- Interactive-only risk: anyone attached to the HERDR TUI can type into a worker. That's a feature for Phil; just remember that the HERDR socket is effectively "full control of every pane" for any `philj` process, **including a worker**. Under option B a worker could call `herdr pane run` on another pane. The guard should block `herdr` and the socket path; option A removes the risk.

## 7. OpenRig as inspiration

From its [transport doc](https://github.com/mvschwarz/openrig/blob/main/docs/as-built/architecture/transport-and-transcripts.md) and source:
- "tmux is transport, not truth": state lives in its daemon (SQLite), the same principle as our chart.
- Sends text via a uniquely named tmux buffer, `paste-buffer -d -r -p` (bracketed paste, raw LF). HERDR's `agent prompt` already does the equivalent.
- "Send-readiness classification": it refuses to send while the agent is mid-work or at a dialog. HERDR's `agent_blocked` refusal is the same idea.
- Activity **hooks** post event type/session id to its daemon; a **statusline collector** writes context + rate-limit data to a state file; managed startup **pre-trusts the workspace**. All three map 1:1 onto the design above (files instead of an HTTP daemon).
- Transcripts are periodic `capture-pane` snapshots to files. Optional for us: Claude's own JSONL transcript is better.

## 8. Recommended v1 design

1. **Topology:** a HERDR workspace `factory`, with the dispatcher in one tab (already decided) and **one worker slot tab** (v1 concurrency = 1), created by the dispatcher with `--no-focus` and labelled `factory-slot-1`.
2. **Per issue:** the factory makes the worktree, writes `runs/<issue>/<run>/prompt.md`, pre-trusts the path, then runs the launch wrapper in the slot: `claude --session-id <uuid> --permission-mode dontAsk --settings worker-settings.json --model sonnet "Read …/prompt.md and follow it."` with `CLAUDE_CONFIG_DIR=/mnt/data/factory/claude-home` and soft-isolated env.
3. **Observe:** subscribe to HERDR events for the slot; tail `events.jsonl` (hooks) and `quota.jsonl` (statusline); poll `process-info` every minute as a backstop.
4. **Finish:** on `result.json` + `Stop`: exit Claude, verify, then start the **review** run the same way with `--model opus` in the same slot (fresh process, same worktree, review prompt). Then merge/push per chart rules.
5. **Needs Phil:** post the question from `result.json` to GitHub (`needs-phil`), free the slot, and resume later with `--resume <session_id>`.
6. **Quota:** `claude -p "/usage"` before each start (if Q2 = yes), statusline samples during the run, built-in auto-continue for 5-hour limits, and `esc` + park for the weekly menu.
7. **Stuck/timeout:** watchdog as in §3. Escalation = `esc` → one nudge → `/exit` → `needs-phil` with the last 60 screen lines.

Spikes before relying on it (each small, one slot, a throwaway repo): (a) `agent start --kind claude` via the wrapper is recognised; (b) pre-trusting a worktree avoids the trust dialog; (c) `dontAsk` interactive: what a denial looks like, and whether a hook sees it; (d) `Stop`-hook "write result.json" loop behaves; (e) statusline fires in an unfocused, unattached pane (it should; HERDR keeps a virtual terminal).

## Open questions for Phil

1. **Pool shape:** should the factory create and own its worker slot panes (A), or adopt shell panes you open and label `factory-slot-*` (B)? Handing work to Claude sessions you started yourself (C) is not recommended. *Recommend: A, with B allowed later. v1 = 1 slot.*
2. **`claude -p "/usage"` for quota checks:** it's a zero-token, non-worker call. Is it OK under the "no headless" decision? *Recommend: yes. It's a meter read, not a worker. Statusline samples cover the run itself.*
3. **Permission mode:** start workers in `dontAsk` (no prompts ever; questions only via `result.json`) rather than Manual (prompts wait for you) or auto (LLM classifier)? *Recommend: `dontAsk`. You can still type into the pane if you're watching.*
4. **Weekly limit mid-task:** when Claude shows the usage-limit menu (reset > 24 h away), should the factory cancel it and park the issue, or leave the session sitting until the reset? *Recommend: park (`esc`, free the slot, `--resume` after reset). Same result, and the slot isn't hostage.*
5. **Auto-continue after a 5-hour limit:** keep Claude's built-in auto-continue on for workers? It continues even if your reserve window has started. *Recommend: keep it on for v1. The chart already says running tasks finish. Revisit if it eats your weekend reserve.*
6. **Worker HERDR access:** under soft isolation a worker could drive other panes via the HERDR socket. Block `herdr`/the socket path in the guard now, and move to a separate Linux user before truly unattended runs? *Recommend: yes to both (same as #4's B-then-A).*
7. **HERDR sidebar accuracy:** should factory hooks report `working/idle/blocked` to HERDR (`pane report-agent`) so your TUI shows true worker state? *Recommend: yes, later. Cheap, but not needed for the factory's own decisions.*
8. **Review session:** run the review as a fresh Opus process in the same slot and worktree right after the worker exits? *Recommend: yes. Fresh process, never `/clear` in the worker's process.*
