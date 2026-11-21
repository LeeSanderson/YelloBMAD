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
storyId: '6.3'
storyKey: '6-3-define-the-space-default-statuses-and-cascade-a-rename'
storyFile: '_bmad-output/implementation-artifacts/6-3-define-the-space-default-statuses-and-cascade-a-rename.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-6-3-define-the-space-default-statuses-and-cascade-a-rename.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the wire, isolation, gate and browser scaffolds share SpaceStatusWire and one build)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultsRace.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaults.cs'
  - 'tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetSpaceDefaultTests.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.SpaceDefaultStatuses.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SpaceDefaultStatuses.cs'
  - 'tests/Yello.Tests.Client/SpaceDefaultStatusesTests.cs'
  - 'tests/Yello.Tests.Client/ProjectSettingsTests.SpaceDefaults.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.SpaceDefaultStatuses.cs'
  - 'tests/Yello.Tests.Slices/Projects/SpaceDefaultStatusWire.cs'
  - 'tests/Yello.Tests.Shared/SpaceStatusWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/6-3-define-the-space-default-statuses-and-cascade-a-rename.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-6-2-remove-a-status-from-a-project-by-migrating-its-tasks.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 6, Story 6.3: Define the Space default Statuses, and cascade a rename

## Story summary

As an Owner or Admin, I want to change the Statuses every Project starts from, and to decide what happens where a Project
has diverged, so that a Space-wide change never silently overwrites a deliberate local one. Requirements: **FR-24** (the
ordered default set), **FR-27** (add, rename, reorder), **UX-DR16** (the rename half of the report-and-ask cascade), with
AD-16 (ids survive a rename; derived, never materialised), AD-17 / S-2 (P0), FR-16 (`ConfigureSpaceDefaultStatuses`),
NFR-1 / FR-15 / AD-2 / AD-3, AD-18, UX-DR27, UX-DR31, UX-DR42, NFR-8, NFR-9.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority.
- **Story:** `ready-for-dev`, AC1-AC16. **No open question is decided**: every scaffold is written against the recommended
  option (a), and a skip that rests on one names it (Q1-Q14).
