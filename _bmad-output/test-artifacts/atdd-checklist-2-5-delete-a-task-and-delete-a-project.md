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
lastSaved: '2026-10-26'
workflowType: 'testarch-atdd'
storyId: '2.5'
storyKey: '2-5-delete-a-task-and-delete-a-project'
storyFile: '_bmad-output/implementation-artifacts/2-5-delete-a-task-and-delete-a-project.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-2-5-delete-a-task-and-delete-a-project.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Tasks/DeleteWire.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Delete.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.DeleteIdempotency.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.DeleteRace.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Delete.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.DeleteIdempotency.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.DeleteRace.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Delete.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.DestructiveConfirm.cs'
  - 'tests/Yello.Tests.Architecture/DeletionCopyGateTests.cs'
  - 'tests/Yello.Tests.Client/DeleteTests.cs'
  - 'tests/Yello.Tests.Client/TextSpacingTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/2-5-delete-a-task-and-delete-a-project.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-2-4-define-and-apply-labels.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - 'CLAUDE.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 2, Story 2.5: Delete a Task, and delete a Project

**Date:** 2026-10-26
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server (`Yello.Tests.Slices`, new partials of the
existing `TaskEndpointTests` and `ProjectEndpointTests` on the suite's one engine), isolation rows on both surfaces
(`Yello.Tests.Isolation`, pending on 2.5), ARCH gates (`Yello.Tests.Architecture`), and browser cases against the
network-edge fake (`Yello.Tests.Client`).

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every earlier
story recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by `tests/TESTING-CONVENTIONS.md`.

**Prerequisites - met.** Story `ready-for-dev`; AC1-AC6 verbatim from `epics.md:1140-1164` (`status: final`), AC7-AC14 cite
their sources. MTP, the six test projects, .NET 10 SDK, Rancher (`docker info` 29.5.3). No checklist and no progress file
existed for 2.5, so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
`tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`. No `project-context.md` exists.

**Knowledge:** the core fragments (`data-factories`, `test-quality`, `test-healing-patterns`, `component-tdd`) and the
backend set (`test-levels-framework`, `test-priorities-matrix`, `ci-burn-in`) are generic TypeScript-first guidance;
where they and `tests/TESTING-CONVENTIONS.md` or `CLAUDE.md` disagree, the repository documents win (one engine per test
project, partials not new classes, `LCK_M_%` races, red scaffolds as `[Fact(Skip = "red: AC<n> - ...")]`).

**Entry state:** green after 2.4's review - 989 total, 986 passed, 0 failed, 3 skipped (the bundle cases). No isolation
row pending on any story.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack; no recording. Sources: the story's ACs, Tasks, isolation-row
table and thirteen open questions; the live schema (`dbo.[Task]`, `dbo.[Project]`, `dbo.[TaskLabel]` after
`20261025175001_LabelTables`) and harness; 2.4's scaffolds and their activated form as the pattern.

**Execution mode: sequential.** The runtime can launch subagents, but the API and browser workers would share one build
output and one memory-bound machine (CLAUDE.md: never two `dotnet test` at once), and each would need this whole context
handed over; the standing answer permits generating both in sequence, which this run did.

## Step 3 - Test strategy

### The constraint that shapes every scaffold

No production member of this story exists: no `DELETE` route on either item, no `TaskDeletion`/`ProjectDeletion`, no
deletion slice, no `ProjectRoutes.Project`, no `taskCount`, no "Delete Task"/"Delete Project" control, no deletion copy.
Unlike 2.4, **no scaffold constant file is needed**: both item paths already exist as contract helpers
(`TaskRoutes.TaskOf`, `ProjectRoutes.ProjectOf`), the problem types exist, and the isolation rows can name
`TaskRoutes.Task` and `ProjectRoutes.Rename` (the same template Task 4 renames `Project`). So:

- **Wire cases send `DELETE` on the real paths** (no body - 2.4's decision) and read raw JSON; today those paths answer
  the unmatched `404`, so every case whose request is a deletion is skipped `red: AC<n>`.
- **Architecture gates reach unwritten types by name** (`Yello.Domain.Tasks.TaskDeletion`,
  `Yello.Application.Tasks.DeleteTask.DeleteTaskHandler`, `ProjectRoutes.Project` by reflection).
- **Isolation rows are pending rows**, each with its real `Prepare` and `Then`.
- **Race holders are the product's own statement shapes** (`DeleteWire`), so the test and the request meet on one key.

### Open-question dependencies (scaffolded against the recommended option)

| Q | Recommended | Where the scaffold depends on it | If decided otherwise |
|---|---|---|---|
| Q1 | (a) Delete Project on `ProjectRow`, Space home | `DeleteTests` Project cases, `TextSpacingTests` Project case (locator `main li` + button "Delete Project") | Move the locators to the chosen surface |
| Q2 | (a) `taskCount` appended to the listing, re-read when the confirm opens | `ProjectEndpointTests.The_Project_listing_states_each_Projects_true_Task_count`; `FakeYelloApi.ServeProjectTaskCounts`; `DeleteTests.The_Project_confirm_names...` asserts a re-read | Rewrite that slice case to the chosen read; drop the re-read assertion for (c) |
| Q3 | Task: focus in the column that held it; Project: a heading of Space home | `DeleteTests` focus assertions (`column:Todo`; `H1-H3` inside `main`) | Correct the evaluated expectation |
| Q4 | `404 not-available`, byte-identical | Every not-found case; `ARCH-505` (`{Deleted, NotFound}` only) | (Architecture decides this; not open in practice) |
| Q5 | Task: range → row `INDEX(1)` → delete; Project: row → range → delete | `DeleteWire.DeleteTaskRowAsync` / `DeleteProjectRowAsync` (the held-deletion races) | Change the holder's order to the measured one and record |
| Q6 | No `BEFORE DELETE` predicate | The two-layer plants on the `FOREIGN-ID-IN-OWN-SPACE` rows | Add the predicate to the plant set |
| Q7 | Late edit answers for a deleted Task are dropped | `DeleteTests.An_edit_answered_404_after_its_Tasks_deletion_raises_no_refusal` | Rewrite to "the danger control waits" |
| Q8 | "Delete Task" is the panel's last control, `button-secondary` | Locator by name only; `FocusLastAsync` is the dev's | - |
| Q9 | Focus opens on Cancel | `Delete_Task_replaces_the_panel_content...` | Expect the sentence (`tabindex=-1`) instead |
| Q10 | `aria-busy`, never `disabled` | `DisabledAnywhere` count 0 in the Viewer case | - |
| Q11 | "Task deleted." / "Project deleted.", polite | Live-region assertions | Correct the copy |
| Q12 | A shared `DestructiveConfirm` component | Gates select `.destructive-confirm`, so either way | - |
| Q13 | Measure only, ledger to 2.9 | `A_Project_of_5000_Tasks_is_deleted_whole_by_one_request` writes the elapsed time | - |

### Scenario map (AC → level → case)

| AC | Level | Cases | Priority |
|---|---|---|---|
| AC1 | E2E, INT | `Delete_Task_replaces_the_panel_content_with_a_confirm_naming_the_Task`; Viewer `403` (INT) and absence (E2E) | P0 |
| AC2 | INT, E2E | `The_Project_listing_states_each_Projects_true_Task_count`; `The_Project_confirm_names_the_Project_and_its_true_Task_count` (0/1/3) | P0 |
| AC3 | INT | `An_Owner_Admin_or_Member_deletes_a_Project_and_every_Task_and_application_in_it_goes` (`Assumption=PRD-12-5`); `A_Project_of_5000_Tasks_is_deleted_whole_by_one_request`; schema: cascade key, no soft-delete column | P0 |
| AC4 | ARCH, E2E | `DeletionCopyGateTests` copy; `The_..._confirm(s)_say_it_cannot_be_undone_with_one_danger_action_and_no_icon` | P0 |
| AC5 | ARCH | `No_copy_uses_the_word_archive` + its permanent oracle | P0 |
| AC6 | E2E | `Cancelling_the_Task_confirm...` (Esc and Cancel); `Cancelling_the_Project_confirm...` | P0 |
| AC7 | INT, ARCH | Task and Project: Owner/Admin/Member × both surfaces `204`; Viewer `403` × both surfaces; absent/deleted/foreign `404` byte-identical; Label loop closed; both bounds freed; ARCH-501/502/503/505/506 | P0 |
| AC8 | ISO, ARCH | Six rows pending on 2.5 (story table); ARCH-504 | P0 |
| AC9 | INT, E2E | Replay `204` not `404`; key on another path `422`; keyless resend `404` - Task and Project × both surfaces; client resend reuses its key | P0 |
| AC10 | INT | RACE-T1..T5, RACE-P1 (a)(b), RACE-P2 (a)(b) | P0 |
| AC11 | E2E | Held `DELETE` keeps card and panel; release removes, announces, focuses the column; refusals (`403`/`409`/`422`); no answer → "Not sent." + same key; `404` → Board pattern; late edit `404` → nothing | P0 |
| AC12 | E2E | Project confirm in place, held deletion keeps the row, release removes, announces, focus; last Project → "No Projects yet." | P0 |
| AC13 | ARCH, E2E | DSN-501 border; DSN-502 motion; DSN-503 shadow; rendered copy/one danger/no icon; text spacing (Task and Project confirm) | P1 |
| AC14 | E2E | `A_Viewer_is_offered_no_delete_control_and_nothing_is_disabled` (existing matrix gates hold the predicates - no new capability) | P0 |

Not scaffolded, deliberately: the **handler unit tests** (`DeleteTaskHandlerTests`, `DeleteProjectHandlerTests`) - the
handler types do not exist, a hand-fake test cannot compile against them, and the dev phase writes them with the slice on
the `DeleteLabelHandlerTests` pattern. **I-11**: no Account-scoped route is touched.

## Step 4 - Generated scaffolds

### Seed constraints checked (real schema, including foreign keys)

- **`dbo.[Task]` inserts** (`DeleteWire.InsertTaskAsync`, RACE-P1 (a)): `Id` supplied (`ValueGeneratedNever`,
  `TaskConfiguration.cs:43`); `SpaceId`, `ProjectId`, `StatusId`, `Title`, `PositionKey`, `CreatedAt` all supplied, `DueDate`
  nullable (`TaskConfiguration.cs:45-66`, `20261025143020_TaskDueDate`); the composite
  `FK_Task_Project_SpaceId_ProjectId` (`CASCADE`) satisfied by a Project the product created in that Space; the composite
  `(SpaceId, StatusId)` key (`NO_ACTION`, `TaskConfiguration.cs:80-82`) by a Status read from that Space under its key; the
  unique `(SpaceId, ProjectId, StatusId, PositionKey)` index by `PositionKey.After(last)` of the column's own last key;
  `BLOCK AFTER INSERT` by the Space's own session key.
- **`UPDATE dbo.[Task]`** (`MoveTaskRowAsync`): the destination column is empty in both uses, so `PositionKey.First` is
  unique; `BLOCK AFTER UPDATE` holds under the Space's key; the Status is the Space's own.
- **`DELETE dbo.[Task]` / `DELETE dbo.[Project]`** (the held deletions): through the FILTER predicate under the Space's
  key; `TaskLabel→Task` and `Task→Project` are `CASCADE` (pinned by the active
  `A_Task_reaches_its_Project_through_a_key_carrying_SpaceId_that_cascades`, verified green), `TaskLabel→Label` `NO_ACTION`
  is never on the path. These are the race's competing writer, never cleanup.
- **`TaskLabel` inserts** reuse 2.4's `LabelWire.InsertApplicationAsync` (composite keys to `Task (SpaceId, Id)` and
  `Label (SpaceId, Id)`, both rows product-created in the same Space).
- **Memberships** reuse `AddMemberAsync` (`AK_Membership_SpaceId_AccountId`, never a second Owner); Projects for the bound
  case reuse 2.1's `SeedProjectsAsync`; 5,000 Tasks reuse 2.2's set-based `SeedTasksAsync`.

### Files

| File | Entries (cases) - ids | ACs | State |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/DeleteWire.cs` | harness: `DELETE` on both surfaces; holders for the range, the Task row, the Project row, held Task/Project deletions, a Status move, a creation's insert and its Project read; `TaskPlacement` | - | active |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Delete.cs` | 7 (11) - DEL-T01 (4: Owner/Session, Admin/Token, Member × both), T02, T03 (2), T04, T05; SCH-01, SCH-02 | AC3, AC7 | DEL skipped `red: AC7`; **SCH-01 and SCH-02 green today** |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.DeleteIdempotency.cs` | 2 (4) - IDM-T01, IDM-T02 × both surfaces | AC9 | skipped |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.DeleteRace.cs` | 6 (7) - RACE-T1, T2, T3 (a), T3 (b), T4 (commit, roll back), T5 | AC10 | T1, T3 (a), T5 skipped; **T2, T3 (b), T4 ×2 green today** |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Delete.cs` | 6 (10) - PDEL-01 (4), 02, 03 (2), 04, 05 (Q2 (a)), 06 (5,000 Tasks, measured) | AC2, AC3, AC7 | skipped |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.DeleteIdempotency.cs` | 2 (4) - IDM-P01, IDM-P02 × both surfaces | AC9 | skipped |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.DeleteRace.cs` | 4 - RACE-P1 (a), P1 (b), P2 (a), P2 (b) | AC10 | P1 (a), P2 (a) skipped; **P1 (b), P2 (b) green today** |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 6 rows × 2 surfaces - the story's six (`ISO-DELETE-TASK-FOREIGN-SPACE`, `-TASK-FOREIGN-ID-IN-OWN-SPACE`, `-PROJECT-FOREIGN-SPACE`, `-PROJECT-FOREIGN-ID-IN-OWN-SPACE`, `ISO-RECORD-KIND-TASK-DELETE-VIEWER`, `-PROJECT-DELETE-VIEWER`) | AC7, AC8 | **pending on 2.5** (GATE-003: all six live before `review`); `IsolationCaseListTests` 14/14 green with them |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Delete.cs` | 6 - ARCH-501..506 | AC7, AC8 | skipped |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.DestructiveConfirm.cs` | 3 - DSN-501 border, DSN-502 motion, DSN-503 shadow | AC13 | DSN-501 skipped; **DSN-502, DSN-503 green today** |
| `tests/Yello.Tests.Architecture/DeletionCopyGateTests.cs` | 3 - ARC-001 "archive", ARC-000 its oracle, COPY-501 | AC4, AC5 | COPY-501 skipped; **ARC-001 and ARC-000 green today** |
| `tests/Yello.Tests.Client/DeleteTests.cs` | 13 (18) - E2E-501..513 | AC1, AC2, AC4, AC6, AC11-AC14 | E2E-501..512 skipped; **E2E-513 (the Label confirm) green today** |
| `tests/Yello.Tests.Client/TextSpacingTests.cs` | 2 (4) - A11Y-501 Task confirm, A11Y-502 Project confirm × (1.4.12, 200% zoom) | AC13 | skipped |
| `tests/Yello.Tests.Client/FakeYelloApi.cs` | harness: `DELETE` on the Task and Project item paths, held/refused/unanswered, keyed replay; `ServeProjectTaskCounts`; `FakeProject.TaskCount` (omitted when null, so earlier cases get the same bytes) | - | active |

### Green today - standing regression gates, left active

| Case | Why it is green now | Planted |
|---|---|---|
| `A_Task_reaches_its_Project_through_a_key_carrying_SpaceId_that_cascades` (SCH-01) | 2.2 declared the cascade; this story makes it load-bearing (Q6) | - (a catalogue pin, like 2.4's `TaskLabel` one) |
| `No_Project_Task_or_TaskLabel_column_could_hold_a_deletion_back` (SCH-02) | No soft-delete column exists (§6.2) | - (anchored on each table's real columns) |
| RACE-T2 `A_Status_edit_queued_behind_a_deletion_answers_404_once_the_deletion_commits` | 2.3's review patch: an edit of a vanished Task answers `404` | - |
| RACE-T3 (b) `A_Labels_edit_queued_behind_a_deletion_answers_404_and_writes_no_application` | The Labels edit locks the Task first (2.4 review) | - |
| RACE-T4 ×2 `A_Label_deletion_queued_behind_the_deletion_of_its_last_Task_follows_that_deletions_outcome` | `LabelStore`'s probe waits on the cascaded, uncommitted `TaskLabel` delete | - |
| RACE-P1 (b) `A_creation_queued_behind_a_Project_deletion_answers_404_never_500` | The creation's bound count waits on the range; then its Project read finds nothing | - |
| RACE-P2 (b) `A_Status_edit_queued_behind_its_Projects_deletion_answers_404` | As RACE-T2, through the cascade | - |
| DSN-502 `Nothing_in_a_destructive_confirm_transitions_or_animates` | No rule on the confirm or `.button-danger` declares motion | **caught 1/1** (a `transition` on `.destructive-confirm .button-secondary`) |
| DSN-503 `A_destructive_confirm_carries_no_shadow` | None declares a shadow | **caught 1/1** (a `box-shadow` on the same rule) |
| ARC-001 `No_copy_uses_the_word_archive` | The word appears nowhere in product source | **caught 1/1** ("Label archived." in `ClientCopy.resx`); oracle ARC-000 is permanent |
| E2E-513 `The_Label_confirm_says_it_cannot_be_undone_with_one_danger_action_and_no_icon` | 2.4's `LabelRow` confirm already meets AC13's rendered half | **caught 1/1** (an `svg` in `LabelRow`'s confirm, Debug client rebuilt) |

Every plant was held in memory and restored in a `finally`; `git status` showed no product file changed afterwards.
Each race case waits on `LCK_M_%` before it commits or rolls back (`WaitUntilBlockedOnALockAsync`), so a request that never
waited fails as "answered without waiting" - none relies on timing.

### Gates that go red by design when the dev phase maps the routes (amend by exactly this, never loosen)

1. `RefusalBoundaryGateTests.Projects.cs` `No_route_moves_a_Project` - ARCH-502 is its successor.
2. `RefusalBoundaryGateTests.Tasks.cs` `The_routes_under_a_Project_are_its_rename_its_Task_creation_and_its_Board` - ARCH-503.
3. `RefusalBoundaryGateTests.TaskEdit.cs` `The_Task_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound` -
   `["GET","PATCH"]` becomes `["DELETE","GET","PATCH"]`; ARCH-501 holds the `DELETE`'s capability.
4. `ProjectEndpointTests.An_unmapped_method_on_a_Project_route_answers_404_with_no_Allow_header` sends `DELETE` - retarget
   that one request to a still-unmapped method.
5. If Q2 (a): `ProjectEndpointTests.ProjectProperties` widens by exactly `taskCount` (PDEL-05's
   `ProjectPropertiesWithCount` is the successor); `BoardEndpointTests.ProjectProperties` must not change.
6. `SpaceHomeProjectsTests` cases that count a row's buttons for an Owner will see "Delete Project" - widen by exactly
   that control and record it.

## Step 5 - Activation order (riskiest assumption first)

1. **RACE-P1 (a) first** - `A_deletion_waits_for_a_creation_that_has_read_its_Project_and_takes_the_new_Task_with_it`.
   It tests the story's riskiest assumption (Q5): that the Project deletion's lock on the Project's Task range is what
   serialises it against a creation that has already counted and read its Project. Without it the creation's insert meets
   547 → `500`. Then **RACE-T1** (Task deletion vs a Status edit holding the range: row-before-range deadlocks), then
   RACE-T3 (a), RACE-T5, RACE-P2 (a). Plant each `UPDLOCK, HOLDLOCK`, each `INDEX(1)` and the deletion's range lock, three
   runs each, `n/3` in the Debug Log; record a masked plant (2.4's plant 9 pattern) rather than deleting a case.
2. ARCH-505, ARCH-506 (ports and slices), ARCH-504 (`ProjectRoutes.Project`), then ARCH-501..503 with the three
   red-by-design gates amended in the same change, and the `PUT` retarget.
3. Slices: DEL-T01, T03, T02, T04, T05; PDEL-01, 03, 02, 04; SCH stay green.
4. Isolation rows one at a time, the two `FOREIGN-ID-IN-OWN-SPACE` rows with their two-layer plants (query filter +
   FILTER predicate together fail both surfaces; each alone green); switch the Project rows to `ProjectRoutes.Project`.
   GATE-003: none pending at `review`.
5. IDM-T01/T02, IDM-P01/P02.
6. PDEL-05 (Q2 (a)) with the `ProjectProperties` widening; PDEL-06 (record the measured duration; ledger to 2.9).
7. COPY-501, DSN-501 (Task 11's border; it changes 2.4's Label confirm deliberately).
8. E2E-501..512 and A11Y-501/502, correcting assumed locators to the markup built and recording each correction.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Validation (checklist.md)

- [x] Prerequisites satisfied (story approved, ACs testable, framework configured, environment up).
- [x] Every AC mapped to at least one case; each case names its AC in its skip reason or its traits.
- [x] Red phase: every scaffold whose AC is unimplemented is skipped `red: AC<n> - <why>`; the 13 green-today cases are
      listed above as standing gates, four of them proved against a plant.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] No new test project, package, engine, fixture type or container-backed class: all wire cases are partials of
      `TaskEndpointTests` / `ProjectEndpointTests` on the Slices engine; browser cases share `ClientSurfaceCollection`.
- [x] Isolation rows pending on 2.5 with reasons, following the 2.3/2.4 pattern; GATE-001..004 green.
- [x] Races deterministic (`LCK_M_%`); no timing assertion added, so no new serial collection.
- [x] No product code changed (`git status`: test files and this checklist only).
- [x] No CLI browser sessions opened (no recording mode).

## Run

`dotnet build Yello.slnx`: 0 warnings, 0 errors. `dotnet build-server shutdown`, then
`dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`: **1044 total, 999 passed, 0 failed, 45 skipped**
(11 m 20 s). Against the 989/986/3 baseline that is +13 active (the standing gates above) and +42 skipped (a skipped
theory reports once). Still four engines; no engine, fixture type or serial collection added.

## Completion

- Story handoff: `_bmad-output/implementation-artifacts/2-5-delete-a-task-and-delete-a-project.md`, Dev Notes "ATDD
  artifacts - red-phase scaffolds already in the tree".
- Key assumption: the recommended Q1-Q13. The riskiest is Q5's lock order (RACE-P1 (a) first).
- Next workflow: `bmad-dev-story`.
