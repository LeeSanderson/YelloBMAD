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

# Test Design for QA: Yello v1 (system-level)

**Purpose:** the test execution recipe — what to test, at which level, in what priority, and
what must be settled by someone else first.

**Date:** 2026-08-22
**Author:** Murat (Master Test Architect), for Lee
**Status:** Draft
**Project:** YelloBMAD

**Related:** see `test-design-architecture.md` for the testability concerns, the full risk
register with mitigation plans, and the six blockers.

---

## Executive Summary

**Scope:** Yello v1 in full — 43 FRs, 9 NFRs, 29 ADs, 8 epics, 53 stories. Greenfield: no
code, no tests, no CI. The architecture already fixes five test projects
(`Yello.Tests.Isolation`, `.Revocation`, `.Merge`, `.Architecture`, `.Slices`) and four
release-gating suites, so this plan populates a shape that is already decided rather than
proposing one.

**Risk summary:**

- Total risks: **18** — 1 critical (score 9), 10 high (6–8), 5 medium (4–5), 2 low (≤3)
- Critical categories: **SEC** and **DATA** carry the most high-priority risks, which is what
  you would expect when NFR-1 is stated as having no acceptable failure rate

**Coverage summary:**

| Priority | Scenario classes | Notes |
| --- | :-: | --- |
| **P0** | ~205 | Includes the isolation suite's 51 P0 classes, which double to ~102 tests across both surfaces |
| **P1** | ~65 | Second-order isolation, performance budgets, the accessibility conformance surface, deprecation behaviour |
| **P2** | ~5 | Cold-start characterisation, convention assertions |
| **P3** | 0 | Nothing here is genuinely optional; `risk_threshold: p1` excludes P2/P3 from gating anyway |
| **Total** | **~275 classes → ~330 tests** | **~160–245 hours** of construction, spread across 8 epics |

P0 dominating is a property of the product and of this document's scope rather than of the
scoring: NFR-1 admits no failure rate, SM-1 and SM-2 gate release outright, AD-21 makes the
paradigm a build gate — and the ordinary per-story slice tests that would form the bulk of the
denominator are deliberately delegated to epic-level passes. See the note under P0 below.

---

## Not in Scope

| Item | Reasoning | Mitigation |
| --- | --- | --- |
| **Per-story slice tests (53 stories)** | Belongs to epic-level TD passes, which can now read these two documents as prior system-level context. Enumerating them here would duplicate and go stale | The *shape* is fixed by convention: one folder per use case holding its command, handler, validator and tests. Two cross-cutting obligations that are not per-story are included below as S-1 and S-2 |
| **OAuth sign-in** | Deferred by PRD §9.2. It would be Yello's first inbound third-party dependency | FR-1, FR-2 and NFR-6 are to be written so this can change without redesign — a constraint on implementation, not a test today |
| **Trash / restore** | Ruled out for v1 (§6.2). Deletion is irreversible | Raises the value of the atomicity cases (S-1), which are in scope |
| **Cross-Project search** | Scoped to a Project in v1 (§9.2); no index exists | Any future implementation inherits AD-2 and AD-3 in full |
| **Webhooks / outbound integrations** | API is inbound-only (§9.2). This is why `tea_use_pactjs_utils: false` is correct — there is no consumer/provider pair, so contract testing here is snapshot-shaped (AD-19), not Pact-shaped | C-1 and C-2 lock the shape by snapshot instead |
| **Horizontal sync scaling** | AD-14 keeps it possible by forbidding backplane-dependent designs; it does not build one | AD-14's single-replica assertion is in scope (V-13) |
| **DR beyond included backups** | Free offer gives 7-day PITR, locally redundant. No RTO/RPO was ever stated | Recorded as an UNKNOWN threshold, not silently accepted |
| **Session telemetry / SM-C2** | Out of scope by §9.2, so SM-C2 is defined but not measurable in v1 | Stated in the PRD rather than assumed |
| **Load testing against Azure by default** | Would consume a material share of the 100,000 vCore-s free grant, and auto-pause-until-next-month is configured deliberately (R13) | Local Testcontainers target; Azure reserved for one costed window |

---

## Dependencies & Test Blockers

**Critical:** the following must be settled before the dependent tests can be written. Full
detail in the Architecture doc's Quick Guide.

### Architecture dependencies (pre-implementation)

1. **B2 — RLS seeding strategy** · story 1.5 · *blocks the isolation suite (story 1.9)*
   - Needed: which of three shapes is used to populate two Spaces when AD-2 filters every
     Space-scoped table on session context and forbids raw SQL outside `Infrastructure`.
   - Blocks testing because: seeding Space B's rows under Space A's context writes nothing
     visible. Every isolation test needs two populated Spaces.

2. **B3 — duration-indistinguishability method** · stories 1.3, 1.6 · *blocks I-6 and I-7*
   - Needed: sample size, statistic, tolerance, measurement point.
   - Blocks testing because: a single-sample assertion tests one draw from two distributions.
     Writing it without a method produces a test that cannot detect the oracle it exists to
     detect.

3. **B6 — isolation case list** · story 1.9 · *blocks the SM-1 claim*
   - Needed: the case list as data, so the surface cross-product is generated.
   - Blocks testing because: "every case on both surfaces" cannot be verified against an
     unenumerated set. The suite would be complete by construction at any size.

