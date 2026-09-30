---
baseline_commit: 4abd4b71370a0eb1d6e33a2ef1e68e45e5ebd2bc
---

# Story 1.5: Resolve the active Space per request, enforced in the database

Status: review

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-5-resolve-the-active-space-per-request-enforced-in-the-database`
Requirements owned: **FR-15** *(the enforcement half — 1.6 owns the refusal half)*, **AR-5, AR-6, AR-7, AR-8 (all = AD-2)**, **NFR-1** (`epics.md:348`)
Also owned, and **not** in the epic's acceptance criteria: **blocker B2 — the RLS seeding strategy** (`test-design-architecture.md:109`, `handoff.md:87`). See AC8.
Depends on: **story 1.1** (done) — the eight-project ring layout, the gating suites, the build gates.
**story 1.3** (done) — `Account`, `Space`, `Membership`, `StatusDefinition`, the `SpaceIsolationPolicy` migration, `SchemaNames`, `SpaceIsolationSchemaTests`, `MigratedDatabaseFixture`.
**story 1.4** (done) — the Session, `SessionPrincipal.AccountIdOf`, `UseAuthentication()`, the route group and its endpoint-filter factory.
Seeds, does not own: the pipeline 1.6 hangs refusal shape and refusal recording on; the second session-context key (`AccountId`) story 1.7 needs; the `AccountId`/`SpaceId` establishment seam story 1.8 reuses for Tokens; the behaviour story 1.9 proves.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As any Account holder,
I want every read and write of Space-scoped data authorised against my Membership in the Space that owns it,
So that nothing I am not a member of can reach me by any route.

## Acceptance Criteria

AC1–AC7 are reproduced **verbatim** from `epics.md:693-726` (`status: final`), numbered by position per the convention `epics.md:8` establishes. AC8 is **not** from `epics.md` — it is carried from the test-design handoff, which assigns it to this story by name; see *The acceptance criterion the epic does not carry* below.

**AC1 — the context is resolved from a Membership row, before anything is decided**

**Given** an authenticated request carrying `{spaceId}` as the first path segment after the version
**When** it enters the pipeline
**Then** an `ActiveSpaceContext` is resolved by reading the Membership row for that `(AccountId, SpaceId)` pair *before* any authorisation decision is made
**And** a request whose Space context cannot be resolved is refused rather than defaulted

**AC2 — per unit of work, never per connection**

**Given** a resolved Space context
**When** a unit of work begins
**Then** `sp_set_session_context 'SpaceId', …, @read_only = 1` is called at the start of **every** unit of work, from `ActiveSpaceContext` and never from a client-supplied value
**And** never once per connection, because a pooled connection reused across two Spaces is the exact leak this guards against

**AC3 — every Space-scoped table carries the column and the policy**

**Given** every Space-scoped table
**When** the schema test runs
**Then** each carries a non-nullable `SpaceId` and a row-level security policy filtering on `SESSION_CONTEXT('SpaceId')`
**And** a Space-scoped table without a policy fails the test

**AC4 — the second layer is independent, and nothing routes around it**

**Given** the RLS policies
**When** EF Core global query filters are inspected
**Then** each policy is independently restated from application state rather than from session context, so neither layer alone carries NFR-1
**And** no raw SQL bypassing global query filters exists outside `Infrastructure`

**AC5 — the pooled connection, and the parallel-plan defect class**

**Given** a pooled connection
**When** two requests for different Spaces are served consecutively on the same physical connection
**Then** the second returns only its own Space's rows
**And** the database is configured `MAXDOP = 1`, removing the class of defect in which a parallel plan reading `SESSION_CONTEXT()` on a pool-reset session returns another tenant's rows silently

**AC6 — same-Space by construction**

**Given** a cross-entity reference inside a Space
**When** it is declared
**Then** it uses a composite foreign key carrying `SpaceId`, so a same-Space constraint holds by construction rather than by validation

**AC7 — Role comes off this request's Membership row and nowhere else**

**Given** a Role is needed for an authorisation decision
**When** it is obtained
**Then** it comes from the Membership row read on this request, and never from a cookie, a claim, an API Token payload, or any cache outliving the request

**AC8 — the seeding strategy is stated (carried from the test-design handoff, not from `epics.md`)**

**Given** the RLS seeding problem every isolation test inherits
**When** this story's session wiring lands
**Then** the seeding strategy is **stated in the Completion Notes and is one of the three named shapes** — seed through the application per Space with the context switched between blocks; seed as a principal the RLS policy exempts; or seed before the policies exist and rely on migration ordering
**And** if the exempt-principal shape is chosen, the note states how that principal is prevented from reaching production, because *"a seeding role that bypasses RLS is itself a hole if it ever reaches production"* (`test-design-progress.md:253`)

## Tasks / Subtasks

- [x] **Task 1 — Resolve the bootstrap contradiction, and record the decision before writing code** (AC: 1, 2)
  - [x] **Read *The bootstrap problem* in the Dev Notes first. It is the whole story.** `Membership` already carries a `FILTER PREDICATE` on `SESSION_CONTEXT('SpaceId')` (`20260828164716_InitialSchema.cs:235`). AC1 requires reading the Membership row *to discover* the active Space — a read that happens before any `SpaceId` is in the context, and which that predicate returns nothing for.
  - [x] Three shapes are available and they are laid out with their costs in the Dev Notes. **Option A (set-then-verify) is the recommendation.** Whichever is taken, state in the Completion Notes: which shape, why, what it costs story 1.7, and whether it pre-empts the AD-24 amendment that is readiness issue 3.
  - [x] **Do not resolve this by disabling a policy, opening a second connection, or inventing an exemption.** `AccountRegistrationStore.cs:25-28` names precisely that as the failure AD-24 exists to prevent, *"and that bypass then spreading"*.
  - [x] **Do not put a `SpaceId` on the Session to make it easier.** That trades AC1 of story 1.4 and AD-1. `SessionPrincipal` asserts a one-claim set and `SessionContractTests` plants a Space claim against it.

- [x] **Task 2 — `ActiveSpaceContext`: the type, its ring, and the one place it is populated** (AC: 1, 7)
  - [x] The spine names the type and nothing else — **no interceptor, no middleware, no filter, no ordering is prescribed anywhere.** Constraints on the choice: it fires per unit of work (`ARCHITECTURE-SPINE.md:82`), the session-context call is Infrastructure's (`:42`, `:82`), and `Application`/`Domain` may not see ASP.NET Core types (`:204`, gated twice by `AllowedReferenceEdges` and the package-prefix ban).
  - [x] **The context carries `AccountId`, `SpaceId` and the `Role` read from the Membership row.** AC7 makes that Role the only authorisation input; AD-1 (`ARCHITECTURE-SPINE.md:75`) forbids every other source.
  - [x] **Request-scoped, and it must not outlive the request.** NFR-2 (`quality-budgets.md:20`): *"no cache may outlive a request, so a delay of even one request means a cache was introduced, which is the failure this requirement exists to catch."*
  - [x] **`AccountId` comes from `SessionPrincipal.AccountIdOf(httpContext.User)`** (`Yello.Host/Sessions/SessionPrincipal.cs`). Do not re-parse a cookie, do not add a second claim, do not re-derive it. It returns `Guid?` and returns `null` rather than throwing on a malformed value.
  - [x] **Leave the `AccountId` establishment pluggable.** Story 1.8's AC (`epics.md:868-871`) requires a Token request and a browser request to *"differ only in how `AccountId` and `SpaceId` are established"* and thereafter traverse the identical pipeline. AD-4 (`:100`) says the same. Design the seam now; do not build the Token half.
  - [x] **Not "ambient tenant middleware".** `SchemaNames.cs:26-27`: *"A generic tenant column or ambient tenant middleware is an explicitly rejected shape (`decisions-settled.md:18`, `addendum.md:18`), not a naming preference."* Build `ActiveSpaceContext`; do not name or shape it as tenancy.
  - [x] Vocabulary is a discipline gate: `Space`, `Membership`, `Account`, `Role`. **Never `Tenant`, `Workspace`, `Org`, `Team`, or `User` where `Account` is meant.**

- [x] **Task 3 — Attach resolution to the route group, by shape rather than by a list** (AC: 1)
  - [x] **Copy the pattern story 1.4's review forced.** Anti-forgery began as a hand-enumerated per-route opt-in with no gate; the review's rule is at `Yello.Host/Program.cs:198-207`: attach a cross-cutting filter to the **group** via `AddEndpointFilterFactory`/`AddEndpointFilter`, matched on what the endpoint *is*, *"so a future `POST`/`PUT`/`DELETE` is then protected because of what it is, not because someone remembered. An exception list is where this kind of rule rots."*
  - [x] The matching property here is **the presence of `{spaceId}` in the route pattern**, not a hand-written route list.
  - [x] **Route shape is `/api/v1/spaces/{spaceId}/…`** (`ARCHITECTURE-SPINE.md:267`). There is **no `{spaceId}` segment anywhere in the solution today**, and `v1` is a literal inside each route string — `Asp.Versioning.Http` is pinned but referenced by no project, and story 8.2 owns versioning. Do not wire a versioning package to satisfy this. Route constants live in `Yello.Contracts` so the client and Host share one string; a new one belongs there.
  - [x] Note `SessionSeamTests` already drives a fake `/api/v1/projects` route through the client handler. If the real route shape differs, that test's fixture is the one that notices.
  - [x] **Ordering.** `app.UseAuthentication()` is at `Program.cs:173` and nothing follows it before the endpoints. An unauthenticated request must be refused at the **authentication** boundary and must never enter Space resolution — that is a **401**, and it is not a 404 (story 1.4's Completion Notes state this explicitly; AD-3 draws the 403/404 line at the Space boundary *and nowhere else*).
  - [x] There is no `AddAuthorization` and no `UseAuthorization` in the file today, deliberately (`Program.cs:24-30`). Plain `[Authorize]` with no `Roles` argument is **not** banned by Gate C; `[Authorize(Roles = …)]` fails the build.
  - [x] **Rewrite the comment block at `Program.cs:24-30`.** It predicts this story by name. A comment that predicts a story must be rewritten when that story lands — a review finding in both 1.3 and 1.4.

- [x] **Task 4 — The session-context call, per unit of work** (AC: 2, 5)
  - [x] **Extract `AccountRegistrationStore.SetSpaceSessionContextAsync` (`:167-182`) into one shared implementation rather than writing a second.** Keep the explicit parameter types: the key is `sysname` (`nvarchar(128)`) and the value is `sql_variant`; letting the provider infer them *"sends an `nvarchar(4000)` key into a 128-character parameter, which is a conversion nobody asked for in the one statement the whole isolation model rests on."* Use `SchemaNames.SpaceIdSessionKey`.
  - [x] **A generic per-unit-of-work setter must not double-set with the registration path.** `@read_only = 1` documents the key as unchangeable for the rest of the logical connection, so a *second* `sp_set_session_context` for the same key is expected to fail — **confirm that empirically in Task 8 before designing around it**, per this project's standing rule on unverified engine behaviour. Registration already sets the key inside its own transaction and is unauthenticated with no `ActiveSpaceContext`; a pipeline-level setter that also fires there would break a shipped, green path. Decide how the two coexist — most likely by the setter being driven by a resolved `ActiveSpaceContext` that registration does not have — and assert it.
  - [x] **A unit of work must own its connection open-to-close, and the mechanism is an explicit transaction.** EF Core closes the connection between statements when nothing holds it open, and a closed-and-reopened connection has no session context. `AccountRegistrationStore.cs:81-85` is the shape: `BeginTransactionAsync` → set the context → do the work → commit.
  - [x] **Read *What `@read_only = 1` actually costs* in the Dev Notes.** The key is immutable for the life of the **logical connection**, not the transaction. Two Spaces on one open connection is impossible, and that is a documented consequence, not a bug to work around here.
  - [x] **This is also what makes AC2's "never once per connection" satisfiable**: the context is cleared when the connection returns to the pool and is handed out again. Verify that empirically (Task 8) rather than trusting it.
  - [x] **The single choke point is the point.** AR-3 (`epics.md:191`): *"Cross-cutting invariants — authorisation, Space resolution, refusal recording, idempotency, NFR-8 bound checks — live in the request pipeline and never inside a slice. A slice that re-implements any of them is a defect."* One place sets the context. A second place is the defect.

- [x] **Task 5 — The independent second layer: EF Core global query filters** (AC: 4)
  - [x] **Derived from application state, never from session context.** `ARCHITECTURE-SPINE.md:84`: *"EF Core global query filters restate every policy as an **independent** second layer, derived from application state rather than session context, so neither layer alone carries NFR-1."* A filter that reads `SESSION_CONTEXT()` is the same layer twice and fails AC4.
  - [x] In practice that means the filter closes over the request-scoped `ActiveSpaceContext.SpaceId` held in the `DbContext`, not over a database function.
  - [x] **Every Space-scoped entity gets one**: `Membership`, `StatusDefinition` today. A configuration class must be added to **both** `Yello.Infrastructure/Persistence/Configurations/` **and** `YelloDbContext.OnModelCreating`'s explicit list (`YelloDbContext.cs:79-82`) — `PersistenceModelGateTests.Every_entity_configuration_reaches_the_model` catches the list falling behind.
  - [x] **`Account` and `Space` are the open shape and must stay open.** `deferred-work.md:90` records Lee's 2026-08-28 decision: the spine states no predicate for them, and inventing one belongs to the AD-24 amendment due before epic 3. Do not close it here as a side effect.
  - [x] **Rewrite `YelloDbContext.cs:28-35`**, which names this story's work as deliberately absent. It must stop predicting and start describing.
  - [x] Gate for the raw-SQL half of AC4: this is **A-13** (`test-design-qa.md:364`), an ARCH-level test — *"raw SQL bypassing global query filters only in Infrastructure"*. Glob the whole repository; do not name files.

- [x] **Task 6 — Migration: policies for every Space-scoped table, `MAXDOP = 1`, composite FKs** (AC: 3, 5, 6)
  - [x] **Extend the existing policy rather than creating a second one.** `dbo.SpaceIsolationPolicy` and `dbo.fn_SpaceIsolationPredicate` already exist (`20260828164716_InitialSchema.cs:215-241`) with FILTER + BLOCK AFTER INSERT on `Membership` and `StatusDefinition`. **Both halves are needed** — the migration's own comment: *"a filter alone lets a caller write a row into another Space and merely not see it afterwards, which is the more damaging direction."*
  - [x] Migrations carry **literal** object names, never `SchemaNames` constants — *"an applied migration is a historical record"* (`:208-213`). Drift is *detected* by the schema test, not prevented.
  - [x] **`MAXDOP = 1`: decide and state where it is set** — a migration statement, a database-scoped configuration, or deployment configuration. Neither the spine (`:83`) nor the AC (`epics.md:718`) says. It must survive a fresh database created from migrations, because that is what the test fixture builds. Record the choice.
  - [x] **Composite FKs (AC6): there is no cross-entity reference inside a Space yet, and every FK in the schema today is single-column.** `Membership` carries `SpaceId` and `AccountId` and references nothing Space-scoped. The first real consumer is FR-21's Assignee, `(SpaceId, MembershipId)`, in story 4.6. **Deliver the enabling shape, not an invented reference**: the composite alternate key on `Membership(SpaceId, Id)` that a later composite FK must target — a composite FK needs a matching unique/alternate key on the principal, and `Membership`'s PK is `Id` alone — plus the architecture gate that fails a future Space-scoped reference declared without `SpaceId`. Do not fabricate an entity to hang a foreign key on.
  - [x] Existing relational shape to preserve: `MembershipConfiguration` has a filtered unique `UX_Membership_SpaceId_Owner`, a unique index on `(SpaceId, AccountId)`, FK→`Space` **Cascade** and FK→`Account` **Restrict** (SQL Server refuses two cascade paths). `StatusDefinitionConfiguration` has a unique `(SpaceId, Name)` and FK→`Space` Cascade. Do not disturb the Cascade/Restrict pair.
  - [x] **Generate the migration with the pinned tool.** `PackageVersionPinTests.The_ef_tool_is_pinned_to_the_ef_runtime_version` requires `.config/dotnet-tools.json`'s `dotnet-ef` to equal the `Microsoft.EntityFrameworkCore` pin — **10.0.11**. Run `dotnet tool restore` first. The `.Designer.cs` and `YelloDbContextModelSnapshot.cs` are `// <auto-generated />` and are regenerated, not hand-edited — that exemption does **not** extend to the migration itself.
  - [x] **Every migration needs a hand-editing pass** (`TESTING-CONVENTIONS.md:269-282`, `deferred-work.md:94`): `IDE0005`, `IDE0161`, `MA0197`, `CA1861` and `S138` all fire on the scaffolded file. File-scoped namespace, real documentation instead of `<inheritdoc />`, composite index columns lifted into static fields, `Up` split so no method exceeds 80 lines. **Do not add `// <auto-generated />`.**
  - [x] **The word `Todo` cannot appear in any comment** (SonarAnalyzer S1135 + `TreatWarningsAsErrors`) — and this story touches the default Status set's table.

