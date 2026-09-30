---
stepsCompleted:
  [
    'step-01-preflight-and-context',
    'step-02-generation-mode',
    'step-03-test-strategy',
    'step-04-generate-tests',
    'step-05-validate-and-complete',
  ]
lastStep: 'step-05-validate-and-complete'
lastSaved: '2026-09-20'
workflowType: 'testarch-atdd'
storyId: '1.5'
storyKey: '1-5-resolve-the-active-space-per-request-enforced-in-the-database'
storyFile: '_bmad-output/implementation-artifacts/1-5-resolve-the-active-space-per-request-enforced-in-the-database.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-1-5-resolve-the-active-space-per-request-enforced-in-the-database.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceIsolationLayerTests.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/PooledConnectionIsolationTests.cs'
  - 'tests/Yello.Tests.Architecture/SpaceResolutionSeamTests.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/1-5-resolve-the-active-space-per-request-enforced-in-the-database.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/data-factories.md'
---

# ATDD Checklist — Epic 1, Story 1.5: Resolve the active Space per request, enforced in the database

**Date:** 2026-09-20
**Author:** Lee
**Primary Test Level:** INT (integration, container-backed), with ARCH for the structural gates

---

## Story Summary

Every read and write of Space-scoped data is authorised against the acting Account's Membership in the Space that owns it. The story builds the per-request `ActiveSpaceContext`, the per-unit-of-work session-context call that drives row-level security, the independent EF Core query-filter layer, and the schema constraints that make same-Space hold by construction.

**As a** any Account holder
**I want** every read and write of Space-scoped data authorised against my Membership in the Space that owns it
**So that** nothing I am not a member of can reach me by any route.

---

## Acceptance Criteria

1. **AC1** — `ActiveSpaceContext` resolved from the Membership row for `(AccountId, SpaceId)` before any authorisation decision; unresolvable context refused, never defaulted.
2. **AC2** — `sp_set_session_context 'SpaceId', …, @read_only = 1` at the start of **every** unit of work, from `ActiveSpaceContext`, never from a client value, never once per connection.
3. **AC3** — every Space-scoped table carries a non-nullable `SpaceId` and an RLS policy on `SESSION_CONTEXT('SpaceId')`; an uncovered table fails the schema test.
4. **AC4** — EF Core global query filters restate each policy **from application state**, independently; no raw SQL bypassing them outside `Infrastructure`.
5. **AC5** — pooled-connection reuse across two Spaces returns only the second Space's rows; database configured `MAXDOP = 1`.
6. **AC6** — cross-entity references inside a Space use composite foreign keys carrying `SpaceId`.
7. **AC7** — Role comes from the Membership row read on this request, never a cookie, claim, Token payload or cache outliving the request.
8. **AC8** — the RLS seeding strategy (blocker B2) is stated and is one of three named shapes. *Carried from `handoff.md:87`, not from `epics.md`.*

---

## Step 1 — Preflight

**Stack detection:** `test_stack_type: auto` → **`backend`**. No `package.json`, no `playwright.config.*`, no `vite`/`webpack` config; `Yello.slnx` plus 15 `.csproj` files present. Worth stating precisely, because it is not the whole truth about the repository: Yello *does* have a browser surface (Blazor WebAssembly) and *does* have browser tests (`Yello.Tests.Client`, Playwright **for .NET**) — but those are driven from .NET, not a JS toolchain, so none of the algorithm's frontend indicators fire. For story 1.5 the classification is right on the merits anyway: the story has no UX surface in scope, and `MainLayout.razor:10-13` records that no Space-scoped client surface exists yet.

**Prerequisites — all met:**

| Requirement | State |
|---|---|
| Story approved, clear AC | ✅ `ready-for-dev`, 8 AC blocks, reproduced verbatim from `epics.md:693-726` |
| Backend test config exists | ✅ six test projects under `tests/`, MTP runner opted in via `global.json` |
| Development environment | ✅ .NET SDK **10.0.303** (matches the `global.json` pin), container runtime **up** (Docker server 29.5.3) |

The container-runtime check matters here rather than being box-ticking: `TESTING-CONVENTIONS.md:434-450` records that a stopped runtime produces **failures, not skips**, and that `IsContainerRuntimeAvailable()` performs no I/O so a dead-but-configured backend reports available.

**TEA config flags read:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `test_stack_type: auto`.

**Knowledge fragments loaded:** `test-levels-framework`, `test-priorities-matrix`, `test-quality`, `data-factories`.

