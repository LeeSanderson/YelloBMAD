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
lastSaved: '2026-11-01'
workflowType: 'testarch-atdd'
storyId: '4.3'
storyKey: '4-3-accept-an-invitation'
storyFile: '_bmad-output/implementation-artifacts/4-3-accept-an-invitation.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-4-3-accept-an-invitation.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; wire, gates and browser scaffolds written in the main run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ViewInvitation.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitation.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitationRace.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitationBound.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.InvitationAcceptance.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs'
  - 'tests/Yello.Tests.Isolation/IsolationTimingTests.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationAcceptance.cs'
  - 'tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.Invitation.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.InvitationView.cs'
  - 'tests/Yello.Tests.Client/InvitationViewTests.cs'
  - 'tests/Yello.Tests.Client/TextSpacingTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.InvitationAcceptance.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Isolation/IsolationWorld.cs'
  - 'tests/Yello.Tests.Shared/InvitationWire.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
  - 'tests/Yello.Tests.Shared/ProductActor.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/4-3-accept-an-invitation.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-4-2-see-and-revoke-pending-invitations.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/component-tdd.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-healing-patterns.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
  - 'resources/knowledge/selector-resilience.md'
  - 'resources/knowledge/timing-debugging.md'
---

# ATDD Checklist - Epic 4, Story 4.3: Accept an Invitation

## Story summary

As someone who has been invited, I want to see what I am being offered and then deliberately accept it, so that I join
exactly one Space at exactly one Role, and nothing joins me to anything by accident. Requirements: **FR-11**, AR-30 /
AD-25 / NFR-8, UX-DR18, UX-DR35, with AD-2, AD-18, AD-22, AD-23, AD-24, AD-27, AD-28, NFR-6 and NFR-9 binding. Two new
routes, neither carrying `{spaceId}`: the view `GET /api/v1/invitations/offer` (token in the `Yello-Invitation-Token`
header) and the accept `POST /api/v1/invitations/acceptance` (`{ invitationToken }`, Session only). Client: a new
unauthenticated page `/invitations/{Token}`.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. Framework present; `tests/TESTING-CONVENTIONS.md` is the authority.
- **Story:** `ready-for-dev`, AC1-AC13 with cited sources. **Q1-Q7 decided by Lee (2026-11-01)**, all (a): Q1 a
  policy-free `InvitationToken` index; Q2 one named `SpaceId`-only unit of work; Q3 client-chained register, sign in,
  accept; Q4 header for the view, body for the accept; Q5 mismatch at view time; Q6 one uniform `404
  invitation-not-valid` plus a floor; Q7 accept is Session-only. Q8-Q19 scaffolded at their recommendations, each named in
  the skip reason where it changes what a test expects.
