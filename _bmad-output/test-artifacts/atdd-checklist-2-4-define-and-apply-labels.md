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
storyId: '2.4'
storyKey: '2-4-define-and-apply-labels'
storyFile: '_bmad-output/implementation-artifacts/2-4-define-and-apply-labels.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-2-4-define-and-apply-labels.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Shared/LabelScaffold.cs'
  - 'tests/Yello.Tests.Shared/ProductActor.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
  - 'tests/Yello.Tests.Slices/Tasks/LabelWire.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.Labels.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.LabelsSchema.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.LabelsRace.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.LabelsIdempotency.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Isolation/Expectation.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Labels.cs'
  - 'tests/Yello.Tests.Architecture/CieDe2000.cs'
  - 'tests/Yello.Tests.Architecture/LabelPaletteGateTests.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.Labels.cs'
  - 'tests/Yello.Tests.Architecture/RefusalCopyTests.Labels.cs'
  - 'tests/Yello.Tests.Client/LabelTests.cs'
  - 'tests/Yello.Tests.Client/TextSpacingTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/2-4-define-and-apply-labels.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-2-3-edit-a-tasks-title-status-and-due-date.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 2, Story 2.4: Define and apply Labels

**Date:** 2026-10-25
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server (`Yello.Tests.Slices`, new partials of
2.2's `TaskEndpointTests` on the shared engine), isolation rows on both surfaces (`Yello.Tests.Isolation`), ARCH
gates (`Yello.Tests.Architecture`, including the computed palette gate), and browser cases against the
network-edge fake (`Yello.Tests.Client`).

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every
earlier story recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by
`tests/TESTING-CONVENTIONS.md`, so the chip, picker and Labels-surface cases are written there.

**Prerequisites - met.** Story `ready-for-dev` (`sprint-status.yaml:93`); AC1-AC6 verbatim from `epics.md:1105-1130`
(`status: final`), AC7-AC14 cite their sources. MTP, six test projects, .NET 10 SDK, Rancher. No checklist and no
progress file existed for 2.4, so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
`tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. Where a
fragment and `tests/TESTING-CONVENTIONS.md` disagree, the repository document wins.

**Entry state:** green after 2.3's review (850 total, 847 passed, 3 skipped). No isolation row was pending on any
story.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack; no recording. Sources: the story's ACs, Tasks, isolation-row
table and fourteen open questions; the live schema (`dbo.[Task]` after `20261025115412_TaskTable`) and harness;
2.3's scaffolds and their activated form as the pattern.

## Step 3 - Test strategy

### The constraint that shapes every scaffold

No production type of this story exists: no `Yello.Domain.Labels.Label`, no `LabelRoutes`, no `LabelResponse`, no
Label problem type, no `Label` or `TaskLabel` table, no `labelIds` member, no chip, picker or Space settings page.
So, as 2.1-2.3 did:

- **The routes and the five problem types live in one test-side file**, `tests/Yello.Tests.Shared/LabelScaffold.cs`.
  On activation each use is replaced by the contract constant (`LabelRoutes.List`/`Create`/`Label`,
  `ProblemTypes.InvalidLabelName` ...) and the file is deleted. The Architecture suite and the Client fake carry the
  same literals as private constants.
- **Wire cases read raw JSON and send JSON text or anonymous objects**, so they compile today and an added property
  cannot hide.
- **Architecture gates reach unwritten types by name** (`Yello.Domain.Labels.Label`, `LabelPalette`,
  `Yello.Contracts.Labels.LabelResponse`, a client method named for `ManageLabels`).
- **Isolation rows are pending rows, not skipped facts**, each with its real `Prepare` and expectation.
- **SQL against the two new tables is written to story Task 3's shape** and only runs once a case is activated.

### Open-question dependencies (scaffolded against the recommended option)

| Q | Recommended | Where the scaffold depends on it | If decided otherwise |
|---|---|---|---|
| Q1 | (a) `labelIds` on the Task `PATCH`, the whole set | every apply case; ISO-APPLY-FOREIGN-LABEL; ARCH-006; E2E body asserts | Re-own the apply helpers in `LabelWire` and the fake's `Apply` |
| Q2 | trimmed 1-50, unique per Space case-insensitively, `409 label-name-taken` | LBL-007, LBL-009, RACE-003; `LabelScaffold.NameMaxLength` | Correct the constant and the status |
| Q3 | a palette key (`rust` ...), `400 invalid-label-colour` | LBL-001, LBL-008, PAL-002, the fake | Correct `LabelScaffold.PaletteKeys` |
| Q4 | one fixed `--label-ink` for every fill | PAL-001, PAL-003's ink plant | Correct the ink lookup |
| Q5 | `+N` is non-interactive text named "{N} more Labels" | E2E-201 | Correct the locator |
| Q6 | no NFR-8 bound; `labelIds` length guarded as input shape | LBL-012's oversize element (1,001 ids) | Correct `LabelScaffold.OversizeLabelIds` |
| Q7 | Space settings page at `/spaces/{id}/settings`, linked from Space home for Owner/Admin | E2E-208, E2E-211, E2E-212, E2E-213, A11Y-001 | Correct `SettingsPath` and the link name |
| Q8 | pre-select the first unused palette entry | E2E-212 (asserts one radio checked, eight offered) | Drop the "one checked" assert |
| Q9 | `TaskLabel` Infrastructure-only | nothing on the wire | - |
| Q10 | no rename/recolour | nothing | Add isolation rows for a `PATCH` |
| Q11 | refuse with `409 label-in-use`, no count | LBL-013, RACE-001, E2E-211 | - |
| Q12 | server order by name then id | LBL-015, LBL-016, E2E-201 | Correct the order asserts |
| Q13 | "Label added." / "Label deleted." polite; none on apply | E2E-212 (polite region) | Drop the announcement assert |
| Q14 | native radio group, `legend` "Colour" | E2E-212, E2E-216 | Correct the locators |

### Container budget

**No new engine, no new fixture type, no new container-backed class.** Every Slices case is a new partial of 2.2's
`TaskEndpointTests` (one database on the Slices engine). The isolation rows run on the existing
`IsolationWorldFixture`. The browser cases join `ClientSurfaceCollection` and start no Host. The palette gate reads
text. Every new container-backed case is skipped today.

### Scenario map

| id | AC | Level | Pri | Scenario | File | State |
|---|---|---|:--:|---|---|---|
| `2.4-LBL-001` (x2) | 1, 11 | INT (wire+db) | P0 | Owner defines a Label on Session and Token: `201`, body exactly `{colour,id,name}`, name trimmed, `Location` its own path; it lists; the row agrees | `Slices/Tasks/TaskEndpointTests.Labels.cs` | skip AC1 |
| `2.4-LBL-002` | 1, 2 | INT (wire+db) | P0 | An Admin defines one; a Member applies it to Tasks in two different Projects of the Space | `.Labels` | skip AC1 |
| `2.4-LBL-003` (x2) | 1, 9 | INT (wire+db) | P0 | A Member / a Viewer creating and deleting a Label on both surfaces: four `403 manage-labels`, four `InsufficientRole`, nothing written, the Label still listed | `.Labels` | skip AC9 |
| `2.4-LBL-004` | 2, 7 | INT (wire+db) | P0 | A Member applies two Labels (`200`, answer and `SELECT` agree, server order), `[]` clears, an edit without `labelIds` leaves them | `.Labels` | skip AC2 |
| `2.4-LBL-005` | 7 | INT (wire+db) | P1 | Labels survive title, Status and due-date edits; a Labels-only edit touches no other column | `.Labels` | skip AC7 |
| `2.4-LBL-006` | 2, 9 | INT (wire+db) | P0 | A Viewer's `labelIds` edit on Session and Token: `403 manage-tasks`, two `InsufficientRole`, applications unchanged; Member anchor `200` | `.Labels` | skip AC2 |
| `2.4-LBL-007` (x3) | 11 | INT (wire+db) | P1 | Name `""`, blank, 51 characters: `400 invalid-label-name`, no row, no refusal; 50-character anchor `201` | `.Labels` | skip AC11 |
| `2.4-LBL-008` (x3) | 4, 11 | INT (wire+db) | P1 | Colour `magenta`, `#D2592D`, `null`: `400 invalid-label-colour`; `rust` anchor | `.Labels` | skip AC4 |
| `2.4-LBL-009` | 11 | INT (wire+db) | P0 | Same name differently cased: `409 label-name-taken`, no name echoed, one row; the same name in another Space `201` | `.Labels` | skip AC11 |
| `2.4-LBL-010` (x3) | 11 | INT (wire) | P1 | Unreadable body, `{}`, a JSON array: `400`, never `500`, no refusal row | `.Labels` | skip AC11 |
| `2.4-LBL-011` | 8, 11 | INT (wire+db) | P0 | `invalid-task-labels` for a random id, another Space's real Label id (byte-identical), duplicate ids, a deleted Label's id; nothing changed, no id echoed, no refusal row | `.Labels` | skip AC11 |
| `2.4-LBL-012` (x4) | 11 | INT (wire) | P1 | `labelIds: null`, a string, 1,001 ids: `400 invalid-task-edit`; a non-GUID element: `400 invalid-task-labels` | `.Labels` | skip AC11 |
| `2.4-LBL-013` | 3 | INT (wire+db) | P0 | Delete unused `204`; delete applied `409 label-in-use` (no count, no title), still listed, applications intact; remove, delete `204` | `.Labels` | skip AC3 |
| `2.4-LBL-014` | 8 | INT (wire) | P0 | `DELETE` of an id that matches nothing: boundary `404` bytes vs an absent Space, no refusal row | `.Labels` | skip AC8 |
| `2.4-LBL-015` (x2) | 1, 14 | INT (wire) | P1 | `GET` list on both surfaces orders by name then id, rows exactly `{colour,id,name}`; a Viewer reads it | `.Labels` | skip AC1 |
| `2.4-LBL-016` | 7 | INT (wire) | P0 | The Board and `GET` Task carry each Task's `labelIds` in server order; a Task with none carries `[]` | `.Labels` | skip AC7 |
| `2.4-SCH-001` | 6 | INT (db) | P0 | `Label` and `TaskLabel`: non-nullable `SpaceId`, FILTER + BLOCK AFTER INSERT/UPDATE | `.LabelsSchema` | skip AC6 |
| `2.4-SCH-002` | 6 | INT (db) | P0 | Moving a Label to another Space is refused `33504`; anchor: a rename in SQL | `.LabelsSchema` | skip AC6 |
| `2.4-SCH-003` | 3, 6 | INT (db) | P0 | `TaskLabel -> Label` `NO_ACTION`, `TaskLabel -> Task` `CASCADE`, both on `SpaceId`; `Label -> Space` `CASCADE` | `.LabelsSchema` | skip AC3 |
| `2.4-SCH-004` | 6, 10 | INT (db) | P1 | Unique keys: `Task (SpaceId, Id)`, `Label (SpaceId, Id)`, `Label (SpaceId, Name)`, `TaskLabel (SpaceId, TaskId, LabelId)`; index `(SpaceId, LabelId, TaskId)` | `.LabelsSchema` | skip AC6 |
| `2.4-SCH-005` | 3 | INT (db) | P0 | The database refuses deleting an applied Label (`547`); anchor: an unused one deletes | `.LabelsSchema` | skip AC3 |
| `2.4-RACE-001` | 10 | INT (wire+db) | P0 | Test holds an uncommitted application; `DELETE` blocks on `LCK_M_%`; commit; `409 label-in-use`, Label and application both present | `.LabelsRace` | skip AC10 |
| `2.4-RACE-002` | 10 | INT (wire+db) | P0 | Test holds an uncommitted Label delete; the apply blocks; commit; `400 invalid-task-labels`, no `TaskLabel` row | `.LabelsRace` | skip AC10 |
| `2.4-RACE-003` | 11 | INT (wire+db) | P0 | Test holds an uncommitted Label of the same name; `POST` blocks; commit; `409 label-name-taken`, never `500` | `.LabelsRace` | skip AC11 |
| `2.4-IDM-001` (x2) | 11 | INT (wire+db) | P0 | `POST` replay: one Label, identical bodies | `.LabelsIdempotency` | skip AC11 |
| `2.4-IDM-002` (x2) | 11 | INT (wire+db) | P0 | Same key, different body: `422 idempotency-key-reused`, nothing written | `.LabelsIdempotency` | skip AC11 |
| `2.4-IDM-003` (x2) | 11 | INT (wire+db) | P0 | `DELETE` replay: both `204`, the second not a `404` | `.LabelsIdempotency` | skip AC11 |
| `2.4-IDM-004` (x2) | 11 | INT (wire+db) | P0 | `labelIds` replay after another key's edit: the first body, not re-applied | `.LabelsIdempotency` | skip AC11 |
| `2.4-ISO-001` | 8 | INT (wire) | P0 | ISO-LIST-LABELS-FOREIGN-SPACE: `GET LabelsOf(SB)` boundary `404` | `Isolation/IsolationCases.cs` | pending 2.4 |
| `2.4-ISO-002` | 8 | INT (wire) | P0 | ISO-LIST-LABELS-OWN-SPACE: `200`, exactly SA's Label ids, no SB name or id | `.IsolationCases` | pending 2.4 |
| `2.4-ISO-003` | 8 | INT (wire) | P0 | ISO-CREATE-LABEL-FOREIGN-SPACE: boundary `404`, nothing written in either Space | `.IsolationCases` | pending 2.4 |
| `2.4-ISO-004` | 8 | INT (wire) | P0 | ISO-DELETE-LABEL-FOREIGN-SPACE: boundary `404`, B's Label still present | `.IsolationCases` | pending 2.4 |
| `2.4-ISO-005` | 8 | INT (wire) | P0 | ISO-LABEL-FOREIGN-ID-IN-OWN-SPACE: `DELETE LabelOf(SA, LB)` = random-id bytes, no refusal row, B's Label present (the two-layer plant) | `.IsolationCases` | pending 2.4 |
| `2.4-ISO-006` | 8 | INT (wire) | P0 | ISO-APPLY-FOREIGN-LABEL: `PATCH` own Task `labelIds: [LB]` = `[random]` bytes, `400 invalid-task-labels`, nothing written | `.IsolationCases` | pending 2.4 |
| `2.4-ISO-007` | 9 | INT (wire) | P0 | ISO-RECORD-KIND-LABEL-DEFINE-MEMBER: `403 manage-labels`, one `InsufficientRole`, no Label | `.IsolationCases` | pending 2.4 |
| `2.4-ISO-008` | 9 | INT (wire) | P0 | ISO-RECORD-KIND-LABEL-APPLY-VIEWER: `403 manage-tasks`, Labels unchanged | `.IsolationCases` | pending 2.4 |
| `2.4-ARCH-001` | 8, 9 | ARCH | P0 | Label routes: `GET` (`ReadProjectsAndTasks`), `POST`/`DELETE` (`ManageLabels`), none Session-only, no bound | `Architecture/RefusalBoundaryGateTests.Labels.cs` | skip AC8 |
| `2.4-ARCH-002` | 6 | ARCH | P0 | `Yello.Domain.Labels.Label`: `SpaceId` init-only, no navigation property | `.Labels` | skip AC6 |
| `2.4-ARCH-003` | 8 | ARCH | P0 | Every name in `SpaceScopedEntities`, `Label` included, resolves to a Domain type | `.Labels` | skip AC8 |
| `2.4-ARCH-004` | 11 | ARCH | P0 | `Yello.Contracts.Labels` holds `LabelResponse {Colour, Id, Name}` and `CreateLabelRequest {Colour, Name}`; no `SpaceId` | `.Labels` | skip AC11 |
| `2.4-ARCH-005` | 7 | ARCH | P0 | `TaskResponse` gains `LabelIds` (`IReadOnlyList<Guid>`), keeping every earlier field | `.Labels` | skip AC7 |
| `2.4-ARCH-006` | 7 | ARCH | P0 | `EditTaskRequest` is exactly `{DueDate, LabelIds, StatusId, Title}`, `LabelIds` `IReadOnlyList<Guid>?` | `.Labels` | skip AC7 |
| `2.4-ARCH-007` | 1, 14 | ARCH | P0 | The client's `ManageLabels` predicate agrees with the matrix | `.Labels` | skip AC1 |
| `2.4-PAL-000` | 12 | ARCH | P0 | CIEDE2000 known answers (Sharma, Wu & Dalal 2005), before it is trusted | `Architecture/LabelPaletteGateTests.cs` | **ACTIVE, green** |
| `2.4-PAL-000-ORACLE` | 4, 12 | ARCH | P0 | The AC4 rule passes the story's eight fills and reports `#8B80F9`, `#FFFFFF` and an ink set to `text-primary` | `.LabelPaletteGateTests` | **ACTIVE, green (permanent plant)** |
| `2.4-PAL-001` | 4, 12 | ARCH | P0 | Every `--label-*` fill in `tokens.css` meets AC4 in both themes | `.LabelPaletteGateTests` | skip AC12 |
| `2.4-PAL-002` | 12 | ARCH | P0 | The `--label-*` fills are exactly the Domain `LabelPalette` keys, plus `--label-ink` | `.LabelPaletteGateTests` | skip AC12 |
| `2.4-PAL-003` | 4, 12 | ARCH | P1 | No theme-boundary rule rebinds a `--label-*` token (one fill in both themes) | `.LabelPaletteGateTests` | skip AC12 |
| `2.4-DSN-001` | 13 | ARCH | P0 | `.label-chip` radii resolve to 2px (`deferred-work.md:36`) | `Architecture/DesignFoundationGateTests.Labels.cs` | skip AC13 |
| `2.4-DSN-002` | 13 | ARCH | P0 | `.label-chip` declares no fixed height and no shadow other than `none` | `.DesignFoundationGateTests.Labels` | skip AC13 |
| `2.4-DSN-003` | 5, 13 | ARCH | P0 | `.task-card__meta` wraps (`flex-wrap: wrap`) and never scrolls or refuses to wrap | `.DesignFoundationGateTests.Labels` | skip AC5 |
| `2.4-DSN-004` | 4, 13 | ARCH | P1 | Each palette key's chip class takes its fill as `var(--label-{key})` and its ink as `var(--label-ink)` | `.DesignFoundationGateTests.Labels` | skip AC13 |
| `2.4-COPY-001` (x5) | 11, 14 | ARCH | P1 | `invalid-label-name`, `invalid-label-colour`, `label-name-taken`, `label-in-use`, `invalid-task-labels` each render copy of their own | `Architecture/RefusalCopyTests.Labels.cs` | skip AC14 |
| `2.4-COPY-002` | 9, 14 | ARCH | P1 | `403 manage-labels` renders "Only Owners and Admins can manage Labels." | `.RefusalCopyTests.Labels` | **ACTIVE, green** |
| `2.4-E2E-201` | 5 | E2E (fake) | P0 | Five Labels: three chips in server order, then `+2` named "2 more Labels"; the card keeps one control | `Client/LabelTests.cs` | skip AC5 |
| `2.4-E2E-202` | 5, 13 | E2E (fake) | P0 | 320px, six long Labels: no horizontal overflow in the metadata row or the card; the row wrapped | `.LabelTests` | skip AC5 |
| `2.4-E2E-203` | 7 | E2E (fake) | P0 | The picker is a multi-select listbox of exactly the Space's Labels, applied ones selected, nothing else | `.LabelTests` | skip AC7 |
| `2.4-E2E-204` | 7 | E2E (fake) | P0 | Held `PATCH`: card and panel show the new chip at once; body is only `labelIds`, the whole set; `Idempotency-Key`; admitted, stays | `.LabelTests` | skip AC7 |
| `2.4-E2E-205` (x4) | 7 | E2E (fake) | P0 | `400 invalid-task-labels`, `403 manage-tasks`, `409 in-flight`, `422 reused`: the chip reverts on card and panel, copy inline | `.LabelTests` | skip AC7 |
| `2.4-E2E-206` (x2) | 7 | E2E (fake) | P0 | No answer: revert, "Not sent."; a `404`: revert, the page's refusal surface, panel gone | `.LabelTests` | skip AC7 |
| `2.4-E2E-207` | 7 | E2E (fake) | P0 | A Labels refusal reverts only the Labels: an admitted title change stays | `.LabelTests` | skip AC7 |
| `2.4-E2E-208` | 1, 14 | E2E (fake) | P0 | A Member sees no "Space settings" link and, by URL, the capability copy and no define form; Owner anchor sees the link; nothing disabled | `.LabelTests` | skip AC1 |
| `2.4-E2E-209` | 2 | E2E (fake) | P0 | A Viewer's panel shows the chips as text and no picker; Owner anchor has the picker; nothing disabled | `.LabelTests` | skip AC2 |
| `2.4-E2E-210` (x2) | 14 | E2E (fake) | P0 | Switching Space from the Labels surface / from the open panel leaves no previous Label name in the DOM | `.LabelTests` | skip AC14 |
| `2.4-E2E-211` | 3 | E2E (fake) | P0 | Delete confirm replaces in place (at most one dialog), names the Label; `409 label-in-use` keeps it and says why | `.LabelTests` | skip AC3 |
| `2.4-E2E-212` | 1, 4, 14 | E2E (fake) | P0 | Define form: name `autocomplete="off"` with a non-reusable name, eight named colour radios, "Add Label" posts `{name, colour}` and waits for the server | `.LabelTests` | skip AC1 |
| `2.4-E2E-213` | 14 | E2E (fake) | P0 | Role re-read to Member on Space settings: narrated, then routed to Space home | `.LabelTests` | skip AC14 |
| `2.4-E2E-214` | 14 | E2E (fake) | P0 | Role re-read to Viewer with the panel open: the picker goes in place, the chips stay | `.LabelTests` | skip AC14 |
| `2.4-E2E-215` | 14 | E2E (fake) | P1 | No Label name in `document.title`, the address or browser storage | `.LabelTests` | skip AC14 |
| `2.4-A11Y-001` (x4) | 13 | E2E (fake) | P1 | A card with wrapped Labels, and the Labels surface, under 1.4.12 and 200% text-only zoom | `Client/TextSpacingTests.cs` | skip AC13 |

### Priorities

P0 for every case SM-1 (isolation), AC3/AC10 (no dangling reference), AD-18 (idempotency), AD-11 (the revert), AD-3,
UX-DR7 (computed contrast) or a verbatim AC rests on. P1 for validation edge cases, read order, theme-boundary
hygiene, copy mappings and text spacing.

### Duplicate-coverage guard - deliberately not written

- **"A foreign Space's Labels are unreachable" is not a slice case.** ISO-001, ISO-003 and ISO-004 prove it on both
  surfaces; LBL-011 holds only the in-Space byte-identity of a foreign id inside a `labelIds` body, which ISO-006
  states as a row too (the row on the wire, the slice case beside a database read of nothing written).
- **The Member's and Viewer's `403` are written once per level that can see them**: LBL-003 and LBL-006 (added to an
  Owner's Space, AC9's shape, four routes), ISO-007 and ISO-008 (the demoted variance and the record kind).
- **No timing row**: `NotAvailableFloor` holds every Space-scoped `404` (story Task 8).
- **The matrix itself** is already gated (`RefusalBoundaryGateTests.cs:89`); ARCH-001 proves the routes declare it.

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| `LabelName.TryNormalise`, `LabelPalette.IsKnown`, `DefineLabelHandler` / `DeleteLabelHandler` unit cases with hand fakes | The types do not exist; LBL-007, LBL-008, LBL-013 cover the observable halves over the wire | Tasks 2, 5, 7 |
| `has-pending-model-changes` reports none; `SpaceIsolationSchemaTests` and `SchemaVarianceTests` widen through `SchemaNames.SpaceScopedTables` | A command, and generic gates that derive their list; SCH-001 names the two tables so a missed append is still caught | Task 3 |
| The two-layer plant, both lock-hint plants (three runs each), the skipped foreign-Label check, the cascade-FK plant, `ConsultsBound` on a Label route, the GATE-002/003 plants, a Member offered the settings link, a Viewer offered the picker, `flex-wrap: nowrap`, a 3px chip, a Label list left after a purge | Each edits product code this phase does not write; the scaffolds above are the gates that must catch them | Task 12 plant table |
| Amending 2.3's `The_Task_edit_request_carries_the_title_the_Status_and_the_due_date_and_nothing_else`, `The_due_date_is_a_nullable_DateTimeOffset_on_the_Task_and_its_response`, the two `TaskProperties` lists (`TaskEndpointTests.cs:47`, `BoardEndpointTests.cs:44`), removing the `Label` exclusion in `RefusalBoundaryGateTests.Tasks.cs:71`, and the three colour-token count gates | Green, active gates that the story's own change turns red by design; ARCH-003, ARCH-005 and ARCH-006 are the successors | Tasks 4, 8, 9 |
| A Label picker keyboard pattern beyond multi-select | Deferred to 2.8 (`deferred-work.md:515`) | 2.8 |

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` resolves to `subagent` by capability; generation stayed
in-thread, as every earlier story recorded, because C# under this analyser set has to be built and fixed here anyway
(no temp JSON worker outputs were written). Build fixes on the way: `CS8602` (x2), `S2148`, `S6605`, `S8969`,
`MA0181`, `S3776` (the fake's edit, split into `LabelsRefusal`), and the `Assumption` trait value (`STORY-2-4-Q2` would
fail `Every_Assumption_trait_carries_a_source_identifier_the_selective_run_can_find`; it is `FR-22-Q2`, the requirement
the Q2 name rule hardens). Worker A's output is the Slices, Isolation and Architecture files; worker B's is the
browser class, the text-spacing case and the fake.

### `tests/Yello.Tests.Slices/Tasks` - 45 cases in 28 skipped entries, the existing class

| File | Cases | ids | ACs |
|---|---|---|---|
| `TaskEndpointTests.Labels.cs` | 28 (16 entries) | LBL-001 to LBL-016 | AC1, AC2, AC3, AC4, AC7, AC8, AC9, AC11 |
| `TaskEndpointTests.LabelsSchema.cs` | 6 (5 entries) | SCH-001 to SCH-005 | AC3, AC6 |
| `TaskEndpointTests.LabelsRace.cs` | 3 | RACE-001 to RACE-003 | AC10, AC11 |
| `TaskEndpointTests.LabelsIdempotency.cs` | 8 (4 entries) | IDM-001 to IDM-004 | AC11 |
| `LabelWire.cs` | harness | define/list/delete on either surface, `ApplyBody`, the two tables read under the Space key, the races' other writes | - |

### `tests/Yello.Tests.Isolation/IsolationCases.cs` - 8 rows, all pending on 2.4

ISO-LIST-LABELS-FOREIGN-SPACE, ISO-LIST-LABELS-OWN-SPACE, ISO-CREATE-LABEL-FOREIGN-SPACE, ISO-DELETE-LABEL-FOREIGN-SPACE,
ISO-LABEL-FOREIGN-ID-IN-OWN-SPACE, ISO-APPLY-FOREIGN-LABEL, ISO-RECORD-KIND-LABEL-DEFINE-MEMBER,
ISO-RECORD-KIND-LABEL-APPLY-VIEWER - each with its real `Prepare`, an expectation for **both** surfaces, a send on the
exact route constant it names (`LabelScaffold.Labels`, `LabelScaffold.Label`, `TaskRoutes.Task`), and
`PendingUntil: "2.4"` with a reason. **Harness change (active, behaviour-preserving):** `CaseRequest` gains optional
`Body` and `AnchorBody` factories and `SendAsync`/`SendAnchorAsync`; `BoundaryNotFound`, `SameAsAnchor`, `Served` and
`RoleRefused` send through them. A row with neither sends exactly what it sent before (`ProductActor`'s valid body), so
every existing row is unchanged - the full Isolation run passed. The apply row needs it: its foreign id is in the body
(`labelIds: [LB]`) and its anchor is the same Task with `[random]`. Helpers added: `SeedLabelAsync` (names added to the
owner's secrets via the new `ProductActor.HoldsLabelNamed`), `MemberDefinesALabelAsync`, `ViewerClearsALabelAsync`, and
`WireDatabase.LabelsInAsync` / `ApplicationsInAsync`. `IsolationCaseListTests` stays green: GATE-002 sees no new mapped
route yet, and GATE-003 permits pending rows while 2.4 is `ready-for-dev` or `in-progress`.

### `tests/Yello.Tests.Architecture` - 30 cases in 26 entries: 11 active, 15 skipped

| File | Tests |
|---|---|
| `RefusalBoundaryGateTests.Labels.cs` | ARCH-001 to ARCH-007 skipped |
| `CieDe2000.cs` | the harness (Sharma, Wu & Dalal 2005; sRGB to CIELAB D65) |
| `LabelPaletteGateTests.cs` | **PAL-000 (8 published pairs), the CIELAB endpoint check and PAL-000-ORACLE active and green**; PAL-001 to PAL-003 skipped |
| `DesignFoundationGateTests.Labels.cs` | DSN-001 to DSN-004 skipped |
| `RefusalCopyTests.Labels.cs` | COPY-001 (5 cases, 1 entry) skipped; **COPY-002 active and green** |

**The story's palette table was re-derived**, not trusted: an independent scratch script and the C# harness both
reproduce every published CIEDE2000 pair to four places and the story's eight rows exactly (teal's ΔE 13.8 is the
thinnest separation; blue's 3.89 the thinnest dark-card ratio; grass's 3.29 the thinnest light-card ratio). An ink at
`text-primary` (#E6EAF5) fails every fill (2.74-3.40:1), which is Q4's reason for one fixed ink.

### `tests/Yello.Tests.Client` - 24 cases in 16 skipped entries

`LabelTests.cs`: E2E-201 to E2E-215 (20 cases, 15 entries), in `ClientSurfaceCollection` (one dev server, one
Chromium, no Host). `TextSpacingTests.cs`: A11Y-001 (4 cases, 1 entry), the labelled card and the Labels surface under
`deferred-work.md:20`'s convention.

### Harness added (test code, active)

- `Yello.Tests.Shared/LabelScaffold.cs` - the two route patterns and builders, the five problem types, `NameMaxLength`
  (Q2), `OversizeLabelIds` (Q6), `PaletteKeys` (Q3). Temporary; deleted on activation.
- `FakeYelloApi`: `GET`/`POST …/labels`, `DELETE …/labels/{id}` (Q2 name rule, Q3 palette, case-insensitive
  uniqueness, `409 label-in-use` when any Task carries it), `HoldLabelCreateAsync`/`ReleaseLabelCreate`, `labelIds` on
  the Task edit (whole set, `invalid-task-edit` when not an array, `invalid-task-labels` for a non-GUID, unknown or
  repeated id, served in name-then-id order). `FakeTask` gains `LabelIds`, **omitted from the body when null**, so every
  case written before 2.4 is served the bytes it always was - the full Client run passed.

### Fixtures and seed data - every constraint checked

No new fixture type and no new container-backed class. Checked against `20261025115412_TaskTable` (`dbo.[Task]`, no
`(SpaceId, Id)` key yet - Task 3 adds `AK_Task_SpaceId_Id`), `20261025094743_Projects.cs` and story 2.4 Task 3 for
the two tables to come:

- **Through the product:** Accounts, Personal Spaces, Owner Memberships, Tokens, Projects, Tasks, Labels and every
  application a case reads back. Label names are unique per call where uniqueness matters (Q2's `(SpaceId, Name)`
  index); the isolation seeds are `Label {guid}` cut to 30 characters (inside Q2's 50).
- **`AddMemberAsync`** (Admin, Member, Viewer added to an Owner's Space): one per (Space, Account)
  (`AK_Membership_SpaceId_AccountId`), never a second Owner (`UX_Membership_SpaceId_Owner`), under the Space's key.
  **`ChangeRoleAsync`** for the two record-kind rows: the demoted Space has no Owner, which the filtered
  `UX_Membership_SpaceId_Owner` permits; the Viewer's Task, Label and application are created **before** the demotion
  (creating needs `manage-tasks` / `manage-labels`).
- **`LabelWire.InsertApplicationAsync`** (RACE-001): `(SpaceId, TaskId, LabelId, AppliedAt)` under the Space's own key
  (`BLOCK AFTER INSERT`), naming a Task and a Label the product created in that Space (both composite FKs to
  `(SpaceId, Id)`), once (the `(SpaceId, TaskId, LabelId)` key).
- **`LabelWire.InsertLabelAsync`** (RACE-003): `(Id, SpaceId, Name, Colour, CreatedAt)` under the Space's key, a fresh
  id (`ValueGeneratedNever`), `Colour` a palette key, the Space a real one (`FK SpaceId -> Space`). Its name collides
  with the request's **on purpose** - that is the case.
- **`LabelWire.DeleteLabelRowAsync`** (RACE-002): the Label is unused when deleted, so the `NoAction` FK permits it.
- **SCH-005** deletes an applied Label by SQL to prove `547`; the anchor deletes an unused one on the same connection.
- **If Task 3 lands another non-nullable column without a default** on either table, correct `LabelWire`'s three
  statements (its class remarks say so).

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Test run

```
dotnet build-server shutdown
dotnet test Yello.slnx --no-build
Test run summary: Passed!   total: 920   failed: 0   succeeded: 858   skipped: 62   (4m 06s)
```

Against the post-2.3-review baseline (850 total, 847 passed, 3 skipped): **+70 entries** - 11 new active and green
(PAL-000's eight pairs, the CIELAB endpoint check, PAL-000-ORACLE, COPY-002) and 59 new skipped entries (28 Slices, 15
Architecture, 16 Client). The 3 baseline skips are unchanged (`YELLO_MIGRATION_BUNDLE` unset). Four engines, one full
run, no engine-start failure; the browser suite passed in the combined run. The 8 pending isolation rows generate no
theory rows.

### Red-phase compliance

- Every scaffold whose AC is not implemented is skipped with `red: AC<n> - <why> (story 2.4 Task n; Qn)`; every
  isolation row is pending on 2.4 with a reason.
- Active and green, each a standing gate: PAL-000 (the ΔE2000 harness against published pairs), the CIELAB endpoint
  check, PAL-000-ORACLE (the AC4 rule passes the proposed palette and reports three plants - a permanent plant, read
  against the live token layer), and COPY-002 (the `manage-labels` sentence).
- Every scaffold compiles under warnings-as-errors. No scaffold references a type 2.4 has not written, except by name
  or through `LabelScaffold`.
- Every absence is anchored on a presence in the same case (the 50-character name beside the refused 51, `rust`
  beside the refused hex, the own Label beside the foreign one, the Member's `200` beside the Viewer's `403`, the
  unused Label's deletion beside the applied one's refusal, the Owner's settings link beside the Member's absence, the
  Owner's picker beside the Viewer's absence).
- Product code is unchanged: `git status` shows test files, this checklist and the story's Dev Notes only.

### Checklist validation (`checklist.md`, mapped to this repository)

The skill's checklist is written for a TypeScript Playwright project; the repository's binding (TESTING-CONVENTIONS)
replaces its directory, factory and fixture items: tests sit in the existing suites, seeding is through the product
(no faker factories), fixtures are the existing class fixtures, and `[Fact(Skip = "red: ...")]` is `test.skip()`.
Items that apply hold: ACs testable and mapped (all 14), levels chosen without duplicate coverage, P0/P1 assigned,
no hard waits (deadline polls and `TaskCompletionSource` holds only; races wait on `LCK_M_%`), the network edge
installed before navigation (`InstallAsync` before `GotoAsync`), one behaviour per test, story metadata and handoff
paths in the frontmatter. No CLI browser session was opened; no temp artefact was left outside the scratchpad.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected
and the correction recorded in the Debug Log, not deleted.**

0. **Task 1.** Run `IsolationCaseListTests` (green). Eight rows are pending on 2.4 - list them in the Debug Log. Decide
   Q1-Q14; where a decision differs from the Q table above, correct the scaffold (`LabelScaffold` first) before
   activating anything.
1. **RACE-002 first - the riskiest assumption.** An application racing a deletion that commits first must answer
   `400 invalid-task-labels`, never a `500`, and leave no `TaskLabel` row. It tests what no earlier story has measured:
   **whether the edit's Label read under `UPDLOCK, HOLDLOCK` (after 2.3's Task/Project lock) and the delete's
   Label-then-TaskLabel order can deadlock, and whether, without the hint, read-committed snapshot lets the edit read a
   Label deleted under it** (the composite FK then answers as a `500`). Build the minimum (Task 2's `Label`, Task 3's
   two tables, Task 4's `labelIds`, Task 5's apply), activate RACE-002, then RACE-001 (the deletion meets an uncommitted
   application: `409`, never a `500`; the `NoAction` FK is the backstop), then RACE-003 (the unique index answers a
   duplicate as `409`). Plant each hint's removal three times and record what you measured, including whether the
   probe-hint plant is masked by the FK backstop.
2. **Schema and wire shape** (Tasks 2-4): SCH-001 to SCH-005, ARCH-002, ARCH-003 (removing the exclusion in
   `RefusalBoundaryGateTests.Tasks.cs:71` in the same change), ARCH-004, ARCH-005 and ARCH-006 (amending 2.3's two
   gates and the two `TaskProperties` lists deliberately in the same change), ARCH-001. Replace `LabelScaffold` with the
   contracts as each case activates; delete it when unused.
3. **Define, list, delete, apply** (Task 5): LBL-001, LBL-015, LBL-007, LBL-008, LBL-009, LBL-010, LBL-013, LBL-014,
   LBL-004, LBL-005, LBL-011, LBL-012, LBL-016, LBL-002, LBL-003, LBL-006.
4. **Isolation rows** (Task 8), one at a time: ISO-RECORD-KIND-LABEL-DEFINE-MEMBER, ISO-LIST-LABELS-FOREIGN-SPACE,
   ISO-LIST-LABELS-OWN-SPACE, ISO-CREATE-LABEL-FOREIGN-SPACE, ISO-DELETE-LABEL-FOREIGN-SPACE,
   ISO-LABEL-FOREIGN-ID-IN-OWN-SPACE (plant the two-layer drop, then each layer alone), ISO-APPLY-FOREIGN-LABEL,
   ISO-RECORD-KIND-LABEL-APPLY-VIEWER. GATE-002 must stay green as each route is mapped; **GATE-003 fails at `review`
   if any of the eight is still pending.**
5. **Idempotency** (Task 6): IDM-001 to IDM-004.
6. **Palette** (Task 9): PAL-001, PAL-002, PAL-003 - amending the three colour-token count gates by an explicit,
   named `--label-` prefix exclusion. PAL-000 and its oracle are already active.
7. **Copy and design** (Tasks 10, 12): COPY-001; DSN-001 to DSN-004. COPY-002 is already active.
8. **Client** (Tasks 10, 11): first ARCH-007 with the `LabelCapabilities` predicate; then E2E-201, E2E-202, E2E-203,
   E2E-204, E2E-205, E2E-206, E2E-207, E2E-209, E2E-214, E2E-208, E2E-212, E2E-211, E2E-213, E2E-210, E2E-215; then
   A11Y-001. Correct the assumed locators to the real markup and record each. Amend 2.2's
   `BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow` only if it goes red, deliberately.
9. Keep PAL-000, its oracle and COPY-002 green throughout. Task 12's product plants run against the activated cases.

### Commands

```
dotnet build Yello.slnx
dotnet build-server shutdown
dotnet test Yello.slnx --no-build
dotnet test --project tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj --no-build -- --filter-class Yello.Tests.Isolation.IsolationCaseListTests
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.Tasks.TaskEndpointTests
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.LabelPaletteGateTests
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.LabelTests
```

`--no-build` runs zero tests unless the same configuration was just built. Run test processes one at a time on this
machine. If a Client case fails only in the full run, run the Client project alone before treating it as real. Rebuild
`Yello.Client` before a browser plant.

## Notes and risks

- **The race cases decide a design question, not just a test** (lock order; read-committed snapshot). RACE-002 is the
  case that shows a missing hint as a `500`; RACE-001's plant may be masked by the `NoAction` backstop answering `409` -
  record which.
- **Gates that go red by design** when Tasks 4 and 9 land: 2.3's edit-request and response-shape gates, the two
  `TaskProperties` lists, and the three colour-token count gates. Widen each deliberately, never loosely, and record it.
- **`LabelScaffold`, the fake's handling of `labelIds` and the browser locators carry the largest correction risk**
  (Q1, Q5, Q7, Q14). Correct the scaffold, not the product, and record it.
- **E2E-201 counts the card's focusable elements**: a chip or the `+N` must not take `tabindex`, or the card has a
  second control.
- **Container cost:** none today. On activation, the same Slices database as 2.2's Task cases; three short
  lock-holding cases; no timing row.

## Completion summary

- **Checklist:** `_bmad-output/test-artifacts/atdd-checklist-2-4-define-and-apply-labels.md`.
- **Story handoff:** `_bmad-output/implementation-artifacts/2-4-define-and-apply-labels.md` (Dev Notes section "ATDD
  artifacts - red-phase scaffolds already in the tree").
- **Next workflow:** `bmad-dev-story`; `bmad-testarch-automate` after implementation.

---

**Generated by BMad TEA Agent** - 2026-10-25
