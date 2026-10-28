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
lastSaved: '2026-10-28'
workflowType: 'testarch-atdd'
storyId: '2.9'
storyKey: '2-9-the-board-at-five-thousand-tasks'
storyFile: '_bmad-output/implementation-artifacts/2-9-the-board-at-five-thousand-tasks.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-2-9-the-board-at-five-thousand-tasks.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential (main run; the browser scaffolds depend on the FakeYelloApi paging harness written in the same run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Boards/BoardPageWire.cs'
  - 'tests/Yello.Tests.Slices/Boards/BoardPagingTests.cs'
  - 'tests/Yello.Tests.Slices/Boards/BoardScaleTimingTests.cs'
  - 'tests/Yello.Tests.Slices/Boards/EnvelopeLoadTests.cs'
  - 'tests/Yello.Tests.Slices/Boards/PercentileTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/DatabaseIdentityTests.cs'
  - 'tests/Yello.Tests.Shared/Percentile.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Architecture/BoardPagingGateTests.cs'
  - 'tests/Yello.Tests.Architecture/DeploymentShapeTests.Tiers.cs'
  - 'tests/Yello.Tests.Client/BoardPagingTests.cs'
  - 'tests/Yello.Tests.Client/BoardRenderCostTests.cs'
  - 'tests/Yello.Tests.Client/RealHostBoardScaleTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Client/RealHostFixture.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/2-9-the-board-at-five-thousand-tasks.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/addendum.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-13.md'
  - '_bmad-output/specs/spec-yello/SPEC.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-2-6-move-and-reorder-a-task-by-explicit-control.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - 'CLAUDE.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 2, Story 2.9: The Board at five thousand Tasks

