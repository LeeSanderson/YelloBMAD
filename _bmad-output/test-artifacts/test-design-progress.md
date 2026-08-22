---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-08-22'
mode: 'system-level'
detectedStack: 'fullstack (.NET 10 / Blazor WASM — declared, not detected)'
inputDocuments:
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/addendum.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/implementation-artifacts/sprint-status.yaml'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/adr-quality-readiness-checklist.md'
  - 'resources/knowledge/nfr-criteria.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/risk-governance.md'
  - 'resources/knowledge/probability-impact.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# Test Design Progress — YelloBMAD

## Step 1: Mode Detection & Prerequisites

### Mode: System-Level

**Decision:** System-Level test design (PRD + ADR → test architecture + QA documents).

**Why.** Both detection branches in the mode-detection priority order fired, so the
tie was resolved by asking the user rather than by file precedence:

- Rule B (file-based) pointed at Epic-Level, because
  `_bmad-output/implementation-artifacts/sprint-status.yaml` exists.
- Rule A (user intent) pointed at System-Level, because all four planning spines are
  present — PRD + addendum, architecture spine with AD-1..AD-29, and the UX pair — and
  the rule prefers System-Level first when PRD/ADR *and* epics/stories both exist.

The user invoked the workflow with no scope stated, so intent was unclear and rule B
would have applied by default. Presented both scopes with their concrete deliverables;
Lee selected System-Level. Epic-level passes remain available per epic afterwards and
will be able to read these outputs as prior system-level context.

**Project position at time of run:** end of phase 3-solutioning / start of phase
4-implementation. Sprint planning is complete, no story files exist yet, all 8 epics
and 53 stories are at `backlog`, and no code or tests exist in the repository. So this
run is a greenfield test design with no existing coverage to audit.

### Prerequisite Check — System-Level Mode

| Requirement | Status | Source |
| --- | --- | --- |
| PRD (functional + non-functional requirements) | present | `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md` (+ `addendum.md`) |
| ADR / architecture decision records | present | `ARCHITECTURE-SPINE.md`, AD-1..AD-29 |
| Architecture / tech-spec document | present | same spine document |
| Epics (for scope) | present | `_bmad-output/planning-artifacts/epics.md` (status: final, 8 epics / 53 stories) |

No halt conditions triggered.

### Supporting inputs available (not required by the mode, carried as context)

- `implementation-readiness-report-2026-08-22.md` — READY, 43/43 FR coverage, 5 open
  non-blocking issues, of which AR-40a/b/c and the six NFR-8 bounds are directly
  test-relevant.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — records four staged FRs
  (FR-13, FR-14, FR-23, FR-30) that complete in a later epic than the one they map to.
  These are traceability hazards for any coverage claim made per epic.
- UX pair `DESIGN.md` + `EXPERIENCE.md` — carries the accessibility and contrast gates
  (30 colour tokens, 18 gated contrast pairs) that are build-gate assertions, not
  advisory design notes.

## Step 2: Context & Knowledge Loaded

### Configuration (`_bmad/tea/config.yaml`)

| Flag | Value | Consequence for this design |
| --- | --- | --- |
| `test_stack_type` | `auto` | Auto-detection found **nothing** — see stack detection below |
| `tea_use_playwright_utils` | `true` | Flagged as a mismatch — see below |
| `tea_use_pactjs_utils` | `false` | Correct. Yello has no provider/consumer pair; the API is inbound-only (PRD §9.2 rules out webhooks), so contract testing here is snapshot-shaped, not Pact-shaped (AD-19) |
| `tea_pact_mcp` | `none` | No Pact MCP fragments loaded |
| `tea_browser_automation` | `auto` | Browser exploration **skipped** — there is no running application to explore |
| `risk_threshold` | `p1` | P0 and P1 scenarios are in scope for gating |
| `test_artifacts` | `_bmad-output/test-artifacts` | Output root |

### Stack detection — declared, not detected

Auto-detection scanned `{project-root}` for frontend and backend indicators and found
**none**: no `package.json`, `*.csproj`, `*.sln`, `pyproject.toml`, `go.mod`,
`playwright.config.*` or `cypress.config.*`. The repository currently holds only
`.claude/`, `_bmad/`, `_bmad-output/` and `docs/`. Nothing is built.

`{detected_stack}` is therefore taken from the architecture spine's pinned stack rather
than from the filesystem: **fullstack** — .NET 10.0.11, ASP.NET Core / Blazor
WebAssembly 10, EF Core 10, Azure SQL Database (serverless, free offer), Azure
Container Apps (max 1 replica), Azure Static Web Apps, Azure Communication Services
Email. Test tooling is already pinned too: **xunit.v3 4.0.0**,
**Testcontainers.XunitV3 4.6.0** against `mcr.microsoft.com/mssql/server:2025-latest`,
and **TngTech.ArchUnitNET 0.13.3**.

**Consequence:** every scenario in this design must be expressible in that stack. It
also means step 3's "analyse existing test coverage" has nothing to analyse — there is
no baseline, no flaky history, and no fixture patterns to inherit. This is a
from-scratch design, which is the best case for the four gating suites and the worst
case for estimating effort.

### ⚠️ Configuration mismatch — `tea_use_playwright_utils`

`tea_use_playwright_utils: true` selects the **Full UI+API profile**, whose fragments
(`api-request`, `auth-session`, `recurse`, `intercept-network-call`, …) are TypeScript
helpers for `@playwright/test`. The pinned stack is .NET with xUnit v3. Playwright has
a supported .NET binding, so browser automation itself is available and is the right
answer for NFR-9 and the Blazor client — but the **TEA Playwright Utils helpers are not
consumable from C#**, and the auth-session and api-request patterns they encode are
already served by Testcontainers plus `WebApplicationFactory` in this stack.

Recorded rather than silently worked around: the fragments were loaded but their code
patterns are treated as *design guidance only*, not as a library this project will take
a dependency on. Either `tea_use_playwright_utils` should be set `false` and browser
coverage specified directly against Playwright for .NET, or the two Blazor-facing
suites accept a separate TypeScript test project. That is a decision for the framework
run (TF), and it is carried into the risk register rather than assumed here.

### Artifacts loaded (system-level mode)

| Input | What was extracted |
| --- | --- |
| `prd.md` (884 lines) | FR-1..FR-43, NFR-1..NFR-9 with thresholds, §6.1–§6.4 constraints, §7 surfaces, §10 SM-1/SM-2 gating metrics + SM-3..SM-6 behavioural, §12's 13 unconfirmed assumptions |
| `addendum.md` (169 lines) | Rejected alternatives, the ownership-trap reasoning, the four architectural obligations, the 2026-08-18 rejection list |
| `ARCHITECTURE-SPINE.md` (434 lines) | AD-1..AD-29, consistency conventions, pinned stack, source tree with 5 named test projects, capability→architecture map, 11 deferrals |
| `epics.md` (2,822 lines) | 8 epics / 53 stories, the FR coverage map, the Story Coverage Index, AR-1..AR-40 and UX-DR1..UX-DR42 identifiers |
| readiness report | READY verdict, 43/43 FR coverage, 5 open non-blocking issues |
| `DESIGN.md` | Verified contrast table (both themes, computed not estimated), the two non-contrast adjacency pairs, the density position, the `readonly` FR-34 editor state |
| `sprint-status.yaml` | The four staged FRs and their true completion stories |

### Integration points identified

Azure SQL (RLS via `SESSION_CONTEXT`, `MAXDOP = 1`), Azure Communication Services
Email (the only outbound dependency), Key Vault via managed identity, the `/sync`
WebSocket, and the browser↔API cross-origin boundary (distinct origins, `SameSite=None`
cookie, anti-forgery on every state change). No inbound third-party dependency exists
in v1 — OAuth is deferred (PRD §9.2), which is why FR-1/FR-2 carry no
provider-outage handling.

### Existing test coverage

**None.** No test project, no fixture, no CI pipeline. The architecture already names
the five projects that must exist (`Yello.Tests.Isolation`, `.Revocation`, `.Merge`,
`.Architecture`, `.Slices`) and the four suites that gate release, so the target shape
is fixed even though nothing is written.

## Step 3: Testability Review & Risk Assessment

### 🚨 Testability Concerns

Ordered by how much they cost to fix later. Each names the AD that causes it, because
none of these is an oversight — they are consequences of decisions that are correct for
production and awkward for testing.

---

**T1 — There is no seam to interleave a revocation with an in-flight sync frame.**
*Controllability. Blocks the requirement the PRD says the product should be judged on.*

FR-34 requires that a frame authored after removal is discarded, and that a frame
admitted before it is retained. AD-9 delivers `MembershipChanged` **in-process at the
transaction boundary**, and AD-8 checks every inbound frame against the lease. The
assertion that matters is about *ordering across two concurrent paths*: a frame must be
in flight at the moment the removal transaction commits.

