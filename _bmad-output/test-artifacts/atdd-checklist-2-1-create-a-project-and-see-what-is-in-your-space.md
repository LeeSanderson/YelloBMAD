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
storyId: '2.1'
storyKey: '2-1-create-a-project-and-see-what-is-in-your-space'
storyFile: '_bmad-output/implementation-artifacts/2-1-create-a-project-and-see-what-is-in-your-space.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-2-1-create-a-project-and-see-what-is-in-your-space.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Bound.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Idempotency.cs'
  - 'tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.Schema.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCaseListTests.cs'
  - 'tests/Yello.Tests.Isolation/Expectation.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Projects.cs'
  - 'tests/Yello.Tests.Client/SpaceHomeProjectsTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Shared/ProjectRouteScaffold.cs'
  - 'tests/Yello.Tests.Shared/ProductActor.cs'
  - 'tests/Yello.Tests.Shared/WireSession.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/2-1-create-a-project-and-see-what-is-in-your-space.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-1-9-prove-isolation-on-both-surfaces.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 2, Story 2.1: Create a Project and see what is in your Space

**Date:** 2026-10-25
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server (`Yello.Tests.Slices`, one class on the shared engine), isolation rows on both surfaces (`Yello.Tests.Isolation`), ARCH gates (`Yello.Tests.Architecture`), and browser cases against the network-edge fake (`Yello.Tests.Client`).

---

## Story summary

The first Space-scoped entity after Statuses, and the first Space-scoped write that not every Role holds. A `Project` table under the Space policy, three slices (list, create, rename) on three routes for both surfaces, and Space home rendering the list. Three pipeline behaviours get their first real subject: the capability check produces the first wire `403`, the bound registry enforces the Projects-per-Space bound at creation, and `Idempotency-Key` is built.

