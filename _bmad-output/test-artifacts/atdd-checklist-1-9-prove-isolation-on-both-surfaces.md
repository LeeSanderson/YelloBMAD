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
storyId: '1.9'
storyKey: '1-9-prove-isolation-on-both-surfaces'
storyFile: '_bmad-output/implementation-artifacts/1-9-prove-isolation-on-both-surfaces.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-1-9-prove-isolation-on-both-surfaces.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Isolation/IsolationWorld.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCaseListTests.cs'
  - 'tests/Yello.Tests.Isolation/IsolationBoundaryTests.cs'
  - 'tests/Yello.Tests.Isolation/IsolationTimingTests.cs'
  - 'tests/Yello.Tests.Isolation/PooledConnectionCaseTests.cs'
  - 'tests/Yello.Tests.Isolation/SchemaVarianceTests.cs'
  - 'tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj'
  - 'tests/Yello.Tests.Revocation/RevocationWorld.cs'
  - 'tests/Yello.Tests.Revocation/RevocationCaseTests.cs'
  - 'tests/Yello.Tests.Revocation/Yello.Tests.Revocation.csproj'
  - 'tests/Yello.Tests.Architecture/ContainerTopologyGateTests.cs'
  - 'tests/Yello.Tests.Architecture/ReleaseGatingSuiteTests.cs'
  - 'tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.Isolation.cs'
  - 'tests/Yello.Tests.Slices/ContainerRuntimeProbeTests.cs'
  - 'tests/Yello.Tests.Client/SpaceScopedFirstLoadTests.cs'
  - 'tests/Yello.Tests.Client/RealHostBrowserTests.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/1-9-prove-isolation-on-both-surfaces.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-1-8-issue-an-api-token-scoped-to-one-space.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 1, Story 1.9: Prove isolation on both surfaces

**Date:** 2026-10-24
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server, on two surfaces (Session cookie, API Token), in the two release-gating suites `Yello.Tests.Isolation` and `Yello.Tests.Revocation`. Also ARCH gates (topology, suite strictness, Account-scoped reads, last-Space scan), browser cases against the network-edge fake (AC7), and a real-Host browser collection (AC11).

---

## Story summary

Two empty release-gating suites gain their cases, and the test estate is rebuilt so it can afford them. The cases are a list held as data. Each row names a route, a vector and an expected answer for every surface. The theory data is the cross-product, so a row missing a surface fails as a gap, and a mapped route missing a row fails as a gap. Every case runs over the wire against a real Host, is seeded through the product, compares refusal bytes against an anchor, reads `AccessRefusal` for the kind, and checks its own slice of the Host log for secrets. Under it, SQL Server engines drop from 13 per run to about 4.