**Date:** 2026-10-28
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server (`Yello.Tests.Slices`, new classes on the
suite's one engine), isolation rows on both surfaces (`Yello.Tests.Isolation`, pending on 2.9), ARCH gates
(`Yello.Tests.Architecture`), and browser cases against the network-edge fake (`Yello.Tests.Client`). Nightly and Weekly
cases carry `Tier` and are skipped.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every earlier
story recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by `tests/TESTING-CONVENTIONS.md`.

**Prerequisites - met.** Story `ready-for-dev`; AC1-AC8 verbatim from `epics.md:1303-1336` (`status: final`), AC9-AC19 cite
their sources; sixteen open questions, each with a recommendation. MTP, the six test projects, .NET 10 SDK, Rancher
(`docker info` 29.5.3). No checklist and no progress file existed for 2.9, so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
`tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`. No `project-context.md` exists.

**Knowledge:** the core fragments and the backend set are generic TypeScript-first guidance; where they and
`tests/TESTING-CONVENTIONS.md` or `CLAUDE.md` disagree, the repository wins (one engine per test project, a database per
class, `LCK_M_%` races held never hoped for, timing in `DisableParallelization` collections, red scaffolds as
`[Fact(Skip = "red: AC<n> - ...")]`).

**Entry state:** green at HEAD `ff8b728` - 1284 total, 1281 passed, 0 failed, 3 skipped (the bundle cases). No isolation row
pending on any story (`PendingUntil(` none).

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack; no recording, no browser session opened. Sources: the story's ACs,
Tasks and open questions; the live schema (`Task`, `TaskLabel`, `Label`, `SpacePresence`, `BoundRegistry`) and the harness
(`TaskWire`, `LabelWire`, `MoveWire`, `DeleteWire`, `RunningHost`, `DurationIndistinguishability`, `FakeYelloApi`,
`KeyboardHarness`, `DragHarness`, `RealHostFixture`); 2.6's checklist as the pattern.

**Execution mode: sequential, in the main run.** The browser scaffolds depend on the `FakeYelloApi` paging harness written in
the same run, so one writer did both; nothing was built or run by a worker. One `dotnet build`, then one `dotnet test` at a
time (CLAUDE.md).

## Step 3 - Test strategy

### The constraint that shapes every scaffold

No production member of this story exists: no column-page route, no `BoardColumnResponse.NextAfter`, no
`BoardColumnPageResponse`, no `BoardPage.Size`, no `invalid-board-cursor` or `task-position-full` type, no RCSI migration, no
"Show more", no Tier filter, no Nightly/Weekly workflow. So:

- **Wire cases send `GET {BoardOf}/columns/{statusId}?after={key}`** through `BoardPageWire.ColumnOf` and read the answer as
  JSON (`nextAfter`), with scaffold constants `ColumnRoute`, `PageSize`, `InvalidBoardCursor`, `TaskPositionFull`. On
  activation, swap each for the contract member and change nothing else.
- **Architecture gates reach unwritten members by name** (reflection over `Yello.Contracts` and `Yello.Application`).
- **Isolation rows are pending rows** naming the scaffold `ColumnRoute`, each with its real `Prepare` and checker.
- **The browser fake pages only when asked** (`FakeYelloApi.PageSize`, `null` by default): every existing browser case is
  served the same bytes; the paging cases set 50.

### Open-question dependencies (scaffolded against the recommended option)

| Q | Recommended | Where the scaffold depends on it | If decided otherwise |
|---|---|---|---|
| Q1 | (a) Board first page + `nextAfter`; `GET …/board/columns/{statusId}?after=` | every wire case, the isolation rows, the fake, ARCH page-route/contract gates | change `ColumnOf`/`ColumnRoute` and the JSON reads |
| Q2 | (a) a page carries `Total` | PG-03 (`Total` on every page), PG-10, the isolation page checker | drop the `total` reads on pages |
| Q4 | (a) newer read wins, never twice | client `A_page_holding_a_Task_already_drawn_…` | rewrite to "skipped" |
| Q5 | (a) "Show more Tasks" button, accessible name starts so | every client "Show more" locator (`ShowMoreName`) | correct the locator |
| Q7 | (a) focus the column `<section>` | the two AC12 browser cases (`column:Doing`) | expect the control |
| Q8 | (a) "Board loaded." once on the cold load only | AC6 case records after it | (b): drop the wait on it |
| Q10 | (a) RCSI everywhere, per-statement snapshots | PG-14, PG-15, SCH-901 | (b): add snapshot-isolation assertions |
| Q11 | (a) `409 task-position-full` | PG-18, ARCH problem-type gate | correct the type |
| Q13 | (a) `Tier` trait, `nightly.yml`/`weekly.yml`, `--filter-not-trait` in `ci.yml` | `Tier` traits, ARCH-901..903 | rewrite to the decision, never delete |
| Q14 | (a) per-Space applock `Yello/ProjectsPerSpace/{spaceId}` | PG-17's hold and anchor | rewrite the hold to the product's shape |
| Q15 | (a) only if escalation observed | PG-16 (B completes while A held) | unchanged - the assertion is the outcome |
| Q16 | (a) .NET harness over `RunningHost` | `EnvelopeLoadTests` | needs Lee's approval for a tool |

### Scenario map (AC -> level -> case)

| AC | Level | Cases | Priority |
|---|---|---|---|
| AC1 | INT | PG-03, PG-11 (logical reads, deterministic), PG-12 (plan: index seek, no implicit key conversion) | P0 |
| AC2 | INT (Nightly LOAD) | `BoardScaleTimingTests` (p95 first/last page and Board read <= 300 ms, depth indistinguishable); PG-11 its deterministic half | P1 |
| AC3 | ARCH, E2E | `Nothing_in_the_client_virtualises_a_list` + its plant (green); `Paging_appends_cards_and_never_reuses_an_element_…` | P0 |
| AC4 | INT, E2E | PG-01, PG-03; `With_150_of_5000_loaded_…` (X-6) | P1 |
| AC5 | INT, E2E | PG-03, PG-04; `A_page_holding_a_Task_already_drawn_…`; real-Host Nightly traversal | P0 |
| AC6 | E2E | `Nothing_is_announced_when_a_page_loads_…`; X-5 asserts nothing said | P1 |
| AC7 | E2E | X-5 `Down_on_the_last_loaded_card_…` (P0); real-Host Nightly traversal | P0 |
| AC8 | INT (Weekly) | `The_cold_path_…_is_characterised_and_recorded` (P-11, records, asserts nothing about time); the decision is docs (Task 11) | P2 |
| AC9 | INT, ARCH, ISO | PG-01, 02, 05, 06, 07, 08, 09; ARCH contract/route/page-size/problem-type gates; 4 isolation rows | P0 |
| AC10 | INT, E2E | PG-10 (server half); `A_Task_created_in_a_partly_loaded_column_…`; `A_page_holding_…` | P0 |
| AC11 | E2E | X-5; `A_carried_Task_stepped_down_…`; `Show_more_appends_…`; `A_Viewer_pages_a_column_too`; `The_Move_picker_extends_…` | P0 |
| AC12 | E2E | `Closing_the_panel_on_a_Task_held_out_…`; `Deleting_a_Task_whose_card_is_not_loaded_…` | P1 |
| AC13 | INT | SCH-901 `Every_migrated_database_reads_committed_data_from_a_snapshot`; PG-14; PG-15 | P0 |
| AC14 | E2E (PR + Nightly) | `A_lifted_card_reads_no_card_rects_while_the_pointer_is_still` (PR); `BoardRenderCostTests` (Nightly, 90 ms p95) | P1 |
| AC15 | ARCH | ARCH-901 (both workflows), ARCH-902 (PR excludes tiers), ARCH-903 + plant (green) | P1 |
| AC16 | INT (Weekly LOAD) | `At_the_envelope_every_operation_class_meets_its_p95_budget` (P-1) | P1 |
| AC17 | INT | PG-16 (cross-Space write while a 5,000 x 3-Label deletion is held), PG-17 (Projects-per-Space applock) | P0 |
| AC18 | INT, ARCH | PG-18; ARCH problem-type gate | P1 |
| AC19 | - | ledger work, not a test (dev phase, Task 13) | - |

Not scaffolded, deliberately: **handler unit tests** for `ViewBoardColumnHandler` and the changed `ViewBoardHandler` - the
port (`IBoardReader.PageAsync`) and handler do not exist, a hand fake cannot compile against them, and the dev phase writes
them with the slice on `ViewBoardHandlerTests`' pattern (its `BoardWorld : IBoardReader` changes with the port). **The
RCSI race re-run** (Task 8) is a re-run of 40 existing call sites, not a new case. **`MigrationBundleEquivalenceTests`'**
RCSI column is an edit to that comparison in the same change as the migration (noted on SCH-901).

## Step 4 - Generated scaffolds

### Seed constraints checked (real schema, including foreign keys)

- **5,000 Tasks** reuse `TaskWire.SeedTasksAsync` (Slices) and its twin `WireDatabase.SeedTasksAsync` (new, for the real-Host
  case): one set-based `INSERT` under the Space's key; every non-nullable column of `dbo.[Task]` supplied (`Id`
  `ValueGeneratedNever`, `SpaceId`, `ProjectId`, `StatusId`, `Title`, `PositionKey`, `CreatedAt`; `DueDate` nullable,
  `20261025143020_TaskDueDate`); composite FK `(SpaceId, ProjectId)` by a Project the product created in that Space;
  composite FK `(SpaceId, StatusId)` by one of that Space's own Statuses; `BLOCK AFTER INSERT` by the session key; unique
  `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` by distinct zero-padded digit keys (`0` + 5 digits, so `004950` is the
  4,950th key and digit keys order numerically under BIN2).
