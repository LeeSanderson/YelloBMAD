---
baseline_commit: e541bb1
---

# Story 2.10: View a Project as a filterable, sortable list

Status: done

Epic: 2 — Track your own work on a Board
Story key: `2-10-view-a-project-as-a-filterable-sortable-list`

**Requirements owned.** These come from the Story Coverage Index (`epics.md:363`: "2.10 | **FR-30**, AR-22, UX-DR19").

- **FR-30, partial.** Any Membership can view a Project's Tasks as rows, filtered and sorted by Status, Assignee, due date and Label (`prd.md:536-543`).
  - Filters never surface a Task from another Project or another Space.
  - At 5,000 Tasks the List View still meets NFR-5 and NFR-9 "on the same terms as FR-28".
  - **FR-30 is a staged FR.** Its Assignee dimension completes in **4.6** (`epics.md:400`; readiness report `:1234`, `:1321`; `sprint-status.yaml` header). Do **not** mark FR-30 delivered when this story closes.
- **AR-22.** A List View keyset is `(sortColumn, TaskId)`, with a composite index behind every offered sort. "A List View sort offered in the interface without a matching composite index is a defect, not a slow query" (`epics.md:210`; spine AD-29 `:249-259`, especially `:258`).
- **UX-DR19, `list-view-controls`.** It requires all of the following (`epics.md:257`; `EXPERIENCE.md:217`, `:255`, `:275`):
  - filter and sort
  - pages, never infinite scroll
  - the page size stated
  - keyboard row traversal specified
  - the filter result count announced politely
  - "No Tasks match." with nothing else
- **Test-design scenarios:**
  - **P-3** (P0, INT; `test-design-qa.md:312`): keyset correct at every offered sort, with ties and NULLs, so that no row is skipped or repeated across a page boundary.
  - **P-4** (ARCH; `:358`): every offered sort has a matching composite index. `test-design-qa.md` gives it P1 and `test-design-progress.md:645` gives it P0. **Treat it as P0**, the stricter one. The handoff (`test-design/YelloBMAD-handoff.md:99`) makes P-3 and P-4 acceptance criteria, not follow-ups.
  - **I-3** (P0; `:267`): no listing or aggregate includes a foreign row. `test-design-progress.md:531` names "List View page" among its eight.

**Also binding:**

- **FR-28's terms by reference:** every Task is reachable at 5,000 (`prd.md:524`).
- **NFR-5:** 300 ms p95 server-side read (`prd.md:718`). It is measured warm and the cold path is exempt, as story 2.9 decided for AR-40b (`docs/operations.md` §7).
- **NFR-8:** 5,000 Tasks per Project.
- **NFR-9:** WCAG 2.1 AA, with keyboard parity (`prd.md:753-757`).
- **AD-2:** RLS plus an independent EF filter.
- **AD-3 / AD-20:** an in-Space not-found is `404 not-available`, byte-identical and not recorded.
- **AD-4:** both surfaces.
- **AD-16:** the effective Status set is computed on read.
- **AD-19:** append fields, never remove or retype them.
- **AD-29:** keyset paging, never `OFFSET`. Rows are appended and never recycled, and the true total comes from a separate `COUNT`.
- **UX-DR24:** keyboard grammar.
- **UX-DR26:** no infinite scroll, no hover-only control, no disabled control for a Role.
- **UX-DR27:** absent, not disabled.
- **UX-DR31:** filter and sort state is purged with the Space, synchronously, before any render (`EXPERIENCE.md:164`).
- **UX-DR33:** the title stays fixed and routes carry ids only.
- **UX-DR35:** cold-load skeleton, `aria-busy`.
- **UX-DR37:** voice.
- **UX-DR38:** announce on deliberate action only, which includes "a filter count", and never on loading or paging (`EXPERIENCE.md:383-390`).
- **UX-DR39:** focus follows the Task identity (`EXPERIENCE.md:404`).
- **UX-DR40:** 1.4.12 text spacing, 200% zoom, 320px reflow.
- **UX-DR42:** logical properties, sentence case, dates in the viewer's locale and zone.
- **`EXPERIENCE.md:165`:** no Space-scoped content in `localStorage`, `sessionStorage` or IndexedDB.

**Explicitly not in this story:**

- The **Assignee** filter and sort, and its composite index (**4.6**; AC2).
- The bulk "move a whole Status" from a List View filtered to one Status (**6.6**, `epics.md:2339`).
- Search (`prd.md:834`: no search in v1 beyond the Project).
- Creating, editing or moving a Task *from a row* (open question 9).
- A descending sort direction (open question 5).
- Live push of other people's changes (**Epic 7**).
- The <768px Status pager (**2.11**).
- axe-core (X-3, blocked on Lee's dependency approval).
- **No new NuGet package and no new project** (CLAUDE.md). **No Azure deployment, login or push.**

**Ledger entries owned by this story (`deferred-work.md`):**

- `:524`, "Sorting a List View by Label". This is decided by open question 3 and folded into AC13 and Task 2.
- The ledger has no other entry owned by 2.10. This was checked by grepping for `2.10`, `List View`, `sort` and `filter` on 2026-10-28.

**Code hooks that name 2.10:**

- `Yello.Infrastructure/Persistence/Configurations/TaskConfiguration.cs:64-66` says "No index - sorting by due date is 2.10's."
- `TaskLabelConfiguration.cs:24-26` says the index `(SpaceId, LabelId, TaskId)` is "for story 2.10's filter by Label".
- Story files: 2.3 `:16`, `:127` (sort by due date, index decided here) and 2.4 `:18`, `:134`, `:487` (Label filter and sort; Q12 says "2.10's sort decides its own meaning").

**Depends on:**

- **2.2:** the Board, the Board page, `SpaceArrival`.
- **2.3:** `DueDate`, `TaskDates`.
- **2.4:** Labels, `TaskLabel`, `LabelChip`, the `LabelRoutes` read.
- **2.8:** `LiveAnnouncement`, roving tabindex, the key-module pattern.
- **2.9:** `BoardPage`, keyset paging, the cursor 400, RCSI, application locks, the scale harness, the tiers.

All of them are done.

> **Isolation gates.** This story maps a **new Space-scoped route** (the List View page, open question 1). **1.9-GATE-002** therefore requires isolation rows on **both surfaces** before `review`. Route discovery reads the Host's endpoint data source, so the suite goes red until those rows exist. No row is pending today. If a row is added as pending, **GATE-003** requires it live before `review`.

## Story

As someone looking for particular work,
I want the same Tasks as rows I can filter and sort,
so that I can answer a question the Board's shape does not answer.

## Acceptance Criteria

AC1 to AC9 are reproduced **verbatim** from `epics.md:1346-1384` (`status: final`) and numbered by position. AC10 to AC22 come from the spine, the UX spines, the test design and the ledger, and each one cites its source. Where an AC needs a decision to read it, the open question that decides it is named.

**AC1 — the same Tasks as rows**
**Given** a Project
**When** the List View is opened from the Board's view toggle
**Then** the same Tasks render as rows, filterable and sortable by Status, due date and Label
*(The view toggle: open question 6. What sorting "by Label" means: open question 3. What filtering "by due date" means: open question 4.)*

**AC2 — no Assignee yet**
**Given** the Assignee dimension
**When** this epic ships
**Then** it is absent from filter and sort, because no Task holds an Assignee until FR-21 in Epic 4
**And** Epic 4 adds that dimension together with its supporting composite index

**AC3 — no foreign row**
**Given** any filter
**When** results render
**Then** no Task from another Project or another Space appears

**AC4 — the pair keyset**
**Given** a List View sort
**When** its query runs
**Then** the keyset is `(sortColumn, TaskId)` with `TaskId` as a mandatory tiebreaker and the seek predicate comparing the pair
**And** this is required because every offered sort column is non-unique, and a naive keyset on a tied value silently skips or repeats rows across a page boundary

**AC5 — NULL fixed at one end**
**Given** a nullable sort column such as due date
**When** it is sorted
**Then** `NULL` is fixed at one end explicitly rather than left to the default
*(Which end: open question 5. The Label sort key is nullable too, and the same rule applies to it.)*

**AC6 — a composite index per offered sort**
**Given** a sort offered in the interface
**When** the schema is inspected
**Then** a composite index carries both columns in that order
**And** a sort offered without one is a defect rather than a slow query, because at 5,000 Tasks it is a stated-budget failure

**AC7 — paged, not infinite, at 5,000**
**Given** the List View at 5,000 Tasks
**When** it renders
**Then** it pages with a stated page size and never scrolls infinitely, and keyboard row traversal is operable throughout
**And** paging rather than virtualising sidesteps the focus-recycling problem entirely
*(Page size and paging control: open question 2. Row traversal: open question 8.)*

**AC8 — the empty filter**
**Given** a filter yielding nothing
**When** it renders
**Then** it states "No Tasks match." and nothing else — no suggestion to broaden, and no count of what was excluded

**AC9 — the count is announced**
**Given** a filter change
**When** results settle
**Then** the result count is announced politely

**AC10 — the read meets NFR-5 at every sort's last page** *(FR-30 "on the same terms as FR-28", `prd.md:543`; NFR-5; AD-29 `:254`; P-3/P-4 measured like 2.9's P-2)*
**Given** a Project holding 5,000 Tasks with ties on every sort column, about a third with no due date and about a third with no Label
**When** the first page and the last page of each offered sort are read, both unfiltered and under the heaviest filter combination (open question 4)
**Then** each one meets the 300 ms p95 server-side read budget, measured warm (AR-40b as decided in 2.9)
**And** the last page reads no more logical pages than the first, within the tolerance 2.9 used (PG-11: warm-up first, tolerance 5). This is the deterministic proof; the timing is the Nightly proof.

**AC11 — the true total, from its own count** *(AD-29 `:256`; X-6 by analogy; UX-DR19 "result count")*
**Given** any filter
**When** a page is served
**Then** it carries the true number of the Project's Tasks matching that filter, from a separate `COUNT` over the same filter predicate, never the number of rows served
**And** the row count the view states, the announced count and the table's `aria-rowcount` all come from that one number

**AC12 — the server owns page size and cursor** *(2.9 Q1 (a), AR-22 "the seek shape is the invariant")*
**Given** the List View route
**When** a client sends a page size
**Then** the size is not honoured, because the page size is one named server constant
**And** a cursor that is not a cursor this route issued for the same sort is refused `400` with its own problem type, which depends only on what was sent
**And** a cursor naming a Task that no longer exists, or whose sort value has changed, still seeks to the first row after the cursor's pair, so nothing is skipped or repeated *(open question 1)*

**AC13 — sorting by Label means one stated, indexed key** *(`deferred-work.md:524`; open question 3)*
**Given** Tasks carrying zero, one or several Labels
**When** the List View is sorted by Label
**Then** each Task appears exactly once, ordered by the key open question 3 decides. The recommendation is the name of its first Label in the order its chips already show (story 2.4 Q12).
**And** Tasks with no Label are fixed at one end (AC5), and that key carries a composite index (AC6)

**AC14 — the Status sort is the effective order** *(AD-16; open question 7)*
**Given** the List View sorted by Status
**When** rows render
**Then** Statuses follow the Project's effective order, the Board's column order, and never id or name order
**And** within one Status, rows follow the `TaskId` tiebreaker

