---
baseline_commit: 4ccba9f
---

# Story 2.9: The Board at five thousand Tasks

Status: done

Epic: 2 — Track your own work on a Board
Story key: `2-9-the-board-at-five-thousand-tasks`
Requirements owned (Story Coverage Index, `epics.md:362`): **FR-28** (Board at the NFR-8 bound holds NFR-5 and NFR-9; every Task reachable and in exactly one column — `prd.md:516-524`), **AR-22** (keyset seek on the position key, never `OFFSET`; append, never recycle; separate `COUNT`; initial page size 50 per column, an assumption — `epics.md:210`, spine AD-29 `:249-259`), **AR-40b** (cold start against NFR-5: decide and *state* it — `epics.md:228`, `:437`, spine deferral `:425`), **NFR-5** (reads 300 ms, writes 500 ms, p95, server-side, within NFR-8 — `prd.md:716-720`), **NFR-8** (5,000 Tasks per Project — `prd.md:745`), **NFR-9** (WCAG 2.1 AA, keyboard parity — `prd.md:753-757`). Test-design scenarios **X-5** (P0, E2E: focus identity survives paging, no row recycled — `test-design-qa.md:311`), **X-6** (P1: `aria-setsize` true total, `aria-posinset` true ordinal — `:356`), **P-2** (P1, LOAD: first paint one bounded seek per column, last page no slower than the first, measured after paging to the end — `:371`), **P-1** (P1, LOAD: read p95 ≤ 300 ms, write p95 ≤ 500 ms — `:370`), **P-11** (P2: cold-start characterisation, record and assert nothing until AR-40b closes — `:388`), **I-3** (Board column counts include no foreign row — `:267`).
Also binding: **UX-DR38** (announce on deliberate action only, never on loading or windowing — `epics.md:282`, `EXPERIENCE.md:390`; AC6 implements it although the trace row omits it), **UX-DR24** (2.8's keyboard grammar, now over a paged column — `epics.md:265`), **UX-DR25** (move/pick-up sentences carry the *true* total — `epics.md:266`), **UX-DR26** (no infinite scroll — `epics.md:267`, `EXPERIENCE.md:334`), **UX-DR35** (cold-load skeleton, `aria-busy` — `epics.md:279`, `EXPERIENCE.md:279`), **UX-DR39** (focus destinations — `EXPERIENCE.md:401-404`), **AD-15/AR-21** (BIN2 position key; a move writes one key between two concrete neighbours — spine `:162-168`), **AD-2** (`MAXDOP = 1`, unrelaxable here — spine `:83`, test-design-architecture `:135-141`), **AD-10** (no component touches the database on a timer, not even test infrastructure — spine `:132-136`, `test-design-qa.md:432-435`), **AD-19** (wire contracts: append fields, never remove or retype), **AD-29** (spine `:249-259`), **FR-35** (position is readable over the API, never writable — `prd.md:605`), **R3/R4/R13/R18** (`test-design-architecture.md:213-238`, mitigations `:441-471`).
Explicitly **not** here: the List View and its `(sortColumn, TaskId)` keyset (**2.10**, P-3/P-4); the <768px Status pager (**2.11**); push of another participant's change to an open Board, its debounced "3 Tasks changed" summary and refresh affordance (**Epic 7**, UX-DR38 second clause); axe-core reports (X-3 — blocked on Lee's dependency approval, `deferred-work.md:589`); a position-key rebalancing pass (PM, see open question 11); rate-limiter ordering and per-request guess costs (**8.3**, see Ledger). **No Azure deployment, no Azure login, no push** (CLAUDE.md): every deploy-target measurement is re-owned to Lee, not faked locally.
Ledger entries owned by this story (`deferred-work.md`) — each is folded into an AC/Task below or explicitly re-owned in Task 13: `:130` (posinset/setsize kept true under paging), `:219` (sequential GUID locality), `:257` (Space-scoped transaction across the endpoint), `:298` (presence-bound race), `:313` (presence count blocks behind uncommitted presence writes — RCSI), `:315` (refusal purge unbatched), `:336` (one `Account` read per request), `:348` (revocation read ahead of the limiter), `:382` (one `ApiToken` read per Token request), `:390` (per-guess `ApiToken` seek), `:419` (Nightly and Weekly CI stages), `:450` (one Warning line per rate-limit refusal vs the log cap), `:451` (synchronous key-ring read on a cold process), `:459` (idempotency rows of a quiet Space), `:471`/`:476-477` (Projects-per-Space key-range lock reaches another Space; deletions take the same ranges), `:478` (two concurrent same-key creates at one below the bound), `:488` (not-found floor calibrated on a dev machine), `:495` (Board totals, Tasks and Labels are separate statements), `:523` (no Label bound — measured here, decided by PM), `:541` (large Project delete may escalate to a table lock), `:547` (Project listing reads Projects and counts in two statements), `:548` (every listing aggregates all the Space's Tasks), `:551` (deleting a Task with no loaded card moves focus nowhere), `:557` (same-gap key growth; picker and drag reach only loaded cards), `:562` (a full gap is refused with "Choose it again."), `:578` (per-frame drag layout cost). Touched: `:421` (7.3's two-process overlap — its locality half is `:219`), `:498` ("Board loaded." vs "never on loading" — UX; AC6 forces a reading, open question 8), `:589` (X-3 stays Lee's). Code hooks naming 2.9: `Yello.Domain/Boards/IBoardReader.cs:13`, `Yello.Application/Boards/ViewBoard/ViewBoardHandler.cs:19`, `Yello.Contracts/Boards/BoardRoutes.cs:8`, `Yello.Contracts/Boards/BoardResponse.cs:28`, `Yello.Infrastructure/Persistence/ProjectStore.cs:33`, `tests/Yello.Tests.Slices/Tasks/MoveTask/PositionKeyBetweenTests.cs:77`, `tests/Yello.Tests.Client/ClientSurfaceCollection.cs:12`, `tests/Yello.Tests.Client/BoardTests.cs:184`, `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Delete.cs:353`, `tests/Yello.Tests.Slices/Boards/ViewBoard/ViewBoardHandlerTests.cs:18`.
Depends on: **2.2** (Board read, totals, cards), **2.3** (panel, replica), **2.5** (deletions, column focus target), **2.6** (`MoveAsync`, `PositionKey.Between`, Move picker, re-read on `409`), **2.7** (`board-drag.js`), **2.8** (roving grid, `BoardTraversal`, carry, `LiveAnnouncement`, posinset/setsize) — all done.

> **Isolation gates.** 2.9 maps a **new route** (the column page, open question 1), so **1.9-GATE-002** requires isolation rows for it on **both surfaces** before `review`, and the existing Board checker changes meaning (`IsolationCases.cs:1194-1216`, `BoardHoldsExactlyTheCallersTasksAsync`, asserts `tasks.length == total` "every Task is returned (Q6)" — false once a column holds more than one page). No row is pending today (`PendingUntil(` grepped in `tests/Yello.Tests.Isolation`, 2026-10-28: none). If a row is added as pending, **GATE-003** requires it live before `review`.

## Story

As someone with a large Project,
I want the Board to stay fast, complete and keyboard-operable at the scale the product claims,
so that the stated envelope is a guarantee rather than an aspiration.

## Acceptance Criteria

AC1–AC8 are reproduced **verbatim** from `epics.md:1303-1336` (`status: final`), numbered by position. AC9–AC19 come from the architecture spine, the UX spines, the test design and the ledger; each cites its source. Where an AC's reading needs a decision, the open question that decides it is named.

**AC1 — keyset seek, never OFFSET**
**Given** a Project holding 5,000 Tasks
**When** a Board column page is read
**Then** it is fetched by keyset seek on the position key — `WHERE (ProjectId, StatusId) = … AND PositionKey > @last ORDER BY PositionKey` — and never by `OFFSET`
**And** its latency is independent of how deep in the column it sits
*(Proved deterministically by logical reads per page, not only by time: Task 3. Wire shape: open question 1.)*

**AC2 — the last page meets the budget**
**Given** that scale
**When** the last page of a column is read
**Then** it meets the 300 ms p95 server-side read budget, as does the first — because a budget that holds only near the top of a column is not a budget

**AC3 — append, never recycle**
**Given** rendered rows
**When** the window changes
**Then** rows are appended to the DOM and never recycled, and DOM virtualisation is not used
**And** the reason is stated in the story: a recycled row silently re-points keyboard focus at a different Task, so the next keyboard move operates on the wrong one — a data-corrupting defect reachable only by keyboard and invisible to pointer testing

**AC4 — true totals to the eye and to assistive technology**
**Given** a column header at that scale
**When** it renders
**Then** it states the true total from a separate `COUNT` over the same indexed predicate
**And** `aria-setsize` carries that total while `aria-posinset` carries each Task's true ordinal, so the visible chip and assistive technology agree rather than one reporting 4,812 and the other a list of 30

**AC5 — every Task reachable, exactly once**
**Given** the Board at that scale
**When** any Task is sought
**Then** every Task remains reachable and appears in exactly one column
*(Under concurrent change by another Session, see AC10's de-duplication rule and open question 4.)*

**AC6 — nothing announced on loading or windowing**
**Given** loading or windowing
**When** it occurs
**Then** nothing is announced — announcements fire on deliberate action only: a pick-up, a move, a drop, or a filter count
*(The cold load's "Board loaded.": open question 8.)*

**AC7 — focus follows identity; keyboard drives the window**
**Given** focus resting on a Task when the window changes
**When** rendering completes
**Then** focus follows the Task **identity** and never the row element, and keyboard navigation drives the window rather than the reverse

**AC8 — the cold-start position is decided and recorded**
**Given** the first measurement of this budget
**When** it is taken
**Then** the cold-start position is decided and recorded rather than left silent — either minimum replicas pinned to 1, or NFR-5 declared measured warm with the cold path explicitly exempted
*(Open question 9. Recorded in `docs/operations.md` §7 and the spine's deferral row `:425`, closing AR-40b.)*

**AC9 — the page contract** *(AR-22 page size; AD-19; FR-35; GATE-002; open questions 1-2)*
**Given** the Board read and the new column-page read
**When** either answers
**Then** each column carries at most **50** Tasks (one named constant, tunable — "the seek shape is the invariant, the page size is not", spine `:259`), its true `Total`, and a cursor that is `null` exactly when no Task follows the page
**And** the cursor is the last returned Task's `PositionKey` (readable, as FR-35 already makes it), an invalid cursor is refused `400` with a machine-readable type, a Status outside the Project's effective set or a foreign Project answers the boundary's `404 not-available`, and the route has isolation rows on both surfaces
**And** wire changes are appends only (AD-19): `BoardColumnResponse` keeps every existing field

**AC10 — the loaded column is a contiguous prefix** *(AD-29 posinset; `deferred-work.md:495`, `:557`; open questions 3-4)*
**Given** a column whose cursor is not `null`
**When** a Task's key sorts after the column's cursor — a creation (`PositionKey.After(last)`), a Status edit (lands last), or another Session's change seen in a page
**Then** that Task is **not** rendered until paging reaches it, while the column's `Total` moves at once; and a move placed directly after the last loaded card advances the cursor to the moved Task's admitted key
**And** a page that returns a Task id already rendered moves that card to the page's position (newer read wins) unless the client holds an unsettled write for it, so no Task is ever drawn twice
**And** therefore every rendered card's `aria-posinset` (its index + 1 in the column) is its true ordinal

**AC11 — every route reaches the next page** *(AC7; `deferred-work.md:557`; UX-DR21/24; open questions 5-6)*
**Given** a column with more Tasks than are loaded
**When** `↓` is pressed on its last loaded card, or a carried Task is stepped `↓` past it, or the column's "Show more" control is activated, or the Move picker's position list is extended
**Then** the next page is fetched, appended, and focus (or the carried Task) lands on the next Task by identity; nothing is announced
**And** the Move picker offers "Bottom of column" only when the column is fully loaded, because `afterTaskId` must name a concrete neighbour and the client does not know the true last Task of a partly loaded column

**AC12 — focus destinations for a Task with no loaded card** *(`deferred-work.md:551`; UX-DR39, `EXPERIENCE.md:401-404`; open question 7)*
**Given** the Task detail panel open on a Task whose card is not loaded — after a Status change sent it past a cursor, or after it was deleted
**When** the panel closes or the deletion completes
**Then** focus moves to the column that holds the Task (its `<section>` focus target), never to `<body>`

**AC13 — reads never wait on writers, and the snapshot is decided** *(`deferred-work.md:495`, `:547`, `:548`, `:313`; open question 10)*
**Given** any Space-scoped read in every environment (Testcontainers, CI, Azure SQL)
**When** it runs beside an uncommitted write — a presence insert, a large Project deletion, a creation
**Then** it does not wait on that write, because `READ_COMMITTED_SNAPSHOT` is on in every database the migrations create or update, asserted by a schema test
**And** the decision on whether a column's `COUNT`, its page and its Labels share one snapshot is recorded, with the bounded disagreement it permits stated

**AC14 — the client holds 5,000 loaded cards within the motion budget** *(test-design R4 `:142-146`, `:459-471`; `deferred-work.md:578`; NFR-3; open question 12)*
**Given** one column with all 5,000 Tasks loaded by paging
**When** a keyboard step, a pick-up, a carried step, or a drag frame occurs
**Then** the time from input to the focused or lifted card's update is measured and recorded, a keyboard step stays inside the near-instant motion budget (`--motion-instant` 90 ms), and a lifted drag performs no per-frame layout read of every card while the pointer is still
**And** if append-only cannot hold it, the story stops and escalates an AD-29 amendment (a focus-identity-preserving window) rather than introducing virtualisation

**AC15 — Nightly and Weekly stages exist, with their first suites** *(`deferred-work.md:419`; `test-design-qa.md:402-435`; `TESTING-CONVENTIONS.md:711-714`; open question 13)*
**Given** the CI workflows
**When** they are inspected
**Then** a Nightly stage runs the Board-at-5,000 measurements (P-2) and the focus-identity-paging E2E (X-5), and a Weekly stage runs the local LOAD at the envelope (P-1) and the cold-start characterisation (P-11), each against a local Testcontainers target and never Azure (R13)
**And** the PR stage does not run them, still finishes under 15 minutes, `actionlint` reports clean, and no suite introduces a timer-driven database touch (AD-10)

**AC16 — the envelope load and its report** *(P-1; NFR-5; R18 `test-design-architecture.md:238, 264`; `deferred-work.md:257`, `:336`, `:382`)*
**Given** the Weekly load at the NFR-8 envelope realisable today (5,000 Tasks in one Project, 50 Sessions of one Account — multi-member Spaces arrive in Epic 4)
**When** it runs
**Then** it reports p95 per operation class (Board read, column page first and last, create, edit, move, delete) measured server-side from the Host's own request-finished figure, asserts reads ≤ 300 ms and writes ≤ 500 ms, and states its measurement condition in its own output (local engine, warm, not production evidence)

**AC17 — locks at scale stay inside one Space** *(`deferred-work.md:471`, `:476-477`, `:541`; open questions 14-15)*
**Given** a Project of 5,000 Tasks each carrying Labels being deleted in Space A, or a Project creation counting the Projects-per-Space bound in Space A
**When** a write in Space B runs at the same moment
**Then** B does not wait on A — proved deterministically (A held open; B completes while A is still held), never by timing

**AC18 — a full gap is refused in its own words** *(`deferred-work.md:557`, `:562`; open question 11)*
**Given** a move into a gap where no key of at most 128 characters fits
**When** it is refused
**Then** the refusal type and copy differ from a changed neighbour's, and say to choose another position rather than "Choose it again."

**AC19 — the ledger is settled** *(every entry listed in the header)*
**Given** each `deferred-work.md` entry owned by 2.9
**When** the story reaches `review`
**Then** each is CLOSED with its evidence, or RE-OWNED with a named owner and a reason, in the ledger itself

## Tasks / Subtasks

- [x] **Task 1 — Baseline, then decide the open questions** (AC: all)
  - [x] `dotnet tool restore`; `dotnet build Yello.slnx` (0 warnings, 0 errors); `dotnet build-server shutdown`; record the full-run baseline with `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1` (expected 1284 total / 1281 passed / 0 failed / 3 skipped, from 2.8's review). Check Docker readiness first (`docker info --format '{{.ServerVersion}}'`).
  - [x] Decide open questions 1-16 below; record each in Completion Notes as "**Qn - (x).** … Cost to later stories: …". Each is reversible.

- [x] **Task 2 — Server: the column page and the paged Board read** (AC: 1, 4, 5, 9)
  - [x] Domain port `IBoardReader` (`Yello.Domain/Boards/IBoardReader.cs`): replace `ListTasksAsync(projectId)` (all Tasks, project-wide order) with a per-column seek, e.g. `PageAsync(Guid projectId, Guid statusId, string? after, int size, CancellationToken)` returning up to `size + 1` rows so the adapter knows whether a cursor follows. Keep `CountTasksByStatusAsync` (AD-29's separate count) and `LabelIdsOfAsync` (already "one statement for the whole page"). Add a column count for one Status for the page route (`COUNT` over the same predicate).
  - [x] Adapter `Yello.Infrastructure/Persistence/BoardReader.cs`: `Where(ProjectId == p && StatusId == s && PositionKey > after)` → `OrderBy(PositionKey)` → `Take(size + 1)`, `AsNoTracking`. **Verify in the logged SQL** that (a) there is no `OFFSET`; (b) the `@after` parameter is `varchar(128)`, not `nvarchar` — an `nvarchar` parameter against the `varchar` BIN2 column forces a conversion and loses the seek; (c) the plan seeks `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` (RLS adds the leading `SpaceId` predicate). Use `string.Compare(t.PositionKey, after) > 0` (EF translates it to `>`); the column's BIN2 collation makes the comparison ordinal. The index is unique on `(SpaceId, ProjectId, StatusId, PositionKey)`, so no `Id` tiebreaker is needed — say so in the remarks.
  - [x] Board read (`ViewBoardHandler`): first page per Status — **one bounded seek per column** (P-2: "first paint one bounded seek per column"); never `ROW_NUMBER() OVER (PARTITION BY StatusId …)`, which reads the whole Project. A loop over the effective Statuses (3-5 statements) or one statement applying a `TOP` seek per Status are both acceptable; measure and record which. Remove the remark "every Task is returned (open question 6 (a)); story 2.9 pages them" and the matching remarks at `IBoardReader.cs:13`, `BoardRoutes.cs:8`, `BoardResponse.cs:28`.
  - [x] New slice `Yello.Application/Boards/ViewBoardColumn/` (handler + validation of the cursor through `PositionKey`'s alphabet and `MaxLength` 128): Project not found or Status outside the Project's effective set (`EffectiveStatusSet`) → `null` → `BoundaryProblems.NotAvailable()`; malformed cursor → `400` with a new `ProblemTypes` entry (e.g. `urn:yello:problem:invalid-board-cursor`), added to `RefusalCopy`/`ClientCopy.resx` if the client can meet it.
  - [x] Host: `BoardEndpoints.MapBoard` gains the page route with `.RequiresCapability(Capability.ReadProjectsAndTasks)` (every Role, Viewers included); register the handler in `Program.cs` beside `ViewBoardHandler` (`:112`). Space resolution attaches because the route carries `{spaceId}` (`SpaceScopedRouteCheck`, `Program.cs:436`).
  - [x] Contracts (append only): `BoardColumnResponse` gains `string? NextAfter` (name per Q1); new `BoardColumnPageResponse` and a `BoardRoutes` entry + builder. Update `PersistenceModelGateTests`/contract gates if they enumerate routes or response types.
  - [x] Page size: one constant (e.g. `BoardPage.Size = 50`) in Application or Contracts, cited to AR-22 and spine `:259` as an assumption.

- [x] **Task 3 — Prove the seek and the budget at 5,000** (AC: 1, 2, 4, 5, 9; P-2, I-3)
  - [x] PR tier (Slices, `BoardEndpointTests` or a new `BoardPagingTests` partial): seed 5,000 Tasks in one column with `TaskWire.SeedTasksAsync` (one set-based INSERT; ~ms) and walk every page: assert 100 pages, every Task id exactly once, keys strictly ascending, each page's `Total` 5,000, last cursor `null`. Second case: 5,000 split over three Statuses — every Task in exactly one column (AC5).
  - [x] Deterministic depth-independence (PR tier, no timing): read `SET STATISTICS IO`-equivalent logical reads (or `sys.dm_exec_query_stats` for the statement) for the first and the last page; assert they are equal within a small constant. **Plant** an `OFFSET`/`Skip()` implementation: the last page's reads must grow and the case must fail. Restore in a `finally` (never `git checkout`/`restore`).
  - [x] Nightly tier (P-2, Slices, a new `DisableParallelization` collection e.g. `BoardScaleCollection`): server-side p95 of the first and the last page over ≥ 100 interleaved samples each after a warm-up, measured with `RunningHost.ServerDurationOfAsync` (the Host's own request-finished figure). Assert both ≤ 300 ms, and depth independence with `DurationIndistinguishability.CompareMeasuredAsync` (medians, 20% tolerance — reuse it; do not write another). Add a nearest-rank percentile helper to `Yello.Tests.Shared` (none exists) with its own unit tests in Architecture or Slices.
  - [x] Seed the scale cases with Labels applied (e.g. three per Task, via raw `WireDatabase` insert into `TaskLabel`) so the Labels read is inside the measured read (`deferred-work.md:523`'s measurement half). Record key lengths of seeded vs product-placed keys.
  - [x] Isolation (`tests/Yello.Tests.Isolation/IsolationCases.cs`): change `BoardHoldsExactlyTheCallersTasksAsync` to assert `tasks ≤ 50`, cursor null iff the column fits, and that walking the pages yields exactly `total` of the caller's Tasks (I-3 includes Board column counts). Add rows for the page route on both surfaces: foreign Space (`ISO-DIRECT-BOARD-COLUMN`), a foreign Project id / foreign Status id inside one's own Space (`ISO-BOARD-COLUMN-FOREIGN-ID-IN-OWN-SPACE`, `SameAsAnchor(404, not-available)`, no refusal row), the page's `Total` counts no foreign row (`ISO-BOARD-COLUMN-AGGREGATE`), and a timing row if `IsolationCases.TimingRows` covers every Space-scoped `GET` (follow `ISO-TIMING-IN-SPACE`). Prove each new row against a plant that drops the Task's layers (`IgnoreQueryFilters()` + the FILTER predicate), as 2.3 did.

- [x] **Task 4 — Client: the paged column state** (AC: 3, 4, 5, 10)
  - [x] `BoardView.razor` `ColumnState` gains the cursor (`string? After`), a loading flag, and a Task-id index so `Locate` (`BoardView.razor.cs:1026-1039`, a linear scan of every column) becomes O(1) at 5,000 loaded cards. Keep `@key="task.Id"` on every `TaskCard` and `@key="column.StatusId"` on columns.
  - [x] Append a page: for each Task, if its id is already rendered anywhere, apply AC10's rule (move the card to the page's position unless `_replicas` holds an unsettled write for it — then the replica wins); otherwise append. Advance the cursor to the page's cursor. Never remove, reorder or reuse an element for a different Task id.
  - [x] Hold-out rule (AC10): `Add` (`BoardView.razor:468-480`) appends only when the column's cursor is `null`; otherwise `Total++` and no card. Status edits and `InsertByKey`/`Relocate` (`BoardView.razor.cs:1020-1024`, `912-994`) render a Task in a partly loaded column only when its key ≤ the cursor; a move placed directly after the last loaded card renders at once (optimistic) and, on admission, sets the cursor to the admitted key (it is strictly below the old successor, so the prefix stays contiguous). Totals keep moving with the card as today (`column.Total--/destination.Total++`).
  - [x] The page's `Total` (Q2): adopt it for a column only when no write touching that column is unsettled in `_replicas`.
  - [x] Re-read after `409` (`RereadBoardAsync`, `:371-399`, replaces `_columns` wholesale): apply Q3's decision; whatever it is, the focused/active/held Task identity must be present after the re-read or focus moves to its column (AC12), and nothing is announced except 2.8's "Returned to …" for a held Task.
  - [x] Add a gate in `tests/Yello.Tests.Architecture` (beside `KeyboardGateTests`) that no `Virtualize` component and no `ItemsProvider` is used anywhere in `Yello.Client`, with a permanent plant case (AC3).

- [x] **Task 5 — Client: keyboard, picker, drag and the "Show more" control drive the window** (AC: 6, 7, 11)
  - [x] `BoardView.Keys.cs` `Traverse` (`:195-228`): `↓` on the last loaded card of a column with a cursor fetches the next page, then requests focus on the next Task by id (`FocusCardTitle`). While a fetch is out, further `↓` in that column is ignored (never queued past the boundary). `BoardTraversal` (pure) stays over loaded counts; feed it the loaded counts and handle the boundary in `BoardView`, or extend it with a `HasMore` flag — record which. The sticky origin (`_runOrigin`) survives the fetch.
  - [x] Carry (`Step`, `:308-341`): a carried `↓` past the last loaded card in a column with a cursor fetches the next page first, then steps. Announced totals stay the true `Total` (+1 when entering another column), as today.
  - [x] "Show more" control at each partly loaded column's foot (`BoardColumn.razor`), for pointer, touch and screen-reader browse mode (Q5). Activating it appends a page and moves focus to the first newly loaded Task. Copy in `ClientCopy.resx` (localisation gate); no count in it (no nudges, `epics.md:1049`). Present for every Role.
  - [x] Move picker (`MovePicker.razor:108-120`, `MoveColumns` `BoardView.razor.cs:212`): the position list for a partly loaded column ends with an option that loads the next page into the Board's column state (one data source) and extends the list; "Bottom of column" appears only when the column's cursor is `null` (AC11). Keep the picker's one-tab-stop roving pattern (2.8 Q7).
  - [x] Drag (`board-drag.js`): drops still target loaded cards only (ledger `:557` 2.7 note — record as accepted; the "Show more" control is the pointer route deeper). Fix the per-frame cost (`deferred-work.md:578`, `#step` `:339-357`, `#hitTest` `:404-435`): cache the hovered column's card rects at lift and on scroll/auto-scroll/append, and skip `#follow`/`#hitTest` on frames with no pointer movement and no auto-scroll. Planted: per-frame `getBoundingClientRect` calls on a still pointer must be zero (count them through a test hook, as 2.7's harness reads page state).
  - [x] AC6: no `Say` on page fetch, page append, "Show more", cursor advance or re-read. Assert with `KeyboardHarness.RecordAnnouncements` (`window.__said`) and a plant (say "Loaded." on append) that the case catches.

- [x] **Task 6 — Client: focus destinations for a Task with no loaded card** (AC: 12)
  - [x] `Deleted` (`BoardView.razor.cs:502-535`): when `Remove` finds no card, raise the column focus request for the Status the panel's Task was in (the panel holds the `TaskResponse`). Same for the panel's `Close()` when the card is held out after a Status change.
  - [x] Browser cases (FakeYelloApi): a deletion and a Status change on a Task beyond the loaded page each leave `document.activeElement` inside that column's `<section>`; planted (no request) → `<body>` → caught.

- [x] **Task 7 — Client render cost at 5,000 loaded cards** (AC: 14)
  - [x] Measure first: in a browser case with one column paged to all 5,000 (FakeYelloApi serving pages), record per page append time, input-to-focus latency of `↓` at the bottom, pick-up, carried step, and the JS heap. Record the numbers in the Debug Log.
  - [x] Expected hot spot: every `BoardView` render re-renders every `TaskCard`, because `TaskResponse` is a complex parameter (and `LabelIds` is a list, defeating record equality). Give `TaskCard` (and `BoardColumn`) a `ShouldRender` that compares the inputs it draws (id, title, Status, due date, Label ids sequence, `Active`, `Position`, `SetSize`, request counters, carried/lifted flags). Re-measure.
  - [x] Gate the keyboard step at the near-instant budget measured on the page's own `performance.now()` (2.7's pattern), in a `DisableParallelization` collection (e.g. reuse `TouchTimingCollection` or add `BoardScaleTimingCollection`) — Nightly tier if the 5,000-card setup is slow. If the budget cannot be met append-only, stop and raise the AD-29 amendment (AC14), recording it as a blocker rather than shipping a window.

- [x] **Task 8 — Snapshot and RCSI** (AC: 13)
  - [x] New migration (`dotnet ef migrations add …` with the local `dotnet-ef` 10.0.11): `suppressTransaction: true`, guarded — `IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()) = 0 ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;` (Azure SQL already defaults it on, so the guard makes production a no-op). `Down` turns it off the same way. Follow the shape of `20260925132825_ActiveSpaceResolution.cs:58-60` (MAXDOP).
  - [x] Assert it: extend `DatabaseIdentityTests` (`:94` asserts MAXDOP) and `MigrationBundleEquivalenceTests` (`:132`) with `is_read_committed_snapshot_on = 1`; confirm the migration bundle still builds (`YELLO_MIGRATION_BUNDLE` cases skip locally unless set — run them once with it set, per CLAUDE.md).
  - [x] **Re-run every race class** after the migration (`TaskEndpointTests.MoveRace/EditRace/DeleteRace/LabelsRace`, `ProjectEndpointTests.DeleteRace`, the `Bound` races, `TaskWire.WaitUntilBlockedOnALockAsync` users — 40 call sites). Hinted reads (`UPDLOCK, HOLDLOCK`) still lock under RCSI; a race whose *waiting* side is an unhinted read will no longer block and must be re-examined, not loosened.
  - [x] Deterministic proof (AC13): hold an uncommitted presence insert / a Project deletion of 5,000 Tasks open on a `WireDatabase` connection; a Board read and a Project listing in the same Space complete while it is held. Plant: RCSI off for that database → the read waits (`LCK_M_S`) → caught.
  - [x] Record Q10's snapshot decision in the remarks of `ViewBoardHandler`, the page handler and `ListProjectsHandler`, and in the ledger (`:495`, `:547`).

- [x] **Task 9 — Locks at scale** (AC: 17)
  - [x] Project deletion (`ProjectStore.DeleteAsync:92-117`, `deferred-work.md:541`): seed 5,000 Tasks with three Labels each; run the product's delete statement shape inside a held `WireDatabase` transaction and read `sys.dm_tran_locks` for an `OBJECT`-level `X` on `Task`/`TaskLabel`. If it escalates, apply Q15's decision and prove a Task write in another Space completes while the deletion is held. Keep atomicity (never half a Project — 2.5 AC3); `A_Project_of_5000_Tasks_is_deleted_whole_by_one_request` keeps asserting nothing about time.
  - [x] Projects-per-Space count (`BoundCounter.cs:48-67`, `deferred-work.md:471`, `:476-477`): reproduce the cross-Space wait deterministically (Space A's creation held at the count; Space B's creation queues on `LCK_M_RS_U` at the end-of-range key — the review's observation), then apply Q14 and prove B completes while A is held. Re-run RACE-* and BND-* cases; the Tasks-per-Project count is already Space-led (`:471` NOTE) and stays.

- [x] **Task 10 — The full-gap refusal** (AC: 18)
  - [x] `TaskStore.MoveAsync`/`Placed` (`:300-375`): distinguish "neighbour not in the destination column" (`TaskMove.PositionUnavailable`, unchanged) from "no key fits" (new outcome) — today both return `PositionUnavailable` (`:337`, `:351`). New `ProblemTypes` entry (e.g. `urn:yello:problem:task-position-full`), mapped in `TaskMoveEndpoints.cs:72`/`TaskMoveBody.cs:92`, `RefusalCopy` + `ClientCopy.resx` with Q11's copy; the client re-reads the Board for it as for the other `409`. `RefusalCopyTests` and the refusal gates updated.
  - [x] Case: `PositionKeyBetweenTests`'s 718-placement growth (`:77`) reused to fill one gap through the endpoint (or seed two neighbours whose keys leave no room) → the new type; the changed-neighbour case keeps the old type.
  - [x] Record real key lengths after the Weekly load (max and p99 length per column) in the Debug Log and the ledger (`:557`).

- [x] **Task 11 — Nightly and Weekly stages** (AC: 15, 16, 8)
  - [x] Tier selection (Q13): a way to tag Nightly and Weekly tests that `TestingConventionTests` permits, and a PR run that excludes them (`dotnet test Yello.slnx … -- --filter-not-trait …` under MTP/xunit v3 — verify the multi-value semantics). Beware the exit-code-8 trap: a filtered run of a project with zero selected tests exits 8 (`TESTING-CONVENTIONS.md:520-562`); run only the projects that hold tier tests.
  - [x] Workflows: `.github/workflows/nightly.yml` and `weekly.yml` (or jobs in `ci.yml` — Q13), `schedule` + `workflow_dispatch`, same restore/build/Playwright steps as `ci.yml`'s `test` job, `--max-parallel-test-modules 1`, TRX upload. `actionlint` 1.7.12 clean; extend `DeploymentShapeTests` to hold the new files' shape (triggers, no Azure login, no secrets, no deploy steps).
  - [x] Weekly LOAD (P-1, AC16) in Slices (`RunningHost` in-process, the suite's one engine — no new container class; `ContainerTopologyGateTests` budget 5 unchanged): 5,000 Tasks with Labels in one Project, 50 Sessions (one Account signed in 50 times; presence bound 50), a fixed mix of Board reads, column pages (first and deepest), creates, edits, moves and deletes for a fixed number of iterations (not a duration — no timers). p95 per operation class from the Host's request-finished figures; assert read ≤ 300 ms, write ≤ 500 ms; print the measurement condition line (engine, `MAXDOP = 1`, RCSI on, warm, local, "not production evidence" — R18). Include the two `404` paths' p95 for `NotAvailableFloor` (`deferred-work.md:488`) and the Space home listing at 50 Projects × 100 Tasks plus one 5,000-Task Project (`:548`). Runs in a `DisableParallelization` collection; never in parallel with another timing class.
  - [x] Weekly P-11 (cold-start characterisation, local): time from Host process start to the first Space-scoped response (key-ring load `KeyRingStore`, EF model build, first query) for several fresh Hosts; record, assert nothing (`test-design-qa.md:388`). Note what local cannot show: Azure SQL resume (up to ~1 minute) and Container Apps scale-from-zero.
  - [x] AR-40b (AC8, Q9): record the decision in `docs/operations.md` §7 (replace the provisional sentence), and close the spine's deferral row (`ARCHITECTURE-SPINE.md:425`) with the decision, date and the R3/R5 note (`MAXDOP = 1` stays unrelaxable; re-measure after AR-40c — `test-design-architecture.md:630`). `OperationsRecordTests` may hold §7's wording — update it with the text. Fix the $/£ inconsistency in passing only if that row is edited (spine says $12–15; operations.md says £12–15).
  - [x] Update `tests/TESTING-CONVENTIONS.md:711-714` ("Nightly and Weekly do not exist yet …") to describe the stages, tier selection, the percentile helper and the scale collections.

- [x] **Task 12 — Browser suites for paging** (AC: 3-7, 10-12; X-5, X-6)
  - [x] `FakeYelloApi` (`tests/Yello.Tests.Client/FakeYelloApi.cs`): serve the Board's first page per column (50) and the page route with cursors from `FakeColumn`; record requests; keep `HoldBoardAsync`/`ReleaseBoard` and add a hold for a page answer (`TaskCompletionSource`, "hold, assert, release, assert"). `DragHarness.Many(count)` (`:139`) seeds large columns.
  - [x] X-5 (P0): focus identity survives paging — focus on the last loaded card, `↓` loads the next page and focus lands on Task 51 by `data-task-id`; the previously focused element is still the same Task's card; plant (render a page by replacing the list / key by index) → caught.
  - [x] X-6 (P1): with 120 of 5,000 loaded, every card's `aria-posinset` equals its true ordinal and `aria-setsize` 5,000; the column description reads "5000 Tasks" (or the locale's formatting — check `ColumnCountMany`); extend `KeyboardTests.Each_column_is_named_by_its_Status_and_described_by_its_true_total` rather than duplicating it.
  - [x] AC10 cases: creation in a partly loaded column → no card, total +1, "Task added." said once; a page containing an already-rendered Task (moved by "another Session" in the fake) → one card, at the page's position.
  - [x] AC11 cases: carry `↓` across a page boundary; "Show more"; the Move picker's extension and the absent "Bottom of column".
  - [x] Nightly real-Host case (`RealHostCollection`): 5,000 Tasks in one column; keyboard traversal from the first to the last Task asserting focus lands on the intended Task at each step (`test-design-architecture.md:467`). Long; Nightly only.
  - [x] Build `Yello.Client` in **Debug** before any browser run (`TESTING-CONVENTIONS.md:253-257`); rerun a Client failure seen only in the full run alone before calling it real (memory: full-run starvation).

- [x] **Task 13 — Plants, runs, ledger, status** (AC: all, 19)
  - [x] Plants, one at a time, original saved in memory and restored in a `finally`, hash compared after restore: `OFFSET` seek; `nvarchar` cursor parameter (logical reads or plan shows a scan); posinset from a page-local index; card element reused across Task ids (key by index); announcement on append; no column focus for an unloaded deleted Task; RCSI off; per-frame rect reads on a still pointer; full gap mapped to the old type; Projects-per-Space count with the old range lock. Record the table `| # | Plant | Gate | Caught |`.
  - [x] `dotnet build Yello.slnx` 0/0; `dotnet build-server shutdown`; full run with `--max-parallel-test-modules 1` (never two `dotnet test` at once); Nightly/Weekly tests run once locally by their filter and their numbers recorded.
  - [x] Ledger: for every entry in the header, write CLOSED (with the test/evidence) or RE-OWNED (owner, reason) **in `deferred-work.md` itself**. Recommended dispositions are in "Ledger dispositions" below. Add new entries for anything deferred here, with owners.
  - [x] `sprint-status.yaml`: `2-9-the-board-at-five-thousand-tasks: review`.

### Review Findings

Code review 2026-10-28 (`bmad-code-review`, full mode, `ff8b728..HEAD`): 44 raw findings from three layers (Blind 20, Edge 11, Auditor 13), 30 after de-duplication - 14 patch, 5 defer, 11 dismissed; one more patch was found while verifying the patches (15 in all).

- [x] [Review][Patch] **The Project's Tasks lock is not named for the Space** (high): `Yello/ProjectTasks/{projectId}` is global, and the Tasks-per-Project bound takes it from the route before the Project is checked, so `POST /spaces/{B}/projects/{A's project}/tasks` queues behind Space A's held writers (a cross-tenant wait and a timing signal that the id exists - AC17, AD-3) [Yello.Infrastructure/Persistence/ApplicationLocks.cs:43]
- [x] [Review][Patch] **Idempotency-Key lookup no longer waits on an uncommitted record under RCSI** (medium): a retry overlapping the first request's commit now runs the effect again and answers `409 idempotency-key-in-flight` instead of replaying; `READCOMMITTEDLOCK` restores the pre-RCSI behaviour (a Q18-class read) [Yello.Infrastructure/Persistence/IdempotencyStore.cs:51]
- [x] [Review][Patch] **A refused column page (`404`) is silent and the control stays** (medium): the Board's cold load raises the page's one refusal surface on a `404`; the page fetch swallows it [Yello.Client/Components/BoardView.Paging.cs:89]
- [x] [Review][Patch] **The card index survives a Space purge** (low): `_cardIndex` keeps the previous Space's `ColumnState`s and Tasks after `Purge` [Yello.Client/Components/BoardView.razor:300]
- [x] [Review][Patch] **RCSI migration: `Down` would turn off Azure SQL's own default, and its remarks are stale** (low): guard `Down` off Azure SQL Database (`EngineEdition` 5); the remark that the hinted range locks "still lock exactly as before" contradicts Q17; state that `ROLLBACK IMMEDIATE` ends open transactions on a non-Azure target [Yello.Infrastructure/Persistence/Migrations/20261028120526_ReadCommittedSnapshot.cs:44]
- [x] [Review][Patch] **The scheduled workflows' checkout keeps the token in `.git/config`** (low): `persist-credentials: false` [.github/workflows/nightly.yml, .github/workflows/weekly.yml]
- [x] [Review][Patch] **The browser p95 is taken over 21 samples; the convention is at least 100** (low) [tests/Yello.Tests.Client/BoardRenderCostTests.cs:36]
- [x] [Review][Patch] **A doc comment split from its method in the fake** (low): `TryColumnPage` was inserted between `TryUnderProject`'s summary and its method [tests/Yello.Tests.Client/FakeYelloApi.cs]
- [x] [Review][Patch] **Stale range-lock and pre-RCSI wording** (medium): `TESTING-CONVENTIONS.md` race sections (2.2-2.6) still teach `UPDLOCK, HOLDLOCK` range shapes, "range before row" and "a read-committed read already waits"; `TaskStore`'s class remarks, `ApplyAsync`'s Q18 comment (it does not stop a title edit racing a deletion), `DeleteWire`, `MoveWire`, `EditRace`, `LabelsRace`, `TaskEndpointTests.Bound` and the two ATDD-era gate remarks say the same [tests/TESTING-CONVENTIONS.md:102-161, Yello.Infrastructure/Persistence/TaskStore.cs:22-70]
- [x] [Review][Patch] **Q10's snapshot decision is not recorded in `ListProjectsHandler`** (low): Task 8's last subtask [Yello.Application/Projects/ListProjects/ListProjectsHandler.cs]
- [x] [Review][Patch] **The key-length report gives max and average, not the p99 Task 10 asks for** (low) [tests/Yello.Tests.Slices/Boards/EnvelopeLoadTests.cs]
- [x] [Review][Patch] **The cold-path figure is called "a fresh Host process"; it is a fresh in-process Host** (low) [docs/operations.md]
- [x] [Review][Patch] **AC10's cursor advance (a move placed directly after the last loaded card) has no test** (medium) [tests/Yello.Tests.Client/BoardPagingTests.cs]
- [x] [Review][Patch] **The story's record overstates what was done** (low): `BoardColumn` has no `ShouldRender` (Task 7); the deleted-Task focus plant and the baseline run (Task 1) were not done; three of the four new isolation rows are not plant-proven (Task 3); AC14's drag frame records rect reads, not input-to-update time; AC15's "PR stage under 15 minutes" has no CI timing behind it [this file, Dev Agent Record]
- [x] [Review][Patch] **PG-14..PG-17 and PG-19 counted any lock wait in the database as "the request waited on the held side"** (medium; found while verifying the patches): under the full suite's load PG-16 failed 2 runs in 3 on a transient wait that was gone a moment later, while it passed alone and in its namespace. The cases now count only waits that lead to the holder's session (`WaitUntilBlockedByTheHolderOrAnsweredAsync`); the RCSI-off and lock-name plants are still caught [tests/Yello.Tests.Slices/Boards/BoardPagingTests.cs]
- [x] [Review][Defer] **Label deletion's `TaskLabel` range lock, and the point `UPDLOCK, HOLDLOCK` reads on ids another Space owns, can still lock another Space's keys** [Yello.Infrastructure/Persistence/LabelStore.cs:103, TaskStore.cs:235, ProjectStore.cs:100] — deferred, pre-existing (not in this diff); owner story 3.4
- [x] [Review][Defer] **A title or due-date edit racing a Task deletion still reaches a 0-row UPDATE (`500`)** [Yello.Infrastructure/Persistence/TaskStore.cs:192] — deferred, pre-existing (the deletion's `UPDLOCK` never blocked the edit's shared read, before or after RCSI); owner story 7.4
- [x] [Review][Defer] **After a re-read, a kept Task that another Session deleted or moved makes `RestoreDepthAsync` page its whole column** [Yello.Client/Components/BoardView.Paging.cs:261] — deferred; owner story 7.3
- [x] [Review][Defer] **Three of the four new isolation rows have no plant of their own** [tests/Yello.Tests.Isolation/IsolationCases.cs] — deferred; owner story 3.4
- [x] [Review][Defer] **The `:219` locality closure measured the seeded `NEWID()` rows, not product-created ones** [_bmad-output/implementation-artifacts/deferred-work.md:219] — deferred, closure reopened; owner story 7.3

## Dev Notes

### The shape of the story in one paragraph

Today nothing pages: `BoardReader.ListTasksAsync` returns every Task in the Project in one project-wide ordered statement, the client renders all of them, and `aria-posinset` is the card's index among loaded cards — true only because everything is loaded. This story makes each column a **contiguous loaded prefix** read 50 at a time by keyset seek on `PositionKey` (unique within `(SpaceId, ProjectId, StatusId)`, BIN2), with the true total from a separate `COUNT`, a cursor that is the last key, and every route — `↓`, a carried `↓`, "Show more", the Move picker — able to fetch the next page and land on the next Task **by identity**. The prefix invariant is what keeps `aria-posinset` true: a Task whose key falls after the cursor is held out until paging reaches it, and a page never draws a Task twice. Around that core it proves the budget (deterministically by logical reads in the PR tier, by p95 in Nightly), turns `READ_COMMITTED_SNAPSHOT` on everywhere so reads never wait on writers, keeps large-Project deletion and the Projects-per-Space count from blocking other Spaces, gives a full gap its own refusal, creates the Nightly and Weekly stages with their first suites, decides AR-40b, and settles 26 ledger entries.

### Why append and never recycle (AC3, stated as the story requires)

A virtualised list reuses a DOM row for whichever item scrolls into it. Keyboard focus sits on an element, not on a Task, so when the window shifts the focused element silently comes to represent a different Task, and the next `Space`/`Enter`/arrow acts on the wrong one — a data-corrupting defect reachable only by keyboard and invisible to pointer testing (spine AD-29 `:255`, `EXPERIENCE.md:404`). Append-only rendering with `@key="task.Id"` means an element only ever represents one Task, so focus following the element *is* focus following the identity. The cost is an unbounded render tree (test-design R4); Task 7 measures it and AC14 says what happens if it fails.

### What exists today (read before writing)

- **Server read.** `Yello.Application/Boards/ViewBoard/ViewBoardHandler.cs` (`HandleAsync` `:37`): `FindProjectAsync` → `EffectiveStatusSet.Of(statuses.ListAsync())` → `CountTasksByStatusAsync` (`:45`, one grouped `COUNT`) → `ListTasksAsync` (`:46`, **all** Tasks) → `LabelIdsOfAsync(ids)` (`:47`) → groups in memory, throwing if a Task's Status is outside the set (AD-17). Records `BoardView`/`BoardColumnView(StatusDefinition Status, int Total, IReadOnlyList<TaskItem> Tasks)` (`:78-89`). Port `Yello.Domain/Boards/IBoardReader.cs`; adapter `Yello.Infrastructure/Persistence/BoardReader.cs` (`:31-37` totals, `:40-47` all Tasks ordered `PositionKey, Id`). Labels: `TaskLabelReads.LabelIdsOfAsync` (`:30-53`) — one statement for the ids given; at 50 ids per column it is small.
- **Endpoint.** `Yello.Host/Endpoints/BoardEndpoints.cs` (`MapBoard` `:22-24`, `ReadAsync` `:36`, mapping `:46-52` via `TaskEndpoints.Response`), registered at `Program.cs:401` on the API group; handler DI `Program.cs:112`.
- **Contracts.** `Yello.Contracts/Boards/BoardResponse.cs` — `BoardResponse(BoardProjectResponse Project, IReadOnlyList<BoardColumnResponse> Columns)`, `BoardColumnResponse(Guid StatusId, string Name, int Total, IReadOnlyList<TaskResponse> Tasks)` (`:35`). `BoardRoutes.Board = "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/board"` (`:15`). `TaskResponse(Guid Id, string Title, Guid StatusId, string PositionKey, DateTimeOffset CreatedAt, DateTimeOffset? DueDate = null) { LabelIds }`.
- **Schema.** `TaskConfiguration.cs`: clustered PK `Id` (sequential GUID), query filter `SpaceId == ScopedSpaceId` (`:50`), `PositionKey` `varchar(128)` BIN2 (`:56-60`), **unique** `IX_Task_SpaceId_ProjectId_StatusId_PositionKey` (`:87-88`, no INCLUDE — 50 key lookups per page; acceptable, measure). RLS predicate `fn_SpaceIsolationPredicate` (`InitialSchema.cs:222-229`) on `SESSION_CONTEXT('SpaceId')`, set read-only in `SpaceSessionContext.cs:111`. Space-scoped transaction begun with no isolation level (`MembershipActiveSpaceResolver.BeginAsync:187`) → READ COMMITTED. **No `READ_COMMITTED_SNAPSHOT`/`ALLOW_SNAPSHOT_ISOLATION` anywhere** in migrations, infra or tests. `MAXDOP = 1` at `20260925132825_ActiveSpaceResolution.cs:58-60`.
- **Counts and locks.** `ListProjectsHandler.cs:27-33` (`ListAsync` then `TaskCountsAsync`, two statements); `ProjectStore.TaskCountsAsync` (`:79-85`, grouped over the whole Space); `ProjectStore.DeleteAsync` (`:92-117`: Project row `UPDLOCK, HOLDLOCK, INDEX(1)` → Task range `COUNT … WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId` → `ExecuteDeleteAsync`, cascade). `BoundCounter.CountAsync` (`:48-67`): `ProjectsPerSpace` = `SELECT COUNT(*) FROM dbo.[Project] WITH (UPDLOCK, HOLDLOCK)` (RLS narrows rows, not the range lock); `TasksPerProject` seeks the Space-led index.
- **Moves.** `TaskStore.MoveAsync` (`:300-360`): owner read → project range lock → row lock → `NeighbourKeyAsync` (not in destination → `PositionUnavailable` `:337`) → `SuccessorAsync` → `Placed()` (`:365-375`) wraps `PositionKey.Between` and maps "no key fits" to the **same** `PositionUnavailable` (`:351`). `409 urn:yello:problem:task-position-unavailable` (`ProblemTypes.cs:274`, `TaskMoveEndpoints.cs:72`); copy "That position has changed. Choose it again." (`ClientCopy.resx:658-661`, `RefusalCopy.cs:101,170`). `PositionKey.Longest = 128`; 718 same-gap placements reach it.
- **Client.** `BoardView.razor` (`ColumnState(statusId, name, total, List<TaskResponse> tasks)` `:502-511`; skeleton `:63-75` with `aria-busy`; `LoadAsync` `:305-363` → `Say("BoardLoaded")` `:335`; `RereadBoardAsync` `:371-399` replaces `_columns` wholesale; `Add` `:468-480`; `Remove` `:486-497`). `BoardView.razor.cs` (`Deleted` `:502-535`; `Locate` `:1026-1039` linear; `Place`/`PlaceAfter`/`Relocate`/`InsertByKey` `:912-1024`; `AnnounceMove` `:327-354`; `AfterRefusalAsync` `:787-798` sets `_rereadBoard`; `MoveColumns` `:212`; replicas `:1102-1182`). `BoardView.Keys.cs` (`ActiveStop` `:57-80`; `Traverse` `:195-228` over loaded counts; `PickUp` `:257`; `Step` `:308-341`; `DropCarriedAsync` `:347`). `BoardTraversal.cs` (`Traverse` `:51`, `Carry` `:94`, pure). `BoardColumn.razor` (`Positioned` `:224` = loaded index + 1; `SetSize="@Total"`; column `<section>` focus target `:256-280`). `TaskCard.razor` (`<li aria-posinset aria-setsize>` `:44-46`; roving `tabindex` `:234-235`; focus requests `:245-285`). `MovePicker.razor` (`Positions` `:108-120` from loaded Tasks; "Bottom" on the last). `board-keys.js` (never changes what Blazor renders; `reveal()` `:68-74`). `board-drag.js` (`#step` `:339-357`, `#follow` `:364-372`, `#autoScroll` `:374-402`, `#hitTest` `:404-435` reads every card's rect per frame). No Board API client class: `BoardView` uses `HttpClient` directly; writes go through `SpaceWrites`.
- **`BoardView` is at S1200's cap of 30 dependencies** (2.8 Debug Log). Adding paging state may push it over: prefer new partial files (`BoardView.Paging.cs`) and plain fields/tuples over new injected services or types the analyzer counts — 2.8 used `(int Column, int? Index)` tuples for this reason.
- **Tests.** `TaskWire.SeedTasksAsync` (`tests/Yello.Tests.Slices/Tasks/TaskWire.cs:205-226`): one set-based INSERT of `count` Tasks into one Status, `NEWID()` ids, digit keys `prefix + 00001…`. `ProjectEndpointTests.Delete.cs:361` (5,000 delete, prints 330 ms, asserts no time). `BoardEndpointTests.cs` (`:54-276`), `ViewBoardHandlerTests.cs` (in-memory `BoardWorld : IBoardReader` `:106` — update for the new port). `DurationIndistinguishability` (`Yello.Tests.Shared`, 21 samples, median, 20%; `CompareMeasuredAsync` `:172`); `RunningHost.ServerDurationOfAsync` (`:302`). **No percentile helper exists.** Serial collections: `BoundaryDurationCollection`, `PasswordDurationCollection`, `PinnedPoolCollection`, `TimingCollection` (Isolation), `PooledConnectionCollection`, `TouchTimingCollection` (Client). Client: `FakeYelloApi` serves the whole Board (`:618`, `:1125`), `FakeColumn.Total` is independent of its Tasks; `DragHarness.Seed/Many`, `KeyboardHarness` (`WaitForKeysAsync`, `PressAndFocusAsync`, `RecordAnnouncements`/`WaitUntilSaidAsync`), `RealHostCollection`.
- **CI.** `.github/workflows/ci.yml` (281 lines): `test` job (30 min) restores, builds Release, installs Playwright Chromium, builds the EF bundle, runs Architecture (`:77`) then `dotnet test Yello.slnx -c Release --no-build` (`:80`, every suite), then `build-images`, `migrate`, `promote`, `client`. No Nightly/Weekly. Every test carries `Trait("Suite", "<project>")` — Suite is the project, not a tier.
- **Infra.** `infra/modules/containers.bicep:131-133` `minReplicas` param (default 0, `infra/main.bicep:62`), `maxReplicas: 1`; `infra/modules/sql.bicep:64-75` serverless `GP_S_Gen5`, free limit, `autoPauseDelay: 15`, `minCapacity: 0.5`. `docs/operations.md` §7 (`:100-102`) holds the provisional "NFR-5 is measured warm, and the cold path is exempt until story 2.9 measures it"; §2 cost table (`:35-47`): `minReplicas 0` ≈ £4.50 of £30, `1` ≈ £17–20 of £30; P6 (`:220`, state "not yet" `:236`) is the one deploy-target cold observation.

### Open questions (decide in Task 1; each reversible; record as "Qn - (x)")

1. **Wire shape for paging.** Options: (a) the Board `GET` returns each column's first page plus `NextAfter` (appended to `BoardColumnResponse`), and a new `GET …/projects/{projectId}/board/columns/{statusId}?after={key}` returns `BoardColumnPageResponse(Guid StatusId, int Total, IReadOnlyList<TaskResponse> Tasks, string? NextAfter)`; (b) one route with optional `statusId`/`after` query parameters on the Board route; (c) an opaque (base64) cursor. **Recommend (a)** — one bounded seek per request, the Board route's shape unchanged for existing consumers (AD-19 append), the cursor is the readable key FR-35 already exposes, and a distinct route gets its own isolation rows cleanly. No client-supplied page size (the server owns it; a `limit` parameter is an unbounded-read lever). Cost: one more route on both isolation surfaces; Epic 8's API docs list it.
2. **Does a page carry `Total`, and does the client adopt it?** (a) The page carries the `COUNT` (same predicate); the client adopts it for that column only when no write touching the column is unsettled in `_replicas`; (b) carry it, client ignores it; (c) omit it. **Recommend (a)** — keeps the chip, `aria-setsize` and announcements true under other Sessions' changes without fighting the optimistic replica. Cost: one `COUNT` seek per page (cheap on the index).
3. **What a re-read after `409` does to loaded depth.** (a) Re-read reloads every column's first page, then fetches further pages only in the column holding the focused/active/held Task until that identity is loaded again (at most `depth/50` seeks), other columns reset to one page; (b) reload every column to its previous loaded count; (c) reset everything to first pages and send focus to the column. **Recommend (a)** — focus identity is the invariant (AC7), depth elsewhere is cheap to regain. Cost: a deep re-read in one column is several sequential seeks; deep columns elsewhere collapse to 50.
4. **Another Session's change seen in a page.** (a) A Task id already rendered is moved to the page's position (newer read wins) unless an own write for it is unsettled; a Task moved *above* the cursor by another Session is not seen until the next Board read (accepted until Epic 7's push); (b) skip ids already rendered. **Recommend (a)** — never twice (AC5), and (b) leaves a card at a stale position. Cost: moving a card element is a reorder of a keyed element for the *same* Task — not a recycle; focus stays on it.
5. **The pointer/touch/browse-mode route to the next page.** (a) A "Show more" button at a partly loaded column's foot, `tabindex="-1"` inside the roving composite (keyboard users have `↓`; screen readers reach it in browse mode; it adds no tab stops), visible label "Show more Tasks" with the Status in its accessible name; (b) the same button as its own tab stop; (c) load on scroll near the end — **rejected**: that is infinite scroll (UX-DR26). **Recommend (a)**, ledgered to UX for copy and placement (no document specifies the Board's load trigger; spine `:257` says "an explicit page boundary"). Cost: a pointer user scrolls to the foot and clicks once per 50.
6. **Does the drag reach beyond the loaded page?** (a) No — drops target loaded cards only; the "Show more" control is the pointer route deeper, and auto-scroll does not load; (b) auto-scroll at the column's foot loads the next page mid-drag. **Recommend (a)** — (b) is infinite scroll by another name and multiplies the per-frame cost. Ledger for UX.
7. **Focus for a Task with no loaded card.** (a) The column that holds it (the `<section>` target 2.5 built), for both deletion and panel close; (b) the column's "Show more" control. **Recommend (a)** — `EXPERIENCE.md:403`'s "nearest surviving container"; the control may not exist (fully loaded column).
8. **"Board loaded." against AC6.** `EXPERIENCE.md:279`/UX-DR35 announce cold-load completion; `EXPERIENCE.md:390`/UX-DR38 and this story's AC6 (`status: final`) say nothing is announced on loading (`deferred-work.md:498`, UX). Options: (a) keep "Board loaded." once on the cold load only and announce nothing for page loads and re-reads — reading AC6's "loading or windowing" as the window changing, and UX-DR35 as the one-time cold load; (b) remove "Board loaded." entirely. **Recommend (a)** — it reconciles both final sources without deleting shipped behaviour; record the reading on `:498` for UX. Cost: if UX reads AC6 literally, (b) is a one-line removal plus the `BoardTests`/`KeyboardTests`/`LabelTests` assertions that wait on it (`BoardLoaded` appears in 7 files).
9. **AR-40b: cold-start position.** (a) Declare NFR-5 **measured warm** and exempt the cold path, stated in `docs/operations.md` §7 and the spine's `:425` row; keep `minReplicas: 0`; P-11 characterises the local cold path and records it; the one deploy-target observation (operations P6) is Lee's; (b) pin `minReplicas: 1` (£17–20 of the £30 ceiling). **Recommend (a)** — pinning a replica removes only the container's cold start; the database still auto-pauses after 15 idle minutes (AD-10 forbids a keep-alive) and its resume (up to ~a minute) dominates, so (b) spends ~£13/month without making the cold path meet 300 ms. Cost: a first request after idle is slow and exempt; documented.
10. **Snapshot for count, page and Labels.** (a) Turn `READ_COMMITTED_SNAPSHOT` on everywhere (Task 8) and accept **statement-level** snapshots: a column's `COUNT`, its page and its Labels may disagree by the writes that commit between them, bounded to concurrent change and corrected by the next read; the client's own writes move totals with cards; (b) additionally run Space-scoped `GET`s at `SNAPSHOT` isolation (`ALLOW_SNAPSHOT_ISOLATION ON`, isolation chosen per request at `BeginAsync`) so all statements share one snapshot. **Recommend (a)** — (b) puts presence writes (`SpacePresenceRegister`, inside every resolution transaction) under snapshot isolation, where a concurrent refresh of the same row fails with update-conflict 3960; that is a new failure mode on every read for a cosmetic gain. Cost: `:495`/`:547` close as "decided: per statement, bounded"; revisit with Epic 7's push.
11. **Full gap.** (a) A distinct `409` type (`task-position-full`) and copy "There is no room at that position. Choose another position."; no rebalancing pass now (718 placements into one gap), with a PM ledger entry for a deliberate maintenance pass if Weekly key lengths show growth; (b) same type, different copy chosen client-side — impossible, the client cannot tell them apart; (c) build rebalancing now. **Recommend (a).** Cost: one problem type and one string; the copy goes to UX for confirmation.
12. **Render cost at 5,000 loaded cards.** (a) `ShouldRender` on `TaskCard`/`BoardColumn` comparing drawn inputs, an id index for `Locate`, measured before and after; (b) leave as is if the measurement passes. **Recommend (a) only if measurement shows the full re-render** (expected — `TaskResponse` with a list is a complex parameter, so every render re-renders all cards). If neither holds the budget, escalate AD-29 (AC14).
13. **Tier selection and workflow layout.** `TESTING-CONVENTIONS.md` allows exactly four trait names (`Suite`, `Priority`, `Requirement`, `Assumption`; gated by `TestingConventionTests`), and `Suite` is the project name. Options: (a) add a fifth trait name `Tier` with values `Nightly`/`Weekly` (edit the conventions and its gate in the same change), PR run excludes both with `--filter-not-trait`; separate `nightly.yml`/`weekly.yml` workflows; (b) select by class-name convention (`*NightlyTests`) with `--filter-not-class`; (c) jobs in `ci.yml` with `schedule` triggers. **Recommend (a)** — explicit, gate-checked, and keeps `ci.yml`'s ordered shape (`DeploymentShapeTests`) untouched apart from the exclusion. Cost: one convention amendment; scheduled workflows run only from `main` and cannot be exercised before a push — verify locally by running the same `dotnet test` filter and `actionlint`.
14. **Projects-per-Space range lock across Spaces.** (a) Take a transaction-owned `sp_getapplock` named for the Space (`Yello/ProjectsPerSpace/{spaceId}`, exclusive) before the count, drop the `UPDLOCK, HOLDLOCK` range hints from that count, and have Project deletion take the same applock; (b) explicit `WHERE SpaceId = @s` with a seek hint (narrows, but next-key locking still reaches the adjacent Space — `:476`); (c) measure and accept. **Recommend (a)** — it removes the cross-tenant wait and the timing signal entirely. Cost: BND/RACE cases for Projects re-proved; the applock is released at commit with the transaction.
15. **Large Project deletion escalation.** (a) If Task 9 observes an `OBJECT X` on `Task`/`TaskLabel`, set `LOCK_ESCALATION = DISABLE` on both tables in a migration (≈10,000 row/key locks for one deletion is a few MB of lock memory on a 2 GB engine), keeping the one atomic `DELETE`; (b) per-Space applock around deletions (does not stop escalation from blocking other Spaces); (c) accept. **Recommend (a) if escalation is observed, else close as measured.** Under RCSI (Q10) readers in other Spaces are not blocked by an escalated `X`; writers would be. Cost: a schema-level setting the bundle-equivalence test must hold.
16. **Load harness.** The test design names "k6 or equivalent" (`test-design-qa.md:513`); a new tool or NuGet package needs Lee's approval (CLAUDE.md). (a) A .NET harness in Slices over `RunningHost`/`HttpClient`, p95 from the Host's request-finished figures, no new dependency; (b) k6 in CI (a new tool — needs approval); (c) NBomber (a new package — needs approval). **Recommend (a).** Cost: concurrency is bounded by one in-process Host and the test process; the report says so.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

| Entry | Disposition |
|---|---|
| `:130` | CLOSED — X-6 under paging (Task 12). X-3 stays with Lee (`:589`). |
| `:219` | Fold: after the Weekly load, record `sys.dm_db_index_physical_stats` fragmentation of the `Task` clustered index (sequential ids vs seeded `NEWID()` — measure product-created rows). Close as measured; the clock-step/two-instance overlap goes with `:421` to **7.3**. |
| `:257` | CLOSED as by-design, with the Weekly p95 (the transaction is inside every measured request). |
| `:298` | RE-OWN to **7.5** (presence — "See who else is here"), where the presence bound becomes user-visible; a range lock on every request is a throughput decision for that story. |
| `:313` | CLOSED by RCSI (Task 8's deterministic proof: the count no longer waits on an uncommitted presence insert). |
| `:315` | RE-OWN to **8.3** (rate limiting; batching the purge and a Space-scoped limit belong together). |
| `:336`, `:382` | CLOSED — measured inside the Weekly p95 (every load request is cookie-authenticated; add a Token-authenticated read class for `:382`). |
| `:348`, `:390`, `:450` | RE-OWN to **8.3** (limiter ordering, per-guess cost and the log line under a refusal flood are rate-limit design). |
| `:419` | CLOSED — Nightly and Weekly stages with their first suites (Task 11). |
| `:451` | Fold into P-11 (local time-to-first-response includes the key-ring load); deploy-target half RE-OWN to **Lee** (operations P6). |
| `:459` | RE-OWN to **8.3** (idempotency at API scale); record row growth seen in the Weekly load. |
| `:471`/`:476-477` | CLOSED by Q14 (Task 9). |
| `:478` | RE-OWN to **8.3** ("without duplicating writes" — the repeated lookup after the count). |
| `:488` | Fold: Weekly records both 404 paths' local p95; setting `SpaceBoundary:NotAvailableFloorMilliseconds` from the deploy target RE-OWN to **Lee** (operations P6) — no Azure here. |
| `:495`, `:547` | CLOSED by Q10 (decided: per statement, bounded; RCSI). |
| `:523` | Measured (5,000 Tasks × 3 Labels inside P-2/P-1); the bound decision stays **PM**. |
| `:541` | CLOSED by Task 9 / Q15. |
| `:548` | CLOSED — listing p95 in the Weekly load and no longer blocked under RCSI; if over budget, a new entry with the measured figure. |
| `:551` | CLOSED by Task 6. |
| `:557` | CLOSED — picker and keyboard reach every page (Task 5); drag-only-loaded accepted (Q6, UX entry); key lengths recorded; rebalancing → **PM** (new entry if needed). |
| `:562` | CLOSED by Task 10 / Q11 (copy to UX for confirmation). |
| `:578` | CLOSED by Task 5's rect cache and still-frame skip. |
| `:421`, `:498`, `:589` | Touched only: note 2.9's reading (Q8) on `:498`. |

### Architecture compliance (must)

- **AD-29 / AR-22.** Keyset seek on `PositionKey` per `(ProjectId, StatusId)`, never `OFFSET`/`Skip()`; separate `COUNT` over the same predicate; append, never recycle; no `Virtualize`. Page size 50 is an assumption — one constant.
- **AD-15 / AR-21.** BIN2 comparison; `varchar(128)` parameter; a move still writes one key between two concrete neighbours and never renumbers. Position stays read-only over the API (FR-35).
- **AD-2.** `MAXDOP = 1` is not relaxed to buy latency (R3+R5).
- **AD-10.** No timer touches the database — not a warm-up loop, not a keep-alive, not in tests; the load harness runs a fixed number of iterations.
- **AD-3 / AR-10.** A foreign Project or Status on the page route answers the boundary's `404 not-available`, byte-identical; the `NotAvailableFloor` filter applies to it as to every API-group route.
- **AD-19.** Contracts: append only.
- **RLS + query filter** stay the only Space narrowing; no explicit `WHERE SpaceId` in readers (the index's leading `SpaceId` is supplied by the policy predicate).
- **Project references** follow `AllowedReferenceEdges.cs`; Client sees only Contracts.

### Library / framework

- No new NuGet package or project (Lee's approval required — CLAUDE.md). Versions per `Directory.Packages.props`: EF Core 10.0.11, Microsoft.Data.SqlClient 7.0.2, xunit.v3 4.0.0, Microsoft.Playwright 1.62.0, Testcontainers 4.6.0. Local tools: `dotnet-ef` 10.0.11 for the migration(s).
- EF Core 10: `string.Compare(a, b) > 0` translates to `a > b`; check the parameter's inferred type in the logged SQL (`varchar(128)`). `Take(n)` emits `TOP`; never `Skip`.
- SQL Server: `ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON` needs no other active connection (`WITH ROLLBACK IMMEDIATE`) and cannot run in a user transaction (`suppressTransaction: true`); Azure SQL defaults it on. Lock escalation triggers at ~5,000 locks on one object in one statement; `LOCK_ESCALATION = DISABLE` is a table option.
- Blazor WASM (.NET 10): a component re-renders when any complex-type parameter is set; `ShouldRender` is the lever. Keep `@key` by Task id.
- xunit v3 on MTP: `--filter-trait`/`--filter-not-trait` after `--`; zero selected tests exits 8.

### Testing standards (from `tests/TESTING-CONVENTIONS.md` and CLAUDE.md)

- **Memory budget.** Every 5,000-Task case runs on the Slices (or Client) suite's **one** 2 GB engine, in a database of its own class — no new container class, no private engine (`ContainerTopologyGateTests`: budget 5, `PrivateEngines` empty). Timing cases live in a `DisableParallelization` collection and never run beside `BoundaryDurationCollection`/`TimingCollection`/`PasswordDurationCollection`. Full runs always with `--max-parallel-test-modules 1`; never two `dotnet test` processes at once; iterate one project with `--filter`.
- **Determinism first.** Depth independence and "reads do not wait" are proved by logical reads and by a held transaction ("B completes while A is held"), not by time. Races wait on `LCK_M_%` (`TaskWire.WaitUntilBlockedOnALockAsync`), never `Task.Delay`.
- **Measured timings** are server-side (the Host's request-finished figure via `RunningHost.ServerDurationOfAsync`), ≥ 100 samples for a p95, warm-up first, and the report states the condition (local engine, warm — R18: never production evidence).
- **Seeding.** Through the product where an endpoint exists; `TaskWire.SeedTasksAsync`/`WireDatabase` raw SQL for bulk (5,000 creations over HTTP would test the bound lock, not the read). Clean up by dropping the class database, never `DELETE`.
- **Plants** for every absence assertion; one at a time; restore in a `finally`; never `git checkout --`/`restore`/`reset`/`stash`.
- **Traits:** `Suite`, `Priority`, `Requirement` (cite `AR-22` with `AD-29`, `AR-40b`, `NFR-5`, …), `Assumption` (page size 50 cites the spine `:259`); plus the tier trait if Q13 (a).
- **Browser:** Debug build of `Yello.Client` first; `FakeYelloApi` routes are Contracts types and paths; hold/assert/release/assert; poll `location.pathname`, never Playwright's URL wait; rerun a full-run-only Client failure alone.
- Three migration-bundle skips are expected unless `YELLO_MIGRATION_BUNDLE` is set.

### Previous story intelligence (2.8, 2.7, 2.6)

- 2.8 handed over: keep `aria-posinset`/`aria-setsize` true under paging (Q8 (a)); focus is tracked by Task id through `FocusCardTitle`/`_focusCardRequests`/`FocusColumnRequest` — reuse those, never focus an element directly; JS never writes `tabindex`; `LiveAnnouncement` has two alternating regions (`Say` joins sentences said before one render); nothing is announced on traversal, load or re-render. Carry and drag exclude each other. One write path: `MoveAsync(..., MoveOrigin.*)`.
- 2.8's review patched focus-run resets on foreign focus changes (`BoardView.Keys.cs:83`) — a page append must not count as a foreign focus change that resets the sticky origin.
- 2.7: drops hit-test rendered cards; the lifted card is fixed to the view; `TouchTimingCollection` for timing; the drag is movers-only.
- 2.6: `{statusId, afterTaskId}` was chosen because ordinals go stale under paging; the Move picker lists loaded Tasks; 718 same-gap placements reach 128 characters; every key placer meets on the Project's Task range (range before row).
- 2.5: Project deletion is one atomic statement by decision (AC3); 330 ms for 5,000 Tasks without Labels on the Slices engine.
- Repeated lesson: a flaky timing case in the full run was a missing `DisableParallelization` collection (2.2, 2.7) — put every new timing case in one from the start.

### Git intelligence

Last commits: `4ccba9f` (2.8 review: Space on the Move picker's list), `3d4c511` (2.8: keyboard Board, 31 files), `b87c082` (2.8 story), `f2e4f72` (2.7 review: lifted card clipping), `211c388` (2.7: drag). 2.8 touched only the Client and tests (no Host/Application/Domain/Infrastructure/Contracts/migrations, no `board-drag.js`). 2.9 is the first story since 2.6 to change Contracts, Infrastructure and migrations together.

### Project Structure Notes

- New slice folder `Yello.Application/Boards/ViewBoardColumn/` beside `ViewBoard/` (vertical slices, `Application/<Area>/<Slice>/`).
- New migration(s) in `Yello.Infrastructure/Persistence/Migrations` (RCSI; `LOCK_ESCALATION` if Q15 (a) applies).
- Client: prefer `BoardView.Paging.cs` (partial) for paging state to stay under S1200.
- Tests: Slices `Boards/` for paging and scale; Isolation rows in `IsolationCases.cs`; Client `BoardPagingTests.cs` (in `ClientSurfaceCollection`) plus a Nightly real-Host case; Architecture gates (no `Virtualize`, tier trait, workflow shape).
- Workflows: `.github/workflows/nightly.yml`, `weekly.yml` (Q13).
- Docs: `docs/operations.md` §7; `ARCHITECTURE-SPINE.md:425` (AR-40b closed); `tests/TESTING-CONVENTIONS.md:711-714`.

### References

- `_bmad-output/planning-artifacts/epics.md:1295-1336` (story 2.9), `:362` (trace row), `:210` (AR-22), `:228` (AR-40), `:265-267`, `:279`, `:282` (UX-DR24/25/26/35/38), `:431`, `:437` (Epic 2 notes)
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md:516-524` (FR-28), `:605` (FR-35), `:716-720` (NFR-5), `:745-751` (NFR-8), `:753-757` (NFR-9), `:778` (£30 ceiling), `:884`; `addendum.md:146`, `:153`, `:165`
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md:249-259` (AD-29), `:162-168` (AD-15), `:83` (AD-2), `:132-136` (AD-10), `:156-160` (AD-14), `:293-294` (stack), `:425` (AR-40b deferral), `:427` (AR-40c)
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:38`, `:199-201`, `:254`, `:279`, `:316`, `:334`, `:386`, `:390`, `:401-404`, `:423`; `DESIGN.md:127-137`, `:421-427`, `:508-521`
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md:1110-1117` (m3 — 2.9 as a split candidate, noted not pressed), `:1250-1259` (issue 6, AR-40b)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-13.md` — no bearing on 2.9 (FR-1 registration fields).
- `_bmad-output/specs/spec-yello/SPEC.md:146-148` (CAP-28), `:283`; `quality-budgets.md:44-48`, `:74`
- `_bmad-output/test-artifacts/test-design-architecture.md:114-122` (B4, B5), `:135-146`, `:174`, `:180-182`, `:213-238` (R3, R4, R13, R16, R18), `:441-471` (mitigations), `:630`; `test-design-qa.md:79`, `:112-114`, `:166-167`, `:201-203`, `:267` (I-3), `:311` (X-5), `:356` (X-6), `:370-371` (P-1, P-2), `:388` (P-11), `:402-435` (tiers), `:513` (tools)
- `docs/operations.md:35-47`, `:90-102`, `:220`, `:236`, `:262`
- `tests/TESTING-CONVENTIONS.md:46-72` (traits), `:88-175`, `:183-320` (browser), `:392-424` (B3), `:448-511` (containers), `:520-562` (runner), `:635-648` (seeding), `:657-679`, `:681-714` (CI)
- Prior stories: `2-8-operate-the-whole-board-from-the-keyboard.md` (Q1-Q14, Completion Notes `:373-386`, File List `:390-427`), `2-7-…md:13`, `:199`, `:288`, `2-6-…md:13-14`, `:313-322`, `:441`, `:480-487`
- `_bmad-output/implementation-artifacts/deferred-work.md` — every line cited in the header.

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-2-9-the-board-at-five-thousand-tasks.md` (scaffold constants, seed constraints checked, assumed markup and copy, the gates that go red by design).

| File | Cases | ACs | State |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Boards/BoardPagingTests.cs` | 18 entries (23 cases) PG-01..PG-18 | AC1, AC2 (deterministic half), AC4, AC5, AC9, AC10, AC13, AC17, AC18 | skipped `red` |
| `tests/Yello.Tests.Slices/Boards/BoardPageWire.cs` | harness (page route, walk, Labels at scale, page-seek logical reads and plan) | - | active |
| `tests/Yello.Tests.Slices/Boards/BoardScaleTimingTests.cs` | 1 (P-2) + `BoardScaleCollection` | AC2 | skipped `red`, `Tier=Nightly` |
| `tests/Yello.Tests.Slices/Boards/EnvelopeLoadTests.cs` | 2 (P-1 load, P-11 cold start) | AC16, AC8 | skipped `red`, `Tier=Weekly` |
| `tests/Yello.Tests.Slices/Boards/PercentileTests.cs` + `tests/Yello.Tests.Shared/Percentile.cs` | 4 entries (8 cases) | Task 3 | **green today** |
| `tests/Yello.Tests.Slices/Deployment/DatabaseIdentityTests.cs` | +1 (SCH-901, RCSI on) | AC13 | skipped `red` |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` | 4 rows x 2 surfaces (`ISO-DIRECT-BOARD-COLUMN`, `ISO-BOARD-COLUMN-FOREIGN-ID-IN-OWN-SPACE`, `ISO-BOARD-COLUMN-AGGREGATE`, `ISO-BOARD-AGGREGATE-PAGED`) | AC9, AC4, AC5 | pending on 2.9 (GATE-003: live before `review`) |
| `tests/Yello.Tests.Architecture/BoardPagingGateTests.cs` | 7 | AC3, AC9, AC18 | **2 green today** (no `Virtualize`/`ItemsProvider` + its plant); 5 skipped |
| `tests/Yello.Tests.Architecture/DeploymentShapeTests.Tiers.cs` | 4 entries (5 cases) | AC15 | **2 green today** (every `Tier` is Nightly/Weekly + its plant); 3 skipped |
| `tests/Yello.Tests.Client/BoardPagingTests.cs` | 13 (X-5, AC3 identity, X-6, AC6, AC10 x2, AC11 x4, AC12 x2, AC14 still-pointer rects) | AC3-AC7, AC10-AC12, AC14 | skipped `red` |
| `tests/Yello.Tests.Client/BoardRenderCostTests.cs` | 1 (90 ms keyboard step at 5,000 loaded) | AC14 | skipped `red`, `Tier=Nightly` |
| `tests/Yello.Tests.Client/RealHostBoardScaleTests.cs` | 1 (real-Host `↓` traversal of 5,000) | AC5, AC7, AC11 | skipped `red`, `Tier=Nightly` |
| `FakeYelloApi.cs`, `RealHostFixture.cs`, `WireDatabase.cs` | harness: opt-in `PageSize` (null = unchanged bytes), page route and hold; `Database`; `SeedTasksAsync` | - | active |

Green today (standing gates): `PercentileTests` (8), `Nothing_in_the_client_virtualises_a_list` and `The_virtualisation_scan_detects_a_plant`, `Every_Tier_trait_is_Nightly_or_Weekly` and `The_Tier_trait_scan_reads_a_planted_value`.

Activation order, riskiest assumption first:
1. `BoardRenderCostTests` - measure first (Task 7): append-only at 5,000 loaded cards within 90 ms is the one assumption whose failure stops the story (AC14 -> AD-29 amendment). Take the first figure with the fake's `PageSize` left `null` (all 5,000 at once, paging's end state) before paging exists; activate the scaffold as written once "Show more" exists.
2. PG-11, PG-12 - keyset seek with a `varchar(128)` cursor on the BIN2 index under RLS, logical reads independent of depth (plants: `Skip()`/`OFFSET`; `nvarchar` cursor).
3. PG-01, PG-02 (amend `BoardEndpointTests`' column pin by exactly `nextAfter`), PG-05, PG-03, PG-04, PG-06..PG-10, PG-13; the ARCH contract gates.
4. Isolation rows one at a time (swap the scaffold `ColumnRoute`/`ColumnOf`/`PageSize` for the contract members; fold `ISO-BOARD-AGGREGATE`'s `tasks == total` into `ISO-BOARD-AGGREGATE-PAGED`).
5. SCH-901 with the RCSI migration, then PG-14, PG-15; re-run every race class.
6. PG-16, PG-17, PG-18.
7. Browser: X-5, the AC3 identity case, X-6, AC6, AC10, AC11, AC12, the still-pointer rects case.
8. ARCH-901/902 with the workflows (amend `The_revision_is_promoted_…`'s `Assert.Single` over `dotnet test Yello.slnx`, or run tiers per project); run the Nightly/Weekly cases once by their filter and record the numbers.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), bmad-dev-story, run unattended by the story-cycle orchestrator.

### Debug Log References

**Baseline** (orchestrator, ATDD commit `428dcc5`): 1340 total, 1293 passed, 0 failed, 47 skipped. Not re-run before work began; the build was 0/0.

**AC14 measured first** (activation order 1). Fake with `PageSize = null` (all 5,000 cards at once - paging's end state), Debug client, local Chromium: keyboard step p95 **229.8 ms**, pick-up 219.7 ms, carried step 230.5 ms, JS heap 16.3 MB - every card re-rendered on every Board render (complex parameters). With `TaskCard.ShouldRender` (Q12 (a)): step p95 **56.1 ms**, pick-up 64.7, carried 58.5, heap 20.7 MB. Scaffold restored (hash matched). Activated as written once "Show more" existed (99 activations to 5,000): step p95 **75.6 ms**, then **56.6 ms** after the page-append index fix (below); pick-up 63.2 ms; carried step 57.6 ms; heap 23.4 MB. Inside the 90 ms budget append-only - no AD-29 amendment needed. Append of a page at 4,950 loaded: p95 3.7 s, then 3.1 s after fixing `Locate` rebuilding its index per page Task (deferred, owner Lee).

**Server seek (PG-11, PG-12).** Logical reads of the product's page statement: page 2 / 50 / 99 / 100 = 110 / 110 / 111 / 108 (and 110 / 110 / 110 / 110 on later runs). Plan seeks `IX_Task_SpaceId_ProjectId_StatusId_PositionKey`, `@after varchar(128)`, no conversion. Board first paint is a loop of one `TOP (51)` seek per effective Status (3-5 statements), chosen over one statement (`ROW_NUMBER()` reads the Project).

**Scaffold corrections** (each kept, corrected, recorded):
1. PG-11 - the statement's first execution read 128 pages (compilation) against 108-111 later at any depth: a warm-up read added; the tolerance 2 -> 5 (the last page lacks the look-ahead row: one clustered key lookup fewer; plus a leaf boundary). OFFSET grows it to thousands.
2. PG-15 - read as the Owner, already present, so the presence count never ran: with RCSI planted off the case still passed. The reader is now a second Member entering the Space; planted off, it fails.
3. PG-16 - `ObjectLocksHeldAsync` called `OBJECT_NAME` on every lock row's `bigint` hobt id before the `OBJECT` filter (arithmetic overflow); now names object locks only. `WaitsAsync` added to the failure message (wait type, resource, statement).
4. ISO-BOARD-COLUMN-FOREIGN-ID-IN-OWN-SPACE - the scaffold's plant (drop the Task's two layers) cannot fail on this route: the Project and Status checks stand in front. The plant is the handler's two checks skipped (caught on both surfaces).
5. `ViewBoardHandlerTests` - 2.2's "a Task outside the effective set fails loudly" cannot hold once each column reads only its Status; replaced by `First_paint_is_one_bounded_seek_per_column_of_the_effective_set` (+3 unit cases for the cursor and the page handler).
6. Client `A_Viewer_pages_a_column_too` asserted nothing (S2699): now asserts the control is gone once fully loaded.
7. Client `A_page_holding_a_Task_already_drawn_...` - the key-by-index plant was not caught by any append-only case (appending at the end never shifts an index); the case now tags Card 11's element and asserts it still draws Card 11 after the reorder.
8. `BoardPageWire`, `IsolationCases` - scaffold constants swapped for `BoardRoutes.Column`/`ColumnOf`, `BoardPage.Size`, `ProblemTypes.*` (Isolation keeps a local `PageSize = 50`: it references Host and Contracts only). `FakeYelloApi` keeps anonymous bodies: `FakeTask` serialises to `TaskResponse`'s members, so the bytes are the contract's.
9. Gates amended by exactly 2.9's addition: `BoardEndpointTests` column pin +`nextAfter`; ARCH-503 route list +`GET …/board/columns/{statusId:guid}`; `RefusalBoundaryGateTests.Tasks` vacuity guard +`BoardColumnPageResponse`; MOV-11 now expects `task-position-full`. Each went red first.

**RCSI fallout (Task 8's "re-run every race class").** First full Slices run after the migration: 603, 2 failed. (a) `A_Labels_edit_queued_behind_a_deletion_answers_404_...` - a **real regression**: the edit's unhinted read saw the committed row past an uncommitted deletion and answered `400 invalid-task-labels`, not `404`; a title edit would have reached a 0-row UPDATE (`500`). (b) `A_deletion_racing_an_application_...` - the edit's current-set read no longer waited. Fix: `READCOMMITTEDLOCK` on `TaskStore.ApplyAsync`'s first read and `ReplaceAsync`'s current-set read (shared locks released at statement end, so no new lock order). Both green.

**Locks at scale (Task 9).** PG-16 observed escalation: `OBJECT X` on `Task` and `TaskLabel` (5,000 Tasks x 3 Labels) -> Q15 (a), migration `20261028123715_TaskLockEscalationDisabled`. Still red: B's creation waited on `LCK_M_RS_U` at a key of A's - the Tasks-per-Project count's `UPDLOCK, HOLDLOCK` range locked the next key after B's range, which was A's first (adjacent Space ids). Q17 below: every key placer takes the Project's own application lock instead of the range. Test holders that played "the Project's range" moved to the lock (`DeleteWire.HoldProjectRangeAsync`, `HoldProjectsPerSpaceAsync`, `TaskEndpointTests.Bound`, `EditRace.HoldColumnAsync`, `ProjectEndpointTests.Bound`/`Idempotency` - the last waited on `LCK_M_RS_U`, now `LCK_M_X APPLICATION`).

**Drag (AC14 half).** First run read 10 rects over 10 still frames: `#follow` ran on "scrolling" frames while the pointer sat in an edge zone that could not scroll. Auto-scroll now continues only on real scroll events.

**Plants** (one at a time; original saved, restored in a `finally` and touched - Copy-Item had kept an old timestamp once and MSBuild skipped the rebuild, so the binary kept a plant until touched; hashes compared after restore):

| # | Plant | Gate | Caught |
|---|---|---|---|
| 1 | `OFFSET`/`Skip()` page (statement still `> @floor`) | PG-11 (reads 216 / 5,343 / 10,572 / 10,678), PG-12 | yes |
| 2 | `nvarchar(128)` cursor parameter | PG-12 ("converts PositionKey implicitly"); PG-11 green - a mismatched-type range still seeks | yes |
| 3 | Column-page handler skips the Project and effective-set checks | ISO-BOARD-COLUMN-FOREIGN-ID-IN-OWN-SPACE, Session and ApiToken | yes, both surfaces |
| 4 | RCSI off (migration's `ALTER` replaced) | SCH-901, PG-14, PG-15 (after correction 2) | yes |
| 5 | Announcement on append ("Loaded.") | `Nothing_is_announced_when_a_page_loads_...` | yes |
| 6 | Cards keyed by index | `A_page_holding_a_Task_already_drawn_...` (after correction 7) | yes |
| 7 | No column focus for an unloaded Task | `Closing_the_panel_on_a_Task_held_out_...` | yes |
| 8 | Rects read every frame | `A_lifted_card_reads_no_card_rects_...` (2,060 reads over 10 frames) | yes |
| 9 | `aria-posinset` from a page-local index | X-6 (index 50: expected `51/5000`) | yes |
| 10 | Full gap answered `task-position-unavailable` | PG-18 (`Strings differ`, expected `task-position-full`) | yes |
| 11 | Projects-per-Space count with the old range lock, no applock | PG-17 (A's creation no longer queued on the held lock) | yes |
| - | Lock escalation on (before Q15's migration) | PG-16 (`OBJECT X` listed) | yes (the red state) |

**Tier runs** (by filter, `--explicit on`; local engine, warm - never production evidence):
- Nightly P-2 (`BoardScaleTimingTests`, 5,000 Tasks x 3 Labels, 100 interleaved samples): Board read p95 14.9 ms, page 1 12.8 ms, page 100 12.2 ms; depth medians 10.1 vs 10.2 ms (0.6 %).
- Nightly AC14 (`BoardRenderCostTests`): see above.
- Nightly X-5 real Host (`RealHostBoardScaleTests`): 4,999 presses through the real client, Host and column-page route, focus on Seeded 2 … 5,000 in turn, all 5,000 cards drawn once - passed in 2 m 58 s.
- Weekly P-1 (`EnvelopeLoadTests`, 50 Sessions, 100 samples per class): Board 12.8, page-first 10.8, page-last 10.3, listing 7.8, Board on a Token 11.9, 404-boundary 65.3, 404-in-Space 62.6; create 26.4, edit 15.7, move 18.6, delete 13.0 ms p95. Key length in the 5,000-Task column max 7, avg 6. `Task` clustered index 98.8 % fragmented over 248 pages (the seeded `NEWID()` rows).
- Weekly P-11 (cold path, 3 fresh Hosts): listening after 1,180-1,214 ms; first Space-scoped response after 1,684-1,806 ms.

**Bundle.** `artifacts/efbundle-win-x64/efbundle.exe` rebuilt with both migrations; `MigrationBundleEquivalenceTests` 2/2 with `YELLO_MIGRATION_BUNDLE` set (catalogue holds `rcsi|1` and `escalation|Task|DISABLE`).

**Runs.** `dotnet build Yello.slnx` 0 warnings, 0 errors; `dotnet build-server shutdown`. First full run (`dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`): 1360 total, 1350 passed, **2 failed**, 8 skipped, 17 m 52 s - `DesignFoundationGateTests`: three doc comments quoted the new copy ("Show more …") as C# string literals, and `.board-column__more` declared a block-axis target floor without the inline one; fixed (comments name the copy keys; `min-inline-size: var(--target-min)`). The rerun of the whole solution was stopped by the host for low memory (the user's browser held about 9 GB; nothing in the run itself), so - once, as the orchestrator allows - the same binaries were run one project at a time: Architecture 333/333, Merge 0 (empty by design), Revocation 17/17, Isolation 127/127, Client 279 (277 passed, 2 skipped), Slices 604 in two halves by namespace (223/223; 381 with 375 passed, 6 skipped). **Total 1360, 1352 passed, 0 failed, 8 skipped.** The 8 skips: the three migration-bundle cases (`YELLO_MIGRATION_BUNDLE` unset; run separately above, 2/2 + the bundle-backed SLC cases) and the five Explicit Nightly/Weekly cases (run by their tier filter above, all green). No isolation row is pending (`PendingUntil: "2.9"` none).

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-10-28).
- **Implemented**: per-column keyset paging (`IBoardReader.PageAsync`, `BoardPage`, `ViewBoardColumnHandler`, `GET …/board/columns/{statusId}?after=`), `BoardColumnResponse.NextAfter` appended, `BoardColumnPageResponse`, `invalid-board-cursor` and `task-position-full`; the client's contiguous loaded prefix (hold-out rule, de-duplication, cursor advance on a move after the last loaded card, page totals adopted when nothing is unsettled), every route to the next page (`↓`, carried `↓`, "Show more Tasks", the Move picker's "Show more positions"; "Bottom of column" only when fully loaded), AC12 focus destinations, `TaskCard.ShouldRender` and a card index, the drag's cached rects; RCSI and lock-escalation migrations; application locks for Projects-per-Space and for each Project's Tasks; Nightly and Weekly workflows; AR-40b decided; 26 ledger entries settled.
- Q1 - (a) the Board answers each column's first page plus `nextAfter`; `GET …/board/columns/{statusId}?after={key}` answers the next. No client page size. Cost to later stories: one more route on both isolation surfaces; Epic 8's API docs list it.
- Q2 - (a) a page carries the column's `COUNT`; the client adopts it only when no write of its own touching the column is unsettled. Cost: one more seek per page.
- Q3 - (a) a re-read after `409` loads every column's first page, then pages only the column holding the carried, active or focused Task until it is drawn again; if it cannot be, focus goes to its column. Cost: deep columns elsewhere collapse to 50.
- Q4 - (a) a page holding a drawn Task moves that card (newer read wins) unless its own write is unsettled; never twice. Cost: a Task moved above the cursor by another Session is seen on the next Board read (Epic 7's push).
- Q5 - (a) a "Show more Tasks" button at a partly loaded column's foot, `tabindex -1`, accessible name "Show more Tasks in {Status}", every Role. Cost: a pointer user clicks once per 50; copy and placement ledgered to UX.
- Q6 - (a) a drag drops among loaded cards only; auto-scroll does not load. Ledgered to UX.
- Q7 - (a) focus for a Task with no loaded card goes to the column that counts it (deletion and panel close).
- Q8 - (a) "Board loaded." stays once on the cold load; page loads and re-reads say nothing. Reading noted on `:498` for UX; reversal is one line plus its assertions.
- Q9 - (a) NFR-5 measured warm, cold path exempt, `minReplicas: 0` (`docs/operations.md` §7; spine row closed). Cost: a first request after idle is slow and exempt.
- Q10 - (a) RCSI everywhere, statement-level snapshots, disagreement bounded; recorded in `BoardReader`, `ViewBoardHandler` and `ViewBoardColumnHandler`. Snapshot-per-request rejected (presence writes would meet 3960).
- Q11 - (a) `409 task-position-full`, "There is no room at that position. Choose another position."; no rebalancing (PM entry). Copy to UX.
- Q12 - (a) applied: measured the full re-render (229.8 ms), `ShouldRender` brought it to 56 ms.
- Q13 - (a) a fifth trait `Tier` (`Nightly`/`Weekly`), `nightly.yml` and `weekly.yml` run per project, `ci.yml` excludes both tiers by trait; **plus** every tiered test is `Explicit = true` (gate `Every_tiered_test_is_Explicit`) so a plain local `dotnet test Yello.slnx` never starts the 5,000-Task suites. Cost: a tier is run with `--explicit on`; the promotion gate's single `dotnet test Yello.slnx` job is untouched.
- Q14 - (a) `sp_getapplock` `Yello/ProjectsPerSpace/{spaceId}` before the count (no range hints); Project deletion takes it too.
- Q15 - (a) escalation observed, `LOCK_ESCALATION = DISABLE` on `Task` and `TaskLabel`; deletion stays one statement.
- Q16 - (a) a .NET harness over `RunningHost`, no new dependency; one request at a time across 50 Sessions (ledgered: parallel load needs an approved tool).
- Q17 (new, cut-able) - the Project's Task range lock (`UPDLOCK, HOLDLOCK WHERE ProjectId`) became an application lock `Yello/ProjectTasks/{projectId}` taken by every key placer (creation's bound count, Status edit, move, Task deletion, Project deletion), with plain reads after it under RCSI. Reason: the range's next key reached the adjacent Space, so AC17 failed even with escalation disabled. Cost: race tests hold the lock, not the range. **Amended at code review:** the name carries the Space too (`Yello/ProjectTasks/{spaceId}/{projectId}`) - a creation naming another Space's Project took the bound count's lock on that Project and queued on its writers (PG-19).
- Q18 (new, cut-able) - `READCOMMITTEDLOCK` on the two write-path reads that relied on waiting (`TaskStore.ApplyAsync`, `ReplaceAsync`). Reason: RCSI turned a held Task deletion into a `400`/`500` for a racing edit.
- Q19 (new, cut-able) - every tiered test is `Explicit` (see Q13); `TESTING-CONVENTIONS.md` documents running a tier.
- **Corrections to this record (code review, 2026-10-28)** - what the checked tasks above did not do, stated rather than re-checked:
  - Task 7: `BoardColumn` has no `ShouldRender`; only `TaskCard` does. AC14's budget held without it (56.6 ms). The page-append cost it would address is ledgered (owner Lee).
  - Task 1: the baseline run was the orchestrator's figure (1340 / 1293 / 47 skipped), not a run by the dev phase.
  - Task 3: of the four new isolation rows, only `ISO-BOARD-COLUMN-FOREIGN-ID-IN-OWN-SPACE` is plant-proven (Plants 3). The other three are live on both surfaces but have no plant of their own (ledgered, owner story 3.4).
  - Tasks 6 and 13: plant 7 was run against the panel-close case only. `Deleting_a_Task_whose_card_is_not_loaded_focuses_its_column` has no plant of its own.
  - AC14: the drag half records per-frame rect reads (zero on a still pointer), not input-to-update time for a drag frame.
  - AC15: "the PR stage still finishes under 15 minutes" has no CI timing behind it. The PR job's test filter only gained the `Tier` exclusion; no GitHub Actions run was triggered (the story may not trigger one).
  - Q18 has a third site, added at review: `IdempotencyStore.FindAsync` reads with `READCOMMITTEDLOCK` (a retry overlapping a first use's commit replays again instead of answering `409 in flight`).
  - The key-length report gives the p99 as well since review. P-11's "fresh Host" is in-process, and `docs/operations.md` says so.

### File List

- `.github/workflows/ci.yml`
- (new) `.github/workflows/nightly.yml`
- (new) `.github/workflows/weekly.yml`
- (new) `Yello.Application/Boards/BoardPage.cs`
- `Yello.Application/Boards/ViewBoard/ViewBoardHandler.cs`
- (new) `Yello.Application/Boards/ViewBoardColumn/ViewBoardColumnHandler.cs`
- `Yello.Application/Tasks/MoveTask/MoveTaskHandler.cs`
- `Yello.Client/Components/BoardColumn.razor`
- `Yello.Client/Components/BoardView.Keys.cs`
- (new) `Yello.Client/Components/BoardView.Paging.cs`
- `Yello.Client/Components/BoardView.razor`
- `Yello.Client/Components/BoardView.razor.cs`
- `Yello.Client/Components/MoveChoice.cs`
- `Yello.Client/Components/MovePicker.razor`
- `Yello.Client/Components/TaskCard.razor`
- `Yello.Client/Localisation/ClientCopy.resx`
- `Yello.Client/Refusals/RefusalCopy.cs`
- `Yello.Client/wwwroot/css/components.css`
- `Yello.Client/wwwroot/js/board-drag.js`
- `Yello.Contracts/Boards/BoardResponse.cs`
- `Yello.Contracts/Boards/BoardRoutes.cs`
- `Yello.Contracts/ProblemTypes.cs`
- `Yello.Domain/Boards/IBoardReader.cs`
- `Yello.Domain/Tasks/ITaskStore.cs`
- `Yello.Domain/Tasks/PositionKey.cs`
- `Yello.Host/Endpoints/BoardEndpoints.cs`
- `Yello.Host/Endpoints/TaskMoveBody.cs`
- `Yello.Host/Endpoints/TaskMoveEndpoints.cs`
- `Yello.Host/Program.cs`
- (new) `Yello.Infrastructure/Persistence/ApplicationLocks.cs`
- `Yello.Infrastructure/Persistence/BoardReader.cs`
- `Yello.Infrastructure/Persistence/BoundCounter.cs`
- (new) `Yello.Infrastructure/Persistence/Migrations/20261028120526_ReadCommittedSnapshot.Designer.cs`
- (new) `Yello.Infrastructure/Persistence/Migrations/20261028120526_ReadCommittedSnapshot.cs`
- (new) `Yello.Infrastructure/Persistence/Migrations/20261028123715_TaskLockEscalationDisabled.Designer.cs`
- (new) `Yello.Infrastructure/Persistence/Migrations/20261028123715_TaskLockEscalationDisabled.cs`
- `Yello.Infrastructure/Persistence/ProjectStore.cs`
- `Yello.Infrastructure/Persistence/TaskStore.cs`
- `_bmad-output/implementation-artifacts/2-9-the-board-at-five-thousand-tasks.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`
- `docs/operations.md`
- `tests/TESTING-CONVENTIONS.md`
- `tests/Yello.Tests.Architecture/BoardPagingGateTests.cs`
- `tests/Yello.Tests.Architecture/DeploymentShapeTests.Tiers.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Delete.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Tasks.cs`
- `tests/Yello.Tests.Client/BoardPagingTests.cs`
- `tests/Yello.Tests.Client/BoardRenderCostTests.cs`
- `tests/Yello.Tests.Client/FakeYelloApi.cs`
- `tests/Yello.Tests.Client/RealHostBoardScaleTests.cs`
- `tests/Yello.Tests.Isolation/IsolationCases.cs`
- `tests/Yello.Tests.Slices/Boards/BoardEndpointTests.cs`
- `tests/Yello.Tests.Slices/Boards/BoardPageWire.cs`
- `tests/Yello.Tests.Slices/Boards/BoardPagingTests.cs`
- `tests/Yello.Tests.Slices/Boards/BoardScaleTimingTests.cs`
- `tests/Yello.Tests.Slices/Boards/EnvelopeLoadTests.cs`
- `tests/Yello.Tests.Slices/Boards/ViewBoard/ViewBoardHandlerTests.cs`
- `tests/Yello.Tests.Slices/Deployment/DatabaseIdentityTests.cs`
- `tests/Yello.Tests.Slices/Deployment/MigrationBundleEquivalenceTests.cs`
- `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Bound.cs`
- `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Idempotency.cs`
- `tests/Yello.Tests.Slices/Tasks/DeleteWire.cs`
- `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Bound.cs`
- `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.EditRace.cs`
- `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Move.cs`

### Change Log

- 2026-10-28 - Story created (create-story): ACs 1-8 verbatim from `epics.md:1303-1336`, ACs 9-19 derived; 16 open questions with recommendations; 26 ledger entries folded or with recommended re-owners.
- 2026-10-28 - Implemented (dev-story): keyset paging server and client, page contract and refusals, RCSI and lock-escalation migrations, application locks for Projects-per-Space and Project Tasks, full-gap refusal, Nightly/Weekly stages, AR-40b decided, ledger settled. Status -> review.
- 2026-10-28 - Code review (bmad-code-review, full, `ff8b728..HEAD`): 44 raw findings, 30 after de-duplication; 15 patches applied (the Project's Tasks lock named for the Space - PG-19; the Idempotency-Key lookup under `READCOMMITTEDLOCK`; a refused column page raises the refusal surface; the card index purged; the RCSI migration's `Down` leaves Azure SQL alone; `persist-credentials: false`; the browser p95 over 100 steps; stale range-lock and pre-RCSI wording in `TESTING-CONVENTIONS.md`, `TaskStore`, `ProjectStore`, `BoundCounter`, `PositionKey` and the race helpers; Q10 recorded in `ListProjectsHandler`; the key-length p99; P-11's in-process cold path stated; AC10's cursor-advance case; the record corrected; the "does not wait" cases count only waits on the held side). 5 deferred with owners (3.4, 7.4, 7.3). Files touched beyond the File List: `Yello.Application/Projects/ListProjects/ListProjectsHandler.cs`, `Yello.Infrastructure/Persistence/IdempotencyStore.cs`, `tests/Yello.Tests.Slices/Tasks/MoveWire.cs`, `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.LabelsRace.cs`. Build 0/0; 1364 total, 1356 passed, 0 failed, 8 skipped (run one project at a time after the host stopped the full run for low memory). Status -> done.
