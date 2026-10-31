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
storyId: '3.3'
storyKey: '3-3-delete-a-space-and-belong-to-none'
storyFile: '_bmad-output/implementation-artifacts/3-3-delete-a-space-and-belong-to-none.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-3-3-delete-a-space-and-belong-to-none.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteSpace.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteSpaceRace.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Revocation/RevocationCases.cs'
  - 'tests/Yello.Tests.Revocation/RevocationCaseTests.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.DeleteSpace.cs'
  - 'tests/Yello.Tests.Architecture/SpaceDeletionClientGateTests.cs'
  - 'tests/Yello.Tests.Client/SpaceDeleteTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.SpaceDeletion.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/3-3-delete-a-space-and-belong-to-none.md'
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
  - '_bmad-output/test-artifacts/atdd-checklist-3-2-switch-between-spaces-with-nothing-carrying-over.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
---

# ATDD Checklist - Epic 3, Story 3.3: Delete a Space, and belong to none

**Date:** 2026-10-31
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** wire cases on the real engine as partials of the existing `ActiveSpaceEndpointTests` container class (`Yello.Tests.Slices`); isolation rows on both surfaces (pending) and one revocation row; IL, route-table and client-type gates (`Yello.Tests.Architecture`); browser cases against the network-edge fake (`Yello.Tests.Client`, one new class in `ClientSurfaceCollection`).

---

## Story summary

3.3 is a new slice on a schema that already knows how to delete a Space. One `DELETE` of the `Space` row under its own session key cascades to every Space-scoped table and takes the Tokens with the Memberships; `AccessRefusal` has no FK and stays. The work is in the pipeline: the request's own unit of work destroys the Space its presence row and idempotency record hang off (Q1), and every concurrent Space-scoped write can lose its Space under it (Q4). Then the client: the confirm, its counts, the route afterwards, and five ledger closures (`deferred-work.md:356`, `:360`, `:370`, `:382`, `:666`, `:679`).

**As** someone finished with a body of work, **I want** to destroy a Space and be told exactly what that takes with it, **so that** the most destructive operation in the product is never taken by accident.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - Owner deletes; Projects, Tasks, Memberships, Invitations destroyed; an Admin has no affordance.
2. **AC2** - the confirm names the Space, its Project and Task counts, and that other Accounts lose access.
3. **AC3** - the typed name, `autocomplete="off"`, a non-reusable field name.
4. **AC4** - immediate, irreversible; every Token invalidated in the same transaction.
5. **AC5** - other Accounts lose this Space only.
6. **AC6** - belonging to none is valid; no Space is undeletable.
7. **AC7** - the confirm replaces the settings panel in place; one level of modal.
8. **AC8** - "Delete Northwind Redesign?" and "This cannot be undone." with no reassurance before it.
9. **AC9** - the wire contract: `204`; boundary `404`; `403 insufficient-role`; `400` on a malformed key; a replay never applies twice or answers `500`; the counts read is Owner-only; no Space name anywhere.
10. **AC10** - one `ExecuteDeleteAsync` in `SpaceStore` under the Space key only; cascades proved with data; `AccessRefusal` survives; S-1 atomicity.
11. **AC11** - Tokens `401` and Sessions `404` on the next request; other Spaces keep working; an endpoint-driven revocation row.
12. **AC12** - the client after a deletion: not optimistic; `Removed(id)`, purge, `/` with `replace`, home or the no-Space state, focus, one nameless announcement; `5xx`/no answer keeps the confirm and the key; `404` routes like `204` with no announcement.
13. **AC13** - the races deletion makes reachable answer the boundary `404`, never `200` or `500`.
14. **AC14** - the switcher with nothing to list explains itself; a failure is said differently.
15. **AC15** - the refusal page at `/` points to the switcher instead of linking to itself.
16. **AC16** - keyboard, focus, Esc, rings, 320x256 reflow.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every earlier run recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by `tests/TESTING-CONVENTIONS.md`.

