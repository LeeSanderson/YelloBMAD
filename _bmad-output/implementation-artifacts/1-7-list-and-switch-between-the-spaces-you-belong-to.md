# Story 1.7: List and switch between the Spaces you belong to

Status: ready-for-dev

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-7-list-and-switch-between-the-spaces-you-belong-to`
Requirements owned (coverage row `epics.md:350`): **FR-9**, **AR-29** (AD-24), **UX-DR9**, **UX-DR28**, **UX-DR31**, **UX-DR33**. Through Epic 1's implementation notes (`epics.md:427`): **UX-DR37** (voice, copy externalised), **UX-DR39** (focus on Space switch), **UX-DR40/NFR-9** (Space switching is one of the five WCAG 2.1 AA flows).
Test-design scenarios this story lands (`test-design-qa.md`): **I-11** (P0, AD-24, Account-scoped surfaces return Space identity only), **I-10** (P0, FR-9/NFR-2, authorisation never carried across a Space switch, the request-path half), **X-8** (P1, the Role chip renders as a bordered chip, Role verbatim). Lifted to both surfaces by story 1.9; the Token half of I-11 is story 1.8's (see Q9).
Ledger entries assigned to this story (`deferred-work.md`), all folded in below: the Membership `AccountId` predicate (`:92-96`, 1.3/1.5); sign-out (`:149-151`, 1.4, provisional); no browser has driven a signed-in flow (`:153-157`, 1.4); the refusal page's route back (`:268`, 1.6); no client code calls a Space-scoped endpoint, no browser case of the two 404s (`:270`, 1.6); the SPEC refusal-copy defect (`:282`, 1.6).
Depends on: **1.2** (tokens: `--role-chip*`, `.type-role-label`, radii), **1.3** (schema, `MigratedDatabaseFixture`, resources), **1.4** (Session, `SessionState` purge seam, `YelloApiHandler`, `WireSession`, `HostProcess`), **1.5** (`SpaceSessionContext`, `SpaceScope`, `ActiveSpaceResolution`, `SpaceSeeding`), **1.6** (capabilities, `ApiRouteGroup`, `RefusalSurface`, `RefusalCopy`, `not-available`, `RefusalSurfaceTests`). All done.
Seeds, does not own: the Account-scoped data path Account settings (1.8 for API Tokens, 5.4 for deletion) reuses; the client Space-state purge seam epic 2 registers its Board with; the switcher response 1.9 checks every Space-scoped request against; the multi-Space stress 3.2 applies.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As someone who will hold Memberships in several Spaces,
I want a switcher showing exactly the Spaces I belong to, which changes my whole working surface when I pick one,
So that I always know which context I am operating in and can never see one I have no standing in.

## Acceptance Criteria

AC1–AC8 are reproduced **verbatim** from `epics.md:800-836` (`status: final`), numbered by position. AC9–AC11 are carried from the ledger and the UX spine, and say where from.

**AC1 — exactly my Spaces, and no other way to find one**

**Given** an authenticated Account
**When** the switcher is opened
**Then** it lists exactly the Spaces that Account holds a Membership in
**And** no Space is discoverable by any other means — no directory, no search, no enumeration reaching beyond Membership

**AC2 — a row is a name**

**Given** the switcher
**When** a row renders
**Then** it carries the Space name and nothing else — no count, no Role, no badge — because this surface may return Space identity only

**AC3 — the Account-scoped context, in the database**

**Given** the switcher and Account settings, the only two Account-scoped surfaces
**When** either queries the datastore
**Then** it runs under an `AccountScopedContext` whose RLS predicate is `SESSION_CONTEXT('AccountId')` — never a disabled policy and never a raw connection
**And** no Project, Task, Membership, Label or count crosses a Space boundary through them

**AC4 — the Role is the destination's**

**Given** a Space is selected
**When** the switch completes
**Then** the acting Role becomes whatever that Account holds in the destination, with no carry-over from the previous Space
**And** focus moves to the context bar, with the new Space and Role announced politely

**AC5 — the context bar**

**Given** the context bar
**When** it renders on any authenticated surface
**Then** it names the active Space and shows the acting Role verbatim as one of Owner / Admin / Member / Viewer in a display-only chip that is never interactive
**And** it never scrolls away, collapses, or hides behind a menu

**AC6 — the accessible name states what the Role permits**

**Given** the context bar's accessible name
**When** read by assistive technology
**Then** it states the Role **and what it permits** — "Northwind Redesign — Admin, can manage Members and settings", or "— Viewer, read only in this Space"
**And** the copy resource holds sentence case with the uppercase applied by `text-transform`, so the Role is not spelled out letter by letter

**AC7 — the synchronous purge**

**Given** a Space switch, a sign-out, or back/forward navigation
**When** it occurs
**Then** everything Space-scoped for the departed Space is purged synchronously, before any render
**And** no Space-scoped content was ever written to `localStorage`, `sessionStorage` or IndexedDB

**AC8 — browser-owned surfaces**

**Given** any authenticated surface
**When** browser-owned surfaces are inspected
**Then** `document.title` is a fixed `Yello` carrying no Space, Project or Task name; every Space name input carries `autocomplete="off"` with a non-reusable field name; and scroll restoration is disabled on Space-scoped routes

**AC9 — the refusal page's route back is drawn from the switcher** *(carried from story 1.6 AC9 / UX-DR32; ledger `deferred-work.md:268`)*

**Given** the refusal page, reached by a real Space-scoped request answered 404 or 403, or by an unknown route
**When** its route back is followed by a signed-in Account
**Then** it lands on a Space taken from the current switcher response — never a remembered last Space
**And** the two 404 causes render the same page, words, layout and focus in a browser, with no client-side branch

**AC10 — no Space-scoped request for a Space the switcher did not list** *(UX-DR31, `EXPERIENCE.md:146`; the client half of `epics.md:914`)*

**Given** a Space-scoped client route naming a Space id
**When** that id is absent from the current switcher response
**Then** the client renders the refusal page without issuing any Space-scoped request for it
**And** the context bar never renders a Space name that did not come from the server's answer for that Space on this load (`EXPERIENCE.md:279`)

**AC11 — signing in lands somewhere** *(ledger `deferred-work.md:149-157`; `SignIn.razor:80`)*

**Given** a successful sign-in, or a signed-in Account arriving at `/`
**When** the client decides where to go
**Then** it navigates to a Space drawn from the switcher response, or — for an Account holding no Membership — to a state that says so and never auto-creates a Space (UX-DR35, FR-7)
**And** a sign-out control exists in the context bar, ends the Session on the server, and purges through the same seam as an expired Session (Q4)

## Tasks / Subtasks

- [ ] **Task 1 — Read the open questions first, and record each decision before the code that depends on it** (AC: all)
  - [ ] Eleven questions are at the end of the Dev Notes, each with a recommended decision. Take each as a **reversible, recorded decision** in the Completion Notes: which option, why, and what it costs a later story. No human is in the loop for this phase; do not stop to ask.
  - [ ] Q1 (the RLS shape) and Q2 (where the context bar's Role comes from) shape the server design. Settle them before Task 2.

- [ ] **Task 2 — The Account-scoped context, in both layers** (AC: 1, 3)
  - [ ] **One session-context call site, two keys.** Generalise `Yello.Infrastructure/Persistence/SpaceSessionContext.cs` so the single `sp_set_session_context … @read_only = 1` line takes its key from a closed set (`SpaceId`, `AccountId`) — e.g. a `SessionContextKey` enum or two named entry points over one private call. `SpaceResolutionSeamTests.Exactly_one_production_call_site_sets_the_Space_session_context` counts **lines** containing `sp_set_session_context` (expected 1, under `Yello.Infrastructure/`); keep it at one line and rename the gate only if its name stops being exactly what it checks. Add `SchemaNames.AccountIdSessionKey`.
  - [ ] **A unit of work sets exactly one key.** A Space-scoped unit of work never sets `AccountId`; an Account-scoped unit of work never sets `SpaceId`. If both were set on one connection, a Space-scoped request would see the Account's Memberships (and Roles) in every other Space through the widened predicate below. Gate it (Task 8).
  - [ ] **The value comes from the Session, never the client.** `AccountId` is `SessionPrincipal.AccountIdOf(httpContext.User)` — server-issued. AD-2's "never from a client-supplied value" (`ARCHITECTURE-SPINE.md:81-86`).
  - [ ] **RLS (Q1, recommended):** a new predicate function for `Membership`'s **filter** only, e.g. `dbo.fn_MembershipVisibilityPredicate(@SpaceId, @AccountId)` returning a row when `@SpaceId = CAST(SESSION_CONTEXT(N'SpaceId') AS uniqueidentifier) OR @AccountId = CAST(SESSION_CONTEXT(N'AccountId') AS uniqueidentifier)`, swapped in with `ALTER SECURITY POLICY dbo.SpaceIsolationPolicy ALTER FILTER PREDICATE … ON dbo.Membership` (verify the exact `ALTER`/`DROP`+`ADD` syntax against SQL Server 2025 docs before writing it — this project has twice shipped a plausible-but-wrong platform claim). The **BLOCK** predicate on `Membership` stays `SpaceId`-only: the switcher never writes. `StatusDefinition` and `SpacePresence` are untouched. `NULL` on either side compares unknown, so an unset key admits nothing — assert it.
  - [ ] **EF query filter (the independent second layer, AD-2 / AR-6):** restate the same disjunction for `Membership` from application state: `membership.SpaceId == context.ScopedSpaceId || membership.AccountId == context.ScopedAccountId`, with `ScopedAccountId` read from a new request-scoped `AccountScope` (mirror `SpaceScope`: `Guid? AccountId`, internal `Narrow`/`Clear`, fails closed when null). `YelloDbContext`'s constructor gains it (`AccountScope? = null`, same fail-closed fallback as `SpaceScope`). **`PersistenceModelGateTests.Every_Space_scoped_entity_has_a_query_filter_from_application_state` requires the whole filter body to be `SpaceId == <context member>`** — widen it deliberately to accept exactly this one disjunction on exactly `Membership`, and plant a third disjunct and a `true ||` to show it still refuses anything else. `SpaceIsolationSchemaTests.Every_predicate_filters_on_the_SpaceId_session_context` asserts the predicate text: extend, do not delete — every filter predicate still contains the `SpaceId` clause, and `Membership`'s alone also contains the `AccountId` clause.
  - [ ] **`Space` names (Q1b, recommended):** `Space` stays policy-free (Lee's decision at 1.5 Q6, pending the AD-24 amendment). The switcher reads `Space.Id, Space.Name` only through a join from the Account-scoped `Membership` read, in one query, inside the Account-scoped unit of work — so which Spaces it can name is decided by the RLS-and-filter-protected table. Gate that `Space` is read nowhere else outside resolution and registration (Task 8), and record the variance.
  - [ ] **Port and adapter.** `Yello.Application/Spaces/AccountScopedContext.cs` (`record AccountScopedContext(Guid AccountId)`), a port such as `IAccountSpaceDirectory.ListAsync(AccountScopedContext, ct) → IReadOnlyList<SpaceIdentity>` where `SpaceIdentity(Guid Id, string Name)` has **exactly two** members. The adapter in `Yello.Infrastructure/Persistence/` opens a transaction, sets the `AccountId` key, narrows `AccountScope`, runs the one query, commits or rolls back, and disposes — same shape as `MembershipActiveSpaceResolver`. Order by `Name`, then `Id` (deterministic for duplicate names — display names are not unique, `sprint-change-proposal-2026-09-13.md:175`). No `IQueryable` leaves Infrastructure.
  - [ ] Register `AccountScope` **Scoped** and add it to `Space_resolution_state_is_registered_per_request` (or a sibling gate). No cache (`No_production_code_introduces_a_cache`).
  - [ ] Migration via the pinned tool (`dotnet tool restore`; `dotnet-ef` 10.0.11), then the **hand-editing pass** (`TESTING-CONVENTIONS.md:269-282`): file-scoped namespace, real XML docs, static column arrays, ≤ 80-line methods, **no `// <auto-generated />`**, literal object names. `Down` restores the 1.3 predicate exactly.

