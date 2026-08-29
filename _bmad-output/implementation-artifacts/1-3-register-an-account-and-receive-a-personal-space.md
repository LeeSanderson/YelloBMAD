---
baseline_commit: 73805cb699b4d3c1868ddc4d9fb41cf4b377f21d
---

# Story 1.3: Register an Account and receive a Personal Space

Status: review

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-3-register-an-account-and-receive-a-personal-space`
Requirements owned: **FR-1, FR-4, AR-27, AR-28, NFR-6, FS-NFR-1** (`epics.md:328`)
Depends on: **story 1.1** (done) — the eight-project ring layout, the four gating suites, the pinned stack, the build gates.
**story 1.2** (done) — the token layer `tokens.css` / `base.css`. It shipped **no components**; this story builds the first ones.
Seeds, does not own: the Space default Status set Todo / In Progress / Done (FR-24, configured in Epic 6, story 6.3).

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As someone who has just found Yello,
I want to create an Account with my email address and a password and immediately have a Space of my own,
So that I can start holding work without making a single structural decision first.

## Acceptance Criteria

Reproduced verbatim from `epics.md:599-632` (`status: final`). Seven blocks, numbered by position per the
convention `epics.md:8` establishes ("Acceptance Criteria, fifth block (AC5)").

**AC1 — one Account, one Space, one Owner Membership, one transaction**

**Given** no Account exists for an address
**When** registration completes for it
**Then** exactly one `Account` exists for that address, exactly one `Space` exists owned by it, and exactly one `Membership` at Role `Owner` joins the two — all committed in a single transaction by a single slice
**And** the Space carries the default Status set Todo / In Progress / Done and no Projects

**AC2 — the duplicate path is indistinguishable, including in duration**

**Given** an Account already exists for that address
**When** registration is attempted for it again
**Then** the response is identical to a successful new registration in status, body and shape
**And** identical in duration, because the password hash is performed anyway rather than skipped
**And** no second Account and no second Space are created

**AC3 — a failure is a failed transaction, not a repairable state**

**Given** a registration attempt
**When** the transaction cannot complete
**Then** no Account is left holding zero Spaces or two — this is a failed transaction, not a repairable state

**AC4 — the provisioned Space is an ordinary Space**

**Given** the provisioned Space
**When** its attributes are compared with a Space created by any other route
**Then** no attribute distinguishes it, and it is renameable, shareable, transferable and deletable on the same terms as any other Space

**AC5 — the password is never observable, and its work factor is tunable**

**Given** a password submitted at registration
**When** the datastore, every log, every error body and every API response are inspected
**Then** the password appears in none of them
**And** it is stored only under a deliberately slow one-way function whose work factor is tunable without re-registering existing Accounts

**AC6 — the registration surface states its wait**

**Given** the registration surface
**When** it is submitted
**Then** the in-flight condition is stated, resubmission is disabled, and completion is announced — this being one of only two submit-and-wait interactions in a product with no save button

**AC7 — nothing hardens what OAuth will break**

**Given** the Account entity and the registration slice
**When** they are reviewed
**Then** nothing assumes a password exists on every Account, and nothing binds identity to the email address in a way that cannot be revisited
**And** this is explicit, because PRD §9.2 requires FR-1, FR-2 and NFR-6 to absorb deferred OAuth sign-in without redesign

## Tasks / Subtasks

- [x] **Task 1 — The schema: first entities, first DbContext, first migration** (AC: 1, 3, 4)
  - [x] Add `Account`, `Space`, `Membership`, `StatusDefinition` and the `Role` enum to `Yello.Domain`. No EF Core attribute, no EF Core type, no ASP.NET Core type — Gate B scans bytecode and fails the build on either.
  - [x] `Guid` ids generated application-side via EF Core's `SequentialGuidValueGenerator`. **Not** `Guid.CreateVersion7()`, never sequential integers (AR-34, `epics.md:204`; `ARCHITECTURE-SPINE.md:268`).
  - [x] All timestamps `DateTimeOffset` in UTC. `DateTime` is banned; `DateTimeOffset.Now` is a banned API at build (use `UtcNow`).
  - [x] Add the `DbContext`, all EF configuration and the migration to `Yello.Infrastructure`. Add `PackageReference` (no `Version` attribute) for `Microsoft.EntityFrameworkCore`, `.SqlServer`, `.Design` and `Microsoft.AspNetCore.Identity.EntityFrameworkCore` — **already pinned at 10.0.11 in `Directory.Packages.props`; no `PackageVersionPinTests` edit is needed.**
  - [x] `Membership` and `StatusDefinition` carry a **non-nullable `SpaceId`** (AD-2, `ARCHITECTURE-SPINE.md:81`).
  - [x] Filtered unique index `Membership(SpaceId) WHERE Role = Owner` (AD-5, `:106`). Invariant test: no Space ever holds zero or two Owner Memberships (AR-12, `epics.md:182`).
  - [x] Email uniqueness enforced by index, but **never surfaced as a distinguishing response** — see Task 4.
  - [x] Write the RLS policies for the Space-scoped tables this story creates, in the same migration, plus the schema test asserting them. Rationale and the alternative are in *The isolation seam* below.
  - [x] Add `dotnet-ef` to `.config/dotnet-tools.json` (the tool-manifest gate checks only `aspire.cli`, so this is safe).
  - [x] **No migration is applied at startup** (AR-36, `epics.md:206`). Story 1.10 applies them as an explicit deploy step.

- [x] **Task 2 — The `RegisterAccount` slice: one implementation, one transaction** (AC: 1, 3, 4)
  - [x] `Yello.Application/Accounts/RegisterAccount/` — command, handler, validator and its tests in one folder (AR-3, `epics.md:173`).
  - [x] The handler writes `Account` + `Space` + `Membership(Owner)` + three `StatusDefinition` rows **in one transaction**. Registration completing with anything other than exactly one owned Space is a failed transaction, not a repairable state (AD-22, `ARCHITECTURE-SPINE.md:210`).
  - [x] **Build it to be called.** Story 4.3 delegates to this exact slice for registration-while-accepting-an-Invitation and adds the invited-Space Membership as a *separate, additional* Membership (`epics.md:1703-1706`). It must not provision twice, and 4.3 must not need a second provisioning path.
  - [x] `Yello.Application` may not reference ASP.NET Core types. Identity's hasher reaches the slice through a port declared in `Yello.Domain` and implemented in `Yello.Infrastructure`.
  - [x] Seed exactly Todo / In Progress / Done as identity-bearing `StatusDefinition` rows with stable ids that survive rename (AR-23, `epics.md:193`). Do **not** materialise any per-Project effective Status set; no table stores one.
  - [x] Re-implementing authorisation, Space resolution, refusal recording, idempotency or a bound check **inside the slice is a defect** (AR-3). No NFR-8 bound applies to this story — the registry is built in 1.6 and assigns Spaces-per-Account to 3.1.

- [x] **Task 3 — Identity, wired for authentication only** (AC: 5, 7)
  - [x] Configure ASP.NET Core Identity for the Account store, password hashing and nothing else (AD-1, `ARCHITECTURE-SPINE.md:75`).
  - [x] **Gate C is a live IL scan over all 14 assemblies and will fail the build on:** `IdentityRole` (any arity), `IdentityUserRole`, `IdentityRoleClaim`, `RoleManager<>`, `IRoleStore<>`, `RoleStore<>`, `AddRoles<>()`, `AddRoleManager<>()`, `[Authorize(Roles=…)]`, `AuthorizationPolicyBuilder.RequireRole`, `ClaimsPrincipal.IsInRole`, `ClaimTypes.Role` however read, and `UserManager<>`'s role surface (`AddToRoleAsync`, `GetRolesAsync`, `IsInRoleAsync`, …). `ClaimsPrincipal` and `UserManager<>` are permitted *types*; the bans are on members.
  - [x] Yello's `Role` is a column on `Membership`. It is never an Identity role, never a claim, never a cookie value.
  - [x] Choose and record the NFR-6 work factor — see Task 8.
  - [x] Nothing may assume a password exists on every Account (AC7; `harness-constraints.md:64`). The password is a nullable credential on the Account, not a required field of identity.

- [x] **Task 4 — The uniform response, and the endpoint** (AC: 2)
  - [x] `POST` a registration endpoint from `Yello.Host` as a **Minimal API** (not MVC). It is not Space-scoped, so it carries no `{spaceId}` segment; AR-9's gate lists Task/Project/Label/StatusDefinition and does not reach it.
  - [x] The duplicate path performs the password hash it would otherwise skip, then returns without creating anything (AD-23, `ARCHITECTURE-SPINE.md:216`).
  - [x] Identical **status, body, shape and duration** for a known and an unknown address. **A `409 Conflict` on duplicate email is the exact defect AD-23 exists to prevent** (`:215`).
  - [x] Errors are RFC 9457 `application/problem+json` with a stable machine-readable `type`; prose is never the contract (AR-34).
  - [x] Do not log the address in a way that distinguishes the two paths. Structured logs to stdout, never carrying a password (AR-34; FS-NFR-1).
  - [x] `[LoggerMessage]` source-generated partials only — CA1848/CA1873 are errors. `Yello.Host/StartupLog.cs` is the template; EventIds 1000–1007 are taken.

- [x] **Task 5 — The registration surface** (AC: 6)
  - [x] `Yello.Client` has no `<Router>`, no layout, no pages and no components. This story introduces them, plus the `@using` entries in `_Imports.razor`.
  - [x] Build the first components from the token layer: a form field, a primary button and an inline error region. Story 1.2 shipped tokens only — `base.css` states "It builds no component."
  - [x] Two fields: email and password. No plan picker, no team-size question, no confirm-password, no terms checkbox, no CAPTCHA, no onboarding — every one of these is ruled out by UJ-1's climax (`EXPERIENCE.md:477`) and the mockup's negative-constraints block.
  - [x] **On submit: state the in-flight condition, disable resubmission, announce completion** (`EXPERIENCE.md:273`). Never a spinner over the whole surface; no progress percentage; no celebration.
  - [x] The wait is deliberately long by design. Motion must not cover it (`DESIGN.md:508`).
  - [x] Repoint `Yello.Client/Program.cs`'s `HttpClient` BaseAddress at the Aspire-injected Host address and update its now-stale comment. `Yello.Client` **cannot** reference `Yello.Host` — the ring table forbids the edge.
  - [x] If any `*.razor.css` is added, `index.html` must gain a `<link>` whose href contains `.styles.css`, or `Every_stylesheet_is_linked_by_the_host_page` fails.
  - [x] Validation errors use `--border-hairline`, **never `--danger`** — danger is reserved for the genuinely irreversible (`DESIGN.md:501`). Precedent already shipped at `base.css:300-306`.

- [x] **Task 6 — Localisation, because the copy gate leaves no alternative** (AC: 6)
  - [x] `No_user_visible_string_literal_appears_in_a_component` fails the build on **any word of 2+ letters that is not `Yello`** in a `.razor` text node or in `title`/`alt`/`placeholder`/`label`/`aria-label`/`aria-description`/`aria-placeholder`/`aria-roledescription`/`aria-valuetext`/`abbr`. A single word — `Email`, `Password` — is a build failure.
  - [x] The recognised idiom is `@Localizer["Key"]` via an injected `IStringLocalizer`. Build the resource system: `.resx`, registration, and a culture provider.
  - [x] Copy resources hold **sentence case**. Uppercase comes from `text-transform`, never from the string — a resource holding `VIEWER` makes the accessible name "V-I-E-W-E-R" under JAWS (`DESIGN.md:396-400`).
  - [x] `deferred-work.md:32` names "the first story that introduces localisation resources and a culture provider" as the owner of the hard-coded `<html lang="en">` and the inert 26-locale casing exclusion in `base.css`. **This story is that story.** Add the assertion that the exclusion is no longer inert, and update or close the ledger entry.

- [x] **Task 7 — Accessibility: registration is a named NFR-9 gated flow** (AC: 6)
  - [x] WCAG 2.1 AA. Registration is named **first** among the five gated flows (`quality-budgets.md:84`).
  - [x] Real `<label for>` association. Focus order follows reading order. `:focus-visible` already draws the ring globally from `base.css:194-197` — never remove it, never set `outline-offset: 0`, never draw it inset.
  - [x] The error region is `role="alert"` **and focusable (`tabindex="-1"`) with focus moved to it**. A bare `aria-live` region with no focus move is the exact failure a critical review finding rejected (`review-accessibility.md:62`).
  - [x] Must work at a **320px CSS viewport** — that is the 1.4.10 audit condition (`EXPERIENCE.md:421`). No layout sized to an English string; German and Finnish run 30–40% longer. The mockup's fixed `330px` form violates this and is superseded.
  - [x] Survive the 1.4.12 text-spacing overrides and 200% **text-only** zoom. `deferred-work.md:14` and `:28` and `:46` name "the first story with a rendered surface" as owner of the measurement half — all three are blocked on **B5** (the browser-test binding), which is still undecided. Record that this story makes them reachable rather than letting them pass silently to 1.4.
  - [x] Interactive target floor of 24px is already applied to `input`, `button`, `select`, `textarea` in both axes by `base.css:246-266`. Do not restate it.

- [x] **Task 8 — Choose, measure and record the password work factor** (AC: 5)
  - [x] Keep IdentityV3: PBKDF2, HMAC-SHA512, 128-bit salt, 256-bit subkey. Set `PasswordHasherOptions.IterationCount` explicitly rather than inheriting the default.
  - [x] **Measure before choosing.** Registration is a write under NFR-5's 500 ms p95 budget *and* is required to be deliberately slow. Pick the highest work factor whose server-side p95 leaves headroom under 500 ms on the deploy target, with the framework default of 100,000 as the floor and OWASP's current SHA512 figure of 220,000 as the target.
  - [x] Record the chosen number, the measurement, and the hardware it was measured on, in the Dev Agent Record.
  - [x] Prove tunability without re-registering: the iteration count is embedded in each stored hash, and `VerifyHashedPassword` returns `SuccessRehashNeeded` when `embeddedIterCount < _iterCount`. Raising the number never invalidates an existing hash. Assert this directly.

- [x] **Task 9 — The B3 duration-indistinguishability method** (AC: 2)
  - [x] `test-design-architecture.md:113` assigns this to **stories 1.3 and 1.6**. Write the method once, covering both AD-3 and AD-23, and put it where 1.6 and 1.9 can reuse it.
  - [x] It needs: sample size, statistic, tolerance and measurement point. A single-sample assertion is one draw from two distributions and detects nothing.
  - [x] **Validate it by planting the oracle**: skip the hash for an unknown address and confirm the test fails by name. An absence assertion not validated against a planted signal is not a test (`TESTING-CONVENTIONS.md:93`).
  - [x] `MAXDOP = 1` plus a single replica make variance unusually low here, so this is more tractable than most places — but only if written down once instead of improvised twice (`test-design-architecture.md:312`).

- [x] **Task 10 — Tests** (AC: all)
  - [x] Slice and integration tests in `tests/Yello.Tests.Slices`, mirroring `Yello.Application`'s `{Area}/{UseCase}/` structure. **Do not create a new test project** — it breaks six gates and requires two visible architecture edits.
  - [x] Testcontainers SQL Server via `SqlServerContainerFixture`. **Never an in-memory provider, never SQLite** — neither can exercise RLS, and neither is centrally pinned, so a referencing project fails to restore.
  - [x] **Randomise the email address in every test.** FR-1's uniqueness makes a shared literal a cross-suite flake (`TESTING-CONVENTIONS.md:85`).
  - [x] Cleanup by transaction rollback or container disposal, never by delete statements — those would need an RLS session context to see the rows they are removing, so a cleanup that "works" may be evidence isolation is broken.
  - [x] Every absence assertion in this story (password absent from datastore/logs/errors/responses; no second Account; no distinguishing attribute) must be proved against a planted violation, with the result recorded.
  - [x] Traits: `Suite`, `Priority`, `Requirement` (cite the `AR` id **and** the `AD` id), and `Assumption` naming the source document (`PRD-12-1`, `PRD-12-6`).
  - [x] If a test is added to `Yello.Tests.Isolation`, `--ignore-exit-code 8` **must** come out of that csproj in the same change — `Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code` is gated both ways. Recommended: keep this story's cases in `Slices` and leave the both-surfaces isolation suite to 1.9.
  - [x] No coverage threshold. Do not invent one.

- [x] **Task 11 — Housekeeping the prior stories left**
  - [x] Update the now-stale comments in `Yello.Client/App.razor`, `wwwroot/index.html`, `Yello.Client/Program.cs`, `Yello.Infrastructure/AssemblyMarker.cs` (which says in so many words that story 1.3 creates the first three tables) and `Yello.Client/AssemblyMarker.cs`.
  - [x] Do **not** add review narration to source files. `deferred-work.md:44` records that as an open finding owned by **Lee, once, for the project** — not by the next story to touch those files.
  - [x] `git diff --stat` against `73805cb` before calling the File List complete.

## Dev Notes

### Scope boundary — what this story does NOT build

Story 1.3 sits third in a strictly linear epic (`implementation-readiness-report-2026-08-22.md:961`). The
adjacent stories own things registration appears to need, and building them here is scope creep that
later stories will have to unpick.

| Not this story | Owner | Citation |
|---|---|---|
| Sign-in, Sessions, cookies, CORS, anti-forgery | **1.4** | `epics.md:634-667` |
| `ActiveSpaceContext`, per-request session context, EF global query filters, `MAXDOP`, the pooled-connection case | **1.5** | `epics.md:669-708` |
| Refusal recording, the 403/404 line, the problem+json machinery, the NFR-8 bound registry | **1.6** | `epics.md:710-772` |
| The context bar, the Role chip, the Space switcher, `AccountScopedContext` | **1.7** | `epics.md:774-818` |
| API Tokens, Account settings | **1.8** | `epics.md:820-857` |
| The both-surfaces isolation suite (SM-1) | **1.9** | `epics.md:859-897` |
| Applying migrations, CI, deployment, the §6.4 data-protection gate | **1.10** | `epics.md:899-949` |
| Creating further Spaces, renaming a Space, the 50-Space bound | **Epic 3** | `epics.md:1426-1428` |
| Invitations, and registration-as-acceptance | **Epic 4** | `epics.md:1703-1706` |
| Configuring the Status set (the editor) | **Epic 6** | `epics.md:2216-2219` |

**The consequence that matters most:** 1.4 owns authentication, so **this story does not sign the new
Account in and does not navigate anywhere.** AC1–AC7 say nothing about a Session, and 1.7 owns the
context bar the user would arrive at. UJ-1's "lands directly in a Space already named 'Ravi's Space'"
is the *epic's* outcome, realised once 1.4 and 1.7 land — not this story's. AC6's obligation ends at
"completion is announced". This is question 3 below.

### What stories 1.1 and 1.2 hand over

**Verified against the repo at `73805cb`, not against the story records.** `dotnet build Yello.slnx` is clean
at 0 warnings; `dotnet test Yello.slnx` is 83 total / 80 passed, the 3 failures being container-dependent
Slices tests with the Rancher Desktop backend stopped.

- **Eight production projects and six test projects exist.** `Yello.Domain`, `Yello.Application`,
  `Yello.Infrastructure`, `Yello.Contracts` and `Yello.Merge` each contain **exactly one file,
  `AssemblyMarker.cs`**. There is no `src/`, no `.editorconfig`, no `.github/`, and no CI of any kind.
- **There is no `DbContext`, no entity, no migration and no DI registration anywhere.** `Yello.Host/Program.cs`
  is a `WebApplication`, a startup connectivity check and `RunAsync()` — no endpoints, no authentication,
  no `AddDbContext`.
- **EF Core 10.0.11, `.SqlServer`, `.Design` and `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.11
  are already pinned** and labelled in `Directory.Packages.props` as "first referenced by story 1.3".
  Reference them with **no `Version` attribute**; CPM supplies it.
