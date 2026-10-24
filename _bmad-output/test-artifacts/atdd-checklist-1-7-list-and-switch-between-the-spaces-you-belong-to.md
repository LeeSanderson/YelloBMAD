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
lastSaved: '2026-10-24'
workflowType: 'testarch-atdd'
storyId: '1.7'
storyKey: '1-7-list-and-switch-between-the-spaces-you-belong-to'
storyFile: '_bmad-output/implementation-artifacts/1-7-list-and-switch-between-the-spaces-you-belong-to.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-1-7-list-and-switch-between-the-spaces-you-belong-to.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.Switcher.cs'
  - 'tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/SessionEndpointTests.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceIsolationLayerTests.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/PooledConnectionIsolationTests.cs'
  - 'tests/Yello.Tests.Slices/WireSession.cs'
  - 'tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Client/SpaceSwitchingTests.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/1-7-list-and-switch-between-the-spaces-you-belong-to.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-1-6-refuse-at-the-space-boundary-and-record-every-refusal.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 1, Story 1.7: List and switch between the Spaces you belong to

**Date:** 2026-10-24
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT (container-backed, over the wire and at the two isolation layers), ARCH for the structural gates, and the first browser cases that drive a signed-in flow (Playwright for .NET, API faked at the network edge)

---

## Story summary

Stories 1.5 and 1.6 built a Space-scoped pipeline that sees one Space at a time, and a client that has nowhere to go once signed in. Story 1.7 adds the one sanctioned read across Spaces - the switcher, under a second session-context key (`AccountId`) that widens exactly `Membership` in both RLS and the EF filter - and the one endpoint that reports the active Space's own resolution (name and Role). On the client: the context bar, the Role chip, the switcher, a Space home, sign-out, and a per-Space state whose purge runs before any render.

