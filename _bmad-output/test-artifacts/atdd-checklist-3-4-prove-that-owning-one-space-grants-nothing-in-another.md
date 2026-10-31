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
lastSaved: '2026-10-31'
workflowType: 'testarch-atdd'
storyId: '3.4'
storyKey: '3-4-prove-that-owning-one-space-grants-nothing-in-another'
storyFile: '_bmad-output/implementation-artifacts/3-4-prove-that-owning-one-space-grants-nothing-in-another.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-3-4-prove-that-owning-one-space-grants-nothing-in-another.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Isolation/IsolationCases.SeveralSpaces.cs'
  - 'tests/Yello.Tests.Isolation/SameAsAnchorAndStranger.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Isolation/IsolationWorld.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCaseListTests.cs'
  - 'tests/Yello.Tests.Revocation/RevocationCaseTests.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
  - 'tests/Yello.Tests.Shared/ProductActor.cs'
  - 'tests/Yello.Tests.Slices/Boards/BoardPagingTests.CrossSpaceLocks.cs'
  - 'tests/Yello.Tests.Slices/Boards/BoardPagingTests.cs'
  - 'tests/Yello.Tests.Architecture/ClientStaticStateGateTests.cs'
  - 'tests/Yello.Tests.Architecture/SpaceNavigationGateTests.cs'
  - 'tests/Yello.Tests.Client/SpaceSwitchMemoryTests.cs'
  - 'tests/Yello.Tests.Client/RealHostRefusalNoiseTests.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/3-4-prove-that-owning-one-space-grants-nothing-in-another.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-3-3-delete-a-space-and-belong-to-none.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 3, Story 3.4: Prove that owning one Space grants nothing in another

**Date:** 2026-10-31
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** isolation rows on both surfaces in the existing worlds (`Yello.Tests.Isolation`); held-lock race cases as a partial of the existing `BoardPagingTests` container class (`Yello.Tests.Slices`); reflection, IL and source gates (`Yello.Tests.Architecture`); browser cases against the network-edge fake and the real Host (`Yello.Tests.Client`, in the existing collections).

---

## Story summary

3.4 is mostly a measurement story. The server properties it asks for are believed to hold already: the Space comes only from the route, Role only from the `(AccountId, SpaceId)` Membership row, a Token's binding is checked first, and the session key is set per unit of work. The story makes each of these falsifiable for **one Account holding several Spaces**, on both surfaces and in the client. The one product change is AC10, the locking reads that still reach another Space's keys (`deferred-work.md:606`).