- **The token layer is `Yello.Client/wwwroot/css/tokens.css` and `base.css`** — 30 colour names, 8 type roles,
  the 3px spacing scale, the 24px target floor, 1.5px hairlines, radii, motion. Hand-written CSS custom
  properties. **No npm, no bundler, no preprocessor, no webfont, no external request. Do not introduce one.**
- **Dark is canonical.** The unsuffixed token carries the dark value; the `-light` sibling may be read
  **only** inside the `THEME BOUNDARY BEGIN`/`END` block in `tokens.css`. A component reaching for a
  `-light` token is a defect and a gate failure.
- **`--danger` and `--danger-on` currently have no consumer.** Do not become their first one; validation
  errors take `--border-hairline`.

### The three decisions this story must make and record

The test-design artifacts assign these to story 1.3 by name. They are deliverables, not open questions.

1. **The NFR-6 password work factor.** `test-design-qa.md:486`: "Choose the NFR-6 password work factor |
   Story 1.3 | Epic 1 | Currently unspecified." NFR-6 says the work factor "is the architecture's call,
   not this document's" — and the architecture never made it. Task 8.
2. **The B3 duration-indistinguishability method.** `test-design-architecture.md:113`: "Owner: stories 1.3
   and 1.6." It blocks P0 test I-7. Task 9.
