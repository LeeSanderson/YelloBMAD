---
stepsCompleted:
  [
    'step-01-preflight',
    'step-02-select-framework',
    'step-03-scaffold-framework',
    'step-04-docs-and-scripts',
    'step-05-validate-and-summary',
  ]
lastStep: 'step-05-validate-and-summary'
lastSaved: '2026-09-08'
---

# Test Framework Setup — Progress

Run started 2026-09-07 by TEA (`bmad-testarch-framework`, Create mode).

Inputs handed over explicitly, because these sit outside this skill's default search patterns:

- `_bmad-output/test-artifacts/test-design-architecture.md` — blockers, risk register, ASRs
- `_bmad-output/test-artifacts/test-design-qa.md` — scenario coverage matrix
- `_bmad-output/test-artifacts/test-design/YelloBMAD-handoff.md` — risk-to-story mapping
- `tests/TESTING-CONVENTIONS.md` — the suite table, trait vocabulary and selection commands
- `_bmad-output/implementation-artifacts/deferred-work.md` — the two 2026-09-05 entries this run owns

## Step 1 — Preflight

### 1. Stack detection

`config.test_stack_type: auto`, so the auto-detection algorithm applied.

| Indicator class | Found | Notes |
|---|---|---|
| Backend manifests | `Yello.slnx` + 8 `.csproj` (`AppHost`, `Application`, `Client`, `Contracts`, `Domain`, `Host`, `Infrastructure`, `Merge`) | .NET 10, `global.json` pinned, central package management via `Directory.Packages.props` |
| Frontend manifests | **none** — no `package.json`, no `playwright.config.*`, `vite.config.*` or `webpack.config.*` | the repo carries no Node toolchain at all |

**Algorithm result: `backend`. Overridden to `fullstack`.**

The detection algorithm keys frontend presence on JavaScript build manifests. `Yello.Client`
is a Blazor WebAssembly SPA — a browser-delivered frontend with no npm involvement — so the
algorithm cannot see it. Recording `fullstack` because the browser test level is the specific
gap this run exists to close (blocker B5, risk R16, testability concern T9). Left as a note
rather than a config edit: `test_stack_type` stays `auto` so the reason travels with this
document instead of becoming an unexplained literal.

`{detected_stack}` = **fullstack**

### 2. Prerequisites

**Browser half — clean, nothing to conflict with.**

- No `package.json` in the project root
- No `playwright.config.*`, `cypress.config.*` or `cypress.json` anywhere
- No browser-driven test project exists

**Backend half — already built, and deliberately not in scope.**

The literal preflight rule ("no existing test framework config that conflicts") fires here, and
it is a false positive. What exists is not a competing framework awaiting a decision — it is a
settled one:

| Project | Covers | Release-gating |
|---|---|---|
| `Yello.Tests.Isolation` | SM-1 — every isolation case on both surfaces | yes |
| `Yello.Tests.Revocation` | SM-2 — FR-34 | yes |
| `Yello.Tests.Merge` | AD-12 merge conformance | yes |
| `Yello.Tests.Architecture` | AD-21 — dependency rule, Role-API ban, version pins | yes |
| `Yello.Tests.Slices` | use-case slice tests | no |
| `Yello.Tests.Shared` | shared Testcontainers SQL Server fixture; **no test cases** | n/a |

Framework: xunit.v3, pinned under AR-1 and gated by `PackageVersionPinTests`. Established by
story 1.1 (`done`), documented in `tests/TESTING-CONVENTIONS.md`, currently 136 tests green
(82 architecture, 54 slices).

The handoff anticipated this collision directly: *"TF and 1.1 collide unless one defers to the
other."* Story 1.1 shipped first, so **this run defers.** No backend scaffolding, no changes to
the five suites, no re-decision of the runner.

**Not a halt. A scope reduction:** this run binds the browser test level and nothing else.

### 3. Project context

**Stack.** .NET 10, `Yello.slnx`, central package management, `TreatWarningsAsErrors` with
SonarAnalyzer active (S1135 makes the word `Todo` in a comment a build error — recorded in
`deferred-work.md` as a trap owned by Epic 6). Aspire AppHost present (`aspire.config.json`).
Container runtime on this machine is Rancher Desktop, not Docker Desktop.

**Existing conventions this run must not break.**

- Trait vocabulary is exactly four names: `Suite`, `Priority`, `Requirement`, `Assumption`.
  `TestingConventionTests` asserts the `Assumption` value format, so drift fails the build.
- CI tiering selects on those traits via `dotnet test --filter`. Any new test level that cannot
  be reached by that command is invisible to the tiering the test design specifies.
- Tiers: PR (< 15 min) → Nightly (accessibility E2E, focus-identity paging, Board at 5,000,
  property-based merge interleaving) → Weekly (LOAD at full envelope, cold-start).