- [ ] **Task 3 — The switcher endpoint, Account-scoped by the pipeline** (AC: 1, 2, 3)
  - [ ] `GET /api/v1/spaces` → `200` with `[{ "id": …, "name": … }]` — `Yello.Contracts/Spaces/SpaceRoutes.cs` gains the constant; the DTO in `Yello.Contracts/Spaces/` carries **exactly** `Id` and `Name`. Nothing else, ever (I-11).
  - [ ] **The Account-scoped context is established by the pipeline, not the slice** (AR-3). Today `ActiveSpaceResolution.RequireAccountAsync` only 401s `{spaceId}` routes, and non-`{spaceId}` endpoints get no unit of work. Add a pipeline-owned marker (e.g. `.AccountScoped(AccountScopedSurface.SpaceSwitcher)` metadata, mirroring `RequiresCapability`) and a filter on the `api` group that, for marked endpoints, answers `401 authentication-failed` without a Session and otherwise builds the `AccountScopedContext` from the Session. The handler (`Yello.Application/Spaces/ListSpaces/`) receives the context; it never reads `HttpContext.User`.
  - [ ] **`AccountScopedSurface` is a closed enum of exactly the two AD-24 names**: `SpaceSwitcher`, `AccountSettings`. Only `SpaceSwitcher` is used in this story. A third member is an AD-24 amendment (gate, Task 8).
  - [ ] Session-only. CAP-35 (`acceptance-criteria.md:355`) forbids any API operation enumerating Spaces beyond a Token's Space, so this endpoint must refuse a Token — Tokens are story 1.8's; ledger the assertion there (Q9). Do **not** build Token handling here.
  - [ ] Not a refusal surface: an Account with no Memberships gets `200 []`, not a 404. No `AccessRefusal` is written by this endpoint, ever.
  - [ ] `SpaceScopedRouteCheck` must not mistake `/api/v1/spaces` for a `{spaceId}` route, and the A-4 gate (`RefusalBoundaryGateTests.No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity`) must still pass — the endpoint reaches `Membership`/`Space`, not the four named entities. Check `UnmatchedApiEndpoint`'s `/api/{**path}` fallback does not shadow it (a literal route outranks a catch-all; assert over the wire).

- [ ] **Task 4 — The context bar's data: the active Space's own answer** (AC: 4, 5, 10)
  - [ ] `GET /api/v1/spaces/{spaceId}` — Space-scoped, mapped through `MapApiGroup()`, `.RequiresCapability(Capability.ReadProjectsAndTasks)` (every Role holds it, so every member is served). Returns `{ "id", "name", "role" }` where `role` is the Role's stable code (`StableCode`, kebab form) — the client maps it to copy. A non-member or missing Space gets the boundary `404 not-available` and a `CrossSpace` record, exactly as `…/statuses` does (no new refusal code).
  - [ ] **Where the Role comes from (Q2, recommended):** from `ActiveSpaceContext` resolved on **this** request (AD-1). `RefusalBoundaryGateTests.Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` forbids a slice reading `Role`, deliberately. The endpoint that *reports the pipeline's own resolution* is pipeline-owned: put it in `Yello.Host/Spaces/` beside `ActiveSpaceResolution` and name it in that gate's permitted list with a one-line reason. It reads `Space.Name` by `context.SpaceId` inside the request's unit of work. Do not add a Role to the Session, a cookie, a claim or any client cache (`SessionContractTests` plants one).
  - [ ] The name crosses no boundary: it is the name of the Space the caller just proved Membership in. Logs carry `SpaceId`, never the name (AR-34).
  - [ ] Note the side effect: each call writes or refreshes the caller's `SpacePresence` row in that Space (1.6 decision 1). Switching therefore counts the Account toward the destination's 50-Session bound — correct, and a `409 bound-reached` here renders the bound copy on the refusal page.

- [ ] **Task 5 — Sign-out (Q4)** (AC: 7, 11)
  - [ ] **Recommended:** `DELETE /api/v1/sessions` → `SignOutAsync` on the cookie scheme, `204`, anti-forgery required (it is state-changing — `AntiforgeryRequirement` already covers unsafe methods on the group), idempotent for a caller with no Session (`204`, not `401`, so a double press is harmless — record it). Constant in `Yello.Contracts/Accounts/SessionRoutes.cs`.
  - [ ] Client: a **Sign out** button in the context bar. On press: the request, then the same purge path as `SessionState.SessionEndedAsync` (cancel in-flight → purge → navigate), landing on sign-in **without** `?reason=session-ended` (the person chose it; nothing ended under them). Give `SessionState` a sign-out entry that shares the purge and differs only in the destination — not a second purge implementation.
  - [ ] Rewrite the "NO SIGN-OUT CONTROL EITHER" paragraph in `MainLayout.razor` to describe what now exists, and close `deferred-work.md:149-151`.

