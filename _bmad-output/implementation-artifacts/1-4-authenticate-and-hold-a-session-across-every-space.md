---
baseline_commit: 5651d415187629d0f337fed0a1a4b56d72e36f76
---

# Story 1.4: Authenticate and hold a Session across every Space

Status: review

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-4-authenticate-and-hold-a-session-across-every-space`
Requirements owned: **FR-2, AR-14 (= AD-7), AR-28 (= AD-23)** (`epics.md:347`)
Depends on: **story 1.1** (done) — the eight-project ring layout, the gating suites, the pinned stack, the build gates.
**story 1.2** (done) — the token layer. **story 1.3** (done) — `Account`, `Account.PasswordHash`, the one-way
`IPasswordHasher` port, the slice pattern, the first components, the resource system, `DurationIndistinguishability`.
Seeds, does not own: the purge seam story 1.7 extends to Space switch and sign-out (UX-DR31).

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a registered Account holder,
I want to sign in once and stay signed in across everything I belong to,
So that moving between contexts never asks me who I am again.

## Acceptance Criteria

Reproduced verbatim from `epics.md:658-685` (`status: final`). **Six blocks**, numbered by position per the
convention `epics.md:8` establishes ("Acceptance Criteria, fifth block (AC5)"). Note the count: the Story
Coverage Index and the epic's own implementation notes both under-describe this story — see *The two places
the epic under-states this story* below.

**AC1 — the Session identifies an Account and nothing more**

**Given** valid credentials
**When** authentication succeeds
**Then** a Session is issued that identifies the Account and carries no Role, no Space and no capability of its own

**AC2 — a Space change does not touch the Session**

**Given** an authenticated Session
**When** the acting Account changes active Space
**Then** the same Session is used unchanged and no re-authentication occurs

**AC3 — an expired Session grants nothing, and the client is emptied**

**Given** a Session that has expired or been invalidated
**When** any request is made with it
**Then** it grants access to nothing, including Spaces the Account still holds a Membership in
**And** the User is returned to sign-in with the reason stated, and all Space-scoped client state is purged

**AC4 — the two failure causes are one answer, including in duration**

**Given** an unknown address, and separately a known address with a wrong password
**When** authentication is attempted with each
**Then** the two responses are identical in status, body, shape and duration
**And** the message is one string for every cause — "Email or password is incorrect." — never "no account found" and never "wrong password"

**AC5 — the cross-origin session contract**

**Given** the browser surface
**When** a Session is established across the distinct client and API origins
**Then** the cookie is `HttpOnly; Secure; SameSite=None`, CORS allows exactly the configured client origin with credentials and never a wildcard or a reflected `Origin`, and every state-changing request carries an anti-forgery token
**And** no credential is ever written to `localStorage` or `sessionStorage`

**AC6 — held input is never replayed under a new Session**

**Given** locally-held unsent input at the moment a Session expires
**When** the Account re-authenticates
**Then** that input is never submitted automatically, because it would then be authored under a new Session against possibly-changed permission

## Tasks / Subtasks

- [x] **Task 1 — Decide the Session's shape and lifetime, and record the decision** (AC: 1, 3)
  - [x] **Nothing in any artifact states a Session lifetime.** Verified by grep across `_bmad-output/`: no duration, no idle timeout, no absolute expiry, no sliding-vs-absolute choice, no rotation rule, no revocation-on-password-change rule. `decisions-settled.md` has no session row at all. This is question 1 — a deliverable, not an open question.
  - [x] **A pure cookie ticket is the recommended shape and the reasoning must be written down either way.** AD-27 (`ARCHITECTURE-SPINE.md:241`) requires anything expiring by the passage of time to carry `ExpiresAt` evaluated "server-side inside the guarded statement's own `WHERE` clause", and **fails the build on a scheduled component writing a terminal expiry state**. AD-10 (`:136`) forbids any component querying the database on a fixed interval. A cookie ticket's expiry is carried in the encrypted payload and evaluated on read, so it satisfies both by construction and adds no table, no sweeper and no per-request read.
  - [x] **The word "invalidated" in AC3 does not by itself require a server-side Session record.** Membership removal (FR-14) revokes *access*, and per-request Membership resolution (1.5) already makes that immediate — the Session survives and grants nothing. Read AC3 against AD-1 before concluding a revocation list is needed. If one is needed, it is `ExpiresAt`-shaped and evaluated on read.
  - [x] Whatever is chosen, state in the Completion Notes: the lifetime, whether it slides, what happens on password change, and whether a Session is represented in the database at all.
  - [x] `DateTimeOffset` in UTC, never `DateTime` (`ARCHITECTURE-SPINE.md:269`). `DateTimeOffset.Now` is banned at build.

- [x] **Task 2 — The `AuthenticateAccount` slice** (AC: 1, 4)
  - [x] `Yello.Application/Accounts/AuthenticateAccount/` — command, handler, validator in one folder; tests mirror at `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/` (AR-3, `epics.md:191`; namespace convention `Yello.Application.{Area}.{UseCase}`).
  - [x] **The slice verifies a credential and returns an `AccountId` or nothing. It does not issue a cookie, touch `HttpContext`, or know what a Session is.** `Yello.Application` may not reference ASP.NET Core — enforced twice, by the ring gate (`AllowedReferenceEdges.cs:110-180`, exact equality) and by the `Microsoft.AspNetCore.` package-prefix ban (`:233-246`).
  - [x] The handler **hashes unconditionally**, exactly as `RegisterAccountHandler` does. An unknown address must still perform a verification against a dummy hash of the same work factor, or AC4's duration clause fails the moment it is measured. `if (account is null) return Fail();` is the defect AD-23 exists to catch.
  - [x] Copy the validator shape: hand-written `public static IReadOnlyList<string> Validate(...)`, **not FluentValidation** — that package is not in AR-1 and adding one is an architecture edit. Every rule evaluated, never short-circuited.
  - [x] **A validator that rejects before the hash reintroduces the branch by another route.** A client-side rejection that never reaches the server is safe; a server-side one that returns early is not. Story 1.3's Dev Notes state this for registration and it transfers unchanged.
  - [x] Re-implementing authorisation, Space resolution, refusal recording, idempotency or a bound check inside this slice **is a defect** (AR-3). None of them exists yet; 1.5 and 1.6 build them.

- [x] **Task 3 — `Verify` on the hasher port, and the rehash path story 1.3 left here** (AC: 1, 4)
  - [x] Add a `Verify` member to `Yello.Domain/Accounts/IPasswordHasher.cs`. Its current doc comment (`:14-16`) says the omission is deliberate — "verification is authentication, which is story 1.4's, and a port that offered it here would invite this story to grow a sign-in path". **Update that comment; do not leave it claiming an absence that is no longer true.**
  - [x] Return a three-state result, not a bool: `IdentityPasswordHasher` wraps `VerifyHashedPassword`, which returns `Failed`, `Success` and **`SuccessRehashNeeded`**. `IdentityPasswordHasher.cs:26-32` already records that "story 1.4 rewrites the hash on the next successful sign-in", and `Account.PasswordHash` is `{ get; set; }` rather than `init` **specifically so this story can write it** (`Account.cs:93-98`).
  - [x] The mechanism is already proved: `RegisterAccountIntegrationTests.Raising_the_work_factor_leaves_an_existing_hash_valid_and_asks_for_a_rehash`. This story makes it *reachable* — assert the rehash actually lands in the datastore, which nothing does today.
  - [x] **A rehash write must not make a successful sign-in distinguishable in duration from a failed one.** It happens only on success, so it cannot leak existence on the failure path — but say so in the notes rather than leaving a reviewer to work it out.
  - [x] **`Account.PasswordHash` is nullable and must stay representable as null.** `harness-constraints.md:65` requires NFR-6 to "tolerate Accounts that hold no password at all, rather than treating that as an invalid state". A passwordless Account must fail sign-in through the *same uniform path*, not throw and not be treated as corruption.

- [x] **Task 4 — Identity for authentication only: cookie issuance in the composition root** (AC: 1, 5)
  - [x] `InfrastructureServices.cs:52-53` is the hand-off and names what is absent: "no `AddIdentity`, no `AddIdentityCore`, no `IdentityDbContext`, no `UserManager<>`, no `SignInManager<>` — and by Gate C's construction, no role surface of any kind. **Story 1.4 adds authentication.**"
  - [x] **`AddIdentity<TUser, TRole>()` fails the build.** Gate C (`RoleApiBanTests`) is a live IL scan over every assembly and refuses `IdentityRole` at any arity, `RoleManager<>`, `IRoleStore<>`, `AddRoles<>()`, `[Authorize(Roles = …)]`, `RequireRole`, `ClaimsPrincipal.IsInRole`, and `ClaimTypes.Role` however spelled. Plain `[Authorize]` with no `Roles` argument is **not** banned.
  - [x] **Do not use `MapIdentityApi`.** It mints its own `/register` and `/login`, which forks Account creation away from AD-22's single slice (`ARCHITECTURE-SPINE.md:210`) and answers differently for a known and an unknown address — a direct AD-23 violation. It is the framework's shortcut for a shape this architecture has ruled out.
  - [x] **No new package is needed on the Host.** Cookie authentication lives in the `Microsoft.AspNetCore.App` shared framework, which `Yello.Host` (`Microsoft.NET.Sdk.Web`) already has. Adding a central pin is an architecture edit — `epics.md`'s AR-1 table first, then `Directory.Packages.props`, then `PackageVersionPinTests`' expected set, which is asserted as an **exact** set.
  - [x] **Cookie policy is configured once, in the composition root.** AD-7's *Prevents* clause (`ARCHITECTURE-SPINE.md:117`) bans "per-endpoint cookie configuration" in as many words — this is stronger than AR-14's paraphrase and is the binding text.
  - [x] The cookie carries an Account identifier and nothing else. **No Role claim, no Space claim, no capability claim** — AC1, AD-1 (`:75`: "Role is never read from a cookie, a claim, an API Token payload, or any cache outliving the request") and NFR-2 (`quality-budgets.md:20-25`).
  - [x] `Yello.Host/RegistrationLog.cs` uses EventIds 1008–1009 and `StartupLog.cs` uses 1000–1007. **A sign-in log starts at 1010.** `[LoggerMessage]` source-generated partials only — CA1848 and CA1873 are errors.
  - [x] **Never log the cookie** (`ARCHITECTURE-SPINE.md:274`, AR-34). And the log must not distinguish the two failure causes, which is the trap story 1.3 met on 2026-09-12 when EF Core's command log named every duplicate registration — that filter (`Program.cs:41`) is already unconditional and covers this story too.

- [x] **Task 5 — The cross-origin contract: CORS, the cookie's attributes, anti-forgery** (AC: 5)
  - [x] **Read the .NET 10 CSRF note in the Dev Notes before writing any of this.** The framework now registers an automatic CSRF middleware by default, its trust list *is* the CORS policy, and its verdict is enforced only by a form consumer. Getting this wrong produces a configuration that looks protected and is not.
  - [x] CORS: `WithOrigins(<the configured origin>)` + `AllowCredentials()`. **Never `AllowAnyOrigin`, never `SetIsOriginAllowed(_ => true)`.** The second is the trap — `AllowAnyOrigin` combined with `AllowCredentials` throws at runtime, which pushes an implementer straight onto the reflected-`Origin` form AD-7 bans by name.
  - [x] The allowed origin comes from **configuration, not source** (`ARCHITECTURE-SPINE.md:273`, AR-37: environment variables only, "no secret in source, appsettings, or a container image"). That is also what makes "exactly the configured client origin" testable rather than tautological.
  - [x] Cookie: `HttpOnly`, `SecurePolicy = CookieSecurePolicy.Always`, `SameSite = SameSiteMode.None`. `SameSite=None` without `Secure` is rejected by every current browser, so these are one decision, not three.
  - [x] Anti-forgery on every state-changing request. **This reaches registration too** — `AccountRoutes.Register` is a state-changing POST and AR-14 says *every*. Whether an unauthenticated endpoint takes a token is question 5; decide it, do not let it default.
  - [x] Nothing may write a credential to `localStorage` or `sessionStorage` (AC5), and UX-DR31 (`epics.md:275`) extends the ban to Space-scoped *content* in `localStorage`, `sessionStorage` **or IndexedDB**. There is no browser-storage interop in the client today — keep it that way, and assert it.
  - [x] The `/sync` WebSocket crosses the same origin boundary with the same cookie (`ARCHITECTURE-SPINE.md:323-325`). Nothing about it is built here; do not configure the cookie in a way that assumes HTTP only.

- [x] **Task 6 — The client reaches the Host: credentials on the wire** (AC: 3, 5)
  - [x] `Yello.Client/Program.cs:75` registers a bare `AddScoped(_ => new HttpClient { BaseAddress = ... })` — no factory, no typed client, **no `DelegatingHandler`**. A cross-origin cookie is not sent by `fetch` unless credentials are included.
  - [x] Use `SetBrowserRequestCredentials(BrowserRequestCredentials.Include)`. The namespace `Microsoft.AspNetCore.Components.WebAssembly.Http` is **already imported** at `_Imports.razor:7`, so this needs no package.
  - [x] Update the capitalised comment at `Yello.Client/Program.cs:64-68` — "A CROSS-ORIGIN CALL ALSO NEEDS CORS, AND CORS IS STORY 1.4'S". This story is that story; the comment must stop predicting and start describing.
  - [x] **Do not touch the culture block at `:29-34`.** It carries a capitalised prohibition for a reason: assigning the resolved culture to `CultureInfo.DefaultThreadCurrentCulture` stopped the application booting in *every* browser on 2026-09-01, and 136 green tests did not notice. The resolved culture drives `<html lang>` and nothing else.
  - [x] `deferred-work.md:76` closes here, or is rewritten. It is the CORS entry, owned by this story by name.

- [x] **Task 7 — The sign-in surface** (AC: 3, 4, 6)
  - [x] `Yello.Client/Pages/SignIn.razor`. Copy `Register.razor`'s structure: `@inject IStringLocalizer<ClientCopy> Localizer`, `@inject HttpClient Http`, a phase enum, an `_attempt` counter used as `@key` so a repeated failure re-announces, and the existing `FormField` / `PrimaryButton` / `InlineErrorRegion` components.
  - [x] **Two fields, email and password.** No "remember me" — nothing in any artifact mentions one, and it is a Session-lifetime decision wearing a checkbox. No password-reset link: no FR, CAP or journey anywhere mentions password reset, and adding one is inventing a requirement (the same reasoning that kept email verification out of 1.3).
  - [x] **Sign-in is a submit-and-wait interaction** — `EXPERIENCE.md:273` names registration and sign-in as "the only submit-and-wait interactions in a product with no save button". State the in-flight condition, disable resubmission, announce completion. Never a whole-surface spinner.
  - [x] **One error string for every cause**: `"Email or password is incorrect."` (`EXPERIENCE.md:272`; drawn at `mockups/registration.html:123`). One resource key, one problem `type` on the wire, no client-side branch.
  - [x] **Validation errors take `--border-hairline`, never `--danger`.** `DESIGN.md:501` lists "use danger for validation errors or ordinary refusals" as the Don't; `DESIGN.md:289` and `:482` make the spine win over any mock. The sign-in mockup's `border:1px solid var(--danger)` is precisely the forbidden move, and the file is one of the four rendered before the border and type corrections.
  - [x] **Do not copy the mockup's `1px` borders, its `px` type, or its fixed `330px` width.** All three are superseded (UX-DR2, UX-DR4, and story 1.3's registration surface, which dropped the fixed width because it would overflow a 320px viewport before a word was translated).
  - [x] Copy resources hold **sentence case** — `Email`, `Password`, not `EMAIL`. The uppercase comes from `text-transform`. A resource holding `VIEWER` makes the accessible name "V-I-E-W-E-R" under JAWS (`DESIGN.md:396-400`).
  - [x] `autocomplete`: UX-DR33's `="off"` rule covers **Space, Project and Task name inputs only**. Do not over-apply it here — `autocomplete="email"` and `="current-password"` are what WCAG 1.3.5 asks for, and `FormField` already takes an `AutoComplete` parameter.
  - [x] Decide what `/` becomes. `Register.razor:25-30` routes at both `/` and `/register` and says the root is provisional, "stories 1.4 and 1.7 decide what belongs there". This story is half of that decision — question 6.

- [x] **Task 8 — The expiry path: the 401, the reason, and the purge** (AC: 3, 6)
  - [x] **An expired Session must be refused at the authentication boundary, not the Space boundary.** AD-3 (`ARCHITECTURE-SPINE.md:88-94`) draws the 403/404 line at the Space boundary *and nowhere else*; an unauthenticated request must not enter Space resolution at all. This is a **401**, and it is not a 404.
  - [x] **Do not write an `AccessRefusal` row for a failed sign-in or an expired Session.** AD-20 (`:198`) scopes that record to "every 403 and every Space-boundary 404", with exactly two kinds, `CrossSpace` and `InsufficientRole`. A sign-in failure has no acting Account and no target Space; recording one would need a third kind and would create an enumeration artifact in the audit store. The pipeline that writes those rows is 1.6's and does not exist yet.
  - [x] **The reason stated may name the Session and nothing else.** UJ-6 tells a *live* participant that access ended, because that connection was already authorised for the Space; UJ-4's cold path must not (`surfaces-and-journeys.md:62-64`). An expired Session is the cold case. No Space name, no Membership information, no "you were removed" — UX-DR38 keeps the Space name out of the copy "in any case".
  - [x] **No copy string exists for this anywhere.** Contrast `"Access ended."`, `"Not available."`, `"Email or password is incorrect."` and `"This invitation is no longer valid."`, all printed verbatim in the spine. Session expiry gets a requirement to state the reason and no string. This story originates it — question 4.
  - [x] **No focus destination is specified either.** `EXPERIENCE.md:396-405` enumerates seven remote events and session expiry is not among them, despite `:394` stating the hazard: a destroyed focused node drops focus to `<body>` and silently strands a keyboard user. Returning to sign-in destroys the whole focused subtree. The only sanctioned pattern to extend is `:398`'s — `role="alert"`, `tabindex="-1"`, persistent, in the reading order — which `InlineErrorRegion.razor` already implements.
  - [x] **Purge before announce** (UX-DR30, `epics.md:274`). Reversing the order renders a queued frame one tick after the announcement, disclosing a Space the Account no longer belongs to.
  - [x] **The purge is nearly vacuous today and must be recorded as such.** There is no replica, no cached Board, no filter state, no Space-scoped client state of any kind — the client holds three fields in one component. A test asserting "everything Space-scoped is purged" would pass against a system with nothing to purge, which is the vacuous-gate defect this repository keeps rediscovering (`deferred-work.md:30`). **Build the seam so 1.7 and epic 2 extend it; assert what the seam does, not what it will one day cover; and say plainly in the Completion Notes that the coverage is a seam and not a proof.**
  - [x] AC6 is in the same position. There is no editor and no unsent input until epic 7. What this story can honestly hold is the *rule* — nothing auto-submits after re-authentication — and the absence of any code path that would. State the limit rather than implying coverage.

- [x] **Task 9 — AC4's duration proof, reusing the existing method** (AC: 4)
  - [x] `tests/Yello.Tests.Shared/DurationIndistinguishability.cs` already exists, built by story 1.3. `TESTING-CONVENTIONS.md:189`: "**Use it; do not improvise a second one.**" Its four fixed decisions are 21 samples per arm, the **median** not the mean, 20% of the slower median as the tolerance, a server-side measurement point, and interleaved samples.
  - [x] Two arms: unknown address, and known address with a wrong password. The measurement point is server-side, timing the operation and nothing around it.
  - [x] **Pair it with a planted oracle as a permanent test**, exactly as `RegistrationDurationTests.The_method_detects_a_registration_that_skips_the_hash` does. Keeping the plant as a live test is what stops a later story widening the tolerance to quiet a flake and silently disarming the real assertion beside it.
  - [x] Put the pure cases in a class separate from the container-backed ones, as `DurationIndistinguishabilityTests` is deliberately separate from `RegistrationDurationTests` — so a stopped container runtime does not take the pure cases down with it.
  - [x] Also assert the two responses match in **status, body, shape and headers**, compared *against each other* rather than each against an expectation. `A_duplicate_registration_answers_exactly_as_a_new_one_does` is the shape to copy.

- [x] **Task 10 — Tests** (AC: all)
  - [x] **Cases go in `tests/Yello.Tests.Slices`, mirroring `Accounts/AuthenticateAccount/`.** Do not create a suite (`TESTING-CONVENTIONS.md:30`).
  - [x] **Do not add cases to `Yello.Tests.Isolation` or `Yello.Tests.Revocation`.** Both are still empty and both carry `--ignore-exit-code 8`; adding one case means removing that property in the same change, gated both ways by `Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code`. More to the point, AD-4 requires every isolation case to run on **both** surfaces and the API-token surface does not exist until 1.8 — so the isolation suite genuinely cannot be populated here. 1.9 owns it.
  - [x] Endpoint tests boot the real Host as a process via `HostProcess.RunAsync` — there is no `WebApplicationFactory` and no `Microsoft.AspNetCore.Mvc.Testing` in this solution, and no central pin for one.
  - [x] Container-backed classes take `IClassFixture<MigratedDatabaseFixture>` and open with `Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason)`. **Each consumer gets its own ~2 GB SQL Server**; a full run already starts seven engines sequentially. Do not attempt to settle the topology — that is 1.9's, and `deferred-work.md:8` is explicit.
  - [x] **Randomise the email address in every test** (`TESTING-CONVENTIONS.md:93`). Cleanup by transaction rollback or container disposal, never by delete statements.
  - [x] **No `Task.Delay` as a synchronisation mechanism. Ever.** An expiry test that sleeps is the obvious way to write this and is forbidden — inject `TimeProvider` (already registered as `TimeProvider.System` at `Program.cs:58`) and advance it, or construct a ticket that is already expired.
  - [x] Traits: `Suite`, `Priority`, `Requirement` — **cite the `AR` id and the `AD` id** (`TESTING-CONVENTIONS.md:59`) — and `Assumption` naming its source document.
  - [x] **Every absence assertion gets a planted violation, and the result is recorded.** This story is dense with them: no Role or Space in the cookie; no credential in web storage; no wildcard or reflected origin accepted; no log line distinguishing the two failure causes; no auto-submit after re-authentication. Every one of those is the shape that passes vacuously.
  - [x] The four R8 negative tests are named and owned: wildcard origin rejected, arbitrary reflected `Origin` rejected, state change without an anti-forgery token refused, no credential in web storage (`test-design-architecture.md:522-528`, `YelloBMAD-handoff.md:86`). They are **P0**.
  - [x] A browser-level case needs thought before it is written. `tests/Yello.Tests.Client` sees `Yello.Contracts` and nothing else, runs no container, and the conventions already flag that "cases that need a signed-in Session or real data will need more, and arrive with the stories that build them" (`TESTING-CONVENTIONS.md:110-185`). Adding a Host and a database to that suite is a visible architecture edit — take it deliberately or not at all.
  - [x] No coverage threshold. Do not invent one.

- [x] **Task 11 — Housekeeping and hand-offs**
  - [x] Comments that predict this story and must now describe it: `IPasswordHasher.cs:14-16`, `IdentityPasswordHasher.cs:26-32`, `InfrastructureServices.cs:52-53`, `Yello.Client/Program.cs:64-68`, `Register.razor:25-30`, `MainLayout.razor:11-15`, `RegistrationPhase.cs:41-44`, `Yello.Host/Program.cs:11-15`.
  - [x] **Do not add review narration to source files.** `deferred-work.md:56` records that as an open finding owned by **Lee, once, for the project** — history lives in the commit message and this story's Change Log.
  - [x] If a new failure-code class is added, decide whether `LocalisationGateTests.Every_registration_failure_code_has_a_message_and_a_field` is extended to cover it. As written it reflects over `typeof(RegisterAccountFailure)` specifically, so a `SignInFailure` class is **not** covered — an uncovered code would ship with no message and no gate complaining.
  - [x] `InlineErrorRegion.razor:23` emits `class="form-status__heading"` and no such rule exists in any stylesheet. Small, pre-existing, and this story is the first to reuse that component — fix it or record it.
  - [x] `git diff --stat 5651d41` before calling the File List complete, and state the figure excluding this story file, `deferred-work.md` and `sprint-status.yaml` — including them makes the number move every time the record is edited.

## Dev Notes

### The two places the epic under-states this story

Both verified at the source, and both matter because a reviewer reading the epic rather than the story would
conclude this story is smaller than it is.

1. **`epics.md:347` assigns AR-14 to story 1.4, but epic 1's own implementation notes (`epics.md:427`) list the
   epic's rules as "AR-1 … AR-11, AR-25, AR-27 … AR-30, AR-34 … AR-39" — AR-14 falls in the gap between AR-11
   and AR-25 and appears in neither range.** The whole cross-origin contract is therefore assigned to a story
   in epic 1 while being absent from epic 1's rule list.
2. **The same line lists the epic's UX rules and omits UX-DR31 and UX-DR35** — which are the *only* upstream
   sources for AC3's purge clause and AC6's held-input clause. AC3 and AC6 have no PRD or SPEC source at all.

Neither is a defect in this story. Both are recorded so the scope is not quietly trimmed back to the epic's list.

### What the requirement artifacts actually say — and what they do not

**`AR-*` ids do not exist in the architecture spine.** The spine numbers its rules `AD-1 … AD-29`;
`epics.md:185` re-numbers them "so stories can cite them stably". The mapping this story needs:
**AR-14 = AD-7**, **AR-28 = AD-23**, **AR-3 = the spine's Design Paradigm at `:46`**, **AR-4 = AD-1**,
**AR-25 = AD-18**, **AR-30 = AD-25**. The AD text is longer and stricter in places — AD-7 additionally bans
per-endpoint cookie configuration, which AR-14's paraphrase drops. **Cite both.**

**FR-2 has exactly four testable consequences** (`prd.md:149-153`), and CAP-2 (`acceptance-criteria.md:23-28`)
restates them word for word. They are AC1, AC2, AC3's first line, and AC4's first clause.

**Everything else in the ACs comes from somewhere other than the PRD.** Neither the PRD nor the spec set
contains the words CORS, CSRF, anti-forgery, cookie, SameSite, `localStorage`, `sessionStorage`, "sign out",
"purge" or "client state" — verified by grep over both directories. So:

- AC4's **duration** clause is AD-23/AR-28, not FR-2. FR-2 `:153` only requires the *response* not to reveal
  which failure occurred; the constant-duration escalation is architectural.
- AC5 is **entirely** AD-7/AR-14.
- AC3's "reason stated" and "client state purged", and AC6 in full, are **UX-DR35 and UX-DR31** — story-level
  and UX-level, with no PRD requirement behind them.

Cite accordingly. A citation to `prd.md` for a cookie attribute would be a false reference.

**NFR-9 does not name sign-in.** Its five gated flows are "registration, Space switching, the Board, the Task
editor and the invitation flow" (`quality-budgets.md:84`). Space switching *is* named, so AC2's surface is
under WCAG 2.1 AA; the sign-in form is not, by the letter. **Hold it to AA anyway** — it is registration's twin,
drawn in the same frame row — but record that as a sensible default rather than citing NFR-9 for it. Story 1.3
could cite NFR-9 for registration. This story cannot, for sign-in.

**One divergence to report rather than resolve** (`SPEC.md:22` requires exactly that): `prd.md:147` says FR-2
"Realizes UJ-4", while `surfaces-and-journeys.md:55` lists UJ-4's carriers as "CAP-9, CAP-15, CAP-16" — CAP-2 is
not among them. The PRD is the live contract and wins.

### The decisions this story must make, because nothing states them

Story 1.3 had three of these; this story has more, and they are larger. `harness-constraints.md:110` records
that Lee's contributions are "editorial rather than generative — reject, reframe, decide" and that the working
method is to **propose concrete options and let him cut them**. Every question below carries a recommendation
and an interim default, so nothing blocks.

1. **The Session's mechanism and lifetime.** Grepped: no duration, no sliding-vs-absolute, no rotation, no
   revocation-on-password-change anywhere in the PRD, the spec, the spine, the epics or the readiness report.
   `decisions-settled.md` has no session row. Question 1.
2. **Whether a Session exists in the database at all.** AD-27 and AD-10 between them make a persisted,
   swept Session expensive and a cookie ticket cheap. Question 1.
3. **The sign-in route and its response shapes.** No document names either, exactly as none named
   registration's before 1.3 chose `POST /api/v1/accounts` → `204`. Question 2.
4. **The anti-forgery mechanism across two origins.** Question 3.
5. **The copy for "the reason stated" on expiry, and its focus destination.** Question 4.
6. **Whether registration's endpoint gains anti-forgery.** Question 5.
7. **What `/` routes to once a Session exists.** Question 6.
8. **Whether to move the dev loop to the `https` launch profile.** Question 7.

### .NET 10's automatic CSRF middleware — read this before writing Task 5

Verified against Microsoft Learn (`aspnetcore-10.0`) on 2026-09-15. This is new, it is on by default, and it
behaves in a way that is easy to misread in both directions.

- **It is registered automatically by `WebApplication.CreateBuilder`** and runs after authentication and
  authorization. `Yello.Host/Program.cs:17` calls exactly that, so **it is already active today**.
- It inspects `Sec-Fetch-Site`, falling back to `Origin`. Safe methods pass. `same-origin` and `none` pass.
  **Any other value is denied — unless the CORS policy trusts the `Origin`.**
- **Its trust list *is* the CORS policy.** There is no separate list. And **`AllowAnyOrigin` is deliberately
  not honoured as a CSRF trust signal**, because treating it as trusted would make the middleware a no-op.
  That is a second, independent reason AC5's wildcard prohibition cannot be satisfied by a wildcard that
  "happens to work".
- **The catch, and it is the one that matters here: the verdict is recorded on
  `IAntiforgeryValidationFeature` and enforced only by a *form* consumer.** A JSON endpoint that binds no form
  data records a verdict nobody reads. So the automatic middleware alone **does not** satisfy AR-14's "every
  state-changing request carries an anti-forgery token" for this API — that needs the token system
  (`AddAntiforgery` / `UseAntiforgery`, `AntiforgeryOptions.HeaderName`, and explicit validation on JSON
  endpoints, which do not carry `IAntiforgeryMetadata` by default).
- Where both run, **token validation takes precedence** and replaces the middleware's verdict.

The practical consequence: configure CORS with `WithOrigins` *first*, because the CSRF middleware reads it; then
add token antiforgery for the JSON endpoints, because the middleware will not enforce anything for them.

### The framework's own sample is the wrong shape for this architecture

Microsoft's standalone-Blazor-WASM-with-Identity guidance is the first thing an implementer will find, and
three of its central moves fail this build:

- **`MapIdentityApi<TUser>()`** — forks Account creation away from AD-22's single slice and returns
  existence-revealing responses. Banned by consequence.
- **The `/roles` endpoint, `RoleClaimType`, role claims on the principal** — Gate C fails the build on the
  whole family.
- **`AddIdentity<TUser, TRole>()`** — requires a role type. `AddIdentityCore<TUser>()` is the roleless form,
  but note that story 1.3 registered `PasswordHasher<HashedCredential>` directly and took *no* Identity
  builder at all. Reaching for a builder now is a change worth justifying rather than assuming.

What *is* reusable from that guidance: `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`
as the cookie shape, `ConfigureApplicationCookie` with `SameSite = SameSiteMode.None` and
`SecurePolicy = CookieSecurePolicy.Always` for the cross-domain case, and the `DelegatingHandler` +
`SetBrowserRequestCredentials(BrowserRequestCredentials.Include)` pattern on the client.

One further warning from that page worth carrying: **`AuthorizeView` content is discoverable in a WASM bundle
without authentication.** Anything sensitive comes from the API after the server has answered. That is the same
rule UX-DR31 states as "optimistic rendering never precedes authorisation on the first read of a Space".

### Why AC2 is true by construction — and why that must be said, not tested vacuously

**Nothing on the Session stores the active Space.** The only two session-context keys in the entire spine are
`SESSION_CONTEXT('SpaceId')` (AD-2) and `SESSION_CONTEXT('AccountId')` (AD-24), and the active Space arrives
per request in the `{spaceId}` route segment (AD-3, and the endpoint convention at `ARCHITECTURE-SPINE.md:267`).
There is therefore **nothing on the Session for a Space change to alter**.

That makes AC2 a property of the design rather than a behaviour to implement — and it makes a test of it the
easiest vacuous assertion in the story, because no Space switcher exists until 1.7 and no Space-scoped endpoint
exists until 1.5. **What can be asserted honestly now:** that the issued cookie contains no Space and no Role,
by reading the ticket's claims and requiring the set to be exactly what it should be. **What cannot:** an actual
switch. Say which is which.

The adjacent unresolved question is real and is *not* this story's to settle: `Membership` carries a
`SESSION_CONTEXT('SpaceId')` filter predicate today, and 1.7's switcher must read Memberships **across** Spaces
under an `AccountId` predicate that AD-24 has not been amended to describe. That is readiness issue 3, due
before Epic 3, and the only mention of `SESSION_CONTEXT('AccountId')` in the whole repository is a comment at
`20260828164716_InitialSchema.cs:203`.

### The bound this story does not implement, and the question it leaves behind

NFR-8 sets **50 concurrent active Sessions per Space** (`quality-budgets.md:76`), and AD-25 requires every bound
to be "declared in one place and checked by the pipeline, not by the slice". `epics.md:785` is explicit that
**story 1.6** implements the concurrent-Sessions entry, so **this story does not build it**.

But there is a contradiction underneath it that this story is the right place to surface, because this story
defines what a Session is: **a Session carries no Space (AC1, FR-2), so "concurrent active Sessions per Space"
cannot be a property of a Session and cannot be counted at sign-in.** It is a derived count over something else
— most plausibly active Space resolutions, which is 1.5's territory. The test design has already flagged the
same hole from the other side: *"the definition of an 'active Session' for NFR-8"* is listed as **UNKNOWN**
three separate times (`test-design-qa.md:122`, `:204`, `test-design-progress.md:470`).

**Do not invent a definition.** Record it against 1.6 with this story's reasoning attached, so 1.6 meets it as a
known question rather than a surprise. And do not model a `SpaceId` on the Session to make the count easy — that
would trade AC1 for a bound this story does not own.

### What NFR-5 does and does not say about sign-in

Sign-in is a write, so the only budget available is NFR-5's **500 ms p95 server-side** (`quality-budgets.md:44`).
There is no auth-specific latency budget anywhere. Story 1.3 measured PBKDF2 at 220,000 iterations on this
machine at **p50 273 ms / p95 298 ms**, decided registration is *bounded by NFR-5 and meets it*, and recorded
the caveat that an Azure vCPU more than ~1.7× slower crosses the budget (story 1.10 re-measures).

**Sign-in inherits that number and adds to it**: a successful sign-in performs a verify *and*, when the work
factor has moved, a rehash — two PBKDF2 operations. The failure path performs one. Inherit 1.3's resolution
rather than re-deciding the work factor, and measure the success path once before claiming the budget is met.

Note the direction this cuts against AC4: the failure and success paths have *different* costs, and AC4 only
requires the **two failure causes** to match each other. Do not over-scope it into success-equals-failure.

### The security exposure this story inherits but does not fix

`deferred-work.md:117` records, at severity **high**: the registration endpoint is an unauthenticated
220,000-iteration PBKDF2 with **no rate limit and no concurrency bound**. Verified at story 1.3's review: no
`AddRateLimiter`, no `UseRateLimiter`, no rate-limiting package anywhere. **Lee assigned it to story 1.6** on
2026-09-10, because AR-3 forbids a slice bolting a limiter into itself and 1.6 is where a refusal path with a
uniform response belongs.

**Sign-in doubles the exposure** — a second unauthenticated endpoint performing the same deliberate 273 ms of
CPU on any well-formed body. This story does not fix it, for the same AR-3 reason. What it must do is **update
that ledger entry to name both endpoints**, so 1.6 sizes the limiter against two surfaces rather than one.

And carry forward the constraint 1.6 must not miss, already recorded there: **the 429 has to be uniform.** A
limiter that answers differently for a known address than an unknown one reintroduces exactly the oracle AD-23
closed, by a slower route.

### What OAuth will break, and what this story must leave soft

`harness-constraints.md:53-55`: **OAuth sign-in is the selected P6 mid-flight change, scheduled to fire "once
the identity epic has shipped"** — that is, immediately after this epic. `SPEC.md:241` names CAP-1, CAP-2 and
NFR-6 as the stories that must leave its assumptions soft. **CAP-2 is this story.**

The four rows that must stay soft (`harness-constraints.md:63-66`), and what each forbids here:

- **`Account` is unique by email address** — "the load-bearing one". Resolve the Account *by* email in v1, but
  do not let email-as-identity reach the Session, a claim, a route, or anything the credential path cannot
  survive without.
- **An Account is created with an email address, a password and a display name** — an OAuth Account has no
  password. `Account.PasswordHash` is already nullable; sign-in must treat a null hash as a **uniform failure**,
  never as corruption and never as a different response.
- **NFR-6's storage requirements** — must tolerate Accounts holding no password rather than treating that as an
  invalid state.
- **AD-23's uniform responses** — "the guarantee must hold identically on the OAuth path, which is a new
  disclosure surface." **This is the one most likely to be got wrong here:** implement the uniformity —
  status, body, shape and duration — in the endpoint, **not inside the password comparator**. A guarantee that
  lives in the password branch does not exist on any other credential path.

And the counter-instruction, `harness-constraints.md:81-83`: **do not pre-build OAuth-shaped scaffolding.**
"Authentic complexity is worth materially more than contrived complexity that ticks the same box." Leave the
assumptions soft; do not invent the seam.

### The defect class this repository keeps rediscovering

Stories 1.1, 1.2 and 1.3 each needed multiple review passes, every time for the same reason — in the commit
subjects: *"gates that asserted less than they claimed"*, *"the fixes that repeated the defect they fixed"*.
Two structural remedies are in force and both apply to every assertion here:

1. **Glob the whole repository, never a named file.** A gate naming files that do not exist yet passes
   vacuously (`deferred-work.md:30`).
2. **Plant a violation a later story would plausibly write, confirm the gate fires by name, record the result**
   (`TESTING-CONVENTIONS.md:102-104`).

A third: **do not write a completion note claiming a gate covers more than it does** — `deferred-work.md:14`
names that the "false-record defect", and story 1.3's review found four separate instances of it in a story
whose author was actively watching for them.

This story's exposure is unusually high, because **three of its six ACs describe behaviour over systems that do
not exist yet** — no Space switcher (AC2), no Space-scoped client state (AC3's purge), no editor and no unsent
input (AC6). Each is an invitation to write an assertion that passes because there is nothing to fail. Name the
limit in the Completion Notes for each one.

### Previous story intelligence

- **The client did not start, and 136 green tests did not notice** (2026-09-01). The cause was a post-boot
  culture assignment; the lesson is that this project's server-side suites do not execute the client at all.
  `tests/Yello.Tests.Client` now exists precisely for that, and it runs the real build output with
  `--no-build`, so `dotnet build Yello.slnx` must precede it.
- **Logic a test must reach cannot be a local function in `Program.cs`.** Story 1.1 found deleting an entire
  startup call left 52/52 green. Hence the named-static-class-plus-`HandleAsync` shape, and hence
  `RegisterAccountEndpointMarker` — a sign-in endpoint needs its own marker, and S2094 means that marker needs
  a member.
- **The nullable body parameter is deliberate.** `RegisterAccountEndpoint.cs:66-70` — an absent body arrives as
  `null` and is reported as malformed, rather than throwing `BadHttpRequestException` out of the binder with an
  unshaped response. Repeat it.
- **`HostProcess` drains both stdout and stderr.** It did not until the 2026-09-10 review, and AC5's "every log"
  assertion had been reading the wrong stream.
- **The container runtime's failure looks like TLS and is not.** Rancher Desktop: `rdctl shutdown`,
  `wsl --terminate rancher-desktop`, relaunch in dockerd/moby mode. `rdctl start` no-ops when the app is
  running with its backend down. `./run.ps1` now probes for this before `aspire run`.
- **A stopped container runtime produces failures, not skips** — 74 total / 52 succeeded / 22 failed / 0 skipped,
  measured. `IsContainerRuntimeAvailable()` performs no I/O, so a dead-but-configured backend reports available.
  Story 1.9 owns the fix.

### Git intelligence

Commit subjects are a sentence stating the change's thesis — no prefix, no ticket id, not conventional-commits.
Bodies run 20–50 lines: the dominant finding, what was demonstrated empirically, Lee's numbered decisions, the
patch count, plant results, the exact build and test numbers. Trailers are `Co-Authored-By: Claude Opus 5` and
`Claude-Session:`. **Committed straight to `main` — no branches, no PRs.**

One commit per workflow phase, never per file: (1) the story-context commit — story file and
`sprint-status.yaml` only; (2) the implementation commit — code, tests, `deferred-work.md`, story record;
(3) one commit per review round. Every commit carries the state transition and the verified numbers.

### Project Structure Notes

```
Yello.Domain/Accounts/          IPasswordHasher gains Verify; no Session type unless Task 1 decides otherwise
Yello.Application/
  Accounts/AuthenticateAccount/ command, handler, validator (+ tests in tests/Yello.Tests.Slices)
