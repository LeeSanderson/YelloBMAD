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
storyId: '1.8'
storyKey: '1-8-issue-an-api-token-scoped-to-one-space'
storyFile: '_bmad-output/implementation-artifacts/1-8-issue-an-api-token-scoped-to-one-space.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-1-8-issue-an-api-token-scoped-to-one-space.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'sequential'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.Tokens.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryRouteTests.cs'
  - 'tests/Yello.Tests.Slices/Accounts/RegisterAccount/SpaceIsolationSchemaTests.cs'
  - 'tests/Yello.Tests.Slices/WireSession.cs'
  - 'tests/Yello.Tests.Architecture/ApiTokenGateTests.cs'
  - 'tests/Yello.Tests.Client/AccountSettingsTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/1-8-issue-an-api-token-scoped-to-one-space.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-1-7-list-and-switch-between-the-spaces-you-belong-to.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 1, Story 1.8: Issue an API Token scoped to one Space

**Date:** 2026-10-24
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host and SQL Server, plus in-process pipeline cases, ARCH gates, and browser cases with the API faked at the network edge.

---

## Story summary

A Token is a 256-bit random secret. Only its SHA-256 is stored, in an `ApiToken` row whose composite foreign key points at one `(SpaceId, AccountId)` Membership and cascades on delete. Every Token request looks the row up by hash, with no cache, and builds a principal from the row. That principal carries no Session claim and no Role. The establishment seam from story 1.5 reports the bound Space beside the route's Space, and a mismatch is refused exactly as a non-member is. The Account-scoped surfaces read only the Session claim, so they answer a Token `401` by construction. On the client, Account settings shows the plaintext once and makes dismissal a confirmed act.

**As** someone who will drive Yello from a script, **I want** a credential that reaches exactly one Space, **so that** automation can never become a route around the boundary.

## Acceptance criteria (abridged; verbatim text is in the story)

1. **AC1** - bound to one `SpaceId` at issue, immutable.
2. **AC2** - another Space is refused as if no Membership existed, even one the Account owns.
3. **AC3** - capability is the current Role, read per request.
4. **AC4** - plaintext shown once, after the page has said so; only a hash stored; dismissal confirmed.
5. **AC5** - invalidated in the same transaction as Membership removal, Space deletion, Account deletion.
6. **AC6** - the two surfaces differ only in establishment; no slice branches on surface.
7. **AC7** - no per-Space Role in the Token list.
8. **AC8** - an invalidated (or revoked) Token is `401` on the very next request; nothing cached.
9. **AC9** - a Token reaches no Account-scoped surface, `sessions/current` or sign-out.
10. **AC10** - the plaintext appears only in the single issuance response.
11. **AC11** - the issuance surface: Space picker from the switcher, shown-once copy, Copy primary, `Esc` inert, memory only, purged.
12. **AC12** - a Token's unsafe request needs no anti-forgery token; a cookie request still does; cookie plus bearer is authenticated by the bearer alone.