**As** the operator deciding whether Yello can ship, **I want** the isolation suite to cover one Account holding several Spaces, on both surfaces and in the client, **so that** the sharpest form of the invariant is measured rather than assumed.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - a foreign object (SA2's) named under SA1 is refused exactly as for a stranger.
2. **AC2** - ownership of SA confers nothing in SB (Viewer, Admin, no Membership); I-10, no Role carried.
3. **AC3** - a Token bound to SA1 is refused on SA2 which the same Account owns, on every Space-scoped route family.
4. **AC4** - one Account, two Spaces, one pooled connection: neither discloses the other.
5. **AC5** - nothing of the departed Space in the DOM or client memory after a switch.
6. **AC6** - every case on both surfaces; a case on one only is a gap.
7. **AC7** - records are `CrossSpace` for the cross-Space cases (C1, decided by Lee); the client manufactures none.
8. **AC8** - a world with one Account holding several Spaces; `AddMembershipAsync`.
9. **AC9** - aggregates and listings for the two-Space Account hold exactly their own Space.
10. **AC10** - no lock reaches another Space's keys (Label deletion, Label application, Project deletion).
11. **AC11** - absences seen red (2.9's rows, 3.2's eight cases, this story's own).
12. **AC12** - no shared mutable state in the client runtime.
13. **AC13** - every exit from a Space purges, whoever navigates.
14. **AC14** - the deletion rows carry their `Assumption` as a row trait.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every earlier run recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by `tests/TESTING-CONVENTIONS.md`.

**Prerequisites - met.** The story is `ready-for-dev`; AC1-AC7 are verbatim from `epics.md:1583-1611` and AC8-AC14 cite their sources. No progress file or checklist existed for this story, so Create mode ran from the start. Docker (Rancher) answered `29.5.3`. The baseline is 1,680 total, 1,671 passed, 0 failed, 9 skipped.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Harness read:** `IsolationCases` (all of it), `Expectation`, `IsolationWorld`, `CaseRun`, `IsolationCaseListTests`, `IsolationCaseTests`, `PooledConnectionCaseTests`, `RevocationCaseTests`, `ProductActor`, `WireDatabase`, `BoardPagingTests` (PG-16, PG-17, PG-19 and their wait helpers), `LabelWire`, `TaskWire`, `DeleteWire`, `ClientStorageBanTests`, `SpaceSwitchCarryOverTests`, `SpaceDeleteTests`, `FakeYelloApi`, `RealHostBrowserTests`, `RealHostFixture`. Product read: `LabelStore`, `ProjectStore`, `Capability`, `RoleCapabilityMatrix`, the EF model snapshot and migrations (Membership and TaskLabel constraints), the route contracts, `ClientRoutes`, `ProjectRow.razor`, every `NavigateTo` call site.

**Knowledge:** `test-quality`, `data-factories`, `test-levels-framework` and `test-priorities-matrix`, as earlier runs used them. Where a fragment and `TESTING-CONVENTIONS.md` disagree, the repository document wins.

## Step 2 - Generation mode

**AI generation** from the story, its Dev Notes survey and the code. No recording. Because the story's server properties are believed to hold already, every scaffold was **run against the real engine or browser** to tell green from red rather than guess (Step 5).

## Step 3 - Test strategy

### Decisions taken

- **C1 (decided by Lee, 2026-10-31).** AD-3 stands. The lesser-Role rows (AC2's Viewer and Admin arms, I-10) assert `403` and `InsufficientRole`. AC7's `CrossSpace` classification is asserted only on the cross-Space cases: AC3's ApiToken arm and AC2's no-Membership arm.
- **Open questions, each at its recommended option** (the story leaves them undecided):

| Q | Assumed | Scaffolds that depend on it |
|---|---|---|
| Q1 (a) | AC1 is an SA2 id under an SA1 route, compared byte for byte with the in-Space not-found anchor **and** with B's answer for the same id under B's own Space (`SameAsAnchorAndStranger`) | `ISO-OWN-OTHER-SPACE-*` |
| Q2 (a) | "Client memory" = reachable managed state, proved by four layers; the heap scan is a time-boxed spike, held skipped | AC5 cases, the heap spike |
| Q3 (a) | A gate plus a vector: `IsolationVector.TokenOwnOtherSpace`; Session-only routes exempt by their own `NoCredential` row | `ISO-TOKEN-OWN-OTHER-SPACE-*`, the Q3 gate |
| Q4 (a) | Browser cases name their API counterpart rows in remarks; no cross-project gate | `SpaceSwitchMemoryTests`, `RealHostRefusalNoiseTests` |
| Q5 (a) | **Not the [LEE] option.** A foreign id under the caller's own Space writes no record (in-Space not-found, 2.5 precedent); no cross-Space read | `SameAsAnchorAndStranger` asserts nothing recorded, for the actor and for B |
| Q6 (a) | AC10's no-wait cases live in Slices as a partial of `BoardPagingTests`, beside PG-16/17/19; no timing collection (they wait on `LCK_M_%`) | `BoardPagingTests.CrossSpaceLocks.cs` |
| Q7 (a) | Forbid writable statics, static setters and `AddSingleton`; allow `static readonly` only for immutable types and `JsonSerializerOptions`; convert `RefusalCopy.OwnKeys` and the `PathEnds` fields | `ClientStaticStateGateTests` |
| Q8 (a) | An optional `Assumption` column on both row types, emitted as a per-row trait; read by reflection until it exists | the two AC14 facts |
| Q9 (a) | A helper file `SpaceNavigation.cs`; a source gate allows no other `NavigateTo` except named files with reasons | `SpaceNavigationGateTests` |
| Q10 (a) | The real-Host second Space is created through `POST /api/v1/spaces` | `RealHostRefusalNoiseTests` |

### Findings (record in dev-story)

1. **Every isolation row passes today: 50 rows, 100 surface runs, first run.** AC1, AC2, AC3, AC4 and AC9 measure properties the product already has, as the story predicted. They are left **active as standing regression gates**, not pending. Their red evidence is the dev phase's plants (1-5 in the story's table), which have not been run.
2. **All four AC10 cases are red for exactly the ledger's reason.** In each, the anchor (the holding Space's own request) queued first, then the other Space's request waited on the held key: `LCK_M_U` on `Project` (`ProjectStore.DeleteAsync`'s point lock), `LCK_M_U` on `Label` (`LabelStore.DeleteAsync`'s first read), `LCK_M_RS_U` on `Label` via `OPENJSON` (`TaskStore.ReplaceAsync`), and `LCK_M_RS_U` on `TaskLabel` (`LabelStore.DeleteAsync`'s `WHERE LabelId` range read). This is the only product change the story makes, and it is real.
3. **AC2's no-Membership arm needs no new rows.** A owns SA and holds no Membership in SB, so `ISO-WRITE-PROJECT`, `ISO-WRITE-TASK`, `ISO-CREATE-LABEL-FOREIGN-SPACE`, `ISO-EDIT-TASK-FOREIGN-SPACE`, `ISO-MOVE-TASK-FOREIGN-SPACE`, `ISO-RENAME-SPACE-CROSS`, `ISO-DELETION-SUMMARY-CROSS` and `ISO-DELETE-SPACE-CROSS` already are that arm, each a boundary `404` with one `CrossSpace` record. Duplicates were not written.
4. **AC3's Session surface writes for real.** A Session carries no Space (FR-2), so on the Session the Account's own SA2 request is served (`201`/`200`/`204`, and `409 label-in-use` for the applied Label). The "SA2 unchanged" `Then` check is on the ApiToken arm only. Each row uses a fresh Account, so the Session's writes touch nothing another row reads.
5. **The Project's Task count is visible only in its deletion confirm** (`ProjectRow.razor`). The AC5 Project-list case opens that confirm first, so the count `49` is on screen before the switch. The first draft anchored on the list and failed at its anchor; corrected.
6. **AC13's deletion exit is green today** (3.3's explicit `Navigating(null)`), so it stands as a gate. This does **not** answer AC13's question of whether the location-changing hook runs for a programmatic `NavigateTo`. That observation is still the dev phase's first AC13 step.
7. **AC13's Session-end case could not be triggered through the fake.** Clearing `SignedIn` does not make the fake answer `401` to Space-scoped reads, so no sign-in page appeared in 30 s. It stays skipped with that reason. The real-Host case `A_Session_ended_on_the_server_lands_the_client_on_sign_in_with_the_reason` is the model if the fake cannot be taught.
8. **AC12's mutable-collection gate is red on exactly the story's list:** `RefusalCopy.OwnKeys`, `ClientRoutes.PathEnds`, `SpaceArrival.PathEnds`, `ViewToggle.PathEnds`. `MovePicker.MorePositions` is not reported, so its declared type is not a mutable collection. The writable-static and singleton rules are green, and each detector fires on its plant.