Yello.Infrastructure/Identity/  the Verify adapter over PasswordHasher<HashedCredential>
Yello.Host/                     the sign-in endpoint, cookie + CORS + antiforgery in the composition root
Yello.Contracts/Accounts/       SignInRequest, the route, the problem type
Yello.Client/Pages/             SignIn.razor; the session state service; the purge seam
tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/
tests/Yello.Tests.Architecture/ any new structural gate
```

**The ring table is asserted as exact equality in both directions** (`AllowedReferenceEdges.cs:23-29`). A new
project reference means editing that table, and the edit is meant to be the visible moment the dependency rule
changes. The package-prefix ban (`:233-246`) closes the hole the project rule leaves: `Yello.Domain`,
`Yello.Application`, `Yello.Contracts` and `Yello.Merge` may not reference anything beginning
`Microsoft.AspNetCore.`. `Yello.Client` is exempt.

**Consequences worth stating before they are discovered:**
- `Yello.Contracts` cannot carry `ProblemDetails` or any ASP.NET Core attribute — which is why
  `ProblemResponse` exists.
- Cookie issuance, `ClaimsPrincipal` and `AuthenticationProperties` belong in `Yello.Host` (or
  `Yello.Infrastructure` behind a `Yello.Domain` port, as `IPasswordHasher` is).
- `Yello.Client` sees only `Yello.Contracts` and `Yello.Merge`, so a failure code declared in
  `Yello.Application` is unreachable from the client. That is exactly the problem `RegistrationFields` exists to
  solve and the reason the cross-checking gate lives in the architecture suite.
- A new configuration class must be added to **both** the `Configurations/` folder and `OnModelCreating`'s
  explicit list — `ApplyConfigurationsFromAssembly` was removed because MA0182 flags reflection-only internal
  types, and `Every_entity_configuration_reaches_the_model` catches the list falling behind.

`Directory.Build.props` is imported **before** the coding-standards package's props, so restating any standard
property there silently forks the standard. There is no `Directory.Build.targets` and no `.editorconfig`, both
deliberately — the only narrowing levers are `#pragma warning disable` with a stated reason.

