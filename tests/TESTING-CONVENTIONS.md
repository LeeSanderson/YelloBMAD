# Testing conventions

Established by story 1.1, which is the first code in the repository. There was no prior
convention to inherit, so these are the conventions — later stories follow them rather than
re-deciding them.

## The five suites, and which are gates

| Project | Covers | Release-gating |
|---|---|---|
| `Yello.Tests.Isolation` | SM-1 — every isolation case on both surfaces (HTTP and API token) | yes |
| `Yello.Tests.Revocation` | SM-2 — FR-34 | yes |
| `Yello.Tests.Merge` | AD-12 merge conformance | yes |
| `Yello.Tests.Architecture` | AD-21 — the dependency rule, the Role-API ban, the version pins | yes |
| `Yello.Tests.Slices` | use-case slice tests | **no** |

Four gates, five projects. The readiness report says "the four test suites" in one section and
"all five test projects" in another; both are correct, and this table is the reconciliation.

`Yello.Tests.Shared` is a sixth project but **not a suite**: it holds the shared
Testcontainers SQL Server fixture and no test cases. It is the one declared variance from the
spine's Structural Seed.

**Later stories add cases to these existing suites rather than creating suites** (AC5). If a
new suite seems necessary, that is a conversation about the architecture, not a project
template.

## Trait vocabulary

CI tiering selects on these filters, so they exist from story 1.1 rather than being
retrofitted. Four names, and only these four:

| Trait | Values | Meaning |
|---|---|---|
| `Suite` | `Architecture`, `Isolation`, `Revocation`, `Merge`, `Slices` | which suite the case belongs to |
| `Priority` | `P0`, `P1`, `P2` | selection tier. `P0` is release-gating |
| `Requirement` | e.g. `AR-1`, `AR-2`, `AR-4`, `NFR-1`, `FR-34` | the requirement the case traces to |
| `Assumption` | e.g. `A-3` from PRD §12 | the assumption the case hardens into an assertion |

`Assumption` is declared here but **unused so far**: no story-1.1 assertion hardens a PRD §12
assumption. Thirteen of those assumptions are open (readiness issue 5); the story that
confirms one tags its test with this trait.

Cite the `AR` id **and** the `AD` id where one exists. `AR-1 … AR-40` and `UX-DR1 … UX-DR42`
exist only in `epics.md`; the architecture spine numbers `AD-1 … AD-29` and the mapping is not
one-to-one (`AR-21` carries `AD-15`). Story 1.1's gates trace: AR-1 → Stack + Structural Seed,
AR-2 → AD-21, AR-4 → AD-1, AR-35 → Consistency Conventions.

### Selecting on a trait

Verified against the built suite:

```
Yello.Tests.Architecture.exe -list traits          # list every trait pair in the assembly
Yello.Tests.Architecture.exe -trait "Priority=P0"  # simple filter
Yello.Tests.Architecture.exe -filter "/*/*/*/*[Requirement=AR-4]"   # query filter
```

Simple, query and VSTest filtering **cannot be mixed** in one invocation.

## Rules

**One behaviour per test, named for the behaviour.** Test names are sentences with
underscores. The coding standard permits this without a local exemption:
`Opinionated.DotNet.CodingStandards` sets `CA1707` to `none` solution-wide, and its naming
rules report `IDE1006` at `suggestion`, so neither fails a build. Both rules police a public
API surface, and a test method is not one — it is never called by anything but the runner.
The repository no longer carries an `.editorconfig`; the standard is the
`GlobalPackageReference` in `Directory.Packages.props`.

**No `Task.Delay` as a synchronisation mechanism.** Ever. Wait on the condition, not on the
clock — a Testcontainers wait strategy, a completion signal, a polled predicate with a
deadline. The test design calls this "cheaper to enforce as a convention from story 1.1 than
to unpick later".

**Randomised data where identity does not matter; fixed where an assertion depends on it.**

**Never a shared email address.** FR-1 makes an Account's email unique, so a shared literal is
a cross-test collision waiting to happen — and it will surface as a flake in an unrelated
suite.

**Cleanup by transaction rollback or container disposal.** Never by delete statements: those
would themselves need an RLS session context to see the rows they are trying to remove, so a
cleanup that "works" may be evidence that isolation is broken.

