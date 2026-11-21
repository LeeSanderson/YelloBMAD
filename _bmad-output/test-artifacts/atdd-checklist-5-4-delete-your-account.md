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
storyId: '5.4'
storyKey: '5-4-delete-your-account'
storyFile: '_bmad-output/implementation-artifacts/5-4-delete-your-account.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-5-4-delete-your-account.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the API, isolation, revocation, gate and browser scaffolds share AccountDeletionWire and one build)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccountRace.cs'
  - 'tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/AuthenticateAccountHandlerTests.cs (1 method added)'
  - 'tests/Yello.Tests.Isolation/IsolationCases.AccountDeletion.cs'
  - 'tests/Yello.Tests.Revocation/RevocationCases.cs (1 row added)'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.DeleteAccount.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.Tombstone.cs'
  - 'tests/Yello.Tests.Client/AccountSettingsTests.cs (5 methods added)'
  - 'tests/Yello.Tests.Client/InvitationListTests.cs (1 method added)'
  - 'tests/Yello.Tests.Client/InvitationViewTests.cs (1 method added)'
  - 'tests/Yello.Tests.Client/FakeYelloApi.AccountDeletion.cs'
  - 'tests/Yello.Tests.Shared/AccountDeletionWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/5-4-delete-your-account.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-5-3-decline-revoke-lapse-and-the-states-that-are-no-longer-open.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 5, Story 5.4: Delete your Account

## Story summary

As someone leaving Yello, I want to erase my Account without orphaning a Space or destroying anyone else's work.
Requirements: **FR-3**, **AR-29**, **UX-DR10** (tombstone), **UX-DR36** (the refused rung), with FR-8, FR-12, FR-21, FR-36,
AD-5, AD-6, AD-24, AD-26, NFR-6, and test-design rows V-3, V-11, I-11, S-2. New: `DELETE /api/v1/account` (refuse while the
Account owns a Space, naming every owned Space by identity; otherwise leave every Space through the existing `LeaveAsync`,
then hard-delete the Account), the nullable SET NULL Invitation issuer key, the tombstone avatar, and the Account settings
deletion section.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025, Playwright
  for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority. Docker up (server 29.9.2).
- **Story:** `ready-for-dev`, AC1-AC14. **Decided by Lee:** Q1 (a) three-step sequence; Q3 (a) nullable
  `Invitation.IssuedByAccountId`, `ON DELETE SET NULL`. **Q2, Q4-Q10 undecided**: every scaffold is written against the
  recommendation (a), and a skip that depends on one names it.
