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
storyId: '6.5'
storyKey: '6-5-move-a-task-to-another-project'
storyFile: '_bmad-output/implementation-artifacts/6-5-move-a-task-to-another-project.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-6-5-move-a-task-to-another-project.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; every suite shares the ProjectMoveWire stand-ins and one build)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.ProjectMove.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.ProjectMoveRace.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.ProjectMove.cs'
  - 'tests/Yello.Tests.Architecture/RefusalCopyTests.ProjectMove.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.ProjectMove.cs'
  - 'tests/Yello.Tests.Client/TaskProjectMoveTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.ProjectMove.cs'
  - 'tests/Yello.Tests.Slices/Tasks/ProjectMoveSliceWire.cs'
  - 'tests/Yello.Tests.Shared/ProjectMoveWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/6-5-move-a-task-to-another-project.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-6-4-remove-a-space-default-status-across-every-project.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 6, Story 6.5: Move a Task to another Project

## Story summary

As someone reorganising work, I want to move a Task into a different Project in the same Space, so that work can be
re-filed without being retyped. Requirements: **FR-41** (single-Task form), **AR-24**, with FR-19, FR-20, FR-26, FR-35,
NFR-8, AD-15/AD-16/AD-17/AD-18, AD-2/AD-3/AD-20 (I-1, I-2, I-4, I-5, I-13, V-1), S-1 and S-2 (P0), FR-16 and UX-DR11/12/21/27.

## Step 1 - Preflight

- **Stack:** `fullstack`: .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025, and
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority.
- **Story:** `ready-for-dev`, AC1-AC16, Q1-Q12. No question is decided. Every scaffold follows the recommended option (a),
  and a skip that depends on a question names it.
- **No progress file existed** (`atdd-checklist-6-5-*` was absent), so this run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.
- **Harness reused:**
  - The `TaskEndpointTests` partials: one database, no new engine.
  - `TaskWire`, `TaskEditWire`, `MoveWire`, `DeleteWire`, `LabelWire`.
  - `ProjectStatusSliceWire` (S-2 through `WireDatabase.TasksOutsideTheirProjectsSetSql`), `StatusRemovalWire` (the held
    removal, `AssertRefused`, `DropTriggerAsync`), `AssignmentWire`, `MailDrop`, `WireDatabase`.
  - The isolation world: `SeedProjectWithTaskAsync`, `SeedProjectAsync`, `ForeignProjectAsync`, `StatusIdsAsync`,
    `TokenOwnOtherSpace`.
  - The gates' `RouteTable`, `CarriesSpaceId`, `TaskItemPattern`, `TaskContractsNamespace`.
  - `FakeYelloApi` (`FindTaskIn`, `ProjectStatuses`, `SeedProjectStatuses`, `TaskEditHold`).

## Step 2 - Generation mode

AI generation from the story and the documentation, as the standing answer says. No recording mode: the route, the
control and the picker do not exist yet. Every path, locator and copy string for them is the story's recommendation, and
each file's remarks name it.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| Q1 | (a) `POST …/tasks/{taskId}/project`, body `{ projectId, statusId? }`, answering `200` with the Task | `ProjectMoveWire` stand-ins; every wire case; the route gates; the fake |
| Q2 | (a) Session and Token | the Token theory row of the first wire case; the Viewer and Member Token cases; the route gate (not Session-only); the isolation rows on both surfaces |
| Q3 | (a) `400 invalid-task-project`, byte-identical for random, foreign, own and deleted-meanwhile destinations | `A_destination_that_is_not_another_Project_…`; race 4; `ISO-PROJECT-MOVE-FOREIGN-PROJECT-IN-BODY`; the problem-type gate |
| Q4 | (a) both `ProjectTasks` locks through the sorted helper, then the row | race 5 (opposite moves); the sorted-lock-port gate; `ProjectMoveSliceWire.HoldProjectMoveAsync` plays this order |
| Q5 | (a) the slice counts after the locks; the refusal is the creation's, byte for byte; no pipeline bound on the route | the full-Project case; race 6; the route gate (`RequiredBound` is null) |
| Q6 | (a) a supplied Status is validated and applied | `A_supplied_Status_is_applied_even_when_…` |
| Q7 | (a) the client reads the destination's set; no choices in the `409` | browser Status-step and refusal cases; the fake serves the set read |
| Q8 | (a) the deletion re-checks `ProjectId` under its row lock and answers `404` | race 3 (red); race 2 (green, the edit and the 2.6 move) |
| Q9 | (a) end of column; not optimistic; focus on the column; panel closes | first wire case; browser commit and in-flight cases |
| Q10 | (a) Task detail only | the browser cases use the panel only; nothing scaffolded on the card menu |
| Q11 | (a) `TaskResponse.ProjectId` on every Task answer | `Every_Task_answer_states_the_Tasks_Project`; `TaskResponse_states_the_Tasks_Project` |
| Q12 | (a) correct the remark | ledger only; nothing scaffolded |

