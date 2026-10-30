---
baseline_commit: 364d4c3
---

# Story 3.1: Create and rename a Space

Status: done

Epic: 3 — Several Spaces, cleanly separated
Story key: `3-1-create-and-rename-a-space`

**Requirements owned.** These come from the Story Coverage Index (`epics.md:365`: "3.1 | **FR-5, FR-6**, AR-29, AR-30, UX-DR33").

- **FR-5, Create a Space** (`prd.md:184-192`). An authenticated Account creates more Spaces. The creator becomes the Owner. The new Space has exactly one Membership, the default Status set (FR-24: Todo / In Progress / Done) and no Projects. Creation has no effect on, and no visibility from, any other Space.
- **FR-6, Rename a Space** (`prd.md:194-200`). An Owner or Admin renames. A Member or Viewer cannot. Names are not unique across Yello, and a collision discloses nothing.
- **AR-29 / AD-24** (`ARCHITECTURE-SPINE.md:218-222`). There are exactly two Account-scoped surfaces. The Spaces-per-Account count is a third read that AD-24's letter does not permit. `EXPERIENCE.md:183-191` has already decided that it is **permitted**, and that it "needs naming in AD-24". This story records that amendment (AC7).
- **AR-30 / AD-25 / NFR-8** (`ARCHITECTURE-SPINE.md:224-228`). The bound is 50 Spaces per Account. It is declared in one place (`BoundRegistry`, already there). The **pipeline** checks it, not the slice, inside the transaction of the creation it refuses, with a machine-readable reason.
- **UX-DR33** (`epics.md:277`). Every Space name input has `autocomplete="off"` and a non-reusable field name.

**Also binding:**

- **UX-DR9, UX-DR28.** The context bar is always present. The Role chip is display-only and never a link to Space settings (`EXPERIENCE.md:202`).
- **UX-DR12, UX-DR26.** The only button variants are primary, secondary and danger. There is no disabled state for a Role, and no modal deeper than one level.
- **UX-DR27.** A capability that a Role lacks is **absent, not disabled**.
- **UX-DR29.** An Admin demoted while in Space settings is told so first, then routed to Space home. That code already exists (`SpaceSettings.razor`) and must keep working.
- **UX-DR31.** No Space-scoped content goes into Web Storage. The client never issues a Space-scoped request for a Space id that is absent from the current switcher answer.
- **UX-DR35.** The no-Space state offers to create a Space and never creates one itself. A bound is stated plainly.
- **UX-DR37.** Copy is terse and lives in resources.
- **UX-DR39.** A switch moves focus to the context bar and announces the new Space and Role politely.
- **UX-DR40, UX-DR41, UX-DR42.** Text spacing and text-only zoom, reflow at 320px, and logical properties throughout.
- **AR-3.** No slice re-implements authorisation, Space resolution, refusal recording, idempotency or a bound check.
- **AR-10, AR-11.** A 404 at the boundary, a 403 within the Space, and an `AccessRefusal` written by the pipeline.
- **AR-25 / AD-18.** Idempotency (Q2).
- **AR-34.** `SequentialGuid` ids, `DateTimeOffset` UTC, RFC 9457 problems with a stable `type`, and logs carrying `SpaceId` and **never the Space name**.
- **AR-35, AR-36.** Testcontainers SQL Server, never in-memory. Migrations carry the RLS and are never applied at startup.
- **§6.1.** No surface aggregates across Spaces. The count is the caller's own and is never returned as data, only as the bound's `limit` in a refusal.

**Explicitly not in this story:**

- **Deleting a Space and belonging to no Space by deletion: story 3.3.** 3.1 only *offers creation* in the no-Space state that already exists.
- **The purge-on-switch rules (story 3.2) and the one-Account-two-Spaces isolation extension (story 3.4).** 3.1 must not break what 1.7 built, but it owns none of those cases.
- **Membership, Invitation and ownership sections of Space settings: 4.2, 4.4, 5.1.** Space default Statuses: 6.3.
- **Pushing a rename live** to other open tabs or other Members (Epic 7 sync). They see the new name on their next arrival or switcher refresh.
- **No new NuGet package and no new project** (CLAUDE.md). **No Azure deployment, login or push.**

**Ledger entries owned or touched (`deferred-work.md`):**

- `:332` — `autocomplete="off"` plus a non-reusable name on every Space name input, and a gate that finds its own input before asserting. **Owned; close it.**
- `:334` — the no-Space state offers nothing to do. **Owned; close it** (Task 8).
- `:461` — Account-scoped state-changing routes are outside `Idempotency-Key`. **Owned; decide by Q2 and close it.**
- `:95-101` — `Space` and `Account` carry no RLS policy, pending the AD-24 amendment. **Owned by the amendment this story records.** Decide by Q5 and close it or re-own it explicitly.
- `:526` — "Space settings holds only Labels", which wrongly names **5.1** for "rename the Space". Rename is FR-6 and is **3.1's**. Correct the owner list and record that the rename section has landed.
- `:528` — the Space settings link beside an empty Space's one action (owner UX). **Touched.** Q4's recommendation moves the link to the context bar, which is one of the three options UX listed there. Record the disposition, and leave the final call with UX if Q4 picks anything else.
- `:211` — 4.3 cannot write a second Space's Membership on one unit of work. **Touched.** Q1's mechanism (two keys on one named unit of work) is the precedent 4.3 will meet. Note it there.
- `:478` — two concurrent creates with the same key at one below the bound (re-owned to 8.3). Touched only if Q2 (a) adds an Account-scoped idempotency record. Note it if so.

**Code hooks that name 3.1:**

- `Yello.Domain/Bounds/BoundRegistry.cs:18` ("Spaces per Account by 3.1").
- `Yello.Infrastructure/Persistence/BoundCounter.cs:43` ("Each later bound (Spaces per Account, 3.1 ...) adds its own count here").
- `Yello.Application/Accounts/RegisterAccount/RegisterAccountHandler.cs:35-36`.
- `Yello.Client/Localisation/ClientCopy.resx:447` (`NoSpaceBody` comment).
- `Yello.Client/Pages/Home.razor` (the no-Space comment).
- `tests/Yello.Tests.Shared/WireDatabase.cs` (`AddOwnedSpaceAsync`, commented "no endpoint creates a Space yet").

**Depends on** (all done):

- **1.3:** registration seeds a Space.
- **1.5:** the pipeline.
- **1.6:** boundary, refusals and the bound registry.
- **1.7:** the switcher, `AccountScopedContext` and the `AccountId` key.
- **1.8:** Account settings and the Token surface.
- **1.9:** isolation gates.
- **2.1:** the bound pipeline step, the Projects create and rename pattern, and idempotency.
- **2.4:** the Space settings page.
- **2.9:** `ApplicationLocks`.
- **2.11:** context bar breakpoints.

> **Isolation gates.** 3.1 maps two new `/api` routes, so 1.9-GATE-002 requires a row naming each, with an expectation on **both** surfaces (Session and ApiToken). If any row is added as pending, GATE-003 requires it to be live before `review`.

## Story

As a freelancer whose client work should not sit beside my personal work,
I want to create more Spaces and give them names that mean something,
So that separate bodies of work are separate by construction rather than by convention.

## Acceptance Criteria

AC1 to AC8 are reproduced **verbatim** from `epics.md:1444-1477` (`status: final`) and numbered by position. AC9 to AC16 come from the PRD, the spines, the ledger and the code 1.x and 2.x built, and each cites its source.

**AC1 — creation** (FR-5)
**Given** an authenticated Account
**When** it creates a Space
**Then** that Account becomes the Owner, the Space holds exactly one Membership, and it carries the default Status set and no Projects

**AC2 — no effect elsewhere** (FR-5)
**Given** a newly created Space
**When** every other Space is inspected
**Then** the creation has no effect on, and no visibility from, any of them

**AC3 — rename** (FR-6)
**Given** a Space
**When** an Owner or Admin renames it
**Then** the new name is used, and a Member or Viewer has no rename affordance present

**AC4 — Space settings** (UX-DR9, UX-DR27; Q4)
**Given** Space settings
**When** it is opened
**Then** it is reachable from the context bar and available to Owner and Admin only
**And** the controls an Admin lacks are absent rather than disabled

**AC5 — names are not unique** (FR-6)
**Given** two Accounts each owning a Space with an identical name
**When** either is read
**Then** there is no collision and no disclosure, because Space names are not unique across Yello

**AC6 — the 50-Space bound** (NFR-8, AD-25; Q1)
**Given** an Account already holding Membership in 50 Spaces
**When** it attempts to create a 51st
**Then** it is refused inside the same transaction with a machine-readable reason, and the copy states the bound plainly

**AC7 — the AD-24 amendment** (AR-29; Q1, Q5)
**Given** that bound check reads the acting Account's own Space count on a surface which is neither the Space switcher nor Account settings
**When** AD-24 is consulted
**Then** the amendment naming this read is recorded rather than left implicit
**And** the reason is stated: the count is the caller's own and discloses no other Account's data, but AD-24 exists precisely so that an unauthorised read is never solved by disabling row-level security, opening a second connection, or inventing a bypass that then spreads

**AC8 — the Space name input** (UX-DR33)
**Given** the Space name input
**When** it renders
**Then** it carries `autocomplete="off"` and a non-reusable field name
**And** the reason is stated: autofill is origin-scoped and Account-agnostic, so it would otherwise offer one Account's Space name to the next Account using that browser profile

**AC9 — the wire contract** (AR-26, AR-34, NFR-5; Q6, Q9)

- `POST /api/v1/spaces` with `CreateSpaceRequest(string? Name)` answers `201`. It carries a `Location` of the new Space's `SpaceRoutes.ActiveOf(id)` and a body of `SpaceIdentityResponse(Id, Name)`.
- `PATCH /api/v1/spaces/{spaceId}` with `RenameSpaceRequest(string? Name)` answers `200` with `ActiveSpaceResponse(Id, Name, Role)`.
- A name that is empty after trimming, or longer than `SpaceName.MaxLength`, is refused `400` with the new stable problem type `InvalidSpaceName`. It is never a `500`, and never truncated silently.
- The 51st creation is refused `409 urn:yello:problem:bound-reached` with `bound: "spaces-per-account"` and `limit: 50`.

