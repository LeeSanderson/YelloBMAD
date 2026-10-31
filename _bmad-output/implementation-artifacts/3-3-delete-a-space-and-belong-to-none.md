---
baseline_commit: 5e04528
---

# Story 3.3: Delete a Space, and belong to none

Status: ready-for-dev

Epic: 3 — Several Spaces, cleanly separated
Story key: `3-3-delete-a-space-and-belong-to-none`

**Requirements owned.** From the Story Coverage Index (`epics.md:367`: "3.3 | FR-7, UX-DR36, UX-DR37, §6.2").

- **FR-7** (`prd.md:202-211`).
  - "Only the Owner can delete a Space. Admins cannot."
  - "Deletion removes every Membership in that Space; other Accounts lose access without losing their own Spaces or their Account."
  - "Deletion invalidates every API Token issued for that Space (FR-36)."
  - "An Account may delete its last remaining Space. Belonging to no Space is a valid state ... No Space is undeletable."
  - Irreversibility is PRD §12 assumption **#2 (†, unconfirmed)** (`prd.md:893`). Tag every case that asserts it `Assumption=PRD-12-2`.
- **§6.2** (`prd.md:769-774`). "Deletion of an Account, Space, Project or Task is irreversible in v1. There is no trash and no restore." "Deleting a Space deletes its contents for every Member, and this is stated at the point of the action."
- **UX-DR36 / UX-DR37** (`epics.md:280-281`; `EXPERIENCE.md:99-122`, `:269`). Destructive copy names the object and states "This cannot be undone." with no reassurance before it. The delete-Space confirm names the Space, its Project and Task counts, and that other Accounts lose access, and asks for the Space name to be typed.

**Also binding, though the trace row omits them:**

- **FR-36 / AD-6** (`prd.md:615`; `ARCHITECTURE-SPINE.md:112`). Space deletion invalidates the Token **in the same transaction**. V-3 (`test-design-qa.md:282`, P0).
- **AD-1 / AD-3** (`ARCHITECTURE-SPINE.md:75`, `:92`). No Membership gives `404 not-available`. A Role the matrix denies gives `403 insufficient-role`. Neither is converted into the other. Error bodies carry no Space name.
- **AD-2** (`:81-86`). Every unit of work sets `SESSION_CONTEXT('SpaceId')` read-only from `ActiveSpaceContext`.
- **AD-4** (`:100`). Both surfaces use one path. Every isolation case runs on Session **and** ApiToken.
- **AD-18 / AR-25** (`:186`). Every state-changing endpoint accepts an `Idempotency-Key`. See Q1: this route cannot keep a Space-scoped record.
- **AD-20 / NFR-7** (`:198`; `prd.md:730-734`). The pipeline records every `403` and every boundary `404`. `AccessRefusal` has no FK to `Space` and must outlive it (90 days).
- **AD-24 and its amendment of 2026-10-29** (`:218-228`). `Space` is policy-free, and its writers are a named list. Deletion adds a writer, so the amendment gains one dated line (Task 2).
- **UX-DR12, UX-DR27, UX-DR32, UX-DR33, UX-DR35, UX-DR38** (`epics.md:250-282`):
  - Esc closes, and focus returns to the invoker.
  - A missing capability means the control is absent, not disabled.
  - The route back goes to a listed Space, never a remembered one.
  - The typed-name field defeats autofill.
  - Belonging to no Space is a valid state.
  - No announcement carries a Space name.
- **EXPERIENCE.md**:
  - `:67`: modals go one level deep, never two. The confirm replaces the panel's content in place.
  - `:253`: belonging to no Space means offering creation and never auto-creating.
  - `:335`: no optimistic UI on anything destructive.
  - `:403`: focus goes to the nearest surviving container.
- **DESIGN.md**:
  - `:277-284`: `destructive-confirm` uses the danger border and `button-danger`.
  - `:476`: the copy is the signal. No icon.
  - `:519`: nothing on the destructive path animates.
- **NFR-1, NFR-2** (`prd.md:683-700`). Revocation governs the very next request, with no tolerance.
- **NFR-9**. WCAG 2.1 AA, keyboard-complete.

**This story is mostly a new slice, plus a set of closures.**

- The schema already cascades correctly: Space → Membership → ApiToken, Space → Project → Task → TaskLabel, Label, StatusDefinition, SpacePresence and IdempotencyRecord.
- The raw-SQL proof already exists (`WireDatabase.DeleteSpaceAsync`, `ActiveSpaceEndpointTests.Deleting_the_Space_invalidates_its_Tokens`, `REV-SPACE-DELETED`).
- The no-Space state on `/` already exists (3.1 AC15).
- `Capability.DeleteSpace` is already Owner-only in `RoleCapabilityMatrix` (`[Yes, No, No, No]`), and its refusal copy `delete-space` ("Only the Owner can delete the Space.") is already in `ClientCopy.resx:506`.

**What is new:**

- the endpoint;
- the pipeline's handling of a unit of work that destroys its own Space (Q1, Q4);
- the counts read;
- the confirm;
- the post-deletion route;
- five ledger closures.

**Explicitly not in this story:**

- **Live-session termination** (V-11, FR-34) and the `MembershipChanged` publish (AD-9). No publisher or sync lease exists until Epic 7, so this belongs to **stories 7.3 and 7.7**. Ledger it (see Ledger dispositions). Do not build a publisher seam now.
- **The Invitation and Ownership Offer tables** belong to Epics 4 and 5. The FK behaviour of those tables when a Space is deleted is ledgered to 4.1 and 5.1 (contradictions C1 and C2 below).
- **Transport failure and 5xx copy** (`deferred-work.md:354`) is story 7.6's. Do not take it.
- **Isolation for one Account owning two Spaces** (the SM-1 extension) is story 3.4's.
- **Out of scope by design:** trash, restore, soft delete and the word "archive" (`EXPERIENCE.md:458`). No auto-created Space. No optimistic delete. No remembered last Space.
- **No new NuGet package and no new project** (CLAUDE.md). **No Azure deployment, login or push. No commit**: the orchestrator commits.

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

- `:370` (medium). **Space deletion invalidates its Tokens only if it deletes the `Space` row in its own transaction**, under that Space's session context. Add V-3's case for the event. Covered by AC4 and AC11.
- `:382` (note). `REV-SPACE-DELETED` runs on raw SQL. 3.3 adds the **endpoint-driven** row to `RevocationCases`. Covered by AC11.
- `:360`. **`ActiveSpaceNameReader` throws if the Space is deleted between resolution and the name read** (`SingleAsync` gives a `500`). Covered by AC13.
- `:666`. **Rename answers 200 when no `Space` row was updated.** Covered by AC13.
- `:356`. **The switcher opens as an empty, unexplained list** when its answer fails or the Account has no Space (`ContextBar.razor` `ToggleAsync`). Covered by AC14 and Q7.
- `:679`. **On the all-refused page at `/`, "Back to Yello" links to the page it is on**, and the copy is generic. Covered by AC15 and Q8.

**Depends on** (all done):

- 1.6: the pipeline, the refusal recorder and `BoundaryProblems`.
- 1.8: Tokens and the cascade.
- 1.9: Isolation and Revocation suites, `RevocationCases`.
- 2.5: the delete pattern. Inline `destructive-confirm`, `SendWithoutBodyAsync`, a per-object key reused after an unanswered attempt, and the `DeleteWire` race tests.
- 3.1: `SpaceStore`, Space settings, the no-Space state, the AD-24 amendment, the 50-Space bound counted live.
- 3.2: `SpaceDirectory`'s refused set, `ApiNoStore`, the purge seam.

