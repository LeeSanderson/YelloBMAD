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
lastSaved: '2026-10-31'
workflowType: 'testarch-atdd'
storyId: '3.2'
storyKey: '3-2-switch-between-spaces-with-nothing-carrying-over'
storyFile: '_bmad-output/implementation-artifacts/3-2-switch-between-spaces-with-nothing-carrying-over.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-3-2-switch-between-spaces-with-nothing-carrying-over.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CacheControl.cs'
  - 'tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.CarryOver.cs'
  - 'tests/Yello.Tests.Architecture/ActiveSpaceStateTests.cs'
  - 'tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/3-2-switch-between-spaces-with-nothing-carrying-over.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-3-1-create-and-rename-a-space.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 3, Story 3.2: Switch between Spaces with nothing carrying over

**Date:** 2026-10-30 (completed 2026-10-31)
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** browser cases against the network-edge fake (`Yello.Tests.Client`, one new class in `ClientSurfaceCollection`), static and IL gates plus one unit case (`Yello.Tests.Architecture`), and one wire case in an existing container class (`Yello.Tests.Slices`, a partial of `ActiveSpaceEndpointTests`).

---

## Story summary

3.2 is a delta on 1.7. The purge seam, the storage ban, manual scroll restoration, the fixed title, the unlisted-id refusal, the switch focus and the announcement already exist. Epic 2 registered its Board, Labels, Projects and List view with that seam. The story proves those rules hold with several real Spaces. It also closes one holder gap (`SpaceNarration`), the route-back loop (`deferred-work.md:358`) and the HTTP-cache door (AC18). The work is proof, not plumbing. **So most browser scaffolds are green today and stay active as standing gates.** Red-phase scaffolds exist only where 3.2 has product work: AC11, AC12 and AC18.

**As** someone who works in several Spaces in a day, **I want** a switch to change the entire working surface and leave nothing of the previous one behind, **so that** I am never in doubt which Space I am in.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - the switcher lists exactly the Account's Spaces by name, and is the only list of them.
2. **AC2** - the Role is the destination's; no filter, sort, scroll, open Task or Project selection survives.
3. **AC3** - Space switch, sign-out, Account switch and Back/Forward purge synchronously before any render.
4. **AC4** - the purge is a data rule.
5. **AC5** - nothing Space-scoped in `localStorage`, `sessionStorage` or IndexedDB.
6. **AC6** - no optimistic first read.
7. **AC7** - an id in a URL confers nothing; no request for an unlisted Space.
8. **AC8** - scroll restoration is disabled on Space-scoped routes.
9. **AC9** - focus moves to the context bar; the new Space and Role are announced politely.
10. **AC10** - two tabs on two Spaces share no state.
11. **AC11** - every Space-scoped holder is registered with the purge; `SpaceNarration` is the gap.
12. **AC12** - the route back never loops (Q1).
13. **AC13** - the route back after removal, reached in-app.
14. **AC14** - the purge is proved by a plant, with a page kept mounted (Q3).
15. **AC15** - keyboard switch, bar Tab order and visible focus; the same on `/spaces/new` and the rename section.
16. **AC16** - an Account switch in one tab.
17. **AC17** - the Role chip and affordances follow the destination.
18. **AC18** - no Space-scoped answer in the browser's HTTP cache (Q2).

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every earlier run recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by `tests/TESTING-CONVENTIONS.md`.

**Prerequisites - met.** The story is `ready-for-dev`. AC1-AC10 are verbatim from `epics.md:1487-1529` (`status: final`), and AC11-AC18 cite their sources. No progress file existed for this story, so Create mode ran from the start. Baseline: `dotnet build Yello.slnx` gave 0 warnings and 0 errors at `4837bfe`. Docker (Rancher) answered `29.5.3`.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Harness read:** `SpaceSwitchingTests`, `FakeYelloApi`, `KeyboardHarness`, `ListViewTests`, `LabelTests`, `SpaceCreateAndRenameTests`, `TaskDetailTests`, `RefusalFingerprint`, `ActiveSpaceStateTests`, `SpaceSwitcherGateTests`, and `ActiveSpaceEndpointTests(.Switcher)`. Product read: `ActiveSpaceState`, `SpaceDirectory`, `SpaceNarration`, `SpaceArrival`, `ContextBar`, `SpaceSwitcher`, `Home`, `SpaceSettings`, `SpaceHome`, `AccountSettings`, `MainLayout`, `Program.cs` and `base.css`.

