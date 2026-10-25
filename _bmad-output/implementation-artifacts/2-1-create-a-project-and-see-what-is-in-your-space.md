---
baseline_commit: 311d1d50632b7ec192c27dac270179c0e84c0ce3
---
# Story 2.1: Create a Project and see what is in your Space

Status: ready-for-dev

Epic: 2 — Track your own work on a Board
Story key: `2-1-create-a-project-and-see-what-is-in-your-space`
Requirements owned (coverage row `epics.md:354`): **FR-17** (create and rename here; delete is story 2.5) (`prd.md:367-375`, `epics.md:107`), **FR-18** (`prd.md:377-383`, `epics.md:108`), **AR-30** for the Projects-per-Space bound (`epics.md:218`, AD-25 `ARCHITECTURE-SPINE.md:224-228`), **UX-DR35** for the empty Space and the bound-reached state (`epics.md:279`, `EXPERIENCE.md:252, 276`). Also, folded in from the ledger: **AR-25 / AD-18** idempotency (`epics.md:213`, `ARCHITECTURE-SPINE.md:182-186`), the first wire **403** (AD-3, AD-20, NFR-7), **V-1** on the Token surface (`test-design-qa.md:278`), **A-12**'s idempotency clause (`test-design-qa.md:363`), and **SM-1**'s gap gates for every route this story adds (AR-35, AD-4).
Capabilities in the SPEC: **CAP-17**, **CAP-18** (`specs/spec-yello/SPEC.md:101-106`, `acceptance-criteria.md:170-184`).
Ledger entries assigned to this story (`deferred-work.md`): 10, every one folded into a task or stated out of scope — see "Ledger entries owned by 2.1" in Dev Notes.
Depends on: **Epic 1**, done. The Space default Status set it relies on is seeded at registration (story 1.3); configuring it is Epic 6.
Extended later, not here: **2.2** (the Board, the first Task, `ISO-TIMING-IN-SPACE`), **2.5** (delete a Project), **6.1** (a Project's Status delta), **8.1** (FR-35 parity sweep), **8.2** (snapshot contract tests on these routes).

> **The isolation suite is RED from the moment this file is committed, and Task 1 is what turns it green.** `IsolationCaseListTests.Every_pending_row_names_a_story_that_is_still_in_backlog` (1.9-GATE-003) fails as soon as `2-1-…` leaves `backlog` in `sprint-status.yaml`, which this create-story phase does. That is `deferred-work.md:411`. The create-story phase changes no test code by rule, so the fix is the dev phase's **first** task. Do nothing else until it is done.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As someone with a Space of my own,
I want to create named Projects and see the ones in the Space I am in,
So that my work has somewhere to live before I put anything in it.

## Acceptance Criteria

AC1–AC7 are reproduced **verbatim** from `epics.md:983-1013` (`status: final`), numbered by position. AC8–AC13 are carried from the ledger, the 1.9 gap gates and the architecture, and say where from.

**AC1 — the empty Space asks for nothing but a Project**
**Given** an active Space holding no Projects
**When** Space home renders
**Then** it states "No Projects yet." and offers one action to create a Project
**And** it never prompts to configure Statuses, because UJ-1 requires meeting no empty state that asks for configuration

**AC2 — create, and its Space is fixed**
**Given** an active Space
**When** an Owner, Admin or Member creates a Project
**Then** it belongs to exactly that Space, fixed at creation, and its effective Status set is the Space default set with an empty delta
**And** a Viewer has no create affordance present at all

**AC3 — rename**
**Given** a Project
**When** an Owner, Admin or Member renames it
**Then** the new name is used, and a Viewer has no rename affordance present

**AC4 — the listing is the Space, the whole Space and nothing but the Space**
**Given** an active Space
**When** its Projects are listed
**Then** the listing contains every Project in that Space and no Project from any other Space
**And** Viewers see the same Projects as Members, because Role affects what can be done to a Project rather than whether it is visible

**AC5 — the 51st Project is refused, visibly**
**Given** a Space already holding 50 Projects
**When** a 51st is attempted
**Then** it is refused inside the same transaction with a machine-readable reason, and the copy states the bound plainly
**And** nothing is silently accepted, because a bound that is not enforced is a defect rather than a relaxation

**AC6 — the schema**
**Given** the Project entity
**When** the schema test runs
**Then** it carries a non-nullable `SpaceId` with a row-level security policy, and its `Guid` id is generated application-side by `SequentialGuidValueGenerator`

**AC7 — no route moves a Project**
**Given** every route on both surfaces
**When** they are enumerated
**Then** none moves a Project to another Space, so the cross-Space leak is closed by construction rather than by rule

**AC8 — every new route is an isolation row on both surfaces, and the first wire 403 exists** *(carried from 1.9-GATE-001/002 `IsolationCaseListTests.cs:36-87`, AR-35, AD-4; `deferred-work.md:293, 374`; test-design I-1, I-2, I-3, I-4, I-5, I-13, V-1)*
**Given** the Project routes this story maps
**When** the isolation suite runs
**Then** each route is named by at least one `IsolationCases` row with an expectation for **both** `Session` and `ApiToken`, and a foreign Space's Projects are unreachable by read, listing, write and rename on both surfaces — the boundary `404`, byte-identical to an absent Space, one `CrossSpace` row, nothing written
**And** `ISO-RECORD-KIND-INSUFFICIENT-ROLE` is a **live** row: a Viewer's Session and a Viewer's Token creating a Project are each refused `403 insufficient-role` with capability `manage-projects`, each writing exactly one `InsufficientRole` row, distinguishable from `CrossSpace`

**AC9 — a retried write is applied once** *(carried from AR-25 / AD-18; `deferred-work.md:78-83, 253-256, 372`; A-12)*
**Given** a state-changing Space-scoped request carrying an `Idempotency-Key`
**When** the same key is replayed by the same Account in the same Space with the same request
**Then** the original response is returned and the effect is not applied a second time, and this is done by the request pipeline, never by a slice
**And** a replayed API Token issuance never returns the Token plaintext a second time, and no stored replay record holds a usable Token (NFR-6)

**AC10 — the bound is the pipeline's** *(carried from AR-3, AR-30 / AD-25; `deferred-work.md:319`; A-12)*
**Given** the Projects-per-Space bound
**When** a creation consults it
**Then** the limit is read from `BoundRegistry` and checked by the pipeline, not by the slice, and a gate fails the build on any other consumer of the bound's enforcement path

**AC11 — the Token surface reaches the first write, and the method table is decided** *(carried from `deferred-work.md:388, 300`)*
**Given** a Token bound to the active Space
**When** it creates a Project with no anti-forgery cookie or header
**Then** it is not refused `400`, while the same request carrying a Session cookie and no bearer still is
**And** an unsupported method on a Project route answers exactly what the recorded decision says (`404 not-available` from the fallback, or `405`), proved over the wire

**AC12 — a pending isolation row cannot turn the suite red at a phase boundary, nor outlive its story** *(carried from `deferred-work.md:411`)*
**Given** a pending row in `IsolationCases` naming a story
**When** that story moves through `ready-for-dev` and `in-progress`
**Then** the suite stays green, and it fails once the story reaches the state the recorded decision names (recommended: `review`) with the row still pending

**AC13 — Space home is a Space-scoped surface like any other** *(carried from UX-DR27, UX-DR31, UX-DR33, UX-DR37; `EXPERIENCE.md:85, 158-179, 252, 276`)*
**Given** Space home in the client
**When** it renders, refuses, or the tab leaves the Space
**Then** the Project list renders only after the server has answered, is purged synchronously with the Space it belongs to, never appears in `document.title`, and every Project name input carries `autocomplete="off"` and a non-reusable field name
**And** every string is externalised, and every capability the Role lacks is absent rather than disabled

## Tasks / Subtasks

- [ ] **Task 1 — Fix GATE-003 first; the isolation suite is red until you do** (AC: 12)
  - [ ] Confirm the red: `dotnet build Yello.slnx`, then run `Yello.Tests.Isolation` filtered to `IsolationCaseListTests` (it needs no database). Expect `ISO-RECORD-KIND-INSUFFICIENT-ROLE: pending until 2.1, which is now 'ready-for-dev' - convert the row`. Record it in the Debug Log.
  - [ ] Q1 decides the rule. Recommended (c): a pending row passes while its story is `backlog`, `ready-for-dev` or `in-progress`, and fails at `review` or `done`. The dev phase sets `review` at its end, so a forgotten conversion fails the dev phase itself, not a later commit. Update the test's name, summary and failure message to say exactly that.
  - [ ] **The vacuity guard breaks too, and nobody has noticed.** `Assert.True(pending.Count >= 2, …)` (`IsolationCaseListTests.cs:99-100`) fails the moment Task 9 converts 2.1's row (one pending row left), and again when 2.2 converts its own (none left). Replace it with a guard that still proves the gate read something: the status file was read and holds the `1-9-` key (already asserted), and the gate is planted against a synthetic pending row in a test of its own (a row naming a story that is `review` must be reported). Do not let the guard depend on how many pending rows happen to exist.
  - [ ] Plants: a pending row naming a story that is `review` → caught; one naming a story absent from the file → caught; one with no reason → caught. Save the original content in memory and restore it in a `finally`.
  - [ ] Close `deferred-work.md:411` with a dated note naming the rule chosen.

- [ ] **Task 2 — Read the open questions and record each decision before the code that depends on it** (AC: all)
  - [ ] Q1 before Task 1. Q2–Q5 before Task 3. Q6–Q8 before Task 7. Q9 before Task 8. Q10–Q12 before Task 10. Q13 any time.
  - [ ] Each decision goes in the Completion Notes as "**Qn - (x).** … Cost to later stories: …". Every one is reversible.

- [ ] **Task 3 — The Project entity, its table and its policy** (AC: 2, 6, 7)
  - [ ] `Yello.Domain/Projects/Project.cs`: `Id` (`init`), `SpaceId` (`init` — never settable after creation; this is AC7 at the type level), `Name` (settable), `CreatedAt` (`DateTimeOffset`, UTC). No navigation properties (see `YelloDbContext` remarks). **The namespace must be `Yello.Domain.Projects`**: `RefusalBoundaryGateTests.SpaceScopedEntities` already names `Yello.Domain.Projects.Project` (`RefusalBoundaryGateTests.cs:117-123`), so the AR-9 gate and the switcher gate cover it the moment it exists.
  - [ ] `Yello.Domain/Projects/ProjectName.cs`: a `TryNormalise` like `ApiTokenName` — trimmed, 1–100 characters (Q3). Invariants live in `Domain`, not in a validator (AR-34).
  - [ ] `Yello.Infrastructure/Persistence/Configurations/ProjectConfiguration.cs` taking the context: table `Project`, `ValueGeneratedNever()` on `Id` (the id comes from `IIdentifierGenerator`, the `SequentialGuidValueGenerator` port — AC6), `SpaceId` mapped to `SchemaNames.SpaceIdColumn`, required; `HasQueryFilter(project => project.SpaceId == context.ScopedSpaceId)` (AD-2's second layer); FK to `Space` with `Cascade` (as `StatusDefinition`); an alternate key or unique index on `(SpaceId, Id)` so 2.2's `Task` can carry the composite foreign key AR-8 requires (Q4). Add it to `YelloDbContext.OnModelCreating` and a `DbSet<Project>`; `PersistenceModelGateTests` fails otherwise.
  - [ ] `SchemaNames`: `ProjectTable`, and add it to `SpaceScopedTables`. `SpaceIsolationSchemaTests` and `SchemaVarianceTests` derive from that list and from the catalogue.
  - [ ] Migration `…_Projects`: the table, then `ALTER SECURITY POLICY dbo.SpaceIsolationPolicy ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.Project, ADD BLOCK PREDICATE … ON dbo.Project AFTER INSERT, ADD BLOCK PREDICATE … ON dbo.Project AFTER UPDATE` — the `AFTER UPDATE` block is new to this repository and is what makes "no operation moves a Project" a database fact (Q5). Literal names, not `SchemaNames` (`SchemaNames.cs` remarks). `Down` drops the predicates before the table. Pattern: `20261024121107_SpaceBoundaryRefusals.cs:80-97`.
  - [ ] Hand-edit the scaffold (`TESTING-CONVENTIONS.md:298-318`): file-scoped namespace, real documentation, lifted index-column arrays, `Up` under 80 lines. Expand/contract: a new table breaks nothing running. `MigrationBundleEquivalenceTests` covers the predicates and the new index.
  - [ ] No Status column, no delta table, no effective-set table on `Project` (AD-16: "No table stores a Project's effective Status set"). AC2's "Space default set with an empty delta" holds because no delta exists until story 6.1 (Q2).

- [ ] **Task 4 — Contracts** (AC: 2, 3, 4, 8, 9)
  - [ ] `Yello.Contracts/Projects/ProjectRoutes.cs`: `List` and `Create` = `/api/v1/spaces/{spaceId:guid}/projects`; `Rename` = `/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}` (Q6); `ListIn(spaceId)`, `ProjectOf(spaceId, projectId)`, `ProjectIdParameter`. `{spaceId}` stays the first segment after the version (AR-9).
  - [ ] `CreateProjectRequest(string? Name)`, `RenameProjectRequest(string? Name)`, `ProjectResponse(Guid Id, string Name, DateTimeOffset CreatedAt)`. **No request type carries a Space id or any field but the name** (AC7).
  - [ ] `ProblemTypes`: `InvalidProjectName` (`400`), and for idempotency `IdempotencyKeyReused` (`422`, same key with a different request) and `IdempotencyKeyInFlight` (`409`, Q7). Each with remarks that say what it never means.
  - [ ] `Yello.Contracts/Idempotency.cs` (or beside `Antiforgery.cs`): the header name `Idempotency-Key` stated once, for the client and the Host.

- [ ] **Task 5 — The three slices and their endpoints** (AC: 2, 3, 4, 7, 8)
  - [ ] `Yello.Application/Projects/CreateProject/`, `RenameProject/`, `ListProjects/` (AR-3 naming, imperative from the FR title). Each handler takes the pipeline-resolved `ActiveSpaceContext` or nothing; **none reads the Role** (`Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context`), none resolves a Space, none checks a capability, none counts Projects (Task 6), none touches `Idempotency-Key` (Task 7).
  - [ ] Ports in `Yello.Domain/Projects` (`IProjectStore` or reader/writer pair), implemented in `Yello.Infrastructure/Persistence`, registered in `InfrastructureServices`. **No `Where(SpaceId == …)`** in a query: both isolation layers are already narrowed (`StatusDefinitionReader.cs` remarks). Creation sets `SpaceId` from the context, never from the request. Rename updates `Name` only — `ExecuteUpdate` with one `SetProperty`, or a tracked entity whose `SpaceId` has no setter — and answers "not found" when zero rows match.
  - [ ] Listing order (Q8): name, then id — the switcher's order (`SpaceRoutes.Directory` remarks). Materialised before return (`UnitOfWorkResults.EnsureMaterialised`).
  - [ ] Endpoints in `Yello.Host/Endpoints/ProjectEndpoints.cs`, mapped through the `api` group in `Program.cs` (never on `app`; `SpaceScopedRouteCheck` refuses to start otherwise):
    - `GET` list → `RequiresCapability(Capability.ReadProjectsAndTasks)` → `200` with `ProjectResponse[]`.
    - `POST` create → `RequiresCapability(Capability.ManageProjects)` and the bound declaration (Task 6) → `201` with `ProjectResponse` and a `Location` of `ProjectRoutes.ProjectOf(…)`; `400 invalid-project-name`.
    - `PATCH` rename → `RequiresCapability(Capability.ManageProjects)` → `200` with `ProjectResponse`; `400 invalid-project-name`; `404 not-available` (`BoundaryProblems.NotAvailable()`, byte-identical) for a Project id that matches nothing in the active Space — including another Space's Project id, which RLS and the filter both hide.
    - Not `SessionOnly`: these routes admit Tokens (AD-4, FR-35 accrues per slice).
    - Read the body **inside** the endpoint, as `ApiTokenEndpoints.IssueAsync` does (`ApiTokenEndpoints.cs:66-130`): a body bound as a parameter would be answered `400`/`415` before the pipeline ran, so a non-member would get neither the `404` nor its record.
  - [ ] The in-Space not-found does **not** write an `AccessRefusal` (AD-20 records boundary `404`s and `403`s only), so it is the first not-found that does less work than a boundary refusal. See Q13 and `deferred-work.md:291`; measure the gap and record it in the Debug Log.
  - [ ] Structured logs: `SpaceId` as a field, never a Project name or Space name (`ARCHITECTURE-SPINE.md:274`). The isolation suite's per-case log check (I-17) will catch a Project name in a log only if the world registers it as a secret — add Project names to the world's secret set (Task 9).

- [ ] **Task 6 — The Projects-per-Space bound, in the pipeline** (AC: 5, 10)
  - [ ] Endpoint metadata, declared as the capability is: e.g. `.ConsultsBound(Bound.ProjectsPerSpace)` (a `RequiredBound` record in `Yello.Host/Spaces`). The pipeline reads `BoundRegistry.For(bound)` and asks a port (e.g. `IBoundCounter` in `Yello.Application/Spaces`, implemented in Infrastructure) for the current count **inside the request's unit of work, before the endpoint runs**; at or above the limit it answers `BoundaryProblems.BoundReached(entry)` (`409 bound-reached`, `bound: projects-per-space`, `limit: 50`), the transaction rolls back, and nothing is recorded (not an `AccessRefusal`).
  - [ ] **Concurrency**: two creations at 49 must not both pass. Count under `UPDLOCK, HOLDLOCK` on the Space's Project rows (a key-range lock held to the end of the unit of work), so the second waits for the first to commit and then counts 50. Story 1.6 left the same race open for presence (`deferred-work.md:298`, owned by 2.9); do not repeat it for a bound whose whole point is the number. Prove it with two concurrent creations at 49 → exactly one `201` and one `409`, and 50 rows.
  - [ ] Wire case: seed 50 Projects (through the endpoint, or raw SQL under the Space session key as `WireDatabase` does), then the 51st → `409` with the two extensions, and `SELECT COUNT(*)` is still 50. Anchor: the 50th succeeded.
  - [ ] AC10's gate: extend `RefusalBoundaryGateTests.Only_the_pipeline_depends_on_the_capability_matrix_and_the_refusal_recorder` (or a sibling) so only the pipeline (and the port's adapter and registration) depends on the bound-count port; and so no type outside the pipeline and the registry's own gate reads `BoundRegistry`. Plant: a handler injecting the port → caught. Close `deferred-work.md:319`.
  - [ ] Client copy already exists: `projects-per-space` = "This Space has {0} Projects, the maximum." (`ClientCopy.resx:422-425`), rendered by `RefusalCopy.For` from the bound code and limit (`RefusalCopy.cs:43-90`).

- [ ] **Task 7 — `Idempotency-Key`, in the pipeline** (AC: 9)
  - [ ] Q6–Q7 decide the shape; the recommended one: a pipeline behaviour on every Space-scoped state-changing request (`POST`, `PUT`, `PATCH`, `DELETE` on a `{spaceId}` route), run **after** resolution and the capability check and **before** the creation bound, **inside** the unit of work. The order matters: a replay applies nothing, so replaying the key of the 50th creation must return its original `201`, not the 51st's `409` — test exactly that. With no header it does nothing. With one, it looks up `(SpaceId, AccountId, Key)`:
    - none → run the endpoint; on a materialised success, write the record **in the same transaction** as the effect (status, content type, `Location`, body, and a fingerprint of method + route pattern + body SHA-256); the unit of work commits both or neither.
    - found, same fingerprint → return the stored response without running the endpoint; nothing is applied.
    - found, different fingerprint → `422 idempotency-key-reused`.
    - a concurrent first use (the unique index refuses the second insert) → `409 idempotency-key-in-flight`, rolled back.
    - a failure (`4xx`/`5xx`) is never stored — the unit of work rolls it back anyway (`UnitOfWorkResults.IsFailure`).
  - [ ] Storage: `IdempotencyRecord`, Space-scoped (`SpaceId` non-nullable, filter and block predicates, query filter, added to `SpaceScopedTables`), unique on `(SpaceId, AccountId, Key)`, key ≤ 255 characters (longer → `400`). The stored body holds Space content (a Project name), which is why it must be under the Space's policy. Retention (Q7): 24 hours by `CreatedAt`, **evaluated on read** against the database clock inside the lookup's own `WHERE` (an expired record is treated as absent, AR-31's shape), never by a timer (`No_production_code_runs_on_a_timer_or_as_a_hosted_service`). **Do not copy the refusal purge**: `AccessRefusal` carries no policy, so `AccessRefusalPurge` can delete across Spaces; `IdempotencyRecord` is under the Space policy, so a cross-Space purge would need an RLS bypass, which AD-2 and AD-24 forbid. Delete expired rows only inside a Space's own unit of work (for example, the expired rows of the active Space when a new record is written), or leave them and ledger the table's growth to 2.9.
  - [ ] **Token issuance (`deferred-work.md:372`, severity high).** `POST …/api-tokens` is Space-scoped and state-changing, so it falls under the behaviour. Its body carries the plaintext, and storing that body would make a datastore read yield a usable Token — an NFR-6 breach. Recommended (Q6): the endpoint's metadata marks the response as carrying a secret; the record stores the body **with the `token` property removed**, and a replay returns `201` with that body and `Cache-Control: no-store`. The client copy for a response without a plaintext says the Token was issued and its value cannot be shown again (revoke and issue a new one). Prove: replay returns no plaintext; `SELECT` over `IdempotencyRecord` contains no plaintext and no `SecretHash`; exactly one `ApiToken` row exists.
  - [ ] Account-scoped and anonymous state-changing routes (register, sign-in, sign-out, Token revocation) are outside this behaviour in this story; record the reason per route in the Completion Notes (registration's replay is already indistinguishable, `deferred-work.md:80`; revocation is `204` whatever the state; sign-in and sign-out are credential operations). Ledger anything left owed.
  - [ ] The client sends a fresh key per user-initiated create and rename (a retry of the same attempt reuses it).
  - [ ] A-12's idempotency clause: a gate that only the pipeline (and the store's adapter and registration) depends on the idempotency port, planted with a handler injecting it. Close `deferred-work.md:78-83` and `:253-256`.

- [ ] **Task 8 — The method table, and the Token on the first write** (AC: 11, 8)
  - [ ] Q9: with `GET` and `POST` on `/projects` and `PATCH` on `/projects/{id}`, a `PUT` or `DELETE` there goes to the `/api/{**path}` fallback today and answers `404 not-available` with no `Allow` header (`UnmatchedApiEndpoint.cs`). Recommended: keep it (AD-3, no route-shape disclosure), record the decision, and pin it with a wire case on one Project route. Close `deferred-work.md:300`.
  - [ ] AC11's first half, over the wire with a real bearer (`WireSession.PostWithTokenAsync`, which attaches no cookie and no anti-forgery header): a Token `POST` to `ProjectRoutes.ListIn(space)` → `201`; the same `POST` with the Session cookie and no anti-forgery header → `400 antiforgery-token-missing`. Close `deferred-work.md:388`.

- [ ] **Task 9 — The isolation rows; the pending row converted** (AC: 8, 4, 7)
  - [ ] `tests/Yello.Tests.Isolation/IsolationCases.cs`: add rows for every route Task 5 maps, each with an expectation for **both** surfaces — "Isolation rows" table in Dev Notes. 1.9-GATE-002 (`Every_api_route_the_Host_maps_…_is_named_by_a_case`) fails the suite until every route has one; each row must send a request on the route it names (`CaseRun.cs`).
  - [ ] Convert `ISO-RECORD-KIND-INSUFFICIENT-ROLE` (`IsolationCases.cs:156-161`): route `ProjectRoutes.Create`, method `POST`; a Viewer actor in A's Space on each surface (`WireDatabase.ChangeRoleAsync` on a second registered Account's Membership, or a Membership added by raw SQL); expectation: `403`, `type insufficient-role`, `capability manage-projects`, exactly one new `InsufficientRole` row and no new `CrossSpace` row for (actor, Space), and no Project written. Remove `PendingUntil` and `PendingReason`. This is V-1 on both surfaces (`deferred-work.md:374`): close it, and `:293`.
  - [ ] `ProductActor.SendAsync(surface, method, path)` sends no body. Add a body-carrying overload (JSON, plus the `Idempotency-Key` header when a case wants one) in `Yello.Tests.Shared`; the Session surface keeps sending the anti-forgery header on unsafe methods, the Token surface keeps sending nothing but the bearer.
  - [ ] Add every Project name a case creates to the world's secret set, so I-17's per-case log check fails on a Project name in the Host's log.
  - [ ] `ISO-TIMING-IN-SPACE` stays pending for **2.2** unless Q13 (b) is chosen. Its `PendingReason` says "No route looks a resource up by id inside a Space until story 2.2", which this story makes false: update the reason to name the rename route and say why the timing arm is still 2.2's.
  - [ ] Plant (one at a time, restored in a `finally`): the list handler drops its filter **and** the policy's filter predicate on `Project` is dropped in the test database → `ISO-LIST-PROJECTS-AGGREGATE` caught on both surfaces; one layer alone stays green (AD-2's falsifiable-layer property).

