---
baseline_commit: e074c55fe80e68ae495deb7fa07ba177d5995332
---

# Story 1.8: Issue an API Token scoped to one Space

Status: ready-for-dev

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-8-issue-an-api-token-scoped-to-one-space`
Requirements owned (coverage row `epics.md:351`, which cites "AR-6"; that is a slip for **AD-6**, see Q13): **FR-36 / CAP-36** (`prd.md:607-616`, `acceptance-criteria.md:358-364`), **AD-6** (`ARCHITECTURE-SPINE.md:108-112`), **NFR-6**'s Token clauses (`prd.md:725-726`). Also **AD-4** (`:96-100`, parity of the two surfaces), **AD-24** (`:222`, Account settings as the second Account-scoped surface), the Token half of **CAP-35 / FR-35** (`prd.md:604`, no API operation enumerates Spaces beyond the Token's own), **AR-34** (no Token in any log, `epics.md:222`), **AR-29** (no per-Space Role in the Token list, `epics.md:217`).
Test-design scenarios this story lands (`test-design-qa.md`): **I-12** (P0, `:276`, FR-36/AD-6, a Token reaches only its bound Space, including others its Account owns), the Token half of **I-11** (P0, `:275`, AD-24, the switcher is unreachable with a Token), **V-3** (P0, `:282`) as far as the schema can carry it (the three events do not exist yet, see Task 4 and Q6), the **NFR-6 credentials** row (P0, `:199`, Tokens unusable at rest, shown once), **I-17** (P1, `:350`, no Token in any log). Lifted to both surfaces in the Isolation and Revocation suites by story 1.9.
Ledger entries assigned to this story (`deferred-work.md`), folded in below: **the Token half of I-11** (`:316`, from 1.7: `GET /api/v1/spaces` with a Token must be refused over the wire, and the switcher must be unreachable from the API surface) → AC9.
Depends on: **1.3** (schema, `MigratedDatabaseFixture`, `IIdentifierGenerator`), **1.4** (Session scheme, `SessionPrincipal`, `WireSession`, `HostProcess`, `YelloApiHandler`, `SessionState` purge), **1.5** (`ISpaceRequestEstablishment` — the seam built for this story — `ActiveSpaceResolution`, `MembershipActiveSpaceResolver`, `SpaceSeeding`), **1.6** (`ApiRouteGroup`, capability matrix incl. `Capability.IssueApiTokenForOneself`, `AccessRefusal` recording, `not-available`, `DurationIndistinguishability`), **1.7** (`AccountScopedResolution`, `AccountScopedSurface.AccountSettings` reserved for this story, `AccountSpaceDirectory`, `SessionRevocation`, context bar, `FakeYelloApi`). All done.
Seeds, does not own: the invalidation that stories **3.3** (Space deletion), **4.5** (Membership removal) and **5.4** (Account deletion) perform inside their own transactions; the per-Token identity story **8.3** partitions its rate limit by; the two-surface fixture story **1.9** builds over this Token surface; the Viewer-Token-cannot-write case (V-1) that lands with the first Space-scoped write in **2.1** and is proved on a live Token in **7.8**.

**Settled before this story (Lee, story 1.7, 2026-10-24):** an invalidated credential grants access to nothing (FR-2, CAP-2). Sign-out ends Sessions server-side through `Account.SessionsValidAfter`. A Token is held to the same standard: a Token that has been invalidated — by any of the three events or by its holder — is refused on the very next request. **A stateless Token that survives its invalidation is not acceptable.** That is why the design below looks every Token up on every request and never caches the result.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As someone who will want to drive Yello from a script,
I want to generate a credential that reaches exactly one Space and no further,
So that automation can never become a route around the boundary.

## Acceptance Criteria

AC1–AC7 are reproduced **verbatim** from `epics.md:846-875` (`status: final`), numbered by position. AC8–AC12 are carried from the ledger, the spine and the UX spine, and say where from.

**AC1 — bound to one Space at issue, for good**
**Given** a Membership in a Space
**When** that Account issues an API Token for it
**Then** the Token is bound to that one `SpaceId` at issue, and the binding cannot afterwards be changed

**AC2 — another Space is refused as if no Membership existed, even one the Account owns**
**Given** an Account owning several Spaces
**When** it uses a Token issued for one of them against another
**Then** the request is refused exactly as it would be for an Account holding no Membership there — including for Spaces that same Account owns

**AC3 — the current Role, read per request**
**Given** a Token-authenticated request
**When** capability is resolved
**Then** it is the issuing Account's **current** Membership Role in that Space, resolved through the same per-request path as a browser request
**And** never the Role held at the moment the Token was issued

**AC4 — shown once, stored as a hash**
**Given** a Token
**When** it is created
**Then** the plaintext is displayed exactly once, with the interface saying so *before* generating it, and only a hash is stored
**And** a read of the datastore yields no usable Token, and dismissing the display is a confirmed act because it is the only copy

**AC5 — invalidated in the same transaction as the event**
**Given** a Token
**When** its Account's Membership is removed, its Space is deleted, or its Account is deleted
**Then** the Token is invalidated inside the same transaction as that event

**AC6 — the two surfaces differ only in establishment**
**Given** a Token-authenticated request and an equivalent browser request
**When** both enter the pipeline
**Then** they differ only in how `AccountId` and `SpaceId` are established, and thereafter traverse the identical pipeline and the identical slice
**And** no slice branches on calling surface

**AC7 — no per-Space Role in the Token list**
**Given** the API Token list in Account settings
**When** it renders
**Then** it shows no per-Space Role — that would be a Membership read across a boundary on an Account-scoped surface, and it is decoration rather than function

**AC8 — an invalidated Token grants nothing, on the very next request** *(carried from FR-2/CAP-2 as Lee settled it at story 1.7, NFR-2 `prd.md:696-700` "applies to API Tokens on the same terms", and AD-1 "never … any cache outliving the request")*
**Given** a Token that has been invalidated — by any AC5 event, or by its holder revoking it (Q3)
**When** any request is made with it
**Then** it is refused `401 authentication-failed` on that request, including for the Space it was bound to and in which the Account still holds a Membership
**And** nothing about a Token's validity is cached beyond the request

**AC9 — a Token cannot reach the Account-scoped surfaces** *(carried from `deferred-work.md:316`, the Token half of I-11; CAP-35 `acceptance-criteria.md:355`; AD-24)*
**Given** a valid Token
**When** it is presented to `GET /api/v1/spaces` (the switcher), to any Account-settings endpoint (listing, issuing or revoking Tokens), to `GET /api/v1/sessions/current`, or to `DELETE /api/v1/sessions`
**Then** each answers exactly as it does with no credential at all — `401 authentication-failed` — and nothing enumerates any Space, Account or Membership beyond the Token's own Space
**And** a Token can never issue, list or revoke Tokens

**AC10 — no Token in any log, error, or response but the one** *(carried from NFR-6 `prd.md:726`, AR-34 `epics.md:222`, I-17)*
**Given** a Token's plaintext
**When** the datastore, every log line the Host writes, every error body and every API response are inspected
**Then** the plaintext appears only in the single issuance response, and in none of the rest

**AC11 — the issuance surface** *(carried from `EXPERIENCE.md:274`, UJ-7 `:533-534`, UX-DR12, UX-DR37)*
**Given** Account settings, reached from the context bar
**When** a person issues a Token
**Then** they choose the one Space it is bound to from the Spaces they belong to (identity only), and are told before generating it that it will be shown once
**And** once shown, copying is the primary action, dismissing asks for confirmation, and `Esc` never dismisses it unconfirmed
**And** the plaintext is held only in component memory, never in `localStorage`, `sessionStorage` or IndexedDB, and is purged with every other Space-scoped and Session-scoped state

**AC12 — a Token request to a state-changing route needs no anti-forgery token, and a cookie request still does** *(carried from AD-7, whose Binds line is "FR-2, every browser request", `ARCHITECTURE-SPINE.md:116-118`)*
**Given** a Token-authenticated request with an unsafe method to a Space-scoped route
**When** it carries no anti-forgery cookie or header
**Then** it is not refused for anti-forgery, because it carries no ambient credential a third-party page could ride
**And** a request carrying the Session cookie is still refused without the anti-forgery token, and a request carrying both a cookie and a Token is authenticated by the Token alone (Q5)

## Tasks / Subtasks

- [ ] **Task 1 — Read the open questions first, and record each decision before the code that depends on it** (AC: all)
  - [ ] Q1 (where issuance lives), Q2 (where the bound Space meets the route), Q4 (the table's isolation shape) and Q5 (scheme selection) must be settled before Task 2. Q3 (revocation) and Q8 (name, count) before Task 5. Q10 (the display-once surface) before Task 7.
  - [ ] Each decision goes in the Completion Notes as "**Qn - (x).** … Cost to later stories: …". Every one is reversible.

- [ ] **Task 2 — The `ApiToken` entity, table and invalidation-by-construction** (AC: 1, 4, 5, 8)
  - [ ] `Yello.Domain/ApiTokens/ApiToken.cs`: `Id` (from `IIdentifierGenerator`, `ValueGeneratedNever` — gate `No_key_is_generated_by_the_database`), `AccountId`, `BoundSpaceId` (**not** named `SpaceId`, Q4), `Name` (Q8), `SecretHash` (`byte[]`, 32 bytes), `CreatedAt` (`DateTimeOffset` from `TimeProvider`). `BoundSpaceId` has a private/init-only setter and no method changes it (AC1). No Role, no expiry column (Q7), no last-used column (Q8).
  - [ ] `Yello.Infrastructure/Persistence/Configurations/ApiTokenConfiguration.cs`, added to the explicit `ApplyConfiguration` list in `YelloDbContext.cs:136-141` (gate `Every_entity_configuration_reaches_the_model`). Table and column names in `SchemaNames.cs`; **do not** add it to `SpaceScopedTables` (`SchemaNames.cs:137-138`).
  - [ ] Unique index on `SecretHash` (`varbinary(32)`) — the authentication lookup.
  - [ ] Index on `AccountId` — the Account-settings list.
  - [ ] **Composite FK `(BoundSpaceId, AccountId) → Membership(SpaceId, AccountId)` with `ON DELETE CASCADE`.** Add `HasAlternateKey(m => new { m.SpaceId, m.AccountId })` on `MembershipConfiguration` (the unique index at `:86-87` already holds the pair). This makes AC5 a database fact: removing the Membership deletes its Tokens in the same statement; deleting a Space cascades Space → Membership (`MembershipConfiguration.cs:92-95`) → ApiToken in the same statement; deleting an Account must delete its Memberships first (Account → Membership is `Restrict`, `:104-107`), which takes the Tokens with them. **Do not** add a second cascade from `Space` or `Account` directly: SQL Server refuses multiple cascade paths (error 1785; reasoning at `MembershipConfiguration.cs:97-103`).
  - [ ] Migration `yyyyMMddHHmmss_ApiTokens` scaffolded with the pinned `dotnet-ef` 10.0.11 (`dotnet tool restore`), then hand-edited to `TESTING-CONVENTIONS.md:292-305` (file-scoped namespace, real XML docs, no `// <auto-generated />`, literal names not `SchemaNames`, composite columns in static fields for CA1861, `Up` under 80 lines). It must apply cleanly after `20261024145154_SessionRevocation`.
  - [ ] **Verify on the real engine, not by reasoning** (this repository has twice shipped a plausible-but-wrong platform claim): (i) inserting an `ApiToken` whose `(BoundSpaceId, AccountId)` matches a Membership succeeds while the Space session context is set, and one that matches no Membership fails with the FK error; (ii) a `DELETE` of a Membership under that Space's session context cascades into `ApiToken`; (iii) a `DELETE` of a Space cascades through Membership into `ApiToken`. Record the outcome in the Debug Log.

