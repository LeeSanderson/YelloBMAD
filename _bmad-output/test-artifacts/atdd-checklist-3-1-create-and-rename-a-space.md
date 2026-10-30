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
lastSaved: '2026-10-29'
workflowType: 'testarch-atdd'
storyId: '3.1'
storyKey: '3-1-create-and-rename-a-space'
storyFile: '_bmad-output/implementation-artifacts/3-1-create-and-rename-a-space.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-3-1-create-and-rename-a-space.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CreateSpace.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CreateSpaceBound.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RenameSpace.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Spaces.cs'
  - 'tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.Creation.cs'
  - 'tests/Yello.Tests.Client/SpaceCreateAndRenameTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/3-1-create-and-rename-a-space.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-2-1-create-a-project-and-see-what-is-in-your-space.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 3, Story 3.1: Create and rename a Space

**Date:** 2026-10-29
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server (`Yello.Tests.Slices`, partials of the existing `ActiveSpaceEndpointTests`), isolation rows on both surfaces (`Yello.Tests.Isolation`), ARCH gates (`Yello.Tests.Architecture`), and browser cases against the network-edge fake (`Yello.Tests.Client`).

---

## Story summary

Creating a Space is the first write without `{spaceId}` other than registration, and its bound needs the Account's own Membership count across Spaces inside the creation's transaction. Lee decided Q1 (a) (one named creation unit of work holding both session-context keys), Q2 (a) (an Account-scoped idempotency record) and Q5 (a) (`Space` and `Account` stay policy-free, a recorded variance). Rename is an ordinary Space-scoped `PATCH` gated by the existing `Capability.RenameSpace`.

**As** a freelancer whose client work should not sit beside my personal work, **I want** to create more Spaces and name them, **so that** separate bodies of work are separate by construction.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - the creator becomes Owner; exactly one Membership, the default Statuses, no Projects.
2. **AC2** - creation has no effect on, and no visibility from, any other Space.
3. **AC3** - Owner or Admin renames; a Member or Viewer has no rename affordance.
4. **AC4** - Space settings is reachable from the context bar, Owner and Admin only; absent, not disabled.
5. **AC5** - names are not unique across Yello; no collision, no disclosure.
6. **AC6** - the 51st Space is refused in the same transaction, machine-readable, stated plainly.
7. **AC7** - the AD-24 amendment naming the creation's count is recorded, with its reason.
8. **AC8** - every Space name input is `autocomplete="off"` with a non-reusable field name.
9. **AC9** - the wire contract: `201` + `Location` + `{id, name}`; `200` + `{id, name, role}`; `400 invalid-space-name`; `409 bound-reached`.
10. **AC10** - authority on both routes: `401` without a Session, a Token is no credential on creation; rename `404`/`403`/`200` by Membership and Role; the pipeline decides.
11. **AC11** - creation is atomic and serialised per Account; another Account never waits.
12. **AC12** - every Membership counts; the count is live.
13. **AC13** - after creating: the arrival is a switch, the directory is refreshed first.
14. **AC14** - after renaming: the bar updates in place, polite announcement with no name, title `Yello`, no name in any log.
15. **AC15** - the no-Space state offers one action to create; nothing is created for it.
16. **AC16** - labels, focus, Esc, 1.4.12 text spacing, 200% zoom at 320px; the Role chip survives.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every earlier run recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by `tests/TESTING-CONVENTIONS.md`, so the story's browser cases are written.

**Prerequisites - met.** The story is `ready-for-dev` (`sprint-status.yaml:105`); AC1-AC8 are verbatim from `epics.md:1444-1477` (`status: final`), AC9-AC16 cite their sources. No progress file existed for this story, so Create mode ran from the start. Baseline: `dotnet build Yello.slnx` 0 warnings, 0 errors at `f3da467`. Docker (Rancher) answered `29.5.3`.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. Playwright Utils and Pact fragments not loaded (disabled, TypeScript-shaped). Where a fragment and `TESTING-CONVENTIONS.md` disagree, the repository document wins.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack. No recording. Sources: the story's ACs, Tasks, Testing requirements and open questions (Q1, Q2, Q5 as Lee decided; the rest at their recommended option); the live schema (16 migrations); the existing harness (`HostProcess`, `WireSession`, `SpaceSeeding`, `TaskWire`, `LabelWire`, `ProductActor`, `IsolationWorld`, `FakeYelloApi`, `ClientSurfaceFixture`); and the existing gates (`SpaceSwitcherGateTests`, `RefusalBoundaryGateTests`, `IsolationCaseListTests`).

