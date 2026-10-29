# YelloBMAD (Yello)

A multi-tenant task board built story by story with BMad. Work is tracked in
`_bmad-output/implementation-artifacts/sprint-status.yaml`. Each story file lives in the same folder
and owns its acceptance criteria. `tests/TESTING-CONVENTIONS.md` is the authority on how to write tests.

## Stack

- .NET 10. The SDK is 10.0.303 (`global.json`, rolls forward to the latest patch), and the runtime is
  pinned to 10.0.11 in `Directory.Build.props`.
- Solution: `Yello.slnx`. Package versions are managed centrally in `Directory.Packages.props`, and
  `PackageVersionPinTests` checks them.
- Projects:
  - `Yello.Domain` and `Yello.Application` (vertical slices under `Application/<Area>/<Slice>/`).
  - `Yello.Infrastructure`: EF Core and SQL Server, with migrations in `Persistence/Migrations`.
  - `Yello.Contracts`: wire types shared with the client.
  - `Yello.Host`: the ASP.NET Core API, which also hosts the client.
  - `Yello.Client`: Blazor WebAssembly.
  - `Yello.AppHost`: Aspire.
  - `Yello.Merge`.
  - Project references follow the ring rule in `tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs`.
- Key versions:

  | Package | Version |
  |---|---|
  | EF Core, ASP.NET Core Identity, Localization, Blazor WebAssembly | 10.0.11 |
  | Microsoft.Data.SqlClient | 7.0.2 |
  | Asp.Versioning.Http | 10.0.0 |
  | Aspire.Hosting.* | 13.4.6 |
  | xunit.v3 | 4.0.0 |
  | Testcontainers.MsSql / XunitV3 | 4.6.0 |
  | TngTech.ArchUnitNET | 0.13.3 |
  | Microsoft.Playwright | 1.62.0 |
  | Opinionated.DotNet.CodingStandards (GlobalPackageReference) | 0.0.11 |

- Coding standards, including TreatWarningsAsErrors and Nullable, come from that last package. Do not
  restate them in `Directory.Build.props`.
- Local tools are listed in `.config/dotnet-tools.json`: aspire.cli 13.4.6 and dotnet-ef 10.0.11. Run
  `dotnet tool restore` first.
- Database: SQL Server 2025 (`mcr.microsoft.com/mssql/server:2025-latest`, set once in
  `Directory.Build.props`).
  - Isolation between Spaces uses row-level security plus EF query filters.
  - `SESSION_CONTEXT` is set for each Space.
  - Collation is `Latin1_General_100_BIN2`.
- Infra: Bicep in `infra/`. CI: `.github/workflows/ci.yml`. Azure prerequisites are in
  `docs/operations.md`.
- Never deploy, log in to Azure or push without being asked.

## Build, run, verify

```powershell
dotnet build Yello.slnx                                    # must be 0 warnings, 0 errors
dotnet build-server shutdown                               # free build-server memory before a full test run
dotnet test Yello.slnx --no-build --max-parallel-test-modules 1   # full suite, one test project at a time
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build   # one project
./run.ps1                                                  # the app via Aspire (dotnet aspire run)
```

- **`--no-build` runs the binaries already built.** Rebuild after every change, or the run silently
  tests stale code.
- **Client (Playwright) tests need `Yello.Client` built in the same configuration as the tests.** The
  browser dev server serves the client output of the configuration the test assembly was built in.
- **Leftover `Yello.Host.exe` processes** can lock `Yello.Host/bin/Debug` and fail the build. Check
  with `Get-Process Yello.Host`.
- **Migration bundle tests are skipped unless `YELLO_MIGRATION_BUNDLE` is set.** They need
  `artifacts/efbundle-win-x64/efbundle.exe`, which git ignores. Three skips on a full run are
  expected for this reason.

## Test memory budget (read before running tests)

The containers run in WSL, which is capped at **8GB with no swap** (`~/.wslconfig`). Do not raise the
cap. Fit the tests to it instead.

- **One SQL Server per test project.** Each container-backed test project (Slices, Isolation,
  Revocation, Client) starts its own SQL Server through `SqlServerContainerFixture`. Each server is
  capped at `MSSQL_MEMORY_LIMIT_MB=2048` (`tests/Yello.Tests.Shared/SqlServerContainerFixture.cs`).
  Within a project there is one server, and each test class gets its own database.
- **The default run doesn't fit.** A plain `dotnet test Yello.slnx` runs the projects in parallel:
  4 × 2GB = 8GB, the whole WSL allowance before dockerd and Kubernetes take their share. Runs
  have been killed for low memory, have failed engine start, and have produced timing failures
  that were not real.
- **Always pass `--max-parallel-test-modules 1` to a full run.** At most one SQL Server is then
  alive at a time.
- **Never run two `dotnet test` processes at once.** When iterating, run one project with
  `--filter`.
- **Do not add a container-backed test class casually.** Reuse the existing fixtures.
- **Timing tests need a `DisableParallelization` collection.** Any test that measures time
  (`BoundaryDurationCollection`, `IsolationTimingTests`, `TimingCollection`) belongs in one.
- **Race tests must be deterministic.** Control the overlap, for example by waiting on `LCK_M_%`.
  Never rely on timing.
- **Rerun once before calling a failure real.** This applies to a SQL engine that fails to start and
  to a process killed for low memory.

## Container runtime: Rancher Desktop

Rancher Desktop provides Docker on this machine. There is no Docker Desktop.

- Check readiness with `docker info --format '{{.ServerVersion}}'`. It must return a non-empty
  version. A bare `docker info` exits 0 even when the server is unreachable.
- The failure "timed out dialing Hyper-V socket" means the Win32 socket proxy is in a crash loop.
  Its log is `%LOCALAPPDATA%\rancher-desktop\logs\background.log`. To recover:
  1. Run `rdctl shutdown`.
  2. Run `wsl --shutdown`.
  3. Relaunch `Rancher Desktop.exe`.
  4. Poll the readiness check above.

  `rdctl` lives at `C:\Program Files\Rancher Desktop\resources\resources\win32\bin\`. `rdctl start`
  does nothing when the app is already running.
- When container-backed projects fail with SqlException 1225 or connection refused, check Docker
  before suspecting the code.

## Rules that have bitten before

- **Undoing a temporary change.** Never use `git checkout --`, `git restore` or `git reset` to undo
  a planted violation or other temporary change. Save the original content and restore it in a
  `finally`.
- **Commits.** Commit directly to `main`; this is a solo repository with no PR flow. Never use
  `--no-verify`.
- **New dependencies.** A new NuGet package or a new project needs Lee's approval first.
- **Audit suppressions.** The "One-off NuGet audit suppressions" ItemGroup in `Directory.Build.props`
  was approved by Lee. Leave it alone. Remove it only when SqlClient brings in Pkcs >= 9.0.21.
- **Isolation rows.** Isolation cases live in `tests/Yello.Tests.Isolation/IsolationCases.cs`. A row
  may stay pending only while its story is not yet at review (GATE-003).
- **Sign-out.** Sign-out invalidates the session on the server through `Account.SessionsValidAfter`.
  That was a deliberate decision.
