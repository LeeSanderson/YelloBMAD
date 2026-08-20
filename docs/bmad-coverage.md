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
| P5 | Substantial UI | The only thing that makes the UX phase more than a formality | **§4.8 Board and List Views + §7 Information Architecture.** FR-28–30, plus the Space context bar as a functional surface (authorisation context, not navigation), Space/Project settings, and NFR-9 (WCAG 2.1 AA, keyboard-operable Board). **Made concrete 2026-08-20** by `DESIGN.md` + `EXPERIENCE.md`: nine surfaces, nine journeys (UJ-9 added to close a surface with no journey), seven mockups, and a WCAG 2.5.1 Level A failure found and fixed before any code. The UX phase was not a formality — it produced two architecture escalations and a PRD amendment | 🟡 |
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
| `bmad-prd` — update | PRD | | ✅ | Exercised 2026-08-20 against a genuine cross-phase defect rather than a contrived one, on the same principle as the architecture update. **Capability:** the intent's *"surface conflicts with prior decisions before applying"* step did real work — it forced the SM-C4 collision into the open (adding a notification runs against a stated counter-metric) instead of letting it pass unnoticed, and the resolution now lives in the PRD as a *"Why this does not violate SM-C4"* note. SM-C4's wording turned out narrower than it reads: it guards volume added *to drive* SM-3/SM-4, and FR-43 drives no metric. **Capability:** the memlog/addendum/PRD split again had the right home for each part — the trap analysis to the memlog, the four rejected options to the addendum, only the requirement to the PRD. **Limitation:** FR-range staleness is the same class as the 2026-08-16 section-numbering finding and confirms it generalises. Three separate places restate the FR count (§0 reading map, §4 preamble, §9.1) and nothing checks them against each other or against the actual highest FR |
| `bmad-prd` — validate | PRD | | 🟡 | Rubric walker exercised inside the Finalize gate, not as a standalone Validate run. Caught two real defects (§0 reading map described template numbering; §0 pointed at the wrong section for the Assumptions Index). Synthesis pipeline and HTML report not exercised — Finalize deliberately keeps findings in-conversation |
| `bmad-ux` | CU | | ✅ | **Capability:** the *surface closure* rule — every stated need has a surface, every surface has a journey — converted an abstract IA into a falsifiable check and found a defect two prior phases had passed over. FR-8 permits an Ownership Offer to name any Role, PRD §7 scopes Space settings to Owner and Admin, so a Member named in an offer had nowhere to accept it; UJ-8 is exactly that case. The architecture run had confirmed the gap was real and handed it on rather than closing it — the UX phase is where it became answerable, and the answer (indicator on the Space context bar) needed no AD amendment. **Capability:** the two-spine split held under pressure. Measured at Finalize: across 165 `DESIGN.md` and 521 `EXPERIENCE.md` sentences of 8+ words, exactly **one** appears verbatim in both, and it is the intentional spine-wins header note. Every `{token}` reference resolved. **Limitation:** the reviewer gate has no ordering rule against the author's own work. The skill tells you to run Pass 1 coverage checks proactively *and* offers the gate at Finalize, with nothing sequencing them — so the three lenses read a moving target, cited stale line numbers, and re-reported findings already fixed, all needing manual reconciliation. **Limitation:** the skill mandates *"contrast targets stated for load-bearing combinations"* and provides no way to check the arithmetic. Eight of twelve hand-computed figures were wrong, and the two genuine AA failures sat in pairs the table never thought to state at all (accent-as-link against body text, 2.66:1; Role chip fill against its own ground, 1.05:1). The configured accessibility lens caught both; the rubric alone would not have. **Limitation:** `doc_standards` editorial reviews *propose* and do not apply, and `status: final` is a separate manual step — so a run can look complete while three steps short. Lee caught it by asking rather than assuming |

### Phase 3 — Solutioning

