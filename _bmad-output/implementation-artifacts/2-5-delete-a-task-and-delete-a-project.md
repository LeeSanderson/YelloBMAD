# Story 2.5: Delete a Task, and delete a Project

Status: ready-for-dev

Epic: 2 — Track your own work on a Board
Story key: `2-5-delete-a-task-and-delete-a-project`
Requirements owned (Story Coverage Index, `epics.md:358`): **FR-23** (Owner/Admin/Member delete a Task, a Viewer cannot; `epics.md:116`, `prd.md:427-433`, SPEC CAP-23), **FR-17's delete clause** ("create, rename **and delete** Projects … Deleting a Project deletes its Tasks, immediately and irreversibly"; `epics.md:107`, `prd.md:367-375`, SPEC CAP-17; create and rename were 2.1's), **UX-DR12** (the in-place `destructive-confirm`, one level deep, `Esc`, focus returned; `epics.md:250`), **UX-DR36** (the confirm ladder's Task and Project rungs; `epics.md:280`), **UX-DR37** (copy standard, never "archive"; `epics.md:281`), and **PRD §6.2** (no trash, no restore; `prd.md:769-774`, `epics.md:179` "Owned by Stories 2.5, 3.3, 5.4").
**Staged, not completed here:** FR-23's "deletion terminates any active collaborative editing session … participants are told it was deleted" completes in **story 7.4** (`epics.md:316, 389, 401`; `sprint-status.yaml` "STAGED FRs" header; readiness report issue 4). No editing session exists before Epic 7. **Do not mark FR-23 fully delivered when this story closes.**
Also binding: **AD-2 / AR-5, AR-6, AR-8** (RLS + independent query filter; composite FKs carrying `SpaceId`), **AD-3 / AR-9, AR-10, AR-11 / AD-20** (`{spaceId}` on every route; 404 = absent, byte-identical, floored; 403 recorded; an in-Space not-found is not recorded), **AD-4** (both surfaces), **AD-7** (anti-forgery on every state change), **AD-18 / AR-25** (`Idempotency-Key`, pipeline-only), **AD-15** (no sibling renumber), **AD-29** (counts are true totals), **AR-34** (RFC 9457 stable `type`, one transaction, mutation only through a slice, no content in logs), **AR-35** (isolation rows on both surfaces), **UX-DR26** (no optimistic UI on anything destructive; banned list), **UX-DR27** (absent, not disabled), **UX-DR39** (focus destinations), **NFR-5** (a retried write is not applied twice), **NFR-7** (refusals recorded), **NFR-9** (Board and Task editor are WCAG 2.1 AA flows).
SPEC: **CAP-17** (delete clause), **CAP-23** (request-path clause) (`specs/spec-yello/SPEC.md:101-103, 122-124, 208, 269`; `acceptance-criteria.md:174-179, 218-221`; `role-capability-matrix.md:12-13`).
Test design: no Task/Project-delete scenario id exists. Generic rows that name this story's routes: **I-1, I-2, I-4, I-5, I-6, I-13, A-4, C-6, P-10, V-1** (`test-design-qa.md:265-309`). **V-12** (session termination) is Epic 7's. Irreversibility is "Raises the value of the atomicity cases (S-1)" (`test-design-qa.md:73`; `test-design-architecture.md:396`). Project-irreversibility cases take `Assumption=PRD-12-5` (`prd.md:896`; R10).
Ledger entries owned by this story (`deferred-work.md`): **`:510`** (focus returns to the card, never to its column, when the panel closes — "2.5's delete is the first way a card disappears under an open panel"); the destructive-confirm clause of **`:36`** (6px for dialogs other than the panel — owed only if this story builds one); **`:20`**'s convention (each new surface adds its own text-spacing case).
Depends on: **2.1** (Projects, `ProjectList`/`ProjectRow`, rename), **2.2** (Tasks, Board, `FK_Task_Project_SpaceId_ProjectId` already `CASCADE`), **2.3** (Task detail panel, per-attribute replica, `TaskStore` lock order), **2.4** (`TaskLabel` with `TaskLabel→Task` `CASCADE`, `TaskLabel→Label` `NO_ACTION`; the in-place `destructive-confirm` in `LabelRow`; `SpaceWrites.SendWithoutBodyAsync`; `SpaceNarration`), all done.
Extended later, not here: **3.3** (Space deletion: the top of the ladder, typed-name confirm), **4.5** (Membership removal rung), **5.4** (Account deletion refused rung), **6.1** (Project settings — EXPERIENCE.md places Delete Project there; see Q1), **7.4** (session termination on Task delete), **7.7** (a Task deleted *by someone else* while open: retained-text panel, then the column).

> **Two isolation gates bind this story. Read both before writing a route.**
> 1. **1.9-GATE-002** (`IsolationCaseListTests.Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case`) fails the Isolation suite for any `/api` route the Host maps that no `IsolationCases` row names by exact `(Method, Route)`. **`DELETE TaskRoutes.Task` and `DELETE` on the Project item route each need rows with an expectation on both surfaces (`Session` and `ApiToken`) in the same change that maps them.** A row must actually send a request on the route it names (`CaseRun.cs:38`).
> 2. **1.9-GATE-003** (`Every_pending_row_names_a_story_that_has_not_yet_reached_review`) lets a row stay `PendingUntil: "2.5"` only while 2.5 is `backlog`, `ready-for-dev` or `in-progress`. Today no row is pending on 2.5 (grepped 2026-10-26). The ATDD phase may add some; every one must be live before the dev phase sets `review`.
>
> This create-story change moves 2.5 to `ready-for-dev`, which GATE-003 permits.

## Story

As someone tidying up,
I want to remove a Task or a whole Project and be told exactly what that destroys,
so that an irreversible action is never a surprise.

## Acceptance Criteria

AC1–AC6 are reproduced **verbatim** from `epics.md:1140-1164` (`status: final`), numbered by position. AC7–AC14 come from the PRD, the architecture, the UX spine, the ledger and the 1.9 gates; each cites its source.

**AC1 — delete a Task, confirmed in place**
**Given** a Task
**When** an Owner, Admin or Member deletes it
**Then** a confirm names that Task and states "This cannot be undone.", replacing its invoking panel's content in place rather than stacking on it
**And** a Viewer has no delete affordance present

**AC2 — delete a Project, its Task count named**
**Given** a Project
**When** an Owner, Admin or Member deletes it
**Then** a confirm names the Project **and its Task count**, and when that count is zero it says so — the friction and the reassurance being the same mechanism

**AC3 — immediate and irreversible**
**Given** a confirmed Project deletion
**When** it completes
**Then** the Project and every Task in it are destroyed immediately and irreversibly, with no trash and no restore window

**AC4 — copy carries the danger**
**Given** any destructive confirm
**When** it renders
**Then** the copy carries the destructiveness and the danger border is reinforcement only, never the reverse, because accent and danger are 1.19 apart and converge under deuteranopia
**And** it carries no icon and no illustration

**AC5 — no "archive"**
**Given** all copy in the product
**When** it is reviewed
**Then** the word "archive" appears nowhere, because Yello has no archive and borrowing the word from the products users arrive from would promise a safety net that does not exist

**AC6 — cancel destroys nothing**
**Given** a destructive confirm
**When** it is cancelled or dismissed
**Then** nothing is destroyed and focus returns to the invoking element

**AC7 — the API refuses a Viewer and deletes nothing else** *(FR-16 matrix `prd.md:341-342`, FR-23, FR-17, AR-10, AR-11, NFR-7; C-6, V-1)*
**Given** `DELETE /api/v1/spaces/{spaceId}/tasks/{taskId}` and `DELETE /api/v1/spaces/{spaceId}/projects/{projectId}`
**When** an Owner, Admin or Member sends one on either surface
**Then** it answers `204` with no body, and the Task (with its `TaskLabel` rows) or the Project (with every Task and every `TaskLabel` row of those Tasks) is gone in the same transaction — no other Task, Project, Label, Status or position key changes, and no sibling is renumbered (AD-15)
**And** a Viewer's request is `403 insufficient-role` with `capability` `manage-tasks` (Task) or `manage-projects` (Project), one `InsufficientRole` row is written, and nothing changes
**And** an id that is absent, already deleted, or a real id from another Space answers the byte-identical `404 not-available`, floored, with no refusal row (AD-3, AD-20)

**AC8 — the new routes are isolation rows on both surfaces, and each entity's own two layers are proved** *(1.9-GATE-001/002/003, AR-35, AD-3, AD-4; I-1, I-2, I-13)*
**Given** the two `DELETE` routes
**When** the isolation suite runs
**Then** each is named by rows with an expectation for **both** `Session` and `ApiToken`: a foreign Space's Task or Project is unreachable (boundary `404`, one `CrossSpace` row, nothing deleted in the foreign Space); a foreign Task or Project id used inside one's own Space answers the same bytes as a random id with no refusal row and nothing deleted — and each such row **fails** when that entity's query filter and FILTER predicate alone are dropped, proved by a plant
**And** no isolation row names 2.5 as pending when 2.5 reaches `review`

**AC9 — deletes are idempotent** *(AD-18, AR-25, NFR-5; P-10)*
**Given** a Task or Project deletion carrying an `Idempotency-Key`
**When** it is replayed
**Then** the original `204` is returned without re-applying it (the replay is **not** a `404`); the same key on a different path is `422 idempotency-key-reused`; a resend without a key after a `204` is the `404` of AC7

**AC10 — deletes cannot race into a 500, a deadlock or a dangling row** *(AR-34; TESTING-CONVENTIONS deterministic-race rule; CLAUDE.md "Race tests must be deterministic"; 2.3 review "a Task removed between the first read and the reload under the lock")*
**Given** a deletion in flight together with a Task creation in the same Project, a Status edit or Labels edit of the same Task, a Label deletion whose last application belongs to the Task, or the creation of a Task in a Project being deleted
**When** both commit
**Then** each answers one of its own documented outcomes (`201`/`200`/`204`/`404`/`409` as listed in Task 7) — never a `500`, never a deadlock victim (1205), never a Task whose Project is gone and never a `TaskLabel` whose Task is gone — proved by cases that hold the lock and wait on `LCK_M_%` in `sys.dm_exec_requests`, not by firing requests together

**AC11 — the confirm, the wait and the result on the Board** *(UX-DR12, UX-DR26, UX-DR36, UX-DR39, UX-DR40; `EXPERIENCE.md:67, 207, 266, 335, 401-403`; DESIGN.md:476, 519; `deferred-work.md:510`)*
**Given** the Task detail panel open for an Owner, Admin or Member
**When** "Delete Task" is chosen
**Then** the panel's content is replaced in place by the confirm (still one `[role=dialog]`, focus trapped, focus moved into the confirm), which names the Task by its title and says "This cannot be undone." with exactly one `button-danger` and one `button-secondary`
**And** the deletion is **not optimistic**: while the `DELETE` is held the card and the panel remain; on `204` the panel closes, the card leaves its column, the column's true total drops by one, "Task deleted." is announced politely, and focus moves to **the column that held the Task** — never to `<body>`, never to an adjacent card
**And** `Esc` or the secondary control cancels: the panel's content returns and focus returns to the "Delete Task" control
**And** a refusal (`403`, `409`, `422`) is said in the confirm's own status region and nothing is removed; no answer says "Not sent." and keeps the key for an identical resend; a `404` follows the Board's existing 404 pattern

**AC12 — the Project confirm and where focus lands** *(UX-DR12, UX-DR36, UX-DR39; `EXPERIENCE.md:268, 403, 555-566`; Q1, Q2)*
**Given** the Project delete control (on the surface Q1 decides) for an Owner, Admin or Member
**When** it is chosen
**Then** the confirm replaces the invoking control's container in place, names the Project, states its Task count as a true total read when the confirm opens (AD-29) — "It holds no Tasks." when zero, singular and plural forms otherwise — and "This cannot be undone."
**And** the deletion is not optimistic; on `204` the Project leaves the listing, "Project deleted." is announced, and focus moves to the nearest surviving container (Q3); on cancel nothing is destroyed and focus returns to the delete control

**AC13 — the confirm is drawn from the design system** *(UX-DR4, UX-DR5, UX-DR12; `DESIGN.md:277-284, 360, 468, 476, 519`; `deferred-work.md:36`)*
**Given** every `.destructive-confirm` (the Task's, the Project's, and 2.4's Label confirm)
**When** the design gates run
**Then** it carries the `danger` border at the emphasis width (reinforcement), `button-danger` for the destructive action and `button-secondary` beside it, no `img`, `svg` or icon glyph, no animation or transition, and its copy contains the externalised "This cannot be undone." sentence — gated, each with a plant
**And** under the WCAG 1.4.12 override and 200% text-only zoom no confirm text is clipped or overlapped (`deferred-work.md:20`)

**AC14 — absent for a Viewer, gated against the matrix** *(UX-DR27; `epics.md:1887`)*
**Given** a Viewer on the Board, in Task detail and on the Project surface
**When** they render
**Then** no delete control is present (not disabled, not hidden by CSS), and `main [disabled], main [aria-disabled='true'], [role=dialog] [disabled]` count 0; the client's delete predicates are held to `RoleCapabilityMatrix` by the existing gate pattern, each with a plant

## Tasks / Subtasks

- [ ] **Task 1 — Confirm the gates, then decide the open questions** (AC: all)
  - [ ] `dotnet build Yello.slnx`, then `dotnet test --project tests/Yello.Tests.Isolation --no-build` filtered to `IsolationCaseListTests`. With 2.5 `in-progress` it is green. List every row with `PendingUntil: "2.5"` (the ATDD phase may add some) in the Debug Log; each is converted before `review`.
  - [ ] Read the open questions (Dev Notes). Decide Q1–Q6 before Task 2, Q7–Q10 before Task 8, Q11–Q13 before Task 9. Record each in the Completion Notes as "**Qn - (x).** … Cost to later stories: …". Each is reversible.

- [ ] **Task 2 — Domain ports and slices** (AC: 3, 7)
  - [ ] `ITaskStore.DeleteAsync(Guid taskId, CancellationToken)` and `IProjectStore.DeleteAsync(Guid projectId, CancellationToken)` returning a typed outcome, mirroring `ILabelStore.DeleteAsync` / `LabelDeletion { Deleted, NotFound, InUse }` (`Yello.Domain/Labels/ILabelStore.cs:36-58`): `TaskDeletion { Deleted, NotFound }`, `ProjectDeletion { Deleted, NotFound }`. No `Where(SpaceId == …)`: both layers narrow; a foreign id is simply not found.
  - [ ] Slices, one handler file each (AR-3, the `DeleteLabelHandler` shape — a pass-through with remarks "No Role, no Space"): `Yello.Application/Tasks/DeleteTask/DeleteTaskHandler.cs`, `Yello.Application/Projects/DeleteProject/DeleteProjectHandler.cs`. **No handler reads the Role, a capability, a count for a bound, or `Idempotency-Key`** (the pipeline-only gates catch each).
  - [ ] If Q2 chooses a server-side count for the Project confirm, it is a read (`ReadProjectsAndTasks`), in its own slice or on an existing read — never computed by a delete handler.

- [ ] **Task 3 — Stores, with a lock order that fits every existing writer** (AC: 3, 7, 10)
  - [ ] **Read the lock-order remarks first**: `TaskStore.cs:21-46` (edit: Project Task range → Task row `INDEX(1)` → Labels in id order → TaskLabel), `LabelStore.cs:24-32` (Label `INDEX(1)` → TaskLabel), `BoundCounter.cs:46-62` (creation: `COUNT(*) … WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = @p`, the same Project range). Raw SQL only in Infrastructure, parameterised `FromSql`/`ExecuteSql` composed on the filtered `DbSet` (keeps the query filter; RLS still applies).
  - [ ] **`TaskStore.DeleteAsync`** (recommended order, to be confirmed by Task 7's races — Q5):
    1. Read the Task's `ProjectId` (plain read; none → `NotFound`).
    2. Take the Project's Task range exactly as creation and the Status edit do: `SELECT … FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = {projectId}` (same statement shape as `LastKeyUnderTheProjectLockAsync`/`BoundCounter`, so the same index and the same range).
    3. Lock the Task row `WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = {taskId}` (the edit's Task lock, same clustered key — the 2.4 deadlock was two writers locking one row through different indexes). Gone → `NotFound` (the row vanished between 1 and 3: **never** `Deleted`, never `500`).
    4. `ExecuteDeleteAsync` on the Task; the database's `TaskLabel→Task` `CASCADE` removes its applications. 0 rows → `NotFound`.
    **Why the range first:** a Status edit holds the Project range `U` and then wants the Task row; a delete that took the row first and then needed the nonclustered `(SpaceId, ProjectId, StatusId, PositionKey)` key inside that range would deadlock against it. Record the measured order in `TaskStore`'s remarks.
  - [ ] **`ProjectStore.DeleteAsync`**:
    1. Lock the Project row `WITH (UPDLOCK, HOLDLOCK, INDEX(1))`; none → `NotFound`.
    2. Take the Project's Task range `WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = {projectId}` — **this is what serialises the delete against a Task creation in that Project**: creation counts under that range in the pipeline (`ConsultsBound(Bound.TasksPerProject)`), then reads the Project, then inserts with the composite FK. Without the range lock, a creation that has read the Project can insert after the delete commits → FK 547 → `500`.
    3. `ExecuteDeleteAsync` on the Project; `FK_Task_Project_SpaceId_ProjectId` (`CASCADE`) removes its Tasks and `TaskLabel→Task` (`CASCADE`) their applications. Labels are per Space and **stay**.
    Whether 1 and 2 must be the other way round (creation takes Task range then Project `S` for the FK check; a delete holding the Project `U` then asking for the range could meet it) is exactly what RACE-P1 measures — Q5. Pick the order the races prove; record it in the remarks of `ProjectStore`, `TaskStore` and `BoundCounter`.
  - [ ] Catch 547 (`SqlException`/`DbUpdateException`, `LabelStore.HasError` pattern) only if a race proves it reachable, and answer a documented outcome, never `500`.
  - [ ] **Do not add a `BEFORE DELETE` block predicate** unless Q6 decides so: the FILTER predicate already makes another Space's row invisible to `DELETE`, every cascade runs along composite FKs carrying `SpaceId` (it cannot leave the Space), and `SpaceIsolationSchemaTests.MovableTablePredicates = 3` pins three predicates on Project, Task, Label and TaskLabel. **No migration is expected in this story.**
  - [ ] **Lock escalation on a large Project** (measure, do not fix): deleting a Project holding ~5,000 Tasks plus their applications takes more than SQL Server's ~5,000-lock escalation threshold in one statement and may escalate to a table lock on `Task`/`TaskLabel` — every Space's Task traffic waits for its duration. Measure the duration with the existing 4,999-Task seed helper in the Slices suite (2.2's `TaskEndpointTests.Bound` seeds) and record it in the Debug Log; ledger it to **2.9** (owner of the 5,000-Task Board and load). Atomicity wins: no batching that could leave half a Project.

- [ ] **Task 4 — Contracts** (AC: 7, 9)
  - [ ] `ProjectRoutes`: add `Project = "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}"` and make `Rename => Project` (one template, two names; the route table and gates compare the template). Use `ProjectRoutes.Project` for the DELETE map and its isolation rows. `TaskRoutes.Task` already exists.
  - [ ] **No new problem type is expected** (`not-available`, `insufficient-role`, the idempotency types exist). If Q2 adds a count read, its response type lives in `Yello.Contracts.Projects` and carries no `SpaceId`. If Q4 chooses a `409`, its type gets remarks stating what it never means, a `RefusalCopy` entry and copy.
  - [ ] No body on either `DELETE` (2.4 decided "DELETE carries no body"; `SpaceWrites.SendWithoutBodyAsync`).

- [ ] **Task 5 — Endpoints and the gates that break on purpose** (AC: 7, 9)
  - [ ] `TaskEndpoints.MapDeleteTask`: `MapDelete(TaskRoutes.Task, DeleteAsync).RequiresCapability(Capability.ManageTasks)`; `ProjectEndpoints.MapDeleteProject`: `MapDelete(ProjectRoutes.Project, …).RequiresCapability(Capability.ManageProjects)`. Copy `LabelEndpoints.DeleteAsync` (`LabelEndpoints.cs:138-154`): `Deleted` → `Results.NoContent()`; `NotFound` → `BoundaryProblems.NotAvailable()` (not recorded). **No `ConsultsBound`**, not `SessionOnly`, no `Results.Created`/`Accepted`. Register handlers (`Program.cs:92-109`) and maps beside their siblings (`Program.cs:382-405`), before `MapUnmatchedApi()`.
  - [ ] Logs carry `SpaceId` and ids only — never a Task title or Project name (AR-34, I-17).
  - [ ] **Gates that go red by design — amend deliberately, by exactly this, never loosen** (each named by the research for this story):
    1. `RefusalBoundaryGateTests.Projects.cs` `No_route_moves_a_Project` (l.107-126) — its own doc says "Story 2.5 adds DELETE on the Project and widens the permitted set by exactly that."
    2. `RefusalBoundaryGateTests.Tasks.cs` `The_routes_under_a_Project_are_its_rename_its_Task_creation_and_its_Board` (l.175-194) — add exactly `DELETE {ProjectItemPattern}`; rename the test if its name stops being true.
    3. `RefusalBoundaryGateTests.TaskEdit.cs` `The_Task_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound` (l.127-140) — `["GET","PATCH"]` becomes `["DELETE","GET","PATCH"]`; DELETE declares `manage-tasks`, both surfaces, no bound.
    4. `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.cs` `An_unmapped_method_on_a_Project_route_answers_404_with_no_Allow_header` (l.545-570) sends **DELETE** and expects `404` — retarget it to a method still unmapped (e.g. `PUT`), keeping its purpose.
    5. `TaskEndpointTests.LabelsSchema.cs` `TaskLabel_reaches_Task_and_Label_only_through_keys_that_carry_SpaceId` already pins `Task|CASCADE` — keep it green; it is now load-bearing.
    Also must stay green unchanged: `Every_spaceId_endpoint_declares_exactly_one_capability`, `Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay` (a bodiless `204` replays, `IdempotentResponses.cs:129-141, 173-188`), `No_endpoint_answers_through_Results_Created_or_Accepted`, `No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity`.

- [ ] **Task 6 — Idempotency** (AC: 9)
  - [ ] Nothing new in the pipeline (`IdempotentResponses.IsStateChanging` already includes DELETE). Wire cases on **both** surfaces for each route: same key twice → one deletion, second answer `204` (not `404`); same key on another Task's path → `422 idempotency-key-reused`; no key, second send → `404 not-available`, no refusal row.

- [ ] **Task 7 — Slice, schema and race tests** (AC: 3, 7, 9, 10)
  - [ ] **Memory-bound machine (CLAUDE.md "Test memory budget"): add no container-backed class.** New partials of the existing classes: `TaskEndpointTests.Delete.cs`, `.DeleteRace.cs`, `.DeleteIdempotency.cs`; `ProjectEndpointTests.Delete.cs`, `.DeleteRace.cs`, `.DeleteIdempotency.cs` (each class keeps its one database on the project's one engine). Reuse `TaskWire`/`TaskEditWire`/`LabelWire` (`LabelWire.DeleteAsync`/`DeleteWithTokenAsync`/`DeleteOnAsync`, `SendWithHeadersAsync(HttpMethod.Delete, …, content: null, …)` is the shape), `HostProcess`, `WireSession.DeleteWithTokenAsync`. Handler unit tests (`Tasks/DeleteTask/DeleteTaskHandlerTests.cs`, `Projects/DeleteProject/DeleteProjectHandlerTests.cs`) use hand fakes, no database (`DeleteLabelHandlerTests` pattern).
  - [ ] Cases (assert the problem `type`, not only the status; every `4xx` asserts "no refusal row" unless it is a `403`/boundary `404`):
    - Owner, Admin, Member delete a Task → `204`; `GET` the Task → `404`; the Board no longer lists it and its column's `Total` dropped by one; its `TaskLabel` rows are gone; its Labels still list; sibling position keys unchanged (`SELECT` before and after).
    - Owner, Admin, Member delete a Project holding Tasks in several Statuses with Labels applied → `204`; the listing omits it; `SELECT COUNT(*)` of its Tasks and of their `TaskLabel` rows is 0 (read with the Space's session context — the rows are gone, not hidden); another Project's Tasks and every Label are untouched. Tag `Assumption=PRD-12-5`.
    - A Label whose only application was on a deleted Task is now deletable (`204`), closing 2.4's `409 label-in-use` loop.
    - Viewer → `403 manage-tasks` / `403 manage-projects`, one `InsufficientRole` row, nothing deleted, both surfaces.
    - Random id, deleted id, a real id from another Space → byte-identical `404 not-available`, no refusal row.
    - Delete frees bound capacity: a Project at 5,000 Tasks refuses the 5,001st; delete one; the next creation is `201`. Same for 50 Projects per Space (reuse the existing `.Bound` seeds; no new seed of 5,000 unless one exists).
    - Schema: `FK_Task_Project_SpaceId_ProjectId` is `CASCADE` and carries `SpaceId` (pin it, as `.LabelsSchema.cs` pins `TaskLabel`).
  - [ ] **Deterministic races** with `TaskWire.WaitUntilBlockedOnALockAsync` (`TaskWire.cs:347-370`) or `LabelWire.WaitUntilBlockedOrAnsweredAsync` (`LabelWire.cs:199-217`). The test holds the competing change in its own open transaction (`LabelWire.InsertApplicationAsync`/`HoldTaskLabelsAsync`/`DeleteLabelRowAsync` are the raw-SQL holders), sends one request, waits for `LCK_M_%`, then commits or rolls back. **Never `Task.Delay`, never two requests fired together.** Expected outcomes:
    - **RACE-T1** Status edit holding the Project range vs Task delete → delete waits; edit commits; delete `204`.
    - **RACE-T2** Task delete holding its locks (held after step 3) vs Status edit of the same Task → edit waits; delete commits; edit `404` (2.3's review patch: never `200` on a vanished Task).
    - **RACE-T3** Task delete vs a Labels-only edit of the same Task, both orders → `204` + `404`, or `200` + `204`; never a `TaskLabel` row left for a gone Task, never 1205.
    - **RACE-T4** Label delete racing the Task delete that removes its last application (test holds the Task deletion uncommitted) → Label delete waits; commit → `204`; roll back → `409 label-in-use`.
    - **RACE-T5** Task creation in the same Project holding the bound's range vs Task delete → both succeed.
    - **RACE-P1** Task creation in a Project holding the bound's range vs Project delete → delete waits; creation commits; delete `204` and takes the new Task with it. And the reverse: Project delete held → creation `404 not-available` (the Project is gone), **never `500`/547**.
    - **RACE-P2** Status edit of a Task in the Project vs Project delete → `200` then `204`, or `204` then `404`.
    - Plants (three runs each, record `n/3`): each `UPDLOCK, HOLDLOCK` removed; `INDEX(1)` removed; the delete's range lock removed (RACE-P1 must catch the `500`). If a plant is masked (2.4 plant 9 was, by the `NoAction` backstop), record why rather than deleting the case.

- [ ] **Task 8 — Isolation rows** (AC: 7, 8)
  - [ ] Add the rows in "Isolation rows" (Dev Notes) via `Both(…)`, each sending a request on the exact route constant it names, seeded through the product (`ProductActor`; `SeedProjectWithTaskAsync`; `ViewerEditsATaskAsync`/`ViewerCreatesAProjectAsync` for the Viewer seeds, `IsolationCases.cs:618, 707`). `Then` callbacks assert the foreign row is still present (`TheForeignLabelIsStillPresentAsync` pattern, `IsolationCases.cs:406-434`). Every Project name and Task title a row writes goes in the world's secret set (`HoldsProjectNamed`).
  - [ ] Two-layer plants, one at a time, original held in memory and restored in a `finally`: Task — `IgnoreQueryFilters()` on the delete's reads **and** the `Task` FILTER predicate removed → `ISO-DELETE-TASK-FOREIGN-ID-IN-OWN-SPACE` fails on both surfaces; each layer alone → green. Project — the same with the `Project` layers → `ISO-DELETE-PROJECT-FOREIGN-ID-IN-OWN-SPACE` fails. (If the plant requires editing a migration, follow 2.3's P6 and 2.4's plant 3: plant in the migration file, rebuild, restore.)
  - [ ] No new timing row: the routes are under the API group, so `NotAvailableFloor` applies. If one is added, it runs in `TimingCollection`.
  - [ ] GATE-003: no row pending on 2.5 at `review`.

- [ ] **Task 9 — Client: the Task delete in the panel** (AC: 1, 4, 6, 11, 13, 14)
  - [ ] `TaskDetail.razor` (its header says "no delete (2.5)" — update it): for `CanManage` only, a `button-secondary` "Delete Task" control as the panel's last control (after the attributes; Q8). **Update `FocusLastAsync`** (l.370-388) — the last control changes. Viewer branch: no control (absent, not disabled).
  - [ ] **The confirm replaces the panel's content in place** (UX-DR12): a `_confirming` state swaps the panel body for `<div class="destructive-confirm">` holding a heading/sentence naming the Task (`"Delete {title}?"`), the sentence "This cannot be undone.", `button-danger` "Delete Task" and `button-secondary` "Cancel". Still one `[role=dialog]`, same focus-trap sentinels, focus moves to the confirm's first control (Q9: recommend **Cancel**, because neither choice may be a default in a dialog — UX-DR12 `button-secondary` "for a dialog where neither choice may be a default"). `Esc` in the confirm cancels the confirm first (2.3 Q9: innermost meaning wins), a second `Esc` closes the panel.
  - [ ] **Not optimistic** (UX-DR26): `BoardView.razor.cs` gains `DeleteTaskAsync(taskId)` sending `Writes.SendWithoutBodyAsync(HttpMethod.Delete, TaskRoutes.TaskOf(SpaceId, taskId), key, token)` with a key that is **reused on an identical resend after no answer** (2.3 Q8 rule; 2.4's Label delete took a fresh key per click and earned `deferred-work.md:530`). While held, a busy state on the danger control (`aria-busy`, not `disabled` — no disabled state anywhere is the safest reading of UX-DR27; record Q10). Answers:
    - `204`: drop the Task from its `ColumnState` (mirror `Add`, `BoardView.razor:340-352`: remove, `Total--`), drop its `TaskReplica` and any queued edits, close the panel **without** the card focus request, announce "Task deleted." in the Board's live region (`BoardView.razor:49`), and move focus to the column that held the Task (Q3).
    - Edits still in flight for that Task when the `204` lands: their later answers (`404`) must **not** raise the Space refusal surface — mark the id as deleted by this tab and drop answers for it (Q7).
    - `403`/`409`/`422`: `RefusalText.Of(…)` in the confirm's status region; nothing removed.
    - `404`: the Board's existing pattern (`UseAnswerAsync` l.284-296 → `OnRefused`). This is the same indistinguishable-404 trade-off ledgered for Labels (`deferred-work.md:530`, owner 4.4); add this case to that entry rather than solving it here.
    - No answer / `5xx`: "Not sent." in the confirm, key kept.
  - [ ] **Focus to the column (closes `deferred-work.md:510`)**: `BoardColumn` gains a focusable target (recommended: the column's `<section>`/heading with `tabindex="-1"`, never in the tab order) and a `FocusRequest`-style parameter like `TaskCard`'s (`_focusCardRequests` pattern; keep the 2.3 review fix — clear a stale focus request so a re-created card never steals focus). Close the ledger entry with what was built; `Close()` still returns focus to the card when it exists.
  - [ ] Purge (UX-DR31): the confirm state and its title are panel state; `PurgePanel()` clears them.

- [ ] **Task 10 — Client: the Project delete** (AC: 2, 4, 6, 12, 13, 14)
  - [ ] Build it on the surface Q1 decides (recommended: `ProjectRow` on Space home, beside Rename, gated by `ProjectCapabilities.PermitsManageProjects`). The confirm replaces the row's content in place (the `LabelRow` pattern, `LabelRow.razor:18-35`; state in `ProjectList` as `LabelSection` holds `_confirming`/`_deleting`/`_rowRefusal`, l.128-130, 347-414). Only one row confirms at a time; opening rename on another row cancels it.
  - [ ] **The Task count** (Q2): read when the confirm opens, as a true total; the confirm waits for it before offering the danger control (a confirm that cannot state the count does not offer deletion). Copy forms: zero, one, many — externalised, never concatenated.
  - [ ] Not optimistic; key reused on identical resend after no answer. `204`: remove the row, announce "Project deleted." in `ProjectList`'s live region (l.41), focus per Q3 (when the last Project goes, the "No Projects yet." empty state renders with its one action). `404`: `OnSpaceRefused` (existing). Others: inline in the row.
  - [ ] A Board open in another tab on a deleted Project answers its next read with `404` and shows the refusal surface — existing behaviour, nothing to build; note it.

- [ ] **Task 11 — Copy, the "archive" gate, the confirm's design gates** (AC: 4, 5, 13)
  - [ ] `ClientCopy.resx` only, sentence case, every entry with a `<comment>`, no copy quoted in a code comment (a 2.3 run tripped `No_user_visible_string_literal_appears_in_a_component` on that): "Delete Task", "Delete {0}?", "This cannot be undone." (one key, reused by every confirm — the Label confirm's `LabelDeleteWarning` may be refactored onto it), "Delete Project", "It holds no Tasks.", "It holds 1 Task.", "It holds {0} Tasks.", "Task deleted.", "Project deleted.", "Cancel" (reuse an existing key if one fits). No "Are you sure", no exclamation, no apology (`EXPERIENCE.md:106-113`).
  - [ ] **New gate `No_copy_uses_the_word_archive`** (Architecture suite, beside `LocalisationGateTests`): case-insensitive, word-prefix match (`archiv`) over `ClientCopy.resx` values **and** comments, every `.razor`/`.cs` under `Yello.Client`, `Yello.Contracts/ProblemTypes.cs` titles, and the Host's problem titles. Plant "Archive" in one resx value → caught; restore in a `finally`.
  - [ ] **Design gates** (`DesignFoundationGateTests`, a new partial `.DestructiveConfirm.cs`): `.destructive-confirm` declares `border: var(--emphasis-width…) solid var(--danger)` (use the actual border-width token name in `tokens.css`) — the current rule (`components.css:1106-1111`) has **no** border, so this changes 2.4's Label confirm too, deliberately; no `animation`/`transition` on it or its descendants (DESIGN.md:519); no `box-shadow`. A browser case asserts each confirm has no `img`, `svg`, `[class*=icon]`, contains the "This cannot be undone." copy, and has exactly one `.button-danger`. Plants: border removed; an `svg` added; a transition added. If the confirm is styled as a dialog surface (it is not a new dialog — it lives in the panel or a row), no 6px clause is owed (`deferred-work.md:36`); state which in the ledger update.
  - [ ] **Capability gates**: a `TaskCapabilities`/`ProjectCapabilities` predicate drives each delete control (reuse, do not add a predicate if the existing ones are exact — `ManageTasks` and `ManageProjects` are the delete capabilities; no new capability). The existing `…predicate_agrees_with_the_capability_matrix` gates hold them.

- [ ] **Task 12 — Browser tests** (AC: 1, 2, 4, 6, 11, 12, 13, 14)
  - [ ] `tests/Yello.Tests.Client` against `FakeYelloApi` — add `DELETE TaskRoutes.Task` and `DELETE ProjectRoutes.Project` (`FakeYelloApi.cs:400-460` switch; `DeleteLabel` fake at l.913-937 is the model), removing from the fake Board/Projects so a re-read agrees; a hold/release for the deletes (`TaskCompletionSource`, the `HoldTaskEditAsync` pattern); the count read if Q2 adds one. Same `ClientSurfaceCollection`; no new fixture. Bodies are `Yello.Contracts` types.
  - [ ] Cases: the Task confirm replaces the panel in place (exactly one `[role=dialog]`, panel attributes no longer in it, title named, "This cannot be undone."); **held** `DELETE` → card and panel still present; release `204` → card gone, column total −1, "Task deleted." in the live region, `document.activeElement` is the column target (not `body`, not a card); `Esc` and Cancel → content back, focus on "Delete Task", nothing sent (the fake recorded no DELETE); `403`/`409`/`422`/no answer → copy and nothing removed; an edit in flight answered `404` after the delete's `204` raises no refusal surface.
  - [ ] Project: confirm names the Project and "It holds no Tasks." for an empty one, "It holds 3 Tasks." for three, in place in the row; cancel restores and focuses the delete control; `204` removes the row, announces, focus per Q3; deleting the last Project shows "No Projects yet.".
  - [ ] Absence: Viewer sees no "Delete Task" in the panel and no Project delete control; the disabled-selector count is 0. Plants: a Viewer offered each control → caught.
  - [ ] `TextSpacingTests`: the Task confirm in the panel and the Project confirm in its row under the 1.4.12 override and 200% text-only zoom (`deferred-work.md:20`).
  - [ ] Never Playwright's URL wait; poll `location.pathname`. No `RegexOptions.CultureInvariant`. **Rebuild `Yello.Client` in Debug before a browser plant** (the suite's dev server runs `--no-build` against it), or the plant looks uncaught.

- [ ] **Task 13 — Plants, runs, ledger, status** (AC: all)
  - [ ] Plants one at a time in the foreground; original held in memory and restored in a `finally`; compare the tree hash after each. **Never** `git checkout --`, `git restore`, `git reset` or `git stash`. Write each plant so it compiles. Record `| # | Plant | Gate | Caught |` in the Debug Log. Minimum set: a row left `PendingUntil: "2.5"` at `review` (GATE-003); a DELETE route named by no row (GATE-002); each two-layer drop and each layer alone; each lock hint (3 runs); `ConsultsBound` on a delete route (ARCH-004 / Host start); a Viewer offered each delete control; "Archive" in copy; the confirm's border removed; an `svg` in the confirm; an optimistic removal (card removed before the `204`) → the held-`DELETE` case catches it; focus left on `body` after delete.
  - [ ] Runs (CLAUDE.md): `dotnet build Yello.slnx` (0 warnings, 0 errors), `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. **Baseline after the 2.4 review: 989 total, 986 passed, 0 failed, 3 skipped** (the 3 are the bundle cases, `YELLO_MIGRATION_BUNDLE` unset). Never two `dotnet test` processes at once; iterate one project with `--filter`. If a Client case fails only in the full run, run `dotnet test --project tests/Yello.Tests.Client --no-build` alone before calling it real. Rerun once on an engine start failure or a low-memory kill. If `Yello.Host.exe` processes lock `bin/Debug`, check `Get-Process Yello.Host` (2.4 had to build in Release; the browser suite still needs a Debug `Yello.Client`).
  - [ ] `tests/TESTING-CONVENTIONS.md`: the delete race pattern (which side holds what), the new counts.
  - [ ] Ledger (`deferred-work.md`): CLOSE `:510` (focus to the column, built). UPDATE `:36` (the destructive confirm's border mapping; whether a 6px clause was owed), `:20` (confirm cases added), `:530` (the Task delete's `404` shares the trade-off), `:492` (a note: deletion places no key and renumbers nothing; its lock order). ADD with owners: Project-delete lock escalation on a large Project (owner **2.9**); Delete Project's home once Project settings exists (owner **6.1**, Q1); any Q decided against the UX spine (owner **UX**).
  - [ ] `sprint-status.yaml`: set `review` at the end of the dev phase. **Do not mark FR-23 complete anywhere**; its session clause is 7.4's. **Do not add review narration to source files.**

## Dev Notes

### The shape of the story in one paragraph

On the server this is two `DELETE` routes and two stores through a pipeline that already resolves the Space, checks `manage-tasks`/`manage-projects`, records refusals, replays keys and floors not-founds; the schema already cascades Project → Task → TaskLabel (`CASCADE` on both composite FKs, `NO_ACTION` from TaskLabel to Label), so **no migration is expected**. Three things carry the risk. **(1) Lock order.** Every existing Task writer — creation (bound count), Status edit, Labels edit, Label delete — takes its locks in a documented order, and 2.3 and 2.4 each found a race the first draft got wrong (2.3: a column range could deadlock against the creation's Project-wide scan; 2.4: a real 1205 because two readers locked one Label through different indexes, fixed by `INDEX(1)`). A delete is a new writer on the same rows; a Project delete is a new writer on rows a creation is about to reference. Task 3 states a recommended order and Task 7 proves it. **(2) The first destructive confirm inside the modal panel**, with a focus destination that does not exist yet (the column, `deferred-work.md:510`) and a non-optimistic write in a client whose every other Task write is optimistic. **(3) Where Project deletion lives**: the UX spine puts it in Project settings, which is Owner/Admin and does not exist until 6.1, while FR-17 grants it to Members (Q1).

### What exists today (read before writing)

- **There is no `src/` folder.** Production projects sit at the repo root; tests in `tests/`.
- **Nothing deletes a Task or a Project** (verified: no route, handler, store member or problem type). The word "archive" appears nowhere in product code, tests or docs (only under `.claude/skills` and `_bmad`, which are tooling and out of scope for AC5's gate).
- **Delete precedent — Labels (2.4)**: `LabelEndpoints.MapDeleteLabel` (l.56-58) / `DeleteAsync` (l.138-154) → `204` / `409` / `BoundaryProblems.NotAvailable()`; `DeleteLabelHandler` is a pass-through; `LabelStore.DeleteAsync` (l.89-130) locks with `FromSql($"SELECT * FROM dbo.[Label] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = {labelId}")`, probes `TaskLabel` under `UPDLOCK, HOLDLOCK`, then `ExecuteDeleteAsync`, catching 547.
- **Task (2.4 state)**: `ITaskStore` = `CreateAsync`, `FindAsync`, `EditAsync`, `ReplaceLabelsAsync`, `LabelIdsOfAsync`. `TaskStore.ApplyAsync` (l.120-166) loads a tracked Task; `ReplaceAsync` (l.180-233) locks the Task `WITH (UPDLOCK, HOLDLOCK, INDEX(1))` (l.186) then Labels (l.206) then `RemoveRange` of TaskLabel (l.222); `LastKeyUnderTheProjectLockAsync` (l.241-244) is `SELECT MAX(...) FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = {projectId}`. 2.3's review patched "a Task removed between the first read and the reload under the lock is answered `200` (reachable once 2.5 deletes)" — RACE-T2 is where that patch is first exercised for real.
- **Project (2.1 state)**: `IProjectStore` = `CreateAsync`, `RenameAsync` (returns `null` when not visible), `ListAsync`; `ProjectStore.RenameAsync` (l.45-61) is one `ExecuteUpdateAsync`. `ProjectResponse(Guid Id, string Name, DateTimeOffset CreatedAt)` — **no Task count**. `BoardResponse(BoardProjectResponse Project, IReadOnlyList<BoardColumnResponse> Columns)` with `BoardColumnResponse.Total` a true per-Status total (AD-29), so the Board already knows its Project's true Task count as the sum of `Total`s.
- **FKs** (no migration needed): Project→Space `CASCADE`; Task→Project `(SpaceId, ProjectId)` `CASCADE` (`TaskConfiguration.cs:46-50`, `20261025115412_TaskTable.cs:70-74`); Task→StatusDefinition `NO_ACTION`; TaskLabel→Task `CASCADE` (`TaskLabelConfiguration.cs:47-51`, remarks l.19-23 already say "Deleting a Task removes its applications (story 2.5)"); TaskLabel→Label `NO_ACTION`; Label→Space `CASCADE`. Alternate keys `(SpaceId, Id)` on Task and Project.
- **RLS**: `dbo.fn_SpaceIsolationPredicate(SpaceId)` FILTER + `BLOCK AFTER INSERT` + `BLOCK AFTER UPDATE` on Project, Task, Label, TaskLabel; **no `BEFORE DELETE`**. The query filter reads `context.ScopedSpaceId`; `SESSION_CONTEXT` is set once per unit of work (`SpaceSessionContext.cs:111`). No SaveChanges interceptors. `ExecuteDelete` leaves cascades to the database (no navigations).
- **Pipeline** (do not reimplement): `ActiveSpaceResolution.ServeAsync` (l.210-251): presence → `BeginIdempotentAsync` → `ConsultCreationBoundAsync` (only with `.ConsultsBound`) → endpoint → commit on success. Role refusal records `InsufficientRole` and answers `BoundaryProblems.InsufficientRole(capability)` (l.186-197). Idempotency key optional; a bodiless `204` is recorded and replayed (`IdempotentResponses.cs:129-141, 173-188`).
- **Capabilities**: `ManageProjects` and `ManageTasks` are `[Yes, Yes, Yes, No]` (`RoleCapabilityMatrix.cs:38-52`) and **are** the delete capabilities (`prd.md:341-342` "Create, edit, delete"). Add no capability. Client predicates `TaskCapabilities.PermitsManageTasks`, `ProjectCapabilities.PermitsManageProjects`. Refusal copy `manage-tasks`, `manage-projects` exist (`ClientCopy.resx:454-526`).
- **Client (2.4 state)**: `TaskDetail.razor` — modal dialog (l.40), sentinels (l.41, 183), `_close` (l.45), refusal region `role="status"` (l.181), parameters l.257-258, first render focuses Close (l.300-306), `KeyDownAsync` `Esc` (l.333-361), `FocusLastAsync` (l.370-388). `BoardView.razor.cs` — `_replicas`, `_openTaskId`, `_panelRefusal`, `_focusCard`, `_focusCardRequests` (l.44-55); `Close()` (l.101-111) requests card focus; `PurgePanel` (l.116-125); `UseAnswerAsync` 404 → `OnRefused` (l.284-296). `BoardView.razor` — polite live region (l.49), panel (l.105-120), `Add(TaskResponse)` (l.340-352), `ColumnState` with mutable `Total` (l.357-367). `BoardColumn.razor` — cards with `@key`, `FocusTaskId`/`FocusRequest` (l.36, 47-82). `ProjectList.razor` (571 lines) — live region (l.41), rows (l.107-119), non-optimistic `RenameAsync` (l.431-475, fresh `Guid.NewGuid()` key), `OnSpaceRefused` on 404 (l.391). `ProjectRow.razor` — name link + Rename when `CanManage`. `LabelRow.razor` (l.18-35) — the in-place `<span class="destructive-confirm">` with `button-danger` + `button-secondary`. `SpaceWrites.SendWithoutBodyAsync` (l.54-55). `SpaceNarration` (`Say`/`Take`, "narrated, then routed"). `.destructive-confirm` (`components.css:1106-1111`) is flex/wrap/gap only — **no danger border yet**; `.button-danger` l.540-561.
- **Preserve**: every 2.3 and 2.4 review fix (stale focus request cleared; pointer-blocking layer; refusal for one Task never shown in another's panel; `InFlight` cleared only after admission; unreadable `200` keeps the key; `5xx` is no answer; Labels toggle keeps carried Labels; `invalid-task-labels` re-reads the Label list; Space settings resets per Space; timeouts say "Not sent.").

### Isolation rows (Task 8)

| Id | Test design | Vector | Route | Request | Expectation (both surfaces) |
|---|---|---|---|---|---|
| ISO-DELETE-TASK-FOREIGN-SPACE | I-2 | Write | `DELETE TaskRoutes.Task` | A → `TaskOf(SB, TB)` (anchor: random Space) | `BoundaryNotFound`, B's Task still present |
| ISO-DELETE-TASK-FOREIGN-ID-IN-OWN-SPACE | I-1, I-13 | Write | `DELETE TaskRoutes.Task` | A → `TaskOf(SA, TB)`, anchor `TaskOf(SA, random)` | `SameAsAnchor(404, not-available)`, no refusal row, B's Task present; two-layer plant |
| ISO-DELETE-PROJECT-FOREIGN-SPACE | I-2 | Write | `DELETE ProjectRoutes.Project` | A → `ProjectOf(SB, PB)` | `BoundaryNotFound`, B's Project and its Tasks present |
| ISO-DELETE-PROJECT-FOREIGN-ID-IN-OWN-SPACE | I-1, I-13 | Write | `DELETE ProjectRoutes.Project` | A → `ProjectOf(SA, PB)`, anchor `ProjectOf(SA, random)` | `SameAsAnchor(404, not-available)`, no refusal row, B's Project and Tasks present; two-layer plant |
| ISO-RECORD-KIND-TASK-DELETE-VIEWER | V-1, C-6, NFR-7 | RecordKind | `DELETE TaskRoutes.Task` | a Viewer of SA | `RoleRefused("manage-tasks")`, Task present |
| ISO-RECORD-KIND-PROJECT-DELETE-VIEWER | V-1, C-6, NFR-7 | RecordKind | `DELETE ProjectRoutes.Project` | a Viewer of SA | `RoleRefused("manage-projects")`, Project present |

I-11 (Account-scoped surfaces) needs no row: no Account-scoped route is touched.

### Open questions (decide in Task 1; each reversible; record as "Qn - (x)")

1. **Where does "Delete Project" live?** `EXPERIENCE.md:268` and UJ-9 (`:555-566`) put it in **Project settings**, which is Owner/Admin only (`EXPERIENCE.md:54`, `prd.md:808`), holds only the Status delta, and does not exist until 6.1. FR-16/FR-17 grant deletion to **Members**, and UX-DR27 makes a missing surface the same as no capability — so Project settings alone would silently take the capability from Members. Options: (a) `ProjectRow` on Space home, beside Rename (same Role audience as rename, all Roles see the list, the `LabelRow` in-place pattern fits exactly); (b) a Board header control; (c) build a minimal Project settings now. **Recommend (a)**: the capability lives where its siblings (create, rename) live; 6.1 decides whether Project settings also offers it. Ledger the spine variance to UX and 6.1. Cost: none in code for 6.1; a second entry point is a link.
2. **How does the Project confirm get a true Task count?** `ProjectResponse` carries none. Options: (a) add `TaskCount` to `ProjectResponse` (AD-19 permits appending; one grouped `COUNT` over ≤50 Projects in the list read) and **re-read the list when the confirm opens**, so the count is fresh; (b) a new `GET …/projects/{id}` returning the count (a new route → its own isolation rows on both surfaces, GATE-002); (c) read the Board and sum `Total`s (heavy at 5,000 Tasks). **Recommend (a)**; the count is shown only in the confirm (a count in the list would be a nudge, UX-DR26/`epics.md:1049`). The count can change between confirm and commit (another tab adds a Task): the deletion still destroys everything in the Project, as FR-17 says — **do not** add an expected-count precondition (it would be a new refusal nobody specified); record it. Cost: `ProjectEndpointTests.ProjectProperties = ["createdAt", "id", "name"]` (`tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.cs:74`, asserted exactly at `:106`) widens by exactly `taskCount`, deliberately; the fake's Project shape (`FakeYelloApi.cs:1130`) follows. `BoardEndpointTests.ProjectProperties = ["id", "name"]` is the Board's `BoardProjectResponse` and must **not** change.
3. **Where does focus land after a successful delete?** Task: **the column that held it** — `EXPERIENCE.md:401-403` (dialog close → "the invoking element; if it no longer exists, its nearest surviving container") and UX-DR39's column rule, `deferred-work.md:510`. Recommend the column's heading region with `tabindex="-1"`. Project (Q1 (a)): recommend the Projects list's heading (`tabindex="-1"`); when the list becomes empty, the empty state's heading. Never `<body>`.
4. **What does deleting an already-gone Task or Project answer?** AD-3: `404 not-available`, byte-identical (no `410`, no `204` for "already gone" — that would disclose existence across Spaces). **Decided by the architecture**; the client cost (an indistinguishable 404 raises the refusal surface) is the same as 2.4's Label case (`deferred-work.md:530`, owner 4.4) — add to that entry.
5. **Lock order.** Recommended in Task 3 (Task delete: Project Task range → Task row `INDEX(1)` → delete; Project delete: Project row `INDEX(1)` → Project Task range → delete, or range first if RACE-P1 shows a cycle). **The races are the authority**: run RACE-P1 and RACE-T1/T2 first (riskiest assumption), record what was measured, pick the order that is green 3/3 with every plant caught. Cost: none to later stories beyond the remark they must read (2.6 Move adds another range writer).
6. **Add a `BEFORE DELETE` block predicate?** The FILTER predicate already hides foreign rows from `DELETE`, and cascades stay inside one `SpaceId` along composite FKs. **Recommend no** (no migration, `MovableTablePredicates = 3` unchanged). The two-layer plants in Task 8 prove the filter carries it. Cost: 3.3 (Space delete) reconsiders when it cascades a whole Space.
7. **In-flight edits when a delete lands.** Recommend: the delete may be confirmed while an edit is in flight; on `204` the tab drops the replica, discards queued edits, and ignores any later answer for that id (no refusal surface, no revert, no copy). Alternative: the danger control waits until the Task's replica has nothing in flight. Cost: none.
8. **Where is "Delete Task" in the panel?** Recommend the last control, `button-secondary` (danger is spent only inside the confirm — `components.css:535-537`, DESIGN.md:491), after the attributes. `FocusLastAsync` changes.
9. **Which confirm control takes focus on open?** Recommend **Cancel** (the non-destructive choice; a misplaced `Enter` destroys nothing). Alternative: the confirm's sentence (`tabindex="-1"`) so a screen reader reads the consequence first, then Tab reaches the buttons. Either satisfies AC1/AC11; record.
10. **The busy state while the `DELETE` is held.** `disabled` is banned only "for Role reasons" (UX-DR27), but the browser gate counts every `[disabled]` in `main`/dialogs. Recommend `aria-busy="true"` and ignoring repeat clicks (the `LabelSection._deleting` pattern), no `disabled`. Cost: none.
11. **Announcements.** Recommend polite "Task deleted." in the Board's region and "Project deleted." in `ProjectList`'s; refusals in the confirm's own status region. A remote observer's Board (another tab) learns nothing until it re-reads — live push is Epic 7 (`EXPERIENCE.md:256` debounced summary), not here.
12. **One shared confirm component or two?** Recommend a small `DestructiveConfirm.razor` (sentence(s) + danger action + Cancel, parameters for copy and callbacks) used by the panel, `ProjectRow`, and refactor `LabelRow` onto it only if cheap — it makes AC13's gate one component. Alternative: three copies of the markup, gated by class. Cost: none.
13. **Project-delete lock escalation** (Task 3): measure only; ledger to 2.9. If the measured duration of a 5,000-Task Project delete is long enough to fail `NotAvailableFloor`-adjacent timing cases elsewhere, record it — do not batch (atomicity, AC3).

### Architecture compliance (must)

- `SpaceId` never from the request; from `ActiveSpaceContext`. No `Where(SpaceId == …)` in stores — it would mask a missing layer (the two-layer plants depend on that).
- State changes only through a slice, inside the pipeline's one transaction; the store saves before returning (`IdempotencyStore.EnsureNothingElseIsPending`).
- `404` for absent and foreign, byte-identical and floored; `403` recorded; no in-Space not-found recorded (AD-3, AD-20). Error bodies carry no Project name or Task title.
- No `[Authorize(Roles=…)]`, `IsInRole` (AR-4). No `Results.Created`/`Accepted` (ARCH-009). Answers via `Results.NoContent()`/problem results (ARCH-010).
- No trash, no soft delete, no `DeletedAt` column, no restore window (§6.2, `ARCHITECTURE-SPINE.md:431` — reversal would change the delete path for Space, Project and Task at once).
- Logs: `SpaceId` and ids only.
- Client: nothing in browser storage; `document.title` stays `Yello`; the URL holds ids only; every string externalised.

### Library / framework

No new packages (CLAUDE.md: a new package needs Lee's approval). .NET 10.0.11, EF Core 10 (`ExecuteDeleteAsync`, `FromSql` composed on a filtered `DbSet`), Microsoft.Data.SqlClient 7.0.2 (error numbers 547, 1205), Blazor WASM 10, xunit.v3 4.0.0, Testcontainers 4.6.0 (`mssql/server:2025-latest`), Microsoft.Playwright 1.62.0, ArchUnitNET 0.13.3 — as pinned. SQL Server facts that matter here: lock escalation is attempted at ~5,000 locks held by one statement on one object; `INDEX(1)` forces the clustered index so two lockers meet on one key; cascading referential actions run inside the deleting statement's transaction.

### Testing standards (from `tests/TESTING-CONVENTIONS.md` and CLAUDE.md)

- Add cases to existing suites; **no new suite, no new container fixture** (`ContainerTopologyGateTests`). One SQL engine per test project, one database per class — hence partials.
- Only the `Suite`, `Priority`, `Requirement`, `Assumption` traits; `Assumption=PRD-12-5` on Project-irreversibility cases (Task irreversibility is §6.2, not §12 — no Assumption trait). Sentence-style names, one behaviour per test.
- No `Task.Delay`. Races hold the contended lock and wait on `LCK_M_%` (`TESTING-CONVENTIONS.md:101-133`); "when the race is about a read, the test does the other write"; a race about a lock held between two statements holds the request between them.
- Randomised data; never a shared email. Cleanup by rollback or disposal — **never a `DELETE` statement in test cleanup** (the product's own delete route is the thing under test, not cleanup).
- Every absence assertion is proved against a plant. No coverage threshold.
- Playwright fakes the API at the network edge with Contracts types. A non-optimistic write: **hold, assert nothing removed, release, assert removed**.
- Timing-sensitive cases in `DisableParallelization` collections. Full runs with `--max-parallel-test-modules 1`; never two `dotnet test` at once.

### Previous story intelligence (2.4, 2.3)

- **Lock ordering is where both previous stories found their real bug.** 2.3: a column-only range could deadlock against the creation's Project-wide scan → every key placer takes the Project Task range first. 2.4 dev: RACE-002 stayed green with the hint removed (a read-committed read already waits), so a new deterministic case was added and it found a **1205 deadlock → 500** because the apply's `COUNT` was covered by `AK_Label_SpaceId_Id` and the delete's read used the clustered key → `INDEX(1)` on both. 2.4 review: two concurrent Labels edits could store a set neither sent → the edit now locks the Task row first and Labels in id order. **Expect the same here; write the case that holds the request between its two statements, not only the one that holds the other writer.**
- 2.4 plant 9 was masked (probe hint removed; the `NoAction` backstop answered `409`). If a delete plant is masked by a cascade or FK, record why.
- 2.4 decisions to keep: DELETE carries no body (`SendWithoutBodyAsync`); "narrated, then routed" via `SpaceNarration`; the in-place confirm is not a dialog (no 6px clause was owed).
- 2.3 decisions to keep: Q6 the panel is Board state, not a route; Q8 keys reused on an identical resend after no answer; Q9 `Esc` innermost first; 6px panel; 40rem panel width.
- Gates surprised 2.3 and 2.4 at implementation (shape lists, route sets, `TaskProperties`); Task 5 names the ones this story breaks so they are not surprises. A Project response shape gate (if Q2 (a)) may be another — search for one pinning `ProjectResponse`'s members before changing it.
- Environment: orphaned `Yello.Host.exe` held `bin/Debug` in 2.4 → it built Release; the browser suite needs a Debug `Yello.Client` regardless. A full run once lost the Isolation engine at start and was rerun once.
- 2.4 review defer that this story touches: `deferred-work.md:530` (a delete's 404 raises the Space refusal surface; owner 4.4).

### Git intelligence

Recent commits follow "Add story → Add ATDD scaffolds → implement → Close the code review" (`cd45af9` story 2.4, `a932685` scaffolds, `0cad37b` implementation — 58 files: Domain port + entity, Application slices, Infrastructure store/config/migration, Contracts, Host endpoints, client components/CSS/resx, gates, isolation rows, Slices partials, Client tests; `6e0baab` review). `10d1a5c` added `CLAUDE.md` (stack, verification commands, test memory budget) — read it. Expect a smaller footprint than 2.4: no migration, no entity, no palette; two slices, two store methods, two endpoints, six isolation rows, the confirm in two places, one copy gate.

### Ledger entries

- `deferred-work.md:510` (owner **2.5**) — focus to the column when the card is gone: **build and close**.
- `:36` — radius/border mapping: the destructive confirm's danger border; state whether a 6px clause is owed.
- `:20` — text spacing: the confirms join `TextSpacingTests`.
- `:530` (owner 4.4) — the indistinguishable 404 after a delete: add the Task and Project delete cases.
- `:492` (owner 2.6) — position keys: note that deletion writes no key and renumbers nothing, and its lock order.
- `:493` (owner 2.9) — Board totals vs Tasks: a deletion committing between the count and the Task read is the mirror of the creation case; note it.
- `:471`, `:477` (owner 2.9) — bound locks: a delete takes the same Project range; note it.
- New (Task 13): Project-delete lock escalation (2.9); Delete Project's home vs Project settings (6.1 / UX).

### Project Structure Notes

- New: `Yello.Application/Tasks/DeleteTask/DeleteTaskHandler.cs`, `Yello.Application/Projects/DeleteProject/DeleteProjectHandler.cs`; deletion outcome enums beside `ITaskStore`/`IProjectStore` in `Yello.Domain`; possibly `Yello.Client/Components/DestructiveConfirm.razor` (Q12); `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.{Delete,DeleteRace,DeleteIdempotency}.cs`, `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.{Delete,DeleteRace,DeleteIdempotency}.cs`, handler tests, `tests/Yello.Tests.Architecture/DesignFoundationGateTests.DestructiveConfirm.cs`, the "archive" gate, `tests/Yello.Tests.Client/DeleteTests.cs` (or partials of `TaskDetailTests`/`SpaceHomeProjectsTests`).
- Updated: `ITaskStore`, `IProjectStore`, `TaskStore`, `ProjectStore` (and `BoundCounter` remarks), `TaskEndpoints`, `ProjectEndpoints`, `Program.cs`, `ProjectRoutes`, possibly `ProjectResponse` + `ListProjects` read (Q2), `TaskDetail.razor`, `BoardView.razor(.cs)`, `BoardColumn.razor`, `ProjectList.razor`, `ProjectRow.razor`, `LabelRow.razor` (if Q12), `components.css`, `ClientCopy.resx`, `RefusalCopy.cs` (only if a new type), the five gates in Task 5, `IsolationCases.cs`, `FakeYelloApi.cs`, `TextSpacingTests.cs`, `TESTING-CONVENTIONS.md`, `deferred-work.md`.
- Variance: Delete Project on Space home rather than Project settings (Q1) — the spine's surface does not exist and its audience excludes Members, whom FR-17 grants the capability. AR-3's "validator" files do not exist by convention; follow the code.

### References

- `_bmad-output/planning-artifacts/epics.md:1132-1164` (story), `:107, :116` (FR-17, FR-23), `:179` (§6.2 owners), `:250, :267, :271, :280, :281, :283` (UX-DR12, 26, 27, 36, 37, 39), `:316, :358, :389, :401` (coverage, staging)
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md:321-359` (FR-15/16, matrix `:341-342`), `:367-375` (FR-17), `:427-433` (FR-23), `:716-734` (NFR-5, NFR-7), `:769-774` (§6.2), `:804-811` (§7 IA), `:896` (§12 item 5); `addendum.md` (nothing delete-specific)
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md` — `:46` (pipeline-only), AD-2 `:81-86`, AD-3 `:92-94`, AD-4 `:100`, AD-5 `:106`, AD-7 `:118`, AD-15 `:166`, AD-18 `:186`, AD-20 `:198`, AD-29 `:256`, conventions `:267-276`, ER `:363-377`, deferred trash `:431`
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:49-54, 67, 85, 99-122, 207, 220-221, 256, 266, 268, 271, 335, 375, 394-405, 458, 555-566`; `DESIGN.md:23-24, 38-39, 210-212, 277-284, 322, 360, 468, 476, 491, 501, 519`
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md:496, 502, 651, 1102-1108, 1232-1237, 1321`; `sprint-change-proposal-2026-09-13.md` (nothing on deletion)
- `_bmad-output/specs/spec-yello/SPEC.md:101-103, 122-124, 208, 269`; `acceptance-criteria.md:174-179, 218-221`; `role-capability-matrix.md:12-13`
- `_bmad-output/test-artifacts/test-design-qa.md:73, 173, 265-309, 329, 352, 587`; `test-design-architecture.md:220, 396, 549`
- `_bmad-output/implementation-artifacts/2-4-define-and-apply-labels.md` (Debug Log, plants, review findings); `2-3-edit-a-tasks-title-status-and-due-date.md` (review findings `:241-266`, Debug Log `:482-527`); `deferred-work.md:20, 36, 471, 477, 492, 493, 510, 530`
- `tests/TESTING-CONVENTIONS.md:9-151, 205-237, 265-307, 358-403, 530-588`; `CLAUDE.md` (memory budget, race rule, undo rule, isolation rows)

## Dev Agent Record

### Agent Model Used

### Debug Log References

**Create-story phase (2026-10-26).** No isolation row is pending on 2.5 (grepped `PendingUntil` in `tests/Yello.Tests.Isolation`). No ATDD scaffold exists for 2.5. After setting `2-5-delete-a-task-and-delete-a-project: ready-for-dev`: `dotnet build Yello.slnx` 0 warnings, 0 errors; `IsolationCaseListTests` 14/14 passed (GATE-001..004 green). Planning inputs read via two research passes (planning documents; code map); facts above cite paths and lines as read on 2026-10-26.

### Completion Notes List

- Story created 2026-10-26 by create-story: ultimate context engine analysis completed — comprehensive developer guide created. Open questions Q1–Q13 carry recommended decisions; the dev phase decides each as a reversible, recorded decision.

### File List
