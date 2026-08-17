# BMAD Coverage Tracker

**Purpose:** Yello is a test harness. The goal is to learn the BMAD method — its capabilities *and* its limitations. This file tracks which parts of BMAD have been exercised, which remain, and — most importantly — **what each one actually taught**.

**Started:** 2026-08-15
**Owner:** Lee

---

## How to use this

1. **Status** — `⬜ Not started` · `🟡 In progress` · `✅ Done` · `⛔ N/A for this project`
2. **Findings** is the column that matters. A tick means you touched it; a finding means you learned something. If a row is `✅` with an empty finding, you went through the motions.
3. Update this at the end of each BMAD session, not in a batch at the end. Retrospective-quality memory decays fast.
4. Findings worth keeping should be phrased as **capability** ("BMAD did X well") or **limitation** ("BMAD couldn't handle Y / needed manual intervention at Z").

> **Standing risk — tested 2026-08-16 at the PRD, partially upheld.** The claim was that Yello as conceived covers all nine properties. The PRD makes it falsifiable and the verdict is: **seven carried, one partial, one not yet applicable.** P1, P2, P3, P5, P8 and P9 have concrete carriers; P6 has a reserved candidate held outside MVP; **P4 is partial** — API versioning and contract testing are carried, third-party failure handling is not, because Yello depends on almost nothing external. P7 is time-based and has no PRD carrier by nature. The claim survives, but not intact. Still provable only at implementation. Revisit whenever scope changes.

---

## Part 1 — Project properties that unlock BMAD surfaces

A BMAD skill only activates if the project contains something for it to grip. These are the properties Yello must genuinely have. **Carrier** = the epic/feature that will actually deliver it (fill in during PRD / epics).

| # | Property | Why it matters | Carrier (from PRD 2026-08-15) | Status |
|---|----------|----------------|-------------------|--------|
| P1 | Multi-tenant isolation | A real architectural invariant spanning *every* epic — the architecture spine has something to hold, and every story must respect it | **§4.2 Spaces + §4.4 Access Control.** FR-15 (Space-scoped authorisation), FR-9 (context resolution before authorisation), NFR-1 (isolation absolute). Many-to-many Membership means authorisation is `(Account, Space)`, not a uniform column filter | 🟡 |
| P2 | Roles & permissions | A genuine cross-cutting concern. Tests whether the architecture spine actually *constrains* downstream stories or merely describes them | **§4.3 Membership + §4.4 Role matrix.** FR-16 (four-Role capability matrix), FR-13 (Admin cannot promote to Admin), FR-14 (Owner unremovable). Role is per-Space, so no story can name a capability without naming a Space | 🟡 |
| P3 | Concurrent / real-time editing | The hardest invariants — races, conflict resolution, ordering. Forces real test design over happy-path checking | **§4.9 Collaborative Task Editing.** FR-31 convergence, FR-33 reconnection, **FR-34 permission change on a live session** (the load-bearing one). Secondary surface: FR-29 Board ordering races | 🟡 |
| P4 | External integrations | Contract testing (Pact utils are installed but disabled), API versioning, failure handling | **§4.10 Public API — PARTIAL.** FR-37 versioning/deprecation and FR-35 published contract cover contract testing and versioning. **Gap: third-party failure handling.** Yello's only outbound dependency is email (FR-39/40); the API is inbound-only and webhooks are out of MVP scope. See gap note below | 🟡 |
| P5 | Substantial UI | The only thing that makes the UX phase more than a formality | **§4.8 Board and List Views + §7 Information Architecture.** FR-28–30, plus the Space context bar as a functional surface (authorisation context, not navigation), Space/Project settings, and NFR-9 (WCAG 2.1 AA, keyboard-operable Board) | 🟡 |
| P6 | Planned mid-flight requirements change | Activates `correct-course`, re-planning, and a retrospective with real findings | **RESERVED — deliberately absent from the PRD.** Primary candidate: **iteration planning (cycles/sprints)**, listed in §9.2 as out of MVP scope. Ripples through Task, Board, API and permissions simultaneously. Backups: Task comments/activity history, webhooks | ⬜ |
| P7 | Deliberate brownfield re-entry | `document-project` and `generate-project-context` against code BMAD wrote months earlier | Time-based; no PRD carrier required or possible | ⬜ |
| P8 | Falsifiable non-functional requirements | The NFR evidence audit needs performance/security/reliability claims that can actually *fail* | **§5 Cross-Cutting NFRs (NFR-1–9) + §10 SM-1/SM-2.** NFR-1 has no acceptable failure rate; NFR-2 (5s revocation), NFR-3 (300ms p95 propagation), NFR-4 (2s convergence, 10 editors), NFR-5 (300/500ms p95), NFR-8 (stated scale envelope so every budget has a domain) | 🟡 |
| P9 | Enough epics that ordering matters | Story sequencing and dependency management. With two epics, sequencing is trivial and teaches nothing | **11 features, 40 FRs** with genuine dependency ordering: identity → Spaces → Membership → Access Control → Projects → Tasks → Status config → Views → Collaboration, with API and Notifications cutting across | 🟡 |