### Coding-standard traps reachable from this story's code

Everything comes from `Opinionated.DotNet.CodingStandards` 0.0.11: `TreatWarningsAsErrors`,
`AnalysisLevel=latest-all`, `Nullable=enable`, `EnforceCodeStyleInBuild`, `NuGetAudit` at `low` with NU1900–1904
as errors, and five analyser packages.

- **`S2339` refuses a `public const`** — every shared name is a `public static string` property with a private
  `const` behind it. `SignInFailure` codes and any new route follow that.
- **`S2094` refuses an empty class** — a new endpoint marker or resource marker needs one member.
- **`CA1848` / `CA1873` forbid `logger.LogInformation(...)`** — `[LoggerMessage]` partials only, EventIds from 1010.
- **Banned APIs reachable here:** `DateTimeOffset.Now` → `UtcNow`; `StringComparison.InvariantCulture*` →
  `Ordinal` (this bites on email comparison); `new CultureInfo(string)` → `GetCultureInfo`; all of
  `Newtonsoft.Json`; `Enum.TryParse` without `ignoreCase`.
- **`S109` magic numbers** — a session lifetime literal belongs in a named private const.
- **`CA1849` / `S6966`** — async all the way; `RunAsync`, never `Run`.
- **EF migrations, if Task 1 produces one, need a hand-editing pass** for `IDE0005`, `IDE0161`, `MA0197`,
  `CA1861` and `S138`. Do **not** add `// <auto-generated />` to silence it.

