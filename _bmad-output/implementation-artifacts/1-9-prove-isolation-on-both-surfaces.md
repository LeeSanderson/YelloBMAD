# Story 1.9: Prove isolation on both surfaces

Status: ready-for-dev

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-9-prove-isolation-on-both-surfaces`
Requirements owned (coverage row `epics.md:352`): **AR-35** (`epics.md:223`, four suites gate release; isolation runs every case on both surfaces), **NFR-1** (`prd.md:683-690`), the **SM-1 gate** (`prd.md:854`, `SPEC.md:260`). Also **AD-3** (`ARCHITECTURE-SPINE.md:88-94`, the timing case), **AD-4** (`:96-100`, a one-surface case is a gap), **AD-2** (`:77-86`, the pooled-connection reuse case), **AD-20** / **NFR-7** (`:194-198`, `prd.md:730-734`, `CrossSpace` vs `InsufficientRole`), **AR-7** (`epics.md:195`), **AR-10** (`epics.md:198`), **UX-DR31** (`epics.md:275`, `EXPERIENCE.md:158-168, 279`), FR-15 / FR-35 / FR-36 as SM-1 validates them.
Test-design items this story lands (`test-design-qa.md`, `test-design-architecture.md`): **B6** (the case list held as data, surface cross-product generated; `arch:123-126`, R17 `:231`, T6 `:313`), the **two-Space fixture** and **surface parameterisation** (`qa:129-147`), **I-1, I-3, I-4, I-5, I-6, I-8, I-9, I-10, I-11, I-12, I-13** as far as epic 1's routes carry them (`qa:265-277`), **I-17** (`qa:350`), **V-3** (`qa:282`), improvement **#2** (pooled-connection collection, `arch:329-337`) and **#3** (`/sync` as a third surface, `arch:339-347`), the shared-container contingency (`arch:604-609`).
Ledger entries assigned to this story (`deferred-work.md`), every one folded in below or explicitly re-owned — see "Ledger entries owned by 1.9" in Dev Notes.
Depends on: **1.3–1.8**, all done. Story 1.10 depends only on 1.1 (`implementation-readiness-report-2026-08-22.md:963`).
Extended later, not here: **3.4** (SM-1 extension: one Account, two Spaces), **8.1** (FR-35 parity sweep), **2.1/2.2** (the first wire 403 and the first in-Space lookup), **7.3/7.8** (the sync channel), **1.10** (CI that runs this on every push).

**Settled before this story (Lee, 2026-10-24):** (1) sign-out ends Sessions server-side through `Account.SessionsValidAfter`; an invalidated credential grants access to nothing. (2) A Token on `DELETE /api/v1/sessions` is treated as no credential (`400 antiforgery-token-missing`, nothing stamped). Both are expectations in the case list below, not questions.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the operator deciding whether Yello can ship,
I want an isolation suite exercising every case on the browser and the API alike,
So that SM-1's zero-disclosure gate is measured rather than believed.

## Acceptance Criteria

AC1–AC7 are reproduced **verbatim** from `epics.md:885-915` (`status: final`), numbered by position. AC8–AC12 are carried from the test design and the ledger, and say where from.

**AC1 — every case on both surfaces; a one-surface case is a gap**
**Given** the isolation suite
**When** it runs
**Then** every case executes against both the browser surface and the API surface
**And** a case existing for one surface and not the other fails the suite as a gap rather than passing as a choice

**AC2 — two Accounts, five routes, one answer**
**Given** two Accounts each holding their own Personal Space
**When** each attempts to reach the other's Space by direct identifier, by deep link, by listing, by aggregate and by API
**Then** every attempt returns the boundary 404, and no data and no existence is disclosed
**And** possessing an identifier confers nothing

**AC3 — duration as well as body**
**Given** a Space-boundary 404 and an in-Space not-found
**When** both are produced
**Then** they are compared for **duration** as well as for body, and are indistinguishable in both

**AC4 — pooled-connection reuse**
**Given** the pooled-connection reuse case
**When** two requests for different Spaces are served consecutively on one physical connection
**Then** the second discloses nothing belonging to the first

**AC5 — runs on every change; one disclosure fails the build**
**Given** the suite
**When** any change is pushed
**Then** it runs on that change, and a single verified cross-Space disclosure fails the build
**And** this is the one requirement with no acceptable failure rate

**AC6 — the refusal record tells the two kinds apart**
**Given** an authorisation refusal record
**When** it is inspected
**Then** a cross-Space attempt is distinguishable from a within-Space permission failure, because the two mean very different things

**AC7 — the client renders nothing it was not just told**
**Given** the client
**When** a Space-scoped surface first loads
**Then** nothing renders from cache before the server answers, no request is issued for a Space id absent from the current switcher response, and nothing is prefetched across a Space boundary
**And** the context bar may render its shell but never the Space name, which cannot come from a 404 and so would have to be sourced from cache

**AC8 — the case list is data, and complete against the route table** *(carried from B6, R17, T6, `test-design-architecture.md:123-126, 231, 313`)*
**Given** the isolation case list
**When** the suite is built
**Then** it is a single declared list of cases, each naming an expected outcome for **every** surface, and the surface cross-product is generated from it rather than written by hand
**And** every Space-scoped and Account-scoped route the Host maps is named by at least one case, so a route added without an isolation case fails the suite

**AC9 — revocation on both surfaces, request path** *(carried from V-3 `test-design-qa.md:282`, NFR-2's request-path clause `prd.md:696-700`, `deferred-work.md:364`)*
**Given** a Session and a Token for the same Account and Space
**When** the credential is ended (sign-out; Token revocation), the Membership is removed, the Space is deleted, the Account is deleted, or the Role changes
**Then** the very next request on each surface reflects it — refused, or served at the new Role — with no tolerance, in `Yello.Tests.Revocation`

**AC10 — no secret in any log, on any case** *(carried from I-17 `test-design-qa.md:350`, NFR-6, AR-34, `deferred-work.md:378`)*
**Given** any case in the Isolation or Revocation suite
**When** the Host's output for that case is inspected
**Then** it contains no password, no Token plaintext, no session-cookie value and no Space name — `SpaceId` as a field only (spine `:274`)

**AC11 — the real browser against the real Host** *(carried from `deferred-work.md:157-163, 318`)*
**Given** a real Host, a real database and the real client in Chromium
**When** a person signs in and is then refused
**Then** the real `SameSite=None; Secure` cookie survives the two origins, the real preflight admits the anti-forgery header, a deep link to the other Account's Space renders the refusal page without requesting it, and a real `401` from the Host reaches the client's session-end path

**AC12 — the suites fit the machine they run on** *(carried from `deferred-work.md:8-12, 250, 296`, `test-design-architecture.md:604-609`, `TESTING-CONVENTIONS.md:338-379, 463-479`)*
**Given** a full `dotnet test Yello.slnx`
**When** it runs
**Then** SQL Server engines are shared rather than started per test class, the number started concurrently is stated and gated, the pooled-connection case keeps its own pinned pool and runs without parallelism, and a configured-but-dead container runtime is detected before a fixture tries to start a container

## Tasks / Subtasks

- [ ] **Task 1 — Read the open questions first, and record each decision before the code that depends on it** (AC: all)
  - [ ] Q1 (topology) and Q2 (where the harness lives) before Task 2. Q3 (what "browser surface" means) and Q4 (case-list shape) before Task 4. Q5 (the in-Space arm) before Task 6. Q6 (CI) before Task 10. Q7 (`/sync`) any time.
  - [ ] Each decision goes in the Completion Notes as "**Qn - (x).** … Cost to later stories: …". Every one is reversible.

- [ ] **Task 2 — Container topology: one engine per test process, a database per class** (AC: 12, 4)
  - [ ] Measure first and record in the Debug Log: engines started by a full `dotnet test Yello.slnx` today (expected **13**: ten `IClassFixture<MigratedDatabaseFixture>` classes in Slices and three direct `new SqlServerContainerFixture()` in `SharedFixtureSmokeTest`, `HostStartupTests`, `StartupConnectivityCheckTests`), peak Docker memory, wall clock. This machine has about **4.5 GB free**, and full runs have been killed for low memory.
  - [ ] Q1 (a): an xunit v3 **assembly fixture** (`[assembly: AssemblyFixture(typeof(SqlServerEngine))]` — present in the pinned `xunit.v3.extensibility.core` 4.0.0) starts **one** engine per test process. Each class fixture then creates **its own database** on that engine (`CREATE DATABASE [yello_{guid:N}]`, then `RegistrationDatabase.MigrateAsync`), so classes stay isolated from each other without each paying for an engine. `MigratedDatabaseFixture` keeps its public shape (`ConnectionString`, `IsAvailable`, `UnavailableReason`), so the ten Slices classes change by one line at most.
  - [ ] The migration includes `ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 1` (`20260925132825_ActiveSpaceResolution`). That is database-scoped, so it applies per database. Confirm it with `SpaceIsolationLayerTests`' serial-plan case (`:313`).
  - [ ] Cap each engine's memory: `MSSQL_MEMORY_LIMIT_MB` through `WithEnvironment` (2048 recommended; SQL Server on Linux needs about 2 GB). Record the value chosen and the measured peak.
  - [ ] Fold the three story-1.1 direct constructions onto the shared engine where their subject allows it. `SharedFixtureSmokeTest` tests the fixture itself; `HostStartupTests` and `StartupConnectivityCheckTests` may need an engine they can stop or a connection string that fails, so keep a private engine only where the test's subject requires one, and say why in the class remarks.
  - [ ] **Fix the skip guard** (`TESTING-CONVENTIONS.md:463-479`): `SqlServerContainerFixture.IsContainerRuntimeAvailable()` (`:306-331`) does no I/O, so a dead daemon reads as available. Probe the daemon (for example a Docker client `PingAsync` / version call with a short deadline) and keep the outcome as the skip reason. **Decision recorded, not changed:** a dead runtime still makes container-backed tests **fail** loudly, not skip — skipping SM-1 silently on a dead runtime would be the vacuous gate this repository refuses. The probe only gives the failure a precise message. (If you think skip is right, that is Q1's sub-question; recommend fail.)
  - [ ] A gate, in `Yello.Tests.Architecture` (no container): no test class in any suite constructs `SqlServerContainerFixture` directly or takes `IClassFixture<SqlServerContainerFixture>`, except a named list with reasons. Plant: a new direct construction is caught.
  - [ ] Measure again and record: engines started (target **≤ 5** in a full run: Slices 1, Isolation 1, Revocation 1, Client 1, plus any private engine Task 2 kept), peak memory, wall clock, and a full run with the browser suite green **in the same run** (the memory note `full-test-run-starves-browser-suite` says a full run starves Chromium; this task is the fix, so the combined run is the evidence).
  - [ ] Update `SqlServerContainerFixture`'s remarks and `TESTING-CONVENTIONS.md:313-388` ("Topology is NOT settled", "nine engines", "16.6 GB") to state the settled topology and the measured numbers. Close `deferred-work.md:8`, `:250` and `:296` with dated CLOSED notes.

- [ ] **Task 3 — The shared wire harness** (AC: 1, 2, 9, 10)
  - [ ] Q2 (a): move `HostProcess` and `WireSession` (+ `WireResponse`) from `tests/Yello.Tests.Slices` into `tests/Yello.Tests.Shared`, and change `AllowedReferenceEdges.cs:179` so Shared may reference `Yello.Contracts` (and nothing else). This is a visible architecture edit; state the reason on the row. `HostProcess` reads the connection-string key through `HostMetadata` (`HostProcess.cs:109`); pass the key in, or emit it as assembly metadata as `YelloSqlServerImage` already is (`Directory.Build.props:50-57`), so Shared needs no `Yello.Host` edge.
  - [ ] Slices keeps working through the moved types (namespace change only; 68 `RunAsync` call sites). Do not change behaviour while moving.
  - [ ] A long-lived Host for a test class (Q2 (a)): today every `RunAsync` starts a process. The isolation suite runs dozens of cases on two surfaces, so give it a `RunningHost` fixture (one process per class, or per set of equal settings) with a **log cursor**, so each case can read the slice of output its own requests produced. That cursor is what makes AC10 a per-case check rather than one capture per class.
  - [ ] Seeding **through the product**, not through Infrastructure internals (Isolation and Revocation cannot see them; `InternalsVisibleTo` names only Slices): register two Accounts (`POST /api/v1/accounts` — each provisions a Personal Space, sprint-change-proposal `:30-58`: display name, email, password), sign each in, read the Personal Space id from `GET /api/v1/spaces`, issue a Token for it (`POST /api/v1/spaces/{id}/api-tokens`). This is test-design B2's first shape ("seed through the application per Space") and needs no RLS exemption.
  - [ ] Raw SQL only for what no endpoint does yet (Membership removal, Space and Account deletion, Role change, `AccessRefusal` reads): add `Microsoft.Data.SqlClient` (already pinned, `Directory.Packages.props`) to Isolation and Revocation, and put the statements in one helper per suite, mirroring `ActiveSpaceEndpointTests.Tokens.cs:1090-1188` (`RemoveMembershipAsync` sets the Space session key; `DeleteAccountAsync` the Account key). `AccessRefusal` and `Account` carry no policy.
  - [ ] The surface abstraction (`test-design-qa.md:133-135`, Appendix A `:548-590`): an `IsolationSurface` enum (`Session`, `ApiToken`) and an actor type that sends a request **as** that surface — `Session`: cookie + anti-forgery token, exactly as `WireSession.GetAsync` does; `ApiToken`: bearer only, no cookie, no anti-forgery header, exactly as `GetWithTokenAsync` does. A case never branches on the surface; it reads its expectation from the case row.

- [ ] **Task 4 — The case list as data, and the gap gate** (AC: 1, 8)
  - [ ] `tests/Yello.Tests.Isolation/IsolationCases.cs`: one static list of `IsolationCase` records — `Id` (e.g. `ISO-DIRECT-SPACE`), the test-design id it lands (`I-1`, …), the route (a `Yello.Contracts` constant, never a literal), the method, the vector (`DirectIdentifier`, `DeepLink`, `Listing`, `Aggregate`, `Api`, `Write`, `Timing`, `PooledConnection`, `RecordKind`), and an `Expectation` **per surface** (`IReadOnlyDictionary<IsolationSurface, Expectation>`). There is **no** "not applicable": where a surface cannot reach a route, the expectation says what it gets (e.g. a Token on the switcher gets `401 authentication-failed`, identical to no credential).
  - [ ] Generate the theory data as the cross-product `cases × surfaces` (`TheoryData` / `MemberData`). A case is executed once per surface it declares.
  - [ ] **Gap gate 1 (AC1):** a test asserting every case declares an expectation for **every** `IsolationSurface` member. Plant: remove the Token expectation from one case → caught by name.
  - [ ] **Gap gate 2 (AC8):** every Space-scoped and Account-scoped route is covered. Enumerate the routes from the real Host's route table (start the Host and read its endpoints, or reflect over the `Yello.Contracts` route classes — Q4 decides), and assert each one appears in at least one case. The gate must first find a known route (`SpaceRoutes.ActiveOf`) before asserting, so it cannot pass over zero inputs. Plant: a case removed → caught; a fake route added to the comparison set → caught.
  - [ ] **Rows deferred to a later story are data too:** a case whose route or arm does not exist yet (the in-Space lookup, the first wire `403`) is a row with `PendingUntil = "2.2"` and a reason, executed as nothing. A gate asserts every pending row names a story key that is `backlog` in `sprint-status.yaml`, so a pending row cannot outlive its story silently.
  - [ ] Remove `--ignore-exit-code 8` from `Yello.Tests.Isolation.csproj:32` **and** `Yello.Tests.Revocation.csproj:32` in the same change that adds their first cases. `TestingConventionTests.Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code` enforces both directions.

- [ ] **Task 5 — The isolation cases (epic 1's routes)** (AC: 1, 2, 4, 6, 8, 10)
  - [ ] Implement the case list in "The epic-1 case list" (Dev Notes). Each case is a `[Theory]` over the generated data; traits `Suite=Isolation`, `Priority=P0` (I-17 `P1`), `Requirement` citing the FR/NFR **and** the AD (e.g. `NFR-1`, `AD-3`).
  - [ ] Every refusal compares **bytes**: status, `Content-Type`, body, and header names (`WireResponse` excludes `Date`) against the anchor answer — the `404 not-available` for a random Space id, or the no-credential `401`.
  - [ ] Every absence is anchored on a presence first: the actor reads its **own** Space successfully on the same surface before the foreign attempt (`TESTING-CONVENTIONS.md` rule, "an absence assertion must be validated against a planted signal").
  - [ ] I-13 on every refusal: the body contains neither Account's Space name, neither Space id unless it was in the URL, and no status name of the other Space.
  - [ ] AC6 (record kind): after each boundary 404, exactly one new `AccessRefusal` row for (acting Account, route Space) with kind `CrossSpace`, on **both** surfaces. The `InsufficientRole` half cannot be produced over the wire in epic 1 — every route a Token or Session reaches today is allowed for every Role (`ReadProjectsAndTasks`, `IssueApiTokenForOneself`; story 1.6 decision 4). It is proved through the filter in `SpaceBoundaryPipelineTests:172`; here it is a pending row naming **2.1** (already ledgered: `deferred-work.md:284`, `:360`).
  - [ ] AC4 / I-8 (the pooled-connection case) on both surfaces, in its **own collection with parallelisation disabled** (`[CollectionDefinition(DisableParallelization = true)]`) and its own database: a Host started with `Max Pool Size=1` on its connection string; Account A's actor reads A's statuses, then B's actor reads B's, alternately, several times; each answer holds exactly its own Space's status ids. Prove one physical connection was used: from a side connection, `sys.dm_exec_sessions` for that database and the Host's `program_name` shows exactly one session (anchor: the count is 1, not 0). Keep `Slices/PooledConnectionIsolationTests` (in-process, `@@SPID` identity) as the lower-level guard; it moves to the shared engine with its own database and pinned pool — SqlClient keys pools by connection string, so a pinned pool does not need its own engine.
  - [ ] AC10 / I-17: after **every** case, the case's log slice contains none of: both passwords, every Token plaintext issued in the class, both session-cookie values, both Space names. Anchor: the slice contains the Host's own line for that request (e.g. the `SpaceNotResolved` log for a refusal), so an empty slice cannot pass. Close `deferred-work.md:378`.

- [ ] **Task 6 — The timing case on both surfaces** (AC: 3)
  - [ ] Use `Yello.Tests.Shared/DurationIndistinguishability` (21 samples per arm, median, 20% of the slower median, server-side, interleaved, one warm-up). **Do not write a second method** (`TESTING-CONVENTIONS.md:264-296`).
  - [ ] "Server-side" over the wire: the measurement point is the Host's own duration for the request, not the client's round trip. If `HostProcess` exposes no server-side figure, read Kestrel's request-finished log line (it carries elapsed ms) from the case's log slice, or time the round trip and record that the measurement point moved — Q5 sub-question; recommend the log line.
  - [ ] Arms (Q5 (a)), on **each** surface: (i) boundary 404 — `GET /spaces/{other Account's Space}`; (ii) non-existence — `GET /spaces/{random}`; (iii) the Token-bound-elsewhere arm on the Token surface vs a non-member Session (already in Slices as `Tokens.cs:275-307`; lift it). Body comparison for every pair as well.
  - [ ] **The in-Space not-found arm** does not exist in epic 1: no route looks a resource up by id inside a Space (the first is a Task in 2.2). It is a pending row naming **2.2**, and `deferred-work.md:282` is updated, not closed: 1.9 delivers the two-surface harness and the boundary arms; 2.2 adds the in-Space arm to the same case list.
  - [ ] The planted oracle for each surface: an arm that skips the `AccessRefusal` write (story 1.6 measured that at 44-48% apart, `SpaceBoundaryPipelineTests:233-237`) — reproduce it at the wire by a test-only Host setting **or** by comparing against an arm that genuinely does less work (e.g. an unmatched `/api` path, which records nothing). The comparison must **fail** for the oracle. Record the measured gaps (real pair vs oracle) per surface in the Debug Log. If the wire's noise swamps the 20% band (it did not for 1.8's wire case), say so and fall back to the in-process measurement for that surface, recorded as a deviation.

