# Running `claude -p` unattended on pi5 safely

Research for issue #4 (chart #1). Checked against Claude Code **2.1.289** on pi5, 2026-10-04.
Sources are the official docs at code.claude.com (linked inline), `claude --help`, and one small test run.

## TL;DR

- Use **`--permission-mode dontAsk`** plus an explicit allow list. Anything not on the list is denied right away and the run keeps going. This is the deterministic option: no LLM decides. Don't use `auto`, which is an LLM classifier. Don't use `bypassPermissions`, which the docs say is for containers/VMs only.
- Add a **factory-owned `PreToolUse` hook** as the real guard. Bash allow/deny rules only match command *text*, so they can be bypassed (`git -C . push` gets past `Bash(git push *)`).
- **Give the worker no GitHub credentials at all.** Today `gh` holds a `repo`+`workflow` OAuth token, and git uses `gh` as its credential helper. Any process running as `philj` can push, open PRs, merge and edit CI. The factory reads issues, pushes, opens PRs and posts comments. The worker only edits files and commits locally.
- **One worktree per issue, in a factory-owned clone** under `/mnt/data/factory`, never in `~/dev`.
- **Exit contract via `--json-schema`.** In `-p` mode the worker can't ask a question interactively, so it must *end* with `status: needs_input` and a question. The factory posts the question to GitHub, then `--resume`s the session with Phil's answer.
- Wrap each run in `timeout -s INT` with `--max-turns` and `--max-budget-usd`. Log `stream-json` to a file.

## 1. Permissions

### Modes (from [permission-modes](https://code.claude.com/docs/en/permission-modes))

| Mode | Verdict for workers |
|---|---|
| `default` (Manual) | This is what `-p` starts in by default. Anything that would prompt is denied because nobody can answer. It works, but the behaviour is implicit. |
| `acceptEdits` | Auto-approves file edits plus `mkdir/touch/mv/cp`. Other shell commands still need allow rules. |
| **`dontAsk`** | **Recommended.** "auto-denies every tool call that would otherwise prompt". Read-only commands, `permissions.allow` / `--allowedTools` matches and hook-approved calls still run. "Use this mode for CI pipelines… the session never waits for input." Writes to protected paths (`.git`, `.claude`, `.bashrc`, …) are denied. |
| `auto` | An LLM classifier reviews each action. It is good, and blocks force-push, merging unapproved PRs and `curl \| bash`. But it is non-deterministic and the docs say it "does not guarantee safety". It also conflicts with "software enforces". Possible later as an extra layer, not as the gate. |
| `plan` | Don't use. `ExitPlanMode` needs a permission host, so a `-p` run can't leave plan mode. |
| `bypassPermissions` / `--dangerously-skip-permissions` | **No.** The docs say "Isolated containers and VMs only". Allow rules have no effect in this mode. Protected-path writes run, and unknown network hosts are allowed. On pi5 the worker would have Phil's full user: gh token, docker group, `~/dev`, `/mnt/data`. |

Rules that hold in every mode: deny rules always win. `rm -rf` on critical paths (`/`, `~`) is never auto-approved. `AskUserQuestion` is never auto-approved.

