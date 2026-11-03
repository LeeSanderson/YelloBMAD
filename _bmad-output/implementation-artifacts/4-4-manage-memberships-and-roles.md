---
baseline_commit: 885ac9eca02ec45c021eed575ac42a2868586d6e
---

# Story 4.4: Manage Memberships and Roles

Status: review

Epic: 4 — Bring people into one piece of your work (the fourth story of the epic)
Story key: `4-4-manage-memberships-and-roles`

**Requirements owned.** From the Story Coverage Index (`epics.md:372`): **FR-13**, AR-12, AR-16, UX-DR14, UX-DR34, NFR-2. §6.1 also lists 4.4 as an owner (`epics.md:178`).

- **FR-13** (`prd.md:293-301`; `acceptance-criteria.md` CAP-13). Every consequence binds:
  - An Admin can change Memberships between Member and Viewer only.
  - Only an Owner can promote a Membership to Admin, or demote one from Admin. This is PRD §12 assumption #4 (`prd.md:895`), and it is still unconfirmed.
  - No Role change can produce a second Owner or remove the sole Owner. Ownership moves only by an Ownership Offer (FR-42, story 5.2).
  - "Takes effect on the target's active Sessions without requiring them to re-authenticate." The **request-path** half is fully met in Epic 4. The live-session half completes in 7.7 and 7.8 (`epics.md:402`; `sprint-status.yaml` header: FR-13 is staged).