**AC15 — filters name only this Space's things, and a foreign id matches nothing** *(AC3, I-13 pattern from 2.3 and 2.4; open question 4)*
**Given** a filter naming a Status or Label id that is not one of this Project's effective Statuses or this Space's Labels, whether random, another Space's real id, or deleted since it was chosen
**When** the page is read
**Then** the answer is byte-for-byte the same for a random id and for another Space's real id, and reveals nothing about either
**And** no Task outside the filter's own Project appears

**AC16 — every Role reads it, nothing is disabled** *(`prd.md:512` "read-available to every Role"; UX-DR27; FR-35)*
**Given** a Viewer
**When** the List View renders
**Then** it is exactly a Member's List View: same rows, same filters, same sorts, same paging, on both the Session and the Token surface (the read capability `ReadProjectsAndTasks`)
**And** no control anywhere in it is disabled for a Role reason

**AC17 — purged with the Space, never persisted** *(UX-DR31, `EXPERIENCE.md:91`, `:164-165`, `:177`)*
**Given** filter and sort state
**When** the Space is left (switch, sign-out, Session end) or the view is left
**Then** the state is gone synchronously before any render, and nothing carries over
**And** none of it is written to `localStorage`, `sessionStorage` or IndexedDB, and no Label or Status *name* reaches the URL or `document.title`

**AC18 — the rows are appended, never recycled, and focus follows the Task** *(AD-29 `:253`, `:255`; X-5; UX-DR39)*
**Given** rows already rendered
**When** the next page is loaded
**Then** rows are appended and keyed by Task id, no element is ever reused for another Task, and nothing in the client virtualises a list. The existing gate `BoardPagingGateTests.Nothing_in_the_client_virtualises_a_list` must keep passing over the new files.
**And** after a page loads by keyboard, focus lands on the next Task **by identity**

**AC19 — nothing is announced on loading or paging** *(UX-DR38; 2.9 AC6)*
**Given** a page loads, more rows are appended, or the view first renders
**When** it completes
**Then** nothing is announced except the cold load's one completion sentence (UX-DR35, as the Board does), and the AC9 count on a deliberate filter change

**AC20 — the stated row state of each row** *(UX-DR8 card metadata mirrored, `DESIGN.md:451`; UX-DR40; story 2.3 AC7)*
**Given** a row
**When** it renders
**Then** it shows the title, the Status name, the due date in the viewer's own locale and zone (or nothing when there is none), and at most three Label chips followed by `+N`
**And** at a 320px viewport and under the 1.4.12 text-spacing override, nothing requires two-dimensional scrolling (open question 10)

**AC21 — isolation rows on both surfaces** *(1.9-GATE-002; I-1, I-3, I-4/I-5; TESTING-CONVENTIONS isolation section)*
**Given** the new route
**When** the isolation suite runs
**Then** it holds live rows on **both** surfaces for:
- a foreign Space's List View, which answers the boundary 404
- a foreign Project id in one's own Space, which answers 404 `not-available`, byte-identical
- the aggregate: walking every page under an own-Space Label filter yields exactly the caller's Tasks, and the total is exactly theirs
- a foreign Label or Status id in the filter, which matches nothing and answers bytes identical to a random id
**And** each row's log slice holds no Task title or Label name (I-17)

