---
baseline_commit: 54d50a9
---

# Story 1.6: Refuse at the Space boundary, and record every refusal

Status: done

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-6-refuse-at-the-space-boundary-and-record-every-refusal`
Requirements owned (coverage row `epics.md:349`): **FR-15** *(the refusal half — 1.5 built enforcement)*, **FR-16**, **AR-9, AR-10, AR-11** (AD-3, AD-20), **AR-30** (AD-25, *all six bounds declared*), **AR-34** (problem bodies), **NFR-7**, **NFR-8**, **UX-DR32**, **UX-DR35**. UX-DR37 (voice) applies through Epic 1's implementation notes (`epics.md:427`).
Also owned, **not** in the epic's acceptance criteria: the test-design handoff row for 1.6 — **I-5, I-6**, boundary 404 identical in body **and duration**, with the statistical method stated (`test-artifacts/test-design/YelloBMAD-handoff.md:88`). See AC10.
Ledger entries assigned to this story: `deferred-work.md` — rate limiting on the two PBKDF2 endpoints (**high**, Lee 2026-09-10), the concurrent-Sessions definition, the non-GUID `{spaceId}` bare 404, A-12's remaining three clauses, anti-forgery-before-401 ordering, the `{spaceId}`-group mapping gate, and `DbContext` reuse after a refusal. Idempotency (AR-25) is unassigned and "most plausibly 1.6" — see open question 10.
Depends on: **1.1** (gates, suites), **1.3** (schema, `MigratedDatabaseFixture`, `DurationIndistinguishability`), **1.4** (Session, `WireSession`, `HostProcess`), **1.5** (`ActiveSpaceResolution`, `IActiveSpaceResolver`, `SpaceScope`, `SpaceSessionContext`, `SpaceSeeding`, the first Space-scoped route). All done.
Seeds, does not own: the capability metadata every later Space-scoped endpoint declares; the bound registry stories 2.1, 2.2, 3.1, 4.3 and 7.3 consult; the refusal surface story 1.7 points its switcher at; the timing case story 1.9 runs on both surfaces.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As someone holding an identifier I should not have, or a Role that does not permit an action,
I want the product's answer to tell me nothing I am not entitled to know,
So that isolation holds under error as firmly as it holds under success.

## Acceptance Criteria

AC1–AC9 are reproduced **verbatim** from `epics.md:734-790` (`status: final`), numbered by position. AC10 is carried from the test-design handoff, not from `epics.md`.

**AC1 — one 404, two causes**

**Given** a resource in a Space the caller holds no Membership in, and separately an identifier matching no resource at all
**When** each is requested
**Then** both return **404** with a body identical in words, shape and layout
**And** neither carries a Space name, a resource title, or an identifier that was not already in the URL

**AC2 — 403 is capability-shaped and never converted**

**Given** a resource in a Space the caller does hold a Membership in, but lacks the Role for
**When** it is requested
**Then** **403** is returned with capability-shaped copy — "Viewers cannot edit Tasks." — rather than object-shaped copy that would confirm the resource exists
**And** no handler converts a 403 into a 404 or the reverse

**AC3 — the matrix governs**

**Given** the Role capability matrix and seeded Memberships at Owner, Admin, Member and Viewer
**When** each row of the matrix is exercised
**Then** the matrix governs the outcome in every case, and where an individual restatement disagrees with the matrix the matrix is correct and the restatement is a defect
**And** every capability is refused for an Account holding no Membership in the Space, without disclosing existence

**AC4 — the pipeline records every refusal**

**Given** any 403 or any Space-boundary 404
**When** it is produced
**Then** the pipeline — not the slice — writes an `AccessRefusal` row carrying the acting Account, the target Space, the capability attempted, the outcome, and a kind of `CrossSpace` or `InsufficientRole`
**And** those rows are retained 90 days and purged by a job running at most daily

**AC5 — `{spaceId}` in every Space-scoped path, gated**

**Given** a Space-scoped endpoint
**When** the architecture suite runs
**Then** the build fails if that endpoint resolves a `Task`, `Project`, `Label` or `StatusDefinition` without `{spaceId}` in its path
**And** no bare-id deep link exists, which is what keeps a record-writing boundary refusal from becoming separable by duration from an ordinary not-found

**AC6 — RFC 9457, and the `type` is the contract**

**Given** any refusal
**When** its body is inspected
**Then** it is RFC 9457 `application/problem+json` carrying a stable machine-readable `type` a client can branch on
**And** prose is never the contract

**AC7 — bounds refuse visibly, in the creating transaction**

**Given** the NFR-8 bound registry, declared in one place and checked by the pipeline rather than by any slice
**When** a bound is exceeded
**Then** the refusal carries a machine-readable reason and is raised inside the same transaction as the creation it refuses
**And** the copy states the bound plainly to the User, because a bound must degrade visibly, never silently

**AC8 — the registry declares all six**

**Given** the bound registry
**When** its entries are enumerated
**Then** it declares **all six** of NFR-8's bounds, each paired with the creation operation that must consult it, and a registry missing any of them fails the architecture suite:

| Bound | Value | Creation operation that consults it | Enforced by |
|---|:--:|---|---|
| Concurrent active Sessions per Space | 50 | establishing a Session in a Space (FR-2, FR-9) | this story |
| Projects per Space | 50 | creating a Project (FR-17) | Story 2.1 |
| Tasks per Project | 5,000 | creating a Task (FR-19) | Story 2.2 |
| Spaces per Account | 50 | creating a Space (FR-5) | Story 3.1 |
| Memberships per Space | 100 | accepting an Invitation (FR-11) | Story 4.3 |
| Concurrent editors per Task | 10 | establishing a sync lease on a Task (FR-31) | Story 7.3 |

**And** the registry is enumerated here rather than accumulated story by story, because a registry whose completeness is incidental is one where a missing bound is invisible — and NFR-8 makes an unenforced bound a defect rather than a relaxation
**And** this story implements the concurrent-active-Sessions-per-Space entry; the other five are implemented by the stories named, each of which consults this registry rather than declaring its own limit

**AC9 — a refusal in the browser is a page**

**Given** a refusal rendered in the browser
**When** it appears
**Then** it is a page and not a toast, because a toast implies a transient fault and this is a final answer
**And** the route back leads to a Space drawn from the switcher rather than a remembered last-Space, and the client owns the string rather than templating server-supplied prose

**AC10 — identical in duration, with the method stated (carried from the test-design handoff)**

**Given** the two Space-boundary 404 causes — a Space that does not exist, and a Space the caller holds no Membership in
**When** both are produced
**Then** they are compared for **duration** as well as body using `Yello.Tests.Shared/DurationIndistinguishability.cs` (21 interleaved samples per arm, medians, 20% of the slower median, server-side), and are indistinguishable
**And** a permanent planted-oracle test beside it (one arm skips the `AccessRefusal` write) proves the comparison can fail

## Tasks / Subtasks

- [x] **Task 1 — Read the open questions first, and record each decision before the code that depends on it** (AC: all)
  - [x] Fourteen questions are listed at the end of the Dev Notes, each with a recommended decision. Take each as a **reversible, recorded decision** in the Completion Notes: which option, why, and what it costs a later story. The orchestrator has no human in the loop for this phase; do not stop to ask.
  - [x] Questions 1 (what "active Session in a Space" counts), 2 (where `AccessRefusal` lives) and 4 (how a 403 is produced when no endpoint requires more than Viewer) shape the design. Settle them before Task 3.

- [x] **Task 2 — The capability vocabulary and the matrix, as data** (AC: 2, 3)
  - [x] `Yello.Domain/Memberships/Capability.cs` (enum) and `RoleCapabilityMatrix.cs` (a static table `Role × Capability → Allowed | Denied | Qualified`). Source: `_bmad-output/specs/spec-yello/role-capability-matrix.md:9-28` (question 5). One enum member per **Role-decided** row; the Ownership-Offer acceptance row is **not** a Role decision (AD-26 / AR-13 — "the only capability in Yello decided outside the FR-16 Role matrix"), so it is either absent or explicitly marked row-identity and never consulted by the Role check.
  - [x] Qualified cells ("Member ↔ Viewer only", "Members and Viewers only") are **Allowed-at-the-Role-gate** for Admin, with the target constraint owned by stories 4.4 / 4.5. Mark them `Qualified` so the later story cannot mistake an unconditional ✔ for the whole rule.
  - [x] `Yello.Domain` may reference nothing. No ASP.NET Core type, no EF type.
  - [x] **Never call it a policy, never use `[Authorize(Roles=…)]`, `RequireRole`, `IsInRole`.** Gate C (`RoleApiBanTests`) fails the build. `[Authorize]` with a policy is allowed but is not the mechanism here: AD-1 makes every decision a per-request `(Account, Space)` resolution off the Membership row.

- [x] **Task 3 — Every Space-scoped endpoint declares its capability; the pipeline checks it** (AC: 2, 3, 4)
  - [x] Endpoint metadata, e.g. `.RequiresCapability(Capability.ReadProjectsAndTasks)` → a `RequiredCapability` metadata record read by the pipeline. `GET /api/v1/spaces/{spaceId}/statuses` declares the read capability.
  - [x] In `ActiveSpaceResolution.ResolveCoreAsync`, after the unit of work resolves the context and **before** `next`: look up the metadata, consult `RoleCapabilityMatrix` with `ActiveSpaceContext.Role`, and on `Denied` return the 403 problem (Task 5). The 403 returns through `IsFailure`, so the unit of work rolls back — keep that.
  - [x] **A Space-scoped endpoint with no declared capability fails closed** (refuse, or throw at startup) — and an architecture gate asserts every `{spaceId}` endpoint declares exactly one (Task 8). Do not default to "read".
  - [x] No handler, endpoint or slice inspects `Role`. A-12's authorisation clause (Task 8) gates it.
  - [x] **No handler converts a 403 into a 404 or the reverse** (AC2). The 404 is produced only at resolution; the 403 only at the capability check. Assert both directions over the wire where possible.

- [x] **Task 4 — `AccessRefusal`: entity, table, migration, writer** (AC: 4)
  - [x] Fields: `Id` (app-generated via `IIdentifierGenerator`; `No_key_is_generated_by_the_database`), `AccountId`, target Space id, `Capability` (string, as `Role` is), `Outcome` (`404`/`403`), `Kind` (`CrossSpace` | `InsufficientRole`, stored as name), `OccurredAt` (`DateTimeOffset` UTC from the injected `TimeProvider`). Shape is AD-20 / AR-11 verbatim (`ARCHITECTURE-SPINE.md:198`, `epics.md:199`).
  - [x] **Kind mapping**: every Space-boundary 404 is `CrossSpace` (non-member *and* no-such-Space — never distinguished, AD-3); every 403 is `InsufficientRole`. NFR-7 (`prd.md:730-734`) requires the two to be distinguishable in the record.
  - [x] **Storage — question 2. Recommended:** the target column is **not** named `SpaceId` (e.g. `TargetSpaceId`), has **no FK** to `Space` (a refusal for a Space that does not exist is still recorded — `.memlog.md:68`), and the table carries **no RLS policy and no query filter**. Detection of "Space-scoped" is purely by a column/property literally named `SpaceId` (`SpaceIsolationSchemaTests.Every_table_with_a_SpaceId_column_is_covered_by_the_policy`, `PersistenceModelGateTests`), so this shape is **outside** `SchemaNames.SpaceScopedTables` by construction. State the variance from the spine's ER (`SPACE ||--o{ ACCESS_REFUSAL`, `ARCHITECTURE-SPINE.md:377`) in the Completion Notes. Add a gate that nothing outside the recorder and the purge (both in Infrastructure) references the `AccessRefusal` entity or `DbSet` — it is write-only from the product's point of view; there is no read surface (AD-24 allows exactly two Account-scoped surfaces).
  - [x] **The write needs its own unit of work.** At `Refuse()` the resolution transaction is already rolled back and disposed; a 403 is rolled back by `IsFailure`. `deferred-work.md:246` (owner 1.6): a `DbContext` reused after a refusal runs on a fresh pooled connection with no session context. Use a fresh scope/`DbContext` (or `IDbContextFactory`) for the insert — never `SpaceSessionContext.SetAsync` a second time on the same connection (error **15664**, verified in 1.5).
  - [x] **Synchronous, before the response** (question 7). Both boundary-404 arms then do identical work, which is what AC10 measures.
  - [x] The writer lives in `Yello.Infrastructure/Persistence/` behind a port in `Yello.Application` (e.g. `IAccessRefusalRecorder`). The pipeline (`Yello.Host/Spaces/ActiveSpaceResolution`) is its only caller.
  - [x] Migration via the pinned tool (`dotnet tool restore`; `dotnet-ef` **10.0.11**), then the **hand-editing pass** (`TESTING-CONVENTIONS.md:269-282`): file-scoped namespace, real XML docs, composite index columns in static fields, no method over 80 lines, **no `// <auto-generated />`**. Literal object names in the migration, not `SchemaNames`. Add an index on `OccurredAt` for the purge.
  - [x] Add the configuration to **both** `Configurations/` and `YelloDbContext.OnModelCreating`'s explicit list (`Every_entity_configuration_reaches_the_model`; table named exactly the CLR type name).

- [x] **Task 5 — The refusal contract: problem types, bodies, routes** (AC: 1, 2, 6)
  - [x] `Yello.Contracts/ProblemTypes.cs` — keep the `urn:yello:problem:<kebab>` scheme. Question 6: **recommended** replace `SpaceNotFound` with one `NotAvailable` (`urn:yello:problem:not-available`) used for **every** 404 the product answers, so a future in-Space not-found (a Task id missing inside the caller's own Space) cannot differ in body from a boundary 404 (AC1's "identifier matching no resource at all"). Add `InsufficientRole` (403) and `BoundReached`. Each gets the same remarks discipline as the existing constants.
  - [x] **403 body**: `type` = insufficient-role, plus a machine-readable `capability` extension (stable code) so the client can render "Viewers cannot edit Tasks." from its own resources. Nothing object-shaped: no resource id, no title, no Space name.
  - [x] **404 body**: identical bytes for both causes. Today's title "The Space was not found." — replace with a neutral title (e.g. "Not available."), the same constant on every 404. The title is not the contract; the client never reads it (`ProblemResponse` drops it).
  - [x] **Do not register `AddProblemDetails()` casually.** 1.5's `ActiveSpaceEndpointTests` compares raw bodies byte-for-byte and they match today, so no per-request `traceId` is emitted. The default problem-details writer can add a `traceId` that differs per request and would make every "identical body" assertion fail — or tempt someone to normalise it away. Verify before adopting; if adopted, strip or pin it and assert the bodies still match raw.
  - [x] **Non-GUID `{spaceId}`** (`deferred-work.md` 1.5 section; question 8): routing's `:guid` constraint currently answers an empty bare 404. Recommended: keep the constraint and give unmatched `/api/…` requests the same `not-available` problem body (e.g. a fallback endpoint on the `api` prefix, or status-code handling scoped to `/api`), writing **no** `AccessRefusal` because no Space boundary was reached. Assert body equality with a boundary 404.
  - [x] **Filter order** (`deferred-work.md` 1.5 review): a Space-scoped `POST` with no Session currently gets the anti-forgery `400`, not `401`. Recommended: a Space-scoped request with no Account is answered `401` before anti-forgery (question 11). Keep: no Session → **401** before any Space is read; 401 is **not** recorded and is **not** a 404 (AD-3 draws the line at the Space boundary and nowhere else). The anti-forgery 400 is not recorded either (`ProblemTypes.cs:73-79`, `AntiforgeryRequirement.cs:51-55`).

- [x] **Task 6 — The NFR-8 bound registry, and the one bound this story enforces** (AC: 7, 8)
  - [x] One declaration, e.g. `Yello.Domain/Bounds/BoundRegistry.cs`: six entries, each `(Bound id, value, consulting operation)`, values exactly as AC8's table. Story 2.1/2.2/3.1/4.3/7.3 consult it; none declares its own limit.
  - [x] **Concurrent active Sessions per Space = 50 — question 1. Recommended definition:** distinct Accounts with a live presence row in the Space. A Space-scoped table (`SpacePresence` or similar — it **does** carry `SpaceId`, so it needs FILTER + BLOCK predicates, a query filter from application state, and an entry in `SchemaNames.SpaceScopedTables`), keyed `(SpaceId, AccountId)`, `ExpiresAt` set to now + a stated window on each resolution. Inside the resolution transaction, already under the Space's session context: count rows `WHERE ExpiresAt > @now` (AD-27: expiry evaluated in the guarded statement's own `WHERE`, never by a timer); if the caller has no live row and the count is ≥ 50, refuse; otherwise upsert the caller's row. That is "raised inside the same transaction as the creation it refuses".
    - [x] **Do not put a `SpaceId` on the Session** (`deferred-work.md` 1.4 section; `SessionContractTests` plants one). Do not use an in-process counter (`No_production_code_introduces_a_cache`; NFR-2).
    - [x] Accounts, not cookies, are counted (a Session has no server-side identity — `SessionCookiePolicy` is a plain cookie ticket with no table). Record that simplification.
    - [x] Write amplification: an upsert per Space-scoped request. Only rewrite `ExpiresAt` when it is past half its window, and say so.
    - [x] Note the rollback interaction: `IsFailure` rolls back the presence upsert on any 4xx. Acceptable; record it.
  - [x] Refusal: status per question 9 (recommended **409**), `type` = bound-reached, extensions `bound` (stable id) and `limit` (the number). Not recorded as an `AccessRefusal` (AD-20 has exactly two kinds).
  - [x] Copy (UX-DR35): no UX string exists for this bound; the only exemplar is "This Space has 100 Memberships, the maximum." (`EXPERIENCE.md:276`). Add a client resource in that voice, keyed on the bound id, stating the number plainly.

