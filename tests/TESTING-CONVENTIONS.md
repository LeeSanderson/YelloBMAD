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
| `Yello.Tests.Client` | the rendered client in a real browser — AD-29's two client-only invariants, NFR-9, and that the application starts at all | yes |

Four gates across the Structural Seed's five projects. The readiness report says "the four test
suites" in one section and "all five test projects" in another; both are correct, and this table
is the reconciliation.

`Yello.Tests.Client` is the **sixth project and the fifth gate**, added by the framework run on
2026-09-07 rather than by the Structural Seed. See "The browser level" below for why it exists
and what conversation preceded it.

`Yello.Tests.Shared` is a sixth project but **not a suite**: it holds the shared
Testcontainers SQL Server fixture and no test cases. It is the one declared variance from the
spine's Structural Seed.

**Later stories add cases to these existing suites rather than creating suites** (AC5). If a
new suite seems necessary, that is a conversation about the architecture, not a project
template.

**Where a `Yello.Client` unit test goes, and why it is not obvious.** The ring table gives
`Yello.Tests.Slices` no edge to `Yello.Client`, and `Yello.Tests.Client` sees only
`Yello.Contracts` — deliberately, because a compile-time edge to the thing under test would let a
case assert against a type instead of against what the browser actually received, which is the
defect that suite exists to catch. So the only suite that can see the client's types is
`Yello.Tests.Architecture`, which references all eight production projects because it has to load
their assemblies. Story 1.4 put `SessionSeamTests` and `ClientStorageBanTests` there for that
reason rather than widening a ring row for a unit test. Widening one is available and is an
architecture edit; it should be taken deliberately, by a story that needs it, not as a side effect.

## Trait vocabulary

CI tiering selects on these filters, so they exist from story 1.1 rather than being
retrofitted. Four names, and only these four:

| Trait | Values | Meaning |
|---|---|---|
| `Suite` | `Architecture`, `Isolation`, `Revocation`, `Merge`, `Slices`, `Client` | which suite the case belongs to |
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

## The browser level — blocker B5

`Yello.Tests.Client`, added by the framework run on 2026-09-07. It is the fifth of the five test
levels the test design names and the only one it left unbound: UNIT, INT, ARCH and LOAD all had a
binding, and E2E read "browser, binding per B5".

**The binding is Playwright for .NET**, not a TypeScript `@playwright/test` project. The reasons
are about this repository rather than about Playwright: one `dotnet test` invocation, the same
four-name trait vocabulary CI tiers on, and a version that `Directory.Packages.props` states and
`PackageVersionPinTests` asserts. An npm lockfile would be a second toolchain that none of those
gates can read.

**Adding a suite required a conversation, and this was it.** The rule below — "if a new suite
seems necessary, that is a conversation about the architecture" — was followed rather than
sidestepped. `test-design-architecture.md:315` asks for "the browser-test binding decision, and a
sixth test project"; the alternative of folding the cases into `Yello.Tests.Slices` was considered
and rejected because this suite's entry criterion is a live dev-server process and a real browser,
which nothing else here needs. Decided with Lee on 2026-09-07; the record is in
`_bmad-output/test-artifacts/framework-setup-progress.md`.

### What it needs to run

```
pwsh tests/Yello.Tests.Client/bin/Debug/net10.0/playwright.ps1 install chromium
```

Once per machine, and once per cold CI agent — add `--with-deps` on a CI Linux agent to install
the shared libraries headless Chromium links against. **The browser is pinned by the
`Microsoft.Playwright` version**, so it is as fixed as every other dependency here; that is why
the bundled Chromium was chosen over `Channel = "msedge"`, which would run whatever Edge the
machine happened to have.

**No container.** `Yello.Client` is a standalone WebAssembly application with its own dev server,
so this suite starts one process, needs no SQL Server, and belongs in the **PR tier** despite
being a browser suite. Cases that need a signed-in Session drive the API **faked at the network
edge** - see "Signed-in cases: the API faked at the network edge" below - so they still start no
Host and no engine.

The suite serves the real build output and launches it with `--no-build`, so
`dotnet build Yello.slnx` has to have run first. It says so by name rather than timing out.

### What it asserts today