### Container budget

**No new engine, fixture type or container-backed class.** Isolation rows run in `IsolationWorldFixture` and `PinnedPoolWorldFixture`. The AC10 cases are a partial of `BoardPagingTests` (its `MigratedDatabaseFixture`). The AC5/AC13 browser class joins `ClientSurfaceCollection` (fake API, no engine). The AC7 class joins `RealHostCollection`, whose collection fixture is shared, so it starts no second Host. No new package and no new project.

### Scenario map

| id | AC | Level | Pri | Scenario | State today |
|---|---|---|:--:|---|---|
| `ISO-OWN-OTHER-SPACE-{PROJECT-BOARD, PROJECT-LIST, TASK-READ, TASK-EDIT, TASK-DELETE, PROJECT-RENAME, PROJECT-DELETE, LABEL-DELETE, TASK-CREATE}` (9 x 2) | 1, 6 | ISO | P0 | SA2's id under SA1: `404 not-available`, the same bytes as the random-id anchor and as B for the same id; nothing recorded for either; neither Space changed | **green, standing** |
| `ISO-OWN-OTHER-SPACE-TASK-MOVE` (x2) | 1 | ISO | P0 | As above on the Session; Token `401` (Session-only route) | **green, standing** |
| `ISO-OWN-OTHER-SPACE-LABEL-APPLY` (x2) | 1 | ISO | P0 | SA2's Label in an SA1 Task's `labelIds`: `400 invalid-task-labels`, the same as a random id and as B's own Task | **green, standing** |
| `ISO-OWNER-ELSEWHERE-VIEWER-{CREATE-PROJECT, CREATE-TASK, CREATE-LABEL, EDIT-TASK, RENAME-SPACE, DELETION-SUMMARY, DELETE-SPACE}` (7 x 2) | 2, 7 (C1) | ISO | P0 | Owner of SA, Viewer in SB, with a Token issued in SB: `403` naming the capability, one `InsufficientRole`, no `CrossSpace`; SB unchanged | **green, standing** |
| `ISO-OWNER-ELSEWHERE-VIEWER-MOVE-TASK` (x2) | 2 | ISO | P0 | Session `403 manage-tasks`; Token `401` | **green, standing** |
| `ISO-OWNER-ELSEWHERE-ADMIN-{DELETION-SUMMARY, DELETE-SPACE}` (2 x 2) | 2 | ISO | P0 | Admin in SB: `403 delete-space` | **green, standing** |
| `ISO-OWNER-ROLE-NOT-CARRIED` (x2) | 2 (I-10) | ISO | P0 | SA create `201`, SB create `403`, SA, SB: two `InsufficientRole`, no `CrossSpace` | **green, standing** |
| `ISO-TOKEN-OWN-OTHER-SPACE-{ACTIVE, STATUSES, PROJECTS, PROJECT-CREATE, PROJECT-RENAME, PROJECT-DELETE, BOARD, BOARD-COLUMN, LIST, TASK, TASK-CREATE, TASK-EDIT, TASK-DELETE, LABELS, LABEL-CREATE, LABEL-DELETE, SPACE-RENAME, SPACE-DELETE, DELETION-SUMMARY}` (19 x 2) | 3, 7 | ISO | P0 | SA1's Token on SA2: the same bytes as B's Session, one `CrossSpace`, SA2 unchanged. The Session is served | **green, standing** |
| `ISO-TWO-SPACES-{STATUSES, PROJECTS, BOARD, BOARD-COLUMN, LIST, LABELS}` (6 x 2) | 9 | ISO | P0 | SA1's read holds exactly SA1's rows and totals (2 Tasks) and nothing of SA2's; SA2's own read holds SA2's (3 Tasks) | **green, standing** |
| `ISO-TWO-SPACES-SWITCHER`, `-TOKENS` (2 x 2) | 9 | ISO | P0 | Session: exactly `[SA1, SA2]` as `{id, name}`; each Token row names its own Space. Token `401` | **green, standing** |
| `ISO-POOLED-ONE-ACCOUNT-TWO-SPACES` (x2) | 4 | ISO (pooled) | P0 | Statuses, Projects, Labels alternated 5 times and then concurrently, each exactly its own; one Host session | **green, standing** |
| `IsolationCaseListTests.Every_Space_scoped_route_a_Token_reaches_has_a_row_with_a_Token_of_the_same_Account_bound_elsewhere` | 3 (Q3) | ISO gate | P0 | Every mapped `/api/v1/spaces/{spaceId}` route has a `TokenOwnOtherSpace` row or a Session-only row | **green, standing** |
| `IsolationCaseListTests.The_deletion_row_carries_its_assumption_as_a_trait_on_both_surfaces` | 14 | ISO gate | P1 | `IsolationCase.Assumption` = `PRD-12-2` on `ISO-DELETE-SPACE-OWN`; the trait on both generated rows, not on others | skipped `red: AC14`; **activated: no `Assumption` property** |
| `RevocationCaseTests.The_deletion_row_carries_its_assumption_as_a_trait_on_both_surfaces` | 14 | REV gate | P1 | The same for `REV-SPACE-DELETED-BY-OWNER` | skipped `red: AC14`; **activated: no `Assumption` property** |
| `BoardPagingTests.A_Label_deletion_naming_another_Spaces_Label_does_not_wait_on_that_Spaces_hold` | 10 | INT (race) | P0 | B's Label held; B's own deletion queues (anchor); A's deletion naming it answers `404` while held | skipped `red: AC10`; **activated: waited `LCK_M_U` on Label** |
| `BoardPagingTests.A_Label_deletion_does_not_wait_on_another_Spaces_held_Label_application` | 10 | INT (race) | P0 | B's application held (with AC10's applock); A deletes its own unapplied Label: `204` while held | skipped `red: AC10`; **activated: waited `LCK_M_RS_U` on TaskLabel** |
| `BoardPagingTests.A_Label_application_naming_another_Spaces_Label_does_not_wait_on_that_Spaces_hold` | 10 | INT (race) | P0 | B's Label held; A's Labels edit naming it answers `400 invalid-task-labels` while held | skipped `red: AC10`; **activated: waited `LCK_M_RS_U` on Label** |
| `BoardPagingTests.A_Project_deletion_naming_another_Spaces_Project_does_not_wait_on_that_Spaces_hold` | 10 | INT (race) | P0 | B's Project held; A's deletion naming it answers `404` while held | skipped `red: AC10`; **activated: waited `LCK_M_U` on Project** |
| `ClientStaticStateGateTests.No_client_type_holds_a_writable_static` | 12 | ARCH | P0 | No non-readonly static field, no static setter (compiler-generated excluded) | **green, standing** |
| `ClientStaticStateGateTests.No_client_service_is_registered_as_a_singleton` | 12 | ARCH (IL) | P0 | No `*Singleton*` registration call in `Yello.Client` | **green, standing** |
| `ClientStaticStateGateTests.No_client_static_readonly_field_holds_a_mutable_collection` | 12 | ARCH | P0 | Q7 (a) | skipped `red: AC12`; **activated: OwnKeys + three PathEnds** |
| `ClientStaticStateGateTests.The_detector_fires_on_each_planted_kind_of_shared_state` | 12 | ARCH | P0 | Each detector fires on a planted type and not on an immutable one | **green, standing** |
| `SpaceNavigationGateTests.Every_navigation_that_leaves_a_Space_goes_through_the_one_helper` | 13 | ARCH (source) | P0 | Only `SpaceNavigation.cs` and the named files call `NavigateTo` | skipped `red: AC13`; **activated: six files call it directly** |
| `SpaceNavigationGateTests.Every_allowed_file_still_navigates` | 13 | ARCH | P2 | No stale allow-list entry | **green, standing** |
| `SpaceSwitchMemoryTests.Nothing_of_a_departed_Board_survives_a_switch_in_the_DOM_its_counts_included` | 5 | E2E (fake) | P0 | Board held mounted, counts 13/17/19 seen, then held and after: no title, Label, Status, Project or count | **green, standing** |
| `SpaceSwitchMemoryTests.Nothing_of_a_departed_Project_list_survives_a_switch_its_Task_count_included` | 5 | E2E (fake) | P0 | Project deletion confirm open stating `49`; after the switch no `49`, no Project name | **green, standing** (anchor corrected, finding 5) |
| `SpaceSwitchMemoryTests.Nothing_of_a_departed_Spaces_Labels_survives_a_switch_from_its_settings` | 5 | E2E (fake) | P0 | From settings with Labels listed | **green, standing** |
| `SpaceSwitchMemoryTests.The_departed_Spaces_Task_titles_are_not_in_the_WebAssembly_heap_after_a_switch` | 5 (Q2) | E2E spike | P2 | UTF-16 heap scan, anchor before the switch. If kept, it moves to its own file with `Tier=Nightly` and `Explicit = true` | skipped `red: AC5` (spike) |
| `SpaceSwitchMemoryTests.Signing_out_purges_the_departed_Space_before_the_sign_in_page_renders` | 13 | E2E (fake) | P0 | No frame at `/sign-in` holds the departed Space | **green, standing** |
| `SpaceSwitchMemoryTests.Deleting_a_Space_purges_it_before_the_destination_renders` | 13 | E2E (fake) | P0 | No frame outside the deleted Space's routes holds its name or Labels | **green, standing** (finding 6) |
| `SpaceSwitchMemoryTests.A_Session_ending_purges_the_departed_Space_before_the_sign_in_page_renders` | 13 | E2E (fake) | P0 | Session end via a `401` | skipped `red: AC13`; **activated: no sign-in in 30 s (finding 7)** |
| `RealHostRefusalNoiseTests.Ordinary_use_of_two_owned_Spaces_records_no_refusal_and_one_probe_records_one` | 7 | E2E (real Host) | P0 | Home, Board, List, settings of both Spaces; switcher both ways; Back/Forward; deep link: 0 refusals. One `fetch` to B's Space: exactly 1 | **green, standing** |

