---
title: 'TEA Test Design → BMAD Handoff Document'
version: '1.0'
workflowType: 'testarch-test-design-handoff'
sourceWorkflow: 'testarch-test-design'
generatedBy: 'TEA Master Test Architect (Murat)'
generatedAt: '2026-08-22'
projectName: 'YelloBMAD'
mode: 'system-level'
inputDocuments:
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/implementation-artifacts/sprint-status.yaml'
---

# TEA → BMAD Integration Handoff

## Purpose — and a correction to the template's assumption

The template positions this document as input to `bmad-create-epics-and-stories`. **On this
project that workflow has already run and its output is final** — `epics.md` carries
`status: final` with 8 epics and 53 stories, and sprint planning has produced
`sprint-status.yaml`. So this handoff has no upstream consumer left.

It is therefore retargeted at the workflow that *will* read it: **`bmad-create-story`**, which
builds each story file, and the **epic-level TD passes** that will run per epic. Paths are
given explicitly below rather than left to discovery, because these documents sit outside the
directory patterns those skills search by default.

## TEA Artifacts Inventory

| Artifact | Path | Integration point |
| --- | --- | --- |
| Architecture-facing test design | `_bmad-output/test-artifacts/test-design-architecture.md` | Blockers, risk register with mitigation plans, testability concerns, ASRs |
| QA-facing test design | `_bmad-output/test-artifacts/test-design-qa.md` | Scenario-level coverage matrix, priorities, execution strategy, entry/exit criteria |
| Working notes / audit trail | `_bmad-output/test-artifacts/test-design-progress.md` | How each conclusion was reached, step by step |
| This handoff | `_bmad-output/test-artifacts/test-design/YelloBMAD-handoff.md` | Risk-to-story mapping, per-story acceptance criteria to embed |

**Note the path split:** `_bmad/tea/config.yaml` sets
`test_design_output: _bmad-output/test-artifacts/test-design`, but step 5 of the workflow
writes the two main documents to `{test_artifacts}` directly. Both locations are in use — the
two design documents at the `test-artifacts` root, this handoff in the `test-design/`
subfolder. Stated so nobody concludes a file is missing.

## Epic-Level Integration Guidance

### Risk references — which epic owns which risk