**Knowledge:** `test-quality`, `data-factories`, `test-levels-framework` and `test-priorities-matrix`, as the earlier runs used them. Where a fragment and `TESTING-CONVENTIONS.md` disagree, the repository document wins.

## Step 2 - Generation mode

**AI generation** from the story, its Dev Notes survey, and the code. No recording. The browser cases were run against the real client and the fake to tell green from red, rather than guessed.

## Step 3 - Test strategy

### Assumed answers to open questions (Q1-Q6 undecided; each at its recommended option)

| Q | Assumed | Scaffolds that depend on it |
|---|---|---|
| Q1 (a) | A per-load exclusion set on `SpaceDirectory` (a member whose name contains "Refused"). Home skips its members, and it can never be a destination | ARCH-004, E2E-012, E2E-013 |
| Q2 (a) | `Cache-Control: no-store` on every `/api` response, set once in the pipeline | SLC-001 |
| Q3 (a) | Plant each layer separately and record which the browser sees | E2E-015 (see the plant record below) |
| Q4 (a)->(b) | Measure first. **Measured green today** (window and every `main` element at offset 0 after a switch and after Back), so Q4 lands as (b) unless the dev phase measures otherwise. The case stays as the framework-change catcher | E2E-010 |
| Q5 (a) | No isolation row. 3.2 adds no `/api` route, and Q2 touches only headers | none |
| Q6 (a) | One `IBrowserContext`, two pages, one `FakeYelloApi` per page seeded identically | E2E-011 |

### Finding the run made (decide in dev-story; the gate assumes the answer below)

**AC1's "the only list of Spaces" already has a second list.** Account settings' Token form renders the Account's Spaces in a `<select>`, to bind a new Token (story 1.8, `AccountSettings.razor:106`). The story's AC1 note names only story 5.4's exception. ARCH-001 is written green today. It allow-lists the 1.8 select as a binding choice rather than a way to reach a Space, and its message names both exceptions. If the dev phase reads AC1 more strictly, the select must change, and so must the allow-list. Record it either way as "AC1 - Token select".

### Container budget

**No new engine, no new fixture type, no new container-backed class.** The one wire case is a partial of the existing `ActiveSpaceEndpointTests` (one Host process, one registered Account). The browser class joins `ClientSurfaceCollection` (one dev server, one Chromium, no Host). No new package and no new project.

### Scenario map