- **The matrix** (`prd.md:333-349`; `role-capability-matrix.md`). "Change a Membership's Role": Owner ✔, Admin "Member ↔ Viewer only", Member —, Viewer —. The code transcribes it as `Capability.ChangeMembershipRole` = Allowed / **Qualified** / Denied / Denied (`Yello.Domain/Memberships/RoleCapabilityMatrix.cs:47`).
- **AR-12 / AD-5** (`epics.md:200`; `ARCHITECTURE-SPINE.md:103-107`). A filtered unique index on `Membership(SpaceId) WHERE Role = Owner` exists: `UX_Membership_SpaceId_Owner`, filter `[Role] = N'Owner'` (`SchemaNames.cs:217`; migration `20260828164716_InitialSchema.cs:171-175`). "Invariant tests assert no Space ever holds zero or two Owner Memberships" (S-2, `test-design-qa.md:319`).
- **AR-16 / AD-9** (`epics.md:204`; `ARCHITECTURE-SPINE.md:127-131`). "Any operation mutating a Membership publishes `MembershipChanged(AccountId, SpaceId)` at its transaction boundary, delivered in-process ... One publish per affected Account." **No publisher exists today** (Q7).
- **UX-DR14** (`epics.md:252`; `EXPERIENCE.md:213`): the `membership-list`. It shows every Membership with its Role. Email addresses are visible here and nowhere else. An Admin's missing controls are absent, not disabled. The list pages at the 100-Membership bound. (The Owner-row remove clause is 4.5's.)
- **UX-DR34** (`epics.md:278`; `EXPERIENCE.md:151-156`): email confinement, a Membership-scoped discriminator, and attribution from a captured name.
- **NFR-2** (`prd.md:692-700`): "On the request path — reflected on the very next request. No tolerance ... on the browser and the API alike." Test-design row **V-1** (`test-design-qa.md:280`): "Role change governs the very next request — 5 capability classes, both surfaces".
- **UX-DR11** (Role picker narrowing, "never a default selection where the choice is consequential"), **UX-DR27** (absent, not disabled), **UX-DR29**/**UX-DR39** (the Space settings narrated exit, which already exists from 3.1). The pushed narration, the Role chip update and the per-tab filter are **4.7's**.

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Severity | Covered by |
|---|---|---|---|
| `:329` | Qualified Admin cells pass the Role gate, and nothing enforces the target constraint ("4.4 and 4.5 must add a pipeline-owned seam") | medium | Q1 **[LEE]**, AC4, AC9 |
| `:540` | Space settings holds only Labels; 4.4 adds Roles and Memberships | low | AC1, AC10 |
| `:546` | Deleting a Label that is already gone raises the Space-level refusal surface ("4.4 decides alongside concurrent Admin editing") | low | Q16, AC10 |
| `:550` | The "narrated, then routed" sentence is inserted into Space home already filled; "4.4 renders the region empty and fills it after the first render" | low | Q15, AC10 |
| `:758` | Re-run 4.2 AC3's **demotion** arm through the product's own route (4.5 owns the removal arm) | low | AC8 |
| `:762` | Focus drops to `body` while a destructive row action is in flight (`InvitationRow`, `LabelRow`, `ApiTokenRow`) | low | Q14, AC10 |
| `:769` | Acceptance publishes no `MembershipChanged` (owner 7.7) | low | Q18. Update the entry only; the owner stays 7.7 for subscribers |

**Depends on** (all done):

- 1.5 and 1.6: the pipeline, `RoleCapabilityMatrix`, `CapabilityDecision.Qualified`, `RequiresCapability`, refusal recording, idempotency and `NotAvailableFloor`.
- 1.7: `ActiveSpaceState`, `RoleChip`, `SpaceArrival`'s per-arrival Role re-read.
- 1.9: the isolation and revocation suites (`REV-ROLE-CHANGED` uses `WireDatabase.ChangeRoleAsync`).
- 2.4: `LabelSection`/`LabelRow`.
- 3.1: Space settings, `SpaceCapabilities.PermitsSpaceSettings`, the narrated exit.
- 4.1 to 4.3: `InvitationSection`/`InvitationRow` (the row-confirm pattern), the Role picker radio group, `IssuerName` captured at issue, and Memberships created through acceptance.

**Explicitly not in this story:**

- **Removing a Membership, or leaving a Space** (4.5): the remove control, the confirm with live-session state, Token invalidation and unassignment. 4.4's list leaves room for the remove control. It renders none.
- **Assignee** (4.6). **Presence** (7.5). **Ownership panel and offers** (5.1, 5.2).
- **The pushed narration of a Role drop, the Role chip updating under the person, and the per-tab filter** (4.7, UX-DR29/38). 4.4 keeps the existing request-path narrated exit and fixes ledger `:550`.
- **Lease invalidation and the live-session clause** (7.3, 7.7, 7.8). 4.4 builds the publish; nothing subscribes yet (Q7).
- **A display-name change feature.** None exists, and none is asked for.
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits.
- **No new NuGet package and no new project without Lee** (CLAUDE.md).

## Story

As an Owner or Admin,
I want to see who is in this Space and change what they can do,
So that standing matches the working relationship without anyone having to leave and rejoin.

## Acceptance Criteria

AC1 to AC7 are reproduced **verbatim** from `epics.md:1761-1794` (`status: final`) and numbered by position. AC8 to AC11 come from the spines, the ledger and a survey of the code; each cites its source. The indented notes are this story's implementation guidance. A note that depends on an open question names it. The **[LEE]** questions (Q1-Q5) must be decided before development starts.

**AC1 — the Membership list** (UX-DR14, NFR-8, AR-29)
**Given** Space settings
**When** the Membership list renders
**Then** it shows every Membership in the active Space with its Role, to Owners and Admins only
**And** it pages rather than growing unbounded at the 100-Membership bound

> - **Route** (Q9): `GET /api/v1/spaces/{spaceId:guid}/memberships`, with an optional `after` cursor. Add it as `MembershipRoutes.List` in a new `Yello.Contracts/Memberships/MembershipRoutes.cs`, with `MembershipsOf(spaceId, after)`.
> - **Capability** (Q4 [LEE]): `.RequiresCapability(Capability.ChangeMembershipRole)`. That is Owner Allowed, Admin Qualified, Member and Viewer Denied. It follows 4.2's precedent of putting an email-bearing list under the write capability, never under `ReadProjectsAndTasks`. A Member or Viewer gets the pipeline's `403`, recorded as `InsufficientRole`. A non-member gets the boundary `404`. **No new matrix row.**
> - **Response** (Q8, Q9): `MembershipPageResponse(int Total, IReadOnlyList<MembershipResponse> Memberships, string? NextAfter)`.
>   - `MembershipResponse(Guid Id, string DisplayName, string EmailAddress, string Role, bool IsYou)`.
>   - `Role` is the stable code (`owner`, `admin`, `member`, `viewer`).
>   - `IsYou` compares `Membership.AccountId` with the context's `AccountId`. Reading `AccountId` is not gated; only `Role` is.
>   - `Total` is the true count, a separate `COUNT` and never the page length. The client shows "{n} of 100" (mockup `space-settings.html:120`).
>   - Never return the Account id or the normalised address. A contract-shape gate holds the exact property list (the `InvitationResponse` gate's style, `RefusalBoundaryGateTests.InvitationRevoke.cs:80`).
> - **Paging** (Q8 (a)): keyset on `(CreatedAt, Id)` ascending. The page size is a named server constant of 50 (the `ListPage.Fifty` precedent), so 100 Memberships take two pages. The cursor is opaque (the `ListCursorText` style). The client **appends and never recycles** rows (AD-29; `Nothing_in_the_client_virtualises_a_list`). A "Show more" `button-secondary` loads the next page. There is no infinite scroll (UX-DR26).
> - **Where the Role is read:** the projection runs in Infrastructure (a new `MembershipStore`). `No_slice_or_endpoint_reads_the_Role_off_a_Membership_row` (`RefusalBoundaryGateTests.cs:633-651`) forbids `Membership.get_Role` in `Yello.Application.*` and `Yello.Host.*`, and an expression-tree projection counts as a read. The store returns the Role as its stable code inside a read model.
> - **The email and the name** are read from the policy-free `Account` (Q4, Q5), joined on `Membership.AccountId` under the resolved Space's key. RLS and the query filter narrow `Membership`; `Account` carries no predicate.
> - **Proof (Slices):**
>   - The Owner and an Admin each list all N Memberships across pages: union equals the seeded set, no row is repeated or skipped, and `Total` = N.
>   - At exactly 100, the first page has 50 rows and a cursor, and the second has 50 rows and none.
>   - A Member and a Viewer get `403`, recorded. A non-member gets `404`, byte-identical to an absent Space.
>   - Seed 100 Memberships with 4.3's `SeedSpaceHoldingMembershipsAsync` shape. Promote it to `SpaceSeeding` if a second file needs it.

**AC2 — email addresses here and nowhere else** (UX-DR34, §6.1, AD-23)
**Given** the Membership list
**When** it renders
**Then** email addresses are visible **here and nowhere else in the product** — not in the Assignee picker, not in Presence, not in an avatar tooltip, not in attribution, and not in any announcement

> - **Today** two response contracts carry an address: `InvitationResponse.EmailAddress` (4.2) and the new `MembershipResponse.EmailAddress`. Neither the Assignee picker nor Presence exists yet. Proof therefore rests on gates that bind the future (Q4 [LEE]):
>   - **Contract gate:** `Only_the_Membership_list_and_the_Invitation_list_carry_an_email_address`. Scan every public type in `Yello.Contracts` for a property whose name contains `Email` or `Address`, and fail on any type other than those two (and the request types that send an address *in*: registration, sign-in, issue). Detector oracle: a planted `EmailAddress` property on another response type turns it red.
>   - **Reader gate:** `Only_named_types_read_an_Accounts_email_address`. An IL scan for `Account.get_EmailAddress`/`get_NormalizedEmailAddress` admits the registration, credential and session stores, `InvitationStore` (the issue's already-a-member check and the accept's caller address) and the new `MembershipStore` list projection. Detector oracle required.
>   - **Client:** the address appears only inside the `MembershipSection` component. Leaving the Space purges the rows (`ActiveSpace.RegisterPurge`, as `InvitationSection` does; `InvitationListTests.cs:302,453` precedent). A role-change announcement names the **display name**, never the address (UX-DR38).
>   - **Logs:** `No_log_message_takes_an_address_a_token_or_an_email_body` stays green. The new log messages take ids only.
> - **Which address:** `Account.EmailAddress`, as typed at registration, never the normalised form (Q4).

**AC3 — identical display names are disambiguated without the address** (UX-DR34, `EXPERIENCE.md:155`)
**Given** two Memberships with identical display names
**When** they render anywhere outside the Membership list
**Then** they are disambiguated by a Membership-scoped discriminator and never by the email address
**And** the reason is stated: 20px monospace initials and a display name do not disambiguate two people, and the address is the field every implementer reaches for

> - **No surface outside the Membership list renders a Membership today.** The first ones are 4.6's Assignee picker and filter, then 7.5's Presence. So 4.4 builds the **rule and its one implementation**, and later stories consume it. Q13 decides the discriminator's form. Recommended: (a) a short code derived from the `MembershipId`, shown only when two display names in the same Space collide.
> - **Build:**
>   - A pure function, e.g. `Yello.Contracts/Memberships/MembershipLabels.cs` (shared by client and server; Contracts holds no logic today, so Q13 records where it lives). Its signature: `Disambiguate(IReadOnlyList<(Guid MembershipId, string DisplayName)>) -> IReadOnlyDictionary<Guid, string>`. It returns "Sam Lee" alone, or "Sam Lee · 3F2A" on a collision.
>   - Plus `Initials(displayName)`, the monospace avatar initials (DESIGN.md `avatar`).
>   - The Membership list uses both (initials avatar, and the discriminator beside the name only on a collision) although the address already disambiguates there, so the function has a live caller in this story.
> - **"The reason is stated"**: quote `EXPERIENCE.md:151` in the function's remarks.
> - **Proof:**
>   - Unit cases: two identical names get two distinct suffixes, and a unique name gets none.
>   - Comparison is ordinal after NFC (the 4.1 Q3 normalisation family), so "Sam Lee" and "sam lee" are distinct names.
>   - The suffix is stable across calls and orderings.
>   - The suffix never contains `@` or any fragment of an address. A property check over random addresses and ids.
>   - A browser case: two seeded Memberships named alike render distinct visible labels.

**AC4 — the Role picker's narrowing, absent rather than disabled** (FR-13, UX-DR11, UX-DR27; ledger `:329`)
**Given** an Admin
**When** the Role picker renders
**Then** it offers Member ↔ Viewer only, and the controls an Admin lacks are absent rather than disabled
**And** only an Owner can promote a Membership to Admin or demote one from Admin

> - **Route** (Q9): `PATCH /api/v1/spaces/{spaceId:guid}/memberships/{membershipId:guid}` with body `ChangeMembershipRoleRequest(string Role)`, under `.RequiresCapability(Capability.ChangeMembershipRole)`. `Idempotency-Key` is handled by the pipeline. The answer is `200 MembershipResponse` (the updated row).
> - **The target rule, enforced without the slice reading a Role** (Q1 **[LEE]**; ledger `:329`). Recommended (a):
>   - `RoleCapabilityMatrix` gains the Qualified cells' target rules in Domain, e.g. `RoleChangeAuthority For(Role actor)`. It returns `{ From: {Admin, Member, Viewer}, To: {Admin, Member, Viewer} }` for Owner and `{ From: {Member, Viewer}, To: {Member, Viewer} }` for Admin. **Owner is in neither set for any actor.**
>   - The pipeline computes it from the Role it already read and places it in a request-scoped holder, e.g. `IActingAuthority`/`QualifiedAuthority` in Application, set only by `ActiveSpaceResolution`.
>   - The slice and store receive **the sets**, never the Role.
>   - The store's one guarded statement composes them:
>
>     ```
>     UPDATE Membership SET Role = @new
>     WHERE Id = @target AND Role IN (@from...) AND @new IN (@to...)
>       AND Role <> @new
>       AND EXISTS (actor's Membership still holds the Role the authority was computed from) -- Q1 (a), in-flight demotion
>     ```
>
>     It is run under the Space's Memberships application lock (Q17).
>   - On rowcount 0, a disambiguation read decides between:
>     - target absent: the in-Space not-found, `BoundaryProblems.NotAvailable()`, unrecorded and held by the floor (4.2's revoke precedent);
>     - same Role: a no-op `200` with the unchanged row (Q11);
>     - Owner target: `409 owner-role-fixed` (Q10);
>     - outside the actor's authority: a typed `QualifiedRefusal` outcome that **the pipeline** turns into the `403 insufficient-role` for `ChangeMembershipRole` and records as `InsufficientRole` (AD-20: the pipeline, not the slice, records every 403).
> - **Gates:**
>   - `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` and `No_slice_or_endpoint_reads_the_Role_off_a_Membership_row` stay green and **unwidened**.
>   - New: `Only_the_pipeline_sets_the_acting_authority` (IL: only `ActiveSpaceResolution` writes it).
>   - New: `The_target_rules_agree_with_the_matrix_transcription` (extends the second transcription in `RefusalBoundaryGateTests.cs:90-140` with the Qualified cells' sets).
>   - New: `A_Qualified_cell_reaches_no_endpoint_without_its_target_rule`: every endpoint whose capability is Qualified for some Role consumes the authority. Today that is the role change; 4.5's removal joins it.
> - **The matrix of outcomes (Slices, both surfaces where the isolation suite applies), each its own case:**
>
>   | Actor | Target (from → to) | Answer |
>   |---|---|---|
>   | Owner | Member → Admin, Admin → Viewer, Viewer → Member, Admin → Member | `200`, row changed |
>   | Owner | own row (Owner) → anything | `409 owner-role-fixed`, unchanged |
>   | Owner/Admin | any → `owner` | `400` validation (`role-not-assignable`), nothing read or written. The pipeline's validation, before the store |
>   | Admin | Member → Viewer, Viewer → Member | `200` |
>   | Admin | Member → Admin; another Admin → Member; own row → Member; Owner row → anything | `403`, recorded `InsufficientRole`, unchanged |
>   | Member, Viewer | anything | `403` from the pipeline, recorded |
>   | anyone | an id from another Space, or random | `404 not-available`, byte-identical to the boundary, unrecorded, floored |
>   | non-member | anything | boundary `404`, recorded `CrossSpace` |
>
> - **Client** (Q12): per row in `MembershipSection`:
>   - The **Owner's row** shows the Role as a fixed chip, with no picker for anyone.
>   - **One's own row** is a fixed chip with "· You" (mockup `space-settings.html:164`).
>   - **For an Admin viewer**, Admin rows are fixed chips, and Member and Viewer rows carry a "Change Role" control offering exactly Member and Viewer.
>   - **For the Owner**, every other row offers Admin, Member and Viewer.
>   - The offered set comes from a client predicate `MembershipCapabilities.OfferableRoles(viewerRole, targetRole)` with a matrix-agreement gate (the `The_clients_invite_predicate_agrees_with_the_capability_matrix` precedent).
>   - **Absent, never `disabled`**: `DesignFoundationGateTests`/a client gate asserts no `disabled` attribute on a Role control for a Role reason (UX-DR27, the existing banned-list gate if one exists; else add it).

**AC5 — never a second Owner, never the sole Owner removed** (AR-12, AD-5, S-2)
**Given** any Role change
**When** it is attempted
**Then** no change can produce a second Owner or remove the sole Owner, guaranteed by the filtered unique index rather than by application logic alone

> - **Read carefully (C2).** The filtered unique index guarantees **at most one** Owner: a second Owner row fails `2601` whatever the application does. It cannot guarantee **at least one**, because zero rows never violate uniqueness (AD-26 relies on exactly this for its demote-first swap). So "removes the sole Owner" needs a second mechanism. Q2 **[LEE]** decides it. Recommended (a):
>   - Owner is in no authority set (AC4), so the statement cannot touch an Owner row or write `Owner`.
>   - The S-2 invariant test runs after every Role operation.
>   - The honest record that the "at least one" half rests on the guarded statement, not the schema. Neither a trigger nor a BLOCK BEFORE UPDATE predicate on the Owner row is compatible with story 5.2's demote-then-promote swap, which passes through zero Owners mid-transaction.
> - **Proof:**
>   - **Schema (the index really is the guarantee for "second"):** under a Space's key, raw SQL that sets a second Membership to `Owner` fails with `2601` on `UX_Membership_SpaceId_Owner`; an update to `Admin` of the same row succeeds. Anchor and plant: the index dropped in a scratch database turns it red. This may already exist from 1.3. If so, name it and keep it.
>   - **Invariant (S-2):** `Every_Space_holds_exactly_one_Owner` is a database-wide assertion (`COUNT(*) WHERE Role = 'Owner' GROUP BY SpaceId` = 1 for every Space; run under each Space's key, or in a test-only connection that sets each key in turn). Run it after every AC4 matrix case and every race.
>   - **Plants:**
>     - `Owner` added to the Owner's `To` set: the promote case is refused `2601`. That must map to a stable answer, never a `500`; recommended `409 owner-role-fixed`. Record it.
>     - `Owner` added to the `From` set: the Owner's own demotion succeeds and the invariant goes red.

**AC6 — `MembershipChanged` at the boundary, and the next request governed** (AR-16, AD-9, NFR-2, V-1)
**Given** a Role change
**When** the transaction commits
**Then** `MembershipChanged` publishes at the transaction boundary in-process, once per affected Account
**And** the change governs that Account's **very next request** with no tolerance, on the browser and the API alike, because no cache may outlive a request

> - **The publish** (Q7): a port in Application, e.g. `IMembershipChanges.Record(Guid accountId, Guid spaceId)`, collected by the unit of work and published **after** commit, never on rollback, a refusal or a no-op. It is delivered in-process to every registered `IMembershipChangeSubscriber`. Production registers none until 7.3/7.7.
>   - Wire it to the existing commit signal (`MembershipActiveSpaceResolver.UnitOfWork`, which already fires `OutboxCommitSignal.Committed`; `Yello.Infrastructure/Outbox/OutboxCommitSignal.cs:46-54`), not to a timer or a table. **Do not reuse the outbox row** (`Only_the_outbox_store_its_dispatcher_and_its_configuration_refer_to_the_outbox_row`).
>   - Every publish writes one structured log line (an event id such as `MembershipChangedPublished`, with `AccountId` and `SpaceId` and nothing else). That makes it observable from a Host process without test-only production code (1.9 Q4).
>   - One Role change affects one Account, so it makes one publish.
> - **Proof (Slices):**
>   - A committed change logs exactly one publish for the target's `AccountId` and the Space, after the request's commit.
>   - A `403`, a `409`, a `404`, a same-Role no-op and an idempotent replay log none.
>   - Plant: publish before commit, or on every attempt. The refusal cases go red.
> - **Next request (V-1, both surfaces):**
>   - Add `REV-ROLE-CHANGED-BY-OWNER` to `RevocationCases.cs`, the product-route twin of `REV-ROLE-CHANGED` (the 3.3 `REV-SPACE-DELETED-BY-OWNER` precedent). The actor is made an Admin of the Owner's Space through the product (an Invitation at Admin, accepted; 4.3). It is anchored on listing Invitations successfully. The Owner then demotes it to Member through `PATCH`. The actor's very next request on each surface is `403` on the Invitations list, and its own write (a Project create) still succeeds.
>   - A Slices case covers the five capability classes for V-1, each on both surfaces: Admin → Member loses Labels, Invitations, rename and the Membership list; Member → Viewer loses Project and Task writes. **No `Task.Delay`**: the next request is sent after the `PATCH`'s `200`.
>   - **No cache is introduced.** `MembershipActiveSpaceResolver` still reads the Role per request (`:39-41`). A gate exists or is added: no `IMemoryCache`/`IDistributedCache`/`HybridCache` in production code.

**AC7 — attribution from a captured name, never a live Account lookup** (UX-DR34, `EXPERIENCE.md:156`)
**Given** attribution of authored content
**When** it renders
**Then** it uses the name captured at authoring time or held on the Membership row, never a live global Account lookup
**And** the reason is stated: resolving a current name globally would propagate a later name change into a Space the reader no longer shares with that person

> - **Attribution today** is only the Invitation's `IssuerName`, captured at issue (`InvitationStore.cs:117-123`). Tasks carry no author (`TaskItem.cs:29-82`); the description log's authorship is 7.2's.
> - Q5 **[LEE]** decides where the **Membership list's** display name comes from:
>   - (a) a live `Account.DisplayName` read, the list being Membership management, which already reads `Account` for the address;
>   - (b) a `Membership.DisplayName` captured at Membership creation, on every insert path.
>   - Recommended (a). Record the rule for authored content in the remarks of `Invitation.IssuerName` and in `TESTING-CONVENTIONS.md` (or the gate), so 7.2 inherits it.
> - **Gate:** `Attribution_never_reads_a_live_Account_name`. No type outside the Membership list projection and the Invitation **issue** reads `Account.get_DisplayName` in a Space-scoped unit of work. The registration and session stores, which are Account-scoped, are named. The existing `Neither_the_list_nor_the_revoke_reads_the_issuers_Account` stays green. Detector oracle required.
> - **"The reason is stated"**: in that gate's remarks and in `MembershipStore`'s.

**AC8 — 4.2's demotion arm through the product route** (ledger `:758`)

> - In `ActiveSpaceEndpointTests.RevokeInvitation.cs`, convert `An_Invitation_whose_issuer_was_demoted_stays_listed_and_revocable_by_any_Owner_or_Admin` (`:191`) so that the issuer is made Admin and then demoted to Member **through `PATCH /memberships/{id}` by the Owner**, not through `wire.ChangeRoleAsync` (`:221`).
>   - Seeding the issuer's Admin Membership through an accepted Invitation is preferred. `AddMembershipAsync` may stay for the seed if acceptance makes the case unreadable; record which.
>   - Leave the removal arm (`:249`) to 4.5.
> - Update the file remark (`:28-29`) and the ledger line.

**AC9 — isolation rows on both surfaces, and the route gates** (AD-3, AD-4, I-1, I-13, I-17; `TESTING-CONVENTIONS.md:704-811`)

> Both routes fail `Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` until they have rows. Add `tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs`, spread into `IsolationCases.All` beside `InvitationCases()`, following 4.2's row kinds (`IsolationCases.Invitations.cs:93-180`):
>
> - `ISO-LIST-MEMBERSHIPS-FOREIGN-SPACE` and `ISO-CHANGE-ROLE-FOREIGN-SPACE`: boundary `404`, byte-identical to an absent Space, recorded `CrossSpace`.
> - `ISO-LIST-MEMBERSHIPS-OWN-SPACE`: the actor's own list, holding **no** name or address from another Space the actor or its Accounts belong to (I-11's spirit: the world's other Spaces have Members with randomised names and addresses held as secrets).
> - `ISO-RECORD-KIND-LIST-MEMBERSHIPS-MEMBER`/`-VIEWER` and `ISO-RECORD-KIND-CHANGE-ROLE-MEMBER`/`-VIEWER`: `403`, recorded `InsufficientRole`. Seed the lesser Role through acceptance (4.3) where practical.
> - `ISO-CHANGE-ROLE-ADMIN-BEYOND-AUTHORITY`: an Admin promoting a Member to Admin gets `403`, recorded. This is the Qualified seam's row.
> - `ISO-CHANGE-ROLE-FOREIGN-ID-IN-OWN-SPACE`: another Space's `MembershipId` in one's own Space gets the in-Space not-found, byte-identical to the boundary, unrecorded.
> - `ISO-TOKEN-OWN-OTHER-SPACE-LIST-MEMBERSHIPS` and `-CHANGE-ROLE` (the 3.4 `TokenOwnOtherSpace` gate requires one per route a Token reaches).
> - Every address and display name seeded is in the actor's secrets (`HoldsSecret`), so the I-17 log check fails on any address in the Host log.
> - No row may be left `PendingUntil: "4.4"` at review (GATE-003).
> - Add both routes to `IsolationTimingTests` only if Q9's in-Space not-found differs in work from the boundary. Otherwise the floor holds them and `ISO-TIMING-IN-SPACE` already proves the floor. Record it.

**AC10 — the client: the Membership section, and the owned client ledger items** (UX-DR14, UX-DR27, UX-DR38, UX-DR39; mockup `space-settings.html:112-179`; ledger `:540`, `:546`, `:550`, `:762`)

> - **`Yello.Client/Components/MembershipSection.razor`** (plus `MembershipRow.razor`), placed in `SpaceSettings.razor` above the Invitations section (mockup order: Members, then Invitations), as `SectionKey.Memberships`. Show it when `MembershipCapabilities.PermitsManageMemberships(role)` (Owner, Admin), with a matrix-agreement gate. `PermitsSpaceSettings` gains it. Its audience is unchanged, because it admits the same Roles.
>   - **The heading** is "Members" with "{Total} of 100" in monospace meta (`DESIGN.md:391`). The sub-copy is "Email addresses are visible here because you're an {Role}. They appear nowhere else in Yello." (mockup `:121`). Q19 confirms the copy.
>   - **A row** holds: the initials avatar, the display name (plus a discriminator on a collision, AC3), the address, and the Role chip or "Change Role" control (AC4).
>   - **Loading** follows `InvitationSection`: a purge registered first, a generation counter, `aria-busy`, and only the latest read applied.
> - **The change flow** (Q12):
>   - "Change Role" replaces the row's control in place (never a dialog, never a stacked modal) with a `fieldset`/`legend` radio group of the offerable Roles, with **no option checked**. The current Role is named in the legend, not pre-selected (UX-DR11). It has "Change" (`button-primary`) and "Cancel".
>   - Sending uses one `Idempotency-Key` per deliberate "Change", kept for an identical resend after no answer (`InvitationSection.razor:362-427`). It is never optimistic.
>   - On `200`: update the row from the response and announce "{DisplayName} is now a {Role}." (no address), with focus returned to the row's "Change Role" control.
>   - On the AC4 `403` (an Admin out of authority, or one's own authority lost meanwhile): re-read the Role (`SpaceArrival`) and follow `SpaceSettings.Permitted()`'s narrated exit if the section is no longer permitted. Otherwise use the capability-shaped copy "Admins can change Roles between Member and Viewer only." (`RefusalCopy`).
>   - On `409 owner-role-fixed`: "Ownership moves only by an Ownership Offer." On a `404`: re-read the list (Q16) and announce "That Membership no longer exists." if the row is gone.
>   - With no answer or a `5xx`: "Not sent." with the key kept.
> - **Ledger `:762` (Q14):** while any row action is in flight, focus must not fall to `body`. **Fix all three existing rows and the new one together**: `InvitationRow`, `LabelRow`, `ApiTokenRow` and `MembershipRow`. Recommended: the in-flight status element is `tabindex="-1"` and receives focus when it replaces the focused button; the existing after-answer focus rules (UX-DR39) are unchanged. One browser case per row type asserts `document.activeElement` is not `body` while the answer is held (hold, assert, release), each seen red under the old markup.
> - **Ledger `:550` (Q15):** `SpaceHome`'s `space-home__narration` region is rendered **empty** on every render and filled after the first render (the `BoardView` announcement-region pattern), so a screen reader announces it. Assert in a browser case that the region exists empty in the first rendered DOM and holds the sentence afterwards. Keep 3.2's purge behaviour.
> - **Ledger `:546` (Q16):** `LabelSection.DeleteAsync`'s `404` re-reads the Label list. If the list answers, drop the row and announce "That Label no longer exists."; if the list `404`s, raise the Space refusal surface as today. Apply the same rule to the Membership change's `404`. Task and Project deletion are unchanged (they are on other surfaces; record it).
> - **Gates and rules:**
>   - No storage of rows (`ClientStorageBanTests`).
>   - No mutable static (`ClientStaticStateGateTests`).
>   - Every new `NavigateTo` through `SpaceNavigation.GoTo`.
>   - Copy in `ClientCopy.resx` with a `<comment>` naming 4.4.
>   - No Razor `else` around user-visible text (the 3.3 copy gates).
>   - CSS in `components.css` only: logical properties, no px or colour literals.
>   - 320×256 reflow and `TextSpacingTests` under both adaptations for the section (ledger `:20`, rolling).
> - **Client tests** (`Yello.Tests.Client`, `ClientSurfaceCollection`, a new `FakeYelloApi.Memberships.cs` using `MembershipRoutes`/`ProblemTypes` constants only, no literals; 4.1 P13):
>   - The Owner sees pickers on every non-Owner row and no picker on its own row.
>   - An Admin sees Member/Viewer only on Member and Viewer rows, fixed chips on Admin and Owner rows, and **no `disabled` attribute anywhere** in the section.
>   - The picker has no option checked.
>   - The change is sent once with a key and waits for the answer.
>   - The announcement holds no `@`.
>   - The address appears in the section only, and is gone after a Space switch.
>   - Two pages load, and the second appends without replacing focused rows.
>   - The `403` narrated exit runs for an Admin demoted meanwhile (the fake answers `403`, then the Role read answers `member`).
>   - Build `Yello.Client` in **Debug** first.

**AC11 — idempotency and races** (AD-18, P-10; `TESTING-CONVENTIONS.md:102-180`)

> - **Replay:** the same `Idempotency-Key` after a committed change returns the original `200` and publishes nothing. A different body under the same key is `422`. A malformed key is `400`. `Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay` stays green (the endpoint returns `IResult`).
> - **Race 1, two writers on one target** (Q17): hold `Yello/Memberships/{spaceId}` on a test connection, send the Owner's Viewer → Admin and an Admin's Viewer → Member for the same target with distinct keys, wait until both are blocked (`WaitUntilBlockedOnALockAsync`), then release. Both answer from a defined set: the second to run sees the first's commit. If the Owner's ran first, the target is Admin and the Admin's change is a `403`. Assert the end state is one of the two serial outcomes, and the invariant holds.
> - **Race 2, the actor demoted while its change is queued (Q1, in-flight authority):** hold the lock, send an Admin's Member → Viewer, wait until it is blocked, then **in the holder's transaction** demote that Admin to Member (the product's statement shape) and commit. The Admin's change must answer `403` and leave the target unchanged, because the guarded statement re-checks the actor's Role. Plant: the actor clause removed. It answers `200` and goes red. *If Q1 decides against the actor clause, record this case as the documented gap instead.*
> - **Race 3, Role change against acceptance:** an accept in the same Space (4.3 takes `Yello/Invitations/{spaceId}`) never blocks on, or deadlocks with, a Role change. Hold the Memberships lock and accept: it answers without release (`WaitUntilBlockedOrAnsweredAsync`). That proves the two locks are independent. If Q17 chooses one shared lock, invert the assertion and state the order.

## Tasks / Subtasks

- [x] **Task 0: Lee's decisions** (before any code)
  - [x] Q1-Q5 are **[LEE]**. The orchestrator routes them to Lee before development. Record each answer in this file under "Decided by Lee" before ATDD.
  - [x] Do not build any of the following without Lee's recorded decision: a Role-read path for a slice, a new RLS predicate, a new email reader, or a stored display name. If development starts without an answer, stop and return the question as a blocker.
- [x] **Task 1: decide Q6-Q19.** Each recommendation is the default. Record each as "Qn - (x)" with its cost in Completion Notes.
- [x] **Task 2: Domain** (AC4, AC5)
  - [x] Add the Qualified cells' target rules to `RoleCapabilityMatrix` (Q1). Owner appears in no set. Remarks quote FR-13 and PRD §12 #4.
  - [x] Add `Role` parsing from a stable code that refuses `owner` for a change (or reuse `InvitationRoles`' parser if one exists).
- [x] **Task 3: Contracts** (AC1, AC3, AC4)
  - [x] `Yello.Contracts/Memberships/`: `MembershipRoutes`, `MembershipResponse`, `MembershipPageResponse`, `ChangeMembershipRoleRequest`, and `MembershipLabels` (Q13).
  - [x] `ProblemTypes.OwnerRoleFixed` (`urn:yello:problem:owner-role-fixed`) and `RoleNotAssignable` (or reuse an existing validation type; Q10). Add remarks on status and disclosure.
- [x] **Task 4: Application** (AC1, AC4, AC6)
  - [x] `Yello.Application/Memberships/ListMemberships/ListMembershipsHandler.cs` and `Yello.Application/Memberships/ChangeMembershipRole/ChangeMembershipRoleHandler.cs`. Folder `Memberships/` per the spine (`ARCHITECTURE-SPINE.md:277,421`); see C5.
  - [x] `IMembershipStore`, the acting-authority holder (Q1), and `IMembershipChanges` plus `IMembershipChangeSubscriber` (Q7).
  - [x] Handlers read no Role, count nothing and record nothing (AR-3).
- [x] **Task 5: Infrastructure** (AC1, AC4-AC7)
  - [x] `MembershipStore`: the paged projection (Role as code, `Account.EmailAddress`, the display name per Q5, `IsYou`), the count, the guarded update under `ApplicationLocks.TakeMembershipsAsync` (Q17) and the disambiguation read.
  - [x] Watch S1200, and split by port if it fires.
  - [x] Collect `MembershipChanged` records in the unit of work and publish after commit (Q7).
  - [x] Q3 (a), if Lee chooses it: a hand-edited, expand-only migration adding `BLOCK AFTER UPDATE` on `Membership` (`fn_SpaceIsolationPredicate(SpaceId)`).
    - Update `SpaceIsolationSchemaTests.TablesThatCannotMove` (`tests/Yello.Tests.Slices/Accounts/RegisterAccount/SpaceIsolationSchemaTests.cs:96-97`) and the per-table predicate counts (`:120-123`).
    - Read `TESTING-CONVENTIONS.md:519-540` before editing the migration.
- [x] **Task 6: Host** (AC1, AC4, AC6, AC9)
  - [x] `ActiveSpaceResolution` sets the acting authority for Qualified decisions, and turns the endpoint's `QualifiedRefusal` outcome into the recorded `403` (nested types to stay under S1200).
  - [x] `Yello.Host/Endpoints/MembershipEndpoints.cs`: an explicit outcome switch, **no `_ =>` arm** (4.1 P10). Register and map in `Program.cs` next to the Invitation routes, and update the route comment block.
- [x] **Task 7: Client** (AC2-AC4, AC10)
  - [x] Files: `MembershipSection.razor`, `MembershipRow.razor`, `MembershipCapabilities`, `SpaceSettings.razor`, `SpaceCapabilities.PermitsSpaceSettings`, `RoleCopy` (if needed), `RefusalCopy`, `ClientCopy.resx`, `components.css`, `FakeYelloApi.Memberships.cs`.
  - [x] Ledger fixes: `InvitationRow`/`LabelRow`/`ApiTokenRow` (`:762`), `SpaceHome` (`:550`), `LabelSection` (`:546`).
- [x] **Task 8: tests and plants** (all ACs). See Testing requirements. Save each original in memory and restore it in a `finally`. **Never** `git checkout --`, `git restore` or `git reset`. Record `| Plant | Expected red | Seen |` in the Debug Log.
- [x] **Task 9: ledger, docs, verification**
  - [x] Apply the ledger dispositions below: one line per CLOSED/UPDATED note, no TAB or FORM FEED, keep the final newline.
  - [x] If Q1 (a) is chosen, add a dated additive note to AD-1 or AD-20 in `ARCHITECTURE-SPINE.md` naming the acting-authority seam. Q3 (a) adds a sentence to AD-2.
  - [x] Grep `Yello.* tests` for comments predicting "4.4" (for example `Membership.cs:48`, `CapabilityDecision.cs:24`, `ActiveSpaceResolution.cs:182`) and rewrite them to say what exists.
  - [x] `sprint-status.yaml`: this story goes to `review` at the end of dev. `epic-4` stays `in-progress`. FR-13 stays staged (7.7, 7.8).
  - [x] Run `docker info --format '{{.ServerVersion}}'`, `dotnet build Yello.slnx` (0/0), `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Record totals. Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

Space settings gains a "Members" section for Owners and Admins. It shows a paged list (50 per page, keyset on join order, true total "n of 100") with each Membership's display name, email address (here and nowhere else) and Role, and lets them change a Role. The change is one `PATCH` under the existing `ChangeMembershipRole` capability. The matrix's `Qualified` Admin cell is finally given teeth: the pipeline, which alone reads the Role, hands the slice the **sets** of Roles the actor may change from and to (Owner is in none). The store composes them into one guarded `UPDATE` under the Space's Memberships lock, together with a re-check that the actor still holds its Role. A refusal for authority comes back as a typed outcome that the pipeline turns into a recorded `403`. The filtered unique index keeps a second Owner impossible; the authority sets keep the sole Owner untouched, and the S-2 invariant test watches both. On commit, one `MembershipChanged` is published in-process for the target Account. Nothing subscribes until Epic 7, and a log line makes it observable. Because the Role is read per request, the target's very next request, by Session or Token, is governed by the new Role, and the revocation suite proves it through the product route. The story also pays four small client debts in the same surface family.

### What exists today (survey at `6b06b20`)

- **Domain:**
  - `Membership(Id, SpaceId, AccountId, Role { get; set; }, CreatedAt)`, with no name field (`Membership.cs:27-57`).
  - `Role { Owner, Admin, Member, Viewer }`, stored by name.
  - `RoleCapabilityMatrix` (`:36-53`): `ChangeMembershipRole` and `RemoveMembership` are Owner Allowed and Admin **Qualified**.
  - `CapabilityDecision.Qualified` (`:24`) "names 4.4, 4.5".
  - `Account(EmailAddress, NormalizedEmailAddress, DisplayName ≤128, ...)`, which is policy-free.
- **Infrastructure:**
  - `MembershipConfiguration`:
    - query filter `SpaceId == ScopedSpaceId || AccountId == ScopedAccountId`;
    - alternate keys `(SpaceId, Id)` (4.6's composite FK) and `(SpaceId, AccountId)` (ApiToken FK, cascading);
    - `UX_Membership_SpaceId_Owner` filtered unique;
    - FK to Account `Restrict`.
  - **RLS on Membership:** FILTER `fn_MembershipVisibilityPredicate` (1.7) plus **BLOCK AFTER INSERT only** (Space-only). There is **no BLOCK AFTER UPDATE** (Q3). Every other Space table has one.
  - `MembershipActiveSpaceResolver` reads the Role per request with no cache (`:39-41,58-103`).
  - `ApplicationLocks` (`TakeInvitationsAsync` and siblings). Add `TakeMembershipsAsync` (`Yello/Memberships/{spaceId}`) per Q17.
  - `OutboxCommitSignal` is the existing after-commit hook.
  - No `IMemoryCache` anywhere.
- **Host:**
  - `ActiveSpaceResolution.cs:124-199`: establish → capability → resolve → Token bound-Space check → `Decide(Role, capability) != Denied`. Qualified passes (`:182-184`).
  - `RequiresCapability` (exactly one per endpoint).
  - `ActiveSpaceResolution.Idempotency.cs`: every Space-scoped write, with no endpoint declaration.
  - `BoundaryProblems`, `NotAvailableFloor`.
  - `InvitationEndpoints` (the pattern to copy).
  - `ActiveSpaceEndpoints` (`GET Active`) is the one other permitted reader of `ActiveSpaceContext.Role`.
- **Client:**
  - `SpaceSettings.razor` (sections Name, Labels, Invitations, Delete; `SectionKey`; `Permitted()` narrates "SpaceSettingsNoLongerAvailable" and routes to Space home on a lower re-read, `:116-138`).
  - `InvitationSection`/`InvitationRow` (row confirm, key per confirm, purge, generation counter, focus rules).
  - `LabelSection`/`LabelRow`, `ApiTokenRow`.
  - `RoleChip` (display-only), `RoleCopy`, `LiveAnnouncement`, `SpaceNarration`.
  - `SpaceHome.razor:34-37` (the region is inserted already filled: ledger `:550`).
  - `ActiveSpaceState.Current.Role` (per page load). `SpaceArrival` re-reads it on every arrival.
- **Tests:**
  - `WireDatabase.AddMembershipAsync`/`ChangeRoleAsync`/`RemoveMembershipAsync` (`:445,484,363`; `ChangeRoleAsync` has no Owner guard).
  - `SpaceSeeding`, `SeedSpaceHoldingMembershipsAsync` (4.3).
  - `REV-ROLE-CHANGED` (raw SQL; already proves the per-request read).
  - The gates `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` (`RefusalBoundaryGateTests.cs:590-615`), `No_slice_or_endpoint_reads_the_Role_off_a_Membership_row` (`:633-651`), `Every_spaceId_endpoint_declares_exactly_one_capability` (`:291`), the matrix transcription (`:90-149`) and `Space_is_read_only_where_named` (`:423-475`; untouched unless the list reads `Space`, which it should not).

### What must be preserved (regression guard)

- **Both Role-read gates stay unwidened.** The whole point of Q1 is to enforce the Qualified cell without breaking them.
- **The boundary `404` stays byte-identical** (`RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal`).
- **4.1 to 4.3 unchanged**: issue (including Lee's 4.1 Q2: an Admin may *invite* at Admin, see C3), list, revoke, view, accept, their races and timing rows.
- **Membership's BLOCK AFTER INSERT stays Space-only** (AD-24, C1 of 4.3). **No both-keys unit of work** is added. The SpaceId-key setter list (`The_SpaceId_key_is_set_by_exactly_the_named_units_of_work`) is unchanged: the Membership routes run on the ordinary resolver.
- **The Space settings narrated exit** and `SpaceNavigationGateTests`, `ClientStaticStateGateTests`, `ClientStorageBanTests`.
- **`REV-ROLE-CHANGED` stays** as the raw-SQL schema proof. The product twin is added beside it.
- **`IdempotencyStore` refuses to save with other changes pending.** The `ExecuteUpdate` tracks nothing; keep the record's save last.
- **AD-10:** no timer, no hosted service, no polling for `MembershipChanged`.

### Locking and the races (RCSI)

- **The Role change** takes `Yello/Memberships/{spaceId}` first (Q17 (a)), then runs its guarded `UPDATE`. That statement re-evaluates under RCSI because an `UPDATE` reads the latest committed row.
- **The actor clause:** `EXISTS (SELECT 1 FROM Membership WITH (READCOMMITTEDLOCK) WHERE Id = @actorMembershipId AND Role = @actorRole)` makes a change by an actor demoted after its request began fail as out of authority.
- **Lock order:** application lock, then rows. No `HOLDLOCK` (3.4).
- **4.5's removal** must take the same lock. **5.2's swap** must take it too, so that a Role change never interleaves with the demote-promote pair. Write both into the ledger as constraints for those stories.
- **Acceptance** (4.3) takes `Yello/Invitations/{spaceId}` and inserts a Membership. A Role change never touches a row being inserted, so the locks are independent (Race 3).

### Open questions

**[LEE]** marks a question whose answer touches isolation, RLS, a `SESSION_CONTEXT` key, a cross-Space read, Role/authority escalation, or security. Q1-Q5 go to Lee before development; each has concrete options and a recommendation. Q6-Q19 are decided by the dev phase as reversible, recorded decisions: take the recommendation unless the code shows it is wrong, and record why.

**Decided by Lee, 2026-11-02 (not open):** Q1 - (a) a pipeline-owned acting authority: Role sets the actor may change from and to (Owner in neither), in a holder only the pipeline writes, composed into the one guarded `UPDATE` with the actor-still-holds-its-Role clause kept; Q2 - (a) "at least one Owner" held by the guarded statement plus the S-2 invariant test, with a dated note on AD-5 (no block predicate or trigger); Q3 - (a) add `BLOCK AFTER UPDATE` on `Membership` with `fn_SpaceIsolationPredicate(SpaceId)` in an expand-only migration; Q4 - (a) the list sits under `ChangeMembershipRole` with Token parity, plus the two address gates; Q5 - (a) a live `Account.DisplayName` read for current Memberships in Membership management only, with a named-reader gate. Q6-Q19 remain for the dev phase.

1. **[LEE] Enforcing the Admin's "Member ↔ Viewer only" target rule without a slice reading the Role** (ledger `:329`; AD-1, AR-3, AD-20).
   - **(a) Recommended. A pipeline-owned acting authority.**
     - Domain declares each Qualified cell's target rule as Role sets (`From`, `To`), and Owner is in none.
     - The pipeline, the only Role reader, computes the sets for the actor and places them in a request-scoped holder that only it writes (gated).
     - The store composes them into the one guarded `UPDATE`, plus an actor-still-holds-its-Role clause, so an actor demoted while its request is in flight cannot act on authority it lost (NFR-2's "no tolerance", read strictly).
     - A refusal is a typed outcome the pipeline turns into the recorded `403 insufficient-role` (AD-20 unchanged).
     - Cost: a new seam in the pipeline, a gate pair and a spine note. 4.5 reuses it for removal.
   - (b) **Split the capability.** Two matrix rows, e.g. `ChangeRoleBetweenMemberAndViewer` (Owner, Admin) and `ChangeRoleInvolvingAdmin` (Owner). The endpoint would have to know the target's current Role before choosing its capability, so this needs two routes or a pre-read in the pipeline. Cost: the code matrix diverges from the PRD's, which is "the single source of truth" (`prd.md:331`). It also does not catch the in-flight demotion.
   - (c) **Widen the Role-read gates** so the slice (or store) reads the actor's Role. Cost: it reverses AD-1/AR-3's "the pipeline, not the slice", which every story since 1.6 has relied on. The gates exist to prevent exactly this.
   - Sub-question: keep the actor clause (a) or drop it (accept that a request authorised before a concurrent demotion completes). Recommended: keep it.
2. **[LEE] "Never the sole Owner removed", when the filtered index only prevents a second Owner** (AC5, AD-5, AD-26, AR-12; C2).
   - **(a) Recommended.** The index guarantees "at most one". "At least one" is held by Owner being in no authority set, so no Role-change statement can read or write an Owner row. The S-2 invariant test runs after every Role operation and race. AD-5's text is honest about which half the schema carries: record a dated note. Cost: half of AC5's "guaranteed by the filtered unique index" is a guarded statement plus an invariant test, not the schema.
   - (b) A `BLOCK BEFORE UPDATE` RLS predicate (or a trigger) that forbids changing an Owner row's Role. Cost: it breaks story 5.2's AD-26 demote-then-promote swap, which must demote the Owner. 5.2 would need its own exemption, which is a bypass by another name.
   - (c) An AFTER trigger asserting exactly one Owner per touched Space. Cost: SQL Server has no deferred constraints, so 5.2's swap, which passes through zero Owners between its two statements, fails it. Not buildable alongside AD-26.
3. **[LEE] Adding `BLOCK AFTER UPDATE` to `Membership`** (AD-2; RLS).
   - **(a) Recommended. Add it**, using `fn_SpaceIsolationPredicate(SpaceId)`, in an expand-only migration. Membership becomes the first Space table the product *updates*, and every other updated Space table already carries one (Project, Task, Label, TaskLabel, Invitation). `SpaceId` is init-only in the entity, but the database should not rest on that. Cost: a migration, a schema-test update (`TablesThatCannotMove`), and a check that 5.2's swap runs under the Space's key, which it will.
   - (b) Leave it. The EF filter, the init-only property and the guarded statement's `Id` predicate already confine the update. Cost: an RLS asymmetry on the one table that decides authority.
4. **[LEE] Who may read an Account's email address, and on which capability the list sits** (AC1, AC2; AD-23, §6.1, UX-DR34).
   - **(a) Recommended.**
     - The list sits under `ChangeMembershipRole` (Owner, Admin).
     - It returns `Account.EmailAddress` as typed.
     - New gates: (i) only the Membership and Invitation list response types carry an address property; (ii) only named stores read `Account.EmailAddress`/`NormalizedEmailAddress`.
     - Token surface: an Owner's or Admin's Token reads the list (AD-4 parity).
     - Cost: two gates; the list exposes every Member's address to every Admin, which is the design (`EXPERIENCE.md:213`).
   - (b) A new matrix row `ViewMemberships` (Owner, Admin). Cost: the code matrix gains a row the PRD does not have.
   - (c) Session-only for the list (no Token reads addresses). Cost: an AD-4 asymmetry; it would need its own isolation expectation and recorded variance.
5. **[LEE] Where the Membership list's display name comes from** (AC7; UX-DR34 "never a live global Account lookup").
   - **(a) Recommended. A live `Account.DisplayName` read, for current Memberships in Membership management only.** The rule against live lookups targets *attribution of authored content* for people the reader may no longer share a Space with. A current Membership's name is shared by definition, and this surface already reads `Account` for the address. Attribution keeps captured names (`IssuerName`; 7.2's change log). Gate: `Account.get_DisplayName` is read only by named types. Cost: if a display-name change feature is ever built, the list shows the new name at once (correct for current Members).
   - (b) **`Membership.DisplayName` captured at Membership creation.** Every insert path writes it: `AccountRegistrationStore` (Lee kept registration's files unchanged in 4.3), `SpaceCreationScope`'s store, and the accept. A migration backfills it from `Account`. Cost: three insert paths, a backfill, a touch to registration (AD-22/23 duration unaffected in principle, but its byte-for-byte proof must be rerun), and a second copy of the name to keep in step.
   - (c) (a) now, plus a `Membership.DisplayName` column added empty for 4.6/7.x to fill. Cost: a column nobody writes yet; not recommended.
6. **(not [LEE]) Is "Remove" in scope?** **(a) Recommended: no** (4.5). The list leaves a slot for the control and renders none.
7. **`MembershipChanged` publishing** (AC6; AD-9, AR-16; C4). **(a) Recommended:** build the in-process seam now: an Application port, collected in the unit of work, published after commit through the existing commit signal, one log line per publish, no production subscriber. 7.3/7.7 subscribe. (b) Defer to 7.7 as 4.3 did. Cost: AC6's first clause would be unmet, and it is this story's AC verbatim.
8. **Paging** (AC1). **(a) Recommended:** 50 per page, keyset `(CreatedAt, Id)` ascending, true `Total`, append-never-recycle, "Show more". (b) Order by Role then name. Cost: a three-column keyset with a nullable-free but collation-sensitive name; join order already puts the Owner first in a Space they created.
9. **Routes and shapes** (AC1, AC4). **(a) Recommended:** `GET .../memberships?after=` → `MembershipPageResponse`; `PATCH .../memberships/{membershipId}` `{ "role": "admin" }` → `200 MembershipResponse`. (b) `PUT .../memberships/{id}/role` → `204`. Cost: the client then re-reads the row.
10. **The Owner's own row, and `owner` in the body.** **(a) Recommended:** `409 owner-role-fixed` for any change to the Owner's row by the Owner, with copy "Ownership moves only by an Ownership Offer."; `400 role-not-assignable` for `owner` (or an unknown code) in the body, before any read. An Admin targeting the Owner row is out of authority, so `403`. (b) `403` for every Owner-row case. Cost: the Owner holds full authority, so "insufficient Role" would be untrue for them.
11. **Same Role (no-op).** **(a) Recommended:** `200` with the unchanged row, nothing written, nothing published. (b) `409`. Cost: a double-click or a resend with a new key would read as an error.
12. **The picker interaction** (UX-DR11). **(a) Recommended:** an in-row "Change Role" that opens an in-place radio group with **no option checked**. The current Role is in the legend, then "Change"/"Cancel". (b) A native `<select>` showing the current Role and sending on change. Cost: a pre-selected value, and a send on arrow-key navigation in some browsers.
13. **The discriminator** (AC3). **(a) Recommended:** the first four hex characters of a SHA-256 of the `MembershipId`, uppercase, shown as "Name · 3F2A" only when two names collide in the Space. It is stable, Membership-scoped and discloses nothing new. It lives in `Yello.Contracts/Memberships/MembershipLabels.cs` (pure, shared by client and server); if a gate forbids logic in Contracts, put it in the client and say so. (b) A per-Space ordinal by join order ("Sam Lee (2)"). Cost: it renumbers when someone leaves, so a reader's "Sam Lee (2)" silently becomes someone else.
14. **Focus while a row action is in flight** (ledger `:762`). **(a) Recommended:** the status element that replaces the confirming button is `tabindex="-1"` and takes focus, in all four row components. (b) Keep the button in the DOM with `aria-disabled="true"` and changed text. Cost: a disabled-looking control, which is close to UX-DR27's banned state, though not for a Role reason.
15. **The narration region** (ledger `:550`). **(a) Recommended:** render it empty, fill it in `OnAfterRenderAsync` after the first render, as `BoardView` does.
16. **A row action answered `404`** (ledger `:546`). **(a) Recommended:** re-read the list. If it answers, drop the row and announce. If it `404`s, raise the Space refusal surface. This applies to the Label delete and the Membership change; Task/Project deletion are unchanged. (b) Keep the current behaviour and re-own the entry to 4.5.
17. **The lock.** **(a) Recommended:** a new `Yello/Memberships/{spaceId}` application lock taken by the Role change, and later by 4.5's removal and 5.2's swap (ledger constraints). (b) Reuse `Yello/Invitations/{spaceId}`. Cost: Role changes then queue behind issues and accepts.
18. **Acceptance's `MembershipChanged`** (ledger `:769`). **(a) Recommended:** once Q7's seam exists, the accept records one publish for the new Member at its commit (`InvitationScope`'s commit path). Update `:769` to "published; 7.7 subscribes". (b) Leave acceptance unchanged. Cost: AD-9's "any operation mutating a Membership" stays unmet for one path.
19. **Copy** (`EXPERIENCE.md:99`; sentence case, no exclamation marks, capability-shaped refusals). **(a) Recommended:**
    - "Members" / "{n} of 100"
    - "Email addresses are visible here because you're an {Role}. They appear nowhere else in Yello."
    - "Change Role" / "Change {DisplayName}'s Role. Currently {Role}." (legend) / "Change" / "Cancel" / "Changing."
    - "{DisplayName} is now a {Role}."
    - "Admins can change Roles between Member and Viewer only."
    - "Ownership moves only by an Ownership Offer."
    - "That Membership no longer exists." / "That Label no longer exists."
    - "Show more" / "Loading Members." (visually hidden) / "Not sent."
    - "You" as a suffix on one's own row ("· You").

**Summary.** Q1-Q5 are [LEE]. The recommended options add one pipeline-owned seam (Q1), add no RLS relaxation, and add **one** RLS predicate (Q3 (a), a tightening). They state honestly which half of the Owner invariant the schema carries (Q2), add email-reader and email-contract gates (Q4), and keep the display name as a live read for current Members only (Q5). None disables a policy, adds a session key, adds a both-keys unit of work, or widens a Role-read gate.

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. AC4's "Qualified" cell versus the Role-read gates.** The matrix lets an Admin through and leaves the target rule to "the owning slice", while two gates forbid any slice from reading a Role. Ledger `:329` saw this. Q1 resolves it.
- **C2. AC5's "guaranteed by the filtered unique index"** can only be half true. A unique index cannot express "at least one" (zero rows satisfy it), and AD-26 depends on that. Q2.
- **C3. An Admin may *invite* at Admin (Lee's 4.1 Q2) but may not *promote* to Admin (FR-13).** So an Admin can create an Admin by invitation, and nothing in 4.4 changes that. Record it for Lee and the PM against PRD §12 assumption #4. It is not this story's to change.
- **C4. AD-9's publisher versus "no publisher before 7.7"** (4.3 Q14). This story's AC requires the publish, so 4.4 builds the seam (Q7) and 7.7 still owns its subscribers.
- **C5. The folder.** The spine shows `Memberships/RevokeInvitation/` (`:277`, `:398`); 4.1 to 4.3 settled `Invitations/`. Membership slices belong in `Application/Memberships/`, which matches the spine's area name.
- **C6. AD-3's `{spaceId}` rule** holds for both routes. Nothing here is a non-`{spaceId}` route.
- **C7. The mockup** shows a remove `×` and "EDITING NOW" on rows (`space-settings.html:123-124`). Both are 4.5's (UX-DR36). 4.4 renders neither.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:329` → **UPDATED by 4.4 (Lee's Q1 (x))**: the seam exists and the Role change consumes it. The removal half stays **4.5's**.
- `:540` → **UPDATED by 4.4**: the Members section exists. Remaining owners: 5.1, 6.3.
- `:546` → **CLOSED by 4.4 (Q16)**, or re-owned with a reason.
- `:550` → **CLOSED by 4.4 (Q15)**, with the browser case name.
- `:758` → **UPDATED by 4.4**: the demotion arm runs through `PATCH`. The removal arm stays **4.5's**.
- `:762` → **CLOSED by 4.4 (Q14)**, with the four case names.
- `:769` → **UPDATED by 4.4 (Q18)**, if built.
- **New entries:**
  - "4.5's removal and 5.2's swap take `Yello/Memberships/{spaceId}` and use the acting-authority seam" (owners 4.5, 5.2; low).
  - C3, Admin can create an Admin by invitation (owner **Lee/PM**; low).
  - If Q2 (a): "AR-12's 'at least one Owner' half rests on the guarded statement and S-2, not the schema" (owner **Lee**, low; recorded, not a defect).

### Architecture compliance (must)

- **AD-21 ring.** Handlers in Application; the store, lock and publisher plumbing in Infrastructure; the pipeline seam and endpoints in Host; contracts in Contracts. Check `AllowedReferenceEdges.cs`.
- **AR-3 / AD-1.** Slices never read a Role, count a bound, set a key or record idempotency or refusals. The acting authority carries Role **sets**, not the actor's Role.
- **AD-2.** No raw SQL outside Infrastructure. The store needs no `Where(SpaceId == ...)` (the key and filter do it), though adding one is allowed.
- **AD-20.** Every `403` is recorded by the pipeline, including the Qualified refusal.
- **AD-23.** No address in any log. Addresses only in the list response.
- **AR-16/AD-9.** Publish after commit, in-process, once per affected Account. No timer (AD-10).
- **No test-only code in production** (1.9 Q4). Observe the publish through the log line.

### Library / framework

No new package. Use:

- EF Core 10.0.11 (`ExecuteUpdateAsync` composed with `Where`; `SqlQuery`; a hand-edited migration if Q3 (a));
- ASP.NET Core 10.0.11 minimal APIs (`MapGet`, `MapPatch`);
- Blazor WebAssembly 10.0.11;
- Microsoft.Data.SqlClient 7.0.2 (`sp_getapplock` through `ApplicationLocks`);
- xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3, and Mono.Cecil for the IL gates.

No web research was needed or run: every API used is already in the codebase.

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-4-4-manage-memberships-and-roles.md` (decisions per Q, seed constraints
checked, findings). Written by the ATDD run of 2026-11-02 against Lee's Q1-Q5 (a) and Q6-Q19 at their recommendations. Every
scaffold is `Skip = "red: AC<n> - ..."`, every isolation row `PendingUntil: "4.4"`. Route and problem-type stand-ins live in
`tests/Yello.Tests.Shared/MembershipWire.cs` (`ListRoute`, `MembershipRoute`, `MembershipsOf`, `MembershipOf`,
`OwnerRoleFixedProblemType`, `RoleNotAssignableProblemType`); Task 3 swaps every use for `MembershipRoutes`/`ProblemTypes` and
deletes the six.

| File | Cases | ACs | Green today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ListMemberships.cs` | 5 | AC1, AC2, AC7 | none |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ChangeMembershipRole.cs` | 24 (14 methods) | AC4, AC5, AC6, AC11 | `A_second_Owner_fails_2601_on_the_filtered_index_while_an_Admin_update_of_the_same_row_succeeds` |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ChangeMembershipRoleRace.cs` | 3 | AC11 (races 1-3) | `An_acceptance_answers_while_the_Spaces_Memberships_lock_is_held` (race 3) |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs` (one added) | 1 | AC8 (twin of the raw-SQL demotion arm; delete the original when green) | none |
| `tests/Yello.Tests.Slices/Memberships/MembershipLabelsTests.cs` | 7 (5 methods, reflection until Task 3) | AC3 | none |
| `tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs` (spread in `IsolationCases.Invitations.cs`) | 11 rows, pending | AC9 (AC1, AC4) | n/a |
| `tests/Yello.Tests.Revocation/RevocationCases.cs` (`REV-ROLE-CHANGED-BY-OWNER`; `RevocationWorld` gained `Mail`, `RevocationCase` gained `Skip`) | 1 row x 2 surfaces | AC6 (V-1) | none |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Memberships.cs` | 14 | AC1, AC2, AC4, AC5, AC7, AC10 | `Only_the_Membership_list_and_the_Invitation_list_carry_an_email_address`, `Only_named_types_read_an_Accounts_email_address`, `Attribution_never_reads_a_live_Account_name`, and their two detector oracles |
| `tests/Yello.Tests.Client/MembershipSectionTests.cs` (+ `FakeYelloApi.Memberships.cs`) | 13 methods (19 cases) | AC10 (AC1-AC4), ledger `:546`, `:550`, `:762` | none |

Harness added (no product code): `WireDatabase.MembershipRowsInAsync`/`MembershipOfAsync`/`SpacesNotHoldingExactlyOneOwnerAsync`
(S-2, scoped to the case's Spaces because the class database is shared)/`SetDisplayNameAsync`. The green-today cases are standing
regression gates: leave them active. The existing no-cache gate is `SpaceResolutionSeamTests.No_production_code_introduces_a_cache`.

**Activation order (riskiest assumption first):**

1. `An_Admin_demoted_while_its_change_is_queued_is_refused_403_and_the_target_is_unchanged` - the acting authority (Role sets from
   the pipeline, the guarded `UPDATE` with the actor clause under `Yello/Memberships/{spaceId}`, the typed refusal turned into
   the pipeline's recorded `403`) without widening either Role-read gate. Then `An_Admin_beyond_its_authority_...` (x4),
   `ISO-CHANGE-ROLE-ADMIN-BEYOND-AUTHORITY`, and the three authority gates.
2. The Owner's and Admin's changes, the Member/Viewer refusals, the own-row `409`, the body `400` (S-2 after each); then the
   BLOCK AFTER UPDATE case with the `SpaceIsolationSchemaTests` amendment.
3. The list (403/404, 60-row walk, 100-row bound, shape with the live name), the routes and contract gates, the in-Space `404`,
   the replay.
4. `MembershipChanged`: one publish, no publish, same Role.
5. Race 1 (its deciding assertion is that both waited; plant 5 is masked on the end state).
6. V-1 (x2), `REV-ROLE-CHANGED-BY-OWNER`, the AC8 twin.
7. The isolation rows (clear every `PendingUntil: "4.4"` before review, GATE-003), the log case, the problem-type gate.
8. `MembershipLabelsTests`, then the client gates.
9. The browser cases (build `Yello.Client` in Debug first), the three ledger cases last, each seen red under the old markup.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the
correction recorded in the Debug Log, not deleted.

Verified at the ATDD run: `dotnet build Yello.slnx` 0 warnings, 0 errors; full run (`--max-parallel-test-modules 1`) 2,116
total, 2,056 passed, 0 failed, 60 skipped (10 existing + 50 scaffolds).

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:

- Traits: `Suite`, `Priority`, `Requirement` (FR-13, AR-12/AD-5, AR-16/AD-9, NFR-2, UX-DR14, UX-DR34), `Assumption` (`PRD-12-4` for every case resting on "Admins cannot modify each other"), and `Tier`. Names are underscore sentences.
- Never `Task.Delay`. Races wait on `LCK_M_%` (`:102-180`).
- **Never a shared email address** (`InvitationWire.FreshAddress`). Display names are randomised, except in the collision cases, which fix them on purpose.
- **Seed through the product where the case is about Membership.** Make Admins, Members and Viewers via an Invitation at that Role, accepted (4.3). `WireDatabase.AddMembershipAsync` and `SpaceSeeding` only for bulk (100) and where acceptance would bury the point. Record which.
- **No new container-backed test class.**
  - Slices: partials `ActiveSpaceEndpointTests.ListMemberships.cs`, `.ChangeMembershipRole.cs`, `.ChangeMembershipRoleRace.cs`.
  - Isolation: rows in `IsolationCases.Memberships.cs`.
  - Revocation: one row in `RevocationCases.cs`.
  - Handler unit tests take no database (`tests/Yello.Tests.Slices/Memberships/...`).
- **Every absence has an anchor and is seen red under a plant.** A plant caught 0/3 is recorded as masked, never deleted.
- **Memory budget (CLAUDE.md).** One `dotnet test` at a time. Iterate with `--project ... --filter`. The Revocation project starts its own SQL Server, so never run it beside another project. Run the full suite with `--max-parallel-test-modules 1`, and rerun Client failures alone.

**Minimum plant table** (restore each in a `finally`; rebuild `Yello.Client` in Debug before each browser plant):

| # | Plant | Expected red |
|---|---|---|
| 1 | Admin's `To` set gains Admin | AC4 Admin-promote case; `ISO-CHANGE-ROLE-ADMIN-BEYOND-AUTHORITY`; the target-rule transcription gate |
| 2 | Owner added to the Owner's `From` set | the AC4 Owner-own-row case; the S-2 invariant |
| 3 | Owner added to the Owner's `To` set | the AC5 second-Owner case (must answer `409`, never `500`) |
| 4 | The actor clause removed | AC11 race 2 |
| 5 | The Memberships lock removed | AC11 race 1 (or recorded masked, with the reason) |
| 6 | The slice reads `ActiveSpaceContext.Role` | `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` |
| 7 | A second writer of the acting authority | `Only_the_pipeline_sets_the_acting_authority` |
| 8 | The Qualified refusal returned as a plain `403` by the endpoint (unrecorded) | `ISO-CHANGE-ROLE-ADMIN-BEYOND-AUTHORITY` record assertion |
| 9 | `MembershipChanged` published before commit or on a refusal | AC6 no-publish cases |
| 10 | A Role cache (a static or singleton dictionary) in the resolver | `REV-ROLE-CHANGED-BY-OWNER`; the no-cache gate |
| 11 | `EmailAddress` added to another response contract | the contract gate |
| 12 | Another store reads `Account.EmailAddress` | the reader gate |
| 13 | The discriminator built from the address | the AC3 property case |
| 14 | A `disabled` Role control for an Admin | the client absence case |
| 15 | The picker pre-checks the current Role | the no-default client case |
| 16 | The list unpaged (all 100 in one page) | the AC1 paging case |
| 17 | The narration region rendered filled | the ledger `:550` case |
| 18 | The in-flight status span without focus | the ledger `:762` cases (each row) |
| 19 | Q3 (a) only: the BLOCK AFTER UPDATE predicate omitted from the migration | the schema test |

**Run** (CLAUDE.md): `docker info --format '{{.ServerVersion}}'`; `dotnet build Yello.slnx` (0/0); `dotnet build-server shutdown`; `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Expect three migration-bundle skips.

### Previous story intelligence (4.3, 4.2, 4.1)

- **4.3:**
  - S1200 pushed the pipeline step into nested types and the endpoints into their own class. Expect the same here.
  - A gate scaffold that names an `internal` Infrastructure type from Host is wrong by the ring: gate the port instead.
  - The `REV`/`ISO` rows must seed through the product (the accept now exists, so use it).
  - The intermittent `401` case taught that every assertion must state what it compared.
  - Plants in weak forms were blocked by analyzers (S1172) or masked (`typeof`). Plant the strong form, and record both.
- **4.2:**
  - The guarded `ExecuteUpdateAsync` shape, and its disambiguation read deciding the answer after rowcount 0.
  - The generation counter for overlapping list reads.
  - Focus never to `body` after an answer (UX-DR39).
  - The fake API answers what the Host answers, through contract constants.
  - Address purge on leaving the Space.
- **4.1:**
  - P10: no `_ =>` arm. P13: no literals in the fake. P14: never poll SQL back to back.
  - The Role picker is a `fieldset`/`legend` radio group with nothing checked.
  - Lee's Q2: an Admin may invite at Admin (C3).
- **The revocation suite** has its own engine. 3.3 added a product-route twin (`REV-SPACE-DELETED-BY-OWNER`) beside the raw-SQL row; do the same.

### Git intelligence

Recent commits: `6b06b20` (4.3 review close: a late idempotency replay under the lock), `5382fce` (4.3 implementation), `17191e8` (4.3 ATDD scaffolds), `5dba45e` (Lee's 4.3 decisions recorded), `7c447d0` (4.3 story).

The cycle is:

1. story;
2. Lee's [LEE] decisions recorded in the story;
3. ATDD scaffolds (`[Fact(Skip = "red: ACn - ...")]`, `atdd-checklist-<key>.md`, rows with `PendingUntil`);
4. implementation;
5. review fixes.

The orchestrator commits each. 4.3's File List names the pipeline and endpoint files this story extends.

### Project Structure Notes

- **New (expected):**
  - `Yello.Contracts/Memberships/{MembershipRoutes, MembershipResponse, MembershipPageResponse, ChangeMembershipRoleRequest, MembershipLabels}.cs`
  - `Yello.Application/Memberships/ListMemberships/`, `Yello.Application/Memberships/ChangeMembershipRole/`, `IMembershipStore.cs`, `IMembershipChanges.cs`, `IMembershipChangeSubscriber.cs`, and the acting-authority holder (name per Q1)
  - `Yello.Infrastructure/Persistence/MembershipStore.cs` and the publisher plumbing (Q7); a migration if Q3 (a)
  - `Yello.Host/Endpoints/MembershipEndpoints.cs`, and `Yello.Host/Spaces/ActiveSpaceResolution.Authority.cs` (or nested)
  - `Yello.Client/Components/MembershipSection.razor`, `MembershipRow.razor`, `Yello.Client/Spaces/MembershipCapabilities.cs`
  - Tests: the Slices partials above, `IsolationCases.Memberships.cs`, `FakeYelloApi.Memberships.cs`, `MembershipSectionTests.cs`, gate files `RefusalBoundaryGateTests.Memberships.cs`
- **Updated (expected):**
  - Domain: `RoleCapabilityMatrix.cs`, `CapabilityDecision.cs` (comment only), `Membership.cs` (comment only)
  - Infrastructure: `ApplicationLocks`
  - Host: `ActiveSpaceResolution*.cs`, `Program.cs`
  - Contracts: `ProblemTypes.cs`
  - Client: `SpaceSettings.razor`, `SpaceCapabilities.cs`, `InvitationRow.razor`, `LabelRow.razor`, `ApiTokenRow.razor`, `LabelSection.razor`, `SpaceHome.razor`, `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`
  - Tests: `RevocationCases.cs`, `ActiveSpaceEndpointTests.RevokeInvitation.cs`, `IsolationCases.cs` (spread), `RefusalBoundaryGateTests.cs` (transcription), `TextSpacingTests.cs`, `SpaceIsolationSchemaTests.cs` (Q3 (a))
  - Docs: `ARCHITECTURE-SPINE.md` (dated notes per Q1-Q3), `deferred-work.md`, `sprint-status.yaml`
  - Possibly `InvitationStore.cs` (Q18)
- **No new project, no new package, no new container-backed test class.**

### References

- `epics.md`: `:97` (FR-13), `:155` (NFR-2), `:178` (§6.1), `:200` (AR-12), `:204` (AR-16), `:249` (UX-DR11), `:252` (UX-DR14), `:271-283` (UX-DR27/28/29/34/36/38/39), `:372`, `:402`, `:1613-1617`, `:1755-1794` (4.4), `:1796-1834` (4.5, the boundary), `:1877-1916` (4.7, the boundary).
- `prd.md`: `:50-60` (glossary: Account display name, Membership, Role, Admin), `:293-313` (FR-13, FR-14), `:315-349` (§4.4, the matrix), `:692-700` (NFR-2), `:895` (§12 #4).
- `ARCHITECTURE-SPINE.md`: `:71-75` (AD-1), `:77-87` (AD-2 and its 4.3 amendment), `:89-95` (AD-3), `:97-101` (AD-4), `:103-107` (AD-5), `:109-113` (AD-6), `:127-131` (AD-9), `:183-188` (AD-18), `:196-200` (AD-20), `:218` (AD-23), `:220-232` (AD-24), `:240-245` (AD-26), `:277`, `:421`.
- `EXPERIENCE.md`: `:53`, `:99`, `:115`, `:151-156`, `:202-213`, `:244`, `:267`. `DESIGN.md`: `:391`, `:439`, `:455`, `:464`. `mockups/space-settings.html:1-30`, `:108-179`.
- `specs/spec-yello/`: `acceptance-criteria.md:132-137` (CAP-13); `role-capability-matrix.md`.
- `test-design-qa.md`: `:205`, `:274` (I-10), `:280` (V-1), `:319` (S-2). `test-design-architecture.md:528-540` (R9).
- `implementation-readiness-report-2026-08-22.md`: `:492`, `:532`, `:542`, `:606`, `:651`. `sprint-change-proposal-2026-09-13.md:41`, `:89`.
- `tests/TESTING-CONVENTIONS.md`: `:87-200`, `:458-490`, `:519-540`, `:704-811`.
- `deferred-work.md`: `:329`, `:540`, `:546`, `:550`, `:701`, `:758`, `:762`, `:769`.
- `4-3-accept-an-invitation.md` (Q1-Q19, Review Findings, Debug Log), `4-2-see-and-revoke-pending-invitations.md` (AC2, AC4, Q8, Review Findings), `4-1-invite-an-email-address-to-a-space-at-a-role.md` (Q2, C6, the Role picker).

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), bmad-dev-story, unattended story cycle, 2026-11-02.

### Debug Log References

**Activation.** Every ATDD scaffold was activated in the checklist's order, starting with race 2 and the Admin-beyond-authority theory (the acting authority, the riskiest assumption). The ATDD run had already seen the wire scaffolds red on the unmapped route's boundary `404`; the dev phase saw each go green against the implementation, and saw the guarded behaviours red again under the plants below.

**Scaffolds corrected (recorded, not deleted):**

1. `The_very_next_request_after_a_demotion_is_governed_on_both_surfaces(member, viewer)` - the seeded Space held no Status, so the Task-creation probe answered `500` before the change. The case now seeds one Status with the Space.
2. `ISO-LIST-MEMBERSHIPS-OWN-SPACE` - the shared Member's second join through the product read the first Invitation's email back from the world's mail drop (`WaitForEmailToAsync` takes the first email to an address), so the accept answered `invitation-not-valid`. The second Membership is now seeded directly (`AddMembershipAsync`).
3. AC8 - the product-route twin passed; the raw-SQL original was deleted and the twin took its name (`An_Invitation_whose_issuer_was_demoted_stays_listed_and_revocable_by_any_Owner_or_Admin`); the file remark now describes the product route.
4. The six `MembershipWire` stand-ins were swapped for `MembershipRoutes` and `ProblemTypes.OwnerRoleFixed`/`RoleNotAssignable` in Slices, Isolation, Revocation and the fake, then deleted.
5. `SpaceBoundaryPipelineTests` and `ActiveSpaceUnitOfWorkTests` compose the pipeline from their own service collections; the pipeline now resolves `ActingAuthority`, so both register it (19 cases failed on the first full run with "No service for type ActingAuthority", green after). Production registers it in `Program.cs`.
6. Added beyond the scaffolds: `MembershipHandlerTests` (31 cases with the labels tests, no database: paging, the authority handed as sets, fail-closed authority, `Judge`, the cursor, `AssignableRoles`) and `TextSpacingTests.Text_spacing_and_text_only_zoom_clip_nothing_in_the_Members_section` (both adaptations, ledger `:20` rolling).

**Fixes the tests drove:** S1200 on `ActiveSpaceResolution` (the matrix decision moved into the nested `QualifiedAuthority` step); the client copy-literal, disabled-attribute and mutable-static gates (comments rephrased, `IReadOnlyList` statics); the Space-home narration region first lived inside `SpaceArrival` and was filled in the same browser task it was inserted, then rendered on the refusal page and broke `A_Project_id_followed_under_another_Space_..._refuses_as_unlisted`'s fingerprint - it is now outside `SpaceArrival`, rendered only when there is a sentence, and filled after the arrival (`AfterArrival`).

**Plants** (each applied by a script that saved the original and restored it in a `finally`; never `git checkout`/`restore`/`reset`). The run was killed once by the OS for low memory while plant 14 was applied, so its `finally` did not run: the two planted lines in `MembershipRow.razor` were found by grep and restored by hand, and the tree was checked clean of every plant marker before continuing.

| # | Plant | Expected red | Seen |
|---|---|---|---|
| 1 | Admin's `To` gains Admin | Admin-promote case; transcription gate | red: `An_Admin_beyond_its_authority...(member-to-admin)` 1/4; `The_target_rules_agree_with_the_matrix_transcription` |
| 2 | Owner in the Owner's `From` | Owner own-row case; S-2 | red: `The_Owner_changing_its_own_row...`; transcription gate |
| 3 | Owner in the Owner's `To` | second-Owner case | **masked on the wire**: `owner` in a body is `400 role-not-assignable` before the store, so no request can reach the plant; the transcription gate is red. The `2601` mapping to `409` is in the store and unreachable while Owner is in no set |
| 4 | actor clause removed | race 2 | red: `An_Admin_demoted_while_its_change_is_queued...` |
| 5 | Memberships lock removed | race 1 (expected masked on the end state) | red: the deciding assertion (both requests wait on the held lock) fails |
| 6 | the slice reads `ActiveSpaceContext.Role` | Role-read gate | red: `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` |
| 7 | a second writer of the acting authority | writer gate | red: `Only_the_pipeline_sets_the_acting_authority` |
| 8 | the Qualified refusal escapes unrecorded | record assertion | red: all four `An_Admin_beyond_its_authority...` cases (weak form - the endpoint answering a plain `403` - was blocked by MA0182 on the then-unused `QualifiedRefusal`; the strong form drops the pipeline's record) |
| 9 | publish on every attempt and on rollback | AC6 no-publish case | red: `Refusals_not_founds_no_ops_and_replays_publish_nothing` (the same-Role case stayed green: it seeds no refusal) |
| 10 | a static Role cache in the resolver | `REV-ROLE-CHANGED-BY-OWNER`; the no-cache gate | red: `REV-ROLE-CHANGED-BY-OWNER` and `REV-ROLE-CHANGED`, both surfaces. **The no-cache gate stayed green**: it bans `IMemoryCache`/`IDistributedCache`/`HybridCache`, not a static dictionary - recorded, not widened here |
| 11 | `EmailAddress` on another response | contract gate | red: `Only_the_Membership_list_and_the_Invitation_list_carry_an_email_address` |
| 12 | another type reads `Account.EmailAddress` | reader gate | red: `Only_named_types_read_an_Accounts_email_address` |
| 13 | the discriminator carries `@` | the AC3 property case | red: `The_suffix_never_carries_an_at_sign...` |
| 14 | a `disabled` Role control for an Admin | client gate and browser case | red: `No_control_in_the_Membership_section_is_disabled`; `An_Admin_sees_Member_and_Viewer_only...` |
| 15 | the picker pre-checks the current Role | the no-default case | red: `The_Role_picker_opens_in_place_with_no_option_checked...` (a first run showed it green because `Yello.Tests.Client` does not build `Yello.Client`; rerun with the client rebuilt) |
| 16 | the list unpaged | the 100-row case | red: `At_exactly_100_Memberships...` |
| 17 | the narration region rendered filled | ledger `:550` case | red: `The_Space_home_narration_region_is_inserted_empty...` |
| 18 | the in-flight status lines without focus | ledger `:762` cases | red: all four rows (membership, invitation, label, api-token) |
| 19 | BLOCK AFTER UPDATE omitted from the migration | the schema tests | red: `Membership_carries_a_BLOCK_AFTER_UPDATE_predicate_on_its_Space`; `Every_Space_scoped_table_carries_a_filter_and_a_block_predicate` |

**Timing.** No row added to `IsolationTimingTests`: the in-Space not-found is the boundary `404` body and passes through `NotAvailableFloor`, which `ISO-TIMING-IN-SPACE` already proves. The Isolation project ran green (331/331) with both load-sensitive rows.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-11-02). Five open questions (Q1-Q5) are marked [LEE] and must be decided before development; Q6-Q19 are the dev phase's, with recommendations.
- **Implemented** FR-13 (request path), AR-12/AD-5, AR-16/AD-9, UX-DR14, UX-DR34, NFR-2 (V-1) and the client ledger items `:546`, `:550`, `:762`. Lee's Q1-Q5 (a) as decided: the pipeline-owned acting authority (`RoleCapabilityMatrix.RoleChangeAuthorityOf` -> `ActingAuthority`, set only by `ActiveSpaceResolution`, composed by `MembershipStore` into one guarded `UPDATE` under `Yello/Memberships/{spaceId}` with the actor clause; `QualifiedRefusal` -> the pipeline's recorded `403`); "at least one Owner" by the guarded statement plus S-2 (dated AD-5 note); `BLOCK AFTER UPDATE` on `Membership` (migration `20261102085905_MembershipBlockAfterUpdate`, `TablesThatCannotMove` updated); the list under `ChangeMembershipRole` with Token parity and the two address gates; a live `Account.DisplayName` read in Membership management only. Neither Role-read gate was widened. Dated spine notes on AD-1, AD-2, AD-5, AD-9.

**Reversible decisions (Q6-Q19), each a line that can be cut:**

- Q6 (a): no remove control; the row leaves room for 4.5's. Cost: none.
- Q7 (a): `IMembershipChanges` (Application) collected by a scoped `MembershipChangePublisher`, published after commit by both units of work through `CommitHooks`, one `MembershipChangedPublished` log line (EventId 1047) per publish, no production subscriber. Cost: a hook on two units of work.
- Q8 (a): 50 a page, keyset `(CreatedAt, Id)`, a separate `COUNT` for the total, an opaque versioned cursor (`MembershipCursorText`), "Show more" appends. Cost: two queries a page.
- Q9 (a): `GET .../memberships?after=` -> `MembershipPageResponse`; `PATCH .../memberships/{id}` `{ role }` -> `200 MembershipResponse`. Cost: none.
- Q10 (a): `409 owner-role-fixed` for the Owner's own row, `400 role-not-assignable` before any read for `owner` or an unknown code (one shared Domain parser, `AssignableRoles`, now also used by the Invitation issue), `403` for an Admin on the Owner's row; a `2601` from the store maps to `409`. Cost: two problem types.
- Q11 (a): the same Role is a `200` no-op with the unchanged row, nothing written or published - **but only within the actor's authority**: an Admin "changing" another Admin to Admin is `403`, because the authority check precedes the no-op. Cost: none.
- Q12 (a): in-row "Change Role" opens a fieldset radio group with nothing checked, the current Role in the legend, then "Change"/"Cancel"; "Change" with nothing chosen says "Choose a Role." Cost: none.
- Q13 (a): first four hex characters of SHA-256(MembershipId), upper case, in `Yello.Contracts.Memberships.MembershipLabels` (no gate forbids logic in Contracts; `LabelColours` is precedent). Two colliding names whose suffixes also collide (1 in 65,536 per pair) are not separated further. Cost: that edge.
- Q14 (a): the in-flight status line takes focus in all four rows; `ApiTokenRow` gained a `Revoking` state to have one. Cost: none.
- Q15 (a), adapted: the region could not be filled in the same component's first after-render (it was inserted and filled in one browser task), so `SpaceHome` renders it outside `SpaceArrival`, only when it holds a sentence, and fills it once the Space has answered (`AfterArrival`). Cost: the sentence sits above the page heading.
- Q16 (a): a Label delete or a Membership change answered `404` re-reads its list; drops the row and announces if the list answers, raises the Space refusal surface if not. Task and Project deletion unchanged. Cost: one extra read on a rare path.
- Q17 (a): new `Yello/Memberships/{spaceId}` lock, independent of the Invitations lock (race 3 green). Cost: 4.5 and 5.2 must take it (ledgered).
- Q18 (a): the accept records `MembershipChanged` for the new Member; `:769` updated. Cost: an accept's publish line in logs.
- Q19 (a): the copy as listed, except "{name} is now a {Role}." is three keys ("an Admin", "a Member", "a Viewer") for the article. Cost: three keys instead of one.
- Also decided: a `403` on the change asks the Role again through a new `SpaceArrivalContext.Reread` (the page's narrated exit follows a lower Role); the acting authority's `HeldBy` set is the Roles whose own authority covers it (Owner for the Owner's; Owner or Admin for the Admin's), so the store never receives the actor's Role. Cost: one more record member on `SpaceArrivalContext`.

**Ledger** (`deferred-work.md`): `:329` UPDATED (removal half stays 4.5), `:540` UPDATED (owners 5.1, 6.3), `:546` CLOSED, `:550` CLOSED, `:758` UPDATED (removal arm stays 4.5), `:762` CLOSED, `:769` UPDATED (7.7 subscribes), `:701` UPDATED (seam exists, 7.7). New: the lock and seam constraint for 4.5 and 5.2; C3 (an Admin can create an Admin by invitation) for Lee/PM; AR-12's "at least one Owner" half for Lee.

### File List

New:

- Yello.Domain/Memberships/RoleChangeAuthority.cs
- Yello.Domain/Memberships/AssignableRoles.cs
- Yello.Contracts/Memberships/MembershipRoutes.cs
- Yello.Contracts/Memberships/MembershipResponse.cs
- Yello.Contracts/Memberships/MembershipPageResponse.cs
- Yello.Contracts/Memberships/ChangeMembershipRoleRequest.cs
- Yello.Contracts/Memberships/MembershipLabels.cs
- Yello.Application/Memberships/ActingAuthority.cs
- Yello.Application/Memberships/IMembershipStore.cs
- Yello.Application/Memberships/IMembershipChanges.cs
- Yello.Application/Memberships/ListMemberships/ListMembershipsHandler.cs
- Yello.Application/Memberships/ListMemberships/MembershipCursorText.cs
- Yello.Application/Memberships/ChangeMembershipRole/ChangeMembershipRoleHandler.cs
- Yello.Infrastructure/Persistence/MembershipStore.cs
- Yello.Infrastructure/Persistence/CommitHooks.cs
- Yello.Infrastructure/Memberships/MembershipChangePublisher.cs
- Yello.Infrastructure/Memberships/MembershipLog.cs
- Yello.Infrastructure/Persistence/Migrations/20261102085905_MembershipBlockAfterUpdate.cs
- Yello.Infrastructure/Persistence/Migrations/20261102085905_MembershipBlockAfterUpdate.Designer.cs
- Yello.Host/Endpoints/MembershipEndpoints.cs
- Yello.Host/Spaces/ActiveSpaceResolution.Authority.cs
- Yello.Client/Components/MembershipSection.razor
- Yello.Client/Components/MembershipRow.razor
- Yello.Client/Components/AfterArrival.razor
- Yello.Client/Spaces/MembershipCapabilities.cs
- tests/Yello.Tests.Slices/Memberships/MembershipHandlerTests.cs

Modified:

- Yello.Domain/Memberships/RoleCapabilityMatrix.cs
- Yello.Domain/Memberships/Capability.cs
- Yello.Domain/Memberships/CapabilityDecision.cs
- Yello.Domain/Invitations/Invitation.cs
- Yello.Contracts/ProblemTypes.cs
- Yello.Infrastructure/Persistence/ApplicationLocks.cs
- Yello.Infrastructure/Persistence/InvitationScope.cs
- Yello.Infrastructure/Persistence/InvitationStore.cs
- Yello.Infrastructure/Persistence/MembershipActiveSpaceResolver.cs
- Yello.Infrastructure/SpaceContentStores.cs
- Yello.Host/Endpoints/InvitationEndpoints.cs
- Yello.Host/Program.cs
- Yello.Host/Spaces/ActiveSpaceResolution.cs
- Yello.Client/Components/ApiTokenRow.razor
- Yello.Client/Components/BoardView.razor.cs
- Yello.Client/Components/InvitationRow.razor
- Yello.Client/Components/LabelRow.razor
- Yello.Client/Components/LabelSection.razor
- Yello.Client/Components/SpaceArrival.razor
- Yello.Client/Localisation/ClientCopy.resx
- Yello.Client/Pages/AccountSettings.razor
- Yello.Client/Pages/SpaceHome.razor
- Yello.Client/Pages/SpaceSettings.razor
- Yello.Client/Refusals/RefusalCopy.cs
- Yello.Client/Spaces/SpaceArrivalContext.cs
- Yello.Client/Spaces/SpaceCapabilities.cs
- Yello.Client/wwwroot/css/components.css
- tests/TESTING-CONVENTIONS.md
- tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Memberships.cs
- tests/Yello.Tests.Client/FakeYelloApi.Memberships.cs
- tests/Yello.Tests.Client/MembershipSectionTests.cs
- tests/Yello.Tests.Client/TextSpacingTests.cs
- tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs
- tests/Yello.Tests.Revocation/RevocationCases.cs
- tests/Yello.Tests.Shared/MembershipWire.cs
- tests/Yello.Tests.Slices/Accounts/RegisterAccount/SpaceIsolationSchemaTests.cs
- tests/Yello.Tests.Slices/Memberships/MembershipLabelsTests.cs
- tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ChangeMembershipRole.cs
- tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ChangeMembershipRoleRace.cs
- tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ListMemberships.cs
- tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RevokeInvitation.cs
- tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs
- tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceUnitOfWorkTests.cs
- _bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md
- _bmad-output/implementation-artifacts/deferred-work.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/4-4-manage-memberships-and-roles.md

### Change Log

- 2026-11-02 - Story 4.4 implemented (bmad-dev-story): the Membership list and the Role change on both surfaces, the pipeline-owned acting authority, `BLOCK AFTER UPDATE` on `Membership`, the in-process `MembershipChanged` publisher, the Members section of Space settings, and the three client ledger items. 50 ATDD scaffolds activated, 11 isolation rows and the revocation row made live, 19 plants run. Build 0 warnings, 0 errors; full run (`--max-parallel-test-modules 1`) 2,180 total, 2,170 passed, 0 failed, 10 skipped (the 10 pre-existing skips, three of them the migration-bundle cases; none of the 50 4.4 scaffolds remains skipped).
