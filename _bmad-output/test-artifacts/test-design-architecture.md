---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-08-22'
workflowType: 'testarch-test-design'
mode: 'system-level'
inputDocuments:
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/addendum.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/implementation-artifacts/sprint-status.yaml'
  - '_bmad/tea/config.yaml'
---

# Test Design for Architecture: Yello v1 (system-level)

**Purpose:** Architectural concerns, testability gaps and NFR requirements arising from
the Yello architecture spine. Serves as the contract between test design and
implementation on what must be settled before the corresponding tests can be written.

**Date:** 2026-08-22
**Author:** Murat (Master Test Architect), for Lee
**Status:** Architecture Review Pending
**Project:** YelloBMAD
**PRD Reference:** `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md` (status: final)
**ADR Reference:** `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md` — AD-1 … AD-29 (status: final)

---

## Executive Summary

**Scope:** Yello v1 in full — FR-1…FR-43, NFR-1…NFR-9, 8 epics, 53 stories. This is a
system-level design produced at the boundary between planning and implementation: sprint
planning is complete, no story files exist, and no code or tests exist in the repository.
There is no baseline coverage to extend and no flaky history to inherit.

**Product context** (from PRD):

- **The bet:** that a Space is a single primitive serving a private notebook, a client
  engagement and a company's shared work. §10's SM-3 (proportion of Accounts in two or
  more Spaces) is the number that tells you whether the bet paid off. There is no revenue
  model in v1 (§9.2 rules out billing).
- **The problem:** authorisation is a function of `(Account, Space)`, never of Account
  alone. Nothing is expressible as "an Admin can X" without naming the Space.
- **Timeline:** epic-sequenced, no calendar date set. Epic 1 is startable now.

**Architecture — the decisions that shape testing most:**

- **AD-2:** Space scoping is enforced *in the database* by row-level security on
  `SESSION_CONTEXT('SpaceId')`, with EF Core global query filters as an independent second
  layer. `MAXDOP = 1`. Neither layer alone carries NFR-1.
- **AD-8/AD-9:** the sync channel carries no authority. Every inbound frame is checked
  against a lease held until invalidated by an in-process push at the transaction boundary
  — no TTL, no polling.
- **AD-29:** Board and List View are keyset-paginated on the AD-15 position key, and
  rendered rows are **appended and never recycled**, because a recycled row re-points
  keyboard focus at a different Task.
- **AD-12:** the merge port's contract *is* an executable conformance suite, written
  before any implementation.
- **AD-21:** the dependency rule is a build gate (ArchUnitNET), not a convention.
- **Stack:** .NET 10.0.11, Blazor WebAssembly, EF Core 10, Azure SQL Database (serverless,
  free offer), Container Apps (max 1 replica), Static Web Apps. Test tooling already
  pinned: xunit.v3 4.0.0, Testcontainers.XunitV3 4.6.0 against
  `mcr.microsoft.com/mssql/server:2025-latest`, TngTech.ArchUnitNET 0.13.3. The in-memory
  EF provider is **forbidden** — it cannot exercise RLS.

**Expected scale** (NFR-8, confirmed final for v1):

| Dimension | Bound |
| --- | --- |
| Spaces per Account | 50 |
| Memberships per Space | 100 |
| Projects per Space | 50 |
| Tasks per Project | **5,000** |
| Concurrent editors per Task | 10 |
| Concurrent active Sessions per Space | 50 |

**Risk summary:**

- **Total risks:** 18
- **Score 9 (BLOCK):** 1 — R1, the FR-34 interleaving seam
- **Score ≥6 (MITIGATE):** 10
- **Score 4–5 (MONITOR):** 5 · **Score ≤3 (DOCUMENT):** 2
- **Test effort:** ~235 scenario classes (~330 tests once the isolation suite doubles
  across surfaces), **~160–245 hours** of test construction spread across eight epics

---

## Quick Guide

### 🚨 BLOCKERS — must be decided before the dependent tests can be written

These are not "nice to settle". Each one blocks a specific suite, and each is far cheaper
now than after the story that consumes it has shipped.

1. **B1 — The FR-34 interleaving seam** (risk R1, score **9**). Nothing lets a test hold a
   sync frame in flight across the removal transaction's commit, so the assertions SM-2
   gates release on cannot be made deterministically. *Owner: story 7.3 (the sync channel),
   before 7.7/7.8 consume it.*
2. **B2 — The RLS seeding strategy** (concern T4). Every isolation test needs two Spaces
   populated, but AD-2 filters every Space-scoped table on session context and forbids raw
   SQL outside `Infrastructure`. Three shapes are possible and the choice is architectural,
   not a fixture detail. *Owner: story 1.5 (session wiring), consumed by 1.9.*
3. **B3 — The duration-indistinguishability method** (risk R2, score 6). AD-3 and AD-23
   both require identical *duration*, and neither states a sample size, statistic,
   tolerance or measurement point. Without one, the test is a single draw from two
   distributions and detects nothing. *Owner: stories 1.3 and 1.6.*
4. **B4 — Warm or cold for NFR-5** (risk R3, score 6, spine deferral AR-40b). "Measured
   warm" and "measured cold" are different harnesses, not different numbers. The test
   design cannot proceed on NFR-5 until this is answered. *Owner: epic 2's first
   measurement (story 2.9 / 1.10).*