**AC10 — authority on both routes** (AR-10, AR-11, FR-16, CAP-35)

- `POST /api/v1/spaces` without a Session is refused `401`, and nothing is recorded. An API Token is no credential on it (`NoCredential`, like `ISO-LIST-SPACES`).
- `PATCH /api/v1/spaces/{spaceId}`:
  - by a non-Member: `404 not-available`, with an `AccessRefusal` of kind `CrossSpace`;
  - by a Member or a Viewer: `403` with `capability: "rename-space"`, and an `AccessRefusal` of kind `InsufficientRole`;
  - by an Owner or an Admin: `200`.
- An API Token reaches rename **only for its own Space**, at the Token's current Role.
- The pipeline decides each of these; the slice decides none of them.

**AC11 — creation is atomic and serialised** (AD-25 "inside the same transaction"; `BoundCounter` precedent)

- The count, the Space, the Owner Membership and the three `StatusDefinition` rows commit together or not at all.
- Two concurrent creations by one Account holding 49 Memberships end with exactly one `201` and one `409`. The test makes them overlap deterministically, by waiting on the lock rather than on time.
- A creation by another Account never waits on this Account's lock.

**AC12 — what counts toward the bound** (NFR-8: "Spaces per Account")

- Every Membership the Account holds counts, at any Role, owned or not.
- Registration's Personal Space counts.
- A Space the Account has left or that was deleted does not count. There is nothing to write for this in 3.1, but the count must be a live `COUNT`, never a stored counter.

**AC13 — after creating** (UX-DR31, UX-DR39; Q3)

- The person lands in the new Space's home. The arrival is a switch, so focus moves to the context bar and the new Space and Role ("Owner") are announced politely.
- The empty Space shows "No Projects yet." and its one action.
- The switcher lists the new Space by name and nothing else.
- The client refreshes the directory before it navigates, so the new id passes the `Lists(spaceId)` gate (UX-DR31).

**AC14 — after renaming** (FR-6; Q7)

- The context bar's visible name and its accessible name show the new name without a reload.
- The switcher's next opening lists the new name.
- A success announcement is made politely.
- `document.title` stays the fixed `Yello` (UX-DR33).
- No log line, problem body or `AccessRefusal` carries the old or the new name (AR-34, I-17).

**AC15 — the no-Space state offers creation** (UX-DR35, FR-7 "offered the chance to create a Space"; `deferred-work.md:334`)

- An Account with no Membership sees "No Spaces yet", its sentence, and **one** action to create a Space.
- Nothing is ever created for it.
- A refusal of that creation renders in place.

**AC16 — accessibility of the new surfaces** (NFR-9, UX-DR40, UX-DR41)

- The create form and the rename section each have a real `<label for>`, focus visible, and Tab in reading order. Esc closes an open inline form and returns focus to its opener.
- Each passes the 1.4.12 text-spacing override and 200% text-only zoom at 320px with no clipping and no sideways page scroll, using a 100-character name.
- The context bar keeps the Role chip at every breakpoint with the new Space settings link present (2.11 AC16 stays green).

## Tasks / Subtasks

- [x] **Task 1: decide the open questions** (all ACs)
  - [x] Read Dev Notes → Open questions. Decide each as a reversible decision and record it in Completion Notes as "Qn - (x)", with its cost. Each recommendation is the default.
- [x] **Task 2: record the AD-24 amendment** (AC7; Q1, Q5)
  - [x] Edit `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md` AD-24 (`:218-222`). Append a dated **Amendment (2026-10-29, story 3.1)** bullet that does five things:
    - Names the third Account-scoped unit of work: **Space creation**, which reads the acting Account's own Membership count for AD-25's Spaces-per-Account bound, and writes the new Space under its own server-generated `SpaceId`.
    - Names the second permitted read now as well: delete-Account-refused listing every owned Space (FR-3, story 5.4). Its implementation stays 5.4's.
    - States the reason in AC7's words.
    - States the mechanism Q1 chose. Write this as a rule, so a later reader cannot generalise it.
    - States Q5's decision on `Space` and `Account` policies.
  - [x] Do not rewrite AD-24's rule text or AD-26's "AD-24 stands unamended" (correct for its own subject). The amendment is additive.
  - [x] Add the same note, in one line, to `EXPERIENCE.md:190`'s table row ("Recorded in AD-24, 2026-10-29").
  - [x] Note it in `_bmad-output/specs/spec-yello/SPEC.md` only if that file restates AD-24's two surfaces. Grep for it first, and record what you found.
- [x] **Task 3: the domain** (AC1, AC5, AC9; Q6)
  - [x] Add `Yello.Domain/Spaces/SpaceName.cs`. Copy the shape of `Projects/ProjectName.cs`: `TryNormalise(string? submitted, out string name)`, which trims, `MaxLength`, and no uniqueness.
    - Use **200** as the maximum (Q6), because a provisioned name can already be up to 128 + `"'s Space"` characters.
    - `SpaceConfiguration.NameMaxLength` should reference `SpaceName.MaxLength` (or assert it is equal), so there is one source of truth.
  - [x] Extract the Space-seeding block from `RegisterAccountHandler` (Space, Owner Membership, `DefaultStatusSet` rows, one clock read) into one Domain or Application helper, for example `NewSpace.Provision(spaceId, accountId, name, createdAt, identifiers)`. Use it from both registration and creation. **Registration's behaviour and its tests must not change** (AD-22: still exactly one slice creates an Account).
- [x] **Task 4: Space creation through the pipeline** (AC1, AC2, AC6, AC7, AC10-AC12; Q1, Q2)
  - [x] Contracts:
    - `Yello.Contracts/Spaces/SpaceRoutes.cs`: create shares the `Directory` path with POST; rename shares `Active` with PATCH. Keep route strings as constants.
    - Add `CreateSpaceRequest(string? Name)` and `RenameSpaceRequest(string? Name)`.
    - Add `ProblemTypes.InvalidSpaceName`.
  - [x] Pipeline (Q1 (a)): give Account-scoped creation its own named step beside `AccountScopedResolution`. Do not put it in the slice. The step:
    1. opens the unit of work's transaction;
    2. sets **both** keys through `SpaceSessionContext`: `AccountId` from the Session, and `SpaceId` set to a **server-generated** new id (`IIdentifierGenerator`, never the body);
    3. narrows `AccountScope` and `SpaceScope`;
    4. consults the declared bound (`.ConsultsBound(Bound.SpacesPerAccount)`) through `BoundRegistry` and `IBoundCounter`;
    5. runs the endpoint;
    6. commits on success, and rolls back on any `>= 400`.
    - Mirror `ActiveSpaceResolution`'s commit and rollback discipline. Log the refusal with `SpaceLog.BoundReachedFor`, keyed by Account and never by name. It writes **no** `AccessRefusal`: a bound refusal is neither kind (`ActiveSpaceResolution.Bound.cs:59-60`).
  - [x] `BoundCounter`: add `Bound.SpacesPerAccount`.
    - First take `ApplicationLocks` `"Yello/SpacesPerAccount/{accountId}"` (Exclusive, Transaction owner; add `TakeSpacesPerAccountAsync` beside the existing two).
    - Then run `SELECT COUNT(*) FROM dbo.[Membership] WHERE AccountId = @accountId`. Under the two-key unit of work, RLS admits the Account's own rows plus the new Space's rows, of which there are none. The explicit `WHERE` keeps the count the caller's own even so.
    - Replace the "No creation endpoint consults this bound" message accordingly.
  - [x] Add the slice `Yello.Application/Spaces/CreateSpace/CreateSpaceHandler.cs`. It receives the established context (Account id, new Space id) and the normalised name, provisions through Task 3's helper, and saves on the pipeline's unit of work through a store port, for example `ISpaceCreationStore` in Domain or Application, implemented in Infrastructure. It returns `SpaceIdentity`.
  - [x] Add the endpoint `MapCreateSpace` in `Yello.Host/Endpoints/SpaceDirectoryEndpoints.cs`, or a new `SpaceEndpoints.cs`. It reads the body with `JsonRequestBody.ReadAsync`, validates with `SpaceName.TryNormalise` (`400 InvalidSpaceName`), and answers `201` with a `Location`. Register the handler and store in `Program.cs` and `InfrastructureServices.cs` / `AccountScopedReads.cs` as their neighbours are.
  - [x] Idempotency (Q2 (a)): add an Account-scoped idempotency record, with a migration and an RLS policy on `SESSION_CONTEXT('AccountId')`. Replay it inside the same step, before the bound, so a replay of a created Space at 50 returns the original `201` and not a `409`. Mirror `ActiveSpaceResolution.Idempotency.cs`. If Q2 picks (b), write the stated-out reason in Completion Notes and make sure the client never re-sends a create without its original key.
- [x] **Task 5: rename** (AC3, AC9, AC10, AC14; Q7)
  - [x] Map `MapRenameSpace` as a PATCH on `SpaceRoutes.Active` with `.RequiresCapability(Capability.RenameSpace)`. That capability already exists in `RoleCapabilityMatrix`: Owner and Admin yes, Member and Viewer no. Read the body, validate it with `SpaceName.TryNormalise`, and call the slice.
  - [x] Add `Yello.Application/Spaces/RenameSpace/RenameSpaceHandler.cs`. It takes `ActiveSpaceContext` and the name. The store runs `ExecuteUpdateAsync` on `Space` **`WHERE Id = activeSpace.SpaceId`**, the resolved Space and never an id from the body.
    - `Space` has no RLS policy (Q5), so this `WHERE` is the only thing that scopes the write. A gate must hold it (Task 9).
    - Answer with `ActiveSpaceResponse` carrying the caller's Role.