**Fragments deliberately not loaded, with reasons** — recorded rather than silently skipped:

- `component-tdd.md` — UI component TDD; this story has no UI surface.
- `ci-burn-in.md` — CI is built by **story 1.10** (`TESTING-CONVENTIONS.md:457-463`), explicitly out of scope here.
- `selector-resilience.md`, `timing-debugging.md` — the step routes these to frontend/fullstack only.
- All Playwright Utils, Pact.js Utils and Pact MCP fragments — disabled in `_bmad/tea/config.yaml`, and there is no contract-testing surface in this story.

**Standing caveat on the loaded fragments:** they are written against TypeScript/Playwright. Their *principles* transfer (determinism, isolation, factory-with-overrides, API-first seeding, cleanup discipline) and their *examples* do not. Where they conflict with `tests/TESTING-CONVENTIONS.md`, the repository document wins — it is enforced by build gates, and it already encodes stricter versions of the same rules (no `Task.Delay` ever; randomise every email address; cleanup by transaction rollback or container disposal, never delete statements; **no coverage threshold — do not invent one**).

---

## Step 2 — Generation mode

**AI generation.** Step 2 §1 makes this unconditional for a `backend` stack: *"Always use AI generation (no browser recording needed)."* Recording is skipped entirely. Scaffolds are derived from the story's acceptance criteria, `ARCHITECTURE-SPINE.md` AD-2 (`:77-86`), the named test ids in `test-design-qa.md`, and the live schema.

---

## Step 3 — Test strategy

### The constraint that shapes every scaffold

**A red-phase test in this repository must still compile.** `TreatWarningsAsErrors` is on solution-wide via `Opinionated.DotNet.CodingStandards`, so a scaffold referencing a type that does not exist yet does not produce a failing test — it produces a **broken build**, which blocks every other suite including the four gates. That is strictly worse than no scaffold.

So every scaffold below is written against surfaces that **already exist**: HTTP through `HostProcess` (the established pattern — `SessionEndpointTests` boots the real Host as a process), SQL through `RegistrationDatabase`'s existing connection helpers, and reflection over the built EF model. None references `ActiveSpaceContext` or any other not-yet-written type. They compile today and fail at **runtime**, which is a true red phase.

Scenarios that genuinely cannot be expressed without the new types are listed under *Deferred to dev-story* rather than faked.

**Scaffolds ship `[Fact(Skip = …)]`**, per the workflow's red-green-refactor contract, with the skip reason naming the AC. Two consequences worth stating:

- It keeps `dotnet test` — *the* gate — green while the story is in flight. This repository commits straight to `main` with no branches, so a genuinely red tree would mean a red `main` for the duration.
- A skipped test is an assertion that cannot fail, which this project treats as a defect class. The mitigation is that it is time-boxed to dev-story and the skip reason names its AC, so an un-removed skip is visible. **Task 10's planted-signal discipline still applies when each is un-skipped** — activation is not completion.

No suite gains its first test here, so `TestingConventionTests.Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code` is not tripped: everything lands in `Yello.Tests.Slices` and `Yello.Tests.Architecture`, both of which already hold tests. **`Yello.Tests.Isolation` is deliberately not populated** — AD-4 requires every isolation case on both surfaces and the API-token surface does not exist until story 1.8; story 1.9 owns that suite.

### Container budget

`TESTING-CONVENTIONS.md:317-332` measures a full run at **nine** SQL Server engines, ~2 GB each, in parallel, on a 16.6 GB machine whose headroom was recorded as gone on 2026-09-18. Cost is per container-backed **class**, not per test, so the non-pooled integration cases are consolidated into **one** class rather than split per AC. The pooled-connection case must have its own container with the pool pinned (`TESTING-CONVENTIONS.md:347-350`) and cannot be merged.

**Net: +2 engines → 11.** Flagging it rather than discovering it: this is the story the warning was written about.

### Scenario map

Level names follow the repo's suites. Ids follow `{EPIC}.{STORY}-{LEVEL}-{SEQ}`.