5. **B5 — The browser-test binding** (risk R16, concern T9). Two AD-29 invariants —
   append-never-recycle and `aria-setsize`/`aria-posinset` agreeing with the visible count
   — exist only in the rendered client and are unreachable from xUnit or ArchUnit. Whether
   that is Playwright for .NET or a separate TypeScript project decides whether they are
   tested at all. *Owner: the framework run (TF), consumed by stories 1.2, 2.8, 2.9.*
6. **B6 — The isolation suite's case list** (concern T6). AD-4 requires every case on both
   surfaces and calls a one-surface case a gap; nothing enumerates the cases. SM-1 gates
   release on a suite whose membership is undefined, so it can be "complete" at any size.
   *Owner: story 1.9 — hold the list as data and generate the cross-product.*

**What is needed:** decisions on all six. B2, B3 and B6 land inside epic 1 and are the
ones that gate the largest suite in the product.

---

### ⚠️ HIGH PRIORITY — recommendation provided, confirmation wanted

1. **R5 + R3 interaction: do not let latency work reopen the tenancy defect.** The cold
   start budget (R3) creates direct pressure to relax `MAXDOP = 1`, which is the mitigation
   suppressing a documented defect where a parallel plan reading `SESSION_CONTEXT()` on a
   pool-reset session returns **another tenant's rows, silently and successfully** (R5).
   Recommendation: treat `MAXDOP = 1` as unrelaxable until Azure SQL's exposure is
   confirmed *and* the pooled-connection isolation case is green. Neither deferral records
   this interaction today. *Confirm before story 1.10.*
2. **R4: measure the Board at 5,000, not at a sample.** The mechanism protecting keyboard
   users from focus corruption (append, never recycle) is the same mechanism that grows the
   Blazor WASM render tree without bound. Recommendation: story 2.9 measures at the real
   bound; if append-only cannot hold it, the escalation is a focus-identity-preserving
   windowing scheme, which is an AD-29 amendment rather than an implementation choice.
3. **R7: assert the collation in a schema test, not only in the migration.** Omitting
   `COLLATE Latin1_General_100_BIN2` makes `a0` and `A0` compare equal under Azure SQL's
   case-insensitive default, and `ALTER DATABASE … COLLATE` is unsupported on Azure SQL.
   Recommendation: assert it on the key column *and* the `(ProjectId, StatusId, PositionKey)`
   index, with a mixed-case ordering case.
4. **R6: make the merge suite falsifiable.** Recommendation: write it in story 7.1 citing
   FR-31/FR-33/NFR-4 clause by clause with no candidate algorithm in the repository, and
   assert that whole-field last-writer-wins **fails** it. That assertion is what stops the
   suite being written to whatever was chosen.
5. **R9: the ownership swap's ordering is correct in prose and unenforced in code.** AD-26
   forbids tracked-entity `SaveChanges` because EF picks its own statement order.
   Recommendation: an invariant test on never-zero-or-two-Owners, a concurrency case on one
   offer, and an ArchUnit rule scoped to the `AcceptOwnershipOffer` slice.
6. **R10: tag every test whose expectation traces to a PRD §12 assumption.** Thirteen
   remain unconfirmed and four have already hardened into architecture. Tests make an
   assumption permanent by making its reversal a test failure; tagging turns that into a
   known set rather than a hunt.
7. **R11: give the §6.4 gate a tripwire.** "The first Account created by anyone other than
   the operator makes this document non-compliant" is stated as testable and nothing counts
   Accounts. Must be operator-side, since §6.1 forbids an in-product cross-Space aggregate.

---

### 📋 INFO ONLY — solutions provided, no decision needed

1. **Test strategy:** five levels mapped to this stack — UNIT (xunit.v3), INT (xunit.v3 +
   Testcontainers + `WebApplicationFactory`, both calling surfaces), ARCH (ArchUnitNET +
   schema tests), E2E (browser, binding per B5), LOAD (k6 or equivalent, local target).
   The Role matrix is tested at INT and never at E2E; the merge algorithm at UNIT; only
   convergence-as-experienced at E2E.
2. **Suite structure:** already fixed by the spine — `Yello.Tests.Isolation`, `.Revocation`,
   `.Merge`, `.Architecture`, `.Slices`, gating on SM-1, SM-2, AD-12 and AD-21 respectively.
3. **Tiered execution:** PR (< 15 min: ARCH, contrast harness, merge conformance, slices,
   isolation, revocation, contract) → Nightly (accessibility E2E, focus-identity paging,
   Board at 5,000, property-based merge interleaving) → Weekly (LOAD at the full envelope,
   cold-start characterisation), local target by default.
4. **Coverage:** ~235 scenario classes prioritised P0–P3. P0 dominates at ~215, which is
   what "isolation is absolute with no acceptable failure rate" costs rather than scoring
   inflation.
5. **Quality gates:** P0 100%, P1 ≥95%, and three gates that are not percentages — a single
   verified cross-Space disclosure blocks release (SM-1), revocation in 100% of tested cases
   (SM-2), and the architecture suite failing the build rather than the review.

---

## For Architects and Devs — Open Topics 👷

### Risk Assessment

**Total risks identified:** 18 — 1 critical (score 9), 10 high (6–8), 5 medium (4–5),
2 low (≤3).

Scored probability (1 unlikely / 2 possible / 3 likely) × impact (1 minor / 2 degraded /
3 critical), per `probability-impact.md`.

