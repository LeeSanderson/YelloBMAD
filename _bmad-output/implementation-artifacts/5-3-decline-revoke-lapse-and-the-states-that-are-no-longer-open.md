# Story 5.3: Decline, revoke, lapse — and the states that are no longer open

Status: ready-for-dev

Epic: 5 — Hand a Space over and leave cleanly (third story)
Story key: `5-3-decline-revoke-lapse-and-the-states-that-are-no-longer-open`

**Requirements owned.** From the Story Coverage Index (`epics.md:378`): **FR-42** (the decline half, "the offering Owner is told it was declined", and "a declined or lapsed offer cannot afterwards be accepted"), **AR-13** (the 409-not-404 rule for a caller who holds a Membership), and **AR-31** (no job or timer writes a terminal expiry state; the architecture suite fails the build on one). Also carried here:

- test-design **A-10** (`test-design-qa.md:308`) and **R12** (`:185`; `test-design-architecture.md:237`, `:319-327`). The handoff table names this story twice: "State the expiry-seeding convention once" and "Give AD-27's ArchUnit rule a concrete predicate" (`test-design-qa.md:494-495`).
- **NFR-1 / I-1 / I-2** (`test-design-qa.md:197`, `:265-266`): a caller with no Membership gets the boundary `404`, so a settled offer never confirms that a Space or an offer exists.
- **FR-43** (`prd.md:671`): "Revoking the offer, declining it, or letting it lapse sends no further email."

The rules that bind this story:

- **FR-42** (`prd.md:229-241`):
  - "Only the single Membership the offer names can accept or decline it, and only while it is pending." (`:234`)
  - "Declining leaves every Membership and Role exactly as it was, and the offering Owner is told it was declined." (`:237`)
  - "A declined or lapsed offer cannot afterwards be accepted; the Owner must make a new offer." (`:238`)
- **AD-26** (`ARCHITECTURE-SPINE.md:257`): "A transition refused because the offer is no longer `Pending` — already accepted, declined, revoked, lapsed, or lost to a concurrent offer hitting the filtered index — returns **409** with a stable problem `type`, never 404: the caller holds a Membership in the Space, so AD-3's boundary rule does not apply." Its notes (`:259-260`) record what 5.1 and 5.2 built.
- **AD-27** (`:262-267`): one predicate, inside the statement's own `WHERE`, against the database clock; "No job and no timer writes the lapsed state"; "The architecture suite (AD-21) fails the build on a scheduled component writing a terminal expiry state". The 2026-11-08 note permits exactly one request-path write of `Lapsed` on an expired row: a new offer superseding it.
- **UX:**
  - `EXPERIENCE.md:262`: "This offer is no longer open." is a designed state, not an error toast. "Resolve the Space context first, evaluate `State = Pending` second, never the reverse."
  - `EXPERIENCE.md:551`: "409 for her, because she holds a Membership; a caller with none gets 404 on the same route."
  - `EXPERIENCE.md:216`, `:263`: the Ownership panel (Owner-only).
  - Mockup `mockups/ownership-offer.html:142-158` ("3 · No longer open").

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Covered by |
|---|---|---|
| `:746` (+ `:748`) | A-10: the `OwnershipOffer` half of "no scheduled writer of a state" | AC7, AC9 |
| `:940` | Decline exists since 5.2; telling the Owner, the 404-before-409 proofs, and A-10's `OwnershipOffer` half are still owed | AC1, AC3-AC5, AC7, AC9 |
| `:946` | `OfferStale` rolls back to `Pending`, but the client is told the offer is no longer open | Q4 |
| `:947` | A revoke whose actor's Membership went while it waited answers `404`/`409`, not the recorded `LostMembership` | AC4, AC5 (a real defect against AC4) |
| `:953` | An offer-level `404` on Accept or Decline closes the dialog silently | AC6, Q5 |
| `:948` (epic 5, optional) | Offer statements without their own `SpaceId = @space` | Q8 (fold in the statements this story touches) |
| `:932` (epic 5, optional) | `OwnershipOfferPendingButExpired` restates the shared predicate | Q9 |
| `:937` (owner 7.7) | The Offer indicator is not pushed | UPDATED: the Owner's "declined" statement has the same bound (Q10) |

## What already exists — do not rebuild it

