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
lastSaved: '2026-11-21'
workflowType: 'testarch-atdd'
storyId: '6.2'
storyKey: '6-2-remove-a-status-from-a-project-by-migrating-its-tasks'
storyFile: '_bmad-output/implementation-artifacts/6-2-remove-a-status-from-a-project-by-migrating-its-tasks.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-6-2-remove-a-status-from-a-project-by-migrating-its-tasks.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the API, isolation, gate and browser scaffolds share ProjectStatusWire and one build)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.RemovedStatus.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemovalRace.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemoval.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemovalScale.cs'
  - 'tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetRemovalTests.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.StatusRemoval.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.StatusRemoval.cs'
  - 'tests/Yello.Tests.Client/ProjectSettingsTests.Removal.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.StatusRemoval.cs'
  - 'tests/Yello.Tests.Slices/Projects/StatusRemovalWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/6-2-remove-a-status-from-a-project-by-migrating-its-tasks.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-6-1-shape-a-projects-statuses.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 6, Story 6.2: Remove a Status from a Project by migrating its Tasks

## Story summary

As an Owner or Admin, I want removing a Status to move the Tasks sitting in it rather than stranding them, so that a Task
can never hold a Status its Project does not expose. Requirements: **FR-26**, **FR-25** (remove and add back), **AR-24**,
with AD-17 (one transaction, no removal without its mapping, the S-2 invariant), AD-16 (`Remove` keyed on the Status id),
AD-15 (migrated keys after the destination's last), S-1 / S-2 (P0), FR-16, NFR-1 / FR-15 / AD-2 / AD-3, AD-18, UX-DR15,
UX-DR16, UX-DR27, UX-DR42, NFR-8, NFR-9.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority.
- **Story:** `ready-for-dev`, AC1-AC16. **No open question is decided**: every scaffold is written against the recommended
  option (a), and a skip that rests on one names it (Q1-Q12).
- **No progress file existed** (`atdd-checklist-6-2-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`. No `project-context.md` exists.
- **Knowledge:** core (data-factories, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** `ProjectEndpointTests` and `TaskEndpointTests` partials (one database each, no new engine);
  `ProjectStatusSliceWire` (`DefaultsAsync`, `EffectiveAsync`, `BoardColumnsAsync`, `DeltaCountAsync`,
  `AssertNoTaskOutsideItsProjectsSetAsync`); `TaskWire.WaitUntilBlockedOnALockAsync`, `DeleteWire.HoldProjectRangeAsync`,
  `MoveWire.HoldMoveAsync` / `PlacementsAsync` / `ColumnAsync`, `TaskWire.SeedTasksAsync`, `ListWire.WalkAsync`; the S-1
  trigger pattern of `ActiveSpaceEndpointTests.DeleteSpace.cs`; the isolation world (`ForeignProjectAsync`,
  `ForeignStatusesInOwnProjectAsync`, `OwnerOfTwoAsync`, `TokenOwnOtherSpaceExpectations`); the gates' `RouteTable`,
  `PropertyNames`, `YelloDbContextFactory`; `FakeYelloApi` and `ProjectSettingsTests`' helpers.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the routes, the dialog and the
add-back list do not exist; every path, locator and copy string for them is the story's recommendation, named in each
file's remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| Q1 | (a) `Remove` op for a default; an added Status's definition and ops deleted | the delta-row asserts; `Removing_an_unoccupied_added_Status_...`; the unit tests |
| Q2 | (a) `POST …/statuses/{id}/remove` `{ destinationStatusId }`, `POST …/statuses/{id}/restore` no body, both `200` with the set | `ProjectStatusWire.Remove`/`Restore` stand-ins; every wire case; the route gate; the fake |
| Q3 | (a) dependent places re-anchored; the removed Status's rename/place deleted; add-back at the Space slot under the Space name | `Removing_a_Status_that_others_are_placed_after_...`; `A_removed_default_is_added_back_...`; the fallback unit test |
| Q4 | (a) `409 status-destination-required`, `400 invalid-status-destination` (no count in the body) | stand-ins `ProjectStatusWire.StatusDestinationRequired` / `InvalidStatusDestination`; the problem-type gate |
| Q5 | (a) one Status per request | the destination = removed Status case; race 5 |
| Q6 | (a) `ITaskStore.LockProjectAsync` before the edit/move reads the set | the two queued-writer races (RACE-1, RACE-2) |
| Q7 | (a) commit enabled; empty commit refused locally with "Choose where these Tasks go." | `An_occupied_Status_opens_a_dialog_...` |
| Q8 | (a) a write's `404` re-reads; inline "That Status is no longer in this Project." | `A_404_for_a_Status_that_left_the_set_...` |
| Q9 | (a) unique `(SpaceId, ProjectId, Sequence)` | `The_delta_sequence_is_unique_per_Project_in_the_model`; the played `Remove` row takes the next Sequence |
| Q10 | (a) a Status-sort cursor on a removed Status is `400 invalid-list-cursor` | `A_Status_sort_cursor_on_a_removed_Status_...` |
| Q11 | (a) one `Nightly`, `Explicit` case, no duration assertion | `ProjectEndpointTests.StatusRemovalScale.cs` |
| Q12 | (a) an unoccupied Status is removed directly | `Removing_an_unoccupied_Status_removes_it_directly_...` |

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ProjectEndpointTests` and
`TaskEndpointTests`; the unit class uses no database; isolation rows ride the boundary world (removals only in fresh
actors' Spaces); the browser cases are a partial of `ProjectSettingsTests` on the assembly's `ClientSurfaceFixture`.
Engines on a full run: unchanged.

### Scenario map

| AC | Level | Scaffold | Priority | Today |
|---|---|---|---|---|
| AC8 (S-2) | Wire (SQL) | `ProjectEndpointTests.The_S2_statement_counts_a_Task_whose_Status_its_own_Project_removed_and_nothing_else` | P0 | **green, active** |
| AC8 (Q6, ledger :1020) | Wire (race) | `TaskEndpointTests.An_edit_to_a_Status_queued_behind_its_removal_is_refused_400_invalid_task_status` | P0 | red |
| AC8 (Q6, ledger :1020) | Wire (race) | `TaskEndpointTests.A_move_to_a_Status_queued_behind_its_removal_is_refused_400_invalid_task_status` | P0 | red |
| AC1, AC8 | Wire (race) | `A_removal_without_a_destination_queued_behind_an_edit_into_the_Status_is_refused_409` | P0 | red |
| AC1, AC8 | Wire (race) | `A_removal_with_a_destination_queued_behind_an_edit_into_the_Status_migrates_the_edited_Task` | P0 | red |
| AC8 | Wire (race) | `TaskEndpointTests.A_creation_queued_behind_the_removal_of_the_first_Status_lands_in_the_new_first_Status` | P0 | red |
| AC2, AC8 | Wire (race) | `Two_removals_naming_each_other_as_destination_queue_and_the_second_is_refused_400` | P0 | red |
| AC3 (S-1) | Wire (trigger) | `A_failure_forced_after_the_migration_rolls_the_whole_removal_back` | P0 | red |
| AC5, AC13 | Wire | `Removing_an_unoccupied_default_drops_it_from_the_set_and_the_Board_and_leaves_the_Space_read_unchanged` | P0 | red |
| AC7 (Q1) | Wire | `Removing_an_unoccupied_added_Status_deletes_its_definition_and_its_name_is_added_again_with_a_new_id` | P0 | red |
| AC1, AC9 | Wire | `Removing_an_occupied_Status_moves_every_Task_to_the_end_of_the_destination_in_source_order` | P0 | red |
| AC1 | Wire | `An_occupied_Status_removed_without_a_destination_is_refused_409_and_nothing_changes` | P0 | red |
| AC2 | Wire | `Every_destination_outside_the_set_after_the_removal_is_refused_with_one_identical_400` | P0 | red |
| AC5 | Wire | `An_unoccupied_Status_with_a_valid_destination_is_removed_and_with_an_invalid_one_is_refused` | P0 | red |
| AC6 | Wire | `Removing_the_last_Status_is_refused_409_status_set_empty_before_any_destination_check` | P0 | red |
| AC7, AC10 | Wire | `A_removed_default_is_added_back_with_its_id_at_its_Space_slot_under_its_Space_name` | P0 | red |
| AC7 | Wire | `Adding_back_a_name_the_set_now_holds_is_409_and_an_id_that_is_not_a_removed_default_is_404` | P0 | red |
| AC10 (ledger :1022) | Wire | `Removing_a_Status_that_others_are_placed_after_leaves_the_rest_in_their_order` | P0 | red |
| AC11 | Wire | `A_Member_or_a_Viewer_is_refused_403_on_removal_and_add_back_on_both_surfaces` (Member, Viewer) | P0 | red |
| AC12 | Wire | `An_unknown_Project_or_a_foreign_Status_in_the_path_answers_the_404_byte_identical` | P0 | red |
| AC13 | Wire | `A_replayed_removal_returns_the_original_answer_and_migrates_once` | P0 | red |
| AC3 (deletion) | Wire | `Deleting_a_Project_or_a_Space_holding_a_removed_default_and_Tasks_succeeds` | P0 | red |
| AC15 (ledger :655) | Wire | `A_Status_sort_cursor_on_a_removed_Status_is_refused_400_and_a_fresh_walk_returns_every_Task_once` | P0 | red |
| AC8, AC15 | Wire | `TaskEndpointTests.A_removed_Status_is_refused_for_an_edit_and_a_move_and_its_column_page_is_404` | P0 | red |
| AC16 (Q11) | Wire (Nightly) | `A_removal_migrating_five_thousand_Tasks_commits_as_one_transaction` | P1 tier | red, Explicit |
| AC2, AC5-AC7, AC10 | Unit | `EffectiveStatusSetRemovalTests` (7 cases) | P0 | red |
| AC11, AC12 | Architecture | `The_remove_and_restore_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound` | P0 | red |
| AC4 (AR-24) | Architecture | `A_Status_leaves_a_Project_only_through_the_remove_route_whose_request_carries_its_destination` | P0 | red |
| AC1, AC2 | Architecture | `The_removal_refusals_have_stable_problem_types` | P0 | red |
| AC7 | Architecture | `The_delta_kinds_are_add_rename_place_and_remove` | P0 | red |
| AC7 | Architecture | `Every_delta_kind_fits_the_column_it_is_stored_in` | P0 | **green, active** |
| Task 2 (Q9, ledger :1032) | Architecture | `The_delta_sequence_is_unique_per_Project_in_the_model` | P0 | red |
| AC12 | Isolation | 11 rows (below), both surfaces | P0 | pending 6.2 |
| AC14 (Q12) | Browser | `Removing_an_unoccupied_Status_removes_it_directly_and_announces_it` | P0 | red |
| AC14 (UX-DR16, Q7) | Browser | `An_occupied_Status_opens_a_dialog_with_no_destination_chosen_and_only_the_Statuses_left` | P0 | red |
| AC14 | Browser | `In_flight_the_progress_region_holds_focus_and_names_the_scope_and_done_moves_focus_to_the_destination` | P0 | red |
| AC14 (Q4) | Browser | `A_stale_count_answered_409_re_reads_the_set_and_opens_the_dialog` | P0 | red |
| AC14 | Browser | `A_refused_removal_says_nothing_was_moved_and_keeps_the_Status` | P0 | red |
| AC7, AC14 (Q2) | Browser | `A_removed_default_is_listed_and_added_back` | P0 | red |
| AC14 (Q8) | Browser | `A_404_for_a_Status_that_left_the_set_re_reads_and_says_so_inline` | P0 | red |
| AC14 (NFR-9) | Browser | `The_removal_dialog_reflows_at_320_pixels_wide` | P0 | red |

Isolation rows (`IsolationCases.StatusRemoval.cs`, `PendingUntil: "6.2"`): `ISO-REMOVE-PROJECT-STATUS-FOREIGN-SPACE` and
`ISO-RESTORE-PROJECT-STATUS-FOREIGN-SPACE` (boundary 404, no delta operation written in the target Space),
`ISO-REMOVE-PROJECT-STATUS-FOREIGN-PROJECT-IN-OWN-SPACE` (in-Space 404), `ISO-REMOVE-FOREIGN-STATUS-IN-OWN-PROJECT` and
`ISO-RESTORE-FOREIGN-STATUS-IN-OWN-PROJECT` (B's Status and a sibling's added Status in the path, each as a random id),
`ISO-REMOVE-FOREIGN-DESTINATION-IN-OWN-PROJECT` and `ISO-REMOVE-SIBLING-DESTINATION-IN-OWN-PROJECT` (body-borne destination,
`400 invalid-status-destination` identical to a random destination), `ISO-RECORD-KIND-PROJECT-STATUS-REMOVE-VIEWER` and
`-RESTORE-VIEWER` (Role refusal naming `configure-project-status-delta`), `ISO-TOKEN-OWN-OTHER-SPACE-PROJECT-STATUS-REMOVE`
and `-RESTORE` (route coverage gate). Both routes have rows on both surfaces.

**Green today, left active (standing regression gates):**

- `The_S2_statement_counts_a_Task_whose_Status_its_own_Project_removed_and_nothing_else` - the S-2 statement
  (`WireDatabase.TasksOutsideTheirProjectsSetSql`) now counts a Task whose Status its own Project's delta removes. This run
  taught the statement `Remove` (it is test code, and with no `Remove` row anywhere it answers exactly what it did). **Seen
  red under a plant:** with the statement's `Remove` clause removed, the gate failed `Expected: 1, Actual: 0`; restored from
  saved content in a `finally`, rebuilt, green again.
- `Every_delta_kind_fits_the_column_it_is_stored_in` - every `StatusDeltaKind` name fits the `varchar(16)` `Kind` column.

**Not scaffolded here, by design:** the handler tests with fakes (Task 3) and the re-anchoring planner's and post-removal
delta's unit tests (Task 1) - their types do not exist, so a test could not compile; the dev phase writes them with the
types. The pager's stale-selection case (AC15 second bullet): no deterministic path re-reads the Board without remounting
`BoardView`, so the dev picks the trigger and writes the `SmallViewportTests` case with it. A specific Member/Viewer browser
case: the existing `A_Member_or_a_Viewer_has_no_settings_link_and_the_page_offers_no_control` counts every button and
field on the page, so it stays the gate once Remove and Add back exist. The Assignee half of AC9: the migration case compares
the whole Task view except `statusId` and `positionKey`, with Labels and a due date set; the Assignee is null in it. The
12-row plant table (product code; Task 8) - plant 1 is already proved above.

## Step 4 - Generated scaffolds (TDD red phase)

Every red scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; isolation rows are
`PendingUntil: "6.2"`. Execution mode resolved to **sequential**.

| File | Cases | Red (skipped / pending) | Active, green today | ACs |
|---|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.RemovedStatus.cs` (new) | 4 | 4 | 0 | AC8, AC15 |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemovalRace.cs` (new) | 5 | 4 | 1 | AC1-AC3, AC8 |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemoval.cs` (new) | 15 | 15 | 0 | AC1-AC3, AC5-AC7, AC9-AC13, AC15 |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemovalScale.cs` (new, Nightly, Explicit) | 1 | 1 | 0 | AC16 |
| `tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetRemovalTests.cs` (new, no database) | 7 | 7 | 0 | AC2, AC5-AC7, AC10 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.StatusRemoval.cs` (new) | 6 | 5 | 1 | AC1, AC2, AC4, AC7, AC11, AC12, Task 2 |
| `tests/Yello.Tests.Isolation/IsolationCases.StatusRemoval.cs` (new) | 11 rows | 11 (0 generated while pending) | 0 | AC11, AC12 |
| `tests/Yello.Tests.Client/ProjectSettingsTests.Removal.cs` (new partial) | 8 | 8 | 0 | AC7, AC14 |

Supporting (no cases): `tests/Yello.Tests.Slices/Projects/StatusRemovalWire.cs` (remove/restore on both surfaces, the delta
rows, the played `Remove` row and the held removal, the S-1 trigger); `tests/Yello.Tests.Shared/ProjectStatusWire.cs`
(stand-ins `Remove`, `Restore`, `RemoveOf`, `RestoreOf`, `RemoveBody`, `StatusDestinationRequired`,
`InvalidStatusDestination`); `tests/Yello.Tests.Shared/WireDatabase.cs` (`TasksOutsideTheirProjectsSetSql` learns `Remove`;
`DeltaOperationCountInAsync`, additive); `tests/Yello.Tests.Client/FakeYelloApi.StatusRemoval.cs` (remove with the `409`s
and the `400`, held for the in-flight case; add-back; the Space's own Status read) with one hook in
`FakeYelloApi.SpaceDeletion.cs` and the Space defaults seeded by `SeedProjectStatuses`; `ProjectSettingsTests` made
`partial`; one spread line `.. StatusRemovalCases()` in `IsolationCases.All`.

### Seed data against the real schema

- **The played `Remove` row** (`StatusRemovalWire.InsertRemoveOperationAsync`, used by the S-2 gate and RACE-1/2/creation),
  checked against `20261121155445_ProjectStatusDelta`: every non-nullable column supplied (`Id`, `SpaceId`, `ProjectId`,
  `StatusId`, `Kind` `varchar(16)`, `Sequence`, `CreatedAt`); `FK_StatusDeltaOperation_Project_SpaceId_ProjectId` - a
  Project the product created in that Space; `FK_StatusDeltaOperation_StatusDefinition_SpaceId_StatusId` - one of that
  Space's own defaults; `IX_StatusDeltaOperation_SpaceId_ProjectId_StatusId_Kind` - each default removed once per Project;
  `Sequence` = the Project's `MAX + 1`, so Q9's unique `(SpaceId, ProjectId, Sequence)` holds too; written under the
  Space's session key (`BLOCK AFTER INSERT`). No `CHECK` constrains `Kind`, so the row is writable before the product
  knows the kind - and the product cannot read it until it does, which is why RACE-1/2 fail today at the set read.
- **The played edit** (`MoveWire.HoldMoveAsync`, RACE-3): the Project's lock, the Task row `INDEX(1)`, then one `UPDATE` to the
  third default at key `V0` - an empty column, so `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` holds; `V0` is base-62.
- **Bulk Tasks** (`TaskWire.SeedTasksAsync`, the cursor case's 60 and the Nightly case's 5,000): the constraints its remarks
  list - composite `FK_Task_Project`, `(SpaceId, StatusId)` to a default of the Space, unique keys by prefix + ordinal. 5,000
  equals the Tasks-per-Project bound; nothing creates a Task through the product afterwards in that Project.
- **The S-1 trigger** (`PlantFailingDeltaTriggerAsync`) is DDL on a connection without Space context, dropped in a `finally`,
  and fires only for the case's Project.
- Every other Account, Space, Project, Task, Label, added Status and removal is written by the product.

### Discovery runs (no full run, per the phase's policy)

Build: `dotnet build Yello.slnx` - 0 warnings, 0 errors.

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Architecture, whole suite (log `artifacts/test-runs/6-2-remove-a-status-from-a-project-by-migrating-its-tasks-atdd-architecture.log`) | 534 | 5 (the new red gates) | 529 | 0 |
| - `RefusalBoundaryGateTests.StatusRemoval.cs` (6 methods, filtered) | 6 | 5 | 1 | 0 |
| Slices, `--filter-method` on the 25 new wire methods (log `...-atdd-slices.log`) | 26 | 25 | 1 | 0 |
| - `ProjectEndpointTests.StatusRemoval.cs` (the theory reports once while skipped) | 15 | 15 | 0 | 0 |
| - `ProjectEndpointTests.StatusRemovalRace.cs` | 5 | 4 | 1 (the S-2 gate) | 0 |
| - `ProjectEndpointTests.StatusRemovalScale.cs` | 1 | 1 | 0 | 0 |
| - `TaskEndpointTests.RemovedStatus.cs` | 4 | 4 | 0 | 0 |
| - (a wildcard also matched one `EffectiveStatusSetRemovalTests` case) | 1 | 1 | 0 | 0 |
| Slices, `--filter-class EffectiveStatusSetRemovalTests` | 7 | 7 | 0 | 0 |
| Isolation, `IsolationCaseListTests` (GATE-001..004, route coverage, pending rows; log `...-atdd-isolation.log`) | 16 | 0 | 16 | 0 |
| Client, the 8 new `ProjectSettingsTests` methods (log `...-atdd-client.log`) | 8 | 8 | 0 | 0 |

## Activation order (riskiest assumption first)

1. **S-2 under the edit/move-versus-removal race, with the lock taken before the Status check** - the riskiest assumption:
   that once a set can lose a Status, no Task writer can strand a Task in it. First confirm the standing gate
   `The_S2_statement_counts_a_Task_whose_Status_its_own_Project_removed_and_nothing_else` is green (it proves the S-2
   statement is not vacuous: planted red already). Then, with `StatusDeltaKind.Remove` and `Apply` in place (Task 1):
   `TaskEndpointTests.An_edit_to_a_Status_queued_behind_its_removal_is_refused_400_invalid_task_status` and
   `A_move_to_a_Status_queued_behind_its_removal_is_refused_400_invalid_task_status` - each must first fail with `200` and an
   S-2 count of 1 (validation before the lock), then pass once Task 4 takes `LockProjectAsync` before `EffectiveOfAsync`;
   then `A_creation_queued_behind_the_removal_of_the_first_Status_lands_in_the_new_first_Status` (expected to pass with no
   creation change - the bound count already holds the lock).
2. `EffectiveStatusSetRemovalTests` (7) with Task 1, and the gates `The_delta_kinds_are_add_rename_place_and_remove`,
   `The_delta_sequence_is_unique_per_Project_in_the_model` with Task 2's migration.
3. `The_removal_refusals_have_stable_problem_types`, `The_remove_and_restore_routes_declare_their_capability_...`,
   `A_Status_leaves_a_Project_only_through_the_remove_route_...` (widen ARCH-503 and amend the two other gates in the same
   change; swap `ProjectStatusWire`'s stand-ins for the contract members).
4. The removal itself: `Removing_an_unoccupied_default_...`, `Removing_an_occupied_Status_moves_every_Task_...`,
   `An_occupied_Status_removed_without_a_destination_...`, `Every_destination_outside_the_set_...`,
   `An_unoccupied_Status_with_a_valid_destination_...`, `Removing_the_last_Status_...`,
   `Removing_an_unoccupied_added_Status_...`.
5. Occupancy under the lock and the transaction: `A_removal_without_a_destination_queued_behind_an_edit_...`,
   `A_removal_with_a_destination_queued_behind_an_edit_...`, `Two_removals_naming_each_other_...`,
   `A_failure_forced_after_the_migration_rolls_the_whole_removal_back` (S-1).
6. Order and add-back: `Removing_a_Status_that_others_are_placed_after_...`, `A_removed_default_is_added_back_...`,
   `Adding_back_a_name_the_set_now_holds_...`.
7. Boundary and pipeline: `A_Member_or_a_Viewer_is_refused_403_on_removal_and_add_back_...`,
   `An_unknown_Project_or_a_foreign_Status_in_the_path_...`, `A_replayed_removal_...`,
   `Deleting_a_Project_or_a_Space_holding_a_removed_default_...`, then the 11 isolation rows made live (GATE-003).
8. Readers: `TaskEndpointTests.A_removed_Status_is_refused_for_an_edit_and_a_move_...`,
   `A_Status_sort_cursor_on_a_removed_Status_...` (Task 7).
9. The 8 `ProjectSettingsTests.Removal` browser cases (Task 6), then the Nightly
   `A_removal_migrating_five_thousand_Tasks_commits_as_one_transaction` run by hand once.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Findings for the dev phase

1. **Active gates that go red by design when the routes and the remove control land** - amend them in the same change:
   `RefusalBoundaryGateTests.The_Project_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound`
   (lists exactly four routes), ARCH-503 `The_routes_under_a_Project_are_its_deletion_its_rename_its_Task_creation_and_its_Board`
   (widen by exactly `POST …/remove` and `POST …/restore`), `No_Project_request_contract_carries_anything_but_a_name` (add
   `RemoveProjectStatusRequest` to its list), `IsolationCaseListTests`' route coverage until the 11 rows are live, and the
   browser case `ProjectSettingsTests.The_Board_links_an_Owner_to_the_editor_which_names_each_Statuses_source` (it asserts
   no "Remove" button and no "×"; its summary says removal is the next story's).
2. **The S-2 statement already knows `Remove`** (Task 8's first bullet is done; remarks rewritten, C3 resolved in test
   code). Plant 1 is proved. Keep asserting it after every case.
3. **Stand-ins:** `ProjectStatusWire.Remove`, `Restore`, `RemoveOf`, `RestoreOf`, `StatusDestinationRequired`,
   `InvalidStatusDestination`; the gates name `RemoveProjectStatusRequest`, the two problem-type members and `Remove` as
   strings; `EffectiveStatusSetRemovalTests.Remove` parses the kind. Swap each for the typed member and delete the stand-in.
4. **The played `Remove` row is the product's shape for an unoccupied default** (Q1 (a)). If the dev phase stores a removal
   differently, correct `StatusRemovalWire.InsertRemoveOperationAsync` and the S-2 statement together and record it.
5. **Client locators and copy are this run's assumptions** (listed in `ProjectSettingsTests.Removal`'s remarks), including a
   native `select` for the picker. A different control is corrected there and recorded in the Debug Log, never by weakening
   the assertion. The fake now serves `GET /api/v1/spaces/{id}/statuses` for any Space whose Project set was seeded.
6. **xUnit filters of different kinds AND together**; the Slices scaffolds are partials, so filter with
   `--filter-method "*<name>*"`. The wildcard `*Removing_the_last_Status*` also matches a unit case.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs; every open question at its recommendation; framework; Docker up for the one
      active container-backed gate).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC16 mapped above (handler tests and the pager selection recorded as the dev phase's).
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 6.2; two standing gates are
      green and active, and say so.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (above).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open.
- [x] No full run (this phase's policy); discovery runs above.

**Next workflow:** `bmad-dev-story` for 6.2, then `bmad-code-review`.