4. **B5 — browser-test binding** · TF run · *blocks all E2E (X-3 … X-11)*
   - Needed: Playwright for .NET, or a separate TypeScript project.
   - Blocks testing because: two AD-29 invariants live only in the rendered client and are
     unreachable from xUnit and ArchUnit.

5. **B4 — NFR-5 warm or cold (AR-40b)** · story 2.9 / 1.10 · *blocks P-1, P-2, P-11*
   - Needed: the decision, recorded in the spine.
   - Blocks testing because: warm and cold are different harnesses, not different numbers.

6. **B1 — FR-34 interleaving seam** · story 7.3 · *blocks V-4 … V-9*
   - Needed: a deterministic ordering point around lease invalidation.
   - Blocks testing because: without it the SM-2 assertions are a race nobody controls.

7. **Unknown values that must be chosen before their tests exist:** NFR-6 password work
   factor (story 1.3) · FR-38 rate-limit values (story 8.3) · the 50 ms RTT simulation method
   (P-6) · the definition of an "active Session" for NFR-8 (P-5).

8. **AD-24 amendment** (readiness issue 3, due before epic 3) — changes what I-11 asserts
   about Account-scoped surfaces.

### Test infrastructure setup (pre-implementation)

1. **Test data factories** — per-entity builders for Account, Space, Membership, Project,
   Task, StatusDefinition, Label, Invitation, OwnershipOffer, ApiToken. Randomised where
   identity does not matter; deterministic where an assertion depends on it. Note that
   **email addresses must be randomised per test** — FR-1's uniqueness constraint makes a
   shared fixture address a cross-test collision.
2. **Two-Space fixture** — the workhorse of the isolation suite: two Spaces, two Accounts,
   one Membership each, populated with one of every Space-scoped entity. Blocked on B2.
3. **Surface parameterisation** — one abstraction over "browser session" and "API Token" so
   AD-4's every-case-both-surfaces requirement is a `[Theory]` parameter rather than a
   duplicated file.
4. **Shared SQL Server container** — one Testcontainers instance across collections to
   amortise startup, **except** the pooled-connection collection (I-8), which needs its own
   with pool size pinned to 1 and parallelism disabled.
5. **Interleaving harness** — blocked on B1; the seam plus a test helper that forces both
   orderings.
6. **Contrast harness** — computes all 18 gated pairs from the 30 tokens by the WCAG 2.x
   formula (sRGB linearisation at the 0.03928 threshold, `(L₁+0.05)/(L₂+0.05)`), both themes,
   and **excludes** the two surface-adjacency ratios that DESIGN.md names as not contrast
   pairs. Needs no browser, so it can be built before B5 resolves.

**Environments:** Local (`aspire run` + `mssql/server:2025-latest` container) and Azure. There
is no staging environment and one is not wanted — a second Azure environment would consume the
same free grants that make production free.

---

## Risk Assessment

Full detail and mitigation plans in the Architecture doc. This section maps each risk to the
coverage that validates it.

### High-priority risks (score ≥6)

| Risk ID | Cat | Description | Score | Coverage that validates it |
| --- | --- | --- | :-: | --- |
| **R1** | TECH | FR-34's interleaving cannot be tested deterministically | **9** | **V-9** (both orderings forced) gates V-4…V-8. Verified by 100× burn-in: a real seam gives 100 identical results, a `Task.Delay` gives a distribution |
| **R2** | SEC | Timing oracles behind vacuous tests (AD-3, AD-23) | **6** | **I-6**, **I-7**. Validated by planting a known oracle (skip the hash for an unknown address) and confirming the test fails |
| **R3** | PERF | NFR-5 missed by an order of magnitude on cold requests | **6** | **P-1**, **P-11**. Cold and warm characterised separately, never averaged |
| **R4** | PERF | The Board's protection against focus corruption is an unbounded DOM | **6** | **P-2** at 5,000 measured *after* paging to the end, plus **X-5** keyboard traversal first-to-last |
| **R5** | DATA | `SESSION_CONTEXT` parallel-plan defect; R3 pressures the mitigation | **6** | **I-8** pooled-connection reuse, permanently green as the standing regression guard |
| **R6** | TECH | Merge suite written to the algorithm rather than the requirements | **6** | **M-3** (LWW must fail) and **M-9** (property-based interleaving) |
| **R7** | DATA | AD-15 collation, not fixable server-wide later | **6** | **A-6** schema assertion on column *and* index, plus a mixed-case ordering case |
| **R8** | SEC | CORS / anti-forgery misconfiguration | **6** | Four negative INT tests in story 1.4 |
| **R9** | DATA | Ownership swap ordering unenforced in code | **6** | **S-2** invariant, a two-acceptance concurrency case, **A-14** ArchUnit rule |
| **R10** | BUS | 13 unconfirmed §12 assumptions become acceptance criteria | **6** | Cross-cutting: tag every test tracing to a §12 assumption |
| **R11** | SEC | §6.4 gate has no tripwire | **6** | Story 1.10's five prerequisites as acceptance criteria |

### Medium / low-priority risks

