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
lastSaved: '2026-11-22'
workflowType: 'testarch-atdd'
storyId: '6.6'
storyKey: '6-6-move-a-whole-status-of-tasks-at-once'
storyFile: '_bmad-output/implementation-artifacts/6-6-move-a-whole-status-of-tasks-at-once.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-6-6-move-a-whole-status-of-tasks-at-once.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; every suite shares the StatusBulkMoveWire stand-ins and one build)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.StatusBulkMove.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.StatusBulkMoveRace.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.StatusBulkMoveScale.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.StatusBulkMove.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.BulkMoveBar.cs'
  - 'tests/Yello.Tests.Architecture/RefusalCopyTests.StatusBulkMove.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.StatusBulkMove.cs'
  - 'tests/Yello.Tests.Client/StatusBulkMoveTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.StatusBulkMove.cs'
  - 'tests/Yello.Tests.Slices/Tasks/StatusBulkMoveSliceWire.cs'
  - 'tests/Yello.Tests.Shared/StatusBulkMoveWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/6-6-move-a-whole-status-of-tasks-at-once.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-13.md'
  - '_bmad-output/specs/spec-yello/SPEC.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-6-5-move-a-task-to-another-project.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 6, Story 6.6: Move a whole Status of Tasks at once

## Story summary

As someone retiring a Project, I want to move everything in one Status to another Project in a single operation, so that
emptying a Project safely takes one action per Status rather than one per Task. Requirements: **FR-41** (bulk form),
**AR-24**, **UX-DR17**, **UX-DR39**, with FR-16, FR-19, FR-26, FR-35, NFR-1, NFR-5 (measured, not asserted), NFR-8,
AD-2/AD-3/AD-20, AD-15, AD-16/AD-17, AD-18, AD-25, S-1 and S-2 (P0), and the isolation rows I-1, I-2, I-4, I-5, I-13, V-1.

## Step 1 - Preflight

- **Stack:** `fullstack`: .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025, and
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority.
- **Story:** `ready-for-dev`, AC1-AC19, Q1-Q15. No question is decided. Every scaffold follows the recommended option (a),
  except Q11 (see the decisions below), and a skip that depends on a question names it.
- **No progress file existed** (`atdd-checklist-6-6-*` was absent), so this run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists. The TEA knowledge fragments
  are Playwright/TypeScript-oriented; the project's conventions file and the 6.5 scaffolds were the working references.