- [ ] **Task 7 — The Revocation suite: request-path revocation on both surfaces** (AC: 9, 10)
  - [ ] `tests/Yello.Tests.Revocation`, same harness, its own case list with the same both-surfaces gap gate (Task 4's gate, generic over the list).
  - [ ] Cases, each on `Session` **and** `ApiToken`, each anchored by a successful request on the same credential first:
    - credential ended by its holder — Session: `DELETE /api/v1/sessions`, then the **replayed** pre-sign-out cookie → `401` (Lee's decision 1); Token: `DELETE /api/v1/account/api-tokens/{id}` → next bearer request `401`;
    - Membership removed (raw SQL, under the Space key) — Session: next `GET /spaces/{id}` → `404 not-available` + `CrossSpace` row (the Session is still valid for the Account's other Spaces, so not `401`); Token: `401` (the row is gone by cascade; V-3);
    - Space deleted — Session `404`; Token `401` (V-3);
    - Account deleted (Memberships first, then the Account, one transaction) — Session `401` (`SessionRevocation.cs:85` rejects a missing Account); Token `401` (V-3);
    - Role changed (raw SQL) — next request on each surface reports the new Role (`GET /spaces/{id}`'s Role field) — NFR-2's request-path clause, I-10.
  - [ ] Negative anchors: a rolled-back Membership removal leaves both credentials working; a sign-out leaves the Token working (story 1.8 Q11); a Token on `DELETE /api/v1/sessions` answers `400 antiforgery-token-missing` byte-identical to no credential and stamps nothing (Lee's decision 2).
  - [ ] The three V-3 events do not exist as endpoints until **3.3, 4.5, 5.4**; these cases prove the schema and the very-next-request answer with raw SQL. Leave the ledger entries `deferred-work.md:352-356` open (their owners add the endpoint-driven case to this suite's list). Close `deferred-work.md:364` for I-12, I-11 and V-3.
  - [ ] AC10's per-case log check runs here too.

- [ ] **Task 8 — The client: nothing from cache, nothing unlisted, nothing prefetched** (AC: 7)
  - [ ] In `Yello.Tests.Client` against `FakeYelloApi` (no Host needed for these): first load of a Space with its `GET /spaces/{id}` **held** (`HoldAsync`): the context bar shows its shell and **no Space name and no Role chip**, `main` shows no content from a previously visited Space; release it, then the name appears. Anchor: after release the name is present (so the "absent" was not a selector miss).
  - [ ] Switch A → B with B held: nothing of A remains in `main` or the bar while B is outstanding (`ContextBar.razor:25-30, 57-67` renders only from `ActiveSpace.Current`, purged before render, `Program.cs:163-168`).
  - [ ] No request for an unlisted id: navigate to `/spaces/{id not in the switcher answer}` (in-app navigation **and** a full load) → refusal page, and `FakeYelloApi.WasRequested(id)` is false. The in-app half closes the gap `deferred-work.md:346` names for the route-back case; leave that entry with 3.2 if its specific route-back case is not converted.
  - [ ] Nothing prefetched: across sign-in → land → switch, the recorded request log contains Space-scoped requests only for the Space being shown, never for another listed Space.
  - [ ] "Same layout" compared structurally (`deferred-work.md:348`): replace `RefusalPageAtAsync`'s `main.InnerText` comparison with a structural fingerprint of `main` — element tree (tag, role, `aria-*`, class), text per node, and the focused element's path — and compare the two refusal pages by that. Plant: an extra wrapper `div` on one path → caught.
  - [ ] Check whether existing 1.7 browser cases already prove part of this (`SpaceSwitchingTests.A_refused_listed_Space_and_an_unlisted_one_render_the_same_refusal_page`, the AC10 case). Reuse rather than duplicate; name in the Completion Notes which cases prove which AC7 clause.

- [ ] **Task 9 — The real browser against the real Host** (AC: 11, 2)
  - [ ] Q3 (a): a new collection in `Yello.Tests.Client` (`RealHostCollection`) holding the client dev server, Chromium, a Host process and a database on the suite's one shared engine. Edge change: `AllowedReferenceEdges.cs:174` Client row gains `Yello.Tests.Shared` (for `HostProcess`, `WireSession`, the engine). It keeps **no** reference to `Yello.Client` (the row's rationale stands).
  - [ ] Order: start the client dev server first (`ClientDevServer.cs:85-142`, OS port), then the Host with `ClientOrigin=<the client's origin>` (CORS allows exactly that one, `CrossOriginPolicy.cs:50,77`), then point the client at the Host: `HostBaseAddress` is read from `wwwroot/appsettings.Development.json` (`Yello.Client/Program.cs:82-86`, hard-coded `https://localhost:7093`) — answer that file's request with `page.RouteAsync` carrying the Host's real address. **That route is the only fake in this collection**; every `/api/v1/**` request goes to the real Host.
  - [ ] Origins: the dev server serves `http://127.0.0.1:<port>` and the Host `https://127.0.0.1:<port>` with the dev certificate. A `SameSite=None; Secure` cookie on a cross-origin credentialed fetch from an http page may be treated as third-party by Chromium; if it is refused, serve the client over https too (Kestrel dev cert) rather than relaxing the cookie, and record which. `IgnoreHTTPSErrors = true` on the browser context for the self-signed loopback certificate only.
  - [ ] Seed two Accounts through the real API (Task 3's seeding), then in Chromium: sign in as A through `SignIn.razor` → lands on A's Space with the context bar and Role chip drawn (the real cookie and the real preflight both worked); navigate to `/spaces/{B's Space id}` → the refusal page, and the Host received **no** request for B's id (read the Host's log slice — the Host log, not the fake, is the witness here); end A's Sessions server-side (sign-out from a second context with the same Account, or stamp `SessionsValidAfter` by SQL) → A's next navigation gets a real `401` and lands on `/sign-in` with the reason stated and Space-scoped state purged.
  - [ ] Close `deferred-work.md:157-163` and `:318`. Leave `:165` (the `aspire run` https loop) with its owner.

- [ ] **Task 10 — Gates owed to this story** (AC: 2, 8)
  - [ ] `No_caller_sets_both_session_context_keys` is per type (`deferred-work.md:344`). Strengthen it so a Space-scoped endpoint cannot reach the Account-scoped reads: assert that the only types depending on `IAccountSpaceDirectory` / `IAccountApiTokenDirectory` (constructor parameter or call, read from IL with the existing `CallersOf` helper, `SpaceSwitcherGateTests.cs:362-388`) are the Account-scoped endpoints and their handlers, by exact list. Plant: inject `IAccountSpaceDirectory` into `ActiveSpaceEndpoints` → caught.
  - [ ] `RemembersASpace` (`SpaceSwitcherGateTests.cs:426-427`) misses `_priorSpace`, `spaceHistory`. Widen it to both word orders and the missing words (`prior`, `history`, `lastUsed`, `mru`), and add both names to its plant test (`:287-294`). Do not let it match `SpaceDirectory` or `ActiveSpace`; assert those are not offenders.
  - [ ] Named isolation variances (`deferred-work.md:382`): in the Isolation suite, a case reading the catalogue: every table with a column whose name ends in `SpaceId` is either under the Space policy or in a named exception list `{ AccessRefusal: TargetSpaceId — written after rollback with no context, AD-20; ApiToken: BoundSpaceId — looked up before any Space is known, story 1.8 Q4 }`. Today `SpaceIsolationSchemaTests:127` only sees columns named exactly `SpaceId`, and only `ApiToken` has a positive exemption (`:342`); `AccessRefusal` has none. Plant: a new table with an `OtherSpaceId` column and no policy → caught.
  - [ ] Anything written to `Yello.Tests.Architecture` must find its subject before asserting.

- [ ] **Task 11 — "Runs on every change"** (AC: 5)
  - [ ] Q6 (a): this story makes the half it can make true and ledgers the other. Locally: Isolation and Revocation are strict suites inside `dotnet test Yello.slnx` (exit `2` on any failure; no `--ignore-exit-code`), and a single disclosure fails them — prove it with a plant (Task 12). CI does not exist (no `.github`, no pipeline; `TESTING-CONVENTIONS.md:486-492` gives CI to **1.10**). Add a ledger entry to **1.10**: the PR stage runs `Yello.Tests.Isolation` and `Yello.Tests.Revocation` in full on every push and fails the build on any failure; Playwright needs `install chromium --with-deps` on the agent; the dev certificate is needed for the Host; the engine topology from Task 2 is what keeps the PR stage under 15 minutes (`test-design-architecture.md:604-609`).
  - [ ] Record in the Completion Notes that AC5 is met locally and owed to 1.10 for "on every push", so nobody reads it as fully closed.

- [ ] **Task 12 — Plants, runs, ledger, conventions** (AC: all)
  - [ ] Plants, one at a time in the foreground, original content held in memory and restored in a `finally`, SHA-256 of the tree compared to a pre-run snapshot after each. **Never** `git checkout --`, `git restore` or `git reset`. Record `| # | Plant | Gate | Caught |` in the Debug Log. Minimum set: a case missing its Token expectation (gap gate 1); a route with no case (gap gate 2); a planted disclosure — the Space endpoint returns the Space name of the **route** Space in the 404 body, or the statuses read drops its `SpaceId` filter **and** its RLS policy is disabled in the test database (both layers, AD-2 — one layer alone must stay green) — caught on both surfaces; the refusal record kind swapped to `InsufficientRole` (AC6); the timing oracle (Task 6) on both surfaces; a log line writing the Token plaintext (AC10); a client render of the cached Space name before the answer (AC7); `Max Pool Size=1` removed from the pooled case (its one-session anchor must fail, proving the case observes reuse); a direct `SqlServerContainerFixture` construction (Task 2 gate); `_priorSpace` in client code; `IAccountSpaceDirectory` in a Space-scoped endpoint; an unexempted `OtherSpaceId` table.
  - [ ] Runs: `dotnet build Yello.slnx` (warnings are errors), then `dotnet test Yello.slnx` (MTP via `global.json`; `--no-build` runs zero tests). Record totals passed/failed/skipped, engines started, peak memory and wall clock. Baseline at `d912f57`: **434 total, 433 passed, 0 failed, 1 skipped** (the legacy-factor duration case). If Rancher's socket times out: `rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch.
  - [ ] `tests/TESTING-CONVENTIONS.md`: the settled topology and numbers; the moved harness; the surface abstraction and case-list rules ("a new Space-scoped route needs an isolation row on both surfaces"); the real-Host collection; correct the counts at `:451-461`.
  - [ ] Ledger (`deferred-work.md`): CLOSE with dated notes `:8`, `:157-163`, `:250`, `:296`, `:318`, `:344`, `:348`, `:364`, `:378`, `:382`; UPDATE `:282` (two-surface harness delivered; in-Space arm stays with 2.2). ADD: to **1.10** (CI, Task 11); to **2.1** (add the first wire `403` and its `InsufficientRole` row to the case list — append to `:284`); to **2.2** (the in-Space arm, append to `:282`); to **3.4** (SM-1 extension: add one-Account-two-Spaces rows to the same list); to **7.3** (Q7: `/sync` becomes a third `IsolationSurface` member, and the gap gate then demands an expectation for every row); to **8.1** (parity sweep reads this case list). `:211` (`SequentialGuidIdentifierGenerator`) was "1.9 or 2.9" — **already re-owned to 2.9 by this story's creation** (see Dev Notes).
  - [ ] **Do not add review narration to source files** (owned by Lee, project-wide). Do not tick this unless the diff holds it.

## Dev Notes

### The shape of the story in one paragraph

Two empty release-gating suites gain their cases, and the test estate is rebuilt so it can afford them. The cases are a **list held as data**: each row names a route, a vector and an expected answer for **each** surface (Session cookie, API Token); the theory data is the cross-product, so a row missing a surface fails as a gap, and a route missing a row fails as a gap. Every case runs over the wire against a real Host and a real SQL Server, seeded through the product (register, sign in, issue a Token), compares refusal bytes against the anchor 404 or 401, reads `AccessRefusal` for the kind, and checks its own slice of the Host's log for secrets. The Revocation suite does the same for the five events that end or narrow a credential. A pooled-connection case runs alone with one connection. The timing case reuses the one duration method on both surfaces. The client side is held in Chromium twice: against the network-edge fake for render-before-answer, and once against the real Host for the real cookie, preflight and `401`. Under all of it, SQL Server engines go from **13 per run to about 4**: one per test process, a database per class.

### Ledger entries owned by 1.9 — every one folded or re-owned

| Entry | Where it lands |
|---|---|
| `deferred-work.md:8-12` container topology (now 13 engines, ~4.5 GB free, runs killed for memory) | Task 2, AC12 |
| `:250` three 1.5 container classes; `:296` thirteenth engine | Task 2 |
| `TESTING-CONVENTIONS.md:463-479` skip guard does no I/O ("belongs with 1.9's topology decision") | Task 2 |
| `:157-163`, `:318` real-Host browser sign-in | Task 9, AC11 |
| `:282` boundary vs in-Space duration, "1.9 for both surfaces" | Task 6 (in-Space arm stays 2.2) |
| `:344` weak session-key gate and last-Space regex | Task 10 |
| `:348` "same layout" compared as text | Task 8 |
| `:364` lift I-12, I-11 Token half, V-3 into Isolation/Revocation | Tasks 5, 7 |
| `:378` I-17 log check covers one Host run | Task 5, AC10 |
| `:382` `ApiToken` as a named isolation variance | Task 10 |
| `:211` `SequentialGuidIdentifierGenerator` reseed ("1.9 or 2.9") | **Re-owned to 2.9**: it is an index-locality property under scale-out, measured by 2.9's five-thousand-Task Board; it discloses nothing across a Space, so it is not an isolation case. Annotated in the ledger at story creation. |
| `test-design-architecture.md:339-347` `/sync` as a third surface | Q7, ledgered to 7.3 |

Not 1.9's and left alone: `:270` (`SpaceScope` DI miswiring, no owner, fails closed), `:346` (route-back reload, 3.2) unless Task 8 converts it.

### The epic-1 case list (Isolation)

Actors: **A** and **B**, each registered through the product with a Personal Space (`SA`, `SB`), each signed in (Session) and holding a Token for its own Space. Anchor 404 = `GET /api/v1/spaces/{random}` on the same surface. Anchor 401 = the same request with no credential.

| Id | Test design | Vector | Request (by A, about B) | Session expectation | ApiToken expectation |
|---|---|---|---|---|---|
| ISO-DIRECT-SPACE | I-1, I-9 | DirectIdentifier | `GET /spaces/{SB}` | 404 = anchor, `CrossSpace` row | 404 = anchor, `CrossSpace` row |
| ISO-DIRECT-STATUSES | I-1, I-3 | DirectIdentifier / Listing | `GET /spaces/{SB}/statuses` | 404 = anchor, row, no B status name | same |
| ISO-WRITE-TOKEN | I-2 (epic-1 analogue) | Write | `POST /spaces/{SB}/api-tokens` | 404 = anchor, row, no Token row created for A in SB | 401 = no-credential (Session-only route) |
| ISO-LIST-SPACES | I-3, I-11 | Listing | `GET /spaces` | 200, exactly `[SA]`, rows exactly `{id, name}` | 401 = no-credential |
| ISO-LIST-TOKENS | I-11 | Listing | `GET /account/api-tokens` | 200, only A's Tokens, rows exactly `{id, name, space{id,name}, createdAt}`, no SB | 401 = no-credential |
| ISO-REVOKE-FOREIGN-TOKEN | I-4 | DirectIdentifier | `DELETE /account/api-tokens/{B's token id}` | 204, and B's Token still works (anchor) | 401 = no-credential |
| ISO-AGGREGATE | I-3 | Aggregate | `GET /spaces/{SA}/statuses` after B has been active in SB | exactly SA's status ids (count 3, none of SB's) | same |
| ISO-IDENTIFIER-CONFERS-NOTHING | I-4 | DeepLink | A obtains `SB` (from B's URL) and uses it on every Space route | every route 404 = anchor | same |
| ISO-TOKEN-BOUND-ELSEWHERE | I-12 | Api | A owns SA and a second Space SA2 (raw SQL Membership); credential established for SA, request to SA2 | 200 (a Session carries no Space, FR-2) | 404 = non-member anchor, `CrossSpace` against SA2 |
| ISO-UNMATCHED-UNDER-SPACE | I-5 | DeepLink | `GET /spaces/{SB}/nothing` vs `GET /spaces/{SA}/nothing` | identical 404 `not-available`, nothing recorded | same |
| ISO-BODIES | I-13 | (applies to every refusal above) | — | no Space name, no id not in URL | same |
| ISO-TIMING-BOUNDARY | I-6 | Timing | foreign vs random Space id | indistinguishable (21/median/20%), oracle caught | same |
| ISO-TIMING-IN-SPACE | I-6 | Timing | boundary 404 vs in-Space not-found | **pending 2.2** | **pending 2.2** |
| ISO-POOLED | I-8 | PooledConnection | A then B alternately, pool of one | each sees only its own | same |
| ISO-RECORD-KIND | AC6, NFR-7 | RecordKind | cross-Space → `CrossSpace`; within-Space Role failure → `InsufficientRole` | `CrossSpace` now; `InsufficientRole` **pending 2.1** | same |
| ISO-LOGS | I-17 | (after every case) | — | no secret in the case's log slice | same |

Not in epic 1 and therefore not rows yet: I-2 proper (writes to Space-scoped entities), I-14 (notifications), I-15 (Assignee), I-16 (Presence). They arrive with the stories that build their routes, through gap gate 2: a new `{spaceId}` route fails the suite until it has a row.

**Analysis worth keeping (ISO-UNMATCHED-UNDER-SPACE):** an unmatched path under a Space goes to the `/api/{**path}` fallback, which is outside the route group and does no resolution, so it answers `not-available` fast and records nothing — for a foreign Space and an own Space alike. It is therefore not a Space-existence oracle (both are equally fast). It *is* faster than a boundary 404 on a real route; that difference discloses route shape, not Space existence, and is the same for every Space. Record the measurement; do not "fix" it without a decision.

### Current state of the files this story changes — read each before editing

| File | Today | This story | Preserve |
|---|---|---|---|
| `tests/Yello.Tests.Shared/SqlServerContainerFixture.cs` | Plain `IAsyncLifetime`, one engine per instance (`:67`); image from assembly metadata (`:112-117`); sqlcmd wait strategy (`:161-174`); 5-min deadline (`:385-409`); `IsContainerRuntimeAvailable()` no I/O (`:306-331`); remarks say topology is 1.9's (`:32-54`). | Becomes the per-process engine (assembly fixture) with a per-class database factory; daemon probe; memory cap. | Image pin, wait strategy, deadline, logs attached to a failed start. |
| `tests/Yello.Tests.Slices/Accounts/RegisterAccount/MigratedDatabaseFixture.cs` | Wraps one container per class, migrates, records `UnavailableReason` (`:64-88`). | A database per class on the shared engine. | Public shape; skip-reason behaviour. |
| `tests/Yello.Tests.Slices/Accounts/RegisterAccount/HostProcess.cs` | Starts `Yello.Host.exe` per call, env vars (`:108-141`), HTTPS `127.0.0.1:0`, port from the listening line (`:35, :161-174`), log returned whole (`:251`), process tree killed (`:241-249`). | Moves to Shared; gains a long-lived form with a per-case log cursor. | Every env var, the `Production` environment, the rate-limit override, the cert handling. |
| `tests/Yello.Tests.Slices/WireSession.cs` | Cookie and anti-forgery by hand; bearer helpers (`:247-268`); `WireResponse` (`:374-415`). | Moves to Shared. | "A Token request attaches no cookie." |
| `tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs` | Isolation/Revocation → Host, Contracts, Shared (`:142-143`); Client → Contracts (`:174`); Shared → nothing (`:179`); exact equality (`:23-30`). | Shared → Contracts; Client → Contracts, Shared. | Exact equality; no Client → `Yello.Client`. |
| `tests/Yello.Tests.Isolation/*.csproj`, `tests/Yello.Tests.Revocation/*.csproj` | Empty; `--ignore-exit-code 8` at `:32`. | Cases; switch removed; `Microsoft.Data.SqlClient` added. | Central version pin (`PackageVersionPinTests`). |
| `tests/Yello.Tests.Client/SpaceSwitchingTests.cs`, `FakeYelloApi.cs`, `ClientSurfaceFixture.cs`, `ClientDevServer.cs` | Fake at the network edge with request log and `HoldAsync` (`FakeYelloApi.cs:103-133`); `RefusalPageAtAsync` text compare (`:576-591`). | Structural compare; AC7 cases; a real-Host collection. | One dev server and Chromium per collection; poll `location.pathname`, never Playwright's URL wait. |
| `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.cs` | `No_caller_sets_both_session_context_keys` (`:340-353`), `RemembersASpace` (`:426-427`), plant (`:287-294`). | Stronger dependency gate; wider regex. | The single `SetAccountAsync` caller rule. |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/PooledConnectionIsolationTests.cs` | Pool pinned to 1 (`:230-239`), `@@SPID` identity (`:70`), remarks hand I-8's suite case to 1.9 (`:23-30`). | Shared engine, own database, non-parallel collection. | Pinned pool, SPID identity. |

Production code: **none expected.** If a case finds a disclosure, that is a defect to fix in the product (and a finding to record), not a case to weaken. `Yello.Client/Components/ContextBar.razor` already renders the name only from `ActiveSpace.Current` (`:25-30, :57-67`) and `SpaceHome.razor` refuses an unlisted id without a request (`:86-95`); AC7's cases should confirm both, not change them.

### Every route today (the gap gate's subject)

| Method | Path | Credential |
|---|---|---|
| POST | `/api/v1/accounts` | anonymous, rate-limited |
| POST / DELETE | `/api/v1/sessions` | anonymous / Session only |
| GET | `/api/v1/sessions/current` | Session only |
| GET | `/api/v1/antiforgery/token` | any |
| GET | `/api/v1/spaces` (switcher) | Session only, Account-scoped |
| GET | `/api/v1/spaces/{spaceId:guid}` | Session or Token, `ReadProjectsAndTasks` |
| GET | `/api/v1/spaces/{spaceId:guid}/statuses` | Session or Token |
| POST | `/api/v1/spaces/{spaceId:guid}/api-tokens` | Session only (`.SessionOnly()`) |
| GET | `/api/v1/account/api-tokens` | Session only, Account-scoped |
| DELETE | `/api/v1/account/api-tokens/{tokenId:guid}` | Session only, Account-scoped |
| any | `/api/{**path}` fallback | outside the group |

Mapped in `Yello.Host/Program.cs:274-321`. The Token rule is `AdmitsTokens = IsSpaceScoped && no SessionOnly marker` (`ApiTokenAuthenticationHandler.cs:110-112`); anywhere else a bearer is no credential. The gap gate covers every `{spaceId}` route and every Account-scoped route; the anonymous and session routes are covered by the Revocation suite's 401 cases and need not be isolation rows (state that in the gate's name).

### Architecture constraints that bind here

- **AR-35** (`epics.md:223`): four suites gate release; isolation runs every case on both surfaces; integration tests against `mssql/server:2025-latest` via Testcontainers — **never an in-memory provider**.
- **AD-4** (`ARCHITECTURE-SPINE.md:96-100`): a case for one surface and not the other is a gap, not a choice. "AD-4's single pipeline means the both-surfaces requirement is a test parameter, not a second suite" (`test-design-architecture.md:368-386`).
- **AD-3** (`:88-94`): identical body **and duration**; no Space name, title or existence hint in an error body.
- **AD-2** (`:77-86`): two independent layers. A planted disclosure must disable **both** to be a disclosure; disabling one must stay green (that is the falsifiable-layer property, `test-design-architecture.md:368-386`). `MAXDOP = 1` stays until the pooled case is green on Azure (story 1.10).
- **AD-20 / NFR-7**: the pipeline writes `AccessRefusal` with kind `CrossSpace` or `InsufficientRole`.
- **AD-24** (`:218-222`): the switcher and Account settings return Space identity only. The amendment is not made (readiness issue 3, due before epic 3); assert the current letter.
- Logging (`:274`): never a password, Token or cookie; `SpaceId` as a field, never the name.
- **UX-DR31** (`epics.md:275`, `EXPERIENCE.md:146, 158-168, 279`): the client replica is in scope; no request for an id absent from the switcher answer (it would manufacture `CrossSpace` noise and bury real probing, `:146`); nothing renders from cache before the server answers; the context bar never renders the name early.

### Testing requirements

Conventions (`tests/TESTING-CONVENTIONS.md`): underscore-sentence names, one behaviour each; traits `Suite` (`Isolation`, `Revocation`, `Client`), `Priority`, `Requirement` (cite the FR/NFR **and** the AD); randomised data (never a shared email address); no `Task.Delay` (poll with a deadline); cleanup by container or database disposal — "a cleanup that 'works' may be evidence that isolation is broken"; every absence anchored on a presence and validated by a plant.

Theory shape (Appendix A, `test-design-qa.md:548-590`), generated rather than `InlineData`:

```csharp
[Theory, MemberData(nameof(IsolationCases.CrossProduct), MemberType = typeof(IsolationCases))]
[Trait("Suite", "Isolation"), Trait("Priority", "P0")]
public async Task A_foreign_Space_is_indistinguishable_from_an_absent_one(IsolationCase @case, IsolationSurface surface) { … }
```

Make each theory row's display name carry the case id and surface, so a failure names "ISO-DIRECT-SPACE / ApiToken" rather than an index.

### Previous story intelligence (1.8, 1.7, 1.6)

- **1.8 said not to put cases in Isolation/Revocation** (Task 9) and ledgered the lift here; the wire cases in `ActiveSpaceEndpointTests.Tokens.cs` stay as the Slices-level proof. Lift, do not move: the Isolation suite proves the property on both surfaces; Slices proves the mechanism.
- **1.8's duration case over the wire** (`Tokens.cs:275-307`) passed the 20% band, so wire timing is workable on this machine. The planted oracle there was an early return before the Membership read.
- **1.8 AC9 / Lee:** a Token on sign-out is `400 antiforgery-token-missing`, byte-identical to no credential. Pin that; do not "correct" it to `401`.
- **1.7's dominant review finding** was a false claim in a source remark. Every remark about isolation behaviour must be backed by a case.
- **1.6's review** caught gates checking less than their names claimed. Name every gate for exactly what it checks, and make each find its subject before asserting.
- **1.6 decision 4:** no wire 403 exists until 2.1.
- **Plant discipline:** 1.6 caught 19/19, 1.7 28/29 (one miss recorded), 1.8 19/19. Restore in a `finally`; never `git checkout --`, `git restore` or `git reset`.
- **Memory:** a full run with 12–13 engines starved the browser suite (8 Client failures that passed alone). Until Task 2 lands, run `dotnet test --project tests/Yello.Tests.Client` alone before calling a Client failure a regression; after Task 2, the combined run must be green.

### Git intelligence

- `3cb7f43` built API Tokens (`ApiTokenAuthenticationHandler`, `SpaceRequestEstablishment`, `ApiTokenStore`, the `ApiTokens` migration, `AccountSettings.razor`); `d912f57` closed its review (body read after the pipeline; unit of work clears `SpaceScope` on dispose). `4d12ea8` built the switcher, `SessionRevocation`, `FakeYelloApi`, `SpaceSwitchingTests`.
- Commits are per phase, straight to `main`, by the orchestrator. Do not commit, push, branch or stash.

### Latest technical notes (pinned versions; no upgrades)

- **xunit.v3 4.0.0** has assembly fixtures: `[assembly: AssemblyFixture(typeof(T))]`; the fixture is injected into a test class constructor like a class fixture, created once per test assembly (= per process under MTP), disposed at the end. `[CollectionDefinition(DisableParallelization = true)]` runs that collection after the parallel ones, alone.
- **Testcontainers 4.6.0**: `WithReuse(true)` shares a container across processes but leaves it running and bypasses the resource reaper — not recommended here (Q1 (c)). `MsSqlBuilder().WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "2048")` caps the engine. A daemon probe: `TestcontainersSettings` / Docker client `System.Version` call with a short timeout.
- **Microsoft.Data.SqlClient**: pools are keyed by the exact connection string, so `Max Pool Size=1` on one database is its own pool on a shared engine. `sys.dm_exec_sessions` filtered by `database_id` and `program_name` counts the Host's physical sessions.
- **Playwright 1.62.0**: `BrowserNewContextOptions.IgnoreHTTPSErrors`; `page.RouteAsync` for the one `appsettings` rewrite; Chromium treats a cookie set by a cross-site credentialed fetch as third-party — `SameSite=None; Secure` is required and the page origin's scheme can matter.

### Project structure notes

- **New, most likely:** `tests/Yello.Tests.Isolation/{IsolationCases.cs, IsolationSurface.cs, IsolationCaseTests.cs, IsolationGapTests.cs, PooledConnectionCaseTests.cs, IsolationTimingTests.cs, SchemaVarianceTests.cs, AssemblyInfo.cs}`; `tests/Yello.Tests.Revocation/{RevocationCases.cs, RevocationCaseTests.cs, AssemblyInfo.cs}`; `tests/Yello.Tests.Shared/{SqlServerEngine.cs, RunningHost.cs, SurfaceActor.cs, ProductSeeding.cs}` (names indicative); `tests/Yello.Tests.Client/RealHost*.cs`; an architecture gate for direct container construction.
- **Moved:** `HostProcess.cs`, `WireSession.cs` → `tests/Yello.Tests.Shared`.
- **Modified:** as in the current-state table, plus `TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml`, this file.
- **Variance to record:** the Shared → Contracts and Client → Shared edges, with reasons on the rows.

### References

- `_bmad-output/planning-artifacts/epics.md:171, 195, 198, 223, 275, 352, 877-915`
- `prds/prd-YelloBMAD-2026-08-15/prd.md:321-330, 359, 603-616, 683-690, 696-700, 730-734, 767, 854`; `addendum.md:123`
- `architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md:75, 77-86, 88-94, 96-100, 194-198, 218-222, 267, 274, 276, 397, 427`
- `ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:130-147, 158-168, 172-179, 279`
- `specs/spec-yello/SPEC.md:198, 214, 256, 260`; `acceptance-criteria.md:94-98, 154-162, 352-364`
- `test-artifacts/test-design-architecture.md:106-128, 135-141, 172-188, 212-231, 263, 279-290, 311-347, 368-386, 480-488, 590-609`
- `test-artifacts/test-design-qa.md:54, 90-147, 197, 209, 226-241, 265-282, 349-350, 406-417, 452, 481-484, 528-535, 548-590`
- `implementation-readiness-report-2026-08-22.md:542-544, 750, 763, 767-770, 963`
- `tests/TESTING-CONVENTIONS.md:9-42, 86-118, 120-262, 264-296, 313-388, 395-492`
- `implementation-artifacts/deferred-work.md` (entries in the ledger table above); `1-8-issue-an-api-token-scoped-to-one-space.md` (Task 9, Q4, Q11, AC9)

### Open questions — each with a recommended decision

The dev phase takes these as **reversible, recorded decisions** in the Completion Notes. None blocks starting.

1. **Container topology.** (a) One engine per test process via an xunit v3 assembly fixture, a database per class, memory capped; ~4 engines in a full run. (b) One engine per collection, classes grouped into a few collections — fewer changes, more engines, and cross-class parallelism lost inside each collection. (c) Testcontainers `WithReuse(true)` — one engine for every process, but it outlives the run, skips the resource reaper, and needs an environment opt-in on every machine and CI agent. **Recommend (a).** Sub-question: a dead runtime — fail (recommended, with the probe's message) or skip.
2. **Where the wire harness lives.** (a) Move `HostProcess` and `WireSession` into `Yello.Tests.Shared`, adding the Shared → Contracts edge. (b) Duplicate them in Isolation and Revocation — two copies of the thing every case trusts. (c) Let Isolation reference Slices — a test project referencing an Exe test project, and Slices sees Infrastructure internals Isolation must not. **Recommend (a).**
3. **What "the browser surface" means in the isolation suite.** (a) The Session-cookie wire surface — exactly what the browser sends, cookie and anti-forgery header — as the test design defines it ("HTTP and API token", `TESTING-CONVENTIONS.md:9`; `Surface.Browser` in Appendix A); the real Chromium-against-real-Host cases live in a `Yello.Tests.Client` collection (Task 9), covering what only a browser can (the cookie across origins, the preflight, client rendering). (b) Drive every isolation case through Chromium — slow, memory-heavy on this machine, and it would test the client's refusal to request (AC7) rather than the server's refusal to answer. **Recommend (a).**
4. **How gap gate 2 finds the routes.** (a) Start the Host and read its endpoint data source (the real route table, nothing hand-listed). (b) Reflect over `Yello.Contracts` route constants — cheap, but a route mapped without a Contracts constant is invisible. **Recommend (a)**, falling back to (b) only if the Host exposes no route list without a test-only endpoint; a test-only endpoint in production code is not acceptable.
5. **The in-Space arm of AC3.** No in-Space lookup by id exists until 2.2. (a) Deliver the two-surface timing harness with the boundary-vs-absent arms now, hold the in-Space arm as a pending row naming 2.2, and update `deferred-work.md:282`. (b) Build a test-only in-Space lookup route — production code no requirement asks for. (c) Treat the unmatched-path fallback under one's own Space as the in-Space arm — it does no resolution, so it is not the work AD-3 means. **Recommend (a).** Sub-question: the server-side measurement point over the wire — recommend Kestrel's request-finished log line from the case's log slice.
6. **"When any change is pushed."** No CI exists; 1.10 builds it. (a) Make the suites strict and part of `dotnet test` now, and ledger the push-triggered run to 1.10. (b) Add a minimal GitHub Actions workflow now — unverifiable from this phase (nothing may be pushed), and it pre-empts 1.10's three-stage design. **Recommend (a)**, with AC5 recorded as met locally and owed to 1.10.
7. **Is `/sync` a third isolation surface?** (`test-design-architecture.md:339-347`.) (a) Yes, from story 7.3, when the channel exists: it becomes a third `IsolationSurface` member and the gap gate then forces an expectation on every row. Ledger to 7.3. (b) Decide nothing now. **Recommend (a)** — the enum and the gate make it a one-line change with a loud consequence.
8. **The Session-surface answer for a Token-only case (ISO-TOKEN-BOUND-ELSEWHERE).** A Session carries no Space, so "credential established for SA, request to SA2" is `200` for a Session by FR-2 and `404` for a Token by FR-36. (a) Keep it as one row with different per-surface expectations — AC1's rule is that each surface has a stated outcome, not the same one. (b) Split it into a Token-only row with an exemption — that is the "choice" AC1 forbids. **Recommend (a).**

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-1-9-prove-isolation-on-both-surfaces.md`. It holds the scenario map, the schema constraints checked, the red-reason evidence and the plants.

The ATDD phase added 45 cases: 38 are skipped (`red: AC<n> - ...`) and 7 are active. The full run is 479 total, 440 passed, 0 failed, 39 skipped (baseline 434 / 433 / 1). No scaffold starts an engine: still 13.

| File | Cases | ACs | Green today |
|---|---|---|---|
| `tests/Yello.Tests.Isolation/IsolationWorld.cs` | 0 (scaffold contract `IIsolationWorld`; `StartAsync` throws naming Tasks 2-3) | - | - |
| `tests/Yello.Tests.Isolation/IsolationCaseListTests.cs` | 4 (GATE-001..004, the case list reached by name) | AC1, AC8 | none |
| `tests/Yello.Tests.Isolation/IsolationBoundaryTests.cs` | 11 (ISO-001..011, one per case-list row, each over both surfaces, with the I-17 log check inside each) | AC2, AC6, AC10 | none |
| `tests/Yello.Tests.Isolation/IsolationTimingTests.cs` | 3 (TIME-001..003, including the permanent oracle) | AC3 | none |
| `tests/Yello.Tests.Isolation/PooledConnectionCaseTests.cs` | 1 (POOL-001, `DisableParallelization` collection, no fixture) | AC4, AC12 | none |
| `tests/Yello.Tests.Isolation/SchemaVarianceTests.cs` | 1 (SCHEMA-001) | AC2 (Task 10) | none |
| `tests/Yello.Tests.Revocation/RevocationWorld.cs` | 0 (scaffold contract `IRevocationWorld`) | - | - |
| `tests/Yello.Tests.Revocation/RevocationCaseTests.cs` | 9 (REV-000..008; Lee's decisions are REV-001 and REV-008) | AC9, AC10, AC1 | none |
| `tests/Yello.Tests.Architecture/ContainerTopologyGateTests.cs` | 3 (ARCH-001 direct engines, ARCH-002 engine budget of 5, ARCH-003 memory cap) | AC12 | none |
| `tests/Yello.Tests.Architecture/ReleaseGatingSuiteTests.cs` | 1 (ARCH-004, no `--ignore-exit-code` on Isolation/Revocation) | AC5 | **yes, planted** |
| `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.Isolation.cs` | 2 (ARCH-005 Account-scoped reads by exact list; ARCH-006 wider last-Space scan) | AC2/AC8, AC7 | **ARCH-005 yes, planted**; ARCH-006 no |
| `tests/Yello.Tests.Slices/ContainerRuntimeProbeTests.cs` | 2 (PROBE-001/002, the probe reached by name, no engine) | AC12 | none |
| `tests/Yello.Tests.Client/SpaceScopedFirstLoadTests.cs` | 5 (E2E-001..005, against `FakeYelloApi`) | AC7 | **all five**; E2E-002 planted |
| `tests/Yello.Tests.Client/RealHostBrowserTests.cs` | 3 (E2E-006..008, no collection yet) | AC11, AC2 | none |

`--ignore-exit-code 8` came off both `Yello.Tests.Isolation.csproj` and `Yello.Tests.Revocation.csproj` in the ATDD change. The zero-test gate counts skipped facts, and an all-skipped run under `dotnet test` exits 0. The per-row facts loop both surfaces, and three branch on the surface. On activation they become rows of `IsolationCases` / `RevocationCases` and one generated theory (Task 4).

Activation order:
1. **ARCH-001, ARCH-002, ARCH-003**, then PROBE-001/002. They test the riskiest assumption: that the shared-engine topology (one engine per test process, a database per class, a memory cap) brings a full run to 5 engines or fewer with the browser suite green in the same run.
2. POOL-001, the pinned pool on the shared engine.
3. The harness (Task 3), then ISO-001.
4. GATE-001, GATE-004 and GATE-003 with `IsolationCases`, then GATE-002.
5. ISO-002..011 and SCHEMA-001. TIME-003 (the oracle) goes before TIME-001/002.
6. REV-000..008.
7. ARCH-006. ARCH-004 and ARCH-005 stay green throughout.
8. Keep E2E-001..005 green, then E2E-006..008 with `RealHostCollection`, E2E-006 first.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

- 2026-10-24: Story created (create-story). Status -> ready-for-dev.