3. **Email normalisation and casing.** Nothing in the SPEC, the companions, the PRD, the addendum, the
   spine, the epics or the readiness report states whether email comparison is case-insensitive or how the
   uniqueness index is collated. ASP.NET Core Identity's default `NormalizedEmail` gives case-insensitive
   comparison — but that is an inherited framework default, not a recorded decision. **Record it as a
   decision**, and keep it soft: `harness-constraints.md:63` marks email-as-identity as the load-bearing
   assumption OAuth will break.

### The one decision this story cannot make on its own

**The Personal Space's name is derived from a field nothing collects.** This is a genuine contradiction
across five documents, not an ambiguity to resolve by reading harder.

Three sources require a display name: `prd.md:164` and `SPEC.md:268` and `acceptance-criteria.md:49`
("named from the Account's display name, e.g. \"Ravi's Space\""); `surfaces-and-journeys.md:24` and
`EXPERIENCE.md:473` both show the literal string "Ravi's Space"; and UX-DR34 (`epics.md:259`) needs a
display name for **every** Membership rendering from Epic 4 onward, with avatar initials derived from it
(`EXPERIENCE.md:210`).

Five sources fix registration at exactly two fields: FR-1 (`prd.md:119`), `harness-constraints.md:64`,
`EXPERIENCE.md:472`, this story's own user story (`epics.md:596`), and the mockup, which has exactly two
controls. **Nothing anywhere defines a display-name attribute on `Account`.** And no acceptance criterion
in `epics.md` covers the naming half — a grep for `display name` / `Ravi's Space` / `named from` returns
only `epics.md:618`, the *renameable* half.

This is PRD §12 assumption #1, one of the thirteen the readiness report records as hardened into
acceptance criteria without ever being confirmed (issue 5, `:1240-1247`).

**Do not let this block implementation.** Put the naming behind a single choke point — one function in
`Yello.Domain` returning a localised, resource-formatted name — so whichever answer comes back changes
one function and one resource string, not the slice, the schema or the tests. Implement the interim
default in question 1 below and flag it in the Completion Notes.

### The atomic transaction, and where the session context comes from

AD-22 is the load-bearing decision (`ARCHITECTURE-SPINE.md:210`): "Exactly one slice creates an Account,
and it provisions the Personal Space and its Owner Membership in the same transaction… Registration
completing with anything other than exactly one owned Space is a failed transaction, not a repairable
state."

There is a real seam here that no document resolves. AD-2 requires
`sp_set_session_context 'SpaceId', …, @read_only = 1` "at the start of **every** unit of work, from
`ActiveSpaceContext` and never from a client-supplied value". Registration is unauthenticated, has no
`ActiveSpaceContext`, and creates the very Space whose id the context would need.

**It resolves cleanly, and the resolution is worth stating because it is not obvious:** ids are generated
**application-side** by `SequentialGuidValueGenerator`, not by the database. So the slice generates the
`SpaceId` first, sets the session context to it inside the transaction, and then inserts. The value is
server-generated, never client-supplied, so AD-2's prohibition is honoured rather than bypassed.

**Never** resolve this by disabling a policy, opening a second connection or inventing a bypass — AD-24
(`:222`) names precisely that as the failure it exists to prevent, "and that bypass then spreading".

### The isolation seam — why this story writes the policies it creates

`implementation-readiness-report-2026-08-22.md:992` maps `Account`, `Space` and `Membership` to
"1.3 Register", and `:1003` states: "Every one carries a schema test asserting the RLS policy in the same
story." AD-2 (`:86`) is blunt: "A Space-scoped table without an RLS policy fails the schema test." And the
Migrations convention (`:275`) says migrations *include* the RLS policies.

So this story creates its tables **with** their policies and **with** the schema test asserting them.
`Yello.Tests.Shared` is already the mechanism for asserting a migrated schema; `TESTING-CONVENTIONS.md:137`
notes story 1.1 wrote no schema assertion "because there is no schema". **This story creates the first schema.**

What still belongs to 1.5: `ActiveSpaceContext` and the per-request wiring, the EF global query filters
that form the independent second layer, `MAXDOP = 1`, and the pooled-connection reuse case.

Two shapes the spine genuinely does not state, and this story should record rather than silently invent:
whether `Account` and `Space` themselves carry policies, and under which predicate — `Account` sits at the
top of the ER and is global, while `Membership` sits below `SPACE` yet must be readable across Spaces by
1.7's switcher under an `AccountId` predicate (AD-24). Question 4.

### The duplicate path: identical in status, body, shape and duration

This is the sharpest constraint in the story and the one that is most expensive to retrofit —
`YelloBMAD-handoff.md:84` warns that "retrofitting that changes the endpoint's shape".

Two acceptance criteria look contradictory and are reconciled only by AD-23. `acceptance-criteria.md:17`
says an existing address "cannot be registered a second time"; `:18` says the attempt "produces a response
indistinguishable from registering a new address". The resolution: the duplicate is refused **server-side,
silently**, while the response matches in status, body, shape and duration — the hash runs anyway.

**What this means concretely for the interface:** a duplicate registration renders the *success* path.
There is no "that email is taken" message, no inline error on the email field, no "sign in instead?" nudge,
and **no client-side email-availability check of any kind**. Adding any of those is a specification
violation, not a UX improvement (`EXPERIENCE.md:479`).

A server-side password-policy rejection that returns *before* the hash is performed reintroduces the same
branch by another route. A client-side rejection that never reaches the server is safe.

**Recommended response shape:** `204 No Content`. With no body there is nothing to differ, replay under
`Idempotency-Key` is trivially identical, and no identifier leaks. A `201` with a `Location` header would
force the duplicate path to fabricate an id. No source names the route or the status; this is question 5.

### The password, and what "tunable without re-registering" actually rests on

ASP.NET Core Identity 10's `PasswordHasher<T>` in IdentityV3 mode uses **PBKDF2 with HMAC-SHA512**, a
128-bit salt and a 256-bit subkey, with the iteration count taken from `PasswordHasherOptions.IterationCount`
(default **100,000**, raised from SHA256/10,000 in .NET 7).

