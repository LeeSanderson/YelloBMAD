---
baseline_commit: 7d52f27e8d57eb3c8b2f3f18fd977ec516afad69
---

# Story 1.10: Ship it to Azure with migrations as an explicit step

Status: done

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-10-ship-it-to-azure-with-migrations-as-an-explicit-step`
Requirements owned (coverage row `epics.md:353`): **AR-33** (`epics.md:221`), **AR-36** (`:224`), **AR-37** (`:225`), **AR-38** (`:226`), **AR-39** (`:227`), **AR-40c** (`:228`), **PRD §6.3** cost ceiling (`epics.md:180`, `prd.md:776-780`) and the **PRD §6.4 data-protection gate** (`epics.md:181`, `prd.md:782-796`). Also: **NFR-6**'s transit clause (`prd.md:722-728`; readiness `:654-656` says only this story's deployment asserts it), **AD-7** (`ARCHITECTURE-SPINE.md:114-118`), **AD-10** (`:132-136`), **AD-14** (`:156-160`), the spine's Config, Migrations and Operations conventions (`:273, :275, :278`) and its Deployment seed (`:301-331`). **AR-40b** (cold start against NFR-5) is shared with story 2.9 (`sprint-status.yaml:62`, readiness `:546`). This story states a provisional position on it, and 2.9 measures it.
Test-design items this story lands: **R5** (AR-40c, `test-design-architecture.md:215, 474-488`; `test-design-qa.md:489`), **R11** (the §6.4 tripwire, `arch:221, 556-565`; `qa:174, 210, 488`), the **PR stage** of the three-stage CI shape (`arch:179-182`, `qa:402-435`, `TESTING-CONVENTIONS.md:526-532`), **A-9** kept green (`qa:307`), and the **B4/T7** statement (`arch:116-117, 314`). R3/B4's measurement stays with 2.9.
Ledger entries owned by 1.10 (`deferred-work.md`): every one is folded in below as a task or stated as out of scope. See "Ledger entries owned by 1.10" in Dev Notes.
Depends on: **1.1** only (`implementation-readiness-report-2026-08-22.md:963`). 1.2–1.9 are done, and their behaviour must survive the deploy.

**The shape in one line.** Every repo-side artefact is built and **verified locally without deploying**: the infrastructure as code, the CI and deploy workflows, the container image, the migration bundle run as an explicit step, the probes, and the production-only Host configuration. Everything that needs an Azure subscription, an Entra tenant, a domain or money is **Lee's**. Those items are listed as prerequisites (P1–P7), and the story is complete without them. The first real deploy is an operator act after this story, and nothing in the repository may claim it happened.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the operator,
I want Yello deployed to the two environments it has, with schema changes applied deliberately and the free-tier budget visible,
So that the £30/month ceiling stays a decidable question rather than a surprise.

## Acceptance Criteria

AC1–AC9 are reproduced **verbatim** from `epics.md:925-967` (`status: final`), numbered by position. AC10–AC17 come from the ledger, the test design, and what a working deploy needs (the skill's end-to-end rule). Each one says where it comes from. "Verified locally" means proved in this repository with no Azure subscription. Where a criterion can be **fully** met only by a real deploy, the local half is the story's work. The Azure half is listed under Lee's prerequisites and recorded as **not yet observed**.

**AC1 — the pipeline, and migrations before promotion**
**Given** a push to the default branch
**When** the pipeline runs
**Then** the client deploys to Azure Static Web Apps and the Host deploys as a Container App revision limited to at most one replica
**And** migrations run as an explicit job **before** the revision is promoted, never on application start

**AC2 — isolation is created by migration**
**Given** the migrations
**When** they are applied
**Then** they include the row-level security policies and the filtered unique indexes, so isolation is created by migration rather than by hand

**AC3 — configuration from the environment; secrets from Key Vault**
**Given** configuration
**When** the application starts in Azure
**Then** every value comes from environment variables, with connection strings and the Communication Services key drawn from Key Vault via managed identity
**And** no secret exists in source, in appsettings, or in the container image

**AC4 — exactly two environments**
**Given** the environment list
**When** it is enumerated
**Then** there are exactly two — Local via `aspire run`, and Azure — and no staging environment, because a second Azure environment would consume the same free grants that make production free

**AC5 — the free-tier budget is visible, never billed silently**
**Given** the free-tier vCore allowance
**When** 10% of the monthly remainder is reached
**Then** a metric alert fires
**And** `Behavior when free limit reached` is set to auto-pause until next month rather than paid overage, because exceeding the budget must be visible and not billed silently

**AC6 — probes answer from process state; nothing wakes an idle database**
**Given** liveness and readiness probes
**When** they answer
**Then** they do so from process state with no database round trip
**And** no component queries the database on a fixed interval, so an idle database is never woken

**AC7 — AR-40c before the first production deploy**
**Given** the first production deploy
**When** it is prepared
**Then** Azure SQL Database's exposure to the `SESSION_CONTEXT` parallel-plan defect has been confirmed directly and the finding recorded
**And** `MAXDOP = 1` is relaxed only if the pooled-connection isolation test remains green

**AC8 — the §6.4 gate, recorded as an operational condition**
**Given** PRD §6.4's data-protection gate
**When** the deployment is recorded
**Then** it is asserted in writing that this is a **single-operator deployment** claiming no data-protection posture, and that **the first Account created by anyone other than the operator makes the PRD non-compliant until amended**
**And** the five prerequisites that become live at that moment are named rather than left to be rediscovered: a lawful basis for holding email addresses and authored content; a stated data region with no replication outside it; encryption at rest asserted (NFR-6 covers transit only); a breach-notification position; and a subject-access or export route (FR-3 covers erasure only)
**And** the gate is recorded as an operational condition alongside the §6.3 cost ceiling rather than as a backlog item, because the PRD wrote it as a testable release condition and nothing else in this plan detects it

**AC9 — the obligations parked against the gate travel with it**
**Given** that gate
**When** the obligations parked against it are collected
**Then** the UX spine's accepted egress is carried here rather than left in `EXPERIENCE.md` alone: browser enhanced-spellcheck and cloud IME transmission of description text to third-party services is **accepted for v1** and becomes a real obligation at the gate
**And** the reason it is recorded here is stated: it was deliberately parked *"against §6.4's data-protection gate"*, so a plan that carries the obligation without the gate loses both — and the description editor is the product's largest free-text surface

**AC10 — every push runs the release-gating suites** *(carried from story 1.9 AC5, `deferred-work.md:391`; `TESTING-CONVENTIONS.md:526-532`; `test-design-qa.md:402-435`)*
**Given** a push or a pull request to the default branch
**When** CI runs
**Then** the architecture suite runs **first**, then the whole of `dotnet test Yello.slnx`, including Isolation, Revocation and Client in full. A single failing case fails the run, and no deploy follows a red run.
**And** a CI agent on which the container runtime is unconfigured **fails** the container-backed classes rather than skipping them, so SM-1 cannot pass vacuously in CI

**AC11 — the Host behind the platform's proxy** *(carried from `deferred-work.md:290, 304`; required by AD-7 and the anti-forgery behaviour found 2026-09-18, `deferred-work.md:171`)*
**Given** the Host running behind Container Apps ingress, which terminates TLS
**When** it serves a request
**Then** it sees the original scheme (`https`) and the client's address from the hop the platform appends, and never from a value a client can rotate
**And** the credential-hashing rate limit partitions on that address, normalised: an IPv4-mapped IPv6 address keys as its IPv4 form, and IPv6 keys on its /64

**AC12 — a Session survives a revision and a cold start** *(new, required for AC1 to leave the system working; see Dev Notes "Findings this story must not miss")*
**Given** the Host scaled to zero and started again, or replaced by a new revision
**When** a browser presents a Session cookie or an anti-forgery token issued by the previous process
**Then** the ticket is still readable, and the Session is honoured or refused by `SessionsValidAfter` alone, as on one long-lived process

**AC13 — the client finds its Host, and deep links resolve** *(carried from `deferred-work.md:92`; required by AC1)*
**Given** the client served by Static Web Apps on its own origin
**When** it boots, or a deep link such as `/spaces/{id}` is loaded directly
**Then** it calls the Container App's address taken from deploy-time configuration (not a secret), the Host allows exactly that origin (AD-7), and the static host serves `index.html` for every client route
*Amended at code review, 2026-10-25 (Lee's decision D1 (b)):* the WebAssembly loader fetches only the appsettings files listed in the boot configuration written at publish. So `Yello.Client/wwwroot/appsettings.Production.json` **is committed, holding no value (`{}`)**, and the deploy overwrites the published copy with the address. This replaces Task 7's "never committed". The file carries no value and no secret.

**AC14 — a paused or unreachable database is an honest refusal, not a 500** *(carried from `deferred-work.md:336`, `:314`, `:334`)*
**Given** the database paused, resuming or unreachable
**When** a request presents a Session cookie, or arrives while the refusal purge is due
**Then** a request that needs the database is answered with a uniform `503 application/problem+json` carrying `Retry-After`, never an unhandled `500`, and nothing is admitted
**And** a request that otherwise touches no database (`GET /api/v1/antiforgery/token`, the unmatched `/api` fallback, an unauthenticated `401`) does not start the refusal purge
**And** a sign-in issued after a sign-out is admitted even if the host clock has stepped back past `SessionsValidAfter`

**AC15 — migrations run the same way in both environments, and never from the Host** *(carried from AR-36, `deferred-work.md:242`; the local half of AC4)*
**Given** the migrations
**When** they are applied in Azure or under `aspire run`
**Then** they are applied by one artefact (the migration bundle), run as its own step before the Host starts serving, under an identity that may alter schema. The Host's identity can read and write data only, and cannot alter a security policy or a database-scoped configuration.
**And** no production assembly calls `Migrate`, `MigrateAsync`, `EnsureCreated` or an `IMigrator`, gated at build

**AC16 — the test estate's debts to CI are paid** *(carried from `deferred-work.md:400, 401`)*
**Given** the shared test harness on a CI agent
**When** a dead container runtime or a browser-driven Host is involved
**Then** `SqlServerEngine` is proved to fail with the probe's reason before constructing a container, and to cache that failure for later callers
**And** `RunningHost`'s log cursor counts only the requests the harness itself sent, so a browser's requests cannot satisfy a wait early

**AC17 — the one-off audit suppression is re-checked, never silently kept** *(carried from `deferred-work.md:396`; Lee's approval of 2026-10-24)*
**Given** the labelled ItemGroup "One-off NuGet audit suppressions" in `Directory.Build.props`
**When** this story runs
**Then** whether a stable fix exists is checked and recorded with the date: `System.Security.Cryptography.Pkcs` at or above 9.0.21 through a stable `Microsoft.Data.SqlClient`, and an Aspire past `MessagePack` 2.5.302
**And** a suppression is removed only for an advisory a stable, in-policy version bump actually clears. While no fixed release exists, the ItemGroup stays exactly as Lee approved it.

## Lee's prerequisites — what only Lee can provide

None of these blocks the story. Each one blocks the **first real deploy**, and the story's runbook (Task 12) names where each is used. Record each one's state in `docs/operations.md`, as **provided**, **not yet** or **declined**. Never record one as done on Lee's behalf.

- **P1 — an Azure subscription, a region and an operator alert address.** The resource-group name, the `location` parameter (§6.4: "Nothing pins a region", `prd.md:790-794`, so the chosen region is incidental and must **not** be written down as the gate's "stated data region"), and the email address the action group notifies.
- **P2 — Entra rights for the one-time bootstrap.** The first `az deployment group create` of `infra/main.bicep` needs Owner, or Contributor plus User Access Administrator, on the resource group, because it creates role assignments. It also needs an Entra administrator for the SQL logical server. After that, someone with that role must create the two database users once (`CREATE USER [<id-yello-migrate>] FROM EXTERNAL PROVIDER` with schema rights, and `CREATE USER [<id-yello-app>] FROM EXTERNAL PROVIDER` with `db_datareader` and `db_datawriter`). An ARM deployment cannot run T-SQL as an Entra principal. The runbook ships the exact script.
- **P3 — the GitHub repository's deploy trust.** The repository (or its `azure` environment) must be allowed to log in to Azure by OIDC federation, through the federated credential the IaC creates on the deploy identity. It also needs three repository **variables** (not secrets): tenant id, subscription id and client id. No long-lived Azure secret is stored in GitHub.
- **P4 — a domain, if Safari and Firefox must work.** See Finding F2. The default hostnames put the client (`*.azurestaticapps.net`) and the API (`*.azurecontainerapps.io`) on different sites, so the `SameSite=None` Session cookie is a third-party cookie. Safari blocks it by default. A custom domain that puts both on one registrable domain (for example `app.<domain>` and `api.<domain>`) makes it first-party. That costs a domain registration (about £10 a year) and two DNS records. The IaC takes the two hostnames as optional parameters.
- **P5 — the AR-40c confirmation (AC7).** Confirm directly whether Azure SQL Database is exposed to the `SESSION_CONTEXT` parallel-plan defect, by a Microsoft support ticket or a vendor statement. The spine and `test-design-qa.md:439` both call it "a vendor question". Then run the runbook's pooled-connection check against the deployed database. Record both in `docs/operations.md`. Until then, `MAXDOP = 1` stays.
- **P6 — pressing the button, and the first measurements.** Run the first deploy. Then record in `docs/operations.md`: sign-in p95 on the real hardware (`deferred-work.md:147`; 220,000 PBKDF2 iterations measured ~270 ms on the development machine, and Container Apps consumption at 0.5 vCPU may cross NFR-5's 500 ms), and one cold-start observation for AR-40b. Do **not** change `PasswordWorkFactor.ChosenIterationCount` on the strength of it. `deferred-work.md:225` makes the remedy for the timing oracle a precondition of any raise.
- **P7 — the §6.4 signature.** AC8's assertion is the operator's, not the developer's. The story writes it. Lee confirms it (or amends it) before the first deploy.

## Tasks / Subtasks

- [x] **Task 1 — Read the open questions and findings first, and record each decision before the code that depends on it** (AC: all)
  - [x] Q1–Q4 before Task 3. Q5–Q8 before Tasks 4–6. Q9–Q13 before Tasks 7–8. Q14–Q18 any time.
  - [x] Record each decision in the Completion Notes as "**Qn - (x).** … Cost to later stories: …". Every one is reversible.
  - [x] Record each prerequisite's state (P1–P7) in `docs/operations.md`. All of them are **not yet** at the end of this story unless Lee says otherwise.

- [x] **Task 2 — AR-36 at build: nothing in production migrates** (AC: 15, 2)
  - [x] Add a gate in `Yello.Tests.Architecture` that reads production IL (use the existing `CallersOf`-style IL helpers, e.g. `SpaceSwitcherGateTests.cs:362-388`). No type in `Yello.Host`, `Yello.Application`, `Yello.Infrastructure` or `Yello.Domain` may call `RelationalDatabaseFacadeExtensions.Migrate`/`MigrateAsync`, `DatabaseFacade.EnsureCreated`/`EnsureCreatedAsync`, or `IMigrator.Migrate`/`MigrateAsync`. Vacuity guard: the scan must find a known call into `Microsoft.EntityFrameworkCore` (e.g. `UseSqlServer` in `InfrastructureServices`) before it asserts.
  - [x] Plant: `context.Database.Migrate()` in a production type → caught by name. Restore in a `finally`.
  - [x] AC2 is already true of the migrations: the RLS functions and policies (`20260828164716_InitialSchema.cs:219-240`, `20261024143930_AccountScopedMembership.cs:49-77`), the filtered Owner index (`InitialSchema.cs:175`, `[Role] = N'Owner'`), and `MAXDOP = 1` (`20260925132825_ActiveSpaceResolution.cs:58-60`, `suppressTransaction: true`). Task 3 proves that **the deployed artefact** applies them, which is the half nothing proves today.

- [x] **Task 3 — The migration bundle: one artefact, run as its own step, proved equivalent** (AC: 2, 15, 1, 4)
  - [x] Q3 (a): `dotnet ef migrations bundle --project Yello.Infrastructure --startup-project Yello.Infrastructure --self-contained -r linux-x64 --configuration Release -o artifacts/efbundle` uses the existing design-time factory (`YelloDbContextFactory.cs:32-37`, which connects to nothing) and the local `dotnet-ef` 10.0.11 tool (`.config/dotnet-tools.json`). The bundle takes the connection at run time (`--connection`). No new project and no new reference edge. `artifacts/` is already git-ignored.
  - [x] **Do not use `dotnet ef migrations script --idempotent`.** It wraps each statement in `IF NOT EXISTS (… __EFMigrationsHistory …) BEGIN … END`, and `CREATE FUNCTION` / `CREATE SECURITY POLICY` must be the first statement in a batch. That is why the migrations put each in its own `Sql()` call (`InitialSchema.cs:219-221`). An idempotent script fails on them, or would need every one rewritten as `EXEC('…')`. The bundle executes each `Sql()` operation as its own command, exactly as `MigrateAsync` does in the test fixtures.
  - [x] **Equivalence test** (Slices, on the shared engine — `SqlServerEngine.CreateDatabaseAsync`, never a new engine; `ContainerTopologyGateTests` holds that): apply the bundle to one fresh database and `MigrateAsync` to another. Compare from the catalogue: tables and columns, `sys.security_policies` and `sys.security_predicates` (predicate definition text, FILTER/BLOCK, operation), `sys.indexes` with `filter_definition`, `sys.database_scoped_configurations` where `name = 'MAXDOP'` (value 1), and the `__EFMigrationsHistory` rows. Then run the bundle a **second** time: it applies nothing and exits 0. Anchor: the comparison must find the `SpaceIsolationPolicy` and the filtered Owner index before asserting equality.
  - [x] The bundle's path comes from an environment variable (`YELLO_MIGRATION_BUNDLE`). With it unset, the class **skips with that reason** locally. In CI the variable is set by the step that builds the bundle, and `YELLO_REQUIRE_MIGRATION_BUNDLE=1` turns the skip into a failure (same shape as Task 9's runtime requirement). State that in the class remarks and in `TESTING-CONVENTIONS.md`.
  - [x] **Local environment parity (AC4, AC15).** Today **nothing migrates the database under `aspire run`**: the Host never migrates (AR-36), the AppHost only creates the `yello` catalogue (`Yello.AppHost/Program.cs:31`), and `StartupConnectivityCheck` opens a connection and nothing else. So the Local environment serves `500`s on registration until somebody migrates by hand. Q17 (a): the AppHost runs the migration as its own resource before the Host. Either `AddExecutable` running `dotnet ef database update --project Yello.Infrastructure --connection <the database's connection string>` through the local tool, or the bundle. The Host then `.WaitForCompletion(migrate)`. Aspire 13.4 has `WaitForCompletion`. Verify it in the pinned version before relying on it. Run `./run.ps1` once, register an Account in the browser, and record it in the Debug Log. That also discharges `deferred-work.md:169` (the `https` dev loop never run).
  - [x] Record in the Completion Notes: the bundle's size and build time, and that the equivalence test ran (not skipped) in the final run.