- **Labels at scale** (`BoardPageWire.ApplyLabelsToEveryTaskAsync`): three Labels defined through the product (Labels per
  Space are unbounded today - `:523`), then one `INSERT dbo.[TaskLabel] … SELECT … FROM dbo.[Task] WHERE ProjectId = @p` per
  Label under the Space's key: all four columns supplied (`20261025175001_LabelTables.cs`), `PK_TaskLabel (SpaceId, TaskId,
  LabelId)` unique because the Labels are distinct, `FK_TaskLabel_Task_SpaceId_TaskId` -> `AK_Task_SpaceId_Id` by rows read
  from `dbo.[Task]` in the same statement, `FK_TaskLabel_Label_SpaceId_LabelId` -> `AK_Label_SpaceId_Id` by product-defined
  Labels, `BLOCK AFTER INSERT` by the session key. 15,000 rows: a few MB of the 2 GB engine.
- **Full gap** (PG-18): `MoveWire.SeedTaskAtAsync` keys `A` and `A0` in the second Status - distinct under BIN2 (unique index),
  and `PositionKey.Between("A", "A0")` has no answer (nothing sorts between a key and that key followed by the alphabet's
  first character).
- **Presence** (PG-15): `dbo.[SpacePresence] (SpaceId, AccountId, ExpiresAt)`, key `(SpaceId, AccountId)` free because the
  member has never been present in the Owner's Space; `FK SpaceId -> Space`; written under that Space's key; rolled back.
- **Held deletions** (PG-14, PG-16) reuse 2.5's `DeleteWire.DeleteProjectRowAsync` - the product's own statement shape - and
  roll back in a `finally`. **The Projects-per-Space hold** (PG-17) is `sp_getapplock` with `@LockOwner = 'Transaction'`.
- **Memory:** every scale case runs in its class's own database on the suite's one engine (`ContainerTopologyGateTests`
  budget of 5 unchanged; no new engine, no new fixture type, no private engine).
- **Isolation rows** seed through the product only (`SeedProjectWithTaskAsync`; 50 more Tasks over HTTP for the paged Board
  row, each title held as an I-17 secret).

### Files

| File | Entries (cases) | ACs | State |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Boards/BoardPageWire.cs` | harness: `ColumnOf`, `PageAsync`, `PageOf`, `ColumnsOf`, `WalkAsync`, `ApplyLabelsToEveryTaskAsync`, `LastLogicalReadsOfThePageSeekAsync`, `PlanOfThePageSeekAsync`, `ConvertsThePositionKeyImplicitly`; scaffold constants | - | active |
| `tests/Yello.Tests.Slices/Boards/BoardPagingTests.cs` | 18 (23) - PG-01..PG-18 (PG-06 4 rows, PG-09 3 rows) | AC1, AC2, AC4, AC5, AC9, AC10, AC13, AC17, AC18 | all skipped `red: AC<n>` |
| `tests/Yello.Tests.Slices/Boards/BoardScaleTimingTests.cs` | 1 - P-2 (+ `BoardScaleCollection`, `DisableParallelization`) | AC2 | skipped `red: AC2`, `Tier=Nightly` |
| `tests/Yello.Tests.Slices/Boards/EnvelopeLoadTests.cs` | 2 - P-1 load, P-11 cold start | AC16, AC8 | skipped `red: AC16` / `red: AC8`, `Tier=Weekly` |
| `tests/Yello.Tests.Slices/Boards/PercentileTests.cs` | 4 (8) | Task 3 | **green today** |
| `tests/Yello.Tests.Slices/Deployment/DatabaseIdentityTests.cs` | +1 - SCH-901 RCSI on | AC13 | skipped `red: AC13` |
| `tests/Yello.Tests.Shared/Percentile.cs` | nearest-rank helper (Task 3: "none exists") | - | active |
| `tests/Yello.Tests.Shared/WireDatabase.cs` | `SeedTasksAsync` (5,000 in one statement) | - | active |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 4 rows x 2 surfaces - `ISO-DIRECT-BOARD-COLUMN`, `ISO-BOARD-COLUMN-FOREIGN-ID-IN-OWN-SPACE` (three foreign-id paths), `ISO-BOARD-COLUMN-AGGREGATE`, `ISO-BOARD-AGGREGATE-PAGED` (the paged successor of `ISO-BOARD-AGGREGATE`'s `tasks == total`) | AC9, AC4, AC5 | **pending on 2.9** (GATE-003: all live before `review`); `IsolationCaseListTests` 14/14 green with them |
| `tests/Yello.Tests.Architecture/BoardPagingGateTests.cs` | 7 - virtualisation gate + plant, `BoardColumnResponse` append, `BoardColumnPageResponse`, page route, `BoardPage.Size`, problem types | AC3, AC9, AC18 | **2 green today**; 5 skipped |
| `tests/Yello.Tests.Architecture/DeploymentShapeTests.Tiers.cs` | 4 (5) - ARCH-901 (Nightly, Weekly), ARCH-902, ARCH-903 + plant | AC15 | **ARCH-903 and its plant green today**; ARCH-901/902 skipped |
| `tests/Yello.Tests.Client/BoardPagingTests.cs` | 13 - X-5, AC3 identity, X-6, AC6, AC10 x2, AC11 x4, AC12 x2, AC14 still-pointer rects | AC3-AC7, AC10-AC12, AC14 | all skipped |
| `tests/Yello.Tests.Client/BoardRenderCostTests.cs` | 1 - keyboard step p95 <= 90 ms at 5,000 loaded (`TouchTimingCollection`) | AC14 | skipped, `Tier=Nightly` |
| `tests/Yello.Tests.Client/RealHostBoardScaleTests.cs` | 1 - real-Host traversal of 5,000 by `↓` | AC5, AC7, AC11 | skipped, `Tier=Nightly` |
| `tests/Yello.Tests.Client/FakeYelloApi.cs` | harness: `PageSize` (opt-in, `null` = unchanged bytes), the page route, `ColumnPathOf`, `ColumnPageRequests`, `HoldColumnPageAsync`/`ReleaseColumnPage`; `FakeRequest.Query` | - | active |
| `tests/Yello.Tests.Client/RealHostFixture.cs` | `Database` exposed for the 5,000 seed | - | active |

**Browser markup and copy the scaffolds assume** (correct to what is built; record each correction): a button whose
accessible name starts "Show more Tasks" at a partly loaded column's foot, absent on a fully loaded column; the Move picker's
position list ending with an option whose name starts "Show more" for a partly loaded column, and "Bottom of column" only
when fully loaded; focus on a held-out Task's column described as `column:{Status}` by `KeyboardHarness`; the column
description "5000 Tasks" (`ColumnCountMany` "{0} Tasks", no group separator).

### Green today - standing regression gates, left active

| Case | Why it is green now | Planted |
|---|---|---|
| `BoardPagingGateTests.Nothing_in_the_client_virtualises_a_list` | no `Virtualize`/`ItemsProvider` in `Yello.Client` | **the detector's permanent plant** `The_virtualisation_scan_detects_a_plant` (green: matches `<Virtualize …>`, `ItemsProvider…`; the template's `@using …Virtualization` in `_Imports.razor` is an import, not a use - first run flagged it, the scan was narrowed to uses and the plant case holds that line) |
| `DeploymentShapeTests.Every_Tier_trait_is_Nightly_or_Weekly` | the only `Tier` traits are this run's | `The_Tier_trait_scan_reads_a_planted_value` (assembled text, so the file does not match itself) |
| `PercentileTests` (8) | the helper is written with them | known answers (100 samples 1..100, rounding up, order-independence, refusals) |