- [x] **Task 7 — Retention: a purge that never wakes an idle database** (AC: 4)
  - [x] Delete `AccessRefusal` rows with `OccurredAt < now − 90 days`. **90 days is PRD §12 assumption 11, † hardened, unconfirmed** (`prd.md:902`, `SPEC.md:277`) — tag its test `[Trait("Assumption","PRD-12-11")]`.
  - [x] **AD-10 / AR-33** (`ARCHITECTURE-SPINE.md:136`, `epics.md:221`): "No component queries the database on a fixed interval … Cleanup jobs run at most daily." Story 1.10's AC (`epics.md:951`) adds "so an idle database is never woken". Question 3, **recommended:** opportunistic — triggered by inbound request traffic, at most once per 24h per process (an in-process last-run instant via `TimeProvider`, not a cache of Space data), never by a `PeriodicTimer`, `Timer` or `BackgroundService` loop. Running it off the request path is fine; running it on a schedule is not.
  - [x] An architecture gate: no `BackgroundService`/`IHostedService`/`PeriodicTimer`/`System.Threading.Timer` in production code except where named (A-9, `test-design-qa.md:307`). AD-27's "scheduled component writing a terminal expiry state" ban is about writes, not deletes — the purge is not caught by it and must not be argued as an exception to it.
  - [x] Tests use a fake `TimeProvider` and rows at 91 and 89 days. **No `Task.Delay`.** **No delete statements for cleanup** — container disposal only.