Nothing in the architecture names a way to hold a frame at that point. Without one, the
test for stories 7.7 and 7.8 becomes `Task.Delay`-shaped — which is either flaky or
passes vacuously by always landing on the same side of the race. SM-2 gates release on
this in 100% of tested cases, so a vacuous test here is worse than no test: it reports
green on the one thing that must not be wrong.

**What is needed:** a deterministic interleaving seam specified in **story 7.3** (the
sync channel), before 7.7/7.8 consume it. A test-only barrier the frame handler awaits,
or an injectable ordering point around lease invalidation. Naming it in 7.3 costs
almost nothing; retrofitting it after 7.7 means rewriting the sync handler's
concurrency shape.

---

**T2 — Time cannot be advanced, so lapse-by-time is only reachable by writing the past.**
*Controllability. Affects FR-8, FR-11, FR-39, FR-42.*

AD-27 is explicit: the expiry predicate is evaluated **server-side inside the guarded
statement's own `WHERE` clause, against the database clock** — never loaded into memory
and checked in C# first. That is the right call (it removes a race and a second clock
source) and it removes the usual test lever: there is no `IClock` to fake, because the
decision is made by SQL Server.

Two consequences. First, every lapse test must seed `ExpiresAt` in the past rather than
advance time forward — acceptable, but it means the test never exercises the same code
path a real 7-day lapse takes, only the same predicate. Second, AD-27 also says the
architecture suite must **fail the build on a scheduled component writing a terminal
expiry state**. That is an ArchUnit assertion about absence, and absence is exactly what
ArchUnit is weakest at expressing — it needs a definition of "scheduled component" to
match against.

**What is needed:** the seeding convention stated once (in story 5.3, which owns
decline/revoke/lapse) rather than reinvented per test, and the ArchUnit rule for AD-27
given a concrete predicate — a type implementing `IHostedService`/`BackgroundService`
that writes to `OwnershipOffer.State` or `Invitation.State`.

---

**T3 — Two requirements demand duration-indistinguishability, and neither has a method.**
*Observability + reliability. This is the concern most likely to ship a vacuous test.*

AD-3 requires that a Space-boundary 404 and an in-Space not-found be
indistinguishable **by duration as well as by body**, and puts a timing case in the
isolation suite. AD-23 requires that registration, authentication and Invitation issue
be identical in duration whether or not the address is known — a registration attempt
for an existing address still performs the password hash it would otherwise skip.

Both are statistical claims. A single-sample `Assert.True(Math.Abs(a - b) < someMs)` is
not a test of indistinguishability; it is a test of one draw from two distributions, and
it will either flake or pass regardless of a real oracle. Neither the PRD nor the spine
states a sample size, a tolerance, a statistic, or where the measurement is taken
(server-side stopwatch vs wall-clock round trip).

**What is needed:** a stated method before story 1.6 (which owns boundary refusal) and
story 1.3 (registration). Concretely: N samples per arm, compare distributions rather
than means, assert the difference is below a tolerance derived from observed variance,
and take the measurement server-side so network jitter is not the dominant term. Also
worth stating: with `MAXDOP = 1` and a single replica the variance is unusually low,
which makes this *more* tractable here than in most systems — but only if the method is
written down instead of improvised twice.

---

**T4 — RLS makes multi-Space seeding a first-class design problem, not a fixture detail.**
*Controllability.*

Every isolation test needs at least two Spaces populated, then a request made as an
Account holding Membership in only one. But AD-2 filters every Space-scoped table on
`SESSION_CONTEXT('SpaceId')`, set per unit of work from `ActiveSpaceContext` and never
from a client value. Seeding Space B's data while the session context says Space A will
silently write nothing visible, or fail — and AD-2 forbids raw SQL bypassing the global
query filters outside `Infrastructure`.

So the seeding strategy has exactly three shapes, and the choice belongs in story 1.5
(which introduces the session wiring) rather than being discovered in story 1.9: seed
through the application per Space with the context switched between blocks; seed as a
principal the RLS policy exempts (`db_owner`, or a policy predicate that admits a
seeding role); or seed before the policies are created and rely on migration ordering.
The second is the usual answer and it needs stating, because a seeding role that bypasses
RLS is itself a hole if it ever reaches production.

---

**T5 — The pooled-connection reuse case is deliberately hostile to test parallelism.**
*Reliability.*

AD-2 requires an isolation case where **two requests for different Spaces are served
consecutively on one physical connection** — the exact leak `sp_set_session_context`
per-unit-of-work guards against. Reproducing it means controlling ADO.NET connection
pooling: forcing reuse, pinning pool size to 1, and guaranteeing no other test shares
that pool. That is the opposite of what xUnit v3's default parallelism wants.

**What is needed:** this case isolated into its own collection with parallelism
disabled, and a connection string whose pool is scoped to that collection. Cheap if
planned in story 1.9; a source of intermittent cross-test interference if not.

---

**T6 — "Every case on both surfaces" has no case list, so the suite's completeness is unfalsifiable.**
*Observability of coverage.*

AD-4 says the isolation suite runs every case against browser and API, and that a case
existing for one and not the other is a gap rather than a choice. AD-21 makes the
dependency rule a build gate. But nothing enumerates *what the cases are*. SM-1 gates
release on "zero verified cross-Space disclosures" — a claim about a suite whose
membership is undefined, which means the suite can be complete by construction at any
size, including too small.

**What is needed:** an enumerated case list held as data rather than as prose, so
"every case on both surfaces" becomes a cross-product the test framework generates and
a missing pair fails rather than goes unnoticed. Story 1.9 owns this. The same pattern
the last commit applied to NFR-8's bound registry — enumerate all six, fail on a
registry missing any — applies here for the same reason.

---

**T7 — Cold start is uncontrollable, and it is the term that decides whether NFR-5 passes.**
*Controllability + a genuinely open decision (AR-40b).*

Container Apps scale-to-zero plus Azure SQL auto-pause at 15 minutes means most
requests are cold under sparse traffic, against a 300 ms p95 read budget on 0.5 vCore
with `MAXDOP = 1`. An Azure SQL serverless resume alone is measured in seconds, not
milliseconds — so a cold p95 does not miss the budget marginally, it misses it by an
order of magnitude.

The spine defers this ("when NFR-5 is first measured") with two named mitigations: pin
min replicas to 1, or state that NFR-5 is measured warm and exempt the cold path. **The
test design cannot proceed on this without the answer**, because "measured warm" and
"measured cold" are different tests with different harnesses. This is recorded as risk
R3 and as an UNKNOWN threshold rather than guessed.

---

**T8 — The load-test target environment is undefined, and the free tier cannot absorb it.**
*Reliability + OPS.*

NFR-8's verification was rescheduled to the NFR-evidence audit (PRD §11 Q4), and the
bounds are confirmed final. But exercising 5,000 Tasks per Project, 10 concurrent
editors and 50 concurrent Sessions against the Azure deployment would consume a
material share of the 100,000 vCore-s monthly free grant — and the operations
convention sets `Behavior when free limit reached` to **auto-pause until next month**,
never paid overage. A load test that exhausts the grant does not cost money; it takes
the environment away until the month rolls over.

**What is needed:** load tests targeted at the Testcontainers SQL Server locally, with
the Azure run reserved for a single deliberate measurement window, and the vCore cost
of that window estimated before it runs rather than after.

---

**T9 — The Blazor client has no named test level, and two invariants live only there.**
*Controllability.*

The source tree names five test projects, all server-side. Two AD-29 invariants are
client-side and cannot be observed from the server at all:

- **Rows are appended and never recycled**, because a recycled row re-points keyboard
  focus at a different Task — "a data-corrupting defect reachable only by keyboard, and
  invisible to pointer testing", in the spine's own words.
- **`aria-setsize` carries the true total and `aria-posinset` the true ordinal**, agreeing
  with the visible count chip.

Both are assertions about rendered DOM and focus identity. Neither xUnit nor ArchUnit
can reach them. This is where the `tea_use_playwright_utils` mismatch actually bites: the
right tool is browser automation, and the decision about which binding (Playwright for
.NET vs a separate TypeScript project) determines whether these invariants are tested at
all. Story 2.8 (whole Board from the keyboard) and story 2.9 (the Board at five thousand
Tasks) both depend on the answer.

---

**T10 — There are no RED metrics, and AD-10 constrains how that can be fixed.**
*Observability.*

Liveness and readiness answer from process state without a database round trip, and
nothing may query the database on an unconditional timer. There is no `/metrics`
endpoint and no rate/error/duration series. The one operational signal specified is a
metric alert at 10% of the free vCore allowance remaining, plus rate-limit refusals.

For a single-operator v1 this is a defensible position rather than a defect — but it
means NFR-5's p95 has **no production evidence source**. It can be measured in a load
test and never again. Recorded as a gap against checklist 6.3 with the AD-10 constraint
attached, not as something to "just add".

