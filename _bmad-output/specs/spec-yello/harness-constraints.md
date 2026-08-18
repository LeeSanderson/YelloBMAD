# Harness Constraints

Companion to `SPEC.md` (SPEC-yello). Holds the constraints that come from Yello being a test harness rather than a commercial product.

Yello exists so Lee can learn the BMad method — its capabilities *and its limitations*. That inverts normal product discovery: **requirements are derived from the methodology, not from users.** A feature earns its place by forcing part of BMad to activate, not because a user asked for it. Nothing here is optional decoration; each item below rules something out.

Two reframes govern every scope decision:

1. **This is a coverage problem, not a design problem.** The question is not "what should Yello be?" but "what must Yello contain so every part of BMad is forced to activate?"
2. **Do not design a test for failure modes you cannot predict.** Design for *guaranteed contact* with every surface, plus enough friction that each surface has something to grip. Limitations reveal themselves on contact; the retrospectives capture them.

The living record of what has been exercised and what was learned is `docs/bmad-coverage.md`. That file is the source of this one and continues to be updated per session; it is deliberately **not** a companion, because its skill-coverage checklist and findings log are a record of how artifacts were produced rather than requirement content. Read this file for the constraints; read that one for progress.

## The nine required properties

Yello must genuinely have all nine. Each unlocks BMad surfaces that stay dormant otherwise. **Carrier** is the capability that actually delivers it — an empty carrier is a reported gap, never a contrived feature.

| # | Property | Why it matters | Carrier |
|---|---|---|---|
| P1 | Multi-tenant isolation | A real architectural invariant spanning *every* epic, so the architecture has something to hold and every story must respect it | CAP-15, CAP-9, NFR-1. Many-to-many Membership makes authorisation `(Account, Space)` rather than a uniform column filter |
| P2 | Roles and permissions | A genuine cross-cutting concern. Tests whether the architecture actually *constrains* downstream stories or merely describes them | CAP-16, CAP-13, CAP-14. Role is per-Space, so no story can name a capability without naming a Space |
| P3 | Concurrent / real-time editing | The hardest invariants — races, conflict resolution, ordering. Forces real test design over happy-path checking | CAP-31 convergence, CAP-33 reconnection, **CAP-34 permission change on a live session** (the load-bearing one). Secondary surface: CAP-29 Board ordering races |
| P4 | External integrations | Contract testing, API versioning, third-party failure handling | **PARTIAL — see the gap below.** CAP-37 and CAP-35 carry versioning and contract testing |
| P5 | Substantial UI | The only thing that makes the UX phase more than a formality | CAP-28, CAP-30, plus the Space context bar as a functional surface, Space/Project settings, and NFR-9 |
| P6 | Planned mid-flight requirements change | Activates correct-course, re-planning, and a retrospective with real findings | **RESERVED — deliberately absent from v1. See below.** |
| P7 | Deliberate brownfield re-entry | Documenting and re-contextualising code BMad wrote months earlier | Time-based; no carrier required or possible |
| P8 | Falsifiable non-functional requirements | The NFR evidence audit needs claims that can actually *fail* | NFR-1 … NFR-9 plus the two gating metrics. NFR-1 has no acceptable failure rate; NFR-8 gives every budget a stated domain |
| P9 | Enough epics that ordering matters | Story sequencing and dependency management. With two epics, sequencing teaches nothing | 41 capabilities across 11 groups with genuine dependency ordering: identity → Spaces → Membership → Access Control → Projects → Tasks → Status config → Views → Collaboration, with API and Notifications cutting across |

Carriers are named at capability level, not epic level, because epics did not exist when they were assigned. **The mapping needs revisiting once epics exist**, since one capability group may split across several.

## The P4 gap — reported, not contrived

Two of P4's three purposes are carried; one is not.

- **API versioning** — carried by CAP-37.
- **Contract testing** — carried. The public API is a published contract with a stable shape, which is what consumer-driven contract testing needs.
- **Third-party failure handling** — **not carried.** Yello depends on almost nothing external. Email delivery is the only outbound integration and it is fire-and-forget. There is no third-party API to be rate-limited by, time out against, or receive a breaking change from.

**Resolution: report the gap for v1 and close it later with OAuth sign-in** — a feature wanted regardless, deferred rather than ruled out. It introduces Yello's first genuine inbound third-party dependency and with it provider outage, token expiry, revoked consent and provider contract change. Because it is independently wanted, it closes P4 without triggering the contrived-complexity anti-pattern.

**Rejected:** bolting on calendar sync or a similar dependency purely to reach the surface. Webhooks remain available as a further option (outbound delivery, retry, backoff, replay).

## The P6 reserve — v1 is deliberately not frozen

A mid-flight requirements change is a *required property*, so one is held back on purpose. Two candidates are both genuinely wanted rather than manufactured, and both sit in the deferred list in `SPEC.md`:

| Candidate | Shape of the ripple |
|---|---|
| **Iteration planning** (cycles/sprints) | Ripples **wide** — through Task, Board, API and permissions simultaneously |
| **OAuth sign-in** | Ripples **deep** into one subsystem (authentication), and closes the P4 gap at the same time |

Either works; running both would exercise correct-course twice against changes of different shapes. **`SPEC.md` carries an open question on which fires and when**, because the choice determines which v1 assumptions must stay soft — deciding it after sprint planning is too late.

## Anti-patterns — reject these

Each would produce a comfortable, useless test.

- **All-CRUD design** — every feature its own table and form, with no rule spanning two features. The architecture then has no invariants to hold and BMad flattens the whole thing. *Avoided:* several rules genuinely span capabilities — CAP-15 conditions every read and write in the spec; CAP-26 makes Status removal a migration crossing Projects; CAP-34 puts authorisation inside the concurrency path; CAP-36 resolves Token capability at request time against a Role that can change; CAP-21 constrains Assignee to the Task's own Space.
- **Frozen requirements** — decide everything up front and never change your mind, so correct-course never fires and you never see what BMad does when phase 3 proves wrong during phase 4. *Avoided by the P6 reserve above.*
- **Nothing genuinely fails** — no acceptance criteria with teeth, leaving the whole test-architecture module idle while you tell yourself testing was covered. *Avoided:* NFR-1 has no acceptable failure rate, a gating metric blocks release on it, and CAP-34 is named as the criterion the product should be judged on.
- **Contrived complexity** — a requirement bolted on purely to reach a BMad skill. Teaches you how BMad handles a fake project, which is a weak signal. *Avoided, and tested once:* the P4 gap was left open rather than closed with a bolted-on dependency.

**Authentic complexity is worth materially more than contrived complexity that ticks the same box.** When the two conflict, report the gap.

## Working relationship

Recorded because it changed how upstream skills had to be run, and will change how downstream ones should be.

Lee's contributions are consistently **editorial rather than generative** — reject, reframe, decide. He is best deployed as a critic of what BMad produces, not as a source of ideas fed into it. **Propose concrete options and let him cut them;** open-ended "what would you like?" prompts stall.

This has already required overriding two stated interaction rules by hand, because no mechanism exists for an upstream artifact to set a downstream skill's interaction style. Expect to override them again.
