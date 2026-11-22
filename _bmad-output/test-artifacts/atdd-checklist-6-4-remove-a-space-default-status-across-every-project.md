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
storyId: '6.4'
storyKey: '6-4-remove-a-space-default-status-across-every-project'
storyFile: '_bmad-output/implementation-artifacts/6-4-remove-a-space-default-status-across-every-project.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-6-4-remove-a-space-default-status-across-every-project.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the wire, isolation, gate and browser scaffolds share SpaceStatusWire stand-ins and one build)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemoval.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemovalRace.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemovalScale.cs'
  - 'tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetSpaceRemovalTests.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SpaceDefaultStatusRemoval.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.SpaceDefaultStatusRemoval.cs'
  - 'tests/Yello.Tests.Client/SpaceDefaultStatusRemovalTests.cs'
  - 'tests/Yello.Tests.Client/SmallViewportTests.cs (one case added)'
  - 'tests/Yello.Tests.Client/FakeYelloApi.SpaceDefaultStatusRemoval.cs'
  - 'tests/Yello.Tests.Slices/Projects/SpaceDefaultStatusWire.cs (extended)'
  - 'tests/Yello.Tests.Shared/SpaceStatusWire.cs (extended)'
inputDocuments:
  - '_bmad-output/implementation-artifacts/6-4-remove-a-space-default-status-across-every-project.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/mockups/project-settings.html'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-13.md'
  - '_bmad-output/specs/spec-yello/SPEC.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-6-3-define-the-space-default-statuses-and-cascade-a-rename.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 6, Story 6.4: Remove a Space default Status across every Project

## Story summary

As an Owner or Admin, I want to retire a Status across a whole Space and be asked about every Project that cannot take my
chosen destination, so that no Task is silently placed somewhere I did not choose. Requirements: **FR-27** (the removal
half), **FR-24** (never empty), **AR-24** (no endpoint removes without its mapping), **UX-DR16** (report-and-ask, removal
half), with FR-26, AD-16/AD-17/AD-18, S-1 and S-2 (P0), FR-16, NFR-1/FR-15/AD-2/AD-3, NFR-8, UX-DR27, UX-DR31, UX-DR42,
NFR-9.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority.
- **Story:** `ready-for-dev`, AC1-AC17. **No open question is decided**: every scaffold is written against the recommended
  option (a), and a skip that rests on one names it (Q1-Q15).