- `tea_use_playwright_utils: false`, configured with the standing reason: *"Playwright Utils are
  TypeScript helpers for @playwright/test; this project is .NET 10 / xunit.v3."*

**Five test levels named by the test design.** UNIT (xunit.v3), INT (xunit.v3 + Testcontainers +
`WebApplicationFactory`, both calling surfaces), ARCH (ArchUnitNET + schema tests), LOAD (k6 or
equivalent, local target) — all four bound. **E2E: "browser, binding per B5" — unbound.** That
is the deliverable.

### 4. What the browser level owes, per the design documents

**B5 / R16 / T9 — the two AD-29 invariants unreachable from every existing suite:**

1. append-never-recycle — focus must not silently re-point at a *different* Task across paging
2. `aria-setsize` / `aria-posinset` agreeing with the visible count chip

T9 records why this matters beyond coverage bookkeeping: one of them is *"a data-corrupting
defect reachable only by keyboard, invisible to pointer testing."* Consumed by stories 2.8 and
2.9. Requires the `data-task-id` attribute the handoff recommends adding to story 2.2's ACs.

**NFR-9 accessibility** — WCAG 2.1 AA on five named flows, keyboard parity for every Board
pointer operation, ARIA live announcements. The 18 gated contrast pairs already ship as pure
unit tests in story 1.2 (deliberately browser-free, so they were never blocked on B5); axe
reports and the keyboard parity matrix are not yet covered anywhere.

**The boot smoke check — high severity, added to `deferred-work.md` 2026-09-05, owned by this
run.** Story 1.3 closed with 136 green tests and a client that did not start in any browser or
locale: `Program.cs` assigned the resolved culture after the WebAssembly runtime had booted, and
Blazor refuses a post-boot culture change unless `BlazorWebAssemblyLoadAllGlobalizationData` is
set. Every one of the 136 tests reads a *file*; nothing loaded the page. The interim was proved
on 2026-09-01 against a dev server on a spare port:

```
msedge --headless=new --virtual-time-budget=20000 --dump-dom http://localhost:5131/
```

Two assertions on the returned DOM: `#app` contains the routed `<h1>`, and `#blazor-error-ui`
still carries `display:none`. **Run it per supported culture, not once** — the defect fired for
every locale, and a single `en-GB` run is exactly what misses the class it belongs to.

### 5. Findings summary

- Project type: .NET 10 solution, Blazor WebAssembly client + ASP.NET Core host, Aspire orchestration
- Bundler: none (no JS toolchain present)
- Framework already installed: **yes for UNIT/INT/ARCH** (xunit.v3, six projects, settled by story 1.1) — **no for E2E**
- Context docs found: architecture spine, PRD, epics, both test-design documents, the TEA handoff, testing conventions, deferred-work ledger
- **Open decision blocking step 2: B5, the browser-test binding**

### Sequencing note

Story 1.3 is at status `review` with `bmad-code-review` not yet run, and the working tree carries
uncommitted 1.3 work (the `Program.cs` culture fix, two `deferred-work.md` entries, an untracked
`run.ps1`). A new test project landing now becomes part of what that review sees. Flagged for
Lee; not a blocker either way.

**Resolved.** Lee chose to scaffold on a clean baseline. The 1.3 work was committed as `6774f60`
and `run.ps1` as `7a90eb0` before any framework file was written, so the framework diff and the
story-1.3 diff are separate commits and the code review of 1.3 reads only its own change.

---

## Step 2 — Framework selection

### The decision

**Playwright for .NET** (`Microsoft.Playwright` 1.62.0), as a new xunit.v3 project
`tests/Yello.Tests.Client`. This is blocker **B5**, which the test design assigns to this run
rather than recommending an answer for: "Whether that is Playwright for .NET or a separate
TypeScript project decides whether they are tested at all."

Decided by Lee on 2026-09-07 from three options.

| Option | Why not chosen |
| --- | --- |
| **Playwright for .NET** | **Chosen.** |
| TypeScript `@playwright/test` | Richest runner and the direct home for `@axe-core/playwright`, but it introduces Node into a repository that deliberately has none: a second CI toolchain, a second trait vocabulary that `dotnet test --filter` cannot reach, and npm versions invisible to AR-1's `PackageVersionPinTests`. |
| bUnit + a headless smoke check | Cheapest, stays in xunit.v3 — and does not close B5. bUnit renders components in-process, so it has no real focus behaviour, no real ICU boot and no browser-dispatched keyboard events. AD-29's append-never-recycle invariant would stay untested, which is the invariant T9 calls "a data-corrupting defect reachable only by keyboard". |

### Why the .NET binding fits this repository specifically

- `tea_use_playwright_utils: false` was already configured, with the standing reason: *"Playwright
  Utils are TypeScript helpers for @playwright/test; this project is .NET 10 / xunit.v3."*
