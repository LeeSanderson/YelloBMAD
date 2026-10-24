---
stepsCompleted:
  [
    'step-01-preflight-and-context',
    'step-02-generation-mode',
    'step-03-test-strategy',
    'step-04-generate-tests',
    'step-04c-aggregate',
    'step-05-validate-and-complete',
  ]
lastStep: 'step-05-validate-and-complete'
lastSaved: '2026-10-24'
workflowType: 'testarch-atdd'
storyId: '1.6'
storyKey: '1-6-refuse-at-the-space-boundary-and-record-every-refusal'
storyFile: '_bmad-output/implementation-artifacts/1-6-refuse-at-the-space-boundary-and-record-every-refusal.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-1-6-refuse-at-the-space-boundary-and-record-every-refusal.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs'
  - 'tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/SessionEndpointTests.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/1-6-refuse-at-the-space-boundary-and-record-every-refusal.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/specs/spec-yello/role-capability-matrix.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-1-5-resolve-the-active-space-per-request-enforced-in-the-database.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 1, Story 1.6: Refuse at the Space boundary, and record every refusal

**Date:** 2026-10-24
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT (container-backed, over the wire and through the pipeline filter), with ARCH for the structural gates

---

## Story summary

Story 1.5 built a pipeline with one refusal point and one counting seam. Story 1.6 hangs four things on it: a capability check that turns the Membership row's Role into a 403; an `AccessRefusal` row written by the pipeline for every 403 and every Space-boundary 404; the NFR-8 bound registry, with the one bound that lives at Space resolution (concurrent active Sessions per Space = 50); and the full refusal contract - one 404 body, one 403 shape, RFC 9457 types, a browser page. Plus the ledger's high-severity rate limit on the two PBKDF2 endpoints.

