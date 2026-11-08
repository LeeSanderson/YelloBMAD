---
baseline_commit: 2251d84
---
# Story 5.1: Offer ownership of a Space, and tell the recipient

Status: ready-for-dev

Epic: 5 — Hand a Space over and leave cleanly (the first story of the epic)
Story key: `5-1-offer-ownership-of-a-space-and-tell-the-recipient`

**Requirements owned.** From the Story Coverage Index (`epics.md:376`): **FR-8**, **FR-43**, AR-12, AR-31, AR-33, UX-DR14. Test-design rows **I-14** (the offer email discloses nothing across a boundary, `test-design-qa.md:278`) and **S-2** (no Space holds zero or two Owners, `:319`) after every write.

- **FR-8** (`prd.md:213-225`; glossary `:64`; `role-capability-matrix.md:23`, `:28`, `:38-40`). Every clause binds:
  - Ownership can be offered only to an existing Membership. Never to an email address, an Invitation, or an Account with no Membership in the Space. **Any Role may be named.**
  - Making the offer does not move ownership. The offering Owner stays Owner with every capability until acceptance (5.2).
  - At most one pending offer per Space.
  - The Owner may revoke a pending offer. Revocation leaves every Membership and Role exactly as it was.
  - A pending offer lapses when the recipient's Membership is removed (FR-14) or their Account is deleted (FR-3). Lapsing changes no Role.
  - The offering Owner's Membership is still not removable, and their Account deletion still refused, while an offer is pending: "making an offer is not itself an exit" (`prd.md:222`, `:309`).
  - The offer expires: `[ASSUMPTION: the offer expires after 7 days, mirroring FR-39.]` (`prd.md:225`; PRD §12 assumption #3, `:894`, unconfirmed). Every case that rests on the 7 days carries `Assumption = "PRD-12-3"` (R10, `test-design-architecture.md:548-556`).
- **FR-43** (`prd.md:663-677`): exactly one email per offer, to the named recipient. It names the Space, says the recipient is being offered ownership of it, and names who offered it. It discloses nothing about the Space's contents, its other Memberships or any other Space. It carries **no means of accepting**. Nothing is sent on revoke, decline or lapse. A send record (Space, kind, time; never content or address) is kept (`:677`, AD-10's 4.6 note: `NotificationSend`).
- **The matrix** (`prd.md:352`; `role-capability-matrix.md:23`): "Offer to transfer ownership | ✔ | — | — | —". The code already transcribes it: `Capability.OfferOwnershipTransfer` = `[Yes, No, No, No]` (`Yello.Domain/Memberships/RoleCapabilityMatrix.cs:50`). It is **Allowed/Denied, not Qualified**, so it needs no `ActingAuthority` set. Acceptance (5.2) is the row deliberately outside the matrix (`Capability.cs` remarks; `RefusalBoundaryGateTests.OfferAcceptance`).
- **AD-26** (`ARCHITECTURE-SPINE.md:250-255`), the binding decision: an `OwnershipOffer` carries `SpaceId`, the named recipient `MembershipId`, `ExpiresAt` and `State` ∈ {`Pending`, `Accepted`, `Declined`, `Revoked`, `Lapsed`}. A filtered unique index on `OwnershipOffer(SpaceId) WHERE State = Pending`. Every transition is guarded by `WHERE State = Pending` with a rowcount check (AD-18 on top). A transition refused because the offer is no longer pending returns **409** with a stable problem `type`, never 404. Exactly one email per offer, enqueued in the same transaction as the insert, dispatched in-process on enqueue.
- **AD-5** (`:106-113`, with its 2026-11-02 and 2026-11-07 notes): one Owner per Space by `UX_Membership_SpaceId_Owner`; "at least one" rests on the guarded statements and S-2; removing or deleting **lapses** a pending offer naming that Membership "rather than deleting it or leaving it orphaned". The Owner on its own row answers `409 ownership-held`.
- **AD-9** (`:134-140`, with the 4.4 and 4.5 notes): the acting-authority seam (`ActingAuthority.RoleChanges`/`.Removals`, set by the pipeline only), the `Yello/Memberships/{spaceId}` lock that every Membership write takes, and **Owner in no acting set**. 5.1 changes no Role and publishes no `MembershipChanged`.
- **AD-27 / AR-31** (`:257-261`; `epics.md:219`): one shared actionable predicate, `State = Pending AND ExpiresAt > now`, evaluated in the statement's own `WHERE` against the **database clock**. No job and no timer writes a lapsed state. Lapse **by event** is written inside the causing transaction. The two are deliberately not unified.
- **AR-12** (`epics.md:200`): both filtered unique indexes, and invariant tests that no Space holds zero or two Owners.
- **AR-33 / AD-10** (`epics.md:221`; spine `:142-147`): email goes through the outbox in the triggering write's transaction, dispatched in-process on enqueue, never on a timer.
- **UX-DR14** (`epics.md:252`; `EXPERIENCE.md:216`, `:263`, `:438`): the `ownership-panel`, Owner-only: offer to a named Membership of **any** Role, see the pending offer with its recipient and its expiry computed against the **server** clock, and revoke it. An Admin has **no** ownership affordance present (absent, not disabled; UX-DR27). Mockup: `mockups/space-settings.html` (the "Ownership" section: "Ownership offered to Nadia Ansari. Waiting for her to accept. EXPIRES IN 6 DAYS [Revoke]"; the Admin view has no Ownership panel at all).

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Severity | Covered by |
|---|---|---|---|
| `:544` | Space settings: remaining owners 5.1 (ownership) and 6.3. 5.1 adds the Ownership section and its predicate to the settings-link union | low | AC9, AC12; Q8 |
| `:664` | The Offer indicator must survive every breakpoint (owner "5.1") | low | Q9 (re-own to 5.2, which builds the indicator: `epics.md:1983-1986`, UX-DR9) |
| `:705` | Ownership Offers on a Space deletion: state it in AD-5 and prove it with a populated-Space deletion case | low | AC11; Q2 |
| `:817` | An Ownership Offer lapses when its recipient's Membership is removed (owners 5.1 and 5.3; 4.5's Q10) | low | AC5; Q4 |

**Depends on** (all done):