| id | AC | Level | Pri | Scenario | Red because |
|---|---|---|:--:|---|---|
| `1.5-INT-001` | 1 | INT | P0 | A request for a Space the Account holds a Membership in resolves a context carrying that Membership's Role | no Space-scoped route exists |
| `1.5-INT-002` | 1 | INT | P0 | A request for a Space the Account holds **no** Membership in is refused, not defaulted | as above |
| `1.5-INT-003` | 1 | INT | P0 | A request for a `{spaceId}` matching no Space is refused, and indistinguishably from INT-002 | as above |
| `1.5-INT-004` | 1 | INT | P0 | An **unauthenticated** request to a Space-scoped route is refused at the authentication boundary (401), never entering Space resolution | as above |
| `1.5-INT-005` | 2 | INT | P0 | The session context is set per unit of work and names this request's resolved Space | nothing sets it per request |
| `1.5-INT-006` | 2 | INT | P0 | Two consecutive requests for **different** Spaces each return only their own Space's rows | as above |
| `1.5-INT-007` | 3 | INT | P0 | **Planted signal** for the existing catalogue gate: a Space-scoped table created without a policy fails `Every_table_with_a_SpaceId_column_is_covered_by_the_policy` | the assertion exists; the plant proving it fires does not |
| `1.5-INT-008` | 4 | INT | P0 | With RLS **not** in play, the EF query filter alone still excludes another Space's rows | no query filter exists |
| `1.5-INT-009` | 4 | INT | P0 | With query filters bypassed (`IgnoreQueryFilters()`), RLS alone still excludes | filter does not exist to bypass |
| `1.5-INT-010` | 5 | INT | P0 | Pooled-connection reuse, `Max Pool Size=1`, two Spaces consecutively — second sees only its own rows | no per-unit-of-work context |
| `1.5-INT-011` | 5 | INT | P0 | `MAXDOP = 1` is set on a database built from migrations | not configured anywhere |
| `1.5-INT-012` | 2, 5 | INT | P1 | **Empirical** confirmation that `sp_reset_connection` clears `SESSION_CONTEXT` on pool reuse, and that a second set of a `@read_only` key is refused | the design rests on these; `deferred-work.md:185` records them as inferred, never executed |
| `1.5-INT-013` | 6 | INT | P1 | The composite alternate key on `Membership(SpaceId, Id)` exists and is unique | not in the schema |
| `1.5-INT-014` | 7 | INT | P0 | The Role governing a decision equals the Membership row's **current** Role; changing the row changes the next request with no re-authentication | no authorisation path exists |
| `1.5-INT-015` | 8 | INT | P0 | The chosen B2 seeding shape actually works: two Spaces seeded, each readable only under its own context | strategy not yet chosen |
| `1.5-ARCH-001` | 2 | ARCH | P1 | Exactly one production call site sets the `SpaceId` session context | a second site exists today only in registration; gate absent |
| `1.5-ARCH-002` | 4 | ARCH | P1 | **A-13** — no raw SQL bypassing global query filters outside `Infrastructure` | gate absent |
| `1.5-ARCH-003` | 6 | ARCH | P1 | A Space-scoped cross-entity reference declared without `SpaceId` fails the build | gate absent |
| `1.5-ARCH-004` | 7 | ARCH | P0 | No Role reaches an authorisation decision from a cache outliving the request | gate absent |

### Duplicate-coverage guard — what is deliberately **not** written

Checked against the existing suite before adding anything:

- **AC3's non-nullable half** — already asserted by `PersistenceModelGateTests.Every_Space_scoped_entity_has_a_non_nullable_SpaceId:122`, derived from the built model. No new test; the story cites it.
- **AC3's policy-coverage half** — already asserted by `SpaceIsolationSchemaTests.Every_table_with_a_SpaceId_column_is_covered_by_the_policy`, which is catalogue-driven and therefore already covers tables added later. Only the **plant** (`1.5-INT-007`) is new.
- **"RLS hides rows with no context / under another Space's context"** — already proved by `RegisterAccountIntegrationTests.Space_scoped_rows_are_invisible_without_a_session_context`. `1.5-INT-008/009` are **not** duplicates: they test *layer independence*, which is a different claim and the one AC4 actually makes.
- **The Role-API ban** — already a live IL scan (`RoleApiBanTests`, Gate C) and `SessionContractTests` already plants a Space claim against the ticket. `1.5-ARCH-004`'s new half is **cache lifetime**, not claim shape.
- **A-12** (no slice re-implements Space resolution) — named P1 in `test-design-qa.md:363`. Deferred: there is no second slice to violate it until epic 2, and a gate written against one slice passes vacuously. Recorded here so it is not lost.

### Red-phase verification

