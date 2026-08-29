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
| `Assumption` | e.g. `PRD-12-2` — source document, then location within it | the assumption the case hardens into an assertion |

`Assumption` was unused until story 1.3, which carries the first one: `PRD-12-1`, on
`RegisterAccountHandlerTests.The_Space_is_named_through_the_naming_port_from_the_display_name`.
That assumption — that an Account has a display name its Personal Space is named from — was
**confirmed rather than revised** by Lee on 2026-08-28, which is what readiness issue 5 asks for.
Twelve of the thirteen remain open; the story that confirms one tags its test with this trait.

**The value must name its source document.** The test design documents the selective run
`dotnet test --filter "Assumption~PRD-12"` to find "every test resting on an unconfirmed
assumption", and a trait valued `A-3` never matches it — the tests would be unfindable by the
one command written to find them. This convention previously gave `A-3` as its example, which
was the divergence. `TestingConventionTests` asserts the format now, so a copied-and-adapted
trait cannot drift back.

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

## Comparing two code paths' durations — blocker B3

`Yello.Tests.Shared/DurationIndistinguishability.cs`, added by story 1.3. Use it; do not
improvise a second one. `test-design-architecture.md:113` assigns B3 to "stories 1.3 and 1.6" and
it blocks P0 test I-7 — 1.3 needs it for AD-23 (a duplicate registration must be indistinguishable
from a new one *in duration*), 1.6 for AD-3 (a refusal must not reveal whether the thing refused
exists), and 1.9 reuses it on both surfaces.

The four things it fixes, so nobody re-decides them: **21 samples per arm**, the **median** (not
the mean — one GC pause moves a mean by more than the effect being measured), **20% of the slower
median** as the tolerance, and a **server-side** measurement point timing the operation and
nothing around it. Samples are **interleaved**, which matters more than it sounds: measured while
choosing story 1.3's password work factor, whichever arm ran first read about 2.5× too fast,
because the CPU is on turbo before sustained load pulls the clock down.

**What it cannot do, stated so nobody reaches for it wrongly.** It is not a side-channel analysis.
It is built for an enormous effect — a skipped password hash removes ~270 ms from a ~275 ms
operation — and a few microseconds of difference from an early string comparison passes it
comfortably.

**Its oracle is a permanent test, not a note in a story record.** `RegistrationDurationTests`
holds both the real assertion and a planted one that skips the hash and requires the method to
tell the two paths apart. Keeping the plant as a test is what stops a later story widening the
tolerance to quiet a flake and quietly disarming the assertion beside it.

## EF Core migrations need a hand-editing pass

`dotnet ef migrations add` scaffolds a migration that **fails this build**: `IDE0005`, `IDE0161`,
`MA0197`, `CA1861` and `S138` all fire on the generated file. Fix them by hand — a file-scoped
namespace, real documentation on the type instead of `<inheritdoc />`, composite index columns
lifted into static fields, and `Up` split so no method exceeds 80 lines. None of it changes an
executed statement.

Do **not** add `// <auto-generated />` to silence it. EF writes that marker into the
`.Designer.cs` and the model snapshot — which is why those two are exempt — and deliberately not
into the migration, because that is the file it expects a human to edit. Story 1.3's carries
hand-written row-level-security DDL, so claiming it is generated would be false. There is no
narrower lever either: this repository has no `.editorconfig` on purpose, so a per-folder
analyser exemption would mean forking the standard.

## The shared container

`Yello.Tests.Shared` holds one Testcontainers SQL Server fixture. Never an EF Core in-memory
provider: it cannot exercise row-level security, which is what NFR-1 rests on. Never SQLite
either, including `:memory:` — no `CREATE SECURITY POLICY`, no `SESSION_CONTEXT`, so it fails
the ban's *reason* while passing its letter. The ban is enforced by a gate rather than a
convention: neither has a central version, so a project referencing one fails to restore.

The image comes from `Directory.Build.props` (`YelloSqlServerImage`), stamped into every
assembly as metadata and read by both the fixture and `Yello.AppHost`. It used to be a literal
in each, which let the suites and local orchestration run different engine builds with nothing
in the repository changing. A gate asserts no source file states it literally.

**One container per test CLASS, via `IClassFixture`.** Story 1.3 added `MigratedDatabaseFixture`
(`Yello.Tests.Slices`), which wraps `SqlServerContainerFixture`, applies the migrations once, and
exposes both a `RegistrationDatabase` and the raw connection string. Use it rather than
constructing the fixture per test: each engine is roughly 2 GB and takes tens of seconds to become
ready, so a class of eight cases would otherwise mean eight sequential starts.