| id | AC | Level | Pri | Scenario | State today |
|---|---|---|:--:|---|---|
| `3.2-ARCH-001` | 1 | ARCH (source) | P0 | Only `SpaceSwitcher.razor` (and 1.8's Token select) loop over the switcher's answer; anchor: the switcher is found | **green, standing**; proved by a plant |
| `3.2-ARCH-002` | 1 | ARCH | P1 | The scan matches `Directory.Current`, `Spaces` and `_spaces` loops, and not Project or Label loops | **green, standing** |
| `3.2-ARCH-003` | 11 | ARCH (IL) | P0 | `Program` calls `ActiveSpaceState.RegisterPurge` and references `SpaceNarration.Forget`; anchor: `Navigating` | skipped `red: AC11` |
| `3.2-ARCH-004` | 12 | ARCH (reflection + source) | P0 | `SpaceDirectory` has a "Refused" member that `RemembersASpace` does not match (regex unchanged); `Home.razor` uses it and has no `spaces[0]` | skipped `red: AC12` |
| `3.2-UNIT-001` | 11 | UNIT | P0 | A narration survives `Navigating(same)` (anchor), and is gone after `Navigating(other)` | skipped `red: AC11` |
| `3.2-SLC-001` | 18 | INT (wire) | P0 | Switcher `200`, active Space `200`, Statuses `200` and boundary `404` (anchors) each carry `no-store` | skipped `red: AC18` |
| `3.2-E2E-001` | 1 | E2E (fake) | P0 | Three Spaces at mixed Roles: exactly three rows by name, no Role text, `aria-current` on the active one only, foot link outside the list | **green, standing** |
| `3.2-E2E-002` | 2, 17 | E2E | P0 | Owner to Viewer: while held, no "Create a Project", no settings link, no chip and no old name; after, `Viewer` and the Viewer accessible name; back to Owner restores the affordances from a fresh read | **green, standing**; the purge plant turns it red |
| `3.2-E2E-003` | 17 | E2E | P0 | Owner's Board has "Add a Task" (anchor); Viewer's Board after it has none | **green, standing** |
| `3.2-E2E-004` | 2 | E2E | P0 | List of A: "Doing" checked and sort changed (anchors); B's List: unchecked, default sort | **green, standing** |
| `3.2-E2E-005` | 2 | E2E | P0 | A's Board with a panel open and the bar's Project name (anchors); B's home: no bar Project and no A names; B's Board: no panel | **green, standing** |
| `3.2-E2E-006` | 3, 4 | E2E | P0 | Back and Forward between A's and B's Boards: while each destination is held, no departed title, Label, Status, Project or Space name is in `body.innerHTML` | **green, standing**; the purge plant turns it red |
| `3.2-E2E-007` | 5 | E2E | P0 | Boards, Lists and settings of two Spaces: storage length 0 and no IndexedDB database | **green, standing** |
| `3.2-E2E-008` | 6, 7 | E2E | P0 | After A's Board, `/spaces/B/projects/{A's project}`: nothing of A while B's Board is held; `404` fingerprint = the unlisted Space's | **green, standing** |
| `3.2-E2E-009` | 8 | E2E | P1 | A's Board scrolled (anchor); after a switch to B's Board and after Back, the window and every `main` element are at 0; `scrollRestoration` is `manual` | **green, standing** (Q4 measurement) |
| `3.2-E2E-010` | 9 | E2E | P0 | Back between Spaces: focus on `nav.context-bar`; the destination is announced exactly once with its Role; nothing said names the departed Space | **green, standing** |
| `3.2-E2E-011` | 10 | E2E | P0 | Two tabs: tab 1 filters, switches and opens a Task in B; tab 2's panel, focus and bar are unchanged, and tab 2 never requested A | **green, standing** |
| `3.2-E2E-012` | 12 | E2E | P0 | A `409` on arrival: the route back lands on Harbour; Northwind is read once | skipped `red: AC12` |
| `3.2-E2E-013` | 12 | E2E | P0 | Both refuse (`409`, `403`): the route back goes to the other one, then stops on the refusal page with the shell; the switcher lists both; each is read once | skipped `red: AC12` |
| `3.2-E2E-014` | 13 | E2E | P0 | A visited, removed from the switcher (refreshed, one row), in-app navigation to A: refused with no request for A; the route back lands on B | **green, standing** |
| `3.2-E2E-015` | 14 | E2E | P0 | Settings B to settings A by Back, page kept mounted: while A is held, no B Label or name in the DOM | **green, standing**; the purge plant turns it red |
| `3.2-E2E-016` | 15 | E2E | P0 | Keyboard only: Tab to the trigger, Enter, Tab to the Harbour row, Enter; focus on the bar; Harbour announced | **green, standing** |
| `3.2-E2E-017` (x4) | 15 | E2E | P0 | Bar Tab order (Owner: skip, switcher, settings, account, sign-out; Viewer: no settings) at 1280x800 and 320x256; visible ring that is not inset at every stop; chip visible | **green, standing** |
| `3.2-E2E-018` | 15 | E2E | P1 | `/spaces/new`: name, Create, Cancel, each with a ring | **green, standing** |
| `3.2-E2E-019` | 15 | E2E | P1 | Rename section: Rename, field, Save, Cancel, each with a ring | **green, standing** |
| `3.2-E2E-020` | 16 | E2E | P0 | Sign out as Account 1 (A, B), the fake becomes Account 2 (C), sign in, no reload: no A/B/Project/title/Label in the DOM; the switcher lists C only; the directory request precedes any Space-scoped one; no request names A or B | **green, standing** |

### Priorities

P0 for the isolation-bearing rules: purge, Role carry-over, storage, ids, two tabs, the route back, the Account switch and the HTTP cache. P1 for scroll and the create and rename Tab orders.

### Duplicate-coverage guard - deliberately not written

- **The one-Space cases of 1.7** (`SpaceSwitchingTests`) stand. E2E-007 is the several-Space path of `No_Space_scoped_content_reaches_web_storage`, and does not duplicate it.
- **`SpaceScopedFirstLoadTests`** stand for AC6/AC7. E2E-008 adds only the cross-Space Project id.
- **NFR-2 on the wire** is `Each_Space_reports_the_Role_held_there_with_no_carry_over_across_switches`. It is not repeated.
- **`ClientStorageBanTests`, `No_client_code_remembers_a_last_Space` and its plant test, the title and scroll-restoration gates, and `The_Role_chip_is_never_interactive`** stay green untouched (Task 6).

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| AC11 in the browser: settings A narrates a lowered Role, the person switches to B before A's home, and B's home says nothing | `SpaceSettings` says the sentence and then navigates on the next `InvokeAsync` turn. `SpaceHome` takes it in `OnInitialized`. A browser cannot put a switch between the two without patching Blazor internals, so a scaffold would only be a race. UNIT-001 and ARCH-003 cover AC11 deterministically. If the dev phase finds a reachable path, write it and record the path | Task 7 |
| The plant table (Story Dev Notes, "Plants") beyond the one run here | The plants exercise product changes 3.2 has not made yet (`Home` back to `spaces[0]`, the exclusion set made a destination, the Q4 reset, `outline: none`, a shared static, `no-store` removed) | Task 6/7 |
| A `TESTING-CONVENTIONS.md` note "Two tabs and an Account switch" | The dev phase owns this documentation (Task 8). The harness facts are below | Task 8 |
| Ledger dispositions (`:358`, `:364`, `:542`, `:669`) | The dev phase writes them in `deferred-work.md`. The cases that close them are E2E-012/013, E2E-014, E2E-015 with the plant record, and E2E-017/018/019 | Task 8 |

## Step 4 - Scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents. Generation stayed in-thread, as every earlier run recorded, because C# under this analyser set has to be built and fixed here anyway. Build fixes on the way: `S3776` (the fake's dispatcher grew too complex, so the active-Space answer moved to `ActiveSpaceAsync`), `CA1823`/`S1144` (an unused constant), and `S107` (a seeding helper with eight parameters). Worker A's output is the Slices and Architecture files. Worker B's output is the browser class and the fake.

| File | Entries (cases) | Green, standing | Skipped (red) | ACs |
|---|---|---|---|---|
| `tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs` (new, `ClientSurfaceCollection`) | 20 (23) | 18 (21) | 2 (E2E-012, E2E-013) | AC1-AC10, AC12-AC17 |
| `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.CarryOver.cs` (new partial) | 4 (4) | 2 (ARCH-001, ARCH-002) | 2 (ARCH-003, ARCH-004) | AC1, AC11, AC12 |
| `tests/Yello.Tests.Architecture/ActiveSpaceStateTests.cs` (one case added) | 1 (1) | 0 | 1 (UNIT-001) | AC11 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CacheControl.cs` (new partial) | 1 (1) | 0 | 1 (SLC-001) | AC18 |
| **Total** | **26 (29)** | **20 (23)** | **6 (6)** | |

