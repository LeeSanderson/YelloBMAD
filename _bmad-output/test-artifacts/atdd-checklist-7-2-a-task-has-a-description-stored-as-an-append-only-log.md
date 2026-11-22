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
lastSaved: '2026-11-22'
workflowType: 'testarch-atdd'
storyId: '7.2'
storyKey: '7-2-a-task-has-a-description-stored-as-an-append-only-log'
storyFile: '_bmad-output/implementation-artifacts/7-2-a-task-has-a-description-stored-as-an-append-only-log.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-7-2-a-task-has-a-description-stored-as-an-append-only-log.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; every suite shares the TaskDescriptionWire stand-ins and one build)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.TaskDescription.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.TaskDescriptionSchema.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.TaskDescriptionRace.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.TaskDescriptionCompaction.cs'
  - 'tests/Yello.Tests.Merge/Conformance/MergeCases.Load.cs'
  - 'tests/Yello.Tests.Merge/Conformance/LoadConformanceTests.cs'
  - 'tests/Yello.Tests.Merge/ReplicaStateLoadTests.cs'
  - 'tests/Yello.Tests.Merge/Conformance/DescriptionRebuildBudgetTests.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.TaskDescription.cs'
  - 'tests/Yello.Tests.Architecture/DesignFoundationGateTests.DescriptionEditor.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.TaskDescription.cs'
  - 'tests/Yello.Tests.Client/DescriptionEditorTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.TaskDescription.cs'
  - 'tests/Yello.Tests.Slices/Tasks/TaskDescriptionSliceWire.cs'
  - 'tests/Yello.Tests.Shared/TaskDescriptionWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/7-2-a-task-has-a-description-stored-as-an-append-only-log.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-6-6-move-a-whole-status-of-tasks-at-once.md'
  - '_bmad-output/test-artifacts/atdd-checklist-7-1-the-merge-port-and-its-conformance-suite.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 7, Story 7.2: A Task has a description, stored as an append-only log

## Story summary

As someone describing a piece of work, I want a description that can hold real detail, so that a Task carries more than a
title. Requirements: **FR-31** (persistence and editor half), **AR-19 / AD-13**, **UX-DR10**, with AD-2/AR-5/AR-8, AD-11/AR-20,
AD-12, AD-21, SM-5, FR-3/AD-5, FR-16, X-9, NFR-6/I-17, and the test-design ids M-11, A-11, A-5.

## Step 1 - Preflight

- **Stack:** `fullstack`: .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0 on Microsoft.Testing.Platform,
  Testcontainers SQL Server 2025, Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority.
- **Story:** `ready-for-dev`, AC1-AC11, Q1-Q9, none marked [LEE]. Every scaffold follows each question's recommended
  option (a); a skip that depends on a question names it.
- **No progress file existed** (`atdd-checklist-7-2-*` was absent), so this run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists. The TEA knowledge fragments are
  TypeScript-oriented; the conventions file and the 6.6 and 7.1 scaffolds were the working references.
- **Harness reused (no new fixture, engine or container-backed class):**
  - Slices: four new partials of `TaskEndpointTests` on its one database; `TaskWire`, `TaskEditWire`, `ProjectMoveWire`,
    `AccountDeletionWire`, `HostProcess` (for the one log case), and a second pooled settings set (the lowered compaction
    threshold); the class used only one before.
  - Merge: `Candidate`, `Network`, `Port`, `Glyphs`, `Utf16`, `Workload`, `MergeTimingCollection`; `MergeCases.WithHistory`.
  - Architecture: partials of `RefusalBoundaryGateTests` (`RouteTable`, `SessionOnlyMarkerName`, `TaskContractsNamespace`,
    `PropertyNames`, `OutermostOf`, `SourceMemberOf`, `SetList`, `DisabledAttribute`, `RuntimeAssemblies`) and
    `DesignFoundationGateTests` (`CssCorpus`, `SelectsComponent`, `AssertComponentRadius`).
  - Isolation: `SeedProjectWithTaskAsync`, `TokenOwnOtherSpace`, `Both`, `BoundaryNotFound`, `SameAsAnchor`, `RoleRefused`,
    `Served`, `ChangeRoleAsync`.
  - Client: `ClientSurfaceFixture`, `FakeYelloApi` (`TaskEditHold`, `TaskEditOutcome`, `FakeRequest`).

