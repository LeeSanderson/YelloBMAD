---
baseline_commit: 07ff06fc1e9cf5a0233f4c8ac891be6342549570
---

# Story 5.4: Delete your Account

Status: done

Epic: 5 — Hand a Space over and leave cleanly (fourth and last story)
Story key: `5-4-delete-your-account`

**Requirements owned.** From the Story Coverage Index (`epics.md:379`): **FR-3**, **AR-29**, **UX-DR10** (the avatar's tombstone state), **UX-DR36** (the "Account deletion refused" rung of the destructive ladder). Also carried here:

- **§6.2** (`prd.md:770-774`, `epics.md:179`): deletion is irreversible, and deleting an Account never deletes another Account's work.
- **FR-8 / AD-5 / AD-26**: a pending offer naming the Account's Membership **lapses** in the deletion; the offering Owner's deletion is still refused (`prd.md:222-223`).
- **FR-21 / AD-5 note 2026-11-08** (`prd.md:415`): Tasks the Account was Assignee of are unassigned, never deleted.
- **FR-12** (`prd.md:291`): an Invitation survives its issuer's Account deletion and stays revocable by any remaining Owner or Admin.
- **FR-36 / AD-6** (`prd.md:615`): Account deletion invalidates its Tokens. Test design **V-3** (`test-design-qa.md:282`, P0) and **V-11** (`:351`, the deleted Account's Sessions end).
- **AD-24 / AR-29 / I-11** (`ARCHITECTURE-SPINE.md` AD-24 rule and amendment 2026-10-29; `test-design-qa.md:275`, P0): the refusal lists Space **identity only** (id and name), and only Spaces the caller **owns**.
- **S-2** (`test-design-qa.md:319`, P0): no Space ever holds zero or two Owners.
- **NFR-6 / FR-2**: a deleted Account's Session grants access to nothing.

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Covered by |
|---|---|---|
| `:250` | `RehashAsync` discards its rowcount, so a rehash against a deleted Account still issues a Session | AC11 |
| `:338` | `AccessRefusal` keeps the deleted Account's id for up to 90 days | AC9, Q6 |
| `:385` | Account deletion invalidates its Tokens only if the Memberships go first; add V-3's case | AC1, AC12 |
| `:393-394` | `REV-ACCOUNT-DELETED` still owes its endpoint-driven twin | AC12 |
| `:424` | The dead-cookie `401` also clears the cookie (not byte-identical to no credential) | Q9 |
| `:687`, `:787` | Unit-of-work dependency allow-lists (creation unit, Invitation unit) re-owned to "the next Account-scoped writer" | Q10 |
| `:717` | One cascading `DELETE` of a very large Space can escalate locks | Re-own (Account deletion never deletes a Space; see Q10) |
| `:767` | The issuer-deleted arm of FR-12: `Invitation.IssuedByAccountId` is `Restrict` | AC4, AC5, Q3 |
| `:790` | A deleted Account's still-valid Session is told the Invitation is for another Account | AC11 |
| `:849` | Unassign the Account's Tasks in every Space, under each Space's Memberships lock, before its Memberships go | AC3 |
| `:910` | Lapse the pending offers naming each Membership; refuse an offering Owner | AC2, AC6 |
| `:921` | The pending read answers `204` when the recipient's name cannot be read | AC6 (close: no deletion path skips the lapse) |

The remaining entries owned by **"epic 5"** (`:914`, `:918`, `:922-925`, `:929-930`, `:933`, `:944-945`, `:951-952`, `:954`, `:961`, `:964-965`, `:966-967`, `:969`, `:971`) are low and are **not** in this story's scope. This is the epic's last story: leave them for the epic-5 retrospective (or the sweep) and say so in Completion Notes. Do not touch them.

## What already exists — do not rebuild it

| Concern | Where it lives today | Notes |
|---|---|---|
| Account row, hard-delete intent | `Yello.Domain/Accounts/Account.cs` (remarks: "Account deletion is story 5.4's and is a hard delete; there is deliberately no `DeletedAt` column") | **Keep it a hard delete.** No soft delete, no `DeletedAt`, no filter on the email index (`SpaceIsolationSchemaTests.cs:349-375` pins the unfiltered index). |
| Leaving a Space, in one Space-scoped transaction | `MembershipStore.LeaveAsync` (`Yello.Infrastructure/Persistence/MembershipStore.cs:178-217`): Exclusive `Yello/Memberships/{spaceId}` lock → read own row → `UnassignAsync` → `OwnershipOfferWrites.LapseNamingAsync` → guarded `DELETE ... AND Role <> 'Owner'` → `RemovedAsync` (deletes `SpacePresence`, records `MembershipChanged`) | This is exactly the per-Space work Account deletion needs (AC1-AC3, AC6). **Reuse it; do not write a second unassign or lapse.** `RemovalOutcome { Removed, LostMembership, OwnershipHeld, NotFound, BeyondAuthority }`. |
| Unassign | `MembershipStore.Assignees.cs:114-121` (`UnassignAsync`, private) | The Assignee key is NO ACTION (AD-5 note 2026-11-08). |
| Written lapse | `OwnershipOfferWrites.LapseNamingAsync` (`OwnershipOfferWrites.cs:158`) | Only `OwnershipOfferStore`/`OwnershipOfferWrites` may write `State` (gate `RefusalBoundaryGateTests.OwnershipOffers.cs:176`). |
| Tokens die with the Membership row | `FK_ApiToken_Membership_BoundSpaceId_AccountId ON DELETE CASCADE` (`ApiTokenConfiguration.cs:60-64`) | `Deleting_the_Account_with_its_Memberships_invalidates_its_Tokens` (`ActiveSpaceEndpointTests.Tokens.cs:470`) proves the schema with raw SQL. |
| A Space-scoped unit of work for a given Space | `IActiveSpaceResolver.BeginAsync(accountId, spaceId)` → `MembershipActiveSpaceResolver.BeginAsync` (`MembershipActiveSpaceResolver.cs:64-109`): transaction, `SpaceSessionContext.SetAsync`, scope narrowed, Membership read; commit through `UnitOfWork.CompleteAsync` and `CommitHooks.Committed()` (outbox signal, `MembershipChangePublisher`) | It is already one of the four permitted `SpaceId` setters (`SpaceSwitcherGateTests.Invitation.cs:36`). Reusing it adds **no** setter. |
| An Account-scoped unit of work | `AccountSpaceDirectory.InAccountScopeAsync` (`AccountSpaceDirectory.cs:273-299`): transaction, `SpaceSessionContext.SetAccountAsync`, `accountScope.Narrow` | The `AccountId` setters are pinned to exactly `[AccountSpaceDirectory, SpaceCreationScope]` (`SpaceSwitcherGateTests.cs:363`). |
| RLS on `Membership` | FILTER `fn_MembershipVisibilityPredicate(SpaceId, AccountId)`: SpaceId key **or** AccountId key (`20261024143930_AccountScopedMembership.cs:52-64`); BLOCK AFTER INSERT/UPDATE are Space-only; no BLOCK on DELETE | Under the `AccountId` key the Account's own Memberships in every Space are readable — that is how the owned-Space read works. `Task`, `OwnershipOffer`, `SpacePresence`, `Invitation` are **Space-key only**. |
| The Account's lock | `ApplicationLocks.TakeSpacesPerAccountAsync` = `Yello/SpacesPerAccount/{accountId}` (`ApplicationLocks.cs:236`, `:312`), taken by Space creation | Every Space-named taker requires `ScopedSpaceId`; there is **no** helper that takes a Space lock for an arbitrary id. |
| Account-scoped routing | `.AccountScoped(AccountScopedSurface.AccountSettings)` (`Yello.Host/Spaces/AccountScopedResolution.cs:45`); Session only (the Token scheme authenticates nothing without `{spaceId}`); `AccountScopedSurface.cs:22` already says "Account deletion is 5.4's" | **No new enum member**: deletion is part of the Account settings surface (AD-24 rule). |
| Account-scoped write without a key | `DELETE /api/v1/account/api-tokens/{tokenId}` (`ApiTokenEndpoints.cs:59-61`): no `Idempotency-Key`, no unit of work | Precedent for Q7. |
| Session ends when the Account row is gone | `SessionRevocation.ValidateAccountAsync` (`Yello.Host/Sessions/SessionRevocation.cs:125-139`): `AccountSessionStore.FindAsync` returns `null` → `RejectPrincipal()` + `SignOutAsync` → `401` with a cookie-clearing `Set-Cookie` | V-11 for the deleted Account's own Sessions holds by construction; prove it through the product (AC12). |
| Sign-out | `SignOutHandler` → `AccountSessionStore.EndSessionsAsync` (`SessionsValidAfter`); client `ContextBar.SignOutAsync` (`ContextBar.razor:353-379`) → `Session.SignedOutAsync()` | The client's post-deletion landing reuses `SessionState.SignedOutAsync(Func<Task> landing)` (`SessionState.cs:200`). |
| Account settings page | `Yello.Client/Pages/AccountSettings.razor` at `/account` (`ClientRoutes.cs:68`); API Token section only; header remark says "Profile, password and Account deletion are later stories'"; own `SendStateChangeAsync` (`:503`) with the anti-forgery retry; purge registered (`:198`) | Add the deletion section here. |
| Destructive confirm pattern | `SpaceDeleteSection.razor` (in-place confirm, `destructive-confirm`, heading "Delete {name}?", "This cannot be undone.", `button-danger` + Cancel, Esc cancels, "Not sent." on a 5xx) | Copy its shape, not its typed-name friction (Q4). |
| Refusal copy plumbing | `Yello.Contracts/ProblemTypes.cs` (42 `urn:yello:problem:*` constants; `OwnershipHeld` `:476`), `Yello.Client/Refusals/RefusalCopy.cs` (`OwnKeys` `:253-284`), `ClientCopy.resx` (`ownership-held` `:1370` already says "The Owner leaves by having an Ownership Offer accepted, or by deleting the Space.") | A refusal body carries extensions (`BoundaryProblems.cs:56-111`); the client reads them through `ProblemResponse` (`Yello.Contracts/ProblemResponse.cs:38`: `Type, Errors, Capability, Bound, Limit`) — a list needs a new member. |
| Space identity contract | `SpaceIdentityResponse(Id, Name)` (exactly two members, gated `SpaceSwitcherGateTests.cs:55`) | Reuse it for the owned-Space list. |
| Avatars | `Components/AssigneeAvatar.razor` (`span.assignee-avatar`), `MembershipRow.razor:39` (inline `.membership-row__avatar`); CSS `components.css:1957-1972`, `:2220-2233`; tokens `--surface-column` (`tokens.css:46`), `--text-muted` (`:50`) | **No tombstone state exists anywhere.** |
| Attribution that names an Account | `Invitation.IssuedByAccountId` (FK `Restrict`, `InvitationConfiguration.cs:128-131`) + `IssuerName` snapshot, rendered "Sent by …" in `InvitationRow.razor:25` and in the Invitation view (`InvitationOfferResponse.IssuerName`); `OwnershipOffer.OffererName` snapshot (no FK) | `Project`, `Task`, `Label` and `Status` have **no** author column; `TaskResponse` has no creator. So the Invitation issuer is the **only** authored content with an Account key today. Gate `Attribution_never_reads_a_live_Account_name` (`RefusalBoundaryGateTests.Memberships.cs:195`). |
| Test seams | `WireDatabase.DeleteAccountAsync` (`WireDatabase.cs:415-431`: AccountId key, delete Memberships, delete Account — fails `547` on an assigned Membership, ledger `:870`, owner sweep), `REV-ACCOUNT-DELETED` (`RevocationCases.cs:86-92`, raw SQL), the endpoint twins `REV-SPACE-DELETED-BY-OWNER` (`:74-84`), `REV-MEMBERSHIP-REMOVED-BY-ADMIN` (`:115-121`), `REV-LEFT-THE-SPACE` (`:123-129`) | Add the product-driven twin; keep the raw-SQL row as the schema proof. |

**What is genuinely new in 5.4:**

1. `DELETE /api/v1/account` and its slice: refuse while the Account owns any Space (naming them), otherwise leave every Space through the existing leave, then hard-delete the Account row.
2. A way for one request to run the leave in **several** Spaces without relaxing RLS (Q1 — the dominant finding below).
3. The issuer-deleted arm of FR-12: the `Restrict` key would make deletion fail today (Q3).
4. The tombstone avatar state and "a deleted Account" attribution (Q5).
5. The Account settings deletion section, its refusal rendering and its post-deletion landing.

**Explicitly not in this story:**

- Profile and password editing (Account settings' other items, not in any epic-5 AC).
- Deleting owned Spaces on the caller's behalf. The refusal states the exits; it never takes one.
- A live push to other Members' open tabs (`MembershipChanged` is recorded; subscribers arrive with 7.3/7.7).
- The low "epic 5" ledger entries listed above.
- No commit, push, branch, Azure login or deployment. The orchestrator commits.
- No new NuGet package and no new project without Lee (CLAUDE.md).

## Story

As someone leaving Yello,
I want to erase my Account without orphaning a Space or destroying anyone else's work,
so that leaving is genuinely available to me and costs nobody else what they have made.

## Acceptance Criteria

AC1 to AC8 are reproduced **verbatim** from `epics.md:2075-2109` and numbered by position. AC9 to AC14 come from the PRD, the architecture, the test design and the ledger, and each cites its source. The indented notes are implementation guidance; a note that depends on an open question names it. **[LEE]** marks a question for Lee. Otherwise the dev phase takes the recommendation and records it as a reversible decision.

**AC1 — an Account that owns nothing is erased from every Space** (FR-3 `prd.md:161`)
**Given** an Account owning no Space
**When** it deletes itself
**Then** every remaining Membership is removed and the Account disappears from every Space it belonged to

> - Route: `DELETE /api/v1/account` (`AccountRoutes.Account` or similar in `Yello.Contracts/Accounts/AccountRoutes.cs`), `.AccountScoped(AccountScopedSurface.AccountSettings)`, Session only, anti-forgery required. Success is `204`, and the response clears the Session cookie (sign the cookie scheme out, as sign-out does).
> - Shape (Q1 (a)): three steps, each its own transaction.
>   1. **Check** — Account-scoped unit (`AccountId` key), Account lock taken: read the Account's Memberships with `Role = Owner`, joined to `Space` for the name. Any → refuse (AC2). None → read the list of `SpaceId`s the Account holds a Membership in, and commit nothing.
>   2. **Leave each Space** — for each `SpaceId`, a Space-scoped unit of work through `IActiveSpaceResolver.BeginAsync(accountId, spaceId)` running `IMembershipStore.LeaveAsync(accountId)`, then completing. `LeaveAsync` already takes the Memberships lock, unassigns, lapses, deletes the row (`Role <> 'Owner'`), deletes `SpacePresence` and records `MembershipChanged`; the Token cascade runs in that `DELETE`. `LostMembership` (already gone) is fine; `OwnershipHeld` (the Account became an Owner since step 1, by accepting an offer) stops the sequence and answers AC2's refusal naming that Space.
>   3. **Erase** — Account-scoped unit, Account lock taken again: re-read the Account's Memberships. If any remain (an Invitation accepted between steps), run step 2 for them once more (one extra pass, then refuse with a stable `409` if still not empty — Q2). If an owned one appears, AC2. Otherwise delete the Account's `AccessRefusal` rows (Q6) and the `Account` row (exactly one row, else treat as already deleted); `AccountIdempotencyRecord` cascades.
> - The slice lives at `Yello.Application/Accounts/DeleteAccount/` (handler + port). The handler orchestrates; it never sets a key, reads a Role or records a refusal (AR-3/AD-1). The steps are behind one Application port (for example `IAccountDeletion`) implemented in Infrastructure, so session-context keys stay in Infrastructure.
> - "Disappears from every Space": after `204`, no `Membership`, `SpacePresence` or `ApiToken` row carries the `AccountId`; each former Space's Membership list (`GET .../memberships`) no longer lists it; every former Space still holds exactly one Owner (S-2).

**AC2 — an Owner is refused, naming every owned Space** (FR-3 `prd.md:160`; AD-5)
**Given** an Account owning one or more Spaces
**When** deletion is attempted
**Then** it is refused inside the same transaction that checks for owned Spaces
**And** the refusal **names every Space still owned**

> - `409` problem+json, new stable type `ProblemTypes.AccountOwnsSpaces` = `urn:yello:problem:account-owns-spaces`, with an extension `spaces: [{ id, name }]` ordered by name then id. Append a member to `ProblemResponse` (for example `IReadOnlyList<SpaceIdentityResponse>? Spaces`) so the client can read it; keep the existing members and their order.
> - **Identity only** (AD-24, I-11): id and name, nothing else — no Role, no count, no Membership. Only Spaces where the caller's own Membership is `Owner`; never a Space the caller merely belongs to, never anyone else's.
> - Step 1 refuses inside its own transaction and writes nothing — no Membership removed, no offer lapsed, no Session ended. The refusal is not an `AccessRefusal` (the caller is acting on itself; compare `ownership-held`, which is not recorded either).
> - An Owner with a **pending offer** is refused the same way: making an offer is not an exit (FR-8 `prd.md:223`). Prove it explicitly (ledger `:910`).
> - The race: an acceptance commits after step 1. Step 2's leave answers `OwnershipHeld` for that Space → stop, refuse naming it; Spaces already left stay left (Q2 records that cost). Prove it with a deterministic race (block the acceptance on the Memberships lock, `WaitUntilBlockedOnALockAsync`), never `Task.Delay`.

**AC3 — the refusal states exactly two exits** (UX-DR36; `EXPERIENCE.md:270`)
**Given** that refusal
**When** it renders
**Then** it states exactly two exits — transfer the Space and have the offer accepted, or delete the Space — and does not imply a third
**And** the reason is stated: because a transfer requires someone else to accept, deleting the Space is the only exit the Account controls unilaterally

> - Account settings renders: a statement that the Account owns Spaces and cannot be deleted yet; the list of every owned Space by name (each a link to that Space's settings, `ClientRoutes` Space settings of that id — Q8); the two exits in one or two sentences; the reason sentence. Copy in `ClientCopy.resx` with `<comment>`s. Suggested: "You own these Spaces, so your Account cannot be deleted yet." / "For each one, either offer ownership to someone in it and have them accept, or delete the Space." / "Deleting the Space is the only exit you control on your own, because a transfer needs someone else to accept."
> - No third exit: no "leave", no "archive" (EXPERIENCE.md bans the word), no "contact support", no "remove yourself".
> - Not a toast, not `role="alert"` with a reflex; a designed state in the section, focus moved to its heading (as 5.3's no-longer-open state). `RefusalCopy` gains the `account-owns-spaces` key.

**AC4 — Tasks unassigned, authored content retained** (FR-3 `prd.md:162-163`; FR-21 `:415`; §6.2)
**Given** deletion completes
**When** surviving data is inspected
**Then** Tasks the Account was Assignee of are unassigned rather than deleted, and content it authored in Spaces it did not own is retained with attribution rendering as a deleted Account

> - Unassignment is `LeaveAsync`'s existing `UnassignAsync`, per Space, inside that Space's transaction, under its Memberships lock (ledger `:849`). Prove: Tasks in two different Spaces assigned to the Account survive with `AssigneeMembershipId = NULL`, unchanged otherwise (title, Status, position).
> - Authored content today = Invitations the Account issued (the only Account-keyed authorship; see the table above). They survive, stay listed with their state, and stay revocable by any remaining Owner or Admin (FR-12 `prd.md:291`). Their issuer renders as a deleted Account (AC5). See Q3 for the key.
> - Include an issuer the Account had already **been removed from** the Space before deleting itself: its Invitations in that Space must survive too (this is why the key must be handled by the schema, not by step 2).
> - `OwnershipOffer.OffererName` snapshots on terminal offers are retained unchanged (no key; nothing renders them after settlement).

**AC5 — the tombstone** (UX-DR10; `DESIGN.md:464`; `EXPERIENCE.md:210`)
**Given** a deleted Account's attribution
**When** an avatar renders
**Then** it is a tombstone — the same shape, muted, with no initials — never blank and never removed
**And** attribution therefore survives without the identity persisting

> - Q5 (a): add a tombstone state to the avatar (a `Deleted` parameter on `AssigneeAvatar`, or a small shared `AccountAvatar` both sites can use): same box, radius and size as the live avatar; `background: var(--surface-column)`, `color: var(--text-muted)`; **no text node**; accessible name "Deleted Account" (`role="img"`), never empty, never `display:none`.
> - Render it where a deleted Account's attribution appears: the Invitation row's issuer ("Sent by a deleted Account" with the tombstone) and the Invitation view's issuer line. The server returns `IssuerName = null` when the issuer's Account is gone (Q3), so the deleted Account's name is never served again; the client never falls back to blank.
> - Gates: extend `DesignFoundationGateTests` so the tombstone class takes the 3px radius, `--surface-column` and `--text-muted`, and holds contrast (if the gated contrast pairs include the avatar, add this pair; a muted, text-less glyph has no text-contrast obligation — state that in the gate remark).

**AC6 — the offer naming the Account lapses** (FR-8 `prd.md:222`; AD-5 note 2026-11-08; AD-26)
**Given** a pending Ownership Offer naming that Account's Membership
**When** the Account is deleted
**Then** that offer lapses inside the deletion transaction

> - "The deletion transaction" is, per Space, the transaction that deletes that Membership (step 2): `LeaveAsync` lapses before its guarded `DELETE`, under the lock. Record this reading (contradiction C1).
> - Prove: the offer reads `Lapsed`, no Role moved, the Owner's pending read answers `204`, the Owner can make a new offer, no email is sent on the lapse (FR-43), and `AssertNoPendingOfferNamesAMissingMembershipAsync` holds. Closes `:910` and `:921` (no deletion path skips the lapse).

**AC7 — the address is reusable and inherits nothing** (FR-3 `prd.md:164`)
**Given** deletion completes
**When** the email address is used to register again
**Then** a new Account is created inheriting no Membership, no Space and no history

> - Through the product: register → join two Spaces → delete → register again with the same address (different case allowed). The new Account has a new `Id`, exactly one Membership (its new Personal Space, AD-22), no Token, no `AccessRefusal` row, no `AccountIdempotencyRecord`, and the former Spaces answer it the boundary `404`.
> - An Invitation still pending to that **address** is not "history" of the deleted Account (it belongs to its Space and issuer); it remains acceptable by whoever holds the address. Record it (C4).

**AC8 — where it lives, and the AD-24 record** (AD-24; AR-29; `EXPERIENCE.md:55`)
**Given** deletion
**When** it is offered
**Then** it lives on Account settings, one of only two Account-scoped surfaces
**And** the read that enumerates owned Spaces in the refusal is recorded against the AD-24 amendment, being the caller's own Memberships and Roles rather than anyone else's

> - The route is `AccountScopedSurface.AccountSettings`. `AccountScopedSurface` keeps exactly three members (`SpaceSwitcherGateTests.cs:77`).
> - Add a dated **AD-24 amendment** (2026-11-xx, story 5.4) to `ARCHITECTURE-SPINE.md`: the owned-Space read (named 2026-10-29) is built; the deletion's write sequence (Q1) is stated — Account-scoped check, per-Space leave on the existing Space-scoped unit of work, Account-scoped erase — and that it **adds no session-key setter, relaxes no policy, adds no BLOCK predicate and carries no unit of work with both keys**. Add a dated **AD-5 note** (the lapse and the unassignment run per Space through `LeaveAsync`; the issuer key per Q3) and an **AD-6 note** (each Space's Tokens die in that Space's leave).
> - Gates to update: `Every_Account_scoped_endpoint_names_an_AD_24_surface` (`RefusalBoundaryGateTests.cs:371`, "5.4 adds deletion"); `SpaceSwitcherGateTests.Isolation.cs:13-37` (`AccountScopedReads`/`Readers`/`Endpoints`); `SpaceListRenderers` (`SpaceSwitcherGateTests.CarryOver.cs:31-35`, "5.4 adds its file here") if the refusal list renders in a new component; `Space_is_read_only_where_named` (`RefusalBoundaryGateTests.cs:427`) for the type that joins `Space` for names; `RefusalBoundaryGateTests.cs:697` if the erase step refers to `AccessRefusal`; `SpaceNavigationGateTests.cs:43-45` if `AccountSettings.razor` gains a direct navigation (prefer `Session.SignedOutAsync(landing)`).
> - If the Account-scoped steps live in a **new** Infrastructure type, `SpaceSwitcherGateTests.cs:363`'s `AccountId`-setter list must widen to name it, and the AD-24 amendment names it. Prefer putting them behind `AccountSpaceDirectory.InAccountScopeAsync` or a type it already allows, so the list stays as it is (Q1).

**AC9 — the erasure leaves nothing linkable** (§6.4 "Erasure", `prd.md:796`; ledger `:338`)
**Given** deletion completes
**When** the database is inspected
**Then** no row anywhere carries the deleted `AccountId` except where this story records a reason

> - A test enumerates every column named `AccountId`, `*AccountId`, `RecipientMembershipId`/`OfferedByMembershipId` (by the deleted Membership ids) and asserts the expected set: none in `Account`, `Membership`, `ApiToken`, `SpacePresence`, `AccountIdempotencyRecord`, `AccessRefusal` (Q6 (a)), `Invitation.IssuedByAccountId` (Q3 (a)). Retained by design: terminal `OwnershipOffer` rows naming the deleted Membership ids (AD-26 note, no key, unlinkable to a new Account).

**AC10 — Tokens die with the Memberships** (FR-36 `prd.md:615`; AD-6; V-3; ledger `:385`)
**Given** an Account holding Tokens bound to two Spaces
**When** it deletes itself
**Then** both Tokens answer the no-credential `401` on their next request, and no `ApiToken` row remains

**AC11 — a dead Account authenticates nothing** (FR-2; V-11; ledgers `:250`, `:790`)
**Given** an Account deleted while a Session, a Token or a sign-in is in flight
**When** any of them is next used
**Then** the Session answers `401` (and is cleared), the Token answers `401`, and a sign-in whose rehash wrote zero rows issues **no** Session

> - `AccountCredentialStore.RehashAsync` returns the rowcount (or a bool); zero → `AuthenticateAccountHandler` returns `null` (the uniform `authentication-failed`, AD-23 duration rules unchanged). A handler unit test with a fake store covers it; no timing test.
> - `:790`: a deleted Account's Session is refused by `SessionRevocation` before the Invitation accept runs, so the accept answers `401`, never `403 invitation-for-another-account`. Prove with one Slices case (delete, then replay the accept with the old cookie).

**AC12 — revocation and isolation rows** (V-3, V-11, I-11; GATE-003; `TESTING-CONVENTIONS.md:790-911`)

> - Revocation: add `REV-ACCOUNT-DELETED-BY-HOLDER` (product-driven twin of the raw-SQL `REV-ACCOUNT-DELETED`, which stays as the schema proof): event = the holder deletes their Account through `DELETE /api/v1/account`; Session next answer `NoCredential(ClearsTheDeadCookie: true)`, Token next answer `NoCredential()`. Update ledger `:393-394`.
> - Isolation (`IsolationCaseListTests.cs:72` fails on an unmapped route): rows for `DELETE /api/v1/account` on both surfaces — Token surface: the no-credential `401` (Account-scoped routes are Session-only); Session surface: the refusal lists only the caller's owned Spaces (identity only, two members per item; never a Space the caller is merely a Member of, never another Account's) — I-11. Name them `ISO-DELETE-ACCOUNT-*`. Any row left `PendingUntil: "5.4"` must be live before review (GATE-003).

**AC13 — S-2 everywhere** (S-2; FR-42)
Every Slices case that deletes an Account, or races a deletion against an acceptance, asserts `SpacesNotHoldingExactlyOneOwnerAsync` is empty afterwards.

**AC14 — the client flow** (UX-DR36; `EXPERIENCE.md:55`, `:84-87`)

> - Account settings gains a "Delete your Account" section below the API Tokens. Opening it shows an in-place `destructive-confirm` (Q4 (a)): heading "Delete your Account?", "This cannot be undone.", what happens (you leave every Space you belong to; Tasks assigned to you become unassigned; your API Tokens stop working; Spaces you belonged to keep everything their members made), `button-danger` "Delete Account" and Cancel; Esc cancels; neither button pre-focused beyond the heading receiving focus.
> - `204` → `Session.SignedOutAsync(landing)` to the sign-in page with a polite statement "Account deleted." (the purge runs first, as for sign-out). `409 account-owns-spaces` → AC3's state. `401` → the Session-ended path. 5xx / no answer → "Not sent." and the confirm stays.
> - `autocomplete="off"` is irrelevant (no typed field under Q4 (a)). No literal strings in Razor (gate). Reflow at 320×256 with text spacing (`TextSpacingTests`).

## Tasks / Subtasks

- [x] **Task 0: decisions** (before any code)
  - [x] Q1 and Q3 are marked **[LEE]**. If the orchestrator routes them to Lee, record his answers here under "Decided by Lee" before ATDD. Otherwise take recommendation (a) and record it as "Q1 - (a)", with its cost, in Completion Notes.
  - Decided by Lee (2026-11-21): **Q1 - (a)** three-step sequence, recorded as an AD-24 amendment. **Q3 - (a)** nullable `IssuedByAccountId` with `ON DELETE SET NULL`. If the cascade cannot get past RLS, fall back to (b) as the question describes.
  - [x] Decide Q2 and Q4-Q10 the same way. Each recommendation is the default.
  - [x] **Prove Q3's riskiest assumption first** (below): a cascading `SET NULL` from `Account` reaches `Invitation` rows in Spaces whose key is not set. If it does not, take Q3 (b).
- [x] **Task 1: Contracts** (AC1, AC2)
  - [x] `AccountRoutes` gains the deletion route (`/api/v1/account`).
  - [x] `ProblemTypes.AccountOwnsSpaces`; `ProblemResponse` gains `Spaces` (appended).
  - [x] `InvitationResponse.IssuerName` / `InvitationOfferResponse.IssuerName` become nullable (Q3 (a)), with the remark "null once the issuer's Account is deleted". Check any contract-shape gate that pins them.
- [x] **Task 2: Domain/Application** (AC1, AC2, AC6, AC11)
  - [x] `Yello.Application/Accounts/DeleteAccount/DeleteAccountHandler.cs` and its port (`IAccountDeletion` or similar) with an outcome: `Deleted`, `OwnsSpaces(IReadOnlyList<SpaceIdentity>)`, `Raced` (Q2).
  - [x] Reuse `IMembershipStore.LeaveAsync` per Space; no new unassign or lapse.
  - [x] `IAccountCredentialStore.RehashAsync` returns whether a row was written; `AuthenticateAccountHandler` returns `null` on zero (AC11).
- [x] **Task 3: Infrastructure** (AC1-AC4, AC6, AC9)
  - [x] The Account-scoped check and erase steps (Account lock `Yello/SpacesPerAccount/{accountId}`; owned-Space read joined to `Space`; Membership list; `AccessRefusal` delete; `Account` delete).
  - [x] The per-Space leave driven through `IActiveSpaceResolver.BeginAsync` + `LeaveAsync` + `CompleteAsync`, one Space at a time, in a deterministic order (by `SpaceId`).
  - [x] Migration per Q3 (a): `Invitation.IssuedByAccountId` nullable, `ON DELETE SET NULL` (expand-only; literal names, as the existing migrations do). Update `InvitationConfiguration` and its remark. Read paths map a null issuer id to `IssuerName = null`.
  - [x] `AccountCredentialStore.RehashAsync`: return the rowcount.
  - [x] Delete or update `WireDatabase.DeleteAccountAsync` only if a case needs it; prefer the product route in new cases.
- [x] **Task 4: Host** (AC1, AC2, AC8)
  - [x] `AccountEndpoints` (or extend an existing Account endpoints class): `MapDeleteAccount` → `.AccountScoped(AccountScopedSurface.AccountSettings)`; `204` + cookie cleared; `409 account-owns-spaces` with the `spaces` extension; a stable `409` for `Raced` (Q2).
  - [x] Register the handler and port in `Program.cs` / the Infrastructure registration beside the other Account-scoped slices.
  - [x] Log one line on deletion with the Account id only (no address, no name), after commit — follow `SessionLog`/`RegistrationLog`.
- [x] **Task 5: Client** (AC3, AC5, AC14)
  - [x] `AccountSettings.razor` (or a new `AccountDeleteSection.razor`): confirm, refusal state, landing.
  - [x] Tombstone avatar state + CSS (`components.css`, tokens only, logical properties).
  - [x] `InvitationRow.razor` and the Invitation view render the tombstone and "a deleted Account" when `IssuerName` is null.
  - [x] `ClientCopy.resx` strings with `<comment>`s; `RefusalCopy` key for `account-owns-spaces`.
  - [x] `FakeYelloApi.AccountDeletion.cs` (the fake's partial pattern, as `FakeYelloApi.SpaceDeletion.cs`).
- [x] **Task 6: tests** (all ACs). See Testing requirements.
- [x] **Task 7: gates** (AC5, AC8). See AC8's list.
- [x] **Task 8: plants.** (7 of 12 rows run, all 7 caught; 5b, 6, 8, 10, 11 not run - memory kill, see Debug Log) Save each original in memory and restore it in a `finally`. **Never** use `git checkout --`, `git restore` or `git reset`. Run them in small groups. Stop if a planted build fails, so stale binaries never pass for green. Record each in the Debug Log table. Grep for the plant marker after any killed run.
- [x] **Task 9: ledger, docs, verification**
- [x] **Review Follow-ups (AI)** - see Review Findings below.

### Review Findings

- [x] [Review][Patch] The erase's foreign-key (`547`) path returned empty holdings, so the extra pass left nothing and a Membership that arrived in that window ended `Interrupted` instead of being left [Yello.Infrastructure/Persistence/AccountSpaceDirectory.cs:176] - the catch now reads the holdings again in the same transaction.
- [x] [Review][Patch] Plant 5b was not caught: no case saw the sequence continue after a leave answered `OwnershipHeld` [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccountRace.cs:34] - the race now holds a later Space that must stay held.
- [x] [Review][Patch] AC13: two deleting cases did not assert one Owner per Space [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount.cs:427, :484]
- [x] [Review][Patch] AC6: no proof that the lapse sends no email or that the Owner can offer again [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.OfferOwnershipOutbox.cs:228] - a `delete` row on the silent-lapse theory, with a new offer to a bystander.
- [x] [Review][Patch] AC4: the unassigned Tasks' Status and position were not compared [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount.cs:121]
- [x] [Review][Patch] Two refusal cases did not assert that no `@` is in the body [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount.cs:320, :370]
- [x] [Review][Patch] AC14: the refusal state's reflow ran without the text-spacing override [tests/Yello.Tests.Client/AccountSettingsTests.cs:529]
- [x] [Review][Patch] New comments carried story, Q and requirement ids or narrative history [InvitationConfiguration.cs:16, InvitationRow.razor:12, RefusalBoundaryGateTests.DeleteAccount.cs:24, AccountSettingsTests.cs:408, FakeYelloApi.AccountDeletion.cs:13, FakeYelloApi.SpaceDeletion.cs:114, IsolationCases.AccountDeletion.cs:18/:84/:114, ActiveSpaceEndpointTests.IssueInvitationSchema.cs:48]
- [x] [Review][Patch] Two XML summaries restated the member's name [Yello.Application/Accounts/DeleteAccount/DeleteAccountHandler.cs:27, Yello.Host/Endpoints/AccountEndpoints.cs:37]
- [x] [Review][Defer] The erase's `DELETE` can deadlock (1205) with a concurrent Invitation accept and answer `500` [Yello.Infrastructure/Persistence/AccountSpaceDirectory.cs:169] — deferred, low severity
- [x] [Review][Defer] An in-flight request can record an `AccessRefusal` for the Account after the erase commits [Yello.Infrastructure/Persistence/AccessRefusalPurge.cs:50] — deferred, low severity
- [x] [Review][Defer] The migration's `Down` fails once any issuer was nulled [Yello.Infrastructure/Persistence/Migrations/20261121132222_InvitationIssuerSetNull.cs:54] — deferred, low severity
- [x] [Review][Defer] The endpoint's `default` arm answers any unknown outcome as `interrupted` [Yello.Host/Endpoints/AccountEndpoints.cs:76] — deferred, low severity
- [x] [Review][Defer] The deletion logs under `SessionEndpointMarker`'s category [Yello.Host/Endpoints/AccountEndpoints.cs:52] — deferred, low severity
- [x] [Review][Defer] Two concurrent deletions both answer `204` and both log [Yello.Host/Endpoints/AccountEndpoints.cs:62] — deferred, low severity
- [x] [Review][Defer] "Not sent." reuses `TaskEditNotSent` [Yello.Client/Components/AccountDeleteSection.razor:209] — deferred, low severity
- [x] [Review][Defer] The heading and the opener share one accessible name [Yello.Client/Components/AccountDeleteSection.razor:80] — deferred, low severity
- [x] [Review][Defer] In-flight buttons look active but ignore presses [Yello.Client/Components/AccountDeleteSection.razor:67] — deferred, low severity
- [x] [Review][Defer] "Account deleted." is in a status region present at first render [Yello.Client/Pages/SignIn.razor:69] — deferred, low severity
- [x] [Review][Defer] The tombstone's name and "Sent by a deleted Account" are read out together [Yello.Client/Components/InvitationRow.razor:33] — deferred, low severity
- [x] [Review][Defer] `ProblemResponse.Spaces` has a namespace's name [Yello.Contracts/ProblemResponse.cs:38] — deferred, low severity
- [x] [Review][Defer] The erasure inventory is found by column name [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount.cs:605] — deferred, low severity
- [x] [Review][Defer] The client fake never answers `account-deletion-interrupted` [tests/Yello.Tests.Client/FakeYelloApi.AccountDeletion.cs:56] — deferred, low severity
- [x] [Review][Defer] Nothing gates the tombstone's size against the live avatar's [Yello.Client/wwwroot/css/components.css:2445] — deferred, low severity
- [x] [Review][Defer] The three-Space inventory seeds no issued Invitation and no keyed Space write [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount.cs:121] — deferred, low severity
- [x] [Review][Defer] The re-registration case does not assert that there is no Account idempotency record [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount.cs:427] — deferred, low severity

Dismissed (8): a sign-in that needs no rehash and races the deletion gets a Session that `SessionRevocation` refuses on its next request; the stored `IssuerName` is the recorded Q3 (a) cost (ledger, owner Lee); the client never meets an empty `spaces` list, because `OwnsSpaces` is only returned with at least one Space; the resolver records no refusal for a Space the Account has already left; the reset of pooled connections is AD-2's existing design; aborting mid-sequence is the recorded Q1 (a) cost; "Not sent." on a 5xx is what AC14 specifies; plant 6 is an equivalent mutant.
  - [x] Ledger dispositions (below): one line per CLOSED/UPDATED/RE-OWNED note, written out, no blank lines, no TAB or FORM FEED, final newline kept.
  - [x] Dated notes in `ARCHITECTURE-SPINE.md`: AD-24 amendment, AD-5 note, AD-6 note (AC8).
  - [x] `Account.cs` remarks: keep the hard-delete statement; the "story 5.4's" wording is existing text — leave it unless you touch that paragraph (CLAUDE.md: do not rewrite existing comments as a side task).
  - [x] `sprint-status.yaml`: this story to `review` at the end of dev. `epic-5` stays `in-progress` (the retrospective is optional).
  - [x] Verification follows CLAUDE.md: `docker info --format '{{.ServerVersion}}'`; `dotnet build Yello.slnx` (0 warnings, 0 errors); `dotnet ef migrations has-pending-model-changes` (none after the migration); iterate with `--filter-class`; then **exactly one** full run in the background without the browser suite (`dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`, logged to `artifacts/test-runs/5-4-delete-your-account-develop.log`); the affected browser classes filtered (`AccountSettingsTests`, the Invitation row/view classes, `TextSpacingTests`). Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

Account deletion is "leave every Space, then remove the Account row", refused up front if the Account owns anything. Almost all of the per-Space work already exists in `MembershipStore.LeaveAsync` (lock, unassign, lapse, guarded delete that refuses an Owner, presence, `MembershipChanged`, and the Token cascade). The real problem is that the leave runs under one Space's `SESSION_CONTEXT` key, which is set `@read_only = 1` per connection, and `Task`/`OwnershipOffer`/`SpacePresence`/`Invitation` are Space-key only — so no single transaction can unassign, lapse and delete across several Spaces without relaxing RLS or adding a both-keys unit of work, which AD-24 forbids. Q1 (a) resolves this by sequencing: an Account-scoped check, one Space-scoped leave per Space on the existing resolver, and an Account-scoped erase. The second schema obstacle is `Invitation.IssuedByAccountId ON DELETE RESTRICT` (Q3).

### Dominant finding: one transaction cannot span Spaces (Q1)

- `SpaceSessionContext.SetAsync`/`SetAccountAsync` use `sp_set_session_context ..., @read_only = 1`; a second set on the same connection fails (15664). AD-2 forbids setting it once per connection and reusing it.
- `Task` (unassign), `OwnershipOffer` (lapse) and `SpacePresence` are filtered and blocked by `fn_SpaceIsolationPredicate` on the `SpaceId` key only. Under the `AccountId` key alone, only `Membership` (FILTER admits either key) and `AccountIdempotencyRecord` are reachable.
- `WireDatabase.DeleteAccountAsync` shows the naive single-transaction form and why it fails: it cannot unassign, so it fails `547` on an assigned Membership (ledger `:870`), and it neither lapses offers nor clears presence.
- Hence the epic's "inside the same transaction" phrases are read per step: the refusal is inside the transaction that checks for owned Spaces (step 1); the lapse is inside the transaction that deletes that Membership (step 2). Record as C1.

### Locking and races (RCSI)

- Step 1 and step 3 take `Yello/SpacesPerAccount/{accountId}` (Exclusive), which Space creation also takes: a Space created mid-deletion makes the caller its Owner, and step 3's re-read refuses it (AC2).
- Step 2 takes each Space's `Yello/Memberships/{spaceId}` inside `LeaveAsync`. An acceptance in that Space serialises against it: before → the leave answers `OwnershipHeld` (refuse, naming the Space); after → the offer was lapsed by the leave, so the acceptance answers `409 ownership-offer-not-pending` or the boundary `404`.
- Do **not** hold the Account lock across step 2 (it would hold an Account lock while taking Space locks; the reverse order is taken by nothing today, but keep the invariant simple: Account lock only inside steps 1 and 3).
- An Invitation accepted between step 1 and step 3 adds a Membership: step 3 sees it and runs one extra step-2 pass (Q2).

### Open questions

Each has a recommendation, which is the default. **[LEE]** marks a question for Lee. Otherwise the dev phase decides each as a reversible, recorded decision.

1. **[LEE] How one deletion reaches several Spaces** (AC1, AC2, AC6; AD-2, AD-24).
   - **(a) Recommended.** Three sequenced steps (Account-scoped check → per-Space `LeaveAsync` on the existing Space-scoped unit of work → Account-scoped erase), recorded as an AD-24 amendment. Adds no session-key setter, no policy change, no both-keys unit. Cost: not one transaction — a crash or a refusal (AC2's race) part-way leaves the Account out of some Spaces with its Account row intact. Deletion is irreversible by intent (§6.2) and a retry completes it, so the partial state is "further along the user's own request", never another Account's loss. Record this cost in the amendment.
   - (b) One transaction under a new named unit of work that may set the `SpaceId` key for each Space in turn (non-read-only context). Cost: weakens AD-2's read-only rule, the core of the pooled-connection guarantee.
   - (c) Extend `Task`/`OwnershipOffer`/`SpacePresence` predicates to admit the `AccountId` key for rows naming the caller's Membership. Cost: relaxes RLS on Space content — the bypass AD-24 exists to prevent.
   - (d) A stored procedure executed as a privileged principal exempt from the policy. Cost: a disabled-policy path by another name; forbidden by AD-24.
2. **Races in the sequence** (AC1, AC2). **(a) Recommended:** an acceptance that makes the Account an Owner mid-sequence stops it with `409 account-owns-spaces` naming that Space (Spaces already left stay left); a Membership added between steps gets one extra leave pass, and if Memberships still remain, `409` with a stable type `account-deletion-interrupted` ("Try again"). (b) Retry until empty. Cost: unbounded loop under a hostile inviter.
3. **[LEE] The Invitation issuer key** (AC4, AC5, AC9; ledger `:767`; FR-12; AD-5's "tombstone").
   - **(a) Recommended.** Migration: `IssuedByAccountId` nullable with `ON DELETE SET NULL`; the `IssuerName` snapshot stays in the row but is **not served** once the id is null (`IssuerName = null` on the wire → "a deleted Account" + tombstone). Covers Spaces the issuer was removed from earlier, which no Space-scoped step can reach. **Riskiest assumption: that the engine's cascading `SET NULL` from `Account` reaches `Invitation` rows despite the Space-only RLS policy on `Invitation` with no `SpaceId` key set.** Prove it first with a Slices case (issuer removed from Space S, issuer deletes Account, Owner of S lists Invitations: row present, issuer null). If the cascade is blocked or filtered, take (b).
   - (b) Drop the foreign key (as `OwnershipOffer` does, AD-26 note) and keep the dangling id; the read treats an id with no `Account` row as deleted. Cost: an Account id survives in the row (unlinkable to a new Account, which gets a new id) — AC9 records it as retained by design; the read needs an anti-join to `Account`.
   - (c) A soft-deleted "tombstone Account" row. Not allowed: `Account.cs` and FR-3 forbid a `DeletedAt` and require the address freed.
   - The stored `IssuerName` text under (a) and (b) is personal data left in the row; nulling it would need each Space's key. Record it in the ledger for the §6.4 gate (owner Lee), low.
4. **Confirm friction** (AC14; UX-DR36 specifies only the *refused* rung). **(a) Recommended:** an in-place confirm with consequences and "This cannot be undone.", no typed field and no password re-entry; the Session plus anti-forgery are the authority, as for every other write; OAuth Accounts will have no password (`Account.cs`). (b) Type your display name. (c) Re-enter the password. Cost of (c): breaks for passwordless Accounts and adds a credential-hashing path to AD-23's duration discipline.
5. **Where the tombstone renders** (AC5). **(a) Recommended:** build the state once in the avatar component, render it on the Invitation row and the Invitation view for a deleted issuer, and add a ledger entry (owner **7.2**, the description authorship surface) to use it there. (b) Build it with no consumer. Cost: a gate-only state no case can see, which is the vacuity this suite avoids.
6. **`AccessRefusal` rows of the deleted Account** (AC9; ledger `:338`). **(a) Recommended:** delete them in the erase step (`AccessRefusal` has no policy; filter by `AccountId`). FR-3 is an erasure; §6.4 counts it. Cost: those refusals leave the 90-day record early. (b) Keep them as an audit; the id is unlinkable to a re-registered address. Cost: an erased Account's id persists.
7. **`Idempotency-Key` on the deletion** (AD-18). **(a) Recommended:** none, like `DELETE .../api-tokens/{id}` and sign-out: a replay after success is `401` (the Session is dead) and a replay of a refusal re-reads the truth. Record the reading. (b) Require one in the Account idempotency store. Cost: the record cascades away with the Account, so it cannot replay anyway.
8. **Links in the refusal** (AC3). **(a) Recommended:** each owned Space name links to that Space's settings (switching Space by navigation, AD-2 unchanged). (b) Plain names. Cost: the user must find each Space in the switcher.
9. **The dead-cookie answer** (ledger `:424`). **(a) Recommended:** keep the cookie-clearing `401` (it discloses nothing about any Space) and close the entry as accepted; the new revocation row asserts exactly that set. (b) Drop the `SignOutAsync` and tighten the rows. Cost: a dead cookie lingers in the browser.
10. **Ledger re-owns** (`:687`, `:787`, `:717`). **(a) Recommended:** add a reach gate for the new deletion port (only its handler and registration reach it), and re-own the two pre-existing allow-list entries to **sweep** (they concern the creation and Invitation units, which this story does not change). Re-own `:717` to **story 2.9** (or sweep): Account deletion never deletes a Space — an owner is refused — so it never runs that cascade. (b) Build both allow-lists here. Cost: scope unrelated to FR-3.

### Contradictions found (record; do not resolve beyond what is stated)

- **C1.** The epic's "inside the same transaction" / "inside the deletion transaction" versus AD-2's read-only per-connection key (Q1). Read per step.
- **C2.** AD-5 says "a deleted Account leaves a tombstone so authored content keeps attribution"; `Account.cs` and FR-3 require a hard delete with the address freed. Read "tombstone" as the rendered attribution (UX-DR10), not a retained Account row (Q3 (c) rejected).
- **C3.** Deferred `:385` says Memberships and Account "in one transaction"; under Q1 (a) each Space's Tokens die in that Space's leave transaction, which is still "the same transaction" as the Membership's removal (AD-6).
- **C4.** FR-3 "inherits no history" versus Invitations still pending to the address: they are the Space's, not the deleted Account's (AC7).
- **C5.** PRD §7 says Account settings is "the only surface" spanning Spaces; AD-24 says two (already recorded at `EXPERIENCE.md:59`). No effect.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:250` → **CLOSED by 5.4**: the rehash's zero-row result answers `authentication-failed`.
- `:338` → per Q6. Under (a): **CLOSED**, rows erased with the Account.
- `:385` → **CLOSED by 5.4**: product-driven V-3 cases (AC10, AC12).
- `:393-394` → **UPDATED**: `REV-ACCOUNT-DELETED-BY-HOLDER` added; the raw-SQL row stays as the schema proof.
- `:424` → per Q9. Under (a): **CLOSED (accepted)**.
- `:687`, `:787` → per Q10. Under (a): **RE-OWNED to sweep**, with the reason.
- `:717` → **RE-OWNED** to 2.9/sweep: Account deletion never runs the Space cascade.
- `:767` → **CLOSED by 5.4** (Q3).
- `:790` → **CLOSED by 5.4**: the accept answers `401` for a deleted Account's Session.
- `:849` → **CLOSED by 5.4**: unassignment per Space through `LeaveAsync`.
- `:870` (owner sweep) → **UPDATED** if `WireDatabase.DeleteAccountAsync` is changed or kept; say which.
- `:910`, `:921` → **CLOSED by 5.4**.
- **New** (low): "A deleted issuer's `IssuerName` text stays stored in Invitation rows" (owner **Lee**, the §6.4 gate). "The tombstone avatar has one consumer (Invitations); description authorship should use it" (owner **7.2**). "Account deletion is a sequence, not one transaction; a crash part-way leaves the Account out of some Spaces" (owner **Lee**, recorded cost of Q1 (a)) — only if Q1 (a).

### Architecture compliance (must)

- **AD-21 ring** (`tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs`): slice in `Yello.Application/Accounts/DeleteAccount/`, store/steps in Infrastructure, endpoint in Host, contracts in `Yello.Contracts/Accounts/`.
- **AD-1 / AR-3:** the handler never sets a key, reads a Role, records idempotency or a refusal.
- **AD-2:** no raw SQL outside Infrastructure (tests' helpers in `Yello.Tests.Shared`); keys set only by the existing setters; statements carry their own `SpaceId = @space` where Space-scoped (the leave already does).
- **AD-3 / AD-20:** a former Space answers the deleted Account's leftover credentials with nothing (they are dead before reaching it). The refusal is not recorded.
- **AD-5 / AD-26:** never zero Owners (the guarded `Role <> 'Owner'` delete), lapse not delete, unassign not delete; S-2 after every case.
- **AD-6:** Tokens die in the Membership `DELETE`.
- **AD-9:** one `MembershipChanged` per Space left (already recorded by `RemovedAsync`, published after each commit); none on a refusal.
- **AD-10:** no timer, no background job finishes a deletion later.
- **AD-23 / §6.1:** no address in any response, log or refusal; the deletion log line carries the Account id only.
- **AD-24 / AR-29:** identity only in the refusal; only the caller's own Owner Memberships; the amendment written.
- **AR-34:** problem+json, stable codes, `DateTimeOffset` UTC.
- **Code comments** (CLAUDE.md): explain why; no story numbers, AC/Q/ledger ids, dates or history; no XML doc restating a name.

### Library / framework

No new package, no new project. One migration under Q3 (a) (expand-only; `dotnet ef` 10.0.11 from `.config/dotnet-tools.json`; `dotnet tool restore` first).

- EF Core 10.0.11: `ExecuteDeleteAsync`/`ExecuteUpdateAsync`, `AsNoTracking` projections; `DeleteBehavior.SetNull` on a nullable FK.
- ASP.NET Core 10.0.11 minimal APIs; `SignOutAsync` on the cookie scheme for the `204`.
- Blazor WebAssembly 10.0.11. Microsoft.Data.SqlClient 7.0.2. SQL Server 2025 (RCSI; `sp_getapplock` grants queued waiters in order).
- xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3 (and Mono.Cecil, already used by IL gates).

No web research was needed: every API is already in the codebase. The one engine behaviour to verify (RLS versus a cascading `SET NULL`) is verified by a test, not by documentation (Q3).

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`: traits `:44-95`, rules `:97-207` (races `:102-180`), browser cases `:275-305`, Memberships and attribution `:545-580`, the shared container `:604-673`, isolation and revocation `:790-911`.

- **Traits:** `Suite`, `Priority` (V-3, V-11, I-11, S-2 cases and the Owner refusal are P0), `Requirement` (FR-3, FR-8, FR-12, FR-21, FR-36, AD-5, AD-6, AD-24, AD-26, AR-29, NFR-1, UX-DR10, UX-DR36), `Tier`. Sentence names with underscores.
- **No new container-backed test class.** Slices cases go in new partials of `ActiveSpaceEndpointTests` (for example `ActiveSpaceEndpointTests.DeleteAccount.cs`, `.DeleteAccountRace.cs`) — Account-scoped writes put their cases there (`TESTING-CONVENTIONS.md`, as creation does). Handler tests with fakes (no database). Rows in `RevocationCases.cs` and a new isolation partial spread into `IsolationCases`. Client cases in `AccountSettingsTests` (and the Invitation row/view classes).
- **Seed through the product**: register, join through Invitations, assign Tasks, make offers, issue Tokens — then delete through `DELETE /api/v1/account`. Raw SQL only for what no endpoint does.
- **Races:** block on `LCK_M_%` (`WaitUntilBlockedOnALockAsync`); racing Accounts fetch an anti-forgery token first. Never `Task.Delay`.
- **Mail:** the lapse sends nothing; wait for the offer's own email first, then delete, then assert nothing new to that address. A case that waits on mail calls `SoleDispatcherWireAsync()` and runs its own `HostProcess.RunAsync`.
- **Every absence has an anchor and is seen red under a plant.**
- **Memory budget (CLAUDE.md):** one `dotnet test` at a time; iterate filtered; one background full run with `--max-parallel-test-modules 1`, without the browser suite in Develop.

**Minimum cases (Slices):** an Account in three Spaces (Admin, Member, Viewer) with assigned Tasks in two, a pending offer naming it in one, Tokens in two, presence in one, and Invitations it issued in one it was earlier removed from → `204`, AC1/AC4/AC6/AC9/AC10/AC13 asserted; Owner of one Space → `409` naming exactly that Space, nothing changed; Owner of two plus Member of one → `409` naming both, the Member Space untouched; Owner with a pending offer → `409`; owned Spaces never include one the caller merely belongs to or another Account's; the acceptance race (AC2); a Membership added between steps (AC1/Q2); re-registration (AC7); the Invitation accept with a dead Session (AC11); Token surface `401`; no `@` in any body.

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | Skip the owned-Space check in step 1 | the Owner refusal case (and S-2: the guarded leave refuses, but other Spaces were left) |
| 2 | The owned-Space read without `Role = Owner` (lists every Membership's Space) | the "Member Space is not named" case; the I-11 isolation row |
| 3 | The refusal extension adds the Role | the identity-only gate / I-11 row |
| 4 | Delete Memberships by `AccountId` under the `AccountId` key instead of the per-Space leave | the assigned-Task case (`547`) and the lapse case |
| 5 | Step 2 ignores `OwnershipHeld` and continues | the acceptance race (expects `409` naming the Space; under the plant the erase step meets the remaining Owner Membership and fails on the `Restrict` key, a `500`) |
| 6 | Remove the erase step's re-read of Memberships | the "Membership added between steps" case (`547` on `Account` or a stray Membership) |
| 7 | Serve `IssuerName` for a null issuer id | the tombstone attribution cases (Slices and browser) |
| 8 | The tombstone renders the initials | the browser tombstone case |
| 9 | `RehashAsync` ignores a zero rowcount again | the handler test |
| 10 | The client shows a toast instead of the refusal state, or adds a third exit sentence | the AC3 browser case |
| 11 | Keep the Account's `AccessRefusal` rows (under Q6 (a)) | the erasure inventory case |

### Previous story intelligence (5.3, 5.2, 5.1, 4.5, 3.3)

- **5.3:** every store miss reads the caller's own Membership before anything else (Space first, state second); keep that if step 2's leave meets a vanished Membership (`LostMembership` is "already gone", not an error). Two scaffolds were corrected because `RoleNarration` is an always-present empty `role="alert"`: count only alerts that say something (`SpokenAlerts`). Ledger dispositions must be written out, not blank lines. Plants ran in small groups; one batch failed to build three times and the script stopped each time.
- **5.2:** S1200 (class coupling) forced a new endpoints class; S1541 forced an extracted method. Expect the same on a new orchestration class — keep steps small. A real-host browser flake passed on rerun.
- **5.1:** the lapse is written under the lock before the `DELETE`; `PopulateSpaceAsync` makes an offer, so populated Spaces have a pending offer naming a Member — a deletion case built on it lapses that offer. A case that stops its Host before its own emails are sent leaks them into the next case's drop.
- **4.5:** leaving deletes the row (never soft), the Owner gets `409 ownership-held`, the leave is decided by identity under `ReadProjectsAndTasks`; Token cases for "former" Spaces must use a Token bound elsewhere (the cascade makes a former Space's Token `401`, not `404`).
- **3.3:** the client's deletion exit (`SpaceExit`) purges before navigating; `SpaceNavigationGateTests` pins direct `NavigateTo` counts per file.

### Git intelligence

- `180d469` / `7bb5122` / `ce960ea` / `a9f4473`: story 5.3 (declined read, revoke `LostMembership`, A-10 gates).
- `dca9ee3` / `1ea4a9c`: story 5.2 (accept/decline, `OwnershipOfferStore.Answers.cs`, `OwnershipOfferAnswerEndpoints.cs`).
- The cycle: story → decisions recorded → ATDD scaffolds (`Skip = "red: ..."`, `atdd-checklist-<key>.md`, `PendingUntil: "5.4"` rows that must be live before review, GATE-003) → implementation → review fixes. The orchestrator commits each phase.

### Project Structure Notes

- **New (expected):** `Yello.Application/Accounts/DeleteAccount/DeleteAccountHandler.cs` (+ port, outcome); an Infrastructure implementation of the steps (inside or beside `AccountSpaceDirectory`, Q1/AC8); `Yello.Host/Endpoints/AccountEndpoints.cs` (or similar); a migration under `Yello.Infrastructure/Persistence/Migrations/` (Q3 (a)); `Yello.Client/Components/AccountDeleteSection.razor` (optional split); `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount*.cs`; `tests/Yello.Tests.Isolation/IsolationCases.AccountDeletion.cs`; `tests/Yello.Tests.Client/FakeYelloApi.AccountDeletion.cs`; a handler test under `tests/Yello.Tests.Slices/Accounts/`.
- **Updated (expected):** Contracts `AccountRoutes.cs`, `ProblemTypes.cs`, `ProblemResponse.cs`, the two Invitation responses; Domain/Application `IAccountCredentialStore`, `AuthenticateAccountHandler`; Infrastructure `AccountCredentialStore.cs`, `InvitationConfiguration.cs`, the Invitation read paths, the model snapshot; Host `Program.cs`; Client `AccountSettings.razor`, `AssigneeAvatar.razor` (or a shared avatar), `InvitationRow.razor`, the Invitation view, `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`; tests `RevocationCases.cs`, `AccountSettingsTests.cs`, the gate files named in AC8, `DesignFoundationGateTests`, `TextSpacingTests`; docs `ARCHITECTURE-SPINE.md`, `deferred-work.md`, `sprint-status.yaml`.
- **No new project, no new package, no new container-backed test class.**

### References

- `epics.md`: `:178-179` (§6.1, §6.2), `:217` (AR-29), `:248` (UX-DR10), `:280` (UX-DR36), `:379` (coverage), `:1918-1922`, `:1950-1957` (5.1's lapse/refusal arms), `:2020-2022`, `:2067-2109` (this story).
- `prd.md`: `:155-167` (FR-3), `:210`, `:222-223` (FR-8), `:239`, `:291` (FR-12), `:309` (FR-14), `:415` (FR-21), `:615` (FR-36), `:770-774` (§6.2), `:782-796` (§6.4).
- `ARCHITECTURE-SPINE.md`: AD-2 rule and 2026-11-02 amendment; AD-5 rule and notes (2026-11-02 to 2026-11-09); AD-6 rule and note; AD-9; AD-10; AD-24 rule and amendments (2026-10-29, 2026-10-31, 2026-11-02); AD-26 rule and notes.
- `DESIGN.md:464`; `EXPERIENCE.md:55-59`, `:84-87`, `:110-120`, `:183-191`, `:210`, `:269-270`, `:540-551`; `review-isolation.md:44-50` (attribution from captured names, never a live lookup).
- `specs/spec-yello/SPEC.md:44-46` (CAP-3).
- `test-design-qa.md`: `:197`, `:205`, `:275` (I-11), `:282` (V-3), `:319` (S-2), `:351` (V-11).
- `implementation-readiness-report-2026-08-22.md:482`, `:552` (the rejected removed-Membership tombstone is a different thing), `:782`.
- `deferred-work.md`: `:250`, `:338`, `:385`, `:393-394`, `:424`, `:687`, `:717`, `:767`, `:787`, `:790`, `:849`, `:870`, `:910`, `:921`.
- Prior stories: `5-3-...md` (Dev Notes, review findings), `5-2-...md`, `5-1-...md` (AC5/AC6 Account arms met vacuously, Q10), `4-5-remove-a-membership-or-leave-a-space.md`, `3-3-delete-a-space-and-belong-to-none.md`.
- Code: `Account.cs`, `AccountConfiguration.cs`, `MembershipStore.cs:178-217`, `MembershipStore.Assignees.cs:114-121`, `OwnershipOfferWrites.cs:158`, `MembershipActiveSpaceResolver.cs:64-109`, `AccountSpaceDirectory.cs:255-299`, `SpaceSessionContext.cs:358`, `:376`, `ApplicationLocks.cs:236`, `:261-284`, `:312`, `InvitationConfiguration.cs:104`, `:128-131`, `ApiTokenConfiguration.cs:60-64`, `SessionRevocation.cs:125-139`, `AccountCredentialStore.cs:257-268`, `AccountScopedResolution.cs:45-87`, `AccountScopedSurface.cs`, `ApiTokenEndpoints.cs:50-61`, `AccountSettings.razor`, `SpaceDeleteSection.razor`, `SessionState.cs:158-207`, `ContextBar.razor:353-379`, `ProblemResponse.cs:38`, `RefusalCopy.cs:253-284`, `WireDatabase.cs:415-431`, `RevocationCases.cs:74-129`, `SpaceSwitcherGateTests.cs:55`, `:77`, `:363`, `RefusalBoundaryGateTests.cs:371`, `:427`, `:697`.

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-5-4-delete-your-account.md` (scenario map, decisions, findings, seed
data against the schema). Scaffolds follow Q1 (a) and Q3 (a) as Lee decided, and the recommendation (a) of Q2 and Q4-Q10;
a skip that depends on an open question names it.

| File | Cases | ACs | Today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount.cs` (new) | 7 | AC1, AC2, AC4-AC7, AC9-AC11, AC13 | red (skipped) |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccountRace.cs` (new) | 2 | AC1, AC2, AC13 (Q2) | red (skipped) |
| `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/AuthenticateAccountHandlerTests.cs` (1 added) | 1 | AC11 | red (skipped) |
| `tests/Yello.Tests.Isolation/IsolationCases.AccountDeletion.cs` (new) | 2 rows | AC12 (I-11, V-3) | `PendingUntil: "5.4"` |
| `tests/Yello.Tests.Revocation/RevocationCases.cs` (`REV-ACCOUNT-DELETED-BY-HOLDER`) | 1 row x 2 surfaces | AC12 (V-3, V-11) | red (row `Skip`) |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.DeleteAccount.cs` (new) | 4 | AC2, AC4, AC5, AC8 | red (skipped) |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.Tombstone.cs` (new) | 3 | AC5 | red (skipped) |
| `tests/Yello.Tests.Client/AccountSettingsTests.cs` (5 added) | 5 | AC3, AC14 | red (skipped) |
| `tests/Yello.Tests.Client/InvitationListTests.cs` (1 added) | 1 | AC5 | red (skipped) |
| `tests/Yello.Tests.Client/InvitationViewTests.cs` (1 added) | 1 | AC5 | red (skipped) |

Supporting, no cases: `tests/Yello.Tests.Shared/AccountDeletionWire.cs` (stand-ins `Route`, `AccountOwnsSpacesType`,
`DeletionInterruptedType` - swap for the contract members and delete), `tests/Yello.Tests.Client/FakeYelloApi.AccountDeletion.cs`
(hooked at the top of `FakeYelloApi.SpaceDeletion.cs`'s `SpaceDeletionAsync`), and `ListedInvitation` / `OfferedInvitation`
`IssuerName` made `string?` in `InvitationWire.cs`.

Green today: none of the new scaffolds. The existing standing gates `Deleting_the_Account_with_its_Memberships_invalidates_its_Tokens`,
`REV-ACCOUNT-DELETED` and the three-member `AccountScopedSurface` gate must stay green.

Activation order (riskiest assumption first):

1. `An_Invitation_issued_in_a_Space_its_issuer_was_removed_from_survives_the_issuers_deletion_with_no_issuer` with the gate
   `The_Invitation_issuer_key_is_nullable_and_set_null_when_the_Account_is_deleted` - Q3's riskiest assumption, that the
   cascading `SET NULL` from `Account` reaches `Invitation` rows past the Space-only policy. If it does not, take Q3 (b).
2. `The_Invitation_contracts_serve_a_nullable_issuer_name`.
3. The route gate and the refusal-type gate (swap the stand-ins; widen `Every_Account_scoped_endpoint_names_an_AD_24_surface`
   and the `SpaceSwitcherGateTests.Isolation` lists in the same change).
4. The three refusal cases.
5. `An_Account_in_three_Spaces_is_erased_from_each_and_every_Space_keeps_its_work`.
6. The two race cases.
7. The re-registration case, the dead-Session accept replay, and the rehash handler test.
8. The two `ISO-DELETE-ACCOUNT-*` rows made live and `REV-ACCOUNT-DELETED-BY-HOLDER` unskipped (GATE-003).
9. The tombstone CSS gates, the Invitation row and view cases, then the five Account settings cases.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the
correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), bmad-dev-story, unattended.

### Debug Log References

- **Riskiest assumption (Q3) - holds.** `An_Invitation_issued_in_a_Space_its_issuer_was_removed_from_survives_the_issuers_deletion_with_no_issuer` passed on its first activation with the migration and the route in place: the engine's `ON DELETE SET NULL` from `Account` reaches `Invitation` rows in a Space whose `SpaceId` key is not set (the issuer had been removed from it), past the Space-only FILTER and BLOCK AFTER UPDATE predicates. Q3 (a) stands; (b) was not needed.
- **Scaffolds corrected (2):**
  1. `ActiveSpaceEndpointTests.DeleteAccountRace.An_acceptance_queued_ahead_of_the_deletions_leave_makes_the_deletion_refuse_naming_that_Space` asserted the former Owner becomes `Member`; an accepted offer leaves the former Owner `Admin` (story 5.2's swap). Corrected to `Admin` and its summary to match. It failed for that reason only; the deletion half (409 naming the Space) was green.
  2. `An_Owner_is_refused_409_naming_exactly_its_owned_Space_and_nothing_changes` seeded the Member Space with a random id, so a deletion that skipped the owned-Space check would leave it only when it happened to sort before the Personal Space. Strengthened (not weakened): the Member Space's id now sorts first in SQL Server's `uniqueidentifier` order (`FirstInSqlServerOrder`), so plant 1 is seen red every time.
- **Stand-ins swapped and deleted:** `AccountDeletionWire.Route`, `.AccountOwnsSpacesType`, `.DeletionInterruptedType` -> `AccountRoutes.Account`, `ProblemTypes.AccountOwnsSpaces`, `ProblemTypes.AccountDeletionInterrupted` (Slices, Isolation, the fake).
- **Existing gates widened in the same change (by design, ATDD finding 3):** `Every_Account_scoped_endpoint_names_an_AD_24_surface` (+ `AccountRoutes.Account` on `AccountSettings`); `SpaceSwitcherGateTests.Isolation` lists (+ `IAccountDeletion`, `DeleteAccountHandler`, `AccountEndpoints::DeleteAsync`; `MethodsDependingOn` now ignores a listed handler's own compiler-generated state machines, which call its private methods); `The_endpoints_unreachable_with_a_Token_are_Token_management_the_switcher_and_the_move` (+ the deletion route); `Only_the_pipeline_depends_on_the_Space_resolver` (+ `AccountDeletion`, with its reason); `SpaceNavigationGateTests.Allowed` (+ `AccountDeleteSection.razor`, 1 call, the post-purge landing); `An_Invitation_cascades_from_its_Space_sets_its_issuer_null_and_references_no_Membership` (renamed from `..._restricts_its_issuer_...`, expects `SET_NULL`). `WireDatabase.InvitationRow.IssuedByAccountId` became `Guid?`.
- **Gates unchanged on purpose:** `No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys` (the Account-scoped steps live in `AccountSpaceDirectory`), `Space_is_read_only_where_named` (the owned-Space join is in `AccountSpaceDirectory`), `Only_the_recorder_and_the_purge_refer_to_the_refusal_record` (the erase calls `AccessRefusalPurge.EraseAccountAsync`).
- **Browser filter note:** `--filter-class` and `--filter-method` AND together; the first browser run combined them and matched nothing (exit 8), rerun with method filters only.

- **Plants** (`artifacts/test-runs/5-4-delete-your-account-plants.log`). The script saved each original and restored it in a `finally`, built before every run, and stopped when a planted build failed (three times, all on analyzer rules - a one-line `if`, an unused `using` - never a stale-binary pass). Single-line plants guarded by an impossible condition (`accountId == Guid.Empty`) kept the analyzers quiet.

| # | Plant | Expected red | Result |
|---|---|---|---|
| 1 | Skip the owned-Space check | `An_Owner_is_refused_409_naming_exactly_its_owned_Space_and_nothing_changes` | Caught |
| 2 | Owned-Space read without `Role = Owner` | the same, `An_Owner_of_two_Spaces_and_Member_of_a_third_...` | Caught (both) |
| 3 | Refusal item carries a `role` | `An_Owner_is_refused_409_...` (identity-only members) | Caught |
| 4 | Leave skipped, Memberships deleted under the `AccountId` key in the erase | `An_Account_in_three_Spaces_...` | Caught |
| 5a | Handler ignores `OwnershipHeld` | `DeleteAccountHandlerTests.A_Space_owned_part_way_...` | Caught |
| 5b | `AccountDeletion` maps `OwnershipHeld` to `AlreadyGone` | race case | Review: **not caught** on the first run (the erase's re-read refused the same way); the race case now holds a later Space that must stay held, and the rerun **caught** it |
| 6 | Erase skips its re-read | `A_Membership_accepted_while_the_deletion_waits_...` | Review: **not caught - equivalent mutant**. The review's `547` fix reads the holdings again on the foreign-key path, so the outcome is the same with or without the explicit re-read |
| 7 | `IssuerName` served for a null issuer | `An_Invitation_issued_in_a_Space_its_issuer_was_removed_from_...` | Caught |
| 8 | Tombstone renders initials | the two browser tombstone cases | Review: **Caught** (both) |
| 9 | Rehash ignores a zero rowcount | `A_rehash_that_wrote_no_row_authenticates_nothing` | Caught |
| 10 | A third exit ("leave") in the refused state | `An_Owner_is_shown_the_Spaces_it_owns_...` | Review: **Caught** |
| 11 | Erase keeps the `AccessRefusal` rows | the three-Space inventory | Review: **Caught** |

  Group F (5b, 6, 11) was killed by Claude Code for host memory pressure while it ran; its three plants were left in the files and were reversed by hand to the exact original lines (a marker grep found none afterwards, and the clean build and full run followed). Groups F and G were not restarted (the kill notice says not to); they are for Review.

  **Review (2026-11-21):** the hand reversal left no residue. `AccountDeletion.cs` maps `OwnershipHeld` to `OwnershipHeld`, the erase re-reads before it deletes and calls `AccessRefusalPurge.EraseAccountAsync`, and no `accountId == Guid.Empty` guard is left anywhere in production or test code. Plants 5b, 6, 11, 8 and 10 were then run one at a time by a script that saved each file's bytes, restored them in a `finally`, built before every run and compared file hashes at the end (0 files changed). The log is `artifacts/test-runs/5-4-delete-your-account-review-plants.log`, and the results are in the table above.

### Completion Notes List

- **Built.** `DELETE /api/v1/account` on Account settings (Session only, anti-forgery, no key): `DeleteAccountHandler` orders three steps behind `IAccountDeletion`; `AccountDeletion` (Infrastructure) delegates the Account-scoped check and erase to `AccountSpaceDirectory` (so the `AccountId` key keeps its two setters) and leaves each Space on `IActiveSpaceResolver` + `MembershipStore.LeaveAsync`, clearing the Space scope after each. `204` signs the cookie scheme out and logs the Account id only; `409 account-owns-spaces` carries `spaces: [{id, name}]`; `409 account-deletion-interrupted` for Memberships that keep arriving. Migration `InvitationIssuerSetNull`; the reads serve `IssuerName = null` for a null issuer. `RehashAsync` returns whether it wrote; zero rows authenticates nothing. Client: `AccountDeleteSection` (in-place confirm, refused state with links and two exits, landing on `/sign-in?reason=account-deleted` saying "Account deleted." politely after the Session purge), `DeletedAccountAvatar` (`.avatar-tombstone`) on the Invitation row and view. Spine: AD-24 amendment, AD-5 and AD-6 notes (2026-11-21).
- **Q1 - (a)** (Lee): three-step sequence; cost recorded in the AD-24 amendment and the ledger (owner Lee): a crash or mid-sequence refusal leaves the Account out of the Spaces already left with its row intact; a resend finishes it.
- **Q3 - (a)** (Lee): nullable `IssuedByAccountId`, `ON DELETE SET NULL`; the riskiest assumption holds (Debug Log). The stored `IssuerName` text stays in the row (ledger, owner Lee, §6.4).
- **Q2 - (a)** (dev decision; cut it by choosing (b)): an acceptance that makes the Account an Owner mid-sequence stops it with `409 account-owns-spaces`, read again so it names what is owned now; a Membership found by the erase gets one extra leave pass, then `409 account-deletion-interrupted`. Reason: bounded, and a hostile inviter cannot keep a request looping.
- **Q4 - (a)** (dev decision): in-place confirm, no typed field, no password. Reason: Session + anti-forgery are the authority for every write, and passwordless (OAuth) Accounts must be able to leave.
- **Q5 - (a)** (dev decision): one tombstone component (`DeletedAccountAvatar`), rendered on the Invitation row and view; ledger entry for story 7.2 to reuse it. Reason: a state with a consumer a case can see.
- **Q6 - (a)** (dev decision): the erase deletes the Account's `AccessRefusal` rows. Reason: FR-3 is an erasure and §6.4 counts it; the refusals leave the 90-day record early.
- **Q7 - (a)** (dev decision): no `Idempotency-Key`. Reason: a replay after success meets a dead Session (`401`), a replay of a refusal re-reads the truth, and an Account-scoped record would cascade away with the Account anyway.
- **Q8 - (a)** (dev decision): each owned Space name links to its settings. Reason: the person can act on each exit at once; navigation, not a key change.
- **Q9 - (a)** (dev decision): keep the cookie-clearing dead-cookie `401`; ledger `:424` closed as accepted. Reason: discloses nothing about any Space, and a dead cookie would otherwise linger.
- **Q10 - (a)** (dev decision): a reach gate for the new port (the `SpaceSwitcherGateTests.Isolation` lists); ledger `:687`, `:787` and `:717` re-owned to the sweep (2.9 is done, so `:717` goes to the sweep). Reason: Account deletion adds no unit of work and never deletes a Space.
- **ATDD finding - `IdempotencyRecord.AccountId`** (dev decision; cut it by erasing them in the per-Space leave): recorded as **retained**, not erased. Reason: the row is Space-scoped with no key to `Account`, no Account-scoped step can reach it, and Spaces the Account was removed from earlier are beyond any per-Space step, so a partial erase would not make AC9 true anyway; it answers retries for 24 hours and goes with that Space's next keyed write after that; its id is unlinkable to a re-registered address. Ledger entry, owner Lee (§6.4 gate).
- **Contradictions read as the story records them:** C1 (per-step transactions), C2 (tombstone = rendered attribution, Account hard-deleted), C3 (Tokens die in each Space's leave transaction), C4 (an Invitation pending to the address belongs to its Space, not to the deleted Account), C5 (no effect).
- **`WireDatabase.DeleteAccountAsync`** kept unchanged as the raw-SQL schema proof behind `REV-ACCOUNT-DELETED`; ledger `:870` updated.
- **The low "epic 5" ledger entries were not touched**, as scoped: left for the epic-5 retrospective or the sweep (a ledger line says so).
- **Ledger:** 14 dispositions written (`:250`, `:338`, `:385`, `:394`, `:424`, `:687`, `:717`, `:767`, `:787`, `:790`, `:849`, `:870`, `:910`, `:921`) and 7 new entries (Lee x3, story 7.2, sweep, epic-5 retrospective, story 5.4's review for the unrun plants).

- **Tests.** New: 7 Slices wire cases + 2 races (all live), `DeleteAccountHandlerTests` (6, fakes), the rehash handler case, 4 + 3 Architecture gates, 5 Account settings + 2 Invitation browser cases, `ISO-DELETE-ACCOUNT-NAMES-ONLY-OWNED-SPACES` and `ISO-DELETE-ACCOUNT-OWN` live (no `PendingUntil` left, GATE-003), `REV-ACCOUNT-DELETED-BY-HOLDER` unskipped. Filtered runs while iterating: Architecture 520/520; Slices deletion and invitation cases 84/84; browser `AccountSettingsTests` + deleted-issuer cases 16/16, `InvitationListTests` + `InvitationViewTests` + `TextSpacingTests` 61/61. `dotnet ef migrations has-pending-model-changes`: none.
- **One full run** (without the browser suite, `-p:YelloSkipBrowserSuite=true`, `--max-parallel-test-modules 1`): 2132 total, 2125 passed, 0 failed, 7 skipped - `artifacts/test-runs/5-4-delete-your-account-develop.log`.

### File List

New:
- `Yello.Application/Accounts/DeleteAccount/IAccountDeletion.cs`
- `Yello.Application/Accounts/DeleteAccount/DeleteAccountHandler.cs`
- `Yello.Infrastructure/Persistence/AccountDeletion.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261121132222_InvitationIssuerSetNull.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261121132222_InvitationIssuerSetNull.Designer.cs`
- `Yello.Host/Endpoints/AccountEndpoints.cs`
- `Yello.Host/AccountLog.cs`
- `Yello.Client/Components/AccountDeleteSection.razor`
- `Yello.Client/Components/DeletedAccountAvatar.razor`
- `tests/Yello.Tests.Slices/Accounts/DeleteAccount/DeleteAccountHandlerTests.cs`

Modified (production):
- `Yello.Contracts/Accounts/AccountRoutes.cs`, `Yello.Contracts/ProblemTypes.cs`, `Yello.Contracts/ProblemResponse.cs`, `Yello.Contracts/Invitations/InvitationResponse.cs`, `Yello.Contracts/Invitations/InvitationOfferResponse.cs`
- `Yello.Domain/Accounts/IAccountCredentialStore.cs`, `Yello.Domain/Invitations/Invitation.cs`
- `Yello.Application/Accounts/AuthenticateAccount/AuthenticateAccountHandler.cs`, `Yello.Application/Invitations/IInvitationStore.cs`
- `Yello.Infrastructure/AccountScopedReads.cs`, `Yello.Infrastructure/Persistence/AccessRefusalPurge.cs`, `Yello.Infrastructure/Persistence/AccountCredentialStore.cs`, `Yello.Infrastructure/Persistence/AccountSpaceDirectory.cs`, `Yello.Infrastructure/Persistence/ApplicationLocks.cs`, `Yello.Infrastructure/Persistence/Configurations/InvitationConfiguration.cs`, `Yello.Infrastructure/Persistence/InvitationStore.cs`, `Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs`
- `Yello.Host/Program.cs`
- `Yello.Client/Components/InvitationRow.razor`, `Yello.Client/Localisation/ClientCopy.resx`, `Yello.Client/Pages/AccountSettings.razor`, `Yello.Client/Pages/ClientRoutes.cs`, `Yello.Client/Pages/InvitationView.razor`, `Yello.Client/Pages/InvitationView.razor.cs`, `Yello.Client/Pages/SignIn.razor`, `Yello.Client/Refusals/RefusalCopy.cs`, `Yello.Client/Spaces/SpaceWrites.cs`, `Yello.Client/wwwroot/css/components.css`

Modified (tests):
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.Tombstone.cs`, `RefusalBoundaryGateTests.DeleteAccount.cs`, `RefusalBoundaryGateTests.Move.cs`, `RefusalBoundaryGateTests.cs`, `SpaceNavigationGateTests.cs`, `SpaceResolutionSeamTests.cs`, `SpaceSwitcherGateTests.Isolation.cs`
- `tests/Yello.Tests.Client/AccountSettingsTests.cs`, `FakeYelloApi.AccountDeletion.cs`, `InvitationListTests.cs`, `InvitationViewTests.cs`
- `tests/Yello.Tests.Isolation/IsolationCases.AccountDeletion.cs`
- `tests/Yello.Tests.Revocation/RevocationCases.cs`
- `tests/Yello.Tests.Shared/AccountDeletionWire.cs`, `tests/Yello.Tests.Shared/WireDatabase.cs`
- `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/AuthenticateAccountHandlerTests.cs`, `PasswordVerificationDurationTests.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteAccount.cs`, `ActiveSpaceEndpointTests.DeleteAccountRace.cs`, `ActiveSpaceEndpointTests.IssueInvitationSchema.cs`

Modified (docs and tracking):
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/5-4-delete-your-account.md`

## Change Log

- 2026-11-21: Story created (bmad-create-story). Status ready-for-dev.
- 2026-11-21: Developed (bmad-dev-story): `DELETE /api/v1/account` as a check / per-Space leave / erase sequence (AD-24 amendment), `Invitation.IssuedByAccountId` nullable `ON DELETE SET NULL`, the rehash rowcount, the Account settings deletion section, the tombstone avatar; every ATDD scaffold live (two corrected), isolation and revocation rows live; ledger dispositions; spine notes. Status review.
- 2026-11-21: Code review (bmad-code-review). 9 patches: the erase's `547` path reads the holdings again; the race case catches plant 5b; AC4, AC6, AC13 and AC14 test gaps closed; comment-rule fixes. 17 low findings deferred to the ledger; 8 dismissed. Plants 5b, 6, 8, 10 and 11 run (6 is an equivalent mutant). One full run including the browser suite: 2707 total, 2697 passed, 0 failed, 10 skipped (`artifacts/test-runs/5-4-delete-your-account-review.log`). Status done.
