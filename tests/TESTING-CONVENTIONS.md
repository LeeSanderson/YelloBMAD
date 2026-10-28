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
retrofitted. Five names, and only these five (`Tier` added by story 2.9, open question 13 (a)):

| Trait | Values | Meaning |
|---|---|---|
| `Suite` | `Architecture`, `Isolation`, `Revocation`, `Merge`, `Slices`, `Client` | which suite the case belongs to |
| `Priority` | `P0`, `P1`, `P2` | selection tier. `P0` is release-gating |
| `Requirement` | e.g. `AR-1`, `AR-2`, `AR-4`, `NFR-1`, `FR-34` | the requirement the case traces to |
| `Assumption` | e.g. `PRD-12-2` — source document, then location within it | the assumption the case hardens into an assertion |
| `Tier` | `Nightly`, `Weekly` | the scheduled stage that runs the case; absent for the PR stage. A tiered case is also `Explicit = true` |

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

**A race is controlled, never hoped for - added by story 2.2.** A case about two requests that race
(a creation bound, a key's first use) holds the contended range itself and lets the requests queue on
it: the test opens a connection under the Space's session key, begins a transaction, takes the same
lock the product takes (`SELECT COUNT(*) ... WITH (UPDLOCK, HOLDLOCK)`), sends both requests, polls
`sys.dm_exec_requests` with a deadline until both are blocked on any `LCK_M_%` wait
(`TaskWire.WaitUntilBlockedOnALockAsync`), then commits. Wait on "blocked on a lock", not on one wait
type, so a plant that drops the product's hint is caught at the insert instead. Make every racing
Account present in the Space first: an Account's first request in a Space inserts its
`SpacePresence` row, and the second Account's presence count waits on it, which serialises the pair
before they reach the race at all - why story 2.1's case caught its plant only 2 times in 3. Firing two
requests with `Task.WhenAll` and asserting on whatever happened is not a race case.

**When the race is about a read, the test does the other write - added by story 2.3.** Holding the range
proves both writers queue, but a writer that reads without the lock may still be serialised by the
other writer's exclusive key locks and get the right answer by luck (story 2.3's two race cases caught a
removed `UPDLOCK, HOLDLOCK` only one run in three). So the deterministic case holds the range, sends the
one request, waits until it is blocked, then writes in its own transaction what the competing writer would
(`A_Status_edit_reads_the_last_key_of_its_new_column_only_under_the_lock` places a Task at the column's
next key) and commits: a request that read before the lock computes the same key and collides, three runs
in three.

**A race about a lock held between two statements holds the request between them - added by story 2.4.**
When the guarded interleaving is "the request read X, then something changed X before it wrote", holding X
itself cannot show it: a read-committed read already waits on the other writer's uncommitted change and
reads the right answer, so removing the hint is caught zero runs in three (story 2.4's
`An_application_racing_a_deletion_that_commits_first_answers_400_invalid_task_labels`). Hold something the
request touches *after* its locked read instead (`A_deletion_racing_an_application_that_has_read_its_Labels_waits_and_answers_409`
holds an uncommitted application of another Label to the same Task), send the competing request, and wait
until it is blocked **or has answered** (`LabelWire.WaitUntilBlockedOrAnsweredAsync`): a product that holds
its lock makes it wait; one that does not lets it through, and the assertions after the release report it.
That case also found a real deadlock the first time it ran - the two writers' locking reads chose
different indexes of one table (a covering alternate key and the clustered key), so their U locks never
met. Name the index in a lock hint that two writers must share (`INDEX(1)`).

**A deletion races every writer on its rows, from both sides - added by story 2.5.** `DeleteWire` plays each
competing writer in the product's own statement shape, so the test and the request meet on one key: the
Project's Task range (`SELECT COUNT(*) ... WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = @p`, the bound count's
shape), a Task or Project row (`WITH (UPDLOCK, HOLDLOCK, INDEX(1))`), a Status move, a creation's insert, and
the whole deletion held uncommitted. Write both directions for each pair. *The other writer holds, the request
is the deletion* (RACE-T1, T3 (a), T5, P1 (a), P2 (a)): this is where a lock-order cycle shows - RACE-T1 caught
a deletion that took the Task row before the range as a 1205, 3 runs in 3. *The deletion holds, the request is
the other writer* (RACE-T2, T3 (b), T4, P1 (b), P2 (b)): this is where a writer answering `200` or `500` on a
row that has gone shows. For the Project, hold the creation **between its statements** - counted under the
range and read its Project, insert not yet sent - and wait until the deletion is blocked or has answered
(RACE-P1 (a)); a deletion that skipped the range commits under it and the test's insert meets 547, 3 runs in
3. A lock hint whose plant is caught 0/3 is recorded as masked with the reason, never deleted
(`deferred-work.md`, story 2.5).