### Vocabulary

PRD §2 Glossary verbatim, in code, identifiers, namespaces, folders and UI copy.

`Account`, `Session`, `Space`, `Membership`, `Role`, `User`. **Forbidden:** `Workspace`, `Tenant`, `Org`,
`Organisation`, `Team`, and `User` where `Account` is meant. **There is no `Users` table — the entity is
`Account`**, which collides directly with Identity's `IdentityUser` naming conventions; plan for it rather
than meeting it.

**Sign-in is unauthenticated, so no Space is established, so every string on this surface says "Account", never
"User"** — `EXPERIENCE.md:18`: "**User** is an Account acting in the context of a specific Space; where no Space
is established the correct term is Account." The registration surface already obeys this.

### Testing requirements

- **xunit.v3 4.0.0 on Microsoft.Testing.Platform only.** `Microsoft.NET.Test.Sdk` is deliberately absent and
  gated absent. xunit's own `Assert` — no FluentAssertions, no Shouldly.
- Test names are sentences with underscores, one behaviour per test.
- Testcontainers SQL Server. **Never an in-memory provider, never SQLite** — neither can exercise RLS or
  `SESSION_CONTEXT`, and neither is centrally pinned, so a referencing project fails to restore.
- `Microsoft.Playwright` 1.62.0 pins the browser as well as the library. Machine setup, once:
  `pwsh tests/Yello.Tests.Client/bin/Debug/net10.0/playwright.ps1 install chromium`.