### Container budget

No new container-backed class, fixture or engine:
- The Slices cases are partials of `TaskEndpointTests`.
- The Architecture gates are partials of `RefusalBoundaryGateTests` and `RefusalCopyTests`.
- The isolation rows use the boundary world and seed only in fresh actors' Spaces. A and B are read only for a Status id.
- The browser class runs on the assembly's `ClientSurfaceFixture` with `FakeYelloApi`.

A full run starts no more engines than it does today.

### Scenario map

| AC | Level | Scaffold | Priority | Today |
|---|---|---|---|---|
| AC6, Q4 | Wire (race) | `Two_opposite_moves_queued_on_one_lock_both_land_and_neither_deadlocks` | P0 | red - **riskiest** |
| AC7, Q5 | Wire (race) | `At_4999_a_move_and_a_second_write_into_the_destination_admit_exactly_one` (move, creation) | P0 | red |
| AC6 | Wire (race) | `A_move_queued_behind_a_Status_change_decides_on_the_Status_that_change_committed` | P0 | red |
| AC2, AC6 | Wire (race) | `A_move_queued_behind_its_destinations_deletion_is_refused_400_invalid_task_project` | P0 | red |
| AC4, AC6 | Wire (race) | `A_move_queued_behind_the_destinations_removal_of_its_Status_needs_a_destination_Status` | P0 | red |
| AC9, Q8 | Wire (race) | `A_deletion_queued_behind_a_cross_Project_move_answers_404_and_the_Task_survives` | P0 | red (needs no route) |
| AC9 | Wire (race) | `A_Status_edit_or_a_move_queued_behind_a_cross_Project_move_answers_404_and_writes_nothing` (edit, move) | P0 | **green, active** |
| AC7, AC9 | Wire (race) | `A_creation_queued_behind_a_cross_Project_move_counts_the_moved_Task_and_lands_after_it` (full, not full) | P0 | **green, active** |
| AC3, AC8, AC15 | Wire | `A_Task_whose_Status_the_destination_holds_keeps_it_and_lands_last_in_its_column` (Session, Token) | P0 | red |
| AC4 | Wire | `A_Task_whose_Status_the_destination_lacks_needs_a_destination_Status_…` (4 rows) | P0 | red |
| AC4, AC6 | Wire | `A_destination_Status_the_destination_holds_is_applied_at_the_end_of_its_column` (2 rows) | P0 | red |
| AC4, Q6 | Wire | `A_supplied_Status_is_applied_even_when_the_current_one_would_be_kept` | P1 | red |
| AC4 | Wire | `A_destination_Status_outside_the_destinations_set_is_refused_400_…` (3 rows) | P0 | red |
| AC2, Q3 | Wire | `A_destination_that_is_not_another_Project_of_the_Space_is_refused_400_…` (foreign, own) | P0 | red |
| AC1 | Wire | `A_body_that_is_not_a_move_to_a_Project_is_refused_400_…` (9 rows) | P1 | red |
| AC5 | Wire | `What_a_Task_carries_survives_the_move_and_no_email_is_sent` | P0 | red |
| AC7 | Wire | `A_move_into_a_full_Project_is_refused_409_bound_reached_as_a_creation_is` | P0 | red |
| AC12 | Wire | `A_Viewer_is_refused_403_on_both_surfaces_and_a_Member_moves_over_a_Token` | P0 | red |
| AC13 | Wire | `Another_Spaces_path_or_Task_answers_the_boundary_404_and_nothing_moves` | P0 | red |
| AC1, AD-18 | Wire | `A_replayed_key_answers_the_original_move_and_moves_nothing_again` | P1 | red |
| AC10 (S-1) | Wire | `A_failure_forced_at_the_row_write_leaves_the_Task_where_it_was` | P0 | red |
| AC11, AC16 | Wire | `After_a_move_the_Task_reads_the_same_id_and_Space_with_its_new_Project_on_its_new_Board` | P0 | red |
| AC11 | Wire | `Every_Task_answer_states_the_Tasks_Project` | P1 | red |
| AC1, AC12, Q5 | Arch | `The_move_to_another_Project_route_requires_ManageTasks_serves_both_surfaces_and_counts_no_bound_in_the_pipeline` | P0 | red |
| AC1 | Arch | `TaskRoutes_names_the_move_to_Project_template_once` | P1 | red |
| AC1, AC13 | Arch | `The_move_to_Project_request_names_a_Project_and_an_optional_Status_and_is_the_only_Task_request_naming_a_Project` | P0 | red |
| AC13 | Arch | `No_Task_request_contract_carries_a_Space_member` + its planted-type oracle | P0 | **green, active** |
| AC2 | Arch | `The_bad_destination_refusal_is_a_stable_problem_type` | P1 | red |
| AC11 | Arch | `TaskResponse_states_the_Tasks_Project` | P1 | red |
| AC6 | Arch | `A_move_to_another_Project_is_one_sorted_lock_port_and_one_slice` | P1 | red |
| AC14 | Arch | `RefusalCopyTests.A_bad_destination_Project_refusal_renders_its_own_copy` | P2 | red |
| AC12, AC13 | Isolation | 6 rows (`ISO-PROJECT-MOVE-FOREIGN-SPACE`, `-FOREIGN-TASK-IN-OWN-SPACE`, `-FOREIGN-PROJECT-IN-BODY`, `-FOREIGN-STATUS-IN-BODY`, `ISO-RECORD-KIND-TASK-PROJECT-MOVE-VIEWER`, `ISO-TOKEN-OWN-OTHER-SPACE-TASK-PROJECT-MOVE`), each on both surfaces | P0 | `PendingUntil: "6.5"` |
| AC12, AC14 | Browser | `TaskProjectMoveTests`: 10 methods, 12 cases | P0 | red |

