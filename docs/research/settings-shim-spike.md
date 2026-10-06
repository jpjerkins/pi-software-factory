# `--settings` hooks and PATH shim spike

Run on pi5, 2026-10-05, HERDR 0.9.3, Claude Code 2.1.290. One `herdr agent start --kind claude` in tab `spike-slot` of workspace `factory`, with `--settings` pointing at a hooks file and a `claude` shim dir put first on `PATH` via `tab create --env`.

## Findings

- **A. `--settings` hooks fire: yes.** `hooks.log` got `SessionStart`, `PreToolUse Bash`, `SessionEnd`. `Notification` did not fire (no prompt happened). Format used: `{"hooks":{"<Event>":[{"matcher":"*","hooks":[{"type":"command","command":"bash <abs path>"}]}]}}`.
- **B. Merge, not override.** Phil's global `SessionStart` hook still ran: `herdr agent get spike-1` showed `agent_session.value` equal to the `--session-id`, source `herdr:claude`, and status went working, then idle. Hook arrays are merged (inferred from this; the global `UserPromptSubmit` and `reset-model` hooks were not separately checked).
- **C. The shim did NOT run.** No `exit_code` file. `pane process-info` showed `claude` directly under the pane's bash (parent pid = shell), no shim process. Cause: the pane's shell is an interactive bash and `~/.bashrc` prepends `~/.local/bin`, `~/.npm-global/bin`, `~/.opencode/bin` to `PATH`, so the real `claude` wins over `S/bin`. The process env of claude showed a `PATH` starting with `/home/philj/.opencode/bin`, not the shim dir. HERDR recognised the agent fine (name `spike-1`, `interactive_ready: true`). Earlier worker-spike.md said a PATH shim works; it may have worked there because of how that shim was placed or a different PATH. Not re-tested here. Fixes to try: have the factory call the shim by absolute path rather than relying on `PATH`, or put the shim dir at a location `.bashrc` puts first (for example `~/.opencode/bin`, which is a bad idea), or avoid the interactive shell.
- **D. `GH_TOKEN` blank in hook env: yes.** All hook lines show `GH_TOKEN=[<empty>]`; claude's env had `GH_TOKEN=` and `GITHUB_TOKEN=` (empty). HERDR server processes (pids 707809, 1643740) have neither variable set in `/proc/<pid>/environ`, so nothing to leak from the server (the server may also have been started without them; values never printed).
- **E. Trust entry avoided the dialog: yes.** After setting `projects["<S>/repo"].hasTrustDialogAccepted = true`, claude started with no trust dialog and no stall. Backup of `~/.claude.json` is in the scratch folder.
- **F. Surprises.**
  - The shim was bypassed by `.bashrc` (above). This is the main finding.
  - `uuidgen` is not installed on pi5. My first attempt expanded it to empty and `--session-id` swallowed the prompt ("Invalid session ID"). Use `python3 -c 'import uuid;print(uuid.uuid4())'` or `/proc/sys/kernel/random/uuid`. `agent start` then timed out with the pane left at a shell prompt.
  - `agent start` returned status `done` right away because the one-shot prompt had already finished; `agent get` then said `idle`.
  - `herdr workspace create` makes a default tab `w2:t1` along with the workspace. It was left alone.
  - `/exit` via `pane send-text` then `pane send-keys Enter` worked; `SessionEnd` fired.
