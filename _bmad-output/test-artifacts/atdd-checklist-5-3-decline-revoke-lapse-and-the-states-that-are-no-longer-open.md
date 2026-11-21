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
storyId: '5.3'
storyKey: '5-3-decline-revoke-lapse-and-the-states-that-are-no-longer-open'
storyFile: '_bmad-output/implementation-artifacts/5-3-decline-revoke-lapse-and-the-states-that-are-no-longer-open.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-5-3-decline-revoke-lapse-and-the-states-that-are-no-longer-open.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the API, isolation, gate and browser scaffolds share OwnershipOfferWire and one build)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeclinedOwnershipOffer.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.SettledOwnershipOffer.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.SettledOwnershipOfferRace.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.SettledOwnershipOffers.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SettledOwnershipOffers.cs'
  - 'tests/Yello.Tests.Client/OwnershipSectionTests.cs (5 methods added)'
  - 'tests/Yello.Tests.Client/OwnershipOfferTests.cs (3 methods added)'
  - 'tests/Yello.Tests.Client/FakeYelloApi.DeclinedOwnershipOffers.cs'
  - 'tests/Yello.Tests.Shared/OwnershipOfferWire.Declined.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/5-3-decline-revoke-lapse-and-the-states-that-are-no-longer-open.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-5-2-accept-an-ownership-offer.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 5, Story 5.3: Decline, revoke, lapse - and the states that are no longer open

## Story summary