**As** someone who will hold Memberships in several Spaces, **I want** a switcher showing exactly the Spaces I belong to, which changes my whole working surface when I pick one, **so that** I always know which context I am in and can never see one I have no standing in.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - the switcher lists exactly the Account's Spaces; nothing else discovers one.
2. **AC2** - a row is a name: no count, Role or badge (identity only).
3. **AC3** - the switcher and Account settings run under `AccountScopedContext`, RLS on `SESSION_CONTEXT('AccountId')`; nothing crosses a Space boundary through them.
4. **AC4** - the acting Role is the destination's, no carry-over; focus moves to the context bar and the new Space and Role are announced.
5. **AC5** - the context bar names the Space and shows the Role verbatim in a display-only chip; never scrolls away or hides.
6. **AC6** - the accessible name states the Role and what it permits; copy in sentence case, uppercase by `text-transform`.
7. **AC7** - switch, sign-out and Back purge the departed Space synchronously before any render; nothing Space-scoped in web storage.
8. **AC8** - `document.title` fixed `Yello`; Space-name inputs `autocomplete="off"`; scroll restoration disabled on Space routes.
9. **AC9** - the refusal page's route back is drawn from the switcher; both 404 causes render identically in a browser.
10. **AC10** - no Space-scoped request for a Space the switcher did not list.
11. **AC11** - sign-in lands on a switcher Space (or a no-Space state, never auto-creating one); a sign-out control ends the Session and purges through the session-end seam.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` -> **`backend`**, for the reason stories 1.5 and 1.6 recorded: `Yello.slnx` and `.csproj` files, no `package.json`, no Playwright/Cypress config. The browser suite is Playwright *for .NET* and fires none of the frontend indicators. Its cases are written anyway, because the story's Task 10 and the test design (X-8, the AC9/AC10 browser half) require them and `TESTING-CONVENTIONS.md` binds the E2E level to `Yello.Tests.Client`.

**Prerequisites - met:**

| Requirement | State |
|---|---|
| Story approved, clear AC | `ready-for-dev`; AC1-AC8 verbatim from `epics.md:800-836` (`status: final`), AC9-AC11 carried from the ledger and UX spine |
| Test framework | six test projects, MTP runner via `global.json`; `Yello.Tests.Client` with Playwright and Chromium installed |
| Development environment | .NET SDK 10.0.303; container runtime up (Rancher) |

**No progress file existed for this story** (only the 1.5 and 1.6 checklists), so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `test_stack_type: auto`, `tea_execution_mode: auto`. No `project-context.md` exists, so the persistent-facts glob loaded nothing.

**Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. **Not loaded, with reasons:** `component-tdd` (no component level in this repository), `test-healing-patterns` (nothing to heal), `ci-burn-in` (story 1.10's), Playwright Utils / Pact fragments (disabled; TypeScript-shaped). Where a fragment disagrees with `tests/TESTING-CONVENTIONS.md`, the repository document wins - it is enforced by gates.

## Step 2 - Generation mode

**AI generation** - unconditional for a `backend` stack, and recording would need a signed-in client that does not exist yet. Sources: the story's AC, Tasks and open questions; `EXPERIENCE.md`/`DESIGN.md` for the bar, chip and switcher; the live schema (three migrations); the existing harnesses (`HostProcess`, `WireSession`, `SpaceSeeding`, `RegistrationDatabase`, `ClientSurfaceFixture`).

## Step 3 - Test strategy

### The constraint that shapes every scaffold

A scaffold must **compile** (warnings are errors solution-wide) and must not need product code. So, as in 1.6: types story 1.7 has not written are reached **by name** (`SpaceIdentityResponse`, `SpaceIdentity`, `AccountScopedSurface`, `AccountScope`, the three-argument `YelloDbContext` constructor); routes it has not written are **string literals** (`/api/v1/spaces`, `/api/v1/spaces/{id}`); copy keys are **resource names** read through a `ResourceManager`; client markup is **CSS selectors**; the session-context key is set by a **raw `sp_set_session_context`** in test code. Each such stand-in fails at run time with a message naming the task that makes it real. **On activation, replace each with the typed reference or route constant it stands for.**

Names are the story's where it gives one and this run's where it does not (`SpaceIdentityResponse`, the four `RolePermits{Role}` keys, the `.role-chip` class). They are the scaffolds' contract, not a design decision: a different name chosen in dev-story is corrected in the scaffold and recorded in the Debug Log.

### Container budget

A full run starts **thirteen** engines today. Every container-backed case goes into an **existing** class: wire cases into `ActiveSpaceEndpointTests` (as a second partial file) and `SessionEndpointTests`, layer cases into `SpaceIsolationLayerTests`, the pooled case into `PooledConnectionIsolationTests`. **No new engine, now or on activation.** The browser cases use `FakeYelloApi` (Q5 option a) - no Host, no database.

### Scenario map

| id | AC | Level | Pri | Scenario | File |
|---|---|---|:--:|---|---|
| `1.7-INT-001` | 1, 2 | INT (wire) | P0 | The switcher lists exactly the Account's Spaces (Personal + seeded Owner + seeded Viewer), not another Account's; each row's raw JSON property set is exactly `{id, name}` (I-11) | `ActiveSpaceEndpointTests.Switcher.cs` |
| `1.7-INT-002` | 1 | INT (wire) | P1 | An Account with no Membership gets `200 []`, not a 404, and nothing is recorded (Q6) | `ActiveSpaceEndpointTests.Switcher.cs` |
| `1.7-INT-003` | 1 | INT (wire) | P1 | Ordered by name, then id (SQL Server `uniqueidentifier` order) | `ActiveSpaceEndpointTests.Switcher.cs` |
| `1.7-INT-004` | 3 | INT (wire) | P0 | No Session -> `401 authentication-failed`, no `AccessRefusal` (anchored on a recorded 404) | `ActiveSpaceEndpointTests.Switcher.cs` |
| `1.7-INT-005` | 4 | INT (wire) | P0 | `GET /api/v1/spaces/{id}` answers `{id, name, role}`: owner -> viewer -> owner -> viewer on one Session (I-10, request path) | `ActiveSpaceEndpointTests.Switcher.cs` |
| `1.7-INT-006` | 4 | INT (wire) | P0 | A Role changed between two requests reaches the next answer (AD-1) | `ActiveSpaceEndpointTests.Switcher.cs` |
| `1.7-INT-007` | 4 | INT (wire) | P0 | That endpoint's non-member and missing-Space 404s are identical and each records one `CrossSpace` row | `ActiveSpaceEndpointTests.Switcher.cs` |
| `1.7-INT-008` | 11 | INT (wire) | P0 | Sign-out: `204`, the cookie expired with its attributes, the next request `401`, and a replayed copy of the pre-sign-out cookie `401` (corrected 2026-10-24, Lee's decision (a)) | `SessionEndpointTests.cs` |
| `1.7-INT-011` | 11 | INT (wire) | P0 | A Session issued after sign-out is served, while the pre-sign-out cookie replayed beside it is refused (added 2026-10-24) | `SessionEndpointTests.cs` |
| `1.7-INT-009` | 11 | INT (wire) | P0 | Sign-out without an anti-forgery token: `400`, Session survives; with it: `204` | `SessionEndpointTests.cs` |
| `1.7-INT-010` | 11 | INT (wire) | P1 | Sign-out with no Session: `204` (idempotent, Q4) | `SessionEndpointTests.cs` |
| `1.7-LAYER-001` | 3 | INT (db) | P0 | Under the `AccountId` key, RLS alone admits exactly that Account's Memberships - not another Account's in a shared Space | `SpaceIsolationLayerTests.cs` |
| `1.7-LAYER-002` | 3 | INT (db) | P0 | Under the `AccountId` key, no `StatusDefinition` or `SpacePresence` row is visible (anchored on the Memberships being visible) | `SpaceIsolationLayerTests.cs` |
| `1.7-LAYER-003` | 3 | INT (db) | P0 | The EF filter alone (`AccountScope`) admits only the scoped Account's Memberships while RLS admits both Accounts' | `SpaceIsolationLayerTests.cs` |
| `1.7-LAYER-004` | 3 | INT (db) | P0 | A Space-scoped unit of work sees only its own Membership of a multi-Space Account, through either layer ("one key per unit of work") | `SpaceIsolationLayerTests.cs` |
| `1.7-LAYER-005` | 3 | INT (db) | P0 | On one pinned physical connection, an Account-scoped read and a Space-scoped read disclose nothing to each other, in both orders | `PooledConnectionIsolationTests.cs` |
| `1.7-ARCH-001` | 2 | ARCH | P0 | `SpaceIdentityResponse` and `SpaceIdentity` have exactly `Id: Guid`, `Name: string` (I-11 static) | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-002` | 3 | ARCH | P0 | `AccountScopedSurface` is a closed enum of exactly `SpaceSwitcher`, `AccountSettings` | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-003` | 8 | ARCH | P0 | `index.html` title is exactly `Yello`; no `PageTitle`, `HeadContent` or `document.title` anywhere in the client | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-004` | 8 | ARCH | P0 | The detector behind ARCH-003 fires on planted signals | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-005` | 8 | ARCH | P0 | `index.html` sets `history.scrollRestoration = 'manual'` before the runtime script (Q10) | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-006` | 5 | ARCH | P0 | `RoleChip.razor` is a `span` with no handler, link, button, tabindex or role | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-007` | 5, 6 | ARCH | P0 | `RoleOwner`/`RoleAdmin`/`RoleMember`/`RoleViewer` are the Role names verbatim, sentence case | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-008` | 6 | ARCH | P0 | Admin and Viewer phrases verbatim; every phrase true of `RoleCapabilityMatrix`; `ContextBarAccessibleName` composes AC6's example exactly (Q3) | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-009` | 9 | ARCH | P0 | No client identifier remembers a last/previous/recent Space | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-010` | 9 | ARCH | P0 | The detector behind ARCH-009 fires on planted signals and not on prose | `SpaceSwitcherGateTests.cs` |
| `1.7-ARCH-011` | 3 | ARCH | P0 | The switcher endpoint reaches no `StatusDefinition`/`Task`/`Project`/`Label`/`SpacePresence`/`AccessRefusal` (I-11 static, IL reachability, vacuity guard) | `RefusalBoundaryGateTests.cs` |
| `1.7-E2E-000` | Q5 | E2E | P1 | The `RouteAsync` fake answers the client's cross-origin credentialed requests: the fake's token comes back on the sign-in POST's header | `SpaceSwitchingTests.cs` |
| `1.7-E2E-001` | 11, 5 | E2E | P0 | Sign-in lands on the first switcher Space; bar names it; chip says `Owner` (en-GB, tr-TR) | `SpaceSwitchingTests.cs` |
| `1.7-E2E-002` | 1, 2 | E2E | P0 | The switcher lists exactly the served Spaces, names only, `aria-current` on the active row | `SpaceSwitchingTests.cs` |
| `1.7-E2E-003` | 4 | E2E | P0 | Choosing a row: final focus owner is the bar; polite region announces destination name, Role and permit, not the departed Space | `SpaceSwitchingTests.cs` |
| `1.7-E2E-004` | 7 | E2E | P0 | Back with the destination's answer held: nothing of the departed Space and no chip in the DOM until released | `SpaceSwitchingTests.cs` |
| `1.7-E2E-005` | 10 | E2E | P0 | An unlisted id renders the refusal page and is never requested (anchored on the switcher being requested) | `SpaceSwitchingTests.cs` |
| `1.7-E2E-006` | 9 | E2E | P0 | A listed-but-refused Space (server 404) and an unlisted one render the same text and focus | `SpaceSwitchingTests.cs` |
| `1.7-E2E-007` | 9 | E2E | P0 | After visiting the second Space, the refusal page's route back lands on the first listed - not the remembered one | `SpaceSwitchingTests.cs` |
| `1.7-E2E-008` | 5 | E2E | P1 | X-8: the chip is a `SPAN`, `tabIndex -1`, `border-top-width` not `0px`, `text-transform` uppercase in en-GB and none in tr-TR | `SpaceSwitchingTests.cs` |
| `1.7-E2E-009` | 8 | E2E | P0 | On a Space route `document.title` is `Yello` and `history.scrollRestoration` is `manual` | `SpaceSwitchingTests.cs` |
| `1.7-E2E-010` | 7 | E2E | P0 | After two Spaces, `localStorage`, `sessionStorage` and IndexedDB are empty | `SpaceSwitchingTests.cs` |
| `1.7-E2E-011` | 11 | E2E | P0 | Sign out sends the DELETE with the token and lands on `/sign-in` with no `reason`; no bar | `SpaceSwitchingTests.cs` |
| `1.7-E2E-012` | 11 | E2E | P1 | No Membership: `/` shows a no-Space state, never sign-in, and no Space is created | `SpaceSwitchingTests.cs` |