NFR-6's "work factor tunable without re-registering existing Accounts" is satisfied **by construction**,
and the mechanism should be asserted rather than assumed: the iteration count is embedded in each stored
hash, and `VerifyHashedPassword` returns `PasswordVerificationResult.SuccessRehashNeeded` when
`embeddedIterCount < _iterCount`. Raising the configured number never invalidates an existing hash; the
upgrade happens on next successful sign-in. **The rehash-on-verify path itself lands in story 1.4** — this
story's obligation is to store hashes in a form that admits it, and to prove the format embeds the count.

The live tension to resolve, not to ignore: NFR-5 budgets writes at **500 ms p95 server-side**
(`quality-budgets.md:44`), and AD-23 requires registration to hash **even on the duplicate path**. Registration
is the one write in the product required to be slow. `quality-budgets.md:48` records that NFR-5's own
measurement basis — warm or cold — is unresolved and owned by the spine. Measure, choose, and state in the
Completion Notes whether registration is bounded by NFR-5 or exempt from it, with the reason either way.

### Personal Space is not a type — prove it, do not assert it

`glossary.md:10`: "**Personal Space** — Descriptive only, not a distinct type." `decisions-settled.md:26`
records "A distinct undeletable type" and "a permanently private type" as explicitly **rejected**.
`acceptance-criteria.md:48`: "No attribute distinguishes it from a Space created by CAP-5."

So: **no `IsPersonal` flag, no boolean, no subtype, no discriminator column, no separate table, no special
lifecycle rule.** The story title and AD-22 both say "the Personal Space", which is exactly the naming that
invites the defect the decision rejected.

AC4 requires this be *proved* by comparison. The comparison target — a Space created by another route —
does not exist until Epic 3 builds FR-5. Write the assertion against the shape FR-5 will produce, stated at
`acceptance-criteria.md:53-56`: the creating Account is Owner, exactly one Membership, the default Status
set, no Projects, and no visibility from any other Space.

A second reason not to flag it: `success-metrics.md:34` derives SM-3 — "the product's central bet, and the
most important number in this group" — from `Membership` rows, and SM-C1 from `Space` + `Membership`. The
rows this story writes are that metric's denominator.

### Forward compatibility: what OAuth will break, and what AC7 is really asking

`SPEC.md:241` records OAuth sign-in as "the P6 mid-flight change… **scheduled to fire once the identity
epic has shipped**". This is not hypothetical, and AC7 is the review that keeps it cheap.

Four things must stay soft (`harness-constraints.md:63-66`):

- **`Account` is unique by email address** — "the load-bearing one… a Glossary-level claim, so it reaches
  every artifact". A provider may return a different address, or none.
- **An Account is created with an email address *and a password*** — an OAuth Account has no password.
  **Nothing may assume a password exists on every Account.**
- **NFR-6's storage requirements** — must tolerate Accounts holding no password rather than treating that
  as an invalid state.
- **AD-23's uniform responses** — the guarantee must hold identically on the OAuth path.

One is already soft and needs nothing: AD-22 anticipates two Account-creation paths and requires them to
share one slice, so a third fits the rule it already states.

Note the interaction with FR-3 (`prd.md:146`): a deleted Account's address "can be used to register a new
Account, and that new Account inherits no Membership, no Space and no history." **The uniqueness index
cannot be a soft-delete tombstone that keeps the address occupied.**

### Email verification is absent from the contract — and adding it would breach one

No FR, CAP, NFR, acceptance criterion, journey, surface or architecture decision anywhere mentions email
verification, a confirmation link or an unverified-Account state. It is not in the MVP out-of-scope list
either, so it is neither included nor explicitly deferred.

**Do not add it**, and say so explicitly in the Completion Notes so a later reviewer does not read the
omission as an oversight. Two reasons: `acceptance-criteria.md:47` requires the Space be usable "at the
moment registration completes", with no pending state; and a verification mail sent only for genuinely-new
addresses is an **out-of-band enumeration oracle** that defeats AD-23's in-band uniformity.

### This story's code trips the §6.4 data-protection gate — reference it, do not implement it

PRD §6.4 claims no data-protection posture "while the operator is the only data subject", and gives the
gate a **testable trigger** rather than an aspiration: *the first Account created by anyone other than the
operator.* `epics.md:940-949` states it plainly — "the first Account created by anyone other than the
operator makes the PRD non-compliant until amended" — with five named prerequisites.

That trigger fires on the exact row this story's slice writes. The readiness remediation assigned the gate
itself to **story 1.10** as two new acceptance criteria (`:1386-1398`), so **do not implement it here**.
Cross-reference it at the point the `Account` row is created so the connection is not lost.

One property worth recording because it is a consequence of this story's schema rather than of 1.10's
prose: `harness-constraints.md:101` notes erasure holds only incidentally — FR-3 is a hard delete, every
Membership goes, the address is freed for reuse, and the new Account inherits nothing. That is true **only
because ownership cannot be forced on an Account**, which is why the Owner Membership this story writes
must be a `Membership` at Role `Owner` and never an `OwnerId` column on `Space` (`addendum.md:33`).

### The copy gate is the largest hidden scope in this story

No acceptance criterion mentions localisation. The build gate makes it unavoidable anyway, and this is
exactly the case the workflow means by "a behaviour required for the feature to work in the existing
system is a requirement whether or not it is written in the story".

`No_user_visible_string_literal_appears_in_a_component` scans every `.razor` text node and ten attributes,
and fails on **any word of two or more letters that is not `Yello`**. The word `Email` in a label fails the
build. Its sibling gate scans `@code` blocks and `*.razor.cs` for sentence-shaped literals.

There is **no localisation infrastructure in the repository** — no `.resx`, no `IStringLocalizer`
registration, no culture provider — and `<html lang="en">` is hard-coded, which makes `base.css`'s
26-locale casing exclusion **inert**. `deferred-work.md:32` records that, notes "no gate detects the
inertness either", and names this story's class of work as its owner.

### Testing requirements

- **xunit.v3 4.0.0 on Microsoft.Testing.Platform only.** `Microsoft.NET.Test.Sdk` is deliberately absent
  and gated. xunit's own `Assert` — no FluentAssertions, no Shouldly.
- **`dotnet test Yello.slnx` works.** Story 1.2's record claims it reports "Zero tests ran" and that suites
  must be run by executing each binary directly. **That is stale**; `global.json`'s
  `"test": { "runner": "Microsoft.Testing.Platform" }` opt-in works. Do not carry the workaround forward.
- Test names are sentences with underscores, one behaviour per test.
- **No `Task.Delay` as a synchronisation mechanism. Ever.** Wait on the condition, not the clock.
- The container fixture pattern, copied from `SharedFixtureSmokeTest.cs`:
  `Assert.SkipUnless(SqlServerContainerFixture.IsContainerRuntimeAvailable(), "<reason>")`, then
  `await using var fixture = new SqlServerContainerFixture(); await fixture.InitializeAsync();`
- **Each consumer gets its own ~2 GB SQL Server.** The fixture has no `[CollectionDefinition]` and no
  `WithReuse(true)`, and the suites run as separate processes. `deferred-work.md:8` defers the topology to
  **story 1.9** and justifies the deferral on the grounds that "the fixture has no consumer yet" —
  **this story is plausibly its first real consumer, so that justification expires here.** Do not attempt
  to settle the topology; do not write anything that assumes container sharing.
- If a production project gains a `ProjectReference`, check `deferred-work.md:10` first: declared edges are
  not the effective dependency closure, and "the first time a production row gains an edge" is that
  entry's stated trigger.

### The defect class this repository keeps rediscovering

Story 1.1 needed three review passes and story 1.2 needed two, each time because the previous pass's fix
reproduced the defect it fixed. The finding, in the commit subjects: *"gates that asserted less than they
claimed"*, *"the fixes that repeated the defect they fixed"*, *"Make the gates see the markup they were
always claimed to cover"*.

Two structural remedies are now in force and apply to every assertion this story writes:

1. **Glob the whole repository, never a named file.** A gate naming files that do not exist yet passes
   vacuously — `deferred-work.md:30` calls this "the defect class this suite was built to avoid".
2. **Plant a violation a later story would plausibly write, confirm the gate fires by name, and record the
   result.** `TESTING-CONVENTIONS.md:93`: "An absence assertion must be validated against a planted signal,
   or it is not a test."

A third, related: **do not write a completion note claiming a gate covers more than it does.**
`deferred-work.md:14` names that the "false-record defect".

This story is dense with absence assertions — the password absent from the datastore, the logs, every
error body and every API response; no second Account; no attribute distinguishing the Space. Every one of
them is the shape that passes vacuously.

### Project Structure Notes

The ring table in `tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs` is asserted as **exact
equality** in both directions. A new edge requires editing that table, and that edit is the visible moment
the dependency rule changes.