- [ ] **Task 6 — The client: Space state, purge, routes** (AC: 4, 7, 9, 10, 11)
  - [ ] **Routes** (`Pages/ClientRoutes.cs`): add the Space home, e.g. `/spaces/{spaceId:guid}` (`SpaceHome(Guid)`), and a no-Space state, e.g. `/spaces` or a state of the Space-home resolver (Q6). `/` stays the entry point: signed in → fetch the switcher → navigate to the first listed Space (order as served) or to the no-Space state; not signed in → sign-in. `Home` keeps meaning "the entry point that resolves a switcher-sourced Space", so `RefusalSurface`'s link needs no change beyond its comment. **Never a remembered last Space** (UX-DR32, `EXPERIENCE.md:144`): nothing records which Space was last visited.
  - [ ] **Post-sign-in navigation.** `SignIn.razor`'s `Complete()` currently stays put ("story 1.7 owns the Space home"). Navigate to `Home` after `Session.SignedIn()`. Registration (`Register.razor`, `RegistrationPhase.cs:45`) does not sign in — leave it, and rewrite the comments that predict 1.7.
  - [ ] **The switcher response is the client's only list of Space ids.** A scoped client service (e.g. `Spaces/SpaceDirectory` holding the last `GET /api/v1/spaces` answer, refetched on sign-in, on opening the switcher, and on arriving at `/`) — **held in a field, never in browser storage** (`ClientStorageBanTests`). This list is Account-scoped identity, not Space-scoped content, so it survives a Space switch and is dropped on sign-out/session end.
  - [ ] **AC10:** the Space home checks its route id against the current switcher response **before** issuing `GET /api/v1/spaces/{spaceId}`. Absent → render the refusal (navigate to `NotAvailable` or render `RefusalSurface` in place — same words, layout and focus either way) and issue nothing. Present → issue the request; a `404`/`403`/`409` answer renders the refusal page with `RefusalCopy.For` (the deferred client refusal flow, `deferred-work.md:270`); `401` stays `YelloApiHandler`'s session-end path.
  - [ ] **Active-Space state with a synchronous purge (AC7).** A scoped client service (e.g. `Spaces/ActiveSpaceState`) holding the active Space's `{id, name, role}` from Task 4, plus a purge registry and an in-flight `CancellationTokenSource` **per Space** — the same shape and failure rules as `SessionState` (`RegisterPurge` → disposable handle; every purge runs even if one throws; cancel in-flight **first**). Triggers: (a) a navigation whose target Space id differs from the active one, or leaves Space routes — hook `NavigationManager.RegisterLocationChangingHandler`, which runs **before** the router renders the new location and covers link clicks, `NavigateTo`, and back/forward alike; (b) sign-out and session end — `SessionState`'s purge registers `ActiveSpaceState`'s. Purge = clear the held context and every registration, synchronously before any render of the destination. Today the registry holds only the context bar's own state; epic 2 registers its Board — say so in the remarks (the seam is near-empty and is asserted for what it **does**, the 1.4 lesson at `SessionState.cs:9-19`).
  - [ ] **Requests are linked to both lifetimes.** Space-scoped requests link to the active Space's token and the Session's, so a response for the departed Space cannot land after the purge (UX-DR30 by the other door, `SessionState.cs:52-67`).
  - [ ] **Each tab is independent** (`EXPERIENCE.md:93`): scoped services are per tab in WebAssembly; add nothing shared (no `BroadcastChannel`, no storage events).