## Step 3 - Test strategy

### Assumed answers to open questions (still undecided; each the story's recommendation)

| Q | Assumed | Scaffolds that depend on it |
|---|---|---|
| Q3 (a) | A create page linked from the switcher panel's foot (outside its `<ul>`) and from the no-Space state | every `SpaceCreateAndRenameTests` create case reaches the page through the foot link; no case types the route |
| Q4 (a) | A "Space settings" link in the context bar for Owner and Admin; Space home's link removed | `Space_settings_is_linked_from_the_context_bar_...`, `OpenSettingsAsync`, the 320px case, `The_clients_Space_settings_predicate_is_the_union_of_its_sections` |
| Q6 (a) | `SpaceName`: trimmed, 1-200 | both invalid-name theories (201 refused, 200 accepted), `The_Space_name_rule_admits_1_to_200_...`, the fake's `TrySpaceName` |
| Q7 (a) | A rename updates `ActiveSpaceState` without counting as a switch | `Renaming_updates_the_context_bar_in_place_...` asserts focus is NOT moved to the bar |
| Q8 | "Space created." / "Space renamed." carry no Space name | the rename theory asserts the polite text contains "renamed" and not the new name |
| Q9 | `201` + `Location: ActiveOf(id)` + `{id, name}` | the creation slice cases, `ISO-CREATE-SPACE` |
| Q10 | (not asserted) | no scaffold depends on `AddOwnedSpaceAsync` seeding Statuses |

Decided by Lee and asserted as such: **Q1 (a)** (the race, the bound, `Exactly_one_creation_unit_of_work_sets_both_session_context_keys`), **Q2 (a)** (three idempotency cases), **Q5 (a)** (the AD-24 amendment gate looks for `Space` and `Account` in the amendment; slice cases read `Space` without a session context).

### Container budget

**No new engine, no new fixture type, no new container-backed class.** The slice scaffolds are three new partial files of the existing `ActiveSpaceEndpointTests` (its one `MigratedDatabaseFixture` database on the Slices engine). The story named `Spaces/CreateSpace/` and `Spaces/RenameSpace/` folders; the files sit beside the class instead so the namespace matches the folder. The isolation rows run on the existing `IsolationWorldFixture`. The browser class joins `ClientSurfaceCollection` (one dev server, one Chromium, no Host). No new package, no new project.

### Scenario map