## Step 2 - Generation mode

AI generation from the story and the documentation. No recording mode: the routes, the component and the fake's routes do
not exist yet. Every path, locator, member name and copy string for them is the story's recommendation, and each file's
remarks name it.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| Q1 | (a) `POST …/tasks/{taskId}/description/changes` carrying encoded changes and a version, never text | every Slices write case; the change-route and request gates; the isolation rows |
| Q2 | (a) `TaskDescriptionStore` in Infrastructure, the edge `Infrastructure → Merge` | A-11 gate (`DescriptionStoreName`); attribution gate; "gates expected red" below |
| Q3 | (a) a separate read resource; `TaskResponse` unchanged | read case; `No_Task_edit_contract_and_no_Task_answer_names_a_description` (green) |
| Q4 | (a) `AuthorAccountId` SET NULL + `AuthorName`; three predicates | schema cases; erasure case |
| Q5 | (a) 250 ms idle / 1 s oldest / blur, Esc, close; one in flight | batching browser case (skip reason names Q5) |
| Q6 | (a) no authorship UI; "No description." in `text-muted` | Viewer empty case (skip reason names Q6) |
| Q7 | (a) snapshot at the threshold in the crossing transaction; change rows never removed; previous snapshot deleted | M-11 and snapshot cases |
| Q8 | (a) fix the load check and bump the format now | `LoadConformanceTests`, `ReplicaStateLoadTests` |
| Q9 | (a) no length limit; 256 changes / 256 KiB per batch | projection column case (`max_length -1`); cap case |

### Scenario map