- **Harness reused:**
  - The `TaskEndpointTests` partials: one database, no new engine.
  - `ProjectMoveSliceWire` (`TwoProjectsAsync`, `HoldProjectsAsync`, `HoldProjectMoveAsync`, `SurvivorsAsync`, the S-1
    trigger), `ProjectMoveWire` (the body is the single move's), `TaskWire`, `TaskEditWire`, `MoveWire`, `DeleteWire`,
    `LabelWire`, `StatusRemovalWire` (`HoldRemovalAsync`, `InsertRemoveOperationAsync`, `AssertRefused`,
    `DropTriggerAsync`), `ProjectStatusSliceWire` (S-2), `AssignmentWire`, `MailDrop`, `WireDatabase`.
  - The isolation world: `SeedProjectWithTaskAsync`, `SeedProjectAsync`, `ForeignProjectAsync`, `StatusIdsAsync`,
    `TokenOnAProjectsStatusesElsewhere`.
  - The gates' `RouteTable`, `CarriesSpaceId`, `DependentsOf`, `AssertOnlyThePipelineDependsOn`, `OutermostOf`,
    `TaskRequests`, `SpaceMembersOf`, `CssCorpus`, `SelectsComponent`, `AssertRadius`.
  - `FakeYelloApi` (`ReadProjectMove`, `ProjectMoveProblem`, `ProjectStatuses`, `SeedProjectStatuses`, `TaskEditHold`).

## Step 2 - Generation mode

AI generation from the story and the documentation, as the standing answer says. No recording mode: the route, the
controls and the bar do not exist yet. Every path, locator and copy string for them is the story's recommendation, and
each file's remarks name it.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| Q1 | (a) `POST …/projects/{projectId}/statuses/{statusId}/tasks/project`, body `{ projectId, statusId? }` | `StatusBulkMoveWire.Route`/`PathOf`; every wire case; the route gates; the fake |
| Q2 | (a) the selection is fixed under the locks | races 2 and 3 (a Task created into the Status before the locks is moved; one moved out is not) |
| Q3 | (a) cancel before commit only; focus on the originating column on commit and cancel | browser commit, Esc/Cancel and 404 cases |
| Q4 | (a) existing problem types only: source → boundary `404`, destination → `400 invalid-task-project` | wire cases 6 and 7; isolation rows |
| Q5 | (a) the single move's mapping rule; an empty Status answers `200` with 0 after validating a supplied Status | wire cases 2-5 and 9 |
| Q6 | (a) the slice counts `count + n > limit` after its locks; both bound gates admit exactly the two slices | wire case 11; race 9; the two renamed bound gates |
| Q7 | (a) `TaskStatusMigration` generalised; it is the only raw SQL that sets `ProjectId` | the SQL writer gate; the removal regression case; race 4's played shape |
| Q8 | (a) one head control per non-empty column for a managing Role | browser control case |
| Q9 | (a) List View entry only with exactly one Status and no other filter | two List View browser cases |
| Q10, Q14, Q15 | ledger dispositions | nothing scaffolded; Q14's fake fixes are the dev phase's (see below) |
| Q11 | **(b), by the orchestrator's standing answer** - the 5,000-Task case is `Explicit`, `Tier=Nightly` | `TaskEndpointTests.StatusBulkMoveScale.cs` |
| Q12 | (a) a `BulkMoveBar` hosting `ProjectMovePicker` | browser markup (`.bulk-move-bar .project-move-picker`) |
| Q13 | (a) `{ projectId, statusId, movedCount }` | `AssertBulkMoved`; the response gate; the fake's answer |

### Container budget

No new container-backed class, fixture or engine:
- The Slices cases are partials of `TaskEndpointTests`.
- The Architecture gates are partials of `RefusalBoundaryGateTests`, `DesignFoundationGateTests` and `RefusalCopyTests`.
- The isolation rows use the boundary world and seed only in fresh actors' Spaces. B is read only for a Status id.
- The browser class runs on the assembly's `ClientSurfaceFixture` with `FakeYelloApi`.

### Scenario map

| AC | Level | Scaffold | Priority | Today |
|---|---|---|---|---|
| AC6, Q7 | Wire (race) | `Two_opposite_bulk_moves_queued_on_one_lock_both_land_and_neither_deadlocks` | P0 | red - **riskiest** |
| AC2, Q2 | Wire (race) | `A_Task_created_into_the_Status_before_the_move_took_its_locks_is_moved` | P0 | red |
| AC7, Q6 | Wire (race) | `At_4990_a_bulk_move_of_ten_and_a_creation_into_the_destination_admit_exactly_one` | P0 | red |
| AC2 | Wire (race) | `A_Task_moved_out_of_the_Status_before_the_move_took_its_locks_is_not_moved` | P0 | red |
| AC5, AC6 | Wire (race) | `A_bulk_move_queued_behind_its_destinations_deletion_is_refused_400_invalid_task_project` | P0 | red |
| AC3, AC6 | Wire (race) | `A_bulk_move_queued_behind_its_sources_deletion_answers_the_boundary_404` | P0 | red |
| AC3, AC6 | Wire (race) | `A_bulk_move_queued_behind_the_removal_of_its_source_Status_answers_the_boundary_404` | P0 | red |
| AC4, AC6 | Wire (race) | `A_bulk_move_queued_behind_the_destinations_removal_of_its_Status_needs_a_destination_Status` | P0 | red |
| AC6 | Wire (race) | `A_bulk_move_and_a_Labels_edit_on_one_of_its_rows_both_land` | P0 | red |
| AC6 | Wire (race) | `A_writer_queued_behind_a_bulk_move_answers_404_and_writes_nothing` (deletion, Status edit, single move) | P0 | **green, active** |
| AC1, AC4, AC8 | Wire | `A_Status_the_destination_holds_is_kept_and_its_Tasks_land_last_in_source_order` (Session, Token) | P0 | red |
| AC4 | Wire | `A_Status_the_destination_lacks_needs_a_destination_Status_and_nothing_is_written` (4 rows) | P0 | red |
| AC4, AC8 | Wire | `A_destination_Status_the_destination_holds_is_applied_to_every_Task_at_the_end_of_its_column` (2 rows) | P0 | red |
| AC4, Q5 | Wire | `A_supplied_Status_is_applied_even_when_the_source_Status_would_be_kept` | P1 | red |
| AC4 | Wire | `A_destination_Status_outside_the_destinations_set_is_refused_400_as_a_random_id_and_the_single_move_are` (3 rows) | P0 | red |
| AC5, Q4 | Wire | `A_destination_that_is_not_another_Project_of_the_Space_is_refused_400_as_a_random_id_and_the_single_move_are` (2 rows) | P0 | red |
| AC3, Q4 | Wire | `A_source_that_is_not_here_answers_the_boundary_404_as_a_random_Project_does` (5 rows) | P0 | red |
| AC1 | Wire | `A_bulk_move_body_that_is_not_a_move_to_a_Project_is_refused_400_and_nothing_is_written` (10 rows) | P1 | red |
| AC2, Q5 | Wire | `An_empty_Status_answers_200_with_nothing_moved` (2 rows), `An_empty_Status_still_refuses_a_destination_Status_outside_the_destinations_set` | P1 | red |
| AC12 | Wire | `What_each_Task_carries_survives_the_bulk_move_and_no_email_is_sent` | P0 | red |
| AC7, Q6 | Wire | `The_destinations_bound_counts_every_Task_the_move_adds` (4,999 refused; 4,998 lands on 5,000) | P0 | red |
| AC15 | Wire | `A_Viewer_is_refused_403_on_both_surfaces_a_Member_moves_over_a_Token_and_an_Admin_over_a_Session` | P0 | red |
| AC3, AC16 | Wire | `Another_Spaces_path_answers_the_boundary_404_recorded_and_nothing_moves` | P0 | red |
| AC1, AD-18 | Wire | `A_replayed_key_answers_the_original_bulk_move_and_moves_nothing_again` | P1 | red |
| AC17 (S-1) | Wire | `A_failure_forced_on_the_last_Task_written_leaves_every_Task_where_it_was` | P0 | red |
| AC19 | Wire | `A_Project_emptied_Status_by_Status_is_deleted_with_no_Task_in_it` | P1 | red |
| Q7 trap | Wire | `A_Status_removal_moves_its_Tasks_within_their_own_Project_only` | P0 | **green, active** |
| AC8, Q11 | Wire (Nightly) | `A_Status_of_5000_Tasks_moves_in_one_request_in_source_order` | P2 | red, `Explicit` |
| AC1, AC13 | Arch | `The_bulk_move_route_requires_ManageTasks_serves_both_surfaces_and_counts_no_bound_in_the_pipeline` | P0 | red |
| AC1 | Arch | `ProjectStatusRoutes_names_the_bulk_move_template_once` | P1 | red |
| AC1, AC9, AC13 | Arch | `The_bulk_move_request_names_a_Project_and_an_optional_Status_and_no_Space_or_Task_list` | P0 | red |
| AC1, Q13 | Arch | `The_bulk_move_answer_states_the_destination_its_Status_and_the_count_moved` | P1 | red |
| AC6 | Arch | `The_bulk_move_is_one_slice` | P1 | red |
| AC13, Q6 | Arch | `Nothing_but_the_pipeline_and_the_two_cross_Project_slices_read_the_bound_registry` | P1 | red |
| AC13, Q6 | Arch | `Only_the_pipeline_and_the_two_cross_Project_slices_depend_on_the_bound_count_port` | P1 | red |
| AC13 | Arch | `Only_the_creation_and_the_single_move_set_a_Tasks_Project` + oracle | P0 | **green, active** |
| AC13, Q7 | Arch | `Only_the_Status_migration_sets_a_Tasks_Project_in_raw_SQL` | P0 | red |
| AC13 | Arch | `The_raw_SQL_scan_reports_a_planted_statement_that_sets_the_Project` (oracle) | P0 | **green, active** |
| AC10, AC13 | Arch | `DesignFoundationGateTests.No_component_but_the_bulk_move_bar_is_bordered_in_the_accent` | P1 | **green, active** |
| AC10 | Arch | `DesignFoundationGateTests.The_bulk_move_bar_takes_the_accent_emphasis_border_on_the_card_surface` | P2 | red |
| AC10, AC11 | Arch | `RefusalCopyTests.The_bulk_move_copy_is_in_the_client_copy` (10 rows) | P2 | red |
| AC15, AC16 | Isolation | 7 rows (`ISO-STATUS-BULK-MOVE-FOREIGN-SPACE`, `-FOREIGN-PROJECT-IN-OWN-SPACE`, `-FOREIGN-STATUS-IN-OWN-PATH`, `-FOREIGN-PROJECT-IN-BODY`, `-FOREIGN-STATUS-IN-BODY`, `ISO-RECORD-KIND-STATUS-BULK-MOVE-VIEWER`, `ISO-TOKEN-OWN-OTHER-SPACE-STATUS-BULK-MOVE`), each on both surfaces | P0 | `PendingUntil: "6.6"` |
| AC9-AC11, AC15 | Browser | `StatusBulkMoveTests`: 10 methods, 14 cases | P0 | red |

**Not scaffolded:**
- `MoveStatusTasksToProjectHandlerTests`. They need the handler and port types to compile, so the develop phase writes
  them with the slice (AC13's recording fake: `TasksPerProject` counted, after `LockProjectsAsync`, for the destination
  only). The slice gate and the two bound gates pin the type they will reach.
- The 6.5 handler's recording assertions (AC13): `MoveTaskToProjectHandlerTests.Both_Projects_are_locked_in_one_call_and_every_decision_is_read_after_it`
  already pins the call order (`lock` before `count {destination}`), and its `FixedCount` fake asserts
  `Bound.TasksPerProject`. It is a standing gate, green today; nothing to add.
- The fake API fixes of AC14 / ledger `:1104` (`FakeYelloApi.ProjectMove.cs` reading the body first, `FakeTask.ProjectId`).
  They change the single move's fake under green browser cases, so the develop phase makes them with the client work.
- Purging the bar's state on Space switch, sign-out and Account switch (AC10, last line). The develop phase adds it beside
  `SpaceSwitchCarryOverTests` once the bar's state exists.
- The plant table. Plants are the develop phase's work.

### Red phase

Every scaffold whose AC is not implemented is committed skipped, with `Skip = "red: AC<n> - <why>"`. The build and the
suite stay green. Seven new cases pass today and stay active as standing regression gates: race 4 (3 rows), the Status
removal regression, the setter gate and its oracle, the SQL scan's oracle, and the accent-border gate.

## Step 4 - Generated scaffolds

Execution mode: sequential (the Agent tool is available, but all workers share stand-ins and one build).

| File | Cases | Skipped | Active |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.StatusBulkMove.cs` | 41 (18 methods) | 40 (17 methods) | 1 |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.StatusBulkMoveRace.cs` | 12 (10 methods) | 9 | 3 |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.StatusBulkMoveScale.cs` | 1 (Explicit, Nightly) | 1 | 0 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.StatusBulkMove.cs` | 11 | 8 | 3 |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.BulkMoveBar.cs` | 2 | 1 | 1 |
| `tests/Yello.Tests.Architecture/RefusalCopyTests.StatusBulkMove.cs` | 10 (1 method) | 10 | 0 |
| `tests/Yello.Tests.Isolation/IsolationCases.StatusBulkMove.cs` | 7 rows x 2 surfaces | pending | - |
| `tests/Yello.Tests.Client/StatusBulkMoveTests.cs` | 14 (10 methods) | 14 | 0 |

**Supporting files:**
- `Yello.Tests.Shared/StatusBulkMoveWire.cs` holds the stand-ins `Route` and `PathOf`, and `MovedCountIn` and
  `StatusIdIn` for the answer. When the contract declares `ProjectStatusRoutes.TasksToProject` and `TasksToProjectOf`,
  swap every use for them and delete the two stand-ins.
- `Yello.Tests.Slices/Tasks/StatusBulkMoveSliceWire.cs`: the request on either surface, `AssertBulkMoved`,
  `TasksInStatusAsync`, and three held writes in the product's lock shape - `HoldStatusBulkMoveAsync` (both locks in
  ordinal order, then the selected rows rewritten), `HoldOccupiedRemovalAsync` (the Project's lock, the occupants moved,
  then the delta row) and `HoldCreationAsync` (the Project's lock, then one row).
- `Yello.Tests.Client/FakeYelloApi.StatusBulkMove.cs` decides the route in AC6's order, with the bound, and with hold,
  release, refused and unanswered. It moves Board columns, which the List route serves from. One hook was added in
  `FakeYelloApi.SpaceDeletion.cs`, after the single move's.
- One spread line was added in `IsolationCases.All`.

### Seed and schema constraints checked

- Every played write (`StatusBulkMoveSliceWire`) keeps `SpaceId`, because the `BLOCK AFTER UPDATE` predicate refuses a
  change, and runs under the Space's own session key.
  - A played destination is a Project the product created in the same Space, so the composite
    `FK_Task_Project_SpaceId_ProjectId (SpaceId, ProjectId)` holds.
  - Every Status written is a `StatusDefinition` of the same Space, read through the product, so
    `(SpaceId, StatusId) → StatusDefinition` holds.
  - Every key is the target column's `MAX(PositionKey)`, read under the target Project's lock, followed by a four-digit
    ordinal (`V` and the ordinal in an empty column). The keys are strictly increasing, inside the `PositionKey` alphabet
    (`0-9A-Za-z`) and within `varchar(128)`, so the unique `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` holds.
  - `HoldOccupiedRemovalAsync` moves the occupants before it writes the `StatusDeltaOperation` row
    (`InsertRemoveOperationAsync`, whose `Sequence` is `MAX + 1` per Project), so no Task is outside its set at commit.
- The bound and scale seeds reuse `TaskWire.SeedTasksAsync`. Its constraints are stated on that class. The bound race seeds
  the source (10) and the destination (4,990) with the same key prefix, which is safe because the unique index includes
  `ProjectId`.
- Everything else is written through the product: Projects, Tasks, Labels, the Assignee (a Membership of the same Space,
  `(SpaceId, AssigneeMembershipId) → Membership`), added Statuses and removals.
- The S-1 trigger (`PlantFailingTaskUpdateTriggerAsync`) fires per statement, and `inserted` holds every row a set-based
  `UPDATE` touched, so naming the last Task in source order fails the whole statement. It is dropped in a `finally`.
- The isolation rows seed only in fresh actors' Spaces. B is read for a Status id and never written, so its 50-Project bound
  is untouched.

### Findings recorded while writing the gates

- **Two hairline accent borders already exist**: `.button-primary` (`components.css:166`) and `.offer-indicator`
  (`components.css:324`), each `var(--border-hairline-width) solid var(--accent)`. The story's "no accent border exists
  anywhere" and AC13's "no selector except `.bulk-move-bar` declares an accent border" are therefore false today. The
  active accent-border gate names those two as hairline exceptions, held to the hairline width, and admits no other
  selector. The develop phase either keeps that reading (and words AC13 to match in the Debug Log) or asks Lee to drop the
  two hairline accent edges. Recorded here and not decided further.
- **The `TaskItem.ProjectId` setter writers today** are exactly `TaskStore.CreateAsync` and `TaskStore.MoveToProjectAsync`.
  The scan works at the member level through the compiler's async and lambda names, and it also counts an
  `ExecuteUpdate`-shaped `SetProperty` over the getter, so plant 9 is caught.

## Step 5 - Validation

- Prerequisites: the story has clear ACs, the framework is configured, and the environment is available (Docker 29.9.2).
- **Build:** `dotnet build Yello.slnx` gives 0 warnings and 0 errors.
- **Runs:** see the results section below.
- No CLI browser sessions were opened. The only temp artifacts are the logs in `artifacts/test-runs/` (git ignores them).

### Activation order (riskiest assumption first)

1. **The lock order:** `Two_opposite_bulk_moves_queued_on_one_lock_both_land_and_neither_deadlocks`. If two opposite
   bulk moves can deadlock, or the set-based statement takes the Project locks in a different order from the single move,
   the design fails.
2. **The selection under the locks:** `A_Task_created_into_the_Status_before_the_move_took_its_locks_is_moved`, then
   `A_Task_moved_out_of_the_Status_before_the_move_took_its_locks_is_not_moved`.
3. **The bound for n:** `At_4990_a_bulk_move_of_ten_and_a_creation_into_the_destination_admit_exactly_one`, then
   `The_destinations_bound_counts_every_Task_the_move_adds`.
4. The re-reads under the locks: the destination's deletion, the source's deletion, the source Status's removal, the
   destination's removal, and the Labels-edit row race.
5. The migration change: keep `A_Status_removal_moves_its_Tasks_within_their_own_Project_only` green, and run the
   `ProjectEndpointTests` removal partials and the Space-level removal cases after generalising `TaskStatusMigration`.
6. The contract and structure gates: route, `ProjectStatusRoutes`, request, response, slice, the SQL writer gate, and the
   two bound gates (delete the two old bound gates in the same change).
7. The core wire cases: kept, required, applied, supplied-unneeded, refusals, source `404`s, bodies, empty Status.
8. Survivors, S-1, UJ-9, role, boundary, replay. Then the 7 isolation rows go live before review (GATE-003).
9. The browser cases, the copy gate and the bar's CSS gate. The scale case runs `--explicit on` once, and its duration goes
   to ledger `:1078`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

### Existing gates the implementation will turn red (amend them deliberately, in the same change)

- **The bound gates** (`RefusalBoundaryGateTests.Projects.cs`): `Nothing_but_the_pipeline_reads_the_bound_registry` and
  `Only_the_pipeline_depends_on_the_bound_count_port` admit only `MoveTaskToProjectHandler`. They go red when the bulk slice
  reads `BoundRegistry` or `IBoundCounter`. Delete them when activating their replacements in
  `RefusalBoundaryGateTests.StatusBulkMove.cs`, and rename the `MovesATaskToAnotherProject` summary ("the one slice").
- **The Task request sets** (widen by exactly `MoveStatusTasksToProjectRequest`):
  - `RefusalBoundaryGateTests.The_Task_creation_request_carries_a_title_and_nothing_else` (`.Tasks.cs`);
  - `RefusalBoundaryGateTests.The_Task_edit_request_carries_its_editable_attributes_and_nothing_else` (`.TaskEdit.cs`);
  - `RefusalBoundaryGateTests.The_move_to_Project_request_names_a_Project_and_an_optional_Status_and_is_the_only_Task_request_naming_a_Project`
    (`.ProjectMove.cs`), whose name and summary say "the only"; reword it, do not delete it.
- **The routes under a Project** (ARCH-503): `RefusalBoundaryGateTests.The_routes_under_a_Project_are_its_deletion_its_rename_its_Task_creation_and_its_Board`
  (`.Delete.cs`) pins the list exactly. Add `POST {ProjectPattern}/statuses/{statusId:guid}/tasks/project` and a sentence to
  its summary.
- **Isolation route coverage:**
  - `IsolationCaseListTests.Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` and
    `Every_Space_scoped_route_a_Token_reaches_has_a_row_with_a_Token_of_the_same_Account_bound_elsewhere`, until the 7 rows
    are live;
  - GATE-003 (`Every_pending_row_names_a_story_that_has_not_yet_reached_review`) once 6.6 reaches review with any row
    still pending.
- **The `ProjectId` setter gate** (new, active): it stays green if the bulk move writes `ProjectId` only in
  `TaskStatusMigration`'s raw SQL. A third setter (for example a typed `ExecuteUpdate`) turns it red by design.
- **Must stay green unchanged:**
  - the role-matrix theory (`ActiveSpaceEndpointTests.RoleMatrix.cs`) and its source gate (`RefusalBoundaryGateTests.RoleDrop.cs`):
    `ManageTasks` already has a representative, so no row is needed;
  - the `ProblemResponse` parameter gates: no problem-body member is added, and `bound-reached` reuses `Bound` and `Limit`;
  - `ProblemTypes` uniqueness (`BoardPagingGateTests.cs`, `ListViewGateTests.cs`): no new problem type (Q4);
  - `Every_spaceId_endpoint_declares_exactly_one_capability`;
  - `The_Tasks_per_Project_bound_is_declared_only_where_the_route_names_the_Project`: the new route declares no bound;
  - `The_Project_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound` and
    `A_Status_leaves_a_Project_only_through_the_remove_route_whose_request_carries_its_destination`: the new route matches
    neither filter, and its request lives in `Yello.Contracts.Tasks`;
  - `No_Project_request_contract_carries_anything_but_a_name`: same reason;
  - the schema gate (`ProjectEndpointTests.StatusSchema.cs`): no migration;
  - `MoveTaskToProjectHandlerTests` and `TaskProjectMoveTests`: the single form's behaviour must not change.

## Results of this run

- **Build:** `dotnet build Yello.slnx`: 0 warnings, 0 errors.
- **Architecture, whole suite:** 569 tests, 559 passed, 10 skipped (the 10 red gates), 0 failed. The 4 new active gates pass.
- **Slices,** with `--filter-method` on the 29 new methods: 31 discovered, 4 passed, 27 skipped, 0 failed.
  - The 4 passes are race 4 (3 rows) and the Status removal regression.
  - xunit reports a skipped theory once per method, not once per data row.
- **Client,** `--filter-class Yello.Tests.Client.StatusBulkMoveTests`: 10 discovered, 10 skipped, 0 failed.
- **Isolation,** `--filter-class Yello.Tests.Isolation.IsolationCaseListTests`: 16 passed, 0 failed. The 7 pending rows
  satisfy GATE-003 while 6.6 is ready-for-dev.
- The logs are in `artifacts/test-runs/6-6-atdd-*.log`.

**Next workflow:** `bmad-dev-story`.
