---
baseline_commit: 4969754
---

# Story 2.4: Define and apply Labels

Status: ready-for-dev

Epic: 2 — Track your own work on a Board
Story key: `2-4-define-and-apply-labels`
Requirements owned (Story Coverage Index, `epics.md:357`): **FR-22** (Owner/Admin define Labels per Space, Owner/Admin/Member apply them, a Label in use cannot be deleted; `epics.md:115`, `prd.md` FR-22, SPEC `acceptance-criteria.md#CAP-22`), **UX-DR8** (the card's metadata row: at most three Label chips then `+N`, wrapping before it scrolls; `epics.md:246`), **UX-DR13** (a constrained palette satisfying the fill rules by construction; `epics.md:251`, `DESIGN.md:457-462`).
Also owned here, by hand-over: **FR-20's Labels clause** ("change a Task's … Labels"; 2.3 left it to 2.4, `2-3-…md:11`, `epics.md:113`). The coverage row omits it; this story closes it.
Also binding: **AD-2 / AR-5, AR-6, AR-8** (non-nullable `SpaceId`, three RLS predicates, an independent query filter, composite FKs carrying `SpaceId`), **AD-3 / AR-9** (`{spaceId}` on every Label route; the AR-9 gate already names `Yello.Domain.Labels.Label`), **AR-10 / AR-11 / AD-20** (404 = absent, 403 recorded, nothing else recorded), **AD-18 / AR-25** (`Idempotency-Key`, pipeline-only), **AR-30** (no bound is added; see Q6), **AR-34** (sequential `Guid`, RFC 9457 stable `type`, mutation only through a slice, invariants in Domain), **AD-19 / FR-37** (adding a response field is legal in v1), **UX-DR5** (2px Label chips), **UX-DR7** (contrast is a computed release gate), **UX-DR11** (the picker, Label use; never a default selection where consequential), **UX-DR12 / UX-DR36** (destructive confirm replaces in place), **UX-DR26 / UX-DR27** (banned list; absent, not disabled), **UX-DR31** (purge "Assignee and Label lists"), **UX-DR32 / UX-DR33 / UX-DR37** (refusal surfaces, browser disclosure, copy), **UX-DR40** (text spacing, 200% text-only zoom, chips size to content), **UX-DR41** (320px reflow), **NFR-9** (the Board and the Task editor are WCAG 2.1 AA flows), and the 1.9 isolation gates (**AR-35, AD-4**) for every route this story adds.
Capabilities in the SPEC: **CAP-20** (Labels clause), **CAP-22** (`specs/spec-yello/SPEC.md:114, 120-121`; `role-capability-matrix.md:18` "Manage Labels ✔ ✔ — —").
Test design: **I-1, I-2, I-3, I-11, A-4, A-5, C-6, V-1, P-10, X-1** (no Label-specific IDs exist; Label is named inside these generic rows, `test-design-qa.md`, `test-design-progress.md:529-603`).
Ledger entries owned by this story (`deferred-work.md`): the 2.4 clause of `:36` (Label chips take the 2px radius, gated as a mapping) and `:20`'s convention (each new surface adds its own text-spacing case). See "Ledger entries" in Dev Notes.
Depends on: **2.3**, done (`TaskDetail`, `BoardView.razor.cs`'s per-attribute replica, `TaskEditBody`, `EditTaskAttributesHandler`, `PickerOption`, `TaskRoutes.Task`).
Extended later, not here: **2.5** (deleting a Task removes its applications by cascade), **2.8** (the picker's listbox keyboard pattern, `deferred-work.md:515`), **2.10** (filter and sort by Label, AD-29 keyset), **3.2** (Space switch purge, already general), **3.3** (Space deletion cascades through Labels), **6.5** (Labels survive a cross-Project move — nothing to do, they are per Space).

> **Two isolation gates bind this story. Read both before writing a route.**
> 1. **1.9-GATE-002** (`IsolationCaseListTests.Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case`) fails the Isolation suite for any `/api` route the Host maps that no `IsolationCases` row names by exact `(Method, Route)` constant. **Every new `/api` route this story maps — `GET` and `POST …/labels`, `DELETE …/labels/{labelId}` — must have an isolation row with an expectation on both surfaces (`Session` and `ApiToken`) in the same change that maps it.** A row must actually send a request on the route it names (`CaseRun.cs:38`).
> 2. **1.9-GATE-003** (`Every_pending_row_names_a_story_that_has_not_yet_reached_review`) lets a row stay pending (`PendingUntil: "2.4"`) only while 2.4 is `backlog`, `ready-for-dev` or `in-progress`. **It fails any row still pending on 2.4 once 2.4 reaches `review`.** Today no row is pending on 2.4 (grep, 2026-10-25). The ATDD phase may add some; every one must be live before the dev phase sets `review`.
>
> This create-story change moves 2.4 to `ready-for-dev`, which GATE-003 permits.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As someone with more than a handful of Tasks,
I want to tag Tasks with Labels I define for the Space,
so that I can find groups of work without inventing a naming convention inside titles.

## Acceptance Criteria

AC1–AC6 are reproduced **verbatim** from `epics.md:1105-1130` (`status: final`), numbered by position. AC7–AC14 come from FR-20, the ledger, the 1.9 gates, the architecture and the UX spine; each cites its source.

**AC1 — define, Owner and Admin only**
**Given** an active Space
**When** an Owner or Admin defines a Label
**Then** it becomes available to every Project in that Space
**And** a Member or Viewer has no Label-definition affordance present

**AC2 — apply, many-to-many, never by a Viewer**
**Given** a defined Label
**When** an Owner, Admin or Member applies it to a Task
**Then** the application is recorded many-to-many, and a Viewer cannot apply one by any route including the API

**AC3 — no dangling application**
**Given** a Label applied to at least one Task
**When** its deletion is attempted
**Then** it is refused until those applications are removed, so no Task ever references a Label that does not exist

**AC4 — the constrained palette**
**Given** the Label colour choice
**When** it is offered
**Then** it is a constrained palette that satisfies the rules by construction rather than a free colour picker
**And** every offered fill holds 3:1 against both the dark and the light card ground simultaneously, 4.5:1 against its own foreground text, and at least ΔE2000 10 from the focus ring, danger, accent and presence colours — accent and presence included because a Label confusable with the Offer indicator or with Presence defeats both

**AC5 — three chips, then `+N`, and the row wraps**
**Given** a Task carrying more than three Labels
**When** its card renders
**Then** at most three chips appear followed by a `+N` affordance
**And** the metadata row wraps to a second line before it ever scrolls horizontally, because horizontal overflow inside a card at the 320px reflow width is a 1.4.10 failure in the exact layout an audit is conducted in

**AC6 — the entity is per Space**
**Given** the Label entity
**When** the schema test runs
**Then** it is defined per Space with a non-nullable `SpaceId` and a row-level security policy, and no Label is reachable from another Space

**AC7 — applying is an optimistic Task edit** *(FR-20, AR-20 / AD-11; 2.3 Q1 "2.4's Labels … add members"; UX-DR11)*
**Given** the Task detail panel for an Owner, Admin or Member
**When** they add or remove a Label through the Label picker
**Then** the panel and the card show the new set at once without blocking on the network, the server admits or rejects it, and a rejected change reverts the Labels attribute (only) to its last server-admitted value, by the same per-attribute replica 2.3 built
**And** the picker offers exactly the Labels defined for the active Space, shows the current ones as selected, and pre-selects nothing that is not already applied

**AC8 — the new routes are isolation rows on both surfaces, and Label's own two layers are proved** *(1.9-GATE-001/002/003, AR-35, AD-3, AD-4; test-design I-1, I-2, I-3, I-13, C-6)*
**Given** `GET` and `POST /api/v1/spaces/{spaceId}/labels` and `DELETE /api/v1/spaces/{spaceId}/labels/{labelId}`, and the Task `PATCH` carrying `labelIds`
**When** the isolation suite runs
**Then** each route is named by at least one `IsolationCases` row with an expectation for **both** `Session` and `ApiToken`
**And** a foreign Space's Labels are unreachable by list, create and delete on both surfaces, with the boundary `404` byte-identical to an absent Space, one `CrossSpace` row, and nothing written
**And** a foreign Label id used inside one's own Space (deleted, or applied to one's own Task) answers the same bytes as a random id, with no refusal row and nothing written — and that row **fails** when the `Label` query filter and the `Label` FILTER predicate alone are dropped, proved by a plant
**And** no isolation row names 2.4 as pending when 2.4 reaches `review`

**AC9 — refusals are capability-shaped and recorded** *(FR-16, AR-10, AR-11, C-6, V-1)*
**Given** a Member creating or deleting a Label, or a Viewer applying one
**When** the request reaches the API on either surface
**Then** it is `403 insufficient-role` with `capability` `manage-labels` (Member) or `manage-tasks` (Viewer), one `InsufficientRole` row is written, and nothing changes

**AC10 — deletion and application cannot race into a dangling reference** *(FR-22, AR-34; TESTING-CONVENTIONS deterministic-race rule)*
**Given** a Label deletion and an application of the same Label in flight together
**When** both commit
**Then** either the application wins and the deletion is `409 label-in-use`, or the deletion wins and the application is `400 invalid-task-labels` — never a `500`, never a `TaskLabel` row whose Label is gone — proved by cases that hold the lock and wait on `sys.dm_exec_requests`, not by firing requests together

**AC11 — Label writes are idempotent and stable-typed** *(AD-18, AR-25, AR-34; test-design P-10, C-7)*
**Given** a Label creation, deletion, or Labels edit carrying an `Idempotency-Key`
**When** it is replayed
**Then** the original response is returned without re-applying it; the same key with a different body is `422 idempotency-key-reused`
**And** an invalid name, an unknown colour, a duplicate name, an unknown or foreign Label id each answer a `400`/`409` RFC 9457 problem with a stable `type`, write no refusal row, change nothing, and echo no name or id that was not in the request URL

**AC12 — the palette is gated by computation** *(UX-DR7, UX-DR13, X-1)*
**Given** the Label palette in the token layer and its keys in Domain
**When** the Architecture suite runs
**Then** every fill is computed (not estimated) against the AC4 rules in both themes — WCAG ratio for the three contrast rules and CIEDE2000 for the separation rule against both the dark and `-light` value of `focus-ring`, `danger`, `accent` and `presence` — and the Domain palette keys and the CSS palette are the same set
**And** a planted fill that fails any one rule is caught

**AC13 — chips are drawn from the design system and survive text spacing** *(UX-DR5, UX-DR40, UX-DR41; `DESIGN.md:180-183, 451, 457-462`; `deferred-work.md:36, :20`)*
**Given** a Label chip on a card and in the panel
**When** the design gates and the browser suite run
**Then** the chip takes the 2px radius (`--radius-sm`), the `meta` type role, no fixed height and no shadow, gated as a mapping with a plant
**And** at the 320px viewport a card carrying six long Labels has no horizontal overflow in its metadata row, and under the WCAG 1.4.12 override and 200% text-only zoom no chip text is clipped or overlapped

**AC14 — Labels are Space-scoped client state** *(UX-DR27, UX-DR29, UX-DR31, UX-DR33, UX-DR37)*
**Given** the Label list held by the client (Board, panel, Labels surface)
**When** the Space is purged, the Role is re-read lower, or the tab leaves the Space
**Then** the Label list is purged with the Space before any render, a now-Member's Label-definition affordances disappear (narrated, then routed per UX-DR29) and a now-Viewer's Label picker disappears in place, nothing is written to browser storage, `document.title` stays `Yello`, the Label name input carries `autocomplete="off"` and a non-reusable field name, and every string is externalised

## Tasks / Subtasks

- [ ] **Task 1 — Confirm the gates, then record decisions before the code that depends on them** (AC: all)
  - [ ] `dotnet build Yello.slnx`, then run `dotnet test --project tests/Yello.Tests.Isolation` filtered to `IsolationCaseListTests`. With 2.4 at `ready-for-dev` or `in-progress` it is green. List every row with `PendingUntil: "2.4"` (the ATDD phase may add some) in the Debug Log; each is converted before `review`.
  - [ ] Read the open questions (Dev Notes). Decide Q1–Q7 before Task 2, Q8–Q10 before Task 5, Q11–Q14 before Task 8. Record each in the Completion Notes as "**Qn - (x).** … Cost to later stories: …". Each is reversible.

- [ ] **Task 2 — Domain: `Label`, the palette, the name rule, the Labels edit** (AC: 1, 2, 4, 6, 11, 12)
  - [ ] `Yello.Domain/Labels/Label.cs` — **exactly that namespace and type name**: the gates already name `"Yello.Domain.Labels.Label"` (`RefusalBoundaryGateTests.cs:113-124`). Properties as `Project`: `Id` (init), `SpaceId` (init, required), `Name` (set), `Colour` (a palette key, Q3), `CreatedAt`. No navigation properties (the model has none by design).
  - [ ] `LabelName.TryNormalise(string?, out string)` (Q2): trim, 1–50 characters, mirroring `TaskTitle`/`ProjectName`. Invariants in Domain, not validators (AR-34).
  - [ ] `LabelPalette` (Q3): the closed set of palette keys (recommended eight: `rust`, `ochre`, `olive`, `grass`, `jade`, `teal`, `blue`, `orchid`) with `IsKnown(string)`. **Keys only** — no hex in Domain; hex is the token layer's (CSS). A gate holds the two sets equal (Task 9).
  - [ ] `ILabelStore` (`Yello.Domain/Labels/ILabelStore.cs`): `ListAsync`, `CreateAsync(spaceId, name, colour)`, `DeleteAsync(labelId)` returning an outcome (`Deleted`, `NotFound`, `InUse`). Like `ITaskStore`: **no `Where(SpaceId == …)`** — both layers already narrow; a foreign id is simply not found.
  - [ ] `TaskEdit` (`Yello.Domain/Tasks/TaskEdit.cs`) gains the Labels member per Q1 (recommended: `bool ChangesLabels, IReadOnlyList<Guid>? LabelIds`), and `TaskItem` does **not** gain a collection (no navigations); applications live in the store.
  - [ ] No new `Bound` (Q6). `BoundRegistry` stays at six and its gate stays green.

- [ ] **Task 3 — Schema: `Label` and `TaskLabel`** (AC: 2, 3, 6, 10)
  - [ ] `LabelConfiguration(YelloDbContext context)`: table `Label`; `Id` `ValueGeneratedNever()`; `SpaceId` required; `Name nvarchar(50)` required; `Colour varchar(16)` required; `CreatedAt datetimeoffset`; `HasAlternateKey(x => new { x.SpaceId, x.Id })`; unique index `(SpaceId, Name)` (Q2 — the database's default case-insensitive collation makes "Bug" and "bug" collide, which is the intended rule); `HasQueryFilter(label => label.SpaceId == context.ScopedSpaceId)`; FK `SpaceId` → Space cascade, matching `ProjectConfiguration`.
  - [ ] `TaskLabel` (Domain record or Infrastructure-only entity — Q9): `SpaceId`, `TaskId`, `LabelId`, `AppliedAt`. Key `(SpaceId, TaskId, LabelId)`. Index `(SpaceId, LabelId, TaskId)` (the delete's in-use probe, and 2.10's filter by Label). Query filter on `SpaceId`.
    - FK `(SpaceId, TaskId)` → `Task(SpaceId, Id)` **cascade** (2.5's Task delete removes applications).
    - FK `(SpaceId, LabelId)` → `Label(SpaceId, Id)` **`NoAction`** — this is what makes AC3 hold in the database, and it is required anyway: SQL Server refuses two cascade paths into one table (error 1785; Space→Project→Task→TaskLabel and Space→Label→TaskLabel). Same reason Task's Status FK is `NoAction`.
  - [ ] **`Task` has no `(SpaceId, Id)` alternate key yet.** Add `HasAlternateKey(task => new { task.SpaceId, task.Id })` to `TaskConfiguration` and `AddUniqueConstraint("AK_Task_SpaceId_Id", …)` in the migration, as `20261025115412_TaskTable` did for `AK_StatusDefinition_SpaceId_Id`. Expand-only; the previous revision is unaffected.
  - [ ] Register both configurations in `YelloDbContext.OnModelCreating` (`PersistenceModelGateTests.Every_entity_configuration_reaches_the_model`). Add `SchemaNames.Label` and `SchemaNames.TaskLabel` and append both to `SchemaNames.SpaceScopedTables` (the schema tests derive from it). Check `PersistenceModelGateTests.GlossaryTableNames`: `Label` is a glossary term; `TaskLabel` is a join table — if the gate requires glossary names, amend it deliberately with a remark naming the join (Q9).
  - [ ] Migration `…_Labels` via `dotnet ef migrations add`, hand-edited (`TESTING-CONVENTIONS.md` "EF Core migrations", `deferred-work.md:103`): file-scoped namespace, real XML docs, no `// <auto-generated />`, `Up` under 80 lines, no class name that shadows a namespace (S4041 — `Labels` is a namespace; name it e.g. `LabelTables`). Hand-write the RLS SQL for **both** tables, the `Task` pattern verbatim:
    ```sql
    ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
        ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Label],
        ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Label] AFTER INSERT,
        ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Label] AFTER UPDATE;
    ```
    (and the same for `dbo.[TaskLabel]`). `Down` drops the predicates before the tables. `dotnet ef migrations has-pending-model-changes` reports none. `SpaceIsolationSchemaTests` (three predicates per table) and `SchemaVarianceTests` stay green.

- [ ] **Task 4 — Contracts** (AC: 1, 2, 7, 8, 11)
  - [ ] `Yello.Contracts/Labels/LabelRoutes.cs`: `List` and `Create` = `"/api/v1/spaces/{spaceId:guid}/labels"`, `Label` = `"/api/v1/spaces/{spaceId:guid}/labels/{labelId:guid}"`, builders `LabelsOf(s)`, `LabelOf(s, l)`. Routes sit under the Space, **not** under a Project (`The_routes_under_a_Project_are_its_rename_its_Task_creation_and_its_Board` must stay green; FR-22 "available to every Project").
  - [ ] `Yello.Contracts/Labels/LabelResponse(Guid Id, string Name, string Colour)` and `CreateLabelRequest(string? Name, string? Colour)`. **Own namespace** `Yello.Contracts.Labels`: `No_Task_or_Board_response_carries_a_SpaceId_and_a_column_carries_its_own_total` pins the response names in the Tasks and Boards namespaces to exactly four. No `SpaceId` on any Label response.
  - [ ] `TaskResponse` gains `IReadOnlyList<Guid> LabelIds` (appended with a default of empty; AD-19 / FR-37 — `The_due_date_is_a_nullable_DateTimeOffset…` permits gaining fields). Ordered server-side by Label name then id, so the card's first three are stable. Update the client fakes and every construction site.
  - [ ] `EditTaskRequest` gains `IReadOnlyList<Guid>? LabelIds` (Q1). **Amend the gate deliberately**: `The_Task_edit_request_carries_the_title_the_Status_and_the_due_date_and_nothing_else` pins `{DueDate, StatusId, Title}` — make it `{DueDate, LabelIds, StatusId, Title}`, still **no** description, position key, Project or Space. The creation request gate stays exactly `Title`.
  - [ ] **The two Slices shape lists break on purpose**: `TaskProperties = ["createdAt","dueDate","id","positionKey","statusId","title"]` in `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.cs:47` and `tests/Yello.Tests.Slices/Boards/BoardEndpointTests.cs:44` — add `"labelIds"` to both.
  - [ ] `ProblemTypes.cs` (each with remarks stating what it never means):
    - `invalid-label-name` (`400`), `invalid-label-colour` (`400`), `label-name-taken` (`409`, Q2), `label-in-use` (`409`, AC3 — no count and no Task names in the body).
    - `invalid-task-labels` (`400`): a `labelIds` entry that is unknown, foreign, duplicated, or deleted under the request (AC10). Random id and foreign id are **byte-identical**.
    - Reuse `invalid-task-edit` for `labelIds: null` or a non-array, and `insufficient-role` (`manage-labels`, `manage-tasks`) for Role refusals. The `manage-labels` copy already exists (`ClientCopy.resx:482` "Only Owners and Admins can manage Labels.").

- [ ] **Task 5 — Slices, store and endpoints** (AC: 1, 2, 3, 7, 9, 10, 11)
  - [ ] Slices (AR-3, one handler file per slice like 2.1–2.3): `Yello.Application/Labels/ListLabels/ListLabelsHandler.cs`, `Yello.Application/Labels/DefineLabel/DefineLabelHandler.cs`, `Yello.Application/Labels/DeleteLabel/DeleteLabelHandler.cs`. Applying is **not** a new slice: it extends `EditTaskAttributesHandler` (Q1). Handlers take `ActiveSpaceContext` or nothing; **none reads the Role, counts, checks a capability or touches `Idempotency-Key`** (the gates `Only_the_pipeline_depends_on_…`, `Nothing_but_the_pipeline_reads_the_Role_…` catch each). Results follow 2.3's result-plus-outcome-enum pattern (`EditTaskAttributesResult`).
  - [ ] `LabelStore` (`Yello.Infrastructure/Persistence/LabelStore.cs`), registered in `SpaceContentStores.cs`. Ids from `IIdentifierGenerator.Next()` (`SequentialGuidIdentifierGenerator`; never `Guid.NewGuid()`/`CreateVersion7()`). A duplicate name is detected by catching the unique-index violation (2601/2627) on `(SpaceId, Name)` and answering `label-name-taken` — **never a `500`**, and never a pre-check alone (two concurrent creates).
  - [ ] **Delete, serialised against apply (AC3, AC10)** — raw SQL is permitted in Infrastructure only (`SpaceResolutionSeamTests`), parameterised `SqlQuery`/`ExecuteSql`, never concatenation:
    1. Lock the Label row: `SELECT … FROM Label WITH (UPDLOCK, HOLDLOCK) WHERE Id = @id` (filter and RLS narrow it). None → `NotFound`.
    2. Probe `TaskLabel WITH (UPDLOCK, HOLDLOCK) WHERE LabelId = @id` (the `(SpaceId, LabelId, TaskId)` index keeps the range inside one Space). Any → `InUse` → `409 label-in-use`.
    3. Delete. The `NoAction` FK is the backstop; if it ever fires (547), answer `409 label-in-use`, never `500`.
  - [ ] **Apply, inside the 2.3 edit (AC2, AC7, AC10)**: in `TaskStore.EditAsync` (or a sibling the handler calls in the same unit of work), when `ChangesLabels`:
    1. Refuse duplicates in the submitted list → `invalid-task-labels`.
    2. Read the submitted Labels `WITH (UPDLOCK, HOLDLOCK)` (filter + RLS hide foreign ones). Count mismatch → `invalid-task-labels`, **before any write**, so the composite FK never answers a `500` (2.3's Status rule).
    3. Replace the Task's set: delete the applications not submitted, insert the missing ones. Only that Task's `TaskLabel` rows change; no other attribute, no position key.
    4. Save before returning (`IdempotencyStore.EnsureNothingElseIsPending`).
    Lock order: the Task (2.3 takes the Project range), then Labels. Delete takes Label then TaskLabel. Confirm with the race cases (Task 7) that the two orders cannot deadlock; if they can, record what was measured and take the Label locks first in the edit. Record the choice in `TaskStore`'s and `LabelStore`'s remarks.
  - [ ] `TaskEditBody.cs` (merge patch): `labelIds` absent = unchanged; `[]` clears; `null` or not an array → `invalid-task-edit`; an element that is not a GUID → `invalid-task-labels`. Cap the array length as an **input-shape guard**, not an NFR-8 bound (Q6; 2.3's unbounded `dueDate` string ran a regex to timeout — review finding). A body naming no editable member stays `invalid-task-edit`.
  - [ ] Endpoints in `Yello.Host/Endpoints/LabelEndpoints.cs`, mapped in `Program.cs` through `api` beside the Task endpoints and before `MapUnmatchedApi()`:
    - `GET LabelRoutes.List` → `RequiresCapability(Capability.ReadProjectsAndTasks)` → `200 LabelResponse[]` ordered by name then id. Viewers read Labels (they see chips).
    - `POST LabelRoutes.Create` → `RequiresCapability(Capability.ManageLabels)` → `201` with `Location` set on the headers yourself and `Results.Json(…, statusCode: 201)` (**`Results.Created`/`Accepted` are banned**, ARCH-009). No `ConsultsBound`.
    - `DELETE LabelRoutes.Label` → `RequiresCapability(Capability.ManageLabels)` → `204` via `Results.NoContent()`/`Results.StatusCode(204)`; `IdempotentResponses` replays a bodiless status (`IdempotentResponses.cs:187`) — confirm with the replay case. Not found → `BoundaryProblems.NotAvailable()` (byte-identical, no refusal row).
    - The Task `PATCH` stays `ManageTasks` (FR-20: applying is a Task edit; `ManageLabels` is definition only — `prd.md` FR-22, matrix row). **No new capability** is added.
    - None is `SessionOnly` (AD-4). Each `{spaceId}` endpoint declares exactly one capability (`SpaceScopedRouteCheck` refuses startup otherwise). Bodies are read with `JsonRequestBody.ReadAsync`; unreadable → `400`, never `500`. Answers through `Results.Json`/`Results.Ok`/status results (ARCH-010).
  - [ ] Board read (`ViewBoardHandler` / `BoardEndpoints`) and `ViewTask` return each Task's `LabelIds`: one extra query over `TaskLabel` for the Task ids of the page being returned (never per Task; never `OFFSET`), composed into `TaskEndpoints.Response(TaskItem, …)`. Note on `deferred-work.md:493` (Board totals vs the Task read, owner 2.9) that the Board read now has a third statement.
  - [ ] Logs carry `SpaceId`, never a Label name or Task title (AR-34, I-17). Every Label name an isolation case writes goes in the world's secret set.

- [ ] **Task 6 — Idempotency** (AC: 11)
  - [ ] Nothing new in the pipeline (it already covers POST/PUT/PATCH/DELETE on `{spaceId}` routes). Wire cases on **both** surfaces: `POST` label same key twice → one Label, identical bodies; same key different body → `422`; `DELETE` same key twice → one `204` replayed (the second is **not** a `404`); a `PATCH` `labelIds` replay → one change.

- [ ] **Task 7 — Slice, schema and wire tests** (AC: 1, 2, 3, 6, 9, 10, 11)
  - [ ] **Memory-bound machine: add no new container-backed class.** Put the cases in new partials of the existing `TaskEndpointTests` (`IClassFixture<MigratedDatabaseFixture>`), e.g. `TaskEndpointTests.Labels.cs`, `.LabelsSchema.cs`, `.LabelsRace.cs`, `.LabelsIdempotency.cs`, reusing `TaskWire`/`TaskEditWire` and `HostProcess`/`WireSession`. Do not add a `SqlServerContainerFixture` (`ContainerTopologyGateTests`). Handler unit tests (`Labels/DefineLabel/DefineLabelHandlerTests.cs` etc.) use hand fakes and no database.
  - [ ] Cases:
    - Owner and Admin create a Label; it lists in that Space and is applicable to a Task in **two different Projects** of the Space (AC1 "every Project").
    - Member and Viewer `POST`/`DELETE` → `403 manage-labels`, one `InsufficientRole` row, nothing written, both surfaces.
    - Member applies and removes Labels (`200`, response and `SELECT` agree; `[]` clears; absent member unchanged). Viewer `PATCH labelIds` → `403 manage-tasks`, one row, nothing written, both surfaces (C-6).
    - `invalid-label-name` (empty, whitespace, 51 characters), `invalid-label-colour` (unknown key, a hex string), `label-name-taken` (same name, differently cased), unreadable body → `400`/`409`, never `500`, no refusal row.
    - `invalid-task-labels`: random id, a **real Label id from another Space** (byte-identical to random), duplicate ids, a deleted Label's id; nothing changed.
    - Delete an unused Label → `204`, gone from the list; delete an applied Label → `409 label-in-use`, still listed, applications intact; remove the applications, delete again → `204`.
    - The same Label name in two Spaces is allowed (uniqueness is per Space).
    - Labels survive a title, Status and due-date edit (the Labels member absent leaves them untouched).
  - [ ] **Schema** (`TaskEndpointTests.LabelsSchema.cs`, the `TaskEndpointTests.Schema.cs` pattern): `Label` and `TaskLabel` carry a non-nullable `SpaceId` under the Space policy with three predicates; moving a `Label` to another Space is refused by the database; `TaskLabel` reaches `Task` and `Label` only through keys that carry `SpaceId`; the `TaskLabel→Label` FK is `NO_ACTION` and `TaskLabel→Task` is `CASCADE`; `Task` has `AK_Task_SpaceId_Id`.
  - [ ] **Deterministic races (AC10)**, with `TaskWire.WaitUntilBlockedOnALockAsync`: (a) the test inserts a `TaskLabel` in its own open transaction, sends `DELETE`, waits until the request is blocked on `LCK_M_%`, commits → `409 label-in-use`; (b) the test deletes the Label in its own open transaction, sends the `PATCH` applying it, waits, commits → `400 invalid-task-labels`; (c) two `POST`s of the same name, one held → one `201`, one `409 label-name-taken`. Plant: remove each lock hint → caught three runs out of three. Never `Task.Delay`.

- [ ] **Task 8 — Isolation rows, including Label's two-layer row** (AC: 8, 9)
  - [ ] Add the rows in the "Isolation rows" table (Dev Notes), each covering **both** surfaces via `Both(…)`, each sending a request on the exact route constant it names. Seed through the product (`ProductActor`): add a seed helper (e.g. `SeedLabelAsync(actor)` → `labelId`, and an apply helper using the Task `PATCH`) beside `SeedProjectWithTaskAsync`.
  - [ ] **`ISO-LABEL-FOREIGN-ID-IN-OWN-SPACE`**: A sends `DELETE /spaces/{SA}/labels/{LB}`, anchored on `DELETE /spaces/{SA}/labels/{random}` → `SameAsAnchor(404, not-available)`, no refusal row, B's Label still present. Plant, one at a time, original held in memory and restored in a `finally`: drop **only** the `Label` query filter **and** the `Label` FILTER predicate → the row fails on both surfaces; then each layer alone → green.
  - [ ] **`ISO-APPLY-FOREIGN-LABEL`**: A `PATCH`es its own Task with `labelIds: [LB]`, anchored on `[random]` → `SameAsAnchor(400, invalid-task-labels)`, nothing written in either Space. Pass the body explicitly (`ProductActor.SendAsync` sends a default `{name, title}` on `PATCH`).
  - [ ] The Member row needs a Member in A's Space: seed it the way `ViewerEditsATaskAsync` seeds its Viewer.
  - [ ] No new timing row: the routes are under the API group, so `NotAvailableFloor` applies. If one is added, it runs in `TimingCollection`.
  - [ ] Remove the exclusion `.Where(name => name != "Yello.Domain.Labels.Label")` in `RefusalBoundaryGateTests.Tasks.cs:61-77` (`Every_Space_scoped_entity_the_gates_name_resolves_to_a_Domain_type`) — the type now exists. AR-9's `No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity` then covers `Label` for real; plant a Label route without `{spaceId}` → caught.
  - [ ] GATE-003: no row pending on 2.4 at `review`.

- [ ] **Task 9 — The palette in the token layer, gated by computation** (AC: 4, 12, 13)
  - [ ] `tokens.css`: a **separate family** of palette tokens, declared once and **not** rebound at the theme boundary (one fill renders in both themes — the rule is "against **both** card grounds simultaneously"): `--label-rust: #D2592D` … and one foreground `--label-ink: #0B1020` (Q4). Values (computed for this story; Debug Log must record the harness's own numbers):

    | Key | Fill | vs `surface-card` #18213C | vs `surface-card-light` #FFFFFF | vs ink #0B1020 | min ΔE2000 vs the 8 avoided values |
    |---|---|---|---|---|---|
    | rust | #D2592D | 3.94 | 4.03 | 4.69 | 20.4 |
    | ochre | #B18225 | 4.61 | 3.45 | 5.49 | 38.6 |
    | olive | #8A8A42 | 4.38 | 3.62 | 5.22 | 40.2 |
    | grass | #5B9E2E | 4.82 | 3.29 | 5.75 | 44.2 |
    | jade | #469169 | 4.17 | 3.81 | 4.97 | 28.9 |
    | teal | #428A8A | 3.96 | 4.01 | 4.72 | 13.8 |
    | blue | #357FD4 | 3.89 | 4.09 | 4.63 | 15.6 |
    | orchid | #CC3DD6 | 3.94 | 4.03 | 4.69 | 18.0 |

    Avoided values: `focus-ring` #ADA4FB / #6E62E6, `danger` #FB7185 / #BE123C, `accent` #8B80F9 / #5145CD, `presence` #38BDF8 / #0E7490. Minimum mutual ΔE2000 between the eight fills is 14.0. The arithmetic (sRGB → linear → WCAG luminance; sRGB → XYZ D65 → CIELAB → CIEDE2000) is a scratch computation done during story creation; **the gate's own implementation is the authority** — if it disagrees, fix the table, not the gate.
  - [ ] **Gates this breaks on purpose — amend deliberately, never loosen**: `The_colour_token_layer_declares_exactly_the_thirty_names_the_design_states` (counts every hex custom property), `The_gated_set_is_exactly_the_eighteen_pairs_the_requirement_names`, `Every_theme_boundary_rule_rebinds_all_fifteen_semantic_names`. Exclude the `--label-*` family from those three by an explicit, named prefix, and add a gate pinning that family to exactly the Domain `LabelPalette` keys plus `--label-ink`. `No_colour_is_stated_as_a_literal_outside_the_token_layer` must stay green: chips take their fill through a class (`.label-chip--rust { background: var(--label-rust) }`), **never** an inline `style=""`.
  - [ ] `tests/Yello.Tests.Architecture/CieDe2000.cs` (new; nothing implements ΔE2000 today) beside `WcagContrast.cs`, with a known-answer test from Sharma, Wu & Dalal (2005) reference pairs before it is trusted. Then `LabelPaletteGateTests` (or a partial of `ColorTokenContrastTests`): for each fill, the three ratios and the eight ΔE values against the AC4 thresholds. Plants: one fill at #8B80F9 (accent), one at #FFFFFF, `--label-ink` set to `--text-primary`'s value → each caught.
  - [ ] Picker presentation never relies on colour alone: each swatch carries its localised colour name (UX-DR7 "nothing depends on colour").

- [ ] **Task 10 — The client: chips, the Label picker, the Labels surface** (AC: 1, 2, 5, 7, 13, 14)
  - [ ] **Label list state**: the Space's Labels are read from `GET …/labels` by the surfaces that need them (`BoardView` alongside the Board; the Labels surface), held in component state, purged through `ActiveSpace.RegisterPurge` and the `_generation` guard (UX-DR31). Nothing in browser storage (`ClientStorageBanTests`). The client never requests a Space id absent from the switcher answer.
  - [ ] **`LabelChip.razor`**: a non-interactive `<span class="label-chip label-chip--{key} type-meta">` holding the name; `border-radius: var(--radius-sm)`, `color: var(--label-ink)`, padding from spacing tokens in rem (`card-pad`-style), no fixed height, no shadow, no `text-transform` (Label names are user data, not copy). `--radius-full` is forbidden (`The_fully_round_radius_is_declared_once_and_used_by_exactly_one_component_the_column_count_chip`).
  - [ ] **`TaskCard.razor`** metadata row (`task-card__meta`): the Task's Labels ordered by the server's order, **at most three chips then `+N`** (Q5), then the due date. `flex-wrap: wrap`, `min-width: 0` on chips, long names wrap or break (`overflow-wrap: anywhere`) — **never** `overflow-x: auto` or `nowrap`. The card keeps **exactly one control** (the open button) — `BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow` holds that; amend its "text is only the title" clause deliberately to "the title is the only text outside the metadata row". Update the card comment that says "Labels (2.4) … join it later" and the `components.css:731-736` remark.
  - [ ] **`TaskDetail.razor`**: a Labels field after the due date ("attributes above", `DESIGN.md:471`). For `CanManage`: the applied chips plus a "Labels" control opening an in-panel multi-select listbox (`role="listbox"` `aria-multiselectable="true"`, options from the Space's list, `aria-selected` true for applied ones) — a multi-select variant of `PickerOption` (it is single-select and typed to `TaskStatusOption` today), the `.picker` styling, inside the focus trap; `Esc` closes the picker first (innermost meaning wins). Toggling an option commits the whole new set (Q1). **Update `FocusLastAsync`** — it picks Clear/date/Close as the last control today; the new field changes the order. When the Space has no Labels: for an Owner/Admin "No Labels yet." plus a link to the Labels surface; for a Member "No Labels yet." alone. For a Viewer: the chips as text in the `<dl>`, no control (UX-DR27).
  - [ ] **Optimism and revert (AC7)**: add `TaskAttribute.Labels` to `BoardView.razor.cs` and a case in each per-attribute helper (`BodyOf` → `{"labelIds": […]}`, `MemberOf`, `ValueOf`, `Overlay`, `Admit`, `Displayed()`). Same queueing (only the latest queued set is sent), same `Idempotency-Key` reuse on an identical resend after no answer, same 404 → `OnRefused`, same inline `RefusalText.Of(…)` for 400/403/409/422, same "Not sent." on 5xx/no answer, same refusal-for-this-Task-only rule (2.3 review). A refusal reverts **only** the Labels attribute to its last admitted set.
  - [ ] **The Labels surface (AC1, Q7)**: `Pages/SpaceSettings.razor` at `ClientRoutes.SpaceSettings(spaceId)` (`/spaces/{spaceId}/settings`), holding a Labels section only for now (`label-list` + define form + delete). Reached from Space home by a "Space settings" link **present only for Owner and Admin** (`LabelCapabilities.PermitsManageLabels(roleCode)`, held to the matrix by a gate cloned from `The_clients_Task_controls_predicate_agrees_with_the_capability_matrix`). A Member or Viewer arriving by URL gets the within-Space capability surface ("Only Owners and Admins can manage Labels.", UX-DR32 — not a toast). A Role dropping below Admin while on it: narrate "You're now a Member. Space settings is no longer available." then route to Space home (UX-DR29; focus per UX-DR39).
    - Define form: name input (`autocomplete="off"`, non-reusable field name via 2.1's `FormField`), a colour radio group of the eight swatches each with its localised name (Q8 decides the default), one `button-primary` "Add Label". Creation waits for the server (it needs the id; 2.1's Project creation pattern), refusals inline with their own copy.
    - Delete: per Label, a delete control opening `destructive-confirm` **in place** (UX-DR12/36: names the Label, "This cannot be undone.", `button-danger` + `button-secondary`); **not optimistic** (UX-DR26). `409 label-in-use` → its own copy inline, the Label stays.
    - `document.title` stays `Yello`; the URL holds only ids.
  - [ ] **Copy** — `ClientCopy.resx` only, sentence case, no "archive", no "save": "Labels", "Space settings", "Add Label", "Label name", "Colour", the eight colour names, "No Labels yet.", "+{0}" and its accessible name "{0} more Labels" (Q5), "Delete Label", the confirm text, and `RefusalCopy.For` branches for `invalid-label-name`, `invalid-label-colour`, `label-name-taken`, `label-in-use`, `invalid-task-labels` (unknown types fall through to "Not available.", which would be untrue). `LocalisationGateTests` and `No_user_visible_string_literal_appears_in_a_component` hold the rest — and do not quote copy in a code comment (a 2.3 run tripped on that).

- [ ] **Task 11 — Browser tests** (AC: 1, 2, 5, 7, 13, 14)
  - [ ] `tests/Yello.Tests.Client` against `FakeYelloApi` (add `GET/POST …/labels`, `DELETE …/labels/{id}`, `labelIds` on the Task `PATCH`, `FakeTask.LabelIds`; bodies are `Yello.Contracts` types; reuse `HoldTaskEditAsync`/`ReleaseTaskEdit*`). Same collection (`ClientSurfaceCollection`) — no new fixture.
  - [ ] Optimistic apply: **hold** the `PATCH`, assert the card and panel already show the new chip, release with `400 invalid-task-labels`, `403`, `404`, `409`, `422`, and no answer — assert each revert and its copy; a concurrent title edit's outstanding value is not reverted by a Labels refusal.
  - [ ] Card: five Labels → three chips and `+2` with its accessible name; at a 320px viewport with six long names, `scrollWidth <= clientWidth` on the metadata row and the card. Plant `flex-wrap: nowrap` → caught.
  - [ ] Absence: a Member sees no "Space settings" link and no define form; a Viewer sees chips and no Label picker; `main [disabled], main [aria-disabled='true'], [role=dialog] [disabled]` count 0. Plants for each.
  - [ ] Purge: switch Space with the Labels surface and the panel open → no previous Label name in the DOM.
  - [ ] Delete confirm replaces in place (at most one `[role=dialog]`); `409` keeps the Label and says why.
  - [ ] `TextSpacingTests`: add a card with Labels (wrapped) and the Labels surface under the 1.4.12 override and 200% text-only zoom (`deferred-work.md:20`).
  - [ ] Never Playwright's URL wait; poll `location.pathname`. No `RegexOptions.CultureInvariant`. **Rebuild `Yello.Client` before a browser plant** (the suite runs `--no-build` against it), or the plant looks uncaught.

- [ ] **Task 12 — Design gates, plants, runs, ledger** (AC: all)
  - [ ] `DesignFoundationGateTests` (beside `Task_cards_and_columns_take_the_3px_radius` and `The_Task_detail_panel_takes_the_6px_radius`): `Label_chips_take_the_2px_radius` — plant 3px → caught. If the Labels surface's delete confirm is the product's first dialog other than the panel, add its 6px clause too (`deferred-work.md:36` "dialogs other than the panel (the first story that builds one)").
  - [ ] Plants one at a time in the foreground; hold the original in memory and restore in a `finally`; compare the tree hash after each. **Never** `git checkout --`, `git restore`, `git reset` or `git stash`. Write each plant so it compiles. Record `| # | Plant | Gate | Caught |` in the Debug Log. Minimum set:
    - A row left `PendingUntil: "2.4"` with 2.4 at `review` (GATE-003).
    - The `POST …/labels` with no isolation row (GATE-002).
    - The Label two-layer drop, and each layer alone (green).
    - The delete's lock hint removed; the apply's lock hint removed (three runs each).
    - The handler skips the foreign-Label check (must not become `500` or `200`).
    - `TaskLabel→Label` FK set to cascade (the in-use case must catch it).
    - `ConsultsBound` on a Label route (ARCH-004 / Host start).
    - A palette fill failing each rule; `--label-ink` = `text-primary`.
    - A Member offered the Space settings link; a Viewer offered the Label picker.
    - `flex-wrap: nowrap` on the metadata row; a 3px chip.
    - The Labels list left in the DOM after a Space purge.
  - [ ] Runs: `dotnet build Yello.slnx` (warnings are errors), then `dotnet test Yello.slnx`. Baseline after the 2.3 review: **850 total, 847 passed, 0 failed, 3 skipped** (`YELLO_MIGRATION_BUNDLE` unset; Client alone 102/102). `--no-build` runs zero tests unless the same configuration was just built. If a Client case fails only in the full run, run `dotnet test --project tests/Yello.Tests.Client` alone before calling it a regression. If Rancher's socket times out: `rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch.
  - [ ] `tests/TESTING-CONVENTIONS.md`: the palette harness (ΔE2000 known-answer first), the new counts.
  - [ ] Ledger (`deferred-work.md`): UPDATE `:36` (Label chips done; which surfaces remain), `:20` (Labels case added), `:493` (third Board statement). ADD each with an owner: the Label-chip foreground amendment to `DESIGN.md:180-183` (owner UX, Q4); rename/recolour of a Label (owner per Q10); the Labels-per-Space / per-Task bound question (owner PM, Q6); sort by a many-to-many Label in the List View (owner 2.10); the `+N` interaction (owner UX, Q5); the Space settings page's later sections (owners 4.2/4.4/5.1/6.3).
  - [ ] `sprint-status.yaml`: set `review` at the end of the dev phase. **Do not add review narration to source files.**

## Dev Notes

### What exists today (read before writing)

- **Already in place for Labels**: `Capability.ManageLabels` (`Yello.Domain/Memberships/Capability.cs`; matrix row `[Yes, Yes, No, No]` in `RoleCapabilityMatrix.cs:45`, transcribed in `RefusalBoundaryGateTests.cs:89`); the `manage-labels` refusal copy (`ClientCopy.resx:482`); `"Yello.Domain.Labels.Label"` in `RefusalBoundaryGateTests.SpaceScopedEntities` (`:113-124`) with a temporary exclusion in `RefusalBoundaryGateTests.Tasks.cs:61-77`; `--radius-sm: 2px` (`tokens.css:278-282`, meant for Label chips). **Nothing else**: no entity, table, route, contract, problem type, component, CSS class or palette.
- **There is no `src/` folder.** Production projects sit at the repo root (`Yello.Domain`, `Yello.Application`, `Yello.Infrastructure`, `Yello.Host`, `Yello.Contracts`, `Yello.Client`). Tests are in `tests/`.
- **Slices are one handler file each** (AR-3 says command, handler, validator and tests; in practice validation is a Domain static rule, e.g. `TaskTitle.TryNormalise`, called by the endpoint, which answers `Results.Problem(type: …)`). Follow the code, not the letter.
- **Routes are `/api/v1/spaces/{spaceId:guid}/…`** (`TaskRoutes`, `SpaceRoutes.Statuses`).
- **Task (2.3 state)**: `TaskItem` (`Id`, `SpaceId`, `ProjectId`, `StatusId`, `Title`, `DueDate`, `PositionKey`, `CreatedAt`); `ITaskStore.CreateAsync/FindAsync/EditAsync`; `TaskStore.EditAsync` takes the Project-wide range under `UPDLOCK, HOLDLOCK` for a Status change; `EditTaskAttributesHandler(ITaskStore, IStatusDefinitionReader)` returns `EditTaskAttributesResult` with `EditTaskAttributesOutcome { Edited, NotFound, StatusOutsideTheEffectiveSet }` — add `LabelsInvalid`. `TaskEditBody` reads a `JsonDocument` and uses `TryGetProperty` to distinguish absent from `null`. **Preserve**: title/Status/due-date behaviour and every 2.3 test; position keys untouched by a Labels-only edit.
- **Client (2.3 state)**: `BoardView.razor.cs` holds `Dictionary<Guid, TaskReplica> _replicas` (each with `Admitted`, `InFlight`, `Queued`, `Unanswered` keyed by `TaskAttribute { Title, Status, DueDate }`), `EditAsync → SendInTurnAsync → SendAsync → SettleAsync → UseAnswerAsync`, `PurgePanel()`, `_generation`, `_focusCard`. `TaskDetail.razor` is a modal dialog with focus-trap sentinels and `FocusLastAsync`; Viewer branch is a `<dl>`. `TaskCard.razor` has `<div class="type-meta task-card__meta">@DueText</div>`. `PickerOption.razor` is single-select `<li role="option">` typed to `TaskStatusOption`. Capability predicate pattern: `Spaces/TaskCapabilities.PermitsManageTasks`. **Preserve** every 2.3 review fix (stale focus request, pointer-blocking layer, per-Task refusal routing, `InFlight` cleared after admission, unreadable-200 keeps the key, 5xx is no answer).
- **Pipeline (do not reimplement in a slice)**: Space resolution, capability, refusal recording, idempotency (`ActiveSpaceResolution.Idempotency.cs`, `IdempotentResponses.cs`, records per (Space, Account), 24 h), bounds (`BoundRegistry`, `BoundCounter`, `.ConsultsBound`), `NotAvailableFloor`. The pipeline-only gates catch any slice that touches these.

### Isolation rows (Task 8)

| Id | Test design | Vector | Route | Request | Expectation (both surfaces) |
|---|---|---|---|---|---|
| ISO-LIST-LABELS-FOREIGN-SPACE | I-1 | Read | `GET LabelRoutes.List` | A → `LabelsOf(SB)` | `BoundaryNotFound` |
| ISO-LIST-LABELS-OWN-SPACE | I-3 | Read (listing) | `GET LabelRoutes.List` | A → `LabelsOf(SA)` with B holding Labels | `200`, no B Label name or id in the body |
| ISO-CREATE-LABEL-FOREIGN-SPACE | I-2 | Write | `POST LabelRoutes.Create` | A → `LabelsOf(SB)` | `BoundaryNotFound`, nothing written in either Space |
| ISO-DELETE-LABEL-FOREIGN-SPACE | I-2 | Write | `DELETE LabelRoutes.Label` | A → `LabelOf(SB, LB)` | `BoundaryNotFound`, B's Label still present |
| ISO-LABEL-FOREIGN-ID-IN-OWN-SPACE | I-1, I-13 | Write | `DELETE LabelRoutes.Label` | A → `LabelOf(SA, LB)`, anchor `LabelOf(SA, random)` | `SameAsAnchor(404, not-available)`, no refusal row; two-layer plant |
| ISO-APPLY-FOREIGN-LABEL | I-2, I-13 | Write | `PATCH TaskRoutes.Task` | A → own Task, `labelIds: [LB]`, anchor `[random]` | `SameAsAnchor(400, invalid-task-labels)`, nothing written |
| ISO-RECORD-KIND-LABEL-DEFINE-MEMBER | V-1, C-6, NFR-7 | RecordKind | `POST LabelRoutes.Create` | a Member of SA | `RoleRefused("manage-labels")`, nothing written |
| ISO-RECORD-KIND-LABEL-APPLY-VIEWER | V-1, C-6, NFR-7 | RecordKind | `PATCH TaskRoutes.Task` | a Viewer of SA, `labelIds` body | `RoleRefused("manage-tasks")`, Labels unchanged |

I-11 (Account-scoped surfaces carry no Label) needs no new row: no Account-scoped route is touched. If the switcher or Account settings answer is ever widened, I-11 applies.

### Open questions (decide in Task 1; each reversible, record as "Qn - (x)")

1. **How is a Label applied over the API?** (a) A `labelIds` member on the existing Task `PATCH` (merge patch, the whole set replaces the current set; `ManageTasks`). (b) `PUT`/`DELETE /tasks/{taskId}/labels/{labelId}` per application. **Recommend (a)**: 2.3 Q1 said "2.4's Labels … add members"; it reuses the per-attribute replica and the 2.3 edit pipeline unchanged; one route fewer for GATE-002. Cost: two people editing one Task's Labels at once lose one change (last writer wins per attribute — the 2.3 Q2 rule). 8.1 may add (b) for scripts later.
2. **Label name rule and uniqueness.** No source specifies either (rubric review flagged "unbounded name length"). **Recommend**: trimmed, 1–50 characters; unique per Space case-insensitively by the default collation, refused `409 label-name-taken`. Tag the tests `Assumption` per TESTING-CONVENTIONS (source: this story) since no PRD §12 entry covers it.
3. **How is the colour stored and sent?** **Recommend** a palette key (`"rust"`), not a hex value: Domain validates membership, the token layer owns the hex, and a later palette adjustment changes no data. An unknown key is `400 invalid-label-colour`.
4. **Which foreground does a chip use?** `DESIGN.md:180-183` says `foreground: {colors.text-primary}` (#E6EAF5 dark / #131A2B light). No fill can hold 3:1 against both card grounds *and* 4.5:1 against a foreground that swaps with the theme: the dark-theme foreground is too light. **Recommend** one fixed ink `--label-ink: #0B1020` for every fill in both themes (UX-DR13's own words are "4.5:1 against its **own** foreground text"). This is a deviation from the token block: record it in `deferred-work.md` (owner UX) as an amendment `DESIGN.md` needs. Do not edit planning artifacts.
5. **What is `+N`?** Unspecified. **Recommend**: non-interactive text (no second control on the card; the card's one control opens the panel, where every Label shows), accessible name "{N} more Labels". Ledger to UX.
6. **Bounds.** NFR-8 names no Label bound and the `BoundRegistry` gate pins six. **Recommend**: add no NFR-8 bound; guard the `labelIds` array length in `TaskEditBody` as input shape (refuse above, e.g., 500 entries as `invalid-task-edit`) and cap the name at 50 characters. Ledger the "Labels per Space / per Task" question to PM (owner PM, measured by 2.9).
7. **Where does Label definition live?** EXPERIENCE/PRD §7 say Space settings, which does not exist yet and has no Label component row or mockup. **Recommend** a Space settings page with a Labels section only, linked from Space home for Owner/Admin. Alternative: a section on Space home (smaller, but every later settings story moves it).
8. **Default colour in the define form.** UX-DR11 forbids a default only "where the choice is consequential"; a colour is not. **Recommend** pre-selecting the first palette entry not yet used in the Space (or `rust` when all are used).
9. **`TaskLabel` in Domain or Infrastructure only?** **Recommend** Infrastructure-only entity (no Domain behaviour; the Domain sees `IReadOnlyList<Guid>` on the edit and the response). If `PersistenceModelGateTests.GlossaryTableNames` requires glossary terms, add `TaskLabel` with a remark that it is the many-to-many named by the glossary's Label entry.
10. **Rename and recolour.** FR-22 says "define"; no AC requires either. **Recommend** out of scope: delete-and-recreate covers a mistake while unused. Ledger "rename/recolour a Label" (owner: epic 2 backlog / PM). If the dev phase finds it cheap, a `PATCH …/labels/{labelId}` would need its own isolation rows.
11. **Delete when in use: refuse or strip?** The PRD says "cannot be deleted without those applications being removed"; the epic AC says "refused until". **Decided by the AC**: refuse with `409 label-in-use`. Should the copy say how many Tasks carry it? **Recommend** no count in the body (no wire change needed); copy "This Label is on Tasks. Remove it from them first."
12. **Label order on the card and in the panel.** **Recommend** server order by name (database collation) then id; the client does not re-sort.
13. **Announcements.** **Recommend** none on apply (deliberate action in a dialog, the chip appears in place); refusals in the panel's existing `role="status"` region. A Label created or deleted on the Labels surface announces politely ("Label added.", "Label deleted.").
14. **Swatch control.** **Recommend** a native radio group (`fieldset`/`legend` "Colour"), each radio labelled by colour name with a swatch beside it — arrow keys come free, no custom listbox.

### Architecture compliance (must)

- `SpaceId` is never read from the request body; it comes from `ActiveSpaceContext`. No `Where(SpaceId == …)` in stores — RLS + query filter already narrow; adding it would mask a missing layer.
- Validate every referenced id (Label) **before** any write, so the composite FK is never what refuses (that is a `500`).
- `DateTimeOffset` UTC only (`CreatedAt`, `AppliedAt`); no `DateTime` (`No_Domain_or_Contracts_member_is_typed_DateTime_or_DateOnly`).
- No `[Authorize(Roles=…)]`, `IsInRole`, Identity roles (AR-4).
- Logs: `SpaceId` as a field; never a Label name, Task title, cookie or token.
- No Space-scoped content in browser storage; `document.title` fixed `Yello`; the client owns every string.

### Library / framework

No new packages. .NET 10.0.11, EF Core 10, Blazor WASM 10, xunit.v3, Testcontainers (`mssql/server:2025-latest`), Playwright for .NET, ArchUnitNET 0.13.3 — all as pinned by 1.1. CIEDE2000 is ~60 lines of arithmetic in the test project; do not add a colour library.

### Testing standards (from `tests/TESTING-CONVENTIONS.md`)

- Add cases to the existing suites; no new suite, no new container fixture. One SQL engine per process, one database per class — hence partials of `TaskEndpointTests`.
- Only the `Suite`, `Priority`, `Requirement`, `Assumption` traits. Sentence-style names, one behaviour per test.
- No `Task.Delay`; races by holding `UPDLOCK, HOLDLOCK` and polling `sys.dm_exec_requests`.
- Randomised data; never a shared email address. Cleanup by rollback or disposal, never `DELETE`.
- Every absence assertion is proved against a plant. No coverage threshold.
- Playwright fakes the API at the network edge with Contracts types; optimistic edits: hold, assert, release, assert.
- Timing-sensitive tests in `DisableParallelization` collections (`BoundaryDurationCollection`, `TimingCollection`). Run plants alone, never beside the full suite — this machine is memory-bound.

### Previous story intelligence (2.3)

- 2.3 decisions to keep: Q1 merge patch; Q2 per-attribute last writer wins; Q5 a stable `400` type per field plus `invalid-task-edit`; Q6 the panel is Board state, not a route; Q8 per-attribute last-admitted revert, same-attribute edits serialised, only the latest queued value sent, "Not sent." keeps the key; Q9 `Esc` innermost first; Q11 the in-panel `.picker` inside the focus trap.
- Review lessons that apply directly: a refusal for one Task must not show in another's panel; `InFlight` cleared only after admission; a `200` with an unreadable body keeps the key; `5xx` is no answer, not a refusal; bound every free-form wire input (the `dueDate` regex timeout); a row removed between read and reload under the lock must not answer `200`; every `400` asserts "no refusal row"; assert the problem `type`, not just the status.
- Gates surprised 2.3 at implementation: the Task request/response shape gates and the two `TaskProperties` lists. They are named in Task 4 so they are not surprises here.
- Debug practice: ATDD scaffolds use `[Fact(Skip="red: …")]`; corrections to scaffolds are recorded, not silently deleted.
- Deferred to 2.8 from 2.3: the picker is not the full listbox keyboard pattern (`deferred-work.md:515`). The Label picker inherits that; do not fix it here beyond what multi-select needs.

### Git intelligence

Recent commits follow the cycle "Add story → Add ATDD scaffolds → implement → Close the code review". 2.3's implementation (`d29402c`) touched 55 files: Domain rule + store port, one migration (+Designer, snapshot), Contracts (route constant, request, response field, problem types), Host endpoint + body reader, client `BoardView.razor.cs`/`TaskDetail.razor`/`TaskCard.razor`/`PickerOption.razor`/`components.css`/`ClientCopy.resx`/`RefusalCopy.cs`, gates in `tests/Yello.Tests.Architecture`, `IsolationCases.cs`, Slices partials of `TaskEndpointTests`, `TaskDetailTests`/`TextSpacingTests`/`FakeYelloApi`. Expect the same footprint plus a palette gate and a settings page.

### Ledger entries

- `deferred-work.md:36` — radius mapping: 2.4 adds the Label-chip 2px clause; update "still unmapped" (context bar and buttons, Role chip, Offer indicator, other dialogs unless 2.4 builds one, invitation view).
- `deferred-work.md:20` — text spacing: 2.4 adds the Labels case.
- `deferred-work.md:32` (closed) — `--radius-full` has exactly one consumer; chips must not be pills.
- `deferred-work.md:103` — migration hand-editing pass applies.
- `deferred-work.md:493` (owner 2.9) — Board read gains a Labels statement; note it.
- `deferred-work.md:509` (owner UX) — button radius prose 3px vs token 2px; the Labels surface's buttons inherit whatever `.button-*` say today.
- `deferred-work.md:515` (owner 2.8) — picker keyboard pattern; the Label picker inherits it.

### Project Structure Notes

- New: `Yello.Domain/Labels/{Label,LabelName,LabelPalette,ILabelStore}.cs`; `Yello.Application/Labels/{ListLabels,DefineLabel,DeleteLabel}/…Handler.cs`; `Yello.Infrastructure/Persistence/{LabelStore.cs,Configurations/LabelConfiguration.cs,Configurations/TaskLabelConfiguration.cs}`; one migration; `Yello.Contracts/Labels/{LabelRoutes,LabelResponse,CreateLabelRequest}.cs`; `Yello.Host/Endpoints/LabelEndpoints.cs`; `Yello.Client/Components/{LabelChip,LabelPicker?}.razor`, `Yello.Client/Pages/SpaceSettings.razor`, `Yello.Client/Spaces/LabelCapabilities.cs`; `tests/Yello.Tests.Architecture/{CieDe2000.cs,LabelPaletteGateTests.cs}`.
- Updated: `TaskEdit`, `TaskConfiguration` (AK), `TaskStore`, `EditTaskAttributesHandler`, `TaskEditBody`, `TaskEndpoints`/`BoardEndpoints`, `TaskResponse`, `EditTaskRequest`, `ProblemTypes`, `SchemaNames`, `YelloDbContext`, `SpaceContentStores`, `Program.cs`, `ClientRoutes`, `SpaceHome.razor`, `BoardView.razor(.cs)`, `TaskCard.razor`, `TaskDetail.razor`, `PickerOption.razor` (or a multi-select sibling), `components.css`, `tokens.css`, `ClientCopy.resx`, `RefusalCopy.cs`, the named gates, `IsolationCases.cs`, `FakeYelloApi.cs`, `TextSpacingTests.cs`, `TESTING-CONVENTIONS.md`.
- Variance: AR-3's "validator" files do not exist in the codebase convention; follow the code (Domain rules). The mockup `board.html` styles Label chips with the Role-chip colours in uppercase — the spine wins over the mock (`DESIGN.md:482`); a Label chip that looks like the Role chip blurs UJ-4.

### References

- `_bmad-output/planning-artifacts/epics.md:1097-1130` (story), `:113, :115` (FR-20, FR-22), `:246, :249, :251` (UX-DR8, 11, 13), `:189-228` (AR-n)
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md` — §2 Glossary "Label"; FR-16 matrix row "Manage Labels" (`:347`); FR-20; FR-22; NFR-8 (`:740-747`); §7 Space settings
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md` — core entities ER (`SPACE ||--o{ LABEL`, `TASK ||--o{ TASK_LABEL`, `LABEL ||--o{ TASK_LABEL`), AD-2, AD-3, AD-18, AD-20, AD-25, AD-29
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md:180-183` (label-chip tokens), `:451` (no cap), `:457-462` (fill rules), Do's and Don'ts; `EXPERIENCE.md:53` (IA: Space settings), `:211` (picker Label use)
- `_bmad-output/specs/spec-yello/SPEC.md:114, 120-121`; `acceptance-criteria.md#CAP-22`; `role-capability-matrix.md:18`; `glossary.md:23`
- `_bmad-output/test-artifacts/test-design-qa.md` (I-1, I-2, I-3, I-11, A-4, A-5, C-6, P-10, X-1; factories include Label at `:130`)
- `_bmad-output/implementation-artifacts/2-3-edit-a-tasks-title-status-and-due-date.md` (Q1–Q14, review findings, File List)
- `_bmad-output/implementation-artifacts/deferred-work.md:20, :32, :36, :103, :493, :509, :515`
- `tests/TESTING-CONVENTIONS.md`

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Story created 2026-10-25 by create-story: ultimate context engine analysis completed — comprehensive developer guide created. No isolation row was pending on 2.4 at creation. After moving 2.4 to `ready-for-dev`: `dotnet build Yello.slnx` 0 warnings, 0 errors; `IsolationCaseListTests` 14/14 passed (GATE-001..004 green). The palette table was computed in a scratch script during creation and must be re-derived by the gate the dev phase writes.

### File List