- [x] **Task 7 — Extend the schema test; do not create a suite** (AC: 3)
  - [x] `SpaceIsolationSchemaTests` already exists at `tests/Yello.Tests.Slices/Accounts/RegisterAccount/SpaceIsolationSchemaTests.cs`, with `Every_table_with_a_SpaceId_column_is_covered_by_the_policy` — a **catalogue-driven** gate that reads `sys.security_policies` / `sys.security_predicates` and therefore already covers tables added later. Extend it; do not write a second one.
  - [x] **AC3's non-nullable half is already gated and needs no new test** — `PersistenceModelGateTests.Every_Space_scoped_entity_has_a_non_nullable_SpaceId` derives from the built model: any entity with a `SpaceId` must have it non-nullable and `Guid`. Cite it rather than duplicating it. (`PersistenceModelGateTests` also asserts `Every_entity_configuration_reaches_the_model` — a configuration must map to a table named **exactly** the CLR type name — and `No_key_is_generated_by_the_database`.)
  - [x] **A new Space-scoped table must be added to `SchemaNames.SpaceScopedTables` (`:106-107`) *and* get both FILTER and BLOCK predicates**, or the schema test fails in two places.
  - [x] This test is **A-5** (`test-design-qa.md:303`), P0, and it stays in `Yello.Tests.Slices` rather than `Architecture` — story 1.3's Note 11: asserting a migrated schema needs a real SQL Server, and the architecture suite *"takes seconds and should fail before anything slower starts"*.
  - [x] **Do not populate `Yello.Tests.Isolation`.** AD-4 requires every isolation case to run on **both** surfaces and the API-token surface does not exist until story 1.8. Story 1.9 owns that suite. Adding one case also means removing `--ignore-exit-code 8` in the same change, which `Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code` gates in both directions.