**A move is a key placer that reads its neighbours, so its decisive race is the read race - added by story
2.6.** `MoveWire.HoldMoveAsync` plays a move in the product's shape (the Project's Task range in the count
shape, then the Task row `INDEX(1)`, then the one `UPDATE`), and the existing writers are played by
`DeleteWire` and `LabelWire`, so every pair meets on one key. *The test holds, the request is the move*
(RACE-M1..M3, M4 (a)..M8 (a)); *the move is held, the request is the other writer* (M4 (b)..M8 (b), M4 (b) a
real Status `PATCH` in its own `MAX(CASE…)` range shape). The case
that decides the move's lock order is RACE-M2 (`A_move_reads_its_neighbours_keys_only_under_the_lock`): the
test holds the range, sends a move "after X", waits until it is blocked, then itself inserts a Task right after
X and commits - a move that read X's successor before the lock lands after the new Task or collides with it,
3 runs in 3, and the same case caught the range taken without its hints 3/3. Two moves into one gap held on the
range (RACE-M1) pass even with the range hint removed: the moves then queue on the index at their `UPDATE`, and
jitter keeps their keys apart - the jitter is the second line, so the read race, not M1, is the gate.

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
- **An optimistic edit is tested hold, assert, release, assert - added by story 2.3.** Arm the hold
  (`HoldTaskEditAsync` holds the *next* edit only), make the change, await the hold's arrival, assert the
  new value is already on the card and in the panel while the request is out (AC1's ordering, the one 2.1's
  non-optimistic rename rejected), then release it admitted, refused (`ReleaseTaskEditRefused`) or
  unanswered (`ReleaseTaskEditUnanswered`, a connection reset) and assert the value stays or reverts with
  its copy. A case about two attributes holds the first edit and lets the second be answered, so a revert
  is proved to go to the attribute's last *admitted* value, not to the display before either edit.
- **Build `Yello.Client` before a browser plant.** The suite serves the client's own build output
  with `--no-build` and the test project does not reference it, so `dotnet build tests/Yello.Tests.Client`
  leaves a planted component unserved and the plant looks uncaught. Build `Yello.Client` (or the solution).
  **And build it in Debug** (story 2.4): the dev server is `dotnet run --no-build` with no configuration, so
  it serves `Yello.Client/bin/Debug` whatever configuration the test project itself was built and run in.
- **What it does not prove**, stated in its remarks: the real Session cookie across origins, the
  real preflight, and anything the Host decides. Those are story 1.9's.
- **Never wait for a client-side navigation with Playwright's URL wait.** Its default waits for
  the `load` event, which a history-API navigation never fires, and even with `WaitUntil = Commit`
  it hung intermittently on navigations that had already happened (story 1.7 measured it: the
  address and the DOM both on the destination, the wait still pending at 30 s). Poll the page's own
  `location.pathname` (`SpaceSwitchingTests.WaitForPathAsync`), or wait on what the destination
  renders.

### Drag: pointer and touch gestures - added by story 2.7

`DragTests` (pointer) and `TouchDragTests` (touch) drive the Board's drag; `DragHarness` holds what
they share.

- **A pointer drag is Playwright's mouse**: `Mouse.MoveAsync` to the grab point, `DownAsync`, a
  move past the drag-start distance (the 10px slop token), then `MoveAsync(x, y, new() { Steps = n })`
  to the target and `UpAsync`. Before releasing, **wait for the drop zone to be in the expected
  slot** (`DragHarness.WaitForDropZoneAsync`): the slot reaches the page through .NET, one render
  behind the pointer, and a release that races it drops into the previous slot.
