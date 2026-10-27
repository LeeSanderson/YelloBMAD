---
baseline_commit: 12eba8d
---

# Story 2.6: Move and reorder a Task by explicit control

Status: ready-for-dev

Epic: 2 — Track your own work on a Board
Story key: `2-6-move-and-reorder-a-task-by-explicit-control`
Requirements owned (Story Coverage Index, `epics.md:359`): **FR-29** (move between columns and reorder within one; a Viewer cannot; the column's Status is set; concurrent moves converge and none is silently discarded; order survives reload — `prd.md:526-534`, `epics.md:129`, SPEC CAP-29 `SPEC.md:149-151`, `acceptance-criteria.md:298-303`), **AR-21** (jittered fractional key, unique per `(ProjectId, StatusId)`, a move writes the moved Task's key alone, BIN2 on the column and the index, readable but not writable — `epics.md:209`), **UX-DR21** (the Move control: plain, canonical, at every breakpoint, never hover-only, never second to a gesture — `epics.md:262`, `EXPERIENCE.md:283-295`), **UX-DR25** (move announcements, `epics.md:266`, `EXPERIENCE.md:322-324`).
Also delivered here: **FR-35's position clause** (readable, not writable over the API — `prd.md:594, 605`, `acceptance-criteria.md:356`, `decisions-settled.md:85`; test design **C-5**, `test-design-qa.md:359`); risk **R7** / scenario **A-6** (BIN2 asserted on the column *and* the index, plus the mixed-case ordering case — `test-design-architecture.md:217, 506-520`, `test-design-qa.md:304`).
Also binding: **AD-15** (`ARCHITECTURE-SPINE.md:162-168`), **AD-11 / AR-20** (optimistic local replica, a rejected change reverted — spine `:138-142`, `epics.md:208`), **AD-29 / AR-22** (true column totals; the `PositionKey` keyset seek 2.9 will page on — spine `:249-259`), **AD-2/AD-3/AD-4/AD-20** (both isolation layers; byte-identical floored `404`; no slice branches on surface), **AD-7** (anti-forgery), **AD-18 / AR-25** (`Idempotency-Key`), **AD-19** (contract snapshot), **AR-34** (RFC 9457 stable `type`, one transaction, mutation only through a slice), **AR-35** (isolation rows on both surfaces), **NFR-5** (a retried write is not applied twice), **NFR-9** (Board and Task editor are WCAG 2.1 AA flows), **UX-DR6** (motion: reflow only for cards that moved; reduced motion), **UX-DR11** (one picker, no default where consequential), **UX-DR12** (one level deep, `Esc`, focus returned), **UX-DR24** (`Enter`/context-menu key), **UX-DR27** (absent, not disabled), **UX-DR32** (refusal surface), **UX-DR38** (announce on deliberate action only), **UX-DR39** (focus destinations), **UX-DR41** (breakpoints).
Explicitly **not** here: dragging (**2.7**), the keyboard pick-up/arrow grammar and `role="grid"` (**2.8** — it reuses this story's server move and its "two concrete neighbours" rule, `epics.md:1279-1280`), paging and `aria-setsize`/`aria-posinset` (**2.9**), the small-viewport pager following a move (**2.11**, `EXPERIENCE.md:320`), moving a Task to **another Project** (**6.5**, FR-41, AD-17, slice `Tasks/MoveTaskToProject/` — a 2.6 move never changes `ProjectId`), live observation of another participant's move (**Epic 7**, `epics.md:1196`).
Ledger entries owned by this story (`deferred-work.md`): **`:494`** (position keys safe only under the bound's lock; the midpoint generator; "2.6's moves take the same range first"), **`:543`** (three of 2.5's deletion row-lock hints masked 0/3 — find a race that decides them or remove them), **`:550`** (deletion reads `ProjectId` unlocked — correct only while a Task cannot change Project), **`:198`** (the email unique index takes the default collation — assigned to 2.6 by Lee on 2026-09-10 as "the same decision" as AD-15's BIN2). Code hooks naming 2.6: `Yello.Domain/Tasks/PositionKey.cs:9-11, 36`; `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Schema.cs:20`; `tests/Yello.Tests.Shared/SqlServerContainerFixture.cs:26, 94` and `Yello.AppHost/Program.cs:151` (engine digest pin, Q13); `Yello.Domain/Accounts/EmailAddressNormalisation.cs:32, 37`; `TaskCard.razor:22`, `TaskDetail.razor:22`, `BoardTests.cs:140` (Move control absent "until 2.6").
Depends on: **2.2** (Task, `PositionKey`, BIN2 column, Board), **2.3** (Task detail panel, Status picker `PickerOption`, per-attribute replica, `TaskStore` range lock, "Moved to {0}."), **2.4** (`INDEX(1)` doctrine, Labels on cards), **2.5** (range-before-row lock order for every Task writer, column focus target, `_deleted` handling), all done.

> **Two isolation gates bind this story. Read both before writing a route.**
> 1. **1.9-GATE-002** (`IsolationCaseListTests.Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case`) fails for any `/api` route the Host maps that no `IsolationCases` row names by exact `(Method, Route)`. **The move route (Q1) needs rows with an expectation on both surfaces (`Session` and `ApiToken`) in the same change that maps it** — for a Session-only route the `ApiToken` expectation is `NoCredential` (the `ISO-WRITE-TOKEN` precedent, `IsolationCases.cs:69-77`). A row must actually send a request on the route it names (`CaseRun.cs:38`).
> 2. **1.9-GATE-003** (`Every_pending_row_names_a_story_that_has_not_yet_reached_review`) lets a row stay `PendingUntil: "2.6"` only while 2.6 is `backlog`, `ready-for-dev` or `in-progress`. Today no row is pending on 2.6 (grepped 2026-10-26). The ATDD phase may add some; every one must be live before the dev phase sets `review`.
>
> This create-story change moves 2.6 to `ready-for-dev`, which GATE-003 permits.

## Story

As someone whose work moves between states,
I want a plain control that moves a Task to another Status and position,
so that moving work never depends on a gesture I cannot make.

## Acceptance Criteria

AC1–AC7 are reproduced **verbatim** from `epics.md:1174-1209` (`status: final`), numbered by position. AC8–AC16 come from the PRD, the architecture, the UX spine, the test design, the ledger and the 1.9 gates; each cites its source.

**AC1 — the Move control opens a picker with no default**
**Given** a Task
**When** its Move control is opened — from Task detail, or from the Task's context menu on the Board by pointer, by `Enter` on a focused card, or by the platform context-menu key
**Then** a picker names the destination Status and the position within it
**And** it carries no default selection, because the choice is consequential

**AC2 — present everywhere, absent only for a Role that cannot move**
**Given** the Move control
**When** any breakpoint renders
**Then** it is present at every one, never hover-only, and never offered second to a gesture
**And** it is absent only for a Role that cannot move Tasks at all

**AC3 — one key written, no renumber**
**Given** a Task moved to a column
**When** the move commits
**Then** its Status becomes that column's Status, and a position key is written for the moved Task alone — never a renumber of its siblings

**AC4 — BIN2 on the column and the index**
**Given** the position key column
**When** the migration is inspected
**Then** it is declared `COLLATE Latin1_General_100_BIN2`, and the `(ProjectId, StatusId, PositionKey)` index carries the same collation
**And** this is set at column definition because `ALTER DATABASE … COLLATE` is unsupported on Azure SQL and cannot be retrofitted, while a case-insensitive comparison would make a mixed-case fractional index non-deterministic in `ORDER BY` and falsely duplicate on insert

**AC5 — concurrent moves converge**
**Given** two concurrent moves into the same column from two Sessions
**When** both complete
**Then** neither is silently discarded and both converge to one order, verified after reload
**And** live observation of the other participant's move arrives with the sync push in Epic 7

**AC6 — order survives reload**
**Given** a completed move
**When** the page is reloaded
**Then** the order within each column is exactly as it was left, because ordering is per column and survives reload

**AC7 — announced**
**Given** any move
**When** it commits
**Then** an ARIA live region announces the destination Status, the position ordinal and the column total — "Moved to In Progress, position 3 of 12." — because for a blind User this announcement replaces the entire visual drop-zone system
**And** a cancelled move announces the restoration — "Returned to Todo, position 7."

**AC8 — readable, not writable, over the API** *(`epics.md:1207-1209` verbatim; FR-35 `prd.md:605`; AR-21; C-5 `test-design-qa.md:359`)*
**Given** a Task's position within its Status
**When** it is read over the API
**Then** it is readable so a consumer can reproduce what a User sees, and it is not writable, which keeps the convergence requirement confined to one surface
**And** concretely: `TaskResponse.positionKey` is served on both surfaces (it already is, `TaskResponse.cs:19,26`); no request on the `ApiToken` surface can choose or change a Task's position within a column (the move route answers a Token as no credential, Q1; a `PATCH` carrying a `positionKey` member changes no key and answers its documented outcome); a Status change over `PATCH` still places the Task at the end of its new column exactly as 2.3 built it (Q14)

**AC9 — the move route, its outcomes and its refusals** *(FR-16 matrix `prd.md:342` "Create, edit, delete Tasks"; FR-29; AR-10, AR-11, AR-34; NFR-5, NFR-7; Q1–Q3, Q18)*
**Given** the move route (Q1) carrying a destination Status of the Task's own Project's effective set and a position expressed by neighbour (Q2), never a key
**When** an Owner, Admin or Member sends it with a Session
**Then** it answers `200` with the moved `TaskResponse` (its new `statusId` and server-chosen `positionKey`), having written that Task's row alone — every sibling's key, in both columns, is unchanged
**And** a Viewer's request is `403 insufficient-role` with `capability` `manage-tasks`, one `InsufficientRole` row is written, and nothing changes
**And** a Task id that is absent, deleted or real in another Space answers the byte-identical floored `404 not-available` with no refusal row
**And** a Status outside the Task's Project's effective set is `400 invalid-task-status` (2.3's type), identical for a random and a foreign id, with no refusal row
**And** a neighbour that is not a Task in the destination column of the same Project (absent, foreign, deleted, moved away, or the moved Task itself) answers one documented refusal (Q3), identical for a random and a foreign id, with no refusal row — never a `500`
**And** an unreadable or over-long body is `400`, never `500`; a body carrying a position key is refused or ignored, never honoured (Q2)

**AC10 — keys: midpoint with jitter, ordinal everywhere** *(AD-15, AR-21, `PositionKey.cs:9-11`; `deferred-work.md:494`; Q10)*
**Given** `PositionKey`
**When** a key is placed between two neighbours, before the first, after the last, or into an empty column
**Then** the result sorts strictly between its neighbours under `StringComparer.Ordinal` **and** under the column's `Latin1_General_100_BIN2`, contains only the base-62 alphabet, and carries jitter so two independent placements into one gap are unlikely to coincide
**And** repeated placement into the same gap stays strictly ordered and its key length growth is measured; a key that would exceed `PositionKey.MaxLength` (128) answers the documented refusal of Q10 — never a `500`, never a sibling renumber

**AC11 — every key placer meets on one lock, in one order** *(`deferred-work.md:494, 543, 550`; TESTING-CONVENTIONS `:101-147`; CLAUDE.md "Race tests must be deterministic"; 2.3, 2.4 and 2.5 race findings)*
**Given** a move in flight together with: a second move into the same gap or column (from another Session), a Task creation in that column, a Status `PATCH` or Labels `PATCH` of the same Task, a deletion of the same Task or of its neighbour, or a deletion of its Project
**When** both commit
**Then** each answers one of its documented outcomes (`200`/`201`/`204`/`404`/the Q3 refusal) — never a `500`, never a deadlock victim (1205), never two Tasks with one key in a column, never a move that is silently lost — proved by cases that hold the lock and wait on `LCK_M_%` in `sys.dm_exec_requests`, never by firing requests together
**And** the move takes the Project's Task range first in the statement shape every other key placer takes, then the Task row on its clustered key (`INDEX(1)`), then reads its neighbours' keys under the range — range before row (2.5)

**AC12 — the route is an isolation row on both surfaces** *(1.9-GATE-001/002/003, AR-35, AD-3, AD-4; I-1, I-2, I-13, V-1)*
**Given** the move route
**When** the isolation suite runs
**Then** it is named by rows with an expectation for **both** `Session` and `ApiToken`: a foreign Space's Task is unreachable (Session: boundary `404`, one `CrossSpace` row, nothing moved); a foreign Task id used in one's own Space answers the same bytes as a random id; a foreign Status or neighbour id used in one's own Space answers the same bytes as a random one; a Viewer is `RoleRefused("manage-tasks")`; and every `ApiToken` expectation is `NoCredential` (Q1) — and the foreign-Task row **fails** when the Task's query filter and FILTER predicate alone are dropped, proved by a plant
**And** no isolation row names 2.6 as pending when 2.6 reaches `review`

**AC13 — idempotent** *(AD-18, AR-25, NFR-5; P-10)*
**Given** a move carrying an `Idempotency-Key`
**When** it is replayed
**Then** the original `200` and body are returned without re-applying it (no second key is written); the same key with a different body is `422 idempotency-key-reused`; the client reuses a key only for an identical resend after no answer

**AC14 — the picker and the menu, drawn from the design system** *(UX-DR11, UX-DR12, UX-DR21, UX-DR24, UX-DR27; `EXPERIENCE.md:199, 206, 211, 221, 287, 295, 315`; `DESIGN.md:256-262, 473, 504-521`; mockup `board.html:126-158`; Q4–Q6, Q9)*
**Given** the Move picker opened from Task detail or from a card's context menu
**When** it renders
**Then** it uses the picker treatment (`surface-card`, hairline border, `rounded.md`, rows at least the target minimum), lists the Project's effective Statuses with the Task's current Status **labelled** "current" and nothing selected, then the positions within the chosen Status (Q4), and commits only on an explicit position choice
**And** it never stacks a second dialog on Task detail; `Esc` closes the innermost layer first; on close or commit focus goes where Q9 says, never to `<body>`
**And** the card's context menu opens by pointer (a visible control and the `contextmenu` event), by `Enter` (Q6) and by the platform context-menu key (and `Shift+F10`, Q6); every one of these is absent — not disabled, not hidden by CSS — for a Viewer, and present at 320px, 768px and 1280px with no hover-only rule
**And** under the WCAG 1.4.12 override and 200% text-only zoom no picker or menu text is clipped or overlapped (`deferred-work.md:20` convention)

**AC15 — optimistic, and reverted out loud** *(AD-11/AR-20, UX-DR6, UX-DR25, UX-DR32, UX-DR38; `EXPERIENCE.md:145, 322-324, 390`; `DESIGN.md:517-521`; Q7, Q8)*
**Given** a move chosen in the picker
**When** it is sent
**Then** the card moves to the chosen slot at once and both column totals change at once; on `200` the card is re-placed by the server's key and the move is announced politely as AC7 says, with the ordinal and the column's true total
**And** a refusal reverts the card to its last admitted slot, both totals with it, says the refusal in the client's own words (inline where the move was made; a `404` follows the Board's refusal surface), and announces the restoration ("Returned to {Status}, position {n}."); no answer says "Not sent.", reverts, and keeps the key for an identical resend
**And** only the moved card's reflow is animated (`motion.quick`), nothing is animated under `prefers-reduced-motion: reduce`, and nothing at all is announced on load

**AC16 — the collation decisions the ledger hands this story** *(R7/A-6; `deferred-work.md:198`; Q11–Q13)*
**Given** the migrated database
**When** the schema tests run
**Then** they assert `Latin1_General_100_BIN2` on `Task.PositionKey` **and** on that column as a key column of the unique position index (`sys.index_columns` joined to `sys.columns`), and a mixed-case ordering case (keys differing only in case, inserted in one column) reads back in ordinal order with no false duplicate — and that case fails against a default-collation column (plant)
**And** the email unique index's collation is decided and asserted as Q12 records

## Tasks / Subtasks

- [ ] **Task 1 — Confirm the gates, then decide the open questions** (AC: all)
  - [ ] `dotnet build Yello.slnx`, then `dotnet test --project tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj --no-build` filtered to `IsolationCaseListTests`. With 2.6 `in-progress` it is green. List every row with `PendingUntil: "2.6"` (the ATDD phase may add some) in the Debug Log; each is converted before `review`.
  - [ ] Read the open questions (Dev Notes). Decide Q1–Q3 and Q10 before Task 2, Q11–Q13 before Task 4, Q4–Q9 before Task 9. Record each in the Completion Notes as "**Qn - (x).** … Cost to later stories: …". Each is reversible.
  - [ ] Re-measure the baseline (the last recorded full run is 2.5's dev phase: **1077 total, 1074 passed, 0 failed, 3 skipped**; 2.5's review added browser cases since). Record the number before any change.

- [ ] **Task 2 — `PositionKey`: the jittered midpoint** (AC: 3, 10)
  - [ ] Add `PositionKey.Between(string? lower, string? upper, …jitter source…)` beside `After` (`Yello.Domain/Tasks/PositionKey.cs`). `lower == null` means "before the first"; `upper == null` means "after the last"; both null is an empty column (`First`). Base-62 midpoint in the existing ordinal alphabet (digits, upper, lower — `Alphabet`, `:31`), then jitter (Q10). The jitter source is injected (a `Func<int,int>`/`Random` passed in, or an interface in Domain implemented in Infrastructure) so tests are deterministic; the Domain stays free of `Random.Shared` calls a test cannot control.
  - [ ] Guard: `lower` must sort strictly below `upper` (ordinal) or the call throws `ArgumentException` — the store must never ask for an impossible gap; a result longer than `MaxLength` follows Q10.
  - [ ] Keep `After` for creation (2.2) and the Status `PATCH` (2.3) unless Q10 decides to route them through `Between(last, null)`; either way creation still appends.
  - [ ] Update the remarks (`:9-24`) to say what is true now — no review archaeology (`deferred-work.md` "Review archaeology" entry).
  - [ ] Unit tests in the Slices handler-test style (no database): strictly between for random pairs (seeded); before first / after last / empty; 1,000 placements into the same gap stay strictly ordered and report the length reached; keys differing only in case order as `StringComparer.Ordinal` says; every character is in the alphabet; `Between(x, x)` and `Between(b, a)` throw.

- [ ] **Task 3 — Domain port, slice and store** (AC: 3, 9, 11)
  - [ ] Domain: `ITaskStore.MoveAsync(Guid taskId, Guid statusId, Guid? afterTaskId, CancellationToken)` (or the Q2 shape) returning a typed outcome, mirroring `TaskDeletion` (`Yello.Domain/Tasks/ITaskStore.cs`): e.g. `TaskMove { Moved(TaskItem), NotFound, NeighbourUnavailable, PositionExhausted }`. No `SpaceId` parameter; no `Where(SpaceId == …)`.
  - [ ] Slice `Yello.Application/Tasks/MoveTask/MoveTaskHandler.cs` (AR-3; slice folder is the imperative of FR-29's title, spine `:266`): validates the Status against the Task's Project's effective set exactly as `EditTaskAttributesHandler` does (`EffectiveStatusSet.Of(...)`), then calls the store. **No handler reads the Role, a capability, a bound or `Idempotency-Key`** (the pipeline-only gates catch each). Handler unit tests with hand fakes (`DeleteTaskHandlerTests` pattern).
  - [ ] **`TaskStore.MoveAsync`** — recommended order (Task 7's races are the authority, as in 2.5):
    1. Read the Task's `ProjectId` (plain read; none → `NotFound`).
    2. Take the Project's Task range **in the statement shape the creation's count, the Status edit and the deletion take**: `… FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = {projectId}` (`TaskStore.cs:263-266, 295-298`; `BoundCounter.cs`). Same index, same range, same order (`deferred-work.md:494` notes). If this read can also return the destination column's keys you need, do it in the same statement.
    3. Lock the Task row `WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = {taskId}` and **re-check** that it still exists and its `ProjectId` is the one whose range you hold (gone → `NotFound`, never `200`; 2.3 review fix). This is also what closes `deferred-work.md:550` for good if 6.5 ever lets `ProjectId` change.
    4. Under the range: resolve the neighbour (Q2) — it must be a Task of the same Project in the destination Status and not the moved Task — and read the key immediately after it in that column **excluding the moved Task** (or the column's first key for "top"). Anything else → the Q3 outcome.
    5. Same slot (the neighbour is already the Task's predecessor in its own column): no write, `Moved` with the unchanged Task (Q18) — the locks were still taken (2.3 review: never skip the lock for "no change").
    6. Otherwise set `StatusId` and `PositionKey = PositionKey.Between(lower, upper, jitter)`; `SaveChangesAsync`. Exactly one row updated.
  - [ ] Record the measured order in `TaskStore`'s remarks beside 2.5's, and in `PositionKey`'s.
  - [ ] **Bound**: a move consults no bound (2.3 P3: `ConsultsBound` on an edit refuses moves in a full Project; ARCH-004 refuses Host start). A move into a Project holding 5,000 Tasks is admitted — one case, reuse the existing 4,999-Task seed helper; no new large seed.
  - [ ] Catch `2601`/`2627` (unique index) only if a race proves it reachable — with the range lock it should not be; if it is, that is a lock bug to fix, not an error to map.

- [ ] **Task 4 — Collation: A-6/R7 and the email index** (AC: 4, 16)
  - [ ] Extend `TaskEndpointTests.Schema.cs` (`:164-182`, `The_position_key_is_BIN2_and_unique_within_a_Project_and_Status`): assert the collation of `PositionKey` **as a key column of** `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` via `sys.indexes` → `sys.index_columns` → `sys.columns` (an index key takes its column's collation; assert it there so a recreated index over a re-collated column is caught). Record the AC4 tuple variance (Q11).
  - [ ] New case **mixed-case ordering** (R7 functional half, `test-design-architecture.md:506-520`): in one column insert keys that differ only in case (`"a0"`, `"A0"`, `"b"`, `"B"` …, through the product or the seed helper with valid unique keys), read the Board, assert ordinal order and that both case variants coexist (no false duplicate on the unique index). **Plant** (one run, recorded): the column re-collated to the default in the migration (or a scratch table with the default collation in the test database, if the migration plant is impractical) → the case fails (duplicate on insert or non-ordinal order). Restore in a `finally`.
  - [ ] Traits: `Requirement=AR-21`, `Requirement=AD-15`, `Priority=P0` (A-6 is P0, `test-design-qa.md:304`).
  - [ ] **Email index (`deferred-work.md:198`, Q12).** First, against the running Slices engine, read the current collation of `Account.NormalizedEmailAddress` and record it in the Debug Log (the ledger entry was "reasoned rather than executed"). Then apply Q12: recommended an EF migration declaring `UseCollation("Latin1_General_100_BIN2")` on `NormalizedEmailAddress` (drop and recreate `SchemaNames.AccountEmailUniqueIndex` around the `ALTER COLUMN`), a schema assertion beside the existing Account schema tests, and a case that two addresses `EmailAddressNormalisation.Normalise` maps to different strings but a kana/width-insensitive collation would equate (e.g. full-width vs ASCII letters) both register. Update `AccountConfiguration.cs:62-65` and `EmailAddressNormalisation.cs:20-40` remarks to say what is true now. Close `:198` with what was done.
  - [ ] Migration rules: `dotnet ef migrations add` via the local tool; the migration bundle tests stay skipped unless `YELLO_MIGRATION_BUNDLE` is set; `MigrationAtStartupGateTests` and `SchemaVarianceTests` stay green.

- [ ] **Task 5 — Contracts** (AC: 8, 9)
  - [ ] `TaskRoutes`: add the move route (Q1 recommended `Move = "/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}/move"` and `MoveOf(spaceId, taskId)`).
  - [ ] `Yello.Contracts/Tasks/MoveTaskRequest.cs` (Q2 recommended `MoveTaskRequest(Guid StatusId, Guid? AfterTaskId)`) — **no position key member**, no Project, no Space. The answer is the existing `TaskResponse`.
  - [ ] Problem type for Q3 (recommended `task-position-unavailable`, `409`) in `Yello.Contracts/ProblemTypes.cs`, with remarks stating what it never means (it never says whether the neighbour exists in another Space); and Q10's exhaustion outcome (recommended the same type). A `RefusalCopy` branch for each new type (unknown types fall through to "Not available.").
  - [ ] AD-19: the contract snapshot gains the route and its request shape, deliberately.

- [ ] **Task 6 — Endpoint and the gates that break on purpose** (AC: 8, 9, 12)
  - [ ] `TaskEndpoints.MapMoveTask`: `MapPost(TaskRoutes.Move, MoveAsync).RequiresCapability(Capability.ManageTasks).SessionOnly()` (Q1). Body read with `JsonRequestBody.ReadAsync` in the `TaskEditBody` style (absent vs `null` distinguished; unreadable → `400 invalid-task-edit` or a move-specific type, decided and recorded; a `positionKey` member → refused, Q2). Answers: `Moved` → `Results.Ok(TaskResponse)`; `NotFound` → `BoundaryProblems.NotAvailable()` (unrecorded); Status invalid → `400 invalid-task-status`; neighbour → Q3. **No `ConsultsBound`**, no `Results.Created`/`Accepted` (ARCH-009). Register the handler and the map beside their siblings in `Program.cs`, before `MapUnmatchedApi()`.
  - [ ] Logs carry `SpaceId` and ids only — never a title (AR-34, I-17).
  - [ ] **Gates expected to go red by design — amend deliberately, by exactly this, never loosen** (search for each before changing; record every unpredicted widening in the Debug Log, as 2.4 did):
    1. `RefusalBoundaryGateTests.cs:420-457` `The_endpoints_marked_unreachable_with_a_Token_are_exactly_Token_management_and_the_switcher` — widen by exactly `TaskRoutes.Move`; rename the test (it is no longer only Token management) and carry the FR-35 reason in its remarks.
    2. `RefusalBoundaryGateTests.Tasks.cs` / `.TaskEdit.cs` — any exact set of Task routes or methods widens by exactly `POST {Move}`; the move asserts `ManageTasks`, Session-only, no bound.
    3. `RefusalBoundaryGateTests.Delete.cs:62` asserts the deletes are **not** Session-only — must stay green unchanged.
    4. `The_Task_edit_request_carries_its_editable_attributes_and_nothing_else` (2.4 rename) — stays green: the edit request gains nothing.
    5. `TaskEndpointTests.TaskProperties`, `BoardEndpointTests.TaskProperties`, `EditedTaskProperties` — unchanged (the response is unchanged).
    Also must stay green unchanged: `Every_spaceId_endpoint_declares_exactly_one_capability`, `Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay`, `No_endpoint_answers_through_Results_Created_or_Accepted`, `No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity`.

- [ ] **Task 7 — Slice, idempotency, API-narrowness and race tests** (AC: 3, 5, 6, 8, 9, 11, 13)
  - [ ] **Memory-bound machine (CLAUDE.md "Test memory budget"): add no container-backed class.** New partials of the existing class: `TaskEndpointTests.Move.cs`, `.MoveRace.cs`, `.MoveIdempotency.cs`; a `MoveWire.cs` harness beside `DeleteWire.cs`/`TaskEditWire.cs` (`POST` with a Session; a Token attempt; raw-SQL holders in the product's statement shapes). Never two `dotnet test` processes at once; iterate with `--filter`.
  - [ ] Cases (assert the problem `type`, not only the status; every `4xx` other than `403` and boundary `404` asserts "no refusal row"):
    - Owner, Admin, Member move a Task across columns and within one (top, between two, bottom) → `200`; `GET` the Board: the Task is where asked; **every sibling key in both columns is byte-identical before and after** (AC3); reload (a second Board read, a fresh Session) gives the same order (AC6).
    - Two Sessions of two Members of one Space move two Tasks into the same gap → both admitted, distinct keys, one order both Sessions read after reload (AC5) — the deterministic version is RACE-M1.
    - Viewer → `403 manage-tasks`, one `InsufficientRole` row, nothing changed.
    - Random / deleted / foreign Task id → byte-identical `404 not-available`, no refusal row.
    - Status outside the effective set (random id, a real Status of another Space) → `400 invalid-task-status`, identical, no row.
    - Neighbour: random id, a foreign Space's Task id, a Task in another Project of the same Space, a Task in another column, a deleted Task, the moved Task itself → the Q3 refusal, identical bytes for random vs foreign, no row, nothing changed.
    - Body: unreadable JSON, missing `statusId`, a `positionKey` member → documented `400`; never `500`.
    - **API narrowness (C-5, AC8)**: with a Token — the move route answers the no-credential `401` byte-identical to no credential, nothing changed; `PATCH` carrying only `{"positionKey": "0"}` changes no key and answers its documented `400`; `PATCH` carrying `statusId` + `positionKey` moves to the end of the column (2.3 behaviour) and ignores the key; the Board and `GET` Task over a Token show `positionKey`. Tag `Requirement=FR-35`.
    - A move in a Project holding 5,000 Tasks is admitted (no bound).
    - Exhaustion (Q10): seed a gap whose neighbours differ only at character 128 (valid alphabet keys, seed helper) → the documented refusal, nothing changed.
  - [ ] Idempotency on the Session surface (the Token surface is `401`): same key twice → one write, byte-identical body, no second key; same key with a different body → `422 idempotency-key-reused`.
  - [ ] **Deterministic races** with `TaskWire.WaitUntilBlockedOnALockAsync` (`TaskWire.cs:347-370`) or the "blocked or answered" wait (`LabelWire.cs:199-217`). The test holds the competing lock or change in its own transaction, sends one request (or two, for M1), waits for `LCK_M_%`, then commits or rolls back. **Never `Task.Delay`, never `Task.WhenAll` as the race.** Expected outcomes (confirm, record any that differ, with the reason):
    - **RACE-M1** test holds the Project range; two moves into the same gap from two Sessions wait; commit → both `200`, distinct keys strictly between the gap's neighbours, neither lost.
    - **RACE-M2 (read race, the 3/3 shape of 2.3's `A_Status_edit_reads_the_last_key_of_its_new_column_only_under_the_lock`)** test holds the range, the move waits; the test inserts a Task at exactly the key the move would compute without the lock's re-read (or moves the neighbour's successor) and commits → the move still lands strictly between its *current* neighbours, no duplicate key, no `500`.
    - **RACE-M3** a Task creation holding the bound's range vs a move into that column → both succeed, distinct keys.
    - **RACE-M4** a Status `PATCH` of the same Task holding its locks vs a move → last writer wins per its own outcome (`200` both, final state = the later one); never 1205.
    - **RACE-M5** Task deletion vs move of the same Task, both orders → `204` + `404`, or `200` + `204`; never `500`/1205. **Plant 2.5's three masked row-lock hints here** (`deferred-work.md:543`): if this or M6 now catches one, close the entry with the gate; if all stay 0/3, record "masked, kept (judged harmless at 2.5 review)" and close the entry as decided.
    - **RACE-M6** deletion of the **neighbour** while the move waits → the Q3 refusal, nothing written; and the reverse (move held, neighbour deletion waits) → `200` then `204`.
    - **RACE-M7** Project deletion vs move → `204` + `404`, or `200` + `204`.
    - **RACE-M8** a Labels `PATCH` of the same Task vs a move → both `200`; never 1205.
    - Plants (three runs each, record `n/3`): the move's range hint removed; the move's row `INDEX(1)` removed; the row's `UPDLOCK, HOLDLOCK` removed; the neighbour read moved before the range lock. Masked plants are recorded with the reason, never deleted (TESTING-CONVENTIONS `:135-147`).

- [ ] **Task 8 — Isolation rows** (AC: 12)
  - [ ] Add the rows in "Isolation rows" (Dev Notes), each sending a request on `TaskRoutes.Move` with its own body (`CaseRequest.Body`/`AnchorBody`, `Expectation.cs`), seeded through the product (`ProductActor`; `SeedProjectWithTaskAsync`; the Viewer seed pattern, `IsolationCases.cs` `ViewerEditsATaskAsync`). `Then` callbacks assert the foreign Task's Status and key are unchanged.
  - [ ] Two-layer plant, one at a time, original held in memory and restored in a `finally`: `IgnoreQueryFilters()` on the move's Task reads **and** the `Task` FILTER predicate removed (in the migration file, rebuild, restore — 2.3 P6, 2.4 plant 3) → `ISO-MOVE-TASK-FOREIGN-ID-IN-OWN-SPACE` fails on the Session surface; each layer alone → green.
  - [ ] No new timing row (the route is in the API group; `NotAvailableFloor` applies).
  - [ ] GATE-003: no row pending on 2.6 at `review`.

- [ ] **Task 9 — Client: the Move picker** (AC: 1, 2, 7, 14, 15)
  - [ ] One component, `Yello.Client/Components/MovePicker.razor` (Q5), used by Task detail and by the card's context menu: a Status step (the Board's columns — the effective set — current Status labelled "current", none selected) then a position step for the chosen Status (Q4), committing on the position choice; `Esc` steps back/closes innermost-first; built on 2.3's picker classes and `PickerOption` (reuse; do not fork its markup). Its options are not a second dialog.
  - [ ] **Task detail** (`TaskDetail.razor`; header `:22` says "no Move control (2.6)" — update it): a `button-secondary` "Move" control for `CanManage` only, placed per Q5 (recommended directly after the Status control, before Labels; Delete stays last). `FocusLastAsync` unchanged if Delete stays last; re-check it. Viewer: no control.
  - [ ] **Card context menu** (`TaskCard.razor`, Q6): for `CanManage` only, a visible actions control on the card (named "Actions for {title}" or as Q6 decides) opening a menu holding "Open" and "Move"; the card also opens the same menu on the `contextmenu` event (pointer) and on the `ContextMenu` key and `Shift+F10` (`@onkeydown` on the card, `preventDefault` only for those keys). Not hover-only: the control is always rendered (assert it is visible without hover at 320/768/1280). Viewer: no control, no handler (the browser's own context menu is left alone). The card's "exactly one control" rule changes — widen `BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow` deliberately to "the title button and, for a Role that can move, the actions control", and record it.
  - [ ] **Sending** (`BoardView.razor.cs`): a `MoveAsync(taskId, statusId, afterTaskId)` that renders at once (Q7: optimistic — remove from its column, insert at the chosen index, both `Total`s moved), sends `Writes.SendAsync(HttpMethod.Post, TaskRoutes.MoveOf(SpaceId, taskId), body, key, token)`, and settles on the existing replica machinery or a sibling of it: the move is a Task-level change that conflicts with an in-flight Status edit of the same Task — queue one behind the other (per-Task, the 2.3 Q8 "one write per attribute at a time" rule applied to Status+position) rather than racing them. On `200`: `Admit` with the server's `statusId` and `positionKey` and re-place by key (`InsertByKey`, 2.3 review fix); announce (Q8). On refusal: revert to the admitted slot, `RefusalText.Of(…)` where the move was made (the panel's status region, or the Board's region for a card-menu move — Q8), announce "Returned to {Status}, position {n}.". `404` → the existing `OnRefused` pattern. No answer / `5xx` → "Not sent.", revert, key kept for an identical resend. A Task deleted by this tab meanwhile (`_deleted`) → answer dropped (2.5 Q7).
  - [ ] **Announcements** (Q8): new resx entries "Moved to {0}, position {1} of {2}." and "Returned to {0}, position {1}." in `ClientCopy.resx` with `<comment>`s; the ordinal is 1-based within the column as the client holds it after re-placing by the server's key, and the total is the column's true `Total`. Decide (Q8) whether 2.3's Status change ("Moved to {0}.") gains the position too; it lands last, so "position N of N". The live region exists empty from the first render (2.2 review).
  - [ ] **Focus** (Q9): commit from Task detail → focus stays on the panel's Move control; commit from the card menu → the moved card's title button in its new place (the `_focusCardRequests` pattern, stale requests cleared); dismiss → the invoking control.
  - [ ] **Motion**: only the moved card's reflow uses `motion.quick`; none under `prefers-reduced-motion: reduce` (`DESIGN.md:517-521`). No lift, settle or drop zone (2.7).
  - [ ] Purge (UX-DR31): picker and menu state are Board state; `PurgePanel()` and the Space purge clear them.
  - [ ] Copy (`ClientCopy.resx` only, sentence case, `<comment>` on each, never quoted in a C# comment): "Move", "Move {0}" (accessible name if needed), "Actions for {0}", "Open", "Current" (the current-Status label), the position option forms (Q4), "Top of column", "Bottom of column", the two announcements, the Q3 refusal copy (e.g. "That position has changed. Choose it again."), "Not sent." reuse. No "Are you sure", no exclamation, no "archive" (ARC-001 gate).

- [ ] **Task 10 — Design and capability gates** (AC: 2, 14)
  - [ ] `DesignFoundationGateTests` new partial `.MovePicker.cs`: the picker and menu use the picker tokens (`surface-card`, hairline, `--radius-md`, `min-height` ≥ target minimum), no `box-shadow`, transitions only via the motion tokens and removed under reduced motion; **no `:hover`-only rule hides or reveals the Move or actions control** (a CSS-corpus check that no rule for those selectors sets `display`/`visibility`/`opacity` only under `:hover`). Each with a plant.
  - [ ] Capability: `TaskCapabilities.PermitsManageTasks` drives every move affordance (FR-16 matrix has no move row; moving is "Create, edit, delete Tasks", `prd.md:342`). **Add no capability.** The existing `…predicate_agrees_with_the_capability_matrix` gate holds it.

- [ ] **Task 11 — Browser tests** (AC: 1, 2, 7, 14, 15)
  - [ ] `tests/Yello.Tests.Client` against `FakeYelloApi`: add `POST TaskRoutes.Move` (re-ordering the fake Board so a re-read agrees; the fake computes a key with the real `PositionKey.Between` or simply orders by the requested neighbour), with hold/release/refuse/unanswered controls (`HoldTaskEditAsync` pattern, `TaskCompletionSource`). Same `ClientSurfaceCollection`; no new fixture. Bodies are `Yello.Contracts` types.
  - [ ] New `MoveTests.cs` (or partials of `BoardTests`/`TaskDetailTests`): picker from Task detail and from the card menu (pointer click on the actions control, right-click `contextmenu`, `Enter` on the actions control, `ContextMenu` key, `Shift+F10`); no option is selected or `aria-checked` as a destination on open (the current Status is labelled, not checked); **held** move → card already in its new slot and both totals moved; release `200` → announcement text exact ("Moved to In Progress, position 3 of 12." shape with the fake's numbers); release refused (`403`, `409`, `400`) → card back, totals back, copy shown, "Returned to …, position n." announced; unanswered → "Not sent.", back, resend reuses the key; `404` → refusal surface; dismiss → nothing sent (the fake recorded no POST), focus on the invoker.
  - [ ] Breakpoints: at 320, 768 and 1280 px the actions control and the panel's Move control are visible without hover (`IsVisibleAsync` after moving the mouse away).
  - [ ] Absence (**extend E2E-003**, `BoardTests.cs:140`): a Viewer has no actions control, no Move control in the panel, no `contextmenu`/`ContextMenu`-key handler effect (right-click opens nothing of ours), and `main [disabled], main [aria-disabled='true'], [role=dialog] [disabled]` count 0. Plants: a Viewer offered each control → caught.
  - [ ] `TextSpacingTests`: the picker in the panel and the card menu under the 1.4.12 override and 200% text-only zoom.
  - [ ] Never Playwright's URL wait; poll `location.pathname`. No `RegexOptions.CultureInvariant`. `blur()` and reset focus before keyboard tests. **Rebuild `Yello.Client` in Debug before a browser plant.**

- [ ] **Task 12 — Plants, runs, ledger, status** (AC: all)
  - [ ] Plants one at a time in the foreground; original held in memory and restored in a `finally`; compare the tree hash after each. **Never** `git checkout --`, `git restore`, `git reset` or `git stash`. Each plant compiles. Record `| # | Plant | Gate | Caught |` in the Debug Log. Minimum set: a row left `PendingUntil: "2.6"` at `review` (GATE-003); the move route named by no row (GATE-002); `.SessionOnly()` removed (the AC8 Token case and the Token-unreachable gate); the two-layer drop and each layer alone; each lock hint (3 runs); the neighbour read before the lock; `ConsultsBound` on the move; a sibling renumber (a planted `UPDATE` of a sibling's key in the store) → the AC3 sibling-key comparison; the mixed-case plant (Task 4); a Viewer offered each control; a hover-only rule on the actions control; a default selection in the picker; an announcement without the ordinal.
  - [ ] Runs (CLAUDE.md): `dotnet build Yello.slnx` (0 warnings, 0 errors), `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. If a Client case fails only in the full run, run `dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build` alone before calling it real (memory: the full run starves the browser suite). Rerun once on an engine start failure or a low-memory kill. Check `Get-Process Yello.Host` if `bin/Debug` is locked. Docker readiness: `docker info --format '{{.ServerVersion}}'` non-empty.
  - [ ] `tests/TESTING-CONVENTIONS.md`: the move race pattern (who holds what; the read-race shape), the new counts.
  - [ ] Ledger (`deferred-work.md`): CLOSE `:494` (moves and the midpoint built; the lock order measured), `:543` (decided by RACE-M5/M6 plants: caught → gate named; masked → kept and why), `:550` (a 2.6 move never changes `ProjectId`; the move re-checks `ProjectId` under its row lock; re-own any residue to **6.5**), `:198` (per Q12). UPDATE `:490` (DESIGN.md's "+" in every column: a later column's "+" would be a create-then-move — still UX's), `:517`/`:549` (the Move picker repeats the picker keyboard pattern and the identical-announcement pattern — owner 2.8). ADD with owners: any Q decided against the UX spine or the mockup (owner **UX**: Q4 position list vs the mockup's Top/Bottom, Q6 `Enter` and the actions control, Q8 announcement on a Status change); the position-key length ceiling and same-gap growth (owner **2.9**, measured under load, Q10); the picker's position list at 5,000 Tasks (owner **2.9**, paging); Board push of another participant's move (owner **Epic 7** — no 7.x story names it today, `epics.md:2654-2657`; flag to PM); Q13's digest pin decision if deferred.
  - [ ] `sprint-status.yaml`: set `review` at the end of the dev phase. **Do not add review narration to source files.**

## Dev Notes

### The shape of the story in one paragraph

The column, its BIN2 collation, its unique index, the readable `positionKey` and an append-only key generator all exist (2.2), and a Status `PATCH` already moves a Task to the end of another column under the Project-range lock (2.3). What 2.6 adds is **placement**: a move to a chosen position, which needs (1) a **midpoint key generator with jitter** (`PositionKey.Between`), (2) a **new write route** that takes a relative intent (destination Status + neighbour) and never a key, and that a Token cannot reach (FR-35: "the single place the API is deliberately narrower than the browser"), (3) a **fourth key placer** that must take the same Project range in the same statement shape as creation, Status edit and deletion, then the row, then read its neighbours — every previous story found its real bug in exactly this kind of lock ordering, and (4) the **canonical, non-gesture Move control** in two places (Task detail and a card context menu), optimistic, announced with ordinal and true total, absent for a Viewer, present at every breakpoint. The ledger also hands this story the R7/A-6 collation assertion it was always meant to finish, the masked deletion hints, the deletion's unlocked `ProjectId` read, and the email index collation (Lee, 2026-09-10).

### What exists today (read before writing)

- **There is no `src/` folder.** Production projects sit at the repo root; tests in `tests/`.
- **`Yello.Domain/Tasks/PositionKey.cs`** — append-only `After(string? last)` (base-62 carry increment, ordinal alphabet `0-9A-Za-z` at `:31`, `First = "V0000"`, `MaxLength = 128`). Remarks at `:9-24` promise "Story 2.6 replaces this with the jittered midpoint for moves, and adds the BIN2 schema assertion and the mixed-case case". **No midpoint exists.**
- **Schema** — `TaskConfiguration.cs:30-34, 56-60` declares `PositionKey varchar(128) COLLATE Latin1_General_100_BIN2`; unique index `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` over `(SpaceId, ProjectId, StatusId, PositionKey)` (`TaskConfiguration.cs:87`; migration `20261025115412_TaskTable.cs:45, 63, 82`). The `SpaceId` lead is deliberate (a seek and its range lock stay in one Space, `TaskEndpointTests.Schema.cs:164-168`). **AC4 needs no migration for the Task table** — only the assertions (Task 4, Q11).
- **Existing schema check** — `TaskEndpointTests.Schema.cs:174-182` asserts the column collation and the unique key's columns; `:20` says "The full A-6 / R7 collation assertion and the mixed-case ordering case stay with 2.6".
- **`TaskStore.cs`** (read the remarks `:9-62` first):
  - `CreateAsync` (`:76-114`) reads the column's last key **without its own lock** and appends — safe only because the pipeline's `BoundCounter` count already holds the Project range (`deferred-work.md:494`).
  - `ApplyAsync` (`:130-176`, the Status `PATCH`): any Status named takes `LastKeyUnderTheProjectLockAsync` (`:295-298`, `SELECT MAX(CASE WHEN StatusId = {s} THEN PositionKey END) … WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = {p}`), reloads the Task under it (gone → `null`), and writes `After(last)` only if the Status changed.
  - `ReplaceAsync` (Labels, `:190-243`): Task row `WITH (UPDLOCK, HOLDLOCK, INDEX(1))`, then Labels in server GUID order.
  - `DeleteAsync` (`:246-287`): unlocked `ProjectId` read (`:248-253`, `deferred-work.md:550`), then the range in the **count** shape (`:263-266`), then the row `INDEX(1)`, then `ExecuteDeleteAsync`.
  - Note the two range statements differ (`COUNT(*)` vs `MAX(CASE…)`) but scan the same index range under the same hints; RACE-T1 (2.5) proved they meet. Use one of these two shapes for the move — do not invent a third that could choose another index.
- **Contracts** — `TaskResponse(Guid Id, string Title, Guid StatusId, string PositionKey, DateTimeOffset CreatedAt, DateTimeOffset? DueDate)` + `LabelIds` (already readable on both surfaces); `EditTaskRequest(string? Title, Guid? StatusId, DateTimeOffset? DueDate, IReadOnlyList<Guid>? LabelIds)` — pinned by `The_Task_edit_request_carries_its_editable_attributes_and_nothing_else`; `TaskRoutes` has `Create`, `Task`, `CreateIn`, `TaskOf`; `BoardResponse`/`BoardColumnResponse(Guid StatusId, string Name, int Total, IReadOnlyList<TaskResponse> Tasks)`.
- **Endpoints** — `TaskEndpoints.cs`: `MapCreateTask` (`:37-40`, `ConsultsBound(TasksPerProject)`), `MapViewTask` (`:94-96`), `MapEditTask` (`:107-109`), `MapDeleteTask` (`:120-122`); body parsing in `TaskEditBody.cs`. `SessionOnly()` is `ApiTokenAuthentication.SessionOnly` (`Yello.Host/ApiTokens/ApiTokenAuthenticationHandler.cs:102-112`); today only `ApiTokenEndpoints.MapIssueApiToken` uses it (`ApiTokenEndpoints.cs:42`), and a Token on it gets the no-credential `401` byte-for-byte (`IsolationCases.cs:24-25`).
- **Client** —
  - `TaskCard.razor`: one control, the title `<button data-task-id>` that opens Task detail (2.3 Q7); header `:22` "No drag handle or Move control for any Role (2.7, 2.6)".
  - `BoardColumn.razor`: `<section tabindex="-1">` focus target (2.5), count chip `Total`, `FocusTaskId`/`FocusRequest` per card.
  - `BoardView.razor`: polite live region `:49` (`_announcement`); `ColumnState(StatusId, Name, Total, Tasks)` `:379`; `Remove` `:363`.
  - `BoardView.razor.cs`: replica machinery `EditAsync → SendInTurnAsync → SendAsync → SettleAsync → UseAnswerAsync` (`:317-489`), `TaskReplica` (`:686-…`, `Admitted`/`InFlight`/`Queued`/`Unanswered` per `TaskAttribute`), `Place` (`:568-610`: an outstanding Status change goes to the **end** of the new column; an admitted one by key), `InsertByKey` (`:612-616`, ordinal), `Locate`, `_deleted` (2.5 Q7), `Admit` for Status takes the server's `StatusId` **and** `PositionKey` (`:677`). The Status change announces `TaskMoved` "Moved to {0}." at edit time (`:326-330`; `ClientCopy.resx:594-597`).
  - `TaskDetail.razor` (616 lines): modal dialog, sentinels, Status picker (`PickerOption`, listbox, current marked), Labels picker, Delete (last), `FocusLastAsync`, `KeyDownAsync` (`Esc` innermost-first), header `:22` "no Move control (2.6)".
  - `PickerOption.razor` — one tab stop per option, no arrow keys (`deferred-work.md:517`, owner 2.8): the Move picker inherits this; do not fix it here.
- **Gates that pin shapes** (2.3 and 2.4 were surprised by these): `TaskEndpointTests.TaskProperties`, `BoardEndpointTests.TaskProperties`, `EditedTaskProperties`, the Token-unreachable route set (`RefusalBoundaryGateTests.cs:435-457`), the Task route/method sets in `RefusalBoundaryGateTests.{Tasks,TaskEdit,Delete}.cs`, `BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow` ("exactly one control"), E2E-003 (`BoardTests.cs:140`).
- **Preserve** every 2.3/2.4/2.5 review fix: the stale focus request cleared; the pointer-blocking layer; a refusal for one Task never shown in another's panel; `InFlight` cleared only after admission; an unreadable `200` keeps the key; `5xx` is no answer; an admitted Status change re-placed by the server's key; the same-Status edit still takes the lock; a Task gone under the lock answers not-found; a deletion's answer closes only its own panel; an edit's refusal is never shown in the delete confirm.

### Isolation rows (Task 8)

| Id | Test design | Vector | Route | Request | Session | ApiToken |
|---|---|---|---|---|---|---|
| ISO-MOVE-TASK-FOREIGN-SPACE | I-2 | Write | `POST TaskRoutes.Move` | A → `MoveOf(SB, TB)` with a valid body for B | `BoundaryNotFound`, B's Task unchanged | `NoCredential`, unchanged |
| ISO-MOVE-TASK-FOREIGN-ID-IN-OWN-SPACE | I-1, I-13 | Write | `POST TaskRoutes.Move` | A → `MoveOf(SA, TB)`, anchor `MoveOf(SA, random)` | `SameAsAnchor(404, not-available)`, no row, unchanged; **two-layer plant** | `NoCredential` |
| ISO-MOVE-FOREIGN-NEIGHBOUR-IN-OWN-SPACE | I-1, I-13 | DirectIdentifier | `POST TaskRoutes.Move` | A moves TA with `afterTaskId = TB`, anchor `afterTaskId = random` | `SameAsAnchor(<Q3 status>, <Q3 type>)`, no row, nothing moved | `NoCredential` |
| ISO-MOVE-FOREIGN-STATUS-IN-OWN-SPACE | I-1, I-13 | DirectIdentifier | `POST TaskRoutes.Move` | A moves TA with B's Status id, anchor random Status | `SameAsAnchor(400, invalid-task-status)`, nothing moved | `NoCredential` |
| ISO-RECORD-KIND-TASK-MOVE-VIEWER | V-1, C-6, NFR-7 | RecordKind | `POST TaskRoutes.Move` | a Viewer of SA | `RoleRefused("manage-tasks")`, unchanged | `NoCredential` |

The ISO-WRITE-TOKEN row (`IsolationCases.cs:69-77`) is the model for a per-surface dictionary instead of `Both(…)`. I-11 needs no row (no Account-scoped route touched). C-6 (the browser/API parity audit) gains one recorded, intended difference: the move route (FR-35).

### Open questions (decide in Task 1; each reversible; record as "Qn - (x)")

1. **The move route and its surface.** FR-35 (`prd.md:605`, `acceptance-criteria.md:356`, `decisions-settled.md:85`) makes position not writable over the API, and AD-4 forbids a slice branching on surface. Options: (a) a new `POST /api/v1/spaces/{spaceId}/tasks/{taskId}/move`, `.SessionOnly()` — endpoint metadata, not a slice branch (the Token-issue precedent); a Token gets the no-credential `401`; (b) the same route on both surfaces, arguing that a relative intent is not "writing the key" — contradicts FR-35's "reordering happens only through the interface"; (c) a `position` member on the existing `PATCH` — cannot be Session-only per member, and breaks the edit-request gate. **Recommend (a).** Cost: the Token-unreachable gate widens by one route (Task 6); the isolation rows carry `NoCredential` for `ApiToken`; story 8.1's API docs state the exception (FR-35 already does).
2. **What a move request names.** It must never carry a key (AR-21, the edit-request gate). Options: (a) `MoveTaskRequest(Guid StatusId, Guid? AfterTaskId)` — `null` = top; the server places between that neighbour and its *current* successor (excluding the moved Task) under the lock; (b) `{ statusId, beforeTaskId?, afterTaskId? }` — both neighbours, refused if they are no longer adjacent (more refusals under concurrency); (c) `{ statusId, ordinal }` — the server resolves the ordinal under the lock (simple wire, but an ordinal is stale the moment another move lands, and 2.9's paging makes it ambiguous). **Recommend (a)**: one neighbour is enough to converge (two concurrent "after X" moves both land between X and X's old successor, the later one nearer X — nothing discarded, AC5), and it is exactly the "two concrete neighbours" rule 2.8 needs (`epics.md:1279-1280`), with the second neighbour found server-side. A `positionKey` member in the body is refused `400` (recommended) rather than ignored, so a client cannot believe it wrote one. Cost: none.
3. **A neighbour that is no longer a neighbour.** Absent, deleted, foreign, in another Project or column, or the moved Task itself. Options: (a) refuse `409 task-position-unavailable` (one type for every case, byte-identical for random and foreign ids, no row), and the client reverts, says "That position has changed. Choose it again." and re-reads the Board; (b) fall back to the bottom of the destination column and answer `200` — the person's chosen place is silently not honoured, which is the kind of discard FR-29 forbids; (c) `400`. **Recommend (a)**; `409` because the request was well-formed and the state moved. Cost: one problem type, one `RefusalCopy` branch, one copy entry.
4. **What the position step offers.** The mockup (`board.html:152-154`) offers only "Top of column" / "Bottom of column"; AC7's "position 3 of 12" and FR-29's "reorder within a column" need any slot; the spines win over mocks (`EXPERIENCE.md:73`) but neither lists the options. Options: (a) "Top of column", then one option per Task currently loaded in that column (the moved Task excluded) named by ordinal and neighbour — "Position {n}, after {title}" — the last also labelled bottom; (b) Top/Bottom only (mockup) — reorder becomes two-step at best and cannot reach the middle; (c) a numeric ordinal input. **Recommend (a)**; at 5,000 Tasks the list is the loaded page (2.9 pages it — ledger). Cost: UX amends the mockup (ledger, owner UX).
5. **The picker's shape and home.** No document says listbox, menu or dialog; Task detail is already a dialog and dialogs never stack (UX-DR12). Options: (a) one `MovePicker` component rendered **in place** — inside the panel below the Move control, and on the Board as a non-modal popover anchored to the card — Status listbox then position listbox (the 2.3 `.picker`/`PickerOption` treatment, UX-DR11 "one component, five uses"), commit on position; (b) a single combined list of "{Status}: position n" rows (long); (c) a dialog (breaks UX-DR12 from Task detail). **Recommend (a)**, Move placed after the Status control in the panel. Cost: none; 2.8 adds arrow keys to every picker together (`deferred-work.md:517`).
6. **"`Enter` on a focused card" vs 2.3's `Enter` opens Task detail.** `epics.md:1175` and UX-DR24 (`epics.md:265`) say `Enter` opens the context menu; `EXPERIENCE.md:315` says "open the focused Task, **or** open its context menu"; 2.3 Q7 made the title a native button whose `Enter` opens the panel. Options: (a) add a visible **actions control** to the card (for movers only) that opens the context menu ("Open", "Move") — it is the pointer path, `Enter` on it opens the menu, the `contextmenu` event and the `ContextMenu` key (plus `Shift+F10`, the Windows convention, no document names it) on the card open the same menu, and `Enter` on the title still opens the Task; (b) make `Enter` on the card open the menu with "Open" first — every open costs an extra step and 2.3's behaviour changes; (c) no visible control, menu only by right-click/keys — fails "never hover-only"/discoverability and touch (no right-click). **Recommend (a)**; record the AC1 wording variance for UX. Cost: the card's one-control gate widens (Task 9); 2.8's grid places the second control in its cell model.
7. **Optimistic or not.** AD-11 makes Task writes optimistic and only destructive ones are not (UX-DR26); 2.3's Status change is optimistic. **Recommend optimistic** (AC15), re-placing by the server's key on `200`. Alternative: wait for the server (simpler, but the Board lags and diverges from the Status picker). Cost: none.
8. **Announcements.** AC7's "cancelled move announces the restoration" was written for pick-up (2.7/2.8). Recommend: (a) announce "Moved to {Status}, position {n} of {total}." on **admission** (ordinal after re-placing by the server's key — truthful under concurrency), and "Returned to {Status}, position {n}." when a refusal or no answer reverts it; a dismissed picker moved nothing and says nothing; (b) also extend 2.3's Status change to the same sentence (it lands last: "position N of N") so the Board has one move sentence — **recommend yes**, announced on admission rather than at edit time; (c) refusal words go where the move was made (panel region, or the Board's region for the card menu). Cost: 2.3's `TaskMoved` key changes or is retired (update its browser case deliberately); 2.8 owns re-announcing identical sentences (`:549`).
9. **Focus after a move.** No rule exists (`EXPERIENCE.md:403` covers dialog close only). Recommend: from Task detail, focus returns to the panel's Move control (the panel stays open); from the card menu, focus goes to the moved card's title button in its new column (it still exists); on dismiss, to the invoking control. Never `<body>`. Cost: none.
10. **Jitter and the length ceiling.** AD-15 requires jitter (`ARCHITECTURE-SPINE.md:166`) and forbids renumbering. Recommend: midpoint in base 62, then append 1–2 random alphabet characters when the midpoint lands on a key the gap already makes tight (or always append a short random suffix — decide by Task 2's same-gap growth measurement), random source injected. With the server-side range lock, jitter is a second line, not the convergence mechanism (spine `:429`: interleaving is mitigated, not eliminated — accepted deferral). A key that would exceed 128: refuse `409 task-position-unavailable` with copy suggesting another position (recommended) rather than widen the column (a migration) or renumber (banned). Measure how many same-gap placements reach 128 and record it; ledger to 2.9. Cost: none now.
11. **The AC4 index tuple.** AC4 names `(ProjectId, StatusId, PositionKey)`; the index is `(SpaceId, ProjectId, StatusId, PositionKey)` (2.2 Q2 (a), so a seek and its range lock stay inside one Space under RLS). Recommend: keep it, assert BIN2 on `PositionKey` as a key column of that index, and record the variance (the AC's intent — the index the keyset seek uses carries the column's collation — holds). Cost: none; 2.9's keyset seek uses this index.
12. **The email unique index (`deferred-work.md:198`).** `NormalizedEmailAddress` (NFC, trimmed, upper-cased by `EmailAddressNormalisation.Normalise`) has no explicit collation, so its unique index takes the database default — on the Linux image `SQL_Latin1_General_CP1_CI_AS`, kana- and width-insensitive — and two addresses the function maps to different strings can collide, which AD-23 answers as a silent `204`. Options: (a) migrate the column to `Latin1_General_100_BIN2` (the function already defines equality; the index should compare exactly what it stores — the design `EmailAddressNormalisation.cs:20-40` describes), with a schema assertion and the collision case; (b) leave the default and document it; (c) re-own to 5.4 (Account deletion). **Recommend (a)**, confirming the live default first (the entry was reasoned, not executed). A stricter collation cannot fail on existing data (it only separates values). Cost: one migration; 1.3's registration and 4.3's invitation matching compare the same normalised form, so nothing else changes.
13. **Pin the SQL Server engine by digest?** `SqlServerContainerFixture.cs:94` and `AppHost/Program.cs:151` leave it "an open question for story 2.6". Options: (a) keep the floating `2025-latest` tag — the A-6 assertions and the mixed-case case run on every engine pull and are the detector R7 asks for; (b) pin a digest in `Directory.Build.props` and change both files together (the AppHost rejects digests today, so this is a code change). **Recommend (a)**, recorded; revisit if an engine update ever changes collation or RLS behaviour. Cost: none.
14. **The Status `PATCH` over the API.** It already lets a Token move a Task to another column, placing it last (2.3). That writes a key but not a *chosen position*, so FR-35 holds. Recommend: leave it; AC8's cases prove a Token cannot choose a position. Cost: none.
15. **2.5's masked row-lock hints (`deferred-work.md:543`).** Recommend: plant them under RACE-M5/M6; if a move race catches one, it is load-bearing and the entry closes naming the gate; if not, keep them (2.5's review judged them harmless) and close the entry as decided. Do not remove them in this story without a measurement. Cost: none.
16. **Deletion's unlocked `ProjectId` read (`deferred-work.md:550`).** A 2.6 move never changes `ProjectId` (FR-41 is 6.5), so the assumption still holds. Recommend: close the 2.6 ownership, re-own the residue to **6.5** (whose move-to-Project must re-check `ProjectId` under the row lock in every Task writer). Cost: 6.5 reads the note.
17. **Capability.** FR-16's matrix has no "move" row; moving is "Create, edit, delete Tasks" (`prd.md:342`). Recommend reusing `ManageTasks` and its `manage-tasks` refusal copy; add no capability. Cost: none.
18. **A move to where the Task already is.** Recommend `200` with the unchanged Task, nothing written, locks still taken; the client still announces the (unchanged) position. Alternative: refuse `409`. Cost: none.

### Architecture compliance (must)

- `SpaceId` never from the request; from `ActiveSpaceContext`. No `Where(SpaceId == …)` in stores (the two-layer plants depend on it).
- State changes only through a slice, in the pipeline's one transaction; the store saves before returning (`IdempotencyStore.EnsureNothingElseIsPending`).
- `404` for absent and foreign, byte-identical and floored; `403` recorded; no in-Space not-found or `4xx` validation refusal recorded (AD-3, AD-20). Error bodies carry no title and no id not in the URL.
- AD-15: a move writes the moved Task's key alone; no sibling `UPDATE`, ever; keys only from `PositionKey`.
- Raw SQL only in `Yello.Infrastructure`, parameterised `FromSql`/`SqlQuery` interpolation composed on the filtered set (`SpaceResolutionSeamTests`).
- No `[Authorize(Roles=…)]`/`IsInRole` (AR-4); no `Results.Created`/`Accepted` (ARCH-009).
- Client: nothing in browser storage; `document.title` stays `Yello`; the URL holds ids only; every string externalised; no disabled state for a Role reason (UX-DR27).

### Library / framework

No new packages (CLAUDE.md: a new package needs Lee's approval). .NET 10.0.11, EF Core 10 (`FromSql`, `SqlQuery`, `UseCollation`, migrations via the local `dotnet-ef` 10.0.11 tool), Microsoft.Data.SqlClient 7.0.2 (error numbers 1205 deadlock, 2601/2627 unique), Blazor WASM 10 (`@oncontextmenu` with `:preventDefault` conditional; `KeyboardEventArgs.Key == "ContextMenu"`, and `"F10"` with `ShiftKey`), xunit.v3 4.0.0, Testcontainers 4.6.0 (`mssql/server:2025-latest`), Microsoft.Playwright 1.62.0 (`ClickAsync(new() { Button = MouseButton.Right })`, `Keyboard.PressAsync("ContextMenu")`/`"Shift+F10"`, `SetViewportSizeAsync`), ArchUnitNET 0.13.3 — as pinned. SQL Server facts: an index key column takes its column's collation (assert via `sys.index_columns`); `UPDLOCK, HOLDLOCK` takes key-range locks (`LCK_M_RS_U`, `LCK_M_RIn_NL` seen in 2.2/2.3), matched by `LIKE 'LCK_M_%'`; `ALTER COLUMN … COLLATE` requires dropping the indexes on the column first.

### Testing standards (from `tests/TESTING-CONVENTIONS.md` and CLAUDE.md)

- Add cases to existing suites; **no new suite, no new container fixture** (`ContainerTopologyGateTests`). One SQL engine per test project, one database per class — hence partials of `TaskEndpointTests`.
- Traits: only `Suite`, `Priority`, `Requirement`, `Assumption`; cite AR and AD together (AR-21 with AD-15). Sentence-style names, one behaviour per test.
- No `Task.Delay`. Races hold the contended lock and wait on `LCK_M_%` (`TESTING-CONVENTIONS.md:101-133`); "when the race is about a read, the test does the other write" (`:113-120`); a lock held between two statements waits "blocked or answered" (`:122-133`); deletions race every writer in both directions, and a Status move is named as a writer (`:135-147`).
- Randomised data; never a shared email. Cleanup by rollback or disposal — never a `DELETE` in cleanup.
- Every absence assertion is proved against a plant. No coverage threshold.
- Playwright fakes the API at the network edge with Contracts types. An optimistic write: **hold, assert the new state, release (admitted / refused / unanswered), assert** — the revert goes to the last *admitted* value (`:232-238`).
- Timing-sensitive cases in `DisableParallelization` collections. Full runs with `--max-parallel-test-modules 1`; never two `dotnet test` at once; a Client failure seen only in the full run is rerun alone before it is called real.

### Previous story intelligence (2.5, 2.4, 2.3, 2.2)

- **Lock ordering is where every previous story found its real bug.** 2.3: a column-only range could deadlock against creation's Project-wide scan → every key placer takes the Project range; its first race cases caught a removed hint only 1/3 by timing, and the read-race case (the test inserts the colliding key itself) caught it 3/3 — **write M2 that way**. 2.4: a real 1205 because two lockers read one row through different indexes → `INDEX(1)` on both; concurrent Labels edits stored a set neither sent → Task row first, Labels in id order. 2.5: range before row, or a Status edit deadlocks the deletion (RACE-T1 caught row-first 3/3 as a 1205); three row hints masked 0/3 (ledger `:543`, yours).
- **2.3 review fixes a move can repeat**: skipping the lock when the target equals the current Status/slot; a Task vanished between the first read and the reload under the lock answered `200`; the client keeping an old slot after an admitted move instead of placing by the server's key.
- **Gates that surprised earlier stories**: response/request shape lists and route sets. Task 6 names the ones this story is expected to break; search for any other exact set over Task routes before mapping.
- **Isolation**: 2.2/2.3/2.4/2.5 each had their ATDD phase add 5–8 pending rows; list them in Task 1. Foreign ids inside one's own Space answer byte-identical to random ones (I-13; `ISO-APPLY-FOREIGN-LABEL` pattern).
- **Client**: live regions exist empty from first render (2.2 review); copy only in resx, never quoted in a C# comment (tripped `No_user_visible_copy_is_written_as_a_csharp_string_literal` in 2.3 and 2.4); rebuild the Debug client before browser plants; 2.4 had to build in Release when orphaned `Yello.Host.exe` held `bin/Debug` (the browser suite still needs a Debug client).
- **2.5 decisions to keep**: DELETE carries no body; a deletion is not optimistic; focus to the column when a card is gone; the panel's last control is Delete; `_deleted` drops late answers for a deleted Task — a move's late answer must honour it too.
- **Environment**: Rancher Desktop, not Docker Desktop; on "timed out dialing Hyper-V socket" run `rdctl shutdown`, `wsl --shutdown`, relaunch, poll readiness (CLAUDE.md).

### Git intelligence

Each story lands as "Add story → Add ATDD scaffolds → implement → Close the code review" (`bc19cea` story 2.5, `9ca31d1` scaffolds, `798b3ec` implementation, `12eba8d` review). `798b3ec` touched Domain ports, two slices, both stores, `BoundCounter` remarks, Contracts, Host endpoints and `Program.cs`, five client components, resx and CSS, five Architecture gates, `IsolationCases.cs`, six Slices partials, handler tests, `DeleteTests.cs`, `TextSpacingTests.cs`, `TESTING-CONVENTIONS.md` and the ledger. Expect a similar footprint plus one generator, one migration (Q12, the email index — none for the Task table), one picker component and a card menu.

### Ledger entries

- `deferred-work.md:494` (owner **2.6**) — moves, the midpoint, the move's own lock: **build and close**.
- `:543` (owner **2.6**) — the masked deletion hints: **decide by measurement (Q15) and close**.
- `:550` (owner **2.6**) — deletion's unlocked `ProjectId`: **close; re-own the residue to 6.5 (Q16)**.
- `:198` (owner **2.6**, Lee 2026-09-10) — the email index collation: **decide (Q12) and close**.
- `:490` (owner UX) — "+" in later columns would be create-then-move: note.
- `:517`, `:549` (owner 2.8) — picker keyboard pattern and identical announcements: note that the Move picker repeats both.
- `:495` (owner 2.9) — Board totals vs Tasks: a move committing between the count and the Task read shows one column's `Total` off by one until the next read; note it.
- New (Task 12): mockup/spine variances (UX), key length under load and the position list at 5,000 Tasks (2.9), Board push of a remote move (Epic 7 / PM), Q13 if deferred.

### Project Structure Notes

- New: `Yello.Application/Tasks/MoveTask/MoveTaskHandler.cs`; a move outcome type beside `ITaskStore` in `Yello.Domain/Tasks`; `Yello.Contracts/Tasks/MoveTaskRequest.cs`; `Yello.Client/Components/MovePicker.razor` (and possibly `TaskActionsMenu.razor`); an EF migration for the email index collation (Q12); `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.{Move,MoveRace,MoveIdempotency}.cs`, `tests/Yello.Tests.Slices/Tasks/MoveWire.cs`, `tests/Yello.Tests.Slices/Tasks/MoveTask/MoveTaskHandlerTests.cs`, `PositionKey` unit tests, `tests/Yello.Tests.Architecture/DesignFoundationGateTests.MovePicker.cs`, `tests/Yello.Tests.Client/MoveTests.cs`.
- Updated: `PositionKey.cs`, `ITaskStore.cs`, `TaskStore.cs` (and its remarks), `TaskRoutes.cs`, `ProblemTypes.cs`, `TaskEndpoints.cs`, `Program.cs`, `AccountConfiguration.cs`, `EmailAddressNormalisation.cs` (remarks), model snapshot, `TaskCard.razor`, `TaskDetail.razor`, `BoardView.razor(.cs)`, `BoardColumn.razor` (if the card needs new parameters), `components.css`, `ClientCopy.resx`, `RefusalCopy.cs`, the gates in Task 6, `BoardTests.cs`, `FakeYelloApi.cs`, `TextSpacingTests.cs`, `IsolationCases.cs`, `TaskEndpointTests.Schema.cs`, `TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml`.
- Variances to record: the AC4 index tuple (Q11); the card's second control and `Enter` (Q6); the position list vs the mockup (Q4); a Session-only Task write (Q1, FR-35's named exception to C-6 parity).

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-2-6-move-and-reorder-a-task-by-explicit-control.md` (Q-dependency table, seed constraints checked, assumed locators, gates that go red by design). Every scaffold follows the recommended answer to Q1-Q18. The route, problem type and request are scaffold constants until Task 5 adds them (`MoveWire.MoveOf`/`TaskPositionUnavailable`, `IsolationCases.MoveRoute`/`MoveOf`/`TaskPositionUnavailable`); `PositionKeyBetweenTests.Between` is a seam to bind to `PositionKey.Between`.

| File | Entries (cases) | ACs | Green today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/MoveWire.cs` | harness (active) | - | - |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Move.cs` | 15 (22) - MOV-01..15 | AC2, AC3, AC5, AC6, AC8, AC9, AC10 | MOV-13, MOV-14, MOV-15 (FR-35 standing gates) |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.MoveIdempotency.cs` | 2 - IDM-M01, IDM-M02 | AC13 | - |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.MoveRace.cs` | 12 - RACE-M1, M2, M3, M4 (a), M5-M8 (a) and (b) | AC5, AC11 | M5 (b), M6 (b), M7 (b), M8 (b) |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.MoveSchema.cs` | 4 - SCH-601..604 | AC4, AC16 | SCH-601, SCH-602 (SCH-602's plant is owed, Task 4) |
| `tests/Yello.Tests.Slices/Tasks/MoveTask/PositionKeyBetweenTests.cs` | 7 - PK-01..07 (no database) | AC3, AC10 | - |
| `tests/Yello.Tests.Shared/WireDatabase.cs` | `TaskPlacementsInAsync` (read-only helper) | AC12 | - |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 5 rows × 2 surfaces, **pending on 2.6** (every `ApiToken` expectation is `NoCredential`, Q1) | AC9, AC12 | - (GATE-001..004 green, 14/14) |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Move.cs` | 7 - ARCH-601..607 | AC3, AC8, AC9 | - |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.MovePicker.cs` | 3 (4) - DSN-601..603 | AC2, AC14, AC15 | DSN-602 (hover-only plant caught 1/1) |
| `tests/Yello.Tests.Client/MoveTests.cs` | 16 (24) - E2E-601..616 | AC1-AC3, AC6, AC7, AC14, AC15 | E2E-613 |
| `tests/Yello.Tests.Client/TextSpacingTests.cs` | +2 (4) - the panel picker, the card menu | AC14 | - |
| `tests/Yello.Tests.Client/FakeYelloApi.cs` | `POST .../move` harness (active) | - | - |

Activation order: **RACE-M2 first** (`A_move_reads_its_neighbours_keys_only_under_the_lock` - the riskiest assumption: Q2 (a) converges only if the move reads its neighbour's current successor under the Project range; bind the PK seam and map the route, then activate it before anything else on the wire), then RACE-M4 (a), M1, M3, M5-M8 (a) with the hint plants (three runs each) and 2.5's masked hints under M5 (b)/M6 (b); PK-01..07; ARCH-604..607, then ARCH-601..603 with the 1.8 Token-unreachable gate and the Task route sets amended by exactly the move route; MOV-01..12 and IDM-M01/02; the five isolation rows with the two-layer plant; SCH-603/604 with Q12's migration and SCH-602's plant; DSN-601/603, then E2E-601..616 and the text-spacing theories (amending `BoardTests`' one-control gate and, if Q8 (b), `TaskDetailTests`' "Moved to Done." deliberately).

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

### References

- `_bmad-output/planning-artifacts/epics.md:1166-1209` (story), `:129, :141` (FR-29, FR-35), `:158, :162` (NFR-5, NFR-9), `:208-213, :222` (AR-20, 21, 22, 24, 25, 34), `:241, :249, :250, :262, :265, :266, :271, :282, :285` (UX-DR6, 11, 12, 21, 24, 25, 27, 38, 41), `:322, :328, :359` (coverage), `:1253-1293` (2.8), `:2654-2657` (7.6), `:2782-2790` (8.1)
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md:336-357` (FR-16 matrix, `:342`), `:526-534` (FR-29), `:594, :605` (FR-35); `addendum.md:142` (interleaving mitigated, not eliminated)
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md` — AD-4 `:100`, AD-11 `:138-142`, AD-15 `:162-168`, AD-17 `:176-180`, AD-18 `:182-186`, AD-19 `:188-192`, AD-29 `:249-259`, conventions `:266-271`, slices `:389, :413, :415`, deferral `:429`
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:73, 85, 134, 145, 199, 201, 206, 211, 221, 283-295, 315, 317, 320, 322-326, 335, 390, 403, 415`; `DESIGN.md:256-262, 473, 504-521`; `mockups/board.html:75, 126-158`
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md:932, 973-976`; `sprint-change-proposal-2026-09-13.md` (nothing on moves)
- `_bmad-output/specs/spec-yello/SPEC.md:149-151, 175`; `acceptance-criteria.md:298-303, 356`; `decisions-settled.md:73, 85`; `harness-constraints.md:22`
- `_bmad-output/test-artifacts/test-design-architecture.md:147, 217, 429, 506-520`; `test-design-qa.md:170, 266-267, 304, 314, 317, 319, 336-341, 355-359, 367, 491`
- `_bmad-output/implementation-artifacts/2-5-delete-a-task-and-delete-a-project.md` (lock order, Debug Log plants 1-5, review findings `:228-253`); `2-3-edit-a-tasks-title-status-and-due-date.md` (Q3, Q7, Q8, Q10, Q11; review fixes `:241-266`; Debug Log `:482-527`); `2-4-define-and-apply-labels.md` (`INDEX(1)` deadlock, `:442`; Labels race `:272, :595`); `2-2-add-tasks-and-see-them-on-a-board.md` (Q2 (a), `:139, :546-550, :703`); `deferred-work.md:20, 198, 490, 494, 495, 517, 532, 543, 549, 550`
- `tests/TESTING-CONVENTIONS.md:7-32, 44-72, 88, 96, 101-161, 219-251`; `CLAUDE.md` (memory budget, race rule, undo rule, isolation rows)

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

**Create-story phase (2026-10-26).** No isolation row is pending on 2.6 (grepped `PendingUntil` in `tests/Yello.Tests.Isolation`). No ATDD scaffold exists for 2.6. Planning inputs read via two research passes (planning documents; stories 2.2–2.4), plus 2.5, `deferred-work.md` and the code; facts above cite paths and lines as read on 2026-10-26. After setting `2-6-move-and-reorder-a-task-by-explicit-control: ready-for-dev`: `dotnet build Yello.slnx` 0 warnings, 0 errors; `IsolationCaseListTests` 14/14 passed (GATE-001..004 green).

### Completion Notes List

- Story created 2026-10-26 by create-story: ultimate context engine analysis completed — comprehensive developer guide created. Open questions Q1–Q18 carry recommended decisions; the dev phase decides each as a reversible, recorded decision.

### File List