- [x] **Task 6: the client - create** (AC8, AC13, AC15, AC16; Q3)
  - [x] Add a page `Yello.Client/Pages/CreateSpace.razor` at `ClientRoutes.CreateSpace` (for example `/spaces/new`).
    - The route must never be matched by `/spaces/{id:guid}`, and `ClientRoutes.SpaceIn` must return `null` for it, so arriving there purges the departed Space as any non-Space route does.
    - It is Account-scoped: no `SpaceArrival` and no Space token.
    - It holds `<PageHeading>`, a `<FormField>` with `FieldName` set to a fresh `Guid.NewGuid().ToString("N")` (AutoComplete defaults to `"off"`) and `FocusOnFirstRender`, a `PrimaryButton`, and Cancel as `button-secondary`.
    - Refusals render beside the field (`InvalidSpaceName`) or in the form's status region (`bound-reached` through `RefusalText.Of`, which already resolves `spaces-per-account`).
    - Not optimistic. Reuse the key after an unanswered attempt (the `UnansweredCreate` precedent in `ProjectList.razor`).
    - On `201`: `await Directory.RefreshAsync(...)`, then `Navigation.NavigateTo(ClientRoutes.SpaceHome(id))`.
  - [x] Entry points (Q3 (a)): a "Create a Space" link at the foot of the switcher panel, **outside** its `<ul>`, so the switcher's rows still carry a name and nothing else. And the same action on `Home.razor`'s no-Space state.
  - [x] Writes: `SpaceWrites` is documented as about the active Space. Add a small sibling for Account-scoped writes, or a method that takes no Space token. Record which. Either way it uses the same `HttpClient`, anti-forgery and `ProblemOfAsync` path.
- [x] **Task 7: the client - rename and Space settings** (AC3, AC4, AC8, AC14, AC16; Q4, Q7)
  - [x] Add `SpaceCapabilities.PermitsRenameSpace(role)` (`owner|admin`) in `Yello.Client/Spaces/`.
    - Add the gate `The_clients_rename_predicate_agrees_with_the_capability_matrix`, beside the Label one in `RefusalBoundaryGateTests`.
    - Add a `PermitsSpaceSettings(role)` that is the union of the sections' predicates, so the link's audience is defined in one place.
  - [x] Add a "Space name" section to `Pages/SpaceSettings.razor`, rendered only when `PermitsRenameSpace`.
    - Follow `ProjectRow.razor:36-64` and `ProjectList.RenameAsync:474-550`: a "Rename" `button-secondary` opens an inline form with `FormField` (non-reusable `FieldName`, `autocomplete="off"`), submit and Cancel.
    - Keep the form open until the server answers, then close it, announce the rename (`LiveAnnouncement`), and update `ActiveSpace` with the answer (Q7: an `Arrive`-shaped update that does not count as a switch, or a new `Renamed(ActiveSpaceResponse)` method). This makes the context bar re-render.
    - Esc closes the form and returns focus to "Rename".
  - [x] Add a "Space settings" link to `Components/ContextBar.razor` (Q4), present only when `PermitsSpaceSettings(role)` and absent otherwise.
    - Never put it on the Role chip.
    - Copy comes from `SpaceSettingsLink`; update its `<comment>`.
    - Remove the Space home link, or keep it, per Q4. Then update `SpaceHomeProjectsTests.An_empty_Space_states_no_Projects_yet_and_offers_one_action_to_create_one`'s set-aside and `LabelTests.A_Member_has_no_Label_definition_affordance` accordingly.
    - The 320px context bar must still keep the Role chip, with no sideways scroll (2.11 AC16). Add CSS only in `components.css`, using logical properties and rem/em, with no px literal.
- [x] **Task 8: copy** (AC6, AC13-AC15; UX-DR37)
  - [x] Add these keys to `ClientCopy.resx`, each with a `<comment>` naming 3.1 and its AC:
    - `CreateSpaceAction` ("Create a Space");
    - the create page heading;
    - `SpaceNameLabel` ("Space name");
    - an invalid-name message stating the rule ("A Space name is 1 to 200 characters." or the like);
    - `SpaceCreated` / `SpaceRenamed` announcements, which must carry **no Space name** (Q8);
    - `RenameSpaceAction` ("Rename").
  - [x] Change `NoSpaceBody`'s comment, which currently says "arrives with story 3.1". `spaces-per-account` and `rename-space` exist already; reuse them.
  - [x] Add `InvalidSpaceName` to `RefusalCopy.OwnKeys`, which keeps `RefusalCopyTests` green.
- [x] **Task 9: gates** (each proved against a plant; save the original content and restore it in a `finally`, **never `git checkout`/`restore`/`reset`**)
  - [x] `SpaceSwitcherGateTests.The_Account_scoped_surfaces_are_exactly_the_two_AD_24_names`, `Every_Account_scoped_endpoint_names_an_AD_24_surface`, `No_caller_sets_both_session_context_keys` and the `.Isolation.cs` allow-lists:
    - amend each to admit **exactly** the one named creation step or surface Q1 chose;
    - make every assertion cite the AD-24 amendment;
    - make sure a second both-keys caller still fails.
  - [x] `SpaceResolutionSeamTests.Exactly_one_production_line_sets_a_session_context_key` must stay green. Both keys still go through `SpaceSessionContext.SetKeyAsync`.
  - [x] `RefusalBoundaryGateTests.No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity`: add the creation route as the second named exemption beside `/api/v1/accounts`, with its reason.
  - [x] `Space_is_read_only_where_named`: name the creation and rename stores. Add a gate that every `Space` **write** filters on the resolved context's `SpaceId`, or name the one rename store as the only `ExecuteUpdate` on `Space`.
  - [x] The bound registry: a gate that `Bound.SpacesPerAccount` is consulted by exactly one endpoint, the creation route. Follow the existing per-bound gates.
  - [x] Space name inputs (AC8, `deferred-work.md:332`): extend the client's name-input gate (`SpaceHomeProjectsTests.AssertNameInputsAreNotAutofilledAsync` / `ReusableFieldNames`), or add a static gate over `*.razor`. It must **first find** the create input and the rename input, then assert `autocomplete="off"` and a non-reusable `name` on each. A gate over zero inputs fails.
  - [x] Logs: if `SpaceLog` gains a creation or rename event, it carries `SpaceId` and never the name. Extend the existing log-content gate.