---

### ✅ Testability Assessment Summary — what is already strong

This architecture is markedly more testable than most at this stage, and it is worth
being specific about why, because several of these are load-bearing for the coverage
plan being affordable.

| Strength | Why it matters for testing |
| --- | --- |
| **`AccessRefusal` is written by the pipeline for every 403 and boundary 404** (AD-20), carrying acting Account, target Space, capability, outcome and a kind of `CrossSpace` / `InsufficientRole` | SM-1 and NFR-7 get a **deterministic table to assert against** instead of inferring refusal from a status code. The `kind` discriminator is precisely the distinction NFR-7 requires, already materialised |
| **`MAXDOP = 1` and a single replica** (AD-2, AD-14) | Removes plan-shape and replica-timing nondeterminism. Latency variance is unusually low, which is what makes T3's timing assertions tractable at all |
| **Integration tests run against real SQL Server via Testcontainers; the in-memory provider is forbidden** (Tests convention) | RLS is actually exercised. An in-memory provider cannot express a row-level security policy, so the ban is what keeps the isolation suite honest rather than decorative |
| **Two independent isolation layers** — RLS on `SESSION_CONTEXT` and EF global query filters derived from application state (AD-2) | Neither layer alone carries NFR-1, so a test can disable one and assert the other still holds. That is a rare and valuable property: the invariant is *falsifiable layer by layer* |
| **The merge port's contract *is* an executable conformance suite, written before any implementation** (AD-12) | Inverts the usual order. The suite cannot be retrofitted to the algorithm's behaviour because it predates it — provided it is written against FR-31/FR-33/NFR-4 and not against a candidate |
| **Append-only `TaskDescriptionChange` with an in-transaction projection** (AD-13) | State is replayable and a read after an admitted write is never stale. Makes convergence assertions deterministic rather than eventually-consistent |
| **Composite foreign keys carrying `SpaceId`** (AD-2) — an Assignee is `(SpaceId, MembershipId)` | FR-21's same-Space constraint holds **by construction**. This deletes a whole class of test rather than requiring one: there is no way to express the violation |
| **`{spaceId}` in every Space-scoped route, enforced by an architecture test** (AD-3) | No bare-id routes means no deep-link case to test, and the timing oracle AD-3 worries about is removed structurally instead of mitigated |
| **One pipeline for both surfaces; no slice branches on calling surface** (AD-4) | The both-surfaces requirement is satisfied by **parameterising** the suite, not by writing it twice |
| **Bounds declared in one registry and checked by the pipeline** (AD-25) | Six bounds, one choke point, one place to assert. The registry itself is assertable — as the last commit established, a registry missing any of the six fails the architecture suite |
| **ArchUnitNET gates the dependency rule** (AD-21) | The paradigm cannot erode one story at a time, and cross-cutting re-implementation (a slice re-doing authorisation) is a build failure rather than a review finding |

### Architecturally Significant Requirements (ASRs)

| ASR | Source | Threshold / gate | Status |
| --- | --- | --- | --- |
| Absolute Space isolation, both surfaces, no acceptable failure rate | NFR-1, SM-1 | Zero verified disclosures; blocks release | **ACTIONABLE** |
| Revocation on the request path — the very next request, no tolerance | NFR-2, SM-2 | Next request | **ACTIONABLE** |
| Revocation on a live session — 1 s from the transaction boundary, without the affected participant acting | NFR-2, FR-34, SM-2 | 1 s | **ACTIONABLE** |
| Unsynchronised local changes are never applied after revocation, however long propagation takes | FR-34 | Absolute, independent of both timings | **ACTIONABLE** |
| Boundary refusal indistinguishable by body **and duration** | AD-3 | Method undefined — see T3 | **ACTIONABLE** |
| Account existence never disclosed; responses identical in status, body **and duration** | AD-23, §6.1 | Method undefined — see T3 | **ACTIONABLE** |
| Merge conformance suite passes before any implementation merges | AD-12, AR-40a | Suite green; closes before epic 7 | **ACTIONABLE** |
| Six NFR-8 bounds enforced as refusals at one choke point | NFR-8, AD-25 | 50 / 100 / 50 / 5,000 / 10 / 50 | **ACTIONABLE** |
| Board and List View hold NFR-5 and NFR-9 at 5,000 Tasks | FR-28, FR-30, AD-29 | 300 ms p95 read; WCAG 2.1 AA | **ACTIONABLE** |
| Keyset pagination only, no `OFFSET`; DOM appended, never recycled | AD-29 | Structural | **ACTIONABLE** |
| Every List View keyset carries `TaskId` as mandatory tiebreaker, with a matching composite index | AD-29 | A sort offered without its index is a defect | **ACTIONABLE** |
| Position key column collated `Latin1_General_100_BIN2`, set at column definition | AD-15 | Not retrofittable on Azure SQL | **ACTIONABLE** |
| Ownership swap as two ordered `ExecuteUpdate` calls, demote before promote; `SaveChanges` forbidden | AD-26 | Never zero or two Owners, observable | **ACTIONABLE** |
| Status removal and cross-Project move are single atomic operations | AD-17 | No partial application | **ACTIONABLE** |
| Every state-changing endpoint idempotent under `Idempotency-Key` | AD-18, NFR-5 | Replay returns original response | **ACTIONABLE** |
| API shape locked per served version by snapshot contract test | AD-19, FR-37 | Breaking change fails the build | **ACTIONABLE** |
| Dependency rule enforced by ArchUnitNET | AD-21 | Build gate | **ACTIONABLE** |
| WCAG 2.1 AA on registration, Space switching, Board, Task editor, invitation; keyboard parity for every Board pointer operation | NFR-9 | 18 gated contrast pairs, 30 tokens | **ACTIONABLE** |
| Credentials: slow one-way hash, tunable without re-registration; Tokens shown once, stored unusable | NFR-6 | Work factor **unspecified** | **ACTIONABLE** |
| Nothing touches the database on an unconditional timer | AD-10, §6.3 | Structural; architecture suite fails on violation | **ACTIONABLE** |
| §6.4 data-protection gate opens on the first non-operator Account | PRD §6.4 | Five prerequisites become live | **ACTIONABLE** |
| Compaction preserves per-author change counts and timestamps | AD-13, addendum §7 | Obligation stated, **not yet guaranteed by AD-13** | **ACTIONABLE** |
| Cost stays under £30/month at NFR-8 scale | §6.3 | Free-tier grants load-bearing | **ACTIONABLE** |
| OAuth deferral shapes FR-1, FR-2, NFR-6 so they can change without redesign | §9.2 | No test today; a constraint on how they are written | **FYI** |
| Behavioural metrics SM-3..SM-6 stay derivable by operator query | §10, §6.1 | No thresholds by design | **FYI** |
| Trash/restore, search, horizontal sync scaling, OAuth, DR beyond included backups | Spine deferrals | Out of v1 | **FYI** |

### Risk Register

Scored probability (1 unlikely / 2 possible / 3 likely) × impact (1 minor / 2 degraded
/ 3 critical). Action thresholds per `probability-impact.md`: 1–3 DOCUMENT, 4–5 MONITOR,
6–8 MITIGATE, 9 BLOCK.