**Status key for this table:** 🟡 = carrier identified in the PRD, unproven until implementation.

### Gap: P4 is only partially carried

Reported rather than contrived, per the brainstorm's instruction. The PRD covers two of P4's three purposes:

- **API versioning** — carried (FR-37: no field removed/renamed/retyped within a version, deprecation announced before withdrawal).
- **Contract testing** — carried. The public API is a published contract with a stable shape, which is exactly what consumer-driven contract testing needs.
- **Third-party failure handling** — **not carried.** Yello depends on almost nothing external. Email delivery is the only outbound integration, and it is fire-and-forget. There is no third-party API to be rate-limited by, time out against, or receive a breaking change from.

**Resolution taken (2026-08-15):** report the gap for MVP, close it later with **OAuth sign-in** — a feature Lee wants regardless, deferred in PRD §9.2 rather than ruled out. It introduces Yello's first genuine inbound third-party dependency, and with it provider outage, token expiry, revoked consent and provider contract change. Because it is independently wanted, it closes P4 without triggering the contrived-complexity anti-pattern.

This also makes OAuth a **strong P6 candidate** — a mid-flight requirements change that is genuinely desired rather than manufactured. It ranks alongside iteration planning; iteration planning ripples wider through the domain, OAuth ripples deeper into a single subsystem (authentication) and closes a coverage gap at the same time. Either works; running both would exercise `correct-course` twice against changes of different shapes.

Rejected: bolting on calendar sync or a similar dependency purely to reach the surface. Webhooks remain available as a further option (outbound delivery, retry, backoff, replay) and are noted in the PRD addendum.

### Anti-patterns to avoid

Identified during the scoping brainstorm — these would produce a comfortable, useless test:

- **All-CRUD design** — every feature its own table and form, no rule spanning two features. The architecture spine has no invariants to hold and BMAD flattens the whole thing.
- **Frozen requirements** — decide everything up front, never change your mind. `correct-course` never fires; you never see what BMAD does when phase 3 proves wrong during phase 4.
- **Nothing genuinely fails** — no acceptance criteria with teeth. The entire 7-skill Test Architecture module sits idle while you tell yourself testing was covered.
- **Contrived complexity** — a requirement bolted on purely to reach a BMAD skill. Teaches you how BMAD handles a fake project, which is a weak signal.

---

## Part 2 — BMAD skill coverage

### Phase 1 — Analysis

| Skill | Code | Status | Findings — capability / limitation |
|-------|------|--------|-------------------------------------|
| `bmad-brainstorming` | BP | ✅ | Coverage-first reframe worked well; technique batch was abandoned early when the premise (a design tension) didn't hold — the skill adapted fine to being redirected |
| `bmad-market-research` | MR | ⬜ | |
| `bmad-domain-research` | DR | ⬜ | |
| `bmad-technical-research` | TR | ⬜ | |
| `bmad-product-brief` | CB | ⬜ | |
| `bmad-prfaq` | WB | ⬜ | |

