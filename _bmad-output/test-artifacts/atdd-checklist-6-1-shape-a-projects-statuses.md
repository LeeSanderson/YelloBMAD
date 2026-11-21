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
storyId: '6.1'
storyKey: '6-1-shape-a-projects-statuses'
storyFile: '_bmad-output/implementation-artifacts/6-1-shape-a-projects-statuses.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-6-1-shape-a-projects-statuses.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the API, isolation, gate and browser scaffolds share ProjectStatusWire and one build)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusSchema.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Statuses.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRace.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.SiblingStatus.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.ProjectStatuses.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.ProjectStatuses.cs'
  - 'tests/Yello.Tests.Client/ProjectSettingsTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.ProjectStatuses.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectStatusSliceWire.cs'
  - 'tests/Yello.Tests.Shared/ProjectStatusWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/6-1-shape-a-projects-statuses.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-5-4-delete-your-account.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 6, Story 6.1: Shape a Project's Statuses

## Story summary

As an Owner or Admin, I want to add, rename and reorder the Statuses in one Project, so that its Board reflects how the
work flows without changing anyone else's. Requirements: **FR-25** (add, rename, reorder), **AR-23**, **UX-DR15**, with
AD-16, AD-17/S-2, FR-16, FR-20, NFR-1, FR-15, AD-2, AD-3, AD-18. New: a Project-owned `StatusDefinition` row
(`OwnerProjectId`, NO ACTION to `Project`), a Space-scoped delta table of operations (`Add`, `Rename`, `Place`), the
effective set computed per Project by `EffectiveStatusSet` for all six consumers, four routes, and the Project settings
page with the `StatusDeltaEditor`.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority.
- **Story:** `ready-for-dev`, AC1-AC14. **No open question is decided**: every scaffold is written against the recommended
  option (a), and a skip that rests on one names it (Q1, Q2, Q3, Q4, Q5, Q6, Q12, Q13).
