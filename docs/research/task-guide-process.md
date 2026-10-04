# task-guide: how an issue is picked, worked and finished

Research for [#3](https://github.com/jpjerkins/pi-software-factory/issues/3), chart [#1](https://github.com/jpjerkins/pi-software-factory/issues/1).
Read-only, 2026-10-04 (final pass). Read from the working tree at `~/dev/task-guide`, `main` @ `3fba410`
(clean, equal to `origin/main`). Live issue data from `gh`, same day. Paths below are relative to task-guide.

## Sources

| What | Where |
|---|---|
| The process (source of truth) | `.claude/skills/start-lane/SKILL.md` (§1–§9). Codex copy is hand-mirrored to `~/.codex/skills/start-lane/` (`CLAUDE.md`) |
| Rules the skill enforces | `docs/superpowers/plans/2026-09-03-application-layer.md`: Global constraints 1–12, Lanes, Merge safety, Review gate, Definition of done, Departures |
| Roles, authority, budget floors | `docs/coordination.md` (banner, § Authority and roles, § Capacity); banner of `docs/agent-status.md` |
| Repo agent rules | `CLAUDE.md` (Sonnet subagents code, Opus subagents review, always a worktree, `npm run build` is the web build gate), `AGENTS.md` (same two coding rules) |
| Tracker conventions | `docs/agents/issue-tracker.md` (claim = assign; native GitHub blockers; `blocked_by` counts open blockers only) |
| Scripts | `scripts/check-schema-drift.sh`, `scripts/check-css-union.mjs` (`npm run check:css`). `scripts/frontier.sh` is for wayfinder maps, **not** build tickets (SKILL §1), but its ranking ("by how many tickets it unblocks") matches Phil's ordering rule |

## 1. How the next issue is chosen

**Takeable** (SKILL §1) = open, label `build`, **no assignee**, `issue_dependencies_summary.blocked_by == 0`.
A named issue is still checked; a blocked one is refused and the blocker named. The skill then says
"present the result and let the user choose" — it defines **no ordering**.

- **`agent:*`** decides who implements (coordination.md § Authority; agent-status.md banner). Check it live
  before every claim, resume, commit or close. **SKILL §1's query does not filter on it** — the dispatcher must
  add `agent:claude`. v1 skips `agent:codex` (chart #1).
- **No `agent:` label**: sources disagree. coordination.md: "ask Claude with Opus to decide… do not ask Phil".
  agent-status.md banner (same date, 2026-09-15): "an unlabelled ticket waits for Phil". Today: **#181**.
- **`lane:*`** sets the branch prefix and file-ownership lane (SKILL §3, plan § Lanes). No role in ordering.
  `lane:validation` is in SKILL §3's list but not in the plan's Lanes table.
- **Claim** = `gh issue edit <n> --add-assignee @me`, the first write (SKILL §2). On the Pi `@me` is
  `jpjerkins`, the same account as Phil's own sessions, so a factory claim is indistinguishable from Phil's,
  and a stale claim never expires. **#146 and #88 are assigned today**; unknown whether anything is working them.
- **Concurrency**: one implementation worker per provider, across machines (coordination.md § Capacity).

**Phil's decided ordering (chart #1):** most-unblocking first, else oldest. Proposed deterministic form:
sort by count of **open issues this one directly blocks** (`GET issues/<n>/dependencies/blocking`, filter
`state==open`), descending; tie → `createdAt` ascending, then number. (Unverified whether
`issue_dependencies_summary.blocking` already excludes closed dependents, so use the endpoint.)

Live Claude frontier, 2026-10-04, in that order:

| Order | Issue | Directly blocks (open) | Created |
|---|---|---|---|
| 1 | #103 WN3 (`lane:web-now`) | #109 | 2026-09-03 12:55:06 |
| 2 | #106 WA2 (`lane:web-authoring`) | #109 | 2026-09-03 12:55:10 |
| 3 | #105 WA1 (`lane:web-authoring`) | — | 2026-09-03 |
| 4 | #182 (`lane:web-authoring`) | — | 2026-09-22 |

Not takeable: #146, #88 (assigned); #109, #110 (2 open blockers); #49, #50 (1 each); #181 (no `agent:`);
#65 (no `build`). Note #109 needs **both** #103 and #106, so "blocks most" ≠ "unblocks most" — the direct
count is simpler and matches `frontier.sh`.

## 2. What a worker does, step by step

`disable-model-invocation: true` — the skill runs only on explicit `/start-lane [n]`. Its premise: decide
nothing the plan settled; a ticket that looks wrong is reported, not redesigned (SKILL preamble).

| § | Step (SKILL.md) | Headless? |
|---|---|---|
| 0 | Read plan's Global constraints, Lanes, Merge safety, Review gate before §4 | Yes |
| 1 | Pick (named issue or frontier; refuse if blocked) | **Factory** (deterministic) |
| 2 | Claim: assign self | **Factory** (needs issues:write) |
| 3 | `fetch`; `worktree add <main>-<slug> -b <lane>/<slug> origin/main`; check `git worktree list`, never reuse/remove/prune another's; `npm install` for web lanes | **Factory** (cwd = worktree also fixes §7's review-target hazard) |
| 4 | Read only what the ticket names: ticket, Global constraints, cited ADRs (`docs/adr/README.md` first), `CONTEXT.md` by line range via `CONTEXT-INDEX.md` | Yes (factory supplies ticket text if worker has no `gh`) |
| 4a | Claude only: brief a coding subagent with findings, re-sendable verbatim, "commit each section as it lands"; parallel only on disjoint files; review its diff yourself | Yes, needs Task tool + a Sonnet coding agent |
| 5 | TDD red first (assertion failure); quote verbatim red/green; mutation drill on pure-rule tickets; test names from `tests/TEST-INVENTORY.md` | Yes |
| 6 | Stay in the `Owns:` lane (+ Merge safety extension); other lane's file, any `.csproj`/`.slnx`, `Program.cs` DI → **stop and report** | Yes, ends as `blocked` |
| 7 | `dotnet test`; web: `npm test` (+ `npm run build` per `CLAUDE.md`); `check:css` if `index.css`/guard prototypes touched; API surface → regenerate `schema.d.ts` with API on 8007 + `check-schema-drift.sh`; record Phil's in-session decisions in a ticket comment before review; confirm review target; `/code-review high <lane>/<slug>` | Mostly; see §5 blockers |
| 7a | Review gate: every finding needs provenance found in order open issues → ADR README → plan Departures/Global constraints + TEST-INVENTORY → code | Needs read access to open issues |
| 8 | Rebase, drift re-check, ff-merge, push `main`, close, cleanup | **Factory** (see §3) |
| 9 | Report: other lane's file, ticket looks wrong, anything out of scope; flag diffs touching `Application/Ports/`, `Api/Program.cs`, `TaskGuide.TestSupport` | Yes, via exit contract |

Gotcha from `docs/coordination.md` (Final pilot validation): `dotnet test --no-restore` silently ran only the
API project. Use `dotnet test task-guide.slnx`. The Playwright E2E suite (`tests/TaskGuide.E2E`) is not in
the `.slnx`, so `dotnet test` needs no browser.

## 3. How an issue finishes vs Phil's merge decision

**SKILL §8 as written:** "Do not open a PR, and do not stop here to wait for Phil." In the worktree
`git fetch && git rebase origin/main` → if API surface, re-run drift check (regenerate + amend on fail) →
`git -C <main> merge --ff-only` + `push origin main` (non-ff → rebase again, never `--no-ff`/`-s ours`/force) →
`gh issue close <n> --comment "<what ran + green output>"` → `worktree remove` + `branch -d` (never `-f`/`-D`) →
`pull --ff-only` in the main clone. §9 calls all of this standing permission; coordination.md's
"DIRECT INTEGRATION POLICY" (2026-09-14) says "Do not use PRs".

**Phil's decision (chart #1):** factory runs §8 only after a **separate review-agent session** confirms
(a) new tests written + passing, (b) existing tests pass, (c) architecture consistent, (d) new problems
logged as GitHub issues. Changes touching CI, deps, config or `.claude/` go to a PR.

| Point | Fit / conflict |
|---|---|
| §8 mechanics | **Fits.** Factory does §8 itself; no main clone needed: rebase in the worktree, re-run tests (+drift if API), `git push origin HEAD:main` (rejected if not ff = same safety as `merge --ff-only`), close with evidence, `worktree remove`, `branch -d`. Merge lock: one worker at a time on the Pi, but Phil's Mac can push too, so retry-on-reject stays. |
| §7/§7a review | **Different layer.** §7's `/code-review` is a forked agent *inside* the worker session — not separate. Phil's review session is an extra gate after the worker exits. §7a's provenance gate should bind the review session too. |
| Cross-provider review | **Conflict.** coordination.md: "the agent that did not implement a ticket reviews it… Never self-review". A Claude review session on Claude work is a departure (v1 is Claude-only). |
| PR route | **Conflict with "Do not use PRs"**, but only for the excepted class; Phil's newer decision wins for the factory. Repo has no `.github/` and no CI today, so "CI" is currently empty. `.csproj`/`.slnx` are integration-lane files a non-integration worker must not touch (§6) — the PR rule is a backstop. |
| Ports / `Program.cs` / TestSupport | Plan § Review gate requires "a Claude integration-lane review before merge"; SKILL §9 only asks to say so. Phil's decision is silent. |
| Plan § Merge safety / Review gate say "PR into `main`" | Stale vs SKILL §8 and coordination.md. Not a factory problem; worth an upstream fix. |
| "Lane done" | Plan § Definition of done: ~10-min guided smoke session with Phil when a lane's last ticket merges. Not part of §8. |

**What the review session needs**
- *Inputs (factory-supplied):* ticket body + comments; branch, base and head SHAs; the worker's exit
  contract (red/green output, mutation named, files touched, decisions, problems found); a pre-fetched list
  of open issue numbers/titles (for §7a step 1 and to de-duplicate new problems).
- *Read in repo:* plan Global constraints + Merge safety + Departures, `docs/adr/README.md`,
  `tests/TEST-INVENTORY.md`, the ticket's `Owns:` block, the 7a gate text (restated in its prompt).
- *Runs itself:* `dotnet test task-guide.slnx`; web lanes `npm ci && npm test && npm run build`;
  `check:css` and `check-schema-drift.sh` when triggered. No Edit/Write — it reports only.
- *Checks:* (a) new tests exist, named from the inventory (or appended), red-first evidence present;
  (b) whole suite green; (c) only `Owns:` files touched, Global constraints 6–11, ADRs, Ports/Program.cs/TestSupport flagged;
  (d) every out-of-scope or other-lane problem is listed.
- *Output contract:* per-criterion pass/fail + evidence; findings with provenance lines; `new_issues[]`
  (title, body); verdict `merge | fix | escalate`. The factory creates the issues (reviewer holds no credentials),
  then on `fix` resumes the worker with the findings, on `escalate` raises an attention item.

## 4. Steps needing Phil vs the factory

| Step | Source | Who |
|---|---|---|
| Choose ticket (ordering rule decided) | SKILL §1, chart #1 | Factory |
| Claim (+ a factory claim marker) | SKILL §2 | Factory |
| Fetch, worktree, branch, `npm install` | SKILL §3 | Factory |
| §4–7a implementation, local commits | SKILL | Worker (no credentials) |
| Post worker questions; write Phil's answer as a ticket comment before review; resume | SKILL §7, safety note §6 | Factory posts; **Phil answers** |
| Separate review session; create `new_issues[]` | chart #1 | Factory runs it (LLM session), factory writes issues |
| Rebase, re-test, drift re-check, push `main`, close with evidence | SKILL §8 | Factory |
| Push branch + open PR for CI/deps/config/`.claude/` diffs | chart #1 | Factory opens; **Phil merges** |
| `worktree remove` / `branch -d` in the factory's own clone | SKILL §8 | Factory (Phil to confirm, see Q9) |
| Other lane's file, `.csproj`/`.slnx`, `Program.cs` DI, new `index.css` class | SKILL §6, §7, §9 | **Phil / integration lane** (attention item; keep claim + worktree) |
| Ticket looks wrong; anything out of scope | SKILL §9 | **Phil** (attention item or new issue) |
| Unlabelled ticket (#181) | coordination.md vs agent-status.md | **Phil** in v1 (skip + one attention item) |
| Stale claims (#146, #88) | live GitHub | **Phil** (factory lists, never takes over) |
| Lane-done smoke session | plan § Definition of done | **Phil only** |
| Budget floors: stop at 20% of 5-hour / 5% weekly remaining; unknown limits ≠ spare | coordination.md § Capacity | Factory (governor) |
| Deployment, secrets, deletion outside own clone, spending, reset credits, permission changes | coordination.md banner + § Authority | **Phil only** |

## 5. Headless blockers and fixes

| Blocker | Evidence | Fix |
|---|---|---|
| Hard-coded `/Users/phil/dev/task-guide` (10× in SKILL.md; also in coordination/agent-status history) | `grep -c` on SKILL.md | Factory owns §1–3 and §8, so the worker barely needs it. Prompt says "the main clone is `<factory clone>`; ignore `/Users/phil` paths". Upstream: derive with `git rev-parse --path-format=absolute --git-common-dir`. |
| `AskUserQuestion` / "let the user choose" / "report it" | SKILL §1, §6, §7, §9 | Pass the issue number. `--permission-prompts none` removes `AskUserQuestion`; worker ends with `needs_input` / `blocked` in the `--json-schema` exit contract (safety note §6). |
| Pi `~/.claude/CLAUDE.md`: "show a plan BEFORE acting. Pause…"; "NEVER delete things, **stop processes**…" | loaded for every session as `philj` | Dedicated `CLAUDE_CONFIG_DIR` (safety note §1) with its own neutral CLAUDE.md; prompt states the standing authority (commit, stop its own `dotnet run`). **Unverified** that the dedicated dir drops the user file — test once. Deletion (§8 cleanup) moves to the factory anyway. |
| Skill invocation | `disable-model-invocation: true` | **Unverified** that `-p "/start-lane 103"` expands it. Since the factory owns §1–3/§8 anyway, prefer a factory prompt: "follow SKILL.md §4–7a for #N; stop before §8; return the exit contract". |
| Permissions | `-p` ignores project allow rules; repo's `.claude/settings.local.json` is gitignored and only allows `gh issue *` | `dontAsk` + explicit allow: git `add/commit/diff/log/status/rebase` (in worktree), `dotnet build/test/run`, npm `ci/install/test/run build/run gen:api/run check:css`, `./scripts/check-schema-drift.sh`, Task, Skill, scoped `kill`. |
| Background `dotnet run` for `gen:api` | SKILL §7 ("one shell… another shell") | Background Bash job, then kill it before the drift check. Better upstream: a `scripts/regen-schema.sh` that boots, generates and kills with a trap, like `check-schema-drift.sh` does. |
| `lsof` missing on the Pi | `command -v lsof` empty; drift script's `$(lsof -ti:$PORT \|\| true)` then always passes | Install `lsof` (Phil), or the dispatcher checks `ss -ltn 'sport = :8007'` before each run and guarantees one worker. |
| Drift script waits 60 s for the API | `check-schema-drift.sh` | A cold `dotnet run` build on the Pi may exceed that. Run `dotnet build` first. Unverified. |
| ARM64 | aarch64, dotnet 10.0.102 (`~/bin`), node 22.22.0 | Not yet run green here. Before the first dispatch the factory runs a baseline on `main` (`dotnet test task-guide.slnx`, `npm ci && npm test && npm run build`) and records timings for timeouts. |
| `/code-review` is ambiguous | SKILL §7 says `/code-review high <branch>` (matches the built-in effort-level skill); plan § Review gate says "Standards + Spec" (matches `mattpocock-skills:code-review`) | Pick one explicitly; under a dedicated config dir only built-ins exist unless plugins are installed there. |
| Coding subagent | `CLAUDE.md`: "Sonnet subagents"; SKILL §4a says "`coder` subagent", which is a user-level agent (`~/.claude/agents/coder.md`, pi5/container-flavoured) | Provide a task-guide-neutral Sonnet coding agent in the worker config dir, or use general-purpose with `model: sonnet`. |
| Same GitHub identity as Phil | §1 above | `factory:running` label + claim comment with run id (or a bot account later). |
| Plan mode | — | Never start workers in plan mode. |

The old `.claude/worktrees/*` entries in `~/dev/task-guide` (`prunable`, from earlier research) are
metadata only. Leave them; the factory uses its own clone.

## 6. Open questions for Phil (each with a recommendation)

1. **Claude reviewing Claude** breaks coordination.md's cross-provider review rule. *Recommend:* accept for v1
   as a declared departure; the factory notes it in each close comment. Revisit when Codex joins.
2. **Keep the worker's own §7 `/code-review`** as well as the separate review session? *Recommend:* keep it in
   v1 (it is the repo's process), measure cost, drop it if the review session catches the same things.
3. **Ports / `Program.cs` / TestSupport diffs** (plan wants an integration-lane review). *Recommend:* route them
   to a PR like config changes. They should be rare.
4. **Exact PR-route path list.** *Recommend:* `.github/**`, `.claude/**`, `.mcp.json`, `CLAUDE.md`, `AGENTS.md`,
   `**/*.csproj`, `task-guide.slnx`, `Directory.Build.props`, `**/package.json`, `**/package-lock.json`,
   `Dockerfile`, `.dockerignore`, `.gitignore`, `**/appsettings*.json`, `**/tsconfig*.json`, `**/vite.config.*`.
5. **Which `/code-review`** (built-in effort-level vs mattpocock Standards+Spec)? *Recommend:* built-in, since
   the skill's `high <branch>` syntax is written for it and it needs no plugin in the worker config dir.
6. **Claim marker.** *Recommend:* `factory:running` label + claim comment with run id; the factory never
   touches issues assigned without it (#146, #88) and lists them for Phil.
7. **Unlabelled tickets (#181).** *Recommend:* skip and raise one attention item (agent-status banner rule),
   not ask Opus — keeps model spend out of selection.
8. **Labels on factory-created "new problem" issues.** *Recommend:* no `build`/`agent:` labels, body starts
   "Found by the factory while working #N", so they wait for Phil's triage and never auto-enter the queue.
9. **Cleanup in the factory's own clone** (`worktree remove`, `branch -d`, no `-f`/`-D`) as standing
   permission, despite the global "never delete" rule? *Recommend:* yes, factory clone only.
10. **Review-fail loop.** *Recommend:* at most 2 worker fix rounds, then an attention item; ticket stays
    claimed, worktree kept.
11. **Install `lsof`?** *Recommend:* yes (one `apt install`); keep the `ss` pre-check anyway.
12. **Upstream fixes to task-guide** (path-agnostic SKILL.md, `agent:` filter in §1, name the review skill,
    regen script, drop "PR" from the plan). *Recommend:* one ticket later; v1 works around them in the prompt.

## 7. Consistency with `headless-worker-safety.md`

Conflicts (that note was written before Phil's merge decision; not edited here):

| Safety note says | This note / chart #1 | Resolution |
|---|---|---|
| Factory pushes only `factory/*`, opens draft PRs; "Needs Phil: merge any PR"; "Never (nobody): push to `main`" | Factory pushes `main` after the review gate; PRs only for CI/deps/config/`.claude/` | Chart #1 supersedes. Update that note's split and question 1. |
| Branch `factory/issue-<N>` | SKILL §3: `<lane>/<slug>` | Use `<lane>/<slug>` (or `factory/<lane>/<slug>` if a marker is wanted); it is never pushed in the routine case. |
| `Bash(gh *)` denied; worker has no `gh` | §7a step 1 runs `gh issue list --search` | Factory pre-fetches open issues into the prompt (or read-only PAT). Compatible. |
| Allow list sketch lacks npm, `dotnet run`, scripts, Task, `kill` | §5 above | Extend the allow list. |
| `Bash(git switch *)` allowed | SKILL §3 forbids `git switch` in the main clone | Harmless in a worktree; drop it anyway. |
| Dedicated config dir "holds… the `start-lane` skill" | Skill is project-scoped and travels with the worktree (plan § Working a ticket) | No copy needed. |
| "Factory removes its own worktrees after the PR is merged" | After the factory's own push to `main` | Same idea, different trigger. |
| Factory verifies "tests re-run green" before push | Review session runs them; factory re-runs after rebase | Compatible; both. |

Consistent: no worker credentials, factory-owned clone and worktrees, `dontAsk` + hook, exit contract,
`--permission-prompts none`, dedicated `CLAUDE_CONFIG_DIR`, `Edit(.claude/**)` deny (matches the PR rule).