## Story

As someone finished with a body of work,
I want to destroy a Space and be told exactly what that takes with it,
So that the most destructive operation in the product is never taken by accident.

## Acceptance Criteria

AC1 to AC8 are reproduced **verbatim** from `epics.md:1539-1573` (`status: final`) and numbered by position. AC9 to AC16 come from the spines, the ledger and a survey of the code. Each one cites its source.

**AC1 — the Owner deletes; an Admin cannot** (FR-7, UX-DR27)
**Given** a Space
**When** its Owner deletes it
**Then** its Projects, Tasks, Memberships and Invitations are destroyed
**And** an Admin has no delete affordance present at all

> Invitations do not exist yet (Epic 4), so "Invitations are destroyed" has nothing to assert today. Write the clause into the slice's remarks, and ledger the FK decision to 4.1 (C1). Labels, Statuses, SpacePresence and the Space's IdempotencyRecords go too, through the existing cascades. A Member and a Viewer also see no affordance. The server refuses any non-Owner `DELETE` with `403 insufficient-role` and an `InsufficientRole` record.

**AC2 — the confirm states the cost** (§6.2, UX-DR37)
**Given** the delete confirm
**When** it renders
**Then** it names the Space, states its Project and Task counts, and states that other Accounts lose access — §6.2 requiring this at the point of the action

**AC3 — the typed name** (UX-DR33, UX-DR37)
**Given** the delete confirm
**When** it asks for confirmation
**Then** it requires the Space name to be typed, in a field carrying `autocomplete="off"` and a non-reusable field name

**AC4 — immediate, irreversible, Tokens in the same transaction** (FR-7, FR-36, AD-6, §6.2)
**Given** a confirmed deletion
**When** it completes
**Then** it is immediate and irreversible with no trash and no restore window
**And** every API Token issued for that Space is invalidated in the same transaction

**AC5 — other Accounts lose only this Space** (FR-7)
**Given** other Accounts holding Membership in the deleted Space
**When** the deletion completes
**Then** they lose access to it without losing their own Spaces, their own Accounts, or any other Membership

**AC6 — belonging to none** (FR-7, UX-DR35)
**Given** an Account deleting its last remaining Space
**When** it completes
**Then** belonging to no Space is a valid state — the Account persists and is offered the chance to create a Space rather than having one created for it
**And** no Space anywhere in the product is undeletable

**AC7 — one level of modal** (`EXPERIENCE.md:67`)
**Given** the delete confirm invoked from inside Space settings
**When** it opens
**Then** it replaces that panel's content in place rather than stacking on it, because modals go one level deep and never two

**AC8 — the copy** (UX-DR36)
**Given** the deletion copy
**When** it is reviewed
**Then** it names the object — "Delete Northwind Redesign?" — rather than "Delete this item?"
**And** it states "This cannot be undone." with no sentence of reassurance placed before the fact

**AC9 — the wire contract** (AD-1, AD-3, AD-18, AD-20; Q1, Q2, Q3)

- `DELETE /api/v1/spaces/{spaceId}` (`SpaceRoutes.Active`) is mapped with `.RequiresCapability(Capability.DeleteSpace)`.
- It answers `204` with no body once the deletion has committed.
- A non-member, or a Space that does not exist, gets the boundary `404 not-available` with a `CrossSpace` record. The answer is byte-identical to every other boundary 404, and it is held to `NotAvailableFloor`.
- An Admin, Member or Viewer gets `403 insufficient-role` with an `InsufficientRole` record.
- A malformed `Idempotency-Key` gets `400`. A retried delete with the same key never applies twice and never answers `500` (Q1).
- The counts read (Q2) is Owner-only and answers counts and nothing else.
- No response body, log line or refusal record carries the Space name (I-13, I-17).

**AC10 — one transaction, under the Space key only** (AD-2, AD-6, AD-24; `deferred-work.md:370`)

- The `Space` row is deleted by `SpaceStore` (the gate `The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space` names `SpaceStore` as the only writer). The deletion runs inside the request's own Space-scoped unit of work, filtered on the resolved `context.SpaceId`, as **one** `ExecuteDeleteAsync`, and the database cascades do the rest.
- No unit of work sets the `AccountId` key alongside it. `No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys` stays green and is not widened (Q1).
- A Space that holds Projects, Tasks on every Status, applied Labels, several Memberships, Tokens for several Accounts and a SpacePresence row is deleted by that one statement. `Task → StatusDefinition` and `TaskLabel → Label` are `NoAction`, so prove it with data rather than assume it.
- `AccessRefusal` rows naming the Space survive the deletion. They have no FK; do not add one.
- A forced failure after the delete statement and before commit leaves every row in place (S-1, atomicity).

**AC11 — Tokens and Sessions on the next request** (V-3, NFR-2; `:370`, `:382`)

- The next request with a Token bound to the deleted Space is `401 authentication-failed`, on the Token's own Space route and on every other route.
- The next Session request for the deleted Space, from the Owner or any former Member, is the boundary `404`.
- The same Account's Tokens and Sessions for its other Spaces keep working.
- `RevocationCases` gains an endpoint-driven row beside `REV-SPACE-DELETED`, for example `REV-SPACE-DELETED-BY-OWNER`, on both surfaces.

**AC12 — the client after a deletion** (UX-DR32, UX-DR35, UX-DR38, `EXPERIENCE.md:335`, `:403`)

- The delete is never optimistic. While the answer is out, the confirm stays as it is, marked `aria-busy`, and its controls are not disabled (the 2.5 pattern).
- On `204`:
  - the deleted Space leaves `SpaceDirectory` and the Space-scoped state is purged;
  - the tab navigates (`replace: true`) to `/`;
  - Home lands on the first remaining listed Space, or on the no-Space state when none remain;
  - focus goes to that page's heading;
  - one polite announcement is made with no Space name (Q6).
- When no answer arrives, or a `5xx` arrives, the confirm stays open with 2.5's not-sent copy. A retry reuses the same key (Q1).
- A `404` on the delete means the Space is already gone for this Account, whether this tab's own earlier attempt deleted it or someone else did. It is treated like a `204` for routing: leave, purge, go home. The announcement is not made (Q1).
- A former Member's tab that is open on the deleted Space gets the boundary refusal on its next request. Its route back lands on another listed Space (3.2 AC12). Nothing in that tab is purged ahead of a request: that is Epic 7's live push.

**AC13 — the races that deletion makes reachable** (`deferred-work.md:360`, `:666`; Q4)

- A rename whose update touches no row (the Space was deleted after resolution) answers the boundary `404`, not `200`. `ISpaceStore.RenameAsync` reports the row count, and the endpoint maps `0` to `BoundaryProblems.NotAvailable()`. Follow the `ProjectStore.RenameAsync` precedent.
- `ActiveSpaceNameReader` uses `SingleOrDefaultAsync` and answers the boundary `404` on `null`.
- A Space-scoped write that loses to a concurrent deletion answers the boundary `404` and never `500`, per Q4. This covers a Project or Task create, the presence insert, and an idempotency record insert.
- Each case is proved by a deterministic race that holds one side on a lock and waits on `LCK_M_%` (TESTING-CONVENTIONS "Races"). Never by timing.