### Harness added (test code, active)

- **`FakeYelloApi.ArrivalRefusals`** (`IDictionary<Guid, int>`) refuses a listed Space's arrival with `409 bound-reached` (`bound: concurrent-active-sessions-per-space`, `limit: 50`), `403 insufficient-role`, or otherwise `404 not-available`. It is empty unless a case fills it, so every earlier case is served the bytes it always was. `ConcurrentSessionsBound` holds the stable code of `Bound.ConcurrentActiveSessionsPerSpace`.
- **`FakeYelloApi.ActiveSpaceAsync`**: the existing `GET /api/v1/spaces/{id}` branch moved into its own method (S3776), unchanged except for the refusal check.
- **Account switch:** the fake's identity is changed between sign-out and sign-in by clearing and refilling `Switcher` and `Roles`. No new member was needed.
- **Two tabs:** `context.NewPageAsync()` in one context, with one `FakeYelloApi` per page seeded with the same Space and Project ids. Task ids differ per seed, so each page's panel helper looks them up in its own fake.

### Seed data

No database rows are written by any new case. The slice case uses only the product's registration (`UX_Account_NormalizedEmailAddress` holds with a random address, and every foreign key is the product's own). The browser seeds are fake-side records shaped as the contracts (`SpaceIdentityResponse`, `ActiveSpaceResponse`, the Board, Label and Project shapes). Each Task's `StatusId` is one of its own Board's columns, and each `LabelIds` entry is a Label of its own Space, mirroring the composite foreign keys the Host enforces, checked in `Yello.Infrastructure/Persistence/Migrations`: `FK_Task_StatusDefinition_SpaceId_StatusId`, `FK_TaskLabel_Label_SpaceId_LabelId` and `FK_Label_Space_SpaceId`, all keyed on the same `SpaceId`.

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Every skipped scaffold, activated, fails for its stated reason (2026-10-30/31)