- **A touch gesture is the Chrome DevTools Protocol.** Playwright's touchscreen only taps. Open the
  context with `hasTouch: true` (`ClientSurfaceFixture.OpenAsync(locale, timezoneId, hasTouch)`), then
  `context.NewCDPSessionAsync(page)` and `Input.dispatchTouchEvent` with `touchStart`, `touchMove`
  and `touchEnd` (`DragHarness.TouchAsync`, `SwipeAsync`). It runs through Chromium's real input
  pipeline, so a pan begun on a card really scrolls the column or the Board. A second finger is a
  `touchStart` naming both points. Chromium only - the only browser the suite runs.
- **Poll the drag state; never sleep.** A lift is waited for by polling the card's
  `task-card--lifted` class. "Never lifted" is asserted once the page's own `performance.now()` is
  past a window measured from the press (`WaitPastLastPressAsync`), against a `MutationObserver`
  installed before the press that records every element gaining the class (`ObserveLifts`, which
  also stubs `navigator.vibrate` to count haptic pulses).
- **A case that must move before the long-press threshold lengthens it first.** The module reads
  `--motion-long-press-threshold` at each press, so a style tag (`LengthenThresholdAsync`) gives the
  case a 1,000ms window instead of racing 320ms against a loaded machine. The lower bound of a lift
  is then measured on the page's clock (press to lift >= threshold).
- **Long-press cases run in `TouchTimingCollection`** (`DisableParallelization = true`, the same
  `ClientSurfaceFixture`): they measure time, so nothing else in the process competes with them.

### Keyboard: the Board's composite - added by story 2.8

