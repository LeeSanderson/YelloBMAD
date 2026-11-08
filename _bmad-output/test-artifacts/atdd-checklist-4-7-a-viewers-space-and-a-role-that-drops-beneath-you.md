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
lastSaved: '2026-11-08'
workflowType: 'testarch-atdd'
storyId: '4.7'
storyKey: '4-7-a-viewers-space-and-a-role-that-drops-beneath-you'
storyFile: '_bmad-output/implementation-artifacts/4-7-a-viewers-space-and-a-role-that-drops-beneath-you.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-4-7-a-viewers-space-and-a-role-that-drops-beneath-you.md'
detectedStack: 'fullstack'
generationMode: 'ai-generation'
executionMode: 'sequential (unattended run; the API, browser and gate scaffolds share RoleHeaderWire and one build, so all were written in the main run)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RoleMatrix.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RoleHeader.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RemoveMembership.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.ViewerSpace.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.Memberships.cs'
  - 'tests/Yello.Tests.Revocation/RevocationCases.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.RoleDrop.cs'
  - 'tests/Yello.Tests.Client/ViewerSpaceTests.cs'
  - 'tests/Yello.Tests.Client/RoleDropTests.cs'
  - 'tests/Yello.Tests.Client/MembershipSectionTests.cs'
  - 'tests/Yello.Tests.Client/LeaveSpaceTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.RoleHeader.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Shared/RoleHeaderWire.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/4-7-a-viewers-space-and-a-role-that-drops-beneath-you.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-13.md'
  - '_bmad-output/specs/spec-yello/SPEC.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-4-6-assign-a-task-and-be-told.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 4, Story 4.7: A Viewer's Space, and a Role that drops beneath you

## Story summary

