# Story 4.5: Remove a Membership, or leave a Space

Status: ready-for-dev

Epic: 4 — Bring people into one piece of your work (the fifth story of the epic)
Story key: `4-5-remove-a-membership-or-leave-a-space`

**Requirements owned.** From the Story Coverage Index (`epics.md:373`): **FR-14**, AR-16, UX-DR36. Test-design rows **V-2** and **V-3** (`test-design-qa.md:281-282`).

- **FR-14** (`prd.md:303-313`; `acceptance-criteria.md:139-146` CAP-14; `SPEC.md:86-88`). Every consequence binds:
  - An Owner or Admin can remove a Membership. Any Account can remove its own.
  - An Admin can remove Members and Viewers only. An Admin cannot remove the Owner or another Admin.
  - The Owner's Membership cannot be removed by anyone, the Owner included, while it holds ownership. The Owner's exits are an Ownership Offer accepted (FR-8/FR-42, Epic 5) or deleting the Space (FR-7, done in 3.3). "A pending offer is not an exit."
  - Removal revokes access immediately. The **request-path** half is met here. "Reaches active Sessions and open editors" is the live-session half, which completes in 7.7 and 7.8 (`epics.md:402`; `sprint-status.yaml:56`: FR-14 is staged).
  - Removal invalidates every API Token that Account holds for the Space (FR-36, AD-6).
  - Tasks the removed Account was Assignee of become unassigned, and the Tasks survive. **There is no Assignee yet** (4.6 builds it). See C1 and Q9.
  - The removed Account keeps its Account, its own Spaces and every other Membership.
- **The matrix** (`role-capability-matrix.md:22`): "Remove a Membership": Owner ✔, Admin "Members and Viewers only", Member —, Viewer —. The code transcribes it as `Capability.RemoveMembership` = Allowed / **Qualified** / Denied / Denied (`Yello.Domain/Memberships/RoleCapabilityMatrix.cs:47`). "Any Account can remove its own" is **not a matrix row**. It is decided by identity, not by Role. See C2 and Q1.
- **Boundaries the matrix does not express** (`role-capability-matrix.md:37-40`): owner removability, and admin symmetry ("Admins cannot modify each other", PRD §12 assumption #4, still unconfirmed).
- **AR-16 / AD-9** (`epics.md:204`; `ARCHITECTURE-SPINE.md:130-135`): a removal publishes `MembershipChanged(AccountId, SpaceId)` once, for the removed Account, after commit. The seam exists since 4.4 (`IMembershipChanges`). Nothing subscribes until 7.3/7.7.
- **AD-6** (`ARCHITECTURE-SPINE.md:112-116`): "Membership removal ... invalidate[s] the Token in the same transaction." The cascade `FK_ApiToken_Membership_BoundSpaceId_AccountId ON DELETE CASCADE` does this, **only if the Membership row is deleted** (ledger `:380`).
- **UX-DR36** (`epics.md:280`; `EXPERIENCE.md:220,267`): the Membership rung of the destructive-confirm ladder. It sits between Task and Project. It names the person and states whether they currently have a live session. It replaces the invoking control in place and never stacks.
- **UX-DR14 / `EXPERIENCE.md:213`**: "The Owner's row carries no remove control for anyone, including the Owner (FR-14)." **UX-DR27**: an Admin's missing controls are absent, never disabled. Mockup: `mockups/space-settings.html:121-149` (the `×` remove control and the "EDITING NOW" tag on a row, and note `:149` on the Owner's row).
- **NFR-2 / V-2**: removal governs the removed Account's **very next request**, on both surfaces, with no tolerance.
- **AD-3 / FR-15** (`EXPERIENCE.md:509`): a removed Account gets the boundary `404`, which is indistinguishable from a resource that does not exist. The copy never says "removed".

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Severity | Covered by |
|---|---|---|---|
| `:329` | The Qualified cells' target rule. The Role-change half was done by 4.4. **The removal half is 4.5's.** | medium | AC1, AC9; Q2 |
| `:380` | Membership removal invalidates Tokens only if it deletes the row. Add V-3's case for the event. | medium | AC4; Q3 |
| `:390` | The endpoint-driven `REV-` twin of `REV-MEMBERSHIP-REMOVED` ("4.5 and 5.4 still owe theirs") | medium | AC4, AC7 |
| `:398` | A Membership removed between resolution and the Token insert makes issuance a `500` | low | AC10; Q12 |
| `:711` | 3.3's re-resolution answers "still a Member?", and log event 1040 says "was deleted" for a removal under a request | low | AC10; Q12 |
| `:758` | Re-run 4.2 AC3's **removal** arm through the product route | low | AC5 |
| `:783` | The removal takes `Yello/Memberships/{spaceId}` and uses the acting-authority seam | low | AC1, AC11 |
| `:789` | A row action answered `404` re-reads page 1 only, so appended pages vanish | low | AC8; Q14 |
| `:790` | An actor removed while its change waits on the lock gets a recorded `403`, not the boundary `404` | low | AC10, AC11; Q12 |
| `:791` | A second row's action pressed while another is in flight is dropped with no feedback | low | AC8; Q15 |

**Depends on** (all done):

- 1.5 and 1.6: the pipeline, `RoleCapabilityMatrix`, `CapabilityDecision.Qualified`, `RequiresCapability`, refusal recording, idempotency, `NotAvailableFloor`.
- 1.6: `SpacePresence` (NFR-8's live presence, `Yello.Domain/Spaces/SpacePresence.cs`), the source of "live session" (Q5).
- 1.8: `ApiToken`'s composite foreign key to `Membership(SpaceId, AccountId)` with `ON DELETE CASCADE`.
- 1.9: the isolation and revocation suites (`REV-MEMBERSHIP-REMOVED` and `REV-ROLLED-BACK-REMOVAL` by raw SQL).
- 3.3: `LostSpace` (a reference conflict under a request re-resolves and answers the boundary `404`), `SpaceDeleteSection.Leave` (purge, then route home), `SpaceDirectory.Removed`.
- 4.2: the guarded-statement-then-disambiguation shape, and `An_Invitation_whose_issuer_was_removed_stays_listed_and_revocable` (raw SQL today).
- 4.3: acceptance (seed Members through the product), and the Membership bound on acceptance.
- 4.4: the Members section, `MembershipStore`, `ActingAuthority`, `RoleChangeAuthority`, `ApplicationLocks.TakeMembershipsAsync`, `IMembershipChanges`, `QualifiedRefusal`, `MembershipCapabilities`, the address and attribution gates.

**Explicitly not in this story:**

- **The live-session half of FR-14** (7.3, 7.7, 7.8): lease invalidation, the open editor made inert, "Access ended.", the ≤ 1 s push (V-4, V-5, V-9). 4.5 publishes `MembershipChanged`; nothing subscribes yet.
- **The pushed narration to the removed person's open tab** (4.7 and 7.7). Their open tab learns on its next request, through the boundary refusal surface.
- **The Assignee and its unassignment** (4.6). See C1 and Q9.
- **Ownership Offers** (5.1-5.3), including "an offer lapses if the named recipient's Membership is removed" (`prd.md:222`). No offer table exists yet. See Q10.
- **Account deletion** (5.4) and its own `REV-` twin.
- **Presence in the Task UI** (7.5). 4.5 only *reads* `SpacePresence` to state the live session in the confirm.
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits.
- **No new NuGet package and no new project without Lee** (CLAUDE.md).

## Story

As an Owner or Admin ending a working relationship, or as anyone who wants out,
I want access to end immediately and completely,
so that when I remove someone I can believe it took effect.

## Acceptance Criteria

AC1 to AC7 are reproduced **verbatim** from `epics.md:1802-1834` and numbered by position. AC8 to AC11 come from the spines, the ledger and a survey of the code; each cites its source. The indented notes are this story's implementation guidance. A note that depends on an open question names it. Questions marked **[LEE]** touch authority, isolation or a security seam: the orchestrator may route them to Lee before development; otherwise the dev phase takes the recommendation and records it as a reversible decision.

**AC1 — an Admin removes Members and Viewers only** (FR-14, UX-DR27; ledger `:329`, `:783`)
**Given** a Member or a Viewer
**When** an Admin removes them
**Then** the removal succeeds
**And** an Admin cannot remove the Owner or another Admin, those controls being absent rather than refused

> - **Route** (Q4): `DELETE /api/v1/spaces/{spaceId:guid}/memberships/{membershipId:guid}` (the existing `MembershipRoutes.Membership` pattern, a new verb). It requires `.RequiresCapability(Capability.RemoveMembership)`, which is Owner Allowed, Admin Qualified, Member and Viewer Denied. `Idempotency-Key` is handled by the pipeline. On success the answer is `204 No Content`. Both surfaces (AD-4).
> - **The target rule, the 4.4 way** (Q2 **[LEE]**; ledger `:329`, `:783`). Recommended (a):
>   - Domain declares `RemovalAuthority(IReadOnlySet<Role> Removable, IReadOnlySet<Role> HeldBy)` beside `RoleChangeAuthority`, and `RoleCapabilityMatrix.RemovalAuthorityOf(Role actor)`:
>     - Owner: `Removable = {Admin, Member, Viewer}`, `HeldBy = {Owner}`;
>     - Admin: `Removable = {Member, Viewer}`, `HeldBy = {Owner, Admin}`;
>     - any other Role: `RemovalAuthority.None`.
>     - **Owner is in no `Removable` set for any actor.**
>   - `ActingAuthority` gains a `Removals` property, set by the pipeline in `QualifiedAuthority.ServeWithAuthorityAsync` beside `RoleChanges`, from the Role it already read. **No slice reads a Role.** Both Role-read gates stay unwidened.
>   - The store composes the sets into **one guarded `DELETE`** under `ApplicationLocks.TakeMembershipsAsync` (Q11):
>
>     ```
>     DELETE FROM dbo.[Membership]
>     WHERE Id = @target AND SpaceId = @space
>       AND Role IN (@removable)                    -- Owner never in it
>       AND EXISTS (SELECT 1 FROM dbo.[Membership] AS actor WITH (READCOMMITTEDLOCK)
>                   WHERE actor.SpaceId = @space AND actor.AccountId = @actor
>                     AND actor.Role IN (@heldBy))  -- the in-flight re-check, as 4.4's
>     ```
>
>   - The self-target rule (Q2 sub-question): recommended, the actor's own non-Owner row is always removable on this route as well, so an Admin can remove itself here as on the leave route (`... AND (Role IN (@removable) OR AccountId = @actor) AND Role <> 'Owner'`). Record the choice.
>   - On rowcount 0, a disambiguation read decides (`RemovalAuthority.Judge`, the `RoleChangeAuthority.Judge` shape):
>     - target absent from this Space: the in-Space not-found, `BoundaryProblems.NotAvailable()`, unrecorded and floored;
>     - the actor's own Membership gone: the boundary `404`, **recorded** `CrossSpace` (Q12; ledger `:790`);
>     - an Owner target: `409 ownership-held` for the Owner acting on its own row (Q6), and `403` beyond authority for anyone else;
>     - otherwise beyond authority: `QualifiedRefusal(Capability.RemoveMembership)`, which the pipeline records as `InsufficientRole` and answers `403` (AD-20).
> - **Gates:**
>   - Extend `The_target_rules_agree_with_the_matrix_transcription` with the removal sets (the second transcription in `RefusalBoundaryGateTests.cs:90-149`).
>   - `A_Qualified_cell_reaches_no_endpoint_without_its_target_rule` now finds two Qualified capabilities. The removal slice's type name must contain `RemoveMembership` (the gate matches the capability's whole name since the 4.4 review).
>   - `Only_the_pipeline_sets_the_acting_authority` stays green, with the new setter call inside the pipeline only.
>   - Update `The_Membership_routes_require_ChangeMembershipRole_and_serve_both_surfaces` to the new route set (Q4): `GET`, `PATCH` and `DELETE` on the item, and the leave route. It becomes a per-route capability map.
> - **The matrix of outcomes (Slices, each its own case; S-2 after each):**
>
>   | Actor | Target | Answer |
>   |---|---|---|
>   | Owner | an Admin, a Member, a Viewer | `204`, row gone |
>   | Owner | own row | `409 ownership-held`, unchanged |
>   | Admin | a Member, a Viewer | `204` |
>   | Admin | another Admin; the Owner | `403`, recorded `InsufficientRole`, unchanged |
>   | Admin | own row | `204` if Q2's self-target rule is (a); otherwise `403`. Record which |
>   | Member, Viewer | anyone, own row included | `403` from the pipeline, recorded (they leave through the leave route, AC3) |
>   | anyone | an id from another Space, or random | `404 not-available`, byte-identical to the boundary, unrecorded, floored |
>   | non-member | anything | boundary `404`, recorded `CrossSpace` |
>
> - **Client** (AC8): a "Remove" control on a row where `MembershipCapabilities.Removable(viewerRole, targetRole, isYou)` holds. For an Admin viewer that is Member and Viewer rows. For the Owner it is every row but its own. An agreement gate checks it against `RemovalAuthorityOf` for every (viewer, target) pair (the `The_clients_Role_offer_agrees_with_the_target_rules_for_every_pair` precedent). Elsewhere the control is **absent from the DOM** (UX-DR27).

