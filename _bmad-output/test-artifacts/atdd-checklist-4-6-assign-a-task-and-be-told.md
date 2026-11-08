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
lastSaved: '2026-11-08'
workflowType: 'testarch-atdd'
storyId: '4.6'
storyKey: '4-6-assign-a-task-and-be-told'
storyFile: '_bmad-output/implementation-artifacts/4-6-assign-a-task-and-be-told.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-4-6-assign-a-task-and-be-told.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the API and browser scaffolds share AssignmentWire and one build, so both were written in the main run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AssignTask.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AssignTaskRace.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.AssignSchema.cs'
  - 'tests/Yello.Tests.Slices/Lists/ListPagingTests.Assignee.cs'
  - 'tests/Yello.Tests.Slices/Memberships/MembershipLabelsTests.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.Assignment.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Assignment.cs'
  - 'tests/Yello.Tests.Architecture/ListViewGateTests.cs'
  - 'tests/Yello.Tests.Client/AssigneeTests.cs'
  - 'tests/Yello.Tests.Client/MembershipSectionTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.Assignees.cs'
  - 'tests/Yello.Tests.Shared/AssignmentWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/4-6-assign-a-task-and-be-told.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-4-5-remove-a-membership-or-leave-a-space.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 4, Story 4.6: Assign a Task, and be told

## Story summary

As someone dividing up work, I want to give a Task to someone in this Space and have them know. Requirements: **FR-21**,
**FR-40**, **FR-30** (the Assignee dimension, completing it), **AR-8/AD-2**, **FS-NFR-2**, with AD-5, AD-10/AR-33, AD-18,
AD-19, AD-23, AD-29, UX-DR8, UX-DR11, UX-DR19, UX-DR27, UX-DR31, UX-DR34, UX-DR38 binding and test-design rows I-14, I-15,
P-3, P-4. Two new routes: `PUT /api/v1/spaces/{spaceId}/tasks/{taskId}/assignee` under `AssignTask` answering `200` with
the Task, and `GET /api/v1/spaces/{spaceId}/assignees` under `ReadProjectsAndTasks`. A composite NO ACTION key
`(SpaceId, AssigneeMembershipId) → Membership(SpaceId, Id)`, an assignment email through the outbox, a Space-scoped
`NotificationSend` record, the unassignment inside removal and leaving, and the List View's Assignee filter and sort.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority. Docker up (server 29.5.3).
- **Story:** `ready-for-dev`, AC1-AC14 with cited sources. **Q1, Q4, Q7, Q10 decided by Lee (2026-11-08), all (a)**, recorded
  under Task 0. Q2, Q3, Q5, Q6, Q8, Q9, Q11 scaffolded at their recommendations (a).