**Prerequisites - met.** The story is `ready-for-dev`; AC1-AC8 are verbatim from `epics.md:1539-1573` (`status: final`) and AC9-AC16 cite their sources. No progress file or checklist existed for this story, so Create mode ran from the start. Baseline: `dotnet build Yello.slnx` gave 0 warnings and 0 errors at `a8fe2c6`. Docker (Rancher) answered `29.5.3`.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Harness read:** `ActiveSpaceEndpointTests` (all partials), `SpaceSeeding`, `TaskWire`, `DeleteWire`, `LabelWire`, `TaskEditWire`, `ProjectEndpointTests.DeleteRace`, `WireSession`, `IsolationCases` and its expectations and list gates, `RevocationCases` and `RevocationCaseTests`, `RefusalBoundaryGateTests(.Spaces)`, `DeletionCopyGateTests`, `SpaceSwitcherGateTests(.CarryOver)`, `FakeYelloApi`, `DeleteTests`, `SpaceCreateAndRenameTests`, `SpaceSwitchCarryOverTests`, `KeyboardHarness`. Product read: `ActiveSpaceResolution`, `SpacePresenceRegister`, `ActiveSpaceNameReader`, `ActiveSpaceEndpoints`, `SpaceStore`, `SpaceScope`, `ProjectConfiguration`, the EF model snapshot, `Idempotency`, `ProblemTypes`, `SpaceRoutes`, `SpaceSettings.razor`, `SpaceNameSection.razor`, `ContextBar.razor`, `SpaceSwitcher.razor`, `Home.razor`, `RefusalSurface.razor`, `SpaceDirectory`, `SpaceCapabilities`, `ClientCopy.resx`.

**Knowledge:** `test-quality`, `data-factories`, `test-levels-framework` and `test-priorities-matrix`, as the earlier runs used them. Where a fragment and `TESTING-CONVENTIONS.md` disagree, the repository document wins.

## Step 2 - Generation mode

**AI generation** from the story, its Dev Notes survey and the code. No recording. Scaffolds that exercise only existing routes were activated against the real engine to tell green from red rather than guess.

## Step 3 - Test strategy

### Assumed answers to open questions (Q1-Q9 undecided; each at its recommended option)

