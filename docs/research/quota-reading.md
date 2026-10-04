# Reading Claude Code quota (5-hour and weekly) from the dispatcher

> **Decision (2026-10-04, chart #1): the factory will NOT use headless workers (`claude -p`).** Workers are real, interactive Claude Code CLI sessions hosted in a HERDR server; Phil can open a fixed number of them manually if needed. Findings below that depend on headless mode (exit contract, `--resume`, `dontAsk`, stream-json events) do not apply. Driving interactive sessions is researched in a follow-up ticket.

Research for issue #2 (chart #1). Checked against Claude Code **2.1.289** on pi5, 2026-10-04, subscription login.
Sources: official docs at code.claude.com (linked inline), the [CHANGELOG](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md), anthropics/claude-code GitHub issues, strings in the installed `claude` binary, the OpenRig repo, and two small test runs.

## TL;DR

- **Two good sources, both account-wide (they include Phil's interactive use and other devices):**
  1. **`claude -p "/usage"`** prints the plan bars as text: `Current session: 30% used · resets Oct 4, 6:40pm (America/Chicago)` and `Current week (all models): 13% used · resets Oct 8, 2am`. **It costs zero tokens** (`total_cost_usd: 0`, `num_turns: 0`) and took 250 ms inside the process. Use it **before starting a task**.
  2. **`rate_limit_event`** in `--output-format stream-json`. It is emitted on the first API response of every run, and again whenever a window's rounded percentage or reset time moves. It carries **`unifiedWindows.five_hour` and `.seven_day`** with `utilization` (0–1) and `resetsAt` (epoch seconds). Use it **during a run** to watch the burn and stop the worker at the floor.
- The previous note's claim is **mostly right but incomplete**: the documented top-level `utilization` is *absent* while `status` is `allowed`. The per-window numbers are in `unifiedWindows`, which works but is marked `@internal` in the code and is not in the public SDK types.
- **No `claude usage` subcommand exists.** `claude --help` lists none.
- **The statusline JSON has the same data** (`rate_limits.five_hour/seven_day.used_percentage`, `resets_at`). But it is a TUI feature, and it is not useful for `-p` workers.
- **OpenRig does expose quota over HTTP** (`/api/provider/status`, `/api/telemetry/usage/*`). It gets the data by installing a **statusline sidecar** into its interactive tmux "seats". That does not help with `claude -p` workers, and OpenRig is not installed. Don't adopt it for this.
- **Don't call `/api/oauth/usage` directly** with Claude Code's OAuth token. It is undocumented, it rate-limits, and using the subscription token outside Claude Code is a ToS risk. `claude -p "/usage"` gives the same data with Claude Code making the call itself.
- **Local transcripts / ccusage** only give tokens from this machine, not plan %. Use them only for per-task cost history and as a fallback.
- **Per-model limits exist as categories**: "Opus limit", "Sonnet limit" and "Fable limit" (`seven_day_opus`, `seven_day_sonnet`, `seven_day_overage_included`). Phil's `/usage` today shows only "Current week (all models)", so **no separate Opus/Sonnet weekly cap applies to this account right now**. Parse for extra rows anyway.

## Test runs (2 runs, in the scratchpad)

**Run 1:** `claude -p "/usage" --output-format stream-json --verbose --max-turns 1`. Exit 0. Events: `system/init`, hooks, one `assistant` text message, `result/success`. No `rate_limit_event` (no API call). `result.result` text:

```
You are currently using your subscription to power your Claude Code usage

Current session: 30% used · resets Oct 4, 6:40pm (America/Chicago)
Current week (all models): 13% used · resets Oct 8, 2am (America/Chicago)

What's contributing to your limits usage?
Approximate, based on local sessions on this machine — ...
Last 24h · 372 requests · 5 sessions
  89% of your usage came from subagent-heavy sessions ...
```

`total_cost_usd: 0`, all token counts 0, `modelUsage: {}`.

**Run 2:** `claude -p "say hi" --model haiku --output-format stream-json --verbose --max-turns 1`. Exit **1**: haiku tried a tool and hit `error_max_turns`, so non-zero exit isn't only for quota errors. Estimated cost $0.023. One `rate_limit_event`:

```json
{"type":"rate_limit_event","rate_limit_info":{
  "status":"allowed","resetsAt":1791157200,"rateLimitType":"five_hour",
  "overageStatus":"rejected","overageDisabledReason":"out_of_credits","isUsingOverage":false,
  "unifiedWindows":{"five_hour":{"utilization":0.3,"resetsAt":1791157200},
                    "seven_day":{"utilization":0.12,"resetsAt":1791442800}}},
 "uuid":"…","session_id":"…"}
```

1791157200 = Sun Oct 4 18:40 CDT and 1791442800 = Thu Oct 8 02:00 CDT, which match `/usage`. Weekly shows 12% here and 13% in `/usage`. The two come from different sources (response headers vs the usage endpoint) and are rounded differently, so expect ±1–2 points.

## Sources

### 1. stream-json `rate_limit_event` (recommended: during a run)

- **Fields.** Schema from the binary (`C8r`):
  - `status`: `allowed | allowed_warning | rejected`
  - `resetsAt?` (epoch s)
  - `rateLimitType?`: `five_hour | seven_day | seven_day_opus | seven_day_sonnet | seven_day_overage_included | overage`
  - `utilization?` (0–1)
  - `unifiedWindows?`: `{five_hour?, seven_day?, seven_day_overage_included?}`, each `{utilization, resetsAt}`
  - plus overage fields (`overageStatus`, `overageResetsAt`, `overageDisabledReason`, `isUsingOverage`, …) and `errorCode: "credits_required"`.
- **Public docs** ([TS SDK › SDKRateLimitEvent](https://code.claude.com/docs/en/agent-sdk/typescript), [Python SDK › RateLimitInfo](https://code.claude.com/docs/en/agent-sdk/python)) document `status`, `resetsAt`, `rateLimitType`, `utilization` and the overage fields. They **do not document `unifiedWindows`**. The binary's own description of it:
  > "Per-window usage for the session (5-hour), weekly (7-day), and overage-included weekly (per-model bucket …) … as read from the anthropic-ratelimit-unified-* response headers. … Unlike the top-level status/utilization fields, which describe the currently limiting window, both windows are tracked on every observation, and events are emitted when a window's rounded percentage or reset time moves, not only on status transitions. … always absent for API-key, Bedrock, and Vertex sessions."
- **When emitted.** After the first API response of a run (seen at 30% with `status: allowed`). After that, whenever a rounded % or a reset time changes. Not emitted for runs with no API call (e.g. `/usage`). Also see [#91857](https://github.com/anthropics/claude-code/issues/91857) (open), which reports the same as of 2.1.259 and asks Anthropic to document it.
- **Top-level `utilization`** is only filled when a window is the limiting one (warning/rejected). Read `unifiedWindows` instead.
- **Reliability.** Good. It comes straight from server headers on real requests, it is account-wide, and it updates live during a run.
- **Stability.** Medium-low for `unifiedWindows` (`@internal`, may change or vanish). High for `status`/`resetsAt`/`rateLimitType` (documented). Earlier versions emitted no percentages at all ([#78476](https://github.com/anthropics/claude-code/issues/78476), [#77018](https://github.com/anthropics/claude-code/issues/77018), 2.1.207–2.1.209).
- **Gotcha.** An old bug wrote this event in the middle of another NDJSON line ([#49640](https://github.com/anthropics/claude-code/issues/49640), v2.1.74, closed as stale). The parser should tolerate bad lines (skip and log), not crash.

### 2. `claude -p "/usage"` (recommended: before a run)

- [commands](https://code.claude.com/docs/en/commands): `/usage` (aliases `/cost`, `/stats`) shows "session cost, plan usage limits, and activity stats". The [TS SDK](https://code.claude.com/docs/en/agent-sdk/typescript) says a command sent as a prompt returns its output as an assistant message, and that is what happened in run 1.
- **What it reports:** session % and reset, week (all models) % and reset, and any per-model weekly rows if the account has them. It also lists local "behaviors" (it scans transcripts from the last 7 days; cheap with a dedicated worker config dir).
- **Reliability.** Good. It comes from the account-wide usage endpoint. The [costs doc](https://code.claude.com/docs/en/costs#using-the-usage-command) says that when the endpoint is rate-limited, `/usage` shows "last-known usage" with a `Showing last-known usage` note and the data's age. The governor must detect that note and treat the data as stale. Changelog 2.1.x: "non-interactive sessions on one machine now share a read made in the last minute", and it now backs off after rate-limit/reject. So **poll no more than about every 5 minutes**.
- **Stability.** Low-medium. It's human text: whole integers, local-time resets with no year ("Oct 8, 2am (America/Chicago)"). [#77018](https://github.com/anthropics/claude-code/issues/77018) says that on 2.1.207 `/usage` in `-p` showed *no* percentages, so the format has changed before. Parse with a regex, check the result, and fail closed (treat it as "no data").
- **Structured version exists but isn't reachable from plain `-p`.** The binary has an SDK control request `get_usage` (with `skip_behaviors`). It returns `rate_limits.{five_hour, seven_day, seven_day_opus, seven_day_sonnet, model_scoped[], extra_usage}`, each `{utilization 0–100, resets_at ISO-8601}`, and is described as "Experimental — the response shape may change". It needs `--input-format stream-json` and the control protocol, and it is undocumented. Not tested. It is a candidate spike if the text parsing proves fragile.

### 3. Statusline JSON

- [statusline › rate-limit usage](https://code.claude.com/docs/en/statusline#rate-limit-usage): `rate_limits.five_hour.used_percentage` (0–100), `.resets_at` (epoch s), and the same for `seven_day`. They are present only for Pro/Max and only after the first API response, each window may be missing, and a window is dropped once its `resets_at` passes. Phil's `~/.claude/statusline-command.sh` already shows these.
- **No per-model fields** in the statusline.
- **Use for the factory: none directly.** The statusline is the interactive TUI's bar, and nothing documents it running in `-p` (unverified). Each terminal's value is only as fresh as that terminal's last turn ([#75408](https://github.com/anthropics/claude-code/issues/75408)). Optional extra: Phil's interactive statusline script could *also* append readings to a file, giving the governor free samples while Phil works. That is a settings change, so it's Phil's call.

### 4. HTTP usage endpoint (document only, don't use)

- The binary calls `https://api.anthropic.com/api/oauth/usage` (variants `?at_wall=1&skip_spend=1`) with the subscription OAuth token. That is where `/usage` gets its data. Response windows: `five_hour`, `seven_day`, `seven_day_oauth_apps`, `seven_day_opus`, `seven_day_sonnet`, `model_scoped[]`, `extra_usage`.
- Response headers on every Messages call carry the raw data: `anthropic-ratelimit-unified-5h-utilization`, `-5h-reset`, `-7d-utilization`, `-7d-reset`, `-status`, `-representative-claim`, … The CLI turns these into `rate_limit_event`.
- **Risks.** Undocumented and can change without notice. It rate-limits (several changelog fixes are about backing off from it). Reading `~/.claude/.credentials.json` and using the token from our own code means using subscription credentials outside Claude Code. Third-party tools are told they "rightly cannot and should not" do this ([#78476](https://github.com/anthropics/claude-code/issues/78476)), and it's a likely ToS problem. It also clashes with the worker-safety rule of keeping credentials out of reach. **Not recommended.**

### 5. OpenRig

- [mvschwarz/openrig](https://github.com/mvschwarz/openrig) (Apache-2.0, ~4.9k stars, very active): a "network of agents from Claude Code, Codex and Pi". It boots each "seat" as an **interactive** CLI in its own tmux session. A Hono HTTP daemon with SQLite keeps state, and it has a CLI, TUI and MCP server ([pyshine overview](https://pyshine.com/OpenRig-Multi-Agent-Control-Plane-Deep-Dive/), [HN](https://hn.svelte.dev/item/47772935)).
- **Quota:** yes, sort of. `rig provider status` → `GET /api/provider/status`, and `rig usage top|series` → `/api/telemetry/usage/*` (usage samples, %/hour per window). The data comes from `packages/daemon/assets/claude-statusline-context.cjs`, a **statusline sidecar** that OpenRig installs into each Claude seat. It writes `rate_limits` to a per-seat cache file, which `claude-usage-reader.ts` reads. So it wraps source 3.
- **Fit:** poor for v1. It only sees seats running the interactive TUI, not `claude -p` children. It would add a daemon, tmux seats and its own state, and the chart says factory state must not live in the orchestrator. Phil's belief is **confirmed in spirit** (it has a quota API) but **it doesn't fit our worker model**. Its "percent per hour per window" design is worth copying.

### 6. Local transcripts and ccusage (fallback / history)

- Each assistant line in `~/.claude/projects/<proj>/<session>.jsonl` (or `$CLAUDE_CONFIG_DIR/projects/…` for workers) has `timestamp`, `message.model` and `message.usage{input_tokens, output_tokens, cache_creation_input_tokens, cache_read_input_tokens}`. The final `result` also has `usage`, `modelUsage` and `total_cost_usd`.
- **What it can't do:** tell you plan %, the limit, or when a reset happens. It only sees this machine's sessions, not Phil's phone, desktop or claude.ai use. Anthropic doesn't publish how tokens map to % (it varies by model, and cache reads are cheaper).
- [ccusage](https://ccusage.com/guide/blocks-reports) (not installed; `npx ccusage` would download it, so not run) reads the same files. `ccusage blocks` groups them into 5-hour blocks starting at the first message and projects tokens/min. `--token-limit max` compares against your largest previous block. `--json` is available. It is an estimate only, and its live monitor was removed in v18.
- **Use:** the factory already gets `usage`/`total_cost_usd` per run from the `result`. Store those. Transcripts only matter for back-filling.

### 7. What a limit hit looks like in `-p`

From [errors › usage limits](https://code.claude.com/docs/en/errors#youve-hit-your-session-limit), [interactive-mode › wait for reset](https://code.claude.com/docs/en/interactive-mode#wait-for-a-usage-limit-to-reset), the SDK types and the binary. **Not reproduced**, since that would mean burning the quota.
- Messages: `You've hit your session limit · resets 3:45pm`, `… weekly limit · resets Mon 12:00am`, `… Opus limit …`, `… Sonnet limit …`, plus "Fable limit" / "usage credit limit" in the binary. **Session and weekly are shared across models. Opus and Sonnet limits apply only to that family.**
- Expected stream: a `rate_limit_event` with `status: "rejected"`, `rateLimitType` and `resetsAt` (epoch). Then an assistant message with `error: "rate_limit"` (a 429 against quota, as opposed to `overloaded` 529). Then a `result` with `is_error: true`, the message text, probably `api_error_status: 429`, and a **non-zero exit**.
- **`-p` does not wait for the reset** ("Background sessions and `-p` runs: the menu row isn't available"). The interactive auto-continue doesn't apply. The governor must sleep until `resetsAt` and then `--resume`.
- Before the hard stop: `allowed_warning`. The CLI's own early-warning table (binary) is: 5h at ≥90% used while ≤72% of the window has passed; 7d at 75%/60%, 50%/35% and 25%/15%. The UI text is "You've used 85% of your session limit · resets 3:45pm".
- Transient server 429s that aren't your quota show up as `system/api_retry` with `error: "rate_limit"` and are retried automatically ([headless](https://code.claude.com/docs/en/headless)). **Don't treat `api_retry` as a quota signal.** Only `rate_limit_event.status == "rejected"` counts.
- Don't rely on the exit code alone: run 2 exited 1 for `error_max_turns`.

## Recommended governor design (v1)

**Signals, in priority order**
1. `rate_limit_event.unifiedWindows` from the running worker's stream: live, free, account-wide.
2. `claude -p "/usage"` (worker `CLAUDE_CONFIG_DIR`, `--output-format json`, 60 s timeout). Run it before each task start, and while idle at most every 15 min (≤ every 5 min near a decision). Parse `Current session: N% used · resets …`, `Current week (all models): N% …`, and any `Current week (<model>): …` rows. Reject the reading if it has the `last-known` note or doesn't parse.
3. The last stored reading, with its age. It is never trusted past its `resetsAt`.

**State** (durable, `/mnt/data/factory/state/quota.jsonl`): append `{ts, source, u5, r5, u7, r7, perModel{}, stale}` for every reading. It is cheap and gives the trend.

**Per-task cost model**
- For each run, record `u5`/`u7` at start (from /usage) and the last `rate_limit_event` at the end. Also record `usage`, `total_cost_usd`, model and task kind (code/review/design).
- Δ5 and Δ7 per run are the cost in plan points. If a window reset during the run, record cost as "unknown" for that window.
- Estimate for the next task = **p80 of the last N runs of the same kind**. Until there are ~5 samples, use a conservative default (e.g. Sonnet code task = 25 points of 5h, review = 10; Phil to tune).
- Note: Phil's own use runs in parallel and shows up in the deltas. That is fine for a conservative estimate but noisy. Optionally flag runs where the delta is far above the token count (Phil was busy).

**Start rule** (floors from task-guide's process: 20% of 5h left, 5% weekly left)
- Start only if `u5 + est5 ≤ 80` **and** `u7 + est7 ≤ 95`, and any per-model weekly bucket for the chosen model has room.
- If the 5h check fails but the reset is soon: if `r5 − now < est_duration`, the task would straddle the reset, so allow it if `est5 ≤ 80` (the window resets mid-run). Simpler v1: **just wait for `r5`**.
- Interactive reserve for Phil: add an extra margin, e.g. stop at 70% instead of 80%. That is a Phil decision (chart "not yet specified").

**During a run**
- Stream parser: on each `rate_limit_event`, if `unifiedWindows.five_hour.utilization ≥ 0.80` or `seven_day ≥ 0.95`, send **SIGINT** (clean turn end). Record `paused_for_quota` with the session id, and `--resume` after the reset.
- `status: "rejected"`: stop, store `resetsAt` + `rateLimitType`, mark the issue "waiting for quota" (not failed).

**Reset handling**
- Sleep until `max(resetsAt of the blocking window) + 2–5 min jitter`, then take a fresh `/usage` reading before starting. Never assume a reset happened; confirm it.
- A 5h window only exists after first use. If `/usage` shows no session line, or `five_hour` is absent, treat the 5h window as 0% used.
- Weekly reset can be days away. Surface `needs-phil`/status only if useful. Otherwise just stay idle.

**Fallback when there's no data** (parse failure, endpoint rate-limited, field missing after an upgrade)
- Fail closed for *starting* work: don't start a new task without a reading less than 15 min old. Retry with backoff (5, 10, 20 min).
- After 1 h of no data, post one `needs-phil` item ("quota reading broken on vX.Y.Z") instead of guessing.
- Optional degraded mode (Phil's call): allow at most one small task per 5 h, using `ccusage`-style token estimates from local transcripts.
- Pin `DISABLE_AUTOUPDATER=1` (already recommended). After any upgrade, run one canary check: `/usage` parses and one tiny run has `unifiedWindows`.

## Open questions for Phil

1. **Interactive reserve:** should the factory stop earlier than task-guide's 20% / 5% floors to leave room for you, e.g. 5h at 70% used and weekly at 85%? *Recommend: yes, 5h at 70% used and weekly at 90% used for v1. Loosen it once trends are visible.*
2. **Mid-run stop:** when a running task crosses the floor, should the factory interrupt it (SIGINT, then `--resume` after reset), or let it finish? *Recommend: interrupt at the floor, resume later. Finishing could eat the reserve.*
3. **No-data behaviour:** if quota can't be read, should the factory stop starting work (fail closed) or allow one small task per 5 h? *Recommend: fail closed. Post `needs-phil` after 1 h.*
4. **Undocumented fields:** OK to depend on `unifiedWindows` (internal) and on parsing `/usage` text, with a canary check after upgrades? Or spike the experimental `get_usage` control request first? *Recommend: use both now with the canary. Spike `get_usage` only if parsing breaks.*
5. **Statusline sampling:** should your interactive statusline script also append `rate_limits` to `/mnt/data/factory/state/` so the governor sees your burn between its own readings? *Recommend: not needed for v1. `/usage` already covers your use.*
6. **OpenRig:** given that its quota API only covers interactive tmux seats, drop it from the quota question? *Recommend: yes. Keep it only as design inspiration (per-window %/hour trend).*
7. **Starting cost guesses** per task kind until there's history: Sonnet code task 25 points of 5h, Opus review 10. Do these match your feel? *Recommend: start there and let the p80 of real runs take over after 5 runs.*