Every scenario above fails today for a stated reason, and none fails for a *test* reason. The distinction matters in this repository: story 1.3's review found four gates that "asserted less than they claimed", and the framework run found a plant that passed because it compared `"en"` with `"en"`. **Every scaffold that compares a Space id therefore uses two Spaces with different ids** — a filter test where the ambient `SpaceId` happens to equal the row's `SpaceId` asserts nothing.

### Deferred to dev-story — cannot compile before the types exist

Listed rather than faked, because a scaffold referencing a missing type breaks the build:

- Unit-level: `ActiveSpaceContext` is request-scoped and not reused across requests (DI lifetime).
- Unit-level: the resolver returns *unresolvable* rather than a default when the Membership read is empty.
- Unit-level: the query-filter expression derives from application state, not from `SESSION_CONTEXT()` — assertable by inspecting the model's filter expression once one exists.

These are the three that most want writing **first** in dev-story, before the implementation they describe.

---

## Step 4 — Red-phase scaffolds created

**Execution mode:** sequential. Generation was not delegated — a subagent writing C# that must satisfy `TreatWarningsAsErrors` against an unfamiliar analyser set is a poor trade when the output has to be built and verified here anyway. That judgement was borne out: the first build failed on two analyser rules (`MA0181`, `S6605`) that no amount of prompt detail would reliably have avoided.

**Worker B (E2E) produced nothing, deliberately.** The step dispatches an E2E worker, and there is no E2E work: the stack is `backend`, step 3 states *"No E2E for pure backend projects"*, and `Yello.Tests.Client` — the only suite that could hold a browser case — references `Yello.Contracts` alone and cannot see a Space-scoped surface that does not exist. Generating browser tests here would be fabricated coverage. Recorded as a deviation rather than silently skipped.

### `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceIsolationLayerTests.cs` (6 tests)

One container-backed class, so one engine rather than six.

| Test | id | Status |
|---|---|---|
| `The_query_filter_alone_excludes_another_Spaces_rows` | `1.5-INT-008` | RED — no EF global query filter exists |
| `Row_level_security_alone_excludes_another_Spaces_rows` | `1.5-INT-009` | RED — activated with the case above; passes vacuously alone |
| `The_database_is_configured_for_serial_plans` | `1.5-INT-011` | RED — `MAXDOP` is configured nowhere |
| `Membership_carries_a_unique_key_on_SpaceId_and_Id` | `1.5-INT-013` | RED — no composite alternate key |
| `A_read_only_session_key_is_cleared_by_the_pool_and_refuses_a_second_set` | `1.5-INT-012` | **Activatable immediately** — needs no product code |
| `Two_Spaces_can_be_seeded_and_each_is_readable_only_under_its_own_context` | `1.5-INT-015` | RED — pending the B2 decision |

### `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/PooledConnectionIsolationTests.cs` (1 test)

Its own class, therefore its own container, with `Max Pool Size=1` so the second open is certainly a reuse.

| Test | id | Status |
|---|---|---|
| `A_reused_pooled_connection_returns_only_the_second_Spaces_rows` | `1.5-INT-010` | RED — no per-unit-of-work session context |

### `tests/Yello.Tests.Architecture/SpaceResolutionSeamTests.cs` (2 tests)

| Test | id | Status |
|---|---|---|
| `Exactly_one_production_call_site_sets_the_Space_session_context` | `1.5-ARCH-001` | RED once a second setter lands — **green today** (one call site) |
| `No_production_code_outside_Infrastructure_bypasses_the_query_filters` | `1.5-ARCH-002` (**A-13**) | **Green today.** A standing regression gate, not a red-phase assertion — and not a test until planted |

**`1.5-ARCH-003`** (composite-FK gate) and **`1.5-ARCH-004`** (no Role cache outliving the request) are **not generated**: both are absence assertions about code shapes that do not exist yet, so either would pass vacuously and neither can be planted against anything. They belong in dev-story alongside the implementation they police.

---

## Step 5 — Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

Two analyser errors were found and fixed before this clean result — recorded because they are the trap any future scaffold will hit:

- `MA0181: Do not use cast` — `(string)result` on a `SELECT` scalar. Use `as string`.
- `S6605: Collection-specific "Exists" should be used instead of "Any"` — on a `string[]`. Use `Array.Exists`.

### Test run

```
dotnet test Yello.slnx --no-build
Test run summary: Passed!
  total: 253   failed: 0   succeeded: 243   skipped: 10
```

