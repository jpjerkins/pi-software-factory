# Coding standards — pi-software-factory

Set by Phil, 2026-10-04 (chart #1). Applies to all factory code and every agent working on it.

## Stack
- **C# on .NET 10** (latest full release).
- Front end: to be decided.

## Structure
- **Onion architecture** with **SOLID**: Domain at the centre, then Application, then Adapters/Infrastructure, with the host (CLI) outermost. Dependencies point inward only, and the Domain has no I/O.
- **DDD naming** in the backend: name types after domain concepts (`Issue`, `Run`, `EligibilityPolicy`), not technical roles.
- **Small files.** One type per file.

## Tests
- **Always test-first** (red, then green, then refactor).
- Domain and Application are tested with **fakes** for every adapter (GitHub, git, HERDR, Claude trust, usage, run store).
- Adapters get focused tests where practical. End-to-end runs are manual and watched in HERDR.

## Where things live
- **All source code lives in `~/dev`**: the factory in `~/dev/pi-software-factory`. The factory's clones of the projects it works on are in `~/dev/factory/<repo>`, with per-issue worktrees under each clone.
- **Live = compiled output** of `dotnet publish`, never run from the source folder and never a second copy of the source.
- **Live data** goes in `/mnt/data/factory`.
- **v1 runtime:** the dispatcher runs in a HERDR pane, for visibility and debugging. Agent workers also run in HERDR panes Phil can watch. Moving the dispatcher into a DCM container waits until its logging can be trusted.
