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
lastSaved: '2026-11-02'
workflowType: 'testarch-atdd'
storyId: '4.4'
storyKey: '4-4-manage-memberships-and-roles'
storyFile: '_bmad-output/implementation-artifacts/4-4-manage-memberships-and-roles.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-4-4-manage-memberships-and-roles.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; wire, gates and browser scaffolds written in the main run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ListMemberships.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ChangeMembershipRole.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ChangeMembershipRoleRace.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs'
  - 'tests/Yello.Tests.Slices/Memberships/MembershipLabelsTests.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs'
  - 'tests/Yello.Tests.Revocation/RevocationCases.cs'
  - 'tests/Yello.Tests.Revocation/RevocationWorld.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Memberships.cs'
  - 'tests/Yello.Tests.Client/MembershipSectionTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.Memberships.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Shared/MembershipWire.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/4-4-manage-memberships-and-roles.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-4-3-accept-an-invitation.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 4, Story 4.4: Manage Memberships and Roles

## Story summary

As an Owner or Admin, I want to see who is in this Space and change what they can do, so that standing matches the working
relationship without anyone having to leave and rejoin. Requirements: **FR-13**, AR-12/AD-5, AR-16/AD-9, UX-DR14, UX-DR34,
NFR-2 (V-1, S-2), with UX-DR11, UX-DR27, UX-DR29, UX-DR38, UX-DR39, AD-18, AD-20, AD-23 binding. Two new routes, both
`{spaceId}`-scoped under `Capability.ChangeMembershipRole`: the list `GET /api/v1/spaces/{spaceId}/memberships?after=` and the
Role change `PATCH .../memberships/{membershipId}` with `{ role }`. Client: a "Members" section in Space settings, plus the
three client ledger items the story owns (`deferred-work.md:546`, `:550`, `:762`).

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025, Playwright
  for .NET 1.62.0. Framework present; `tests/TESTING-CONVENTIONS.md` is the authority. Docker up (server 29.5.3).
- **Story:** `ready-for-dev`, AC1-AC11 with cited sources. **Q1-Q5 decided by Lee (2026-11-02), all (a):** Q1 pipeline-owned
  acting authority (Role sets, Owner in neither, actor-still-holds-its-Role clause); Q2 guarded statement + S-2 invariant test;
  Q3 BLOCK AFTER UPDATE on `Membership`; Q4 list under `ChangeMembershipRole` with Token parity and two address gates; Q5 live
  `Account.DisplayName` read with a named-reader gate. Q6-Q19 scaffolded at their recommendations, each named in the skip
  reason or the case's summary where it shapes an expectation.
