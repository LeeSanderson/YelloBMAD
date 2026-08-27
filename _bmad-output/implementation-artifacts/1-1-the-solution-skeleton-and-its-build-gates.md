---
baseline_commit: c83450c63c8e68773349ec312eac286127f057cf
---

# Story 1.1: The solution skeleton and its build gates

Status: done

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-1-the-solution-skeleton-and-its-build-gates`
Requirements owned: **AR-1, AR-2, AR-3, AR-4, AR-35**
Depends on: **nothing** — this is the first story in the repository. No source code exists.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer building Yello,
I want the solution laid out in its five rings with the dependency rule, the Role-API ban and the stack versions enforced by tests that fail the build,
So that no later story can erode the structure NFR-1 depends on.

## Acceptance Criteria

**AC1 — the solution exists, in the exact shape, at the exact versions**

**Given** a clean checkout
**When** the solution builds
**Then** it contains `Yello.AppHost`, `Yello.Domain`, `Yello.Application`, `Yello.Infrastructure`, `Yello.Host`, `Yello.Contracts`, `Yello.Merge` and `Yello.Client`, plus `tests/Yello.Tests.Isolation`, `Yello.Tests.Revocation`, `Yello.Tests.Merge`, `Yello.Tests.Architecture` and `Yello.Tests.Slices`
**And** every dependency is pinned to the AR-1 versions — .NET 10.0.11, EF Core 10, ASP.NET Core Identity 10, Asp.Versioning.Http 10.0.0, Aspire 13.4, xunit.v3 4.0.0, Testcontainers.XunitV3 4.6.0, TngTech.ArchUnitNET 0.13.3

**AC2 — the ring rule is a build gate**

**Given** the architecture suite
**When** a project reference is added that violates the ring rule — `Domain` referencing anything, `Application` referencing `Infrastructure` or `Host`, `Infrastructure` referencing `Host`
**Then** the build fails, naming the offending reference
**And** the same happens when an EF Core type appears in `Domain`, or an ASP.NET Core type in `Application` or `Domain`

**AC3 — the Role-API ban is a build gate**

**Given** the architecture suite
**When** `[Authorize(Roles = …)]`, `ClaimsPrincipal.IsInRole`, `IdentityRole` or Identity's role store appears anywhere in the solution
**Then** the build fails
**And** ASP.NET Core Identity remains wired for authentication only — Account store, password hashing, cookie issuance

**AC4 — the local orchestration substrate runs**

**Given** `aspire run` on a developer machine
**When** the AppHost starts
**Then** `Yello.Host`, `Yello.Client` and a `mcr.microsoft.com/mssql/server:2025-latest` container are running with a working connection from Host to container
**And** no test project references an EF Core in-memory provider, which cannot exercise row-level security

**AC5 — the gating suites exist and run empty**

**Given** the four gating suites — isolation, revocation, merge conformance, architecture
**When** they run against a solution with no feature code
**Then** each builds and executes rather than failing to build
**And** any suite that holds no cases yet reports zero tests without that being treated as a failure
**And** later stories add cases to existing suites rather than creating suites

> *AC5's wording as amended in `epics.md` (`status: final`), 2026-08-26 and refined 2026-08-27 —
> reproduced here rather than paraphrased, because the pre-amendment text ("each builds and
> executes, **reporting zero tests** rather than failing to build") required the architecture
> suite to report zero tests while AC2 and AC3 require that same suite to fail the build on a
> violation. The two cannot both hold. The second review pass found this story file still
> carrying the contradictory text after `epics.md` had been amended, which is exactly the drift
> the amendment was meant to end.*

## Tasks / Subtasks

- [x] **Task 1 — Repository build foundation** (AC: 1)
  - [x] Create `global.json` pinning the SDK: `{ "sdk": { "version": "10.0.303", "rollForward": "latestPatch" } }`. Two .NET 10 SDKs are installed on this machine (10.0.302 and 10.0.303); without this pin the build is non-deterministic across machines.
  - [x] Create `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and one `<PackageVersion>` per pinned dependency (exact versions in Dev Notes → *Pinned versions*). This is the single place AC1's "every dependency is pinned" is expressed, and the single place the version-pin test reads.
  - [x] Under central package management, projects declare `<PackageReference Include="…" />` with **no** `Version` attribute. So pin all eight AR-1 versions centrally, but add an actual `PackageReference` only where this story needs one — `xunit.v3`, `xunit.runner.visualstudio`, `TngTech.ArchUnitNET`, `Testcontainers.XunitV3`, and the Aspire hosting packages. EF Core, Identity and `Asp.Versioning.Http` are **pinned but not yet referenced**; stories 1.3 and 1.5 add the references against the versions pinned here. This satisfies AC1 without dragging unused packages into a solution that has no schema.
  - [x] Create `Directory.Build.props` setting, for all projects: `TargetFramework=net10.0`, `RuntimeFrameworkVersion=10.0.11`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `ImplicitUsings=enable`, `EnforceCodeStyleInBuild=true`, `AnalysisLevel=latest-recommended`. `RuntimeFrameworkVersion` is what makes AC1's ".NET 10.0.11" a stated, testable fact rather than an implication of the SDK. **Delivered differently, deliberately — corrected 2026-08-27.** `Directory.Build.props` sets only `TargetFramework`, `RuntimeFrameworkVersion` and the two shared values (`YelloSqlServerImage`, `YelloDatabaseResourceName`). The five coding-standard properties moved into `Opinionated.DotNet.CodingStandards` (commit `fd556ff`) and are **not** restated here: that file is imported before the package's props, so restating a value would silently fork the standard rather than reinforce it. `AnalysisLevel` is now `latest-all`, stricter than specified. This is the resolved decision from the first review pass, and this line had gone on describing the arrangement it replaced.
  - [x] Create `.config/dotnet-tools.json` and pin the Aspire CLI as a **local** tool: `dotnet tool install --local Aspire.Cli --version 13.4.6`. `Aspire.Cli` 13.4.6 is a `DotnetTool` package (`tools/net10.0/any/`). `aspire` is **not** on PATH on this machine; a local tool manifest both installs it and pins it to AR-1's Aspire 13.4 line, which the shell-script install does not.
  - [x] ~~Create `.editorconfig` with the C# conventions the analysers enforce.~~ **Superseded 2026-08-26, corrected here 2026-08-27.** There is no `.editorconfig`: commit `fd556ff` deleted it (115 lines) when `Opinionated.DotNet.CodingStandards` became the one coding standard, and `.gitattributes` and `tests/TESTING-CONVENTIONS.md` both say so. The first review pass raised this against *two* artefacts — "[Task 1 checkbox; File List]" — and only the File List was corrected, leaving this line asserting a file that does not exist. This is the checkbox half.
  - [x] Extend `.gitignore` — it currently carries Python entries only and no .NET entries. Add at minimum `bin/`, `obj/`, `TestResults/`, `.vs/`, `artifacts/`, `*.user`.
  - [x] Create the solution as **`Yello.slnx`** (XML format), and keep it the only solution file — see Dev Notes → *Project Structure Notes* for why the extension was once thought load-bearing and what the gate actually protects now.

- [x] **Task 2 — The eight production projects** (AC: 1)
  - [x] Create `Yello.Domain`, `Yello.Application`, `Yello.Infrastructure`, `Yello.Host`, `Yello.Contracts`, `Yello.Merge`, `Yello.Client` (Blazor WebAssembly), `Yello.AppHost`, at the repository-root layout in Dev Notes → *The five rings*.
  - [x] Add a public `AssemblyMarker` type to each of the seven non-AppHost production projects, in the project's own namespace. The architecture suite needs a deterministic handle on each assembly (`typeof(Yello.Domain.AssemblyMarker).Assembly`); an empty project offers none.
  - [x] Create **no** entities, no `DbContext`, no migrations, no endpoints, no components, no pipeline behaviours, no merge implementation. See Dev Notes → *Scope boundary*.

- [x] **Task 3 — The five test projects plus a shared fixture home** (AC: 1, 5)
  - [x] Create `tests/Yello.Tests.Isolation`, `tests/Yello.Tests.Revocation`, `tests/Yello.Tests.Merge`, `tests/Yello.Tests.Architecture`, `tests/Yello.Tests.Slices`.
  - [x] Create `tests/Yello.Tests.Shared` to hold the shared Testcontainers SQL Server fixture. This closes a test-design entry criterion that has no owning story ("All suites: shared Testcontainers SQL Server fixture running `mssql/server:2025-latest`"). It is *infrastructure, not a suite*, so it does not breach AC5's "later stories add cases to existing suites rather than creating suites". Flagged in *Questions* below.
  - [x] Set `OutputType=Exe` on every test project. `xunit.v3` 4.0.0 depends on `xunit.v3.mtp-v2 [4.0.0]` — it runs on Microsoft.Testing.Platform only. Do **not** add `Microsoft.NET.Test.Sdk`; there is no VSTest path here.
  - [x] Reference `xunit.v3` 4.0.0 and `xunit.runner.visualstudio` 4.0.0. Add `Testcontainers.XunitV3` 4.6.0 to `Yello.Tests.Shared`, and `TngTech.ArchUnitNET` 0.13.3 to `Yello.Tests.Architecture`.
  - [x] Reference **no** EF Core in-memory provider from any test project (AC4). Do not add `Microsoft.EntityFrameworkCore.InMemory` to `Directory.Packages.props` at all — the cheapest enforcement is that the version is not centrally available.

- [x] **Task 4 — Encode the ring rule in the project references** (AC: 2)
  - [x] Wire `<ProjectReference>` elements to exactly the allowed edges in Dev Notes → *Allowed reference edges*. `Yello.Domain` gets none.

- [x] **Task 5 — Gate A: the project-file gate** (AC: 1, 2, 4)
  - [x] In `Yello.Tests.Architecture`, write a test that parses every `.csproj` in the repository, reads its `<ProjectReference>` elements, and asserts each edge against the allowed-edge table. The failure message must name the offending reference — AC2 requires the build to fail "naming the offending reference".
  - [x] Exclude test projects and `Yello.AppHost` from the ring assertions, and assert them against their own allowed edges instead. Without this the gate fails on itself: `Yello.Tests.Architecture` legitimately references all eight production projects, and `Yello.AppHost` legitimately references `Yello.Host` and `Yello.Client`.
  - [x] **Why this gate is not optional:** ArchUnitNET analyses compiled bytecode through Mono.Cecil. Roslyn emits no `AssemblyRef` for a referenced assembly whose types are never used, so a ring-violating `<ProjectReference>` that nobody has written code against yet is **invisible to ArchUnitNET**. AC2 says the build must fail when *a project reference is added* — that is a `.csproj` fact, and only a `.csproj` gate sees it.
  - [x] **Assert the project inventory** (AC1): the solution contains exactly the thirteen named projects — the eight production projects and the five test projects — plus the one declared variance, `tests/Yello.Tests.Shared`. A missing or renamed project fails the build.
  - [x] **Assert the version pins** (AC1): every `<PackageVersion>` in `Directory.Packages.props` matches the AR-1 table exactly, the `global.json` SDK band is the pinned one, and `RuntimeFrameworkVersion` is `10.0.11`. The story statement requires "the stack versions enforced by tests that fail the build", so a pin drifting silently must break the build, not a review.
  - [x] **Assert the in-memory-provider ban** (AC4): no `.csproj` under `tests/` references `Microsoft.EntityFrameworkCore.InMemory`, and no `<PackageVersion>` for it exists centrally. AC4 states the ban as a property of the solution, so it needs a gate rather than a convention — the reason is worth keeping in the failure message: an in-memory provider cannot exercise row-level security, which is what NFR-1 rests on.