- [ ] **Task 10 — Space home: the list, the empty state, create and rename** (AC: 1, 2, 3, 4, 5, 13)
  - [ ] `Yello.Client/Pages/SpaceHome.razor` (read it whole before editing; its current state is in Dev Notes). After `ArriveAsync` succeeds, `GET ProjectRoutes.ListIn(SpaceId)` with `ActiveSpace.Token`; render nothing of the list before that answer (UX-DR31), skeleton with `aria-busy="true"` while it is out (UX-DR35 cold load), then the list or the empty state. Keep every existing behaviour: the unlisted-id refusal without a request, the one refusal surface for both `404` causes, the `401` hand-off to `YelloApiHandler`, the heading focus rule.
  - [ ] Empty: "No Projects yet." and **one** primary action, "Create a Project" (`EXPERIENCE.md:252`). Nothing about Statuses, columns or configuration.
  - [ ] Create (Q10): a name field (`autocomplete="off"`, a non-reusable `name` attribute — UX-DR33) and one primary button. Not optimistic: the id is the server's. While in flight, state it and refuse resubmission. On `201`, add the row and announce politely. On `400 invalid-project-name`, inline error. On `409 bound-reached`, the bound copy inline in the creation surface (`EXPERIENCE.md:276`: visible, never silent) via `RefusalCopy.For`. On `403` (a Role changed under the page), the capability copy, not a toast (UX-DR32). On `404`, the full refusal surface.
  - [ ] Rename (Q11): an explicit control per row, never hover-only (UX-DR26), opening an inline name field with the same autofill rules; commit and cancel are explicit, `Esc` cancels. Optimistic with revert on refusal is permitted (UX-DR26 bans optimism only for destructive and permission changes); if you take it, a refused rename reverts the row and states why. There is no save button and no auto-save indicator anywhere (UX-DR26) — the commit control's label is "Rename", not "Save".
  - [ ] **Absent, not disabled** (UX-DR27): for a Viewer the create action, the empty state's action and every rename control are not rendered. Q12 decides how the client knows: recommended (a) a client predicate over the Role's stable code from `ActiveSpace.Current`, held against `RoleCapabilityMatrix` by an architecture gate like the one `SpaceSwitcherGateTests` already runs for the Role copy (`SpaceSwitcherGateTests.cs:215-233, 398-399`). The server still refuses (`403`) whatever the client offers.
  - [ ] Project names render as text in this story — no link target exists until 2.2's Board (Q10). `document.title` stays the fixed `Yello` (`SpaceSwitcherGateTests`' title gate). No Project name in any URL.
  - [ ] Register the Project list with `ActiveSpaceState`'s purge (`ActiveSpaceState.cs` remarks: "Epic 2 registers its Board here"), so a switch, sign-out or leaving the Space drops it before the destination renders. Nothing in `localStorage`, `sessionStorage` or IndexedDB (`ClientStorageBanTests`).
  - [ ] Copy in `ClientCopy.resx` only, sentence case, terse (UX-DR37): "No Projects yet.", "Create a Project", the name label, "Rename", the in-flight and completion announcements, `invalid-project-name`. Never "archive", never "save". `LocalisationGateTests` and `RefusalCopyTests` hold the rest.
  - [ ] Layout: reuse `PrimaryButton`, `FormField`, `InlineErrorRegion`. Rem padding, no fixed heights, 24px target floor, logical properties only (UX-DR3, UX-DR40, UX-DR42). A new structural border needs `--hairline-width` (1.5px); see the device-pixel-snapping note in Dev Notes before adding one.
  - [ ] Browser cases in `Yello.Tests.Client` against `FakeYelloApi` (extend it with the three Project routes, bodies as `Yello.Contracts` types): empty state and its one action; the list after the answer and not before (`HoldAsync`); a Viewer sees the list and no create or rename control (anchor: a Member, same list, sees both); create adds the row and announces; the `409` renders "This Space has 50 Projects, the maximum."; a switch A → B with B's list held leaves nothing of A's list. Never Playwright's URL wait; poll `location.pathname` (`TESTING-CONVENTIONS.md:187-192`).