**AC14 — the switcher with nothing to list** (`deferred-work.md:356`; Q7)

- With an empty switcher answer, the open switcher shows a sentence saying the Account belongs to no Space. The sentence sits outside the `<ul>`, and the foot "Create a Space" link stays. It is never an empty, unexplained panel.
- With a failed refresh and nothing held, it shows a distinct sentence saying the list could not be loaded. It never shows the "no Space" sentence for a failure.
- The `<ul>` still holds rows of names only, or is absent.

**AC15 — the all-refused page at `/`** (`deferred-work.md:679`; Q8)

- On `/`, when every listed Space has refused in this load, or the switcher could not be asked, the refusal page does not offer a "Back to Yello" link to the page it is already on.
- It points to the context bar's switcher instead (Q8's copy). The heading stays the uniform "Not available." for the 404 family, so no client-side branch separates 404 causes (`EXPERIENCE.md:130-147`).
- `RefusalFingerprint` comparisons in existing cases elsewhere (refusals on Space routes) stay unchanged.

**AC16 — keyboard, focus and reflow** (NFR-9, UX-DR12, UX-DR40, UX-DR41)

- The delete section and the confirm work by keyboard alone.
- Opening the confirm moves focus to its first actionable control, the typed-name field (Q5).
- Esc, or Cancel, restores the panel's content and returns focus to "Delete this Space".
- The Tab order follows reading order, and every stop has a visible ring that is not inset.
- At 320×256 there is no sideways scroll, and the Role chip stays.

## Tasks / Subtasks

- [ ] **Task 1: decide the open questions** (all ACs)
  - [ ] Read Dev Notes, then Open questions. Decide each one as a reversible decision and record it in Completion Notes as "Qn - (x)", with its cost. Each recommendation is the default, and no human is in the loop, so do not stop to ask. **Exception:** a question marked **[LEE]** relaxes an isolation or security rule under its non-recommended option. Take the recommended option, which relaxes nothing. Never take the [LEE] option without a recorded decision from Lee.
- [ ] **Task 2: record the AD-24 / AD-18 notes** (AC9, AC10; Q1)
  - [ ] In `ARCHITECTURE-SPINE.md`, add one dated bullet under AD-24's amendment: "**Amendment (2026-10-31, story 3.3):** `SpaceStore` gains the one deletion of `Space`, the resolved Space only, under the `SpaceId` key alone. `The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space` becomes '... the rename or the deletion of the resolved Space'." It is additive; do not rewrite the rule.
  - [ ] Under AD-18, add the Q1 variance line: what a replayed Space deletion answers, and why.
- [ ] **Task 3: Application and Infrastructure** (AC1, AC4, AC10, AC13)
  - [ ] `Yello.Application/Spaces/DeleteSpace/DeleteSpaceHandler.cs` (new slice folder, matching `RenameSpace/`) calls `ISpaceStore.DeleteAsync(ActiveSpaceContext, ct)` and returns `SpaceDeletion { Deleted, NotFound }`, the same shape as `ProjectDeletion`. Register it as the other Space handlers are registered.
  - [ ] `ISpaceStore.DeleteAsync`. `SpaceStore.DeleteAsync`:
    - Optionally take `ApplicationLocks.TakeProjectsPerSpaceAsync` first (Q4).
    - Then `dbContext.Spaces.Where(s => s.Id == context.SpaceId).ExecuteDeleteAsync(ct)`.
    - `0` rows gives `NotFound`.
    - It is one statement. Never delete child tables by hand: the cascades are the contract.
  - [ ] `ISpaceStore.RenameAsync` returns the row count, or a result type. `RenameSpaceHandler` and `ActiveSpaceEndpoints.RenameAsync` map `0` to `BoundaryProblems.NotAvailable()` (`:666`).
  - [ ] `ActiveSpaceNameReader`: `SingleAsync` becomes `SingleOrDefaultAsync`, and its caller maps `null` to the boundary 404 (`:360`).
  - [ ] Widen `RefusalBoundaryGateTests.Space_is_read_only_where_named` only if a new type touches `Space`. Keeping the delete inside `SpaceStore` needs no widening. Amend `The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space` to admit the deletion, citing the 2026-10-31 amendment in its message.
- [ ] **Task 4: Host** (AC9, AC12, AC13; Q1, Q2, Q4)
  - [ ] `ActiveSpaceEndpoints.MapDeleteSpace` maps `MapDelete(SpaceRoutes.Active, DeleteAsync).RequiresCapability(Capability.DeleteSpace)`. It answers `Results.NoContent()`, or `BoundaryProblems.NotAvailable()` on `NotFound`. Map it in `Program.cs` beside `MapRenameSpace`.
  - [ ] Implement Q1 in `ActiveSpaceResolution.Idempotency.cs`, with a route marker in the style of `CarriesSecretInResponse`. **Note:** today the pipeline records the idempotency record **after** the endpoint (`ActiveSpaceResolution.cs:263-269`). For a deleted Space, that insert fails on `FK_IdempotencyRecord_Space` (error 547) and gives a `500`. This must not ship.
  - [ ] Implement Q2's counts read.
  - [ ] Implement Q4: a Space-scoped unit of work that fails because its Space was deleted under it answers the boundary `404`.
  - [ ] `SpaceLog.SpaceDeleted(logger, spaceId)` logs the id only, never the name (I-17).
- [ ] **Task 5: Contracts** (AC9; Q2)
  - [ ] Add `SpaceRoutes.DeletionSummary`, or whatever Q2 names, and its response record in `Yello.Contracts/Spaces/`. No request body for `DELETE` (Q3).
