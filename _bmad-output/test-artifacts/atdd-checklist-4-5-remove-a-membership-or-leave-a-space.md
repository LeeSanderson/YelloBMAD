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
lastSaved: '2026-11-07'
workflowType: 'testarch-atdd'
storyId: '4.5'
storyKey: '4-5-remove-a-membership-or-leave-a-space'
storyFile: '_bmad-output/implementation-artifacts/4-5-remove-a-membership-or-leave-a-space.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-4-5-remove-a-membership-or-leave-a-space.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the API and browser scaffolds share MembershipWire and one build, so both were written in the main run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RemoveMembership.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.LeaveSpace.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RemoveMembershipRace.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs'
  - 'tests/Yello.Tests.Revocation/RevocationCases.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.RemoveMembership.cs'
  - 'tests/Yello.Tests.Client/MembershipSectionTests.cs'
  - 'tests/Yello.Tests.Client/LeaveSpaceTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.Memberships.cs'
  - 'tests/Yello.Tests.Shared/MembershipWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/4-5-remove-a-membership-or-leave-a-space.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-4-4-manage-memberships-and-roles.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 4, Story 4.5: Remove a Membership, or leave a Space

## Story summary

As an Owner or Admin ending a working relationship, or as anyone who wants out, I want access to end immediately and
completely. Requirements: **FR-14**, AR-16/AD-9, UX-DR36, with V-2 and V-3 (`test-design-qa.md:281-282`) and NFR-2, AD-3,
AD-4, AD-6, AD-18, AD-20, AD-23, AD-27, UX-DR14, UX-DR27, UX-DR38, UX-DR39 binding. Three new routes: the removal
`DELETE /api/v1/spaces/{spaceId}/memberships/{membershipId}` under `RemoveMembership` (Owner Allowed, Admin Qualified), the
leave `DELETE /api/v1/spaces/{spaceId}/membership` under `ReadProjectsAndTasks` (identity-decided), and the single-row read
`GET .../memberships/{membershipId}` under `ChangeMembershipRole` carrying `InSpaceNow`. Client: a Remove control and its
in-place confirm in the Members section, a "Leave this Space" control on Space home, and the two client ledger items the
story owns (`deferred-work.md:789`, `:791`).

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority. Docker up (server 29.5.3).
- **Story:** `ready-for-dev`, AC1-AC11 with cited sources. **Q1, Q2, Q8, Q12 decided by Lee (2026-11-07), all (a)**, recorded
  under Task 0. Q3-Q7, Q9-Q11, Q13-Q16 scaffolded at their recommendations (a).
