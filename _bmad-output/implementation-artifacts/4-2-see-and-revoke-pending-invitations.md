---
baseline_commit: 53b087c8563a48b31b68fbc992afcfabbbe46d70
---

# Story 4.2: See and revoke pending Invitations

Status: done

Epic: 4 — Bring people into one piece of your work (the second story of the epic)
Story key: `4-2-see-and-revoke-pending-invitations`

**Requirements owned.** From the Story Coverage Index (`epics.md:370`): "4.2 | **FR-12**, UX-DR14".

- **FR-12** (`prd.md:284-291`; `epics.md:96`). The clauses are:
  - "An Owner or Admin can revoke an Invitation that has not been accepted."
  - "A revoked Invitation cannot subsequently be accepted."
  - "Revocation is possible only before acceptance; removing an accepted invitee is FR-14, not FR-12."
  - "An Invitation remains valid if its issuer later loses the capability to have issued it — by demotion, by removal, or by Account deletion. ... Any remaining Owner or Admin may revoke it."
- **UX-DR14, `invitation-list`** (`epics.md:252`; `EXPERIENCE.md:214`). "pending Invitations with address, Role and issuer, revocable by any Owner or Admin **including one who did not issue it and including when the issuer has been demoted, removed or deleted**; terminal Invitations retained in the record but **not shown**".
- **FR-10's retention clause** (`prd.md:269`). "The Invitation record retains its terminal state once it leaves pending — accepted, revoked or expired — rather than being deleted. No product surface reads it." The readiness report assigns this retention obligation to 4.2 (`implementation-readiness-report-2026-08-22.md:545`).

**Also binding, though the coverage row omits them:**

- **Role capability matrix** (`prd.md:348`; `role-capability-matrix.md:19`). "Issue and revoke Invitations | ✔ | ✔ | — | —". In code: `Capability.IssueAndRevokeInvitations`, matrix row `[Yes, Yes, No, No]` (`Yello.Domain/Memberships/RoleCapabilityMatrix.cs:46`).
- **AD-27 / AR-31** (`ARCHITECTURE-SPINE.md:245-249`). The one predicate `State = Pending AND ExpiresAt > now` is "applied by every read and every transition", inside the statement's own `WHERE`, against the database clock. It exists as `OfferPredicates.Actionable` (`Yello.Infrastructure/Persistence/OfferPredicates.cs`). Rows are never deleted on expiry.
- **AD-26's transition discipline, which AD-28 applies to Invitations** (`ARCHITECTURE-SPINE.md:241`, `:255`). "Every transition is guarded ... by `WHERE State = Pending` with a rowcount check." A transition refused because the offer is no longer `Pending` "returns **409** with a stable problem `type`, never 404: the caller holds a Membership in the Space".
- **AD-5** (`:106`). "removing a Membership does **not** delete Invitations that Membership issued (FR-12)".
- **AD-1 / AD-2 / AD-3 / AD-4 / AD-20.** Role comes from the Membership row per request. A non-member gets the boundary `404` with a `CrossSpace` record. A Member or Viewer gets `403` with an `InsufficientRole` record. Both surfaces behave the same.
- **AD-18** (`:182-187`). The revoke accepts an `Idempotency-Key`, and a replay returns the original answer without re-applying.
- **AD-23 and §6.1** (`prd.md:763-765`). Email addresses are readable only by Owners and Admins of the Space. The list carries addresses, so it is an Owner-and-Admin read.
- **AD-26's notification rule, applied to Invitations by 4.2's own AC** (`ARCHITECTURE-SPINE.md:241`; `epics.md:1699-1701`). Nothing is emitted on revoke.
- **UX-DR26 / UX-DR27 / UX-DR31 / UX-DR33 / UX-DR38.** No optimistic UI on anything destructive. Capabilities a Role lacks are absent, not disabled. Space-scoped client state is purged on leaving the Space. No address in any announcement.
- **NFR-6 / I-17.** The invited address and the token are secrets in logs.
- **NFR-9** (`prd.md:755`). The invitation flow meets WCAG 2.1 AA.

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Severity | Owner per ledger | Covered by |
|---|---|---|---|---|
| `:532` | Space settings sections: "Remaining owners: **4.2** (the pending list in the same section), 4.4, 5.1 and 6.3" | low | 4.2, 4.4, 5.1, 6.3 | AC9 |
| `:723` | A-10: "nothing but the named transition writes the state" belongs with "4.2's revoke, 4.3's accept" and 5.3 | low | 5.3 (4.2 named) | AC11, Q13 |
| `:20` | 1.4.12 text spacing and 200% text-only zoom: "each later surface adds its own case" | medium | open, rolling | AC9 |
| `:731` (code review of 4.1) | Issuing an Invitation has no rate limit (D3) | medium | 4.6, Lee/PM | Not closed. Q8 adds the unpaged list's growth to the same entry |

**Depends on** (all done):

- 4.1: `Invitation` under RLS, `InvitationState`, `OfferPredicates.Actionable`, `DatabaseClock`, `InvitationStore`, `InvitationRoutes`, `InvitationSection.razor`, the outbox, `MailDrop`, `InvitationWire`, and the `WireDatabase` Invitation helpers.
- 1.6: the pipeline, the matrix, refusal recording, idempotency.
- 1.8: the API Token revoke (`DELETE`, confirm in place, `ApiTokenRow.razor`).
- 1.9 and 3.4: the isolation case list and its gates, including `TokenOwnOtherSpace`.
- 2.4: Label list and delete, the in-place confirm and `LabelSection`'s purge registration.

**Explicitly not in this story:**