- **No progress file existed** (`atdd-checklist-6-1-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`.
- **Knowledge:** core (data-factories, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code. No `project-context.md` exists.
- **Harness reused:** `ProjectEndpointTests` partials (`RegisterAsync`, `CreateAsync`, `AddMemberAsync`,
  `IssueTokenAsync`, `RefusalKindsAsync`, `CatalogueAsync`, `ProjectRowsAsync`, `AsTaskActor`), `TaskEndpointTests` with
  `TaskWire`, `TaskEditWire`, `MoveWire`, `DeleteWire` (`HoldProjectRangeAsync`, `DeleteProjectAsync`), `ListWire`,
  `TaskWire.WaitUntilBlockedOnALockAsync`; the isolation world (`SeedProjectAsync`, `StatusIdsAsync`, `NewActorAsync`,
  `ChangeRoleAsync`, `BoundaryNotFound`, `SameAsAnchor`, `RoleRefused`, `Served`); the gates' `RouteTable`,
  `SpaceScopedEntities`, `ProductionAssemblies`, `YelloDbContextFactory`; and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the routes, the page and the
editor do not exist; every path, locator and copy string for them is the story's recommendation, named in each file's
remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| Q1 | (a) `StatusDefinition.OwnerProjectId` + `dbo.StatusDeltaOperation` (`Add`, `Rename`, `Place`; unique per Project, Status, Kind) | the riskiest-assumption schema case; the delta-count reads; the model gate |
| Q2 | (a) NO ACTION owning key; ordered Project deletion in one transaction | `Two_Projects_add_the_same_name_and_deleting_one_...`; the model gate |
| Q3 | (a) an absent anchor leaves the Status in place | not reachable in 6.1; an anchor outside the set is `400 invalid-status-position` |
| Q4 | (a) delta writes take `Yello/ProjectTasks/{space}/{project}` | `Two_adds_of_one_name_queue_on_the_Projects_lock_...` |
| Q5 | (a) `{ defaultName, id, name, source, taskCount }`, `space-default` / `added-here` | every Slices read; the entry-contract gate; the editor's chips |
| Q6 | (a) `SQL_Latin1_General_CP1_CI_AS` on three name columns; trimmed, 1-100, case-insensitive | the collation case and model gate; the name-rule case |
| Q7 | (a) the empty-set guard is a domain unit test | not scaffolded (see below) |
| Q12 | (a) `GET`/`POST …/statuses`, `PATCH …/statuses/{id}`, `POST …/statuses/{id}/place` | `ProjectStatusWire` stand-ins; the route gate |
| Q13 | (a) a direct URL renders read-only for Member and Viewer | `A_Member_or_a_Viewer_has_no_settings_link_...` |
| Q8-Q11 | ledger dispositions | not scaffolded (documents, not behaviour) |

An add answers `201` with the whole set; rename and place answer `200`. An add may carry `afterStatusId` (the delta's
"Add then Place in one transaction"). Both are this run's reading of Q12/AC12 and are corrected here if the dev phase
builds otherwise.

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ProjectEndpointTests` and
`TaskEndpointTests`; isolation rows ride the boundary world (and seed added Statuses only in fresh actors' Spaces, never
A's or B's); the browser class uses the assembly's `ClientSurfaceFixture` and `FakeYelloApi`. Engines on a full run:
unchanged.

### Scenario map

| AC | Level | Scaffold | Priority | Today |
|---|---|---|---|---|
| AC4, AC11, AC13 (Q1, Q2) | Wire (catalogue) | `A_Project_added_Status_is_a_StatusDefinition_keyed_NO_ACTION_to_its_Project_under_filtered_unique_names` | P0 | red |
| AC13 (Q2) | Wire | `Two_Projects_add_the_same_name_and_deleting_one_holding_Tasks_in_it_removes_only_its_own` | P0 | red |
| AC4 | Wire (catalogue) | `The_delta_is_stored_as_operations_and_no_table_stores_an_effective_set` | P0 | red |
| AC13 | Wire | `Deleting_a_Space_holding_Projects_with_deltas_and_added_Statuses_succeeds_in_one_statement` | P0 | red |
| AC10 (Q6, ledger :558) | Wire (catalogue) | `The_Status_and_Label_name_columns_compare_case_insensitively` | P0 | red |
| AC1, AC7, AC12 | Wire | `Adding_a_Status_appends_it_as_added_here_and_the_Board_gains_its_column` | P0 | red |
| AC1, AC5 | Wire | `Adding_after_a_default_places_the_new_Status_right_after_it` | P1 | red |
| AC1, AC3, AC7 | Wire | `Renaming_a_default_in_a_Project_keeps_its_id_and_leaves_the_Space_default_as_it_was` | P0 | red |
| AC1, AC3 | Wire | `Renaming_an_added_Status_changes_its_effective_name_and_keeps_its_id` | P0 | red |
| AC5, AC9 | Wire | `Placing_first_and_placing_after_reorders_the_Board_and_the_List` | P0 | red |
| AC5 | Wire | `Task_creation_takes_the_first_Status_of_its_own_Projects_set` | P0 | red |
| AC6 | Wire | `Two_Projects_hold_different_sets_at_once` | P0 | red |
| AC10 | Wire | `A_name_the_set_holds_or_a_blank_or_overlong_name_is_refused_and_a_change_of_case_is_admitted` | P0 | red |
| AC2 | Wire | `A_Member_or_a_Viewer_is_refused_403_on_every_write_on_both_surfaces` (Member, Viewer) | P0 | red |
| AC11 | Wire | `An_unknown_Project_answers_the_boundary_404_byte_identical_and_records_nothing` | P0 | red |
| AC11 | Wire | `A_sibling_Projects_or_another_Spaces_Status_in_the_path_answers_the_in_Space_404` | P0 | red |
| AC12 | Wire | `A_replayed_add_returns_the_original_answer_and_adds_once` | P0 | red |
| AC10 (Q4) | Wire (race) | `Two_adds_of_one_name_queue_on_the_Projects_lock_and_the_second_is_refused_409` | P0 | red |
| AC6 (ledger :533) | Wire | `TaskEndpointTests.A_sibling_Projects_Status_is_refused_for_an_edit_identically_to_a_random_id` | P0 | red |
| AC6 (ledger :533) | Wire | `TaskEndpointTests.A_sibling_Projects_Status_is_refused_for_a_move_identically_to_a_random_id` | P0 | red |
| AC11 | Isolation | 9 rows `ISO-*PROJECT-STATUS*` (see below), both surfaces | P0 | pending 6.1 |
| AC2, AC11 (Q12) | Architecture | `The_Project_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound` | P0 | red |
| AC9 | Architecture | `No_route_or_contract_offers_an_inherit_override_or_reset_mode_for_a_Projects_Statuses` | P0 | red |
| AC8, AC10 | Architecture | `The_Status_refusals_have_stable_problem_types` | P0 | red |
| AC7 (Q5) | Architecture | `The_Project_Status_entry_states_its_source_and_never_a_Space_or_a_position` | P0 | red |
| AC1 | Architecture | `The_Spaces_own_Status_read_keeps_its_shape` | P0 | **green, active** |
| AC4, AC11 (Q1) | Architecture | `The_delta_entity_is_Space_scoped_fixed_at_creation_and_holds_one_operation` | P0 | red |
| AC10, AC13 (Q2, Q6) | Architecture | `The_owning_key_is_NO_ACTION_and_the_three_name_columns_declare_their_case_insensitive_collation_in_the_model` | P0 | red |
| AC2 | Architecture | `The_clients_Project_Status_delta_predicate_agrees_with_the_capability_matrix` | P0 | red |
| AC2, AC7 | Browser | `The_Board_links_an_Owner_to_the_editor_which_names_each_Statuses_source` | P0 | red |
| AC2 (Q13) | Browser | `A_Member_or_a_Viewer_has_no_settings_link_and_the_page_offers_no_control` (member, viewer) | P0 | red |
| AC9, AC14 | Browser | `Move_up_reorders_announces_the_new_position_and_the_Board_follows` | P0 | red |
| AC7, AC14 | Browser | `Adding_a_Status_renders_it_last_as_added_here` | P1 | red |
| AC10, AC14 | Browser | `A_refused_rename_says_why_inline_and_keeps_the_admitted_name` | P1 | red |
| AC14 (NFR-9) | Browser | `The_editor_reflows_at_320_pixels_wide` | P1 | red |
| AC14 (UX-DR31) | Browser | `Switching_Space_purges_the_editors_Statuses` | P1 | red |

Isolation rows (`IsolationCases.ProjectStatuses.cs`, `PendingUntil: "6.1"`): `ISO-DIRECT-PROJECT-STATUSES` (GET, boundary
404), `ISO-WRITE-PROJECT-STATUS` (POST, boundary 404, nothing named in B), `ISO-RENAME-PROJECT-STATUS-FOREIGN-SPACE` (PATCH),
`ISO-PLACE-PROJECT-STATUS-FOREIGN-SPACE` (POST place), `ISO-PROJECT-STATUSES-FOREIGN-PROJECT-IN-OWN-SPACE` (GET, in-Space
404), `ISO-RENAME-FOREIGN-STATUS-IN-OWN-PROJECT` and `ISO-PLACE-FOREIGN-STATUS-IN-OWN-PROJECT` (B's Status and a sibling
Project's added Status, each against a random id), `ISO-RECORD-KIND-PROJECT-STATUS-VIEWER` (Role refusal naming
`configure-project-status-delta`), `ISO-PROJECT-STATUSES-AGGREGATE` (exactly the caller's set). Every route of the four has
at least one row on both surfaces.

**Green today, left active:** `The_Spaces_own_Status_read_keeps_its_shape` - a standing regression gate (AD-19: the Space's
read keeps `{ Id, Name, Position }` and serves defaults only). Every other new scaffold is red and skipped.

**Not scaffolded here, by design:** the `EffectiveStatusSet` unit tests and the empty-set guard (AC8, Q7 (a)) - the delta
types and the two-argument `Of` do not exist, so a test could not compile; the dev phase's Task 1 writes them with the
types. The `TaskCount` value under Tasks (only `0` is asserted). The pager and Task-detail picker follow-order cases in
`SmallViewportTests` and `TaskDetailTests` - the fake now reorders the Board answer those components already follow, so the
dev phase can add one line each. The 10-row plant table (product code; Task 7).

## Step 4 - Generated scaffolds (TDD red phase)

Every red scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; isolation rows are
`PendingUntil: "6.1"`. Execution mode resolved to **sequential**.

| File | Cases | Red (skipped / pending) | Active, green today | ACs |
|---|---|---|---|---|
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusSchema.cs` (new) | 5 | 5 | 0 | AC4, AC10, AC11, AC13 |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Statuses.cs` (new) | 12 | 12 | 0 | AC1-AC3, AC5-AC7, AC9-AC12 |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRace.cs` (new) | 1 | 1 | 0 | AC10 |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.SiblingStatus.cs` (new) | 2 | 2 | 0 | AC6 |
| `tests/Yello.Tests.Isolation/IsolationCases.ProjectStatuses.cs` (new) | 9 rows | 9 (0 generated while pending) | 0 | AC11 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.ProjectStatuses.cs` (new) | 8 | 7 | 1 | AC1, AC2, AC4, AC7-AC11, AC13 |
| `tests/Yello.Tests.Client/ProjectSettingsTests.cs` (new) | 7 | 7 | 0 | AC2, AC7, AC9, AC10, AC14 |

Supporting (no cases): `tests/Yello.Tests.Shared/ProjectStatusWire.cs` (route, problem-type and source stand-ins, bodies,
the entry reader); `tests/Yello.Tests.Slices/Projects/ProjectStatusSliceWire.cs` (the four calls on both surfaces, the
Board's columns, the Space read, defaults/definitions/delta readers, the S-2 query once Projects differ);
`tests/Yello.Tests.Client/FakeYelloApi.ProjectStatuses.cs` (the four paths, and the Board following the set) with one hook
line in `FakeYelloApi.SpaceDeletion.cs`'s `SpaceDeletionAsync`; `WireDatabase.StatusNamesInAsync` (additive); one spread
line `.. ProjectStatusCases()` in `IsolationCases.All`.

### Discovery runs (no full run, per the phase's policy)

Build: `dotnet build Yello.slnx` - 0 warnings, 0 errors.

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Architecture, whole suite (log `artifacts/test-runs/6-1-shape-a-projects-statuses-atdd-architecture.log`) | 528 | 7 (the new red gates) | 521 | 0 |
| - `RefusalBoundaryGateTests.ProjectStatuses.cs` (8 methods, filtered) | 8 | 7 | 1 | 0 |
| Slices, `--filter-method` on the 20 new methods (log `...-atdd-slices.log`) | 22 | 20 | 2 (pre-existing, matched by a wildcard) | 0 |
| - `.StatusSchema.cs` | 5 | 5 | 0 | 0 |
| - `.Statuses.cs` (the theory reports once while skipped) | 12 | 12 | 0 | 0 |
| - `.StatusRace.cs` | 1 | 1 | 0 | 0 |
| - `TaskEndpointTests.SiblingStatus.cs` | 2 | 2 | 0 | 0 |
| Isolation, `IsolationCaseListTests` (GATE-001..004, route coverage, pending rows; log `...-atdd-isolation.log`) | 16 | 0 | 16 | 0 |
| Client, `ProjectSettingsTests` (log `...-atdd-client.log`) | 7 | 7 | 0 | 0 |

## Activation order (riskiest assumption first)

1. `A_Project_added_Status_is_a_StatusDefinition_keyed_NO_ACTION_to_its_Project_under_filtered_unique_names`, then
   `Two_Projects_add_the_same_name_and_deleting_one_holding_Tasks_in_it_removes_only_its_own`, with the model gate
   `The_owning_key_is_NO_ACTION_and_the_three_name_columns_declare_their_case_insensitive_collation_in_the_model` -
   **the riskiest assumption**: that a Project-added Status can be a `StatusDefinition` row with a NO ACTION key to
   `Project` (a cascade is a second path from `Space`, error 1785), that the two filtered unique indexes replace
   `(SpaceId, Name)`, and that Project deletion works as ordered statements in one transaction. If the migration cannot
   take that shape, revisit Q1/Q2 before building anything on it.
2. `Deleting_a_Space_holding_Projects_with_deltas_and_added_Statuses_succeeds_in_one_statement` (the NO ACTION keys checked
   at end of statement), `The_delta_is_stored_as_operations_and_no_table_stores_an_effective_set` (amend the old
   `%Delta%` clause in the same change), `The_Status_and_Label_name_columns_compare_case_insensitively`,
   `The_delta_entity_is_Space_scoped_fixed_at_creation_and_holds_one_operation`.
3. The domain unit tests for `EffectiveStatusSet` and the empty-set guard (dev-written, Task 1).
4. `The_Status_refusals_have_stable_problem_types`, `The_Project_Status_entry_states_its_source_and_never_a_Space_or_a_position`,
   `The_Project_Status_routes_declare_their_capability_...` (widen ARCH-503 in the same change); swap
   `ProjectStatusWire`'s stand-ins for the contract members.
5. `Adding_a_Status_appends_...`, `Renaming_a_default_in_a_Project_...`, `Renaming_an_added_Status_...`,
   `Adding_after_a_default_...`, `A_replayed_add_...`.
6. The six consumers: `Placing_first_and_placing_after_...`, `Task_creation_takes_the_first_Status_...`,
   `Two_Projects_hold_different_sets_at_once`, and the two `TaskEndpointTests.A_sibling_Projects_Status_is_refused_...`.
7. `A_name_the_set_holds_...`, `Two_adds_of_one_name_queue_on_the_Projects_lock_...`,
   `A_Member_or_a_Viewer_is_refused_403_...`, `An_unknown_Project_answers_...`, `A_sibling_Projects_or_another_Spaces_Status_...`.
8. The nine isolation rows made live (`PendingUntil` removed, GATE-003) and
   `No_route_or_contract_offers_an_inherit_override_or_reset_mode_...`.
9. `The_clients_Project_Status_delta_predicate_...`, then the seven `ProjectSettingsTests`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Findings for the dev phase

1. **Active gates that go red by design when the schema or routes land** - amend them in the same change:
   `ProjectEndpointTests.Creating_a_Project_leaves_the_Space_default_Statuses_unchanged_and_stores_no_effective_set` (its
   `%Delta%` clause meets `StatusDeltaOperation`; its successor is `The_delta_is_stored_as_operations_...`), ARCH-503
   `The_routes_under_a_Project_are_its_deletion_its_rename_its_Task_creation_and_its_Board` (widen by exactly the four
   routes), and `IsolationCaseListTests`' route coverage until the nine rows are live.
2. **Readers that read every `StatusDefinition` row will see Project-owned rows** once they exist:
   `TaskWire.StatusesAsync`, `ProjectEndpointTests.StatusIdsAsync` and `WireDatabase.StatusIdsInAsync`. No existing case adds
   a Status, so none changes today, but a new case must use `ProjectStatusSliceWire.DefaultsAsync` for the Space's three.
   `TaskEditWire.TasksOutsideTheirEffectiveSetAsync`'s remarks already say 6.1 narrows it; its narrowed form is
   `ProjectStatusSliceWire.TasksOutsideTheirProjectsSetAsync` - fold it into `WireDatabase` (Task 7) and retire the old one.
3. **Stand-ins:** `ProjectStatusWire.Collection`, `Item`, `Place`, `*Of`, the four `*Type` members, `SpaceDefault`,
   `AddedHere`, `ConfigureCapability`. Swap every use (Slices, Isolation, the fake) for the contract members and delete them.
4. **Client locators and copy are this run's assumptions** (listed in `ProjectSettingsTests`' remarks). A different locator
   in the built page is corrected there and recorded in the Debug Log, never by weakening the assertion.
5. **xUnit filters of different kinds AND together**; the Slices scaffolds are partials of `ProjectEndpointTests` and
   `TaskEndpointTests`, so filter with `--filter-method "*<name>*"`.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs; every open question at its recommendation; framework; Docker not needed for
      the discovery runs).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC14 mapped above (AC8 by the dev's domain unit test, recorded).
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 6.1; one standing gate is
      green and active, and says so.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (below).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open.
- [x] No full run (this phase's policy); discovery runs above.

### Seed data against the real schema

Nothing is seeded by SQL in the new cases: Accounts, Spaces, Projects, Tasks, Memberships' Owners and Tokens are written by
the product, and Statuses only through the routes under test.

- `FK_Task_StatusDefinition` `(SpaceId, StatusId)` -> `StatusDefinition (SpaceId, Id)`, NO ACTION
  (`TaskConfiguration.cs:86-90`): every Task's Status is one the product admitted for its Project - a default, or the
  Project's own added Status set by an edit the product accepted. The sibling and foreign ids are only ever sent, never
  written.
- `AK_Project_SpaceId_Id` and the Task's `(SpaceId, ProjectId)` key: every Task is created in a Project the product created
  in that Space.
- `IX_StatusDefinition_SpaceId_Name` (unique, today unfiltered; `20260828164716_InitialSchema`): the cases that add "Parked" to two Projects of one Space are
  exactly the ones that need its filtered replacement - which is why that case is first in the activation order.
- `AK_Membership_SpaceId_AccountId` / `UX_Membership_SpaceId_Owner`: `AddMemberAsync` adds one non-Owner Membership per
  Space for an Account the product registered, under the Space's own key (`BLOCK AFTER INSERT`).
- The race holds `Yello/ProjectTasks/{space}/{project}` with `sp_getapplock` on a connection opened for the Space, the
  lock's name read from `SESSION_CONTEXT` (`DeleteWire.HoldProjectRangeAsync`).

**Next workflow:** `bmad-dev-story` for 6.1, then `bmad-code-review`.