- **No progress file existed** for this story (`atdd-checklist-4-5-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`.
- **Knowledge:** core (data-factories, component-tdd, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** the `ActiveSpaceEndpointTests` partials (`JoinedThroughTheProductAsync`, `ChangeRoleOverWireAsync`,
  `AssertOneOwnerEachAsync`, `CapabilityOf`, `PublishesNaming`, `CreateProjectOverWireAsync`, `InviteIntoFullSpaceAsync`,
  `MembershipsInAsync`, `RecordPresenceAsync`, `TakeApplicationLockAsync`, `MembershipsLockOf`, `DemoteUncommittedAsync`,
  `BearerForAsync`, `IssueAsync`, `AcceptAsync`, `RefusalsOfAsync`), `TaskWire.WaitUntilBlockedOnALockAsync`, `WireDatabase`,
  `SpaceSeeding`, the isolation world (`OwnerOfTwoAsync`, `ForeignAsync`, the 4.4 `JoinedThroughTheProductAsync`), the
  revocation world (`Mail`, `RevocationCase.Skip`), `FakeYelloApi` and `RefusalFingerprint`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the Remove control, its confirm and
the Leave control do not exist, so every locator is an assumption written from AC6, AC8, Q16 (a) and the mockup.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| **Q1 [LEE]** | (a) leave route under `ReadProjectsAndTasks`, identity-decided, one write under it; Token parity | leave cases; `The_leave_route_is_the_one_write_under_ReadProjectsAndTasks`; routes gate; `ISO-LEAVE-FOREIGN-SPACE`, `ISO-TOKEN-OWN-OTHER-SPACE-LEAVE`; `REV-LEFT-THE-SPACE`; the Token leave case |
| **Q2 [LEE]** | (a) `RemovalAuthority(Removable, HeldBy)`, Owner in no set; self-target (a): own non-Owner row removable on `{id}` | Admin-beyond theory; Admin own-row `204`; race 2; `The_removal_rules_agree_with_the_matrix_transcription`; `ISO-REMOVE-MEMBERSHIP-ADMIN-BEYOND-AUTHORITY`; client agreement gate |
| **Q8 [LEE]** | (a) "Leave this Space" at the foot of Space home, absent for the Owner | `LeaveSpaceTests` |
| **Q12 [LEE]** | (a) typed `LostMembership` -> boundary `404`, recorded `CrossSpace` | `An_Admin_removed_while_its_action_waits_meets_the_boundary_404_recorded_CrossSpace` (change and removal arms); leave replay |
| Q3 | (a) hard delete | Token `401` case; presence case; `REV-*` Token arms |
| Q4 | (a) `DELETE {id}` / `DELETE .../membership` -> `204`; `GET {id}` -> `200` row | every send; `MembershipWire` stand-ins |
| Q5 | (a) `InSpaceNow` from unexpired `SpacePresence`, database clock, fresh at confirm-open; removal deletes presence | single-row read case; presence case; confirm theory; contract gate |
| Q6 | (a) `409 ownership-held` | Owner own-row and Owner-leave cases; problem-type gate; client `409` case |
| Q7 | (a) no schema guard | none (the active DELETE-narrowing case proves the FILTER predicate suffices for isolation) |
| Q9 | (a) unassignment met vacuously; prove "survives" | `What_the_removed_Member_created_survives_the_removal` |
| Q10 | (a) ledger to 5.1/5.3 | none |
| Q11 | (a) `Yello/Memberships/{spaceId}` | races 1-3 |
| Q13 | (a) leave replay is the boundary `404`, recorded; client reads a same-key `404` as left | leave replay case; client resend case |
| Q14 | (a) re-read page 1 to learn the Space answers; drop one row, keep appended pages | ledger `:789` client case |
| Q15 | (a) per-row in-flight state | ledger `:791` client case |
| Q16 | (a) the copy list | client cases and the problem-type gate, asserted as written |

### New assumptions this run made (no question covered them)

1. **Temporary stand-ins in `MembershipWire`** (`LeaveRoute`, `LeaveOf`, `OwnershipHeldProblemType`), the 4.4 precedent:
   the contract declares the leave route and `ProblemTypes.OwnershipHeld` in Task 2; the dev phase swaps every use (Slices,
   Isolation, Revocation, the fake) and deletes the three. `RemoveCapabilityCode`, `LeaveCapabilityCode`,
   `PresentRowMembers` and `PresentMembership` are lasting test readings of the wire.
2. **`PresentRowMembers` is kept apart from `RowMembers`.** The active list-shape case holds today's five members; widening
   `RowMembers` now would turn it red. When `InSpaceNow` lands, the dev phase adds `inSpaceNow` to `RowMembers` (the list's
   shape case goes red at that moment, as designed) and may then fold the two.
3. **The leave route's capability code is `read-projects-and-tasks`**, per Lee's Q1 (a). No `403` is expected on it for any
   Role; the code appears only in the routes gate.
4. **The accept answers `200`, not the `201` AC11 states** for the bound case. Every accept in the suite answers `200` with
   its body (`At_99_Memberships_the_same_accept_succeeds_and_makes_100`); the scaffold asserts `200` and says so.
5. **The client's "first control of the next row"** after a removal is asserted as "focus is inside the next row", so the
   case does not fix which control the dev phase puts first.
6. **The AC7 browser case navigates within the app** (`Blazor.navigateTo`) after the fake stops listing the Space, then
   compares the refusal fingerprint with an in-app navigation to a never-existing Space. This is "the open tab's next
   request"; the 1.9 fingerprint case (`SpaceScopedFirstLoadTests`) compares two full loads.
7. **The `MembershipChanged`, presence and race cases seed directly** (`AddMembershipAsync`, `SpaceSeeding`), as 4.4 did: the
   accept publishes its own `MembershipChanged` and inserts its own presence row, either of which would bury the count.

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ActiveSpaceEndpointTests`; isolation rows
ride the boundary world; revocation rows ride their world; `LeaveSpaceTests` uses the Client assembly's existing fixtures
(`SqlServerEngine`, `ClientSurfaceFixture`). Engines on a full run: unchanged.

### Scenario map

| AC | Level | Scaffold | Priority |
|---|---|---|---|
| AC1 | Wire | `The_Owner_removes_an_Admin_a_Member_or_a_Viewer_and_the_row_is_gone` (x3) | P0 |
| AC1 | Wire | `An_Admin_removes_a_Member_or_a_Viewer` (x2) | P0 |
| AC1, AC2 | Wire | `An_Admin_removing_another_Admin_or_the_Owner_is_refused_403_recorded_and_nothing_changes` (x2) | P0 |
| AC1 (Q2 self) | Wire | `An_Admin_removing_its_own_row_on_the_item_route_answers_204_and_is_out` | P1 |
| AC2 | Wire | `The_Owner_removing_its_own_row_is_refused_409_ownership_held_and_nothing_changes` | P0 |
| AC1 | Wire | `A_Member_or_a_Viewer_is_refused_403_by_the_pipeline_even_on_its_own_row` (x2) | P0 |
| AC1, AC9 | Wire | `Removing_an_id_from_another_Space_or_never_issued_is_the_boundary_404_and_is_not_recorded` | P0 |
| AC4 (V-3) | Wire | `Removal_kills_the_removed_Accounts_Tokens_for_this_Space_and_no_other` | P0 |
| AC4 (Q5) | Wire | `Removal_deletes_the_removed_Accounts_presence_in_this_Space_only` | P1 |
| AC4 | Wire | `A_committed_removal_logs_exactly_one_MembershipChanged_for_the_removed_Account` | P0 |
| AC4, AC11 | Wire | `Refusals_not_founds_and_replays_of_a_removal_publish_nothing` | P0 |
| AC11 | Wire | `A_removal_replays_under_its_key_a_reused_key_on_another_target_is_422_and_a_malformed_key_400` | P1 |
| AC4 (Q9) | Wire | `What_the_removed_Member_created_survives_the_removal` | P1 |
| AC7 (V-2) | Wire | `The_very_next_request_after_a_removal_is_the_boundary_404_by_Session_and_401_by_Token` | P0 |
| AC11 | Wire | `A_removal_in_a_full_Space_frees_the_slot_a_pending_acceptance_then_takes` | P2 |
| AC6 (Q5) | Wire | `The_single_row_read_says_in_this_Space_now_only_for_an_unexpired_presence_here` | P1 |
| Task 4 | Wire (schema) | `A_Membership_DELETE_under_one_Spaces_key_deletes_nothing_in_another_Space` (**active**) | P0 |
| AC3 | Wire | `A_non_Owner_leaves_and_keeps_its_Account_its_own_Space_and_its_other_Memberships` (x3) | P0 |
| AC2, AC3 | Wire | `The_Owner_leaving_is_refused_409_ownership_held_and_keeps_the_Space` | P0 |
| AC3 (Q1 parity) | Wire | `A_Token_leaves_its_own_Space_and_its_next_request_is_the_no_credential_401` | P1 |
| AC3, AC11 (Q13) | Wire | `A_leave_replayed_under_its_key_after_it_committed_is_the_boundary_404_and_recorded` | P1 |
| AC4 | Wire | `A_committed_leave_logs_one_MembershipChanged_and_a_refused_leave_none` | P1 |
| AC11 race 1 | Wire (race) | `A_removal_and_a_Role_change_of_one_target_held_on_the_Memberships_lock_end_in_a_serial_outcome` | P0 |
| AC11 race 2 | Wire (race) | `An_Admin_demoted_while_its_removal_is_queued_is_refused_403_and_the_target_stays` | P0 |
| AC11 race 3 | Wire (race) | `A_leave_and_a_removal_of_one_Membership_held_on_the_lock_commit_once_and_publish_once` | P1 |
| AC10 (`:790`, Q12) | Wire (race) | `An_Admin_removed_while_its_action_waits_meets_the_boundary_404_recorded_CrossSpace` (x2) | P0 |
| AC10 (`:398`) | Wire (race) | `A_Token_issue_racing_the_removal_of_its_issuer_is_the_boundary_404_and_never_a_500` (**active**) | P1 |
| AC5 (`:758`) | Wire | `An_Invitation_whose_issuer_was_removed_through_the_product_stays_listed_revocable_and_acceptable` | P1 |
| AC9 | Isolation | 11 rows, below | P0 |
| AC4, AC7 | Revocation | `REV-MEMBERSHIP-REMOVED-BY-ADMIN`, `REV-LEFT-THE-SPACE` (both surfaces each) | P0 |
| AC1, AC3 | Architecture | `The_Membership_routes_declare_one_capability_each_and_serve_both_surfaces` | P0 |
| AC3 (Q1) | Architecture | `The_leave_route_is_the_one_write_under_ReadProjectsAndTasks` | P0 |
| AC1, AC2 (Q2) | Architecture | `The_removal_rules_agree_with_the_matrix_transcription` | P0 |
| AC1, AC8 | Architecture | `The_clients_Remove_control_agrees_with_the_removal_rules_for_every_pair` | P0 |
| AC6 | Architecture | `The_Membership_row_carries_InSpaceNow_and_the_Role_change_answer_does_not` | P1 |
| AC2 (Q6) | Architecture | `The_ownership_held_problem_type_is_declared_and_has_client_copy` | P1 |
| AC8 | Architecture | `No_Remove_or_Leave_control_is_disabled` | P1 |
| AC8 | Browser | 9 methods added to `MembershipSectionTests` (ledger `:789`, `:791` included) | P0/P1 |
| AC2, AC3, AC7, AC8 | Browser | `LeaveSpaceTests`, 7 methods | P0/P1 |

Not scaffolded here, by design: the `RemovalAuthority.Judge` and handler unit tests (`MembershipHandlerTests`) cannot
compile before Task 1 and Task 3 declare their types; the transcription gate covers the sets by shape until then, and the
dev phase writes the unit tests from the `RoleChangeAuthority.Judge` cases. Race 4 (removal against acceptance) is the
existing active `An_acceptance_answers_while_the_Spaces_Memberships_lock_is_held`, which already proves acceptance does not
queue on the Memberships lock. The 4.4 route gate and the empty-Space-home "one action" case are amended by the dev phase
(see Findings), not scaffolded. Duplicates avoided: the non-member's boundary `404` on removal and leave, and the
Member/Viewer refusals on the Token surface, are the isolation rows, not Slices cases.

## Step 4 - Generated scaffolds (TDD red phase)

Every scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; every isolation row is
`PendingUntil: "4.5"` with a reason; the revocation rows carry `Skip`. Execution mode resolved to **sequential**.

| File | Methods / rows | Cases | ACs |
|---|---|---|---|
| `tests/Yello.Tests.Slices/.../ActiveSpaceEndpointTests.RemoveMembership.cs` | 17 (16 skipped, 1 active) | 22 | AC1, AC2, AC4, AC6, AC7, AC9, AC11 |
| `tests/Yello.Tests.Slices/.../ActiveSpaceEndpointTests.LeaveSpace.cs` | 5 (all skipped) | 7 | AC2, AC3, AC4, AC11 |
| `tests/Yello.Tests.Slices/.../ActiveSpaceEndpointTests.RemoveMembershipRace.cs` | 5 (4 skipped, 1 active) | 6 | AC10, AC11 |
| `tests/Yello.Tests.Slices/.../ActiveSpaceEndpointTests.RevokeInvitation.cs` (one added) | 1 (skipped) | 1 | AC5 |
| `tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs` (`RemovalCases`) | 11 rows, `PendingUntil: "4.5"` | 22 when active (0 generated while pending) | AC1, AC3, AC6, AC9 |
| `tests/Yello.Tests.Revocation/RevocationCases.cs` | 2 rows (skipped) | 4 | AC4, AC7 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.RemoveMembership.cs` | 7 (all skipped) | 7 | AC1, AC2, AC3, AC6, AC8 |
| `tests/Yello.Tests.Client/MembershipSectionTests.cs` (eight added) | 8 (all skipped) | 10 | AC6, AC8, ledger `:789`, `:791` |
| `tests/Yello.Tests.Client/LeaveSpaceTests.cs` (new class, existing assembly fixtures) | 7 (6 skipped, 1 active) | 11 | AC2, AC3, AC7, AC8 |

### Discovery runs (no full run, per the phase's policy)

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Slices, `--filter-method` on the 28 new methods | 28 | 26 | 2 | 0 |
| Isolation, `IsolationCaseListTests` (GATE-001..003, route coverage) | 16 | 0 | 16 | 0 |
| Revocation, `RevocationCaseTests` | 26 | 4 (the new rows) | 22 | 0 |
| Architecture, whole suite | 458 | 7 (the new gates) | 451 | 0 |
| Client, `LeaveSpaceTests` | 7 | 6 | 1 | 0 |
| Client, the 8 new `MembershipSectionTests` methods | 8 | 8 | 0 | 0 |

### Green today, left active (standing regression gates)

| Gate | Why it is green now |
|---|---|
| `A_Membership_DELETE_under_one_Spaces_key_deletes_nothing_in_another_Space` | `Membership`'s FILTER predicate narrows a raw `DELETE` to the keyed Space (no migration needed, Q7 (a)) |
| `A_Token_issue_racing_the_removal_of_its_issuer_is_the_boundary_404_and_never_a_500` | 3.3's `LostSpace` already catches the `547` and answers the recorded boundary `404`: ledger `:398` is closed by this case |
| `LeaveSpaceTests.A_removed_Members_next_request_meets_the_same_refusal_as_a_Space_that_never_existed` | the one refusal surface already answers a Space the server stops listing (AC7's client half needs no new code) |

### Red verified (temporary unskip, restored in `finally`; never `git checkout`)

| Scaffold | Failed with | Reason matches |
|---|---|---|
| `An_Admin_removing_another_Admin_or_the_Owner_is_refused_403_recorded_and_nothing_changes` (both arms) | "The Admin's in-authority anchor: 404 [...] not-available" | yes - the route is unmapped; seeding three joiners through issue and accept worked |

### Seed data against the real schema

- `FK_Membership_Space_SpaceId`, `FK_Membership_Account_AccountId` (Restrict): every seeded Membership names a Space seeded
  first and an Account registered through the product or added in the same unit (`SpaceSeeding.NewAccount`).
- `AK_Membership_SpaceId_AccountId`: one Membership per Account per Space; second Memberships go to different Spaces.
- `UX_Membership_SpaceId_Owner`: one Owner per seeded Space; `AddMembershipAsync` refuses an Owner.
- `PK_SpacePresence (SpaceId, AccountId)` and `FK_SpacePresence_Space` (cascade): `RecordPresenceAsync` writes only for an
  Account that has made no request in that Space (seeded with `AddMembershipAsync`), under the Space's key (its BLOCK
  predicate).
- `FK_ApiToken_Membership_BoundSpaceId_AccountId` (cascade): no Token row is inserted by hand; every Token is issued through
  the product for a Space its Account belongs to.
- `Invitation` rows only through 4.1's issue, accepted through 4.3's route; the raw `DELETE` of a Membership in the races
  touches no Invitation (its issuer key references `Account`).

### Findings for the dev phase

1. **The 4.2/4.3/4.4 trap holds:** every `DELETE` and `GET` under `/memberships/{id}` and the leave path answer the boundary
   `404 not-available` today (seen red). Every refusal and not-found scaffold is anchored on a `204` first.
2. **Ledger `:398` is already fixed**; close it citing the active race case.
3. **Active gates to amend when the work lands:** `The_Membership_routes_require_ChangeMembershipRole_and_serve_both_surfaces`
   (replace with the new routes gate), `MembershipWire.RowMembers` (+ `inSpaceNow`),
   `SpaceHomeProjectsTests.An_empty_Space_states_no_Projects_yet_and_offers_one_action_to_create_one` (set aside Leave), and
   `Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` until the eleven rows are live.
4. **xUnit filters of different kinds AND together**: `--filter-class X --filter-method Y` matches nothing; run them
   separately.

## Activation order (riskiest assumption first)

Recorded in the story's Dev Notes, "ATDD artifacts - red-phase scaffolds already in the tree". First:
`An_Admin_removed_while_its_action_waits_meets_the_boundary_404_recorded_CrossSpace` (Lee's Q12 (a): the pipeline, not the
slice, turns a typed `LostMembership` into the recorded boundary `404`).

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Implementation guidance

- **Contracts:** `MembershipRoutes` leave route (`Own`, `OwnOf`) and the item `DELETE`/`GET` remarks; `MembershipResponse.InSpaceNow`;
  `ProblemTypes.OwnershipHeld`. Then swap `MembershipWire.LeaveRoute`, `LeaveOf` and `OwnershipHeldProblemType` everywhere and
  delete them.
- **Domain:** `RemovalAuthority(Removable, HeldBy)` and `RoleCapabilityMatrix.RemovalAuthorityOf(Role)`, Owner in no set.
- **Host:** `ActingAuthority.Removals` set in `QualifiedAuthority`; `MembershipLost` -> recorded boundary `404`; endpoints with
  no `_ =>` arm.
- **Infrastructure:** guarded removal and leave `DELETE`s under `TakeMembershipsAsync`, presence delete, disambiguation read,
  `Record` after one row deleted, `InSpaceNow` with `SYSUTCDATETIME()`.
- **Client:** Remove control and in-place confirm in `MembershipRow`, per-row in-flight state, the `404` re-read that keeps
  appended pages, `MembershipCapabilities.Removable`, `LeaveSpaceSection.razor` on Space home, copy in `ClientCopy.resx`.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs and Lee's Q1, Q2, Q8, Q12 decisions; framework; Docker up).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC11 each mapped above.
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 4.5 with a reason; the three
      green ones are left active and named.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (constraints listed above).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open; temporary logs live in the session scratchpad.
- [x] No full run (this phase's policy); discovery runs above.

**Next workflow:** `bmad-dev-story` for 4.5, then `bmad-code-review`.