- **No progress file existed** for this story (`atdd-checklist-4-3-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`.
- **Knowledge loaded:** core (data-factories, component-tdd, test-quality, test-healing-patterns), backend
  (test-levels-framework, test-priorities-matrix) and frontend (selector-resilience, timing-debugging), read for principle;
  the repository's conventions govern the .NET code.
- **Harness reused (4.1/4.2):** `InvitationWire`, `MailDrop`, `WireDatabase`, the `ActiveSpaceEndpointTests` partials
  (`IssueAsync`, `KeyHeaders`, `SignedInAsync`, `RegisteredAndSignedInAsync`, `SeedNamedSpaceAsync`, `MembershipsInAsync`,
  `PersonalSpaceOfAsync`, `RefusalsOfAsync`, `DeleteSpaceOverWireAsync`, `TakeApplicationLockAsync`, `InvitationsLockOf`),
  `TaskWire.WaitUntilBlockedOnALockAsync`, `LabelWire.WaitUntilBlockedOrAnsweredAsync`, `DurationIndistinguishability`,
  the isolation world (`OwnerOfTwoAsync`, `OwnerElsewhereAsync`), and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the page does not exist, so every
locator is an assumption written from AC13 and Q15 (a)'s copy.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| **Q1 [LEE]** | (a) policy-free `InvitationToken` index, one named reader | token-index gate; five-state Space-deleted arm (cascade) |
| **Q2 [LEE]** | (a) one `SpaceId`-only unit of work, spine amendment | setter gate, dependents gate, amendment phrase gate |
| **Q3 [LEE]** | (a) register (unchanged) -> sign in -> accept | Slices registration case; client chain-order case |
| **Q4 [LEE]** | (a) `GET .../offer` + header; `POST .../acceptance` + body | every view and accept send; fake CORS allows the header |
| **Q5 [LEE]** | (a) `addressedToYou` with a Session; always at accept | view credential case; mismatch view and accept cases |
| **Q6 [LEE]** | (a) one byte-identical `404 invitation-not-valid`, floor | five-state cases (Slices, two isolation rows), timing row + oracle |
| **Q7 [LEE]** | (a) accept Session-only, Token gets the `401` | no-Session/Token case, isolation Token arms (`NoCredential`), route gate |
| Q8 | (a) accept takes `Yello/Invitations/{spaceId}` | three races and the bound race |
| Q9 | (a) lock -> actionable + addressed -> bound -> write | full-Space stranger / dead-token case |
| Q10 | (a) `200 { spaceId }` | accept cases; contract gate |
| Q11 | (a) honour `Idempotency-Key` | replay case; client "Not sent." keeps the key |
| Q12 | (a) `409 already-a-member` | already-a-Member case |
| Q13 | (a) `403 invitation-for-another-account`, unrecorded | mismatch cases |
| Q14 | (a) no publisher; the handler takes no outbox | acceptance-slice gate |
| Q15 | (a) the copy list | client cases, asserted as written |
| Q16 | (a) inline sign-in, no return URL | client inline sign-in case |
| Q17 | (a) "lapsed" has no stored form | covered by the expired arm; no case of its own |
| Q18 | (a) raw-SQL scan beside the IL gates | `No_raw_SQL_names_an_Invitation_table_outside_the_Invitation_store` (active) |
| Q19 | (a) land on `SpaceHome` after `SpaceDirectory.Forget` | client accept case |

### New assumptions this run made (no question covered them)

1. **Temporary stand-ins in `InvitationWire`** (`OfferRoute`, `AcceptanceRoute`, `TokenHeaderName`, `NotValidProblemType`,
   `ForAnotherAccountProblemType`), the 4.2 `RevokeRoute` precedent: Task 3 declares them in `Yello.Contracts`; the dev
   phase swaps every use (Slices, Isolation, the fake, the gates' literal patterns) and deletes the five.
2. **The accept body's member is `invitationToken`** (the story's example); the view's Role is the stable code (`member`).
3. **Two AC6 isolation rows, one per route** (`ISO-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES` on the view,
   `ISO-ACCEPT-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES` on the accept): a row names one route. The Space-deleted arm is
   the Slices case's only.
4. **The isolation world now has a mail drop** (`IsolationWorld.Mail`): an Invitation's token exists in plaintext only in
   its email. `CaseRequest` gains optional `Headers`; `ProductActor.SendAsync` accepts a null body (a `GET` with a header).
   Both changes leave every earlier row's sends byte-identical.
5. **The expired arm moves `ExpiresAt` on an Invitation issued through the product** (`WireDatabase.SetInvitationExpiresAtAsync`)
   rather than seeding a row with `AddInvitationAsync`: a seeded row has no token the test knows and no token-index row.
6. **The acceptance unit of work is found by shape** (the one new `SpaceId`-key setter whose name contains `Invitation`),
   because its name is the dev phase's.

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ActiveSpaceEndpointTests`; the isolation
rows ride the boundary and timing worlds; client cases join `ClientSurfaceCollection`. Engines on a full run: unchanged at
four.

### Scenario map

| AC | Level | Scaffold | Priority |
|---|---|---|---|
| AC2 | Wire | `The_view_answers_exactly_the_Space_the_Role_and_the_issuer_of_a_live_Invitation` | P0 |
| AC1 | Wire | `Viewing_an_Invitation_writes_nothing_and_only_the_accept_changes_anything` | P0 |
| AC1, AC7 | Wire | `The_view_answers_the_same_offer_to_no_Session_any_Session_and_a_bearer_Token` | P0 |
| AC7 | Wire | `A_different_Account_viewing_is_told_it_is_not_theirs_without_either_address` | P0 |
| AC4 | Wire | `The_invited_Account_accepts_and_joins_exactly_the_invited_Space_at_exactly_the_invited_Role` | P0 |
| AC5 | Wire | `A_second_acceptance_is_refused_with_the_uniform_answer_and_no_second_Membership` | P0 |
| AC11 | Wire | `An_accept_replays_its_200_under_the_same_key_and_a_new_key_meets_the_uniform_answer` | P1 |
| AC6 | Wire | `Five_states_are_one_answer_on_the_view_and_on_the_accept` | P0 |
| AC7 | Wire | `A_different_Account_accepting_is_refused_403_without_an_address_and_nothing_is_written` | P0 |
| AC3 | Wire | `Registering_from_the_Invitation_then_signing_in_and_accepting_joins_through_the_one_registration_slice` | P0 |
| AC4, Q7 | Wire | `An_accept_with_no_Session_or_with_a_bearer_Token_is_the_same_401_and_writes_nothing` | P0 |
| AC4 | Wire | `A_GET_on_the_accept_route_creates_nothing` | P1 |
| Q12 | Wire | `An_Account_already_a_Member_accepting_is_refused_409_already_a_member_and_nothing_changes` | P2 |
| I-17 | Wire | `Neither_the_token_nor_an_address_nor_a_name_reaches_the_Host_log_on_view_or_accept` | P0 |
| AC5 | Wire (race) | `Two_accepts_of_one_Invitation_held_on_the_Space_lock_admit_one_and_answer_the_other_uniformly` | P0 |
| AC5 | Wire (race) | `An_accept_racing_a_revoke_that_commits_under_it_answers_uniformly_and_joins_nothing` | P0 |
| AC5 | Wire (race) | `An_accept_waits_on_the_Space_Invitations_lock_and_an_issue_to_the_same_address_queues_behind_it` | P0 |
| AC9 | Wire | `The_101st_Membership_is_refused_at_acceptance_409_and_the_Invitation_stays_pending` | P0 |
| AC9 | Wire | `At_99_Memberships_the_same_accept_succeeds_and_makes_100` | P0 |
| AC9 | Wire (race) | `Two_accepts_at_99_overlapped_on_the_lock_admit_one_and_refuse_the_other_at_the_bound` | P0 |
| AC9, Q9 | Wire | `In_a_full_Space_a_stranger_or_a_dead_token_never_learns_the_Space_is_full` | P1 |
| AC10 | Isolation | 9 rows, below | P0 |
| AC6 | Isolation (timing oracle) | `The_duration_method_tells_the_not_valid_answer_from_an_arm_that_does_no_work` (x2) | P0 |
| AC4, AC9 | Architecture | `The_view_and_accept_routes_carry_no_Space_the_accept_is_Session_only_and_consults_the_Memberships_bound` | P0 |
| AC2 | Architecture | `The_view_response_carries_the_Space_name_the_Role_the_issuer_and_the_addressee_indicator_and_nothing_else` | P0 |
| AC4 | Architecture | `The_accept_request_carries_the_token_alone_and_its_answer_the_Space_alone` | P0 |
| AC12 | Architecture | `The_accept_is_the_Invitation_stores_third_sanctioned_state_write` | P0 |
| AC12, Q18 | Architecture | `No_raw_SQL_names_an_Invitation_table_outside_the_Invitation_store` + detector (**active**) | P1 |
| AC12, Q1 | Architecture | `The_token_index_is_read_by_its_configuration_and_one_named_reader_only` | P0 |
| AC12, Q2 | Architecture | `The_acceptance_unit_of_work_is_reached_only_by_its_port_its_registration_and_the_pipeline_step` | P0 |
| AC3 | Architecture | `Only_the_registration_slice_constructs_an_Account` + detector (**active**) | P0 |
| AC3, Q14 | Architecture | `The_acceptance_slice_never_provisions_a_Space_or_an_Account_and_takes_no_outbox` | P0 |
| AC6, AC7 | Architecture | `The_invitation_acceptance_problem_types_are_declared_and_each_has_client_copy_naming_story_4_3` | P1 |
| AC12, Q2 | Architecture | `The_SpaceId_key_is_set_by_exactly_the_named_units_of_work` | P0 |
| AC12, Task 2 | Architecture | `The_spine_amendment_names_the_acceptance_unit_of_work_and_corrects_AD_24` | P1 |
| AC2 | Architecture | `The_invitation_view_takes_the_6px_radius`, `No_component_declares_more_padding_than_the_invitation_view` | P1 |
| AC2 | Architecture | `The_host_page_has_a_fixed_title_and_no_preview_metadata` + detector (**active**) | P0 |
| AC2 | Architecture | `The_static_host_sends_noindex_and_no_referrer_for_invitation_pages` | P0 |
| AC2, AC13 | Architecture | `The_client_invitation_route_is_the_Hosts_link_path_and_is_reached_without_a_Session` | P0 |
| AC2 | Architecture | `The_invitation_page_has_a_fixed_title_and_a_noindex_meta` | P1 |
| AC13 | Browser | 10 methods in `InvitationViewTests`, 1 theory in `TextSpacingTests` | P0/P1 |

Not scaffolded here, by design: the handler unit tests (`ViewInvitationHandlerTests`, `AcceptInvitationHandlerTests`)
cannot compile before Task 5 declares their types; the dev phase writes them from the `IssueInvitationHandlerTests`
template. Gates the story says "stay green unchanged" are existing tests and are not duplicated (listed under "Green
today").

## Step 4 - Generated scaffolds (TDD red phase)

Every scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; every isolation row is
`PendingUntil: "4.3"` with a reason. The build stays at 0 warnings and the suite green.

| File | Methods / rows | Cases | ACs |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ViewInvitation.cs` | 4 | 4 | AC1, AC2, AC7 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitation.cs` | 10 | 10 | AC3, AC4, AC5, AC6, AC7, AC11, Q12, I-17 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitationRace.cs` | 3 | 3 | AC5 (races 1-3) |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitationBound.cs` | 4 | 4 | AC9 |
| `tests/Yello.Tests.Isolation/IsolationCases.InvitationAcceptance.cs` | 9 rows | 18 when active (0 generated while pending) | AC6, AC7, AC8, AC10 |
| `tests/Yello.Tests.Isolation/IsolationTimingTests.cs` (one theory added) | 1 | 2 | AC6 (oracle) |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationAcceptance.cs` | 12 | 12 (8 skipped, 4 active) | AC3, AC4, AC5, AC6, AC7, AC9, AC12 |
| `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.Invitation.cs` | 2 | 2 | AC12 |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.InvitationView.cs` | 7 | 7 (5 skipped, 2 active) | AC2, AC13 |
| `tests/Yello.Tests.Client/InvitationViewTests.cs` | 10 | 10 | AC13 (AC2, AC3, AC4, AC6, AC7, AC9) |
| `tests/Yello.Tests.Client/TextSpacingTests.cs` (one theory added) | 1 | 2 | AC13 (ledger `:20`) |

**Skipped methods added: 48** (21 Slices, 1 Isolation theory, 15 Architecture, 11 Client); a skipped theory is reported
once. **Active methods added: 6** (Architecture), all green.

### Green today, left active (standing regression gates)

| Gate | Why it is green now | Its oracle |
|---|---|---|
| `No_raw_SQL_names_an_Invitation_table_outside_the_Invitation_store` | no raw SQL names `Invitation` | `The_raw_SQL_scan_detects_a_plant` (planted text, both directions) |
| `Only_the_registration_slice_constructs_an_Account` | only `RegisterAccountHandler` constructs `Account` | `The_Account_construction_scan_detects_a_plant` (a planted constructor in the test assembly) |
| `The_host_page_has_a_fixed_title_and_no_preview_metadata` | `index.html` already holds it | `The_preview_metadata_scan_detects_a_plant` (planted pages) |

Existing gates the story requires to stay green, unchanged and still the guard: 4.2's
`Only_the_Invitation_store_writes_an_Invitation_state` (plant 13), `The_Invitation_routes_all_require_IssueAndRevokeInvitations_and_serve_both_surfaces`
(AC9's "issue consults no bound"), 4.1's `Only_the_Invitation_store_and_its_configuration_read_an_Invitation_token_hash` and
`Only_the_shared_actionable_predicate_compares_ExpiresAt` (plant 14), `No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys`,
`No_production_code_runs_on_a_timer_or_as_a_hosted_service`, `RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal`,
4.2's `A_revoke_racing_an_acceptance_that_commits_under_it_answers_409_and_leaves_it_accepted`, and every 4.1/4.2 case.

### Isolation rows (AC10), all `PendingUntil: "4.3"`

| Row | Vector | Session | ApiToken |
|---|---|---|---|
| `ISO-INVITATION-VIEW-DISCLOSES-ONLY-THE-OFFER` | I-14 | `Served(200)`: Space name, Role, issuer; none of the issuer's other secrets, no `@`, no id | same |
| `ISO-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES` | I-13 | `UniformNotValid` on the view | same |
| `ISO-ACCEPT-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES` | I-13 | `UniformNotValid` on the accept | `NoCredential` |
| `ISO-ACCEPT-INVITATION-NO-SESSION` | I-2, AD-4 | `Served(200)` + no-credential `401` anchor | `NoCredential`, nothing written |
| `ISO-ACCEPT-INVITATION-OTHER-ACCOUNT` | I-2, I-13 | `Served(403)` for-another-account, nothing written | `NoCredential` |
| `ISO-ACCEPT-JOINS-ONLY-THE-INVITED-SPACE` | I-2, UJ-2 | `Served(200)`: one Account, exactly B/C/A, B and C unchanged | `NoCredential` |
| `ISO-INVITER-SEES-NOTHING-OF-THE-INVITEES-OTHER-SPACES` | I-1 | `BoundaryNotFound` on B, then C 404 and A's reads clean | same |
| `ISO-ACCEPT-INVITATION-TOKEN-FROM-ANOTHER-SPACE` | I-2, I-4 | `Served(403)`, never a Membership in Y | `NoCredential` |
| `ISO-TIMING-INVITATION-NOT-VALID-SAME-DURATION` | I-7 | `NotValidIndistinguishable` (revoked and expired vs unrecognised) | same |

Every token and invited address is held as its issuer's secret (I-17).

### Supporting harness (no product code)

- `InvitationWire`: temporary `OfferRoute`, `AcceptanceRoute`, `TokenHeaderName`, `NotValidProblemType`,
  `ForAnotherAccountProblemType`; lasting `MembershipsPerSpaceBoundCode`, `MembershipsPerSpace`, `OfferMembers`,
  `AcceptedMembers`, `TokenPrefix`, `AcceptBody`, `TokenHeaders`, `UnrecognisedToken`, `MalformedTokens`, `TokenIn(email)`,
  `ParseOffer`, `MemberNames`, `AcceptedSpaceId`, and the `OfferedInvitation` record.
- `WireDatabase`: `SetInvitationExpiresAtAsync`, `MembershipsOfAccountAsync` (under the `AccountId` key), `AccountCountAsync`.
- `IsolationWorld.Mail`; `CaseRequest.Headers`; `ProductActor.SendAsync(..., HttpContent? body, ...)`.
- `FakeYelloApi.InvitationAcceptance.cs`: `Offers`, `AddresseeSignedIn`, `NextAcceptRefusal`, `HoldAcceptAsync`,
  `ReleaseAccept`, `ReleaseAcceptUnanswered`, `InvitationOfferReads`, `InvitationAccepts`, and registration answered `204`;
  `FakeYelloApi.cs` allows the token header in CORS.

### Seed data against the real schema (checked against migration `20261101140742_Invitations` and the initial schema)

- `FK_Invitation_Space_SpaceId` (cascade) and `FK_Invitation_Account_IssuedByAccountId`: every Invitation is issued through
  4.1's route by a registered Owner of a Space seeded first; nothing inserts an Invitation directly.
- `UX_Invitation_TokenHash`: product-minted tokens only; `UnrecognisedToken()` is never stored.
- `ExpiresAt datetimeoffset not null`, default, **no check constraint**; the expired arm's `UPDATE` keeps `SpaceId`, so the
  BLOCK AFTER UPDATE predicate admits it, and runs under the Space's key (FILTER).
- The held revoke in race 2: `UPDATE ... SET State = N'Revoked'` (`nvarchar(16)`, no check constraint), Space unchanged.
- `SeedSpaceHoldingMembershipsAsync`: `FK_Membership_Space_SpaceId`, `FK_Membership_Account_AccountId` (principals in the
  same unit of work), `AK_Membership_SpaceId_AccountId` (one per Account), `UX_Membership_SpaceId_Owner` (one Owner),
  `Account.NormalizedEmailAddress` unique (random addresses), BLOCK AFTER INSERT (the unit's key names the Space).
- `WireDatabase.AddMembershipAsync` (Q12 case): refuses an Owner, adds one Viewer after the issue.

### Red verified for two scaffolds (temporary unskip, restored in `finally`)

| Scaffold | Failed with | Reason matches |
|---|---|---|
| `The_view_answers_exactly_the_Space_the_Role_and_the_issuer_of_a_live_Invitation` | `NotFound {"type":"urn:yello:problem:not-available",...}` on the view | yes - the route is unmapped; the issue and the token read from the email worked |
| `An_accept_waits_on_the_Space_Invitations_lock_and_an_issue_to_the_same_address_queues_behind_it` | "The accept answered while the Space's Invitations lock was held: NotFound ... not-available" | yes - unmapped; the hold and the issue's wait on the lock worked |

### Findings for the dev phase

1. **The 4.2 trap holds here too:** any path under `/api/v1/invitations/` answers the boundary `404 not-available` today
   (seen in both red runs). Every `404` and absence scaffold is anchored on a served answer on the same route, and the uniform
   answer is asserted to be `invitation-not-valid`, never `not-available`, and to differ from the unmatched path's body.
2. **Race 3 cannot be shown by its end state.** With one actionable Invitation per address, the issue's own AC13 check
   (`invitation-pending`) refuses an issue that reads before the accept commits; so "no Member with a new pending
   Invitation" holds even without the accept's lock. The decisive assertion is that the accept *waited* on the held lock.
   Plant 5 is expected red on that assertion; record it so in the Debug Log.
3. **Active gates the dev phase must amend, not scaffolded (each would be red today):**
   `RefusalBoundaryGateTests.Space_is_read_only_where_named` (the view's Space-name reader, named with 4.3);
   `SchemaVarianceTests.NamedVariances` (`InvitationToken.OfferedSpaceId`, Lee's Q1 (a)) - it fails on its own once the
   table exists; `CrossOriginConfigurationTests` / `CrossOriginPolicy` (the token header, Q4's cost); the AD-19 contract
   snapshot, if one lists contract types.
4. **The Q5 indicator and Q7 interact:** a bearer Token on the view gets `addressedToYou: null` (it reads a Session only);
   the scaffolds assert that.
5. **The isolation harness changed in three small ways** (assumption 4); no earlier row's request bytes change.

## Activation order (riskiest assumption first)

1. **`Five_states_are_one_answer_on_the_view_and_on_the_accept`** - the riskiest assumption: that a token resolves to its
   Space through the policy-free index before any Space context exists (Lee's Q1 (a)), that one `SpaceId`-only unit of work
   then reads the Invitation through RLS and `OfferPredicates.Actionable` (Q2 (a)), that an unrecognised or malformed token
   runs the same statements against a sentinel key, and that the index cascades with its Space - all yielding one
   byte-identical body (Q6 (a)). Then the two isolation five-state rows.
2. `The_view_answers_exactly_...`, `Viewing_an_Invitation_writes_nothing_...` (AC1's safe read), the view credential and
   mismatch cases, the view contract gate and `ISO-INVITATION-VIEW-DISCLOSES-ONLY-THE-OFFER`.
3. The setter gate, the token-index gate, the dependents gate, then the spine amendment and its gate (Task 2).
4. The accept: `The_invited_Account_accepts_...`, the race 2 case (the guarded `ExecuteUpdateAsync` composed with
   `Actionable` and the address clause - confirm the single `UPDATE` once at Debug logging), race 1, race 3, the second
   acceptance, the mismatch, no-Session/Token, GET, already-a-Member, replay, and the third-write and route gates.
5. The bound: at 100, at 99, the bound race, the full-Space ordering case.
6. Registration from the Invitation (AC3) and the AD-22 slice gate; the I-17 log case; the remaining isolation rows; clear
   every `PendingUntil: "4.3"` (GATE-003) before review.
7. The timing row and its permanent oracle (alone; record masking honestly, the D4 precedent, if the oracle is not caught).
8. The handler unit tests (written by dev), the problem-type gate, the client gates (route, static host, page head, CSS).
9. The browser cases, after `Yello.Client` is built in Debug: offer, no accept on navigation, not optimistic, uniform copy,
   different Account, bound, registration chain, inline sign-in, focus and "Not sent.", reflow, text spacing.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Implementation guidance

- **Contracts:** `InvitationRoutes` view and accept routes; `InvitationOfferResponse(SpaceName, Role, IssuerName,
  AddressedToYou?)`, `AcceptInvitationRequest(InvitationToken)`, `AcceptedInvitationResponse(SpaceId)`;
  `ProblemTypes.InvitationNotValid`, `ProblemTypes.InvitationForAnotherAccount`. Then swap the five `InvitationWire`
  stand-ins everywhere and delete them.
- **Endpoints:** view (no credential required, floor, no bound) and accept (`SessionOnlyEndpoint`,
  `.ConsultsBound(Bound.MembershipsPerSpace)`, anti-forgery, idempotency); outcome switch with no `_ =>` arm.
- **Store:** `ReadOfferAsync` through `Actionable`; `AcceptAsync` = lock, guarded `ExecuteUpdateAsync` with the address
  clause, then one `Membership` insert; the token index written in the issue's transaction.
- **Client:** `InvitationView.razor`, `ClientRoutes.Invitation`, `IsReachedWithoutASession`, `staticwebapp.config.json`
  route headers, `ClientCopy.resx`, `RefusalCopy.OwnKeys`, `components.css` (`.invitation-view`).

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs and Lee's decisions; framework; Docker up: server 29.5.3).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC13 each mapped above.
- [x] Every scaffold is skipped with its AC (`red: AC<n> - ...`) or pending until 4.3 with a reason; Q8-Q19 named where they
      shape an expectation.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (constraints listed above).
- [x] Story metadata and handoff paths captured in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open; temporary scripts live in the session scratchpad, not the repository.
- [x] Full build and suite run (totals in the story's Dev Notes section and the run report).

**Next workflow:** `bmad-dev-story` for 4.3, then `bmad-code-review`.