- **The fourth plant from the framework run is the one to learn from**: `DocumentLanguage` writing `data-lang`
  instead of `lang` **passed**, because `index.html` hard-codes `lang="en"` and every locale resolves to `en`,
  so the case compared `"en"` with `"en"`. The fix was in the test. Any assertion in this story that compares a
  value against a default is the same shape.

### References

- Story text and ACs — `epics.md:652-685`; Coverage Index row — `epics.md:347`; epic notes — `epics.md:427`
- FR-2 — `prd.md:145-153`, `epics.md:79`; CAP-2 — `acceptance-criteria.md:23-28`; `SPEC.md:41-43`
- Session definition — `prd.md:74`, `glossary.md:28`
- AD-7 / AR-14 (cross-origin session contract) — `ARCHITECTURE-SPINE.md:114-118`, `epics.md:202`
- AD-23 / AR-28 (uniform responses, including duration) — `ARCHITECTURE-SPINE.md:212-216`, `epics.md:216`
- AD-1 / AR-4 (authorisation is `(Account, Space)`; Identity for authentication only) —
  `ARCHITECTURE-SPINE.md:71-75`, `epics.md:192`
- AD-3 (the 403/404 line is at the Space boundary and nowhere else) — `ARCHITECTURE-SPINE.md:88-94`
- AD-4 (both surfaces, one pipeline) — `ARCHITECTURE-SPINE.md:96-100`
- AD-10 (no polling), AD-27 (expiry evaluated on read, never swept) — `ARCHITECTURE-SPINE.md:136`, `:241`
- AD-20 / AR-11 (`AccessRefusal` is 403 and Space-boundary 404 only) — `ARCHITECTURE-SPINE.md:194-198`
- AD-24 / AR-29 (the two Account-scoped surfaces) — `ARCHITECTURE-SPINE.md:218-222`, `epics.md:217`
- AD-25 / AR-30 (bounds in one registry, checked by the pipeline) — `ARCHITECTURE-SPINE.md:224-228`;
  the Sessions bound is 1.6's — `epics.md:777`, `:785`