| Epic | Risks becoming live | Epic-level quality gate to add |
| --- | --- | --- |
| **1** — Account, Space, boundary | R2 (timing method), R8 (CORS/anti-forgery), R10 (assumption tagging), R11 (§6.4 tripwire), R17 (case list), R5 (confirm AR-40c before deploy) | The isolation suite exists, enumerates its cases **as data**, and runs every case on both surfaces. SM-1 first goes green here |
| **2** — Board for a solo user | R3 (warm/cold), R4 (unbounded DOM), R7 (collation), R13 (load-test grant), R16 (browser binding) | The Board is measured at the real 5,000-Task bound, after paging to the end of a column — not at first paint |
| **3** — Several Spaces | — (inherits epic 1's suite) | AD-24's amendment lands before this epic, and I-11's assertions are updated with it |
| **4** — Invitations, Memberships, Roles | R2 (invitation-issue disclosure arm) | The FR-16 Role matrix is exercised at INT with someone other than an Owner in the room |
| **5** — Ownership handover | R9 (swap ordering), R12 (expiry seeding) | The never-zero-or-two-Owners invariant is asserted on observable state after every ownership operation |
| **6** — Status deltas | — | Atomicity under forced mid-operation failure, including FR-27's per-Project exceptions |
| **7** — Collaborative editing | **R1 (score 9)**, R6 (conformance falsifiability), R14 (compaction), R15 (16 ms render) | **The conformance suite is green before any candidate algorithm exists, and story 7.3 delivers an interleaving seam that can force both orderings.** SM-2 gates release here |
| **8** — API contract | — | Contract snapshots locked per served version; the parity audit finds nothing refused in the browser that succeeds via the API |

### Quality gates per epic

Beyond each epic's own acceptance criteria, three gates apply from epic 1 onward and should be
restated in every epic's definition of done:

1. **The architecture suite is green.** AD-21 makes it a build gate, so a violation is a
   failure, not a review comment.
2. **The isolation suite is green in full.** Not the subset touching this epic — the whole
   suite, because a new Space-scoped table without an RLS policy is a hole regardless of which
   epic added it.
3. **No test synchronises on `Task.Delay`.** This is R1's failure mode generalised, and it is
   cheaper to enforce as a convention from story 1.1 than to unpick later.

## Story-Level Integration Guidance

### P0 test scenarios that must become story acceptance criteria

These are the scenarios where the test *is* the requirement. If they are not acceptance
criteria on the named story, they will be written late or not at all.

| Story | Scenario | Why it must be an AC rather than a follow-up |
| --- | --- | --- |
| **1.2** | X-1, X-2 — 18 gated contrast pairs computed from the 30 tokens in both themes; the 2 surface-adjacency ratios **excluded** | The harness needs no browser, so it is not blocked on the B5 binding decision. Gating the 2 adjacency ratios would fail the build permanently |
| **1.3** | I-7 — registration identical in status, body **and duration** for a known vs unknown address | AD-23 requires the hash to run even for an existing address. Retrofitting that changes the endpoint's shape |
| **1.3** | The NFR-6 password work factor is **chosen and recorded**, and changeable without re-registering existing Accounts | Currently unspecified — "the architecture's call", never made |
| **1.4** | R8 — wildcard origin rejected, reflected `Origin` rejected, state change without an anti-forgery token refused, no credential in web storage | `SameSite=None` has already removed the implicit CSRF protection |
| **1.5** | B2 — the RLS seeding strategy is stated, and it is one of the three named shapes | Every isolation test depends on it; discovering it in 1.9 means rewriting fixtures |
| **1.6** | I-5, I-6 — boundary 404 and in-Space not-found identical in body **and duration**, with the statistical method stated | A single-sample assertion cannot detect the oracle it exists to detect |
| **1.9** | B6 — the case list held **as data**, with the surface cross-product generated so a missing pair fails | SM-1's claim is otherwise unfalsifiable |
| **1.9** | I-8 — pooled-connection reuse in its own collection, parallelism disabled, pool size 1 | The case guarding a silent tenancy leak must not be the one that gets quarantined for flakiness |
| **1.9** | An explicit decision on whether `/sync` is a third isolation surface | It currently falls between the isolation and revocation suites |
| **1.10** | R11 — the §6.4 single-operator position, the condition that ends it, and all five prerequisites | The gate is stated as testable and nothing counts Accounts |
| **1.10** | R5 — Azure SQL's `SESSION_CONTEXT` exposure confirmed before first deploy | Relaxing `MAXDOP = 1` without this is how a silent cross-tenant read ships |
| **2.2** | Task cards carry a stable Task identifier attribute (see below) | X-5's focus-identity test cannot assert *which* Task holds focus without it |
| **2.6** | A-6 — collation asserted in the schema test on the key column *and* the AD-29 index, with a mixed-case ordering case | `ALTER DATABASE … COLLATE` is unsupported on Azure SQL |
| **2.8** | X-4 — every Board pointer operation has a keyboard equivalent, including cross-column move | NFR-9 states it; six operations, each its own case |
| **2.9** | P-2, X-5 — measured at 5,000 Tasks **after paging to the end of a column**, asserting both the latency budget and keyboard reach of the last Task | First paint passing tells you nothing about the case AD-29 was written for |
| **2.9 / 1.10** | B4 — the AR-40b warm/cold decision recorded in the spine | "State it, do not leave it silent" is the spine's own instruction |
| **2.10** | P-3, P-4 — keyset correctness at every offered sort with ties and NULLs; every sort has a matching composite index | AD-29: a sort offered without its index is a defect, not a slow query |
| **5.2** | S-2 — no Space ever holds zero or two Owners, on observable state; two acceptances racing on one offer | EF chooses its own statement order for two tracked rows |
| **5.3** | R12 — the expiry-seeding convention stated once; AD-27's ArchUnit rule given a concrete predicate | Otherwise the rule is unimplementable and quietly omitted |
| **7.1** | M-3 — whole-field last-writer-wins **fails** the conformance suite, asserted | This is what makes the suite a contract rather than a description |
| **7.1** | M-9 — a property-based interleaving harness over randomised operation orders | A CRDT's failure modes live in orderings nobody enumerates by hand |
| **7.3** | **B1/R1 — a seam that lets a test force both orderings across the removal commit, verified by a 100× burn-in** | **The only score-9 risk. Story 7.3 is not done without it, and 7.7/7.8 inherit a race if it is missing** |
| **7.7 / 7.8** | V-4…V-8 — editor inert ≤ 1 s unprompted; unsynchronised changes not applied; admitted changes retained; a retried frame discarded, not queued | SM-2 gates release on these in 100% of tested cases |
| **8.2** | C-1, C-2 — snapshot locks the shape; no field removed, renamed or retyped and no input narrowed, each its own assertion | FR-37 is a published contract, so a breaking change must fail the build |
| **8.3** | FR-38 rate-limit values chosen and recorded | Currently unspecified; nothing can be asserted without a number |

### Selector and testability requirements

Blazor rather than React, so the template's `data-testid` convention needs adapting. Two rules,
and the second is a genuine architectural requirement rather than a preference:

1. **Prefer role + accessible name as the primary selector.** Because NFR-9 requires WCAG 2.1
   AA on the five named flows anyway, a selector built on accessible name doubles as an
   accessibility assertion — a locator that stops resolving is usually an accessibility
   regression, which is exactly the signal you want. This is cheaper than maintaining a
   parallel `data-testid` vocabulary.

2. **Task cards must carry a stable Task identifier attribute** (`data-task-id` or equivalent).
   This is not a testing convenience. AD-29's central claim is that appending rather than
   recycling prevents focus from silently re-pointing at a *different* Task — and verifying it
   requires asserting the identity of whatever holds focus before and after a page. Role and
   accessible name are not sufficient: two Tasks may legitimately share a title. Without the
   attribute, X-5 can only assert that *something* has focus, which is precisely the vacuous
   test that lets the defect through. **Recommend adding this to story 2.2's acceptance
   criteria.**

3. Board columns should expose their Status identity and the AD-29 true total (`aria-setsize`)
   as inspectable state, since X-6 asserts the count chip and the ARIA total agree.

## Risk-to-Story Mapping

| Risk ID | Category | P×I | Story / epic | Test level |
| --- | --- | :-: | --- | --- |
| **R1** | TECH | **3×3 = 9** | **Story 7.3** (seam), consumed by 7.7 / 7.8 | INT |
| R2 | SEC | 2×3 = 6 | Stories 1.3, 1.6 | INT |
| R3 | PERF | 3×2 = 6 | Story 2.9 / 1.10 (AR-40b) | LOAD |
| R4 | PERF | 2×3 = 6 | Story 2.9 | LOAD + E2E |
| R5 | DATA | 2×3 = 6 | Story 1.10 (AR-40c), guarded by story 1.9's I-8 | INT |
| R6 | TECH | 2×3 = 6 | Story 7.1 | UNIT |
| R7 | DATA | 2×3 = 6 | Story 2.6, schema assertion seeded in 1.1 | ARCH |
| R8 | SEC | 2×3 = 6 | Story 1.4 | INT |
| R9 | DATA | 2×3 = 6 | Story 5.2 | INT + ARCH |
| R10 | BUS | 3×2 = 6 | All stories (tagging convention) | All |
| R11 | SEC | 2×3 = 6 | Story 1.10 | INT + story AC |
| R13 | OPS | 2×2 = 4 | Story 2.9 | LOAD |
| R14 | DATA | 2×2 = 4 | Epic 7 (before compaction) | INT |
| R15 | PERF | 2×2 = 4 | Story 7.4 | E2E |
| R16 | TECH | 2×2 = 4 | TF run, consumed by 2.8 / 2.9 | E2E |
| R17 | TECH | 2×2 = 4 | Story 1.9 | INT |
| R12 | TECH | 3×1 = 3 | Story 5.3 | ARCH |
| R18 | OPS | 2×1 = 2 | Documented; no story | — |

## Recommended workflow sequence from here

The template's sequence assumes epics have not been created. Corrected for this project's
actual position:

1. ✅ **TEA Test Design** (`TD`) — this run. Produced two documents and this handoff
2. ✅ **BMAD Create Epics & Stories** — already complete and final (8 epics / 53 stories)
3. ✅ **BMAD Sprint Planning** — complete (`sprint-status.yaml`)
4. → **BMAD Create Story** (`CS`) for story 1.1 — **the immediate next step.** Hand it the two
   design documents explicitly; it will not find them by its own search patterns
5. Optionally **TEA Test Framework** (`TF`) — resolves blocker B5, the browser-test binding. Note
   the overlap: story 1.1 *is* the solution skeleton and its four build gates, so TF and 1.1
   collide unless one defers to the other
6. **TEA ATDD** (`AT`) per story — red-phase scaffolds, now with P0 scenarios already named per
   story above
7. **BMAD Dev Story** (`DS`) → **Code Review** (`CR`) → next `CS`
8. **TEA Automate** (`TA`) / **Test Review** (`RV`) as suites grow
9. **TEA NFR Assessment** (`NR`) — the PRD explicitly schedules NFR-8's bound verification here
10. **TEA Trace** (`TR`) — coverage traceability and the gate decision

## Phase Transition Quality Gates

| From | To | Gate criteria |
| --- | --- | --- |
| Test Design | Story Creation | Blockers B2, B3 and B6 have owners on epic-1 stories. All ten score-≥6 risks have a mitigation plan (they do — see the Architecture doc) |
| Story Creation | ATDD | Each story carries the P0 scenarios named for it above as acceptance criteria, not as a follow-up task |
| ATDD | Implementation | Failing acceptance tests exist for every P0 scenario in that story. For story 7.3, the interleaving seam exists and a 100× burn-in is deterministic |
| Implementation | Test Automation | All acceptance tests pass; the architecture suite is green; the full isolation suite is green |
| Test Automation | Release | **SM-1: zero verified cross-Space disclosures** (not a percentage — one blocks release). **SM-2: 100% of tested revocation cases**, including sessions holding unsynchronised edits. Merge conformance green with LWW failing it. All six NFR-8 bounds enforced. 18 contrast pairs passing in both themes |

**Deliberate deviation from the template.** Its final row reads "trace matrix shows ≥80%
coverage of P0/P1 requirements". That threshold is wrong for this product: NFR-1 is stated as
the one requirement with no acceptable failure rate, and SM-1 gates release on *zero* verified
disclosures. 80% of the isolation suite passing is a failed release, not a qualified one. The
percentage gate is kept for P1 and replaced by absolutes for the two gating metrics.
