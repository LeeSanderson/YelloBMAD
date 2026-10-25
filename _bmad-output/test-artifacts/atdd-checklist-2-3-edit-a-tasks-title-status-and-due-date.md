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
storyId: '2.3'
storyKey: '2-3-edit-a-tasks-title-status-and-due-date'
storyFile: '_bmad-output/implementation-artifacts/2-3-edit-a-tasks-title-status-and-due-date.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-2-3-edit-a-tasks-title-status-and-due-date.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Shared/TaskEditScaffold.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEditWire.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Edit.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.EditRace.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.EditIdempotency.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.EditSchema.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.TaskEdit.cs'
  - 'tests/Yello.Tests.Architecture/RefusalCopyTests.TaskEdit.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.TaskDetail.cs'
  - 'tests/Yello.Tests.Client/TaskDetailTests.cs'
  - 'tests/Yello.Tests.Client/TextSpacingTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Client/ClientSurfaceFixture.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/2-3-edit-a-tasks-title-status-and-due-date.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-2-2-add-tasks-and-see-them-on-a-board.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 2, Story 2.3: Edit a Task's title, Status and due date

**Date:** 2026-10-25
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server (`Yello.Tests.Slices`, more partials of
2.2's `TaskEndpointTests` on the shared engine), isolation rows on both surfaces (`Yello.Tests.Isolation`), ARCH gates
(`Yello.Tests.Architecture`), and browser cases against the network-edge fake (`Yello.Tests.Client`).

---

## Story summary

Small on the server - one nullable column, a by-id read, a `PATCH`, two problem types - and new on the client: the
product's first optimistic write and its first dialog. Three risks carry the story: a Status change is a **second
writer that places position keys** (without its own lock, an edit racing a creation into one column is a `500` on the
unique index); the **replica and its per-attribute revert** are new client ground; and the **panel** is the first
surface with a focus trap, `Esc`, focus return and "never two deep". The isolation work adds the ledger's Task-only
two-layer row (`deferred-work.md:485`).

**As** someone whose work changes, **I want** to change what a Task says and where it sits without saving, **so that**
keeping the Board true costs nothing.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - optimistic edit of title, Status, due date; the server admits or rejects; a rejected change is reverted.
2. **AC2** - no save button and no auto-save indicator anywhere.
3. **AC3** - a Viewer sees attributes as text; an API write is refused regardless of the interface.
4. **AC4** - a Status in the Project's effective set is accepted; a sibling Project's is refused.
5. **AC5** - the panel opens over the Board one deep, `Esc` closes, focus trapped and returned to the card.
6. **AC6** - no description editor.
7. **AC7** - the due date is a UTC `DateTimeOffset`, ISO 8601 with offset on the wire, rendered in the viewer's
   locale and zone.
8. **AC8** - the new routes are isolation rows on both surfaces; the Task's own two layers are proved by a plant;
   nothing pending on 2.3 at `review`.
9. **AC9** - a Status change takes a fresh key after the new column's last; two placements cannot collide, proved by
   a held lock.
10. **AC10** - the edit is idempotent.
11. **AC11** - refusals are stable `400` types that disclose nothing; random and foreign Status ids byte-identical.
12. **AC12** - every refusal the client can meet is reverted and said truthfully.
13. **AC13** - the panel takes 6px, a hairline and no shadow, and survives text spacing and 200% zoom.
14. **AC14** - the panel is purged with the Space, follows a lowered Role, keeps titles out of the tab and URL.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every
earlier story recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by
`tests/TESTING-CONVENTIONS.md`, so the panel's cases are written there.

**Prerequisites - met.** Story `ready-for-dev` (`sprint-status.yaml`); AC1-AC7 verbatim from `epics.md:1067-1095`
(`status: final`), AC8-AC14 cite their sources. MTP, six test projects, .NET 10 SDK, Rancher. No checklist and no
progress file existed for 2.3, so Create mode ran from the start. The story file carries no `baseline_commit`
frontmatter; dev-story has not started.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
`tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. Where a
fragment and `tests/TESTING-CONVENTIONS.md` disagree, the repository document wins.

**Entry state:** green after 2.2's review (749 total, 746 passed, 3 skipped). No isolation row was pending on any
story.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack; no recording. Sources: the story's ACs, Tasks, isolation-row
table and fourteen open questions; the live schema and harness; 2.2's scaffolds as the pattern.

## Step 3 - Test strategy

### The constraint that shapes every scaffold

No production type of this story exists: no `TaskRoutes.Task`, no `ProblemTypes.InvalidTaskStatus` /
`InvalidDueDate`, no `EditTaskRequest`, no `DueDate` column or property, no panel. So, as 2.1 and 2.2 did:

- **The route pattern and the two problem types live in one test-side file**, `tests/Yello.Tests.Shared/TaskEditScaffold.cs`.
  The Task's own path already exists (`TaskRoutes.TaskOf`, the creation's `Location`). On activation each use is
  replaced by the contract constant and the file is deleted. The Architecture suite and the fake carry the same
  literals as private constants (neither references `Yello.Tests.Shared` for them).
- **Wire cases read raw JSON and send JSON text or anonymous objects**, so they compile today, an added property
  cannot hide, and Q1's three states (absent, `null`, value) can each be sent.
- **Architecture gates reach unwritten types by name** (`EditTaskRequest`, `DueDate`, a client method named for
  `TaskTitle`).
- **Isolation rows are pending rows, not skipped facts**, each with its real `Prepare` and expectation.

### Open-question dependencies (scaffolded against the recommended option)

| Q | Recommended | Where the scaffold depends on it | If decided otherwise |
|---|---|---|---|
| Q1 | (a) one `PATCH`, merge-patch | every edit case sends only the changed member; `title: null`/`statusId: null`/`{}` refused; `dueDate: null` clears; ARCH `EditTaskRequest` = `{DueDate, StatusId, Title}`; E2E asserts the request carries one member | Correct `TaskEditScaffold.Task`, the ARCH property set and `Members(...)` asserts |
| Q2 | (a) per-attribute last-writer-wins | IDM-001 (a later edit by another key stands; the replay does not re-apply) | Add a `412` case |
| Q3 | (a) end of the new column | EDT-004, EDT-005, both race cases, the fake's move | Correct the "after the last key" asserts |
| Q4 | (a) start of the editor's day in UTC; 2000-2100 | EDT-002's exact wire form `+00:00`; EDT-008's range; E2E-117 (Tokyo 3 Nov = 2 Nov 15:00Z); E2E-116 | Correct `TaskEditScaffold.EarliestDueDate`/`FirstDueDateAfterTheRange` and the E2E instants |
| Q5 | (a) `400` + type per field | EDT-006, EDT-007, EDT-008; COPY-001/002 | Correct the status codes |
| Q6 | (a) Board state, no route | E2E-101 asserts the path is unchanged | Re-own E2E-101's address assert |
| Q7 | (a) `button[data-task-id]` in the card | every E2E case opens through it | Correct `OpenPanelAsync` and `CardOf` |
| Q8 | (a) per-attribute revert; no answer = revert + "Not sent." | E2E-111, E2E-112 | Correct the copy and the expectation |
| Q9 | (a) Enter/blur commits; `Esc` reverts, then closes | `CommitTitleAsync`; E2E-120 | Correct the commit helper |
| Q10 | (a) "Moved to {Status}." polite | E2E-107 | Drop the announcement assert |
| Q11 | in-panel listbox | E2E-107, E2E-110 (`[role=listbox] [role=option]`); DSN-005 `.picker` | Correct the locators |
| Q12 | `EditTaskAttributes` | nothing (no slice is named by a case) | - |
| Q13 | (a) foreign-Space Status stands in | EDT-007; S-2 case | Add the 6.1 ledger entry either way |
| Q14 | existing button styles | nothing | - |

### Container budget

**No new engine, no new fixture type, no new class on an engine.** Every Slices case is a new partial of 2.2's
`TaskEndpointTests` (one database on the Slices engine, as today). The isolation rows run on the existing
`IsolationWorldFixture`. The browser cases join `ClientSurfaceCollection` and start no Host. One 5,000-row seed (the
`ConsultsBound` case), one statement. Every new container-backed case is skipped today.

### Scenario map

| id | AC | Level | Pri | Scenario | File | State |
|---|---|---|:--:|---|---|---|
| `2.3-EDT-001` (x2) | 1 | INT (wire+db) | P0 | Title edit on Session and Token: `200`, body exactly `{createdAt,dueDate,id,positionKey,statusId,title}`, trimmed, unsent members unchanged; row agrees | `Slices/Tasks/TaskEndpointTests.Edit.cs` | skip AC1 |
| `2.3-EDT-002` | 7 | INT (wire+db) | P0 | `+05:30` due date stored and answered as the same instant, `2026-11-03T04:00:00+00:00`, offset 0 in the column | `.Edit` | skip AC7 |
| `2.3-EDT-003` | 7 | INT (wire+db) | P0 | `dueDate: null` clears to `NULL`, wire `null` (anchor: set first) | `.Edit` | skip AC7 |
| `2.3-EDT-004` | 4, 9 | INT (wire+db) | P0 | Status change: key after the new column's last, only its key written, old key not reused | `.Edit` | skip AC9 |
| `2.3-EDT-005` | 9 | INT (wire+db) | P1 | Same Status: row unchanged (no key write) | `.Edit` | skip AC9 |
| `2.3-EDT-006` | 4, 11 | INT (wire+db) | P0 | Random and foreign-Space Status ids: `400 invalid-task-status`, byte-identical, no id echoed, nothing changed, no refusal row; own Status anchor `200` | `.Edit` | skip AC4 |
| `2.3-EDT-007` (x4) | 11 | INT (wire+db) | P1 | Title `""`, blank, `null`, 201 chars: `400 invalid-task-title`, unchanged; 200 anchor | `.Edit` | skip AC11 |
| `2.3-EDT-008` (x4) | 11 | INT (wire+db) | P1 | Due date 1999, 2101, prose, a number: `400 invalid-due-date`, still `NULL`; 2000-01-01 anchor | `.Edit` | skip AC11 |
| `2.3-EDT-009` (x4) | 11 | INT (wire+db) | P1 | Unreadable body, `{}`, unknown member only, `statusId: null`: `400`, never `500`, unchanged | `.Edit` | skip AC11 |
| `2.3-EDT-010` | 3 | INT (wire+db) | P0 | Viewer added to an Owner's Space: Session and Token `403 manage-tasks`, two `InsufficientRole`, unchanged; Member anchor `200` | `.Edit` | skip AC3 |
| `2.3-EDT-011` | 8 | INT (wire+db) | P0 | Unknown Task id on `GET` and `PATCH` = boundary `404` bytes vs absent Space, no refusal row | `.Edit` | skip AC8 |
| `2.3-EDT-012` | 3, 7 | INT (wire) | P0 | `GET` answers the Task with `dueDate`; a Viewer's Session and Token read the Owner's bytes | `.Edit` | skip AC3 |
| `2.3-EDT-013` | 4 | INT (wire+db) | P0 | S-2 / AD-17: after a run of edits (incl. a refused foreign Status), no Task holds a Status outside its set | `.Edit` | skip AC4 |
| `2.3-EDT-014` | 1 | INT (wire+db) | P0 | An edit in a Project holding 5,000 Tasks is `200` (creation anchor `409`) - the `ConsultsBound` plant's case | `.Edit` | skip AC1 |
| `2.3-RACE-001` | 9 | INT (wire+db) | P0 | Test holds the first column's range; an edit into it and a creation into it both blocked on `LCK_M_%`; commit; `200` + `201`, distinct keys after the last | `.EditRace` | skip AC9 |
| `2.3-RACE-002` | 9 | INT (wire+db) | P0 | Same, two edits by two Accounts into the third column | `.EditRace` | skip AC9 |
| `2.3-IDM-001` (x2) | 10 | INT (wire+db) | P0 | Same key twice: the replay is the first `200` byte for byte even after another key's edit, and is not re-applied | `.EditIdempotency` | skip AC10 |
| `2.3-IDM-002` (x2) | 10 | INT (wire+db) | P0 | Same key, different body: `422 idempotency-key-reused`, unchanged | `.EditIdempotency` | skip AC10 |
| `2.3-IDM-003` (x2) | 10 | INT (wire+db) | P0 | Another Account's same key on its own Task is a first use | `.EditIdempotency` | skip AC10 |
| `2.3-SCH-001` | 7 | INT (db) | P0 | `DueDate` is `datetimeoffset NULL`; no description column (anchor: `CreatedAt` non-null) | `.EditSchema` | skip AC7 |
| `2.3-ISO-001` | 8 | INT (wire) | P0 | ISO-DIRECT-TASK: `GET /spaces/{SB}/tasks/{TB}` = anchor `404`, one `CrossSpace`, no title in body | `Isolation/IsolationCases.cs` | pending 2.3 |
| `2.3-ISO-002` | 8 | INT (wire) | P0 | ISO-EDIT-TASK-FOREIGN-SPACE: `PATCH` same; nothing written (Token admitted: boundary, not `401`) | `.IsolationCases` | pending 2.3 |
| `2.3-ISO-003` | 8 | INT (wire) | P0 | ISO-TASK-FOREIGN-ID-IN-OWN-SPACE: `GET /spaces/{SA}/tasks/{TB}` = random-id bytes, no refusal row (`:485`, the two-layer plant) | `.IsolationCases` | pending 2.3 |
| `2.3-ISO-004` | 8 | INT (wire) | P0 | ISO-EDIT-TASK-FOREIGN-ID-IN-OWN-SPACE: `PATCH` same; nothing written in either Space | `.IsolationCases` | pending 2.3 |
| `2.3-ISO-005` | 3, 8 | INT (wire) | P0 | ISO-RECORD-KIND-TASK-EDIT-VIEWER: demoted Viewer's Session and Token = `403 manage-tasks`, one `InsufficientRole` each; its Task unchanged | `.IsolationCases` | pending 2.3 |
| `2.3-ARCH-001` | 1, 6 | ARCH | P0 | Task contracts hold `CreateTaskRequest` and `EditTaskRequest` only; the edit's properties are exactly `DueDate`, `StatusId`, `Title`; `DueDate` is `DateTimeOffset?` | `Architecture/RefusalBoundaryGateTests.TaskEdit.cs` | skip AC1 |
| `2.3-ARCH-002` | 7 | ARCH | P0 | `TaskItem.DueDate` and `TaskResponse.DueDate` are `DateTimeOffset?`; `TaskResponse` keeps 2.2's fields | `.TaskEdit` | skip AC7 |
| `2.3-ARCH-003` | 7 | ARCH | P0 | No public Domain/Contracts member is `DateTime`/`DateOnly` (A-15) | `.TaskEdit` | **ACTIVE, green** |
| `2.3-ARCH-003-ORACLE` | 7 | ARCH | P0 | The scan reports a planted type's `DateTime` property, `DateOnly?` parameter and `DateTime` return | `.TaskEdit` | **ACTIVE, green (permanent plant)** |
| `2.3-ARCH-004` | 3, 8 | ARCH | P0 | The Task route is `GET` (`ReadProjectsAndTasks`) and `PATCH` (`ManageTasks`), neither Session-only, the `PATCH` with no `RequiredBound` | `.TaskEdit` | skip AC8 |
| `2.3-ARCH-005` | 12 | ARCH | P1 | The client's mirrored title predicate agrees with `TaskTitle.TryNormalise` on seven boundaries | `.TaskEdit` | skip AC12 |
| `2.3-COPY-001` | 12 | ARCH | P1 | `invalid-task-status` has its own key and copy | `Architecture/RefusalCopyTests.TaskEdit.cs` | skip AC12 |
| `2.3-COPY-002` | 12 | ARCH | P1 | `invalid-due-date` has its own key and copy | `.RefusalCopyTests.TaskEdit` | skip AC12 |
| `2.3-COPY-003` | 3, 12 | ARCH | P1 | `403 manage-tasks` renders "Viewers cannot edit Tasks." | `.RefusalCopyTests.TaskEdit` | **ACTIVE, green** |
| `2.3-DSN-001` | 13 | ARCH | P0 | `.task-detail` radii resolve to 6px (`:36`) | `Architecture/DesignFoundationGateTests.TaskDetail.cs` | skip AC13 |
| `2.3-DSN-002` | 13 | ARCH | P1 | `.picker` radii resolve to 3px | `.TaskDetail` | skip AC13 |
| `2.3-DSN-003` | 13 | ARCH | P0 | `.task-detail` declares `box-shadow: none` and a `var(--border-hairline-width)` border | `.TaskDetail` | skip AC13 |
| `2.3-E2E-101` | 5, 14 | E2E (fake) | P0 | One modal dialog labelled by its `h2` title; focus inside; Board beneath; path and `document.title` unchanged | `Client/TaskDetailTests.cs` | skip AC5 |
| `2.3-E2E-102` | 5 | E2E (fake) | P0 | Tab/Shift+Tab x12 stay inside; `Esc` closes; focus on the card's `data-task-id` control | `.TaskDetailTests` | skip AC5 |
| `2.3-E2E-103` | 5 | E2E (fake) | P0 | Activating a second card replaces the panel; one dialog | `.TaskDetailTests` | skip AC5 |
| `2.3-E2E-104` | 6 | E2E (fake) | P0 | No textarea, contenteditable or "description" in the panel (anchor: title field present) | `.TaskDetailTests` | skip AC6 |
| `2.3-E2E-105` | 2 | E2E (fake) | P0 | No "Save"/"Saving"/"Saved", no save button, no busy marker - while held and after | `.TaskDetailTests` | skip AC2 |
| `2.3-E2E-106` | 1 | E2E (fake) | P0 | Held `PATCH`: card and panel show the new title; body has only `title`; `Idempotency-Key`; admitted, stays | `.TaskDetailTests` | skip AC1 |
| `2.3-E2E-107` | 1 | E2E (fake) | P0 | Picker lists the effective set, current marked, nothing pre-selected; choosing moves the card to the end and both chips at once; "Moved to Done." | `.TaskDetailTests` | skip AC1 |
| `2.3-E2E-108` (x3) | 12 | E2E (fake) | P0 | `400`/`403`/`422` revert title on card and panel, copy inline, never a toast | `.TaskDetailTests` | skip AC12 |
| `2.3-E2E-109` | 12 | E2E (fake) | P0 | `404` reverts, then the page's refusal surface; panel gone | `.TaskDetailTests` | skip AC12 |
| `2.3-E2E-110` | 12 | E2E (fake) | P0 | Refused Status change: card back in place, chips back | `.TaskDetailTests` | skip AC12 |
| `2.3-E2E-111` | 12 | E2E (fake) | P0 | No answer: revert, "Not sent." | `.TaskDetailTests` | skip AC12 |
| `2.3-E2E-112` | 12 | E2E (fake) | P0 | Per-attribute revert: held title refused after an admitted due date; date stays | `.TaskDetailTests` | skip AC12 |
| `2.3-E2E-113` | 3 | E2E (fake) | P0 | Viewer panel: text only, Close is the only button, nothing disabled (Owner anchor) | `.TaskDetailTests` | skip AC3 |
| `2.3-E2E-114` | 14 | E2E (fake) | P0 | Role re-read lower while open: panel stays, title field goes | `.TaskDetailTests` | skip AC14 |
| `2.3-E2E-115` | 14 | E2E (fake) | P0 | Switching Spaces closes the panel; title gone; `document.title` `Yello` | `.TaskDetailTests` | skip AC14 |
| `2.3-E2E-116` (x2) | 7 | E2E (fake) | P0 | 02:00Z on 3 Nov: Los Angeles shows the 2nd, Tokyo the 3rd, on the card and in the date field | `.TaskDetailTests` | skip AC7 |
| `2.3-E2E-117` | 7 | E2E (fake) | P0 | Choosing 3 Nov in Tokyo sends an ISO instant with offset equal to 2 Nov 15:00Z | `.TaskDetailTests` | skip AC7 |
| `2.3-E2E-118` | 7 | E2E (fake) | P1 | "Clear due date" sends exactly `{"dueDate":null}`; field empties | `.TaskDetailTests` | skip AC7 |
| `2.3-E2E-119` | 14 | E2E (fake) | P0 | Title input `autocomplete="off"`, non-reusable name; no title in tab or address after an edit | `.TaskDetailTests` | skip AC14 |
| `2.3-E2E-120` | 5 | E2E (fake) | P1 | `Esc` in the title field reverts it and sends nothing; a second `Esc` closes | `.TaskDetailTests` | skip AC5 |
| `2.3-A11Y-001` (x2) | 13 | E2E (fake) | P1 | Panel open on a long title with a due date: no clipping under 1.4.12 or 200% text zoom | `Client/TextSpacingTests.cs` | skip AC13 |

### Priorities

P0 for every case SM-1 (isolation), AD-15 (the key race), AD-18 (idempotency), AD-11 (the replica's revert), AD-3 or a
verbatim AC rests on. P1 for the validation edge cases, the same-Status no-op, the copy mappings, the picker radius,
the `Esc` layering and text spacing.

### Duplicate-coverage guard - deliberately not written

- **"A foreign Space's Task is unreachable" is not a slice case.** ISO-DIRECT-TASK and ISO-EDIT-TASK-FOREIGN-SPACE
  prove it on both surfaces.
- **The Viewer's `403` is written once per level that can see it**: EDT-010 (Viewer added to an Owner's Space, AC3's
  shape) and ISO-RECORD-KIND-TASK-EDIT-VIEWER (the demoted variance, the record kind). The story marks the isolation
  row optional; it is scaffolded because it costs one row and guards the record kind on the new route.
- **No timing row**: `NotAvailableFloor` already holds every Space-scoped `404`, and ISO-TIMING-IN-SPACE measures it
  (story Task 8).
- **The capability matrix itself** is already gated; ARCH-004 proves the routes declare the right capability.

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| `TaskDueDate.TryNormalise`, `EffectiveStatusSet` membership unit cases | The types do not exist; EDT-002, EDT-006 and EDT-008 cover the observable halves over the wire | Task 2 |
| `has-pending-model-changes` reports none | A command, not a test; the bundle-equivalence test already compares the catalogue | Task 3 |
| The two-layer plant (`:485`), the lock-hint plant, the skipped-effective-set plant, `ConsultsBound`, "Saved", a description region, a Viewer offered the input, a 3px panel, pre-edit revert, the panel left open after a purge | Each edits product code this phase does not write; the scaffolds above are the gates that must catch them | Task 12 plant table |
| Narrowing 2.2's `The_Task_creation_request_carries_a_title_and_nothing_else` and `BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow` | Both are green, active 2.2 gates that this story's own change turns red by design (story Task 4; Q7 (a)) | Task 4, Task 9 |

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents; generation stayed in-thread, as
every earlier story recorded, because C# under this analyser set has to be built and fixed here anyway (no temp JSON
worker outputs were written). Build fixes on the way: `IDE1006`, `IDE0005`, `S6566` (the planted `DateTime`, pragma
with its reason), `S6602` (x2), `S3776`, `CA1861`, `S8969`, `CS8602` (x2), `S6966`, a missing `using`. Worker A's
output is the Slices, Isolation and Architecture files; worker B's is the browser class, the text-spacing case, the
fake and the fixture overload.

### `tests/Yello.Tests.Slices/Tasks` - 33 cases in 20 skipped entries, the existing class

| File | Cases | ids | ACs |
|---|---|---|---|
| `TaskEndpointTests.Edit.cs` | 24 (14 entries) | EDT-001 to EDT-014 | AC1, AC3, AC4, AC7, AC8, AC9, AC11 |
| `TaskEndpointTests.EditRace.cs` | 2 | RACE-001, RACE-002 | AC9 |
| `TaskEndpointTests.EditIdempotency.cs` | 6 (3 entries) | IDM-001 to IDM-003 | AC10 |
| `TaskEndpointTests.EditSchema.cs` | 1 | SCH-001 | AC7 |
| `TaskEditWire.cs` | harness | edit/read on either surface, raw and member bodies, the due-date read, the AD-17 query | - |

### `tests/Yello.Tests.Isolation/IsolationCases.cs` - 5 rows, all pending on 2.3

ISO-DIRECT-TASK, ISO-EDIT-TASK-FOREIGN-SPACE, ISO-TASK-FOREIGN-ID-IN-OWN-SPACE, ISO-EDIT-TASK-FOREIGN-ID-IN-OWN-SPACE,
ISO-RECORD-KIND-TASK-EDIT-VIEWER, each with its real `Prepare`, an expectation for **both** surfaces, a send on the
exact route it names (`TaskEditScaffold.Task`), and `PendingUntil: "2.3"` with a reason. Helpers added:
`ViewerEditsATaskAsync`, `TheViewersTaskIsUnchangedAsync`; `SeedProjectWithTaskAsync` and
`NoTaskWrittenInEitherSpaceAsync` reused (a `PATCH` sends `ProductActor`'s valid body, whose title is
`ProductActor.CaseBodyText`, so a write that landed anywhere would carry it). `IsolationCaseListTests` stays green:
GATE-002 sees no new mapped route yet, and GATE-003 permits pending rows while 2.3 is `ready-for-dev`.

### `tests/Yello.Tests.Architecture` - 12 cases: 3 active, 9 skipped

| File | Tests |
|---|---|
| `RefusalBoundaryGateTests.TaskEdit.cs` | ARCH-001, ARCH-002, ARCH-004, ARCH-005 skipped; **ARCH-003 and ARCH-003-ORACLE active and green** |
| `RefusalCopyTests.TaskEdit.cs` | COPY-001, COPY-002 skipped; **COPY-003 active and green** |
| `DesignFoundationGateTests.TaskDetail.cs` | DSN-001 to DSN-003 skipped |

### `tests/Yello.Tests.Client` - 25 cases in 21 skipped entries

`TaskDetailTests.cs`: E2E-101 to E2E-120 (23 cases, 20 entries), in `ClientSurfaceCollection` (one dev server, one
Chromium, no Host). `TextSpacingTests.cs`: A11Y-001 (2 cases, 1 entry), the panel under `deferred-work.md:20`'s
convention.

### Harness added (test code, active)

- `Yello.Tests.Shared/TaskEditScaffold.cs` - the route pattern, the parameter name, the two problem types, the Q4
  range. Temporary; deleted on activation.
- `FakeYelloApi`: `GET` and `PATCH` on `TaskRoutes.TaskOf`; `HoldTaskEditAsync` (holds the **next** edit only, so a
  second attribute's edit is answered while the first is held - Q8), `ReleaseTaskEdit`, `ReleaseTaskEditRefused`,
  `ReleaseTaskEditUnanswered` (the connection reset), `NextTaskEditRefusal`, `FindTask`. The edit applies Q1 (a)
  merge-patch, the 1-200 title rule, the effective set (the Board's columns) and a Status move to the end of the new
  column with both totals. `FakeTask` gains an optional `DueDate` (AD-19: an added field); 2.2's cases are unchanged
  and green.
- `ClientSurfaceFixture.OpenAsync(locale, timezoneId)` - a context in a given IANA zone (AC7).

### Fixtures and seed data - every constraint checked

No new fixture type. Checked against `20261025094743_Projects.cs` (`AK_Project_SpaceId_Id`), 2.2's Task migration
(`dbo.[Task]`: composite FKs `(SpaceId, ProjectId) -> Project` and `(SpaceId, StatusId) -> StatusDefinition`, unique
`(SpaceId, ProjectId, StatusId, PositionKey)`, FILTER and BLOCK AFTER INSERT/UPDATE predicates) and story 2.3 Task 3
for the column to come:

- **Through the product:** Accounts, Personal Spaces, Owner Memberships, Tokens, Projects and every Task an edit case
  touches. `UX_Account_NormalizedEmailAddress` holds because every address is random.
- **`AddMemberAsync`:** a `Member` or `Viewer` Membership for an Account the product registered, under the Space's key
  (`BLOCK AFTER INSERT`), once per (Space, Account) (`AK_Membership_SpaceId_AccountId`), never a second Owner
  (`UX_Membership_SpaceId_Owner`).
- **`SeedTasksAsync` (EDT-014 only, 4,999 rows):** unchanged 2.2 statement. It stays valid after Task 3 **because
  `DueDate` is nullable** - the expand-only rule - so the `INSERT` naming seven columns still satisfies the table.
- **A Status edit** sends a `StatusDefinition` id read from the editing Space under its own key, so the composite FK
  `(SpaceId, StatusId)` holds. The foreign-Space id (EDT-006, EDT-013) is the case: the slice must refuse it **before**
  any write, so that FK is never what answers (it would be a `500`).
- **The race cases' held lock** runs under the Space's session key (`OpenForSpaceAsync`), so RLS's FILTER predicate
  lets the range read see the Space's rows, and the `SpaceId`-leading unique index keeps the key-range lock inside one
  Space. Both racing Accounts read the Board first, so neither inserts `SpacePresence` inside the race.
- **ISO-RECORD-KIND-TASK-EDIT-VIEWER** creates the Project and Task before the demotion (creating needs
  `manage-tasks`); the demoted Space has no Owner, which the filtered `UX_Membership_SpaceId_Owner` permits.

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Test run

```
dotnet test Yello.slnx --no-build
Test run summary: Passed!   total: 802   failed: 0   succeeded: 749   skipped: 53   (2m 54s)
```

Against the post-2.2-review baseline (749 total, 746 passed, 3 skipped): **+53 entries** - 3 new active and green
(ARCH-003, its oracle, COPY-003) and 50 new skipped entries (20 Slices, 9 Architecture, 21 Client). The 3 baseline
skips are unchanged (`YELLO_MIGRATION_BUNDLE` unset). Four engines, one full run, no engine-start failure; the browser
suite passed in the combined run.

### Red-phase compliance

- Every scaffold whose AC is not implemented is skipped with `red: AC<n> - <why> (Task n; Qn)`; every isolation row is
  pending on 2.3 with a reason.
- Active and green, each a standing gate: ARCH-003 (no `DateTime`/`DateOnly` in Domain or Contracts) with a permanent
  oracle over a planted type, and COPY-003 (the `manage-tasks` sentence the panel's `403` revert renders).
- Every scaffold compiles under warnings-as-errors. No scaffold references a type 2.3 has not written, except by name
  or through `TaskEditScaffold`.
- Every absence is anchored on a presence in the same case (the 200-character title beside the refused 201, the
  2000-01-01 date beside the refused 1999, the own Status beside the foreign one, the Member's `200` beside the
  Viewer's `403`, the creation's `409` beside the full Project's admitted edit, the title field beside the absent
  description, the Owner's field beside the Viewer's absence).
- Product code is unchanged: `git status` shows test files, this checklist and the story's Dev Notes only.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected
and the correction recorded in the Debug Log, not deleted.**

0. **Task 1.** Run `IsolationCaseListTests` (green). Five rows are pending on 2.3 - list them in the Debug Log. Decide
   Q1-Q14; where a decision differs from the table above, correct the scaffold first.
1. **RACE-001 first - the riskiest assumption.** It tests that a Status edit's key placement is serialised against a
   creation into the same column (AD-15, `deferred-work.md:492`), and Task 6 asks the open question underneath it:
   **does creation's Project-wide `UPDLOCK, HOLDLOCK` range conflict with the edit's column range?** Build the minimum
   (Task 2's `DueDate`-free edit, Task 4's route, Task 5's `PATCH` with the Status branch, Task 6's locked last-key
   read), activate RACE-001, then RACE-002. Plant the lock hint's removal three times; record what you measured. If the
   ranges do not conflict, take the Project-wide range in the edit and widen `HoldColumnAsync` to match.
2. **Schema and wire shape** (Tasks 2-4): SCH-001, ARCH-002, ARCH-001 (and narrow 2.2's
   `The_Task_creation_request_carries_a_title_and_nothing_else` in the same change), ARCH-004. Replace
   `TaskEditScaffold` with the contracts as each case activates; delete it when unused.
3. **Edit and read** (Task 5): EDT-001, EDT-002, EDT-003, EDT-004, EDT-005, EDT-006, EDT-007, EDT-008, EDT-009,
   EDT-011, EDT-012, EDT-013 (S-2), EDT-014 (then plant `ConsultsBound`).
4. **Isolation rows** (Task 8), one at a time: ISO-RECORD-KIND-TASK-EDIT-VIEWER, ISO-DIRECT-TASK,
   ISO-EDIT-TASK-FOREIGN-SPACE, ISO-TASK-FOREIGN-ID-IN-OWN-SPACE (plant the two-layer drop, then each layer alone;
   close `:485`), ISO-EDIT-TASK-FOREIGN-ID-IN-OWN-SPACE. Then EDT-010. GATE-002 must stay green; **GATE-003 fails at
   `review` if any of the five is still pending.**
5. **Idempotency** (Task 7): IDM-001 to IDM-003.
6. **Copy** (Task 9 Copy): COPY-001, COPY-002. COPY-003 is already active.
7. **Client** (Task 9, Task 11): first widen 2.2's `A_Task_card_is_its_title_alone_and_carries_no_shadow` to admit the
   opening button (Q7 (a)) in the change that adds it. Then E2E-101, E2E-102, E2E-120, E2E-103, E2E-104, E2E-113 with
   ARCH-005, E2E-106, E2E-105, E2E-107, E2E-108, E2E-109, E2E-110, E2E-111, E2E-112, E2E-116, E2E-117, E2E-118,
   E2E-119, E2E-114, E2E-115; then DSN-001 to DSN-003 and A11Y-001. Correct the assumed locators to the real markup
   and record each.
8. Keep ARCH-003 (with its oracle) and COPY-003 green throughout. Task 12's product plants run against the activated
   cases.

### Commands

```
dotnet build Yello.slnx
dotnet test Yello.slnx --no-build
dotnet test --project tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj --no-build -- --filter-class Yello.Tests.Isolation.IsolationCaseListTests
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.Tasks.TaskEndpointTests
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.RefusalBoundaryGateTests
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.TaskDetailTests
```

`--no-build` runs zero tests unless the same configuration was just built. Run test processes one at a time on this
machine. If a Client case fails only in the full run, run the Client project alone before treating it as real.

## Notes and risks

- **The race cases decide a design question, not just a test.** If the creation's and the edit's ranges do not
  conflict, an edit and a creation can still collide even with the edit's own lock; RACE-001 is the case that shows it.
- **Two active 2.2 gates go red by design** when the edit lands (the creation-request shape gate and the
  card-has-no-control browser case). Widen each deliberately, never loosely, and record it.
- **`TaskEditScaffold`, the fake's merge-patch and the browser locators carry the largest correction risk** (Q1, Q4,
  Q7, Q11). Correct the scaffold, not the product, and record it.
- **The fake holds only the next edit.** A case that needs two edits held at once must arm a second hold after the
  first edit arrives.
- **Container cost:** none today. On activation, the same Slices database as 2.2's Task cases; one 5,000-row seed in
  one statement; two short lock-holding cases.

## Completion summary

- **Checklist:** `_bmad-output/test-artifacts/atdd-checklist-2-3-edit-a-tasks-title-status-and-due-date.md`.
- **Story handoff:** `_bmad-output/implementation-artifacts/2-3-edit-a-tasks-title-status-and-due-date.md` (Dev Notes
  section "ATDD artifacts - red-phase scaffolds already in the tree").
- **Next workflow:** `bmad-dev-story`; `bmad-testarch-automate` after implementation.

---

**Generated by BMad TEA Agent** - 2026-10-25
