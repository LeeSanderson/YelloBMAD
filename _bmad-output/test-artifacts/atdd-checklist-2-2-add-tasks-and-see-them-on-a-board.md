---
stepsCompleted:
  [
    'step-01-preflight-and-context',
    'step-02-generation-mode',
    'step-03-test-strategy',
    'step-04-generate-tests',
    'step-04c-aggregate',
    'step-05-validate-and-complete',
  ]
lastStep: 'step-05-validate-and-complete'
lastSaved: '2026-10-25'
workflowType: 'testarch-atdd'
storyId: '2.2'
storyKey: '2-2-add-tasks-and-see-them-on-a-board'
storyFile: '_bmad-output/implementation-artifacts/2-2-add-tasks-and-see-them-on-a-board.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-2-2-add-tasks-and-see-them-on-a-board.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Tasks/TaskWire.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Bound.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Idempotency.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Schema.cs'
  - 'tests/Yello.Tests.Slices/Boards/BoardEndpointTests.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Bound.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Isolation/Expectation.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Tasks.cs'
  - 'tests/Yello.Tests.Architecture/RefusalCopyTests.Tasks.cs'
  - 'tests/Yello.Tests.Architecture/RefusalCopyTests.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.Board.cs'
  - 'tests/Yello.Tests.Client/BoardTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Shared/TaskRouteScaffold.cs'
  - 'tests/Yello.Tests.Shared/ProductActor.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/2-2-add-tasks-and-see-them-on-a-board.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/addendum.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-13.md'
  - '_bmad-output/specs/spec-yello/SPEC.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-2-1-create-a-project-and-see-what-is-in-your-space.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 2, Story 2.2: Add Tasks and see them on a Board

**Date:** 2026-10-25
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server (`Yello.Tests.Slices`, two classes on the
shared engine), isolation rows on both surfaces (`Yello.Tests.Isolation`), ARCH gates (`Yello.Tests.Architecture`),
and browser cases against the network-edge fake (`Yello.Tests.Client`).

---

## Story summary

The second Space-scoped content entity and the product's primary surface: a `Task` table under the Space policy with
composite keys to its Project and Status, one create slice, one Board read, two routes, and the Board page. Three
things are new and carry the risk: a creation bound counted per Project from a route value, the first in-Space by-id
read where AD-3's duration promise is tested, and a second Space-scoped client page that must not copy Space home's
arrival logic.

**As** someone tracking my own work, **I want** to add Tasks to a Project and see them arranged in columns by Status,
**so that** value has landed before I have made a single structural decision.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - the empty Board: "Nothing here yet.", one primary action, columns for the effective set in order.
2. **AC2** - create takes the first Status; one Project at a time, one Space for ever.
3. **AC3** - a Viewer's Board is a Member's without the manipulation affordances, absent not disabled.
4. **AC4** - every Task in exactly one column.
5. **AC5** - column header: name, true total over the indexed predicate, create affordance pushed right.
6. **AC6** - the card is its title; metadata in monospace; no shadow; no nudging counts.
7. **AC7** - the 5,001st Task is refused in-transaction, machine-readably, with copy stating the bound.
8. **AC8** - `Task.SpaceId` non-nullable on the Task itself.
9. **AC9** - every new route is an isolation row on both surfaces.
10. **AC10** - `ISO-TIMING-IN-SPACE` live at `review`; the gap recorded whatever it is.
11. **AC11** - the bound is the pipeline's, scoped to the Project, and its race is proved deterministically (also for
    2.1's Projects pair).
12. **AC12** - the idempotency record cannot silently mis-record a write.
13. **AC13** - the client renders its own copy for `idempotency-key-in-flight` and `idempotency-key-reused`.
14. **AC14** - Space home and the Board keep what a person typed on re-arrival.
15. **AC15** - the Board is Space-scoped content: skeleton, purge, no names in title or URL, autofill off, skip control.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every
earlier story recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by
`tests/TESTING-CONVENTIONS.md`, so Task 10's browser cases are written there.