- [x] **Task 6 — Gate B: the type-dependency gate** (AC: 2)
  - [x] In `Yello.Tests.Architecture`, load all eight production assemblies with `ArchLoader` and assert with ArchUnitNET:
    - `Yello.Domain` types depend on no other Yello assembly (4 assertions for the ring rule per the test design's A-1).
    - No EF Core type is referenced from `Yello.Domain`; no ASP.NET Core type is referenced from `Yello.Application` or `Yello.Domain` (A-2, 2 assertions).
  - [x] These assertions are **vacuously true** today because the production projects hold only an `AssemblyMarker`. That is expected and correct (AC5: the suites run against a solution with no feature code). It also means they are unproven — which Task 9 addresses.

- [x] **Task 7 — Gate C: the Role-API ban** (AC: 3)
  - [x] Assert, across every production assembly, that nothing references `[Authorize(Roles = …)]`, `ClaimsPrincipal.IsInRole`, `IdentityRole`, or Identity's role store (`RoleManager<>`, `IRoleStore<>`) — 4 assertions per the test design's A-3.
  - [x] Record in the suite, as a comment or test name, that Identity remains permitted for authentication only: Account store, password hashing, cookie issuance. Nothing in this story wires Identity; the ban is what this story delivers.

- [x] **Task 8 — Aspire local orchestration** (AC: 4)
  - [x] `Yello.AppHost.csproj`: `<Project Sdk="Aspire.AppHost.Sdk/13.4.6">`, `OutputType=Exe`, `TargetFramework=net10.0`. Use a **project-based** AppHost, not a single-file `apphost.cs` — the 13.4.x CLI has a known recursion defect launching file-based AppHosts (`dotnet run --file apphost.cs`), fixed only from 13.5.
  - [x] Pin the SQL Server container image explicitly to `mcr.microsoft.com/mssql/server:2025-latest` via registry/image/tag rather than relying on the hosting integration's default tag.
  - [x] Add `Yello.Host` and `Yello.Client` as project resources; give the Host a reference to the database resource and have it wait for it.
  - [x] Verify `dotnet aspire run` brings up all three resources. **Docker Desktop is installed on this machine but its daemon is not running** — start it, in Linux container mode, before running.
  - [x] For "a working connection from Host to container": consume the Aspire-injected connection string and open it **once**, in Development only, logging the result. Do **not** implement this as a health check or a periodic task — AR-33 requires liveness and readiness probes to answer from process state with no database round trip, and forbids any component touching the database on an unconditional timer. Do **not** run migrations at startup (AR-36). Flagged in *Questions* below.

- [x] **Task 9 — Prove the gates, then prove the suites run empty** (AC: 2, 3, 5)
  - [x] For each of the four gates (ring reference, ring type-dependency, EF/ASP.NET type leak, Role-API ban): temporarily introduce a real violation, confirm the build fails and that the message names the offence, then revert. Record each result in the Dev Agent Record. The test design's definition of done states that "a test asserting the absence of a signal must be validated against a planted signal, or it is not a test" — with empty production projects, every gate in this story is an absence assertion.
  - [x] Add `<TestingPlatformCommandLineArguments>$(TestingPlatformCommandLineArguments) --ignore-exit-code 8</TestingPlatformCommandLineArguments>` to `Yello.Tests.Isolation`, `Yello.Tests.Revocation`, `Yello.Tests.Merge` and `Yello.Tests.Slices`. Microsoft.Testing.Platform returns **exit code 8** for "the test session ran zero tests" and is strict by default, so without this AC5 fails. Do **not** add it to `Yello.Tests.Architecture` — that suite ships ~10 real assertions in this story and must stay strict.
  - [x] Leave a comment at each `--ignore-exit-code 8` site instructing that it be removed from a suite as soon as that suite gains its first test, so a later filter typo cannot silently pass as "zero tests".
  - [x] Confirm `dotnet test` over the whole solution returns success: architecture green with real assertions, the other suites reporting zero tests.

- [x] **Task 10 — Establish the test conventions later stories depend on** (AC: 5)
  - [x] Document and use the trait vocabulary: `[Trait("Priority", …)]`, `[Trait("Suite", …)]`, `[Trait("Requirement", …)]`, `[Trait("Assumption", …)]`. The CI tiering selects on these filters, so they must exist with the suites rather than be retrofitted.
  - [x] Record the `Task.Delay` prohibition as a project convention: no test may synchronise on `Task.Delay`. The test design calls it "cheaper to enforce as a convention from story 1.1 than to unpick later".
  - [x] Record the shared-container topology note for later stories: one Testcontainers instance amortised across collections, **except** the pooled-connection isolation case (story 1.9), which needs its own container with pool size pinned to 1 and parallelism disabled.

### Review Findings

Code review of `c83450c..HEAD` (3 commits, 55 files, +2,982/−66), run 2026-08-26 with three
parallel adversarial layers — Blind Hunter, Edge Case Hunter, Acceptance Auditor. Baseline
re-verified at HEAD before triage: `dotnet build` clean (0 errors, 0 warnings), `dotnet test`
26 passed, exit 0. Every finding below was confirmed against the source during triage;
severities are the reviewer's, not the layers'.

**AC2, AC3 (for the four named shapes), AC4 and AC1's project inventory are met**, and the
planted-violation validation in Task 9 is the reason they can be trusted. The dominant theme
of the findings is narrower than "bugs": **several gates assert something materially weaker
than their names, comments and the ACs claim**, and three are defeated by a single line a
later story would plausibly write. Risk R7 was handled exactly as the test-design handoff
requires, and no later-story P0 scenario is pre-empted.

#### Decisions needed

- [x] [Review][Decision] **`NuGetAuditLevel=low` + `TreatWarningsAsErrors` makes any new advisory a build break** — **Resolved 2026-08-26 (Lee): keep it strict.** No change. Any newly-published advisory at `low` or above breaks the build until someone pins forward, and that is the accepted posture. Story 1.10's CI pipeline should be built knowing this — an upstream publication can block the build with no code change. Original finding: Evaluated on `Yello.Domain`: `NuGetAudit=true`, `NuGetAuditLevel=low`, `NuGetAuditTreatWarningsAsErrors=true`, `WarningsNotAsErrors=""`. The SSH.NET forward-pin is the first instance and is documented as "the gate working as intended". The consequence for the remaining 52 stories and for story 1.10's CI pipeline is that any newly-published low-or-above advisory on any transitive package breaks every build until a human hand-pins forward. Defensible as security posture, hostile as CI policy. Options: keep as-is; add `WarningsNotAsErrors` for NU1901–NU1904 plus a separate scheduled audit job; or raise the audit level. Needs a decision rather than discovery mid-sprint.
- [x] [Review][Decision] **The entire build-gate enforcement chain comes from an unasserted pre-1.0 package** — **Resolved 2026-08-26 (Lee): leave as-is, exposure accepted.** No gate added. The version is pinned at 0.0.11 so nothing floats on restore; the risk materialises only on a deliberate bump, which the file already declares a developer decision. Anyone bumping it should re-check that `TreatWarningsAsErrors` and `NuGetAuditTreatWarningsAsErrors` still evaluate true — `dotnet msbuild Yello.Domain/Yello.Domain.csproj -getProperty:TreatWarningsAsErrors` — because nothing will tell them otherwise. Original finding: `Directory.Packages.props:32` declares `Opinionated.DotNet.CodingStandards` 0.0.11 as a `GlobalPackageReference` and lines 25–29 deliberately exclude it from `PackageVersionPinTests`, declaring a bump "a developer decision". `Directory.Build.props` deliberately restates none of its properties (correctly — it is imported first, so restating would silently fork the standard). So `TreatWarningsAsErrors`, `Nullable`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-all`, `NuGetAudit`/`NuGetAuditLevel`/`NuGetAuditTreatWarningsAsErrors` and five analyser packages all have exactly one source, under `Condition="'$(X)' == ''"` defaults, that no gate reads. This is the mechanism that surfaced the SSH.NET advisory, and therefore the justification for that pin. A bump to 0.1.0 (already in the local NuGet cache) that changes one default silently disables it. `Directory.Build.props` quotes the project's own bar — invariants "enforced by construction … rather than by an agent remembering" — while delegating enforcement to the one thing nothing asserts. Options: gate the package version (contradicts the stated developer-decision stance); or assert the *effective* MSBuild properties regardless of source (more robust, and independent of where they come from).
- [x] [Review][Decision] **The SQL Server image tag floats, and is stated in two places with nothing keeping them equal** — **Resolved 2026-08-26 (Lee): single source plus a gate, tag keeps floating.** Became a patch below. The digest-pin question stays open until the RLS and AD-15 collation work in stories 1.5 / 2.6 gives a reason to freeze the engine. Original finding: `tests/Yello.Tests.Shared/SqlServerContainerFixture.cs:62` and `Yello.AppHost/Program.cs:18-21` both hard-code `mcr.microsoft.com/mssql/server:2025-latest`; no assertion compares them, and the tag is mutable in both. A cumulative-update push changes the engine that AD-15's `Latin1_General_100_BIN2` collation and NFR-1's row-level security are verified against, with no file in the repository changing. Already flagged in the fixture's own comments as an open question. The digest-pin question is yours; the two-places-one-value half is a patch either way.
- [x] [Review][Decision] **AC5's literal wording is self-contradictory and should be amended upstream** — **Resolved 2026-08-26 (Lee): amend `epics.md`.** Became a patch below. Note `epics.md` carries `status: final`, so this is a deliberate amendment to a finalised artifact, not a drafting fix — the Change Log should say so. Original finding: `epics.md:490-492` requires the four gating suites *including architecture* to report **zero tests**, while AC2 and AC3 require that same suite to fail the build on violations — impossible together. The implementation read AC5's operative contrast as "rather than failing to build", gave the four genuinely-empty suites `--ignore-exit-code 8` and deliberately withheld it from the architecture suite. That is the only coherent reading and it is documented. Not a code defect — but the AC text should be fixed in `epics.md` rather than re-litigated each time a suite gains its first test.

#### Patches

**All 33 applied 2026-08-26.** The architecture suite went from 26 assertions to 44.

*Claim narrowed 2026-08-27.* This paragraph previously read "every new absence assertion was
validated against a planted violation before being trusted, per the convention this story
established". That was broader than the plant table below supports: 18 assertions were new
against 21 plants, and several had none — the six Gate B ring rules for `Contracts`, `Merge`,
`Client`, `AppHost` and `Application`/EF, plus `No_two_project_files_share_a_name` and
`The_scan_can_read_every_assembly_in_the_solution`. Plant 8 failed the Gate B rules through
ArchUnitNET's empty-source-set behaviour rather than through a planted type dependency, so it
did not validate them. That gap was not academic: the unplanted
`The_scan_can_read_every_assembly_in_the_solution` was passing over **zero** test assemblies
under one output layout, which is the second pass's highest-severity finding. The four Gate B
rules and both other assertions are planted in *Task 9 (third pass)* above. See Dev Agent Record → *Completion Notes* for what each fix
was and → *Debug Log References* for the planted-violation results.

- [x] [Review][Patch] HIGH — `--ignore-exit-code 8` is permanent and ungated in four release-gating suites [tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj:32, and identically Revocation:32, Merge:32, Slices:32]. The only protection is a comment reading "REMOVE THIS THE MOMENT THIS SUITE GAINS ITS FIRST TEST", which names this exact risk and then does not gate it. Story 1.9 writes the isolation cases that SM-1 gates release on; a `[Trait]` typo or broken discovery then yields zero tests, `dotnet test` returns 0, and the suite reports success having asserted nothing. Fix: a gate asserting that any project carrying the switch contains no `[Fact]`/`[Theory]`. (`Yello.Tests.Architecture` correctly omits the switch — verified.)
- [x] [Review][Patch] HIGH — Gate C does not ban the idiomatic ASP.NET Core role APIs [tests/Yello.Tests.Architecture/RoleApiScan.cs:25-38, :155-197]. Unbanned and undetected: `AuthorizationPolicyBuilder.RequireRole(...)` (the standard policy-based role check); `RolesAuthorizationRequirement` (wrong namespace for both regexes); `IdentityBuilder.AddRoles<TRole>()` / `AddRoleManager<T>()`; and `RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" })`, which constructs the attribute in IL and so never appears in `CustomAttributes` — `Yello.Host.csproj` describes the host as Minimal API endpoints, making that the form this codebase will reach for. Also: `:183` requires exact `FullName` equality, so `class SpaceAuthorizeAttribute : AuthorizeAttribute` escapes; `IdentityRoleType` (`:31`) misses `IdentityUserRole<TKey>`, the account↔role join entity; `RoleStoreType` (`:38`) misses `IRoleClaimStore<TRole>`; and `:168-171` matches the bare method name `IsInRole` with no declaring-type check (false positives on any same-named helper, and blind to reflective invocation). AC3's letter — the four named shapes — is met; its intent is not, because role-based authorisation can be fully wired with all 26 assertions green.
- [x] [Review][Patch] HIGH — Gate C scans production assemblies only, but AC3 says "anywhere in the solution" [tests/Yello.Tests.Architecture/RoleApiScan.cs:91]. `ProductionAssemblies.All` excludes `tests/**`, so any banned role API in a test project or fixture passes all four A-3 assertions. The narrowing is inherited from Task 7's wording ("across every production assembly"); the AC and spine AD-1:75 both say the solution.
- [x] [Review][Patch] HIGH — `ManagePackageVersionsCentrally` and `CentralPackageTransitivePinningEnabled` are never asserted [Directory.Packages.props:4-5; tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:210-218]. Every pin assertion reads `PackageVersion` *elements* as XML, which remain present regardless. Set `ManagePackageVersionsCentrally=false` and all ~20 pins become inert — every version-less `PackageReference` resolves to latest-available — while all six pin assertions stay green. Set `CentralPackageTransitivePinningEnabled=false` and the SSH.NET 2026.0.0 transitive pin stops applying, silently reinstating GHSA-q939-rpr3-3284 (HIGH severity).
- [x] [Review][Patch] HIGH — the version-pin gate is subset-only, and the in-memory ban is defeated by a different provider [tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:65-91, :159-188]. `Every_AR1_dependency_is_pinned_to_the_specified_version` iterates `ExpectedPins` and never iterates the file, so `SSH.NET`, `Microsoft.Data.SqlClient`, `Testcontainers.MsSql`, `xunit.v3.extensibility.core`, `TngTech.ArchUnitNET.xUnitV3` and both Blazor WASM pins can be added, removed, re-versioned or set to a floating range with nothing failing. Sharper still: `No_EF_Core_in_memory_provider_is_centrally_available` tests one exact key and `No_test_project_references_an_EF_Core_in_memory_provider` substring-matches `"InMemory"` — so `Microsoft.EntityFrameworkCore.Sqlite` matches neither, and SQLite `:memory:` cannot exercise row-level security any more than the banned provider can, which *is* the ban's stated reason. Note the story hardened Gate A from subset to exact equality for precisely this defect class (`AllowedReferenceEdges.cs:22-30`) and left it in place here.
- [x] [Review][Patch] HIGH — `ProductionAssemblies.All` and `AllowedReferenceEdges.ProductionProjects` are two hand-maintained eight-element lists, never reconciled [tests/Yello.Tests.Architecture/ProductionAssemblies.cs:49-52 vs AllowedReferenceEdges.cs:41-51]. Add a ninth production project: the inventory gate forces the `ProductionProjects` and `Table` edits, and nothing forces the `ProductionAssemblies.All` edit. Gate B's ring rules and all four Gate C bans then silently never examine the new production assembly — the Role-API ban stops covering new production code, in the story that exists to prevent exactly that. One assertion closes it.
- [x] [Review][Patch] HIGH — solution membership is a substring match on raw text, so an XML comment drops a release-gating suite while the gate written to catch that stays green [tests/Yello.Tests.Architecture/SolutionInventoryTests.cs:40-55]. Proven empirically during review: wrapping the `Yello.Tests.Isolation` entry in `<!-- -->` makes `dotnet sln list` return 13 projects while `solutionText.Contains(p.Name)` still matches, so the assertion passes. The test's own failure message states the consequence it fails to prevent — "`dotnet build` and `dotnet test` over the solution would silently skip them - including any gate they contain". Also, the test is named `..._exists_on_disk_and_vice_versa` and implements only the disk→solution direction. Fix: parse the `.slnx` as XML and compare resolved paths in both directions.
- [x] [Review][Patch] HIGH — the Testcontainers fixture replaces SQL Server's engine-readiness probe with a bare TCP port check [tests/Yello.Tests.Shared/SqlServerContainerFixture.cs:66]. `MsSqlBuilder.Init()` registers a `WaitUntil` that shells `sqlcmd -Q "SELECT 1;"`; `.WithWaitStrategy(...)` **replaces** rather than appends. Verified by reflecting into `IContainerConfiguration.WaitStrategies` on Testcontainers.MsSql 4.6.0 — default `[UntilContainerIsRunning, MsSqlBuilder+WaitUntil]`, after this call `[UntilContainerIsRunning, UntilUnixPortIsAvailable]`. SQL Server binds 1433 well before `master`/`tempdb` recovery and login initialisation complete, so from story 1.3 onward every consuming suite races the engine and fails intermittently with login errors. `InitializeAsync`'s own doc comment (`:76`) claims it "waits for the engine to accept connections" — the code deleted exactly that, in a file whose remarks advertise "Readiness is a wait strategy, never a sleep." Fix: drop the override, or append rather than replace.
- [x] [Review][Patch] MEDIUM — no gate constrains package references per ring, and Gate B covers four of eight assemblies [tests/Yello.Tests.Architecture/RingDependencyTests.cs; AllowedReferenceEdges.cs:81-129 governs `ProjectReference` only]. `<PackageReference Include="Microsoft.EntityFrameworkCore" />` in `Yello.Application` passes all 26 assertions: Gate A has no allowed-package table, and Gate B is a *type*-dependency rule that fires only once a type is touched — the same declared-but-unused asymmetry the story closed for project references and left open for packages. Gate B also has **no rule of any kind** for `Yello.Contracts` (whose row is `[]`), `Yello.Merge`, `Yello.Client` or `Yello.AppHost`, and neither EF Core nor ASP.NET Core is banned from Contracts or Merge — the two assemblies compiled into the WebAssembly client.
- [x] [Review][Patch] MEDIUM — Gate B's bans match namespace spelling, not the way the dependency actually enters an inner ring [tests/Yello.Tests.Architecture/RingDependencyTests.cs:100, :114]. `^Microsoft\.EntityFrameworkCore\.` misses `Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions` — i.e. `services.AddDbContext<…>()` called from Application or Domain — and `^Microsoft\.AspNetCore\.` misses ASP.NET Core surface living under `Microsoft.Extensions.*`.
- [x] [Review][Patch] MEDIUM — Gate A reads only `.csproj`, so every MSBuild import is a blind spot [tests/Yello.Tests.Architecture/RepositoryLayout.cs:65-86; ProjectFileGateTests.cs:100-103]. Three bypasses that leave 26/26 green: a `<ProjectReference>` placed in `Directory.Build.props` gives *every* project — Domain included — a forbidden edge `DeclaredProjectReferences` never sees; a new `tests/Directory.Build.props` shadows the root file entirely (MSBuild imports only the nearest) and can set any framework property while the gate still finds its literal in the root file; and `<Compile Include="..\Yello.Domain\Invariants.cs" />` moves domain source across a ring boundary with no `ProjectReference` (Gate A blind) and no cross-assembly dependency (Gate B blind). AC2's "when a project reference is added" does not hold for the import path.
- [x] [Review][Patch] MEDIUM — the runtime pin is a raw substring match and `TargetFramework` is asserted nowhere [tests/Yello.Tests.Architecture/ProjectFileGateTests.cs:100-103]. `Assert.Contains("<RuntimeFrameworkVersion>10.0.11</RuntimeFrameworkVersion>", props)` passes if the string sits inside an XML comment or a `Condition`-guarded `PropertyGroup` that never evaluates, or if a later `PropertyGroup` overrides it. It inspects only the root `Directory.Build.props`, so a per-project override in any of the 14 `.csproj` files is undetected — and because `Directory.Build.props` is imported first, a project-level `<TargetFramework>net9.0</TargetFramework>` silently wins with no gate looking.
- [x] [Review][Patch] MEDIUM — semicolon-separated `Include` loses every item but the last [tests/Yello.Tests.Architecture/RepositoryLayout.cs:71]. MSBuild accepts `Include="..\A\A.csproj;..\B\B.csproj"`; `Path.GetFileNameWithoutExtension` over the whole value yields only `B`. Silent bypass wherever a row is non-empty: `Yello.Application`'s row is `["Yello.Domain"]`, so `Include="..\Yello.Infrastructure\…csproj;..\Yello.Domain\Yello.Domain.csproj"` produces declared `["Yello.Domain"]`, satisfies exact equality, and genuinely references Infrastructure. The same shape defeats the VSTest ban — `DeclaredPackageReferences` (`:78-86`) returns the raw value and `PackageVersionPinTests.cs:201` compares by element equality, so `Include="Microsoft.NET.Test.Sdk;xunit.v3"` slips through. Fix: split on `;`.
- [x] [Review][Patch] MEDIUM — `GlobalPackageReference` bypasses both halves of the in-memory ban and the VSTest ban [tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:213; RepositoryLayout.cs:81]. `CentralPackageVersions()` reads `PackageVersion` elements only and `DeclaredPackageReferences` reads `PackageReference` elements only, inside `.csproj` only. One line — `<GlobalPackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.11" />` in `Directory.Packages.props` — adds the banned provider to all 14 projects and is invisible to all three assertions that exist to stop it. `GlobalPackageReference` is already the established idiom in that file (`:32`), so this is the natural way a future story would write it.
- [x] [Review][Patch] MEDIUM — `VersionOverride` and a child `<Version>` element escape both the inline-version ban and the central-pin read [tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:103, :214]. Only the `Version` *attribute* is read. `VersionOverride` is central package management's sanctioned escape hatch and resolves normally, so a project can leave the AR-1 pins entirely with the gate green. And `<PackageVersion Include="…InMemory"><Version>10.0.11</Version></PackageVersion>` is filtered out of the dictionary, so the in-memory ban reports the package absent while NuGet honours the version and the project restores.
- [x] [Review][Patch] MEDIUM — NuGet package ids are case-insensitive but the central-availability check is Ordinal [tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:163, :218]. `<PackageVersion Include="microsoft.entityframeworkcore.inmemory" Version="10.0.11" />` restores correctly and `ContainsKey` misses it — AC4's ban bypassed by letter case alone. (The project-reference half at `:177` uses `OrdinalIgnoreCase` and is unaffected.) Untrimmed attribute values have the same effect in both directions.
- [x] [Review][Patch] MEDIUM — the AC4 connectivity check crashes the Host on any non-`SqlException` failure [Yello.Host/Program.cs:49-57]. `new SqlConnection(connectionString)` sits inside the `try` but `catch (SqlException)` does not cover it. Verified against Microsoft.Data.SqlClient 7.0.2: `Notakeyword=1` → `ArgumentException: Keyword not supported`; `Integrated Security=blah` → `ArgumentException`. Both escape the handler and kill the process at startup — precisely the outcome the comment at `:33-35` says was designed out ("a failure is logged rather than thrown … a Host that refuses to start would report that badly"). `connection.ServerVersion` at `:52` is likewise an `InvalidOperationException` source that is not a `SqlException`.
- [x] [Review][Patch] MEDIUM — AC4's check is silently disabled by a rename, and has zero automated coverage [Yello.Host/Program.cs:39; Yello.AppHost/Program.cs:23]. `GetConnectionString("yello")` duplicates the Aspire resource name as a bare literal in two projects with no gate comparing them. Rename the resource and the Host takes the `IsNullOrWhiteSpace` branch, logs `ConnectionStringMissing` at Warning, and starts normally — AC4's only evidence gone, process exit code unaffected. Nothing among the 26 assertions touches `Program.cs`, so removing the check, inverting its condition or breaking the name is caught by nothing; AC4 is evidenced only by a one-off manual run transcribed into this record.
- [x] [Review][Patch] MEDIUM — `Production_projects_sit_at_the_repository_root_and_test_projects_under_tests` never checks "at the repository root" [tests/Yello.Tests.Architecture/SolutionInventoryTests.cs:63-79]. The only condition is `underTests != shouldBeUnderTests`. Moving all eight production projects to `src/` leaves both false and records no violation — while the method's own doc comment (`:57-60`) claims it enforces "There is no `src/` — that is the Structural Seed's layout, reproduced literally."
- [x] [Review][Patch] MEDIUM — AR-1's "ASP.NET Core / Blazor WASM 10" pin is classified as not-AR-1 and left ungated [Directory.Packages.props:150-159]. Verified: `epics.md:146` lists it under "Pinned versions" and `ARCHITECTURE-SPINE.md:285` gives it its own stack-table row, so it *is* an AR-1 row. The chosen value (10.0.11) is correct; the classification and the gate exclusion are not. Mitigating: AC1's own enumeration (`epics.md:473`) drops the row while AR-1 keeps it, so the implementation followed the paraphrase rather than the source. Fix: add both packages to `ExpectedPins`.
- [x] [Review][Patch] MEDIUM — the Aspire CLI pin is a third ungated copy of the Aspire version [.config/dotnet-tools.json:12]. `13.4.6` is hard-coded and no test reads it. The story reasoned explicitly about this hazard for the AppHost `Sdk` attribute — "the one version in the solution able to drift away from the rest of the Aspire pin unnoticed" — and gated that copy (`PackageVersionPinTests.cs:139-152`) while missing the tool manifest, which is the same hazard.
- [x] [Review][Patch] MEDIUM — the shared fixture has never been executed, so the entry criterion it closes is unproven [tests/Yello.Tests.Shared/SqlServerContainerFixture.cs]. No suite constructs it and `Yello.Tests.Shared` holds no tests, so `InitializeAsync` never runs; `test-design-qa.md:225` requires a fixture *running* `mssql/server:2025-latest`. Combined with the wait-strategy finding above, the AR-1 Testcontainers 4.6.0 + xunit.v3 4.0.0 pairing is verified at compile time only — and Testcontainers.MsSql 4.6.0 locates `sqlcmd` inside the container at runtime (`FindSqlCmdFilePathAsync` exists precisely because that path moved between image generations), against a `2025-latest` image. The story applied "an absence assertion must be validated against a planted signal, or it is not a test" rigorously to every absence claim and not at all to this presence claim, where one throwaway container start would have proven it.
- [x] [Review][Patch] MEDIUM — fixture robustness: no startup timeout, no runtime-absent path, no state guard, no diagnostics on partial start [tests/Yello.Tests.Shared/SqlServerContainerFixture.cs:64-88]. No `WithStartupTimeout` and `InitializeAsync` accepts no `CancellationToken`, so a cold ~1.5 GB image pull, a host below SQL Server's memory floor, or a container that starts and never opens 1433 leaves the wait strategy retrying with no deadline — the run **hangs** rather than failing. With no container runtime present, `StartAsync` throws and all four release-gating suites become unrunnable rather than skippable (this repo runs Rancher Desktop, so that path is a live local concern, not theoretical). `ConnectionString` (`:73`) states its precondition only in a comment. And `DisposeAsync` tears the container down without capturing `GetLogsAsync()`, so *why* an engine failed to start is unrecoverable after the fact.
- [x] [Review][Patch] MEDIUM — the `Assumption` trait's documented format diverges from the test design's filter [tests/TESTING-CONVENTIONS.md:1615-ish (trait vocabulary section)]. The convention gives `A-3`; `test-design-qa.md:555` uses `[Trait("Assumption", "PRD-12-2")]` and `:576` documents the selective run `dotnet test --filter "Assumption~PRD-12"` for "every test resting on an unconfirmed assumption". A trait valued `A-3` never matches that filter. This is the one of the four traits not yet used, so it is cheap now and expensive once stories start copying it.
- [x] [Review][Patch] MEDIUM — `.editorconfig` is marked complete and listed as delivered, and does not exist [Task 1 checkbox; File List]. Verified: absent from disk and untracked; commit `fd556ff` deleted it (115 lines). `.gitattributes:29` says "There is no .editorconfig any more" and `tests/TESTING-CONVENTIONS.md:68` agrees. Three places in one commit disagree, and the wrong one is the File List — the artefact a reviewer or a later story reads to learn what exists.
- [x] [Review][Patch] MEDIUM — commit `fd556ff` is absent from the Change Log and File List, and the Completion Notes now misdescribe the tree [Change Log; File List; Completion Notes]. The Change Log stops at 2026-08-24; the commit is dated 2026-08-26. `.gitattributes` (50 lines, new) has no AC, no task and no File List entry. The Completion Notes still read "`CA1707`/`IDE1006` switched off under `tests/**`", whereas the delivered state is `CA1707 = none` **solution-wide**, a wider relaxation now covering production code. Task 1's checkbox still enumerates `Nullable`, `TreatWarningsAsErrors`, `ImplicitUsings`, `EnforceCodeStyleInBuild` and `AnalysisLevel=latest-recommended` as set in `Directory.Build.props`; the delivered file sets only `TargetFramework` and `RuntimeFrameworkVersion` (deliberately and correctly — see the Decision above), and `AnalysisLevel` is now `latest-all`. For a story at `Status: review`, the Dev Agent Record no longer describes the tree being reviewed.
- [x] [Review][Patch] LOW — `AR-35` is claimed by the suite and traced by nothing [tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj:4; tests/TESTING-CONVENTIONS.md:47]. The csproj heads the suite "AD-21 / AR-2 / AR-4 / AR-35" and the conventions map "AR-35 → Consistency Conventions", but no test carries `[Trait("Requirement","AR-35")]` — verified, zero matches across the suite. AR-35 is one of the five requirements this story owns.
- [x] [Review][Patch] LOW — gate-support robustness cluster [tests/Yello.Tests.Architecture/RepositoryLayout.cs, PackageVersionPinTests.cs, ProjectFileGateTests.cs, SolutionInventoryTests.cs]. Individually minor, collectively the difference between a gate that reports and a gate that errors: unguarded `XDocument.Load` (a malformed or zero-byte `.csproj` aborts the `foreach` mid-iteration, so partial coverage is indistinguishable from full); duplicate `PackageVersion` `Include` throws from `ToDictionary` instead of naming the duplication; `global.json` structure unguarded past the existence check, making the intended failure message unreachable for every malformation; `rollForward` and the `Microsoft.Testing.Platform` runner opt-in both unasserted despite the SDK pin's stated purpose being determinism; `.Single()` (`PackageVersionPinTests.cs:142`) and `FirstOrDefault` (`ProjectFileGateTests.cs:158`) on project lookups; `SolutionFile.Exists` (`ProjectFileGateTests.cs:126`) is unreachable because `Root` is *defined* as the directory containing `Yello.slnx`; the stray-solution search (`:132`) is root-only and non-recursive, checks for no second `.slnx`, and ignores `.slnf` filters (which can exclude the architecture suite from a CI run entirely); `IsBuildOutput` misses `artifacts/` and enumeration walks `.git`, `.claude` and `_bmad`, so a vendored `.csproj` arriving with a skill update presents as an architecture violation; `IsUnderTestsDirectory` is Ordinal case-sensitive (renaming `tests`→`Tests` makes the in-memory reference check vacuous, though the layout assertion then fails loudly, so it is not silent); `FindRoot` has no fallback when the test binary sits outside the repository (custom `ArtifactsPath`, a published test project, or CI downloading only the test artifact), throwing `TypeInitializationException` across all 26; `Except` deduplicates, so a doubly-declared reference is invisible; and the inventory `HashSet` keys on file name, so duplicate project files collapse.
- [x] [Review][Patch] LOW — the Blazor error surface was removed with no replacement [Yello.Client/wwwroot/index.html]. The template's `#blazor-error-ui` element was deleted (a documented scope decision). `blazor.webassembly.js` reports unhandled exceptions and reconnect failures by unhiding that element, so with it absent every unhandled client error is console-only and the page shows `Loading Yello…` or a stale render. Story 1.2 reintroduces an error surface; until then the failure mode is silent by construction.
- [x] [Review][Patch] LOW — orchestration and startup details [Yello.AppHost/Program.cs:23, :30-32; Yello.Host/Program.cs:36, :49-50]. `WaitFor(database)` waits on the Aspire *resource*, not on the `yello` catalog existing, so "container up, database missing" logs an Error (SQL 4060) and the Host starts anyway — indistinguishable from "container down" in the log and from success in the exit code. The startup open has no connect timeout and no cancellation token, so a reachable-but-unready endpoint delays Kestrel binding for the full driver retry budget on every Development start and Ctrl+C does not interrupt it. There is no `else` on the `IsDevelopment()` branch, so "check passed", "check failed" and "check never ran" are indistinguishable outside Development. Pooling is left on, which is the shape story 1.9's pooled-connection case exists to catch.
- [x] [Review][Patch] LOW — two wording corrections in the pin file [Directory.Packages.props:82]. "the two AR-1 pins disagree about this driver" — they do not pin `Microsoft.Data.SqlClient` at all; their transitive *floors* disagree, which is what the Change Log's own "conflicting floors" says correctly. And `AnalysisLevel` moved from Task 1's specified `latest-recommended` to `latest-all` (stricter, and fine) with no Change Log entry.

- [x] [Review][Patch] MEDIUM — the SQL Server image tag needs one source of truth and a gate [tests/Yello.Tests.Shared/SqlServerContainerFixture.cs:62; Yello.AppHost/Program.cs:18-21]. *From the resolved decision above.* Both projects hard-code `mcr.microsoft.com/mssql/server:2025-latest` independently. Give the value one home that both read, and add an assertion that the fixture's image and the AppHost's registry/image/tag agree, so the suites and local orchestration cannot silently run different engine builds. The tag continues to float by CU deliberately; the digest-pin question stays open for stories 1.5 / 2.6.
- [x] [Review][Patch] LOW — amend AC5's wording in `epics.md` [_bmad-output/planning-artifacts/epics.md:490-492]. *From the resolved decision above.* AC5 currently requires the four gating suites *including architecture* to report zero tests, which contradicts AC2 and AC3 requiring that same suite to fail the build on violations. Reword so the operative test is "builds and executes rather than failing to build", and exclude the architecture suite from the zero-tests clause. `epics.md` carries `status: final`, so record the amendment in its own change history rather than editing silently.

#### Deferred

- [x] [Review][Defer] MEDIUM — the documented "one container amortised across collections" is not implemented and cannot hold as built [tests/Yello.Tests.Shared/SqlServerContainerFixture.cs:30-35, :64-67] — deferred to the first consuming story. The class is a plain fixture with no `[CollectionDefinition]`, no assembly-level fixture and no `WithReuse(true)`; xunit constructs one instance per collection, and the five suites are five separate Microsoft.Testing.Platform *processes*, so cross-suite sharing is impossible without container reuse or an external orchestrator. Each consumer gets its own 2 GB SQL Server. Nothing detects the divergence. The topology decision belongs with story 1.9, which owns the single-connection variant; the doc comment overstating it as settled fact should be softened now.
- [x] [Review][Defer] LOW — declared edges are not the effective dependency closure [tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs:81-129] — deferred, by-design for now. `ProjectReference` output flows transitively and nothing sets `ReferenceOutputAssembly` or `PrivateAssets`, so "exact equality" describes what is *written*, not what a project can compile against: `Yello.Tests.Isolation` can `using Yello.Infrastructure` through Host despite its row naming only Host, Contracts and Shared. The production rows happen to be closed under transitivity today; nothing asserts that, so a future table edit could open a ring hole the "exact equality" wording conceals. Worth revisiting when a row actually opens.

#### Dismissed (6)

Recorded so they are not re-investigated:

1. **ArchUnitNET rules passing vacuously on an empty source set** — false positive. ArchUnitNET 0.13.3 *requires* positive evaluation: a rule whose source set matches nothing **fails** with "The rule requires positive evaluation, not just absence of violations. Use `WithoutRequiringPositiveResults()`…". Verified with a probe during review. The `AssemblyMarker` types keep every source set non-empty. An empty *target* set does pass silently, but for the A-2 rules that is the correct semantics. The real version of this concern is the `ProductionAssemblies.All` reconciliation patch above.
2. **The SSH.NET compensating control is a code-path argument where a load argument was needed** — `Directory.Packages.props:136-137` does give a load argument: "Testcontainers loads SSH.NET only on its remote/SSH port-forwarding path; the local Docker path used here never touches it."
3. **The `.slnx` reasoning overstates the preflight** — the concern was that a root `Yello.sln` would have matched where nested `*.csproj` did not. The skill's Validate Prerequisites step lists `*.csproj` without `*.sln` at all, which closes it regardless of the root-qualification reading.
4. **Line-ending normalisation, and whether the coding standard actually applies** — both tested and confirmed sound. `git ls-files --eol` shows no mixed-ending blob; evaluated properties on `Yello.Domain` confirm the full chain is live.
5. **`FindRoot` broken by IDE launch, `cd tests`, or working directory** — `AppContext.BaseDirectory` is CWD-independent. Only the outside-the-repository case is real, and it is in the robustness cluster above.
6. **`Directory.Build.props` no longer setting five of Task 1's seven properties, as a design defect** — the omission is deliberate and correctly reasoned: the file is imported before the package's props, so restating a value would silently fork the standard rather than reinforce it. Only the record drift and the `AnalysisLevel` change survive, as patches above.

### Review Findings — second pass (2026-08-26 / 27)

Code review of `fd556ff..HEAD` — commit `9459d4b`, the pass that closed the 33 first-pass
findings. 25 files, +2,972/−353. Three parallel adversarial layers again (Blind Hunter, Edge
Case Hunter, Acceptance Auditor), all on `claude-opus-5[1m]`. Every finding below was verified
against the source during triage; severities are the reviewer's, not the layers'.

**Baseline re-verified at HEAD before triage, independently of the Dev Agent Record:**
`dotnet build Yello.slnx` clean (0 warnings, 0 errors); `dotnet test Yello.slnx` 46 passed,
0 failed, 0 skipped, exit 0. Every numeric claim in the record checked out — 44 architecture
assertions (`-trait "Priority=P0"` → 44, and 44 `[Fact]`s), the 26→44 delta, and all six
per-requirement trait counts (AR-1 18, AR-2 22, AR-4 6, AR-35 4, NFR-1 2). **The shared
fixture was confirmed to genuinely run**, by reading the Docker event log rather than the
record: container created, `find /opt/mssql-tools*/bin/sqlcmd` executed, then
`sqlcmd -C -Q "SELECT 1;"` — so the single most consequential first-pass fix (the wait
strategy having *replaced* SQL Server's engine-readiness probe with a bare port check) is real
and works. One correction to the record: engine readiness measured ~5–6s here, not the ~21s
quoted, so the "~16-second race window" is warmth-dependent rather than a constant.

**AC2, AC3 and AC5 are met** (AC5 against the amended wording, and the amendment is
legitimate — recorded in `epics.md`'s own `amendments` frontmatter, self-consistent with AC2/AC3,
and not shaped to fit the implementation: `Yello.Tests.Slices` was never among AC5's four, so
the smoke test needed no accommodation). AC1 is met for the declared state. AC4's substrate is
correctly shaped but its evidence is the weakest of the five. No scope creep against the story's
own boundary table — no entities, no `DbContext`, no `:root`, no hex value, no endpoint, no CI,
no coverage threshold.

**The dominant theme is a recursion of the first pass's theme.** The first pass found that
several gates asserted materially less than their names and the ACs claimed. This pass finds
that **several of the gates written to fix that have the same defect** — and in two cases the
new file's own docstring states the property it fails to hold. `SolutionAssemblies.cs` is the
clearest: it was added to stop Gate C narrowing its scope in silence, its class comment says
"when one is not there, the gate **fails** rather than quietly scanning a smaller set", and it
`continue`s. Six of the seven HIGH findings are defeated by one line a later story would
plausibly write, which is the same calibration the first pass used.

**A second theme is narrower and worth naming: the plants validated the finding, not the
fix.** Where a first-pass finding cited a specific example, the fix closed that example and the
plant reproduced it — leaving the sibling routes open. Plant 17 put `GlobalPackageReference` in
`Directory.Packages.props`, the one file the new gate reads; plant 1 used a solo `[Fact]`, the
one attribute form the new regex matches; plant 14 restated `<TargetFramework>` in a `.csproj`,
not conditionally in the root props file. All three sibling routes are open and verified below.

#### Decisions needed

- [x] [Review][Decision] MEDIUM — **AC4's connectivity check still has no automated coverage, and the first-pass finding that said so is marked closed.** — **Resolved 2026-08-27 (Lee): an integration test in `Yello.Tests.Slices`.** Became a patch below. It is the only option that catches an inverted guard, and it discharges the stale-runtime-evidence patch at the same time by making the test the evidence rather than a transcript. Original finding: The rename half genuinely closed (the resource name has one home plus `ResourceNameLiteralPattern`), but nothing among the 44 assertions touches `Yello.Host/Program.cs`: deleting `CheckDatabaseConnectivityAsync`, inverting the `IsDevelopment()` guard or dropping the `await` is still caught by nothing. The record's own Completion Notes headline this as "**AC4's check, which had no automated coverage at all**" while adding none. Three defensible answers: (a) a source-level gate in the architecture suite asserting the call and its Development guard are present — cheap, consistent with the rest of the suite, and weak; (b) a real integration test in `Yello.Tests.Slices`, which already starts a container and already holds the fixture's only consumer — genuine coverage, and the only option that would catch an inverted guard; (c) accept the manual `aspire run` evidence and stop recording the finding as closed. Related: this is the AC whose runtime evidence is also stale (patch below), so (b) would discharge both.
- [x] [Review][Decision] LOW — **`#blazor-error-ui` was restored, which crosses this story's scope boundary into story 1.2.** — **Resolved 2026-08-27 (Lee): keep it, and record it as a declared variance.** Became a patch below. The element is what `blazor.webassembly.js` unhides to report an unhandled exception or a failed reconnect, so its absence is a real silent-failure mode rather than a missing decoration; story 1.2 should inherit it knowingly rather than discover it. Original finding: The first-pass finding was LOW and observational — "Story 1.2 reintroduces an error surface; until then the failure mode is silent by construction" — and asked for no change. The fix added markup carrying `style="display:none"`, which is the only CSS anywhere in this story, into a story whose boundary table assigns *every* design foundation to 1.2 and whose AC gates the token count at exactly 30 "so an incomplete token set is detectable rather than merely wrong". Defensible on the grounds that `blazor.webassembly.js` needs the element to report anything at all. Options: keep and record it as a declared variance alongside `tests/Yello.Tests.Shared`; revert and let 1.2 own it; or keep and note the inline style as the one sanctioned exception.
- [x] [Review][Decision] LOW — **Server infrastructure identifiers are now stamped into every assembly, including the WebAssembly payload.** — **Resolved 2026-08-27 (Lee): accepted as harmless, no change.** The image tag is public and the resource name is not a secret, so the cost is a few dozen bytes in the client payload. Recorded here so a later reader does not re-raise it, and so anyone who later puts a genuinely sensitive value behind this mechanism knows it reaches every assembly by default — that is the point at which this becomes a real finding. Original finding: `Directory.Build.props` emits `YelloSqlServerImage` and `YelloDatabaseResourceName` as `AssemblyMetadata` unconditionally for all 14 projects; only three assemblies read either value. Verified with `strings`: `mcr.microsoft.com/mssql/server:2025-latest` is present in `Yello.Client.dll`, `Yello.Contracts.dll` and `Yello.Domain.dll`. It sits awkwardly beside the same commit's new per-ring rule reasoning that Contracts and Merge are compiled into the client and so cross the wire boundary. Not a vulnerability — the image tag is public — but it is server topology shipped to every browser, and the mechanism was introduced to remove duplication, not to broadcast. Options: condition the metadata onto the three consuming projects; accept it as harmless; or have the three consumers read a generated internal constant instead.

#### Patches

- [x] [Review][Patch] HIGH — **`SolutionAssemblies` silently drops every test assembly, in the file written to stop exactly that** [tests/Yello.Tests.Architecture/SolutionAssemblies.cs:52-55]. `ExpectedOutputPath` returns `null` whenever the output tail climbs out of the project directory, and `Discover()` responds with `continue` — adding nothing to `Missing`. So `Unreadable` stays empty, `The_scan_can_read_every_assembly_in_the_solution` passes, and all four A-3 assertions quietly cover 8 assemblies instead of 14. **Verified empirically**: built with `-p:ArtifactsPath=<scratch>` and ran the suite with the documented `YELLO_REPOSITORY_ROOT` escape hatch — **44/44 green with zero test assemblies scanned**. Trigger is one line, `<UseArtifactsOutput>true</UseArtifactsOutput>`, and `RepositoryLayout.ExcludedDirectories` already lists `artifacts`, so the layout was anticipated. The class doc (`:20-22`) says "when one is not there, the gate **fails** rather than quietly scanning a smaller set. A gate that narrows its own scope in silence is the exact failure mode this file exists to fix"; `ExpectedOutputPath`'s own comment (`:87`) says "the caller turns an empty result into a named failure". Fix: `missing.Add(...)` instead of `continue`. This restores Gate C's coverage of `tests/**` — the widening that was the first pass's third HIGH.
- [x] [Review][Patch] HIGH — **`GlobalPackageReference` in `Directory.Build.props` bypasses the provider ban, the VSTest ban and the ring package ban** [tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:520-521; ProjectFileGateTests.cs:31]. `The_only_solution_wide_package_is_the_coding_standard` reads `GlobalPackageReference` from `Directory.Packages.props` **only**, and the import-file gate's `ReferenceItemTypes` is `["ProjectReference", "PackageReference"]` — so the item is invisible to both in `Directory.Build.props` *and* `Directory.Build.targets`. **Verified end-to-end** by two layers independently: the item evaluates into every project (`-getItem:GlobalPackageReference` shows `DefiningProjectFullPath` = `Directory.Build.props`), `Microsoft.EntityFrameworkCore.InMemory` reaches `project.assets.json`, and `Microsoft.NET.Test.Sdk` placed this way applies its build assets — `-getProperty:IsTestProject` and `GenerateProgramFile` both return `true` in a project declaring neither. Plant 17 used `Directory.Packages.props`, which is why the route survived. Compounding it, the gate's failure message (`ProjectFileGateTests.cs:280-282`) reassures the reader that "GlobalPackageReference in Directory.Packages.props is the one sanctioned solution-wide form, and PackageVersionPinTests asserts that set exactly" — true of that one file, and precisely what leaves the other two unguarded. Fix: add `GlobalPackageReference` to `ReferenceItemTypes`, exempting `Directory.Packages.props` as its sanctioned home.
- [x] [Review][Patch] HIGH — **Every project-file ban is bypassed by an MSBuild property in the `Include`** [tests/Yello.Tests.Architecture/RepositoryLayout.cs:162-168]. `ItemIncludes` reads the raw XML attribute and never expands properties. **Verified**: `<Orm>Microsoft.EntityFrameworkCore</Orm>` plus `<PackageReference Include="$(Orm)" />` restores `Microsoft.EntityFrameworkCore/10.0.11` while the string any XML reader sees is the literal `$(Orm)`. Defeats `No_project_references_a_package_its_ring_forbids`, `No_project_references_the_VSTest_SDK`, `No_test_project_references_a_provider_that_cannot_enforce_row_level_security`, and — via `Path.Combine(projectDir, "$(Shared)/X.cs")`, which never climbs out — `No_project_compiles_source_from_outside_its_own_directory`. The consequence is EF Core landing in `Yello.Application` *declared but unused*, which `AllowedReferenceEdges.cs:144-148` states Gate B cannot see and which is the entire reason the package ban exists. The ring **edge** gate is unaffected: an unexpanded `$(X)` presents as an unauthorised edge, so it errs strict. Fix, minimal and sufficient: reject any gated `Include` containing `$(`, naming it as unresolvable. Fix, thorough: read items via `dotnet msbuild -getItem:` so the gates see evaluated values — larger, and worth considering for the whole file rather than bolting on here.
- [x] [Review][Patch] HIGH — **The zero-test-exit-code gate cannot see the combined-attribute form of a test** [tests/Yello.Tests.Architecture/TestingConventionTests.cs:224, :212-216]. `\[\s*(Xunit\.)?(Fact|Theory)\s*[\](]` requires `]` or `(` after the attribute name, and a comma is neither. **Verified**: `[Fact]` ✓, `[Theory]` ✓, `[Fact(Skip="x")]` ✓, but `[Theory, InlineData(1)]` ✗, `[Theory, MemberData(nameof(X))]` ✗, `[Fact, Trait("a","b")]` ✗. `[FactAttribute]` — the fully-suffixed legal spelling — also misses. Story 1.9 writes SM-1's isolation cases into `Yello.Tests.Isolation`, and "the same isolation case on both surfaces" is `[Theory, InlineData]` shaped; `ContainsTestMethods` then reads the suite as empty, `--ignore-exit-code 8` legitimately stays, and a later `[Trait]` typo or broken discovery makes a release-gating suite exit 0 having asserted nothing. That is verbatim the failure mode the gate's own remarks and `TESTING-CONVENTIONS.md` ("That is a gate, not a request") claim to have closed. Plant 1 used a solo `[Fact]`. Fix: `@"\[\s*(Xunit\.)?(Fact|Theory)(Attribute)?\s*[\](,]"`. Same gate, second hole: `:212-216` substring-matches the `--ignore-exit-code 8` literal, so the multi-code form `--ignore-exit-code 2;8` is not recognised as carrying 8 (a populated suite keeps swallowing zero-test runs undetected) while an unrelated `--ignore-exit-code 80` reads as if it does. Parse the value: split after the flag and test set membership.
- [x] [Review][Patch] HIGH — **A `Condition`-guarded framework property in the root `Directory.Build.props` defeats the pin, and the gate's message says it cannot** [tests/Yello.Tests.Architecture/ProjectFileGateTests.cs:163, :205-210, :410-412]. `AssertPinnedOnce` counts only *unconditional* declarations in that file, so the real `net10.0` still reads as "exactly once"; `FrameworkRedeclarations` runs over `AllProjectFiles.Concat(OtherImportFiles())`, and `OtherImportFiles()` explicitly excludes the root `Directory.Build.props`. A conditional `<TargetFramework>net9.0</TargetFramework>` there is therefore seen by neither. **Verified**: `-getProperty:TargetFramework` returns `net9.0` while the gate reports one unconditional `net10.0`. AC1's ".NET 10.0.11" is defeated silently, and the failure message at `:184-186` actively misleads the next reader — "a value inside an XML comment or a Condition-guarded PropertyGroup does not count - it does not reach the build" is true of a comment and false of a condition that evaluates true. Plant 14 restated the property in a `.csproj`, which is the route that *is* covered. Fix: run `FrameworkRedeclarations` over every import file including the root, flagging conditional framework properties there specifically, and correct the message.
- [x] [Review][Patch] HIGH — **The central-package-management gate passes on any occurrence and ignores conditions** [tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:153-161]. `properties` is a `ToLookup` over every `PropertyGroup` child regardless of `Condition`, and the test asks `.Any(v => v == "true")`. So `true` followed later by `false` passes, and a `Condition`-guarded `true` passes while the build takes the other value. **Verified**: `-getProperty` returns `ManagePackageVersionsCentrally=false` where the XML the gate reads still says `true`. The consequential half is `CentralPackageTransitivePinningEnabled` — turn it off and the `SSH.NET` 2026.0.0 forward-pin stops applying, silently reinstating GHSA-q939-rpr3-3284 (HIGH severity), with no NuGet backstop equivalent to the NU1015 that protects the other switch. Note the sibling gate in `ProjectFileGateTests` already implements exactly the right rule (`IsUnconditional`, declared-exactly-once) and it was not carried across — the same non-transfer this commit's own notes criticise elsewhere. Fix: take the last unconditional declaration and require it to equal `true`.
- [x] [Review][Patch] HIGH — **Gate C still misses two non-reflective, first-party routes to role authorisation** [tests/Yello.Tests.Architecture/RoleApiScan.cs:213-245, :304-332, and the "Known limits" at :45-50]. Raised independently by two layers. (a) **The role claim.** `RequireClaim(ClaimTypes.Role, "Admin")`, `principal.HasClaim(ClaimTypes.Role, …)` and `FindFirst(ClaimTypes.Role)` touch no banned namespace, method or type: those methods are not in `ClassifyCall`'s list, `ClaimTypes.Role` is a `ldsfld` so `ClassifyCall` exits at its `is not MethodReference` guard, and `System.Security.Claims.ClaimTypes` is neither under a banned namespace nor named `*Role*`. So `[Authorize(Policy = "Admin")]` over a policy built from the role claim wires role-based authorisation with all four A-3 assertions green — and it is the first thing a developer told "use a policy, not `RequireRole`" reaches for, which is the same reasoning that put `RequireRole` in the ban last pass. (b) **`UserManager<T>`'s role surface.** `AddToRoleAsync`, `IsInRoleAsync`, `GetRolesAsync` and `RemoveFromRoleAsync` all hang off a type that is permitted (Identity for authentication is explicitly allowed), and none is matched. `RoleApiBanTests.cs:67-73` claims the ban covers role authorisation "in every form" while `RoleApiScan.cs:45-50` names only reflection and third-party assemblies as its limits. Fix: ban the role-claim operand and the `UserManager` role methods, and if either is deliberately out of scope, say so in Known limits rather than in a claim of completeness.
- [x] [Review][Patch] MEDIUM — **A `PackageVersion` with no version is skipped entirely, defeating the provider ban and the exact-set check** [tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:587-590]. `if (string.IsNullOrEmpty(include) || version is null) { continue; }` drops the element from the dictionary both assertions read. **Verified**: `<PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" />` restores successfully, resolving to the lowest available version with only NU1604 — so the banned provider is present and invisible to `No_EF_Core_in_memory_provider_is_centrally_available` *and* to `Directory_Packages_props_pins_exactly_the_expected_set`. Fix: record it as a problem rather than skipping it.
- [x] [Review][Patch] MEDIUM — **`PackageVersion` elements are read without the unconditional filter the sibling gate uses** [tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:580-582]. `CentralPackageVersions()` reads every `PackageVersion` descendant; a `Condition` on the element or its `ItemGroup` that evaluates false leaves the pin gate green while no pin reaches restore, and a version-less `PackageReference` then resolves freely. **Verified**: `-getItem:PackageVersion` returns empty for a condition-guarded declaration. `ProjectFileGateTests.IsUnconditional` (`:387`, `:393`) is the rule already written for this; it was not applied here.
- [x] [Review][Patch] MEDIUM — **AC4's named container image is now asserted by nothing** [tests/Yello.Tests.Architecture/TestingConventionTests.cs:112-130; Directory.Build.props:59]. AC4 requires "a `mcr.microsoft.com/mssql/server:2025-latest` container". `Values_shared_between_projects_are_stated_once_in_the_build` asserts that `<YelloSqlServerImage>` *exists* and that the `AssemblyMetadata` is emitted — never its value. **Verified**: `2025-latest` appears in exactly one non-prose location in the whole solution, its own definition at `Directory.Build.props:59`; changing it to `2022-latest` passes all 44 assertions. This is a regression introduced by the centralisation fix, which removed the two literals *and* the only thing capable of comparing them. The sole remaining backstop is `Assert.StartsWith("17.", …)` in `Yello.Tests.Slices` — the one non-release-gating suite, which skips when no runtime is reachable. Fix: assert the property's value against the expected reference, assembling the expected string the way `SqlServerImageLiteral` already does to avoid tripping the gate that bans the literal in `.cs` files.
- [x] [Review][Patch] MEDIUM — **`SplitImageReference` mis-splits a digest reference — the exact change three comments say is coming** [Yello.AppHost/Program.cs:170-187]. **Verified**: `mcr.microsoft.com/mssql/server@sha256:1a2b3c4d` yields registry `mcr.microsoft.com`, image `mssql/server@sha256`, tag `1a2b3c4d`, with no throw. `SqlServerContainerFixture` passes the whole reference to `MsSqlBuilder().WithImage(...)`, which parses digests correctly. So the moment the open digest question — stated in `Directory.Build.props:54-56`, `Yello.AppHost/Program.cs:113-114` and the fixture's `:83-87`, and left open for stories 1.5 / 2.6 — is settled by pinning a digest, the AppHost and the suites silently run different images from one shared value. That is precisely the divergence the assembly-metadata mechanism was introduced to make impossible, and `Values_shared_between_projects_are_stated_once_in_the_build` only checks that no source file restates the literal. Second, narrower case: a reference with no registry host (`library/postgres:17`) uses `library` as the registry. Fix: handle `@` by delegating the whole reference, and require the first segment to look like a host.
- [x] [Review][Patch] MEDIUM — **The "check never ran" log line is emitted at a level the shipped configuration filters out** [Yello.Host/StartupLog.cs:51-55]. `ConnectivityCheckSkipped` (EventId 1005) is `Level = LogLevel.Debug`, while both `appsettings.json` and `appsettings.Development.json` set `Logging:LogLevel:Default` to `Information`. So it produces no output in any environment — leaving "passed", "failed" and "never ran" exactly as indistinguishable as before, which is the whole reason the `else` branch was added. `Program.cs:50-53` and `StartupLog.cs:12-17` both state that distinguishing them "is the whole value of a check whose only output is a log line". Fix: `LogLevel.Information`. (Minor, same file: the remarks say "The four outcomes below" over six `[LoggerMessage]` methods, 1000–1005.)
- [x] [Review][Patch] MEDIUM — **`BuildConstants` reintroduces the `TypeInitializationException` hazard `RepositoryLayout` documents at length** [Yello.Host/BuildConstants.cs:271-272; Yello.Host/Program.cs:46]. The value is produced by a static property initialiser that throws, and `Program.cs:46` reads it *before* entering the `try`. `RepositoryLayout.cs:252-258` explains exactly why this shape is wrong — "Throwing from a static initialiser surfaces as `TypeInitializationException` … which names neither the cause nor the remedy". Missing or blank metadata therefore kills the Host at startup with the message swallowed, the outcome `Program.cs:40-42` says was designed out ("a failure is logged rather than thrown"), and it does so even in Production and Staging, which never use the value. `Yello.AppHost` avoids this by using a local function called inline. Fix: read the value inside the `try`, below the non-Development early return.
- [x] [Review][Patch] MEDIUM — **The cross-ring `Compile` gate uses a string prefix rather than a path boundary** [tests/Yello.Tests.Architecture/ProjectFileGateTests.cs:324-326]. `Path.GetFullPath` yields no trailing separator, so `..\Yello.Domain.Extras\Thing.cs` resolves to a path that `StartsWith(projectDirectory)` — **verified `True`**. A sibling directory whose name extends a project's (`Yello.Domain.Extras`, `Yello.Application.Slices`, `Yello.Host.Endpoints`) can have its source compiled into another ring with no `ProjectReference` for Gate A and no cross-assembly dependency for Gate B: the method's own remarks (`:288-292`) call that "both gates report green over a solution whose rings have been merged". Fix: compare against `projectDirectory + Path.DirectorySeparatorChar`.
- [x] [Review][Patch] MEDIUM — **Two further routes across a ring that no gate reads: a raw `<Reference>` and an explicit `<Import>`** [tests/Yello.Tests.Architecture/RepositoryLayout.cs:83-89, :130]. `<Reference Include="Yello.Domain" HintPath="..\Yello.Domain\bin\...\Yello.Domain.dll" />` crosses a ring with no `ProjectReference` for Gate A, and if the types are never touched, no `AssemblyRef` for Gate B — the declared-but-unused asymmetry the story closed for project references. Separately, `MsBuildImportFiles` covers only the three directory-positional files, so a `.csproj` carrying `<Import Project="..\shared.props" />` whose target declares a reference, a `GlobalPackageReference` or a framework property is read by nothing; explicit `<Import>` is the ordinary way to share MSBuild logic. Neither exists today (verified: no `<Import>` in any project file). Fix: add `Reference` to the gated item types and fail on any `<Import>` in a project file, so the "declared facts" the gates read stay the facts the build uses.
- [x] [Review][Patch] MEDIUM — **A `ProjectReference` is matched by file name alone, so a foreign project satisfies a permitted edge** [tests/Yello.Tests.Architecture/RepositoryLayout.cs:131]. `DeclaredProjectReferences` reduces each `Include` to its file name, so `..\..\vendored\Yello.Domain\Yello.Domain.csproj` — or any path outside the repository or under an excluded directory — reads as the permitted `Yello.Domain` edge. Fix: resolve the `Include` against the project directory and assert the result is one of `RepositoryLayout.AllProjectFiles`.
- [x] [Review][Patch] MEDIUM — **The shared-value gate accepts a conditional declaration and never checks what is stamped** [tests/Yello.Tests.Architecture/TestingConventionTests.cs:118-129]. A `Condition`-guarded property or `AssemblyMetadata` item, or an empty `Value`, leaves the gate green while nothing reaches the assembly — **verified**: `-getItem:AssemblyMetadata` returns empty for a condition-guarded declaration. The consumers then throw at startup (see the `BuildConstants` patch above) rather than the gate failing at build. Fix: require both declarations to be unconditional, and assert `Value` is `$(<property>)` rather than only checking `Include`.
- [x] [Review][Patch] MEDIUM — **The resource-name gate is bypassed by interpolation or a constant** [tests/Yello.Tests.Architecture/TestingConventionTests.cs:234]. `\b(AddDatabase|GetConnectionString)\s*\(\s*"` requires a quote immediately after the parenthesis, so `GetConnectionString($"yello")`, `GetConnectionString(Name)` where `const string Name = "yello"`, or the name held in a local all pass — reinstating the second source of truth this gate exists to prevent. Fix: also scan for the resource-name value itself, read from the metadata, anywhere in a `.cs` file.
- [x] [Review][Patch] MEDIUM — **`IsTestProject` is read as a declared literal, so a suite that inherits it is skipped by the zero-test policy** [tests/Yello.Tests.Architecture/TestingConventionTests.cs:206-210]. All five suites declare it today (verified), so the filter is not currently vacuous — but a future suite letting the xunit props infer the property drops out of the gate entirely, taking its `--ignore-exit-code 8` with it. Fix: treat any project under `tests/` with `OutputType=Exe` as a suite, or evaluate the property through MSBuild.
- [x] [Review][Patch] MEDIUM — **Fixture failure-path cluster: six ways a container problem arrives without its diagnosis** [tests/Yello.Tests.Shared/SqlServerContainerFixture.cs:100-104, :123-125, :141-147, :176-189, :219-224, :227-234]. Individually small, collectively the difference between a fixture that reports and one that misleads — and this is the fixture all four release-gating suites inherit from story 1.3 onward. (1) `:219-224` catches only Docker-typed exceptions around `GetLogsAsync`, so a throw there *replaces* the original startup failure and the cause is lost — it runs inside a `catch`, so it should catch broadly. (2) `:141-147`'s `when (exception is not InvalidOperationException)` filter excludes Testcontainers' own "Docker not running" shape, which arrives with no container diagnostics attached. (3) `:176-189` `IsContainerRuntimeAvailable()` tests for a reachable socket, not a working daemon, so a stopped-but-socketed runtime makes the smoke test wait out the full five-minute deadline and fail instead of skipping — the precise local condition the skip was written for on this machine. **Partially fixed, and the remainder stated rather than papered over:** the catch was broadened (it caught only `ArgumentException`, while Testcontainers also reports an uninterpretable endpoint as `InvalidOperationException`, which was turning a skip into an error in every consuming suite). Distinguishing "socket present, daemon dead" properly needs a daemon ping, and the only client for one arrives transitively through Testcontainers — referencing it directly would add an unpinned package, which Gate A correctly forbids. So the honest split is now documented on the method: configuration problems skip, daemon problems fail with a bounded deadline and container diagnostics. That is what makes the startup timeout non-optional rather than defensive. (4) `:227-234` silently falls back to the default when `YELLO_CONTAINER_STARTUP_TIMEOUT_SECONDS` is malformed (`600s`, `10m`, `0`, negative — `int.TryParse` rejects all), so the documented remedy does nothing and the same failure recurs; and ≥ 4,294,968 throws `ArgumentOutOfRangeException` outside the `try`, unwrapped (**verified**: 4,294,967 constructs, 4,294,968 throws). (5) `:100-104` gates `ConnectionString` on the container being non-null rather than ready, so a container whose engine never came up still hands out a plausible connection string. (6) `:123-125` has no guard against a second `InitializeAsync`, which abandons the first container undisposed and leaves a stray 2 GB SQL Server for the rest of the run.
- [x] [Review][Patch] MEDIUM — **The blanket plant-validation claim is broader than the plants, and the gaps are the new Gate B rules** [story record, *Task 9 (second pass)*; tests/Yello.Tests.Architecture/RingDependencyTests.cs:158-272]. The record states "every new absence assertion was validated against a planted violation before being trusted". 18 assertions are new and 21 plants are recorded, but several new assertions have none: the **six new Gate B ring rules** for `Contracts`, `Merge`, `Client`, `AppHost` and `Application`/EF, plus `No_two_project_files_share_a_name` and `The_scan_can_read_every_assembly_in_the_solution`. Plant 8 removed `AppHost` from `ProductionAssemblies.All`, which fails those rules through ArchUnitNET's empty-source-set behaviour rather than through a planted type dependency — no plant puts a `Yello.Domain` type inside `Yello.Contracts`, or an EF Core type in `Yello.Merge`. These are exactly the category the story's own definition of done governs: "an absence assertion must be validated against a planted signal, or it is not a test." Note the `SolutionAssemblies` finding above is what an unplanted `The_scan_can_read_every_assembly_in_the_solution` costs. Fix: plant the eight, and narrow the claim to what was actually validated.
- [x] [Review][Patch] MEDIUM — **Task 1's checkboxes still describe a tree that does not exist — the half of the first-pass finding that was not closed** [story lines 65, 67]. The finding cited two artefacts, "[Task 1 checkbox; File List]", and only the File List was corrected. Line 67 still reads `- [x] Create '.editorconfig' with the C# conventions the analysers enforce.` for a file `fd556ff` deleted and which `.gitattributes` and `TESTING-CONVENTIONS.md` both say is gone. Line 65 still lists `Nullable`, `TreatWarningsAsErrors`, `ImplicitUsings`, `EnforceCodeStyleInBuild` and `AnalysisLevel=latest-recommended` as set in `Directory.Build.props`, which sets only `TargetFramework`, `RuntimeFrameworkVersion` and the two shared values — deliberately and correctly, per the resolved decision — and `AnalysisLevel` is now `latest-all`. The first-pass finding named both lines verbatim.
- [x] [Review][Patch] MEDIUM — **The File List omits two of this commit's 25 files, reintroducing the defect the "records had stopped describing the tree" finding closed** [story File List]. Missing: `_bmad-output/planning-artifacts/epics.md` (*modified* — the AC5 amendment, the most consequential documentation change in the commit) and `_bmad-output/implementation-artifacts/deferred-work.md` (*new*). Both appear in the Change Log, so it is the File List specifically — which the first-pass finding singled out as "the artefact a reviewer or a later story reads to learn what exists".
- [x] [Review][Patch] MEDIUM — **The story file's own AC5 was never updated to the amended wording** [story lines 52-57]. `epics.md` was amended; the spec a downstream story actually reads still carries `**Then** each builds and executes, reporting zero tests rather than failing to build` — the text the amendment declares self-contradictory. Fix: mirror the amended wording here and cite the amendment, so the two artifacts cannot drift.
- [x] [Review][Patch] MEDIUM — **AC4's runtime evidence predates the rewrite of both files it evidences** [story *Debug Log References*]. `Yello.AppHost/Program.cs` (+70) and `Yello.Host/Program.cs` (+83) were materially rewritten in this commit — the image now arrives via `AssemblyMetadata` through the new `SplitImageReference`, and `AddDatabase`/`GetConnectionString` now take a metadata value — yet the `dotnet aspire run` transcript (the five-resource table, `Connected to SQL Server 17.00.4075`) is unchanged from the previous pass, and no test exercises either file. Verified: the story's diff contains no edit to that section. Combined with the AC4 coverage decision above, AC4's only evidence is a manual run against superseded code. Fix: re-run `dotnet aspire run` and re-record.
- [x] [Review][Patch] LOW — **Gate-support robustness cluster** [tests/Yello.Tests.Architecture/PackageVersionPinTests.cs:277, :281, :391, :401; ProjectFileGateTests.cs:358-362, :363-371]. Four sharp edges, none a bypass: `global.json` and `.config/dotnet-tools.json` singularity is unasserted, so a nested copy could select a different SDK or Aspire CLI while both gates read the root one — `MsBuildImportFiles` already establishes the pattern for the props files; both `JsonDocument.Parse` calls are unguarded, unlike `LoadXml`, so malformed JSON produces a bare `JsonException` naming no file and no remedy, and a numeric `"version": 13.4` throws from `GetString()` (use `GetRawText()`); the stray-solution search does its own enumeration filtering only `/bin/` and `/obj/`, bypassing `RepositoryLayout.ExcludedDirectories` — whose own doc warns that a file "arriving under `.claude` with a skill update … would fail a build nobody had changed" — so a vendored `.sln`/`.slnf` under `.claude`, `_bmad`, `_bmad-output` or `artifacts` does exactly that; and `Assert.True(SolutionFile.Exists, …)` is worth keeping rather than pruning as a tautology, because it stops being one on the `YELLO_REPOSITORY_ROOT` path this commit added.
- [x] [Review][Patch] LOW — **Comment and record drift cluster.** Six places where the prose no longer matches the code, in a suite whose whole discipline is that it must. (1) `PackageVersionPinTests.cs:141-149` states an exposure that was disproven during this review: setting `ManagePackageVersionsCentrally=false` does **not** leave "pins inert, assertions green" — in a `.csproj` it fails restore with NU1015, and in a root `Directory.Build.targets` the property evaluates false while restore still honours the central pin. The story's own plant 4 records this correctly; the comment was left stating the refuted version. Keep the assertion, rewrite the justification. (2) `PackageVersionPinTests.cs:105-127` — one `///` block carries two `<summary>` elements, so the "providers that cannot exercise row-level security" rationale now documents `CentralManagementSwitches` and `BannedProviders` at `:128` has no documentation at all. (3) `Yello.Tests.Architecture.csproj:4` still says the suite ships "roughly ten" assertions (44), omits `TestingConventionTests` from its Gate A list, and says "the four suites that are genuinely empty" (three). (4) `Yello.Tests.Slices.csproj` repeats the same "four suites" arithmetic. (5) `Yello.Client.csproj` still argues that the Blazor WASM packages are "not AR-1 stack choices" — the classification `Directory.Packages.props:152-167` and `PackageVersionPinTests.cs:65-72` now call "a misreading of the source"; it is the third of three places and the only one left on the retracted side. (6) `TestingConventionTests.cs:192` matches the image literal in any `.cs` text including comments and XML docs, so quoting the registry-qualified reference in prose fails the build — the reason `SqlServerImageLiteral` has to assemble itself by `string.Concat` to dodge its own scan. **Resolved differently from the proposal, deliberately:** the matcher was left alone and the rule documented instead. Stripping comments first means respecting string literals, and getting that wrong produces a *false negative* — a genuine second source of truth passing unseen — which is strictly worse than the false positive it removes, in a gate whose whole job is to be trusted. Prose names the MSBuild property instead; the existing comments in the fixture and the AppHost already do, which is why they pass. Confirmed while applying these patches: two of my own new comments tripped it by quoting the literals they described, and the gate was right both times.
- [x] [Review][Patch] LOW — **The interruptibility half of the connectivity check's contract does not hold, and a shutdown is reported as a timeout** [Yello.Host/Program.cs:66-72, :71-95]. `ConsoleLifetime` registers its SIGINT handler inside `IHost.StartAsync` — i.e. inside `app.RunAsync()` at `:25`, which has not been called when `:71` links to `app.Lifetime.ApplicationStopping`. So that token cannot fire during the check, and Ctrl+C there terminates the process outright; the comment's claim that "without **both** … the wait is not interruptible" is half true, and the 15-second deadline is doing all the work. Relatedly, if the linked token ever does cancel for shutdown, the handler reports "gave up waiting for SQL Server after 15s", which misdiagnoses a clean shutdown. Fix: correct the comment, and distinguish shutdown from deadline in the catch.
- [x] [Review][Patch] LOW — **The AC5 amendment writes today's suite inventory into a `status: final` artifact** [_bmad-output/planning-artifacts/epics.md]. The reworded clause names "the three that hold no cases yet — isolation, revocation, merge conformance", which becomes false the moment story 1.9 lands its first case and will need a second amendment to a finalised document. "the suites that hold no cases yet" carries the same meaning durably. Also, `Yello.Tests.Slices` moved off the zero-tests list by gaining a test, which is the mechanism proving the point.
- [x] [Review][Patch] LOW — **Change Log gaps** [story Change Log]. The 2026-08-26 entry records the move `review` → `in-progress`; the story header and `sprint-status.yaml` both now say `review`, with no entry recording the return. Rows are also not chronological — a 2026-08-24 row sits between two 2026-08-26 rows.

- [x] [Review][Patch] MEDIUM — **give AC4 real coverage with an integration test in `Yello.Tests.Slices`** [Yello.Host/Program.cs; tests/Yello.Tests.Slices/]. *From the resolved decision above.* The suite already starts a container for `SharedFixtureSmokeTest` and is not release-gating, so this costs no gate latency. The test must exercise the behaviour rather than the text: that the connectivity check runs in Development and opens a real connection against the fixture's container, and that it does **not** run outside Development — an inverted `IsDevelopment()` guard is the case a source-level assertion cannot catch, and the reason this option was chosen. Skip on the same `IsContainerRuntimeAvailable()` condition the existing test uses. This also discharges the *AC4 runtime evidence is stale* patch above: once the check is exercised by a test, the evidence is the test rather than a transcript that can go stale behind a rewrite. Note the `BuildConstants` and `ConnectivityCheckSkipped` patches change this code path, so land those first.
- [x] [Review][Patch] LOW — **record `#blazor-error-ui` as this story's second declared variance** [Yello.Client/wwwroot/index.html; story *Project Structure Notes* and *Scope decisions worth flagging*]. *From the resolved decision above.* The element stays. `Project Structure Notes` already carries `tests/Yello.Tests.Shared` as a stated variance from the Structural Seed and is the right home for a second: the markup and its `style="display:none"` are the only CSS in a story whose boundary table assigns every design foundation to story 1.2. State why it is here (the framework unhides it to report unhandled exceptions and failed reconnects, so its absence is a silent-failure mode, not a missing decoration), and state what 1.2 inherits — an element that exists and is deliberately unstyled, so 1.2's exactly-30-token gate is not pre-empted. Also correct the *Scope decisions* bullet, which still reads as though the template's styled error banner was removed with nothing put back.

#### Dismissed (5)

Recorded so they are not re-investigated:

1. **`EnumerateFiles("*.sln")` matching `Yello.slnx`** — investigated by two layers and confirmed not to occur on .NET 10 here. The `.Extension` filter at `ProjectFileGateTests.cs:365-366` is belt-and-braces rather than load-bearing; the *reasoning* recorded in Completion Notes finding 5 is a real Windows behaviour, so the filter is worth keeping either way.
2. **`ManagePackageVersionsCentrally=false` as a live "pins inert, gates green" exposure** — disproven. NU1015 fails restore outright from a `.csproj`, and from `Directory.Build.targets` the property evaluates false while restore still honours the central pin. Survives only as the comment-wording item in the drift cluster.
3. **Test-project rows of `AllowedReferenceEdges.Table` not being closed under transitivity** — real, and already logged in `deferred-work.md` by the first pass, so not re-raised. Worth noting the production rows *were* independently re-verified as genuinely closed this pass, which the ledger entry asserts.
4. **ArchUnitNET rules passing vacuously over an empty source set** — dismissed in the first pass and re-confirmed: 0.13.3 requires positive evaluation, and the `AssemblyMarker` types keep every source set non-empty. The reachable version of this concern is the `SolutionAssemblies` patch above.
5. **Server infrastructure identifiers reaching the WebAssembly payload** — raised as a decision, resolved as accepted (see above). The image tag is public and the resource name is not a secret. Kept in the record because the mechanism stamps *every* assembly by default, so the calculus changes the first time a sensitive value is put behind it.

## Dev Notes

### Scope boundary — what this story does NOT build

This story is a skeleton and four gates. Every item below is owned by a named later story; building any of it here is scope creep, and in two cases would actively conflict with a later story's acceptance criteria.

| Not in this story | Owner |
|---|---|
| Any entity, `DbContext`, migration or table | Story 1.3 creates the first three tables (`Account`, `Space`, `Membership`). Tables are created by the story that first needs them — never upfront. |
| Any CSS, colour token, type scale, spacing scale, focus ring, or the contrast harness | **Story 1.2.** Do not write a `:root` block or any hex value. Story 1.2's AC gates the token count at exactly 30 "so an incomplete token set is detectable rather than merely wrong" — a premature partial token set is precisely the failure that AC exists to catch. |
| The request pipeline's behaviours — authorisation, Space resolution, refusal recording, idempotency, NFR-8 bound checks | Stories 1.5 and 1.6. AR-3: a slice that re-implements any of them is a defect. |
| CI/CD, GitHub Actions, deployment, migrations-as-a-job | **Story 1.10.** "Build gates" in this story means test suites that fail `dotnet build` / `dotnet test`, not a pipeline. |
| An E2E / browser test project | Blocker **B5** (Playwright for .NET vs a separate TypeScript project) is undecided. It is decided at the `bmad-testarch-framework` run, which this story unblocks — see *The TF ordering* below. |
| Any code-coverage threshold | **Deliberately absent from the contract.** No coverage target appears in the PRD, the architecture spine, the epics or the spec kernel. The 80% figure in the test design is explicitly "offered as a proposal for Lee to accept or drop, not presented as an extracted requirement", and is called "the weakest gate on this list". Do not invent it. |
| The merge algorithm | AR-40a is open; story 7.1 writes the conformance suite first. `Yello.Merge` exists here as an empty, referenced project. |
| The AD-24 amendment | Readiness issue 3, due before Epic 3. Its earliest consumer is story 1.7. Irrelevant here. |
| AR-40c (`SESSION_CONTEXT` / `MAXDOP = 1` confirmation) | Story 1.10, before Epic 1's first production deploy. |

Nullable reference types, warnings-as-errors and analysers **are** in scope (Task 1) despite not being named upstream. They are ordinary build hygiene, not a budget, and the project's own stated bar supports them: *"invariants must be enforced by construction (type system, single choke point, lint/test gates) rather than by an agent remembering to read the spine. A rule that relies on discipline is not a rule here."* A coverage percentage is a budget, which is why it is excluded above and these are not.

### The five rings and the exact solution layout

Reproduced literally from the architecture spine's Structural Seed. Production projects sit at the repository root; test projects sit under `tests/`. There is no `src/`.

```text
Yello/
  Yello.AppHost/              # Aspire orchestration for local run
  Yello.Domain/               # entities, invariants, ports — references nothing
  Yello.Application/          # use-case slices + the request pipeline
  Yello.Infrastructure/       # EF Core, Identity, RLS session, outbox, email, merge adapter
  Yello.Host/                 # Minimal API endpoints, /sync WebSocket, composition root
  Yello.Contracts/            # wire DTOs, shared client + server
  Yello.Merge/                # ITextMergeStrategy implementation, shared client + server
  Yello.Client/               # Blazor WebAssembly
  tests/
    Yello.Tests.Isolation/    # SM-1 — every case on both surfaces
    Yello.Tests.Revocation/   # SM-2 — FR-34
    Yello.Tests.Merge/        # AD-12 conformance suite
    Yello.Tests.Architecture/ # AD-21
    Yello.Tests.Slices/
```

**Five test projects, four of which are release-gating.** `Yello.Tests.Slices` is the fifth and is not a gate. The readiness report says "the four test suites" in one section and "all five test projects" in another; both are correct and this is the reconciliation.

### Allowed reference edges

Derived from AD-21's prose for the four rings, and from the spine's dependency graph for the three projects AD-21's prose does not mention (`Contracts`, `Merge`, `Client`). AD-21 enumerates only Domain / Application / Infrastructure / Host, so the rules for the rest are **derived, not quoted** — flagged as such.

| Project | May reference |
|---|---|
| `Yello.Domain` | *nothing* |
| `Yello.Application` | `Yello.Domain` |
| `Yello.Infrastructure` | `Yello.Application`, `Yello.Domain` |
| `Yello.Host` | all of `Domain`, `Application`, `Infrastructure`, `Contracts`, `Merge` |
| `Yello.Contracts` | *nothing* |
| `Yello.Merge` | `Yello.Contracts` |
| `Yello.Client` | `Yello.Contracts`, `Yello.Merge` |
| `Yello.AppHost` | `Yello.Host`, `Yello.Client` (Aspire project resources) |
| `Yello.Tests.Architecture` | all eight production projects |
| `Yello.Tests.Isolation` / `.Revocation` | `Yello.Host`, `Yello.Contracts`, `Yello.Tests.Shared` |
| `Yello.Tests.Merge` | `Yello.Merge`, `Yello.Contracts` |
| `Yello.Tests.Slices` | `Yello.Application`, `Yello.Domain`, `Yello.Infrastructure`, `Yello.Host`, `Yello.Tests.Shared` |

Two derivation notes worth carrying forward: the spine's graph shows `Merge --> Contracts` as Merge's only outbound edge, yet describes `Yello.Infrastructure` as holding the "merge adapter" with no `Infra --> Merge` edge; and `Yello.AppHost` appears in the source tree and the graph but in **no** ring-table row. Neither blocks this story — the adapter arrives in Epic 7 — but the ArchUnitNET ruleset for these projects is inferred and should be revisited when Epic 7 wires the adapter.

### Pinned versions (AR-1) — every one re-verified against NuGet on 2026-08-22

All eight AR-1 pins exist exactly as specified. The spine's own web-verification review noted the stack was last verified 2026-08-17 and that "a full re-verification is due whenever the Stack table is next edited" — the spine was then edited on 2026-08-20, so this re-verification discharges that.

| Dependency | AR-1 pin | Exists | Latest available | Note |
|---|---|---|---|---|
| .NET SDK / runtime | 10.0.11 | ✅ runtime 10.0.11 present | SDK 10.0.303 installed | Pin via `global.json` + `RuntimeFrameworkVersion` |
| EF Core | 10 | ✅ `10.0.11` | 10.0.11 | Use **10.0.11** to track the runtime patch |
| ASP.NET Core Identity EF Core | 10 | ✅ `10.0.11` | 10.0.11 | Same |
| `Asp.Versioning.Http` | 10.0.0 | ✅ exact | 10.2.2 | Pin 10.0.0 as specified |
| Aspire | 13.4 | ✅ `13.4.0`–`13.4.6` | 13.5.2 | AR-1 gives no patch — use **13.4.6**, the last of the pinned minor line |
| `xunit.v3` | 4.0.0 | ✅ exact | 4.0.0 | Current latest stable |
| `Testcontainers.XunitV3` | 4.6.0 | ✅ exact | 4.14.0 | Pin 4.6.0 as specified |
| `TngTech.ArchUnitNET` | 0.13.3 | ✅ exact | 0.13.4 | Pin 0.13.3 as specified |
| SQL Server image | `mcr.microsoft.com/mssql/server:2025-latest` | — | floating tag | Not pinned by digest; see *Questions* |

Four pins have drifted from the current latest (`Asp.Versioning.Http`, Aspire, `Testcontainers.XunitV3`, `TngTech.ArchUnitNET`). **Implement the AR-1 pins as written** — AC1 asserts them, and changing a pin is an architecture edit, not a developer decision. The drift is raised in *Questions* for Lee, not resolved here.

Two compatibility facts confirmed by inspection: `TngTech.ArchUnitNET` 0.13.3 ships `net462` and `netstandard2.0` (loads fine in a `net10.0` test project) and depends on `Mono.Cecil` 0.11.6; `Aspire.Cli` 13.4.6 is `packageType="DotnetTool"`.

### Testing requirements

**Runner.** `xunit.v3` 4.0.0 → `xunit.v3.mtp-v2` 4.0.0. Microsoft.Testing.Platform is the only runner. Test projects are `OutputType=Exe`. No `Microsoft.NET.Test.Sdk`.

**The zero-tests trap (AC5).** MTP exit codes: `0` success, `2` at least one test failed, **`8` the test session ran zero tests**, `9` minimum execution policy violated. MTP "is designed to be strict by default", where VSTest tolerated an empty run. Note the precise failure mode: the *build* succeeds; `dotnet test` returns 8. The remedy is `--ignore-exit-code 8` per project, scoped to the four suites that are genuinely empty after this story (see Task 9). `--minimum-expected-tests` is the wrong lever — it governs exit code 9.

**Assertion budget for this story.** The architecture suite reaches 24 assertions across the project's life (A-1 … A-15). This story delivers **10 of them**: A-1 (ring rule, 4), A-2 (EF absent from Domain, ASP.NET absent from Application and Domain, 2), A-3 (Role-API ban, 4). A-4 onward need routes, tables, a bound registry and slices, and accrue in stories 1.5, 1.6, 2.6, 5.2, 5.3 and 7.1.

The project-file gate (Task 5) adds assertions **outside** that numbering — the declared-reference edges, the project inventory, the version pins and the in-memory-provider ban. They are not part of A-1 … A-15 because the test design scoped that series to bytecode and schema assertions; these read `.csproj`, `Directory.Packages.props` and `global.json`. Keep them in the same suite, in a separate test class, so the counts stay legible when later stories add A-4 onward.

**Commands.** `dotnet restore` → `dotnet tool restore` (installs the pinned Aspire CLI) → `dotnet build` → `dotnet test` → `dotnet aspire run`. `dotnet test` is the gate: the architecture suite must be green with real assertions while the other four report zero tests and still exit 0.

**The schema-test mechanism is seeded here.** Risk R7 (AD-15's `Latin1_General_100_BIN2` collation, irreversible because `ALTER DATABASE … COLLATE` is unsupported on Azure SQL) is owned by story 2.6 "with the schema assertion seeded in 1.1". Provide the mechanism for asserting against a migrated database schema — the `Yello.Tests.Shared` container fixture is that mechanism. Write no schema assertion yet; there is no schema.

**CI tiering, for context only** (built in story 1.10): PR / Nightly / Weekly, with the architecture suite running **first** in the PR stage because it takes seconds and should fail before anything slower starts; PR target under 15 minutes. Do **not** build smoke/P0/P1 tiers — the test design's checklist explicitly resolved that contradiction in favour of the three-stage shape.

**Conventions from the test design's definition of done**, established now: one behaviour per test, named for the behaviour; no `Task.Delay` as a synchronisation mechanism; randomised data where identity does not matter and fixed where an assertion depends on it, and **never a shared email address** (FR-1's uniqueness makes that a cross-test collision); cleanup by transaction rollback or container disposal, never by delete statements that themselves need RLS context; an absence assertion must be validated against a planted signal.

### The TF ordering — why this story comes first

`bmad-testarch-framework` was attempted on 2026-08-22 and **halted at preflight**: auto-detection found no project manifest of any kind because nothing is built. The TEA handoff had framed this as "TF and 1.1 collide unless one defers to the other"; the coverage tracker settles it — "story 1.1 *is* the solution skeleton, i.e. exactly the manifest set TF demands, so TF cannot precede it. Re-run after story 1.1 lands." Two consequences for this story: no framework scaffolding will be generated for it, so the test layout is built by hand from the Structural Seed; and blocker B5 stays open, so the browser/E2E project is deliberately not a deliverable here.

### Naming and vocabulary discipline

PRD §2 Glossary terms are used **verbatim** in every artifact including code: `Account`, `User`, `Space`, `Personal Space`, `Membership`, `Role`, `Owner`, `Admin`, `Member`, `Viewer`, `Invitation`, `Ownership Offer`, `Project`, `Task`, `Status`, `Assignee`, `Label`, `Board`, `List View`, `Presence`, `API Token`, `Session`. The spine adds `StatusDefinition`, `StatusDeltaOp`, `TaskLabel`, `TaskDescriptionChange`, `AccessRefusal`.

Forbidden in any identifier, namespace or folder name: `Workspace`, `Tenant`, `Org`, `Organisation`, `Team`, and `User` where `Account` is meant — `User` is reserved for "an Account acting in the context of a specific Space". A synonym is a discipline violation, not a style choice. Nothing in this story names a domain concept, but the constraint binds from the first commit.

Also settled and not to be reopened: a generic `TenantId` column filter or ambient tenant middleware is an **explicitly rejected shape**, because authorisation is a function of `(Account, Space)` via a many-to-many `Membership` rather than a property of the Account.

AR-34 conventions that bind every later story and that nothing here may contradict: `Guid` ids via EF Core's `SequentialGuidValueGenerator` — **not** `Guid.CreateVersion7()`, never sequential integers; `DateTimeOffset` in UTC everywhere, never `DateTime`; RFC 9457 `application/problem+json` with a stable machine-readable `type`.

One forward-compatibility instruction that reaches this story's structure: OAuth sign-in is the scheduled mid-flight change, so "implement FR-1, FR-2 and NFR-6 so those can change without redesign" — do not build email-as-identity in a way that cannot be revisited, and do not assume every Account holds a password.

### Project Structure Notes

**Use `Yello.slnx`, not `Yello.sln`.** *Superseded 2026-08-24. The original instruction was the reverse, on the grounds that `bmad-testarch-framework`'s preflight globs for `package.json`, `*.csproj`, `*.sln`, `playwright.config.*` and has no `.slnx` branch. Re-read against the skill, that rationale does not hold.* The backend indicator is the **alternation** `*.csproj`/`*.sln` (`step-01-preflight.md:47`), and the skill's *Validate Prerequisites* step lists `*.csproj` **without** `*.sln` at all (`:65`). Fourteen `.csproj` files satisfy backend detection on their own, so the solution's extension never reaches the decision. The same alternation, with the same `*.csproj` alternative, is what the `atdd`, `automate` and `ci` preflights use. No BMad skill in this install reads a solution file. **What the gate protects now is singularity, not format:** `dotnet sln migrate` writes the `.slnx` and *leaves the `.sln` in place*, and two solution files can disagree about the project inventory — which is the fact every other Gate A assertion reads.

**Second variance, stated deliberately — the Blazor error surface.** *Added 2026-08-27, resolved by Lee.* `Yello.Client/wwwroot/index.html` carries the template's `#blazor-error-ui` element, including `style="display:none"` — the only CSS anywhere in this story, in a story whose scope boundary assigns *every* design foundation to story 1.2. It is here because `blazor.webassembly.js` reports unhandled exceptions and failed reconnects by unhiding that element: without it, every unhandled client error is console-only and the page shows a stale render, which is a silent failure mode rather than a missing decoration. **What story 1.2 inherits:** an element that exists and is deliberately unstyled. 1.2 owns how it looks, and its gate on exactly 30 design tokens is not pre-empted — nothing here defines a token, a `:root` block or a hex value. The inline `display:none` is the one sanctioned exception, and it is markup the framework contract requires rather than a style choice.

**Variance from the spine, stated deliberately:** `tests/Yello.Tests.Shared` is a fourteenth project not named in the Structural Seed. AC1 says the solution "contains" the thirteen named projects; it does not say "and nothing else". The justification is that the shared SQL Server fixture is a stated entry criterion for *all* suites with no owning story, and every consumer of it arrives after this one.

**Unreconciled in the sources, decided here:** AR-3 says a slice folder holds "its command, handler, validator **and tests**", while the Structural Seed and AC1 also require a separate `tests/Yello.Tests.Slices` project. No document reconciles them. Decision: slice **test code** lives in `tests/Yello.Tests.Slices`, mirroring the `{Area}/{UseCase}/` folder structure of `Yello.Application`, so AR-3's "one folder" is the conceptual unit and the physical test project mirrors it. This is the only reading consistent with both requirements. Raised in *Questions*.

**Identifier provenance, so nothing is mis-cited later:** `AR-1 … AR-40` and `UX-DR1 … UX-DR42` exist **only in `epics.md`**. The architecture spine numbers `AD-1 … AD-29` and does not number its Stack, Structural Seed, Consistency Conventions or Deferred items; the UX spines number nothing. The mapping is not one-to-one (for example `AR-21` carries `AD-15`), and the spine has no back-reference to the `AR` ids. When citing mechanism, cite the `AR` id **and** the `AD` id where one exists. This story's gates trace: AR-1 → Stack + Structural Seed; AR-2 → AD-21; AR-4 → AD-1; AR-35 → Consistency Conventions → Tests.

**Two spine items added after every review ran:** the three review files are dated 2026-08-19 and the rubric walker records "27 ADs"; the spine now carries 29 and was updated 2026-08-20. **AD-28 and AD-29 have never been reviewed by any lens.** AD-29 (keyset pagination, DOM virtualisation forbidden) is Epic 2 material, not this story's, but treat both as unreviewed when they are reached.

### Environment preflight — verified on this machine, 2026-08-22

| Check | State | Action |
|---|---|---|
| .NET SDKs | 10.0.303 and 10.0.302 both installed | `global.json` pins 10.0.303 |
| .NET runtime 10.0.11 | present (`Microsoft.AspNetCore.App 10.0.11`) | matches AR-1 |
| `aspire` CLI | **not on PATH** | install as a local tool, `Aspire.Cli` 13.4.6 |
| Docker | Docker 29.6.2-rd installed, **daemon not running** | start Docker Desktop, Linux container mode, before `aspire run` |
| .NET workloads | none installed | not needed for standalone Blazor WASM; `wasm-tools` becomes relevant in Epic 7 when `Yello.Merge` is compiled to WASM |
| Node | v22.20.0 present | **not needed.** No npm, bundler, preprocessor or token-build step appears anywhere in the corpus. Do not introduce one. |
| Git | on `main`, 14 commits, all planning artifacts, **zero source files** | — |

### Git intelligence

Fourteen commits, none containing source code — the entire history is planning artifacts. There is no prior code pattern to follow, no previous story file, and no established convention to inherit; the conventions in this story are the first. The three most recent commits are directly relevant context rather than code: `a2240ee` records the TF halt, `841e14a` turns off `tea_use_playwright_utils` because the helpers are TypeScript against a .NET stack, and `e79e851` adds the system-level test design. Note that the `tea_use_playwright_utils` flip did **not** unblock TF — the binding constraint was the empty repository, which is what this story removes.

### Previous story intelligence

None. This is story 1 of 53 and the first story in the repository.

### References

- Story and epic definition, AR-1 … AR-4, AR-34, AR-35, AR-38, AR-40, UX-DR1 … UX-DR7, FR/NFR inventory, Story Coverage Index: [Source: _bmad-output/planning-artifacts/epics.md#Epic 1] and [#Story 1.1: The solution skeleton and its build gates], [#Story 1.2], [#Story 1.10], [#Additional Requirements], [#Story Coverage Index]
- Stack table, Structural Seed, source tree, Design Paradigm, Consistency Conventions, AD-1, AD-2, AD-4, AD-10, AD-12, AD-14, AD-15, AD-19, AD-21, AD-24, AD-25, Deferred: [Source: _bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md#Stack], [#Structural Seed], [#Design Paradigm], [#Consistency Conventions], [#AD-21 — The dependency rule is a build gate, not a convention], [#AD-1], [#Deferred]
- Review caveats, unreviewed AD-28/AD-29, the "treat SQL Server engine claims as unverified" rule: [Source: .../architecture-YelloBMAD-2026-08-17/reviews/review-web-verification.md#Pattern worth acting on], [#Not re-verified], [.../reviews/review-rubric-walker.md#Verdict]
- NFR-1 … NFR-9, §6 constraint blocks, §12 assumptions, OAuth forward-compatibility instruction: [Source: _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md#5. Cross-Cutting Non-Functional Requirements], [#6.3 Cost], [#12. Assumptions Index], [#9.2 Out of scope for MVP]
- Glossary, settled decisions, the rejected uniform-tenant-filter shape, absence of any coverage budget: [Source: _bmad-output/specs/spec-yello/glossary.md#preamble], [.../decisions-settled.md#What a tenant is], [.../quality-budgets.md], [.../SPEC.md#Constraints]
- Test levels, five test projects, trait vocabulary, definition of done, CI tiering, risk R7, entry criteria, coverage-gate proposal: [Source: _bmad-output/test-artifacts/test-design-qa.md#Test infrastructure setup (pre-implementation)], [#Appendix A: Conventions & Tagging], [#Execution Strategy], [#Entry Criteria], [.../test-design-progress.md#Step 4], [#Note on the coverage gate], [.../test-design-architecture.md#BLOCKERS], [.../test-design/YelloBMAD-handoff.md#Quality gates per epic]
- Verdict, open issues 3/4/5/6/8/9, "nothing blocks implementation", Story 1.1 starter-template and no-upfront-schema findings: [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md#Critical Issues Requiring Immediate Action], [#Step 5 > E. Starter Template and Greenfield Checks], [#Step 5 > D. Database and Entity Creation Timing], [#Remediation Applied — 2026-08-22]
- Blazor WASM as the client stack, the 1.1/1.2 boundary, no npm anywhere: [Source: _bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md#Foundation], [.../DESIGN.md#Brand & Style]
- TF halt and its resolution, the `tea_use_playwright_utils` finding: [Source: docs/bmad-coverage.md#Test Architecture Enterprise (TEA)]
- MTP exit codes and `--ignore-exit-code`: https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-troubleshooting
- Aspire 13 AppHost SDK attribute form, `aspire run`, CLI install: https://aspire.dev/get-started/aspire-sdk/ · https://aspire.dev/reference/cli/commands/aspire-run/ · https://aspire.dev/whats-new/aspire-13/
- ArchUnitNET analyses compiled bytecode via Mono.Cecil: https://github.com/TNG/ArchUnitNET

### Questions for Lee — raised, not resolved

1. **Version drift.** Four AR-1 pins are behind current latest: `Asp.Versioning.Http` 10.0.0 → 10.2.2, Aspire 13.4 → 13.5.2, `Testcontainers.XunitV3` 4.6.0 → 4.14.0, `TngTech.ArchUnitNET` 0.13.3 → 0.13.4. This story implements AR-1 as written. Do you want a spine amendment to refresh the Stack table, given its own review said a full re-verification is due whenever that table is next edited?
2. **`aspire run` vs `dotnet aspire run`.** A local tool manifest pins the CLI to AR-1's 13.4 line but changes the invocation to `dotnet aspire run`. A global script install gives the literal `aspire run` of AC4 but is not version-pinned. I have chosen the local tool for reproducibility, reading AC4 as naming the Aspire run path rather than the exact shell token. Confirm?
3. **The floating container tag.** `mcr.microsoft.com/mssql/server:2025-latest` is the only container image in the stack and is unpinned by digest or CU, so builds are not reproducible across time. Pin a digest, or accept the float?
4. **`tests/Yello.Tests.Shared`.** A fourteenth project not in the Structural Seed, to host the shared SQL Server fixture that is an entry criterion for all suites with no owning story. Accept, or fold the fixture into each suite?
5. **AR-3's colocated slice tests.** AR-3 says the slice folder holds its tests; the Structural Seed also requires `tests/Yello.Tests.Slices`. I have decided the physical tests live in the test project mirroring the slice structure. Confirm, or amend AR-3?
6. **The "working connection from Host to container" in AC4.** AR-33 forbids database round trips in probes and any component touching the database on a timer, so I have specified a one-shot Development-only connectivity log rather than a health check. Confirm that reading?
7. **The 80% coverage gate.** Explicitly a proposal in the test design, not a requirement. This story does not implement it. Accept or drop it — and if accepted, which story owns wiring it?

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (1M context) — `claude-opus-5[1m]`

### Debug Log References

Verified command chain, in order, all from the repository root:

| Command | Result |
|---|---|
| `dotnet --version` | `10.0.303` — confirms the `global.json` SDK pin resolves |
| `dotnet tool restore` | `Tool 'aspire.cli' (version '13.4.6') was restored` |
| `dotnet aspire --version` | `13.4.6+87fe259e4fc244c599019a7b1304c85a1488f248` |
| `dotnet build Yello.slnx` | `0 Error(s) 0 Warning(s)` — with `TreatWarningsAsErrors=true` across all 14 projects |
| `dotnet test Yello.slnx` | `Passed!  total: 52  failed: 0  skipped: 0` — exit 0 |

**Assertion counts, after the second review pass (2026-08-27).** 47 in
`Yello.Tests.Architecture` and 5 in `Yello.Tests.Slices`; the three suites that are still
genuinely empty report zero tests and still exit 0. The progression is 26 → 44 → 47 for the
architecture suite, and 0 → 2 → 5 for Slices. The three new architecture assertions are
`No_gated_item_hides_behind_an_MSBuild_property`,
`No_project_declares_a_raw_assembly_reference_or_an_explicit_import` and
`Every_declared_project_reference_resolves_to_a_project_in_this_repository`; the three new
Slices tests are AC4's coverage (see below). Most of the second pass hardened existing
assertions rather than adding new ones, which is why the count moved by three while nineteen
gates changed behaviour.

Of the 44, **10 are the A-series** this story owes — A-1 (ring rule, 4), A-2 (EF/ASP.NET leak,
2), A-3 (Role-API ban, 4). The other 34 sit outside the A-1…A-15 numbering, because that series
was scoped to bytecode and schema assertions: Gate A's read `.csproj`, `Directory.Packages.props`,
`global.json`, `.config/dotnet-tools.json` and the `.slnx` as files, and the Gate B additions
extend A-1 and A-2 to the four assemblies those two never named (`Contracts`, `Merge`, `Client`,
`AppHost`). They live in separate test classes so the counts stay legible when later stories add
A-4 onward.

**Trait selection re-verified** (Task 10), since CI tiering depends on it:

| Command | Result |
|---|---|
| `Yello.Tests.Architecture.exe -list traits` | `Priority: [P0]`, `Requirement: [AR-1, AR-2, AR-4, AR-35, NFR-1]`, `Suite: [Architecture]` |
| `-trait "Requirement=AR-1"` | 19 tests (was 18 before the second pass) |
| `-trait "Requirement=AR-2"` | 25 tests (was 22) |
| `-trait "Requirement=AR-4"` | 6 tests — Gate C, now including its own readability precondition |
| `-trait "Requirement=AR-35"` | 4 tests — was **0**, which is the review finding that AR-35 was claimed by the suite and traced by nothing |
| `-trait "Requirement=NFR-1"` | 2 tests |
| `-trait "Priority=P0"` | 47 tests (was 44) |

The runner banner also prints `64-bit .NET 10.0.11`, which is independent confirmation that
`RuntimeFrameworkVersion` binds the output to AR-1's patch rather than merely to the SDK.

**AC4 now has automated coverage, and this transcript is no longer its only evidence.**
*Added 2026-08-27.* `tests/Yello.Tests.Slices/StartupConnectivityCheckTests.cs` exercises the
check against a real containerised SQL Server: that it opens a connection in Development, that
it does **not** run outside Development even when a database would answer, and that a missing
connection string is reported rather than thrown. The middle one is the case a source-level gate
could not express — an inverted `IsDevelopment()` guard reads identically in source — and it is
why Lee chose a test over a gate. The check moved out of `Program.cs` into
`Yello.Host/StartupConnectivityCheck.cs` to make that reachable; a static local function inside
a top-level program cannot be called by a test.

The transcript below remains as the record of the orchestration graph working end to end, which
no in-process test covers. **Read its date, not just its content:** it was taken on 2026-08-26,
before `Yello.AppHost/Program.cs` and `Yello.Host/Program.cs` were both materially rewritten in
the patch pass, and the second review flagged precisely that — evidence that cannot notice the
code beneath it changing. The suite can; that is the point of replacing it.

**AC4 verified at runtime.** `dotnet aspire run` from the repository root, against the live
container runtime. All five Aspire resources reached ready:

| Resource | Evidence |
|---|---|
| `sql` — the container | `sql-uhvzkaqh \| mcr.microsoft.com/mssql/server:2025-latest \| Up \| 127.0.0.1:32768->1433/tcp` |
| `yello` — the database | `Resource 'yello' is ready` |
| `host` — `Yello.Host` | `Resource 'host' is ready`, `changed state: Running`, `Yello.Host.exe` in the process list |
| `client` — `Yello.Client` | `Resource 'client' is ready`, `changed state: Running`; Blazor devserver returned **HTTP 200** serving the stripped `index.html` |
| `aspire-dashboard` | `https://localhost:54564` |

**"A working connection from Host to container" — the actual log line**, from running
`Yello.Host` against the container Aspire had brought up, with the Aspire-injected connection
string:

```
info: Yello.Host.Startup[1001]
      Connected to SQL Server 17.00.4075, database yello.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://127.0.0.1:5411
```

`17.00.4075` is the SQL Server 2025 engine, so the pinned tag is what actually ran. Exactly one
connectivity line, at startup, before Kestrel binds — no health check, no timer, no migration,
per AR-33 and AR-36.

Worth recording because it demonstrates the orchestration graph rather than just its outcome:
on an earlier attempt the container runtime was down, and `aspire run` started the AppHost and
`Yello.Client` while **holding `Yello.Host` back entirely**. That is `WaitFor(database)` doing
its job — the Host does not start until the database is healthy. The container runtime on this
machine (Rancher Desktop) needed a `rdctl shutdown`, a `wsl --terminate rancher-desktop` and a
relaunch before its Windows named-pipe bridge stopped crash-looping; that is environmental, not
a property of the solution.

**AC5's exit-code-8 mechanism, proven in both directions** on `Yello.Tests.Merge`:

| State | `dotnet test` result |
|---|---|
| `--ignore-exit-code 8` removed | `Zero tests ran` … `Exit code: 8` — **fails** |
| `--ignore-exit-code 8` restored | `Zero tests ran` … exit **0** |

So the switch is load-bearing, not decorative, and the story's stated failure mode is exact:
the *build* succeeds and `dotnet test` returns 8.

One quirk found and worth carrying forward: running a suite's `.exe` **directly** invokes
xunit's own console runner, which returns **0** for zero tests and rejects
`--ignore-exit-code` as an unknown option. The exit-code-8 behaviour belongs to MTP mode under
`dotnet test`. Exit codes must be verified the way CI will invoke them, not by running the
binary by hand. This briefly looked like the story being wrong about MTP; it was not.

### Completion Notes List

#### Task 9 (third pass) — planted-violation results for the second review pass

Twenty-four violations planted 2026-08-27, one per gate added or hardened, **each confirmed to
be caught by a named assertion** and then reverted; plus one negative check. The suite returned
to 47/47 green after every one. Plants were applied and restored by a script that backs up and
rewrites files directly — never by `git checkout`, because the working tree held the whole patch
set at the time.

**Why this pass planted differently.** The second review found that the previous pass had
planted the *example each finding cited* rather than the *route the fix opened*: plant 17 put
`GlobalPackageReference` in `Directory.Packages.props`, the one file the new gate read; plant 1
used a solo `[Fact]`, the one attribute form the new regex matched; plant 14 restated
`<TargetFramework>` in a `.csproj`, the one location the new check covered. All three sibling
routes were open. Every plant below is therefore the sibling route, not the cited one.

| # | Planted violation | Caught by |
|---|---|---|
| 1 | `GlobalPackageReference` in **`Directory.Build.props`** | `The_only_solution_wide_package_is_the_coding_standard` **and** `No_MSBuild_import_file_declares_a_project_or_package_reference` |
| 2 | `<TargetFramework>net9.0</TargetFramework>` inside a `Condition` in the **root** `Directory.Build.props` | `The_target_framework_and_runtime_are_pinned_in_exactly_one_unconditional_place` |
| 3 | `<PackageVersion Include="Polly" />` with no version at all | the exact-pin-set assertion **and** the provider ban |
| 4 | `PackageVersion` inside a `Condition`-guarded `ItemGroup` | both, again |
| 5 | `YelloSqlServerImage` changed to `…:2022-latest` | `Values_shared_between_projects_are_stated_once_in_the_build` |
| 6 | `AssemblyMetadata` emitted only under a `Condition` | the same |
| 7 | `AssemblyMetadata` `Include` kept, `Value` emptied | the same |
| 8 | the database resource name restated as a literal in a `.cs` file | the same |
| 9 | `<PackageReference Include="$(PlantOrm)" />` resolving to EF Core in `Yello.Domain` | `No_gated_item_hides_behind_an_MSBuild_property` |
| 10 | a raw `<Reference>` with a `HintPath` crossing a ring | `No_project_declares_a_raw_assembly_reference_or_an_explicit_import` |
| 11 | an explicit `<Import Project="plant.props" />` in a project file | the same |
| 12 | `<Compile Include="..\Yello.Tests.IsolationExtras\Thing.cs" />` — a sibling whose directory name **extends** the project's | `No_project_compiles_source_from_outside_its_own_directory` |
| 13 | `[Theory, InlineData(1)]` — the combined attribute form — in a suite carrying `--ignore-exit-code 8` | `Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code` |
| 14 | `--ignore-exit-code 2;8` on a populated suite | the same |
| 15 | a second `global.json` under `tests/` | `The_global_json_pins_the_sdk_band_and_opts_into_the_test_platform` |
| 16 | a second `.config/dotnet-tools.json` under `tests/` | `Every_ungoverned_copy_of_the_Aspire_version_matches_the_pin` |
| 17 | `ClaimTypes.Role` read in `Yello.Host` | `No_code_applies_Authorize_with_a_Roles_argument` |
| 18 | the role claim type written out as its literal URI | the same |
| 19 | `ProjectReference` resolving to a project outside the source tree | `Every_declared_project_reference_resolves_to_a_project_in_this_repository` (plus the edge table) |
| 20 | a `Yello.Contracts` type touching `Yello.Domain` | `Contracts_types_depend_on_no_other_Yello_assembly` |
| 21 | a `Yello.Client` type touching `Yello.Domain` | `Client_types_depend_on_nothing_but_Contracts_and_Merge` |
| 22 | a `Yello.Merge` type touching `Yello.Domain` | `Merge_types_depend_on_nothing_but_Contracts` |
| 23 | `class PlantedEfLeak : DbContext` in `Yello.Contracts` | `No_EF_Core_or_ASP_NET_Core_type_is_referenced_from_Contracts_or_Merge` (plus the ring package ban) |
| 24 | two project files sharing a name (`tests/PlantDup/Yello.Tests.Merge.csproj`) | `No_two_project_files_share_a_name` (plus the solution/disk agreement gate) |
| — | **negative check:** a vendored `.sln` under `.claude/` | **nothing** — 47/47 green, which is the false-positive fix confirmed. Before it, this failed a build nobody had changed. |

**Plants 20–23 close four of the six Gate B rules the previous pass left unvalidated**, which
the second review raised: those rules had been added for `Contracts`, `Merge`, `Client` and
`AppHost` and no plant had ever put a forbidden type inside any of them.

**The `SolutionAssemblies` plant is the one worth reading in full**, because it is the pass's
highest-severity finding and the only one whose "before" state was measured. Built with
`-p:ArtifactsPath=<scratch>` and run through the documented `YELLO_REPOSITORY_ROOT` escape
hatch, the suite previously reported **44/44 green having scanned zero test assemblies** — Gate
C silently covering 8 of 14. After the fix:

```
RoleApiBanTests.The_scan_can_read_every_assembly_in_the_solution [FAIL]
  - Yello.Tests.Architecture (its assembly could not be located: the build output
    layout is not the one this convention assumes ...)
  - Yello.Tests.Isolation  (…)   - Yello.Tests.Merge   (…)
  - Yello.Tests.Revocation (…)   - Yello.Tests.Shared  (…)   - Yello.Tests.Slices (…)
Total: 47, Failed: 1
```

**Two assertions remain unplanted, stated rather than implied**, because the story's own
standard is that an unvalidated absence assertion is not a test and a blanket claim of coverage
was itself a finding this pass:

1. **`UserManager<>`'s role surface** (`AddToRoleAsync`, `IsInRoleAsync`, `GetRolesAsync` and
   siblings, newly banned in `RoleApiScan`). Planting it requires referencing ASP.NET Core
   Identity, which no project does until **story 1.3** wires it. That story should plant it.
2. **`AppHost_types_depend_on_no_Yello_assembly`.** Unplantable by construction: the AppHost's
   project references are Aspire project *resources*, which the Aspire SDK marks
   `ReferenceOutputAssembly=false`, so `Yello.AppHost` cannot compile against a Yello type at
   all. The rule is a statement about a dependency the build makes impossible; there is nothing
   to plant unless that changes.

#### Task 9 (second pass) — planted-violation results for the review patches

Twenty-one violations planted, one per gate added or hardened in the patch pass, each confirmed
to fail the build and then reverted. Run 2026-08-26; the suite returned to green after each.

| # | Planted violation | Caught by |
|---|---|---|
| 1 | `--ignore-exit-code 8` restored to `Yello.Tests.Slices`, which now has tests | `Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code` |
| 2 | `ClaimsPrincipal.IsInRole` in a **test** project (`Yello.Tests.Merge`) | `No_code_calls_IsInRole_on_a_principal` — the scope widening, proven |
| 3 | `RequireRole`, `new AuthorizeAttribute { Roles = … }`, an `AuthorizeAttribute` subclass, `AddRoles<>()`, `IRoleClaimStore<>`, `IdentityUserRole<Guid>` | A-3.1, A-3.3 and A-3.4 — all six forms, none of which the original four assertions saw |
| 4 | `ManagePackageVersionsCentrally=false` | **NuGet, not a gate**: NU1015 breaks restore outright. See below |
| 5 | An unexpected `PackageVersion` (`Polly`) added to the file | `Directory_Packages_props_pins_exactly_the_expected_set…` |
| 6 | `Microsoft.EntityFrameworkCore.Sqlite` pinned centrally | the provider ban + the exact-set assertion |
| 7 | Same, spelled `microsoft.entityframeworkcore.inmemory` | both, again — the case-sensitivity bypass closed |
| 8 | `Yello.AppHost` removed from `ProductionAssemblies.All` | `The_loaded_production_assemblies_are_exactly_the_production_projects`, plus four Gate B rules |
| 9 | `Yello.Tests.Isolation` commented out of `Yello.slnx` | `The_solution_file_and_the_disk_agree…` — the exact bypass the review demonstrated |
| 10 | `Microsoft.EntityFrameworkCore` `PackageReference` in `Yello.Application` | `No_project_references_a_package_its_ring_forbids` |
| 11 | `ProjectReference` in `Directory.Build.props` | `No_MSBuild_import_file_declares_a_project_or_package_reference` |
| 12 | A nested `tests/Directory.Build.props` shadowing the root | `Exactly_one_of_each_MSBuild_import_file_governs_the_solution` |
| 13 | Domain source excluded from Domain and `Compile Include`d into Contracts | `No_project_compiles_source_from_outside_its_own_directory` |
| 14 | `<TargetFramework>` restated in `Yello.Merge.csproj` | `The_target_framework_and_runtime_are_pinned_in_exactly_one_unconditional_place` |
| 15 | `<RuntimeFrameworkVersion>` declared twice in `Directory.Build.props` | the same assertion |
| 16 | `Include="…Yello.Domain.csproj;…Yello.Merge.csproj"` on `Yello.Tests.Merge` | `Every_project_declares_exactly_the_references_the_dependency_rule_allows` |
| 17 | `GlobalPackageReference` for the in-memory provider | `The_only_solution_wide_package_is_the_coding_standard` |
| 18 | `VersionOverride` on a `PackageReference` | `No_project_declares_a_package_version_of_its_own` |
| 19 | `.config/dotnet-tools.json` bumped to Aspire 13.5.2 | `Every_ungoverned_copy_of_the_Aspire_version_matches_the_pin` |
| 20 | The image string hard-coded back into the fixture | `Values_shared_between_projects_are_stated_once_in_the_build` |
| 21 | `[Trait("Assumption", "A-3")]` in the old format | `Every_Assumption_trait_carries_a_source_identifier…` |

**Plant 11 found a real defect in the gate it was testing, which is the argument for the
discipline.** `No_MSBuild_import_file_declares_a_project_or_package_reference` reused the file
filter its neighbour needs — the one that excludes the root `Directory.Build.props`, because
that file is the legitimate home of the framework pin. The consequence was that the reference
check skipped the single most likely place for the violation, and the planted `ProjectReference`
went undetected on the first run. Fixed to read every import file; the plant then failed it.

**Plant 4 is recorded as "build fails" rather than "gate fails", accurately.** Turning central
package management off produces NU1015 (`PackageReference items do not have a version
specified`) for every project, because no project in this solution carries an inline version —
which `No_project_declares_a_package_version_of_its_own` independently enforces. So the exposure
the review described (pins inert, assertions green) cannot occur here while that holds. The
assertion is kept as a direct statement of the invariant rather than an inference from it.

#### Task 9 (first pass) — planted-violation results

Every gate this story ships is an absence assertion against empty production projects, so each
was failed on purpose before being trusted. All plants were reverted and the suite returned to
26/26 green after each.

| # | Gate | Violation planted | Build/test failed? | Did the message name the offence? |
|---|---|---|---|---|
| 1 | **Gate A** — ring reference (project file) | `Yello.Domain` → `Yello.Contracts` `ProjectReference`, **no code using it** | Yes — 2 of 26 failed | Yes: `Yello.Domain/Yello.Domain.csproj: 'Yello.Domain' -> 'Yello.Contracts' is NOT PERMITTED. 'Yello.Domain' may reference NOTHING.` |
| 2 | **Gate B** — ring type dependency | added a `Yello.Domain` type using `Yello.Contracts.AssemblyMarker` | Yes — 3 of 26 failed | Yes: ArchUnitNET named the rule, the assemblies and the `Because` reason |
| 3 | **Gate B** — EF Core type leak (A-2) | `Microsoft.EntityFrameworkCore` referenced from `Yello.Domain`, using `DbContext` | Yes — 1 of 26 failed | Yes: `Yello.Domain.PlantedEfCoreLeak does depend on "Microsoft.EntityFrameworkCore.DbContext"` |
| 4 | **Gate C** — Role-API ban (A-3, all four) | `[Authorize(Roles = "Admin")]`, `principal.IsInRole(...)`, `typeof(IdentityRole)`, `typeof(RoleManager<IdentityRole>)` in `Yello.Host` | Yes — all 4 A-3 assertions failed **independently** | Yes, each naming its own site — e.g. `Yello.Host.PlantedRoleApiUse.CheckRole calls System.Security.Claims.ClaimsPrincipal.IsInRole` |

**Plant 1 empirically confirmed the story's reason for having two gates.** With the forbidden
`ProjectReference` present but unused, Gate A failed and **every Gate B ring assertion passed**.
Roslyn emits no `AssemblyRef` for a referenced assembly whose types are never touched, so the
violation was genuinely invisible to ArchUnitNET. AC2 requires the build to fail when *a
project reference is added*; only a project-file gate sees that. This is now measured rather
than argued.

**Gate C specificity also checked**, because an over-broad ban would be its own defect. A file
containing only the *permitted* shapes — `[Authorize(Policy = "SpaceMember")]` and a plain
`ClaimsPrincipal` member access — left all 26 green. The ban is on the `Roles` argument and on
`IsInRole`, not on `[Authorize]` or on `ClaimsPrincipal`, which is exactly AC3's "Identity
remains wired for authentication only".

#### Findings that changed the implementation

1. **A subset-based ring gate does not gate.** Gate A first asserted only that each declared
   edge was *permitted*. It reported green over a solution with **no project references at
   all**, because the empty set is a subset of everything — `dotnet add reference --no-restore`
   had failed silently for all 21 edges (`--no-restore` is not a valid flag for that command,
   and it produced no error output). The gate now asserts **exact equality** in both
   directions, so a missing edge fails too. Task 4's wording is "wire … to exactly the allowed
   edges", so this is also the literal reading.
2. **`Testcontainers` 4.6.0 drags in a HIGH-severity advisory.** It depends on `SSH.NET`
   2024.2.0 — GHSA-q939-rpr3-3284, arbitrary file write via server-controlled SCP filenames.
   `TreatWarningsAsErrors` promoted NuGet's NU1903 to an error, which is the gate working.
   Refreshing the AR-1 pin would **not** have fixed it: the advisory covers everything up to
   and including 2025.1.0 and is first patched in **2026.0.0**. Resolved by a transitive pin to
   `SSH.NET` 2026.0.0 (`CentralPackageTransitivePinningEnabled`), leaving the AR-1 table
   untouched. Yello never calls `ScpClient`; Testcontainers loads SSH.NET only on its
   remote/SSH port-forwarding path.
3. **Two AR-1 pins disagree about the SQL driver.** `Aspire.Hosting.SqlServer` 13.4.6 requires
   `Microsoft.Data.SqlClient` >= 7.0.1, while `Microsoft.EntityFrameworkCore.SqlServer` 10.0.11
   requires >= 6.1.6. Pinning EF Core's floor produced NU1109 (`detected package downgrade …
   from 7.0.1 to centrally defined 6.1.6`). Pinned **7.0.2**, the newest 7.x stable, which is
   where both floors are satisfied.
4. **`dotnet test` needs an explicit MTP opt-in, and it lives in `global.json`.** On the .NET 10
   SDK the VSTest target refuses outright: *"Testing with VSTest target is no longer supported
   by Microsoft.Testing.Platform on .NET 10 SDK and later."* The opt-in is
   `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json` — **not**
   `dotnet.config`, which I tried first and which had no effect. Without it the story's stated
   command chain cannot run at all.
5. **`dotnet new sln` defaults to `.slnx` on .NET 10.** *Revised 2026-08-24: this was first read
   as confirming the story's warning, and the solution was created with `-f sln`. The warning
   itself turned out not to hold (see Project Structure Notes), and the SDK default is now the
   format the repository uses.* Migrated with `dotnet sln Yello.sln migrate`, which writes the
   `.slnx` but **leaves the `.sln` behind** — so the gate now asserts that `Yello.slnx` exists
   and that no `.sln` sits beside it. One trap in writing that gate: on Windows a
   three-character extension in a search pattern also matches longer ones, so
   `EnumerateFiles("*.sln")` matches `Yello.slnx` and the assertion must filter on
   `Extension` rather than trust the pattern.
6. **The container runtime is Rancher Desktop, not Docker Desktop.** The `-rd` suffix in the
   story's preflight (`Docker 29.6.2-rd`) is the tell. There is no Docker Desktop on this
   machine. Its engine is `moby`/dockerd, which is what Testcontainers needs. It was running
   with its backend deliberately stopped; `rdctl shutdown` followed by a fresh launch brought
   it up in Linux container mode (`OSType=linux`, server 29.5.3).
7. **`Yello.Tests.Shared` cannot be `OutputType=Exe`.** Task 3 says to set that on every test
   project, but the `xunit.v3` metapackage hard-fails a non-`Exe` project and its own error
   points at the answer: *"If this is not a test project, reference
   xunit.v3.extensibility.core instead."* Shared holds fixtures and no test cases — the story
   itself calls it "infrastructure, not a suite" — so it references
   `xunit.v3.extensibility.core` and is not marked a test project. Making it `Exe` would hand
   `dotnet test` a seventh suite that can only ever report zero tests.
8. **`Testcontainers.XunitV3` 4.6.0 was built against `xunit.v3.extensibility.core` 2.0.2**, two
   major versions behind AR-1's xunit.v3 4.0.0. Pinned explicitly to 4.0.0 so the version the
   solution compiles against is a stated fact rather than a silent NuGet unification.
   `SqlServerContainerFixture` compiles clean against the pair, so the AR-1 combination holds
   at compile time. Runtime behaviour is exercised by the first story that consumes the fixture.

#### Scope decisions worth flagging

- **The Blazor template was stripped.** `dotnet new blazorwasm` ships Bootstrap, `css/app.css`,
  a scoped-CSS bundle, a styled error banner, and `Counter`/`Weather`/`NavMenu`/`MainLayout`
  components. All were removed. *Corrected 2026-08-27: the error banner's **element** was
  subsequently put back, unstyled, and is now a declared variance — see Project Structure Notes.
  Its styling is still story 1.2's. This bullet previously read as though the error surface had
  been removed with nothing in its place, which stopped being true in the patch pass.* Story 1.2 owns every design foundation and gates the token
  count at **exactly 30** "so an incomplete token set is detectable rather than merely wrong" —
  shipping a whole third-party design system here is precisely the failure that AC exists to
  catch — and Task 2 says to create no components. `App.razor` is a bare shell with no
  `<Router>` (there are no routable pages, and a Router cannot resolve an empty route table),
  and `index.html` carries no stylesheet link, no `:root` block and no hex value.
- **The Host's `MapGet("/", …)` was removed.** Task 2 creates no endpoints.
- **`Microsoft.Data.SqlClient` was added to `Yello.Host`** — a package not in the AR-1 table,
  needed because AC4 requires actually *opening* a connection and .NET ships no SQL driver.
  Deliberately **not** `Aspire.Microsoft.Data.SqlClient`: that client integration registers a
  database health check by default, and AR-33 requires probes to answer from process state with
  no database round trip. Raised for Lee below.
- **Logging is source-generated** (`[LoggerMessage]` in `Yello.Host/StartupLog.cs`). CA1848 and
  CA1873 are errors here, and this is the first logging in the solution, so the shape it takes
  is the shape later stories copy.
- **`GenerateDocumentationFile=true` with `CS1591` suppressed.** IDE0005 (unused using) only
  runs at build when a doc file is generated (dotnet/roslyn#41640), and IDE0005 matters here
  because a stray `using Microsoft.EntityFrameworkCore;` in a Domain file is exactly what Gate
  B hunts. Generating the file would otherwise turn CS1591 into a documentation mandate nothing
  in the corpus asks for, so the gate is kept and the mandate is not invented.
- **`CA1707` is `none` solution-wide and `IDE1006` reports at `suggestion`** — so test names can
  be sentences, per the test design's "one behaviour per test, named for the behaviour". Both
  rules police a public API surface; a test method is not one. *Corrected 2026-08-26: this note
  previously read "switched off under `tests/**`", which described an arrangement that no longer
  exists. Commit `fd556ff` moved both settings into `Opinionated.DotNet.CodingStandards`, where
  the relaxation is solution-wide and therefore also covers production code. That is wider than
  the note claimed, and wider than the note's own justification supports — worth revisiting if
  a production identifier ever trips it, since nothing now would.*
- **No coverage threshold anywhere**, per the scope boundary. The 80% figure in the test design
  is explicitly a proposal.
- **`xunit.runner.visualstudio` is referenced as Task 3 instructs**, but it is the VSTest
  adapter and there is no VSTest path on this SDK, so it is inert. Left in place rather than
  silently dropped — worth removing in a later story if Lee agrees.

#### What the 33 review patches changed (2026-08-26)

The dominant finding was one defect class, not thirty-three: **several gates asserted something
materially weaker than their names, comments and the ACs claimed.** The fixes group accordingly.

**Gates that could be defeated by one line a later story would plausibly write.**
`--ignore-exit-code 8` sat in four release-gating suites protected only by a comment saying to
remove it; it is now a gate (`TestingConventionTests`), and `Yello.Tests.Slices` has already
come off the switch. `ManagePackageVersionsCentrally` and `CentralPackageTransitivePinningEnabled`
were never read by anything, so every pin could be made inert while all six pin assertions stayed
green. The pin gate itself iterated only the expected table and never the file, so a package
could be added, removed or re-versioned unseen — it is exact in both directions now, which is the
same hardening Gate A's ring rule received during the first pass and which was not carried across.
`GlobalPackageReference` bypassed the provider ban, the VSTest ban and the ring's package ban at
once; the set of them is asserted exactly. `VersionOverride`, a child `<Version>` element, and a
package id differing only by letter case each escaped a read that looked only at the `Version`
attribute with an Ordinal comparison.

**Gates whose scope was narrower than the AC they implement.** Gate C scanned production
assemblies only, while AC3 says "anywhere in the solution" — so a banned role API in a fixture
passed all four assertions. It reads every assembly now, and fails loudly rather than quietly
narrowing when one cannot be found. Gate C also missed the *idiomatic* ways to write role
authorisation in ASP.NET Core: `AuthorizationPolicyBuilder.RequireRole`, an object-initialiser
`Roles` (which never appears in `CustomAttributes` at all, and is the form a Minimal API host
reaches for), `IdentityBuilder.AddRoles<>()`, any subclass of `AuthorizeAttribute`, and the
`IdentityUserRole`/`IRoleClaimStore` types the two regexes did not match. Role-based
authorisation could have been fully wired with all 26 assertions green. Gate B had no rule of any
kind for `Contracts`, `Merge`, `Client` or `AppHost` — four of eight assemblies, two of which are
compiled into the WebAssembly client — and its EF Core ban matched a namespace rather than the
way EF Core actually enters an inner ring (`services.AddDbContext<>()` lives under
`Microsoft.Extensions.DependencyInjection`).

**Gates blind to how a rule is really broken.** Solution membership was a substring match on raw
text, so an XML comment could drop a release-gating suite while the gate written to catch that
stayed green — demonstrated during review, not argued. It parses the `.slnx` now and compares
resolved paths both ways. Gate A read `.csproj` files only, leaving three MSBuild-import
bypasses: a `ProjectReference` in `Directory.Build.props` gives *every* project that edge, a
nested `tests/Directory.Build.props` replaces the root file entirely, and a `Compile Include`
moves source across a ring with no reference for either gate to see. A semicolon-separated
`Include` collapsed to its last entry, which let a forbidden edge present as a permitted one. The
runtime pin was a raw substring match that passed inside a comment or a dead `Condition`, and
`TargetFramework` was asserted nowhere at all.

**Two hand-maintained lists nothing reconciled.** Adding a ninth production project forced edits
to `ProductionProjects` and the ring table and forced nothing for `ProductionAssemblies.All` —
after which Gate B's ring rules and all four of Gate C's bans would silently never examine the new
assembly, in the story that exists to prevent exactly that.

**The one presence claim, unproven.** The shared fixture had never been executed: no suite
constructed it, so `InitializeAsync` had never run and the AR-1 pairing of Testcontainers 4.6.0
with xunit.v3 4.0.0 was verified at compile time only. It now has a consumer (see below), and
running it immediately exposed the finding underneath: `.WithWaitStrategy(...)` **replaces**
`MsSqlBuilder`'s `sqlcmd` engine-readiness probe rather than appending to it, so the fixture had
been waiting on "port 1433 is bound" instead. Measured on this image, `sqlcmd -Q "SELECT 1;"`
first succeeded at **20.99s** while the port was available at roughly **5s** — a ~16-second window
in which every consuming suite from story 1.3 onward would have raced the engine and failed
intermittently with login errors. The override is gone. The fixture also gained a startup
deadline (it hung rather than failed), container logs on failure, a state guard on
`ConnectionString`, and `IsContainerRuntimeAvailable()` so a suite can skip with a reason instead
of erroring when no runtime is up.

**Two values that were stated twice.** The SQL Server image and the Aspire database resource name
each lived as independent literals in two projects. Both now have one home — MSBuild properties in
`Directory.Build.props`, stamped into every assembly as metadata and read by each consumer from
its own copy — and a gate asserts no source file states either literally. The reader is written
three times because no two of the three consumers can share a type: the AppHost's project
references are Aspire project *resources* (`ReferenceOutputAssembly=false`, so it cannot compile
against `Yello.Host` even though the ring table permits the edge), and `Yello.Tests.Shared` has an
empty ring row on purpose. The duplication is in the mechanism, not in the fact.

**AC4's check, which had no automated coverage at all.** `catch (SqlException)` did not cover
`new SqlConnection(...)` or `connection.ServerVersion`; verified against Microsoft.Data.SqlClient
7.0.2, an unrecognised connection-string keyword throws `ArgumentException` from the constructor
and killed the process at startup — the exact outcome the surrounding comment says was designed
out. It now catches that family, distinguishes a timeout, and has a connect deadline and a
cancellation token so Ctrl+C interrupts it and a reachable-but-unready endpoint does not delay
Kestrel for the driver's full retry budget. Pooling is off for the startup connection, so it
cannot return a connection carrying this session's context to the pool — the shape story 1.9
exists to catch. An `else` branch says "skipped, not Development" out loud, because "passed",
"failed" and "never ran" were previously indistinguishable.

**Records that had stopped describing the tree.** `.editorconfig` was listed as delivered and does
not exist — commit `fd556ff` deleted it, and `.gitattributes` and `TESTING-CONVENTIONS.md` both
say so. The `CA1707` note described an arrangement that no longer exists. Both corrected above and
in the File List; `fd556ff` now has a Change Log entry.

#### Where the fixture smoke test lives, and why — worth Lee's attention

`Yello.Tests.Slices` gained two tests. It is the fifth test project and the only one that is
**not** release-gating, so a container start costs no release-gate latency, and it is not among
the four suites AC5's zero-tests clause names. The alternative homes were all worse: the
architecture suite runs first in CI *because* it takes seconds; `Yello.Tests.Isolation` is
release-gating and belongs to story 1.9; and making `Yello.Tests.Shared` a test project reverses a
deliberate decision recorded in this story.

The test **skips** when no container runtime is reachable rather than failing, because a stopped
Rancher Desktop backend is a local condition and a suite that fails for it is one a developer
learns to ignore. It was run for real on this machine and passed: the container started, the
`sqlcmd` probe reported ready, and a host connection returned `ServerVersion` `17.00.4075` — SQL
Server 2025, which is what the pinned tag claims.

#### An environmental finding, outside this story but affecting every later one

Getting that run to pass surfaced something worth recording: **Rancher Desktop's host port
forwarding had failed for ephemeral ports.** A container published on a fixed port (`-p 14340:80`)
answered in 2.8ms; the same image on a Docker-assigned port (32778) never answered at all.
Testcontainers always uses ephemeral ports, so every suite from story 1.3 onward would have
failed on this machine with a pre-login handshake timeout that looks like a TLS fault and is not —
the same symptom also took out Testcontainers' own Ryuk reaper. Reproduced outside the solution
with plain `docker run` and `nginx`, so it is not a defect in the fixture. The remedy is the one
already known for this box: `rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch. Worth
knowing before story 1.10 builds CI, and worth knowing before anyone spends an afternoon
debugging SQL Server.

#### Answers to the story's own open questions

Implemented as the story decided, all seven unchanged. Q1 version drift: AR-1 pins implemented
as written — and finding 2 above adds a concrete security consequence to that conversation.
Q2: local tool, so the invocation is `dotnet aspire run`. Q3: tag left floating; still the only
unpinned input in the stack. Q4: `Yello.Tests.Shared` created, and it is the single declared
variance the inventory gate knows about. Q5: slice tests live in `tests/Yello.Tests.Slices`.
Q6: one-shot Development-only connectivity log, not a health check and not a timer. Q7: no
coverage gate.

### File List

Paths relative to the repository root. Baseline commit `c83450c`, which contained **zero
source files** — every path below is new except the two marked *modified*.

*Corrected 2026-08-26.* `.editorconfig` was listed here and does not exist: commit `fd556ff`
deleted it (115 lines) when `Opinionated.DotNet.CodingStandards` took over the C# conventions,
and both `.gitattributes` and `tests/TESTING-CONVENTIONS.md` say so. Three places in one commit
disagreed and the wrong one was this list — the artefact a reviewer or a later story reads to
learn what exists. `.gitattributes` itself was missing, and the files added by the review-patch
pass are now included.

**Repository build foundation (Task 1)**

- `global.json` — SDK pin `10.0.303` + the `dotnet test` MTP runner opt-in
- `Directory.Build.props` — `TargetFramework`, `RuntimeFrameworkVersion` 10.0.11, and the two
  shared values (`YelloSqlServerImage`, `YelloDatabaseResourceName`) emitted as assembly metadata
- `Directory.Packages.props` — every version pin; the single place AC1's pins are expressed
- `.gitattributes` — line-ending normalisation; added by `fd556ff` and previously unlisted here
- `.config/dotnet-tools.json` — `Aspire.Cli` 13.4.6 as a local tool (a gate asserts the version)
- `aspire.config.json` — written by the Aspire CLI on first run; records the AppHost path so
  `dotnet aspire run` resolves it from the repository root with no `--project` argument. Its
  contents are repo-relative, not machine-specific, so it is kept rather than ignored.
- `Yello.slnx` — XML format, and the only solution file (a gate asserts no `.sln` beside it)
- `.gitignore` — *modified*: added .NET entries (it carried Python entries only)

**The eight production projects (Tasks 2, 4, 8)**

- `Yello.Domain/Yello.Domain.csproj`, `Yello.Domain/AssemblyMarker.cs`
- `Yello.Application/Yello.Application.csproj`, `Yello.Application/AssemblyMarker.cs`
- `Yello.Infrastructure/Yello.Infrastructure.csproj`, `Yello.Infrastructure/AssemblyMarker.cs`
- `Yello.Contracts/Yello.Contracts.csproj`, `Yello.Contracts/AssemblyMarker.cs`
- `Yello.Merge/Yello.Merge.csproj`, `Yello.Merge/AssemblyMarker.cs`
- `Yello.Host/Yello.Host.csproj`, `Yello.Host/AssemblyMarker.cs`, `Yello.Host/Program.cs`,
  `Yello.Host/StartupConnectivityCheck.cs` — AC4's check, moved out of `Program.cs` in the
  second review pass so a test can reach it,
  `Yello.Host/StartupLog.cs`, `Yello.Host/BuildConstants.cs`, `Yello.Host/appsettings.json`,
  `Yello.Host/appsettings.Development.json`, `Yello.Host/Properties/launchSettings.json`
- `Yello.Client/Yello.Client.csproj`, `Yello.Client/AssemblyMarker.cs`,
  `Yello.Client/Program.cs`, `Yello.Client/App.razor`, `Yello.Client/_Imports.razor`,
  `Yello.Client/wwwroot/index.html`, `Yello.Client/wwwroot/favicon.png`,
  `Yello.Client/Properties/launchSettings.json`
- `Yello.AppHost/Yello.AppHost.csproj`, `Yello.AppHost/Program.cs`

**The five suites plus the shared fixture home (Task 3)**

- `tests/Yello.Tests.Isolation/Yello.Tests.Isolation.csproj`
- `tests/Yello.Tests.Revocation/Yello.Tests.Revocation.csproj`
- `tests/Yello.Tests.Merge/Yello.Tests.Merge.csproj`
- `tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj`
- `tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj`
- `tests/Yello.Tests.Shared/Yello.Tests.Shared.csproj`
- `tests/Yello.Tests.Shared/SqlServerContainerFixture.cs`
- `tests/Yello.Tests.Slices/SharedFixtureSmokeTest.cs` — the fixture's first consumer, and the
  proof that it runs. Its arrival is why `Yello.Tests.Slices` no longer carries
  `--ignore-exit-code 8`.
- `tests/Yello.Tests.Slices/StartupConnectivityCheckTests.cs` — AC4's automated coverage, added
  in the second review pass: the check opens a real connection in Development, does not run
  outside it, and reports a missing connection string rather than throwing.

**The four gates (Tasks 5, 6, 7)** — all in `tests/Yello.Tests.Architecture/`

- `AllowedReferenceEdges.cs` — the dependency rule as data
- `RepositoryLayout.cs` — locates and parses the repository's build files
- `ProjectFileGateTests.cs` — Gate A: declared ring edges, `RuntimeFrameworkVersion`, solution-file format and singularity
- `SolutionInventoryTests.cs` — Gate A: project inventory and source-tree shape
- `PackageVersionPinTests.cs` — Gate A: AR-1 pins (exact, both directions), SDK band and MTP
  opt-in, the row-level-security provider ban, per-ring package bans, the solution-wide package
  set, both ungoverned copies of the Aspire version, no VSTest SDK
- `SolutionAssemblies.cs` — locates every assembly in the solution, tests included, so Gate C
  covers what AC3 actually says
- `TestingConventionTests.cs` — Gate A / AR-35: the zero-test-exit-code policy, the shared
  values, and the `Assumption` trait format
- `ProductionAssemblies.cs` — loads the eight production assemblies for the bytecode gates
- `RingDependencyTests.cs` — Gate B: A-1 (4) and A-2 (2)
- `RoleApiScan.cs` — Mono.Cecil IL scan behind Gate C
- `RoleApiBanTests.cs` — Gate C: A-3 (4)

**Conventions (Task 10)**

- `tests/TESTING-CONVENTIONS.md` — trait vocabulary, the `Task.Delay` prohibition, container
  topology, the exit-code-8 trap, and the commands

**Story tracking and planning artifacts**

- `_bmad-output/implementation-artifacts/1-1-the-solution-skeleton-and-its-build-gates.md` — *modified*
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — *modified*
- `_bmad-output/implementation-artifacts/deferred-work.md` — the two first-pass deferrals.
  *Previously unlisted; added 2026-08-27.*
- `_bmad-output/planning-artifacts/epics.md` — *modified*. AC5 amended against a `status: final`
  artifact, recorded in that file's own `amendments` frontmatter (2026-08-26, refined 2026-08-27).
  *Previously unlisted — and it is the most consequential documentation change in the commit,
  because it edits an acceptance criterion this story is judged against.*

*Note on this list, 2026-08-27.* The first review pass raised "records had stopped describing the
tree" against this File List; the patch pass corrected `.editorconfig` and `.gitattributes` and
then omitted two of its own 25 changed files, both above. A `git diff --stat` against the
baseline is the check worth running before calling this list complete — it is what caught both.

### Change Log

| Date | Change |
|---|---|
| 2026-08-23 | Story 1.1 implemented. Created the fourteen-project solution in its five rings with the AR-1 versions pinned centrally; wired the ring rule into the project references; added the four gates (26 assertions, 10 of them the A-series A-1/A-2/A-3) in `Yello.Tests.Architecture`; stood up Aspire local orchestration with a SQL Server 2025 container and a one-shot Development-only connectivity check in `Yello.Host`; established the test conventions. All four gates validated against planted violations and reverted. `dotnet build` and `dotnet test` both clean with warnings as errors. |
| 2026-08-23 | Hardened Gate A from a subset check to exact edge equality after it reported green over a solution with no project references at all. |
| 2026-08-23 | Pinned `SSH.NET` forward to 2026.0.0 (GHSA-q939-rpr3-3284, high severity) reached transitively through AR-1's `Testcontainers` 4.6.0, and `Microsoft.Data.SqlClient` to 7.0.2 to reconcile the conflicting floors of two AR-1 pins. The AR-1 table itself is unchanged. |
| 2026-08-24 | Switched the solution to `Yello.slnx` (via `dotnet sln migrate`, `.sln` deleted) and inverted the format gate to assert the `.slnx` is the only solution file. The story's original "use `.sln`" instruction rested on `bmad-testarch-framework`'s preflight having no `.slnx` branch; re-reading the skill, its backend indicator is the alternation `*.csproj`/`*.sln` and its prerequisite check names only `*.csproj`, so the fourteen project files satisfy detection either way. Gate validated against a planted stray `.sln` and reverted; `dotnet build` and `dotnet test` clean, 26 assertions, unchanged. |
| 2026-08-26 | Code review of `c83450c..HEAD` (three adversarial layers). Build and tests re-verified clean at HEAD: 26 passed, exit 0. AC2, AC3 (for its four named shapes), AC4 and AC1's inventory confirmed met; risk R7 handled as the handoff requires; no later-story P0 pre-empted. 4 decisions resolved by Lee (audit policy stays strict; the coding-standard enforcement exposure is accepted un-gated; the image tag gets one source plus a gate; AC5's contradictory wording is to be amended in `epics.md`), 33 patch findings recorded as action items, 2 deferred to `deferred-work.md`, 6 dismissed. Status moved `review` → `in-progress`: the dominant finding is that several gates assert materially less than their names and the ACs claim — eight of them high severity — so the suite the remaining 52 stories are gated by needs the fixes and a repeat of Task 9's planted-violation validation before this story closes. No code changed in this pass. |
| 2026-08-26 | Adopted `Opinionated.DotNet.CodingStandards` 0.0.11 as a `GlobalPackageReference`, the one coding standard (commit `fd556ff`). It replaced the hand-maintained `.editorconfig`, which was **deleted**, and the coding-standard properties in `Directory.Build.props`, which were removed rather than restated — that file is imported before the package's props, so restating a value would silently fork the standard instead of reinforcing it. Net effect on strictness: `AnalysisLevel` moved from Task 1's specified `latest-recommended` to `latest-all`, and `CA1707` moved from a `tests/**`-scoped relaxation to `none` solution-wide. Added `.gitattributes` (50 lines) for line-ending normalisation. *Recorded 2026-08-26: this commit was absent from the Change Log and the File List entirely, and the Completion Notes still described the arrangement it replaced.* |
| 2026-08-26 | Amended AC5 in `epics.md`, which carries `status: final` — a deliberate amendment recorded in that file's own `amendments` frontmatter, not a drafting fix. AC5 required the four gating suites *including architecture* to report zero tests, while AC2 and AC3 require that same suite to fail the build on a violation; the two cannot both hold. Reworded so the operative test is "builds and executes", with the zero-tests clause scoped to the suites that actually hold no cases. |
| 2026-08-26 | Applied all 33 review patches. The architecture suite went from 26 assertions to 44 and `Yello.Tests.Slices` gained 2, so `dotnet test Yello.slnx` is now 46 passed, exit 0, with `dotnet build` clean at 0 warnings. The dominant fix is one defect class rather than thirty-three: several gates asserted materially less than their names and the ACs claimed. Gate C now covers the whole solution (AC3's words) and the idiomatic ASP.NET Core role APIs it previously missed; Gate B gained rules for the four assemblies it had none for; Gate A's pin, inventory and solution-membership checks are exact in both directions and read parsed XML rather than raw text; three MSBuild-import bypasses and the `--ignore-exit-code 8` hazard are gated rather than commented. The SQL Server image and the Aspire resource name each have one home in `Directory.Build.props` instead of two literals apiece. AC4's connectivity check no longer kills the process on a non-`SqlException`, and has a deadline, a cancellation token and pooling off. The shared fixture ran for the first time — which immediately exposed that its wait strategy had **replaced** SQL Server's engine-readiness probe with a bare port check, a ~16-second window measured on this image in which every suite from story 1.3 onward would have raced the engine. Twenty-one violations were planted and reverted to validate the new gates; one of them found a real defect in the gate it was testing. Two findings stay deferred to story 1.9 in `deferred-work.md`, and the fixture's doc comment no longer states the container topology as settled fact. |
| 2026-08-26 | Status moved `in-progress` → `review` once the 33 patches were applied and the suite was green, returning the story to review for a second adversarial pass. *Recorded 2026-08-27: this transition had no Change Log entry, so the log showed the story leaving `review` and never re-entering it.* |
| 2026-08-27 | **Second code review of `fd556ff..HEAD`** — the pass that closed the first 33 findings, reviewed on its own terms by three parallel adversarial layers. Baseline independently re-verified before triage, and every numeric claim in this record checked out (44 assertions, all six trait counts, the 26→44 delta); the shared fixture was confirmed to genuinely run by reading the Docker event log rather than the record, which also confirmed `sqlcmd` is back in the wait chain. 3 decisions resolved by Lee, 30 patches, 5 dismissed, 0 deferred. **The dominant finding recurses the first pass's:** several of the gates written to fix "asserts less than it claims" had the same defect, and in two cases the new file's own docstring stated the property its code failed to hold — `SolutionAssemblies.cs` was added to stop Gate C narrowing its scope in silence and `continue`d, so under one output layout the suite reported 44/44 green having scanned **zero** test assemblies. A second, mechanical finding: the previous pass's plants validated the *example each finding cited* rather than the *sibling route the fix left open* — `GlobalPackageReference` in `Directory.Build.props`, `[Theory, InlineData]`, and a `Condition`-guarded framework pin were all still reachable. Also closed: Gate C's two remaining first-party role routes (the role claim, and `UserManager<>`'s role surface), MSBuild property indirection defeating every literal-matching ban, a version-less or `Condition`-guarded `PackageVersion` vanishing from the pin set, AC4's named image being asserted by nothing after centralisation, and a `TypeInitializationException` hazard the test suite documents at length and the Host had reintroduced. AC4 gained real automated coverage in `Yello.Tests.Slices` — including the inverted-guard case a source-level gate cannot express — which supersedes the manual `aspire run` transcript that had gone stale behind two rewrites. 24 violations planted and reverted, all 24 caught by a named assertion, plus one negative check; two assertions are recorded as unplantable with reasons rather than claimed. `dotnet build` clean at 0 warnings, `dotnet test Yello.slnx` 52 passed / 0 skipped / exit 0, architecture suite 44 → 47. Status moved `review` → **`done`**: all 3 decisions resolved by Lee, all 32 patches applied, nothing deferred, no unresolved finding at any severity. |

