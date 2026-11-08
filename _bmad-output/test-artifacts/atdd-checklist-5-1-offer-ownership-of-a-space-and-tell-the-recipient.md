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
storyId: '5.1'
storyKey: '5-1-offer-ownership-of-a-space-and-tell-the-recipient'
storyFile: '_bmad-output/implementation-artifacts/5-1-offer-ownership-of-a-space-and-tell-the-recipient.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-5-1-offer-ownership-of-a-space-and-tell-the-recipient.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the API and browser scaffolds share OwnershipOfferWire and one build, so both were written in the main run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OfferOwnership.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OfferOwnershipOutbox.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OfferOwnershipRace.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OwnershipOfferLapse.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OwnershipOfferSchema.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeOwnershipOffer.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.OwnershipOffers.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.OwnershipOffers.cs'
  - 'tests/Yello.Tests.Client/OwnershipSectionTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.OwnershipOffers.cs'
  - 'tests/Yello.Tests.Shared/OwnershipOfferWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/5-1-offer-ownership-of-a-space-and-tell-the-recipient.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-4-6-assign-a-task-and-be-told.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 5, Story 5.1: Offer ownership of a Space, and tell the recipient

## Story summary

As an Owner who wants to hand a Space on, I want to offer ownership to someone already in it and have them know it is
waiting. Requirements: **FR-8**, **FR-43**, AR-12, AR-31, AR-33, UX-DR14, with AD-5, AD-9, AD-10, AD-18, AD-23, AD-26,
AD-27 binding and test-design rows I-14 and S-2. Three new routes, all under `OfferOwnershipTransfer` (Owner only, both
surfaces): `POST /api/v1/spaces/{spaceId}/ownership-offers` (`201` + offer), `GET .../ownership-offers/pending` (`200` or
`204`), `DELETE .../ownership-offers/{offerId}` (`204`). An `OwnershipOffer` table with one FK (Space, cascade), a filtered
unique index on `Pending`, a database-clock `ExpiresAt`, a written lapse on removal and leave, one email through the outbox,
and the Owner-only Ownership section in Space settings.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority. Docker up (server 29.5.3).
- **Story:** `ready-for-dev`, AC1-AC13. **Q1, Q2, Q4, Q5 decided by Lee (2026-11-08), all (a)**, recorded under Task 0.
  Q3, Q6-Q12 scaffolded at their recommendations (a).