| ID | Cat | Risk | P | I | Score | Action | Mitigation / owner |
| --- | --- | --- | :-: | :-: | :-: | --- | --- |
| **R1** | TECH | **FR-34's interleaving cannot be tested deterministically.** No seam exists to hold a sync frame across the removal transaction's commit. SM-2 gates release on this in 100% of cases, so the likely outcome is a `Task.Delay`-shaped test that passes vacuously on the one requirement the PRD says the product should be judged on (T1) | 3 | 3 | **9** | **BLOCK** | Specify the interleaving seam in **story 7.3**, before 7.7/7.8 consume it. Gate: 7.3 is not done until a test can force both orderings deterministically. Owner: dev on 7.3 |
| R2 | SEC | **Timing oracles ship behind vacuous tests.** AD-3 and AD-23 both demand duration-indistinguishability; no sample size, statistic or tolerance is stated, so a single-draw assertion satisfies the letter and detects nothing. A real oracle discloses "this id exists in Yello" / "this address is registered", which §6.1 forbids (T3) | 2 | 3 | 6 | MITIGATE | State the method before **story 1.3** and **1.6**: N samples per arm, distribution comparison, server-side measurement, tolerance derived from observed variance |
| R3 | PERF | **NFR-5's 300 ms p95 read budget is missed by an order of magnitude on cold requests.** Scale-to-zero + 15-min auto-pause makes most requests cold under sparse traffic on 0.5 vCore with `MAXDOP = 1`. AR-40b is open, and warm-vs-cold changes the harness, not just the number (T7) | 3 | 2 | 6 | MITIGATE | Close AR-40b at epic 2's first measurement. Pin min replicas to 1 (~£12–15/mo, inside the ceiling) **or** state NFR-5 as measured warm and exempt the cold path explicitly. Do not leave it silent |
| R4 | PERF | **The Board's protection against focus corruption is also an unbounded DOM.** AD-29 forbids virtualisation and requires append-never-recycle; paging through a 5,000-Task column therefore grows the Blazor WASM render tree monotonically, against NFR-5 and NFR-9 at a bound that is confirmed final | 2 | 3 | 6 | MITIGATE | Measure in **story 2.9** at the real bound, not at a sample. If append-only cannot hold 5,000, the escalation is a focus-identity-preserving windowing scheme — which is an AD-29 amendment, not an implementation choice |
| R5 | DATA | **AR-40c: the `SESSION_CONTEXT` parallel-plan defect.** Documented across SQL Server 2019 CU14–CU31, 2022 CU1–CU23 and 2025 RTM–CU2 — a parallel plan on a pool-reset session can return **another tenant's rows, silently and successfully**. `MAXDOP = 1` removes the class; Azure SQL's exposure is unestablished. R3 creates direct pressure to relax exactly this mitigation for latency | 2 | 3 | 6 | MITIGATE | Confirm Azure SQL's status before first production deploy (story 1.10). Relax `MAXDOP = 1` **only** with the pooled-connection isolation case still green. Trace flag 11042 is unavailable on Azure SQL. Note the interaction with R3 explicitly so latency work cannot quietly reopen this |
| R6 | TECH | **The merge conformance suite gets written to the algorithm instead of to the requirements.** AD-12 requires it before any implementation; the algorithm is deferred (AR-40a). The failure mode is a suite that encodes whichever CRDT was chosen and can no longer reject a wrong one | 2 | 3 | 6 | MITIGATE | Write the suite in **story 7.1** citing FR-31, FR-33 and NFR-4 clause by clause, with no candidate in the repository. Falsification check: whole-field last-writer-wins must **fail** the suite, and that assertion is itself a test |
| R7 | DATA | **AD-15's collation is a migration detail with irreversible consequences.** Omit `COLLATE Latin1_General_100_BIN2` and Azure SQL's case-insensitive default makes `a0` and `A0` compare equal — non-deterministic `ORDER BY` and spurious uniqueness violations on a mixed-case base62 alphabet. `ALTER DATABASE … COLLATE` is unsupported on Azure SQL, so it cannot be fixed server-wide later | 2 | 3 | 6 | MITIGATE | Assert the collation in the **schema test**, not only in the migration — on the key column *and* on the `(ProjectId, StatusId, PositionKey)` index AD-29 seeks against. Include a mixed-case ordering case |
| R8 | SEC | **CORS and anti-forgery are one misconfiguration from a cross-site hole.** AD-7 fixes `SameSite=None` with an exact allowed origin and anti-forgery on every state change. A reflected `Origin` or a wildcard is the classic regression, and `SameSite=None` has already removed the implicit protection | 2 | 3 | 6 | MITIGATE | Negative tests in **story 1.4**: wildcard rejected, reflected arbitrary `Origin` rejected, state-changing request without an anti-forgery token refused, no credential in `localStorage`/`sessionStorage` |
| R9 | DATA | **The ownership swap's ordering is correct in prose and unenforced in code.** AD-26 requires two ordered `ExecuteUpdate` calls, demote before promote, and forbids tracked-entity `SaveChanges` because EF picks its own statement order. ArchUnit cannot see "this handler used SaveChanges for the Role swap" | 2 | 3 | 6 | MITIGATE | Invariant test asserting no Space ever holds zero or two Owner Memberships, plus a concurrency case driving two acceptances at one offer. Consider an ArchUnit rule scoped to the `AcceptOwnershipOffer` slice forbidding `SaveChanges` |
| R10 | BUS | **Thirteen unconfirmed assumptions become acceptance criteria.** PRD §12 lists 13, none confirmed; four are marked † as already hardened into architecture. Tests written against an unconfirmed assumption make it permanent by making its reversal a test failure | 3 | 2 | 6 | MITIGATE | Readiness issue 5 stays open. Tag every test whose expectation traces to a §12 assumption, so reversal shows as a known set rather than a hunt. The four † entries are the ones that already cost more than a document edit |
| R11 | SEC | **The §6.4 gate has no automated tripwire.** "The first Account created by anyone other than the operator makes this document non-compliant" is stated as testable, and five prerequisites become live at that moment. Nothing counts Accounts | 2 | 3 | 6 | MITIGATE | Story 1.10 asserts the single-operator position and its five prerequisites. Make the condition observable — an operator-side count, since §6.1 forbids an in-product cross-Space aggregate |
| R12 | TECH | **Expiry is only reachable by seeding the past** (T2), and AD-27's ArchUnit rule is an assertion about absence | 3 | 1 | 3 | DOCUMENT | State the seeding convention once in **story 5.3**. Give the AD-27 rule a concrete predicate: a `BackgroundService`/`IHostedService` writing `OwnershipOffer.State` or `Invitation.State` |
| R13 | OPS | **A load test at NFR-8 scale can take the environment away.** Exhausting the 100,000 vCore-s free grant triggers auto-pause until next month by configuration — the budget is protected, availability is not (T8) | 2 | 2 | 4 | MONITOR | Load-test locally against the Testcontainers SQL Server. Reserve Azure for one deliberate measurement window, costed before it runs |
| R14 | DATA | **Compaction can destroy SM-5 irrecoverably.** AD-13 permits replacing a log prefix with a snapshot without saying what survives; the addendum requires per-author change counts and timestamps to persist. Once compacted, the data is gone | 2 | 2 | 4 | MONITOR | Close the AD-13 obligation before compaction is implemented (epic 7). Test: compact a log and assert per-author counts and timestamps are unchanged |
| R15 | PERF | **NFR-3's 16 ms local render is tight for Blazor WASM.** One frame at 60 Hz for a keystroke through the render-tree diff plus a local CRDT apply. AD-11 forbids blocking on the network, which is the easy half; the render is the hard half | 2 | 2 | 4 | MONITOR | Measure early in **story 7.4** rather than at the end of epic 7. If it does not hold, the lever is the editor's render granularity, not the merge algorithm |
| R16 | TECH | **The client's two invariants have no test level** (T9), and the `tea_use_playwright_utils` mismatch is what decides whether they get one | 2 | 2 | 4 | MONITOR | Decide the browser-test binding at the framework run (TF) — Playwright for .NET, or a separate TypeScript project. Stories 2.8 and 2.9 both consume the answer |
| R17 | TECH | **The isolation suite's completeness is unfalsifiable** while "every case on both surfaces" has no enumerated case list (T6) | 2 | 2 | 4 | MONITOR | Hold the case list as data in **story 1.9** and generate the cross-product, so a missing surface pair fails rather than passes unnoticed |
| R18 | OPS | **NFR-5 has no production evidence source.** No RED metrics, no `/metrics`, and AD-10 forbids the timer-driven collection that would be the usual answer (T10) | 2 | 1 | 2 | DOCUMENT | Acceptable for single-operator v1. Record that p95 is a load-test figure, not a monitored one, so nobody later cites it as production evidence |

**Distribution:** 1 BLOCK, 10 MITIGATE, 5 MONITOR, 2 DOCUMENT.

That the register clusters so heavily at 6 is not scoring inflation — it reflects a
design where a large number of decisions are individually load-bearing and explicitly
irreversible (a collation that cannot be altered, a bound confirmed final, a gate with
no acceptable failure rate). The distinguishing feature of R1 is not that it is more
severe in consequence than R5 or R7, but that its **probability is 3**: the other
critical risks have a stated mitigation already in the architecture, and R1 has none.

### NFR Planning Assessment

Thresholds extracted from the PRD, spine and epics. **UNKNOWN** means no value is
stated — recorded as a clarification item, never guessed.