- [x] **Task 8 — Prove AC5's behaviour here; leave I-8's harness to 1.9** (AC: 2, 5)
  - [x] **Read *The pooled-connection case is split* in the Dev Notes.** The behaviour is this story's AC; the isolation-suite case (**I-8**) needs its own collection, parallelism disabled, pool size 1 and its own container — all assigned to story 1.9 (`test-design-architecture.md:329-337`, `TESTING-CONVENTIONS.md:347-350`).
  - [x] Assert it here with a **locally pinned pool** (`Max Pool Size=1`) in a class of its own, so the behaviour is evidenced now and 1.9 inherits a passing fact rather than an open question. Do not settle the container topology — that is 1.9's (`framework-setup-progress.md:288`).
  - [x] **Reuse the seams story 1.3 already built** rather than improvising: `RegistrationDatabase.OpenForSpaceAsync(Guid spaceId, ct)` opens a connection and sets the context; `OpenWithoutSpaceContextAsync` opens one without; `CountAsync` and `ReadEveryTextValueAsync` are the assertions. `RegisterAccountIntegrationTests.Space_scoped_rows_are_invisible_without_a_session_context` already proves the policy works with no context and under another Space's context — extend that line of evidence rather than restating it.
  - [x] **Verify the two SQL Server behaviours this design rests on, empirically.** `deferred-work.md:185` flags them as *"inferred from documented SQL Server semantics rather than executed"* and asks for *"ten minutes against a live server"*. They are: that `sp_reset_connection` on pool reuse clears `SESSION_CONTEXT`, and that `@read_only = 1` refuses a second set on the same logical connection. Story 1.3 verified three such claims empirically and the standing warning is `review-web-verification.md:40-46` — *"treat any assertion about SQL Server engine behaviour in this project as unverified until checked"*, two already caught wrong.
  - [x] **The assertion must not compare a value against a default.** The plant that passed during the framework run compared `"en"` with `"en"` because the feature was off and the default agreed. **For this story that shape is acute: a query-filter test where the ambient `SpaceId` happens to equal the row's `SpaceId` asserts nothing.** Use two Spaces with different ids in every isolation-flavoured assertion.

- [x] **Task 9 — Decide B2 and write it down** (AC: 8)
  - [x] Three shapes, verbatim at `test-design-progress.md:249-253`; pick one and state it. This is the only story-1.5 row in the test-design handoff's acceptance table (`handoff.md:87`), and story 1.9's fixtures are written against whatever is chosen.
  - [x] If the exempt-principal shape is chosen, state how it is kept out of production.

- [x] **Task 10 — Plants, and the record of them** (AC: all)
  - [x] Every absence assertion this story adds gets a **planted violation, confirmed to fire by name, recorded in a table** (`TESTING-CONVENTIONS.md:111-114`). Story 1.4 planted ten and kept four as permanent tests; reproduce that table shape.
  - [x] **Plant against the scanner, not the helper the scanner calls** — 1.4's review found a plant that validated the predicate rather than the scan.
  - [x] **A planted-oracle test must actually exercise the production code path** — 1.4's review found one that never called `SessionPrincipal.For`.
  - [x] **Never `git checkout --` to revert a plant.** Hold the original in memory and restore in a `finally`.
  - [x] Suggested plants: a Space-scoped table with no policy; a query filter reading `SESSION_CONTEXT()` instead of application state; raw SQL outside `Infrastructure`; a second place setting the session context; a slice resolving the Space itself; a `SpaceId` made nullable.

- [x] **Task 11 — Housekeeping sweep** (AC: all)
  - [x] Six source comments currently predict this story and must be rewritten to describe it: `YelloDbContext.cs:28-35`, `AccountCredentialStore.cs:11-19`, `Yello.Host/Program.cs:24-30`, `SessionEndpoints.cs:31-38`, `SessionPrincipal.cs:17-26`, `20260828164716_InitialSchema.cs:197-206`.
  - [x] Close or rewrite `deferred-work.md:90` (the `Account`/`Space` policy question) — at minimum record what this story learned about it. Close `deferred-work.md:94` for this story's migration.
  - [x] **Do not add review narration to source files** (`deferred-work.md:60`, addendum `:213` — owned by Lee, once, for the project). **Do not tick this checkbox unless the diff actually holds it** — 1.4 ticked it while adding six more instances, and the review said so.
  - [x] A story-1.5 log starts after 1.4's block — check `SessionLog.cs` for the highest EventId in use. `[LoggerMessage]` source-generated partials only (CA1848/CA1873 are errors). **Log `SpaceId` as a field, never the Space name** (AR-34). Never log the cookie.

## Dev Notes

### The bootstrap problem — read this before anything else

AC1 requires resolving `ActiveSpaceContext` **by reading the Membership row** for `(AccountId, SpaceId)`. AC2 requires the session context to be set **from `ActiveSpaceContext`** and *"never from a client-supplied value"*. But `Membership` already carries a filter predicate requiring the context to name the Space:

```sql
CREATE FUNCTION dbo.fn_SpaceIsolationPredicate(@SpaceId uniqueidentifier)
RETURNS TABLE WITH SCHEMABINDING AS RETURN
    SELECT 1 AS IsAccessible
    WHERE @SpaceId = CAST(SESSION_CONTEXT(N'SpaceId') AS uniqueidentifier);
```
— `20260828164716_InitialSchema.cs:220-226`, applied to `Membership` at `:235`.

So the read that *establishes* the context is hidden by the policy the context *unlocks*. This is circular as written, and it is already recorded: `deferred-work.md:90` notes that `Membership` *"has a `SpaceId` policy today, and story 1.7's Space switcher has to read a person's Memberships across Spaces, which that predicate makes impossible."* The only mention of `SESSION_CONTEXT('AccountId')` anywhere in the repository is a comment at `20260828164716_InitialSchema.cs:203`.

**Three shapes, with what each costs.**

**Option A — set-then-verify (recommended).** Set `SESSION_CONTEXT('SpaceId')` to the `{spaceId}` from the route at the start of the unit of work, then read the Membership row for `(AccountId, thatSpaceId)` under the policy. The filter means the read can only ever see Memberships in that one Space; if the Account's row is there it is a member, and if it is not the request is refused. One predicate, no schema change, no AD-24 amendment.
- *The objection:* the value comes from the route, which is literally client-supplied. *The answer, and it must be written down rather than assumed:* the spine itself makes `{spaceId}` the designed input — `ARCHITECTURE-SPINE.md:267`, *"`spaceId` is always the first path segment after the version; **it is the input to AD-1**"*. What AR-5 forbids is serving data on a Space the client named without a Membership check; Option A performs that check before anything else runs. AD-2's own stated rationale for the prohibition is the *pooled connection* (`:82`), not the route.
- *The real risk, and it must be closed by construction:* between setting the context and completing the Membership check, the connection names a Space the caller may not belong to. Within one unit of work, with resolution as the first thing that happens and the request dying on failure, that window is closed — **but it has to be closed by a single choke point, not by ordering discipline.** *"A rule that relies on discipline is not a rule here"* (`review-rubric-walker.md:26-28`).

**Option B — resolve under an `AccountId` predicate.** Widen `Membership`'s predicate to admit `SESSION_CONTEXT('AccountId')`, set that key first, read the Membership row, then set `SpaceId`. Honours AR-5's letter exactly. `@read_only` is per key, so setting two different keys on one connection is fine. But it pre-empts the AD-24 amendment that is readiness issue 3 and due before epic 3, and it widens `Membership`'s isolation for every other query in the product. It is also what story 1.7 will need regardless — so choosing it here is bringing 1.7's decision forward, which is a decision for Lee, not for this story.

**Option C — an exempt principal or a second connection.** Ruled out by name. `AccountRegistrationStore.cs:25-28` and AD-24 (`ARCHITECTURE-SPINE.md:220`) both name *"disabling a policy, opening a second connection or inventing a bypass — and that bypass then spreading"* as the failure the decision exists to prevent.

**This is question 1 for Lee.** It is a deliverable of this story either way.

### What `@read_only = 1` actually costs — verified, not assumed

Two facts, checked against Microsoft's documentation on 2026-09-19 because this project has twice shipped a plausible-but-wrong SQL Server claim (`review-web-verification.md:40-46`):

1. **`@read_only = 1` locks the key for the life of the *logical connection*, not the transaction.** The only way to change it is to close and reopen. (`sp_set_session_context`, Microsoft Learn.)
2. **`sp_reset_connection`, which ADO.NET issues when a pooled connection is handed to a new consumer, clears `SESSION_CONTEXT`.** (`SESSION_CONTEXT`, Microsoft Learn.)

Together they make AC2 satisfiable and they fix the design: **a unit of work must own its connection open-to-close.** Set the key once on a freshly-taken connection, work, release; the pool clears it on the next hand-out. That is also why the existing call sits inside `BeginTransactionAsync` (`AccountRegistrationStore.cs:81-85`) — EF Core closes the connection between statements when nothing holds it open, and a reopened connection has no context at all.

The consequence already recorded: two Spaces cannot be written on one open connection, which is why story 4.3 cannot add the invited Membership on registration's unit of work (`deferred-work.md:185`). That is a real constraint for 4.3, not a defect to fix here.

One further constraint worth knowing: **`@read_only` cannot be set while multiple MARS batches are active** on the connection.

**Both facts are documentation, not measurement.** Task 8 requires executing them. `deferred-work.md:185` explicitly asks for it.

### The acceptance criterion the epic does not carry

`epics.md` gives this story seven AC blocks. The test-design handoff gives it an eighth, and it is the only story-1.5 row in that table:

> | **1.5** | B2 — the RLS seeding strategy is stated, and it is one of the three named shapes | Every isolation test depends on it; discovering it in 1.9 means rewriting fixtures |
> — `test-design/YelloBMAD-handoff.md:87`

The full statement of the problem is `test-design-progress.md:239-255`:

> Every isolation test needs at least two Spaces populated, then a request made as an Account holding Membership in only one. But AD-2 filters every Space-scoped table on `SESSION_CONTEXT('SpaceId')` … Seeding Space B's data while the session context says Space A will silently write nothing visible, or fail — and AD-2 forbids raw SQL bypassing the global query filters outside `Infrastructure`.
> So the seeding strategy has exactly three shapes, and the choice belongs in story 1.5 (which introduces the session wiring) rather than being discovered in story 1.9 … **because a seeding role that bypasses RLS is itself a hole if it ever reaches production.**

Note the interaction with the BLOCK PREDICATE: seeding is a *write*, and `BLOCK … AFTER INSERT` refuses any row whose `SpaceId` the context does not name. Whichever shape is chosen has to satisfy the block half, not just the filter half.

### The pooled-connection case is split between this story and 1.9

AC5 states the behaviour. The test design puts the *harness* elsewhere:

- **I-8** (`test-design-qa.md:272`) — *"pooled-connection reuse across two Spaces on one physical connection"*, INT level, **"own collection, parallelism off, pool size 1"**.
- Owner of that collection isolation: **story 1.9** (`test-design-architecture.md:337`), whose reasoning is worth quoting because it is the trap: *"intermittent cross-test interference, and the case most likely to be quarantined is the one guarding a silent tenancy leak."*
- `TESTING-CONVENTIONS.md:347-350`: *"the pooled-connection isolation case in story 1.9 needs its own container, with pool size pinned to 1 and parallelism disabled. A pooled connection carrying a stale session context is the thing that case exists to catch, and it cannot be observed on a shared pool."*

**The resolution this story takes:** assert the behaviour now, in its own class, against a connection string with `Max Pool Size=1`, so AC5 is evidenced rather than asserted. Do **not** build it in `Yello.Tests.Isolation`, do **not** settle the container-sharing topology, and do **not** claim the I-8 case is done. **This is question 3 for Lee** if the dev agent thinks the split is wrong.

`MAXDOP = 1` is set here. Whether it can ever be *relaxed* is AR-40c and belongs to story 1.10 (`epics.md:353`, `:427`) — and the standing rule is `test-design-architecture.md:139-141`: *"treat `MAXDOP = 1` as unrelaxable until Azure SQL's exposure is confirmed **and** the pooled-connection isolation case is green."*

### Container memory: this story is the one the warning was about

`TESTING-CONVENTIONS.md:327-332`, measured 2026-09-18: a full `dotnet test Yello.slnx` starts **nine** SQL Server engines, ~2 GB each, in parallel; during story 1.4's run *"the operating system killed a background process for low memory"*. Docker on this machine reports 16.6 GB and Rancher Desktop runs a Kubernetes control plane in the same allocation. **"The next story to add a container-backed class is the one that finds out."**

This story adds container-backed classes — the schema test extension and the pooled-connection class at minimum. Budget for it, and if a run behaves strangely, check memory before suspecting the tests. Keep any class that needs no database **out** of the fixture, exactly as `PasswordVerificationDurationTests` is.

Also: **a stopped container runtime produces failures, not skips** — measured 74 total / 52 succeeded / 22 failed / 0 skipped. `IsContainerRuntimeAvailable()` performs no I/O, so a configured-but-dead backend reports available. Story 1.9 owns the fix. Rancher Desktop recovery: `rdctl shutdown`, `wsl --terminate rancher-desktop`, relaunch — `rdctl start` no-ops when the app is running with its backend down.

### Why "a `SpaceId` column on every table" is not the rejected shape

Worth stating, because the storage layer this story builds looks exactly like the thing the spec ruled out. `decisions-settled.md:16` lists the rejected alternative as *"Tenancy becomes uniform: every table gets a tenant id, every query the same filter, no rule ever spans two features."* What makes Yello's version legitimate is `decisions-settled.md:20`:

> Membership is many-to-many with the Role carried on the relationship, so authorisation is a function of `(Account, Space)` rather than a property of the Account. One person legitimately holds different standing in different Spaces at the same time, which means the active Space must be resolved *before* any authorisation decision, no requirement can be expressed as "an Admin can X", and isolation is a rule about a relationship rather than a filter applied uniformly.

The column is uniform; **the authorisation is not**, because the Role comes off the Membership relationship per request. That distinction is what AC7 protects, and it is why naming anything here `Tenant` would be a discipline violation rather than a synonym.

### What this story does NOT build

| Not here | Owner | Source |
|---|---|---|
| The 403/404 shape, the identical bodies, capability-shaped 403 copy | 1.6 | `epics.md:736-744` |
| `AccessRefusal` rows, `CrossSpace`/`InsufficientRole`, 90-day purge | 1.6 | AR-11, `epics.md:751-754` |
| RFC 9457 problem bodies as the refusal contract | 1.6 | `epics.md:761-764` |
| The `{spaceId}`-in-path **architecture gate** | 1.6 | AR-9, `epics.md:756-759` |
| Exercising the FR-16 Role capability matrix | 1.6 | `epics.md:746-749` |
| The NFR-8 bound registry, all six entries | 1.6 | AR-30, `epics.md:766-785` |
| Rate limiting on the two unauthenticated PBKDF2 endpoints | 1.6 | `deferred-work.md:155` (severity **high**) |
| `AccountScopedContext`, `SESSION_CONTEXT('AccountId')`, the switcher, the context bar, the Role chip | 1.7 | AR-29, `epics.md:809-827` |
| `ApiToken`, and the Token half of `AccountId` establishment | 1.8 | `epics.md:844-875` |
| The isolation suite itself, on both surfaces; I-8's collection; container topology | 1.9 | AR-35, `epics.md:885-915` |
| Confirming Azure SQL's exposure; relaxing `MAXDOP`; migrations as a deploy job | 1.10 | AR-40c, AR-36, `epics.md:925-956` |
| Idempotency (AR-25) | unassigned, *"most plausibly 1.6"* | `deferred-work.md:76` |

AC1 requires a **refusal**; 1.6 decides its *shape* and writes its *record*. Build the refusal as a seam 1.6 can hang both on. The spine's own line resolves what the refusal is: `ARCHITECTURE-SPINE.md:75` — *"A request whose Space context cannot be resolved — no such Space, or no Membership in it — is refused with **404** (AD-3), never defaulted and never distinguished."*

Note also `deferred-work.md:127`: NFR-8's *"50 concurrent active Sessions per Space"* is owned by 1.6, but *"it is a derived count over something else — most plausibly active Space resolutions, which is story 1.5's territory."* **Do not resolve it by putting a `SpaceId` on the Session.** If this story's resolution point is the natural counting seam, say so in the notes and leave it to 1.6.

### Previous story intelligence

**Story 1.4 left this story a specific opening move.** Its Completion Notes: *"No `AddAuthorization`, no `UseAuthorization`, no policy and no `[Authorize]` — authorisation is 1.5's and 1.6's, and AD-1 makes every authorisation decision a per-request `(Account, Space)` resolution nothing here can perform."*

**Story 1.4 could not prove two of its own clauses, and this story can.** *"There is no Space-scoped surface for it to fail to grant access to until story 1.5, so the Membership clause is not proved."* And on AC2: *"What is not asserted is an actual switch, because no Space switcher exists until 1.7 and no Space-scoped endpoint until 1.5."* The first Space-scoped endpoint this story creates makes 1.4's AC3 Membership clause testable for the first time.

**The defect class this repository keeps rediscovering**, from 1.4's Dev Notes: *"Stories 1.1, 1.2 and 1.3 each needed multiple review passes, every time for the same reason — 'gates that asserted less than they claimed', 'the fixes that repeated the defect they fixed'."* Three standing remedies: glob the repository rather than naming files; plant a violation a later story would plausibly write and confirm the gate fires by name; and **do not write a completion note claiming a gate covers more than it does** — story 1.3's review found four instances of that in a story actively watching for it.

**Review rules from 1.3 and 1.4 that bear directly here:**
- Attach cross-cutting filters at the **group**, by shape, never a hand-written route list (`Program.cs:198-207`).
- A configuration fallback must **log the resolved value unconditionally**, not just the failure (`Program.cs:159-171`). If `MAXDOP` or a pool setting becomes configurable, this applies.
- **Never match a broad error class and report a narrow cause** — a unique violation anywhere was reported as "address already registered", returning 204 with nothing written.
- **EF Core's command log is an AD-23 disclosure and stays filtered** (`Program.cs:56`). It is unconditional and covers this story's queries too. **Do not remove it, and do not assume you can read SQL from the logs in a test.**
- A leaked `DbContext` was found *"in the one class whose fixture remarks make a point of connection lifetime"* (`RegisterAccountIntegrationTests.cs:303`). This story's pooled-connection work is in exactly that territory.

### Git intelligence

Last four commits: `4abd4b7` (1.4 review close), `641f399` (a container-figure correction that story 1.9 sizes its topology decision from), `076ecc7` (1.4 implementation, 53 files), `3aff5e4` (1.4 story context). Story 1.3's implementation (`15a7de1`) is where `YelloDbContext`, `SchemaNames`, the configurations, the initial migration and its RLS DDL all landed — read that commit's diff for the RLS half specifically.

Commit discipline: subjects are a sentence stating the thesis, no prefix and no ticket id; bodies 20–50 lines carrying the dominant finding, empirical demonstrations, Lee's numbered decisions, patch count, plant results and exact build/test numbers. **Straight to `main`, no branches, no PRs.** One commit per workflow phase: story context first (story file and `sprint-status.yaml` only), then implementation, then one per review round.

### Project Structure Notes

