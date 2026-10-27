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
storyId: '2.6'
storyKey: '2-6-move-and-reorder-a-task-by-explicit-control'
storyFile: '_bmad-output/implementation-artifacts/2-6-move-and-reorder-a-task-by-explicit-control.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-2-6-move-and-reorder-a-task-by-explicit-control.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'subagent (API in the main run, browser in one worker; built and run once, by the main run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Tasks/MoveWire.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Move.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.MoveIdempotency.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.MoveRace.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.MoveSchema.cs'
  - 'tests/Yello.Tests.Slices/Tasks/MoveTask/PositionKeyBetweenTests.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Move.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.MovePicker.cs'
  - 'tests/Yello.Tests.Client/MoveTests.cs'
  - 'tests/Yello.Tests.Client/TextSpacingTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/2-6-move-and-reorder-a-task-by-explicit-control.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-2-5-delete-a-task-and-delete-a-project.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - 'CLAUDE.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 2, Story 2.6: Move and reorder a Task by explicit control

**Date:** 2026-10-26
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server (`Yello.Tests.Slices`, new partials of the
existing `TaskEndpointTests` on the suite's one engine), UNIT for the key generator (no database), isolation rows on both
surfaces (`Yello.Tests.Isolation`, pending on 2.6), ARCH gates (`Yello.Tests.Architecture`), and browser cases against the
network-edge fake (`Yello.Tests.Client`).

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every earlier
story recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by `tests/TESTING-CONVENTIONS.md`.

**Prerequisites - met.** Story `ready-for-dev`; AC1-AC7 verbatim from `epics.md:1174-1209` (`status: final`), AC8-AC16 cite
their sources. MTP, the six test projects, .NET 10 SDK, Rancher (`docker info` 29.5.3). No checklist and no progress file
existed for 2.6, so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
`tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`. No `project-context.md` exists.

**Knowledge:** the core fragments (`data-factories`, `test-quality`, `test-healing-patterns`, `component-tdd`) and the
backend set (`test-levels-framework`, `test-priorities-matrix`, `ci-burn-in`) are generic TypeScript-first guidance; where
they and `tests/TESTING-CONVENTIONS.md` or `CLAUDE.md` disagree, the repository documents win (one engine per test project,
partials not new classes, `LCK_M_%` races, red scaffolds as `[Fact(Skip = "red: AC<n> - ...")]`).

**Entry state:** green after 2.5's review - 1080 total, 1077 passed, 0 failed, 3 skipped (the bundle cases). No isolation
row pending on any story.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack; no recording, no browser session opened. Sources: the story's ACs,
Tasks, isolation-row table and eighteen open questions; the live schema (`dbo.[Task]` after `20261025115412_TaskTable` and
`20261025143020_TaskDueDate`) and harness; 2.5's scaffolds (now activated) as the pattern.

**Execution mode: subagent.** The API, isolation and ARCH scaffolds were written in the main run; the browser scaffolds by
one worker that wrote files only. Neither worker built or ran anything: one `dotnet build`, then one `dotnet test` at a
time, by the main run (CLAUDE.md: never two `dotnet test` at once).

## Step 3 - Test strategy

### The constraint that shapes every scaffold

No production member of this story exists: no move route, no `TaskRoutes.Move`/`MoveOf`, no `MoveTaskRequest`, no
`ProblemTypes.TaskPositionUnavailable`, no `PositionKey.Between`, no `ITaskStore.MoveAsync`, no `MoveTaskHandler`, no Move
picker, no card actions control. So:

- **Wire cases send `POST {TaskRoutes.TaskOf}/move`** with anonymous-object or raw-JSON bodies (`MoveWire.MoveOf`,
  `MoveWire.MoveBody`), and assert the Q3 type through the scaffold constant `MoveWire.TaskPositionUnavailable`. Today the
  path answers the unmatched `404`, so every case whose request is a move is skipped `red: AC<n>`. On activation, swap the
  two constants for the contract members - nothing else changes.
- **The key generator is reached through a seam** (`PositionKeyBetweenTests.Between`, which throws today): story Task 2
  leaves the jitter source's type open, so the seam takes a seeded `Random` and is bound to `PositionKey.Between` in one line.
- **Architecture gates reach unwritten types by name** (`TaskRoutes.Move`, `MoveTaskRequest`, `ITaskStore.MoveAsync`,
  `MoveTaskHandler`, `ProblemTypes.TaskPositionUnavailable`).
- **Isolation rows are pending rows**, each with its real `Prepare` and `Then`, naming the scaffold constant `MoveRoute`
  (the exact template GATE-002 will look for).
- **Race holders are the product's own statement shapes** (`DeleteWire`, `LabelWire`, `MoveWire.HoldMoveAsync`), so the
  test and the request meet on one key.

### Open-question dependencies (scaffolded against the recommended option)

Every scaffold follows the story's recommended answer to Q1-Q18.

| Q | Recommended | Where the scaffold depends on it | If decided otherwise |
|---|---|---|---|
| Q1 | (a) `POST .../tasks/{taskId}/move`, `.SessionOnly()` | Every wire case's path; MOV-12 (Token = no-credential `401`); ARCH-601/602/604; every isolation row's `ApiToken` = `NoCredential` | Change `MoveOf`/`MoveRoute`; for (b) the `ApiToken` rows become real expectations |
| Q2 | (a) `{ statusId, afterTaskId }`, `null` = top; a `positionKey` member refused `400` | `MoveBody`; MOV-08 (`positionKey` → `400`); ARCH-605 (exactly `AfterTaskId`, `StatusId`); M1 (later move lands nearer the neighbour) | Rewrite the bodies; for "ignored" MOV-08's last row asserts `200` + key not taken |
| Q3 | (a) `409 task-position-unavailable`, identical random/foreign, no row | MOV-07, M6 (a), `ISO-MOVE-FOREIGN-NEIGHBOUR-IN-OWN-SPACE`, ARCH-606, E2E-605 | Correct status/type |
| Q4 | (a) "Top of column" + "Position {n}, after {title}", last also "Bottom of column" | E2E-603, E2E-604, E2E-610 locators | Correct the option names |
| Q5 | (a) one `MovePicker` in place; Move after Status in the panel | E2E-601 (order Status → Move → Delete Task); `.move-picker` in DSN-601/603 | Move the locators/class constants |
| Q6 | (a) a visible actions control "Actions for {title}" + `contextmenu` + `ContextMenu` + `Shift+F10` | E2E-602, E2E-611, E2E-612; `.task-menu` | Correct locators |
| Q7 | Optimistic | E2E-604 (held move already placed, totals moved) | Hold asserts nothing moved yet |
| Q8 | (a) announce on admission; (b) the Status change gains the position; (c) refusal copy where the move was made | E2E-604/605/606/610/615 announcement text | Correct the copy; drop E2E-615 for "no" on (b) |
| Q9 | Panel → its Move control; card menu → moved card's title | E2E-609 | Correct the focus expectation |
| Q10 | Midpoint + short jitter; > 128 → the Q3 refusal | MOV-11, PK-03 (exhaustion as `InvalidOperationException` at the Domain level) | Correct the type/exception |
| Q11 | Keep `(SpaceId, ProjectId, StatusId, PositionKey)`, assert BIN2 on the key column, record the variance | SCH-601 | - |
| Q12 | (a) migrate `NormalizedEmailAddress` to BIN2 | SCH-603, SCH-604 | (b)/(c): delete neither - re-skip with the decision as the reason, and record |
| Q13 | (a) keep the floating tag | SCH-601/602 run on every engine pull | - |
| Q14 | Leave the Status `PATCH` appending over the API | MOV-14 (green today) | - |
| Q15 | Plant 2.5's masked hints under M5/M6 | M5 (b), M6 (b) are where | - |
| Q17 | `ManageTasks`, no new capability | MOV-04, ARCH-601, `ISO-RECORD-KIND-TASK-MOVE-VIEWER` | - |
| Q18 | Same slot → `200`, nothing written | MOV-09 | Expect `409` |

### Scenario map (AC → level → case)

| AC | Level | Cases | Priority |
|---|---|---|---|
| AC1 | E2E | E2E-601 (panel), E2E-602 (five openings), E2E-603 (no default, positions), E2E-616 | P0 |
| AC2 | E2E, ARCH, INT | E2E-611 (320/768/1280, no hover), E2E-612 (Viewer: absent, nothing disabled), DSN-602, MOV-04 | P0 |
| AC3 | INT, UNIT | MOV-01/02 (every sibling's Status and key byte-identical), MOV-09, PK-01..07, ARCH-607 | P0 |
| AC4 | INT | SCH-601 (BIN2 on the index key column), SCH-602 | P0 |
| AC5 | INT | MOV-03 (sequential), RACE-M1 (deterministic) | P0 |
| AC6 | INT, E2E | MOV-01 (another Account's read), E2E-610 (reload) | P0 |
| AC7 | E2E | E2E-604, E2E-605, E2E-610, E2E-615 | P0 |
| AC8 | INT, ARCH | MOV-12 (Token = no credential), MOV-13, MOV-14, MOV-15 (green), ARCH-602, ARCH-605 | P0 |
| AC9 | INT, ARCH | MOV-01..11, ARCH-601/603/604/606/607 | P0 |
| AC10 | UNIT, INT | PK-01..07, MOV-11 | P0 |
| AC11 | INT | RACE-M1..M8 (a) and the four (b) standing gates | P0 |
| AC12 | ISO | Five rows × two surfaces, pending on 2.6 | P0 |
| AC13 | INT | IDM-M01, IDM-M02; E2E-606 (client keeps the key) | P0 |
| AC14 | E2E, ARCH | E2E-601/602/603/608/609, DSN-601, TextSpacing (picker, menu) | P1 |
| AC15 | E2E, ARCH | E2E-604..607, E2E-613 (green), E2E-614, DSN-603 | P0 |
| AC16 | INT | SCH-601, SCH-602 (green), SCH-603, SCH-604 | P0 |

Not scaffolded, deliberately: the **handler unit tests** (`MoveTaskHandlerTests`) - the handler and its port method do not
exist, a hand-fake cannot compile against them, and the dev phase writes them with the slice on the `DeleteTaskHandlerTests`
pattern. **I-11**: no Account-scoped route is touched.

## Step 4 - Generated scaffolds

### Seed constraints checked (real schema, including foreign keys)

- **`dbo.[Task]` inserts** (`MoveWire.SeedTaskAtAsync`, `InsertTaskAtAsync` - MOV-11, SCH-602, RACE-M2): every non-nullable
  column supplied - `Id` (`ValueGeneratedNever`, `TaskConfiguration.cs:43`), `SpaceId`, `ProjectId`, `StatusId`, `Title`,
  `PositionKey`, `CreatedAt`; `DueDate` nullable (`20261025143020_TaskDueDate`). The composite
  `FK_Task_Project_SpaceId_ProjectId` (`CASCADE`) is satisfied by a Project the product created in that Space; the composite
  `(SpaceId, StatusId)` key to `StatusDefinition` (`NO_ACTION`) by a Status read from that Space under its key
  (`TaskWire.StatusesAsync`); `BLOCK AFTER INSERT` by the Space's own session key. The unique
  `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` by keys distinct within their column: MOV-11's two 128-character keys
  differ in their last character; SCH-602's seven keys are distinct under BIN2 (that is the case's point); RACE-M2's key is
  built strictly between two product keys and asserted so (`KeyJustAfter`). Keys are passed as `varchar(128)` (the column,
  `PositionKey.MaxLength`), never truncated.
- **`UPDATE dbo.[Task]`** (`MoveWire.HoldMoveAsync` via `DeleteWire.MoveTaskRowAsync`): range, then the row on its clustered
  key, then the one row; the destination is the Space's own Status; the key is `PositionKey.First` into an empty column, or
  a key built strictly between two neighbours (RACE-M6 (b)) - unique either way; `BLOCK AFTER UPDATE` holds under the
  Space's key.
- **`DELETE`s** (RACE-M5 (a), M6 (a), M7 (a)) reuse 2.5's `DeleteWire` holders: through the FILTER predicate, with the
  database's own cascades (`Task→Project`, `TaskLabel→Task`, `CASCADE`). Competing writers, never cleanup.
- **`TaskLabel` inserts** (RACE-M8 (a)) reuse 2.4's `LabelWire.InsertApplicationAsync` (composite keys to `Task (SpaceId, Id)`
  and `Label (SpaceId, Id)`, both rows product-created in the same Space).
- **Memberships** reuse `AddMemberAsync` (`AK_Membership_SpaceId_AccountId`, never a second Owner); 4,999 Tasks reuse 2.2's
  set-based `SeedTasksAsync` (MOV-10; no new large seed).
- **Isolation rows** seed through the product only (`SeedProjectWithTaskAsync`; Viewer by `ChangeRoleAsync`); Statuses are
  read over the actor's own Session. `WireDatabase.TaskPlacementsInAsync` (new, read-only) reads `(Id, StatusId,
  PositionKey)` under the Space's key for the `Then` checks.

### Files

| File | Entries (cases) - ids | ACs | State |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/MoveWire.cs` | harness: move on a Session, a Token, no credential; `MoveBody`; `PlacementOf`; `ColumnAsync`/`PlacementsAsync`/`KeyOfAsync`; `SeedTaskAtAsync`, `InsertTaskAtAsync` (`varchar(128)`); `HoldMoveAsync`; `KeyJustAfter`; `AssertStrictlyBetween`; scaffold constants `MoveOf`, `TaskPositionUnavailable` | - | active |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Move.cs` | 15 (22) - MOV-01 (Owner/Admin/Member), 02 (top/between/bottom), 03..07, 08 (4 bodies), 09..15 | AC2, AC3, AC5, AC6, AC8, AC9, AC10 | MOV-01..12 skipped `red: AC9/AC5/AC8/AC10`; **MOV-13, 14, 15 green today** |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.MoveIdempotency.cs` | 2 - IDM-M01, IDM-M02 (Session only; the Token is MOV-12's `401`) | AC13 | skipped `red: AC13` |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.MoveRace.cs` | 12 - RACE-M1, M2, M3, M4 (a), M5 (a), M5 (b), M6 (a), M6 (b), M7 (a), M7 (b), M8 (a), M8 (b) | AC5, AC11 | 8 skipped `red: AC11`; **M5 (b), M6 (b), M7 (b), M8 (b) green today** |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.MoveSchema.cs` | 4 - SCH-601, SCH-602, SCH-603, SCH-604 | AC4, AC16 | **SCH-601, SCH-602 green today**; SCH-603/604 skipped `red: AC16` |
| `tests/Yello.Tests.Slices/Tasks/MoveTask/PositionKeyBetweenTests.cs` | 7 - PK-01..07 (no database) | AC3, AC10 | skipped `red: AC10` (seam throws until bound) |
| `tests/Yello.Tests.Shared/WireDatabase.cs` | helper `TaskPlacementsInAsync` (read-only) | AC12 | active |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 5 rows × 2 surfaces - `ISO-MOVE-TASK-FOREIGN-SPACE`, `-TASK-FOREIGN-ID-IN-OWN-SPACE`, `ISO-MOVE-FOREIGN-NEIGHBOUR-IN-OWN-SPACE`, `-FOREIGN-STATUS-IN-OWN-SPACE`, `ISO-RECORD-KIND-TASK-MOVE-VIEWER`; every `ApiToken` expectation is `NoCredential` (Q1: a refusal, not a success) | AC9, AC12 | **pending on 2.6** (GATE-003: all five live before `review`); `IsolationCaseListTests` 14/14 green with them |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Move.cs` | 7 - ARCH-601..607 | AC3, AC8, AC9 | skipped |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.MovePicker.cs` | 3 (4) - DSN-601 (picker, menu), DSN-602, DSN-603 | AC2, AC14, AC15 | DSN-601/603 skipped; **DSN-602 green today** |
| `tests/Yello.Tests.Client/MoveTests.cs` | 16 (24) - E2E-601..616 | AC1-AC3, AC6, AC7, AC14, AC15 | E2E-601..612, 614..616 skipped; **E2E-613 green today** |
| `tests/Yello.Tests.Client/TextSpacingTests.cs` | 2 (4) - the panel picker and the card menu × (1.4.12, 200% zoom) | AC14 | skipped `red: AC14` |
| `tests/Yello.Tests.Client/FakeYelloApi.cs` | harness: `POST .../move` (`MovePathOf`), held/refused/unanswered (`HoldTaskMoveAsync`, `ReleaseTaskMove*`, `NextTaskMoveRefusal`), keyed replay and `422`, `404`/`400`/`409` outcomes, the Board re-ordered with only the moved Task re-keyed (`KeyBetween`); earlier cases unaffected (matches only paths ending `/move`) | - | active |

**Browser locators and copy the scaffolds assume** (correct to the markup built, record each correction): panel button
"Move"; card button "Actions for {title}" inside `.task-card`; `[role=menu]` with `[role=menuitem]` "Open" then "Move";
picker `.move-picker` with `[role=option]` rows, current Status labelled "Current", nothing `aria-selected`/`aria-checked`
true; "Top of column", "Position {n}, after {title}", last also "Bottom of column"; live region `[aria-live='polite']` with
"Moved to {S}, position {n} of {total}." and "Returned to {S}, position {n}."; Q3 copy "That position has changed. Choose it
again."; `Esc` steps back from the position step to the Status step. CSS classes assumed by DSN-601/603: `.move-picker`,
`.task-menu`.

### Green today - standing regression gates, left active

| Case | Why it is green now | Planted |
|---|---|---|
| MOV-13 `A_Token_PATCH_naming_only_a_position_key_is_refused_and_writes_no_key` | 2.3's edit body ignores members it does not read; a body naming none is `400 invalid-task-edit` | - (FR-35 standing gate) |
| MOV-14 `A_Token_Status_PATCH_carrying_a_position_key_appends_and_ignores_the_key` | 2.3's Status edit appends (Q14) | - |
| MOV-15 `A_Tasks_position_key_is_readable_over_a_Token_on_the_Board_and_the_Task` | 2.2 serves `positionKey` on both surfaces | - |
| SCH-601 `The_position_index_carries_the_key_column_in_BIN2` | 2.2 declared the column BIN2 and built the index over it | - (catalogue pin, anchored on the four key columns) |
| SCH-602 `Keys_differing_only_in_case_coexist_in_one_column_and_read_back_in_ordinal_order` | BIN2: no false duplicate, ordinal `ORDER BY` | **owed by the dev phase** (story Task 4: a default-collation column → caught) |
| RACE-M5 (b) `A_deletion_queued_behind_a_move_of_its_Task_answers_204_once_the_move_commits` | 2.5's deletion takes the range first | - (2.5's masked-hint plants go here, Q15) |
| RACE-M6 (b) `A_deletion_of_the_neighbour_queued_behind_a_move_answers_204_and_the_move_stands` | as M5 (b) | - (Q15) |
| RACE-M7 (b) `A_Project_deletion_queued_behind_a_move_answers_204_once_the_move_commits` | the Project deletion takes the range after its row | - |
| RACE-M8 (b) `A_Labels_edit_queued_behind_a_move_of_the_same_Task_answers_200_once_the_move_commits` | the Labels edit locks the Task row `INDEX(1)` first (2.4) | - |
| DSN-602 `No_rule_reveals_or_hides_anything_on_hover_alone` | no `:hover` rule exists in the client's CSS | **caught 1/1** (`.task-card:hover .task-card__open { opacity: 1; }` appended to `components.css`) |
| E2E-613 `Nothing_about_a_move_is_announced_on_load` | no move sentence exists; anchored on the live region existing | - (meaningful once Task 9's announcements land) |

The DSN-602 plant was held in memory as bytes and restored in a `finally`; the file hash matched afterwards and `git status`
showed no product file changed. Each green race case waits on `LCK_M_%` before it commits
(`WaitUntilBlockedOnALockAsync`), so a request that never waited fails as "answered without waiting" - none relies on
timing; none measures time, so no serial collection was added.

### Gates that go red by design when the dev phase builds the story (amend by exactly this, never loosen)

1. `RefusalBoundaryGateTests.The_endpoints_marked_unreachable_with_a_Token_are_exactly_Token_management_and_the_switcher` -
   widens by exactly `TaskRoutes.Move`; ARCH-602 is its successor (rename, carry FR-35's reason).
2. Any exact set of Task routes/methods in `RefusalBoundaryGateTests.Tasks.cs` / `.TaskEdit.cs` - widens by exactly
   `POST {Move}`; ARCH-603 holds the set under one Task.
3. `BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow` ("exactly one control") - widens to the title button
   and, for a mover, the actions control (Q6).
4. If Q8 (b): `TaskDetailTests.Choosing_a_Status_moves_the_card_and_both_totals_at_once` waits for "Moved to Done." - the
   sentence gains the position; amend the expectation (E2E-615 is the successor).
5. Compile, not red: every hand fake of `ITaskStore` (`CreateTaskHandlerTests`, `EditTaskAttributesHandlerTests`,
   `DeleteTaskHandlerTests`, and any other) gains `MoveAsync` throwing `NotSupportedException`.
6. Must stay green unchanged: ARCH-501 (deletes not Session-only), `The_Task_edit_request_carries_its_editable_attributes_and_nothing_else`,
   `TaskProperties` / `EditedTaskProperties` pins, `Every_spaceId_endpoint_declares_exactly_one_capability`, the
   replayable-result gate, the `Results.Created`/`Accepted` ban, the Space-scoped-entity gate.

## Step 5 - Activation order (riskiest assumption first)

1. **RACE-M2 first** - `A_move_reads_its_neighbours_keys_only_under_the_lock`. It tests the story's riskiest assumption:
   that Q2 (a)'s "after X" converges because the move reads X's *current* successor under the Project range (story Task 3,
   steps 2-4), not before it. Every earlier story found its real bug in exactly this kind of lock ordering, and 2.3 proved
   the read-race shape catches it 3/3. It needs the route and the generator: bind the PK seam, map the route, then activate
   M2 before anything else on the wire. Then **RACE-M4 (a)** (a move that took the row before the range deadlocks with a
   Status edit), M1, M3, M5 (a), M6 (a), M7 (a), M8 (a). Plant each hint (range hint, row `INDEX(1)`, row
   `UPDLOCK, HOLDLOCK`, neighbour read before the range), three runs each, `n/3` in the Debug Log; plant 2.5's three masked
   deletion hints under M5 (b)/M6 (b) (Q15). A masked plant is recorded with its reason, never deleted.
2. PK-01..07 (bind the seam to `PositionKey.Between`; record PK-03's same-gap count and length for the 2.9 ledger entry).
3. ARCH-604..607 (contract and port), then ARCH-601..603 with gates 1-2 above amended in the same change.
4. Slices: MOV-01, 02, 09, 04, 05, 06, 07, 08, 11, 10, 03, 12; IDM-M01, IDM-M02. MOV-13..15 stay green.
5. Isolation rows one at a time (swap `MoveRoute`/`MoveOf`/`TaskPositionUnavailable` for the contract members); the
   two-layer plant on `ISO-MOVE-TASK-FOREIGN-ID-IN-OWN-SPACE` (both layers dropped → the Session row fails; each alone →
   green). GATE-003: none pending at `review`.
6. SCH-603, SCH-604 with Q12's migration (record the live default collation first - the ledger entry was reasoned, not
   executed); SCH-602's plant.
7. DSN-601, DSN-603; then E2E-601..616 and the two text-spacing theories, correcting assumed locators to the markup built and
   recording each correction; gates 3-4 above amended deliberately.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Validation (checklist.md)

- [x] Prerequisites satisfied (story approved, ACs testable, framework configured, environment up).
- [x] Every AC mapped to at least one case; each case names its AC in its skip reason or its traits.
- [x] Red phase: every scaffold whose AC is unimplemented is skipped `red: AC<n> - <why>`; the 11 green-today cases are
      listed above as standing gates, one proved against a plant now and one plant owed by the dev phase (SCH-602).
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] No new test project, package, engine, fixture type or container-backed class: all wire cases are partials of
      `TaskEndpointTests` on the Slices engine; `PositionKeyBetweenTests` takes no fixture; browser cases share
      `ClientSurfaceCollection`.
- [x] Isolation rows pending on 2.6 with reasons, following the 2.3-2.5 pattern; the move route's `ApiToken` expectation is
      the no-credential refusal; GATE-001..004 green (14/14).
- [x] Races deterministic (`LCK_M_%`); no timing assertion added, so no new serial collection.
- [x] Scaffolds follow the story's recommended answers to Q1-Q18 (table above).
- [x] No product code changed (`git status`: test files, `WireDatabase.cs` and this checklist only).
- [x] No CLI browser sessions opened (no recording mode).

## Run

See the story's Dev Notes ("ATDD artifacts") and the orchestrator report for the full-run totals of this ATDD run.

## Completion

- Story handoff: `_bmad-output/implementation-artifacts/2-6-move-and-reorder-a-task-by-explicit-control.md`, Dev Notes
  "ATDD artifacts - red-phase scaffolds already in the tree".
- Key assumption: the recommended Q1-Q18. The riskiest is Task 3's read-under-the-range (RACE-M2 first).
- Next workflow: `bmad-dev-story`.