- CI tiering selects on four trait names, and `TestingConventionTests` asserts their format. A
  separate toolchain cannot be reached by the one command written to find things.
- `Directory.Packages.props` stays the single place a version is stated, and the pin gate keeps
  asserting it.

### The browser is pinned, and that decided the second question

Chromium **151.0.7922.34**, downloaded by Playwright and fixed by the `Microsoft.Playwright`
version. `Channel = "msedge"` — what the 2026-09-01 interim used — was rejected: it runs whatever
Edge the machine happens to have, which is the same unpinned-dependency shape the repository
already flags as an open question for the SQL Server `2025-latest` tag, and a CI Linux agent has
no Edge at all.

Cost: one `playwright install chromium` per machine or cold CI agent (~192 MB, cacheable).

---

## Step 3 — Scaffold

### Files added

| File | What it is |
| --- | --- |
| `tests/Yello.Tests.Client/Yello.Tests.Client.csproj` | The sixth test project. `OutputType=Exe`, `IsTestProject`, no `ignore-exit-code 8` because it ships with cases. |
| `ClientDevServer.cs` | Starts `Yello.Client`'s dev server and reads back the address. |
| `ClientSurfaceFixture.cs` | One dev server and one browser for the suite; per-case isolation from a fresh browser context. |
| `ClientSurfaceCollection.cs` | The collection definition. |
| `BrowserLocales.cs` | The four browser locales, held as data with the path each one covers. |
| `BootSmokeTests.cs` | Eight cases: two claims across the four locales. |

### Files changed, and why each was unavoidable

| File | Change |
| --- | --- |
| `Directory.Packages.props` | The `Microsoft.Playwright` pin, with the note that it also pins the browser. |
| `PackageVersionPinTests.cs` | `ExpectedNonAr1Pins` — the gate asserts the two tables exactly, so an unlisted pin fails the build. |
| `AllowedReferenceEdges.cs` | `TestProjects` plus a `Table` row (`Yello.Contracts` only). The table asserts edges by **exact equality**, so this edit is the visible moment the dependency rule changed — which is what it is for. |
| `SolutionInventoryTests.cs` | "thirteen named projects" → fourteen, "five test projects" → six, in a method name and two messages that state counts as fact. |
| `Yello.slnx` | The project entry. A project missing here is silently skipped by `dotnet build` and `dotnet test`. |
| `tests/TESTING-CONVENTIONS.md` | The suite table, the `Suite` trait vocabulary, and a new "The browser level" section. |

### Design decisions worth carrying forward

**A real process serving the real build output**, mirroring `HostProcess` in `Yello.Tests.Slices`.
That is not a stylistic choice: the defect this suite exists to catch was in the composition root
and threw before the first render, so a harness that assembled the application itself would
reproduce the original blindness exactly.

**`dotnet run --no-build --no-launch-profile --urls http://127.0.0.1:0`.** `--no-launch-profile`
is load-bearing — the `http` profile sets `launchBrowser: true` and pins port 5131, so honouring
it would open a window on whoever ran the suite and collide with a developer already serving the
client. Port 0 with the address read back from Kestrel's own line removes the probe-then-bind race
entirely.

**`Yello.Contracts` and nothing else.** No reference to `Yello.Client`: the suite drives it as a
black box over HTTP, which is what a browser level is for. Contracts is there so the expected
`<html lang>` is computed from `CultureSelection` rather than written as `"en"`.

**Per locale, not once.** `SupportedCultures.All` has one entry, so iterating it would run each
case once under `en` — the exact run `deferred-work.md` warned would miss this class of defect.

---

## Step 4 — Docs and scripts

`tests/TESTING-CONVENTIONS.md` gained a **"The browser level — blocker B5"** section covering the
binding and why, the conversation that preceded a sixth suite, the one install command, the
no-container property that puts it in the PR tier, what it asserts today, and the plant results.

The suite table and the `Suite` trait vocabulary were updated in place — both are read as fact by
anyone adding a case, and a stale table is how a seventh suite gets created by accident.

No script was added. The install is one documented command, and `run.ps1` (committed as `7a90eb0`)
already covers starting the full stack for a human.

### For CI, when story 1.10 builds the pipeline

- `dotnet build Yello.slnx` **must precede** the suite: it launches with `--no-build`.
- `pwsh tests/Yello.Tests.Client/bin/<config>/net10.0/playwright.ps1 install chromium --with-deps`
  once per agent; cache `~/.cache/ms-playwright` (Linux) or `%LOCALAPPDATA%\ms-playwright`
  (Windows).
- **PR tier.** No container, ~14 seconds. It is the cheapest gate in the repository that can tell
  you the application starts.
- Select with `--filter "Suite~Client"`; `Priority~P0` already reaches it.