- [ ] **Task 11 — Slice and wire tests** (AC: 2, 3, 4, 5, 6, 7, 9)
  - [ ] `tests/Yello.Tests.Slices/Projects/…` (one folder per use case, as `Accounts/`, `Spaces/`): create in the active Space only; `SpaceId` from the context; id generated before the insert by the generator port (assert it equals what the generator returned); default Status set unchanged and no delta rows (AC2); rename changes `Name` and nothing else; listing ordered and complete; a Viewer's create and rename refused `403` with no row written (Session and Token), each with one `InsufficientRole` record; name validation codes.
  - [ ] AC7 by construction, three layers: (1) the contracts carry no Space field; (2) `Project.SpaceId` has no setter, and a gate asserts no production type outside the create path writes it; (3) an `UPDATE dbo.Project SET SpaceId = …` under the Space session key fails on the `AFTER UPDATE` block predicate (a schema test).
  - [ ] AC6: `SpaceIsolationSchemaTests` covers `Project` through `SpaceScopedTables`; add the `AFTER UPDATE` predicate to what it asserts for `Project` (Q5).
  - [ ] Idempotency (Task 7) over the wire, on both surfaces: create with a key twice → one row, identical `201` bodies, same `Location`; same key, different name → `422`; replay after the Project was renamed → the original response, not the current state (that is what "returns the original response" means); a key used by Account B in its own Space does not replay A's record (the record is Space- and Account-scoped); Token issuance replay without plaintext.
  - [ ] Bound (Task 6): the 51st and the concurrent pair.