| Risk ID | Cat | Description | Score | Coverage |
| --- | --- | --- | :-: | --- |
| R13 | OPS | Load test can exhaust the free grant | 4 | Local target by default; Azure window costed first |
| R14 | DATA | Compaction can destroy SM-5 irrecoverably | 4 | **M-11** — compact and assert per-author counts and timestamps unchanged |
| R15 | PERF | NFR-3's 16 ms local render tight for Blazor WASM | 4 | **P-7**, measured early in story 7.4 |
| R16 | TECH | Client invariants have no test level | 4 | Blocked on B5; **X-5**, **X-6** are the affected cases |
| R17 | TECH | Isolation suite completeness unfalsifiable | 4 | Blocked on B6 |
| R12 | TECH | Expiry only reachable by seeding the past | 3 | Seeding convention stated once in story 5.3; **A-10** given a concrete predicate |
| R18 | OPS | NFR-5 has no production evidence source | 2 | None — documented so p95 is never later cited as production evidence |

---

## NFR Test Coverage Plan

Defines what evidence to create. No PASS/CONCERNS/FAIL is assigned — that is
`bmad-testarch-nfr`'s job once implementation evidence exists.

| NFR Category | Requirement / Threshold | Planned Validation | Tool / Level | Evidence Artifact | Priority |
| --- | --- | --- | --- | --- | :-: |
| Security — isolation | NFR-1: zero cross-Space disclosure, both surfaces, no acceptable failure rate | 59 case classes × 2 surfaces; `AccessRefusal` row assertions | INT (xunit + Testcontainers) | Isolation suite report + refusal rows | **P0** |
| Security — disclosure | AD-3 / AD-23: identical body **and duration** | Byte-identical body comparison; statistical duration comparison | INT | Distribution comparison, server-side | **P0** |
| Security — credentials | NFR-6: slow one-way hash tunable without re-registration; Tokens unusable at rest, shown once | Hash verification, work-factor change without re-registration, Token single-display | INT | Story 1.3/1.8 results. **Work factor UNKNOWN** | **P0** |
| Security — cross-origin | AD-7: exact origin, credentials, anti-forgery | Four negative tests | INT | Story 1.4 results | **P0** |
| Performance — API | NFR-5: 300 ms read / 500 ms write p95 server-side | Load run within the NFR-8 envelope | LOAD (k6) | Summary JSON, p95 per operation class. **Warm/cold UNKNOWN (B4)** | P1 |
| Performance — client | NFR-3: 16 ms local, 300 ms p95 remote at 50 ms RTT | Browser timing; simulated RTT | E2E + LOAD | Timing traces. **RTT method UNKNOWN** | P1 |
| Performance — Board | FR-28/FR-30 at 5,000 Tasks holding NFR-5 and NFR-9 | Keyset page cost at depth; keyboard reach of the last Task | LOAD + E2E | Depth-vs-latency curve; traversal result | P1 |
| Scalability | NFR-8: six bounds refuse rather than answer wrongly | One refusal case per bound, in-transaction; registry completeness | INT + ARCH | Bound registry test + 6 refusal cases. **"Active Session" UNDEFINED** | **P0** |
| Reliability — revocation | NFR-2: next request (no tolerance); 1 s live session | 22 case classes; both orderings forced | INT | Revocation suite report. **Blocked on B1** | **P0** |
| Reliability — convergence | NFR-4: 2 s, 10 editors, 5-min reconnect | Conformance suite + property-based interleaving | UNIT + INT | Merge conformance report | **P0** |
| Accessibility | NFR-9: WCAG 2.1 AA on five flows; keyboard parity; ARIA live | 18 gated contrast pairs × 2 themes; axe on five flows; keyboard traversal; focus identity; `aria-setsize`/`posinset` | UNIT + E2E | 36 computed ratios; axe reports; parity matrix | **P0** |
| Maintainability | No target stated in PRD, spine or epics | Coverage + vulnerability scan in CI | CI | Coverage report. **Target is a proposal — see gates** | P1 |
| Observability | NFR-7: refusal recorded with the `CrossSpace`/`InsufficientRole` distinction; 90 days | Row assertions; purge job ≤ daily | INT | `AccessRefusal` assertions | **P0** |
| Compliance | §6.4 gate opens on the first non-operator Account | Single-operator assertion + five prerequisites | INT + story AC | Story 1.10 acceptance criteria | **P0** |

**Missing thresholds or evidence sources needing a decision before `nfr-assess` can run:**
NFR-5 warm vs cold · NFR-6 work factor · FR-38 rate-limit values · NFR-3's 50 ms RTT method ·
"active Session" definition · availability target · RTO/RPO · coverage target. None is invented
here.

---

## Entry Criteria

Testing cannot begin on a given suite until its blockers are cleared. Stated per suite rather
than globally, because this project builds them across eight epics rather than at once.

- [ ] **All suites:** the solution skeleton and its four build gates exist (story 1.1)
- [ ] **All suites:** shared Testcontainers SQL Server fixture running `mssql/server:2025-latest`
- [ ] **Isolation:** B2 (seeding strategy) and B6 (case list) decided; surface parameterisation built
- [ ] **Isolation:** the pooled-connection collection isolated with parallelism disabled
- [ ] **Isolation / registration:** B3 (timing method) decided
- [ ] **Revocation:** B1 (interleaving seam) exists in story 7.3
- [ ] **Merge:** the conformance suite written and green *before* any candidate algorithm is added
- [ ] **Accessibility:** B5 (browser binding) decided — except the contrast harness, which needs no browser
- [ ] **Performance:** B4 (warm/cold) decided; local load target provisioned
- [ ] **Rate limiting:** FR-38 values chosen
- [ ] Requirements and assumptions agreed — noting PRD §12's thirteen remain unconfirmed (R10)