### Gates that go red by design when the dev phase builds the story (amend by exactly this, never loosen)

1. `BoardEndpointTests.The_Board_answer_has_exactly_the_contracts_properties_and_no_SpaceId` - `ColumnProperties` widens by
   exactly `nextAfter` (PG-02 is the successor).
2. `IsolationCases.BoardHoldsExactlyTheCallersTasksAsync` (`ISO-BOARD-AGGREGATE`) - its `tasks == total` line stays true
   only while a column fits one page; fold it into `ISO-BOARD-AGGREGATE-PAGED`'s rule on activation.
3. `DeploymentShapeTests.The_revision_is_promoted_only_after_the_migration_job_and_only_after_green_tests` - `Assert.Single`
   over jobs running `dotnet test Yello.slnx`; a scheduled workflow running the same command makes two. Run tiers per
   project, or narrow that gate to `ci.yml` - record which.
4. `tests/TESTING-CONVENTIONS.md:47` "Four names, and only these four" - amended by Q13 (a) in the same change; it is prose,
   not a gate (no test enforces the trait names), so nothing goes red, but the text must not contradict the `Tier` traits.
5. `BoardEndpointTests.Every_Task_of_the_Project_appears_in_exactly_one_column_and_no_other_Projects_does` - 6 Tasks, still
   one page; unchanged. `BoardTests.A_column_header_shows_…_servers_total` (Total 7, 2 cards, no cursor) - unchanged:
   without a cursor the column is fully loaded and has no "Show more".