`KeyboardTests` drive the Board from the keyboard; `KeyboardHarness` holds what they share (and
`TextSpacingTests`' mid-carry case uses it too).

- **Wait for the keyboard module before the first key.** `js/board-keys.js` attaches after the
  Board's first render, asynchronously, and a key pressed before then is the browser's alone.
  `KeyboardHarness.WaitForKeysAsync` polls by dispatching a cancelable `ArrowUp` on the Board's one
  tab stop (the first card, top of its column, where `↑` moves nothing) until the module prevents
  it. Never a sleep.
- **Start each key sequence from a known focus.** Either `ResetFocusAsync` (blur, then focus the
  body - `BoardTests`' idiom) before a Tab sequence, or `FocusCardAsync(title)` before arrows. Then
  `PressAndFocusAsync(key, expected)` presses and polls where focus is, named by
  `FocusedAsync`: a card's title, `actions:{title}`, `column:{Status}`, or `TAG:{accessible text}`.
- **Assert `defaultPrevented` from a window listener.** `RecordKeys` adds a bubbling `keydown`
  listener on `window`, which runs after the Board's, and records whether each Space was prevented;
  it also counts each `Escape` that reaches the document, so a carry's Esc that escaped the Board is
  caught.
- **Force right to left with `dir="rtl"` on `<html>`** after the Board loads. No product locale is
  right to left, and the module reads the Board's computed direction at each key.
- **Observe announcements with a `MutationObserver`.** `RecordAnnouncements` records, in
  `window.__said`, each non-empty text a polite region changes to. A region set to the same sentence
  again does not change and records nothing - the defect `LiveAnnouncement` closes - so "said twice"
  is `WaitUntilSaidAsync(sentence, 2)`. Two sentences said before one render are joined in one entry,
  and the wait matches either.
- **Hold → assert → release → assert** for every keyboard drop, as for the picker and the drag.

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

**Every migration must work with the revision before it (expand/contract, story 1.10).** The deploy
runs the migration job while the previous revision is still serving. So add columns as nullable or
with a default, and drop or rename only in a later release. A migration that breaks the running
revision is a defect. Migrations are applied by the bundle in Azure and by the AppHost's `migrate`
resource under `aspire run`. The Host never applies them: `MigrationAtStartupGateTests` fails the
build on a production call to `Migrate`, `EnsureCreated` or `IMigrator`.

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
Story 2.1 added no engine; its new cases ride the same four (see its Debug Log for the counts).
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
not from a hand list, and a row must send a request on the route it names. A row pending a later story names that story and a reason, and fails the suite
once the story reaches `review` (it may stay pending while the story is `backlog`, `ready-for-dev` or
`in-progress`; any other status fails). Story 2.1 Q1 (c) corrected this from "leaves `backlog`", which
turned the suite red at the create-story commit, before anyone could convert the row. The gate's
oracle runs against synthetic rows, so it does not depend on how many real rows are pending.

**The harness is shared, and seeds through the product.** `HostProcess` and `WireSession` moved from
the slice suite into `Yello.Tests.Shared`, beside `RunningHost` (one Host process per test class, with
a log cursor that waits for the request-finished line of every request a case sent),
`ProductActor` (register, sign in, read the Personal Space, issue a Token - all over HTTP) and
`WireDatabase` (raw SQL only for what no endpoint does yet: the refusal record, a second owned Space,
Membership removal, Space and Account deletion, a Role change). Each class gets a world: a database
of its own on the process's one engine, migrated, and a Host on it.

**A row that writes sends a body** (story 2.1). `ProductActor.SendAsync(surface, method, path, body,
headers)` carries JSON and any extra header - an `Idempotency-Key`, say - on either surface: the
Session surface still adds the anti-forgery header on an unsafe method, and the Token surface still
sends the bearer and nothing else. `HoldsProjectNamed` adds a Project name to the actor's secrets, so a
Project name in the Host's log fails I-17 like any other secret; seed Projects after the case's log
cursor, so the requests that create them are inside the checked slice.

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

**A Space-scoped not-found is held to a floor - added by story 2.2 (Q7 (a)).** The boundary `404`
writes an `AccessRefusal` and the in-Space not-found (an id that matches nothing in one's own Space)
writes nothing; measured on the Board route, the gap was 10.6-28.8% on a Session and 11.8-15.2% on a
Token, and one run in three failed the 20% tolerance. `NotAvailableFloor` now holds every `404` a
Space-scoped route answers to `SpaceBoundary:NotAvailableFloorMilliseconds` (50 ms by default), and
the gap measures 0.1-1.2%. The unmatched fallback is outside the floor, so the permanent oracle still
separates (93-99%). A timing row that passes only because both arms sit on the floor is the point:
the floor is the product's equaliser, and it is what the row proves is in place.

**The real browser against the real Host** is `Yello.Tests.Client`'s `RealHostCollection`: the dev
server first, then a Host with `ClientOrigin` set to the client's origin, on a database on the
suite's one engine, with `appsettings.Development.json` answered by `page.RouteAsync` carrying the
Host's address - the only fake there. Chromium keeps the Host's `SameSite=None; Secure` cookie from
`https://127.0.0.1` on an `http://127.0.0.1` page, so the client is not moved to https. A case that
needs the running client's session-end path navigates in-app (`Blazor.navigateTo`), because a reload
boots a client that never held the Session.

## CI: the PR stage - built by story 1.10

`.github/workflows/ci.yml` runs on every push and pull request to `main`. Its `test` job runs these
steps in order:

1. `dotnet restore`, then `dotnet tool restore`.
2. `dotnet build Yello.slnx -c Release`. Warnings are errors, and the NuGet audit runs here.
3. `dotnet dev-certs https` creates the certificate. It is not trusted.
4. `playwright.ps1 install chromium --with-deps`.
5. The migration bundle is built with `dotnet ef migrations bundle ... -r linux-x64`, and its path
   is exported as `YELLO_MIGRATION_BUNDLE`.
6. The architecture suite runs **first**, on its own, because it takes seconds and should fail
   before anything slower starts.
7. `dotnet test Yello.slnx -c Release --no-build`. This is the gate. A single failing case fails the
   run, and the deploy jobs `need` this job, so no deploy follows a red run. `--no-build` is safe
   here only because step 2 built the same configuration. The zero-tests trap above still applies
   anywhere else.

The PR-stage target is under 15 minutes. `DeploymentShapeTests` holds the order and the
variables, and `actionlint` 1.7.12 reports no findings on the file.

**The CI environment variables.** Neither is set locally. On a developer machine an absent runtime
and an absent bundle are local conditions, so the classes skip with a reason.

| Variable | Effect |
|---|---|
| `YELLO_REQUIRE_CONTAINER_RUNTIME=1` | An **unconfigured** runtime FAILS every container-backed class instead of skipping. `SqlServerEngine.UnavailableReason` throws. Without this, a CI agent without Docker would pass SM-1 by skipping it. `ContainerRuntimeProbeTests` covers both settings. |
| `YELLO_MIGRATION_BUNDLE` | The path of the migration bundle that `MigrationBundleEquivalenceTests` runs. When it is unset, the class skips and says why. To run it locally, build `dotnet ef migrations bundle --project Yello.Infrastructure --startup-project Yello.Infrastructure --self-contained -r win-x64 -o artifacts/efbundle-win-x64/efbundle.exe`. |
| `YELLO_REQUIRE_MIGRATION_BUNDLE=1` | A missing bundle FAILS the equivalence test instead of skipping it. |

## CI: the Nightly and Weekly stages - built by story 2.9

The three-stage shape is complete: the PR stage (`ci.yml`), `nightly.yml` and `weekly.yml`. Do **not** build smoke, P0
or P1 tiers; the test design's checklist settled that in favour of the three stages.

- **Selection.** A case belongs to a stage by `[Trait("Tier", "Nightly")]` or `[Trait("Tier", "Weekly")]`, and every
  tiered case is declared `[Fact(Explicit = true)]`. Explicit is what keeps it out of a plain `dotnet test Yello.slnx`
  - the PR run and the local full run CLAUDE.md prescribes - so the default run stays inside the memory budget and its
  time. `ci.yml` also excludes both tiers by trait (`--filter-not-trait "Tier=Nightly" --filter-not-trait "Tier=Weekly"`).
  `DeploymentShapeTests` holds all of it: `Every_Tier_trait_is_Nightly_or_Weekly`, `Every_tiered_test_is_Explicit`,
  `The_PR_run_excludes_the_Nightly_and_Weekly_tiers` and the two workflows' shape.
- **Running a tier.** Per project, with the tier filter and `--explicit on`, one module at a time:

  ```
  dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build --max-parallel-test-modules 1 -- --filter-trait "Tier=Nightly" --explicit on
  ```

  Only the projects that hold the tier's cases are run: a filter that selects nothing exits 8 (the zero-tests trap
  above). Nightly: Slices (`BoardScaleTimingTests`) and Client (`BoardRenderCostTests`, `RealHostBoardScaleTests` - the
  Client cases need the Debug client build). Weekly: Slices (`EnvelopeLoadTests`). The workflows run them per project
  for the same reason, which also keeps `dotnet test Yello.slnx` a single job for the promotion gate.
- **Local engines only** (R13): the scheduled workflows log in nowhere, read no secret and name no deployment target.
  Their figures are a local engine's, warm - never production evidence (R18) - and every report says so.
- **The scale collections.** Timed scale cases share `BoardScaleCollection` (`DisableParallelization`), each class on a
  database of its own on the suite's one engine; the browser's timed case uses `TouchTimingCollection`. A p95 is the
  nearest rank of at least 100 samples (`Yello.Tests.Shared.Percentile`, with its own `PercentileTests`), measured by the
  Host's own request-finished figure (`RunningHost.ServerDurationOfAsync`) after a warm-up.
- **No timer touches the database** (AD-10), not even here: the envelope load runs a fixed number of iterations, never a
  duration.

## The deployment's own tests - added by story 1.10

These are wire tests in `Yello.Tests.Slices/Deployment/`. They run on the shared engine, and each
class uses a database of its own:

- **The migration bundle is equivalent to `MigrateAsync`** (`MigrationBundleEquivalenceTests`).
  The bundle and `MigrateAsync` each migrate a fresh database. The test compares the two catalogues:
  columns (with precision, scale, identity and computed flags), security policies and predicates,
  filtered indexes, foreign keys with their referential actions, default, check and key constraints,
  module text, `MAXDOP` and the history rows. A second run of the bundle must apply nothing and exit 0. The catalogue queries cast every
  string column to the database's collation, because `CONCAT` refuses mixed catalogue collations.
- **The production Host shape runs on plain `http://127.0.0.1:0`.** Behind ingress, the Host learns
  the scheme from `X-Forwarded-Proto`. `DeploymentHosts.BehindIngress` sets
  `ForwardedHeaders__Enabled`, and `DeploymentHosts.ForwardHttps` adds the header that the platform
  would append.
- **A fresh profile per Host process reproduces a container.** By default, ASP.NET Core keeps its
  key ring in the developer's profile, which every local Host process shares, so a cold start can
  never lose it. `DeploymentHosts.InAFreshContainer` points `HOME` / `LOCALAPPDATA` at an empty
  folder. With the key ring persisted to `dbo.DataProtectionKey`, a cookie from process 1 still
  works in process 2 on the same database, and it is refused on another database.
- **Probes answer from process state** (`ProbeTests`). Both probes answer `200` against an
  unreachable server, and their log slice shows no connection attempt.
- **Database unavailability is a `503`** (`DatabaseUnavailableTests`). This covers both the key-ring
  path and the revocation-read path. The revocation-read case takes a database of its own offline in
  the middle of the process. Only availability is mapped: `DatabaseUnavailability` in
  `Yello.Infrastructure` classifies SQL Server's connection and availability errors, and every other
  database error stays a `500` (UNIT-008). The same class proves that a sign-out still ends a Session
  issued at a sign-out instant ahead of the clock (SLC-018).
- **The published client calls the configured API** (`StaticHostFallbackTests`, Client suite). The
  committed `appsettings.Production.json` is empty, and it exists so the boot configuration lists it:
  the WebAssembly loader fetches no other appsettings file. The test overwrites the published copy as
  the deploy does, and asserts that the booted client calls that address and never its own origin.

**`RunningHost` counts only its own requests.** Every request its client sends carries
`yello-harness=1`, and only request-finished lines that carry the tag are counted. A browser on the
same Host therefore cannot satisfy a harness wait (`RunningHostCursorTests`).

**Counts when story 1.10 closed:** 550 cases. With `YELLO_MIGRATION_BUNDLE` set, 549 pass and 1 is
skipped (the legacy-factor duration case). Without it, the two bundle cases skip as well. There are
still four engines (Slices, Isolation, Revocation, Client). A full local run took about 2.5 minutes
of wall clock on the development machine.

**Counts when story 2.2's dev phase closed:** 743 cases - 740 passed, 0 failed, 3 skipped (the bundle
cases and the legacy-factor duration case, `YELLO_MIGRATION_BUNDLE` unset), in about 2 m 55 s of wall
clock. Still four engines; 2.2 added no engine and no fixture type.

**Counts when story 2.3's dev phase closed:** 841 cases - 838 passed, 0 failed, 3 skipped (the same three,
`YELLO_MIGRATION_BUNDLE` unset), in about 4 minutes of wall clock. Still four engines; 2.3 added no engine,
no fixture type and no serial collection - its race cases hold locks briefly and add no timing case.