**Stack, as actually pinned** (`Directory.Packages.props`, `global.json`, `Directory.Build.props`): SDK **10.0.303** `rollForward: latestPatch`, `net10.0`, `RuntimeFrameworkVersion` **10.0.11**. EF Core / `.Design` / `.SqlServer` **10.0.11**; `Microsoft.Data.SqlClient` **7.0.2** (referenced by `Yello.Host` and `Yello.Tests.Slices` only); Identity.EntityFrameworkCore 10.0.11; Aspire 13.4.6; xunit.v3 4.0.0; Testcontainers 4.6.0; ArchUnitNET 0.13.3; `Asp.Versioning.Http` 10.0.0 **pinned but referenced by nothing**. `Opinionated.DotNet.CodingStandards` 0.0.11 is the one solution-wide `GlobalPackageReference`; **warnings are build errors**. `Microsoft.EntityFrameworkCore.InMemory` and SQLite are deliberately un-pinned, so a project referencing one fails to restore — the ban is a gate, not a convention. The SQL Server image and database resource name come from `Directory.Build.props` as assembly metadata; **no source file may state either as a complete string literal**.

**New, most likely:**
- `Yello.Application/…/ActiveSpaceContext.cs` and the pipeline component that populates it — ring and folder per the shape chosen in Task 2. `Yello.Application` may not reference ASP.NET Core.
- `Yello.Infrastructure/Persistence/` — the session-context setter, extracted so `AccountRegistrationStore` and the per-request path share one implementation rather than two.
- `Yello.Host/` — the endpoint filter attaching resolution to the group, and whatever first Space-scoped route is needed to exercise it.
- A new migration under `Yello.Infrastructure/Persistence/Migrations/`.
- `tests/Yello.Tests.Slices/…` — the pooled-connection class; `tests/Yello.Tests.Architecture/…` — A-12 and A-13.