Eight cases: two claims across the four locales in `BrowserLocales`. That the application starts
and renders the routed page with no error banner, and that `<html lang>` is the culture the client
resolved — computed from `CultureSelection` against `SupportedCultures`, never a literal, so a
second translation moves the expectation on its own.

**Per locale, not once.** `SupportedCultures.All` has one entry, so iterating it would run each
case once under `en` — which is exactly the run that misses culture-conditioned startup defects.
The four locales are browser inputs chosen for the paths they take, not Yello's resource set.

### Signed-in cases: the API faked at the network edge

Story 1.7 (open question 5, option a). `FakeYelloApi` answers every `/api/v1/**` request the page
makes with `page.RouteAsync`, on any origin, with the CORS headers a credentialed cross-origin
request needs - so the WebAssembly client signs in, lists its Spaces, switches and signs out in a
real Chromium with **no Host, no database and no container**. The rules:

- **Bodies are `Yello.Contracts` types, paths are contract routes.** A contract change breaks the
  fake as well as the Host, rather than leaving a fake that answers a shape nobody serves.
- **Every request is recorded**, so a case can assert a request was *never* made (AC10: no
  Space-scoped request for an id the switcher did not list).
- **Hold an answer with a `TaskCompletionSource`, never with `Task.Delay`** (`HoldAsync` /
  `Release`), to assert what is on screen while a response is outstanding.
- **What it does not prove**, stated in its remarks: the real Session cookie across origins, the
  real preflight, and anything the Host decides. Those are story 1.9's.
- **Never wait for a client-side navigation with Playwright's URL wait.** Its default waits for
  the `load` event, which a history-API navigation never fires, and even with `WaitUntil = Commit`
  it hung intermittently on navigations that had already happened (story 1.7 measured it: the
  address and the DOM both on the destination, the wait still pending at 30 s). Poll the page's own
  `location.pathname` (`SpaceSwitchingTests.WaitForPathAsync`), or wait on what the destination
  renders.

### Why it exists

Story 1.3 shipped the first rendered surface and closed with 136 tests passing. The client did not
start, in any browser, in any locale. All 136 read a *file*; nothing loaded the page. That gap is
not at the margin of what a suite claims — it is the difference between an application that works
and one that cannot start.

### The plants, and the one that found a real hole

Run 2026-09-08, per the planted-signal rule below.

| Plant | Caught |
|---|---|
| The 2026-09-01 defect re-armed — culture assigned after the runtime booted | yes — all 8 |
| Routed heading demoted from `h1` to `h2`, which also breaks `FocusOnNavigate` | yes — all 8 |
| An exception after the first render, unhiding the framework error banner | yes — the 4 boot cases, and not the other 4 |
| `DocumentLanguage` writing `data-lang` instead of `lang` — the feature disabled entirely | **no, at first** |

The fourth is the one worth reading. `index.html` hard-codes `lang="en"`, every locale resolves to
`en` while there is one neutral resource set, so `"en" == "en"` held with the feature switched
off — a vacuous assertion of precisely the kind this suite exists to catch, sitting inside this
suite. The fix is in the test rather than the product: the served document's pre-boot `lang` is
rewritten to `und` before the page loads, so the asserted value can only have come from the
client. The plant fails now, and the case asserts that its own rewrite actually matched — an
inert rewrite would put the hole straight back.

## The Host process is served over HTTPS — added by story 1.4

`HostProcess.RunAsync` starts `Yello.Host` on `https://127.0.0.1:0`, not `http`. That is not a
preference and it is not about realism for its own sake.

AC5 requires the Session cookie to carry `Secure`, so the Host sets
`CookieSecurePolicy.Always` — and **ASP.NET Core's antiforgery system refuses outright to issue a
token when that policy is set and the request is not an SSL request**:

```
The antiforgery system has the configuration value AntiforgeryOptions.Cookie.SecurePolicy = Always,
but the current request is not an SSL request.
```

Found on 2026-09-18 by running story 1.4's endpoint tests over http, where it arrived as a `500`
with an empty body and took eleven of fourteen cases down with it. **The framework was right and
the test was wrong.** Weakening the policy to `SameAsRequest` would have made the suite pass by
removing `Secure` from a production cookie — AC5 traded for convenience.

Two consequences for anyone writing a case here:

- **It needs the .NET development certificate** (`dotnet dev-certs https`). It does **not** need it
  trusted: the client sets a `ServerCertificateCustomValidationCallback` scoped to the loopback
  address it started the Host on, so a cold CI agent needs the certificate created and nothing else.
- **The framework's cookie container is off** (`UseCookies = false`), and every cookie is attached
  by hand — `WireSession` does it. Two reasons: a container attaches the Session cookie invisibly,
  so a case asserting a request was authenticated could pass without anyone having said which cookie
  carried it; and .NET's `CookieContainer` refuses to store a `Secure` cookie at all when the
  response arrived over `http`, which is how this was going to fail next.
- **A Token request attaches no cookie** (story 1.8). `WireSession.GetWithTokenAsync`,
  `PostWithTokenAsync` and `DeleteWithTokenAsync` send `Authorization: Bearer` and nothing else —
  no cookie and no anti-forgery header, whatever the session holds — so a case asserting a Token was
  served cannot pass because the Session cookie rode along. The bearer value is sent unvalidated, so
  a malformed one reaches the Host as written. `GetWithTokenAndCookiesAsync` attaches both and exists
  only to prove a request carrying both is authenticated by the Token alone; use it for nothing else.

**A second lesson from the same run, and it was a product defect rather than a test one.** ASP.NET
Core binds an anti-forgery token to the identity it was issued to, so a token minted while anonymous
is refused once a Session exists. Three sign-ins in one Host process produced one processed log line
instead of three, because the second and third never reached the endpoint. `SessionState.SignedIn`
now drops the token so the next request fetches a fresh one, and
`SessionSeamTests.Signing_in_drops_the_token_that_was_issued_to_an_anonymous_caller` is the
regression test. A case that signs in and then posts again must re-fetch the token.

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

**Story 1.4 reused it and kept the pattern**, in `PasswordVerificationDurationTests`: the real
assertion is that verifying against *no stored hash* costs what verifying against a wrong one
costs — AD-23 on the sign-in path — and the plant is a verifier that returns early when there is
no hash, which is `if (account is null) return Fail();` written one layer down. **That class takes
no database fixture, deliberately.** xunit builds a class fixture before any test in the class
runs, so a container-backed class fails entirely when the runtime is down, including cases that
never touch a database — and the whole of AC4's duration guarantee is decided by the hasher, which
needs no rows.

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

**One engine per test PROCESS, a database per test CLASS - settled by story 1.9.**
`SqlServerEngine` (`Yello.Tests.Shared`) is an xunit v3 **assembly fixture**: each suite that needs
SQL Server declares `[assembly: AssemblyFixture(typeof(SqlServerEngine))]` exactly once, and xunit
creates one instance per test assembly, which under Microsoft.Testing.Platform is one per process.
It starts the container on first use, not at assembly start, so the classes that need no database
(the inverted-guard cases, the probes, the gates) never wait for one. Each class then takes a
**database of its own** on it: `MigratedDatabaseFixture` (Slices) and the isolation and revocation
worlds call `CreateDatabaseAsync`, which creates `yello_{guid:N}` and hands back a connection string
whose `Initial Catalog` names it, and drop it at the end. Classes share the engine and nothing else;
`SharedEngineDatabaseIsolationTests` proves that rather than assuming it.

A full `dotnet test Yello.slnx` therefore starts **at most 5 SQL Server engines** - one each for
Slices, Isolation, Revocation and Client, plus at most one private engine kept where a test's
subject needs one - and `ContainerTopologyGateTests` holds that budget: no test class constructs
and starts `SqlServerContainerFixture` outside a named list, and each suite using the engine
declares exactly one assembly fixture. Every engine is capped at `MSSQL_MEMORY_LIMIT_MB` 2048.

**Measured, 2026-10-24.** Before: thirteen engines started concurrently (ten class fixtures, three
direct constructions), a 7.9 GB Docker peak, 0.63 GB of host memory left at the low point, and the
Rancher backend timing out mid-run ("timed out dialing Hyper-V socket"), which failed 62 Slices
tests. After: **four** engines (Slices, Isolation, Revocation, Client), a **3.4 GB** Docker peak,
and the full run green with the browser suite in the same run (504 total, 503 passed, 1 skipped).
The Slices suite alone starts one engine and peaks at about 1 GB. Host memory is not the cleaner
measure on this machine - Chromium, the editor and the WSL VM move it more than the tests do - so
the Docker peak is the number to compare.

