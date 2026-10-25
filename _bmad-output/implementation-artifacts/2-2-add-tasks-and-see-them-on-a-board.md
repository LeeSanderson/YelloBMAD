---
baseline_commit: 1395c0a5839c146b2fc95f999721f9ee54692cb2
---

# Story 2.2: Add Tasks and see them on a Board

Status: done

Epic: 2 — Track your own work on a Board
Story key: `2-2-add-tasks-and-see-them-on-a-board`
Requirements owned (coverage row `epics.md:355`): **FR-19** (create a Task; `prd.md:391-398`, `epics.md:112`), **FR-28** (the Board, without the 5,000-Task performance half, which is 2.9's; `prd.md:516-524`, `epics.md:128`), **AR-30** for the Tasks-per-Project bound (`epics.md:218, 775-785`; AD-25 `ARCHITECTURE-SPINE.md:224-228`), **UX-DR8** (Board primitives, `epics.md:246`), **UX-DR26** and **UX-DR27** (`epics.md:268-271`). Also used: **UX-DR35** (empty Project, `EXPERIENCE.md:251`), **AD-2 / AR-8** (composite keys carrying `SpaceId`), **AD-15** (the position key column, Q2), **AD-16 / AR-23** (the effective Status set, derived and never stored), **AD-18 / AR-25** (`Idempotency-Key` on the new write), **AD-29**'s count rule (`ARCHITECTURE-SPINE.md:249-259`), and the 1.9 gap gates (**AR-35, AD-4**) for every route this story adds.
Capabilities in the SPEC: **CAP-19**, **CAP-28** (`specs/spec-yello/SPEC.md:110-112`, `acceptance-criteria.md:192-196, 291-296`).
Ledger entries assigned to this story (`deferred-work.md`): 6, every one folded into a task or stated out of scope. See "Ledger entries owned by 2.2" in Dev Notes. Also folded: the 2.1 review's plant P11, which was caught in only 2 of 3 runs (Task 7).
Depends on: **2.1**, done (`Project`, the creation-bound and `Idempotency-Key` pipeline behaviours, Space home). The Status set is the Space default seeded at registration (story 1.3). Configuring it is Epic 6.
Extended later, not here: **2.3** (Task detail panel, edit title/Status/due date), **2.4** (Labels on the card), **2.5** (delete a Task), **2.6** (Move control, position-key moves, the BIN2 schema assertion and the mixed-case case), **2.7** (drag), **2.8** (keyboard grid), **2.9** (keyset paging, `aria-setsize`/`aria-posinset`, the 5,000-Task NFR-5 measurement, AR-40b), **2.10** (List View), **2.11** (small viewport, the context bar's breakpoint rules as ACs), **8.1** (FR-35 parity).

> **Two isolation gates bind this story, and you must read both before writing a route.**
> 1. **1.9-GATE-002** (`IsolationCaseListTests`, `Every_api_route_the_Host_maps_..._is_named_by_a_case`) fails the suite for any `/api` route the Host maps that no `IsolationCases` row names. Every row must state an expectation for **both** `Session` and `ApiToken`, and must actually send a request on the route it names. Every new route needs its rows in the same change that maps it.
> 2. **1.9-GATE-003** (`Every_pending_row_names_a_story_that_has_not_yet_reached_review`, story 2.1 Q1 (c)) lets a pending row pass while its story is `backlog`, `ready-for-dev` or `in-progress`. It **fails once 2.2 reaches `review`** if `ISO-TIMING-IN-SPACE` (`IsolationCases.cs:241-246`, `PendingUntil: "2.2"`) is still pending. That row is the only one pending on 2.2. Converting it is Task 9 and is not optional. The dev phase sets `review` at its end, so a forgotten conversion fails the dev phase itself.
>
> This create-story change moves 2.2 to `ready-for-dev`, which GATE-003 permits. The isolation suite stays green (verified at the end of this phase; see the Dev Agent Record).

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As someone tracking my own work,
I want to add Tasks to a Project and see them arranged in columns by Status,
So that value has landed before I have made a single structural decision.

## Acceptance Criteria

AC1–AC8 are reproduced **verbatim** from `epics.md:1023-1057` (`status: final`), numbered by position. AC9–AC15 come from the ledger, the 1.9 gap gates, the 2.1 review and the architecture; each one cites its source.

**AC1 — the empty Board**
**Given** a Project holding no Tasks
**When** the Board renders
**Then** it states "Nothing here yet." with one primary action to add a Task
**And** columns appear for the Project's effective Status set, in its effective order

**AC2 — create, first Status, one Project, one Space**
**Given** a Project
**When** an Owner, Admin or Member creates a Task
**Then** it takes the first Status in that Project's effective set
**And** it belongs to exactly one Project at a time and permanently to one Space

**AC3 — a Viewer's Board is a Member's Board without the manipulation affordances**
**Given** a Viewer
**When** the Board renders
**Then** it is identical to a Member's Board except that the create affordance, the drag handle and the Move control are **absent** — not greyed, not tooltipped, gone
**And** there is no disabled state anywhere in the product for a Role reason

**AC4 — exactly one column per Task**
**Given** any Task in the Project
**When** the Board renders
**Then** that Task appears in exactly one column, and no Task appears twice or in none

**AC5 — the column header**
**Given** a column header
**When** it renders
**Then** it shows the Status name, then the **true total** for that Status queried over the indexed predicate rather than the number of rows currently rendered, then the create affordance pushed right

**AC6 — the Task card**
**Given** a Task card
**When** it renders
**Then** the title is the only content and everything else is metadata set in monospace, and the card carries no shadow
**And** no count anywhere acts as a nudge — there are no badges, streaks or re-engagement prompts, because engagement is a counter-metric

**AC7 — the 5,001st Task is refused**
**Given** a Project already holding 5,000 Tasks
**When** a 5,001st is attempted
**Then** it is refused inside the same transaction with a machine-readable reason and copy stating the bound

**AC8 — the schema**
**Given** the Task entity
**When** the schema test runs
**Then** it carries a non-nullable `SpaceId` directly rather than reachable by join, so the row-level security predicate never needs one

**AC9 — every new route is an isolation row on both surfaces** *(1.9-GATE-001/002, AR-35, AD-4; test-design I-1, I-2, I-3 "includes Board column counts", I-4, I-5, I-13)*
**Given** the Board and Task routes this story maps
**When** the isolation suite runs
**Then** each route is named by at least one `IsolationCases` row with an expectation for **both** `Session` and `ApiToken`
**And** a foreign Space's Board, column counts and Tasks are unreachable by read and by write on both surfaces, with the boundary `404` byte-identical to an absent Space, one `CrossSpace` row, and nothing written
**And** a foreign Project id used inside one's own Space answers the same bytes as a random id, with no refusal row

**AC10 — the in-Space not-found is indistinguishable by duration, and the pending row is gone** *(`deferred-work.md:291, 411`; AD-3 `ARCHITECTURE-SPINE.md:88-94`; I-6)*
**Given** `ISO-TIMING-IN-SPACE`
**When** 2.2 reaches `review`
**Then** it is a live row on both surfaces, comparing a boundary `404` with an in-Space not-found on a by-id route of this story through the one duration method (`DurationIndistinguishability`: 21 samples, median, 20%)
**And** the measured gap is recorded in the Debug Log whatever the outcome
**And** no isolation row names 2.2 as pending

**AC11 — the Tasks-per-Project bound is the pipeline's, scoped to the Project, and its race is proved deterministically** *(AR-3, AD-25; 2.1 review plant P11)*
**Given** the Tasks-per-Project bound
**When** a creation consults it
**Then** the limit is read from `BoundRegistry`, counted by the pipeline for the route's Project under a lock held to the end of the unit of work, and no slice counts
**And** two creations sent concurrently at 4,999 yield exactly one `201` and one `409`, proved by a case that does not depend on the race happening to race (Task 7); the same is true of 2.1's Projects-per-Space pair

**AC12 — the idempotency record cannot silently mis-record a write** *(`deferred-work.md:465-470`)*
**Given** a Space-scoped state-changing endpoint
**When** the Host starts, or the architecture suite runs
**Then** an endpoint whose result cannot be recorded faithfully for replay is refused, and a duplicate-key error is reported as `idempotency-key-in-flight` only when it is the record's own

**AC13 — the client says something true about every refusal it can meet** *(`deferred-work.md:460`; UX-DR32, UX-DR37)*
**Given** a Task creation answered `409 idempotency-key-in-flight` or `422 idempotency-key-reused`
**When** the client renders it
**Then** it renders copy of its own for each, inline in the creation surface, never the generic "Not available."

**AC14 — Space home and the Board keep what a person typed** *(`deferred-work.md:481`)*
**Given** an open create or rename form on Space home, or an open Task creation on the Board
**When** the same Space's (or Project's) route is navigated to again
**Then** the form and its text survive, and an in-flight creation's confirmation is not dropped, while the Role is still re-read from the server

**AC15 — the Board is a Space-scoped surface like any other** *(UX-DR27, UX-DR31, UX-DR33, UX-DR37; `EXPERIENCE.md:85, 164-179, 251, 279, 390`; `deferred-work.md:338`)*
**Given** the Board in the client
**When** it renders, refuses, or the tab leaves the Space
**Then** nothing of the Board renders before the server has answered, and a skeleton matching the columns, marked `aria-busy="true"`, shows while it is out
**And** it is purged synchronously with the Space, and no Project or Task name appears in `document.title` or a URL
**And** every Task title input carries `autocomplete="off"` and a non-reusable field name, every string is externalised, and a keyboard user can pass the context bar in one step (Q9)

## Tasks / Subtasks

- [x] **Task 1 — Confirm the gates, then record decisions before the code that depends on them** (AC: all)
  - [x] `dotnet build Yello.slnx`, then run `Yello.Tests.Isolation` filtered to `IsolationCaseListTests`. With 2.2 at `ready-for-dev` or `in-progress` the result is green, and `ISO-TIMING-IN-SPACE` is the one pending row. Record this in the Debug Log.
  - [x] Read the open questions. Decide Q1–Q4 before Task 2, Q5–Q6 before Task 4, Q7 before Task 9, Q8–Q10 before Task 10. Q11 can be decided at any time. Record each decision in the Completion Notes as "**Qn - (x).** … Cost to later stories: …". Each decision is reversible.

- [x] **Task 2 — The Task entity, its table and its policy** (AC: 2, 4, 8)
  - [x] Domain type in `Yello.Domain/Tasks/`. **The name collides with `System.Threading.Tasks.Task`** under the implicit usings that `Opinionated.DotNet.CodingStandards` turns on. Q1 decides the CLR name. Whatever it is, update `RefusalBoundaryGateTests.SpaceScopedEntities` (`RefusalBoundaryGateTests.cs:116-122`, which names `Yello.Domain.Tasks.Task` today) to the real full name. Then add an assertion that every listed name, other than `Label` (2.4), resolves to a type in the Domain assembly. Today a wrong name in that list silently covers nothing. Plant: misspell it, and the build must go red.
  - [x] Properties: `Id` (`init`); `SpaceId` (`init`, non-nullable, AC8, never settable after creation); `ProjectId` (settable only so FR-41/6.5 can move it within a Space; **no** 2.2 code path writes it after creation); `StatusId`; `Title` (settable, used by 2.3); `PositionKey` (Q2); `CreatedAt` (`DateTimeOffset`, UTC). There is **no description field** (`epics.md:437`; FR-31 is Epic 7). There are no navigation properties.
  - [x] Title rules in Domain (AR-34): a `TaskTitle.TryNormalise` like `ProjectName` (Q3).
  - [x] Configuration `TaskConfiguration(YelloDbContext context)`:
    - Table `Task` (Glossary-exact; `SchemaNames.TaskTable`, added to `SpaceScopedTables`).
    - `ValueGeneratedNever()` on `Id`.
    - `SpaceId` mapped to `SchemaNames.SpaceIdColumn`, required.
    - `HasQueryFilter(t => t.SpaceId == context.ScopedSpaceId)`.
    - Composite FK `(SpaceId, ProjectId)` → `Project` alternate key `AK_Project_SpaceId_Id` (already exists, 2.1 Q4), `Cascade` (2.5 deletes a Project's Tasks with it).
    - Composite FK `(SpaceId, StatusId)` → `StatusDefinition`. **`StatusDefinition` has no `(SpaceId, Id)` key today** (`StatusDefinitionConfiguration.cs:26, 49`): add `HasAlternateKey(s => new { s.SpaceId, s.Id })`, expand-only.
    - The Status FK is `NoAction`/`Restrict`, and there is **no separate FK from Task to `Space`**. SQL Server refuses multiple cascade paths (error 1785): Space→Project→Task and Space→StatusDefinition→Task would be two. `SpaceId` is already constrained through the Project FK, which carries it.
  - [x] Index for the count and the column read: `(SpaceId, ProjectId, StatusId, PositionKey)`, unique if Q2 (a). `SpaceId` leads, so a seek and its key-range lock stay inside one Space's range (see `deferred-work.md:471`).
  - [x] Migration `…_Tasks`:
    - The `StatusDefinition` alternate key, then the table.
    - `ALTER SECURITY POLICY dbo.SpaceIsolationPolicy ADD FILTER PREDICATE … ON dbo.[Task], ADD BLOCK PREDICATE … ON dbo.[Task] AFTER INSERT, ADD BLOCK PREDICATE … ON dbo.[Task] AFTER UPDATE`. The `AFTER UPDATE` block predicate makes "permanently to one Space" (AC2) a database fact, as 2.1 Q5 did for `Project`.
    - Bracket `[Task]`. `TASK` is not reserved in T-SQL, but brackets cost nothing.
    - Use literal names, not `SchemaNames` (`SchemaNames.cs` remarks).
    - `Down` drops the predicates before the table.
    - Pattern: `20261025094743_Projects.cs`.
    - Hand-edit pass: `TESTING-CONVENTIONS.md:298-318`, with `Up` under 80 lines.
  - [x] `YelloDbContext`: `DbSet` plus an explicit configuration line. `PersistenceModelGateTests` fails otherwise. `SpaceIsolationSchemaTests.Every_Space_scoped_table_carries_a_filter_and_a_block_predicate` asserts **exactly three** predicates for `Project` and exactly two for every other table (2.1 Debug Log). Widen it so `Task` also expects three.
  - [x] **No table stores the effective Status set** (AD-16, AR-23). With no delta table until 6.1, the effective set is the Space's `StatusDefinition` rows in `Position` order. Read it through the existing `IStatusDefinitionReader` and derive it in one place (for example `EffectiveStatusSet.Of(spaceDefaults)` in Domain), so 6.1 adds the delta in one function. Ties: `StatusDefinitionReader` orders by `Position` with no tiebreaker (`deferred-work.md:271`). Add `.ThenBy(Id)` so column order is deterministic between requests. Doing so does not close that entry, which concerns custom Statuses.

- [x] **Task 3 — Contracts** (AC: 1, 2, 5, 9)
  - [x] `Yello.Contracts/Tasks/TaskRoutes.cs`, with shapes per Q5:
    - `Create` = `POST /api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/tasks`.
    - `TaskOf(spaceId, taskId)` = `/api/v1/spaces/{s}/tasks/{t}`. This is the creation's `Location`, and 2.3's by-id route. A Task can change Project later (FR-41), so its URL must not contain the Project.
  - [x] `Yello.Contracts/Boards/BoardRoutes.cs`: `Board` = `GET /api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/board`.
  - [x] `CreateTaskRequest(string? Title)`. **It carries no Status, Project, Space or position**: AC2's "first Status" is then true by construction.
  - [x] `TaskResponse(Guid Id, string Title, Guid StatusId, string PositionKey, DateTimeOffset CreatedAt)`. `PositionKey` is readable and not writable (FR-35, AD-15).
  - [x] `BoardResponse(BoardProjectResponse Project, IReadOnlyList<BoardColumnResponse> Columns)`.
  - [x] `BoardColumnResponse(Guid StatusId, string Name, int Total, IReadOnlyList<TaskResponse> Tasks)`. `Total` is the separate count (AC5), never `Tasks.Count`.
  - [x] Nothing carries a `SpaceId` (as `StatusResponse`'s remarks say).
  - [x] `ProblemTypes.InvalidTaskTitle` (`400`), with remarks stating what it never means.
  - [x] Client copy for the two idempotency refusals (Task 11) uses the existing `IdempotencyKeyInFlight` and `IdempotencyKeyReused` types. Do not add new types for them.

- [x] **Task 4 — The slices and endpoints** (AC: 1, 2, 4, 5, 9)
  - [x] `Yello.Application/Tasks/CreateTask/` (AR-3 naming from FR-19's title) and `Yello.Application/Boards/ViewBoard/` (the spine's capability map: `Application/Boards/*`, `ARCHITECTURE-SPINE.md:412-415`). Handlers take the pipeline's `ActiveSpaceContext` or nothing. **None reads the Role, counts Tasks, checks a capability or touches `Idempotency-Key`**; the existing gates catch each.
  - [x] Ports in `Yello.Domain/Tasks` and `Yello.Domain/Boards` (or one store), implemented in Infrastructure and registered beside `ProjectStore` (`SpaceContentStores.cs`; `InfrastructureServices` is at S1200's coupling limit, per the 2.1 Debug Log). There is **no `Where(SpaceId == …)`**. Both layers are already narrowed.
  - [x] **Create**:
    1. Confirm the Project is visible (RLS and the filter hide another Space's). If it is not, return `null` → the endpoint answers `BoundaryProblems.NotAvailable()` byte-identically, with **no** refusal record (AD-20). An FK violation must never surface as a `500`.
    2. The first Status of the effective set is the Task's `StatusId`.
    3. `SpaceId` comes from the context, never the request.
    4. The id comes from `IIdentifierGenerator`.
    5. The position key is per Q2.
  - [x] **Board read**:
    1. The Project (not found → the same `404`, no record).
    2. The effective set, in order.
    3. Per Status, the **true total** as its own `COUNT` over `(SpaceId, ProjectId, StatusId)`. One grouped `COUNT … GROUP BY StatusId` is acceptable. It must not be derived from the rows returned (AC5, AD-29).
    4. The Tasks per column ordered by `PositionKey` then `Id` (Q6 decides how many).
    5. Materialise before return (`UnitOfWorkResults.EnsureMaterialised`).
    6. A Task whose `StatusId` is not in the effective set cannot exist in 2.2 (composite FK, first-Status create). Still, assert AC4 structurally: the read assigns each Task to exactly one column by `StatusId`, and a test seeds Tasks across all three Statuses and checks the union of the columns equals the Project's Tasks with no duplicate.
  - [x] Endpoints in `Yello.Host/Endpoints/TaskEndpoints.cs` and `BoardEndpoints.cs`, mapped through `api` in `Program.cs`:
    - `POST` create → `RequiresCapability(Capability.ManageTasks)` (wire code `manage-tasks`; copy exists, `ClientCopy.resx:410`) and `.ConsultsBound(Bound.TasksPerProject)` → `201` with `TaskResponse`. Set `Location` on `httpContext.Response.Headers` as `ProjectEndpoints.CreateAsync` does, never via `Results.Created` (see Task 8). Responses: `400 invalid-task-title`; `404 not-available` for an unknown Project.
    - `GET` board → `RequiresCapability(Capability.ReadProjectsAndTasks)` → `200`.
    - Neither is `SessionOnly`. Read the body inside the endpoint (`ProjectEndpoints.ReadAsync` pattern, including the `InvalidOperationException` catch the 2.1 review added).
  - [x] Structured logs: `SpaceId`, never a title or a Project name. Add every Task title an isolation case creates to the world's secret set (I-17).

- [x] **Task 5 — The Tasks-per-Project bound, in the pipeline** (AC: 7, 11)
  - [x] **The count is per Project, and the pipeline does not know the Project today.** `IBoundCounter.CountAsync(Bound, CancellationToken)` counts across the active Space (`BoundCounter.cs`). Extend the port with the scope the bound needs, for example `CountAsync(Bound bound, Guid? scopeId, …)`. For `TasksPerProject`, the pipeline reads `scopeId` from the route value `projectId` (`ProjectRoutes.ProjectIdParameter`), and the declared bound fails closed (Host refuses to start, or the call throws) on a route that lacks it. This is the pipeline's work, not the slice's (AR-3). `Only_the_pipeline_depends_on_the_bound_count_port` still holds.
  - [x] SQL, in Infrastructure only: `SELECT COUNT(*) FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = @projectId`. RLS narrows it to the Space, and the Task 2 index gives a seek on `(SpaceId, ProjectId, …)`, so the range lock covers one Project's range plus the next key. Use a parameterised `SqlQuery` (FormattableString), never concatenation.
  - [x] **Serialisation side effect, which Q2 (a) relies on:** because every Task creation counts under that lock before the endpoint runs, two creations in the same Project serialise, so "append after the current last key" cannot produce a duplicate key. Say this in `BoundCounter`'s remarks and in the position-key code.
  - [x] An unknown or foreign `projectId` counts 0 and passes. The slice then answers `404`. Prove this (Task 12).
  - [x] Wire cases: seed 4,999 Tasks by raw SQL under the Space session key (as `SeedProjectsAsync` does), then the 5,000th (`201`, the anchor), then the 5,001st → `409 bound-reached`, `bound: tasks-per-project`, `limit: 5000`. `SELECT COUNT(*)` is still 5,000, and no refusal row exists. Copy exists: "This Project has {0} Tasks, the maximum." (`ClientCopy.resx:474-476`).
  - [x] **A full Project does not block another Project.** Anchor: Project P2 in the same Space creates a Task while P1 holds 5,000.
  - [x] Replay before bound (2.1 Task 7 order): the key of the 5,000th creation replays its `201`, not the 5,001st's `409`.

- [x] **Task 6 — Idempotency on Task creation** (AC: 2, 12)
  - [x] Nothing new in the pipeline: `POST …/tasks` is a `{spaceId}` state-changing route, so the 2.1 behaviour applies. Wire cases on **both** surfaces: same key twice → one row, identical bodies and `Location`; same key, different title → `422`; Account B's key does not replay A's record.

- [x] **Task 7 — Make the bound race deterministic (2.1 review P11)** (AC: 11)
  - [x] P11 planted "count without `UPDLOCK, HOLDLOCK`" against BND-002 (`ProjectEndpointTests.Bound.cs`, `Two_concurrent_creations_at_49_yield_exactly_one_201_and_one_409`). It was caught in 2 of 3 runs, because the case fires two requests with `Task.WhenAll` and depends on the race happening to race. The gate is in this story's area: 2.2 adds the same bound path for Tasks and changes `BoundCounter`.
  - [x] Rewrite BND-002, and write its Task twin, on the pattern the 2.1 review used for R8 (`ProjectEndpointTests.Idempotency.cs:280-360`, `A_concurrent_first_use_of_one_key_…`, which polls `sys.dm_exec_requests` for `LCK_M_RS_U`):
    1. Seed to limit − 1.
    2. The **test** opens a connection, sets the Space session key, begins a transaction and takes the same range: `SELECT COUNT(*) … WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = …`.
    3. Send both creations.
    4. Poll until **both** requests are blocked on **any** `LCK_M_%` wait, with a deadline and no `Task.Delay`.
    5. Commit the test's transaction.
    6. Assert one `201`, one `409`, and the count equals the limit.
  - [x] Why this is deterministic either way:
    - With the hint, both requests queue on the count (`LCK_M_RS_U`).
    - With the plant (no hint), both counts read through the test's `RangeS-U` locks (S is compatible with U). Both inserts then block on `RangeI-N` (`LCK_M_RIn_NL`) until the test commits, and both then succeed, so the limit is exceeded and the case fails every time.
    - Wait on "blocked on a lock", not on a specific wait type, so the plant is caught at the insert.
  - [x] Plant P11 again, three runs for Projects and three runs for Tasks. All six must be caught. Record the result in the Debug Log.

- [x] **Task 8 — The idempotency record's capture assumptions** (AC: 12; `deferred-work.md:465-470`)
  - [x] Each of the four latent defects either becomes impossible or is stated with a test:
    - **Non-JSON success replayed empty.** A gate (architecture suite, reading the real route table as GATE-002 does) or a startup check beside `SpaceScopedRouteCheck` fails for a Space-scoped `POST`/`PUT`/`PATCH`/`DELETE` endpoint whose handler's declared return type is not `IResult`/`Task<IResult>`. Every such handler today returns `Results.Json`/`Results.Ok`. The record keeps the body only from an `IValueHttpResult`. State that rule in `IdempotentResponses.Capture`'s remarks.
    - **`Results.Created` replays with no `Location`.** A gate bans `Results.Created`, `TypedResults.Created`, `CreatedAtRoute` and `Accepted*` in `Yello.Host/Endpoints`. The comment in `ProjectEndpoints.CreateAsync` is the convention, and the gate holds it. Plant: the Task create uses `Results.Created`. It must be caught.
    - **A non-`IResult` is `500` only when keyed.** This is closed by the first bullet.
    - **Any duplicate-key error reads as in-flight.** In `IdempotencyStore.TryRecordAsync`, before `SaveChangesAsync`, fail closed (throw) if the change tracker holds any pending entry other than the record. Stores save their own effects (`ProjectStore.CreateAsync` does), so only the record is flushed, and a 2601/2627 there can only be the record's own index. Plant: a store that leaves its insert unsaved must be refused, not reported as `409`.
  - [x] Close `deferred-work.md:465` with a dated note.

- [x] **Task 9 — Isolation rows; `ISO-TIMING-IN-SPACE` converted** (AC: 9, 10)
  - [x] Add rows from the "Isolation rows" table in Dev Notes, each covering **both** surfaces. Seed through the product: A creates Project `PA` with Task `TA` in `SA`; B creates `PB` with `TB` in `SB`.
  - [x] **Convert `ISO-TIMING-IN-SPACE`** (`IsolationCases.cs:241-246`):
    - Route: `BoardRoutes.Board`, `GET`.
    - Arm 1: the boundary `404`, `GET …/spaces/{SB}/projects/{PB}/board` by A.
    - Arm 2: the in-Space not-found, `GET …/spaces/{SA}/projects/{random}/board` by A.
    - Use `IndistinguishableFromAbsent` or a sibling expectation through `DurationIndistinguishability.CompareMeasuredAsync` at the Host's request-finished line (`IsolationTimingTests` remarks).
    - Remove `PendingUntil` and `PendingReason`.
    - The permanent oracle (boundary vs unmatched fallback) stays.
  - [x] **Measure first, then decide (Q7).** The boundary path writes an `AccessRefusal` (~3 ms of ~6.5 ms, `deferred-work.md:291`). The in-Space not-found does not. Record both medians and the gap per surface in the Debug Log, whatever the outcome; 2.1 left this unmeasured.
    - If the gap exceeds 20%, equalise per Q7 and re-measure.
    - Run the timing class alone (it is in a non-parallel collection), three times, and record all three. A timing gate caught 2 of 3 times is not a gate.
  - [x] I-3 "includes Board column counts": the aggregate row asserts each column's `Total` counts only A's Tasks after B has created Tasks in B's Space.
  - [x] Plant (one at a time, original held in memory and restored in a `finally`): drop the Board read's query filter **and** the `Task` FILTER predicate → `ISO-BOARD-AGGREGATE` caught on both surfaces. Run the two one-layer anchors as well; each must stay green.
  - [x] Close `deferred-work.md:291` with the measured numbers and the Q7 decision.

- [x] **Task 10 — The Board page** (AC: 1, 3, 4, 5, 6, 14, 15)
  - [x] Client route `/spaces/{SpaceId:guid}/projects/{ProjectId:guid}` (`ClientRoutes.Board(spaceId, projectId)`). `ClientRoutes.SpaceIn` already reads the Space from the first two segments, so the purge hook covers it. A Project row on Space home becomes a link to it (2.1 Q10's promised change in `ProjectRow.razor`). The link text is the Project name, and the URL holds ids only.
  - [x] **Arrival:** do what `SpaceHome.razor` does, in order.
    1. Unlisted Space id → refusal surface with no request.
    2. `GET` Active → `ActiveSpace.Arrive`.
    3. `GET` Board.
    4. One `RefusalSurface` for every `404`. `401` is `YelloApiHandler`'s.
    5. Heading focus rule.

    Share the arrival through a component or a service rather than copying it. Two copies of the refusal and purge ordering would drift.
  - [x] **Re-arrival (AC14, `deferred-work.md:481`):** navigating to the same Space's home, or to the same Board, does **not** unmount the page's content. The Role is re-read (`GET` Active) and applied in place. Controls a now-Viewer lacks disappear, and an open form whose Role no longer permits it closes. A different Space or Project still remounts (`@key`), and the purge still runs on a Space change. Fix `SpaceHome.razor`'s `OnParametersSetAsync`, which today sets `_arrived = false` on every parameter set, and build the Board the same way. Browser cases: an open create on Space home survives a click on the same Space in the switcher; the Board's open creation survives re-navigation to the same Board.
  - [x] **Layout** (DESIGN.md `task-card` 137-144, `column` 149-155, `column-count` 156-161, prose 389-453; EXPERIENCE.md 199-201):
    - Columns side by side, in effective order. Each scrolls **within itself**, never the page.
    - Gutter 9px (`gutter`), card gap 6px (`card-stack-gap`).
    - Column head: `typography.column-head` (uppercase by `text-transform`, never in the string), then the count chip, then the create affordance pushed right (`margin-inline-start: auto`).
    - At 768–1279px the Board scrolls horizontally. Below 768px is 2.11's pager, and nothing here may preclude it.
  - [x] **Column count chip:** `{components.column-count}` is `rounded.full`, the **only pill in the product**. Tighten `The_fully_round_radius_is_declared_once_and_used_by_at_most_one_component` to **exactly one** consumer (`deferred-work.md:32`) and close that entry. The chip shows `Total` from the server, not the rendered row count. It is plain text, not a badge: no colour emphasis and no live updating beyond the user's own create.
  - [x] **Task card:**
    - Title in `typography.task-title`.
    - A metadata row in monospace (`typography.meta`) that is empty in 2.2. Labels (2.4), Assignee (4.6) and Presence (Epic 7) fill it, so render no placeholder.
    - Border `--hairline-width`, 3px radius, `box-shadow: none` declared explicitly (DESIGN.md:431).
    - No click target (detail is 2.3). The card is not focusable in 2.2: 2.8 builds the grid.
    - No drag handle and no Move control for **any** Role, because they arrive in 2.6/2.7. AC3's absence for Viewers therefore holds trivially here. Write the Viewer browser case so it also asserts the *create* affordance is absent and a Member's is present, and so 2.6/2.7 extend it rather than invent one.
  - [x] **Radius mapping (`deferred-work.md:36`):** 2.2 builds Task cards and columns (3px). Add the mapping clause for those two to the radius gate (`DesignFoundationGateTests.cs:720-750`), with a plant (a 2px card), and update the entry to say which surfaces remain.
  - [x] **Structural borders (`deferred-work.md:66`):** card and column borders are structural. The device-pixel snapping half is owned by **UX** and needs a specification that does not exist. Do not invent one. Use `--hairline-width` (1.5px floor, gated), and state in the Completion Notes that the snapping remains with UX. Q10 records this.
  - [x] **Empty Project (AC1):** columns render (`epics.md:1026`), plus "Nothing here yet." and one primary action, "Add a Task" (`EXPERIENCE.md:251`). There is no sub-line: the mock's "Add a Task and get on with it." loses to the spine (DESIGN.md:482). Nothing about Statuses or configuration.
  - [x] **Create (Q4, Q8):** the affordance and the inline form are described in Q4 and Q8.
    - The title field carries `autocomplete="off"` and a random `FieldName` (2.1's `FormField`), plus one primary "Add" button. `Esc` cancels.
    - The request is not optimistic (the id and the position key are the server's).
    - It carries an `Idempotency-Key`, reused for a resend after a lost answer (2.1 review R3, `ProjectList.razor` pattern).
    - On `201`: append the card to the first column, increment that column's `Total` by one, keep the field open and empty for the next, and announce politely ("Task added.").
    - On `400 invalid-task-title`: inline error.
    - On `409 bound-reached`: "This Project has 5000 Tasks, the maximum." inline via `RefusalCopy.For` (the number formatted by culture as the resource does).
    - On `403`: the `manage-tasks` capability copy inline (UX-DR32).
    - On `404`: the page's one refusal surface.
  - [x] **Absent, not disabled (UX-DR27):** add `TaskCapabilities.PermitsManageTasks(roleCode)` beside `ProjectCapabilities`, and extend ARCH-007 (`The_clients_Project_controls_predicate_agrees_with_the_capability_matrix`) or a sibling gate to hold it against `RoleCapabilityMatrix` for `ManageTasks`. A Viewer gets no "+", no empty-state action and no form.
  - [x] **Cold load:** skeleton columns with no text, marked `aria-busy="true"`; completion announced politely once. Nothing is announced on load beyond that (`EXPERIENCE.md:390`).
  - [x] **Purge:** register the Board with `ActiveSpaceState.RegisterPurge` (`ActiveSpaceState.cs` remarks: "Epic 2 registers its Board here"). Every request uses `ActiveSpace.Token`. Nothing goes in browser storage (`ClientStorageBanTests`). `document.title` stays `Yello`. Disable scroll restoration on the route if the framework restores it (`EXPERIENCE.md:179`).
  - [x] **Context bar: Project name, and the skip link (Q9, `deferred-work.md:338`):**
    - On the Board, the bar shows the current Project's name in `typography.meta` after the Space name (DESIGN.md:454). It comes from the Board's own server answer on this load, never from Space home's list. It is purged with the Space and cleared on leaving the Board.
    - At 768–1279px it is dropped by CSS (`EXPERIENCE.md:413-419`; the Role chip is never dropped).
    - Add the skip control (Q9) and close `:338`.
  - [x] **Copy:** `ClientCopy.resx` only, sentence case (UX-DR37): "Nothing here yet.", "Add a Task", "Add", "Cancel", the title label, "Task added.", the loading text, `invalid-task-title`, the skip control's label, and the two idempotency strings (Task 11). Never "archive" or "save". `LocalisationGateTests` holds the rest.
  - [x] **Text spacing (`deferred-work.md:20`, X-11):** that entry's owner is "the first story with a real surface, after B5". 2.1's surface did not take it, and the Board is the product's primary surface. Either add one browser case per surface (Space home, Board) applying the 1.4.12 text-spacing override and 200% text-only zoom and asserting no clipped or overlapping text, or re-own the entry explicitly in the ledger with a reason. Do not leave it silent.

- [x] **Task 11 — Client copy for the idempotency refusals** (AC: 13; `deferred-work.md:460`)
  - [x] `RefusalCopy.For` maps `idempotency-key-in-flight` to its own key ("That was already sent. Reload to see it.") and `idempotency-key-reused` to its own key ("That request was already used for something else. Try again."). `RefusalCopyTests` covers both. Both Space home's create and rename and the Board's create render them inline.
  - [x] Browser case: the fake answers a Task creation `409 idempotency-key-in-flight`, and the creation surface shows the in-flight copy, not "Not available.". Close `:460`.

- [x] **Task 12 — Slice, schema and wire tests** (AC: 2, 4, 5, 7, 8)
  - [x] `tests/Yello.Tests.Slices/Tasks/…` and `…/Boards/…`, on `MigratedDatabaseFixture` and `HostProcess`/`WireSession` as 2.1's `ProjectEndpointTests` are. Cases:
    - Create in the active Space only, with `SpaceId` from the context, the generator's id, and the first effective Status. Anchor: the Space's three Statuses exist, and the Task is in the one with the lowest `Position`.
    - A Viewer refused `403 manage-tasks` with one `InsufficientRole` row and no row written, on both surfaces, with a Viewer added to an Owner's Space (not 2.1's demoted-own-Space variance).
    - An unknown Project id answers `404`, byte-identical to the boundary's, with no refusal row and no Task written.
    - `invalid-task-title`.
    - Board columns in effective order, with totals equal to a `SELECT COUNT(*)` per Status.
    - AC4's union and no-duplicate check.
    - The empty Project's Board answers three columns, each `Total: 0` with no Tasks.
  - [x] AC8 and AC2 at the schema: `Task` carries a non-nullable `SpaceId` with three predicates; an `UPDATE dbo.[Task] SET SpaceId = …` fails on the `AFTER UPDATE` block (error 33504); the composite FKs exist; and the `StatusDefinition` alternate key exists. AD-17's invariant (no Task holds a Status absent from its Project's effective set) as a query-level test over the seeded data; S-2 proper belongs to 2.3 and Epic 6.
  - [x] A gate holding Task's `SpaceId` writes to the create path, sibling of 2.1's `Only_the_create_path_writes_a_Projects_Space`.

- [x] **Task 13 — Plants, runs, ledger, conventions** (AC: all)
  - [x] Plants run one at a time in the foreground. Hold the original content in memory, restore it in a `finally`, and compare the tree hash after each. **Never** `git checkout --`, `git restore`, `git reset` or `git stash`. Write a plant so that it compiles (2.1's P4 and P5 failed on a missing `<param>`).
  - [x] Record `| # | Plant | Gate | Caught |` in the Debug Log. Minimum set:
    - `ISO-TIMING-IN-SPACE` left pending with 2.2 at `review` (GATE-003).
    - A Task route with no isolation row (GATE-002).
    - The two-layer disclosure (Task 9).
    - The Task count in the slice (A-12 bound-port gate).
    - P11, three runs each for Projects and Tasks (Task 7).
    - `Results.Created` in the Task endpoint (Task 8).
    - An unsaved effect beside the idempotency record (Task 8).
    - A misspelt `SpaceScopedEntities` entry (Task 2).
    - A Viewer offered "+" (client gate and browser case).
    - The Board written to `sessionStorage`.
    - A Project name in `document.title`.
    - A shadow on the Task card (`DesignFoundationGateTests`).
    - A second `--radius-full` consumer.
    - The `AFTER UPDATE` predicate removed for `Task`.
    - `Total` taken from `Tasks.Count` (a case where the server answers fewer rows than the total must catch it; Q6).
  - [x] Runs: `dotnet build Yello.slnx` (warnings are errors), then `dotnet test Yello.slnx` (MTP; `--no-build` runs zero tests unless the same configuration was just built). Record totals and wall clock. Baseline after the 2.1 review: **633 total, 630 passed, 0 failed, 3 skipped** (`YELLO_MIGRATION_BUNDLE` unset). If a Client case fails only in the full run, run `dotnet test --project tests/Yello.Tests.Client` alone before calling it a regression. If Rancher's socket times out: `rdctl shutdown`, `wsl --terminate rancher-desktop`, then relaunch.
  - [x] `tests/TESTING-CONVENTIONS.md`: the deterministic concurrency pattern (Task 7) as the rule for every race case, the Q7 timing decision, and the new counts.
  - [x] Ledger (`deferred-work.md`):
    - CLOSE with dated notes: `:291`, `:338`, `:460`, `:465`, `:481`, `:32`.
    - UPDATE: `:36` (which surfaces remain), `:20` (done or re-owned).
    - ADD anything this story defers, each with an owner.
    - Leave `:471` and `:477` with 2.9, but note in `:471` that the Task count's lock is narrowed by the `SpaceId`-leading index.
  - [x] `sprint-status.yaml`: set `review` at the end of the dev phase. GATE-003 then fails if `ISO-TIMING-IN-SPACE` is still pending. That is the gate working.
  - [x] **Do not add review narration to source files.**

### Review Findings

Code review 2026-10-25 (commit range `f37b9b2..HEAD`; Blind Hunter, Edge Case Hunter, Acceptance Auditor). 31 raw findings, 27 after deduplication: 0 decision-needed, 12 patch, 5 defer, 10 dismissed.

- [x] [Review][Patch] AC12 was only half held: a Space-scoped write declared `Task<IResult>` that answered `Results.Text`/`Bytes`/`File`/`Stream` passed ARCH-010 and was replayed with an empty body. `Capture` now refuses it, with a case and a bodiless anchor [Yello.Host/Spaces/IdempotentResponses.cs:118]
- [x] [Review][Patch] A re-arrival whose Role re-read failed (network error, timeout, `503`) replaced the page and its open form with the refusal surface, which defeated AC14. Only a `404` refuses a re-arrival now [Yello.Client/Components/SpaceArrival.razor:151]
- [x] [Review][Patch] An `HttpClient` timeout was taken for a purge, which left the Board as a skeleton for good and the Task field silent. A timeout is now a failure; only the purge token's own cancellation is silent (also Space home's Project list load) [Yello.Client/Components/BoardView.razor:224]
- [x] [Review][Patch] Clicking "Add" removed the focused button, so focus fell to `<body>` during rapid entry. The buttons stay while a creation is out; a second submit and Cancel/Esc are ignored until it answers [Yello.Client/Components/TaskCreate.razor:47]
- [x] [Review][Patch] The refusal and progress live regions were inserted with their text already in them, so they could go unannounced. They are now always present, and empty ones are visually hidden [Yello.Client/Components/TaskCreate.razor:43]
- [x] [Review][Patch] A title typed while a creation was in flight was erased by its `201`. The field now clears only if it still holds the sent title [Yello.Client/Components/TaskCreate.razor:170]
- [x] [Review][Patch] A `201` whose body could not be read dropped its `Idempotency-Key`, so a resend could create the Task twice. The key is kept until the created Task is read [Yello.Client/Components/TaskCreate.razor:130]
- [x] [Review][Patch] A first arrival's heading-focus request outlived a refused page and could be claimed by a later page's heading. A refusal now discards it [Yello.Client/Components/SpaceArrival.razor:225]
- [x] [Review][Patch] The not-found floor accepted any value up to about 24 days, and its comment said the wait holds no connection. It is capped at 2 s, falling back to the default with a startup problem, and the comment is corrected [Yello.Host/Spaces/NotAvailableFloor.cs:62]
- [x] [Review][Patch] ISO-BOARD-AGGREGATE asserted only the sum of the column totals, against the Dev Notes' "each column's `Total`". It now asserts each column [tests/Yello.Tests.Isolation/IsolationCases.cs:569]
- [x] [Review][Patch] AC14's Role half had no case. A browser case now lowers the Role between arrivals: the open Task field and every "+" go, and the Board stays. Planted (Role made sticky in `BoardView`): caught [tests/Yello.Tests.Client/BoardTests.cs:372]
- [x] [Review][Patch] `GlossaryTableNames` was inserted between `Every_entity_configuration_reaches_the_model`'s documentation and its `[Fact]`, so the method's summary documented the field. It has moved above [tests/Yello.Tests.Architecture/PersistenceModelGateTests.cs:25]
- [x] [Review][Defer] Position keys are safe only under the Tasks-per-Project bound's lock [Yello.Infrastructure/Persistence/TaskStore.cs:52]. Deferred to story 2.6, whose moves place keys without consulting the bound.
- [x] [Review][Defer] Column totals and the Task list are separate reads that can disagree [Yello.Infrastructure/Persistence/BoardReader.cs:30]. Deferred to story 2.9, which pages the Board.
- [x] [Review][Defer] The column count chip is a bare number to assistive technology [Yello.Client/Components/BoardColumn.razor:20]. Deferred to story 2.8, which owns the Board's accessible structure.
- [x] [Review][Defer] The cold-load skeleton is always three columns [Yello.Client/Components/BoardView.razor:48]. Deferred to story 6.1, which makes Status sets configurable.
- [x] [Review][Defer] "Board loaded." (AC15, `EXPERIENCE.md:279`) conflicts with `EXPERIENCE.md:390` "never on loading" [Yello.Client/Components/BoardView.razor:213]. Deferred to UX to reconcile.

- [x] [Review][Decision] Full-suite flake in `SpaceBoundaryPipelineTests`' in-process duration cases. 3 of 4 full runs failed `A_missing_Space_and_a_Space_without_Membership_take_the_same_time_to_refuse` and/or `A_Token_mismatch_and_a_non_member_take_the_same_time_to_refuse`; Slices alone was green. Neither arm passes through `NotAvailableFloor`, and the in-process path is unchanged by 2.2. **Lee chose (b):** the class runs alone in `BoundaryDurationCollection` (`DisableParallelization = true`), as `IsolationTimingTests` does, with the method, the 21 samples and the 20% tolerance unchanged; failures already quote both medians. Afterwards: Slices alone green, then two consecutive full runs of 749 total, 746 passed, 0 failed, 3 skipped [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs:57]

Dismissed (10):
- A Task of unknown Status is placed in the first column: unreachable, because of the composite FK and the first-Status create.
- Snapshot drift: `dotnet ef migrations has-pending-model-changes` reports none.
- The pre-save idempotency check can fail every keyed write: Task 8 specified this fail-closed behaviour, and the suite is green.
- Board-to-Board focus: no Board-to-Board link exists, and a keyed `BoardView` remounts.
- Null JSON members: the server never sends them.
- An empty effective set: every Space is seeded with three Statuses, and Epic 6 keeps at least one.
- Zero-width-only titles: Q3 (a) uses the trimmed-length rule, as Project names do.
- AC5's "+" in every column head: Q4 (b) decided this, and the UX ledger entry records it.
- The second "Task added.": already in the ledger, owned by 2.8.
- The Task 2 plant was "build red" but a test was built: the intent holds, because P8 was caught.

## Dev Notes

### The shape of the story in one paragraph

This story adds the second Space-scoped content entity and the product's primary surface. On the server it is small: a `Task` table under the Space policy with composite keys to its Project and Status, one create slice, one Board read, and two routes. The pipeline already resolves the Space, checks `manage-tasks`, replays keys and consults bounds. Three things are genuinely new and carry the risk.
- **The creation bound is per Project**, not per Space, so the pipeline must learn to scope a count by a route value without any slice counting.
- **The Board's first by-id read inside a Space** is where AD-3's timing promise is finally tested. 2.1 deferred that measurement here, and GATE-003 will fail the dev phase if the row is still pending.
- **The client's second Space-scoped page** must not copy SpaceHome's arrival logic, and must fix the re-arrival bug that drops a typed form.

Most of the remaining work is debt with this story's name on it: the idempotency capture assumptions, the refusal copy, the timing-dependent race case (P11), the skip link and two design-gate tightenings.

### Ledger entries owned by 2.2 — every one folded or stated

| Entry | Severity | Where it lands |
|---|---|---|
| `deferred-work.md:291` the in-Space timing arm, and Q13's unmeasured duration gap | medium | **Task 9**, AC10. Convert `ISO-TIMING-IN-SPACE`, measure, and decide Q7 |
| `:411` / GATE-003, which fails at `review` on a still-pending 2.2 row | (closed by 2.1; still binding) | The banner, Task 1, Task 9, Task 13 |
| `:338` the skip link past the context bar | low | **Task 10**, Q9. The Project name enters the bar here |
| `:460` client copy for the two idempotency refusals | low | **Task 11**, AC13 |
| `:465-470` idempotency capture assumptions (non-JSON body, `Results.Created`, non-`IResult`, any duplicate key) | medium | **Task 8**, AC12 |
| `:481` Space home drops an open form on re-arrival | low | **Task 10**, AC14 |
| 1.9's isolation rows pending until 2.2 | — | Only `ISO-TIMING-IN-SPACE` (`IsolationCases.cs:241-246`). No other row in `IsolationCases` or the revocation suite names 2.2 (grepped) |
| 2.1 review, P11 caught 2 of 3 (BND-002) | — | **Task 7**, AC11. In this story's area: same `BoundCounter`, same race, a new bound |

Epic-2-owned entries this story's surface makes due, stated so nobody reads them as forgotten:
- `:32` "exactly one pill": the column count chip is built here. **Task 10** tightens it.
- `:36` radius mapping: cards and columns (3px) are built here. **Task 10** adds those two clauses. Dialogs, chips and the detail panel stay with 2.3 and 2.4.
- `:66` device-pixel snapping is owned by **UX**, not by a dev story. Cards and columns are the first structural borders (2.1 avoided borders). Use the gated 1.5px floor and do not invent the snapping (Q10).
- `:20` 1.4.12 / 200% zoom measurement: **Task 10** either does it or re-owns it with a reason.
- `:271` Status order tiebreaker: **Task 2** adds `ThenBy(Id)` on read. The entry stays open for custom Statuses.

Explicitly **not** this story's, though nearby: `:471` (the bound lock reaching a neighbouring range: 2.9; the `SpaceId`-leading index narrows it for Tasks), `:477` (same key at limit−1 answered `409`: 2.9), `:459` (quiet-Space idempotency rows: 2.9), `:461` (Account-scoped writes: 3.1), and `:298` (the presence race: 2.9).

### Isolation rows (Task 9)

Actors as 1.9 built them: **A** and **B**, each with a Personal Space (`SA`, `SB`), a Session and a Token bound to their own Space. Seed through the product: `PA`/`TA` in `SA`, `PB`/`TB` in `SB`. The anchor 404 is the same route on a random Space id, or, for an in-Space id, a random Project id, on the same surface.

| Id | Test design | Vector | Request (by A, about B) | Session | ApiToken |
|---|---|---|---|---|---|
| ISO-DIRECT-BOARD | I-1 | DirectIdentifier | `GET /spaces/{SB}/projects/{PB}/board` | 404 = anchor, one `CrossSpace` row, no `PB`/`TB` name or id in the body | same |
| ISO-BOARD-AGGREGATE | I-3 ("includes Board column counts") | Aggregate | `GET /spaces/{SA}/projects/{PA}/board` after B created Tasks in `SB` | 200; each column's `Total` and Tasks are A's only | same |
| ISO-WRITE-TASK | I-2 | Write | `POST /spaces/{SB}/projects/{PB}/tasks` | 404 = anchor, `CrossSpace` row, no Task written in `SB` | same (a Token is admitted on this route: the boundary 404, not `401`) |
| ISO-BOARD-FOREIGN-ID-IN-OWN-SPACE | I-4, I-5 | DeepLink | `GET /spaces/{SA}/projects/{PB}/board` | 404 byte-identical to a random Project id in `SA`; **no** refusal row | same |
| ISO-WRITE-TASK-FOREIGN-PROJECT-IN-OWN-SPACE | I-4, I-5 | DeepLink | `POST /spaces/{SA}/projects/{PB}/tasks` | 404 byte-identical to a random id; no refusal row; nothing written in either Space | same |
| ISO-RECORD-KIND-TASK-VIEWER *(optional if Task 12 covers it on both surfaces)* | V-1 | RecordKind | a Viewer in `SA`: `POST …/tasks` | 403 `manage-tasks`, one `InsufficientRole` row | same |
| ISO-TIMING-IN-SPACE *(converted)* | I-6 | Timing | boundary `GET …/spaces/{SB}/projects/{PB}/board` vs in-Space `GET …/spaces/{SA}/projects/{random}/board` | indistinguishable (21 / median / 20%) | same |

I-13 applies to every refusal: no Space name, Project name or Task title in the body, and no id that was not already in the URL.

### Current state of the files this story changes — read each before editing

| File | Today | This story | Preserve |
|---|---|---|---|
| `Yello.Host/Spaces/ActiveSpaceResolution.cs` | Resolve → Token-bound check → matrix → presence bound → idempotent replay → `ConsultCreationBoundAsync` (Space-wide count) → endpoint → record → commit a materialised success (`ServeAsync`) | The creation bound is scoped by the declared bound's route value (`projectId` for `TasksPerProject`) | Order: replay **before** bound. 404 and 403 never convert. Refusal recorded on its own unit. Commit on `CancellationToken.None`. The S1200 split (`ActiveSpaceResolution.Idempotency.cs`, `IdempotentResponses`, `SpaceLog`) |
| `Yello.Application/Spaces/IBoundCounter.cs`, `Yello.Infrastructure/Persistence/BoundCounter.cs` | One bound (`ProjectsPerSpace`); any other throws | Add `TasksPerProject` with a Project scope | `UPDLOCK, HOLDLOCK`; no `Where SpaceId`; raw SQL only here (A-13) |
| `Yello.Host/Spaces/IdempotentResponses.cs`, `Yello.Infrastructure/Persistence/IdempotencyStore.cs` | Captures `IValueHttpResult` bodies only; `Location` from headers; any 2601/2627 → in-flight | Task 8 | Secret stripping fails closed; fingerprint = method + path + query + body hash; byte-identical first answer and replay |
| `Yello.Infrastructure/Persistence/Configurations/StatusDefinitionConfiguration.cs` | PK `Id`; unique `(SpaceId, Name)`; cascade from Space | Add the `(SpaceId, Id)` alternate key | The unique name index; seeding at registration |
| `Yello.Infrastructure/Persistence/StatusDefinitionReader.cs` | `OrderBy(Position)` | `.ThenBy(Id)` | No `Where SpaceId` |
| `Yello.Infrastructure/Persistence/SchemaNames.cs`, `YelloDbContext.cs` | `SpaceScopedTables` = Membership, StatusDefinition, SpacePresence, Project, IdempotencyRecord | Add `Task` | No `ApplyConfigurationsFromAssembly` |
| `Yello.Host/Program.cs` | Maps 14 routes through `api` | Map the two new routes | Filter order; `SpaceScopedRouteCheck` last before `RunAsync` |
| `Yello.Client/Pages/SpaceHome.razor` | Resets `_arrived` on every parameter set (the re-arrival bug) | Re-arrival keeps content; the shared arrival is extracted | Every behaviour in its header comment |
| `Yello.Client/Components/ProjectRow.razor` | The name as text | The name as a link to the Board | The rename control and its rules |
| `Yello.Client/Components/ContextBar.razor`, `Layout/MainLayout.razor` | Three tab stops; no skip link (the decision recorded in `MainLayout`'s comment) | The Project name; the skip control (Q9) | The accessible name rule; never a name not from this load's server answer |
| `Yello.Client/Refusals/RefusalCopy.cs`, `ClientCopy.resx` | Not-available for every unknown type | Two idempotency keys | Type decides, prose never; no branch on which not-found |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 21 rows; `ISO-TIMING-IN-SPACE` pending 2.2 | New rows; the timing row live | Every row states both surfaces; no case branches on surface |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs` | `SpaceScopedEntities` names `Yello.Domain.Tasks.Task` | The real name, and an existence assertion | Each gate finds its subject before asserting |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs` | Pill "at most one"; radius as a value set | "Exactly one"; the mapping for cards and columns | Gates scan the repository, not known files |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Bound.cs` | BND-002 via `Task.WhenAll` | Deterministic (Task 7) | The anchor; the replay-before-bound case |
| `tests/Yello.Tests.Client/FakeYelloApi.cs` | Project routes | Board and Task routes, bodies as `Yello.Contracts` types | `HoldAsync`/`Release` |

### Architecture constraints that bind here

- **AR-3**: authorisation, Space resolution, refusal recording, idempotency and bound checks live in the pipeline. "A slice that re-implements any of them is a defect."
- **AD-1 / AR-4**: the Role is read per request; no slice reads it; no `[Authorize(Roles=…)]` or `IsInRole` (`RoleApiBanTests`).
- **AD-2 / AR-5, AR-6, AR-8**: non-nullable `SpaceId` **on the Task itself** (AC8), RLS, an independent query filter, and composite FKs carrying `SpaceId`.
- **AD-3 / AR-9, AR-10**: `{spaceId}` first after the version. No bare `/tasks/{id}` route outside a Space. The in-Space not-found is identical to the boundary 404 in body **and duration**.
- **AD-4**: one pipeline, both surfaces.
- **AD-15**: one position key per Task, unique within `(ProjectId, StatusId)`, readable over the API and never writable. The `COLLATE Latin1_General_100_BIN2` declaration is at column definition (Q2).
- **AD-16 / AR-23**: the effective set is derived on read, cached only within a request, never stored.
- **AD-17 / AR-24**: no Task holds a Status absent from its Project's effective set.
- **AD-18 / AR-25**: `Idempotency-Key` on the create.
- **AD-20 / AR-11**: only `403`s and boundary `404`s are recorded.
- **AD-25 / AR-30**: the bound is declared once, checked by the pipeline and refused in-transaction.
- **AD-29**: the column total is a separate `COUNT` over the indexed predicate, never the number loaded. Keyset paging and ARIA set sizes are 2.9's.
- **AR-34**: sequential `Guid` via the generator port; `DateTimeOffset` UTC; RFC 9457 with a stable `type`.
- **UX-DR27**: absent, not disabled. **UX-DR31/33**: purge with the Space; no name in the title or URL; `autocomplete="off"`.
- **No description** on a Task in Epic 2 (`epics.md:437`; FR-31 needs the merge port's conformance suite first, AR-18).

### Testing requirements

Conventions (`tests/TESTING-CONVENTIONS.md`):
- Underscore-sentence names, one behaviour each.
- Traits `Suite`, `Priority` and `Requirement`, citing the FR/NFR **and** the AD (`FR-19`, `AD-25`).
- Randomised data.
- No `Task.Delay`: poll with a deadline, or use `HoldAsync`/`Release`.
- Cleanup by database disposal.
- Every absence is anchored on a presence and validated by a plant.
- **New rule from Task 7:** a concurrency case controls its interleaving with a lock and a wait on `sys.dm_exec_requests`, never by firing requests together and hoping.

Where each kind of test goes:
- **Slices:** `tests/Yello.Tests.Slices/{Tasks,Boards}/…` on the Slices process's one engine (`MigratedDatabaseFixture`). Add no `SqlServerContainerFixture` construction (`ContainerTopologyGateTests`).
- **Isolation:** rows in `IsolationCases.cs`; the generated theories run them.
- **Client:** `Yello.Tests.Client` against `FakeYelloApi`. Never Playwright's URL wait; poll `location.pathname` (`TESTING-CONVENTIONS.md:187-192`).
- **Seeding 5,000 Tasks:** one set-based `INSERT … SELECT` under the Space session key, not 5,000 requests. Valid, unique position keys are required if Q2 (a).

### Previous story intelligence (2.1, 1.9)

- **2.1's pipeline is the template.** Create a Project = `RequiresCapability` + `ConsultsBound` + body read inside the endpoint + `Location` set on the headers + `Results.Json(…, 201)`. Copy that shape (`ProjectEndpoints.cs`).
- **2.1 review lessons:**
  - The secret strip failed open on a name mismatch: fail closed everywhere.
  - A fresh key on every press duplicates after a lost answer: reuse the key for a resend.
  - Guard a second edit while one is in flight.
  - An unknown charset threw `InvalidOperationException`, which surfaced as a `500`: catch it.
  - Cold load needs a skeleton and an announcement.
  - Test-file headers went stale: rewrite them when activating.
- **2.1 variance:** `ISO-RECORD-KIND-INSUFFICIENT-ROLE` demotes a Viewer in its own Personal Space. For Tasks, prove the Viewer `403` with a Viewer added to an Owner's Space (Task 12).
- **2.1 Q11:** optimism was dropped from rename because a browser case saw the row change before the request was sent. Creation here is not optimistic either.
- **2.1 Q13 was not done:** the duration gap was never measured. Do it first in Task 9, before deciding Q7.
- **The plant run was stopped for low memory** after P6 in 2.1's dev phase. Run plants in small batches, and do not run the full suite concurrently with a plant.
- **1.9:** GATE-002 reads the real Host's route table in process, and a row must send a request on the route it names. The log check must see the requests that carry the secrets: create the Tasks inside a checked slice.
- **1.10:** migrations are expand/contract. The bundle-equivalence test compares predicates and indexes. Database unavailability is a uniform `503`: do not catch `SqlException` in new stores.

### Git intelligence

- `945d189` story 2.1 → `633e4f4` ATDD scaffolds → `d26be08` dev (63 files) → `bea4914` review (16 files). Commits are per phase, straight to `main`, by the orchestrator. Do not commit, push, branch or stash.
- The ATDD phase may run before dev and leave red-phase scaffolds. If it does, activate them one at a time, as 2.1 did, and record corrections rather than deleting scaffolds.

### Latest technical notes (pinned versions; no upgrades, no new packages)

- **.NET 10 / EF Core 10:**
  - `HasAlternateKey` on `StatusDefinition` emits `AddUniqueConstraint`, which is expand-only.
  - A composite FK: `HasOne<Project>().WithMany().HasForeignKey(t => new { t.SpaceId, t.ProjectId }).HasPrincipalKey(p => new { p.SpaceId, p.Id })`.
  - `UseCollation("Latin1_General_100_BIN2")` on the property sets the column collation in the migration.
  - `Database.SqlQuery<int>($"… WHERE ProjectId = {projectId}")` parameterises the interpolation.
- **SQL Server:**
  - Multiple cascade paths raise error 1785 (see Task 2).
  - `ADD BLOCK PREDICATE … AFTER UPDATE` refuses with 33504.
  - Lock waits in `sys.dm_exec_requests.wait_type` are `LCK_M_RS_U` (range-shared update) and `LCK_M_RIn_NL` (insert range), both under `LIKE 'LCK_M_%'`.
- **Blazor WebAssembly:** `@key` forces a remount, and the same component across a route-parameter change is reused (the re-arrival bug's mechanism). `NavigationManager.RegisterLocationChangingHandler` already drives the purge.
- No web research was required: every library is pinned (`Directory.Packages.props`, `PackageVersionPinTests`), and this story adds none.

### Project structure notes

- **New, most likely:**
  - `Yello.Domain/Tasks/{<Q1 name>,TaskTitle,ITaskStore}.cs`
  - `Yello.Domain/Boards/{IBoardReader}.cs`, and `EffectiveStatusSet` (Domain/Statuses)
  - `Yello.Application/Tasks/CreateTask/CreateTaskHandler.cs`
  - `Yello.Application/Boards/ViewBoard/ViewBoardHandler.cs`
  - `Yello.Infrastructure/Persistence/{TaskStore,BoardReader}.cs`, `Configurations/TaskConfiguration.cs`, and one migration
  - `Yello.Host/Endpoints/{TaskEndpoints,BoardEndpoints}.cs`
  - `Yello.Contracts/Tasks/{TaskRoutes,CreateTaskRequest,TaskResponse}.cs` and `Yello.Contracts/Boards/{BoardRoutes,BoardResponse,…}.cs`
  - `Yello.Client/Pages/Board.razor`, `Components/{BoardColumn,TaskCard,TaskCreate}.razor`, `Spaces/TaskCapabilities.cs`, and the shared arrival piece
  - `tests/Yello.Tests.Slices/{Tasks,Boards}/…` and `tests/Yello.Tests.Client/BoardTests.cs`
- **Modified:** as in the current-state table, plus `components.css`, `TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml` and this file.
- **Ring check (AD-21):** Domain references nothing. Ports the Host pipeline calls live in Application. No ASP.NET type in Application. The client cannot reference Domain (hence the gate-held predicates).
- **Variance to record:** Q4's create affordance on the first column only, against DESIGN.md:453.

### References

- `_bmad-output/planning-artifacts/epics.md`:
  - 112, 128 (FR-19, FR-28)
  - 191-222 (AR-3, AR-5…AR-11, AR-21…AR-25, AR-30, AR-34)
  - 246, 262-271, 279, 285 (UX-DR8, UX-DR21…27, UX-DR35, UX-DR41)
  - 355, 359, 362-363 (story mapping)
  - 431, 437, 775-785, 969-1057 (Epic 2, Story 2.2), 1059-1408 (the later stories' boundaries)
- `prds/prd-YelloBMAD-2026-08-15/prd.md`: 67, 70, 100-101, 332-359, 374, 391-398, 516-534, 605, 716-757, 803-811, 886-904. `addendum.md`: 140, 142, 153, 165.
- `architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`: 77-100, 162-186, 224-228, 249-277, 381-415, 425-429.
- `ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md`: 57-90, 117-161, 204-209, 365-376, 389-454, 482. `EXPERIENCE.md`: 48-55, 85, 99-107, 164-179, 199-201, 251, 276-279, 328-337, 390, 411-419, 472-477. Mocks: `mockups/board.html` 104-112, 162; `mockups/registration.html` 147-158 (the spine wins on conflict).
- `specs/spec-yello/SPEC.md`: 110-112. `acceptance-criteria.md`: 192-196, 291-303. `role-capability-matrix.md`: 5, 11, 13.
- `test-artifacts/test-design-qa.md`: 262-282 (I-1…I-13), 303-305 (A-5…A-7), 313-314 (P-5, P-10), 319 (S-2), 367 (X-9), 493. `test-design-architecture.md`: 424-439 (R2 timing), 441-455 (R3), 506-519 (R7, owner 2.6).
- `implementation-readiness-report-2026-08-22.md`: 498-508, 601, 996 (Task entity → 2.2), 1210, 1240-1258.
- `sprint-change-proposal-2026-09-13.md`: nothing affecting Epic 2 (line 93).
- `tests/TESTING-CONVENTIONS.md`: the whole file; especially 86-118, 172-192, 264-318, 483-543.
- `implementation-artifacts/deferred-work.md`: the entries in the ledger table above. `2-1-create-a-project-and-see-what-is-in-your-space.md`: Tasks 5-7, Review Findings, Debug Log (plants P1-P11, R1-R8), Q1-Q13. `1-9-prove-isolation-on-both-surfaces.md`: case list and timing method.

### Open questions — each with a recommended decision

The dev phase takes these as **reversible, recorded decisions** in the Completion Notes. None blocks starting.

1. **The Task entity's CLR name.** `Task` in `Yello.Domain.Tasks` collides with `System.Threading.Tasks.Task` under implicit usings (CS0104 wherever both namespaces are imported, which is every store, handler and endpoint).
   - (a) Name it `Task` and alias at every use site (`using DomainTask = Yello.Domain.Tasks.Task;`). This is Glossary-exact, but the alias spreads to every file, and an `async Task<Task>` signature is a hazard for each reader.
   - (b) Name the type `WorkTask` (or `TaskItem`), keep the **table** `Task`, the namespace `Yello.Domain.Tasks`, the routes and all copy as "Task", and update `SpaceScopedEntities` to the real name.

   **Recommend (b), `TaskItem`.** The Glossary governs product language and the schema (`SchemaNames` remarks), not CLR identifiers. Record it as a variance.
2. **The position key column (AD-15), now or in 2.6.** The explicit ACs (`epics.md:1188-1191`) and R7 put the BIN2 *assertion* in 2.6. Nothing assigns the column.
   - (a) 2.2 declares `PositionKey varchar(…) COLLATE Latin1_General_100_BIN2 NOT NULL`, with the unique `(SpaceId, ProjectId, StatusId, PositionKey)` index in the same collation. Creation appends a key strictly after the column's current last key, using a small Domain function (for example `PositionKey.After(string? last)` over a base-62 alphabet ordered by ordinal). The bound lock serialises creations per Project, so two can never compute the same key. 2.6 replaces the generator with the jittered midpoint and adds the BIN2 schema assertion and the mixed-case case.
   - (b) No column. The Board orders by `CreatedAt, Id`, and 2.6 adds a column to a populated table with a backfill: an expand step, a nullable phase and a contract step, on a table that may then hold 5,000 rows per Project.

   **Recommend (a).** AR-21 says the collation is "set in the migration" and "cannot be retrofitted". (a) avoids a three-step migration later and makes FR-29's "survives reload" true from the first Task. Add a minimal schema check that the column's collation is BIN2 now (it costs one query), and leave the full A-6/R7 assertion to 2.6.
3. **Task title rules.** (a) Trimmed, 1–200 characters, not unique, `invalid-task-title`. (b) 1–100 like Projects. Nothing in the PRD states a length. Titles are the only content on a card and run longer than names. **Recommend (a).**
4. **Where creation lives, and what Status a new Task takes.** DESIGN.md:453 and the board mock put a "+" in **every** column header. AC2 (verbatim, `status: final`) and FR-19 say a new Task "takes the first Status", and UJ-1 says "adds a Task to Todo".
   - (a) A "+" in every column that creates into **that** column. This contradicts AC2.
   - (b) A "+" only in the **first** column's header, plus the empty state's "Add a Task". The request carries no Status, so AC2 holds by construction. The other columns' create slot is empty for every Role, which is not a Role-disabled state. Record the DESIGN.md:453 variance and raise it to UX.
   - (c) A "+" in every column, each creating into the first column. This is misleading.

   **Recommend (b).** The final AC governs, and a creation into a later Status is a move, which is 2.6's.
5. **Routes.**
   - (a) `POST /spaces/{s}/projects/{p}/tasks` to create, `Location` `/spaces/{s}/tasks/{t}`, and `GET /spaces/{s}/projects/{p}/board` to read.
   - (b) `GET /spaces/{s}/projects/{p}/tasks` as the Board read (a flat list grouped by the client). This loses the per-Status `Total` contract and makes the client derive counts, which AC5 forbids.
   - (c) Per-column endpoints (`…/board/columns/{statusId}`). This is 2.9's paging shape, earlier than needed.

   **Recommend (a).** A Task's own URL excludes the Project because FR-41 moves Tasks between Projects. Each new route is an isolation row on both surfaces (GATE-002).
6. **How many Tasks the Board read returns per column.**
   - (a) All of them, ordered, with `Total` from the separate count. At 5,000 the payload is large, and 2.9 measures it and introduces the keyset seek (append, never recycle).
   - (b) The first 50 per column (AD-29's assumed page size), with no way to load more until 2.9. A column holding 51 would then hide a Task, against AC4.

   **Recommend (a).** Make `Total` independent of `Tasks` in the contract and in a test. Use a handler-level case where the reader returns fewer rows than the count, or assert `Total` against SQL while the plant sets it from `Tasks.Count`. 2.9 then changes only how many rows come back.
7. **Equalising the in-Space not-found with the boundary 404 (AC10).** Measure first (Task 9). If within 20%, convert the row and stop. If not:
   - (a) A constant **duration floor** on every `not-available` answer from a Space-scoped route, applied in one place (the pipeline, around `BoundaryProblems.NotAvailable()`), set above both paths' p95. This costs latency on refusals only, never on a success, and no AD changes.
   - (b) Make the in-Space path do equivalent work, for example a dummy write of the same shape. This is fragile, and it writes something AD-20 says is not recorded.
   - (c) Record in-Space not-founds as a third `AccessRefusal` kind. This amends AD-20 and is a product decision.

   **Recommend (a) if needed**, with the floor configurable and its value logged at startup. Test it with the timing row plus the permanent oracle (the unmatched fallback must still be separable, or the floor has swallowed the oracle: exempt the fallback, or keep it outside the floor).
8. **The creation form.**
   - (a) Inline at the top of the first column, opened by the "+" or the empty state's action. One field, "Add" and "Cancel", `Esc` cancels, and the field stays open after a success for rapid entry. Focus returns to the "+" on cancel.
   - (b) A dialog. This is heavier, and brings the 6px dialog radius and focus-trap rules into scope.

   **Recommend (a).** UJ-1 is "adds a Task and closes the tab".
9. **Skip link and the Project name in the context bar** (`deferred-work.md:338`).
   - (a) Add both now. The bar shows the Project name on the Board (≥1280px; dropped at 768–1279px per `EXPERIENCE.md:413-419`). A visually hidden, focus-revealed **button** "Skip to content", the first tab stop on every authenticated surface, moves focus to the page's heading or `<main>` (`tabindex="-1"`). It is not a `#main` link, which would navigate the router to `/#main` (`MainLayout.razor` comment).
   - (b) Re-own both to 2.11, whose ACs state the bar's breakpoint behaviour. The bar then holds three tab stops for another eight stories.

   **Recommend (a).** Gate it with a browser case: Tab once and press Enter, and focus is on the page heading, on Space home and on the Board. 2.11 later asserts the breakpoints as ACs.
10. **Structural borders on cards and columns** (`deferred-work.md:66`). The device-pixel snapping is owned by UX and has no specification.
    - (a) Use `--hairline-width` (1.5px, gated), state in the Completion Notes that snapping remains with UX, and leave the entry open.
    - (b) Implement snapping. Lee reverted exactly that on 2026-08-28.

    **Recommend (a).**
11. **The Viewer Board's "drag handle and Move control absent" (AC3) before either exists.**
    - (a) Assert the create affordance's absence now, and write the Viewer case as the one 2.6 and 2.7 extend.
    - (b) Build placeholder Move controls for Members now. This builds 2.6's control early, without its picker.

    **Recommend (a).** State in the Completion Notes that the drag and Move halves of AC3 are vacuous until 2.6 and 2.7, and that those stories own extending the case.

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-2-2-add-tasks-and-see-them-on-a-board.md` (scenario map, the open-question dependencies, seed constraints, and what was deliberately not scaffolded). Every scaffold is written against the recommended option of Q1-Q11; the checklist's dependency table says what to correct if a decision differs.

| File | Cases | ACs | State |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.cs` | 8 (5 entries) | AC2, AC3, AC9 | skipped |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Bound.cs` | 5 | AC7, AC11 | skipped |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Idempotency.cs` | 6 (3 entries) | AC2 (Task 6) | skipped |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Schema.cs` | 6 | AC2, AC8 | skipped |
| `tests/Yello.Tests.Slices/Tasks/TaskWire.cs` | harness | - | active |
| `tests/Yello.Tests.Slices/Boards/BoardEndpointTests.cs` | 6 | AC1, AC3, AC4, AC5, AC9 | skipped |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Bound.cs` | 1 rewritten (2.1's BND-002) | AC11 | **active, green** |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 6 rows added, ISO-TIMING-IN-SPACE reshaped | AC9, AC10 | pending on 2.2 |
| `tests/Yello.Tests.Isolation/Expectation.cs` | `IndistinguishableFromInSpaceNotFound` | AC10 | used by the pending timing row |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Tasks.cs` | 12 | AC2, AC3, AC5, AC8, AC9, AC11, AC12 | 8 skipped; **4 active, green** (the `Results.Created`/`Accepted` ban and the `IResult` rule, each with a permanent oracle) |
| `tests/Yello.Tests.Architecture/RefusalCopyTests.Tasks.cs` | 3 | AC7, AC13 | 2 skipped; **1 active, green** (the Tasks-per-Project bound copy) |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.Board.cs` | 3 | AC5, AC6 | skipped |
| `tests/Yello.Tests.Client/BoardTests.cs` | 16 (14 entries) | AC1-AC7, AC13-AC15 | skipped |
| `tests/Yello.Tests.Shared/TaskRouteScaffold.cs`, `ProductActor.cs`, `WireDatabase.cs`; `tests/Yello.Tests.Client/FakeYelloApi.cs` | harness | - | active; `TaskRouteScaffold` is deleted on activation |

**Green today, standing gates:** the rewritten 2.1 BND-002 (P11 planted, caught 3 of 3, after both Accounts are made present in the Space before the race: a first request's `SpacePresence` insert serialised the pair before the bound count, which is why P11 was caught only 2 of 3 at the 2.1 review); ARCH-009 (no `Results.Created`/`Accepted*` in the Host) and its oracle; ARCH-010 (every Space-scoped state-changing handler returns `IResult`) and its oracle; COPY-003.

**Activation order** (the checklist has the detail):
1. **ISO-TIMING-IN-SPACE first.** It tests the riskiest assumption: that the boundary `404`, which writes an `AccessRefusal`, and the in-Space not-found, which writes nothing, cannot be told apart by duration. Build only what it needs (the table, the create, the Board read, the two routes). Then measure, three runs, record both medians per surface, and decide Q7 before building further.
2. Schema: SCH-002, SCH-003, SCH-004, SCH-001 (widen `SpaceIsolationSchemaTests` for `Task`), SCH-005, ARCH-001, ARCH-002.
3. Create and Board: SLC-001, -002, -003, -005, BRD-001 to -004, BRD-006, SCH-006, ARCH-003 to -006. Mapping the routes turns 2.1's `No_route_moves_a_Project` red; narrow it in the same change.
4. Isolation rows, one at a time, then SLC-004 and BRD-005.
5. Bound: BND-001, -002, -003, ARCH-007, BND-004 (P11 three times each for Tasks and Projects), BND-005.
6. Idempotency: IDM-001 to -003.
7. Copy: COPY-001, -002.
8. Client: E2E-001 to -014 with ARCH-008, then DSN-001 to -003.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), dev-story run unattended by the story-cycle orchestrator.

### Debug Log References

**Create-story phase (2026-10-25).**
- `dotnet build Yello.slnx` before the status change: 0 warnings, 0 errors.
- After setting `2-2-add-tasks-and-see-them-on-a-board: ready-for-dev`: `dotnet build Yello.slnx` gave 0 warnings and 0 errors. `dotnet test --project tests/Yello.Tests.Isolation` gave **57 total, 57 passed, 0 failed, 0 skipped** (33 s).
- GATE-003 permits a pending row while its story is `ready-for-dev`. `ISO-TIMING-IN-SPACE` is the only row pending on 2.2.

**Dev phase (2026-10-25).**

*Task 1.* `dotnet build Yello.slnx`: 0 warnings, 0 errors. `IsolationCaseListTests` with 2.2 `in-progress`: **14/14 green**. Not one pending row but **seven**: the ATDD run had added six more (ISO-DIRECT-BOARD, ISO-BOARD-AGGREGATE, ISO-WRITE-TASK, ISO-BOARD-FOREIGN-ID-IN-OWN-SPACE, ISO-WRITE-TASK-FOREIGN-PROJECT-IN-OWN-SPACE, ISO-RECORD-KIND-TASK-VIEWER) beside ISO-TIMING-IN-SPACE. All seven are live; no row names 2.2 as pending.

*Activation order.* Followed the ATDD checklist's order. Per its step 1, the minimum server path the timing row needs (Task 2's table and policy, Task 4's create and Board read, the two routes) was built before any scaffold was activated; the slice, schema, bound and idempotency scaffolds therefore went green on their first activation, and the red evidence for them is the ATDD phase's skip reasons plus the product plants below, each of which turned the activated case red.

*ISO-TIMING-IN-SPACE, measured first (AC10, Q7).* Timing class run alone, three times, through `DurationIndistinguishability` (21 samples, median, 20%) at the Host's request-finished line:

| Run | Session boundary vs in-Space | Token boundary vs in-Space | Oracle S / T |
|---|---|---|---|
| 1 | 9.1 vs 8.1 ms (10.6%) | 9.8 vs 8.6 ms (11.8%) | 77.6% / 97.3% |
| 2 | 8.4 vs 6.8 ms (18.9%) | 8.3 vs 7.0 ms (15.2%) | 76.6% / 97.4% |
| 3 | 11.6 vs 8.3 ms (**28.8%, failed**) | 9.4 vs 8.0 ms (14.0%) | 79.3% / 97.7% |

A consistent 1-3 ms gap (the `AccessRefusal` write), failing one run in three, so Q7 (a) was applied: `NotAvailableFloor`, a filter on the API group immediately before Space resolution, holds every `404` a Space-scoped route answers to `SpaceBoundary:NotAvailableFloorMilliseconds` (50 ms default, logged at startup). Re-measured, three runs: Session 61.7 vs 61.0 (1.2%), 61.7 vs 61.3 (0.7%), 61.1 vs 61.4 (0.6%); Token 61.3 vs 61.4 (0.2%), 61.2 vs 61.2 (0.1%), 61.1 vs 60.9 (0.4%). Oracle (unmatched fallback, outside the floor): 93-99.7%. ISO-TIMING-BOUNDARY and ISO-TIMING-TOKEN-ELSEWHERE stay within 1.1%.

*Scaffold corrections (none deleted).*
- `PersistenceModelGateTests.Every_entity_configuration_reaches_the_model` assumed table name == CLR name; Q1 (b) maps `TaskItem` to `Task`. Added a one-entry `GlossaryTableNames` map; convention would map it to `Tasks`, so an unapplied configuration is still caught.
- 2.1's `No_route_moves_a_Project` matched by prefix and saw `POST .../tasks` and `GET .../board`; narrowed to the collection and the item exactly (the ATDD checklist predicted this). ARCH-006 holds what lies under the item.
- `DesignFoundationGateTests.Board.cs` DSN-001 merged into the existing pill gate, renamed `The_fully_round_radius_is_declared_once_and_used_by_exactly_one_component_the_column_count_chip`.
- `BoardTests` E2E-001 seeded no Board, so the fake answered `404`; added `Seed(..., [])`.
- `BoardTests` E2E-013: `blur()` leaves Chromium's sequential-focus starting point on the heading the arrival focused, so Tab went to the next control after it; the case now resets focus to the document body first.
- `TaskRouteScaffold` replaced by `TaskRoutes`, `BoardRoutes`, `ProblemTypes.InvalidTaskTitle`, `TaskTitle.MaxLength`, `BoundRegistry`, `StableCode`, and deleted; the Client suite keeps a local `ClientBoard` path (it cannot reference the client) and literal `tasks-per-project` / 5000 in the fake, as 2.1's fake does for Projects.
- Stale "red-phase" headers rewritten in the activated files.

*Found while building.* `S4041` refused a migration class named `Tasks` (it shadows `System.Threading.Tasks`): the migration is `20261025115412_TaskTable`. `S1200`: the creation-bound step moved to a nested class in `ActiveSpaceResolution.Bound.cs`, so every gate that asks who reads the bound port still sees the pipeline. Blazor does not re-set a page's parameters on a navigation to the same address (every parameter is an unchanged `Guid`), so AC14's Role re-read is driven by `NavigationManager.LocationChanged` in `SpaceArrival`.

*Plants (one at a time, single test project, originals held in memory and restored in a `finally`, SHA-256 compared after each; never git checkout/restore/reset/stash).*

| # | Plant | Gate | Caught |
|---|---|---|---|
| P1 | `ISO-TIMING-IN-SPACE` pending with 2.2 at `review` | GATE-003 `Every_pending_row_names_a_story_that_has_not_yet_reached_review` | yes |
| P2 | No row names `POST .../tasks` | GATE-002 `Every_api_route_the_Host_maps_..._is_named_by_a_case` | yes |
| P3 | Task query filter + `Task` FILTER predicate dropped (as specified) | ISO-BOARD-AGGREGATE | **no - nothing leaks**: every 2.2 read is narrowed by `ProjectId`, and the Project resolves only through its own two layers (ledger entry, owner 2.3) |
| P3' | Board read `IgnoreQueryFilters()` + `Project` and `Task` FILTER predicates dropped | ISO-BOARD-FOREIGN-ID-IN-OWN-SPACE, Session and ApiToken | yes |
| P3a/b | each layer of P3' alone | IsolationCaseTests (46) | green, as anchors should be |
| P4 | `CreateTaskHandler` injects `IBoundCounter` and counts | `Only_the_pipeline_depends_on_the_bound_count_port` | yes |
| P11 | `UPDLOCK, HOLDLOCK` removed from both counts, 3 runs | Projects BND-002 and Tasks BND-004 | **6 of 6** |
| P6 | `Results.Created` in the Task endpoint | ARCH-009 | yes |
| P7 | `TaskStore` leaves its insert unsaved | TaskEndpointTests (keyed replay answered `500`, not `409`; every create) | yes |
| P8 | `SpaceScopedEntities` names `TaskItme` | ARCH-001 | yes |
| P9 | `TaskCapabilities` offers Viewer | ARCH-008 and E2E-003 (browser) | yes, both |
| P10 | Board name written to `sessionStorage` | `No_client_code_reaches_browser_storage` | yes |
| P11b | `<PageTitle>` with the Project name | `No_client_code_sets_the_document_title` | yes |
| P12 | Shadow on `.task-card` | `No_surface_carries_a_shadow`, DSN-003 | yes |
| P13 | Second `var(--radius-full)` consumer | the exactly-one pill gate | yes |
| P14 | `AFTER UPDATE` predicate removed for `Task` | SCH-001, SCH-002 (33504), `Every_Space_scoped_table_carries_a_filter_and_a_block_predicate` | yes |
| P15 | Column `Total` from the placed Tasks | `ViewBoardHandlerTests.A_columns_total_is_the_count_not_the_number_of_Tasks_placed` | yes |
| P16 | 2px Task card | DSN-002 | yes |
| P17 | Card title `overflow: hidden; white-space: nowrap` | `TextSpacingTests` (spacing and 200% zoom) | yes |

Not planted separately: the floor removed (the pre-floor measurement above is that run - caught 1 of 3, which is why the floor exists).

*Runs.* `dotnet build Yello.slnx`: 0 warnings, 0 errors. Projects alone along the way: Architecture 233/233; Isolation 71/71; Client 70/70 (+4 text-spacing); Slices Task+Board classes 31/31, handler-level 28/28. **Full `dotnet test Yello.slnx` before `review`: 743 total, 740 passed, 0 failed, 3 skipped** (`YELLO_MIGRATION_BUNDLE` unset), 2 m 54 s wall clock. Against the post-ATDD 690 total / 55 skipped: +53 cases, 52 skips activated. **After setting `review` in `sprint-status.yaml`: `dotnet build Yello.slnx` 0 warnings, 0 errors; `dotnet test Yello.slnx` 743 total, 740 passed, 0 failed, 3 skipped** (2 m 51 s) - GATE-003 green, no row pending on 2.2.

### Completion Notes List

- Implemented FR-19 (create a Task) and FR-28 (the Board, without 2.9's 5,000-Task performance half) end to end: `Task` table under the Space policy with three predicates and composite keys carrying `SpaceId`; `POST /api/v1/spaces/{s}/projects/{p}/tasks` and `GET .../projects/{p}/board` on both surfaces; the Tasks-per-Project bound counted by the pipeline within the route's Project; the Board page with a shared Space arrival, inline creation, the skip control and the Project in the context bar. AC1-AC15 each have activated cases (slices, schema, isolation, architecture, browser).
- **Q1 - (b), CLR type `TaskItem`.** Table, namespace, routes and copy stay "Task". Cost to later stories: none beyond reading `TaskItem` in code; reversible by a rename plus the one-line `GlossaryTableNames` entry.
- **Q2 - (a), `PositionKey varchar(128) COLLATE Latin1_General_100_BIN2` now**, unique with `(SpaceId, ProjectId, StatusId)`, appended by `PositionKey.After` (base-62 carry increment, first key `V0000`), serialised by the bound's lock. Cost to later stories: 2.6 replaces the generator with the midpoint and adds the full BIN2 assertion and mixed-case case; no backfill needed.
- **Q3 - (a), titles trimmed, 1-200, not unique, `invalid-task-title`.** Cost: none.
- **Q4 - (b), "+" in the first column only, plus the empty state's "Add a Task"**; on an empty Board only the empty-state action shows, so AC1's "one primary action" holds. Variance from DESIGN.md:453 raised to UX in the ledger. Cost: 2.6 owns a creation into a later Status, if UX wants one.
- **Q5 - (a), the three routes.** A Task's own URL omits the Project (FR-41). Cost: none.
- **Q6 - (a), every Task returned, `Total` from a separate grouped `COUNT`.** Cost: 2.9 pages; a handler case already proves `Total` is not `Tasks.Count`.
- **Q7 - (a), measured gap 10.6-28.8% (Session) / 11.8-15.2% (Token), one run in three failing, so a 50 ms not-found floor on Space-scoped routes**, configurable and logged; oracle outside it. Cost to later stories: every Space-scoped `404` takes at least the floor; 2.9 re-measures the floor on the deploy target (ledger). Reversible by setting the key to 0.
- **Q8 - (a), inline field at the top of the first column**, stays open and empty after each Task, Esc/Cancel return focus to the opener. Cost: none.
- **Q9 - (a), "Skip to content" button, first tab stop on every authenticated surface**, focuses the page heading (or `<main>`); Project name in the bar, dropped at 768-1279px. Cost: 2.11 asserts the breakpoints as ACs.
- **Q10 - (a), structural borders at `--border-hairline-width`; device-pixel snapping remains with UX** (`deferred-work.md:66` open).
- **Q11 - (a), AC3's drag-handle and Move-control halves are vacuous until 2.7 and 2.6**, which extend E2E-003 (it already asserts the create affordance's absence for a Viewer and presence for the Owner).
- Deferred ledger: closed `:291`, `:338`, `:460`, `:465`, `:481`, `:32`; updated `:20` (done: `TextSpacingTests`), `:36`, `:66`, `:271`, `:471`; added four entries (owners 2.3, 2.9, 2.8, UX).

### File List
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/TESTING-CONVENTIONS.md`
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.Board.cs`
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs`
- `tests/Yello.Tests.Architecture/PersistenceModelGateTests.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Projects.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Tasks.cs`
- `tests/Yello.Tests.Architecture/RefusalCopyTests.Tasks.cs`
- `tests/Yello.Tests.Client/BoardTests.cs`
- `tests/Yello.Tests.Client/FakeYelloApi.cs`
- `tests/Yello.Tests.Client/TextSpacingTests.cs` (new)
- `tests/Yello.Tests.Isolation/IsolationCases.cs`
- `tests/Yello.Tests.Shared/TaskRouteScaffold.cs` (deleted)
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/SpaceIsolationSchemaTests.cs`
- `tests/Yello.Tests.Slices/Boards/BoardEndpointTests.cs`
- `tests/Yello.Tests.Slices/Boards/ViewBoard/ViewBoardHandlerTests.cs` (new)
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/IdempotencyRecordCaptureTests.cs` (new)
- `tests/Yello.Tests.Slices/Tasks/CreateTask/CreateTaskHandlerTests.cs` (new)
- `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Bound.cs`
- `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.cs`
- `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Idempotency.cs`
- `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Schema.cs`
- `tests/Yello.Tests.Slices/Tasks/TaskWire.cs`
- `Yello.Application/Boards/ViewBoard/ViewBoardHandler.cs` (new)
- `Yello.Application/Spaces/IBoundCounter.cs`
- `Yello.Application/Tasks/CreateTask/CreateTaskHandler.cs` (new)
- `Yello.Client/Components/BoardColumn.razor` (new)
- `Yello.Client/Components/BoardView.razor` (new)
- `Yello.Client/Components/ContextBar.razor`
- `Yello.Client/Components/PageHeading.razor` (new)
- `Yello.Client/Components/ProjectList.razor`
- `Yello.Client/Components/ProjectRow.razor`
- `Yello.Client/Components/SpaceArrival.razor` (new)
- `Yello.Client/Components/TaskCard.razor` (new)
- `Yello.Client/Components/TaskCreate.razor` (new)
- `Yello.Client/Layout/MainLayout.razor`
- `Yello.Client/Layout/PageHeadingFocus.cs` (new)
- `Yello.Client/Localisation/ClientCopy.resx`
- `Yello.Client/Pages/Board.razor` (new)
- `Yello.Client/Pages/ClientRoutes.cs`
- `Yello.Client/Pages/SpaceHome.razor`
- `Yello.Client/Program.cs`
- `Yello.Client/Refusals/RefusalCopy.cs`
- `Yello.Client/Refusals/RefusalText.cs` (new)
- `Yello.Client/Spaces/ActiveSpaceState.cs`
- `Yello.Client/Spaces/SpaceArrivalContext.cs` (new)
- `Yello.Client/Spaces/SpaceWrites.cs` (new)
- `Yello.Client/Spaces/TaskCapabilities.cs` (new)
- `Yello.Client/wwwroot/css/components.css`
- `Yello.Contracts/Boards/BoardResponse.cs` (new)
- `Yello.Contracts/Boards/BoardRoutes.cs` (new)
- `Yello.Contracts/ProblemTypes.cs`
- `Yello.Contracts/Tasks/CreateTaskRequest.cs` (new)
- `Yello.Contracts/Tasks/TaskResponse.cs` (new)
- `Yello.Contracts/Tasks/TaskRoutes.cs` (new)
- `Yello.Domain/Boards/IBoardReader.cs` (new)
- `Yello.Domain/Statuses/EffectiveStatusSet.cs` (new)
- `Yello.Domain/Statuses/IStatusDefinitionReader.cs`
- `Yello.Domain/Tasks/ITaskStore.cs` (new)
- `Yello.Domain/Tasks/PositionKey.cs` (new)
- `Yello.Domain/Tasks/TaskItem.cs` (new)
- `Yello.Domain/Tasks/TaskTitle.cs` (new)
- `Yello.Host/ApiRouteGroup.cs`
- `Yello.Host/Endpoints/BoardEndpoints.cs` (new)
- `Yello.Host/Endpoints/ProjectEndpoints.cs`
- `Yello.Host/Endpoints/TaskEndpoints.cs` (new)
- `Yello.Host/Program.cs`
- `Yello.Host/SpaceLog.cs`
- `Yello.Host/Spaces/ActiveSpaceResolution.Bound.cs` (new)
- `Yello.Host/Spaces/ActiveSpaceResolution.cs`
- `Yello.Host/Spaces/IdempotentResponses.cs`
- `Yello.Host/Spaces/NotAvailableFloor.cs` (new)
- `Yello.Host/Spaces/RequiredBound.cs`
- `Yello.Host/Spaces/SpaceScopedRouteCheck.cs`
- `Yello.Infrastructure/Persistence/BoardReader.cs` (new)
- `Yello.Infrastructure/Persistence/BoundCounter.cs`
- `Yello.Infrastructure/Persistence/Configurations/StatusDefinitionConfiguration.cs`
- `Yello.Infrastructure/Persistence/Configurations/TaskConfiguration.cs` (new)
- `Yello.Infrastructure/Persistence/IdempotencyStore.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261025115412_TaskTable.cs` (new)
- `Yello.Infrastructure/Persistence/Migrations/20261025115412_TaskTable.Designer.cs` (new)
- `Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs`
- `Yello.Infrastructure/Persistence/SchemaNames.cs`
- `Yello.Infrastructure/Persistence/StatusDefinitionReader.cs`
- `Yello.Infrastructure/Persistence/TaskStore.cs` (new)
- `Yello.Infrastructure/Persistence/YelloDbContext.cs`
- `Yello.Infrastructure/SpaceContentStores.cs`
- `_bmad-output/implementation-artifacts/2-2-add-tasks-and-see-them-on-a-board.md`

## Change Log

- 2026-10-25 - Dev phase: Task table, create and Board routes on both surfaces, the Tasks-per-Project bound in the pipeline, the not-found floor (Q7), idempotency capture gates, the Board page with shared arrival, skip control and Project in the context bar; seven isolation rows live; ledger and conventions updated. Status to review.
- 2026-10-25 - Code review: 31 raw findings, 27 unique; 12 patches applied (AC12's non-value body refused at capture, AC14's re-arrival keeps the page on a failed Role re-read and has its Role case, timeouts no longer read as purges, Task field focus, live regions, typed text and key retention, stale heading focus, floor cap, per-column aggregate assertion, a misplaced doc comment); 5 deferred (2.6, 2.9, 2.8, 6.1, UX); 10 dismissed. Status to done.
- 2026-10-25 - Review follow-up: status back to in-progress. 3 of 4 full runs failed `SpaceBoundaryPipelineTests`' in-process duration cases (Slices alone green), and the reproduction was killed for low memory. The open item is in `deferred-work.md` (owner 2.2).
- 2026-10-25 - Review follow-up, Lee's decision (b): `SpaceBoundaryPipelineTests` serialised in a `DisableParallelization` collection; Slices alone and two consecutive full runs green (749 / 746 passed / 0 failed / 3 skipped). Status to done.
