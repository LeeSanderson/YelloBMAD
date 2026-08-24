---
baseline_commit: c83450c63c8e68773349ec312eac286127f057cf
---

# Story 1.1: The solution skeleton and its build gates

Status: review

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
**Then** each builds and executes, reporting zero tests rather than failing to build
**And** later stories add cases to existing suites rather than creating suites

## Tasks / Subtasks

- [x] **Task 1 — Repository build foundation** (AC: 1)
  - [x] Create `global.json` pinning the SDK: `{ "sdk": { "version": "10.0.303", "rollForward": "latestPatch" } }`. Two .NET 10 SDKs are installed on this machine (10.0.302 and 10.0.303); without this pin the build is non-deterministic across machines.
  - [x] Create `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and one `<PackageVersion>` per pinned dependency (exact versions in Dev Notes → *Pinned versions*). This is the single place AC1's "every dependency is pinned" is expressed, and the single place the version-pin test reads.
  - [x] Under central package management, projects declare `<PackageReference Include="…" />` with **no** `Version` attribute. So pin all eight AR-1 versions centrally, but add an actual `PackageReference` only where this story needs one — `xunit.v3`, `xunit.runner.visualstudio`, `TngTech.ArchUnitNET`, `Testcontainers.XunitV3`, and the Aspire hosting packages. EF Core, Identity and `Asp.Versioning.Http` are **pinned but not yet referenced**; stories 1.3 and 1.5 add the references against the versions pinned here. This satisfies AC1 without dragging unused packages into a solution that has no schema.
  - [x] Create `Directory.Build.props` setting, for all projects: `TargetFramework=net10.0`, `RuntimeFrameworkVersion=10.0.11`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `ImplicitUsings=enable`, `EnforceCodeStyleInBuild=true`, `AnalysisLevel=latest-recommended`. `RuntimeFrameworkVersion` is what makes AC1's ".NET 10.0.11" a stated, testable fact rather than an implication of the SDK.
  - [x] Create `.config/dotnet-tools.json` and pin the Aspire CLI as a **local** tool: `dotnet tool install --local Aspire.Cli --version 13.4.6`. `Aspire.Cli` 13.4.6 is a `DotnetTool` package (`tools/net10.0/any/`). `aspire` is **not** on PATH on this machine; a local tool manifest both installs it and pins it to AR-1's Aspire 13.4 line, which the shell-script install does not.
  - [x] Create `.editorconfig` with the C# conventions the analysers enforce.
  - [x] Extend `.gitignore` — it currently carries Python entries only and no .NET entries. Add at minimum `bin/`, `obj/`, `TestResults/`, `.vs/`, `artifacts/`, `*.user`.
  - [x] Create the solution as **`Yello.sln`** (classic format), not `Yello.slnx` — see Dev Notes → *Project Structure Notes* for why the file extension is load-bearing here.

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

**Use `Yello.sln`, not `Yello.slnx`.** `bmad-testarch-framework`'s preflight detects a project by globbing for `package.json`, `*.csproj`, `*.sln`, `playwright.config.*`. It has no `.slnx` branch. Since this story exists partly to unblock that re-run, the classic solution format avoids re-halting it on a file extension.

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
| `dotnet build Yello.sln` | `0 Error(s) 0 Warning(s)` — with `TreatWarningsAsErrors=true` across all 14 projects |
| `dotnet test Yello.sln` | `Passed!  total: 26  failed: 0` — exit 0 |

**Assertion counts.** 26 in `Yello.Tests.Architecture`; the other four suites report zero tests
and still exit 0. Of the 26, **10 are the A-series** this story owes — A-1 (ring rule, 4),
A-2 (EF/ASP.NET leak, 2), A-3 (Role-API ban, 4). The other 16 are Gate A's project-file
assertions, which sit outside the A-1…A-15 numbering because that series was scoped to
bytecode and schema, and these read `.csproj`, `Directory.Packages.props` and `global.json`
as text. They live in separate test classes so the counts stay legible when later stories add
A-4 onward.

**Trait selection verified** (Task 10), since CI tiering depends on it:

| Command | Result |
|---|---|
| `Yello.Tests.Architecture.exe -list traits` | `Priority: [P0]`, `Requirement: [AR-1, AR-2, AR-4, NFR-1]`, `Suite: [Architecture]` |
| `-trait "Requirement=AR-4"` | 4 tests — Gate C exactly |
| `-trait "Requirement=AR-1"` | 11 tests |
| `-trait "Priority=P0"` | 26 tests |

The runner banner also prints `64-bit .NET 10.0.11`, which is independent confirmation that
`RuntimeFrameworkVersion` binds the output to AR-1's patch rather than merely to the SDK.

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

#### Task 9 — planted-violation results

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
5. **`dotnet new sln` defaults to `.slnx` on .NET 10**, confirming the story's warning was
   well-founded. Created with `-f sln`, and a gate now asserts both that `Yello.sln` exists and
   that no `.slnx` does — this can regress by accident rather than by decision.
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
  components. All were removed. Story 1.2 owns every design foundation and gates the token
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
- **`CA1707`/`IDE1006` switched off under `tests/**`** so test names can be sentences, per the
  test design's "one behaviour per test, named for the behaviour". Both rules police a public
  API surface; a test method is not one.
- **No coverage threshold anywhere**, per the scope boundary. The 80% figure in the test design
  is explicitly a proposal.
- **`xunit.runner.visualstudio` is referenced as Task 3 instructs**, but it is the VSTest
  adapter and there is no VSTest path on this SDK, so it is inert. Left in place rather than
  silently dropped — worth removing in a later story if Lee agrees.

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

**Repository build foundation (Task 1)**

- `global.json` — SDK pin `10.0.303` + the `dotnet test` MTP runner opt-in
- `Directory.Build.props` — shared properties, incl. `RuntimeFrameworkVersion` 10.0.11
- `Directory.Packages.props` — every version pin; the single place AC1's pins are expressed
- `.editorconfig`
- `.config/dotnet-tools.json` — `Aspire.Cli` 13.4.6 as a local tool
- `aspire.config.json` — written by the Aspire CLI on first run; records the AppHost path so
  `dotnet aspire run` resolves it from the repository root with no `--project` argument. Its
  contents are repo-relative, not machine-specific, so it is kept rather than ignored.
- `Yello.sln` — classic format, deliberately not `.slnx`
- `.gitignore` — *modified*: added .NET entries (it carried Python entries only)

**The eight production projects (Tasks 2, 4, 8)**

- `Yello.Domain/Yello.Domain.csproj`, `Yello.Domain/AssemblyMarker.cs`
- `Yello.Application/Yello.Application.csproj`, `Yello.Application/AssemblyMarker.cs`
- `Yello.Infrastructure/Yello.Infrastructure.csproj`, `Yello.Infrastructure/AssemblyMarker.cs`
- `Yello.Contracts/Yello.Contracts.csproj`, `Yello.Contracts/AssemblyMarker.cs`
- `Yello.Merge/Yello.Merge.csproj`, `Yello.Merge/AssemblyMarker.cs`
- `Yello.Host/Yello.Host.csproj`, `Yello.Host/AssemblyMarker.cs`, `Yello.Host/Program.cs`,
  `Yello.Host/StartupLog.cs`, `Yello.Host/appsettings.json`,
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

**The four gates (Tasks 5, 6, 7)** — all in `tests/Yello.Tests.Architecture/`

- `AllowedReferenceEdges.cs` — the dependency rule as data
- `RepositoryLayout.cs` — locates and parses the repository's build files
- `ProjectFileGateTests.cs` — Gate A: declared ring edges, `RuntimeFrameworkVersion`, `.sln` format
- `SolutionInventoryTests.cs` — Gate A: project inventory and source-tree shape
- `PackageVersionPinTests.cs` — Gate A: AR-1 pins, SDK band, in-memory ban, no VSTest SDK
- `ProductionAssemblies.cs` — loads the eight production assemblies for the bytecode gates
- `RingDependencyTests.cs` — Gate B: A-1 (4) and A-2 (2)
- `RoleApiScan.cs` — Mono.Cecil IL scan behind Gate C
- `RoleApiBanTests.cs` — Gate C: A-3 (4)

**Conventions (Task 10)**

- `tests/TESTING-CONVENTIONS.md` — trait vocabulary, the `Task.Delay` prohibition, container
  topology, the exit-code-8 trap, and the commands

**Story tracking**

- `_bmad-output/implementation-artifacts/1-1-the-solution-skeleton-and-its-build-gates.md` — *modified*
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — *modified*

### Change Log

| Date | Change |
|---|---|
| 2026-08-23 | Story 1.1 implemented. Created the fourteen-project solution in its five rings with the AR-1 versions pinned centrally; wired the ring rule into the project references; added the four gates (26 assertions, 10 of them the A-series A-1/A-2/A-3) in `Yello.Tests.Architecture`; stood up Aspire local orchestration with a SQL Server 2025 container and a one-shot Development-only connectivity check in `Yello.Host`; established the test conventions. All four gates validated against planted violations and reverted. `dotnet build` and `dotnet test` both clean with warnings as errors. |
| 2026-08-23 | Hardened Gate A from a subset check to exact edge equality after it reported green over a solution with no project references at all. |
| 2026-08-23 | Pinned `SSH.NET` forward to 2026.0.0 (GHSA-q939-rpr3-3284, high severity) reached transitively through AR-1's `Testcontainers` 4.6.0, and `Microsoft.Data.SqlClient` to 7.0.2 to reconcile the conflicting floors of two AR-1 pins. The AR-1 table itself is unchanged. |