6. Compile, not red: every hand fake of `IBoardReader` (`ViewBoardHandlerTests.BoardWorld`) follows the port change.

## Step 5 - Activation order (riskiest assumption first)

1. **`BoardRenderCostTests` - measure first** (story Task 7: "Measure first"). It tests the one assumption whose failure
   stops the story rather than asking for a fix: that append-only rendering holds 5,000 loaded cards inside the 90 ms
   near-instant budget (AC14, R4). Take the first figure before paging exists - the fake with `PageSize = null` serves all
   5,000 at once, which is paging's end state - and record it in the Debug Log; if it fails even after Q12's `ShouldRender`,
   stop and raise the AD-29 amendment as a blocker. Then activate the scaffold as written once "Show more" exists.
2. **PG-11 and PG-12** - the server's foundation: EF emits a `TOP` seek with a `varchar(128)` `>` on the BIN2 index under the
   RLS predicate, and its logical reads do not grow with depth. Plant `Skip()`/`OFFSET` (PG-11 fails) and an `nvarchar`
   parameter (PG-12 fails), each restored in a `finally`.
3. PG-01, PG-02 (with gate 1 amended), PG-05, PG-03, PG-04, PG-06..PG-10, PG-13; then the ARCH contract gates.
4. Isolation rows one at a time (swap `ColumnRoute`/`ColumnOf`/`PageSize` for the contract members); the Task's two-layer plant
   on `ISO-BOARD-COLUMN-FOREIGN-ID-IN-OWN-SPACE`; `ISO-BOARD-AGGREGATE-PAGED` with gate 2 folded. GATE-003: none pending at
   `review`.