## Exit Criteria

- [ ] **All P0 passing — 100%**
- [ ] **P1 ≥ 95%**, remaining failures triaged and accepted
- [ ] **SM-1: zero verified cross-Space disclosures.** Not a percentage — a single verified
      disclosure blocks release
- [ ] **SM-2: revocation governs the next request with no tolerance, and live sessions within
      1 s, in 100% of tested cases** — including sessions holding unsynchronised edits
- [ ] Merge conformance green, with LWW demonstrably failing it
- [ ] Architecture suite green (it fails the build, not the review)
- [ ] Contract snapshots locked for every served version
- [ ] All six NFR-8 bounds enumerated in the registry and enforced
- [ ] 18 gated contrast pairs passing in both themes; the 2 adjacency ratios still excluded
- [ ] Every score-≥6 risk has an owner and a plan; **R1 resolved before story 7.3 is called done**
- [ ] No open high-severity defect

---

## Test Coverage Plan

**P0/P1/P2/P3 = priority and risk, not execution timing.** See Execution Strategy for when
things run.

### P0 (Critical)

*Blocks core functionality · high risk (≥6) · no workaround · security or data integrity.*

| Test ID | Requirement | Level | Risk Link | Notes |
| --- | --- | --- | --- | --- |
| **I-1** | FR-15, NFR-1 — read in a Space with no Membership → 404, per entity type | INT | R1n/a | 7 entity types × 2 surfaces |
| **I-2** | FR-15, NFR-1 — write/mutate in a foreign Space → 404 | INT | — | 7 × 2 |
| **I-3** | FR-15, FR-18, AD-29 — no listing or aggregate includes a foreign row | INT | — | 8 × 2, includes Board column counts |
| **I-4** | NFR-1 — possessing an identifier confers nothing | INT | — | Deep link by Guid, 3 × 2 |
| **I-5** | AD-3 — boundary 404 and in-Space not-found byte-identical in body, status, headers | INT | — | 2 × 2 |
| **I-6** | AD-3 — indistinguishable in **duration** | INT | **R2** | Statistical; blocked on B3 |
| **I-7** | AD-23, §6.1 — registration / auth / invitation identical for known vs unknown address, including duration | INT | **R2** | 6 × 2; blocked on B3 |
| **I-8** | AD-2 — **pooled-connection reuse** across two Spaces on one physical connection | INT | **R5** | Own collection, parallelism off, pool size 1 |
| **I-9** | FR-15 — Owner of one Space has no standing in another | INT | — | 2 × 2 |
| **I-10** | FR-9, NFR-2 — authorisation never cached across a Space switch | INT | — | 3 × 2 |
| **I-11** | AD-24 — Account-scoped surfaces return Space **identity only** | INT | — | 4 × 2; changes if AD-24 is amended |
| **I-12** | FR-36, AD-6 — a Token reaches only its bound Space, including others its Account owns | INT | — | 2 × 2 |
| **I-13** | AD-3, §6.1 — error bodies carry no Space name, title or existence hint | INT | — | 4 × 2 |
| **I-14** | FR-39, FR-40, FR-43, NFR-1 — notification content discloses nothing across a boundary | INT | — | 3 × 2 |
| **I-15** | FR-21, FR-30 — Assignee candidates and filter offer only same-Space Memberships | INT | — | 2 × 2 |
| **V-1** | NFR-2 request path — Role change governs the very next request | INT | — | 5 capability classes, both surfaces |
| **V-2** | NFR-2, FR-14 — removal governs the very next request | INT | — | |
| **V-3** | FR-36, AD-6 — Token invalidated in the same transaction as removal / Space delete / Account delete | INT | — | 3 cases |
| **V-4** | FR-34, NFR-2 live — removal mid-edit: lease invalidated, editor inert, ≤ 1 s, unprompted | INT | **R1** | Blocked on B1 |
| **V-5** | FR-34 — unsynchronised local changes **not applied** after removal | INT | **R1** | Independent of both timings |
| **V-6** | FR-34 — changes admitted **before** invalidation are retained | INT | **R1** | |
| **V-7** | FR-34, AD-8 — a delayed or retried frame on an invalidated lease is discarded, not queued, not persisted | INT | **R1** | |
| **V-8** | FR-34, UJ-6 — demotion to Viewer ends editing, read access continues | INT | **R1** | |
| **V-9** | **R1** — both orderings forced deterministically across the removal commit | INT | **R1** | **The gate on R1.** 100× burn-in |
| **V-10** | AD-9, AD-26 — accepting an Ownership Offer publishes `MembershipChanged` per affected Account | INT | R9 | Two Roles move, so two publishes |
| **M-1** | FR-31 — different regions, both retain | UNIT | — | 4 cases |
| **M-2** | FR-31 — same region, converge to identical persisted text | UNIT | — | 4 cases |
| **M-3** | AD-12 — **whole-field LWW must fail the suite** | UNIT | **R6** | Falsification; asserted not assumed |
| **M-4** | NFR-4 — convergence ≤ 2 s of the last edit | UNIT/INT | — | |
| **M-5** | NFR-4, NFR-8 — convergence at 10 simultaneous editors | UNIT | — | |
| **M-6** | FR-33, NFR-4 — 5-minute disconnect reconciles, no loss, no duplication | UNIT/INT | — | 3 cases |
| **M-7** | FR-33 — others' changes present after reconnection | INT | — | |
| **M-8** | FR-33 — reconciliation failure reported; text not silently discarded | INT | — | |
| **M-9** | NFR-4 — **property-based interleaving** over randomised operation orders | UNIT | **R6** | Harness. CRDT failures live in orderings nobody enumerates |
| **A-1** | AD-21 — dependency rule, four assertions | ARCH | — | |
| **A-2** | AD-21 — EF types absent from Domain; ASP.NET types absent from Application/Domain | ARCH | — | |
| **A-3** | AD-1 — `[Authorize(Roles=…)]`, `IsInRole`, `IdentityRole`, Identity role store all absent | ARCH | — | 4 assertions |
| **A-4** | AD-3 — every Space-scoped route carries `{spaceId}` | ARCH | — | |
| **A-5** | AD-2 — every Space-scoped table has an RLS policy (schema test) | ARCH | — | |
| **A-6** | AD-15 — `Latin1_General_100_BIN2` on the key column *and* the AD-29 index | ARCH | **R7** | Plus a mixed-case ordering case |
| **A-7** | AD-25 — the bound registry enumerates **all six** bounds | ARCH | — | A registry missing any fails |
| **A-8** | AD-12 — exactly one `ITextMergeStrategy`; no concrete merge type referenced | ARCH | — | |
| **A-9** | AD-10 — nothing queries the database on a fixed interval | ARCH | — | |
| **A-10** | AD-27 — no scheduled component writes a terminal expiry state | ARCH | R12 | Needs the concrete predicate |
| **X-1** | NFR-9 — 18 gated contrast pairs, both themes, computed by the WCAG 2.x formula | UNIT | — | 36 assertions. **No browser needed** |
| **X-2** | DESIGN.md — the 2 surface-adjacency ratios stay **excluded** from the gate | UNIT | — | Gating them would fail the build permanently |
| **X-5** | AD-29 — **focus identity survives paging**; no row recycled onto a different Task | E2E | **R4**, R16 | Kept at P0 because it is a data-corrupting defect, not an accessibility polish item — the spine's own words are "reachable only by keyboard, invisible to pointer testing" |
| **P-3** | AD-29, FR-30 — List View keyset correct at every offered sort, with ties and NULLs | INT | — | 5 sorts; no row skipped or repeated across a page boundary — data integrity |
| **P-5** | NFR-8, AD-25 — each of the six bounds refuses at the bound, in-transaction, machine-readably | INT | — | NFR-8: an unenforced bound is a defect, and the failure is a wrong answer. "Active Session" needs defining |
| **P-10** | AD-18, NFR-5 — replayed `Idempotency-Key` returns the original response without re-applying | INT | — | 3 cases. Data integrity |
| **C-1** | AD-19 — snapshot contract locks each served version's shape and accepted input | INT | — | |
| **C-2** | FR-37 — within a version: no field removed, renamed, retyped; no input narrowed | INT | — | 4 separate assertions; a breaking change fails the build |
| **C-6** | FR-35, AD-4 — parity audit: nothing refused in the browser succeeds via the API | INT | — | Harness |
| **S-1** | AD-17, FR-26, FR-27, FR-41 — atomicity under a forced mid-operation failure | INT | — | 6 cases incl. FR-27's per-Project exceptions and the bulk move |
| **S-2** | AD-17, AD-26, FR-42 — no Task holds an absent Status; no Space holds zero or two Owners | INT | **R9** | Invariant assertions |