Also pass **`--permission-prompts none`** (v2.1.259+). It tells Claude "nobody can approve, don't retry", and it removes `AskUserQuestion` from the tool list ([headless](https://code.claude.com/docs/en/headless#turn-off-permission-prompts-in-unattended-runs)). Denials appear in the result's `permission_denials` array. The factory should log these, because they show where the allow list is too tight.

### Rules alone are not a boundary

From [permissions › bash-rule-limits](https://code.claude.com/docs/en/permissions#bash-rule-limits): "a deny or ask rule covers the invocation Claude usually produces and isn't a security boundary around the program." `Bash(git push *)` does not stop `git -C . push`, `git 'push'`, `/usr/bin/git push` or `sh -c '…'`. Compound commands *are* split, so `a && b` must match each part separately.

So use three layers:

1. **Allow list** (narrow). This decides what the worker can do without friction.
2. **Factory `PreToolUse` hook** (deterministic code). It sees the full `tool_input` JSON. It can block with exit code 2, and "A hook that exits with code 2 stops the tool call before permission rules are evaluated" ([permissions › hooks](https://code.claude.com/docs/en/permissions#extend-permissions-with-hooks), [hooks](https://code.claude.com/docs/en/hooks)). It also writes an audit log line per tool call.
3. **No credentials in reach** (section 2). This means a guard that misses something still can't push.

One caveat: a plugin "mod" that handles `tool.check` can override hook blocks and deny rules. Load only the plugins the worker needs (see the dedicated config dir below).

### Sandbox (optional, later)

The built-in Bash sandbox ([sandboxing](https://code.claude.com/docs/en/sandboxing)) uses bubblewrap to confine writes to the working dir and route network through a domain allow list. It only covers Bash. Read/Edit/WebFetch, hooks and MCP servers run outside it. On pi5 `bwrap` is installed but **`socat` is missing**, so turning it on needs `apt install socat`, which is Phil's call. If enabled, use `sandbox.failIfUnavailable: true` and `allowUnsandboxedCommands: false`. Without those, it silently runs unsandboxed or lets Claude retry outside. Nice to have; not needed for v1 if section 2 is done.

### Settings sources in `-p`

From [permissions › what runs before you trust a folder](https://code.claude.com/docs/en/permissions#what-runs-before-you-trust-a-folder): in `-p`, the repo's `.claude/settings.json` **allow rules are ignored** (untrusted). But the repo's **hooks, `env` block and `.mcp.json` servers are used**. Today task-guide tracks none of these (only `CLAUDE.md`). Still, pass `--strict-mcp-config` and have the factory check the worktree for new `.claude/` or `.mcp.json` files before each run.

`--bare` would be the cleanest isolation, but it only authenticates with `ANTHROPIC_API_KEY` / `apiKeyHelper`. It does not use subscription OAuth, so it's not usable on Phil's plan quota. Use a **dedicated `CLAUDE_CONFIG_DIR`** for workers instead, e.g. `/mnt/data/factory/claude-home`. It holds only the worker's settings, the `start-lane` skill and the needed plugins. Workers then don't inherit Phil's interactive allow rules, hooks or plugins, and Phil's sessions don't see worker transcripts. Authenticate it with `CLAUDE_CODE_OAUTH_TOKEN` from `claude setup-token` ([env-vars](https://code.claude.com/docs/en/env-vars)). That needs Phil to run it once.

## 2. Credentials

Current state on pi5:
- `gh` is logged in as `jpjerkins`. Scopes are `repo, workflow, gist, read:org`. The token is in plaintext in `~/.config/gh/hosts.yml` (mode 0600, readable by any `philj` process).
- git global config has `credential.https://github.com.helper = !gh auth git-credential`, so every `git push` as `philj` is authenticated.
- `CLAUDE_CODE_SUBPROCESS_ENV_SCRUB=1` won't help: it "leaves GitHub tokens … in place".

Recommendation: **the worker gets no GitHub credential.** It doesn't need one.
- *Issue text:* the factory fetches it and puts it in the prompt.
- *Questions / progress:* returned in the JSON result. The factory posts them.
- *Push / PR:* the factory does these after the run.
- *Reading other repos / docs:* public HTTPS works without a token.

If `start-lane` insists on calling `gh` directly, give the worker a **fine-grained PAT scoped to task-guide only, read-only** (Issues: read, Contents: read) via `GH_TOKEN`, and let the factory do every write.

How to keep the token out of reach:

| Option | Strength | Cost |
|---|---|---|
| **A. Separate Linux user** (`factory-worker`) running `claude -p`, with no gh login, its own `CLAUDE_CONFIG_DIR`, write access only to `/mnt/data/factory/work`, and not in the `docker` group | Hard boundary: the OS stops it reading `hosts.yml` | Phil must create the user and token (infra change). Shared installs needed for `claude` and `dotnet` (currently in `~/.local/bin`, `~/bin`). |
| **B. Same user, soft isolation**: worker env has `GH_CONFIG_DIR=<empty dir>`, `GIT_CONFIG_GLOBAL=<factory gitconfig without credential helper>`, `GIT_TERMINAL_PROMPT=0`, unset `GH_TOKEN`/`GITHUB_TOKEN`; `Read(~/.config/gh/**)`, `Read(~/.claude/.credentials.json)`, `Read(~/.ssh/**)` deny rules; hook blocks any command mentioning `push`, `gh`, `hosts.yml`, `credential`, `.config/gh` | Stops ordinary mistakes. A determined or prompt-injected model could still `cat` the file by a path the hook didn't think of. | No infra change |

**Recommend B to get the thin slice running while Phil watches, and A before truly unattended runs.** (Choosing A is a Phil decision.)

Factory clone: its own clone at `/mnt/data/factory/repos/task-guide` with `git remote set-url --push origin DISABLED`. The worker's git therefore can't push even with a credential. The factory pushes with an explicit URL and its own credential.

## 3. Isolation and layout

```
/opt/factory/ (or ~/factory-release)    dispatcher release copy, not ~/dev  (chart #1)
/mnt/data/factory/
  repos/task-guide/                      factory clone (bare or normal), push URL disabled
  work/task-guide/issue-<N>/             git worktree, branch factory/issue-<N>, from origin/main
  claude-home/                           CLAUDE_CONFIG_DIR for workers (settings, skills, transcripts)
  runs/<issue>/<run-id>/                 prompt.md, stream.jsonl, result.json, hook-audit.jsonl
  state/                                 durable factory state (who's running, session ids)
```

- The factory creates the worktree with `git worktree add`, not `claude -w`. `-w` puts it under `<repo>/.claude/worktrees` and the agent controls it. The factory should own it.
- `cwd` = the worktree. File tools only reach the working dir and `--add-dir`s. Don't add `~/dev`.
- Commits: the worker commits on `factory/issue-<N>`. Before pushing, the factory checks: branch name matches, commits are on top of `origin/main`, the diff touches no `.github/`, `.claude/`, `.mcp.json` or secrets, and tests pass when the factory re-runs them.

## 4. Control

- **Wall clock:** `timeout --signal=INT --kill-after=2m 90m claude -p …`. SIGINT ends the turn cleanly. SIGTERM exits 143, leaves the turn unfinished, but still kills child Bash trees and runs `SessionEnd` hooks ([headless › SIGTERM](https://code.claude.com/docs/en/headless#stop-a-run-with-sigterm)).
- **`--max-turns N`** (print-only, hidden from `--help` but documented in the [CLI reference](https://code.claude.com/docs/en/cli-reference)). The run ends with `subtype: error_max_turns`.
- **`--max-budget-usd X`**: a runaway guard based on a client-side cost *estimate*. On a subscription it's notional, but it still bounds the run. Ends with `error_max_budget_usd`.
- `BASH_DEFAULT_TIMEOUT_MS` / `BASH_MAX_TIMEOUT_MS` (default 2 min / 10 min per command). Raise the default if `dotnet test` is slow on the Pi.
- Background subagents keep `-p` alive for up to 10 idle minutes (`CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS`).
- **HERDR pane vs subprocess:** the dispatcher lives in a HERDR pane (chart #1). It should run each worker as a **plain child process** with stdout piped to a file, not in its own interactive pane. This gives you exit codes, timeouts and kills, which you need for deterministic control. To watch live, `tail -f runs/…/stream.jsonl | jq` in a second pane.
- Set `DISABLE_AUTOUPDATER=1` in the worker env so the version doesn't change mid-run. Upgrades become a factory step.

## 5. Capture

`--output-format stream-json --verbose` writes one JSON event per line. The last line is the `result`. Use `--output-format json` if you only want the final object. Test run on pi5 (`--model haiku --max-turns 1`, about $0.03 estimated). Result fields seen:

`type, subtype ("success" | "error_max_turns" | "error_max_budget_usd" | "error_during_execution"), is_error, result, structured_output (with --json-schema), session_id, num_turns, duration_ms, duration_api_ms, total_cost_usd, usage{input_tokens, output_tokens, cache_creation_input_tokens, cache_read_input_tokens, …}, modelUsage{<model>:{costUSD, …}}, permission_denials[], stop_reason, terminal_reason ("completed" | "max_turns" | "tool_deferred" | "budget_exhausted" | …), subagent_stats, api_error_status`.

- **Pick the session id up front** with `--session-id <uuid>` so the factory knows it even if the run crashes.
- **Quota signal for the governor:** stream-json emits `rate_limit_event` with `rate_limit_info{status: allowed|allowed_warning|rejected, resetsAt, utilization}` ([SDK TS ref](https://code.claude.com/docs/en/agent-sdk/typescript)). The `result` object carries no quota info. Relevant to the governor ticket.
- **Resume after a question:** `claude -p --resume <session_id> "<Phil's answer>"` with the same flags and the same `CLAUDE_CONFIG_DIR`. Lookup by ID works from any directory (v2.1.223+). The recorded system prompt is reused until compaction. Totals reported after a resume include earlier runs.
- Exit code: 0 on success, non-zero on failure. Errors inside a run come back as the `result` on stdout.

## 6. Interactive blockers in `-p`

| Thing | Behaviour in `-p` | Handling |
|---|---|---|
| `AskUserQuestion` | Only offered when the run has a permission host. Removed under `--permission-prompts none`, denied under `dontAsk`. | Worker returns `needs_input` in the exit contract. |
| Plan mode / `ExitPlanMode` | Needs a permission host. Plan mode keeps its blocks in `-p`, so the worker would be stuck read-only. | Don't start workers in plan mode. Planning happens in the prompt or skill. |
| Skill says "stop and ask Phil for approval" | The model just ends its turn with text: `subtype: success`, but the work isn't done. | **`--json-schema` exit contract** (below). `start-lane` should be told headless means "return needs_input". |
| Permission prompt | Denied immediately, listed in `permission_denials`. | Factory logs it and may raise a "widen allow list?" item for Phil. |
| Workspace-trust dialog | Skipped in `-p`. Project allow rules are ignored; project hooks and MCP still run. | See section 1. |

Exit contract (pass with `--json-schema`; read from `structured_output`):

```json
{"type":"object","required":["status","summary"],"properties":{
  "status":{"enum":["done","needs_input","blocked","failed"]},
  "summary":{"type":"string"},
  "question":{"type":"object","properties":{
     "text":{"type":"string"},"options":{"type":"array","items":{"type":"string"}},
     "recommendation":{"type":"string"},"reasoning":{"type":"string"}}},
  "commits":{"type":"array","items":{"type":"string"}},
  "tests":{"enum":["passed","failed","not_run"]}}}
```

The advanced alternative is a `PreToolUse` hook that returns `permissionDecision: "defer"` on `AskUserQuestion`. The process exits with `terminal_reason: "tool_deferred"` and the question in `deferred_tool_use`. On resume, the hook answers via `updatedInput` ([hooks › defer](https://code.claude.com/docs/en/hooks)). It's neat, but it needs a `--permission-prompt-tool` MCP host. Leave it for later.

## Recommended v1 invocation

```bash
env -i HOME=$HOME PATH=/usr/bin:/bin:$HOME/bin:$HOME/.local/bin \
  CLAUDE_CONFIG_DIR=/mnt/data/factory/claude-home \
  CLAUDE_CODE_OAUTH_TOKEN="$WORKER_OAUTH_TOKEN" \
  GH_CONFIG_DIR=/mnt/data/factory/empty-gh GIT_CONFIG_GLOBAL=/mnt/data/factory/worker.gitconfig \
  GIT_TERMINAL_PROMPT=0 DISABLE_AUTOUPDATER=1 BASH_DEFAULT_TIMEOUT_MS=600000 \
timeout --signal=INT --kill-after=2m 90m \
claude -p "$(cat prompt.md)" \
  --session-id "$SID" \
  --permission-mode dontAsk --permission-prompts none \
  --settings /mnt/data/factory/worker-settings.json \
  --strict-mcp-config \
  --max-turns 150 --max-budget-usd 15 \
  --output-format stream-json --verbose \
  --json-schema "$(cat exit-contract.json)" \
  > runs/$ISSUE/$SID/stream.jsonl 2> runs/$ISSUE/$SID/stderr.log
```

(Run it with `cwd` = the worktree. Adjust the numbers after a few real runs.)

`worker-settings.json` (sketch):

```json
{
  "permissions": {
    "allow": ["Read","Edit","Write","Glob","Grep","Skill","TodoWrite",
      "Bash(dotnet build *)","Bash(dotnet test *)","Bash(dotnet format *)",
      "Bash(git add *)","Bash(git commit *)","Bash(git checkout -b *)","Bash(git switch *)",
      "WebFetch(domain:learn.microsoft.com)"],
    "deny": ["Bash(git push *)","Bash(gh *)","Bash(curl *)","Bash(wget *)","Bash(sudo *)",
      "Bash(docker *)","Bash(systemctl *)","Bash(ssh *)","Bash(git remote *)","Bash(git config *)",
      "Bash(git reset --hard *)","Bash(git clean *)","Bash(rm -rf *)",
      "Read(~/.config/gh/**)","Read(~/.ssh/**)","Read(~/.claude/.credentials.json)",
      "Edit(.github/**)","Edit(.claude/**)","Edit(.mcp.json)"]
  },
  "hooks": {
    "PreToolUse": [{"matcher": ".*", "hooks": [{"type": "command",
      "command": "/opt/factory/bin/worker-guard"}]}]
  }
}
```

`worker-guard` is deterministic factory code. It logs every call, blocks Bash containing `push`, `gh`, `sudo`, credential paths or absolute paths outside the worktree, and blocks file tools outside the worktree. It exits 2 with a reason on block.

## Proposed split: who may do what

**Worker alone (inside its worktree, no approval):**
- Read the repo and the issue text the factory supplies; read public docs.
- Edit, create and delete files inside its worktree (except `.github/`, `.claude/`, `.mcp.json`).
- Build, run tests and run formatters.
- `git add` / `git commit` on its own `factory/issue-N` branch.
- Spawn subagents and use skills (same guardrails apply).
- Finish with a result: done / needs_input (question + options + recommendation) / blocked / failed.

**Factory (deterministic software, pre-approved by Phil as policy):**
- Choose the next issue, check quota, create the worktree and branch from `origin/main`, write the prompt.
- Launch, time out, cancel, resume; record cost, usage, denials and session ids.
- Verify before push: branch name, diff scope, no CI/secret/config files, tests re-run green.
- **Push `factory/*` branches** (never `main`, never force).
- **Open draft PRs**, post progress and questions as issue comments, set/clear labels.
- Remove its own worktrees and local branches after the PR is merged or closed.

**Needs Phil:**
- Merge any PR into `main` (or flip draft to ready, if he prefers).
- Answer `needs_input` questions; accept or reject "done".
- Any change to CI workflows, secrets, dependencies with new network or install scripts, `.claude`/`.mcp.json`.
- Widening the worker allow list or guard, enabling sandbox/auto mode, installing packages (`socat`).
- Creating the worker Linux user and the OAuth/PAT tokens.
- Deleting remote branches, closing issues outside the normal process, anything destructive outside the factory's own dirs.
- Never (nobody): force push, push to `main`, `--dangerously-skip-permissions` on the host.

## Open questions for Phil

1. Is it OK for the factory to push `factory/*` branches and open **draft** PRs with no per-item approval? (Recommended: yes.)
2. Worker isolation: soft same-user (B) for the first runs, then a separate `factory-worker` user (A)? Or go straight to A?
3. OK to run `claude setup-token` once to create a worker token, and to keep it in `/mnt/data/factory` (0600)?
4. Does `start-lane` need `gh` itself, or can it accept the issue text plus "return needs_input" when headless? (Overlaps the start-lane research ticket.)