### Priorities

P0 for every isolation, lock and purge case. P1 for the `Assumption` traits. P2 for the heap spike and the allow-list freshness gate.

### Duplicate-coverage guard - deliberately not written

- **AC2's no-Membership arm**: finding 3.
- **The direct form of AC1 with the Session** is the Owner's own Space, so a `200` is correct (FR-2). The direct form with the Token is AC3.
- **`ISO-TOKEN-BOUND-ELSEWHERE`** (SQL second Space, no Statuses) stays. `ISO-TOKEN-OWN-OTHER-SPACE-ACTIVE` is its product-created twin.
- **`ISO-POOLED`** (two Accounts) stays. The new pooled row is the one-Account twin.

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| AC11's plants (2.9's three rows, 3.2's eight absences, this story's own) | Plants are hand edits saved and restored in a `finally`, and they are the dev phase's evidence, not test code | Debug Log plant table |
| The story's minimum plant table (1-13) | The same reason. Finding 2 already supplies plant 6's red | Debug Log |
| AC13's observation of the location-changing hook for `NavigateTo`, with and without `replace` | It needs a temporary product probe or a browser trace and decides Q9; record it, do not leave a console line | Completion Notes |
| A hold "on an id at A's key edge" for the Label deletion | Finding 2's application case already shows the `TaskLabel` read crossing Spaces with no key-edge arrangement: the read names no Space and scans | Completion Notes |