**AC2 — the Owner's Membership cannot be removed while it holds ownership** (FR-14, `role-capability-matrix.md:39`)
**Given** the Owner's Membership
**When** removal is attempted by anyone, including the Owner
**Then** it is refused while it holds ownership
**And** the Owner's row carries no remove control for anyone, so the exits are offering ownership and having it accepted, or deleting the Space

> - **The database side:** Owner is in no `Removable` set, and the leave statement carries `Role <> 'Owner'` (AC3). So no removal statement can delete an Owner row. Record honestly, as 4.4 did for AD-5, that this rests on the guarded statements plus the S-2 invariant, not on the schema. A `BLOCK BEFORE DELETE` predicate or a trigger forbidding an Owner row's delete is **not** recommended, because Space deletion (3.3) cascades the Owner's row and must keep working (Q7).
> - **The answers:** the Owner on its own row through either route gets `409 ownership-held` (Q6), with copy "The Owner leaves by having an Ownership Offer accepted, or by deleting the Space." (`EXPERIENCE.md:271`: "Do not imply a third"). An Admin targeting the Owner gets `403`.
> - **Client:** the Owner's row has no Remove control for any viewer. The Owner sees no Leave control anywhere (AC3, Q8). Absent, not disabled.
> - **Proof:** the two Owner cases in AC1's table, the Owner's leave case in AC3, and S-2 (`WireDatabase.SpacesNotHoldingExactlyOneOwnerAsync`) after each. **Plant:** Owner added to the Owner's `Removable` set. The own-row case answers `204` and S-2 goes red; the transcription gate goes red.

**AC3 — any Account may leave, unless it holds ownership** (FR-14, UJ-8 step 6)
**Given** any Account
**When** it removes its own Membership
**Then** it succeeds unless it holds ownership, and it retains its Account, its own Spaces and every other Membership