---

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`**, as stories 1.5-1.7 recorded. The repository has `Yello.slnx` and `.csproj` files, no `package.json`, and no Playwright or Cypress config. The browser suite uses Playwright *for .NET*, which none of the frontend indicators detect. Its cases are still written, because the story's Task 9 requires them and `TESTING-CONVENTIONS.md` binds the E2E level to `Yello.Tests.Client`.

**Prerequisites - met:** the story is `ready-for-dev`, and AC1-AC7 are verbatim from `epics.md:846-875` (`status: final`). The framework is the MTP runner with six test projects. The environment is .NET SDK 10 with the Rancher container runtime up. No progress file existed for this story, so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.

**Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. Not loaded: `component-tdd` (this repository has no component level), `test-healing-patterns` (nothing to heal), `ci-burn-in` (story 1.10 owns it), and the Playwright Utils and Pact fragments (disabled, and TypeScript-shaped). Where a fragment and `tests/TESTING-CONVENTIONS.md` disagree, the repository document wins.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack. The sources are the story's ACs, Tasks, Testing requirements and open questions; the live schema (five migrations); and the existing harnesses (`HostProcess`, `WireSession`, `SpaceSeeding`, `RegistrationDatabase`, `DurationIndistinguishability`, `FakeYelloApi`, `ClientSurfaceFixture`).

## Step 3 - Test strategy

### The constraint that shapes every scaffold

Every scaffold must compile (warnings are errors) without product code. So types story 1.8 has not written are reached **by name through reflection**: `ApiTokenPrincipal`, `ApiToken`, `ApiTokenSecret`, and the three-argument `EstablishedSpaceRequest`. The new routes are **string literals**: `/api/v1/spaces/{id}/api-tokens` and `/api/v1/account/api-tokens[/{id}]`. Response bodies are **raw JSON**, and the fake's Token answers are **anonymous objects**. The `ApiToken` columns are read by **raw SQL**. Each stand-in fails at run time with a message naming the task that makes it real. **On activation, replace each with the typed reference or route constant.** The names come from the story. A different name chosen in dev-story is corrected in the scaffold and recorded in the Debug Log.

### Container budget

A full run starts **thirteen** engines. **No new container-backed class, now or on activation.** The wire cases are a third partial of `ActiveSpaceEndpointTests`. The pipeline cases go into `SpaceBoundaryPipelineTests` and the schema case into `SpaceIsolationSchemaTests`. The AC12 filter cases go into `SpaceBoundaryRouteTests`, which starts no container. `ApiTokenGateTests` and `AccountSettingsTests` start no engine.

### Scenario map

| id | AC | Level | Pri | Scenario | File |
|---|---|---|:--:|---|---|
| `1.8-INT-001` | 1, 4 | INT (wire) | P0 | Issue: `201`, exact property set, `yello_pat_` plaintext, row bound to the Space, 32-byte hash, no stored text value anywhere contains the plaintext | `ActiveSpaceEndpointTests.Tokens.cs` |
| `1.8-INT-002` | 1, 2 | INT (wire) | P0 | A Viewer may issue; a non-member Space and a missing Space each get a byte-identical `404`, `CrossSpace` recorded with `IssueApiTokenForOneself`, nothing written | `.Tokens.cs` |
| `1.8-INT-003` | 2 | INT (wire) | P0 | **I-12**: Token for A, used at B (owned by the same Account), is byte-identical to a non-member Session at B, and identical at a stranger's Space; recorded against the route Space | `.Tokens.cs` |
| `1.8-INT-004` | 2 | INT (wire) | P0 | Duration: Token mismatch vs non-member Session at the same Space (21 samples, median, 20%) | `.Tokens.cs` |
| `1.8-INT-005` | 3 | INT (wire) | P0 | Demoted Admin to Viewer between two Token requests; the second reports `viewer` | `.Tokens.cs` |
| `1.8-INT-006` | 5, 8 | INT (wire) | P0 | **V-3** Membership removal: rolled-back delete leaves the Token working (anchor); committed delete removes the row by cascade; next request `401` | `.Tokens.cs` |
| `1.8-INT-007` | 5, 8 | INT (wire) | P0 | **V-3** Space deletion cascades through Membership; the other Space's Token is still served | `.Tokens.cs` |
| `1.8-INT-008` | 5 | INT (wire) | P0 | **V-3** Account deletion (Memberships, then Account, in one transaction) removes every Token | `.Tokens.cs` |
| `1.8-INT-009` | 8 | INT (wire) | P0 | Revoking another Account's Token id gets `204`, and that Token still works; revoking your own gets `204` with the same body, and the next request is `401` | `.Tokens.cs` |
| `1.8-INT-010` | 8 | INT (wire) | P0 | Byte-identical `401` (body, content type, header names) across no credential, malformed, unknown, revoked, and Membership removed | `.Tokens.cs` |
| `1.8-INT-011` | 9 | INT (wire) | P0 | **I-11 Token half**: switcher, list, issue, revoke and `sessions/current` with a Token each get `401`, identical to no credential; sign-out is identical to no credential; a Token issues no Token; `SessionsValidAfter` is not stamped | `.Tokens.cs` |
| `1.8-INT-012` | Q11 | INT (wire) | P1 | A Token keeps working after its Account signs out of the Session (anchored on the cookie being `401`) | `.Tokens.cs` |
| `1.8-INT-013` | 12 | INT (wire) | P0 | Valid cookie plus an unknown bearer gets `401`; the same cookie alone gets `200` | `.Tokens.cs` |
| `1.8-INT-014` | 10 | INT (wire) | P0 | **I-17**: the secret is in no Host stdout/stderr line and no other response body across issue, use, cross-Space, a corrupted bearer, the switcher, the list, revoke, and use after revoke | `.Tokens.cs` |
| `1.8-INT-015` | 7 | INT (wire) | P0 | List rows are exactly `{createdAt, id, name, space{id, name}}`, with no `role`/`owner`/`viewer` and no secret in the body | `.Tokens.cs` |
| `1.8-INT-016` | Q8 | INT (wire) | P1 | A 100-character name is issued; a blank or 101-character name gets `400` with a `urn:yello:problem:` type that is not anti-forgery; one row written | `.Tokens.cs` |
| `1.8-PIPE-001` | 2 | INT (pipeline) | P0 | A Token bound to A, met at B where the Account is **also a member**, gets `404`; one `CrossSpace` row against B; anchored on A being served | `SpaceBoundaryPipelineTests.cs` |
| `1.8-PIPE-002` | 2 | INT (pipeline) | P0 | Duration: mismatch vs non-member, in-process | `SpaceBoundaryPipelineTests.cs` |
| `1.8-PIPE-003` | 2 | INT (pipeline) | P0 | **Planted oracle**: the method tells a refusal that skips the Membership read from one that makes it | `SpaceBoundaryPipelineTests.cs` |
| `1.8-ROUTE-001` | 12 | INT (filter) | P0 | A Token-authenticated `POST` through the real group, without an anti-forgery token, gets `204` | `SpaceBoundaryRouteTests.cs` |
| `1.8-ROUTE-002` | 12 | INT (filter) | P0 | A Session-authenticated `POST` without an anti-forgery token still gets `400` | `SpaceBoundaryRouteTests.cs` |
| `1.8-SCHEMA-001` | 5, 4 | INT (db) | P0 | `ApiToken`: exact columns, one FK (`Membership`, CASCADE) on `(BoundSpaceId, AccountId) -> (SpaceId, AccountId)`, no security predicate (anchored on Membership's), unique single-column index on `SecretHash`, `varbinary(32)` | `SpaceIsolationSchemaTests.cs` |
| `1.8-ARCH-001` | 9, 6 | ARCH | P0 | `ApiTokenPrincipal`: exactly the three Token claims, values mapped by parameter name, auth type `Yello.ApiToken`, `SessionPrincipal.AccountIdOf` returns null, no Role claim | `ApiTokenGateTests.cs` |
| `1.8-ARCH-002` | 1, 3 | ARCH | P0 | `ApiToken` entity: exact property set, no `Role`, `BoundSpaceId` has no public non-init setter | `ApiTokenGateTests.cs` |
| `1.8-ARCH-003` | 6 | ARCH | P0 | No `Yello.Application` type references `System.Security.Claims`, `Microsoft.AspNetCore` or `Yello.Host` | `ApiTokenGateTests.cs` |
| `1.8-ARCH-004` | 10 | ARCH | P0 | Exactly one Contracts type carries a `Token`/`Secret`/`Plaintext` property, and it is in `Yello.Contracts.ApiTokens` | `ApiTokenGateTests.cs` |
| `1.8-ARCH-005` | 4 | ARCH | P0 | `ApiTokenSecret` (methods found by shape): `yello_pat_` + base64url(32 bytes), two generations differ, hash = SHA-256(UTF-8(plaintext)) | `ApiTokenGateTests.cs` |
| `1.8-E2E-001` | 4, 11 | E2E | P0 | Account link in the context bar leads to `/account`; "shown once" is present before any issue request | `AccountSettingsTests.cs` |
| `1.8-E2E-002` | 4, 11 | E2E | P0 | Generate: the plaintext is in exactly one read-only field with focus; Copy is `button-primary`; one `POST` to the chosen Space; after in-app Back and return, the plaintext is gone | `AccountSettingsTests.cs` |
| `1.8-E2E-003` | 11 | E2E | P0 | `Esc` on the field and on the page does not dismiss | `AccountSettingsTests.cs` |
| `1.8-E2E-004` | 4, 11 | E2E | P0 | Done asks first; Keep showing keeps it; Dismiss removes it from the DOM and from field values; the list keeps the name | `AccountSettingsTests.cs` |
| `1.8-E2E-005` | 11 | E2E | P0 | No web storage while the plaintext is shown; sign-out purges it | `AccountSettingsTests.cs` |
| `1.8-E2E-006` | 7 | E2E | P0 | List shows names and Space names; no `.role-chip` and no Role word in `main` | `AccountSettingsTests.cs` |
| `1.8-E2E-007` | 11 | E2E | P1 | Space picker options are exactly the switcher Spaces, in order | `AccountSettingsTests.cs` |
| `1.8-E2E-008` | Q3 | E2E | P1 | Revoke asks in place; Cancel sends nothing; Revoke sends `DELETE` with the anti-forgery token and the row goes | `AccountSettingsTests.cs` |

### Duplicate-coverage guard - deliberately not written

- **Cookie `POST`/`DELETE` without anti-forgery is `400`** over the wire is already `SessionEndpointTests` (1.7 INT-009) and the registration cases. ROUTE-002 holds the same rule at the filter, so the Token exemption cannot widen it.
- **The `not-available` body and the 404 contract** are 1.6's cases. Token cases assert only that the Token path produces the same body.
- **`ClientStorageBanTests`** bans storage in the IL. E2E-005 is the runtime half.

### Not generated, with reasons - write these in dev-story

| Scenario | Why not scaffolded | Where |
|---|---|---|
| Extending `Every_Account_scoped_endpoint_names_an_AD_24_surface`'s exact list with the two Account-settings routes | An existing gate's expected list, extended rather than duplicated | Task 6 |
| "The set of `.SessionOnly()`/`.AccountScoped()` endpoints is exactly X" | The marker's type is Task 6's design; a reflective stand-in would be fiction | Task 8 |
| "Only the establishment, the handler and the `SessionOnly` filter read the Token claims", "only the Token store reads `SecretHash`" | IL reachability over types that do not exist; the `RefusalBoundaryGateTests` walk takes the real types | Task 8 |
| `Space_is_read_only_where_named` gaining the Token-list reader | Existing allow-list; the reader's type is Task 6's | Task 6 |
| `PersistenceModelGateTests` and `Every_entity_configuration_reaches_the_model` for `ApiToken` | Existing gates that pick up a new entity on their own once it is configured | Task 2 |
| A Viewer's Token refused `403` on a write (V-1) | No Space-scoped write route exists until story 2.1 | Ledger to 2.1 (story Task 10) |
| Plants for a cache around the lookup, `Role` stored on the row, `.SessionOnly()` removed, Role on the list DTO, plaintext logged, cascade removed, second `SetAccountAsync` caller | Each plant edits product code this phase does not write. The scaffolds above are the gates that should catch them: INT-005/006 (cache, Role), INT-011 (SessionOnly), INT-015/E2E-006 (Role in list), INT-014 (logging), SCHEMA-001/INT-006 (cascade). Dev-story runs them | Story Testing requirements, plant table |

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents, but generation stayed in-thread for the reason 1.5-1.7 recorded: C# under this analyser set has to be built and fixed here anyway. The first builds failed on `MA0181` (x5), `S6605` (x2), `S6603`, `S3358` and `S4261`, and all were fixed. Worker A's output is the INT/ARCH files; Worker B's is the browser class.

### `ActiveSpaceEndpointTests.Tokens.cs` - 16 cases, third partial of the existing class (no new engine)

INT-001 to INT-016 as mapped above. **All skipped - red**, each `red: AC<n> - <why> (Task n)`. `WireSession` gained `GetWithTokenAsync`, `PostWithTokenAsync`, `DeleteWithTokenAsync` and `GetWithTokenAndCookiesAsync`. The first three attach **no cookie and no anti-forgery header** whatever the session holds; the fourth attaches both credentials and exists only for INT-013.

### `SpaceBoundaryPipelineTests.cs` - 3 cases added

| Test | id | Status |
|---|---|---|
| `A_Token_bound_elsewhere_is_refused_with_the_boundary_404_and_recorded_against_the_route_Space` | PIPE-001 | skipped - red: AC2 |
| `A_Token_mismatch_and_a_non_member_take_the_same_time_to_refuse` | PIPE-002 | skipped - red: AC2 |
| `The_duration_method_tells_a_refusal_that_skips_the_Membership_read_from_one_that_makes_it` | PIPE-003 | **ACTIVE - green today, permanent planted oracle** |

`ResolveAsync` gained an overload that takes the establishment and a `skipResolver` plant (`SkippingResolver`, a `DispatchProxy` that answers "no Membership" without reading). The existing cases call the old signature unchanged.

### `SpaceBoundaryRouteTests.cs` - 2 cases added (no container)

ROUTE-001 `A_Token_authenticated_state_change_is_not_refused_for_anti_forgery` is **skipped - red: AC12**. ROUTE-002 `A_Session_authenticated_state_change_without_an_anti_forgery_token_is_refused_400` is **ACTIVE - green today, a standing gate, planted**.

### `SpaceIsolationSchemaTests.cs` - 1 case added

SCHEMA-001 `ApiToken_cascades_from_its_Membership_and_is_outside_the_policy_by_design`. **Skipped - red: AC5.**

### `tests/Yello.Tests.Architecture/ApiTokenGateTests.cs` - 5 cases, new class, no container

| Test | id | Status |
|---|---|---|
| `The_Token_principal_carries_exactly_its_three_claims_and_not_the_Session_claim` | ARCH-001 | skipped - red: AC9 |
| `The_ApiToken_entity_stores_no_Role_and_its_bound_Space_cannot_be_reassigned` | ARCH-002 | skipped - red: AC1/AC3 |
| `No_Application_type_references_claims_HTTP_or_the_Host` | ARCH-003 | **ACTIVE - green today, standing gate, planted** |
| `Only_the_issuance_response_carries_a_Token_secret` | ARCH-004 | skipped - red: AC10 |
| `A_Token_secret_is_256_random_bits_behind_a_prefix_and_is_stored_as_its_SHA_256` | ARCH-005 | skipped - red: AC4 |

### `tests/Yello.Tests.Client/AccountSettingsTests.cs` + `FakeYelloApi.cs` - 8 cases, new class in the existing collection, no container

E2E-001 to E2E-008, **all skipped - red**. `FakeYelloApi` now answers the list, issue and revoke routes with anonymous objects until Task 6 writes the contract types, so **serialise those on activation**. It holds `ApiTokens`, issues a unique `IssuedSecret` in Q9's format, and records request bodies (`FakeRequest.Body`, an optional parameter, so existing constructions are unchanged).

### Fixtures and seed data - every constraint checked

No new fixture and no new container class. Seeding reuses `SpaceSeeding` (B2) and `RegistrationDatabase`. Checked against `20260828164716_InitialSchema.cs`, `20261024121107_SpaceBoundaryRefusals.cs` and `20261024143930_AccountScopedMembership.cs`:

- **`FK_Membership_Account_AccountId` (Restrict)**: every seeded Membership's Account is the product-registered one, or is added earlier in the same block (`SeedForeignSpaceAsync`, `SeedSpaceWithMemberAsync`). PIPE-001's second Membership is for an Account already written in an earlier block.
- **`FK_Membership_Space_SpaceId` (Cascade)**: each `Space` is added in the block that first writes a Membership into it. `AddMembershipAsync` writes only into a Space that already exists.
- **`UX_Membership_SpaceId_Owner`**: at most one Owner per Space. PIPE-001's added Membership is `Admin` in a Space that already has an Owner.
- **`IX_Membership_SpaceId_AccountId` (unique)**: one Membership per Account per Space throughout. This is also the alternate key that Task 2's composite FK will reference.
- **The future composite FK `(BoundSpaceId, AccountId) -> Membership(SpaceId, AccountId)`**: no case inserts an `ApiToken` row by hand. Every Token is issued through the product for a Space its Account belongs to, so the FK is satisfied by construction.
- **Deletes**: Space deletion runs under that Space's own session context, so the FILTER predicate exposes every Membership, StatusDefinition and SpacePresence row the cascade must reach (all three FKs to `Space` are `Cascade`). Account deletion removes Memberships first (`Restrict`), under the `AccountId` session key from story 1.7's predicate, in one transaction. Nothing else holds a foreign key to `Account`: `AccessRefusal` and `SpacePresence` carry none.
- **RLS BLOCK AFTER INSERT** on `Membership`: every seeding block runs under its own Space's context.
- **`Account.NormalizedEmailAddress` (unique)**: random addresses throughout. Display names are unique per test.

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Test run

```
dotnet test Yello.slnx
Test run summary: Passed!   total: 427   failed: 0   succeeded: 394   skipped: 33
```

Baseline before this phase: **392 total / 391 passed / 1 skipped**. Delta: **+35 cases, +3 active and passing, +32 skipped**. No previously passing test changed. No new SQL Server engine started. The browser suite passed in the combined run.

### Red scaffolds fail for the stated reason

The red scaffolds in `ApiTokenGateTests` and `SpaceBoundaryRouteTests` were un-skipped in a scratch copy, run, and restored from memory with a SHA-256 match:

- ARCH-001: "`Yello.Host.ApiTokens.ApiTokenPrincipal` does not exist (story 1.8 Task 4)".
- ARCH-002: "`Yello.Domain.ApiTokens.ApiToken` does not exist (Task 2)".
- ARCH-004: `Assert.Single()` failed because the collection was empty.
- ARCH-005: "`ApiTokenSecret` does not exist (Task 3)".
- ROUTE-001: expected `204`, got the anti-forgery `400`.

Every wire case fails on its first anchor: the issue route falls through to `404 not-available`, which `IssuedToken` reports with its status and body.

### The standing gates were planted - 2026-10-24

Each plant was written into the product file, the solution rebuilt, the case run, and the original restored in a `finally` from bytes held in memory. A SHA-256 comparison followed. All files were restored, and `git status` shows no product file modified.

| # | Plant | Gate | Caught |
|---|---|---|---|
| 1 | `System.Security.Claims.ClaimsPrincipal? Planted => null;` added as a default member of `Yello.Application/Spaces/IActiveSpaceResolver.cs` | ARCH-003 | **yes**: "Yello.Application references: System.Security.Claims.ClaimsPrincipal" |
| 2 | `AntiforgeryRequirement.ValidateStateChangingAsync` skips validation for any authenticated user | ROUTE-002 | **yes**: expected `400`, actual `204` |
| 3 | (the oracle is itself the plant) `SkippingResolver` answers "no Membership" without the read | PIPE-003 | **yes, the method tells the arms apart**, so PIPE-002 and INT-004 can mean something once activated |

### Red-phase compliance

- 32 of 35 new cases are skipped with `red: AC<n> - <why> (Task n)`.
- 3 are active and green: ARCH-003 and ROUTE-002 are standing regression gates, and PIPE-003 is a permanent planted oracle. Each was planted.
- Every scaffold compiles. No scaffold references a type story 1.8 has not written.
- Every refusal or absence assertion is anchored on a success in the same test. Every isolation assertion uses two Accounts or two Spaces. No assertion depends on the order of random GUIDs: sets are compared sorted.

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

1. **SCHEMA-001**, first. It tests the riskiest assumption: that a composite FK from `ApiToken (BoundSpaceId, AccountId)` to Membership's alternate key, with `ON DELETE CASCADE`, is accepted by SQL Server beside `Space -> Membership` cascade without error 1785, and works under RLS. Then **INT-006, INT-007, INT-008**: the cascade fires through a Membership delete, a Space delete and an Account delete under row-level security, on the real engine (story Task 2's "verify on the real engine"). AC5 and AC8 both stand on these.
2. **ARCH-002, ARCH-005** with the entity and the secret (Tasks 2-3).
3. **ARCH-001** with `ApiTokenPrincipal` (Task 4). Then **INT-001** (issue) and **INT-010** (401 byte-identity, which also catches a `WWW-Authenticate` that names the Token scheme). Then **INT-013** (cookie plus bearer, Q5).
4. **PIPE-001, PIPE-002**, then **INT-003** and **INT-004** (Task 5). Keep **PIPE-003** green throughout. If PIPE-002 is green but INT-004 is not, read the HTTP stack before widening anything.
5. **INT-002, INT-005, INT-009, INT-011, INT-012, INT-014, INT-015, INT-016** (Task 6). Replace the route literals with `ApiTokenRoutes` constants. **ROUTE-001** with the anti-forgery exemption. **ROUTE-002 stays green.** **ARCH-004** with the issuance response.
6. Client: **E2E-001**, then **E2E-004** (dismissal is the client's riskiest assumption: an in-place confirm, with no dialog component and `Esc` inert), then 002, 003, 005, 006, 007, 008. Serialise the fake's Token answers from the new contract types.

### Commands

```
dotnet build Yello.slnx
dotnet test Yello.slnx
dotnet test tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class "Yello.Tests.Architecture.ApiTokenGateTests"
dotnet test tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class "Yello.Tests.Slices.Spaces.ResolveActiveSpace.ActiveSpaceEndpointTests"
dotnet test tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class "Yello.Tests.Slices.Spaces.ResolveActiveSpace.SpaceBoundaryPipelineTests"
dotnet test tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class "Yello.Tests.Slices.Spaces.ResolveActiveSpace.SpaceBoundaryRouteTests"
dotnet test tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class "Yello.Tests.Client.AccountSettingsTests"
```

The browser suite starves under the full run. If it fails only in the combined run, re-run it alone before treating the failure as real.

## Notes and risks

- **AC9 and sign-out disagree with story 1.7.** AC9 lists `DELETE /api/v1/sessions` among the `401`s. But story 1.7 made credential-less sign-out idempotent (Q4), and anti-forgery answers a credential-less `DELETE` first. So "exactly as no credential" there is probably `400`, not `401`. INT-011 therefore **compares** the Token's sign-out answer with no-credential's and asserts that nothing is stamped, rather than asserting `401`. Dev-story records what the wire gives. If the answer must be `401`, that is a change to 1.7's decision and needs Lee.
- **By-name lookups are scaffolding.** Replace each with a typed reference on activation.
- **INT-001's hash check** asserts 32 bytes and not the plaintext bytes. The SHA-256 equality lives in ARCH-005, so the wire case survives a Q9 choice of a different 32-byte hash.
- **INT-004 is a wire-level duration case.** The in-process PIPE-002 and its oracle PIPE-003 are the sharper instrument. If INT-004 flakes, read PIPE-002 first.
- **Browser selectors and copy** are proposals: no copy is specified in the planning set, and Task 7 lists them as suggestions. They are the most likely scaffold corrections.
- **Container cost on activation: none.**

---

**Generated by BMad TEA Agent** - 2026-10-24