## Step 4 - Scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents. Generation stayed in-thread, as every earlier run recorded, because C# under this analyser set has to be built and fixed here anyway. Build fixes on the way: S4457 (split `AddMembershipAsync`), SA1649 (file named for its first type), IDE0004/MA0181 (casts), S1067 (conditional count), S6605 (`Exists`), MA0190 (partial property for `GeneratedRegex`), MA0182/S1144/CS0649/CA1802 (the planted types), S2325, MA0204.

| File | Entries (cases) | Green, standing | Skipped (red) | ACs |
|---|---|---|---|---|
| `tests/Yello.Tests.Isolation/IsolationCases.SeveralSpaces.cs` (new partial, 50 rows; `IsolationCases` made `partial`, `All` spreads `SeveralSpacesCases()`) | 50 (100) | 50 (100) | 0 | AC1-AC4, AC6, AC7, AC9 |
| `tests/Yello.Tests.Isolation/SameAsAnchorAndStranger.cs` (new expectations: `SameAsAnchorAndStranger`, `EachOwnSpaceOnOneConnection`, `RoleNotCarriedAcrossSpaces`) | harness | - | - | AC1, AC2, AC4 |
| `tests/Yello.Tests.Isolation/IsolationCaseListTests.cs` (2 facts) | 2 (2) | 1 (Q3 gate) | 1 | AC3, AC14 |
| `tests/Yello.Tests.Revocation/RevocationCaseTests.cs` (1 fact) | 1 (1) | 0 | 1 | AC14 |
| `tests/Yello.Tests.Slices/Boards/BoardPagingTests.CrossSpaceLocks.cs` (new partial; `BoardPagingTests` made `partial`) | 4 (4) | 0 | 4 | AC10 |
| `tests/Yello.Tests.Architecture/ClientStaticStateGateTests.cs` (new, no fixture) | 4 (4) | 3 | 1 | AC12 |
| `tests/Yello.Tests.Architecture/SpaceNavigationGateTests.cs` (new, no fixture) | 2 (2) | 1 | 1 | AC13 |
| `tests/Yello.Tests.Client/SpaceSwitchMemoryTests.cs` (new, `ClientSurfaceCollection`) | 7 (7) | 5 | 2 | AC5, AC6, AC13 |
| `tests/Yello.Tests.Client/RealHostRefusalNoiseTests.cs` (new, `RealHostCollection`) | 1 (1) | 1 | 0 | AC6, AC7 |
| **Total** | **71 entries (121 cases)** | **111 cases** | **10** | |