**Counts when story 2.4's dev phase closed:** see the story's Dev Agent Record for the run's figures.
Still four engines; 2.4 added no engine, no fixture type and no serial collection. Its four race cases
hold locks briefly.

**Counts when story 2.5's dev phase closed:** 1077 cases - 1074 passed, 0 failed, 3 skipped (the bundle
cases, `YELLO_MIGRATION_BUNDLE` unset), in about 13 minutes of wall clock. Still four engines; 2.5 added no
engine, no fixture type and no serial collection. Its ten deletion race cases hold locks briefly.

**Counts when story 2.6's dev phase closed:** 1182 cases - 1179 passed, 0 failed, 3 skipped (the bundle cases,
`YELLO_MIGRATION_BUNDLE` unset), in about 13 minutes of wall clock with `--max-parallel-test-modules 1`. Still four
engines; 2.6 added no engine, no fixture type and no serial collection. Its twelve move race cases hold locks briefly.

**The Label palette's harness - added by story 2.4.** `tests/Yello.Tests.Architecture/CieDe2000.cs`
implements CIEDE2000 beside `WcagContrast.cs`, and `LabelPaletteGateTests` proves it against Sharma, Wu
and Dalal's (2005) published pairs to four decimal places **before** any palette figure is trusted (a
known-answer test first), then computes every `--label-*` fill against the AC4 rules in both themes. Its
oracle case is a permanent plant: the rule must pass the proposed fills and report a fill at the accent,
a fill at white and an ink at `text-primary`. The 30-token colour gates exclude the family by its named
`--label-` prefix (`CssCorpus.IsLabelPaletteToken`) and only by it.

## Not yet decided

**The browser binding (blocker B5) is decided** - Playwright for .NET, `Yello.Tests.Client`; see
"The browser level" above. This section used to say no browser project existed and B5 was open,
which contradicted that section from 2026-09-07 on; corrected by story 1.7. What remains open from
it is measurement rather than binding: AC13's text-spacing and 200%-zoom measurement, AC11's two
unmeasurable clauses and the `em`-relative-length parser gap, each owned in `deferred-work.md`.
