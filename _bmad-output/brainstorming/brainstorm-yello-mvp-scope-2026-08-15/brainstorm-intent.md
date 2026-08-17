# Brainstorm Intent — Yello

**Date:** 2026-08-15 · **Source:** `brainstorm-yello-mvp-scope-2026-08-15/.memlog.md`
**Intended consumer:** `bmad-prd` (then `bmad-architecture`)

---

## 1. The actual goal

**Yello is a test harness, not a product.** The objective is for Lee to learn the BMAD method — its capabilities *and its limitations*. Personal/hobby project. No users, no market, no deadline.

This inverts normal product discovery: **requirements are derived from the methodology, not from users.** A feature earns its place if it forces a part of BMAD to activate, not because a user asked for it.

Do not optimise the PRD for market fit, competitive positioning, or time-to-value. Optimise it for *surface contact with BMAD*.

## 2. Product concept (deliberately undefined)

> Yello — a multi-tenant project/task management platform.

That one line is the entire concept, and **defining it is the PRD's job, not the brainstorm's.** Scope discovery was deliberately withheld here so that the quality of `bmad-prd`'s coached discovery becomes a measurable data point. Treat this as a genuine cold start.

## 3. Governing reframe

Two reframes reached during the session, both binding on downstream work:

1. **This is a coverage problem, not a design problem.** The question is not "what should Yello be?" but "what must Yello contain so every part of BMAD is forced to activate?"
2. **Don't design a test for failure modes you can't predict.** Design for *guaranteed contact* with every surface plus enough friction that each surface has something to grip. Limitations reveal themselves on contact — the retrospectives capture them.

## 4. Required properties — treat as PRD constraints

Yello must genuinely have all nine. Each unlocks BMAD surfaces that stay dormant otherwise. Full rationale in `docs/bmad-coverage.md`.

| # | Property | Unlocks |
|---|----------|---------|
| P1 | Multi-tenant isolation | An architectural invariant spanning every epic |
| P2 | Roles & permissions | A cross-cutting concern that must constrain stories, not just describe them |
| P3 | Concurrent / real-time editing | Hard invariants: races, conflict resolution, ordering |
| P4 | External integrations | Contract testing, versioning, failure handling |
| P5 | Substantial UI | Makes the UX phase more than a formality |
| P6 | Planned mid-flight requirements change | `correct-course`, re-planning, a retrospective with real findings |
| P7 | Deliberate brownfield re-entry | `document-project`, `generate-project-context` |
| P8 | Falsifiable non-functional requirements | NFR evidence audit — claims that can actually fail |
| P9 | Enough epics that ordering matters | Story sequencing and dependency management |

**Open claim to test:** Lee's position is that Yello as conceived covers all nine. This is currently unfalsifiable. The PRD is where it gets tested — if a property has no natural home in the feature set, say so rather than contriving one.

**PRD deliverable beyond the usual:** name the **carrier** for each property — the epic or feature that actually delivers it. Empty carriers go back into `docs/bmad-coverage.md` as gaps.

## 5. Anti-patterns — reject these in the PRD

Each produces a comfortable, useless test:

- **All-CRUD design** — every feature its own table and form, no rule spanning two features. The architecture spine gets no invariants to hold and BMAD flattens the project.
- **Frozen requirements** — never changing your mind means `correct-course` never fires, and you never see what BMAD does when phase 3 proves wrong during phase 4.
- **Nothing genuinely fails** — no acceptance criteria with teeth leaves the entire 7-skill Test Architecture module idle.
- **Contrived complexity** — a requirement bolted on purely to reach a BMAD skill. Yields a weak signal: you learn how BMAD handles a fake project.

Authentic complexity — where the requirement genuinely follows from the domain — is worth materially more than contrived complexity that ticks the same box.

## 6. Working relationship

Lee's contributions in this session were consistently **editorial rather than generative** — reject, reframe, decide. He is best deployed as a *critic of what BMAD produces*, not as a source of ideas fed into it.

Practical implication for the PRD session: **propose concrete options and let him cut them.** Open-ended "what would you like?" prompts will stall — he has said plainly he doesn't yet know enough about BMAD to answer questions about its edges, and he shouldn't be asked to.

## 7. Standing artifacts

- `docs/bmad-coverage.md` — permanent coverage tracker: properties, full skill checklist by phase/module, findings log, Bronze→Platinum milestones. **Update at the end of every BMAD session**, not in a batch.
- The findings log is the real output of this whole endeavour. A ticked box records what was touched; a finding records what was learned.

## 8. Open question logged against the method

Does BMAD's phase gating cope with a project whose requirements are derived from the methodology rather than from users? Watch for friction where the workflow assumes user-driven discovery.