- **No progress file existed** (`atdd-checklist-6-4-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`. No `project-context.md` exists.
- **Knowledge:** core (data-factories, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** `ProjectEndpointTests` partials (one database, no new engine) with the 6.2/6.3 helpers
  (`WithoutPlacement`, `SourceTitles`, `Shown`, `AssertSameNotAvailable`, `ConfigureDefaultsCapability`);
  `ProjectStatusSliceWire`, `StatusRemovalWire`, `SpaceDefaultStatusWire`, `MoveWire.HoldMoveAsync`,
  `DeleteWire.HoldProjectRangeAsync`, `TaskWire.WaitUntilBlockedOnALockAsync`, `TaskWire.SeedTasksAsync`, `ListWire`; the
  isolation world (`ForeignProjectAsync`, `NotADefaultInOwnSpaceAsync`, `ViewerOnTheDefaultsAsync`, `OwnerOfTwoAsync`,
  `SeedProjectWithTaskAsync`, `RemoveProjectStatusThroughTheProductAsync`, `TokenOwnOtherSpaceExpectations`); the gates'
  `RouteTable`, `PropertyNames`, `ElementOf`; `FakeYelloApi` (`SetsOf`, `ProjectName`, `MigrateOnTheBoard`,
  `FollowOnTheBoard`, `NextTaskMoveRefusal`) and the 6.3 browser helpers' shapes.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the route, the Remove control and
the dialog do not exist; every path, locator and copy string for them is the story's recommendation, named in each file's
remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| Q1 | (a) `POST …/statuses/{statusId}/remove` with `{ destinationStatusId?, exceptions?: [{ projectId, destinationStatusId }] }` → `200` with the defaults | `SpaceStatusWire.Remove`/`RemoveOf`/`RemoveBody` stand-ins; every wire case; the route and AR-24 gates; the fake |
| Q2 | (a) the removal reports in its `409 status-destination-required` (member `removal`: `{ projectId, projectName, taskCount, destinations: [{ id, name }] }`); blocked Projects refused `409 status-project-destination-required` | `SpaceStatusWire.RemovalOf` and `StatusProjectDestinationRequired` stand-ins; the report, blocked and race-4 cases; the problem-body gate |
| Q3 | (a) a sibling writer | not visible to the scaffolds |
| Q4 | (a) unoccupied needs no destination; a destination sent anyway is validated; zero-Task Projects never reported | `An_unoccupied_default_is_removed_with_no_destination_…`; `ISO-REMOVE-SPACE-STATUS-FOREIGN-DESTINATION` (unoccupied, still 400) |
| Q5 | (a) StatusDefaults X → ProjectsPerSpace → ProjectTasks (sorted) | the five race cases |
| Q6 | (a) exceptions for unblocked, foreign or random Projects ignored | `Exceptions_for_an_unblocked_a_foreign_or_a_random_Project_are_ignored` |
| Q7 | (a) a Project left empty is `409 status-set-empty` naming it | `The_last_default_or_a_Project_left_with_only_it_…`; browser `A_set_left_empty_is_refused_inline_…` |
| Q8 | (a) no cancel in flight | browser in-flight case presses Escape and the dialog stays |
| Q9 | (a) definition and every op of it deleted; positions not compacted | add-back `404`; `A_Project_that_removed_the_default_is_unaffected_…` (no op names it) |
| Q10 | (a) keep the rename's at-commit rule | ledger only; nothing scaffolded |
| Q11 | (a) the keep announcement without a count | browser `The_rename_keep_answer_is_announced_without_a_count` |
| Q12 | (a) the pager case through the fake | `SmallViewportTests.A_selected_column_removed_at_Space_level_…` |
| Q13 | (a) S-1 trigger on the definition delete | `A_failure_forced_at_the_default_delete_rolls_back_every_Project` |
| Q14 | (a) commit enabled; unset pickers marked and focused | browser `A_commit_with_a_picker_unset_…` |
| Q15 | (a) nightly Explicit case, duration recorded | `A_Space_removal_across_fifty_occupied_Projects_…` |

### Container budget

No new container-backed class, fixture or engine. The Slices cases are partials of `ProjectEndpointTests`; the unit class
uses no database; the isolation rows ride the boundary world (removals only in fresh actors' Spaces, never A's or B's);
the browser class runs on the assembly's `ClientSurfaceFixture` with `FakeYelloApi`. Engines on a full run: unchanged.

### Scenario map

| AC | Level | Scaffold | Priority | Today |
|---|---|---|---|---|
| AC6 (Q5) | Wire (race) | `A_Space_removal_queued_behind_a_Task_edit_into_the_default_counts_or_migrates_the_edited_Task` (with, without destination) | P0 | red - **riskiest** |
| AC6 | Wire (race) | `A_Project_created_while_the_Space_removal_holds_the_Project_list_waits_…` | P0 | red |
| AC6 | Wire (race) | `A_Task_edit_into_the_default_queued_behind_the_Space_removal_is_refused_400` | P0 | red |
| AC6 | Wire (race) | `A_Project_add_back_queued_behind_the_Space_removal_applies_to_the_state_after_it` | P0 | red |
| AC4, AC6 | Wire (race) | `A_Project_that_removes_the_destination_first_makes_the_Space_removal_refuse_naming_it` | P0 | red |
| AC10 (S-1, Q13) | Wire (trigger) | `A_failure_forced_at_the_default_delete_rolls_back_every_Project` | P0 | red |
| AC1, AC7 | Wire | `An_unoccupied_default_is_removed_with_no_destination_from_the_defaults_and_every_Projects_set` | P0 | red |
| AC1, AC2 | Wire | `An_occupied_default_removed_without_a_destination_is_refused_409_with_the_occupancy_report` | P0 | red |
| AC1, AC7 | Wire | `A_destination_every_Project_accepts_moves_each_Projects_Tasks_to_the_end_of_it_in_source_order` | P0 | red |
| AC3, AC4 | Wire | `A_blocked_Project_without_an_exception_is_refused_409_naming_it_and_nothing_is_written` | P0 | red |
| AC3, AC7, AC8 | Wire | `A_blocked_Project_with_an_exception_takes_its_own_destination_…` (default, added Status) | P0 | red |
| AC3, AC12 | Wire | `An_exception_outside_the_blocked_Projects_set_is_refused_400_naming_it_…` | P0 | red |
| AC3, AC12 (Q6) | Wire | `Exceptions_for_an_unblocked_a_foreign_or_a_random_Project_are_ignored` | P0 | red |
| AC1, AC12 | Wire | `Every_invalid_Space_wide_destination_is_refused_with_one_identical_400` | P0 | red |
| AC5 (Q7) | Wire | `The_last_default_or_a_Project_left_with_only_it_is_refused_409_status_set_empty` | P0 | red |
| AC8 | Wire | `A_Project_that_removed_the_default_is_unaffected_and_one_that_renamed_it_is_migrated` | P0 | red |
| AC7 | Wire | `A_Project_whose_places_anchor_on_the_default_keeps_the_rest_of_its_order` | P0 | red |
| AC9 | Wire | `Adding_back_a_default_removed_at_Space_level_answers_404_and_writes_nothing` | P0 | red |
| AC14 | Wire | `After_a_Space_removal_the_List_cursor_the_column_page_the_Board_and_Task_creation_follow` | P0 | red |
| AC11 | Wire | `A_Member_or_a_Viewer_is_refused_403_on_the_Space_removal_on_both_surfaces` (Member, Viewer) | P0 | red |
| AC11 | Wire | `An_Admin_removes_a_default_on_both_surfaces` | P0 | red |
| AC12 | Wire | `Another_Spaces_path_or_default_or_a_Project_added_Status_answers_the_404_byte_identical` | P0 | red |
| AC2, AC12 | Wire | `A_foreign_Project_holding_a_same_named_Status_is_never_named_in_the_report` | P0 | red |
| AC15 | Wire | `A_replayed_Space_removal_returns_the_original_answer_and_migrates_once` | P0 | red |
| AC16 (Q15) | Wire (Nightly, Explicit) | `A_Space_removal_across_fifty_occupied_Projects_commits_as_one_transaction` | P1 | red |
| AC17 | Wire | every Slices case ends on `AssertNoTaskOutsideItsProjectsSetAsync` | P0 | with each |
| AC5, AC7, AC8 | Unit | `EffectiveStatusSetSpaceRemovalTests` (7 cases) | P0 | **green, active** |
| AC1, AC11 | Architecture | `The_Space_removal_route_declares_its_capability_serves_both_surfaces_and_consults_no_bound` | P0 | red |
| AC1, AC3 (AR-24) | Architecture | `A_default_leaves_the_Space_only_through_the_remove_route_whose_request_carries_its_destinations` | P0 | red |
| AC4 | Architecture | `The_blocked_Project_refusal_has_a_stable_problem_type` | P0 | red |
| AC2 | Architecture | `The_problem_body_appends_one_nullable_occupancy_report` | P0 | red |
| AC11, AC12 | Isolation | 7 rows (below), both surfaces | P0 | pending 6.4 |
| AC11, AC13 | Browser | `An_Owner_has_a_Remove_per_default_and_a_Member_or_a_Viewer_has_none` (member, viewer) | P0 | red |
| AC13 (Q4) | Browser | `An_unoccupied_default_is_removed_directly_announced_and_focus_goes_to_the_heading` | P0 | red |
| AC13 (Q2) | Browser | `An_occupied_default_opens_the_dialog_with_the_scope_and_nothing_chosen` | P0 | red |
| AC3, AC13 | Browser | `Choosing_a_destination_lists_each_blocked_Project_with_its_count_and_an_unset_picker` | P0 | red |
| AC13 (Q14) | Browser | `A_commit_with_a_picker_unset_sends_nothing_marks_it_and_focuses_it` | P0 | red |
| AC13 (Q8) | Browser | `In_flight_the_progress_region_holds_focus_and_done_announces_the_Tasks_moved` | P0 | red |
| AC4, AC13 | Browser | `A_refusal_at_commit_says_nothing_was_moved_and_re_renders_the_table_naming_the_Project` | P0 | red |
| AC5, AC13 (Q7) | Browser | `A_set_left_empty_is_refused_inline_naming_the_Project` | P0 | red |
| AC13 | Browser | `The_trap_wraps_both_ways_and_Cancel_or_Escape_sends_nothing_and_returns_focus` (Cancel, Escape) | P0 | red |
| AC13 (NFR-9) | Browser | `The_section_and_the_dialog_reflow_at_320_pixels_wide` | P0 | red |
| AC13 (Q11) | Browser | `The_rename_keep_answer_is_announced_without_a_count` | P0 | red |
| AC14 (Q12) | Browser | `SmallViewportTests.A_selected_column_removed_at_Space_level_leaves_no_selection_after_the_Board_is_read_again` | P1 | **green, active** |

Isolation rows (`IsolationCases.SpaceDefaultStatusRemoval.cs`, `PendingUntil: "6.4"`):
`ISO-REMOVE-SPACE-STATUS-FOREIGN-SPACE` (boundary 404, recorded; the foreign owner still reads three defaults);
`ISO-REMOVE-FOREIGN-STATUS-IN-OWN-SPACE` (B's default and a Project-added Status in the caller's own Space path, each as a
random id); `ISO-REMOVE-SPACE-STATUS-FOREIGN-DESTINATION` (Space-wide destination B's default vs random, unoccupied, `400`);
`ISO-REMOVE-SPACE-STATUS-FOREIGN-EXCEPTION-DESTINATION` (blocked Project's exception B's default vs random, `400`);
`ISO-RECORD-KIND-SPACE-STATUS-REMOVE-VIEWER` (Role refusal naming `configure-space-default-statuses`);
`ISO-SPACE-STATUS-REMOVAL-REPORT-AGGREGATE` (the report names the caller's one Project, nothing of another Space's
same-named occupied Status); `ISO-TOKEN-OWN-OTHER-SPACE-SPACE-STATUS-REMOVE` (route coverage gate).

**Green today, left active (standing regression gates):**

- `EffectiveStatusSetSpaceRemovalTests` (7 cases, no database): with the default taken out of the defaults, every op naming
  it taken out of each delta, and dependent places re-anchored by `EffectiveStatusSet.Reanchored`, every Project's set is
  its set before less that one Status (plain; places anchored on it on both sides; a default placed after it; a renamed
  one; a Project that removed it - identical; a blocked Project - holds it, lacks the destination; a Project left empty and
  the last default - `Check` = `SetEmpty`). They pin the "one derivation, no third ordering algorithm" premise (AC7's
  invariant). Positive assertions on existing code, so no plant is required.
- `SmallViewportTests.A_selected_column_removed_at_Space_level_leaves_no_selection_after_the_Board_is_read_again` (fake API,
  375px): the deterministic re-read path 6.3's Q11 looked for exists - the fake removes "Done" behind the open Board
  (`RemoveSpaceDefaultBehindThePage`), a reorder refused `409 task-position-unavailable` sets `_rereadBoard`, and
  `UseRereadBoardAsync` runs `ForgetSelectionThatLeft`. It passes today because 6.3 already shipped the fix; it is the
  ledger `:1045` browser case and stays active as a standing gate. A column falls back to the first whether or not the
  selection is forgotten, so the case forces a second read that serves "Done" again under the same id and asserts the pager
  stays on the first tab - the part only the fix provides. Its plant (drop the `ForgetSelectionThatLeft` call) belongs to
  the dev phase's plant table.
- Unchanged and still green: `ProjectEndpointTests.StatusSchema.cs` (AC17's schema gate), the 6.2 removal cases (the
  migration helper's lift must keep them green), `RefusalBoundaryGateTests.The_clients_Space_default_Status_predicate_agrees_with_the_capability_matrix`
  (the Remove control uses the same predicate).

**Not scaffolded here, by design:** the removal plan's and the handler's unit tests (Task 1, Task 3) - their types do not
exist, so a test could not compile; the dev phase writes them with the types (`SpaceDefaultStatusRemovalTests.cs`). A
structural gate for the sorted-lock helper (Task 2): the race cases prove the order. The 10-row plant table (product code).

## Step 4 - Generated scaffolds (TDD red phase)

Every red scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; isolation rows are
`PendingUntil: "6.4"`. Execution mode resolved to **sequential**.

| File | Cases | Red (skipped / pending) | Active, green today | ACs |
|---|---|---|---|---|
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemoval.cs` (new partial) | 18 (2 theories) | 18 | 0 | AC1-AC5, AC7-AC9, AC11, AC12, AC14, AC15, AC17 |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemovalRace.cs` (new partial) | 6 (1 theory) | 6 | 0 | AC4, AC6, AC10, AC17 |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemovalScale.cs` (new partial, Nightly, Explicit) | 1 | 1 | 0 | AC16 |
| `tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetSpaceRemovalTests.cs` (new, no database) | 7 | 0 | 7 | AC5, AC7, AC8 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SpaceDefaultStatusRemoval.cs` (new partial) | 4 | 4 | 0 | AC1-AC4, AC11 |
| `tests/Yello.Tests.Isolation/IsolationCases.SpaceDefaultStatusRemoval.cs` (new partial) | 7 rows | 7 (0 generated while pending) | 0 | AC3, AC11, AC12 |
| `tests/Yello.Tests.Client/SpaceDefaultStatusRemovalTests.cs` (new, fake API) | 11 (2 theories) | 11 | 0 | AC3-AC5, AC11, AC13 |
| `tests/Yello.Tests.Client/SmallViewportTests.cs` (one case added) | 1 | 0 | 1 | AC14 |

Supporting (no cases): `tests/Yello.Tests.Shared/SpaceStatusWire.cs` (stand-ins `Remove`, `RemoveOf`,
`StatusProjectDestinationRequired`; `RemoveBody`; `RemovalOf` and the `SpaceStatusOccupancy`/`SpaceStatusChoice` records);
`tests/Yello.Tests.Slices/Projects/SpaceDefaultStatusWire.cs` (`RemoveAsync`, `RemoveWithTokenAsync`, `RemovedAsync`,
`SpaceDeltaRowsAsync`, `PlantFailingDefinitionDeleteTriggerAsync`); `tests/Yello.Tests.Client/FakeYelloApi.SpaceDefaultStatusRemoval.cs`
(the removal in the Host's decision order, the hold/release pair, `SpaceStatusRemovals`, `RemoveSpaceDefaultBehindThePage`)
with one hook in `FakeYelloApi.SpaceDeletion.cs`; one spread line `.. SpaceDefaultStatusRemovalCases()` in `IsolationCases.All`.

### Seed data against the real schema

- **No row is inserted by SQL.** Every Account, Space, Membership (`AddMemberAsync` under the Space's key for a
  product-registered Account: `FK_Membership_Account_AccountId`, `AK_Membership_SpaceId_AccountId`), Project, Task, added
  Status, Project rename, place and removal is written by the product. So `FK_Task_StatusDefinition` (`(SpaceId, StatusId)`,
  NO ACTION), `FK_StatusDeltaOperation_StatusDefinition_SpaceId_StatusId` and the `AfterStatusId` twin (both NO ACTION),
  `FK_StatusDeltaOperation_Project_SpaceId_ProjectId`, `IX_StatusDeltaOperation_SpaceId_ProjectId_StatusId_Kind`, the unique
  `(SpaceId, ProjectId, Sequence)` index and the filtered unique name indexes on `StatusDefinition` hold by construction.
  The scale case seeds Tasks with `TaskWire.SeedTasksAsync` (one set-based statement per Project, under the Space's key,
  into a Status the Project holds: its remarks list the constraints it satisfies).
- **Held locks** are the existing `DeleteWire.HoldProjectRangeAsync` (the Project's Tasks lock, named from the holder's
  `SESSION_CONTEXT`) and `MoveWire.HoldMoveAsync` (that lock, then the Task row moved into the Status, uncommitted).
- **The S-1 trigger** (`PlantFailingDefinitionDeleteTriggerAsync`) is DDL on a connection without Space context, dropped in
  a `finally`, firing `AFTER DELETE` on `StatusDefinition` only for the removed id - the last statement of AC7's order, so
  every migration and op delete has run when it throws.
- **The fake** shares the Space's three default ids across the three seeded Boards; sets are shaped after
  `SeedProjectStatuses`.

### Discovery runs (no full run, per the phase's policy)

Build: `dotnet build Yello.slnx` - 0 warnings, 0 errors.

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Architecture, whole suite (log `artifacts/test-runs/6-4-remove-a-space-default-status-across-every-project-atdd-architecture.log`) | 546 | 4 (the new red gates) | 542 | 0 |
| Slices, `--filter-method` on the 25 new wire methods and the unit class (log `...-atdd-slices.log`; a skipped theory reports once) | 32 | 25 | 7 | 0 |
| - `ProjectEndpointTests.SpaceDefaultRemoval.cs` | 18 | 18 | 0 | 0 |
| - `ProjectEndpointTests.SpaceDefaultRemovalRace.cs` | 6 | 6 | 0 | 0 |
| - `ProjectEndpointTests.SpaceDefaultRemovalScale.cs` | 1 | 1 | 0 | 0 |
| - `EffectiveStatusSetSpaceRemovalTests` | 7 | 0 | 7 | 0 |
| Isolation, `IsolationCaseListTests` (GATE-001..004, route coverage, pending rows; log `...-atdd-isolation.log`) | 16 | 0 | 16 | 0 |
| Client, `--filter-method` on the new class and the pager case (log `...-atdd-client.log`) | 12 | 11 | 1 | 0 |
| - `SpaceDefaultStatusRemovalTests` (a skipped theory reports once) | 11 | 11 | 0 | 0 |
| - `SmallViewportTests.A_selected_column_removed_at_Space_level_…` | 1 | 0 | 1 | 0 |

## Activation order (riskiest assumption first)

1. **The lock set** - the riskiest assumption: that the removal holds `StatusDefaults` X, then `ProjectsPerSpace`, then
   the Tasks lock of every Project whose set holds the Status (sorted), and reads the occupancy only after the last grant.
   Activate `A_Space_removal_queued_behind_a_Task_edit_into_the_default_counts_or_migrates_the_edited_Task` (both rows)
   first: it must first fail because the removal answers without ever blocking on the held Project lock (or ends in a
   `547`), then pass once Task 2's sorted-lock helper is in. Then
   `A_Project_created_while_the_Space_removal_holds_the_Project_list_…`, `A_Task_edit_into_the_default_queued_behind_…`,
   `A_Project_add_back_queued_behind_…`, `A_Project_that_removes_the_destination_first_…`.
2. Confirm `EffectiveStatusSetSpaceRemovalTests` stays green, then write the plan's unit tests with its type (Task 1),
   including AC7's invariant through the plan itself.
3. Contract gates: `The_blocked_Project_refusal_has_a_stable_problem_type`, `The_problem_body_appends_one_nullable_occupancy_report`
   (amend the two `ProblemResponse` parameter gates in the same change), `The_Space_removal_route_declares_its_capability_…`
   and `A_default_leaves_the_Space_only_through_the_remove_route_…` (amend the two 6.3 route gates in the same change); swap
   `SpaceStatusWire`'s stand-ins for the contract members.
4. Core: `An_unoccupied_default_is_removed_…`, `An_occupied_default_removed_without_a_destination_…`,
   `A_destination_every_Project_accepts_…`, `A_blocked_Project_without_an_exception_…`, `A_blocked_Project_with_an_exception_…`,
   `A_Project_whose_places_anchor_on_the_default_…`, `A_Project_that_removed_the_default_is_unaffected_…`.
5. Refusals: `Every_invalid_Space_wide_destination_…`, `An_exception_outside_the_blocked_Projects_set_…`,
   `Exceptions_for_an_unblocked_…`, `The_last_default_or_a_Project_left_with_only_it_…`.
6. Atomicity and readers: `A_failure_forced_at_the_default_delete_rolls_back_every_Project` (S-1),
   `Adding_back_a_default_removed_at_Space_level_…`, `After_a_Space_removal_the_List_cursor_…`.
7. Boundary and pipeline: `A_Member_or_a_Viewer_is_refused_403_…`, `An_Admin_removes_a_default_…`,
   `Another_Spaces_path_or_default_…`, `A_foreign_Project_holding_a_same_named_Status_…`, `A_replayed_Space_removal_…`, then
   the 7 isolation rows made live (GATE-003, before review).
8. Client: the 11 `SpaceDefaultStatusRemovalTests` cases, starting with the Owner/Member/Viewer absence case; then the
   scale case once, recording its duration (Q15).

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Findings for the dev phase

1. **Active gates that go red by design** when the route and contract land - amend them in the same change:
   `RefusalBoundaryGateTests.The_Space_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound`
   (exact route list), `No_route_or_request_under_the_Spaces_Statuses_offers_a_removal_a_reset_or_a_mode` (forbids "Remove"
   and "Destination" under the Space's Statuses; rewrite it - do not delete it - so it still forbids a reset and a mode
   everywhere and a removal outside `…/remove`), `The_problem_body_appends_a_nullable_conflict_report_and_a_nullable_Project_list`
   and `RefusalBoundaryGateTests.DeleteAccount`'s `The_owned_Spaces_refusal_has_a_stable_type_and_an_appended_identity_only_Spaces_member`
   (both pin `ProblemResponse`'s parameters exactly), `IsolationCaseListTests.Every_Space_scoped_route_a_Token_reaches_…`
   until `ISO-TOKEN-OWN-OTHER-SPACE-SPACE-STATUS-REMOVE` is live, and the Client case
   `SpaceDefaultStatusesTests.Each_answer_sends_its_decision_once_and_is_announced(false)`, which asserts the old "2 Projects
   kept their own names." copy that AC13 rewords. The role-matrix theory already has a `ConfigureSpaceDefaultStatuses` row;
   no new row is needed unless the theory is changed to need one per route.
2. **Stand-ins:** `SpaceStatusWire.Remove`, `RemoveOf`, `StatusProjectDestinationRequired` and the report member name
   `removal` (read by `RemovalOf`, written by the fake); the gates name `RemoveSpaceStatusRequest`,
   `SpaceStatusDestinationRequest`, `ProblemTypes.StatusProjectDestinationRequired` and the `Removal` parameter as strings.
   Swap each for the typed member and delete the stand-in.
3. **The race cases control the grant order** by sending the second request only once the first is blocked: SQL Server
   grants a released application lock to its waiters in the order they queued. The Space removal is assumed to block on the
   held Project lock *after* taking `StatusDefaults` X and `ProjectsPerSpace`; if the chosen order differs, correct the case
   and record it.
4. **The S-1 trigger names the default's delete**; if the store deletes it with a statement that also deletes other rows,
   the trigger still fires on that id. If the definition delete is not the last statement, move the trigger and record it.
5. **Client locators and copy are this run's assumptions** (listed in `SpaceDefaultStatusRemovalTests`' remarks), including
   the Space-wide picker as `select:not(table select)` and the blocked table as `tbody tr`. The seeded first default is
   named "Queued" in that class (never the registration seed's first name, ledger `:114`/S1135). A different control is
   corrected there and recorded in the Debug Log, never by weakening the assertion.
6. **xUnit filters of different kinds AND together**: filter the Slices partials with `--filter-method` only.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs; every open question at its recommendation; framework; no container needed for
      this phase's runs).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC17 mapped above (plan/handler unit tests and the plants recorded as the dev phase's).
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 6.4; the seven derivation unit
      tests and the pager browser case are green and active, and say so.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (above).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open.
- [x] No full run (this phase's policy); discovery runs above.

**Next workflow:** `bmad-dev-story` for 6.4, then `bmad-code-review`.
