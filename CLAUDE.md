---
tags:
  - AI Generated
  - project/pi-software-factory
---

# Concept - Personal Software Factory

## Purpose

Build a personal software factory that makes useful progress while Phil's attention is elsewhere. It is a reusable SDLC and operating system for multiple repositories—not a machine for maximizing activity. Projects may stop at **done enough** when the remaining value no longer justifies the attention or model budget.

## Top-level goals

- Protect Phil's scarce attention by making unattended progress safe and visible.
- Manage five-hour, weekly, and session model budgets deliberately, while preserving an interactive reserve for urgent work and high-value collaboration.
- Reuse a personal SDLC across projects without trapping its state or practices inside any one orchestration product.
- Make trade-offs explicit: spend tokens and attention where risk, unknowns, or likely value justify them.

## Principles

- The factory's state is durable; agents and their sessions are ephemeral workers.
- Iterate according to risk, unknowns, and value. Discover material risk early instead of doing big design up front.
- Phil manages the portfolio, investment decisions, and exceptions. Agents do the work. Deterministic software manages the work.
- LLMs propose; software enforces; Phil decides.
- Questions should arrive with a proposal, options, recommendation, and—where useful—a prototype or experiment.
- Attention items retain a durable identity even when their required attention expands from a quick answer into a deep-dive or contracts back to a small decision.
- Stop working on a project when the remaining likely value is not worth the needed human attention or tokens.

## Conceptual workflow: Phil

Phil drops in irregularly through a phone-friendly, Tailscale-accessible Kanban and management interface. A check-in shows progress, durable questions and proposals, resource spend, and the pipeline.

Phil chooses the attention items worth addressing now, answers small questions, reviews work, or changes investment. An item can become a grilling or deep-dive session without losing its identity or history. Phil can reprioritize, start, pause, or stop projects, keeping the portfolio aligned with available attention and budget.

## Conceptual workflow: agents and factory

1. Accept work and perform focused initial grilling.
2. Identify the important risks, unknowns, and possible value.
3. Build the smallest useful or risk-reducing slice.
4. Research, spike, prototype, and test as appropriate.
5. Surface a durable attention item only when human input is needed; include options, a recommendation, and the reasoning behind the recommendation.
6. Incorporate feedback and move toward lower-human-touch implementation as uncertainty falls.
7. Conclude at done enough when further work no longer earns its cost.  This may be an explicit "button press", but is more likely to simply be a loss of priority/attention from Phil.

## Top-level architecture

```text
Management Plane
  Kanban; durable projects, cards, attention items, budgets, and audit history
        ↓
Factory Control Plane
  Evolving personal SDLC; replaceable orchestration (such as OpenRig, if useful);
  resource governor and scheduler
        ↓
Execution Plane
  HERDR Session on a Raspberry Pi keeping Claude Code CLI, Codex CLI, and
  possibly OpenCode workers running
```

The personal SDLC and all durable factory state belong to the factory. OpenRig or any other orchestration substrate is replaceable implementation detail, not the system of record.

## Hermes Agent hypothesis

Hermes Agent is a candidate to prototype and test only as a possible deterministic management-plane implementation. Its AI management features must be controllable or disableable so routine management uses no scarce LLM budget. If Hermes cannot operate suitably in that deterministic mode, use it as design inspiration for a custom deterministic management plane—not as the chosen architecture.

## Coding standards

See [docs/coding-standards.md](docs/coding-standards.md). Issue tracking: [docs/agents/issue-tracker.md](docs/agents/issue-tracker.md).