#### Critical (Score 9) — BLOCKS

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner | Timeline |
| --- | --- | --- | :-: | :-: | :-: | --- | --- | --- |
| **R1** | **TECH** | **FR-34's interleaving cannot be tested deterministically.** No seam holds a sync frame across the removal transaction's commit. SM-2 gates release on this in 100% of cases, so the likely outcome is a `Task.Delay`-shaped test that passes vacuously on the requirement the PRD says the product should be judged on | 3 | 3 | **9** | Specify the interleaving seam; 7.3 is not done until a test can force both orderings | Story 7.3 | Before 7.7/7.8 |

#### High Priority (Score 6–8) — IMMEDIATE ATTENTION

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner | Timeline |
| --- | --- | --- | :-: | :-: | :-: | --- | --- | --- |
| **R2** | **SEC** | Timing oracles ship behind vacuous tests — AD-3 and AD-23 demand duration-indistinguishability with no method stated | 2 | 3 | **6** | State N samples per arm, distribution comparison, server-side measurement, variance-derived tolerance | Stories 1.3, 1.6 | Epic 1 |
| **R3** | **PERF** | NFR-5's 300 ms p95 read budget missed by an order of magnitude on cold requests — scale-to-zero + 15-min auto-pause on 0.5 vCore with `MAXDOP = 1` | 3 | 2 | **6** | Close AR-40b: pin min replicas to 1 (~£12–15/mo, inside the ceiling) **or** state NFR-5 as measured warm and exempt the cold path. Do not leave it silent | Story 2.9 / 1.10 | Epic 2 first measurement |
| **R4** | **PERF** | The Board's protection against focus corruption is also an unbounded DOM — AD-29 forbids virtualisation and requires append-never-recycle at a 5,000-Task bound that is final | 2 | 3 | **6** | Measure at the real bound in story 2.9. Escalation is focus-preserving windowing = an AD-29 amendment | Story 2.9 | Epic 2 |
| **R5** | **DATA** | The `SESSION_CONTEXT` parallel-plan defect (AR-40c) — documented across SQL Server 2019 CU14–CU31, 2022 CU1–CU23, 2025 RTM–CU2; a parallel plan on a pool-reset session can return another tenant's rows silently. R3 creates pressure to relax the mitigation | 2 | 3 | **6** | Confirm Azure SQL's status before first production deploy. Relax `MAXDOP = 1` only with the pooled-connection case green. Trace flag 11042 unavailable on Azure SQL | Story 1.10 | Before first deploy |
| **R6** | **TECH** | The merge conformance suite gets written to the algorithm instead of the requirements — AD-12 requires it first, the algorithm is deferred (AR-40a) | 2 | 3 | **6** | Write in 7.1 citing FR-31/FR-33/NFR-4 with no candidate in the repo; assert LWW fails the suite | Story 7.1 | Before epic 7 |
| **R7** | **DATA** | AD-15's collation is a migration detail with irreversible consequences — case-insensitive default makes mixed-case base62 keys collide; not fixable server-wide on Azure SQL | 2 | 3 | **6** | Assert collation in the schema test on the column *and* the AD-29 index; mixed-case ordering case | Story 2.6 / 1.1 | Epic 2 |
| **R8** | **SEC** | CORS and anti-forgery are one misconfiguration from a cross-site hole — `SameSite=None` has already removed the implicit protection | 2 | 3 | **6** | Negative tests: wildcard rejected, reflected `Origin` rejected, state change without token refused, no credential in web storage | Story 1.4 | Epic 1 |
| **R9** | **DATA** | The ownership swap's ordering is correct in prose and unenforced in code — EF picks its own statement order for two tracked rows | 2 | 3 | **6** | Invariant test on never zero-or-two Owners; concurrency case; ArchUnit rule on the slice | Story 5.2 | Epic 5 |
| **R10** | **BUS** | Thirteen unconfirmed PRD §12 assumptions become acceptance criteria; four already hardened into architecture | 3 | 2 | **6** | Tag every test tracing to a §12 assumption so reversal is a known set | All stories | Ongoing |
| **R11** | **SEC** | The §6.4 data-protection gate has no automated tripwire — nothing counts Accounts | 2 | 3 | **6** | Story 1.10 asserts the single-operator position; make the condition observable operator-side | Story 1.10 | Epic 1 |

#### Medium Priority (Score 4–5)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner |
| --- | --- | --- | :-: | :-: | :-: | --- | --- |
| R13 | OPS | A load test at NFR-8 scale can exhaust the 100,000 vCore-s free grant, triggering auto-pause until next month by configuration | 2 | 2 | 4 | Load-test locally against Testcontainers; reserve Azure for one costed window | Story 2.9 |
| R14 | DATA | Compaction can destroy SM-5 irrecoverably — AD-13 permits replacing a log prefix without saying what survives | 2 | 2 | 4 | Close the AD-13 obligation before compaction; test that per-author counts and timestamps survive | Epic 7 |
| R15 | PERF | NFR-3's 16 ms local render is tight for Blazor WASM — one frame at 60 Hz through the render-tree diff plus a local CRDT apply | 2 | 2 | 4 | Measure early in story 7.4; the lever is editor render granularity, not the merge algorithm | Story 7.4 |
| R16 | TECH | The client's two AD-29 invariants have no test level, and the `tea_use_playwright_utils` mismatch decides whether they get one | 2 | 2 | 4 | Decide the binding at the framework run; stories 2.8 and 2.9 consume it | TF run |
| R17 | TECH | The isolation suite's completeness is unfalsifiable while "every case on both surfaces" has no enumerated list | 2 | 2 | 4 | Hold the case list as data in story 1.9; generate the cross-product | Story 1.9 |