| id | AC | Level | Pri | Scenario | File |
|---|---|---|:--:|---|---|
| `3.1-SLC-001` | 1, 9 | INT (wire+db) | P0 | `201`, `Location` = `ActiveOf(id)`, body exactly `{id, name}` trimmed; one Membership, the caller, `Owner`; Statuses = `DefaultStatusSet.Names` by `Position`; 0 Projects | `ActiveSpaceEndpointTests.CreateSpace.cs` |
| `3.1-SLC-002` | 2 | INT (wire+db) | P0 | The creator's Personal Space snapshot (name, Status ids, Memberships, Project count) unchanged; another Account's switcher does not list the new id; anchor: the creator lists both | `.CreateSpace` |
| `3.1-SLC-003` | 5 | INT (wire+db) | P0 | Two Accounts create the same name: both `201`, ids differ, each lists only its own | `.CreateSpace` |
| `3.1-SLC-004` (x3) | 9 | INT (wire) | P1 | Empty, blank, 201 chars = `400 invalid-space-name`, nothing written (not truncated); anchor 200 chars `201` | `.CreateSpace` |
| `3.1-SLC-005` | 9 (AD-2) | INT (wire+db) | P0 | A body `id` is ignored; the answered id is the server's; no Space has the body's id | `.CreateSpace` |
| `3.1-SLC-006` | 10 | INT (wire+db) | P0 | No Session `401`; a Token `401` byte-identical to it; nothing written, nothing recorded; anchor: Session `201` | `.CreateSpace` |
| `3.1-BND-001` | 6, 11, 12 | INT (wire+db) | P0 | Personal + 48 seeded at every Role: the 50th `201` (anchor), the 51st `409 bound-reached`, `spaces-per-account`, `50`; 50 Memberships; no Space of the refused name; no `AccessRefusal` | `.CreateSpaceBound.cs` |
| `3.1-BND-002` | 12 | INT (wire) | P0 | At 50 `409` (anchor); one Membership removed; next creation `201` (live count) | `.CreateSpaceBound` |
| `3.1-BND-003` | 11 | INT (wire+db) | P0 | Race at 49 held on `Yello/SpacesPerAccount/{accountId}`: both blocked, release, exactly one `201` and one `409`; 50 Memberships | `.CreateSpaceBound` |
| `3.1-BND-004` | 11 | INT (wire) | P0 | With A's lock held, B's creation answers `201` without waiting; anchor: A's own creation then waits on it | `.CreateSpaceBound` |
| `3.1-IDM-001` | 9 (Q2) | INT (wire+db) | P0 | Replay with the same key = original status, body, `Location`; one Space | `.CreateSpaceBound` |
| `3.1-IDM-002` | 9, 6 (Q2) | INT (wire+db) | P0 | Replaying the 50th's key at the bound = its `201`; a fresh key `409` (anchor) | `.CreateSpaceBound` |
| `3.1-IDM-003` | 9 (Q2) | INT (wire+db) | P0 | Same key, another body = `422 idempotency-key-reused`, nothing written; another Account's same key = a first use | `.CreateSpaceBound` |
| `3.1-RNM-001` | 3, 9 | INT (wire+db) | P0 | Owner rename `200`, exactly `{id, name, role}`, trimmed, `owner`; persisted; switcher lists it; the Owner's other Space unchanged | `ActiveSpaceEndpointTests.RenameSpace.cs` |
| `3.1-RNM-002` | 3, 10 | INT (wire+db) | P0 | Admin rename `200`, `admin` | `.RenameSpace` |
| `3.1-RNM-003` (x2) | 3, 10 | INT (wire+db) | P0 | Member, Viewer: anchor `GET 200`; `403 insufficient-role`, `rename-space`; one `InsufficientRole` row (`RenameSpace`); name unchanged | `.RenameSpace` |
| `3.1-RNM-004` | 10 | INT (wire+db) | P0 | Non-member: `404 not-available`, bytes = absent Space; `CrossSpace` recorded; foreign name unchanged | `.RenameSpace` |
| `3.1-RNM-005` (x3) | 9 | INT (wire+db) | P1 | Empty, blank, 201 chars = `400 invalid-space-name`, name unchanged; anchor 200 chars `200` | `.RenameSpace` |
| `3.1-RNM-006` | 10 | INT (wire+db) | P0 | Admin's Token renames its own Space `200` (anchor); demoted to Viewer, the same Token `403 rename-space`; name stays | `.RenameSpace` |
| `3.1-RNM-007` | 14 | INT (wire+log) | P0 | No created, renamed, refused or seeded Space name in the Host log; anchors: statuses `201, 200, 403` and the listening line | `.RenameSpace` |
| `3.1-ISO-001` | 2, 10 | INT (wire) | P0 | `ISO-CREATE-SPACE` (`POST /spaces`): Session `Served(201)` - `{id, name}`, `Location`, the caller lists [personal, new], A and B still list only their own, caller is Owner; Token `NoCredential`, then the caller still holds one Space. **Pending until 3.1** | `Isolation/IsolationCases.cs` |
| `3.1-ISO-002` | 10 | INT (wire) | P0 | `ISO-RENAME-SPACE-CROSS` (`PATCH /spaces/{SB}`): both surfaces `BoundaryNotFound`, then B's name unchanged. **Pending until 3.1** | `.IsolationCases` |
| `3.1-ISO-003` | 10 | INT (wire) | P0 | `ISO-RENAME-SPACE-OWN` (in-Space anchor): a fresh Owner renames its own Space on both surfaces `200 {id, name, role: owner}`; B's name unchanged. **Pending until 3.1** | `.IsolationCases` |
| `3.1-ARCH-001` | 3, 4 | ARCH | P0 | The client's rename predicate = the matrix's `RenameSpace` for every Role | `Architecture/RefusalBoundaryGateTests.Spaces.cs` |
| `3.1-ARCH-002` | 4 | ARCH | P0 | The client's Space settings predicate = `RenameSpace` OR `ManageLabels` for every Role (Q4) | `.Spaces` |
| `3.1-ARCH-003` | 9 (AD-2) | ARCH | P0 | `CreateSpaceRequest` and `RenameSpaceRequest` carry `Name` (string) and nothing else | `.Spaces` |
| `3.1-ARCH-004` | 9 | ARCH | P1 | `SpaceName.MaxLength` = 200 = `SpaceConfiguration.NameMaxLength`; `TryNormalise` at null, empty, blank, trimmed, 1, 200, 201, padded 200 | `.Spaces` |
| `3.1-ARCH-005` | 6, 7 | ARCH | P0 | `Bound.SpacesPerAccount` is declared by exactly `POST /api/v1/spaces` | `.Spaces` |
| `3.1-ARCH-006` | 10 | ARCH | P0 | `PATCH` Active declares exactly `RenameSpace`; `POST` Directory declares no capability; no `CreateSpace` capability exists | `.Spaces` |
| `3.1-ARCH-007` | 7, 11 | ARCH | P0 | Exactly one type sets both keys; it is neither the resolver nor the directory; the `AccountId` key setters are exactly the directory and it (Q1 (a)) | `Architecture/SpaceSwitcherGateTests.Creation.cs` |
| `3.1-ARCH-008` | 7 | ARCH (doc) | P0 | AD-24 holds "Amendment (2026-10-29, story 3.1)", Space creation, row-level security, second connection, bypass, FR-3, `Space`, `Account`; its heading still says two; `EXPERIENCE.md` says "Recorded in AD-24, 2026-10-29" | `.Creation` |
| `3.1-E2E-001` | 13 | E2E (fake) | P0 | Switcher foot link (outside the list; rows still 2); create; POST, then directory GET, then arrival GET, in that order; `Idempotency-Key`; lands at `/spaces/{new}`; empty home; focus on the bar; polite announcement names the Space and "Owner"; title `Yello`; switcher lists it | `Client/SpaceCreateAndRenameTests.cs` |
| `3.1-E2E-002` | 15 | E2E (fake) | P0 | No-Space state: exactly one action, "Create a Space"; following it opens the field and sends no POST | `.SpaceCreateAndRenameTests` |
| `3.1-E2E-003` | 6 | E2E (fake) | P0 | `409 spaces-per-account` renders "This Account has 50 Spaces, the maximum." in `main`; no navigation; switcher unchanged | `.SpaceCreateAndRenameTests` |
| `3.1-E2E-004` | 9 | E2E (fake) | P1 | Blank name: the field is `aria-invalid` with a described message; no navigation; nothing created | `.SpaceCreateAndRenameTests` |
| `3.1-E2E-005` | 13 (Q2) | E2E (fake) | P0 | A lost answer, then resubmit: same `Idempotency-Key` twice; one Space | `.SpaceCreateAndRenameTests` |
| `3.1-E2E-006` (x2) | 3, 14 | E2E (fake) | P0 | Owner and Admin: held rename keeps the form open and the old name in the bar; released, the bar's text and `aria-label` show the new name, no reload, focus not on the bar, the polite text says "renamed" without the name, title `Yello`, switcher lists it | `.SpaceCreateAndRenameTests` |
| `3.1-E2E-007` (x2) | 3, 4 | E2E (fake) | P0 | Owner anchor: the bar's "Space settings" link to `/spaces/{N}/settings`, never on the Role chip; Member and Viewer: no link, no rename, nothing disabled | `.SpaceCreateAndRenameTests` |
| `3.1-E2E-008` | 16 | E2E (fake) | P1 | Esc closes the rename form, no `PATCH`, focus back on "Rename" | `.SpaceCreateAndRenameTests` |
| `3.1-E2E-009` | 8 | E2E (fake) | P0 | Both inputs FOUND first (create, rename), each `autocomplete="off"` with a non-reusable `name`; the create page's name differs between two renders | `.SpaceCreateAndRenameTests` |
| `3.1-E2E-010` (x2) | 16 | E2E (fake) | P1 | 320x256 with text spacing / 200% text zoom: Role chip visible beside the link; rename and create with a 100-character name reflow (no sideways scroll) | `.SpaceCreateAndRenameTests` |

