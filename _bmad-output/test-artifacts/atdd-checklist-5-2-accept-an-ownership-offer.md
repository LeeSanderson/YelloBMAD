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
lastSaved: '2026-11-09'
workflowType: 'testarch-atdd'
storyId: '5.2'
storyKey: '5-2-accept-an-ownership-offer'
storyFile: '_bmad-output/implementation-artifacts/5-2-accept-an-ownership-offer.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-5-2-accept-an-ownership-offer.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the API and browser scaffolds share OwnershipOfferWire and one build, so both were written in the main run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptOwnershipOffer.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptOwnershipOfferRace.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ReadMyOwnershipOffer.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeclineOwnershipOffer.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.OwnershipOfferAnswers.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.AcceptOwnershipOffer.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.OfferIndicator.cs'
  - 'tests/Yello.Tests.Client/OwnershipOfferTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.OwnershipOfferAnswers.cs'
  - 'tests/Yello.Tests.Shared/OwnershipOfferWire.Recipient.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/5-2-accept-an-ownership-offer.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-5-1-offer-ownership-of-a-space-and-tell-the-recipient.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 5, Story 5.2: Accept an Ownership Offer

## Story summary

As the person being offered a Space, I want to understand what accepting commits me to and accept in one clean step, so
that ownership arrives by my own agreement and the Space never holds zero or two Owners. Requirements: **FR-42** (the accept
half), AR-13, AR-16, AR-12, UX-DR9, UX-DR37, UX-DR41, with AD-5, AD-9, AD-26, AD-27 binding and test-design rows R9, S-2,
V-10 and A-14. Three new routes, all under `ReadProjectsAndTasks` and decided by row identity, on both surfaces:
`GET /api/v1/spaces/{spaceId}/ownership-offers/mine` (`200`/`204`), `POST .../ownership-offers/{offerId}/acceptance` (`204`)
and, by Lee's Q3 (a), `POST .../ownership-offers/{offerId}/decline` (`204`). The swap is two ordered `ExecuteUpdate`
statements (demote, then promote) under the Memberships lock; the client gets an accent Offer indicator and a modal dialog.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority. Docker up (server 29.5.3).
- **Story:** `ready-for-dev`, AC1-AC12. **Q1, Q3, Q5 decided by Lee (2026-11-09), all (a)**, recorded under Task 0. Q2, Q4,
  Q6-Q10 scaffolded at their recommendations (a).