- **No progress file existed** for this story (`atdd-checklist-4-4-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`.
- **Knowledge:** core (data-factories, component-tdd, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** the `ActiveSpaceEndpointTests` partials (`IssueAsync`, `AcceptAsync`, `KeyHeaders`, `SignedInAsync`,
  `RegisteredAndSignedInAsync`, `SeedNamedSpaceAsync`, `SeedSpaceHoldingMembershipsAsync`, `RefusalsOfAsync`, `BearerForAsync`,
  `TakeApplicationLockAsync`, `Described`), `InvitationWire`, `MailDrop`, `WireDatabase`, `SpaceSeeding`,
  `TaskWire.WaitUntilBlockedOnALockAsync`, `LabelWire.WaitUntilBlockedOrAnsweredAsync`, the isolation world
  (`OwnerOfTwoAsync`, `InviteWithTokenAsync`, `TokenOwnOtherSpace`, `ForeignAsync`), the revocation world, and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the Members section does not exist, so
every locator is an assumption written from AC10, Q12 (a), Q19 (a) and the mockup.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| **Q1 [LEE]** | (a) acting authority, actor clause kept | Admin-beyond-authority theory; race 2; target-rule, writer and Qualified-cell gates; `ISO-CHANGE-ROLE-ADMIN-BEYOND-AUTHORITY` |
| **Q2 [LEE]** | (a) guarded statement + S-2 | `AssertOneOwnerEachAsync` after every change; Owner own-row 409; 2601 schema case (active) |
| **Q3 [LEE]** | (a) BLOCK AFTER UPDATE on `Membership` | `Membership_carries_a_BLOCK_AFTER_UPDATE_predicate_on_its_Space` |
| **Q4 [LEE]** | (a) list under `ChangeMembershipRole`, Token parity, two gates | routes gate; 403 list cases and rows; `TOKEN-OWN-OTHER-SPACE-LIST-MEMBERSHIPS`; contract and reader gates (active) |
| **Q5 [LEE]** | (a) live `Account.DisplayName` | shape case renames the Account and re-lists; `Attribution_never_reads_a_live_Account_name` (active) |
| Q6 | (a) no remove control | no case renders or asserts one |
| Q7 | (a) in-process publish, one log line | `MembershipChangedPublished` cases |
| Q8 | (a) 50 a page, keyset, true total, "Show more" | 60- and 100-row cases; client append case |
| Q9 | (a) `GET ?after=` / `PATCH` -> `200` row | every send; `MembershipWire` stand-ins |
| Q10 | (a) `409 owner-role-fixed`, `400 role-not-assignable` | own-row and body cases; fake |
| Q11 | (a) same Role is a `200` no-op | same-Role case; no-publish case |
| Q12 | (a) in-place radio group, nothing checked | picker client case; offer gate |
| Q13 | (a) SHA-256 hex suffix in `Yello.Contracts.Memberships.MembershipLabels` | `MembershipLabelsTests`; client collision case |
| Q14 | (a) status element takes focus | ledger `:762` theory (four rows) |
| Q15 | (a) region empty, filled after first render | ledger `:550` case |
| Q16 | (a) re-read on `404` | ledger `:546` case |
| Q17 | (a) `Yello/Memberships/{spaceId}` | races 1-3 |
| Q18 | (a) the accept publishes too | AC6 cases seed directly so the accept's publish cannot bury the count |
| Q19 | (a) the copy list | client cases, asserted as written |

### New assumptions this run made (no question covered them)

1. **Temporary stand-ins in `MembershipWire`** (`ListRoute`, `MembershipRoute`, `MembershipsOf`, `MembershipOf`,
   `OwnerRoleFixedProblemType`, `RoleNotAssignableProblemType`), the 4.2/4.3 precedent: Task 3 declares them in
   `Yello.Contracts`; the dev phase swaps every use (Slices, Isolation, Revocation, the fake, the gates) and deletes the six.
2. **S-2 is scoped to the case's Spaces**, not the whole database (`WireDatabase.SpacesNotHoldingExactlyOneOwnerAsync`):
   `ActiveSpaceEndpointTests` shares one database across every partial, and earlier cases deliberately leave Spaces with no
   Owner (`ChangeRoleAsync` on a Personal Space's Owner). A database-wide sweep would fail on them, not on 4.4.
3. **The `MembershipChanged` cases seed through `AddMembershipAsync`**: under Q18 (a) the accept publishes its own
   `MembershipChanged`, which would bury "exactly one publish". The races seed the same way (the overlap is the point).
4. **The revocation suite gained a skip column and a mail drop**: `RevocationCase.Skip` emits `TheoryDataRow.Skip` (the suite
   had no pending mechanism), and `RevocationWorld.Mail` (the `IsolationWorld.Mail` precedent) lets the row join through 4.3's
   accept. Earlier rows' requests are unchanged; the Host now drops mail into the world's folder instead of `MailDrop.Unread`.
5. **The ledger `:762` Label and API Token cases hold their request at the network edge** (`HoldNextAsync`, a Playwright route
   registered after the fake that falls back to it) - the fake has no hold for those two deletes, and adding one to the main
   switch would touch every earlier case's router.
6. **`AC8` is a twin, not a conversion.** Converting the active raw-SQL case now would turn it red; the product-route twin sits
   beside it, skipped, and the dev phase deletes the original once the twin is green (stated in its remarks).

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ActiveSpaceEndpointTests`;
`MembershipLabelsTests` takes no database; isolation rows ride the boundary world; the revocation row rides its world; client
cases join `ClientSurfaceCollection`. Engines on a full run: unchanged at four.

### Scenario map

| AC | Level | Scaffold | Priority |
|---|---|---|---|
| AC1 | Wire | `The_Owner_and_an_Admin_each_list_every_Membership_across_pages_with_the_true_total` | P0 |
| AC1 | Wire | `At_exactly_100_Memberships_the_first_page_holds_50_and_a_cursor_and_the_second_50_and_none` | P0 |
| AC1 | Wire | `A_Member_and_a_Viewer_are_refused_403_recorded_and_a_non_member_meets_the_boundary_404` | P0 |
| AC1, AC2, AC7 | Wire | `Each_row_is_exactly_its_id_live_display_name_typed_address_Role_code_and_IsYou` | P0 |
| AC2 | Wire | `Neither_an_address_nor_a_display_name_reaches_the_Host_log_on_list_or_change` | P0 |
| AC4 | Wire | `The_Owner_changes_a_non_Owner_Role_and_is_answered_the_updated_row` (x4) | P0 |
| AC4 | Wire | `An_Admin_changes_a_Member_or_a_Viewer_between_Member_and_Viewer` (x2) | P0 |
| AC4 | Wire | `An_Admin_beyond_its_authority_is_refused_403_recorded_and_nothing_changes` (x4) | P0 |
| AC4 | Wire | `A_Member_or_a_Viewer_is_refused_403_by_the_pipeline_and_recorded` (x2) | P1 |
| AC4, AC5 | Wire | `The_Owner_changing_its_own_row_is_refused_409_owner_role_fixed_and_nothing_changes` | P0 |
| AC4, AC5 | Wire | `Owner_or_an_unknown_code_in_the_body_is_refused_400_role_not_assignable_and_nothing_changes` (x2) | P0 |
| AC4, AC9 | Wire | `An_id_from_another_Space_or_never_issued_is_the_boundary_404_and_is_not_recorded` | P0 |
| AC4, AC6 | Wire | `A_same_Role_change_answers_200_with_the_unchanged_row_and_publishes_nothing` | P1 |
| AC6 | Wire | `A_committed_change_logs_exactly_one_MembershipChanged_publish_for_the_target_and_the_Space` | P0 |
| AC6, AC11 | Wire | `Refusals_not_founds_no_ops_and_replays_publish_nothing` | P0 |
| AC11 | Wire | `A_change_replays_under_its_key_a_reused_key_on_another_body_is_422_and_a_malformed_key_400` | P1 |
| AC6 (V-1) | Wire | `The_very_next_request_after_a_demotion_is_governed_on_both_surfaces` (x2, five classes) | P0 |
| AC5 | Wire (schema) | `A_second_Owner_fails_2601_on_the_filtered_index_while_an_Admin_update_of_the_same_row_succeeds` (**active**) | P0 |
| AC5, Q3 | Wire (schema) | `Membership_carries_a_BLOCK_AFTER_UPDATE_predicate_on_its_Space` | P0 |
| AC11 | Wire (race) | `An_Owners_and_an_Admins_changes_to_one_target_held_on_the_Memberships_lock_end_in_a_serial_outcome` | P0 |
| AC11 | Wire (race) | `An_Admin_demoted_while_its_change_is_queued_is_refused_403_and_the_target_is_unchanged` | P0 |
| AC11 | Wire (race) | `An_acceptance_answers_while_the_Spaces_Memberships_lock_is_held` (**active**) | P1 |
| AC8 | Wire | `An_Invitation_whose_issuer_was_demoted_through_the_product_stays_listed_and_revocable_by_any_Owner_or_Admin` | P1 |
| AC3 | Unit | `MembershipLabelsTests` - 5 methods (7 cases) | P1 |
| AC9 | Isolation | 11 rows, below | P0 |
| AC6 | Revocation | `REV-ROLE-CHANGED-BY-OWNER` (both surfaces) | P0 |
| AC1, AC4 | Architecture | `The_Membership_routes_require_ChangeMembershipRole_and_serve_both_surfaces` | P0 |
| AC1 | Architecture | `The_Membership_contracts_carry_exactly_their_named_members` | P0 |
| AC2 | Architecture | `Only_the_Membership_list_and_the_Invitation_list_carry_an_email_address` + detector (**active**) | P0 |
| AC2 | Architecture | `Only_named_types_read_an_Accounts_email_address` + detector (**active**) | P0 |
| AC7 | Architecture | `Attribution_never_reads_a_live_Account_name` + detector (**active**) | P0 |
| AC4 | Architecture | `Only_the_pipeline_sets_the_acting_authority` | P0 |
| AC4 | Architecture | `The_target_rules_agree_with_the_matrix_transcription` | P0 |
| AC4 | Architecture | `A_Qualified_cell_reaches_no_endpoint_without_its_target_rule` | P0 |
| AC4, AC5 | Architecture | `The_Membership_problem_types_are_declared_and_each_has_client_copy_naming_story_4_4` | P1 |
| AC1, AC10 | Architecture | `The_clients_Members_section_predicate_agrees_with_the_capability_matrix` | P1 |
| AC4 | Architecture | `The_clients_Role_offer_agrees_with_the_target_rules_for_every_pair` | P0 |
| AC4 | Architecture | `No_control_in_the_Membership_section_is_disabled` | P1 |
| AC10 (AC1-AC4) | Browser | 13 methods in `MembershipSectionTests` (ledger `:546`, `:550`, `:762` included) | P0/P1 |

Not scaffolded here, by design: the handler unit tests (`ListMembershipsHandler`, `ChangeMembershipRoleHandler`) cannot
compile before Task 4 declares their types; the dev phase writes them from the `IssueInvitationHandlerTests` template.
Duplicates avoided: the non-member's `404` on the Role change and the Member/Viewer refusals on the Token surface are the
isolation rows, not Slices cases. Gates the story says "stay green unchanged" are existing tests, listed below.

## Step 4 - Generated scaffolds (TDD red phase)

Every scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; every isolation row is
`PendingUntil: "4.4"` with a reason; the revocation row carries `Skip`. The build stays at 0 warnings and the suite green.

| File | Methods / rows | Cases | ACs |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ListMemberships.cs` | 5 | 5 | AC1, AC2, AC7 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ChangeMembershipRole.cs` | 14 (13 skipped, 1 active) | 24 | AC4, AC5, AC6, AC11 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ChangeMembershipRoleRace.cs` | 3 (2 skipped, 1 active) | 3 | AC11 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs` (one added) | 1 | 1 | AC8 |
| `tests/Yello.Tests.Slices/Memberships/MembershipLabelsTests.cs` | 5 | 7 | AC3 |
| `tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs` | 11 rows | 22 when active (0 generated while pending) | AC1, AC4, AC9 |
| `tests/Yello.Tests.Revocation/RevocationCases.cs` (one row) | 1 row | 2 (skipped) | AC6 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Memberships.cs` | 14 (9 skipped, 5 active) | 14 | AC1, AC2, AC4, AC5, AC7, AC10 |
| `tests/Yello.Tests.Client/MembershipSectionTests.cs` | 13 | 19 | AC10 (AC1-AC4), ledger `:546`, `:550`, `:762` |

**Skipped methods added: 50** as the runner reports them (26 Slices, 9 Architecture, 2 Revocation rows, 13 Client; a skipped
theory is reported once). **Active methods added: 7** (2 Slices, 5 Architecture), all green.

### Green today, left active (standing regression gates)

| Gate | Why it is green now | Its oracle |
|---|---|---|
| `A_second_Owner_fails_2601_on_the_filtered_index_while_an_Admin_update_of_the_same_row_succeeds` | `UX_Membership_SpaceId_Owner` exists (1.3); no earlier case asserted its refusal by number | the dev phase plants the index dropped in a scratch database |
| `An_acceptance_answers_while_the_Spaces_Memberships_lock_is_held` | nothing takes `Yello/Memberships/{spaceId}` yet | red if acceptance is made to queue behind the Role change (Q17 (a) forbids it) |
| `Only_the_Membership_list_and_the_Invitation_list_carry_an_email_address` | only `InvitationResponse` and the three request types carry one | `The_address_contract_scan_detects_a_plant` (a planted Presence response; `bool AddressedToYou` admitted) |
| `Only_named_types_read_an_Accounts_email_address` | today's readers are the named stores and configuration | `The_Account_reader_scan_detects_a_plant` (a planted reader in this assembly) |
| `Attribution_never_reads_a_live_Account_name` | today's readers are the named stores and configuration | the same oracle |

Existing gates the story requires to stay green, unchanged and still the guard: `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context`,
`No_slice_or_endpoint_reads_the_Role_off_a_Membership_row` (both **unwidened**), `SpaceResolutionSeamTests.No_production_code_introduces_a_cache`
(AC6's no-cache gate - it exists, so it is named rather than added), `Neither_the_list_nor_the_revoke_reads_the_issuers_Account`,
`No_log_message_takes_an_address_a_token_or_an_email_body`, `Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay`,
`The_SpaceId_key_is_set_by_exactly_the_named_units_of_work`, `RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal`,
`REV-ROLE-CHANGED`, and every 4.1-4.3 case.

### Isolation rows (AC9), all `PendingUntil: "4.4"`

| Row | Vector | Session | ApiToken |
|---|---|---|---|
| `ISO-LIST-MEMBERSHIPS-FOREIGN-SPACE` | I-1 | `BoundaryNotFound` | same |
| `ISO-CHANGE-ROLE-FOREIGN-SPACE` | I-2 | `BoundaryNotFound`, B still Owner | same |
| `ISO-LIST-MEMBERSHIPS-OWN-SPACE` | I-3, I-11 | `Served(200)`: exactly the Space's two Memberships, nothing of the shared Member's other Space | same |
| `ISO-RECORD-KIND-LIST-MEMBERSHIPS-MEMBER` / `-VIEWER` | V-1, NFR-7 | `RoleRefused(change-membership-role)` | same |
| `ISO-RECORD-KIND-CHANGE-ROLE-MEMBER` / `-VIEWER` | V-1, NFR-7 | `RoleRefused`, the host still Owner | same |
| `ISO-CHANGE-ROLE-ADMIN-BEYOND-AUTHORITY` | Q1 seam | `RoleRefused`, the target still Member (`PRD-12-4`) | same |
| `ISO-CHANGE-ROLE-FOREIGN-ID-IN-OWN-SPACE` | I-4, I-13 | `SameAsAnchor(404, not-available)`, unrecorded | same |
| `ISO-TOKEN-OWN-OTHER-SPACE-LIST-MEMBERSHIPS` | I-12 | `Served(200)` | `SameAsNonMember` |
| `ISO-TOKEN-OWN-OTHER-SPACE-CHANGE-ROLE` | I-12 | `Served(200)`, target Viewer | `SameAsNonMember`, target still Member |

Lesser Roles and the Admin join a fresh host through an Invitation at that Role, accepted (4.3), each with a Token issued
there; `world.A` and `world.B` gain no Members. Every joined Account's address and name, and every invited address and token,
is held as a secret (I-17). No timing row: Q9 (a)'s in-Space not-found is the floored boundary `404`, already proven by
`ISO-TIMING-IN-SPACE` (recorded in the file's remarks).

### Supporting harness (no product code)

- `MembershipWire` (new, `Yello.Tests.Shared`): the six stand-ins above; lasting `CapabilityCode`, `PageSize`,
  `PublishedEvent`, `PageMembers`, `RowMembers`, `ChangeBody`, `ParsePage`, `ParseRow`, `MemberNames`, and the
  `MembershipPage` / `ListedMembership` records the fake also serialises.
- `WireDatabase`: `MembershipRowsInAsync`, `MembershipOfAsync`, `SpacesNotHoldingExactlyOneOwnerAsync` (S-2),
  `SetDisplayNameAsync` (Q5's live read), and the `MembershipRow` record.
- `RevocationWorld.Mail`; `RevocationCase.Skip` emitted as `TheoryDataRow.Skip`.
- `FakeYelloApi.Memberships.cs`: `SpaceMemberships`, `NextRoleChangeRefusal`, `HoldRoleChangeAsync` /
  `ReleaseRoleChange*`, `HoldMembershipListAsync` / `ReleaseMembershipList`, `MembershipListReads`, `RoleChanges`; wired into
  `FakeYelloApi.DecideAsync` after the 4.3 routes.

### Seed data against the real schema (checked against `20260828164716_InitialSchema` and `20261024143930_AccountScopedMembership`)

- `FK_Membership_Space_SpaceId`, `FK_Membership_Account_AccountId` (Restrict): every seeded Membership names a Space seeded
  first and an Account registered through the product or added in the same unit (`SpaceSeeding.NewAccount`).
- `AK_Membership_SpaceId_AccountId`: one Membership per Account per Space - every joiner is a fresh Account; the
  `ISO-LIST-MEMBERSHIPS-OWN-SPACE` shared Member joins two different Spaces.
- `UX_Membership_SpaceId_Owner` (`[Role] = N'Owner'`): one Owner per seeded Space; `AddMembershipAsync` refuses an Owner; the
  2601 case's second row is seeded as Member and only the raw `UPDATE` tries `Owner`.
- `Membership.Role` `nvarchar` stored by name (`HasConversion<string>`): raw SQL writes `N'Member'`/`N'Admin'`, never codes.
- BLOCK AFTER INSERT (`fn_SpaceIsolationPredicate`): every insert runs under the Space's key; the race holder's demotion is an
  `UPDATE` that keeps `SpaceId`, so the future BLOCK AFTER UPDATE (Q3 (a)) admits it.
- `Account.DisplayName nvarchar(128) not null`: `SetDisplayNameAsync` writes a short non-empty name; `Account` carries no policy.
- `Invitation` rows only through 4.1's issue (its FKs and `UX_Invitation_TokenHash`), accepted through 4.3's route.

### Red verified for two scaffolds (temporary unskip, restored in `finally`; never `git checkout`)

| Scaffold | Failed with | Reason matches |
|---|---|---|
| `An_Admin_beyond_its_authority_is_refused_403_recorded_and_nothing_changes` (all four) | "The Admin's in-authority anchor: 404 [...] not-available" | yes - the route is unmapped; seeding four joiners through issue and accept worked |
| `A_Member_and_a_Viewer_are_refused_403_recorded_and_a_non_member_meets_the_boundary_404` | "The Owner's anchor: 404 [...] not-available" | yes - the anchor caught the 4.2/4.3 trap before any refusal was asserted |

### Findings for the dev phase

1. **The 4.2/4.3 trap holds here too:** every path under `/memberships` answers the boundary `404 not-available` today (seen
   in both red runs). Every refusal, `404` and absence scaffold is anchored on a `200` on the same route first, and every
   failure message states the answers it compared.
2. **Race 1 cannot show a missing lock from its end state.** Either serial order ends with the target Admin (the Owner's change
   always succeeds), and the guarded statement's row lock would serialise the two without the application lock. Its deciding
   assertion is that both requests waited on the held lock; record plant 5 as masked on the end state.
3. **Active gates the dev phase must amend, not scaffolded (each turns red when the work lands):**
   `SpaceIsolationSchemaTests.TablesThatCannotMove` and its per-table predicate counts (Q3 (a)); the AD-19 contract
   snapshot, if one lists contract types; and, once the routes are mapped,
   `Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` until the eleven rows are live.
4. **Q18 (a) and AC6 interact:** once the accept publishes, any AC6 case that joins through the product sees one extra publish
   per join. The AC6 cases seed directly for that reason; keep it so.
5. **`ISO-TOKEN-OWN-OTHER-SPACE-CHANGE-ROLE`'s Session arm changes the target to Viewer** (a fresh target per run), so it
   asserts the change landed, not just a `200`.

## Activation order (riskiest assumption first)

1. **`An_Admin_demoted_while_its_change_is_queued_is_refused_403_and_the_target_is_unchanged`** - the riskiest assumption:
   that the pipeline can hand the slice Role **sets** computed from the Role it alone reads (Lee's Q1 (a)), that one guarded
   `UPDATE` under `Yello/Memberships/{spaceId}` composes them with the actor-still-holds-its-Role clause (`READCOMMITTEDLOCK`
   under RCSI), and that a rowcount-0 disambiguation becomes a typed outcome the **pipeline** turns into a recorded `403` -
   all without widening either Role-read gate. Then `An_Admin_beyond_its_authority_...` (x4), `ISO-CHANGE-ROLE-ADMIN-BEYOND-AUTHORITY`,
   and the three authority gates (`Only_the_pipeline_sets_the_acting_authority`, `The_target_rules_agree_with_the_matrix_transcription`,
   `A_Qualified_cell_reaches_no_endpoint_without_its_target_rule`).
2. The Owner's changes (x4), the Admin's changes (x2), the Member/Viewer refusals, the own-row `409` and the body `400` -
   with S-2 after each - then `Membership_carries_a_BLOCK_AFTER_UPDATE_predicate_on_its_Space` (Q3's migration) and the
   `SpaceIsolationSchemaTests` amendment.
3. The list: the 403/404 case, the 60-row walk, the 100-row bound, the shape case (address as typed, name live), the routes
   and contract gates; then the in-Space `404` case and the replay case.
4. `MembershipChanged`: the one-publish case, the no-publish case, the same-Role case.
5. Race 1; confirm race 3 (active) stays green.
6. V-1: `The_very_next_request_after_a_demotion_is_governed_on_both_surfaces` (x2), `REV-ROLE-CHANGED-BY-OWNER`, the AC8 twin
   (then delete the raw-SQL original and take its name).
7. Every isolation row (clear `PendingUntil: "4.4"` before review, GATE-003); the log case; the problem-type gate.
8. `MembershipLabelsTests` (Q13), then the client gates (section predicate, Role offer, no `disabled`).
9. The browser cases, after `Yello.Client` is built in Debug: Owner and Admin rows, picker, change and announcement, address
   confinement and purge, paging, narrated exit, collision labels, lesser Roles, reflow; then the three ledger cases (focus x4,
   narration region, Label `404`), each seen red under the old markup first.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Implementation guidance

- **Contracts:** `MembershipRoutes` (`List`, `Membership`, `MembershipsOf(spaceId, after)`, `MembershipOf`),
  `MembershipResponse`, `MembershipPageResponse`, `ChangeMembershipRoleRequest`, `MembershipLabels`;
  `ProblemTypes.OwnerRoleFixed`, `ProblemTypes.RoleNotAssignable`. Then swap the six `MembershipWire` stand-ins everywhere and
  delete them.
- **Domain:** the Qualified cells' target rules on `RoleCapabilityMatrix` (one public static method taking a `Role`, answering
  `From` and `To`), Owner in neither.
- **Host:** `ActiveSpaceResolution` writes the acting authority and turns the typed Qualified refusal into the recorded `403`;
  `MembershipEndpoints` with no `_ =>` arm.
- **Infrastructure:** `MembershipStore` (paged projection, count, guarded `UPDATE` under `ApplicationLocks.TakeMembershipsAsync`,
  disambiguation read), the after-commit publisher with its `MembershipChangedPublished` log line, the expand-only migration.
- **Client:** `MembershipSection.razor` (`main .membership-section`, heading "Members"), `MembershipRow.razor`
  (`.membership-row__name`), `MembershipCapabilities.PermitsManageMemberships` / `OfferableRoles`, the four row focus fixes,
  `SpaceHome`'s empty-then-filled region, `LabelSection`'s `404` re-read, `ClientCopy.resx` entries commented "4.4".

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs and Lee's Q1-Q5 decisions; framework; Docker up: server 29.5.3).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC11 each mapped above.
- [x] Every scaffold is skipped with its AC (`red: AC<n> - ...`) or pending until 4.4 with a reason; Q6-Q19 named where they
      shape an expectation.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (constraints listed above).
- [x] Story metadata and handoff paths captured in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open; temporary scripts live in the session scratchpad, not the repository.
- [x] Full build and suite run (totals in the story's Dev Notes section and the run report).

**Next workflow:** `bmad-dev-story` for 4.4, then `bmad-code-review`.