#### Low Priority (Score ≤3)

| Risk ID | Category | Description | P | I | Score | Action |
| --- | --- | --- | :-: | :-: | :-: | --- |
| R12 | TECH | Expiry is only reachable by seeding the past (AD-27 evaluates against the database clock inside the guarded `WHERE`), and AD-27's ArchUnit rule is an assertion about absence | 3 | 1 | 3 | Document — state the seeding convention once in story 5.3; give the rule a concrete predicate |
| R18 | OPS | NFR-5 has no production evidence source — no RED metrics, no `/metrics`, and AD-10 forbids the timer-driven collection that would be the usual answer | 2 | 1 | 2 | Monitor — record that p95 is a load-test figure, not a monitored one |

#### Risk Category Legend

- **TECH**: Technical/Architecture (flaws, integration, scalability)
- **SEC**: Security (access controls, auth, data exposure)
- **PERF**: Performance (SLA violations, degradation, resource limits)
- **DATA**: Data Integrity (loss, corruption, inconsistency)
- **BUS**: Business Impact (UX harm, logic errors, revenue)
- **OPS**: Operations (deployment, config, monitoring)

**On the distribution.** Ten risks at exactly 6 is not scoring inflation. It reflects a
design where an unusual number of decisions are individually load-bearing and explicitly
irreversible — a collation that cannot be altered after the fact, a scale bound confirmed
final, a requirement stated as having no acceptable failure rate. What distinguishes R1 is
not that its consequence is worse than R5's or R7's, but that its **probability is 3**: the
other critical risks already have a mitigation sitting in the architecture, and R1 has none.

#### Residual risk after mitigation

What remains once every mitigation above is executed as planned. Stated so it is accepted
deliberately rather than assumed away.

| Residual | Why it cannot be driven to zero | Accepted position |
| --- | --- | --- |
| **NFR-1 completeness** | The isolation suite can only be as complete as its enumerated case list. Even a perfectly generated cross-product tests the cases someone thought of; NFR-1 is a claim about *all* routes | Accepted. The mitigation is enumeration held as data plus two independent enforcement layers, so a missed case still meets RLS *and* the EF filter. This is why the two-layer design matters more than the suite's size |
| **NFR-5 under real traffic** | With no RED metrics and AD-10 forbidding timer-driven collection, p95 is a load-test figure measured once (R18) | Accepted for single-operator v1. The standing rule is that this number is never cited as production evidence |
| **AR-40c / Azure SQL exposure** | Whether Azure SQL Database is affected at all is not established by the vendor, and trace flag 11042 is unavailable there | Accepted while `MAXDOP = 1` holds. The residual is entirely in whether someone later relaxes it — which is why the R3 interaction must be written into both deferrals |
| **The 13 unconfirmed assumptions** | Confirmation is a product decision, not a test outcome | Accepted with tagging (R10), so reversal is a bounded edit rather than an archaeology exercise |
| **NFR-8 bounds themselves** | Set by judgement, not measurement, and confirmed final before any usage evidence could exist | Accepted. Verification is scheduled at the NFR-evidence audit; revising a bound after that is an architecture change |
| **Merge algorithm behaviour outside the conformance suite** | A CRDT's correctness is a property of all interleavings; a suite plus a property harness samples them | Accepted. The property-based harness (M-9) is the mitigation that makes the sample large and unbiased rather than hand-picked |

---

### NFR Testability Requirements

**Purpose:** what the architecture must provide so NFR validation can be automated. Planning
guidance, not final evidence assessment.