**Not scaffolded:**
- The handler unit tests (`MoveTaskToProjectHandlerTests`). They need the handler and port types to compile, so the
  develop phase writes them with the slice. The sorted-lock-port and slice gates pin the types they will reach.
- The plant table. Plants are the develop phase's work.

### Red phase

Every scaffold whose AC is not implemented is committed skipped, with `Skip = "red: AC<n> - <why>"`. The build and the
suite stay green. Five scaffolds pass today and stay active as standing regression gates: race 2 (2 rows), race 8 (2 rows),
and the Space-member gate with its oracle.

## Step 4 - Generated scaffolds

Execution mode: sequential (the Agent tool is available, but all workers share stand-ins and one build).

| File | Cases | Skipped | Active |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.ProjectMove.cs` | 31 (15 methods) | 31 | 0 |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.ProjectMoveRace.cs` | 11 (8 methods) | 7 | 4 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.ProjectMove.cs` | 8 | 6 | 2 |
| `tests/Yello.Tests.Architecture/RefusalCopyTests.ProjectMove.cs` | 1 | 1 | 0 |
| `tests/Yello.Tests.Isolation/IsolationCases.ProjectMove.cs` | 6 rows x 2 surfaces | pending | - |
| `tests/Yello.Tests.Client/TaskProjectMoveTests.cs` | 12 (10 methods) | 12 | 0 |

**Supporting files:**
- `Yello.Tests.Shared/ProjectMoveWire.cs` holds the stand-ins `Route`, `PathOf` and `InvalidTaskProject`, the bodies, and
  `ProjectIdIn`. When the contract declares `TaskRoutes.ProjectMove`, `TaskRoutes.ProjectMoveOf` and
  `ProblemTypes.InvalidTaskProject`, swap every use for them and delete the stand-ins.
- `Yello.Tests.Slices/Tasks/ProjectMoveSliceWire.cs`:
  - the request on either surface and `TwoProjectsAsync`;
  - `HoldProjectMoveAsync`, the move played in SQL in the product's shape (sorted locks, then the row, then one `UPDATE`);
  - `SurvivorsAsync`;
  - the S-1 `AFTER UPDATE` trigger on `Task`.
- `Yello.Tests.Client/FakeYelloApi.ProjectMove.cs` decides the route in AC6's order, with the bound, and with
  hold, release, refused and unanswered. One hook was added in `FakeYelloApi.SpaceDeletion.cs`.
- One spread line was added in `IsolationCases.All`.

### Seed and schema constraints checked

- The played move (`HoldProjectMoveAsync`) keeps `SpaceId`, because the `BLOCK AFTER UPDATE` predicate refuses a change.
  It runs under the Space's own session key.
  - Its destination is a Project the product created in the same Space, so the composite
    `FK_Task_Project_SpaceId_ProjectId (SpaceId, ProjectId)` holds.
  - Its Status is a Space default the destination's set holds, so `(SpaceId, StatusId) → StatusDefinition` holds.
  - Its key is the destination column's `MAX(PositionKey)` plus one alphabet character, or `V0000` in an empty column.
    It is read under the destination's lock, so the unique `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` holds, within
    `varchar(128)`.
- The bound seed reuses `TaskWire.SeedTasksAsync`. Its constraints are stated on that class.
- The race-1 Status change reuses `MoveWire.HoldMoveAsync`. It writes `V0000` into the source's third-default column,
  which is empty.
- Everything else is written through the product: Projects, Tasks, Labels, the Assignee (a Membership of the same Space,
  `(SpaceId, AssigneeMembershipId) → Membership`), added Statuses and removals.
- The S-1 trigger is created and dropped in a `finally` on the class's own database.
- The isolation rows seed only in fresh actors' Spaces. B is read for a Status id and never written, so its 50-Project bound
  is untouched.

## Step 5 - Validation

- Prerequisites: the story has clear ACs, the framework is configured, and the environment is available (Docker 29.9.2).
- **Build:** `dotnet build Yello.slnx` gives 0 warnings and 0 errors.
- **Runs:** see the results section below.
- No CLI browser sessions were opened. The only temp artifacts are the logs in `artifacts/test-runs/` (git ignores them).

### Activation order (riskiest assumption first)

1. **The lock order:** `Two_opposite_moves_queued_on_one_lock_both_land_and_neither_deadlocks`. If the sorted pair can
   deadlock, the design fails.
2. The bound under concurrency: `At_4999_a_move_and_a_second_write_into_the_destination_admit_exactly_one`.
3. The re-reads under the lock: race 1 (Status change), race 4 (destination deleted), race 7 (destination removal).
4. The deletion re-check: `A_deletion_queued_behind_a_cross_Project_move_answers_404_and_the_Task_survives`. It needs no
   route and can go first in Task 2.
5. The contract gates: route, `TaskRoutes`, request, problem type, `TaskResponse.ProjectId`, the lock port and the slice.
6. The core wire cases: kept, required, applied, supplied-unneeded, refusals, bodies.
7. Survivors, bound, S-1, read-back, every Task answer stating `projectId`.
8. Role, boundary, replay. Then the 6 isolation rows go live before review (GATE-003).
9. The browser cases and the copy gate.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

### Existing gates the implementation will turn red (amend them deliberately, in the same change)

- **`TaskResponse` member pins** (AC11 adds `projectId`):
  - `RefusalBoundaryGateTests.The_due_date_is_a_nullable_DateTimeOffset_on_the_Task_and_its_response`;
  - `TaskEndpointTests.TaskProperties` (`TaskEndpointTests.cs:48`), `EditedTaskProperties` (`.Edit.cs:37`);
  - `BoardEndpointTests.TaskProperties`, `BoardPagingTests.cs:245`, `ListPagingTests.TaskProperties`;
  - `ActiveSpaceEndpointTests.AssignedTaskProperties` (`.AssignTask.cs:47`).
- **The Task request sets** (widen by exactly `MoveTaskToProjectRequest`):
  - `RefusalBoundaryGateTests.The_Task_edit_request_carries_its_editable_attributes_and_nothing_else` (`.TaskEdit.cs:50`);
  - the twin list in `RefusalBoundaryGateTests.Tasks.cs:126`.
- **The routes under a Task:** `RefusalBoundaryGateTests.The_routes_under_a_Task_are_its_read_edit_deletion_and_move`
  (`.Move.cs:123`), whose summary says "no route that moves a Task to another Project". Add `POST …/project` and reword it.
  Do not delete it.
- **Isolation route coverage:**
  - `IsolationCaseListTests.Every_api_route_the_Host_maps_…` and
    `Every_Space_scoped_route_a_Token_reaches_has_a_row_…`, until the 6 rows are live;
  - GATE-003 (`Every_pending_row_names_a_story_that_has_not_yet_reached_review`) once 6.5 reaches review with any row
    still pending.
- **Must stay green unchanged:**
  - `ProblemTypes` uniqueness (`BoardPagingGateTests.cs:161`, `ListViewGateTests.cs:205`);
  - `Every_spaceId_endpoint_declares_exactly_one_capability`;
  - `The_Tasks_per_Project_bound_is_declared_only_where_the_route_names_the_Project`. The new route declares no bound;
  - the Token-unreachable gate (`.Move.cs:75`). The new route is not Session-only;
  - the role-matrix theory (`ActiveSpaceEndpointTests.RoleMatrix.cs:53`). It already has a `ManageTasks` representative,
    so it needs no row;
  - the `ProblemResponse` parameter gates. No problem-body member is added;
  - the schema gate (`ProjectEndpointTests.StatusSchema.cs`). No migration.

## Results of this run

- **Build:** `dotnet build Yello.slnx`: 0 warnings, 0 errors.
- **Architecture, whole suite:** 555 tests, 548 passed, 7 skipped (the 7 red gates), 0 failed. Both new active gates pass.
- **Slices,** with `--filter-method` on the 23 new methods: 25 discovered, 4 passed, 21 skipped, 0 failed.
  - The 4 passes are the two green-today theories, 2 rows each.
  - xunit reports a skipped theory once per method, not once per data row.
- **Client,** `--filter-class Yello.Tests.Client.TaskProjectMoveTests`: 10 discovered, 10 skipped, 0 failed.
- **Isolation,** `--filter-class Yello.Tests.Isolation.IsolationCaseListTests`: 16 passed, 0 failed. The 6 pending rows
  satisfy GATE-003 while 6.5 is ready-for-dev.
- The logs are in `artifacts/test-runs/6-5-atdd-*.log`.

**Next workflow:** `bmad-dev-story`.