- **No progress file existed** for this story (`atdd-checklist-5-2-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`.
- **Knowledge:** core (data-factories, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** the `ActiveSpaceEndpointTests` partials (`OfferableSpaceAsync`, `OfferedAsync`, `OfferOverWireAsync`,
  `RevokeOfferOverWireAsync`, `RemoveOverWireAsync`, `LeaveOverWireAsync`, `ChangeRoleOverWireAsync`, `RenameSpaceAsync`,
  `IssueAsync`, `RefusalsOfAsync`, `CapabilityOf`, `PublishesNaming`, `AssertOneOwnerEachAsync`,
  `AssertNoPendingOfferNamesAMissingMembershipAsync`, `MadePresentAsync`, `TakeApplicationLockAsync`, `MembershipsLockOf`,
  `DropTriggerAsync`, `SoleDispatcherWireAsync`, `KeyHeaders`, `NoKey`), `TaskWire.WaitUntilBlockedOnALockAsync`,
  `WireDatabase`, `MailDrop`, the isolation world (`HostWithAPendingOfferAsync`, `OfferedThroughTheProductAsync`,
  `JoinedThroughTheProductAsync`, `TokenOwnOtherSpace`, `ForeignAsync`, `HoldsIdentity`), and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the routes, the indicator and the
dialog do not exist, so every path, locator and copy string is an assumption written from AC1-AC12 and the Q answers, named
as such in each file's remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| **Q1 [LEE]** | (a) the three routes declare `ReadProjectsAndTasks`; the leave-only gate becomes an allow-list; no `Capability` member | `The_ownership_acceptance_routes_are_identity_decided_and_serve_both_surfaces`; `Only_the_identity_decided_writes_sit_under_ReadProjectsAndTasks`; the Viewer row of the any-Role theory |
| **Q3 [LEE]** | (a) decline built here: same guard, writes `Declined`, no Role change, no publish, no email | `.DeclineOwnershipOffer.cs` (3); the decline isolation rows (4); the no-outbox gate names `DeclineOwnershipOfferHandler`; `Decline_sends_one_decline_and_removes_the_indicator` |
| **Q5 [LEE]** | (a) the indicator follows this tab's answers (arrival, re-read, switch, dialog open, the accept's own answer); no polling | `A_re_arrival_answered_204_removes_the_Offer_indicator`; `Opening_the_dialog_on_an_offer_that_has_closed_shows_the_no_longer_open_state`; the switch case |
| Q2 | (a) `GET .../mine` -> `200`/`204`; `POST .../{offerId}/acceptance` and `.../decline` -> `204`, by offer id | every wire case; `OwnershipOfferWire` stand-ins; route gate |
| Q4 | (a) the mockup's copy, "their", "commitments", the third line kept, no "What Owners can do" link | `The_dialog_explains_both_commitments_and_names_the_offerer` (`Commitments` array) |
| Q6 | (a) both surfaces | route gate (no `SessionOnlyEndpoint`); the `ISO-TOKEN-OWN-OTHER-SPACE-*` rows |
| Q7 | (a) non-recipient = in-Space `404`, unrecorded, byte-identical; `409` only to the named recipient | not-named cases (accept and decline); `ISO-*-NAMING-ANOTHER-MEMBERSHIP` |
| Q8 | (a) demote `Id = OfferedByMembershipId AND Role = Owner`, exactly 1, else `OfferStale` -> `409 ownership-offer-not-pending` | `An_offer_whose_offerer_is_no_longer_the_Owner_is_409_and_rolls_back` |
| Q9 | (a) polite rise (4.7's choice) | `Accept_sends_once_with_one_key_rereads_and_narrates_the_new_Owner_politely` asserts `polite:` and not `alert:` |
| Q10 | (a) the test-only `AFTER UPDATE` trigger | `A_promote_that_meets_a_second_Owner_is_409_owner_role_fixed_and_rolls_back_everything` |

### Status codes checked against comparable endpoints

| Route / answer | Scaffold asserts | Comparable endpoint | Discrepancy |
|---|---|---|---|
| Accept / decline success | `204`, empty body | Invitation acceptance `200`, offer revoke `204` | none with the story (Q2); a body-less transition like the revoke |
| Recipient's read | `200` + 4 members, or `204` | 5.1's pending read (`200`/`204`) | none |
| Non-recipient / unknown / foreign id | boundary `404 not-available`, byte-identical, unrecorded | revoke's foreign id | none |
| Named recipient, offer settled or expired | `409 ownership-offer-not-pending`, unrecorded | revoke's settled offer | none |
| Offerer no longer Owner (seeded) | `409 ownership-offer-not-pending` | none (new) | Q8 (a) |
| Promote meets a second Owner | `409 owner-role-fixed` | Role change's `2601` catch | none |
| Lost Membership (race 3, removal first) | pipeline `404`, recorded `CrossSpace` | 4.5's `LostMembership` | none |
| Former Owner's stale offer (AC9 race) | recorded `403 insufficient-role` naming `offer-ownership-transfer` (was `400`) | every Denied matrix cell | intended change (AC9) |
| Former Owner's revoke after the swap (race 2) | `409 ownership-offer-not-pending` **or** the recorded `403` | - | the story allows either; **the dev phase records which** |
| Former Owner changing the new Owner's Role (AC8) | `403` or `409` | 4.4's Role change | the story allows either; **the dev phase records which** |
| Replay / reused key / malformed key | original `204` / `422` / `400 invalid-idempotency-key` | every write route | none |

### New assumptions this run made (no question covered them)

1. **Temporary stand-ins in `OwnershipOfferWire`** (`tests/Yello.Tests.Shared/OwnershipOfferWire.Recipient.cs`), the 5.1
   precedent: `MineRoute`, `AcceptanceRoute`, `DeclineRoute`, `MineOf`, `AcceptanceOf`, `DeclineOf`. Develop swaps every use
   (Slices, Isolation, the fake) for the contract members and deletes them. `RecipientResponseMembers`,
   `ParseForRecipient` and `OwnershipOfferForRecipientAnswer` are lasting readings.
2. **"Across every Space" is read as "no Space newly broken".** The class database is shared with earlier stories' cases,
   some of which leave a Space with no Owner on purpose (`WireDatabase.SpacesNotHoldingExactlyOneOwnerAsync`'s remark), so
   `No_Space_in_the_database_holds_zero_or_two_Owners_after_the_acceptance_cases` reads every Space before and after its own
   batch (two accepts and a held race) and asserts no Space broke that was whole, plus exactly one Owner in each Space the
   batch touched. Added `WireDatabase.AllSpaceIdsAsync`.
3. **The trigger case** sets `SET NOCOUNT ON` in the trigger, because `ExecuteNonQuery`'s rowcount includes rows a trigger
   writes and the store checks the demote for exactly 1; it is planted after all seeding (EF's `OUTPUT` clause refuses a
   table with an enabled trigger) and dropped in a `finally`. `RECURSIVE_TRIGGERS` is off by default.
4. **Publishes are compared, not counted from zero**: the seeded Owner is never joined (any publish naming it is the
   acceptance's), and the recipient is compared with a bystander who joined the same way.
5. **Isolation rows live in a new partial** (`IsolationCases.OwnershipOfferAnswers.cs`, spread from
   `OwnershipOfferCases()`), not inside `IsolationCases.OwnershipOffers.cs`'s list. Same class, same runner. No
   `ISO-RECORD-KIND-…` row, deliberately: nobody is refused by Role.
6. **Client locators and copy**: indicator `nav.context-bar button.offer-indicator`; the dialog is the page's one
   `[role=dialog][aria-modal=true]`; the dialog's CSS root is `.ownership-offer-dialog` (the radius gate's constant; rename
   it if the dialog takes another class); "Not sent." for `OwnershipOfferNotSent` and the same "Accept ownership" press
   resends; after the accept and after Close on the no-longer-open state, focus lands inside `nav.context-bar`.
7. **The story's `SmallViewportTests` extension, `TextSpacingTests` row and `RoleDropTests` case** are written as sibling
   cases in `OwnershipOfferTests` (the story allows a sibling case), so no active class is edited.
8. **The fake moves the Role after the acceptance's own answer** (`ApplyAnsweredOfferRoles`, one call at the top of
   `DecideAsync`), mirroring the Host's header contract (the `204` echoes the pre-swap Role). A no-op without an
   acceptance. The refusal property is `NextOfferAcceptRefusal` (`NextAcceptRefusal` is the Invitation acceptance's).
9. **A-14 check (iii)** reads each `SetProperty<Role>` setter lambda's value as its last `ldc.i4`, in the order the accept
   path reaches the lambdas (depth-first in IL order, through `MoveNext` and module-local callees). It holds while the
   values are the enum constants; a value in a variable fails it rather than passing.
10. **Requirement traits** carry FR/AR/AD/UX-DR/NFR ids only; R9, S-2, V-10 and A-14 are named in summaries (the 5.1
    precedent).

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ActiveSpaceEndpointTests`; isolation rows
ride the boundary world; `OwnershipOfferTests` is a browser class on the assembly's existing `ClientSurfaceFixture` and
`FakeYelloApi` (no SQL Server). Every Slices case stops the pooled Hosts (`SoleDispatcherWireAsync`) and runs one Host of
its own with its own `MailDrop`, because every case joins Members through an Invitation and makes an offer. Engines on a
full run: unchanged.

### Scenario map

| AC | Level | Scaffold | Priority |
|---|---|---|---|
| AC4, AC5, AC7, AC11 | Wire | `A_named_recipient_of_any_Role_accepts_and_becomes_the_sole_Owner_while_the_offerer_becomes_an_Admin` (x3: admin, member, viewer) | P0 |
| AC4 | Wire | `A_Member_an_Admin_and_the_Owner_who_are_not_named_get_the_in_Space_404_and_nothing_changes` | P0 |
| AC4, AC10 | Wire | `An_offer_no_longer_open_to_its_recipient_is_409_and_changes_nothing` (x4: Revoked, Lapsed, Declined, Expired) | P0 |
| AC5 (Q8) | Wire | `An_offer_whose_offerer_is_no_longer_the_Owner_is_409_and_rolls_back` | P0 |
| AC6 (Q10) | Wire | `A_promote_that_meets_a_second_Owner_is_409_owner_role_fixed_and_rolls_back_everything` | P0 |
| AC7 | Wire | `The_former_Owner_keeps_its_access_as_an_Admin_after_the_acceptance` | P0 |
| AC8 | Wire | `The_new_Owner_is_bound_as_any_Owner_is_and_the_former_Owner_can_leave` | P0 |
| AC11 | Wire (mail) | `An_acceptance_sends_no_email_to_either_party` | P1 |
| AC10 | Wire (log) | `An_acceptance_publishes_MembershipChanged_once_for_each_of_the_two_Accounts_and_refusals_publish_nothing` | P0 |
| AC10 | Wire (idempotency) | `An_acceptance_replays_under_its_key_and_a_new_key_is_refused_409_ownership_offer_not_pending` | P1 |
| AC7 | Wire (invariant) | `No_Space_in_the_database_holds_zero_or_two_Owners_after_the_acceptance_cases` | P0 |
| AC10 race 1 | Wire (race) | `Two_acceptances_of_one_offer_held_on_the_Memberships_lock_commit_one_and_refuse_the_other_409` | P0 |
| AC9, AC10 race 2 | Wire (race) | `An_acceptance_and_the_Owners_revoke_held_on_the_Memberships_lock_end_in_a_serial_outcome` | P0 |
| AC10 race 3 | Wire (race) | `An_acceptance_and_the_removal_of_its_recipient_held_on_the_Memberships_lock_end_in_a_serial_outcome` | P0 |
| AC10 race 4 | Wire (race) | `An_acceptance_and_the_Owners_leave_held_on_the_Memberships_lock_end_in_a_serial_outcome` | P0 |
| AC9 | Wire (race) | `An_acceptance_and_the_Owners_offer_to_another_Member_held_on_the_Memberships_lock_end_in_a_serial_outcome` | P0 |
| AC1 | Wire | `The_named_recipient_reads_its_offer_with_the_captured_offerer_name_and_the_database_time` | P0 |
| AC1 | Wire | `The_recipients_read_answers_204_to_everyone_the_offer_does_not_name_and_for_an_expired_offer` | P0 |
| AC3 (Q3) | Wire | `The_named_recipient_declines_no_Role_moves_and_the_offer_cannot_then_be_accepted` | P0 |
| AC3 (Q3), AC4 | Wire | `A_decline_by_anyone_the_offer_does_not_name_is_the_in_Space_404_and_nothing_changes` | P0 |
| AC3 (Q3), AC10 | Wire | `A_decline_replays_under_its_key_and_a_new_key_is_refused_409_ownership_offer_not_pending` | P1 |
| AC12 | Isolation | 10 rows, `PendingUntil: "5.2"` (below) | P0 |
| AC4, AC5, AC6, AC11, AC12 | Architecture | 5 gates in `RefusalBoundaryGateTests.AcceptOwnershipOffer.cs` | P0 |
| AC1, AC2 | Architecture | 4 gates in `DesignFoundationGateTests.OfferIndicator.cs` | P0 |
| AC1-AC3, AC7, AC12 | Browser | `OwnershipOfferTests`, 17 methods | P0 |

Isolation rows (`IsolationCases.OwnershipOfferAnswers.cs`): `ISO-ACCEPT-OWNERSHIP-OFFER-FOREIGN-SPACE`,
`ISO-DECLINE-OWNERSHIP-OFFER-FOREIGN-SPACE` (boundary `404`, recorded `CrossSpace`; the host's offer still `Pending` and the
host still Owner), `ISO-READ-MY-OWNERSHIP-OFFER-FOREIGN-SPACE` (boundary `404`), `ISO-ACCEPT-FOREIGN-OFFER-ID-IN-OWN-SPACE`,
`ISO-DECLINE-FOREIGN-OFFER-ID-IN-OWN-SPACE` (another Space's offer id naming the actor there, under the actor's own Space:
`SameAsAnchor(404, not-available)`, unrecorded), `ISO-ACCEPT-OFFER-NAMING-ANOTHER-MEMBERSHIP`,
`ISO-DECLINE-OFFER-NAMING-ANOTHER-MEMBERSHIP` (`SameAsAnchor(404, not-available)`), `ISO-TOKEN-OWN-OTHER-SPACE-ACCEPT-OWNERSHIP-OFFER`,
`ISO-TOKEN-OWN-OTHER-SPACE-DECLINE-OWNERSHIP-OFFER` (Session `204` with the Role or state written; Token `SameAsNonMember`, the
offer still `Pending`), `ISO-TOKEN-OWN-OTHER-SPACE-READ-MY-OWNERSHIP-OFFER` (Session `204`).

Not scaffolded here, by design: handler unit tests (`AcceptOwnershipOfferHandler`, `DeclineOwnershipOfferHandler`,
`ReadMyOwnershipOfferHandler`) cannot compile before Task 2 declares them; the dev phase writes them with fakes. The
ledger `:929` fold-in (S-2 on six 5.1 cases) edits active tests and is the dev phase's. The `MatrixRoute` table is not
touched (the new routes declare an already-covered capability). The 16-row plant table is the dev phase's.

## Step 4 - Generated scaffolds (TDD red phase)

Every scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; every isolation row is
`PendingUntil: "5.2"` with a reason. Execution mode resolved to **sequential**.

| File | Methods / rows | Cases when active | ACs |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptOwnershipOffer.cs` | 11 (all skipped) | 16 | AC4, AC5, AC6, AC7, AC8, AC10, AC11 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptOwnershipOfferRace.cs` | 5 (all skipped) | 5 | AC9, AC10 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ReadMyOwnershipOffer.cs` | 2 (all skipped) | 2 | AC1 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeclineOwnershipOffer.cs` | 3 (all skipped) | 3 | AC3 (Q3 a), AC4, AC10 |
| `tests/Yello.Tests.Isolation/IsolationCases.OwnershipOfferAnswers.cs` | 10 rows, `PendingUntil: "5.2"` | 20 (0 generated while pending) | AC12 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.AcceptOwnershipOffer.cs` | 5 (all skipped) | 5 | AC4, AC5, AC6, AC11, AC12 |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.OfferIndicator.cs` | 4 (all skipped) | 4 | AC1, AC2 |
| `tests/Yello.Tests.Client/OwnershipOfferTests.cs` (new class, existing assembly fixture) | 17 (all skipped) | 20 | AC1, AC2, AC3, AC7, AC12 |

Supporting (no cases): `tests/Yello.Tests.Shared/OwnershipOfferWire.Recipient.cs` (stand-ins and readings);
`WireDatabase.AllSpaceIdsAsync`; `tests/Yello.Tests.Client/FakeYelloApi.OwnershipOfferAnswers.cs` (the three routes as
the Host answers them: `OffersForMe`, `NextOfferAcceptRefusal`, `LoseNextAcceptAnswer`, `MyOfferReads`, `AcceptancesSent`,
`DeclinesSent`, `SpaceArrivalReads`); three dispatch lines in `FakeYelloApi.OwnershipOffers.cs`; one call in
`FakeYelloApi.DecideAsync`; one spread line in `IsolationCases.OwnershipOffers.cs`.

### Discovery runs (no full run, per the phase's policy)

Build: `dotnet build Yello.slnx` - 0 warnings, 0 errors.

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Slices, `--filter-method` on the 21 new methods | 21 | 21 | 0 | 0 |
| - of which `.AcceptOwnershipOffer.cs` | 11 | 11 | 0 | 0 |
| - of which `.AcceptOwnershipOfferRace.cs` | 5 | 5 | 0 | 0 |
| - of which `.ReadMyOwnershipOffer.cs` | 2 | 2 | 0 | 0 |
| - of which `.DeclineOwnershipOffer.cs` | 3 | 3 | 0 | 0 |
| Isolation, `IsolationCaseListTests` (GATE-001..003, route coverage, cross product) | 16 | 0 | 16 | 0 |
| Architecture, whole suite | 507 | 9 (the new gates) | 498 | 0 |
| - of which `RefusalBoundaryGateTests.AcceptOwnershipOffer.cs` | 5 | 5 | 0 | 0 |
| - of which `DesignFoundationGateTests.OfferIndicator.cs` | 4 | 4 | 0 | 0 |
| Client, `OwnershipOfferTests` | 17 | 17 | 0 | 0 |
| Client, `OwnershipSectionTests` + `RoleDropTests` (regression check of the changed fake) | 19 | 0 | 19 | 0 |

### Green today, left active (standing regression gate)

None. Every new scaffold depends on a route, type, store method or component that does not exist yet.

### Red verified (temporary unskip, restored in `finally` from memory; never `git checkout`)

| Scaffold | Failed with | Reason matches |
|---|---|---|
| `RefusalBoundaryGateTests.The_ownership_acceptance_routes_are_identity_decided_and_serve_both_surfaces` | "Collections differ ... Actual: []" | yes - no route is mapped |
| `RefusalBoundaryGateTests.The_ownership_acceptance_changes_Roles_by_two_ordered_ExecuteUpdate_calls_and_never_SaveChanges` | "OwnershipOfferStore declares no AcceptAsync." | yes - the anchor |

The file was restored byte for byte and the Architecture project rebuilt (0 warnings, 0 errors).

### Seed data against the real schema

- `FK_Membership_Account_AccountId` (Restrict): every Membership names an Account registered through the product
  (`OfferableSpaceAsync` registers the Owner and every joiner); no Account is inserted by hand.
- `AK_Membership_SpaceId_AccountId`: one Membership per Account per Space; every joiner is a fresh Account.
- `UX_Membership_SpaceId_Owner`: one Owner per seeded Space (`SeedAssignableSpaceAsync`); every other `Owner` is written by
  the acceptance itself. The one deliberate second `Owner` is the trigger case's, inside the acceptance's transaction, where
  the demote has left zero.
- `FK_OwnershipOffer_Space_SpaceId`: every seeded offer names an existing Space; the `BLOCK AFTER INSERT` predicate is met by
  `AddOwnershipOfferAsync` writing under the Space's key.
- `UX_OwnershipOffer_SpaceId_Pending`: at most one `Pending` row per Space. Terminal and expired seeds are written after the
  live offer was revoked; the `OfferStale` and expired-read seeds are written in Spaces with no product offer.
- No foreign key from `OwnershipOffer` to `Membership`: the `OfferStale` seed names an Admin's Membership as
  `OfferedByMembershipId`, which only the absence of that key allows.
- `Account.DisplayName` (`nvarchar(128)`, not null): the captured-name case renames the Owner to a short non-empty name.
- The `Membership` `BLOCK AFTER UPDATE` predicate: the trigger's `UPDATE` runs in the Host's session under the same Space key,
  on a row of the same Space.

### Findings for the dev phase

1. **Active tests that go red by design when the routes land** - widen them in the same commit, do not skip them:
   - `RefusalBoundaryGateTests.The_leave_route_is_the_one_write_under_ReadProjectsAndTasks` - replace it with
     `Only_the_identity_decided_writes_sit_under_ReadProjectsAndTasks` (Q1 (a)).
   - The isolation route-coverage gates as soon as a route is mapped: make the 10 rows live and swap the stand-ins.
   - Browser cases that count requests on arrival (5.1's lesson; for example `RoleDropTests.An_idle_tab_sends_no_request`,
     `MembershipSectionTests` read counts) may see the context bar's new `mine` read. The fake answers it `204` by default.
2. **Two answers the story leaves open, asserted as either**: the former Owner's revoke after the swap (race 2: `409` or
   the recorded `403`) and the former Owner's Role change on the new Owner (AC8: `403` or `409`). Record the observed
   answer and narrow the assertion.
3. **xUnit filters of different kinds AND together**; every Slices scaffold here is in `ActiveSpaceEndpointTests`, so filter
   by `--filter-method "*.<name>"`.
4. **Do not name any Application type with "Authority"** (two `Assert.Single` gates). No `Capability` member is added.
5. **The A-14 gate walks module-local callees**, so an internal `MembershipStore` helper for the two statements is seen; its
   setter values must be written as `Role.Admin`/`Role.Owner` constants for check (iii).

## Activation order (riskiest assumption first)

1. `A_named_recipient_of_any_Role_accepts_and_becomes_the_sole_Owner_while_the_offerer_becomes_an_Admin` - the riskiest
   assumption: that two `ExecuteUpdate` statements, demote then promote, run inside the pipeline's Space-scoped unit of work
   (the `Membership` `BLOCK AFTER UPDATE` predicate, the string-converted `Role`, the filtered Owner index) and move ownership
   without a `2601`; the Viewer row proves identity over Role.
2. `A_promote_that_meets_a_second_Owner_is_409_owner_role_fixed_and_rolls_back_everything` - the `2601` mapping and the
   whole-transaction rollback.
3. `An_offer_whose_offerer_is_no_longer_the_Owner_is_409_and_rolls_back`, then the A-14 gate.
4. The not-named, no-longer-open and recipient's-read cases; then races 1-4 and the AC9 race.
5. Publishes, idempotency, no email, the every-Space invariant, the former Owner's access, the new Owner bound.
6. The decline (3 cases), the route, allow-list, contract and no-outbox gates (with the leave gate replaced), and the 10
   isolation rows made live.
7. The design gates and the browser cases.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Implementation guidance

- **Contracts:** `OwnershipOfferRoutes.Mine`/`MineOf`, `Acceptance`/`AcceptanceOf`, `Decline`/`DeclineOf`;
  `OwnershipOfferForRecipientResponse(Guid Id, string OffererName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt)`. Then
  swap the `OwnershipOfferWire` stand-ins and delete them.
- **Application:** `IOwnershipOfferStore.ReadMineAsync`, `AcceptAsync`, a decline; `OwnershipOfferAcceptance { Accepted,
  NotPending, NotFound, LostMembership, OfferStale, OwnerRoleFixed }`; `NoLongerOwner` on the offer and revoke outcomes;
  `AcceptOwnershipOfferHandler`, `DeclineOwnershipOfferHandler`, `ReadMyOwnershipOfferHandler` (no `IOutbox`).
- **Infrastructure:** `OwnershipOfferStore.AcceptAsync` - lock, guarded transition with the identity clause, demote (exactly
  1), promote (exactly 1, `2601/2627` -> `OwnerRoleFixed`), two `Record`s; `ReadMineAsync`; the decline; the revoke's
  actor-still-Owner clause.
- **Host:** `MapReadMyOwnershipOffer`, `MapAcceptOwnershipOffer`, the decline, each `.RequiresCapability(ReadProjectsAndTasks)`.
- **Client:** `OwnershipOfferState`, the indicator in `ContextBar.razor`, `OwnershipOfferDialog.razor`, copy in
  `ClientCopy.resx` (including `OwnershipOfferNotSent`), `.offer-indicator` and the dialog's CSS.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs and Lee's Q1, Q3, Q5 decisions; framework; Docker up).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC12 each mapped above.
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 5.2 with a reason; none is
      green today.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (constraints listed above).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open; the temporary unskip was restored from memory in a `finally`.
- [x] No full run (this phase's policy); discovery runs above.

**Next workflow:** `bmad-dev-story` for 5.2, then `bmad-code-review`.