**Total P0: ~205 scenario classes** (~292 tests once the isolation suite doubles across
surfaces).

> **On the P0 ratio, and the missing denominator.** P0 is ~75% of the scenarios listed here,
> against a best-practice guide of under 10%. Both numbers are correct, and the discrepancy is
> the denominator rather than the classification.
>
> This is a *system-level* design, and it deliberately excludes the ordinary per-story slice
> tests for all 53 stories — the create/rename/delete paths, the validation cases, the
> Role-matrix permutations per capability — because those belong to the epic-level TD passes.
> What is left after that exclusion is almost entirely the four release-gating suites, which
> are P0 by definition. Fold the slice tests back in (realistically 400–600 further scenarios
> across eight epics) and P0 lands nearer 25–30%.
>
> The classification was still re-examined against the strict criterion — blocks core
> functionality **and** high risk **and** no workaround. Six scenarios moved down as a result:
> **X-3, X-4, X-6, X-7** (WCAG conformance, keyboard parity, ARIA totals, live-region
> announcements) are NFR-9 release requirements but are not among §10's gating metrics, and a
> fix-forward path exists; **P-4** is a performance enabling condition rather than a
> correctness one; **C-5** is a scope narrowing whose violation is a design defect, not data
> loss. X-5 stayed at P0 on data-integrity grounds.

---

### P1 (High)