It is a **wrapper** rather than a direct `IClassFixture<SqlServerContainerFixture>` for one reason,
and any replacement must keep it: xunit builds a class fixture *before* any test in the class runs,
so letting `InitializeAsync` throw turns "Rancher Desktop is stopped" into a class of red tests
with a container stack trace. It records the reason instead, and each test opens with
`Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason)`.

**Topology is NOT settled, and the class fixture does not settle it.** The intent recorded in
story 1.1 was "one instance amortised across collections", and as built that cannot hold: the
fixture has no `[CollectionDefinition]`, no assembly-level registration and no container reuse,
and the suites run as separate Microsoft.Testing.Platform *processes* — which puts cross-suite
sharing out of reach without reuse or an external orchestrator. Nothing is shared across classes,
suites or processes. **Story 1.9** owns the decision, being the first story that has to make the
sharing model real. Until then, read "shared" as intent rather than as description.

**The cost is now measured rather than estimated.** A full `dotnet test Yello.slnx` starts four
engines sequentially: two for story 1.3's container-backed test classes, two for story 1.1's
direct constructions. Roughly 30 seconds to first assertion each, about 2 GB apiece.

**One exception that holds regardless:** the pooled-connection isolation case in story 1.9
needs its own container, with pool size pinned to 1 and parallelism disabled. A pooled
connection carrying a stale session context is the thing that case exists to catch, and it
cannot be observed on a shared pool.

**Startup is bounded.** The fixture fails after five minutes rather than hanging
(`YELLO_CONTAINER_STARTUP_TIMEOUT_SECONDS` raises it for a cold image pull), captures the
container log on failure, and exposes `IsContainerRuntimeAvailable()` so a suite can skip with
a reason instead of erroring when no container runtime is up. Its wait strategy is
`MsSqlBuilder`'s own `sqlcmd` probe, deliberately not overridden: `WithWaitStrategy` *replaces*
the strategy list, so substituting a port check trades engine readiness for "1433 is bound",
and SQL Server binds the port well before it accepts logins. Measured on this repository's
image, the gap between the two is roughly 16 seconds.

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

Note the failure mode precisely: the **build succeeds** and `dotnet test` returns 8. The suites that are still
genuinely empty therefore carry:

```xml
<TestingPlatformCommandLineArguments>$(TestingPlatformCommandLineArguments) --ignore-exit-code 8</TestingPlatformCommandLineArguments>
```

`--minimum-expected-tests` is the wrong lever; it governs exit code 9.

**Remove that property from a suite the moment it gains its first test.** Left in place, it
also swallows a genuinely empty run — so a filter typo or broken discovery would pass silently
as "zero tests", which is exactly the signal it is masking on purpose.
`Yello.Tests.Architecture` does not carry it and must stay strict.

That is a gate, not a request: `TestingConventionTests` asserts that a project carrying the
switch contains no test methods, and that a test project without it has some. It used to be a
comment in each project file — one which named this exact risk and then did not prevent it,
in a repository whose stated bar is that "a rule that relies on discipline is not a rule here".
`Yello.Tests.Slices` has already come off the switch, having gained the shared-fixture smoke
test.

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

`dotnet test` is the gate. As of story 1.3 it runs **136 tests, all passing**: 82 in the
architecture suite and 54 in the slice suite. Isolation, Revocation and Merge still report zero
tests and exit 0 — they keep the `--ignore-exit-code 8` switch, and `Yello.Tests.Architecture`
asserts that only empty suites may.

**22 of the slice tests need a container runtime and skip with a reason** when none is reachable,
rather than failing — 19 through the `MigratedDatabaseFixture` guard and 3 through a direct
`Assert.SkipUnless`. A machine with Rancher Desktop stopped therefore reports 136 total with 22
skipped, and stays green. That count is from the skip guards themselves rather than from an
observed run: it was not measured with the runtime down.

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

**B5 is now live rather than pending.** Story 1.3 shipped the product's first rendered surface —
the registration page — and NFR-9 names registration *first* among the five gated flows. Three
`deferred-work.md` entries are owned by "the first story with a rendered surface" and were waiting
on exactly this: AC13's text-spacing and 200%-zoom measurement, AC11's two unmeasurable clauses,
and the `em`-relative-length parser gap. All three are now actionable the moment the binding is
decided. What story 1.3 discharged is the *constructive* half — no fixed widths or heights, a
maximum in `rem`, internal padding in `rem` — and none of the measurement.