- [ ] **Task 3 — The Token secret: generation and hashing** (AC: 4, 10)
  - [ ] `Yello.Infrastructure/ApiTokens/ApiTokenSecret.cs` (BCL only — no package; `PackageVersionPinTests` asserts the exact set): `RandomNumberGenerator.GetBytes(32)`, encoded base64url, with a fixed readable prefix (`yello_pat_`, Q9) so a leaked Token is recognisable by secret scanners. Hash with `SHA256.HashData` over the full plaintext's UTF-8 bytes. A fast hash is correct here and a slow one is wrong: the secret carries 256 bits of entropy, so there is nothing to brute-force, and a 270 ms PBKDF2 on every API request would breach NFR-5. Say so in the type's remarks; the planning set is silent on the algorithm (Q9).
  - [ ] Lookup is by the hash through the unique index, so no plaintext comparison happens anywhere and there is no timing oracle on the secret.
  - [ ] A format check before the lookup (prefix, length, base64url alphabet) answers `401` without touching the database — but answers it identically in body and shape to a well-formed unknown Token.
  - [ ] The plaintext is never a field of any persisted or logged type. `ApiToken.ToString()` is not overridden to include `SecretHash`. The EF command log filter (`Program.cs:64`) logs no parameter values; confirm `EnableSensitiveDataLogging` stays off.