Story 5.2 (commit `1ea4a9c`, reviewed in `dca9ee3`) built **accept and decline** (Lee's Q3 (a), 2026-11-09). Story 5.1 (`542f95d`, `89b98a3`) built the offer, the revoke and the lapse by event. This story **adds no decline route, no decline handler, no decline button, no new problem type, no new `OwnershipOfferState` member and no migration** unless Q1 says otherwise. Here is what is already in the tree:

| Concern | Where it lives today | Already proven by |
|---|---|---|
| Decline route `POST /api/v1/spaces/{spaceId}/ownership-offers/{offerId}/decline` → `204` | `OwnershipOfferRoutes.Decline`/`DeclineOf`; `OwnershipOfferAnswerEndpoints.MapDeclineOwnershipOffer` (`ReadProjectsAndTasks`, both surfaces) | `ActiveSpaceEndpointTests.DeclineOwnershipOffer.cs` (3 cases); 4 isolation rows in `IsolationCases.OwnershipOfferAnswers.cs` |
| Decline handler (no outbox, no Role read, no publish) | `Yello.Application/Spaces/DeclineOwnershipOffer/DeclineOwnershipOfferHandler.cs` | gate `The_ownership_acceptance_and_decline_take_no_outbox` |
| Decline store: lock → guarded `ExecuteUpdate` (`NamingTheCaller` + `OwnershipOfferActionable`) → `Declined`; miss → `MissAsync` (own Membership first, then "named the caller", then not found) | `OwnershipOfferStore.Answers.cs:131-184` | `The_named_recipient_declines_no_Role_moves_and_the_offer_cannot_then_be_accepted`, `A_decline_by_anyone_the_offer_does_not_name_is_the_in_Space_404_and_nothing_changes`, `A_decline_replays_under_its_key_and_a_new_key_is_refused_409_ownership_offer_not_pending` |
| Accept: 409 for a settled offer to its recipient | `OwnershipOfferStore.AcceptAsync` | Theory `An_offer_no_longer_open_to_its_recipient_is_409_and_changes_nothing` over `Revoked`, `Lapsed`, `Declined`, `Expired`; race 1 and the new-key replay case (an `Accepted` offer) |
| Revoke: lock → guarded `ExecuteUpdate` (actionable + actor still Owner) → `Revoked`; miss → `NoLongerOwner` / `NotPending` / `NotFound` | `OwnershipOfferStore.cs:134-167` | Theory `Revoking_an_offer_that_is_no_longer_pending_is_409_and_changes_nothing` over `Revoked`, `Lapsed`, `Expired`; race 2 (`403` after a swap) |
| Lapse by event (removal, leave), written under the lock before the `DELETE` | `OwnershipOfferWrites.LapseNamingAsync` | `ActiveSpaceEndpointTests.OwnershipOfferLapse.cs` (5 cases), `OfferOwnershipRace.cs` |
| The new offer lapses an expired `Pending` row before its insert | `OwnershipOfferStore.OfferAsync:76-79`, `OfferPredicates.OwnershipOfferPendingButExpired` | AD-27 note 2026-11-08 |
| A concurrent second offer → `409 ownership-offer-pending` (the filtered index, `2601`) | `OfferAsync:102-105` | `Two_offers_held_on_the_Memberships_lock_commit_one_and_refuse_the_other_409` |
| Expiry on read: the recipient's read and the Owner's read answer `204` for an expired row | `ReadMineAsync`, `ReadPendingAsync` | `The_recipients_read_answers_204_..._for_an_expired_offer`, `The_pending_read_answers_204_when_no_offer_is_actionable` |
| "This offer is no longer open." designed state in the dialog (on an open that reads `204`, and on a `409 ownership-offer-not-pending` or `owner-role-fixed` answer to Accept or Decline) | `OwnershipOfferDialog.razor:61-70`, `:308-323`, `:337-343`; copy `ownership-offer-not-pending`, `OwnershipOfferNoLongerOpenDetail` | `An_acceptance_refused_409_shows_the_offer_is_no_longer_open_and_removes_the_indicator`, `Opening_the_dialog_on_an_offer_that_has_closed_shows_the_no_longer_open_state`, `Decline_sends_one_decline_and_removes_the_indicator` |
| The Owner's revoke that meets `409` re-reads the pending offer | `OwnershipSection.razor:633-640` | `OwnershipSectionTests` |
| State-writer gate: only `OwnershipOfferStore` and `OwnershipOfferWrites` write `OwnershipOffer.State` (IL and raw SQL) | `RefusalBoundaryGateTests.OwnershipOffers.cs:176` | — |
| Timer ban: no `BackgroundService`, `IHostedService`, `IHostedLifecycleService`, `PeriodicTimer` or timer anywhere in production | `RefusalBoundaryGateTests.No_production_code_runs_on_a_timer_or_as_a_hosted_service` (`RefusalBoundaryGateTests.cs:733`) | — |
| The pipeline resolves the Space (Membership → boundary `404`, recorded `CrossSpace`) **before** the capability check, the idempotency lookup and the handler | `ActiveSpaceResolution*.cs` (`ActiveSpaceResolution.Idempotency.cs:11-14`) | isolation suite |

**What is genuinely new in 5.3:**

1. **The offering Owner is told it was declined** (AC1, Q1). Nothing tells the Owner today: the Ownership panel shows the picker again, exactly as after a revoke or an expiry.
2. **The revoke's `LostMembership` miss** (AC4/AC5, ledger `:947`). Today a former Owner whose Membership ended while the revoke waited on the lock gets `409` for an offer that still exists. That is a caller with **no** Membership learning that the offer exists, which is exactly what AC4 forbids.
3. **The settled-state matrix completed** (AC3): decline across every settled state; revoke of a `Declined` and of an `Accepted` offer; the superseded-expired offer; and the non-member `404` on **settled** offers (the isolation rows today use only pending offers).
4. **The A-10 gate with a concrete predicate** and the expiry-seeding convention stated once (AC7, AC9; R12).
5. **The dialog's in-Space `404`** (AC6, ledger `:953`): today it closes silently.

**Explicitly not in this story:**

- **Account deletion and its lapse** (5.4; ledger `:910`, `:921`).
- **A live push** of a decline to the Owner's open panel, or of a revoke to the recipient's indicator (7.7; ledger `:937`).
- The other low entries owned by "epic 5" that this story does not touch (`:918`, `:922-925`, `:929-930`, `:933`, `:944-945`, `:951-952`, `:954`). They stay with epic 5.
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits.
- **No new NuGet package and no new project without Lee** (CLAUDE.md).

## Story

As anyone touching an offer that has already been settled,
I want a clear, non-disclosing answer,
so that a race or a stale tab never transfers a Space and never reveals one.

## Acceptance Criteria

AC1 to AC8 are reproduced **verbatim** from `epics.md:2032-2065` and numbered by position. AC9 to AC11 come from the test design, the ledger and a survey of the code, and each cites its source. The indented notes are implementation guidance; a note that depends on an open question names it. **[LEE]** marks a question for Lee. The orchestrator may route it to him before development. Otherwise the dev phase takes the recommendation and records it as a reversible decision.

**AC1 — decline leaves everything, and the Owner is told** (FR-42 `:237`)
**Given** a pending offer
**When** the named Membership declines it
**Then** every Membership and Role is left exactly as it was, and the offering Owner is told it was declined

> - **The decline itself exists** (5.2). Keep its route, guard, answers and copy unchanged. Add the proofs that are missing:
>   - **No publish.** A `HostProcess.RunAsync` case declines and then counts `MembershipChangedPublished` lines naming the case's ids: none (the `PublishesNaming` shape from `ChangeMembershipRole.cs`). A revoke publishes none either; add it to the same case.
>   - **No email** (FR-43 `prd.md:671`). Wait for the offer's own email first, then decline, then assert that neither the recipient nor the offerer received another (wait by recipient, never "any N"). Do the same after a revoke if no 5.1 case already covers it (5.1 AC7 lists revoke and lapse; check `OfferOwnershipOutbox.cs` before duplicating).
>   - Every Membership row is byte-equal before and after (`MembershipRowsInAsync`), and S-2 holds.
> - **How the Owner is told** (Q1 **[LEE]**). Recommendation (a):
>   - **Route:** `GET /api/v1/spaces/{spaceId:guid}/ownership-offers/declined`. It declares `Capability.OfferOwnershipTransfer` (Owner only, like the pending read) and serves both surfaces (AD-4).
>     - It answers `200 DeclinedOwnershipOfferResponse(Guid Id, string? RecipientName)` when the Space's **most recent** offer (latest `CreatedAt`, ties broken by `Id`) is `Declined`.
>     - It answers `204` otherwise. That covers no offer, a pending offer, and a latest offer that was accepted, revoked, lapsed or expired.
>     - `RecipientName` is the recipient's display name now (`IMembershipStore.DisplayNameOfAsync`, as `ReadOwnershipOfferHandler` reads it). It is `null` when that Membership is gone; a retained offer outlives the Membership it names (AD-26 note 2026-11-08).
>     - No address and no recipient id.
>   - **Store:** `IOwnershipOfferStore.ReadLastDeclinedAsync` is one no-tracking projection under the Space key, with its own `SpaceId = @space` (Q8). Take the top row by `CreatedAt DESC, Id DESC` first, **then** test `State == Declined`. Filtering before taking the top row would surface an old decline behind a newer revoked offer.
>   - **Why "latest" is enough:** ownership moves only by accepting the latest offer, so the latest offer's offerer is always the current Owner unless that offer was `Accepted`. An accepted offer answers `204`. No offerer clause is needed; say so in a remark.
>   - **Handler:** `Yello.Application/Spaces/ReadDeclinedOwnershipOffer/ReadDeclinedOwnershipOfferHandler.cs`. It writes nothing, takes no outbox and reads no Role.
>   - **Client** (`OwnershipSection.razor`):
>     - When the pending read answers `204`, read `declined`. On `200`, render a statement above the picker, `<p class="ownership-section__declined">`. Its copy is `OwnershipOfferDeclinedBy` "{name} declined your offer of ownership.", or `OwnershipOfferDeclined` "Your last offer of ownership was declined." when the name is `null`.
>     - The statement is a plain statement: not accent, not `role="alert"`, not a toast. No exclamation mark; it names no Project, Task or other Member. Each string goes in `ClientCopy.resx` with a `<comment>`.
>     - It is absent on `204`, and absent once a new offer is made (the `201` shows the pending offer). It is never hidden or disabled (UX-DR27).
>     - After a revoke answered `409 ownership-offer-not-pending`, the section already re-reads the pending offer (`:633-640`). Make that re-read also read `declined`. The polite status stays "This offer is no longer open."; the statement explains why.
>     - Purge both reads with the Space (the 3.2 rule). No timer and no polling (AD-10). The Owner learns of the decline at the next read of the panel; the live push belongs to 7.7 (Q10).
>   - **Gates:**
>     - `OfferOwnershipTransfer_is_required_by_exactly_the_three_offer_routes` becomes four routes. Rename it, for example `..._exactly_the_four_offer_routes`.
>     - `The_ownership_offer_routes_require_OfferOwnershipTransfer_and_serve_both_surfaces` gains the new pattern.
>     - The contract-shape gate pins `DeclinedOwnershipOfferResponse` to exactly two members.
>     - `Every_spaceId_endpoint_declares_exactly_one_capability` must stay green. Check whether `The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare` or `RoleMatrix` enumerates routes per capability. If it does, add the route.
>   - **Expect the panel's request counts to change.** This adds one `GET` when no offer is pending. Grep `OwnershipSectionTests`, `MembershipSectionTests` and `SpaceSettings*` for counted arrival reads, and widen them deliberately (the 5.1 lesson).
> - **Slices cases** (new partial `ActiveSpaceEndpointTests.DeclinedOwnershipOffer.cs`):
>   - after a product decline, the Owner reads `200` with the recipient's name; the recipient's own `mine` read is `204`;
>   - after a new offer, `204`;
>   - after a decline followed by a revoke of a later offer, `204` (the latest wins);
>   - after the declining recipient leaves, `200` with `RecipientName = null`;
>   - an Admin, a Member and a Viewer get the recorded `403` for `offer-ownership-transfer`;
>   - no `@` in any body.
> - **Browser cases** (`OwnershipSectionTests`; the fake gains the `declined` read):
>   - the statement renders with the name, above the picker, and there is no `[role=alert]` in the section;
>   - it renders the nameless copy;
>   - it is gone after a confirmed new offer;
>   - a revoke answered `409` shows it after the re-read;
>   - add a `TextSpacingTests`/320px reflow row.

**AC2 — declined or lapsed cannot be accepted; the Owner must make a new offer** (FR-42 `:238`)
**Given** a declined or lapsed offer
**When** acceptance is later attempted
**Then** it is refused, and the Owner must make a new offer

> - **The refusal exists** (`An_offer_no_longer_open_to_its_recipient_is_409_and_changes_nothing` for `Declined`/`Lapsed`, and the decline case's follow-up accept). Add the second half, which no case proves today: **the Owner can make a new offer, and only the new one is acceptable.**
>   - **After a decline:** a product offer → decline `204` → a new product offer to the **same** recipient → `201`. The old id's accept → `409 ownership-offer-not-pending`. The new id's accept → `204`. The old row stays `Declined`. S-2.
>   - **After a lapse by event:** the recipient is removed (the offer lapses) → re-invited and joined through the product (a **new** Membership id) → a new offer → `201`. The old id's accept by the new Membership → the **in-Space `404`**, because the old offer names a Membership that no longer exists and so does not name the caller (5.2's Q7). The new id's accept → `204`.
>   - **After an expiry superseded by a new offer** (the AD-27 2026-11-08 note): a product offer whose `ExpiresAt` is moved into the past (Q7, `SetOwnershipOfferExpiresAtAsync`) → a new offer → `201`. The old row is now `Lapsed`, written by the new offer's request-path statement. The old id's accept → `409`. The new id's accept → `204`.
>   - **The stale tab** (the user story's "a stale tab never transfers a Space"): in each of the three cases above, the old id was the one a stale dialog would hold. An accept by offer id can never land on a newer offer (5.2's Q2). Say so in the case summaries.

**AC3 — a settled offer is 409 with a stable type to a caller who holds a Membership** (AD-26, AR-13)
**Given** an offer no longer `Pending` — already accepted, declined, revoked, lapsed, or lost to a concurrent offer hitting the filtered index
**When** a transition is attempted by a caller holding a Membership in that Space
**Then** **409** is returned with a stable problem `type`, never 404
**And** the reason is stated: the caller holds a Membership, so the boundary rule does not apply, and inventing a 404 here would be a divergence rather than a disclosure

> - **Who "a caller" is** (Q2, Q3). A transition belongs to someone: accept and decline to the named recipient, revoke to the Owner. The `409` goes to that party. 5.2 decided (Q7, applied) that a Member the offer does **not** name gets the unrecorded in-Space `404`. That `404` is byte-identical to the boundary, so a Member learns nothing about whom an offer names. A Member who is not the Owner and calls the revoke gets the pipeline's recorded `403` for `offer-ownership-transfer`, because the capability is judged before any state. Keep both. Record the reading in a dated AD-26 note: "the 409 goes to the party the transition belongs to". Neither answer is a disclosure, and neither is the `404` that AC3 forbids, which is a `404` given to that party.
> - **The matrix.** Each cell is `409 ownership-offer-not-pending` unless stated otherwise. In every cell: no `@` in the body, nothing recorded (`AccessRefusal` count unchanged), the offer row unchanged (an expired row stays `Pending`, because expiry is never written), every Membership row unchanged, and S-2.
>
>   | State | Accept (recipient) | Decline (recipient) | Revoke (Owner) |
>   |---|---|---|---|
>   | `Accepted` | exists (race 1, new-key replay) | **new** | **new** (a seeded `Accepted` row while the offerer is still Owner. Through the product, the revoker is then an Admin and gets the recorded `403`, which race 2 already proves) |
>   | `Declined` | exists | exists (the new-key case) | **new** |
>   | `Revoked` | exists | **new** | exists |
>   | `Lapsed` (event) | exists | **new** | exists |
>   | `Lapsed` (superseded expiry) | AC2 | **new** | **new** |
>   | Expired (`Pending`, past `ExpiresAt`) | exists | **new** | exists |
>   | Lost to a concurrent offer | n/a: the losing **offer** is refused `409 ownership-offer-pending` (`Two_offers_held_on_the_Memberships_lock_commit_one_and_refuse_the_other_409`) and no row is written, so no id exists to transition. Cite it; do not duplicate it. | | |
>
> - **Shape.** Prefer **extending the existing theories** over new classes:
>   - add `"Accepted"` to `OffersNoLongerOpenToTheRecipient`;
>   - add `"Declined"` and `"Accepted"` to `OffersNoLongerPending`;
>   - add a decline theory over the same data, `A_settled_offer_is_409_to_its_recipients_decline_and_changes_nothing`, in `ActiveSpaceEndpointTests.DeclineOwnershipOffer.cs`.
>   - Seeding a terminal state uses `AddOwnershipOfferAsync` with the state, after the Space's live offer was revoked (the existing anchor). Record which rows are seeded (R12; the `Accepted` row cannot be reached through the product with the offerer still Owner).
> - **The reason stated:** it already sits in `OwnershipOfferRoutes` remarks and AD-26. Add a short `<remarks>` "why" line to `OwnershipOfferEndpoints.NotPending()` (no ids, no history): a caller who reaches it holds a Membership here, so the boundary rule does not apply.

**AC4 — 404 to a caller with no Membership** (AD-3, AD-26, NFR-1, I-1/I-2)
**Given** the same route
**When** it is called by an Account holding no Membership in that Space
**Then** **404** is returned, because 409 would confirm that the Space and the offer exist

> - **Three kinds of non-member, each on a settled offer.** Each gets the boundary `404 not-available`, byte-identical to a random Space id, recorded `CrossSpace`, with the offer row and every Role unchanged:
>   1. **A stranger** (never a Member): accept, decline and revoke on a `Declined` offer, a `Revoked` offer and an `Accepted` offer.
>   2. **The former recipient**, removed by an Admin (the offer is now `Lapsed`): its accept and decline on its own old offer id. Today this is answered by the pipeline; prove it.
>   3. **The former Owner** who left after the acceptance (UJ-8's end): its revoke on the `Accepted` offer.
> - **Where:** the isolation suite, on both surfaces (`TESTING-CONVENTIONS.md:773-895`), in `IsolationCases.OwnershipOfferAnswers.cs`/`.OwnershipOffers.cs`. Suggested ids:
>   - `ISO-ACCEPT-SETTLED-OWNERSHIP-OFFER-FOREIGN-SPACE`, `ISO-DECLINE-SETTLED-OWNERSHIP-OFFER-FOREIGN-SPACE`, `ISO-REVOKE-SETTLED-OWNERSHIP-OFFER-FOREIGN-SPACE` (the stranger, `Then` = the offer's state is unchanged);
>   - `ISO-ACCEPT-OWNERSHIP-OFFER-AFTER-REMOVAL`, `ISO-DECLINE-OWNERSHIP-OFFER-AFTER-REMOVAL` (the former recipient);
>   - `ISO-REVOKE-OWNERSHIP-OFFER-AFTER-LEAVING` (the former Owner).
>   - Each is `Both(new BoundaryNotFound { Then = ... })`. Seed through the product. For the ApiToken surface of the "former" rows, issue the token while the Membership exists; a removed Membership's token is refused by the pipeline.
> - **The store-level arm** (the defect, ledger `:947`). A caller whose Membership ends **while its request waits on the Memberships lock** passes the pipeline and reaches the store. Every store miss must check the caller's Membership **before** it reads the offer:
>   - `MissAsync` already does this for accept and decline.
>   - `RevokeAsync` does not: `ActorRoleAsync` returning `null` falls through to the existence read and answers `409`/`404`.
>   - **Fix:** add `OwnershipOfferRevocation.LostMembership`, return it when `ActorRoleAsync` is `null`, and map it to `new MembershipLost()` in `OwnershipOfferEndpoints` (the recorded boundary `404`). The switch keeps its `_ => throw`.
> - **Controlled races** (`WaitUntilBlockedOnALockAsync`; never `Task.Delay`; each racing Account is present and has fetched its anti-forgery token):
>   - **Decline versus removal:** hold `Yello/Memberships/{spaceId}`. Queue the Admin's removal of the recipient and wait until it blocks. Then queue the recipient's decline and wait until it blocks. Release. The removal `204`s and lapses the offer. The decline answers the recorded boundary `404` (`LostMembership`, `CrossSpace`), not `409`. The offer stays `Lapsed`. S-2. `AssertNoPendingOfferNamesAMissingMembershipAsync`.
>   - **Revoke after the revoker's Membership ended:** hold the lock. Queue, in order and each waited until blocked, (1) the recipient's accept, (2) the Owner's leave, (3) the Owner's revoke. All three pass the pipeline under their pre-swap Roles. Release.
>     - Expected: the accept `204`s.
>     - The leave runs after the swap, as an Admin, and `204`s.
>     - The revoke finds no Membership and answers the recorded boundary `404`. It must **not** answer `409`.
>     - S-2; one Owner (the recipient).
>     - The `sp_getapplock` exclusive waiters are granted in queue order, which is why each request is waited until blocked before the next is sent. If a run shows otherwise, record the observed order and make the assertion order-tolerant: the revoke answers `404` whenever the leave committed before it, and is never a `409` to a non-member.
>   - **Plant:** remove the new `LostMembership` branch → the revoke race answers `409`, red.

**AC5 — Space first, state second** (AD-26, `EXPERIENCE.md:262`)
**Given** that pair of requirements
**When** the handler runs
**Then** the Space context is resolved **first** and `State = Pending` evaluated **second**, never the reverse

> - **In the pipeline:** the Space is resolved before the capability check, the idempotency lookup and the handler (`ActiveSpaceResolution.Idempotency.cs:11-14`). AC4's isolation rows prove it at the route.
> - **In the store:** every miss reads the caller's Membership first (`MissAsync` for accept and decline; `RevokeAsync` after the AC4 fix). Add a short "why" comment at each of those reads: the caller's own Membership decides between the boundary and the in-Space answers before the offer's state is looked at.
> - **Plants:**
>   - move the revoke's existence read before `ActorRoleAsync` → the AC4 revoke race answers `409`, red;
>   - in `MissAsync`, read "named the caller" by `AccountId` without first resolving the caller's Membership → the decline-versus-removal race answers `409`, red. Record the exact plant used.
> - Replays: an `Idempotency-Key` replay by a caller who has since lost its Membership is refused by resolution before the lookup. Add one Slices case: decline `204` under a key, the recipient is removed, the same key → boundary `404`, not the recorded `204`.

**AC6 — "This offer is no longer open." is a designed state** (`EXPERIENCE.md:262`, mockup `ownership-offer.html:142-158`)
**Given** the offer dialog open when the offer stops being pending
**When** the User answers
**Then** they see "This offer is no longer open." as a designed state rather than an error toast

> - **Exists** for Accept (409), for opening (204), and for Decline's code path (`UseAnswerAsync` treats both alike). Add the browser proofs that are missing (`OwnershipOfferTests`):
>   - **Decline answered `409 ownership-offer-not-pending`:**
>     - the body is replaced in place by the heading "This offer is no longer open." and "Nothing has changed about your access to this Space.";
>     - focus is on that heading, and "Close" is the only button;
>     - the indicator is gone;
>     - there is no `[role=alert]`, no toast element and no reason or actor named;
>     - a Decline does **not** request a re-read (no Role can have moved).
>   - **Each cause renders the same state:** a Theory over the fake's settled causes (revoked, declined elsewhere, lapsed, expired, accepted elsewhere). Each answers the same `409` type, so the dialog's text is identical across rows. This proves the dialog never names a reason.
> - **The in-Space `404` on Accept or Decline** (ledger `:953`; Q5 recommendation (a)). Today `UseAnswerAsync:325-332` closes the dialog silently and requests a re-read.
>   - Change it to show the no-longer-open state, keep the re-read, and let the arrival decide. If the Space itself refused, the re-read lands on the one refusal surface, as today. If the Space is readable, the User sees the designed state rather than a vanished dialog.
>   - The in-Space and boundary `404`s are byte-identical by design, so the client must not try to tell them apart.
>   - Browser case: Decline answered by the not-available body with the Space still readable → the no-longer-open state, focus on its heading, the indicator gone.
>   - Plant: restore the silent close → red.

**AC7 — expiry by time is computed on read; nothing is emailed on lapse** (AD-27, AR-31, FR-43)
**Given** expiry by the passage of time
**When** it occurs
**Then** no job and no timer writes the lapsed state — it is computed on read by the shared predicate against the database clock
**And** there is consequently **no event on which to hang a notification**, which is why nothing is emailed on lapse; this is a structural consequence rather than a policy choice

> - **Runtime proof** (Slices, new or extended in `OwnershipOfferLapse.cs`): a product offer, its own email awaited, then `ExpiresAt` moved into the past (Q7). Then:
>   - the Owner's pending read is `204`, the recipient's `mine` read is `204`, and the Owner's `declined` read is `204` (an expiry is not a decline);
>   - accept, decline and revoke are `409` (AC3);
>   - the stored row is **still `Pending`** with its `ExpiresAt`, read straight from the table: nothing wrote it;
>   - no further email reaches either party, and no `NotificationSend` row is added for the offer kind;
>   - nothing is published.
>   - `Assumption = "PRD-12-3"`.
> - **The one sanctioned writer of `Lapsed` on an expired row** is a new offer's request-path statement (AD-27 note 2026-11-08). AC2's superseded case proves it fires only when a new offer is made.
> - **The gate** is AC9.

**AC8 — every transition guarded by `WHERE State = Pending` and a rowcount** (AD-26, AR-13, AD-18)
**Given** any transition
**When** it is guarded
**Then** it uses `WHERE State = Pending` with a rowcount check rather than relying on the endpoint's `Idempotency-Key` alone
**And** the reason is stated: no route, the API included, may admit ownership arriving unrequested

> - **Exists** for accept, decline and revoke (each composes `OfferPredicates.OwnershipOfferActionable`, which contains `State == Pending`, into its own `ExecuteUpdate` and branches on the rowcount). The "a new key is refused 409" cases exist for all three.
> - **Gate** (new, `RefusalBoundaryGateTests.OwnershipOffers.cs`): `Every_OwnershipOffer_state_transition_composes_the_actionable_predicate`.
>   - Every method of `OwnershipOfferStore` that calls `ExecuteUpdateAsync` on `OwnershipOffer` with a target state of `Accepted`, `Declined` or `Revoked` also references `OfferPredicates.get_OwnershipOfferActionable` in its body, including its async state machine. Use the Cecil walker that `The_ownership_acceptance_changes_Roles_by_two_ordered_ExecuteUpdate_calls_and_never_SaveChanges` uses.
>   - The two `Lapsed` writers are named exceptions, each with its reason. `LapseNamingAsync` writes by event and is raw SQL with `State = Pending` in its text: assert that literal. `OfferAsync`'s supersede composes `OwnershipOfferPendingButExpired`.
>   - Anchor: the gate finds the three transition methods.
>   - Plant: remove `.Where(OfferPredicates.OwnershipOfferActionable)` from `DeclineAsync` → red. A runtime case would also go red (the new-key decline answers `204`).
> - **The reason stated:** a short `<remarks>` "why" on `IOwnershipOfferStore` (or on `OwnershipOfferStore`'s class remarks, which already describe the guard): the key deduplicates one request, while the guarded statement refuses a second request under a new key, so ownership can never arrive through a retry the recipient did not make. No ids, no history.

**AC9 — A-10: no scheduled component writes an offer or an Invitation state** (AR-31, AD-27, test-design A-10, `test-design-architecture.md:319-327`, ledger `:746`)

> - **The concrete predicate** (Q6 recommendation (a)), a new gate `No_scheduled_component_reaches_an_offer_or_Invitation_state_write` in `RefusalBoundaryGateTests.OwnershipOffers.cs` (or a new partial `.Expiry.cs`):
>   - **Writers:** the set of production methods that write `OwnershipOffer.State` or `Invitation.State`. Compute it the way the two writer gates do (bulk `ExecuteUpdate`/`ExecuteDelete` on the entity, `set_State`, and raw SQL matching the write regex), at **method** granularity.
>   - **Scheduled roots:** every production type that derives from, implements or references `BackgroundService`, `IHostedService`, `IHostedLifecycleService`, `PeriodicTimer`, `System.Threading.Timer` or `System.Timers.Timer`; every method passed as a timer callback; and every type registered through `AddHostedService`.
>   - **Assertion:** the transitive call graph from every scheduled root, across the Yello assemblies and including compiler-generated state machines and lambdas, reaches no writer. Interface calls resolve to every production implementation, because `IOwnershipOfferStore`'s methods are reached through the interface.
>   - **Vacuity anchors.** Today there are no scheduled roots, so the walk alone proves nothing. Two anchors make it real:
>     - (i) from `DeclineOwnershipOfferHandler.HandleAsync`, the same walker reaches `OwnershipOfferStore.DeclineAsync`, a writer;
>     - (ii) the writer set contains every method named in the AC8 gate plus `InvitationStore`'s revoke and accept.
> - **Expiry is never the predicate of a write, with one named exception.** `OfferPredicates.OwnershipOfferPendingButExpired` is referenced only by `OwnershipOfferStore.OfferAsync`, and no Invitation expiry write exists at all.
> - **Plant:** a `BackgroundService` in Infrastructure whose `ExecuteAsync` calls `OwnershipOfferWrites.LapseNamingAsync` (and is never registered) → this gate goes red **and** the timer ban goes red. Record both. A second plant, a non-hosted method that is passed to `new Timer(...)` and calls the lapse, goes red here too.
> - **Ledger** `:746` → CLOSED (both halves). The `Invitation` half was the 4.2/4.3 writer gates; this gate adds the scheduled-root predicate for both entities.

**AC10 — the expiry-seeding convention, stated once** (R12, `test-design-qa.md:185`, `:494`)

> - Add a short section to `tests/TESTING-CONVENTIONS.md`, "Expiry is reached only by seeding the past" (R12). It says:
>   - The shared predicate reads `SYSDATETIMEOFFSET()` inside the statement. So **no** fake `TimeProvider`, no `Task.Delay` and no browser clock can make a row expire.
>   - A case reaches expiry by writing a past `ExpiresAt` measured from the **database** clock (`await wire.DatabaseNowAsync()`, then `.AddMinutes(-1)`).
>   - Prefer moving a product-made row: `SetInvitationExpiresAtAsync`, and the new `SetOwnershipOfferExpiresAtAsync` (Q7). Insert a raw expired row (`AddOwnershipOfferAsync(..., "Pending", past)`, `AddInvitationAsync`) only when no product path can make it.
>   - Such cases carry `Assumption = "PRD-12-3"` (and its Invitation counterpart).
>   - Browser cases fake `ReadAt`/`ExpiresAt` pairs and set Playwright's `Clock` far away to prove the client never uses its own.
> - **Helper:** `WireDatabase.SetOwnershipOfferExpiresAtAsync(Guid spaceId, Guid offerId, DateTimeOffset expiresAt)`. It mirrors `SetInvitationExpiresAtAsync` (`WireDatabase.cs:641`): under the Space's own key, raw `UPDATE dbo.[OwnershipOffer] SET ExpiresAt = @expires WHERE Id = @id`.
> - **Watch the writer gate.** `Only_the_offer_store_writes_an_OwnershipOffer_state` scans **production** assemblies for SQL writes, so a test helper is outside it. Confirm the scan does not load `Yello.Tests.Shared` (`RuntimeAssemblies()`).

**AC11 — what must not change** (regression guard)

> - The accept and its swap, its races, `MembershipChanged` (two per accept), S-2 and A-14 are unchanged.
> - The offer, the pending read, the revoke's existing answers (`204`, `409`, the recorded `403` for `NoLongerOwner`, the in-Space `404`) and the email are unchanged. The **only** change to the revoke's answers is `LostMembership` → the boundary `404` where it used to be `409`/`404`.
> - The dialog's existing states, focus rules and "Not sent" key reuse are unchanged, except the in-Space `404` (AC6).
> - The boundary `404` stays byte-identical everywhere (`RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal`, `SameAsAnchorAndStranger`).
> - Address confinement: no address in any new response, log, idempotency record or panel text.
> - `The_ownership_offer_problem_types_are_declared_and_each_has_client_copy` stays green. **No new problem type.**
> - Gates `Only_the_offer_store_writes_an_OwnershipOffer_state` (two writers), `The_ownership_offer_revoke_and_lapse_take_no_outbox` and `The_ownership_acceptance_and_decline_take_no_outbox` keep their lists. The new read handler takes no outbox; add it to the no-outbox gate if it lists read handlers.
> - The "Authority" name trap: no new Application type whose name contains `Authority`.

## Tasks / Subtasks

- [ ] **Task 0: decisions** (before any code)
  - [ ] Q1 is marked **[LEE]**. If the orchestrator routes it to Lee, record his answer here under "Decided by Lee" before ATDD. Otherwise take recommendation (a) and record it as "Q1 - (a)", with its cost, in Completion Notes.
  - [ ] Decide Q2-Q10 the same way. Each recommendation is the default.
- [ ] **Task 1: Contracts** (AC1)
  - [ ] `OwnershipOfferRoutes.Declined` and `DeclinedOf(spaceId)`. Update the class remark, which today says "All six are served on both surfaces"; with this route there are seven.
  - [ ] `Yello.Contracts/Spaces/DeclinedOwnershipOfferResponse.cs` (`Guid Id, string? RecipientName`), with a short remark: no address, and the name is null once the Membership is gone.
- [ ] **Task 2: Application** (AC1, AC4)
  - [ ] `IOwnershipOfferStore.ReadLastDeclinedAsync` returning a small record (`Id`, `RecipientMembershipId`) or `null`.
  - [ ] `OwnershipOfferRevocation.LostMembership`.
  - [ ] `Yello.Application/Spaces/ReadDeclinedOwnershipOffer/ReadDeclinedOwnershipOfferHandler.cs`: no outbox, no Role read, nothing recorded, the name read through `IMembershipStore.DisplayNameOfAsync`.
- [ ] **Task 3: Infrastructure** (AC1, AC4, AC5, Q8)
  - [ ] `OwnershipOfferStore.ReadLastDeclinedAsync`: a projection, top row first, then the state test, with an explicit `SpaceId = @space`.
  - [ ] `RevokeAsync`: `ActorRoleAsync` null → `LostMembership`, **before** the existence read. Add the "why" comment.
  - [ ] Q8 (a): add `SpaceId == spaceId` to `ActorRoleAsync`, `MissAsync`'s offer read and `SwapAsync`'s offer read.
  - [ ] **No migration.** Confirm with `dotnet ef migrations has-pending-model-changes`.
- [ ] **Task 4: Host** (AC1, AC3, AC4)
  - [ ] `OwnershipOfferEndpoints`: `MapReadDeclinedOwnershipOffer` (`OfferOwnershipTransfer`; `200`/`204`); map `OwnershipOfferRevocation.LostMembership` → `new MembershipLost()`; add the "why" remark on `NotPending()`.
    - If S1200's coupling limit trips again (it did in 5.2), put the read in `OwnershipOfferAnswerEndpoints` or a new small endpoints class. Record the choice.
  - [ ] Register the handler and map the route in `Program.cs` beside the other offer routes, and extend that comment block.
- [ ] **Task 5: Client** (AC1, AC6)
  - [ ] `OwnershipSection.razor`: the `declined` read after a pending `204` (including the re-read after a revoke `409`), the statement, and the purge.
  - [ ] `ClientCopy.resx`: `OwnershipOfferDeclinedBy`, `OwnershipOfferDeclined`, each with a `<comment>`.
  - [ ] `components.css`: `.ownership-section__declined`, with tokens only and logical properties.
  - [ ] `OwnershipOfferDialog.razor`: the in-Space `404` shows the no-longer-open state and keeps the re-read (Q5).
  - [ ] `FakeYelloApi.OwnershipOffers.cs`: the `declined` read, plus a settable settled cause for the dialog theory.
- [ ] **Task 6: tests** (all ACs). See Testing requirements.
- [ ] **Task 7: gates** (AC1, AC8, AC9): the four-route capability gate, the route/surface gate, the contract-shape pin, `Every_OwnershipOffer_state_transition_composes_the_actionable_predicate`, and `No_scheduled_component_reaches_an_offer_or_Invitation_state_write`.
- [ ] **Task 8: plants.** Save each original in memory and restore it in a `finally`. **Never** use `git checkout --`, `git restore` or `git reset`. Run them in small groups (the low-memory reaper killed a 5.1 batch and a 5.2 batch). Stop if a planted build fails, so stale binaries never pass for green. Record each plant in the Debug Log table. After any killed run, grep for the plant marker.
- [ ] **Task 9: ledger, docs, verification**
  - [ ] Ledger dispositions (below): one line per CLOSED/UPDATED/RE-OWNED note, no TAB or FORM FEED, keep the final newline. Write the text; do not leave blank lines (the 5.2 review's finding).
  - [ ] `TESTING-CONVENTIONS.md`: the R12 section (AC10).
  - [ ] Dated notes (2026-11-xx, story 5.3) in `ARCHITECTURE-SPINE.md`:
    - AD-26: the 409 goes to the party the transition belongs to (Q2/Q3); the revoke's `LostMembership`; the Owner's `declined` read (Q1).
    - AD-27: the A-10 gate's concrete predicate.
  - [ ] `sprint-status.yaml`: this story goes to `review` at the end of dev. `epic-5` stays `in-progress`.
  - [ ] Verification follows CLAUDE.md:
    - `docker info --format '{{.ServerVersion}}'`, then `dotnet build Yello.slnx` (0 warnings, 0 errors).
    - Iterate with `--filter-class` on the affected classes only.
    - Then **exactly one** full run in the background without the browser suite: `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`, logged to `artifacts/test-runs/5-3-decline-revoke-lapse-and-the-states-that-are-no-longer-open-develop.log`.
    - Run the affected browser classes filtered (`OwnershipOfferTests`, `OwnershipSectionTests`, `TextSpacingTests`, and any class whose request counts moved).
    - Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

5.2 already lets the named recipient decline, and every transition already refuses a settled offer with `409 ownership-offer-not-pending`. This story closes the remaining gaps:

- It **tells the Owner**: an Owner-only read says whether the Space's latest offer was declined, and the Ownership panel states it in plain words above the picker until a new offer is made.
- It **completes the proof** that every settled state answers the party it belongs to with a `409`, and anyone without a Membership with the byte-identical boundary `404`. It fixes the one path that broke that rule: a revoke whose caller's Membership ended while it waited on the lock.
- It **turns AD-27's "no timer writes expiry"** into a gate with a concrete predicate, and writes down once how a test reaches expiry: by seeding the past on the database clock.
- It **makes the dialog's in-Space `404`** show the same designed "This offer is no longer open." state as its `409`.

### What exists today (survey at `dca9ee3`)

- **Domain:** `OwnershipOffer` (`Id`, `SpaceId`, `RecipientMembershipId`, `OfferedByMembershipId`, `OffererName`, `State`, `ExpiresAt`, `CreatedAt`; no foreign key to `Membership`/`Account`). `OwnershipOfferState { Pending, Accepted, Declined, Revoked, Lapsed }`, stored as a string. There is no "answered at" column; Q1 (a) needs none.
- **Application:** `IOwnershipOfferStore` (`OfferAsync`, `ReadPendingAsync`, `RevokeAsync`, `ReadMineAsync`, `AcceptAsync`, `DeclineAsync`). Outcome enums `OwnershipOfferOutcome`, `OwnershipOfferRevocation { Revoked, NotPending, NotFound, NoLongerOwner }`, `OwnershipOfferAcceptance`, `OwnershipOfferDecline { Declined, NotPending, NotFound, LostMembership }`. Handlers: `OfferOwnership`, `ReadOwnershipOffer` (the name through `IMembershipStore.DisplayNameOfAsync`), `RevokeOwnershipOffer`, `ReadMyOwnershipOffer`, `AcceptOwnershipOffer`, `DeclineOwnershipOffer`.
- **Infrastructure:**
  - `OwnershipOfferStore.cs`: `OfferAsync:68`, `ReadPendingAsync:125`, `RevokeAsync:134` (the AC4 defect is at `:154-166`), `ActorRoleAsync:176` (no explicit `SpaceId`).
  - `OwnershipOfferStore.Answers.cs`: `ReadMineAsync:15`, `AcceptAsync:43`, `SwapAsync:71`, `DeclineAsync:131`, `NamingTheCaller:151`, `MissAsync:162`.
  - `OwnershipOfferWrites.LapseNamingAsync` (raw SQL, `State = Pending`).
  - `OfferPredicates.OwnershipOfferActionable:36` and `OwnershipOfferPendingButExpired:47`.
  - `ApplicationLocks.TakeMembershipsAsync`.
- **Host:**
  - `OwnershipOfferEndpoints.cs`: the offer, the pending read and the revoke under `OfferOwnershipTransfer`; the revoke switch is at `:159-167`; `NotPending()`.
  - `OwnershipOfferAnswerEndpoints.cs`: `mine`, acceptance and decline under `ReadProjectsAndTasks`; the switches are at `:98-111` and `:141-149`.
  - `MembershipLost`, `BoundaryProblems.NotAvailable()`, `QualifiedRefusal`, `NotAvailableFloor`.
- **Client:**
  - `OwnershipSection.razor` (653 lines; the pending read `:271`, the revoke answer `:605-643`).
  - `OwnershipOfferDialog.razor` (the answer handling `:278-335`, the in-Space `404` at `:325-332`).
  - `OwnershipOfferState`, `ContextBar.razor`; `ClientCopy.resx` (`ownership-offer-not-pending` `:1538`, `OwnershipOfferNoLongerOpenDetail` `:1586`, `OwnershipOfferRevoked` `:1530`).
- **Tests:**
  - Slices partials: `ActiveSpaceEndpointTests.{OfferOwnership,OfferOwnershipOutbox,OfferOwnershipRace,OwnershipOfferLapse,OwnershipOfferSchema,RevokeOwnershipOffer,ReadMyOwnershipOffer,AcceptOwnershipOffer,AcceptOwnershipOfferRace,DeclineOwnershipOffer,RoleMatrix}.cs`.
  - `WireDatabase` (`AddOwnershipOfferAsync`, `OwnershipOffersInAsync`, `SpacesNotHoldingExactlyOneOwnerAsync`, `AllSpaceIdsAsync`, `MembershipRowsInAsync`, `DatabaseNowAsync`, `SetInvitationExpiresAtAsync:641`), `OwnershipOfferWire`, `OwnershipOfferWire.Recipient.cs`.
  - Isolation: `IsolationCases.OwnershipOffers.cs`, `.OwnershipOfferAnswers.cs` (pending offers only).
  - Architecture: `RefusalBoundaryGateTests.OwnershipOffers.cs` (`:58`, `:81`, `:102`, `:176`, `:246`, `:306`, `:323`, `:372`), `.AcceptOwnershipOffer.cs` (`:53`, `:80`, `:115`, `:163`, `:183`), and `RefusalBoundaryGateTests.cs:733` (the timer ban).
  - Client: `OwnershipOfferTests.cs` (16 methods), `OwnershipSectionTests.cs` (9), `FakeYelloApi.OwnershipOffers.cs`, `FakeYelloApi.OwnershipOfferAnswers.cs`.

### Locking and the races (RCSI)

- Accept, decline, revoke, offer, Role change, removal and leave all take `Yello/Memberships/{spaceId}` **Exclusive** first. So every race here serialises, and a request that waited sees, in its post-lock statement snapshot, everything that committed before the grant.
- The pipeline's Membership check runs **before** the lock. A Membership can therefore end between the pipeline and the store. That gap is exactly why every store miss checks the caller's own Membership first (AC4/AC5).
- The `declined` read takes no lock, like the pending read.

### Open questions

Each has a recommendation, which is the default. **[LEE]** marks a question for Lee. Otherwise the dev phase decides each one as a reversible, recorded decision.

1. **[LEE] How the offering Owner is told it was declined** (AC1). FR-42 requires it. FR-43 forbids any email on decline (`prd.md:671`). No UX surface specifies it (`EXPERIENCE.md:216`, `:263`, and the mockup `space-settings.html:136-137` show only the pending state). There is no push before Epic 7.
   - **(a) Recommended.** A new Owner-only read, `GET .../ownership-offers/declined` → `200 DeclinedOwnershipOfferResponse(Id, RecipientName?)`/`204`, true while the Space's latest offer is `Declined`. The Ownership panel shows "{name} declined your offer of ownership." above the picker until a new offer is made. Cost: one more route (with gates and isolation rows), one more `GET` when no offer is pending, and copy that UX has not specified (ledger it for UX).
   - (b) Widen the existing pending read into a "latest offer" answer carrying a `State`. Cost: it changes 5.1's pinned five-member contract, its gates and its tests, and every pending-read consumer must learn a new shape.
   - (c) Email the Owner. Not allowed: FR-43 says "exactly one email per offer" and "declining it ... sends no further email".
   - (d) An accent element in the Owner's context bar. Not allowed: the Offer indicator is the only accent element in chrome (UX-DR9).
2. **A Member the offer does not name, on a settled offer** (AC3). **(a) Recommended:** keep 5.2's Q7, the unrecorded in-Space `404`, byte-identical. The `409` goes to the party the transition belongs to; add a dated AD-26 note. (b) `409` to any Member. Cost: any Member can probe whether an offer id exists, and it reverses a decision Lee's cycle already applied.
3. **A non-Owner Member calling revoke on a settled offer** (AC3). **(a) Recommended:** keep the pipeline's recorded `403` for `offer-ownership-transfer`. The capability is judged before any state, as for every Owner-only route. Record it as the reading of AC3. (b) Evaluate the state first. Not acceptable: it reverses "capability before state" for one route.
4. **`OfferStale`** (ledger `:946`). It is reachable only through a seeded row whose offerer is no longer Owner. **(a) Recommended:** keep the rollback and the `409`, and close the entry as unreachable through the product: ownership moves only by accepting the latest offer, and account deletion (5.4) refuses an Owner. (b) When the demote writes zero rows, write the offer `Lapsed` and commit with no Role change. Cost: a third `Lapsed` writer for a state no request can produce; it widens the writer gate and the A-10 exceptions.
5. **The dialog's in-Space `404`** (AC6, ledger `:953`). **(a) Recommended:** show the no-longer-open state and keep the re-read. If the Space refused, the arrival renders the refusal surface. (b) Keep the silent close. Cost: a dialog that vanishes is the "silent" failure NFR-8 forbids.
6. **A-10's concrete predicate** (AC9). **(a) Recommended:** the call-graph gate from scheduled roots to the state writers, plus "`OwnershipOfferPendingButExpired` is referenced only by `OfferAsync`", with the two vacuity anchors. (b) Rely on the existing timer ban and record that it implies A-10. Cost: the test design asks for a concrete predicate precisely because the timer ban is a different rule, and lifting it for an unrelated reason would silently drop A-10.
7. **Seeding the past** (AC10). **(a) Recommended:** add `WireDatabase.SetOwnershipOfferExpiresAtAsync` (move a product-made row) and state the convention once in `TESTING-CONVENTIONS.md`. (b) Keep raw expired inserts only. Cost: a raw row skips the guarded insert, so the case proves less.
8. **`SpaceId = @space` in the offer statements** (ledger `:948`). **(a) Recommended:** add it to the statements this story touches (`ActorRoleAsync`, `MissAsync`'s offer read, `SwapAsync`'s offer read, the new `declined` read) and close the entry. (b) Leave it with epic 5. Cost: none beyond leaving AD-2's "belt and braces" unfinished on the very statements this story edits.
9. **`OwnershipOfferPendingButExpired` restates the predicate** (ledger `:932`). **(a) Recommended:** close it as accepted. Both expressions sit in one file, `The_ownership_offer_actionable_predicate_lives_beside_the_Invitations` holds that, and composing a negated expression needs an expression visitor for no change in behaviour. (b) Introduce the visitor and compose `!(ExpiresAt > Now())`.
10. **The live push of a decline** (AC1). **(a) Recommended:** the Owner is told at the next read of the panel. Update ledger `:937` (owner 7.7) to name the declined statement too. (b) Re-read on every request's answer. Cost: an extra read per request for a rare state; AD-10's spirit.

### Contradictions found (record; do not resolve beyond what is stated)

- **C1.** AC3's "a caller holding a Membership ... 409, never 404" versus 5.2's in-Space `404` for a non-recipient (Q2). Read as "the party the transition belongs to".
- **C2.** AC3 versus the recorded `403` for a non-Owner on the revoke (Q3).
- **C3.** "Lost to a concurrent offer hitting the filtered index" is listed with the settled states. In the code it is a second **offer** refused `409 ownership-offer-pending` (a different stable type), and no row is written. Both are `409` with a stable `type`, which is what AD-26 requires.
- **C4.** FR-42 says the Owner "is told"; FR-43 says nothing is emitted on decline; no UX surface exists (Q1).
- **C5.** AC7 says "no job and no timer writes the lapsed state", yet a new offer writes `Lapsed` on an expired row. These are consistent: that write is on a request path, caused by the new offer (AD-27 note 2026-11-08). The A-10 gate names it as the one exception.
- **C6.** The SPEC still says the offer is not emailed (ledger `:912`, owner PM/Lee). It does not affect this story: decline and lapse send nothing either way.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:746` → **CLOSED by 5.3**: `No_scheduled_component_reaches_an_offer_or_Invitation_state_write` (the concrete predicate) and `Every_OwnershipOffer_state_transition_composes_the_actionable_predicate`.
- `:940` → **CLOSED by 5.3**: the Owner is told (the `declined` read and the panel statement), the settled-state matrix, the non-member `404` rows and the A-10 gate.
- `:946` → per Q4. Under (a): **CLOSED (accepted)**, unreachable through the product.
- `:947` → **CLOSED by 5.3**: `OwnershipOfferRevocation.LostMembership` → the recorded boundary `404`, with the revoke race.
- `:953` → **CLOSED by 5.3**: the in-Space `404` shows the designed state (Q5 (a)).
- `:948` → per Q8. Under (a): **CLOSED** for the statements touched; name any statement left.
- `:932` → per Q9. Under (a): **CLOSED (accepted)**, with the reason.
- `:937` → **UPDATED**: the Owner's declined statement is likewise read, not pushed (owner 7.7).
- **New:** "The Owner's declined statement copy is the story's recommendation; no UX surface specified it" (owner **UX**; low).

### Architecture compliance (must)

- **AD-21 ring.** The slice goes under `Yello.Application/Spaces/ReadDeclinedOwnershipOffer/`, the store in Infrastructure, the endpoint in Host and the contract in `Yello.Contracts/Spaces/`. Check `AllowedReferenceEdges.cs`.
- **AR-3 / AD-1.** Slices never read a Role, set a key, record idempotency or record refusals.
- **AD-2.** No raw SQL outside Infrastructure (the test helper is in `Yello.Tests.Shared`, as `SetInvitationExpiresAtAsync` is). Statements carry `SpaceId = @space` (Q8).
- **AD-3 / AD-20.** Every not-found is the boundary body. Only the pipeline records (`MembershipLost` is how a store asks it to).
- **AD-10 / AR-33.** No timer, no hosted service, no polling.
- **AD-18.** Keys deduplicate, and the guard refuses (AC8).
- **AD-23.** No address in any new response, log or text.
- **AD-26 / AR-13.** Identity for accept and decline; `409` with a stable type to the transition's party; `404` to a caller with no Membership; Space first, state second.
- **AD-27 / AR-31.** One predicate, in the statement, on the database clock; nothing scheduled writes a state.
- **AR-34.** `DateTimeOffset` in UTC, problem+json.
- **Code comments** (CLAUDE.md): they say why. No story numbers, no AC/Q/ledger ids, no history, no XML doc that restates a member name. The 5.1 and 5.2 reviews each patched comment violations; write them clean.

### Library / framework

No new package, no new project and no migration.

- EF Core 10.0.11: `ExecuteUpdateAsync`, `AsNoTracking` projections, `OrderByDescending(...).ThenByDescending(...).Take(1)` before a filter (write it as a subquery or project the top row and test its state in C#; either translates).
- ASP.NET Core 10.0.11 minimal APIs.
- Blazor WebAssembly 10.0.11.
- Microsoft.Data.SqlClient 7.0.2.
- SQL Server 2025 (`sp_getapplock` grants exclusive waiters in queue order).
- xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3, and Mono.Cecil (already used by the IL gates) for the call-graph walk.

No web research was needed: every API is already in the codebase.

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-5-3-decline-revoke-lapse-and-the-states-that-are-no-longer-open.md`. All written against Q1-Q10's recommendations (a); a skip that depends on one names it.

| File | Cases | ACs | State today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeclinedOwnershipOffer.cs` (new) | 5 | AC1, AC7 | all red (skipped, Q1) |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.SettledOwnershipOffer.cs` (new) | 8 methods, 12 cases | AC1 (no publish, no mail), AC2 (3), AC3 (decline theory x5, superseded), AC5 (replay), AC7 | all green today, active |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.SettledOwnershipOfferRace.cs` (new) | 2 | AC4, AC5 | decline-vs-removal race green, active; revoke-after-leaving race red (skipped) |
| `.AcceptOwnershipOffer.cs` / `.RevokeOwnershipOffer.cs` theory data | +1 row (`Accepted`) / +2 rows (`Declined`, `Accepted`) | AC3 | green today, active |
| `tests/Yello.Tests.Isolation/IsolationCases.SettledOwnershipOffers.cs` (new; spread from `OwnershipOfferCases()`) | 9 rows | AC4 (6 settled/former-member rows), AC1 (3 declined-read rows) | all `PendingUntil: "5.3"`; the 6 AC4 rows are expected green today but unverified |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SettledOwnershipOffers.cs` (new) | 8 | AC1 (4 gates), AC4 (`LostMembership`), AC8, AC9 (2) | 5 red (skipped); the AC8 and both AC9 gates green today, active |
| `tests/Yello.Tests.Client/OwnershipSectionTests.cs` (5 added) | 5 | AC1 | all red (skipped, Q1/Q10) |
| `tests/Yello.Tests.Client/OwnershipOfferTests.cs` (3 added) | 3 methods, 7 cases | AC6 | in-Space 404 red (Q5); the decline-409 case and the cause theory expected green but unverified (skipped) |

Support: `tests/Yello.Tests.Shared/OwnershipOfferWire.Declined.cs` (route stand-ins `DeclinedRoute`/`DeclinedOf`: swap for the contract members and delete them), `WireDatabase.SetOwnershipOfferExpiresAtAsync` (Q7 (a), done), `tests/Yello.Tests.Client/FakeYelloApi.DeclinedOwnershipOffers.cs`.

**Unverified.** An Isolation run was stopped by Claude Code for low system memory and was not restarted, and no browser run was made. So eight scaffolds that should pass today stay red: the six AC4 isolation rows and the two AC6 browser cases. Make them live first.

**A correction to this story:** the "former" isolation rows cannot use a Token issued in the Space they left, because removing a Membership cascades its Tokens away (a `401`, not a non-member's `404`). They use a Token bound to the Account's own Space.

**Activation order (riskiest assumption first):**
1. `A_revoke_queued_behind_the_acceptance_and_its_offerers_leave_is_the_recorded_boundary_404`: three queued waiters granted in order, and the store reads the caller's Membership before the offer. It should be seen `409` before the fix.
2. `The_revoke_outcome_names_a_lost_Membership`, then the mapping.
3. `A_decline_followed_by_a_later_offer_that_was_revoked_reads_204`: top row first, state second.
4. The rest of `.DeclinedOwnershipOffer.cs`; the four AC1 gates (replace `OfferOwnershipTransfer_is_required_by_exactly_the_three_offer_routes`, widen the route/surface gate); the 9 isolation rows made live (the 6 AC4 rows can go first, before any code).
5. The 5 `OwnershipSectionTests` cases (widen any arrival-request counts deliberately).
6. The 3 `OwnershipOfferTests` cases (the two unverified ones first; then the in-Space `404`).

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`: `:44-86` traits, `:87-207` rules (races `:102-113`), `:517-543` outbox, `:545-580` Memberships, `:604-673` the shared container, `:773-895` isolation.

- **Traits:** `Suite`, `Priority` (the 404-not-409 rows and the revoke race are P0), `Requirement` (FR-42, FR-43, FR-8, AD-26, AD-27, AD-3, AR-13, AR-31, NFR-1, A-10), `Assumption` (`PRD-12-3` on the expiry cases), `Tier`. Use underscore-sentence names.
- **Never `Task.Delay`.** Races wait on `LCK_M_%` (`WaitUntilBlockedOnALockAsync`). Racing Accounts are made present and fetch an anti-forgery token first.
- **Seed through the product.** Seed directly only for a terminal state the product cannot reach with the offerer still Owner (`Accepted` for the revoke row) and for expiry (AC10). Record which.
- **S-2 after every case** (`SpacesNotHoldingExactlyOneOwnerAsync`).
- **Mail:** wait for the offer's own email first, then act, then assert no new mail **to this address**. A case that waits on mail calls `SoleDispatcherWireAsync()` first and runs its own `HostProcess.RunAsync`.
- **No new container-backed test class.** Use Slices partials of `ActiveSpaceEndpointTests` (`DeclinedOwnershipOffer.cs` is new, plus additions to the existing offer partials), handler unit tests with fakes (no database), rows in the two isolation files, and the Client classes `OwnershipOfferTests` and `OwnershipSectionTests`.
- **Every absence has an anchor and is seen red under a plant.** A masked plant is recorded, never deleted.
- **Memory budget (CLAUDE.md).** One `dotnet test` at a time; iterate with `--project ... -- --filter-class "<full name>"`; one full run in the background with `--max-parallel-test-modules 1`, without the browser suite in Develop.

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | Remove the revoke's `LostMembership` branch | the revoke-after-leaving race (`409` to a non-member) |
| 2 | Move the revoke's existence read before `ActorRoleAsync` | the same race |
| 3 | `MissAsync` decides "named the caller" without the caller's Membership first | the decline-versus-removal race (`409`) |
| 4 | `DeclineAsync` without `.Where(OwnershipOfferActionable)` | the transition-predicate gate; the settled-decline theory (`204`) |
| 5 | The `declined` read filters on `Declined` before taking the latest row | the "decline, then a later revoked offer" case (`200` instead of `204`) |
| 6 | The `declined` read without its capability (or under `ReadProjectsAndTasks`) | the four-route capability gate; the Admin/Member/Viewer `403` case |
| 7 | An unregistered `BackgroundService` calling `LapseNamingAsync` | the A-10 gate and the timer ban |
| 8 | A method passed to `new Timer(...)` that calls the lapse | the A-10 gate |
| 9 | A second reference to `OwnershipOfferPendingButExpired` outside `OfferAsync` | the A-10 exception clause |
| 10 | The dialog's in-Space `404` closes silently again | the in-Space `404` browser case |
| 11 | The declined statement rendered with `role="alert"` | the panel browser case |
| 12 | An email enqueued on decline (an `IOutbox` on the decline handler) | `The_ownership_acceptance_and_decline_take_no_outbox`; the decline no-mail case |

### Previous story intelligence (5.2, 5.1)

- **5.2:**
  - Decline was built here on Lee's Q3 (a), so 5.3's first AC is split across the two stories.
  - The scaffolds did not show their own red because the code came first; the plants supplied the red-green step. Activate the scaffolds one at a time instead.
  - Four scaffolds were corrected against observed answers. One example: renaming a Space is **not** Owner-only, because Admins hold `RenameSpace`; use `DeleteSpace` for an Owner-only refusal.
  - The revoke's actor clause is LINQ `Any` under the lock, not `READCOMMITTEDLOCK` SQL. That is sound under RCSI because the statement's snapshot starts after the lock is granted.
  - S1200 forced a new endpoints class (`OwnershipOfferAnswerEndpoints`).
  - The review found the ledger dispositions written as blank lines, and comments carrying ids: write both properly.
  - One real-host browser flake passed on a rerun.
- **5.1:**
  - The written lapse runs under the lock before the `DELETE`.
  - The state-writer gate reads raw SQL as well as IL.
  - `PopulateSpaceAsync` makes an offer, so populated Spaces have a pending offer naming a Member. The new `declined` read and statement may surface in cases that populate a Space and then decline or revoke.
  - Its develop plant batch was killed by the reaper; run plants in small groups.
  - The first browser-suite run caught a request-count case that the new section disturbed.
- **4.5 / 4.7:** `LostMembership` → the pipeline's recorded boundary `404`. A case that stops its Host before its own emails are sent leaks them into the next case's drop.

### Git intelligence

Recent commits:

- `dca9ee3`: 5.2 review closure (the ledger dispositions written).
- `1ea4a9c`: 5.2 implementation. It added `OwnershipOfferStore.Answers.cs`, `OwnershipOfferAnswerEndpoints.cs`, `OwnershipOfferDialog.razor`, `OwnershipOfferState.cs`, the three recipient routes, the accept/decline Slices partials, the isolation rows, and the A-14, allow-list and offer-indicator gates.
- `2f3faf6`: 5.2 ATDD scaffolds.
- `3328f7f`: the 5.2 story.
- `89b98a3`: 5.1 review (the state-writer gate sees raw SQL).

The cycle: story, then decisions recorded, then ATDD scaffolds (`Skip = "red: ..."`, `atdd-checklist-<key>.md`, `PendingUntil: "5.3"` rows that must all be live before review, GATE-003), then implementation, then review fixes. The orchestrator commits each.

### Project Structure Notes

- **New (expected):**
  - `Yello.Contracts/Spaces/DeclinedOwnershipOfferResponse.cs`
  - `Yello.Application/Spaces/ReadDeclinedOwnershipOffer/ReadDeclinedOwnershipOfferHandler.cs`
  - `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeclinedOwnershipOffer.cs`
  - Optionally `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Expiry.cs`
- **Updated (expected):**
  - Contracts: `OwnershipOfferRoutes.cs`.
  - Application: `IOwnershipOfferStore.cs`.
  - Infrastructure: `OwnershipOfferStore.cs`, `OwnershipOfferStore.Answers.cs` (Q8 only).
  - Host: `OwnershipOfferEndpoints.cs` (or a sibling, because of S1200), `Program.cs`.
  - Client: `OwnershipSection.razor`, `OwnershipOfferDialog.razor`, `ClientCopy.resx`, `components.css`.
  - Tests: `WireDatabase.cs` (`SetOwnershipOfferExpiresAtAsync`), `FakeYelloApi.OwnershipOffers.cs`, `IsolationCases.OwnershipOffers.cs`, `IsolationCases.OwnershipOfferAnswers.cs`, `RefusalBoundaryGateTests.OwnershipOffers.cs`, the Slices offer partials (`AcceptOwnershipOffer.cs`, `DeclineOwnershipOffer.cs`, `RevokeOwnershipOffer.cs`, `OwnershipOfferLapse.cs`, `AcceptOwnershipOfferRace.cs` or a new race partial), `OwnershipOfferTests.cs`, `OwnershipSectionTests.cs`, `TextSpacingTests.cs`.
  - Docs: `tests/TESTING-CONVENTIONS.md`, `ARCHITECTURE-SPINE.md`, `deferred-work.md`, `sprint-status.yaml`.
- **No new project, no new package, no new container-backed test class, no migration.**

### References

- `epics.md`: `:201` (AR-13), `:219` (AR-31), `:221` (AR-33), `:378` (coverage), `:459-467`, `:1924-1973` (5.1), `:1975-2022` (5.2), `:2024-2065` (5.3), `:2098-2100` (5.4's lapse).
- `prd.md`: `:64`, `:122`, `:213-227` (FR-8), `:229-241` (FR-42), `:663-672` (FR-43, `:671`); `addendum.md:40`, `:151`.
- `ARCHITECTURE-SPINE.md`: AD-5 note `:115`; AD-26 `:251-260`; AD-27 `:262-267`.
- `EXPERIENCE.md`: `:110-111`, `:204`, `:216`, `:261-263`, `:438`, `:541-553`. Mockups: `ownership-offer.html:138-158`, `space-settings.html:136-137`, `:149`.
- `specs/spec-yello/`: `SPEC.md:64-66`, `acceptance-criteria.md:83-92`, `role-capability-matrix.md:24`.
- `test-design-qa.md`: `:172`, `:185` (R12), `:197`, `:265-266` (I-1, I-2), `:308` (A-10), `:319` (S-2), `:494-496`. `test-design-architecture.md`: `:237` (R12), `:319-327` (the concrete predicate).
- `tests/TESTING-CONVENTIONS.md`: `:44-86`, `:87-207`, `:517-543`, `:545-580`, `:773-895`.
- `deferred-work.md`: `:746-748`, `:910`, `:932`, `:937`, `:940`, `:944-955`.
- Prior stories: `5-2-accept-an-ownership-offer.md` (whole; Q3, Q5, Q7, the review findings), `5-1-offer-ownership-of-a-space-and-tell-the-recipient.md` (AC4, AC5, AC7, AC12, Q3, Q6).

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

- 2026-11-21: Story created (bmad-create-story). Status ready-for-dev.