| Skill | Code | Req | Status | Findings — capability / limitation |
|-------|------|-----|--------|-------------------------------------|
| `bmad-architecture` | CA | ✔ | ✅ | **Capability:** the "show the load-bearing calls, don't make them silently" rule did real work — three recommendations were overridden (Blazor WASM over a TS client, Azure SQL over Postgres, SWA+Functions over one Container App) and the skill surfaced hard blockers instead of quietly building on them: Azure Functions cannot hold WebSockets, the SWA backend proxy explicitly refuses them, YDotNet ships no WASM target. **Capability:** the mandated web verification earned its keep twice — it caught a *critical* data-leak class (SESSION_CONTEXT under connection pooling + a documented parallel-plan defect returning another tenant's rows under RLS) and a defect in the skill's own draft (`Guid.CreateVersion7()` fragments a SQL Server index exactly like a v4). **Capability:** deferring the merge algorithm behind a port whose contract is a conformance suite is the spine philosophy working as designed — it converts an agent's judgement call into a test outcome. **Limitation:** `references/reviewer-gate.md` specifies parallel subagents precisely because "a fresh reviewer finds the divergences the author talks past"; run inline (session policy) it still found six issues including two of its own, but the independence property it names is lost and that result isn't guaranteed. **Limitation:** the spine template has no slot for *why* — by design, rationale lives in the memlog — so the spine alone is not reviewable by a human who wasn't in the session. The "something to review it by" artifact had to be built separately to make the decisions judgeable at all. **Limitation:** it never asked whether a `SPEC.md` existed before treating the PRD as its richest available input, and its Close step actively recommends running `bmad-spec` *afterwards* — the order that left FR-42 out of the spine. See [Sequencing defect](#sequencing-defect-bmad-spec-ran-after-bmad-architecture) below. **Update intent exercised twice** (2026-08-19 for the FR-42 gap, 2026-08-20 for the UX escalations); `AD` IDs stayed stable across both, amend-in-place worked as specified, and the deterministic `lint_spine.py` pass was clean each time. **Capability, now three for three:** the configured web-verification lens has caught a plausible-and-wrong SQL Server claim on every single run — `Guid.CreateVersion7()` fragmenting the index (create), the deferred-constraint claim behind the single-statement Owner swap (first update), and a position key declared *"lexicographically sortable"* with no collation while Azure SQL defaults to **case-insensitive**, under which `a0` and `A0` compare equal and the uniqueness constraint rejects the second as a duplicate (second update). Three for three is no longer a lens finding, it is a project rule: treat any SQL Server engine-behaviour assertion here as unverified until checked against Microsoft Learn. **Limitation:** a cross-phase escalation arrives as an assertion with no falsification attached. The UX run handed over a 403/404 *timing oracle* that does not actually hold — `spaceId` is always the first path segment, so the caller always supplies the Space and the refusal record fires on a condition they already know — and the only way to discover that was to re-derive it from AD-1, AD-3 and AD-20. The real finding was the *precondition* (the oracle appears the moment any route resolves a Space-scoped resource without `spaceId` in the path, which a share-a-link feature would introduce), which is a different and better fix. Nothing in either skill asks an escalation to carry its own test |
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
| `bmad-spec` | SP | ✅ | **Limitation (sequencing):** run *after* `bmad-architecture`, on the strength of that skill's own Close-step recommendation — which cost the spine FR-42 and three other resolutions. See [Sequencing defect](#sequencing-defect-bmad-spec-ran-after-bmad-architecture) above. **Limitation:** no installed downstream skill reads `SPEC.md`, so its output had to be hand-folded into `prd.md` to take effect at all. **Capability:** the open-question pass was genuinely load-bearing — Q1 uncovered a real trap where one Account could unilaterally block another's erasure, and closing it created CAP-42. **Capability:** deriving from `.memlog.md` rather than editing in place held up — the second run (`c0010fa`) re-rendered cleanly with capability IDs preserved |
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

### Sequencing defect: `bmad-spec` ran after `bmad-architecture`

**What happened.** The phases ran brainstorm → PRD → architecture → spec. Commit `b581f10` (2026-08-17 17:39) landed `prd.md`, `addendum.md` and an already-finalised `ARCHITECTURE-SPINE.md` together; `dee13ab` (2026-08-18 09:54) landed `specs/spec-yello/` the next morning, its own message recording the order as *"Initial output of /bmad-spec (following /bmad-brainstoring, /bmad-prd, and /bmad-architecture)"*. The spine's `sources:` are `prd.md`, `addendum.md` and this file. The spec is absent from them because it did not exist.

**Why it looked correct.** Both skills sanction it. `bmad-architecture`'s Close step says to *"lead with `bmad-spec`"* afterwards and to *"offer to invoke the `bmad-spec` skill to adopt the spine as a companion"* — which is exactly what happened, and `SPEC.md` duly lists `ARCHITECTURE-SPINE.md` under `companions:`. `bmad-spec` claims order-independence outright: deriving the contract from a living log *"lets the steps around the spec (PRD, UX, architecture, epics) run in any order and feed the same spec without merge drift."* Nothing in either skill objected at any point.

**Why it is a defect anyway — the order-independence is asymmetric.** The spec can absorb an input arriving at any time because `bmad-spec` re-derives `SPEC.md` from `.memlog.md` on every run: nothing is edited in place, so nothing drifts. The architecture has no equivalent guarantee. Its update path is amend-in-place with **frozen identifiers** — *"keep `AD` IDs stable — amend a Rule in place, add the next `AD-n` for a new decision, never renumber or reuse a retired ID"* — and downstream mechanism cites those IDs. A resolution reaching the spec is therefore free, while the same resolution reaching the spine costs a change to a `status: final` artifact. `bmad-spec` buys its order-independence by re-rendering; `bmad-architecture` cannot, so the "any order" claim quietly transfers the cost onto the one phase that has to pay it.

The same asymmetry shows up as two incompatible positions inside `bmad-architecture` itself: a spec package is *"the richest start and the spine's home"* (spec first), and also the thing to run next in order to adopt the spine as a companion (spec second). Running it second forfeits the first. The skill states both and never says which it means.

**What it cost, concretely.**

- **FR-42 is absent from the spine.** Resolving the spec's open questions on 2026-08-18 made ownership transfer an offer requiring consent, creating FR-42 / CAP-42. The spine's `scope:` still reads *"FR-1…FR-41"* and it mentions no Ownership Offer anywhere — no entity under Core entities, no 7-day expiry, no at-most-one-pending-per-Space constraint, no lapse-on-membership-end rule. AD-5 (Owner uniqueness as a filtered-index schema guarantee) and AD-22 were both shaped before the offer existed. A mechanism gap created purely by the ordering.
- **Three further resolutions landed on frozen mechanism.** NFR-2's revocation budget became two clauses (the very next request, plus 1 s on the live-session path) after AD-9 was written; FR-27's first-Status fallback became report-and-ask after AD-17 fixed Status removal as one atomic operation; FR-41 gained a bulk form after AD-17 fixed the Task move sitting alongside it.
- **The PRD already reports the failure mode in its own words.** §11 Q4: *"The revisit was to happen before the architecture was shaped around them and did not."* NFR-8's scale bounds hardened into AD-25 unverified, and revising one is now *"an architecture change, not a document edit."* Four PRD assumptions carry † for the same reason — they cost more than a document edit to reverse.
- **This file went stale from it.** Part 1 still cites *"NFR-2 (5s revocation)"* under P8 and *"40 FRs"* under P9. Both were written before the resolutions the spec produced. Left uncorrected, as evidence.
- **The spec ended up an orphan.** `SPEC.md` carries a consumer note recording that no installed BMad skill downstream of `bmad-architecture` reads it — `bmad-create-epics-and-stories`, `bmad-check-implementation-readiness`, `bmad-sprint-planning`, `bmad-create-story` and `bmad-dev-story` all discover requirements from the PRD. Every resolution therefore had to be hand-folded back into `prd.md` to reach implementation at all. The ordering cost twice over: the architecture did not get the spec's rigour going in, and the spec's own output could not reach the implementation chain coming out without manual copying.

**Type: limitation — the most expensive one found so far.** Not a user error. The documented flow produced it, and both detection points that should have caught it stayed silent: `bmad-architecture` never asked whether a spec existed before treating the PRD as its richest available input, and `bmad-spec` accepted a `status: final` spine as a peer `companion:` without flagging that a capability contract cannot bind mechanism already frozen.

**Resolution taken (2026-08-18):** report the divergence rather than silently repair it. `prd.md` is the live contract — it carries FR-1…FR-42 with all six open questions resolved — and `SPEC.md` is demoted in its own header to *"the reasoning record and audit trail, not the live contract."* The spine's FR-42 gap is real and outstanding; it is the first thing `bmad-architecture` update mode should be pointed at, and doing so exercises that intent against a genuine defect rather than a contrived one. Do **not** hand-patch the spine — that would forfeit the finding.

**For the next project:** run `bmad-spec` before `bmad-architecture` and let the spine adopt the spec, not the reverse. The Close-step recommendation to run `bmad-spec` afterwards is sound only as a *refresh* of a spec that already existed going in.

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
| 2026-08-18 | Method-level | Limitation | **Phase ordering — `bmad-spec` ran after `bmad-architecture`.** BMad's order-independence claim is asymmetric: the spec re-derives from its memlog and absorbs a late input for free, while the spine amends in place with frozen `AD` IDs and cannot. Running spec second put four resolutions onto finalised mechanism and left FR-42 absent from the spine entirely. Neither skill objected — `bmad-architecture`'s own Close step recommends the order that caused it. Full analysis at the end of Part 2 |
| 2026-08-18 | `bmad-architecture` / `bmad-spec` | Limitation | The two skills state incompatible positions for the spec. `bmad-architecture` calls a spec package "the richest start and the spine's home" *and* tells you to run `bmad-spec` afterwards to adopt the spine as a companion. Both claims sit in the same file, neither is marked as the default, and taking the second silently forfeits the first |
| 2026-08-18 | `bmad-spec` | Capability | The open-question pass earned its keep. Q1 surfaced a defect the PRD's wording permitted — chaining immediate ownership transfer with Owner-unremovable and delete-blocked-while-owning let one Account permanently block another's erasure. Consent-based transfer (CAP-42) closed it at the root. A capability contract catching a rights defect that both the PRD and the architecture passed over is the strongest argument for running it *first* |
| 2026-08-18 | `bmad-spec` | Limitation | `SPEC.md` is read by no installed downstream skill — the entire implementation chain discovers requirements from the PRD. Its resolutions had to be hand-copied into `prd.md` to take effect, and `SPEC.md` now opens with a header demoting itself to an audit trail. A canonical contract nothing consumes is a contract in name only |
| 2026-08-20 | `bmad-ux` | Capability | **Surface closure earned its place.** Requiring that every stated need has a surface *and* every surface has a journey turned the IA into a falsifiable check, and it caught a defect the PRD and the architecture had both passed over: a Member named in an Ownership Offer had no surface to accept on. The architecture run had already confirmed the gap was real and handed it forward, so this is the clearest evidence yet that the phases catch different classes of defect rather than re-checking each other |
| 2026-08-20 | `bmad-ux` | Limitation | **The reviewer gate has no ordering rule against the author's own fix pass.** The skill instructs a proactive Pass 1 self-check and also offers the gate at Finalize, with nothing sequencing the two. Run concurrently, the three lenses reviewed a moving target — stale line numbers, re-reported fixed findings, and a manual reconciliation pass to tell resolved from open. Dispatch reviewers *before* fixing, or fix *before* dispatching; the skill implies neither |
| 2026-08-20 | `bmad-ux` | Limitation | **Stated-but-unverifiable numbers.** The rubric requires contrast targets for load-bearing combinations and gives no way to check them. Eight of twelve hand-computed ratios were wrong (all in the safe direction), and the two real AA failures were in pairs the table never thought to state — an accent link against body text at 2.66:1, and a Role chip fill invisible against its own ground at 1.05:1. A rubric that mandates a number without mandating its derivation invites exactly this |
| 2026-08-20 | Method-level | Limitation | **Finalize steps that produce recommendations are easy to mistake for finished.** `doc_standards` editorial reviews *propose* and do not apply, and `status: final` is a separate manual flag — so a run can present as complete while carrying unapplied recommendations, an unrun review and a `draft` artifact. Caught only because Lee asked whether it was done rather than assuming. The phases read as narrative but function as gates |
| 2026-08-20 | `bmad-architecture` | Capability | **The web-verification lens is three for three.** Every run of this spine has produced a plausible-and-wrong SQL Server claim, and the same configured lens has caught each one: `Guid.CreateVersion7()` index fragmentation, the false deferred-constraint premise behind the single-statement Owner swap, and a fractional-index key with no collation against an Azure SQL default of case-insensitive. Three for three promotes it from a review lens to a project rule |
| 2026-08-20 | Method-level | Limitation | **A cross-phase escalation arrives as an assertion, not a test.** The UX run escalated a 403/404 timing oracle that does not hold under the spine's own routing convention, and finding that out required re-deriving it from three ADs. The real defect was the *precondition* — the oracle appears the moment a route resolves a Space-scoped resource without `spaceId` in the path — which is a different fix from the one escalated. Nothing in either skill asks a hand-off to carry its own falsification, so the receiving phase either trusts it or redoes the analysis |

---

## Scoring your own progress

Rough milestones, in order of how much they'd tell you:

- **Bronze** — every *required* skill run once (PRD → architecture → epics → readiness → sprint → story cycle). Proves the happy path.
- **Silver** — Bronze, plus `correct-course` fired on a real mid-flight change (P6) and a retrospective produced findings you didn't already know.
- **Gold** — Silver, plus the TEA module exercised against falsifiable NFRs (P8), and a brownfield re-entry (P7) months after the code was written.
- **Platinum** — Gold, plus you found a limitation concrete enough that you built or customised a BMAD skill to address it.