| NFR | Threshold | Planned evidence source |
| --- | --- | --- |
| NFR-1 Isolation | Zero verified disclosures. No acceptable failure rate | `Yello.Tests.Isolation`, every case × both surfaces, + `AccessRefusal` assertions. Case list itself is a gap (T6) |
| NFR-2 request path | The very next request. **No tolerance** | `Yello.Tests.Revocation` — change Role, assert the immediately following request |
| NFR-2 live session | **1 s** from the transaction boundary, unprompted | `Yello.Tests.Revocation` — blocked on the R1 seam |
| NFR-3 local render | **16 ms** (one frame @ 60 Hz) | Browser measurement, client-side. Binding undecided (R16) |
| NFR-3 remote render | **300 ms p95** at 50 ms RTT | Load harness. *Method for simulating 50 ms RTT is* **UNKNOWN** |
| NFR-3 presence | Appears ≤ **2 s**, disappears ≤ **10 s** | Sync integration tests |
| NFR-4 convergence | Identical text ≤ **2 s**; ≥ **10** simultaneous editors; **5 min** disconnect reconciles | Merge conformance suite (AD-12) + sync integration |
| NFR-5 latency | **300 ms** read / **500 ms** write, p95, server-side | k6 or equivalent. **Warm vs cold is UNKNOWN** (AR-40b) — the single most consequential open threshold |
| NFR-5 refusals | Machine-readable reason on every refusal | RFC 9457 `problem+json` shape assertions with stable `type` |
| NFR-6 password hash | Slow one-way, tunable without re-registration. **Work factor UNKNOWN** — "the architecture's call", never made | Story 1.3/1.4. Needs a number before it can be asserted |
| NFR-6 tokens | Stored unusable; displayed exactly once | Story 1.8 |
| NFR-6 transit | All traffic encrypted | Deployment assertion |
| NFR-6 at rest | Explicitly **not required** in v1; becomes a §6.4 prerequisite at the gate | Known-deferred, not unknown |
| NFR-7 refusal record | Acting Account, target Space, capability, outcome, `CrossSpace` / `InsufficientRole`; retained **90 days** (assumption) | `AccessRefusal` table assertions; purge job ≤ daily |
| NFR-8 bounds | 50 Spaces/Account · 100 Memberships/Space · 50 Projects/Space · 5,000 Tasks/Project · 10 editors/Task · 50 Sessions/Space | Bound registry test (all six enumerated) + refusal-at-bound cases. *What counts as an "active Session"* is **UNKNOWN** for enforcement purposes |
| NFR-9 accessibility | WCAG 2.1 AA on five named flows; keyboard parity for every Board pointer operation; ARIA live announcements | axe-core via browser automation + the story 1.2 contrast harness (30 tokens, 18 gated pairs) + explicit keyboard traversal tests |
| §6.3 cost | **£30/month** at NFR-8 scale | Cost estimate + the 10%-remaining vCore alert |
| Availability | **UNKNOWN** — no uptime target stated anywhere | None. Checklist 3.3 gap |
| RTO / RPO | **UNKNOWN** — 7-day PITR and locally redundant backup are what the free offer gives, not a stated objective | None. Checklist 4.1 gap; spine defers DR |
| Rate limit values | **UNKNOWN** — FR-38 requires per-Token limiting and `Retry-After`, no numbers | Story 8.3 needs a figure before it can assert one |
| Test coverage % | **UNKNOWN** — no target stated. Deliberately not invented here | — |

**Boundary respected:** this is NFR *planning*. No PASS/CONCERNS/FAIL verdict is issued
against implementation evidence — that is `bmad-testarch-nfr`'s job, and the PRD already
schedules NFR-8's verification there.

### Risk summary — the four that change what gets built

1. **R1 (score 9)** — the FR-34 test seam. The only BLOCK, and the only critical risk
   with no mitigation already present in the architecture. It is also the cheapest to
   fix *now* and among the most expensive to fix after story 7.3 ships.
2. **R3 + R5 together** — the cold-start budget creates pressure to relax the very
   `MAXDOP = 1` setting that suppresses a silent cross-tenant read. Neither is alarming
   alone; the interaction is, and it is not recorded in either deferral's own entry.
3. **R2** — two timing requirements with no method. The failure mode is a green suite
   over a live oracle, which is the worst shape a test can take.
4. **R4** — the mechanism chosen to protect keyboard users from focus corruption is the
   same mechanism that grows the DOM without bound. Must be measured at 5,000, not
   sampled.

## Step 4: Coverage Plan & Execution Strategy

### Test levels for this stack

The generic level names map onto the pinned stack as follows. Stated once so the
coverage matrix can use short labels.

| Label | Means here | Tooling |
| --- | --- | --- |
| **UNIT** | Domain invariants, pure logic, the merge algorithm's own behaviour. No database | xunit.v3 |
| **INT** | A slice end to end through the real request pipeline against real SQL Server, both calling surfaces | xunit.v3 + Testcontainers (`mssql/server:2025-latest`) + `WebApplicationFactory` |
| **ARCH** | Static assertions over the compiled assemblies and the migrated schema | ArchUnitNET + schema tests |
| **E2E** | Rendered DOM, focus identity, keyboard traversal, ARIA | Browser automation — binding undecided (R16) |
| **LOAD** | Latency percentiles, concurrency at the NFR-8 bounds | k6 or equivalent; local Testcontainers target (T8) |

**Duplicate-coverage guard applied throughout.** The Role capability matrix (FR-16, 15
capabilities × 4 Roles) is tested at **INT** and never at E2E — E2E's only job on Roles
is UJ-4's "affordances are absent, not present-and-failing", which is a rendering claim
INT cannot make. Similarly the merge algorithm is **UNIT**, the sync channel's
authorisation is **INT**, and only convergence-as-experienced is E2E.

### Coverage matrix

Grouped by the suite that owns each scenario, because the architecture already fixes
five test projects and four release gates. Counts are scenario classes, not individual
`[Fact]` methods.

#### 1. Isolation suite — `Yello.Tests.Isolation` · gates SM-1 · NFR-1

Every case runs against **both surfaces** (AD-4); the count column is per surface.

| # | Scenario class | Level | P | Cases | Discharges |
| --- | --- | --- | :-: | :-: | --- |
| I-1 | Read a resource in a Space with no Membership → 404, per entity type (Project, Task, Label, StatusDefinition, Membership, Invitation, ApiToken) | INT | **P0** | 7 | FR-15, NFR-1 |
| I-2 | Write/mutate a resource in a foreign Space → 404, per entity type | INT | **P0** | 7 | FR-15, NFR-1 |
| I-3 | No listing or aggregate includes a foreign row (Projects, Tasks, Labels, Memberships, Invitations, Board column, List View page, column count) | INT | **P0** | 8 | FR-15, FR-18, AD-29 |
| I-4 | Possessing an identifier confers nothing — deep link by Guid to Task, Project, Space | INT | **P0** | 3 | NFR-1 |
| I-5 | Boundary 404 and in-Space not-found are **byte-identical in body, status and headers** | INT | **P0** | 2 | AD-3 |
| I-6 | Boundary 404 and in-Space not-found are **indistinguishable in duration** — statistical, method per R2 | INT | **P0** | 2 | AD-3, T3 |
| I-7 | Registration / authentication / Invitation issue identical in status, body **and duration** for known vs unknown address | INT | **P0** | 6 | AD-23, §6.1, FR-1, FR-2, FR-10 |
| I-8 | **Pooled-connection reuse** — two requests for different Spaces served consecutively on one physical connection. Own collection, parallelism off (T5) | INT | **P0** | 1 | AD-2 |
| I-9 | Owner of one Space has no elevated standing in another | INT | **P0** | 2 | FR-15, epic 3 |
| I-10 | Authorisation never cached across a Space switch; no Role carried over | INT | **P0** | 3 | FR-9, NFR-2 |
| I-11 | Account-scoped surfaces return Space **identity only** — no Project, Task, Membership, Label or count crosses the switcher or Account settings | INT | **P0** | 4 | AD-24 |
| I-12 | An API Token reaches only its bound Space, including other Spaces the same Account owns | INT | **P0** | 2 | FR-36, AD-6 |
| I-13 | Error bodies and problem documents carry no Space name, resource title or existence hint | INT | **P0** | 4 | AD-3, §6.1 |
| I-14 | Notification content discloses nothing across a boundary — invitation, ownership offer, assignment | INT | **P0** | 3 | FR-39, FR-40, FR-43, NFR-1 |
| I-15 | Assignee candidates and Assignee filter offer only same-Space Memberships | INT | **P0** | 2 | FR-21, FR-30 |
| I-16 | Presence never reveals activity in another Space | INT | P1 | 1 | FR-32 |
| I-17 | Structured logs contain no password, Token, cookie or Space/Project/Task content; `SpaceId` as field, never Space name | INT | P1 | 2 | NFR-6, §6.1 |

**Subtotal: 59 case classes × 2 surfaces ≈ 118 tests.** This is the largest single
suite in the product and the one SM-1 gates release on.

> **Gap noted:** AD-4 names two surfaces — browser and API Token. The `/sync` WebSocket
> is a *third* ingress, authorised per frame under AD-8, and the isolation suite is not
> required to run its cases there. Cases I-1..I-4 have sync analogues (can a frame reach
> a Task in a foreign Space?) that fall to the revocation suite instead. Worth an
> explicit decision in story 1.9 rather than leaving the third surface implicit.

#### 2. Revocation suite — `Yello.Tests.Revocation` · gates SM-2 · FR-34, NFR-2