The Skip attributes were removed by a script that held the original content in memory and restored it in a `finally`. Each restored file's SHA-256 matched its original, and the projects were rebuilt from the restored files.

| Scaffold (activated) | Failed with |
|---|---|
| ARCH-003 | `Assert.Contains` - `Program` is not among `ActiveSpaceState.RegisterPurge`'s callers (`BoardView`, `LabelSection`, `ProjectList`, `TaskListView`) |
| ARCH-004 | "SpaceDirectory holds no set of the Spaces that refused this tab (Q1 (a))." |
| UNIT-001 | `Assert.NotNull()` - `SpaceNarration` has no `Forget()` |
| E2E-012 | Timeout at `WaitForPathAsync(/spaces/{Harbour})` (line 593): the route back returned to Northwind, the Space that refused |
| E2E-013 | Timeout at `WaitForPathAsync(/spaces/{Harbour})` (line 622): the same loop |
| SLC-001 | Not activated: it needs a Host and a SQL engine, and its red reason is the story's own survey ("No Space-scoped GET sets Cache-Control"). Confirm on activation |

### Plants run against the standing gates

| Plant (restored in `finally`, SHA-256 checked) | Result |
|---|---|
| A `@foreach (var space in Directory.Current ?? [])` in `Home.razor` | ARCH-001 **red**: "Found: [Components/SpaceSwitcher.razor, Pages/AccountSettings.razor, Pages/Home.razor]" |
| `Purge()` removed from `ActiveSpaceState.Navigating` (Debug client rebuilt) | **Red:** E2E-002 (the Owner affordance survived while held), E2E-006 and E2E-015 ("'Harbour Ops' of the departed Space is still in the DOM"). **Stayed green:** the other 18 active cases, including E2E-008 |