- 1.5/1.6: the pipeline, `RoleCapabilityMatrix`, `RequiresCapability`, refusal recording, idempotency, `NotAvailableFloor`, the boundary `404`.
- 3.3: `SpaceStore.DeleteCoreAsync` (one `ExecuteDelete` on `Space`, relying on cascades) and the populated-Space deletion case.
- 4.1: the outbox (`IOutbox`, `OutboxStore`, `OutboxDispatcher`, `OutboxCommitSignal`), `DatabaseClock`, `OfferPredicates.Actionable` (Invitation), the 7-day `ExpiresAt` column default, the email template-as-content-contract pattern (`InvitationEmail.Render`).
- 4.2: the guarded revoke (`ExecuteUpdate ... Where(Actionable)`, rowcount, then a disambiguation read) and `invitation-not-pending`.
- 4.4/4.5: `MembershipStore`, `ApplicationLocks.TakeMembershipsAsync`, `RemoveCoreAsync`/`LeaveAsync` (the lapse's insertion point), `409 ownership-held`, `MembershipLabels`, the Members section.
- 4.6: `NotificationSend` at every enqueue, `AssignmentEmail` (strings and a `Uri` only, a link to the client origin with no ids), `MembershipStore.AssignmentNoticeAsync` (the recipient's address and the actor's name read in `MembershipStore`, so no address-reader gate widens), `IActiveSpaceNameReader`, `SoleDispatcherWireAsync`.
- 4.7: `ActiveSpaceEndpointTests.RoleMatrix.cs`'s `MatrixRoute` table and its coverage gate (C4 there: a matrix row gains its `MatrixRoute` when its endpoint is built).

**Explicitly not in this story:**

- **The recipient's side** (5.2): the context-bar Offer indicator (UX-DR9, UX-DR41), the offer dialog, acceptance, the demote-then-promote swap, the two `MembershipChanged` publishes, the recipient's read of the offer inside the Space.
- **Decline, "This offer is no longer open.", the 404-vs-409 ordering for a non-member caller, the A-10 timer gate's `OwnershipOffer` half** (5.3). 5.1's revoke is Owner-only, so it is resolved by the pipeline first anyway; 5.1 answers the revoke's own `409` (AC4) and leaves the general transition rules to 5.3.
- **Account deletion** (5.4). 5.1 builds the lapse so that 5.4 calls it (AC5, Q10); the Account-deletion arm and "the offering Owner's Account deletion is still refused" are proved by 5.4.
- **Telling the Owner the offer was declined** (FR-42; 5.3).
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits.
- **No new NuGet package and no new project without Lee** (CLAUDE.md).

## Story

As an Owner who wants to hand a Space on,
I want to offer ownership to someone already in it and have them know it is waiting,
so that leaving is possible without anyone being made an Owner against their will.

## Acceptance Criteria

AC1 to AC9 are reproduced **verbatim** from `epics.md:1932-1973` and numbered by position. AC10 to AC13 come from the spines, the ledger and a survey of the code; each cites its source. The indented notes are this story's implementation guidance. A note that depends on an open question names it. Questions marked **[LEE]** touch authority, isolation or a security seam: the orchestrator may route them to Lee before development; otherwise the dev phase takes the recommendation and records it as a reversible decision.

**AC1 — an offer names an existing Membership** (FR-8, AD-26)
**Given** a Space
**When** its Owner offers ownership to an existing Membership at any Role
**Then** an `OwnershipOffer` is created naming that Membership, carrying `SpaceId`, `ExpiresAt` and a `State` of `Pending`
**And** it cannot be offered to an email address, to an Invitation, or to an Account holding no Membership in that Space

> - **Domain** (`Yello.Domain/Spaces/OwnershipOffer.cs`, AD-26's area 4.2 Spaces, spine `:410`, `:431`): `OwnershipOffer { Id, SpaceId, RecipientMembershipId, OfferedByMembershipId, OffererName, State, ExpiresAt, CreatedAt }` and `enum OwnershipOfferState { Pending, Accepted, Declined, Revoked, Lapsed }` (all five now, so 5.2/5.3 add no enum member and no migration). `State` stored as a string (max 16), as `InvitationState`. `OffererName` is the attribution captured at authoring time (`Invitation.IssuerName` precedent; `Attribution_never_reads_a_live_Account_name`). Q2.
> - **Route** (Q3): `POST /api/v1/spaces/{spaceId:guid}/ownership-offers`, body `OfferOwnershipRequest(Guid? MembershipId)`, `.RequiresCapability(Capability.OfferOwnershipTransfer)`, both surfaces (AD-4). `201 Created` with `OwnershipOfferResponse` (AC9's shape) and a `Location` of the pending-offer read. `Idempotency-Key` by the pipeline.
> - **The recipient rule** (Q5 **[LEE]**): the insert is **one guarded statement** that proves the recipient in the same statement, under `ApplicationLocks.TakeMembershipsAsync` (Exclusive):
>
>   ```
>   INSERT INTO dbo.[OwnershipOffer] (Id, SpaceId, RecipientMembershipId, OfferedByMembershipId, OffererName, State, CreatedAt)
>   SELECT @id, @space, recipient.Id, owner.Id, @offererName, N'Pending', @createdAt
>   FROM dbo.[Membership] AS recipient
>   JOIN dbo.[Membership] AS owner ON owner.SpaceId = @space AND owner.AccountId = @actor AND owner.Role = N'Owner'
>   WHERE recipient.Id = @recipient AND recipient.SpaceId = @space AND recipient.Role <> N'Owner'
>   ```
>
>   (`ExpiresAt` from the column default, AC10.) Or the EF equivalent: a read of both rows under the lock, then `Add` + `SaveChanges`. The lock makes either correct; the statement form is recommended because it carries the rule in the `WHERE` (record which).
>   - Rowcount 0, disambiguated by one read: the recipient is the caller's own (Owner) Membership, does not exist, or belongs to another Space → **`400 invalid-offer-recipient`** ("Ownership can be offered only to another Membership of this Space."), one body for all three, so a foreign id is indistinguishable from a random one (Q6; the 4.6 `400 invalid-assignee` precedent for an id carried in a body).
>   - The caller is no longer the Owner (demoted is impossible, removed is the pipeline's `MembershipLost`): return the typed `LostMembership` the 4.5 way; the pipeline records `CrossSpace` and answers the boundary `404`.
> - **"Not an email, not an Invitation, not a non-member":** the contract has **no** address field and no Invitation field; a body naming an `EmailAddress` instead of a `MembershipId` is `400` (the missing `MembershipId`); an Invitation id or an Account id in `MembershipId` matches no Membership row → `400 invalid-offer-recipient`. Each is its own Slices case. The contract-shape gate pins `OfferOwnershipRequest` to exactly `MembershipId`.
> - **Any Role:** a Theory over Admin, Member and Viewer recipients, each joined **through the product** (an accepted Invitation, `JoinedThroughTheProductAsync`), each `201`.

**AC2 — ownership does not move** (FR-8)
**Given** the offer
**When** it is created
**Then** ownership does not move — the offering Owner remains the Owner with every capability of the Role until it is accepted

> - After a `201`: every Membership's Role is as before (`WireDatabase.MembershipRowsInAsync` before/after), S-2 holds, and the Owner still succeeds at an Owner-only action (rename the Space, and read the pending offer under `OfferOwnershipTransfer`). No `MembershipChanged` is recorded (no `MembershipChangedPublished` log line; a `HostProcess.RunAsync` case, because it reads the log).
> - The recipient's Role and capabilities are unchanged: a Viewer recipient still gets the recorded `403` on a write.

**AC3 — one pending offer per Space, by the schema** (FR-8, AD-26, AR-12)
**Given** a Space with a pending offer
**When** a second offer is attempted
**Then** it is refused by a filtered unique index on `OwnershipOffer(SpaceId) WHERE State = Pending`
**And** this is the same schema-level guarantee that gives Owner uniqueness, for the same reason

> - **Index:** `UX_OwnershipOffer_SpaceId_Pending` (a `SchemaNames` constant beside `MembershipOwnerUniqueIndex`), `HasIndex(SpaceId).IsUnique().HasFilter("[State] = N'Pending'")`.
> - **The answer:** `409 ownership-offer-pending` ("This Space already has a pending Ownership Offer. Revoke it to make another.") (Q6). The store catches `SqlException` 2601/2627 on the insert and returns `AlreadyPending` (the `ChangeRoleCoreAsync` precedent). It may also pre-check under the lock; the index is the guarantee and the catch is what makes it a `409` and never a `500`.
> - **Proof that the index refuses, not the code:** (i) a schema case: two raw `INSERT`s of `Pending` rows for one Space under its key → the second fails `2601` naming `UX_OwnershipOffer_SpaceId_Pending`; a `Revoked` and a `Pending` row coexist. (ii) A product case: offer, offer again (another recipient, another key) → `409`, one `Pending` row. (iii) **Plant:** drop the pre-check (if any) → still `409` (the index); drop the index from the migration → the case goes red.
> - **A time-expired pending row must not block a new offer** (Q1 **[LEE]**, C1). Recommended (a): inside the offer's transaction, before the insert, under the same lock, the offer store writes `State = Lapsed` on this Space's `Pending` rows that are **no longer actionable** (`ExpiresAt <= SYSDATETIMEOFFSET()`, the negation of the shared predicate, composed from it). This is a request-path write caused by the new offer, not a scheduled one (AD-27's ban is on timers and jobs). Case: seed a `Pending` row with `ExpiresAt` in the past (R12: expiry is reached only by seeding the past), offer → `201`, the old row is `Lapsed`, one `Pending` row. Plant: skip the supersede → `409`, red.

**AC4 — revoke leaves everything as it was** (FR-8, AD-26)
**Given** a pending offer
**When** the offering Owner revokes it
**Then** every Membership and Role is left exactly as it was

> - **Route** (Q3): `DELETE /api/v1/spaces/{spaceId:guid}/ownership-offers/{offerId:guid}` under `OfferOwnershipTransfer` → `204`. By id, so a stale panel cannot revoke a newer offer it never saw.
> - **The statement** (the 4.2 shape): `ExecuteUpdate` on `OwnershipOffer` `.Where(o => o.Id == offerId).Where(OfferPredicates.OwnershipOfferActionable)` setting `State = Revoked`; rowcount 1 → `Revoked`; 0 → read: exists → `409 ownership-offer-not-pending` ("This offer is no longer open." as the problem title; the type is shared with 5.3); absent (or another Space's id) → `BoundaryProblems.NotAvailable()`, unrecorded, floored.
> - **Never deleted** (AD-27: "Rows are never deleted on expiry"; FR-8: retained). No `ExecuteDelete` on `OwnershipOffer` anywhere but the Space cascade (gate, AC12).
> - **Nothing else moves:** Roles before = after; S-2; no email (AC7); no `MembershipChanged`.
> - **Replay:** the same key returns the original `204`. A second revoke with a new key → `409 ownership-offer-not-pending`.
> - A time-expired offer (seeded past) revoked → `409` (it is not actionable). Record: that is the honest answer; the panel never shows it (AC9).

**AC5 — lapse by event, in the same transaction** (FR-8, FR-14, FR-3, AD-5, AD-27; ledger `:817`)
**Given** a pending offer
**When** the named recipient's Membership is removed, or their Account is deleted
**Then** the offer **lapses** inside that same transaction rather than being deleted or left orphaned, and no Role changes
**And** this is lapse *by event*, which is written — as distinct from lapse by the passage of time, which is not

> - **Where** (Q4 **[LEE]**): in `MembershipStore.RemoveCoreAsync` and `LeaveAsync`, beside `UnassignAsync`, **after** the lock and **before** the guarded `DELETE`, for a non-Owner target only:
>
>   ```
>   UPDATE dbo.[OwnershipOffer] SET State = N'Lapsed'
>   WHERE SpaceId = @space AND RecipientMembershipId = @membership AND State = N'Pending'
>   ```
>
>   Every `Pending` row naming it, actionable or time-expired, so no `Pending` row ever names a Membership that no longer exists. If the guarded `DELETE` then writes nothing, the endpoint answers a refusal and the unit of work rolls back the lapse with the unassign (4.5's behaviour; prove it: a refused removal leaves the offer `Pending`).
> - **One writer:** the statement lives in the offer's persistence code (`OwnershipOfferWrites.LapseNamingAsync(dbContext, spaceId, membershipId, ct)`, internal static, beside the offer store) and `MembershipStore` calls it, so the state-writer gate (AC12) names every sanctioned writer. Account deletion (5.4) calls the same method for each of the Account's Memberships (Q10; ledger to 5.4).
> - **Written, not joined:** deferred-work `:817` allowed "the actionable predicate joins the Membership, so no write is needed". AD-5, AD-27, AR-31 and this AC require the **written** lapse. Record the choice (C2).
> - **Cases** (Slices, S-2 after each): the Owner removes the recipient → offer `Lapsed`, Roles of everyone else unchanged; an Admin removes a Member recipient → `Lapsed`; the recipient leaves (`DELETE .../membership`) → `Lapsed`; a refused removal (an Admin on an Admin recipient, `403`) → offer still `Pending`; the removal of **another** Member → offer still `Pending`. After a lapse, a new offer is `201` (the index frees).
> - **Plants:** omit the lapse in the removal → the removal case goes red (`Pending` naming a vanished Membership); omit it in the leave → the leave case red.

**AC6 — an offer is not an exit** (FR-8, FR-14, FR-3, AD-5)
**Given** a pending offer
**When** the offering Owner attempts to remove their own Membership or delete their Account
**Then** both are still refused, because making an offer is not itself an exit

> - With a pending offer: the Owner's `DELETE .../membership` (leave) and `DELETE .../memberships/{ownId}` both answer `409 ownership-held`, unchanged; the offer stays `Pending`; S-2.
> - **Plant:** let the leave statement drop `Role <> 'Owner'` when an offer is pending → red.
> - **Account deletion does not exist** (5.4). Record the clause as met vacuously and owned by 5.4, whose AC already requires the refusal (`epics.md:2079-2082`).

**AC7 — exactly one email, in the same transaction** (FR-43, AD-26, AR-33, AD-10)
**Given** the offer
**When** it is created
**Then** exactly one email is enqueued through the existing outbox **in the same transaction as the insert** and dispatched in-process on enqueue
**And** nothing is emitted on revoke, decline or lapse

> - **Kind:** `OutboxKinds.OwnershipOffered => "ownership-offered"` (`Yello.Application/Outbox/OutboxEmail.cs`). `OutboxStore.EnqueueAsync` also writes the `NotificationSend` row; it throws outside a Space-scoped unit of work, which the offer is in.
> - **Handler** (`Yello.Application/Spaces/OfferOwnership/OfferOwnershipHandler.cs`): `store.OfferAsync(...)` → on `Offered`, `memberships.OwnershipOfferNoticeAsync(offerId/recipientMembershipId, ...)` returns `(RecipientAddress, OffererName)` from `MembershipStore` (4.6's `AssignmentNoticeAsync` precedent, so `AddressReaders` does not widen; Q7) → `spaceNames.ReadAsync` (null → `SpaceGone`, the boundary `404`) → `OwnershipOfferEmail.Render(...)` → `outbox.EnqueueAsync(new OutboxEmail(OutboxKinds.OwnershipOffered, address, subject, body))`.
> - **Same transaction proof** (`TESTING-CONVENTIONS.md:517-543`): an `AFTER INSERT` trigger on `IdempotencyRecord` that throws for the case's key (dropped in a `finally`) → no `OwnershipOffer` row, no outbox row, no `NotificationSend` row, no email; anchored by a later offer with another key that does send.
> - **Exactly one, to this address:** `mail.WaitForEmailsToAsync(recipient, IsOwnershipOfferEmail)` → one; the offerer receives none. A replay of the same key → still one email.
> - **Nothing on revoke or lapse:** after revoke, after a recipient removal (lapse) and after a leave (lapse): no further email to the recipient and no new `NotificationSend` row for the kind. Wait for the offer's own email first, then act, then assert the count is unchanged by recipient (4.7's review lesson: wait for your own mail, never "any N"). Decline is 5.3's; the revoke and lapse paths take **no `IOutbox`** dependency (gate `The_ownership_offer_revoke_and_lapse_take_no_outbox`, the `The_revoke_handler_takes_no_outbox` precedent).
> - **Shared-Host trap:** `ActiveSpaceEndpointTests` shares a pooled Host per class whose outbox dispatcher can claim seeded or enqueued rows and send them into the shared drop. Any case here that seeds the outbox, or reads mail through a `MailDrop`, starts with `SoleDispatcherWireAsync()` (`ActiveSpaceEndpointTests.IssueInvitationOutbox.cs:482`) and then runs its own `HostProcess.RunAsync(conn, mail.With(Configured), ...)`; it must not use the pool afterwards. Use a fresh recipient per email case (`InvitationWire.FreshAddress`).

**AC8 — what the email says, and does not** (FR-43, I-14, AD-23)
**Given** that email
**When** it is read
**Then** it names the Space, states that the recipient is being offered ownership of it, and identifies who offered it
**And** it discloses nothing about the Space's contents, its other Memberships, or any other Space — stated as a requirement because delivery is irreversible: a pending offer lapses when the recipient's Membership ends, but an email already sent cannot be recalled, so anything it carried outlives the Membership that justified carrying it
**And** it carries no means of accepting; acceptance happens inside the Space

> - **Template** (`Yello.Application/Spaces/OwnershipOfferEmail.cs`): `public static RenderedEmail Render(string spaceName, string offererName, Uri link, DateTimeOffset expiresAt)` — strings, a `Uri` and the expiry only; no Domain type can be passed in. Gate `The_ownership_offer_email_template_takes_the_Space_the_offerer_the_link_and_the_expiry_and_nothing_else` (the `InvitationEmail` gate's shape).
> - **Copy** (Q7; plain text, English, no exclamation marks, `EXPERIENCE.md:99`, `:110`):
>   - Subject: "{offerer} offered you ownership of {space} on Yello"
>   - Body: "{offerer} has offered you ownership of {space} on Yello." / "You stay as you are until you answer. To accept or decline, sign in to Yello and open {space}." / "The offer is open until {expiresAt:yyyy-MM-dd HH:mm} UTC." / the link (the client origin, **no ids**, the 4.6 `AssignmentLinks.Home` precedent: an id in a forwardable link would disclose existence).
>   - Not "Congratulations", not the Role change, not a count of anything (addendum `:70`: "(12 Projects, 340 Tasks, 8 Members)" leaks the Space's shape).
> - **No means of accepting:** no token, no accept route, no id in the body or link (assert no GUID of the Space, offer or Membership; no `"ownership-offers"`; no `"accept"` URL). The SPEC (`SPEC.md:200`) rules out state-changing GETs, so even a link to an accept route would be a defect.
> - **Disclosure case** (I-14; `ActiveSpaceEndpointTests.AssignTask.cs:438` is the shape): the Space holds a Project, a Task, a Label, a third Member and an Invitation, and the recipient also belongs to **another** Space with its own names. The email (`DroppedEmail.Everything`) contains the Space name and the offerer's name; contains none of: the Project, Task or Label names, the third Member's name or address, the Invitation address, the other Space's name, the recipient's or offerer's address, `Password`, `yello_pat_`, any GUID.
> - **Log:** no address, no body in any log line (`No_log_message_takes_an_address_a_token_or_an_email_body` stays green).

**AC9 — the Ownership panel** (UX-DR14, `EXPERIENCE.md:216`, `:263`, `:438`; ledger `:544`)
**Given** the Ownership panel in Space settings
**When** the Owner views a pending offer
**Then** it shows the named recipient and the expiry computed against the **server** clock, and offers revocation
**And** it is Owner-only, an Admin having no ownership affordance present

> - **Read** (Q3): `GET /api/v1/spaces/{spaceId:guid}/ownership-offers/pending` under `OfferOwnershipTransfer` → `200 OwnershipOfferResponse(Guid Id, Guid RecipientMembershipId, string RecipientName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt)` or `204` when no **actionable** offer exists (`Where(OfferPredicates.OwnershipOfferActionable)`; a time-expired row reads as none). `RecipientName` is the recipient's current display name read in `MembershipStore` as the Members list reads it (never the address; the address gates hold). `ReadAt` is `SYSDATETIMEOFFSET()` from the same statement (Q11).
> - **Server clock** (`EXPERIENCE.md:263`, `:438`, AD-27): the client computes "N days left" as `ExpiresAt - ReadAt`, **never** from the browser clock. Gate: no `DateTime.Now`, `DateTimeOffset.Now`, `UtcNow` or `TimeProvider` in the Ownership section's files.
> - **Client** (`Yello.Client/Components/OwnershipSection.razor`, Q8):
>   - Section heading "Ownership", in `SpaceSettings.razor` after Invitations and before Delete (`SectionKey.Ownership`), gated by `OwnershipCapabilities.PermitsOfferOwnership(role)` (owner only). Name the predicate so its token is unique for `ClientRolePredicate` (`RefusalBoundaryGateTests.Spaces.cs:348` matches by substring).
>   - **No offer:** a recipient picker listing the Space's Memberships other than the Owner's, by disambiguated label (`MembershipLabels`, never the address), with **no default selection** (`EXPERIENCE.md:211`), and an "Offer ownership" button. Pressing it replaces the form in place with a confirm: "Offer ownership of {space} to {label}? You stay the Owner until they accept. They will be emailed." with "Offer" (`button-primary`) and "Cancel"; neither pre-focused (focus to the confirm text, the `InvitationRow` precedent). One `Idempotency-Key` per deliberate "Offer", kept for a resend after no answer.
>   - **Pending:** "Ownership offered to {label}. Waiting for their answer." and "Expires in {n} days · {local date and time}" (`<time datetime="O">`), with a "Revoke" control (no confirm: revoking is reversible by a new offer and is not on the destructive ladder, `EXPERIENCE.md:220`; record it). On `204` revoke: back to the empty form, announce "Ownership offer revoked." (no name), focus to the picker.
>   - **Answers:** `409 ownership-offer-pending` → re-read and show the pending offer; `409 ownership-offer-not-pending` on revoke → re-read; `400 invalid-offer-recipient` → "Ownership can be offered only to another Membership of this Space." and re-read the picker; `403` → `OnRoleDoubted` (4.7's `SpaceArrival.Reread`); `404` → the Space refusal surface.
>   - **Absent for an Admin** (and Member, Viewer): no section, no heading, no `disabled` control (UX-DR27). `PermitsSpaceSettings`' union gains the predicate; the link's audience is unchanged (Owner and Admin).
> - **Client proofs** (`Yello.Tests.Client`, a new `OwnershipSectionTests.cs` on the existing collection; `FakeYelloApi.OwnershipOffers.cs` using contract constants only): the Owner sees the section; an Admin sees none (absent from the DOM); the picker has no selected option; the confirm pre-focuses nothing; the send carries one key; the pending state shows the label and "Expires in 6 days" from a fake `ReadAt`/`ExpiresAt` pair whose difference is 6 days while the **browser clock is set far away** (Playwright `Clock`), proving the server clock is used; no `@` in the section; Revoke returns to the form with focus off `body`; 320×256 reflow and `TextSpacingTests`.

**AC10 — the expiry, on the database clock** (FR-8, AD-27, AR-31; `prd.md:225`)

> - `ExpiresAt` is stamped by a column default `DATEADD(day, 7, SYSDATETIMEOFFSET())` (`DatabaseClock.FunctionName`), `ValueGeneratedOnAdd`, as `InvitationConfiguration` does (`ExpiryDays = 7`, its own constant on the offer configuration). Never a C# clock. `Assumption = "PRD-12-3"` on the expiry cases.
> - **The shared predicate:** `OfferPredicates.OwnershipOfferActionable` **in `Yello.Infrastructure/Persistence/OfferPredicates.cs`** — `o => o.State == OwnershipOfferState.Pending && o.ExpiresAt > DatabaseClock.Now()`. The gate `Only_the_shared_actionable_predicate_compares_ExpiresAt` allows exactly one file to compare `ExpiresAt` (plus `SpacePresenceRegister.cs`), so it **must** live in that file. AC3's supersede composes its negation from the same file. The sibling gate bans C# clocks there.
> - **Cases:** a fresh offer's `ExpiresAt` is within a minute of `SYSDATETIMEOFFSET() + 7 days` read from the database; a row seeded with `ExpiresAt` in the past is not listed by the read (`204`) and is not revocable (`409`).

**AC11 — a Space deletion takes its offers** (ledger `:705`; AD-5)

> - `FK_OwnershipOffer_Space_SpaceId` **cascades** (the Invitation precedent, Lee's 4.1 Q1). `SpaceStore.DeleteCoreAsync` stays one statement.
> - Extend `ActiveSpaceEndpointTests.The_Owner_deletes_a_populated_Space_and_every_row_hanging_from_it_goes_with_it`: add `"OwnershipOffer"` to its `SpaceScopedTables` and seed a pending offer in `PopulateSpaceAsync` (through the product route). Before > 0, after = 0.
> - Add a dated note to AD-5 in `ARCHITECTURE-SPINE.md`: a Space deletion cascades its offers (the Space and every Membership the offer could name go with it, so nothing is orphaned and no lapse is meaningful); a removal or leave **writes** the lapse (AC5).

**AC12 — the gates** (AD-21, AD-26, AD-27, I-1..I-3)

> - **Schema** (`SchemaNames`, `SpaceIsolationSchemaTests`, `PersistenceModelGateTests`):
>   - `SchemaNames.OwnershipOfferTable` appended to `SpaceScopedTables`; RLS in the migration: `ADD FILTER PREDICATE`, `ADD BLOCK PREDICATE ... AFTER INSERT`, `... AFTER UPDATE` on `dbo.[OwnershipOffer]` (the `20261101140742_Invitations` shape; dropped first in `Down`). Add it to `TablesThatCannotMove` (3 predicates).
>   - Query filter exactly `SpaceId == context.ScopedSpaceId`; non-nullable `SpaceId`; `Id` `ValueGeneratedNever()`; singular table name.
>   - **No FK to `Membership` and none to `Account`** (Q2 **[LEE]**): a retained row naming a removed Membership would fail a NO ACTION key (`547`), CASCADE would delete the record AD-5 says to keep, and SET NULL would null `SpaceId` (the `TaskConfiguration.cs:117-126` reasoning). `Every_reference_between_Space_scoped_entities_carries_SpaceId` is unaffected (no reference). The configuration's remarks say why; a schema case asserts the table's only foreign key is to `Space`.
>   - Add `OwnershipOfferConfiguration` to `Space_is_read_only_where_named`'s permitted list; add `Yello.Domain.Spaces.OwnershipOffer` to `RefusalBoundaryGateTests.SpaceScopedEntities`. Apply the configuration through a model helper (`YelloDbContext` is at S1200's limit; `InvitationModel.Apply` precedent).
> - **State writers** (`Only_the_Invitation_store_writes_an_Invitation_state` is the Cecil shape): new `Only_the_offer_store_writes_an_OwnershipOffer_state` — the insert of `Pending`, the revoke (`Revoked`), the supersede (`Lapsed`) and `LapseNamingAsync` (`Lapsed`) are the only writers; no `ExecuteDelete` on `OwnershipOffer`; no `set_State` outside the store. 5.2/5.3 extend it (ledger `:744`'s `OwnershipOffer` half stays 5.3's).
> - **Capability:** `Every_spaceId_endpoint_declares_exactly_one_capability` (three routes, each `OfferOwnershipTransfer`); a new `The_ownership_offer_routes_require_OfferOwnershipTransfer_and_serve_both_surfaces`. Add `new MatrixRoute(Capability.OfferOwnershipTransfer, "POST", OwnershipOfferRoutes.Offers, 201)` (or the read's `200`) to `ActiveSpaceEndpointTests.RoleMatrix.cs` (`:51-61`) so `The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare` stays green and the matrix theory proves Admin/Member/Viewer `403` against product-joined Memberships.
> - **Authority-name trap:** `Only_the_pipeline_sets_the_acting_authority` and `A_Qualified_cell_reaches_no_endpoint_without_its_target_rule` `Assert.Single` over non-nested `Yello.Application` types whose name contains `"Authority"`. **Do not** name any new Application type `...Authority...`.
> - **Problem types** (`Yello.Contracts/ProblemTypes.cs`): `OwnershipOfferPending` (`urn:yello:problem:ownership-offer-pending`), `OwnershipOfferNotPending` (`...:ownership-offer-not-pending`), `InvalidOfferRecipient` (`...:invalid-offer-recipient`), each with a `ClientCopy.resx` entry keyed by the bare code with a non-empty `<comment>` and a gate (`The_ownership_held_problem_type_is_declared_and_has_client_copy` shape). The capability refusal copy `offer-ownership-transfer` already exists (`ClientCopy.resx:502`).
> - **Contracts:** `OwnershipOfferRoutes` (`Offers`, `OffersOf`, `Pending`, `PendingOf`, `Offer`, `OfferOf`) in `Yello.Contracts/Spaces/`; contract-shape gate pinning `OfferOwnershipRequest` and `OwnershipOfferResponse` to exactly their members (no address).
> - **Email kind:** extend `The_assignment_email_has_its_own_outbox_kind`'s sibling: the three kinds are distinct.

**AC13 — isolation rows on both surfaces, and races** (AD-3, AD-4, I-1..I-3, I-12, I-13; `TESTING-CONVENTIONS.md:773-895`)

> - New `tests/Yello.Tests.Isolation/IsolationCases.OwnershipOffers.cs`, spread into `All`. Every row states both surfaces:
>   - `ISO-OFFER-OWNERSHIP-FOREIGN-SPACE`, `ISO-READ-OWNERSHIP-OFFER-FOREIGN-SPACE`, `ISO-REVOKE-OWNERSHIP-OFFER-FOREIGN-SPACE`: boundary `404`, byte-identical, recorded `CrossSpace`; then check no offer exists in either Space.
>   - `ISO-RECORD-KIND-OFFER-OWNERSHIP-ADMIN`, `-MEMBER`, `-VIEWER`: `RoleRefused("offer-ownership-transfer")`, recorded `InsufficientRole`; the lesser Role joined through acceptance.
>   - `ISO-OFFER-OWNERSHIP-FOREIGN-MEMBERSHIP`: another Space's `MembershipId` in one's own Space → `400 invalid-offer-recipient`, identical to a random id (`SameAsAnchor(400, ExpectedType: ...)`), unrecorded, and no row.
>   - `ISO-REVOKE-FOREIGN-OFFER-ID-IN-OWN-SPACE`: another Space's offer id → the in-Space not-found, byte-identical to the boundary, unrecorded; the foreign offer stays `Pending`.
>   - `ISO-TOKEN-OWN-OTHER-SPACE-OFFER-OWNERSHIP`, `-READ-OWNERSHIP-OFFER`, `-REVOKE-OWNERSHIP-OFFER` (the 3.4 gate needs one per route a Token reaches).
>   - The actor's secrets hold every seeded address and display name (`HoldsSecret`), so the I-17 log check fails on an address in the Host log.
>   - Rows created `PendingUntil: "5.1"` by ATDD must all be live before review (GATE-003).
> - I-14 (the email) is proved in Slices (AC8), not Isolation: the isolation world shares one Host and one drop (4.6 precedent). Record it.
> - **Races** (`WaitUntilBlockedOnALockAsync`, every racing Account made present and holding an anti-forgery token first):
>   - **Race 1, offer vs removal of its recipient:** hold `Yello/Memberships/{spaceId}`; send the Owner's offer to M and an Admin's removal of M; both block; release. Serial outcomes only: offer first → removal lapses it (`Lapsed`, no `Pending` naming a vanished row); removal first → offer `400 invalid-offer-recipient`. Never a `Pending` row naming a removed Membership. Plant: the offer skips the lock → the deciding "both waited" assertion red (or recorded masked).
>   - **Race 2, two offers:** hold the lock; two offers (two recipients, two keys); both block; release → one `201`, one `409 ownership-offer-pending`; one `Pending` row.
>   - **Race 3, revoke vs leave of the recipient:** both under the lock (the revoke takes it too, Q5) → `Revoked` then leave `204`, or `Lapsed` then revoke `409`. Either serial outcome; never both written.
> - **Idempotency:** a replay returns the original `201` with the same body and enqueues nothing; the same key with a different recipient → `422`; malformed → `400` (`Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay` stays green).

## Tasks / Subtasks

- [ ] **Task 0: decisions** (before any code)
  - [ ] Q1, Q2, Q4 and Q5 are marked **[LEE]** (authority, isolation or a security seam). If the orchestrator routes them to Lee, record the answers here under "Decided by Lee" before ATDD. Otherwise take each recommendation and record it as "Qn - (x)" with its cost in Completion Notes.
  - [ ] Decide Q3 and Q6-Q12 the same way. Each recommendation is the default.
- [ ] **Task 1: Domain** (AC1, AC4, AC5)
  - [ ] `Yello.Domain/Spaces/OwnershipOffer.cs`, `OwnershipOfferState.cs` (five states). Remarks quote FR-8 and AD-26 briefly; **no story ids** (CLAUDE.md).
  - [ ] Update `Capability.OfferOwnershipTransfer`'s summary to say what exists, if it predicts the offer.
- [ ] **Task 2: Contracts** (AC1, AC3, AC4, AC9, AC12)
  - [ ] `OwnershipOfferRoutes`, `OfferOwnershipRequest(Guid? MembershipId)`, `OwnershipOfferResponse(Id, RecipientMembershipId, RecipientName, ExpiresAt, ReadAt)`.
  - [ ] `ProblemTypes.OwnershipOfferPending`, `OwnershipOfferNotPending`, `InvalidOfferRecipient`, with remarks on status and disclosure.
- [ ] **Task 3: Application** (AC1, AC4, AC7)
  - [ ] `Yello.Application/Spaces/OfferOwnership/OfferOwnershipHandler.cs`, `RevokeOwnershipOffer/RevokeOwnershipOfferHandler.cs` (no `IOutbox`), `ReadOwnershipOffer/ReadOwnershipOfferHandler.cs`; `IOwnershipOfferStore` with outcome types (`Offered`, `AlreadyPending`, `InvalidRecipient`, `LostMembership`; `Revoked`, `NotPending`, `NotFound`).
  - [ ] `IMembershipStore.OwnershipOfferNoticeAsync` (recipient address, offerer name). `OutboxKinds.OwnershipOffered`. `OwnershipOfferEmail.Render`. A link source with no ids (reuse `AssignmentLinks.Home` or a sibling).
  - [ ] Handlers read no Role, count nothing and record nothing (AR-3). **No type named `...Authority...`.**
- [ ] **Task 4: Infrastructure** (AC1, AC3-AC5, AC10-AC12)
  - [ ] `OwnershipOfferConfiguration` (index, column default, cascade from Space, no other FK, string state), applied through a model helper; `SchemaNames` additions.
  - [ ] Migration `OwnershipOffers` (`dotnet tool restore`, then `dotnet ef migrations add`): table, index, RLS predicates in `Up`, dropped first in `Down`; fix the scaffold by hand (`TESTING-CONVENTIONS.md:582-602`).
  - [ ] `OfferPredicates`: the offer's actionable predicate (same file).
  - [ ] `OwnershipOfferStore`: offer (lock, supersede, guarded insert, `2601` → `AlreadyPending`), revoke (lock, guarded `ExecuteUpdate`, disambiguation), read (actionable, `ReadAt` from the database). `OwnershipOfferWrites.LapseNamingAsync`.
  - [ ] `MembershipStore`: call `LapseNamingAsync` in `RemoveCoreAsync` and `LeaveAsync` beside `UnassignAsync`; `OwnershipOfferNoticeAsync`; the recipient's display name for the read. Watch S1200 and split by partial if it fires.
- [ ] **Task 5: Host** (AC1, AC3, AC4, AC9)
  - [ ] `OwnershipOfferEndpoints`: `MapOfferOwnership`, `MapReadOwnershipOffer`, `MapRevokeOwnershipOffer`, each `.RequiresCapability(Capability.OfferOwnershipTransfer)`, explicit outcome switches with `_ => throw` (never a fall-through to success or `404`). `LostMembership` → `new MembershipLost()`. Register handlers and map in `Program.cs` beside the Membership routes with the route comment block.
- [ ] **Task 6: Client** (AC9)
  - [ ] `OwnershipSection.razor` (+ row/confirm as needed), `OwnershipCapabilities.PermitsOfferOwnership`, `SpaceCapabilities.PermitsSpaceSettings` union, `SpaceSettings.razor` (`SectionKey.Ownership`), `RefusalCopy`, `ClientCopy.resx`, `components.css` (logical properties, tokens only).
  - [ ] Rules: no storage, no mutable static, every `NavigateTo` via `SpaceNavigation.GoTo`, copy in `ClientCopy.resx` with `<comment>`, no Razor `else` around user-visible text, no `disabled` on a Role-reason control, no browser clock.
- [ ] **Task 7: tests and plants** (all ACs). See Testing requirements. Save each original in memory and restore it in a `finally`. **Never** `git checkout --`, `git restore` or `git reset`. Record `| Plant | Expected red | Seen |` in the Debug Log. After any killed run, grep for plant markers.
- [ ] **Task 8: ledger, docs, verification**
  - [ ] Apply the ledger dispositions below: one line per CLOSED/UPDATED/RE-OWNED note, no TAB or FORM FEED, keep the final newline.
  - [ ] Dated notes in `ARCHITECTURE-SPINE.md`: AD-5 (Space deletion cascades offers; removal/leave write the lapse), AD-26 (no FK to Membership, Q2; the supersede of a time-expired row, Q1), AD-27 (the supersede is a request-path write caused by a new offer, not a scheduled one).
  - [ ] Rewrite comments that predict "5.1" (e.g. `OutboxEmail.cs` "story 5.1's Ownership Offer reuses it", `OfferPredicates.cs` "5.1-5.3's Ownership Offers") so they say what exists.
  - [ ] `sprint-status.yaml`: this story goes to `review` at the end of dev. `epic-5` stays `in-progress`.
  - [ ] Verification follows CLAUDE.md. `docker info --format '{{.ServerVersion}}'`, `dotnet build Yello.slnx` (0/0), iterate with `--filter-class`, then **exactly one** full run in the background without the browser suite: `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`, logged to `artifacts/test-runs/5-1-dev.log`. Record totals. Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

An offer is 4.1's Invitation turned inward: a row with a 7-day `ExpiresAt` stamped by the database, a state that only guarded statements write, and one email through the outbox in the same transaction. The difference is that it names a **Membership**, so it is inserted by one statement that proves the recipient is a non-Owner Membership of this Space and the caller is its Owner, under the Space's Memberships lock that every Membership write already takes. A filtered unique index on `Pending` makes a second offer impossible, and the store turns that `2601` into a `409`. A time-expired pending row would hold that index forever, because nothing writes expiry, so a new offer first marks it `Lapsed` in the same transaction. Revoke is 4.2's guarded `UPDATE`. The removal and the leave (4.5) write `Lapsed` on any pending offer naming the departing Membership before they delete it, and roll it back with the unassign if they are refused; a Space deletion cascades the offers away. Ownership never moves and no Role changes in this story: that is 5.2's swap. The Owner sees an Ownership section in Space settings: a picker with no default, an in-place confirm, then the pending offer with its recipient, its expiry counted from the server's own clock, and Revoke. An Admin sees nothing.

### What exists today (survey at `2251d84`)

- **Domain:** `Capability.OfferOwnershipTransfer` and its matrix row `[Yes, No, No, No]`; `Membership.Role` has a setter for Epic 5; `RoleChangeAuthority`, `RemovalAuthority` (Owner in no set); no `OwnershipOffer` type.
- **Application:** `ActingAuthority` (`RoleChanges`, `Removals`; set only in `ActiveSpaceResolution.Authority.cs`); `IOutbox`, `OutboxEmail`, `OutboxKinds { InvitationIssued, TaskAssigned }`; `IActiveSpaceNameReader`; `IMembershipStore` (`RemovalOutcome`, `AssignmentNotice`); `InvitationEmail`/`AssignmentEmail` templates.
- **Infrastructure:**
  - `OutboxStore` (writes `NotificationSend` + `OutboxMessage`, `SaveChanges`, `signal.MarkEnqueued()`; throws outside a Space-scoped unit of work), `OutboxDispatcher` (claims with `READPAST`, no timer), `OutboxCommitSignal`, `CommitHooks`.
  - `OfferPredicates.Actionable` (Invitation only); `DatabaseClock`.
  - `InvitationConfiguration` (`ExpiresAt` default `DATEADD(day, 7, SYSDATETIMEOFFSET())`, cascade from Space, `Restrict` to the issuing Account); `InvitationModel.Apply`.
  - `InvitationStore` (guarded revoke shape).
  - `MembershipStore.RemoveCoreAsync`: lock → `TargetAsync` → `UnassignAsync` (non-Owner) → `GuardedDeleteAsync` → `RemovedAsync` or judge. `LeaveAsync`: lock → own row → `UnassignAsync` → `DELETE ... Role <> 'Owner'`. `MembershipStore.Assignees.cs` (`UnassignAsync`, `AssignmentNoticeAsync`). `ChangeRoleCoreAsync` catches `2601/2627`.
  - `ApplicationLocks` (`TakeMembershipsAsync` Exclusive, `TakeMembershipsSharedAsync`, `TakeInvitationsAsync`), `sp_getapplock` owned by the transaction.
  - `SpaceStore.DeleteCoreAsync` (one `ExecuteDelete`, cascades). `SchemaNames.SpaceScopedTables`. Latest migration `20261108094508_TaskAssignee`.
- **Host:** `MembershipRemovalEndpoints` (`409 ownership-held`), `InvitationEndpoints` (revoke's `409 invitation-not-pending`), `TaskAssigneeEndpoints` (`400 invalid-assignee`), `ActiveSpaceResolution.Idempotency.cs`, `MapApiGroup`, `SpaceScopedRouteCheck`.
- **Client:** `SpaceSettings.razor` (`SectionKey { Name, Labels, Memberships, Invitations, Delete }`), `MembershipSection`/`MembershipRow`, `InvitationSection`/`InvitationRow` (in-place confirm, focus to text), `SpaceCapabilities.PermitsSpaceSettings` (union), `MembershipCapabilities`, `ContextBar` (Role chip), `ClientCopy.resx` (`offer-ownership-transfer` refusal copy exists at `:502`).
- **Tests:** `MailDrop` (`WaitForEmailsToAsync`), `SoleDispatcherWireAsync` (`IssueInvitationOutbox.cs:482`), `WireDatabase` (`SpacesNotHoldingExactlyOneOwnerAsync`, `MembershipRowsInAsync`, `AddOutboxRowAsync`), `JoinedThroughTheProductAsync`, `ActiveSpaceEndpointTests.RoleMatrix.cs`, `IsolationCases*.cs` (no offer rows), `RefusalBoundaryGateTests*.cs`, `PersistenceModelGateTests`, `SpaceIsolationSchemaTests`.

### What must be preserved (regression guard)

- **4.5's removal and leave**: answers, races, S-2, the Owner's `409 ownership-held`, the unassign-then-delete order, `MembershipChanged` once per removal. The lapse is one more statement in the same transaction, nothing else.
- **3.3's Space deletion** stays one statement; the new cascade must not create a second cascade path (there is no FK from the offer to `Membership`, so none).
- **The outbox**: the invitation and assignment emails, `NotificationSend` per enqueue, dispatch only on commit/start/piggyback. No timer (`No_production_code_runs_on_a_timer_or_as_a_hosted_service`).
- **Both Role-read gates stay unwidened**; the offer reads no Role in a slice (the Owner check is in the guarded statement, which is persistence, as 4.5's actor clause is).
- **Address confinement**: `AddressReaders`/`DisplayNameReaders` do not widen (the notice and the name read stay in `MembershipStore`); no address in any response, log, idempotency record, or the panel.
- **The boundary `404` stays byte-identical** (`RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal`, `SameAsAnchorAndStranger`).
- **`IdempotencyStore` refuses to save with other changes pending**: `OutboxStore` saves before the pipeline's record; keep the offer's own save before the enqueue.

### Locking and the races (RCSI)

- The offer and the revoke take `Yello/Memberships/{spaceId}` **Exclusive** first (Q5), then the supersede and the guarded statement. Lock order: application lock, then rows. No `HOLDLOCK`.
- The removal and the leave already hold that lock when they write the lapse, so an offer and a removal of its recipient serialise (Race 1). The 5.2 swap will take the same lock (ledger `:792`).
- The read takes no lock (a pure projection under the Space key).
- The filtered index is the last line: if anything ever bypasses the lock, the second insert fails `2601`, mapped to `409`.

### Open questions

Each has a recommendation, which is the default. **[LEE]** marks a question whose answer touches authority, isolation, a security seam or a binding prior decision. The orchestrator may route those to Lee; otherwise the dev phase decides them like the rest, as reversible, recorded decisions.

1. **[LEE] A time-expired pending row holds the filtered index** (AC3; C1). AD-27 writes no expiry, so a row past `ExpiresAt` keeps `State = Pending`, and `UX_OwnershipOffer_SpaceId_Pending` would refuse every later offer in that Space.
   - **(a) Recommended.** The offer's transaction, under the lock, writes `Lapsed` on this Space's `Pending` rows that fail the shared predicate, before the insert. A request-path write caused by the new offer; no job, no timer. Dated notes on AD-26/AD-27. Cost: "lapsed" now has three causes (removal, leave, superseded-after-expiry), all written by an event.
   - (b) Write `Revoked` instead. Cost: the Owner did not revoke it; the record lies.
   - (c) Refuse a new offer until someone writes the row. Cost: nothing ever does; the Space is stuck. Not viable.
2. **[LEE] What the offer row references** (AC1, AC11, AC12).
   - **(a) Recommended.** `RecipientMembershipId` and `OfferedByMembershipId` as plain columns, **no FK** to `Membership` or `Account`; FK to `Space` only (cascade). The recipient's existence is proved by the guarded insert under the lock, and the lapse is written before the Membership goes. `OffererName` captured at authoring time. Cost: referential integrity for retained rows rests on the statements, recorded in AD-26 (as AD-5's "at least one Owner" rests on S-2).
   - (b) Composite FK `(SpaceId, RecipientMembershipId) → Membership(SpaceId, Id)`, NO ACTION, with a nullable column that the removal nulls when it lapses. Cost: the retained record forgets whom it named; every later state would need the same nulling, including Accepted offers when that Owner later transfers on and is removed.
   - (c) CASCADE from `Membership`. Cost: deletes the record AD-5 says to keep, and adds a second cascade path from `Space` (error 1785). Not viable.
3. **Routes and shapes** (AC1, AC4, AC9). **(a) Recommended:** `POST .../ownership-offers` → `201`; `GET .../ownership-offers/pending` → `200`/`204`; `DELETE .../ownership-offers/{offerId}` → `204`; all under `OfferOwnershipTransfer`, both surfaces. 5.2/5.3 add the recipient's read and `.../{offerId}/acceptance`, `/decline` under the identity rule. (b) A singular `.../ownership-offer` resource. Cost: revoke without an id can revoke an offer the panel never showed.
4. **[LEE] Lapse on removal: written, and where** (AC5; ledger `:817`; C2).
   - **(a) Recommended.** Written, by `OwnershipOfferWrites.LapseNamingAsync`, called from `RemoveCoreAsync` and `LeaveAsync` before the guarded `DELETE`, for every `Pending` row naming the Membership (actionable or not); rolled back with the unassign when the removal is refused. 5.4 calls the same method.
   - (b) Join the Membership in the actionable predicate, so no write. Cost: contradicts AD-5 and AR-31 ("written inside the causing transaction") and leaves `Pending` rows naming vanished Memberships, which also hold the index.
5. **[LEE] The offer's guard and lock** (AC1, AC13).
   - **(a) Recommended.** One guarded insert proving (recipient is a non-Owner Membership of this Space) and (caller is this Space's Owner) in its `WHERE`, under `TakeMembershipsAsync` Exclusive; the revoke takes the same lock. The capability is the pipeline's; the statement's Owner clause is the in-flight re-check (4.4/4.5's actor clause), never a Role read in the slice.
   - (b) `TakeMembershipsSharedAsync` (assignment's mode). Cost: two concurrent offers then race to the index instead of queuing; correct, but the `2601` path becomes the normal one.
   - Sub-question: an Owner offering to **itself**. Recommended: `400 invalid-offer-recipient` (`recipient.Role <> 'Owner'`), the matrix's "the Owner cannot accept one, because the Owner is who makes it" (`role-capability-matrix.md:28`).
6. **Answers and problem types** (AC1, AC3, AC4). **(a) Recommended:** `400 invalid-offer-recipient` for an unknown, foreign or own-Owner recipient (one body, the 4.6 precedent: an id in a body that matches nothing is a bad request, and a single answer discloses nothing); `409 ownership-offer-pending`; `409 ownership-offer-not-pending` (title "This offer is no longer open.", shared with 5.3). (b) The in-Space `404` for an unknown recipient. Cost: diverges from `invalid-assignee` for the same situation.
7. **Email** (AC7, AC8). **(a) Recommended:** kind `ownership-offered`; `OwnershipOfferEmail.Render(spaceName, offererName, link, expiresAt)`; the copy in AC8; the link is the client origin with no ids; the notice read in `MembershipStore` (no gate widening). Include the expiry, because FR-43 says the email exists "to bring the recipient back before the offer expires". (b) Omit the link. Cost: the recipient must find Yello on their own; a link with no id authorises and discloses nothing.
8. **The panel** (AC9; ledger `:544`). **(a) Recommended:** section "Ownership" after Invitations, before Delete; picker with no default; in-place confirm before sending; pending state with Revoke and no revoke confirm. (b) Place it above "Space name" as the mockup's order suggests. Cost: the code's order differs from the mockup already (Name is first); record it either way.
9. **The Offer indicator ledger entry** (ledger `:664`). **(a) Recommended:** RE-OWN to **5.2**, which builds the indicator (`epics.md:1983-1986`, UX-DR9, `epics.md:376` maps UX-DR9 to 5.2). (b) Build an indicator now with nothing to open. Cost: the recipient's read and dialog do not exist until 5.2.
10. **Account deletion arms** (AC5, AC6). **(a) Recommended:** met vacuously and recorded; 5.4 calls `LapseNamingAsync` for each of the Account's Memberships inside its deletion transaction, and proves "the offering Owner's Account deletion is still refused". Add a ledger entry owned by 5.4. (b) Build an Account-deletion stub. Cost: scope creep.
11. **Server-clock expiry on the client** (AC9). **(a) Recommended:** the read returns `ReadAt` (`SYSDATETIMEOFFSET()` in the same statement); the client shows `ExpiresAt - ReadAt` in whole days ("Expires in {n} days", "Expires in 1 day", "Expires today" below 24 hours) plus the absolute local time. (b) Return `RemainingSeconds`. Cost: the absolute time still needs `ExpiresAt`; two fields either way.
12. **Revoke by an Admin** (AC9). Recommended: no route access (`OfferOwnershipTransfer` is Owner-only), no section, the recorded `403` at the API (`ISO-RECORD-KIND-OFFER-OWNERSHIP-ADMIN` covers the offer; add the revoke's twin). Recorded, not a choice.

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. AD-26's index versus AD-27's unwritten expiry.** Together they let a time-expired offer block every later offer. No source resolves it. Q1.
- **C2. `deferred-work.md:817` versus AD-5/AR-31.** The ledger allows a predicate that joins the Membership "so no write is needed"; the spines and this AC require a written lapse. Q4 takes the written form.
- **C3. `SPEC.md:274` and `acceptance-criteria.md:81`** say the offer "is surfaced in Space settings rather than emailed", and the SPEC has no CAP-43. FR-43 (PRD), AD-26 and this story's AC7 require the email. The PRD, spine and epics win; record the SPEC divergence (`SPEC.md:22` asks for a divergence report) in Completion Notes.
- **C4. `EXPERIENCE.md:544` (UJ-8)** says the email names "the Role change offered"; FR-43 says it "states that the recipient is being offered ownership". The PRD wins (AC8's copy).
- **C5. The Offer indicator's owner.** Ledger `:664` says 5.1; the epics place it in 5.2. Q9.
- **C6. AC5 and AC6 name Account deletion**, which is 5.4's. Q10.
- **C7. Mockup section order** (Ownership after Invitations, before "Space name") versus the code (Name first). Q8.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:544` → **CLOSED for 5.1**: the Ownership section exists (`OwnershipSection.razor`, Owner only, `SectionKey.Ownership`); the link's audience is unchanged. Remaining owner: 6.3.
- `:664` → **RE-OWNED to 5.2** (Q9): the indicator is 5.2's AC1.
- `:705` → **CLOSED by 5.1**: `FK_OwnershipOffer_Space_SpaceId` cascades; the populated-Space case covers `OwnershipOffer`; AD-5 note.
- `:817` → **CLOSED by 5.1** (removal and leave arms, written; Q4), citing the cases. The Account-deletion arm → new entry for 5.4.
- **New entries:**
  - "Account deletion must lapse pending offers naming each of its Memberships via `OwnershipOfferWrites.LapseNamingAsync`, and is refused for an offering Owner" (owner **5.4**; low).
  - "`OwnershipOffer` has no FK to `Membership`; retained rows' integrity rests on the guarded insert and the written lapse" (owner **Lee**; low; recorded, not a defect), if Q2 (a).
  - "The SPEC says the offer is not emailed (`SPEC.md:274`, `acceptance-criteria.md:81`); FR-43 says it is" (owner **PM/Lee**; low), C3.
  - "The offer email's copy is the story's recommendation" (owner **UX**; low).

### Architecture compliance (must)

- **AD-21 ring.** Domain type in `Yello.Domain/Spaces`; handlers in `Yello.Application/Spaces/<Slice>/`; the store, predicate and migration in Infrastructure; endpoints in Host; contracts in Contracts. Check `AllowedReferenceEdges.cs`.
- **AR-3 / AD-1.** Slices never read a Role, set a key, record idempotency or refusals.
- **AD-2.** No raw SQL outside Infrastructure; every statement carries `SpaceId = @space` as well as relying on the key.
- **AD-3 / AD-20.** Every not-found is the boundary body; only the pipeline records.
- **AD-10.** No timer, no hosted service, no polling. The supersede and the lapse are request-path writes.
- **AD-18.** Every write route accepts an `Idempotency-Key`.
- **AD-23.** No address in a log, a response, an idempotency record or the panel.
- **AD-26.** Guarded `WHERE State = Pending` plus rowcount on every transition; `409` with a stable type, never `404`, for a non-pending offer the Owner can see.
- **AD-27.** One predicate, one file, the database clock, in the statement's own `WHERE`.
- **AR-34.** Guid ids generated by `IIdentifierGenerator` (never the database), `DateTimeOffset` UTC, problem+json.
- **Code comments** (CLAUDE.md): why, never "added by story 5.1", no AC or Q ids, no history; no XML doc that restates the member name.

### Library / framework

No new package. EF Core 10.0.11 (`ExecuteUpdateAsync`, `Database.ExecuteSqlAsync` for the guarded insert and the lapse, LINQ projections for reads; `dotnet-ef` 10.0.11 from `.config/dotnet-tools.json` for the migration), ASP.NET Core 10.0.11 minimal APIs (`MapPost`, `MapGet`, `MapDelete`), Blazor WebAssembly 10.0.11, Microsoft.Data.SqlClient 7.0.2 (`SqlException.Number` 2601/2627), SQL Server 2025 (filtered unique index, RLS predicates, `SYSDATETIMEOFFSET`), xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0 (`Page.Clock` for the browser-clock case), ArchUnitNET 0.13.3 and Mono.Cecil for the IL gates.

No web research was needed: every API used is already in the codebase.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md` (`:44-73` traits, `:87-207` rules, `:517-543` outbox, `:545-580` Memberships, `:582-602` migrations, `:773-895` isolation):

- Traits: `Suite`, `Priority`, `Requirement` (FR-8, FR-43, AD-26, AD-27, AD-5, AR-12, AR-31, AR-33, UX-DR14, I-14, S-2), `Assumption` (`PRD-12-3` on every case resting on the 7-day expiry; `PRD-12-4` where an Admin is refused on an Admin), `Tier`. Underscore-sentence names.
- Never `Task.Delay`. Races wait on `LCK_M_%`; racing Accounts are made present and fetch an anti-forgery token first.
- **Never a shared email address** (`InvitationWire.FreshAddress`); a fresh recipient per email case; wait by recipient.
- **Shared-Host trap:** in `ActiveSpaceEndpointTests`, any case that seeds the outbox or waits on mail calls `SoleDispatcherWireAsync()` first and runs its own `HostProcess.RunAsync` with a `MailDrop`; it does not use the pool afterwards. Cases that read the Host log (no `MembershipChanged`) also use their own process.
- **Seed through the product** where the case is about Membership (accepted Invitations). Seed directly only for time-expired rows (R12) and the schema cases. Record which.
- **S-2 after every offer, revoke, lapse and race case.**
- **No new container-backed test class.** Slices: partials `ActiveSpaceEndpointTests.OfferOwnership.cs`, `.OfferOwnershipOutbox.cs`, `.OfferOwnershipRace.cs`, `.RevokeOwnershipOffer.cs`, plus additions to `.RemoveMembership.cs`/`.LeaveSpace.cs` (lapse), `.DeleteSpace.cs` (AC11), `.RoleMatrix.cs` (the `MatrixRoute`), and a schema partial for the index and the RLS predicates. Handler unit tests with a `RecordingOutbox` fake (no database). Isolation: `IsolationCases.OwnershipOffers.cs`. Client: `OwnershipSectionTests.cs` on the existing collection. Gates: `RefusalBoundaryGateTests.OwnershipOffers.cs`.
- **Every absence has an anchor and is seen red under a plant.** A masked plant is recorded, never deleted.
- **Memory budget (CLAUDE.md).** One `dotnet test` at a time; iterate with `--project ... -- --filter-class "<full name>"`; the single full run in the background with `--max-parallel-test-modules 1`, without the browser suite in Develop.

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | The filtered index removed from the migration (and any pre-check removed) | the second-offer case (`201`, two `Pending`); the schema `2601` case |
| 2 | The `2601` catch removed | the race-2 loser answers `500` |
| 3 | The insert's `recipient.Role <> 'Owner'` removed | the offer-to-self case (`201`) |
| 4 | The insert's `recipient.SpaceId = @space` removed (the key alone) | `ISO-OFFER-OWNERSHIP-FOREIGN-MEMBERSHIP` (or recorded masked by the FILTER predicate, with the reason) |
| 5 | The supersede skipped | the time-expired-row case (`409`) |
| 6 | The lapse omitted from `RemoveCoreAsync` | the removal-lapses case |
| 7 | The lapse omitted from `LeaveAsync` | the leave-lapses case |
| 8 | The revoke without `.Where(Actionable)` | the revoke-twice case (`204` instead of `409`) |
| 9 | The revoke as `ExecuteDelete` | the retained-row assertion; the state-writer gate |
| 10 | The offer skips the Memberships lock | Race 1's "both waited" assertion (or recorded masked) |
| 11 | The enqueue moved after commit (or a second enqueue on revoke) | the same-transaction trigger case; the nothing-on-revoke case; `The_ownership_offer_revoke_and_lapse_take_no_outbox` |
| 12 | A Project name passed into the email | the I-14 disclosure case; the template-signature gate |
| 13 | An offer id in the email link | the no-GUID assertion |
| 14 | `ExpiresAt` set from `TimeProvider` in C# | `Only_the_shared_actionable_predicate_compares_ExpiresAt` / the clock gate, or the database-clock case |
| 15 | The panel computes days from `DateTimeOffset.Now` | the browser-clock client case; the no-browser-clock gate |
| 16 | The Ownership section rendered for an Admin | the client absence case; the predicate gate |
| 17 | The picker pre-selects the first Membership | the no-default client case |
| 18 | The recipient's address in `OwnershipOfferResponse` | the contract-shape gate; the no-`@` client case |

### Previous story intelligence (4.7, 4.6, 4.5)

- **4.7:** a capability row gains its `MatrixRoute` when its endpoint is built (C4); the `Yello-Role` header is the pipeline's only; `RoleNarrationOwner` exists for a rise to Owner (5.2 will use it). The review found a case that stopped its Host before its own emails were sent, so the next Host dispatched them into another case's drop: **the producer waits for its own mail; consumers wait by recipient, never "any N"**.
- **4.6:** `NotificationSend` at every enqueue (Lee's Q4 (a)); the actor's name captured at authoring time; the link to the client origin with no ids; the recipient's address read in `MembershipStore` so no gate widens; a Space read in `MembershipStore` failed `Space_is_read_only_where_named`, so read the Space name through `IActiveSpaceNameReader`. `SoleDispatcherWireAsync` came from 4.6's develop rerun (`A_stale_claim_is_claimed_again...` failed because a pooled Host stole the rows). The full run caught schema lists (`TaskEndpointTests.Schema.cs`'s FK list, `Space_is_read_only_where_named`, `TablesThatCannotMove`) that iteration missed: widen them deliberately.
- **4.5:** the lapse's insertion point is beside `UnassignAsync`; a refused removal rolls back with the unit of work; Q10 (a) ledgered this lapse to 5.1/5.3; `409 ownership-held` and its copy; `LostMembership` → the pipeline's recorded `404`.
- **4.4:** S1200 pushes code into partials and nested types; a new pipeline dependency must be registered in `SpaceBoundaryPipelineTests` and `ActiveSpaceUnitOfWorkTests`; `WaitForEmailToAsync` returns the first email to an address.
- **All three reviews** patched story ids, Q references and name-restating XML docs in comments. Write them clean the first time.

### Git intelligence

Recent commits: `2251d84` close epic 4; `044b7b0`/`ae2d430` 4.7 (Role drop narration, Viewer read-only Space); `11d0dad`/`ee5e9a9` 4.6 (assignment and its email; `NotificationSend`). The cycle is: story; decisions recorded; ATDD scaffolds (`Skip = "red: ..."`, `atdd-checklist-<key>.md`, `PendingUntil` rows); implementation; review fixes. The orchestrator commits each.

### Project Structure Notes

- **New (expected):**
  - `Yello.Domain/Spaces/OwnershipOffer.cs`, `OwnershipOfferState.cs`
  - `Yello.Contracts/Spaces/OwnershipOfferRoutes.cs`, `OfferOwnershipRequest.cs`, `OwnershipOfferResponse.cs`
  - `Yello.Application/Spaces/IOwnershipOfferStore.cs`, `OwnershipOfferEmail.cs`, `OfferOwnership/OfferOwnershipHandler.cs`, `RevokeOwnershipOffer/RevokeOwnershipOfferHandler.cs`, `ReadOwnershipOffer/ReadOwnershipOfferHandler.cs`
  - `Yello.Infrastructure/Persistence/OwnershipOfferStore.cs`, `OwnershipOfferWrites.cs`, `Configurations/OwnershipOfferConfiguration.cs`, `Migrations/<timestamp>_OwnershipOffers.cs`
  - `Yello.Host/Endpoints/OwnershipOfferEndpoints.cs`
  - `Yello.Client/Components/OwnershipSection.razor`, `Yello.Client/Spaces/OwnershipCapabilities.cs`
  - Tests: the Slices partials above, `IsolationCases.OwnershipOffers.cs`, `RefusalBoundaryGateTests.OwnershipOffers.cs`, `OwnershipSectionTests.cs`, `FakeYelloApi.OwnershipOffers.cs`, an `OwnershipOfferWire` (with `IsOwnershipOfferEmail`)
- **Updated (expected):** `OutboxEmail.cs` (kind), `IMembershipStore.cs`, `MembershipStore*.cs` (lapse, notice, name), `OfferPredicates.cs`, `SchemaNames.cs`, `InvitationModel.cs` (or a sibling model helper), `ProblemTypes.cs`, `Program.cs`, `SpaceSettings.razor`, `SpaceCapabilities.cs`, `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`; tests `RefusalBoundaryGateTests.cs` (`SpaceScopedEntities`, `Space_is_read_only_where_named`), `SpaceIsolationSchemaTests.cs`, `ActiveSpaceEndpointTests.DeleteSpace.cs`, `.RoleMatrix.cs`, `.RemoveMembership.cs`, `.LeaveSpace.cs`, `RefusalBoundaryGateTests.Spaces.cs` (settings union), `TextSpacingTests.cs`; docs `ARCHITECTURE-SPINE.md`, `deferred-work.md`, `sprint-status.yaml`.
- **No new project, no new package, no new container-backed test class.** One migration.

### References

- `epics.md`: `:88-89` (FR-8, FR-42), `:150` (FR-43), `:200` (AR-12), `:201` (AR-13), `:204` (AR-16), `:219` (AR-31), `:221` (AR-33), `:222` (AR-34), `:247` (UX-DR9), `:252` (UX-DR14), `:285` (UX-DR41), `:376`, `:406`, `:459-467`, `:1918-1973` (5.1), `:1975-2110` (5.2-5.4, the boundaries).
- `prd.md`: `:59-64`, `:160` (FR-3), `:213-227` (FR-8), `:229-239` (FR-42), `:266`, `:300`, `:309`, `:352`, `:663-677` (FR-43), `:807`, `:894`; `addendum.md:55`, `:66-70`, `:151`.
- `ARCHITECTURE-SPINE.md`: AD-2, AD-3, AD-5 (`:106-113`), AD-9 (`:134-140`), AD-10 (`:142-147`), AD-18, AD-20, AD-23, AD-24, AD-26 (`:250-255`), AD-27 (`:257-261`), conventions `:289-298`, ER `:383-384`, tree `:410`, `:431`; `reviews/review-adversarial-seam.md:29-59`.
- `EXPERIENCE.md`: `:48`, `:53`, `:65`, `:99`, `:110-111`, `:204`, `:211`, `:216`, `:220`, `:262-263`, `:419`, `:438`, `:543-551`. `DESIGN.md:175-179`, `:319`, `:456`. Mockups `space-settings.html` (Ownership section), `ownership-offer.html` (5.2's dialog).
- `specs/spec-yello/`: `SPEC.md:62-67`, `:200-201`, `:274` (C3); `role-capability-matrix.md:23-28`, `:38-40`; `acceptance-criteria.md:73-92`.
- `test-design-qa.md`: `:278` (I-14), `:289` (V-10, 5.2), `:308` (A-10, 5.3), `:319` (S-2), `:365` (A-14, 5.2), `:494-496`. `test-design-architecture.md`: `:219`, `:237` (R12), `:319-326`, `:531-556` (R9, R10).
- `implementation-readiness-report-2026-08-22.md`: `:487`, `:521-522`, `:548`, `:799`, `:999`.
- `tests/TESTING-CONVENTIONS.md`: `:44-73`, `:87-207`, `:517-543`, `:545-580`, `:582-602`, `:604-672`, `:773-895`.
- `deferred-work.md`: `:544`, `:664`, `:705`, `:744-746`, `:792`, `:807`, `:817`.
- Prior stories: `4-5-remove-a-membership-or-leave-a-space.md` (Q10, the removal shape), `4-6-assign-a-task-and-be-told.md` (the email, `NotificationSend`, `SoleDispatcherWireAsync`), `4-7-a-viewers-space-and-a-role-that-drops-beneath-you.md` (C4, the mail-wait lesson), `4-1-invite-an-email-address-to-a-space-at-a-role.md` (outbox, expiry default), `4-2-see-and-revoke-pending-invitations.md` (guarded revoke).

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

- 2026-11-08: Story created (bmad-create-story). Status ready-for-dev.