| # | Scenario class | Level | P | Cases | Discharges |
| --- | --- | --- | :-: | :-: | --- |
| V-1 | Role change governs the **very next request**, both surfaces, per capability class | INT | **P0** | 5 | NFR-2 request path |
| V-2 | Membership removal governs the very next request | INT | **P0** | 2 | NFR-2, FR-14 |
| V-3 | Token invalidated in the same transaction as Membership removal / Space deletion / Account deletion | INT | **P0** | 3 | FR-36, AD-6 |
| V-4 | **Removal mid-edit** — lease invalidated, editor inert, access-ended reason delivered, ≤ 1 s, unprompted | INT | **P0** | 1 | FR-34, NFR-2 live |
| V-5 | **Unsynchronised local changes are not applied** after removal — the assertion independent of both timings | INT | **P0** | 1 | FR-34 |
| V-6 | **Changes admitted before invalidation are retained** — revocation stops future writes, does not roll back past ones | INT | **P0** | 1 | FR-34 |
| V-7 | **A delayed or retried frame on an invalidated lease is discarded, not queued, not persisted** | INT | **P0** | 1 | FR-34, AD-8 |
| V-8 | Demotion to Viewer ends editing while read access continues uninterrupted | INT | **P0** | 1 | FR-34, UJ-6 |
| V-9 | Both orderings forced deterministically across the removal commit — **requires the R1 seam** | INT | **P0** | 2 | R1, FR-34 |
| V-10 | Accepting an Ownership Offer publishes `MembershipChanged` **per affected Account** (two Roles move) | INT | **P0** | 1 | AD-9, AD-26 |
| V-11 | Space deletion and Account deletion terminate live sessions | INT | P1 | 2 | FR-7, FR-3 |
| V-12 | Task deletion terminates the editing session with a deleted reason, not a silent drop | INT | P1 | 1 | FR-23 |
| V-13 | Leases do not survive process restart; reconnection re-authorises | INT | P1 | 1 | AD-8, AD-14 |

**Subtotal: 22 case classes.** V-9 is the gate on R1 — until the seam exists, V-4..V-8
are assertions about a race nobody controls.

#### 3. Merge conformance suite — `Yello.Tests.Merge` · AD-12 · written before any implementation

| # | Scenario class | Level | P | Cases | Discharges |
| --- | --- | --- | :-: | :-: | --- |
| M-1 | Two participants editing **different regions** both retain their changes | UNIT | **P0** | 4 | FR-31 |
| M-2 | Two participants editing the **same region** converge to identical text, and that text persists | UNIT | **P0** | 4 | FR-31 |
| M-3 | **Falsification** — whole-field last-writer-wins **must fail** this suite. Asserted, not assumed | UNIT | **P0** | 1 | AD-12, R6 |
| M-4 | Convergence within 2 s of the last edit, all participants identical | UNIT/INT | **P0** | 2 | NFR-4 |
| M-5 | Convergence holds at **10 simultaneous editors** | UNIT | **P0** | 2 | NFR-4, NFR-8 |
| M-6 | Disconnect up to **5 minutes** reconciles with no loss and no duplication | UNIT/INT | **P0** | 3 | FR-33, NFR-4 |
| M-7 | Changes made by others during a disconnection are present after reconnection | INT | **P0** | 1 | FR-33 |
| M-8 | Reconciliation failure is reported explicitly; unsynchronised text is not silently discarded | INT | **P0** | 1 | FR-33 |
| M-9 | **Property-based interleaving** — randomised operation orders over N participants, assert convergence and no lost insert. *Recommended addition*: a CRDT's failure modes live in orderings nobody enumerates by hand | UNIT | **P0** | 1 harness | NFR-4, R6 |
| M-10 | No merge prompt, lock or stale-content warning is ever surfaced during normal concurrent editing | E2E | P1 | 1 | FR-31 |
| M-11 | Compaction preserves per-author change counts and timestamps | INT | P1 | 1 | AD-13, R14 |

**Subtotal: 21 case classes + 1 property harness.** M-3 and M-9 are the two that make
this suite a contract rather than a description.

#### 4. Architecture suite — `Yello.Tests.Architecture` · AD-21 · build gate

| # | Assertion | Level | P | Cases | Discharges |
| --- | --- | --- | :-: | :-: | --- |
| A-1 | Dependency rule: Domain → nothing; Application → Domain; Infrastructure → Application+Domain; Host → all | ARCH | **P0** | 4 | AD-21 |
| A-2 | EF Core types absent from Domain; ASP.NET Core types absent from Application and Domain | ARCH | **P0** | 2 | AD-21 |
| A-3 | `[Authorize(Roles=…)]`, `ClaimsPrincipal.IsInRole`, `IdentityRole` and Identity's role store are absent | ARCH | **P0** | 4 | AD-1 |
| A-4 | Every Space-scoped route carries `{spaceId}`; no endpoint resolves Task/Project/Label/StatusDefinition without it | ARCH | **P0** | 1 | AD-3 |
| A-5 | Every Space-scoped table has an RLS policy — **schema** test against the migrated database | ARCH | **P0** | 1 | AD-2 |
| A-6 | Position key column and the `(ProjectId, StatusId, PositionKey)` index are collated `Latin1_General_100_BIN2`; a mixed-case ordering case proves it | ARCH | **P0** | 2 | AD-15, R7 |
| A-7 | The NFR-8 bound registry enumerates **all six** bounds; a registry missing any fails | ARCH | **P0** | 1 | AD-25 |
| A-8 | Exactly one `ITextMergeStrategy` implementation is registered; no domain/application/sync code references a concrete merge type | ARCH | **P0** | 2 | AD-12 |
| A-9 | No component queries the database on a fixed interval; probes answer from process state | ARCH | **P0** | 1 | AD-10 |
| A-10 | No scheduled component writes a terminal expiry state — predicate: `BackgroundService`/`IHostedService` writing `OwnershipOffer.State` or `Invitation.State` (R12) | ARCH | **P0** | 1 | AD-27 |
| A-11 | Nothing writes the description projection except the projector | ARCH | P1 | 1 | AD-13 |
| A-12 | No slice re-implements authorisation, Space resolution, refusal recording or idempotency | ARCH | P1 | 1 | AD-21, AR-3 |
| A-13 | Raw SQL bypassing global query filters appears only in Infrastructure | ARCH | P1 | 1 | AD-2 |
| A-14 | `AcceptOwnershipOffer` does not use tracked-entity `SaveChanges` for the Role swap (R9) | ARCH | P1 | 1 | AD-26 |
| A-15 | No `DateTime` in domain or wire types — `DateTimeOffset` only | ARCH | P2 | 1 | Conventions |

**Subtotal: 24 assertions.** Cheap to write, and they are what stops the paradigm
eroding one story at a time.

#### 5. Accessibility & client invariants · NFR-9 · release requirement

| # | Scenario class | Level | P | Cases | Discharges |
| --- | --- | --- | :-: | :-: | --- |
| X-1 | **Contrast harness** — 18 gated pairs computed from the 30 tokens by the WCAG 2.x formula, both themes, failing the build on regression | UNIT | **P0** | 36 | NFR-9, story 1.2, UX-DR1/DR7 |
| X-2 | The two surface-adjacency ratios are **excluded** from the gate — gating them would fail the build permanently | UNIT | **P0** | 2 | DESIGN.md |
| X-3 | WCAG 2.1 AA (axe-core, zero violations) on the five named flows: registration, Space switching, Board, Task editor, invitation | E2E | **P0** | 5 | NFR-9 |
| X-4 | **Every Board pointer operation has a keyboard equivalent**, including moving a Task between columns | E2E | **P0** | 6 | NFR-9, FR-29, story 2.8 |
| X-5 | **Focus identity survives paging** — a row is never recycled onto a different Task; the next keyboard move operates on the Task the user was on | E2E | **P0** | 2 | AD-29, R4 |
| X-6 | `aria-setsize` carries the **true total** and `aria-posinset` the true ordinal, agreeing with the visible count chip | E2E | **P0** | 2 | AD-29 |
| X-7 | Presence and permission-change notices announced via ARIA live regions, not colour or position alone | E2E | **P0** | 3 | NFR-9, FR-32, FR-34 |
| X-8 | Role chip renders as a chip (border present) and states one of Owner/Admin/Member/Viewer verbatim | E2E | P1 | 1 | UJ-4, DESIGN.md |
| X-9 | Capabilities a Role lacks are **absent**, not present-and-failing | E2E | P1 | 4 | UJ-4, §7 |
| X-10 | FR-34 editor state is `readonly` with the revoked-edge border; text stays legible and selectable | E2E | P1 | 1 | FR-34, DESIGN.md |
| X-11 | Board on a small viewport; text-spacing override at 1.4.12; 1.4.4 resize | E2E | P1 | 3 | NFR-9, story 2.11 |

**Subtotal: 65 case classes.** X-5 and X-6 are the two invariants that exist only in the
client (T9) and decide whether R16's binding question gets answered.

#### 6. Performance & scale · NFR-3, NFR-5, NFR-8