- [x] **Task 8 — Architecture gates** (AC: 3, 4, 5, 8)
  - [x] **AC5 / A-4**: enumerate the real route table — the `Map*` extensions are public static, so map them onto a throwaway `WebApplication` and read `EndpointDataSource`. Fail any endpoint whose handler reaches `Task`, `Project`, `Label` or `StatusDefinition` (or their readers/ports — IL reachability, the `ReferencesType` technique at `SpaceResolutionSeamTests.cs:302-330`) without `{spaceId}` in its pattern. Only `StatusDefinition` exists today: add a **vacuity guard** (the statuses endpoint must be found and classified) and name the other three by type name so they are covered the day they exist.
  - [x] **Every `{spaceId}` route is mapped through the group carrying `ActiveSpaceResolution`** (`deferred-work.md` 1.5 review, owner 1.6) — today a route mapped on `app` skips resolution and answers an empty `200`.
  - [x] **Every `{spaceId}` endpoint declares exactly one capability** (Task 3).
  - [x] **AC8 / A-7**: the registry has exactly the six bounds with AC8's values and operations.
  - [x] **A-12, the remaining three clauses** (`deferred-work.md` 1.5 section; `SpaceResolutionSeamTests.cs:261`): no type but the pipeline depends on `RoleCapabilityMatrix`, the refusal recorder, or the bound registry's enforcement path. Extend `Only_the_pipeline_depends_on_the_Space_resolver`'s permitted list (l.269-275) for any new pipeline type. Idempotency's clause stays open if question 10 is taken as recommended.
  - [x] Plant every gate (Task 11).

- [x] **Task 9 — Rate limiting on the two unauthenticated PBKDF2 endpoints** (ledger, severity **high**, Lee 2026-09-10)
  - [x] `POST /api/v1/accounts` and `POST /api/v1/sessions` each spend ~273 ms of CPU on any well-formed body. Use ASP.NET Core's built-in rate limiter (`AddRateLimiter` / `UseRateLimiter` / `RequireRateLimiting`, `Microsoft.AspNetCore.RateLimiting` in the shared framework since .NET 7 — **no package**, so `Directory.Packages.props` and `PackageVersionPinTests` are untouched).
  - [x] Question 12, **recommended**: a global concurrency limiter shared by the two endpoints (permits ≈ processor count, small queue) **plus** a per-client-IP fixed window. Attach by endpoint (a named policy on the two routes) or by group shape, never inside a slice (AR-3).
  - [x] **The 429 must be uniform**: the partition key may never include the submitted address, and the response may not differ for a known versus unknown address (AD-23). `application/problem+json` with a `rate-limited` type and `Retry-After` (AD-18). Not an `AccessRefusal`.
  - [x] Anti-forgery is **not** a rate limit (a token costs one `GET` total).
  - [x] Tests: drive past the limit over the wire with `HostProcess`; assert 429, `Retry-After`, identical bodies for known and unknown addresses. Configure limits so the test is fast — a configuration key with the resolved value **logged unconditionally** (`Program.cs:169-181` precedent).