### Duplicate-coverage guard - what is deliberately not written

- **"Neither key set -> nothing visible"** is `RegisterAccountIntegrationTests.Space_scoped_rows_are_invisible_without_a_session_context`. It still guards the widened predicate (an unset `AccountId` compares `NULL`); not repeated.
- **The `not-available` body and the `CrossSpace` record** are 1.6's `ActiveSpaceEndpointTests` cases on the statuses route. `1.7-INT-007` asserts only that the *new* route goes through the same pipeline.
- **`ClientStorageBanTests`** bans storage in the IL; `1.7-E2E-010` is the runtime half, not a repeat.
- **`RefusalSurfaceTests`** proves the unknown-route and `/not-available` pages focus their `h1`; `1.7-E2E-006` adds the server-404 cause beside them.

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| Widening `PersistenceModelGateTests.Every_Space_scoped_entity_has_a_query_filter_from_application_state` to accept exactly one disjunct on exactly `Membership`, with its two plants (a third disjunct, `true \|\|`) | It edits an existing gate's predicate, which is the design decision itself (Task 2); a parallel gate would let the two disagree | Task 2 / Task 8 |
| Extending `SpaceIsolationSchemaTests.Every_predicate_filters_on_the_SpaceId_session_context` (Membership's predicate also has the `AccountId` clause) | Same: an existing gate, extended not duplicated | Task 2 |
| Renaming/keeping `SpaceResolutionSeamTests.Exactly_one_production_call_site_sets_the_Space_session_context`, and the call-site gates "only the Account-scoped adapter sets `AccountId`" / "nothing sets both" | Their shape depends on how Task 2 generalises `SpaceSessionContext` (enum vs two entry points) | Task 2 / Task 8 |
| A-12's permitted list naming the active-Space endpoint | An existing gate's allow-list; the endpoint's type name is dev-story's | Task 4 |
| "`Space` is read only where named" | The three permitted readers' type names are dev-story's | Task 8 |
| `ActiveSpaceState` purge semantics as a unit (every registration runs, cancel first, survives a throw, runs on Space change and leaving Space routes, not on same-Space navigation), the accessible name for all four Roles, the switcher row renders the name only | The type's shape is Task 6's design; reflection over a purge registry would be fiction. The browser cases E2E-003/004/010 hold the behaviour | Task 10, in `Yello.Tests.Architecture` (`SessionSeamTests` precedent) |
| `SessionState` sign-out shares the purge and differs only in destination | Same - a seam whose API Task 5 designs | Task 5 / Task 10 |
| A Token request to `GET /api/v1/spaces` is refused (CAP-35, I-11's API half) | Tokens are story 1.8's (Q9) | Ledger to 1.8 |
| Replaying a copy of the pre-sign-out cookie is refused | *Superseded 2026-10-24.* This run read the Session as a stateless cookie ticket and left the replay unrefused. The dev phase raised that as a contradiction with FR-2 / CAP-2 / story 1.4 AC3 ("an invalidated Session grants access to nothing"), and **Lee chose option (a)**: `Account.SessionsValidAfter`, stamped at sign-out and compared on every request. The replay **is refused** (`401`) - asserted in `1.7-INT-008`, with `1.7-INT-011` (a Session issued after sign-out is served) added beside it | Written in dev-story |
| `autocomplete="off"` + non-reusable name on Space-name inputs | No Space-name input exists until story 3.1; a gate over zero inputs passes vacuously (the story says not to write one) | Ledger to 3.1 |
| The real-Host browser sign-in (cookie across origins, real preflight) | Q5 option a; `FakeYelloApi`'s remarks say what it does not prove | Ledger to 1.9 |

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents, but generation was kept in-thread for the reason 1.5 and 1.6 recorded: C# under this analyser set has to be built and fixed here anyway. The first builds failed on `IDE1006`, `S6605`, `S3358`, `MA0181`, `MA0190` (x5), `S4261` and `S4040` (x2); all fixed. Worker A's output is the INT/ARCH files; Worker B's is the browser class.

### `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.Switcher.cs` - 7 cases, second partial file of the existing class (no new engine)

INT-001 `The_switcher_lists_exactly_the_Spaces_the_Account_holds_a_Membership_in` (AC1/AC2), INT-002 `The_switcher_answers_an_Account_holding_no_Membership_with_an_empty_list` (AC1), INT-003 `The_switcher_orders_Spaces_by_name_then_id` (AC1), INT-004 `The_switcher_refuses_a_request_without_a_Session_with_401_and_records_nothing` (AC3), INT-005 `Each_Space_reports_the_Role_held_there_with_no_carry_over_across_switches` (AC4), INT-006 `A_Role_changed_between_two_requests_reaches_the_next_answer_about_the_Space` (AC4), INT-007 `The_active_Space_answer_refuses_a_non_member_and_a_missing_Space_alike_and_records_each` (AC4). **All skipped - red.**

### `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/SessionEndpointTests.cs` - 3 cases added

INT-008 `Sign_out_ends_the_Session_and_expires_its_cookie`, INT-009 `Sign_out_without_an_antiforgery_token_is_refused_and_the_Session_survives`, INT-010 `Sign_out_without_a_Session_answers_204` (all AC11). **All skipped - red.** `WireSession` gained `DeleteAsync` (test helper).

### `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceIsolationLayerTests.cs` - 4 cases added

| Test | id | Status |
|---|---|---|
| `Under_the_AccountId_key_row_level_security_alone_admits_exactly_that_Accounts_Memberships` | LAYER-001 | skipped - red: AC3 |
| `Under_the_AccountId_key_no_other_Space_scoped_table_is_visible` | LAYER-002 | skipped - red: AC3 |
| `The_query_filter_alone_admits_only_the_scoped_Accounts_Memberships` | LAYER-003 | skipped - red: AC3 |
| `A_Space_scoped_unit_of_work_sees_only_its_own_Membership_of_an_Account_in_several_Spaces` | LAYER-004 | **ACTIVE - green today, standing gate; not plantable until the widening exists (below)** |

### `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/PooledConnectionIsolationTests.cs` - 1 case added

LAYER-005 `A_reused_connection_carries_no_Account_key_into_a_Space_scoped_read_or_the_reverse` (AC3). **Skipped - red.**

### `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.cs` - 10 cases, new class, no container

| Test | id | Status |
|---|---|---|
| `The_switcher_response_and_the_Space_identity_carry_exactly_an_id_and_a_name` | ARCH-001 | skipped - red: AC2 |
| `The_Account_scoped_surfaces_are_exactly_the_two_AD_24_names` | ARCH-002 | skipped - red: AC3 |
| `No_client_code_sets_the_document_title` | ARCH-003 | **ACTIVE - green today, standing gate, planted** |
| `The_document_title_scan_detects_a_planted_title` | ARCH-004 | **ACTIVE - permanent detector check** |
| `The_host_page_disables_scroll_restoration_before_the_runtime_loads` | ARCH-005 | skipped - red: AC8 |
| `The_Role_chip_is_never_interactive` | ARCH-006 | skipped - red: AC5 |
| `Every_Role_has_its_name_in_sentence_case_in_the_client_copy` | ARCH-007 | skipped - red: AC5/AC6 |
| `Every_permit_phrase_agrees_with_the_capability_matrix` | ARCH-008 | skipped - red: AC6 |
| `No_client_code_remembers_a_last_Space` | ARCH-009 | **ACTIVE - green today, standing gate, planted** |
| `The_last_Space_scan_detects_a_planted_field` | ARCH-010 | **ACTIVE - permanent detector check** |

### `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs` - 1 case added

ARCH-011 `The_switcher_endpoint_reaches_no_Space_scoped_content` (AC3, I-11 static). **Skipped - red.** The A-4 walk was split into `HandlerReaches(endpoint, graph, entities)` so this case can walk a wider list; A-4 itself is unchanged in behaviour and still passes.

### `tests/Yello.Tests.Client/SpaceSwitchingTests.cs` + `FakeYelloApi.cs` - 13 reported cases (two theories), new class in the existing collection, no container

E2E-000 `A_faked_API_answers_the_client_across_origins_with_credentials` is **ACTIVE, green today, and planted**. The other twelve (E2E-001 to E2E-012) are **skipped - red**, each naming its AC and task. `FakeYelloApi` answers `/api/v1/**` on any origin with CORS headers, records every request, can hold one Space's answer on a `TaskCompletionSource` (no `Task.Delay`), and serialises `AntiforgeryTokenResponse` and `ProblemResponse` from `Yello.Contracts`. The switcher row and the active-Space answer are anonymous objects until Tasks 3/4 write their contract types - **serialise those on activation**.

### Fixtures and seed data - every constraint checked

No new fixture, no new container class. Seeding reuses `SpaceSeeding` (B2) and `RegistrationDatabase`. Checked against `20260828164716_InitialSchema.cs`, `20260925132825_ActiveSpaceResolution.cs` and `20261024121107_SpaceBoundaryRefusals.cs`:

- **`FK_Membership_Account_AccountId`** - every seeded Membership's Account is the product-registered one, or is added earlier in the same block (`context.Accounts.Add/AddRange` before the Membership).
- **`FK_Membership_Space_SpaceId`** - each `Space` is added in the block that first writes a Membership (or Status) into it.
- **`UX_Membership_SpaceId_Owner`** (filtered unique, one Owner per Space) - at most one `Role.Owner` per seeded Space; the shared Space in the layer cases has two Members and no Owner.
- **`IX_Membership_SpaceId_AccountId`** (unique) - one Membership per Account per Space throughout.
- **`IX_StatusDefinition_SpaceId_Name`** (unique) - at most one Status per seeded Space.
- **`Account.NormalizedEmailAddress`** (unique) - random addresses (`SpaceSeeding.NewAccount`, `RandomAddress`); display names unique per test (`Ravi-<guid>`), which is also how the Personal Space is found by name in the policy-free `Space` table.
- **RLS BLOCK AFTER INSERT** on `Membership`, `StatusDefinition`, `SpacePresence` - every block runs under its own Space's session context; the `SpacePresence` rows in LAYER-002 are inserted on `OpenForSpaceAsync(thatSpace)`. `SpacePresence` is keyed `(SpaceId, AccountId)` with `ExpiresAt`, and every presence's Account holds a Membership in that Space.
- **INT-002's Membership removal** runs under the Personal Space's own context, the only one whose FILTER predicate shows the row; nothing references a `Membership` row by FK.
- **No `Space.Name` constraint** beyond required: `NamedSpace` supplies `Id`, `Name`, `CreatedAt` (all `required`).

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Test run

```
dotnet test Yello.slnx
Test run summary: Passed!   total: 369   failed: 0   succeeded: 335   skipped: 34
```

Baseline before this phase: **330 / 329 passed / 1 skipped**. Delta **+39 reported cases**: +6 active and passing, +33 skipped. No previously passing test changed, and no new SQL Server engine started (every container-backed case went into an existing class). The browser suite passed in the combined run.

### The standing gates were planted - 2026-10-24

Plants were written into the file under test, the case run, and the original restored in a `finally` from content held in memory, with a SHA-256 comparison afterwards (all restored; `git status` showed no production file modified).

| Plant | Gate | Caught |
|---|---|---|
| `<PageTitle>Northwind Redesign</PageTitle>` added to `Yello.Client/Pages/NotAvailable.razor` | ARCH-003 | **yes** - "Set by: Yello.Client\Pages\NotAvailable.razor" |
| `private static Guid? _lastSpaceId;` added to `Yello.Client/Pages/ClientRoutes.cs` | ARCH-009 | **yes** - "ClientRoutes.cs: '_lastSpaceId'" |
| `FakeYelloApi` stops sending `Access-Control-Allow-Origin` | E2E-000 | **yes** - timed out waiting for the sign-in POST. This also establishes that the client really is cross-origin to the fake (Development `HostBaseAddress`) and that Chromium enforces CORS on fulfilled responses - so the harness is exercising credentialed CORS, not bypassing it |
| (none possible today) | LAYER-004 | **Not plantable before the widening.** Both layers are keyed on `SpaceId` alone, so a filter planted to admit the Account everywhere is masked by RLS, and RLS cannot be planted without a migration. Dev-story plants it with the story's own "a Space-scoped unit of work also sets `AccountId`" and an `AccountScope` narrowed inside a Space-scoped unit of work |

### Red-phase compliance

- 33 of the 39 new reported cases are skipped with `red: AC<n> - <why>` naming the story task that makes them pass (a skipped theory reports as one case).
- 6 are active and green: 4 architecture (2 gates + 2 detector checks), 1 layer case, 1 browser harness case. Each gate is planted except LAYER-004, whose reason is recorded above. A-4 is behaviourally unchanged by the helper split and still passes.
- No scaffold references a type story 1.7 has not written; every one compiles.
- Every refusal or absence assertion is anchored on a success in the same test; every isolation assertion uses two Accounts or two Spaces with different ids.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

1. **LAYER-001 `Under_the_AccountId_key_row_level_security_alone_admits_exactly_that_Accounts_Memberships`** - first. It tests the riskiest assumption: that one `ALTER SECURITY POLICY ... ALTER FILTER PREDICATE` swapping in a `SpaceId OR AccountId` function on `Membership` alone (Q1a) admits exactly the Account's rows and nothing by Space - verified against SQL Server 2025, not memory. Everything server-side builds on it. Then **LAYER-002** (nothing else widened) and **LAYER-005** (the pool reset clears `AccountId`), then **LAYER-003** (the filter, independently).
2. **Plant LAYER-004** as soon as the widening exists (both halves, see above).
3. **ARCH-001, ARCH-002, ARCH-011** with the port, the DTO and the Account-scoped marker (Tasks 2-3); then **INT-001 to INT-004** over the wire. Replace the `/api/v1/spaces` literal with the `SpaceRoutes` constant.
4. **INT-005 to INT-007** - the active-Space endpoint (Task 4); add it to A-12's permitted list.
5. **INT-008 to INT-010** - sign-out (Task 5). *Corrected 2026-10-24:* a replayed pre-sign-out cookie is **refused** (Lee's decision (a), `Account.SessionsValidAfter`); INT-008 asserts it, and INT-011 asserts a Session issued after sign-out is served.
6. **E2E-000 stays green throughout**; then the client in this order: **E2E-005** (no request for an unlisted id) and **E2E-004** (purge before render on Back - the client's riskiest assumption: that `RegisterLocationChangingHandler` runs before the router renders for browser Back), then E2E-001, 002, 003, 006, 007, 010, 011, 012. Serialise the switcher and active-Space fakes from the new contract types.
7. **ARCH-005, ARCH-006, ARCH-007, ARCH-008, E2E-008, E2E-009** - copy, chip and browser-owned surfaces (Task 7).

### Commands

```
dotnet build Yello.slnx
dotnet test Yello.slnx
dotnet test tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class "Yello.Tests.Architecture.SpaceSwitcherGateTests"
dotnet test tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class "Yello.Tests.Slices.Spaces.ResolveActiveSpace.SpaceIsolationLayerTests"
dotnet test tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class "Yello.Tests.Slices.Spaces.ResolveActiveSpace.ActiveSpaceEndpointTests"
dotnet test tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class "Yello.Tests.Client.SpaceSwitchingTests"
```

The browser suite serves the build output with `--no-build`, so `dotnet build Yello.slnx` must run first. If it fails only in the combined run, re-run it alone before treating the failure as real (it starves under thirteen engines).

## Notes and risks

- **By-name lookups are scaffolding.** Replace each with a typed reference on activation so a rename becomes a compile error.
- **INT-003's tie-break** assumes the database does the ordering (`SqlGuid` order). If dev-story sorts in memory, correct the expectation and record it.
- **INT-001 finds the Personal Space by name**, through `CHARINDEX(displayName, Space.Name)`; it relies on the Personal Space name containing the display name, which `PersonalSpaceName.From` guarantees.
- **The browser selectors** (`nav.context-bar`, `.role-chip`, `button[aria-controls]`, `[role=status]`, `/spaces/{id}`) follow the story's Task 6/7 markup; they are the most likely scaffold corrections.
- **Container cost on activation: none.** All container-backed cases are in existing classes.

---

**Generated by BMad TEA Agent** - 2026-10-24