| Test ID | Requirement | Level | Risk Link | Notes |
| --- | --- | --- | --- | --- |
| **I-16** | FR-32 — Presence never reveals activity in another Space | INT | — | |
| **I-17** | NFR-6, §6.1 — logs contain no password, Token, cookie or Space/Project/Task content | INT | — | `SpaceId` as a field, never the name |
| **V-11** | FR-7, FR-3 — Space deletion and Account deletion terminate live sessions | INT | — | |
| **V-12** | FR-23 — Task deletion terminates the editing session with a deleted reason | INT | — | Not a silent drop |
| **V-13** | AD-8, AD-14 — leases do not survive a process restart; reconnection re-authorises | INT | — | |
| **X-3** | NFR-9 — WCAG 2.1 AA, zero axe violations on the five named flows | E2E | R16 | Blocked on B5. Release requirement, not a §10 gating metric |
| **X-4** | NFR-9, FR-29 — every Board pointer operation has a keyboard equivalent | E2E | R16 | 6 operations incl. cross-column move |
| **X-6** | AD-29 — `aria-setsize` true total, `aria-posinset` true ordinal, agreeing with the visible chip | E2E | R16 | Only satisfiable because the count is queried, not derived from the page |
| **X-7** | NFR-9, FR-32, FR-34 — presence and permission-change notices via ARIA live regions | E2E | R16 | Not colour or position alone |
| **P-4** | AD-29 — every offered List View sort has a matching composite index | ARCH | — | A sort without one is a defect, not a slow query — at 5,000 Tasks it is an NFR-5 failure |
| **C-5** | FR-35 — Board position readable over the API and **not writable** | INT | — | The one deliberate narrowing; keeps FR-29's convergence confined to one surface |
| **M-10** | FR-31 — no merge prompt, lock or stale-content warning during normal concurrent editing | E2E | R16 | |
| **M-11** | AD-13 — compaction preserves per-author change counts and timestamps | INT | **R14** | |
| **A-11** | AD-13 — nothing writes the projection except the projector | ARCH | — | |
| **A-12** | AD-21, AR-3 — no slice re-implements authorisation, Space resolution, refusal recording or idempotency | ARCH | — | |
| **A-13** | AD-2 — raw SQL bypassing global query filters only in Infrastructure | ARCH | — | |
| **A-14** | AD-26 — `AcceptOwnershipOffer` does not use tracked-entity `SaveChanges` | ARCH | **R9** | |
| **X-8** | UJ-4, DESIGN.md — Role chip renders as a chip (border present), Role verbatim from §2 | E2E | R16 | Fill sits at 1.05 without the border |
| **X-9** | UJ-4, §7 — capabilities a Role lacks are **absent**, not present-and-failing | E2E | R16 | 4 cases |
| **X-10** | FR-34, DESIGN.md — revoked editor is `readonly` with the revoked-edge border, text legible and selectable | E2E | R16 | |
| **X-11** | NFR-9 — small viewport; 1.4.12 text-spacing override; 1.4.4 resize | E2E | R16 | 3 cases |
| **P-1** | NFR-5 — read p95 ≤ 300 ms, write p95 ≤ 500 ms server-side | LOAD | **R3** | Blocked on B4 |
| **P-2** | FR-28, AD-29 — Board at 5,000: first paint one bounded seek per column; last page no slower than the first | LOAD | **R4** | Measured after paging to the end |
| **P-6** | NFR-3 — remote edit renders ≤ 300 ms p95 at 50 ms RTT | LOAD | — | RTT method unknown |
| **P-7** | NFR-3, AD-11 — local edit renders ≤ 16 ms without a network round trip | E2E | **R15** | Measure early in story 7.4 |
| **P-8** | NFR-3 — presence appears ≤ 2 s, disappears ≤ 10 s | INT | — | |
| **P-9** | FR-38, AD-18 — rate limiting per Token with `Retry-After`; one Space cannot exhaust another | INT | — | Values unknown |
| **C-3** | AD-19 — at most two versions served concurrently; a named version gets its own shape | INT | — | |
| **C-4** | FR-37 — deprecation announced before withdrawal; the version keeps serving throughout | INT | — | |
| **C-7** | NFR-5 — RFC 9457 problem documents with a stable machine-readable `type` | INT | — | Prose is never the contract |

**Total P1: ~65 scenario classes.**

---

### P2 (Medium)

| Test ID | Requirement | Level | Risk Link | Notes |
| --- | --- | --- | --- | --- |
| **P-11** | Cold-start characterisation — measure and record, assert nothing until AR-40b closes | LOAD | **R3** | |
| **A-15** | Conventions — no `DateTime` in domain or wire types; `DateTimeOffset` only | ARCH | — | |

**Total P2: ~5 scenario classes.** With `risk_threshold: p1`, these do not gate.

---

### P3 (Low)

None. Nothing in this design is genuinely optional — see the note on P0 dominance in the
Executive Summary.

---

## Execution Strategy

Organised by cost, not by priority. Every priority level appears in the PR stage.

### Every PR — target < 15 minutes

- **ARCH** (24 assertions) — seconds. Runs first; a violation fails the build before anything
  slower starts
- **UNIT** — contrast harness (36 ratios), merge conformance (21 classes), domain invariants
- **INT** — isolation suite (~118 tests), revocation suite, contract snapshots, bound refusals,
  atomicity, slice tests
- Shared SQL Server container across collections; the pooled-connection collection (I-8) gets
  its own with parallelism disabled

**The long pole is the isolation suite.** If the PR stage exceeds 15 minutes, shard by surface
before moving anything out, and move I-16/I-17 to nightly before touching any P0.