### Harness added (test code, active)

- **`IsolationWorld.OwnerOfTwoAsync(label)`** (AC8): a fresh Account, its second Space created through `POST /api/v1/spaces` (so it has default Statuses), a Token per Space, and both Spaces populated through the product (one Project, a Label applied to the first Task, and 2 or 3 Tasks). Names are randomised. It returns two `SeededSpace` views.
- **`IsolationWorld.OwnerElsewhereAsync(role)`** (AC2): a fresh Owner, a second fresh Account's populated Space, and the Owner added there at `role` with a Token it issued there.
- **`SeededSpace`** record: the Account's view of the Space, the Project, the Tasks and the Label.
- **`ProductActor.InSpace(spaceId, name, token)`**, **`Origin`** and **`HoldsSecret`**: a view of the same Account from another of its Spaces. It shares the Session, the Token client and every secret list, so I-17 checks the second Space's name and Token too.
- **`WireDatabase.AddMembershipAsync(spaceId, accountId, roleStableCode)`** (AC8), beside `ChangeRoleAsync` and in the same style.
- **`CaseRequest`** gains optional `Other` (the Account's other Space) and `StrangerPaths` (Q1 (a)). **`IsolationVector.TokenOwnOtherSpace`** (Q3 (a)).

### Seed data and the constraints checked

- **Isolation.** Every Account, Space, Project, Task, Label, application and Token is written through the product. The one SQL write is `AddMembershipAsync`: an `INSERT dbo.[Membership]` under the target Space's own key, for an Account and a Space the product created. It satisfies `FK_Membership_Space_SpaceId` and `FK_Membership_Account_AccountId` (both rows exist), `AK_Membership_SpaceId_AccountId` (once per Space), `UX_Membership_SpaceId_Owner` (Owner refused by the helper), the `BLOCK AFTER INSERT` predicate `fn_SpaceIsolationPredicate(SpaceId)` (the key is set), and `Role nvarchar(20)` (stored by name, as `ChangeRoleAsync` stores it). The Viewer may issue its own Token (`IssueApiTokenForOneself`, every Role).
- **Slices.** Accounts, Projects, Tasks and Labels go through the product. The held side writes only under B's key and is always rolled back. It writes one `TaskLabel` row for B's own Task and Label, which satisfies `FK_TaskLabel_Task_SpaceId_TaskId`, `FK_TaskLabel_Label_SpaceId_LabelId` and the new key `(SpaceId, TaskId, LabelId)`. It also takes AC10's applock `Yello/LabelApplications/{spaceId:D}/{labelId:D}`, which no product code takes today. The other held reads are the product's own locking-read shapes.
- **Browser.** The fake's records are shaped as the contracts: column totals set on `FakeColumn`, and `ServeProjectTaskCounts` for the `49`. On the real Host, everything is written through the product.

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Active scaffolds run (2026-10-31)

- `Yello.Tests.Isolation` (whole project): 253 total, 252 passed, 0 failed, 1 skipped. This is every new row on both surfaces, the Q3 gate and the pooled row.
- `SpaceSwitchMemoryTests` and `RealHostRefusalNoiseTests`: every active case passed. The Project-list case passed after its anchor was corrected (finding 5).

### Skipped scaffolds activated against the product

The Skip attributes were removed by a script that held each original file in memory and restored it in a `finally`. Every restored file's SHA-256 matched its original. Each project was rebuilt and run with a filter.

| Scaffold (activated) | Failed with |
|---|---|
| AC10 Project deletion | B's own deletion queued (anchor); A then waited: `LCK_M_U` on `Project` (`SELECT ... WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = @p0`) |
| AC10 Label deletion naming B's Label | Anchor queued; A waited: `LCK_M_U` on `Label` |
| AC10 Label application naming B's Label | Anchor queued; A waited: `LCK_M_RS_U` on `Label` (`... WHERE Id IN (... OPENJSON(@p0))`) |
| AC10 Label deletion beside B's held application | Anchor queued; A waited: `LCK_M_RS_U` on `TaskLabel` (`... WITH (UPDLOCK, HOLDLOCK) WHERE LabelId = @p0`) |
| AC12 mutable collections | `RefusalCopy.OwnKeys`, `ClientRoutes.PathEnds`, `SpaceArrival.PathEnds`, `ViewToggle.PathEnds` |
| AC13 navigation gate | `SpaceArrival.razor:116`, `SpaceDeleteSection.razor:376`, `AccountSettings.razor:192`, `CreateSpace.razor:96, :133, :252`, `Home.razor:76, :133`, `SignIn.razor:309` |
| AC14 Isolation and Revocation | `Assert.NotNull()` - no `Assumption` property on either row type |
| AC13 Session end | Timeout waiting for `#password` (finding 7) |
| AC13 deletion exit | **Passed**, so it was un-skipped and left active (finding 6) |

### Full run

`dotnet test Yello.slnx --no-build --max-parallel-test-modules 1` reported 1,801 total, 1,781 passed, 1 failed and 19 skipped (20 m 36 s). The one failure was the heap spike's `Tier=Nightly` trait. `DeploymentShapeTests.Every_tiered_test_is_Explicit` requires every test in a file that carries the trait to be `Explicit = true`. The trait was removed from the skipped spike, and the remarks now say where a kept spike goes. After a rebuild (0 warnings, 0 errors), `Yello.Tests.Architecture` ran alone: 396 total, 394 passed, 0 failed, 2 skipped. The corrected totals are therefore 1,801 total, 1,782 passed, 0 failed and 19 skipped: the baseline's 1,680 plus 121 new, and the baseline's 9 skips plus 10 red scaffolds.

### Red-phase compliance

- Every red scaffold is skipped with `red: AC<n> - <why> (story 3.4 Task n)`.
- **No isolation row is pending.** All 50 rows pass today, so they stand as regression gates (finding 1), and GATE-003 has nothing to convert.
- Every scaffold compiles under warnings-as-errors. No scaffold references a type 3.4 has not written: the `Assumption` column is read by reflection, and the helper file and the applock are matched by name.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

1. **AC10 first, because it is the riskiest assumption and the only product change.** Start with `A_Label_deletion_does_not_wait_on_another_Spaces_held_Label_application`. The `TaskLabel` range read is the ledger's core finding, and its fix (a Space-named applock taken by both the deletion and `TaskStore.ReplaceAsync`) must keep 2.4's rule that a Label in use cannot be deleted. Then the Label application, the Label deletion and the Project deletion cases. After each fix, keep `LabelWire`'s races, PG-16, PG-17 and PG-19 green. Plant 6: restore each old read and see its case go red again.
2. **AC12:** convert `OwnKeys` to `FrozenDictionary` and the `PathEnds` fields to `SearchValues<char>` or `string`, then un-skip `No_client_static_readonly_field_holds_a_mutable_collection`. Plant 11 against the real client: a `static List<string>` in a client service, and an `AddSingleton`.
3. **AC13:** first make the hook observation (Q9). Then add `SpaceNavigation.cs` (or apply Q9 (b) and widen the allow-list with reasons), and un-skip the navigation gate. Fix the fake's Session-end trigger, or move that case to the real Host, and un-skip it. Plant 12 on the deletion and sign-out cases.
4. **AC14:** add the `Assumption` column and the per-row trait, un-skip both facts, verify `--filter "Assumption~PRD-12"`, and extend `TestingConventionTests`. Use Q8 (b) if the runner does not filter on row traits.
5. **AC11 and the standing gates' plants:** plants 1-5, 7-10 and 13 against the green rows and browser cases, each recorded `| Plant | Expected red | Seen |`.
6. **AC5's heap spike** (time-boxed, half a day at most): un-skip it if the signal is reliable 3/3; otherwise record why and ledger it to Lee.

### Commands

```
dotnet build Yello.slnx
dotnet build-server shutdown
dotnet test Yello.slnx --no-build --max-parallel-test-modules 1
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.Boards.BoardPagingTests
dotnet test --project tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj --no-build
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.ClientStaticStateGateTests
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.SpaceSwitchMemoryTests
```

Never run two `dotnet test` processes at once. `--no-build` runs whatever was last built. Build `Yello.Client` in Debug before any browser run or plant.

## Notes and risks

- **The isolation run is longer.** 100 new surface runs, each registering one or two Accounts: the Isolation project took about 2 minutes in all.
- **`AssertSeededSpaceUnchangedAsync` assumes the seed's shape** (one Project, Tasks in one Status, one Label applied once). A future row that seeds more must use its own check.
- **`ISO-TWO-SPACES-SWITCHER` asserts exactly two Spaces.** It uses a fresh Account, so 3.1's creation rows cannot change it.

---

**Generated by BMad TEA Agent** - 2026-10-31
