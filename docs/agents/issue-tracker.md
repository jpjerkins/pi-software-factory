# Issue tracker

This repo uses **GitHub issues** on `jpjerkins/pi-software-factory` (chosen by Phil, 2026-10-04).

## Cartography operations

- **Chart**: a single issue labelled `cartographer:map`, holding the
  destination / *Not yet specified* / *Out of scope* / Decisions so far /
  Oracle / Sounding shape body. `gh issue create --label cartographer:map`.
- **Child ticket**: an issue linked to the chart as a GitHub sub-issue (`gh
  api` on the sub-issues endpoint). Where sub-issues aren't enabled, add the
  child to a task list in the chart body and put `Part of #<chart>` at the
  top of the child body. Kind is a `cartographer:<kind>` label, one per
  ticket — `CHART.md` defines the kinds and what each means. Once claimed,
  the ticket is assigned to the driving dev.
- **States**: the issue's own open/closed state carries the outer two;
  the middle state is a `cartographer:prepped` label, GitHub having no
  native tri-state. `carried:` is an integer line in the ticket body.
  `CHART.md` and `BRIEF.md` define the states and when `carried:` moves.
- **Blocking**: GitHub's **native issue dependencies** — the canonical,
  UI-visible representation. Add an edge with `gh api --method POST
  repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F
  issue_id=<blocker-db-id>`, where `<blocker-db-id>` is the blocker's
  numeric **database id** (`gh api repos/<owner>/<repo>/issues/<n> --jq
  .id`, _not_ the `#number` or `node_id`). GitHub reports
  `issue_dependencies_summary.blocked_by` (open blockers only — the live
  gate). Where dependencies aren't available, fall back to a `Blocked by:
  #<n>, #<n>` line at the top of the child body. A ticket is unblocked when
  every blocker is closed.
- **Frontier query**: `gh issue list --state open` scoped to the chart's
  children, keeping only those with no open blocker
  (`issue_dependencies_summary.blocked_by == 0`, or no open issue in a
  `Blocked by` line), no assignee, and no `cartographer:prepped` label;
  first in chart order wins.
- **Eligible query**: the same child set filtered to
  `cartographer:prepped`, then by the ticket body's oracle role — see
  `BRIEF.md` for what makes a ticket eligible.
- **Claim**: `gh issue edit <n> --add-assignee @me` — the session's first
  write.
- **Prepped**: `gh issue edit <n> --add-label cartographer:prepped`, with
  the variant set link added to the ticket body.
- **Sounded decision lines**: appended to the chart issue's Decisions so
  far (direct body edit, or `gh issue comment <chart>` cross-linked into
  the body), with each required link as a markdown link in the same line.
  `CHART.md` states which links a `sounded` line carries.
- **Resolve**: `gh issue close <n>` once its decision line is written.
