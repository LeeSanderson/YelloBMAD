# Story 5.2: Accept an Ownership Offer

Status: ready-for-dev

Epic: 5 — Hand a Space over and leave cleanly (second story)
Story key: `5-2-accept-an-ownership-offer`

**Requirements owned.** From the Story Coverage Index (`epics.md:377`): **FR-42** (the accept half; 5.3 owns decline, the settled states and the Owner being told), **AR-13**, **AR-16**, **UX-DR9**, **UX-DR37**. Also carried here: **UX-DR41** for the Offer indicator (ledger `:664`, re-owned from 5.1), test-design **R9** (`test-design-architecture.md:531-542`), **S-2** (`test-design-qa.md:319`), **V-10** (`:289`) and **A-14** (`:365`). The handoff names 5.2 as the owner of "Ownership invariant + concurrency + ArchUnit rule" (`test-design-qa.md:496`).

- **FR-42** (`prd.md:229-241`). Every clause that binds acceptance:
  - "Only the single Membership the offer names can accept or decline it, and only while it is pending." (`:234`)
  - "On acceptance, ownership moves in one atomic step: the recipient becomes the sole Owner and the previous Owner becomes an Admin without losing access. At no point does the Space have zero Owners or two Owners." (`:235`)
  - "No Account becomes an Owner without having agreed to it. There is no route, the API included, by which ownership arrives unrequested." (`:236`)
  - "A declined or lapsed offer cannot afterwards be accepted" (`:238`). Decline itself, and "the offering Owner is told it was declined" (`:237`), belong to 5.3 (Q3).
  - "After acceptance the new Owner is bound by every rule that binds any Owner" (`:239`): FR-14 (no removal while holding ownership) and FR-3 (Account deletion refused, which is 5.4's).
- **AD-26** (`ARCHITECTURE-SPINE.md:251-257`), the binding decision. It authorises by **row identity, never Role**, and it is "the only capability in Yello decided outside the FR-16 Role matrix". Every transition is guarded by `WHERE State = Pending` plus a rowcount check. The Role change is two `UPDATE`s in one transaction, demote **then** promote. "Performing the change through tracked-entity `SaveChanges` is **forbidden**". A no-longer-pending transition answers **409** with a stable `type` to a caller who holds a Membership. "The named recipient reads a pending offer **inside the Space's own context** under AD-2, at whatever Role they hold". No offers inbox and no third Account-scoped surface.
- **AD-5** (`:106-114`):
  - The 4.4 note (`:111`): the filtered index carries "at most one" Owner, and "at least one" rests on the guarded statements plus S-2. There is no trigger and no BLOCK predicate on the Owner row, because the swap passes through zero Owners mid-transaction.
  - The 4.5 note (`:112`): leaving is decided by identity under `ReadProjectsAndTasks`, and a gate keeps it the one write under that capability. Q1 widens that gate.
- **AD-9** (`:135-141`): "accepting an Ownership Offer moves two Roles (AD-26) and must publish both". Publishing goes through `IMembershipChanges.Record`, after commit only. The 4.7 note (`:141`): a Role change reaches an open tab on its next request through `Yello-Role`. `ActiveSpaceState.ReconcileAsync` is the single Role entry point, and a drop is narrated before the lower Role is applied.
- **AD-27 / AR-31** (`:259-264`; `epics.md:219`): the shared actionable predicate goes in the statement's own `WHERE`, against the database clock.
- **AR-13** (`epics.md:201`), **AR-16** (`:204`), **AR-12** (`:200`).
- **UX** (`EXPERIENCE.md:117-119`, `:204`, `:221`, `:261-262`, `:390-404`, `:419`, `:438`, `:541-553`; `DESIGN.md:175-179`, `:319`, `:456`, `:468-469`; mockup `mockups/ownership-offer.html:115-158`).
  - UX-DR9 (`epics.md:247`): the `offer-indicator`.
  - UX-DR12 (`:250`): button variants and the dialog.
  - UX-DR37 (`:281`): the terseness override.
  - UX-DR39 (`:283`): focus.
  - UX-DR41 (`:285`): survives every breakpoint.
  - UX-DR42 (`:286`): server clock.

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Covered by |
|---|---|---|
| `:664` | The Offer indicator must survive every breakpoint (re-owned from 5.1) | AC1, Task 6 |
| `:792-793` | 5.2's swap must take `Yello/Memberships/{spaceId}` and use the acting-authority seam | AC5 (the lock). The seam does not apply: acceptance is identity-decided and declares no Qualified capability. Record why. |
| `:807` | The `2601 -> 409 owner-role-fixed` mapping is reached by no test; 5.2's swap is the first statement to write `Owner` | AC6 (the trigger case) |
| `:915` | A caller who is no longer the Owner gets `400 invalid-offer-recipient`, not a Role re-read (`OwnershipOfferStore.cs:102`) | AC9 |
| `:40` (partly) | The Offer indicator's 2px radius mapping is ungated (owner epic 2 "at each story that builds one of the named surfaces"), and so is the dialog's 6px | AC1/AC2 gates, Task 7 |
| `:929` (optional) | Six offer, lapse and race cases skip the S-2 check (owner epic 5) | AC7. Fold it in: S-2 is this story's theme, and the edit is one line per case. |

**Depends on** (all done):

- 5.1: `OwnershipOffer`, `OwnershipOfferStore`, `OwnershipOfferWrites`, `OfferPredicates.OwnershipOfferActionable`, `ProblemTypes.OwnershipOfferNotPending`, `OwnershipOfferRoutes`, the Ownership section, `WireDatabase.OwnershipOffersInAsync`/`AddOwnershipOfferAsync`, `AssertNoPendingOfferNamesAMissingMembershipAsync`, `FakeYelloApi.OwnershipOffers.cs`.
- 4.4/4.5: `MembershipStore`, `ApplicationLocks.TakeMembershipsAsync`, the `2601/2627` catch in `ChangeRoleCoreAsync`, `409 owner-role-fixed`, `409 ownership-held`, the leave route's identity rule and its gate, `IMembershipChanges`, and `MembershipChangedPublished` counting.
- 4.7: the `Yello-Role` header, `ActiveSpaceState.ReconcileAsync`, `RoleRose`, `RoleNarrationOwner`/`RoleNarrationAdmin`, `SpaceArrival.Reread`, `MatrixRoute`.
- 2.x: `TaskDetail.razor`, the only modal today. Its focus-trap sentinels and `Esc` handling are the pattern the dialog follows.

**Explicitly not in this story:**

- **Decline** (Q3 **[LEE]**), "the offering Owner is told it was declined" (FR-42 `:237`), the A-10 timer gate's `OwnershipOffer` half (ledger `:744`), and the general proofs of the 404-before-409 ordering and of every settled state (5.3, `epics.md:2024-2065`). This story still answers its own route's `409` and renders "This offer is no longer open." when an accept meets it (AC2 notes).
- **Account deletion** (5.4). The FR-3 half of "the new Owner is bound" is met vacuously here and proved by 5.4.
- **A live push of the indicator's disappearance to other tabs** (the sync channel is Epic 7; Q5 **[LEE]**).
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits.
- **No new NuGet package and no new project without Lee** (CLAUDE.md).

## Story

As the person being offered a Space,
I want to understand exactly what accepting commits me to and then accept it in one clean step,
so that ownership arrives by my own agreement and the Space never holds zero or two Owners.

## Acceptance Criteria

AC1 to AC8 are reproduced **verbatim** from `epics.md:1983-2022` and numbered by position. AC9 to AC12 come from the spines, the ledger and a survey of the code, and each cites its source. The indented notes are this story's implementation guidance; a note that depends on an open question names it. Questions marked **[LEE]** are for Lee (product, scope, or an authority seam a prior decision fixed). The orchestrator may route them to him before development. Otherwise the dev phase takes the recommendation and records it as a reversible decision.

**AC1 — the Offer indicator** (UX-DR9, UX-DR41, FR-42)
**Given** a pending offer naming my Membership in the active Space
**When** the context bar renders
**Then** an Offer indicator is present in the accent colour — the only accent element in chrome, because it is the only chrome element that is a proposition rather than a statement
**And** it disappears the moment the offer leaves `Pending`

> - **The recipient's read** (Q2): `GET /api/v1/spaces/{spaceId:guid}/ownership-offers/mine`.
>   - It answers `200 OwnershipOfferForRecipientResponse(Guid Id, string OffererName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt)` when an **actionable** offer names the caller's own Membership, and `204` otherwise.
>   - Store: `OwnershipOfferStore.ReadMineAsync(Guid actorAccountId, ct)`. It is one projection: `Where(OfferPredicates.OwnershipOfferActionable)` plus `RecipientMembershipId == (the caller's Membership Id in this Space, by AccountId)`, and `ReadAt = DatabaseClock.Now()` from the same statement, the `ReadPendingAsync` shape.
>   - `OffererName` is the attribution captured when the offer was made (`OwnershipOffer.OffererName`), never a live Account name read.
>   - It carries no recipient id, no address, and no other Membership.
>   - It declares `Capability.ReadProjectsAndTasks`, which every Role holds (Q1), and serves both surfaces (AD-4).
>   - The Owner never gets `200`, because it cannot be named (5.1's guarded insert, `recipient.Role <> 'Owner'`).
>   - Note: the existing `GET .../ownership-offers/pending` is `OfferOwnershipTransfer` (Owner only) and stays the Owner's.
> - **When the client reads it** (Q5 **[LEE]**): on every arrival in a Space (`SpaceArrival`, including `Reread` and a Space switch), whenever the Role reconciles (`ActiveSpaceState.Changed`/`RoleRose`), and whenever the dialog opens. The state lives in a small scoped `OwnershipOfferState` beside `ActiveSpaceState`. It is purged on a Space switch and on sign-out (the 3.2 purge rule: nothing carries over between Spaces). No timer and no polling (AD-10).
> - **"Disappears the moment":** in this tab, the indicator goes on the accept's `2xx`, on a `409 ownership-offer-not-pending`, on a `204` from the read, and on a Space switch. Another tab, or a revoke made by the Owner elsewhere, is seen at the next read. AD-10 bans polling and the sync channel is Epic 7, so "the moment" means the next answer this tab receives (Q5; the AD-9 4.7 note sets the same bound for Roles). Ledger a new entry for 7.7.
> - **Markup** (`ContextBar.razor`): `<button type="button" class="offer-indicator type-role-label">` placed **before** the `RoleChip` in `context-bar__space`, as the mockup has it (`ownership-offer.html:97`, "OWNERSHIP OFFERED" before "MEMBER").
>   - Visible text: copy key `OfferIndicator`, value "Ownership offered", uppercased by CSS as the Role chip is.
>   - Accessible name: `OfferIndicatorName`, value "Ownership of {space} offered to you. Open the offer."
>   - Pressing it opens the dialog (AC2). It is a button because it acts (`DESIGN.md:456`: "Indicator only; the decision opens in `{components.dialog}`"). The Role chip stays display-only (`EXPERIENCE.md:202`).
>   - It is rendered only while the state holds an offer for the **active** Space. When absent it is absent, never hidden or disabled (UX-DR27).
> - **CSS** (`components.css`): `.offer-indicator { background: var(--accent); color: var(--accent-on); border-radius: var(--radius-sm); }` with the `role-label` type and the light-theme swap through the existing token pairs. Logical properties, tokens only.
> - **Gates** (Architecture, `DesignFoundationGateTests*`):
>   - (i) Inside `.context-bar` rules, `--accent` appears only under `.offer-indicator`. A second accent element in chrome goes red.
>   - (ii) `.offer-indicator` takes `var(--radius-sm)` (ledger `:40`).
>   - (iii) Extend `The_Role_chip_is_never_hidden_at_any_breakpoint` to `.offer-indicator`: no at-rule may hide it (ledger `:664`).
> - **Browser cases** (`Yello.Tests.Client`, a new `OwnershipOfferTests.cs` on the existing collection; `FakeYelloApi.OwnershipOffers.cs` gains the `mine` read and the accept):
>   - a Member recipient sees the indicator, with its accessible name;
>   - a Member who is not named, an Admin and the Owner see none (absent from the DOM);
>   - after a Space switch to a Space with no offer, the indicator is gone;
>   - the read answering `204` after a re-arrival removes it;
>   - extend `SmallViewportTests.At_320px_the_bar_keeps_the_Role_chip_and_drops_the_switch_label_visually` (or a sibling case) so that at 320px the indicator is visible, keeps its accessible name and causes no horizontal reflow (ledger `:664`);
>   - `TextSpacingTests` gets a row for the indicator.

**AC2 — the dialog explains in full** (UX-DR37, FR-42 `:239`)
**Given** the offer dialog
**When** it opens
**Then** it explains in full what accepting commits me to: my Membership cannot be removed while I hold ownership, and my Account deletion is refused until I transfer the Space onward or delete it
**And** this is one of only two places the product's terseness is deliberately overridden, because nobody should agree to that from a one-line dialog

> - **Component** `Yello.Client/Components/OwnershipOfferDialog.razor`. The `Ownership*` file name puts it under the existing gate `The_Ownership_section_reads_no_browser_clock` (it scans every `Yello.Client` file named `Ownership*`).
>   - `role="dialog" aria-modal="true" aria-labelledby` (its heading) `aria-describedby` (the explanation).
>   - One level deep and never stacked. `Esc` closes. Focus is trapped with the `TaskDetail.razor` sentinel pattern (`task-detail__sentinel`, `FocusFirstAsync`/`FocusLastAsync`).
>   - On close, focus returns to the indicator. If the indicator no longer exists (after accept, or after a no-longer-open answer), it goes to the context bar, the nearest surviving container (`EXPERIENCE.md:403`, UX-DR39).
>   - Style: `{components.dialog}`, which is `surface-card`, a hairline border, `var(--radius-lg)` (6px; gate it, ledger `:40`), `spacing.5` and `dialog-title` type, over a scrim.
> - **On open**, it re-reads `.../ownership-offers/mine` (AC1). A `204` shows the no-longer-open state (below).
> - **Copy** (Q4; mockup `ownership-offer.html:121-129`; every string in `ClientCopy.resx` with a `<comment>`; no exclamation marks; nothing about Projects, Tasks or other Members):
>   - Heading: "{offerer} has offered you ownership of {space}."
>   - "Accepting makes you the Owner. {offerer} becomes an Admin and keeps their access."
>   - "As Owner you would take on commitments that leaving cannot undo:"
>   - A list:
>     - "You could not remove your own Membership while you hold ownership."
>     - "You could not delete your Yello Account until you pass ownership on, or delete this Space."
>     - "Passing it on needs someone else to accept. Nobody can be made an Owner without agreeing."
>   - Expiry: "Expires in {n} days · {local date and time}" in a `<time datetime="O">`. Reuse the Ownership section's day arithmetic, which is `ExpiresAt - ReadAt` from the server and **never** the browser clock (UX-DR42, `EXPERIENCE.md:438`).
>   - The mockup's "What Owners can do" link has no target anywhere in the planning, so it is **omitted** (Q4).
> - **The no-longer-open state** (the designed state, `EXPERIENCE.md:262`, mockup `:151-154`). It is reached when opening finds `204`, or when Accept answers `409 ownership-offer-not-pending`.
>   - The dialog body is replaced in place by "This offer is no longer open." and "Nothing has changed about your access to this Space."
>   - It has one button, "Close" (`button-secondary`).
>   - It is not a toast. It names neither the reason nor who acted. The indicator is removed.
>   - 5.3 proves every cause; this story builds the state, because its own route can return the `409`.
> - **Browser cases**:
>   - the dialog shows both commitments (the two FR-42 sentences) and the offerer's name;
>   - "Expires in 6 days" comes from a fake `ReadAt`/`ExpiresAt` pair while the browser clock is set far away (Playwright `Clock`);
>   - `Esc` closes and focus returns to the indicator;
>   - Tab cycles within the dialog;
>   - no `@` in the dialog;
>   - 320×256 reflow;
>   - `TextSpacingTests`.

**AC3 — neither choice is a default** (UX-DR12, `EXPERIENCE.md:261`)
**Given** the dialog
**When** Accept and Decline render
**Then** both are explicit, neither is a default, and neither is pre-focused, because a mis-hit `Enter` must not transfer a Space

> - "Accept ownership" is `button-primary`. "Decline" is `button-secondary` (`DESIGN.md:468`: "`button-secondary` for the second option in a dialog where neither choice may be a default"). Accent inside the dialog does not breach AC1's "only accent element in **chrome**".
> - On open, focus goes to the dialog heading (`tabindex="-1"`), the 5.1 `InvitationRow` precedent. Neither button carries `autofocus`, and the dialog has no `<form>` with an implicit submit.
> - **Decline** (Q3 **[LEE]**). The recommendation is (a): build the decline route in this story and wire the button. Under (b), the Decline control is not rendered until 5.3. A disabled or no-op Decline is never acceptable (UX-DR27).
> - **Browser cases**:
>   - after open, `document.activeElement` is neither button;
>   - pressing `Enter` straight after open sends **no** accept (the fake counts accepts = 0), and so does `Enter` after a click on the explanation text;
>   - one `Idempotency-Key` per deliberate Accept, kept for a resend after no answer (the 5.1 review's lesson: a retry after no answer reuses the key).

**AC4 — authorised by row identity** (AD-26, AR-13, FR-42 `:234`)
**Given** acceptance
**When** it is authorised
**Then** it is authorised by **row identity** — is the caller the named Membership — and never by Role
**And** this is the only capability in Yello decided outside the FR-16 matrix, because FR-8 permits any Role to be named

> - **Route** (Q2): `POST /api/v1/spaces/{spaceId:guid}/ownership-offers/{offerId:guid}/acceptance`, with no body, answering `204`. `Idempotency-Key` comes from the pipeline. Both surfaces (Q6).
> - **Capability** (Q1 **[LEE]**): it declares `Capability.ReadProjectsAndTasks`, held by every Role, so the pipeline admits any Membership and refuses none by Role. Identity then decides inside the store's guarded statement.
>   - Amend the gate `The_leave_route_is_the_one_write_under_ReadProjectsAndTasks` to an **explicit allow-list** of identity-decided writes: `DELETE .../membership` and `POST .../ownership-offers/{offerId}/acceptance` (plus the decline under Q3 (a)). Rename it, for example `Only_the_identity_decided_writes_sit_under_ReadProjectsAndTasks`.
>   - Add a dated note to AD-5's 4.5 note, or to AD-26, saying why.
>   - **No `Capability` member is added.** `Capability.cs:14-19` and `RefusalBoundaryGateTests.cs:89`/`:184` keep the acceptance row out of the vocabulary.
> - **The guard** is inside the store, under the lock (AC5). The offer's transition statement carries the identity in its own `WHERE`:
>
>   ```
>   ExecuteUpdate on OwnershipOffer
>     .Where(o => o.Id == offerId)
>     .Where(OfferPredicates.OwnershipOfferActionable)
>     .Where(o => o.RecipientMembershipId == <the caller's Membership Id in this Space, by AccountId>)
>     set State = Accepted
>   ```
>
>   The caller's Membership is a subquery on `Membership` by `AccountId` under the Space key (and `SpaceId = @space`). It is never a Role read in the slice, so `No_slice_or_endpoint_reads_the_Role_off_a_Membership_row` and `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` stay unwidened.
> - **Rowcount 0, disambiguated by one read** (Q7):
>   - the caller no longer holds a Membership → `LostMembership` → the pipeline's `MembershipLost` (recorded `CrossSpace`, the boundary `404`);
>   - an offer with this id exists in this Space **naming the caller** but not actionable (accepted, declined, revoked, lapsed, expired) → **`409 ownership-offer-not-pending`** ("This offer is no longer open.", the 5.1 type);
>   - otherwise (no such id, another Space's id, or an offer naming **someone else**, the Owner included) → `BoundaryProblems.NotAvailable()`, the in-Space not-found: byte-identical to the boundary, unrecorded, floored.
>   - A non-recipient therefore learns nothing about whom an offer names, and is **never** given a Role-shaped `403` (`review-isolation.md:28`).
> - **Any Role:** a Theory over Admin, Member and Viewer recipients, each joined **through the product** (`JoinedThroughTheProductAsync`), each answering `204` and ending `Owner`. The Viewer case is the one that proves identity over Role: a Viewer holds no write capability in the matrix.
> - **Not the recipient:** another Member, an Admin and the Owner itself each get the in-Space `404` on the offer's id. The offer stays `Pending`, Roles are unchanged and nothing is recorded (`AccessRefusal` count unchanged).
> - **Owner on its own offer:** `404`, as above. The matrix footnote (`role-capability-matrix.md:28`): "The Owner cannot accept one, because the Owner is who makes it."

**AC5 — two ordered `ExecuteUpdate` statements in one transaction** (AD-26, AR-13)
**Given** acceptance
**When** the transaction runs
**Then** the Role change is two explicit ordered `ExecuteUpdate` statements inside one transaction: demote the current Owner to `Admin`, **then** promote the recipient to `Owner`

> - **Where:** `OwnershipOfferStore.AcceptAsync(Guid actorAccountId, Guid offerId, ct)` in Infrastructure. The offer's own state write must live in `OwnershipOfferStore` or `OwnershipOfferWrites`, because `Only_the_offer_store_writes_an_OwnershipOffer_state` hard-codes those two writers (`RefusalBoundaryGateTests.OwnershipOffers.cs:177`). The Membership updates may live in the same method or in an internal `MembershipStore` helper. Record the choice.
> - **Order inside the unit of work:**
>   1. `ApplicationLocks.TakeMembershipsAsync` (**Exclusive**; ledger `:792`). The Role change, the removal, the leave, the offer and the revoke all serialise on this lock, so no Role change can land between the demote and the promote.
>   2. The guarded offer transition (AC4). Rowcount 1, or disambiguate and return.
>   3. **Demote:** `Memberships.Where(m => m.SpaceId == spaceId && m.Id == offer.OfferedByMembershipId && m.Role == Role.Owner).ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, Role.Admin))`. It must write **exactly 1** row.
>      - The offerer is the current Owner by construction: ownership moves only by acceptance, and acceptance consumes the Space's only pending offer.
>      - If 0 rows are written (reachable only through a seeded row), return `OfferStale`, which the endpoint answers `409 ownership-offer-not-pending`. The unit of work rolls back the offer's `Accepted`. Never a `500`, and never a promotion without a demotion.
>   4. **Promote:** `Memberships.Where(m => m.SpaceId == spaceId && m.Id == recipientMembershipId && m.AccountId == actorAccountId && m.Role != Role.Owner).ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, Role.Owner))`. It must write **exactly 1** row.
>      - Catch `SqlException` `2601`/`2627` here and return `OwnerRoleFixed`, which the endpoint answers **`409 owner-role-fixed`** (ledger `:807`, AC6). The unit of work rolls back everything.
>   5. `membershipChanges.Record(formerOwnerAccountId, spaceId)` and `membershipChanges.Record(actorAccountId, spaceId)`. This happens only after both statements wrote 1 row (AC7).
> - **No `SaveChanges`, no tracked `Membership` and no `Update`/`Attach`** anywhere in the accept path. The demoted Owner's `AccountId` is read by a no-tracking projection (`AsNoTracking`/`Select`).
> - **The Membership `BLOCK AFTER UPDATE` predicate** (`20261102085905_MembershipBlockAfterUpdate.cs`) keys on the Space and is satisfied, because the session's key is this Space. Its remark already names this swap.
> - **Plant** (the R9 verification, `test-design-architecture.md:542`: "the invariant test fails against a promote-before-demote implementation"): swap steps 3 and 4. The accept cases go red, because the promote fails `2601`, the store answers `409 owner-role-fixed`, nothing commits and the recipient is not Owner.

**AC6 — why not `SaveChanges`** (AD-26, A-14, R9)
**Given** that ordering
**When** the implementation is reviewed
**Then** tracked-entity `SaveChanges` is not used
**And** the reason is stated: EF Core picks its own statement order for two tracked rows, SQL Server has no deferred constraint enforcement, and promote-before-demote transiently writes a second `Owner` row that fails the filtered unique index — while demote-first removes the row from that filtered index entirely, leaving zero matching rows, and zero never violates uniqueness

> - **The reason in the code:** a short `<remarks>` on `AcceptAsync`. It explains *why* (CLAUDE.md comment rules: no story or AD ids, no history). Use the AC's own reasoning in a few lines.
> - **Gate A-14** (`RefusalBoundaryGateTests.OwnershipOffers.cs`, Mono.Cecil, the shape of the existing state-writer gate). New `The_ownership_acceptance_changes_Roles_by_two_ordered_ExecuteUpdate_calls_and_never_SaveChanges`. It asserts three things:
>   - (i) The accept method's body, including its compiler-generated async state machine, calls `ExecuteUpdateAsync` on `Membership` **at least twice**.
>   - (ii) It calls no `SaveChanges`/`SaveChangesAsync`, `Update`, `Attach` or `set_Role`, and nothing it delegates to within the accept path does either.
>   - (iii) In IL order, the demote's `Role.Admin` constant load precedes the promote's `Role.Owner` load, or an equivalent structural check that holds.
>   - Anchor: the gate finds the method. Plants: a `SaveChangesAsync` added to the method → red; the two statements swapped → red, by (iii) if feasible, otherwise by the AC5 plant at runtime. Record which.
> - **Keep `set_Role` writers tight:** `Membership.Role` keeps its setter (Epic 5 was why it had one), but the gate must show the accept path never calls it.
> - **The `2601 -> 409 owner-role-fixed` mapping, reached by a test** (ledger `:807`). The case is deterministic, with no plant left in the tree:
>   - It installs, for the case's Space only, an `AFTER UPDATE` trigger on `dbo.[Membership]`. When the case's Owner row changes to `Admin`, the trigger sets a **third** Membership (an Admin) to `Owner`. That succeeds, because the demote left zero Owners.
>   - The accept's promote then fails `2601` on `UX_Membership_SpaceId_Owner`, and the store answers `409 owner-role-fixed`.
>   - Assert all of these: everything rolled back (the original Owner is still `Owner`, the third is still `Admin`, the recipient is unchanged), the offer is still `Pending`, S-2 holds, and no `MembershipChanged` is published.
>   - Drop the trigger in a `finally` (the `IdempotencyRecord` trigger precedent, `TESTING-CONVENTIONS.md:534-536`). `RECURSIVE_TRIGGERS` is off by default; say so in the case.
>   - Anchor: the same accept without the trigger answers `204`.

**AC7 — never zero, never two** (FR-42 `:235`, S-2, AR-12)
**Given** acceptance commits
**When** observable state is inspected at any point
**Then** the Space never holds zero Owners and never two, and an invariant test asserts this across every Space
**And** the previous Owner is now an Admin without having lost access

> - **After every case** in this story (accept, refusals, races, the trigger case): `WireDatabase.SpacesNotHoldingExactlyOneOwnerAsync(<the case's Spaces>)` is empty (`TESTING-CONVENTIONS.md:562-563`).
> - **Across every Space:** a new helper `WireDatabase.AllSpaceIdsAsync()`, with an invariant case `No_Space_in_the_database_holds_zero_or_two_Owners_after_the_acceptance_cases` that runs **after** the class's accept and race cases have run in the same database and asserts over every Space id. Readers see only committed state under RCSI, and that is the observable state the AC names. Make it order-independent: the case itself runs a batch of accepts, including a race, and then checks every Space, so it does not rely on xunit ordering.
> - Also fold in ledger `:929`: add S-2 to the six 5.1 cases that skip it.
> - **The previous Owner keeps access:**
>   - After the accept, the former Owner's next Space read answers `200` with `Yello-Role: admin`.
>   - It can still do an Admin thing (for example create a Label or list the Members), and is refused an Owner-only thing with a recorded `403` (rename the Space, `OfferOwnershipTransfer`, delete the Space).
>   - Its row is `Admin` in `MembershipRowsInAsync`, and every other Membership's Role is unchanged.
> - **The former Owner's tab** (client; 4.7's mechanism, so no new path): on its next request it gets `Yello-Role: admin`, and the drop is narrated assertively with `RoleNarrationAdmin` before the lower Role applies. In Space settings, the Ownership section and Delete disappear. Space settings itself stays, because Admin keeps it. Add one browser case (`RoleDropTests` or `OwnershipOfferTests`) for Owner→Admin in Space settings: the Ownership section is gone after the narration, which is observed through the existing `MutationObserver` log.

**AC8 — the new Owner is bound like any Owner** (FR-42 `:239`, FR-14, FR-3)
**Given** the new Owner
**When** they act afterwards
**Then** they are bound by every rule binding any Owner: their Membership cannot be removed while they hold ownership, and their Account deletion is refused until they transfer onward or delete the Space

> - **Slices cases after an accept:**
>   - the new Owner's leave → `409 ownership-held`;
>   - the former Owner (now Admin) removing the new Owner → refused as 4.5 refuses an Admin on the Owner's row (`RemovalAuthority.Judge`: `BeyondAuthority`, the recorded `403`). Record the observed answer;
>   - the former Owner changing the new Owner's Role → refused (`409 owner-role-fixed` or the recorded `403`, as 4.4 answers it today; record which);
>   - the new Owner offering onward to the former Owner → `201` (the chain works);
>   - the former Owner (now Admin) **leaving** → `204`. This is UJ-8's end, `prd.md:121-122`.
>   - S-2 after each.
> - **Account deletion** does not exist yet (no route, `AccountRoutes` has only `Register`). The FR-3 half is met vacuously and owned by 5.4, whose AC already requires the refusal (`epics.md:2079-2082`). Record it; there is no new ledger entry, because 5.4's existing entry covers it.

**AC9 — a former Owner's stale offer or revoke is a Role refusal, not a bad request** (ledger `:915`; AD-1's actor clause)

> - Today `OwnershipOfferStore.OfferAsync` returns `InvalidRecipient` (`400 invalid-offer-recipient`) when the actor is still in the Space but no longer the Owner (`:102-107`). Once the swap exists, a former Owner whose request passed the pipeline as Owner before the swap committed reaches exactly this.
> - **Change:** in the disambiguation read, if the actor's Membership exists and is not `Owner`, return a new outcome `NoLongerOwner`. The endpoint answers it as the recorded `403` for `offer-ownership-transfer`, via the existing `QualifiedRefusal` path, so the client's `403` handling (`OnRoleDoubted` → `SpaceArrival.Reread`) applies.
> - Give **revoke** the same treatment. Its guarded `ExecuteUpdate` gets an actor-still-Owner clause (an `EXISTS` on the actor's Membership with `Role = Owner`, `READCOMMITTEDLOCK` like `GuardedUpdateAsync`), and a miss with the actor present and not Owner → `NoLongerOwner` → recorded `403`.
> - **Cases:** controlled races with `WaitUntilBlockedOnALockAsync`. With a pending offer to M, hold the lock, then send M's accept and the Owner's offer to a third Member T. Both pass the pipeline as their pre-swap Roles. Wait until both block, then release. Serial outcomes:
>   - accept first: the Owner's offer finds its actor no longer Owner and answers the recorded `403` for `offer-ownership-transfer` (not `400`); nothing is inserted; S-2;
>   - offer first: the index refuses it, `409 ownership-offer-pending`; then the accept answers `204`.
>   - The revoke arm is AC10 race 2.
> - **Plant:** remove the new branch → the stale-offer race answers `400`, red.

**AC10 — races, idempotency and `MembershipChanged`** (R9, V-10, AR-16, AD-9, AD-18; `TESTING-CONVENTIONS.md:102-113`, `:556-561`)

> - **`MembershipChanged` once per affected Account** (AC from `epics.md:2016-2018`, reproduced: "**Given** acceptance **When** `MembershipChanged` publishes **Then** it publishes **once per affected Account** — twice here — because a lease carries `Role` and two Roles moved").
>   - A `HostProcess.RunAsync` case reads the log and counts `MembershipChangedPublished` lines naming the case's ids (the `ChangeMembershipRole.cs:515` shape, `PublishesNaming`).
>   - Expect exactly one naming (former Owner, Space), exactly one naming (recipient, Space), and none naming any other Account.
>   - Refusals (`404`, `409`), the trigger case's rollback and a **replay** each publish nothing.
>   - Plant: drop the former Owner's `Record` → red.
> - **Race 1, two acceptances of one offer** (R9 item 2, `test-design-architecture.md:538`): the same recipient, two different keys. Hold `Yello/Memberships/{spaceId}`, send both, wait until both block on the lock, release. Expect one `204` and one `409 ownership-offer-not-pending`, never `404` and never two `204`s. One `Owner` (the recipient), the former Owner `Admin`, S-2, two publishes in total.
> - **Race 2, accept versus the Owner's revoke:** serial outcomes only. Either accept `204` then revoke `409 ownership-offer-not-pending`, or (with AC9) the recorded `403` if the revoke's actor clause sees the swap; or revoke `204` then accept `409`. Never both written. Record the observed answers.
> - **Race 3, accept versus the removal of the recipient by an Admin:**
>   - accept first: the removal targets an Owner → refused, recipient Owner;
>   - removal first: the offer `Lapsed` (5.1), and the accept answers the pipeline's `404` (`LostMembership`, recorded `CrossSpace`).
>   - Never a removed Membership holding `Owner`, never zero Owners. `AssertNoPendingOfferNamesAMissingMembershipAsync`.
> - **Race 4, accept versus the Owner leaving:** leave first → `409 ownership-held`, then accept `204`. Accept first → the former Owner, now Admin, leaves `204`. S-2 either way.
> - **Plant:** the accept skips the Memberships lock → race 1's "both waited" assertion goes red (or record it as masked by the guarded statement, with the reason).
> - Every racing Account is made present and fetches an anti-forgery token first; never `Task.Delay`.
> - **Idempotency:**
>   - A replay of the accept's key returns the original `204`, changes nothing more and publishes nothing.
>   - The same key on a **different** offer id → `422`. A malformed key → `400`.
>   - `Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay` stays green.
>   - A second accept with a **new** key → `409 ownership-offer-not-pending` (the guard, not the key, refuses it; `epics.md:2064-2065`).
> - **Expiry:** a row seeded with `ExpiresAt` in the past (R12: expiry is reached only by seeding the past) → accept `409 ownership-offer-not-pending`, Roles unchanged. The `mine` read answers `204` for it. `Assumption = "PRD-12-3"` on both.

**AC11 — what the acceptance does not do** (FR-43, AD-26 `:256`, AR-33)

> - **No email** to anyone on acceptance. FR-43: "exactly one email per offer". The accept handler takes **no `IOutbox`**: extend `The_ownership_offer_revoke_and_lapse_take_no_outbox` to the accept handler (and the decline under Q3 (a)), and rename it accordingly. Case: after an accept, wait for the offer's own email first, then accept, then assert that neither the former Owner nor the recipient received another (wait by recipient; 4.7's lesson).
> - **No new `NotificationSend` row.**
> - **No other Membership changes**, and no `Pending` offer remains in the Space. The accepted row is **retained** with `State = Accepted` and is never deleted.
> - The response carries no Role body. The `Yello-Role` header on the accept's own `204` echoes the Role **judged at request start** (`EchoJudgedRole`, `ActiveSpaceResolution.Authority.cs:53`), that is the recipient's old Role. That is correct by the header's contract. The client therefore re-reads after a `204` (AC12). **Do not** change the pipeline's header writer (`Exactly_one_production_type_writes_the_Role_header`). Record this.

**AC12 — the client after Accept, and the gates** (UX-DR38, UX-DR39, AD-9 4.7 note)

> - **After `204`:** close the dialog, remove the indicator, then call `SpaceArrival.Reread` (exposed as `SpaceArrivalContext.Reread`). The re-arrival brings `Role = owner`, and `ActiveSpaceState.ReconcileAsync` raises `RoleRose`, which narrates `RoleNarrationOwner` ("You're now the Owner. You can manage everything in this Space, including ownership.") **politely**, as 4.7 built it. Focus goes to the context bar (UX-DR39; the indicator no longer exists). Space settings now shows the Ownership section and Delete.
> - **Polite or assertive** (C4): UX-DR38 says permission changes are assertive. 4.7 made rises polite and drops assertive. Keep 4.7's choice (a rise removes nothing) and record it.
> - **Answers:**
>   - `409 ownership-offer-not-pending` → the no-longer-open state (AC2);
>   - `409 owner-role-fixed` → the same no-longer-open state, followed by a Reread. The copy key exists, but the dialog shows the designed state rather than a refusal sentence; record it;
>   - `404` → the Space refusal surface (the boundary);
>   - no answer → "Not sent" with a retry under the same key (do **not** reuse `TaskEditNotSent`; the 5.1 review's defer, ledger "reuses `TaskEditNotSent`". Add an `OwnershipOfferNotSent` key).
> - **Client rules:** no storage, no mutable static, every `NavigateTo` through `SpaceNavigation.GoTo`, copy in `ClientCopy.resx` with a `<comment>`, no Razor `else` around user-visible text, no `disabled` on a Role-reason control, no browser clock.
> - **Gates (Architecture):**
>   - `Every_spaceId_endpoint_declares_exactly_one_capability`: the new routes declare `ReadProjectsAndTasks`.
>   - A new `The_ownership_acceptance_routes_are_identity_decided_and_serve_both_surfaces`: exact patterns, methods and capability.
>   - The amended identity-write allow-list (Q1).
>   - `Only_the_offer_store_writes_an_OwnershipOffer_state`: the accept's `Accepted` write is inside `OwnershipOfferStore`. Keep the hard-coded writer list exactly as is, and extend the gate's doc to name the third sanctioned transition.
>   - The A-14 gate (AC6).
>   - Contract shape: `OwnershipOfferForRecipientResponse` is pinned to exactly its four members (no recipient id, no address).
>   - `The_ownership_offer_problem_types_are_declared_and_each_has_client_copy` stays green; no new problem type unless Q7 changes.
>   - The "Authority" name trap: **no** new Application type whose name contains `Authority` (`RefusalBoundaryGateTests.Memberships.cs:246`, `:310`).
>   - `The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare`: `ReadProjectsAndTasks` is already covered, so check that the new routes do not disturb its route table. If the theory enumerates routes per capability, exclude identity-decided writes deliberately and say why.
> - **Isolation rows** (`tests/Yello.Tests.Isolation/IsolationCases.OwnershipOffers.cs`; every new Space route needs both surfaces before the suite goes green, `TESTING-CONVENTIONS.md:785-795`):
>   - `ISO-ACCEPT-OWNERSHIP-OFFER-FOREIGN-SPACE` and `ISO-READ-MY-OWNERSHIP-OFFER-FOREIGN-SPACE`: the boundary `404`, byte-identical, recorded `CrossSpace`. Afterwards the foreign offer is still `Pending` and every Role is unchanged.
>   - `ISO-ACCEPT-FOREIGN-OFFER-ID-IN-OWN-SPACE`: another Space's offer id (one naming the actor's Membership **there**) → the in-Space `404`, byte-identical, unrecorded. The foreign offer stays `Pending`.
>   - `ISO-ACCEPT-OFFER-NAMING-ANOTHER-MEMBERSHIP`: an offer in one's own Space naming someone else → the in-Space `404`, identical to a random id (`SameAsAnchor`), unrecorded, nothing written.
>   - `ISO-TOKEN-OWN-OTHER-SPACE-ACCEPT-OWNERSHIP-OFFER` and `-READ-MY-OWNERSHIP-OFFER` (the 3.4 gate requires one per route a Token reaches).
>   - Under Q3 (a), the decline's twins.
>   - Add no `ISO-RECORD-KIND-…` rows, because acceptance has no Role refusal. Record that this is deliberate.
>   - Rows created `PendingUntil: "5.2"` by ATDD must all be live before review (GATE-003).

## Tasks / Subtasks

- [ ] **Task 0: decisions** (before any code)
  - [ ] Q1, Q3 and Q5 are marked **[LEE]**. If the orchestrator routes them to Lee, record his answers here under "Decided by Lee" before ATDD. Otherwise take each recommendation and record it as "Qn - (x)", with its cost, in Completion Notes.
  - [ ] Decide Q2, Q4 and Q6-Q10 the same way. Each recommendation is the default.
- [ ] **Task 1: Contracts** (AC1, AC4)
  - [ ] Add `OwnershipOfferRoutes.Mine`/`MineOf(spaceId)` and `Acceptance`/`AcceptanceOf(spaceId, offerId)` (plus `Decline`/`DeclineOf` under Q3 (a)).
  - [ ] Add `OwnershipOfferForRecipientResponse(Guid Id, string OffererName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt)`.
  - [ ] Rewrite remarks that predict 5.2 so they say what exists (`ProblemTypes.OwnerRoleFixed` `:450-452`, `RoleChangeAuthority.cs:14-18`, `AssignableRoles.cs:8`, the `OwnershipOfferRoutes` remark that all routes are `OfferOwnershipTransfer`), with no story numbers.
- [ ] **Task 2: Application** (AC4, AC5, AC9, AC11)
  - [ ] Add `IOwnershipOfferStore.ReadMineAsync` and `AcceptAsync`, with outcome enum `OwnershipOfferAcceptance { Accepted, NotPending, NotFound, LostMembership, OfferStale, OwnerRoleFixed }` (map `OfferStale` → `409 ownership-offer-not-pending`).
  - [ ] Add `NoLongerOwner` to `OwnershipOfferOutcome` and to `OwnershipOfferRevocation` (AC9).
  - [ ] Add `Yello.Application/Spaces/AcceptOwnershipOffer/AcceptOwnershipOfferHandler.cs` (the spine's slice name, `ARCHITECTURE-SPINE.md:413`) and `ReadMyOwnershipOffer/ReadMyOwnershipOfferHandler.cs`. No `IOutbox`, no Role read, no recording. **No type named `...Authority...`.**
- [ ] **Task 3: Infrastructure** (AC4-AC7, AC9, AC10)
  - [ ] `OwnershipOfferStore.AcceptAsync`: lock → guarded transition → demote (exactly 1) → promote (exactly 1, `2601/2627` → `OwnerRoleFixed`) → two `Record`s. `ExecuteUpdateAsync` only. Add the `<remarks>` giving the reason.
  - [ ] `OwnershipOfferStore.ReadMineAsync` (a projection; `ReadAt` from the database).
  - [ ] `OfferAsync`/`RevokeAsync`: the `NoLongerOwner` branch, and the revoke's actor-still-Owner clause.
  - [ ] Inject `IMembershipChanges` into `OwnershipOfferStore` (DI in `SpaceContentStores.cs`). Check that `SpaceBoundaryPipelineTests`/`ActiveSpaceUnitOfWorkTests` need no new registration (4.4's lesson).
  - [ ] Widen the `OfferPredicates.cs` remark (`:13-15`) that lists the predicate's users.
  - [ ] **No migration.** `Accepted` already exists in the enum and the column holds strings. Confirm with `dotnet ef migrations has-pending-model-changes` (or an empty `add`, which is then removed).
- [ ] **Task 4: Host** (AC4, AC9, AC12)
  - [ ] `OwnershipOfferEndpoints`: `MapReadMyOwnershipOffer` and `MapAcceptOwnershipOffer`, each `.RequiresCapability(Capability.ReadProjectsAndTasks)`, with explicit outcome switches ending `_ => throw`. Map `LostMembership` → `new MembershipLost()`, `NotFound` → `BoundaryProblems.NotAvailable()`, `NotPending`/`OfferStale` → `409 ownership-offer-not-pending`, `OwnerRoleFixed` → `409 owner-role-fixed`, `NoLongerOwner` → `QualifiedRefusal` (the recorded `403`).
  - [ ] Register the handlers and map them in `Program.cs` beside the 5.1 routes, extending the route comment block (`:587-597`).
- [ ] **Task 5: the decline route** (only under Q3 (a))
  - [ ] Same identity guard and same disambiguation, writing `Declined`. No Role change, no publish, no email. 5.3 adds telling the Owner.
- [ ] **Task 6: Client** (AC1-AC3, AC7, AC12)
  - [ ] `OwnershipOfferState` (scoped, purged on a Space switch and on sign-out). Update the indicator in `ContextBar.razor`. Add `OwnershipOfferDialog.razor`, `ClientCopy.resx` keys (`OfferIndicator`, `OfferIndicatorName`, the dialog copy, `OwnershipOfferAccept`, `OwnershipOfferDecline`, `OwnershipOfferNoLongerOpenDetail`, `OwnershipOfferClose`, `OwnershipOfferNotSent`), `components.css` (`.offer-indicator`, the dialog layer), and `RefusalCopy` mappings where needed.
  - [ ] `FakeYelloApi.OwnershipOffers.cs`: the `mine` read, the accept (counting accepts and keys), the decline under Q3 (a), and a Role header change after an accept for the reread.
- [ ] **Task 7: tests and plants** (all ACs). See Testing requirements. Save each original in memory and restore it in a `finally`. **Never** use `git checkout --`, `git restore` or `git reset`. Record plants in the Debug Log as a table. After any killed run, grep for plant markers.
- [ ] **Task 8: ledger, docs, verification**
  - [ ] Apply the ledger dispositions below: one line per CLOSED/UPDATED/RE-OWNED note, no TAB or FORM FEED, keep the final newline.
  - [ ] Dated notes in `ARCHITECTURE-SPINE.md`:
    - AD-26: the routes, `ReadProjectsAndTasks` as the identity carrier (Q1), the in-Space `404` for a non-recipient (Q7), the demote targeting `OfferedByMembershipId`, and `OfferStale`.
    - AD-5: the identity-write allow-list now holds leave and accept (and decline).
    - AD-9: two `Record`s per accept, and the accept's header echoes the pre-swap Role while the client re-reads.
  - [ ] `sprint-status.yaml`: this story goes to `review` at the end of dev. `epic-5` stays `in-progress`.
  - [ ] Verification follows CLAUDE.md:
    - `docker info --format '{{.ServerVersion}}'`, then `dotnet build Yello.slnx` (0 warnings, 0 errors), then iterate with `--filter-class`.
    - Then **exactly one** full run in the background without the browser suite: `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`, logged to `artifacts/test-runs/5-2-accept-an-ownership-offer-develop.log`.
    - Record the totals. Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

The recipient, at any Role, reads "the pending offer naming me" inside the Space, under the capability every Role holds. The context bar shows an accent "Ownership offered" button only while that read answers `200`. The button opens a modal that spells out what being Owner commits them to. It offers Accept and Decline with neither focused, and counts its expiry from the server's clock. Accept is a `POST` that the pipeline admits for any Membership. Under the Space's Memberships lock, the store moves the offer to `Accepted` in one guarded statement whose `WHERE` holds both the actionable predicate and "names the caller's own Membership". It then demotes the offering Owner to `Admin` and promotes the caller to `Owner`, as two `ExecuteUpdate`s in that order: demoting first leaves zero rows in the filtered Owner index, and zero never collides, while promoting first would fail `2601`. It records `MembershipChanged` for both Accounts, and they publish after commit. Nothing is emailed. Any miss is a `409` for an offer that named the caller and a not-found `404` for anything else. The recipient's tab re-reads its Space and narrates "You're now the Owner". The former Owner's tab learns it is an Admin on its next request, through the `Yello-Role` header 4.7 built.

### What exists today (survey at `89b98a3`)

- **Domain:** `OwnershipOffer` (init-only; `RecipientMembershipId`, `OfferedByMembershipId`, `OffererName`, `State`, `ExpiresAt`, `CreatedAt`; no FK to Membership or Account). `OwnershipOfferState { Pending, Accepted, Declined, Revoked, Lapsed }`. `Membership.Role { get; set; }`, stored as a string, max 20. `Capability.cs:14-19` (the acceptance row deliberately absent). `RoleCapabilityMatrix`, `RoleChangeAuthority`, `RemovalAuthority.Judge` (an Admin on the Owner's row → `BeyondAuthority`, the recorded `403`; the Owner on its own row → `OwnershipHeld`).
- **Application:** `IOwnershipOfferStore` (`OfferAsync`, `ReadPendingAsync`, `RevokeAsync`; outcomes `OwnershipOfferOutcome`, `OwnershipOfferRevocation`); `OfferOwnershipHandler`, `RevokeOwnershipOfferHandler` (no outbox), `ReadOwnershipOfferHandler`; `IMembershipChanges.Record(accountId, spaceId)` (deduplicated per pair, published after commit, `MembershipChangedPublished` log); `ActingAuthority` (only the pipeline sets it); `ActiveSpaceContext(AccountId, SpaceId, Role)`.
- **Infrastructure:**
  - `OwnershipOfferStore` (`:45` ctor `(YelloDbContext, IIdentifierGenerator, TimeProvider)`):
    - the guarded raw `INSERT ... SELECT` (`:76-89`);
    - the `2601/2627` catch (`:91`);
    - the actor-still-here disambiguation (`:102-107`, AC9's target);
    - `RevokeAsync` (`:120`, lock plus `ExecuteUpdate` plus a read).
  - `OwnershipOfferWrites.LapseNamingAsync`. `OfferPredicates.OwnershipOfferActionable` (`:36`) and `OwnershipOfferPendingButExpired` (`:47`).
  - `MembershipStore`:
    - `ChangeRoleCoreAsync` (`:336`; lock, then the raw `GuardedUpdateAsync` `:397-425` with the `READCOMMITTEDLOCK` actor `EXISTS`, then the `2601/2627` → `OwnerRoleFixed` catch `:353-358`, then `Record` `:365`);
    - `LeaveAsync` (`:178`) and `RemoveCoreAsync` (`:225`), which `Record` in `RemovedAsync` `:323`.
  - `ApplicationLocks.TakeMembershipsAsync` (`:122`; its remark already says "an ownership swap must take it as well"). `MembershipChangePublisher`, `CommitHooks`. `SchemaNames.MembershipOwnerUniqueIndex` (`UX_Membership_SpaceId_Owner`). Latest migration `20261108164044_OwnershipOffers`.
- **Host:**
  - `OwnershipOfferEndpoints` (three routes under `OfferOwnershipTransfer`; the body is read as a `JsonDocument`).
  - `MembershipRemovalEndpoints.MapLeaveSpace` (`:52-54`, `ReadProjectsAndTasks`, identity-decided).
  - `RequiredCapability` (exactly one per endpoint).
  - `ActiveSpaceResolution*` (`EchoJudgedRole` `:53`, `QualifiedRefusal` `:124`, `MembershipLost` `:142`). `NotAvailableFloor` (50 ms).
  - `Program.cs` `:162-164` (handlers), `:587-597` (the ownership routes and their comment block).
- **Client:**
  - `ContextBar.razor` (`nav.context-bar` `:58`, `context-bar__space` `:59`, `RoleChip` `:81`, the settings link `:84-87`, a polite status `:106`, `OnRoleRose` `:209`).
  - `RoleChip.razor`; `RoleNarration.razor`, `RoleNarrator`, `RoleCopy.NarrationKey`.
  - `ActiveSpaceState` (`Changed`, `RoleDropping`, `RoleRose`, `ReconcileAsync` `:259`); `YelloApiHandler` (`:86-91` reconciles on every answer); `SpaceArrival.RereadAsync` (`:316`).
  - `TaskDetail.razor` (the modal pattern, `:59-60`, `:307`, `:656-662`); `OwnershipSection.razor` (the day arithmetic for the expiry, the Idempotency-Key handling).
  - Tokens `--accent`/`--accent-on` (`tokens.css:51-52`, light `:72-73`), `--radius-sm` (2px, whose remark already names the Offer indicator), `--radius-lg` (6px).
  - `ClientCopy.resx` `RoleNarrationOwner` (`:718`), `RoleNarrationAdmin`, `ownership-offer-not-pending` (`:1538`), `owner-role-fixed`.
- **Tests:**
  - `ActiveSpaceEndpointTests.OfferOwnership*.cs`, `.OwnershipOffer*.cs`, `.RevokeOwnershipOffer.cs`, `.ChangeMembershipRole.cs` (`:515` publish counting, `PublishesNaming` `:925`), `.LeaveSpace.cs`, `.RemoveMembership.cs`, `.RoleMatrix.cs`.
  - `WireDatabase` (`SpacesNotHoldingExactlyOneOwnerAsync` `:741`, `OwnershipOffersInAsync` `:1098`, `AddOwnershipOfferAsync` `:1149`, `MembershipRowsInAsync`). `MembershipWire.PublishedEvent`. `OwnershipOfferWire`.
  - `IsolationCases.OwnershipOffers.cs`.
  - `RefusalBoundaryGateTests.OwnershipOffers.cs` (`Only_the_offer_store_writes_an_OwnershipOffer_state` `:175`, writer list `:177`, SQL scan `:235`; `The_ownership_offer_revoke_and_lapse_take_no_outbox` `:245`; `The_Ownership_section_reads_no_browser_clock` `:371`), `.RemoveMembership.cs` (`:89` the leave-only write gate), `.Memberships.cs`, `.RoleDrop.cs`.
  - `DesignFoundationGateTests.SmallViewport.cs:91`, `SmallViewportTests.cs:819`.

### What must be preserved (regression guard)

- **4.4-4.7:**
  - Role change, removal and leave keep their answers, their races and S-2.
  - `409 ownership-held` and `409 owner-role-fixed` keep their copy.
  - `MembershipChanged` stays once per removal or change.
  - The `Yello-Role` header contract stays: the Role judged at start, on every 2xx/403, never on a 404, live on replay.
- **5.1:**
  - The offer, the revoke and the pending read keep their answers. The AC9 change only turns a stale former Owner's `400` into a recorded `403`.
  - The state-writer gate keeps its two writers.
  - Revoke and lapse take no outbox.
  - The Ownership section is unchanged for the Owner.
- **The boundary `404` stays byte-identical** (`RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal`, `SameAsAnchorAndStranger`), including the accept's in-Space `404`.
- **Address confinement:** no address in any new response, log, idempotency record, the indicator or the dialog. `AddressReaders`/`DisplayNameReaders` do not widen. The offerer's name comes from the row's captured `OffererName`.
- **`IdempotencyStore` refuses to save with other changes pending.** The accept tracks nothing (bulk statements only), so nothing is pending.

### Locking and the races (RCSI)

- The accept takes `Yello/Memberships/{spaceId}` **Exclusive** first, then runs the three statements. Lock order: the application lock, then rows. No `HOLDLOCK`.
- The offer, the revoke, the Role change, the removal and the leave already take the same lock (Exclusive), so each race serialises. The assignment takes it Shared, which also serialises against an Exclusive holder, and its answers are unaffected by a Role swap.
- The `mine` read takes no lock (a projection under the Space key).
- Within the accept's transaction, the Space has zero Owners between the demote and the promote. Under RCSI no other session observes that, and no other writer can act on the Space's Memberships while the lock is held. The filtered index is the last line of defence for "at most one". The guarded statements, the exact-1 rowcounts and S-2 hold "at least one".

### Open questions

Each has a recommendation, which is the default. **[LEE]** marks a question for Lee: product, scope, or an authority seam a prior decision fixed. The orchestrator may route those to him; otherwise the dev phase decides them like the rest, as reversible, recorded decisions.

1. **[LEE] Which capability the identity-decided routes declare** (AC1, AC4). Every Space route must declare exactly one capability (`Every_spaceId_endpoint_declares_exactly_one_capability`). The existing offer routes are Owner-only, and a Viewer recipient holds no write capability.
   - **(a) Recommended.** Declare `ReadProjectsAndTasks` on the `mine` read and the accept (and the decline), as the leave route does. Turn `The_leave_route_is_the_one_write_under_ReadProjectsAndTasks` into an explicit allow-list of identity-decided writes, and add a dated note to AD-5/AD-26. Cost: Lee's 4.5 decision made leave "the one write" under the read capability; this makes it two (three with decline). Each is still guarded by identity in its own statement, and the allow-list keeps any other write out.
   - (b) A route metadata marker `DecidedByIdentity` in place of a capability, and the pipeline checks only Membership. Cost: a new pipeline path and gate changes in the security seam, for the same effect.
   - (c) Add `Capability.AcceptOrDeclineOwnershipOffer` as Allowed for every Role. Cost: it contradicts `Capability.cs:14-19` and AR-13 ("decided outside the matrix"), and the matrix theory would then "prove" a Role rule that does not exist.
2. **Routes and shapes** (AC1, AC4).
   - **(a) Recommended:** `GET .../ownership-offers/mine` → `200 OwnershipOfferForRecipientResponse`/`204`; `POST .../ownership-offers/{offerId}/acceptance` → `204` (as 5.1's Q3 foresaw); under Q3 (a), `POST .../{offerId}/decline` → `204`. By offer id, so a stale dialog cannot accept a newer offer it never showed.
   - (b) `POST .../ownership-offers/mine/acceptance` with no id. Cost: a stale tab could accept an offer whose terms it never displayed (a new offer from a different offerer).
3. **[LEE] Decline: here or in 5.3** (AC3). The dialog must render Accept **and** Decline (AC3), but decline is 5.3's first AC.
   - **(a) Recommended.** Build the decline route here: the same guard and disambiguation, writing `Declined`, with no Role change, no publish and no email, and wire the button. 5.3 keeps "the offering Owner is told it was declined", the 404-before-409 proofs across all settled states, and A-10. Cost: 5.3's AC1 is split across two stories; the 5.3 story must say so.
   - (b) Do not render Decline until 5.3. Cost: AC3 is not met in 5.2, and the dialog ships with one choice, which is the "default" AC3 forbids in spirit.
   - (c) Render a Decline that only closes the dialog. Not acceptable: the button would say something it does not do.
4. **Dialog copy** (AC2). **(a) Recommended:** the mockup's copy (`ownership-offer.html:121-129`), with "his" made "their" and "three things" made "commitments". It keeps the third line ("Passing it on needs someone else to accept…"), because it explains why the first two bind. The "What Owners can do" link is omitted, because no target exists. The heading names the offerer (`review-isolation.md:86` accepts that disclosure to a Member). Cost: `EXPERIENCE.md:110`'s example ("You've been offered ownership of …") does not name the offerer; the mockup does. Ledger the copy for UX. (b) EXPERIENCE's headline, without the offerer. Cost: the recipient does not learn who is handing over.
5. **[LEE] "Disappears the moment the offer leaves Pending"** (AC1). There is no push channel before Epic 7, and AD-10 bans polling.
   - **(a) Recommended.** The indicator is updated at every answer this tab gets about the offer: on arrival, on a reread, on a Space switch, on opening the dialog, and on the accept's (or decline's) own answer. A revoke or a lapse made elsewhere is seen at the next arrival or dialog open, where the dialog shows "This offer is no longer open." Ledger "the indicator goes live with the sync channel" to 7.7. Cost: the AC's "the moment" is met for this tab's own actions and at the next read for others, the same bound AD-9's 4.7 note accepts for Roles.
   - (b) Re-read the offer on every request's answer (piggyback on `YelloApiHandler`). Cost: one extra read per request, for a rare state.
6. **Both surfaces** (AC4). **(a) Recommended:** the accept and the `mine` read serve the session **and** API tokens (AD-4: one path for both). An Account's own token accepting is the Account's own act, so it is not ownership "arriving unrequested" (FR-42 `:236`). Isolation rows on both surfaces. (b) Session only (`SessionOnly` metadata). Cost: it diverges from AD-4 without a stated reason.
7. **What a non-recipient gets** (AC4). **(a) Recommended:** the in-Space `404`, byte-identical to the boundary and unrecorded, for an offer id that names someone else (and for the Owner on its own offer). The `409` goes only to the named recipient on a settled offer. This discloses neither whom an offer names nor that one exists. (b) `409 ownership-offer-not-pending` for any Member. Cost: a Member can probe for an offer's existence by id. Both meet `review-isolation.md:28` ("409 … or a 404. Never a Role-shaped 403").
8. **Demote target** (AC5). **(a) Recommended:** demote `Id = offer.OfferedByMembershipId AND Role = Owner`, exactly 1 row, otherwise `OfferStale` → `409 ownership-offer-not-pending` with a rollback. (b) Demote "the Space's Owner" whoever it is. Cost: it would accept an offer whose offerer is no longer Owner, which only a seeded row can produce, but which the offer's terms did not describe.
9. **Narration on the rise** (AC12; C4). **(a) Recommended:** keep 4.7's polite `RoleNarrationOwner` for a rise and assertive for a drop. (b) Assertive for both, per UX-DR38's letter. Cost: a rise interrupts a screen reader for news that removes nothing.
10. **The `2601` proof** (AC6; ledger `:807`). **(a) Recommended:** the test-only `AFTER UPDATE` trigger case, which makes the promote's `2601` deterministic with no plant left in the tree. (b) Prove it by a plant only and record it in the Debug Log. Cost: the mapping is again reached by no permanent test, the very finding the ledger records.

### Contradictions found (record; do not resolve beyond what is stated)

- **C1. The SPEC companion has an acceptance row; the code and AR-13 do not.** `role-capability-matrix.md:24` shows "Accept or decline an Ownership Offer | — | ✔† | ✔† | ✔†". AR-13/AD-26 say the acceptance is outside the matrix, and `Capability.cs:14-19` leaves it out on purpose. The row is an identity rule written as a table row, so follow AD-26 (Q1).
- **C2. Leave as "the one write" under `ReadProjectsAndTasks`** (AD-5 4.5 note, gate `RemoveMembership.cs:89`) versus a second identity-decided write. Q1.
- **C3. "The moment" versus AD-10.** There is no push, so Q5.
- **C4. UX-DR38 ("permission changes are assertive") versus 4.7's polite rise.** Q9.
- **C5. Dialog copy.** `EXPERIENCE.md:110` versus the mockup; two commitments in the ACs versus three in the mockup; the mockup's link has no target. Q4.
- **C6. Decline is 5.3's, but AC3 renders it.** Q3.
- **C7. UX-DR41** is mapped only to 2.11 in the coverage index (`epics.md:364`), yet the ledger re-owned the indicator's breakpoint survival to 5.2. Done here.
- **C8. PRD §7 (`prd.md:807`) lists "ownership" under Space settings (Owner and Admin).** That is right for the Owner's panel only. The recipient's decision lives in the context bar and the dialog (`EXPERIENCE.md:204`, `DESIGN.md:456`, the mockup).
- **C9. The `Yello-Role` header on the accept's own `204` shows the pre-swap Role.** That is the header's contract (the Role judged at start). The client re-reads (AC11/AC12).

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:664` → **CLOSED by 5.2**: the indicator exists, the small-viewport case and the no-hide gate cover it.
- `:792-793` → **CLOSED by 5.2**: the swap takes the lock Exclusive. The acting-authority seam does not apply, because acceptance is identity-decided under `ReadProjectsAndTasks` with the identity in the guarded statement, and no Qualified capability is involved. Say so.
- `:807` → **CLOSED by 5.2**: the trigger case reaches `2601 → 409 owner-role-fixed` on the swap's promote (Q10).
- `:915` → **CLOSED by 5.2**: `NoLongerOwner` → the recorded `403` on the offer and the revoke (AC9).
- `:40` → **UPDATED**: the Offer indicator's 2px and the offer dialog's 6px are mapped. Still unmapped: buttons, and dialogs other than these.
- `:929` → **CLOSED by 5.2** if folded in (AC7).
- **New entries:**
  - "The Offer indicator updates only at this tab's next read; a revoke or lapse elsewhere is not pushed" (owner **7.7**; low), Q5.
  - "The offer dialog's copy is the mockup's, minus the link" (owner **UX**; low), Q4.
  - Under Q3 (a): "Decline exists since 5.2; 5.3 adds telling the Owner and the settled-state proofs" (owner **5.3**).

### Architecture compliance (must)

- **AD-21 ring.** Slices go under `Yello.Application/Spaces/AcceptOwnershipOffer/` and `ReadMyOwnershipOffer/`. The store goes in Infrastructure, the endpoints in Host and the contracts in `Yello.Contracts/Spaces/`. Check `AllowedReferenceEdges.cs`.
- **AR-3 / AD-1.** Slices never read a Role, set a key, record idempotency or record refusals. The identity check is persistence.
- **AD-2.** No raw SQL outside Infrastructure. Every statement carries `SpaceId = @space` as well as relying on the key.
- **AD-3 / AD-20.** Every not-found is the boundary body. Only the pipeline records.
- **AD-10.** No timer, no hosted service, no polling.
- **AD-18.** The accept accepts an `Idempotency-Key`, and the guard still refuses a second accept under a new key.
- **AD-23.** No address in a log, a response, an idempotency record, the indicator or the dialog.
- **AD-26.** Identity, never Role. Guarded `WHERE State = Pending` plus rowcount. Demote then promote, by `ExecuteUpdate`, in one transaction. `409` with a stable type, never `404`, to the recipient on a settled offer.
- **AD-27.** One predicate, one file, the database clock.
- **AR-34.** `DateTimeOffset` in UTC, problem+json.
- **Code comments** (CLAUDE.md): they say why. No "added by story 5.2", no AC or Q ids, no history, no XML doc that restates a member name.

### Library / framework

No new package and no migration.

- EF Core 10.0.11: `ExecuteUpdateAsync` with `SetProperty` on an enum-to-string converted property (supported since EF 7; the converter applies), `AsNoTracking` projections.
- ASP.NET Core 10.0.11 minimal APIs.
- Blazor WebAssembly 10.0.11.
- Microsoft.Data.SqlClient 7.0.2 (`SqlException.Number` 2601/2627).
- SQL Server 2025: the filtered unique index, an `AFTER UPDATE` trigger in the test, `sp_getapplock`.
- xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0 (`Page.Clock`), ArchUnitNET 0.13.3, and Mono.Cecil for the IL gates.

No web research was needed: every API used is already in the codebase. One check is still required: `ExecuteUpdateAsync` must translate `m.Role == Role.Owner` and `SetProperty(m => m.Role, Role.Admin)` through the string converter. A query-string assertion or a Slices case will show it.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`: `:44-86` traits, `:87-207` rules, `:102-113` races, `:517-543` outbox, `:545-580` Memberships, `:575-580` the Role header in the browser fake, `:773-895` isolation.

- **Traits:** `Suite`, `Priority`, `Requirement` (FR-42, FR-14, AD-26, AD-9, AD-5, AR-13, AR-16, AR-12, UX-DR9, UX-DR37, UX-DR41, S-2, V-10, A-14), `Assumption` (`PRD-12-3` on the expiry cases), `Tier`. Underscore-sentence names.
- **Never `Task.Delay`.** Races wait on `LCK_M_%`. Racing Accounts are made present and fetch an anti-forgery token first.
- **Seed through the product** (accepted Invitations, then a product offer). Seed directly only for time-expired rows (R12), the `OfferStale` row (Q8) and the trigger case. Record which.
- **S-2 after every case.** Plus the every-Space case.
- **`MembershipChanged`:** count `MembershipChangedPublished` lines naming the case's own ids, in a `HostProcess.RunAsync` case.
- **Mail:** wait for the offer's own email first, then accept, then assert no new mail **to this address** (never "any N"). In `ActiveSpaceEndpointTests`, a case that waits on mail calls `SoleDispatcherWireAsync()` first and runs its own `HostProcess.RunAsync`.
- **No new container-backed test class.**
  - Slices partials: `ActiveSpaceEndpointTests.AcceptOwnershipOffer.cs`, `.AcceptOwnershipOfferRace.cs`, `.ReadMyOwnershipOffer.cs`, plus additions to `.RoleMatrix.cs` if needed and to the 5.1 files for `:929`.
  - Handler unit tests with fakes (no database).
  - Isolation: rows in `IsolationCases.OwnershipOffers.cs`.
  - Client: `OwnershipOfferTests.cs` on the existing collection.
  - Gates: `RefusalBoundaryGateTests.OwnershipOffers.cs`, `.RemoveMembership.cs` (the allow-list), `DesignFoundationGateTests*`.
- **Every absence has an anchor and is seen red under a plant.** A masked plant is recorded, never deleted.
- **Memory budget (CLAUDE.md).** One `dotnet test` at a time. Iterate with `--project ... -- --filter-class "<full name>"`. The single full run goes in the background with `--max-parallel-test-modules 1`, without the browser suite in Develop. The 5.1 develop plant batch was killed by the low-memory reaper, so run plants in small groups.

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | Promote before demote | the accept cases (`409 owner-role-fixed`, recipient not Owner); R9's verification |
| 2 | The swap through tracked entities and `SaveChangesAsync` | the A-14 gate |
| 3 | The accept's identity clause removed (any Member may accept) | the not-the-recipient cases (`204` instead of `404`); `ISO-ACCEPT-OFFER-NAMING-ANOTHER-MEMBERSHIP` |
| 4 | The accept without `.Where(OwnershipOfferActionable)` | the accept-twice and the expired cases |
| 5 | The accept skips the Memberships lock | race 1's "both waited" assertion (or recorded as masked) |
| 6 | The former Owner's `Record` dropped | the two-publishes case |
| 7 | The demote's exact-1 check removed | the `OfferStale` case (the recipient promoted with the old Owner still Owner → `2601` → `409 owner-role-fixed`, or recorded) |
| 8 | The `2601` catch on the promote removed | the trigger case (`500`) |
| 9 | `NoLongerOwner` removed | the AC9 stale-offer race (`400`) |
| 10 | An `IOutbox` added to the accept handler | the no-outbox gate |
| 11 | A second accent element in the context bar | the accent gate |
| 12 | The indicator hidden by a media query | the no-hide gate; the 320px case |
| 13 | `autofocus` on Accept | the no-default browser case (Enter accepts) |
| 14 | The dialog's days computed from `DateTimeOffset.Now` | the browser-clock case; `The_Ownership_section_reads_no_browser_clock` |
| 15 | No reread after a `204` | the narration and Owner-section case |
| 16 | The `mine` read without the recipient clause | a non-recipient sees the indicator |

### Previous story intelligence (5.1, 4.7, 4.6, 4.5)

- **5.1:**
  - The offer is stored, the lapse is written, the revoke is guarded. `OwnershipOfferStore` is the one sanctioned writer, and the gate now reads raw SQL too.
  - Reviews patched comments carrying story and AD ids in 29 files: write them clean.
  - The develop plant batch was killed by the low-memory reaper, so run plants in small groups.
  - The first browser-suite run caught a read-count case in `MembershipSectionTests` that the new section disturbed. The context bar's new `mine` read may likewise disturb cases that count requests (grep the client tests for request counts on arrival, and widen them deliberately).
  - `PopulateSpaceAsync` now makes an offer, so a populated Space has a pending offer naming a Member. Keep that in mind in DeleteSpace and any case that reads the indicator.
- **4.7:**
  - The header is the client's only Role correction.
  - `RoleNarrationOwner` exists for this story.
  - A case that stops its Host before its own emails are sent leaks them into the next case's drop: the producer waits for its own mail.
  - The narration order is proved with a `MutationObserver` log.
- **4.6:** the actor's name is captured when an item is authored (the offer's `OffererName` follows this). A new pipeline dependency needs registration in `SpaceBoundaryPipelineTests`/`ActiveSpaceUnitOfWorkTests`.
- **4.5:** identity-decided leave under `ReadProjectsAndTasks`, its gate, `409 ownership-held`, and `LostMembership` → the pipeline's recorded `404`.
- **4.4:** the guarded Role statement with a `READCOMMITTEDLOCK` actor clause, `2601/2627` → `409 owner-role-fixed`, and S1200 pushing code into partials.

### Git intelligence

Recent commits:
- `89b98a3`: 5.1 review (the state-writer gate reads raw SQL; comment cleanup).
- `542f95d`: 5.1 implementation (OwnershipOffer table and routes, lapse, email, Ownership section).
- `54c5aa7`: `NotAvailableFloor` waits on its own clock.
- `67d92bb`: 5.1 ATDD.
- `e5bf109`: 5.1 story.

The cycle: story, then decisions recorded, then ATDD scaffolds (`Skip = "red: ..."`, `atdd-checklist-<key>.md`, `PendingUntil` rows), then implementation, then review fixes. The orchestrator commits each.

### Project Structure Notes

- **New (expected):**
  - `Yello.Contracts/Spaces/OwnershipOfferForRecipientResponse.cs`
  - `Yello.Application/Spaces/AcceptOwnershipOffer/AcceptOwnershipOfferHandler.cs`, `ReadMyOwnershipOffer/ReadMyOwnershipOfferHandler.cs` (plus `DeclineOwnershipOffer/` under Q3 (a))
  - `Yello.Client/Components/OwnershipOfferDialog.razor`, `Yello.Client/Spaces/OwnershipOfferState.cs`
  - Tests: `ActiveSpaceEndpointTests.AcceptOwnershipOffer.cs`, `.AcceptOwnershipOfferRace.cs`, `.ReadMyOwnershipOffer.cs`, `tests/Yello.Tests.Slices/Spaces/AcceptOwnershipOffer/AcceptOwnershipOfferHandlerTests.cs`, `tests/Yello.Tests.Client/OwnershipOfferTests.cs`
- **Updated (expected):**
  - Contracts: `OwnershipOfferRoutes.cs`, `ProblemTypes.cs` (remarks only).
  - Application: `IOwnershipOfferStore.cs`.
  - Infrastructure: `OwnershipOfferStore.cs`, `OfferPredicates.cs` (remark), `SpaceContentStores.cs`.
  - Host: `OwnershipOfferEndpoints.cs`, `Program.cs`.
  - Client: `ContextBar.razor`, `ClientCopy.resx`, `components.css`, `RefusalCopy.cs` (if needed), and the DI registration for `OwnershipOfferState`.
  - Domain remarks: `RoleChangeAuthority.cs`, `AssignableRoles.cs`, `OwnershipOfferState.cs` (if any predict 5.2).
  - Tests: `WireDatabase.cs` (`AllSpaceIdsAsync`), `OwnershipOfferWire.cs`, `FakeYelloApi.OwnershipOffers.cs`, `IsolationCases.OwnershipOffers.cs`, `RefusalBoundaryGateTests.OwnershipOffers.cs`, `RefusalBoundaryGateTests.RemoveMembership.cs`, `DesignFoundationGateTests*.cs`, `SmallViewportTests.cs`, `TextSpacingTests.cs`, and the six 5.1 Slices cases (`:929`).
  - Docs: `ARCHITECTURE-SPINE.md`, `deferred-work.md`, `sprint-status.yaml`.
- **No new project, no new package, no new container-backed test class, no migration.**

### References

- `epics.md`: `:88-89`, `:103`, `:200-204`, `:213`, `:219`, `:222`, `:240`, `:247`, `:250`, `:267`, `:271-273`, `:281-286`, `:335`, `:364`, `:376-378`, `:459-467`, `:1924-1973` (5.1), `:1975-2022` (5.2), `:2024-2065` (5.3), `:2079-2100` (5.4).
- `prd.md`: `:59-64`, `:121-122`, `:155-164`, `:171`, `:213-227`, `:229-241`, `:300`, `:309`, `:332-359`, `:663-672`, `:796`, `:803-811`, `:894`; `addendum.md:33`, `:38-55`, `:151`.
- `ARCHITECTURE-SPINE.md`: AD-1 (`:71-77`), AD-2, AD-3 (`:92-98`), AD-4, AD-5 (`:106-114`), AD-8 (`:129-133`), AD-9 (`:135-141`), AD-10, AD-18, AD-20, AD-24, AD-26 (`:251-257`), AD-27 (`:259-264`), conventions `:291-296`, tree `:413`. `reviews/review-web-verification.md:9-27`, `reviews/review-adversarial-seam.md:16`, `:29-37`, `:43-49`, `:59`.
- `EXPERIENCE.md`: `:48`, `:65`, `:81-89`, `:99-119`, `:202-204`, `:221`, `:244`, `:261-263`, `:373-375`, `:390-404`, `:419`, `:438`, `:541-553`. `DESIGN.md`: `:19-20`, `:34-35`, `:169-179`, `:204-229`, `:319`, `:439`, `:456`, `:468-469`, `:480`. Mockup `mockups/ownership-offer.html:52-53`, `:97`, `:115-158`. `review-isolation.md:24`, `:28`, `:86`, `:112`.
- `specs/spec-yello/`: `role-capability-matrix.md:24`, `:28`, `:38-40`; `acceptance-criteria.md:83-92`; `SPEC.md:65-67`, `:200-201`.
- `test-design-qa.md`: `:172-173`, `:289` (V-10), `:319` (S-2), `:365` (A-14), `:494-496`. `test-design-architecture.md`: `:156-159`, `:219`, `:531-542` (R9). `test-design/YelloBMAD-handoff.md:56`, `:100`.
- `implementation-readiness-report-2026-08-22.md`: `:187`, `:521`, `:548`, `:799`, `:1057`. `sprint-change-proposal-2026-09-13.md`: no ownership content.
- `tests/TESTING-CONVENTIONS.md`: `:44-86`, `:87-207`, `:209-437`, `:517-543`, `:545-580`, `:604-673`, `:773-895`.
- `deferred-work.md`: `:38-40`, `:664`, `:744`, `:792-793`, `:807`, `:908-909`, `:915`, `:929`.
- Prior stories: `5-1-offer-ownership-of-a-space-and-tell-the-recipient.md` (whole), `4-7-a-viewers-space-and-a-role-that-drops-beneath-you.md` (Role header, narration), `4-5-remove-a-membership-or-leave-a-space.md` (identity-decided leave), `4-4-manage-memberships-and-roles.md` (guarded Role statement, `owner-role-fixed`).

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

- 2026-11-08: Story created (bmad-create-story). Status ready-for-dev.