**Prerequisites - met.** Story `ready-for-dev`; AC1-AC8 verbatim from `epics.md:1023-1057` (`status: final`),
AC9-AC15 cite their sources. MTP, six test projects, .NET 10 SDK, Rancher. No checklist or progress file existed for
2.2, so Create mode ran from the start. The story file carries no `baseline_commit` frontmatter; dev-story has not
started.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
`tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. Where a
fragment and `tests/TESTING-CONVENTIONS.md` disagree, the repository document wins.

**Entry state:** green. 2.2 at `ready-for-dev`; `ISO-TIMING-IN-SPACE` is the only row pending on 2.2; GATE-003
permits it.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack; no recording. Sources: the story's ACs, Tasks, isolation-row
table and open questions; the live schema (nine migrations); the existing harness and gates.

## Step 3 - Test strategy

### The constraint that shapes every scaffold

None of the story's production types exist: no `Task` entity, no `TaskRoutes`/`BoardRoutes`, no
`ProblemTypes.InvalidTaskTitle`, no client Board page. So, as 2.1's run did:

- **Routes, wire codes and the problem type live in one test-side file**, `tests/Yello.Tests.Shared/TaskRouteScaffold.cs`.
  On activation each use is replaced by the `Yello.Contracts` constant and the file is deleted. The architecture
  suite does not reference `Yello.Tests.Shared`, so its gate file carries its patterns as private constants.
- **Wire cases read raw JSON and send anonymous bodies**, so they compile today and an added property cannot hide.
- **Architecture gates reach unwritten types by name** (`Yello.Domain.Tasks.TaskItem`, `Yello.Contracts.Tasks`,
  `Yello.Contracts.Boards`, `TaskStore`, a client method named for `ManageTasks`).
- **Isolation rows are pending rows, not skipped facts**, each with its real `Prepare` and expectation.

### Open-question dependencies (scaffolded against the recommended option)

| Q | Recommended | Where the scaffold depends on it | If decided otherwise |
|---|---|---|---|
| Q1 | (b) CLR type `TaskItem` | `RefusalBoundaryGateTests.Tasks.cs` `TaskEntityName`; ARCH-001..003 | Correct `TaskEntityName`; ARCH-001 still requires `SpaceScopedEntities` to name a real type |
| Q2 | (a) `PositionKey varchar … BIN2` now | `positionKey` in SLC-001's and BRD-002's property sets; SCH-005; `TaskWire.SeedTasksAsync`'s column list | Drop the property and SCH-005; correct the seed statement |
| Q3 | (a) trimmed, 1-200 | SLC-003 (201 refused, 200 anchor); the fake's title rule | Correct `TaskRouteScaffold.TitleMaximumLength` |
| Q4 | (b) "+" on the first column only | E2E-001, E2E-003, E2E-006 (`AddTaskControls(...).First`) | E2E-004's order check would see a button in every head |
| Q5 | (a) the three routes | every route in `TaskRouteScaffold` | Correct the scaffold, then the rows |
| Q6 | (a) every Task returned | BRD-003/BRD-004 (rows equal total) | Add a paging case; BRD-004's union needs every page |
| Q7 | measure first | `ISO-TIMING-IN-SPACE` | none - the row is the measurement |
| Q8 | (a) inline form, field stays open | E2E-006, E2E-009 | Correct the locators |
| Q9 | (a) skip button, first tab stop | E2E-013 ("Skip to content") | Re-own the case to 2.11 with the ledger entry |
| Q11 | (a) | E2E-003 asserts only the create affordance | - |

### Container budget

**No new engine, no new fixture type.** Two Slices classes (`TaskEndpointTests`, `BoardEndpointTests`) each take the
existing `MigratedDatabaseFixture` - a database each on the Slices engine. The isolation rows run on the existing
`IsolationWorldFixture` (the timing row on the timing collection's). The browser cases join `ClientSurfaceCollection`
and start no Host. Every new container-backed case is skipped today.

### Scenario map

| id | AC | Level | Pri | Scenario | File | State |
|---|---|---|:--:|---|---|---|
| `2.2-SLC-001` (x2) | 2 | INT (wire+db) | P0 | Create on Session and Token: `201`, body exactly `{createdAt,id,positionKey,statusId,title}`, trimmed, `Location` = `/spaces/{s}/tasks/{t}` (no Project), row in the route's Space and Project, Status = lowest `Position` of three | `Slices/Tasks/TaskEndpointTests.cs` | skip AC2 |
| `2.2-SLC-002` | 2 | INT (wire+db) | P1 | Task id has no default; ids sort in creation order under SQL Server ordering | `.TaskEndpointTests` | skip AC2 |
| `2.2-SLC-003` (x3) | 2 | INT (wire) | P1 | Empty, blank, 201 chars = `400 invalid-task-title`; 200 anchor accepted | `.TaskEndpointTests` | skip AC2 |
| `2.2-SLC-004` | 3, 9 | INT (wire) | P0 | Viewer added to an Owner's Space: Session and Token `403 manage-tasks`, two `InsufficientRole`, nothing written; Member anchor `201` | `.TaskEndpointTests` | skip AC3 |
| `2.2-SLC-005` | 9 | INT (wire) | P0 | Unknown Project id = boundary `404` bytes vs absent Space, no refusal row, never `500`; anchor `201` | `.TaskEndpointTests` | skip AC9 |
| `2.2-BND-001` | 7 | INT (wire) | P0 | 4,999 seeded; 5,000th `201`; 5,001st `409 bound-reached`, `tasks-per-project`, `5000`; 5,000 rows; no refusal row | `.TaskEndpointTests.Bound` | skip AC7 |
| `2.2-BND-002` | 11 | INT (wire) | P0 | Full Project P1; P2 in the same Space takes a Task (anchor: P1 refuses) | `.Bound` | skip AC11 |
| `2.2-BND-003` | 11 | INT (wire) | P0 | Unknown Project while another is full = `404`, not `409` | `.Bound` | skip AC11 |
| `2.2-BND-004` | 11 | INT (wire+db) | P0 | Deterministic race at 4,999: test holds the range, both requests blocked on `LCK_M_%`, commit; one `201`, one `409`, 5,000 rows | `.Bound` | skip AC11 |
| `2.2-BND-005` | 11 | INT (wire) | P0 | Replay of the 5,000th's key = its `201`; fresh key `409` | `.Bound` | skip AC11 |
| `2.2-IDM-001` (x2) | 2 | INT (wire) | P0 | Same key twice = one row, identical body and `Location` | `.TaskEndpointTests.Idempotency` | skip AC2 |
| `2.2-IDM-002` (x2) | 2 | INT (wire) | P0 | Same key, different title = `422 idempotency-key-reused` | `.Idempotency` | skip AC2 |
| `2.2-IDM-003` (x2) | 2 | INT (wire) | P0 | Another Account's same key in its own Space is a first use | `.Idempotency` | skip AC2 |
| `2.2-SCH-001` | 8 | INT (db) | P0 | `Task.SpaceId` non-nullable; FILTER, BLOCK AFTER INSERT, BLOCK AFTER UPDATE; no description column | `.TaskEndpointTests.Schema` | skip AC8 |
| `2.2-SCH-002` | 2 | INT (db) | P0 | Moving a Task (SpaceId, ProjectId, StatusId all to B's own) = SQL error 33504; retitle anchor | `.Schema` | skip AC2 |
| `2.2-SCH-003` | 8 | INT (db) | P0 | FKs exactly `(SpaceId,ProjectId)→Project` CASCADE and `(SpaceId,StatusId)→StatusDefinition` NO_ACTION; none to `Space` | `.Schema` | skip AC8 |
| `2.2-SCH-004` | 8 | INT (db) | P1 | `StatusDefinition` unique `(SpaceId, Id)` | `.Schema` | skip AC8 |
| `2.2-SCH-005` | 8 | INT (db) | P1 | `PositionKey` collation `Latin1_General_100_BIN2`; unique `(SpaceId,ProjectId,StatusId,PositionKey)` | `.Schema` | skip AC8 |
| `2.2-SCH-006` | 2 | INT (wire+db) | P0 | AD-17: no Task holds a Status its Space does not define (two Spaces) | `.Schema` | skip AC2 |
| `2.2-BRD-001` | 1 | INT (wire+db) | P0 | Empty Board: Project `{id,name}`, three columns in `Position` order, `total 0`, no Tasks | `Slices/Boards/BoardEndpointTests.cs` | skip AC1 |
| `2.2-BRD-002` | 5 | INT (wire) | P1 | Exact property sets; Space id nowhere in the body | `.BoardEndpointTests` | skip AC5 |
| `2.2-BRD-003` | 5 | INT (wire+db) | P0 | Totals 3/1/2 per Status = `SELECT COUNT(*)` per Status | `.BoardEndpointTests` | skip AC5 |
| `2.2-BRD-004` | 4 | INT (wire+db) | P0 | Every Task in exactly one column (its Status's), by position key; another Project's Tasks absent | `.BoardEndpointTests` | skip AC4 |
| `2.2-BRD-005` | 3 | INT (wire) | P0 | Viewer Session and Token Board = Member's, byte for byte | `.BoardEndpointTests` | skip AC3 |
| `2.2-BRD-006` | 9 | INT (wire) | P0 | Unknown Project's Board = boundary `404` bytes, no refusal row | `.BoardEndpointTests` | skip AC9 |
| `2.1-BND-002` (rewritten) | 11 | INT (wire+db) | P0 | Projects' race made deterministic (Task 7): both Accounts present first, test holds the range, both blocked, commit | `Slices/Projects/ProjectEndpointTests.Bound.cs` | **ACTIVE, green, P11 caught 3/3** |
| `2.2-ISO-001` | 9 | INT (wire) | P0 | ISO-DIRECT-BOARD: `GET /spaces/{SB}/projects/{PB}/board` = anchor `404`, one `CrossSpace` | `Isolation/IsolationCases.cs` | pending 2.2 |
| `2.2-ISO-002` | 9 | INT (wire) | P0 | ISO-BOARD-AGGREGATE: A's Board totals and Tasks are A's only after B created Tasks | `.IsolationCases` | pending 2.2 |
| `2.2-ISO-003` | 9 | INT (wire) | P0 | ISO-WRITE-TASK: `POST /spaces/{SB}/projects/{PB}/tasks` = anchor `404`, nothing written (Token admitted: boundary, not `401`) | `.IsolationCases` | pending 2.2 |
| `2.2-ISO-004` | 9 | INT (wire) | P0 | ISO-BOARD-FOREIGN-ID-IN-OWN-SPACE: `GET /spaces/{SA}/projects/{PB}/board` = random id bytes, no refusal row | `.IsolationCases` | pending 2.2 |
| `2.2-ISO-005` | 9 | INT (wire) | P0 | ISO-WRITE-TASK-FOREIGN-PROJECT-IN-OWN-SPACE: same, `POST`, nothing written in either Space | `.IsolationCases` | pending 2.2 |
| `2.2-ISO-006` | 3, 9 | INT (wire) | P0 | ISO-RECORD-KIND-TASK-VIEWER: demoted Viewer's Session and Token = `403 manage-tasks`, one `InsufficientRole` each | `.IsolationCases` | pending 2.2 |
| `2.2-ISO-007` | 10 | INT (wire) | P0 | ISO-TIMING-IN-SPACE (reshaped): boundary Board `404` vs in-Space random-Project Board `404`, bytes and duration (21/median/20%), each verdict written to the test output | `.IsolationCases` + `Expectation.cs` (`IndistinguishableFromInSpaceNotFound`) | pending 2.2 |
| `2.2-ARCH-001` | 8 | ARCH | P0 | Every `SpaceScopedEntities` name but `Label` resolves to a Domain type | `Architecture/RefusalBoundaryGateTests.Tasks.cs` | skip AC8 |
| `2.2-ARCH-002` | 2 | ARCH | P0 | `TaskItem.SpaceId` is `init`-only | `.Tasks` | skip AC2 |
| `2.2-ARCH-003` | 2 | ARCH | P0 | Only `TaskStore` writes a Task's `SpaceId` | `.Tasks` | skip AC2 |
| `2.2-ARCH-004` | 2 | ARCH | P0 | `CreateTaskRequest` carries `Title` and nothing else | `.Tasks` | skip AC2 |
| `2.2-ARCH-005` | 5 | ARCH | P0 | No Task/Board response carries `SpaceId`; `BoardColumnResponse.Total` is its own `int` | `.Tasks` | skip AC5 |
| `2.2-ARCH-006` | 9 | ARCH | P0 | Routes under a Project are exactly `PATCH` item, `POST …/tasks`, `GET …/board` | `.Tasks` | skip AC9 |
| `2.2-ARCH-007` | 11 | ARCH | P0 | `TasksPerProject` declared on at least one route, only where `{projectId}` is in the pattern | `.Tasks` | skip AC11 |
| `2.2-ARCH-008` | 3 | ARCH | P0 | Client `ManageTasks` predicate agrees with the matrix for every Role | `.Tasks` | skip AC3 |
| `2.2-ARCH-009` | 12 | ARCH | P0 | No Host code calls `Results`/`TypedResults` `.Created`/`CreatedAtRoute`/`Accepted`/`AcceptedAtRoute` | `.Tasks` | **ACTIVE, green** |
| `2.2-ARCH-009-ORACLE` | 12 | ARCH | P0 | The same scan reports a planted `Results.Created` caller in the test assembly | `.Tasks` | **ACTIVE, green (permanent plant)** |
| `2.2-ARCH-010` | 12 | ARCH | P0 | Every Space-scoped `POST`/`PUT`/`PATCH`/`DELETE` handler returns `IResult` or a task of one | `.Tasks` | **ACTIVE, green** |
| `2.2-ARCH-010-ORACLE` | 12 | ARCH | P0 | The rule accepts `Task<IResult>`/`IResult` and reports `Task<string>` | `.Tasks` | **ACTIVE, green (permanent plant)** |
| `2.2-COPY-001` | 13 | ARCH | P1 | `idempotency-key-in-flight` → "That was already sent. Reload to see it." | `Architecture/RefusalCopyTests.Tasks.cs` | skip AC13 |
| `2.2-COPY-002` | 13 | ARCH | P1 | `idempotency-key-reused` → "That request was already used for something else. Try again." | `.RefusalCopyTests.Tasks` | skip AC13 |
| `2.2-COPY-003` | 7 | ARCH | P1 | `tasks-per-project`, limit 5000 → "This Project has 5000 Tasks, the maximum." | `.RefusalCopyTests.Tasks` | **ACTIVE, green** |
| `2.2-DSN-001` | 5 | ARCH | P0 | `var(--radius-full)` used by exactly one rule, `.column-count` (`deferred-work.md:32`) | `Architecture/DesignFoundationGateTests.Board.cs` | skip AC5 |
| `2.2-DSN-002` | 6 | ARCH | P0 | `.task-card` and `.board-column` radii resolve to 3px (`:36`) | `.Board` | skip AC6 |
| `2.2-DSN-003` | 6 | ARCH | P0 | `.task-card` declares `box-shadow: none` explicitly | `.Board` | skip AC6 |
| `2.2-E2E-001` | 1 | E2E (fake) | P0 | Empty Board: three column names in order, "Nothing here yet.", one "Add a Task" in it | `Client/BoardTests.cs` | skip AC1 |
| `2.2-E2E-002` | 15 | E2E (fake) | P0 | Held Board: no Tasks, no empty state, `aria-busy` skeleton columns; released: cards, busy gone, "Board loaded." politely | `.BoardTests` | skip AC15 |
| `2.2-E2E-003` | 3 | E2E (fake) | P0 | Viewer: same columns and cards, no "Add a Task", no input, nothing disabled; Owner anchor has it | `.BoardTests` | skip AC3 |
| `2.2-E2E-004` | 5 | E2E (fake) | P0 | Chip shows the server's total (7) with 2 cards served; head order name, count, button | `.BoardTests` | skip AC5 |
| `2.2-E2E-005` | 6 | E2E (fake) | P0 | Card text is the title alone; computed `box-shadow: none`; no control inside | `.BoardTests` | skip AC6 |
| `2.2-E2E-006` | 2, 15 | E2E (fake) | P0 | Add: card in first column, chip 0→1, field open and empty, "Task added.", `Idempotency-Key`, autofill off, title `Yello`, no name in URL | `.BoardTests` | skip AC2 |
| `2.2-E2E-007` | 7 | E2E (fake) | P0 | `409 bound-reached` renders "This Project has 5000 Tasks, the maximum." inline; no card | `.BoardTests` | skip AC7 |
| `2.2-E2E-008` (x2) | 13 | E2E (fake) | P0 | `409` in-flight / `422` reused render their own copy, not "Not available." | `.BoardTests` | skip AC13 |
| `2.2-E2E-009` | 14 | E2E (fake) | P0 | Open creation's text survives `navigateTo` the same Board; Role re-read | `.BoardTests` | skip AC14 |
| `2.2-E2E-010` | 14 | E2E (fake) | P0 | Open Project create on Space home survives choosing the same Space; Role re-read | `.BoardTests` | skip AC14 |
| `2.2-E2E-011` | 15 | E2E (fake) | P0 | Switch away with the destination held: nothing of the Board or its Project name on screen; title `Yello` | `.BoardTests` | skip AC15 |
| `2.2-E2E-012` | 1 | E2E (fake) | P1 | Project row is a link named for the Project, `href` ids only; following it renders the Board | `.BoardTests` | skip AC1 |
| `2.2-E2E-013` (x2) | 15 | E2E (fake) | P0 | First Tab = "Skip to content"; Enter focuses `main h1` - Board and Space home | `.BoardTests` | skip AC15 |
| `2.2-E2E-014` | 15 | E2E (fake) | P1 | Context bar names the Project from the Board's answer, not Space home's list; cleared on leaving | `.BoardTests` | skip AC15 |

### Priorities

P0 for every case SM-1 (isolation), AR-30 (the bound), AD-18 (idempotency), AD-2/AD-3 (schema, boundary) or a verbatim
AC rests on. P1 for the id ordering, title validation, the response shape, the `(SpaceId, Id)` and BIN2 keys, the copy
mapping, the Project link and the context-bar name.

### Duplicate-coverage guard - deliberately not written

- **"No Task from another Space on the Board" is not a slice case.** ISO-BOARD-AGGREGATE proves it on both surfaces.
- **The capability matrix itself** is already gated; the `403` cases prove the route declares `ManageTasks`.
- **`SpaceIsolationSchemaTests` covers `Task`** once it joins `SchemaNames.SpaceScopedTables`; SCH-001 adds the
  `AFTER UPDATE` predicate, which that test cannot name.
- **The bound copy's wording** is held by COPY-003; E2E-007 proves only that the Board renders it inline.

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| `TaskTitle.TryNormalise`, `PositionKey.After`, `EffectiveStatusSet.Of` unit cases | The types do not exist; SLC-003, BRD-001 and BRD-004 cover the observable halves over the wire | Task 2 |
| Handler-level "`Total` is not `Tasks.Count`" case (Q6) | Under Q6 (a) the wire answer always has rows = total, so only a handler case with a reader returning fewer rows can catch the plant; the handler does not exist | Task 13 plant |
| `IdempotencyStore` fails closed on a pending entry beside the record | Needs the store's change tracker and a planted store; a product-side plant | Task 8, Task 13 |
| `StatusDefinitionReader` `.ThenBy(Id)` tiebreaker | Default Statuses have distinct positions, so no wire case can see a tie; a reader-level case needs two equal positions | Task 2 |
| 1.4.12 text spacing and 200% zoom (`deferred-work.md:20`) | The story lets dev-story do it or re-own it; the Board's markup decides what "clipped" means | Task 10 |
| Plants that edit product code (two-layer disclosure, count in the slice, `Results.Created` in the Task endpoint, unsaved effect, misspelt entity, Viewer "+", `sessionStorage`, `document.title`, card shadow, second pill, `AFTER UPDATE` removed) | Each needs the product this phase does not build; the scaffolds above are the gates that must catch them | Task 13 plant table |

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents; generation stayed in-thread, as
every earlier story recorded, because C# under this analyser set has to be built and fixed here anyway. Build fixes on
the way: `S2339`, `S125`, `S1144`, `S4005`, `S6605`, `MA0211` (x4). Worker A's output is the Slices, Isolation and
Architecture files; worker B's is the browser class and the fake.

### `tests/Yello.Tests.Slices/Tasks` - 25 cases in 19 skipped entries, one class

| File | Cases | ids | ACs |
|---|---|---|---|
| `TaskEndpointTests.cs` | 8 (5 entries) | SLC-001 to SLC-005 | AC2, AC3, AC9 |
| `TaskEndpointTests.Bound.cs` | 5 | BND-001 to BND-005 | AC7, AC11 |
| `TaskEndpointTests.Idempotency.cs` | 6 (3 entries) | IDM-001 to IDM-003 | AC2 (Task 6) |
| `TaskEndpointTests.Schema.cs` | 6 | SCH-001 to SCH-006 | AC2, AC8 |
| `TaskWire.cs` | harness | register, create Project/Task on either surface, seed 4,999 in one statement, read `dbo.[Task]`, the lock-wait poll | - |

### `tests/Yello.Tests.Slices/Boards/BoardEndpointTests.cs` - 6 cases, all skipped

BRD-001 to BRD-006 (AC1, AC3, AC4, AC5, AC9).

### `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Bound.cs` - 1 case rewritten, ACTIVE

`Two_concurrent_creations_at_49_yield_exactly_one_201_and_one_409` is now deterministic (Task 7's pattern). **Finding:**
the 2.1 case was not racing at the bound at all when it passed. An Account's first request in a Space inserts its
`SpacePresence` row (`SpacePresenceRegister.EnterAsync`), and the second Account's presence count
(`CountAsync(ExpiresAt > now)`) waits on that uncommitted row, so the pair serialises before either reaches the bound
count. That is why P11 was caught only 2 of 3 times. The first rewrite (lock held, both blocked, commit) still missed
P11 for the same reason. Both Accounts now read the Space once before the race, so neither writes presence in it.
Planted P11 (`BoundCounter`'s `WITH (UPDLOCK, HOLDLOCK)` removed): **caught 3 of 3** (`[Created, Created]` vs
`[Created, Conflict]`). Restored from memory in a `finally`; SHA-256 of `BoundCounter.cs` before and after:
`598d0007…839deb`. The Task twin (BND-004) carries the same warm-up.

### `tests/Yello.Tests.Isolation` - 6 rows added and 1 reshaped, all pending on 2.2

| File | Change |
|---|---|
| `IsolationCases.cs` | ISO-DIRECT-BOARD, ISO-BOARD-AGGREGATE, ISO-WRITE-TASK, ISO-BOARD-FOREIGN-ID-IN-OWN-SPACE, ISO-WRITE-TASK-FOREIGN-PROJECT-IN-OWN-SPACE, ISO-RECORD-KIND-TASK-VIEWER, with real `Prepare` and expectations; ISO-TIMING-IN-SPACE reshaped to the Board route (arm 1 the boundary `404`, arm 2 the in-Space random-Project `404`), still `PendingUntil: "2.2"` with an updated reason. Helpers `SeedProjectWithTaskAsync` (through the product, titles added to I-17's secrets), `ViewerCreatesATaskAsync`, `NoTaskWrittenInEitherSpaceAsync`, `NoTaskWrittenByTheViewerAsync`, `BoardHoldsExactlyTheCallersTasksAsync` |
| `Expectation.cs` | `IndistinguishableFromInSpaceNotFound`: bytes plus the one duration method, the verdict written to test output for the Debug Log whatever it is (AC10) |

`IsolationCaseListTests` (14) stays green: GATE-002 sees no new mapped route yet, and GATE-003 permits pending rows while
2.2 is `ready-for-dev`.

### `tests/Yello.Tests.Architecture` - 18 cases: 5 active, 13 skipped

| File | Tests |
|---|---|
| `RefusalBoundaryGateTests.Tasks.cs` | ARCH-001 to ARCH-008 skipped; **ARCH-009, ARCH-009-ORACLE, ARCH-010, ARCH-010-ORACLE active and green** |
| `RefusalCopyTests.Tasks.cs` | COPY-001, COPY-002 skipped; **COPY-003 active and green** |
| `RefusalCopyTests.cs` | `partial` (one keyword) |
| `DesignFoundationGateTests.Board.cs` | DSN-001 to DSN-003 skipped |

### `tests/Yello.Tests.Client/BoardTests.cs` - 16 cases in 14 skipped entries

E2E-001 to E2E-014, in `ClientSurfaceCollection` (one dev server, one Chromium, no Host).

### Harness added (test code, active)

- `Yello.Tests.Shared/TaskRouteScaffold.cs` - routes, wire codes, problem type, the client Board path. Temporary;
  deleted on activation.
- `ProductActor`: the write body now carries `title` beside `name` (`ProductActor.CaseBodyText`), so a refused Task
  creation in one's own Space is refused for its Project and never for its body; `HoldsTaskTitled` adds a title to the
  secrets. Every existing row still passes (full run below).
- `WireDatabase.TasksInAsync(spaceId)` - (id, Project, title) under the Space's own key.
- `FakeYelloApi`: `Boards`, `HoldBoardAsync`/`ReleaseBoard`, `TaskBoundReached`, `NextTaskCreateRefusal`; `GET …/board`
  and `POST …/tasks` with key replay, bound, title rule, append to the first column and total + 1. `FakeBoard`,
  `FakeBoardProject`, `FakeColumn`, `FakeTask` stand for the Task 3 contracts.

### Fixtures and seed data - every constraint checked

No new fixture type. Checked against `20260828164716_InitialSchema.cs` (`StatusDefinition`: `Id`, `SpaceId`, `Name`,
`Position`, `CreatedAt`), `20261024143930_AccountScopedMembership.cs`, `20261025094743_Projects.cs`
(`AK_Project_SpaceId_Id`) and story 2.2 Task 2 for the table to come:

- **Through the product:** Accounts, Personal Spaces, Owner Memberships, Tokens, Projects and (in most cases) Tasks.
  `UX_Account_NormalizedEmailAddress` holds because every address is random.
- **`AddMemberAsync`:** a `Member` or `Viewer` Membership for an Account the product registered, under the Space's key
  (RLS `BLOCK AFTER INSERT`), once per (Space, Account) (`AK_Membership_SpaceId_AccountId`), never a second Owner
  (`UX_Membership_SpaceId_Owner`).
- **`SeedTasksAsync`:** one `INSERT dbo.[Task] (Id, SpaceId, ProjectId, StatusId, Title, PositionKey, CreatedAt) SELECT …`
  under the Space's key: the composite FK `(SpaceId, ProjectId)` holds because the Project is a real one the product
  created in that Space; the composite FK `(SpaceId, StatusId)` holds because the Status is read from that Space's own
  `StatusDefinition` rows under its key; `BLOCK AFTER INSERT` holds because it runs under that Space's key; the unique
  `(SpaceId, ProjectId, StatusId, PositionKey)` index holds because each key is a distinct zero-padded number (digits
  only, which every base-62 alphabet orders first, so the product's "after the last" key sorts after them); `Id` is
  supplied because Task 2 maps it `ValueGeneratedNever`. **If the migration adds another non-nullable column without a
  default, or Q2 is decided (b), correct this one statement.**
- **SCH-002** moves the Task's `SpaceId`, `ProjectId` and `StatusId` together to B's own Project and Status, so both
  composite FKs are satisfied and only the `AFTER UPDATE` block predicate can refuse; it asserts error **33504**, not
  "any exception".
- **ISO-RECORD-KIND-TASK-VIEWER** creates the Project before the demotion (creating one needs `manage-projects`); the
  demoted Space has no Owner, which the filtered `UX_Membership_SpaceId_Owner` permits (as 1.9 and 2.1 recorded).

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Test run

```
dotnet test Yello.slnx --no-build
Test run summary: Passed!   total: 690   failed: 0   succeeded: 635   skipped: 55   (2m 29s)
```

Against the post-2.1-review baseline (633 total, 630 passed, 3 skipped): **+57 entries** - 5 new active and green
(ARCH-009 and its oracle, ARCH-010 and its oracle, COPY-003) and 52 new skipped entries (19 Task slices, 6 Board
slices, 13 architecture, 14 browser). The rewritten 2.1 BND-002 replaced its predecessor in place. The 3 baseline skips
are unchanged (`YELLO_MIGRATION_BUNDLE` unset). Four engines; the browser suite passed in the combined run.

### Red-phase compliance

- Every scaffold whose AC is not implemented is skipped with `red: AC<n> - <why> (Task n)`; every isolation row is
  pending on 2.2 with a reason.
- Active and green, each a standing gate: the rewritten 2.1 BND-002 (P11 planted, caught 3/3), ARCH-009 and ARCH-010
  (each with a permanent oracle that runs the rule over a planted subject and must report it), and COPY-003.
- Every scaffold compiles under warnings-as-errors. No scaffold references a type story 2.2 has not written, except by
  name or through `TaskRouteScaffold`.
- Every absence is anchored on a presence in the same case (the 5,000th before the 5,001st, the Member's `201` beside
  the Viewer's `403`, the retitle beside the refused move, the Owner's "Add a Task" beside the Viewer's absence, the
  full Project's `409` beside the other Project's `201`).
- Product code is unchanged: `git status` shows test files and this checklist only.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected
and the correction recorded in the Debug Log, not deleted.**

0. **Task 1.** Run `IsolationCaseListTests` (14 green). Decide Q1-Q11; where a decision differs from the table above,
   correct the scaffold first.
1. **ISO-TIMING-IN-SPACE first - the riskiest assumption.** It tests AD-3's promise that the boundary `404` (which writes
   an `AccessRefusal`, about 3 ms of 6.5 ms, `deferred-work.md:291`) and the in-Space not-found (which writes nothing)
   cannot be told apart by duration. If they can, Q7 is a design decision every later by-id route inherits. Remove its
   pending story and reason; it fails because the Board route does not exist. Build the minimum it needs (Task 2's
   table and policy, Task 4's create and Board read, the two routes). Then run the timing class alone three times, record
   both medians and the gap per surface whatever they are, and decide Q7 before building further.
2. **Schema** (Task 2): SCH-002, then SCH-003 (the cascade-path rule, error 1785), SCH-004, SCH-001 (widen
   `SpaceIsolationSchemaTests` so `Task` expects three predicates), SCH-005, then ARCH-001 (update `SpaceScopedEntities`
   to the real name) and ARCH-002.
3. **Create and Board** (Tasks 3-4): SLC-001, SLC-002, SLC-003, SLC-005, BRD-001, BRD-002, BRD-003, BRD-004, BRD-006,
   SCH-006, then ARCH-003, ARCH-004, ARCH-005 and ARCH-006. **Mapping the routes turns 2.1's `No_route_moves_a_Project`
   red**, because its prefix also matches `…/projects/{projectId}/tasks` and `…/board`. Narrow it to the collection and
   the item in the same change; ARCH-006 holds what lies under the item. Replace `TaskRouteScaffold` with the contracts
   as each case activates.
4. **Isolation rows** (Task 9), one at a time: ISO-RECORD-KIND-TASK-VIEWER, ISO-DIRECT-BOARD, ISO-WRITE-TASK,
   ISO-BOARD-FOREIGN-ID-IN-OWN-SPACE, ISO-WRITE-TASK-FOREIGN-PROJECT-IN-OWN-SPACE, ISO-BOARD-AGGREGATE. Then SLC-004 and
   BRD-005. GATE-002 must stay green; GATE-003 fails at `review` if any 2.2 row is still pending.
5. **Bound** (Tasks 5, 7): BND-001, BND-002, BND-003, ARCH-007, BND-004 (plant P11 three times for Tasks and three times
   for Projects; the Projects case already caught it 3/3), BND-005.
6. **Idempotency** (Tasks 6, 8): IDM-001 to IDM-003. ARCH-009 and ARCH-010 are already active; plant `Results.Created`
   in the Task endpoint against ARCH-009.
7. **Copy** (Task 11): COPY-001, COPY-002.
8. **Client** (Task 10): E2E-001, E2E-002, E2E-003 with ARCH-008, E2E-004, E2E-005, E2E-006, E2E-007, E2E-008, E2E-012,
   E2E-014, E2E-011, E2E-009, E2E-010, E2E-013; then DSN-001 (merge it into the "at most one" gate and close
   `deferred-work.md:32`), DSN-002, DSN-003. Correct the assumed locators to the real markup and record each.
9. Keep the rewritten 2.1 BND-002, ARCH-009, ARCH-010 (with their oracles) and COPY-003 green throughout. Task 13's
   product plants run against the activated cases.

### Commands

```
dotnet build Yello.slnx
dotnet test Yello.slnx --no-build
dotnet test --project tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj --no-build -- --filter-class Yello.Tests.Isolation.IsolationCaseListTests
dotnet test --project tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj --no-build -- --filter-class Yello.Tests.Isolation.IsolationTimingTests
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.Tasks.TaskEndpointTests
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.Boards.BoardEndpointTests
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.RefusalBoundaryGateTests
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.BoardTests
```

`--no-build` runs zero tests unless the same configuration was just built. If a Client case fails only in the full run,
run the Client project alone before treating it as real.

## Notes and risks

- **The presence serialisation is a product fact worth knowing, not a defect found here.** Two Accounts' first requests
  in one Space serialise on `SpacePresence`. Any concurrency case meant to race at something later must make both
  Accounts present first. Task 13's `TESTING-CONVENTIONS.md` note on the deterministic pattern should say so.
- **`TaskRouteScaffold` and the by-name gate subjects carry the largest correction risk** (Q1, Q2, Q5). Correct the
  scaffold, not the product, and record it.
- **Browser locators are assumptions** (`.board-column`, `.column-count`, `.task-card`, "Add a Task", "Board loaded.",
  "Skip to content"); the class remarks list them.
- **`ProductActor`'s write body changed for every row** (now `{name, title}`); each endpoint ignores the property it does
  not read, and the full run is green with it.
- **Container cost:** none today. On activation, two more databases on the Slices engine, not engines; the 4,999-row
  seeds are one statement each.

---

**Generated by BMad TEA Agent** - 2026-10-25