```
Yello.Domain/          Account, Space, Membership, StatusDefinition, Role, the hasher port
                       — references NOTHING; no EF Core type, no ASP.NET Core type
Yello.Application/
  Accounts/RegisterAccount/   command, handler, validator (+ its tests in tests/Yello.Tests.Slices)
Yello.Infrastructure/  DbContext, EF configuration, Migrations/, Identity wiring, the hasher adapter
Yello.Host/            the Minimal API registration endpoint, DI composition
Yello.Contracts/       the wire DTO, shared client + server
Yello.Client/          Router, layout, the registration page, the first components, .resx resources
tests/Yello.Tests.Slices/     slice + integration tests, mirroring {Area}/{UseCase}/
tests/Yello.Tests.Architecture/  the schema test and any new structural gate
```

**Per-ring package bans (prefix match) apply to `Yello.Domain`, `Yello.Application`, `Yello.Contracts` and
`Yello.Merge`:** `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.`, `Microsoft.Data.SqlClient`,
`Microsoft.Data.Sqlite`, `Aspire.`, `Testcontainers`, `Dapper`, `System.Data.SqlClient`. EF Core and Identity
may be referenced from **`Yello.Infrastructure`** and `Yello.Host` only. `Yello.Client` is exempt.

**Values shared between projects are stated once in the build.** `YelloSqlServerImage` and
`YelloDatabaseResourceName` reach code only through `AssemblyMetadata`, read via `BuildConstants.cs`.
A gate asserts no source file states either value literally — `"yello"` in a `.cs` file fails the build.

`Directory.Build.props` is imported **before** the coding-standards package's props, so restating any
standard property there silently forks the standard. An override that must read a package property belongs
in `Directory.Build.targets`, which does not exist.

New `.cs` and `.razor` files are `text eol=crlf` in the working tree per `.gitattributes`.

### Coding-standard traps in this suite

There is no `.editorconfig`. Everything comes from `Opinionated.DotNet.CodingStandards` 0.0.11:
`TreatWarningsAsErrors`, `AnalysisLevel=latest-all`, `Nullable=enable`, `EnforceCodeStyleInBuild`,
`NuGetAudit` at `low` with NU1900–NU1904 promoted to errors, and five analyzer packages (Meziantou,
BannedApiAnalyzers, NetAnalyzers, SonarAnalyzer, StyleCop).

**Banned at build, and all four are reachable from this story's code:**

- `DateTime.Now` / `DateTimeOffset.Now` → use `UtcNow`
- `StringComparison.InvariantCulture(IgnoreCase)` / `StringComparer.InvariantCulture(IgnoreCase)` → use
  `Ordinal` (this bites directly on email comparison)
- `new CultureInfo(string)` → use `CultureInfo.GetCultureInfo` (this bites on the culture provider)
- All of `Newtonsoft.Json` → `System.Text.Json`
- `Enum.TryParse` without `ignoreCase`; `System.Tuple<>` → `ValueTuple`

### Vocabulary

PRD §2 Glossary verbatim, in code, identifiers, namespaces, folders and UI copy. `epics.md:50` calls a
synonym "a discipline violation in every story written below".

Use: `Account`, `User` (an Account acting inside a specific Space — **not** a synonym for Account),
`Space`, `Personal Space` (descriptive only), `Membership`, `Role`, `Owner`, `Admin`, `Member`, `Viewer`,
`Project`, `Task`, `StatusDefinition`, `Session`.

**Forbidden in any identifier, namespace or folder:** `Workspace`, `Tenant`, `Org`, `Organisation`, `Team`,
and `User` where `Account` is meant. **There is no `Users` table in this architecture — the entity is
`Account`.** A generic `TenantId` column filter or ambient tenant middleware is an explicitly rejected
shape, not to be reopened (`decisions-settled.md:18`; `addendum.md:18`).

### Previous story intelligence

- **`Yello.Infrastructure/AssemblyMarker.cs` names this story in so many words:** "Story 1.1 adds no
  DbContext, no migration and no table. **Story 1.3 creates the first three tables (Account, Space,
  Membership).**" Update that comment.
- **Logic a test must reach cannot be a local function in `Program.cs`.** Story 1.1 found that deleting an
  entire startup call left 52/52 tests green. The fix was a named type plus a process-booting integration
  test. The registration endpoint's handler must be reachable by name.
- **There is no `WebApplicationFactory` and no `Microsoft.AspNetCore.Mvc.Testing`.** Integration tests boot
  the real Host **as a process** — `HostStartupTests.RunHostAsync` is the pattern.
- **Story 1.2's ungated-token decision (Lee, 2026-08-27):** a mistyped hex is invisible to every gate unless
  it crosses a contrast threshold. A real transcription error was caught by script, not by a gate. Any new
  literal values this story introduces carry the same exposure.
- **Environmental, and it will look like a TLS fault when it is not:** Rancher Desktop's ephemeral-port
  forwarding failed once during story 1.1, and the note records that "every suite from story 1.3 onward
  would have failed on this machine with a pre-login handshake timeout". Remedy: `rdctl shutdown`,
  `wsl --terminate rancher-desktop`, relaunch.

### Git intelligence

Commit subjects are a sentence stating the change's thesis — no prefix, no ticket id, not
conventional-commits. Bodies run 20–50 lines: the dominant finding, what was demonstrated empirically,
Lee's numbered decisions, the patch count, plant results, and the exact build and test numbers. Trailers
are `Co-Authored-By: Claude Opus 5` and `Claude-Session:`. **Committed straight to `main` — no branches,
no PRs.**

One commit per workflow phase, never per file: (1) the story-context commit — story file and
`sprint-status.yaml` only; (2) the implementation commit — code, tests, `deferred-work.md`, story record;
(3) one commit per review round. Every commit carries the state transition and the verified numbers.

### Latest technical information

Verified 2026-08-28 against Microsoft Learn, the `dotnet/aspnetcore` source and the OWASP Password Storage
Cheat Sheet. **The AR-1 pins are unchanged and must not be edited** — changing one is an architecture edit
that amends `epics.md` first, then the spine, then `PackageVersionPinTests`.

- **`PasswordHasher<T>` / IdentityV3 in ASP.NET Core 10:** PBKDF2, **HMAC-SHA512**, 128-bit salt, 256-bit
  subkey, iteration count from `PasswordHasherOptions.IterationCount`, **default 100,000** (SHA512/100,000
  since .NET 7; previously SHA256/10,000).
- **`VerifyHashedPassword` returns `SuccessRehashNeeded`** when the hash's embedded iteration count is below
  the configured one, and also when the embedded PRF is SHA1 or SHA256. This is the mechanism that makes
  NFR-6's "tunable without re-registering" true.
- **OWASP's current PBKDF2 figures:** 220,000 iterations for HMAC-SHA512, 600,000 for HMAC-SHA256. Argon2id
  is preferred where FIPS-140 validation is not required; PBKDF2 is the FIPS-validated option and is what
  AD-1 selects by selecting Identity.
- **EF Core 10's `ExecuteUpdate`** is available and already relied on by AD-26. Not needed here.
- **A standing caveat from `review-web-verification.md:40-46`:** two plausible-but-wrong SQL Server claims
  have already been caught in this project, "both in *index behaviour* specifically", with the
  recommendation to treat any assertion about SQL Server engine behaviour as unverified until checked
  against Microsoft documentation. **This story creates the first indexes.**

### References

- Story text and ACs — `epics.md:593-632`; Story Coverage Index row — `epics.md:328`
- FR-1 — `prd.md:117-125`, `epics.md:60`; FR-4 — `prd.md:157-164`, `epics.md:66`
- AD-22 / AR-27 (one slice, one transaction) — `ARCHITECTURE-SPINE.md:206-210`, `epics.md:197`
- AD-23 / AR-28 (uniform responses, including duration) — `ARCHITECTURE-SPINE.md:212-216`, `epics.md:198`
- AD-1 / AR-4 (Identity for authentication only) — `ARCHITECTURE-SPINE.md:71-75`, `epics.md:174`
- AD-2 / AR-5…AR-8 (database-enforced Space scoping) — `ARCHITECTURE-SPINE.md:77-86`, `epics.md:175-178`
- AD-5 / AR-12 (filtered unique Owner index) — `ARCHITECTURE-SPINE.md:102-106`, `epics.md:182`
- AD-21 / AR-2 (the ring rule as a build gate) — `ARCHITECTURE-SPINE.md:200-204`, `epics.md:172`
- AR-3 (slice layout; cross-cutting invariants live in the pipeline) — `epics.md:173`
- AR-34 (ids, dates, problem+json, one transaction, logging) — `epics.md:204`
- AR-35 (four gating suites, Testcontainers, never in-memory) — `epics.md:205`; AR-36 (migrations) — `epics.md:206`
- NFR-6 — `quality-budgets.md:50-55`, `epics.md:141`; FS-NFR-1 — `epics.md:148`
- NFR-5 (500 ms p95 writes; measurement basis unresolved) — `quality-budgets.md:42-48`
- NFR-9 (registration is a named gated flow) — `quality-budgets.md:82-86`, `epics.md:144`
- Glossary — `glossary.md:7-16`; Personal Space is not a type — `glossary.md:10`, `decisions-settled.md:26`
- Role capability matrix, Owner column and its four boundaries — `role-capability-matrix.md:11-26, 36-43`
- OAuth softness — `harness-constraints.md:57-69`, `SPEC.md:241`, `prd.md:821`
- Submit-and-wait state pattern — `EXPERIENCE.md:273`; the duplicate path must not branch — `EXPERIENCE.md:479`
- Tokens and components — `DESIGN.md:162-174, 204-222, 295, 332-359, 372, 413-419, 431, 501`
- Accessibility findings — `review-accessibility.md:24-31, 62, 108, 123-127`
- Readiness: entity ownership `:992`, schema test in the same story `:1003`, epic order `:961`,
  assumptions unconfirmed `:1240-1247`