- [x] **Task 10 — The refusal surface in the browser** (AC: 1, 2, 7, 9)
  - [x] A routed page component (not a toast, not an inline region) rendering for `not-available`: heading **"Not available."** (`EXPERIENCE.md:130-134`), the **same** page, words, layout and focus for both causes — **no client-side branch** on which occurred (UX-DR32). For `insufficient-role`: the capability statement, e.g. "Viewers cannot edit Tasks.", from resources keyed by capability code. For `bound-reached`: the bound copy.
  - [x] All strings in `Yello.Client/Localisation/ClientCopy.resx` (sentence case; gates `No_user_visible_string_literal_appears_in_a_component` and its `@code` sibling). Never render `title`/`detail` from the server (`ProblemResponse` deliberately omits them).
  - [x] `document.title` stays fixed `Yello` (UX-DR33). Focus lands on the `h1` (`App.razor`'s `FocusOnNavigate`). No `danger` colour token for a refusal (`DESIGN.md:501`).
  - [x] **Route back — question 13.** The switcher is story 1.7. Recommended: a link to the authenticated home route, which 1.7 repoints to a switcher-sourced Space; never a remembered last Space (UX-DR32, `EXPERIENCE.md:142`). Ledger the "drawn from the switcher" clause to 1.7.
  - [x] Converge the client's own unknown-route `<NotFound>` (`App.razor`, `ClientCopy.resx` `NotFoundHeading`/`NotFoundBody` — "There is nothing here" / "go back to where you were") onto the same surface and copy, so no URL shape produces a different refusal, and "go back to where you were" (a remembered location) disappears.
  - [x] Client logic tests go in `Yello.Tests.Architecture` (the only suite referencing `Yello.Client`; `SessionSeamTests` precedent). A browser case in `Yello.Tests.Client` needs either a Host or Playwright `page.RouteAsync` fakes — neither exists. Recommended: a `RouteAsync`-faked case for the two 404s rendering identically, with a plant; if that is too large, ledger it.

- [x] **Task 11 — Tests, and the plants that prove them** (AC: all)
  - [x] **AC1 / I-5 / I-13** — extend `ActiveSpaceEndpointTests` (do not replace `A_member_is_served_and_a_non_member_is_refused_exactly_as_a_missing_Space_is`): bodies byte-identical, no Space name, no id beyond the URL, both write one `CrossSpace` row.
  - [x] **AC2/AC3** — matrix exhaustively as data (every `Role × Capability`, against a table transcribed independently from `role-capability-matrix.md`, so the test is a restatement the matrix can disagree with). The pipeline's 403 path with **seeded** Memberships at all four Roles through the real resolver and database: invoke the filter directly with a fake endpoint declaring a write capability, as `ActiveSpaceUnitOfWorkTests` already does — do **not** add a product endpoint just to produce a 403 (question 4). Non-member → 404 for every capability.
  - [x] **AC4** — one row per refusal with each field; `CrossSpace` vs `InsufficientRole`; retention; purge cadence.
  - [x] **AC10 / I-6** — `DurationIndistinguishability`, plus a permanent planted oracle (one arm skips the record write) that must be told apart. **A class that needs no database takes no fixture**; this one does.
  - [x] **AC7** — the 51st distinct Account is refused, the 50 already present are not, an expired presence frees a slot (fake `TimeProvider`), and the refusal is in the resolution transaction (nothing written for the refused Account).
  - [x] **`ActiveSpaceUnitOfWorkTests` will break** the moment `ResolveCoreAsync` calls `GetRequiredService` for a new service (it builds a bare `ServiceCollection`, l.141-145). Register fakes there; keep its 409-rolls-back assertion.
  - [x] Every absence assertion gets a **planted violation, fired by name, recorded in a table** (1.5's P1–P15 shape). Suggested plants: a 403 for a member converted to 404; a 404 body carrying the Space name; a slice reading `Role`; a `{spaceId}`-less endpoint reading `StatusDefinition`; a `{spaceId}` route mapped on `app`; a Space-scoped endpoint with no capability; a registry with five bounds; a refusal path that skips the record; a record written by the slice; a purge on a `PeriodicTimer`; a rate-limit partition keyed on the email; a client branch on 404 cause.
  - [x] **Never `git checkout --` / `git restore` / `git reset` to revert a plant.** Hold the original in memory; restore in a `finally`.

- [x] **Task 12 — Housekeeping** (AC: all)
  - [x] Rewrite every comment that predicts this story so it describes what exists: `Program.cs:26-34`, `ActiveSpaceResolution.cs:30-34`, `ProblemTypes.cs:94-96`, `ListStatusesHandler.cs:19`, `Yello.Application/AssemblyMarker.cs:12-14`, `AuthenticateAccountHandler.cs:35,100`, `SpaceResolutionSeamTests.cs:261`, and any others a repository grep for `1.6` finds.
  - [x] Close or rewrite the `deferred-work.md` entries this story owns; add new entries with owners for what it defers (at least: the switcher-sourced route back → 1.7; the boundary-vs-in-Space duration comparison → 1.9 / 2.2; idempotency if deferred).
  - [x] **Do not add review narration to source files** (owned by Lee, project-wide). Do not tick this unless the diff holds it.
  - [x] Logs: `[LoggerMessage]` partials only (CA1848/CA1873). Continue `SpaceLog` EventIds after 1017. `SpaceId` as a field, never a Space name, never the email, never the cookie (AR-34).

### Review Findings

Code review 2026-10-24, range `9118277..HEAD` (ATDD scaffolds and develop), three layers (Blind Hunter, Edge Case Hunter, Acceptance Auditor). 40 raw findings, 29 after dedupe: 12 patch, 7 defer, 10 dismissed.

- [x] [Review][Patch] Two concurrent first requests from one Account both insert `(SpaceId, AccountId)` and the loser answers 500 on a primary-key violation [Yello.Infrastructure/Persistence/SpacePresenceRegister.cs:72]
- [x] [Review][Patch] A capability code the client has no copy for renders as the bare code, though the resx promises a fallback [Yello.Client/Components/RefusalSurface.razor:40]
- [x] [Review][Patch] The A-4 `{spaceId}` gate looks one call deep, not through the readers and ports Task 8 names [tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs:545]
- [x] [Review][Patch] The unknown-route refusal page does not move focus to its `h1`, unlike `/not-available` (AC9 "same focus") [Yello.Client/App.razor:15]
- [x] [Review][Patch] The 403 capability code, the `CrossSpace` record's capability and `OccurredAt`, and the 409's `bound` are asserted only as non-blank [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs, ActiveSpaceEndpointTests.cs]
- [x] [Review][Patch] Nothing asserts a bound refusal (409) writes no `AccessRefusal` row [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.cs]
- [x] [Review][Patch] Registration's rate limit is never tested; removing it from `MapRegisterAccount` fails nothing [Yello.Host/Program.cs:257]
- [x] [Review][Patch] The A-12 Role gate covers `ActiveSpaceContext.Role` but not `Membership.Role` [tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs:325]
- [x] [Review][Patch] `IAccessRefusalRecorder` documents its token as cancelling the write; the implementation ignores it by design [Yello.Application/Spaces/IAccessRefusalRecorder.cs:37]
- [x] [Review][Patch] A backwards clock step after a claim suppresses the purge for longer than a day [Yello.Infrastructure/Persistence/RefusalPurgeSchedule.cs:50]
- [x] [Review][Patch] The A-12 ledger entry repeats its title and leaves an unbalanced `**` [_bmad-output/implementation-artifacts/deferred-work.md:238]
- [x] [Review][Patch] The idempotency ledger entry says both "owner is now story 2.1" and "Owner: unassigned" [_bmad-output/implementation-artifacts/deferred-work.md:76]
- [x] [Review][Patch] `Each_boundary_404_records_one_CrossSpace_refusal_and_a_served_request_records_none` failed in 2 of 7 full runs. The cause is not load: it compared the **sorted** actual target-Space ids with an **unsorted** expected pair of two random `Guid`s, so it failed whenever `missingSpace` sorted after `otherSpace`, about half the time. It passed alone only by chance. The other suspects were ruled out: rows are filtered by a fresh Account's id, the record is awaited before the 404 is returned, and the purge deletes only rows older than 90 days. Both sides are now sorted [tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.cs:335]
  - How the patches were applied (2026-10-24, review phase): `SpacePresenceRegister` now catches the primary-key violation (2627, matched on number, as `AccountRegistrationStore` does), detaches the entity and admits the Account. This has **no deterministic test**: the race needs both reads to run before either insert, and nothing in the harness can order that. `RefusalCopy.For` gained a `hasCopy` overload, which `RefusalSurface` feeds from `ResourceNotFound`. The surface focuses its own `h1` on first render. The new browser case `RefusalSurfaceTests` was **planted** (focus move removed): the unknown-route arm failed and the routed arm passed. The A-4 gate now walks calls transitively, through state machines, closures and interface implementations. Registration is named as the one exemption, and it must be found reaching, which also proves the walk crosses a port. The new `No_slice_or_endpoint_reads_the_Role_off_a_Membership_row` gate was **planted** (a Host type reading `Membership.Role`) and caught it. Other changes: tightened assertions (exact capability code, `OccurredAt` in range, exact bound code, no refusal row for a 409); a new registration 429 case; a new backwards-clock purge case; and the two ledger corrections.
- [x] [Review][Defer] Rate-limit partition: a null remote address shares one bucket, and IPv6 per-address keys let one host rotate within its /64 [Yello.Host/Sessions/CredentialHashingRateLimit.cs:124] — deferred, owner story 1.10
- [x] [Review][Defer] The presence count can block behind another request's uncommitted presence write when `READ_COMMITTED_SNAPSHOT` is off [Yello.Infrastructure/Persistence/SpacePresenceRegister.cs:62] — deferred, owner story 2.9
- [x] [Review][Defer] The purge is one unbatched delete, and an authenticated caller can write refusal rows without throttle [Yello.Infrastructure/Persistence/AccessRefusalPurge.cs:63] — deferred, owner story 2.9
- [x] [Review][Defer] Qualified Admin cells pass the Role gate, but the Role-read gate leaves nowhere for 4.4/4.5 to enforce the target constraint [Yello.Host/Spaces/ActiveSpaceResolution.cs:170] — deferred, owner stories 4.4 / 4.5
- [x] [Review][Defer] `BoundRegistry` is not in the A-12 gate, so a slice could consult a bound itself [tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs:272] — deferred, owner story 2.1
- [x] [Review][Defer] The purge trigger runs on every request, so a probe of a database-free route wakes an idle database once a day [Yello.Host/Program.cs:211] — deferred, owner story 1.10
- [x] [Review][Defer] `AccessRefusal` rows keep an `AccountId` for up to 90 days after the Account is deleted, with no erasure rule stated [Yello.Infrastructure/Persistence/Configurations/AccessRefusalConfiguration.cs] — deferred, owner story 5.4

## Dev Notes

### The shape of the story in one paragraph

Story 1.5 built a pipeline with a single refusal point (`ActiveSpaceResolution.Refuse()`) and a single counting seam (`ResolveCoreAsync`), and said so in its remarks. This story hangs four things on them: a capability check that turns the Membership row's `Role` into a 403; an `AccessRefusal` record written by the pipeline for every 403 and every boundary 404; the bound registry, with the one bound that lives at Space resolution; and the full refusal contract — one 404 body, one 403 shape, RFC 9457 types, and a browser page that reads them. Almost no endpoint exists to exercise the matrix against (only `GET …/statuses`, which every Role may call), so most of the matrix is proved as data plus the pipeline invoked directly; epic 2 onwards adds endpoints that declare capabilities and get the check for free.

### Current state of the files this story changes — read each before editing

- **`Yello.Host/Spaces/ActiveSpaceResolution.cs`** (188 lines). Order: establish → no Account: `401 authentication-failed` → no Space: `Refuse()` → `BeginAsync` null: log 1017, `Refuse()` → `next` → `EnsureMaterialised` → `IsFailure` (≥400) returns **without completing (rollback)** → else `CompleteAsync(CancellationToken.None)`. **Preserve all of it**: 401 before any Space read; one 404 for both causes; rollback on ≥400; the materialisation check; the commit token; `IsSpaceScoped`/`IsFailure`/`EnsureMaterialised` are `internal` and used by tests.
- **`Yello.Host/Program.cs`** (248 lines). `UseRequestLocalization` → `UseCors` → startup logging → `UseAuthentication` → connectivity check → `WarmPasswordHashing` → `api` group with `AntiforgeryRequirement` then `ActiveSpaceResolution` → five `Map*`. **Preserve**: no `AddAuthorization`/`UseAuthorization`; the EF command-log filter (`:60`, an AD-23 disclosure); CORS before authentication; filters attached by shape on the group.
- **`Yello.Contracts/ProblemTypes.cs`** — five URN constants with long remarks; `SpaceNotFound` says 1.6 owns its full contract. `AuthenticationFailed` and the not-found type are deliberately single-valued; keep that property.
- **`Yello.Infrastructure/Persistence/MembershipActiveSpaceResolver.cs`** — `BeginTransactionAsync` → `SpaceSessionContext.SetAsync` → `spaceScope.Narrow` → read Role → no row: clear, rollback, dispose, `null`. The presence count/upsert (Task 6) belongs inside this transaction, after the Membership is confirmed. **Set-then-verify stays** (1.5 decision 1).
- **`Yello.Infrastructure/Persistence/YelloDbContext.cs`, `SchemaNames.cs`, `InfrastructureServices.cs`** — explicit configuration list; `SpaceScopedTables => [Membership, StatusDefinition]`; scoped registrations asserted exactly by `Space_resolution_state_is_registered_per_request`.
- **`Yello.Client/App.razor`, `Pages/ClientRoutes.cs`, `Localisation/ClientCopy.resx`, `Sessions/YelloApiHandler.cs`** — no 403/404 handling exists; `YelloApiHandler` turns a 401 while signed in into `SessionEndedAsync` + sign-in. Keep 401 → session-end only.

### Architecture constraints that bind here

- **AD-1** (`ARCHITECTURE-SPINE.md:71-75`): Role from the Membership row on this request only; never a cookie, claim, Token payload or cache. *"A request whose Space context cannot be resolved — no such Space, or no Membership in it — is refused with 404 (AD-3), never defaulted and never distinguished."*
- **AD-3** (`:88-94`): 403/404 line at the Space boundary and nowhere else; `{spaceId}` in every Space-scoped path; the timing case.
- **AR-3** (`epics.md:191`): authorisation, Space resolution, refusal recording, idempotency and bound checks live in the pipeline; a slice re-implementing one is a defect.
- **AD-20 / AR-11** (`:198`, `epics.md:199`): the `AccessRefusal` row, 90 days, purge at most daily.
- **AD-25 / AR-30** (`:228`): registry in one place, checked by the pipeline, refusal in-transaction with a machine-readable reason; an unenforced bound is a defect.
- **AD-10 / AR-33** (`:136`, `epics.md:221`): nothing queries the database on a fixed interval; cleanup at most daily.
- **AD-18** (AR-25): `Idempotency-Key` on every state-changing endpoint; rate-limit refusals machine-readable with `Retry-After`.
- **AD-21** ring rule: `Domain` references nothing; `Application` no ASP.NET Core / EF; adapters `internal` in Infrastructure; `InternalsVisibleTo("Yello.Tests.Slices")` exists. A new project reference changes `AllowedReferenceEdges` (exact set) — avoid.
- **AD-24** (`:218-222`): exactly two Account-scoped surfaces (switcher, Account settings); no disabled policy, no second raw connection, no bypass that spreads. The refusal log is write-only and has no surface.
- Conventions (`:266-276`): slices `Yello.Application/{Area}/{UseCase}/`; routes `/api/v1/spaces/{spaceId}/…`, `v1` a literal (story 8.2 owns versioning; `Asp.Versioning.Http` stays unreferenced); ids Guid via the sequential generator; dates `DateTimeOffset` UTC; errors RFC 9457.

### The matrix (source of truth — question 5)

`_bmad-output/specs/spec-yello/role-capability-matrix.md:9-28`. Differences from `prd.md:338-354`: "Transfer ownership" → "Offer to transfer ownership"; added "Accept or decline an Ownership Offer" (— / ✔† / ✔† / ✔†, row identity). Both documents claim to win a disagreement. Rows (Owner / Admin / Member / Viewer):

| Capability | O | A | M | V |
|---|:--:|:--:|:--:|:--:|
| Read Projects and Tasks | ✔ | ✔ | ✔ | ✔ |
| Create, edit, delete Projects | ✔ | ✔ | ✔ | — |
| Create, edit, delete Tasks | ✔ | ✔ | ✔ | — |
| Edit a Task description collaboratively | ✔ | ✔ | ✔ | — |
| Assign a Task | ✔ | ✔ | ✔ | — |
| Configure Space default Statuses | ✔ | ✔ | — | — |
| Configure a Project's Status delta | ✔ | ✔ | — | — |
| Manage Labels | ✔ | ✔ | — | — |
| Issue and revoke Invitations | ✔ | ✔ | — | — |
| Change a Membership's Role | ✔ | Member ↔ Viewer only | — | — |
| Remove a Membership | ✔ | Members and Viewers only | — | — |
| Rename the Space | ✔ | ✔ | — | — |
| Offer to transfer ownership | ✔ | — | — | — |
| Delete the Space | ✔ | — | — | — |
| Issue an API Token for oneself | ✔ | ✔ | ✔ | ✔ |
| Accept or decline an Ownership Offer | — | ✔† | ✔† | ✔† |

† Row identity, not Role (AD-26). Boundaries outside the matrix (`role-capability-matrix.md:36-43`: Owner uniqueness, consent, Admin symmetry, no Invitation at Owner) are later stories' and are not encoded here. *Note: "Issue an API Token for oneself" is Account-scoped in story 1.8's surface (Account settings), yet listed per Space — keep it in the enum; 1.8 decides where it is checked.*

The readiness report notes 1.6 uses **seeded** Memberships on purpose and story 4.7 re-exercises the matrix through real paths (`implementation-readiness-report-2026-08-22.md:964-968`). The Role matrix is tested at INT and never E2E (`test-design-architecture.md:175`); it has no system-level scenario ids.

### UX — exact copy

| Situation | Status | User sees |
|---|---|---|
| No Membership | 404 | "Not available." |
| Does not exist | 404 | "Not available." — identical |
| Member, wrong Role | 403 | capability statement, e.g. "Viewers cannot edit Tasks." |
| Bound reached | (Q9) | "This Space has 100 Memberships, the maximum." is the exemplar; no string exists for the Sessions bound |
| Rate limited | 429 | API only; no browser surface (`EXPERIENCE.md:277`) |

`EXPERIENCE.md:101-147`; voice table — never "You don't have permission to view this Task." (object-shaped). The SPEC's own copy (`acceptance-criteria.md:161`, "this isn't available to you; it may not exist, or you may not have access.") **disagrees** with the UX spine and with the epic's AC; use "Not available." (question 14). The SPEC also offers "create a Space" where the caller holds no Membership anywhere (`:162`) — out of scope until 3.1.

### Test design

P0: I-1, I-2, I-4, I-5, I-6, I-9, I-13, A-4, A-7, P-5; P1: A-12, C-7 (`test-design-qa.md:262-337`). NFR rows: observability (`CrossSpace`/`InsufficientRole`, 90 days, purge ≤ daily) and scalability (one refusal case per bound, in-transaction; registry completeness — "Active Session UNDEFINED") are both P0 (`:197-209`). **Do not populate `Yello.Tests.Isolation`**: AD-4 needs both surfaces, the Token surface is 1.8, and story 1.9 owns that suite (adding one case also means removing `--ignore-exit-code 8`, gated both ways). 1.6's I-series cases land in `Yello.Tests.Slices`; story 1.9 lifts them onto both surfaces.

**Timing — question 7.** AD-3 and story 1.9 require a boundary 404 and an **in-Space** not-found to be indistinguishable in duration, but one writes a record and the other does not. No in-Space resource lookup exists yet (no Task). This story times the two **boundary** causes, which do identical work, and ledgers the boundary-vs-in-Space comparison for the first story with an in-Space lookup (2.2) and for 1.9. The `TESTING-CONVENTIONS.md:235-258` method (21/median/20%) is the one to use; `test-design-architecture.md:428-434`'s "N ≥ 100, distribution comparison" was superseded by it — do not improvise a second method.

### Previous story intelligence (1.5)

- Set-then-verify; one `sp_set_session_context` call site (`SpaceSessionContext`), gated by call-site count **and** location; `@read_only = 1` refuses a second set (**15664**) and the pool clears it — both verified empirically.
- B2 seeding: `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceSeeding.cs` seeds per Space under a switched context and can seed any Role — use it for the four-Role matrix cases and the 51-Account bound case.
- Review lessons: gates asserted less than their names (`!=` filters passed; FKs not paired; raw-SQL list incomplete; file count instead of call sites). **Write each gate so its name is exactly what it checks, and plant it.** Assertions must not compare a value with a default — use two Spaces with different ids in every isolation-flavoured assertion. Anchor every refusal assertion on a success first (a request to a non-existent route also returns 404 and would pass for the wrong reason).
- The unit of work rolls back on any ≥400 result and refuses an unmaterialised result.
- A full run now starts **twelve** SQL Server engines (~2 GB each); the browser suite starves under it. Each new container-backed class adds one. Prefer extending existing container-backed classes; keep database-free classes fixture-free. If the full run misbehaves, check memory first. Rancher Desktop recovery: `rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch. A stopped runtime produces **failures**, not skips. `--no-build` runs zero tests.

### Git intelligence

Recent: `54d50a9`/`6a3356f` (story-cycle skill), `c3867aa` (1.5 review: rollback on ≥400, materialisation, gates tightened, A-12 for resolution), `fe5d86d` (1.5: 53 files — resolver, `SpaceScope`, `SpaceSessionContext`, migration `20260925132825_ActiveSpaceResolution`, statuses route, gates). Commit discipline: thesis-sentence subject, no prefix; one commit per phase; straight to `main`. The orchestrator commits — the dev phase does not.

### Latest technical notes

- **Rate limiting** is in the ASP.NET Core shared framework (`Microsoft.AspNetCore.RateLimiting`, `System.Threading.RateLimiting`) since .NET 7: `AddRateLimiter(o => …)`, `o.RejectionStatusCode = 429`, `o.OnRejected` to write the problem body and `Retry-After` (from `RateLimitMetadataName.RetryAfter` lease metadata), `PartitionedRateLimiter.Create`, `RateLimitPartition.GetConcurrencyLimiter`/`GetFixedWindowLimiter`, `app.UseRateLimiter()` (must run after routing for endpoint policies; minimal APIs with implicit routing — place it after `UseAuthentication` and before the endpoints execute), `RequireRateLimiting("name")` on the endpoint. Confirm against the .NET 10 docs before relying on a member name; this project has twice shipped a plausible-but-wrong platform claim.
- **Problem details**: `Results.Problem(...)` today produces bodies identical across requests (verified by 1.5's raw comparison). `AddProblemDetails()` + `IProblemDetailsService` may add `traceId`; verify before adopting (Task 5).
- **SQL Server**: no `MERGE` needed for the presence upsert — an `UPDATE … ; IF @@ROWCOUNT = 0 INSERT` inside the transaction, or EF tracked upsert, under the Space's session context (the BLOCK predicate requires it). Keep `MAXDOP = 1` untouched.

### Project structure notes

**New, most likely:** `Yello.Domain/Memberships/{Capability,RoleCapabilityMatrix}.cs`; `Yello.Domain/Bounds/BoundRegistry.cs`; `Yello.Domain/AccessRefusals/AccessRefusal.cs` (+ kind enum); `Yello.Application/Spaces/IAccessRefusalRecorder.cs` (port); `Yello.Infrastructure/Persistence/{AccessRefusalRecorder,AccessRefusalPurge,…Presence…}.cs`, configurations, one migration; `Yello.Host/Spaces/{RequiredCapability,…}.cs`; client refusal page + resx keys; tests in `Yello.Tests.Architecture` (gates, matrix-as-data, client logic) and `Yello.Tests.Slices/Spaces/…` (wire, recording, bound, duration, purge).
**Vocabulary** (PRD §2): `Account`, `Session`, `Space`, `Membership`, `Role`, `Owner`/`Admin`/`Member`/`Viewer`, `User` (an Account acting in a Space). **Never** `Tenant`, `Workspace`, `Org`, `Team`, or `User` for `Account`. The word `Todo` cannot appear in a comment (S1135).
**Traps** (1.5 Dev Notes `:386-405`, still current): `S2339` no `public const`; `S109` magic numbers → named consts (the six bound values); `S4457` split argument checks; `MA0182` reflection-only types; banned `DateTimeOffset.Now`; hand-maintained exact-set gates (`PackageVersionPinTests`, `AllowedReferenceEdges`, `MsBuildEvaluation.PropertyNames`, `LocalisationGateTests` — a new failure-code class is invisible until it gets its own test and fields list; every public static code needs a `ClientCopy.resx` entry). Warnings are errors.

### Testing requirements

Traits: `Suite`, `Priority`, `Requirement` (cite AR **and** AD, e.g. `AR-11`/`AD-20`), `Assumption` (`PRD-12-11` for retention; `PRD-12-8` for anything fixing the route shape). One behaviour per test, sentence-named. Randomised emails, never a shared literal. No `Task.Delay` — inject `TimeProvider` (registered `Program.cs:120`). Cleanup by container disposal, never delete statements. `IClassFixture<MigratedDatabaseFixture>`, each test opens with `Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason)`. Wire tests boot the real Host via `HostProcess.RunAsync` over HTTPS with `WireSession` (cookies by hand; re-fetch the anti-forgery token after sign-in). xunit.v3 `Assert` only. Baseline: **274 total / 273 passed / 1 skipped** at 1.5's close.

### References

- Story and AC — `epics.md:728-790`; coverage `epics.md:349`; epic notes `:421`, `:427`; UX-DR32/35/37 `:276`, `:279`, `:281`
- AR-3, AR-9…AR-11, AR-25, AR-30, AR-33, AR-34 — `epics.md:191-224`
- Spine — AD-1 `:71-75`, AD-3 `:88-94`, AD-10 `:136`, AD-18, AD-20 `:198`, AD-24 `:218-222`, AD-25 `:228`, AD-26 `:234`, conventions `:266-276`, ER `:377`; `.memlog.md:23`, `:68`
- PRD — FR-2 `prd.md:150`, FR-15/16 `:321-354`, NFR-7 `:730-734`, NFR-8 `:747`, §12 assumption 11 `:902`; SPEC `role-capability-matrix.md`, `acceptance-criteria.md:154-162`, `SPEC.md:277`
- UX — `EXPERIENCE.md:101-147`, `:203`, `:262`, `:276-279`, `:396-405`; `DESIGN.md:501`
- Tests — `test-design-qa.md:96`, `:197-209`, `:262-337`, `:480`; `test-design-architecture.md:110-113`, `:175`, `:212`, `:424-438`; handoff `:88`; `tests/TESTING-CONVENTIONS.md` whole, esp. `:34-42`, `:86-118`, `:197-267`, `:269-282`, `:284-364`
- Readiness `implementation-readiness-report-2026-08-22.md:964-968`, `:1070-1090`. Sprint change proposal 2026-09-13: nothing touches 1.6.
- Ledger — `deferred-work.md` sections for 1.3 implementation (idempotency), 1.4 implementation (Sessions bound), 1.3 review (rate limit), 1.5 implementation and review (non-GUID 404, A-12, ordering, group mapping, `DbContext` reuse)
- Previous story — `1-5-resolve-the-active-space-per-request-enforced-in-the-database.md`

### Open questions — each with a recommended decision

The dev phase takes these as **reversible, recorded decisions** in the Completion Notes. None blocks starting.

1. **What "concurrent active Sessions per Space" counts.** Undefined in every document (`test-design-qa.md:122,204,313`). Options: (a) distinct Accounts with a live presence row in the Space, written at resolution, expiry on read; (b) WebSocket connections (the architect's memlog reading, `.memlog.md:23`) — but epic 7 gives sync admission to the editors-per-Task bound; (c) defer the enforcement. **Recommend (a)** with a stated window (suggest 15 minutes, matching the database auto-pause horizon), counting Accounts rather than cookies.
2. **Where `AccessRefusal` lives.** (a) A non-Space-scoped table with `TargetSpaceId`, no FK, no RLS, write-only from the pipeline, purge in Infrastructure; (b) Space-scoped under RLS, writing under the target's session context on a fresh connection — but the cross-Space purge then cannot see rows under any single context. **Recommend (a)**, recording the variance from the spine's ER.
3. **Purge trigger.** (a) Request-piggybacked, at most once per 24h per process; (b) a daily timer — wakes an idle database, contradicting 1.10's AC; (c) at startup only. **Recommend (a)**, with (c) as an additional trigger only if it adds no database wake beyond the existing connectivity check.
4. **How a 403 is exercised when no endpoint requires more than read.** (a) Matrix as data + the real pipeline invoked directly with a fake endpoint over a seeded database; (b) a test-only product endpoint; (c) defer wire 403s to 2.1. **Recommend (a)**, plus a wire-level 403 the first time a write endpoint exists (ledger to 2.1).
5. **Which matrix wins** — SPEC companion vs PRD. **Recommend the SPEC companion**, with the Offer-acceptance row excluded from the Role check per AD-26.
6. **One 404 type for every not-found.** **Recommend** replacing `space-not-found` with `not-available` for all 404s and a neutral title, so a future in-Space not-found cannot differ in body.
7. **Timing pair and record-write placement.** **Recommend** a synchronous record write, time the two boundary causes here, ledger boundary-vs-in-Space to 2.2 / 1.9.
8. **Non-GUID `{spaceId}`.** **Recommend** keeping `:guid` and giving unmatched `/api` requests the same problem body, with no record.
9. **Bound refusal status.** 403 and 404 are reserved by AD-3. **Recommend 409** with `bound-reached`, `bound` and `limit` extensions.
10. **Idempotency (AR-25).** Not in 1.6's coverage row; no Space-scoped state-changing endpoint exists; registration's replay is already indistinguishable. **Recommend deferring** to story 2.1 (the first `POST` returning an id), re-owning the ledger entry, and leaving A-12's idempotency clause open.
11. **Filter order for an unauthenticated Space-scoped write.** **Recommend** 401 before anti-forgery for `{spaceId}` routes.
12. **Rate-limit shape and numbers.** **Recommend** a global concurrency limiter over both PBKDF2 endpoints (≈ processor count permits, queue ≤ 2× that) plus a per-IP fixed window (suggest 10 per minute), configurable, logged unconditionally; partition never on the address.
13. **Route back without a switcher.** **Recommend** a link to the authenticated home route that 1.7 repoints; ledger the switcher-sourced clause to 1.7.
14. **Copy conflict** — SPEC "this isn't available to you…" vs UX/epics "Not available.". **Recommend "Not available."** (the AC's own source and UX-DR32); flag the SPEC line as a restatement defect for Lee.

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-1-6-refuse-at-the-space-boundary-and-record-every-refusal.md` (scenario ids, seed-data constraints checked, plant results, what was deliberately not scaffolded and why).

Story 1.6's types (`Capability`, `RoleCapabilityMatrix`, `RequiredCapability`, `BoundRegistry`, `IAccessRefusalRecorder`, `AccessRefusal`) are reached **by name, through reflection**, so the scaffolds compile before the types exist. Tables and columns (`AccessRefusal`: `Id, AccountId, TargetSpaceId, Capability, Outcome, Kind, OccurredAt`; `SpacePresence`: `SpaceId, AccountId, ExpiresAt`), the 15 capability names, the six bound/operation names, `RequiredCapability` and the rate-limit key `RateLimiting__PermitsPerWindow` are the scaffolds' contract. Where dev-story chooses a different name, correct the scaffold. On activation, replace each by-name lookup with the typed reference.

| File | Cases | ACs | Green today? |
|---|---|---|---|
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs` (new, no container) | 10 | AC3, AC4, AC5, AC6, AC8, AC9; A-4, A-7, A-9, A-12 | **2 active**: `No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity` (A-4) and `No_production_code_runs_on_a_timer_or_as_a_hosted_service` (A-9). Both are standing gates, both planted on 2026-10-24 and caught. 8 skipped |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.cs` (existing class, +8; now `partial`) | 8 | AC1, AC4, AC6, AC7 (boundary 404 body, `CrossSpace` record, 401 not recorded, non-GUID 404, 51st Account, present Account, expired presence, 90-day purge) | all skipped |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs` (new container class, +1 engine on activation) | 5, one a 20-row theory | AC2, AC3, AC4, AC10 (Role matrix through the real filter, non-member 404 for every capability, `InsufficientRole` record, duration pair, planted oracle) | all skipped |
| `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/SessionEndpointTests.cs` (existing class, +1) | 1 | Task 9 rate limit (uniform 429, `Retry-After`, partition not on the address) | skipped |

**Activation order:**
1. `SpaceBoundaryPipelineTests.The_duration_method_tells_a_refusal_that_skips_its_record_from_one_that_writes_it` goes first. It tests the riskiest assumption: that the B3 method, built for a ~270 ms effect, can see a skipped single-row insert. If it cannot, AC10's oracle clause needs a decision before anything is built on it. Then activate its pair, `A_missing_Space_and_a_Space_without_Membership_take_the_same_time_to_refuse`.
2. The record write: `Each_boundary_404_records_one_CrossSpace_refusal_and_a_served_request_records_none`, `A_request_refused_at_authentication_records_no_refusal`, `A_403_records_one_InsufficientRole_refusal`.
3. The matrix: `The_capability_matrix_agrees_with_the_transcription_in_every_cell` and `The_capability_vocabulary_is_exactly_the_Role_decided_rows`. Then the pipeline: `The_matrix_decides_whether_a_member_reaches_the_endpoint` and `Every_capability_is_refused_with_the_boundary_404_for_a_non_member`. Then `Every_spaceId_endpoint_declares_exactly_one_capability` and `Only_the_pipeline_depends_on_the_capability_matrix_and_the_refusal_recorder`.
4. The contract: `The_refusal_problem_types_are_declared_and_one_type_serves_every_404`, `Both_boundary_404s_answer_not_available_and_carry_nothing_beyond_the_URL` and `A_non_GUID_spaceId_is_answered_as_a_boundary_404_is_and_records_nothing`. Removing `ProblemTypes.SpaceNotFound` breaks the 1.5 case that asserts it. Update that case in the same change.
5. The bound: `The_bound_registry_declares_all_six_bounds_with_their_values_and_operations`, then the three presence cases.
6. Retention: `Refusal_records_past_90_days_are_purged_by_request_traffic_and_younger_ones_kept` and `Only_the_recorder_and_the_purge_refer_to_the_refusal_record`. Also write the "at most daily" unit case with a hand-written `TimeProvider`.
7. Rate limit: `Sign_in_past_the_limit_is_refused_with_429_alike_for_a_known_and_an_unknown_address`. Raise the limit for every other wire test first. `SessionEndpointTests.The_two_failure_causes_take_the_same_time_over_the_wire` signs in 44 times on one Host from one IP.
8. Client copy: `The_refusal_copy_is_the_clients_own_and_names_no_remembered_location`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (`claude-opus-5-5`), dev-story run unattended by the story-cycle orchestrator, 2026-10-24.

### Debug Log References

- **AC10 oracle activated first, as directed.** Red: the fixture threw `PendingModelChangesWarning` (no `AccessRefusal` table yet). Green after the recorder and migration. Measured over five isolated runs: the oracle's arms differed by **44-48%** (6.4-6.5 ms vs 3.4-3.6 ms) against the 20% tolerance; the real pair by **1-4%**. The B3 method sees a skipped single-row insert because the insert is about half a boundary refusal's cost. **No blocker.**
- **Activation order** followed the ATDD checklist. The oracle was the only scaffold confirmed red before its implementation existed. The others were activated after their implementation had landed, so their red evidence is the plant table below.
- **The plant run was stopped by the harness for low system memory** (Claude Code reaps idle background shells under memory pressure, with an instruction not to restart them unasked). Three plants finished. The fourth, P7 (CrossSpace record skipped), was mid-run, and **its edit to `ActiveSpaceResolution.cs` survived the kill**: the runner's `finally` never ran. Found by comparing file hashes against a pre-run snapshot. Restored by hand from known content and confirmed byte-identical to the snapshot (md5 `5c78ed96…`). No other file differed, and no planted file was left behind. A runner bug was also found: a plant whose build failed (P3, an `internal` helper the analysers refused) was not written to the results file. Both P3 and the bug are fixed in the runner. The remaining plants were re-run later in the foreground, with Lee's approval relayed by the orchestrator; see the plant table.
- **Full suite run project by project in the foreground,** not as one `dotnet test Yello.slnx`, because of memory pressure. It was run once after the reap and again after the plants. Results are under Completion Notes.
- **Scaffold corrections: none for wrong assertions.** As the checklist directed, every by-name reflection lookup became a typed reference (`SpaceBoundaryPipelineTests`, `RefusalBoundaryGateTests`), and the transcription cells became `CapabilityDecision` values. Every name the scaffolds guessed matched: `RequiredCapability`, the 15 capabilities, the 6 bounds and operations, `TargetSpaceId`, `SpacePresence`, and `RateLimiting__PermitsPerWindow`. One gate was **extended** rather than corrected: A-12 now also covers the presence-bound port `ISpacePresenceRegister`. One 1.5 assertion moved from `SpaceNotFound` to `NotAvailable`, as the checklist predicted. `HostProcess` now raises the rate limit to 1,000,000 by default, so other wire tests are unaffected. Stale "red-phase / skipped" remarks were rewritten.
- Analyser fixes on the way: MA0211, MA0181, S4040 (pragma: lower case is the code's form), S109, S1200 (registrations split into `SpaceBoundaryLimits`; pipeline helpers into `BoundaryProblems`, `UnitOfWorkResults`, `RequiredCapabilityExtensions.Of`), S2221, S2302, S8969/CS8602.
- Migration `20261024121107_SpaceBoundaryRefusals`: scaffolded with the pinned `dotnet-ef` 10.0.11, then given the hand-editing pass (file-scoped namespace, real docs, no auto-generated marker on the migration, literal names). It extends `SpaceIsolationPolicy` with FILTER and BLOCK AFTER INSERT predicates on `SpacePresence`.
- Temporary files `C:\tmp\b1.txt` and `C:\tmp\b2.txt` were created by mistake outside the scratchpad and deleted.

#### Planted violations

Every plant was run in the foreground, one at a time: one build plus one targeted test. The original was held in memory and restored in a `finally`. After each plant, md5 sums of every `.cs`, `.razor` and `.resx` under the production and test projects were compared with a snapshot taken before the run: identical every time. No git command was used to restore anything.

The first run, on 2026-10-24, was stopped by the harness for low memory after P1, P2 and P4. Its leftover P7 edit was restored by hand, and the rest were re-run with Lee's approval relayed by the orchestrator. Five plants failed to *compile* on their first attempt because of the analysers: P3 and P9 (S4261 and access modifiers), P5 (an unused `using` once the declaration was removed), P6 (an unused constant) and P16 (S109). Each was reshaped to plant the same violation in a form that compiles, then run.

| # | Plant | Caught by | Result |
|---|---|---|---|
| P1 | 403 converted to 404 in the pipeline | `SpaceBoundaryPipelineTests.The_matrix_decides_whether_a_member_reaches_the_endpoint` | caught (8 of 20 rows) |
| P2 | 404 body carries an id beyond the URL (`detail` = Account id) | `ActiveSpaceEndpointTests.Both_boundary_404s_answer_not_available_and_carry_nothing_beyond_the_URL` | caught |
| P3 | an Application type reads `ActiveSpaceContext.Role` | `RefusalBoundaryGateTests.Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` | caught |
| P4 | `{spaceId}` route mapped on `app`, around the group | Host startup check; `ActiveSpaceEndpointTests.A_member_is_served_and_a_non_member_is_refused_exactly_as_a_missing_Space_is` | caught (Host refused to start) |
| P5 | the statuses endpoint declares two capabilities | `RefusalBoundaryGateTests.Every_spaceId_endpoint_declares_exactly_one_capability` | caught. The zero-capability case is held by `SpaceBoundaryRouteTests.A_spaceId_route_outside_the_resolving_group_or_without_a_capability_is_reported` |
| P6 | registry with five bounds | `RefusalBoundaryGateTests.The_bound_registry_declares_all_six_bounds_with_their_values_and_operations` | caught |
| P7 | boundary 404 skips the `CrossSpace` record | `ActiveSpaceEndpointTests.Each_boundary_404_records_one_CrossSpace_refusal_and_a_served_request_records_none` | caught |
| P8 | 403 skips the `InsufficientRole` record | `SpaceBoundaryPipelineTests.A_403_records_one_InsufficientRole_refusal` | caught |
| P9 | an Application type calls `IAccessRefusalRecorder` | `RefusalBoundaryGateTests.Only_the_pipeline_depends_on_the_capability_matrix_and_the_refusal_recorder` | caught |
| P10 | a Host type reads `AccessRefusal` | `RefusalBoundaryGateTests.Only_the_recorder_and_the_purge_refer_to_the_refusal_record` | caught |
| P11 | a `PeriodicTimer` in Infrastructure | `RefusalBoundaryGateTests.No_production_code_runs_on_a_timer_or_as_a_hosted_service` | caught |
| P12 | purge interval one hour, not one day | `SpaceBoundaryRouteTests.The_refusal_purge_reaches_for_the_database_at_most_once_a_day` | caught |
| P13 | rate-limit partition keyed per submission | `SessionEndpointTests.Sign_in_past_the_limit_is_refused_with_429_alike_for_a_known_and_an_unknown_address` | caught |
| P14 | client branches on the not-found cause (unroutable vs `not-available`) | `RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal` | caught |
| P15 | anti-forgery filter before the 401 filter | `SpaceBoundaryRouteTests.An_unauthenticated_state_change_to_a_Space_scoped_route_is_answered_401_before_anti_forgery` | caught |
| P16 | presence bound never refuses (`live > 2 x limit`) | `ActiveSpaceEndpointTests.The_51st_distinct_Account_in_a_Space_is_refused_with_the_bound` | caught |
| P17 | matrix cell flipped (Viewer may manage Tasks) | `RefusalBoundaryGateTests.The_capability_matrix_agrees_with_the_transcription_in_every_cell` | caught |
| P18 | the `/api` fallback removed | `ActiveSpaceEndpointTests.A_non_GUID_spaceId_is_answered_as_a_boundary_404_is_and_records_nothing` | caught |
| P19 | 403 record written fire-and-forget, after the response | `SpaceBoundaryPipelineTests.A_403_records_one_InsufficientRole_refusal` | caught. This catch depends on timing: the record races the read, so a slower read could miss it. Recorded as a weak plant, not as a gate guarantee. |
| AC10 | one arm skips the record (permanent oracle) | `SpaceBoundaryPipelineTests.The_duration_method_tells_a_refusal_that_skips_its_record_from_one_that_writes_it` | caught, 44-48% vs 20% |
| ATDD | bare statuses route; `PeriodicTimer` | A-4, A-9 | caught (ATDD run) |

**19 of 19 caught. No gate needed fixing.**

### Completion Notes List

**Implemented.** Each item below is in place and tested, and every planted violation was caught (table above).

- **Capability vocabulary and matrix as data:** `Capability` (15 Role-decided rows), `CapabilityDecision`, `RoleCapabilityMatrix.Decide`.
- **Declared capability, checked by the pipeline:** `RequiresCapability` metadata. `ActiveSpaceResolution` checks it after resolution and before the endpoint, and answers `403 insufficient-role` with a `capability` code. It fails closed with no declaration, and the Host refuses to start with an undeclared `{spaceId}` route.
- **The refusal record:** `AccessRefusal`, written for every boundary 404 (`CrossSpace`) and every 403 (`InsufficientRole`). It is written synchronously, on its own context, after rollback.
- **Retention:** 90-day purge, triggered by request traffic, at most once a day per process, off the request path.
- **The registry and the one bound this story enforces:** `BoundRegistry` holds all six NFR-8 bounds. The concurrent-active-Sessions bound is enforced through `SpacePresence`, inside the resolution transaction, refusing with `409 bound-reached`.
- **One 404 for everything:** `not-available` for every 404, including unmatched `/api` paths.
- **Filter order:** 401 before anti-forgery on Space-scoped routes.
- **Rate limit:** on both PBKDF2 endpoints, with a uniform 429.
- **The browser refusal page:** one page for every not-found, the client's own copy, and the unknown-route page converged onto it.
- **Housekeeping:** comments that predicted this story were rewritten, and the ledger was updated.

**Tests.** Build: 0 warnings, 0 errors. Run project by project in the foreground: Architecture 144/144; Slices 172 (171 passed, 0 failed, 1 skipped); Client 8/8; Isolation, Revocation and Merge 0. **Total 324: 323 passed, 0 failed, 1 skipped.** The baseline was 298 total (275 passed, 23 skipped): the 22 red scaffolds now pass, the theory counts as 20, and new cases were added. A combined `dotnet test Yello.slnx` was not run because of memory pressure. Its suite now starts 13 SQL Server engines (+1, `SpaceBoundaryPipelineTests`).

**Variance from the spine:** the ER (`ARCHITECTURE-SPINE.md:377`) shows `SPACE ||--o{ ACCESS_REFUSAL`. This story built `AccessRefusal` with `TargetSpaceId`, no FK to Space and no FK to Account, and no RLS or query filter.

**Decisions** (each reversible; cut any line to revisit it):

- Q1: "Active Session in a Space" means a distinct Account with a live `SpacePresence` row, on a 15-minute window, rewritten only once less than half the window remains. Accounts are counted, not cookies. Any 4xx from the endpoint rolls back that request's presence upsert.
- Q2: `AccessRefusal` is not Space-scoped: `TargetSpaceId`, no FKs, no RLS, write-only. A gate allows only the recorder, the purge, the context and its configuration to refer to it.
- Q3: The purge runs on request traffic, at most once per 24 hours per process, and is not awaited by the request. There is no startup trigger.
- Q4: The matrix is proved as data and through the real filter with seeded Memberships. The over-the-wire 403 is ledgered to story 2.1.
- Q5: The SPEC companion matrix wins. The Ownership-Offer acceptance row is absent from `Capability` (AD-26).
- Q6: `not-available` replaces `space-not-found` for every 404, with the neutral title "Not available.".
- Q7: The record is written synchronously. The two boundary causes are timed. The boundary vs in-Space comparison is ledgered to 2.2 and 1.9.
- Q8: `:guid` stays. An `/api/{**path}` fallback answers `not-available` and records nothing. Side effect: a POST to a GET-only route answers 404, not 405 (ledgered to 2.1).
- Q9: A bound refusal is `409 bound-reached`, with `bound` (kebab code) and `limit`.
- Q10: Idempotency is deferred to 2.1, and the ledger entry is re-owned. A-12's idempotency clause stays open.
- Q11: A 401 comes before anti-forgery on `{spaceId}` routes (`RequireAccountAsync` first in `ApiRouteGroup`).
- Q12: The rate limit is a concurrency limiter (ProcessorCount permits, queue of 2×) plus a 10-per-minute window per client IP. The key is `RateLimiting:PermitsPerWindow`, logged at startup. Behind a proxy the per-client window is ledgered to 1.10.
- Q13: The route back is `ClientRoutes.Home` ("/"). The switcher-sourced clause is ledgered to 1.7.
- Q14: The copy is "Not available.". The SPEC line at `acceptance-criteria.md:161` is flagged as a restatement defect and ledgered to 1.7.
- Wire codes are the kebab-case form of enum names (`StableCode`) and are used directly as client resource keys. A test fails the build on any capability or bound code without copy.
- `ProblemResponse` gained optional `Capability`, `Bound` and `Limit`.
- The 403 copy names the Roles the matrix denies, for example "Viewers cannot edit Tasks." or "Only Owners and Admins can manage Labels.". No Role travels on the wire.
- Known limit: two newcomers racing at the presence bound can both be admitted. Ledgered to 2.9.

### File List

**Added:**

- `Yello.Domain/StableCode.cs`
- `Yello.Domain/Memberships/Capability.cs`
- `Yello.Domain/Memberships/CapabilityDecision.cs`
- `Yello.Domain/Memberships/RoleCapabilityMatrix.cs`
- `Yello.Domain/Bounds/BoundRegistry.cs`
- `Yello.Domain/AccessRefusals/AccessRefusal.cs`
- `Yello.Domain/AccessRefusals/AccessRefusalKind.cs`
- `Yello.Domain/Spaces/SpacePresence.cs`
- `Yello.Application/Spaces/IAccessRefusalRecorder.cs`
- `Yello.Application/Spaces/ISpacePresenceRegister.cs`
- `Yello.Application/Spaces/IRefusalRetention.cs`
- `Yello.Infrastructure/SpaceBoundaryLimits.cs`
- `Yello.Infrastructure/Persistence/AccessRefusalRecorder.cs`
- `Yello.Infrastructure/Persistence/AccessRefusalPurge.cs`
- `Yello.Infrastructure/Persistence/RefusalPurgeSchedule.cs`
- `Yello.Infrastructure/Persistence/SpacePresenceRegister.cs`
- `Yello.Infrastructure/Persistence/Configurations/AccessRefusalConfiguration.cs`
- `Yello.Infrastructure/Persistence/Configurations/SpacePresenceConfiguration.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261024121107_SpaceBoundaryRefusals.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261024121107_SpaceBoundaryRefusals.Designer.cs`
- `Yello.Host/ApiRouteGroup.cs`
- `Yello.Host/RateLimitLog.cs`
- `Yello.Host/Endpoints/UnmatchedApiEndpoint.cs`
- `Yello.Host/Sessions/CredentialHashingRateLimit.cs`
- `Yello.Host/Spaces/BoundaryProblems.cs`
- `Yello.Host/Spaces/RefusalRetentionTrigger.cs`
- `Yello.Host/Spaces/RequiredCapability.cs`
- `Yello.Host/Spaces/SpaceScopedRouteCheck.cs`
- `Yello.Host/Spaces/UnitOfWorkResults.cs`
- `Yello.Client/Components/RefusalSurface.razor`
- `Yello.Client/Pages/NotAvailable.razor`
- `Yello.Client/Refusals/RefusalCopy.cs`
- `tests/Yello.Tests.Architecture/RefusalCopyTests.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryRouteTests.cs`

**Modified:**

- `Yello.Application/AssemblyMarker.cs`
- `Yello.Application/Accounts/AuthenticateAccount/AuthenticateAccountHandler.cs`
- `Yello.Application/Accounts/RegisterAccount/RegisterAccountHandler.cs`
- `Yello.Application/Statuses/ListStatuses/ListStatusesHandler.cs`
- `Yello.Infrastructure/InfrastructureServices.cs`
- `Yello.Infrastructure/Persistence/SchemaNames.cs`
- `Yello.Infrastructure/Persistence/YelloDbContext.cs`
- `Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs`
- `Yello.Contracts/ProblemTypes.cs`
- `Yello.Contracts/ProblemResponse.cs`
- `Yello.Host/Program.cs`
- `Yello.Host/SpaceLog.cs`
- `Yello.Host/Endpoints/SpaceStatusEndpoints.cs`
- `Yello.Host/Sessions/AntiforgeryRequirement.cs`
- `Yello.Host/Spaces/ActiveSpaceResolution.cs`
- `Yello.Client/App.razor`
- `Yello.Client/Pages/ClientRoutes.cs`
- `Yello.Client/Localisation/ClientCopy.resx`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs`
- `tests/Yello.Tests.Architecture/SpaceResolutionSeamTests.cs`
- `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/SessionEndpointTests.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/HostProcess.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceUnitOfWorkTests.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/1-6-refuse-at-the-space-boundary-and-record-every-refusal.md`

### Change Log

- 2026-10-24: implemented story 1.6. Added the refusal contract, the capability matrix, the `AccessRefusal` record and its purge, the bound registry and the presence bound, and the rate limit. Activated all 24 ATDD scaffolds. 2026-10-24: planted violations P3 and P5-P19 run in the foreground; all 19 caught. Status set to review.
