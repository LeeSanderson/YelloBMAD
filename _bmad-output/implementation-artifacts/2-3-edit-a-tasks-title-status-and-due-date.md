# Story 2.3: Edit a Task's title, Status and due date

Status: ready-for-dev

Epic: 2 — Track your own work on a Board
Story key: `2-3-edit-a-tasks-title-status-and-due-date`
Requirements owned (Story Coverage Index, `epics.md:356`): **FR-20** (edit title, Status and due date; Labels are 2.4, description is FR-31/Epic 7; `prd.md:400-406`, `epics.md:113`), **AR-20 / AD-11** (local replica, never blocks, server admits or rejects, rejected change reverted; `epics.md:208`, `ARCHITECTURE-SPINE.md:138-142`), **AR-34** (`DateTimeOffset` UTC, ISO 8601 with offset, RFC 9457 stable `type`, mutation only through a slice; `epics.md:222`), **UX-DR11** (the picker, Status use; never a default selection; `epics.md:249`), **UX-DR26** (the banned list: no save button, no auto-save indicator, no modal stack deeper than one, optimistic by default; `epics.md:267`).
Also binding: **AD-3** (404 = absent, `{spaceId}` on every route), **AD-4** (both surfaces), **AD-15** (position key unique within `(ProjectId, StatusId)`, never writable), **AD-16 / AR-23** and **AD-17 / AR-24** (effective set derived on read; no Task holds a Status outside it), **AD-18 / AR-25** (`Idempotency-Key` on the edit), **AD-20** (only 403 and boundary 404 recorded), **AD-19 / FR-37** (adding a response field is legal in v1), **UX-DR5** (6px on the Task detail panel), **UX-DR10** (`task-detail`), **UX-DR12** (dialog rules), **UX-DR27** (absent, not disabled), **UX-DR32**, **UX-DR33**, **UX-DR37**, **UX-DR39/40** (focus trapped, returned on close), **UX-DR42** (dates in the viewer's locale), **NFR-9** (the Task editor is one of the five WCAG 2.1 AA flows), and the 1.9 isolation gap gates (**AR-35, AD-4**) for every route this story adds.
Capabilities in the SPEC: **CAP-20** (`specs/spec-yello/SPEC.md:113`, `acceptance-criteria.md:198, 260`; `surfaces-and-journeys.md:120`).
Ledger entries owned by this story (`deferred-work.md`): `:485` (the Task-only two-layer isolation row) and the 2.3 clauses of `:36` (6px radius mapping for the detail panel and any dialog). Also due here: `:20`'s convention that every new surface adds its own text-spacing case, and `:492`, which is owned by 2.6 but which this story's Status change makes live sooner. See "Ledger entries" in Dev Notes.
Depends on: **2.2**, done (`TaskItem`, the `Task` table and its three predicates, `TaskRoutes.TaskOf`, the Board page, `SpaceArrival`, `SpaceWrites`, `NotAvailableFloor`).
Extended later, not here: **2.4** (Labels on the card and in the panel), **2.5** (delete a Task from the panel; focus return when the card no longer exists), **2.6** (the Move control in the panel, position within a column, the midpoint key generator), **2.8** (the keyboard grid: roving tabindex on cards), **2.10** (sort by due date), **4.6** (Assignee), **6.1** (Project Status deltas: the literal sibling-Project case), **Epic 7** (description, live propagation of other people's edits).

> **Two isolation gates bind this story. Read both before writing a route.**
> 1. **1.9-GATE-002** (`IsolationCaseListTests`, `Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case`) fails the Isolation suite for any `/api` route the Host maps that no `IsolationCases` row names by exact `(Method, Route)` constant. **Every new `/api` route this story maps — the Task `GET` and the Task `PATCH` — must have an isolation row with an expectation on both surfaces (`Session` and `ApiToken`) in the same change that maps it.** A row must actually send a request on the route it names (`CaseRun.cs:38`).
> 2. **1.9-GATE-003** (`Every_pending_row_names_a_story_that_has_not_yet_reached_review`) lets a row stay pending (`PendingUntil: "2.3"`) only while 2.3 is `backlog`, `ready-for-dev` or `in-progress`. **It fails any row still pending on 2.3 once 2.3 reaches `review`.** Today no row is pending on 2.3, but the ATDD phase may add some (2.2's ATDD added six). Every one must be live before the dev phase sets `review`, because the dev phase sets `review` at its end and a forgotten row fails the dev phase itself.
>
> This create-story change moves 2.3 to `ready-for-dev`, which GATE-003 permits. Build and Isolation suite results after the change are in the Dev Agent Record.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As someone whose work changes,
I want to change what a Task says and where it sits without saving,
so that keeping the Board true costs nothing.

## Acceptance Criteria

AC1–AC7 are reproduced **verbatim** from `epics.md:1067-1095` (`status: final`), numbered by position. AC8–AC14 come from the ledger, the 1.9 gap gates, the architecture and the UX spine; each cites its source.

**AC1 — optimistic edit, server arbitrates**
**Given** a Task
**When** an Owner, Admin or Member changes its title, Status or due date
**Then** the change renders locally at once without blocking on the network, and the server admits or rejects it afterwards
**And** a rejected change is reverted in the client replica, because the client is never the arbiter of what is in the Space

**AC2 — no save**
**Given** any surface in the product
**When** it is inspected
**Then** there is no save button and no auto-save indicator anywhere, because an indicator would imply a save that does not exist

**AC3 — the Viewer**
**Given** a Viewer
**When** a Task opens
**Then** attributes render as text with no edit affordance present
**And** a write attempted over the API is refused regardless of what the interface offered, because the interface hiding an action is never the mechanism that enforces it

**AC4 — Status within the effective set**
**Given** a Task
**When** its Status is set to a value in its Project's effective set
**Then** the change is accepted; and when set to a Status valid in a sibling Project but absent from this one, it is refused

**AC5 — the panel**
**Given** the Task detail panel
**When** it opens
**Then** it opens over the Board one level deep and is never stacked two deep, `Esc` closes it, focus is trapped while it is open and returned to the originating Task card on close

**AC6 — no description**
**Given** Task detail in this epic
**When** it renders
**Then** no description editor is present — description editing is FR-31 and arrives with the merge port in Epic 7, which is also why nothing here writes a description field

**AC7 — the due date on the wire**
**Given** a due date
**When** it is stored and sent
**Then** it is a `DateTimeOffset` in UTC, ISO 8601 with offset on the wire, and rendered in the viewer's own locale and zone

**AC8 — the new routes are isolation rows on both surfaces, and the Task's own two layers are proved** *(1.9-GATE-001/002/003, AR-35, AD-3, AD-4; test-design I-1, I-2, I-4, I-5, I-13, C-6; `deferred-work.md:485`)*
**Given** `GET` and `PATCH /api/v1/spaces/{spaceId}/tasks/{taskId}`
**When** the isolation suite runs
**Then** each route is named by at least one `IsolationCases` row with an expectation for **both** `Session` and `ApiToken`
**And** a foreign Space's Task is unreachable by read and by write on both surfaces, with the boundary `404` byte-identical to an absent Space, one `CrossSpace` row, and nothing written
**And** a foreign Task id used inside one's own Space answers the same bytes as a random id, with no refusal row — and that row **fails** when the Task's query filter and the `Task` FILTER predicate alone are dropped (the Project's layers intact), proved by a plant
**And** no isolation row names 2.3 as pending when 2.3 reaches `review`

**AC9 — a Status change places the Task in its new column without a duplicate key** *(AD-15, AD-17; `deferred-work.md:492`; TESTING-CONVENTIONS deterministic-race rule)*
**Given** a Task whose Status is changed
**When** the change is admitted
**Then** the Task takes a fresh position key after the last key of its new column, only the edited Task's key is written, and its old key is not reused
**And** two placements into the same column (an edit and a creation, or two edits) cannot compute the same key, proved by a case that holds the lock and waits on `sys.dm_exec_requests`, not by firing requests together

**AC10 — the edit is idempotent** *(AD-18, AR-25; test-design P-10)*
**Given** a `PATCH` carrying an `Idempotency-Key`
**When** it is replayed
**Then** the original response is returned byte-identically without re-applying the change; the same key with a different body is `422 idempotency-key-reused`; another Account's key does not replay

**AC11 — refusals are stable types that disclose nothing** *(AR-34, test-design C-7, I-13)*
**Given** an edit with an invalid title, a Status outside the Project's effective set (a random id, or a real Status id from another Space), or an invalid due date
**When** it is refused
**Then** it is a `400` RFC 9457 problem with a stable `type` per field, the random-id and foreign-id Status refusals are byte-identical, no refusal row is written, nothing is changed, and no body echoes a title, name or id that was not in the request URL

**AC12 — every refusal the client can meet is reverted and said truthfully** *(AD-11; UX-DR32; `EXPERIENCE.md:143-147`)*
**Given** an optimistic edit in the panel
**When** the server answers `400`, `403`, `404`, `409`/`422` for the idempotency key, or does not answer
**Then** the replica (panel and card, and column totals for a Status change) returns to the last server-admitted value; a `400` shows the field's own copy inline; a `403` shows the `manage-tasks` capability copy inline; a `404` reverts and then renders the page's one refusal surface; never a toast, never a silent revert, never server prose

**AC13 — the panel is drawn from the design system and survives text spacing** *(UX-DR5, UX-DR10; `DESIGN.md:238-244, 439`; `deferred-work.md:36, :20`)*
**Given** the Task detail panel
**When** the design gates and the browser suite run
**Then** the panel takes the 6px radius (`--radius-lg`), a hairline border and no shadow, gated with a plant
**And** under the WCAG 1.4.12 text-spacing override and 200% text-only zoom no panel text is clipped or overlapped

**AC14 — the panel is a Space-scoped surface like any other** *(UX-DR31, UX-DR33, UX-DR37, UX-DR27)*
**Given** the panel open in the client
**When** the Space is purged, the Role is re-read lower, or the tab leaves the Space
**Then** the panel closes with the Board's purge, a now-Viewer's edit controls disappear in place, no Task title appears in `document.title` or a URL, the title input carries `autocomplete="off"` and a non-reusable field name, and every string is externalised

## Tasks / Subtasks

- [ ] **Task 1 — Confirm the gates, then record decisions before the code that depends on them** (AC: all)
  - [ ] `dotnet build Yello.slnx`, then run `Yello.Tests.Isolation` filtered to `IsolationCaseListTests`. With 2.3 at `ready-for-dev` or `in-progress` it is green. List every row with `PendingUntil: "2.3"` (the ATDD phase may have added some) in the Debug Log; each is converted before `review`.
  - [ ] Read the open questions. Decide Q1–Q5 before Task 2, Q6–Q8 before Task 6, Q9–Q13 before Task 9. Record each in the Completion Notes as "**Qn - (x).** … Cost to later stories: …". Each is reversible.

- [ ] **Task 2 — Domain: the due date, the edit and its invariants** (AC: 1, 4, 7, 9, 11)
  - [ ] `TaskItem.DueDate` (`DateTimeOffset?`, settable; `null` = no due date, `prd.md:387` "optionally … a due date"). Never `DateTime` or `DateOnly` (A-15, AR-34).
  - [ ] A due-date rule in Domain (for example `TaskDueDate.TryNormalise(DateTimeOffset? submitted, out DateTimeOffset? dueDate)`), converting to UTC (`ToUniversalTime()`, offset zero) and refusing values outside a sane range (Q4). Invariants live in Domain, not validators (AR-34).
  - [ ] Status membership checked through `EffectiveStatusSet.Of(...)` — the one place the derivation lives, so 6.1 adds the Project delta there. Add a Domain predicate (for example `EffectiveStatusSet.Contains(statusId)` or a static `IsIn`), not an ad hoc `Any` in the handler.
  - [ ] `ITaskStore` gains `FindAsync(taskId)` and an edit method (Q1 shapes it). Both return `null` for a Task not visible in the active Space. **No `Where(SpaceId == …)`**: both layers are already narrowed.
  - [ ] No description field, no `ProjectId` write, no `SpaceId` write (gate `Only_the_create_path_writes_a_Tasks_Space` must stay green).

- [ ] **Task 3 — Schema: the due-date column** (AC: 7)
  - [ ] `TaskConfiguration`: `DueDate` → `datetimeoffset` nullable. Nothing else changes; no new index (2.10 owns sort by due date and decides an index then).
  - [ ] Migration `…_TaskDueDate` via `dotnet ef migrations add`, hand-edited per `TESTING-CONVENTIONS.md:309-329` (file-scoped namespace, real XML docs, no `// <auto-generated />`, `Up` under 80 lines). **Expand only**: a nullable column, so the previous revision keeps working (1.10's expand/contract rule). Avoid a migration class name that shadows a namespace (S4041 refused `Tasks` in 2.2).
  - [ ] The RLS predicates are per table, not per column, so nothing changes in the policy. `SpaceIsolationSchemaTests` (three predicates on `Task`) must stay green. `dotnet ef migrations has-pending-model-changes` reports none.

- [ ] **Task 4 — Contracts** (AC: 1, 7, 8, 11)
  - [ ] `TaskRoutes.Task` = `"/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}"` (the pattern constant; `TaskOf(s, t)` already builds the path). Add `TaskRoutes.TaskIdParameter` if the pipeline or a gate needs it. The URL never holds the Project (FR-41).
  - [ ] `TaskResponse` gains `DateTimeOffset? DueDate` (appended; AD-19 permits adding a field in v1). Update the client's fakes and every construction site.
  - [ ] The edit request per Q1 (recommended: `EditTaskRequest` read as JSON merge-patch semantics: an absent member is unchanged, `dueDate: null` clears the due date, `title: null` or `statusId: null` is invalid).
  - [ ] **Two Architecture gates fail the moment this lands, by design — update them deliberately, not loosely** (`tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Tasks.cs`):
    - `The_Task_creation_request_carries_a_title_and_nothing_else` assumes `CreateTaskRequest` is the only `*Request` in `Yello.Contracts.Tasks`. Keep its assertion that the **creation** request carries only `Title`, and add the edit request's exact property set (title, Status, due date — **no** description, position key, Project or Space).
    - `No_Task_or_Board_response_carries_a_SpaceId_and_a_column_carries_its_own_total` pins the exact set of response names. Adding a property to `TaskResponse` does not break it; a new response type does.
  - [ ] Problem types in `ProblemTypes.cs` with remarks stating what each never means (Q5): reuse `invalid-task-title`; add `invalid-task-status` and `invalid-due-date` (`400`). No new type for the Viewer (`insufficient-role` with `capability: manage-tasks` exists).

- [ ] **Task 5 — The slices and endpoints** (AC: 1, 3, 4, 7, 9, 10, 11)
  - [ ] Slices (AR-3 naming from FR-20's title): `Yello.Application/Tasks/ViewTask/ViewTaskHandler.cs` and `Yello.Application/Tasks/EditTask/EditTaskHandler.cs` (or `EditTaskAttributes`; Q12). Handlers take the pipeline's `ActiveSpaceContext` or nothing. **None reads the Role, counts, checks a capability or touches `Idempotency-Key`** — the existing gates (`Only_the_pipeline_depends_on_…`, `Nothing_but_the_pipeline_reads_the_Role_…`) catch each.
  - [ ] **Edit**, in order:
    1. Find the Task (RLS and the filter hide another Space's). Not visible → `null` → `BoundaryProblems.NotAvailable()` byte-identically, **no** refusal record (AD-20).
    2. If a Status is supplied, it must be in the effective set of **the Task's own Project** (`IStatusDefinitionReader` → `EffectiveStatusSet.Of`). If not → `invalid-task-status`. This check fires **before** any write, so the composite FK `(SpaceId, StatusId)` is never what refuses a foreign Status (that would be a `500`).
    3. Apply title, due date, Status. A Status that differs from the current one takes a fresh position key (Task 6). Setting the same Status is a no-op for the key.
    4. Save the store's own effect before returning (the idempotency record refuses anything else pending; `IdempotencyStore.EnsureNothingElseIsPending`).
    5. Return the Task as stored; the endpoint answers `200` with `TaskResponse`.
  - [ ] **View**: find → `200 TaskResponse` or the same `404`.
  - [ ] Endpoints in `Yello.Host/Endpoints/TaskEndpoints.cs`, mapped through `api` in `Program.cs` beside `MapCreateTask`:
    - `GET` → `RequiresCapability(Capability.ReadProjectsAndTasks)`.
    - `PATCH` → `RequiresCapability(Capability.ManageTasks)`. **No `ConsultsBound`** (an edit adds no Task; consulting `TasksPerProject` would refuse edits in a full Project). Rename (`ProjectEndpoints.MapRenameProject`) is the template.
    - Neither is `SessionOnly` (AD-4).
    - Read the body inside the endpoint with `JsonRequestBody.ReadAsync` (Q1 may need a `JsonElement`/`JsonDocument` read to tell absent from `null`); an unreadable body is `400`, never a `500`.
    - Return `Results.Json`/`Results.Ok` (ARCH-010 and `IdempotentResponses.Capture` refuse anything the record cannot replay).
  - [ ] Structured logs carry `SpaceId`, never a title (I-17). Every title an isolation case writes goes in the world's secret set.

- [ ] **Task 6 — Position key on a Status change, serialised** (AC: 9; `deferred-work.md:492`)
  - [ ] Today the only thing serialising key placement is the creation bound's `UPDLOCK, HOLDLOCK` count over the Project (`BoundCounter`), which an edit does not take. A Status edit is a **second writer that places keys**. Without a lock, an edit and a creation (or two edits) into the same column compute the same `PositionKey.After(last)`, and the unique index `(SpaceId, ProjectId, StatusId, PositionKey)` answers a `500` (or a mis-reported `409` when keyed).
  - [ ] In `TaskStore` (Infrastructure only; raw SQL is permitted there, `SpaceResolutionSeamTests`): read the target column's last key under `WITH (UPDLOCK, HOLDLOCK)` over `WHERE ProjectId = @p AND StatusId = @s` (parameterised `SqlQuery`, never concatenation), then write the new key, in the request's unit of work. The `SpaceId`-leading index keeps the range lock inside one Space (`deferred-work.md:471` note). State in `TaskStore`'s and `PositionKey`'s remarks that both writers now take a lock.
  - [ ] Check that creation's Project-wide range lock and the edit's column range lock conflict (both `RangeS-U` over overlapping ranges serialise). If they do not, take the Project-wide range in the edit too. Record what you measured.
  - [ ] Deterministic race cases (TESTING-CONVENTIONS "deterministic concurrency" rule, `TaskWire.WaitUntilBlockedOnALockAsync`): the **test** holds the column range in its own transaction, sends (a) an edit into that column plus a creation into it (first column), and (b) two edits into the same column; waits until both requests are blocked on any `LCK_M_%`; commits; asserts both `200`/`201` and distinct keys. Plant: remove the hint → caught three runs out of three.
  - [ ] Add a note to `deferred-work.md:492` that 2.3 added the second key-placing writer and how it is serialised. The entry stays with 2.6 (moves and the midpoint generator).

- [ ] **Task 7 — Idempotency on the edit** (AC: 10)
  - [ ] Nothing new in the pipeline: `PATCH …/tasks/{taskId}` is a `{spaceId}` state-changing route. Wire cases on **both** surfaces: same key twice → one change, identical bodies (a second request with the same key after a **different** edit by another key still replays the first answer); same key, different body → `422`; Account B's key does not replay A's record.

- [ ] **Task 8 — Isolation rows, including the Task-only two-layer row** (AC: 8)
  - [ ] Add rows from the "Isolation rows" table in Dev Notes, each covering **both** surfaces, each sending a request on the exact route constant it names. Seed through the product (`SeedProjectWithTaskAsync` returns `(ProjectId, TaskId)`).
  - [ ] **`ISO-TASK-FOREIGN-ID-IN-OWN-SPACE` (`deferred-work.md:485`)**: A asks `GET /spaces/{SA}/tasks/{TB}`, anchored on `GET /spaces/{SA}/tasks/{random}`; `SameAsAnchor(404, not-available)`, no refusal row. Plant, one at a time, original held in memory and restored in a `finally`: drop **only** the `Task` query filter **and** the `Task` FILTER predicate (Project layers intact) → the row fails on both surfaces. Then each layer alone → green (the one-layer anchors). Close `:485` with the result.
  - [ ] The same foreign-id row for `PATCH` must also show that nothing was written in `SB` (B's Task title unchanged).
  - [ ] `ProductActor.SendAsync` sends a default `{name, title}` body on `PATCH`, which is a valid edit; a row that needs a Status or due date passes its own body.
  - [ ] No new timing row is required: `ISO-TIMING-IN-SPACE` and `NotAvailableFloor` already cover the in-Space not-found; the floor applies to the new routes because they are under the API group. If the dev phase adds a timing row, it runs in `TimingCollection` (`DisableParallelization = true`).
  - [ ] GATE-003: no row pending on 2.3 at `review`.

- [ ] **Task 9 — The Task detail panel** (AC: 1, 2, 3, 5, 6, 7, 12, 13, 14)
  - [ ] **Opening.** `TaskCard` becomes openable (`TaskCard.razor` comment: "No click target (the detail panel is 2.3)"). Per Q7: a native `<button>` inside the card whose accessible name is the title, carrying `data-task-id`. Click or Enter/Space opens the panel. 2.8 later converts cards to the roving-tabindex grid. **Viewers open it too** (`EXPERIENCE.md:199`: "For a Viewer: click still opens").
  - [ ] **The panel** (`Yello.Client/Components/TaskDetail.razor`, per Q6 rendered by `BoardView`, not a route): over the Board, one level deep; a `role="dialog"` with `aria-modal="true"` and `aria-labelledby` the panel's title heading; `h2`/`type-dialog-title`; `Esc` closes; focus moves into the panel on open and is **trapped** (Tab and Shift+Tab cycle within it); on close focus returns to the originating card's button (look it up by `data-task-id`; if the card is gone, its column — `EXPERIENCE.md:403`). A close control labelled from `ClientCopy.resx` ("Close"). **Never stacked**: opening another card while one is open replaces the content, it does not stack. No `inert` attribute and no mock-baked uppercase strings (the mock `task-detail.html` loses to the spine, `DESIGN.md:482`).
  - [ ] **Contents** ("attributes above", `DESIGN.md:471`): title, Status, due date. **No description region, no heading for one, no placeholder** (AC6). No Labels (2.4), no Assignee (4.6), no Move control (2.6), no delete (2.5).
  - [ ] **Title** (Q9): an input with `autocomplete="off"` and a random field name (2.1's `FormField`). Commit on Enter or blur when the trimmed value differs; `Esc` inside the field reverts the field first (innermost meaning wins, `EXPERIENCE.md:318`), and a second `Esc` closes the panel. Validation before sending uses the same 1–200 rule as the server (a mirrored predicate held to `TaskTitle` by an Architecture gate, as `TaskCapabilities` is held to the matrix); the server still decides.
  - [ ] **Status** (UX-DR11, `EXPERIENCE.md:211`): the picker offers only the Project's effective set, from the Board's own server answer (its columns). An in-panel listbox/popover, **not** a second dialog. The current Status is shown as current (`aria-current` or `aria-checked`) but nothing is **pre-selected as a destination**. Choosing a Status commits it. The card moves to the end of the new column at once and both column totals change at once (optimistic), and revert together.
  - [ ] **Due date** (Q4): `<input type="date">` with its label; choosing a date commits; a "Clear due date" control commits `null`. Rendered in the viewer's locale and zone (`CultureInfo.CurrentCulture`, browser time zone). The card's metadata row shows the due date in monospace (`typography.meta`) when present (`task-detail.html` "DUE 22 AUG" shape; uppercase only via `text-transform`).
  - [ ] **Optimism and revert (AC1, AC12)**: each attribute keeps its last **server-admitted** value. A change renders at once in the panel and on the card. The `PATCH` carries only the changed member(s) and an `Idempotency-Key` (reused on a resend after no answer, 2.1 review R3). On `200`, the admitted value becomes the server's answer. On refusal, revert that attribute to the last admitted value — **not** to whatever the panel showed before, if a later edit of a different attribute is outstanding (Q8). On `404`, revert and then render the page's one `RefusalSurface`. No answer: per Q8. Edits to the same Task are sent in order; a second edit of the same attribute while one is outstanding waits or supersedes per Q8 — never two writes racing for one field from one tab.
  - [ ] **No save, no indicator (AC2)**: no control whose name contains "Save", no "Saving…", "Saved", spinner or tick tied to an edit. A refusal message is not an indicator. Browser case with a plant (a "Saved" text) — caught.
  - [ ] **Viewer (AC3)**: the same panel, with the title as text, Status and due date as text; no input, no picker, no clear control, no button but Close. Absent, not disabled (UX-DR27): browser case asserts `main [disabled], main [aria-disabled='true'], [role=dialog] [disabled]` count 0. Plant: a Viewer offered the title input — caught. `TaskCapabilities.PermitsManageTasks` decides, held to the matrix by the existing ARCH gate.
  - [ ] **Role lowered while open (AC14)**: the Role re-read on re-arrival (`SpaceArrival`) re-renders the open panel without its edit controls, in place.
  - [ ] **Purge**: the panel's state belongs to `BoardView`, which is purged with the Space (`ActiveSpace.RegisterPurge`). Nothing in browser storage. `document.title` stays `Yello`. The URL does not change on open (Q6).
  - [ ] **Announcements**: a Status change announces politely, once, per Q10. Nothing else is announced on a successful edit; refusals are rendered in an always-present `role="status"` region (2.2 review lesson: insert empty regions up front).
  - [ ] **CSS** (`components.css`): `.task-detail` with `border-radius: var(--radius-lg)` (6px), `--hairline-width` border, `box-shadow: none` declared, padding `{spacing.5}`, no fixed height (`No_box_is_given_a_fixed_height`), controls at the 24px target floor, the focus ring never removed. The picker takes 3px (`rounded.md`). `prefers-reduced-motion` removes any transition.
  - [ ] **Radius gate (`deferred-work.md:36`)**: add `The_Task_detail_panel_takes_the_6px_radius` (beside `Task_cards_and_columns_take_the_3px_radius` in `DesignFoundationGateTests.Board.cs`), and a picker 3px clause; plant a 3px panel → caught. If 2.3 adds a button, see Q14. Update `:36` with which surfaces remain (Label chips 2.4, the Offer indicator Epic 5, the invitation view Epic 4, the context bar and buttons, the Role chip).
  - [ ] **Text spacing (`deferred-work.md:20` convention)**: add the panel (open, long title, a due date) to `TextSpacingTests` under the 1.4.12 override and 200% text-only zoom.
  - [ ] **Copy**: `ClientCopy.resx` only, sentence case: the panel's close label, field labels ("Title", "Status", "Due date"), "Clear due date", `invalid-task-status` and `invalid-due-date` copy, the Status-change announcement. `RefusalCopy.For` gains branches for the two new types (it falls through to "Not available." today, which would be untrue for a validation refusal). Never "save" or "archive". `LocalisationGateTests` holds the rest.

- [ ] **Task 10 — Slice, schema and wire tests** (AC: 1, 3, 4, 7, 9, 10, 11)
  - [ ] `tests/Yello.Tests.Slices/Tasks/…` (new partials of `TaskEndpointTests`, e.g. `TaskEndpointTests.Edit.cs`, plus `Tasks/EditTask/EditTaskHandlerTests.cs`) on `MigratedDatabaseFixture` and `HostProcess`/`WireSession`. Cases:
    - Edit title, Status, due date, each alone and together; the response and a `SELECT` agree; an absent member is unchanged.
    - Clearing the due date (`null`) stores `NULL`; the wire carries ISO 8601 with offset (`+00:00` or `Z` — assert the exact serialised form Q4 decides).
    - A due date sent with a non-zero offset is stored and returned as the same instant in UTC.
    - Status in the effective set → `200`; a random Status id and a **real `StatusDefinition` id from another Space** → `400 invalid-task-status`, byte-identical, nothing changed, no refusal row (AC4's sibling clause as far as Epic 2 data can reach; Q13).
    - A Status change gives the Task a key after the new column's last key; the old column's other keys are untouched.
    - A Viewer refused `403 manage-tasks` with one `InsufficientRole` row and nothing changed, on **both** surfaces (C-6), with a Viewer added to an Owner's Space.
    - An unknown Task id answers `404` byte-identical to the boundary's, no refusal row.
    - `invalid-task-title` (empty, whitespace, 201 characters), `invalid-due-date`, an unreadable body → `400`, never `500`.
    - `GET` returns the Task with its due date; a Viewer may read it.
  - [ ] **S-2 / AD-17** (`2-2` story: "S-2 proper belongs to 2.3 and Epic 6"): after a run of edits, a query-level invariant asserts no Task holds a Status outside its Project's effective set.
  - [ ] The schema case: `DueDate` is `datetimeoffset` and nullable.
  - [ ] An Architecture gate: no `DateTime` in Domain or Contracts (A-15) if one does not already exist; otherwise confirm it covers `DueDate`.

- [ ] **Task 11 — Browser tests** (AC: 1, 2, 3, 5, 6, 7, 12, 13, 14)
  - [ ] `tests/Yello.Tests.Client/TaskDetailTests.cs` against `FakeYelloApi` (add `PATCH`/`GET` on the Task route, bodies as `Yello.Contracts` types, `HoldAsync`/`Release`). **Hold the `PATCH`** and assert the card and panel already show the new value while it is outstanding (this is the ordering 2.1's rename case rejected, and it is AC1's required behaviour here). Then release with `400`, `403`, `404`, `422` and assert each revert and its copy.
  - [ ] Focus: open from a card → focus in the panel; Tab cycles inside; `Esc` closes; focus is on the originating card's button (assert by `data-task-id`).
  - [ ] Never two deep: opening a second card from within (if reachable) replaces, never stacks — at most one `[role=dialog]`.
  - [ ] No description editor and no save/indicator (planted absence proofs). Viewer panel with no edit affordance (planted). Role lowered while open.
  - [ ] Due date rendered in the browser's locale: set the Playwright context's `Locale` and `TimezoneId` to two different values and assert the rendering follows (AC7).
  - [ ] Never Playwright's URL wait; poll `location.pathname` (`TESTING-CONVENTIONS.md:198-203`).

- [ ] **Task 12 — Plants, runs, ledger, conventions** (AC: all)
  - [ ] Plants run one at a time in the foreground. Hold the original content in memory, restore it in a `finally`, compare the tree hash after each. **Never** `git checkout --`, `git restore`, `git reset` or `git stash`. Write a plant so that it compiles.
  - [ ] Record `| # | Plant | Gate | Caught |` in the Debug Log. Minimum set:
    - A row left `PendingUntil: "2.3"` with 2.3 at `review` (GATE-003).
    - The Task `PATCH` with no isolation row (GATE-002).
    - The Task-only two-layer drop (Task 8), and each layer alone (green).
    - The Status-key lock hint removed (Task 6), three runs.
    - The handler skips the effective-set check (the foreign-Space Status → must not become a `500` or a `200`).
    - `ConsultsBound` on the edit (a case editing in a 5,000-Task Project must catch it).
    - A Viewer offered the title input (client gate and browser case).
    - A "Saved" indicator (browser case).
    - A description region in the panel (browser case).
    - A 3px panel (radius gate).
    - The revert restores the pre-edit display instead of the last admitted value (browser case with two outstanding edits; Q8).
    - The panel left open after a Space purge.
  - [ ] Runs: `dotnet build Yello.slnx` (warnings are errors), then `dotnet test Yello.slnx`. Baseline after the 2.2 review: **749 total, 746 passed, 0 failed, 3 skipped** (`YELLO_MIGRATION_BUNDLE` unset). `--no-build` runs zero tests unless the same configuration was just built. If a Client case fails only in the full run, run `dotnet test --project tests/Yello.Tests.Client` alone before calling it a regression. If Rancher's socket times out: `rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch.
  - [ ] `tests/TESTING-CONVENTIONS.md`: the optimistic-edit browser pattern (hold, assert the new value, release, assert the revert), the new counts, and any new serial collection.
  - [ ] Ledger (`deferred-work.md`): CLOSE `:485` with the plant result. UPDATE `:36` (which surfaces remain), `:492` (the second key-placing writer). ADD anything this story defers, each with an owner — at least the literal sibling-Project Status case (owner 6.1, Q13) and any UX question raised (Q4, Q14).
  - [ ] `sprint-status.yaml`: set `review` at the end of the dev phase. GATE-003 then fails if any row is still pending on 2.3 — that is the gate working.
  - [ ] **Do not add review narration to source files.**

## Dev Notes

### The shape of the story in one paragraph

On the server this is small: one nullable column, a by-id read, a `PATCH` and two problem types, all through the pipeline that already resolves the Space, checks `manage-tasks`, replays keys and floors not-founds. Three things carry the risk. **(1) A Status change is a second key-placing writer**: the position key is unique per column and 2.2's "append after the last key" is safe only because creation holds the bound's lock, which an edit does not take — without Task 6 a Status edit racing a creation is a `500`. **(2) This is the product's first optimistic write**: 2.1 and 2.2 deliberately made rename and create non-optimistic, so the replica, its last-admitted values and the revert are new client ground, and the revert must be per attribute. **(3) The first dialog-shaped surface**: focus trap, `Esc`, return focus, never two deep, and the 6px radius gate. The isolation work is the familiar kind, plus the ledger's Task-only two-layer row.

### Ledger entries — every one folded or stated

| Entry | Severity | Where it lands |
|---|---|---|
| `deferred-work.md:485` a foreign Task id inside one's own Space, failing when only the Task's two layers are dropped | low | **Task 8**, AC8. `ISO-TASK-FOREIGN-ID-IN-OWN-SPACE` on `GET` (and `PATCH`), with the plant. Close the entry |
| `:36` radius mapping — "dialogs and the Task detail panel (2.3)" | low | **Task 9**, AC13. 6px panel clause + 3px picker clause, planted. Update the entry with the remaining surfaces |
| `:20` text spacing — done for Board and Space home by 2.2; later surfaces add their own case | (done) | **Task 9**: the panel joins `TextSpacingTests` |
| `:492` position keys safe only under the bound's lock (owner 2.6) | low | **Task 6**, AC9. 2.3 is the second writer; it serialises its own placement. The entry stays with 2.6 with a dated note |
| GATE-003 / any row pending on 2.3 | — | Banner, Task 1, Task 8, Task 12. None today |

Explicitly **not** this story's, though nearby: `:471` and `:477` (bound lock and same-key-at-limit: 2.9), `:493` (Board totals vs Task read: 2.9 — note an optimistic Status change adjusts totals client-side, so it does not widen that gap), `:487`/`:494` (Board announcements and the count chip's name: 2.8), `:66` (device-pixel snapping: UX — the panel's border uses the gated hairline), the "Board loaded." vs `EXPERIENCE.md:390` conflict (UX).

### Isolation rows (Task 8)

Actors as 1.9 built them: **A** and **B**, each with a Personal Space (`SA`, `SB`), a Session and a Token bound to their own Space. Seed through the product: `PA`/`TA` in `SA`, `PB`/`TB` in `SB`. Route constant `TaskRoutes.Task`.

| Id | Test design | Vector | Request (by A, about B) | Session | ApiToken |
|---|---|---|---|---|---|
| ISO-DIRECT-TASK | I-1 | DirectIdentifier | `GET /spaces/{SB}/tasks/{TB}` | `BoundaryNotFound`: 404 = anchor, one `CrossSpace` row, no `TB` title in the body | same |
| ISO-EDIT-TASK-FOREIGN-SPACE | I-2 | Write | `PATCH /spaces/{SB}/tasks/{TB}` | 404 = anchor, `CrossSpace` row, `TB` unchanged | same (a Token is admitted on this route: the boundary 404, not `401`) |
| ISO-TASK-FOREIGN-ID-IN-OWN-SPACE | I-4, I-5; `:485` | DeepLink | `GET /spaces/{SA}/tasks/{TB}` vs `GET /spaces/{SA}/tasks/{random}` | `SameAsAnchor(404, not-available)`; **no** refusal row; **fails when only the Task's filter + FILTER predicate are dropped** | same |
| ISO-EDIT-TASK-FOREIGN-ID-IN-OWN-SPACE | I-4, I-5 | DeepLink | `PATCH /spaces/{SA}/tasks/{TB}` vs a random id | same bytes as a random id; no refusal row; nothing written in either Space | same |
| ISO-RECORD-KIND-TASK-EDIT-VIEWER *(optional if Task 10 covers it on both surfaces)* | V-1, C-6 | RecordKind | a Viewer in `SA`: `PATCH /spaces/{SA}/tasks/{TA}` | `RoleRefused("manage-tasks")`, one `InsufficientRole` row | same |

I-13 applies to every refusal: no Space name, Project name or Task title in the body, and no id that was not already in the URL. Revocation suite: nothing to add (`RevocationCases` is event-driven against `SpaceRoutes.Active`).

### Current state of the files this story changes — read each before editing

| File | Today | This story | Preserve |
|---|---|---|---|
| `Yello.Domain/Tasks/TaskItem.cs` | `Id`, `SpaceId` (init), `ProjectId`, `StatusId`, `Title` ("Settable for story 2.3's edit"), `PositionKey`, `CreatedAt`; no due date, no description | `DueDate` (`DateTimeOffset?`) | `SpaceId` init-only; no description; no navigation properties |
| `Yello.Domain/Tasks/ITaskStore.cs` | `CreateAsync` only | `FindAsync`, edit | `null` = not visible |
| `Yello.Domain/Tasks/TaskTitle.cs`, `PositionKey.cs` | 1–200 trimmed; `After(last)` base-62 | Reused | Ordinal ordering |
| `Yello.Domain/Statuses/EffectiveStatusSet.cs` | `Of(spaceDefaults)`, "the one place the derivation lives" | A membership predicate | 6.1 adds the delta here |
| `Yello.Infrastructure/Persistence/TaskStore.cs` | Create: visibility check, last key, append, save | Find; edit; the locked last-key read on a Status change | No `Where SpaceId`; saves its own effect; `null` for a foreign Project |
| `Yello.Infrastructure/Persistence/ProjectStore.cs` | `RenameAsync`: `ExecuteUpdateAsync`, 0 rows → `null`, re-read | The pattern for the edit (or a tracked load + save — either way the store saves) | — |
| `Yello.Infrastructure/Persistence/Configurations/TaskConfiguration.cs` | Columns, composite FKs, unique key index | `DueDate` | Everything else |
| `Yello.Host/Endpoints/TaskEndpoints.cs` | `MapCreateTask` + `CreateAsync` + `Response(task)` | `MapViewTask`, `MapEditTask`; `Response` gains `DueDate` | `Location` on headers, never `Results.Created` (ARCH-009); body read inside |
| `Yello.Host/Program.cs` (~366-381) | `api.MapCreateTask(); api.MapBoard();` | The two new maps | `MapUnmatchedApi()` after; `SpaceScopedRouteCheck` |
| `Yello.Contracts/Tasks/TaskRoutes.cs`, `TaskResponse.cs`, `ProblemTypes.cs` | `Create`, `CreateIn`, `TaskOf`; 5-field response | `Task` pattern; `DueDate`; two types | No `SpaceId` in any response |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Tasks.cs` | Pins the Task request set and the response set | Widened deliberately (Task 4) | The creation request still carries only `Title` |
| `Yello.Client/Components/TaskCard.razor` | `<li>` with title `<p>` and empty meta; "NOT A CONTROL YET" | An opening button with `data-task-id`; the due date in meta | No shadow; 3px; no drag handle/Move (2.6/2.7) |
| `Yello.Client/Components/BoardView.razor`, `BoardColumn.razor` | Columns of `TaskResponse`; `_generation` guard; purge; one polite region | Owns the panel state; optimistic move between columns with total adjustment; revert | Purge order; skeleton; `_canManage` re-read |
| `Yello.Client/Refusals/RefusalCopy.cs`, `Localisation/ClientCopy.resx` | Unknown types → "Not available." | Two new types; panel copy | Type decides, prose never |
| `Yello.Client/Spaces/SpaceWrites.cs` | `SendAsync(method, path, body, key)` with antiforgery retry | Used for `PATCH` | Key reuse after no answer |
| `tests/Yello.Tests.Client/FakeYelloApi.cs` | Board, Task create, `HoldBoardAsync` | Task `GET`/`PATCH`, hold/release, refusal injection | Bodies are Contracts types; literal codes where the client project cannot be referenced |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 2.2's rows live; none pending | The rows above | Every row states both surfaces; no case branches on surface |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.Board.cs` | 3px cards and columns; "detail panel stays with 2.3" | 6px panel, 3px picker | Gates scan the repository, not known files |

### Architecture constraints that bind here

- **AR-3**: authorisation, Space resolution, refusal recording, idempotency and bounds live in the pipeline. A slice that re-implements any of them is a defect.
- **AD-1 / AR-4**: the Role is read per request; no slice reads it.
- **AD-3**: `{spaceId}` first after the version; **no bare `/tasks/{id}` route** (A-4). The in-Space not-found is identical to the boundary 404 in body and duration (the floor holds the duration).
- **AD-4**: one pipeline, both surfaces; no slice branches on surface.
- **AD-11 / AR-20**: the client edits a replica and never blocks; the server admits or rejects; a rejected change is reverted. The client is never the arbiter.
- **AD-15**: one key per Task, unique within `(ProjectId, StatusId)`; a move writes only the moved Task's key; readable, never writable — the edit request carries no position key.
- **AD-16 / AD-17**: the effective set is derived on read (cached only within a request) and no Task holds a Status outside it.
- **AD-18**: `Idempotency-Key` on the `PATCH`.
- **AD-20**: only `403`s and boundary `404`s are recorded; validation `400`s and in-Space not-founds are not.
- **AD-19 / FR-37**: adding `dueDate` to `TaskResponse` is legal in v1; removing or retyping a field is not.
- **AD-12/AD-13**: no description column, no whole-text writes; last-writer-wins is banned **for the description**, which this story does not touch.
- **AR-34**: `DateTimeOffset` UTC, ISO 8601 with offset; RFC 9457 with a stable `type`; mutation only through a slice in one transaction; invariants in Domain.
- **UX-DR26**: no save button, no auto-save indicator, no modal stack deeper than one, no hover-only affordance, no Role-disabled control; optimistic by default except destructive or permission changes.
- **UX-DR27**: absent, not disabled. **UX-DR31/33**: purge with the Space; no title in `document.title` or a URL; `autocomplete="off"`.

### Testing requirements

Conventions (`tests/TESTING-CONVENTIONS.md`):
- Underscore-sentence names, one behaviour each. Traits `Suite`, `Priority`, `Requirement` — cite the FR/AR **and** the AD (`FR-20`, `AR-20`, `AD-11`; `AD-17` for S-2).
- Randomised data. Cleanup by database disposal. Every absence anchored on a presence and validated by a plant.
- **No `Task.Delay`.** Browser: `HoldAsync`/`Release`. Server races: hold the product's lock and poll `sys.dm_exec_requests` for `LCK_M_%` (`TaskWire.WaitUntilBlockedOnALockAsync`) — never `Task.WhenAll` and hope.
- **Machine-memory lesson from 2.2 (`09d1285`): heavy parallel Slices tests — large seeds such as the 5,000-Task Project, and lock-holding race cases — starve timing-sensitive tests on the shared SQL engine.** 3 of 5 full runs failed `SpaceBoundaryPipelineTests`' duration cases until Lee had the class serialised. **Any duration or timing test this story adds must live in a `DisableParallelization = true` collection**: `BoundaryDurationCollection` (`tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs:565-566`) in Slices, or `TimingCollection` (`tests/Yello.Tests.Isolation/IsolationTimingTests.cs:85`) in Isolation. Keep the method (21 samples, median, 20%) unchanged. Task 6's race cases hold locks — keep them short and do not add a 5,000-row seed beyond the one case that needs it (the `ConsultsBound` plant).
- Run plants in small batches; do not run the full suite concurrently with a plant (2.1 was stopped for low memory).

Where each kind of test goes:
- **Slices:** `tests/Yello.Tests.Slices/Tasks/…` on the process's one engine (`MigratedDatabaseFixture`). No new `SqlServerContainerFixture` (`ContainerTopologyGateTests`).
- **Isolation:** rows in `IsolationCases.cs`; the generated theories run them.
- **Architecture:** contract-shape gates, radius gate, client predicate gates (only this suite sees the client assemblies).
- **Client:** `Yello.Tests.Client` against `FakeYelloApi`; `RealHostCollection` only if a real-Host case is needed. Build first: the suite runs `--no-build`.

### Previous story intelligence (2.2, 2.1)

- **2.2's create endpoint and 2.1's rename are the templates**: `RequiresCapability`, body read inside the endpoint (`JsonRequestBody.ReadAsync`, which already catches `JsonException` and `InvalidOperationException`), `BoundaryProblems.NotAvailable()` for `null`, `Results.Json`/`Results.Ok`.
- **2.2 review lessons, all apply to the panel**: a timeout is a failure, not a purge; keep focused controls in the DOM while a request is out (focus fell to `<body>` when "Add" was removed); live regions exist empty from the start; keep typed text that arrived after a send; keep the `Idempotency-Key` until the answer is read; a refused page discards stale heading-focus requests; only a `404` refuses a re-arrival.
- **2.1 Q11**: rename was made non-optimistic because a browser case saw the change before the request reached the fake. In 2.3 that ordering is the asserted behaviour (AC1). Do not copy rename's non-optimistic client.
- **2.2 found**: Blazor does not re-set a page's parameters on navigation to the same address, so the Role re-read is driven by `NavigationManager.LocationChanged` in `SpaceArrival` — the panel inherits it through `BoardView`.
- **2.2 Q7**: `NotAvailableFloor` (50 ms default) holds every Space-scoped `404`; the new routes are inside the API group and inherit it. Do not add a second floor.
- **2.2 P3**: dropping only the Task's two layers leaked nothing because every 2.2 read was narrowed by `ProjectId`. The by-id `GET` is the first read that is not — that is why `:485` lands here.
- **2.2 Q2 / `:492`**: keys are appended under the bound's lock. 2.3's Status change must bring its own lock (Task 6).
- **1.10**: migrations are expand/contract; the bundle-equivalence test compares predicates and indexes; database unavailability is a uniform `503` — do not catch `SqlException` in new stores.

### Git intelligence

- Recent: `f37b9b2` story 2.2 → `1395c0a` ATDD scaffolds → `2cb17a5` dev (Task table, Board, both surfaces) → `09d1285` review (idempotent replay of non-JSON bodies refused at capture; `BoundaryDurationCollection`). Commits are per phase, straight to `main`, by the orchestrator. **Do not commit, push, branch or stash.**
- The ATDD phase may run before dev and leave red-phase scaffolds (`[Fact(Skip = "red: AC<n> - … (Task <k>; Q<n>)")]`, isolation rows `PendingUntil: "2.3"`, possibly a temporary `TaskRouteScaffold`-like class in `Yello.Tests.Shared` that is deleted on activation). Activate them one at a time; correct a wrong scaffold and record the correction, never delete it.

### Latest technical notes (pinned versions; no upgrades, no new packages)

- **.NET 10 / EF Core 10:** `DateTimeOffset?` maps to `datetimeoffset` nullable. `ExecuteUpdateAsync(s => s.SetProperty(...))` is how `ProjectStore.RenameAsync` edits; a tracked load + `SaveChangesAsync` also works and makes the conditional key write simpler. `Database.SqlQuery<string>($"… WHERE ProjectId = {p} AND StatusId = {s}")` parameterises the interpolation.
- **System.Text.Json:** a positional record cannot tell an absent member from `null`. For merge-patch semantics (Q1) read the body as `JsonDocument`/`JsonElement` and test `TryGetProperty`, or use explicit "set" flags. `DateTimeOffset` serialises as ISO 8601 with offset by default (`2026-10-25T00:00:00+00:00`).
- **SQL Server:** `UPDLOCK, HOLDLOCK` on a range under RLS gives `RangeS-U` key-range locks within the `SpaceId`-leading index; waits show as `LCK_M_RS_U` / `LCK_M_RIn_NL`, both under `LIKE 'LCK_M_%'`.
- **Blazor WebAssembly:** no built-in focus trap. Implement with a keydown handler on the panel for Tab/Shift+Tab over its focusable elements (or a sentinel pair), plus `ElementReference.FocusAsync()`; JS interop only if needed, through the existing pattern. `@key` on the panel by Task id. `TimeZoneInfo.Local` in the browser is the browser's zone; `CultureInfo.CurrentCulture` follows the configured culture provider.
- **Playwright for .NET:** `Browser.NewContextAsync(new() { Locale = "…", TimezoneId = "…" })` for AC7's rendering case.
- No web research was required: every library is pinned (`Directory.Packages.props`, `PackageVersionPinTests`), and this story adds none.

### Project structure notes

- **New, most likely:**
  - `Yello.Domain/Tasks/TaskDueDate.cs`
  - `Yello.Application/Tasks/ViewTask/ViewTaskHandler.cs`, `Yello.Application/Tasks/EditTask/EditTaskHandler.cs`
  - `Yello.Contracts/Tasks/EditTaskRequest.cs`
  - `Yello.Infrastructure/Persistence/Migrations/…_TaskDueDate.cs` (+ Designer, snapshot)
  - `Yello.Client/Components/TaskDetail.razor` (+ a picker component if not inline)
  - `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Edit.cs`, `Tasks/EditTask/EditTaskHandlerTests.cs`, `tests/Yello.Tests.Client/TaskDetailTests.cs`
- **Modified:** per the current-state table, plus `components.css`, `TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml` and this file.
- **Ring check (AD-21):** Domain references nothing; no ASP.NET type in Application; the client cannot reference Domain (hence gate-held mirrored predicates).
- **CLR name**: the entity is `TaskItem` (2.2 Q1); the table, routes and copy say "Task".

### References

- `_bmad-output/planning-artifacts/epics.md`: 113 (FR-20), 208 (AR-20), 222 (AR-34), 240-286 (UX-DR5, 10, 11, 12, 26, 27, 31-33, 37, 39, 40, 42), 313, 356 (coverage), 437 (no description), 969-973, 1059-1095 (Story 2.3), 1132-1210 (2.5, 2.6 boundaries).
- `prds/prd-YelloBMAD-2026-08-15/prd.md`: 66, 387 (optional due date), 400-406 (FR-20), 444, 538, 624 (FR-37), 755 (NFR-9). `addendum.md`: 107.
- `architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`: 75, 88-100 (AD-1, AD-3, AD-4), 138-154 (AD-11…AD-13), 162-198 (AD-15…AD-20), 263-277 (conventions), 413, 424.
- `ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md`: 27, 52, 67, 83-87, 99-147, 164-177, 199, 206-211, 221, 271, 287-291, 318, 324, 335-336, 370-403, 435-447. `DESIGN.md`: 207-262 (tokens incl. `task-detail` 238-244, `picker` 256-262), 439, 465-473, 482, 497-519. Mock `mockups/task-detail.html` 79-89 (loses to the spine).
- `specs/spec-yello/SPEC.md` (CAP-20), `decisions-settled.md:75`.
- `test-artifacts/test-design-qa.md`: 71, 265-277 (I-1…I-13), 302, 314, 317, 319 (S-2), 350, 354, 363, 367, 369, 373, 378, 389.
- `implementation-readiness-report-2026-08-22.md`: 499, 932-933, 973.
- `sprint-change-proposal-2026-09-13.md`: nothing affecting 2.3 (86-91).
- `tests/TESTING-CONVENTIONS.md`: 30-72, 86-129, 137-203, 233-273, 309-329, 344-381, 416-445, 494-583.
- `implementation-artifacts/deferred-work.md`: 20, 36, 66, 471, 485, 492, 493. `2-2-add-tasks-and-see-them-on-a-board.md`: Tasks 2, 6-10, Review Findings, Q1-Q11, Debug Log plants. `2-1-…md`: Q11 (non-optimistic rename).

### Open questions — each with a recommended decision

The dev phase takes these as **reversible, recorded decisions** in the Completion Notes. None blocks starting.

1. **The edit's wire shape.**
   - (a) One `PATCH /spaces/{s}/tasks/{t}` with JSON merge-patch semantics: an absent member is unchanged; `dueDate: null` clears; `title`/`statusId` present as `null` → `400`. The client sends only the changed member.
   - (b) `PUT` of all three attributes. Two quick edits of different fields from one tab would then overwrite each other with stale values.
   - (c) One route per attribute. Triples the isolation rows for no gain.

   **Recommend (a).** Matches 2.1's `PATCH` rename and keeps per-attribute revert honest. A body with no recognised member is `400` (nothing to admit).
2. **Concurrency between two editors.** No rowversion, ETag or If-Match exists, and AD-12's ban on last-writer-wins is scoped to the description.
   - (a) Last writer wins **per attribute**, no concurrency token; "rejected" means refused by authorisation, validation or boundary. Live propagation of others' edits is Epic 7.
   - (b) A rowversion with `If-Match` and a `412`. UX-DR26 bans "stale-content warnings", so the client could only revert silently.

   **Recommend (a)**, recorded as a decision; the merge-patch shape keeps it per attribute rather than per Task.
3. **Where a Status change puts the Task.** (a) The end of the new column, a fresh key under a lock (Task 6). (b) The top. (c) Keep the old key (collides with the unique index). **Recommend (a)**; position within a column is 2.6's Move control.
4. **Due date semantics.** The AC says `DateTimeOffset` UTC rendered in the viewer's zone; the UX shows a date only ("DUE 22 AUG"); nothing specifies time of day.
   - (a) `<input type="date">`; store the **start of that day in the editor's zone** converted to UTC; render the date part in each viewer's zone. Satisfies the AC literally; a viewer in another zone can see the adjacent day.
   - (b) Store UTC midnight and render the date in UTC. Stable across zones but contradicts "viewer's own zone".
   - (c) `datetime-local`, an explicit instant. Heavier than the mock.

   **Recommend (a)**, Epic 2 being solo-user; accept any instant on the API (normalised to UTC) within a sane range (for example years 2000–2100, `invalid-due-date` outside). Add a UX ledger entry asking whether the due date is a day or an instant before Epic 4 makes Spaces multi-person.
5. **Status codes and types for validation refusals.** (a) `400` with `invalid-task-title`, `invalid-task-status`, `invalid-due-date`, mirroring `invalid-task-title`; random and foreign Status ids byte-identical. (b) `422`. (c) A `404` for an unknown Status. **Recommend (a)**; a `404` would read as "the Task is absent", which it is not.
6. **Is the panel a route?** (a) Board state only: the URL does not change, the panel is purged with the Board, and the Task data comes from the Board's own server answer on this load (`GET` exists for the API and the isolation row). (b) A route `/spaces/{s}/projects/{p}/tasks/{t}`: deep-linkable, but every arrival must `GET` before rendering (UX-DR31), the Task's Project can change later (FR-41), and back-button semantics need deciding. **Recommend (a).** Re-reading the Task with `GET` on open is optional; if done, render nothing from it until it answers.
7. **The card's open control before 2.8's grid.** (a) A native `<button>` inside each card, accessible name = title, `data-task-id`, one tab stop per card until 2.8 converts to roving tabindex. (b) Make the whole `<li>` clickable with `tabindex=0` (not a native control; Enter/Space handling by hand). **Recommend (a).** Record that 2.8 owns the conversion.
8. **The replica under overlapping edits and no answer.**
   - Each attribute holds its last admitted value. A refusal reverts **that attribute** to it, leaving other outstanding edits alone.
   - Edits of the same attribute are serialised per Task: the next waits for the previous answer, and the latest local value wins the queue (intermediate values are dropped, not sent).
   - **No answer** (timeout, network): keep the `Idempotency-Key`, revert the attribute to its last admitted value, and show a "Not sent." refusal inline; the person's next change of that attribute resends with the key only if the body is identical, otherwise a new key.

   **Recommend these three.** The revert on no answer honours "the client is never the arbiter"; a later Board load shows the truth.
9. **Title commit.** (a) Enter or blur commits; `Esc` reverts the field, a second `Esc` closes the panel. (b) An explicit "Rename"/"Done" button — a save button in all but name, banned. **Recommend (a).**
10. **Announcing a Status change.** (a) Polite, once, "Moved to {Status}." (UX-DR25 shape without position, which is 2.6's). (b) Silent. **Recommend (a)**; the Board announces deliberate moves (`EXPERIENCE.md:383-390`). Title and due-date edits announce nothing.
11. **The picker inside the panel.** An in-panel listbox/popover (not a dialog), included in the focus trap; `Esc` closes the innermost layer first. **Recommend this**; it keeps AC5's "never two deep".
12. **Slice name.** `Tasks/EditTask` vs `Tasks/EditTaskAttributes` (FR-20's title is "Edit Task attributes"). **Recommend `EditTaskAttributes`**, AR-3's imperative-from-FR-title rule; either is reversible by a rename.
13. **AC4's sibling-Project clause before 6.1.** Every Project's effective set is the Space defaults until 6.1 adds deltas (readiness report `:932-933`), so no Status is "valid in a sibling Project but absent from this one". (a) Validate against the Task's Project's effective set now (correct by construction once 6.1 adds deltas), prove it with a random id and a real foreign-Space Status id, and add a ledger entry, owner **6.1**, for the literal sibling case. (b) Fabricate a delta table now. **Recommend (a).**
14. **Button radius.** DESIGN.md prose and UX-DR5 say buttons are 3px; the button tokens say `rounded.sm` 2px (`DESIGN.md:207, 213, 220`). Relevant only if the panel adds a button (Close, Clear due date). **Recommend:** use the existing button styles unchanged, and add a UX ledger entry for the token/prose mismatch rather than fixing it silently.

## Dev Agent Record

### Agent Model Used

### Debug Log References

**Create-story phase (2026-10-25).**
- `dotnet build Yello.slnx` before the status change: 0 warnings, 0 errors.
- After setting `2-3-edit-a-tasks-title-status-and-due-date: ready-for-dev`: `dotnet build Yello.slnx` 0 warnings, 0 errors; `dotnet test --project tests/Yello.Tests.Isolation` **71 total, 71 passed, 0 failed, 0 skipped** (52 s).
- No isolation row is pending on 2.3 (grepped `PendingUntil`). GATE-003 permits pending rows while 2.3 is `ready-for-dev`.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List