- [x] **Task 4 — The Host's production shape: probes, forwarded headers, Data Protection, HSTS** (AC: 6, 11, 12, 3)
  - [x] **Probes (Q15 (a)):** `GET /health/live` and `GET /health/ready` mapped on `app`, **outside `/api`**. They are not product routes, and an `/api` path would fall into the isolation gap gate's "every `/api` route" sweep. Each is a plain `MapGet` answering `200` from process state. **Do not call `AddHealthChecks()`**: it registers `HealthCheckPublisherHostedService` as an `IHostedService`, which is the timer-shaped service AR-33 and A-9 forbid, even with no publishers. Confirm against the pinned 10.0.11 framework. If it does register one, that confirms the rule. No auth, no anti-forgery and no rate limit on the probes. The probes are anonymous, and their bodies say nothing about the database.
  - [x] Probe test (Slices, wire, `HostProcess`): start a Host whose connection string names an unreachable server (port 1). Both probes answer `200` within a short deadline, and the Host's log shows **no** connection attempt. Anchor: in the same run, an `/api` request that needs the database fails, so the database really was unreachable.
  - [x] Architecture gate: the probe handlers depend on nothing from `Yello.Infrastructure`, `Microsoft.EntityFrameworkCore` or `Microsoft.Data.SqlClient` (IL). Extend `No_production_code_runs_on_a_timer_or_as_a_hosted_service` (`RefusalBoundaryGateTests.cs:693-704`) only if a new banned shape appears. It stays green and is A-9. Plant: a probe that opens a `SqlConnection` → caught.
  - [x] **Forwarded headers (Q10 (a)), enabled only by configuration.** Read `ForwardedHeaders:Enabled` (env var `ForwardedHeaders__Enabled=true`, set only by the IaC). When it is on, use `XForwardedFor | XForwardedProto`, `ForwardLimit = 1`, and clear `KnownProxies`/`KnownNetworks`. Container Apps ingress has no fixed proxy address, and the container is reachable only through ingress, so the single right-most hop the platform appends is the one to trust. A client-supplied `X-Forwarded-For` lands to its **left** and is ignored. `UseForwardedHeaders()` goes **first** in the pipeline, before `UseCors`. Log the resolved setting at startup, like `RateLimitLog.PermitsResolved` (`Program.cs:222`).
  - [x] **This is not optional, and it is not only about rate limits.** TLS ends at ingress, so the request reaches Kestrel as `http`. With `CookieSecurePolicy.Always` the anti-forgery system **refuses to issue a token over a non-SSL request** (found 2026-09-18, `deferred-work.md:171`, "arriving as a 500 with an empty body"), and the `Secure` cookies are not set. Without `XForwardedProto` the deployed product cannot sign anyone in.
  - [x] Wire tests (Slices, `HostProcess` on plain `http://127.0.0.1:0` with `ForwardedHeaders__Enabled=true`):
    - `GET /api/v1/antiforgery/token` with `X-Forwarded-Proto: https` succeeds. Anchor: without the header, the same Host refuses it.
    - With the setting **off**, `X-Forwarded-Proto` is ignored.
    - Two sign-in bursts with `X-Forwarded-For: <random>, 203.0.113.7` and a **different** left part share one window (the 11th request is `429`).
    - `203.0.113.7` and `::ffff:203.0.113.7` share one window.
    - `2001:db8:1:2::1` and `2001:db8:1:2::2` share one window, and `2001:db8:1:3::1` does not.
  - [x] `CredentialHashingRateLimit.PartitionOf` (`:124-125`) normalises: `MapToIPv4()` when `IsIPv4MappedToIPv6`, and the /64 prefix for IPv6. A **missing** address (Q10 sub-question): keep the shared `""` partition, and add one startup line when forwarded headers are on, because under ingress a missing address means a misconfiguration. Unit-test `PartitionOf` directly as well. Keep the uniform `429` (`SessionEndpointTests.Sign_in_past_the_limit_is_refused_with_429_alike_for_a_known_and_an_unknown_address` stays green). The partition is still never the submitted address.
  - [x] **Data Protection keys (Q7 (a), Finding F1).** Persist the key ring so that a new revision and a cold start can read cookies and anti-forgery tokens issued by the previous process. Recommended: `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.11 (in-band with the runtime, and a non-AR-1 pin like `Microsoft.Extensions.Localization`, so record it in `Directory.Packages.props` with a reason; add it to `PackageVersionPinTests` only if that gate's expected set covers non-AR-1 pins, so read the gate first). Then `PersistKeysToDbContext<…>` and `SetApplicationName("Yello")`.
    - The keys live in a `DataProtectionKeys` table added by a **new migration**. Apply the migration-by-hand convention (`deferred-work.md:102`, `TESTING-CONVENTIONS.md:298-311`): five analyser rules, no `<auto-generated />`.
    - The table carries no `SpaceId`, so `SchemaVarianceTests` (Isolation) needs no entry. Confirm it stays green.
    - Use a context of its own or `YelloDbContext` (it then implements `IDataProtectionKeyContext`). Rec: `YelloDbContext`, so that one migration history covers it.
    - The key ring is read lazily on first protect/unprotect, which happens on a request. Nothing here is on a timer.
    - Keys are not encrypted at rest. NFR-6 says encryption at rest is explicitly not required (`prd.md:722-728`). Record it in `docs/operations.md` as a §6.4 prerequisite item (encryption at rest), not as a defect.
  - [x] Data Protection test (Slices, wire). **The local default hides F1**: without configuration, ASP.NET Core keeps the ring in the *user profile* (`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` on Windows, `~/.aspnet/DataProtection-Keys` on Linux), so two Host processes on one developer machine already share it. A container gets a fresh profile on every start. Each Host process in this test must therefore get its **own empty profile directory** (`LOCALAPPDATA`/`HOME`/`USERPROFILE` env vars pointed at a fresh temp folder), which reproduces the container.
    - The test: Host process 1 issues a Session (sign-in) and an anti-forgery token. Stop it. Host process 2 starts on **the same database**, with its own fresh profile. The cookie from process 1 reads `GET /api/v1/sessions/current` as `204`, and a state change carrying process 1's anti-forgery pair is not refused.
    - Anchor: a Host 2 on a **different** database, also with a fresh profile, refuses the same cookie as `401`. Also assert that the `DataProtectionKeys` table holds a row after process 1.
    - Then sign out on Host 2, and the replayed cookie is `401` (`SessionsValidAfter` still rules).
    - The plant (persistence removed) must fail this test. If it does not, the profile isolation is wrong.
  - [x] Outside Development: `UseHsts()` and the default HSTS max-age. TLS is the platform's (Container Apps ingress with `allowInsecure: false`, Static Web Apps by default), and SqlClient's default `Encrypt=Mandatory` stays (`Encrypt=True` in the Key Vault connection string, never `False`). Together these are NFR-6's transit clause. Record that in `docs/operations.md`.
  - [x] The Host's container must listen where ingress expects: `ASPNETCORE_HTTP_PORTS=8080` (the .NET 8+ container default). The IaC's `targetPort` must match.

- [x] **Task 5 — Failure honesty: a paused database, the purge trigger, the clock** (AC: 14, 6)
  - [x] **Revocation read failure (Q11 (a), `deferred-work.md:336`).** In `SessionRevocation.ValidateAccountAsync` (`SessionRevocation.cs:78-92`), a `DbException` / `SqlException` (and the EF wrapper `InvalidOperationException` for a transient failure) must not escape as an unhandled `500`. Answer `503 application/problem+json` with a stable new type (e.g. `ProblemTypes.ServiceUnavailable`, `urn:yello:problem:service-unavailable`) and `Retry-After`, uniform for every route. Admit nothing. A middleware or a single exception-to-problem mapping beats a per-endpoint catch (AR-3).
    - Apply the same mapping to the Token lookup (`ApiTokenStore.FindAsync`) and to Space resolution.
    - **Do not enable `EnableRetryOnFailure`.** `SqlServerRetryingExecutionStrategy` throws on user-initiated transactions, and Space resolution holds one across the endpoint (`deferred-work.md:252`, `MembershipActiveSpaceResolver.cs`).
    - For a resuming serverless database, put `Connect Timeout=60` in the **Azure** connection string (in Key Vault, Task 6). A paused Azure SQL serverless database takes up to about a minute to resume, and error 40613 is the transient code. Ingress allows 240 s (spine `:272`).
  - [x] **Interaction with Q7 (a).** Once the key ring is in the database, the **first** cookie or anti-forgery operation of a process loads the ring from the database. Against a paused database that load can fail before `SessionRevocation` runs, and Data Protection may then try to create and persist a new key, which also fails. The `503` mapping must cover that path as well. A fresh key must never be created silently in memory and used, because that would void every outstanding cookie for the life of the process. Test it with a fresh profile and an unreachable database.
  - [x] Test: Host with an unreachable database, a request with a **validly encrypted** Session cookie. To get one, issue it from a Host on a real database that shares the Data Protection key ring, then repoint. Or test the mapping in-process with a store that throws. The answer is `503`, the body is byte-identical across two different routes, and `Retry-After` is present. Anchor: with the database up, the same cookie is `204` on `sessions/current`.
  - [x] **Purge trigger (Q13 (a), `deferred-work.md:314`).** `app.Use(RefusalRetentionTrigger.InvokeAsync)` (`Program.cs:232`) runs on **every** request. A scanner hitting only `antiforgery/token`, the `/api` fallback or anonymous `401`s therefore starts the daily delete and wakes the database. Worse, the once-a-day memory is per **process** (`RefusalRetentionTrigger.cs:14-16`), and with scale-to-zero every cold start is a new process, so the first request after each cold start purges. Move the trigger behind a request that has **already** reached the database: after successful authentication of a Session (the revocation read) or a Token (the lookup). Do not run it for the anonymous paths. Keep "never a timer" and "at most once a day per process".
  - [x] Test: with the purge due, `GET /api/v1/antiforgery/token` and `GET /api/v1/nothing` start no purge (a purge counter or log line is absent). Anchor: a signed-in `GET /api/v1/spaces` starts it, exactly once.
  - [x] **Clock step (Q12 (a), `deferred-work.md:334`).** At sign-in, issue the ticket's instant (`SessionRevocation.Stamp`) at `max(now, SessionsValidAfter)`. `SessionStanding.Admits` (`IAccountSessionStore.cs:56-58`) is `issued >= validAfter`, so equality admits. Test with a `FakeTimeProvider`-style clock (the Host already takes `TimeProvider`, `Program.cs:153`): sign out at T, step the clock back to T-10 min, sign in, and the new Session is admitted. Anchor: the pre-sign-out ticket is still `401`.

- [x] **Task 6 — Infrastructure as code: `infra/` (Bicep)** (AC: 1, 3, 4, 5, 6, 11, 12, 13)
  - [x] Q1 (a): Bicep, under `infra/` (`main.bicep` plus `modules/*.bicep`, and one `main.parameters.example.json` holding **no** values that are secrets). One environment. There is no `env` parameter that could name a second one (AC4), and nothing in `infra/` or `.github/` mentions `staging`.
  - [x] Resources:
    - **Log Analytics workspace**, the Container Apps environment's log destination. Stdout logs go here (AR-34). Set `workspaceCapping.dailyQuotaGb` to a small cap (e.g. 0.1) so logs cannot breach §6.3.
    - **Container Apps managed environment**, Consumption.
    - **Container App `yello-host`**:
      - single revision mode; `minReplicas` per Q6, `maxReplicas: 1` (AD-14);
      - ingress external, `allowInsecure: false`, `targetPort: 8080`;
      - liveness and readiness probes on `/health/live` and `/health/ready`;
      - user-assigned identity `id-yello-app`; registry pull through that identity (Q2);
      - environment: `ASPNETCORE_ENVIRONMENT=Production`, `ForwardedHeaders__Enabled=true`, `ClientOrigin=https://<SWA hostname or the custom client hostname>`, and `ConnectionStrings__yello` as a **Key Vault secret reference** (`keyVaultUrl` + `identity`). The key is `yello` — `YelloDatabaseResourceName` (`Directory.Build.props:51`), read through `BuildConstants.DatabaseResourceName()` (`Program.cs:73-74`).
    - **Container Apps Job `yello-migrate`**: manual trigger, the bundle image, identity `id-yello-migrate`, and its connection string also a Key Vault reference.
    - **Azure Container Registry**, Basic, admin user **disabled** (Q2). `AcrPull` for both identities and `AcrPush` for the deploy identity.
    - **Azure SQL** logical server, Entra-only authentication, administrator = Lee (P2), and database `yello`:
      - General Purpose serverless (`GP_S_Gen5`), `useFreeLimit: true`, `freeLimitExhaustionBehavior: 'AutoPause'` (AC5);
      - `autoPauseDelay: 15` (spine `:293`), `minCapacity: 0.5`;
      - the firewall rule "allow Azure services" (consumption Container Apps has no fixed egress). Record that trade-off in `docs/operations.md`.
      - **Do not set a database-level collation**: AD-15 sets collation per column, and Azure SQL cannot `ALTER DATABASE … COLLATE` (spine `:167`).
    - **Key Vault**, RBAC mode, `Key Vault Secrets User` for both runtime identities, and secrets `yello-app-connection` and `yello-migrate-connection`. Both are Entra managed-identity connection strings (Q8): `Authentication=Active Directory Managed Identity;User Id=<client id>;Encrypt=True;Connect Timeout=60`. They contain no password, but AR-37 still puts them in Key Vault. The ACS key slot is **named but not created** (Q9).
    - **Static Web App**, Free, and an optional custom domain (P4).
    - **Action group** (email, P1).
    - **Metric alert** on the database's `free_amount_remaining` metric: fires at `<= 10000` vCore-seconds, i.e. 10% of the 100,000 monthly allowance (AC5). Confirm the metric's exact name and unit in the Azure SQL free-offer documentation when writing it, and cite the page.
    - **Consumption budget** on the resource group at £30/month, alerting at 50/80/100% (Q16).
    - Optionally (Q14): a log-search alert on the rate-limit refusal log line (AR-39's second signal), with its monthly cost stated.
    - **Deploy identity** `id-yello-deploy` with a **federated identity credential** for the GitHub repository's `main` branch / `azure` environment (P3), and the least roles it needs (Container App and Job update, `AcrPush`, SWA deployment token read). Lee runs the first deployment (P2).
  - [x] Outputs: the Container App FQDN, the SWA hostname, the registry login server and the identity client ids. The deploy workflow consumes them.
  - [x] **Local verification (no subscription):** `az bicep build --file infra/main.bicep` with zero warnings (Bicep's linter: `bicepconfig.json` with `"analyzers": { "core": { "rules": { … "level": "error" } } }` for the rules that matter — no secrets in outputs, secure params, no hard-coded locations). Record the Bicep CLI version. `az` 2.71 is installed. `az bicep install` downloads the CLI once and needs the network. Do **not** run `what-if` or any deployment: it needs a subscription (P1).
  - [x] **Cost table** in `docs/operations.md` (§6.3), each line with its source and date. Name the free grants: SQL 100,000 vCore-s, 32 GB data and 32 GB backup per month; Container Apps' monthly consumption grant; SWA Free; Log Analytics ingestion; Key Vault operations; ACR Basic's daily rate. Give the estimate for `minReplicas: 0` and for `minReplicas: 1` (spine `:425`: ~£12–15). The total must be stated against £30, and the ACR line must be in it.

- [x] **Task 7 — The client on Static Web Apps** (AC: 13, 1)
  - [x] `Yello.Client/wwwroot/staticwebapp.config.json`: `navigationFallback` → `/index.html`, excluding `/_framework/*`, `/css/*`, `*.{png,ico,json,wasm,dll,dat,blat,js,css}` as appropriate, so `/spaces/{id}` loaded directly reaches the router. Check that the file survives `dotnet publish` into `wwwroot`.
  - [x] *(Amended at code review, 2026-10-25, Lee's decision D1 (b): an empty `appsettings.Production.json` is committed so the boot configuration lists it, and the deploy overwrites the published copy and deletes its `.br`/`.gz`. Written after publish without a committed file, it was never fetched.)* The production `HostBaseAddress`: the deploy workflow writes `wwwroot/appsettings.Production.json` into the **publish output** from the IaC output (the Container App FQDN, or the custom API hostname). It is not a secret, and nothing is written into source. The client reads `HostBaseAddress` (`Yello.Client/Program.cs:82-86`). Blazor WebAssembly on SWA boots as `Production` unless told otherwise, so confirm that the environment name it loads is the one the file is named for (`Blazor-Environment` header, or the default).
  - [x] Test (Client suite, Playwright, no Host needed): serve the **published** output with a static server (not the dev server) on an OS port. Load `/spaces/<random guid>` directly: the app boots (no `#blazor-error-ui` visible) and routes. This proves the fallback works on a static host, using the same rule SWA applies. Only one rewrite is in scope: the test server implements `navigationFallback` by reading `staticwebapp.config.json`. If that is too heavy, test the file's rules as data and record the deviation.

- [x] **Task 8 — The container image, built and inspected locally** (AC: 3, 1, 6)
  - [x] Q-image: `dotnet publish Yello.Host -c Release -r linux-x64 /t:PublishContainer` (SDK container support: no Dockerfile, no new package). Set `ContainerRepository`, `ContainerImageTag` (the commit SHA in CI) and the base image `mcr.microsoft.com/dotnet/aspnet:10.0` pinned to the runtime patch (10.0.11, `Directory.Build.props:28`). The migration bundle gets its own small image (`mcr.microsoft.com/dotnet/runtime-deps:10.0` plus the self-contained bundle) through a minimal `infra/migrate.Dockerfile`, or by `PublishContainer` on a tiny wrapper. Rec: the Dockerfile, because there is no project to publish.
  - [x] Verified locally, with Rancher's `docker` (memory: `yello-container-runtime-is-rancher`):
    - (i) the image builds;
    - (ii) `docker run -e ConnectionStrings__yello=… -e ClientOrigin=… -e ForwardedHeaders__Enabled=true -p 8080` against a migrated database on a local SQL container answers `/health/live` `200`, and a sign-in through `X-Forwarded-Proto: https` gets a `Secure` cookie;
    - (iii) with **no** database reachable, both probes still answer `200`;
    - (iv) **no secret is in the image.** `docker image inspect` shows no `ConnectionStrings*` env, and a scan of the image's filesystem (`docker export | tar -t` plus `grep` over `appsettings*.json`) finds no connection string, `Password=` or key. Record the commands and output in the Debug Log.
  - [x] The migration image is run once against a fresh local database, and the equivalence test (Task 3) covers its content.

- [x] **Task 9 — CI: `.github/workflows/ci.yml` (the PR stage)** (AC: 10, 16)
  - [x] Triggers: `push` to `main`, `pull_request` to `main`. `ubuntu-latest`, which has Docker. `actions/setup-dotnet` from `global.json` (SDK 10.0.303). Pin every third-party action to a full commit SHA, with the version in a trailing comment.
  - [x] Steps, in order (`TESTING-CONVENTIONS.md:526-532`, "architecture suite **first**"):
    1. `dotnet restore`, then `dotnet tool restore`.
    2. `dotnet build Yello.slnx -c Release`. Warnings are errors. The NuGet audit runs here, under the one-off suppression (AC17).
    3. `dotnet dev-certs https` (created, not trusted — `TESTING-CONVENTIONS.md:241-243`).
    4. `pwsh tests/Yello.Tests.Client/bin/Release/net10.0/playwright.ps1 install chromium --with-deps` (`:142-159`).
    5. Build the migration bundle and export `YELLO_MIGRATION_BUNDLE`.
    6. `dotnet test --project tests/Yello.Tests.Architecture` (fails fast).
    7. `dotnet test Yello.slnx -c Release` (the gate; `--no-build` runs zero tests, memory `full-test-run-starves-browser-suite`, so build first and do not pass `--no-build` unless the counts are verified).
  - [x] Environment: `YELLO_REQUIRE_CONTAINER_RUNTIME=1` and `YELLO_REQUIRE_MIGRATION_BUNDLE=1`. Q19 (a): `SqlServerContainerFixture.IsContainerRuntimeAvailable()` / `SqlServerEngine` treat an **unconfigured** runtime as a failure when the variable is set. Today an unconfigured runtime **skips** (`TESTING-CONVENTIONS.md:445-450`), so a CI agent without Docker would pass SM-1 by skipping it. Test it in `ContainerRuntimeProbeTests` (`tests/Yello.Tests.Slices/ContainerRuntimeProbeTests.cs`): with the variable set and no runtime configured, the reason is a failure, not a skip.
  - [x] Upload the TRX/MTP results as an artefact. Use the `concurrency` group per ref. Stay under the 15-minute PR target (`qa:402-435`). Record the measured wall clock from a local run of the same command sequence; a hosted run is Lee's (P6). If it is over 15 minutes, follow the contingency: shard by surface, then move I-16/I-17 (`arch:604-609`).
  - [x] **Do not create Nightly or Weekly workflows.** Q16: no suite exists for them yet (accessibility E2E, Board at 5,000, load and cold start all arrive with epic 2+). An empty stage is a gate that passes vacuously. Ledger it to **2.9** (Task 13).
  - [x] `actionlint` (1.7.12 installed) on every workflow: zero findings. Record it.
  - [x] **AC16, test-estate debts:**
    - (a) `SqlServerEngine`'s dead-runtime path (`tests/Yello.Tests.Shared/SqlServerEngine.cs:184-194`). `ProbeContainerRuntimeAsync` is static, so add an internal seam (a constructor taking the probe delegate, used only by tests via `InternalsVisibleTo` or a test-only factory in Shared). Then prove: with the probe reporting dead, `CreateDatabaseAsync` throws with the probe's reason, **constructs no container**, and a second call rethrows the cached failure without probing again (count the probe calls).
    - (b) `RunningHost`'s cursor (`RunningHost.cs:303-337, 331-365`). Tag the harness's own requests with a header (e.g. `X-Yello-Harness: <cursor id>`) and count only request-finished lines for those. Kestrel's request-finished line does not carry headers, so log the tag through a test-only Host setting the suites already use for request-finished lines (story 1.9 decision, Information level by environment). Or correlate by `TraceIdentifier` the handler records from a response header. The design is the dev's. Prove it with a test that interleaves untagged requests from a second `HttpClient`, and assert that `LogSinceAsync` waits for the tagged line.
    - Close `deferred-work.md:400` and `:401` with dated notes.

- [x] **Task 10 — Deploy: `.github/workflows/deploy.yml`** (AC: 1, 3, 4, 15)
  - [x] Trigger: `workflow_run` of CI on `main` with `conclusion == success`, or a single workflow with `needs:`. Rec: a `deploy` job in the CI workflow with `needs: [test]` and `if: github.event_name == 'push' && github.ref == 'refs/heads/main'`, so that no deploy follows a red run. Use the GitHub environment `azure` (exactly one environment, AC4). `permissions: id-token: write, contents: read`. `azure/login` by OIDC with the three repository variables (P3). No Azure secret is stored.
  - [x] Jobs, **in this order** (AR-38):
    1. **build-images**: Host image and migration image, tagged with the SHA, pushed to ACR.
    2. **migrate**: `az containerapp job update --image <migrate:sha>`, then `az containerapp job start`. Poll the execution until it ends, and **fail the workflow** unless it succeeded.
    3. **promote**: `az containerapp update --image <host:sha>`. In single revision mode this creates and activates the new revision.
    4. **client**: `dotnet publish Yello.Client -c Release`, write `appsettings.Production.json` (Task 7), then `Azure/static-web-apps-deploy` with the deployment token **fetched at run time** (`az staticwebapp secrets list`) and passed as a masked step output. No stored token.
  - [x] **The migrate-before-promote order is gated** (Architecture, text-based, Q18): the `promote` job `needs` the `migrate` job, and nothing in the repository starts the Host image before the migration job. Plant: drop the `needs` → caught. The gate reads the YAML as text with anchors. **Do not add a YAML parser package** for one gate (a new pin for a test is a pin edit). If text proves too brittle, record that and add `YamlDotNet` as a non-AR-1 test pin, with the reason.
  - [x] **Expand/contract rule** (Q5), written into `docs/operations.md` and `TESTING-CONVENTIONS.md`'s migration paragraph: the migration runs while the **previous** revision is still serving, so every migration must be compatible with the code of the revision before it. Add columns nullable or defaulted. Drop or rename only in a later release. A migration that breaks the running revision is a defect. No migration in the repository today violates this. Check the seven and say so.
  - [x] Verified locally: `actionlint`, and the order gate. The real run is P6.

- [x] **Task 11 — Secret hygiene and the two-environment rule, at build** (AC: 3, 4)
  - [x] Architecture gate, scanning `infra/**`, `.github/**`, every `appsettings*.json` (Host and Client), `launchSettings.json`, and `Yello.AppHost/*`. It fails on credential-shaped content: `Password=`, `Pwd=`, `AccountKey=`, `SharedAccessKey=`, `client_secret`, a `-----BEGIN`, a 32+ char base64 run assigned to a key named like a secret. `Yello.Host/appsettings*.json` carry **no** `ConnectionStrings` section. The scan must first find the files it names (a known `appsettings.json`) before asserting.
    - Plant 1: a connection string with a password in `appsettings.json` → caught.
    - Plant 2: `ConnectionStrings__yello` as a plain (non-Key-Vault) env value in `main.bicep` → caught (assert the container env entry uses `secretRef`).
  - [x] Two environments: no `staging`, `stage`, `uat`, `preprod` or `dev` deployment target in `infra/` or `.github/` (case-insensitive; `Development` as the ASP.NET environment name of the Local environment is allowed and named). Plant: a second `environment: staging` → caught.
  - [x] The Bicep `maxReplicas: 1`, `useFreeLimit: true`, `freeLimitExhaustionBehavior: 'AutoPause'`, the 10% threshold, and the Key Vault reference are each asserted by the same text-gate family. Each one is planted once.

- [x] **Task 12 — `docs/operations.md`: the operational record, and the runbook** (AC: 5, 7, 8, 9, and P1–P7)
  - [x] A new file (the AC requires the assertion "in writing"; Q18 chooses the place). Sections:
    1. **Environments**: Local and Azure, nothing else.
    2. **§6.3 cost ceiling**: the cost table (Task 6), the budget and the vCore alert, and the auto-pause behaviour.
    3. **§6.4 data-protection gate** (AC8): verbatim the single-operator assertion, the non-compliance trigger, and the five prerequisites, each with state "not live; becomes live at the gate".
    4. **The tripwire** (R11, `test-design-architecture.md:556-565`): an operator-side SQL query counting Accounts. §6.1 forbids an in-product cross-Space aggregate, so it is a **direct datastore query, not an endpoint**. For example `SELECT COUNT(*) FROM dbo.Account;`, run as the Entra admin. More than one Account means the gate has fired. Never build an endpoint for it.
    5. **The parked obligations** (AC9): the browser spellcheck / cloud IME egress (`EXPERIENCE.md:181`), with the reason quoted; and subject-access/export held "behind the §6.4 gate" (`addendum.md:164`).
    6. *(Code review, 2026-10-25: the record and the procedure are written, but the pooled-connection override below was ledgered rather than built; see `deferred-work.md`, "AR-40c's pooled-connection check cannot target an external database".)* **AR-40c** (AC7): the status ("not confirmed; `MAXDOP = 1` in force") and the procedure (P5). Run `Yello.Tests.Isolation`'s pooled-connection case against the deployed database (a connection-string override, read from an env var by the pooled world, documented here; the suite never reads a production database unless that variable is set), then relax only by a **new migration** setting `MAXDOP = 0`, and only if that case is green. Record the R3 interaction (`arch:474-488`): relaxing MAXDOP changes the cold-path latency 2.9 measures.
    7. **AR-40b** (Q6): the provisional statement, either "NFR-5 is measured warm; the cold path is exempt until story 2.9 measures it" or "min replicas 1". Never silent (spine `:425`, `quality-budgets.md:48`).
    8. **NFR-6 transit**: what carries TLS on each hop.
    9. **Identities and permissions**: app vs migrate vs deploy. `deferred-work.md:242`: the migrate identity needs `ALTER ANY DATABASE SCOPED CONFIGURATION` (MAXDOP) and the rights to create functions and security policies. The app identity has `db_datareader`/`db_datawriter` only, so it cannot disable RLS.
    10. **The first-deploy runbook**: P1–P7 in order, with the exact commands. This includes the T-SQL for the two database users, the federated credential, the first `az deployment group create`, the first workflow run, and the post-deploy measurements (P6).
    11. **What is not observed yet**: everything that needs Azure. List it explicitly.
  - [x] Do not edit the PRD, the spine or `EXPERIENCE.md`. AC8/AC9 *carry* the gate. They do not amend its sources.

- [x] **Task 13 — Ledger, conventions, the conditional audit check, runs** (AC: 17, all)
  - [x] **AC17**, without changing any AR-1 pin:
    - Run `dotnet list Yello.slnx package --vulnerable --include-transitive` **with the suppressions in place**. It lists advisories regardless of `NuGetAuditSuppress`, so verify that it does. Also check the newest stable `Microsoft.Data.SqlClient` and its `System.Security.Cryptography.Pkcs` dependency, and the newest stable Aspire against `MessagePack`.
    - If a stable SqlClient brings Pkcs >= 9.0.21, bump `Microsoft.Data.SqlClient`. It is **not** an AR-1 pin (`Directory.Packages.props`, "Not an AR-1 stack choice"), but Aspire.Hosting.SqlServer 13.4.6's floor and EF Core's floor must still be satisfied. Then remove the two Pkcs suppressions **only if** the AppHost's Pkcs 10.0.8 path is also cleared (it arrives through Aspire).
    - An Aspire bump is an **AR-1 architecture edit** (the 13.4 line is pinned). Do not take it. If Aspire is the only remaining cause, the MessagePack and Pkcs-10 suppressions stay. Record that, and ask Lee as Q20.
    - Record the date, the versions checked and the outcome in the Completion Notes, and update `deferred-work.md:396` (not closed unless the ItemGroup is gone).
  - [x] Ledger (`deferred-work.md`). CLOSE with dated notes what this story discharges: `:92` (client production address), `:169` (https dev loop), `:203` (NFR-6 transit in deployment config), `:242` (deploy principal), `:290`, `:304`, `:314`, `:334`, `:336`, `:391`, `:400`, `:401`.
    - Update, not close: `:147` (work-factor re-measure → P6, an operator measurement; the remedy precondition at `:225` stands); `:396` per AC17.
    - ADD: (i) to **2.9**: the Nightly and Weekly CI stages, created with their first suites (Board at 5,000, load, cold-start characterisation) and AR-40b's measurement in the stated condition; (ii) to **4.1**: the ACS resource, its Key Vault secret and the `ConnectionStrings`-style env reference, created with the outbox (Q9); (iii) to **7.3**: the single-revision swap briefly runs two processes, so an in-process push (AD-9) and the `/sync` lease cannot assume one process across a deploy — test V-13 (leases do not survive a restart) covers reconnection; (iv) whatever this story finds and does not fix.
  - [x] `tests/TESTING-CONVENTIONS.md`: replace "CI tiering, for context" (`:526-532`) with what was built. Add the CI environment variables, the migration-bundle equivalence test, the probe and forwarded-header tests, and the new counts.
  - [x] Runs: `dotnet build Yello.slnx` (warnings are errors, no overrides) and `dotnet test Yello.slnx`. Baseline at `9de3ff3`: **504 total, 503 passed, 0 failed, 1 skipped** (the legacy-factor duration case), 4 engines, 3.4 GB Docker peak. Record the new totals, the engines (still ≤ 5, `ContainerTopologyGateTests`) and the wall clock. If Rancher's socket times out: `rdctl shutdown`, `wsl --terminate rancher-desktop`, then relaunch (memory note).
  - [x] Plants: one at a time, in the foreground, original content held in memory and restored in a `finally`. Compare SHA-256 of the tree to a pre-run snapshot after each. **Never** `git checkout --`, `git restore` or `git reset`. Record `| # | Plant | Gate | Caught |` in the Debug Log. Minimum set: a production `Migrate()` call; a probe that opens a connection; a password in `appsettings.json`; a plain connection-string env in Bicep; `maxReplicas: 2`; `freeLimitExhaustionBehavior: 'BillOverUsage'`; a `staging` environment; `promote` without `needs: migrate`; `ForwardLimit` removed (a rotated left-hand XFF escapes the window); Data Protection persistence removed (the cross-process cookie test fails); the purge trigger back on every request; the revocation read's `503` mapping removed (a `500` appears); `YELLO_REQUIRE_CONTAINER_RUNTIME` ignored.
  - [x] **Do not add review narration to source files** (owned by Lee, project-wide). Do not tick this unless the diff holds it.

### Review Findings

Code review of `8bc54e5..HEAD`, 2026-10-25. Three layers (Blind Hunter, Edge Case Hunter, Acceptance Auditor): 57 raw findings, 29 after deduplication and triage (2 decision-needed, 21 patch, 6 defer), 13 dismissed.

- [x] [Review][Decision] The deployed client never loads `appsettings.Production.json`, so it never finds the API (AC13) — **Lee decided (b), 2026-10-25; applied:** an empty `Yello.Client/wwwroot/appsettings.Production.json` is committed; the deploy overwrites the published copy and deletes its `.br`/`.gz`, and fails on an empty API hostname; ARCH-015 now requires the committed file to exist and hold no value; E2E-001 asserts that the booted client calls `https://api.yello.test/api/...` and never its own origin; AC13 and Task 7 record the deviation from "never committed". Original finding: The deploy job writes the file into the publish output **after** `dotnet publish` (`.github/workflows/ci.yml:230-231`). The .NET 10 WebAssembly loader fetches only the appsettings files listed in the boot config embedded in `dotnet.*.js`. Verified on a Release publish of this tree: the list is `["../appsettings.Development.json"]` only, and the loader filters on it (`"appsettings.json"!==n&&n!==\`appsettings.${e.applicationEnvironment}.json\`` over `e.appsettings`). So in Azure `HostBaseAddress` is empty, and `Yello.Client/Program.cs:82-86` falls back to the client's own origin: every API call goes to the Static Web App. E2E-001 writes the same file the same way but asserts only routing, so it cannot see this. Adding the file as a linked `Content` item fed by an MSBuild property was tried: the static-web-assets compression task fails (MSB4018). Options: (a) the deploy job writes `Yello.Client/wwwroot/appsettings.Production.json` **before** `dotnet publish` (git-ignored, never committed; ARCH-015's "not committed" gate holds). E2E-001 then has to stage the file into the source `wwwroot` around its publish, or the order is gated only as text. (b) Commit a value-free `Yello.Client/wwwroot/appsettings.Production.json` (`{}`) so the boot config always lists it. The deploy job overwrites the published copy and deletes its `.br`/`.gz` siblings. ARCH-015 is inverted to "committed and holds no value", and E2E-001 asserts that the booted client calls `https://api.yello.test/`. This deviates from Task 7's "never committed". (c) The client reads its API address from a file of its own (for example `/host.json`) that it fetches at boot, outside the appsettings mechanism: a client code change. **Recommend (b)**: the test then exercises exactly what the deploy does, and the committed file holds no value.
- [x] [Review][Decision] Every `DbException` is answered as "database briefly unavailable" (AC14) — **Lee decided (a), 2026-10-25; applied:** `Yello.Infrastructure/Persistence/DatabaseUnavailability.cs` classifies SQL Server connection and availability errors (a number allow-list including 40613, plus severity 20 or above) anywhere in the exception chain; `DatabaseAvailability` maps only those to the `503`, and everything else stays a `500` (UNIT-008). Original finding: `Yello.Host/Deployment/DatabaseAvailability.cs:74-85, 98`. `IsDatabaseFailure` matches any provider exception in the chain. That includes a unique or foreign-key violation, an RLS block-predicate violation (33504), a deadlock, a permission denial for the app user, and an invalid-object error from a migration that broke expand/contract. Each becomes a retryable `503` with `Retry-After: 30`, logged at Warning as "could not be reached". Defects and possible cross-Space write attempts are hidden, and clients are told to retry. Task 5's text names `DbException`/`SqlException`, while AC14's intent is "paused, resuming or unreachable". Options: (a) narrow the mapping to connection and availability failures (a `SqlException` number allow-list such as -2, 53, 4060, 40197, 40501, 40613, 10928/10929, plus the key-ring load), classified in `Yello.Infrastructure` so the Host does not learn SqlClient; everything else stays a 500. (b) Keep the broad mapping, but log the non-availability errors at Error with the provider error number. (c) Keep it as built. **Recommend (a).**
- [x] [Review][Patch] A sign-out after a clamped sign-in revokes nothing [Yello.Infrastructure/Persistence/AccountSessionStore.cs:38-45; Yello.Host/Sessions/SessionRevocation.cs:74-75]
- [x] [Review][Patch] `YELLO_REQUIRE_CONTAINER_RUNTIME` is not honoured by `StartupConnectivityCheckTests`, `HostStartupTests`, `SharedFixtureSmokeTest` or `ContainerRuntimeProbeTests:63`, which still skip in CI (AC10) [tests/Yello.Tests.Shared/SqlServerContainerFixture.cs:297]
- [x] [Review][Patch] The 503 keeps headers set before the failure (`Set-Cookie` included) [Yello.Host/Deployment/DatabaseAvailability.cs:108-117]
- [x] [Review][Patch] Promote does not wait for the new revision to be healthy, so a crash-looping image still gives a green deploy [.github/workflows/ci.yml:194-197]
- [x] [Review][Patch] The migration poll aborts on one transient `az` failure, its 30-minute loop equals the job timeout, and a timed-out execution is left running [.github/workflows/ci.yml:158-177]
- [x] [Review][Patch] An empty API hostname is published as `https:///` [.github/workflows/ci.yml:225-231]
- [x] [Review][Patch] The tested bundle is kept for one day, so re-running a deploy job after that fails [.github/workflows/ci.yml:96]
- [x] [Review][Patch] Any `Cookie` header, not only Yello's, loads the key ring and wakes the database [Yello.Host/Deployment/DatabaseAvailability.cs:91-94]
- [x] [Review][Patch] Remarks say that the ring is read lazily and that probes and anonymous requests never read the database, but the framework's `DataProtectionHostedService` loads the ring when the process starts [Yello.Host/Deployment/PersistedKeyRing.cs:27-29; Yello.Host/Spaces/RefusalRetentionTrigger.cs:18-24; docs/operations.md:262]
- [x] [Review][Patch] Stale or contradictory remarks: ProbeTests ("purge offered on every request"), DatabaseUnavailableTests ("before F1 is fixed"), DatabaseIdentityTests (Entra users vs Q8 (b)), ARCH-010 ("to be confirmed"), ARCH-013 ("Q10"), MigrationBundleEquivalenceTests ("under `aspire run`"), and operations.md ("all eight migrations"; there are nine) [tests/Yello.Tests.Slices/Deployment/*; tests/Yello.Tests.Architecture/DeploymentShapeTests.cs; docs/operations.md]
- [x] [Review][Patch] operations.md says the deploy identity "holds no data access", but Contributor on the job and the app reaches both connection secrets [docs/operations.md:122]
- [x] [Review][Patch] The P3 runbook leaves the `azure` environment open to any branch, so any branch can mint the deploy token [docs/operations.md:183]
- [x] [Review][Patch] The P4 runbook deploys the custom domain before the CNAME exists [docs/operations.md:198]
- [x] [Review][Patch] ARCH-016 does not check that the Host waits on the `migrate` resource [tests/Yello.Tests.Architecture/DeploymentShapeTests.cs]
- [x] [Review][Patch] The migrate-before-promote gate ignores an `if: always()`/`failure()`/`cancelled()` on a deploy job [tests/Yello.Tests.Architecture/DeploymentShapeTests.cs:200-201]
- [x] [Review][Patch] The bundle equivalence comparison omits foreign keys, default, check and key constraints [tests/Yello.Tests.Slices/Deployment/MigrationBundleEquivalenceTests.cs:58-81]
- [x] [Review][Patch] Test child processes are not killed on timeout, the static host's serve loop dies on any non-listener exception, and `Resolve`'s root check has no trailing separator [tests/Yello.Tests.Client/PublishedClientHost.cs:169, 213-231, 265; MigrationBundleEquivalenceTests.cs:193]
- [x] [Review][Patch] The AppHost migrate block splits the `ClientOrigin` comment from the code it describes [Yello.AppHost/Program.cs]
- [x] [Review][Patch] The ledger closes `:169` (the dev loop "needs a browser") on a curl-only run, and Task 12 §6 is ticked while the pooled-connection override was ledgered rather than built [deferred-work.md; this file]
- [x] [Review][Patch] ARCH-017 accepts a §6.4 prerequisite named anywhere in the file, so a dropped table row passes (plant 20) [tests/Yello.Tests.Architecture/OperationsRecordTests.cs]
- [x] [Review][Patch] ARCH-010's threshold is not tied to the free-allowance alert, and ARCH-007 does not check `dev`/`stage` in workflows [tests/Yello.Tests.Architecture/DeploymentShapeTests.cs]
- [x] [Review][Defer] The deploy identity probably lacks `managedEnvironments/join/action`, `userAssignedIdentities/assign/action` on both runtime identities, and registry read for `az acr login` [infra/main.bicep] — deferred, Azure-only, owner Lee (P2/P6)
- [x] [Review][Defer] One Warning line per rate-limit refusal can exhaust the 0.1 GB daily log cap during a flood, blinding the alert [Yello.Host/Sessions/CredentialHashingRateLimit.cs; infra/modules/monitoring.bicep:23] — deferred, owner story 2.9 (load)
- [x] [Review][Defer] The synchronous key-ring read can hold request threads for up to `Connect Timeout=60` against a resuming database [Yello.Infrastructure/Persistence/KeyRingStore.cs] — deferred, owner story 2.9 (cold start)
- [x] [Review][Defer] With a custom client hostname, CORS refuses the still-live `*.azurestaticapps.net` hostname [infra/main.bicep:264, 278] — deferred, owner Lee (P4)
- [x] [Review][Defer] Template operations hazards: a redeploy without `hostImage` reverts to the placeholder image, a first deploy without both connection strings fails, a soft-deleted Key Vault blocks a same-name redeploy, and `namePrefix` is checked only for length [infra/main.bicep] — deferred, owner Lee (P1/P2)
- [x] [Review][Defer] The Token-lookup 503 path and the sign-in path's clock clamp are tested only through the middleware's generality and the pure function [tests/Yello.Tests.Slices/Deployment/DatabaseUnavailableTests.cs] — deferred, owner Epic 1 retrospective

## Dev Notes

### Findings this story must not miss

**F1 — No Data Protection key persistence exists, so every revision and every cold start would end every Session.** `grep DataProtection` over `*.cs` finds nothing. The Session is an encrypted cookie ticket with no row (`SessionRevocation.cs:15-20`), and the anti-forgery tokens use the same key ring. In a container the ring lives in the container's file system and is regenerated on each start. The defect is invisible locally, because the default ring lives in the user profile and every local Host process shares it; only a fresh profile reproduces the container (Task 4's test). With `minReplicas: 0`, a cold start follows every idle spell of a few minutes, and each one would sign everybody out and void every anti-forgery token held in an open tab. The client would read this as a `401` and land on `/sign-in?reason=session-ended`. That is AC12, and Task 4 fixes it.

**F2 — The default Azure hostnames make the Session cookie third-party.** AD-7 puts the client and the API on distinct origins, which is fine. But `*.azurestaticapps.net` and `*.azurecontainerapps.io` are different **sites**, so the `SameSite=None; Secure` cookie is a third-party cookie. Chromium accepts it, and story 1.9 proved it across `127.0.0.1` origins (same site). **Safari blocks third-party cookies by default.** Firefox's Total Cookie Protection partitions them: they still work within this one top-level site, but this has not been verified. The fix is P4 (one registrable domain for both). This is **not** an AD-7 change: the origins stay distinct and CORS stays exact. The story makes the IaC take the custom hostnames as optional parameters and records the browser consequence in `docs/operations.md`. It does **not** switch to SWA's linked backend: that needs the Standard plan (a fixed monthly cost against §6.3) and would make the API same-origin, which *is* an AD-7 change.

**F3 — Behind ingress, the Host cannot issue an anti-forgery token without `X-Forwarded-Proto`.** See Task 4. The ledger entries about rate limiting (`:290`, `:304`) are the visible half. The anti-forgery refusal (`deferred-work.md:171`) is the half that would make the first deploy unusable.

**F4 — `aspire run` never migrates its database.** Task 3. The Local environment that AC4 names is not functional today without a manual migrate.

**F5 — The idempotent SQL script cannot carry these migrations.** Task 3. Use the bundle.

**F6 — `AddHealthChecks()` registers a hosted service.** Task 4. Probes are plain endpoints.

**F7 — `EnableRetryOnFailure` is incompatible with the explicit transactions Space resolution holds.** Task 5.

### Ledger entries owned by 1.10 — every one folded or stated

| Entry (`deferred-work.md`) | Where it lands |
|---|---|
| `:391` push-triggered CI run (1.9 AC5) | Task 9, AC10 |
| `:396` one-off `NuGetAuditSuppress` ItemGroup | Task 13, AC17, conditional. Removed only for an advisory a stable, in-policy bump clears. Never an Aspire bump (AR-1) without Lee. |
| `:400` `RunningHost` log cursor | Task 9 (b), AC16 |
| `:401` `SqlServerEngine` dead-runtime fail-fast test | Task 9 (a), AC16 |
| `:242` deploy principal needs `ALTER ANY DATABASE SCOPED CONFIGURATION` | Tasks 3, 6, 12 (§9 identities), AC15 |
| `:290` rate-limit window behind a proxy | Task 4, AC11 |
| `:304` partition: missing address, IPv6 /64, IPv4-mapped | Task 4, AC11 |
| `:314` purge trigger wakes an idle database | Task 5, AC14 |
| `:334` clock step back refuses fresh sign-ins | Task 5, AC14 |
| `:336` revocation read failure is a `500` | Task 5, AC14 |
| `:203` NFR-6 transit clause | Task 4 (HSTS, forwarded proto), Task 6 (ingress, `Encrypt=True`), Task 12 §8 |
| `:92` "Story 1.10 owns what production looks like" (client address) | Task 7, AC13 |
| `:169` the `https` dev loop never run | Task 3 (local parity run) |
| `:147` work factor re-measured on the deploy target | **Out of scope until deployed**: P6, an operator measurement recorded in `docs/operations.md`. `:225`'s precondition stands: no raise without the remedy. Update the entry, do not close it. |
| `:215` `SequentialGuidIdentifierGenerator` ("arrives with deployment (story 1.10) or scale-out") | **Not 1.10's**: owned by 2.9. With `maxReplicas: 1` there is one singleton per process. The single-revision swap's brief overlap runs two processes, which is two generators. A duplicate id is caught by the primary key (`:221` says that path is patched), so the remaining effect is index locality, which is 2.9's. State this in the Completion Notes and do not change the generator. |
| Readiness issue 6 / AR-40c (`sprint-status.yaml:62-63`) | AC7, P5, Task 12 §6 |
| AR-40b (2.9/1.10) | Q6, Task 12 §7. Measurement stays with 2.9. |

### Current state of the files this story changes — read each before editing

| File | Today | This story | Preserve |
|---|---|---|---|
| `Yello.Host/Program.cs` | Composition root. `UseRequestLocalization` → `UseCors` → startup logs → `UseAuthentication` → `UseRateLimiter` → `app.Use(RefusalRetentionTrigger.InvokeAsync)` on every request (`:232`) → connectivity check (Development only) → `WarmPasswordHashing` → `MapApiGroup` routes → `MapUnmatchedApi` → `SpaceScopedRouteCheck`. EF command log filtered to Warning (`:68`). No probes, no forwarded headers, no Data Protection, no HSTS. | `UseForwardedHeaders` first (configured on); Data Protection persistence; `UseHsts` outside Development; probes on `app`; purge trigger moved; `503` mapping for a database failure. | Middleware order rationale (CORS before auth; rate limiter after auth and before endpoints); the EF command-log filter (AD-23); every startup log line; `SpaceScopedRouteCheck` last; no `AddAuthorization`. |
| `Yello.Host/Sessions/CredentialHashingRateLimit.cs` | `PartitionOf` = `RemoteIpAddress?.ToString() ?? ""` (`:124-125`). | Normalised partition. | Uniform `429`, never the submitted address, both limits, configurable permits. |
| `Yello.Host/Sessions/SessionRevocation.cs` | DB read in `OnValidatePrincipal` with no fallback (`:78-92`). | Failure → `503` problem. | No cache; reject plus sign out of a dead ticket; full-precision issue instant. |
| `Yello.Host/Spaces/RefusalRetentionTrigger.cs` | Runs on every request, purge at most daily per process, not awaited (`:32-47`). | Runs only after a request already reached the database. | Never a timer; off the request path; failure logged. |
| `Yello.Host/Endpoints/SessionEndpoints.cs` | Sign-in stamps `now` (`SessionRevocation.Stamp`). | Stamps `max(now, SessionsValidAfter)`. | AD-23 uniformity of the sign-in answer and duration. |
| `Yello.AppHost/Program.cs` | SQL container, `AddDatabase("yello")`, Host waits for the database, both projects on `https` profiles, `ClientOrigin` injected (`:26-84`). | A migration resource the Host waits on (Q17). | Image from build metadata; the https profiles; the `ClientOrigin` injection gated by `CrossOriginConfigurationTests`. |
| `Yello.Infrastructure/Persistence/YelloDbContext.cs` (+ a new migration) | No `Migrate()` ever (`:15-20`). | Data Protection keys table (if Q7 (a)). | AD-2's two layers; the no-navigation rule; the hand-edit migration convention. |
| `Yello.Client/wwwroot/` | `appsettings.Development.json` only, with `HostBaseAddress: https://localhost:7093` and four `//` notes; `index.html`. | `staticwebapp.config.json`. `appsettings.Production.json` is committed empty and overwritten in the publish output at deploy (amended at code review, Lee's decision D1 (b)). | The Development file and its notes. |
| `Directory.Build.props` | Shared values; the labelled one-off suppression ItemGroup (Lee, 2026-10-24). | Only per AC17. | The ItemGroup's label and wording if it stays. |
| `Directory.Packages.props` | AR-1 pins and labelled non-AR-1 pins. | Possibly `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.11 (non-AR-1, labelled); possibly a SqlClient bump (AC17). | Every AR-1 pin. An AR-1 change is an architecture edit. |
| `tests/Yello.Tests.Shared/SqlServerEngine.cs`, `SqlServerContainerFixture.cs`, `RunningHost.cs` | One engine per process; dead runtime fails, unconfigured skips; cursor counts the harness's sent requests (`RunningHost.cs:344`). | Probe seam; CI "require runtime" switch; tagged cursor. | The topology (≤ 5 engines), the memory cap, the 5-minute start deadline. |
| `tests/TESTING-CONVENTIONS.md` | "CI tiering, for context" (`:526-532`) says 1.10 builds it. | What was built. | The rest. |

Production code beyond these is **not expected**. In particular, no endpoint, no `/api` route and no product behaviour change: the isolation gap gate would demand a row for any new `/api` route.

### Architecture constraints that bind here

- **AR-36 / spine `:275`**: migrations include the RLS policies and the filtered Owner index, and are applied as an explicit deploy step, never on application start. **AR-38 / spine `:331`**: GitHub Actions; the SWA deploy action; a container build plus revision update; migrations as an explicit job before the revision is promoted.
- **AR-37 / spine `:273`**: environment variables only; Key Vault through managed identity in Azure, user-secrets locally; no secret in source, appsettings or an image.
- **AD-10 / AR-33**: no timer, and probes from process state. `No_production_code_runs_on_a_timer_or_as_a_hosted_service` (A-9) stays green. Cleanup at most daily, piggybacking on traffic.
- **AD-14**: at most one replica. In single revision mode the platform briefly overlaps old and new during a swap. Record it (ledger to 7.3); it does not change `maxReplicas`.
- **AD-7**: the client origin and the API origin stay distinct. CORS is exactly the configured origin with credentials. Do not make the API same-origin.
- **AD-2**: `MAXDOP = 1` stays until AR-40c is closed (AC7). Relaxing it is a **new migration**, never a portal change, so it travels with the schema (`ActiveSpaceResolution.cs:45-55`).
- **AD-15**: never a database-level collation in IaC.
- **Spine `:278` Operations**: alert at 10% of the monthly `Free amount remaining`, auto-pause on exhaustion, never paid overage. Free-tier exhaustion and rate-limit refusals are the two signals worth alerting on.
- **Spine `:272`**: ingress request timeout 240 s (relevant to the resume window; `/sync` heartbeats are epic 7's).
- **AR-34 logging**: stdout only, structured; never a password, Token, cookie or Space name. The new startup lines (forwarded headers, Data Protection) log settings, never values from requests.
- **AR-1**: the stack pins (`Directory.Packages.props`) are architecture edits. New packages are non-AR-1, labelled with a reason, and in-band with the runtime where Microsoft ships them.

### Testing requirements

Conventions (`tests/TESTING-CONVENTIONS.md`): underscore-sentence names, one behaviour each; traits `Suite`, `Priority`, `Requirement` (cite the AR/NFR **and** the AD, e.g. `AR-36`, `AD-10`); randomised data; no `Task.Delay` (poll with a deadline); every absence anchored on a presence and validated by a plant; every gate finds its subject before asserting. Wire tests use `HostProcess` / `RunningHost` (Shared) on the shared engine. A new container-backed class creates a **database**, never an engine. MTP: `dotnet test`, never the `.exe`, when exit codes matter (`:399-426`).

Gates over non-C# files (Bicep, YAML, JSON) are text-based with anchors, like the CSS corpus gates from story 1.2. Each one names exactly what it checks, and each one is planted.

What is **not** tested here, and must be listed in `docs/operations.md` §11 rather than claimed: any Azure behaviour (ingress forwarding, Key Vault references, managed-identity SQL auth, the job's execution, the alert firing, auto-pause), Safari/Firefox cookie behaviour on real hostnames, and hosted CI wall clock.

### Previous story intelligence (1.9, 1.8, 1.7)

- **1.9 left AC5 half-closed on purpose**, and its Completion Notes say "Nobody should read AC5 as fully closed." This story closes it only in the repository: the workflow exists and passes `actionlint`, and the suites are strict. The first **hosted** run is P6. Say exactly that.
- **1.9 Q1**: an unconfigured runtime skips and a dead one fails. CI flips the first to a failure by variable (Task 9). Do not change the local default.
- **1.9 decision**: Isolation, Revocation and Client reference `Yello.Infrastructure` "for the schema only… Cut-able if 1.10 produces a migration script or bundle the harness can run instead." Do **not** cut the edge in this story. The bundle needs a build step that the suites cannot assume locally. Record it as possible future work in the Completion Notes.
- **1.9's memory lessons**: a full run with many engines starved Chromium. The topology is now 4 engines. Do not add a fifth: `ContainerTopologyGateTests` allows 5, but the bundle-equivalence and probe tests belong on Slices' engine.
- **1.9 Debug Log**: the plain build failed on the NuGet audit until Lee's one-off suppression. Lee chose the upgrade first, and no stable SqlClient fixed it (7.1.1 still brings Pkcs 9.0.18). Expect the same today, and check, don't assume.
- **1.8/1.7**: a source remark that claims a behaviour must be backed by a test. 1.7's dominant review finding was a false claim in a remark. The production files this story touches carry long dated comments; follow the "no review narration" rule.
- **Plant discipline**: 1.6 19/19, 1.7 28/29, 1.8 19/19, 1.9 15/15 (one only after a gate fix). Never revert with git.

### Git intelligence

- `9de3ff3` closed 1.9's review (the log-slice cursor now starts before seeding). `9e9aa4e` built the shared engine, `RunningHost`, `WireDatabase`, the case lists and the real-Host browser collection. `3cb7f43` built API Tokens. Commits are per phase, straight to `main`, by the orchestrator. Do not commit, push, branch or stash.
- There is no `.github/`, `infra/`, Dockerfile or `docs/operations.md` in the repository today. Everything under those paths is new.

### Latest technical notes (verify against the pinned versions; no AR-1 upgrades)

- **Azure SQL free offer** ([learn.microsoft.com/azure/azure-sql/database/free-offer](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer)):
  - 100,000 vCore-seconds of serverless compute, 32 GB data and 32 GB backup per database per month, for the lifetime of the subscription; up to 10 such databases per subscription.
  - "Behavior when free limit reached" is either auto-pause until the next calendar month or continue with billed overage.
  - The `Free amount remaining` / `Free amount consumed` metrics exist, and Microsoft suggests a no-cost alert on the first.
  - An open connection (an SSMS session left open) prevents auto-pause and burns the allowance. That is a runbook warning for P5/P6.
- **EF Core migration bundles** (`dotnet ef migrations bundle`): a self-contained executable, `--connection` at run time, idempotent against `__EFMigrationsHistory`. Use `EXEC` for any statement that must open a batch **only** if a script is ever generated ([EF Core custom operations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations)).
- **SDK container publishing**: `dotnet publish /t:PublishContainer` with `ContainerRepository`, `ContainerImageTag(s)`, `ContainerBaseImage`. The .NET 8+ ASP.NET images listen on `8080` (`ASPNETCORE_HTTP_PORTS`) as non-root.
- **Microsoft.Data.SqlClient 7.x and Entra auth (Q8): verify before relying on it.** SqlClient 7 moved the Azure Identity dependency out of the core package. Managed-identity authentication may need `Microsoft.Data.SqlClient.Extensions.Azure` (a new non-AR-1 pin) and an explicit registration. Check the 7.0.2 release notes. If it needs that package, either add it (labelled) or take Q8 (b). Never put a password in source either way.
- **Forwarded headers**: `ForwardedHeadersOptions.ForwardLimit = 1` processes only the right-most entry. `KnownNetworks`/`KnownProxies` default to loopback, so they must be cleared, or the platform's proxy is not trusted and nothing is forwarded.
- **GitHub Actions**: `azure/login` OIDC needs `permissions: id-token: write`. `Azure/static-web-apps-deploy` takes `skip_app_build: true` with `app_location` pointing at the published `wwwroot`. `az containerapp job start` returns an execution name; poll `az containerapp job execution show` for `Succeeded`/`Failed`.
- **Playwright 1.62.0** on Linux needs `install chromium --with-deps`.

### Project structure notes

- **New:**
  - `infra/main.bicep`, `infra/modules/*.bicep`, `infra/main.parameters.example.json`, `infra/bicepconfig.json`, `infra/migrate.Dockerfile`;
  - `.github/workflows/ci.yml` (with the deploy job, or a separate `deploy.yml`);
  - `docs/operations.md`;
  - `Yello.Client/wwwroot/staticwebapp.config.json`;
  - one migration (`…_DataProtectionKeys`) if Q7 (a);
  - tests: `Yello.Tests.Architecture/{DeploymentShapeTests.cs, MigrationAtStartupGateTests.cs, SecretHygieneTests.cs, ProbeGateTests.cs}`, `Yello.Tests.Slices/{Deployment/MigrationBundleEquivalenceTests.cs, Deployment/ProbeTests.cs, Deployment/ForwardedHeadersTests.cs, Deployment/DataProtectionPersistenceTests.cs, Deployment/DatabaseUnavailableTests.cs}`, `Yello.Tests.Client/StaticHostFallbackTests.cs`, `Yello.Tests.Shared` engine and cursor tests (names indicative).
- **Modified:** as in the current-state table, plus `deferred-work.md`, `TESTING-CONVENTIONS.md`, `sprint-status.yaml` and this file.
- **No new project.** If one becomes necessary (a migrator), it needs an `AllowedReferenceEdges` row, a solution-inventory entry and a reason. Prefer not to.
- Architecture tests that read `infra/`, `.github/` or `docs/` locate the repository root the way existing gates do (they already read `Directory.Packages.props` and CSS).

### References

- `_bmad-output/planning-artifacts/epics.md:180-181, 221-228, 353, 917-967`
- `prds/prd-YelloBMAD-2026-08-15/prd.md:697-702, 716-728, 740-747, 774-796, 884, 904`; `addendum.md:146, 164`
- `architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md:77-86, 114-118, 132-136, 156-160, 167, 272-278, 289-297, 301-331, 425, 427`
- `ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:23, 41, 181, 279`
- `specs/spec-yello/SPEC.md:22, 209, 217, 283`; `quality-budgets.md:23, 48, 57`; `harness-constraints.md:45, 85-104`
- `test-artifacts/test-design-architecture.md:116-117, 135-141, 179-182, 213-221, 314, 441-455, 474-488, 556-565, 604-609`; `test-design-qa.md:174, 210, 402-439, 488-492, 535-539`
- `implementation-readiness-report-2026-08-22.md:546, 549, 583-588, 654-656, 963, 1003-1005, 1119-1122, 1257-1261`
- `tests/TESTING-CONVENTIONS.md:9-54, 142-159, 241-243, 298-311, 313-375, 399-450, 526-532`
- `implementation-artifacts/deferred-work.md` (the ledger table above); `1-9-prove-isolation-on-both-surfaces.md` (Q1, Q6, Completion Notes, Debug Log "Build")
- Code: `Yello.Host/Program.cs`, `Yello.AppHost/Program.cs`, `Yello.Host/Sessions/{CredentialHashingRateLimit,SessionRevocation}.cs`, `Yello.Host/Spaces/RefusalRetentionTrigger.cs`, `Yello.Host/StartupConnectivityCheck.cs`, `Yello.Infrastructure/Persistence/{YelloDbContext,YelloDbContextFactory}.cs`, `Yello.Infrastructure/Persistence/Migrations/*`, `Yello.Client/Program.cs:82-105`, `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`, `tests/Yello.Tests.Shared/{SqlServerEngine,RunningHost}.cs`, `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs:684-704`

### Open questions — each with a recommended decision

The dev phase takes these as **reversible, recorded decisions** in the Completion Notes. None blocks starting. Questions that need Lee's money, tenant or signature are P1–P7 above, not here.

1. **IaC tool.** (a) Bicep under `infra/`: first-party, `az bicep build` validates it locally, and no state file. (b) Terraform: a state backend to host and pay for. (c) `azd`/Aspire publish: it would generate the infrastructure from the AppHost, but the AppHost is local orchestration by design (`Yello.AppHost/Program.cs:6-8`), and Aspire 13.4's generated shape (probes, Key Vault references, the free-offer flags) would need overriding anyway. **Recommend (a).**
2. **Container registry.** (a) ACR Basic: managed-identity pull, no stored credential (AR-37), and a small fixed daily cost inside §6.3. (b) GHCR: free, but Container Apps pulls it with a stored credential. **Recommend (a)**, with the cost in the table.
3. **Migration artefact.** (a) An EF Core migration bundle. (b) An idempotent SQL script: breaks on the batch-first DDL (Finding F5). (c) A migrator console project: a new project and edges for what the bundle already does. **Recommend (a).**
4. **Where the migration runs.** (a) A Container Apps Job inside Azure, under its own identity, started by the workflow, which waits for success. (b) From the GitHub runner: it needs a SQL firewall opening for dynamic runner addresses and an Entra token on the runner. **Recommend (a).**
5. **Revision mode and the order.** (a) Single revision mode: migrate, then update the image (the new revision is promoted on creation), plus the expand/contract rule. (b) Multiple revision mode with a 0%-traffic revision, then a traffic shift: it runs two replicas for longer, against AD-14's single-replica intent. **Recommend (a).**
6. **AR-40b: min replicas.** (a) `minReplicas: 0`, with `docs/operations.md` stating "NFR-5 is measured warm; the cold path is exempt until story 2.9 measures it". The cost stays near zero, and 2.9 owns the decision (readiness `:546`). (b) `minReplicas: 1` (~£12–15/month, spine `:425`). It decides 2.9's question for it, and it costs half the ceiling before any user exists. **Recommend (a)**: stated, never silent, and a one-parameter change.
7. **Data Protection key store (F1).** (a) The EF Core store in the application database: one in-band package, a migration, no new Azure resource. (b) Blob storage plus a Key Vault key: three Azure packages, an `Azure.Identity` dependency in the Host, and a storage account. (c) An Azure Files volume mount with `PersistKeysToFileSystem`: no package, but a storage account and keys on a share. **Recommend (a).** Encryption at rest is explicitly not required (NFR-6) and is recorded under §6.4.
8. **SQL authentication.** (a) Entra managed identity for both runtime identities, with a password-free connection string still delivered through Key Vault. Verify SqlClient 7.0.2's support (see notes). (b) SQL logins, with the passwords generated at bootstrap and stored only in Key Vault. **Recommend (a)**, falling back to (b) only if (a) needs an Azure dependency the Host should not carry. Record which.
9. **The Communication Services key (AC3).** No email feature exists until 4.1 (the outbox). (a) Name the Key Vault secret and the env reference in the IaC as a commented, disabled slot, provision **no** ACS resource, and ledger the rest to 4.1. (b) Provision ACS now: a resource with no consumer. **Recommend (a).** AC3's "Communication Services key" clause is met in shape and owed in substance to 4.1. Say exactly that.
10. **Forwarded headers.** (a) Enabled only by configuration, set by IaC: `XForwardedFor | XForwardedProto`, `ForwardLimit = 1`, known proxies and networks cleared. (b) Always on: a local Host would then trust any `X-Forwarded-*`. **Recommend (a).** Sub-question, a missing address: keep the shared partition and log it once at startup when forwarded headers are on.
11. **A database failure on the revocation read.** (a) A uniform `503` problem with `Retry-After`, admitting nothing. (b) Treat it as unauthenticated (`401`): that signs the person out of a working Session because the database was resuming. **Recommend (a).**
12. **The clock step.** (a) Issue at `max(now, SessionsValidAfter)`. (b) Stamp from the database clock: one more round trip and a second clock source (AD-27 reasons against two clocks). **Recommend (a).**
13. **The purge trigger.** (a) Only after a request has authenticated a Session or a Token (it has already read the database). (b) Keep it on every request and accept the wake. **Recommend (a).**
14. **AR-39's second signal (rate-limit refusals).** (a) A log-search alert on the rate-limit refusal log line, with its monthly cost in the table. (b) None until a dashboard exists. **Recommend (a)** if its cost is under about £1/month, else (b) with the reason recorded.
15. **Probe paths.** (a) `/health/live` and `/health/ready` as plain endpoints outside `/api`, with no `AddHealthChecks`. (b) Under `/api/v1`: they enter the product's route table and the isolation gap gate. **Recommend (a).**
16. **CI stages and the budget.** (a) Build the PR stage now, ledger Nightly and Weekly to 2.9, and put a consumption budget at £30 on the resource group. (b) Create empty Nightly/Weekly stages now: vacuous gates. **Recommend (a).**
17. **Local migration under `aspire run`.** (a) An AppHost resource that runs the migration, which the Host waits on. (b) A documented manual `dotnet ef database update`: the Local environment then stays broken by default. **Recommend (a).**
18. **Where the operational record lives, and how pipeline invariants are gated.** (a) `docs/operations.md`, plus text-based architecture gates over `infra/` and `.github/`, each planted. (b) Story notes only: invisible after the story closes, and AC8 says "recorded". **Recommend (a).**
19. **An unconfigured container runtime in CI.** (a) `YELLO_REQUIRE_CONTAINER_RUNTIME=1` turns the skip into a failure, set only in CI. (b) Rely on `ubuntu-latest` having Docker: a runner change would then skip SM-1 silently. **Recommend (a).**
20. **AC17, if only an Aspire bump would clear the remaining advisories.** That is an AR-1 edit. (a) Keep the suppression and record the check. Lee decides the Aspire move separately. (b) Bump Aspire within this story. **Recommend (a).**

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-1-10-ship-it-to-azure-with-migrations-as-an-explicit-step.md` (scenario ids, failure evidence, plants, proposed seam names, and the Azure-only manual checks tied to P1-P7). Written by the ATDD run on 2026-10-25: **44 cases, 6 active and green today, 38 skipped** with `red: AC<n> - <why> (Task n)`. Full run after it: 548 total, 509 passed, 39 skipped. No new project, package, fixture type or engine.

| File | Cases | ACs | Green today |
|---|---|---|---|
| `tests/Yello.Tests.Architecture/MigrationAtStartupGateTests.cs` | 1 | 15, 2 | ARCH-001 (no production `Migrate`/`EnsureCreated`/`IMigrator`) - planted, caught |
| `tests/Yello.Tests.Architecture/ProbeGateTests.cs` | 2 | 6 | ARCH-002 (no `AddHealthChecks`) - planted, caught |
| `tests/Yello.Tests.Architecture/SecretHygieneTests.cs` | 3 | 3 | ARCH-004 (no credential shape in config/IaC/workflows), ARCH-005 (no `ConnectionStrings` in the Host's appsettings) - both planted, caught |
| `tests/Yello.Tests.Architecture/DeploymentShapeTests.cs` | 10 | 1, 4, 5, 6, 10, 11, 13, 15 | none |
| `tests/Yello.Tests.Architecture/OperationsRecordTests.cs` | 5 | 7, 8, 9 | none |
| `tests/Yello.Tests.Slices/Deployment/MigrationBundleEquivalenceTests.cs` | 2 | 2, 15 | none |
| `tests/Yello.Tests.Slices/Deployment/DatabaseIdentityTests.cs` | 1 | 15 | SLC-003 (a `db_datareader`+`db_datawriter` identity cannot disable the policy or change `MAXDOP`) - not yet planted |
| `tests/Yello.Tests.Slices/Deployment/ProbeTests.cs` | 1 | 6 | none |
| `tests/Yello.Tests.Slices/Deployment/ForwardedHeadersTests.cs` | 5 | 11 | none |
| `tests/Yello.Tests.Slices/Deployment/RateLimitPartitionTests.cs` | 3 | 11 | UNIT-003 (IPv4 keys as itself; a missing address keeps the shared partition) - not yet planted |
| `tests/Yello.Tests.Slices/Deployment/DataProtectionPersistenceTests.cs` | 2 | 12 | none |
| `tests/Yello.Tests.Slices/Deployment/DatabaseUnavailableTests.cs` | 4 | 14, 12, 6 | none |
| `tests/Yello.Tests.Slices/Deployment/SqlServerEngineFailFastTests.cs` | 1 | 16 | none |
| `tests/Yello.Tests.Slices/Deployment/RunningHostCursorTests.cs` | 1 | 16 | none |
| `tests/Yello.Tests.Slices/ContainerRuntimeProbeTests.cs` (2 cases added) | 2 | 10 | none |
| `tests/Yello.Tests.Client/StaticHostFallbackTests.cs` | 1 | 13 | none |

`tests/Yello.Tests.Slices/Deployment/DeploymentHosts.cs` is the shared helper (behind-ingress settings, an unreachable connection string, an empty per-process user profile). Names the scaffolds reach by reflection or assume (`SessionRevocation.IssueInstant`, `SqlServerEngine.UnavailableReasonFor`, the engine's probe/factory constructor, `urn:yello:problem:service-unavailable`, the `RefusalPurgeClaimed` log marker, `dbo.DataProtectionKeys`) are proposals; correct them on activation and record the correction.

Activation order:

1. **SLC-013, then SLC-010 and SLC-011 - the riskiest assumption first** (Q7 (a)): the key ring can live in the application database and fail honestly when it is paused (a `503`, never a fresh in-memory key). They run behind ingress, so they first fail for F3's reason: implement Q10 (activating SLC-005 and SLC-006), confirm they then fail for F1's reason, then persist the ring. If the EF store cannot fail honestly, revisit Q7 before building on it.
2. SLC-001, SLC-002 (the bundle; build it for the local runtime and set `YELLO_MIGRATION_BUNDLE`), then ARCH-016 (AppHost migration).
3. SLC-012, SLC-014, UNIT-004; then SLC-004 and ARCH-003 (the probe's log slice is clean only once the purge trigger has moved).
4. SLC-007 to SLC-009, UNIT-001, UNIT-002.
5. UNIT-005, UNIT-006, UNIT-007, SLC-015 (a race today; confirm red over several runs).
6. ARCH-006 to ARCH-011 (Bicep), ARCH-012 to ARCH-014 (workflows), ARCH-015 and E2E-001 (static host), ARCH-017 to ARCH-021 (`docs/operations.md`).
7. Keep ARCH-001, ARCH-002, ARCH-004, ARCH-005, SLC-003 and UNIT-003 green throughout.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), bmad-dev-story, run unattended under the story-cycle orchestrator.

### Debug Log References

**Activation order.** I followed the Dev Notes order. The production code for Tasks 4 and 5 (forwarded headers, the key ring, the 503 mapping, the purge trigger, the issue instant and the probes) went in as one change. I then activated SLC-005/006/010/011/012/013/014, UNIT-001/002/004 and SLC-004 together against it. All except SLC-004 passed on the first run.

This means red was **not** re-observed by me before the code went in for those cases. The red evidence for them comes from two places: the ATDD run's table (`atdd-checklist-1-10-...md`, "Red scaffolds fail for the stated reason"), and the plants below. Every plant removes one of these behaviours and turns its case red. For SLC-001/002 and E2E-001, red was observed directly: the first run failed on a scaffold defect, recorded below.

**Scaffolds corrected, and why**

- **SLC-004 (`ProbeTests`).** The anchor was "a sign-in against an unreachable database answers ≥ 500". Now that the key ring lives in the database, the anti-forgery token the sign-in fetches first already fails with a `503`. The sign-in then went out without a token and was answered `400`. The anchor now asks for the anti-forgery token directly and expects ≥ 500.
- **SLC-010 (`DataProtectionPersistenceTests`).** The table is `dbo.DataProtectionKey`, not `dbo.DataProtectionKeys`. `PersistenceModelGateTests` requires singular table names, and I did not use the EF package.
- **SLC-001/002 (`MigrationBundleEquivalenceTests`).** The catalogue queries failed with "Cannot resolve collation conflict ... in concat operator" because the catalogue views mix collations. Every string column is now `COLLATE DATABASE_DEFAULT`. After that, both cases passed against the win-x64 bundle.
- **SLC-012 path.** With F1 fixed, the unreachable Host fails while loading the key ring, so the revocation-read mapping is not reachable over the wire. As the scaffold remarks ask, I **added SLC-016**: `A_revocation_read_against_a_database_that_went_away_is_the_same_503`. A Host loads its ring, its database is taken `OFFLINE`, and the Session's next request must be the same `503`.
- **SLC-015 (`RunningHostCursorTests`).** This is a race that never lost on this machine. With the counting fix planted out, it passed 3 runs out of 3. I **added SLC-017**, which is deterministic: the other client's requests finish before the harness's single request. `ServerDurationOfAsync` then reads six lines where one request was sent, and refuses. With the fix planted out it was caught 2 times out of 2.
- **E2E-001 (`StaticHostFallbackTests`).** The scaffold's `PublishedClientHost` contract was replaced by a real type, `tests/Yello.Tests.Client/PublishedClientHost.cs`. It runs `dotnet publish Yello.Client -c Release` into a temp folder, writes `appsettings.Production.json`, and serves the output from an `HttpListener` that applies the published `navigationFallback` (rewrite plus exclude globs). No package was added.
- **UNIT-004, UNIT-005/006, UNIT-007.** The proposed seams exist under the proposed names: `SessionRevocation.IssueInstant`, `SqlServerEngine.UnavailableReasonFor` and the engine's `(probe, factory)` internal constructor. They are still reached by reflection, because `Yello.Tests.Shared` grants Slices no internals and I did not widen it.
- **ARCH-012.** `Ci_runs_the_architecture_suite_first...` found the text `dotnet test Yello.slnx` in the workflow's header comment, ahead of the architecture step. I reworded the comment and left the gate unchanged.
- **ARCH-006.** The Key Vault rule's `[^}]*` stopped at the `}` of Bicep interpolation (`'${keyVaultUri}secrets/...'`). The URLs now come from two variables (`appSecretUrl`, `migrateSecretUrl`). That reads better too, and the gate is unchanged.
- Stale "red-phase / skipped / does not exist yet" remarks in every activated scaffold class are rewritten to describe the classes as they are now.

**Local parity run (Task 3, `deferred-work.md:169`).** `./run.ps1`: `dotnet aspire run` built the AppHost. The `migrate` resource went `Running -> Finished` about 5 s after the SQL container was ready, and the Host started after it. Against `https://localhost:7093`, all driven with `curl -k` and a cookie jar, **not a browser**:

- `GET /health/live` 200
- anti-forgery token, then `POST /api/v1/accounts` 204
- token, then `POST /api/v1/sessions` 204
- `GET /api/v1/sessions/current` 204
- `GET /api/v1/spaces` returned `[{"id":"…","name":"Local Check's Space"}]`

The AppHost was stopped. The leftover `sql-*` container and the `aspire-session-network-*` network were removed.

**Container images (Task 8), Rancher `docker` 29.5.3.**

- `dotnet publish Yello.Host/Yello.Host.csproj -c Release -r linux-x64 -t:PublishContainer -p:ContainerImageTag=local` produced `yello-host:local` on `mcr.microsoft.com/dotnet/aspnet:10.0.11` (100 MB content, user 1654, port 8080) in 19 s.
- `dotnet ef migrations bundle ... -r linux-x64 -o artifacts/efbundle/efbundle` produced a 154 MB bundle, and `docker build -f infra/migrate.Dockerfile` produced `yello-migrate:local`. The first image could not execute the bundle, because a Windows-built file has no execute bit, so the Dockerfile now uses `COPY --chmod=0755`.
- Against a fresh SQL Server 2025 container with an empty `yello` database, run 1 of the migration image applied all eight migrations ("Done.", exit 0). Run 2 printed "No migrations were applied. The database is already up to date.", exit 0.
- (ii) `docker run` of the Host with `ConnectionStrings__yello`, `ClientOrigin` and `ForwardedHeaders__Enabled=true` gave:
  - `/health/live` 200 and `/health/ready` 200
  - anti-forgery token without `X-Forwarded-Proto`: 500 (F3, as expected)
  - with `X-Forwarded-Proto: https`: 200, and `Set-Cookie: yello_antiforgery=…; secure; samesite=none; httponly`
  - register 204, sign-in 204 with `Set-Cookie: yello_session=…; secure; samesite=none; httponly`
- (iii) A Host pointed at `10.255.255.1` (no database) answered `live=200 ready=200`.
- (iv) `docker image inspect yello-host:local` env is `PATH`, `APP_UID`, `ASPNETCORE_HTTP_PORTS=8080`, `DOTNET_RUNNING_IN_CONTAINER`, `DOTNET_VERSION`, `ASPNET_VERSION`. There is no `ConnectionStrings*`. `docker export` lists 5,566 entries, and `grep -E "Password=|Pwd=|AccountKey=|SharedAccessKey=|ConnectionStrings|BEGIN (RSA|PRIVATE)"` over `/app/*.json` found nothing (exit 1). The migrate image's env holds no connection string either.
- Every verification container, network and the generated `sa` password were removed afterwards.

**Bicep and workflow, verified locally.**

- Bicep CLI **0.45.15** (already installed under `~/.azure/bin`): `bicep build infra/main.bicep` and `bicep lint infra/main.bicep` report **zero warnings and zero errors**. `infra/bicepconfig.json` raises the secret, location and unused-symbol rules to `error`. `what-if` was **not** run (no subscription).
- `actionlint` **1.7.12** reports zero findings on `.github/workflows/ci.yml`.
- I checked `--report-xunit-trx` locally. `--report-trx` is not an option of this test platform, and the run reports "Zero tests ran", so the workflow uses `--report-xunit-trx`.
- Action SHAs were resolved with `gh api`: checkout v4.2.2, setup-dotnet v4.3.1, upload-artifact v4.6.2, download-artifact v4.3.0, azure/login v2.3.0 (the annotated tag dereferenced to its commit), static-web-apps-deploy v1.
- The free-offer page was read on 2026-10-25. It confirms 100,000 vCore s, the "Free amount remaining" metric, a 10,000 vCore-s (10%) alert, and that `BillOverUsage` cannot be switched back.

**SqlClient and Entra (Q8).** `strings` over `Microsoft.Data.SqlClient.dll` 7.0.2 finds: "Cannot find an authentication provider for '{0}'. Install the 'Microsoft.Data.SqlClient.Extensions.Azure' NuGet package ... to use Active Directory (Entra ID) authentication methods." That settled Q8 as (b).

**AC17 (2026-10-25).**

- `dotnet list Yello.slnx package --vulnerable --include-transitive` lists GHSA-846f-f764-76mx and GHSA-2fmv-rv2c-59x7 against Pkcs 9.0.13 and 10.0.8, and GHSA-qhrr-8q5h-9q3h against MessagePack 2.5.302, with the suppressions in place.
- The newest stable `Microsoft.Data.SqlClient` is 7.1.1, and it brings Pkcs 9.0.18. The fix is 9.0.21, so no SqlClient bump clears it.
- The newest stable Aspire is 13.6.4. It brings MessagePack 2.5.307, which is past the 2.5.303 fix. That is an AR-1 edit, so it was not taken.
- Stable Pkcs 9.0.21 and 10.0.13 now exist. A transitive security pin would clear both Pkcs advisories. That is a new central pin, so it is ledgered to Lee and not taken.
- **The ItemGroup is unchanged.**

**Plants.** Run one at a time in the foreground by `scratchpad/plants.py`. Each one saves the original bytes, plants, builds, runs the gate's class, and restores in a `finally`. The script takes a SHA-256 over every modified or untracked file before and after: **identical after every plant**. No `git checkout`, `git restore` or `git reset` was used.

| # | Plant | Gate | Caught |
|---|---|---|---|
| 1 | New public `PlantedStartupMigration` calling `context.Database.Migrate()` | ARCH-001 | yes. The first attempt was an internal class and failed the build on MA0182 instead; it was rewritten as public. |
| 2 | `/health/live` handler opens a `SqlConnection` | ARCH-003 | yes. The first attempt failed on IDE2001; it was reformatted. |
| 3 | `"Planted": "Server=x;Password=hunter2hunter2"` in `Yello.Host/appsettings.json` | ARCH-004 | yes |
| 4 | `ConnectionStrings__yello` as a plain `value` in `containers.bicep` | ARCH-006 | yes |
| 5 | `maxReplicas: 2` | ARCH-008 | yes |
| 6 | `freeLimitExhaustionBehavior: 'BillOverUsage'` | ARCH-009 | yes |
| 7 | A second job with `environment: staging` | ARCH-007 | yes |
| 8 | `promote` needs `build-images`, not `migrate` | ARCH-013 | yes |
| 9 | `ForwardLimit = null`. Deleting the line is not a plant: the default is already 1. | SLC-007 | yes |
| 10 | Key ring persistence removed (the `XmlRepository` is null unless an unset variable is set) | SLC-010, SLC-011 | yes. The first attempt left `DatabaseXmlRepository` unused, and MA0182 failed the build. |
| 11 | Purge trigger back on every request | SLC-014 | yes |
| 12 | `IsDatabaseFailure` answers false (the 503 mapping removed) | SLC-012, SLC-013, SLC-016 | yes |
| 13 | `YELLO_REQUIRE_CONTAINER_RUNTIME` ignored | UNIT-005 | yes |
| 14 | App identity also granted `db_owner` | SLC-003 | yes |
| 15 | A missing address keyed on its own partition | UNIT-003 | yes |
| 16 | `IssueInstant` comparison inverted (it stamps `now` after a clock step) | UNIT-004 | yes. The first attempt (`? now : now`) failed S3923. |
| 17 | The cursor counts untagged lines | SLC-015 | **no** (3/3 passed: the race never lost) |
| 17b | The same plant | SLC-017 (added) | yes (2/2) |
| 18 | `navigationFallback.rewrite` pointed at a missing file | E2E-001 | yes |
| 19 | `.WaitForCompletion(migrate)` becomes `.WaitFor(migrate)` | ARCH-016 | yes. The first attempt deleted the line, which left `migrate` unused, and S1481 failed the build. |
| 20 | "Encryption at rest" renamed in the §6.4 table | ARCH-017 | **no**. The phrase also appears in sections 8 and 9, and the gate asks only that each phrase appear somewhere. |
| 20b | The **breach-notification** prerequisite reworded | ARCH-017 | yes |
| 21 | The engine re-probes after a dead probe instead of rethrowing the cached failure | UNIT-007 | yes. The first attempt failed S4201. |
| 22 | `useFreeLimit: false` | ARCH-009 | yes |
| 23 | Free-allowance alert threshold `20000` | ARCH-010 | yes |

The plants were 21 of 23 caught on the first gate tried. Both misses are explained above, and one of them has a new deterministic case. Plant 20 is a **weakness of the gate, recorded rather than fixed**: a phrase gate cannot tell the prerequisite table from other mentions.

**Runs.**

- `dotnet build Yello.slnx` (plain, no overrides): **0 warnings, 0 errors.**
- `dotnet test Yello.slnx` with `YELLO_MIGRATION_BUNDLE` pointing at the win-x64 bundle: **550 total, 549 passed, 0 failed, 1 skipped** (the legacy-factor duration case). Per suite: Architecture 9 s, Revocation 46 s, Isolation 64 s, Client 106 s, Slices 142 s. Wall clock **147 s**.
- Engines: still 4 (`ContainerTopologyGateTests` green). The Docker peak was not measured this run.
- Without `YELLO_MIGRATION_BUNDLE`, the two bundle cases skip with their reason, giving 547 passed and 3 skipped.
- The baseline at `9de3ff3` was 504/503/1. The ATDD run took it to 548 with 39 skipped. This story added SLC-016 and SLC-017.
- An earlier full run, before SLC-017, was 549/548/1. No engine-start errors occurred in any run.

### Completion Notes List

- Ultimate context engine analysis completed: the comprehensive developer guide was created.
- **The shape.** Every repo-side artefact exists and has been verified locally, without deploying:
  - `infra/` (Bicep, one environment)
  - `.github/workflows/ci.yml` (the PR stage, plus deploy jobs on `main`: build-images → migrate → promote → client)
  - the Host image, built by the SDK's `PublishContainer`, with no Dockerfile
  - the migration image (`infra/migrate.Dockerfile` around the self-contained EF bundle)
  - the probes
  - the production-only Host configuration
  - `docs/operations.md`

  **Nothing in the repository claims a deploy happened.** P1–P7 are all recorded as "not yet".
- **The dominant finding: F1, F3 and the database-failure path had to be built as one design.** The key ring now lives in the database (AC12). The framework's cookie reader turns any decryption failure into "no cookie". So a paused database would have been answered as a sign-out (`401`). `DatabaseAvailability` therefore loads the ring once per process before any cookie is read, and maps any `DbException` in the exception chain to the uniform `503` (AC14). SLC-013 proves that a cold process with no reachable database answers `503`, not `401`.
- **Requirements implemented in the repository:** AR-33 (probes from process state, no timer; A-9 still green), AR-36 (IL gate, bundle, job before promote), AR-37 (env only, Key Vault references, secret-hygiene gates, an image with no secret), AR-38 (the workflow, order-gated), AR-39 (free-limit alert and auto-pause, the rate-limit refusal log alert), AR-40c (recorded procedure; `MAXDOP = 1` stays), §6.3 (cost table, budget), §6.4 (assertion, five prerequisites, tripwire, parked egress), the NFR-6 transit chain, AD-7 (CORS unchanged; forwarded scheme), AD-10, AD-14 (`maxReplicas: 1`, single revision).
- **AC-by-AC, what is proven and what is only shaped:**
  - **AC1:** the repository half is done. The Azure run is P6.
  - **AC2:** proven. SLC-001 shows the bundle produces exactly `MigrateAsync`'s catalogue, policy and filtered index included.
  - **AC3:** in shape. Secret references and the image scan are done. The ACS clause is met in shape only (Q9); its substance belongs to story 4.1.
  - **AC4:** done. Gate ARCH-007; the Local environment now migrates.
  - **AC5:** IaC and gates done. Firing is P6.
  - **AC6:** done. SLC-004, ARCH-002 and ARCH-003.
  - **AC7:** recorded. It is not confirmed (P5).
  - **AC8:** recorded. Lee's confirmation is P7.
  - **AC9:** recorded.
  - **AC10:** in the repository only. The first hosted run is P3/P6. 1.9's AC5 is closed in the repository only.
  - **AC11:** done. SLC-005 to SLC-009 and UNIT-001 to UNIT-003.
  - **AC12:** done. SLC-010, SLC-011 and SLC-013.
  - **AC13:** done. E2E-001 and ARCH-015. The Safari cookie issue is P4 (F2).
  - **AC14:** done. SLC-012, SLC-013, SLC-014, SLC-016 and UNIT-004.
  - **AC15:** done. ARCH-001, SLC-001/002, SLC-003, ARCH-016, and the AppHost run.
  - **AC16:** done. UNIT-007, SLC-015 and SLC-017.
  - **AC17:** checked and recorded. The ItemGroup is unchanged.
- **Test changes:** 44 ATDD cases activated, and 2 added (SLC-016, SLC-017). E2E-001's static host is a new helper, `PublishedClientHost`. There is no new project, package, engine or fixture type.
- **The bundle (Task 3):** win-x64 is 121 MB and linux-x64 is 154 MB. Each builds in about 14 s. The equivalence test ran, and was **not skipped**, in the final run (`YELLO_MIGRATION_BUNDLE` set).
- **Production code beyond the expected list:**
  - `Yello.Contracts/ProblemTypes.cs` (`ServiceUnavailable`)
  - `Yello.Host/RateLimitLog.cs` (a `RateLimitRefused` warning for Q14's alert, with no address)
  - `Yello.Host/Yello.Host.csproj` (container properties)
  - new `Yello.Host/Deployment/*` and `DeploymentLog.cs`
  - the key-ring store in Infrastructure

  There is no new `/api` route: the probes are outside `/api`.
- **Not done here, stated so nobody over-reads it:**
  - **Cutting Isolation/Revocation/Client's schema edge to Infrastructure.** This was not done (1.9 decision). It could happen later now that a bundle exists, but the suites would then need a build step they cannot assume locally.
  - **`SequentialGuidIdentifierGenerator`** stays as it is. `deferred-work.md:215` is story 2.9's. With `maxReplicas: 1` there is one generator per process, and the brief two-process overlap during a revision swap affects index locality only, because the primary key catches a duplicate.
  - **The work-factor re-measure** (`:147`) is P6, and the `:225` precondition stands.
- **Lee's attention (none of these blocks review):**
  1. **Q7 and Q8 were built without new packages**, because the orchestrator makes a new package a HALT. A hand-written key-ring store replaced the EF Data Protection package, and SQL contained users with passwords replaced Entra managed identity. Both are ledgered as package decisions for Lee.
  2. **A transitive pin of `System.Security.Cryptography.Pkcs` to 9.0.21/10.0.13 would clear two of the three suppressed advisories.** This is AC17's "found, not taken".
  3. **The first IaC deployment runs a placeholder Host image** that answers neither probe. `docs/operations.md` section 11 says what to do if it refuses to complete.
  4. **The workflow needs six repository variables, not three** (P3).
  5. **ARCH-017 is a phrase gate.** Plant 20 shows that it cannot tell the §6.4 table apart from other mentions of the same words.

**Open questions: decisions taken (each reversible; cut any line to revisit it)**

- **Q1 - (a) Bicep under `infra/`.** It is first-party, `az bicep build` validates it locally, and it keeps no state file. Cost to later stories: none. Every new Azure resource is added in Bicep.
- **Q2 - (a) ACR Basic, admin user disabled, pulls by managed identity.** About £4 a month, and that is in the cost table. Cost to later stories: none.
- **Q3 - (a) the EF Core migration bundle.** The win-x64 bundle is 121 MB and the linux-x64 bundle is 154 MB (self-contained). Each builds in about 14 s on this machine with the local `dotnet-ef` 10.0.11. Cost to later stories: none. Each new migration rides in the next bundle.
- **Q4 - (a) a Container Apps Job, started by the workflow, which waits for `Succeeded`.** Cost to later stories: none.
- **Q5 - (a) single revision mode, plus the expand/contract rule.** The rule is written into `docs/operations.md` section 9 and `TESTING-CONVENTIONS.md`. All eight migrations were checked against it. Cost to later stories: every migration must be compatible with the previous revision.
- **Q6 - (a) `minReplicas: 0`, stated as "NFR-5 is measured warm; the cold path is exempt until story 2.9 measures it".** Cost to later stories: story 2.9 owns the measurement. Moving to 1 is a one-parameter change (`minReplicas`).
- **Q7 - (a), built without the package.** The key ring lives in the application database (`dbo.DataProtectionKey`, migration `DataProtectionKeyRing`) through a hand-written `IXmlRepository` (`PersistedKeyRing`, `KeyRingStore`), not `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`. The orchestrator's rule makes a new NuGet package a HALT, and the repository is about 40 lines. The table key is an application-generated `Guid`, which `PersistenceModelGateTests.No_key_is_generated_by_the_database` requires; the package's `int` identity key would fail that gate. The riskiest assumption checked out: when the ring cannot load, the Host answers `503`, never a fresh in-memory key (SLC-013). Cost to later stories: none. Ledgered for Lee as a package option.
- **Q8 - (b) SQL contained users with passwords that exist only in Key Vault.** `Microsoft.Data.SqlClient` 7.0.2 has no Entra authentication without `Microsoft.Data.SqlClient.Extensions.Azure`. The driver's own resource string says so ("Install the 'Microsoft.Data.SqlClient.Extensions.Azure' NuGet package ... to use Active Directory (Entra ID) authentication methods"). A new package is a HALT, and (b) is the story's stated fallback. Because of this, the server allows SQL authentication alongside its Entra administrator. Cost to later stories: none. Moving to (a) is ledgered for Lee.
- **Q9 - (a) the ACS slot is named in `infra/main.bicep` (`yello-acs-connection`, `ConnectionStrings__acs`) but not provisioned.** Cost to later stories: story 4.1 provisions it. Ledgered.
- **Q10 - (a) forwarded headers are on only by configuration (`ForwardedHeaders__Enabled`), with `ForwardLimit = 1` and the known lists cleared.** A missing address keeps the shared partition, and the Host logs one line about it at startup when forwarding is on. Cost to later stories: none.
- **Q11 - (a) a uniform `503` with `urn:yello:problem:service-unavailable` and `Retry-After: 30`, from one pipeline middleware (`DatabaseAvailability`).** Cost to later stories: none. A client may later branch on the new problem type.
- **Q12 - (a) sign-in issues at `max(now, SessionsValidAfter)`.** The success path reads `SessionsValidAfter` by primary key, one extra read. Cost to later stories: none.
- **Q13 - (a) the purge trigger runs after authentication, for authenticated requests only.** Cost to later stories: none.
- **Q14 - (a) a log-search alert on a new `RateLimitRefused` warning line (EventId 1030).** It costs about US$0.50 a month, which is under the £1 bar. That figure is unverified and is to be confirmed at P1. Cost to later stories: none.
- **Q15 - (a) `/health/live` and `/health/ready` are plain endpoints outside `/api`, mapped by `HealthProbes`, with no `AddHealthChecks`.** Cost to later stories: none.
- **Q16 - (a) the PR stage only, plus a £30 budget on the resource group.** Nightly and Weekly are ledgered to story 2.9.
- **Q17 - (a) the AppHost `migrate` resource runs `dotnet ef database update` through the local tool, and the Host `.WaitForCompletion(migrate)`.** `WaitForCompletion` exists in the pinned Aspire 13.4.6, and it ran. I used the tool rather than the bundle because a bundle would need a build step before every `aspire run`. Both apply the same migrations assembly, and SLC-001 proves the bundle matches `MigrateAsync`. Cost to later stories: none.
- **Q18 - (a) `docs/operations.md`, plus text gates with anchors.** No YAML parser package was needed: ARCH-012 to ARCH-014 worked as text.
- **Q19 - (a) `YELLO_REQUIRE_CONTAINER_RUNTIME=1`, set only in CI.** The seam is `SqlServerEngine.UnavailableReasonFor(bool, bool)`.
- **Q20 - (a) no Aspire bump; the suppressions stay.** See AC17 below.

### File List

New:
- `.github/workflows/ci.yml`
- `infra/main.bicep`
- `infra/modules/containers.bicep`
- `infra/modules/sql.bicep`
- `infra/modules/monitoring.bicep`
- `infra/modules/web.bicep`
- `infra/main.parameters.example.json`
- `infra/bicepconfig.json`
- `infra/migrate.Dockerfile`
- `docs/operations.md`
- `Yello.Client/wwwroot/staticwebapp.config.json`
- `Yello.Host/DeploymentLog.cs`
- `Yello.Host/Deployment/DatabaseAvailability.cs`
- `Yello.Host/Deployment/ForwardedHeadersPolicy.cs`
- `Yello.Host/Deployment/HealthProbes.cs`
- `Yello.Host/Deployment/PersistedKeyRing.cs`
- `Yello.Host/Deployment/ProductionShape.cs`
- `Yello.Infrastructure/Persistence/DataProtectionKey.cs`
- `Yello.Infrastructure/Persistence/IKeyRingStore.cs`
- `Yello.Infrastructure/Persistence/KeyRingStore.cs`
- `Yello.Infrastructure/Persistence/KeyRingStoreServices.cs`
- `Yello.Infrastructure/Persistence/Configurations/DataProtectionKeyConfiguration.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261025001437_DataProtectionKeyRing.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261025001437_DataProtectionKeyRing.Designer.cs`
- `tests/Yello.Tests.Client/PublishedClientHost.cs`

Modified:
- `Yello.AppHost/Program.cs`
- `Yello.Contracts/ProblemTypes.cs`
- `Yello.Host/Program.cs`
- `Yello.Host/Yello.Host.csproj`
- `Yello.Host/RateLimitLog.cs`
- `Yello.Host/Endpoints/SessionEndpoints.cs`
- `Yello.Host/Sessions/CredentialHashingRateLimit.cs`
- `Yello.Host/Sessions/SessionRevocation.cs`
- `Yello.Host/Spaces/RefusalRetentionTrigger.cs`
- `Yello.Infrastructure/Persistence/SchemaNames.cs`
- `Yello.Infrastructure/Persistence/YelloDbContext.cs`
- `Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs`
- `tests/Yello.Tests.Shared/RunningHost.cs`
- `tests/Yello.Tests.Shared/SqlServerEngine.cs`
- `tests/Yello.Tests.Architecture/DeploymentShapeTests.cs`
- `tests/Yello.Tests.Architecture/OperationsRecordTests.cs`
- `tests/Yello.Tests.Architecture/ProbeGateTests.cs`
- `tests/Yello.Tests.Architecture/SecretHygieneTests.cs`
- `tests/Yello.Tests.Slices/ContainerRuntimeProbeTests.cs`
- `tests/Yello.Tests.Slices/Deployment/DatabaseUnavailableTests.cs`
- `tests/Yello.Tests.Slices/Deployment/DataProtectionPersistenceTests.cs`
- `tests/Yello.Tests.Slices/Deployment/ForwardedHeadersTests.cs`
- `tests/Yello.Tests.Slices/Deployment/MigrationBundleEquivalenceTests.cs`
- `tests/Yello.Tests.Slices/Deployment/ProbeTests.cs`
- `tests/Yello.Tests.Slices/Deployment/RateLimitPartitionTests.cs`
- `tests/Yello.Tests.Slices/Deployment/RunningHostCursorTests.cs`
- `tests/Yello.Tests.Slices/Deployment/SqlServerEngineFailFastTests.cs`
- `tests/Yello.Tests.Client/StaticHostFallbackTests.cs`
- `tests/TESTING-CONVENTIONS.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/1-10-ship-it-to-azure-with-migrations-as-an-explicit-step.md`

Not committed: `artifacts/` (the bundles, git-ignored). No local images or containers were left behind.

## Change Log

- 2026-10-25: Story created (create-story). Status -> ready-for-dev.
- 2026-10-25: Implemented (dev-story). Built the IaC, the CI/deploy workflow, the images, the migration bundle and the AppHost migration. Added the probes, forwarded headers, the persisted key ring, the 503 mapping, the purge-trigger move and the issue-instant fix. Paid the test-estate debts and wrote docs/operations.md. 44 ATDD cases activated, 2 added, 23 plants. Status -> review.
- 2026-10-25: Code review (bmad-code-review, three layers): 57 raw findings, 29 triaged. Lee decided D1 (b) and D2 (a), and every patch was applied: SLC-018 and UNIT-008 were added, and E2E-001, ARCH-007/010/013/015/016/017 and the equivalence catalogue were strengthened. Five review plants (ARCH-015, -016, -013, -017, -007) were all caught, with the files restored by hash. Build: 0 warnings, 0 errors. Architecture suite: 208/208. actionlint and the Bicep linter: zero findings. The first full run was stopped by the host for low memory. The orchestrator then ran the full verification on the patched tree: `dotnet build Yello.slnx` gave 0 warnings and 0 errors, and `dotnet test Yello.slnx --no-build` with `YELLO_MIGRATION_BUNDLE` set gave **552 total, 551 passed, 0 failed, 1 skipped** (the legacy-factor duration case). Status -> done.