> - **The leave route** (Q1 **[LEE]**, Q4). Recommended (a): `DELETE /api/v1/spaces/{spaceId:guid}/membership`, the caller's own Membership, as `MembershipRoutes.Own` with `OwnOf(spaceId)`.
>   - It must declare exactly one capability (`Every_spaceId_endpoint_declares_exactly_one_capability`), and `RemoveMembership` denies Members and Viewers. Recommended: `.RequiresCapability(Capability.ReadProjectsAndTasks)`, the capability every Role holds, because leaving is decided by **identity** (the caller's own row), not by Role. This mirrors how AD-26 decides an Offer's acceptance outside the matrix.
>   - Remarks on the route and a new gate, `The_leave_route_is_the_one_write_under_ReadProjectsAndTasks`, so no other write slips under the read capability.
>   - **No new matrix row**: `SpaceSwitcherGateTests` holds a Viewer to exactly `ReadProjectsAndTasks` and `IssueApiTokenForOneself`, and the transcription gate holds the spec's rows.
> - **The statement:** under `TakeMembershipsAsync`: `DELETE FROM dbo.[Membership] WHERE SpaceId = @space AND AccountId = @actor AND Role <> 'Owner'`. On rowcount 0, read the caller's row:
>   - the Owner: `409 ownership-held`;
>   - gone (a concurrent removal or leave committed first): the boundary `404`, recorded `CrossSpace` (Q12).
> - **Both surfaces** (AD-4 parity, Q1). A Token may leave its own Space. The cascade then deletes that Token with the Membership, inside the request's own transaction. The `204` is still answered, and the Token's next request is `401`.
> - **Idempotency** (Q13): a replay after a committed leave finds no Membership, so it is the boundary `404`. That is the same recorded variance as 3.3's Space deletion (AD-18). The record may still be written: its foreign key is to `Space`, not `Membership`, and it is purged by age. **Do not** mark the route `DestroysItsSpace` (`Only_the_deletion_route_carries_the_marker_that_it_destroys_its_own_Space`). Record it.
> - **Retained** (Slices): after a leave, the Account still signs in. `GET /api/v1/spaces` (the switcher) lists its Personal Space and every other Space it holds, and not the one it left. A Project it created in the Space it left still exists for the Owner (the Tasks survive, AC4).
> - **Client** (Q8 **[LEE/UX]**): where the Leave control lives, its confirm, and what happens after. See AC8.

**AC4 — the removal's effects, in one transaction** (FR-14, FR-36, AR-16/AD-9, AD-6; V-2, V-3; ledger `:380`, `:390`)
**Given** a removal
**When** it commits
**Then** access is revoked immediately, `MembershipChanged` publishes at the transaction boundary, every API Token that Account holds for this Space is invalidated in the same transaction, and Tasks it was Assignee of become unassigned rather than deleted

> - **Access revoked immediately (V-2):** the resolver reads the Membership per request (`MembershipActiveSpaceResolver`), so the removed Account's next Session request is the boundary `404`. **No cache is introduced** (`No_production_code_introduces_a_cache` stays green).
> - **Tokens (V-3, ledger `:380`):** delete the `Membership` row (never a soft delete). The cascade takes every `ApiToken` with `(BoundSpaceId, AccountId)` = the removed pair, in the same statement. Proof: before the removal the target's Token reads `200`, and on the first request after it reads `401` (the no-credential bytes). The target's Token for **another** Space still reads `200`.
> - **Presence** (Q5): delete the removed Account's `SpacePresence` row for the Space in the same transaction. Otherwise it keeps counting toward NFR-8's 50-presence bound until it expires, and a rejoin would show a live session that is not live. Raw SQL under the Space's key: `SpacePresence` carries a FILTER predicate, so the delete is narrowed to the Space.
> - **`MembershipChanged`:** `membershipChanges.Record(removedAccountId, spaceId)` once, after the guarded `DELETE` returned 1. It publishes after commit, and never on a refusal, a not-found, a rollback or a replay. One log line `MembershipChangedPublished` per removal and leave. **Plant:** record before the `DELETE`, or on every attempt. The refusal cases go red.
> - **Unassignment** (Q9): no Task carries an Assignee before 4.6, so the clause is **met vacuously and recorded**. What 4.5 proves now is "rather than deleted": Projects and Tasks the removed Member created survive the removal, with counts unchanged. Add a ledger entry for 4.6 (C1): the Assignee foreign key `(SpaceId, AssigneeMembershipId)` cannot be `ON DELETE SET NULL`, because SQL Server would null the non-nullable `SpaceId` too. So 4.6 must unassign explicitly inside this removal's transaction, before the `DELETE`, or the delete fails `547`. 4.6 adds the case "an assigned Member is removed: Task unassigned, Task survives".
> - **Revocation suite (ledger `:390`):** add two endpoint-driven rows to `RevocationCases.All` beside the raw-SQL `REV-MEMBERSHIP-REMOVED`, which stays as the schema proof:
>   - `REV-MEMBERSHIP-REMOVED-BY-ADMIN`: the actor joins the Owner's Space as a Member through an accepted Invitation (4.3). An Admin of that Space, also joined through the product, removes it through `DELETE .../memberships/{id}`. Next request: Session → `BoundaryNotFound`, Token → `NoCredential`. Trait `Assumption = "PRD-12-4"` is not needed (Admin removes Member).
>   - `REV-LEFT-THE-SPACE`: the actor, a Member, leaves through `DELETE .../membership`. Same expectations.
>   - `REV-ROLLED-BACK-REMOVAL` stays the negative anchor.

**AC5 — Invitations the removed Membership issued are not withdrawn** (FR-12, FR-14; ledger `:758`)
**Given** a removal
**When** Invitations that Membership issued are inspected
**Then** they are **not** deleted — removing a Membership does not withdraw offers it legitimately made

> - **By construction:** `Invitation.IssuedByAccountId` references `Account` (`Restrict`), not `Membership` (`InvitationConfiguration.cs:128-131`). The removal touches no Invitation row. Say so in the store's remarks, and add no Invitation statement.
> - **Ledger `:758`, the removal arm:** in `ActiveSpaceEndpointTests.RevokeInvitation.cs`, convert `An_Invitation_whose_issuer_was_removed_stays_listed_and_revocable` (`:258`) so that the issuer joins as an Admin through an accepted Invitation, issues one, and is then **removed through `DELETE .../memberships/{id}` by the Owner**, not by `wire.RemoveMembershipAsync` (`:286`). Keep the raw-SQL original until the twin is green, then delete it and give the twin its name (4.4 AC8's precedent). Update the file remark (`:29`).
> - Also assert: the removed issuer's pending Invitation can still be **accepted** by its invitee (4.3's accept, the "remains valid" half of FR-12).

**AC6 — the removal confirm** (UX-DR36, `EXPERIENCE.md:220,267`; mockup `space-settings.html:124,148`)
**Given** the removal confirm
**When** it renders
**Then** it names the person and states whether they currently hold a live session, so the remover knows they are interrupting someone mid-work
**And** it sits on the blast-radius ladder between Task and Project, because it ends someone's access to everything in the Space immediately

> - **"Live session"** (Q5): an unexpired `SpacePresence` row for (this Space, the target's Account), `ExpiresAt > SYSUTCDATETIME()`. It is written at every resolved Space-scoped request, with a 15-minute window (`SpacePresenceRegister.Window`). It is the only server-side signal that exists, because a Session has no server identity (`SpacePresence.cs:9-12`).
> - **When it is read** (Q5): recommended (a), when the confirm opens. `GET /api/v1/spaces/{spaceId:guid}/memberships/{membershipId:guid}` under `ChangeMembershipRole` (the list's capability; Q4) answers `MembershipResponse` with a new `bool InSpaceNow`. The list's rows carry it too, so the mockup's tag can render. The confirm states the fresh value, never the list's.
>   - The contract-shape gate (`The_Membership_contracts_carry_exactly_their_named_members`) gains `InSpaceNow`.
>   - The address gates are unchanged: the single-row answer is the list's own type.
>   - `MembershipRoleChangeResponse` stays address-free. Do not add `InSpaceNow` there.
> - **Disclosure:** it is presence in **this** Space only, read under this Space's key. It is shown only to an Owner or Admin who could already see the row. Nothing about any other Space is read (FR-32's rule, `EXPERIENCE.md:209`).
> - **The confirm** (`EXPERIENCE.md:220`: "Replaces its invoking panel's content in place rather than stacking"):
>   - It replaces the row's controls in place. It is never a dialog and never a stacked modal.
>   - It names the row's **disambiguated label** (AC3 of 4.4, `MembershipLabels`), never the address.
>   - It states the consequence and the live state (copy, Q16).
>   - "Remove" (`button-danger`) and "Cancel" (`button-secondary`). **Neither is pre-focused as the default action.** Focus goes to the confirm's text first (the `InvitationRow` confirm precedent). No typed confirmation: that rung is the Space's.
>   - Esc or Cancel restores the Remove control with focus on it.
> - **The rung:** Task names it and says "This cannot be undone."; Membership names the person and the live state; Project names it and its Task count. The Membership confirm is a two-step in-place confirm like Task's, with more stated consequence. Record the rung in `components.css`/the component remarks, without story numbers (CLAUDE.md comment rule).
> - **Proof (Client):** the confirm names the label, states each live state (the fake answers `InSpaceNow` true, then false), contains no `@`, has no pre-checked or pre-focused destructive control, and Esc restores focus. **Proof (Slices):** `InSpaceNow` is true for an Account that made a Space-scoped request in the window, false for one whose presence expired (seed `ExpiresAt` in the past), and false for an Account present only in **another** Space.

**AC7 — a removed Account gets the boundary 404, by any route** (FR-15, AD-3, NFR-2; V-2)
**Given** a removed Account
**When** it requests anything in that Space by any route
**Then** it receives the boundary 404, indistinguishable from a resource that does not exist
**And** the copy does not distinguish "removed" from "never existed", the usability cost being paid with deliberately ambiguous copy rather than with a disclosure

> - **Server:** nothing new. The resolver finds no Membership, so the answer is the boundary `404`, recorded `CrossSpace`. Prove it on a sample of the capability classes, each on both surfaces: a read (the Board or the Project list), a write (Project create), the Members list, and the leave route itself. Each must be byte-identical to an absent Space (`SameAsAnchorAndStranger` style) and recorded.
> - **Client:** the removed person's open tab, on its next request, renders the one refusal surface ("Not available." plus the route back, `EXPERIENCE.md:509`). There is no new copy. `RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal` stays green. The switcher no longer lists the Space after a refresh. A browser case: a signed-in Member on Space home, removed through the fake's answer, sees the same refusal fingerprint (`RefusalFingerprint`) as an unknown Space.

**AC8 — the client: Remove, Leave, and the owned client ledger items** (UX-DR14, UX-DR27, UX-DR36, UX-DR39; ledger `:789`, `:791`)

> - **Remove** (in `MembershipRow`):
>   - The control is "Remove" (mockup `×` with an accessible name "Remove {label}"; Q16), placed after the Role control.
>   - The confirm is AC6's.
>   - Sending uses one `Idempotency-Key` per deliberate "Remove", kept for an identical resend after no answer or `409 idempotency-key-in-flight` (the 4.4 review fix). It is never optimistic.
>   - On `204`: drop the row from the shown rows **without re-reading or recycling the others** (AD-29), decrement the shown total, announce "{label} was removed." (no address), and move focus to the next row's first control, else the previous row's, else the section heading (UX-DR39; never `body`).
>   - On `403`: re-read the Role (`OnRoleDoubted`, 4.4's `SpaceArrival.Reread`) and follow the page's narrated exit if the section is no longer permitted. Otherwise show capability-shaped copy: "Admins can remove Members and Viewers only."
>   - On `409 ownership-held`: show the AC2 copy. On `404`: Q14's re-read. With no answer or a `5xx`: "Not sent." with the key kept.
> - **Ledger `:789`** (Q14): a row action answered `404` re-reads the first page **only to learn whether the Space still answers**. If it answers, drop that one row from the rows already shown, keep every appended page, take `Total` from the fresh read, and announce "That Membership no longer exists.". If it `404`s, raise the Space refusal surface. This applies to both the Role change (`GoneAsync`) and the removal. One browser case: two pages shown, a row on page 2 answers `404`, and page 2's other rows are still present and focus is not on `body`.
> - **Ledger `:791`** (Q15): row actions are **per row**. Replace the section-wide `_changing` gate with per-row in-flight state, so a second row's Change or Remove proceeds independently. The server serialises them on the Memberships lock. One browser case: two rows' actions in flight at once, both answered, both rows updated. No silent drop.
> - **Leave** (Q8 **[LEE/UX]**). Recommended (a): a "Leave this Space" control at the foot of **Space home**, rendered for Admin, Member and Viewer and **absent for the Owner** (UX-DR27).
>   - Its confirm is in place: "Leave this Space? You lose access to everything in it at once. To come back, someone must invite you again." Then "Leave" (`button-danger`) and "Cancel", with neither as the default.
>   - On `204`, or on a `404` after a resend with the same key (Q13): purge and route home exactly as `SpaceDeleteSection.Leave` does: `SpaceDirectory.Removed(SpaceId)`, `ActiveSpace.Navigating(null)`, then `Navigation.GoTo(ActiveSpace, ClientRoutes.Home, replace: true)`. Narrate "You left the Space." with no Space name (the 3.3 "Space deleted." precedent, UX-DR38). Extract the shared purge-and-route helper rather than copying it.
>   - On `409 ownership-held`, which is unreachable from the UI, show the AC2 copy.
>   - Record the deliberate second action on an empty Space's home, against UX-DR35's "exactly one action" (ledger `:542`, resolved by 3.1 by setting the settings link aside). Widen `SpaceHomeProjectsTests.An_empty_Space_states_no_Projects_yet_and_offers_one_action_to_create_one` to set aside the Leave control as well, so any other control still fails it.
> - **Gates and rules** (4.4's list, unchanged):
>   - No `disabled` attribute on a Role-reason control (`No_control_in_the_Membership_section_is_disabled`, extended to Remove and Leave).
>   - No storage (`ClientStorageBanTests`), no mutable static (`ClientStaticStateGateTests`).
>   - Every `NavigateTo` through `SpaceNavigation.GoTo`.
>   - Copy in `ClientCopy.resx` with a `<comment>`. No Razor `else` around user-visible text.
>   - CSS in `components.css` only: logical properties, no px or colour literals.
>   - 320×256 reflow and `TextSpacingTests` for the confirm and the Leave control.
> - **Client tests** (`Yello.Tests.Client`; extend `FakeYelloApi.Memberships.cs` using `MembershipRoutes`/`ProblemTypes` constants only):
>   - The Owner sees Remove on every row but its own.
>   - An Admin sees Remove on Member and Viewer rows only, and none on Admin or Owner rows (absent).
>   - The confirm names, states the live state, and is sent once with a key.
>   - `204` drops the row and keeps focus off `body`.
>   - The announcement holds no `@`.
>   - Leave is absent for the Owner and present for the other three Roles. A leave purges and routes home before the next Space-scoped render (the 3.4 `SpaceSwitchMemoryTests` style).
>   - The `:789` and `:791` cases.
>   - The client suite publishes `Yello.Client` itself (commit `6145bf7`). Do not build the Debug client separately.

**AC9 — isolation rows on both surfaces, and the route gates** (AD-3, AD-4, I-1, I-12, I-13; `TESTING-CONVENTIONS.md:756-811`)

> The new routes fail `Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` until they have rows. Extend `tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs` with 4.4's row kinds:
>
> - `ISO-REMOVE-MEMBERSHIP-FOREIGN-SPACE`, `ISO-LEAVE-FOREIGN-SPACE`, `ISO-READ-MEMBERSHIP-FOREIGN-SPACE` (if Q5 (a)): boundary `404`, byte-identical, recorded `CrossSpace`.
> - `ISO-RECORD-KIND-REMOVE-MEMBERSHIP-MEMBER` and `-VIEWER`: `403`, recorded `InsufficientRole`. Seed the lesser Role through acceptance.
> - `ISO-REMOVE-MEMBERSHIP-ADMIN-BEYOND-AUTHORITY`: an Admin removing another Admin gets `403`, recorded. This is the Qualified seam's row, and it carries the `PRD-12-4` assumption.
> - `ISO-REMOVE-MEMBERSHIP-FOREIGN-ID-IN-OWN-SPACE` (and the read's twin): another Space's `MembershipId` used in one's own Space gets the in-Space not-found, byte-identical to the boundary, unrecorded.
> - `ISO-TOKEN-OWN-OTHER-SPACE-REMOVE-MEMBERSHIP`, `-LEAVE` (and `-READ-MEMBERSHIP`): the 3.4 `TokenOwnOtherSpace` gate requires one per route a Token reaches.
> - The actor's secrets hold every seeded address and display name (`HoldsSecret`), so the I-17 log check fails on an address in the Host log.
> - Rows created `PendingUntil: "4.5"` by ATDD must all be live before review (GATE-003).
> - Timing: the in-Space not-found is the boundary body through `NotAvailableFloor`, which `ISO-TIMING-IN-SPACE` proves. Add no `IsolationTimingTests` row unless the not-found does work the boundary does not. Record it.

**AC10 — removals under a request: the boundary answer and the log** (ledger `:398`, `:711`, `:790`)

> - **Ledger `:398`, Token issuance against a concurrent removal:** since 3.3, a `547` under a Space-scoped request is caught by `LostSpace.ServeOrRefuseAsync`, re-resolved, and answered as the boundary `404`, recorded. So the `500` the entry describes is likely already gone. Prove it with a deterministic race. The Token insert takes no Memberships lock, so the Memberships lock cannot hold it. Instead, delete the issuer's `Membership` row in an uncommitted test transaction (which holds the row's lock), send the Token issue, wait until it blocks on `LCK_M_%` (`WaitUntilBlockedOnALockAsync`), commit the delete, and assert `404 not-available`, recorded `CrossSpace`, with no `500`. If the resolution itself blocks before the insert, record where it blocked and keep the assertion on the answer. Then close `:398`, citing the case.
> - **Ledger `:711`, the log wording** (Q12): `LostSpace` now covers a Membership lost under a request as well as a Space deleted. Recommended: rename event 1040's message to cover both ("lost its Membership in the Space under the request"), keep the event id, and keep the `CrossSpace` record, because it is the next request's answer and discloses nothing new. Close `:711`.
> - **Ledger `:790`, an actor removed while its change waits** (Q12): in `MembershipStore`, `actor is null` is no longer judged as lost authority. Both the Role change and the removal answer a typed `LostMembership` outcome. The endpoint maps it to a new `MembershipLost` result that **the pipeline** records as `CrossSpace` and answers as the boundary `404`, the `QualifiedRefusal` shape. The leave route uses it for its own "gone" case. Race: hold the Memberships lock, send an Admin's change (and, separately, an Admin's removal) of a Member, wait until it blocks, remove the Admin in the holder's transaction, and commit. The Admin is answered the boundary `404`, recorded `CrossSpace`, with the target unchanged. **Plant:** map `LostMembership` to `QualifiedRefusal`. The case goes red (`403`).

**AC11 — idempotency and races** (AD-18; `TESTING-CONVENTIONS.md:102-180`; ledger `:783`)

> - **Replay:** the same `Idempotency-Key` after a committed removal returns the original `204` and publishes nothing. A different target under the same key is `422`. A malformed key is `400`. A leave's replay is the boundary `404` (Q13; AC3). `Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay` stays green.
> - **Race 1, removal against a Role change on one target:** hold `Yello/Memberships/{spaceId}`, send the Owner's removal of a Viewer and an Admin's Viewer → Member change with distinct keys, wait until both are blocked, then release. The end state is one of the two serial outcomes: either removed (and the change is the in-Space `404`), or changed then removed (`204`, `200`). S-2 holds. The deciding assertion is that both waited. **Plant:** the removal skips the lock. Race 1 goes red.
> - **Race 2, the actor demoted while its removal is queued:** hold the lock, send an Admin's removal of a Member, wait until blocked, demote that Admin to Member in the holder's transaction, and commit. The answer is `403` and the target is intact. **Plant:** the actor clause removed. The answer becomes `204` and the case goes red.
> - **Race 3, two leaves (or a leave and a removal) of one Membership:** both blocked on the lock, then released. One answers `204`. The other is the in-Space `404` (a removal) or the boundary `404` (a leave). One `MembershipChanged` line.
> - **Race 4, removal against acceptance:** the accept takes `Yello/Invitations/{spaceId}`. With the Memberships lock held, an accept in the same Space answers without release (`WaitUntilBlockedOrAnsweredAsync`), as 4.4's race 3 does.
> - **At the bound:** a Space at 100 Memberships, one removed, then a pending Invitation accepted: `201`, and the count is 100 again. The removal frees the bound slot inside the same rules (4.3's bound on acceptance).

## Tasks / Subtasks

- [ ] **Task 0: decisions** (before any code)
  - [x] Q1, Q2, Q8 and Q12 are marked **[LEE]** (authority, isolation or a security seam). If the orchestrator routes them to Lee, record the answers here under "Decided by Lee" before ATDD. Otherwise take each recommendation and record it as "Qn - (x)" with its cost in Completion Notes.
    - **Decided by Lee (2026-11-07):**
      - Q1 - (a): a separate leave route under `ReadProjectsAndTasks`, decided by identity, pinned by a gate as the one write under that capability. Token parity: yes.
      - Q2 - (a): `RemovalAuthority(Removable, HeldBy)` on `ActingAuthority.Removals`, set by the pipeline only. Owner is in no set. Self-target sub-question: (a), so the actor's own non-Owner row is removable on the `{id}` route.
      - Q8 - (a): "Leave this Space" at the foot of Space home for every non-Owner Role, absent for the Owner, and set aside in the empty-state case.
      - Q12 - (a): a typed `LostMembership` outcome, answered as the boundary `404` and recorded `CrossSpace`.
  - [ ] Decide Q3-Q7, Q9-Q11 and Q13-Q16 the same way. Each recommendation is the default.
- [ ] **Task 1: Domain** (AC1, AC2)
  - [ ] `RemovalAuthority` (sets `Removable`, `HeldBy`; `None`; `Judge`) beside `RoleChangeAuthority`, and `RoleCapabilityMatrix.RemovalAuthorityOf`. Owner is in no `Removable` set. Remarks quote FR-14 and the matrix's boundary. **No story ids in new comments** (CLAUDE.md).
  - [ ] Update `Capability.RemoveMembership`'s summary to say what exists, without story numbers.
- [ ] **Task 2: Contracts** (AC1, AC3, AC6)
  - [ ] `MembershipRoutes`: the `DELETE` on `Membership`, the leave route (`Own`, `OwnOf`), and the single-row `GET` if Q5 (a). Update the remarks.
  - [ ] `MembershipResponse` gains `InSpaceNow` (Q5).
  - [ ] `ProblemTypes.OwnershipHeld` (`urn:yello:problem:ownership-held`, Q6), with remarks on status and disclosure.
- [ ] **Task 3: Application** (AC1, AC3, AC4, AC10)
  - [ ] `Yello.Application/Memberships/RemoveMembership/RemoveMembershipHandler.cs` and `Yello.Application/Memberships/LeaveSpace/LeaveSpaceHandler.cs`. The removal slice's name must contain `RemoveMembership` (the Qualified-cell gate).
  - [ ] `ActingAuthority.Removals`. `IMembershipStore.RemoveAsync`, `LeaveAsync` and `ReadAsync` (Q5), plus their result types. Add a `LostMembership` outcome to both the Role change and the removal (Q12).
  - [ ] Handlers read no Role, count nothing and record nothing (AR-3).
- [ ] **Task 4: Infrastructure** (AC1-AC4, AC6, AC10)
  - [ ] `MembershipStore`: the guarded removal `DELETE`, the leave `DELETE`, each under `TakeMembershipsAsync`; the presence delete; the disambiguation read; `Record` after a delete of one row; `InSpaceNow` from `SpacePresence` with `ExpiresAt > SYSUTCDATETIME()` (the database clock, AD-27), in the list and the single read.
  - [ ] Watch S1200, and split by port if it fires.
  - [ ] No migration is expected: `Membership`'s FILTER predicate narrows a `DELETE` to the Space under the Space-scoped unit of work. Prove it with a schema case: under Space A's key, a raw `DELETE` of a Space B Membership id deletes 0 rows. If Q7 chooses a `BLOCK BEFORE DELETE`, read `TESTING-CONVENTIONS.md:565-586` first.
- [ ] **Task 5: Host** (AC1, AC3, AC10)
  - [ ] `QualifiedAuthority` sets `Removals`. The pipeline turns `MembershipLost` into the recorded `CrossSpace` `404` (nested, as `QualifiedRefusal` is).
  - [ ] Event 1040's message (Q12).
  - [ ] `MembershipEndpoints`: `MapRemoveMembership`, `MapLeaveSpace` and `MapReadMembership` (Q5), each with an explicit outcome switch and **no `_ =>` arm** that falls into a success or a `404` (4.1 P10). Map them in `Program.cs` beside the Membership routes, and update the route comment block.
- [ ] **Task 6: Client** (AC6-AC8)
  - [ ] Files: `MembershipRow.razor` (Remove, confirm), `MembershipSection.razor` (per-row in-flight, `:789`, `:791`), `MembershipCapabilities.Removable`, `SpaceHome.razor` plus a new `LeaveSpaceSection.razor` (Q8), the shared purge-and-route helper with `SpaceDeleteSection`, `RefusalCopy`, `ClientCopy.resx`, `components.css`, `FakeYelloApi.Memberships.cs`.
- [ ] **Task 7: tests and plants** (all ACs). See Testing requirements. Save each original in memory and restore it in a `finally`. **Never** `git checkout --`, `git restore` or `git reset`. Record `| Plant | Expected red | Seen |` in the Debug Log.
- [ ] **Task 8: ledger, docs, verification**
  - [ ] Apply the ledger dispositions below: one line per CLOSED/UPDATED note, no TAB or FORM FEED, keep the final newline.
  - [ ] Add a dated note to AD-5 (or AD-1) in `ARCHITECTURE-SPINE.md` naming the removal authority and the leave route's identity rule (Q1, Q2). Add one to AD-6 naming the presence delete beside the Token cascade.
  - [ ] Rewrite comments that predict "4.5" (for example `Capability.cs` `RemoveMembership`, `ActingAuthority.cs` remarks, `RefusalBoundaryGateTests.Memberships.cs:315`, `ActiveSpaceEndpointTests.RevokeInvitation.cs:29`, `MembershipRow.razor` "leaves room") so they say what exists.
  - [ ] `sprint-status.yaml`: this story goes to `review` at the end of dev. `epic-4` stays `in-progress`. FR-14 stays staged (7.7, 7.8).
  - [ ] Verification follows CLAUDE.md's policy. Run `docker info --format '{{.ServerVersion}}'`, then `dotnet build Yello.slnx` (0/0). Iterate on the affected classes with `--filter-class`. Then make **exactly one** full run, in the background, without the browser suite: `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`, logged to `artifacts/test-runs/4-5-dev.log`. Record totals. Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

Removal is 4.4's Role change with a `DELETE` in place of an `UPDATE`. The pipeline, which alone reads the acting Role, hands the slice a second set on `ActingAuthority`: the Roles the actor may remove (Owner in none), and the Roles it must still hold. The store composes them into one guarded `DELETE` under the Space's Memberships lock, with the actor-still-holds-its-Role clause. On a rowcount of zero it reads why: the in-Space `404`, the Owner's `409`, a recorded `403`, or, new here, the boundary `404` when the actor itself has lost its Membership. Leaving is a second route, decided by identity rather than Role: the caller deletes its own row unless that row is the Owner's. Deleting the row is what invalidates the Account's Tokens for the Space, through the existing cascade. The same transaction deletes its presence row and records one `MembershipChanged`, published after commit. Invitations it issued are untouched by construction. Tasks have no Assignee yet, so "unassigned, not deleted" is recorded for 4.6 with the one schema trap 4.6 must avoid. The removed Account's next request, by Session or Token, meets the boundary `404` or the no-credential `401`, and the revocation suite proves both through the product. In the client, Owners and Admins get an in-place Remove confirm that names the person and says whether they are in the Space now. Every non-Owner gets a Leave control. The Members section stops dropping appended pages on a `404` and stops dropping a second row's action.

### What exists today (survey at `a907766`)

- **Domain:**
  - `Membership(Id, SpaceId, AccountId, Role, CreatedAt)`.
  - `RoleCapabilityMatrix` (`:36-53`): `RemoveMembership = [Yes, Qualified, No, No]`. `RoleChangeAuthorityOf` (`:77-82`) with `OwnerChanges`/`AdminChanges` (`:57-70`).
  - `RoleChangeAuthority(From, To, HeldBy)` with `Admits`, `Judge`, `None` (`RoleChangeAuthority.cs`). Mirror it for removal.
  - `SpacePresence(SpaceId, AccountId, ExpiresAt)`: no foreign key to `Membership`, only to `Space` (cascade). The window is 15 minutes, refreshed below 7.5 (`SpacePresenceRegister.cs:50-55`).
- **Infrastructure:**
  - `MembershipStore` (`ListAsync`, `ChangeRoleAsync` → `GuardedUpdateAsync` → `TargetAsync`/`RowAsync`). The pattern to extend. `actor is null` is judged as lost authority today (ledger `:790`).
  - `MembershipConfiguration`:
    - alternate keys `(SpaceId, Id)` and `(SpaceId, AccountId)`;
    - `UX_Membership_SpaceId_Owner`;
    - Space → Membership cascade;
    - Account → Membership `Restrict`.
  - `ApiTokenConfiguration.cs:60-64`: `ApiToken → Membership(BoundSpaceId, AccountId)` **cascade**. That is the Token invalidation.
  - `InvitationConfiguration.cs:128-131`: the issuer references `Account` (`Restrict`), never `Membership`.
  - **RLS on Membership:** FILTER `fn_MembershipVisibilityPredicate`, BLOCK AFTER INSERT and BLOCK AFTER UPDATE (`20261102085905_MembershipBlockAfterUpdate`), all Space-only. No table in the schema carries `BLOCK BEFORE DELETE`. A `DELETE` is narrowed by the FILTER predicate.
  - `ApplicationLocks.TakeMembershipsAsync` (`Yello/Memberships/{spaceId}`). `CommitHooks` and `MembershipChangePublisher` (publish after commit, log event `MembershipChangedPublished`, 1047).
  - `ReferenceConflict.IsReferenceConflict` (547).
  - `IdempotencyRecord` → `Space` cascade, unique `(SpaceId, AccountId, Key)`, no `Membership` reference.
- **Host:**
  - `ActiveSpaceResolution.ResolveCoreAsync` (`:124-199`): establish → capability → resolve → Token bound-Space check → `QualifiedAuthority.Permits` → `ServeWithAuthorityAsync` (sets `ActingAuthority`, turns `QualifiedRefusal` into the recorded `403`).
  - `ServeAsync` (`:212-252`): the presence refresh, then idempotency, then the endpoint.
  - `LostSpace` (`ActiveSpaceResolution.Deletion.cs`): a 547 under a request re-resolves, then answers the boundary `404` with a `CrossSpace` record, logging `SpaceDeletedUnderRequest` (ledger `:711`).
  - `DestroysItsSpace` (one route only, gated).
  - `MembershipEndpoints` (`MapListMemberships`, `MapChangeMembershipRole`).
- **Client:**
  - `SpaceSettings.razor` (sections Name, Labels, Memberships, Invitations, Delete).
  - `MembershipSection.razor` (676 lines: `_changing` section-wide, `GoneAsync` → `LoadAsync` re-reads page 1).
  - `MembershipRow.razor` (238 lines; the remarks say it "leaves room" for removal).
  - `MembershipCapabilities` (`PermitsManageMemberships`, `OfferableRoles`), `SpaceCapabilities.PermitsSpaceSettings`.
  - `SpaceHome.razor` (narration region, `SpaceArrival`, `ProjectList`).
  - `SpaceDeleteSection.Leave` (`:363-377`): `Directory.Removed`, `ActiveSpace.Navigating(null)`, `GoTo(Home, replace: true)`.
  - `ContextBar` (the settings link for Owner/Admin only).
- **Tests:**
  - `WireDatabase.RemoveMembershipAsync` (`:363`), `ChangeRoleAsync`, `AddMembershipAsync`, `MembershipRowsInAsync`, `MembershipOfAsync`, `SpacesNotHoldingExactlyOneOwnerAsync`.
  - `RevocationCases` (`REV-MEMBERSHIP-REMOVED`, `REV-ROLLED-BACK-REMOVAL`, `REV-ROLE-CHANGED-BY-OWNER` with `AdminDemotedByTheOwnerAsync` as the product-route template).
  - `IsolationCases.Memberships.cs` (11 rows).
  - `ActiveSpaceEndpointTests.ChangeMembershipRole*.cs` (the race helpers `WaitUntilBlockedOnALockAsync`/`WaitUntilBlockedOrAnsweredAsync`).
  - `RefusalBoundaryGateTests.Memberships.cs` (14 gates).
  - `MembershipSectionTests.cs`, `FakeYelloApi.Memberships.cs`.
  - `HostPool`: a Slices case shares its class's Host unless it reads the log, which the `MembershipChanged` counts do. Those cases use `HostProcess.RunAsync` (`TESTING-CONVENTIONS.md:785-800`).

### What must be preserved (regression guard)

- **Both Role-read gates stay unwidened.** The removal authority reaches the slice as sets only.
- **The boundary `404` stays byte-identical** (`RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal`, `SameAsAnchorAndStranger`).
- **4.4 unchanged:** the list, the Role change and its races, `MembershipRoleChangeResponse` with no address, the address and attribution gates, and S-2.
- **3.3's Space deletion** still cascades the Owner's Membership. No delete predicate or trigger may block it (Q7).
- **`DestroysItsSpace` stays on one route.**
- **`IdempotencyStore` refuses to save with other changes pending.** Raw SQL tracks nothing, so keep the record's save last.
- **AD-10:** no timer, no hosted service, no polling. Presence is still expired on read, never swept.
- **`REV-MEMBERSHIP-REMOVED` and `REV-ROLLED-BACK-REMOVAL` stay** as the raw-SQL proofs. The product twins go beside them.
- **Address confinement:** the Remove confirm, the announcement and the Leave narration carry no address.

### Locking and the races (RCSI)

- The removal and the leave take `Yello/Memberships/{spaceId}` first (ledger `:783`), then run their one guarded `DELETE`. Lock order: application lock, then rows. No `HOLDLOCK`.
- The actor clause reads the actor's row `WITH (READCOMMITTEDLOCK)`, as 4.4's does.
- **Acceptance** takes `Yello/Invitations/{spaceId}` and inserts. A delete never touches a row being inserted, so the locks are independent (Race 4).
- **Token issuance** takes no Membership lock. Its insert's composite foreign key meets a concurrent delete as a 547, which `LostSpace` answers (AC10).
- **5.2's swap** must take the same lock (still ledgered to 5.2).

### Open questions

Each has a recommendation, which is the default. **[LEE]** marks a question whose answer touches authority, isolation, a security seam or a matrix reading. The orchestrator may route those to Lee; otherwise the dev phase decides them like the rest, as reversible, recorded decisions.

1. **[LEE] How "any Account can remove its own" passes the capability gate** (AC3; C2).
   - **(a) Recommended.** A separate leave route under `ReadProjectsAndTasks`, the capability every Role holds, because leaving is decided by identity, not Role (the AD-26 reasoning). A gate pins it as the one write under that capability. Both surfaces. Cost: a read capability on a `DELETE`, explained in remarks and gated.
   - (b) A new `Capability.LeaveSpace` row, `[Yes, Yes, Yes, Yes]`. Cost: the code matrix gains a row the spec does not have (`role-capability-matrix.md` is "the single source of truth"). `SpaceSwitcherGateTests`' Viewer assertion and the transcription gate must both change.
   - (c) A metadata marker, e.g. `DecidedByIdentity`, exempting the route from `Every_spaceId_endpoint_declares_exactly_one_capability`. Cost: the first exemption from that gate, and a second gate to hold it to one route.
   - Sub-question: Token parity. Recommended: yes (AD-4, FR-35). A Token leaving deletes itself by cascade. Alternative: `SessionOnlyEndpoint`, with a recorded AD-4 variance.
2. **[LEE] The removal target rule** (AC1, AC2; ledger `:329`, `:783`).
   - **(a) Recommended.** `RemovalAuthority(Removable, HeldBy)` from `RoleCapabilityMatrix.RemovalAuthorityOf`, set on `ActingAuthority.Removals` by the pipeline only, composed into one guarded `DELETE` with the actor clause under the Memberships lock. Owner is in no set. This reuses Lee's 4.4 Q1 (a) seam unchanged.
   - (b) Reuse `RoleChangeAuthority.From` as the removable set. Cost: it couples two matrix cells that the spec states separately. They agree today (Admin: Member and Viewer), but an amendment to one would silently move the other.
   - Sub-question, self-target on the `{id}` route: (a) the actor's own non-Owner row is always removable there, so for Owner and Admin both routes agree; (b) the `{id}` route is for others only, and an Admin targeting its own row gets `403`. Cost of (b): "any Account can remove its own" then depends on which route was used. Recommended (a).
3. **Hard delete** (AC4; ledger `:380`). **(a) Recommended:** delete the row, so the cascade takes the Tokens. (b) A soft delete (a `RemovedAt` column). Cost: the cascade cannot see it, AD-6 breaks, and every query and both RLS predicates would need the flag. Not viable.
4. **Routes and shapes** (AC1, AC3, AC6). **(a) Recommended:**
   - `DELETE .../memberships/{membershipId}` → `204`;
   - `DELETE .../membership` (the caller's own) → `204`;
   - `GET .../memberships/{membershipId}` → `200 MembershipResponse` under `ChangeMembershipRole` (for Q5 (a)).
   - (b) `POST .../memberships/{id}/removal`. Cost: a non-REST verb for a plain delete.
5. **Where "live session" comes from, and when it is read** (AC6; UX-DR36).
   - **(a) Recommended.** An unexpired `SpacePresence` row for (Space, target Account), the 15-minute window, read against the database clock. It is fresh at confirm-open through the single-row `GET`, and carried on list rows as `InSpaceNow` for the row tag. The copy says "in this Space now", not "editing", because presence is request recency, not an open editor. Cost: one route and its isolation rows, and one contract member.
   - (b) The list's value only. Cost: up to the list's age stale, while the confirm says "currently".
   - (c) Wait for 7.5's Presence (sync connections). Cost: AC6 is unmet in this story.
   - Sub-decision: removal deletes the target's presence row in the same transaction (recommended), so the NFR-8 bound frees and a rejoin starts clean.
6. **The Owner's answer** (AC2). **(a) Recommended:** `409 ownership-held` for the Owner on its own row through either route, with copy "The Owner leaves by having an Ownership Offer accepted, or by deleting the Space.". An Admin on the Owner's row gets `403`. (b) Reuse `owner-role-fixed`. Cost: its copy is about Roles, and its title says so.
7. **A schema guard against deleting an Owner row** (AC2). **(a) Recommended: none.** The guarded statements plus S-2 hold it, with a dated AD-5 note. (b) A `BLOCK BEFORE DELETE` predicate or an `INSTEAD OF DELETE` trigger refusing an Owner row. Cost: it blocks 3.3's cascade from `Space` and 5.4's Account deletion sequence, so it needs an exemption, which is a bypass by another name.
8. **[LEE/UX] Where a Member or Viewer leaves** (AC3, AC8). Space settings is Owner/Admin only (`EXPERIENCE.md:53`), and 4.7's verbatim AC narrates "Space settings is no longer available" to a demoted Admin.
   - **(a) Recommended.** "Leave this Space" at the foot of Space home for every non-Owner Role, absent for the Owner. Admins use the same control; their own Members row carries no Remove. Cost: an empty Space's home gains a second control beside its one action (UX-DR35; ledger `:542`); set it aside in the empty-state case as 3.1 did the settings link.
   - (b) Space settings opens to every Role, with only a Leave section for Members and Viewers. Cost: the UX table changes, and 4.7's AC copy contradicts it.
   - (c) Account settings lists Spaces with Leave. Cost: the Owner's Spaces must show no Leave, which needs the Role on an Account-scoped surface (an AD-24 amendment), or else a control that fails on click (UX-DR27).
9. **Unassignment before an Assignee exists** (AC4; C1). **(a) Recommended:** record it as met vacuously. Prove "Tasks survive". Ledger the 4.6 constraint (no `ON DELETE SET NULL` on the composite key; unassign explicitly inside the removal's transaction, before the `DELETE`). (b) Build a seam now (e.g. an `IMembershipRemovalStep` list run before the delete). Cost: an abstraction with no second user.
10. **A pending Ownership Offer naming the removed Membership** (`prd.md:222`). **(a) Recommended:** no table exists, so ledger it to 5.1/5.3: an offer lapses when its recipient's Membership is removed, read-time (AD-27), with no write needed if the offer's predicate joins the Membership. (b) Nothing. Cost: the clause is lost.
11. **The lock.** **(a) Recommended:** `Yello/Memberships/{spaceId}` for the removal and the leave (ledger `:783`). (b) No lock, relying on the guarded statement. Cost: a removal and a Role change of one target interleave (Race 1).
12. **[LEE] A request whose own Membership vanished under it** (AC10; ledger `:711`, `:790`).
    - **(a) Recommended.** A typed `LostMembership` outcome that the pipeline turns into the boundary `404`, recorded `CrossSpace`, exactly the next request's answer. Event 1040's message covers "Membership lost" as well as "Space deleted", with the id unchanged. It applies to the Role change, the removal and the leave.
    - (b) Unrecorded `404`. Cost: AD-20's record of boundary refusals would miss one path.
    - (c) Keep the `403`. Cost: it discloses that the actor still reaches the Space's authority check after it has no Membership.
13. **The leave's idempotency** (AC3, AC11). **(a) Recommended:** keep the pipeline's record (its foreign key is to `Space`). A replay after a committed leave is the boundary `404`, AD-18's recorded variance as for 3.3. The client treats a `404` on a same-key resend of Leave as "left". (b) Mark it like `DestroysItsSpace`. Cost: the gate holds that marker to one route, and the record is harmless.
14. **A row action answered `404`** (ledger `:789`). **(a) Recommended:** re-read page 1 only to learn whether the Space answers, then drop the one row from the rows shown, keep the appended pages, and take `Total` from the read. (b) Re-read every page shown. Cost: up to two reads that recycle rows (AD-29).
15. **Concurrent row actions** (ledger `:791`). **(a) Recommended:** per-row in-flight state, independent rows, serialised by the server's lock. (b) A section-wide gate with visible feedback ("Wait for the other change to finish."). Cost: friction with no safety gain.
16. **Copy** (`EXPERIENCE.md:99`; sentence case, no exclamation marks, capability-shaped refusals, no address, no Space name in announcements). **(a) Recommended:**
    - "Remove" (accessible name "Remove {label}")
    - Confirm: "Remove {label} from this Space? They lose access to everything in it at once." plus "{label} is in this Space now." or "{label} has not been in this Space in the last 15 minutes." Then "Remove" / "Cancel" / "Removing."
    - "{label} was removed."
    - "Admins can remove Members and Viewers only."
    - "The Owner leaves by having an Ownership Offer accepted, or by deleting the Space."
    - "Leave this Space" / "Leave this Space? You lose access to everything in it at once. To come back, someone must invite you again." / "Leave" / "Cancel" / "Leaving."
    - "You left the Space."
    - The list tag: "In this Space now".

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. AC4's "Tasks it was Assignee of become unassigned" has no Assignee to act on.** 4.6 builds FR-21's `(SpaceId, MembershipId)` composite key (AR-8). A composite foreign key with `ON DELETE SET NULL` would null `SpaceId` as well, which is non-nullable. So 4.6 cannot rely on the schema, and must unassign inside this story's removal transaction. Q9 and a ledger entry to 4.6.
- **C2. "Any Account can remove its own" versus the matrix's "Remove a Membership: Member —, Viewer —".** The matrix row is about removing *a* Membership. Self-removal is identity-decided and absent from the matrix, like the Offer acceptance it explicitly excludes. Every `{spaceId}` endpoint must still declare one capability. Q1.
- **C3. "Live session" versus what exists.** A Session has no server identity. The mockup says "EDITING NOW", but no editor exists before 7.3. `SpacePresence` is request recency within 15 minutes. Q5 and the copy in Q16 say what it is.
- **C4. Where a Member or Viewer leaves.** UX places all Membership controls in Space settings (Owner/Admin), and 4.7's AC assumes Members have no Space settings. Q8.
- **C5. AC2's "refused" and "absent".** The API refuses (`409`). The UI never offers it (absent). Both hold, at different layers, as UX-DR27's honesty contract.
- **C6. "Removal revokes access immediately"** is met on the request path. The live-session half is 7.7/7.8's (`epics.md:402`), and FR-14 stays staged in `sprint-status.yaml`.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:329` → **CLOSED by 4.5**: the removal consumes the seam (`RemovalAuthorityOf`, `ActingAuthority.Removals`). Name the gates.
- `:380` → **CLOSED by 4.5**, citing the Token case and `REV-MEMBERSHIP-REMOVED-BY-ADMIN`.
- `:390` → **UPDATED by 4.5**: its twins are `REV-MEMBERSHIP-REMOVED-BY-ADMIN` and `REV-LEFT-THE-SPACE`. 5.4 still owes its own.
- `:398` → **CLOSED by 4.5**, citing the race case (or re-owned with the reason if it is still a `500`).
- `:711` → **CLOSED by 4.5 (Q12)**.
- `:758` → **CLOSED by 4.5** (the removal arm runs through `DELETE`).
- `:783` → **UPDATED by 4.5**: the removal takes the lock and the seam. 5.2 still owes the swap.
- `:789`, `:790`, `:791` → **CLOSED by 4.5**, with the case names.
- **New entries:**
  - "4.6's Assignee foreign key cannot be `ON DELETE SET NULL`; unassign inside the removal's transaction" (owner **4.6**; medium).
  - "An Ownership Offer lapses when its recipient's Membership is removed" (owner **5.1/5.3**; low).
  - "The leave route is the one write under `ReadProjectsAndTasks`" (owner **Lee**; low; recorded, not a defect), if Q1 (a).
  - "'Live session' is presence within 15 minutes, not an open editor; 7.5 may sharpen it" (owner **7.5**; low).

### Architecture compliance (must)

- **AD-21 ring.** Handlers in Application; the store in Infrastructure; the pipeline seam and endpoints in Host; contracts in Contracts. Check `AllowedReferenceEdges.cs`.
- **AR-3 / AD-1.** Slices never read a Role, count a bound, set a key or record idempotency or refusals.
- **AD-2.** No raw SQL outside Infrastructure. Every new statement carries `SpaceId = @space` as well as relying on the key.
- **AD-3.** Every not-found is the boundary body. Only the pipeline records.
- **AD-6.** Tokens go in the same transaction, by the cascade.
- **AD-9 / AR-16.** One publish per affected Account, after commit. No timer (AD-10).
- **AD-20.** Every `403` and every boundary `404` record is the pipeline's.
- **AD-23.** No address in a log, in the removal or leave answers, or in an idempotency record. The removal and the leave answer `204` with no body.
- **AD-27.** `InSpaceNow` uses the database clock in the statement's own `WHERE`/projection.
- **No test-only code in production** (1.9 Q4). Observe the publish through its log line.
- **Code comments** (CLAUDE.md, commit `dcdbc83`): say why, never "added by story 4.5", no AC or Q ids, no history. Existing comments are rewritten only where they predict 4.5 (Task 8).

### Library / framework

No new package. Use:

- EF Core 10.0.11 (`Database.ExecuteSqlAsync` for the guarded `DELETE`s, as `GuardedUpdateAsync` does; LINQ projections for the reads);
- ASP.NET Core 10.0.11 minimal APIs (`MapDelete`, `MapGet`);
- Blazor WebAssembly 10.0.11;
- Microsoft.Data.SqlClient 7.0.2;
- xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3, and Mono.Cecil for the IL gates.

No web research was needed: every API used is already in the codebase. `STRING_SPLIT` and `READCOMMITTEDLOCK` are already used in `MembershipStore` on SQL Server 2025.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md` (`:545-564` Memberships; `:87-200` rules; `:756-811` isolation and revocation):

- Traits: `Suite`, `Priority`, `Requirement` (FR-14, FR-36, AD-6, AR-16/AD-9, NFR-2, UX-DR36, AD-3), `Assumption` (`PRD-12-4` for every case resting on "Admins cannot modify each other", i.e. an Admin refused on another Admin), and `Tier`. Names are underscore sentences.
- Never `Task.Delay`. Races wait on `LCK_M_%`.
- **Never a shared email address** (`InvitationWire.FreshAddress`). Display names are randomised.
- **Seed through the product where the case is about Membership** (an accepted Invitation at the Role). Seed directly for bulk (the 100 bound), for `MembershipChanged` counts (the accept publishes its own), and for presence expiry. Record which.
- **S-2 after every removal and leave case and every race.**
- **No new container-backed test class.**
  - Slices: partials `ActiveSpaceEndpointTests.RemoveMembership.cs`, `.RemoveMembershipRace.cs`, `.LeaveSpace.cs`, and the `RevokeInvitation.cs` conversion.
  - Handler unit tests take no database (`tests/Yello.Tests.Slices/Memberships/MembershipHandlerTests.cs` extended: `RemovalAuthority.Judge`, fail-closed `None`, the authority handed as sets).
  - Isolation: rows in `IsolationCases.Memberships.cs`. Revocation: two rows in `RevocationCases.cs`.
  - Client: extend `MembershipSectionTests.cs`, add `LeaveSpaceTests.cs` on the existing collection.
  - Gates: extend `RefusalBoundaryGateTests.Memberships.cs`.
- **Every absence has an anchor and is seen red under a plant.** A masked plant is recorded, never deleted.
- **Memory budget (CLAUDE.md).** One `dotnet test` at a time. Iterate with `--project ... -- --filter-class "<full name>"`. The Revocation project starts its own SQL Server, so never run it beside another project. The single full run goes in the background with `--max-parallel-test-modules 1`, without the browser suite in Develop.

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | Admin's `Removable` gains Admin | the Admin-removes-Admin case; `ISO-REMOVE-MEMBERSHIP-ADMIN-BEYOND-AUTHORITY`; the transcription gate |
| 2 | Owner added to the Owner's `Removable` | the Owner own-row case; S-2; the transcription gate |
| 3 | The leave statement loses `Role <> 'Owner'` | the Owner leave case; S-2 |
| 4 | The removal's actor clause removed | Race 2 |
| 5 | The removal skips the Memberships lock | Race 1's deciding assertion (or recorded masked, with the reason) |
| 6 | A soft delete (an `UPDATE` setting the Role to Viewer, or any non-delete) instead of `DELETE` | the Token `401` case; `REV-MEMBERSHIP-REMOVED-BY-ADMIN` (Token surface) |
| 7 | The presence row is not deleted | the presence-after-removal case (and `InSpaceNow` after a rejoin) |
| 8 | `MembershipChanged` recorded before the `DELETE`, or on every attempt | the no-publish cases |
| 9 | `LostMembership` mapped to `QualifiedRefusal` | the AC10 removed-actor race (`403` instead of `404`) |
| 10 | The removal slice reads `ActiveSpaceContext.Role` | `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` |
| 11 | A second writer of `ActingAuthority.Removals` | `Only_the_pipeline_sets_the_acting_authority` |
| 12 | A second write route under `ReadProjectsAndTasks` | `The_leave_route_is_the_one_write_under_ReadProjectsAndTasks` (Q1 (a)) |
| 13 | A Remove control rendered for an Admin on an Admin row | the client absence case; the agreement gate |
| 14 | A `disabled` Remove/Leave control | `No_control_in_the_Membership_section_is_disabled` (extended) |
| 15 | The confirm pre-focuses "Remove" | the no-default client case |
| 16 | `GoneAsync` re-reads and replaces the rows | the ledger `:789` case |
| 17 | The section-wide `_changing` gate restored | the ledger `:791` case |
| 18 | The address added to the confirm or announcement | the no-`@` client cases |
| 19 | `InSpaceNow` read with the app clock, or without the Space key | the expired-presence and other-Space cases |

### Previous story intelligence (4.4, 4.3, 4.2)

- **4.4:**
  - The acting-authority seam is Lee's Q1 (a). The removal reuses it, never the Role.
  - S1200 pushed pipeline steps into nested types (`QualifiedAuthority`, `LostSpace`). Expect the same for `MembershipLost`.
  - The Qualified-cell gate matches the capability's **whole** name. Name the slice `RemoveMembership...`.
  - A pipeline change needs `SpaceBoundaryPipelineTests` and `ActiveSpaceUnitOfWorkTests` to register the new services (19 cases broke on "No service for type ActingAuthority"). Any new pipeline dependency must be registered there too.
  - The address-in-idempotency-record finding: the removal and leave answer `204` with no body, so nothing is stored but the status. Keep it that way.
  - The client's `Yello.Tests.Client` used to need the client rebuilt. Since `6145bf7` the suite publishes the client itself.
  - Plant 14 was left in the tree after an OOM kill. After any killed run, grep for plant markers before continuing.
  - `WaitForEmailToAsync` returns the first email to an address, so seed a second Membership of one address directly.
- **4.3:** acceptance seeds Members through the product, and the Membership bound is on acceptance. A removal frees a slot (AC11).
- **4.2:** the guarded statement plus a disambiguation read; the generation counter; focus never to `body`; the fake answers what the Host answers, through contract constants.
- **3.3:** `LostSpace`, `DestroysItsSpace`, `SpaceDeleteSection.Leave`, and the product-route `REV-` twin precedent.

### Git intelligence

Recent commits:

- `a907766`: mail-file read retry.
- `6145bf7`: the Client suite publishes the client and runs four classes in parallel; timing classes are serial in `BrowserTimingCollection`.
- `7e0fc54`: `HostPool`, one Host per Slices class. Log-reading cases need their own process.
- `dcdbc83`: the test-execution policy and the comment rule.
- `40c28bb`: the 4.4 review close (`MembershipRoleChangeResponse`, no address).
- `84eef75`: the 4.4 implementation.

The cycle is: story; decisions recorded; ATDD scaffolds (`Skip = "red: ..."`, `atdd-checklist-<key>.md`, `PendingUntil` rows); implementation; review fixes. The orchestrator commits each.

### Project Structure Notes

- **New (expected):**
  - `Yello.Domain/Memberships/RemovalAuthority.cs`
  - `Yello.Contracts` additions in `Memberships/MembershipRoutes.cs`, `MembershipResponse.cs` and `ProblemTypes.cs` (no new contract type needed; `204` has no body)
  - `Yello.Application/Memberships/RemoveMembership/RemoveMembershipHandler.cs`, `Yello.Application/Memberships/LeaveSpace/LeaveSpaceHandler.cs`, and, for Q5 (a), `Yello.Application/Memberships/ReadMembership/ReadMembershipHandler.cs`
  - `Yello.Host/Spaces/ActiveSpaceResolution.*` nested `MembershipLost` handling (or in `.Authority.cs`)
  - `Yello.Client/Components/LeaveSpaceSection.razor`, and a shared purge-and-route helper in `Yello.Client/Spaces/`
  - Tests: the Slices partials above, `LeaveSpaceTests.cs`
- **Updated (expected):**
  - Domain: `RoleCapabilityMatrix.cs`, `Capability.cs` (comment)
  - Application: `ActingAuthority.cs`, `IMembershipStore.cs`
  - Infrastructure: `MembershipStore.cs` (split if S1200 fires)
  - Host: `ActiveSpaceResolution.Authority.cs`, `ActiveSpaceResolution.Deletion.cs`, `SpaceLog` (event 1040's message), `MembershipEndpoints.cs`, `Program.cs`
  - Client: `MembershipRow.razor`, `MembershipSection.razor`, `MembershipCapabilities.cs`, `SpaceHome.razor`, `SpaceDeleteSection.razor` (the helper), `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`
  - Tests: `RefusalBoundaryGateTests.Memberships.cs`, `RefusalBoundaryGateTests.cs` (transcription), `IsolationCases.Memberships.cs`, `RevocationCases.cs`, `ActiveSpaceEndpointTests.RevokeInvitation.cs`, `MembershipSectionTests.cs`, `FakeYelloApi.Memberships.cs`, `MembershipHandlerTests.cs`, `TextSpacingTests.cs`, the empty-Space-home "one action" case (Q8), `SpaceBoundaryPipelineTests.cs`/`ActiveSpaceUnitOfWorkTests.cs` if the pipeline gains a dependency
  - Docs: `ARCHITECTURE-SPINE.md` (dated notes on AD-5/AD-1 and AD-6), `deferred-work.md`, `sprint-status.yaml`
- **No new project, no new package, no new container-backed test class, and no migration expected** (Q7 (a)).

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-4-5-remove-a-membership-or-leave-a-space.md` (decisions, assumptions,
seed constraints and findings).

| Scaffold file | Cases | ACs |
|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RemoveMembership.cs` | 17 methods (22 cases): 16 skipped, 1 active | AC1, AC2, AC4, AC6, AC7, AC9, AC11 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.LeaveSpace.cs` | 5 methods (7 cases), all skipped | AC2, AC3, AC4, AC11 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RemoveMembershipRace.cs` | 5 methods (6 cases): 4 skipped, 1 active | AC10, AC11 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs` (one added) | 1, skipped | AC5 (ledger `:758`) |
| `tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs` (`RemovalCases`) | 11 rows, `PendingUntil: "4.5"` | AC1, AC3, AC6, AC9 |
| `tests/Yello.Tests.Revocation/RevocationCases.cs` | 2 rows x 2 surfaces, skipped | AC4, AC7 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.RemoveMembership.cs` | 7, all skipped | AC1, AC2, AC3, AC6, AC8 |
| `tests/Yello.Tests.Client/MembershipSectionTests.cs` (eight added) | 8 methods (10 cases), all skipped | AC6, AC8 (ledger `:789`, `:791`) |
| `tests/Yello.Tests.Client/LeaveSpaceTests.cs` (new class, existing assembly fixtures) | 7 methods (11 cases): 6 skipped, 1 active | AC2, AC3, AC7, AC8 |

Harness: `MembershipWire` gains three stand-ins (`LeaveRoute`, `LeaveOf`, `OwnershipHeldProblemType`) that Task 2 replaces
with contract members and deletes, plus lasting readings (`RemoveCapabilityCode`, `LeaveCapabilityCode`,
`PresentRowMembers`, `PresentMembership`). `FakeYelloApi.Memberships.cs` answers the removal, the leave and the single-row
read as the Host will.

**Green today, left active (standing regression gates):**
`A_Membership_DELETE_under_one_Spaces_key_deletes_nothing_in_another_Space` (the FILTER predicate narrows a raw `DELETE`
to the keyed Space, so no migration is needed for removal isolation) and
`A_Token_issue_racing_the_removal_of_its_issuer_is_the_boundary_404_and_never_a_500` (ledger `:398`: the `500` is already
gone; 3.3's `LostSpace` catches the `547`. Close `:398` citing it), and the browser case
`LeaveSpaceTests.A_removed_Members_next_request_meets_the_same_refusal_as_a_Space_that_never_existed` (AC7's client half:
the one refusal surface already answers a Space the server stops listing).

**Activation order** (riskiest assumption first):

1. `An_Admin_removed_while_its_action_waits_meets_the_boundary_404_recorded_CrossSpace` (both arms). It tests Lee's Q12 (a):
   the store returns a typed `LostMembership` and the **pipeline**, not the slice, turns it into the recorded boundary `404`.
   The change arm runs on 4.4's existing route, so it goes red first for the right reason (`403` today, ledger `:790`).
2. `An_Admin_demoted_while_its_removal_is_queued_is_refused_403_and_the_target_stays`, then
   `An_Admin_removing_another_Admin_or_the_Owner_is_refused_403_recorded_and_nothing_changes`,
   `ISO-REMOVE-MEMBERSHIP-ADMIN-BEYOND-AUTHORITY`, and the gates `The_removal_rules_agree_with_the_matrix_transcription`,
   `Only_the_pipeline_sets_the_acting_authority` (existing) and `A_Qualified_cell_reaches_no_endpoint_without_its_target_rule`
   (existing; now finds `RemoveMembership`). This is Lee's Q2 (a): the removal sets on `ActingAuthority.Removals`, one guarded
   `DELETE`, Owner in no set.
3. The Owner/Admin/Member/Viewer removal cases, the Owner's own-row `409`, the Admin's own-row `204`, the not-found case,
   with S-2 after each; then `The_Membership_routes_declare_one_capability_each_and_serve_both_surfaces` (delete 4.4's
   `The_Membership_routes_require_ChangeMembershipRole_and_serve_both_surfaces` when this goes green).
4. The leave: the three-Role case, the Owner's `409`, the Token leave, the replay, then
   `The_leave_route_is_the_one_write_under_ReadProjectsAndTasks` (Lee's Q1 (a)).
5. Effects: the Token `401` case, the presence case, the two publish cases per route, what-survives, the very-next-request
   case, the bound case, the idempotency case.
6. Races 1 and 3. Then the single-row read (`InSpaceNow`) and its contract gate; then `ownership-held` and its copy gate.
7. Every isolation row (clear `PendingUntil: "4.5"` before review, GATE-003), then `REV-MEMBERSHIP-REMOVED-BY-ADMIN` and
   `REV-LEFT-THE-SPACE`, then the AC5 twin (delete the raw-SQL original and take its name).
8. The client gates (`The_clients_Remove_control_agrees_with_the_removal_rules_for_every_pair`,
   `No_Remove_or_Leave_control_is_disabled`), then the browser cases: Remove visibility, the confirm, the removal, the `403`
   copy, ledger `:789` and `:791`, reflow; then `LeaveSpaceTests`.

Amend, not scaffolded (each turns red when the work lands): 4.4's routes gate (replaced, above); `RowMembers` gains
`inSpaceNow` when `MembershipResponse` does; `SpaceHomeProjectsTests.An_empty_Space_states_no_Projects_yet_and_offers_one_action_to_create_one`
sets the Leave control aside (Q8); `TextSpacingTests` if it enumerates controls.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

### References

- `epics.md`: `:98` (FR-14), `:114` (FR-21), `:172` (SM-2), `:178` (§6.1), `:196` (AR-8), `:204` (AR-16), `:280` (UX-DR36), `:373`, `:402`, `:1613-1617`, `:1796-1834` (4.5), `:1836-1875` (4.6, the boundary), `:1877-1916` (4.7, the boundary).
- `prd.md`: `:222-223` (an offer lapses on removal; an offer is not an exit), `:239`, `:303-313` (FR-14).
- `ARCHITECTURE-SPINE.md`: AD-1, AD-2, AD-3, AD-5, AD-6 (`:112-116`), AD-9 (`:130-135` with its 4.4 note), AD-18, AD-20, AD-23, AD-26, AD-27, AD-29.
- `EXPERIENCE.md`: `:53`, `:85-89`, `:146`, `:153-156`, `:209`, `:213`, `:220`, `:244`, `:267-271`, `:509`, `:523`, `:541-551` (UJ-8). `mockups/space-settings.html:21`, `:108-149`.
- `specs/spec-yello/`: `acceptance-criteria.md:139-146` (CAP-14); `role-capability-matrix.md:22`, `:37-40`; `SPEC.md:86-88`; `surfaces-and-journeys.md:98-108` (UJ-8).
- `test-design-qa.md`: `:281` (V-2), `:282` (V-3), `:283-288` (V-4/V-5/V-9, 7.x). `test-design-architecture.md:206` (R1).
- `implementation-readiness-report-2026-08-22.md`: `:198`, `:493`, `:532`.
- `tests/TESTING-CONVENTIONS.md`: `:87-200`, `:545-564`, `:565-586`, `:756-811`.
- `deferred-work.md`: `:329`, `:380`, `:390`, `:398`, `:542`, `:711`, `:758`, `:783`, `:789`, `:790`, `:791`.
- `4-4-manage-memberships-and-roles.md` (Q1-Q19, Review Findings, Debug Log plants), `4-3-accept-an-invitation.md`, `4-2-see-and-revoke-pending-invitations.md` (AC3), `3-3-delete-a-space-and-belong-to-none.md` (Q1, Q4: `LostSpace`, `DestroysItsSpace`).

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-11-07). Sixteen open questions with recommendations; Q1, Q2, Q8 and Q12 are marked [LEE].

### File List