- [ ] **Task 6: Client** (AC2, AC3, AC7, AC8, AC12, AC14, AC15, AC16)
  - [ ] **The section.** `Components/SpaceDeleteSection.razor` (new) is rendered by `SpaceSettings.razor` for an Owner only. It is `@key`'d on the Space, inside `SpaceArrival`, like `SpaceNameSection`.
    - Add `SpaceCapabilities.PermitsDeleteSpace` (Owner only, mirroring the matrix), and add it to the `PermitsSpaceSettings` union. `The_clients_Space_settings_predicate_is_the_union_of_its_sections` must stay green, and `Every_permit_phrase_agrees_with_the_capability_matrix` must agree.
    - For an Admin, the section is **absent** from the DOM, not hidden and not disabled.
  - [ ] **The confirm replaces the panel's content in place** (AC7). While it is open, `SpaceSettings` renders the confirm **instead of** the name and Label sections, with no scrim, no `<dialog>` and no second layer. On Esc or Cancel, the sections return and focus goes to "Delete this Space". It is the inline `div.destructive-confirm` from 2.5 (`ProjectRow.razor`), scaled to the page. Reuse its CSS class; add only what is missing to `components.css` (logical properties, rem/em, tokens, no animation).
  - [ ] **Its content (AC2, AC8; Q5 copy), in order:**
    - `DeleteQuestion` ("Delete {0}?") as the heading, with the Space's current name;
    - `DeleteCannotBeUndone` ("This cannot be undone.") directly after, with nothing reassuring before it;
    - the counts sentence or list;
    - the other-Accounts sentence;
    - the typed-name field;
    - "Delete this Space" (`button-danger`);
    - `DeleteCancel` (`button-secondary`).
    - Read the counts **when the confirm opens** (Q2). Never show cached counts.
  - [ ] **The typed name** (AC3; Q5).
    - A real `<label>`, `autocomplete="off"`, and a field `name` fresh per render, using the helper `SpaceNameSection` and `CreateSpace` already use (find it; do not write a second one).
    - Pressing Delete when the typed text does not match sends nothing and shows Q5's inline message.
    - The button is never disabled.
  - [ ] **The send.** `SpaceWrites.SendWithoutBodyAsync(HttpMethod.Delete, SpaceRoutes.ActiveOf(id), key, ActiveSpace.Token)`. Keep one key for the life of the confirm, reused after an unanswered attempt (the 2.5 `_deleteKeys` pattern).
  - [ ] **The outcomes** (AC12):
    - `204` or `404`: call `SpaceDirectory.Removed(id)` (new; it drops the row from `Current` and from the refused set), then `NavigateTo(ClientRoutes.Home, replace: true)`. The navigation purges through `Navigating`.
    - `403`: the `delete-space` refusal copy, in the section.
    - `5xx` or no answer: `TaskEditNotSent` (or 2.5's equivalent), with the confirm left open.
    - Announce per Q6 so that the sentence survives the navigation. Use `SpaceNarration` only if Q6 says so, keeping in mind that 3.2 registered it with the Space purge, so a sentence held for the deleted Space is purged on leaving it.
  - [ ] **The switcher** (AC14; Q7): `ContextBar.ToggleAsync` and `SpaceSwitcher.razor` tell "empty" apart from "failed". Copy goes in `ClientCopy.resx`.
  - [ ] **Home and RefusalSurface** (AC15; Q8).
  - [ ] Copy goes only in `ClientCopy.resx`. Each entry carries a `<comment>` naming 3.3 and the AC. Announcements carry no Space name (UX-DR38). The confirm's heading names it, because that is visible text the Owner is acting on.
- [ ] **Task 7: isolation and revocation rows** (AC9, AC11; GATE-002, GATE-003)
  - [ ] `tests/Yello.Tests.Isolation/IsolationCases.cs`, each row on **both** surfaces:
    - `ISO-DELETE-SPACE-CROSS`: delete a foreign Space gives `404`, the Space survives, and a `CrossSpace` record is written;
    - `ISO-DELETE-SPACE-ADMIN` (or the Viewer/Member variant matching `ISO-RECORD-KIND-PROJECT-DELETE-VIEWER`): `403`, the Space survives, and an `InsufficientRole` record is written;
    - a row for Q2's counts read: foreign gives `404`, and a non-Owner gives `403`.
    - Copy the shape of `ISO-DELETE-PROJECT-FOREIGN-SPACE` (`:656`) and `ISO-RENAME-SPACE-CROSS` (`:849`). Leave no row pending: this story reaches review with them.
  - [ ] `tests/Yello.Tests.Revocation/RevocationCases.cs`: the endpoint-driven Space-deletion row (AC11). Keep `REV-SPACE-DELETED` (raw SQL) as the schema proof, or retarget it and say which.
- [ ] **Task 8: tests** (see Testing requirements). Build `Yello.Client` in **Debug** before any browser run or plant.
- [ ] **Task 9: ledger, docs, verification**
  - [ ] `deferred-work.md`: apply Ledger dispositions.
  - [ ] Rewrite any comment that predicts 3.3 so it describes what exists (`grep -rn "3\.3" Yello.* tests`). Example: `Home.razor:23-24`.
  - [ ] Run `dotnet build Yello.slnx` (0 warnings, 0 errors), then `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Record the totals. The expected skips are the 3 bundle cases plus the explicit tiers.

## Dev Notes

### The shape of the story in one paragraph

The database already knows how to delete a Space. One `DELETE FROM dbo.[Space] WHERE Id = @resolved`, run under that Space's session key, cascades to everything, takes the Tokens with the Memberships, and leaves `AccessRefusal` alone. The raw-SQL tests prove it. **The real work is in the pipeline:**

- the request's own unit of work destroys the Space that its presence row, its idempotency record and every concurrent request hang off;
- a slice of client work on top: the confirm, the counts, the route afterwards;
- five honest closures of ledger entries that only become reachable once a Space can disappear.

Get Q1 and Q4 right first. They are where a naive slice ships a `500`.

### What exists today (survey at `5e04528`)

**Server**

- **Pipeline order** (`Yello.Host/Spaces/ActiveSpaceResolution.cs:210-270`, `ServeAsync`):
  1. presence `TryEnterAsync`, which inserts a `SpacePresence` row (FK → Space, cascade);
  2. `BeginIdempotentAsync`, which looks up a Space-scoped `IdempotencyRecord`;
  3. the creation bound;
  4. the endpoint;
  5. `RecordIdempotentAsync`, which **inserts** an `IdempotencyRecord` (FK → Space) when a key was sent;
  6. `CompleteAsync`.
  - **Step 5 fails with 547 after a deletion.** The client always sends a key.
- **Resolution**: `MembershipActiveSpaceResolver` opens a transaction, sets `SpaceId` read-only (`SpaceSessionContext.SetAsync`), narrows the EF scope, and reads the Membership with no lock hint. `null` gives the boundary 404 and a `CrossSpace` record.
- **Routes**: `SpaceRoutes.Active` = `/api/v1/spaces/{spaceId:guid}`. `GET` is `MapActiveSpace` (`ReadProjectsAndTasks`). `PATCH` is `MapRenameSpace` (`RenameSpace`). Both are in `Yello.Host/Spaces/ActiveSpaceEndpoints.cs`, mapped at `Program.cs:378-390`.
- **`SpaceStore`** (`Yello.Infrastructure/Persistence/SpaceStore.cs`): `AddAsync` (creation) and `RenameAsync`. `RenameAsync` runs `ExecuteUpdateAsync` and discards the count, which is the `:666` bug.
- **Delete precedent** (2.5):
  - `ProjectEndpoints.MapDeleteProject` answers `204`, or `BoundaryProblems.NotAvailable()` **without** recording.
  - `ProjectStore.DeleteAsync` takes the app lock, then `UPDLOCK, HOLDLOCK` existence read, then the range lock, then one `ExecuteDeleteAsync`.
  - `DeleteProjectHandler` returns `ProjectDeletion`.
- **FKs** (`Persistence/Configurations/*`):
  - Cascade from Space: Membership, Project (→ Task → TaskLabel), StatusDefinition, Label, SpacePresence, IdempotencyRecord.
  - ApiToken → Membership (alternate key `(BoundSpaceId, AccountId)`) is Cascade.
  - **NoAction:** `Task → StatusDefinition` (`TaskConfiguration.cs:89` area) and `TaskLabel → Label`. SQL Server checks NO ACTION at the end of the statement, after the cascades, so one `DELETE` of the Space should succeed. **Prove it with a populated Space** (AC10).
  - `AccessRefusal.TargetSpaceId` has no FK and no RLS.
  - `Membership → Account` is Restrict, which is irrelevant here.
- **RLS**: FILTER plus BLOCK AFTER INSERT/UPDATE predicates on `fn_SpaceIsolationPredicate`. Membership's filter is `fn_MembershipVisibilityPredicate` (SpaceId **or** AccountId). There are **no BEFORE/AFTER DELETE block predicates** anywhere (`IsolationCases.cs:644` says so). 2.5 Q6 deferred "reconsider a delete block predicate when a whole Space cascades" to here. **Recommendation:** add none. The delete filters on the resolved id, under that Space's key, in `SpaceStore` alone, and a gate holds it. Record this in Completion Notes.
- **Tokens** (`ApiTokenAuthenticationHandler` → `ApiTokenStore.FindAsync`, an indexed read on `SecretHash` with no session context): no row gives `NoResult`, which gives `401 authentication-failed`. No extra code is needed once the Space row goes in the same transaction.
- **Sessions**: the cookie carries only `account-id`, and nothing per Space. Deleting a Space touches no Session. `SessionsValidAfter` is not involved.
- **Gates touching `Space`**:
  - `RefusalBoundaryGateTests.Spaces.cs` `The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space`: any `ExecuteUpdate`/`ExecuteDelete` on `Space` must come from `SpaceStore`.
  - `RefusalBoundaryGateTests.cs:411` `Space_is_read_only_where_named`.
  - `SpaceSwitcherGateTests.No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys` and `Exactly_one_creation_unit_of_work_sets_both_session_context_keys`.
  - `SpaceResolutionSeamTests.Exactly_one_production_line_sets_a_session_context_key`.

**Client**

- **`Pages/SpaceSettings.razor`** (`/spaces/{id}/settings`): renders `SpaceNameSection` and `LabelSection` inside `SpaceArrival`. Its visibility is `SpaceCapabilities.PermitsSpaceSettings`, the union of the section rules (`Spaces/SpaceCapabilities.cs`), held by a gate.
- **`Components/SpaceNameSection.razor`**: the rename form. A fresh field name per page, `autocomplete="off"`. On 200 it calls `ActiveSpace.Renamed` and `Directory.Renamed`. On 404 it calls `OnSpaceRefused`.
- **`Components/ProjectRow.razor` and `ProjectList.razor`** (2.5): the inline confirm.
  - Content: `DeleteQuestion`, then the count sentence (`ProjectHoldsTasks` and its variants), then `DeleteCannotBeUndone`, then `ProjectDelete` and `DeleteCancel`.
  - Focus: Cancel on open; Esc restores and focuses Delete.
  - `aria-busy` while sending, with nothing disabled. A per-object idempotency key is reused after an unanswered attempt.
  - The count is re-read before the confirm opens.
  - **Note the order here:** 2.5 puts the count before "This cannot be undone."; AC8 wants "This cannot be undone." with no reassurance before it. A count is fact, not reassurance, so either order passes AC8. Q5 picks one.
- **`Spaces/SpaceWrites.cs`**: `SendAsync` and `SendWithoutBodyAsync` add `Idempotency-Key` and the anti-forgery token, and resend once on `antiforgery-token-missing`.
- **`Spaces/SpaceDirectory.cs`** (Account-scoped): `Current`, `RefreshAsync`, `Lists`, `Renamed`, `Refused`, `ArrivedIn`, `RefusedInThisLoad` and `Forget`. **There is no `Removed(id)` yet.**
- **`Spaces/ActiveSpaceState.cs`**: `Navigating` purges on a change of Space (cancel in flight, clear, run registrations, raise `Changed`) before the router renders. Its remarks list every holder.
- **`Pages/Home.razor`**:
  - refresh, then empty gives the `_noSpace` panel (`NoSpaceHeading` "No Spaces yet", `NoSpaceBody` "You do not belong to any Space yet.", and a `CreateSpaceAction` link to `/spaces/new`, with focus on the heading);
  - `null` gives `_unavailable`;
  - otherwise it goes to the first listed Space not refused in this load, and gives `_unavailable` if every one has refused.
- **`Components/RefusalSurface.razor`**: `<h1>` from `RefusalCopy.For(Problem, ...)` and `<a href="@ClientRoutes.Home">` "Back to Yello." (`RefusalRouteBack`). On `/` this is a same-URL link that does not re-run Home (`:679`).
- **`Components/ContextBar.razor` `ToggleAsync`** sets `_spaces = Directory.Current ?? []`, then refreshes. An empty or failed list gets no sentence (`:356`).
- **Copy**: `Localisation/ClientCopy.resx`. These exist: `DeleteQuestion` "Delete {0}?", `DeleteCannotBeUndone` "This cannot be undone.", `DeleteCancel` "Cancel", `delete-space` "Only the Owner can delete the Space.", `NotAvailable`, `RefusalRouteBack`, `NoSpaceHeading`, `NoSpaceBody`, `CreateSpaceAction`.

**Tests**

- **Slices**: `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests*.cs` is one partial container class. Add `ActiveSpaceEndpointTests.DeleteSpace.cs` (and `.DeleteSpaceRace.cs`) as **partials. No new container class.** Patterns to copy:
  - `Projects/ProjectEndpointTests.Delete.cs`, `.DeleteIdempotency.cs` and `.DeleteRace.cs`;
  - `Tasks/TaskEndpointTests.Delete*.cs`;
  - `ActiveSpaceEndpointTests.Tokens.cs` `Deleting_the_Space_invalidates_its_Tokens` (`:421`);
  - `WireDatabase.DeleteSpaceAsync` (`tests/Yello.Tests.Shared/WireDatabase.cs:370`).
- **Isolation**: `IsolationCases.cs`. No row is pending today.
- **Revocation**: `RevocationCases.cs:54-60` `REV-SPACE-DELETED`.
- **Client**:
  - `SpaceCreateAndRenameTests.cs`, `SpaceSwitchingTests.cs`, `SpaceSwitchCarryOverTests.cs`;
  - `DeleteTests.cs` (2.5's confirm cases, the model to follow);
  - `RefusalSurfaceTests.cs`;
  - the `FakeYelloApi.cs` harness: `ArrivalRefusals` and the hold helpers.
- **Architecture**: `RefusalBoundaryGateTests(.Spaces/.Delete).cs`, `SpaceSwitcherGateTests*.cs`, `ActiveSpaceStateTests.cs`, `SpaceResolutionSeamTests.cs`.

### What must be preserved (regression guard)

- **Only one unit of work sets both keys:** `SpaceCreationScope`. The deletion sets `SpaceId` only.
- **The boundary 404 stays byte-identical and uniform.** No new `type` for "deleted". A deleted Space is indistinguishable from one that never existed (FR-15, AD-1).
- **Refusal records are written only by the pipeline.** The endpoint's own `NotFound` answer is not recorded (the 2.5 precedent).
- **`ApiNoStore`** puts `no-store` on every `/api` answer, the `204` included.
- **3.2's refused set only excludes.** `Removed(id)` must not turn it into a destination. `No_client_code_remembers_a_last_Space` stays green with its regex unwidened.
- **`SpaceDirectory` survives a Space switch.** `Removed(id)` edits it; it does not `Forget()` it.
- **The Space bound is a live `COUNT`** (3.1). Deleting a Space frees a slot with no counter to maintain.
- **The rename flow** (`SpaceNameSection`) and its 3.2 Tab-order cases.
- **The no-Space state** (3.1 AC15) and its focus on the heading.

### Open questions (decide in Task 1; each is reversible; record as "Qn - (x)")

1. **The `Idempotency-Key` on a request that deletes its own Space** (AC9, AC12; AD-18). The record has an FK to Space, and the pipeline inserts it after the endpoint, so as built the request fails with 547 and answers `500`. Even without the FK, the cascade would take the record, so a replay could never find it.
   - (a) **Recommended.**
     - Mark the route as one whose effect destroys its own idempotency scope. For example, a metadata marker `DestroysItsSpace()`, beside `CarriesSecretInResponse`.
     - For that route, the pipeline still validates the key (malformed gives `400`) and still looks it up, but **records nothing**.
     - A retry after a lost answer re-enters the pipeline. If the first attempt committed, the retry finds no Membership and gets the boundary `404`, so nothing is applied twice. If the first attempt did not commit, the retry deletes. Either way NFR-5 "a retried write does not apply twice" holds.
     - The client treats `404` on its own delete as "gone" (AC12).
     - Record the variance under AD-18 (Task 2): this route's replay answers the boundary `404` rather than the original `204`, because the original answer's scope no longer exists.
     - Cost: one marker and one branch, and a deviation from AD-18's letter that is recorded.
   - (b) **[LEE — relaxes the AD-24 gate.]** Record the key in the Account-scoped `AccountIdempotencyRecord`, which needs **both** session keys in the deletion's unit of work. This would be a second both-keys caller. `No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys` would have to be widened, and AD-24 amended again. **Do not take it without Lee.**
   - (c) Refuse an `Idempotency-Key` on this route. Rejected: the client sends one on every write, and AD-18 says every state-changing endpoint accepts it.
2. **Where the confirm's counts come from** (AC2).
   - (a) **Recommended.** A new Owner-only read, `GET /api/v1/spaces/{spaceId}/deletion` (name it in `SpaceRoutes`), returning `SpaceDeletionSummaryResponse(int Projects, int Tasks, int OtherMembers)`.
     - It runs as one Space-scoped unit of work with live `COUNT`s, under `RequiresCapability(Capability.DeleteSpace)`.
     - It gets isolation rows on both surfaces (Task 7).
     - `OtherMembers` lets the copy say how many other people lose access, as the mockup does (`mockups/space-settings.html:183-213`). If you would rather not count Memberships here, return only Projects and Tasks and use a fixed sentence for other Accounts.
     - Cost: one route, one record and its rows.
   - (b) Sum `ProjectResponse.TaskCount` over the Space home's Project list on the client. Rejected unless that list is provably complete and unpaged: a wrong count at the point of the most destructive action breaks §6.2.
3. **Does the server check the typed name?** (AC3).
   - (a) **Recommended.** No. `DELETE` carries no body. The typed name is a UI guard against accident, as `EXPERIENCE.md:176` and `:269` frame it. A script holding an Owner's Token is already authorised by FR-7 and AD-4. Cost: none.
   - (b) Require `{ "confirmName": "..." }`. Rejected: it is a contract change (AD-19) that no document asks for, and it puts the Space name on the wire on every delete.
4. **Writes and reads that lose to a concurrent deletion** (AC13).
   - (a) **Recommended.**
     - In `ActiveSpaceResolution`, a unit of work that fails with a `DbUpdateException`/`SqlException` whose number is **547** is checked once: does the resolved Space still exist? Use a plain `Space` existence read, since `Space` has no RLS, on a fresh context or after rollback.
     - If the Space is gone, the answer is the uniform boundary `404` with a `CrossSpace` record, consistent with the next request's answer. Otherwise rethrow.
     - Also have `SpaceStore.DeleteAsync` take `ApplicationLocks.TakeProjectsPerSpaceAsync` before its delete, so a Project create in flight serialises ahead of it rather than failing.
     - Prove it with deterministic races. Hold the delete's transaction open (or hold the writer at its lock) and wait on `LCK_M_%`, for: a Project create, a Task create, a rename (`:666`), and a `GET` whose name read follows resolution (`:360`).
     - Cost: one catch in the pipeline and four race cases.
   - (b) Let them answer `500`. Rejected: it is the `:360` bug generalised.
5. **The confirm's copy and focus** (AC2, AC3, AC8, AC16). Recommended (UX to confirm; ledger to UX):
   - Heading `DeleteQuestion` ("Delete {name}?").
   - Then `DeleteCannotBeUndone` ("This cannot be undone.").
   - Then "It holds {p} Projects and {t} Tasks.", with singular and zero forms as in `ProjectHoldsTasks`.
   - Then "{n} other people will lose access to it." (zero form: "No one else has access to it.").
   - Field label: "Type the Space name to confirm".
   - Button: "Delete this Space".
   - Mismatch message: "Type the Space name exactly as shown."
   - Match rule: ordinal equality after trimming both ends, matching `SpaceName.TryNormalise`'s trim, and case-sensitive.
   - Focus on open: the typed-name field. 2.5 focuses Cancel, but here typing is the first required act. Esc and Cancel restore and focus "Delete this Space".
   - Never use the word "archive".
   - **Alternative (b):** focus Cancel, as 2.5 does.
6. **The announcement after deletion** (AC12; UX-DR38).
   - (a) **Recommended.** "Space deleted." with no name. It is said by the destination after the navigation, because the confirm unmounts. Use the narrated-then-routed mechanism that already exists, so it is said once on Home's destination (the next Space's home, or the no-Space state). The 3.2 Space purge must not swallow it: hand it over after the purge, or carry it in an Account-scoped holder that Home takes.
   - (b) No announcement; focus on the destination heading only. Cheaper, but the outcome of the most destructive action then goes unannounced.
7. **The switcher with nothing to list** (AC14; `:356`).
   - (a) **Recommended.**
     - Empty answer: `NoSpaceBody` ("You do not belong to any Space yet.") in a `<p>` inside the panel, outside the `<ul>`, with the foot "Create a Space" link kept.
     - Failed refresh with nothing held: "Your Spaces could not be listed." in the same position.
     - Failed refresh with a held list: keep showing the held list, unchanged from today.
     - Cost: two branches and one string.
   - (b) Hide the switcher trigger when the Account has no Space. Rejected: the bar would change shape, and "Create a Space" would lose its second door.
8. **The all-refused page at `/`** (AC15; `:679`).
   - (a) **Recommended.** `RefusalSurface` gains a parameter (for example `OfferRouteBack`, default `true`). On `/`, Home passes `false` and the surface shows "Choose a Space from the switcher above." instead of the link. The heading stays "Not available." Cost: one parameter, one string, and existing fingerprints unchanged elsewhere.
   - (b) Carry the last refusal's own problem into the heading (a `409` keeps its words). Rejected for now: `SpaceDirectory` would hold per-Space refusal reasons, which edges toward remembering, and it mixes refusal families on one page.
9. **Assertion of "no Space is undeletable"** (AC6). Recommended: a Slices case deletes a Personal Space (registration's) that is the Account's only Space, then asserts the Account row persists, `GET /api/v1/spaces` answers `200 []`, and a new Space can then be created. This also makes 5.4's precondition (owning no Space) reachable. Tag it `Assumption=PRD-12-2`.

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. Invitations.** FR-7 and the epic say "destroyed". FR-10/FR-12 (`prd.md:269`) and AD-27 (`:247`) keep Invitation rows for SM-4. No table exists yet. Ledger to **4.1** (owner Lee/PM): decide the FK behaviour on Space delete (cascade, or keep with a null Space) before the table is created.
- **C2. Ownership Offers.** AD-5 lapses offers on Membership removal and Account deletion, and is silent on Space deletion. Ledger to **5.1**.
- **C3. The ERD** shows `SPACE ||--o{ ACCESS_REFUSAL`. NFR-7's 90-day retention wins: there is no FK and there is no cascade. Keep it.
- **C4. The mockup** shows a dialog over a scrim. `EXPERIENCE.md:67` (in place) wins (`EXPERIENCE.md:73`: spines win).
- **C5. FR-4** (`prd.md:180`) says an Account "never encounters a state in which it belongs to no Space". That holds at registration only; FR-7 makes the state reachable. This is a wording tension only.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:370` → **CLOSED by 3.3**, naming the slice and the V-3 endpoint case.
- `:382` → note: the endpoint-driven Space-deletion row has been added. 4.5 and 5.4 still own theirs.
- `:360`, `:666` → **CLOSED by 3.3** with Q4, naming the race cases.
- `:356` → **CLOSED by 3.3** with Q7, naming the browser cases.
- `:679` → **CLOSED by 3.3** with Q8, naming the browser case.
- **New entries:**
  - C1 → owner 4.1 (Lee/PM), severity medium.
  - C2 → owner 5.1, severity low.
  - Live-session termination on Space deletion (V-11) and the `MembershipChanged` publish per affected Account (AD-9) → owner **7.7**, severity medium. Check first whether an Epic 7 entry already says this.
  - The Q5 copy and focus choice → owner **UX**, severity low.
  - Q1's AD-18 variance → recorded in the spine, so no ledger entry is needed unless it is deferred.
- Give any other deferral an owner and a severity.

### Architecture compliance (must)

- **Mutation lives in an Application slice, in one transaction** (`ARCHITECTURE-SPINE.md:273-282`). Domain stays untouched. Keep to the ring rule (`AllowedReferenceEdges.cs`).
- **The Space comes only from the route** (AD-3). The Role is read per request (AD-1). The endpoint does not authorise. The pipeline does, through `RequiresCapability`.
- **Never** disable RLS, open a second connection to dodge the policy, or set the `AccountId` key in the deletion (AD-24). Q4's existence check reads `Space`, which has no policy. Do it on the request's own context after rollback, or on a fresh unit of work that sets no key. Record which, and make sure `Exactly_one_production_line_sets_a_session_context_key` stays green.
- **Logs**: `SpaceId` only (I-17).
- **CSS**: add only to `components.css`. Use logical properties and rem/em, with no px literal and no colour literal (`DesignFoundationGateTests*`). No animation on the destructive path.
- **Copy**: only in `ClientCopy.resx`. No server prose is templated into the UI.

### Library / framework

No new package. Use .NET 10.0.11, EF Core 10.0.11 (`ExecuteDeleteAsync`), Microsoft.Data.SqlClient 7.0.2 (`SqlException.Number == 547`; look through `DbUpdateException.InnerException` and EF's retry wrapper, as `DatabaseAvailability` already does for `DbException`), Blazor WebAssembly 10.0.11, xunit.v3 4.0.0, Testcontainers 4.6.0, Microsoft.Playwright 1.62.0 and ArchUnitNET 0.13.3. There is no bUnit and no axe-core; adding either needs Lee.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:

- Use only these traits: `Suite`, `Priority`, `Requirement` (cite FR-7, AD-6 and so on), `Assumption` (`PRD-12-2` on irreversibility) and `Tier`.
- Name each test as an underscore sentence, one behaviour per test. Never use `Task.Delay`.
- Races hold a lock and wait on `LCK_M_%`.
- **Every absence assertion has an anchor and is first proved against a plant.**
- Randomise data. Never use a shared email literal. Clean up by container or rollback, never by DELETE.
- Never run two `dotnet test` processes at once. Iterate with `--project ... --filter`.

**Slices** (`ActiveSpaceEndpointTests.DeleteSpace.cs` and `.DeleteSpaceRace.cs`, partials, **no new container class**):

- **The Owner deletes a populated Space; `204`** (AC1, AC4, AC10). Seed it with Projects, Tasks on every Status, applied Labels, 3 Memberships, Tokens for 2 Accounts and a presence row. Afterwards, every child table has 0 rows for that SpaceId, and the `AccessRefusal` rows for it remain. Anchor: the counts before.
- **Admin, Member and Viewer each get `403 insufficient-role`**, the Space is intact, and an `InsufficientRole` record is written. A non-member gets the boundary `404`, byte-identical, with a `CrossSpace` record (AC9).
- **Tokens** (AC11): a Token bound to the deleted Space gets `401` on the next request; the Owner's Token for another Space gets `200`. The deletion and the Token invalidation are one transaction: under a forced failure before commit, the Token still works and the Space is intact (S-1).
- **Other Members** (AC5): Member B's switcher no longer lists it; B's own Space and B's other Memberships are intact; B's Session gets `404` on the deleted Space.
- **Last Space** (AC6, Q9).
- **The bound**: an Account at 50 deletes one and can create one (the live count). P2.
- **Idempotency** (Q1): delete with key K gives `204`. Replay K gives the boundary `404`, with nothing applied and no `500`. A malformed key gives `400`.
- **Counts read** (Q2): the counts are correct, Owner-only, and include no name.
- **Races** (Q4, AC13): rename vs delete gives `404`; a name read vs delete gives `404`; Project create vs delete gives `404` or serialises; Task create vs delete gives `404`.
- **Logs**: no Space name in the captured log of any case (I-17).

**Isolation and Revocation**: Task 7's rows, on both surfaces.

**Architecture**:

- the amended `The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space` (plant: an `ExecuteDeleteAsync` on `Spaces` in another type gives red);
- both-keys gates unchanged and green;
- a gate that the deletion route carries Q1's marker, and that no other route does (plant: remove it);
- `PermitsSpaceSettings` union and permit-phrase gates.

**Browser** (`tests/Yello.Tests.Client`, `ClientSurfaceCollection`, `FakeYelloApi`; add `SpaceDeleteTests.cs` in the same collection; extend `FakeYelloApi` with the `DELETE` and counts answers and a hold):

- **Owner sees the section; Admin sees no section and no button** (AC1). Anchor: the Admin sees the rename section. Plant: render the section for Admin, which must give red.
- **The confirm replaces the panel's content** (AC7): while it is open, there is no rename field and no Label section in the DOM, there is no `dialog` or scrim, and the content returns on Cancel or Esc.
- **The content** (AC2, AC8): heading "Delete {name}?"; "This cannot be undone." as the next text with nothing before it; the counts as served; the other-Accounts sentence.
- **The field** (AC3): `autocomplete="off"`; `name` differs between two openings; mismatch sends **no** request (assert the request log) and shows the message; Delete is never `disabled`.
- **The outcomes** (AC12):
  - **While held:** the confirm is still shown, `aria-busy`, and nothing optimistic.
  - **`204` with Spaces remaining:** the path goes to the next listed Space's home; the switcher no longer lists the deleted one; "Space deleted." is said once with no name; nothing of the deleted Space is in the DOM.
  - **`204` on the last Space:** the no-Space state, with focus on its heading and "Create a Space" present.
  - **`404`:** the same route as `204`.
  - **`5xx` or no answer:** the confirm stays open, and the retry carries the **same** `Idempotency-Key` (assert the header).
- **The switcher states** (AC14): empty gives the sentence plus the foot link; failed with nothing held gives the other sentence; neither puts a `<li>` in the `<ul>`.
- **All-refused at `/`** (AC15): no link whose `href` resolves to `/`, and the switcher sentence is present; existing `RefusalFingerprint` cases stay green.
- **Keyboard and reflow** (AC16): keyboard-only open, type, delete; Esc restores and focuses "Delete this Space"; Tab order and a visible ring; run at 1280×800 and 320×256.
- **Storage**: after a deletion, `localStorage`, `sessionStorage` and IndexedDB are empty (extend `No_Space_scoped_content_reaches_web_storage` if cheap).

**Plants** (each restored from memory in a `finally`; rebuild `Yello.Client` in Debug before each browser plant; record them in a table in the Debug Log):

- Pipeline records the idempotency key on the deletion route → the Slices idempotency case goes red (`500`).
- `RenameAsync` row count ignored → the rename race case.
- `SingleAsync` restored in `ActiveSpaceNameReader` → the name-read race.
- The 547 mapping removed → the create-vs-delete race.
- Delete section rendered for Admin → the AC1 browser case.
- The typed-name check skipped → the mismatch case (a request is sent).
- `Removed(id)` skipped → the switcher still lists the deleted Space.
- `OfferRouteBack` ignored → the AC15 case.
- A `ExecuteDeleteAsync` on `Spaces` planted outside `SpaceStore` → the gate.

**Run** (CLAUDE.md):

1. Check Docker with `docker info --format '{{.ServerVersion}}'`.
2. `dotnet build Yello.slnx` (0/0).
3. `dotnet build-server shutdown`.
4. `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`.

Rerun once before calling a failure real. Run timing rows alone. If Client cases fail only in the full run, run that project alone before investigating.

### Previous story intelligence (3.2, 3.1, 2.5)

- **3.2:**
  - Only a `403`, `404` or `409` on arrival marks a Space refused, behind a token check. Never a transport failure or a `5xx`. `Removed(id)` must also clear the id from the refused set.
  - The code review found absence assertions never seen red. Plant every absence in this story.
  - The full run once failed a timing row (`ISO-TIMING-IN-SPACE`) under load. It passed alone.
  - Q4 measured that scroll is at the origin after a switch. Nothing is needed here.
- **3.1:**
  - The AD-24 amendment is additive and dated. Gate messages must cite it.
  - The review found AC clauses with no test. **Map every AC to a named test** in Completion Notes.
  - A browser plant that does not compile proves nothing.
  - An unreadable `201` once dropped the create key (f3139a1). Make the delete key survive every non-answer path.
- **2.5:**
  - The inline confirm, the reused key, the re-read count and `DeleteWire`'s race pattern.
  - Q6 deferred a delete block predicate to 3.3 (see Dev Notes, RLS).
  - "A deletion races every writer on its rows, from both sides."
- **1.8:** the Token cascade was designed for this story; `:370` names exactly what to do.

### Git intelligence

Recent commits:

- `5e04528` (3.2 review fixes)
- `2bbcdec` (3.2 implementation)
- `162a2c0` (3.2 ATDD scaffolds)
- `4837bfe` (3.2 story)
- `f3139a1` (3.1 review fixes)

The pattern is story, then ATDD scaffolds (`[Fact(Skip = "red: ACn - ...")]` plus an `atdd-checklist-<key>.md`), then implementation, then review fixes. The orchestrator commits each one; do not commit. 3.2 touched `SpaceArrival.razor`, `Home.razor`, `SpaceDirectory.cs`, `ContextBar.razor`, `SpaceSwitcher.razor`, `ActiveSpaceState.cs` and `ApiNoStore.cs`, which are the files 3.3 touches on the client side. Read their current state before editing.

### Project Structure Notes

- New:
  - `Yello.Application/Spaces/DeleteSpace/DeleteSpaceHandler.cs` (+ `SpaceDeletion`)
  - Q2's handler/reader and `Yello.Contracts/Spaces/SpaceDeletionSummaryResponse.cs`
  - `Yello.Client/Components/SpaceDeleteSection.razor`
  - `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteSpace.cs` and `.DeleteSpaceRace.cs`
  - `tests/Yello.Tests.Client/SpaceDeleteTests.cs`
- Updated:
  - `Yello.Application/Spaces/ISpaceStore.cs`, `RenameSpace/RenameSpaceHandler.cs`
  - `Yello.Infrastructure/Persistence/SpaceStore.cs`, `ActiveSpaceNameReader.cs`
  - `Yello.Host/Spaces/ActiveSpaceEndpoints.cs`, `ActiveSpaceResolution*.cs`, `Program.cs`, `SpaceLog`
  - `Yello.Contracts/Spaces/SpaceRoutes.cs`
  - `Yello.Client/Pages/SpaceSettings.razor`, `Home.razor`
  - `Components/RefusalSurface.razor`, `ContextBar.razor`, `SpaceSwitcher.razor`
  - `Spaces/SpaceDirectory.cs`, `SpaceCapabilities.cs`
  - `Localisation/ClientCopy.resx`
  - `wwwroot/css/components.css` (only if needed)
  - `tests/Yello.Tests.Isolation/IsolationCases.cs`, `tests/Yello.Tests.Revocation/RevocationCases.cs`
  - `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Spaces.cs` (+ any Q1 marker gate)
  - `tests/Yello.Tests.Client/FakeYelloApi.cs`
  - `ARCHITECTURE-SPINE.md` (AD-24 and AD-18 lines), `deferred-work.md`
- **No migration** is expected. The cascades exist. If one turns out to be needed (for example, a NoAction FK blocks the statement), it is expand-only with an exact `Down`, and the reason is recorded.
- No new project, package or container-backed class.

### References

- `_bmad-output/planning-artifacts/epics.md`: `:179` (§6.2), `:204` (AR-16), `:250-285` (UX-DR12 to UX-DR41), `:367` (coverage), `:1430-1611` (Epic 3)
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md`:
  - `:60`, `:150-160`, `:180`, `:194-211` (FR-6, FR-7), `:249-250`, `:269`
  - `:323-324`, `:351-353` (role matrix), `:615` (FR-36), `:683-734` (NFRs)
  - `:769-774` (§6.2), `:807`, `:893` (§12 #2)
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`:
  - `:75` (AD-1), `:81-86` (AD-2), `:92` (AD-3), `:100` (AD-4), `:106` (AD-5), `:112` (AD-6)
  - `:130` (AD-9), `:186` (AD-18), `:198` (AD-20), `:218-228` (AD-24 + amendment)
  - `:247` (AD-27), `:273-282`, `:361-383` (ERD)
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md`: `:53`, `:67`, `:73`, `:99-122`, `:130-147`, `:176`, `:244`, `:253`, `:269`, `:279`, `:335`, `:403`, `:458`. `DESIGN.md:277-284`, `:476`, `:519`. `mockups/space-settings.html:178-213`
- `_bmad-output/specs/spec-yello/SPEC.md:59-61` (CAP-7); `acceptance-criteria.md:161-162`
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md:388-391`, `:486`, `:549`, `:1240`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-13.md` has no 3.3 content (FR-1 display name only).
- `_bmad-output/test-artifacts/test-design-qa.md`: `:282` (V-3), `:351` (V-11), P-10, I-1/I-2/I-5/I-6/I-13/I-17, S-1, `:587-588`; `test-design-architecture.md:396`, `:545-552` (R10)
- `_bmad-output/implementation-artifacts/deferred-work.md:356`, `:360`, `:370`, `:382`, `:666`, `:679`; story files `3-1-…md`, `3-2-…md`, `2-5-…md` (Q6), `1-8-…md`
- `tests/TESTING-CONVENTIONS.md`

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-10-31).

### File List