- Test design: work factor `test-design-qa.md:486`; blocker B3 `test-design-architecture.md:110-113, 312`;
  retrofit warning `YelloBMAD-handoff.md:84`
- Conventions and gates — `tests/TESTING-CONVENTIONS.md`; ring table —
  `tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs`
- Deferred-work entries that become reachable here — `deferred-work.md:8, 10, 14, 28, 32, 44, 46`

### Questions for Lee — raised, not resolved

1. **What is the Personal Space called, and does registration collect a display name?** The blocking
   contradiction above. Three options, and my recommendation is (a):
   **(a) Add a display-name field.** It costs a third field and an amendment to FR-1, `harness-constraints.md:64`
   and the mockup — but UX-DR34 needs a display name for every Membership from Epic 4, avatar initials need
   it, "Ravi's Space" needs it, and PRD §12 assumption #1 already assumes it exists. Confirming rather than
   revising is what readiness issue 5 asks for. OAuth providers return display names, so it does not harden
   anything OAuth breaks.
   **(b) Derive from the email local part** — zero document churn, but `ravi@anand.dev` yields "ravi's Space",
   not the "Ravi's Space" UJ-1 and `EXPERIENCE.md:473` both show.
   **(c) A fixed, non-derived name**, abandoning assumption #1 outright.
   **Interim default implemented, so nothing is blocked:** (b), behind a single `Yello.Domain` choke point
   with the format string in a resource. Changing the answer changes one function and one string.

2. **The password work factor, and whether registration is bound by NFR-5.** I have not invented a number.
   Task 8 measures and proposes; the story records it. The question for you is the policy: is registration
   exempt from the 500 ms p95 write budget because AD-23 requires it to be slow, or is the budget the
   ceiling that caps the work factor? `quality-budgets.md:48` records that NFR-5's warm-versus-cold
   measurement basis is itself unresolved and owned by the spine.

3. **Does registration sign the new Account in?** No AC says so, and 1.4 owns authentication while 1.7 owns
   the context bar — so on the ACs alone this story ends at "completion is announced". But UJ-1, `prd.md:84`
   and `EXPERIENCE.md:473` all describe landing directly in the Space. Confirming that the landing is
   assembled in 1.4/1.7 rather than here would settle it. The story is written for that reading.

4. **Do `Account` and `Space` carry RLS policies, and under which predicate?** The spine never states it.
   `Membership` sits below `SPACE` in the ER, so AD-2 gives it a `SpaceId` policy — yet 1.7's switcher must
   read it across Spaces under `SESSION_CONTEXT('AccountId')` (AD-24). This is adjacent to the AD-24
   amendment already due before Epic 3 (readiness issue 3). This story writes the policies for the tables it
   creates; if you would rather all policies land in 1.5, that is a one-line scope change here.

5. **The registration route and success status.** No document names either. I have recommended
   `POST /api/v1/accounts` returning `204 No Content` — no body means nothing can differ between the new and
   duplicate paths, and no identifier leaks. Worth a nod before it becomes the contract, since
   `YelloBMAD-handoff.md:84` warns this endpoint's shape is expensive to retrofit.

6. **Idempotency (AR-25) has no owner in Epic 1.** Every state-changing endpoint is supposed to accept an
   `Idempotency-Key` and return the original response on replay, and AR-3 forbids a slice implementing it
   itself — but no Epic 1 story row owns building the pipeline that would. Registration is the first
   state-changing endpoint in the product. Should this story build that pipeline seam, or is it 1.6's
   alongside the bound registry?

7. **`deferred-work.md`'s three "first rendered surface" entries become reachable here** (AC13's WCAG 1.4.12
   and 200%-text-zoom measurement, AC11's two unmeasurable clauses, and the `em`-relative-length parser gap).
   All three are blocked on **B5**, the browser-test binding, which is still undecided — and NFR-9 names
   registration as a gated flow. Does B5 get decided now, or do these carry forward with this story recorded
   as the point they became live?

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (`claude-opus-5`), via the `bmad-dev-story` workflow, 2026-08-28 / 2026-08-29.

### Lee's decisions during implementation

Seven questions were raised by the story. Four were put to Lee; three were resolved from the
story's own tasks and are recorded here so a reviewer can see they were decided rather than
overlooked.

| # | Question | Decision | Taken by |
|---|---|---|---|
| 1 | What is the Personal Space called? | **Collect a display name.** Three fields at registration; `Account.DisplayName`; the Space named from it. Confirms PRD §12 assumption 1 rather than revising it. | Lee, 2026-08-28 |
| 2 | Work factor, and is registration bound by NFR-5? | **Measure first, then decide** → **220,000** (OWASP). Registration is *bounded* by NFR-5 and meets it; no exemption needed. | Lee, 2026-08-28, after the measurement below |
| 3 | Does registration sign the new Account in? | **No.** 1.4 owns authentication, 1.7 the context bar. AC6 ends at "completion is announced". | From the story's scope table |
| 4 | Do `Account` and `Space` carry RLS policies? | **Space-scoped tables now** (`Membership`, `StatusDefinition`); `Account`/`Space` recorded as an open shape for the AD-24 amendment already due before Epic 3. | Lee, 2026-08-28 |
| 5 | Route and success status | **`POST /api/v1/accounts` → `204 No Content`.** No body, so nothing can differ between the two paths and no identifier leaks. | Lee, 2026-08-28 |
| 6 | Idempotency (AR-25) | **Not built here.** AR-3 forbids a slice implementing it; no Epic 1 row owns the pipeline. Recorded in `deferred-work.md` as unassigned. | From Task 2 + AR-3 |
| 7 | Blocker B5 | **Carried forward, with this story recorded as the point it became live.** | From Task 7 |

### The NFR-6 measurement

Task 8 required measuring before choosing. **Hardware:** 12th Gen Intel Core i7-12700H (14 cores /
20 logical), Windows 11, Debug build, Rancher Desktop running. **Method:** 20-second burn-in, then
60 interleaved samples per candidate.

| Iterations | p50 | p95 | p99 |
|---|---|---|---|
| 100,000 (framework default) | 120.1 ms | **145.9 ms** | 161.1 ms |
| 220,000 (OWASP HMAC-SHA512) | 272.9 ms | **297.7 ms** | 325.4 ms |

Cost is linear in the iteration count (2.2× iterations → 2.04× p95). The surrounding database work
— one transaction, the session-context call, six row inserts — is single-digit milliseconds, so the
hash is effectively the whole request.

**The finding that resolved the story's central tension:** at 220,000 the server-side p95 is about
300 ms, inside NFR-5's 500 ms write budget with roughly 200 ms to spare. Registration can be both
"deliberately slow" for AD-23 and inside the budget, so it is recorded as **bounded by NFR-5 and
meeting it** rather than exempt from it. The story anticipated having to choose between the two.

**A measurement artefact worth recording, because it would mislead anyone repeating this.**
Whichever candidate ran first read about 2.5× too fast: 220,000 measured first gave a p50 of
102 ms against 273 ms for the same count later in the same process. That is CPU turbo before
sustained load pulls the clock down, not a property of the algorithm. An early run without a
burn-in produced 45 ms for 100,000, which is not a usable figure and was discarded. The same
artefact is why `DurationIndistinguishability` interleaves its samples.

**Open caveat, with an owner.** This is a fast laptop P-core; an Azure vCPU is typically slower. If
the deploy target runs more than about 1.7× slower, 220,000 crosses the 500 ms budget. **Story 1.10
owns the deploy target and re-measures there** — and because the count is embedded in every stored
hash, lowering it is a configuration change that invalidates nothing.

### Planted-violation results

`TESTING-CONVENTIONS.md:93` — "an absence assertion must be validated against a planted signal, or
it is not a test". Every absence assertion this story adds was failed on purpose. Each plant was
applied, the solution built, the named test run, and the plant reverted.