| NFR Category | Threshold / Requirement | Current Design Support | Gap / Decision Needed | Planned Evidence |
| --- | --- | --- | --- | --- |
| **Security — isolation** | NFR-1: zero cross-Space disclosure, both surfaces, no acceptable failure rate | **Strong** — two independent layers (RLS + EF global filters), `{spaceId}` on every route enforced by an architecture test, composite FKs carrying `SpaceId` | Case list undefined (B6); seeding strategy undefined (B2); sync is a third ingress not covered by AD-4's two-surface rule | Isolation suite report; `AccessRefusal` row assertions |
| **Security — disclosure** | AD-3, AD-23: identical body **and duration** | Partial — bodies are structurally identical by design; duration is asserted nowhere | Statistical method (B3) | Distribution comparison, server-side |
| **Security — credentials** | NFR-6: slow one-way hash tunable without re-registration; Tokens stored unusable, shown once | Supported | **Work factor never chosen** — "the architecture's call", not made | Story 1.3/1.4 assertions |
| **Security — cross-origin** | AD-7: exact origin, credentials, anti-forgery on every state change | Supported | Negative tests not yet specified (R8) | Four negative INT tests |
| **Performance** | NFR-5: 300 ms read / 500 ms write p95 server-side within the NFR-8 envelope | Partial — keyset pagination makes page cost depth-independent | **Warm vs cold undecided** (B4/AR-40b) — the most consequential open threshold | k6 summary JSON, p95 per operation class |
| **Performance — client** | NFR-3: 16 ms local, 300 ms p95 remote at 50 ms RTT | Partial — AD-11 forbids blocking on the network (the easy half) | RTT simulation method unknown; 16 ms in Blazor WASM unproven (R15) | Browser measurement |
| **Scalability** | NFR-8: six bounds, refusal not wrong answer | **Strong** — AD-25 declares all six in one registry checked by the pipeline | "Active Session" undefined for enforcement | Bound registry test + refusal case per bound |
| **Reliability — revocation** | NFR-2: next request (no tolerance); 1 s live session | **Strong** — per-request resolution, in-process push at the transaction boundary, single replica | **No interleaving seam** (B1/R1) | Revocation suite report |
| **Reliability — convergence** | NFR-4: 2 s, 10 editors, 5-min reconnect | Strong — append-only log, in-transaction projection, one merge port | Algorithm deferred (AR-40a); falsification assertion needed (R6) | Merge conformance report |
| **Accessibility** | NFR-9: WCAG 2.1 AA on five flows; keyboard parity for every Board pointer operation; ARIA live announcements | Partial — DESIGN.md computes all contrast ratios; AD-29 forbids the virtualiser that would break focus | **No client test level** (B5/T9) | 36 computed contrast ratios; axe reports; keyboard parity matrix |
| **Maintainability** | No target stated anywhere in PRD, spine or epics | ArchUnitNET gates the paradigm | Coverage % is a proposal, not a requirement — see the QA doc's gates | CI coverage job |
| **Observability** | NFR-7: refusal recorded with acting Account, target Space, capability, outcome, `CrossSpace`/`InsufficientRole`; 90 days | **Strong** — AD-20 writes it in the pipeline, and the `kind` discriminator is exactly NFR-7's distinction, already materialised | None | `AccessRefusal` assertions |
| **Operations** | AD-10: nothing touches the database on an unconditional timer; §6.3 £30/month | Strong — probes answer from process state, outbox piggybacks on traffic | **No RED metrics** (R18), so NFR-5 has no production evidence source | Load-test figure only |
| **Availability / DR** | — | — | **No uptime target, no RTO, no RPO stated anywhere.** The free offer's 7-day PITR and locally redundant backup are what it gives, not an objective anyone chose | None |

**Unknown thresholds** — carried as clarification items, never guessed: NFR-5 warm vs cold ·
NFR-6 password work factor · FR-38 rate-limit values · NFR-3's 50 ms RTT simulation method ·
the definition of an "active Session" for NFR-8 · availability target · RTO/RPO · code
coverage target.

**Assessment boundary:** final PASS/CONCERNS/FAIL belongs to `bmad-testarch-nfr` once
implementation evidence exists. The PRD already schedules NFR-8's verification there (§11 Q4).

---

### Testability Concerns and Architectural Gaps

#### 1. Blockers to Fast Feedback — what is needed from the architecture

| Concern | Impact on testing | What must be provided | Owner | Timeline |
| --- | --- | --- | --- | --- |
| **T1 — No seam to interleave revocation with an in-flight frame** | The SM-2 assertions become `Task.Delay`-shaped: flaky, or vacuously green on the one requirement that must not be wrong | A deterministic ordering point around lease invalidation — a test-only barrier the frame handler awaits, or an injectable interleaving hook | Story 7.3 | Before 7.7/7.8 |
| **T4 — RLS makes multi-Space seeding an architectural choice** | Every isolation test needs two populated Spaces; seeding Space B under Space A's session context writes nothing visible | One of: seed through the application per Space with the context switched; seed as a principal the RLS policy exempts; seed before policies exist via migration ordering. Pick one and state it | Story 1.5 | Consumed by 1.9 |
| **T3 — No method for duration-indistinguishability** | A single-sample assertion tests one draw from two distributions and detects no real oracle | Sample size, statistic, tolerance and measurement point. `MAXDOP = 1` plus a single replica make variance unusually low, so this is *more* tractable here than most places — but only if written down once instead of improvised twice | Stories 1.3, 1.6 | Epic 1 |
| **T6 — "Every case on both surfaces" has no case list** | SM-1's claim is unfalsifiable; the suite is complete by construction at any size | The case list held as **data**, with the framework generating the surface cross-product so a missing pair fails | Story 1.9 | Epic 1 |
| **T7 — Cold start is uncontrollable and decides whether NFR-5 passes** | Warm and cold are different harnesses. An Azure SQL serverless resume is seconds, against a 300 ms budget | The AR-40b decision, stated rather than left silent | Story 2.9 / 1.10 | Epic 2 |
| **T9 — The Blazor client has no named test level** | Two AD-29 invariants are unreachable from every suite the spine names — and one of them is "a data-corrupting defect reachable only by keyboard, invisible to pointer testing" | The browser-test binding decision, and a sixth test project | TF run | Before story 2.8 |

#### 2. Architectural Improvements Needed

1. **Give AD-27's architecture rule a concrete predicate.**
   - *Current problem:* AD-27 requires the architecture suite to fail the build on "a
     scheduled component writing a terminal expiry state". Absence is what ArchUnit
     expresses least well, and "scheduled component" is not a type.
   - *Required change:* define it as a type implementing `IHostedService`/`BackgroundService`
     that writes `OwnershipOffer.State` or `Invitation.State`.
   - *Impact if not fixed:* the rule is unimplementable and quietly omitted, and AD-27
     reverts from a build gate to a convention.
   - *Owner:* story 5.3. *Timeline:* epic 5.