**An absence assertion must be validated against a planted signal, or it is not a test.** Every
gate story 1.1 ships is an absence assertion against empty projects, so every one of them was
failed on purpose first. See the story's Dev Agent Record for the four results. Any later
story adding an absence assertion does the same.

**No coverage threshold.** None appears in the PRD, the architecture spine, the epics or the
spec kernel. The 80% figure in the test design is explicitly "offered as a proposal for Lee to
accept or drop", and is called "the weakest gate on this list". Do not invent one.

## The shared container

One Testcontainers SQL Server instance, amortised across collections, running
`mcr.microsoft.com/mssql/server:2025-latest`. Never an EF Core in-memory provider: it cannot
exercise row-level security, which is what NFR-1 rests on. The ban is enforced by a gate, not
a convention — `Microsoft.EntityFrameworkCore.InMemory` has no central version, so a project
referencing it fails to restore.

**One exception to the shared topology:** the pooled-connection isolation case in story 1.9
needs its own container, with pool size pinned to 1 and parallelism disabled. A pooled
connection carrying a stale session context is the thing that case exists to catch, and it
cannot be observed on a shared pool.

`Yello.Tests.Shared` is also the mechanism for asserting a **migrated schema**. Story 2.6 owns
risk R7 — AD-15's `Latin1_General_100_BIN2` collation is irreversible, because
`ALTER DATABASE … COLLATE` is unsupported on Azure SQL — "with the schema assertion seeded in
1.1". Story 1.1 seeds the fixture and writes no schema assertion, because there is no schema.

## Runner

`xunit.v3` 4.0.0 depends on `xunit.v3.mtp-v2 [4.0.0]`: **Microsoft.Testing.Platform is the
only runner.** Test projects are `OutputType=Exe`. `Microsoft.NET.Test.Sdk` is deliberately
absent and a gate asserts it stays absent — there is no VSTest path, and on the .NET 10 SDK
the VSTest target refuses outright.

`dotnet test` is opted into MTP via `global.json`:

```json
"test": { "runner": "Microsoft.Testing.Platform" }
```

Not via `dotnet.config`. Without this, `dotnet test` fails before running an assertion.

### The zero-tests trap

MTP exit codes: `0` success, `2` at least one test failed, **`8` the test session ran zero
tests**, `9` minimum execution policy violated. MTP is strict by default where VSTest tolerated
an empty run.

Note the failure mode precisely: the **build succeeds** and `dotnet test` returns 8. The four
empty suites therefore carry:

```xml
<TestingPlatformCommandLineArguments>$(TestingPlatformCommandLineArguments) --ignore-exit-code 8</TestingPlatformCommandLineArguments>
```

`--minimum-expected-tests` is the wrong lever; it governs exit code 9.

**Remove that property from a suite the moment it gains its first test.** Left in place, it
also swallows a genuinely empty run — so a filter typo or broken discovery would pass silently
as "zero tests", which is exactly the signal it is masking on purpose.
`Yello.Tests.Architecture` does not carry it and must stay strict.

One quirk worth knowing: running a suite's `.exe` **directly** uses xunit's own console runner,
which returns 0 for zero tests and does not understand `--ignore-exit-code`. The exit-code-8
behaviour belongs to MTP mode under `dotnet test`. Verify exit codes the way CI will.

## Commands

```
dotnet restore
dotnet tool restore     # installs the pinned Aspire CLI (Aspire.Cli 13.4.6, local tool)
dotnet build
dotnet test             # the gate
dotnet aspire run       # local orchestration, from Yello.AppHost
```

`dotnet test` is the gate: the architecture suite green with real assertions, the other four
reporting zero tests and still exiting 0.

## CI tiering, for context

Built by **story 1.10**, not here. Three stages — PR / Nightly / Weekly — with the architecture
suite running **first** in the PR stage, because it takes seconds and should fail before
anything slower starts. PR target under 15 minutes. Do **not** build smoke/P0/P1 tiers; the
test design's checklist explicitly resolved that contradiction in favour of the three-stage
shape.

## Not yet decided

No E2E / browser project exists. Blocker **B5** — Playwright for .NET versus a separate
TypeScript project — is open, and is decided at the `bmad-testarch-framework` run. That run
halted at preflight on 2026-08-22 because nothing was built; story 1.1 removes that blocker, so
it can be re-run now. Note `tea_use_playwright_utils` is off because those helpers are
TypeScript against a .NET stack, and that flip did *not* unblock TF — the empty repository did.