5. SCH-901 with the RCSI migration, then PG-14, PG-15 (plant: RCSI off for that database); re-run every race class (Task 8).
6. PG-16 (observe the escalation first, then Q15), PG-17 (Q14), PG-18 (Q11).
7. Browser: X-5 first, then the AC3 identity case, X-6, AC6, AC10 x2, AC11 x4, AC12 x2, the still-pointer rects case;
   correcting assumed locators to the markup built and recording each correction.
8. ARCH-901/902 with the workflows and the filter (gate 3 amended); `BoardScaleTimingTests`, `EnvelopeLoadTests` and
   `RealHostBoardScaleTests` run once locally by their tier filter and their numbers recorded.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Validation (checklist.md)

- [x] Prerequisites satisfied (story approved, ACs testable, framework configured, Docker up).
- [x] Every AC mapped to at least one case (AC19 is ledger work); each case names its AC in its skip reason or traits.
- [x] Red phase: every scaffold whose AC is unimplemented is skipped `red: AC<n> - <why>`; the green-today cases are listed
      above as standing gates, each with its plant.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] No new test project, package, engine, fixture type or container-backed engine: new Slices classes take a database
      each on the one engine; browser cases share `ClientSurfaceCollection`, `TouchTimingCollection` and
      `RealHostCollection`; one new serial collection definition (`BoardScaleCollection`), as CLAUDE.md requires for timing.
- [x] Every timed case is in a `DisableParallelization` collection; every heavy case carries `Tier` and is skipped.
- [x] Isolation rows pending on 2.9 with reasons; GATE-001..004 green (14/14).
- [x] Races deterministic (held transactions, `LCK_M_%`, "blocked or answered"); no `Task.Delay`; AD-10: no timer.
- [x] No product code changed (test files, the shared harness and this checklist only).
- [x] No CLI browser sessions opened (no recording mode).

## Run

See the story's Dev Notes ("ATDD artifacts") and the orchestrator report for the full-run totals.

## Completion

- Story handoff: `_bmad-output/implementation-artifacts/2-9-the-board-at-five-thousand-tasks.md`, Dev Notes "ATDD artifacts -
  red-phase scaffolds already in the tree".
- Key assumptions: the recommended Q1-Q16. The riskiest is AC14's append-only render cost (measure first).
- Next workflow: `bmad-dev-story`.