2. **Isolate the pooled-connection reuse case from test parallelism.**
   - *Current problem:* AD-2 requires two requests for different Spaces served consecutively
     on one physical connection. Reproducing it means pinning pool size to 1 and forcing
     reuse — the opposite of what xUnit v3's parallelism wants.
   - *Required change:* its own test collection with parallelism disabled and a
     collection-scoped connection string.
   - *Impact if not fixed:* intermittent cross-test interference, and the case most likely
     to be quarantined is the one guarding a silent tenancy leak.
   - *Owner:* story 1.9. *Timeline:* epic 1.

3. **Decide whether `/sync` is a third surface for isolation purposes.**
   - *Current problem:* AD-4 names browser and API Token and calls a one-surface case a gap.
     The sync channel is a third ingress, authorised per frame under AD-8, and the isolation
     suite is not required to run its cases there. Cases like "can a frame reach a Task in a
     foreign Space?" currently fall between the isolation and revocation suites.
   - *Required change:* state explicitly whether the isolation cross-product is two surfaces
     or three.
   - *Impact if not fixed:* a gap that looks like a boundary between two suites.
   - *Owner:* story 1.9. *Timeline:* epic 1.

4. **Close AD-13's compaction obligation before compaction exists.**
   - *Current problem:* AD-13 permits replacing a log prefix with a snapshot without stating
     what survives. The addendum requires per-author change counts and timestamps to persist
     or SM-5 becomes underivable — and once compacted, unrecoverably so.
   - *Required change:* amend AD-13 to name what compaction preserves.
   - *Impact if not fixed:* a behavioural metric is silently destroyed by an optimisation.
   - *Owner:* epic 7. *Timeline:* before compaction is implemented.

---

### Testability Assessment Summary

**📊 Current state — FYI.** This architecture is markedly more testable than most at this
stage, and several of these properties are what make the coverage plan affordable at all.

#### What works well

Five properties do most of the work, and the coverage plan's affordability rests on them:

- **`AccessRefusal` is written by the pipeline for every 403 and boundary 404** (AD-20) with a
  `CrossSpace` / `InsufficientRole` discriminator. SM-1 and NFR-7 get a deterministic table to
  assert against, and NFR-7's required distinction is already materialised.
- **Two independent isolation layers** (AD-2). Neither RLS nor the EF global filters alone
  carries NFR-1, so a test can disable one and assert the other still holds — the invariant is
  falsifiable layer by layer, which is rare.
- **Real SQL Server via Testcontainers, in-memory provider forbidden.** RLS is genuinely
  exercised; the ban is what keeps the isolation suite from being decorative. `MAXDOP = 1` and
  a single replica then remove plan-shape and replica-timing nondeterminism, which is what
  makes T3's timing assertions tractable at all.
- **Two invariants hold by construction rather than by test.** Composite FKs carrying `SpaceId`
  make FR-21's same-Space Assignee constraint inexpressible to violate, and `{spaceId}` on
  every route (AD-3) removes the bare-id deep-link case entirely. Both *delete* test classes
  instead of adding them.
- **Contracts that precede their implementations.** The merge port's conformance suite is
  written before any algorithm (AD-12); the bound registry declares all six bounds at one
  choke point (AD-25); ArchUnitNET makes the dependency rule a build failure (AD-21). Each is
  a gate rather than a convention, and AD-4's single pipeline means the both-surfaces
  requirement is a test parameter, not a second suite.

#### Accepted trade-offs (no action required)

- **No RED metrics or `/metrics` endpoint** — AD-10 forbids the timer-driven collection that
  would be the usual answer, and this is defensible for a single-operator deployment. The
  consequence is that NFR-5's p95 is a load-test figure and will never be a monitored one.
- **No staging environment** — a second Azure environment would consume the same free grants
  that make production free. Local and Azure are the only two.
- **Encryption at rest not asserted, and no trash or restore** — both explicitly deferred
  (§6.4, §6.2), not forgotten. Irreversible deletion raises the value of the atomicity cases.
- **Expiry only reachable by seeding the past** — the price of AD-27 evaluating against the
  database clock, which is the right call for correctness.

---

### Risk Mitigation Plans (High-Priority Risks ≥6)

#### R1: FR-34's interleaving cannot be tested deterministically (Score 9) — CRITICAL / BLOCKING

**Mitigation strategy:**

1. In story 7.3, define an interleaving seam in the sync frame handler: a test-only
   asynchronous barrier, or an injectable ordering point invoked immediately before the lease
   check and immediately after lease invalidation.
2. Express the requirement as two tests that must *both* pass, forcing opposite orderings
   rather than observing whichever happens: frame-admitted-then-revoked (change retained) and
   revoked-then-frame-arrives (change discarded, not persisted, connection closed with an
   access-ended reason).
3. Add a third case for the retry path — a frame delayed and retried after invalidation is
   refused, which AD-9 requires and which no timing-based test reaches reliably.
4. Make story 7.3's definition of done include "a test can force both orderings", so 7.7 and
   7.8 inherit a controllable seam rather than building on a race.