- [ ] **Task 12 — Plants, runs, ledger, conventions** (AC: all)
  - [ ] Plants, one at a time in the foreground, original content held in memory and restored in a `finally`, tree compared to a pre-run snapshot after each. **Never** `git checkout --`, `git restore`, `git reset` or `git stash`. Record `| # | Plant | Gate | Caught |` in the Debug Log. Minimum: GATE-003 (Task 1); a Project route with no isolation row (GATE-002); the two-layer disclosure (Task 9); the bound counted in the slice instead of the pipeline (AC10 gate); the idempotency port in a handler (A-12); the replay storing the plaintext (Task 7); a Viewer create control rendered (client case); the Project list written to `sessionStorage` (`ClientStorageBanTests`); a Project name in `document.title`; the `AFTER UPDATE` predicate removed (AC7 schema case).
  - [ ] Runs: `dotnet build Yello.slnx` (warnings are errors), then `dotnet test Yello.slnx` (MTP via `global.json`; `--no-build` runs zero tests unless the same configuration was just built). Record totals passed/failed/skipped and the wall clock. Baseline at `311d1d5`: **552 total, 551 passed, 0 failed, 1 skipped** with `YELLO_MIGRATION_BUNDLE` set; without it the two bundle cases also skip. If Rancher's socket times out: `rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch.
  - [ ] `tests/TESTING-CONVENTIONS.md`: the GATE-003 rule as decided; the body-carrying `ProductActor` overload; the new counts.
  - [ ] Ledger (`deferred-work.md`): CLOSE with dated notes `:78-83`, `:253-256`, `:293`, `:300`, `:319`, `:372`, `:374`, `:388`, `:411`; UPDATE `:291` (rename is the first in-Space lookup; the timing arm stays with 2.2 unless Q13 (b)); RE-OWN `:338` (skip link) to **2.2** with its reason unless you implement it; ADD anything this story defers, each with an owner.
  - [ ] `sprint-status.yaml`: `review` at the end of the dev phase. Under Task 1's rule, the suite fails at `review` if `ISO-RECORD-KIND-INSUFFICIENT-ROLE` is still pending — that is the gate working.
  - [ ] **Do not add review narration to source files.**