- AR-3 (slice layout; cross-cutting invariants live in the pipeline) — `epics.md:191`, `ARCHITECTURE-SPINE.md:46`
- AR-21/AR-2 (ring rule as a build gate) — `ARCHITECTURE-SPINE.md:200-204`
- §6.1 Privacy — `prd.md:761-767`; owned partly here — `epics.md:178`
- NFR-1 — `quality-budgets.md:9-16`; NFR-2 — `:18-28`; NFR-5 — `:42-48`; NFR-6 — `:50-57`;
  NFR-8 (50 Sessions per Space) — `:76`; NFR-9 (sign-in **not** named) — `:84`
- UX-DR31 (purge, no web storage) — `epics.md:275`; UX-DR35 (session expired) — `epics.md:279`;
  UX-DR30 (purge before announce) — `epics.md:274`; UX-DR32 (refusal surfaces) — `epics.md:276`;
  UX-DR37 (voice) — `epics.md:281`; UX-DR38 (live regions) — `epics.md:282`;
  UX-DR39 (focus destinations) — `epics.md:283`; UX-DR40 (accessibility gate) — `epics.md:284`
- Session expired, the single binding passage — `EXPERIENCE.md:250`; the purge enumeration — `:164-165`;
  the retention asymmetry — `:168`
- Sign-in failed copy — `EXPERIENCE.md:272`; submit-and-wait — `:273`; the drawn surface —
  `mockups/registration.html:113-127`; danger is not for ordinary refusals — `DESIGN.md:501`;
  the spine wins over any mock — `DESIGN.md:289`, `:482`
- OAuth softness — `harness-constraints.md:51-68`, `SPEC.md:241`, `glossary.md:30`, `prd.md:839`
- UJ-4 and its cold-path edge case — `surfaces-and-journeys.md:53-64`; UJ-6 — `:77-85`
- R8 (CORS/anti-forgery, four negative INT tests, owner 1.4) — `test-design-architecture.md:218`, `:522-528`;
  `test-design-qa.md:171`, `:200`, `:487`; `YelloBMAD-handoff.md:86`
- "Active Session" undefined — `test-design-qa.md:122`, `:204`; `test-design-progress.md:470`
- Conventions, the duration method, the browser level, the container fixture — `tests/TESTING-CONVENTIONS.md`
- Ring table and Gate C — `tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs`, `RoleApiBanTests`
- Deferred-work entries this story touches — `deferred-work.md:76` (CORS, owned here), `:117` (rate limiting,
  1.6's, and this story widens it), `:111` (the culture trap), `:8` (container topology, 1.9's)
- .NET 10 CSRF middleware and cross-origin SPA guidance — Microsoft Learn,
  `security/anti-request-forgery?view=aspnetcore-10.0` and
  `blazor/security/webassembly/standalone-with-identity?view=aspnetcore-10.0`, both read 2026-09-15

### Questions for Lee — raised, not resolved

1. **What is a Session, and how long does it last?** Nothing anywhere states it — not the PRD, the spec, the
   spine, the epics or `decisions-settled.md`. My recommendation is **(a)**:
   **(a) A pure encrypted cookie ticket, absolute expiry, no sliding, no database row.** AD-27 forbids a
   scheduled sweeper and AD-10 forbids interval polling; a ticket satisfies both by construction, adds no
   table, and costs no per-request read on a free-tier database that auto-pauses after 15 minutes.
   **(b) A persisted `Session` row with `ExpiresAt` evaluated on read.** Buys explicit revocation and something
   countable for NFR-8's bound — at the price of a table, a read per request, and an expiry predicate AD-27
   constrains tightly.
   **(c) A ticket now, a row when 1.6 needs one.** Cheapest, and defers rather than decides.
   **Interim default implemented, so nothing is blocked:** (a), with an 8-hour absolute lifetime, behind a
   single named constant so changing the answer changes one line.
2. **The sign-in route and its response shapes.** No document names either. I recommend
   `POST /api/v1/sessions` → **204** on success with the cookie in the `Set-Cookie` header, and **401** with
   RFC 9457 `application/problem+json` carrying one stable `type` for *both* failure causes. A 204 keeps the
   success body empty, and one `type` makes a client-side branch impossible rather than merely forbidden.
   Sign-out would then be `DELETE /api/v1/sessions` — though **no FR anywhere requires sign-out**, so say
   whether you want it in scope at all.
3. **How does a cross-origin JSON API carry an anti-forgery token?** AR-14 requires one on every state-changing
   request; .NET 10's automatic middleware will not enforce it for JSON endpoints. I recommend the documented
   double-submit shape: a `GET /api/v1/antiforgery/token` endpoint issuing a non-`HttpOnly` `XSRF-TOKEN` cookie,
   with `AntiforgeryOptions.HeaderName = "X-XSRF-TOKEN"` and explicit validation on each state-changing
   endpoint. It is more moving parts than I would like for two endpoints; the alternative is to lean on the
   automatic middleware's CORS-origin check alone, which is weaker than AR-14 asks for.
4. **What does the sign-in surface say when a Session has expired, and where does focus go?** No string exists
   anywhere, and `EXPERIENCE.md:396-405`'s focus table does not cover this event. I recommend
   **"Your session ended. Sign in to continue."** — terse, declarative, names the Session and nothing about a
   Space or a Membership (which UJ-4's cold path and UX-DR38 both forbid) — rendered in the subtitle slot the
   mockup leaves at `registration.html:118`, with focus moved to a `role="alert"`, `tabindex="-1"` region, which
   is the only pattern the spine sanctions and which `InlineErrorRegion` already implements.
5. **Does registration's endpoint gain an anti-forgery token?** AR-14 says *every* state-changing request, and
   registration is one — but it is unauthenticated, so a token there protects against nothing a CSRF attacker
   wants. My recommendation is **yes, for uniformity**: an exception list is where this kind of rule rots, and
   the cost is one header. Your call, because it changes a shipped endpoint's contract.
6. **What does `/` route to?** `Register.razor` holds both `/` and `/register` and its comment says 1.4 and 1.7
   decide. I recommend `/` → sign-in, `/register` → registration, and 1.7 repoints `/` at the Space home once a
   context bar exists. That makes the returning user's path the default, which is the common case after week one.
7. **Do we move the dev loop to the `https` launch profile now?** AC5 requires a `Secure` cookie.
   `http://localhost` is a secure context in Chromium so `Secure` is accepted there, but Firefox and Safari
   differ, and the `//nfr-6` note in `appsettings.Development.json` already records that the swap needs
   `WithLaunchProfile("https")` plus a trusted dev certificate and was deferred to **1.10**. I recommend taking
   it here — AC5 is this story's, an unverifiable cookie attribute is worse than a config change, and NFR-6's
   encryption-in-transit clause is currently unmet in development with a password crossing loopback in
   cleartext. But it touches the deploy story's territory, so it is yours to cut.
8. **Sign-out.** Raised in passing above and worth its own answer: there is **no FR, CAP, journey or acceptance
   criterion for signing out** anywhere in the artifact set — yet UX-DR31 names "sign-out" as one of the five
   purge triggers, which presupposes it exists. Is it in this story, 1.7's (with the context bar that would hold
   the control), or genuinely absent from v1?

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (`claude-opus-5`), via the `bmad-dev-story` workflow.