- **No progress file existed** (`atdd-checklist-6-3-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`. No `project-context.md` exists.
- **Knowledge:** core (data-factories, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** `ProjectEndpointTests` partials (one database, no new engine); `ProjectStatusSliceWire`
  (`DefaultsAsync`, `DefinitionsAsync`, `EffectiveAsync`, `BoardColumnsAsync`, `AssertNoTaskOutsideItsProjectsSetAsync`),
  `StatusRemovalWire` (`RemovedAsync`, `RestoreAsync`, `AssertRefused`), `TaskWire.WaitUntilBlockedOnALockAsync`,
  `LabelWire.WaitUntilBlockedOrAnsweredAsync`, `DeleteWire.HoldProjectRangeAsync`, `MoveWire.MoveTaskAsync`, `ListWire`; the
  S-1 trigger pattern of 6.2; the isolation world (`ForeignProjectAsync`, `OwnerOfTwoAsync`,
  `TokenOwnOtherSpaceExpectations`, `AddProjectStatusThroughTheProductAsync`); the gates' `RouteTable`, `PropertyNames`,
  `ClientRolePredicate`; `FakeYelloApi` (`SeedProjectStatuses`, `SpaceDefaultStatuses`, `FollowOnTheBoard`) and
  `ProjectSettingsTests`' helpers.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the Space-level routes, the section
and the dialog do not exist; every path, locator and copy string for them is the story's recommendation, named in each
file's remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| Q1 | (a) `POST …/statuses` `{ name, afterStatusId? }` → `201`; `PATCH …/statuses/{id}` `{ name, replaceProjectNames? }` → `200`; `POST …/statuses/{id}/place` `{ afterStatusId }` → `200`; each answers the defaults as `StatusResponse` rows | `SpaceStatusWire` stand-ins; every wire case; the route and request gates; the fake |
| Q2 | (a) `409 status-rename-conflict` with `conflicts: [{ projectId, projectName, name, taskCount }]` | `A_rename_Projects_conflict_with_…`; the rename race; the problem-body gate; the dialog case |
| Q3 | (a) conflict = own name differs (ordinal) from old and new; equal ones deleted | `An_own_name_equal_to_the_old_or_new_name_…` (case-only is still a conflict); unit `An_own_name_equal_to_the_Space_name_…` |
| Q4 | (a) the decision applies to every Project conflicting at commit | `One_decision_applies_to_every_conflicting_Project_…`; the replayed cascade |
| Q5 | (a) `409 status-name-taken` with `projects: [{ id, name }]`, empty when the clash is among the defaults | `A_Space_add_or_rename_to_a_name_a_Projects_set_holds_…`, `A_clash_through_a_conflicting_Projects_name_…`, the names case |
| Q6 | (a) `Yello/StatusDefaults/{spaceId}`: Exclusive for Space writes, Shared for Project delta writes, before ProjectTasks | the four race cases; `The_Status_defaults_lock_is_named_for_the_Space_…` |
| Q7 | (a) the never-empty guard is the dev phase's unit test (its type does not exist) | not scaffolded (see below) |
| Q8 | (a) last unless `afterStatusId`; before unplaced added Statuses in a Project | `Adding_a_default_reaches_every_Project_…`, `Adding_a_default_after_another_…`; unit `A_new_default_reaches_…` |
| Q9 | (a) positions rewritten 0..n-1 | `Reordering_a_default_…`, `Adding_a_default_after_another_…` |
| Q10 | (a) an S-1 case whose forced failure lands at whichever cascade statement runs second | `A_failure_forced_inside_a_cascading_rename_…` |
| Q11 | the pager browser case | not scaffolded: no deterministic path was found in this run; the dev phase writes it or re-owns the entry to 6.4 |
| Q12 | (a) the Project editor re-reads the defaults with every set it renders | `ProjectSettingsTests.The_add_back_list_takes_a_Space_rename_…` |
| Q13 | (a) no divergence chip | nothing scaffolded |
| Q14 | (a) Escape/Cancel closes, sends nothing, focus back to the rename field | `A_conflicting_rename_opens_a_dialog_…` |

### Container budget

No new container-backed class, fixture or engine. The Slices cases are partials of `ProjectEndpointTests`; the unit class
uses no database; the isolation rows ride the boundary world (Space-level writes only in fresh actors' Spaces, never A's or
B's, and never B's Projects bound); the browser class runs on the assembly's `ClientSurfaceFixture` with `FakeYelloApi`.
Engines on a full run: unchanged.

### Scenario map

| AC | Level | Scaffold | Priority | Today |
|---|---|---|---|---|
| AC9, AC6 (Q5, Q6) | Wire (race) | `A_Space_rename_and_a_Project_add_of_one_name_queue_on_the_Status_lock_and_no_set_holds_it_twice` (Space first, Project first) | P0 | red - **riskiest** |
| AC9, AC6 | Wire (race) | `A_Space_add_and_a_Project_add_of_one_name_queue_on_the_Status_lock_and_exactly_one_is_admitted` | P0 | red |
| AC9, AC4 | Wire (race) | `A_Project_rename_and_a_Space_rename_of_one_default_queue_and_the_outcome_is_consistent` (Space first, Project first) | P0 | red |
| AC9 | Wire (race) | `Two_Project_delta_writes_do_not_wait_on_each_other_while_a_Space_write_waits_on_both` | P0 | red |
| AC1, AC15 | Wire | `Adding_a_default_reaches_every_Project_in_its_Space_slot_and_writes_no_Project_row` | P0 | red |
| AC1 | Wire | `Adding_a_default_after_another_places_it_there_in_the_Space_and_in_a_plain_Project` | P0 | red |
| AC3, AC15 | Wire | `Renaming_a_default_reaches_plain_placed_and_other_renamed_Projects_and_keeps_its_id` | P0 | red |
| AC4 | Wire | `A_rename_Projects_conflict_with_is_refused_409_naming_each_Project_its_name_and_its_Task_count` | P0 | red |
| AC5 | Wire | `One_decision_applies_to_every_conflicting_Project_and_the_rename_reaches_the_rest` (true, false) | P0 | red |
| AC5, AC3 (Q3) | Wire | `An_own_name_equal_to_the_old_or_new_name_or_a_removal_is_no_conflict_and_follows_the_rename` | P0 | red |
| AC6 | Wire | `A_Space_add_or_rename_to_a_name_a_Projects_set_holds_is_refused_409_naming_those_Projects` | P0 | red |
| AC6, AC5 | Wire | `A_clash_through_a_conflicting_Projects_name_depends_on_the_decision` (true, false) | P0 | red |
| AC7 | Wire | `A_blank_overlong_unreadable_or_default_held_name_is_refused_and_100_characters_is_admitted` | P0 | red |
| AC8, AC15 | Wire | `Reordering_a_default_moves_it_in_every_Project_that_has_not_placed_it` | P0 | red |
| AC8, AC12 | Wire | `A_place_anchor_that_is_not_a_default_is_400_and_a_Project_added_Status_in_the_path_is_404` | P0 | red |
| AC2 | Wire | `A_Member_or_a_Viewer_is_refused_403_on_every_Space_default_write_on_both_surfaces` (Member, Viewer) | P0 | red |
| AC2 | Wire | `An_Admin_adds_renames_and_places_a_default_on_both_surfaces` | P0 | red |
| AC12 | Wire | `Another_Spaces_path_or_default_answers_the_404_byte_identical` | P0 | red |
| AC12, AC4, AC6 | Wire | `A_foreign_Project_is_never_named_in_a_conflict_report_or_a_clash_list` | P0 | red |
| AC14 | Wire | `A_replayed_write_answers_the_original_and_applies_once_and_a_replayed_cascade_deletes_nothing_again` | P0 | red |
| AC5 (S-1, Q10) | Wire (trigger) | `A_failure_forced_inside_a_cascading_rename_rolls_both_statements_back` | P0 | red |
| AC16 | Wire | every Slices case above ends on `AssertNoTaskOutsideItsProjectsSetAsync` | P0 | with each |
| AC1, AC3, AC5, AC6, AC8 | Unit | `EffectiveStatusSetSpaceDefaultTests` (6 cases) | P0 | **green, active** |
| AC2, AC12 | Architecture | `The_Space_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound` | P0 | red |
| AC1, AC3, AC8 | Architecture | `No_route_or_request_under_the_Spaces_Statuses_offers_a_removal_a_reset_or_a_mode` | P0 | red |
| AC1, AC5, AC8 | Architecture | `The_Space_Status_requests_carry_exactly_their_members` | P0 | red |
| AC4 | Architecture | `The_rename_conflict_refusal_has_a_stable_problem_type` | P0 | red |
| AC4, AC6 | Architecture | `The_problem_body_appends_a_nullable_conflict_report_and_a_nullable_Project_list` | P0 | red |
| AC2 | Architecture | `The_clients_Space_default_Status_predicate_agrees_with_the_capability_matrix` | P0 | red |
| AC9 | Architecture | `The_Status_defaults_lock_is_named_for_the_Space_as_the_race_holder_names_it` | P0 | red |
| AC12 | Isolation | 14 rows (below), both surfaces | P0 | pending 6.3 |
| AC2, AC11 | Browser | `An_Owner_has_the_section_before_Labels_and_a_Member_or_a_Viewer_never_has_it` (member, viewer) | P0 | red |
| AC11, AC1, AC3, AC8 | Browser | `Adding_renaming_and_moving_a_default_are_each_sent_once_and_announced` | P0 | red |
| AC11, AC4 (Q14) | Browser | `A_conflicting_rename_opens_a_dialog_naming_each_Project_with_nothing_pre_focused` | P0 | red |
| AC11, AC5 | Browser | `Each_answer_sends_its_decision_once_and_is_announced` (replace, keep) | P0 | red |
| AC11, AC6 | Browser | `A_name_a_Project_already_uses_is_refused_inline_naming_the_Project` | P0 | red |
| AC3, AC11 | Browser | `A_Projects_Board_names_the_renamed_column_on_its_next_read` | P0 | red |
| AC11 (NFR-9) | Browser | `The_section_and_the_dialog_reflow_at_320_pixels_wide` | P0 | red |
| AC13 (Q12) | Browser | `ProjectSettingsTests.The_add_back_list_takes_a_Space_rename_after_the_editors_next_write` | P0 | red |

Isolation rows (`IsolationCases.SpaceDefaultStatuses.cs`, `PendingUntil: "6.3"`): `ISO-ADD-SPACE-STATUS-FOREIGN-SPACE` and
`ISO-RENAME-SPACE-STATUS-FOREIGN-SPACE` (boundary 404, recorded, no Status named for the case in the target Space),
`ISO-PLACE-SPACE-STATUS-FOREIGN-SPACE` (boundary 404); `ISO-RENAME-FOREIGN-STATUS-IN-OWN-SPACE` and
`ISO-PLACE-FOREIGN-STATUS-IN-OWN-SPACE` (B's default and a Project-added Status in the caller's own Space path, each as a
random id); `ISO-PLACE-SPACE-STATUS-FOREIGN-ANCHOR` and `ISO-ADD-SPACE-STATUS-FOREIGN-ANCHOR` (body-borne anchor,
`400 invalid-status-position` identical to a random anchor); `ISO-RECORD-KIND-SPACE-STATUS-ADD-VIEWER`, `-RENAME-VIEWER`,
`-PLACE-VIEWER` (Role refusal naming `configure-space-default-statuses`); `ISO-SPACE-STATUS-RENAME-CONFLICT-AGGREGATE` (the
report names the caller's one Project, nothing of another Space's same-named rename); `ISO-TOKEN-OWN-OTHER-SPACE-SPACE-STATUS-ADD`,
`-RENAME`, `-PLACE` (route coverage gate). All three writes have rows on both surfaces.

**Green today, left active (standing regression gates):**

- `EffectiveStatusSetSpaceDefaultTests` (6 cases, no database) - the derivation already delivers most of FR-27: a new default
  reaches every Project at its Space slot (before unplaced added Statuses), a Space rename shows wherever no `Rename` op
  overrides it, a kept own name states the new Space name as its default name, an own name equal (ordinal) to the Space name
  shows no default name, a Space reorder moves a default wherever no `Place` op pins it, and a name a Project's after-set
  would hold twice is the one `Check` refuses. They pin the "no second derivation" premise: the Space plan must reuse `Of`
  and `Check`. They are positive assertions, so no plant is required; a Space write that also wrote per-Project rows would
  not turn them red - the wire cases' delta counts catch that (plants 1 and 7).
- Unchanged and still green: `RefusalBoundaryGateTests.The_Spaces_own_Status_read_keeps_its_shape` (C-1/C-2),
  `ProjectEndpointTests.StatusSchema.cs` (AC15's schema gate), `ActiveSpaceEndpointTests.CreateSpace.cs:108` and
  `RegisterAccountHandlerTests` (AC10's "a new Space starts with three").

**Not scaffolded here, by design:** the planner's and the writer's unit and handler tests (Task 1, Task 4) and the
never-empty guard's unit test (AC10, Q7) - their types do not exist, so a test could not compile; the dev phase writes them
with the types. The pager's stale-selection case (Q11, ledger `:1045`): no Space-level operation takes a column off a Board,
so the dev phase writes it through 6.2's fake removal if a deterministic re-read path exists, or re-owns the entry to 6.4. The
10-row plant table (product code; Task 8). The role-matrix theory row (`ActiveSpaceEndpointTests.RoleMatrix.cs`), which is
amended in the change that maps the routes.

## Step 4 - Generated scaffolds (TDD red phase)

Every red scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; isolation rows are
`PendingUntil: "6.3"`. Execution mode resolved to **sequential**.

| File | Cases | Red (skipped / pending) | Active, green today | ACs |
|---|---|---|---|---|
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultsRace.cs` (new partial) | 4 (2 theories) | 4 | 0 | AC4, AC6, AC9, AC16 |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaults.cs` (new partial) | 17 (3 theories) | 17 | 0 | AC1-AC8, AC12, AC14-AC16 |
| `tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetSpaceDefaultTests.cs` (new, no database) | 6 | 0 | 6 | AC1, AC3, AC5, AC6, AC8 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SpaceDefaultStatuses.cs` (new partial) | 7 | 7 | 0 | AC1-AC6, AC8, AC9, AC12 |
| `tests/Yello.Tests.Isolation/IsolationCases.SpaceDefaultStatuses.cs` (new partial) | 14 rows | 14 (0 generated while pending) | 0 | AC2, AC4, AC12 |
| `tests/Yello.Tests.Client/SpaceDefaultStatusesTests.cs` (new, fake API) | 7 (2 theories) | 7 | 0 | AC1-AC6, AC8, AC11 |
| `tests/Yello.Tests.Client/ProjectSettingsTests.SpaceDefaults.cs` (new partial) | 1 | 1 | 0 | AC13 |

Supporting (no cases): `tests/Yello.Tests.Shared/SpaceStatusWire.cs` (stand-ins `Item`, `Place`, `ItemOf`, `PlaceOf`,
`StatusRenameConflict`; the three bodies; `DefaultsOf`, `ConflictsOf`, `ProjectsOf`; the lock name);
`tests/Yello.Tests.Slices/Projects/SpaceDefaultStatusWire.cs` (the three writes on both surfaces, the Status-defaults lock held
Exclusive or Shared, the Space's delta count and rename ops, the "no name twice" assertion, the S-1 trigger pair);
`tests/Yello.Tests.Client/FakeYelloApi.SpaceDefaultStatuses.cs` (add, rename with the conflict report and the decision, place,
the name clash naming Projects, every seeded Project's set and Board following) with one hook in
`FakeYelloApi.SpaceDeletion.cs`; one spread line `.. SpaceDefaultStatusCases()` in `IsolationCases.All`.

### Seed data against the real schema

- **No row is inserted by SQL.** Every Account, Space, Membership (`AddMemberAsync`, the existing helper, under the Space's
  key for a product-registered Account: `FK_Membership_Account_AccountId`, `AK_Membership_SpaceId_AccountId`), Project, Task,
  added Status, Project rename, place and removal is written by the product. So
  `FK_StatusDeltaOperation_Project_SpaceId_ProjectId`, `FK_StatusDeltaOperation_StatusDefinition_SpaceId_StatusId`,
  `IX_StatusDeltaOperation_SpaceId_ProjectId_StatusId_Kind`, the unique `(SpaceId, ProjectId, Sequence)` index (6.2), the
  filtered unique `(SpaceId, Name) WHERE OwnerProjectId IS NULL` and `(SpaceId, OwnerProjectId, Name)` indexes on
  `StatusDefinition`, `AK_StatusDefinition_SpaceId_Id` and `FK_Task_StatusDefinition` hold by construction.
- **The held lock** (`SpaceDefaultStatusWire.HoldStatusDefaultsAsync`) is `sp_getapplock` on a holder opened for the Space,
  named from its `SESSION_CONTEXT` exactly as `DeleteWire.HoldProjectRangeAsync` names the Project's, and throws if the grant
  fails; the gate `The_Status_defaults_lock_is_named_for_the_Space_as_the_race_holder_names_it` ties the spelling to the
  product's.
- **The S-1 trigger pair** (`PlantFailingCascadeTriggersAsync`) is DDL on a connection without Space context, dropped in a
  `finally`, and fires only for the case's Project and Status: one on `StatusDefinition` `AFTER UPDATE` (throws once that
  Project's `Rename` op is gone), one on `StatusDeltaOperation` `AFTER DELETE` (throws once the default already carries the new
  name), so the throw lands after the first statement whichever order the store chooses.
- **The fake** shares the Space's three default ids across the three seeded Boards, so `SeedProjectStatuses` derives each
  Project's set from the same defaults.

### Discovery runs (no full run, per the phase's policy)

Build: `dotnet build Yello.slnx` - 0 warnings, 0 errors.

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Architecture, whole suite (log `artifacts/test-runs/6-3-define-the-space-default-statuses-and-cascade-a-rename-atdd-architecture.log`) | 541 | 7 (the new red gates) | 534 | 0 |
| Slices, `--filter-method` on the 21 new wire methods (log `...-atdd-slices.log`; a skipped theory reports once) | 21 | 21 | 0 | 0 |
| - `ProjectEndpointTests.SpaceDefaultsRace.cs` | 4 | 4 | 0 | 0 |
| - `ProjectEndpointTests.SpaceDefaults.cs` | 17 | 17 | 0 | 0 |
| Slices, `--filter-class EffectiveStatusSetSpaceDefaultTests` (log `...-atdd-slices-unit.log`) | 6 | 0 | 6 | 0 |
| Isolation, `IsolationCaseListTests` (GATE-001..004, route coverage, pending rows; log `...-atdd-isolation.log`) | 16 | 0 | 16 | 0 |
| Client, `--filter-method` on the 8 new methods (log `...-atdd-client.log`) | 8 | 8 | 0 | 0 |
| - `SpaceDefaultStatusesTests` (a skipped theory reports once) | 7 | 7 | 0 | 0 |
| - `ProjectSettingsTests.The_add_back_list_takes_a_Space_rename_after_the_editors_next_write` | 1 | 1 | 0 | 0 |

## Activation order (riskiest assumption first)

1. **The Space-wide name check under the new lock order** - the riskiest assumption: that a Space write (Exclusive) and a
   Project delta write (Shared, taken before ProjectTasks) on `Yello/StatusDefaults/{spaceId}` serialise every name check,
   so no Project set ever holds a name twice. With Task 2's lock in place, activate the gate
   `The_Status_defaults_lock_is_named_for_the_Space_as_the_race_holder_names_it` (it proves the test holder and the product
   take one resource), then `A_Space_rename_and_a_Project_add_of_one_name_queue_on_the_Status_lock_and_no_set_holds_it_twice`
   (both orders): it must first fail because the Project add answers without waiting on the held lock, then pass once
   `ProjectStatusStore.LockAsync` takes the lock Shared and the Space rename checks every Project's after-set under it. Then
   `A_Space_add_and_a_Project_add_of_one_name_…`, `A_Project_rename_and_a_Space_rename_of_one_default_…` (both orders) and
   `Two_Project_delta_writes_do_not_wait_on_each_other_while_a_Space_write_waits_on_both`.
2. Confirm `EffectiveStatusSetSpaceDefaultTests` stays green, then the dev phase's planner and handler unit tests (Task 1,
   Task 4) and the never-empty guard (AC10).
3. Contract gates: `The_rename_conflict_refusal_has_a_stable_problem_type`, `The_Space_Status_requests_carry_exactly_their_members`,
   `The_problem_body_appends_a_nullable_conflict_report_and_a_nullable_Project_list` (amend
   `The_owned_Spaces_refusal_has_a_stable_type_and_an_appended_identity_only_Spaces_member` in the same change),
   `The_Space_Status_routes_declare_their_capability_…`, `No_route_or_request_under_the_Spaces_Statuses_…`; swap
   `SpaceStatusWire`'s stand-ins for the contract members.
4. Add, rename and reorder: `Adding_a_default_reaches_every_Project_…`, `Adding_a_default_after_another_…`,
   `Renaming_a_default_reaches_plain_placed_and_other_renamed_Projects_…`, `Reordering_a_default_moves_it_…`.
5. The cascade: `A_rename_Projects_conflict_with_…`, `One_decision_applies_to_every_conflicting_Project_…`,
   `An_own_name_equal_to_the_old_or_new_name_…`, then `A_failure_forced_inside_a_cascading_rename_…` (S-1).
6. The clash: `A_Space_add_or_rename_to_a_name_a_Projects_set_holds_…`, `A_clash_through_a_conflicting_Projects_name_…`.
7. Refusals: `A_blank_overlong_unreadable_or_default_held_name_…`, `A_place_anchor_that_is_not_a_default_…`.
8. Boundary and pipeline: `A_Member_or_a_Viewer_is_refused_403_…` (add the role-matrix theory row; the gate
   `The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare` is red until it exists), `An_Admin_adds_renames_…`,
   `Another_Spaces_path_or_default_…`, `A_foreign_Project_is_never_named_…`, `A_replayed_write_…`, then the 14 isolation rows
   made live (GATE-003, before review).
9. Client: `The_clients_Space_default_Status_predicate_agrees_with_the_capability_matrix`, then
   `ProjectSettingsTests.The_add_back_list_takes_a_Space_rename_after_the_editors_next_write` (AC13), then the 7
   `SpaceDefaultStatusesTests` cases.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Findings for the dev phase

1. **Active gates that go red by design** when the contract and routes land - amend them in the same change:
   `The_owned_Spaces_refusal_has_a_stable_type_and_an_appended_identity_only_Spaces_member` (pins `ProblemResponse`'s
   parameters exactly and reads `parameters[^1]` as `Spaces`), `The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare`
   (needs a `ConfigureSpaceDefaultStatuses` row in `ActiveSpaceEndpointTests.RoleMatrix.cs`), and `IsolationCaseListTests`'
   route coverage until the three `ISO-TOKEN-OWN-OTHER-SPACE-SPACE-STATUS-*` rows are live.
   `The_clients_Space_settings_predicate_is_the_union_of_its_sections` stays green when the new predicate joins `Sections`
   (same audience, Owner and Admin); add `ConfigureSpaceDefaultStatuses` to its formula deliberately.
2. **Stand-ins:** `SpaceStatusWire.Item`, `Place`, `ItemOf`, `PlaceOf`, `StatusRenameConflict` and the problem body's
   `conflicts` / `projects` member names; the gates name `AddSpaceStatusRequest`, `RenameSpaceStatusRequest`,
   `PlaceSpaceStatusRequest`, `ProblemTypes.StatusRenameConflict`, `ApplicationLocks.StatusDefaults` and the client predicate as
   strings. Swap each for the typed member and delete the stand-in.
3. **The race cases control the grant order** by sending the second request only once the first is blocked: SQL Server grants
   a released application lock to its waiters in the order they queued. If a run shows otherwise, correct the case (for
   example to assert the either-order outcome) and record it.
4. **The S-1 case names its Project's `Rename` op and the default's new name in its triggers**; if the store writes the
   cascade differently (for example an `ExecuteUpdate` that leaves the op in place), correct the trigger pair and record it.
5. **Client locators and copy are this run's assumptions** (listed in `SpaceDefaultStatusesTests`' remarks), including the
   section's class and the heading "Default Statuses". A different control is corrected there and recorded in the Debug Log,
   never by weakening the assertion. The add announcement is matched only on the new name, because the story gives no copy
   for it.
6. **xUnit filters of different kinds AND together**: the Slices scaffolds are partials of a large class, so filter them with
   `--filter-method`; a `--filter-class` plus `--filter-method` pair selects nothing (exit 8).

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs; every open question at its recommendation; framework; no container needed for
      this phase's runs).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC16 mapped above (planner/handler tests, AC10's guard, the pager case and the plants
      recorded as the dev phase's).
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 6.3; the six derivation unit
      tests are green and active, and say so.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (above).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open.
- [x] No full run (this phase's policy); discovery runs above.

**Next workflow:** `bmad-dev-story` for 6.3, then `bmad-code-review`.