**UPDATE — read each before editing:**
- `Yello.Infrastructure/Persistence/YelloDbContext.cs` — 86 lines. Four `DbSet`s, an explicit configuration list at `:79-82`, no query filters, no interceptors, no `SaveChanges` override. `:28-35` names this story's work as absent.
- `Yello.Host/Program.cs` — 226 lines, order documented in place. `UseRequestLocalization` → `UseCors` → startup logging → `UseAuthentication()` (`:173`) → connectivity check → `WarmPasswordHashing()` → route group with the antiforgery filter (`:207`) → four `Map*` calls. **Nothing sits between `UseAuthentication()` and the endpoints.**
- `Yello.Infrastructure/Persistence/AccountRegistrationStore.cs` — the only existing `sp_set_session_context` call (`:167-182`), inside a transaction (`:81-85`).
- `Yello.Infrastructure/Persistence/SchemaNames.cs` — `SpaceIdColumn`, `SpaceIdSessionKey`, `SpacePredicateFunction`, `SpaceIsolationPolicy`, `SpaceScopedTables => [Membership, StatusDefinition]` (`:106-107`). `S2339` refuses `public const`; every shared name is a `public static string` property.
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/SpaceIsolationSchemaTests.cs` — extend it. `RegistrationDatabase.cs` beside it holds `OpenForSpaceAsync` / `OpenWithoutSpaceContextAsync` / `CountAsync` / `ReadEveryTextValueAsync`.
- `Yello.Infrastructure/Persistence/Configurations/{Membership,StatusDefinition}Configuration.cs` — where the query filters go.
- `Yello.Infrastructure/InfrastructureServices.cs` — the only DI entry point, and where new registrations belong. Adapters stay `internal`; `InternalsVisibleTo("Yello.Tests.Slices")` is already declared.
- `Yello.Contracts/Accounts/*Routes.cs` — the route constant.
- `Yello.AppHost/Program.cs` — **no `MAXDOP`, no database-level settings, no collation, no init script exists**. The database is a bare `AddDatabase`. If `MAXDOP` is set outside a migration, this is the other candidate — and a second AppHost→Host configuration key is spelled twice by necessity (`ReferenceOutputAssembly=false`), which is why `CrossOriginConfigurationTests` IL-scans for the exact literal. Copy that pattern.

**Client:** `MainLayout.razor:10-13` states there is no context bar, no Space switcher and no Role chip, and that *"the first Space-scoped surface arrives with story 1.5. Story 1.7 owns the chrome."* This story need not touch the client at all beyond whatever the first Space-scoped call requires; if it does, `deferred-work.md:143` ("no browser has driven sign-in end to end") is inherited.

**Ring edges are asserted as exact equality in both directions** (`tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs`). Adding one is *"the visible moment the dependency rule changes"* — an architecture edit, taken deliberately. `Yello.Domain`, `Yello.Application`, `Yello.Contracts` and `Yello.Merge` may not reference anything beginning `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.`, `Microsoft.Data.SqlClient`, `Aspire.`, `Testcontainers` or `Dapper`.

**Endpoints** are named static classes with named methods — never lambdas or local functions in `Program.cs`; story 1.1 found that deleting an entire startup call left 52 of 52 tests green. Pattern at `SessionEndpoints.cs:40-64`. A marker class needs a member (`S2094` refuses an empty one).

### ATDD artifacts — red-phase scaffolds already in the tree

An ATDD run on 2026-09-20 put **nine skipped scaffolds** in the repository. They compile, the gate is green (`253 total / 243 passed / 10 skipped`, up from 244/243/1), and each carries `[Fact(Skip = …)]` naming its AC. **Activate one at a time, confirm it fails for the stated reason, then implement.**

- Checklist, activation order and evidence: `_bmad-output/test-artifacts/atdd-checklist-1-5-resolve-the-active-space-per-request-enforced-in-the-database.md`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceIsolationLayerTests.cs` — 6 cases (AC2, AC4, AC5, AC6, AC8)
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/PooledConnectionIsolationTests.cs` — 1 case (AC5), own container, `Max Pool Size=1`
- `tests/Yello.Tests.Architecture/SpaceResolutionSeamTests.cs` — 2 gates (AC2, AC4 / test id A-13)

Four things worth knowing before touching them:

- **Activate `A_read_only_session_key_is_cleared_by_the_pool_and_refuses_a_second_set` first.** It needs no product code and settles the two SQL Server behaviours this whole design rests on — the ones `deferred-work.md:185` records as never executed. If either is wrong, the design changes before anything is built.
- **Two are green today** (`Exactly_one_production_call_site_…`, `No_production_code_outside_Infrastructure_…`). They are standing regression gates, not red assertions, and Task 10's plants are what turn them into tests.
- **Skipped classes cost no container.** Measured: a filtered run of both new Slices classes took 1.5 s and started no engine. The two extra engines arrive on activation, not now.
- **The HTTP cluster is deliberately not scaffolded.** AC1's request-level cases need a Space-scoped resource this story has not chosen, and a request to a non-existent route returns 404 — indistinguishable from AC1's expected refusal, so those tests would pass today for the wrong reason. When written, anchor each on the positive case first.

### Testing requirements

**Six projects, five gates.** `Yello.Tests.Slices` is not a gate. `Yello.Tests.Shared` is not a suite. **Later stories add cases to existing suites rather than creating suites.**

- Slice / integration / endpoint / **migrated-schema** cases → `tests/Yello.Tests.Slices/`
- Structural gates (IL scans, file-corpus scans) → `tests/Yello.Tests.Architecture/`
- **Not** `Yello.Tests.Isolation` — see Task 7.

**Traits**, four names only: `Suite`, `Priority`, `Requirement`, `Assumption`. Cite **the AR id and the AD id** — e.g. `AR-5`/`AD-2`. If a test fixes the route shape, tag it `Assumption=PRD-12-8` (API version by URL path segment); the value must name its source document or `dotnet test --filter "Assumption~PRD-12"` cannot find it, and `TestingConventionTests` asserts the format.

**Named cases this story delivers or enables:** **A-5** (P0, ARCH — every Space-scoped table has an RLS policy) is AC3's gate and already exists to extend. **A-13** (P1, ARCH — raw SQL bypassing global query filters only in `Infrastructure`) is AC4's. **A-12** (P1, ARCH — no slice re-implements authorisation, Space resolution, refusal recording or idempotency) is AR-3's and this story is the first that can violate it. **I-8** is AC5's behaviour, harness deferred to 1.9.

**Standing rules:** one behaviour per test, named as a sentence with underscores. **No `Task.Delay` as synchronisation, ever** — inject `TimeProvider` (registered at `Program.cs:110`) or construct an already-expired artefact, as 1.4 did by starting the Host with a negative lifetime. Randomise every email address; **never a shared literal**. **Cleanup by transaction rollback or container disposal, never delete statements** — *"those would themselves need an RLS session context to see the rows they are trying to remove, so a cleanup that 'works' may be evidence that isolation is broken."* No coverage threshold; do not invent one.

**Container fixture:** `IClassFixture<MigratedDatabaseFixture>` (at `tests/Yello.Tests.Slices/Accounts/RegisterAccount/MigratedDatabaseFixture.cs` — it lives in that folder, not a shared root; 1.4 reused it across folders rather than duplicating it). Every test opens with `Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason)`. Never an EF in-memory provider, never SQLite including `:memory:` — no `CREATE SECURITY POLICY`, no `SESSION_CONTEXT`, so it fails the ban's reason while passing its letter.

**Endpoint tests boot the real Host as a process** via `HostProcess.RunAsync` over `https://127.0.0.1:0` — no `WebApplicationFactory`, no `Microsoft.AspNetCore.Mvc.Testing`, no central pin for one. Needs `dotnet dev-certs https` created but not trusted. The framework cookie container is **off** (`UseCookies = false`); attach cookies by hand via `tests/Yello.Tests.Slices/WireSession.cs`. **A test that signs in and then posts must re-fetch the anti-forgery token** — a token minted while anonymous is refused once a Session exists.

**Runner:** xunit.v3 4.0.0, Microsoft.Testing.Platform only, xunit's own `Assert` (no FluentAssertions, no Shouldly). Exit code 8 means zero tests; remove `--ignore-exit-code 8` the moment a suite gains its first test, gated both ways.

Baseline to beat: `dotnet test` was **244 total / 243 passed / 1 skipped** at the close of 1.4's review.

### Coding-standard traps reachable from this story

`S1135` — the word `Todo` in a comment fails the build. `S2339` — no `public const`. `S109` — magic numbers into named private consts. `CA1848`/`CA1873` — `[LoggerMessage]` partials only. `S4457` — split argument checks from the async body. `MA0182` — a type only reflection reaches is reported unused, which is why `OnModelCreating` names configurations explicitly. Banned APIs: `DateTimeOffset.Now`, `StringComparison.InvariantCulture*`, `new CultureInfo(string)`, all `Newtonsoft.Json`, `Enum.TryParse` without `ignoreCase`, `System.Tuple<>`, `Run` over `RunAsync`. There is no `.editorconfig` and no `Directory.Build.targets`, both deliberate; the only narrowing lever is `#pragma warning disable` with a stated reason. **Do not restate a coding-standard property in `Directory.Build.props`** — it is imported before the standards package and silently forks the standard. **Do not edit `Directory.Packages.props` casually** — `PackageVersionPinTests` asserts an exact set, and an AR-1 pin change amends `epics.md` first.

### The gates that fail on a change you did not think was gated

The architecture suite holds several **hand-maintained lists asserted as exact sets in both directions**, so a perfectly reasonable addition fails the build until the list is updated in the same change:

- **A new package** → `Directory.Packages.props` pin **and** `PackageVersionPinTests.ExpectedPins` (if it is an AR-1 package) or `ExpectedNonAr1Pins` (with the reason stated). No inline version anywhere. A duplicate, conditional, include-less or version-less `<PackageVersion>` **throws** rather than failing soft. An AR-1 pin change amends `epics.md` first.
- **A new project reference** → `AllowedReferenceEdges.Table`, exact equality, extra *or* missing edges fail. Inner rings additionally carry `ForbiddenPackagePrefixes`.
- **A new project** → `Yello.slnx`, `AllowedReferenceEdges.ProductionProjects`/`TestProjects`, `ProductionAssemblies.All` (hand-maintained), and `SolutionInventoryTests`' "exactly the fourteen named projects and the one declared variance". It must also restore — `MsBuildEvaluation` shells `dotnet msbuild` for every project and **throws** on failure, which breaks the whole architecture suite rather than one test.
- **A new MSBuild property a gate reads** → `MsBuildEvaluation.PropertyNames`.
- **A new failure-code class** → `LocalisationGateTests` reflects over **one named type per test** (`RegisterAccountFailure`, `AuthenticateAccountFailure`). A Space-resolution failure class is invisible to the gate until a third test and its own fields list exist. Every public static code needs a `ClientCopy.resx` message.
- **A wildcard/globbed `Compile` or `PackageReference` include** → `ProjectFileGateTests.No_gated_item_hides_behind_an_MSBuild_property` bans `$(`, `@(`, `%(` and `%` in any gated `Include`. No `<Reference>` and no `<Import>` at all.
- **Client-side caching of a Space** → `ClientStorageBanTests` IL-scans `Yello.Client` and text-scans `wwwroot/**` for `localStorage`, `sessionStorage`, `indexedDB`, `openDatabase`.
- **A Space or Role on the Session** → `SessionContractTests` asserts the claim set is exactly `[AccountIdClaimType]` and plants `urn:yello:claim:space-id` against it. This is a build failure by construction, which is what forces `ActiveSpaceContext` to be per-request state.

The Role-API ban is also wider than its obvious names: `RoleApiScan` bans the role-claim URI **as a string literal in any assembly including tests**, any type under `Microsoft.AspNetCore.Identity` or `.Authorization` whose name contains "Role", `UserManager<>`'s seven role methods, and the `set_Roles` object-initialiser form. **`[Authorize]` with a *policy* is allowed; roles are not. Yello's own `Role` enum is fine.**

If `ActiveSpaceContext` lives in `Application` or `Domain` and touches `IHttpContextAccessor`, `HttpContext`, `DbContext` or any `*EntityFramework*` type, it fails `RingDependencyTests` and the package-prefix ban. **Declare the port inward; implement it in `Infrastructure` or `Host`.**

### Vocabulary

PRD §2 Glossary verbatim, in code, identifiers, namespaces, folders and copy: `Account`, `Session`, `Space`, `Membership`, `Role`, `Owner`, `Admin`, `Member`, `Viewer`, `User` (an Account acting inside a Space). **Forbidden: `Workspace`, `Tenant`, `Org`, `Organisation`, `Team`, and `User` where `Account` is meant.** There is no `Users` table. Synonyms are a discipline violation, not a style preference.

### References

- Story and AC — `epics.md:687-726`; coverage row `epics.md:348`; epic framing `epics.md:421`, `:427`; "Access Control is not an epic" `epics.md:415`
- AR-3, AR-5, AR-6, AR-7, AR-8, AR-9, AR-34, AR-35, AR-36 — `epics.md:191`, `:193-197`, `:222-224`
- AD-1 (`ActiveSpaceContext`, Role source, Role-API ban) — `ARCHITECTURE-SPINE.md:71-75`
- AD-2 (RLS, session context, MAXDOP, composite FKs, second layer) — `ARCHITECTURE-SPINE.md:77-86`
- AD-3 (403/404, `{spaceId}` in path, the timing case) — `:88-94`; AD-4 (both surfaces) — `:96-100`; AD-24 (the two Account-scoped surfaces, the bypass ban) — `:218-222`
- Conventions: slices `:266`, endpoints `:267`, ids `:268`, dates `:269`, errors `:270`, mutation `:271`, logging `:274`, migrations `:275`, tests `:276`; source tree `:381-402`; denormalisation `:377`
- FR-15 — `prd.md:321-330`; NFR-1 — `quality-budgets.md:9-16`; NFR-2 — `:18-28`; NFR-5 — `:42-48`; NFR-7 — `:59-63`
- CAP-9 — `acceptance-criteria.md:94-99`; CAP-15 — `:154-162`; CAP-2 — `:25-26`; CAP-36 — `:360-363`
- `decisions-settled.md:7`, `:16`, `:18`, `:20` (the rejected uniform-tenancy shape and why this is not it)
- `harness-constraints.md:20` (P1 — multi-tenant isolation as the invariant every story must respect)
- B2 — `test-design-architecture.md:109`, `:311`; `test-design-progress.md:239-255`; `test-design-qa.md:90`, `:479`; `handoff.md:87`
- I-8 and its collection isolation — `test-design-qa.md:272`; `test-design-architecture.md:329-337`; `TESTING-CONVENTIONS.md:347-350`
- A-5, A-12, A-13 — `test-design-qa.md:303`, `:363`, `:364`
- R5 / AR-40c — `test-design-architecture.md:215`, `:135-141`, `:480-488`; `epics.md:228`; `ARCHITECTURE-SPINE.md:427`
- Live code — `20260828164716_InitialSchema.cs:190-241`; `AccountRegistrationStore.cs:12-56`, `:81-85`, `:167-182`; `YelloDbContext.cs:28-35`, `:79-82`; `SchemaNames.cs:26-27`, `:60-107`; `Program.cs:24-30`, `:56`, `:173`, `:198-207`; `SessionPrincipal.cs`; `Membership.cs:14-25`
- Conventions — `tests/TESTING-CONVENTIONS.md`, whole file; especially `:107-114`, `:269-282`, `:284-359`, `:366-410`
- Ledger — `deferred-work.md:8`, `:60`, `:76`, `:90`, `:94`, `:127`, `:155`, `:185`
- Microsoft Learn — [`sp_set_session_context`](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-set-session-context-transact-sql), [`SESSION_CONTEXT`](https://learn.microsoft.com/en-us/sql/t-sql/functions/session-context-transact-sql)

### Questions for Lee — raised, not resolved

Framed as options to cut rather than open prompts, per `harness-constraints.md:110`.

1. **The bootstrap contradiction (Task 1) — the one that must be answered.** Option A (set the context from the route, then verify Membership under it), Option B (widen `Membership`'s predicate to admit `SESSION_CONTEXT('AccountId')`, bringing 1.7's decision forward and pre-empting the AD-24 amendment), or Option C (an exempt principal — ruled out by AD-24, listed for completeness). **Recommendation: A.**
2. **B2, the seeding strategy (Task 9).** Seed through the application per Space with the context switched; seed as an RLS-exempt principal; or seed before the policies exist via migration ordering. The design note calls the second *"the usual answer"* and immediately warns it is a hole if it reaches production. Story 1.9's fixtures are written against the answer.
3. **The pooled-connection split (Task 8).** Assert AC5's behaviour here with a locally pinned pool and leave I-8's collection, parallelism and container to 1.9 — or pull I-8 forward into this story in full. **Recommendation: assert here, leave the harness to 1.9**, because 1.9 owns the topology decision and a case built against the wrong topology is the one that gets quarantined.
4. **Where `MAXDOP = 1` is set (Task 6)** — migration statement, database-scoped configuration, or deployment configuration. Nothing states it. It must survive a database created from migrations, because that is what the test fixture builds.
5. **How far AC6 goes (Task 6).** There is no cross-entity Space-scoped reference in the schema yet; the first is FR-21's Assignee in story 4.6. Deliver the enabling constraint plus the gate, or wait for a real reference? **Recommendation: enabling constraint plus gate**, so 4.6 inherits a fact rather than a convention.
6. **`Account` and `Space` policies.** `deferred-work.md:90` leaves them deliberately open pending the AD-24 amendment due before epic 3. Confirm they stay open through this story, or close them now.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (`claude-opus-5-5`), via the `bmad-dev-story` workflow, 2026-09-25.

### Debug Log References

- **Engine behaviour verified first, before any design was built on it.** `A_read_only_session_key_is_cleared_by_the_pool_and_refuses_a_second_set` was activated alone against a live SQL Server. A second `sp_set_session_context` of a `@read_only` key is refused with **error 15664**. A pooled connection re-taken after close (same `@@SPID`, asserted) carries **no** `SESSION_CONTEXT`. Both documented claims hold. The scaffold was strengthened with the `@@SPID` check, because without it a fresh connection would have passed the "cleared" assertion vacuously.
- **The ATDD scaffold `Exactly_one_production_call_site_…` was never green.** Its checklist says "green today (one call site)". On activation it reported **four** files: it matched the procedure name in doc comments (`IIdentifierGenerator.cs`, `AccountCredentialStore.cs`, `AccountRegistrationStore.cs`). The scanner now strips comment lines before matching. The A-13 scan and the new cache scan share the same helper.
- **The ATDD seed helpers could not have worked.** They inserted `StatusDefinition` rows for random `SpaceId`s with no `Space` row, and the FK to `Space` refuses that. The helper remarks claimed the reverse. Replaced by `SpaceSeeding`, which adds the `Space` in the block that first writes into it.
- **The ATDD filter-layer scaffold asserted the wrong outcome.** It expected "only Space A's rows" with the session context naming B and the application state naming A, but with both layers live that intersection is empty. The activated test asserts: RLS admits `["Beta"]` (read with the filter disarmed), and the filtered read returns nothing.
- **Plant script mishaps, none of which touched the tree.** The first run died at list construction: `R` is PowerShell's alias for `Invoke-History`. Five plants first failed to *build* on analyzer rules (IDE2001, CA1823, S2696, SA1649, MA0182), not on the gate. A `continue` in the script's build-failure branch silently skipped their report lines, and I caught the gap from the missing headers. All five were rewritten to build and then fired. `git status --porcelain` was snapshotted before the plants and compared after each batch: identical except for the ledger, which I edited by hand in between.
- Container runtime was stopped at session start (all three WSL distros `Stopped`, Rancher not running). `rdctl start` from a clean stop brought Docker 29.5.3 up.

### Completion Notes List

**Decisions (Lee, 2026-09-25, taken as recommended on all six questions).**

1. **Bootstrap problem → Option A, set-then-verify.** `MembershipActiveSpaceResolver` opens a transaction, sets `SESSION_CONTEXT('SpaceId')` to the route's `{spaceId}` through the shared `SpaceSessionContext`, narrows the query-filter scope to the same Space, and reads the Account's `Membership` under both layers. A row resolves the context. No row rolls back, clears the scope, and refuses. The window in which the connection names an unverified Space exists only inside that method, and only the Membership read runs in it. It is closed by construction (a single choke point), not by ordering discipline. **Cost to 1.7:** none taken from it and none given. The switcher still needs a cross-Space Membership read, which this predicate still forbids. **Does it pre-empt the AD-24 amendment (readiness issue 3)?** No: no predicate changed and no `AccountId` key was introduced. The objection that the route is client-supplied is answered in the resolver's remarks from `ARCHITECTURE-SPINE.md:267` ("it is the input to AD-1").
2. **AC8 / B2 → the first named shape: seed through the application, per Space, with the context switched between blocks.** `tests/…/SpaceSeeding.cs`: each block is its own unit of work, meaning a `YelloDbContext` whose `SpaceScope` names that Space, a transaction, and the production `SpaceSessionContext.SetAsync`. That satisfies the FILTER and the BLOCK predicates as a real request would. It needs no exempt principal, no permission and no configuration production lacks, **so there is nothing to keep out of production.** Proved by `Two_Spaces_can_be_seeded_and_each_is_readable_only_under_its_own_context` and `A_seeding_block_cannot_write_into_another_Space` (the BLOCK predicate refuses a cross-Space write with `DbUpdateException`). Story 1.9's fixtures inherit this helper.
3. **Pooled connection → asserted here, harness left to 1.9.** `PooledConnectionIsolationTests` runs in its own class and container with `Max Pool Size=1`, through the product's resolver and reader. It asserts the same `@@SPID` across both requests, so reuse is observed, and identity (`Alpha`, then `Beta`) rather than a count. This is **not** I-8: no `Yello.Tests.Isolation` case, no collection, no topology decision.
4. **`MAXDOP = 1` → a migration statement.** `ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 1` in `20260925132825_ActiveSpaceResolution`, run with `suppressTransaction: true`. It travels with the schema to every database built from migrations; the fixture's database is asserted. The deploy principal's permission is deferred to 1.10 (ledger).
5. **AC6 → the enabling shape plus a gate, no invented reference.** An alternate key `AK_Membership_SpaceId_Id` on `Membership(SpaceId, Id)`, asserted in the migrated schema. `PersistenceModelGateTests.Every_reference_between_Space_scoped_entities_carries_SpaceId` fails any FK between two Space-scoped entities that lacks `SpaceId` on either side. That gate is vacuous today by construction and was planted (P8).
6. **`Account` / `Space` stay policy-free** until the AD-24 amendment. They get no query filter either; the gate only covers entities with a `SpaceId`.

**What was built.**
- `ActiveSpaceContext(AccountId, SpaceId, Role)`, a record in `Yello.Application/Spaces`, plus the port `IActiveSpaceResolver` / `IActiveSpaceUnitOfWork`. No ASP.NET Core or EF type crosses into Application. The ring gates and package bans are unchanged and green.
- `SpaceSessionContext`, the **one** production call site of `sp_set_session_context`, extracted from `AccountRegistrationStore` with the explicit `sysname`/`sql_variant` parameter types kept. Registration and the resolver both call it. They cannot double-set, because registration's route carries no `{spaceId}`, so the resolution filter never fires on it. The registration endpoint tests stay green end to end.
- `SpaceScope`, the request-scoped application state. The query filters on `Membership` and `StatusDefinition` compare `SpaceId` with `YelloDbContext.ScopedSpaceId`, which reads it. They never read `SESSION_CONTEXT`. With no Space in scope they match nothing (fail closed).
- `ActiveSpaceResolution`, the Host endpoint filter attached to the route **group** and matched **by route shape** (`RoutePattern.GetParameter("spaceId")`), beside the anti-forgery filter. Order: unauthenticated → **401** before resolution; no Membership or no such Space → **one 404** (`urn:yello:problem:space-not-found`) for both; otherwise the endpoint runs inside the unit of work, which commits after it returns. `Refuse()` is the single refusal point 1.6 hangs recording on, and `ResolveCoreAsync` is the counting seam for NFR-8 (ledger).
- `ISpaceRequestEstablishment`: the AD-4 seam. The Session half (`SessionSpaceRequestEstablishment`) takes the Account from `SessionPrincipal.AccountIdOf` and the Space from the route. 1.8 adds the Token half behind the same interface.
- **First Space-scoped route:** `GET /api/v1/spaces/{spaceId:guid}/statuses` → `ListStatusesHandler` → `IStatusDefinitionReader`. Chosen because it reads real Space-scoped rows through both layers and is the first read the Board needs, not a surface invented for a test. The route constant is in `Yello.Contracts/Spaces/SpaceRoutes`.
- `ActiveSpaceContext` is available to slices as a scoped service that **throws** when nothing was resolved, rather than defaulting.
- Logs `SpaceLog` 1016 (resolved, Debug) and 1017 (refused, Information): `SpaceId` only, never a name or an Account, and one message for both refusal causes.

**Evidence map, AC by AC.**
- **AC1:** `A_Membership_in_the_requested_Space_resolves_a_context_carrying_its_Role`; `A_Space_the_Account_is_not_a_member_of_is_refused_exactly_as_a_missing_one_is` (anchored on a success); `A_refused_resolution_leaves_the_scope_matching_no_Space`. Over the wire: `A_member_is_served_and_a_non_member_is_refused_exactly_as_a_missing_Space_is` (status, body, content type and header names identical for the two refusals) and `A_request_with_no_Session_is_refused_with_401_before_any_Space_is_resolved`.
- **AC2:** `A_resolved_unit_of_work_reads_only_its_own_Spaces_rows`; `Consecutive_requests_for_different_Spaces_each_return_only_their_own_rows` (A, B, A, B on one Host); `Exactly_one_production_call_site_sets_the_Space_session_context`; the engine-behaviour test.
- **AC3:** existing `PersistenceModelGateTests.Every_Space_scoped_entity_has_a_non_nullable_SpaceId` and `SpaceIsolationSchemaTests` (catalogue-driven, planted P1), **extended** with `Every_predicate_filters_on_the_SpaceId_session_context`, which reads `predicate_definition` and the function body.
- **AC4:** `The_query_filter_alone_excludes_another_Spaces_rows` and `Row_level_security_alone_excludes_another_Spaces_rows` (layers deliberately set to disagree); `Every_Space_scoped_entity_has_a_query_filter_from_application_state` (model gate: compares with a `YelloDbContext` member, calls nothing); A-13 `No_production_code_outside_Infrastructure_bypasses_the_query_filters`.
- **AC5:** `A_reused_pooled_connection_returns_only_the_second_Spaces_rows`; `The_database_is_configured_for_serial_plans`.
- **AC6:** `Membership_carries_a_unique_key_on_SpaceId_and_Id` (asserts the key's ordinal count, not just membership of two columns); the composite-reference model gate.
- **AC7:** `The_Role_is_read_from_the_Membership_row_on_every_request` (Member → row updated → Viewer on the next resolution); `Space_resolution_state_is_registered_per_request` (DI lifetimes read from the real `AddYelloInfrastructure`); `No_production_code_introduces_a_cache`.
- **AC8:** decision 2 above, plus the two seeding tests.
- **Story 1.4's AC3 Membership clause, provable for the first time:** `An_expired_Session_grants_nothing_in_a_Space_the_Account_is_still_a_member_of`.

**Plants — every absence assertion, failed on purpose.** Plants edited production source and ran the gate. Files were restored byte-for-byte in `finally`, never with `git checkout`.

| # | Plant (production edit) | Gate that fired, by name |
|---|---|---|
| P1 | Migration creates `dbo.PlantedScoped (Id, SpaceId)` with no policy | `SpaceIsolationSchemaTests.Every_table_with_a_SpaceId_column_is_covered_by_the_policy` |
| P2 | `StatusDefinition` query filter removed | `The_query_filter_alone_excludes_another_Spaces_rows` **and** `Every_Space_scoped_entity_has_a_query_filter_from_application_state` |
| P3 | Filter replaced by `SpaceId != Guid.Empty` (not application state) | same two |
| P4 | `IgnoreQueryFilters()` in a `Yello.Host` file | `No_production_code_outside_Infrastructure_bypasses_the_query_filters` |
| P5 | Second `EXEC sp_set_session_context` in `Yello.Host` | `Exactly_one_production_call_site_sets_the_Space_session_context` |
| P6 | `IMemoryCache` dependency in `Yello.Host` | `No_production_code_introduces_a_cache` |
| P7 | `SpaceScope` registered as a singleton | `Space_resolution_state_is_registered_per_request` |
| P8 | Single-column FK `StatusDefinition.Id → Membership.Id` | `Every_reference_between_Space_scoped_entities_carries_SpaceId` |
| P9 | `MAXDOP` statement replaced with `SELECT 1` | `The_database_is_configured_for_serial_plans` |
| P10 | Alternate key on `Id` alone | `Membership_carries_a_unique_key_on_SpaceId_and_Id` |
| P11 | Membership read drops the `AccountId` predicate | `A_Space_the_Account_is_not_a_member_of_is_refused_exactly_as_a_missing_one_is` **and** `A_member_is_served_and_a_non_member_is_refused_exactly_as_a_missing_Space_is` |
| P12 | Role memoised in a static cache | `The_Role_is_read_from_the_Membership_row_on_every_request` |
| P13 | Resolver stops setting the session context | `A_reused_pooled_connection_returns_only_the_second_Spaces_rows` **and** `A_Membership_in_the_requested_Space_resolves_a_context_carrying_its_Role` |
| P14 | Establishment defaults a missing Account to `Guid.Empty` | `A_request_with_no_Session_is_refused_with_401_before_any_Space_is_resolved` |
| P15 | Filter matches `{spaces}` instead of `{spaceId}` | `A_member_is_served_and_a_non_member_is_refused_exactly_as_a_missing_Space_is` |

All 15 are kept as permanent tests (the plants themselves are not in the tree). Two suggested plants were **not** run: a nullable `SpaceId`, whose gate predates this story and was planted by 1.3; and "a slice resolving the Space itself" (A-12), which has no second slice to plant against (ledger).

**What does not hold, or is not claimed.**
- AC3's non-nullable half and the policy-coverage gate were *extended and planted*, not rewritten. No new Space-scoped table exists, so no new policy was written.
- A non-GUID `{spaceId}` segment gets routing's bare 404, not the problem body. Both are 404 and neither reveals existence. Ledgered for 1.6.
- A-12 is not written. The pooled case is not I-8. `Yello.Tests.Isolation` is untouched and still ignores exit code 8.
- No client surface calls the new route. Story 1.7 owns the chrome.

**Build and test.** `dotnet build Yello.slnx`: 0 warnings, 0 errors. `dotnet test Yello.slnx`: **268 total / 267 passed / 0 failed / 1 skipped** in 59 s (the skip is the pre-existing 1.4 legacy-work-factor demonstration). Baseline was 244 / 243 / 1 at 1.4's close, and 253 / 243 / 10 after the ATDD scaffolds. Twelve SQL Server engines now start on a full run (+3). This run completed without a memory incident; topology remains 1.9's.

**Housekeeping.** Every source comment that predicted this story was rewritten to describe what now exists. That covers the six named in Task 11 plus `SessionRoutes`, `SessionContractTests`, `YelloApiHandler`, `RegistrationPhase`, `MainLayout.razor`, `Application/AssemblyMarker`, both Account handlers, `SqlServerContainerFixture` and `AppHost/Program.cs`. One review-narration parenthetical was removed from `YelloDbContext`, and none was added: two dated or "first version" sentences I had written during this run were taken out again before completion. Ledger updated: `:90` (narrower, still open), `:94` (paid for this migration), `:127` (counting seam named), `:185` (executed, both hold), plus a new 1.5 section with five entries.

### File List

**New — production**
- `Yello.Application/Spaces/ActiveSpaceContext.cs`
- `Yello.Application/Spaces/IActiveSpaceResolver.cs`
- `Yello.Application/Statuses/ListStatuses/ListStatusesHandler.cs`
- `Yello.Contracts/Spaces/SpaceRoutes.cs`
- `Yello.Contracts/Spaces/StatusResponse.cs`
- `Yello.Domain/Statuses/IStatusDefinitionReader.cs`
- `Yello.Host/Endpoints/SpaceStatusEndpoints.cs`
- `Yello.Host/SpaceLog.cs`
- `Yello.Host/Spaces/ActiveSpaceResolution.cs`
- `Yello.Host/Spaces/ISpaceRequestEstablishment.cs`
- `Yello.Host/Spaces/SessionSpaceRequestEstablishment.cs`
- `Yello.Infrastructure/Persistence/MembershipActiveSpaceResolver.cs`
- `Yello.Infrastructure/Persistence/SpaceScope.cs`
- `Yello.Infrastructure/Persistence/SpaceSessionContext.cs`
- `Yello.Infrastructure/Persistence/StatusDefinitionReader.cs`
- `Yello.Infrastructure/Persistence/Migrations/20260925132825_ActiveSpaceResolution.cs`
- `Yello.Infrastructure/Persistence/Migrations/20260925132825_ActiveSpaceResolution.Designer.cs` (generated)

**New — tests**
- `tests/Yello.Tests.Architecture/SpaceResolutionSeamTests.cs` (ATDD scaffold, activated and extended)
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceIsolationLayerTests.cs` (ATDD scaffold, activated and extended)
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/PooledConnectionIsolationTests.cs` (ATDD scaffold, rewritten onto the product path)
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceSeeding.cs`

**Modified — production**
- `Yello.Contracts/ProblemTypes.cs` (`SpaceNotFound`)
- `Yello.Host/Program.cs`
- `Yello.Infrastructure/InfrastructureServices.cs`
- `Yello.Infrastructure/Persistence/AccountRegistrationStore.cs`
- `Yello.Infrastructure/Persistence/Configurations/MembershipConfiguration.cs`
- `Yello.Infrastructure/Persistence/Configurations/StatusDefinitionConfiguration.cs`
- `Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs` (generated)
- `Yello.Infrastructure/Persistence/YelloDbContext.cs`
- Comment-only: `Yello.AppHost/Program.cs`, `Yello.Application/AssemblyMarker.cs`, `Yello.Application/Accounts/AuthenticateAccount/AuthenticateAccountHandler.cs`, `Yello.Application/Accounts/RegisterAccount/RegisterAccountHandler.cs`, `Yello.Client/Layout/MainLayout.razor`, `Yello.Client/Pages/RegistrationPhase.cs`, `Yello.Client/Sessions/YelloApiHandler.cs`, `Yello.Contracts/Accounts/SessionRoutes.cs`, `Yello.Host/Endpoints/SessionEndpoints.cs`, `Yello.Host/Sessions/AntiforgeryRequirement.cs`, `Yello.Host/Sessions/SessionPrincipal.cs`, `Yello.Infrastructure/Persistence/AccountCredentialStore.cs`, `Yello.Infrastructure/Persistence/Migrations/20260828164716_InitialSchema.cs`

**Modified — tests**
- `tests/Yello.Tests.Architecture/PersistenceModelGateTests.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegistrationDatabase.cs`
- `tests/Yello.Tests.Slices/Accounts/RegisterAccount/SpaceIsolationSchemaTests.cs`
- `tests/Yello.Tests.Slices/WireSession.cs`
- Comment-only: `tests/Yello.Tests.Architecture/SessionContractTests.cs`, `tests/Yello.Tests.Shared/SqlServerContainerFixture.cs`

**Modified — artifacts**
- `_bmad-output/implementation-artifacts/1-5-resolve-the-active-space-per-request-enforced-in-the-database.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/deferred-work.md`

### Change Log

- 2026-09-25: Implemented story 1.5. Per-request Space resolution by set-then-verify; one shared session-context setter; EF Core query filters from request-scoped application state; `MAXDOP = 1` and the `Membership(SpaceId, Id)` alternate key by migration; first Space-scoped route; B2 answered as per-Space application seeding; 9 ATDD scaffolds activated (3 corrected), 15 further tests and gates, 15 plants fired. Status → review.