As anyone touching an offer that has already been settled, I want a clear, non-disclosing answer, so that a race or a stale
tab never transfers a Space and never reveals one. Requirements: **FR-42** (decline half; "the Owner is told"; "declined or
lapsed cannot be accepted"), **AR-13**, **AR-31**, FR-43, NFR-1 / I-1 / I-2, with AD-3, AD-18, AD-26, AD-27 binding and
test-design rows A-10 and R12. New: an Owner-only `GET .../ownership-offers/declined` (Q1 (a)) and the Ownership panel's
declined statement; the revoke's `LostMembership` outcome (ledger `:947`); the dialog's in-Space `404` shown as the designed
no-longer-open state (Q5 (a)); the A-10 gate with a concrete predicate (Q6 (a)); `SetOwnershipOfferExpiresAtAsync` (Q7 (a)).

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025, Playwright
  for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority. Docker up (server 29.9.2).
- **Story:** `ready-for-dev`, AC1-AC11. **Q1-Q10 undecided**; every scaffold is written against the recommendation (a) and a
  skip that depends on one names it.
- **No progress file existed** (`atdd-checklist-5-3-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`, `tea_capability_probe: true`.
- **Knowledge:** core (data-factories, test-quality, test-healing-patterns), backend (test-levels-framework,
  test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle; the repository's
  conventions govern the .NET code.
- **Harness reused:** the `ActiveSpaceEndpointTests` partials (`OfferableSpaceAsync`, `OfferedAsync`, `OfferOverWireAsync`,
  `PendingOfferOverWireAsync`, `RevokeOfferOverWireAsync`, `MineOverWireAsync`, `AcceptOverWireAsync`, `DeclineOverWireAsync`,
  `RemoveOverWireAsync`, `LeaveOverWireAsync`, `IssueAsync`, `AcceptAsync`, `RefusalsOfAsync`, `CapabilityOf`,
  `PublishesNaming`, `AssertOneOwnerEachAsync`, `AssertNoPendingOfferNamesAMissingMembershipAsync`, `MadePresentAsync`,
  `TakeApplicationLockAsync`, `MembershipsLockOf`, `SoleDispatcherWireAsync`), `TaskWire.WaitUntilBlockedOnALockAsync`,
  `WireDatabase`, `MailDrop`, the isolation world (`HostWithAnOfferToAFreshAccountAsync`, `OfferedThroughTheProductAsync`,
  `JoinedThroughTheProductAsync`, `OwnerOfTwoAsync`, `TokenOwnOtherSpace`, `ForeignAsync`, `HoldsIdentity`), the gates'
  `CallGraph`, `TypeReferences`, `OwnershipOfferSqlWrite`, and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the declined route, the panel statement
and the dialog's in-Space `404` state do not exist; every path, locator and copy string for them is the story's
recommendation, named in each file's remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| Q1 | (a) `GET .../ownership-offers/declined`, `OfferOwnershipTransfer`, `200 {id, recipientName?}` / `204`, latest offer decides | `.DeclinedOwnershipOffer.cs` (5); 3 gates; 3 pending isolation rows; 5 `OwnershipSectionTests` cases |
| Q2 | (a) a Member the offer does not name keeps the unrecorded in-Space `404` (5.2's cases stand) | no new case; 5.2's not-named cases are the proof |
| Q3 | (a) a non-Owner's revoke keeps the recorded `403` | no new case; `An_Admin_is_refused_the_read_and_the_revoke_of_the_pending_offer` is the proof |
| Q4 | (a) `OfferStale` closed as unreachable | no scaffold (ledger only) |
| Q5 | (a) in-Space `404` on Decline shows the no-longer-open state, keeps the re-read | `A_decline_answered_by_the_in_Space_404_shows_the_no_longer_open_state` |
| Q6 | (a) call-graph gate from scheduled roots to state writers + expired-predicate exception | `No_scheduled_component_reaches_an_offer_or_Invitation_state_write`; `Only_a_new_offer_composes_the_expired_predicate_and_no_Invitation_expiry_write_exists` |
| Q7 | (a) `WireDatabase.SetOwnershipOfferExpiresAtAsync` (added here, test code) | the superseded and expiry cases |
| Q8, Q9 | (a) | no scaffold (statements and ledger) |
| Q10 | (a) the Owner learns at the next panel read; no push | `OwnershipSectionTests` cases read on arrival only |

### Status codes checked against comparable endpoints

| Route / answer | Scaffold asserts | Comparable endpoint | Discrepancy |
|---|---|---|---|
| Declined read | `200` + 2 members, or `204` | pending read (`200`/`204`) | none (Q1) |
| Declined read by Admin/Member/Viewer | recorded `403` naming `offer-ownership-transfer` | pending read's Admin `403` | none |
| Decline of any settled offer by its recipient | `409 ownership-offer-not-pending`, unrecorded | accept's settled theory | none |
| Revoke of a `Declined`/`Accepted` offer by the Owner | `409 ownership-offer-not-pending` | revoke's settled theory | none |
| Revoker whose Membership ended while waiting | recorded boundary `404` | 4.5's `LostMembership` | **intended change** (today `409`) |
| Old offer id after a lapse, by the re-joined Account | unrecorded in-Space `404` | 5.2's not-named | none (5.2 Q7) |
| Decline replayed after removal | boundary `404`, recorded | pipeline resolution before idempotency | none |

### New assumptions this run made (no question covered them)

1. **Stand-ins in `OwnershipOfferWire`** (`OwnershipOfferWire.Declined.cs`): `DeclinedRoute`, `DeclinedOf`. Develop swaps
   every use (Slices, Isolation, the fake) for `OwnershipOfferRoutes.Declined`/`DeclinedOf` and deletes them.
   `DeclinedResponseMembers`, `ParseDeclined` and `DeclinedOwnershipOfferAnswer` are lasting readings.
2. **The "former" isolation rows use a Token bound to the Account's own Space**, not one issued in the Space it left: the
   story suggested issuing the Token while the Membership exists, but `ApiTokenConfiguration` cascades the Token's delete from
   `Membership`, so that Token would be an unknown credential (`401`), not a non-member's. Corrects the story's note.
3. **The former Owner row uses `OwnerOfTwoAsync`** so the leaving Account keeps a first Space and a Token there.
4. **The three stranger rows put all three settled offers in one host Space** (declined, revoked, accepted, in that order),
   one path each, so each route needs one row, not three.
5. **New Slices partials** (`.SettledOwnershipOffer.cs`, `.SettledOwnershipOfferRace.cs`) hold the cases the story placed in
   `.DeclineOwnershipOffer.cs` / `.OwnershipOfferLapse.cs` / a race partial. Same class, same database, no new engine.
6. **Theory data widened in place:** `OffersNoLongerOpenToTheRecipient` gains `Accepted` (used by the accept theory and the new
   decline theory); `OffersNoLongerPending` gains `Declined` and `Accepted`.
7. **Browser cases are written as sibling methods** in `OwnershipSectionTests` and `OwnershipOfferTests` (the reflow row
   included), not in `TextSpacingTests`, so no other active class is edited.
8. **Fake:** `DeclinedOffers`, `DeclinedReads`, `DeclineElsewhere`, `NextOfferDeclineRefusal`; a new offer clears the Space's
   declined entry; the dispatch gains `GET .../declined`. Inert unless a case sets them.
9. **Locators and copy:** `main .ownership-section p.ownership-section__declined`; "{name} declined your offer of ownership.";
   "Your last offer of ownership was declined."; focus on an `h1`-`h6` whose text is "This offer is no longer open.".
10. **Requirement traits** carry FR/AR/AD/UX-DR/NFR ids only; A-10, R12 and S-2 are named in summaries.

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ActiveSpaceEndpointTests`; isolation rows
ride the boundary world; browser cases use the assembly's `ClientSurfaceFixture` and `FakeYelloApi`. Engines on a full run:
unchanged.

### Scenario map

| AC | Level | Scaffold | Priority | Today |
|---|---|---|---|---|
| AC1 (Q1) | Wire | `After_a_decline_the_Owner_reads_it_by_the_recipients_name_and_the_recipients_own_read_is_204` | P0 | red |
| AC1, AC7 (Q1) | Wire | `The_declined_read_is_204_with_no_offer_a_pending_offer_an_expired_offer_and_after_a_new_offer` | P0 | red |
| AC1 (Q1) | Wire | `A_decline_followed_by_a_later_offer_that_was_revoked_reads_204` (plant 5) | P0 | red |
| AC1 (Q1) | Wire | `After_the_declining_recipient_leaves_the_declined_read_names_no_one` | P1 | red |
| AC1 (Q1) | Wire | `An_Admin_a_Member_and_a_Viewer_are_refused_the_declined_read_with_the_recorded_403` (plant 6) | P0 | red |
| AC1 | Wire (log, mail) | `A_decline_and_a_revoke_publish_nothing_and_send_no_email` | P1 | green |
| AC2 | Wire | `After_a_decline_only_a_new_offer_can_be_accepted` | P0 | green |
| AC2 | Wire | `After_a_lapse_by_removal_only_a_new_offer_to_the_rejoined_Membership_can_be_accepted` | P0 | green |
| AC2, AC7 | Wire | `After_an_expiry_superseded_by_a_new_offer_only_the_new_offer_can_be_accepted` | P0 | green |
| AC3 | Wire | `A_settled_offer_is_409_to_its_recipients_decline_and_changes_nothing` (x5) | P0 | green |
| AC3 | Wire | `A_superseded_expired_offer_is_409_to_its_recipients_decline_and_to_the_Owners_revoke` | P0 | green |
| AC3 | Wire | widened `An_offer_no_longer_open_to_its_recipient_is_409_and_changes_nothing` (+Accepted) | P0 | green |
| AC3 | Wire | widened `Revoking_an_offer_that_is_no_longer_pending_is_409_and_changes_nothing` (+Declined, +Accepted) | P0 | green |
| AC5 | Wire | `A_decline_replayed_after_its_recipient_was_removed_is_the_boundary_404_and_not_the_recorded_204` | P0 | green |
| AC7 | Wire (mail, log) | `Expiry_by_time_writes_nothing_sends_nothing_and_every_transition_is_409` | P0 | green |
| AC4, AC5 | Wire (race) | `A_decline_queued_behind_the_removal_of_its_recipient_is_the_recorded_boundary_404` (plant 3) | P0 | green |
| AC4, AC5 | Wire (race) | `A_revoke_queued_behind_the_acceptance_and_its_offerers_leave_is_the_recorded_boundary_404` (plants 1, 2) | P0 | **red** |
| AC4 | Isolation | 6 settled rows (below), `PendingUntil: "5.3"` | P0 | pending (expected green; unverified) |
| AC1 | Isolation | 3 declined-read rows, `PendingUntil: "5.3"` | P0 | pending |
| AC1 | Architecture | four-route capability, declined route/surfaces, contract shape, no-outbox | P0 | red |
| AC4 | Architecture | `The_revoke_outcome_names_a_lost_Membership` | P1 | red |
| AC8 | Architecture | `Every_OwnershipOffer_state_transition_composes_the_actionable_predicate` (plant 4) | P0 | green |
| AC9 | Architecture | `No_scheduled_component_reaches_an_offer_or_Invitation_state_write` (plants 7, 8) | P0 | green |
| AC9 | Architecture | `Only_a_new_offer_composes_the_expired_predicate_and_no_Invitation_expiry_write_exists` (plant 9) | P0 | green |
| AC1 | Browser | 5 `OwnershipSectionTests` cases (plant 11 on the first) | P0 | red |
| AC6 | Browser | `A_decline_refused_409_shows_the_offer_is_no_longer_open_with_focus_on_its_heading` | P0 | skipped (expected green; unverified) |
| AC6 | Browser | `Every_cause_of_a_closed_offer_renders_the_same_no_longer_open_state` (x5) | P1 | skipped (expected green; unverified) |
| AC6 (Q5) | Browser | `A_decline_answered_by_the_in_Space_404_shows_the_no_longer_open_state` (plant 10) | P0 | red |

Isolation rows (`IsolationCases.SettledOwnershipOffers.cs`, spread from `OwnershipOfferCases()`):
`ISO-ACCEPT-SETTLED-OWNERSHIP-OFFER-FOREIGN-SPACE`, `ISO-DECLINE-SETTLED-OWNERSHIP-OFFER-FOREIGN-SPACE`,
`ISO-REVOKE-SETTLED-OWNERSHIP-OFFER-FOREIGN-SPACE` (world.A on three settled offers; offers and the new Owner unchanged),
`ISO-ACCEPT-OWNERSHIP-OFFER-AFTER-REMOVAL`, `ISO-DECLINE-OWNERSHIP-OFFER-AFTER-REMOVAL` (former recipient; offer `Lapsed`,
host still Owner), `ISO-REVOKE-OWNERSHIP-OFFER-AFTER-LEAVING` (former Owner; offer `Accepted`, recipient Owner) - each
`Both(BoundaryNotFound)`, pending until 5.3 only until they have been run. Pending until 5.3 for the route: `ISO-READ-DECLINED-OWNERSHIP-OFFER-FOREIGN-SPACE`,
`ISO-RECORD-KIND-READ-DECLINED-OWNERSHIP-OFFER-ADMIN`, `ISO-TOKEN-OWN-OTHER-SPACE-READ-DECLINED-OWNERSHIP-OFFER`.

Not scaffolded here, by design: the `ReadDeclinedOwnershipOfferHandler` unit test (the type does not exist; the dev phase
writes it with fakes); the 12-row plant table (the dev phase's, Task 8; plants modify product code).

## Step 4 - Generated scaffolds (TDD red phase)

Every red scaffold is `[Fact(Skip = "red: AC<n> - <why>")]`; the declined-read isolation rows are `PendingUntil: "5.3"`.
Execution mode resolved to **sequential**.

| File | Methods / rows | Red (skipped / pending) | Active, green today | ACs |
|---|---|---|---|---|
| `tests/Yello.Tests.Slices/.../ActiveSpaceEndpointTests.DeclinedOwnershipOffer.cs` (new) | 5 | 5 | 0 | AC1, AC7 |
| `tests/Yello.Tests.Slices/.../ActiveSpaceEndpointTests.SettledOwnershipOffer.cs` (new) | 8 methods, 12 cases | 0 | 12 | AC1, AC2, AC3, AC5, AC7 |
| `tests/Yello.Tests.Slices/.../ActiveSpaceEndpointTests.SettledOwnershipOfferRace.cs` (new) | 2 | 1 (revoke race) | 1 (decline race) | AC4, AC5 |
| `.AcceptOwnershipOffer.cs` / `.RevokeOwnershipOffer.cs` theory data widened | +1 / +2 rows | 0 | 3 new rows | AC3 |
| `tests/Yello.Tests.Isolation/IsolationCases.SettledOwnershipOffers.cs` (new) | 9 rows, `PendingUntil: "5.3"` | 9 (0 generated while pending) | 0 | AC1, AC4 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SettledOwnershipOffers.cs` (new) | 8 | 5 | 3 | AC1, AC4, AC8, AC9 |
| `tests/Yello.Tests.Client/OwnershipSectionTests.cs` (5 added) | 5 | 5 | 0 | AC1 |
| `tests/Yello.Tests.Client/OwnershipOfferTests.cs` (3 added) | 3 methods, 7 cases | 3 | 0 | AC6 |

Supporting (no cases): `tests/Yello.Tests.Shared/OwnershipOfferWire.Declined.cs` (stand-ins and readings);
`WireDatabase.SetOwnershipOfferExpiresAtAsync`; `tests/Yello.Tests.Client/FakeYelloApi.DeclinedOwnershipOffers.cs`; one
dispatch line and one clearing line in `FakeYelloApi.OwnershipOffers.cs`; one refusal hook in
`FakeYelloApi.OwnershipOfferAnswers.cs`; one spread line in `IsolationCases.OwnershipOffers.cs`.

**Six isolation rows and two browser cases are expected to pass today but stay red until they have been run.** These are the
three stranger rows, the two after-removal rows, the after-leaving row,
`A_decline_refused_409_shows_the_offer_is_no_longer_open_with_focus_on_its_heading` and
`Every_cause_of_a_closed_offer_renders_the_same_no_longer_open_state`. The Isolation run that would have executed the rows
was stopped by Claude Code for low system memory before it reported anything, and it was not restarted. No browser run was
made for the same reason. The dev phase activates these first and keeps them active if they pass.

### Discovery runs (no full run, per the phase's policy)

Build: `dotnet build Yello.slnx` - 0 warnings, 0 errors.

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Slices, `--filter-method` on the new and widened methods (log `artifacts/test-runs/5-3-...-atdd-slices.log`) | 29 | 6 | 23 | 0 |
| - `.DeclinedOwnershipOffer.cs` | 5 | 5 | 0 | 0 |
| - `.SettledOwnershipOffer.cs` | 12 | 0 | 12 | 0 |
| - `.SettledOwnershipOfferRace.cs` | 2 | 1 | 1 | 0 |
| - accept theory (5 rows, `Accepted` new) + revoke theory (5 rows, `Declined`/`Accepted` new) | 10 | 0 | 10 | 0 |
| Isolation, whole project | - | - | - | stopped for low memory, no output |
| Isolation, `IsolationCaseListTests` (GATE-001..003, route coverage, cross product) | 16 | 0 | 16 | 0 |
| Architecture, whole suite | 514 | 5 (the new red gates) | 509 | 0 |
| - `RefusalBoundaryGateTests.SettledOwnershipOffers.cs` | 8 | 5 | 3 | 0 |
| Client, `--filter-method` on the 8 new methods | 8 | 8 | 0 | 0 |

### Green today, left active (standing regression gates)

- Slices: `A_decline_and_a_revoke_publish_nothing_and_send_no_email`, the three AC2 cases, the decline theory (5), the
  superseded decline/revoke case, the replay-after-removal case, the expiry case, the decline-versus-removal race, and the
  three new theory rows. Several of these are AC4/AC5 regression guards: plant 3 must turn the decline race red.
- Architecture: `Every_OwnershipOffer_state_transition_composes_the_actionable_predicate`,
  `No_scheduled_component_reaches_an_offer_or_Invitation_state_write`,
  `Only_a_new_offer_composes_the_expired_predicate_and_no_Invitation_expiry_write_exists`.

### Corrections made during the run

- The transition gate first read `Lapsed` as `0`: the existing `LoadedInt` helper reads only `ldc.i4.0`-`3`, and `Lapsed`
  is `ldc.i4.4`. The gate now has its own `LoadedConstant`, covering every short form. Found by dumping `OfferAsync`'s
  setter lambda with Cecil.

## Activation order (riskiest assumption first)

0. The six settled isolation rows and the two AC6 browser cases: make them live and run them; they should pass today.
1. `A_revoke_queued_behind_the_acceptance_and_its_offerers_leave_is_the_recorded_boundary_404` - the riskiest assumption:
   that `sp_getapplock` grants three queued exclusive waiters in the order sent, so the leave commits before the revoke, and
   that the store can tell "no Membership" from "not pending" only by reading the caller's Membership first. Activate it
   before the `LostMembership` fix and see the `409`.
2. `The_revoke_outcome_names_a_lost_Membership`, then the `LostMembership` mapping.
3. `A_decline_followed_by_a_later_offer_that_was_revoked_reads_204` - the "top row first, then the state" query.
4. The other four `.DeclinedOwnershipOffer.cs` cases; the four AC1 gates (with the three-route gate replaced and the
   route/surface gate widened); the three declined isolation rows made live and the stand-ins swapped.
5. The five `OwnershipSectionTests` cases (count-sensitive arrival cases widened deliberately).
6. `A_decline_answered_by_the_in_Space_404_shows_the_no_longer_open_state`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Findings for the dev phase

1. **Active gates that go red by design when the route lands** - widen them in the same commit:
   `OfferOwnershipTransfer_is_required_by_exactly_the_three_offer_routes` (replace with the four-route gate),
   `The_ownership_offer_routes_require_OfferOwnershipTransfer_and_serve_both_surfaces` (add the pattern), the isolation
   route-coverage gate (make the three rows live), and any browser case counting arrival requests on Space settings.
2. **The A-10 and transition gates are green today**; plants 4, 7, 8, 9 must turn them red (product-code plants, Task 8).
   The transition gate reads each target state from the setter lambda's last `ldc.i4` (all short forms, `Lapsed` is
   `ldc.i4.4`); keep states as enum constants.
3. **xUnit filters of different kinds AND together**; every Slices scaffold is in `ActiveSpaceEndpointTests`, so filter by
   `--filter-method "*<name>*"`.
4. Do not name any Application type with "Authority". No new problem type.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs; Q1-Q10 at their recommendations; framework; Docker up).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC9 mapped above; AC10 is documentation plus the helper added here; AC11 is held by the
      existing gates and the widened theories.
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...` or pending until 5.3 with a reason; scaffolds that
      pass today are left active as standing regression gates and listed above.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (below).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open.
- [x] No full run (this phase's policy); discovery runs above.

### Seed data against the real schema

- `FK_Membership_Account_AccountId` (Restrict): every Membership names an Account registered through the product; the
  re-joined recipient is the same registered Account accepting a second Invitation.
- `AK_Membership_SpaceId_AccountId`: the re-join happens only after the removal deleted the first Membership.
- `UX_Membership_SpaceId_Owner`: one Owner per seeded Space; every other `Owner` is written by an acceptance.
- `FK_OwnershipOffer_Space_SpaceId` and the `BLOCK AFTER INSERT/UPDATE` predicates: `AddOwnershipOfferAsync` and
  `SetOwnershipOfferExpiresAtAsync` write under the Space's own key and keep `SpaceId`.
- `UX_OwnershipOffer_SpaceId_Pending`: seeded terminal rows follow the live offer's revoke; the superseded cases rely on the
  product's own supersede, which lapses the expired row before inserting the new one; sequential product offers in the
  isolation helpers each follow a settled one.
- `FK_ApiToken_Membership` (cascade): why the "former" isolation rows use a Token bound to the Account's own Space.

**Next workflow:** `bmad-dev-story` for 5.3, then `bmad-code-review`.