- [x] **Task 10: tests** (see Dev Notes → Testing requirements)
- [x] **Task 11: ledger, conventions, verification**
  - [x] `deferred-work.md`: apply Dev Notes → Ledger dispositions.
  - [x] `tests/TESTING-CONVENTIONS.md`: add a short "Account-scoped creation - added by story 3.1" note. It covers how to seed N Memberships for the bound (`WireDatabase.AddOwnedSpaceAsync`, which should now seed default Statuses too, or say why not), and how to make two creations overlap on the Account's application lock.
  - [x] Run `dotnet build Yello.slnx` (0 warnings, 0 errors), then `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Record the totals. The expected skips are the 3 bundle cases plus the Explicit tiers. **Build `Yello.Client` in Debug before browser plants.**

### Review Findings

Code review 2026-10-30 (bmad-code-review, full mode, range `f3da467..HEAD`). All 6 patches applied; D1 decided by Lee. 39 raw findings from three layers (Blind Hunter 17, Edge Case Hunter 10, Acceptance Auditor 12), 26 after dedupe. 1 decision-needed, 6 patch, 4 defer, 15 dismissed.

- [x] [Review][Decision] **Decided by Lee 2026-10-30: (a)**, keep it non-sticky below 30em of viewport height (WCAG 1.4.10 reflow wins at that height), with a `deferred-work.md` entry owned by UX to confirm the UX-DR9 exception. The context bar stops being sticky below 30em of viewport height — `components.css` adds `@media (max-height: 29.9375em) { .context-bar { position: static; } }` so AC16's 320x256 / 200% text-only zoom case can click below the bar. UX-DR9 (`epics.md:247`, `DESIGN.md:427`) says the bar "never scrolls away", and the story lists UX-DR9 as binding. Recorded only as a reversible decision in Completion Notes, with no ledger entry for UX. Options: (a) keep it, with a ledger entry for UX to confirm; (b) keep it sticky and bound the bar's height on short viewports instead; (c) keep it sticky and narrow AC16's short-viewport case.
- [x] [Review][Patch] A 201 whose body cannot be read clears the retry key, and the "will not be created twice" retry then makes a second Space [Yello.Client/Pages/CreateSpace.razor:163]
- [x] [Review][Patch] The retry key is reused only for an ordinally identical untrimmed name, so a whitespace-only edit draws a new key for the same normalised name [Yello.Client/Pages/CreateSpace.razor:198]
- [x] [Review][Patch] `SpaceCreated` (EventId 1038) is logged by the endpoint before the commit, so a creation rolled back (key in flight, failed commit) is logged as created [Yello.Host/Endpoints/SpaceDirectoryEndpoints.cs:103]
- [x] [Review][Patch] Two amended gates do not cite the AD-24 amendment in their assertions (Task 9) [tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.cs]
- [x] [Review][Patch] `The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space`'s summary claims it checks `Update`/`Remove` on a `Space` entity; it scans only `ExecuteUpdate`/`ExecuteDelete` [tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Spaces.cs]
- [x] [Review][Patch] `CreateSpaceCancel`'s comment says Cancel goes "back to where the person came from"; it goes to `ClientRoutes.Home` [Yello.Client/Localisation/ClientCopy.resx:996]
- [x] [Review][Defer] Rename answers 200 when `ExecuteUpdateAsync` touches no row (the Space deleted after resolution) [Yello.Infrastructure/Persistence/SpaceStore.cs:39] — deferred to story 3.3, which introduces Space deletion and so the only way to reach it
- [x] [Review][Defer] "The both-keys unit of work reads nothing but the count" is stated (story regression guard, AD-24 amendment) but not gated; nothing limits what the creation handler or endpoint may read while both keys are set [Yello.Application/Spaces/CreateSpace/CreateSpaceHandler.cs] — deferred to story 4.3, the next both-keys unit of work (`deferred-work.md:211`)
- [x] [Review][Defer] No test drives the path where the Space rows are saved and then rolled back (the key's first use lost on the unique index → 409 in flight) (AC11) [Yello.Host/Spaces/ActiveSpaceResolution.Creation.cs:116] — deferred to story 8.3, which owns concurrent same-key creation (`deferred-work.md:482-485`)
- [x] [Review][Defer] AC16's Tab-order and focus-visible clauses have no browser assertion on the create page or the rename section [tests/Yello.Tests.Client/SpaceCreateAndRenameTests.cs] — deferred to story 3.2, which next changes switcher and context-bar focus

## Dev Notes

### The shape of the story in one paragraph

Rename is easy: it is an ordinary Space-scoped PATCH through the pipeline that exists, gated by a capability that already exists (`Capability.RenameSpace`), plus a section in a page that exists. **Creation is the hard part, and the hard part is the database, not the UI.**

- Creating a Space is the first write without a `{spaceId}`, other than registration, and its bound needs a **cross-Space read**: the Account's Membership count across all its Spaces.
- The inserts need `SESSION_CONTEXT('SpaceId')` equal to the new id, because of the BLOCK AFTER INSERT predicate on `Membership` and `StatusDefinition`.
- The count needs `SESSION_CONTEXT('AccountId')`, the only key under which `Membership` is readable across Spaces.
- Both keys are `@read_only = 1`, so they are immutable for the connection.
- AD-25 wants the count and the insert in **one transaction**.
- Today a gate forbids any caller from setting both keys.

Q1 resolves this, and AC7 makes the resolution an AD-24 amendment rather than a quiet bypass.

### What exists today (survey of the tree at `364d4c3`)

**Domain** (`Yello.Domain`):

- `Spaces/Space.cs`: `Id`, `Name {get;set}`, `CreatedAt`. No `IsPersonal`, no `OwnerId`.
- `SpaceConfiguration.NameMaxLength = 200`. There is no `SpaceName` type.
- `Memberships/Capability.cs` already has `RenameSpace` and `DeleteSpace`. `RoleCapabilityMatrix.Decide`: RenameSpace is `[Owner yes, Admin yes, Member no, Viewer no]`. There is **no** CreateSpace capability. Creation is Account-scoped and needs none.
- `Bounds/BoundRegistry.cs`: `new(Bound.SpacesPerAccount, 50, BoundOperation.CreateSpace)` is already declared.
- `Statuses/DefaultStatusSet.Names = ["Todo","In Progress","Done"]`, stored per Space as `StatusDefinition` rows.
- `Projects/ProjectName.cs`: `TryNormalise` trims and allows 1-100. This is the template for `SpaceName`.
- The longest provisioned Space name is 128 (display name) + `"'s Space"` (`PersonalSpaceName.From`). That is why Q6 says 200, not 100.

**Registration** (`Application/Accounts/RegisterAccount/RegisterAccountHandler.cs`, `Infrastructure/Persistence/AccountRegistrationStore.cs`):

- Builds Space + Owner Membership + statuses from one clock read and generated ids.
- The store opens a transaction, calls `SpaceSessionContext.SetAsync(newSpaceId)`, then inserts and commits.
- It runs on its own unauthenticated route, `/api/v1/accounts`, which is the only exemption in `No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity`.

**Row-level security:**

- `fn_SpaceIsolationPredicate(SpaceId)` applies FILTER and BLOCK AFTER INSERT on `Membership` and `StatusDefinition`, and later on `Project`, `Task`, `Label`, `TaskLabel`, `SpacePresence` and `IdempotencyRecord`.
- `Membership`'s FILTER was swapped (migration `20261024143930_AccountScopedMembership`) to `fn_MembershipVisibilityPredicate(SpaceId, AccountId)`, which admits by the `SpaceId` key **or** the `AccountId` key. Its BLOCK stays Space-only.
- **`Space` and `Account` have no policy and no EF query filter.**
- The EF filter on `Membership` is `SpaceId == ScopedSpaceId || AccountId == ScopedAccountId` (`MembershipConfiguration.cs:51`).

**Keys:**

- `SpaceSessionContext.SetAsync` / `SetAccountAsync` → one private `sp_set_session_context ... @read_only = 1` line.
- Gates:
  - `SpaceResolutionSeamTests.Exactly_one_production_line_sets_a_session_context_key`;
  - `SpaceSwitcherGateTests.No_caller_sets_both_session_context_keys`, where only `AccountSpaceDirectory` calls `SetAccountAsync`;
  - `SessionContextKey` is a closed enum of two.

**The pipeline** (`Yello.Host/ApiRouteGroup.cs`):

- Filter order: `RequireAccountAsync` → `AccountScopedResolution.EstablishAsync` → `AntiforgeryRequirement` → `NotAvailableFloor` → `ActiveSpaceResolution.ResolveAsync`.
- A route is Space-scoped when it contains `{spaceId}`.
- `ActiveSpaceResolution` does the following, in order:
  1. opens the unit of work, sets `SpaceId`, and reads the Membership and Role;
  2. 404 plus `CrossSpace` if there is no Membership;
  3. 403 plus `InsufficientRole` if `Decide` denies;
  4. presence bound, idempotency (`.Idempotency.cs`), `CreationBound.ConsultAsync` (`.Bound.cs`), then the endpoint;
  5. commit, or roll back on `>= 400`.
- Endpoints declare `.RequiresCapability(...)` (exactly one) and `.ConsultsBound(...)`.
- Account-scoped endpoints: `.AccountScoped(AccountScopedSurface.SpaceSwitcher|AccountSettings)`. The filter only builds `AccountScopedContext(accountId)` from `SessionPrincipal`. The unit of work is then opened by `AccountSpaceDirectory.InAccountScopeAsync`, which sets the `AccountId` key and calls `accountScope.Narrow`. Session-only: the Token scheme authenticates nothing without `{spaceId}`.
- `CreationBound` runs only inside `ActiveSpaceResolution`, so **an Account-scoped route consults no bound today**.
- `BoundCounter.CountAsync` throws for `SpacesPerAccount`.

**Bounds and locks:**

- `BoundCounter` takes an `ApplicationLocks` app lock (`sp_getapplock`, Exclusive, Transaction owner) and then a separate `COUNT(*)` statement. READ COMMITTED with READ_COMMITTED_SNAPSHOT on (migration `20261028120526`).
- `BoundaryProblems.BoundReached(entry)` → `409 urn:yello:problem:bound-reached` with `bound` (`StableCode.Of`) and `limit`.
- Precedent test: `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Bound.cs` (the 51st refused; two concurrent at 49 → one 201 and one 409).

**Idempotency:**

- `IdempotencyRecord` is Space-scoped (RLS on `SpaceId`). Its pipeline step is `ActiveSpaceResolution.Idempotency.cs`, on `{spaceId}` routes only.
- `deferred-work.md:461` gives 3.1 the Account-scoped decision.
- The client already reuses a key after an unanswered create (`ProjectList` `UnansweredCreate`).

**Projects, the pattern to copy:**

- No command or validator classes. The endpoint validates (`ProjectName.TryNormalise` → `400 InvalidProjectName`).
- Handler: `CreateProjectHandler.HandleAsync(ActiveSpaceContext, string name, ct)`.
- Store: `ProjectStore`, which adds then saves for create, and `ExecuteUpdateAsync(SetProperty(Name))` for rename.
- Endpoints: `Yello.Host/Endpoints/ProjectEndpoints.cs` (`MapCreateProject` POST 201 + Location, `MapRenameProject` PATCH, `JsonRequestBody.ReadAsync`).
- Registration: `Program.cs:97-119` (handlers) and `:342-437` (maps).
- There is no rowversion anywhere, so the last writer wins (a rename races a rename: last wins, which is acceptable for a name).

**Contracts:**

- `Yello.Contracts/Spaces/SpaceRoutes.cs`: `Directory = "/api/v1/spaces"` (GET), `Active = "/api/v1/spaces/{spaceId:guid}"` (GET → `ActiveSpaceResponse(Id, Name, Role)`), `ActiveOf`, `Statuses`.
- `SpaceIdentityResponse(Id, Name)`.
- `ProblemTypes.cs`, `ProblemResponse(Type, Errors, Capability, Bound, Limit)`.
- There is no create or rename request type yet.

**Client** (`Yello.Client`; root-level projects, not `src/`):

- `Components/ContextBar.razor`: `<nav class="context-bar" tabindex="-1">`, with the switcher trigger (Space name), the Project name, `RoleChip`, an "Account" link and Sign out. Its accessible name is `RoleCopy.Compose(...)`. Shell only before the answer.
- `Components/SpaceSwitcher.razor`: a non-modal disclosure `<ul>` of `<a href=SpaceHome(id)>` with `aria-current`. Esc returns to the trigger. It refreshes on open.
- `Spaces/SpaceDirectory.cs`: `GET /api/v1/spaces`, `Current`, `RefreshAsync`, and `Lists(spaceId)`, the UX-DR31 gate.
- `Spaces/ActiveSpaceState.cs`: `SpaceId`, `Current` (`ActiveSpaceResponse`), `Token`, `Arrive()`, `Purge()`, `Navigating(Guid?)`. **`Arrive()` is the only setter of `Current`**, so a rename needs a way to update the name (Q7).
- `Components/SpaceArrival.razor`: `GET ActiveOf(id)` → `Arrive`, and provides `SpaceArrivalContext(Current, Refuse)`. A navigation to a new Space id counts as a switch (`LastArrivalWasSwitch`), so focus goes to the context bar with the announcement.
- `Pages/SpaceSettings.razor` (story 2.4, `/spaces/{id}/settings`): inside `SpaceArrival`, it renders `<LabelSection>` when `LabelCapabilities.PermitsManageLabels`, else the `manage-labels` capability copy. It narrates a lowered Role and routes to Space home (UX-DR29). Today it is reached **only** from `Pages/SpaceHome.razor:38-41`, which is **not** the context bar (AC4).
- `Pages/Home.razor`: the no-Space state "No Spaces yet" / "You do not belong to any Space yet.", with nothing to do.
- `Pages/AccountSettings.razor`: Account-scoped, inline (API Tokens).
- Primitives:
  - `Components/FormField.razor`: a real label, `AutoComplete` defaults to `"off"`, plus `FieldName`, `Message`/`Invalid` and `FocusOnFirstRender`;
  - `PrimaryButton`, `LiveAnnouncement`, `PageHeading`, `RoleChip`, `RefusalSurface`, `InlineErrorRegion`.
  - **There is no Dialog primitive.** Creates and renames are inline forms, and `TaskDetail` is the only dialog.
- Non-reusable name idiom: `private readonly string _fieldName = Guid.NewGuid().ToString("N");` passed as `FieldName` (ProjectList 143-145, LabelSection 110, TaskCreate 59, TaskDetail 254).
- Writes: `Spaces/SpaceWrites.cs` `SendAsync(method, path, body, key, ActiveSpace.Token)` sets `Idempotency-Key`, re-sends once on `AntiforgeryTokenMissing`, and returns `SpaceWriteAnswer(Response, Problem)`. `Sessions/YelloApiHandler.cs` sends credentials, adds anti-forgery on unsafe methods, and handles 401.
- Refusal copy:
  - `Refusals/RefusalText.Of` / `RefusalCopy.cs`: a 403 resolves by `problem.Capability`, `bound-reached` by `problem.Bound` with `Limit`, own types by `OwnKeys`.
  - `ClientCopy.resx` **already has** `rename-space` ("Only Owners and Admins can rename the Space."), `spaces-per-account` ("This Account has {0} Spaces, the maximum."), `SpaceSettingsLink`, `SpaceSettingsHeading`, `SpaceSettingsNoLongerAvailable`, `NoSpaceHeading`, `NoSpaceBody`.
- Role gating: static predicates in `Spaces/*Capabilities.cs`, each checked against the Domain matrix by a `RefusalBoundaryGateTests` gate.
- CSS: `wwwroot/css/{tokens,base,components}.css`, no scoped `.razor.css`. The `DesignFoundationGateTests*` rules: no colour or px literals, logical properties, no fixed heights, no shadows, no `text-transform`, `--danger` only in `.destructive-confirm`. The `LocalisableAttributes` gate forbids literal `aria-label`, `title` and `placeholder`.

### What must be preserved (regression guard)

- **Registration** is unchanged in behaviour, in AD-23 timing parity and in its tests. It still creates exactly one Space, through the one slice. The extraction in Task 3 is a pure refactor.
- **The switcher** (1.7) still returns identity only (`ISO-LIST-SPACES`, I-11). Rows carry a name and nothing else. It stays Session-only, and a Token stays no credential.
- **The `AccountId` key admits nothing but `Membership`.** Only the creation unit of work may carry both keys, and it reads nothing but the count.
- **Every existing gate** named in Task 9 still fails against a plant for anything other than the one named exception.
- **Space settings' Label section and its UX-DR29 narration** are unchanged, including 2.4's review fix for `SpaceId` changes.
- **2.11's context bar at 320px:** Role chip present, no menu, no sideways scroll, and the switcher label visually hidden below 30em.
- **`document.title`** stays a fixed `Yello`.

### Open questions (decide in Task 1; each is reversible; record as "Qn - (x)")

**Decided by Lee, 2026-10-29 (not open):** Q1 - (a) one named dual-key creation unit of work; Q2 - (a) Account-scoped idempotency table; Q5 - (a) `Space` and `Account` stay policy-free as a recorded variance. Q3, Q4 and Q6-Q10 remain for the dev phase.

1. **How one transaction can both count the Account's Memberships and insert the new Space's rows** (AC6, AC7, AC11).
   - (a) **Recommended.** One named Account-scoped creation unit of work sets **both** keys: `AccountId` from the Session, and `SpaceId` set to a server-generated new id. It takes the Account's app lock, counts, inserts and commits. While both keys are set, `Membership`'s filter admits the caller's own Memberships across Spaces. This unit of work reads only the count and returns only the new Space's identity, which is exactly the read the UX spine already permitted. `No_caller_sets_both_session_context_keys` is amended to admit exactly this one caller, and the AD-24 amendment names it.
   - (b) Two units of work: count under `AccountId`, then insert under `SpaceId`. Rejected, because it is not "inside the same transaction" (AD-25) and two concurrent creations at 49 can both pass.
   - (c) A stored per-Account Membership counter on `Account`, which has no policy. Rejected: it is a second source of truth that every Membership writer (3.3, 4.3, 4.5, 5.4) must maintain, and AC12 wants a live count.
   - (d) Widening `Membership`'s BLOCK predicate to the `AccountId` key. **Forbidden**: it would let an Account-scoped unit of work insert a Membership for itself into any Space.
2. **`Idempotency-Key` on `POST /api/v1/spaces`** (`deferred-work.md:461`; AD-18 / AR-25).
   - (a) **Recommended.** Add an Account-scoped idempotency record: a new table keyed `(AccountId, Key)`, with FILTER and BLOCK predicates on a new `fn_AccountIsolationPredicate` over `SESSION_CONTEXT('AccountId')`, and a migration with an exact `Down`. Replay it in the creation step before the bound. The reason: a lost response here creates a second Space, which is exactly the duplicate AD-18 exists to stop, and the client already re-sends an unanswered create with the same key.
   - (b) State the route out of AD-18, with the reason in Completion Notes, as registration and sign-in were. Cheaper, but it leaves a duplicate-Space path.
3. **Where "Create a Space" lives** (AC13, AC15). The spines name no surface for it.
   - (a) **Recommended.** A page `/spaces/new`, linked from the foot of the switcher panel (outside the `<ul>`, so the rows stay identity-only) and from the no-Space state on `/`.
   - (b) An inline form inside the switcher disclosure. Rejected: it puts a form in a non-modal disclosure that closes on `LocationChanged` and Esc.
   - (c) Account settings. Rejected: it is hidden from the no-Space state's single action.
4. **"Reachable from the context bar"** (AC4; `deferred-work.md:528`).
   - (a) **Recommended.** A "Space settings" link in the context bar for Owner and Admin, absent otherwise, never on the Role chip. **Remove** the Space home link. This also resolves `:528` by the first of the three options UX listed there; record it in the ledger for UX to confirm.
   - (b) Add the context bar link and keep the Space home link. This leaves `:528` open.
5. **`Space` and `Account` row-level security** (`deferred-work.md:95-101`, whose owner is the AD-24 amendment).
   - (a) **Recommended.** Keep both policy-free, and record that in the amendment as a deliberate variance:
     - `Account` is global.
     - `Space` has no `SpaceId` column, its readers and writers are held to a named list by `Space_is_read_only_where_named` (widened in Task 9), and every write filters on the resolved `SpaceId`.
     - A predicate on `Space` would have to admit by `Id = SpaceId key` **or** by a Membership join under the `AccountId` key. That is a correlated predicate on the switcher's hot path, and it is not needed to hold NFR-1, because `Space` carries identity only.
   - (b) Add a policy on `Space(Id)` using that two-way predicate.
   - Either way, `:95-101` is closed by the amendment.
6. **Space name rules** (AC9).
   - (a) **Recommended.** `SpaceName.TryNormalise`: trim, 1-200 UTF-16 code units (the column length, and above the 136-character longest provisioned name), not unique, nothing else.
   - (b) 1-100 to match Projects. Rejected: an existing Personal Space name could then not be submitted back unchanged.
7. **Updating the client after a rename** (AC14).
   - (a) **Recommended.** Add an `ActiveSpaceState` method (for example `Renamed(ActiveSpaceResponse)`) that replaces `Current` for the **same** `SpaceId` only. It raises `Changed`, does not count as a switch and moves no focus. Also update `SpaceDirectory.Current`'s row in place, or mark it stale.
   - (b) Re-run `Arrive` with the response. Rejected if it would be treated as a switch and move focus to the context bar.
8. **Announcements** (AC13, AC14). Recommended: "Space created." / "Space renamed." with **no name**, keeping announcements free of Space names as UX-DR38 does for cross-Space content. The arrival announcement after creation already names the Space through the context bar's existing switch announcement.
9. **The create response body** (AC9). Recommended: `201` + `Location: SpaceRoutes.ActiveOf(id)` + `SpaceIdentityResponse(Id, Name)`. This is identity only (AD-24), and the client then arrives through the normal `GET ActiveOf`, which yields the Role.
10. **Should `WireDatabase.AddOwnedSpaceAsync` also seed the default Statuses** now that a real creation path exists (it currently seeds none)? Recommended: yes, through the same Task 3 helper's names, so seeded Spaces match created ones. Otherwise, document the difference in the helper's comment.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:332` → **CLOSED by 3.1**, naming the gate and its plant.
- `:334` → **CLOSED by 3.1**: the no-Space state links to creation (AC15).
- `:461` → **CLOSED by 3.1** with Q2's decision.
- `:95-101` → **CLOSED by the AD-24 amendment (3.1)** with Q5's decision, quoting the amendment's location.
- `:526` → correct "5.1 (rename the Space)" to "**3.1 (done)**". The remaining owners are 4.2, 4.4, 5.1 (ownership) and 6.3.
- `:528` → per Q4.
- `:211` → add a note: 3.1's two-key creation unit of work is the precedent and its gate names one caller. 4.3 meets the same question and must amend AD-24 again rather than reuse the exemption silently.
- Any new deferral: an owner and a severity, as every entry has.

### Architecture compliance (must)

- The pipeline owns the transaction, the keys, the bound and idempotency. The slice only provisions or renames (AR-3).
- Ids come from `IIdentifierGenerator` (`SequentialGuid`), never `Guid.CreateVersion7()`, and the new `SpaceId` is never taken from the request (AD-2). Use `TimeProvider`; `DateTimeOffset.Now` is banned.
- RFC 9457 problems with stable types. No prose is parsed by the client (NFR-5). Error bodies carry no Space name (AR-10).
- Raw SQL only in Infrastructure, always parameterised (A-13).
- Migrations: scaffold with `dotnet ef` (run `dotnet tool restore` first), then hand-edit. Use literal names, `CREATE FUNCTION` in its own batch, and an exact `Down`. Nothing is applied at startup (AR-36). Extend `SpaceIsolationSchemaTests` for any new policy, and run `SchemaNames.SpaceScopedTables`-style registration if Q2 (a) adds a table, in an Account-scoped list of its own.
- Ring rule (`AllowedReferenceEdges.cs`): Domain → nothing; Application → Domain; Infrastructure → Application and Domain; Host → all. No ASP.NET types in Application.

### Library / framework

There is no new package. Use what is there:

- .NET 10.0.11, EF Core 10.0.11, `Microsoft.Data.SqlClient` 7.0.2, Asp.Versioning 10.0.0;
- xunit.v3 4.0.0, Testcontainers.MsSql 4.6.0, ArchUnitNET 0.13.3, Microsoft.Playwright 1.62.0.

`ExecuteUpdateAsync` takes the `SetProperty` lambda form in EF Core 10, as `ProjectStore` uses it. There is no axe-core: it is blocked on Lee (`deferred-work.md:589`). Use deterministic ARIA assertions.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:

- Five trait names only. One behaviour per test, with an underscore-sentence name.
- Never `Task.Delay`.
- Every absence assertion has an anchor and is first proved against a plant.
- Randomise data, and never use a shared email literal.
- No DELETE cleanup.
- Never two `dotnet test` processes at once. Iterate with one project and `--filter`.

**Slices** (`tests/Yello.Tests.Slices/Spaces/`; add `CreateSpace/` and `RenameSpace/` partial classes on `MigratedDatabaseFixture`, reusing existing fixtures and adding **no new container-backed fixture**):

- Creation: `201`, Location, and body; the database holds exactly one Membership (Owner, the caller), three `StatusDefinition` rows in `DefaultStatusSet` order, and zero Projects (AC1).
- The creator's other Space is unchanged: Projects, Statuses, Memberships and name. Another Account's switcher does not list the new Space (AC2).
- Same name by two Accounts: both `201`, both read back their own name only (AC5).
- At 50 Memberships (seeded, including non-owned Roles): `409 bound-reached`, `bound`, `limit`, and **no** Space, Membership or Status row written (AC6, AC11). At 49 → `201`.
- Concurrency at 49: two creations made to overlap by waiting until the second request's session is blocked on the app lock (`sys.dm_os_waiting_tasks` / `LCK_M_%`, as the 2.x bound tests do) → exactly one `201` and one `409`. Another Account's creation is not blocked (AC11). Put it in a `DisableParallelization` collection if it measures anything temporal.
- Name rules: whitespace-only, empty, 201 characters → `400 InvalidSpaceName`; 200 → `201`; trimmed (AC9).
- Unauthenticated → `401`, nothing recorded (AC10).
- Idempotency per Q2: a replayed key returns the original `201` and writes nothing; a replay at 50 still returns `201` (AC9, P-10).
- Rename: Owner and Admin → `200` with the new name, persisted. Member and Viewer → `403 rename-space` plus an `InsufficientRole` refusal. Non-member → `404` plus `CrossSpace`. Invalid name → `400`. The rename never touches another Space's row (AC3, AC10).
- Logs: no Space name in any line written during create or rename (I-17). Reuse the existing log-capture helper.

**Isolation** (`tests/Yello.Tests.Isolation/IsolationCases.cs`; GATE-002):

- `ISO-CREATE-SPACE`: Session → `Served(201, check: A's other Space and B's Spaces unchanged, and B's switcher does not list it)`; ApiToken → `NoCredential` (template: `ISO-LIST-SPACES`, line ~80).
- `ISO-RENAME-SPACE-CROSS`: A renaming B's Space → `BoundaryNotFound` on both surfaces, with the name unchanged afterwards.
- A Token bound to A's Space renaming A's Space → `Served(200)`. Add it as the in-Space anchor if the list's shape requires one.
- No row pending. If one must be, GATE-003 makes it live before review.

**Architecture** (`tests/Yello.Tests.Architecture`): Task 9's gates, plus `RefusalCopyTests` for `InvalidSpaceName` and the predicate-against-matrix gate for `PermitsRenameSpace`.

**Client / browser** (`tests/Yello.Tests.Client`, `ClientSurfaceCollection`; extend `FakeYelloApi` with `POST /api/v1/spaces` and `PATCH /api/v1/spaces/{id}`, held and released through a `TaskCompletionSource` like `HoldRenameAsync`):

- Create from the switcher's foot link: the form, submit, the directory refreshed **before** navigation (assert the request order in `api.Requests`), arrival at the new Space home, focus on the context bar, the polite announcement, and the switcher listing it (AC13).
- Create from the no-Space state: exactly one action (AC15).
- `bound-reached` renders "This Account has 50 Spaces, the maximum." in place, with no navigation (AC6).
- `InvalidSpaceName` renders beside the field.
- An unanswered create re-sends with the same key.
- Rename as Owner and as Admin: the context bar's visible and accessible names update without a reload, nothing moves focus to the context bar, the announcement carries no name, and `document.title == "Yello"` (AC14).
- Absence (AC3, AC4): a Member and a Viewer have no Space settings link in the context bar and no rename control. The Owner is the anchor. `main [disabled], main [aria-disabled='true']` counts 0. Typing the settings URL as a Member shows the capability copy (existing behaviour). Each absence is proved against a plant.
- AC8: both inputs found, then `autocomplete="off"` and a non-reusable `name` asserted (a fresh value per render, not in `ReusableFieldNames`).
- `TextSpacingTests` and `SmallViewportTests` (Reflow): the create page and the rename section at 320px with a 100-character name, and the context bar at 320px as Owner with the new link (AC16).
- Esc closes the rename form and focus returns to "Rename".

**Run** (CLAUDE.md):

1. `dotnet build Yello.slnx` (0/0).
2. `dotnet build-server shutdown`.
3. `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`.

Check Docker first with `docker info --format '{{.ServerVersion}}'`. Rerun once before calling a failure real.

### Previous story intelligence (2.11, 2.10, 2.4, 2.1, 1.7)

- **2.11:** the context bar is crowded at 320px. Its Role chip must survive, the switcher label is visually hidden below 30em, and the review found CSS `display` overriding `[hidden]`. Check any new conditional element at narrow widths. Use em breakpoints, never px.
- **2.4:** built Space settings. Its review patch reset `_wasPermitted`/`_leaving` on a `SpaceId` change, because Blazor reuses the page across Spaces. Keep that when adding the rename section. Also, a timeout must not be treated as "the tab left" (`OperationCanceledException` with a `when` on the Space token). The new create page has **no** Space token, so it needs its own cancellation story.
- **2.1:** Projects create and rename is the template. `UnansweredCreate` key reuse. The empty-Space "one action" test was widened for the settings link (`:528`), so Q4 changes it back.
- **1.7:** the `AccountId` key, the switcher, and the "a unit of work sets exactly one key" rule. Q1 is the first deliberate exception, so name it in every gate message.
- Review cycles in Epic 2 repeatedly found **AC clauses with no test** and **defence layers no test can fail**. Map every AC to a named test in Completion Notes, and prove every gate against a plant.

### Git intelligence

The recent commits are `364d4c3` "Close epic 2", `850ebc1`/`06d2a07` (2.11 pager and review), `16d68d9` (add 2.11) and `f37b5b9` (2.10 review). The pattern is one implementation commit plus one review-fix commit per story, made by the orchestrator. Do not commit.

### Project Structure Notes

- New: `Yello.Domain/Spaces/SpaceName.cs` (plus the provisioning helper).
- New: `Yello.Application/Spaces/CreateSpace/*`, `Yello.Application/Spaces/RenameSpace/*`.
- New: `Yello.Infrastructure/Persistence/SpaceCreationStore.cs` / `SpaceRenameStore.cs`, or one `SpaceStore.cs`.
- New: `Yello.Host/Spaces/` (the Account-scoped creation step), `Yello.Host/Endpoints/SpaceEndpoints.cs`.
- New: `Yello.Contracts/Spaces/{CreateSpaceRequest,RenameSpaceRequest}.cs`.
- New: `Yello.Client/Pages/CreateSpace.razor`, `Yello.Client/Spaces/SpaceCapabilities.cs`.
- New if Q2 (a): a migration.
- Updated: `SpaceRoutes.cs`, `ProblemTypes.cs`, `BoundCounter.cs`, `ApplicationLocks.cs`, `RegisterAccountHandler.cs` (refactor only), `ApiRouteGroup.cs` (if the step is a filter), `Program.cs`, `InfrastructureServices.cs`, `ContextBar.razor`, `SpaceSwitcher.razor`, `SpaceSettings.razor`, `SpaceHome.razor`, `Home.razor`, `ActiveSpaceState.cs`, `SpaceDirectory.cs`, `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`, `ClientRoutes.cs`, `FakeYelloApi.cs`, `WireDatabase.cs`, `IsolationCases.cs`, the gate tests named in Task 9, `ARCHITECTURE-SPINE.md` (AD-24 amendment), `EXPERIENCE.md:190`, `deferred-work.md`, `TESTING-CONVENTIONS.md`.
- Variance to record: if the creation step's metadata needs a third `AccountScopedSurface` member, name it for what it is (for example `SpaceCreation`). The gate then says "exactly the AD-24 surfaces as amended on 2026-10-29", never "three".

### References

- `_bmad-output/planning-artifacts/epics.md:85-87` (FR-5/6/7), `:161` (NFR-8), `:178` (§6.1), `:191` (AR-3), `:193-199` (AR-5..AR-11), `:213` (AR-25), `:215` (AR-27), `:217-218` (AR-29, AR-30), `:222` (AR-34), `:247` (UX-DR9), `:250` (UX-DR12), `:252` (UX-DR14), `:271-279` (UX-DR27..UX-DR35), `:365` (coverage), `:1430-1477` (Epic 3, story 3.1)
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md:171-211` (FR-4..FR-7)
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md:210` (AD-22), `:218-222` (AD-24), `:224-228` (AD-25), `:234` (AD-26 "AD-24 stands unamended")
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:43-67` (IA, Space settings, rename placement), `:183-191` (Account-scoped reads beyond AD-24), `:202-203` (context bar, switcher), `:244` (Role drop), `:253` (no Space), `:276` (scale bound), `:402` (switch focus); `mockups/space-settings.html:111-180` (Space name field, Owner vs Admin)
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md:774-803`, `:1222-1230`, `:1307` (AD-24 amendment due before Epic 3)
- `_bmad-output/test-artifacts/test-design-qa.md:124` (AD-24 amendment changes I-11), `:204`, `:275` (I-11), `:305` (A-7), `:313` (P-5), `:314` (P-10)
- `_bmad-output/implementation-artifacts/deferred-work.md:95-101`, `:211`, `:332`, `:334`, `:461`, `:478`, `:526`, `:528`, `:589`
- `tests/TESTING-CONVENTIONS.md`; story files `1-7-…md`, `2-1-…md`, `2-4-…md`, `2-11-…md`

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-3-1-create-and-rename-a-space.md` (scenario map, the constraints every seed satisfies, the scaffold-held names to replace, and the open questions assumed: Q3 (a), Q4 (a), Q6 (a), Q7 (a), Q8, Q9 at their recommendations; Q1 (a), Q2 (a), Q5 (a) as Lee decided).

| File | Cases | ACs |
|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CreateSpace.cs` | 8 (6 entries), skipped | AC1, AC2, AC5, AC9, AC10 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CreateSpaceBound.cs` | 7, skipped | AC6, AC9 (Q2), AC11, AC12 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RenameSpace.cs` | 10 (7 entries), skipped | AC3, AC9, AC10, AC14 |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 3 rows x 2 surfaces, `PendingUntil: "3.1"` | AC2, AC10 (GATE-002) |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Spaces.cs` | 6, skipped | AC3, AC4, AC6, AC7, AC9, AC10 |
| `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.Creation.cs` | 2, skipped | AC7, AC11 |
| `tests/Yello.Tests.Client/SpaceCreateAndRenameTests.cs` | 13 (10 entries), skipped | AC3, AC4, AC6, AC8, AC9, AC13-AC16 |
| `tests/Yello.Tests.Client/FakeYelloApi.cs` | harness only: `POST /api/v1/spaces`, `PATCH /api/v1/spaces/{id}`, holds | - |

The slice files are partials of the existing `ActiveSpaceEndpointTests` (its one database on the Slices engine), not new classes in `Spaces/CreateSpace/` and `Spaces/RenameSpace/`: no new engine, fixture or container-backed class.

**Green today: none.** Every scaffold waits on a route, type, page or the AD-24 amendment, so this run adds no standing gate. Six active gates go red by design when the product lands and are Task 9's to amend (listed in `RefusalBoundaryGateTests.Spaces.cs`'s remarks).

**Activation order:**

1. `The_51st_Space_is_refused_409_bound_reached_and_the_Account_still_holds_50` first. It tests the riskiest assumption, Q1 (a): one connection with both `@read_only` keys, inserts admitted by the `BLOCK AFTER INSERT` predicates under the new `SpaceId`, and the count under `AccountId`, all in one transaction. Then the bare creation (`Creating_a_Space_answers_201_...`), `Exactly_one_creation_unit_of_work_sets_both_session_context_keys`, the race (`Two_concurrent_creations_at_49_...`), `Another_Accounts_creation_never_waits_...`, `A_Membership_the_Account_no_longer_holds_...` and `The_Spaces_per_Account_bound_is_consulted_by_the_creation_route_alone`.
2. `The_AD_24_amendment_names_Space_creation_and_states_its_reason`, with Task 2.
3. The contracts and `SpaceName` gates, then the other creation slice cases.
4. The three idempotency cases.
5. The rename gate and the seven rename slice cases.
6. The three isolation rows. Delete `PendingUntil`/`PendingReason`; all three must be live before `review` (GATE-003).
7. The client predicate gates, then the browser cases (build `Yello.Client` in Debug first).
8. Task 9's amendments to the active gates, each proved against a plant.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), dev-story run by the story-cycle orchestrator, 2026-10-30.

### Debug Log References

- **Red evidence, honestly stated.** The ATDD run's red table (checklist, Step 5) stands for SLC-001, SLC-006, BND-001, ARCH-003 to ARCH-007. In this run the server half (Tasks 3-5) was written before the slice scaffolds were un-skipped, so their red was not re-observed here; on activation all 25 slice cases passed on the first run (`ActiveSpaceEndpointTests`, 61/61). Re-observed red in this run, before the change that greens it: ARCH-008 (AD-24 amendment absent, before Task 2), ARCH-001/ARCH-002 ("No static bool (string roleCode) method named for RenameSpace / SpaceSettings", before Task 7), and the six active gates the scaffold remarks predicted (`No_endpoint_without_spaceId...`, `Every_Account_scoped_endpoint_names_an_AD_24_surface`, `The_Account_scoped_surfaces_are_exactly_the_two_AD_24_names`, `No_caller_sets_both_session_context_keys`, `Space_is_read_only_where_named`) plus one it did not: `The_endpoints_unreachable_with_a_Token_are_Token_management_the_switcher_and_the_move` (creation carries the Account-scoped marker on the switcher's path). Each was amended for exactly the one named exception.
- **Own defect caught by an existing gate:** `Only_the_pipeline_depends_on_the_idempotency_port` failed because `AccountIdempotencyStore` read `IIdempotencyStore.Retention`; the Account port now states its own 24 hours.
- **Scaffold corrections (client):** (1) `The_no_Space_state_offers_exactly_one_action...` counted links and buttons in `#app`, which includes the context bar's shell (skip control, switcher, Account, Sign out) on every authenticated surface - scoped to `main`. (2) `A_creation_resent_after_a_lost_answer...` waited for any `main [role=status]`, which is always attached (the page's polite regions), so it pressed Enter while the first attempt was in flight - it now waits for the unanswered sentence ("The Space may not have been created."). (3) `FakeYelloApi.InvalidSpaceNameType` replaced by `ProblemTypes.InvalidSpaceName` and removed.
- **Scaffold correction found by the first full run:** `Creating_a_Space_leaves_every_other_Space_unchanged_and_unseen` compared an unordered expected pair `[newId, personal]` with the ordered actual ids, so it passed or failed by which sequential GUID sorted first (passed in the class run, failed in the full run). The expected pair is now ordered too.
- **Scaffold corrections (server/arch):** the held `InvalidSpaceNameType` in the slice partials replaced by `ProblemTypes.InvalidSpaceName`; ARCH-003/ARCH-004 lookups by name replaced by `typeof(...)`; red-phase remarks rewritten. No scaffold assertion changed.
- **Product fixes found by the browser scaffolds:** the rename section was a `<section aria-labelledby>` whose heading "Space name" made `GetByLabel("Space name")` resolve to the region and the input - now a plain `div` with its `h2`. At 320x256 with 200% text-only zoom and a 100-character name the sticky context bar grew taller than the viewport and intercepted every click below it - the bar now stops sticking below 30em of viewport height.
- **Plants (each restored from memory in a `finally`; no git restore):** a second both-keys caller in `MembershipActiveSpaceResolver` -> `No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys` and `Exactly_one_creation_unit_of_work_sets_both_session_context_keys` fail; an `ExecuteUpdateAsync` on `Space` in `ActiveSpaceNameReader` -> `The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space` fails; the Account's lock removed from `BoundCounter` -> `Two_concurrent_creations_at_49...` and `Another_Accounts_creation_never_waits...` fail; `name="name"` on the create page -> `Both_Space_name_inputs_are_found...` fails; the context bar link for every Role -> `Space_settings_is_linked_from_the_context_bar...` (member, viewer) fail; a `spaceName` argument on `SpaceLog.SpaceCreated` -> `No_log_message_takes_a_name` fails. A first client plant did not compile (unused field) and so proved nothing; it was redone so it compiled, with `Yello.Client` rebuilt in Debug.
- Migration scaffolded with `dotnet ef migrations add SpaceCreation` (after `dotnet tool restore`), then hand-edited.

### Completion Notes List

**Decisions (Task 1).** Q1 (a), Q2 (a), Q5 (a) were decided by Lee and implemented as decided. The rest are this run's reversible decisions, each at the story's recommendation unless stated:

- Q3 - (a): a page at `/spaces/new`, linked from the switcher panel's foot (outside its `<ul>`) and from the no-Space state. Cost: one route under `/spaces` that is not a Space; `ClientRoutes.SpaceIn` reads it as none.
- Q4 - (a): a "Space settings" link in the context bar after the Role chip, Owner and Admin only, never on the chip; Space home's link removed. Cost: `deferred-work.md:528` resolved by UX's first option and left for UX to confirm.
- Q6 - (a): `SpaceName` 1-200 trimmed, not unique; the column length is `SpaceName.MaxLength`. Cost: none beyond Projects' 100 differing.
- Q7 - (a): `ActiveSpaceState.Renamed(ActiveSpaceResponse)` replaces `Current` for the same Space only, raises `Changed`, is not an arrival and moves no focus; `SpaceDirectory.Renamed` updates the held row.
- Q8: "Space created." / "Space renamed.", no name.
- Q9: `201` + `Location: ActiveOf(id)` + `{id, name}`.
- Q10 - (b), not the recommendation: `WireDatabase.AddOwnedSpaceAsync` still seeds no Statuses, documented in its remarks. Reason: `Yello.Tests.Shared` references only `Yello.Contracts` and cannot read `DefaultStatusSet` without a new project reference, and its two callers read no Status. Cut this line to switch to (a) by adding the reference.

**Other reversible decisions (each a line to cut):**

- The creation surface is a third `AccountScopedSurface` member, `SpaceCreation`, as the story's variance note allowed; the gates say "the AD-24 names as amended".
- The creation step is a nested partial of `ActiveSpaceResolution` (`ActiveSpaceResolution.Creation.cs`, filter `CreateSpaceAsync`, last in `ApiRouteGroup`), so every A-12 gate's "the pipeline" still names one type. Its unit of work is `SpaceCreationScope` behind the Application port `ISpaceCreationScope`.
- Rename is mapped in `ActiveSpaceEndpoints` (beside the GET it answers like), because it states the caller's Role in `ActiveSpaceResponse` and that type is the one the Role-reader gate already names; no gate was widened for it.
- The Account-scoped idempotency table has its own `AccountIsolationPolicy` rather than a predicate in `SpaceIsolationPolicy`, so the Space policy still covers exactly the tables with a `SpaceId`.
- The creation's bound refusal is logged by a new `SpaceLog.CreationBoundReached` naming the bound and limit only - not the Account (SpaceLog's rule forbids Account identity) and not a Space that was never created. The story's "keyed by Account" was read as "not by name".
- Writes: no sibling class; the create page calls `SpaceWrites.SendAsync` with its own token (its disposal linked to the Session).
- The context bar stops being sticky below 30em of viewport height (found by AC16's 200% zoom case).
- `SPEC.md` does not restate AD-24's two surfaces (grepped: no match); its companion `surfaces-and-journeys.md:128` does, and got a one-sentence note.

**What was built.** Domain `SpaceName`, `NewSpace.Provision` (registration refactored onto it; its tests unchanged and green). `POST /api/v1/spaces` on the Space creation unit of work: server-generated id, both keys and both scopes, Account-scoped `Idempotency-Key` replay before the bound, `Bound.SpacesPerAccount` counted live under `Yello/SpacesPerAccount/{accountId}`, `201` + `Location`, `400 invalid-space-name`, `409 bound-reached`, `401` without a Session (a Token is no credential). `PATCH /api/v1/spaces/{spaceId}` under `RenameSpace`, writing `WHERE Id =` the resolved Space. Migration `20261030201142_SpaceCreation`. AD-24 amendment recorded (spine, `EXPERIENCE.md:190`). Client: create page, switcher foot link, no-Space action, Space settings name section, context bar link, `SpaceCapabilities`, `SpaceNameRule`, copy.

**AC -> tests.** AC1: `Creating_a_Space_answers_201_and_makes_the_caller_its_one_Owner...`. AC2: `Creating_a_Space_leaves_every_other_Space_unchanged_and_unseen`, ISO-CREATE-SPACE. AC3: `The_Owner_renames...`, `An_Admin_renames...`, `A_Member_or_a_Viewer_is_refused_403...`, `Renaming_updates_the_context_bar_in_place...` (owner, admin), `Space_settings_is_linked_from_the_context_bar...`, `The_clients_rename_predicate_agrees_with_the_capability_matrix`. AC4: `Space_settings_is_linked_from_the_context_bar...`, `The_clients_Space_settings_predicate_is_the_union_of_its_sections`, `An_empty_Space_states_no_Projects_yet...` (set-aside removed). AC5: `Two_Accounts_may_each_create_a_Space_with_the_same_name...`. AC6: `The_51st_Space_is_refused_409_bound_reached...`, `The_51st_Space_renders_the_bound_in_place...`, `The_Spaces_per_Account_bound_is_consulted_by_the_creation_route_alone`. AC7: `The_AD_24_amendment_names_Space_creation_and_states_its_reason`, `Exactly_one_creation_unit_of_work_sets_both_session_context_keys`, `No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys`, `Only_the_pipeline_opens_the_Space_creation_unit_of_work`. AC8: `Both_Space_name_inputs_are_found_and_neither_can_be_autofilled`. AC9: `An_invalid_Space_name_is_refused_400...`, `An_invalid_rename_is_refused_400...`, `The_new_Space_id_is_generated_by_the_server...`, `The_Space_request_contracts_carry_a_name_and_nothing_else`, `The_Space_name_rule_admits_1_to_200...`, `The_clients_Space_name_predicate_agrees_with_the_Domain_rule`, the three idempotency cases, `The_Account_scoped_policy_covers_exactly_the_Account_scoped_tables_on_the_AccountId_key`. AC10: `Creation_without_a_Session_is_refused_401...`, `A_non_members_rename_is_the_boundary_404...`, `A_Token_renames_its_own_Space_at_its_current_Role`, `Rename_declares_RenameSpace_and_creation_declares_no_capability`, ISO-RENAME-SPACE-CROSS, ISO-RENAME-SPACE-OWN. AC11: `Two_concurrent_creations_at_49...`, `Another_Accounts_creation_never_waits...`, and BND-001's "nothing written" (no injected mid-creation failure: the rows and the record are one `SaveChanges` sequence on one transaction, and no seam exists over the wire). AC12: `A_Membership_the_Account_no_longer_holds_does_not_count...`, BND-001 (seeded at every Role). AC13: `Creating_a_Space_from_the_switcher_lands_in_it_as_a_switch...`, `A_creation_resent_after_a_lost_answer...`. AC14: `Renaming_updates_the_context_bar_in_place_and_announces_no_name`, `No_Space_name_reaches_the_log_on_creation_or_rename`, `No_log_message_takes_a_name`. AC15: `The_no_Space_state_offers_exactly_one_action...`, `The_51st_Space_renders_the_bound_in_place...`. AC16: `The_new_surfaces_reflow_at_320px_with_a_100_character_name` (text spacing, 200% zoom), `Escape_closes_the_rename_form...`.

**Verification:** `dotnet build Yello.slnx` 0 warnings, 0 errors; `dotnet build-server shutdown`; `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`: total 1,585, passed 1,576, failed 0, skipped 9 (baseline 1,565 / 1,518 / 0 / 47: the 38 scaffold entries are live, 6 gates and 1 schema case added). The 9 skips are the 3 migration-bundle cases and the explicit tiers; no story 3.1 case is skipped.

**Ledger:** `:95-101` closed by the amendment (Q5); `:211` noted (3.1 is the precedent, not a licence); `:332`, `:334`, `:461` closed; `:478` noted (same race shape on the creation key, owner 8.3 unchanged); `:526` corrected (rename is 3.1's, done); `:528` resolved by option one, for UX to confirm. No new deferral.

### File List

- `Yello.Domain/Spaces/SpaceName.cs` (new)
- `Yello.Domain/Spaces/NewSpace.cs` (new)
- `Yello.Application/Accounts/RegisterAccount/RegisterAccountHandler.cs`
- `Yello.Application/Spaces/ISpaceCreationScope.cs` (new)
- `Yello.Application/Spaces/IAccountIdempotencyStore.cs` (new)
- `Yello.Application/Spaces/ISpaceStore.cs` (new)
- `Yello.Application/Spaces/CreateSpace/CreateSpaceHandler.cs` (new)
- `Yello.Application/Spaces/RenameSpace/RenameSpaceHandler.cs` (new)
- `Yello.Infrastructure/AccountScopedReads.cs`
- `Yello.Infrastructure/SpaceBoundaryLimits.cs`
- `Yello.Infrastructure/Persistence/AccountIdempotencyRecord.cs` (new)
- `Yello.Infrastructure/Persistence/AccountIdempotencyStore.cs` (new)
- `Yello.Infrastructure/Persistence/ApplicationLocks.cs`
- `Yello.Infrastructure/Persistence/BoundCounter.cs`
- `Yello.Infrastructure/Persistence/Configurations/AccountIdempotencyRecordConfiguration.cs` (new)
- `Yello.Infrastructure/Persistence/Configurations/SpaceConfiguration.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261030201142_SpaceCreation.cs` (new)
- `Yello.Infrastructure/Persistence/Migrations/20261030201142_SpaceCreation.Designer.cs` (new)
- `Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs`
- `Yello.Infrastructure/Persistence/SchemaNames.cs`
- `Yello.Infrastructure/Persistence/SpaceCreationScope.cs` (new)
- `Yello.Infrastructure/Persistence/SpaceStore.cs` (new)
- `Yello.Infrastructure/Persistence/YelloDbContext.cs`
- `Yello.Contracts/ProblemTypes.cs`
- `Yello.Contracts/Spaces/SpaceRoutes.cs`
- `Yello.Contracts/Spaces/CreateSpaceRequest.cs` (new)
- `Yello.Contracts/Spaces/RenameSpaceRequest.cs` (new)
- `Yello.Host/ApiRouteGroup.cs`
- `Yello.Host/Program.cs`
- `Yello.Host/SpaceLog.cs`
- `Yello.Host/Endpoints/SpaceDirectoryEndpoints.cs`
- `Yello.Host/Spaces/AccountScopedResolution.cs`
- `Yello.Host/Spaces/AccountScopedSurface.cs`
- `Yello.Host/Spaces/ActiveSpaceEndpoints.cs`
- `Yello.Host/Spaces/ActiveSpaceResolution.Bound.cs`
- `Yello.Host/Spaces/ActiveSpaceResolution.Creation.cs` (new)
- `Yello.Client/Components/ContextBar.razor`
- `Yello.Client/Components/SpaceNameSection.razor` (new)
- `Yello.Client/Components/SpaceSwitcher.razor`
- `Yello.Client/Localisation/ClientCopy.resx`
- `Yello.Client/Pages/ClientRoutes.cs`
- `Yello.Client/Pages/CreateSpace.razor` (new)
- `Yello.Client/Pages/Home.razor`
- `Yello.Client/Pages/SpaceHome.razor`
- `Yello.Client/Pages/SpaceSettings.razor`
- `Yello.Client/Refusals/RefusalCopy.cs`
- `Yello.Client/Spaces/ActiveSpaceState.cs`
- `Yello.Client/Spaces/SpaceCapabilities.cs` (new)
- `Yello.Client/Spaces/SpaceDirectory.cs`
- `Yello.Client/Spaces/SpaceNameRule.cs` (new)
- `Yello.Client/Spaces/SpaceWrites.cs`
- `Yello.Client/wwwroot/css/components.css`
- `tests/TESTING-CONVENTIONS.md`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Move.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Projects.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Spaces.cs`
- `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.cs`
- `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.Creation.cs`
- `tests/Yello.Tests.Client/FakeYelloApi.cs`
- `tests/Yello.Tests.Client/LabelTests.cs`
- `tests/Yello.Tests.Client/SpaceCreateAndRenameTests.cs`
- `tests/Yello.Tests.Client/SpaceHomeProjectsTests.cs`
- `tests/Yello.Tests.Isolation/IsolationCases.cs`
- `tests/Yello.Tests.Shared/WireDatabase.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/SpaceIsolationSchemaTests.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CreateSpace.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CreateSpaceBound.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RenameSpace.cs`
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md`
- `_bmad-output/specs/spec-yello/surfaces-and-journeys.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/3-1-create-and-rename-a-space.md`

## Change Log

- 2026-10-30 - Story 3.1 implemented (dev-story): Space creation on one named both-keys unit of work with the Spaces-per-Account bound and an Account-scoped Idempotency-Key; rename under RenameSpace; AD-24 amendment recorded; client create page, switcher and no-Space entry points, Space settings name section and context bar link; 38 ATDD scaffold entries activated (20 slice, 8 architecture, 10 browser), 3 isolation rows made live, 7 active gates amended for one named exception each, 6 gates added.