### Nightly

- **E2E** — axe on five flows, keyboard parity, focus-identity paging, the remaining X cases
- **Board at 5,000 Tasks** — too slow for a PR, too important for weekly
- **Property-based merge interleaving** with a larger case budget than the PR run
- **Burn-in** — V-9 run 100× to prove the interleaving seam is deterministic rather than lucky

### Weekly — deliberate windows only

- **LOAD** at the full NFR-8 envelope (5,000 Tasks · 10 editors · 50 Sessions)
- Cold-start characterisation
- Rate-limit saturation

**Two constraints shape this more than usual.** AD-10 means no suite may introduce a
timer-driven database touch, even in test infrastructure. And the weekly stage does **not**
target Azure by default (R13) — exhausting the 100,000 vCore-s grant triggers auto-pause until
the month rolls over, which is the configured behaviour and takes the environment away.

### Not automated

- Confirming Azure SQL's exposure to the `SESSION_CONTEXT` defect (R5) — a vendor question
- Costing the Azure load-test window before running it
- Confirming PRD §12's thirteen assumptions (R10) — a product decision

---

## Effort Estimate

Test construction only. Excludes application implementation and ongoing maintenance (~10%).
Spread across eight epics rather than incurred up front.

| Priority | Classes | Effort | Notes |
| --- | :-: | --- | --- |
| P0 | ~215 | **~95–140 h** | Isolation suite alone is ~35–50 h — 59 classes × 2 surfaces, plus the seeding strategy and the pooled-connection collection |
| P1 | ~55 | **~55–85 h** | Accessibility E2E ~20–30 h, unestimable until B5 resolves |
| P2 | ~5 | **~10–20 h** | Cold-start characterisation |
| P3 | 0 | — | |
| **Total** | **~275** | **~160–245 h** | |

Three items carry the widest uncertainty and are named rather than averaged away:

- **The R1 seam** — ~4–10 h to design *if done in story 7.3*. Deferred past it, the figure is
  not 4–10 hours, because it changes the sync handler's concurrency shape.
- **The B3 timing method** — ~6–12 h once, reused by two requirements. Open-ended if improvised
  twice.
- **Accessibility E2E** — genuinely unestimable until B5. A separate TypeScript project costs
  setup plus a second toolchain; Playwright for .NET keeps one language with a thinner helper
  ecosystem than the TEA fragments assume.

**Note on solo delivery.** There is no separate QA engineer. These hours land on the same
person implementing the features, which is the strongest argument for the tiered execution
above and for building the two-Space fixture and surface parameterisation properly in epic 1 —
they are amortised across roughly 118 isolation tests and every later epic.

---

## Implementation Planning Handoff

| Work item | Owner | Target | Dependencies / notes |
| --- | --- | --- | --- |
| Decide B2 (RLS seeding strategy) | Story 1.5 | Epic 1 | Blocks the isolation suite |
| Decide B3 (timing method) | Stories 1.3, 1.6 | Epic 1 | Blocks I-6, I-7. Validate against a planted oracle |
| Build the two-Space fixture + surface parameterisation | Story 1.9 | Epic 1 | Amortised across ~118 tests |
| Enumerate the isolation case list as data (B6) | Story 1.9 | Epic 1 | Generate the surface cross-product |
| Isolate the pooled-connection collection | Story 1.9 | Epic 1 | Parallelism off, pool size 1 |
| Decide whether `/sync` is a third isolation surface | Story 1.9 | Epic 1 | Currently falls between two suites |
| Build the contrast harness (X-1, X-2) | Story 1.2 | Epic 1 | **Not blocked on B5** — do it early |
| Choose the NFR-6 password work factor | Story 1.3 | Epic 1 | Currently unspecified |
| Four CORS / anti-forgery negative tests | Story 1.4 | Epic 1 | R8 |
| §6.4 tripwire + five prerequisites as AC | Story 1.10 | Epic 1 | R11 |
| Confirm Azure SQL's `SESSION_CONTEXT` exposure (AR-40c) | Story 1.10 | Before first deploy | R5. Record the R3 interaction in both deferrals |
| Decide B5 (browser-test binding) | TF run | Before story 2.8 | Blocks all E2E |
| Assert AD-15 collation in the schema test | Story 2.6 (seeded 1.1) | Epic 2 | R7 |
| Decide B4 (NFR-5 warm/cold, AR-40b) | Story 2.9 / 1.10 | Epic 2 | Blocks the load harness |
| Measure the Board at 5,000 after paging to the end | Story 2.9 | Epic 2 | R4 |
| State the expiry-seeding convention once | Story 5.3 | Epic 5 | R12 |
| Give AD-27's ArchUnit rule a concrete predicate | Story 5.3 | Epic 5 | R12 |
| Ownership invariant + concurrency + ArchUnit rule | Story 5.2 | Epic 5 | R9 |
| Write the merge conformance suite with no candidate present | Story 7.1 | Before epic 7 impl | R6. LWW must fail it |
| **Specify the FR-34 interleaving seam** | **Story 7.3** | **Before 7.7/7.8** | **R1 — the only score-9 risk** |
| Close AD-13's compaction obligation | Epic 7 | Before compaction | R14 |
| Choose FR-38 rate-limit values | Story 8.3 | Epic 8 | Currently unspecified |

---

## Tooling & Access