**What the purge plant says about Q3 (record it when closing `:542`).** With the seam's `Purge()` skipped, the departed Space's **name in the bar** survives, and that is what turns E2E-006 and E2E-015 red. No Task title, Label, Status or Project name of the departed Space reached the DOM. The keyed, arrival-gated children (`BoardView`, `LabelSection` and `TaskListView`, each `@key`'d on the Space and rendered only inside an arrived `SpaceArrival`) hold the data rule by a second layer. The dev phase still has to run Q3's combined plant: remove the `@key` and skip the component `Purge` together. That plant must turn E2E-015 red on a Label name.

### Full run (2026-10-31)

```
dotnet build Yello.slnx                      0 Warning(s), 0 Error(s)
dotnet build-server shutdown
dotnet test Yello.slnx --no-build --max-parallel-test-modules 1
Test run summary: Passed!  total: 1614  failed: 0  succeeded: 1599  skipped: 15  (22m 59s)
```

Against the baseline (1,585 total, 1,576 passed, 9 skipped), the run adds 29 cases: 23 green standing gates and 6 red-phase skips. The 9 earlier skips are unchanged (the migration-bundle and explicit-tier cases). The browser suite passed in the combined run. No engine was added.

### Red-phase compliance

- Every red scaffold is skipped with `red: AC<n> - <why> (story 3.2 Task n)`.
- **23 cases are green today and stay active as standing regression gates.** The story is mostly proof of rules 1.7 built, so a green case here is the expected result, not a missed red. Each one is anchored on a presence before its absence. Three of them (E2E-002, E2E-006, E2E-015) and ARCH-001 have been proved against a plant in this run. The dev phase plants the rest per the story's plant table.
- Every scaffold compiles under warnings-as-errors. No scaffold references a type 3.2 has not written, except by name (reflection on `Forget`, and on members named "Refused").

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

1. **E2E-012 `The_route_back_skips_a_listed_Space_that_refused_with_409` first, because it tests the riskiest assumption:** Q1 (a). An exclusion set can end the loop without becoming a remembered last Space. If the set has to choose a destination to make this pass, Q1 (a) is wrong, and this is the moment to find out. Then **ARCH-004** (the regex stays as it is) and **E2E-013** (every Space refuses: the shell stands, no Space is read twice). Plant "go to the last refused Space" and see E2E-012 and `No_client_code_remembers_a_last_Space` turn red.
2. **AC11:** **UNIT-001** (add `SpaceNarration.Forget()`), then **ARCH-003** (`activeSpace.RegisterPurge(spaceNarration.Forget)` in `Program`). Plant: remove the registration, and ARCH-003 turns red.
3. **AC18:** **SLC-001** with Q2 (a) (`no-store` set once for `/api`). Record whether the per-endpoint `no-store` lines stay or are folded in. Plant: remove it, and SLC-001 turns red.
4. **The standing gates** (all green now) are kept green through every change above. Run Q3's combined plant against E2E-015, and the rest of the story's plant table against E2E-002/004/006/009/011/017. Rebuild `Yello.Client` in **Debug** before each browser plant.
5. **Decide "AC1 - Token select"** (see the finding above) and keep ARCH-001's allow-list in step with the decision.

### Commands

```
dotnet build Yello.slnx
dotnet build-server shutdown
dotnet test Yello.slnx --no-build --max-parallel-test-modules 1
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.SpaceSwitchCarryOverTests
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.SpaceSwitcherGateTests --filter-class Yello.Tests.Architecture.ActiveSpaceStateTests
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.Spaces.ResolveActiveSpace.ActiveSpaceEndpointTests
```

Never run two `dotnet test` processes at once. `--no-build` runs whatever was last built.

## Notes and risks

- **E2E-016's keyboard path tolerates the switcher's DOM position.** The rows render after Account and Sign out in the DOM, so Tab from the trigger passes those first. `TabUntilAsync` walks to the row, and the case was green today. If AC15 is read as "the rows are next after the trigger", the dev phase must make that change and tighten the case.
- **E2E-009 measured offset 0 today.** Q4 therefore defaults to (b). If the dev phase adds the reset anyway, the case still holds.
- **E2E-017's ring check is `outline`-based** (`base.css :focus-visible`). A switch to a `box-shadow` ring needs the check widened, and that must be recorded.
- **Locators held by name:** "Create a Project", "Add a Task" (the create control's accessible name), "Space settings", "Back to Yello.", "Create"/"Cancel", "Rename"/"Save"/"Cancel", "Space name". Correct them to the real markup if copy changes, and record it.

---

**Generated by BMad TEA Agent** - 2026-10-31