**Owner:** story 7.3 · **Timeline:** before stories 7.7 / 7.8 · **Status:** Planned
**Verification:** run the two ordering tests 100 times in CI (burn-in). A seam that works
produces 100 identical results; a `Task.Delay` produces a distribution.

#### R2: Timing oracles ship behind vacuous tests (Score 6)

**Mitigation strategy:**

1. Write the method once, before stories 1.3 and 1.6, covering both AD-3 and AD-23.
2. Take the measurement **server-side** so network jitter is not the dominant term.
3. Collect N samples per arm (N ≥ 100 as a starting point), compare distributions rather than
   means, and derive the tolerance from observed within-arm variance rather than fixing a
   millisecond constant.
4. Assert the *absence of a signal*: the two arms must be statistically indistinguishable, and
   the test should be validated against a deliberately introduced oracle to prove it can fail.

**Owner:** stories 1.3, 1.6 · **Timeline:** epic 1 · **Status:** Planned
**Verification:** introduce a known timing difference (skip the password hash for an unknown
address) and confirm the test fails. A test that cannot detect a planted oracle is not
testing for one.

#### R3: NFR-5's read budget on cold requests (Score 6)

**Mitigation strategy:**

1. Characterise cold and warm separately at epic 2's first measurement — do not average them.
2. Choose explicitly: pin Container Apps min replicas to 1 (~£12–15/month, inside the £30
   ceiling) or declare NFR-5 measured warm with the cold path exempt.
3. Record the decision in the spine, closing AR-40b. The spine's own instruction is "state it,
   do not leave it silent".
4. Whichever is chosen, the load harness follows from it — this is why the decision blocks the
   NFR-5 tests rather than merely informing them.

**Owner:** story 2.9 / 1.10 · **Timeline:** epic 2 · **Status:** Planned
**Verification:** the load report states the measurement condition in its own output, so a
later reader cannot mistake a warm figure for a general one.

#### R4: The Board's unbounded DOM (Score 6)

**Mitigation strategy:**

1. Measure at 5,000 Tasks in one column, not at a sample, and measure the state *after* paging
   to the end of the column rather than at first paint.
2. Assert both halves: NFR-5's latency budget and NFR-9's keyboard reachability of the last
   Task.
3. If append-only cannot hold, escalate to a focus-identity-preserving windowing scheme — one
   that recycles DOM nodes but pins focus to Task identity rather than element position. Note
   this is an **AD-29 amendment**, not an implementation choice, because AD-29 forbids
   virtualisation by name.

**Owner:** story 2.9 · **Timeline:** epic 2 · **Status:** Planned
**Verification:** keyboard traversal from the first to the last Task of a 5,000-Task column,
asserting focus lands on the intended Task at each step.

#### R5: The `SESSION_CONTEXT` parallel-plan defect (Score 6)

**Mitigation strategy:**

1. Confirm Azure SQL Database's exposure directly before first production deploy, closing
   AR-40c. The trace-flag workaround (11042) is not available on Azure SQL.
2. Keep the pooled-connection reuse case (I-8) permanently green as the standing regression
   guard, independent of the `MAXDOP` decision.
3. **Record the R3 interaction explicitly in both deferrals.** Neither AR-40b nor AR-40c
   currently mentions the other, and the cheapest way to buy latency is to relax the setting
   that suppresses a silent cross-tenant read.

**Owner:** story 1.10 · **Timeline:** before first production deploy · **Status:** Planned
**Verification:** the isolation suite's pooled-connection case runs on every PR, so relaxing
`MAXDOP` without it staying green fails the build.

#### R6: The merge conformance suite written to the algorithm (Score 6)

**Mitigation strategy:**

1. Write the suite in story 7.1 with **no candidate implementation in the repository**, citing
   FR-31, FR-33 and NFR-4 clause by clause.
2. Include the falsification case: whole-field last-writer-wins must **fail**. AD-12 says it
   cannot pass; assert that rather than assuming it.
3. Add a property-based interleaving harness — randomised operation orders across N
   participants asserting convergence and no lost insert. A CRDT's failure modes live in
   orderings nobody enumerates by hand.

**Owner:** story 7.1 · **Timeline:** before epic 7's implementation stories · **Status:** Planned
**Verification:** the suite is committed and green against a deliberately wrong implementation
(LWW) failing, before any real candidate is added.

#### R7: AD-15's collation (Score 6)

**Mitigation strategy:**

1. Assert `Latin1_General_100_BIN2` in a schema test against the migrated database — on the
   position key column *and* on the `(ProjectId, StatusId, PositionKey)` index AD-29 seeks
   against.
2. Add a functional case: insert keys differing only in case, assert deterministic ordering
   and that both rows are accepted.
3. Treat it as unfixable-later in planning: `ALTER DATABASE … COLLATE` is unsupported on Azure
   SQL, so this is set at column definition or requires a column rebuild.

**Owner:** story 2.6 (position keys) with the schema assertion seeded in 1.1 · **Timeline:**
epic 2 · **Status:** Planned
**Verification:** the mixed-case ordering case fails against a default-collation column.

#### R8: CORS and anti-forgery (Score 6)

**Mitigation strategy:** four negative INT tests in story 1.4 — a wildcard origin is rejected;
an arbitrary reflected `Origin` is rejected; a state-changing request without an anti-forgery
token is refused; no credential is written to `localStorage` or `sessionStorage`.