| Tool / service | Purpose | Access required | Status |
| --- | --- | --- | --- |
| xunit.v3 4.0.0 | All UNIT and INT suites | — | Pinned by the spine |
| Testcontainers.XunitV3 4.6.0 + `mssql/server:2025-latest` | Real SQL Server for RLS. **In-memory provider forbidden** | Docker locally and in CI | Pinned |
| TngTech.ArchUnitNET 0.13.3 | The 24 ARCH assertions | — | Pinned |
| Browser automation | E2E: axe, keyboard parity, focus identity, ARIA | — | **Binding undecided (B5)** |
| axe-core | WCAG 2.1 AA on five flows | — | Pending B5 |
| k6 or equivalent | LOAD: p95, bounds, rate limiting | Local target by default | Pending B4 |
| Azure subscription | One costed load-test window | Existing | Guard the free grant (R13) |

**Access / decisions to request:**

- [ ] Confirm Azure SQL Database's exposure to the `SESSION_CONTEXT` parallel-plan defect (R5)
- [ ] Confirm whether `tea_use_playwright_utils` should be set `false` — its fragments are
      TypeScript helpers and the stack is .NET (see the Architecture doc)

---

## Interworking & Regression

| Component | Impact | Regression scope | Validation |
| --- | --- | --- | --- |
| **The request pipeline** | Every slice traverses it; it carries authorisation, Space resolution, refusal recording, idempotency and the NFR-8 bounds | **The entire isolation and revocation suites** on any pipeline change | AD-21's rule that a slice re-implementing these is a defect (A-12) |
| **RLS policies (migrations)** | A new Space-scoped table without a policy is a hole | Full isolation suite + the schema test | A-5 fails the build on a table without a policy |
| **`Yello.Merge`** | Shared client and server, compiled to WASM and native from one source | Merge conformance suite in full | A candidate is admissible only by passing it (AD-12) |
| **`Yello.Contracts`** | Shared wire DTOs across both surfaces | Contract snapshots for every served version | C-1, C-2 fail the build on a breaking change |
| **The bound registry** | One choke point for all six NFR-8 bounds | Six refusal cases + registry completeness | A-7 fails on a registry missing any bound |
| **AD-24's enumerated surfaces** | Exactly two Account-scoped surfaces; adding a third requires amending the AD | I-11 | Amendment due before epic 3 changes what I-11 asserts |

**Regression strategy.** Every PR runs the four gating suites in full — the project is small
enough that selective execution buys little and risks the wrong thing. The one exception worth
making is the E2E tier, which is nightly by cost rather than by importance; a change touching
the Board's rendering or the description editor should pull X-4, X-5, X-6 and X-10 forward into
the PR run.

---

## Appendix A: Conventions & Tagging

The templates' examples assume `@playwright/test` and TypeScript. This project is .NET with
xunit.v3, so the equivalents are below.

**Trait-based selection** (xunit.v3's analogue of Playwright tags):

```csharp
[Fact]
[Trait("Priority", "P0")]
[Trait("Suite", "Isolation")]
[Trait("Requirement", "FR-15")]
[Trait("Assumption", "PRD-12-2")]   // R10: reversing assumption 2 shows up as a known set
public async Task Reading_a_Task_in_a_Space_without_Membership_returns_404() { }
```

**Both surfaces as a parameter, not a duplicated file** (AD-4 — a case existing for one
surface and not the other is a gap, not a choice):

```csharp
[Theory]
[Trait("Priority", "P0")]
[InlineData(Surface.Browser)]
[InlineData(Surface.ApiToken)]
public async Task Foreign_Space_resource_is_indistinguishable_from_absent(Surface surface) { }
```

**Selective runs:**

```bash
dotnet test --filter "Priority=P0"
dotnet test --filter "Priority=P0|Priority=P1"       # matches risk_threshold: p1
dotnet test --filter "Suite=Isolation"
dotnet test --filter "Assumption~PRD-12"             # every test resting on an unconfirmed assumption
```

**Definition of done for a test in this project** (from `test-quality.md`, adapted):

- No `Task.Delay` as a synchronisation mechanism — this is exactly the failure mode R1 exists
  to prevent, and it applies beyond the sync channel
- One behaviour per test; the name states the behaviour, not the method under test
- Deterministic data: randomised where identity does not matter, fixed where an assertion
  depends on it, and **never a shared email address** (FR-1's uniqueness makes that a
  cross-test collision)
- Cleanup by transaction rollback or container disposal, not by delete statements that
  themselves need RLS context
- A test asserting the absence of a signal (I-6, I-7) must be validated against a planted
  signal, or it is not a test

## Appendix B: Knowledge Base References

- `risk-governance.md` — scoring, gate decisions, traceability
- `probability-impact.md` — the 1–3 × 1–3 scale and DOCUMENT/MONITOR/MITIGATE/BLOCK thresholds
- `test-priorities-matrix.md` — P0–P3 criteria
- `test-levels-framework.md` — level selection and the duplicate-coverage guard
- `nfr-criteria.md` — NFR categories and tool selection
- `adr-quality-readiness-checklist.md` — the 8-category / 29-criteria testability frame

---

**Generated by:** BMad TEA Agent (Murat)
**Workflow:** `bmad-testarch-test-design` · system-level mode, sequential execution
**Companion:** `test-design-architecture.md`