- **No progress file existed** (`atdd-checklist-5-4-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`.
- **Knowledge:** core (data-factories, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** the `ActiveSpaceEndpointTests` partials (`JoinedThroughTheProductAsync`, `RegisteredAndSignedInAsync`,
  `SignedInAsync`, `SignedInWithoutRegisteringAsync`, `PersonalSpaceOfAsync`, `DeleteSpaceOverWireAsync`,
  `SeedNamedSpaceAsync`, `SeedAssignableSpaceAsync`, `TaskThroughTheProductAsync`, `AssignOverWireAsync`, `OfferedAsync`,
  `PendingOfferOverWireAsync`, `AcceptOverWireAsync`, `AcceptAsync`, `IssueAsync`, `InviteAsync`, `InvitationIdOfAsync`,
  `ListInvitationsAsync`, `RevokeAsync`, `RemoveOverWireAsync`, `ListMembershipsAsync`, `BearerForAsync`, `PresenceRowsAsync`,
  `RefusalsOfAsync`, `AccountRowsAsync`, `PublishesNaming`, `MadePresentAsync`, `TakeApplicationLockAsync`,
  `MembershipsLockOf`, `AssertOneOwnerEachAsync`, `AssertNoPendingOfferNamesAMissingMembershipAsync`),
  `SpaceIsolationLayerTests.OpenForAccountAsync`, `TaskWire.WaitUntilBlockedOnALockAsync`, `WireDatabase`, `MailDrop`; the
  isolation world (`OwnerOfTwoAsync`, `JoinedThroughTheProductAsync`, `HoldsIdentity`); the revocation helpers
  (`JoinOwnersSpaceAsync`, `AnchorBothSurfacesAsync`, `NoCredential(ClearsTheDeadCookie)`, `JoinedTokenDead`); the gates'
  `RouteTable`, `AssertRadius`, `SelectsComponent`, `Hides`; and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the route, the deletion section, the
refusal state and the tombstone do not exist; every path, locator and copy string for them is the story's recommendation,
named in each file's remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| Q1 (Lee) | (a) check, per-Space `LeaveAsync`, erase | every Slices case; the two race cases hold one Space's Memberships lock |
| Q2 | (a) acceptance mid-sequence stops with `409 account-owns-spaces` naming that Space; one extra leave pass | `.DeleteAccountRace.cs` (2) |
| Q3 (Lee) | (a) nullable `IssuedByAccountId`, `ON DELETE SET NULL`, `IssuerName = null` on the wire | riskiest-assumption Slices case; 2 gates; 2 browser cases |
| Q4 | (a) in-place confirm, no typed field | `Deleting_the_Account_asks_in_place_...` |
| Q5 | (a) one tombstone state, rendered on the Invitation row and view | 3 CSS gates; 2 browser cases |
| Q6 | (a) the erase deletes the Account's `AccessRefusal` rows | the inventory in the three-Space case (a refusal is recorded first, as the anchor) |
| Q7 | (a) no `Idempotency-Key` | `A_confirmed_deletion_sends_one_DELETE_...` asserts none is sent |
| Q8 | (a) each owned Space name links to its settings | `An_Owner_is_shown_the_Spaces_it_owns_...` |
| Q9 | (a) keep the cookie-clearing `401` | `REV-ACCOUNT-DELETED-BY-HOLDER` (Session: `NoCredential(ClearsTheDeadCookie: true)`); AC11 Slices case |
| Q10 | (a) | not scaffolded (reach gate and ledger are the dev phase's) |

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ActiveSpaceEndpointTests`; the handler test is
fake-backed; isolation rows ride the boundary world; the revocation row rides `RevocationCaseTests`; browser cases use the
assembly's `ClientSurfaceFixture` and `FakeYelloApi`. Engines on a full run: unchanged.

### Scenario map

| AC | Level | Scaffold | Priority | Today |
|---|---|---|---|---|
| AC4, AC5 (Q3) | Wire | `An_Invitation_issued_in_a_Space_its_issuer_was_removed_from_survives_the_issuers_deletion_with_no_issuer` | P0 | red |
| AC1, AC4, AC6, AC9, AC10, AC13 | Wire (log) | `An_Account_in_three_Spaces_is_erased_from_each_and_every_Space_keeps_its_work` | P0 | red |
| AC2, AC8 (I-11) | Wire | `An_Owner_is_refused_409_naming_exactly_its_owned_Space_and_nothing_changes` | P0 | red |
| AC2 | Wire | `An_Owner_of_two_Spaces_and_Member_of_a_third_is_refused_naming_both_in_name_order` | P0 | red |
| AC2 (FR-8) | Wire | `An_Owner_with_a_pending_offer_is_refused_and_the_offer_stays_pending` | P0 | red |
| AC7 | Wire | `The_deleted_address_registers_again_as_a_new_Account_that_inherits_nothing` | P0 | red |
| AC11 (`:790`) | Wire | `A_deleted_Accounts_Session_replaying_an_Invitation_accept_is_the_401_and_not_the_other_Account_refusal` | P0 | red |
| AC2, AC13 (Q2) | Wire (race) | `An_acceptance_queued_ahead_of_the_deletions_leave_makes_the_deletion_refuse_naming_that_Space` | P0 | red |
| AC1 (Q2) | Wire (race) | `A_Membership_accepted_while_the_deletion_waits_is_left_in_one_extra_pass` | P0 | red |
| AC11 (`:250`) | Unit (fakes) | `AuthenticateAccountHandlerTests.A_rehash_that_wrote_no_row_authenticates_nothing` | P0 | red |
| AC12 (I-11) | Isolation | `ISO-DELETE-ACCOUNT-NAMES-ONLY-OWNED-SPACES` (Session 409 identity-only list; Token 401) | P0 | pending 5.4 |
| AC12 (V-3) | Isolation | `ISO-DELETE-ACCOUNT-OWN` (Session 204; Token 401) | P0 | pending 5.4 |
| AC12 (V-3, V-11) | Revocation | `REV-ACCOUNT-DELETED-BY-HOLDER` (Session dead-cookie 401; joined Token 401) | P0 | skipped red |
| AC8 | Architecture | `The_Account_deletion_route_is_Account_scoped_on_Account_settings_and_nothing_else` | P0 | red |
| AC2 | Architecture | `The_owned_Spaces_refusal_has_a_stable_type_and_an_appended_identity_only_Spaces_member` | P0 | red |
| AC4 (Q3) | Architecture | `The_Invitation_issuer_key_is_nullable_and_set_null_when_the_Account_is_deleted` | P0 | red |
| AC5 (Q3) | Architecture | `The_Invitation_contracts_serve_a_nullable_issuer_name` | P0 | red |
| AC5 (Q5) | Architecture | `DesignFoundationGateTests.Tombstone.cs` (radius, tokens, never hidden) | P0 | red |
| AC14 (Q4) | Browser | `Deleting_the_Account_asks_in_place_and_Cancel_and_Esc_send_nothing` | P0 | red |
| AC14 (Q7) | Browser | `A_confirmed_deletion_sends_one_DELETE_and_lands_on_sign_in_saying_Account_deleted` | P0 | red |
| AC3 (Q8) | Browser | `An_Owner_is_shown_the_Spaces_it_owns_and_exactly_two_exits_with_focus_on_the_statement` | P0 | red |
| AC14 | Browser | `A_deletion_that_fails_says_Not_sent_and_keeps_the_confirm` | P1 | red |
| AC14 (NFR-9) | Browser | `The_refusal_state_reflows_at_320_by_256` | P1 | red |
| AC5 | Browser | `InvitationListTests.A_deleted_issuer_renders_as_a_deleted_Account_beside_a_tombstone_avatar` | P0 | red |
| AC5 | Browser | `InvitationViewTests.A_deleted_issuer_is_named_as_a_deleted_Account_on_the_offer` | P0 | red |

**Green today, left active:** none of the new scaffolds - every one needs the route, the migration or the client. The
existing standing gates this story must keep green are `Deleting_the_Account_with_its_Memberships_invalidates_its_Tokens`
(the schema proof), `REV-ACCOUNT-DELETED` (raw SQL), and `AccountScopedSurface` keeping three members.

Not scaffolded here, by design: the deletion handler's unit tests (its port does not exist; the dev phase writes them with
fakes); Q10's reach gate; Q2's "still not empty after one extra pass" refusal (no deterministic interleaving short of a
hostile loop - `AccountDeletionWire.DeletionInterruptedType` is there for the dev phase's case); the text-spacing variant in
`TextSpacingTests` (the reflow case here has none); the 11-row plant table (the dev phase's Task 8 - plants modify product
code).

## Step 4 - Generated scaffolds (TDD red phase)

Every red scaffold is `[Fact(Skip = "red: AC<n> - <why>")]`; the isolation rows are `PendingUntil: "5.4"`; the revocation
row carries `Skip = "red: AC12 - ..."`. Execution mode resolved to **sequential**.

| File | Methods / rows | Red (skipped / pending) | Active, green today | ACs |
|---|---|---|---|---|
| `tests/Yello.Tests.Slices/.../ActiveSpaceEndpointTests.DeleteAccount.cs` (new) | 7 | 7 | 0 | AC1, AC2, AC4-AC7, AC9-AC11, AC13 |
| `tests/Yello.Tests.Slices/.../ActiveSpaceEndpointTests.DeleteAccountRace.cs` (new) | 2 | 2 | 0 | AC1, AC2, AC13 |
| `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/AuthenticateAccountHandlerTests.cs` (1 added) | 1 | 1 | 0 | AC11 |
| `tests/Yello.Tests.Isolation/IsolationCases.AccountDeletion.cs` (new) | 2 rows | 2 (0 generated while pending) | 0 | AC12 |
| `tests/Yello.Tests.Revocation/RevocationCases.cs` (1 row added) | 1 row x 2 surfaces | 2 (skipped) | 0 | AC12 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.DeleteAccount.cs` (new) | 4 | 4 | 0 | AC2, AC4, AC5, AC8 |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.Tombstone.cs` (new) | 3 | 3 | 0 | AC5 |
| `tests/Yello.Tests.Client/AccountSettingsTests.cs` (5 added) | 5 | 5 | 0 | AC3, AC14 |
| `tests/Yello.Tests.Client/InvitationListTests.cs` (1 added) | 1 | 1 | 0 | AC5 |
| `tests/Yello.Tests.Client/InvitationViewTests.cs` (1 added) | 1 | 1 | 0 | AC5 |

Supporting (no cases): `tests/Yello.Tests.Shared/AccountDeletionWire.cs` (stand-ins, the deletion as the browser sends it,
the keep-the-old-cookie variant, the refusal's list reader); `tests/Yello.Tests.Client/FakeYelloApi.AccountDeletion.cs` and
one hook line at the top of `FakeYelloApi.SpaceDeletion.cs`'s `SpaceDeletionAsync` (the dispatcher was at its cognitive
complexity limit); one spread line in `IsolationCases.OwnershipOffers.cs`; `ListedInvitation.IssuerName` and
`OfferedInvitation.IssuerName` in `InvitationWire.cs` made `string?`, restating Q3 (a)'s contract.

### Discovery runs (no full run, per the phase's policy)

Build: `dotnet build Yello.slnx` - 0 warnings, 0 errors.

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Architecture, whole suite (log `artifacts/test-runs/5-4-delete-your-account-atdd-architecture.log`) | 520 | 7 (the new red gates) | 513 | 0 |
| - `RefusalBoundaryGateTests.DeleteAccount.cs` | 4 | 4 | 0 | 0 |
| - `DesignFoundationGateTests.Tombstone.cs` | 3 | 3 | 0 | 0 |
| Slices, `--filter-method` on the 10 new methods (log `...-atdd-slices.log`) | 10 | 10 | 0 | 0 |
| - `.DeleteAccount.cs` | 7 | 7 | 0 | 0 |
| - `.DeleteAccountRace.cs` | 2 | 2 | 0 | 0 |
| - `AuthenticateAccountHandlerTests.A_rehash_that_wrote_no_row_authenticates_nothing` | 1 | 1 | 0 | 0 |
| Slices, `AuthenticateAccountHandlerTests` whole (the fake's new flag leaves the rest green) | 9 | 1 | 8 | 0 |
| Isolation, `IsolationCaseListTests` (GATE-001..004, route coverage, pending rows; log `...-atdd-isolation.log`) | 16 | 0 | 16 | 0 |
| Revocation, `Every_revocation_case_declares_an_expectation_for_every_surface` (the new row declares both; log `...-atdd-revocation.log`) | 1 | 0 | 1 | 0 |
| Client, `--filter-method` on the 7 new methods (log `...-atdd-client.log`) | 7 | 7 | 0 | 0 |

The revocation theory's rows are not enumerated by the runner's listing (theory data is not pre-enumerated there), so the
new row's two skipped instances were not run alone; running the theory runs every row.

## Activation order (riskiest assumption first)

1. `An_Invitation_issued_in_a_Space_its_issuer_was_removed_from_survives_the_issuers_deletion_with_no_issuer`, with the gate
   `The_Invitation_issuer_key_is_nullable_and_set_null_when_the_Account_is_deleted` - **the riskiest assumption**: that the
   engine's cascading `SET NULL` from `Account` reaches `Invitation` rows in a Space whose key is not set, past the Space-only
   policy. Activate it with the migration and the route; if the cascade is blocked or filtered, take Q3 (b) and correct the
   scaffold (record it in the Debug Log).
2. `The_Invitation_contracts_serve_a_nullable_issuer_name`.
3. `The_Account_deletion_route_is_Account_scoped_on_Account_settings_and_nothing_else` and
   `The_owned_Spaces_refusal_has_a_stable_type_and_an_appended_identity_only_Spaces_member`; swap the stand-ins in
   `AccountDeletionWire` for the contract members; widen `Every_Account_scoped_endpoint_names_an_AD_24_surface` and the
   `SpaceSwitcherGateTests.Isolation` lists in the same change.
4. The three refusal cases (`An_Owner_is_refused_...`, `An_Owner_of_two_Spaces_...`, `An_Owner_with_a_pending_offer_...`).
5. `An_Account_in_three_Spaces_is_erased_from_each_and_every_Space_keeps_its_work` (the inventory included).
6. The two race cases: the acceptance queued first, then the extra leave pass.
7. `The_deleted_address_registers_again_...`, `A_deleted_Accounts_Session_replaying_...`, and the rehash handler test.
8. The isolation rows made live (`PendingUntil` removed, GATE-003) and `REV-ACCOUNT-DELETED-BY-HOLDER` unskipped.
9. The tombstone CSS gates, then the Invitation row and view cases, then the five Account settings cases.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Findings for the dev phase

1. **`IdempotencyRecord.AccountId` is not in AC9's expected set.** The Space-scoped `IdempotencyRecord` carries the acting
   Account's id with no foreign key to `Account`, so a keyed write the Account made in any Space survives the deletion with
   its id, and no Account-scoped step can reach it. The three-Space case makes no keyed write, so its inventory expects only
   `OwnershipOffer.RecipientMembershipId`; the inventory reads the catalogue, so any keyed write turns it red. Decide whether
   the per-Space leave erases them or the story records them as retained until expiry (low; not a blocker).
2. **Every registered Account owns its Personal Space** (AD-22), so "an Account owning no Space" exists only after story
   3.3's deletion of that Space. Every scaffold that deletes an Account first deletes the Personal Space through the product.
3. **Active gates that go red by design when the route lands** - widen them in the same commit:
   `Every_Account_scoped_endpoint_names_an_AD_24_surface`, the `SpaceSwitcherGateTests.Isolation` lists, and
   `IsolationCaseListTests.Every_api_route_the_Host_maps_...` until the two `ISO-DELETE-ACCOUNT-*` rows are live.
4. **Stand-ins:** `AccountDeletionWire.Route`, `AccountOwnsSpacesType`, `DeletionInterruptedType`. Swap every use
   (Slices, Isolation, Revocation, the fake) for the contract members and delete them.
5. **Client locators and copy are this run's assumptions**: the section `main .account-delete-section`; the opener button
   "Delete your Account"; the confirm's heading "Delete your Account?", danger button "Delete Account", "Cancel"; the landing's
   "Account deleted."; the refusal heading and the two sentences from AC3's suggestion; the tombstone class
   `.avatar-tombstone` with `role="img"` and `aria-label="Deleted Account"`; "Sent by a deleted Account" and "A deleted
   Account invited you to {Space}.". A different locator in the built page is corrected here and recorded in the Debug Log,
   never by weakening the assertion.
6. **xUnit filters of different kinds AND together**; every Slices scaffold is in `ActiveSpaceEndpointTests`, so filter by
   `--filter-method "*<name>*"`.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs; Q1 and Q3 decided by Lee, the rest at their recommendations; framework;
      Docker up).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC14 mapped above (AC8's spine amendment and AC13's S-2 are asserted in the Slices cases
      and the route gate; the documents are the dev phase's).
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 5.4; none passes today.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (below).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open.
- [x] No full run (this phase's policy); discovery runs above.

### Seed data against the real schema

- `FK_Membership_Account_AccountId` (Restrict): every Membership names an Account registered through the product; the
  scaffolds that delete the Account expect the product to remove every Membership first (the per-Space leave).
- `AK_Membership_SpaceId_AccountId`: each Account's second and third Memberships are in different Spaces
  (`WireDatabase.AddMembershipAsync`, under the Space's key for the `BLOCK AFTER INSERT` predicate).
- `UX_Membership_SpaceId_Owner`: every seeded Space has exactly one Owner (`SeedNamedSpaceAsync`/`SeedAssignableSpaceAsync`
  with `Role.Owner`); other Owners are written only by the acceptance.
- `FK_Task_Membership` (NO ACTION, `(SpaceId, AssigneeMembershipId)`): assignments are made through the route to a
  Membership in the Task's own Space; the scaffolds expect the leave to unassign before its `DELETE`.
- `FK_Invitation_Account_IssuedByAccountId` (Restrict today): the riskiest-assumption case issues through the product as an
  Admin; under the current key the deletion must fail, which is why it is first in the activation order.
- `FK_ApiToken_Membership_BoundSpaceId_AccountId` (cascade): Tokens are issued in Spaces the Account belongs to, so they die
  with those Memberships; the isolation and revocation actors use a Token bound to the joined Space, never the Personal
  Space they delete first.
- `UX_OwnershipOffer_SpaceId_Pending`: one product offer per Space.

**Next workflow:** `bmad-dev-story` for 5.4, then `bmad-code-review`.