- [ ] **Task 7 — The context bar, the Role chip, the switcher** (AC: 2, 4, 5, 6, 8, 11)
  - [ ] Components in `Yello.Client/Components/`: `ContextBar.razor`, `RoleChip.razor`, `SpaceSwitcher.razor`. The bar lives in `MainLayout.razor` **above `<main>`**, rendered on every authenticated surface. On a Space-scoped surface it shows the Space name, the switcher trigger, the Role chip and Sign out. On an authenticated surface with no active Space (the refusal page, the no-Space state) it renders its **shell** — switcher trigger and Sign out, no Space name and no Role (Q7; `EXPERIENCE.md:279`: "may render its shell before its contents, but never the Space name").
  - [ ] **Never scrolls away, collapses or hides behind a menu** (AC5, `DESIGN.md:427`, `:500`): `position: sticky; top: 0` (or the layout's equivalent), no hamburger, no breakpoint that removes the Role chip — UX-DR41: the chip survives every breakpoint; the drop order is the Project name (none yet), then the switcher's chevron label, never the Role.
  - [ ] **Role chip** (`DESIGN.md:169-174`, `:455`): a `<span>`, no `tabindex`, no handler, not a link or button; `--role-chip` fill on `--role-chip-on` text **with a hairline border** (the fill sits at 1.05 against the bar without it — X-8), `--radius-sm` (2px), `.type-role-label` (mono, uppercase by `text-transform`; the Turkish/Azeri/Greek exclusion in `base.css` already applies). Text from resources in **sentence case**: `RoleOwner` "Owner", `RoleAdmin` "Admin", `RoleMember` "Member", `RoleViewer` "Viewer".
  - [ ] **Context bar** (`DESIGN.md:162-168`, `:454`): `surface-card`, hairline border, `--radius-md` (3px), Space name in `--type-space-name-*`. Element: `<nav class="context-bar" tabindex="-1" aria-label="…">` (or a `role="region"` with `aria-label` — pick one and record it). **Accessible name** (AC6, UX-DR28) = `"{Space name} — {Role}, {what it permits}"`, built from resources with the name as a format argument (`ContextBarAccessibleName` = `"{0} — {1}, {2}"`), Role in sentence case. The four permit phrases (Q3, two are verbatim from the spec, two are derived): Owner "can manage everything in this Space, including ownership"; Admin "can manage Members and settings"; Member "can create and edit Projects and Tasks"; Viewer "read only in this Space". Source each phrase's capability claim from `RoleCapabilityMatrix` in a test, so a matrix change that makes a phrase false fails the build.
  - [ ] **Space switcher** (`EXPERIENCE.md:202-203`, `DESIGN.md:230-237`, `:470`): the Space name in the bar is the trigger (a `<button aria-expanded aria-controls>` whose name includes the Space name and "Switch Space"); it opens a list of the switcher response, one row per Space, each a link to `SpaceHome(id)` with the **name only** — no count, Role, badge or "current" marker beyond `aria-current="page"` on the active row (Q8). Rows meet `--target-min` height. `Esc` closes and returns focus to the trigger; choosing a row navigates. Refetch the list on open (it may have changed — 3.x/4.x). If the active Space becomes unavailable while open, the next Space-scoped answer routes to the refusal page (`EXPERIENCE.md:203`).
  - [ ] **Focus and announcement on switch (AC4, UX-DR39, `EXPERIENCE.md:402`):** after the destination's context arrives, focus the context bar element (not the trigger, whose name has changed and may not re-announce) and announce `"{Space} — {Role}, {permits}"` once through a polite live region (`role="status"`) in the bar. **Conflict to resolve:** `App.razor`'s `<FocusOnNavigate Selector="h1">` moves focus to the page `h1` after **every** navigation. For a Space switch the bar must win: either exclude Space-switch navigations from `FocusOnNavigate`, or focus the bar after the context arrives (which is after `FocusOnNavigate` fires) — and prove the final focus owner in the browser case. Initial arrival after sign-in or a deep link is a navigation, not a switch: `h1` focus stands there (record which arrivals count as a switch).
  - [ ] **Announcements carry no cross-Space information** (UX-DR38): only the destination's name and Role, never the departed Space.
  - [ ] **Copy** (UX-DR37): every string in `Localisation/ClientCopy.resx`, sentence case, no literal in a component (`No_user_visible_string_literal_appears_in_a_component` and its `@code` sibling). New keys: the four Role names, the four permit phrases, the accessible-name format, the switch announcement format, the switcher trigger label, the switcher list label, Sign out, the no-Space state heading/body (Q6). `LocalisationGateTests` keeps hand-maintained exact lists — every Role stable code needs copy; add a gate in the style of 1.6's "every capability code has copy".
  - [ ] **The Space home page** itself: `Pages/SpaceHome.razor`, an `h1` and nothing Space-scoped beyond what the bar shows — Projects are story 2.1. Its `h1` copy must not name the Space (the bar does; keep the name in one place). Suggest a neutral heading such as "Projects" with an empty-state line deferred to 2.1 — or state why none.
  - [ ] **Browser-owned surfaces (AC8, UX-DR33):** `document.title` stays the static `<title>Yello</title>` in `index.html` — no `<PageTitle>`, no `HeadOutlet` content, no `document.title` interop anywhere (gate it). **Scroll restoration** (Q10, recommended): `history.scrollRestoration = 'manual'` set once by an inline statement in `index.html` before `blazor.webassembly.js` loads — global, which covers every Space-scoped route; no new JS file. **`autocomplete="off"`:** no Space name input exists until story 3.1 (create/rename). `FormField.razor` already defaults `AutoComplete = "off"`; record that 3.1 owns the first real case and the "non-reusable field name" half, and do **not** write a gate that passes vacuously over zero inputs (ledger it to 3.1).

- [ ] **Task 8 — Architecture gates** (AC: 1, 2, 3, 7, 8)
  - [ ] **I-11 (static half):** the switcher response DTO and `SpaceIdentity` have exactly `Id` and `Name`; the switcher endpoint's handler reaches no `Project`, `Task`, `Label`, `StatusDefinition`, `SpacePresence` or `AccessRefusal` (IL reachability, the `ReferencesType` technique in `RefusalBoundaryGateTests`).
  - [ ] **AD-24's enumeration:** every endpoint carrying the Account-scoped marker names a member of `AccountScopedSurface`; the enum has exactly the two AD-24 members; no endpoint outside the marked set sets the `AccountId` key (only the Account-scoped adapter calls it — call-site location gate).
  - [ ] **One key per unit of work:** nothing that sets `SpaceId` also sets `AccountId` and vice versa (call-site gate on the two entry points, plus the wire test in Task 9).
  - [ ] **`Space` is read only where named:** the Account-scoped directory, Space resolution's name read (Task 4), and registration.
  - [ ] **Widened, not loosened:** the query-filter and predicate gates from Task 2, each planted.
  - [ ] **No `<PageTitle>`, no `document.title` interop, no `HeadOutlet` content** in `Yello.Client`; `index.html`'s `<title>` is exactly `Yello`; `index.html` sets `scrollRestoration` to `manual`.
  - [ ] **The Role chip is never interactive:** `RoleChip.razor` carries no `@onclick`, `href`, `tabindex`, `<button>` or `<a>` (a source-level gate is acceptable here; the browser case asserts it rendered).
  - [ ] **No remembered last Space:** no client field, query key or storage holding a "last"/"previous" Space id (extend `ClientStorageBanTests`' approach; name what the gate looks for exactly).
  - [ ] Plant every gate (Task 10).

- [ ] **Task 9 — Wire and database tests** (AC: 1, 2, 3, 4, 11)
  - [ ] **Prefer extending existing container-backed classes** — a full run already starts thirteen SQL Server engines (`deferred-work.md:284`) and memory headroom is gone (`TESTING-CONVENTIONS.md:284-364`). `ActiveSpaceEndpointTests` (has a Host and a database) takes the wire cases; `SpaceIsolationLayerTests` takes the layer cases; `PooledConnectionIsolationTests` (pool pinned to one) takes the pooled case. A new container class needs a stated reason in the Completion Notes.
  - [ ] **AC1/I-11:** seed (via `SpaceSeeding`) Account A with Memberships in Spaces S1 (Owner) and S2 (Viewer), Account B in S3; A's `GET /api/v1/spaces` returns exactly `{S1, S2}` with ids and names and no other property (assert the raw JSON's property set, not the deserialised DTO — a DTO drops unknown fields). B sees exactly `{S3}`. An Account with no Membership sees `[]`. Anchor on a success first, then the absence.
  - [ ] **Both layers independently (AD-2):** under the Account key, RLS alone (query filter bypassed via `IgnoreQueryFilters` in test code) returns only A's Memberships; the filter alone (no session key set — `OpenWithoutSpaceContextAsync`-style) returns only A's. Neither key set → nothing. Use two Accounts with different ids in every assertion (1.5 review lesson: never compare against a default).
  - [ ] **Pooled connection:** on one physical connection (`@@SPID` asserted equal), an Account-scoped read for A followed by a Space-scoped read for S3 (as B) discloses no A Membership; and the reverse order. The `AccountId` key is cleared by the pool reset as `SpaceId` is (1.5's 15664 test shape).
  - [ ] **One key per unit of work, over the wire:** a Space-scoped request on S1 by A must not see A's S2 Membership (e.g. the resolver's Membership read under S1's context returns exactly one row).
  - [ ] **AC4/I-10 (request path):** A calls `GET /api/v1/spaces/{S1}` → role `owner`; then `{S2}` → `viewer`; then `{S1}` → `owner` again — same Session cookie throughout, no re-authentication (CAP-2). Change A's Role in S2 in the database between two requests → the next answer reflects it (nothing cached, AD-1).
  - [ ] **Context endpoint refusals:** B on `{S1}` and anyone on a missing Space → the existing `not-available` body, byte-identical, one `CrossSpace` record each (extend, do not duplicate, 1.6's assertions).
  - [ ] **401s:** `GET /api/v1/spaces` without a Session → `401 authentication-failed`, no `AccessRefusal`. `DELETE /api/v1/sessions` → `204` and a following `GET /api/v1/sessions/current` → `401`; without an anti-forgery token → `400` (anchor on the 204 first).
  - [ ] Traits: `Suite`, `Priority` (I-11, I-10 → P0), `Requirement` citing AR **and** AD (`AR-29`/`AD-24`, `FR-9`), no `Assumption` unless a PRD §12 assumption is fixed by the test.

- [ ] **Task 10 — Client tests: logic, browser, and the plants** (AC: 4–11)
  - [ ] **Client logic** in `Yello.Tests.Architecture` (the only suite referencing `Yello.Client`; `SessionSeamTests`/`RefusalCopyTests` precedent): `ActiveSpaceState` purge runs every registration, cancels in-flight first, survives a throwing purge, runs on a Space-id change and on leaving Space routes and **not** on a same-Space navigation; the accessible name for all four Roles; every permit phrase agrees with `RoleCapabilityMatrix`; resource values for Roles are sentence case (not all-caps); the switcher row renders the name only.
  - [ ] **Browser** (`Yello.Tests.Client`, Q5 recommended: Playwright `page.RouteAsync` fakes for `/api/v1/*`, no Host, no database, no new container): (1) sign-in submits and lands on the first switcher Space with the context bar showing name and chip — **this is the first browser-driven sign-in** (`deferred-work.md:153`), faked at the network edge, so record precisely what it does not prove (the real cookie across origins, the real preflight); (2) the switcher lists exactly the faked rows, names only; (3) choosing a row moves focus to the context bar and the polite region announces the destination's name and Role; (4) back navigation purges — after Back, nothing of the departed Space's context is in the DOM before the destination's answer arrives (hold the faked response with a `TaskCompletionSource`, assert, then release — **no `Task.Delay`**); (5) a route id absent from the faked switcher response renders the refusal page and **no** request for that id is seen by the router; (6) the two 404 answers (a faked `not-available` for a listed-but-revoked Space, and an unlisted id) render the same page, words and focus (`deferred-work.md:270`); (7) the route back lands on a switcher Space; (8) the Role chip is a bordered, non-focusable, non-interactive element (X-8: computed `border-width` > 0, not in the tab order); (9) `document.title` is `Yello` on a Space route and `history.scrollRestoration` is `manual`; (10) Sign out lands on sign-in with no `reason`. Run the Space-route cases in at least `en-GB` and `tr-TR` (the uppercase exclusion: the chip must not uppercase in Turkish).
  - [ ] Fakes must answer exactly the shapes the Host answers — reuse the `Yello.Contracts` types to serialise them, so a contract change breaks the fake too.
  - [ ] **Plants** — every absence assertion gets a planted violation, fired by name, recorded in a table (1.5/1.6 shape). Suggested: the switcher DTO gains a `role`; the switcher query drops the Membership join and lists every Space; the `Membership` filter disjunct widened to `true`; the RLS predicate's `AccountId` clause removed (RLS-alone case must then fail); a Space-scoped unit of work also sets `AccountId`; the context endpoint returns a cached Role; `ActiveSpaceState` purge skipped on Back; purge after render instead of before; a client request issued for an unlisted id; a `<PageTitle>` naming the Space; the Role resource written as `VIEWER`; the Role chip given `tabindex="0"`; the route back pointed at a stored last Space; a third `AccountScopedSurface` member.
  - [ ] **Never `git checkout --` / `git restore` / `git reset` to revert a plant.** Hold the original in memory; restore in a `finally`; compare file hashes against a pre-run snapshot after each plant (1.6's run lost a `finally` to a reaped shell — run plants in the foreground).

- [ ] **Task 11 — Housekeeping and the ledger** (AC: all)
  - [ ] **SPEC copy defect (Q11, recommended):** correct `_bmad-output/specs/spec-yello/acceptance-criteria.md:161` to quote "Not available." with a pointer to UX-DR32 / `EXPERIENCE.md:104`, keeping the CAP-15 reasoning ("names neither possibility") intact; and annotate `:162` that the "create a Space" offer arrives with story 3.1 (no creation path exists in Epic 1). SPEC.md:22 says report divergences rather than resolve them silently — the correction is the report, recorded in the Completion Notes and the ledger entry's closure.
  - [ ] Rewrite every comment that predicts this story so it describes what exists: `MainLayout.razor`, `ClientRoutes.cs` (`Home`, the class remarks), `RefusalSurface.razor:20`, `ClientCopy.resx` (`RefusalRouteBack` comment), `SignIn.razor:80`, `SignInPhase.cs:23`, `RegistrationPhase.cs:45`, `Register.razor:29-35`, `SessionState.cs:9-19`, `YelloApiHandler.cs` (the untested-401 note), `Yello.Client/AssemblyMarker.cs`, `InitialSchema.cs:198-207` (do not edit an applied migration's code; its **comment** may be corrected or left with the new migration's remarks superseding it — record which), `YelloDbContext.cs` remarks, `SessionContractTests.cs:249`, `SessionSeamTests.cs:24`, and whatever else a repository grep for `1.7` finds.
  - [ ] Close or rewrite the six `deferred-work.md` entries this story owns; add new entries with owners for what it defers — at least: the Token half of I-11 → **1.8**; the real-Host browser sign-in (cookie across origins, preflight) → **1.9** if Q5 is taken as recommended; the `autocomplete`/non-reusable-name gate → **3.1**; the AD-24 amendment's `Space` predicate question → the amendment (before Epic 3).
  - [ ] Fix the stale `TESTING-CONVENTIONS.md:465-479` "No E2E / browser project exists. Blocker B5 … is open" — it contradicts `:120-138`. Update the browser-level section with the `RouteAsync` convention if adopted.
  - [ ] **Do not add review narration to source files** (owned by Lee, project-wide). Do not tick this unless the diff holds it.
  - [ ] Logs: `[LoggerMessage]` partials only (CA1848/CA1873); continue `SpaceLog` EventIds after the last 1.6 used. `SpaceId`/`AccountId` as fields, never a Space name, email or cookie (AR-34).

## Dev Notes

### The shape of the story in one paragraph

Stories 1.5 and 1.6 built a Space-scoped pipeline that can only ever see one Space at a time, and a client that, once signed in, has nowhere to go. This story adds the **one** sanctioned read across Spaces — the switcher's list, run under a second session-context key (`AccountId`) that widens exactly one table's filter (`Membership`) in both RLS and the EF filter — and the **one** endpoint that reports the active Space's own resolution (name and Role) to the client. On the client it adds the context bar, the Role chip, the switcher, a Space home, sign-out, and a per-Space state service whose purge runs before any render on switch, sign-out and back/forward. It also closes six ledger entries 1.3–1.6 left here. Epic 3 is where several Spaces actually stress this (`implementation-readiness-report-2026-08-22.md:969-972`); here it is built and proved with seeded Memberships.

### Current state of the files this story changes — read each before editing

- **`Yello.Infrastructure/Persistence/SpaceSessionContext.cs`** — the only `sp_set_session_context` line in production (`@key` = `SchemaNames.SpaceIdSessionKey`, `NVarChar(128)`; `@value` `Variant`; `@read_only = 1`). Key is hard-wired. Setting a key twice on one connection fails with **15664**; the pool clears keys (both verified in 1.5). `@read_only` is per key, so a second key on a fresh connection is fine — but Task 2 forbids both keys in one unit of work anyway.
- **`MembershipActiveSpaceResolver.cs`** — set-then-verify: `BeginTransactionAsync` → `SetAsync(spaceId)` → `spaceScope.Narrow` → read Role for `(AccountId, SpaceId)` → resolve or clear+rollback. **Preserve.** Its Membership read under the widened filter still sees one row, because only `SpaceId` is set.
- **`AccountRegistrationStore.cs`** — sets `SpaceId` for the new personal Space. Unaffected; must not start setting `AccountId`.
- **`YelloDbContext.cs`** — `(DbContextOptions, SpaceScope? = null)`, `ScopedSpaceId`; explicit configuration list. `Configurations/MembershipConfiguration.cs` holds `HasQueryFilter(m => m.SpaceId == context.ScopedSpaceId)`.
- **`SchemaNames.cs`** — `SpaceScopedTables = [Membership, StatusDefinition, SpacePresence]`, `SpaceIdSessionKey`, `SpacePredicateFunction`, `SpaceIsolationPolicy`.
- **Migration `20260828164716_InitialSchema.cs:219-240`** — `fn_SpaceIsolationPredicate(@SpaceId)` and `SpaceIsolationPolicy` with FILTER+BLOCK on `Membership` and `StatusDefinition`; 1.6's migration added `SpacePresence`. Its remarks at `:198-207` predict this story.
- **`Yello.Host/ApiRouteGroup.cs`** — the `api` group: `RequireAccountAsync` (401 for `{spaceId}` routes only) → `AntiforgeryRequirement.ValidateStateChangingAsync` → `ActiveSpaceResolution.ResolveAsync` (passes non-`{spaceId}` routes straight through). Every endpoint is stamped `ResolvedBySpacePipeline`. **Preserve the order**; the Account-scoped filter is new and handles only marked endpoints.
- **`Yello.Host/Spaces/ActiveSpaceResolution.cs`** — 401 → resolve → 404+`CrossSpace` record, or capability check → 403+`InsufficientRole` record → presence bound (409) → endpoint → materialisation → commit on success only. **Preserve all of it.**
- **`Yello.Host/Program.cs`** — `MapApiGroup()` then `MapRegisterAccount`, `MapAuthenticate`, `MapCurrentSession`, `MapAntiforgeryToken`, `MapListStatuses`; `MapUnmatchedApi` on `app`. No `AddAuthorization`.
- **`Yello.Host/Endpoints/SessionEndpoints.cs`** — sign-in, current session, anti-forgery token. Sign-out goes here.
- **`Yello.Client/Layout/MainLayout.razor`** — `<main>@Body</main>`; long comment on what is absent (context bar, switcher, chip, sign-out). The bar goes above `<main>` (a skip link becomes meaningful once there is something to skip — `MainLayout` says so; consider it, record the decision).
- **`Yello.Client/App.razor`** — `Router`, `FocusOnNavigate Selector="h1"`, `NotFound` → `RefusalSurface` in `MainLayout`.
- **`Yello.Client/Pages/SignIn.razor`** (`@page "/"` **and** `"/sign-in"`) — `/` is sign-in today. This story moves `/` to the entry resolver; sign-in keeps `/sign-in`. Update `ClientRoutes` remarks and the `SignInAfterSessionEnd` path stays `/sign-in?reason=session-ended`.
- **`Yello.Client/Sessions/SessionState.cs`** — `RegisterPurge`, `SessionEndedAsync` (cancel in-flight → purge each, swallowing failures except OOM → announce in `finally`), `LinkToSession`. Takes no framework type, deliberately — keep it that way; navigation is a callback the composition root supplies.
- **`Yello.Client/Sessions/YelloApiHandler.cs`** — credentials include; anti-forgery header on unsafe methods; links to the session token; 401 while signed in → `SessionEndedAsync`.
- **`Yello.Client/Program.cs`** — builds `HttpClient` over `YelloApiHandler`, wires the announce to `NavigateTo(SignInAfterSessionEnd)`, probes `GET /sessions/current` at boot (5 s) and calls `SignedIn()` on 204, applies `DocumentLanguage`.
- **`Yello.Client/Components/RefusalSurface.razor`**, **`Refusals/RefusalCopy.cs`** — one page for every not-found, `h1` focused on first render, one link to `ClientRoutes.Home`; `RefusalCopy.For(problem, hasCopy)` → `RefusalMessage(Key, Limit)`.
- **`Yello.Client/wwwroot/index.html`** — static `<title>Yello</title>`, `lang="en"` pre-boot. **`wwwroot/css/tokens.css`** has `--role-chip`, `--role-chip-on`, their `-light` siblings, `--type-role-label-*`, `--type-space-name-size`; **`base.css`** has `.type-role-label` with the locale casing exclusions and the target floors that name "the Role chip and the context bar"; **`components.css`** has no bar/chip classes yet.

### Architecture constraints that bind here

- **AD-24 / AR-29** (`ARCHITECTURE-SPINE.md:218-222`, `epics.md:217`): exactly two Account-scoped surfaces — switcher and Account settings — under `AccountScopedContext` with predicate `SESSION_CONTEXT('AccountId')`, never a disabled policy, never a raw connection; Space **identity** (id, name) only. A third surface is an AD amendment.
- **AD-1** (`:71-75`): Role from the Membership row on **this** request only; never a cookie, claim, Token payload or cache outliving the request. The context bar's Role is therefore fetched per Space arrival, never carried.
- **AD-2 / AR-5 / AR-6** (`:81-86`, `epics.md:193-194`): session context set at the start of every unit of work, never once per connection, never from a client value; the EF filter is an **independent** second layer; raw SQL bypassing filters only inside Infrastructure.
- **AD-3 / AR-9** (`:88-94`): `{spaceId}` the first segment after the version on every Space-scoped route; boundary 404 identical for both causes; error bodies carry no Space name.
- **AD-4** (`:100`): browser and Token requests differ only in how `AccountId`/`SpaceId` are established — and CAP-35 removes the switcher from the Token surface entirely.
- **AD-7 / AR-14**: no credential in web storage; UX-DR31 extends the ban to Space-scoped content in IndexedDB too.
- **AR-3**: authorisation, Space resolution, refusal recording, idempotency and bound checks — and, by extension, establishing the Account-scoped context — live in the pipeline, never a slice.
- **AR-34**: problem+json; logs carry ids, never names.
- Conventions (`:265-277`): slices `Yello.Application/{Area}/{UseCase}/`; routes `/api/v1/…` with `v1` a literal (8.2 owns versioning, `Asp.Versioning.Http` stays unreferenced); Guid ids via the sequential generator; `DateTimeOffset` UTC; no synonyms (never Tenant/Workspace/Org/Team; `User` only for an Account acting in a Space — the switcher and sign-out are Account-level, so their copy says "Account" where it says either).
- **AD-21 ring rule:** `Domain` references nothing; `Application` no ASP.NET Core/EF; adapters `internal` in Infrastructure (`InternalsVisibleTo("Yello.Tests.Slices")`). No new project reference (`AllowedReferenceEdges` is an exact set).

### UX — exact behaviour and copy

| Element | Rule | Source |
|---|---|---|
| Context bar | Always present once authenticated; never scrolls away, collapses, or hides behind a menu; names the active Space; shell-only before contents and on Space-less surfaces | `EXPERIENCE.md:48`, `:202`, `:279`; `DESIGN.md:427`, `:454`, `:500` |
| Role chip | Owner / Admin / Member / Viewer verbatim; mono; uppercase by `text-transform`; **bordered**; 2px radius; display-only, never a link or menu | `EXPERIENCE.md:81`, `:205`; `DESIGN.md:169-174`, `:455` |
| Accessible name | `"{Space} — {Role}, {permits}"`; sentence case in resources | `EXPERIENCE.md:83`, `:373`; `DESIGN.md:395-400` |
| Switcher | Space name opens it; rows = name only; the only such list in the product | `EXPERIENCE.md:65`, `:202-203`; `DESIGN.md:470`; `addendum.md:68` |
| Switch | Entire surface changes, nothing carries over; focus → context bar; new Space + Role announced politely | `EXPERIENCE.md:91`, `:402`; UX-DR39 |
| Purge | Before any render; no Space-scoped content in web storage; no optimistic first read; no request for an unlisted id | `EXPERIENCE.md:158-168`, `:146`; UX-DR31 |
| Browser-owned | `document.title` fixed `Yello`; scroll restoration disabled on Space routes | `EXPERIENCE.md:170-179`; UX-DR33 |
| Refusal | "Not available." page, route back to a switcher Space, never a remembered one | `EXPERIENCE.md:104`, `:132-147`; UX-DR32 |
| No Space | Valid state; offer to create, never auto-create (creation is 3.1) | `EXPERIENCE.md:253`; UX-DR35 |
| Tabs | Each tab holds its own Space context; tabs share no state | `EXPERIENCE.md:93` |

Only the Admin and Viewer permit phrases exist in any document (`EXPERIENCE.md:83`, `epics.md:272`, `:826`); punctuation varies across sources (`:506` drops the em dash; `review-accessibility.md:78` uses "Viewer — read only"). Use the AC's form. Owner and Member are derived (Q3). Note that "can manage Members and settings" glosses the matrix loosely (an Admin changes only Member↔Viewer and cannot transfer or delete) — it is the spec's own string; keep it verbatim.

### Previous story intelligence (1.6, and 1.5 where still live)

- **The A-12 Role gates are deliberate**: `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` and `No_slice_or_endpoint_reads_the_Role_off_a_Membership_row`. The context endpoint is the first legitimate non-pipeline consumer of the Role — put it in the pipeline's namespace and permitted list, do not weaken the gate's predicate.
- **`ActiveSpaceUnitOfWorkTests` builds a bare `ServiceCollection`** (l.141-145) and breaks whenever `ResolveCoreAsync` resolves a new service — register fakes there if the pipeline grows.
- **`HostProcess` raises the rate limit to 1,000,000 by default**; wire tests are unaffected by the PBKDF2 limiter.
- **`SpacePresenceRegister`** catches the PK race (2627) and admits; every Space-scoped request refreshes presence.
- **`RefusalSurfaceTests`** (browser, `Yello.Tests.Client`) already proves the unknown-route and `/not-available` pages focus their `h1`; extend it rather than starting a parallel class.
- Review lessons (1.5, 1.6): gates asserted less than their names; a sorted-vs-unsorted comparison made a test fail half the time (`ActiveSpaceEndpointTests:335`); a plant run killed by the harness for low memory left a planted edit behind. **Write each gate so its name is exactly what it checks, plant it, run plants in the foreground, hash-compare after each.** Assertions never compare against a default; anchor every refusal or absence on a success first.
- Analyser traps (still current): `S2339` no `public const`; `S109` magic numbers → named consts; `S4457`; `MA0182`; banned `DateTimeOffset.Now`; `S1135` — the word for the first default Status cannot appear in a comment; `S1200`/`S1541` complexity splits; hand-maintained exact-set gates (`PackageVersionPinTests`, `AllowedReferenceEdges`, `MsBuildEvaluation.PropertyNames`, `LocalisationGateTests`). Warnings are errors. `--no-build` runs zero tests.
- **Run the suites project by project in the foreground** if the full `dotnet test Yello.slnx` misbehaves; check memory first. Rancher Desktop recovery: `rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch. A stopped runtime gives **failures**, not skips. The browser suite starves under twelve-plus engines; run it alone to confirm.

### Git intelligence

Recent: `3a61d32` (1.6 review: presence PK race, refusal-surface focus, A-4 transitive walk, `Membership.Role` gate, `RefusalSurfaceTests`), `57ca9bd` (1.6: 62 files — capabilities, matrix, `AccessRefusal`, `BoundRegistry`, `SpacePresence`, rate limit, `ApiRouteGroup`, refusal page), `d005823` (1.6 ATDD scaffolds), `9118277` (story 1.6 file). Pattern: one commit per phase, thesis-sentence subject, straight to `main`; the orchestrator commits, the dev phase does not. No new packages in the last five commits — rate limiting and Playwright were both already available; this story should need none either (`page.RouteAsync` is in `Microsoft.Playwright`, already pinned).

### Latest technical notes

- **SQL Server RLS:** a security policy's predicate is altered with `ALTER SECURITY POLICY … ALTER FILTER PREDICATE <new function>(cols) ON <table>`; a predicate function must be an inline TVF `WITH SCHEMABINDING`, and a function in use by a policy cannot be dropped or altered until the policy stops referencing it (so create the new function, then alter the policy; `Down` reverses). Check whether more than one filter predicate on the same table across enabled policies is permitted before choosing a second policy — the recommendation avoids the question by replacing the predicate in the existing policy. Confirm each statement against the SQL Server 2025 docs; do not rely on memory.
- **`SESSION_CONTEXT`**: `sp_reset_connection` (pool reuse) clears all keys; a `@read_only = 1` key cannot be reset on the same session (15664). An unset key returns `NULL`, and `NULL = x` is unknown — an unset key admits nothing.
- **Blazor (.NET 10)**: `NavigationManager.RegisterLocationChangingHandler(Func<LocationChangingContext, ValueTask>)` runs before navigation commits, for internal navigations including browser back/forward on the same app; `context.PreventNavigation()` exists but is not wanted here. It runs **before** the router renders the destination — which is what "before any render" needs. `FocusOnNavigate` focuses after the routed page renders. `[SupplyParameterFromQuery]` binds query keys.
- **Playwright for .NET**: `page.RouteAsync("**/api/v1/spaces", route => route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = … }))`; record requests with `page.Request +=` to assert that an unlisted id was never requested. Hold a response by awaiting a `TaskCompletionSource` inside the handler. Computed style via `locator.EvaluateAsync("e => getComputedStyle(e).borderTopWidth")`.
- **Cookie sign-out:** `HttpContext.SignOutAsync(<scheme>)` writes an expired cookie with the same attributes (`SameSite=None; Secure; HttpOnly`) — assert the `Set-Cookie` clears it.

### Project structure notes

**New, most likely:** `Yello.Application/Spaces/{AccountScopedContext,IAccountSpaceDirectory,SpaceIdentity}.cs`, `Yello.Application/Spaces/ListSpaces/ListSpacesHandler.cs`; `Yello.Infrastructure/Persistence/{AccountScope,AccountSpaceDirectory}.cs`, one migration; `Yello.Host/Spaces/{AccountScopedSurface,AccountScopedResolution,ActiveSpaceContextEndpoint}.cs` (names indicative), `Yello.Host/Endpoints/SpaceDirectoryEndpoints.cs`; `Yello.Contracts/Spaces/{SpaceIdentityResponse,ActiveSpaceResponse}.cs`; `Yello.Client/Spaces/{SpaceDirectory,ActiveSpaceState}.cs`, `Yello.Client/Components/{ContextBar,RoleChip,SpaceSwitcher}.razor`, `Yello.Client/Pages/{SpaceHome,EntryRedirect or NoSpace}.razor`; CSS in `components.css`; tests as Tasks 8–10 say.
**Modified, most likely:** `SpaceSessionContext.cs`, `SchemaNames.cs`, `YelloDbContext.cs`, `MembershipConfiguration.cs`, `InfrastructureServices.cs`, model snapshot, `ApiRouteGroup.cs`, `Program.cs`, `SessionEndpoints.cs`, `SpaceRoutes.cs`, `SessionRoutes.cs`, `MainLayout.razor`, `App.razor`, `SignIn.razor`, `ClientRoutes.cs`, `RefusalSurface.razor` (comment), `SessionState.cs`, `Program.cs` (client), `index.html`, `ClientCopy.resx`, `components.css`, the gates named in Tasks 2 and 8, `deferred-work.md`, `TESTING-CONVENTIONS.md`, `acceptance-criteria.md` (SPEC companion, Q11), `sprint-status.yaml`.
**Variance to record:** `Space` stays policy-free (the spine's AD-24 reads as if every Account-scoped read were RLS-guarded; here the guard is the `Membership` join). `/` changes meaning from sign-in to the entry resolver.

### Testing requirements

Traits: `Suite`, `Priority`, `Requirement` (AR **and** AD, e.g. `AR-29`/`AD-24`), `Assumption` only for PRD §12. One behaviour per test, sentence-named. Randomised emails (`SpaceSeeding.NewAccount()`), never a shared literal. No `Task.Delay` — wait on the condition (`TaskCompletionSource`, Playwright auto-waiting). Cleanup by container disposal, never delete statements. Container classes: `IClassFixture<MigratedDatabaseFixture>`, each test opens with `Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason)`. Wire tests boot the real Host via `HostProcess.RunAsync` over HTTPS with `WireSession` (cookies by hand; re-fetch the anti-forgery token after sign-in). xunit.v3 `Assert` only. The browser suite needs `dotnet build Yello.slnx` first (it serves the build with `--no-build`). **Re-baseline before starting**: 1.6 closed at 324 total / 323 passed / 1 skipped, and its code review added cases (`RefusalSurfaceTests`, a registration 429, a backwards-clock purge, the `Membership.Role` gate) — count them from a run, do not assume.

### References

- Story and ACs — `epics.md:792-836`; coverage `epics.md:350`, `:302`, `:408`; epic notes `:421`, `:427`; AR-29 `:217`; UX-DR9 `:247`, UX-DR28 `:272`, UX-DR31 `:275`, UX-DR32 `:276`, UX-DR33 `:277`, UX-DR35 `:279`, UX-DR37–42 `:281-286`; neighbours 1.6 `:787-790`, 1.9 `:912-915`, 3.2 `:1479-1529`
- Spine — AD-1 `:71-75`, AD-2 `:81-86`, AD-3 `:88-94`, AD-4 `:100`, AD-7 `:118`, AD-19 `:192`, AD-24 `:218-222`, AD-26 `:234`, conventions `:265-277`, capability map `:408-411`
- PRD — FR-9 `prd.md:243-250`, FR-15 `:321-330`, NFR-1 `:683-689`, NFR-9 `:753-757`, §7 `:798-811` (`:809` is the stale "only surface" sentence, readiness issue 8), UJ-4 `:110`; `addendum.md:68`
- SPEC — CAP-9 `SPEC.md:68-70`, `acceptance-criteria.md:94-98`, CAP-2 `:25-27`, CAP-15 `:160-162`, CAP-35 `:355`; `surfaces-and-journeys.md:53-64`, `:112-128`; `glossary.md:8`, `:13-16`, `:28`; `role-capability-matrix.md:9-26`
- UX — `EXPERIENCE.md:28`, `:48-65`, `:79-93`, `:101-147`, `:158-191`, `:202-205`, `:244-253`, `:279`, `:370-405`, `:413-419`, `:435`, `:497-509`; `DESIGN.md:162-174`, `:230-237`, `:395-406`, `:427`, `:454-470`, `:495-501`
- Tests — `test-design-qa.md:267`, `:274-276`, `:347-369`, `:545-590`; `test-design-architecture.md:597-598`; handoff `test-design/YelloBMAD-handoff.md:54`, `:109-130`; `tests/TESTING-CONVENTIONS.md` whole, esp. `:9-42`, `:44-72`, `:86-118`, `:120-233`, `:269-364`, `:465-479` (stale)
- Readiness `implementation-readiness-report-2026-08-22.md:723-727`, `:774-803`, `:969-972`, `:1222-1229`, `:1271-1275`. Sprint change proposal 2026-09-13: display names are not unique (`:175`) — order the switcher by name then id.
- Ledger — `deferred-work.md:92-96`, `:149-157`, `:203`, `:268-282`, `:284`
- Previous stories — `1-6-refuse-at-the-space-boundary-and-record-every-refusal.md` (Q13, Q14, decisions, review findings), `1-5-…md:210-220` (Options A/B — Option B is this story's Q1)

### Open questions — each with a recommended decision

The dev phase takes these as **reversible, recorded decisions** in the Completion Notes. None blocks starting.

1. **How `Membership` becomes readable across Spaces (and only for the switcher).** (a) Widen `Membership`'s RLS filter predicate to `SpaceId = SC('SpaceId') OR AccountId = SC('AccountId')` and its EF filter to the same disjunction, set exactly one key per unit of work, BLOCK unchanged; (b) a second security policy on `Membership` keyed on `AccountId` — may not be permitted alongside the first filter predicate, and splits one table's isolation across two policies; (c) a separate read model/view — a second read path AD-24 exists to prevent. **Recommend (a)** — it is 1.5's Option B, which 1.5 deferred to this story precisely because 1.7 needs it regardless (`1-5-…md:215`). **1b — `Space` names:** keep `Space` policy-free (Lee, 1.5 Q6) and reach names only through the Membership join; the alternative (an `EXISTS`-over-Membership predicate on `Space`) is the AD-24 amendment's to decide before Epic 3 — ledger it there.
2. **Where the context bar's Role comes from.** (a) A pipeline-owned `GET /api/v1/spaces/{spaceId}` reporting `ActiveSpaceContext` (name, Role code), named in the A-12 gate's permitted list; (b) a slice reading the Role — fails the A-12 gate by design; (c) Role in the switcher response — forbidden by AD-24 (identity only). **Recommend (a).**
3. **The Owner and Member permit phrases**, which no document defines. **Recommend** Owner "can manage everything in this Space, including ownership"; Member "can create and edit Projects and Tasks"; Admin and Viewer verbatim from the AC. Each derived phrase is asserted against `RoleCapabilityMatrix` in a test. Lee may reword; the test then holds the new words to the matrix.
4. **Sign-out**, which has no FR/CAP/journey but which AC7 and UX-DR31 presuppose (`deferred-work.md:149`). (a) Build `DELETE /api/v1/sessions` + a Sign out button in the context bar, sharing `SessionState`'s purge; (b) client-only purge with no server call — leaves a live cookie behind, which is a sign-out that is not one; (c) defer — leaves AC7's sign-out clause untestable. **Recommend (a)**, the smallest real sign-out; idempotent `204`.
5. **How the browser proves a signed-in flow.** (a) Playwright `page.RouteAsync` fakes for `/api/v1/*` in `Yello.Tests.Client`, serialised from `Yello.Contracts` types, no container; (b) a real Host + database inside `Yello.Tests.Client` — a visible architecture edit (ring row change, +1 engine on a machine already out of headroom); (c) no browser case. **Recommend (a)**, and re-own the real-Host browser sign-in (cookie across origins, preflight) to **1.9**, which owns the browser surface of the isolation suite.
6. **The no-Space state.** An Account with no Membership cannot arise in Epic 1 (registration always provisions one; leaving and deletion are epics 3–5), but the entry resolver must handle `[]`. **Recommend** a minimal page — heading and one sentence ("You do not belong to any Space yet.") with no create button until 3.1 builds creation, and no auto-create. Ledger the create offer to 3.1 (`acceptance-criteria.md:162`, UX-DR35).
7. **The context bar on authenticated surfaces with no active Space** (refusal page, no-Space state). AC5 says "any authenticated surface" yet the bar names the *active* Space. **Recommend** the shell (switcher trigger + Sign out, no name, no chip) — the only reading that satisfies AC5 and `EXPERIENCE.md:279` together. On unauthenticated surfaces (sign-in, register) no bar.
8. **Switcher interaction model.** (a) A non-modal disclosure: trigger button (`aria-expanded`), a list of links, `Esc` closes and returns focus, choosing navigates; (b) a modal dialog with a focus trap (`review-accessibility.md:96` treats it as a dialog). **Recommend (a)** — fewer moving parts, links work with middle-click/new-tab (each tab is independent anyway); record that UX-DR39's switch focus rule applies after navigation in either model. Mark the active row with `aria-current="page"` only — no visual badge.
9. **I-11 on the Token surface** (AD-4 × CAP-35). **Recommend:** this story builds the switcher Session-only and asserts nothing about Tokens; ledger to **1.8** "a Token request to `GET /api/v1/spaces` is refused (CAP-35), and the switcher is unreachable from the API surface" — which is I-11's "×2".
10. **Scroll restoration.** (a) `history.scrollRestoration = 'manual'` inline in `index.html`, global; (b) set only on Space-scoped routes via interop — needs a JS file the repo does not have, or an `eval`-shaped call. **Recommend (a)**: global satisfies "disabled on Space-scoped routes", costs nothing on the three non-Space routes, and adds no JS file.
11. **The SPEC refusal copy** (`acceptance-criteria.md:161`, `deferred-work.md:282`). (a) Correct the SPEC companion to "Not available." citing UX-DR32, keeping the reasoning; (b) implement the SPEC's wording — contradicts the epic's AC, UX-DR32, 1.6's shipped copy and its gate (`The_refusal_copy_is_the_clients_own…` asserts "Not available."); (c) leave both. **Recommend (a)**, recorded as the divergence report SPEC.md:22 asks for, and close the ledger entry.

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-1-7-list-and-switch-between-the-spaces-you-belong-to.md` (ATDD run 2026-10-24). It holds the scenario map, the seed constraints checked, the plant results and the scenarios deliberately left for this story to write (the widened `PersistenceModelGateTests`/`SpaceIsolationSchemaTests` gates, the call-site gates, the `ActiveSpaceState` purge unit cases, A-12's permitted list).

| File | Cases | ACs | Green today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.Switcher.cs` (new partial of the existing class) | 7 | AC1, AC2, AC3, AC4 | none |
| `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/SessionEndpointTests.cs` (+3; `WireSession.DeleteAsync` added) | 3 | AC11 | none |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceIsolationLayerTests.cs` (+4) | 4 | AC3 | `A_Space_scoped_unit_of_work_sees_only_its_own_Membership_of_an_Account_in_several_Spaces` - a standing gate. It cannot be planted until the widening exists; plant it then |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/PooledConnectionIsolationTests.cs` (+1) | 1 | AC3 | none |
| `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.cs` (new, no container) | 10 | AC2, AC3, AC5, AC6, AC8, AC9 | `No_client_code_sets_the_document_title`, `No_client_code_remembers_a_last_Space` (both planted, both caught) and their two detector checks |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs` (+1; A-4's walk split into `HandlerReaches`) | 1 | AC3 (I-11 static) | none |
| `tests/Yello.Tests.Client/SpaceSwitchingTests.cs` + `FakeYelloApi.cs` (new, existing collection, no container) | 13 (two theories) | AC1, AC2, AC4, AC5, AC7, AC8, AC9, AC10, AC11 | `A_faked_API_answers_the_client_across_origins_with_credentials` - the `RouteAsync` harness, planted (CORS removed -> caught) |

Every skipped case is `Skip = "red: AC<n> - <why>"`. Types, routes and copy keys this story has not written are reached by name, literal or `ResourceManager`, so all of it compiles today. Replace each with the typed reference on activation. No new container-backed class, so activation adds no engine.

**Activation order:**
1. `Under_the_AccountId_key_row_level_security_alone_admits_exactly_that_Accounts_Memberships`. It tests the riskiest assumption first: that one `ALTER SECURITY POLICY … ALTER FILTER PREDICATE` on `Membership` (Q1a) admits exactly the Account's rows. Then `Under_the_AccountId_key_no_other_Space_scoped_table_is_visible`, `A_reused_connection_carries_no_Account_key_into_a_Space_scoped_read_or_the_reverse` and `The_query_filter_alone_admits_only_the_scoped_Accounts_Memberships`.
2. Plant the standing layer gate, both halves.
3. `The_switcher_response_and_the_Space_identity_carry_exactly_an_id_and_a_name`, `The_Account_scoped_surfaces_are_exactly_the_two_AD_24_names` and `The_switcher_endpoint_reaches_no_Space_scoped_content`. Then the four switcher wire cases.
4. The three active-Space wire cases.
5. The three sign-out cases. The Session is a stateless cookie, so a replayed pre-sign-out cookie is not refused. Record that decision.
6. Client cases: first `A_Space_id_the_switcher_did_not_list_renders_the_refusal_page_and_is_never_requested` and `Back_navigation_purges_the_departed_Space_before_the_destination_answers`, then the rest.
7. Copy, chip and browser-owned-surface cases.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-10-24).

### File List