## Dev Notes

### The shape of the story in one paragraph

The first Space-scoped entity after Statuses, and the first Space-scoped **write** that every Role does not hold. That makes it the place three pipeline behaviours finally get a real subject: the capability check produces its first `403` over the wire (and the isolation suite's pending `InsufficientRole` row converts), the bound registry enforces its second bound at creation rather than at resolution, and `Idempotency-Key` gets built because this is the first `POST` that returns a created id. The feature itself is small — a `Project` table under the Space policy, three slices (list, create, rename), three routes on both surfaces, and Space home rendering the list with an empty state and Role-absent controls. The risk is not the feature; it is that each pipeline behaviour is built in the pipeline and gated there, not inside the slice that first needs it (AR-3), and that every new route arrives with an isolation row on both surfaces (AD-4), which the 1.9 gap gate enforces whether or not anyone remembers.

### Ledger entries owned by 2.1 — every one folded or stated

| Entry | Severity | Where it lands |
|---|---|---|
| `deferred-work.md:411` GATE-003 turns the suite red when 2.1 leaves `backlog` | medium | **Task 1**, AC12, first |
| `:78-83` Idempotency (AR-25) has no owner; re-owned to 2.1 by 1.6 decision 10 | medium | Task 7, AC9 |
| `:253-256` A-12's idempotency clause | low | Task 7 (gate) |
| `:372` an `Idempotency-Key` replay of Token issuance must never return the plaintext | **high** | Task 7, AC9 |
| `:293` no wire-level 403; convert `ISO-RECORD-KIND-INSUFFICIENT-ROLE` | low | Task 9, AC8 |
| `:374` a Viewer's Token refused `403` on a write (V-1) | medium | Task 9, AC8 |
| `:388` the Token anti-forgery exemption proved only through the filter | medium | Task 8, AC11 |
| `:300` a non-safe request with no handler answers `404`, not `405` | low | Task 8, AC11 (decide and pin) |
| `:319` `BoundRegistry` is not in the A-12 gate | low | Task 6, AC10 |
| `:338` no skip link past the context bar | low | **Out of scope, re-owned to 2.2** (recommended): the entry says "re-decided when the bar grows"; 2.1 adds nothing to the bar — the Project name enters it with the Board in 2.2 (`EXPERIENCE.md:413-414`). If you implement it anyway, it must be a focus-moving control, not a `#main` link (`MainLayout.razor` remarks). |

Epic-2-owned entries this story touches only if it builds the surface they name, stated so nobody reads them as forgotten:
- `:36` AC9's radius **mapping** gate (2px chips, 3px Tasks/columns/buttons, 6px dialogs) — owned "at each story that builds one of the named surfaces". 2.1 builds none of the named components if it reuses `PrimaryButton`; if it builds a dialog for create or rename, that dialog takes 6px and the mapping clause for dialogs becomes gateable — add it.
- `:66` device-pixel snapping of structural borders — owned by **UX, before the first story that renders a structural border** (epic 2). If the Project list needs separators, prefer spacing over a new border; if a border is unavoidable, stop and raise it rather than invent the snapping rule.
- `:32` the `--radius-full` "exactly one consumer" tightening belongs to the column-count chip (2.2), not here.
- `:291` the in-Space timing arm — 2.2, updated here (Q13).

### Isolation rows (Task 9)

Actors as 1.9 built them: **A** and **B**, each with a Personal Space (`SA`, `SB`), a Session and a Token for their own Space. Seed at least one Project in each Space through the product (A creates `PA` in `SA`; B creates `PB` in `SB`). Anchor 404 = the same route on a random Space id (or, for a by-id route inside one's own Space, a random Project id), same surface.

| Id | Test design | Vector | Request (by A, about B) | Session | ApiToken |
|---|---|---|---|---|---|
| ISO-DIRECT-PROJECTS | I-1, I-3 | DirectIdentifier | `GET /spaces/{SB}/projects` | 404 = anchor, `CrossSpace` row, no `PB` name or id in body | same |
| ISO-LIST-PROJECTS-AGGREGATE | I-3, FR-18 | Aggregate | `GET /spaces/{SA}/projects` after B created `PB` | 200, exactly A's Project ids, none of B's | same |
| ISO-WRITE-PROJECT | I-2 | Write | `POST /spaces/{SB}/projects` | 404 = anchor, `CrossSpace` row, no Project written in `SB` (count unchanged) | same (a Token is admitted on this route, so it is the boundary 404, not `401`) |
| ISO-RENAME-FOREIGN-SPACE | I-2 | Write | `PATCH /spaces/{SB}/projects/{PB}` | 404 = anchor, `CrossSpace` row, `PB`'s name unchanged | same |
| ISO-RENAME-FOREIGN-ID-IN-OWN-SPACE | I-4, I-5 | DeepLink | `PATCH /spaces/{SA}/projects/{PB}` | 404 byte-identical to a random Project id in `SA`; **no** refusal row (no boundary was crossed); `PB` unchanged | same |
| ISO-RECORD-KIND-INSUFFICIENT-ROLE *(converted)* | AC6 (1.9), NFR-7, V-1 | RecordKind | a Viewer in `SA`: `POST /spaces/{SA}/projects` | 403 `insufficient-role`, `capability manage-projects`, one `InsufficientRole` row, no `CrossSpace` row, nothing written | same |

`ISO-RENAME-FOREIGN-ID-IN-OWN-SPACE` is the case AD-3 is about: possessing B's Project id must confer nothing, and the answer must not differ from an id that never existed — in body here, in duration in 2.2 (Q13). I-13 applies to every refusal: no Space name, no Project name, no id not already in the URL.

### Current state of the files this story changes — read each before editing

| File | Today | This story | Preserve |
|---|---|---|---|
| `tests/Yello.Tests.Isolation/IsolationCaseListTests.cs` | GATE-003 requires `backlog` (`:94-131`); vacuity guard `pending.Count >= 2` (`:99-100`); `SprintStatuses()` parses `development_status` (`:192-201`). | Task 1: rule relaxed to the recorded state; guard re-based on a planted synthetic row. | GATE-001, -002, -004 unchanged; the exclusion list must still be mapped (`:84`). |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 16 rows; `ISO-RECORD-KIND-INSUFFICIENT-ROLE` pending 2.1 on `GET Active` (`:156-161`); `ISO-TIMING-IN-SPACE` pending 2.2 (`:179-185`). | New rows; the 2.1 row live on `POST Create`; 2.2's reason updated. | Every row states both surfaces; no case branches on surface. |
| `Yello.Host/Spaces/ActiveSpaceResolution.cs` | Resolve → Token-bound check → matrix → presence bound → endpoint → commit only a materialised success (`ServeAsync`). | Idempotency (lookup and replay) then the creation bound join `ServeAsync`, in that order, after the presence bound and before `next`; the idempotency record is written after `next` returns a materialised success, before `CompleteAsync`. | 401 before anti-forgery; 404 and 403 never converted; refusal recorded on its own unit of work; commit on `CancellationToken.None`. |
| `Yello.Host/Program.cs` | Maps 10 routes through `api`; comment says idempotency is 2.1's (`:40-41`). | Register the three handlers; map the Project endpoints; update that comment. | Filter order (`ApiRouteGroup.cs`); `SpaceScopedRouteCheck` last before `RunAsync`. |
| `Yello.Host/Endpoints/ApiTokenEndpoints.cs` | `IssueAsync` returns `201` with plaintext and `no-store` (`:78-108`). | Marked as carrying a secret for the idempotency record. | The plaintext appears in exactly one response, ever; `no-store`. |
| `Yello.Infrastructure/Persistence/YelloDbContext.cs`, `SchemaNames.cs` | Explicit configuration list; `SpaceScopedTables` = Membership, StatusDefinition, SpacePresence. | `Project` and `IdempotencyRecord` added to both. | No `ApplyConfigurationsFromAssembly`; no migration applied from here. |
| `Yello.Client/Pages/SpaceHome.razor` | Renders only the heading "Projects" after the active Space's answer; refuses unlisted ids without a request; one refusal surface. "Projects arrive with story 2.1". | List, empty state, create, rename. | Everything in its header comment; `_focusHeading` rule; cancellation on leaving. |
| `Yello.Client/Spaces/ActiveSpaceState.cs` | The purge seam; "Epic 2 registers its Board here". | Register the Project list. | Synchronous purge before render; in-flight cancelled first. |
| `tests/Yello.Tests.Shared/ProductActor.cs` | `SendAsync(surface, method, path)` sends no body (`:221`). | A body-carrying overload. | Token surface sends only the bearer. |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs` | A-12 permitted lists for matrix, recorder, presence port (`:512-544`). | Bound-count and idempotency ports added with their permitted lists. | Each gate finds its subject before asserting. |

### Architecture constraints that bind here

- **AR-3** (`epics.md:191`): authorisation, Space resolution, refusal recording, idempotency and bound checks live in the pipeline. "A slice that re-implements any of them is a defect."
- **AD-1 / AR-4**: the Role is read off the Membership row per request; no slice reads it; no `[Authorize(Roles=…)]`, `IsInRole` (`RoleApiBanTests`).
- **AD-2 / AR-5, AR-6**: non-nullable `SpaceId`, RLS policy, and an independent EF query filter from application state. No raw SQL outside Infrastructure.
- **AD-3 / AR-10**: no Membership → `404`, identical to absence; Membership but wrong Role → `403`, capability-shaped. Bodies carry no Space or Project name.
- **AD-4**: one pipeline, both surfaces; a one-surface case is a gap.
- **AD-16**: no table stores a Project's effective Status set.
- **AD-18 / AR-25**: every state-changing endpoint accepts `Idempotency-Key`; replay returns the original response.
- **AD-20 / AR-11**: the pipeline records every `403` and boundary `404`; an in-Space not-found is neither.
- **AD-25 / AR-30**: the bound is declared once (`BoundRegistry`), checked by the pipeline, refused inside the creation's transaction with a machine-readable reason.
- **AR-8**: cross-entity references inside a Space carry `SpaceId` — give `Project` the `(SpaceId, Id)` key 2.2's `Task` will reference.
- **AR-9**: `{spaceId}` first after the version; the AR-9 gate already lists `Yello.Domain.Projects.Project`.
- **AR-34**: sequential `Guid` via the generator port; `DateTimeOffset` UTC; RFC 9457 with a stable `type`; state changes only through a slice in one transaction.
- **UX-DR27**: absent, not disabled, for Role reasons — everywhere. **UX-DR31/33**: purge with the Space; no name in the title; `autocomplete="off"` on name inputs.

### Testing requirements

Conventions (`tests/TESTING-CONVENTIONS.md`): underscore-sentence names, one behaviour each; traits `Suite`, `Priority`, `Requirement` (cite the FR/NFR **and** the AD, e.g. `FR-17`, `AD-25`); randomised data, never a shared email address; no `Task.Delay` (poll with a deadline, or `HoldAsync` / `Release` in the client fake); cleanup by database disposal; every absence anchored on a presence and validated by a plant.

- Slices: `tests/Yello.Tests.Slices/Projects/{CreateProject,RenameProject,ListProjects}/`, on `MigratedDatabaseFixture` (a database per class on the shared engine) and `RunningHost`/`WireSession` for wire cases.
- Isolation: rows in `IsolationCases.cs`, executed by the existing generated theories; no new test class needed for boundary rows.
- Client: `Yello.Tests.Client` against `FakeYelloApi`; the real-Host collection only if a case needs the real cookie (none should).
- Architecture: gates in `RefusalBoundaryGateTests` (A-12 for the two new ports), the client Role-predicate gate beside `SpaceSwitcherGateTests`, and anything that reads a production file must find its subject first.
- The suites stay inside the engine budget (`ContainerTopologyGateTests`): add no `SqlServerContainerFixture` construction.

### Previous story intelligence (1.10, 1.9, 1.8, 1.6)

- **1.9 built the gap gates this story must satisfy.** GATE-002 reads the real Host's route table in process; any `/api` route without a row fails the suite. A row must actually send a request on the route it names. Every row is executed on both surfaces from one expectation dictionary.
- **1.9's review finding that mattered:** AC10's log check never saw the requests carrying the secrets, because the cursor was taken after seeding. When you add Project names as secrets, make sure the requests that create them are inside a checked slice.
- **1.8:** a body bound as an endpoint parameter is answered before the pipeline runs — read it inside the endpoint (`ApiTokenEndpoints.cs:66-75`). A Token on a Session-only route is no credential (`401`). Lee: a Token on sign-out is `400 antiforgery-token-missing`.
- **1.6 decision 4:** no wire `403` existed until this story; the matrix is proved through the filter with a 20-row theory (`SpaceBoundaryPipelineTests`). Decision 10: idempotency deferred to here. Its review: gates checked less than their names claimed — name every gate for exactly what it checks.
- **1.10:** migrations must be expand/contract (the previous revision serves while the migration job runs); the bundle-equivalence test compares predicates and indexes, so the new `AFTER UPDATE` block predicate is covered once the migration exists. Database unavailability is a uniform `503` (`DatabaseAvailability`) — do not catch `SqlException` in the new store.
- **Plant discipline:** 1.6 19/19, 1.7 28/29, 1.8 19/19, 1.9 and 1.10 all caught. Restore in a `finally`; never `git checkout --`, `git restore`, `git reset`.
- **Memory:** the full run with the browser suite is green on the shared-engine topology (four engines). If a Client case fails only in the full run, run `dotnet test --project tests/Yello.Tests.Client` alone before calling it a regression.

### Git intelligence

- `9e9aa4e` built the isolation and revocation suites (`IsolationCases`, `RunningHost`, `ProductActor`, `WireDatabase`); `9de3ff3` closed its review. `64a68d9` / `4ca2043` built and reviewed the Azure pipeline (CI runs the architecture suite first, then everything with `--no-build`). `311d1d5` closed epic 1.
- Commits are per phase, straight to `main`, by the orchestrator. Do not commit, push, branch or stash.

### Latest technical notes (pinned versions; no upgrades, no new packages)

- **.NET 10 / EF Core 10**: `ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, name))` issues one `UPDATE` and returns the row count, which is the "not found" signal for rename. A table hint for the bound count needs raw SQL in Infrastructure (`FromSql` with `WITH (UPDLOCK, HOLDLOCK)`) or an `ExecuteSql` count — raw SQL is permitted only in `Yello.Infrastructure` (A-13).
- **SQL Server RLS**: `ADD BLOCK PREDICATE … AFTER UPDATE` refuses an update whose resulting row fails the predicate — here, a `SpaceId` changed away from `SESSION_CONTEXT('SpaceId')`. Each `ADD` is a separate clause in one `ALTER SECURITY POLICY`.
- **Minimal APIs**: `MapPatch` exists on `IEndpointRouteBuilder`. `Results.Created(location, value)` sets `Location`.
- **xunit.v3 4.0.0 / Playwright 1.62.0 / Testcontainers 4.6.0**: as 1.9 recorded; nothing new needed.
- No web research was required: every library is pinned by `Directory.Packages.props` and `PackageVersionPinTests`, and this story adds none. Idempotency is built, not imported.

### Project structure notes

- **New, most likely:** `Yello.Domain/Projects/{Project,ProjectName,IProjectStore}.cs`; `Yello.Application/Projects/{CreateProject,RenameProject,ListProjects}/…Handler.cs`; `Yello.Application/Spaces/{IBoundCounter,IIdempotencyStore}.cs` (names indicative); `Yello.Infrastructure/Persistence/{ProjectStore,BoundCounter,IdempotencyStore}.cs`, `Configurations/{ProjectConfiguration,IdempotencyRecordConfiguration}.cs`, one or two migrations; `Yello.Host/Endpoints/ProjectEndpoints.cs`, `Yello.Host/Spaces/{RequiredBound,IdempotentReplay}.cs`; `Yello.Contracts/Projects/{ProjectRoutes,CreateProjectRequest,RenameProjectRequest,ProjectResponse}.cs`, `Yello.Contracts/Idempotency.cs`; `tests/Yello.Tests.Slices/Projects/…`.
- **Modified:** as in the current-state table, plus `ClientCopy.resx`, `FakeYelloApi.cs`, `TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml`, this file.
- **Ring check (AD-21):** `Domain` references nothing; the ports for counting and idempotency that the Host pipeline calls belong in `Application` (as `IActiveSpaceResolver`, `ISpacePresenceRegister` do), implemented in `Infrastructure`. No ASP.NET type in `Application`.
- **Variance to record:** none expected.

### References

- `_bmad-output/planning-artifacts/epics.md:107-108, 183-228 (AR-3, AR-5…AR-11, AR-25, AR-30, AR-34), 236-286 (UX-DR26, -27, -31, -33, -35, -37), 354, 969-1013`
- `prds/prd-YelloBMAD-2026-08-15/prd.md:361-383, 804, 896`; `addendum.md`
- `architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md:75-100, 170-186, 194-198, 218-228, 263-276, 412`
- `ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:43-67, 75-93, 95-114, 158-179, 197-221, 246-279, 470-477`
- `specs/spec-yello/SPEC.md:101-106`; `acceptance-criteria.md:170-184`; `role-capability-matrix.md`
- `test-artifacts/test-design-qa.md:262-282 (I-1…I-15, V-1), 363 (A-12)`; `test-design-architecture.md:123-126, 368-386`
- `implementation-readiness-report-2026-08-22.md:496-497, 600, 995, 1079, 1209`
- `tests/TESTING-CONVENTIONS.md` (whole file; especially `:86-118, 172-192, 298-318, 482-531`)
- `implementation-artifacts/deferred-work.md` (entries in the ledger table above); `1-9-prove-isolation-on-both-surfaces.md` (case list, Q5); `1-8-issue-an-api-token-scoped-to-one-space.md` (Q12, body read after the pipeline); `1-6-refuse-at-the-space-boundary-and-record-every-refusal.md` (decisions 4, 10)

### Open questions — each with a recommended decision

The dev phase takes these as **reversible, recorded decisions** in the Completion Notes. None blocks starting.

1. **GATE-003's rule** (`deferred-work.md:411`). (a) Pending passes while the story is not `done`. (b) Convert the row in the create-story change — impossible here: the route does not exist until this story builds it, and create-story changes no test code. (c) Pending passes while the story is `backlog`, `ready-for-dev` or `in-progress`, and fails at `review` or `done`. **Recommend (c):** it is still impossible for a pending row to outlive its story, and the failure lands on the phase that owes the conversion (dev sets `review`) rather than on a code-review commit. Sub-point: re-base the vacuity guard (Task 1) whichever rule you choose.
2. **AC2's "effective Status set is the Space default set with an empty delta".** No delta exists until 6.1. (a) Prove it structurally: no Status column on `Project`, no delta table, the Space's three `StatusDefinition` rows unchanged by creation, and a schema assertion that no table stores an effective set (AD-16); the derivation itself is read by 2.2's Board. (b) Add an effective-set read (`GET …/projects/{id}/statuses`) now — a route no story asks for, and a second by-id lookup. (c) Create an empty delta table now — Epic 6's design, made early. **Recommend (a).**
3. **Project name rules.** (a) Trimmed, 1–100 characters, not unique within a Space (the PRD says nothing; Space names are explicitly not unique, `prd.md:200`); a stable `invalid-project-name` code. (b) Unique per Space — a constraint no requirement states, and a rename collision becomes a new refusal to design. **Recommend (a).**
4. **The `(SpaceId, Id)` key for AR-8.** (a) Add it now, so 2.2's `Task` FK is composite from its first migration. (b) Leave it to 2.2 — an extra migration on a populated table, still expand-only. **Recommend (a).**
5. **The `AFTER UPDATE` block predicate on `Project`.** (a) Add it: the database refuses a `SpaceId` change, so AC7 holds even against a future slice that tries. Existing tables carry `AFTER INSERT` only; adding `AFTER UPDATE` to them is not this story's. (b) Rely on the type (`init`) and the contracts. **Recommend (a).**
6. **Idempotency scope and the Token issuance replay.** Scope: (a) every `{spaceId}` state-changing route, header optional, records keyed `(SpaceId, AccountId, Key)`, in the pipeline; anonymous and Account-scoped routes stated out with reasons; (b) every state-changing route including registration and sign-in — those have no Space for the record's policy, and registration's replay is already indistinguishable. Issuance: (i) store and replay the body without the plaintext; (ii) exempt issuance from replay and answer a replayed key with a refusal; (iii) store the plaintext — **refused**: it breaks NFR-6 ("a datastore read yields no usable Token"). **Recommend (a) with (i).**
7. **Idempotency details.** Retention 24 hours, expiry evaluated on read, expired rows deleted only inside their own Space's unit of work (no cross-Space purge: the table is under the Space policy, and a bypass is forbidden); a reused key with a different request `422 idempotency-key-reused`; a concurrent first use `409 idempotency-key-in-flight`; failures never stored. Alternatives: keep records forever (unbounded table on the free tier), or treat a different-request reuse as a new request (silently defeats the guarantee). **Recommend as stated.**
8. **Listing order.** (a) Name, then id — the switcher's order, stable and predictable. (b) Creation order — what a person just created lands last, which suits a growing list. **Recommend (a)**; the list is at most 50 rows.
9. **`405` vs the fallback's `404`** (`deferred-work.md:300`). (a) Keep `404 not-available` with no `Allow` (AD-3: route shape is not disclosed). (b) Map explicit `405` handlers — discloses which methods exist on a Space-scoped route. **Recommend (a)**, pinned by a wire case.
10. **What a Project row is in 2.1.** (a) Text, with its rename control; it becomes a link to the Board in 2.2. (b) A link to a placeholder Project page — a route with nothing to show, and a by-id read 2.2 would redesign. **Recommend (a).** Create is not optimistic (the id is the server's).
11. **Rename interaction.** (a) Inline field in the row, explicit "Rename" commit and cancel, `Esc` cancels, optimistic with revert and a stated reason on refusal. (b) A dialog — one level deep, 6px radius, focus trapped and returned; heavier, and it brings the dialog radius gate (`deferred-work.md:36`) into scope. **Recommend (a).**
12. **How the client knows a Role lacks `ManageProjects`.** (a) A client predicate over the Role's stable code, held against `RoleCapabilityMatrix` by an architecture gate (the pattern `SpaceSwitcherGateTests` already uses for the Role copy). (b) The server adds the permitted capability codes to `GET /spaces/{id}` — one source of truth, but a contract change to an epic-1 route and its isolation rows. **Recommend (a).**
13. **The in-Space not-found arm** (`deferred-work.md:291`, `ISO-TIMING-IN-SPACE`). Rename is the first by-id lookup inside a Space, and its not-found does no refusal write, so it is likely separable from a boundary `404` by duration (1.6 measured the write at about half a boundary refusal's cost). (a) Keep the timing row pending for 2.2, make the bodies byte-identical now (`ISO-RENAME-FOREIGN-ID-IN-OWN-SPACE`), measure and record the gap in the Debug Log, and update the row's reason and the ledger. (b) Convert `ISO-TIMING-IN-SPACE` here on the rename route and equalise the work — a design decision about what the in-Space path does to cost the same, taken under a story whose subject is Projects. **Recommend (a)**; SM-1 is a release gate and nothing ships between 2.1 and 2.2.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List