| Q | Assumed | Scaffolds that depend on it |
|---|---|---|
| Q1 (a) | The route carries a marker (any endpoint metadata whose type name contains `Destroys`; the story's example is `DestroysItsSpace()`); the pipeline validates and looks up the key but records nothing; a replay after a committed deletion is the boundary `404`. **No idempotency record on the delete route, so no second dual-key caller** - both both-keys gates stay as they are | SLC-001, SLC-008, SLC-009, ARCH-003, E2E-011, E2E-012 |
| Q2 (a) | `GET /api/v1/spaces/{spaceId}/deletion`, Owner-only (`DeleteSpace`), answering `{projects, tasks, otherMembers}`. The path is a literal in four places until `SpaceRoutes` names it - retarget on activation | SLC-010, SLC-011, SLC-012, ARCH-002, ISO-DELETION-SUMMARY-*, the fake, E2E-005 |
| Q3 (a) | `DELETE` has no body; the server does not check the typed name | every deletion helper |
| Q4 (a) | A unit of work that meets 547 because its Space is gone answers the uniform boundary `404` with a `CrossSpace` record; `SpaceStore.DeleteAsync` takes `Yello/ProjectsPerSpace/{spaceId}` first | RACE-S1..S5 (the test plays the deletion in that shape) |
| Q5 | Heading "Delete {name}?", then "This cannot be undone.", then "It holds {p} Projects and {t} Tasks." (singular forms as `ProjectHoldsTasks`), then "{n} other people will lose access to it." / "No one else has access to it."; label "Type the Space name to confirm"; button "Delete this Space"; mismatch "Type the Space name exactly as shown."; trimmed, case-sensitive match; focus on the field on open | E2E-003..E2E-008, E2E-017, ARCH-005 |
| Q6 (a) | "Space deleted.", no name, said once by the destination | E2E-009, E2E-011, ARCH-005 |
| Q7 (a) | `NoSpaceBody` in the panel outside the `<ul>`; "Your Spaces could not be listed." on a failed refresh with nothing held | E2E-013, E2E-014, ARCH-005 |
| Q8 (a) | On `/`, `RefusalSurface` shows "Choose a Space from the switcher above." instead of the link; heading stays "Not available." | E2E-015, E2E-016, ARCH-005 |
| Q9 | A Personal Space that is the Account's only Space is deletable; the Account then belongs to none and can create one | SLC-006, ISO-DELETE-SPACE-OWN, E2E-010 |

### Findings the run made (decide or record in dev-story)

1. **The race mechanics are real, and they fail today exactly as the survey predicted.** RACE-S1..S4 and S6 need no new route, so they were activated against the engine: each request blocked on a lock (the strict `WaitUntilBlockedOnALockAsync` passed) and then failed at its final assertion - S1 `200` with the never-written name (`:666`), S2/S3/S4 `500` from 547, S6 `InvalidOperationException: Sequence contains no elements` (`:360`). One run each; the dev phase reruns them 3/3 when activating.
2. **The name read (`:360`) has no wire race.** Under RCSI a `GET` by a present Account takes no lock between its resolution and the name read, so a test has nothing to hold. RACE-S6 drives `ActiveSpaceNameReader` directly (the slice suite has `InternalsVisibleTo`) with a context resolved for a Space deleted afterwards - the state the race produces. The story's plant "restore `SingleAsync`" lands on S6. Record in Completion Notes.
3. **`RevocationCases` has no pending mechanism**: a row in `All` runs. So `REV-SPACE-DELETED-BY-OWNER` is held in `RevocationCases.SpaceDeletedByOwner` and run by a skipped theory `The_Owners_deletion_of_the_Space_governs_the_very_next_request`. On activation: move the row into `All`, delete the theory. `REV-SPACE-DELETED` (raw SQL) stays as the schema proof (Task 7's "keep", recorded here).
4. **`The_clients_Space_settings_predicate_is_the_union_of_its_sections` is green before and after**: an Owner-only section adds no Role to the union. Widen its expected expression to name `DeleteSpace` when the section lands, or it never says anything about deletion.
5. **Focus after a deletion**: AC12 says the destination page's heading. The route home ends on a Space home after a Space change, which 3.2 treats as a switch (focus to the context bar). E2E-009 asserts the `h1`; if the product cannot give both, that is a contradiction to record, not a locator to loosen.
6. **The populated-Space case is also Q1's riskiest probe.** SLC-001 sends an `Idempotency-Key`, as the client always does, so a pipeline that still records the key meets `FK_IdempotencyRecord_Space` (547) and answers `500`.

### Container budget

**No new engine, fixture type or container-backed class.** The 18 Slices entries are partials of `ActiveSpaceEndpointTests` (its one database on the suite's one engine). The isolation and revocation rows run in the existing worlds. The browser class joins `ClientSurfaceCollection` (one dev server, one Chromium, no Host). No new package and no new project.

### Scenario map

| id | AC | Level | Pri | Scenario | State today |
|---|---|---|:--:|---|---|
| `3.3-SLC-001` | 1, 4, 10 | INT (wire) | P0 | Owner deletes a populated Space with a key: `204`, no body; every Space-scoped table, Tokens and the Space row at 0; `AccessRefusal` rows unchanged; anchored on every count before | skipped `red: AC1, AC4, AC10` |
| `3.3-SLC-002` (x3) | 1, 9 | INT | P0 | Admin, Member, Viewer: `403 insufficient-role`, `delete-space`, one `InsufficientRole` row, Space intact; anchor `200` read | skipped `red: AC1, AC9` |
| `3.3-SLC-003` | 9 | INT | P0 | Non-member and absent: boundary `404`, byte-identical; `CrossSpace` row; foreign Space intact | skipped `red: AC9` |
| `3.3-SLC-004` | 4, 11 | INT | P0 | S-1: the statement rolled back leaves every count and the Token (anchor); then Owner's and Member's Tokens `401` on own and other routes; Owner's kept Token `200`; Owner's Session `404` / `200` | skipped `red: AC4, AC11` |
| `3.3-SLC-005` | 5 | INT | P0 | Member B loses only the deleted Space: switcher, `404`, own and third Space `200`, 2 Memberships, Account row | skipped `red: AC5` |
| `3.3-SLC-006` | 6 | INT | P0 | Last (Personal) Space deleted: `204`; switcher `200 []`; Session alive; Account row; create `201` | skipped `red: AC6` |
| `3.3-SLC-007` | 6 | INT | P2 | At 50: create `409` (anchor), delete one, create `201` | skipped `red: AC6` |
| `3.3-SLC-008` | 9, 12 | INT | P0 | Replay with key K: `204` then boundary `404` byte-identical, never `500`; no `IdempotencyRecord` row | skipped `red: AC9` |
| `3.3-SLC-009` | 9 | INT | P1 | Malformed key (256 chars): `400 invalid-idempotency-key`, Space intact; anchor `204` | skipped `red: AC9` |
| `3.3-SLC-010` | 2, 9 | INT | P0 | Counts read: exactly `{otherMembers, projects, tasks}` = 2, Statuses+1, 2; no name in the body | skipped `red: AC2, AC9` |
| `3.3-SLC-011` | 9 | INT | P0 | Counts read: Owner `200` (anchor), Admin `403 delete-space` + record, stranger `404` byte-identical + `CrossSpace` | skipped `red: AC9` |
| `3.3-SLC-012` | 9 | INT | P0 | I-17: no Space name in the Host log across counts read, Admin refusal, stranger refusal, deletion | skipped `red: AC9` |
| `3.3-RACE-S1` | 13 | INT (race) | P0 | Test holds the Space row, rename queues on its `UPDATE`, test deletes and commits: boundary `404`, not `200` (`:666`) | skipped `red: AC13`; **activated: blocked, then `200`** |
| `3.3-RACE-S2` | 13 | INT (race) | P0 | Member's first request queues on its presence insert's FK behind the held deletion: `404` + `CrossSpace`, never `500` | skipped `red: AC13`; **activated: blocked, then `500`** |
| `3.3-RACE-S3` | 13 | INT (race) | P0 | Project creation queues on the Projects lock behind the held deletion: `404`, never `500` | skipped `red: AC13`; **activated: blocked, then `500`** |
| `3.3-RACE-S4` | 13 | INT (race) | P0 | Task creation queues on its FK to the cascaded Project: `404`, never `500` | skipped `red: AC13`; **activated: blocked, then `500`** |
| `3.3-RACE-S5` | 10, 13 | INT (race) | P0 | Test is a creation holding the Projects lock; deletion must queue; test inserts and commits; deletion `204` takes the new Project; 547 reported as "did not wait" | skipped `red: AC10, AC13` |
| `3.3-RACE-S6` | 13 | INT (direct) | P0 | `ActiveSpaceNameReader` on a context resolved for a Space deleted afterwards answers no name; anchor: the name before (`:360`) | skipped `red: AC13`; **activated: throws** |
| `ISO-DELETE-SPACE-CROSS` (x2) | 9 | ISO | P0 | A deletes B's Space: boundary `404`, `CrossSpace`; B keeps its name | pending until 3.3 |
| `ISO-RECORD-KIND-SPACE-DELETE-ADMIN` (x2) | 1, 9 | ISO | P0 | Admin of its own Space: `403 delete-space`, `InsufficientRole`; the Space still listed | pending until 3.3 |
| `ISO-DELETE-SPACE-OWN` (x2) | 4, 6, 11 | ISO | P0 | Fresh Owner deletes its only Space on either surface: `204`, no body; its switcher empty; A's `[SA]`; B's name kept | pending until 3.3 |
| `ISO-DELETION-SUMMARY-CROSS` (x2) | 9 | ISO | P0 | Counts read of B's Space: boundary `404` | pending until 3.3 |
| `ISO-RECORD-KIND-DELETION-SUMMARY-ADMIN` (x2) | 9 | ISO | P0 | Counts read as Admin: `403 delete-space` | pending until 3.3 |
| `REV-SPACE-DELETED-BY-OWNER` (x2) | 11 | REV | P0 | Owner deletes through the Session; next Session request boundary `404` (Session alive), next Token request no-credential `401` | skipped `red: AC11` (row held apart) |
| `3.3-ARCH-001` | 1 | ARCH | P0 | Client `PermitsDeleteSpace` = matrix (Owner only) | skipped `red: AC1`; **activated: no predicate** |
| `3.3-ARCH-002` | 9 | ARCH | P0 | `DELETE` Active and `GET .../deletion` each declare exactly `DeleteSpace` | skipped `red: AC9`; **activated: no route** |
| `3.3-ARCH-003` | 9 | ARCH | P0 | Exactly one endpoint carries Q1's marker: `DELETE /api/v1/spaces/{spaceId:guid}` | skipped `red: AC9`; **activated: `[]`** |
| `3.3-ARCH-004` | 10 | ARCH (IL) | P0 | Bulk writes of `Space` are exactly `SpaceStore.ExecuteDeleteAsync` and `SpaceStore.ExecuteUpdateAsync` | skipped `red: AC10`; **activated: only the update (non-vacuous)** |
| `3.3-ARCH-005` | 8, 12, 14, 15 | ARCH (resx) | P1 | The Q5-Q8 sentences are `ClientCopy` values once each, comment naming 3.3 | skipped `red: AC8...`; **activated: "Delete this Space" 0 times** |
| `3.3-UNIT-001` | 12 | UNIT | P0 | `SpaceDirectory.Removed(id)` drops the row and the refused-set entry; `Current` not forgotten | skipped `red: AC12`; **activated: no `Removed(Guid)`** |
| `3.3-E2E-001` | 1 | E2E (fake) | P0 | Owner is offered one "Delete this Space"; nothing disabled | skipped `red: AC1` |
| `3.3-E2E-002` | 1 | E2E | P0 | Admin: no "Delete this Space" text, no confirm, nothing disabled, no counts read; anchor: "Rename" present | **green, standing** |
| `3.3-E2E-003` | 7 | E2E | P0 | Confirm replaces the panel: no Rename, no Labels heading, no second layer; Cancel restores and focuses the opener; nothing sent | skipped `red: AC7` |
| `3.3-E2E-004` | 16 | E2E | P0 | Focus on the field on open; Esc restores and focuses the opener | skipped `red: AC16` |
| `3.3-E2E-005` (x2) | 2, 8 | E2E | P0 | Heading first, "This cannot be undone." next, counts and other-Accounts sentence; counts re-read on each opening | skipped `red: AC2, AC8` |
| `3.3-E2E-006` | 3 | E2E | P0 | Field: labelled, `autocomplete="off"`, name not reusable, fresh per opening | skipped `red: AC3` |
| `3.3-E2E-007` | 3 | E2E | P0 | Mismatch sends nothing and says so; Delete never disabled; trimmed exact name sends | skipped `red: AC3` |
| `3.3-E2E-008` | 12 | E2E | P0 | Held: `aria-busy`, nothing disabled, field kept, still on settings, bar still names the Space | skipped `red: AC12` |
| `3.3-E2E-009` | 12 | E2E | P0 | `204`: Harbour's home, focus on its `h1`, "Space deleted." once, no name said, no trace in the DOM, switcher lists Harbour only, storage empty, Back does not return | skipped `red: AC12` |
| `3.3-E2E-010` | 6, 12 | E2E | P0 | Last Space: "No Spaces yet" focused, one "Create a Space", nothing created | skipped `red: AC6, AC12` |
| `3.3-E2E-011` | 12 | E2E | P0 | `404` (deleted elsewhere): goes home like `204`, says nothing | skipped `red: AC12` |
| `3.3-E2E-012` (x2) | 12 | E2E | P0 | No answer / `503`: confirm stays with "Not sent."; retry carries the same key | skipped `red: AC12` |
| `3.3-E2E-013` | 1, 9 | E2E | P1 | `403`: "Only the Owner can delete the Space." in `main`; stays | skipped `red: AC1, AC9` |
| `3.3-E2E-014` | 14 | E2E | P0 | Empty switcher: `NoSpaceBody` in the bar outside the list, no `<li>`, foot link kept | skipped `red: AC14` |
| `3.3-E2E-015` | 14 | E2E | P0 | Failed switcher, nothing held: "Your Spaces could not be listed.", never the no-Space sentence | skipped `red: AC14` |
| `3.3-E2E-016` | 15 | E2E | P0 | `/` refusal (switcher failed): "Not available.", no link to `/`, the switcher sentence | skipped `red: AC15` |
| `3.3-E2E-017` | 15 | E2E | P0 | `/` refusal (every Space refused, 3.2's flow): the same; "Back to Yello." still on each Space's refusal (anchor) | skipped `red: AC15` |
| `3.3-E2E-018` (x2) | 16 | E2E | P0 | Keyboard only at 1280x800 and 320x256: Enter opens, type, Tab to Delete and Cancel with a non-inset ring, no sideways scroll, chip visible, Shift+Tab Enter deletes | skipped `red: AC16` |

### Priorities

P0 for everything that guards isolation, revocation, irreversibility and the races. P1 for the malformed key, the copy gate and the `403` path. P2 for the 50-Space bound.

### Duplicate-coverage guard - deliberately not written

- **`ActiveSpaceEndpointTests.Deleting_the_Space_invalidates_its_Tokens`** and **`REV-SPACE-DELETED`** stay as the raw-SQL schema proofs. SLC-004 and the new revocation row are their endpoint-driven twins.
- **The no-Space state on `/`** is 3.1's `The_no_Space_state_offers_exactly_one_action_to_create_a_Space_and_creates_nothing`; E2E-010 adds only the arrival by deletion.
- **"archive" never appears** is `DeletionCopyGateTests.No_copy_uses_the_word_archive`, product-wide; not repeated.
- **"Delete {0}?" and "This cannot be undone." once each** is `DeletionCopyGateTests.The_deletion_copy_is_externalised_once_each_with_a_comment`; the confirm reuses them.
- **Both-keys gates** (`No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys`, `Exactly_one_creation_unit_of_work_sets_both_session_context_keys`, `Exactly_one_production_line_sets_a_session_context_key`) stay green untouched under Q1 (a).

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| A wire race for the name read (`:360`) | No lock to hold under RCSI (finding 2). RACE-S6 covers it directly | Completion Notes |
| A forced failure inside the product after the delete statement and before commit | No seam reaches between the endpoint and `CompleteAsync` without a plant; SLC-004's rolled-back statement is S-1's schema anchor, and Q1's plant (record the key) is the product-level forced failure: it must leave every row in place | Plant table |
| Live-session termination and `MembershipChanged` (V-11, AD-9) | Out of scope; stories 7.3 / 7.7 | Ledger |
| Invitation and Ownership Offer FK behaviour (C1, C2) | No tables yet; stories 4.1 / 5.1 | Ledger |
| The story's plant table | Plants need the product changes 3.3 has not made | Debug Log |

## Step 4 - Scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents; generation stayed in-thread, as every earlier run recorded, because C# under this analyser set has to be built and fixed here anyway. Build fixes on the way: `MA0186` (a custom record `Equals` - replaced by a `Flattened` string), `IDE0005`, `CS0133`, `CA1869` (a cached `JsonSerializerOptions`), `S3358` (a nested ternary in the fake). Worker A (API) output: the Slices, Isolation, Revocation and Architecture files. Worker B (E2E) output: the browser class and the fake.

| File | Entries (cases) | Green, standing | Skipped / pending (red) | ACs |
|---|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteSpace.cs` (new partial) | 12 (14) | 0 | 12 skipped | AC1, AC2, AC4-AC6, AC9-AC12 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.DeleteSpaceRace.cs` (new partial) | 6 (6) | 0 | 6 skipped | AC10, AC13 |
| `tests/Yello.Tests.Isolation/IsolationCases.cs` (5 rows added) | 5 (10 surface rows) | 0 | 5 pending until 3.3 | AC1, AC4, AC6, AC9, AC11 |
| `tests/Yello.Tests.Revocation/RevocationCases.cs` + `RevocationCaseTests.cs` (1 row held apart, 1 theory; the existing theory's body moved into `RunAsync` unchanged) | 1 (2) | 0 | 1 skipped | AC11 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.DeleteSpace.cs` (new partial) | 4 (4) | 0 | 4 skipped | AC1, AC9, AC10 |
| `tests/Yello.Tests.Architecture/SpaceDeletionClientGateTests.cs` (new class, no fixture) | 2 (2) | 0 | 2 skipped | AC8, AC12, AC14, AC15 |
| `tests/Yello.Tests.Client/SpaceDeleteTests.cs` (new, `ClientSurfaceCollection`) | 18 (21) | 1 (E2E-002) | 17 skipped | AC1-AC3, AC6-AC8, AC12, AC14-AC16 |
| **Total** | **48 (59)** | **1** | **42 skipped + 5 pending rows** | |

### Harness added (test code, active)

- **`FakeYelloApi` is now `partial`**, and `FakeYelloApi.SpaceDeletion.cs` adds: `HoldSpaceDeleteAsync` / `ReleaseSpaceDelete` / `ReleaseSpaceDeleteRefused` / `ReleaseSpaceDeleteUnanswered`, `NextSpaceDeleteRefusal`, `DeletionSummaries`, `DeletionSummaryPathOf`, `DeletionSummaryReads`, `SpaceDeletions`, and `SwitcherFails` (`503 service-unavailable` on `GET /api/v1/spaces`). `DecideAsync` consults it first and gets `null` for any other request, so every earlier case is served the bytes it always was. An applied deletion takes the Space out of `Switcher`, `Roles`, `Projects`, `Labels` and `Boards`; an unlisted Space is the boundary `404`, which is Q1 (a)'s replay.
- **`RevocationCaseTests.RunAsync`**: the existing theory's body, moved verbatim so the held-apart row runs through the same steps.
- **Isolation helpers**: `DeletionSummaryRoute` / `DeletionSummaryOf` (assumed path), `AdminDeletesTheSpaceAsync`, `AdminReadsTheDeletionSummaryAsync`, `TheActorsSpaceStillStandsAsync`, `FreshActorDeletesItsOwnSpaceAsync`, `DeletedOnlyTheCallersOwnSpaceAsync`.
- **Slices helpers**: `PopulateSpaceAsync` (a Space populated through the product), `SpaceRowCountsAsync`, `DeleteSpaceInSqlAsync` (the product's statement shape, committed or rolled back), `HoldTheDeletionAsync` (Q4 (a)'s shape, uncommitted), `RunAsync`, `DeletionSummaryOf`.

### Seed data and the constraints checked

- **Slices.** Accounts, Personal Spaces, the doomed Space (`POST /api/v1/spaces`: its Owner Membership and three Statuses by the provisioning), Projects, Tasks, Status moves, the Label and its applications, Tokens and presence rows all go through the product. The only SQL writes are: Memberships by `TaskWire.AddMemberAsync` under the Space's own key, for Accounts the product registered, never a second Owner (`FK_Membership_Space_SpaceId`, `FK_Membership_Account_AccountId`, `AK_Membership_SpaceId_AccountId`, `UX_Membership_SpaceId_Owner`, the `BLOCK AFTER INSERT` predicate; Roles by name, `"Member"`/`"Admin"`, as `BoardEndpointTests` writes them); `SeedNamedSpaceAsync` / `SeedForeignSpaceAsync` (existing B2 blocks: `Account` before `Membership`, `Space` before both); and RACE-S5's one `dbo.[Project]` insert with exactly the columns `ProjectConfiguration` requires (`Id` supplied - `ValueGeneratedNever`, `SpaceId`, `Name`, `CreatedAt`), under the Space's key, into a Space that exists when it is sent (`FK_Project_Space_SpaceId`). The deletions the tests play (`DELETE FROM dbo.[Space] WHERE Id = @space` under the Space's key) are race writers and S-1's rolled-back anchor, never cleanup.
- **Isolation.** Fresh actors through `ProductActor`; Roles changed by the existing `WireDatabase.ChangeRoleAsync`. A fresh actor per destructive row, so A's and B's Spaces survive any failure.
- **Browser.** Fake-side records shaped as the contracts (`SpaceIdentityResponse`, `ActiveSpaceResponse`, `ProblemResponse`), and the counts read as the camel-cased record Q2 names.

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Skipped scaffolds activated against the product (2026-10-31)

The Skip attributes were removed by a script that held each original file in memory and restored it in a `finally`; every restored file's SHA-256 matched its original, and the project was rebuilt from the restored files.

| Scaffold (activated) | Failed with |
|---|---|
| ARCH-001 | "No static bool (string roleCode) method named for DeleteSpace in Yello.Client." |
| ARCH-002 | `Assert.Single()` - no `DELETE` on the active Space's route |
| ARCH-003 | `Expected ["DELETE /api/v1/spaces/{spaceId:guid}"]`, actual `[]` |
| ARCH-004 | Actual `[SpaceStore.ExecuteUpdateAsync]` only - the scan reads the rename, so it is not vacuous |
| ARCH-005 | "\"Delete this Space\" is a ClientCopy value 0 times" |
| UNIT-001 | "SpaceDirectory has no public void Removed(Guid)." |
| RACE-S1 | Blocked on the held Space row, then "answered OK: {...\"name\":\"Renamed into nothing ...\"}" |
| RACE-S2 | Blocked, then "answered InternalServerError" |
| RACE-S3 | Blocked, then "answered InternalServerError" |
| RACE-S4 | Blocked, then "answered InternalServerError" |
| RACE-S6 | `InvalidOperationException: Sequence contains no elements.` |
| SLC-001..012, RACE-S5, the revocation theory, E2E (17) | Not activated: each needs the deletion route, the counts read or the client section, none of which exists; the red reason is the route's absence (`405` / no section). Confirm on activation |

E2E-002 ran green in the browser (`SpaceDeleteTests`: 18 total, 1 passed, 17 skipped, 4 s).

### Full run

See the story's ATDD section and the orchestrator report for the totals of the run made after this checklist.

### Red-phase compliance

- Every red scaffold is skipped with `red: AC<n> - <why> (story 3.3 Task n)`; isolation rows are `PendingUntil: "3.3"` with a `red: AC<n>` reason, allowed by GATE-003 while 3.3 is `ready-for-dev` or `in-progress`.
- **One case is green today and stays active as a standing gate**: E2E-002 (an Admin has no delete affordance). Its anchor is the Admin's "Rename"; its plant (the section rendered for an Admin) is the dev phase's.
- Every scaffold compiles under warnings-as-errors. No scaffold references a type 3.3 has not written: `PermitsDeleteSpace`, Q1's marker, `Removed(Guid)`, the deletion route and the counts read are found by name, metadata, reflection or path.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

1. **SLC-001 `The_Owner_deletes_a_populated_Space_and_every_row_hanging_from_it_goes_with_it` first, because it tests the riskiest assumptions together:** that one `DELETE` of the `Space` succeeds over the `NO ACTION` foreign keys (`Task → StatusDefinition`, `TaskLabel → Label`) with real data, and that Q1 (a) keeps the pipeline from recording the key against a Space it just deleted. If the statement fails, a migration is needed (expand-only, exact `Down`, reason recorded) and this is the moment to find out. Then **SLC-008** (the replay is a `404`, never `500`) and **ARCH-003** (the marker, on that route only). Plant "record the key on this route": SLC-001 and SLC-008 turn red with `500`.
2. **Q4 (a), the races:** **RACE-S2, S3, S4** (the 547 mapping), **RACE-S1** (`RenameAsync` row count, `:666`), **RACE-S6** (`SingleOrDefaultAsync`, `:360`), then **RACE-S5** (the Projects lock in `SpaceStore.DeleteAsync`; if the lock is not taken, record why and keep S5 red). Run each 3/3. Plants: remove the 547 mapping, ignore the row count, restore `SingleAsync`, drop the lock.
3. **The wire contract:** **ARCH-004** (amend `The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space`'s message to cite the 2026-10-31 amendment), **ARCH-002**, **SLC-002**, **SLC-003**, **SLC-009**, **SLC-012**.
4. **Tokens and other Accounts:** **SLC-004**, **SLC-005**, **SLC-006**, **SLC-007**; then the revocation row (move `SpaceDeletedByOwner` into `All`, delete the skipped theory) and the five isolation rows (clear `PendingUntil`; retarget `DeletionSummaryRoute`/`DeletionSummaryOf` to the contract).
5. **Q2's counts read:** **SLC-010**, **SLC-011**; retarget every `/deletion` literal (Slices, Isolation, Architecture, the fake) to the `SpaceRoutes` constant, and record the path if it differs.
6. **The client gates:** **ARCH-001** (`PermitsDeleteSpace`; widen the union gate's expression - finding 4), **UNIT-001** (`Removed`), **ARCH-005** (copy).
7. **The browser cases**, Debug-building `Yello.Client` before each: E2E-001, 003, 004, 005, 006, 007, 008, then 009-013 (the outcomes), then 014-017 (AC14, AC15), then 018 (keyboard). Keep **E2E-002** green throughout and run its plant once the section exists. Record finding 5 (focus) either way.

### Commands

```
dotnet build Yello.slnx
dotnet build-server shutdown
dotnet test Yello.slnx --no-build --max-parallel-test-modules 1
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.Spaces.ResolveActiveSpace.ActiveSpaceEndpointTests
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.SpaceDeletionClientGateTests
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.SpaceDeleteTests
```

Never run two `dotnet test` processes at once. `--no-build` runs whatever was last built.

## Notes and risks

- **Assumed paths and names to retarget:** the counts read `/api/v1/spaces/{spaceId}/deletion` (Slices `DeletionSummaryOf`, Isolation `DeletionSummaryRoute`/`DeletionSummaryOf`, Architecture `DeletionSummaryPattern`, the fake's `DeletionSummaryPathOf`); Q1's marker matched by `Destroys` in its type name.
- **Copy held by value** in the browser cases and ARCH-005: correct to the shipped copy and record it if Q5-Q8 land differently.
- **E2E-009 asserts "Space deleted." is said exactly once.** If a status region nests inside a polite one, the recorder sees the sentence twice; correct the recorder, not the product, and record it.
- **E2E-005's singular forms** ("1 Project and 1 Task") follow 2.5's `ProjectHoldsTasks`; the zero forms are left to the dev phase.
- **The deletion sends a key on every Session row except the revocation event and the isolation rows** (`ProductActor` sends none on `DELETE`): Q1's key path is exercised by SLC-001/008/009 and the browser.

---

**Generated by BMad TEA Agent** - 2026-10-31