| # | Scenario class | Level | P | Cases | Discharges |
| --- | --- | --- | :-: | :-: | --- |
| P-1 | Read p95 ≤ **300 ms**, write p95 ≤ **500 ms**, server-side, within the NFR-8 envelope. **Warm or cold is UNKNOWN** (R3) | LOAD | P1 | 4 | NFR-5 |
| P-2 | **Board at 5,000 Tasks** — first paint one bounded seek per column; last page of a column no slower than the first (the keyset claim) | LOAD | P1 | 3 | FR-28, AD-29, R4 |
| P-3 | List View keyset correctness at every offered sort — `(sortColumn, TaskId)` with ties and NULLs, asserting no row skipped or repeated across a page boundary | INT | **P0** | 5 | AD-29, FR-30 |
| P-4 | Every offered List View sort has a matching composite index — a sort without one is a defect, not a slow query | ARCH | **P0** | 1 | AD-29 |
| P-5 | Each of the six NFR-8 bounds refuses at the bound, in the same transaction, with a machine-readable reason | INT | **P0** | 6 | NFR-8, AD-25 |
| P-6 | Remote edit renders ≤ **300 ms p95** at 50 ms RTT. *RTT simulation method UNKNOWN* | LOAD | P1 | 1 | NFR-3 |
| P-7 | Local edit renders ≤ **16 ms** without a network round trip (R15) | E2E | P1 | 1 | NFR-3, AD-11 |
| P-8 | Presence appears ≤ 2 s, disappears ≤ 10 s | INT | P1 | 2 | NFR-3 |
| P-9 | Rate limiting per Token with `Retry-After`; one Space cannot exhaust another. **Limit values UNKNOWN** | INT | P1 | 3 | FR-38, AD-18 |
| P-10 | Idempotency — replayed `Idempotency-Key` returns the original response without re-applying | INT | **P0** | 3 | AD-18, NFR-5 |
| P-11 | Cold-start characterisation — measure and record, no threshold asserted until AR-40b closes | LOAD | P2 | 1 | R3 |

**Subtotal: 30 case classes.**

#### 7. API contract · AD-19, FR-37

| # | Scenario class | Level | P | Cases | Discharges |
| --- | --- | --- | :-: | :-: | --- |
| C-1 | Snapshot contract test locks each served version's response shape and accepted input | INT | **P0** | 2 | AD-19 |
| C-2 | Within a version: no field removed, renamed, retyped; no input narrowed — each as its own failing assertion | INT | **P0** | 4 | FR-37 |
| C-3 | At most two versions served concurrently; a named supported version gets that version's shape | INT | P1 | 2 | AD-19 |
| C-4 | Deprecation announced before withdrawal; the version keeps serving throughout | INT | P1 | 1 | FR-37 |
| C-5 | Board position readable over the API and **not writable** — the one deliberate narrowing | INT | **P0** | 2 | FR-35 |
| C-6 | Parity audit: no operation refused in the browser succeeds via the API | INT | **P0** | 1 harness | FR-35, AD-4 |
| C-7 | Problem documents are RFC 9457 with a stable machine-readable `type` | INT | P1 | 2 | NFR-5, conventions |

**Subtotal: 14 case classes.**

#### 8. Slice tests — `Yello.Tests.Slices`

Not enumerated here. 53 stories each carry their own command/handler/validator tests in
the slice folder, and the per-epic detail belongs to **epic-level TD passes**, which can
now read this document as prior system-level context. The system-level obligation is the
shape, and it is fixed by the conventions: one folder per use case holding its command,
handler, validator and tests.

Two cross-cutting slice obligations that are *not* per-story and are easy to lose:

| # | Scenario class | Level | P | Cases | Discharges |
| --- | --- | --- | :-: | :-: | --- |
| S-1 | Atomicity: Status removal + remap, cross-Project move + migration, bulk move, FR-27's Space-wide removal with per-Project exceptions — each all-or-nothing under a forced mid-operation failure | INT | **P0** | 6 | AD-17, FR-26, FR-27, FR-41 |
| S-2 | Invariant assertions: no Task ever holds a Status absent from its Project's effective set; no Space ever holds zero or two Owner Memberships | INT | **P0** | 2 | AD-17, AD-26, FR-42 |

### Priority totals