- **Acceptance and the invitation view** (4.3). 4.2 makes a revoked Invitation non-actionable. 4.3's acceptance route proves that a revoked one answers "This invitation is no longer valid." (Q11).
- **Account deletion** (5.4). The issuer-deleted arm of AC3 cannot run until 5.4 exists (Q12).
- **Membership removal and Role change as product paths** (4.4, 4.5). AC3's demotion and removal arms seed them through `WireDatabase` (Q12).
- **A rate limit on issue, and any Invitation bound.** Neither exists in a requirement (4.1's C7 and D3).
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits.
- **No new NuGet package and no new project** (CLAUDE.md).

## Story

As an Owner or Admin,
I want to see what Invitations are outstanding and be able to withdraw one,
So that an offer I no longer mean can be taken back before it is accepted.

## Acceptance Criteria

AC1 to AC7 are reproduced **verbatim** from `epics.md:1673-1701` (`status: final`) and numbered by position. AC8 to AC11 come from the spines, the ledger and a survey of the code. Each one cites its source.

**AC1 — the list: pending Invitations with address, Role and issuer, to Owners and Admins only** (UX-DR14, §6.1)
**Given** Space settings
**When** the Invitation list renders
**Then** it shows each pending Invitation with its address, its Role and who issued it, to Owners and Admins only

> - **The route** is `GET /api/v1/spaces/{spaceId:guid}/invitations`, the same path as the issue (`InvitationRoutes.Issue`). Add `InvitationRoutes.List` for it. It carries `.RequiresCapability(Capability.IssueAndRevokeInvitations)` (Q16), on **both** surfaces.
>   - A Member or Viewer gets `403` `insufficient-role` with one `InsufficientRole` record. A non-member gets the boundary `404` with one `CrossSpace` record. Both come from the pipeline.
>   - **Do not use `ReadProjectsAndTasks`** for this read, as the Label list does. The body carries email addresses, which §6.1 and AD-23 confine to Owners and Admins.
> - **"Pending" means actionable**: the list reads through `OfferPredicates.Actionable`, so an expired Invitation is not shown (Q4).
> - **The body** is `200` with a JSON array of `InvitationResponse(Guid Id, string EmailAddress, string Role, string IssuerName, DateTimeOffset IssuedAt)` (Q5):
>   - `EmailAddress` is the stored, trimmed `Invitation.EmailAddress`;
>   - `Role` is the stable code (`admin`, `member`, `viewer`), the wire form 4.1's request uses;
>   - `IssuerName` is `Invitation.IssuerName`, the name captured at issue. **Never** a live `Account` lookup (`epics.md:1793`; UX-DR34);
>   - `IssuedAt` is `Invitation.CreatedAt`.
>   - **Never** in the response: `TokenHash`, `NormalizedEmailAddress`, `IssuedByAccountId`, `SpaceId`, `State` or `ExpiresAt`. A contract-shape gate holds exactly these five fields.
> - **Order and size**: newest first (`CreatedAt` descending, then `Id` descending). Unpaged (Q8).
> - **The projection selects exactly the five columns.** It never materialises the entity, so `Only_the_Invitation_store_and_its_configuration_read_an_Invitation_token_hash` stays green unchanged.

**AC2 — any Owner or Admin may revoke, including one who did not issue it** (FR-12, matrix)
**Given** a pending Invitation
**When** any Owner or Admin revokes it — including one who did not issue it
**Then** it can no longer be accepted

> - **The route** is `DELETE /api/v1/spaces/{spaceId:guid}/invitations/{invitationId:guid}` (Q1). Add `InvitationRoutes.Invitation`, `InvitationRoutes.InvitationIdParameter` and `InvitationRoutes.InvitationOf(Guid spaceId, Guid invitationId)`. It carries `.RequiresCapability(Capability.IssueAndRevokeInvitations)` on both surfaces. **Success is `204`, with no body.**
> - **The handler never compares the caller with the issuer** and never reads `IssuedByAccountId`. Role checks are the pipeline's (AR-3), and FR-12 grants the capability to every Owner and Admin.
> - **The write is one guarded statement** (Q10):
>
>   ```
>   UPDATE Invitation SET State = 'Revoked' WHERE Id = @id AND <OfferPredicates.Actionable>
>   ```
>
>   It is an `ExecuteUpdateAsync` composed with `.Where(OfferPredicates.Actionable)`, under both isolation layers, with a rowcount check. The predicate runs inside the statement, against `SYSDATETIMEOFFSET()`.
> - **"Can no longer be accepted"** is proved before 4.3 exists by three things (Q11):
>   - the row's `State` is `Revoked`;
>   - the row fails the shared actionable predicate, which is the predicate 4.3's acceptance must compose;
>   - **re-issuing the same address now succeeds** `204`, through AC13's actionable-pending check in 4.1, where before the revoke it answered `409 invitation-pending`.
>
>   4.3 owns the acceptance-route proof. Ledger it to 4.3.
> - **The proof that a non-issuer may revoke:** an Admin revokes an Invitation the Owner issued, and the Owner revokes one an Admin issued. Both are `204`.

**AC3 — survives its issuer's demotion, removal or Account deletion** (FR-12, AD-5)
**Given** an Invitation whose issuer has since been demoted, removed, or had their Account deleted
**When** it is inspected
**Then** it remains valid and revocable by any remaining Owner or Admin
**And** the reason is stated: it was legitimately issued and does not depend on its issuer's continuing authority

> - **Nothing in the list or the revoke depends on the issuer.** No join to `Membership`. No filter on `IssuedByAccountId`. No live name lookup.
> - **The demotion arm:** an Admin issues. `WireDatabase.ChangeRoleAsync` demotes them to Member. The Owner then lists and sees the Invitation, with the issuer name unchanged, and revokes it `204`. Separately, a second Admin revokes another Invitation from the same demoted issuer. 4.4 has no product path yet (Q12).
> - **The removal arm:** an Admin issues. `WireDatabase.RemoveMembershipAsync` removes them. The Owner lists and revokes as above. The `Invitation` row is untouched by the removal (AD-5).
> - **The Account-deletion arm is ledgered to 5.4** (Q12). The `IssuedByAccountId` foreign key is `Restrict` (4.1 AC12), so an `Account` row cannot be deleted while it issued an Invitation. 5.4's tombstone must keep the Invitation listed, with its `IssuerName`, and revocable.
> - **"The reason is stated"**: in the remarks of `Invitation`, `ListInvitationsHandler` and `RevokeInvitationHandler`, quoting FR-12. The list row states nothing about the issuer's current standing, because doing so would disclose a Membership change to the reader.

**AC4 — an accepted Invitation cannot be revoked** (FR-12, FR-14)
**Given** an already-accepted Invitation
**When** revocation is attempted
**Then** it is refused, because revocation is possible only before acceptance and removing an accepted invitee is FR-14 rather than FR-12

> - The guarded update matches no row, and the store then reads whether the id names an Invitation in this Space in any state. If it does, the answer is `409` with a new problem type, `urn:yello:problem:invitation-not-pending` (Q2). Nothing is written, and the row keeps `State = Accepted`.
> - **The same answer** is given for an Invitation that is already `Revoked`, and for one that is `Pending` but expired. An expired Invitation is terminal (FR-10), and AD-27 applies the predicate to every transition. **The expired row stays `Pending`**: nothing writes "expired" (Q4).
> - **An id that names no Invitation in this Space**, whether it never existed or is another Space's, gets the boundary `404 not-available` body, byte-identical and **not recorded** (Q3). This follows the Label delete (`LabelEndpoints.DeleteAsync:151`): an in-Space not-found crossed no boundary (AD-20). Under RLS another Space's row is invisible, so both arms take the same plan.
> - Accepted rows are seeded with `WireDatabase` (the R12 method). 4.3 has no product path yet.
> - **The race.** A revoke must not overwrite an acceptance that commits under it. See "Locking and the race" in the Dev Notes.

**AC5 — terminal state is retained, never deleted, and read by no product surface** (FR-10, AD-27, SM-4, SM-C3)
**Given** an Invitation leaving `Pending` by any route — accepted, revoked or expired
**When** the record is inspected
**Then** its terminal state is retained rather than the row being deleted
**And** no product surface reads it, its purpose being to keep SM-4 and SM-C3 derivable by an operator applying the same predicate

> - **Revoke is an `UPDATE`, never a `DELETE`.** After a revoke, `WireDatabase.InvitationsInAsync` still returns the row, with `State = Revoked` and every other column unchanged: address, Role, issuer, `ExpiresAt` and `TokenHash`.
> - **Expired is never written.** A row seeded with a past `ExpiresAt` stays `Pending` after the list reads it and after a refused revoke.
> - **No product surface reads terminal rows.** The list filters through the actionable predicate. The revoke's not-found disambiguation (AC4) reads **existence only**, to choose between `409` and `404`, and never returns a terminal row's fields (C6).
> - Space deletion still cascades every Invitation away (Lee's 4.1 Q1). That is the recorded exception, and it is unchanged.
> - **Gate (Q13):** the only production writer of `Invitation.State` is the revoke in `InvitationStore`. Read it from IL, as `The_only_bulk_writes_of_a_Space_are_the_rename_and_the_deletion_in_SpaceStore` does: an `ExecuteUpdate`/`ExecuteDelete` on `Invitation`, or a call to `Invitation.set_State`, anywhere else fails. No `ExecuteDelete` on `Invitation` exists anywhere. 4.3 amends the gate to add its accept.

**AC6 — terminal Invitations are not shown** (UX-DR14)
**Given** terminal Invitations
**When** the Invitation list renders
**Then** they are not shown, the list being what is outstanding rather than what has happened

> - **The wire proof:** one Space holds one live pending, one revoked, one accepted and one expired Invitation. The list returns exactly the live one. That row is the anchor, so an empty list cannot pass.
> - **The plant:** the list without the actionable predicate shows all four, and the case turns red.
> - **The client proof:** after a successful revoke, the row leaves the list once the server has answered `204`, and never before.

**AC7 — no email on revoke** (FR-39 "exactly one email per Invitation"; AD-26's notification rule)
**Given** a revocation
**When** it completes
**Then** no further email is sent, because there is exactly one email per Invitation and an already-delivered email cannot be recalled

> - The revoke enqueues nothing. Its handler takes no `IOutbox`, and a gate holds that `RevokeInvitationHandler`'s constructor names no outbox type.
> - **The wire proof**, with `MailDrop`:
>   1. Issue to a fresh address, and wait for its one email.
>   2. Revoke, then make one more authenticated request so the piggyback sweep would run.
>   3. Assert that `OutboxRowsAsync` holds no row for that recipient, and that the drop holds exactly one email to the address.
>
>   Scope the assertion to the case's own address (`TESTING-CONVENTIONS.md:505`).
> - **The revoke confirm's copy says so**: the email already sent cannot be recalled (Q9).

**AC8 — isolation rows on both surfaces** (AD-4; I-1, I-2, I-3, I-4, I-12, I-13, I-17; A-4; `TESTING-CONVENTIONS.md:704-811`)

> Both new routes fail `Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` and `Every_Space_scoped_route_a_Token_reaches_has_a_row_with_a_Token_of_the_same_Account_bound_elsewhere` until they have rows. Add the rows to `tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs` under a heading "Story 4.2 - list and revoke Invitations". Each row states both surfaces:
>
> - `ISO-LIST-INVITATIONS-FOREIGN-SPACE` (I-1): `BoundaryNotFound`.
> - `ISO-LIST-INVITATIONS-OWN-SPACE` (I-3): `Served(200)`. Space B holds a pending Invitation with a randomised address. A's list contains A's own seeded Invitation (the anchor) and never B's address or id.
> - `ISO-RECORD-KIND-LIST-INVITATIONS-MEMBER` and `-VIEWER`: `RoleRefused(InvitationWire.CapabilityCode)`.
> - `ISO-REVOKE-INVITATION-FOREIGN-SPACE` (I-2): `BoundaryNotFound`, with a `Then` proving that B's Invitation is still `Pending`.
> - `ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE` (I-4, I-13): A revokes B's Invitation id under A's own Space path. The answer is `SameAsAnchor(404, not-available)` against an id that never existed, with nothing recorded and B's Invitation still `Pending`. This is the `ISO-LABEL-FOREIGN-ID-IN-OWN-SPACE` model.
> - `ISO-RECORD-KIND-REVOKE-INVITATION-MEMBER` and `-VIEWER`: `RoleRefused`, and the Invitation stays `Pending`.
> - `ISO-TOKEN-OWN-OTHER-SPACE-LIST-INVITATIONS` and `ISO-TOKEN-OWN-OTHER-SPACE-REVOKE-INVITATION` (`IsolationVector.TokenOwnOtherSpace`).
> - The rules for these rows:
>   - Every seeded address is added to the actor's secrets (`HoldsSecret`), so the I-17 row log check sees it.
>   - If ATDD lands rows with `PendingUntil: "4.2"`, clear every one before review (GATE-003).
>   - Seed through the product (4.1's issue route) wherever an endpoint does it. Use `WireDatabase` only for the accepted and expired rows.

**AC9 — the list and revoke in Space settings** (UX: `EXPERIENCE.md:214`; `mockups/space-settings.html:139-143`, `:166-179`; UX-DR12, UX-DR14, UX-DR26, UX-DR27, UX-DR31, UX-DR33, UX-DR38; ledger `:532`, `:20`)

> - **Where.** The list goes in 4.1's `InvitationSection.razor`, below the form, in the same section (4.1 Q8; ledger `:532`). Each row is a new `InvitationRow.razor`, modelled on `ApiTokenRow.razor` and `LabelRow.razor`. The section stays under `InvitationCapabilities.PermitsIssueInvitations`. The predicate covers both halves of the one capability, so rename it only if you also update every gate that names it (Q14).
> - **Loading.** On initialise, register a purge (`ActiveSpace.RegisterPurge`), then `GET` the list with `ActiveSpace.Token`. Follow `LabelSection.LoadAsync`, with a generation counter and `aria-busy` while loading.
>   - A `404` is `OnSpaceRefused`.
>   - A `403` is the capability copy (`RefusalText.Of`). It cannot be reached in practice, because the section is absent for those Roles.
>   - **Render nothing from cache before the server answers** (UX-DR31).
> - **The purge** clears the list, any confirm, any in-flight revoke and any refusal, synchronously. The addresses are Space-scoped data (UX-DR31). Register it in the same way `LabelSection` does, so `ActiveSpaceStateTests` and the switch-memory tests can see it.
> - **A row** is an `<li>` in a `<ul>` and shows:
>   - the address, in the row's name slot with an `id` the Revoke button's `aria-describedby` names. Several Revoke buttons are then not identical controls to a screen reader (the `ApiTokenRow` precedent);
>   - the Role's display name via `RoleCopy.NameKey(code)`. Uppercase comes only through CSS (UX-DR42);
>   - "Sent by {IssuerName}" (Q15 copy);
>   - the issue date as `<time datetime="{IssuedAt:O}">` with a short date (Q6; the `ApiTokenRow.CreatedDate` precedent);
>   - a `button-secondary` "Revoke".
> - **Revoke asks first, in place** (Q9; UX-DR12, UX-DR26). The row's control is replaced by a warning, a `button-danger` "Revoke" and a `button-secondary` "Cancel", never a dialog. The warning is: "Revoke this invitation? The email already sent cannot be recalled." Cancel sends nothing and returns focus to the row's Revoke.
> - **Sending the revoke**: `Writes.SendWithoutBodyAsync(HttpMethod.Delete, InvitationRoutes.InvitationOf(SpaceId, id), key, ActiveSpace.Token)`. Take a fresh key per deliberate confirm, and keep it for an identical resend after no answer (the 2.5 and 4.1 precedent). **Never optimistic**: the row stays until the answer.
> - **The answers:**
>   - `204`: remove the row, then announce "Invitation revoked." through `LiveAnnouncement`. **The announcement names no address** (UX-DR38). Move focus to the list heading, or to the next row's Revoke if there is one. Never let it fall to `<body>` (UX-DR39's "nearest surviving container").
>   - `409 invitation-not-pending`: re-`GET` the list, because the row is no longer outstanding, then announce the refusal copy "This invitation is no longer pending." with no address.
>   - `404`: `OnSpaceRefused`, as the Label delete does. An unknown id renders the one refusal surface (UX-DR32).
>   - `403`: `RefusalText.Of`, in the section.
>   - No answer or a `5xx`: "Not sent." (`TaskEditNotSent`), and keep the key.
> - **After a successful issue**, the form's `204` triggers a re-`GET` of the list, because the `204` has no body (4.1 Q5). The new Invitation appears once the server has listed it. There is no optimistic row.
> - **Empty state**: "No pending invitations." (Q15). Nothing else: no prompt and no count-as-nudge (UX-DR26).
> - **A count beside the heading**, as in the mockup's "Invitations 2"? Not built (Q15). UX-DR26 bans counts-as-nudges, and the list's own length is visible.
> - **Copy** goes in `ClientCopy.resx`, in sentence case, with a `<comment>` naming 4.2. Add the new problem type to `RefusalCopy.OwnKeys`. CSS goes in `components.css` only, with logical properties and no px or colour literals.
> - **The address renders here and nowhere else.** This is Space settings. See C1 for UX-DR34's wording.
> - **Client tests** (`Yello.Tests.Client`, `ClientSurfaceCollection`, `FakeYelloApi`). Build `Yello.Client` in **Debug** first. Each test runs at a stated viewport. See the test list in Testing requirements.
> - **TextSpacing**: add `TextSpacingTests.Text_spacing_and_text_only_zoom_clip_nothing_in_the_Invitation_list` under both adaptations. Use a long-but-ordinary address and a long issuer name, with the confirm open on one row. Append to ledger `:20`.

**AC10 — the revoke is idempotent under retry** (AD-18, AR-25, P-10)

> - A replay with the same `Idempotency-Key` after a committed revoke returns the original `204` from the idempotency record. The row stays `Revoked`, and nothing else is written.
> - A **second, distinct** revoke (a new key) of the same Invitation answers `409 invitation-not-pending`.
> - A malformed key is `400`, from the pipeline.
> - `Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay` stays green: `204` with no body.

**AC11 — the state has one writer, and no timer** (A-10, AD-27; ledger `:723`; `test-design-architecture.md:319-327`)

> - **The gate from AC5**: the only production writer of `Invitation.State` is `InvitationStore`'s revoke. Plant: a second `ExecuteUpdateAsync` setting `State` in another type turns it red.
> - **The existing gate** `No_production_code_runs_on_a_timer_or_as_a_hosted_service` stays green. Together, the two gates are the concrete predicate A-10 asked for, as far as Invitations go. 5.3 keeps the `OwnershipOffer` half.
> - **Gate (i) from 4.1**, `Only_the_shared_actionable_predicate_compares_ExpiresAt`, stays green. The revoke and the list compose `OfferPredicates.Actionable` and state no comparison of their own. Plant: a revoke written `.Where(i => i.ExpiresAt > DatabaseClock.Now())` turns the gate red.

## Tasks / Subtasks

- [x] **Task 1: decide the open questions** (all ACs)
  - [x] Read the Dev Notes, then the open questions. Decide each one as a reversible decision and record it in Completion Notes as "Qn - (x)", with its cost. Each recommendation is the default, and no human is in the loop, so do not stop to ask.
  - [x] **Questions marked [SECURITY].** Q3 and Q16 each have an alternative that relaxes isolation or privacy. Take only the recommended option, which relaxes nothing. If you believe the alternative is needed, stop and return it as a blocker for Lee. Do not build it.
- [x] **Task 2: Contracts** (AC1, AC2, AC4)
  - [x] In `Yello.Contracts/Invitations/InvitationRoutes.cs`, add `List` (= `Issue`), `Invitation` (`/api/v1/spaces/{spaceId:guid}/invitations/{invitationId:guid}`), `InvitationIdParameter` and `InvitationOf(Guid, Guid)`.
  - [x] Add `Yello.Contracts/Invitations/InvitationResponse.cs` with `(Guid Id, string EmailAddress, string Role, string IssuerName, DateTimeOffset IssuedAt)`. **No property is named `Token`, `Secret` or `Plaintext`** (`Only_the_issuance_response_carries_a_Token_secret`).
  - [x] In `ProblemTypes`, add `InvitationNotPending => "urn:yello:problem:invitation-not-pending"`. Its remarks state that the type is answered `409`, is disclosable because the caller is an Owner or Admin of the Space, and covers accepted, revoked and expired.
- [x] **Task 3: Application** (AC1-AC5, AC7; C2)
  - [x] Add `Yello.Application/Invitations/ListInvitations/ListInvitationsHandler.cs`: `HandleAsync(CancellationToken)` returning `IReadOnlyList<PendingInvitation>`, a read model in Application that carries the five fields. It reads no Role and no Space.
  - [x] Add `Yello.Application/Invitations/RevokeInvitation/RevokeInvitationHandler.cs`: `HandleAsync(Guid invitationId, CancellationToken)` returning `InvitationRevocation { Revoked, NotPending, NotFound }`. It takes **no** `IOutbox` (AC7) and never reads `context.Role` or the issuer (AC2).
  - [x] Extend the port. Either add `ListPendingAsync` and `RevokeAsync` to `IInvitationStore`, or add a sibling port. Watch S1200, the reason 4.1 moved configurations around, and record the choice.
- [x] **Task 4: Infrastructure** (AC1, AC2, AC4, AC5)
  - [x] **The list read in `InvitationStore`:**
    - `Set<Invitation>().AsNoTracking().Where(OfferPredicates.Actionable)`;
    - `.OrderByDescending(CreatedAt).ThenByDescending(Id)`;
    - `.Select(...)` of the five columns only.
    - No `Where` on `SpaceId`: both layers already narrow it, as `IssueAsync` relies on.
  - [x] **The revoke in `InvitationStore`:**
    1. `Set<Invitation>().Where(i => i.Id == id).Where(OfferPredicates.Actionable).ExecuteUpdateAsync(s => s.SetProperty(i => i.State, InvitationState.Revoked))`;
    2. if the rowcount is 1, `Revoked`;
    3. if it is 0, `AnyAsync(i => i.Id == id)` (existence only, under RLS): `NotPending` if true, `NotFound` if false.

    No `SaveChanges` is needed after `ExecuteUpdate`. The pipeline's transaction holds the statement, and `IdempotencyStore` saves after. Confirm that `IdempotencyStore`'s "no pending changes" rule is satisfied: `ExecuteUpdate` leaves no tracked changes. No application lock and no `HOLDLOCK` (Q10).
  - [x] Index check: the revoke seeks `PK_Invitation (Id)`, and the list scans `(SpaceId, ...)`. If the list's plan is not a seek on a `SpaceId`-leading index, add `IX_Invitation_SpaceId_CreatedAt` in a migration (expand-only, hand-edited per `TESTING-CONVENTIONS.md:519-540`). Otherwise add no migration. Record which.
- [x] **Task 5: Host** (AC1, AC2, AC4, AC10)
  - [x] In `InvitationEndpoints`, add:
    - `MapListInvitations`: `MapGet(InvitationRoutes.List, ...)` with `.RequiresCapability(Capability.IssueAndRevokeInvitations)`, answering `Results.Ok(list.Select(Response).ToList())`;
    - `MapRevokeInvitation`: `MapDelete(InvitationRoutes.Invitation, ...)` with the same capability, answering `Revoked` → `Results.NoContent()`, `NotPending` → `409` `InvitationNotPending` with the title "This Invitation is no longer pending." and no address, and `NotFound` → `BoundaryProblems.NotAvailable()`, not recorded.
    - **No `_ =>` arm** that turns a new outcome into a `404`. This is 4.1 review P10.
  - [x] In `Program.cs`, register both handlers next to `IssueInvitationHandler` (`:132`) and both routes next to `MapIssueInvitation` (`:499`). Update the route comment block.
- [x] **Task 6: Client** (AC9)
  - [x] Change `InvitationSection.razor` (list, purge, loading, re-`GET` after issue and after `409`).
  - [x] Add `InvitationRow.razor`.
  - [x] Update `ClientCopy.resx` (Q15), `RefusalCopy.OwnKeys` and `components.css`.
  - [x] Rewrite `SpaceSettings.razor`'s and `InvitationSection.razor`'s header comments so they say what exists.
  - [x] Extend `tests/Yello.Tests.Client/FakeYelloApi.Invitations.cs` to cover `GET` (a seeded list per Space) and `DELETE` (a recorded revoke, a hold, and a `NextRevokeRefusal`). It must use `InvitationRoutes` and `ProblemTypes`, **never its own literals** (4.1 review P13).
- [x] **Task 7: tests and plants** (all ACs). See Testing requirements. Save each original in memory and restore it in a `finally`. **Never** use `git checkout --`, `git restore` or `git reset`. Record `| Plant | Expected red | Seen |` in the Debug Log.
- [x] **Task 8: ledger, docs, verification**
  - [x] Apply the ledger dispositions below to `deferred-work.md`. Keep each CLOSED or UPDATED note on one line, introduce no TAB or FORM FEED, and keep the final newline.
  - [x] `grep -rn "4\.2" Yello.* tests` and rewrite every comment that predicts 4.2 so it says what exists. This includes `OfferPredicates.cs:13`, `InvitationState.cs:24`, `IInvitationStore.cs:78`, `ProblemTypes.cs:393`, `InvitationSection.razor:14` and `SpaceSettings.razor:13`.
  - [x] `sprint-status.yaml`: this story goes to `review` at the end of dev. `epic-4` stays `in-progress`.
  - [x] Run `docker info --format '{{.ServerVersion}}'`, then `dotnet build Yello.slnx` (0 warnings, 0 errors), then `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Record the totals. The expected skips are the 3 bundle cases plus the explicit tiers. Rerun a failure once, alone, before calling it real.

### Review Findings

Code review 2026-11-01 (bmad-code-review, full mode, `f908505..HEAD`). Blind Hunter 16, Edge Case Hunter 10, Acceptance Auditor 8: 34 raw, 26 after deduplication. 0 decision-needed, 13 patch, 2 defer, 11 dismissed. Q3 and Q16 were verified as honoured (the existence read runs under both isolation layers; the list is under `IssueAndRevokeInvitations` on both surfaces).

- [x] [Review][Patch] Overlapping list reads: an older answer can overwrite a newer one, so a just-revoked row can reappear (medium) [Yello.Client/Components/InvitationSection.razor:252]
- [x] [Review][Patch] A failed re-read after a successful issue or a 409 (network error, timeout, 5xx) replaces the settings page with the Space refusal surface; keep the rows on screen instead (medium) [Yello.Client/Components/InvitationSection.razor:284]
- [x] [Review][Patch] Focus falls to `body` after a revoke that is not sent, refused or answered 409; return it to the row's Revoke, or to the next surviving row or the heading after a 409 (UX-DR39) (medium) [Yello.Client/Components/InvitationSection.razor:359]
- [x] [Review][Patch] After a 409 the refusal is announced even when a purge ran during the re-read [Yello.Client/Components/InvitationSection.razor:404]
- [x] [Review][Patch] The revoke's no-answer catch writes row state without the generation check the other paths use [Yello.Client/Components/InvitationSection.razor:362]
- [x] [Review][Patch] A revoke's `403` renders on the row; AC9 says "in the section" [Yello.Client/Components/InvitationSection.razor:409]
- [x] [Review][Patch] The fake API answers the boundary `404` for an id withdrawn "elsewhere", where the Host answers `409`; and it hard-codes the capability code instead of `InvitationWire.CapabilityCode` [tests/Yello.Tests.Client/FakeYelloApi.Invitations.cs]
- [x] [Review][Patch] The post-revoke focus test only asserts focus is not `body`; assert the next row's Revoke [tests/Yello.Tests.Client/InvitationListTests.cs]
- [x] [Review][Patch] The Member/Viewer client test does not assert that the Space settings link is absent [tests/Yello.Tests.Client/InvitationListTests.cs]
- [x] [Review][Patch] Two Slices cases seed a live pending Invitation through `WireDatabase` instead of 4.1's issue route (Testing requirements; AC8 rules) [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ListInvitations.cs:222, ActiveSpaceEndpointTests.RevokeInvitation.cs:388]
- [x] [Review][Patch] Stale comments that still predict 4.2 or `InvitationResponse` (Task 8) [Yello.Client/Localisation/ClientCopy.resx:1100, ActiveSpaceEndpointTests.ListInvitations.cs:23]
- [x] [Review][Patch] The Dev Agent Record says 11 isolation rows; AC8 and the diff have 10 [this file, Change Log and ATDD table]
- [x] [Review][Patch] The AC7 wire case is not a guard against an outbox plant; say so in the plant table (P9) [this file, Debug Log]
- [x] [Review][Defer] Focus drops to `body` while a revoke is in flight, because the confirming button is replaced by a status span; LabelRow and ApiTokenRow share the pattern [Yello.Client/Components/InvitationRow.razor:35] — deferred, shared pre-existing pattern; owner 4.4
- [x] [Review][Defer] The Invitation IL gates read only IL calls: raw SQL, expression-tree filters in the store, or an outbox write from the store escape `Only_the_Invitation_store_writes_an_Invitation_state`, `Neither_the_list_nor_the_revoke_reads_the_issuers_Account` and `The_revoke_handler_takes_no_outbox`; the wire cases are the real guard [tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationRevoke.cs:107] — deferred; owner 4.3, which amends these gates

## Dev Notes

### The shape of the story in one paragraph

Two new routes on the path 4.1 created, both under `IssueAndRevokeInvitations` and both on both surfaces. `GET .../invitations` returns the Space's **actionable** pending Invitations (address, Role code, issuer name captured at issue, issue date), newest first. `DELETE .../invitations/{id}` sets `State = Revoked` with one guarded `UPDATE` whose `WHERE` composes the shared actionable predicate, then answers by rowcount: `204`, or `409 invitation-not-pending` when the id exists in this Space but is accepted, revoked or expired, or the boundary `404` body (unrecorded) when it names nothing here. Nothing is deleted, nothing is emailed and nothing reads the issuer. The client adds the list to 4.1's Invitations section, with an in-place confirm, no optimistic removal, a purge on leaving the Space, and no address in any announcement.

### What exists today (survey at `4a393a4`)

- **Domain:**
  - `Invitation` (`Yello.Domain/Invitations/Invitation.cs`). `State` has a public `set`. `SpaceId` and `Role` are `init`. It has `ExpiresAt`, `TokenHash`, `IssuedByAccountId`, `IssuerName` (max 128) and `CreatedAt`.
  - `InvitationState { Pending, Accepted, Revoked }`. "Expired" is never stored. There is no `Lapsed` (4.1 Q11).
- **Application:** `IInvitationStore.IssueAsync` and `InvitationIssue`/`InvitationIssueOutcome`. `IssueInvitationHandler` is the slice template, with the remarks style to follow.
- **Infrastructure:**
  - `InvitationStore`: the lock, then the Membership read, then the actionable-pending read, then the insert. It uses `Set<Invitation>()`, because `YelloDbContext` has no `DbSet<Invitation>` (S1200).
  - `OfferPredicates.Actionable` and `DatabaseClock.Now()` (mapped to `SYSDATETIMEOFFSET`).
  - `InvitationConfiguration`/`InvitationModel`: the query filter on `ScopedSpaceId`, the cascade from Space, `Restrict` to Account, the unique `TokenHash`, and an index on `(SpaceId, NormalizedEmailAddress)`.
  - The RLS FILTER and BLOCK predicates (insert and update) are in migration `20261101140742_Invitations`. The BLOCK AFTER UPDATE predicate allows the revoke's update, because the `SpaceId` is unchanged.
- **Host:**
  - `InvitationEndpoints.MapIssueInvitation`, with the explicit outcome switch.
  - `LabelEndpoints.DeleteAsync` is the not-found → unrecorded boundary body precedent.
  - `ApiTokenEndpoints.MapRevokeApiToken` is the `DELETE`-as-revoke precedent.
  - `ApiNoStore` already marks every `/api` response `no-store`, so the list's addresses are never cached.
- **Client:**
  - `InvitationSection.razor` (the form). `InvitationCapabilities.PermitsIssueInvitations` and `OfferableRoles`.
  - `SpaceSettings.razor` with its `SectionKey.Invitations`.
  - `ApiTokenRow.razor`: the in-place confirm, and `aria-describedby` naming the row.
  - `LabelSection.razor`/`LabelRow.razor`: the purge, the generation counter, the delete answers.
  - `SpaceWrites.SendWithoutBodyAsync`, `RefusalText.Of`, `RoleCopy.NameKey`, `LiveAnnouncement`.
- **Tests:**
  - `WireDatabase.AddInvitationAsync(spaceId, address, role, expiresAt)` (extend it to take a `State`, or add an overload, for the accepted and revoked seeds), `InvitationsInAsync`, `ChangeRoleAsync`, `RemoveMembershipAsync`, `OutboxRowsAsync`.
  - `InvitationWire` (`FreshAddress`, `Body`, `CapabilityCode`) and `MailDrop`.
  - The Slices partials `ActiveSpaceEndpointTests.IssueInvitation*.cs`.
  - The isolation rows in `IsolationCases.Invitations.cs`.
  - `FakeYelloApi.Invitations.cs`.
  - The gates in `RefusalBoundaryGateTests.Invitations.cs`.

### What must be preserved (regression guard)

- **4.1's issue path, byte for byte.** That covers the `204`, the AD-23 oracle closure and its timing row, the lock order and the outbox. Adding a `GET` on the same path must not change the `POST`'s metadata or capability.
- **Gate (i)**: `ExpiresAt` is compared only in `OfferPredicates`. **The token-hash reader gate**: the list never selects `TokenHash`.
- **The boundary `404` stays byte-identical** (`RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal`). No `403` becomes a `404` or the reverse. No new boundary problem type.
- **No timer and no hosted service.** No new key-setting line. No dual-key unit of work.
- **Space deletion stays one statement.** It cascades Invitations in every state.
- **The Space settings narrated exit** on a Role drop. `SpaceNavigationGateTests` and `ClientStaticStateGateTests`: the new row and list add no mutable static and no `AddSingleton`.
- **Copy gates** (3.3): no Razor `else` and no `@if (!(...))` around user-visible text. Use two `@if`s or helper predicates, as `InvitationSection` already does.
- **`IdempotencyStore` refuses to save with other changes pending.** The revoke's `ExecuteUpdate` tracks nothing, so the pipeline's record saves cleanly. Verify that with the replay case.

### Locking and the race (RCSI; `TESTING-CONVENTIONS.md:167-180`)

The revoke is a **single guarded statement**, so no check-then-write window exists and no application lock is needed (Q10).

Under RCSI, an `UPDATE` reads the latest committed row version and takes U and X locks on the row. A concurrent writer holding an uncommitted change to the same row (4.3's accept, or a second revoke) makes it wait. On release it re-evaluates `State = Pending AND ExpiresAt > SYSDATETIMEOFFSET()` against the committed result.

The deterministic race test, in Slices (`ActiveSpaceEndpointTests.RevokeInvitationRace.cs`), follows "the test does the other write":

1. Seed a pending Invitation.
2. Open a holder under the Space's session key. Begin a transaction, and `UPDATE Invitation SET State = 'Accepted' WHERE Id = @id`. Do not commit.
3. Fire the revoke.
4. `WaitUntilBlockedOnALockAsync` (`LCK_M_%`).
5. Commit the holder.
6. Assert `409 invitation-not-pending`, and that the row is `State = Accepted`.

**Plant:** the revoke without the actionable guard (`WHERE Id = @id` only). The row ends `Revoked`, overwriting an acceptance, and the case goes red. A second plant: a load-check-save (`FindAsync`, then a C# `State == Pending` check, then `SaveChanges`). Expected red: the race case, because the C# check read the committed `Pending` before the holder committed. Record either as masked if it is caught 0/3.

The revoke does **not** take `Yello/Invitations/{spaceId}`. A revoke committing between an issue's actionable-pending read and its insert can only make that issue answer `invitation-pending` for an Invitation that was just revoked. The answer is benign and visible, and a retry succeeds. Record it. Do not lock for it.

### Open questions (decide in Task 1; each is reversible; record as "Qn - (x)")

Nothing here is marked [LEE]. **[SECURITY]** marks a question with an alternative that would relax isolation, RLS or privacy. For those, take the recommended option, which relaxes nothing.

1. **The revoke route.**
   - **(a) Recommended. `DELETE /api/v1/spaces/{spaceId}/invitations/{invitationId}`, `204`.** It is the API Token revoke's precedent (`ApiTokenRoutes.Revoke`). The resource leaves the API's view, because the list no longer shows it, although the row is retained (AD-27). The client calls `SendWithoutBodyAsync`.
   - (b) `POST .../invitations/{invitationId}/revocation`. It is truer to "the row is kept", but it adds a route shape no other revoke uses.
2. **The answer for a non-actionable Invitation** (accepted, revoked or expired).
   - **(a) Recommended. `409 urn:yello:problem:invitation-not-pending`, one type for all three.** This is AD-26's rule ("409 with a stable problem type, never 404: the caller holds a Membership"). It is disclosable: the caller is an Owner or Admin who could see the row while it was pending. The response carries no address and does not say which terminal state applies.
   - (b) `204` for an already-revoked Invitation (idempotent by state). Rejected: AC10's `Idempotency-Key` is the retry mechanism, and the AC requires that an accepted Invitation be "refused".
3. **[SECURITY] An unknown id, or another Space's id under one's own Space path.**
   - **(a) Recommended. The boundary `404 not-available` body, byte-identical and not recorded.** This is the Label delete's precedent. Under RLS both arms see no row, so neither the plan nor the answer differs. The `ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE` row proves it.
   - (b) `409` for an id that exists anywhere. **Relaxes isolation**: it would need a read outside the Space's policy and would disclose that the id exists in another Space. Never build it.
4. **Expired Invitations.**
   - **(a) Recommended. Not listed, and not revocable (`409`), with the row left `Pending`.** FR-10 calls expired terminal. UX-DR14 hides terminal rows. AD-27 applies the predicate to every read and every transition and never writes "expired". FR-39's "must be reissued" is already open, because 4.1's AC13 lets an expired Invitation be re-issued.
   - (b) List expired rows with an "Expired" marker so the issuer knows to reissue. Rejected: it contradicts "terminal Invitations ... are not shown".
5. **The list response.**
   - **(a) Recommended. `(Id, EmailAddress, Role, IssuerName, IssuedAt)`.** These are exactly UX-DR14's address, Role and issuer, plus the id the revoke needs and the date the mockup shows.
   - (b) Also `ExpiresAt`. It is not required. Add it later as an additive field if UX asks (AD-19 allows additive change).
   - Never in either option: the token hash, the normalised address, the issuer's Account id, or `State`.
6. **How the date renders.**
   - **(a) Recommended. An absolute short date in a `<time datetime>` element**, as `ApiTokenRow.CreatedDate` does.
   - (b) The mockup's "3 days ago". UX-DR42 requires relative time "computed against the **server** clock", so the response would need a server "now". Rejected for 4.2. See C3.
7. **"Sent by you".**
   - **(a) Recommended. The issuer's captured name only, always.** No `issuedByCaller` flag and no Account id on the wire.
   - (b) Add an `IssuedByCaller` boolean so the row reads "Sent by you", as the mockup does. Cost: one field and a comparison with the context's `AccountId`. It is harmless, but it was not asked for.
8. **Order and paging.**
   - **(a) Recommended. Newest first and unpaged.** NFR-8 sets no Invitation bound, and inventing one is scope creep (4.1 C7). Every Invitation expires after 7 days, so the list holds at most 7 days of issues. The unbounded case is an issuer scripting issues, which is D3's abuse. Add "the list grows with it" to D3's ledger entry (owner 4.6, Lee/PM).
   - (b) Page at 50 with a keyset on `(CreatedAt, Id)`. Cost: a paging contract and client paging for a list that is normally short.
9. **Revoke confirmation.**
   - **(a) Recommended. Confirm in place**, as `ApiTokenRow` and `LabelRow` do. The warning is "Revoke this invitation? The email already sent cannot be recalled.", with Revoke as the danger action and Cancel. Not optimistic (UX-DR26). The confirm ladder (UX-DR36) has no Invitation rung, so this is the lightest in-place form.
   - (b) Revoke on one click. Rejected: it is destructive and irreversible, and the person cannot re-send an identical email without re-issuing.
10. **Concurrency.**
    - **(a) Recommended. One guarded `ExecuteUpdate` with a rowcount check, and no application lock.** See "Locking and the race".
    - (b) Take `Yello/Invitations/{spaceId}` first. It serialises with issue for no invariant gain.
11. **Proving "can no longer be accepted" before 4.3.**
    - **(a) Recommended.** Prove it through the state, the shared predicate and re-issue (AC2). Ledger to **4.3**: "the acceptance route answers a revoked Invitation with the uniform 'This invitation is no longer valid.'". 4.3's AC already lists revoked among its five states.
    - (b) Build a test-only acceptance probe. Rejected: it is test code in production (1.9 Q4).
12. **The issuer-loss arms.**
    - **(a) Recommended.** Run demotion and removal through `WireDatabase.ChangeRoleAsync` and `RemoveMembershipAsync`, because no product path exists until 4.4 and 4.5. Ledger the Account-deletion arm to **5.4**: "an Account that issued Invitations is tombstoned, and its Invitations stay listed with their `IssuerName` and revocable (FR-12, AD-5); the `Restrict` foreign key must not block deletion". When 4.4 and 4.5 land, they re-run these arms through their own routes. Add that to each one's ledger line.
    - (b) Defer the whole AC3 to 4.4, 4.5 and 5.4. Rejected: two of its three arms are provable now.
13. **The A-10 state-writer gate.**
    - **(a) Recommended. Build it now**: `Only_the_Invitation_store_writes_an_Invitation_state`, an IL scan for `ExecuteUpdate`/`ExecuteDelete` generic over `Invitation` and for calls to `Invitation.set_State`. The allowed writer is `InvitationStore`, and its anchor is the revoke. 4.3 adds its accept. Update ledger `:723`. 5.3 keeps the `OwnershipOffer` half.
    - (b) Leave it to 5.3. Rejected: 4.2 introduces the first writer, and the ledger names it.
    - Note: `InvitationStore.IssueAsync` constructs the entity with `State = Pending` in an object initializer, which compiles to `set_State`. The gate allows `InvitationStore` as a type, which covers both. Record that the insert of `Pending` is the other sanctioned write.
14. **The client predicate's name.**
    - **(a) Recommended. Keep `InvitationCapabilities.PermitsIssueInvitations`** for the section as a whole. List and revoke are the same capability (`issue-and-revoke-invitations`), so one predicate is correct. Rename it only together with every gate that names it.
    - (b) Rename it to `PermitsManageInvitations`. Cost: the gate `The_clients_invite_predicate_agrees_with_the_capability_matrix`, `ClientRolePredicate`'s name-fragment uniqueness check and `SpaceCapabilities.PermitsSpaceSettings` all change.
15. **Copy** (`EXPERIENCE.md:99`: terse and factual, no exclamation marks; sentence case).
    - **(a) Recommended:**
      - "Pending invitations" (list sub-heading under the existing "Invitations" heading);
      - "Sent by {0}";
      - "Revoke";
      - "Revoke this invitation? The email already sent cannot be recalled.";
      - "Cancel";
      - "Revoking.";
      - "Invitation revoked." (no address);
      - "This invitation is no longer pending." (`invitation-not-pending`);
      - "No pending invitations.";
      - "Loading invitations." (visually hidden, as `LabelsLoading`).
    - No count in the heading (UX-DR26).
    - (b) Any equivalent that keeps the rules: no address in announcements, no exclamation marks.
16. **[SECURITY] The list's capability.**
    - **(a) Recommended. `IssueAndRevokeInvitations`** (Owner and Admin). A Member or Viewer `GET` is `403` and recorded. The body carries email addresses, which §6.1 and AD-23 confine to Owners and Admins.
    - (b) `ReadProjectsAndTasks`, as the Label list uses, with addresses masked for lesser Roles. **Relaxes §6.1 privacy and AD-23** in any variant that returns an address, and needs a slice that reads Role (banned by AR-3). Never build it.

**Summary.** No question is routed to Lee. Q3 (b) and Q16 (b) would relax isolation or privacy and are marked [SECURITY]. Their recommended options relax nothing. No recommended option adds a policy-free table, a key-setting line, a dual-key unit of work, or a read outside the Space's RLS.

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. UX-DR34 against UX-DR14 on where addresses render.** UX-DR34 says "No surface outside Space settings' **Membership management** renders an email address". UX-DR14 and `EXPERIENCE.md:214` explicitly give the Invitation list "the address". Both live in Space settings and serve the same Owners and Admins (§6.1). Follow UX-DR14, and read "Membership management" as Space settings' people sections. If a client gate restricts where an address may render, extend its allowed list with `InvitationRow` and record that. Do not loosen it beyond that one row.
- **C2. The folder.** `ARCHITECTURE-SPINE.md:395` shows `Memberships/RevokeInvitation/`. 4.1 settled on `Application/Invitations/` (its C4). Use `Application/Invitations/RevokeInvitation/` and `Application/Invitations/ListInvitations/`.
- **C3. The mockup's relative time** ("3 DAYS AGO", `space-settings.html:141`) against UX-DR42's "relative time computed against the server clock". Q6 renders an absolute date.
- **C4. The mockup's "Invite by email" link** (`:143`) against 4.1's inline form. 4.1's form stands.
- **C5. "Expired" as a terminal state** (FR-10) against AD-27's "never written". Expired is derived on read. It is hidden by the predicate and refused by the guarded update, and the stored state stays `Pending`.
- **C6. "No product surface reads it" against the revoke's `409`/`404` choice.** On a missed guarded update, the revoke reads whether the id **exists** in this Space, to tell `409` from `404`. It returns no field of the terminal row and does not name its state. Read the AC as "no product surface **shows** a terminal Invitation". The existence read serves AD-26's 409 rule, and the 409 is disclosable to an Owner or Admin who saw the row while it was pending.
- **C7. The mockup's "Invitations 2" count** against UX-DR26's ban on counts-as-nudges. Not built (Q15).

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:532` → append "**UPDATED by 4.2:** the pending list and revoke are in the Invitations section (`InvitationSection.razor`, `InvitationRow.razor`). Remaining owners: 4.4, 5.1, 6.3."
- `:723` → append "**UPDATED by 4.2 (Q13 (a)):** `Only_the_Invitation_store_writes_an_Invitation_state` holds that `InvitationStore` (the insert and the revoke) is the only writer of `Invitation.State`, with the plant result. 4.3 adds its accept to the gate. 5.3 keeps the `OwnershipOffer` half."
- `:20` → append "**UPDATED by 4.2:** the Invitation list", with the test name. It stays open.
- D3, the rate limit (code review of 4.1) → append that the unpaged list grows with unlimited issuing (Q8). The owner is unchanged.
- **New entries:**
  - The acceptance route must answer a revoked Invitation with the uniform "no longer valid" response. Owner **4.3**. Severity low.
  - The issuer-deleted arm of FR-12. Owner **5.4**. Severity medium, because the `Restrict` foreign key will block Account deletion unless 5.4 tombstones (Q12).
  - Re-run AC3's demotion and removal arms through the product's own routes. Owners **4.4** and **4.5**. Severity low.
- Give any other deferral an owner and a severity.

### Architecture compliance (must)

- **AD-21 ring.** The handlers and the read model go in Application, the store in Infrastructure, the endpoints in Host and the contracts in Contracts. Check `AllowedReferenceEdges.cs` before adding any edge.
- **AR-3.** The slices never read Role, Membership, bounds, idempotency or the issuer.
- **AD-2.** No `Where(SpaceId == ...)` is needed. Both layers narrow it, and adding a C# Space filter is allowed but redundant. `FromSql` and raw SQL stay in Infrastructure only.
- **AD-3.** `403` for Role, the boundary `404` for a non-member and for an unknown id, and `409` for a terminal Invitation. No handler converts one into another.
- **AD-27.** One predicate, composed, translated to SQL and using the database clock. No C# expiry check, and no written "expired".
- **AD-18.** The revoke replays from the idempotency record.
- **Logs.** `SpaceId` and the Invitation id only, never the address, issuer name or token. If you add a `[LoggerMessage]`, `No_log_message_takes_an_address_a_token_or_an_email_body` must stay green.

### Library / framework

No new package. Use:

- EF Core 10.0.11: `ExecuteUpdateAsync` with `SetProperty`, which translates the composed `.Where(OfferPredicates.Actionable)` including the `HasDbFunction` call. Read the generated SQL once with logging at Debug in a local run to confirm `SYSDATETIMEOFFSET()` is inside the `UPDATE ... WHERE`. Do not leave that logging on.
- ASP.NET Core 10.0.11 minimal APIs: `MapGet` and `MapDelete` on `InvitationRoutes` templates.
- Blazor WebAssembly 10.0.11.
- xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3, Mono.Cecil (already used by the IL gates).

No live web check was run in this phase. Nothing here depends on a version-specific API beyond what 4.1 already uses.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:

- Use only these traits: `Suite`, `Priority`, `Requirement` (cite FR-12, FR-10, UX-DR14, AR-31/AD-27, AD-18) and `Tier`. Add `Assumption` `PRD-12-9` to anything that rests on the 7 days, such as the expired seed relative to issue.
- Name each test as an underscore sentence. Never use `Task.Delay`. Races hold a lock and wait on `LCK_M_%`.
- **Never a shared email address**: use `InvitationWire.FreshAddress`.
- Seed through the product (4.1's issue route). Use `WireDatabase` only for the accepted, revoked-by-seed and expired rows, and for the demotion and removal arms (Q12).
- **No new container-backed test class.** Add partials `ActiveSpaceEndpointTests.ListInvitations.cs`, `.RevokeInvitation.cs` and `.RevokeInvitationRace.cs`, and add rows to `IsolationCases.Invitations.cs`. Handler unit tests take no database (the `IssueInvitationHandlerTests` template).
- **Every absence has an anchor and is seen red under a plant** (`:192`). A plant caught 0/3 is recorded as masked, never deleted.
- **Mail assertions are scoped to the case's own address** (`:505`).
- **Memory budget (CLAUDE.md).** Never run two `dotnet test` processes at once. Iterate with `--project ... --filter`. A full run uses `--max-parallel-test-modules 1`. Rerun Client failures alone before calling them real.

**Slices (wire), each its own test:**

- The list shows a pending Invitation's address, Role code, issuer name and issue date, newest first.
- The list excludes revoked, accepted and expired Invitations, anchored on a live one (AC6).
- The list is `[]` for a Space with none.
- A Member's and a Viewer's `GET` is `403`.
- An Admin revokes the Owner's Invitation, `204`, and the reverse (AC2).
- After a revoke, the row is retained as `Revoked` with every other column unchanged (AC5).
- After a revoke, re-issuing the same address is `204`, where before it was `409 invitation-pending` (AC2).
- The demotion arm, and the removal arm (AC3).
- An accepted Invitation answers `409` and stays `Accepted` (AC4).
- An already-revoked Invitation answers `409`.
- An expired Invitation answers `409` and stays `Pending` (AC4, AC5).
- An unknown id answers the boundary `404` body with no `AccessRefusal` row.
- A replay with the same key answers `204`. A new key answers `409` (AC10).
- No email after a revoke (AC7).
- The race (Locking).
- Neither the address nor the issuer name reaches the Host log on list or revoke (I-17; extend 4.1's log case).

**Architecture gates:**

- The `GET` and `DELETE` capability gate, modelled on `RefusalBoundaryGateTests.Labels.cs:62`: both are `IssueAndRevokeInvitations`.
- `The_Invitation_list_response_carries_the_id_address_Role_issuer_and_date_and_nothing_else`.
- `Only_the_Invitation_store_writes_an_Invitation_state` (Q13).
- `The_revoke_handler_takes_no_outbox` (AC7).
- The problem type is declared and has client copy. The 4.1 gate `The_invitation_problem_types_are_declared_and_each_has_client_copy_naming_story_4_1` will not cover a 4.2 type. Either generalise it to "naming the story that declared it", or add a 4.2 sibling, and record which.

**Isolation:** the AC8 rows.

**Client (`InvitationTests.cs` partials, or `InvitationListTests.cs` in the same collection):**

- An Owner and an Admin see each row's address, Role name, "Sent by" name and date. The Admin sees Revoke on a row the Owner issued.
- A Member and a Viewer have no list and no settings link. The anchor is the Admin case. The plant renders the section for a Member.
- Revoke asks first. Cancel sends nothing and returns focus.
- Confirming sends `DELETE` with an `Idempotency-Key`. The row stays while the fake holds the answer, and goes after `204` (not optimistic).
- The announcement on revoke contains no `@`.
- A `409` re-reads the list and says "This invitation is no longer pending.".
- After a successful issue, the list re-reads and shows the new row.
- The empty state.
- Leaving the Space purges the list. Use the `SpaceSwitchMemoryTests` pattern: switch Space while the list is shown, and no address from the departed Space remains in the DOM.
- The 320×256 reflow with a long address and the confirm open.
- `TextSpacingTests`: the list under both adaptations.

**Minimum plant table.** Restore each in a `finally`. Rebuild `Yello.Client` in Debug before each browser plant.

| # | Plant | Expected red |
|---|---|---|
| 1 | The list without `OfferPredicates.Actionable` | AC6 (four rows, not one) |
| 2 | The revoke without the actionable guard (`WHERE Id` only) | the race case; the accepted and expired `409` cases |
| 3 | The revoke as load, C# check, `SaveChanges` | the race case (or record as masked) |
| 4 | The revoke as `ExecuteDelete` | AC5 retention; `Only_the_Invitation_store_writes_an_Invitation_state` if in another type |
| 5 | A second `State` writer in another type | the state-writer gate |
| 6 | The list route under `ReadProjectsAndTasks` | the capability gate; the Member and Viewer rows |
| 7 | `RequiresCapability` removed from the revoke | `Every_spaceId_endpoint_declares_exactly_one_capability`; the Member and Viewer revoke rows |
| 8 | The response gains `NormalizedEmailAddress` or `IssuedByAccountId` | the response-shape gate |
| 9 | The revoke enqueues an outbox row | AC7 wire case; the no-outbox gate |
| 10 | The unknown-id branch answers `409` (or records a refusal) | `ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE`; the unknown-id case |
| 11 | The revoke filters `IssuedByAccountId == caller` | AC2 non-issuer case; AC3 arms |
| 12 | The client removes the row before the answer | the not-optimistic client case |
| 13 | The announcement includes the address | the announcement case |
| 14 | The purge registration skipped | the switch-purge case (or record as masked, as 3.4 found for Labels) |
| 15 | A revoke written with its own `ExpiresAt >` comparison | gate (i) |

**Run** (CLAUDE.md):

1. `docker info --format '{{.ServerVersion}}'`.
2. `dotnet build Yello.slnx` (0/0).
3. `dotnet build-server shutdown`.
4. `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`.

### Previous story intelligence (4.1)

- **The scaffolds' route constants were temporary.** 4.1's ATDD `InvitationScaffold` was deleted for `InvitationRoutes`/`ProblemTypes` plus a permanent `InvitationWire`. Extend `InvitationWire`, and do not add a second scaffold.
- **Every Host sweeps the whole outbox at start**, so a mail count must be per address. A stale-claim row can land in a later case's drop.
- **4.1 review P10:** an endpoint's `_ =>` arm must not turn an unexpected outcome into the boundary `404`. Match each outcome, and throw on an unknown one.
- **4.1 review P13:** the fake API must use contract constants, not literals.
- **4.1 review P14:** never poll SQL back to back. Use a deadline with a small delay that is not `Task.Delay` used as synchronisation (the existing `OutboxEventuallyHoldsNoRowForAsync` shape after its fix).
- **S1200** pushed 4.1 to drop `DbSet<Invitation>` and split configurations. Adding two methods to `InvitationStore` may hit it, so split by port if it does.
- **The Q13 drop folder** is a test sender only. 4.2 sends nothing, so it changes no email configuration.
- **Lee's 4.1 decisions stand:** Q1 cascade on Space deletion, Q2 an Admin offers Admin, Q3 NFC plus BIN2, Q6 the policy-free outbox deleted on send.
- **3.4 and 3.3:** no `HOLDLOCK`; `TokenOwnOtherSpace` rows for every Token-reachable route; the copy gates' Razor rules; duplicate sibling `@key`s throw.
- **2.4 and 1.8:** in-place confirm, never stacked; the danger button only inside the confirm; `aria-describedby` naming the row.

### Git intelligence

Recent commits:

- `4a393a4` (4.1 review close: the file-drop sender guard)
- `d6310d4` (4.1 implementation)
- `dc61dd3` (4.1 ATDD scaffolds)
- `65d3b4a` (Lee's 4.1 decisions recorded)
- `e57aa55` (4.1 story)

The pattern is story, then ATDD scaffolds (`[Fact(Skip = "red: ACn - ...")]` plus `atdd-checklist-<key>.md`, and rows with `PendingUntil`), then implementation, then review fixes. The orchestrator commits each one. 4.1's files to extend are listed in its File List (`4-1-invite-an-email-address-to-a-space-at-a-role.md:939-1028`).

### Project Structure Notes

- **New (expected):**
  - `Yello.Application/Invitations/ListInvitations/ListInvitationsHandler.cs`
  - `Yello.Application/Invitations/RevokeInvitation/RevokeInvitationHandler.cs`
  - the read model, for example `Yello.Application/Invitations/PendingInvitation.cs`, and `InvitationRevocation`
  - `Yello.Contracts/Invitations/InvitationResponse.cs`
  - `Yello.Client/Components/InvitationRow.razor`
  - tests:
    - `ActiveSpaceEndpointTests.ListInvitations.cs`
    - `ActiveSpaceEndpointTests.RevokeInvitation.cs`
    - `ActiveSpaceEndpointTests.RevokeInvitationRace.cs`
    - handler unit tests under `tests/Yello.Tests.Slices/Invitations/`
    - gate additions in `RefusalBoundaryGateTests.Invitations.cs`
- **Updated (expected):**
  - `Yello.Application/Invitations/IInvitationStore.cs`
  - `Yello.Infrastructure/Persistence/InvitationStore.cs`, and possibly `SpaceContentStores.cs` (registration)
  - `Yello.Contracts/Invitations/InvitationRoutes.cs`, `Yello.Contracts/ProblemTypes.cs`
  - `Yello.Host/Endpoints/InvitationEndpoints.cs`, `Yello.Host/Program.cs`
  - `Yello.Client/Components/InvitationSection.razor`, `Yello.Client/Pages/SpaceSettings.razor` (comment only), `Yello.Client/Localisation/ClientCopy.resx`, `Yello.Client/Refusals/RefusalCopy.cs`, `Yello.Client/wwwroot/css/components.css`
  - comments in `Yello.Domain/Invitations/Invitation.cs`, `InvitationState.cs` and `Yello.Infrastructure/Persistence/OfferPredicates.cs`
  - `tests/Yello.Tests.Shared/WireDatabase.cs` (a seed with a state), `InvitationWire.cs`
  - `tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs`
  - `tests/Yello.Tests.Client/FakeYelloApi.Invitations.cs`, `InvitationTests.cs`, `TextSpacingTests.cs`
  - `_bmad-output/implementation-artifacts/deferred-work.md`, `sprint-status.yaml`
- **A migration only if** Task 4's index check needs one. **No new project, no new package, and no new container-backed test class.**

### References

- `epics.md`: `:96` (FR-12), `:178` (§6.1), `:213-220` (AR-25 to AR-32), `:252` (UX-DR14), `:267` (UX-DR26), `:271` (UX-DR27), `:275` (UX-DR31), `:278` (UX-DR34), `:280` (UX-DR36), `:282` (UX-DR38), `:370` (coverage), `:1665-1701` (4.2), `:1703-1753` (4.3, for its boundaries).
- `prd.md`: `:258-291` (FR-10 to FR-12), `:336-352` (matrix), `:755` (NFR-9), `:763-765` (§6.1), `:862-871` (SM-4, SM-C3), `:900` (§12 #9).
- `ARCHITECTURE-SPINE.md`: `:71-112` (AD-1 to AD-6), `:182-187` (AD-18), `:195-199` (AD-20), `:213-217` (AD-23), `:238-255` (AD-26 to AD-28), `:395`, `:418` (folders).
- `EXPERIENCE.md`: `:99` (voice), `:214` (Invitation list), `:211` (picker defaults), State Patterns (live regions, focus destinations).
- `mockups/space-settings.html`: `:139-143` (Owner), `:166-179` (Admin, revoking another's Invitation).
- `specs/spec-yello/`: `SPEC.md:81-82`; `acceptance-criteria.md:126-131` (CAP-12); `role-capability-matrix.md:19`.
- `test-design-qa.md`: `:265-283` (I-1 to I-17), `:302-308` (A-4, A-5, A-9, A-10). `test-design-architecture.md:319-327` (the A-10 predicate).
- `implementation-readiness-report-2026-08-22.md`: `:491`, `:545`.
- `tests/TESTING-CONVENTIONS.md`: `:87-200` (rules, races, RCSI), `:492-518` (outbox and email), `:519-540` (migrations), `:704-811` (isolation suite).
- `deferred-work.md`: `:20`, `:532`, `:723`, and the D3 entry under "code review of 4-1".
- `4-1-invite-an-email-address-to-a-space-at-a-role.md`: AC8, AC12, AC13, Q5, Q8, Q11, C4, C9, Review Findings, Debug Log.

### ATDD artifacts - red-phase scaffolds already in the tree

**Checklist:** `_bmad-output/test-artifacts/atdd-checklist-4-2-see-and-revoke-pending-invitations.md` (decisions, seed
constraints, the red verification, and the one finding below).

**Scaffolds** (every one skipped `red: AC<n> - ...`, or pending until 4.2; Q3 and Q16 at their recommended options):

| File | Cases | ACs |
|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ListInvitations.cs` | 4 methods, 5 cases | AC1, AC5, AC6 (Q4, Q5, Q8, Q16) |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs` | 10 methods, 12 cases | AC2, AC3, AC4, AC5, AC7, AC10, I-17 (Q2, Q3, Q11, Q12) |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitationRace.cs` | 1 | AC4 race (Q10) |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationRevoke.cs` | 6 gates | AC1, AC2, AC3, AC4, AC5, AC7, AC9, AC11 (Q13, Q16) |
| `tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs` | 10 rows, `PendingUntil: "4.2"` (20 when active; corrected from "11" by the code review) | AC8 |
| `tests/Yello.Tests.Client/InvitationListTests.cs` | 10 methods, 12 cases | AC9 |
| `tests/Yello.Tests.Client/TextSpacingTests.cs` | 1 method, 2 cases | AC9 (ledger `:20`) |

Harness: `InvitationWire` (`ListedInvitation`, `ParseList`, and the temporary `RevokeRoute`, `RevokePathOf`,
`NotPendingProblemType` - swap them for the Task 2 contract members and delete them), `WireDatabase.SetInvitationStateAsync`,
and the list and revoke on `FakeYelloApi.Invitations.cs`. Handler unit tests are not scaffolded: their types do not exist.

**Green today:** none of the new scaffolds (each needs a route or type 4.2 adds). The existing gates AC11 names stay
active and green unchanged.

**Activation order:** (1) `A_revoke_racing_an_acceptance_that_commits_under_it_answers_409_and_leaves_it_accepted` - the
riskiest assumption, that one `ExecuteUpdateAsync` composed with `OfferPredicates.Actionable` is a single guarded
`UPDATE` that waits and re-evaluates under RCSI; (2) the unknown-id `404` case and `ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE`
(Q3); (3) `Only_the_Invitation_store_writes_an_Invitation_state` and the terminal-state `409` theory; (4) the list gates and
wire cases, then the list isolation rows; (5) the remaining revoke cases and isolation rows - clear every
`PendingUntil: "4.2"` before review (GATE-003); (6) the handler gates and the problem-type gate; (7) the browser cases,
with `Yello.Client` built in Debug.

**Finding:** an unmapped `GET` on the Invitations path answers the boundary `404 not-available` today, so every `404`
scaffold is anchored on a served request on the same route. And 4.1's
`A_sent_invitation_clears_the_form_keeps_focus_and_announces_no_address` asserts the address is absent from the page after
an issue, which AC9's re-read would contradict against a real list; the fake lists issued Invitations only when
`ListIssuedInvitations` is set. Narrow that 4.1 assertion to the live regions or keep the default, and record which.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), bmad-dev-story, unattended story cycle, 2026-11-01.

### Debug Log References

**Red-green.** The ATDD run verified every scaffold red for its stated reason: the route or type was missing, and an unmapped `GET` answered the boundary `404`. Each was activated by removing its `Skip`, in the checklist's order. The Slices, Architecture, Isolation and Client groups were each run green after their task. Before Task 6, the one Architecture failure was the expected one: `The_invitation_not_pending_problem_type_is_declared_and_has_client_copy_naming_story_4_2` (no `ClientCopy.resx` entry yet). The race case (activation step 1) was green on the first run against the single guarded `ExecuteUpdateAsync`, and plants 2 and 3 below prove that it can fail.

**Scaffolds corrected:** none. The route patterns, type names, locators and copy all matched what Tasks 2 to 6 built. The ATDD stand-ins `InvitationWire.RevokeRoute`, `RevokePathOf` and `NotPendingProblemType` were swapped for `InvitationRoutes.Invitation`, `InvitationRoutes.InvitationOf` and `ProblemTypes.InvitationNotPending` in every use: Isolation, the Slices revoke and race partials, the Client fake and `InvitationListTests`. They were then deleted from `InvitationWire`. The scaffold headers now say the cases are active.

**The ATDD finding about 4.1's address-absent assertion:** the default was kept. `FakeYelloApi.ListIssuedInvitations` stays off, so 4.1's `A_sent_invitation_clears_the_form_keeps_focus_and_announces_no_address` keeps the page it was written against. Only `After_a_successful_issue_the_list_rereads_and_shows_the_new_row` turns it on.

**A defect my own change caused, caught by an existing gate:** a first `text-transform: uppercase` on `.invitation-list__role` failed `DesignFoundationGateTests.Every_casing_transform_excludes_the_locales_it_is_lossy_in`. The Role span now uses base.css's `type-role-label`, which already excludes the lossy-casing and case-less locales.

**Index check (Task 4):** no migration. Both isolation layers narrow the list to `SpaceId = SESSION_CONTEXT`. `IX_Invitation_SpaceId_NormalizedEmailAddress` already leads on `SpaceId`, so the plan seeks it, does a key lookup and sorts at most 7 days of issues. The revoke seeks `PK_Invitation`.

**SYSDATETIMEOFFSET inside the UPDATE:** proved behaviourally rather than by reading logs. The race case and the expired-`409` arm pass with the composed predicate and fail under plant 2. 4.1's gate (ii), `The_shared_actionable_predicate_reads_the_database_clock_and_never_a_CSharp_clock`, still holds the translation.

**Plants.** Each was applied by a scratch script that kept the original text in memory and restored it in a `finally`. `git checkout`, `git restore` and `git reset` were never used. Afterwards `git status` and `git grep` showed no remnant. Independent plants were batched into one build only when their expected-red tests did not overlap.

| # | Plant | Expected red | Seen |
|---|---|---|---|
| 1 | The list without `OfferPredicates.Actionable` | AC6 | Red: `The_list_shows_only_live_pending_Invitations_...` |
| 2 | The revoke without the actionable guard | The race case; the accepted and expired `409` cases | Red: the race case, all three terminal arms (Accepted, Revoked, Expired), and the replay's new-key `409` |
| 3 | The revoke as load, C# check, `SaveChanges` | The race case (or masked) | Red: the race case, 1/1, and the state-writer gate (anchor gone) |
| 4 | The revoke as `ExecuteDelete` | AC5 retention; the state-writer gate | Red: both |
| 5 | A second `State` writer in another type (`SpaceStore`) | The state-writer gate | Red |
| 6 | The list under `ReadProjectsAndTasks` | The capability gate; the Member and Viewer rows | Red: the routes gate, both Slices `403` arms, and `ISO-RECORD-KIND-LIST-INVITATIONS-MEMBER`/`-VIEWER` on both surfaces |
| 7 | `RequiresCapability` removed from the revoke | `Every_spaceId_endpoint_declares_exactly_one_capability`; the Member and Viewer revoke rows | Red: both gates. The Host also refused to start, so every wire case in that build failed and P7 is not isolated on the wire |
| 8 | The response gains `NormalizedEmailAddress` | The response-shape gate | Red |
| 9 | The revoke handler takes an outbox and enqueues | The AC7 wire case; the no-outbox gate | Gate red. The wire case stayed green: the handler cannot know the invitee's address, so the planted row went to another recipient, and the gate is the catch. **The AC7 wire case is not a guard against an outbox plant; `The_revoke_handler_takes_no_outbox` is** (code review) |
| 10 | The unknown-id branch answers `409` | `ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE`; the unknown-id case | Red: both surfaces, and the Slices case |
| 11 | The revoke filtered on the caller as issuer | AC2 non-issuer; AC3 arms | Not planted. Neither the handler nor the store receives the caller (no `ActiveSpaceContext` parameter), so the plant needs a signature change. `Neither_the_list_nor_the_revoke_reads_the_issuers_Account` and the AC2/AC3 wire cases hold it |
| 12 | The client removes the row before the answer | The not-optimistic case | Red |
| 13 | The announcement includes the address | The announcement case | Red |
| 14 | The purge registration skipped | The switch-purge case (or masked) | **Masked, 0/1**: `SpaceSettings` keys the section on `(SectionKey.Invitations, SpaceId)`, so a switch disposes it and a fresh instance starts empty. This is the 3.4 Labels finding. The registration stays, because it is what clears the list before navigation completes |
| 15 | A revoke with its own `ExpiresAt >` comparison | Gate (i) | Red: `Only_the_shared_actionable_predicate_compares_ExpiresAt`, and 4.1's database-clock gate |

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-11-01).
- **Implemented FR-12 and UX-DR14** with FR-10's retention clause. `GET /api/v1/spaces/{spaceId}/invitations` lists the actionable pending Invitations: id, address, Role code, issuer name captured at issue, and issue date. It is newest first and unpaged, under `IssueAndRevokeInvitations` on both surfaces. `DELETE .../invitations/{invitationId}` revokes with one guarded `ExecuteUpdateAsync` composed with `OfferPredicates.Actionable`. It answers `204`, or `409 invitation-not-pending` (accepted, revoked or expired), or the unrecorded boundary `404` for an id that names nothing here. It sends no email and deletes no row. The client lists the Invitations in the Invitations section with an in-place confirm. It is never optimistic, it purges on leaving the Space, and no announcement carries an address.
- **Port choice (Task 3):** `ListPendingAsync` and `RevokeAsync` were added to `IInvitationStore` rather than a sibling port. S1200 did not fire: the store gained no new coupled types beyond `PendingInvitation` and `InvitationRevocation`.
- **Problem-type gate:** 4.2 has its own sibling gate (`The_invitation_not_pending_problem_type_is_declared_and_has_client_copy_naming_story_4_2`, as the ATDD run wrote it). 4.1's gate was not generalised.
- **The A-10 gate** `Only_the_Invitation_store_writes_an_Invitation_state` is live. The issue's insert of `Pending` (the object initializer's `set_State`) is the other sanctioned write. 4.3 adds its accept.
- **Not locked, and recorded:** a revoke committing between an issue's actionable-pending read and its insert can only make that issue answer `invitation-pending` for an Invitation that was just revoked. The answer is benign and visible, and a retry succeeds.
- **Handler unit tests** (`ListAndRevokeInvitationHandlerTests`, no database) cover the pass-through and the three outcomes. `IssueInvitationHandlerTests.RecordingStore` gained the two new port members, throwing `NotSupportedException`.
- **C1 (UX-DR34 against UX-DR14):** no client gate restricts where an address renders, so no allowed list changed. The address renders only in `InvitationRow`, in Space settings.
- **Decisions (each reversible; cut any line to revisit):**
  - Q1 - (a): `DELETE .../invitations/{invitationId}` answers `204`, the API Token revoke precedent. Cost: the route reads as removal although the row is kept.
  - Q2 - (a): one `409 urn:yello:problem:invitation-not-pending` for accepted, revoked and expired, with no address and no state named. Cost: the client cannot tell which terminal state applies.
  - Q3 - (a) [SECURITY, non-relaxing]: an unknown id or another Space's id gets the byte-identical boundary `404`, unrecorded. Cost: none. Isolation is unchanged.
  - Q4 - (a): expired Invitations are not listed and not revocable (`409`), and the row stays `Pending`. Cost: an issuer is not told that an Invitation lapsed.
  - Q5 - (a): the response is `(Id, EmailAddress, Role, IssuerName, IssuedAt)`. Cost: `ExpiresAt` can be added later as an additive field.
  - Q6 - (a): an absolute short date in `<time datetime>`, not the mockup's relative time. Cost: it differs from the mockup.
  - Q7 - (a): the captured issuer name only, never "Sent by you". Cost: the mockup's wording is not matched.
  - Q8 - (a): newest first and unpaged. The growth is added to D3's ledger entry (owner 4.6, Lee/PM). Cost: an abusive issuer makes the list long.
  - Q9 - (a): confirm in place: "Revoke this invitation? The email already sent cannot be recalled." Cost: one extra click.
  - Q10 - (a): one guarded `ExecuteUpdate` with a rowcount check, no application lock and no `HOLDLOCK`. Cost: the benign issue/revoke interleaving above.
  - Q11 - (a): "can no longer be accepted" is proved through the state, the predicate and re-issue. The acceptance-route proof is ledgered to 4.3. Cost: none until 4.3.
  - Q12 - (a): demotion and removal run through `WireDatabase`. Account deletion is ledgered to 5.4, and the product-route re-runs to 4.4 and 4.5. Cost: two arms are seeded rather than driven through the product.
  - Q13 - (a): the state-writer gate is built now. Cost: 4.3 must amend it.
  - Q14 - (a): `InvitationCapabilities.PermitsIssueInvitations` is kept for the whole section. Cost: the name says "issue" for a section that also revokes.
  - Q15 - (a): the recommended copy, with no count beside the heading. Cost: none.
  - Q16 - (a) [SECURITY, non-relaxing]: the list is under `IssueAndRevokeInvitations`, and a Member or Viewer gets a recorded `403`. Cost: none. Privacy is unchanged.
- **Ledger (`deferred-work.md`).** Lines `:20`, `:532`, `:723` and D3 (`:731`) were UPDATED, each on one line. A new section, "Deferred from: dev of 4-2", holds three entries: the acceptance route's uniform answer for a revoked Invitation (4.3, low); the issuer-deleted arm, where 5.4 must tombstone because the `Restrict` foreign key blocks deletion (5.4, medium); and the AC3 arms re-run through the product's own routes (4.4 and 4.5, low).
- **Verification:** `docker info` gave 29.5.3. `dotnet build Yello.slnx` finished with 0 warnings and 0 errors, then `dotnet build-server shutdown`. The full suite (`dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`) was the last thing run: **1,975 total, 1,965 passed, 0 failed, 10 skipped**. The baseline was 1,945 total and 42 skipped. All 32 of 4.2's scaffold skips are now active. The 10 remaining skips are the 3 migration-bundle cases plus the explicit tiers and the recorded 3.4 heap spike. No failure needed a rerun.

### File List

New:

- `Yello.Contracts/Invitations/InvitationResponse.cs`
- `Yello.Application/Invitations/ListInvitations/ListInvitationsHandler.cs`
- `Yello.Application/Invitations/RevokeInvitation/RevokeInvitationHandler.cs`
- `Yello.Client/Components/InvitationRow.razor`
- `tests/Yello.Tests.Slices/Invitations/RevokeInvitation/ListAndRevokeInvitationHandlerTests.cs`

Modified:

- `Yello.Contracts/Invitations/InvitationRoutes.cs`
- `Yello.Contracts/ProblemTypes.cs`
- `Yello.Domain/Invitations/Invitation.cs`
- `Yello.Domain/Invitations/InvitationState.cs`
- `Yello.Application/Invitations/IInvitationStore.cs`
- `Yello.Infrastructure/Persistence/InvitationStore.cs`
- `Yello.Infrastructure/Persistence/OfferPredicates.cs`
- `Yello.Host/Endpoints/InvitationEndpoints.cs`
- `Yello.Host/Program.cs`
- `Yello.Client/Components/InvitationSection.razor`
- `Yello.Client/Pages/SpaceSettings.razor`
- `Yello.Client/Localisation/ClientCopy.resx`
- `Yello.Client/Refusals/RefusalCopy.cs`
- `Yello.Client/wwwroot/css/components.css`
- `tests/Yello.Tests.Shared/InvitationWire.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationRevoke.cs`
- `tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs`
- `tests/Yello.Tests.Client/FakeYelloApi.Invitations.cs`
- `tests/Yello.Tests.Client/InvitationListTests.cs`
- `tests/Yello.Tests.Client/TextSpacingTests.cs`
- `tests/Yello.Tests.Slices/Invitations/IssueInvitation/IssueInvitationHandlerTests.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ListInvitations.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitationRace.cs`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/4-2-see-and-revoke-pending-invitations.md`

## Change Log

- 2026-11-01 - Story 4.2 implemented (dev-story). The pending Invitation list and its revoke work end to end: contracts, slices, the guarded store update, endpoints, and the client list with an in-place confirm. All 32 ATDD scaffolds and all 10 isolation rows are active (the code review corrected "11"). 14 of 15 plants were run: 12 seen red, 1 red only at its gate (P9), and 1 masked (P14); P11 was not plantable. The ledger is updated.
- 2026-11-01 - Verification: build 0/0; full suite 1,975 total, 1,965 passed, 0 failed, 10 skipped. Status set to review.
- 2026-11-01 - Code review (bmad-code-review): 34 raw findings, 26 after deduplication; 13 patched, 2 deferred (4.4, 4.3), 11 dismissed, none decision-needed. Client: only the latest list read is used and ids revoked here never return; a failed re-read keeps the rows instead of leaving the Space; focus returns to the row, the next row or the heading after every non-204 answer; a revoke `403` shows in the section; the 409 announcement is dropped after a purge. Tests: two Slices seeds moved to the issue route, the focus, 409-focus and settings-link assertions added, the fake answers `409` for a withdrawn id. Build 0/0; full suite 1,975 total, 1,964 passed, 1 failed, 10 skipped; the one failure, the timing case `A_Token_used_in_another_Space_takes_as_long_to_refuse_as_a_non_member_Session`, passed when rerun alone. Status set to done.