| AC | Level | Scaffold | Priority | Today |
|---|---|---|---|---|
| AC3, AC2 | Slices (race) | `An_append_rebuilds_the_log_only_under_the_Task_lock` | P0 | red - **riskiest** |
| AC3 | Slices (race) | `Two_appends_queued_on_one_Task_are_both_kept` | P0 | red |
| AC1-AC3 | Slices | `A_batch_appends_one_row_per_change_and_the_projection_holds_the_merged_text` (Session, Token) | P0 | red |
| AC3 | Slices | `A_resent_change_appends_no_row` | P0 | red |
| AC3 | Slices | `A_refused_change_writes_nothing_and_the_changes_beside_it_are_kept` | P0 | red |
| AC3 | Slices | `A_batch_whose_every_change_is_refused_answers_200_and_writes_nothing` | P0 | red |
| AC3 | Slices | `The_answer_carries_what_the_clients_version_lacks_and_applying_it_gives_the_projection` | P0 | red |
| AC3 | Slices | `A_body_that_is_not_a_change_batch_is_refused_400_and_nothing_is_written` (7 rows) | P1 | red |
| AC3, Q9 | Slices | `A_batch_over_the_cap_is_refused_400_and_nothing_is_written` | P1 | red |
| AC3, AC8 | Slices | `A_Viewer_is_refused_on_both_surfaces_and_writes_nothing_and_still_reads_the_description` | P0 | red |
| AC3, AC4 | Slices | `An_unknown_Task_answers_the_not_found_on_both_routes` | P0 | red |
| AC4 | Slices | `The_read_answers_the_projection_and_a_state_that_rebuilds_it` | P0 | red |
| AC2, AC4 | Slices | `A_title_edit_leaves_the_projection_alone_and_the_Task_answer_carries_no_description` | P0 | red |
| AC1 | Slices | `Deleting_the_Task_its_Project_or_its_Space_removes_its_log` (3 rows) | P1 | red |
| AC1 | Slices | `Moving_the_Task_to_another_Project_keeps_its_log_and_its_projection` | P1 | red |
| AC3 | Slices | `A_replayed_key_answers_the_original_and_appends_nothing` | P1 | red |
| AC1, AC6 | Slices | `Erasing_an_author_keeps_their_rows_and_the_name_captured_when_they_wrote` | P1 | red |
| AC3 | Slices | `Neither_the_write_nor_the_read_logs_the_description_its_payload_or_the_title` | P0 | red |
| AC1 | Slices (schema) | 3 log cases: columns and unique order; keys (Task cascade, Account SET NULL, none to Space); policy predicates | P0 | red |
| AC2 | Slices (schema) | `The_projection_is_a_non_nullable_unbounded_text_column_defaulting_to_empty` | P0 | red |
| AC9 (M-11) | Slices | `Compaction_keeps_every_authored_change_row_and_its_time_and_the_projection_rebuilds_from_the_snapshot` | P1 | red |
| AC9 | Slices | `The_snapshot_holds_the_text_of_every_change_before_it` | P1 | red |
| AC10 | Merge | `A_change_withdrawn_before_a_reload_under_a_new_identity_is_never_resurrected` | P0 | red |
| AC10 | Merge | `A_version_decoded_from_its_bytes_asks_for_exactly_what_the_version_itself_asks_for` | P0 | **green, active** |
| AC10 | Merge (candidate) | `ReplicaStateLoadTests`: last sequence below a held own change; withdrawn and held; previous format byte | P0 | red |
| AC9 | Merge (timing) | `A_working_size_description_rebuilds_from_its_snapshot_and_5000_later_changes_within_the_budget` (200 ms p95) | P1 | **green, active** |
| AC3, AC4 | Arch | change route, read route, `TaskRoutes` templates, request, change answer, read answer, two slices | P0/P1 | red |
| AC2 (A-11) | Arch | `Only_the_projector_writes_a_Tasks_description` | P1 | red |
| AC2 (A-11) | Arch | `No_raw_SQL_in_production_sets_a_description` | P1 | **green, active** |
| AC2 | Arch | `The_description_writer_scans_report_a_planted_writer` (oracle) | P1 | **green, active** |
| AC2, AC4 | Arch | `No_Task_edit_contract_and_no_Task_answer_names_a_description` | P0 | **green, active** |
| AC6 | Arch | `Attribution_reads_a_live_Account_name_only_in_named_methods` | P0 | red |
| AC6 | Arch | `The_method_level_Account_name_scan_reports_a_planted_reader` (oracle) | P0 | **green, active** |
| AC8 | Arch | `The_clients_description_editor_predicate_agrees_with_the_capability_matrix` | P0 | red |
| AC7, AC8 | Arch | `The_description_editor_is_never_disabled_read_only_or_inert` | P0 | red |
| AC7 | Arch (CSS) | `The_description_editor_takes_the_3px_radius`; page ground / colour / border / padding / type (5 rows) | P1 | red |
| AC11 | Isolation | 8 rows x 2 surfaces (below) | P0 | `PendingUntil: "7.2"` |
| AC7, AC8 | Browser | `DescriptionEditorTests`: 9 cases | P0/P1 | red |

Isolation rows: `ISO-DESCRIPTION-CHANGES-FOREIGN-SPACE`, `ISO-DESCRIPTION-FOREIGN-SPACE`,
`ISO-DESCRIPTION-CHANGES-FOREIGN-TASK-IN-OWN-SPACE`, `ISO-DESCRIPTION-FOREIGN-TASK-IN-OWN-SPACE`,
`ISO-RECORD-KIND-DESCRIPTION-CHANGES-VIEWER`, `ISO-DESCRIPTION-VIEWER-READS`, `ISO-TOKEN-OWN-OTHER-SPACE-DESCRIPTION-CHANGES`,
`ISO-TOKEN-OWN-OTHER-SPACE-DESCRIPTION`. The I-17 log check runs on every row.