### Phase 2 — Planning

| Skill | Code | Req | Status | Findings — capability / limitation |
|-------|------|-----|--------|-------------------------------------|
| `bmad-prd` — create | PRD | ✔ | ✅ | **Capability:** coached discovery genuinely produced the model — the option-and-cut format surfaced decisions (Status inheritance, rename cascade) that would otherwise have surfaced at architecture. **Limitation:** the rigor dial is stakes-based and assumes user-driven discovery; needed a manual override. Two interaction rules (elicit-don't-direct, capture-don't-author journeys) had to be overridden by hand because the skill has no mechanism for a prior artifact to set interaction style |
| `bmad-prd` — update | PRD | | ⬜ | |
| `bmad-prd` — validate | PRD | | 🟡 | Rubric walker exercised inside the Finalize gate, not as a standalone Validate run. Caught two real defects (§0 reading map described template numbering; §0 pointed at the wrong section for the Assumptions Index). Synthesis pipeline and HTML report not exercised — Finalize deliberately keeps findings in-conversation |
| `bmad-ux` | CU | | ⬜ | |

### Phase 3 — Solutioning

| Skill | Code | Req | Status | Findings — capability / limitation |
|-------|------|-----|--------|-------------------------------------|
| `bmad-architecture` | CA | ✔ | ✅ | **Capability:** the "show the load-bearing calls, don't make them silently" rule did real work — three recommendations were overridden (Blazor WASM over a TS client, Azure SQL over Postgres, SWA+Functions over one Container App) and the skill surfaced hard blockers instead of quietly building on them: Azure Functions cannot hold WebSockets, the SWA backend proxy explicitly refuses them, YDotNet ships no WASM target. **Capability:** the mandated web verification earned its keep twice — it caught a *critical* data-leak class (SESSION_CONTEXT under connection pooling + a documented parallel-plan defect returning another tenant's rows under RLS) and a defect in the skill's own draft (`Guid.CreateVersion7()` fragments a SQL Server index exactly like a v4). **Capability:** deferring the merge algorithm behind a port whose contract is a conformance suite is the spine philosophy working as designed — it converts an agent's judgement call into a test outcome. **Limitation:** `references/reviewer-gate.md` specifies parallel subagents precisely because "a fresh reviewer finds the divergences the author talks past"; run inline (session policy) it still found six issues including two of its own, but the independence property it names is lost and that result isn't guaranteed. **Limitation:** the spine template has no slot for *why* — by design, rationale lives in the memlog — so the spine alone is not reviewable by a human who wasn't in the session. The "something to review it by" artifact had to be built separately to make the decisions judgeable at all. |
| `bmad-create-epics-and-stories` | CE | ✔ | ⬜ | |
| `bmad-check-implementation-readiness` | IR | ✔ | ⬜ | |

### Phase 4 — Implementation

| Skill | Code | Req | Status | Findings — capability / limitation |
|-------|------|-----|--------|-------------------------------------|
| `bmad-sprint-planning` | SP | ✔ | ⬜ | |
| `bmad-sprint-status` | SS | | ⬜ | |
| `bmad-create-story` — create | CS | ✔ | ⬜ | |
| `bmad-create-story` — validate | VS | | ⬜ | |
| `bmad-dev-story` | DS | ✔ | ⬜ | |
| `bmad-code-review` | CR | | ⬜ | |
| `bmad-checkpoint-preview` | CK | | ⬜ | |
| `bmad-qa-generate-e2e-tests` | QA | | ⬜ | |
| `bmad-retrospective` | ER | | ⬜ | |

### Anytime — BMad Method

| Skill | Code | Status | Findings — capability / limitation |
|-------|------|--------|-------------------------------------|
| `bmad-document-project` | DP | ⬜ | |
| `bmad-generate-project-context` | GPC | ⬜ | |
| `bmad-quick-dev` | QQ | ⬜ | |
| `bmad-correct-course` | CC | ⬜ | |
| `bmad-agent-tech-writer` — write | WD | ⬜ | |
| `bmad-agent-tech-writer` — mermaid | MG | ⬜ | |
| `bmad-agent-tech-writer` — validate | VD | ⬜ | |
| `bmad-agent-tech-writer` — explain | EC | ⬜ | |
| `bmad-agent-tech-writer` — standards | US | ⬜ | |

### Anytime — Core

| Skill | Code | Status | Findings — capability / limitation |
|-------|------|--------|-------------------------------------|
| `bmad-spec` | SP | ⬜ | |
| `bmad-forge-idea` | FI | ⬜ | |
| `bmad-party-mode` | PM | ⬜ | |
| `bmad-index-docs` | ID | ⬜ | |
| `bmad-shard-doc` | SD | ⬜ | |
| `bmad-editorial-review-prose` | EP | ✅ | Found a genuine model contradiction the rubric walker missed — the Glossary still defined Status as "a fixed set defined per Space" after §4.7 replaced that with Space defaults plus Project delta. A copy-edit pass catching a semantic defect suggests term-level review has value beyond style |
| `bmad-editorial-review-structure` | ES | ✅ | Caught a dependency-ordering error (journeys used Glossary terms before the Glossary defined them) and ~250 words of true duplication between §2.2 Non-Users and §8 Non-Goals. **Limitation:** its length-reduction framing fits poorly here — a PRD deliberately written at launch rigour reads to it as a candidate for cutting |
| `bmad-review-adversarial-general` | AR | ⬜ | |
| `bmad-review-edge-case-hunter` | ECH | ⬜ | |
| `bmad-customize` | BC | ⬜ | |
| `bmad-advanced-elicitation` | — | ⬜ | |
| `bmad-help` | BH | ✅ | Correctly detected the stalled July session and empty artifact tree; routing recommendation was accurate |

### Test Architecture Enterprise (TEA)

Needs **P8** (falsifiable NFRs) and **P3** (concurrency) to be meaningful.

| Skill | Code | Status | Findings — capability / limitation |
|-------|------|--------|-------------------------------------|
| `bmad-teach-me-testing` | TMT | ⬜ | |
| `bmad-testarch-test-design` | TD | ⬜ | |
| `bmad-testarch-framework` | TF | ⬜ | |
| `bmad-testarch-ci` | CI | ⬜ | |
| `bmad-testarch-atdd` | AT | ⬜ | |
| `bmad-testarch-automate` | TA | ⬜ | |
| `bmad-testarch-test-review` | RV | ⬜ | |
| `bmad-testarch-nfr` | NR | ⬜ | |
| `bmad-testarch-trace` | TRC | ⬜ | |

### BMad Builder (BMB) — stretch

Not required to build Yello. Reach these only if you want to extend BMAD itself — e.g. after finding a limitation you'd rather fix than work around.

| Skill | Code | Status | Findings — capability / limitation |
|-------|------|--------|-------------------------------------|
| `bmad-agent-builder` — build | BA | ⬜ | |
| `bmad-agent-builder` — analyze | AA | ⬜ | |
| `bmad-workflow-builder` — build | BW | ⬜ | |
| `bmad-workflow-builder` — analyze | AW | ⬜ | |
| `bmad-workflow-builder` — convert | CW | ⬜ | |
| `bmad-module-builder` — ideate | IM | ⬜ | |
| `bmad-module-builder` — create | CM | ⬜ | |
| `bmad-module-builder` — validate | VM | ⬜ | |

### BMAD Loop — stretch

Unattended dev loop. Most interesting *after* you understand the manual story cycle, as a comparison point.

| Skill | Code | Status | Findings — capability / limitation |
|-------|------|--------|-------------------------------------|
| `bmad-loop-setup` | SA | ⬜ | |
| `bmad-dev-auto` | — | ⬜ | |
| `bmad-loop-sweep` | ST | ⬜ | |
| `bmad-loop-resolve` | — | ⬜ | |

---

## Part 3 — Findings log

The real output. One entry per thing learned, newest last.

| Date | Skill / phase | Type | Finding |
|------|---------------|------|---------|
| 2026-08-15 | `bmad-brainstorming` | Capability | Redirected cleanly when the chosen technique's premise was rejected — abandoned TRIZ rather than forcing the framework onto a problem that had no contradiction |
| 2026-08-15 | Method-level | Open question | Does BMAD's phase gating cope with a project whose requirements are derived from the *methodology* rather than from users? Normal product discovery is inverted here |
| 2026-08-16 | `bmad-prd` | Limitation | **Answers the 2026-08-15 open question, partially.** The skill calibrates PRD rigour by stakes — hobby / internal / launch — which presumes discovery is user-driven. A methodology-driven project reads as hobby stakes (no users, no market, no deadline) while needing launch rigour, because the downstream phases are the real consumer. Required an explicit manual override. The gate did not detect the mismatch; a human had to |
| 2026-08-16 | `bmad-prd` | Limitation | The skill's interaction rules ("elicitation, not direction"; user journeys "captured, not authored") are stated as absolutes with no mechanism for an upstream artifact to override them. `brainstorm-intent.md` explicitly recorded that Lee works editorially and that open-ended prompts stall him — a fact the skill had no way to honour, so both rules were overridden by hand and logged |
| 2026-08-16 | `bmad-prd` | Capability | Coached discovery earned its place. Offering concrete options to cut produced better output than either party alone: all three proposed domain shapes were rejected in favour of a user-authored Space model, and the same happened twice more (Status revert framing, rename cascade). The skill's real value was surfacing decisions — Status inheritance semantics, cascade-on-conflict — that would otherwise have first appeared as ambiguity during architecture |
| 2026-08-16 | `bmad-prd` | Capability | The memlog / addendum / PRD split held up under pressure. Mechanism decisions, rejected alternatives and audit trail each had a defined home, so the PRD stayed capability-level without losing the reasoning. Thirty-seven memlog entries reconstructed the whole session at Finalize with nothing lost |
| 2026-08-16 | Method-level | Limitation | Section numbering is fragile across the Adapt-In Menu. Pulling in optional template clusters shifted every section after §4, but §0's reading map — written before the clusters were added — silently kept the template's original numbering and pointed at the wrong sections. The skill has no self-consistency check for this, and it is a failure mode that would mislead exactly the downstream extraction workflows §0 exists to serve |
| 2026-08-16 | Method-level | Capability | Requiring a named carrier per property worked as designed. It converted an unfalsifiable claim into a testable one and produced an honest gap (P4) rather than a contrived carrier — the anti-pattern the brainstorm most wanted to avoid |
| 2026-08-16 | Environment | Note | Subagents were unavailable this session, so "extract, don't ingest", the parallel reviewer gate and per-input reconciliation all ran sequentially in the main context. Not a BMAD limitation, but it means the parallel-review path remains unexercised |

---

## Scoring your own progress

Rough milestones, in order of how much they'd tell you:

- **Bronze** — every *required* skill run once (PRD → architecture → epics → readiness → sprint → story cycle). Proves the happy path.
- **Silver** — Bronze, plus `correct-course` fired on a real mid-flight change (P6) and a retrospective produced findings you didn't already know.
- **Gold** — Silver, plus the TEA module exercised against falsifiable NFRs (P8), and a brownfield re-entry (P7) months after the code was written.
- **Platinum** — Gold, plus you found a limitation concrete enough that you built or customised a BMAD skill to address it.