| Priority | Case classes | Notes |
| --- | :-: | --- |
| **P0** | ~215 (of which the isolation suite's 51 double to ~102 across surfaces) | Everything the four gating suites contain, plus the contract lock, the bound refusals, atomicity and the client's two focus invariants |
| **P1** | ~55 | Second-order isolation, performance budgets, the remaining accessibility surface, deprecation behaviour |
| **P2** | ~5 | Cold-start characterisation, convention assertions |
| **P3** | 0 | Nothing in this design is genuinely optional. `risk_threshold: p1` would exclude P2/P3 from gating anyway |

That P0 dominates so heavily is a property of the product, not of the scoring: NFR-1 has
no acceptable failure rate, SM-1 and SM-2 gate release outright, and AD-21 makes the
paradigm a build gate. A design where most tests are P0 is what "isolation is absolute"
costs.

### NFR coverage and evidence plan

| NFR category | Validation level / tool | Evidence artifact for `bmad-testarch-nfr` later |
| --- | --- | --- |
| **Security** (NFR-1, NFR-6, §6.1) | INT for isolation and refusal; ARCH for forbidden APIs; INT negative tests for CORS/anti-forgery | Isolation suite report, `AccessRefusal` assertions, architecture suite output |
| **Performance** (NFR-3, NFR-5) | LOAD for percentiles; E2E for the 16 ms local render | k6 summary JSON with p95 per operation class; **blocked on AR-40b for warm/cold** |
| **Scalability** (NFR-8) | INT for the six bound refusals; LOAD at the bounds | Bound registry test + load run at 5,000 / 10 / 50 |
| **Reliability** (NFR-2, NFR-4, FR-33) | INT for revocation and reconciliation; UNIT for convergence | Revocation suite report, merge conformance report |
| **Accessibility** (NFR-9) | UNIT contrast harness; E2E axe-core + keyboard traversal | Contrast table (36 computed ratios), axe reports for five flows, keyboard parity matrix |
| **Maintainability** | CI: coverage, no critical/high vulnerabilities | CI job output. **No coverage target is stated in the PRD** — see gates below |
| **Compliance** (§6.4) | Operator-side Account count; story 1.10's single-operator assertion | The five prerequisites recorded as live-or-not at the gate |
| **Observability** (NFR-7) | INT against the `AccessRefusal` table | Refusal rows with `CrossSpace`/`InsufficientRole` discrimination |

**Blockers / missing evidence sources, carried forward rather than guessed:** NFR-5
warm-vs-cold (AR-40b, R3); NFR-6 password work factor; FR-38 rate-limit values; NFR-3's
50 ms RTT simulation method; the definition of an "active Session" for NFR-8; availability
target; RTO/RPO. Each is an UNKNOWN in step 3's NFR table, and none is invented here.

### Execution strategy

| Stage | Contents | Budget |
| --- | --- | --- |
| **PR** | Architecture suite (ARCH, seconds) · contrast harness (UNIT) · merge conformance (UNIT) · slice tests (UNIT+INT) · isolation suite · revocation suite · contract snapshots | Target **< 15 min**. The isolation suite at ~118 tests against Testcontainers is the long pole — SQL Server container startup is amortised by sharing one container across collections, **except** the pooled-connection case (I-8), which needs its own |
| **Nightly** | E2E accessibility (axe on five flows) · keyboard traversal · focus-identity paging · Board at 5,000 Tasks · property-based merge interleaving with a larger case budget | No hard limit; must report by morning |
| **Weekly** | LOAD at the full NFR-8 envelope · cold-start characterisation · rate-limit saturation | Deliberate windows only, local target (T8/R13) |

Two constraints shape this more than usual. **AD-10** means no suite may introduce a
timer-driven database touch even in test infrastructure that runs against Azure. And the
free-tier grant means the weekly stage does **not** run against Azure by default (R13) —
a load test that exhausts 100,000 vCore-s takes the environment away until the month
rolls over.

### Resource estimates

Ranges only, for **test construction**, excluding application implementation. Spread
across eight epics rather than incurred up front.

| Priority | Estimate | Largest single item |
| --- | --- | --- |
| P0 | **~95–140 hours** | Isolation suite (~35–50 h) — 59 case classes × 2 surfaces, plus the seeding strategy (T4) and the pooled-connection collection (T5) |
| P1 | **~55–85 hours** | Accessibility E2E (~20–30 h), gated on the R16 binding decision |
| P2 | **~10–20 hours** | Cold-start characterisation |
| P3 | — | Nothing at P3 |
| **Total** | **~160–245 hours** | |

Three items carry the widest uncertainty and are called out rather than averaged away:

- **The R1 seam** (~4–10 h to design, but it changes the sync handler's concurrency
  shape if deferred past story 7.3 — then it is not 10 hours).
- **The timing method for R2** (~6–12 h once, reused by two requirements; open-ended if
  improvised twice).
- **Accessibility E2E** — unestimable until R16 resolves. A separate TypeScript project
  costs setup and a second toolchain; Playwright for .NET keeps one language and has a
  thinner helper ecosystem than the TEA fragments assume.

### Quality gates

| Gate | Threshold | Source |
| --- | --- | --- |
| P0 pass rate | **100%** | Workflow standard |
| P1 pass rate | **≥ 95%** | Workflow standard |
| **NFR-1 / SM-1** | **Any single verified cross-Space disclosure blocks release.** Not a percentage — the PRD states this as the one requirement with no acceptable failure rate | PRD NFR-1, §10 SM-1 |
| **SM-2** | Revocation governs the next request with no tolerance, and live sessions within 1 s, in **100%** of tested cases — including sessions holding unsynchronised edits | PRD §10 SM-2 |
| Merge conformance | Suite green **before** any implementation merges; LWW demonstrably fails it | AD-12, R6 |
| Architecture suite | Green. A violation fails the build, not the review | AD-21 |
| Contract snapshots | Locked per served version; a breaking change fails the build | AD-19 |
| High-risk mitigation | All score-≥6 risks have an owner and a plan before the epic that consumes them. **R1 (score 9) blocks story 7.3 from being called done** | `risk-governance.md` |
| Bound registry | All six NFR-8 bounds enumerated and enforced | AD-25 |
| Contrast | 18 gated pairs pass in both themes; the 2 adjacency ratios stay excluded | NFR-9, story 1.2 |
| Code coverage | **Proposed: ≥ 80% line coverage on `Yello.Domain` + `Yello.Application`**, excluding Host composition and EF-generated migration code | **Not from the PRD** — see note |

**Note on the coverage gate.** No coverage target appears anywhere in the PRD,
architecture or epics. The 80% figure is the workflow's default, scoped here to the two
rings where logic actually lives so it measures something. It is offered as a proposal
for Lee to accept or drop, not presented as an extracted requirement. It is also the
weakest gate on this list: for Yello, "the isolation suite enumerates every case and all
of them pass" is a far stronger statement than any line-coverage percentage, and a high
percentage with an under-enumerated isolation suite (T6) would be actively misleading.

## Step 5: Output Generation & Validation

### Execution mode resolution

`tea_execution_mode: auto` with `tea_capability_probe: true`. Under `auto` the resolver
prefers agent-team, then subagent, then sequential. Subagent launching is **unavailable
for this run by explicit standing instruction** ("do not call the AgentTool unless the
user requested it"), so the capability probe resolves both `agentTeam` and `subagent` to
false.

**Resolved mode: `sequential`.** The two documents were written in series and
cross-checked by hand rather than generated in parallel and reconciled.

### Outputs written

| Document | Path | Lines |
| --- | --- | --- |
| Architecture-facing design | `_bmad-output/test-artifacts/test-design-architecture.md` | 632 |
| QA-facing design | `_bmad-output/test-artifacts/test-design-qa.md` | 605 |
| BMAD handoff | `_bmad-output/test-artifacts/test-design/YelloBMAD-handoff.md` | 189 |
| These working notes | `_bmad-output/test-artifacts/test-design-progress.md` | — |

**Path note.** `_bmad/tea/config.yaml` sets
`test_design_output: _bmad-output/test-artifacts/test-design`, but step 5 specifies
`{test_artifacts}/test-design-architecture.md` and `{test_artifacts}/test-design-qa.md`
— the `test-artifacts` root — while `checklist.md` puts the handoff in the `test-design/`
subfolder. Both were followed as written rather than harmonised, so the checklist
validates and any consumer following either convention finds what it expects. The
inconsistency is in the skill, not in this run.

### Checklist validation

Validated against `checklist.md`. Passing items are not enumerated; the six that
required a decision are recorded below with what was done.

| # | Checklist item | Resolution |
| --- | --- | --- |
| 1 | *"Architecture doc length: target ~150–200 lines max"* | **Deviated — 632 lines.** The same checklist mandates a full mitigation plan (numbered strategy + owner + timeline + status + verification) for every score-≥6 risk, and there are ten of them; that alone is ~140 lines before the risk tables, the Quick Guide or the ASR list. The two constraints are incompatible for a whole-system design covering 43 FRs and 29 ADs, and the length target is calibrated for a single-feature review. Genuine bloat *was* cut in response: "What works well" went from 11 bullets to 5 grouped properties, "Accepted trade-offs" from 5 items to 4 condensed ones, and the decorative ✅ markers were removed from prose |
| 2 | *"Code example with playwright-utils if `tea_use_playwright_utils` is true"*, importing from `@seontechnologies/playwright-utils` | **Deliberately not done.** The flag is true but the stack is .NET 10 / xunit.v3, and those fragments are TypeScript helpers for `@playwright/test`. Including the example would instruct a future reader to take a dependency that cannot be consumed from C#. Appendix A carries the C# equivalents instead — xunit `[Trait]` selection, `[Theory]`-parameterised surfaces, `dotnet test --filter` — and the mismatch is raised in both documents with a recommendation to set the flag `false` |
| 3 | *"Playwright parallelization noted: 100s of tests in 10–15 min"* | Not applicable — Playwright is not the primary runner. The underlying intent (a < 15-minute PR stage, defer only what is expensive) is stated, with the isolation suite named as the long pole and a specific plan if it exceeds the budget |
| 4 | *"P0 tests should cover <10% of total scenarios"* | **Re-examined rather than dismissed.** Six scenarios moved from P0 to P1 on the strict criterion (X-3, X-4, X-6, X-7, P-4, C-5), leaving P0 at ~205 of ~275. The residual gap is the denominator: this design excludes the per-story slice tests for all 53 stories by design, and those are where the non-P0 mass lives. Both the ratio and the reason are now stated in the QA doc |
| 5 | *"Execution order defined (smoke → P0 → P1 → P2/P3)"* | Contradicted by the same checklist's later **CRITICAL** rule — *"Simple structure: PR / Nightly / Weekly (NOT complex smoke/P0/P1/P2 tiers)"*. Followed the CRITICAL rule |
| 6 | *"Status File Integration — test design logged in Quality & Testing Progress"* | **Not done.** `sprint-status.yaml` has no Quality & Testing section, and inventing one in a tracker that another workflow owns would be a silent schema change. Raised as a suggestion for Lee instead |

Also confirmed: no CLI browser sessions were opened (browser exploration was skipped —
there is no running application), so there are no orphaned browsers to clean up; and
every artifact was written under `_bmad-output/test-artifacts/`, not to a scratch or
random location.

**Cross-document consistency checked by hand:** risk IDs R1–R18 identical across all
three documents · blocker IDs B1–B6 identical · priority levels consistent · date
2026-08-22 and author consistent · PRD and spine references identical · no duplicated
content between the two design documents (the architecture doc holds WHAT and WHY, the QA
doc HOW, with cross-references rather than repetition).

### Completion report

**Mode:** system-level, create, sequential.

**Key findings:**

1. **One BLOCK-level risk (R1, score 9).** FR-34's revocation-mid-edit cannot be tested
   deterministically because nothing lets a test hold a sync frame across the removal
   transaction's commit. SM-2 gates release on this in 100% of tested cases, and the PRD
   says FR-34 is the criterion the product should be judged on. It is also the only
   critical risk with no mitigation already sitting in the architecture. Fix: name the
   seam in story 7.3.
2. **The R3 / R5 interaction, which neither deferral records.** The cold-start latency
   problem creates direct pressure to relax `MAXDOP = 1` — the setting suppressing a
   documented defect where a parallel plan returns another tenant's rows silently. Should
   be written into AR-40b and AR-40c themselves.
3. **Two requirements demand duration-indistinguishability with no method** (AD-3, AD-23).
   The default failure mode is a green test over a live oracle.
4. **The isolation suite's completeness is unfalsifiable** while "every case on both
   surfaces" has no enumerated case list, and SM-1 gates release on it.
5. **Six blockers**, of which B2, B3 and B6 land inside epic 1 and gate the largest suite
   in the product.

**Gate thresholds:** P0 100% · P1 ≥95% · SM-1 zero verified disclosures (absolute, not a
percentage) · SM-2 100% of tested cases · merge conformance green with LWW failing it ·
architecture suite green · all six NFR-8 bounds enforced · 18 contrast pairs in both
themes.

**Open assumptions carried forward:** eight UNKNOWN thresholds (NFR-5 warm/cold · NFR-6
work factor · FR-38 rate limits · NFR-3's RTT method · "active Session" · availability ·
RTO/RPO · coverage target), none guessed; PRD §12's thirteen unconfirmed assumptions; and
the AD-24 amendment due before epic 3, which changes what case I-11 asserts.