| # | Planted defect | Assertion | Result |
|---|---|---|---|
| A | The document-language call deleted from `Client/Program.cs` | `The_document_language_is_set_from_the_active_culture` | **Caught** |
| B | A failure code's entry removed from `ClientCopy.resx` | `Every_registration_failure_code_has_a_message_and_a_field` | **Caught** |
| C | A failure code renamed off its field prefix | same | **Caught** |
| D | A configuration written but never applied | *(the build)* — MA0182 | **Caught by the compiler** |
| D′ | A configuration applied but its table name left to EF convention | `Every_entity_configuration_reaches_the_model` | **Caught** |
| E | `Membership.SpaceId` made nullable | `Every_Space_scoped_entity_has_a_non_nullable_SpaceId` | **Caught** |
| F | A key left store-generated | `No_key_is_generated_by_the_database` | **Caught** |
| G | A locale listed with no resources behind it | `Every_supported_culture_has_resources_behind_it` | **Caught** |
| H | A literal rendered in a component | `No_user_visible_string_literal_appears_in_a_component` | **Caught** |
| I | The raw password written into the datastore | `The_password_appears_nowhere_in_the_datastore` | **Caught** |
| J | The security policy created `WITH (STATE = OFF)` | `Space_scoped_rows_are_invisible_without_a_session_context` | **Caught** |
| K | The endpoint logging the email address | `No_log_line_carries_the_password_or_distinguishes_the_two_paths` | **Caught** |

**Plant D is the most useful result and is not a gate success.** Removing an `ApplyConfiguration`
line does not reach any test: the build fails first, because MA0182 reports the configuration class
as unreferenced. That is a better outcome than a gate, and it also exposed a real defect in the
gate I had written — the first version asserted only that the entity was *in the model*, which the
`DbSet<T>` property satisfies on its own. Plant D′ is the case the compiler cannot see, and it
failed until the gate was rewritten to assert the configuration's **effect** (an explicit singular
table name, which EF's conventions would pluralise).

**One plant is a permanent test rather than a table row.**
`RegistrationDurationTests.The_method_detects_a_registration_that_skips_the_hash` plants the
skipped hash and requires `DurationIndistinguishability` to tell the paths apart. Keeping it as a
test is what stops a later story widening the tolerance to quiet a flake and silently disarming the
real assertion beside it.

### Debug Log References

**The container runtime needed the recorded remedy.** Rancher Desktop was stopped, and starting it
gave `failed to connect to the backend: timed out dialing Hyper-V socket` — the exact failure story
1.1's record predicted "would have failed on this machine with a pre-login handshake timeout".
`rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch fixed it, as recorded.

**Three SQL Server claims were verified empirically rather than taken from documentation**, per the
story's standing caveat that two plausible-but-wrong claims have already been caught in this
project, "both in *index behaviour* specifically":

1. **Row-level security applies to `sa`.** The Testcontainers fixture connects as a sysadmin, so if
   RLS had exempted it the isolation assertions would have been vacuous.
   `Space_scoped_rows_are_invisible_without_a_session_context` observes the rows disappearing on
   that very connection.
2. **EF Core 10's `SequentialGuidValueGenerator` really is sequential under SQL Server's ordering.**
   `uniqueidentifier` compares its last six bytes first, so a left-to-right monotonic value (UUIDv7,
   which AR-34 excludes by name) is *not* monotonic in the index.
   `Successive_identifiers_ascend_under_SQL_Servers_own_ordering` compares 500 generated ids under
   `System.Data.SqlTypes.SqlGuid`, which is the BCL's implementation of the engine's own comparison.
3. **The filtered unique index refuses a second Owner and permits a second member.** Asserted by
   inserting an `Admin` Membership successfully and then an `Owner` one, and requiring the second to
   throw with the index named.

**A false positive in a story-1.2 gate, found and fixed.** `No_user_visible_string_literal_appears_in_a_component`
ended a tag at the first `>` anywhere, so `ValueChanged="@(value => _name = value)"` closed the tag
at the lambda arrow and the rest of the attribute was reported as rendered copy — the build failed
saying `Register.razor` "renders the literal text 'displayName value'", which is two identifiers
inside a C# expression. `TextNodes` is now quote-aware, which is strictly *stricter* (it can only
move a tag's end later). The same break would have hit epic 2's first templated component through
its generic type arguments. The markup uses `@bind-Value` regardless, which is better Razor.

**Two build traps later stories will meet.** The word `Todo` cannot appear in any comment —
SonarAnalyzer's S1135 treats it as a task marker and `TreatWarningsAsErrors` makes it an error — so
FR-24's first default Status is named only in `DefaultStatusSet.Names` and never in prose. And every
EF migration needs a hand-editing pass to satisfy the coding standard; both are recorded in
`deferred-work.md` and `TESTING-CONVENTIONS.md`.

### Completion Notes List

1. **AC1 — one Account, one Space, one Owner Membership, one transaction.** `RegisterAccountHandler`
   assembles all four sets of rows and `AccountRegistrationStore` commits them in one transaction,
   with the RLS session context set inside it. Asserted at the unit level
   (`RegisterAccountHandlerTests`) and against a real migrated SQL Server
   (`Registration_commits_an_Account_a_Space_an_Owner_Membership_and_its_Statuses`). The Space
   carries FR-24's three Statuses in order. **The "no Projects" clause is deliberately unasserted**
   — no Project entity exists until Epic 2, so a test for it would be vacuous.

2. **AC2 — the duplicate path is indistinguishable, including in duration.** The hash runs first and
   unconditionally; `RegisterAccountHandler.HandleAsync` returns `Task`, not `Task<bool>`, so no
   caller has anything to branch on. Status, body, content type and header names are compared
   *between the two responses* rather than each against an expectation
   (`A_duplicate_registration_answers_exactly_as_a_new_one_does`), and duration is compared with the
   B3 method. Logs are uniform too, which is the half a response-only test would miss.

3. **AC3 — a failure is a failed transaction.** Forced at the *last* write rather than the first
   (`A_registration_that_cannot_complete_leaves_no_Account_behind`), so the Account and Space
   inserts are already on the wire when it fails. An assertion that failed at the first row would
   prove nothing.

4. **AC4 — the provisioned Space is an ordinary Space.** `Space` has exactly three members, asserted
   by name, so an `IsPersonal` flag or a `ProvisionedAtRegistration` timestamp fails. The comparison
   target FR-5 will produce does not exist until Epic 3, so the assertion is against the *shape*
   that comparison will use, stated as such rather than pretending to make it.

5. **AC5 — the password is never observable, and the work factor is tunable.** The datastore
   assertion reads **every character column of every user table from the catalogue**, not just
   `PasswordHash` — so a password copied somewhere nobody was looking is caught, and tables later
   stories add are covered without extending the test. Response bodies and logs are asserted
   separately. Tunability is asserted as the mechanism: a hash written at 100,000 verifies
   `Success` at 100,000 and `SuccessRehashNeeded` at 220,000, from a hash read back out of the
   database.

6. **AC6 — the surface states its wait.** In-flight condition stated in words, resubmission disabled
   by a real `disabled` attribute, completion announced through `role="status"` and focused. No
   spinner, no progress percentage, no celebration, and nothing that moves.

7. **AC7 — nothing hardens what OAuth will break.** `PasswordHash` is nullable; the Account's
   identity is `Id` and not the address; `EmailAddressNormalisation` states the uniqueness rule in
   one function; there is no soft-delete tombstone that would keep a deleted address occupied
   (FR-3). AD-22's "one slice, two paths" already accommodates a third.

8. **Email verification is deliberately absent, and that is not an oversight.** No FR, CAP, NFR,
   acceptance criterion, journey, surface or architecture decision mentions it. Two reasons it must
   stay absent: `acceptance-criteria.md:47` requires the Space to be usable "at the moment
   registration completes", and a verification mail sent only for genuinely-new addresses is an
   **out-of-band enumeration oracle** that defeats AD-23's in-band uniformity.

9. **PRD §6.4's data-protection gate fires on the row this story writes**, and is **not** implemented
   here. The readiness remediation assigned it to **story 1.10** as two acceptance criteria; it is
   cross-referenced at the point the `Account` row is created so the connection is not lost.

10. **One deviation from the story text, from Lee's decision.** Task 5 says "Two fields: email and
    password"; registration collects **three**, the third being the display name. That is Lee's
    answer to question 1 and amends FR-1, `harness-constraints.md:64` and the mockup. Every other
    negative constraint in that bullet holds: no plan picker, no team-size question, no
    confirm-password, no terms checkbox, no CAPTCHA, no onboarding.

11. **One deviation from the story's structure note, for a stated reason.** It places "the schema
    test" in `Yello.Tests.Architecture`. It cannot go there: asserting a *migrated* schema needs a
    real SQL Server, that suite's ring row excludes `Yello.Tests.Shared`, and adding it would make
    the one suite that "takes seconds and should fail before anything slower starts" depend on a
    container. `SpaceIsolationSchemaTests` is in `Slices`; the purely structural gates
    (`PersistenceModelGateTests`, `LocalisationGateTests`) are in `Architecture`.

12. **Two visible architecture edits, both deliberate.** `Yello.Tests.Slices` gained a
    `Yello.Contracts` edge in `AllowedReferenceEdges` and its csproj — the endpoint tests POST a
    Contracts DTO and read a Contracts problem type, and reaching those transitively through Host is
    the exposure `deferred-work.md:10` records. And `Microsoft.Extensions.Localization` 10.0.11 was
    added as a **non-AR-1** central pin: neither `Yello.Client` (a Blazor WASM app resolves against
    `Microsoft.NETCore.App`) nor `Yello.Infrastructure` (a plain library) inherits it, verified
    against the restored asset graph. **No AR-1 pin was changed.**

13. **What is NOT gated, stated plainly rather than implied.** The 320px viewport, the WCAG 1.4.12
    text-spacing override and 200% text-only zoom are discharged **constructively** — no fixed
    widths or heights, a maximum in `rem`, internal padding in `rem` — and **not measured**. That
    needs a browser and is blocker B5's. Writing a gate that appeared to cover them would be the
    vacuous-gate defect this suite exists to avoid. Story 1.3 is the first story with a rendered
    surface, so the three `deferred-work.md` entries waiting on that condition are now live.

14. **The end-to-end browser flow does not work yet, by design.** The client and Host are separate
    origins, so a browser POST is refused until CORS exists — and CORS is story 1.4's, by the
    story's own scope table. Recorded in `deferred-work.md`. Every server-side criterion is asserted
    against a Host running as a real process.

### File List

**Added — `Yello.Domain`**
- `Yello.Domain/Accounts/Account.cs`
- `Yello.Domain/Accounts/EmailAddressNormalisation.cs`
- `Yello.Domain/Accounts/IAccountRegistrationStore.cs`
- `Yello.Domain/Accounts/IPasswordHasher.cs`
- `Yello.Domain/IIdentifierGenerator.cs`
- `Yello.Domain/Memberships/Membership.cs`
- `Yello.Domain/Memberships/Role.cs`
- `Yello.Domain/Spaces/PersonalSpaceName.cs`
- `Yello.Domain/Spaces/Space.cs`
- `Yello.Domain/Statuses/DefaultStatusSet.cs`
- `Yello.Domain/Statuses/StatusDefinition.cs`

**Added — `Yello.Application`**
- `Yello.Application/Accounts/RegisterAccount/RegisterAccountCommand.cs`
- `Yello.Application/Accounts/RegisterAccount/RegisterAccountHandler.cs`
- `Yello.Application/Accounts/RegisterAccount/RegisterAccountValidator.cs`

**Added — `Yello.Infrastructure`**
- `Yello.Infrastructure/InfrastructureServices.cs`
- `Yello.Infrastructure/Identity/IdentityPasswordHasher.cs`
- `Yello.Infrastructure/Identity/PasswordWorkFactor.cs`
- `Yello.Infrastructure/Localisation/RegistrationCopy.cs`
- `Yello.Infrastructure/Localisation/RegistrationCopy.resx`
- `Yello.Infrastructure/Persistence/AccountRegistrationStore.cs`
- `Yello.Infrastructure/Persistence/SchemaNames.cs`
- `Yello.Infrastructure/Persistence/SequentialGuidIdentifierGenerator.cs`
- `Yello.Infrastructure/Persistence/YelloDbContext.cs`
- `Yello.Infrastructure/Persistence/YelloDbContextFactory.cs`
- `Yello.Infrastructure/Persistence/Configurations/AccountConfiguration.cs`
- `Yello.Infrastructure/Persistence/Configurations/MembershipConfiguration.cs`
- `Yello.Infrastructure/Persistence/Configurations/SpaceConfiguration.cs`
- `Yello.Infrastructure/Persistence/Configurations/StatusDefinitionConfiguration.cs`
- `Yello.Infrastructure/Persistence/Migrations/20260828164716_InitialSchema.cs`
- `Yello.Infrastructure/Persistence/Migrations/20260828164716_InitialSchema.Designer.cs`
- `Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs`

**Added — `Yello.Contracts`**
- `Yello.Contracts/ProblemResponse.cs`
- `Yello.Contracts/ProblemTypes.cs`
- `Yello.Contracts/Accounts/AccountRoutes.cs`
- `Yello.Contracts/Accounts/RegisterAccountRequest.cs`
- `Yello.Contracts/Localisation/CultureSelection.cs`
- `Yello.Contracts/Localisation/SupportedCultures.cs`

**Added — `Yello.Host`**
- `Yello.Host/Endpoints/RegisterAccountEndpoint.cs`
- `Yello.Host/RegistrationLog.cs`

**Added — `Yello.Client`**
- `Yello.Client/Components/FormField.razor`
- `Yello.Client/Components/InlineErrorRegion.razor`
- `Yello.Client/Components/PrimaryButton.razor`
- `Yello.Client/Layout/MainLayout.razor`
- `Yello.Client/Localisation/ClientCopy.cs`
- `Yello.Client/Localisation/ClientCopy.resx`
- `Yello.Client/Localisation/DocumentLanguage.cs`
- `Yello.Client/Pages/Register.razor`
- `Yello.Client/Pages/RegistrationFields.cs`
- `Yello.Client/Pages/RegistrationPhase.cs`
- `Yello.Client/wwwroot/appsettings.Development.json`
- `Yello.Client/wwwroot/css/components.css`

**Added — tests**
- `tests/Yello.Tests.Architecture/LocalisationGateTests.cs`
- `tests/Yello.Tests.Architecture/PersistenceModelGateTests.cs`
- `tests/Yello.Tests.Shared/DurationIndistinguishability.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/HostProcess.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/MigratedDatabaseFixture.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegisterAccountEndpointTests.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegisterAccountHandlerTests.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegisterAccountIntegrationTests.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegisterAccountValidatorTests.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegistrationDatabase.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegistrationDurationTests.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/SequentialGuidIdentifierGeneratorTests.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/SpaceIsolationSchemaTests.cs`

**Modified**
- `.config/dotnet-tools.json` — added `dotnet-ef` 10.0.11
- `Directory.Packages.props` — added the non-AR-1 `Microsoft.Extensions.Localization` pin
- `Yello.Client/App.razor` — `Router`, layout, localised not-found page
- `Yello.Client/AssemblyMarker.cs` — stale hand-off comment
- `Yello.Client/Program.cs` — culture provider, document language, Host base address
- `Yello.Client/Yello.Client.csproj` — localisation package
- `Yello.Client/_Imports.razor` — new namespaces
- `Yello.Client/wwwroot/index.html` — `components.css` link, `lang` rationale, stale comment
- `Yello.Host/Program.cs` — infrastructure, slice, clock, request localisation, endpoint
- `Yello.Infrastructure/AssemblyMarker.cs` — stale hand-off comment
- `Yello.Infrastructure/Yello.Infrastructure.csproj` — EF Core, Identity, localisation, `InternalsVisibleTo`
- `tests/TESTING-CONVENTIONS.md` — B3 method, class-fixture pattern, migration pass, counts, B5
- `tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs` — Slices → Contracts edge
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs` — quote-aware `TextNodes`
- `tests/Yello.Tests.Architecture/PackageVersionPinTests.cs` — the new non-AR-1 pin
- `tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj` — Contracts reference
- `_bmad-output/implementation-artifacts/deferred-work.md` — one entry closed, five added
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — status transitions