Baseline at the close of story 1.4's review was **244 total / 243 passed / 1 skipped**. This run is **253 / 243 / 10**: nine scaffolds added, all skipped, **no previously-passing test changed, and the gate is still green.** That matters more than usual here — this repository commits straight to `main` with no branches, so a genuinely red tree would mean a red `main` for the length of the story.

### Container cost — measured, not assumed

The concern going in was `TESTING-CONVENTIONS.md:327-332`: nine engines, ~2 GB each, headroom gone as of 2026-09-18, *"the next story to add a container-backed class is the one that finds out."* Measured:

```
containers before: 12
dotnet test … --filter-class "Yello.Tests.Slices.Spaces.ResolveActiveSpace.*"
  total: 7   failed: 0   succeeded: 0   skipped: 7
real  0m1.481s
containers after: 12
```

**1.5 seconds and no change in container count.** A SQL Server start is roughly 30 seconds, so xunit does not construct the class fixture for a statically-skipped class. The two new engines are a cost of **activation**, not of existence — dev-story pays it, one class at a time, and the ninth-to-eleventh engine question arrives with the first activation rather than now.

### Red-phase compliance

- All nine scaffolds carry `[Fact(Skip = …)]` with the reason naming the acceptance criterion.
- None references a type story 1.5 has not written, so none can break the build.
- Every Space comparison uses two Spaces with **different** ids, against the vacuous-assertion shape that has already caught this repository once.
- Two are honestly labelled as green-today standing gates rather than red assertions.

---

## Implementation checklist — activation order for dev-story

Work one at a time: un-skip, confirm it fails for the stated reason, implement, confirm green.

1. **`A_read_only_session_key_is_cleared_by_the_pool_and_refuses_a_second_set`** — *first, before any design work.* It needs no product code and it settles the two SQL Server behaviours the whole design rests on (`deferred-work.md:185` records them as never executed). If either claim is wrong, the session-context design changes before anything is built.
2. **`The_database_is_configured_for_serial_plans`** — smallest independent change; settles story question 4 (where `MAXDOP` lives).
3. **`Membership_carries_a_unique_key_on_SpaceId_and_Id`** — schema only; settles question 5.
4. **`Two_Spaces_can_be_seeded_and_each_is_readable_only_under_its_own_context`** — settles **B2** (question 2), which story 1.9's fixtures depend on.
5. **`The_query_filter_alone_…` + `Row_level_security_alone_…`** — activate as a pair; neither proves layer independence alone.
6. **`A_reused_pooled_connection_…`** — after the per-unit-of-work context exists.
7. **`Exactly_one_production_call_site_…`** — after extracting the shared setter.
8. **`No_production_code_outside_Infrastructure_…`** — activate any time; **plant against the scanner** in the same change.

Then write the three deferred unit-level cases and the HTTP cluster once question 1 and the first Space-scoped route are settled.

### Commands

```
dotnet build Yello.slnx
dotnet test Yello.slnx --no-build
dotnet test tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class "Yello.Tests.Slices.Spaces.ResolveActiveSpace.*"
dotnet test tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-method "*SpaceResolutionSeam*"
```

No coverage command: this repository has **no coverage threshold** and `TESTING-CONVENTIONS.md:116-118` says not to invent one.

---

## Notes

- **The HTTP cluster is the gap, and it is a design gap rather than an oversight.** `1.5-INT-001` … `006` and `014` need a Space-scoped route, and story 1.5 creates no Space-scoped *resource* — it says "whatever first Space-scoped route is needed to exercise it". The route *shape* is fixed by `ARCHITECTURE-SPINE.md:267` (`/api/v1/spaces/{spaceId}/…`); the resource is not. Scaffolding them now would hardcode an invented resource, and worse, a request to a route that does not exist returns **404** — which is indistinguishable from AC1's expected refusal. Those tests would pass today for entirely the wrong reason.
- **When they are written, anchor them on the positive case.** Assert in the same test that a member *succeeds* before asserting a non-member is refused, so a missing route cannot masquerade as correct isolation.
- **Activation is not completion.** Story Task 10 requires a planted violation for every absence assertion, confirmed to fire by name and recorded in a table. Un-skipping discharges none of that.
- Two scaffolds are deliberately green today. That is stated in their skip reasons and their XML docs rather than left for a reviewer to discover — the "gates that assert less than they claim" defect has cost this repository three separate review rounds.

---

**Generated by BMad TEA Agent** — 2026-09-20