- **No progress file existed** for this story (`atdd-checklist-4-6-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`.
- **Knowledge:** core (data-factories, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** the `ActiveSpaceEndpointTests` partials (`SignedInAsync`, `RegisteredAndSignedInAsync`,
  `JoinedThroughTheProductAsync`, `SeedNamedSpace`'s `NamedSpace`, `ChangeRoleOverWireAsync`, `RemoveOverWireAsync`,
  `LeaveOverWireAsync`, `DeleteSpaceOverWireAsync`, `IssueAsync`, `BearerForAsync`, `PlantFailingIdempotencyTriggerAsync`,
  `DropTriggerAsync`, `RefusalsOfAsync`, `CapabilityOf`, `AssertOneOwnerEachAsync`, `MadePresentAsync`,
  `TakeApplicationLockAsync`, `MembershipsLockOf`, `RemoveUncommittedAsync`), `TaskWire`, `TaskEditWire`, `LabelWire`,
  `ListWire`, `WireDatabase`, `MailDrop`, `SpaceSeeding`, the isolation world (`OwnerOfTwoAsync`, the 4.4
  `JoinedThroughTheProductAsync`, `TokenOwnOtherSpace`), and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the picker, the avatar, the List's
Assignee controls and the routes do not exist, so every locator and path is an assumption written from AC1-AC14 and the
Q answers, named as such in each file's remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| **Q1 [LEE]** | (a) own `PUT .../assignee` under `AssignTask`, `200 TaskResponse`; PATCH unchanged | every assignment send; routes gate; `AssignTask_is_required_by_exactly_the_assignment_route`; Viewer `403 assign-task`; isolation rows |
| **Q4 [LEE]** | (a) Space-scoped `NotificationSend(Id, SpaceId, Kind, RecordedAt)` written at enqueue, every kind; cascade with the Space | send-record wire case; rollback case; replay case; schema case (columns, three predicates, `Space|CASCADE`); no-reader gate |
| **Q7 [LEE]** | (a) NFC, strip `Cf`, trim, collapse whitespace, ordinal-ignore-case; confusables out | `MembershipLabelsTests` look-alike theory; client picker and Members-list cases |
| **Q10 [LEE]** | (a) no Invitation rate limit here | none (ledger re-own is the dev phase's) |
| Q2 | (a) `GET .../assignees` under `ReadProjectsAndTasks`, `{assignees:[{membershipId, displayName}]}`, unpaged, join order | candidates read case (every Role); contract gate; isolation `ISO-ASSIGNEES-*`; client cases |
| Q3 | (a) Shared applock for assignment, Exclusive for removal/leave/Role change | races 1-3 |
| Q5 | (a) explicit unassign in removal and leave before the `DELETE` | removal, leave, refused-removal cases; race 2 |
| Q6 | (a) sort by `AssigneeMembershipId`, Unassigned last, `TaskId` tiebreak; `unassigned` flag | `ListPagingTests.Assignee.cs`; `ListViewGateTests`; schema index case; `ISO-LIST-FOREIGN-ASSIGNEE-FILTER`; client List cases |
| Q8 | (a) email names assigner, Space, Project, Task; base URL, no ids; subject "{Assigner} assigned you a Task in {Space}" | I-14 case; template gate (strings and `Uri`, five parameters) |
| Q9 | (a) email only on a change to another Account's Membership | self, no-op, reassign, unassign, replay, removal cases |
| Q11 | (a) the copy list | client cases assert "Assignee", "Unassigned", "Assigned to {label}.", "That person is no longer in this Space." |

### New assumptions this run made (no question covered them)

1. **Temporary stand-ins in `AssignmentWire`** (`tests/Yello.Tests.Shared`), the 4.4/4.5 precedent: `AssigneeRoute`,
   `AssigneeOf`, `AssigneesRoute`, `AssigneesOf`, `InvalidAssigneeProblemType`, `SortAssignee`, `AssigneeParameter`,
   `UnassignedParameter`. Develop swaps every use (Slices, Isolation, the fake) for the contract members and deletes them.
   `AssignCapabilityCode` (`assign-task`, which `StableCode.Of(Capability.AssignTask)` yields), `TaskAssignedKind`,
   `InvitationIssuedKind`, `ListMembers`, `CandidateMembers`, `ParseCandidates`, `IsAssignmentEmail` are lasting readings.
2. **The wire cases live in `ActiveSpaceEndpointTests`, not a new `TaskEndpointTests.Assign.cs`** as the story's Testing
   requirements suggest: every case that seeds a Role through the product needs `JoinedThroughTheProductAsync` and the mail
   helpers, which are private to that class. The removal and leave unassignment cases sit in the same partial rather than in
   `RemoveMembership.cs` / `LeaveSpace.cs`. No new class either way.
3. **The look-alike unit cases are in `Yello.Tests.Slices/Memberships/MembershipLabelsTests.cs`**, where 4.4 put
   `MembershipLabels`' cases (the type is in Contracts, which Slices sees), not in the Architecture project as AC6 says.
4. **An email Assignee is seeded directly** (`WireDatabase.AddMembershipAsync`) so its address holds no Invitation email;
   where a joined Account is emailed, the assignment email is told apart by its subject (`AssignmentWire.IsAssignmentEmail`,
   "assigned you a Task"). `MailDrop` gained `WaitForEmailsToAsync(recipient, matching[, count])` for that.
5. **Absence of an email is fenced**: each "no email" case waits for a later, unrelated assignment email first, so a late
   dispatch cannot hide behind a quick read.
6. **Client locators** (`AssigneeTests`): a button named "Assignee" opening a `[role=listbox]`, `.assignee-avatar` with
   `aria-label` "Assigned to {label}", a `dt`/`dd` "Assignee" for the Viewer, a fieldset named "Assignee", sort option
   "Assignee", column header "Assignee". Correct here first if the markup differs, and record it.
7. **`ListPagingTests` was made `partial`** (no other change) so the Assignee cases sit beside 2.10's.

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ActiveSpaceEndpointTests`,
`TaskEndpointTests` and `ListPagingTests`; `MembershipLabelsTests` takes no database; isolation rows ride the boundary
world; `AssigneeTests` is a browser class on the assembly's existing `ClientSurfaceFixture` and `FakeYelloApi` (no SQL
Server). Engines on a full run: unchanged.

### Scenario map

| AC | Level | Scaffold | Priority |
|---|---|---|---|
| AC1 | Wire | `An_Owner_an_Admin_or_a_Member_assigns_a_Task_to_a_Member_of_the_Space` (x3) | P0 |
| AC1 (AD-4) | Wire | `A_Token_assigns_without_anti_forgery_and_null_unassigns` | P0 |
| AC1 | Wire | `A_body_that_is_not_an_object_or_names_no_Membership_is_400_invalid_assignee` (x4) | P1 |
| AC1 (I-13, I-15) | Wire | `A_random_or_another_Spaces_Membership_id_is_the_same_400_invalid_assignee_and_nothing_is_stored` | P0 |
| AC1, AC6 (Q2) | Wire | `Every_Role_reads_the_candidates_as_names_and_Membership_ids_only` (x4) | P0 |
| AC3 | Wire | `A_Viewer_may_be_assigned_and_gains_no_write_over_the_Task` (x3) | P0 |
| AC3 | Wire | `Demoting_an_Assignee_to_Viewer_keeps_every_assignment` | P1 |
| AC4 (I-14) | Wire | `The_assignment_email_names_this_Space_Project_and_Task_and_nothing_of_another_Space` | P0 |
| AC4 (Q9) | Wire | `Assigning_oneself_sends_no_email_and_an_assignment_by_someone_else_does` | P0 |
| AC4 (Q9) | Wire | `Only_a_change_to_another_Accounts_Membership_sends_an_email` | P1 |
| AC4, AC5 (AD-10) | Wire | `A_failure_after_the_enqueue_rolls_back_the_assignment_its_email_and_its_send_record` | P0 |
| AC5 (Q4) | Wire | `An_Invitation_and_an_assignment_each_leave_one_send_record_in_their_Space` | P0 |
| AC9 (AD-18) | Wire | `A_replayed_assignment_answers_its_original_200_and_sends_and_records_nothing_more` | P1 |
| AC8 (Q5) | Wire | `An_assigned_Member_is_removed_and_its_Tasks_are_unassigned_and_survive` | P0 |
| AC8 (Q5) | Wire | `An_assigned_Member_leaves_and_its_Tasks_are_unassigned_and_survive` | P0 |
| AC8 | Wire | `A_refused_removal_leaves_the_targets_Tasks_assigned` | P1 |
| AC2 | Wire | `Deleting_a_Space_that_holds_an_assigned_Task_answers_204_and_leaves_no_row` | P0 |
| AC9 race 1 | Wire (race) | `An_assignment_queued_behind_the_removal_of_its_target_is_400_invalid_assignee_and_sends_nothing` | P0 |
| AC8, AC9 race 2 | Wire (race) | `A_removal_queued_behind_an_assignment_of_its_target_unassigns_and_answers_204` | P0 |
| AC9 race 3 | Wire (race) | `Two_assignments_in_one_Space_do_not_wait_on_each_other` | P1 |
| AC2 | Wire (schema) | `The_Assignee_is_a_composite_key_carrying_SpaceId_to_Membership_with_no_action`, `The_database_refuses_an_Assignee_from_another_Space` | P0 |
| AC7 | Wire (schema) | `The_Assignee_sort_has_its_composite_index` | P0 |
| AC5 | Wire (schema) | `The_send_record_holds_only_the_Space_the_kind_and_the_time_under_the_Space_policy` | P0 |
| AC7 (P-3) | Wire | `Sorted_by_Assignee_each_persons_Tasks_are_grouped_Unassigned_last_and_none_skipped_or_repeated` | P0 |
| AC7 | Wire | `Filtering_by_an_Assignee_and_Unassigned_serves_exactly_their_Tasks_and_their_true_total` | P0 |
| AC7 (NFR-5) | Wire | `The_last_page_of_the_Assignee_sort_reads_no_more_than_its_first_at_5000_Tasks` (x2) | P0 |
| AC7 (P-4) | Wire | `The_Assignee_sort_seeks_its_own_composite_index_and_never_converts_its_column` | P0 |
| AC6, AC14 (Q7) | Unit | `Look_alike_names_collide_and_both_get_a_discriminator` (x6), `A_folded_collision_keeps_each_name_as_written`, `Different_names_that_share_a_prefix_do_not_collide` (**active**) | P1 |
| AC12 | Isolation | 9 rows, `PendingUntil: "4.6"` (below) | P0 |
| AC1, AC13 | Architecture | 10 gates in `RefusalBoundaryGateTests.Assignment.cs` | P0 |
| AC7 | Architecture | `The_offered_sorts_are_exactly_four_and_the_Assignee_is_among_them`, `The_Assignee_sort_has_its_composite_index_in_the_model` | P0 |
| AC6, AC10, AC11, AC7 | Browser | `AssigneeTests`, 9 methods | P0 |
| AC14 | Browser | `MembershipSectionTests.Look_alike_names_in_the_Members_list_each_carry_a_discriminator` | P1 |

Isolation rows (`IsolationCases.Assignment.cs`, spread from `MembershipCases()`): `ISO-ASSIGNEES-FOREIGN-SPACE`,
`ISO-ASSIGN-FOREIGN-SPACE`, `ISO-ASSIGN-FOREIGN-MEMBERSHIP` (I-15, `SameAsAnchor(400, invalid-assignee)`),
`ISO-ASSIGN-FOREIGN-TASK-IN-OWN-SPACE`, `ISO-ASSIGNEES-ONLY-THIS-SPACE` (I-15 aggregate), `ISO-LIST-FOREIGN-ASSIGNEE-FILTER`,
`ISO-RECORD-KIND-ASSIGN-VIEWER` (`RoleRefused("assign-task")`), `ISO-TOKEN-OWN-OTHER-SPACE-ASSIGN`,
`ISO-TOKEN-OWN-OTHER-SPACE-ASSIGNEES`.

Not scaffolded here, by design: `AssignTaskHandlerTests` (unit, `RecordingOutbox`) cannot compile before Task 3 declares
the handler; the dev phase writes it from the `IssueInvitationHandlerTests` shape. The Role change "writes no Task"
statement-level check is the dev phase's plant 10 against the demotion case. I-14 on the isolation suite is recorded as
proved in Slices (its world shares one Host and one mail drop). No `IsolationTimingTests` row (reason in the partial's
remarks). The 19-row plant table is the dev phase's.

## Step 4 - Generated scaffolds (TDD red phase)

Every scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; every isolation row is
`PendingUntil: "4.6"` with a reason. Execution mode resolved to **sequential**.

| File | Methods / rows | Cases when active | ACs |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AssignTask.cs` | 17 (all skipped) | 27 | AC1-AC5, AC8, AC9 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AssignTaskRace.cs` | 3 (all skipped) | 3 | AC8, AC9 |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.AssignSchema.cs` | 4 (all skipped) | 4 | AC2, AC5, AC7 |
| `tests/Yello.Tests.Slices/Lists/ListPagingTests.Assignee.cs` | 4 (all skipped) | 5 | AC7 |
| `tests/Yello.Tests.Slices/Memberships/MembershipLabelsTests.cs` (three added) | 3 (2 skipped, 1 active) | 8 | AC6, AC14 |
| `tests/Yello.Tests.Isolation/IsolationCases.Assignment.cs` | 9 rows, `PendingUntil: "4.6"` | 18 (0 generated while pending) | AC1, AC7, AC12 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Assignment.cs` | 10 (all skipped) | 10 | AC1, AC2, AC4-AC6, AC10, AC13 |
| `tests/Yello.Tests.Architecture/ListViewGateTests.cs` (two added) | 2 (all skipped) | 2 | AC7 |
| `tests/Yello.Tests.Client/AssigneeTests.cs` (new class, existing assembly fixtures) | 9 (all skipped) | 9 | AC6, AC7, AC10, AC11 |
| `tests/Yello.Tests.Client/MembershipSectionTests.cs` (one added) | 1 (skipped) | 1 | AC14 |

Supporting (no cases): `tests/Yello.Tests.Shared/AssignmentWire.cs` (stand-ins and wire readings);
`WireDatabase.AssigneeOfAsync`, `AssigneesInAsync`, `NotificationSendsInAsync`, `NotificationSendsVisibleUnderAsync`;
`MailDrop.WaitForEmailsToAsync`; `tests/Yello.Tests.Client/FakeYelloApi.Assignees.cs` (the candidates read and the assignment
as the Host will answer them, with hold and refusal hooks); `FakeTask.AssigneeMembershipId` (appended, omitted when null, so
every earlier case is served the same bytes); `FakeListQuery` reads `assignee` / `unassigned` and the fake List sorts
`assignee`; the fake's CORS answer allows `PUT` (the Host's `CrossOriginPolicy` already allows any method).

### Discovery runs (no full run, per the phase's policy)

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Slices, `--filter-method` on the 31 new methods | 31 | 30 | 1 | 0 |
| - of which `ActiveSpaceEndpointTests` (AssignTask, AssignTaskRace) | 20 | 20 | 0 | 0 |
| - of which `TaskEndpointTests` (AssignSchema) | 4 | 4 | 0 | 0 |
| - of which `ListPagingTests` (Assignee) | 4 | 4 | 0 | 0 |
| - of which `MembershipLabelsTests` | 3 | 2 | 1 | 0 |
| Isolation, `IsolationCaseListTests` (GATE-001..003, route coverage) | 16 | 0 | 16 | 0 |
| Architecture, whole suite | 469 | 12 (the new gates) | 457 | 0 |
| Client, `AssigneeTests` | 9 | 9 | 0 | 0 |
| Client, the new `MembershipSectionTests` method | 1 | 1 | 0 | 0 |

### Green today, left active (standing regression gate)

| Gate | Why it is green now |
|---|---|
| `MembershipLabelsTests.Different_names_that_share_a_prefix_do_not_collide` | "Ann" and "Anne" never collide under today's key or the folded one; it keeps the widened key from over-reaching |

### Red verified (temporary unskip, restored in `finally`; never `git checkout`)

| Scaffold | Failed with | Reason matches |
|---|---|---|
| `RefusalBoundaryGateTests.The_assignment_routes_declare_one_capability_each_and_serve_both_surfaces` | "Collections differ ... Actual: []" | yes - neither route is mapped |
| `ActiveSpaceEndpointTests.A_Token_assigns_without_anti_forgery_and_null_unassigns` | `SqlException: Invalid column name 'AssigneeMembershipId'` | yes, after correction - the case reads the column before it asserts the status, so the schema's absence shows first. The shared skip reason now names both (route unmapped and column absent). Seeding through registration, a direct Membership and the product's Project and Task create all worked. |

### Seed data against the real schema

- `FK_Membership_Account_AccountId` (Restrict): every Membership names an Account registered through the product first
  (`SignedInAsync`, `RegisterAsync`); no Account is inserted by hand.
- `AK_Membership_SpaceId_AccountId`: one Membership per Account per Space; the I-14 recipient's second Membership is in a
  different Space.
- `UX_Membership_SpaceId_Owner`: one Owner per seeded Space; every other Membership is `member`, `admin` or `viewer`.
- `FK_Task_Project_SpaceId_ProjectId`, the `(SpaceId, StatusId)` key and `IX_Task_SpaceId_ProjectId_StatusId_PositionKey`:
  every wire-case Task is created through the product, in a Space seeded with one Status (`SeedAssignableSpaceAsync`,
  because a seeded Space carries none); the 5,000-row seed is `ListWire.SeedListAsync`, whose remarks list its constraints.
- The Assignee key `(SpaceId, AssigneeMembershipId) → Membership(SpaceId, Id)` (Task 4): the only hand-written assignments -
  the race holder's `UPDATE` and `SeedAssignedAsync` - run under the Task's Space key and name Memberships of that same
  Space, so the key and BLOCK AFTER UPDATE admit them; `The_database_refuses_an_Assignee_from_another_Space` is the one
  deliberate violation (547 expected).
- `IdempotencyRecord` trigger: the forced-rollback trigger fires only for the case's own key and is dropped in a `finally`.

### Findings for the dev phase

1. **Status codes checked against the existing endpoints, no discrepancy found.** The PATCH edit answers `200` with the
   Task (the new `PUT` follows it); the Invitation issue answers `204` (`An_Invitation_and_an_assignment_...` asserts `204`,
   not `201`); removal and leave `204`; Space deletion `204`; accept `200`; Role change `200`; reused key `422`; malformed
   key `400`. The story's own codes for the new routes (`200`, `400 invalid-assignee`, the in-Space `404`, `403`) are what
   the scaffolds assert.
2. **Active tests that go red by design when the Assignee lands** - amend them in the same commit, do not skip them:
   - `TaskResponse` member pins: `TaskEndpointTests.TaskProperties`, `TaskEndpointTests.Edit.cs` `EditedTaskProperties`,
     `BoardEndpointTests.TaskProperties`, `BoardPagingTests` (`:245`), `ListPagingTests.TaskProperties`, and the Architecture
     gates `The_due_date_is_a_nullable_DateTimeOffset_on_the_Task_and_its_response` and
     `The_Task_response_gains_its_Label_ids_and_keeps_every_earlier_field` - each gains `assigneeMembershipId` /
     `AssigneeMembershipId` (the Host writes `null` members, as `dueDate` shows).
   - `ListViewGateTests.The_offered_sorts_are_exactly_three_and_none_is_the_Assignee` - replaced by the new four-sort gate.
   - `ListPagingTests` LV-10 `[InlineData("sort=assignee")]` (now `200`) and the cursor case at `:572`.
   - `MembershipLabelsTests.Names_are_compared_ordinally_after_NFC` asserts "Sam Lee" and "sam lee" are two names. **Lee's
     Q7 (a) reverses that** (case now folds). Rewrite it to assert the composed and decomposed "José" still collide and
     drop the case clause, which the new look-alike theory now holds the other way.
   - `ActiveSpaceEndpointTests.What_the_removed_Member_created_survives_the_removal` remarks ("No Task carries an Assignee
     yet") - Task 8's comment rewrite.
3. **The isolation route-coverage gate** fails as soon as either route is mapped until the nine rows are live; swap the
   `AssignmentWire` stand-ins for the contract members when you activate them.
4. **xUnit filters of different kinds AND together** (`--filter-class X --filter-method Y` matches nothing); run them
   separately.

## Activation order (riskiest assumption first)

Recorded in the story's Dev Notes, "ATDD artifacts - red-phase scaffolds already in the tree". First:
`A_removal_queued_behind_an_assignment_of_its_target_unassigns_and_answers_204` (race 2) - it tests the riskiest
assumption, that a NO ACTION composite key plus an explicit unassign under the Memberships lock (Exclusive for removal,
Shared for assignment) lets a removal never fail `547`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Implementation guidance

- **Contracts:** `TaskRoutes.Assignee` / `AssigneeOf`; `AssignTaskRequest(Guid? MembershipId)`; `TaskResponse.AssigneeMembershipId`
  (appended); `AssigneeRoutes.List` / `ListOf`; `AssigneeListResponse`, `AssigneeResponse(MembershipId, DisplayName)`;
  `ProblemTypes.InvalidAssignee`; `ListSorts.Assignee`; `ListRoutes` `assignee` / `unassigned`; the widened
  `MembershipLabels` key. Then swap the `AssignmentWire` stand-ins and delete them.
- **Domain / Application:** `TaskItem.AssigneeMembershipId`; the assign port and outcome; `AssignTaskHandler`,
  `AssignmentEmail.Render` (strings and `Uri`, five parameters), `OutboxKinds.TaskAssigned`; `ListAssigneesHandler`;
  `ViewListHandler` narrowing of Assignee ids.
- **Infrastructure:** the composite NO ACTION key and the `(SpaceId, ProjectId, AssigneeMembershipId, Id)` index;
  `NotificationSend` with its policy, query filter and Space cascade, written by `OutboxStore` before its save;
  `ApplicationLocks` Shared mode; the guarded `UPDATE ... OUTPUT`; the unassign in `RemoveCoreAsync` and `LeaveAsync`;
  `ListSortColumns`, `ListReader.ByAssigneeAsync`, `Matching`; one migration `TaskAssignee`.
- **Host:** `PUT` assignee endpoint (`AssignTask`, explicit outcome switch, no `_ =>` arm), `GET` assignees endpoint
  (`ReadProjectsAndTasks`), `ListQueryReading` parameters.
- **Client:** the Assignee attribute and picker in `TaskDetail`, `AssigneeAvatar`, the card's `CardInputs`, `BoardView`'s
  `TaskAttribute.Assignee` and its own send, a per-Space candidates holder purged on switch, `TaskCapabilities.PermitsAssignTask`,
  the List's fieldset, sort option and column, `RefusalCopy` and `ClientCopy.resx` entries.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs and Lee's Q1, Q4, Q7, Q10 decisions; framework; Docker up).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC14 each mapped above.
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 4.6 with a reason; the one
      green case is left active and named.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (constraints listed above).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open; the temporary red-check script was deleted, and both files it touched were restored from memory in a `finally`.
- [x] No full run (this phase's policy); discovery runs above.

**Next workflow:** `bmad-dev-story` for 4.6, then `bmad-code-review`.