**AC22 — the wire is stated once and appended only** *(AD-19; 2.9's gate pattern)*
**Given** the List View contract
**When** it is inspected
**Then** its route is stated once, in `Yello.Contracts`, beside the Board's. Its response type has exactly the members open question 1 decides, and carries no `SpaceId`.
**And** `TaskResponse` is reused unchanged, so a row is the same Task the Board serves

## Tasks / Subtasks

- [x] **Task 1 — Decide the open questions (AC all).**
  - [x] Read the Dev Notes' open questions. Take each one as a reversible decision and record it in Completion Notes as "Qn - (x)", with a one-line reason. Change a recommendation only on evidence, for example a measurement or a translation that does not work. Never change one for taste.
- [x] **Task 2 — Schema: the sort keys and their indexes (AC4, AC5, AC6, AC13, AC14; Q3, Q5, Q7).**
  - [x] (a) `TaskItem`: add `FirstLabelName` (`string?`, at most `LabelName.MaxLength` = 50), the Q3 sort key.
    - Use the same column type and collation as `Label.Name` (`nvarchar(50)`, database default), so its order is the chip order of story 2.4 Q12.
    - Name it in the Glossary's terms in docs. It is Infrastructure-maintained, and no request contract carries it.
  - [x] (b) `TaskConfiguration`: add three indexes. Leading `SpaceId` keeps every seek inside one Space's range, as 2.9's index does.
    - `(SpaceId, ProjectId, StatusId, Id)`, the Status sort and the Status filter
    - `(SpaceId, ProjectId, DueDate, Id)`, the due-date sort
    - `(SpaceId, ProjectId, FirstLabelName, Id)`, the Label sort
    - Each `INCLUDE (StatusId)` where `StatusId` is not already a key column, so a Status filter under another sort needs no key lookup (Q4). Measure with and without in Task 8 and keep `INCLUDE` only if it earns its place.
    - Replace the `TaskConfiguration.cs:64-66` comment.
  - [x] (c) One migration, `ListViewSortKeys`. It is expand-only (TESTING-CONVENTIONS migrations section): a nullable column, a set-based backfill, then the three indexes.
    - The backfill is `UPDATE t SET FirstLabelName = (SELECT TOP (1) l.Name FROM TaskLabel tl JOIN Label l ON … WHERE tl.SpaceId = t.SpaceId AND tl.TaskId = t.Id ORDER BY l.Name, l.Id)`, run per Space by the migration's own connection.
    - RLS: confirm how earlier data migrations ran. A migration connection has no `SESSION_CONTEXT`, so check whether the FILTER predicate hides every row. If it does, follow whatever 2.4's or 2.9's migrations did, or state the bypass in the migration's remarks.
    - Fix the analyser warnings by hand: file-scoped namespace, docs, composite index columns in static fields, `Up` at 80 lines or fewer. Never add `// <auto-generated />`.
    - Update `MigrationBundleEquivalenceTests` and the schema snapshot as 2.9 did.
  - [x] (d) Keep `FirstLabelName` true on every write that changes a Task's Labels.
    - Today that is `TaskStore.ReplaceAsync` (`TaskStore.cs:212`, the Labels edit). Recompute the key in the same transaction, after the replace, from the final set, in one statement, with the same `ORDER BY l.Name, l.Id` as `TaskLabelReads`.
    - Creation sets it `null` (a new Task carries no Labels).
    - Label deletion is refused while a Label is in use (2.4 AC3), and Label rename does not exist (2.4 Q10), so neither needs a write. Ledger the rename half (see Ledger dispositions).
    - Prove it with slices for apply, remove-first, replace-all and clear. A concurrent-edit case must leave the key equal to the final set's first name: two edits of one Task serialise on its row, so control the overlap with the existing race helpers. Never use timing.
- [x] **Task 3 — Server read: the slice (AC1, AC3–AC5, AC10–AC15; Q1, Q2, Q4, Q5, Q7).**
  - [x] (a) Domain port `Yello.Domain/Lists/IListReader.cs`. The spine capability map names `Application/Lists/*` (`:415`). It has these members:
    - `FindProjectAsync`. You may reuse `IBoardReader`'s by injecting both, but do not widen `IBoardReader`.
    - `CountAsync(projectId, ListFilter)`
    - `PageAsync(projectId, ListFilter, ListSort, ListCursor?, int take)`
    - `LabelIdsOfAsync`, which delegates to `TaskLabelReads` as `BoardReader` does
  - [x] (b) `Yello.Application/Lists/ListPage.cs`, which mirrors `BoardPage`:
    - `Size => 50`, one named constant (Q2)
    - asks for `Size + 1` rows
    - builds the cursor from the last served row's pair
    - `null` exactly when nothing follows
  - [x] (c) `Yello.Application/Lists/ViewList/ViewListHandler.cs`:
    1. Find the Project; if it is not found, return `null` (the endpoint answers 404).
    2. Compute the effective Status set (`EffectiveStatusSet.Of`).
    3. Narrow the filter's Status ids to the effective set and its Label ids to the Space's Labels. Ids outside them match nothing (Q4/AC15). Never throw, and never answer differently for a foreign id.
    4. Count, page, then read the Labels.
    5. Return the page with the Project's effective Statuses, in order (Q1).
    - Include no Role checks, no Space checks and no locks (AR-3; RCSI reads never wait, 2.9 Q10). Record "statements, not one snapshot" in its remarks, exactly as `ViewBoardHandler` does.
  - [x] (d) `Yello.Infrastructure/Persistence/ListReader.cs`. It contains no `Where` on `SpaceId` (the policy and the filter supply it) and implements the three sorts.
    - **Due date (Q5, NULLs last):** two segments on `(SpaceId, ProjectId, DueDate, Id)`.
      - The dated segment is `DueDate IS NOT NULL AND (DueDate > @v OR (DueDate = @v AND Id > @id)) ORDER BY DueDate, Id`.
      - It is followed, in the same page when the dated segment runs out, by the undated segment `DueDate IS NULL AND Id > @id ORDER BY Id`.
      - The cursor says which segment it is in.
    - **Label (Q3):** the same two-segment shape on `FirstLabelName`.
    - **Status (Q7):** one seek per effective Status in effective order, on `(SpaceId, ProjectId, StatusId, Id)`.
      - Within the cursor's Status, read `Id > @id`. Statuses after it start from their beginning, and Statuses before it are skipped.
      - Stop when `take` rows are gathered. Read at most the effective Statuses the filter admits, so the page is 3 to 5 bounded statements, as 2.9's first paint is.
    - **The `TaskId` comparison must happen in SQL.** SQL Server orders `uniqueidentifier` by its low-order bytes first (spine `:268`), so .NET `Guid` order is not SQL order.
      - Never sort or compare Task ids in .NET.
      - Verify that EF translates `task.Id.CompareTo(id) > 0` to `Id > @id` by reading the SQL. If it does not, use `FromSql` on `dbContext.Tasks`, which keeps the query filter, inside `Infrastructure` (AD-2 allows raw SQL there only).
      - In tests, the expected order comes from `System.Data.SqlTypes.SqlGuid` comparison or from an `ORDER BY` in SQL, never from `Guid.CompareTo`.
    - **Filters (Q4):**
      - Status: `StatusId IN (…)`.
      - Label: `EXISTS (SELECT 1 FROM TaskLabel WHERE TaskId = t.Id AND LabelId IN (…))`, any-of. The PK `(SpaceId, TaskId, LabelId)` serves it.
      - Due: a half-open `[from, to)` range, or `DueDate IS NULL` for "No due date".
      - The count uses the same predicate with no sort.
    - **Parameter types:**
      - `FirstLabelName` takes the column's own `nvarchar(50)` mapping.
      - `DueDate` takes `datetimeoffset`.
      - Check that no conversion is in the plan, as PG-12 does.
  - [x] (e) Register the handler and reader in `Yello.Host/Program.cs`, beside `ViewBoardColumnHandler` (`:113-114`).
- [x] **Task 4 — Endpoint and contract (AC12, AC15, AC16, AC22; Q1).**
  - [x] (a) Add `Yello.Contracts/Lists/ListRoutes.cs` (or `BoardRoutes.List`; Q1 decides):
    - The route `GET /api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/list`, with query `sort`, `status` (repeatable), `label` (repeatable), `dueFrom`, `dueTo`, `dueNone` and `after`.
    - Builders that escape every value.
    - No size parameter.
  - [x] (b) Add `Yello.Contracts/Lists/ListPageResponse.cs`, exactly as Q1 decides. The recommendation is `ListPageResponse(BoardProjectResponse Project, IReadOnlyList<ListStatusResponse> Statuses, int Total, IReadOnlyList<TaskResponse> Tasks, string? NextAfter)`, with `ListStatusResponse(Guid Id, string Name)`.
    - It carries no `SpaceId`.
    - Sort codes are stable strings in Contracts (`ListSorts.Status`, `.DueDate`, `.Label`), so the client and Host share one constant.
  - [x] (c) Add `Yello.Host/Endpoints/ListEndpoints.cs`: `MapGet(…).RequiresCapability(Capability.ReadProjectsAndTasks)`. Check the request in this order, because each check depends only on what was sent:
    1. An unknown `sort`, a malformed id, or a malformed or out-of-range due bound (the same 2000-2100 window `TaskDueDate` uses) answers `400 invalid-list-query`.
    2. A cursor this route did not issue for this sort answers `400 invalid-list-cursor`.
    3. Then the handler runs. A `null` result answers `BoundaryProblems.NotAvailable()`, which is not recorded.
    - Add the two problem types to `ProblemTypes.cs` with remarks in the house style ("never a statement about a stored …"), and add their client copy to `RefusalCopy`.
  - [x] (d) Map the route beside `MapBoardColumn()` (`Program.cs:403-404`), before `MapUnmatchedApi()`.
  - [x] (e) Amend each gate that enumerates routes or contracts by exactly this addition:
    - the ARCH-503 route list
    - `RefusalBoundaryGateTests` vacuity guards
    - any wire pin
- [x] **Task 5 — Client: the List View page and the toggle (AC1, AC7–AC9, AC11, AC16–AC20; Q2, Q6, Q8, Q9, Q10).**
  - [x] (a) Add `ClientRoutes.List(spaceId, projectId)` → `/spaces/{s}/projects/{p}/list`. It carries ids only and the Space leads, so the purge hook and `SpaceIn` cover it unchanged. Add `Pages/ProjectList.razor`, wrapped in `SpaceArrival`, exactly as `Pages/Board.razor` is.
    - Name it so it cannot be confused with the existing `Components/ProjectList.razor` (Space home's list of Projects), for example `Pages/TaskListPage.razor` with component `TaskListView`.
  - [x] (b) Add a view toggle on both views: a `nav` with two links, "Board" and "List", with `aria-current="page"` on the current one (Q6).
    - Put it in a small shared component, rendered under the page heading by `BoardView` and by the new view.
    - `BoardView` is at S1200's cap of 30 dependencies (2.9 `:269`), so add no injected service to it. Rendering a parameterless child component is fine.
  - [x] (c) Add `Components/TaskListView.razor`, with partials if it grows:
    - **Load order.** Read the Labels first (`LabelRoutes`, as `BoardView.LoadLabelsAsync` does), then the first page.
    - **Skeleton** with `aria-busy`, and one cold-load sentence, "List loaded." (UX-DR35). Paging says nothing (AC19).
    - **Filters (Q4).** Use native controls, with a visible label on each:
      - a Status checkbox group over the response's effective Statuses
      - a Label checkbox group over the Space's Labels, in the server's order
      - a due-date choice: Any / No due date / a from-to day range in the viewer's zone, sent as instants with an offset (2.3 Q4's rule)
    - **Sort.** A sort `select` (Status / Due date / Label) with **Status preselected as the view's initial order** (Q5/Q7).
      - A sort is a view order, not a consequential choice, so the picker's no-default rule (`EXPERIENCE.md:211`) does not apply. Record this.
    - **A change to any filter or sort.**
      1. Drop the loaded rows. Changing the filter is a new list, so this is not recycling: the old elements are removed, never re-pointed.
      2. Read the first page.
      3. When it settles, say the AC9 sentence once: "{n} Tasks match.", "1 Task matches." or "No Tasks match.".
      4. Drop a response that a later change has superseded, using a generation counter like `BoardView._generation`.
    - **Rows (Q8).** A native `<table>` with a caption naming the Project.
      - Column headers: Task, Status, Due, Labels.
      - `aria-rowcount` is the true total + 1 (for the header row), and each row's `aria-rowindex` is its true ordinal + 1.
      - Each row carries `data-task-id`, and `@key` is the Task id.
      - Labels show at most three `LabelChip`s, then `+N`, reusing the card's rule and the `TaskDates` formatting.
    - **Stated page size (Q2).** Under the table, show "Showing {loaded} of {total}", which is visible and inspectable, and a "Show more Tasks" button while a cursor remains.
      - The button appends the next 50 rows. Focus moves to the first new row by identity, and nothing is announced.
    - **Empty states.**
      - When a filter is active and the total is 0, show "No Tasks match." alone, with no table header, no count and no suggestion (AC8).
      - When no filter is active and the total is 0, decide by Q11.
    - **Purge.** Register with `ActiveSpace.RegisterPurge`, clearing filters, sort, rows, cursor and announcement synchronously. Never use browser storage.
  - [x] (d) Copy goes in `ClientCopy.resx` only, in sentence case with no exclamation marks:
    - the toggle names, filter and sort labels, "No due date", "Any due date"
    - "Show more Tasks", "Showing {0} of {1}"
    - the three count sentences, "List loaded."
    - the two refusal copies
- [x] **Task 6 — Keyboard row traversal (AC7, AC18; Q8).**
  - [x] The rows are **one roving tab stop**, the active row's title cell:
    - `↑`/`↓` move between loaded rows.
    - `Home`/`End` go to the first and last loaded rows.
    - `↓` on the last loaded row loads the next page, and focus lands on the next Task **by identity**. Further `↓` is ignored while a fetch is out, as 2.9's AC11 does.
    - `Space` and `Tab` keep their native meaning.
    - No `role="grid"` and no `role="application"`. The native table keeps the screen reader's table commands in browse mode.
  - [x] Pure traversal goes in a `ListTraversal` class, tested in `tests/Yello.Tests.Architecture`, like `BoardTraversalTests`.
  - [x] Per-key `preventDefault` uses a JS module in the 2.8 pattern (`board-keys.js`). Either add `list-keys.js` or generalise the module; do not fork its logic in two places without a reason recorded in Completion Notes.
  - [x] RTL needs nothing here (vertical only), and the gate that bans `role="application"` must keep passing.
- [x] **Task 7 — Isolation rows (AC3, AC15, AC21).**
  - [x] In `tests/Yello.Tests.Isolation/IsolationCases.cs`, add these rows, each with `Both(...)`, seeded through `ProductActor`:
    - `ISO-DIRECT-LIST` (I-1): B's Space and Project through A. Expected: `BoundaryNotFound`.
    - `ISO-LIST-FOREIGN-ID-IN-OWN-SPACE` (I-4/I-5): B's Project id in A's Space. Expected: `SameAsAnchor(404, not-available)`.
    - `ISO-LIST-AGGREGATE` (I-3): more than 50 of A's Tasks and some of B's. Walk every page under each sort with an own-Label filter; the walk yields exactly A's ids, the total is A's, and no B title is present.
    - `ISO-LIST-FOREIGN-FILTER-IN-OWN-SPACE` (I-13 shape): B's real Label id and B's real Status id as filters in A's own Project. The answer is byte-identical to a random id's (`SameAsAnchor(200, …)`).
  - [x] Prove each row against a plant that drops the Task's layers (the query filter and the FILTER predicate) or skips the handler's narrowing. Record plants and catch rates. Restore each plant in a `finally`, never by `git checkout`, `git restore` or `git reset`.
  - [x] Keep a local `PageSize = 50`, as 2.9 did: Isolation references only Host and Contracts.
  - [x] Treat Label names as content for the I-17 log check (`HoldsProjectNamed` is the precedent).
- [x] **Task 8 — Slices: correctness and cost (AC4–AC6, AC10–AC15; P-3).**
  - [x] Add `tests/Yello.Tests.Slices/Lists/ListPagingTests.cs` and a `ListWire.cs` helper modelled on `BoardPageWire`. Use one database per class on the shared engine: **no new engine and no new fixture type** (`ContainerTopologyGateTests`).
    - **P-3, the five sorts:** Status, due date (dated ties), due date (a page boundary that crosses into the NULL segment), Label (ties), and Label (crossing into the NULL segment).
      - Use page boundaries that land inside a run of ties: for example 120 Tasks sharing one due date, and 70 sharing one first Label.
      - Walk to the end. Every Task appears exactly once, in the SQL order of the pair. Compute the expected order with `SqlGuid` or SQL, never with `Guid`.
    - **Filters:** each filter alone and in combination, the count equal to the walk's length, and any-of semantics for Labels.
    - **A stale cursor (AC12):** delete the cursor's Task, or change its due date or Labels, between pages. The next page still starts at the first pair after the cursor.
    - **Malformed input:** cursors (the wrong sort, garbage, too long) answer `400 invalid-list-cursor`; sort, id or due input answers `400 invalid-list-query`.
    - **Page size:** a `limit` or `pageSize` parameter is ignored.
    - **Viewer:** a Viewer is served the Member's bytes on both surfaces.
    - **The `FirstLabelName` maintenance cases** from Task 2 (d).
    - **Deterministic cost (AC10):** logical reads of the first page versus the last page for each sort at 5,000 Tasks. Use the `SET STATISTICS IO` technique from `BoardPageWire` and 2.9 PG-11: warm up first, tolerance 5. Plant an `OFFSET` (or a sort with its index dropped) and see it caught.
    - **Plan check:** read the plan and confirm each sort seeks its named index with no implicit conversion, as PG-12 does.
  - [x] Add `tests/Yello.Tests.Slices/Lists/ListScaleTimingTests.cs`:
    - `Tier=Nightly`, `[Fact(Explicit = true)]`, in `BoardScaleCollection`, reusing `RunningHost.ServerDurationOfAsync` and `Percentile.NearestRank`
    - at least 100 samples after warm-up
    - the first and last page of each sort, unfiltered and under the heaviest filter combination, each p95 at most 300 ms
    - print the measurement-condition line as `EnvelopeLoadTests` does
    - **Do not run Nightly in the PR stage.** `ci.yml` already excludes the tier by trait.
- [x] **Task 9 — Architecture gates (AC6, AC18, AC22; P-4).**
  - [x] Add `tests/Yello.Tests.Architecture/ListViewGateTests.cs`:
    - **P-4, P0.** For every sort code in `ListSorts`, the EF model holds an index on `Task` whose key columns are exactly `(SpaceId, ProjectId, <sort column>, Id)`. The sort-to-column map is stated once, in Infrastructure, and the gate reads it.
      - Its detector must catch a planted sort code with no index (TESTING-CONVENTIONS: an absence assertion is validated against a planted signal).
    - The route is stated once. The contract members are exactly as Q1 decides, with no `SpaceId`. The page size is one named constant of 50. The two problem types are distinct.
    - The existing virtualisation gate's anchor still finds the Board, and the new files are within its scan.
  - [x] Traits: `Suite`, `Priority`, and `Requirement` with **both** the AR id and the AD id (`AR-22`, `AD-29`), plus `FR-30` and `UX-DR19` where they apply.
- [x] **Task 10 — Browser cases (AC1, AC7–AC9, AC16–AC20).**
  - [x] Add `tests/Yello.Tests.Client/ListViewTests.cs` in `ClientSurfaceCollection`, using `FakeYelloApi`. Add a List route handler to it, opt-in and not changing existing bytes, with a hold for a page.
  - [x] Cases:
    - The toggle from the Board opens the List View, and back again; `aria-current` is right on each.
    - Rows show the title, Status, due date in the viewer's zone, and chips with `+N`.
    - A filter change announces "{n} Tasks match." once (use `RecordAnnouncements`/`WaitUntilSaidAsync`). An empty result shows only "No Tasks match." and announces it.
    - Paging says nothing; "Showing 50 of 812" is visible.
    - `↓` on the last loaded row loads the next page, and focus lands on the next Task by identity. Tag the element (`window.__was`) and assert that it is still connected and still the same `data-task-id`.
    - With 150 of 5,000 loaded, `aria-rowcount` is 5001 and each `aria-rowindex` is its true ordinal.
    - A Viewer gets the same view and no disabled control.
    - A Space switch purges filters and rows before any render, and nothing is in Web Storage. Assert that `localStorage`/`sessionStorage` length is unchanged.
    - A page answered 404 raises the refusal surface.
    - 320px reflow and text spacing hold.
    - Run across `BrowserLocales`.
  - [x] Build `Yello.Client` in **Debug** before running. Rerun a failure that appears only in the full run on its own (MEMORY: the full run starves the browser suite).
- [x] **Task 11 — Ledger and records (all).**
  - [x] In `deferred-work.md`, close `:524` with the Q3 decision and its evidence, and add the entries in the Ledger dispositions table, each with an owner.
  - [x] Amend the 2.3 and 2.4 code comments that name 2.10 (`TaskConfiguration.cs:64-66`, `TaskLabelConfiguration.cs:24-26`).
  - [x] Update `ARCHITECTURE-SPINE.md` **only** if Q1, Q3 or Q7 changes something the spine states. AD-29 already states the rule, so the recommendation is no spine change, with a note in Completion Notes.
- [x] **Task 12 — Verify.**
  - [x] `dotnet build Yello.slnx` gives 0 warnings and 0 errors.
  - [x] Run `dotnet build-server shutdown`.
  - [x] Iterate on one project at a time with `--filter`.
  - [x] The full run is `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. **Rebuild before every `--no-build` run.** Never run two `dotnet test` processes at once.
  - [x] Run the Nightly test once with `--explicit on` and record its p95 figures.
  - [x] Record totals (passed, failed, skipped) in the Dev Agent Record. Expect 3 bundle skips plus the Explicit tiers.

### Review Findings

Code review 2026-10-28 (bmad-code-review, diff `e4cd50d..HEAD`, full mode): 36 raw findings (Blind Hunter 15, Edge Case Hunter 11, Acceptance Auditor 10), triaged to 1 decision, 12 patches, 5 deferrals and 11 dismissed.

- [x] [Review][Decision] The Label sort's cursor carries the first Label's name in the List route's query string — `ListCursorText` encodes `FirstLabelName` as base64url text with no protection, so every "Show more Tasks" request under the Label sort puts a Label name (Space content, I-17) in an API URL. The Host logs no request lines (`Microsoft.AspNetCore` at Warning), and the I-17 log slice passes, but an ingress, proxy or APM log that records query strings would hold the name. "No names in URLs" (Dev Notes, UX-DR33) was written for client routes. Options: (a) accept it, and state that the cursor is an opaque API parameter and not a route; (b) protect the cursor with the Host's existing Data Protection key ring, so a key rotation turns old cursors into `400 invalid-list-cursor`; (c) carry no value, and have the server read the cursor Task's current `FirstLabelName`, which needs a fallback when that Task is gone or has moved. **Resolved: (a), decided by Lee.** Recorded in Completion Notes; query-string logging is ledgered to Lee.
- [x] [Review][Patch] A due-date cursor in the dated segment, replayed with "No due date", skips undated Tasks with lower ids: `after` is never cleared [Yello.Infrastructure/Persistence/ListReader.cs:150]
- [x] [Review][Patch] When the count and the page disagree (concurrent writes, between statements), the list renders blank (total > 0 with no rows), "Showing 51 of 50", or "Showing 100 of 300" with no "Show more Tasks" [Yello.Client/Components/TaskListView.razor.cs:344]
- [x] [Review][Patch] An unfiltered empty Project announces "No Tasks match." while the page shows "Nothing here yet." (Q11 (a)), on a sort change or when the last filter is cleared [Yello.Client/Components/TaskListView.razor.cs:232]
- [x] [Review][Patch] "Between dates" with no date entered counts as filtered, so an empty Project says "No Tasks match." [Yello.Client/Components/TaskListFilters.cs:69]
- [x] [Review][Patch] A `200` whose JSON body is `null` leaves the skeleton up for good, with no refusal [Yello.Client/Components/TaskListView.razor.cs:320]
- [x] [Review][Patch] ISO-LIST-AGGREGATE never checks the body served on the surface under test (the Token surface): every page it checks is re-read on A's Session (AC21, both surfaces) [tests/Yello.Tests.Isolation/IsolationCases.cs:1599]
- [x] [Review][Patch] ISO-LIST-FOREIGN-FILTER-IN-OWN-SPACE's comment names a plant it cannot catch (0/2, masked); `ViewListHandlerTests` is what catches it [tests/Yello.Tests.Isolation/IsolationCases.cs:411]
- [x] [Review][Patch] Two `Requirement` traits name the wrong requirement: LV-19 says AD-15 (position keys), and the virtualisation gate says UX-DR24 (keyboard) [tests/Yello.Tests.Slices/Lists/ListPagingTests.cs:935, tests/Yello.Tests.Architecture/ListViewGateTests.cs:197]
- [x] [Review][Patch] No test pins the cursor parser's refusal branches (wrong part count, unknown sort or segment, a non-GUID id, an over-long or empty Label value, a non-round-trip date) [tests/Yello.Tests.Slices/Lists/ViewList/ListCursorTextTests.cs]
- [x] [Review][Patch] `display: flex` on the Labels `<td>` takes the cell out of table layout; the flex belongs on an inner wrapper [Yello.Client/wwwroot/css/components.css:1491, Yello.Client/Components/TaskListRow.razor:22]
- [x] [Review][Patch] The Board/List toggle matches address segments case-sensitively, so it disappears on `/Spaces/.../List`, an address Blazor's router accepts [Yello.Client/Components/ViewToggle.razor:40]
- [x] [Review][Patch] The browser fake sorts by Label with `OrdinalIgnoreCase`, but the database orders by `Latin1_General_100_BIN2`, which is ordinal [tests/Yello.Tests.Client/FakeYelloApi.cs:1354]
- [x] [Review][Defer] A Status-sort cursor whose Status has left the effective set, or a reorder between pages, ends or skips the walk; and the count and the page diverge once the effective set differs per Project [Yello.Infrastructure/Persistence/ListReader.cs:110] — deferred to story 6.1/6.2 (unreachable today: AD-17 makes every Project's set the Space's)
- [x] [Review][Defer] The migration blocks `dbo.[Task]` while it runs: the policy change, the set-based backfill (with row locks, since lock escalation is off) and three offline index builds share one transaction [Yello.Infrastructure/Persistence/Migrations/20261028194820_ListViewSortKeys.cs:77] — deferred, owner Lee (deploy)
- [x] [Review][Defer] Client read paths do not catch `NotSupportedException` (a 200 with a body that is not JSON) [Yello.Client/Components/TaskListView.razor.cs:205] — deferred, pre-existing in every component, owner Lee
- [x] [Review][Defer] The keys queue stays faulted after an exception type its filters miss [Yello.Client/Components/ListKeys.cs:134] — deferred, pre-existing `BoardKeys` pattern, owner Lee
- [x] [Review][Defer] A day bound that falls in a DST gap at local midnight takes the offset of a time that does not exist [Yello.Client/Components/TaskListFilters.cs:136] — deferred, pre-existing in `TaskDates.TryFromInputValue` (story 2.3), owner Lee

## Dev Notes

### The shape of the story in one paragraph

The List View is a second read of the same Tasks: one new route, one new slice under `Application/Lists`, and one new page reached by a Board/List toggle.

Everything hard in it is AD-29 `:258`. The Board pages on a *unique* key (the position key within a column). The List View sorts on *non-unique, partly nullable* keys, so every keyset is the pair `(sortColumn, TaskId)`, compared in SQL, with NULL fixed at one end and a composite index behind each sort.

Three sorts are offered: Status, due date and Label. Status sorts in effective order, within each Status by `TaskId`. Due date puts undated Tasks last. Label sorts by the Task's first Label name, undated-style NULLs last, through a maintained `FirstLabelName` column, because a many-to-many value has no single key until one is chosen.

The page size is 50, server-owned. Rows are appended by "Show more Tasks" or by `↓` past the last row, never recycled. The true filtered total comes from its own `COUNT`, which drives the visible "Showing n of N", `aria-rowcount` and the one polite announcement on a filter change.

Every Role reads it, and nothing in it writes.

### Why paging and appending, not virtualising (AC7, AC18, stated as the story requires)

A virtualised table reuses a DOM row for whichever Task scrolls into it. Keyboard focus sits on an element, not on a Task. When the window shifts, the focused element silently comes to represent a different Task, and the next keyboard action operates on the wrong one. That defect is reachable only by keyboard and invisible to pointer testing (spine AD-29 `:255`).

Explicit pages of 50, appended and keyed by Task id, mean an element only ever represents one Task. A filter or sort change replaces the list wholesale: old elements are removed, not re-pointed. The existing gate `BoardPagingGateTests.Nothing_in_the_client_virtualises_a_list` scans every client `.razor`/`.cs` file, so it covers the new view for free.

### What exists today (read before writing)

**Server read (2.9).**

- `IBoardReader` (`Yello.Domain/Boards/IBoardReader.cs`) has `FindProjectAsync`, `CountTasksByStatusAsync`, `CountTasksAsync`, `PageAsync(projectId, statusId, after, take)` and `LabelIdsOfAsync`.
- `BoardPage` (`Yello.Application/Boards/BoardPage.cs`) has `Size => 50` and `ReadAsync`, which asks for `Size + 1` rows. The cursor is the last served key, and `null` exactly when none follows.
- `ViewBoardColumnHandler` runs: find the Project, compute the effective set, `Contains`, count, page, Labels. It returns `null` for a missing Project or Status, which the endpoint answers 404.
- `BoardReader` (`Yello.Infrastructure/Persistence/BoardReader.cs:65-81`) uses `AsNoTracking`, no `Where SpaceId`, `string.Compare(PositionKey, after) > 0`, then `OrderBy`/`Take`. It needs no tiebreaker because its index is unique. **The List View cannot borrow that shortcut.**
- `TaskLabelReads.LabelIdsOfAsync` reads one statement for the whole page, by Label name then id. Reuse it.

**Endpoint.** `BoardEndpoints.ReadColumnAsync` (`Yello.Host/Endpoints/BoardEndpoints.cs`) is the template:

1. Validate the cursor first: `400 invalid-board-cursor`.
2. Run the handler.
3. A `null` result answers `BoundaryProblems.NotAvailable()` and is not recorded.
4. Map with `TaskEndpoints.Response(task, labelIds)`.

It uses `.RequiresCapability(Capability.ReadProjectsAndTasks)`. Registrations are at `Program.cs:113-114` and maps at `:403-404`.

**Contracts.**

- `BoardRoutes` (route stated once, with builders that escape `after`).
- `BoardResponse`, `BoardProjectResponse(Id, Name)`, `BoardColumnPageResponse`.
- `TaskResponse(Id, Title, StatusId, PositionKey, CreatedAt, DueDate) { LabelIds }`.
- `ProblemTypes` (house-style remarks; `InvalidBoardCursor` is the model).

**Schema.**

- `Task` has a clustered PK on `Id` (a sequential GUID: `SequentialGuidValueGenerator` is ordered for SQL Server, so `Id` order is close to creation order) and an alternate key `(SpaceId, Id)`.
- Its one index is unique: `(SpaceId, ProjectId, StatusId, PositionKey)`, with `PositionKey varchar(128)` BIN2.
- `DueDate` is a nullable `datetimeoffset` with **no index**.
- `TaskLabel` has PK `(SpaceId, TaskId, LabelId)` and index `(SpaceId, LabelId, TaskId)`.
- `Label` has `Name nvarchar(50)`, unique `(SpaceId, Name)` under the default case-insensitive collation.
- `StatusDefinition` has `Position`. `EffectiveStatusSet.Of` orders by `Position`; there are no Project deltas until 6.1.
- RLS FILTER/BLOCK and an EF query filter apply on every Space-scoped table.

**Concurrency.**

- RCSI is on (migration `20261028120526_ReadCommittedSnapshot`). Lock escalation is off on `Task`/`TaskLabel`.
- Every key placer takes `ApplicationLocks.TakeProjectTasksAsync`, named `Yello/ProjectTasks/{spaceId}/{projectId}`.
- **The List View reads take no lock and never wait.** Their count, page and Labels are separate statements that may disagree by concurrent writes; this is 2.9 Q10 (a), and must be recorded the same way.
- The only new *write* is the `FirstLabelName` recompute inside the existing Labels edit (`TaskStore.ReplaceAsync`). It already reads the current set under `READCOMMITTEDLOCK` (`TaskStore.cs:253`).

**Client.**

- `Pages/Board.razor` is `SpaceArrival` around `BoardView`, keyed `(SpaceId, ProjectId)`.
- `BoardView` (partials `.Paging`, `.Keys`, `.Drag`, `.razor.cs`) is **at S1200's 30-dependency cap**. Do not add injections to it.
- `LiveAnnouncement` (two alternating polite regions; `Text` + `Sequence`) is reused here.
- `LabelChip`, `TaskCard`'s three chips then `+N` (`TaskCard.razor:222-229`) and `TaskDates.DayAndMonth` are reused. Mirror the row rule from them, and extract a shared helper if that is cleaner than duplicating it.
- `ActiveSpaceState.RegisterPurge`, `ShowProject`/`ClearProject` (the context bar's Project name).
- `ClientRoutes` (`Board(spaceId, projectId)`; ids only; `SpaceIn`).
- `ClientCopy.resx` is the only locale file.

**Tests.**

- `BoardPagingTests` (Slices, PG-01..PG-19), `BoardPageWire`, `BoardScaleTimingTests` (Nightly), `EnvelopeLoadTests` (Weekly).
- `BoardPagingGateTests` (Architecture).
- `BoardPagingTests` (Client; `FakeYelloApi.PageSize` opt-in, `HoldColumnPageAsync`, `window.__was`, `WaitForCardCountAsync`).
- `KeyboardTests`/`KeyboardHarness`.
- `IsolationCases.cs` (2.9's rows `:285-349` are the model).
- `TaskWire.SeedTasksAsync` (set-based INSERT, one Status).
- `RunningHost.ServerDurationOfAsync`, `Percentile.NearestRank`.

### Open questions (decide in Task 1; each is reversible; record as "Qn - (x)")

1. **Route, contract and cursor.**
   - (a) One route, `GET …/projects/{projectId}/list`, answering every page with the same `ListPageResponse(Project, Statuses, Total, Tasks, NextAfter)`.
     - The Statuses are the Project's effective set in order, so the filter and the Status column need no second read.
     - The cursor is an opaque, URL-safe string the server issues, encoding `(sort code, segment, sort value, TaskId)`. Base64url of a fixed, versioned text form is enough.
     - The server refuses `400 invalid-list-cursor` for anything it did not issue for *this* sort. A well-formed cursor whose Task has gone still seeks to the next pair.
   - (b) The same, with a readable cursor such as `due:2026-11-03T00:00:00Z:{id}`, like 2.9's raw position key.
   - (c) Separate first-page and next-page routes, as the Board has.
   - **Recommend (a).** A pair cursor has no single readable value, as 2.9's key had, and binding the sort into it makes a cross-sort cursor detectable. One route keeps the isolation rows to one family. Cost: the cursor is opaque on the API (FR-35 makes the *position key* readable, not the cursor). Ledger nothing.
2. **Page size and the paging control.**
   - (a) 50 rows, one named constant `ListPage.Size`, server-owned. "Show more Tasks" (and `↓` past the last row) appends the next 50. "Showing {loaded} of {total}" is always visible under the table; that is the "stated page size".
   - (b) Discrete pages with Previous/Next that *replace* the rows. That needs a backward seek or a cursor stack, and loses rows already read.
   - **Recommend (a).** It is the AD-29 rule verbatim ("appended to the DOM and never recycled"), it matches the Board's control, and an explicit button is the page boundary that the infinite-scroll ban makes coherent (AD-29 `:257`).
   - Cost: at 5,000 loaded rows the DOM is 5,000 rows. 2.9 measured the Board's cost at that depth and ledgered the append time (owner Lee). Measure the List View's append at 4,950 rows once and add it to that entry.
3. **What sorting by Label means** (`deferred-work.md:524`).
   - (a) By the Task's **first Label name**, in the chip order 2.4 Q12 already fixes (name, then id, in the database collation). Tasks with no Label go last.
     - The key is kept on the Task as `FirstLabelName`, maintained by the Labels edit.
     - It is indexed `(SpaceId, ProjectId, FirstLabelName, Id)`.
   - (b) By Label count.
   - (c) Grouped by Label, so a Task with several Labels appears once per Label. This breaks "each Task once" and the total.
   - (d) Not offered as a sort, filter only. This contradicts verbatim AC1 ("sortable by … Label").
   - **Recommend (a).** It is the one reading a person can predict from the row: the first chip is the sort key. The alternative, a computed `MIN(Name)` through a join, cannot be served by an index, and AC6 makes that a defect.
   - Costs:
     - (i) A denormalised column must be kept true on every Labels write. There is one such write today; the rename half is ledgered.
     - (ii) During a rolling deploy, the *previous* revision does not maintain the column, so a Labels edit served by it leaves that Task's key stale until its next Labels edit. Ledger it (owner: Lee, deploy). An idempotent re-backfill statement is the remedy if one is ever seen.
4. **What the filters are.**
   - (a) The three filters:
     - **Status:** any of the chosen effective Statuses.
     - **Label:** carries **any** of the chosen Labels.
     - **Due date:** one of *Any* (the default), *No due date*, or *a day range* `[from, to)` chosen in the viewer's zone and sent as instants with offsets (2.3 Q4's rule; the 2000-2100 window).
     - All three are combinable.
     - An id that is not this Project's effective Status or this Space's Label matches nothing, with the same bytes for random and foreign ids (AC15), and is never a 400. A Label deleted while chosen just stops matching.
   - (b) Label *all-of*.
   - (c) Unknown ids refused `400`.
   - **Recommend (a).** Any-of is what a checkbox group means. Matching nothing for a stale id keeps a filter usable after another Session deletes a Label.
   - Cost: "all-of" is ledgered to UX if wanted. A filter combined with a different sort scans that sort's index and tests the filter as a residual. At 5,000 rows that stays far inside 300 ms, and AC10 measures the heaviest combination to prove it.
5. **Sort direction and the NULL end.**
   - (a) Ascending only, with NULL (no due date, no Label) **last**. The cursor's segment flag carries the dated/undated boundary.
   - (b) Ascending and descending, with NULL last in both.
   - (c) NULL first, which is SQL Server's default; AC5 forbids leaving it to the default anyway.
   - **Recommend (a).** "Soonest due first, undated after" is the question the view exists to answer. Descending doubles P-3's matrix for a need nobody has stated. P-3's "5 sorts" are met by the five cases in Task 8.
   - Cost: descending is ledgered to UX/PM, and is cheap to add later because each index serves a backward seek.
6. **The view toggle.**
   - (a) A separate client route `/spaces/{s}/projects/{p}/list` and a two-link `nav` ("Board", "List", with `aria-current="page"`) on both views.
   - (b) A tab-like toggle that swaps the view within the Board route.
   - **Recommend (a).** The address names the view, Back works, `SpaceArrival` and the purge hook cover it unchanged, and `BoardView` (at its dependency cap) grows by one parameterless child at most.
   - Cost: filter state does not survive Board → List → Board. That is intended (AC17), and noted for UX.
7. **What sorting by Status means.**
   - (a) **Effective order** (the Board's column order), then `TaskId`. Implement it as one seek per effective Status in order on `(SpaceId, ProjectId, StatusId, Id)`. The cursor holds `(StatusId, TaskId)`, and the server resolves the Status's ordinal from the effective set it already computed.
   - (b) Effective order, then **position key** (the Board's in-column order), on the existing unique index.
   - (c) Status name order.
   - **Recommend (a).** Verbatim AC4 makes `TaskId` the mandatory tiebreaker. Within a Status, `TaskId` order is close to creation order (sequential GUIDs), which a person can read. AD-16 forbids storing effective order, so the ordinal must be applied in the handler, as the Board's loop already does.
   - Cost: in-Status row order differs from the Board's card order. Ledger it to UX: if (b) is wanted, it is a tiebreaker change on an index that already exists, and the AC4 reading would need an amendment.
8. **Keyboard row traversal.**
   - (a) A native `<table>`. Its rows are one roving tab stop (the active row's title cell, `tabindex 0`; every other row `-1`).
     - `↑`/`↓` and `Home`/`End` move between rows.
     - `↓` past the last loaded row loads the next page.
     - `aria-rowcount`/`aria-rowindex` give true ordinals.
     - No `role="grid"`, no `role="application"`.
   - (b) A `ul`/`li` list with `aria-posinset`/`aria-setsize`, as the Board uses.
   - (c) `role="grid"` with cell navigation.
   - **Recommend (a).** A list of Tasks with several attributes is tabular, the table keeps browse-mode table commands for screen-reader users, and the row attributes carry the true ordinal (X-6's intent).
   - Cost: a second roving composite. Share `BoardTraversal`'s vertical half if it fits, or else write `ListTraversal`, and record which.
9. **Opening a Task from a row.**
   - (a) **Not in 2.10.** The row's title is text, and the focusable element is the row's title cell. The List View is read-only for every Role.
   - (b) The row's title opens the existing `TaskDetail` panel over the List View with full editing. That needs the Board's optimistic replica (`BoardView.razor.cs`) extracted and shared, because 2.3's AC1 makes every edit optimistic.
   - (c) The title links to the Board with the Task's panel opened. That needs a new Board query parameter and paging the column to the Task, and focus returns to a card rather than the row.
   - **Recommend (a)** for this story. AC1 to AC9 ask for rows, filtering and sorting only. `EXPERIENCE.md:52` lists the List View as an entry to Task detail, but (b) is a substantial extraction from a component at its cap. Ledger "open Task detail from a List View row" (owner **UX/PM**, decided before 6.6, which builds bulk move on a filtered List View). If the extraction turns out small, (b) is acceptable; record it.
   - Cost: a person must switch to the Board to edit what they found.
10. **Rows at 320px (AC20).**
    - (a) At narrow widths the table reflows each row into a stacked block: title, then a metadata line holding Status, due date and chips, wrapping. Use CSS on the table elements (`display: block` below a breakpoint, logical properties), keeping the table semantics in the DOM.
    - (b) A horizontally scrolling table. That is a 1.4.10 failure.
    - **Recommend (a).** Cost: a CSS block covered by a 320px browser case. The existing design gates (`DesignFoundationGateTests`) must pass.
11. **An unfiltered empty Project on the List View.**
    - (a) "Nothing here yet." (the Board's empty copy, UX-DR35) with **no** action, because creation lives on the Board.
    - (b) "No Tasks match.", which reads as if a filter excluded something.
    - **Recommend (a).** Ledger to UX: the copy needs no change unless they want the Board's "Add a Task" action here, which would make the List View a writer.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

| Entry | Disposition | Owner |
|---|---|---|
| `:524` Sorting a List View by Label | **Close** with Q3's decision and the P-3 Label cases as evidence | 2.10 |
| A Label rename must recompute `FirstLabelName` for every Task carrying that Label | **Add** | whichever story adds Label rename (2.4 Q10's owner) |
| The previous revision does not maintain `FirstLabelName` during a rolling deploy | **Add** (Q3 cost ii) | Lee (deploy) |
| Descending sort, and Label "all-of" | **Add** (Q4, Q5) | UX/PM |
| In-Status row order follows `TaskId`, not the Board's position | **Add** (Q7) | UX |
| Opening Task detail from a List View row | **Add** (Q9), before 6.6 | UX/PM |
| Filter state does not survive Board → List → Board | **Add** (Q6) | UX |
| The List View's append cost at about 5,000 loaded rows | **Touch** 2.9's append-cost entry with the List figure | Lee |
| FR-30's Assignee dimension, its composite index, and I-15 | Already staged in `sprint-status.yaml` and `epics.md:400`; **no new entry**, and 4.6 owns it | 4.6 |

### Architecture compliance (must)

- **AR-3 vertical slice.** `Application/Lists/ViewList/` contains no authorisation, Space resolution, refusal recording or bounds; the pipeline does those. A slice that re-implements any of them is a defect, and `SpaceResolutionSeamTests`/`RefusalBoundaryGateTests` will catch it.
- **AD-2.** No `Where` on `SpaceId`. No `IgnoreQueryFilters`. Raw SQL is allowed only in `Infrastructure`, and only through `dbContext.Tasks.FromSql(...)`, which keeps the filter.
- **AD-3/AD-20.** An in-Space not-found returns `BoundaryProblems.NotAvailable()` and is not recorded. A refusal body carries no title, name or id.
- **AD-19.** New contract types. `TaskResponse` is reused unchanged. No existing member is removed or retyped.
- **AD-29.** Use a pair keyset compared in SQL. Fix NULL at one end explicitly. Put a composite index behind each sort. Never use `OFFSET`. Append and never recycle. Take the true total from its own `COUNT`.
- **AD-16.** Never store effective Status order; apply it in the handler.
- **Logs (AR-34).** No Task title, Label name or Project name in any log line. Filter ids in the query string are ids, and that is acceptable.
- **No browser storage, no names in URLs or titles** (UX-DR31, UX-DR33).

### Library / framework

- No new packages. Use EF Core 10.0.11, ASP.NET Core minimal APIs, Blazor WebAssembly 10.0.11, xunit.v3 4.0.0, Testcontainers 4.6.0 and Playwright 1.62.0, all as pinned in `Directory.Packages.props`.
- EF Core translates `string.Compare(a, b) > 0` (2.9 relies on it). **Guid comparison translation must be verified, not assumed** (Task 3 (d)).
- `System.Data.SqlTypes.SqlGuid` (from `Microsoft.Data.SqlClient`/BCL) gives SQL Server's `uniqueidentifier` order in tests.
- `Microsoft.AspNetCore.Components.Web.Virtualization` must **not** be used (gated).

### Testing standards (from `tests/TESTING-CONVENTIONS.md` and CLAUDE.md)

**Traits and naming.**

- Exactly five traits: `Suite`, `Priority`, `Requirement` (the AR id **and** the AD id), `Assumption` and `Tier`.
- Test names are underscore sentences, one behaviour each. Write no `Task.Delay`; poll with a deadline.

**Data, fixtures and the memory budget.**

- Use randomised data and never a shared email. Clean up by disposal or rollback.
- Use one SQL Server per test project and a database per class (`MigratedDatabaseFixture`). Add **no new engine and no new fixture type**.
- WSL is capped at 8 GB. Run the full suite only with `--max-parallel-test-modules 1`, and never run two `dotnet test` processes at once.

**Races and absence assertions.**

- Races must be deterministic: use the application-lock helpers, and wait on `LCK_M_%`/`LCK_M_X APPLICATION` waits that lead to the holder's session (2.9's `WaitUntilBlockedByTheHolderOrAnsweredAsync`).
- Every absence assertion (the gates, "nothing announced", "no foreign row") is proved against a **planted** signal. Record each plant and its catch rate, for example 3/3.
- Restore a plant in a `finally` from saved content. **Never** restore with `git checkout --`, `git restore` or `git reset`.

**Browser cases.**

- Use Playwright with Chromium, against a **Debug** build of `Yello.Client`.
- Never use Playwright's URL wait; poll `location.pathname`.
- Wait for the key module before the first key.
- Record announcements with the `MutationObserver` helper.
- Run across `BrowserLocales`.

**Tiers and the run.**

- Tiered tests are `[Fact(Explicit = true)]` with `Tier=Nightly`/`Weekly` and run with `--explicit on`. The PR stage excludes them.
- `--no-build` runs stale binaries, so rebuild after every change.
- Expect 3 migration-bundle skips (`YELLO_MIGRATION_BUNDLE` unset) plus the Explicit tiers.
- Rerun once before calling a SQL engine-start or low-memory failure real.
- Check Docker with `docker info --format '{{.ServerVersion}}'` before suspecting code on SqlException 1225. The runtime is Rancher Desktop; see CLAUDE.md for recovery.

### Previous story intelligence (2.9, 2.8, 2.4, 2.3)

**2.9.**

- AC17's dominant finding was that range locks reach the adjacent Space. The fix was application locks named `{spaceId}/{projectId}`.
  - The List View adds no lock.
  - If any new write path appears (only the `FirstLabelName` recompute does, inside an existing transaction), it must never take a key-range lock that can cross a Space.
- The review's top finding was a lock name without the Space: a cross-tenant wait. Any name you add must include the Space.
- RCSI turned some write-path reads racy. 2.9 used `READCOMMITTEDLOCK` on reads that *decide a write*. The `FirstLabelName` recompute must read the final set inside the same transaction as the replace, not from a snapshot taken before it.
- A cold first execution read 128 pages during compilation. Warm up before measuring logical reads, and use tolerance 5.
- A filtered test run that selects zero tests exits with code 8, a trap noted in 2.9.

**2.8.**

- `LiveAnnouncement` exists because one region saying the same sentence twice is heard once. A filter change that yields the same count twice must still be heard: use `Sequence`.
- The arrow grammar is not the conformance path. Native controls are.

**2.4.**

- Q12 fixed the chip order: name, then id, in the database collation. `FirstLabelName` must use the same order and collation, or the "first chip = sort key" promise breaks.
- The Label-in-use refusal keeps deletion from staling the key.

**2.3.**

- Due dates are instants at offset zero. A day chosen in the viewer's zone is sent as the start of that day with its offset. The adjacent-day effect across zones is already ledgered to UX.
- The List View's day-range filter inherits that semantics. Do not invent another.

### Git intelligence

- `e541bb1` closed the 2.9 review (app-lock names carry the Space; `READCOMMITTEDLOCK` on idempotency lookup).
- `41989bd` (2.9) added `BoardPage`, `ViewBoardColumnHandler`, `BoardView.Paging.cs`, `ApplicationLocks.cs`, two migrations, Nightly/Weekly workflows, `BoardPagingGateTests`, and the Client/Slices paging tests. These are the patterns to copy.
- 2.9's 2026-10-28 run: 1,360 total, 1,352 passed, 0 failed, 8 skipped (3 bundle, 5 Explicit). Treat any new failure outside the List View as a regression.
- Commit style: one commit per phase, made by the orchestrator. This phase does not commit.

### Project Structure Notes

**New:**

- `Yello.Domain/Lists/IListReader.cs`, plus `ListFilter`, `ListSort` and `ListCursor` value types (Domain or Application; keep the cursor encoding in Application).
- `Yello.Application/Lists/ListPage.cs` and `Yello.Application/Lists/ViewList/ViewListHandler.cs`.
- `Yello.Infrastructure/Persistence/ListReader.cs`.
- Migration `…_ListViewSortKeys(.Designer).cs`.
- `Yello.Contracts/Lists/{ListRoutes,ListPageResponse,ListSorts}.cs`.
- `Yello.Host/Endpoints/ListEndpoints.cs`.
- `Yello.Client/Pages/TaskListPage.razor`, `Yello.Client/Components/{TaskListView.razor(+partials),ViewToggle.razor,ListTraversal.cs}`, and possibly `wwwroot/js/list-keys.js`.
- Tests: `tests/Yello.Tests.Slices/Lists/{ListPagingTests,ListWire,ListScaleTimingTests}.cs`, `tests/Yello.Tests.Architecture/{ListViewGateTests,ListTraversalTests}.cs`, `tests/Yello.Tests.Client/ListViewTests.cs`.

**Modified:**

- `TaskItem.cs`, `TaskConfiguration.cs`, `TaskLabelConfiguration.cs` (comment only), `TaskStore.cs` (`ReplaceAsync`)
- `ProblemTypes.cs`, `Program.cs`
- `ClientRoutes.cs`, `BoardView.razor` (toggle only)
- `ClientCopy.resx`, `RefusalCopy.cs`, `components.css`
- `FakeYelloApi.cs`, `IsolationCases.cs`
- The route-list and contract gates
- The schema snapshot and `MigrationBundleEquivalenceTests`
- `deferred-work.md`, the story file, `sprint-status.yaml`

**Naming conflict:** `Yello.Client/Components/ProjectList.razor` already exists and is Space home's list of Projects. Do not name anything here `ProjectList`.

**Capability map** (spine `:415`): `Application/Lists/*` is the named home. This is the first use of that folder.

### References

- `_bmad-output/planning-artifacts/epics.md`: `:1338-1384` (story), `:210` (AR-22), `:257` (UX-DR19), `:363` (trace), `:400` (staging), `:437` (epic notes)
- `prds/prd-YelloBMAD-2026-08-15/prd.md`: `:71`, `:512`, `:524`, `:536-543` (FR-30), `:718`, `:745`, `:753-757`, `:805`, `:834`. `addendum.md:153`.
- `architecture/…/ARCHITECTURE-SPINE.md`: AD-29 `:249-259` (especially `:258`), AD-2 `:81-84`, AD-3 `:92`, AD-16 `:174`, conventions `:267-277`, capability map `:415`
- `ux-designs/…/EXPERIENCE.md`: `:51-52`, `:71`, `:85`, `:91`, `:164-165`, `:177-179`, `:211`, `:217`, `:255`, `:275`, `:279`, `:328-337`, `:383-390`, `:404`, `:437-438`. `DESIGN.md:451`, `:457-470`.
- `test-artifacts/test-design-qa.md`: P-3 `:312`, P-4 `:358`, I-3 `:267`, X-5 `:311`, X-6 `:356`, appendix A `:543-590`. `test-design/YelloBMAD-handoff.md:99`, `:113-130`.
- `implementation-readiness-report-2026-08-22.md`: `:509`, `:548`, `:857-859`, `:1234`, `:1321`
- `specs/spec-yello/SPEC.md:144`, `:153-154`
- `tests/TESTING-CONVENTIONS.md`: traits `:44-73`, rules `:87-199`, browser `:201-340`, migrations `:446-466`, containers `:468-536`, isolation `:631-699`, tiers `:731-759`
- `_bmad-output/implementation-artifacts/2-9-the-board-at-five-thousand-tasks.md` (Q1-Q19, review `:227-248`), `2-4-define-and-apply-labels.md` (Q12 `:487`, TaskLabel `:134`), `2-3-edit-a-tasks-title-status-and-due-date.md` (Q4 `:536`, `:127`), `2-8-operate-the-whole-board-from-the-keyboard.md` (Q1, Q6, Q14)
- `deferred-work.md:524`
- `sprint-change-proposal-2026-09-13.md`: no List View content (checked)

### ATDD artifacts - red-phase scaffolds already in the tree

**Checklist:** `_bmad-output/test-artifacts/atdd-checklist-2-10-view-a-project-as-a-filterable-sortable-list.md` (scenario map, seed constraints checked, assumed markup, gates that go red by design).

**Scaffold files.** Every case is skipped `red: AC<n> - <why>` unless marked green.

- `tests/Yello.Tests.Slices/Lists/ListWire.cs`: the active harness. It holds the path builder, page reader and walk; seeds with ties and NULLs; the expected order of every sort computed **in SQL**; the `FirstLabelName` reads; and logical reads and plans per request. Its scaffold constants are `ListRoute`, `PageSize`, `SortStatus`/`SortDueDate`/`SortLabel` (`status`/`due-date`/`label`), `InvalidListQuery` and `InvalidListCursor`. Swap each for the contract member on activation.
- `tests/Yello.Tests.Slices/Lists/ListPagingTests.cs`: 19 methods, 47 cases (LV-01..LV-19).
  - P-3, the five sorts, ties and NULL crossings: AC4, AC5, AC13, AC14.
  - Filters and the true total: AC11, AC15.
  - A deleted Label stops matching: AC15.
  - Stale cursor x4: AC12.
  - Bad cursor x3, bad query x8, check order, page size x3: AC12, AC2.
  - Viewer on both surfaces: AC16.
  - Contract shape: AC22.
  - Unknown Project 404: AC3.
  - `FirstLabelName` maintenance x4 and its race: AC13.
  - Logical reads first vs last x6: AC10.
  - Plan seeks its index with no implicit conversion x3: AC6.
- `tests/Yello.Tests.Slices/Lists/ListScaleTimingTests.cs`: 1 case, the timed half of AC10. `Tier=Nightly`, `Explicit`, in `BoardScaleCollection`.
- `tests/Yello.Tests.Isolation/IsolationCases.cs`: 4 rows x 2 surfaces, **pending on 2.10**. They must be live before `review` (GATE-003). AC3, AC15, AC21.
  - `ISO-DIRECT-LIST`
  - `ISO-LIST-FOREIGN-ID-IN-OWN-SPACE`
  - `ISO-LIST-AGGREGATE`
  - `ISO-LIST-FOREIGN-FILTER-IN-OWN-SPACE`
- `tests/Yello.Tests.Architecture/ListViewGateTests.cs`: 8 cases. AC2, AC6, AC12, AC18, AC22.
  - P-4: every sort has `(SpaceId, ProjectId, column, Id)`.
  - P-4's detector plant.
  - Three sorts, none Assignee.
  - The route is stated once.
  - The contract members.
  - `ListPage.Size` is 50.
  - The two problem types.
  - The virtualisation scan covers `TaskListView.razor`.
- `tests/Yello.Tests.Client/ListViewTests.cs`: 14 methods, 18 cases. AC1, AC7-AC9, AC11, AC12, AC16-AC20.
  - The toggle.
  - Row content.
  - The count announced once.
  - "No Tasks match." alone.
  - Show more appends.
  - `↓` by identity.
  - X-6 `aria-rowcount`.
  - Viewer.
  - Purge and Web Storage.
  - No name in the URL or title.
  - 404.
  - 320px x2.
  - Locales x4.
  - The sort change replaces rows.
- `tests/Yello.Tests.Client/FakeYelloApi.cs`: the active harness. It answers the List route, sorting, filtering and paging as the Host will, with an opaque cursor bound to its sort and a page hold. No earlier case requests the route, so their bytes are unchanged.

**Green today:** one case, `ListViewGateTests.The_sort_index_detector_reports_a_planted_sort_with_no_index_and_finds_an_existing_one`. It is a standing gate and the P-4 detector's permanent plant. Every other scaffold needs the route, the column or the page.

**Not scaffolded:** the handler and `ListTraversal` unit tests. Their types do not exist, so the dev phase writes them beside `ViewBoardHandlerTests` and `BoardTraversalTests`.

**Activation order, riskiest assumption first:**

1. LV-02, `Sorted_by_due_date_a_page_boundary_inside_120_tied_dates_skips_and_repeats_nothing`, with LV-19's due-date row.
   - It tests whether EF expresses the `(DueDate, Id)` pair seek with the `uniqueidentifier` tiebreaker compared in SQL, as a bounded index seek, and whether the two-segment NULL-last cursor composes.
   - Read the generated SQL and record it. The fallback is `FromSql`.
2. The ARCH P-4 gate with Task 2's schema. Then LV-16 and LV-17.
3. LV-03, LV-01, LV-04 and LV-05. Then LV-18 with its `OFFSET` and dropped-index plants, and the rest of LV-19.
4. LV-06..LV-15, then the remaining ARCH gates.
5. The isolation rows, one at a time, each against its plant.
6. The browser cases, `↓` by identity first.
7. `ListScaleTimingTests`, once, with `--explicit on`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), bmad-dev-story, unattended story cycle.

### Debug Log References

**Activation order followed** (Dev Notes, ATDD artifacts). Each scaffold was activated, seen to fail for its stated reason or proved against a plant, then made green. Plants were restored in a `finally` from saved content (a scratch `plant.py`), never by git.

1. **LV-02 first (riskiest assumption).** EF Core 10 translates `task.Id.CompareTo(id) > 0` to `[t].[Id] > @id`, so the tiebreaker is compared by SQL Server, never in .NET. SQL read from `ToQueryString` (a temporary probe, deleted):
   `WHERE [t].[ProjectId] = @projectId AND [t].[DueDate] IS NOT NULL AND ([t].[DueDate] > @value OR ([t].[DueDate] = @value AND [t].[Id] > @id)) ORDER BY [t].[DueDate], [t].[Id]` with `@value datetimeoffset`, and the Label form with `@name nvarchar(50)`. No `FromSql` fallback was needed for the comparison. Plant (the tied-value half of the pair dropped) caught LV-02 1/1.
2. **P-4 + Task 2 schema.** Plant (the Label index's columns reordered) caught P-4 1/1. LV-16/LV-17 activated with the `FirstLabelName` maintenance; plant (recompute before the replace is saved) caught 5/5.
3. **LV-01, LV-03..LV-05, LV-18, LV-19.** LV-18 first failed for the Label sort under the heaviest filter (last 963 > first 534 reads): the last page also read the unlabelled segment, which a Label filter can never match. Fix: a segment the filter empties is not read (due range -> no undated segment; "No due date" -> no dated segment; Label filter -> no unlabelled segment). Then, under the Status sort, the redundant `StatusId IN (...)` beside the per-Status equality was dropped. Then each page statement names its sort's index (`FromSqlRaw` composed onto `dbContext.Tasks` with an `INDEX` hint, so the query filter and RLS still apply): measured filtered reads fell from 555-680 to 290-580. Plants: `OFFSET`/`Skip()` in the Status cursor seek caught LV-18 2/2; the due-date index dropped from the migration caught LV-18 2/2 and LV-19 1/1 (the hint fails as a 500).
   - **The Status sort's first page under the heaviest filter reads ~13,000 pages**: per-statement deltas (a temporary probe inside LV-18) showed the optimiser driving from `TaskLabel`'s `(SpaceId, LabelId, TaskId)` into the Status index and sorting. Every seek stays inside the Space; the Nightly p95 is 15.3 ms. Ledgered (owner Lee).
   - **INCLUDE measured with and without** (Task 2 (b)): without the INCLUDE columns the filtered first pages read 959 / 790 / 17,177 against 427 / 578 / 13,072 with them; unfiltered pages were unchanged. Kept.
4. **LV-06..LV-15** green on activation, plus `ViewListHandlerTests` (6) and `ListCursorTextTests` (2), written new (not scaffolded). **LV-20 added**: the migration taken down to `20261028123715_TaskLockEscalationDisabled` and up again over existing data - the column and three indexes go and come back, the backfill restores the first chip's name, the policy is on afterwards. Plant (the backfill left under the security policy) caught 1/1. The migration bundle was rebuilt into the scratchpad and the three `YELLO_MIGRATION_BUNDLE` cases run once against it: green (26/26 Deployment).
5. **Isolation rows (GATE-002/003): all four live on both surfaces (8 cases).** Activating them first broke 10 unrelated rows: the shared world's Spaces hit the projects-per-Space bound (50), and `NoLabelAppliedInEitherSpaceAsync` asserted no Label application anywhere in A or B. Fixes: the List rows reuse an existing Project/Task/Label on the side that needs none of its own (`AnyProjectWithTaskAsync`, `AnyLabelAsync`), and that check is scoped to the case's own Task and the foreign Label. Plants: the handler's Project check skipped - ISO-LIST-FOREIGN-ID caught 2/2; the reader's Project predicate dropped - ISO-LIST-AGGREGATE caught 1/2 at first (order-dependent), so the row was strengthened (an unfiltered walk beside the Label-filtered one, and another of A's Projects with a Task ensured) - then 2/2; Project predicate plus both Task layers dropped (`IgnoreQueryFilters` and the Task FILTER predicate) - 2/2; the handler's filter narrowing skipped - ISO-LIST-FOREIGN-FILTER **0/2, masked** (the `TaskLabel` layers and the Project predicate each already hide a foreign id's rows), caught instead 2/2 by `ViewListHandlerTests`.
6. **Browser cases (18) and the virtualisation gate.** First run 15/18; three scaffolds corrected (below). Plants: an announcement on paging caught 2/2; focus left on the old last row (by index) caught 1/1; `<Virtualize` planted in `TaskListView.razor` caught by both gates (2/2); the rows' `@key`s removed **0/1, masked** - a re-read removes the table while it reads, and a page only ever appends at the end, so no element is re-pointed either way; the keys stay as the stated guarantee.
7. **Nightly, once, `--explicit on`** (`ListScaleTimingTests`, local engine, MAXDOP 1, warm, server-side, 100 samples after 5 warm-up rounds): status 12.5 / 12.2 ms p95 (first / last page), status heaviest filter 15.3 / 14.5, due-date 12.2 / 12.0, due-date heaviest 15.9 / 15.5, label 12.2 / 12.1, label heaviest 16.2 / 16.1. All far inside 300 ms.
8. **Append cost at depth (Q2 cost)**: an ad-hoc probe appended 50 to 5,000 rows: 140 ms for the first append, 1.4 s from 4,950 to 5,000 (last ten mean 1.4 s, max 2.2 s). Added to 2.9's append-cost entry (owner Lee).

**Scaffolds corrected (and why):**
- `ListWire`: `PageSize` now reads `ListPage.Size` and the two problem types read `ProblemTypes`; the three sort codes stay literal constants, because `[InlineData]` needs compile-time constants and `ListSorts`' codes are properties (S2339). `LogicalReadsOfAsync` also leaves out `StatMan` statistics updates (engine housekeeping that could land on one side of a comparison) - a hardening, not the cause of any failure.
- `ListViewGateTests`: unchanged in shape; the map it reads is `ListSortColumns.ByCode`, a public static readonly field (an auto-property's backing field would be a second static `IEnumerable` and break `Single()`).
- `IsolationCases`: `ListRoute` is `ListRoutes.List`; the walked sorts come from `ListSorts`; `ListSortStatus` stays a literal because it is an optional parameter's default. The reuse helpers, the scoped check and the strengthened aggregate row are described in step 5.
- `ListViewTests`: the row case expected "Todo" for `tasks[0]`, but `SeedList` puts ordinal 1 in the second column ("Doing") - it now reads the seeded column's name; the empty-filter case located "No Tasks match." page-wide and met the polite region saying the same sentence - it now reads the paragraph in `main`; the no-name case checked "Doing", which the labelled `tasks[0]` is in, so the filter was never empty - it now checks "Todo"; the 404 case gained an assertion (S2699 refused a case with none).
- `FakeYelloApi`: its List route was used as scaffolded; its sort codes and problem-type strings stay literals (they are `case`/pattern constants) and equal the contract's values.

### Completion Notes List

- Story context created by bmad-create-story (2026-10-28): the context engine analysis is complete and a developer guide has been written. The open questions above are to be decided in Task 1 as reversible, recorded decisions.
- **Decisions (reversible; each line can be cut):**
  - Q1 - (a): one route, `GET …/projects/{projectId}/list`, answering `ListPageResponse(Project, Statuses, Total, Tasks, NextAfter)`; the cursor is base64url of a versioned text form naming the sort, the segment, the value and the Task id - a cross-sort cursor is detectable.
  - Q2 - (a): 50 rows, `ListPage.Size`, server-owned; "Show more Tasks" and `↓` past the last row append; "Showing {loaded} of {total}" states it.
  - Q3 - (a): sort by the first Label's name in chip order, kept as `FirstLabelName` on the Task, maintained by the Labels edit, indexed; Tasks with none last.
  - Q4 - (a): any-of Statuses, any-of Labels, Any / No due date / a day range; foreign or stale ids narrowed away, never refused.
  - Q5 - (a): ascending only, NULL last, the cursor's segment flag carrying the boundary.
  - Q6 - (a): a separate client route `/spaces/{s}/projects/{p}/list` and a two-link nav; the toggle is parameterless and reads both ids from the address, so `BoardView` gains no dependency.
  - Q7 - (a): effective Status order, then `TaskId`, one seek per Status in the handler's order.
  - Q8 - (a): a native table, the rows one roving tab stop; `ListTraversal` is its own class, not `BoardTraversal`'s vertical half (one axis, no sticky origin, pages from the arrow); `list-keys.js` is its own module rather than a mode bent into `board-keys.js` (which carries a two-axis grammar, a carry and the pickers).
  - Q9 - (a): not in 2.10; the List View is read-only; ledgered (UX/PM, before 6.6).
  - Q10 - (a): below 30em each row reflows into a stacked block; the header row is clipped for assistive technology, never removed.
  - Q11 - (a): an unfiltered empty Project shows "Nothing here yet." with no action.
  - Each page statement names its sort's composite index with an `INDEX` hint (composed `FromSqlRaw`, both isolation layers kept): AC6 makes the index the sort's contract, and it measured fewer reads under every filter.
  - The `INCLUDE` columns are kept: measured, they cut the filtered pages' reads (Debug Log step 3).
  - The migration's backfill switches the Space policy off and on again inside its own transaction: a migration's connection names no Space, and the one line that sets a session key must stay `SpaceSessionContext`'s (`SpaceResolutionSeamTests`). Stated in the migration's remarks.
  - Sort codes live in the Domain (`ListSortCodes`) and in Contracts (`ListSorts`), because Contracts references nothing; `ListViewGateTests` holds the two equal through `ListSortColumns`.
  - A Status-sort cursor naming a Status outside the admitted order answers an empty page with no cursor; "No due date" with a range, a repeated scalar, or more than 200 ids in one filter answer `400 invalid-list-query`.
  - An appended page skips a Task already drawn (a concurrent edit can move one past the cursor), so a row is never drawn twice and `@key` stays unique.
  - Sort changes announce the count as filter changes do (Task 5 (c)).
  - No spine change: AD-29 already states the pair keyset, the NULL end and the index rule.
  - **Code review D1 - (a), decided by Lee (2026-10-28):** the Label-sort cursor stays an opaque API parameter. Its base64url text carries the first Label's name, and it is not encrypted. "No names in URLs" (UX-DR33) governs client routes, not the List API's `after=` value. The Host logs no request lines. Query-string logging at ingress or APM is ledgered to Lee in `deferred-work.md`.
- **Implemented:** `FirstLabelName` + migration `ListViewSortKeys` (backfill, three indexes, a correct `Down`); the Labels edit keeps the key true in its transaction; `IListReader`/`ListReader` (pair keyset, NULL last, per-Status seeks, filters, count); `ListPage`, `ListCursorText`, `ViewListHandler`; `ListEndpoints` + `ListQueryReading` (query, then cursor, then 404); Contracts `ListRoutes`, `ListSorts`, `ListQuery`, `ListPageResponse`, `ListStatusResponse`; two problem types and their client copy; the client page, view toggle, list view, row and choice components, `ListTraversal`, `ListKeys` + `list-keys.js`, copy and CSS.
- **Tests added or activated:** ListPagingTests 48 (47 scaffolded + LV-20), ViewListHandlerTests 6, ListCursorTextTests 2, ListViewGateTests 8, ListTraversalTests 11, ListViewTests 18, 8 isolation cases, the Nightly timing case; MigrationBundleEquivalenceTests now compares each index's key and included columns.
- **FR-30 is not delivered** - the Assignee dimension is 4.6's (ledgered).
- **Verification:** dotnet build Yello.slnx: 0 warnings, 0 errors. dotnet test Yello.slnx --no-build --max-parallel-test-modules 1: 1,466 total, 1,457 passed, 0 failed, 9 skipped (3 migration-bundle cases with YELLO_MIGRATION_BUNDLE unset, 6 Explicit Nightly/Weekly tiers, the new ListScaleTimingTests among them). Separately: the 3 bundle cases green against a freshly built bundle, and the Nightly List timing case green once with --explicit on.

### File List

New:
- Yello.Domain/Lists/IListReader.cs, ListCursor.cs, ListFilter.cs, ListSort.cs, ListSortCodes.cs
- Yello.Application/Lists/ListPage.cs, ListCursorText.cs, ViewList/ViewListHandler.cs
- Yello.Infrastructure/Persistence/ListReader.cs, ListSortColumns.cs
- Yello.Infrastructure/Persistence/Migrations/20261028194820_ListViewSortKeys.cs, 20261028194820_ListViewSortKeys.Designer.cs
- Yello.Contracts/Lists/ListRoutes.cs, ListSorts.cs, ListQuery.cs, ListPageResponse.cs
- Yello.Host/Endpoints/ListEndpoints.cs, ListQueryReading.cs
- Yello.Client/Pages/TaskListPage.razor
- Yello.Client/Components/TaskListView.razor, TaskListView.razor.cs, TaskListRow.razor, TaskListChoice.razor, TaskListFilters.cs, ViewToggle.razor, ListTraversal.cs, ListKeys.cs
- Yello.Client/wwwroot/js/list-keys.js
- tests/Yello.Tests.Slices/Lists/ViewList/ViewListHandlerTests.cs, ListCursorTextTests.cs
- tests/Yello.Tests.Architecture/ListTraversalTests.cs

Modified:
- Yello.Domain/Tasks/TaskItem.cs
- Yello.Infrastructure/Persistence/Configurations/TaskConfiguration.cs, TaskLabelConfiguration.cs (comment)
- Yello.Infrastructure/Persistence/TaskStore.cs
- Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs
- Yello.Infrastructure/SpaceContentStores.cs
- Yello.Contracts/ProblemTypes.cs
- Yello.Host/Program.cs
- Yello.Client/Components/BoardView.razor (the toggle only)
- Yello.Client/Pages/ClientRoutes.cs
- Yello.Client/Refusals/RefusalCopy.cs
- Yello.Client/Localisation/ClientCopy.resx
- Yello.Client/wwwroot/css/components.css
- tests/Yello.Tests.Slices/Lists/ListPagingTests.cs, ListWire.cs, ListScaleTimingTests.cs (activated, corrected, LV-20)
- tests/Yello.Tests.Slices/Deployment/MigrationBundleEquivalenceTests.cs
- tests/Yello.Tests.Architecture/ListViewGateTests.cs (activated), RefusalBoundaryGateTests.Delete.cs (the List route)
- tests/Yello.Tests.Client/ListViewTests.cs (activated, corrected)
- tests/Yello.Tests.Isolation/IsolationCases.cs (rows live, reuse helpers, scoped check)
- _bmad-output/implementation-artifacts/deferred-work.md, sprint-status.yaml, this story file

### Change Log

- 2026-10-28: Story created (ready-for-dev).
- 2026-10-28: Implemented (bmad-dev-story): the List View - FirstLabelName and three sort indexes, the pair-keyset List route and slice, the client page with filters, sort, paging and keyboard traversal; the four isolation rows live on both surfaces; the ledger updated. Status -> review.