### Priorities

P0 for everything the isolation gate (SM-1), the bound (AR-30, AD-25), Q1's dual-key unit of work, the AD-24 amendment, idempotency and the Role decisions rest on. P1 for name validation edges, Esc and the reflow measurements.

### Duplicate-coverage guard - deliberately not written

- **The capability matrix cell `RenameSpace`** is already transcribed and gated (`The_capability_matrix_agrees_with_the_transcription_in_every_cell`); the new `403` cases prove the route declares it, not the matrix.
- **The bound's copy and its registry entry** are already held (`ClientCopy.resx` `spaces-per-account`, `The_bound_registry_declares_all_six_bounds_with_their_values_and_operations`); E2E-003 proves only that the page renders it.
- **"No Space from another Account in the switcher"** is `ISO-LIST-SPACES`; SLC-002 and ISO-001 assert only the new id's absence.
- **Typing the settings URL as a Member** already shows the capability copy (story 2.4, `LabelTests`); not repeated.

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| The Account-scoped idempotency table's schema (non-nullable `AccountId`, FILTER and BLOCK on `fn_AccountIsolationPredicate`, unique `(AccountId, Key)`, exact `Down`) | The table's name is the dev phase's; a by-name catalogue query would be a guess. `SpaceIsolationSchemaTests`-style registration in an Account-scoped list is Task 4's | Task 4, Q2 (a) |
| Amending the six active gates listed in `RefusalBoundaryGateTests.Spaces.cs`'s remarks | They are green today and go red only when the product lands; amending them now would weaken them before there is anything to admit | Task 9 |
| A gate that every `Space` write filters on the resolved `SpaceId` | The rename store does not exist; the static form (the one `ExecuteUpdate` on `Space`) needs its type name | Task 9 |
| The client's name-input gate over `*.razor` | E2E-009 finds both inputs in the rendered page, which is the stronger form; a static scan is optional | Task 9 |
| Handler-level tests of the provisioning helper (registration's behaviour unchanged) | The helper does not exist; registration's existing tests (`RegisterAccountHandlerTests`, `RegisterAccountIntegrationTests`) are the regression guard | Task 3 |
| `TESTING-CONVENTIONS.md` "Account-scoped creation" note | Documentation the dev phase owns (Task 11); this run's harness facts are in this checklist | Task 11 |
| AC11 "commit together or not at all" under an injected mid-creation failure | No seam exists over the wire to fail between the inserts; BND-001 proves a refused creation writes nothing, and ARCH-007 holds the one unit of work | Task 4 (a store-level test if the dev phase adds a seam) |

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents; generation stayed in-thread, as every earlier run recorded, because C# under this analyser set has to be built and fixed here anyway. Build fixes on the way: `IDE0007` (x6), `IDE0005`/`S1128` (two unused usings), `MA0181` (an `int` cast to `HttpStatusCode`). Worker A's output is the Slices, Isolation and Architecture files; worker B's is the browser class and the fake.

### `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace` - 25 cases in 20 skipped entries, existing class

| File | Entries (cases) | ids | ACs |
|---|---|---|---|
| `ActiveSpaceEndpointTests.CreateSpace.cs` | 6 (8) | SLC-001 to SLC-006 | AC1, AC2, AC5, AC9, AC10 |
| `ActiveSpaceEndpointTests.CreateSpaceBound.cs` | 7 (7) | BND-001 to BND-004, IDM-001 to IDM-003 | AC6, AC9, AC11, AC12 |
| `ActiveSpaceEndpointTests.RenameSpace.cs` | 7 (10) | RNM-001 to RNM-007 | AC3, AC9, AC10, AC14 |

### `tests/Yello.Tests.Isolation/IsolationCases.cs` - 3 rows x 2 surfaces, pending until 3.1

`ISO-CREATE-SPACE` (POST `SpaceRoutes.Directory`), `ISO-RENAME-SPACE-CROSS` and `ISO-RENAME-SPACE-OWN` (PATCH `SpaceRoutes.Active`), each with a real `Prepare` and expectations on both surfaces, `PendingUntil: "3.1"` and a `red: AC10 - ...` reason. Activation is deleting `PendingUntil`/`PendingReason`. GATE-003 fails the suite if any is still pending when 3.1 reaches `review`. The write rows act as a **fresh actor** (`world.NewActorAsync`), because A's switcher is `ISO-LIST-SPACES`' subject and A's Space name is every row's presence anchor; the case body's name is added to that actor's secrets (`HoldsSpaceNamed`), so I-17 checks it.

### `tests/Yello.Tests.Architecture` - 8 skipped

| Test | id | AC |
|---|---|---|
| `RefusalBoundaryGateTests.The_clients_rename_predicate_agrees_with_the_capability_matrix` | ARCH-001 | AC3, AC4 |
| `RefusalBoundaryGateTests.The_clients_Space_settings_predicate_is_the_union_of_its_sections` | ARCH-002 | AC4 |
| `RefusalBoundaryGateTests.The_Space_request_contracts_carry_a_name_and_nothing_else` | ARCH-003 | AC9 |
| `RefusalBoundaryGateTests.The_Space_name_rule_admits_1_to_200_characters_and_the_column_agrees` | ARCH-004 | AC9 |
| `RefusalBoundaryGateTests.The_Spaces_per_Account_bound_is_consulted_by_the_creation_route_alone` | ARCH-005 | AC6, AC7 |
| `RefusalBoundaryGateTests.Rename_declares_RenameSpace_and_creation_declares_no_capability` | ARCH-006 | AC10 |
| `SpaceSwitcherGateTests.Exactly_one_creation_unit_of_work_sets_both_session_context_keys` | ARCH-007 | AC7, AC11 |
| `SpaceSwitcherGateTests.The_AD_24_amendment_names_Space_creation_and_states_its_reason` | ARCH-008 | AC7 |

### `tests/Yello.Tests.Client/SpaceCreateAndRenameTests.cs` - 13 cases in 10 skipped entries

E2E-001 to E2E-010, in `ClientSurfaceCollection`.

### Harness added (test code, active, no active case depends on it yet)

- `FakeYelloApi`: `POST /api/v1/spaces` (key replay, `SpaceBoundReached`, the name rule, a new Owner Space added to `Switcher` and `Roles`, `{id, name}`) and `PATCH /api/v1/spaces/{id}` (unlisted `404`, Member/Viewer `403 rename-space`, the name rule, the switcher row renamed, `{id, name, role}`); `HoldSpaceCreateAsync`/`ReleaseSpaceCreate`, `HoldSpaceRenameAsync`/`ReleaseSpaceRename`, `LoseNextSpaceCreateAnswer`, `InvalidSpaceNameType`. No earlier case sends either request (both fell to the unmatched `404`), so every existing case is served the bytes it always was.
- Slice helpers in the partials: `CreateSpaceAsync`, `RenameSpaceAsync`, `SeedMembershipsAsync`, `MembershipsInAsync`, `MembershipCountOfAsync` (under the `AccountId` key), `StatusNamesInOrderAsync`, `SpaceNamesOfAsync`, `SpaceIdsNamedAsync`, `SnapshotAsync`, `TakeApplicationLockAsync`, `SpacesPerAccountLockOf`.
- Isolation helpers: `FreshActorCreatesASpaceAsync`, `TheCreatedSpaceIsTheCallersAloneAsync`, `TheTokenCreatedNoSpaceAsync`, `TheForeignSpaceKeepsItsNameAsync`, `FreshActorRenamesItsOwnSpaceAsync`, `RenamedOnlyTheCallersOwnSpaceAsync`.

### Scaffold-held names (correct on activation; record any correction in the Debug Log)

| Held here | Stands for | Where |
|---|---|---|
| `"urn:yello:problem:invalid-space-name"` | `ProblemTypes.InvalidSpaceName` | `ActiveSpaceEndpointTests.InvalidSpaceNameType`, `FakeYelloApi.InvalidSpaceNameType` |
| `Yello/SpacesPerAccount/{accountId:D}` | `ApplicationLocks` Spaces-per-Account resource (Task 4) | `SpacesPerAccountLockOf` |
| `Yello.Contracts.Spaces.CreateSpaceRequest` / `RenameSpaceRequest` | Task 4 contracts | ARCH-003 |
| `Yello.Domain.Spaces.SpaceName` (`MaxLength`, `TryNormalise`) | Task 3 | ARCH-004 |
| a client predicate whose name contains `RenameSpace` / `SpaceSettings` | `SpaceCapabilities.PermitsRenameSpace` / `PermitsSpaceSettings` | ARCH-001, ARCH-002 |
| "Create a Space", "Space name", "Rename", "Space settings" | Task 8 copy (`CreateSpaceAction`, `SpaceNameLabel`, `RenameSpaceAction`, `SpaceSettingsLink`) | client locators |

### Fixtures and seed data - every constraint checked

No new fixture type and no new container-backed class. Checked against `20260828164716_InitialSchema.cs` and `20261024143930_AccountScopedMembership.cs`:

- **Through the product:** Accounts, Personal Spaces with their Owner Membership and three Statuses, Tokens. `UX_Account_NormalizedEmailAddress` holds (every address random); `FK_ApiToken_Membership_BoundSpaceId_AccountId` holds because a Token is issued in a Space the Account belongs to.
- **`SeedMembershipsAsync` / `SeedNamedSpaceAsync`** (existing `SpaceSeeding`): one B2 block per Space - the `Space` row and its one `Membership` in the same `SaveChanges`, under that Space's key - so `FK_Membership_Space_SpaceId` and the RLS `BLOCK AFTER INSERT` predicate on `Membership` hold; `FK_Membership_Account_AccountId` holds (the Account was registered by the product); one Membership per Space keeps `IX_Membership_SpaceId_AccountId` unique; a non-Owner Membership leaves its Space with no Owner, which the filtered `UX_Membership_SpaceId_Owner` permits; `Role` is stored by name within `nvarchar(20)`. **Verified by running BND-001 activated: the 48 seeded blocks committed and the case failed at the 50th creation's `404`, not in seeding.**
- **`SeedForeignSpaceAsync`** (existing): the foreign Account before its Membership, the Space before both.
- **The Token's demotion** (RNM-006) re-attaches the tracked Membership in a B2 block, exactly as `A_Token_acts_with_the_current_Role_read_on_each_request` does.
- **`RemoveMembershipAsync`** (BND-002) deletes one Admin Membership under its Space's key (the FILTER predicate), asserting one row.
- **`MembershipCountOfAsync`** reads under the `AccountId` key, which story 1.7's predicate uses to show exactly that Account's Memberships across Spaces; the explicit `WHERE AccountId` keeps it the caller's own.
- **The race hold** takes an application lock on a connection with no session context: an application lock is not a row, so no predicate applies.

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Every activated scaffold fails for its stated reason - 2026-10-29

The Skip attributes of the two Architecture files and the three Slices files were removed in a script that held the original content in memory and restored it in a `finally`; each restored file's SHA-256 matched its original. The test projects were rebuilt from the restored files before the full run.

| Scaffold (activated) | Failed with |
|---|---|
| ARCH-001, ARCH-002 | "No static bool (string roleCode) method named for RenameSpace / SpaceSettings in Yello.Client." |
| ARCH-003 | "Yello.Contracts.Spaces.CreateSpaceRequest, Yello.Contracts is not declared." |
| ARCH-004 | "Yello.Domain.Spaces.SpaceName is not declared." |
| ARCH-005 | Expected `["POST /api/v1/spaces"]`, actual `[]` |
| ARCH-006 | `Assert.Single()` - no `PATCH` on the active Space's route |
| ARCH-007 | `Assert.Single()` - the both-keys intersection is empty |
| ARCH-008 | 4 of 6 required phrases absent from AD-24 |
| SLC-001, SLC-006 | `NotFound` where `Created` was expected (POST unmapped) |
| BND-001 | "Anchor: NotFound ... not-available" - after 48 seeded Memberships committed |
| RNM-001, RNM-003 (Member, Viewer) | failed; their assertion text was not captured by the run's output filter, so the reason is inferred (PATCH unmapped), not seen. Confirm on activation |

### Full run

```
dotnet build Yello.slnx                      0 Warning(s), 0 Error(s)
dotnet build-server shutdown
dotnet test Yello.slnx --no-build --max-parallel-test-modules 1
Test run summary: Passed!  total: 1564  failed: 0  succeeded: 1517  skipped: 47  (18m 03s)
```

The 47 skipped are the 3 expected migration-bundle cases (`YELLO_MIGRATION_BUNDLE` unset) and this run's red-phase scaffolds. The three pending isolation rows run as nothing, and GATE-002 and GATE-003 stay green with 3.1 at `ready-for-dev`. The browser suite passed in the combined run. No engine was added.

### Red-phase compliance

- Every new case is skipped with `red: AC<n> - <why> (story 3.1 Task n)`, or is an isolation row pending with a `red: AC10 - ...` reason.
- **No new case is green today**, so this run adds no standing gate. Every scaffold depends on a route, type, page or document change 3.1 has not made.
- Every scaffold compiles under warnings-as-errors. No scaffold references a type 3.1 has not written, except by name.
- Every absence is anchored on a presence in the same case: the 50th before the 51st; the refusal at 50 before the admission after a removal; the Session's `201` beside the Token's `401`; the read `200` before the `403`; the absent-Space `404` beside the non-member's; A's own waiting creation beside B's non-waiting one; the Owner's link before the lesser Role's absence; both inputs found before the autofill assertions; the log's listening line before the name's absence.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

1. **`The_51st_Space_is_refused_409_bound_reached_and_the_Account_still_holds_50` (BND-001) first - the riskiest assumption.** It cannot pass until Q1 (a) works end to end: one connection carrying both `@read_only` keys, the `Membership` and `StatusDefinition` `BLOCK AFTER INSERT` predicates admitting the new Space's rows under the server-generated `SpaceId` key, and the count under the `AccountId` key in the same transaction. If SQL Server or the `MembershipVisibilityPredicate` refuses that combination, the whole of Q1 (a) is in question, and that is the moment to find out. Then **SLC-001** (the bare creation), **ARCH-007** (exactly one both-keys caller; amend `No_caller_sets_both_session_context_keys` to admit it in the same change), **BND-003** (the race - if it passes with the application lock removed, it did not race; plant that and record it), **BND-004**, **BND-002**, **ARCH-005**.
2. **ARCH-008** with Task 2 (the AD-24 amendment and `EXPERIENCE.md:190`).
3. **ARCH-003, ARCH-004** (Task 3, `SpaceName` and the contracts), then **SLC-004**, **SLC-005**, **SLC-002**, **SLC-003**, **SLC-006**. Replace `InvalidSpaceNameType` with `ProblemTypes.InvalidSpaceName`.
4. **Idempotency** (Task 4, Q2 (a)): **IDM-001**, **IDM-003**, **IDM-002** (replay before the bound).
5. **Rename** (Task 5): **ARCH-006**, **RNM-001**, **RNM-002**, **RNM-003**, **RNM-004**, **RNM-005**, **RNM-006**, **RNM-007**.
6. **Isolation** (GATE-002): delete `PendingUntil`/`PendingReason` from `ISO-CREATE-SPACE`, then `ISO-RENAME-SPACE-CROSS`, then `ISO-RENAME-SPACE-OWN`. GATE-003 fails at `review` if any is still pending.
7. **Client** (Tasks 6-8): **ARCH-001**, **ARCH-002**, then E2E-002 (no-Space), E2E-001, E2E-003, E2E-004, E2E-005, E2E-007, E2E-006, E2E-008, E2E-009, E2E-010. Build `Yello.Client` in Debug before any browser plant. Correct locators to the real markup if needed, and record it.
8. **Task 9's gate amendments** to the six active gates named in `RefusalBoundaryGateTests.Spaces.cs`'s remarks, each proved against a plant (save the original content and restore it in a `finally`; never `git checkout`/`restore`/`reset`).

### Commands

```
dotnet build Yello.slnx
dotnet build-server shutdown
dotnet test Yello.slnx --no-build --max-parallel-test-modules 1
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.Spaces.ResolveActiveSpace.ActiveSpaceEndpointTests
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.RefusalBoundaryGateTests
dotnet test --project tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj --no-build
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.SpaceCreateAndRenameTests
```

Never two `dotnet test` processes at once. `--no-build` runs whatever was last built.

## Notes and risks

- **The lock name and the seeded count carry the largest correction risk.** `SpacesPerAccountLockOf` holds the story's indicative name; if Task 4 spells it otherwise, BND-003 and BND-004 fail at "fewer than 2 creations ever waited" - correct the constant, not the product.
- **BND-001's arithmetic:** the Personal Space plus 48 seeded is 49; the 50th is the anchor. If Q10 or registration changes what registration provisions, recount.
- **`ISO-CREATE-SPACE` on the Token surface expects `NoCredential`** - byte-identical to an anonymous `POST`. If the pipeline answers an anonymous `POST` with an anti-forgery `400` before the `401`, the anchor and the answer still match, but the expectation's `401` will not; then the Session-only marker's order is the question, not the row.
- **The rename theory asserts focus is NOT moved** (Q7 (a)). If Q7 lands as (b), this case is the one that must change, and why.
- **Six active gates go red by design** when the product lands (listed in `RefusalBoundaryGateTests.Spaces.cs`). That is expected; amend each for exactly the one named exception and prove a second exception still fails.
- **Container cost:** none today. On activation the slice cases add Host processes on the class's existing database; the isolation rows add two fresh actors per surface run on the existing world.

---

**Generated by BMad TEA Agent** - 2026-10-29
