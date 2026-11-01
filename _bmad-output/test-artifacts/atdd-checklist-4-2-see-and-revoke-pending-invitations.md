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
storyId: '4.2'
storyKey: '4-2-see-and-revoke-pending-invitations'
storyFile: '_bmad-output/implementation-artifacts/4-2-see-and-revoke-pending-invitations.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-4-2-see-and-revoke-pending-invitations.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; wire, gates and browser scaffolds written in the main run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ListInvitations.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitationRace.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationRevoke.cs'
  - 'tests/Yello.Tests.Client/InvitationListTests.cs'
  - 'tests/Yello.Tests.Client/TextSpacingTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.Invitations.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Shared/InvitationWire.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/4-2-see-and-revoke-pending-invitations.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-4-1-invite-an-email-address-to-a-space-at-a-role.md'
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

# ATDD Checklist - Epic 4, Story 4.2: See and revoke pending Invitations

## Story summary

As an Owner or Admin, I want to see what Invitations are outstanding and be able to withdraw one, so that an offer I no
longer mean can be taken back before it is accepted. Requirements: **FR-12**, **UX-DR14**, FR-10's retention clause, with
AD-5, AD-18, AD-23/section 6.1, AD-26, AD-27/AR-31 and A-10 binding. Two new routes on 4.1's path, both under
`IssueAndRevokeInvitations` on both surfaces: `GET .../invitations` (actionable pending only, newest first) and
`DELETE .../invitations/{invitationId}` (one guarded `UPDATE`, `204` / `409 invitation-not-pending` / unrecorded boundary
`404`). Client: the list in 4.1's Invitations section with an in-place confirm.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. Framework present (six test projects; `tests/TESTING-CONVENTIONS.md` is the authority).
- **Story:** `ready-for-dev`, AC1-AC11 with cited sources; open questions Q1-Q16, none routed to Lee. Q3 and Q16 are
  [SECURITY]; both scaffolded against their recommended, non-relaxing options.