`git diff --stat 73805cb` — **code, tests and build files only: 80 files changed, 7,367
insertions, 65 deletions.**

Stated that way on purpose. The whole-tree figure against the baseline includes this story record,
so it moves every time this section is edited — which made an earlier draft of this line wrong
within minutes of being written. The three excluded files are this story file, `deferred-work.md`
and `sprint-status.yaml`; including them gives 83 files and 8,385 insertions at the moment of
writing, and that number is not stable.

### Change Log

| Date | Change |
|---|---|
| 2026-08-28 | Story picked up; status `ready-for-dev` → `in-progress`. Four questions put to Lee and answered: display name collected, `POST /api/v1/accounts` → 204, measure-then-choose the work factor, Space-scoped RLS now. |
| 2026-08-28 | Domain entities, ports and the naming choke point; EF Core schema, four configurations and the initial migration carrying the row-level security policy; the `RegisterAccount` slice. |
| 2026-08-28 | Work factor measured on an i7-12700H (100,000 → 145.9 ms p95; 220,000 → 297.7 ms p95). Reported to Lee, who chose **220,000**. Registration recorded as bounded by NFR-5 and meeting it. |
| 2026-08-29 | The endpoint, its uniform 204 and its RFC 9457 rejection; the registration surface, the first three components, `components.css`, the resource system and the culture provider. |
| 2026-08-29 | Closed `deferred-work.md`'s hard-coded `<html lang>` entry — the only entry this story closes — and added an IL-based gate so the fix cannot be silently undone. |
| 2026-08-29 | Twelve planted violations run against the story's absence assertions; all caught. Plant D exposed a real weakness in a gate this story wrote, which was rewritten to assert the configuration's effect rather than the entity's presence. |
| 2026-08-29 | Fixed a false positive in story 1.2's copy gate: `TextNodes` ended a tag at the first `>`, so a lambda in an attribute was reported as rendered copy. Now quote-aware. |
| 2026-08-29 | Five new `deferred-work.md` entries: idempotency unowned, CORS needed for the cross-origin call, `Account`/`Space` RLS predicates undecided, the EF migration hand-editing pass, and the `Todo`-in-comments trap. |
| 2026-08-29 | `dotnet build Yello.slnx` clean at **0 warnings**; `dotnet test Yello.slnx` **136 passed / 0 failed** (82 architecture, 54 slices). Status `in-progress` → `review`. |