`MigratedDatabaseFixture` is still a **wrapper** for the reason it always was: xunit builds a class
fixture *before* any test in the class runs, so an unconfigured runtime is recorded as
`UnavailableReason` and each test opens with `Assert.SkipUnless(fixture.IsAvailable,
fixture.UnavailableReason)`. A runtime that is **configured but dead** is different and **fails**:
the engine pings the daemon first (`SqlServerContainerFixture.ProbeContainerRuntimeAsync`) and
throws with the probe's reason. Skipping SM-1 silently on a dead runtime would be a vacuous gate.

**The pooled-connection cases need no engine of their own.** SqlClient keys pools by the exact
connection string, so `Max Pool Size=1` on a class's own database is its own pinned pool on the
shared engine. Those cases (`Slices/PooledConnectionIsolationTests` and
`Isolation/PooledConnectionCaseTests`) run in a collection with `DisableParallelization = true`.

**Story 1.4 deliberately kept two classes away from the database**, and the reason still holds:
`PasswordVerificationDurationTests` and `AuthenticateAccountHandlerTests` need no rows, so they
take no fixture and run on a machine with the container runtime down.

**Startup is bounded.** The fixture fails after five minutes rather than hanging
(`YELLO_CONTAINER_STARTUP_TIMEOUT_SECONDS` raises it for a cold image pull), captures the
container log on failure, and exposes `IsContainerRuntimeAvailable()` so a suite can skip with
a reason instead of erroring when no container runtime is configured. Its wait strategy is
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

`dotnet test` is the gate. As of story 1.9 it runs **504 tests: 503 passing, 1 skipped** (the
legacy-factor duration case): **187** architecture, **221** slice, **44** client, **35** isolation
and **17** revocation. Merge still reports zero tests and exit 0 - it keeps the
`--ignore-exit-code 8` switch, and `Yello.Tests.Architecture` asserts that only empty suites may.
Isolation and Revocation are strict (`ReleaseGatingSuiteTests`): no ignored exit code, so one
failing row fails the run with exit code 2. (Story 1.4 closed at 235; story 1.3 at 136.)

**A configured but dead container runtime FAILS fast; an unconfigured one SKIPS.** Story 1.9 made
the guard probe the daemon: `SqlServerEngine` pings it
(`SqlServerContainerFixture.ProbeContainerRuntimeAsync`, a 15-second deadline) before it starts an
engine, and fails every container-backed class with the probe's reason - including the Rancher
remedy - instead of letting each one time out against a dead socket. No runtime configured at all
is still a skip with a reason. The history below is why the split is where it is.

**Before story 1.9, 22 of the slice tests needed a container runtime, and with none reachable they
FAILED - they did not skip.** Measured on 2026-09-10 with Rancher Desktop stopped: `dotnet test` on the slice suite
reports **74 total, 52 succeeded, 22 failed, 0 skipped**, every failure a
`Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'`.

> **Corrected at code review on 2026-09-10, and this is the second time this claim has been
> wrong in the same direction.** The text said a stopped runtime "reports 136 total with 22
> skipped, and stays green", while disclosing that the number came from reading the skip guards
> rather than from a run. The story's own baseline had already contradicted it — story 1.3 records
> that on this machine with the Rancher backend stopped, the baseline run was "83 total / 80
> passed, the 3 failures being container-dependent Slices tests". The mechanism is
> `SqlServerContainerFixture.IsContainerRuntimeAvailable()`, which only checks that
> `MsSqlBuilder…Build()` does not throw — a configuration-level check that performs no I/O. A
> configured-but-dead backend therefore reports available, the skip guard passes, and the
> container start fails afterwards. **Making the guard probe the daemon is the fix** - taken by
> story 1.9, as described above. A stopped runtime is still a red suite, now with a precise
> message, and that is still the honest expectation to hold.

Note that the 22 are not evenly distributed: xunit constructs a class fixture before any test in
the class runs, so a container-backed *class* fails entirely — including any test in it that never
touches a database. That is why `DurationIndistinguishabilityTests` is a separate class from
`RegistrationDurationTests`.

## The isolation and revocation suites - added by story 1.9