- **No progress file existed** for this story (`atdd-checklist-4-2-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`.
- **Knowledge loaded:** core (data-factories, component-tdd, test-quality, test-healing-patterns), backend
  (test-levels-framework, test-priorities-matrix) and frontend (selector-resilience, timing-debugging). Their TypeScript
  examples were read for principle only; the repository's own conventions govern the .NET code.
- **Harness reused (story 4.1's):** `InvitationWire`, `MailDrop`, `WireDatabase`, the `ActiveSpaceEndpointTests`
  partials (`IssueAsync`, `KeyHeaders`, `SignedInAsync`, `SeedNamedSpaceAsync`, `RefusalsOfAsync`, `SeedForeignSpaceAsync`),
  `TaskWire.WaitUntilBlockedOnALockAsync`, the isolation world and its expectations, and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the client markup does not exist
yet, so there is nothing to record, and every locator is an assumption written from the story's AC9 and Q15 (a) copy.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

All at the story's recommendation; each named in the skip reason or the remarks where it changes what a test expects.

| Q | Option | Where it shows |
|---|---|---|
| Q1 | (a) `DELETE .../invitations/{invitationId}`, `204` | every revoke scaffold; `InvitationWire.RevokeRoute` |
| Q2 | (a) `409 urn:yello:problem:invitation-not-pending` for accepted, revoked and expired | the terminal theory, AC10, race, client 409 |
| **Q3 [SECURITY]** | **(a)** unknown id and another Space's id: the boundary `404` body, byte-identical, not recorded | Slices unknown-id case; `ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE` |
| Q4 | (a) expired: not listed, `409`, row stays `Pending` | AC6 list case; terminal theory (`Expired`) |
| Q5 | (a) `{id, emailAddress, role, issuerName, issuedAt}` | list shape case; contract gate; `ListedInvitation` |
| Q6 | (a) absolute date in `<time datetime>` | client row case |
| Q8 | (a) newest first, unpaged | list order case (tie on `CreatedAt` broken by SQL Server's `uniqueidentifier` order, `SqlGuid`) |
| Q9 | (a) in-place confirm, the Q15 warning | client confirm cases |
| Q10 | (a) one guarded `ExecuteUpdate`, no lock | race case |
| Q11 | (a) prove "cannot be accepted" by state, predicate and re-issue | re-issue case |
| Q12 | (a) demotion and removal through `WireDatabase`; deletion to 5.4 | the two AC3 arms |
| Q13 | (a) `Only_the_Invitation_store_writes_an_Invitation_state` now | state-writer gate |
| Q14 | (a) keep `PermitsIssueInvitations` | no new gate (4.1's predicate gate stays the guard) |
| Q15 | (a) the copy list | client cases, asserted as written |
| **Q16 [SECURITY]** | **(a)** the list under `IssueAndRevokeInvitations`; Member/Viewer `403` | Member/Viewer list case; routes gate; two isolation rows |

### New assumptions this run made (no question covered them)

1. **The 4.2 problem-type gate is a sibling, not a generalisation** of 4.1's
   `The_invitation_problem_types_are_declared_and_each_has_client_copy_naming_story_4_1` (the story leaves the choice
   open). Recorded in the gate's remarks.
2. **The 4.2 gates live in their own partial file**, `RefusalBoundaryGateTests.InvitationRevoke.cs`, rather than appended
   to `RefusalBoundaryGateTests.Invitations.cs`: same class, so 4.1's file reads as 4.1 wrote it.
3. **Temporary scaffold members in `InvitationWire`** (`RevokeRoute`, `RevokePathOf`, `NotPendingProblemType`), because
   the contract members Task 2 adds do not exist and no product code may be written here. The story says "extend
   `InvitationWire`, and do not add a second scaffold", so they sit there, marked temporary. The dev phase swaps each use
   for `InvitationRoutes.Invitation`, `InvitationRoutes.InvitationOf` and `ProblemTypes.InvitationNotPending`, then
   deletes them (the 4.1 `InvitationScaffold` precedent). `ListedInvitation` and `ParseList` stay: they are the test's
   reading of the wire, shared by Slices, Isolation and the client fake.
4. **The terminal seeds are two statements**: `WireDatabase.AddInvitationAsync` (pending) then the new
   `WireDatabase.SetInvitationStateAsync` (an `UPDATE` under the Space's key). An eight-parameter seed broke `S107`.
5. **The fake lists an issued Invitation only when `ListIssuedInvitations` is set.** 4.1's
   `A_sent_invitation_clears_the_form_keeps_focus_and_announces_no_address` asserts the address appears nowhere on the
   page after a `204`; once 4.2 re-reads the list after an issue, a fake that listed it by default would turn that 4.1
   case red for a reason that is not a defect. See "Finding for the dev phase" below.
6. **The danger button is located by the design system's `.button-danger` class** inside the row: the row's own button
   and the confirm's danger button share the accessible name "Revoke", and AC9 names the class.

### Container budget

No new container-backed class and no new fixture. The Slices cases are partials of `ActiveSpaceEndpointTests` (its one
database on the Slices engine); the isolation rows ride the boundary world; the client cases join
`ClientSurfaceCollection`. Engines on a full run: unchanged at four.

### Scenario map

| AC | Level | Scaffold | Priority |
|---|---|---|---|
| AC1 | Wire (Slices) | `The_list_shows_each_pending_Invitations_address_Role_issuer_and_date_newest_first` | P0 |
| AC1, AC6 | Wire | `The_list_is_empty_for_a_Space_with_no_Invitations` (anchored on a later issue) | P1 |
| AC1, Q16 | Wire | `A_Member_or_a_Viewer_listing_Invitations_is_refused_403_and_sees_no_address` (x2) | P0 |
| AC6, AC5 | Wire | `The_list_shows_only_live_pending_Invitations_and_never_revoked_accepted_or_expired_ones` | P0 |
| AC2 | Wire | `An_Admin_revokes_the_Owners_Invitation_and_the_Owner_revokes_the_Admins` | P0 |
| AC2, Q11 | Wire | `After_a_revoke_the_same_address_can_be_invited_again` | P0 |
| AC3 | Wire | `An_Invitation_whose_issuer_was_demoted_stays_listed_and_revocable_by_any_Owner_or_Admin` | P0 |
| AC3 | Wire | `An_Invitation_whose_issuer_was_removed_stays_listed_and_revocable` | P0 |
| AC4, AC5 | Wire | `Revoking_an_Invitation_that_is_no_longer_pending_is_refused_409_and_changes_nothing` (Accepted, Revoked, Expired) | P0 |
| AC4, Q3 | Wire | `Revoking_an_id_that_names_nothing_in_this_Space_is_the_boundary_404_and_is_not_recorded` | P0 |
| AC4 | Wire (race) | `A_revoke_racing_an_acceptance_that_commits_under_it_answers_409_and_leaves_it_accepted` | P0 |
| AC5 | Wire | `A_revoked_Invitation_is_retained_with_every_other_column_unchanged` | P0 |
| AC7 | Wire + mail drop | `A_revoke_sends_no_email` | P0 |
| AC10 | Wire | `A_revoke_replays_under_its_key_and_a_new_key_is_refused_409` | P1 |
| I-17 | Wire | `Neither_the_address_nor_the_issuer_name_reaches_the_Host_log_on_list_or_revoke` | P0 |
| AC8 | Isolation (both surfaces) | 11 rows, below | P0 |
| AC1, AC2, Q16 | Architecture | `The_Invitation_routes_all_require_IssueAndRevokeInvitations_and_serve_both_surfaces` | P0 |
| AC1, Q5 | Architecture | `The_Invitation_list_response_carries_the_id_address_Role_issuer_and_date_and_nothing_else` | P0 |
| AC5, AC11 | Architecture | `Only_the_Invitation_store_writes_an_Invitation_state` | P0 |
| AC7 | Architecture | `The_revoke_handler_takes_no_outbox` | P1 |
| AC2, AC3 | Architecture | `Neither_the_list_nor_the_revoke_reads_the_issuers_Account` | P1 |
| AC4, AC9 | Architecture | `The_invitation_not_pending_problem_type_is_declared_and_has_client_copy_naming_story_4_2` | P1 |
| AC9 | Browser | 10 methods in `InvitationListTests` and 1 in `TextSpacingTests`, below | P0/P1 |

Not scaffolded here, by design: the handler unit tests (`ListInvitationsHandlerTests`, `RevokeInvitationHandlerTests`)
cannot compile before Task 3 declares their types; the dev phase writes them from the `IssueInvitationHandlerTests`
template. AC11's other two gates (gate (i) and the no-timer gate) already exist and are active.

## Step 4 - Generated scaffolds (TDD red phase)

Every scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`; every isolation row is
`PendingUntil: "4.2"` with a reason. The build stays at 0 warnings and the suite green.

| File | Methods / rows | Cases | ACs |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ListInvitations.cs` | 4 | 5 | AC1, AC5, AC6 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs` | 10 | 12 | AC2, AC3, AC4, AC5, AC7, AC10, I-17 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitationRace.cs` | 1 | 1 | AC4 (race) |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationRevoke.cs` | 6 | 6 | AC1, AC2, AC3, AC4, AC5, AC7, AC9, AC11 |
| `tests/Yello.Tests.Client/InvitationListTests.cs` | 10 | 12 | AC9 (with AC1, AC2, AC4, AC6 as shown) |
| `tests/Yello.Tests.Client/TextSpacingTests.cs` (one method added) | 1 | 2 | AC9 (ledger `:20`) |
| `tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs` (rows added) | 11 rows | 22 when active (pending: 0 generated) | AC8 |

**Skipped cases added to the run: 38** (18 Slices, 6 Architecture, 14 Client), which the runner reports as **32**: a
skipped theory is reported once, not once per data row. The 22 isolation theory rows are not generated while pending.

**Full run after this ATDD phase** (`dotnet build Yello.slnx` 0 warnings, 0 errors; `--max-parallel-test-modules 1`):
1,945 total, 1,903 passed, 0 failed, 42 skipped (baseline 1,913 / 1,903 / 0 / 10, plus 32 skipped scaffolds).

**Green today, left active: none.** Every new scaffold needs a type or a route that does not exist yet. The standing
gates the story names as "stays green" are existing tests, unchanged: `Only_the_shared_actionable_predicate_compares_ExpiresAt`,
`No_production_code_runs_on_a_timer_or_as_a_hosted_service`,
`Only_the_Invitation_store_and_its_configuration_read_an_Invitation_token_hash`,
`Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay`,
`The_clients_invite_predicate_agrees_with_the_capability_matrix` and every 4.1 case.

### Isolation rows (AC8), all `PendingUntil: "4.2"`

| Row | Vector | Expectation (both surfaces unless stated) |
|---|---|---|
| `ISO-LIST-INVITATIONS-FOREIGN-SPACE` | I-1 | `BoundaryNotFound` |
| `ISO-LIST-INVITATIONS-OWN-SPACE` | I-3 | `Served(200)`: exactly A's actionable ids, none of B's ids or addresses |
| `ISO-RECORD-KIND-LIST-INVITATIONS-MEMBER` / `-VIEWER` | V-1 | `RoleRefused(issue-and-revoke-invitations)`, Invitation still `Pending` |
| `ISO-REVOKE-INVITATION-FOREIGN-SPACE` | I-2 | `BoundaryNotFound`, B's Invitation still `Pending` |
| `ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE` | I-4, I-13 | `SameAsAnchor(404, not-available)` against a never-existing id, nothing recorded, B's still `Pending` |
| `ISO-RECORD-KIND-REVOKE-INVITATION-MEMBER` / `-VIEWER` | V-1 | `RoleRefused`, Invitation still `Pending` |
| `ISO-TOKEN-OWN-OTHER-SPACE-LIST-INVITATIONS` | I-12 | Session `200`; Token `SameAsNonMember` |
| `ISO-TOKEN-OWN-OTHER-SPACE-REVOKE-INVITATION` | I-12 | Session `204` and the row `Revoked`; Token `SameAsNonMember`, row still `Pending` |

Every seeded address is issued through 4.1's route and held as the issuer's secret (I-17).

### Supporting harness (no product code)

- `InvitationWire`: `ListedInvitation` (the Q5 row), `ListedMembers`, `ParseList`, `ListMemberNames`; temporary
  `RevokeRoute`, `RevokePathOf`, `NotPendingProblemType`.
- `WireDatabase.SetInvitationStateAsync(spaceId, invitationId, state)`: `Pending`, `Accepted` or `Revoked` only.
- `FakeYelloApi.Invitations.cs`: the list (`PendingInvitations`, `HoldInvitationListAsync`, `InvitationListReads`), the
  revoke (`HoldRevokeAsync`, `ReleaseRevoke*`, `NextRevokeRefusal`, `InvitationRevokes`, `WithdrawInvitation`,
  `InvitationPathOf`), and `ListIssuedInvitations`. Paths from `InvitationRoutes` and `InvitationWire`, never literals.

### Seed data against the real schema (checked against migration `20261101140742_Invitations`)

- `FK_Invitation_Space_SpaceId` (cascade): every seeded Invitation's Space is seeded first (`SpaceSeeding`,
  `SeedForeignSpaceAsync`, or the actor's Personal Space).
- `FK_Invitation_Account_IssuedByAccountId` (restrict): every issuer is an Account the product registered.
- `UX_Invitation_TokenHash`: 32 random bytes per seeded row; product issues mint their own.
- `State nvarchar(16)`, no check constraint, stored as the member name: the setter accepts only `Pending`, `Accepted`,
  `Revoked`.
- RLS `BLOCK AFTER INSERT` / `AFTER UPDATE`: every seed and the race's held `UPDATE` run on a connection carrying the
  Space's session key, and none changes `SpaceId`.
- Membership seeds: `FK_Membership_*`, `AK_Membership_SpaceId_AccountId`, `UX_Membership_SpaceId_Owner` (an Admin is added,
  never a second Owner); removal deletes one Membership (no foreign key from `Invitation` to `Membership`, AD-5).

### Red verified for two scaffolds (temporary unskip, restored in `finally`)

| Scaffold | Failed with | Reason matches |
|---|---|---|
| `The_list_is_empty_for_a_Space_with_no_Invitations` | `404 not-available` on the `GET` | yes - unmapped method falls to the boundary fallback |
| `A_revoke_racing_an_acceptance_that_commits_under_it_answers_409_and_leaves_it_accepted` | "answered without waiting on the held ... range" | yes - unmapped `DELETE` answers at once |

**Finding from that run:** an unmapped `GET` on the Invitations path answers the boundary `404 not-available` body.
So an absence or "is the boundary 404" assertion on these routes would pass vacuously today. Every 404 scaffold here is
anchored on a served request on the same route (the unknown-id case revokes a live Invitation `204` first; the isolation
rows read their own Space first).

## Activation order (riskiest assumption first)

1. **`A_revoke_racing_an_acceptance_that_commits_under_it_answers_409_and_leaves_it_accepted`** - the riskiest
   assumption: that one `ExecuteUpdateAsync` composed with `OfferPredicates.Actionable` (and its `HasDbFunction` call)
   translates into a single `UPDATE ... WHERE` that, under RCSI, waits on the uncommitted acceptance and re-evaluates the
   guard. If EF splits it, or the predicate is evaluated client-side, plants 2 and 3 slip through. Confirm the SQL once
   at Debug logging (story, Library section), then run plants 2 and 3.
2. `Revoking_an_id_that_names_nothing_in_this_Space_is_the_boundary_404_and_is_not_recorded` and
   `ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE` (Q3 [SECURITY]: the existence read stays under RLS, no `409` for a foreign id).
3. `Only_the_Invitation_store_writes_an_Invitation_state` (Q13) and `Revoking_an_Invitation_that_is_no_longer_pending_...`.
4. The list: routes gate, contract gate, `The_list_shows_each_...`, `The_list_shows_only_live_...`, the Member/Viewer
   `403`, then the isolation list rows.
5. The revoke's remaining wire cases (AC2, AC3 arms, AC5, AC7, AC10, I-17) and the remaining isolation rows; clear every
   `PendingUntil: "4.2"` (GATE-003) before review.
6. The handler unit tests (written by dev), the two handler gates, the problem-type gate.
7. The browser cases, after `Yello.Client` is built in Debug: row, confirm, not-optimistic, announcement, 409, re-read
   after issue, empty, purge, reflow, text spacing.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Implementation guidance

- **Contracts:** `InvitationRoutes.List`, `.Invitation`, `.InvitationIdParameter`, `.InvitationOf`; `InvitationResponse`;
  `ProblemTypes.InvitationNotPending`. Then swap `InvitationWire.RevokeRoute`/`RevokePathOf`/`NotPendingProblemType`
  everywhere (Slices, Isolation, the fake) and delete them.
- **Endpoints:** `GET` and `DELETE` with `.RequiresCapability(Capability.IssueAndRevokeInvitations)`, both surfaces;
  outcome switch with no `_ =>` arm (4.1 review P10).
- **Store:** list projection of five columns through `OfferPredicates.Actionable`; revoke as one guarded
  `ExecuteUpdateAsync`, then an existence-only `AnyAsync` to choose `409` or `404`.
- **Client:** `InvitationSection.razor` list + purge + loading; `InvitationRow.razor`; copy in `ClientCopy.resx`;
  `RefusalCopy.OwnKeys`; `components.css`.

### Finding for the dev phase (a 4.1 case meets 4.2's re-read)

`InvitationTests.A_sent_invitation_clears_the_form_keeps_focus_and_announces_no_address` (4.1) asserts the address is
absent from `document.body.innerText` after a successful issue. 4.2's AC9 re-reads the list after an issue, and a real
server would then list the new address on the same page. The fake keeps that 4.1 case green by listing issued
Invitations only when `ListIssuedInvitations` is set, but the two requirements meet: the 4.1 assertion's intent is "no
address in any announcement" (UX-DR38), which `A_revoke_is_announced_without_an_address_...` and the 4.1 case's own
`said` checks cover. When 4.2 lands, narrow that 4.1 assertion to the live regions, or leave the fake's default off, and
record which in the Debug Log.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs; framework; development environment and Docker up).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC11 each mapped above; AC3's Account-deletion arm is 5.4's (Q12), as the story says.
- [x] Every scaffold is skipped with its AC (`red: AC<n> - ...`) or pending until 4.2 with a reason.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (constraints listed above).
- [x] Story metadata and handoff paths captured in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open; temporary scripts live in the session scratchpad, not the repository.
- [x] Full build and suite run (totals in the story's Dev Notes section and the run report).

**Next workflow:** `bmad-dev-story` for 4.2, then `bmad-code-review`.
