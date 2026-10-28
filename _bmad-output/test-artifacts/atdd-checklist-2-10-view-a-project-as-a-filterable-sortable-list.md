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
storyId: '2.10'
storyKey: '2-10-view-a-project-as-a-filterable-sortable-list'
storyFile: '_bmad-output/implementation-artifacts/2-10-view-a-project-as-a-filterable-sortable-list.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-2-10-view-a-project-as-a-filterable-sortable-list.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential (main run; the browser scaffolds depend on the FakeYelloApi List harness written in the same run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Lists/ListWire.cs'
  - 'tests/Yello.Tests.Slices/Lists/ListPagingTests.cs'
  - 'tests/Yello.Tests.Slices/Lists/ListScaleTimingTests.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Architecture/ListViewGateTests.cs'
  - 'tests/Yello.Tests.Client/ListViewTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/2-10-view-a-project-as-a-filterable-sortable-list.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-2-9-the-board-at-five-thousand-tasks.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - 'CLAUDE.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 2, Story 2.10: View a Project as a filterable, sortable list

**Date:** 2026-10-28
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server (`Yello.Tests.Slices`, new classes on the
suite's one engine), isolation rows on both surfaces (`Yello.Tests.Isolation`, pending on 2.10), ARCH gates on the EF model
and the contracts (`Yello.Tests.Architecture`), and browser cases against the network-edge fake (`Yello.Tests.Client`). The
one timed case carries `Tier=Nightly` and is `Explicit`.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every earlier
story recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by `tests/TESTING-CONVENTIONS.md`.

**Prerequisites - met.** Story `ready-for-dev`; AC1-AC9 verbatim from `epics.md:1346-1384` (`status: final`), AC10-AC22
cite their sources; eleven open questions, each with a recommendation. MTP, the six test projects, .NET 10 SDK, Rancher
(`docker info` 29.5.3). No checklist and no progress file existed for 2.10, so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
`tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`. No `project-context.md` exists.

**Knowledge:** the core and backend fragments are generic TypeScript-first guidance; where they and
`tests/TESTING-CONVENTIONS.md` or `CLAUDE.md` disagree, the repository wins (one engine per test project, a database per
class, `LCK_M_%` races held never hoped for, timing in `DisableParallelization` collections, red scaffolds as
`[Fact(Skip = "red: AC<n> - ...")]`, expected `uniqueidentifier` order from SQL, never `Guid.CompareTo`).

**Entry state:** green at HEAD `e4cd50d` - 1364 total, 1356 passed, 0 failed, 8 skipped (3 bundle, 5 Explicit). No isolation
row pending on any story.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack; no recording, no browser session opened. Sources: the story's ACs,
Tasks and open questions; the live schema (`Task`, `TaskLabel`, `Label`, `StatusDefinition`) and the harness (`TaskWire`,
`LabelWire`, `TaskEditWire`, `DeleteWire`, `BoardPageWire`, `RunningHost`, `Percentile`, `FakeYelloApi`, `DragHarness`,
`KeyboardHarness`, `IsolationCases`); 2.9's checklist as the pattern.

**Execution mode: sequential, in the main run.** The browser scaffolds depend on the `FakeYelloApi` List harness written in
the same run, so one writer did both. One `dotnet build`, then one `dotnet test` at a time (CLAUDE.md).

## Step 3 - Test strategy

### The constraint that shapes every scaffold

No production member of this story exists: no List route, no `ListRoutes`/`ListSorts`/`ListPageResponse`, no `ListPage`,
no `FirstLabelName` column, no sort indexes, no problem types, no client page. So:

- **Wire cases build the path themselves** (`ListWire.PathOf`) and read the answer as JSON, with scaffold constants
  `ListRoute`, `PageSize`, `SortStatus` (`"status"`), `SortDueDate` (`"due-date"`), `SortLabel` (`"label"`),
  `InvalidListQuery`, `InvalidListCursor`. On activation swap each for the contract member and change nothing else.
- **Architecture gates reach unwritten members by name** (reflection), including the sort-to-column map assumed as a static
  `IReadOnlyDictionary<string, string>` on an Infrastructure type named `ListSortColumns`.
- **Isolation rows are pending rows** naming the scaffold `ListRoute`, each with its real `Prepare` and checker.
- **The browser fake answers the List route** (no earlier case requests it, so their bytes are unchanged).
- **Expected orders are computed by SQL Server** (`ListWire.ExpectedOrderAsync`); the Label key there is computed from
  `TaskLabel`/`Label`, never read from the product's `FirstLabelName`, so a key the product failed to maintain misorders the walk.

### Open-question dependencies (scaffolded against the recommended option)

| Q | Recommended | Where the scaffold depends on it | If decided otherwise |
|---|---|---|---|
| Q1 | (a) one route `…/projects/{p}/list`; `{project, statuses, total, tasks, nextAfter}`; opaque cursor bound to the sort | every wire case, isolation rows, ARCH contract gate, the fake | change `PathOf`/`ListRoute`/`PageOf` and the ARCH member list |
| Q2 | (a) 50, server-owned; "Show more Tasks"; "Showing n of N" | LV-12, every paging assertion, browser paging cases | change `PageSize` and the browser copy |
| Q3 | (a) first Label name in chip order, `FirstLabelName`, NULL last | LV-04, LV-05, LV-16, LV-17, LV-18/19 (label), `BackfillFirstLabelNamesAsync` | rewrite the Label expected order |
| Q4 | (a) any-of Status/Label, due `[from, to)` or none; foreign ids match nothing | LV-06, LV-07, ISO-LIST-FOREIGN-FILTER-IN-OWN-SPACE | rewrite to the decision |
| Q5 | (a) ascending only, NULL last | LV-02, LV-03, LV-05, the expected-order SQL | add direction rows |
| Q6 | (a) separate client route and a two-link `nav` | browser toggle case, purge case | correct the locators |
| Q7 | (a) effective order then `TaskId`; index `(SpaceId, ProjectId, StatusId, Id)` | LV-01, LV-19 (status), P-4 | change the tiebreaker in the expected SQL |
| Q8 | (a) native table, one roving tab stop, `aria-rowcount`/`aria-rowindex` | browser keyboard and X-6 cases | correct the selectors |
| Q10 | (a) stacked rows below a breakpoint | 320px case | unchanged - the assertion is the outcome |
| Q11 | (a) "Nothing here yet." with no action | not asserted (only the filtered empty state is AC8's) | - |

### Scenario map (AC -> level -> case)

| AC | Level | Cases | Priority |
|---|---|---|---|
| AC1 | E2E, INT | browser toggle; LV-01..LV-05 (rows, sorts) | P0 |
| AC2 | INT, ARCH | LV-10 (`sort=assignee` refused); ARCH `The_offered_sorts_are_exactly_three_and_none_is_the_Assignee` | P1 |
| AC3 | INT, ISO | LV-15; ISO-DIRECT-LIST, ISO-LIST-FOREIGN-ID-IN-OWN-SPACE, ISO-LIST-AGGREGATE | P0 |
| AC4 | INT | LV-01, LV-02, LV-04, LV-08 (P-3) | P0 |
| AC5 | INT | LV-03, LV-05 (NULL last across a page boundary) | P0 |
| AC6 | ARCH, INT | P-4 gate + its detector plant (green); LV-19 plan per sort | P0 |
| AC7 | E2E | Show more, `↓` past the last row by identity, X-6 | P0 |
| AC8 | E2E | empty filter: "No Tasks match." alone | P1 |
| AC9 | E2E | filter change announces the count once | P1 |
| AC10 | INT (+ Nightly LOAD) | LV-18 (logical reads, 6 rows); `ListScaleTimingTests` (p95 <= 300 ms, 12 operations) | P0 / P1 |
| AC11 | INT, E2E | LV-06 (total == filtered walk under every sort); `aria-rowcount`, "Showing n of N" | P0 |
| AC12 | INT, ARCH | LV-08 (stale cursor), LV-09, LV-10, LV-11, LV-12; ARCH page-size and problem-type gates; browser 404 | P0 / P1 |
| AC13 | INT | LV-04, LV-05, LV-16 (maintenance x4), LV-17 (race) | P0 |
| AC14 | INT, E2E | LV-01 (effective order, not name order); browser initial sort | P0 |
| AC15 | INT, ISO | LV-06, LV-07; ISO-LIST-FOREIGN-FILTER-IN-OWN-SPACE | P0 |
| AC16 | INT, E2E | LV-13 (both surfaces); browser Viewer case | P0 |
| AC17 | E2E | purge on Space switch + Web Storage; no name in URL or title | P0 |
| AC18 | E2E, ARCH | append-never-recycle; sort change replaces rows; ARCH virtualisation scan covers `TaskListView.razor` | P0 |
| AC19 | E2E | paging says nothing; "List loaded." per locale | P1 |
| AC20 | E2E | row content (zone, chips, +N); 320px with and without text spacing | P1 |
| AC21 | ISO | the four rows, both surfaces, I-17 log slice on each | P0 |
| AC22 | INT, ARCH | LV-14; ARCH route-once and contract gates | P1 |

Not scaffolded, deliberately: **handler and `ListTraversal` unit tests** - `IListReader`, `ViewListHandler` and
`ListTraversal` do not exist and a hand fake cannot compile against them; the dev phase writes them beside
`ViewBoardHandlerTests` and `BoardTraversalTests` (the browser `↓` case covers traversal end to end meanwhile).
**`MigrationBundleEquivalenceTests`/schema snapshot** edits belong to the same change as the migration.

## Step 4 - Generated scaffolds

### Seed constraints checked (real schema, including foreign keys)

- **Tasks** (`ListWire.SeedListAsync`): one set-based `INSERT` under the Space's key; every non-nullable column of
  `dbo.[Task]` supplied (`Id` via `NEWID()` - `ValueGeneratedNever`; `SpaceId`, `ProjectId`, `StatusId`, `Title`,
  `PositionKey`, `CreatedAt`); composite FK `(SpaceId, ProjectId)` by a Project the product created in that Space;
  composite FK `(SpaceId, StatusId)` by that Space's own `StatusDefinition` rows; unique
  `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` by `0` + a distinct 5-digit ordinal; `BLOCK AFTER INSERT` by the session key.
- **Due dates** (`SetDueDatesAsync`, `SetDueDateAsync`): `UPDATE` under the Space's key (`BLOCK AFTER UPDATE`), instants at
  offset zero inside 2.3's 2000-2100 window; `DueDate` is nullable (`20261025143020_TaskDueDate`).
- **Labels, correctness cases**: defined and applied through the product (`LabelWire.SeedAsync`, the Task `PATCH` with
  `labelIds`), so `PK_TaskLabel` and both composite FKs are the product's to satisfy and `FirstLabelName` is the product's to keep.
- **Labels, 5,000-row cases** (`ApplyLabelAsync`): `INSERT dbo.[TaskLabel] (SpaceId, TaskId, LabelId, AppliedAt) SELECT …
  FROM dbo.[Task]` - all four columns supplied (`20261025175001_LabelTables`); `PK_TaskLabel (SpaceId, TaskId, LabelId)`
  unique because each statement applies one Label once per Task; `FK_TaskLabel_Task_SpaceId_TaskId` -> `AK_Task_SpaceId_Id`
  by rows read from `dbo.[Task]` in the same statement; `FK_TaskLabel_Label_SpaceId_LabelId` -> `AK_Label_SpaceId_Id` by
  product-defined Labels. Then `BackfillFirstLabelNamesAsync` in the migration's own shape (Task 2 (c)) - it names the
  `FirstLabelName` column Task 2 adds, so every caller is skipped until then.
- **Memory:** every case runs in its class's own database on the suite's one engine (no new engine, no new fixture type, no
  new collection - the Nightly case reuses `BoardScaleCollection`). The largest seed is 5,000 Tasks plus 6,000 Label rows.
- **Isolation rows** seed through the product only (`SeedProjectWithTaskAsync`, `SeedLabelAsync`, 50 more labelled Tasks
  over HTTP for the aggregate row, each title and Label name held as an I-17 secret).

### Files

| File | Entries (cases) | ACs | State |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Lists/ListWire.cs` | harness: `PathOf`, `PageAsync`, `PageOf`, `WalkAsync`, `SeedListAsync`, `SetDueDatesAsync`, `SetDueDateAsync`, `SetLabelsAsync`, `ApplyLabelAsync`, `BackfillFirstLabelNamesAsync`, `ExpectedOrderAsync`, `FirstLabelNameAsync`, `FirstAppliedLabelNameAsync`, `LogicalReadsOfAsync`, `PlansNamingAsync`, `ConvertsImplicitly`; scaffold constants | - | active |
| `tests/Yello.Tests.Slices/Lists/ListPagingTests.cs` | 19 methods (47 cases) - LV-01..LV-19 | AC1-AC6, AC10-AC16, AC22 | all skipped `red: AC<n>` |
| `tests/Yello.Tests.Slices/Lists/ListScaleTimingTests.cs` | 1 - timed AC10 (`BoardScaleCollection`, `DisableParallelization`) | AC10 | skipped `red: AC10`, `Tier=Nightly`, `Explicit` |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 4 rows x 2 surfaces - `ISO-DIRECT-LIST`, `ISO-LIST-FOREIGN-ID-IN-OWN-SPACE`, `ISO-LIST-AGGREGATE`, `ISO-LIST-FOREIGN-FILTER-IN-OWN-SPACE`; helpers `ListOf`, `ApplyLabelThroughTheProductAsync`, `SeedLabelledTasksThroughTheProductAsync`, `ListPagesHoldExactlyTheCallersTasksAsync` | AC3, AC15, AC21 | **pending on 2.10** (GATE-003: all live before `review`) |
| `tests/Yello.Tests.Architecture/ListViewGateTests.cs` | 8 - P-4 gate, its detector plant, three sorts/no Assignee, route once, contract, page size, problem types, virtualisation scan | AC2, AC6, AC12, AC18, AC22 | **1 green today** (the detector plant); 7 skipped |
| `tests/Yello.Tests.Client/ListViewTests.cs` | 14 methods (18 cases) - toggle, row content, count announced, empty filter, Show more, `↓` by identity, X-6, Viewer, purge, no name in URL/title, 404, 320px x2, locales x4, sort change | AC1, AC7-AC9, AC11, AC12, AC16-AC20 | all skipped |
| `tests/Yello.Tests.Client/FakeYelloApi.cs` | harness: the List route (`ListPageAsync`, `FakeListQuery`, SQL-order tiebreaker via `SqlGuid`, opaque cursor bound to its sort), `ListPageSize`, `ListPathOf`, `ListRequests`, `HoldListPageAsync`/`ReleaseListPage`, two scaffold problem types | - | active |

**Browser markup and copy the scaffolds assume** (correct to what is built; record each correction): see the remarks of
`ListViewTests` - the client route `/spaces/{s}/projects/{p}/list`; a `nav` with links "Board"/"List" and `aria-current`;
`main table tbody tr[data-task-id]` rows with one `tabindex="0"`; checkboxes named by Status/Label; a `select` whose name
starts "Sort" with "Status" preselected and an option "Due date"; "Showing {n} of {N}"; "Show more Tasks"; "List loaded.",
"{n} Tasks match.", "No Tasks match.", "Not available." on a 404.

### Green today - standing regression gates, left active

| Case | Why it is green now | Planted |
|---|---|---|
| `ListViewGateTests.The_sort_index_detector_reports_a_planted_sort_with_no_index_and_finds_an_existing_one` | a pure detector over today's EF model | it is the plant: a sort mapped to `Title` (no index) must be reported, the Board index must be found by the same column-list match, and the same columns reordered must not match |

Nothing else is green by design: every other scaffold needs the route, the column or the page.

### Gates that go red by design when the dev phase builds the story (amend by exactly this, never loosen)

1. `IsolationCaseListTests.Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` - the
   pending rows already name `ListRoute`; if the mapped pattern differs from the scaffold string, swap it for the contract
   member. Convert all four rows to live before `review` (GATE-003).
2. `MigrationBundleEquivalenceTests` and the schema snapshot - the `ListViewSortKeys` migration (Task 2 (c)).
3. `RefusalBoundaryGateTests` vacuity guards and any ARCH-503 route list - widened by exactly the List route (Task 4 (e)).
4. Every hand fake of a port the slice changes (none expected: `IListReader` is new, `IBoardReader` must not widen).

## Step 5 - Activation order (riskiest assumption first)

1. **LV-02 `Sorted_by_due_date_a_page_boundary_inside_120_tied_dates_skips_and_repeats_nothing`, with LV-19's due-date row.**
   It tests the riskiest assumption: that EF Core can express the pair seek `(DueDate, Id)` with the `uniqueidentifier`
   tiebreaker compared **in SQL** (`task.Id.CompareTo(id) > 0` -> `Id > @id`, story Task 3 (d)) as a bounded index seek, and
   that the two-segment NULL-last cursor composes. If EF will not translate it, `FromSql` on `dbo.Tasks` is the fallback,
   and every other sort inherits the answer. Read the generated SQL first and record it in the Debug Log.
2. ARCH P-4 (`Every_offered_sort_has_a_composite_index_…`) with Task 2's schema; then LV-16/LV-17 (the `FirstLabelName`
   maintenance and its race).
3. LV-03, LV-01, LV-04, LV-05 (P-3's remaining sorts), then LV-18 (logical reads x6; plant an `OFFSET` and a dropped index,
   each restored in a `finally`) and LV-19's other two rows.
4. LV-06, LV-07, LV-08, LV-09..LV-15; the ARCH contract, route, page-size, problem-type and Assignee gates.
5. Isolation rows one at a time (swap `ListRoute` for the contract member), each proved against its plant (Task 7).
6. Browser: the `↓` identity case first, then Show more, X-6, the filter count and empty state, the sort change, the toggle,
   Viewer, purge, URL/title, 404, 320px, locales; the ARCH virtualisation-scan gate once `TaskListView.razor` exists.
7. `ListScaleTimingTests` once locally with `--filter-trait "Tier=Nightly" --explicit on`; record the p95 figures.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Validation (checklist.md)

- [x] Prerequisites satisfied (story approved, ACs testable, framework configured, Docker up).
- [x] Every AC mapped to at least one case; each case names its AC in its skip reason.
- [x] Red phase: every scaffold whose AC is unimplemented is skipped `red: AC<n> - <why>`; the one green-today case is
      listed above as a standing gate.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] No new test project, package, engine, fixture type or collection: the new Slices classes take a database each on the
      one engine; browser cases share `ClientSurfaceCollection`; the Nightly case reuses `BoardScaleCollection`.
- [x] The timed case is in a `DisableParallelization` collection, carries `Tier=Nightly` and is `Explicit`.
- [x] Isolation rows pending on 2.10 with reasons; GATE-001..004 green.
- [x] The race is deterministic (held transaction, `LCK_M_%`, "blocked or answered"); no `Task.Delay`; no timer touches the database.
- [x] No product code changed (test files, the test harness and this checklist only).
- [x] No CLI browser sessions opened (no recording mode); temp artifacts in the session scratchpad only.

## Run

See the story's Dev Notes ("ATDD artifacts") and the orchestrator report for the full-run totals.

## Completion

- Story handoff: `_bmad-output/implementation-artifacts/2-10-view-a-project-as-a-filterable-sortable-list.md`, Dev Notes
  "ATDD artifacts - red-phase scaffolds already in the tree".
- Key assumptions: the recommended Q1-Q11; index names by EF's convention (`IX_Task_SpaceId_ProjectId_{Column}_Id`); the
  sort codes `status`, `due-date`, `label`; the sort-to-column map type `ListSortColumns`. The riskiest is the SQL-side
  `uniqueidentifier` tiebreaker in an EF seek (activate LV-02 first).
- Next workflow: `bmad-dev-story`.