**As** someone with a Space of my own, **I want** to create named Projects and see the ones in the Space I am in, **so that** my work has somewhere to live before I put anything in it.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - the empty Space states "No Projects yet." and offers one action; nothing asks for configuration.
2. **AC2** - create fixes the Project to the active Space; effective Status set = Space default, empty delta; a Viewer has no create affordance.
3. **AC3** - rename; a Viewer has no rename affordance.
4. **AC4** - the listing is every Project in the Space and nothing else; Viewers see what Members see.
5. **AC5** - the 51st is refused in the same transaction, machine-readable, stated plainly.
6. **AC6** - non-nullable `SpaceId` under RLS; `Guid` id from the sequential generator.
7. **AC7** - no route moves a Project.
8. **AC8** - every new route is an isolation row on both surfaces; the first wire `403` is live.
9. **AC9** - an `Idempotency-Key` replay is applied once by the pipeline; a Token issuance replay never returns the plaintext.
10. **AC10** - the bound is read from `BoundRegistry` and checked by the pipeline, gated.
11. **AC11** - a Token reaches the first write without anti-forgery; the method table is decided and pinned.
12. **AC12** - a pending isolation row cannot turn the suite red at a phase boundary, nor outlive its story.
13. **AC13** - Space home is Space-scoped content: rendered after the answer, purged with the Space, never in the title, autofill off, strings externalised, absent-not-disabled.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`**, as stories 1.5-1.10 recorded: `Yello.slnx` and `.csproj` files, no `package.json`, no Playwright or Cypress config. The browser level is Playwright *for .NET* in `Yello.Tests.Client`, bound by `tests/TESTING-CONVENTIONS.md`, so the story's Task 10 browser cases are written.

**Prerequisites - met.** The story is `ready-for-dev`; AC1-AC7 are verbatim from `epics.md:983-1013` (`status: final`), AC8-AC13 cite their sources. MTP with six test projects; .NET SDK 10 with the Rancher runtime. No progress file existed for this story, so Create mode ran from the start. The story frontmatter carries `baseline_commit: 311d1d5`; dev-story has not started.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. Not loaded: Playwright Utils and Pact fragments (disabled, TypeScript-shaped), `ci-burn-in` (story 1.10 owns CI). Where a fragment and `tests/TESTING-CONVENTIONS.md` disagree, the repository document wins.

**Known red on entry:** `IsolationCaseListTests.Every_pending_row_names_a_story_that_is_still_in_backlog` (1.9-GATE-003) failed with `ISO-RECORD-KIND-INSUFFICIENT-ROLE: pending until 2.1, which is now 'ready-for-dev' - convert the row`, because create-story moved 2.1 out of `backlog` (`deferred-work.md:411`).

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack. No recording. Sources: the story's ACs, Tasks, isolation-row table and open questions (recommended answers taken as the expectation, Q1-Q13); the live schema (the six migrations); the existing harness (`HostProcess`, `WireSession`, `RunningHost`, `ProductActor`, `WireDatabase`, `IsolationWorld`, `FakeYelloApi`, `ClientSurfaceFixture`); and the existing gates (`RefusalBoundaryGateTests`, `IsolationCaseListTests`, `SpaceIsolationSchemaTests`).

## Step 3 - Test strategy

### The constraint that shapes every scaffold

None of the story's production types exist: no `Project`, no `ProjectRoutes`, no `ProblemTypes.InvalidProjectName`, no idempotency port. So:

- **Routes and problem types are held in one test-side file**, `tests/Yello.Tests.Shared/ProjectRouteScaffold.cs`, with the strings Tasks 4 and 7 name. On activation every use is replaced by the `Yello.Contracts` constant and the file is deleted. The architecture suite does not reference `Yello.Tests.Shared`, so its gate file carries the two route patterns as private constants.
- **Wire cases read raw JSON and send anonymous bodies**, so they compile today and an added property cannot hide behind a DTO.
- **Architecture gates reach unwritten types by name** (`Yello.Application.Spaces.IBoundCounter`, `IIdempotencyStore`, `Yello.Domain.Projects.Project`, `Yello.Contracts.Projects.*Request`, a client method named for `ManageProjects`). The names are the story's indicative ones; on activation, use typed references to whatever dev-story wrote.
- **Isolation rows are pending rows, not skipped facts.** The list's own mechanism for "not yet" is `PendingUntil`; each row already holds its real `Prepare` and real expectation, so activation is deleting `PendingUntil`/`PendingReason` and swapping the scaffold route for the contract one.

### Container budget

**No new engine and no new fixture type.** The slice scaffolds are one class, `ProjectEndpointTests`, on the existing `MigratedDatabaseFixture` (a database per class on the Slices engine). The isolation rows run on the existing `IsolationWorldFixture`. The browser cases join `ClientSurfaceCollection` and start no Host. Today every new container-backed case is skipped, so the class fixture builds nothing.

### Scenario map

| id | AC | Level | Pri | Scenario | File |
|---|---|---|:--:|---|---|
| `2.1-GATE-003` | 12 | INT (data) | P0 | Every pending row names a story still `backlog`/`ready-for-dev`/`in-progress`, with a reason; status file read (`1-9-` key) | `Isolation/IsolationCaseListTests.cs` |
| `2.1-GATE-003-ORACLE` (x10) | 12 | INT (data) | P0 | Synthetic pending row against an in-memory status source: passes at backlog, ready-for-dev, in-progress; reported at review, done, unknown; reported when its story is absent; reported with no reason (null, empty, blank) | `.IsolationCaseListTests` |
| `2.1-ISO-001` | 8 | INT (wire) | P0 | ISO-RECORD-KIND-INSUFFICIENT-ROLE (converted shape, still pending): a Viewer's Session and Token `POST /projects` = `403 insufficient-role`, `capability manage-projects`, +1 `InsufficientRole`, +0 `CrossSpace`, nothing written | `Isolation/IsolationCases.cs` + `Expectation.cs` (`RoleRefused`) |
| `2.1-ISO-002` | 8 | INT (wire) | P0 | ISO-DIRECT-PROJECTS: `GET /spaces/{SB}/projects` = anchor 404, one `CrossSpace` | `.IsolationCases` |
| `2.1-ISO-003` | 8, 4 | INT (wire) | P0 | ISO-LIST-PROJECTS-AGGREGATE: SA's listing = exactly SA's Projects (database view), none of SB's ids or names | `.IsolationCases` |
| `2.1-ISO-004` | 8 | INT (wire) | P0 | ISO-WRITE-PROJECT: `POST /spaces/{SB}/projects` = anchor 404, nothing written in SB | `.IsolationCases` |
| `2.1-ISO-005` | 8, 7 | INT (wire) | P0 | ISO-RENAME-FOREIGN-SPACE: `PATCH /spaces/{SB}/projects/{PB}` = anchor 404, PB not renamed | `.IsolationCases` |
| `2.1-ISO-006` | 8 | INT (wire) | P0 | ISO-RENAME-FOREIGN-ID-IN-OWN-SPACE: `PATCH /spaces/{SA}/projects/{PB}` = a random Project id in SA, byte for byte; no refusal row | `.IsolationCases` |
| `2.1-SLC-001` | 2 | INT (wire) | P0 | Create: `201`, body exactly `{createdAt,id,name}`, trimmed name, `Location` = Project path, row's `SpaceId` = active Space, `CreatedAt` UTC | `Slices/Projects/ProjectEndpointTests.cs` |
| `2.1-SLC-002` | 2 | INT (wire+db) | P0 | Create leaves the Space's three default Statuses unchanged; no Status column on `Project`, no delta/effective-set table (Q2 (a), AD-16) | `.ProjectEndpointTests` |
| `2.1-SLC-003` | 6 | INT (wire+db) | P0 | `Project.Id` has no default; ids in creation order sort in creation order under SQL Server ordering | `.ProjectEndpointTests` |
| `2.1-SLC-004` (x3) | 2 | INT (wire) | P1 | Empty, blank, 101-character name = `400 invalid-project-name`, nothing written; anchor 100 characters accepted | `.ProjectEndpointTests` |
| `2.1-SLC-005` | 2, 8 | INT (wire) | P0 | A Viewer's Session and Token create = `403 manage-projects`, two `InsufficientRole` rows, nothing written; Owner anchor `201` | `.ProjectEndpointTests` |
| `2.1-SLC-006` | 3 | INT (wire) | P0 | Rename changes the name only (id, Space, `CreatedAt` unchanged), answers the renamed Project | `.ProjectEndpointTests` |
| `2.1-SLC-007` | 3, 8 | INT (wire) | P0 | A Viewer's rename on both surfaces = `403`, name unchanged | `.ProjectEndpointTests` |
| `2.1-SLC-008` | 8 | INT (wire) | P0 | Rename of an id matching nothing in the Space = boundary `404` bytes (vs an absent Space), and records nothing | `.ProjectEndpointTests` |
| `2.1-SLC-009` | 4 | INT (wire) | P0 | Listing ordered by name then id (Q8 (a)); duplicate names ordered as the database orders ids | `.ProjectEndpointTests` |
| `2.1-SLC-010` | 4 | INT (wire) | P0 | A Viewer's Session and Token listing = a Member's, byte for byte | `.ProjectEndpointTests` |
| `2.1-SLC-011` | 11 | INT (wire) | P0 | Token creates without anti-forgery (`201`); Session cookie without the header = `400 antiforgery-token-missing` | `.ProjectEndpointTests` |
| `2.1-SLC-012` | 11 | INT (wire) | P1 | `PUT` on both routes and `DELETE` on the item = `404 not-available`, no `Allow` (Q9 (a)); anchor GET `200` | `.ProjectEndpointTests` |
| `2.1-BND-001` | 5 | INT (wire) | P0 | 49 seeded; the 50th `201` (anchor); the 51st `409 bound-reached`, `bound projects-per-space`, `limit 50`; 50 rows; no refusal record | `.ProjectEndpointTests.Bound` |
| `2.1-BND-002` | 5 | INT (wire) | P0 | Two concurrent creations at 49 = exactly one `201` and one `409`; 50 rows | `.Bound` |
| `2.1-BND-003` | 9, 5 | INT (wire) | P0 | Replaying the 50th creation's key once full = its `201`; a fresh key = `409` (anchor) | `.Bound` |
| `2.1-IDM-001` (x2) | 9 | INT (wire) | P0 | Create replayed with the same key on Session and on Token = original status, body, `Location`; one row | `.ProjectEndpointTests.Idempotency` |
| `2.1-IDM-002` | 9 | INT (wire) | P0 | Same key, different request = `422 idempotency-key-reused`, nothing applied | `.Idempotency` |
| `2.1-IDM-003` | 9 | INT (wire) | P0 | Replay after a rename = the original response, not current state | `.Idempotency` |
| `2.1-IDM-004` | 9 | INT (wire) | P0 | Another Account's same key in its own Space is a first use, not a replay | `.Idempotency` |
| `2.1-IDM-005` | 9 | INT (wire) | P1 | A 256-character key = `400`, nothing written; 255 accepted | `.Idempotency` |
| `2.1-IDM-006` | 9 | INT (wire+db) | P0 | Token issuance replay = `201`, `no-store`, no `token` property, no plaintext; one `ApiToken` for the Account; no stored text value holds the secret | `.Idempotency` |
| `2.1-SCH-001` | 6 | INT (db) | P0 | `Project.SpaceId` non-nullable; policy predicates on `Project` = FILTER, BLOCK AFTER INSERT, BLOCK AFTER UPDATE | `.ProjectEndpointTests.Schema` |
| `2.1-SCH-002` | 7 | INT (db) | P0 | Under SA's key, `UPDATE Project SET SpaceId = SB` fails with error 33504 (FK satisfied - SB exists); anchor: a name update succeeds | `.Schema` |
| `2.1-SCH-003` | 6 | INT (db) | P1 | Unique key `(SpaceId, Id)` on `Project` (AR-8, Q4 (a)) | `.Schema` |
| `2.1-SCH-004` | 9 | INT (db) | P0 | `IdempotencyRecord`: non-nullable `SpaceId`, FILTER and BLOCK predicates, unique `(SpaceId, AccountId, Key)` | `.Schema` |
| `2.1-ARCH-001` | 10 | ARCH | P1 | Nothing but the pipeline reads `BoundRegistry` | `Architecture/RefusalBoundaryGateTests.Projects.cs` |
| `2.1-ARCH-002` | 10 | ARCH | P1 | Only the pipeline, `BoundCounter` and `InfrastructureServices` depend on `IBoundCounter` | `.RefusalBoundaryGateTests.Projects` |
| `2.1-ARCH-003` | 9 | ARCH | P1 | Only the pipeline, `IdempotencyStore` and `InfrastructureServices` depend on `IIdempotencyStore` (A-12) | `.Projects` |
| `2.1-ARCH-004` | 7 | ARCH | P0 | The Host maps exactly `GET`/`POST` on the collection and `PATCH` on the item under `/projects` | `.Projects` |
| `2.1-ARCH-005` | 7 | ARCH | P0 | `CreateProjectRequest` and `RenameProjectRequest` carry `Name` and nothing else | `.Projects` |
| `2.1-ARCH-006` | 7 | ARCH | P0 | `Project.SpaceId` has an `init` accessor only | `.Projects` |
| `2.1-ARCH-007` | 13 | ARCH | P0 | The client's ManageProjects predicate agrees with `RoleCapabilityMatrix` for every Role (Q12 (a)) | `.Projects` |
| `2.1-E2E-001` | 1 | E2E (fake) | P0 | Empty Space: "No Projects yet.", exactly one action "Create a Project", no Status/column/configure | `Client/SpaceHomeProjectsTests.cs` |
| `2.1-E2E-002` | 13 | E2E (fake) | P0 | List held: no names, no empty state, `aria-busy`; released, names render | `.SpaceHomeProjectsTests` |
| `2.1-E2E-003` | 2, 3, 4 | E2E (fake) | P0 | Owner sees create + one rename per row (anchor); Viewer sees the same list, no create, no rename, nothing disabled | `.SpaceHomeProjectsTests` |
| `2.1-E2E-004` | 2, 9, 13 | E2E (fake) | P0 | Create adds the row, announces politely, sends an `Idempotency-Key`; `autocomplete="off"`, non-reusable field name; title `Yello` | `.SpaceHomeProjectsTests` |
| `2.1-E2E-005` | 5 | E2E (fake) | P0 | `409 bound-reached` renders "This Space has 50 Projects, the maximum." in `main`; nothing added | `.SpaceHomeProjectsTests` |
| `2.1-E2E-006` | 13 | E2E (fake) | P0 | Switch with the destination's list held: nothing of either Space's Projects | `.SpaceHomeProjectsTests` |
| `2.1-E2E-007` | 3 | E2E (fake) | P1 | Rename is an explicit control; `Esc` cancels with no `PATCH`; commit labelled "Rename"; no "Save" | `.SpaceHomeProjectsTests` |

### Priorities

P0 for every case the isolation gate (SM-1), the bound (AR-30) or the idempotency guarantee rests on, and for the Role and schema properties AC2-AC7 state. P1 for name validation, the method table, the key-length rule, the `(SpaceId, Id)` key, the A-12 port gates and the rename interaction.

### Duplicate-coverage guard - deliberately not written

- **"No Project from another Space" is not a slice case.** `ISO-LIST-PROJECTS-AGGREGATE` proves it on both surfaces; a slice copy would test the same property at the same level.
- **The capability matrix itself** is already transcribed and gated (`The_capability_matrix_agrees_with_the_transcription_in_every_cell`); the new `403` cases prove the route declares `ManageProjects`, not the matrix.
- **The bound copy's wording** is already held by `RefusalCopyTests` and `ClientCopy.resx:422-425`; E2E-005 proves only that the page renders it inline.
- **`SpaceIsolationSchemaTests` covers `Project` automatically** once Task 3 adds it to `SchemaNames.SpaceScopedTables`; SCH-001 adds only what that test cannot see (the `AFTER UPDATE` predicate).

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| Handler-level slice tests (`SpaceId` from the context, id equals the generator's value) | `Yello.Application.Projects` and the store port do not exist; reflection over a handler's shape would be a guess. SLC-001 and SLC-003 cover the observable half over the wire | Task 11 |
| `ProjectName.TryNormalise` unit cases | The type does not exist; SLC-004 covers the rule over the wire | Task 3 |
| Plants that edit product code (two-layer disclosure, bound in the slice, port in a handler, plaintext stored, Viewer control rendered, list in `sessionStorage`, name in `document.title`, `AFTER UPDATE` removed) | Each needs the product this phase does not build. The scaffolds above are the gates that must catch them | Task 12 plant table |
| `ISO-TIMING-IN-SPACE` conversion | Q13 (a) keeps it pending for 2.2; its reason text becomes false once rename exists and is updated by Task 9 | Task 9 |
| Idempotency on Account-scoped and anonymous routes | Out of scope by Q6 (a); the reasons are Completion Notes | Task 7 |

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents; generation stayed in-thread, as stories 1.5-1.10 recorded, because C# under this analyser set has to be built and fixed here anyway. Build fixes on the way: `MA0181` (x3), `S4457` (x2), `CS0419`, `CA1861` (x2), `CA2100`, `CA1849`, `CS8602` (x2), `IDE1006` (x2), `S3776`, `CS0128`, `S4040`, `MA0182`/`MA0211` (plant only). Worker A's output is the Slices, Isolation and Architecture files; worker B's is the browser class and the fake.

### Task 1 gate correction applied in ATDD; dev verifies

`tests/Yello.Tests.Isolation/IsolationCaseListTests.cs`, AC12, Q1 (c), at the orchestrator's instruction:

- **The rule.** `Every_pending_row_names_a_story_that_is_still_in_backlog` is now `Every_pending_row_names_a_story_that_has_not_yet_reached_review`. A pending row passes while its story is `backlog`, `ready-for-dev` or `in-progress` (`StatusesWhileARowMayBePending`), and fails at `review`, `done` or **any other status** - closed by default. The failure message names the rule and says to convert the row before review. Name, summary and message updated.
- **The vacuity guard, rebuilt.** `pending.Count >= 2` is gone: it would have failed the moment Task 9 converts 2.1's rows and again when 2.2 converts its own. The live gate now keeps only "the status file was read and holds a `1-9-` key"; the proof that the gate reports what it should moved to **three permanent oracle tests over synthetic rows and an in-memory status source** - `The_pending_row_gate_reports_a_row_whose_story_has_reached_review` (6 rows: three pass, three reported), `..._whose_story_is_not_in_the_status_file`, `..._with_no_reason` (3 rows). The live gate and the oracle call one function, `StalePendingRows`, so the oracle tests the code the gate runs.
- **Docs.** The `IsolationCases` remark and the one sentence in `TESTING-CONVENTIONS.md` that stated the old rule now state the new one.
- **Left for dev (Task 1):** verify, then close `deferred-work.md:411` with the dated note; record Q1 (c) in the Completion Notes.

### `tests/Yello.Tests.Isolation` - 6 rows (pending), 10 oracle cases (active), 1 gate corrected (active)

| File | Change |
|---|---|
| `IsolationCaseListTests.cs` | GATE-003 corrected; 3 oracle tests (10 cases), **all active and green** |
| `IsolationCases.cs` | `ISO-RECORD-KIND-INSUFFICIENT-ROLE` converted to its real shape (`POST` create, a Viewer of its own Space on each surface - `UX_Membership_SpaceId_Owner` is filtered and permits a Space with no Owner) and five new rows, all `PendingUntil: "2.1"` with real `Prepare` and expectations; helpers `SeedProjectAsync` (through the product, name added to the actor's secrets for I-17), `ViewerCreatesAProjectAsync`, `NoProjectWrittenAsync`, `NothingWrittenInTheForeignSpaceAsync`, `ListsExactlyTheCallersProjectsAsync` |
| `Expectation.cs` | `RoleRefused(capability)`: `403 insufficient-role` with the capability, +1 `InsufficientRole` and +0 `CrossSpace` per path, I-13 |

### `tests/Yello.Tests.Slices/Projects` - 28 cases in 25 skipped entries, one class

| File | Cases | ids | ACs |
|---|---|---|---|
| `ProjectEndpointTests.cs` | 14 (12 entries) | SLC-001 to SLC-012 | AC2, AC3, AC4, AC6, AC8, AC11 |
| `ProjectEndpointTests.Bound.cs` | 3 | BND-001 to BND-003 | AC5, AC9 |
| `ProjectEndpointTests.Idempotency.cs` | 7 (6 entries) | IDM-001 to IDM-006 | AC9 |
| `ProjectEndpointTests.Schema.cs` | 4 | SCH-001 to SCH-004 | AC6, AC7, AC9 |

### `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Projects.cs` - 7 cases

| Test | id | Status |
|---|---|---|
| `Nothing_but_the_pipeline_reads_the_bound_registry` | ARCH-001 | **ACTIVE - green today, standing gate, planted** |
| `Only_the_pipeline_depends_on_the_bound_count_port` | ARCH-002 | skipped - red: AC10 |
| `Only_the_pipeline_depends_on_the_idempotency_port` | ARCH-003 | skipped - red: AC9 |
| `No_route_moves_a_Project` | ARCH-004 | skipped - red: AC7 |
| `No_Project_request_contract_carries_anything_but_a_name` | ARCH-005 | skipped - red: AC7 |
| `A_Projects_Space_can_only_be_set_when_it_is_created` | ARCH-006 | skipped - red: AC7 |
| `The_clients_Project_controls_predicate_agrees_with_the_capability_matrix` | ARCH-007 | skipped - red: AC13 |

`RefusalBoundaryGateTests` became `partial` (one keyword) so the new file reuses `DependentsOf`, `RouteTable` and `PipelineTypeName`.

### `tests/Yello.Tests.Client/SpaceHomeProjectsTests.cs` - 7 cases, all skipped

E2E-001 to E2E-007, in `ClientSurfaceCollection` (one dev server, one Chromium, no Host).

### Harness added (test code, active, no case depends on it yet)

- `Yello.Tests.Shared/ProjectRouteScaffold.cs` - the routes and problem types (temporary; deleted on activation).
- `WireSession.SendWithHeadersAsync` / `SendWithTokenAndHeadersAsync` - any method, a body, extra headers (`PATCH`, `Idempotency-Key`); the Token form still attaches no cookie and no anti-forgery header.
- `ProductActor`: `PATCH` on both surfaces and on the no-credential anchor; a body-carrying `SendAsync(surface, method, path, body, headers)` (Task 9's overload - dev verifies, does not rewrite); `HoldsProjectNamed` adds a Project name to `Secrets`.
- `WireDatabase.ProjectsInAsync(spaceId)` - (id, name) under the Space's own key.
- `FakeYelloApi`: `Projects`, `ProjectBoundReached`, `HoldProjectsAsync`/`ReleaseProjects`; `GET`/`POST` on the collection and `PATCH` on the item; CORS now allows `PATCH` and `Idempotency-Key`. `FakeProject` stands for `ProjectResponse`.

### Fixtures and seed data - every constraint checked

No new fixture type and no new container-backed class beyond `ProjectEndpointTests`, which takes the existing `MigratedDatabaseFixture` (a database of its own on the Slices engine). Checked against `20260828164716_InitialSchema.cs`, `20261024121107_SpaceBoundaryRefusals.cs`, `20261024143930_AccountScopedMembership.cs`, `20261024145154_SessionRevocation.cs` and `20261024164849_ApiTokens.cs`, and against story 2.1 Task 3 for the table to come:

- **Through the product:** Accounts, Personal Spaces, Owner Memberships, Tokens and (in the isolation rows and most slice cases) Projects. `UX_Account_NormalizedEmailAddress` holds because every address is random; `FK_Membership_Account_AccountId`, `FK_Membership_Space_SpaceId`, `UX_Membership_SpaceId_Owner` and `FK_ApiToken_Membership_BoundSpaceId_AccountId` hold by construction.
- **`AddMemberAsync` (Slices):** a `Member` or `Viewer` Membership for an Account the product registered, into an existing Space, under that Space's key (RLS `BLOCK AFTER INSERT` on `Membership`), once per (Space, Account) (`AK_Membership_SpaceId_AccountId`), never a second Owner.
- **`ChangeRoleAsync` to `viewer` (Isolation):** the actor's own Personal Space, leaving it with no Owner, which the filtered unique `UX_Membership_SpaceId_Owner` permits (as 1.9 recorded).
- **`SeedProjectsAsync` (Slices, bound cases):** `INSERT dbo.Project (Id, SpaceId, Name, CreatedAt)` under the Space's key into an existing Space - the FK to `Space` Task 3 declares (`Cascade`, as `StatusDefinition`) and the `AFTER INSERT` block predicate both hold; `Id` is supplied because Task 3 maps it `ValueGeneratedNever`. **If the migration adds another non-nullable column without a default, correct this one statement.**
- **SCH-002** moves a Project to **another real Space** (the second Account's), so the foreign key is satisfied and only the `AFTER UPDATE` block predicate can refuse; it asserts SQL error **33504**, not "any exception".
- **IDM-006** counts `ApiToken` rows **for the Account**, not the table: the class's database is shared by its cases.

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Test run

```
dotnet test Yello.slnx --no-build
Test run summary: Passed!   total: 601   failed: 0   succeeded: 560   skipped: 41   (2m 26s)
```

Delta against the entry state (GATE-003 red): **+49 cases** (552 to 601) - 11 new active and green (10 GATE-003 oracle cases, ARCH-001), 38 new skipped (25 Slices entries, 6 ARCH, 7 Client), and GATE-003 itself green again. `YELLO_MIGRATION_BUNDLE` was not set, so the two bundle-equivalence cases skip, as on any developer machine. Still four engines; the browser suite passed in the combined run.

### The corrected gate and the new standing gate were planted - 2026-10-25

Original content was held in memory and restored in a `finally`; the restored file's SHA-256 matched (P1), and the planted file was removed (P2). No product file is modified (`git status` shows only test files and this checklist).

| # | Plant | Gate | Caught |
|---|---|---|---|
| 1 | `sprint-status.yaml`: `2-1-...: ready-for-dev` -> `review` | GATE-003 (live) | **yes**: all six 2.1 rows reported, "pending until 2.1, which is now 'review' - a row may stay pending only while its story is backlog, ready-for-dev, in-progress; convert the row before the story reaches review" |
| 2 | `Yello.Application/Projects/PlantedBoundReader.cs` reading `BoundRegistry.For(Bound.ProjectsPerSpace).Limit` | ARCH-001 | **yes**: "Yello.Application.Projects.PlantedBoundReader read BoundRegistry" |

The GATE-003 oracle is itself a permanent plant: the `review`, `done` and `optional` rows are synthetic pending rows at a post-review status, and the absent-story and no-reason rows are the other two failure shapes.

### Red-phase compliance

- 38 of 49 new cases are skipped with `red: AC<n> - <why> (Task n)`; 6 isolation rows are pending with `red: AC8 - ...` as their reason.
- 11 are active and green: the 10 GATE-003 oracle cases and ARCH-001. All are standing gates, and both kinds were planted.
- Every scaffold compiles under warnings-as-errors. No scaffold references a type story 2.1 has not written, except by name or through `ProjectRouteScaffold`.
- Every absence is anchored on a presence in the same case (the 50th before the 51st, the Owner's `201` beside the Viewer's `403`, a name update beside the refused move, a fresh key's `409` beside the replay's `201`, a Member's controls beside the Viewer's absence).

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

0. **Task 1 - verify the gate correction** (already applied). Run `IsolationCaseListTests`; all 14 green. Record Q1 (c), close `deferred-work.md:411`.
1. **SCH-002 then SCH-001 first** (Task 3). They test the riskiest assumption the rest stands on: that SQL Server accepts a `BLOCK ... AFTER UPDATE` predicate added to the shared `SpaceIsolationPolicy` in an expand-only migration, and that it - not the foreign key - refuses a `SpaceId` change (error 33504) while a name change under the same key succeeds. Nothing in this repository has used `AFTER UPDATE` yet. Then widen `SpaceIsolationSchemaTests.Every_Space_scoped_table_carries_a_filter_and_a_block_predicate` for `Project`'s third predicate (exactly three for `Project`, two for every other table), and activate **SCH-003** and **ARCH-006**.
2. **SLC-001, SLC-003, SLC-004, SLC-006, SLC-009, SLC-002** (Tasks 4-5), then **ARCH-004** and **ARCH-005**. Replace `ProjectRouteScaffold` with `ProjectRoutes`/`ProblemTypes` as each activates.
3. **The isolation rows** (Task 9): delete `PendingUntil`/`PendingReason` from the six 2.1 rows one at a time. `ISO-RECORD-KIND-INSUFFICIENT-ROLE` first - the first wire `403`. Then **SLC-005**, **SLC-007**, **SLC-010**, **SLC-008**. GATE-002 must stay green (every mapped route named); GATE-003 fails at `review` if any 2.1 row is still pending.
4. **SLC-011, SLC-012** (Task 8).
5. **BND-001, then BND-002** (Task 6), then **ARCH-002**. BND-002 is the race: if it passes without the `UPDLOCK, HOLDLOCK` count, it did not race - run it several times and record it.
6. **Idempotency** (Task 7): **IDM-006 first** (severity high: NFR-6), then IDM-001 (both surfaces), IDM-002, IDM-003, IDM-004, IDM-005, **BND-003** (replay before the bound), **SCH-004**, **ARCH-003**.
7. **Client** (Task 10): E2E-001, E2E-002, E2E-003 with **ARCH-007**, E2E-004, E2E-005, E2E-006, E2E-007. Correct locators to the real markup if needed.
8. Keep **GATE-003 and its oracle** and **ARCH-001** green throughout. Task 12's product plants run against the activated cases.

### Commands

```
dotnet build Yello.slnx
dotnet test Yello.slnx --no-build
dotnet test --project tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj --no-build -- --filter-class Yello.Tests.Isolation.IsolationCaseListTests
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.Projects.ProjectEndpointTests
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.RefusalBoundaryGateTests
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.SpaceHomeProjectsTests
```

`--no-build` runs zero tests unless the same configuration was just built. If a Client case fails only in the full run, run the Client project alone before treating it as real.

## Notes and risks

- **`ProjectRouteScaffold` and the by-name gate subjects carry the largest correction risk.** They are the story's indicative names. If Q6 or the port names land differently, correct the scaffold, not the product, and record it.
- **`SpaceIsolationSchemaTests` will go red** the moment `Project` joins `SpaceScopedTables` with three predicates (activation step 1). That is expected; widen it for `Project` only.
- **ISO-RECORD-KIND-INSUFFICIENT-ROLE's Viewer is the actor's own Personal Space demoted**, not a second member of A's Space, so the Token and the Session both hold exactly Viewer there without a new harness operation. The property (a `403`, recorded as `InsufficientRole`, nothing written) is the same; if dev-story prefers the story table's shape, change `Prepare` only.
- **The isolation write rows send `ProductActor`'s valid body** (`{"name":"from an isolation case"}`), which is a valid Project body as well as a Token body; their `Then` checks that no Project in B's Space carries that name.
- **E2E-004 assumes a polite live region** (`[aria-live='polite']`) carries the completion announcement; if the client announces through a shared announcer, point the locator at it.
- **Container cost:** none today. On activation, `ProjectEndpointTests` is one more database on the Slices engine, not an engine; each case starts a Host process, as `ActiveSpaceEndpointTests` does.

---

**Generated by BMad TEA Agent** - 2026-10-25