**As** someone holding an identifier I should not have, or a Role that does not permit an action, **I want** the product's answer to tell me nothing I am not entitled to know, **so that** isolation holds under error as firmly as it holds under success.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - one 404 for a non-member Space and a missing one, identical in words, shape and layout; nothing beyond the URL.
2. **AC2** - 403 is capability-shaped ("Viewers cannot edit Tasks."), and never converted to or from a 404.
3. **AC3** - the Role matrix governs every row at all four Roles; a non-member is refused every capability without disclosure.
4. **AC4** - the pipeline writes an `AccessRefusal` row (Account, Space, capability, outcome, `CrossSpace`/`InsufficientRole`) for every 403 and boundary 404; 90-day retention, purge at most daily.
5. **AC5** - the architecture suite fails any endpoint resolving `Task`/`Project`/`Label`/`StatusDefinition` without `{spaceId}`.
6. **AC6** - every refusal is RFC 9457 with a stable `type`.
7. **AC7** - a bound refuses visibly, machine-readably, in the creating transaction.
8. **AC8** - the registry declares all six NFR-8 bounds with their consulting operations.
9. **AC9** - a refusal in the browser is a page, the route back is switcher-sourced, the client owns the string.
10. **AC10** - the two boundary-404 causes are indistinguishable in duration by the B3 method, with a permanent planted oracle.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` -> **`backend`**. No `package.json`, no Playwright/Cypress config; `Yello.slnx` and `.csproj` files. The browser suite (`Yello.Tests.Client`) is Playwright *for .NET* and fires none of the frontend indicators - the same classification and reasoning as story 1.5's checklist.

**Prerequisites - met:**

| Requirement | State |
|---|---|
| Story approved, clear AC | `ready-for-dev`, AC1-AC9 verbatim from `epics.md:734-790` (`status: final`), AC10 from the handoff |
| Backend test config | six test projects, MTP runner via `global.json` |
| Development environment | .NET SDK 10.0.303; container runtime **up** (Rancher, 16.6 GB reported to Docker) |

**No progress file existed for this story** (only story 1.5's checklist), so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `test_stack_type: auto`, `tea_execution_mode: auto`.

**Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. **Not loaded, with reasons:** `component-tdd` (no component level in this repository's test levels), `test-healing-patterns` (no failing suite to heal), `ci-burn-in` (CI is story 1.10's), selector/timing/Playwright Utils/Pact fragments (backend stack; disabled in config). As in 1.5, the fragments are TypeScript-shaped; where they disagree with `tests/TESTING-CONVENTIONS.md`, the repository document wins - it is enforced by gates.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack (step 2 section 1). No recording. Sources: the story's AC and Tasks, `role-capability-matrix.md:9-28`, the live schema in the two migrations, and the existing test harnesses (`HostProcess`, `WireSession`, `SpaceSeeding`, `RegistrationDatabase`, `DurationIndistinguishability`, `ActiveSpaceUnitOfWorkTests`' direct filter invocation).

## Step 3 - Test strategy

### The constraint that shapes every scaffold

A red-phase scaffold here must **compile**: warnings are errors solution-wide, so a reference to a type story 1.6 has not written breaks the build and every gate with it. Story 1.5 answered this by writing only against existing surfaces and deferring the rest. Story 1.6's core types (`Capability`, `RoleCapabilityMatrix`, `RequiredCapability`, `BoundRegistry`, `IAccessRefusalRecorder`, `AccessRefusal`) have no existing surface to stand on, so this run takes one step further: **those types are reached by name, through reflection**, from a single constant per class. A scaffold therefore fails at run time with "`<type>` is not in `<assembly>` - story 1.6 Task N writes it" rather than breaking the build. Everything else - HTTP bodies, SQL tables, the route table - is reached the way 1.5 reached it. **On activation, replace each by-name lookup with the typed reference it stands for.**

The names used are the story's own where it gives one (`Capability.ReadProjectsAndTasks`, `IAccessRefusalRecorder`, `TargetSpaceId`, `SpacePresence`, `CrossSpace`/`InsufficientRole`) and this run's where it does not (the other fourteen capability names, the six bound ids and operation names, `RequiredCapability`). They are the scaffolds' contract, not a design decision: a different name chosen in dev-story is a correction made in the scaffold and recorded in the Debug Log.

### Container budget

A full run starts **twelve** engines today. Wire cases go into the **existing** `ActiveSpaceEndpointTests` and `SessionEndpointTests` (no new engine). The pipeline-level cases need a database and are not wire cases, so they share **one** new class, `SpaceBoundaryPipelineTests`: **+1 engine on activation, 0 while skipped** (xunit does not build a class fixture for a class whose every case is statically skipped - measured in story 1.5, and confirmed below: the full run's engine count did not change).

### Scenario map

| id | AC | Level | Pri | Scenario | File |
|---|---|---|:--:|---|---|
| `1.6-INT-001` | 1, 6 | INT (wire) | P0 | Both boundary 404s answer `not-available`, byte-identical, problem+json, no Space name, no Status, no id beyond the URL (I-5) | `ActiveSpaceEndpointTests` |
| `1.6-INT-002` | 4 | INT (wire) | P0 | Each boundary 404 writes one `CrossSpace` row (outcome 404, target Space, capability); a served request writes none (I-13) | `ActiveSpaceEndpointTests` |
| `1.6-INT-003` | 4 | INT (wire) | P0 | A 401 is not recorded (anchored on a recorded 404 in the same test) | `ActiveSpaceEndpointTests` |
| `1.6-INT-004` | 1, 6 | INT (wire) | P1 | A non-GUID `{spaceId}` answers the boundary 404's body and records nothing (open question 8) | `ActiveSpaceEndpointTests` |
| `1.6-INT-005` | 7 | INT (wire) | P0 | The 51st distinct Account in a Space gets 409 `bound-reached` with `bound` and `limit`; its presence is not written (in-transaction) | `ActiveSpaceEndpointTests` |
| `1.6-INT-006` | 7 | INT (wire) | P0 | An Account already present is served at the bound | `ActiveSpaceEndpointTests` |
| `1.6-INT-007` | 7 | INT (wire) | P0 | An expired presence frees a slot (AD-27: expiry in the `WHERE`) | `ActiveSpaceEndpointTests` |
| `1.6-INT-008` | 4 | INT (wire) | P0 | Rows older than 90 days are purged by request traffic; 89-day rows kept (`PRD-12-11`) | `ActiveSpaceEndpointTests` |
| `1.6-INT-009` | 2, 3 | INT (pipeline) | P0 | Theory, 20 rows: the matrix decides at each Role; a denied capability is 403 `insufficient-role` with a `capability` extension and never reaches the endpoint (I-9) | `SpaceBoundaryPipelineTests` |
| `1.6-INT-010` | 3 | INT (pipeline) | P0 | Every one of the 15 capabilities is refused 404 `not-available` for a non-member (I-1), anchored on the same Account being served in its own Space | `SpaceBoundaryPipelineTests` |
| `1.6-INT-011` | 4 | INT (pipeline) | P0 | A 403 writes one `InsufficientRole` row; the served read before it writes none | `SpaceBoundaryPipelineTests` |
| `1.6-INT-012` | 10 | INT (pipeline) | P0 | Missing Space vs non-member Space: indistinguishable in duration by `DurationIndistinguishability` (I-6) | `SpaceBoundaryPipelineTests` |
| `1.6-INT-013` | 10 | INT (pipeline) | P0 | **Permanent planted oracle**: one arm's recorder replaced by a no-op `DispatchProxy`; the method must tell them apart | `SpaceBoundaryPipelineTests` |
| `1.6-INT-014` | Task 9 | INT (wire) | P0 | PBKDF2 endpoints rate-limited: 429, `Retry-After`, `rate-limited` type, identical for known and unknown address; partition not on the address | `SessionEndpointTests` |
| `1.6-ARCH-001` | 3 | ARCH | P0 | The product matrix agrees with an independent transcription in every Role x capability cell | `RefusalBoundaryGateTests` |
| `1.6-ARCH-002` | 3 | ARCH | P0 | `Capability` is exactly the 15 Role-decided rows (the Offer-acceptance row may exist, AD-26) | `RefusalBoundaryGateTests` |
| `1.6-ARCH-003` | 8 | ARCH | P0 | A-7: the registry declares exactly six bounds with AC8's values and operations | `RefusalBoundaryGateTests` |
| `1.6-ARCH-004` | 5 | ARCH | P0 | A-4: no endpoint without `{spaceId}` reaches `Task`/`Project`/`Label`/`StatusDefinition` - real route table, IL reachability, vacuity guard | `RefusalBoundaryGateTests` |
| `1.6-ARCH-005` | 2, 3 | ARCH | P0 | Every `{spaceId}` endpoint declares exactly one capability (fail closed) | `RefusalBoundaryGateTests` |
| `1.6-ARCH-006` | 2, 4 | ARCH | P1 | A-12: only the pipeline depends on the matrix and the recorder port | `RefusalBoundaryGateTests` |
| `1.6-ARCH-007` | 4 | ARCH | P0 | Only the recorder and the purge refer to `AccessRefusal` (write-only, AD-24) | `RefusalBoundaryGateTests` |
| `1.6-ARCH-008` | 4 | ARCH | P0 | A-9: no `BackgroundService`/`IHostedService`/`PeriodicTimer`/`Timer` in runtime assemblies | `RefusalBoundaryGateTests` |
| `1.6-ARCH-009` | 6 | ARCH | P0 | `ProblemTypes` declares `not-available`, `insufficient-role`, `bound-reached`, `rate-limited`; `SpaceNotFound` is gone | `RefusalBoundaryGateTests` |
| `1.6-ARCH-010` | 9 | ARCH | P1 | `ClientCopy.resx` holds "Not available." and "Viewers cannot edit Tasks."; nothing says "where you were" | `RefusalBoundaryGateTests` |

### Duplicate-coverage guard - what is deliberately not written

- **The 1.5 case `A_member_is_served_and_a_non_member_is_refused_exactly_as_a_missing_Space_is`** is kept, not replaced (Task 11). `1.6-INT-001` is a new case beside it: it asserts the new `type`, the content type and the no-id rule. The 1.5 case asserts `ProblemTypes.SpaceNotFound` and **will stop compiling** when Task 5 removes that constant - update its type assertion then; that is expected, not a regression.
- **`ActiveSpaceUnitOfWorkTests`** (rollback on >=400, materialisation, commit token) is not duplicated. It **will throw** the moment `ResolveCoreAsync` resolves a new service from its bare `ServiceCollection` (story Task 11 says so); register fakes there.
- **AC1's 401-before-resolution** is 1.5's `A_request_with_no_Session_is_refused_with_401_before_any_Space_is_resolved`; `1.6-INT-003` adds only "and is not recorded".

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | When |
|---|---|---|
| Purge runs **at most once per 24 h per process** (fake `TimeProvider`, two triggers inside 24 h, one after) | The purge's trigger API is dev-story's design (question 3); a reflection guess at a method signature would be fiction. `1.6-INT-008` covers the 90-day boundary over the wire | Task 7, as a unit case with a hand-written `TimeProvider` subclass (no new package) |
| Every `{spaceId}` route is mapped through the group carrying `ActiveSpaceResolution` (`deferred-work.md` 1.5 review) | The throwaway route table in `1.6-ARCH-004` has no group filters; the real group exists only inside `Program.cs` top-level code. The mechanism (a `RouteGroupBuilder`-typed `Map*`, or an IL scan of `<Main>$`) is a design choice | Task 8 |
| An unauthenticated Space-scoped **write** gets 401 before anti-forgery (question 11) | No Space-scoped `POST` exists; a `POST` to the statuses route is answered 405 by routing before any filter runs | Task 5, against a fake write endpoint in `SpaceBoundaryPipelineTests`' harness, or ledger to 2.1 |
| No handler converts a 403 to a 404 or back, **over the wire** | No write endpoint exists to produce a wire 403 (question 4) | Ledger to story 2.1 |
| No slice inspects `Role` (A-12 authorisation clause) | Today no slice does, and a gate with no possible offender passes vacuously; it needs its plant | Task 8, with its plant |
| AC9 browser case: both 404s render the identical page | `Yello.Tests.Client` has no Host and no `page.RouteAsync` fakes (Task 10) | Task 10, or ledger |

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** Probing allowed subagents, but generation was kept in-thread for the reason 1.5 recorded: C# that must satisfy this analyser set has to be built and fixed here anyway. The first builds failed on `S6605`, `MA0181`, `S1067` (x3), `MA0190` and `IDE0055`; all fixed. **Worker B (E2E) produced nothing**, deliberately: `backend` stack, "no E2E for pure backend projects", and the one browser case AC9 wants needs infrastructure that does not exist (table above).

### `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs` - 10 cases, new class, no container

| Test | id | Status |
|---|---|---|
| `The_capability_matrix_agrees_with_the_transcription_in_every_cell` | ARCH-001 | skipped - red: AC3 |
| `The_capability_vocabulary_is_exactly_the_Role_decided_rows` | ARCH-002 | skipped - red: AC3 |
| `The_bound_registry_declares_all_six_bounds_with_their_values_and_operations` | ARCH-003 | skipped - red: AC8 |
| `No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity` | ARCH-004 | **ACTIVE - green today, standing gate, planted** |
| `Every_spaceId_endpoint_declares_exactly_one_capability` | ARCH-005 | skipped - red: AC2/AC3 |
| `Only_the_pipeline_depends_on_the_capability_matrix_and_the_refusal_recorder` | ARCH-006 | skipped - red: AC2/AC4 |
| `Only_the_recorder_and_the_purge_refer_to_the_refusal_record` | ARCH-007 | skipped - red: AC4 |
| `No_production_code_runs_on_a_timer_or_as_a_hosted_service` | ARCH-008 | **ACTIVE - green today, standing gate, planted** |
| `The_refusal_problem_types_are_declared_and_one_type_serves_every_404` | ARCH-009 | skipped - red: AC6 |
| `The_refusal_copy_is_the_clients_own_and_names_no_remembered_location` | ARCH-010 | skipped - red: AC9 |

### `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.cs` - 8 cases added to the existing class (no new engine)

INT-001 `Both_boundary_404s_answer_not_available_and_carry_nothing_beyond_the_URL` (AC1/AC6), INT-002 `Each_boundary_404_records_one_CrossSpace_refusal_and_a_served_request_records_none` (AC4), INT-003 `A_request_refused_at_authentication_records_no_refusal` (AC4), INT-004 `A_non_GUID_spaceId_is_answered_as_a_boundary_404_is_and_records_nothing` (AC1/AC6), INT-005 `The_51st_distinct_Account_in_a_Space_is_refused_with_the_bound` (AC7), INT-006 `An_Account_already_present_is_served_when_the_Space_is_at_the_bound` (AC7), INT-007 `An_expired_presence_frees_a_slot_under_the_bound` (AC7), INT-008 `Refusal_records_past_90_days_are_purged_by_request_traffic_and_younger_ones_kept` (AC4). **All skipped - red.** The class became `partial` for a `[GeneratedRegex]` property.

### `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs` - 5 cases (one a 20-row theory), new container-backed class

INT-009 `The_matrix_decides_whether_a_member_reaches_the_endpoint` (theory, AC2/AC3), INT-010 `Every_capability_is_refused_with_the_boundary_404_for_a_non_member` (AC3), INT-011 `A_403_records_one_InsufficientRole_refusal` (AC4), INT-012 `A_missing_Space_and_a_Space_without_Membership_take_the_same_time_to_refuse` (AC10), INT-013 `The_duration_method_tells_a_refusal_that_skips_its_record_from_one_that_writes_it` (AC10 oracle). **All skipped - red.**

### `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/SessionEndpointTests.cs` - 1 case added to the existing class

INT-014 `Sign_in_past_the_limit_is_refused_with_429_alike_for_a_known_and_an_unknown_address` (Task 9). **Skipped - red.** The configuration key `RateLimiting__PermitsPerWindow` is this run's guess at Task 9's key.

### Fixtures and seed data - every constraint checked

No new fixture. Seeding reuses story 1.5's B2 `SpaceSeeding` blocks and `RegistrationDatabase`'s connections. Constraints checked against `20260828164716_InitialSchema.cs` and `20260925132825_ActiveSpaceResolution.cs`:

- **`FK_Membership_Account_AccountId`** - every seeded Membership's Account is added in the same block before it, or is the Account registered through the product.
- **`FK_Membership_Space_SpaceId`, `FK_StatusDefinition_Space_SpaceId`** - the `Space` is added in the block that first writes into it.
- **`IX_Membership_SpaceId_AccountId` (unique)** - one Membership per Account per Space, including the 50/51-Account crowd.
- **`IX_StatusDefinition_SpaceId_Name` (unique)** - one Status per seeded Space.
- **Account `NormalizedEmailAddress` (unique)** - random addresses everywhere (`SpaceSeeding.NewAccount`, `RandomAddress`).
- **RLS BLOCK predicates** on `Membership`/`StatusDefinition` - every block runs under its own Space's session context; `SpacePresence` rows are inserted on `OpenForSpaceAsync(spaceId)` for the same reason (the story requires the table to carry `SpaceId` and the policy).
- **`AccessRefusal`** (not yet in the schema) - rows written by the purge case supply `Id` (no key generated by the database), carry an `AccountId` that exists in `Account` (satisfies an FK to `Account` if dev-story declares one) and a random `TargetSpaceId` (question 2: no FK to `Space`, a refusal for a missing Space is still recorded).
- **`SpacePresence`** (not yet in the schema) - every `AccountId` holds a Membership in the Space, so an FK to `Account`, `Space` or the Membership is satisfied; keyed `(SpaceId, AccountId)` with no surrogate id (question 1). If dev-story adds a surrogate `Id`, the insert must supply it.

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Test run

```
dotnet test Yello.slnx
Test run summary: Passed!   total: 298   failed: 0   succeeded: 275   skipped: 23
```

Baseline before this phase: **274 / 273 passed / 1 skipped**. Delta **+24 cases**: +10 architecture (2 active and passing, 8 skipped), +8 and +1 slice cases in existing classes (skipped), +5 in the new pipeline class (skipped; the 20-row theory reports as one skipped case). **No previously passing test changed.** The new container-backed class started no engine: every case in it is statically skipped.

### The two standing gates were planted - 2026-10-24

Plants were written into production source, the suite built and run, and the originals restored in a `finally` from content held in memory (no `git checkout`; `git status` afterwards showed only the new test file).

| Plant | Gate | Caught |
|---|---|---|
| `MapBareStatusesPlant` in `SpaceStatusEndpoints`: `GET /api/v1/statuses/{id:guid}` -> `ListAsync` | ARCH-004 | **yes** - "`/api/v1/statuses/{id:guid}` reach a Space-scoped entity without {spaceId} in the path" |
| `Yello.Infrastructure/PlantedTimer.cs` returning a `PeriodicTimer` | ARCH-008 | **yes** - "`Yello.Infrastructure.PlantedTimer` uses `System.Threading.PeriodicTimer`" |

One harness finding worth keeping: **replacing `IServiceProviderIsService` does not work** - the DI container answers that service itself and ignores a registration - so the route-table harness registers every Yello type outside `Yello.Contracts` with a throwing factory instead. It classifies; it never resolves.

### Red-phase compliance

- 22 of 24 new cases are skipped, each with `red: AC<n> - <why>` (or `red: ledger (high)` for the rate limit, which has no AC) naming the story task that makes it pass.
- 2 are active, green today, documented in the class remarks and here as standing gates, and planted.
- None references a type story 1.6 has not written; every scaffold compiles.
- Every refusal assertion is anchored on a success in the same test; every isolation assertion uses two Spaces with different ids.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

1. **INT-013 `The_duration_method_tells_a_refusal_that_skips_its_record_from_one_that_writes_it`** - first, as soon as the recorder writes a row. It tests the riskiest assumption: that the B3 method, built for a ~270 ms missing hash, can see a skipped single-row insert at all. If it cannot, AC10's oracle clause is unsatisfiable as written and needs a decision from Lee before the design builds on it. Then INT-012 beside it (alone it passes vacuously).
2. **INT-002, INT-003, INT-011** - the record write. INT-002 also exercises `deferred-work.md:246`: the write must use a fresh scope/`DbContext`, never a second `SpaceSessionContext.SetAsync` on the same connection (error 15664).
3. **ARCH-001, ARCH-002** - the matrix as data (Task 2), then **INT-009, INT-010** through the pipeline (Task 3), then **ARCH-005, ARCH-006** with their plants.
4. **ARCH-009, INT-001, INT-004** - the refusal contract (Task 5). Update the 1.5 case's `ProblemTypes.SpaceNotFound` assertion in the same change.
5. **ARCH-003, INT-005, INT-006, INT-007** - the registry and the Sessions-per-Space bound (Task 6).
6. **INT-008, ARCH-007** - retention and the purge (Task 7), plus the unwritten "at most daily" unit case.
7. **INT-014** - rate limiting (Task 9). **Before activating, raise the limit for every other wire test**: `SessionEndpointTests.The_two_failure_causes_take_the_same_time_over_the_wire` alone signs in 44+ times on one Host, from one IP, and a 10-per-minute default would turn it red. Default the limit high in `HostProcess`'s settings, or configure it per class.
8. **ARCH-010** - client copy (Task 10).

The two active gates (ARCH-004, ARCH-008) need nothing; they are already planted.

### Commands

```
dotnet build Yello.slnx
dotnet test Yello.slnx
dotnet test tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class "Yello.Tests.Architecture.RefusalBoundaryGateTests"
dotnet test tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class "Yello.Tests.Slices.Spaces.ResolveActiveSpace.SpaceBoundaryPipelineTests"
dotnet test tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class "Yello.Tests.Slices.Spaces.ResolveActiveSpace.ActiveSpaceEndpointTests"
```

No coverage command: there is no coverage threshold, and `TESTING-CONVENTIONS.md` says not to invent one.

## Notes and risks

- **Reflection is scaffolding, not the end state.** It keeps the build green before the types exist; once they do, each lookup should become a typed reference so a rename is a compile error rather than a run-time one.
- **AC10's per-sample cost includes building a service provider.** Both arms pay it equally, but if noise swamps the effect in step 1, hoist the provider out of the sample before concluding anything about the method.
- **The theory reports as one case** in the totals while it is skipped; it runs 20 rows once active.
- **Container cost on activation: +1 engine (13 on a full run)**, from `SpaceBoundaryPipelineTests`. If the full run misbehaves, check memory first (`TESTING-CONVENTIONS.md`, and the "full test run starves the browser suite" note).

---

**Generated by BMad TEA Agent** - 2026-10-24