- **No progress file existed** for this story (`atdd-checklist-5-1-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`.
- **Knowledge:** core (data-factories, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** the `ActiveSpaceEndpointTests` partials (`SignedInAsync`, `RegisteredAndSignedInAsync`,
  `JoinedThroughTheProductAsync`, `SeedAssignableSpaceAsync`, `SeedNamedSpaceAsync`, `RemoveOverWireAsync`,
  `LeaveOverWireAsync`, `DeleteSpaceOverWireAsync`, `RenameSpaceAsync`, `IssueAsync`, `NamedProjectAsync`,
  `TaskInProjectAsync`, `PlantFailingIdempotencyTriggerAsync`, `DropTriggerAsync`, `RefusalsOfAsync`, `CapabilityOf`,
  `PublishesNaming`, `AssertOneOwnerEachAsync`, `MadePresentAsync`, `TakeApplicationLockAsync`, `MembershipsLockOf`,
  `SoleDispatcherWireAsync`), `TaskWire.WaitUntilBlockedOnALockAsync`, `LabelWire`, `MembershipWire`, `InvitationWire`,
  `WireDatabase`, `MailDrop`, the isolation world (`OwnerOfTwoAsync`, `JoinedThroughTheProductAsync`, `TokenOwnOtherSpace`,
  `HoldsIdentity`), and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the routes, the table, the email and
the Ownership section do not exist, so every path, column and locator is an assumption written from AC1-AC13 and the Q
answers, named as such in each file's remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| **Q1 [LEE]** | (a) the next offer writes `Lapsed` on a time-expired `Pending` row under the lock, before its insert | `A_time_expired_pending_offer_is_lapsed_by_the_next_offer_and_does_not_block_it` |
| **Q2 [LEE]** | (a) no FK to `Membership` or `Account`; FK to `Space` only, cascade; `OffererName` captured | `An_OwnershipOffer_cascades_from_its_Space_and_references_nothing_else`; the EF-model gate (`Assert.Single(GetForeignKeys())`); `WireDatabase.AddOwnershipOfferAsync` names Membership ids no key checks; AC1 row equality on `OffererName` |
| **Q4 [LEE]** | (a) `LapseNamingAsync` in `RemoveCoreAsync` and `LeaveAsync` before the guarded `DELETE`, rolled back with it | the four `OwnershipOfferLapse.cs` cases; race 1 and race 3; `Only_the_offer_store_writes_an_OwnershipOffer_state` names `OwnershipOfferWrites` |
| **Q5 [LEE]** | (a) one guarded insert under `TakeMembershipsAsync` Exclusive; the revoke takes the same lock; self-offer `400 invalid-offer-recipient` | races 1-3 (both requests wait on the held lock); `Offering_to_oneself_...` |
| Q3 | (a) `POST` -> `201`, `GET .../pending` -> `200`/`204`, `DELETE .../{offerId}` -> `204` | every wire case; route gates; isolation rows |
| Q6 | (a) `400 invalid-offer-recipient` (one body), `409 ownership-offer-pending`, `409 ownership-offer-not-pending` | refusal cases; problem-type gate |
| Q7 | (a) kind `ownership-offered`; `Render(string, string, Uri, DateTimeOffset)`; subject "{offerer} offered you ownership of {space} on Yello"; link with no ids | email cases; template gate; `OwnershipOfferWire.IsOwnershipOfferEmail` |
| Q8 | (a) section "Ownership" after Invitations; picker with no default; in-place confirm; Revoke without confirm | `OwnershipSectionTests` |
| Q9 | (a) Offer indicator re-owned to 5.2 | none (ledger is the dev phase's) |
| Q10 | (a) Account-deletion arms met vacuously, owned by 5.4 | none |
| Q11 | (a) the read returns `ReadAt`; days = `ExpiresAt - ReadAt` | `The_pending_read_names_...`; client browser-clock case |
| Q12 | Admin has no route access | `An_Admin_is_refused_the_read_and_the_revoke_of_the_pending_offer`; `ISO-RECORD-KIND-REVOKE-OWNERSHIP-OFFER-ADMIN` |

### Status codes checked against comparable endpoints (orchestrator's instruction)

| Route / answer | Scaffold asserts | Comparable endpoint | Discrepancy |
|---|---|---|---|
| Offer success | `201`, body = offer, `Location` ends with the pending read | Label/Project/Task/Space create: `201` + `Location` set on the response (`Results.Created` is banned, ARCH-009) | **Not** the Invitation issue's `204`: that one answers alike whether or not the address has an Account; an offer names a Membership and hides nothing. Kept the story's `201`. **Location** points at the pending read (the story's text), not at the resource itself as Label/Project create do, because no `GET` exists on `.../{offerId}`. Dev: answer `Results.Json(..., 201)` with `Location` set on `httpContext.Response`, so a replay carries both. |
| Body naming no Membership (`{}`, `[]`, `"x"`, `null`, `{emailAddress}`) | `400 invalid-offer-recipient` | assignment: `400 invalid-assignee` for the same bodies | **Story says only "400 (the missing MembershipId)"**; the scaffolds pin the type, by the assignment precedent. |
| Unknown, foreign or own-Owner recipient | `400 invalid-offer-recipient`, byte-identical, unrecorded | assignment `400 invalid-assignee` | none |
| Second offer | `409 ownership-offer-pending` | Invitation re-issue `409 invitation-pending` | none |
| Revoke | `204` / `409 ownership-offer-not-pending` / boundary `404` unrecorded | Invitation revoke `204` / `409 invitation-not-pending` / boundary `404` | none |
| Pending read, none actionable | `204`, empty body | **no Space-scoped `GET` answers `204`** (only `GET /api/v1/session`) | Story's Q3 choice kept and named here. |
| Offering Owner's leave / own removal | `409 ownership-held` | 4.5's answers, unchanged | none |
| Admin read / revoke | `403 insufficient-role` naming `offer-ownership-transfer`, recorded | every Denied matrix cell | none |
| Replay / reused key / malformed key | original answer / `422` / `400 invalid-idempotency-key` | every write route | none |

### New assumptions this run made (no question covered them)

1. **Temporary stand-ins in `OwnershipOfferWire`** (`tests/Yello.Tests.Shared`), the 4.4-4.6 precedent: `OffersRoute`,
   `PendingRoute`, `OfferRoute`, `OffersOf`, `PendingOf`, `OfferOf`, `OwnershipOfferPendingProblemType`,
   `OwnershipOfferNotPendingProblemType`, `InvalidOfferRecipientProblemType`. Develop swaps every use (Slices, Isolation,
   the fake) for the contract members and deletes them. `CapabilityCode`, `OwnershipOfferedKind`, `ExpiryDays`,
   `ResponseMembers`, `Parse`, `IsOwnershipOfferEmail`, `OfferEmailsTo`, `LinksIn`, `HoldsAGuid` are lasting readings.
2. **Every new wire case stops the pooled Hosts** (`SoleDispatcherWireAsync`) and runs `HostProcess.RunAsync` with its own
   `MailDrop`: every case joins a recipient through an Invitation (mail), and every offer enqueues an email. Cost: one Host
   start per case.
3. **Lapse and Space-deletion cases sit in a new partial** (`.OwnershipOfferLapse.cs`), not inside `.RemoveMembership.cs` /
   `.LeaveSpace.cs` / `.DeleteSpace.cs`. Same class, no new engine. The populated-Space deletion case is **not** edited now
   (it is active and would go red); Develop adds `"OwnershipOffer"` to its `SpaceScopedTables` and an offer to
   `PopulateSpaceAsync` once the table exists, and may then fold `Deleting_a_Space_with_a_pending_offer_takes_the_offer_with_it` into it.
4. **The "no `MembershipChanged`" check** compares publishes rather than counting zero: none names the Owner, and the
   Viewer recipient is named exactly as often as an Admin who joined the same way and was not offered anything (the joins
   themselves publish).
5. **The offer email is told apart** from the recipient's Invitation email by its subject ("offered you ownership of").
   The disclosure check reads subject and both bodies, not the envelope (its `To` is the recipient's own address).
6. **Client locators** (`OwnershipSectionTests`): `main .ownership-section`, heading "Ownership", radios named by label,
   buttons "Offer ownership", "Offer", "Cancel", "Revoke", the copy in AC9 verbatim, a `time[datetime]`. The fake seeds the
   picker's names both as the Members list and as the assignment candidates, so either read works. The browser clock is set
   with `page.Clock.SetFixedTimeAsync` 400 days ahead of the fake's `ServerNow`.
7. **Requirement traits** carry FR/AR/AD/NFR/UX-DR ids only; I-14 and S-2 are named in summaries (no precedent for test-design
   ids as `Requirement` values).

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ActiveSpaceEndpointTests`; isolation rows
ride the boundary world; `OwnershipSectionTests` is a browser class on the assembly's existing `ClientSurfaceFixture` and
`FakeYelloApi` (no SQL Server). Engines on a full run: unchanged.

### Scenario map

| AC | Level | Scaffold | Priority |
|---|---|---|---|
| AC1 | Wire | `The_Owner_offers_ownership_to_a_Membership_of_any_Role_and_one_offer_is_pending` (x3) | P0 |
| AC1 | Wire | `An_offer_to_an_address_an_Invitation_or_an_Account_is_the_400_a_random_id_gets` | P0 |
| AC1 | Wire | `A_body_that_names_no_Membership_is_400_invalid_offer_recipient` (x4) | P1 |
| AC1 (Q5) | Wire | `Offering_to_oneself_to_a_random_id_or_to_another_Spaces_Membership_is_one_400_and_unrecorded` | P0 |
| AC2 | Wire | `An_offer_moves_no_Role_and_the_Owner_keeps_every_Owner_capability` | P0 |
| AC3 | Wire | `A_second_offer_while_one_is_pending_is_409_and_one_offer_stays_pending` | P0 |
| AC3 (Q1) | Wire | `A_time_expired_pending_offer_is_lapsed_by_the_next_offer_and_does_not_block_it` | P0 |
| AC3 | Wire (schema) | `The_database_refuses_a_second_pending_offer_in_a_Space_by_its_filtered_unique_index` | P0 |
| AC4 | Wire | `The_Owner_revokes_a_pending_offer_and_every_Membership_and_Role_is_as_it_was` | P0 |
| AC4, AC10 | Wire | `Revoking_an_offer_that_is_no_longer_pending_is_409_and_changes_nothing` (x3) | P0 |
| AC4 | Wire | `Revoking_an_id_that_names_no_offer_in_this_Space_is_the_boundary_404_and_is_not_recorded` | P0 |
| AC4, AC13 | Wire | `A_revoke_replays_under_its_key_and_a_new_key_is_refused_409_ownership_offer_not_pending` | P1 |
| AC5 (Q4) | Wire | `Removing_the_recipient_lapses_the_offer_and_changes_no_other_Role` (x2), `The_recipient_leaving_lapses_the_offer`, `A_refused_removal_of_the_recipient_leaves_the_offer_pending`, `Removing_another_Member_leaves_the_offer_pending` | P0 |
| AC6 | Wire | `The_offering_Owner_still_cannot_leave_or_be_removed_while_an_offer_is_pending` | P0 |
| AC7 | Wire | `One_offer_sends_one_email_to_the_recipient_and_none_to_the_offerer` | P0 |
| AC7 | Wire | `A_failure_after_the_enqueue_rolls_back_the_offer_its_email_and_its_send_record` | P0 |
| AC7 | Wire | `Revoking_removing_the_recipient_or_its_leaving_sends_nothing` (x3) | P0 |
| AC8 (I-14) | Wire | `The_offer_email_names_the_Space_and_the_offerer_and_discloses_nothing_else` | P0 |
| AC8, AC12 | Wire | `Neither_an_address_nor_a_display_name_reaches_the_Host_log_on_offer_read_or_revoke` | P1 |
| AC9 | Wire | `The_pending_read_names_the_recipient_and_the_expiry_with_the_database_time`, `The_pending_read_answers_204_when_no_offer_is_actionable`, `An_Admin_is_refused_the_read_and_the_revoke_of_the_pending_offer` | P0 |
| AC10 | Wire | `A_fresh_offer_expires_seven_days_after_the_database_clock`; schema `The_offer_ExpiresAt_defaults_to_seven_days_after_the_database_clock` | P0 |
| AC11 | Wire | `Deleting_a_Space_with_a_pending_offer_takes_the_offer_with_it`; schema `An_OwnershipOffer_cascades_from_its_Space_and_references_nothing_else` | P0 |
| AC12 | Wire (schema) | `The_OwnershipOffer_table_is_under_the_Space_policy_with_its_filter_and_both_block_predicates` | P0 |
| AC13 | Wire (idempotency) | `A_replayed_offer_answers_its_original_201_and_stores_and_records_nothing_more` | P1 |
| AC13 races | Wire (race) | race 1 `An_offer_and_the_removal_of_its_recipient_held_on_the_Memberships_lock_end_in_a_serial_outcome`; race 2 `Two_offers_held_on_the_Memberships_lock_commit_one_and_refuse_the_other_409`; race 3 `A_revoke_and_the_recipients_leave_held_on_the_Memberships_lock_end_in_a_serial_outcome` | P0 |
| AC13 | Isolation | 12 rows, `PendingUntil: "5.1"` (below) | P0 |
| AC1-AC12 | Architecture | 12 gates in `RefusalBoundaryGateTests.OwnershipOffers.cs` | P0 |
| AC9 | Browser | `OwnershipSectionTests`, 9 methods | P0 |

Isolation rows (`IsolationCases.OwnershipOffers.cs`, spread from `MembershipCases()`): `ISO-OFFER-OWNERSHIP-FOREIGN-SPACE`,
`ISO-READ-OWNERSHIP-OFFER-FOREIGN-SPACE`, `ISO-REVOKE-OWNERSHIP-OFFER-FOREIGN-SPACE` (boundary `404`, recorded `CrossSpace`,
no offer moved), `ISO-RECORD-KIND-OFFER-OWNERSHIP-ADMIN`, `-MEMBER`, `-VIEWER` and `ISO-RECORD-KIND-REVOKE-OWNERSHIP-OFFER-ADMIN`
(`RoleRefused("offer-ownership-transfer")`), `ISO-OFFER-OWNERSHIP-FOREIGN-MEMBERSHIP` (`SameAsAnchor(400,
invalid-offer-recipient)`), `ISO-REVOKE-FOREIGN-OFFER-ID-IN-OWN-SPACE` (`SameAsAnchor(404, not-available)`),
`ISO-TOKEN-OWN-OTHER-SPACE-OFFER-OWNERSHIP`, `-READ-OWNERSHIP-OFFER`, `-REVOKE-OWNERSHIP-OFFER`.

Not scaffolded here, by design: handler unit tests (`OfferOwnershipHandler`, `RevokeOwnershipOfferHandler` with a
`RecordingOutbox`) cannot compile before Task 3 declares them; the dev phase writes them from the `IssueInvitationHandlerTests`
shape. The `MatrixRoute` row and its `MatrixProbeAsync` probe in `.RoleMatrix.cs` are not added: adding the row now turns the
active coverage gate red before any endpoint declares the capability. The `LostMembership` arm (the Owner removed while its
offer waits) is not reachable through the product (the Owner cannot be removed), so it has no case. I-14 on the isolation
suite is recorded as proved in Slices. No `IsolationTimingTests` row (reason in the partial's remarks). The 18-row plant table
is the dev phase's.

## Step 4 - Generated scaffolds (TDD red phase)

Every scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; every isolation row is
`PendingUntil: "5.1"` with a reason. Execution mode resolved to **sequential**.

| File | Methods / rows | Cases when active | ACs |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OfferOwnership.cs` | 10 (all skipped) | 15 | AC1, AC2, AC3, AC6, AC10, AC13 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OfferOwnershipOutbox.cs` | 5 (all skipped) | 7 | AC7, AC8 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OfferOwnershipRace.cs` | 3 (all skipped) | 3 | AC3, AC4, AC5, AC13 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OwnershipOfferLapse.cs` | 5 (all skipped) | 6 | AC5, AC11 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OwnershipOfferSchema.cs` | 4 (all skipped) | 4 | AC3, AC10, AC11, AC12 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeOwnershipOffer.cs` | 7 (all skipped) | 9 | AC4, AC9, AC10, AC12 |
| `tests/Yello.Tests.Isolation/IsolationCases.OwnershipOffers.cs` | 12 rows, `PendingUntil: "5.1"` | 24 (0 generated while pending) | AC13 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.OwnershipOffers.cs` | 12 (all skipped) | 12 | AC1, AC3-AC5, AC7-AC10, AC12 |
| `tests/Yello.Tests.Client/OwnershipSectionTests.cs` (new class, existing assembly fixtures) | 9 (all skipped) | 9 | AC9 |

Supporting (no cases): `tests/Yello.Tests.Shared/OwnershipOfferWire.cs` (stand-ins and wire readings);
`WireDatabase.OwnershipOffersInAsync`, `AddOwnershipOfferAsync` and the `OwnershipOfferRow` record;
`tests/Yello.Tests.Client/FakeYelloApi.OwnershipOffers.cs` (the three routes as the Host will answer them, with a fixed
`ServerNow`, `PendingOffers`, `NextOfferRefusal`, `OwnershipOffersSent`, `OfferRevokes`); one dispatch line in
`FakeYelloApi.DecideAsync`; one spread line in `IsolationCases.MembershipCases()`.

### Discovery runs (no full run, per the phase's policy)

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Slices, `--filter-method` on the 34 new methods | 34 | 34 | 0 | 0 |
| - of which `.OfferOwnership.cs` | 10 | 10 | 0 | 0 |
| - of which `.OfferOwnershipOutbox.cs` | 5 | 5 | 0 | 0 |
| - of which `.OfferOwnershipRace.cs` | 3 | 3 | 0 | 0 |
| - of which `.OwnershipOfferLapse.cs` | 5 | 5 | 0 | 0 |
| - of which `.OwnershipOfferSchema.cs` | 4 | 4 | 0 | 0 |
| - of which `.RevokeOwnershipOffer.cs` | 7 | 7 | 0 | 0 |
| Isolation, `IsolationCaseListTests` (GATE-001..003, route coverage, cross product) | 16 | 0 | 16 | 0 |
| Architecture, whole suite | 498 | 12 (the new gates) | 486 | 0 |
| Client, `OwnershipSectionTests` | 9 | 9 | 0 | 0 |

### Green today, left active (standing regression gate)

None. Every new scaffold depends on a route, table, type or component that does not exist yet.

### Red verified (temporary unskip, restored in `finally` from memory; never `git checkout`)

| Scaffold | Failed with | Reason matches |
|---|---|---|
| `RefusalBoundaryGateTests.The_ownership_offer_routes_require_OfferOwnershipTransfer_and_serve_both_surfaces` | "Collections differ ... Actual: []" | yes - no route is mapped |
| `ActiveSpaceEndpointTests.An_OwnershipOffer_cascades_from_its_Space_and_references_nothing_else` | assertion failure on the FK list (the table does not exist, the catalogue query returns no rows) | yes |

The temporary script lived in the session scratchpad; both files were restored and the solution rebuilt (0 warnings, 0 errors).

### Seed data against the real schema

- `FK_Membership_Account_AccountId` (Restrict): every Membership names an Account registered through the product first
  (`RegisteredAndSignedInAsync`, `SignedInAsync`); no Account is inserted by hand.
- `AK_Membership_SpaceId_AccountId`: one Membership per Account per Space; the I-14 recipient's second Membership is in a
  different Space (`AddMembershipAsync`), and the schema case's one Account owns two different Spaces.
- `UX_Membership_SpaceId_Owner`: one Owner per seeded Space (`SeedAssignableSpaceAsync` / `SeedNamedSpaceAsync`); every
  other Membership is written by the accept.
- Task and Label seeds in the I-14 case go through the product in a Space seeded with one Status
  (`FK_Task_Project_SpaceId_ProjectId`, the `(SpaceId, StatusId)` key).
- The offer table as Lee's Q2 (a) shapes it: `FK_OwnershipOffer_Space_SpaceId` (every seeded offer names an existing Space),
  `BLOCK AFTER INSERT` (written under that Space's key), `UX_OwnershipOffer_SpaceId_Pending` (at most one seeded `Pending`
  row per Space: every terminal or expired seed is written after the live offer was revoked; the schema case's second
  `Pending` insert is the one deliberate violation, `2601` expected), no FK to `Membership` or `Account`.
- `IdempotencyRecord` trigger: the forced-rollback trigger fires only for the case's own key and is dropped in a `finally`.

### Findings for the dev phase

1. **Status codes**: see the table in Step 3. Two discrepancies with the story's prose are named there: the missing-member
   body pins `invalid-offer-recipient`, and the read's `204` has no Space-scoped precedent. The offer's `Location` is the
   pending read, not the offer itself.
2. **Active tests that go red by design when the offer lands** - widen them in the same commit, do not skip them:
   - `RefusalBoundaryGateTests.SpaceScopedEntities` (add `Yello.Domain.Spaces.OwnershipOffer`), `Space_is_read_only_where_named`
     (add `OwnershipOfferConfiguration`), the `TablesThatCannotMove` list, `SchemaNames.SpaceScopedTables`.
   - `The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare` as soon as an endpoint declares
     `OfferOwnershipTransfer`: add the `MatrixRoute` (`POST`, `201`) and a probe in `MatrixProbeAsync`.
   - The isolation route-coverage gates as soon as a route is mapped: make the 12 rows live and swap the stand-ins.
   - The populated-Space deletion case's table list (assumption 3).
3. **`Only_the_shared_actionable_predicate_compares_ExpiresAt`** allows one file: `OfferPredicates.OwnershipOfferActionable`
   and the supersede's negation must live there (gate `The_ownership_offer_actionable_predicate_lives_beside_the_Invitations`).
4. **Do not name any Application type with "Authority"** (two existing `Assert.Single` gates).
5. **xUnit filters of different kinds AND together** (`--filter-class X --filter-method Y` matches nothing); run them
   separately. Every Slices scaffold here is in `ActiveSpaceEndpointTests`, so filter by `--filter-method "*.<name>"`.

## Activation order (riskiest assumption first)

Recorded in the story's Dev Notes, "ATDD artifacts - red-phase scaffolds already in the tree". First: race 1,
`An_offer_and_the_removal_of_its_recipient_held_on_the_Memberships_lock_end_in_a_serial_outcome` - it tests the riskiest
assumption, Lee's Q2 (a) with Q4 (a) and Q5 (a): with no foreign key to `Membership`, "no `Pending` offer ever names a
removed Membership" rests entirely on the guarded insert and the written lapse both running under the one Memberships lock.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Implementation guidance

- **Contracts:** `OwnershipOfferRoutes` (`Offers`/`OffersOf`, `Pending`/`PendingOf`, `Offer`/`OfferOf`),
  `OfferOwnershipRequest(Guid? MembershipId)`, `OwnershipOfferResponse(Id, RecipientMembershipId, RecipientName, ExpiresAt,
  ReadAt)`, `ProblemTypes.OwnershipOfferPending`, `OwnershipOfferNotPending`, `InvalidOfferRecipient`. Then swap the
  `OwnershipOfferWire` stand-ins and delete them.
- **Domain / Application:** `OwnershipOffer`, `OwnershipOfferState` (five members), `IOwnershipOfferStore`, the three
  handlers (the revoke with no `IOutbox`), `OutboxKinds.OwnershipOffered`, `OwnershipOfferEmail.Render(string, string, Uri,
  DateTimeOffset)`, `IMembershipStore.OwnershipOfferNoticeAsync`.
- **Infrastructure:** `OwnershipOfferConfiguration` (filtered unique index, column default, cascade, no other FK, string
  state), migration `OwnershipOffers` with RLS predicates, `OfferPredicates.OwnershipOfferActionable`, `OwnershipOfferStore`
  (lock, supersede, guarded insert, `2601` -> `AlreadyPending`, guarded revoke, read with `ReadAt`), `OwnershipOfferWrites.LapseNamingAsync`
  called from `RemoveCoreAsync` and `LeaveAsync`.
- **Host:** `OwnershipOfferEndpoints` (explicit outcome switches, `_ => throw`), `Location` set on the response.
- **Client:** `OwnershipSection.razor`, `OwnershipCapabilities.PermitsOfferOwnership`, `SectionKey.Ownership`, copy in
  `ClientCopy.resx`, days from `ExpiresAt - ReadAt` only.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs and Lee's Q1, Q2, Q4, Q5 decisions; framework; Docker up).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC13 each mapped above.
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 5.1 with a reason; none is
      green today.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (constraints listed above).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open; the temporary red-check script lived in the session scratchpad, and both files it
      touched were restored from memory in a `finally`.
- [x] No full run (this phase's policy); discovery runs above.

**Next workflow:** `bmad-dev-story` for 5.1, then `bmad-code-review`.