**Not scaffolded (the develop phase's):**
- The property harness's reload and decoded-version steps (AC10): they change `InterleavingHarness` itself, whose existing
  assertions must not be edited. The decoded-version behaviour has a conformance case above.
- Routing `FakeYelloApi.TaskDescription.cs` and backing it with a replica of the real strategy: the Client test project
  reaches `Yello.Merge` only once Infrastructure references it.
- Handler-level tests of the two slices (they need the port and slice types to compile).
- `ProductActor` holding a description as an I-17 secret, if the develop phase wants the isolation log check to cover it;
  the Slices log case covers the text, the payload and the title.
- Plants: the A-11 plant in a Host or Application type and the second `DisplayName` reader method in an admitted type.

### Red phase

Every scaffold whose AC is not implemented is committed skipped with `Skip = "red: AC<n> - <why>"`. Seven cases pass today
and stay active as standing regression gates: the decoded-version conformance case, the rebuild budget, the raw-SQL A-11
half and its oracle, the method-level name-scan oracle, and the "no description on the Task edit or the Task answer" gate.

## Step 4 - Generated scaffolds

Execution mode: sequential (the Agent tool is available, but every worker shares the stand-ins and one build).

| File | Cases | Skipped | Active |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.TaskDescription.cs` | 16 methods (25 rows) | 16 | 0 |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.TaskDescriptionSchema.cs` | 4 | 4 | 0 |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.TaskDescriptionRace.cs` | 2 | 2 | 0 |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.TaskDescriptionCompaction.cs` | 2 | 2 | 0 |
| `tests/Yello.Tests.Merge/Conformance/LoadConformanceTests.cs` (+ `MergeCases.Load.cs`) | 2 | 1 | 1 |
| `tests/Yello.Tests.Merge/ReplicaStateLoadTests.cs` | 3 | 3 | 0 |
| `tests/Yello.Tests.Merge/Conformance/DescriptionRebuildBudgetTests.cs` | 1 | 0 | 1 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.TaskDescription.cs` | 15 | 11 | 4 |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.DescriptionEditor.cs` | 2 methods (6 rows) | 2 | 0 |
| `tests/Yello.Tests.Isolation/IsolationCases.TaskDescription.cs` | 8 rows x 2 surfaces | pending | - |
| `tests/Yello.Tests.Client/DescriptionEditorTests.cs` | 9 | 9 | 0 |

**Supporting files:**
- `Yello.Tests.Shared/TaskDescriptionWire.cs`: the stand-ins - `ChangesRoute`/`ChangesOf`, `Route`/`Of`, the problem types
  `urn:yello:problem:invalid-description-changes` and `urn:yello:problem:description-batch-too-large`, the capability code,
  the caps (256 changes, 256 KiB), the Host setting `TaskDescription__CompactionThreshold`, the batch body
  `{ changes: [base64], version: base64 }`, and readers for `refused`, `missing`, `version`, `state`, `text`. When
  `TaskRoutes` declares `DescriptionChanges`/`DescriptionChangesOf` and `Description`/`DescriptionOf`, and `ProblemTypes` the two
  types, swap every use and delete the stand-ins.
- `Yello.Tests.Slices/Tasks/TaskDescriptionSliceWire.cs`: editors made through the real strategy under a fresh identity,
  the batch on either surface, the log and the projection read under the Space's key, the rebuild as the server is to do it,
  the Task row held in the product's shape, and the one raw append (read race only).
- `Yello.Tests.Client/FakeYelloApi.TaskDescription.cs`: the case-facing controls (`Descriptions`, hold/release,
  `RefuseEveryChangeInTheNextBatch`, `FailTheNextBatch`, `DescriptionBatches`, `ChangesIn`). Not routed; inert.
- One spread line in `IsolationCases.All`.

**Stand-in wire types**, as earlier stories did: the Slices, Isolation and Merge scaffolds reach `Yello.Merge` through the
existing transitive reference from `Yello.Host`, so they make real changes; nothing references a product type that does not
exist. The Architecture gates find their subjects by name.

### Seed and schema constraints assumed

- `dbo.[TaskDescriptionChange]` columns `Id`, `SpaceId`, `TaskId`, `Sequence` (`bigint`), `Kind` (stored by name, `nvarchar`,
  as every enum here is stored via `HasConversion<string>()`), `Payload` (`varbinary(max)`), `AuthorAccountId`, `AuthorName`
  (nullable), `RecordedAt` (`datetimeoffset`); a unique index whose key is `(SpaceId, TaskId, Sequence)`; `Task.Description`
  `nvarchar(max) NOT NULL DEFAULT N''`. A different column name or type corrects the wire statements and the schema case.
- The one raw write (`InsertChangeAsync`, read race) satisfies: the composite `(SpaceId, TaskId) → Task` key (a product-made
  Task); `BLOCK AFTER INSERT` (runs under the Space's key); the unique `(SpaceId, TaskId, Sequence)` index (`MAX + 1` under the
  held Task lock); `AuthorAccountId → Account` (the registered Owner); a payload made by the real strategy. It re-projects in
  the same transaction, in the product's shape.
- `TaskWire.SeedTasksAsync` names no `Description`; the column's default keeps it valid.
- Every other row is written through the product: Projects, Tasks, Memberships (`AddMemberAsync` for the Member and Viewer,
  as other Task cases do), batches, deletions, the move and the Account erasure.
- Isolation rows seed only in fresh actors' Spaces and check what a refused row left by reading the Task's description
  through the product as its own Owner, so no check needs the policy-hidden log.

### Findings recorded while writing the gates

- **"Refused change ids" cannot always be ids.** A truncated or damaged change may not decode to a `ChangeId`, so the answer
  cannot name its id. The scaffolds count `refused` and never read its element type. The develop phase chooses the shape
  (batch positions, or ids where decodable) and records it; the client withdraws by the id it put at each position.
- **The live `DisplayName` readers, method by method**, are nine today: `AccountConfiguration.Configure`,
  `InvitationStore.IssueAsync`, and seven `MembershipStore` methods (`AssignmentNoticeAsync`, `DisplayNameOfAsync`,
  `ListAssigneesAsync`, `ListAsync`, `OwnershipOfferNoticeAsync`, `ReadAsync`, `RowAsync`). The type-level gate admitted
  `MembershipStore` wholesale "for current Members only"; at method level it also reads the live name for the assignment and
  ownership-offer notices. Pinned as found; not 7.2's to change. Recorded for the review.
- **`Format.Check` refuses a lower format byte as `Malformed`.** AC10 wants the byte before a bump refused as
  `UnsupportedVersion`, so the check changes with the bump.
- **The rebuild already meets the recommended budget natively**: 15,000 characters from a snapshot plus 5,000 single-character
  changes rebuild within 200 ms p95 today, so the case is active. The develop phase records the measured figure against
  ledger `:1147`.
- **A conformance case cannot forge contradicting state**: applying an own change raises the saved counter with it. The two
  contradiction cases and the format case therefore live in the candidate-specific `ReplicaStateLoadTests`, beside
  `HostileChangeTests`, with offsets anchored; the bump that keys the withdrawn set by change identity rewrites the second
  forgery.

## Step 5 - Validation

- Prerequisites: the story has clear ACs, the framework is configured, the environment is available (Docker 29.9.2).
- Every scaffold compiles; every red scaffold is skipped with its AC; no placeholder assertion.
- The four Merge red cases were activated once against the current candidate and fail for the stated reasons
  (`Malformed` not `UnsupportedVersion`; "base refused" not "base"; `Admitted` not `Malformed` twice), then restored from saved
  content.
- No CLI browser session was opened. Logs are in `artifacts/test-runs/7-2-atdd-*.log` (git ignores them).

### Activation order (riskiest assumption first)

1. **The rebuild under the lock:** `An_append_rebuilds_the_log_only_under_the_Task_lock`. If an append reads the log before
   taking the Task row, or projects outside the appending transaction, the design fails (AD-13's single in-transaction
   projector). Then `Two_appends_queued_on_one_Task_are_both_kept`.
2. **The load check before any state is persisted** (T2): `ReplicaStateLoadTests` (3) and
   `A_change_withdrawn_before_a_reload_under_a_new_identity_is_never_resurrected`. Must be green before the compaction cases,
   which persist the first encoded state.
3. The schema (4 cases) with the migration, then the A-11 gates and the ring edge.
4. The write path: one row per change, resend, refused-on-its-own, all-refused, missing changes, bodies, cap, replay.
5. The read, the title edit, unknown Task, Viewer, deletions, move, erasure, the log case.
6. M-11 and the snapshot case; record the rebuild figure.
7. The contract, route, slice, attribution and client-predicate gates; the 8 isolation rows go live before review (GATE-003).
8. The fake's routes, then the browser cases and the CSS gates; amend `The_panel_has_no_description_editor`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

### Existing gates the implementation will turn red (amend them deliberately, in the same change)

- **The ring rule:** `AllowedReferenceEdges.Table["Yello.Infrastructure"]` gains `Yello.Merge` (its remarks rewritten);
  `RingDependencyTests` is red until then. `MergePortGateTests` must stay green (no registration in Infrastructure).
- **RLS and table registries:** `SchemaNames.SpaceScopedTables` (else
  `SpaceIsolationSchemaTests.Every_table_with_a_SpaceId_column_is_covered_by_the_policy` fails), `TablesThatCannotMove` (else
  the predicate-count case expects 2 and finds 3), `PersistenceModelGateTests` and `SchemaVarianceTests` (unchanged in logic,
  applied to the new entity), and `RefusalBoundaryGateTests.SpaceScopedEntities` if the handler-reach gates must see it.
- **Migration gates:** the hand-editing analysers on the new migration; the model snapshot; expand-only (`DEFAULT N''`).
- **Route lists and the role matrix:** `The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare` turns red when
  the change route declares `EditTaskDescriptionCollaboratively` until a `MatrixRoute` row joins
  `ActiveSpaceEndpointTests.RoleMatrix.cs` (and the switch near `:218`).
  `Every_spaceId_endpoint_declares_exactly_one_capability` must stay green.
- **Pinned contract lists:** `The_Task_creation_request_carries_a_title_and_nothing_else` (`.Tasks.cs`) and the request list
  at `.TaskEdit.cs:52` gain `TaskDescriptionChangesRequest`; `No_Task_or_Board_response_carries_a_SpaceId_and_a_column_carries_its_own_total`
  (`.Tasks.cs:150`) gains the two responses. The `TaskResponse` member-set gates stay unchanged.
- **Isolation route coverage:** `Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` and the
  Token-row gate turn red the moment the routes are mapped, until the 8 rows are live; GATE-003 fails at review if any is
  still pending.
- **Attribution:** `Attribution_never_reads_a_live_Account_name` (type-level) turns red when the store reads `DisplayName`;
  replace it with the method-level gate here and delete the type-level list.
- **The two "no description" tests:** `TaskEndpointTests.EditSchema.cs` `The_due_date_is_a_nullable_datetimeoffset_and_no_column_holds_a_description`
  (keep the due-date half, rename) and `TaskDetailTests.The_panel_has_no_description_editor` (replace with the browser cases).
- **Also likely:** `LocalisationGateTests` (the two new strings in every locale), `ClientStaticStateGateTests` (no static
  state in the editor), and the `ProblemTypes` uniqueness gates (two new types).

## Results of this run

- **Build:** `dotnet build Yello.slnx`: 0 warnings, 0 errors.
- **Architecture, whole suite:** 598 total, 585 passed, 13 skipped (the 13 red gates), 0 failed. The 4 new active gates pass.
- **Merge,** the three new classes: 6 discovered, 2 passed (decoded version, rebuild budget), 4 skipped, 0 failed.
- **Slices,** `--filter-method` on the 24 new methods: 24 discovered, 24 skipped, 0 failed (no engine started).
- **Client,** `--filter-class Yello.Tests.Client.DescriptionEditorTests`: 9 discovered, 9 skipped, 0 failed.
- **Isolation,** `--filter-class Yello.Tests.Isolation.IsolationCaseListTests`: 16 passed, 0 failed. The 8 pending rows
  satisfy GATE-003 while 7.2 is ready-for-dev.

**Next workflow:** `bmad-dev-story`.