**Every case is a row of data, and every row states what every surface gets.**
`Yello.Tests.Isolation/IsolationCases.cs` and `Yello.Tests.Revocation/RevocationCases.cs` are single
declared lists. Each row names its route (a `Yello.Contracts` pattern constant, never a literal), and
an expectation for **each** member of `IsolationSurface` (`Yello.Tests.Shared`): `Session` is the
Session cookie plus the anti-forgery header on a state-changing request, exactly what the browser
sends; `ApiToken` is a bearer and nothing else. There is no "not applicable": a Token on a
Session-only route is the no-credential `401`, byte for byte, and the row says so. The theory data is
generated as `cases x surfaces`, each row named "case id / surface". A case never branches on the
surface; it reads its expectation from the row.

**Three gates hold the list.** A row with no expectation for a surface fails as a gap
(`SurfaceGaps`, both suites). Any `/api` route the real Host maps with no row fails as a gap,
except a short named list (registration, sign-in, sign-out, the current Session, the anti-forgery
token, the unmatched fallback), each with its reason, and each still mapped - **so a new Space or
Account route needs an isolation row on both surfaces before the suite goes green**, whatever its
parameter is called. The route table is read from the Host's own endpoint data source, in process,
not from a hand list, and a row must send a request on the route it names. A row pending a later story names that story, and fails the suite once the
story leaves `backlog`.

**The harness is shared, and seeds through the product.** `HostProcess` and `WireSession` moved from
the slice suite into `Yello.Tests.Shared`, beside `RunningHost` (one Host process per test class, with
a log cursor that waits for the request-finished line of every request a case sent),
`ProductActor` (register, sign in, read the Personal Space, issue a Token - all over HTTP) and
`WireDatabase` (raw SQL only for what no endpoint does yet: the refusal record, a second owned Space,
Membership removal, Space and Account deletion, a Role change). Each class gets a world: a database
of its own on the process's one engine, migrated, and a Host on it.

**Every row ends with its own slice of the Host's log checked** for every password, Token plaintext,
Session cookie value and Space name the world has held (I-17, AC10), anchored on the Host's
request-finished line - the Hosts these suites start log those lines through configuration, which
the product's own level does not. Every refusal is compared as bytes against its anchor (an absent
Space on the same surface, or no credential), and every absence is anchored on the actor reading its
own Space first.

**Timing rows run alone and read the Host's own figure.** `DurationIndistinguishability` has one
method and two measurement points: the caller's round trip (`CompareAsync`) and a duration each arm
reports itself (`CompareMeasuredAsync`); the isolation timing rows feed it the elapsed time from the
Host's request-finished line. A permanent oracle (the boundary refusal against the unmatched-path
fallback, which records nothing) must read as distinguishable on both surfaces. The timing and
pooled-connection rows run in collections with parallelisation disabled.

**The real browser against the real Host** is `Yello.Tests.Client`'s `RealHostCollection`: the dev
server first, then a Host with `ClientOrigin` set to the client's origin, on a database on the
suite's one engine, with `appsettings.Development.json` answered by `page.RouteAsync` carrying the
Host's address - the only fake there. Chromium keeps the Host's `SameSite=None; Secure` cookie from
`https://127.0.0.1` on an `http://127.0.0.1` page, so the client is not moved to https. A case that
needs the running client's session-end path navigates in-app (`Blazor.navigateTo`), because a reload
boots a client that never held the Session.

## CI tiering, for context

Built by **story 1.10**, not here. Three stages — PR / Nightly / Weekly — with the architecture
suite running **first** in the PR stage, because it takes seconds and should fail before
anything slower starts. PR target under 15 minutes. Do **not** build smoke/P0/P1 tiers; the
test design's checklist explicitly resolved that contradiction in favour of the three-stage
shape.

## Not yet decided

**The browser binding (blocker B5) is decided** - Playwright for .NET, `Yello.Tests.Client`; see
"The browser level" above. This section used to say no browser project existed and B5 was open,
which contradicted that section from 2026-09-07 on; corrected by story 1.7. What remains open from
it is measurement rather than binding: AC13's text-spacing and 200%-zoom measurement, AC11's two
unmeasurable clauses and the `em`-relative-length parser gap, each owned in `deferred-work.md`.
