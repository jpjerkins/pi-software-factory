# Build plan: #5 dispatcher skeleton (`factory run-once`)

The durable copy of the step plan for issue #5. Until 2026-10-05 it lived only in session handoff notes. Update the status marks as work lands.

Spec: `gh issue view 5`. Decisions: chart #1 (its "no shim" line supersedes the #5 text).

## Steps 1–4: done

1. Solution scaffold.
2. `EligibilityPolicy` and `IssueOrdering`, with the task-guide fixture.
3. `RunOnce` against fakes; `OutcomeDetection`; `ReportedStatus` (4 values) vs `WorkerOutcome` (6).
4. Adapters: `RunStore`, `ClaudeTrust`, `GitWorktrees`, `GitHubIssues`, `UsageProbe`, `HerdrSlots`, plus `WorkerSettingsFile` and `WorkerPromptFile`.

## Step 5: hooks, CLI, publish, end-to-end

1. ✅ `factory hook <event>`: `ToolGuard` in Domain (`src/Factory.Domain/Guard/`), hook commands in `src/Factory.Cli/Hooks/`. Contracts: [research/hook-contracts.md](research/hook-contracts.md).
2. ✅ `factory run-once`: the composition root.
   - `SystemClock`, the real `IClock`.
   - `NextIssue` picks the next issue without claiming it; `RunOnce` uses it.
   - Options: `--repo-root` (default `~/dev/factory/task-guide`), `--runs-root` (`/mnt/data/factory/runs`), `--poll-seconds` (10), `--stuck-minutes` (30).
   - `--dry-run` prints the issue and branch it would claim, with no writes. (Phil: yes.)
   - `RunOnceWiring` and `RunOnceCommand`. Hooks call `Environment.ProcessPath`. Exit codes: 0 for an outcome or no work, 1 for an error, 2 for usage, 130 for Ctrl-C.
   - `CommandRouter` with routing tests.
   - No guard against running from `~/dev`. (Phil: no.)
   - Accepted gap: if a step fails partway, the claim and run dir stay, no `run.json` is written, and the error goes to stderr only.
   - Live `--dry-run` checked 2026-10-06: picked #103 on `web-now/103-wn3-quick-capture-right`, exit 0, no writes.
3. ✅ Publish: `scripts/publish.sh` puts a framework-dependent `linux-arm64` build in `~/apps/factory` (Phil chose this over `/opt` and `/mnt/data`). It finds the runtime via `DOTNET_ROOT` from Phil's profile, and `HerdrSlots` passes `DOTNET_ROOT` into the worker env so hooks find it too. It's a host process rather than a DCM container because it drives HERDR and `claude` on the host (chart #1). Smoke-checked 2026-10-06: usage exits 2; `--dry-run` picks #103.
   - ✅ Phil ran: `sudo mkdir -p /mnt/data/factory/runs && sudo chown -R philj:philj /mnt/data/factory` (`/mnt/data` belongs to root).
4. ⏳ A manual end-to-end run on a real issue, watched in HERDR. This is the #5 acceptance.
   - First live run (2026-10-06, run 20261006-1128-i103) failed: the worker stalled at Claude's folder-trust dialog. A trust entry on the repo root does not cover worktrees beneath it. Fix: trust each worktree path. Phil reset #103 and the worktree was removed; chart #1's 'one entry covers all worktrees' line needs correcting (GitHub write, Phil).
   - Second live run (2026-10-06, run 20261006-1213-i103) reached `plan_ready` in 53 s and the dispatcher exited 0. `run.json` was written with usage before and after. A worker `gh` call was blocked and logged in `denials.log`. `git push` and `herdr` blocks weren't exercised live.
   - Fixed: the factory writes the issue title and body into `prompt.md`.
   - Third live run (2026-10-06, run 20261006-1226-i103): `plan_ready` in 43 s, exit 0, no denials. `prompt.md` carried the full issue, and the plan matched the ticket (lane files, out-of-lane report, test-first, stops before §8). The plan was one dense paragraph rather than "fits a phone screen". The worker is parked in HERDR tab `w2:t5`, waiting for confirmation (resuming it is #7).
   - Remaining #5 acceptance gap: the `git push` and `herdr` blocks haven't been exercised live (unit tests only).
   - Minor: Claude added an untrusted `projects` entry for `<repo>/main` in `~/.claude.json` (cause unknown; harmless).

## Known risks carried forward

- `HerdrSlots`: the `tab create` and `workspace create` result shapes and `agent_not_ready` come from `herdr api schema`, not from a live run.
- Claiming takes two `gh` calls (label and assign, then comment), so a partial failure leaves no comment.
- `AddBlockerAsync` hasn't been tested live.
- `/usage` stale-data note not detected (deferred to #6).
- Guard gaps: `touch`, `dd`, `ln`, `python -c` writes; `eval` and heredocs aren't unwrapped.
- `GitWorktrees` doesn't check for remote-only branch name collisions.
- Spike leftovers to clean up only with Phil's OK: a trust entry for `/tmp/claude-1000/spike-settings/repo` in `~/.claude.json`, and that scratch folder.