**As** the operator deciding whether Yello can ship, **I want** an isolation suite exercising every case on the browser and the API alike, **so that** SM-1's zero-disclosure gate is measured rather than believed.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - every case on both surfaces; a one-surface case fails as a gap.
2. **AC2** - two Accounts, five routes (direct id, deep link, listing, aggregate, API): one boundary 404, nothing disclosed; an identifier confers nothing.
3. **AC3** - boundary 404 vs in-Space not-found compared for duration as well as body.
4. **AC4** - pooled-connection reuse discloses nothing.
5. **AC5** - runs on every change; one disclosure fails the build.
6. **AC6** - the refusal record tells `CrossSpace` from `InsufficientRole`.
7. **AC7** - the client renders nothing from cache, requests no unlisted Space, prefetches nothing; the bar never shows the name early.
8. **AC8** - the case list is data and complete against the route table.
9. **AC9** - revocation on both surfaces, very next request.
10. **AC10** - no secret in any case's log slice.
11. **AC11** - the real browser against the real Host.
12. **AC12** - engines shared, counted and gated; the pooled case pinned and serial; a dead runtime detected.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`**, as stories 1.5-1.8 recorded. The repository has `Yello.slnx` and `.csproj` files, no `package.json`, and no Playwright or Cypress config. The browser suite uses Playwright *for .NET*. Its cases are still written, because the story's Tasks 8-9 require them and `TESTING-CONVENTIONS.md` binds the browser level to `Yello.Tests.Client`.

**Prerequisites - met:** the story is `ready-for-dev`. AC1-AC7 are verbatim from `epics.md:885-915` (`status: final`); AC8-AC12 cite their sources. The framework is MTP with six test projects. The environment is .NET SDK 10.0.303 with the Rancher container runtime. No progress file existed for this story, so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. Not loaded: `component-tdd` (no component level here), `test-healing-patterns` (nothing to heal), `ci-burn-in` (story 1.10 owns CI), Playwright Utils and Pact fragments (disabled, TypeScript-shaped). Where a fragment and `tests/TESTING-CONVENTIONS.md` disagree, the repository document wins.

**Settled decisions carried as expectations (Lee, 2026-10-24):** (1) sign-out ends Sessions server-side through `SessionsValidAfter`; an invalidated credential grants nothing. (2) A Token on `DELETE /api/v1/sessions` is treated as no credential: `400 antiforgery-token-missing`, nothing stamped.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack. No recording. Sources: the story's ACs, Tasks, case list and open questions; the live schema (six migrations); the route table (`Yello.Host/Program.cs:274-321`); the existing harnesses (`HostProcess`, `WireSession`, `DurationIndistinguishability`, `SqlServerContainerFixture`, `FakeYelloApi`, `ClientSurfaceFixture`); and the existing gates (`TestingConventionTests`, `SpaceSwitcherGateTests`).

## Step 3 - Test strategy

### The constraint that shapes every scaffold

The harness these cases run on is the story's own product work: the shared engine (Task 2), the moved `HostProcess`/`WireSession` and a long-lived `RunningHost` with a log cursor (Task 3), and the case list (Task 4). None of it exists, and this phase does not build it. So:

- **Isolation and Revocation scaffolds are written against a scaffold contract**, `IIsolationWorld` / `IRevocationWorld`. Each is one interface listing the operations the cases need (send as a surface, send with no credential, read `AccessRefusal`, read the case's log slice, the raw-SQL events). Its only entry point, `StartAsync`, throws with the Task that makes it real. **On activation, dev-story implements the contract over the real harness, or replaces it with the real types and corrects the scaffolds.** Either way, record the change in the Debug Log.
- **The case list is reached by name through reflection** (`Yello.Tests.Isolation.IsolationCases`, `Yello.Tests.Revocation.RevocationCases`), as story 1.8 reached its unwritten types. The gap-gate scaffolds fail with "does not exist (Task 4)" until it is written.
- **The per-case scaffolds are one `[Fact]` per row, each looping both surfaces.** That is the row's expectation written out, not the final shape. On activation each becomes a row of `IsolationCases` and the facts collapse into the generated `cases x surfaces` theory (Task 4, B6). This is the one deliberate deviation from AC8's shape, and it lasts only until activation.

### Container budget

A full run starts **thirteen** engines today, on a machine with about 4.5 GB free. **No scaffold adds a container-backed class.** No scaffold class takes a class, collection or assembly fixture that starts an engine. The skipped Isolation and Revocation classes start nothing. Neither does the `RealHostBrowserTests` scaffold until Task 9 gives it its collection. On activation these run on the story's shared engine: one per test process (Task 2, Q1 (a)).

### Scenario map

| id | AC | Level | Pri | Scenario | File |
|---|---|---|:--:|---|---|
| `1.9-GATE-001` | 1 | INT (data) | P0 | Every case declares an expectation for every `IsolationSurface` member, and the list is not empty | `Isolation/IsolationCaseListTests.cs` |
| `1.9-GATE-002` | 8 | INT (Host) | P0 | Every Space-scoped and Account-scoped route the real Host maps is named by a case; finds `SpaceRoutes.Active` first | `Isolation/IsolationCaseListTests.cs` |
| `1.9-GATE-003` | 8 | INT (data) | P0 | Every pending row names a story still `backlog` in `sprint-status.yaml`, with a reason | `Isolation/IsolationCaseListTests.cs` |
| `1.9-GATE-004` | 1, 8 | INT (data) | P0 | The theory data is exactly `active cases x surfaces`, each row named "case / surface" | `Isolation/IsolationCaseListTests.cs` |
| `1.9-ISO-001` | 2, 6 | INT (wire) | P0 | ISO-DIRECT-SPACE: `GET /spaces/{SB}` = anchor 404 on both surfaces, one `CrossSpace` row each | `Isolation/IsolationBoundaryTests.cs` |
| `1.9-ISO-002` | 2, 6 | INT (wire) | P0 | ISO-DIRECT-STATUSES: `GET /spaces/{SB}/statuses` = anchor 404, row, no B status name | `.Boundary` |
| `1.9-ISO-003` | 2 | INT (wire) | P0 | ISO-WRITE-TOKEN: Session 404 + row, no Token for A in SB; Token 401 = no credential | `.Boundary` |
| `1.9-ISO-004` | 2 | INT (wire) | P0 | ISO-LIST-SPACES: Session 200, exactly `[SA]`, rows exactly `{id, name}`; Token 401 | `.Boundary` |
| `1.9-ISO-005` | 2 | INT (wire) | P0 | ISO-LIST-TOKENS: Session only A's Tokens, exact row shape, no SB; Token 401 | `.Boundary` |
| `1.9-ISO-006` | 2 | INT (wire) | P0 | ISO-REVOKE-FOREIGN-TOKEN: Session 204 and B's Token still works; Token 401 | `.Boundary` |
| `1.9-ISO-007` | 2 | INT (wire) | P0 | ISO-AGGREGATE: SA's statuses after B is active in SB are exactly SA's three | `.Boundary` |
| `1.9-ISO-008` | 2 | INT (wire) | P0 | ISO-IDENTIFIER-CONFERS-NOTHING: B's id on every Space route = anchor 404 | `.Boundary` |
| `1.9-ISO-009` | 2, 6 | INT (wire) | P0 | ISO-TOKEN-BOUND-ELSEWHERE: SA2 owned by A; Session 200, Token = non-member 404 + `CrossSpace` against SA2 | `.Boundary` |
| `1.9-ISO-010` | 2 | INT (wire) | P1 | ISO-UNMATCHED-UNDER-SPACE: `/spaces/{SB}/nothing` = `/spaces/{SA}/nothing`, nothing recorded | `.Boundary` |
| `1.9-ISO-011` | 6 | INT (wire) | P0 | ISO-RECORD-KIND: every boundary refusal on both surfaces is `CrossSpace`, never `InsufficientRole` | `.Boundary` |
| `1.9-ISO-012` | 10 | INT (wire) | P1 | ISO-LOGS / I-17: applied after every case above through `AssertLogSliceHoldsNoSecret`, anchored on the Host's own line | `.Boundary` (helper) |
| `1.9-TIME-001` | 3 | INT (wire) | P0 | ISO-TIMING-BOUNDARY: foreign vs random Space id, per surface, server-side, 21/median/20%, bodies equal | `Isolation/IsolationTimingTests.cs` |
| `1.9-TIME-002` | 3 | INT (wire) | P0 | ISO-TIMING-TOKEN-ELSEWHERE: Token bound elsewhere vs non-member Session at the same Space | `.Timing` |
| `1.9-TIME-003` | 3 | INT (wire) | P0 | Planted oracle per surface: the method tells the boundary 404 from an arm that records nothing | `.Timing` |
| `1.9-POOL-001` | 4, 12 | INT (wire) | P0 | ISO-POOLED: pool of one, A and B alternately on each surface, each sees only its own statuses; exactly one Host session (not 0) | `Isolation/PooledConnectionCaseTests.cs` |
| `1.9-SCHEMA-001` | 2 | INT (db) | P0 | Every `*SpaceId` column is under the Space policy or a named variance (`AccessRefusal.TargetSpaceId`, `ApiToken.BoundSpaceId`) | `Isolation/SchemaVarianceTests.cs` |
| `1.9-REV-000` | 1, 9 | INT (data) | P0 | Every revocation case declares both surfaces | `Revocation/RevocationCaseTests.cs` |
| `1.9-REV-001` | 9 | INT (wire) | P0 | Credential ended by its holder: replayed pre-sign-out cookie 401; revoked Token 401 | `.Revocation` |
| `1.9-REV-002` | 9 | INT (wire) | P0 | Membership removed: Session 404 + `CrossSpace`; Token 401 | `.Revocation` |
| `1.9-REV-003` | 9 | INT (wire) | P0 | Space deleted: Session 404; Token 401 | `.Revocation` |
| `1.9-REV-004` | 9 | INT (wire) | P0 | Account deleted: both 401 | `.Revocation` |
| `1.9-REV-005` | 9 | INT (wire) | P0 | Role changed: next request on each surface reports the new Role | `.Revocation` |
| `1.9-REV-006` | 9 | INT (wire) | P0 | Anchor: a rolled-back Membership removal leaves both credentials working | `.Revocation` |
| `1.9-REV-007` | 9 | INT (wire) | P1 | Anchor: sign-out leaves the Token working | `.Revocation` |
| `1.9-REV-008` | 9 | INT (wire) | P0 | A Token on sign-out = no-credential `400 antiforgery-token-missing`, nothing stamped (Lee's decision 2) | `.Revocation` |
| `1.9-ARCH-001` | 12 | ARCH | P0 | No test class constructs the SQL Server fixture directly or takes it as a class fixture, outside a named list | `Architecture/ContainerTopologyGateTests.cs` |
| `1.9-ARCH-002` | 12 | ARCH | P0 | Every suite using the engine declares exactly one assembly fixture, and the engine count is stated in `TESTING-CONVENTIONS.md` and is at most 5 | `.Topology` |
| `1.9-ARCH-003` | 12 | ARCH | P1 | The engine is memory-capped (`MSSQL_MEMORY_LIMIT_MB`, at most 2048) | `.Topology` |
| `1.9-ARCH-004` | 5 | ARCH | P0 | The release-gating suites ignore no exit code at all | `Architecture/ReleaseGatingSuiteTests.cs` |
| `1.9-ARCH-005` | 2, 8 | ARCH | P0 | Only the two Account-scoped handlers depend on `IAccountSpaceDirectory` / `IAccountApiTokenDirectory` | `Architecture/SpaceSwitcherGateTests.Isolation.cs` |
| `1.9-ARCH-006` | 7 | ARCH | P1 | The last-Space scan catches `_priorSpace`, `spaceHistory`, `lastUsedSpace`, `mruSpace`, and not `SpaceDirectory` or `ActiveSpace` | `.SpaceSwitcherGateTests.Isolation` |
| `1.9-PROBE-001` | 12 | INT (no engine) | P1 | The runtime probe reports a dead endpoint as unavailable within its deadline, with a reason naming it | `Slices/ContainerRuntimeProbeTests.cs` |
| `1.9-PROBE-002` | 12 | INT (no engine) | P1 | The probe reports the configured, running runtime as available (anchor) | `Slices/ContainerRuntimeProbeTests.cs` |
| `1.9-E2E-001` | 7 | E2E (fake) | P0 | First load, Space answer held: bar shell, no name, no Role chip; released, the name appears | `Client/SpaceScopedFirstLoadTests.cs` |
| `1.9-E2E-002` | 7 | E2E (fake) | P0 | Switch A to B with B held: nothing of A in `main` or the bar | `.FirstLoad` |
| `1.9-E2E-003` | 7 | E2E (fake) | P0 | In-app navigation to an unlisted id: refusal page, id never requested | `.FirstLoad` |
| `1.9-E2E-004` | 7 | E2E (fake) | P0 | Sign-in, land, switch: Space-scoped requests only ever for the Space being shown | `.FirstLoad` |
| `1.9-E2E-005` | 7 | E2E (fake) | P1 | The two refusal pages are structurally identical (element tree, roles, `aria-*`, classes, text, focus path) | `.FirstLoad` |
| `1.9-E2E-006` | 11 | E2E (real Host) | P0 | Real sign-in across two origins lands with the bar and Role chip drawn | `Client/RealHostBrowserTests.cs` |
| `1.9-E2E-007` | 11, 2 | E2E (real Host) | P0 | A deep link to B's Space renders the refusal page, and the Host never receives B's id | `.RealHost` |
| `1.9-E2E-008` | 11 | E2E (real Host) | P0 | Sessions ended server-side: the next navigation gets a real `401` and lands on `/sign-in` with the reason, state purged | `.RealHost` |

### Priorities

P0 for every case SM-1 or SM-2 gates on (AC1-AC4, AC6, AC8, AC9, AC11, and the topology gates AC12 rests on). P1 for I-17 (as the test design rates it), the unmatched-path row, the probe, and the wider last-Space vocabulary.

### Duplicate-coverage guard - deliberately not written

- **Slices-level proofs stay where they are.** `ActiveSpaceEndpointTests.Tokens.cs` (I-12, V-3, I-11 Token half), `SpaceBoundaryPipelineTests` (in-process timing and the `InsufficientRole` filter case) and `PooledConnectionIsolationTests` (`@@SPID`) prove the mechanism. The Isolation and Revocation suites prove the property on both surfaces. Lift, do not move (story, "Previous story intelligence").
- **The existing client cases** `A_Space_id_the_switcher_did_not_list_renders_the_refusal_page_and_is_never_requested` (full load) and `Back_navigation_purges_the_departed_Space_before_the_destination_answers` already prove two AC7 clauses. E2E-003 adds only the in-app navigation half, and E2E-002 only the forward switch.

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| ISO-TIMING-IN-SPACE (boundary 404 vs in-Space not-found) | No in-Space lookup by id exists until 2.2. It is a pending row in the case list, not a scaffold | Task 6, ledger `:282` |
| `InsufficientRole` over the wire (AC6's second half) | No wire 403 exists until 2.1 (story 1.6 decision 4). It is a pending row | Task 5, ledger `:284` |
| Measuring engines started, peak memory and wall clock | A measurement, not an assertion. ARCH-002 gates the stated count | Task 2 Debug Log |
| Folding `SharedFixtureSmokeTest`, `HostStartupTests`, `StartupConnectivityCheckTests` | Product work on existing classes. ARCH-001's allow-list is where any kept private engine is named | Task 2 |
| Strengthening `No_caller_sets_both_session_context_keys` in place | ARCH-005 is the stronger gate beside it; dev-story decides whether to merge them | Task 10 |
| Plants that edit product code (disclosure on both layers, record kind swapped, Token in a log, cached name rendered, pool pin removed) | Each needs the harness this phase does not build. The scaffolds above are the gates that should catch them | Task 12 plant table |

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents. Generation stayed in-thread for the reason stories 1.5-1.8 recorded: C# under this analyser set has to be built and fixed here anyway. The first builds failed on `S126`, `IDE1006`, `MA0184`, `SA1649` (x2), `S6602`, `S6605` and `IDE0055`, and all were fixed. Worker A's output is the Isolation, Revocation, Architecture and Slices files. Worker B's is the two browser classes.

### `tests/Yello.Tests.Isolation` - 20 cases, all skipped (the suite's first cases)

| File | Cases | ids | ACs |
|---|---|---|---|
| `IsolationWorld.cs` | - | scaffold contract: `IIsolationWorld`, `IIsolationActor`, `IIsolationAnswer`, `ScaffoldSurface`, `IsolationWorldOptions`; `IsolationWorld.StartAsync` throws naming Tasks 2-3 | - |
| `IsolationCaseListTests.cs` | 4 | GATE-001 to GATE-004 | AC1, AC8 |
| `IsolationBoundaryTests.cs` | 11 | ISO-001 to ISO-011 (ISO-012, the log check, runs inside every one) | AC2, AC6, AC10 |
| `IsolationTimingTests.cs` | 3 | TIME-001 to TIME-003 | AC3 |
| `PooledConnectionCaseTests.cs` | 1 | POOL-001, in `PooledConnectionCollection` (`DisableParallelization = true`, no fixture) | AC4, AC12 |
| `SchemaVarianceTests.cs` | 1 | SCHEMA-001 | AC2 (Task 10) |

`Yello.Tests.Isolation.csproj`: **`--ignore-exit-code 8` removed**, as Task 4 requires in the same change that adds the first cases. `TestingConventionTests.Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code` counts a skipped `[Fact]` as a test, so the switch had to come off. Verified: an all-skipped MTP run under `dotnet test` exits **0** (summary "Zero tests ran", total 20, skipped 20), not 8.

### `tests/Yello.Tests.Revocation` - 9 cases, all skipped (the suite's first cases)

| File | Cases | ids | ACs |
|---|---|---|---|
| `RevocationWorld.cs` | - | scaffold contract: `IRevocationWorld`, `IRevocationAnswer`, `RevocationSurface`; `StartAsync` throws naming Tasks 2, 3, 7 | - |
| `RevocationCaseTests.cs` | 9 | REV-000 to REV-008 | AC9, AC10, AC1 |

`Yello.Tests.Revocation.csproj`: `--ignore-exit-code 8` removed for the same reason.

### `tests/Yello.Tests.Architecture` - 6 cases, no container

| Test | id | Status |
|---|---|---|
| `ContainerTopologyGateTests.No_test_class_starts_an_engine_of_its_own_outside_the_named_list` | ARCH-001 | skipped - red: AC12 |
| `ContainerTopologyGateTests.Each_suite_starts_at_most_one_engine_and_the_run_stays_within_the_stated_budget` | ARCH-002 | skipped - red: AC12 |
| `ContainerTopologyGateTests.The_engine_is_memory_capped` | ARCH-003 | skipped - red: AC12 |
| `ReleaseGatingSuiteTests.The_isolation_and_revocation_suites_ignore_no_exit_code` | ARCH-004 | **ACTIVE - green today, standing gate, planted** |
| `SpaceSwitcherGateTests.Only_the_Account_scoped_handlers_depend_on_the_Account_scoped_reads` | ARCH-005 | **ACTIVE - green today, standing gate, planted** |
| `SpaceSwitcherGateTests.The_last_Space_scan_detects_both_word_orders_and_the_names_it_missed` | ARCH-006 | skipped - red: AC7 |

ARCH-005 and ARCH-006 are in a new partial file of the existing `SpaceSwitcherGateTests`. They reuse its private `OutermostOf` and `RemembersASpace`.

### `tests/Yello.Tests.Slices/ContainerRuntimeProbeTests.cs` - 2 cases, no fixture, no engine

PROBE-001 and PROBE-002 are both **skipped - red: AC12**. The probe is reached by name as `SqlServerContainerFixture.ProbeContainerRuntimeAsync(Uri, TimeSpan)` and `(TimeSpan)`, each returning `Task<string?>`. Those names are proposals. The dead endpoint is passed in, not set through `DOCKER_HOST`, because Slices runs container classes in parallel in the same process.

### `tests/Yello.Tests.Client` - 8 cases, no container

| Test | id | Status |
|---|---|---|
| `SpaceScopedFirstLoadTests.On_first_load_nothing_of_the_Space_renders_before_the_server_answers` | E2E-001 | **ACTIVE - green today, standing gate** |
| `SpaceScopedFirstLoadTests.A_switch_renders_nothing_from_either_Space_until_the_destination_answers` | E2E-002 | **ACTIVE - green today, standing gate, planted** |
| `SpaceScopedFirstLoadTests.An_in_app_navigation_to_an_unlisted_Space_is_refused_without_a_request` | E2E-003 | **ACTIVE - green today, standing gate** |
| `SpaceScopedFirstLoadTests.Nothing_is_prefetched_across_a_Space_boundary` | E2E-004 | **ACTIVE - green today, standing gate** |
| `SpaceScopedFirstLoadTests.A_refused_listed_Space_and_an_unlisted_one_render_structurally_identical_refusal_pages` | E2E-005 | **ACTIVE - green today, standing gate** |
| `RealHostBrowserTests.Signing_in_against_the_real_Host_lands_on_the_Space_with_the_bar_drawn` | E2E-006 | skipped - red: AC11 |
| `RealHostBrowserTests.A_deep_link_to_another_Accounts_Space_is_refused_without_reaching_the_Host` | E2E-007 | skipped - red: AC11 |
| `RealHostBrowserTests.A_Session_ended_on_the_server_lands_the_client_on_sign_in_with_the_reason` | E2E-008 | skipped - red: AC11 |

`SpaceScopedFirstLoadTests` is in the existing `ClientSurfaceCollection`, which runs one dev server and one Chromium. The story expected AC7 to hold already ("AC7's cases should confirm both, not change them"), and it does. `RealHostBrowserTests` takes **no collection and no fixture**, so it starts nothing until Task 9 gives it `RealHostCollection`.

### Fixtures and seed data - every constraint checked

**No new fixture and no new container-backed class.** No scaffold seeds data today: every database touch goes through a scaffold contract that throws. The contracts state the seeding each case relies on. They were checked against `20260828164716_InitialSchema.cs`, `20261024121107_SpaceBoundaryRefusals.cs`, `20261024143930_AccountScopedMembership.cs`, `20261024145154_SessionRevocation.cs` and `20261024164849_ApiTokens.cs`:

- **Through the product** (test-design B2): `POST /api/v1/accounts` provisions each Personal Space and its Owner Membership. That satisfies `FK_Membership_Account_AccountId` (Restrict), `FK_Membership_Space_SpaceId` (Cascade), `UX_Membership_SpaceId_Owner` and `IX_Membership_SpaceId_AccountId` (unique) by construction. Tokens are issued through `POST /spaces/{id}/api-tokens`, so `FK_ApiToken_Membership_BoundSpaceId_AccountId` (Cascade) holds by construction. Every email is random (`UX_Account_NormalizedEmailAddress`).
- **`AddSecondOwnedSpaceAsync`** (ISO-009, TIME-002) writes the `Space` row before its Owner `Membership`, under that Space's session key (RLS BLOCK AFTER INSERT on `Membership`), for an Account that already exists. One Owner in a new Space satisfies `UX_Membership_SpaceId_Owner`.
- **Revocation events** (`IRevocationWorld` remarks): Membership removal runs under the Space key and cascades to `ApiToken`. Space deletion runs under the Space key, so the FILTER predicate exposes every row that the three `Cascade` FKs to `Space` reach (`Membership`, `StatusDefinition`, `SpacePresence`). Account deletion removes Memberships first, because `FK_Membership_Account_AccountId` is Restrict. It runs under the `AccountId` key and deletes the Account last, in one transaction. `AccessRefusal` carries no FK and no policy. A Role change to `viewer` leaves zero Owners, which `UX_Membership_SpaceId_Owner` (a unique filtered index) permits.
- **`SessionsValidAfter`** (`Account`, nullable, story 1.7) is read to prove a stamp and the absence of one (REV-001, REV-008).

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Test run

```
dotnet test Yello.slnx
Test run summary: Passed!   total: 479   failed: 0   succeeded: 440   skipped: 39
```

Baseline before this phase: **434 total / 433 passed / 1 skipped**. Delta: **+45 cases, +7 active and passing, +38 skipped**. No previously passing test changed. No new SQL Server engine started (still 13). The browser suite passed in the combined run.

### Red scaffolds fail for the stated reason

The Architecture red scaffolds were un-skipped in place and run. Each file was then restored from a byte copy, and the SHA-256 matched:

- ARCH-001 failed with "A test class starts a SQL Server engine of its own ...: `HostStartupTests.cs`, `SharedFixtureSmokeTest.cs`, `StartupConnectivityCheckTests.cs`, `Accounts/RegisterAccount/MigratedDatabaseFixture.cs`". These are the four constructions behind today's 13 engines.
- ARCH-002 failed with "A suite using the engine must declare exactly one assembly fixture for it: Yello.Tests.Slices: 0". The first run also listed `Yello.Tests.Architecture`, because the gate read its own patterns. The scaffold now excludes the scanning suite.
- ARCH-003: `Assert.Single()` failed because the collection was empty (no memory limit).
- ARCH-006: `Assert.Matches()` failed on `private Guid? _priorSpace;`.

Every Isolation and Revocation scaffold fails at its first line with "The isolation/revocation harness does not exist yet (story 1.9 Task 2 ... Task 3 ...)". The gap gates fail with "`Yello.Tests.Isolation.IsolationCases` does not exist (story 1.9 Task 4)". The probe fails with "`ProbeContainerRuntimeAsync(...)` does not exist (story 1.9 Task 2)".

### The standing gates were planted - 2026-10-24

For each plant, the file was edited, the solution rebuilt and the case run. A shell `trap` (the shell's `finally`) then restored the original from a byte copy, and a SHA-256 check followed. `git status` shows no product file modified.

| # | Plant | Gate | Caught |
|---|---|---|---|
| 1 | `--ignore-exit-code 2` added to `Yello.Tests.Isolation.csproj` | ARCH-004 | **yes**: "A release-gating suite ignores an exit code ...: tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj" |
| 2 | `internal static int Planted(IAccountSpaceDirectory directory)` added to `Yello.Host/Spaces/ActiveSpaceEndpoints.cs` | ARCH-005 | **yes**: the actual list gained `Yello.Host.Spaces.ActiveSpaceEndpoints` |
| 3 | `ContextBar.razor` renders the last Space name it saw (a static cache) when `ActiveSpace.Current` is null | E2E-002 | **yes**: `.context-bar__space-name` count expected 0, actual 1, while the destination was held |

E2E-001, E2E-003, E2E-004 and E2E-005 are active but **not yet planted**. Their plants edit client product code (an unlisted id requested, a prefetch, an extra wrapper `div` on one refusal path), so they belong to Task 12's plant table. Plant 3 does not reach E2E-001, because a fresh page load has no cache to render from.

### Red-phase compliance

- 38 of 45 new cases are skipped with `red: AC<n> - <why> (Task n)`.
- 7 are active and green: ARCH-004, ARCH-005 and the five AC7 browser cases. All are standing regression gates.
- Every scaffold compiles under warnings-as-errors. No scaffold references a type that story 1.9 has not written, except by name through reflection.
- Every refusal or absence assertion is anchored on a presence in the same test. Every isolation assertion uses two Accounts or two Spaces. Sets are compared sorted.
- **Known scaffold-shape deviation:** each per-row isolation and revocation fact loops over both surfaces, and three revocation facts branch on the surface. Task 3 says "a case never branches on the surface; it reads its expectation from the case row". The scaffolds meet that rule once the facts become rows of the case list on activation.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

1. **ARCH-001, ARCH-002 and ARCH-003 come first.** They test the riskiest assumption: that one SQL Server engine per test process, with a database per class and a memory cap, can carry the suites. If the full run is not green with the browser suite in the same run, this machine cannot afford anything else here. Implement Task 2, then measure engines, peak memory and wall clock. State the budget in `TESTING-CONVENTIONS.md` in the words ARCH-002 reads ("at most 5 engines"). Then activate **PROBE-001 and PROBE-002** with the daemon probe.
2. **POOL-001** is next, because it tests the second topology assumption: a `Max Pool Size=1` pool on its own database on the shared engine, in a non-parallel collection, observes exactly one Host session. Plant: remove the pin, and the count-of-one anchor must fail.
3. **The harness (Task 3):** implement `IIsolationWorld` / `IRevocationWorld` over the moved `HostProcess`/`WireSession` and a `RunningHost` with a log cursor, or replace them. Then activate **ISO-001**, the simplest row, on both surfaces.
4. **GATE-001, GATE-004 and GATE-003** come with `IsolationCases` (Task 4). Then convert ISO-001 to ISO-011 into rows and the generated theory. Then activate **GATE-002** (route table, Q4).
5. **ISO-002 to ISO-011** and **SCHEMA-001** (Tasks 5 and 10). **TIME-003 goes before TIME-001 and TIME-002**: the oracle must be caught on each surface before a "same time" verdict means anything (Task 6).
6. **REV-000 to REV-008** (Task 7). Lee's two decisions are REV-001 and REV-008.
7. **ARCH-006** comes with the widened regex (Task 10). **Keep ARCH-004 and ARCH-005 green throughout.**
8. Client: keep **E2E-001 to E2E-005** green, and run their plants in Task 12. Then activate **E2E-006, E2E-007 and E2E-008** with `RealHostCollection` (Task 9). E2E-006 goes first: it tests whether Chromium accepts the `SameSite=None; Secure` cookie from an http page origin.

### Commands

```
dotnet build Yello.slnx
dotnet test Yello.slnx
dotnet test --project tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj --no-build
dotnet test --project tests/Yello.Tests.Revocation/Yello.Tests.Revocation.csproj --no-build
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.ContainerTopologyGateTests
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.ContainerRuntimeProbeTests
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.SpaceScopedFirstLoadTests
```

If nothing was built first, `--no-build` runs zero tests. The browser suite starves under a 13-engine run. If it fails only in the combined run, re-run it alone before treating the failure as real.

## Notes and risks

- **The scaffold contracts carry the largest correction risk.** They list operations, not a design. Expect dev-story to rename or reshape them against the real harness, and to record each change.
- **TIME-001 measures the round trip** until Q5 is answered. If the server figure is taken, feed it into `DurationIndistinguishability` through an overload, not a second method.
- **ARCH-002 reads a phrase from `TESTING-CONVENTIONS.md`** ("at most N engines"). If the document states the number differently, correct the gate's pattern, not the number.
- **GATE-002 needs the real route table** (Q4 (a)). With no test-only endpoint, that means reading the endpoint data source of a Host started in-process. Otherwise fall back to Q4 (b) and record the gap.
- **E2E-003 uses `Blazor.navigateTo`** for the in-app navigation. It sets a window marker that a reload would lose, which proves the navigation stayed in-app.
- **Container cost:** none today. On activation, the Isolation, Revocation and Client real-Host classes use the shared engine (one per test process), which Task 2 builds.

---

**Generated by BMad TEA Agent** - 2026-10-24