---

## Step 5 — Validation

### Test results

`dotnet build Yello.slnx` — **succeeded, 0 warnings**.

| Suite | Result |
| --- | --- |
| `Yello.Tests.Architecture` | **82 passed** — including every gate edited above |
| `Yello.Tests.Client` | **8 passed** (~14s) |
| `Yello.Tests.Merge` / `.Isolation` / `.Revocation` | passed (still empty) |
| `Yello.Tests.Slices` | **22 failed** — see below |
| **Total** | **144** (136 before this run, plus 8) |

**The 22 Slices failures are environmental and pre-existing.** Every one is
`DockerUnavailableException: Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'`.
Rancher Desktop's application is running but its backend is down — the wedged-socket state
`run.ps1` was written to diagnose, needing `rdctl shutdown` + `wsl --terminate rancher-desktop` +
a GUI relaunch. Not caused by this run, and not fixed by it: the relaunch step needs a human.

**Worth a look separately:** `TESTING-CONVENTIONS.md` says a suite skips with a reason when no
container runtime is up, via `Assert.SkipUnless(fixture.IsAvailable, …)`. `StartupConnectivityCheckTests`
— a story 1.1 direct construction rather than a `MigratedDatabaseFixture` consumer — does not take
that path and errors instead. Pre-existing, out of scope here, and not recorded as deferred work
because it belongs to whoever owns story 1.9's sharing decision.

### Planted violations — 2026-09-08

Per `TESTING-CONVENTIONS.md`: *"An absence assertion must be validated against a planted signal,
or it is not a test."* Each plant was applied to a tracked file, built, run against the browser
suite only, and restored from an in-memory copy in a `finally`. Never `git checkout --`.

| # | Plant | Result |
| --- | --- | --- |
| A | The 2026-09-01 defect re-armed — culture assigned after the runtime booted | **caught**, all 8 |
| B | `DocumentLanguage` writes `data-lang` instead of `lang` | **not caught** → suite strengthened → **caught**, the 4 language cases only |
| C | Routed heading demoted from `h1` to `h2` | **caught**, all 8 |
| D | An exception after the first render, unhiding the error banner | **caught**, the 4 boot cases only |

**Plant B is the finding of this run.** The document-language case was vacuous: `index.html`
hard-codes `lang="en"`, every locale resolves to `en` while there is one neutral resource set, so
`"en" == "en"` held with the feature entirely switched off. An inert check reporting green — the
exact defect class this suite was built to catch — inside this suite. Fixed in the test, not the
product: the served document's pre-boot `lang` is rewritten to `und` before the page loads, so the
asserted value can only have come from the client, and the case asserts that its own rewrite
actually matched so the coupling to `index.html`'s markup fails loudly rather than silently.

B and D each failed exactly four cases and left the other four passing, which is the right blast
radius in both directions — the suite distinguishes "did not start" from "started, then threw" and
from "started, wrong language".

### Two things changed as a result of running the plants

**The render timeout came down from 60s to 30s.** With the app broken every case waits it out, so
timeout × case count is how long a broken build takes to say so — plants A and C each spent about
eight minutes discovering what they already knew. The suite runs in ~14s warm, so 30s keeps roughly
twenty times the headroom a page load needs.

**`--no-build` after a plant run uses the planted binary.** Cost one confusing full-solution run
here. Rebuild between a plant harness and a real run.

### Blocker and risk status

| Item | Status |
| --- | --- |
| **B5** — the browser-test binding | **RESOLVED.** Playwright for .NET, pinned browser, sixth suite. |
| **R16** — client invariants have no test level | **Mitigated, not closed.** The level exists; the two AD-29 invariants are not asserted yet because there is no Board until epic 2. |
| **T9** — the Blazor client has no named test level | **RESOLVED.** The level and the sixth project both exist. |
| `deferred-work.md` — "no gate runs the client in a browser" (high) | **CLOSED.** |
| `deferred-work.md` — the culture trap re-armed by a second translation (medium) | Still open, still owned by whichever story ships a second translation. Plant B is now a live regression test for half of it. |

### What this run deliberately did not build

- **AD-29's two invariants** (focus identity across paging, `aria-setsize` vs the count chip).
  There is no Board to assert them on. Stories **2.8** and **2.9** own them and now have a home
  for them. Both still need the `data-task-id` attribute the TEA handoff recommends adding to
  story 2.2's acceptance criteria.
- **NFR-9 accessibility** — axe reports and the keyboard-parity matrix. Nightly tier, per the test
  design. Story 1.2's 18 contrast pairs already ship as unit tests and never needed a browser.
- **The backend framework.** Settled by story 1.1; this run deferred to it, as the TEA handoff
  said one of the two would have to.
- **A CI pipeline.** Story 1.10's. The requirements are recorded in step 4 above.