- [ ] **Task 4 — Authenticate a Token on every request, and refuse an invalidated one** (AC: 3, 6, 8, 9, 12)
  - [ ] `Yello.Host/ApiTokens/ApiTokenAuthenticationHandler.cs`: an `AuthenticationHandler<AuthenticationSchemeOptions>` (in .NET 8+ the constructor takes `IOptionsMonitor<>`, `ILoggerFactory`, `UrlEncoder` — no `ISystemClock`). It reads `Authorization: Bearer <token>`, hashes, and looks the row up through a new Infrastructure store (`IApiTokenStore.FindByHashAsync` → `(TokenId, AccountId, BoundSpaceId)?`) on its own read with **no session context**, exactly as `AccountSessionStore` reads `Account` (`AccountSessionStore.cs`, remarks `:11-15`). One indexed read per Token request; **no cache** (gate `SpaceResolutionSeamTests` cache ban, `:224-251`; and Lee's standard above).
  - [ ] No row → `AuthenticateResult.Fail`/`NoResult` so the request continues unauthenticated, and the existing 401 path answers. A deleted Token (cascade, or revocation) is therefore refused on the very next request (AC8) with no extra code.
  - [ ] The principal is built from the **row**, never from anything in the Token string: an `ApiTokenPrincipal` with its own claim types (`urn:yello:claim:token-account-id`, `urn:yello:claim:token-space-id`, `urn:yello:claim:token-id`) and its own authentication type (`Yello.ApiToken`). **It must not carry `SessionPrincipal`'s claim type** (`urn:yello:claim:account-id`) and must not be built with `SessionPrincipal.For`. That single property is what makes the switcher (`AccountScopedResolution.cs:73`), `CurrentSession` (`SessionEndpoints.cs`) and sign-out (`SignOutEndpoints.cs:70-73`) answer a Token `401` with no further code (AC9). No Role claim (AD-1; `RoleApiBanTests`). The token id claim exists for story 8.3's rate-limit partition.
  - [ ] Scheme selection (Q5, recommended (a)): register `AddAuthentication(SelectorScheme).AddPolicyScheme(SelectorScheme, …, o => o.ForwardDefaultSelector = ctx => HasBearer(ctx) ? ApiTokenScheme : SessionCookiePolicy.SchemeName)`, keep `.AddCookie(SessionCookiePolicy.SchemeName, …)` exactly as it is, and add `.AddScheme<…, ApiTokenAuthenticationHandler>(ApiTokenScheme, …)`. A request carrying `Authorization: Bearer` is authenticated by the Token **alone** and its cookie is ignored. `SignInAsync`/`SignOutAsync` already name the cookie scheme explicitly (`SessionEndpoints.cs:152-156`, `SignOutEndpoints.cs:75`, `SessionRevocation.cs:90`) — keep it that way and re-check every `HttpContext.SignInAsync`/`SignOutAsync`/`AuthenticateAsync`/`ChallengeAsync` call names a scheme, because the default changes.
  - [ ] `SessionRevocation` stays on the cookie scheme only. Sign-out does not invalidate Tokens (Q11).
  - [ ] Unknown, malformed and invalidated Tokens answer the same body: `401 authentication-failed`, exactly `BoundaryProblems.Unauthenticated()`. Assert byte-identity between "no credential", "malformed Token", "unknown Token" and "Token whose Membership was removed".

- [ ] **Task 5 — Establish `AccountId` and `SpaceId` from a Token, through the same pipeline** (AC: 2, 3, 6)
  - [ ] Extend `EstablishedSpaceRequest` (`ISpaceRequestEstablishment.cs:40`) with `Guid? BoundSpaceId` — `null` for a Session. It stays a claim to be verified, never a grant, and still carries no Role.
  - [ ] Replace the singleton `SessionSpaceRequestEstablishment` registration (`Program.cs:88`) with one establishment that reads the principal's authentication type: Session → today's behaviour unchanged; Token → `AccountId` and `BoundSpaceId` from `ApiTokenPrincipal`, `SpaceId` from the `{spaceId}` route segment exactly as the Session branch parses it (`SessionSpaceRequestEstablishment.cs:26-30`). Keep it synchronous and allocation-cheap: it runs twice per request (`ActiveSpaceResolution.cs:78,130`). Either a composite type or a rename; Q2 decides the shape.
  - [ ] In `ActiveSpaceResolution.ResolveCoreAsync` (`:123-182`): when `BoundSpaceId` is set and differs from the route's `SpaceId`, the request is refused **exactly** as a non-member is: `CrossSpace` `AccessRefusal` recorded against the **route's** Space, `404 not-available`, byte-identical body. Do **not** return `SpaceId = null` from the establishment for this — that takes the unrecorded 404 at `:144-149` and violates AC2 and AD-20.
  - [ ] Equal work for equal answers (AD-3): the mismatch branch must not be measurably faster than a real non-member resolution. Recommended (Q2 (a)): still call `IActiveSpaceResolver.BeginAsync(accountId, routeSpaceId)`, discard and dispose whatever it returns, then refuse — so a Token for Space A used against Space B costs one Membership read just as a non-member does. Prove it with `DurationIndistinguishability` (21 samples, median, 20%) against a non-member Session request to the same Space, anchored by a planted oracle (an early return) that the comparison catches.
  - [ ] The Role still comes only from `MembershipActiveSpaceResolver.cs:65-70` on this request. The `ApiToken` row stores none, so demotion narrows a Token on the next request (AC3) without any Token code.
  - [ ] No slice, endpoint or handler learns which surface called it (AC6). `ActiveSpaceContext` is unchanged. Presence (`ServeAsync:196`) counts the Account the same way for both surfaces — the concurrent-Sessions bound counts Accounts, not credentials (story 1.6 decision 1).

- [ ] **Task 6 — The endpoints: issue, list, revoke** (AC: 1, 4, 7, 9, 10)
  - [ ] Routes in `Yello.Contracts/ApiTokens/ApiTokenRoutes.cs` (or beside `AccountRoutes`), DTOs in `Yello.Contracts/ApiTokens/`. Endpoint classes expose `public static Map*(IEndpointRouteBuilder)` — the route-table discovery in `RefusalBoundaryGateTests.cs:698-735` finds nothing else, and a gate that cannot see an endpoint passes vacuously.
  - [ ] **Issue** (Q1, recommended (a)): `POST /api/v1/spaces/{spaceId}/api-tokens`, body `{ name }`, mapped through `api`, `.RequiresCapability(Capability.IssueApiTokenForOneself)` (every Role, incl. Viewer — `RoleCapabilityMatrix.cs:52`, `prd.md:354`), and a new pipeline-owned `.SessionOnly()` marker that answers a Token-authenticated request `401` before anti-forgery, exactly as the switcher does (AC9). The Membership check, the boundary 404, the `CrossSpace` record and the matrix all come from the existing pipeline. Response `201` with `{ id, name, space: { id, name }, createdAt, token }` — the **only** response that ever carries the plaintext. The handler writes the row inside the request's unit of work. `ApiToken` carries no RLS (Q4), and the FK to `Membership` confirms the pair at insert.
  - [ ] **List**: `GET /api/v1/account/api-tokens`, `.AccountScoped(AccountScopedSurface.AccountSettings)` — the reserved member, now used. Rows are exactly `{ id, name, space: { id, name }, createdAt }`: **no Role** (AC7, AR-29), no hash, no prefix beyond what Q9 decides. Space identity only (AD-24). Read under the Account-scoped unit of work: the Space names join through `Membership`, which is RLS-filtered on `SESSION_CONTEXT('AccountId')`, and `AccountSpaceDirectory` is the **only** permitted setter of that key (`SpaceSwitcherGateTests.cs:343-353`) — so either add the Token read to `AccountSpaceDirectory`'s unit of work or give it a sibling method there. Do not add a second `SetAccountAsync` caller. If the read touches `Space`, add its type to `Space_is_read_only_where_named` (`RefusalBoundaryGateTests.cs:376-405`) with a stated reason.
  - [ ] **Revoke** (Q3, recommended (a)): `DELETE /api/v1/account/api-tokens/{tokenId}`, `.AccountScoped(AccountScopedSurface.AccountSettings)`, anti-forgery required (it is a cookie request). Deletes the row `WHERE Id = @id AND AccountId = @account`. `204` whether or not it matched — an id belonging to another Account must be indistinguishable from an unknown one and must not be deleted. Recorded nothing (not a Space boundary). The next request with that Token answers `401` (AC8).
  - [ ] Extend the exact expected list in `Every_Account_scoped_endpoint_names_an_AD_24_surface` (`RefusalBoundaryGateTests.cs:340-355`) with the two Account-settings routes. `The_Account_scoped_surfaces_are_exactly_the_two_AD_24_names` stays green unchanged.
  - [ ] Validation: `name` required, trimmed, 1–100 characters (Q8); a validation failure is `400` with a problem `type` the client can branch on, copy client-owned.
  - [ ] Idempotency: AD-18's `Idempotency-Key` is not built yet (story 2.1 owns it). Issuance accepts no key. Ledger to 2.1: a replayed issuance must **never** return the plaintext a second time (Q12).
  - [ ] Map the new routes in `Program.cs` through `api`. `SpaceScopedRouteCheck.EnsureEveryRouteIsResolved` stays last and must accept the issue route (one capability declared).

- [ ] **Task 7 — Account settings in the client: issue, show once, list, revoke** (AC: 4, 7, 11)
  - [ ] Route `/account` in `ClientRoutes.cs` (`Pages/AccountSettings.razor`), reachable only with a Session. An **Account** link in the context bar (`Components/ContextBar.razor:45-78`), after the switcher and before Sign out — three tab stops; re-check the skip-link note at `MainLayout.razor:23-27`.
  - [ ] The page is the Token section only (profile, password and deletion are not this story's). A list (name, Space name, created date, a **Revoke** control — no Role, no chip), and an issue form: a name `FormField` and a Space picker whose options come from the switcher's answer (`SpaceDirectory`) — identity only, no Role.
  - [ ] Before generating: a sentence stating the Token will be shown once and cannot be retrieved afterwards (AC4, `EXPERIENCE.md:274`). Copy is client-owned in `ClientCopy.resx` (PascalCase keys; no literal in any `.razor` — `LocalisationGateTests`).
  - [ ] After generating (Q10, recommended (a) — inline, no modal; no dialog component exists and building one is out of scope): the plaintext replaces the form in place, in a read-only monospace field; **Copy** is the primary action (`navigator.clipboard.writeText` via a small JS interop, with a visible confirmation that it was copied); **Done** asks "You will not be able to see this Token again." with **Dismiss** / **Keep showing** — dismissal is the confirmed act. `Esc` does nothing to it. Focus moves to the plaintext field when shown, and to the list's new row (or the page heading) when dismissed.
  - [ ] The plaintext lives only in a component field. On dismiss, navigation away, sign-out, Session end or Space switch it is cleared: register with `SessionState`'s purge (`Client/Program.cs:151-157`). `ClientStorageBanTests` must stay green.
  - [ ] Revoke asks for confirmation in place ("Scripts using this Token will stop working." / **Revoke** (danger) / **Cancel**), then refreshes the list.
  - [ ] A 401 from any of these is the Session ending (`YelloApiHandler`'s existing path). A 404 from issue (the chosen Space has gone from under you) renders `RefusalSurface`'s words in place.
  - [ ] `document.title` stays `Yello`. Strings respect 30–40% expansion: nothing sized to English.

- [ ] **Task 8 — Gates** (AC: 6, 7, 9, 10)
  - [ ] A Token principal never carries the Session claim: a unit test over `ApiTokenPrincipal` asserting its exact claim set (mirroring `SessionContractTests.cs:253-321`) and that `SessionPrincipal.AccountIdOf` returns `null` for it.
  - [ ] No slice branches on surface (AC6): an architecture gate that no type in `Yello.Application` references `ApiTokenPrincipal`, the Token scheme name, `ClaimsPrincipal`, or `HttpContext`, and that only the establishment, the handler and the `.SessionOnly()` filter in `Yello.Host` read the Token claims. Name it for exactly what it checks.
  - [ ] Only the Token store reads `ApiToken.SecretHash`, and no Contracts type except the issuance response carries a field named `Token`/`Secret`/`Plaintext` (AC10).
  - [ ] Every Session-only endpoint is enumerated: a gate that the set of endpoints carrying `.SessionOnly()` or `.AccountScoped(…)` is exactly the expected list, so a new Token-management route cannot be added without the marker.
  - [ ] **Plant every gate and every new refusal** (see Testing requirements). Write each gate so its name is exactly what it checks. Do not write a gate that passes vacuously over zero inputs — each must first find its subject and assert it found it.

- [ ] **Task 9 — Tests** (AC: all) — see Testing requirements for the case list. Put wire cases in a new partial `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.Tokens.cs` (no new container class — a full run already starts thirteen engines). Add `WireSession` support for a bearer request that attaches **no** cookie (`GetWithTokenAsync`, `PostWithTokenAsync`, `DeleteWithTokenAsync`). Add Token routes to `FakeYelloApi` for the browser cases. **Do not** add cases to `Yello.Tests.Isolation` or `Yello.Tests.Revocation`: story 1.9 owns populating them, they cannot reference Slices' helpers (`AllowedReferenceEdges.cs:142-143`), and adding a case means removing `--ignore-exit-code 8` in the same change.

- [ ] **Task 10 — Housekeeping and the ledger** (AC: all)
  - [ ] Close `deferred-work.md:316` (the Token half of I-11) with a dated CLOSED note naming the wire case.
  - [ ] Add ledger entries: to **3.3**, **4.5** and **5.4** — "the Token is invalidated by the `ApiToken → Membership` cascade; your slice must delete the Membership row (5.4: before the Account row) inside its own transaction, and add V-3's case for your event"; to **2.1** — Idempotency-Key on issuance must never replay the plaintext, and the first Space-scoped write must show a Viewer's Token refused `403` (V-1); to **8.3** — partition by the `token-id` claim; to **1.9** — lift I-12, the Token half of I-11 and V-3 into the Isolation/Revocation suites on both surfaces; and any Q-decision whose cost lands on a later story.
  - [ ] Update `tests/TESTING-CONVENTIONS.md` with the bearer helpers and the "a Token request attaches no cookie" rule.
  - [ ] **Do not add review narration to source files** (owned by Lee, project-wide). Do not tick this unless the diff holds it.

## Dev Notes

### The shape of the story in one paragraph

A Token is a 256-bit random secret whose SHA-256 is stored in an `ApiToken` row keyed to a `(BoundSpaceId, AccountId)` Membership by a cascading foreign key. Every Token request is authenticated by a dedicated scheme that looks the row up by hash — every time, no cache — and builds a principal from the **row**: Account, bound Space, Token id, never a Role. The AD-4 seam built in 1.5 (`ISpaceRequestEstablishment`) gains a Token branch that reports the bound Space beside the route's Space. `ActiveSpaceResolution` refuses a mismatch with the same work, record and body as a non-member, and otherwise everything after establishment is the browser's path, unchanged. The Account-scoped surfaces, `sessions/current` and sign-out read only the Session's claim type, so a Token is `401` there by construction. Invalidation is structural: delete the Membership, Space or Account and the Token row goes in the same statement. Revocation is deleting the row. The browser side is a page in Account settings that shows the plaintext once and makes dismissal a confirmed act.

### Current state of the files this story changes — read each before editing

| File | Today | This story | Preserve |
|---|---|---|---|
| `Yello.Host/Program.cs` | One scheme, cookie, as default (`:101-103`). `AddSingleton<ISpaceRequestEstablishment, SessionSpaceRequestEstablishment>` (`:88`). Middleware: localisation → CORS (`:162`) → `UseAuthentication` (`:210`) → `UseRateLimiter` (`:214`) → retention trigger → routes via `MapApiGroup()` (`:260`) → `MapUnmatchedApi()` (`:299`) → `EnsureEveryRouteIsResolved` (`:303`). No `AddAuthorization`/`UseAuthorization` (deliberate, `:35-38`). | Policy scheme as default, Token scheme added; establishment swapped; new routes mapped. | Middleware order. No authorization middleware. Every route through `api`. The route check last. The EF command-log filter (`:64`). |
| `Yello.Host/ApiRouteGroup.cs` | Filters, in order: stamp → `RequireAccountAsync` → `AccountScopedResolution.EstablishAsync` → `AntiforgeryRequirement.ValidateStateChangingAsync` → `ActiveSpaceResolution.ResolveAsync` (`:35-41`). | Add the `.SessionOnly()` check before anti-forgery; anti-forgery skips Token-authenticated requests (AC12). | 401 before anti-forgery before resolution (1.6 decision 11, plant P15). |
| `Yello.Host/Spaces/ISpaceRequestEstablishment.cs` | Sync `Establish(HttpContext)` → `EstablishedSpaceRequest(Guid? AccountId, Guid? SpaceId)`. Remarks name this story. | Add `BoundSpaceId`. | No Role ever. Sync and cheap. |
| `Yello.Host/Spaces/SessionSpaceRequestEstablishment.cs` | Account from `SessionPrincipal.AccountIdOf`, Space from the route. | One branch of the composite. | The Session's single claim. |
| `Yello.Host/Spaces/ActiveSpaceResolution.cs` | `ResolveCoreAsync` (`:123-182`): no Account → 401; no Space → unrecorded 404 (`:144-149`); null unit of work → `CrossSpace` record + 404 (`:158-164`); matrix → 403 + `InsufficientRole`; `ServeAsync` presence then endpoint. | Mismatch branch (Task 5). | Synchronous recording before the response; 404/403 never convert; the only reader of `Context.Role` with `ActiveSpaceEndpoints`. |
| `Yello.Host/Spaces/AccountScopedResolution.cs` | Session-only via `SessionPrincipal.AccountIdOf` (`:73`); remarks say 1.8 must never reach it. | Nothing, provided the Token principal lacks the Session claim. | Must not read the establishment. |
| `Yello.Host/Spaces/AccountScopedSurface.cs` | `SpaceSwitcher`; `AccountSettings` reserved "1.8 (API Tokens) and 5.4". | `AccountSettings` used. | Exactly two members. |
| `Yello.Host/Sessions/AntiforgeryRequirement.cs` | Validates every unsafe verb on the group (`:97-151`). | Skip when the request was authenticated by the Token scheme (and only then). | Method-shaped rule for cookie requests; plant that a cookie POST without a token is still `400`. |
| `Yello.Host/Sessions/SessionPrincipal.cs`, `SessionCookiePolicy.cs`, `SessionRevocation.cs` | One claim `urn:yello:claim:account-id`, auth type `Yello.Session`; cookie scheme name; `OnValidatePrincipal` revocation. | None. | `SessionContractTests`' exact claim set; `SchemeName` stays the cookie scheme. |
| `Yello.Host/Endpoints/SessionEndpoints.cs`, `SignOutEndpoints.cs`, `SpaceDirectoryEndpoints.cs` | Read the Session claim; the switcher is `.AccountScoped(SpaceSwitcher)`. | None in code; wire cases prove the Token `401` (AC9). | — |
| `Yello.Infrastructure/Persistence/YelloDbContext.cs`, `SchemaNames.cs`, `Configurations/MembershipConfiguration.cs`, `InfrastructureServices.cs` | Explicit configuration list; `SpaceScopedTables` = Membership, StatusDefinition, SpacePresence; Membership cascades from Space, restricts from Account. | Add `ApiToken`; alternate key on Membership; register the store Scoped. | Everything Scoped, no cache; no second cascade path. |
| `Yello.Infrastructure/Persistence/AccountSpaceDirectory.cs` | The only `SetAccountAsync` caller; Membership⋈Space projected to id, name. | Host the Token list read in its Account-scoped unit of work. | Single setter; identity-only projection. |
| `Yello.Contracts` | `ProblemTypes`, `ProblemResponse`, `SpaceRoutes`, `AccountRoutes`, `SessionRoutes`. | Token routes and DTOs. | URN `type` is the contract. |
| `Yello.Client/Components/ContextBar.razor`, `Pages/ClientRoutes.cs`, `Program.cs`, `Localisation/ClientCopy.resx`, `wwwroot/css/components.css` | Bar: switcher, Role chip, Sign out. No settings page, no dialog component. | Account link; `/account` page; purge registration; copy; styles from tokens. | Bar never on sign-in/registration; no storage; no literal copy. |

### Architecture constraints that bind here

- **AD-6** (`ARCHITECTURE-SPINE.md:108-112`): hash only, plaintext once; bound to one `SpaceId`, immutable; capability is the **current** Role via AD-1; Membership removal, Space deletion and Account deletion invalidate in the same transaction.
- **AD-4** (`:96-100`): the two surfaces differ only in establishment; no slice branches on surface.
- **AD-1** (`:75`): Role never from a cookie, claim, API Token payload, or any cache outliving the request. No Role claim on either principal.
- **AD-3 / AD-20**: a cross-Space Token is the boundary 404, byte-identical, recorded `CrossSpace` by the pipeline, indistinguishable by duration.
- **AD-24** (`:222`): Account settings ("profile, password, API Tokens, account deletion") is the second Account-scoped surface; may return Space identity — id and name — and nothing else.
- **AD-7** (`:116-118`): anti-forgery on state-changing **browser** requests; CORS one origin, credentials, never a wildcard. A bearer header is not an ambient credential; CORS does not apply to a script.
- **AD-18** (`:186`): Idempotency-Key on state-changing endpoints and rate limit per Token — both built later (2.1, 8.3).
- **AD-27 / AD-10**: nothing expires on a timer; no hosted service (gate `RefusalBoundaryGateTests.cs:598-621`).
- **CAP-35**: no API operation enumerates Spaces, Accounts or Memberships beyond the Token's Space.
- **Settled decision** (`addendum.md:119-123`, `decisions-settled.md:83-87`): Tokens are Space-bound, not Account-scoped; a script across three Spaces holds three Tokens — deliberate friction. Do not add multi-Space Tokens.
- Naming: entity `ApiToken` (spine `:265`). Never log a password, API Token or session cookie (spine `:274`).

### UX — exact behaviour and copy

| Element | Rule | Source |
|---|---|---|
| Entry | Context bar → Account. Account settings is Self-only. | `EXPERIENCE.md:55` |
| Before generating | State that the Token is shown exactly once and never retrievable. | `EXPERIENCE.md:274`, `epics.md:861` |
| Display | Copying is the primary action; dismissing is a confirmed act; no per-Space Role beside it. | `EXPERIENCE.md:274` |
| Space choice | One Space, chosen from those the Account belongs to; identity only. | UJ-7 `EXPERIENCE.md:533-534`, AD-24 |
| List | No per-Space Role ("decoration rather than function"). | `EXPERIENCE.md:191`, AR-29 |
| Confirms | No stacked modals; a confirm replaces the content in place. `Esc` closes a dialog — so the Token display is not a dialog (Q10). | `EXPERIENCE.md:67, 221` |
| Voice | Terse declaratives, sentence case, all copy in resources. | UX-DR37, `EXPERIENCE.md:435` |
| Buttons | Primary, danger, secondary only. | `DESIGN.md:468` |
| Focus | Dialog/panel close returns to the invoker or its nearest surviving container. | `EXPERIENCE.md:403` |

Copy is not specified anywhere; propose it in `ClientCopy.resx` and list it in the Completion Notes. Suggested: "This Token is shown once. Copy it now; you will not be able to see it again." / "Copy" / "Copied." / "Done" / "You will not be able to see this Token again." / "Dismiss" / "Keep showing" / "Scripts using this Token will stop working."

### Previous story intelligence (1.7, 1.6, 1.5)

- **1.5 built `ISpaceRequestEstablishment` for this story** and said "design the seam now; do not build the Token half" (`1-5-…md:97, 474`). Use it; do not route Tokens round it.
- **1.6 decision 11** put `RequireAccountAsync` (401) ahead of anti-forgery. A Token request with no `Authorization` header to a Space-scoped route is still a plain `401`.
- **1.6's matrix note** (`1-6-…md:279`): "Issue an API Token for oneself" is listed per Space; "1.8 decides where it is checked" → Q1.
- **1.6 refusal contract**: `not-available` byte-identical for both boundary causes; `ProblemResponse` drops title/detail; do not `AddProblemDetails()` (a `traceId` breaks byte-identity).
- **1.7 Q9**: the switcher was built Session-only and the Token assertion was ledgered here (`deferred-work.md:316`).
- **1.7's dominant review finding was a false claim in source remarks** (sign-out said an idempotent `204` where the wire gave `400`). Every remark you write about Token behaviour must be backed by a wire case.
- **1.7's review also caught** late async answers corrupting client state after a switch, and a non-exhaustive key switch. The display-once field and the Token list are exactly the kind of state a late answer can restore after a purge — guard with the same generation check `SpaceDirectory` uses.
- **1.6's review caught** gates that checked less than their names claimed, and assertions that only checked "non-blank". Assert exact codes, exact claim sets, exact route lists.
- **Plant discipline**: hold the original content in memory, restore in a `finally`, run plants in the foreground one at a time, SHA-256 compare the tree to a pre-run snapshot after each. **Never** `git checkout --`, `git restore` or `git reset`. 1.6 caught 19/19; 1.7 caught 28/29 and recorded the one miss rather than papering over it.

### Git intelligence

- `4d12ea8` built the switcher, `AccountScopedResolution`, `AccountScopedSurface`, `AccountSpaceDirectory`, `SessionRevocation`, migration `20261024145154_SessionRevocation`, `ContextBar`, `FakeYelloApi`, `SpaceSwitchingTests`. `e074c55` closed its review: monotonic `SessionsValidAfter`, cookie expiry on a refused ticket, sign-out retry with a fresh anti-forgery token.
- Commits are per phase, straight to `main`, by the orchestrator. Do not commit.

### Latest technical notes (.NET 10, BCL)

- `AuthenticationHandler<TOptions>` constructor since .NET 8: `(IOptionsMonitor<TOptions>, ILoggerFactory, UrlEncoder)`; `ISystemClock` is obsolete — inject `TimeProvider` if a time is needed.
- `AddPolicyScheme(name, displayName, o => o.ForwardDefaultSelector = …)` forwards authenticate/challenge/forbid/sign-in/sign-out to the selected scheme. Because it becomes the default, any `SignOutAsync()` without a scheme would be forwarded by the selector — every call must name the cookie scheme.
- `RandomNumberGenerator.GetBytes(int)`, `SHA256.HashData(ReadOnlySpan<byte>)`, `Base64Url.EncodeToString` (`System.Buffers.Text`, .NET 9+) — all BCL, no package.
- `navigator.clipboard.writeText` requires a secure context (HTTPS — both dev origins already are) and a user gesture.

### Project structure notes

- **New, most likely:** `Yello.Domain/ApiTokens/ApiToken.cs`, `IApiTokenStore.cs`; `Yello.Application/ApiTokens/IssueApiToken/*`, `ListApiTokens/*`, `RevokeApiToken/*`; `Yello.Infrastructure/ApiTokens/ApiTokenSecret.cs`, `Persistence/ApiTokenStore.cs`, `Configurations/ApiTokenConfiguration.cs`, migration `*_ApiTokens.cs`; `Yello.Host/ApiTokens/ApiTokenAuthenticationHandler.cs`, `ApiTokenPrincipal.cs`, `Endpoints/ApiTokenEndpoints.cs`, a `SessionOnly` filter; `Yello.Contracts/ApiTokens/*`; `Yello.Client/Pages/AccountSettings.razor`, `ApiTokens/*`; tests `ActiveSpaceEndpointTests.Tokens.cs`, `ApiTokenGateTests.cs`, `ApiTokenSecretTests.cs`, browser `AccountSettingsTests.cs`.
- **Modified, most likely:** as in the current-state table, plus `WireSession.cs`, `FakeYelloApi.cs`, `RefusalBoundaryGateTests.cs`, `SpaceIsolationSchemaTests.cs` (assert `ApiToken` is deliberately outside the policy), `PersistenceModelGateTests.cs`, `LoggerMessage` partials continuing `SpaceLog`/`SessionLog` EventIds.
- **Variance to record:** `ApiToken` is a table with a Space reference that carries no RLS policy (Q4) — the second after `AccessRefusal`. State why in the configuration's remarks and in `SpaceIsolationSchemaTests`.

### Testing requirements

Conventions (`tests/TESTING-CONVENTIONS.md`): underscore-sentence names, one behaviour each; traits `Suite`, `Priority`, `Requirement` (cite the AR **and** the AD, e.g. `FR-36`, `AD-6`); randomised data via `SpaceSeeding.NewAccount()`; no `Task.Delay`; cleanup by container disposal; every absence assertion anchored on a success first and backed by a plant.

Wire cases (`ActiveSpaceEndpointTests.Tokens.cs`, real Host via `HostProcess`, real SQL Server):
1. Issue for a Space you belong to → `201`, plaintext present once; the row's `BoundSpaceId` is that Space; a direct DB read shows only a 32-byte hash and no column equals or contains the plaintext (AC1, AC4).
2. Issue as a Viewer → `201` (matrix row). Issue for a Space you are not in, and for a random id → both `404 not-available`, byte-identical, `CrossSpace` recorded (AC2 shape).
3. The Token reads its Space (`GET /api/v1/spaces/{bound}` → `200` with the current Role). Anchor for every refusal below.
4. **I-12:** an Account owning Spaces A and B issues a Token for A; the Token against B → `404` byte-identical to a non-member Session request to B, `CrossSpace` recorded against B; and the same for a Space of another Account (AC2).
5. Duration: Token-for-A against B vs a non-member Session against B — indistinguishable (21/median/20%), with a planted early-return oracle that the helper catches.
6. AC3: change the Membership's Role by SQL; the very next Token request reports the new Role. (Viewer-cannot-write over the wire waits for the first write capability route in 2.1 — ledger.)
7. AC5/AC8/V-3: remove the Membership (raw SQL under the Space's session context, the `RemoveMembershipAsync` shape in `ActiveSpaceEndpointTests.Switcher.cs:498-508`) → the `ApiToken` row is gone in the same statement, the next Token request is `401`. Same for deleting the Space row. For Account deletion: delete the Account's Memberships then the Account in one transaction → Token gone. And the negative anchor: a rolled-back Membership delete leaves the Token working.
8. AC8: revoke via `DELETE /api/v1/account/api-tokens/{id}` → `204`; next Token request `401`. Revoking another Account's Token id → `204`, and that Token still works.
9. `401` byte-identity across: no credential, malformed bearer, unknown well-formed Token, revoked Token, Token whose Membership was removed.
10. **AC9 / I-11 Token half:** a valid Token to `GET /api/v1/spaces`, `GET /api/v1/account/api-tokens`, `POST /api/v1/spaces/{bound}/api-tokens`, `DELETE /api/v1/account/api-tokens/{id}`, `GET /api/v1/sessions/current`, `DELETE /api/v1/sessions` → each `401`, identical to no credential; and sign-out with a Token does not stamp `SessionsValidAfter`. Per Q11 (a), a Token still works after its Account signs out with a Session.
11. AC12: a Token `POST` to the issue route is `401` (Session-only), so prove the anti-forgery exemption on a state-changing Token request through a test-only route **or**, preferably, through the pipeline filter directly in `SpaceBoundaryPipelineTests` (no new container); and a cookie `POST` without the anti-forgery header is still `400`. A request carrying a valid cookie **and** an invalid bearer is `401` (the cookie is not consulted).
12. AC10/I-17: capture the Host's stdout/stderr for the whole Token class and assert the plaintext never appears; assert no error body contains it.
13. List: rows have exactly the property set `{id, name, space{id, name}, createdAt}` (raw-JSON property-set readers from `.Switcher.cs`), no `role` anywhere in the body.

Unit / architecture: `ApiTokenSecret` format and hash; `ApiTokenPrincipal` exact claim set; the Task 8 gates; `SpaceIsolationSchemaTests` naming `ApiToken` as outside the policy on purpose.

Browser (`Yello.Tests.Client`, `FakeYelloApi`): the pre-generation sentence is present before the first request; the plaintext appears once; `Esc` does not dismiss it; **Done** requires confirmation; after dismissal it is gone from the DOM; after sign-out it is gone; no storage writes; the list has no Role chip.

Plants (record each in the Debug Log table `| # | Plant | Gate | Caught |`): Token principal carries the Session claim; establishment returns `SpaceId = null` on mismatch (unrecorded 404); mismatch branch skips the resolver (duration); a cache around the Token lookup; the Token row read for capability (`Role` stored on it); anti-forgery exemption widened to all requests; `.SessionOnly()` removed from issue; a Role added to the list DTO; plaintext logged; cascade removed from the FK; list reads with a second `SetAccountAsync` caller.

Commands: `dotnet build Yello.slnx` (warnings are errors), `dotnet test Yello.slnx` (MTP via `global.json`; `--no-build` runs zero tests). The browser suite starves under the full run's engines — confirm it alone. If Rancher's socket times out: `rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch. Baseline at `e074c55`: **392 total, 391 passed, 0 failed, 1 skipped** (the legacy-factor duration case).

### References

- `_bmad-output/planning-artifacts/epics.md:838-875` (story), `:351` (coverage row), `:1555` (3.3 Token AC), `:1820` (4.5 Token AC), `:2067-2115` (5.4, no Token AC)
- `prds/prd-YelloBMAD-2026-08-15/prd.md:73, 209, 311, 354, 358, 604, 607-616, 630-635, 696-700, 725-726, 864`; `addendum.md:119-123`
- `architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md:75, 96-100, 108-112, 116-118, 186, 222, 265, 274`
- `ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:55, 67, 71, 191, 221, 274, 403, 435, 533-534`; `DESIGN.md:468`
- `specs/spec-yello/acceptance-criteria.md:355, 358-364`; `role-capability-matrix.md:26`; `decisions-settled.md:83-87`
- `test-artifacts/test-design-qa.md:130, 136-137, 199, 275, 276, 280, 282, 350, 559-566`
- `implementation-readiness-report-2026-08-22.md:857-859, 994`
- `implementation-artifacts/deferred-work.md:316`; `1-5-…md:97, 474`; `1-6-…md:279`; `1-7-…md:341`

### Open questions — each with a recommended decision

The dev phase takes these as **reversible, recorded decisions** in the Completion Notes. None blocks starting.

1. **Where issuance lives.** (a) `POST /api/v1/spaces/{spaceId}/api-tokens`, Space-scoped, capability `IssueApiTokenForOneself`, plus a pipeline `.SessionOnly()` marker — the Membership check, boundary 404, refusal record and matrix come free. (b) An Account-settings route with `spaceId` in the body, checking Membership in the Account-scoped unit of work — closer to "Account settings" but re-implements the boundary and its recording off the pipeline (AR-3). **Recommend (a).** The page is in Account settings; the write is to a Space.
2. **Where the bound Space meets the route.** (a) `EstablishedSpaceRequest.BoundSpaceId`; `ResolveCoreAsync` still calls the resolver for the route Space, disposes it, then refuses as a non-member. (b) Pass the bound Space into `IActiveSpaceResolver.BeginAsync` as a constraint — changes a port and the resolver's single meaning. (c) Reject in the handler before routing — loses the record and the route Space. **Recommend (a).**
3. **Can a person revoke their own Token?** No FR grants it; SM-6 counts "active" Tokens; the IA lists API Tokens in settings; and a credential its holder cannot withdraw falls short of the standard Lee set at 1.7. (a) Ship `DELETE` revocation (row deleted). (b) Defer revocation to Epic 8. **Recommend (a)** — a leaked Token otherwise lives until a Membership ends. Record it as the one capability this story adds beyond the epic's ACs.
4. **The `ApiToken` table's isolation shape.** (a) No RLS policy, column named `BoundSpaceId` (precedent `AccessRefusal`, and `Account` has none); reads only through one Infrastructure store by hash or by AccountId; the list's Space names via `AccountSpaceDirectory`'s Account-scoped unit of work. (b) A `SpaceId` column under the Space policy — the authentication lookup happens before any context exists and would see nothing, and the Account-wide list would need the `AccountId` key on a second table. **Recommend (a)**, recorded as a variance with a gate asserting it is deliberate.
5. **Scheme selection and the cookie-plus-bearer request.** (a) A policy scheme as default: `Authorization: Bearer` → Token scheme only, otherwise cookie. (b) Run both and merge identities — makes "which credential?" ambiguous and risks the switcher seeing a Session beside a Token. **Recommend (a).**
6. **AC5 without the events.** None of the three events exists until 3.3, 4.5, 5.4. (a) Make invalidation structural (cascading FK), prove it with raw-SQL deletes now, and ledger to each event story. (b) Build an `InvalidateTokens` method each story must remember to call. **Recommend (a)** — it cannot be forgotten. Note 5.4 has no Token AC; its ledger entry carries it.
7. **Expiry.** The docs are silent. (a) No expiry in v1; revocation and the three events are the lifecycle. (b) An optional `ExpiresAt` evaluated in the lookup's `WHERE` (AD-27-shaped). **Recommend (a)**, and ledger (b) to Epic 8.
8. **Name, count, last-used.** (a) A required name (1–100 chars); no count bound (NFR-8 declares none and AD-25 forbids inventing one silently — ledger it); no last-used column (a write on every API request). **Recommend (a).**
9. **Format and hash.** (a) `yello_pat_` + base64url(32 random bytes), SHA-256 of the full string. (b) Embed the Token id and compare a hash in constant time. **Recommend (a)** — lookup by hash removes any comparison. Display only the name, never a prefix or last four (nothing in the docs asks for it).
10. **The display-once surface.** (a) Inline, replacing the form in place; `Esc` inert; dismissal confirmed in place. (b) A modal dialog — `EXPERIENCE.md:221` says `Esc` closes a dialog, which would make an unconfirmed dismissal one key away, and no dialog component exists. **Recommend (a).**
11. **Does sign-out end Tokens?** The docs are silent. (a) No — sign-out ends Sessions (`SessionsValidAfter`); Tokens end by revocation or the three events. (b) Sign-out revokes all Tokens — surprising for a script, and it makes "sign out on this laptop" stop a server's automation. **Recommend (a)**, and record it. A wire case asserts a Token keeps working after its Account signs out, so the choice is visible rather than incidental.
12. **Idempotency vs display-once.** AD-18's replay "returns the original response", which would show the plaintext twice. (a) No `Idempotency-Key` on issuance now; ledger to 2.1 that issuance must replay without the secret or be exempt. **Recommend (a).**
13. **`epics.md:351` cites AR-6** (EF query filters) for this story; it means AD-6. (a) Record the correction in the Completion Notes as a divergence and leave `epics.md` alone (`status: final`). **Recommend (a).**

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

- 2026-10-24: Story created (create-story). Status -> ready-for-dev.