As someone whose standing differs between Spaces, or changes while I am working, I want the interface to tell me what I may
do without having to try. Requirements: **FR-16**, **UX-DR27**, **UX-DR29**, **UX-DR38**, with UX-DR28, UX-DR39, AD-1, AD-3,
AD-9, AD-18 binding and test-design rows X-7, X-8, X-9. The matrix is re-proved against Memberships made through the
product (Invitation accepted, and a demotion); a `Yello-Role` header echoed by the pipeline on `2xx` and `403
insufficient-role` only (Lee's Q1 (a)) lets an open tab learn a drop on its next request; one assertive `RoleNarration`
element (Lee's Q4 (a)) narrates it before the affordances go; two 4.5 ledger entries (`:823`, `:829`) and two folds
(`:824`, `:837`, Lee's Q9 (a)) are closed.

## Step 1 - Preflight

- **Stack:** `fullstack` - .NET 10 Host, Blazor WebAssembly client, xunit.v3 4.0.0, Testcontainers SQL Server 2025,
  Playwright for .NET 1.62.0. `tests/TESTING-CONVENTIONS.md` is the authority. Docker up (server 29.5.3).
- **Story:** `ready-for-dev`, AC1-AC12. **Q1, Q4, Q9 decided by Lee (2026-11-08), all (a)**, recorded under Task 0. Q2, Q3,
  Q5, Q6, Q7, Q8 scaffolded at their recommendations (a).
- **No progress file existed** for this story (`atdd-checklist-4-7-*` absent), so the run is Create.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`.
- **Harness reused:** the `ActiveSpaceEndpointTests` partials (`SignedInAsync`, `JoinedThroughTheProductAsync`,
  `CreateSpaceAsync` / `CreatedSpace`, `ChangeRoleOverWireAsync`, `LeaveOverWireAsync`, `IssueAsync` (Token), `IssuedToken`,
  `NamedProjectAsync`, `TaskThroughTheProductAsync`, `RefusalsOfAsync`, `CapabilityOf`, `AssertOneOwnerEachAsync`,
  `SoleDispatcherWireAsync`, `MalformedBody`, `WellFormedUnknownToken`), `MailDrop`, `WireDatabase`, `AssignmentWire`,
  `MembershipWire`, `InvitationWire`, the isolation world and its `JoinedThroughTheProductAsync`, the revocation world's
  `JoinOwnersSpaceAsync`, and `FakeYelloApi`.

## Step 2 - Generation mode

AI generation from the story and documentation (standing answer). No recording mode: the Role header, the narration element
and the client's Role entry point do not exist, so every header assertion and browser locator for them is an assumption
written from AC1-AC12 and the Q answers, named as such in each file's remarks.

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Option | Where it shows |
|---|---|---|
| **Q1 [LEE]** | (a) `Yello-Role` on `2xx` and `403 insufficient-role` only, live on replay; one client entry point | `RoleHeader.cs` (4 cases); `RoleDropTests`; header-writer and CORS gates; the fake's header |
| **Q4 [LEE]** | (a) one assertive `.role-narration` under the context bar, `tabindex="-1"`; focus moves only when the focused control goes | `RoleDropTests` (order, focus-moved, focus-kept, settings exit) |
| **Q9 [LEE]** | (a) fold `:823`, `:829`, `:824`, `:837` | AC9 two cases (`MembershipSectionTests`); AC10 two cases (`LeaveSpaceTests`); `:837` Token leave probe |
| Q2 | (a) rank order, gated against the matrix nesting | nesting gate (active), client ranks gate (skipped) |
| Q3 | (a) polite rise "You're now a Member." | `A_rise_is_said_politely_and_the_controls_appear` |
| Q5 | (a) `RoleNarration{Admin,Member,Viewer}` copy | narration copy gate; every browser narration string |
| Q6 | (a) typed settings URL routes to Space home, "Space settings is for Owners and Admins." | `A_Member_or_a_Viewer_typing_the_Space_settings_address_...` |
| Q7 | (a) promote the join helper to `Yello.Tests.Shared` | **not done here** - see assumption 2 |
| Q8 | (a) the leave rides a Session-lifetime token | AC10 cases |

### New assumptions this run made (no question covered them)

1. **`RoleHeaderWire.HeaderName` (`Yello.Tests.Shared`) is a stand-in** for `Yello.Contracts.Spaces.RoleHeader.HeaderName`.
   Develop swaps every use (Slices, the fake) for the contract member and deletes it. `RoleCodes` is a lasting reading.
2. **Q7's shared helper is left to the dev phase.** Promoting it rewrites three active suites; the new Slices scaffolds call
   the existing private `JoinedThroughTheProductAsync`, and the isolation rows the isolation one.
3. **The matrix coverage gate lives in the Architecture suite and reads the Slices source**
   (`ActiveSpaceEndpointTests.RoleMatrix.cs`), because the Architecture project references no test project. One
   `new MatrixRoute(Capability.X, ...)` per line, at the start of the line.
4. **Browser locators:** narration `.role-narration` (`role="alert"`, `tabindex="-1"`); rise said in
   `nav.context-bar [role=status]`; Board affordances `main button.board__add`, `.task-card__actions`, `.task-card--movable`;
   settings exit observed through `.space-settings__refusal`. Correct here first if the markup differs, and record it.
5. **The fake echoes the Role header by default** (`FakeYelloApi.EchoesRole = true`) and names it in
   `Access-Control-Expose-Headers`, because the fake answers cross-origin. The client ignores it today, so every earlier
   case is unaffected; `HoldSpaceReadAsync` / `ReleaseSpaceRead` hold any Space-scoped `GET`, optionally answering instead.

### Container budget

No new container-backed class, fixture or engine. Slices cases are partials of `ActiveSpaceEndpointTests`; isolation rows
ride the boundary world; the revocation row rides its world; the browser classes use the assembly's `ClientSurfaceFixture` and
`FakeYelloApi` (no SQL Server). The matrix theory adds about one minute to the Slices suite (11 cases, a Host each).

### Scenario map

| AC | Level | Scaffold | State |
|---|---|---|---|
| AC1 | Browser | `ViewerSpaceTests.A_Viewer_meets_no_affordance_and_nothing_disabled_on_any_surface` | **active, green** |
| AC1 (anchor) | Browser | `ViewerSpaceTests.The_same_sweep_as_the_Owner_finds_every_affordance_class` | **active, green** |
| AC1 (Q6) | Browser | `ViewerSpaceTests.A_Member_or_a_Viewer_typing_the_Space_settings_address_is_taken_to_Space_home_and_told` (x2) | skipped |
| AC1 | Architecture | `No_client_control_is_disabled_for_a_Role` + its detector | **active, green** |
| AC2 | Isolation | 5 refused rows + 2 served rows (below) | **active, green** |
| AC2, AC12 | Revocation | `REV-DEMOTED-TO-VIEWER-BY-OWNER` (both surfaces) | **active, green** |
| AC2 (`:837`) | Wire | `The_very_next_request_after_a_removal_...` gains the Token leave probe | **active, green** |
| AC3, AC12 | Wire | `Each_matrix_row_governs_Memberships_that_arrived_through_the_product` (x11) | **active, green** |
| AC3 | Architecture | `The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare` | **active, green** |
| AC4, AC12 | Wire | `Every_answer_judged_under_a_Membership_echoes_the_Role_it_was_judged_under` | skipped |
| AC4, AD-18 | Wire | `A_replay_carries_the_Role_of_the_replaying_request_and_never_a_stored_one` | skipped |
| AC8 | Wire | `No_answer_but_a_2xx_or_a_403_insufficient_role_carries_the_Role_header` | skipped |
| AC9 | Wire | `A_refusal_outside_the_Admins_target_rule_carries_admin` | skipped |
| AC4 | Browser | `RoleDropTests`: drop order, focus moved, focus kept, Admin on Space home, rise | skipped |
| AC5 | Browser | `RoleDropTests.The_demoted_Admin_in_Space_settings_is_told_focused_then_routed_and_never_half_rendered` | skipped |
| AC6 | Browser | `RoleDropTests.The_chip_and_the_accessible_name_follow_a_drop_and_a_rise` | skipped |
| AC7 | Browser | `RoleDropTests`: two tabs, late answer | skipped |
| AC4, AC11 | Browser | `RoleDropTests.An_idle_tab_sends_no_request` | skipped |
| AC9 (`:823`) | Browser | `MembershipSectionTests.A_demoted_Admins_refused_confirm_read_never_shows_the_Admin_limit` | skipped |
| AC9 (`:824`) | Browser | `MembershipSectionTests.A_late_refused_confirm_read_never_closes_another_rows_confirm` | skipped |
| AC10 (`:829`) | Browser | `LeaveSpaceTests.A_leave_answered_after_a_switch_...`, `A_leave_left_unanswered_after_a_switch_...` | skipped |
| AC11, Q2 | Architecture | `The_Roles_capability_sets_nest_from_Owner_down_to_Viewer` | **active, green** |
| AC11, Q2 | Architecture | `The_clients_Role_ranks_agree_with_the_matrix_nesting` | skipped |
| AC7 | Architecture | `No_client_code_shares_a_Role_or_a_narration_between_tabs` + detector | **active, green** |
| AC11 | Architecture | `No_client_file_polls_on_an_interval` (with planted signals) | **active, green** |
| AC7, Q5 | Architecture | `The_Role_narration_copy_names_the_Role_and_what_it_permits_and_no_Space` | skipped |
| AC11 | Architecture | `Exactly_one_production_type_writes_the_Role_header` | skipped |
| AC4 | Architecture | `The_cross_origin_policy_exposes_the_Role_header` | skipped |

Isolation rows (`IsolationCases.ViewerSpace.cs`, spread from `MembershipCases()`), each a Viewer joined through the product
into a fresh host's Space: `ISO-PROJECT-RENAME-VIEWER` (`manage-projects`), `ISO-LABEL-CREATE-VIEWER`,
`ISO-LABEL-DELETE-VIEWER` (`manage-labels`), `ISO-SPACE-RENAME-VIEWER` (`rename-space`), `ISO-SPACE-DELETE-VIEWER`
(`delete-space`) - each `RoleRefused` with a `Then` that proves nothing changed - and the served `ISO-LEAVE-VIEWER` (`204`
both surfaces) and `ISO-TOKEN-ISSUE-VIEWER` (`201` Session, the no-credential `401` by Token).

## Step 4 - Generated scaffolds (TDD red phase)

Every unimplemented scaffold is `[Fact(Skip = "red: AC<n> - <why>")]` or `[Theory(Skip = ...)]`. Execution mode resolved to
**sequential**.

| File | Methods / rows | Cases | Active (green) | Skipped | ACs |
|---|---|---|---|---|---|
| `Slices/.../ActiveSpaceEndpointTests.RoleMatrix.cs` | 1 theory | 11 | 11 | 0 | AC2, AC3, AC12 |
| `Slices/.../ActiveSpaceEndpointTests.RoleHeader.cs` | 4 | 4 | 0 | 4 | AC4, AC8, AC9, AC12 |
| `Slices/.../ActiveSpaceEndpointTests.RemoveMembership.cs` (one case extended) | 1 | 1 | 1 | 0 | AC2 (`:837`) |
| `Isolation/IsolationCases.ViewerSpace.cs` | 7 rows | 14 | 14 | 0 | AC2 |
| `Revocation/RevocationCases.cs` (one row added) | 1 row | 2 | 2 | 0 | AC2, AC12 |
| `Architecture/RefusalBoundaryGateTests.RoleDrop.cs` | 11 | 11 | 7 | 4 | AC1, AC3, AC4, AC7, AC11 |
| `Client/ViewerSpaceTests.cs` (new class) | 3 | 4 (the skipped theory reports as 1) | 2 | 1 (theory, 2 rows) | AC1 |
| `Client/RoleDropTests.cs` (new class) | 10 | 10 | 0 | 10 | AC4-AC7, AC11 |
| `Client/MembershipSectionTests.cs` (two added) | 2 | 2 | 0 | 2 | AC9 |
| `Client/LeaveSpaceTests.cs` (two added) | 2 | 2 | 0 | 2 | AC10 |

Supporting (no cases): `tests/Yello.Tests.Shared/RoleHeaderWire.cs`; `tests/Yello.Tests.Client/FakeYelloApi.RoleHeader.cs`
(the header following `Roles[space]`, its CORS exposure, a hold on any Space-scoped read, `RequestCount`) and the two-line
hook in `FakeYelloApi.AnswerAsync`.

### Discovery and verification runs (no full run, per the phase's policy)

| Project / filter | Discovered | Skipped | Passed | Failed |
|---|---|---|---|---|
| Slices, `--filter-method "*Each_matrix_row_governs_..."` | 11 | 0 | 11 | 0 |
| Slices, the four `RoleHeader` methods | 4 | 4 | 0 | 0 |
| Slices, status check (temporary unskip, header asserts removed; restored) - absence, replay, the extended removal case | 3 | 0 | 3 | 0 |
| Revocation, whole project (`REV-DEMOTED-TO-VIEWER-BY-OWNER` both surfaces among them) | 28 | 0 | 28 | 0 |
| Isolation, `IsolationCaseTests` + `IsolationCaseListTests` (the 14 new rows among them; GATE-001..003 and route coverage green) | 362 | 0 | 362 | 0 |
| Architecture, whole suite | 479 | 4 | 475 | 0 |
| Client, `ViewerSpaceTests` | 3 | 1 | 2 | 0 |
| Client, `RoleDropTests` | 10 | 10 | 0 | 0 |
| Client, the four added `MembershipSectionTests` / `LeaveSpaceTests` methods | 4 | 4 | 0 | 0 |

### Green today, left active (standing regression gates)

| Gate | Why it is green now |
|---|---|
| The matrix theory (11) | The pipeline already decides every row by the matrix for product-made Memberships, including a Token issued while a Member and narrowed by the demotion |
| Coverage gate | The theory names exactly the 11 capabilities endpoints declare today |
| Nesting gate | The matrix's sets nest Owner > Admin > Member > Viewer |
| Disabled, shared-channel, interval gates (+ detectors) | No client control is disabled for a Role; no `BroadcastChannel`, `SharedWorker`, `storage` event or `setInterval` exists |
| The 7 isolation rows | The pipeline already refuses or serves each route for a product-joined Viewer |
| `REV-DEMOTED-TO-VIEWER-BY-OWNER` | The Role is read per request; the Token narrows with no reissue |
| The `:837` Token leave probe | The Token dies with the Membership, so the leave by Token is the no-credential `401` |
| Both `ViewerSpaceTests` sweeps | Every per-surface absence already holds; the typed settings address shows no affordance (only the `manage-labels` sentence, which Q6 replaces) |

### Red verified

The four `RoleHeader` cases fail only on the header: with their header assertions removed (temporary edit, original saved
and restored in a `finally`, rebuilt after), the absence case's statuses (`404`, `404`, `401`, `400`, `422`,
`409 ownership-held`) and the replay case's (`201`, the replay `201` with the identical body, then `403 manage-projects`)
all hold today. The browser scaffolds' reason is structural - no `.role-narration` element and no header reader exist.

### Seed data against the real schema

- `FK_Membership_Account_AccountId` (Restrict): every Account registers through the product before it joins.
- `AK_Membership_SpaceId_AccountId`, `UX_Membership_SpaceId_Owner`: every non-Owner Membership is written by the accept or
  the Role change; no Membership is inserted by hand anywhere in this story's scaffolds.
- Every Space is made by `POST /api/v1/spaces` (default Statuses) or is the registration's Personal Space, so the Tasks the
  matrix and header cases create satisfy `FK_Task_Project_SpaceId_ProjectId` and the `(SpaceId, StatusId)` key.
- The Assignee key `(SpaceId, AssigneeMembershipId) -> Membership(SpaceId, Id)`: the matrix assigns only to the Owner's own
  Membership in the same Space.

### Findings for the dev phase

1. **Status discrepancies with the story's prose** (checked against the endpoints):
   - AC3's table says the Invitation issue answers `201`; it answers **`204`**. The theory asserts `204`.
   - AC4's replay ("a write by a Member, the Member demoted, the same key replayed -> the stored `201` with
     `Yello-Role: viewer`") cannot happen: the capability check runs **before** the replay
     (`ActiveSpaceResolution.Idempotency.cs`), so a Viewer's replay is `403 manage-projects`. The scaffold replays as Admin
     demoted to Member (stored `201`, header `member`) and then as Viewer (`403`, header `viewer`).
   - The story's `REV-DEMOTED-TO-VIEWER-BY-OWNER` anchor says "`201` Project create, both surfaces": true, kept.
2. **AC8's runner change is already there.** `Expectation.AssertSameBytes` compares header names (only `Date` is dropped, in
   `WireResponse.ReadAsync`), and so does the revocation runner. Plant 7 (header on the in-Space `404`) is caught by the
   existing Slices comparisons (`BoardEndpointTests:302`, `ProjectEndpointTests:423`, `ListPagingTests:733`,
   `BoardPagingTests:326`, `ChangeMembershipRole:461`) and by `No_answer_but_a_2xx_...`; **not** by
   `ISO-ASSIGN-FOREIGN-TASK-IN-OWN-SPACE`, whose anchor is itself an in-Space `404` and would carry the same header.
3. **Plants the theory cannot catch.** Plant 5 (`Decide` says Yes for Viewer on `ManageTasks`) moves the product and the
   theory's expectation together, because the theory reads `Decide`; the transcription gate catches it. Plant 3 (`PATCH
   project` under `ReadProjectsAndTasks`) is outside the theory's representative routes; `ISO-PROJECT-RENAME-VIEWER`
   catches it.
4. **CORS.** `CrossOriginPolicy.Configure` exposes no header, so a client on another origin (the dev server, the real-Host
   browser suite) can never read `Yello-Role`; `The_cross_origin_policy_exposes_the_Role_header` holds it.
5. **The late-answer case may be unable to go red** if the Space switch aborts the held `fetch` before its answer reaches
   the handler. Confirm plant 11 reddens it; if not, hold a request the switch does not cancel and record the change.
6. **Active tests that change by design** when the narration lands: `LabelTests.A_Role_reread_to_Member_on_Space_settings_...`
   and `MembershipSectionTests.An_Admin_demoted_meanwhile_is_narrated_out_of_Space_settings_on_the_403` (rewrite per AC5),
   `MembershipSectionTests.The_Space_home_narration_region_is_inserted_empty_and_filled_after_the_first_render` (moves to
   `.role-narration`), and every case that changes `api.Roles[...]` mid-case (`TaskDetailTests:613`, `LabelTests:720`), which
   now also see a header-driven narration.
7. **xUnit filters of different kinds AND together** (`--filter-class X --filter-method Y` matches nothing); run them
   separately. Two `--filter-class` options OR.

## Activation order (riskiest assumption first)

Recorded in the story's Dev Notes, "ATDD artifacts - red-phase scaffolds already in the tree". First:
`RoleDropTests.A_drop_on_the_Board_is_narrated_before_any_affordance_is_removed` - it tests the riskiest assumption, Lee's
Q1 (a): that an ordinary answer's header, read cross-origin by the client's handler and passed to one entry point, narrates a
drop before the surface settles.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Implementation guidance

- **Contracts:** `Yello.Contracts/Spaces/RoleHeader.cs` (`HeaderName => "Yello-Role"`, the `Idempotency.cs` shape); swap
  `RoleHeaderWire.HeaderName` for it and delete the stand-in.
- **Host:** the pipeline step that holds `ActiveSpaceContext` writes the header on `2xx` and on the `403 insufficient-role`
  (including `QualifiedRefusal`), after the replay step; `CrossOriginPolicy` exposes it; the `Program.cs` comment.
- **Client:** `YelloApiHandler` reads it for `/api/v1/spaces/{id}` answers and calls `ActiveSpaceState.Reconcile`;
  `RoleDropping` before `Changed`; `RoleNarration.razor` (`.role-narration`, `role="alert"`, `tabindex="-1"`); the
  `SpaceSettings` exit; focus-before-removal; `MembershipSection` copy order and the `:824` re-check; `LeaveSpaceSection` on a
  Session-lifetime token, keyed by `SpaceId`; a static `int RankOf(string)`; `RoleNarration{Admin,Member,Viewer}` and the Q6
  sentence in `ClientCopy.resx`.

## Step 5 - Validation

- [x] Prerequisites satisfied (story with ACs and Lee's Q1, Q4, Q9 decisions; framework; Docker up).
- [x] Test files created at the levels `TESTING-CONVENTIONS.md` binds; no new project, package, fixture or engine.
- [x] Checklist matches the ACs: AC1-AC12 each mapped above (AC11's pipeline-registration clause is the dev phase's).
- [x] Every scaffold whose AC is unimplemented is skipped `red: AC<n> - ...`; every green one is left active and named.
- [x] Every scaffold compiles: `dotnet build Yello.slnx` 0 warnings, 0 errors.
- [x] Seed data satisfies the real schema (constraints listed above).
- [x] Story metadata and handoff paths in the frontmatter; the story's Dev Notes link this checklist.
- [x] No browser or CLI session left open; the temporary red-check script lived in the scratchpad, and the one file it
      touched was restored from memory in a `finally` and rebuilt.
- [x] No full run (this phase's policy); discovery and verification runs above.

**Next workflow:** `bmad-dev-story` for 4.7, then `bmad-code-review`.