### Debug Log References

Four things were found by running rather than by reading, and all four changed the implementation
or the tests. They are recorded here because in each case the first guess was wrong.

1. **ASP.NET Core's antiforgery system refuses outright to issue a token over a non-SSL request
   when `Cookie.SecurePolicy = Always`.** `GET /api/v1/antiforgery/token` answered `500` with an
   empty body, and eleven of fourteen endpoint tests went down with it. The exact message:
   *"The antiforgery system has the configuration value AntiforgeryOptions.Cookie.SecurePolicy =
   Always, but the current request is not an SSL request."* **The framework was right and the test
   was wrong.** Weakening the policy to `SameAsRequest` would have made the suite pass by removing
   `Secure` from a production cookie — AC5 traded for a green run. `HostProcess` now serves the Host
   over `https://127.0.0.1:0` with the .NET development certificate, and the client accepts it
   through a validation callback scoped to the loopback address rather than requiring
   `dotnet dev-certs https --trust`.

2. **An anti-forgery token is bound to the identity it was issued to, and the client did not
   refresh it after sign-in.** A defect in this story's own client code, found because
   `No_log_line_carries_the_password_the_cookie_or_the_outcome` counted **one** processed line
   where three sign-ins had been made: the second and third were refused with a `400` before they
   reached the endpoint, because they carried the token minted while anonymous.
   `SessionState.SignedIn()` now drops the token so the next request fetches a fresh one, and
   `SessionSeamTests.Signing_in_drops_the_token_that_was_issued_to_an_anonymous_caller` is the
   regression test. **Without this, the first state-changing action after every sign-in would have
   failed in the real product**, and no assertion in the story would have said so.

3. **An assertion I wrote claimed more than CORS can deliver, and it failed.**
   `A_sign_in_claiming_an_unknown_origin_establishes_no_session` expected no `Set-Cookie`; the
   response set one. That is correct behaviour: the caller is not a browser, it holds the
   anti-forgery cookie *and* the matching token, and explicit token validation takes precedence
   over .NET 10's automatic middleware and replaces its verdict. Forgery means causing *somebody
   else's* browser to make a request with *their* credentials, and a raw HTTP client is neither.
   The case is now `A_response_to_an_unknown_origin_carries_no_cors_grant`, which is what an origin
   policy actually guarantees, with the reasoning in its remarks so nobody re-widens it.

4. **Registration's own tests began failing, and two of them would otherwise have passed for the
   wrong reason.** AR-14's "every state-changing request" reached `POST /api/v1/accounts`
   (question 5), so story 1.3's four endpoint cases broke. Two of them —
   `A_malformed_submission_answers_with_a_stable_problem_type` and
   `An_empty_body_is_refused_as_a_malformed_submission` — assert a `400`, and a missing-token
   refusal is also a `400`: they would have gone green again while never reaching the endpoint.
   Both now assert the problem `type` as well as the status.

**Latency, measured rather than argued** (2026-09-18, this machine, end to end over HTTPS through
the real endpoint — CORS, token validation, the indexed read and the verification):
**success p50 267.4 ms / p95 285.3 ms**, **failure p50 269.0 ms / p95 304.1 ms**. Both inside
NFR-5's 500 ms p95 write budget. The measurement harness was temporary and was deleted; story 1.10
re-measures on the deploy target.

**Planted violations — ten, all caught, each by the expected test name.**
`TESTING-CONVENTIONS.md:101` requires every absence assertion to be validated against a planted
signal. Originals were held in memory and restored in a `finally`, never by `git checkout --`.

| # | Plant | Caught by |
|---|---|---|
| 1 | A Space claim added to the Session ticket | `SessionContractTests.The_session_ticket_carries_one_claim_and_it_is_the_account` |
| 2 | `localStorage.setItem` interop added to the client | `ClientStorageBanTests.No_client_code_reaches_browser_storage` |
| 3 | Announce moved before the purge | `SessionSeamTests.Every_purge_runs_before_the_session_end_is_announced` (+2 others) |
| 4 | The API handler retries after a 401 | `SessionSeamTests.A_refused_request_is_never_retried_after_the_session_ends` |
| 5 | `SlidingExpiration = true` | `SessionContractTests.The_session_expires_absolutely_and_never_slides` |
| 6 | `SameSite = Lax` | `SessionContractTests.The_session_cookie_is_http_only_secure_and_same_site_none` |
| 7 | `SetIsOriginAllowed(_ => true)` — the reflected origin AD-7 bans by name | `SessionEndpointTests.Cors_allows_exactly_the_configured_origin_and_never_a_wildcard_or_a_reflection` (+1) |
| 8 | A log line naming "no account exists for that address" on the failure path | `SessionEndpointTests.No_log_line_carries_the_password_the_cookie_or_the_outcome` |
| 9 | The anti-forgery filter removed from sign-in | `SessionEndpointTests.A_state_changing_request_without_an_antiforgery_token_is_refused` |
| 10 | `if (credential is null) return Failed;` above the verification — the AD-23 oracle | `AuthenticateAccountHandlerTests.An_unknown_address_still_reaches_the_verifier` |

Three further oracles are **permanent tests** rather than one-off runs, following story 1.3's
precedent: `PasswordVerificationDurationTests.The_method_detects_a_verifier_that_returns_early_when_there_is_no_hash`,
`ClientStorageBanTests.The_scan_detects_a_planted_storage_call`,
`SessionContractTests.The_claim_set_assertion_rejects_a_ticket_that_grew_a_space` and
`CrossOriginConfigurationTests.The_scan_does_not_match_a_key_that_is_merely_similar`.

### Completion Notes List

**The eight questions, answered — every one is a decision this story took, and every one is a line
Lee can cut.**

1. **What a Session is, and how long it lasts.** Recommendation (a), taken: **a pure encrypted
   cookie ticket, 8-hour absolute lifetime, no sliding, and no row in any table.** It does not
   slide. On a password change nothing happens — because **there is no password-change flow
   anywhere in the artifact set**, and the honest answer for a stateless ticket is that existing
   Sessions survive until the lifetime elapses; whichever story adds one inherits the question.
   **A Session is not represented in the database at all.** AD-27 forbids a scheduled sweeper and
   AD-10 forbids interval polling; a ticket satisfies both by construction, adds no table and costs
   no per-request read. AC3's word "invalidated" does not by itself require a server-side record:
   Membership removal revokes *access*, and 1.5's per-request resolution makes that immediate. All
   of it is in `SessionCookiePolicy`, behind one named constant.
2. **Route and shapes.** `POST /api/v1/sessions` → **204** with the cookie; **401** with one stable
   `type` for *every* failure cause and no `errors` extension. Malformed → **400** with
   `urn:yello:problem:invalid-sign-in`. Plus `GET /api/v1/sessions/current` → **204/401**, which I
   added and which is the one place AC3 is observable at all — see the limits below.
3. **Anti-forgery across two origins.** The documented double-submit, with one change from the
   story's recommendation: the request token is returned **in the response body** from
   `GET /api/v1/antiforgery/token` and held in a field, rather than in a non-`HttpOnly` cookie the
   page reads back. Both cookie halves stay `HttpOnly`, and nothing needs `document.cookie` interop
   — which is also what keeps AC5's web-storage ban from resting on restraint.
4. **The expiry copy and its focus destination.** `"Your Session ended. Sign in to continue."`,
   rendered through `InlineErrorRegion` — `role="alert"`, `tabindex="-1"`, focus moved to it, which
   is the only pattern the spine sanctions (`EXPERIENCE.md:398`) and the one that component already
   implements. It names the Session and nothing else.
5. **Registration gains anti-forgery: yes.** AR-14 says *every*. It protects against nothing on an
   unauthenticated endpoint; an exception list is where that kind of rule rots, and the cost is one
   header. **This changes a shipped endpoint's contract** and broke four of story 1.3's tests, which
   is the loudest possible way to raise it for your decision.
6. **`/` routes to sign-in**, `/register` to registration, and 1.7 repoints `/` at the Space home.
   The sign-in surface carries a link to registration — without it UJ-1 would be reachable only by
   typing a path, which "decide what `/` becomes" cannot reasonably mean.
7. **The `https` dev loop: taken.** Both projects run on their `https` launch profiles. This was
   not finally a free choice: AC5 requires a `Secure` cookie, and ASP.NET Core's antiforgery system
   refuses to issue a token at all over a non-SSL request when that policy is set. It needs
   `dotnet dev-certs https --trust` once if a browser complains.
8. **Sign-out: not built.** No FR, CAP, journey or acceptance criterion asks for one; UX-DR31
   presupposes it. The story's own header assigns the purge seam's extension "to Space switch and
   sign-out" to **1.7**, which also builds the bar such a control would live in. The seam it needs
   exists.

---

**What this story can and cannot honestly claim. Three of six ACs describe behaviour over systems
that do not exist yet, and each is named here rather than implied covered.**

- **AC2 — "a Space change does not touch the Session" is true by construction and is not tested as
  a switch.** Nothing on the Session stores the active Space: the ticket carries exactly one claim,
  asserted as an *exact set* by `SessionContractTests`. There is therefore nothing for a Space
  change to alter. **What is not asserted is an actual switch**, because no Space switcher exists
  until 1.7 and no Space-scoped endpoint until 1.5.