**Owner:** story 1.4 · **Timeline:** epic 1 · **Status:** Planned
**Verification:** each test fails against a permissive configuration.

#### R9: The ownership swap's unenforced ordering (Score 6)

**Mitigation strategy:**

1. Invariant test: no Space ever holds zero or two Owner Memberships, asserted on observable
   state after every ownership operation.
2. Concurrency case: two acceptances racing on one offer — one succeeds, the other gets 409
   with a stable problem `type`, never 404.
3. ArchUnit rule scoped to the `AcceptOwnershipOffer` slice forbidding tracked-entity
   `SaveChanges`, since AD-26 forbids it precisely because EF chooses its own statement order.

**Owner:** story 5.2 · **Timeline:** epic 5 · **Status:** Planned
**Verification:** the invariant test fails against a promote-before-demote implementation.

#### R10: Unconfirmed assumptions becoming acceptance criteria (Score 6)

**Mitigation strategy:** tag every test whose expected value traces to a PRD §12 assumption
with the assumption's index, so reversing one produces a known list rather than a hunt. The
four marked † (Space deletion irreversible, Project deletion irreversible, API versioning by
path segment with two concurrent versions, 90-day refusal retention) are the ones that have
already hardened into architecture and cost more than a document edit to reverse.

**Owner:** all stories · **Timeline:** ongoing · **Status:** Planned
**Verification:** a grep for the tag returns a complete list matching §12's thirteen entries.

#### R11: The §6.4 gate has no tripwire (Score 6)

**Mitigation strategy:** story 1.10 asserts the single-operator position, the condition that
ends it, and the five prerequisites that become live at that moment. Make the condition
observable operator-side — §6.1 forbids an in-product aggregate spanning Spaces, so this is a
direct datastore query, not an endpoint.

**Owner:** story 1.10 · **Timeline:** epic 1 · **Status:** Planned
**Verification:** the five prerequisites are enumerated in the story's acceptance criteria, so
a missing one is a story failure rather than a later discovery.

---

### Assumptions and Dependencies

#### Assumptions

1. **The pinned stack is the stack.** No code exists, so `test_stack_type: auto` detected
   nothing; the stack is taken from the spine. If it changes, this design's level mapping
   changes with it.
2. **Solo delivery.** There is no separate QA team. Every "owner" in this document is the same
   person wearing a different hat, which is why owners are expressed as *stories* rather than
   as roles — the story is the real scheduling unit.
3. **Epic-level TD passes will follow.** This design deliberately does not enumerate per-story
   slice tests; those belong to epic-level runs, which can now read these two documents as
   prior system-level context.
4. **PRD §12's thirteen assumptions remain unconfirmed** (readiness issue 5). Tests written
   against them inherit that status — see R10.
5. **`risk_threshold: p1`** means P0 and P1 gate; P2 and P3 do not.
6. **The four gating suites are the real quality gate**, and line-coverage percentage is a
   weak secondary signal for this product.

#### Dependencies

1. **B2 — RLS seeding strategy** — required by story 1.9 (the isolation suite).
2. **B3 — timing method** — required by stories 1.3 and 1.6.
3. **B6 — isolation case list** — required by story 1.9.
4. **B5 — browser-test binding** — required by story 1.2 (contrast harness delivery route)
   and stories 2.8 / 2.9.
5. **B4 — AR-40b warm/cold** — required by the NFR-5 load harness, epic 2.
6. **B1 — FR-34 seam** — required by stories 7.7 / 7.8, delivered by 7.3.
7. **AD-24 amendment** (readiness issue 3) — due before epic 3, and it changes what I-11
   asserts about Account-scoped surfaces.
8. **NFR-6 work factor** and **FR-38 rate-limit values** — required before stories 1.3 and
   8.3 can assert anything.

#### Risks to the test plan itself

- **Risk:** the isolation suite's ~118 tests against Testcontainers push the PR stage past
  15 minutes.
  - *Impact:* the fastest feedback loop degrades exactly where the most important suite lives.
  - *Contingency:* share one SQL Server container across collections (the pooled-connection
    case excepted), and shard by surface. If that is not enough, move the second-order
    isolation cases (I-16, I-17) to nightly and keep P0 in the PR stage.
- **Risk:** B5 stays unresolved and accessibility coverage never gets written.
  - *Impact:* NFR-9 is a release requirement covering five flows; the two client-only AD-29
    invariants would ship untested, and one of them is a data-corrupting keyboard-only defect.
  - *Contingency:* if the binding decision slips, write the contrast harness (X-1, X-2) as
    pure UNIT tests immediately — it needs no browser — so the largest single block of
    accessibility assertions lands regardless.
- **Risk:** the load-test window exhausts the free-tier grant (R13).
  - *Impact:* auto-pause until the month rolls over, blocking all Azure work.
  - *Contingency:* local Testcontainers target by default; cost the Azure window before
    running it.

---

**End of Architecture Document**

**Next steps:**

1. Decide the six blockers B1–B6. B2, B3 and B6 land inside epic 1 and gate the largest suite.
2. Confirm the seven high-priority recommendations, in particular the R3/R5 interaction — that
   latency work must not reopen the tenancy defect.
3. Record the R3/R5 interaction in AR-40b and AR-40c themselves, since neither mentions the
   other today.
4. Read the companion QA document (`test-design-qa.md`) for the scenario-level plan.