- **AC3's purge clause is a SEAM, not coverage.** There is no replica, no cached Board, no filter
  state and no Space-scoped client state of any kind. A test asserting "everything Space-scoped is
  purged" would pass against a system with nothing to purge. What `SessionSeamTests` asserts is what
  the seam *does*: registrations run, they run before the announcement (UX-DR30), a failing one does
  not strand the rest, and the announcement is wired (read from the client's IL, so deleting it
  fails the build). Stories 1.7 and epic 2 extend it.
- **AC3's "grants access to nothing, including Spaces the Account still holds a Membership in" is
  demonstrated against the only endpoint there is.** `An_expired_session_grants_nothing` uses a real
  expired ticket — the Host is started with a negative lifetime so every ticket is born past its
  expiry, which is how a real expiry is observed with `Task.Delay` banned. But **there is no
  Space-scoped surface for it to fail to grant access to until story 1.5**, so the Membership clause
  is not proved.
- **AC6 is the rule and the absence of a path that would break it, not a demonstration.** There is
  no editor and no unsent input until epic 7. What is asserted is that a refused request is returned
  to its caller and **never retried** (planted: a retry fails the test), and that nothing stores a
  request to re-send.
- **AC5's web-storage ban is real and is gated twice** — compiled IL for the C# and Razor, and the
  `wwwroot` assets for what the browser loads directly, neither of which sees the other. Both
  validated against plants.
- **No browser has driven sign-in end to end.** `tests/Yello.Tests.Client` sees only
  `Yello.Contracts`, runs no container and has no Host. What is unverified as a result: that a
  browser accepts the `SameSite=None; Secure` cookie across the two development origins, that the
  header survives the preflight, and that the surface renders and submits. Every one of those is
  asserted at the level below a browser. Recorded in `deferred-work.md`.
- **`aspire run` has not been run.** The `https` configuration is proved against a real Host process
  with a real certificate; the orchestration itself is not. Recorded in `deferred-work.md` as a
  five-minute confirmation rather than a story.

---

**What was deliberately not built, and why.**

- **No rate limit.** The exposure doubled — two unauthenticated 220,000-iteration PBKDF2 endpoints
  now — and AR-3 forbids a slice bolting a limiter into itself. `deferred-work.md`'s entry is
  widened to name both endpoints, with the note that an anti-forgery token is **not** a rate limit:
  it costs an attacker one extra request in total, not one per attempt.
- **No NFR-8 bound.** 1.6 owns it. This story surfaces the contradiction underneath it rather than
  resolving it: **a Session carries no Space, so "50 concurrent active Sessions per Space" cannot be
  a property of a Session and cannot be counted at sign-in.** Recorded against 1.6 with the
  reasoning, and with the instruction not to put a `SpaceId` on the Session to make the count easy.
- **No Identity builder.** No `AddIdentity` (fails Gate C outright), no `AddIdentityCore`, no
  `MapIdentityApi` (forks Account creation away from AD-22 and answers differently for a known and
  an unknown address), and not `AddIdentityCookies` either — that brings a security-stamp validator
  whose `ValidationInterval` is a re-read of the user store on a timer, which is the shape AD-10
  bans. Plain cookie authentication is the same ticket without any of it, so "the whole of Identity
  that Yello wires" is still one hasher.
- **No new package.** Cookie authentication, CORS and anti-forgery all live in the
  `Microsoft.AspNetCore.App` shared framework that `Yello.Host` already has, so
  `Directory.Packages.props` and `PackageVersionPinTests`' exact expected set are untouched.
- **No `AddAuthorization`, no `UseAuthorization`, no policy and no `[Authorize]`.** Authentication is
  wired; authorisation is 1.5's and 1.6's, and AD-1 makes every authorisation decision a per-request
  `(Account, Space)` resolution nothing here can perform. The current-session endpoint reads
  `HttpContext.User` and answers 401 itself.

**One correction to the story's own framing, for the record.** The story recommends implementing
AD-23's uniformity "in the endpoint, not inside the password comparator". Both halves were needed
and they are different halves: the *response* uniformity is in the endpoint, over the slice's single
`null` — and the *duration* uniformity has to be in the comparator, because that is the only place
the work happens. `IPasswordHasher.Verify` therefore takes a **nullable** hash and does a full
verification against a dummy hash when there is none, which is what removes the branch a caller
would otherwise have to write.

**Small pre-existing finding, fixed rather than recorded.** `InlineErrorRegion.razor:23` emits
`class="form-status__heading"` and no such rule existed in any stylesheet — story 1.4 is the first
story to reuse that component. It now has one.

### File List

**Production — new**

- `Yello.Domain/Accounts/IAccountCredentialStore.cs`
- `Yello.Application/Accounts/AuthenticateAccount/AuthenticateAccountCommand.cs`
- `Yello.Application/Accounts/AuthenticateAccount/AuthenticateAccountValidator.cs`
- `Yello.Application/Accounts/AuthenticateAccount/AuthenticateAccountHandler.cs`
- `Yello.Infrastructure/Persistence/AccountCredentialStore.cs`
- `Yello.Contracts/Accounts/SessionRoutes.cs`
- `Yello.Contracts/Accounts/SignInRequest.cs`
- `Yello.Contracts/Accounts/Antiforgery.cs`
- `Yello.Host/Sessions/SessionCookiePolicy.cs`
- `Yello.Host/Sessions/SessionPrincipal.cs`
- `Yello.Host/Sessions/CrossOriginPolicy.cs`
- `Yello.Host/Sessions/AntiforgeryRequirement.cs`
- `Yello.Host/Endpoints/SessionEndpoints.cs`
- `Yello.Host/SessionLog.cs`
- `Yello.Client/Sessions/SessionState.cs`
- `Yello.Client/Sessions/YelloApiHandler.cs`
- `Yello.Client/Sessions/AntiforgeryTokenSource.cs`
- `Yello.Client/Pages/SignIn.razor`
- `Yello.Client/Pages/SignInPhase.cs`
- `Yello.Client/Pages/SignInFields.cs`
- `Yello.Client/Pages/ClientRoutes.cs`

**Production — modified**

- `Yello.Domain/Accounts/IPasswordHasher.cs` — gains `Verify` and `PasswordVerification`; the
  doc comment claiming a deliberate absence is replaced.
- `Yello.Infrastructure/Identity/IdentityPasswordHasher.cs` — `Verify`, the dummy hash, and the
  corrected "whole of Identity" paragraph.
- `Yello.Infrastructure/InfrastructureServices.cs` — registers the credential store; hand-off
  comment updated.
- `Yello.Contracts/ProblemTypes.cs` — three new stable types.
- `Yello.Host/Program.cs` — authentication, CORS, anti-forgery, the route table.
- `Yello.Client/Program.cs` — the session seam, the API handler, the announcement wiring.
- `Yello.Client/Pages/Register.razor` — drops `@page "/"`, carries an anti-forgery token.
- `Yello.Client/Pages/RegistrationPhase.cs` — hand-off comment updated.
- `Yello.Client/Layout/MainLayout.razor` — hand-off comment updated; sign-out's absence recorded.
- `Yello.Client/Localisation/ClientCopy.resx` — eight sign-in strings.
- `Yello.Client/wwwroot/css/components.css` — `.auth-panel__aside`, and `.form-status__heading`
  which had no rule at all.
- `Yello.Client/wwwroot/appsettings.Development.json` — the Host address moves to `https`.
- `Yello.AppHost/Program.cs` — both projects on `https`, and `ClientOrigin` injected.

**Tests — new**

- `tests/Yello.Tests.Slices/WireSession.cs`
- `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/AuthenticateAccountValidatorTests.cs`
- `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/AuthenticateAccountHandlerTests.cs`
- `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/PasswordVerificationDurationTests.cs`
- `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/SessionEndpointTests.cs`
- `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/RehashIntegrationTests.cs`
- `tests/Yello.Tests.Architecture/SessionContractTests.cs`
- `tests/Yello.Tests.Architecture/SessionSeamTests.cs`
- `tests/Yello.Tests.Architecture/ClientStorageBanTests.cs`
- `tests/Yello.Tests.Architecture/CrossOriginConfigurationTests.cs`

**Tests — modified**

- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/HostProcess.cs` — HTTPS, explicit cookies,
  extra configuration.
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegisterAccountEndpointTests.cs` — carries a
  token; two cases now assert the problem `type` as well as the status.
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegisterAccountHandlerTests.cs`,
  `RegistrationDurationTests.cs` — their fakes implement `Verify`.
- `tests/Yello.Tests.Architecture/LocalisationGateTests.cs` — the sign-in failure-code gate and the
  sign-in copy gate.

**Records**

- `tests/TESTING-CONVENTIONS.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/1-4-authenticate-and-hold-a-session-across-every-space.md`

### Change Log

| Date | Change |
|---|---|
| 2026-09-15 | Story context created; status `backlog` → `ready-for-dev`. Eight questions raised for Lee, each with a recommendation and an interim default. |
| 2026-09-18 | Implemented. All eight questions answered as decisions; ten planted violations run and caught; sign-in measured at p95 285 ms against NFR-5's 500 ms. Status `ready-for-dev` → `in-progress` → `review`. |
