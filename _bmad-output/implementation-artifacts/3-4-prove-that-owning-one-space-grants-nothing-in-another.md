# Story 3.4: Prove that owning one Space grants nothing in another

Status: ready-for-dev

Epic: 3 — Several Spaces, cleanly separated (the last story of the epic)
Story key: `3-4-prove-that-owning-one-space-grants-nothing-in-another`

**Requirements owned.** From the Story Coverage Index (`epics.md:368`): "3.4 | AR-35, NFR-1, **SM-1 extension**". Story 1.9 is the SM-1 gate (`epics.md:352`); this story extends it to one Account holding several Spaces.

- **SM-1** (`prd.md:854`, `epics.md:171`). "Zero verified cross-Space disclosures across browser and API in any released build, measured by an isolation test suite exercised on every change." It is gating, not a percentage (`test-design-qa.md:238-241`).
- **NFR-1** (`prd.md:683-690`). No disclosure "by any route", including listings, aggregates and error messages. "Possessing the identifier ... confers nothing." "This is the one requirement with no acceptable failure rate."
- **AR-35** (`epics.md:223`). Four suites gate release. Isolation runs every case on both surfaces, against Testcontainers SQL Server and never an in-memory provider.

**Also binding, though the coverage row omits them:**

- **FR-15** (`prd.md:321-330`). "An Owner of one Space has no elevated standing in any other Space, regardless of Membership." "Authorisation is evaluated per request; it is never cached across a Space switch." Test design **I-9** (P0, `test-design-qa.md:273`).
- **FR-36 / AD-6** (`prd.md:607-616`; `ARCHITECTURE-SPINE.md:108-112`). "A Token issued by an Account that owns several Spaces reaches only the Space it names, including Spaces the same Account owns." Test design **I-12** (P0, `:276`). `SPEC.md:178` (CAP-36), `:205`.
- **AD-2 / AR-5 / AR-7** (`ARCHITECTURE-SPINE.md:77-86`; `epics.md:195`). `SESSION_CONTEXT('SpaceId')` is set per unit of work, "never set once per connection: a pooled connection reused across two requests for different Spaces is the exact leak this guards against". `MAXDOP = 1` stays until the pooled case is green on Azure (`ARCHITECTURE-SPINE.md:435`). Test design **I-8** (P0, R5).
- **AD-1** (`:71-75`). Role is read from the `(AccountId, SpaceId)` Membership row on every request, never from a cookie, claim, Token or cache. It prevents "collapsing per-Space Roles into a global role". `SPEC.md:196`: authorisation is a function of `(Account, Space)`, never of Account alone.
- **AD-3 / AR-10** (`:88-94`; `epics.md:198`). No Membership gives the boundary `404`; a Role the matrix denies gives `403`. "No handler converts one into the other."
- **AD-4** (`:96-100`). "The isolation suite runs every case against both surfaces; a case that exists for one and not the other is a gap, not a choice."
- **AD-20 / AR-11 / NFR-7** (`:195-199`; `epics.md:199`; `prd.md:730-734`). The pipeline, never a slice, writes `AccessRefusal` with kind `CrossSpace` or `InsufficientRole`.
- **NFR-2 / I-10** (`prd.md:699`). "No request is authorised using a Role established during a previous active Space."
- **UX-DR31** (`epics.md:275`; `EXPERIENCE.md:158-168`). "The client replica is in scope for isolation." "SM-1's suite exercises *requests*, and a leak rendered from client memory issues no request" (`EXPERIENCE.md:160`). The purge is "a **data** rule, not a presentation one".
- **`EXPERIENCE.md:146`**. A request for a Space id absent from the switcher "produces an `AccessRefusal` classified `CrossSpace` ... a client that manufactures those for ordinary users buries real probing in its own noise".

**This story is mostly a test story, plus one product fix and a set of ledger closures.**

- The pipeline already refuses a Token bound elsewhere exactly as a non-member is refused (`Yello.Host/Spaces/ActiveSpaceResolution.cs:159`).
- Role is already read per request from the Membership row (`MembershipActiveSpaceResolver.cs:65-83`).
- `SESSION_CONTEXT` is already set per unit of work from one line (`Yello.Infrastructure/Persistence/SpaceSessionContext.cs:111`).
- The client purge already exists (`Yello.Client/Spaces/ActiveSpaceState.cs`).

So the work is mostly to **measure** these properties for one Account holding several Spaces, which the suite has never done:

- the isolation world has no Account owning two Spaces;
- no wire case covers "Owner of A, lesser Role or none in B";
- no Token-bound-elsewhere row covers Project, Task or Label routes;
- the pooled case uses two Accounts;
- nothing reads the `AccessRefusal` table after ordinary client use;
- nothing inspects client memory.

The one product change is the lock fix the ledger hands this story (`deferred-work.md:606`).

**Explicitly not in this story:**

- **`/sync` as a third surface** is story 7.3's (`deferred-work.md:408`).
- **The replica and the inbound frames** are story 7.3/7.7's (`:674`).
- **The FR-35 parity sweep** is story 8.1's (`:409`).
- **Invitations.** They do not exist yet. Seed a Membership for another Account's Space with raw SQL, which `TESTING-CONVENTIONS.md:701-721` allows where no endpoint exists.
- **No new NuGet package and no new project** (CLAUDE.md). **No new container-backed test class**: reuse `IsolationWorldFixture`, `PinnedPoolWorldFixture`, `ClientSurfaceFixture`, `RealHostFixture` and the Slices partials.
- **No Azure deployment, login or push. No commit**: the orchestrator commits.

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Severity | Covered by |
|---|---|---|---|
| `:407` | SM-1 extension: one Account, two Spaces, rows in `IsolationCases.cs` | medium | AC1-AC4, AC6, AC9 |
| `:606` | Label deletion's `TaskLabel` range lock and the point `UPDLOCK, HOLDLOCK` reads on ids another Space owns can lock another Space's keys | medium | AC10 |
| `:609` | Three of 2.9's isolation rows (`ISO-DIRECT-BOARD-COLUMN`, `ISO-BOARD-COLUMN-AGGREGATE`, `ISO-BOARD-AGGREGATE-PAGED`) are not proved by a plant of their own | low | AC11 |
| `:675` | A shared static in a client service cannot be caught by the two-tab browser case | low | AC12 |
| `:680` | Absence assertions in 3.2's browser cases never seen red (E2E-001, 003, 005, 007, 008, 010, 014, 020) | low | AC11 |
| `:688` | Blazor's location-changing hook was not seen to run for the deletion's `NavigateTo("/")` | low | AC13 |
| `:696` | The row-driven Space-deletion cases (`ISO-DELETE-SPACE-OWN`, `REV-SPACE-DELETED-BY-OWNER`) cannot carry `Assumption=PRD-12-2` | low | AC14 |

**Depends on** (all done):

- 1.6: the pipeline and the refusal recorder.
- 1.8: Tokens.
- 1.9: the case list, its gates, the pooled world, `RealHostFixture`.
- 2.9: application locks, PG-16/17/19.
- 3.1: Space creation through the product.
- 3.2: the purge and `SpaceSwitchCarryOverTests`.
- 3.3: deletion.

## Story

As the operator deciding whether Yello can ship,
I want the isolation suite to cover one Account holding several Spaces, on both surfaces and in the client,
So that the sharpest form of the invariant is measured rather than assumed.

## Acceptance Criteria

AC1 to AC7 are reproduced **verbatim** from `epics.md:1583-1611` (`status: final`) and numbered by position. AC8 to AC14 come from the spines, the ledger and a survey of the code. Each one cites its source.

**AC1 — a foreign object is refused exactly as for a stranger** (NFR-1, FR-15, I-1, I-4)
**Given** one Account owning two Spaces
**When** it requests a Project, Task or Label belonging to the second while the first is its active Space
**Then** the request is refused exactly as it would be for an Account holding no Membership anywhere near it

> **Reading (Q1).** Every route carries `{spaceId}` (AD-3), so "the first is its active Space" means a request routed under SA1 that names an id living in SA2. Such a request is answered byte for byte as the anchor is answered. The anchor is the same route under SA1 with an id that exists nowhere (the in-Space not-found). It is also answered byte for byte as an Account with no Membership in SA2 is answered for the same SA2 id under its own Space. On both surfaces the Token is bound to SA1. Cover at least:
>
> - read a Project (the List route and the Board route with SA2's `projectId`);
> - read a Task;
> - edit, move and delete a Task;
> - rename and delete a Project;
> - delete a Label;
> - apply an SA2 Label to an SA1 Task;
> - create a Task in SA2's Project.
>
> After every write, a `Then` check proves that SA2's object is unchanged.

**AC2 — ownership confers nothing beyond its own Space** (FR-15, AD-1, I-9, I-10)
**Given** an Owner of one Space
**When** it acts in another Space where it holds a lesser Role or none at all
**Then** it has no elevated standing whatsoever, because Role is per Space and ownership confers nothing beyond its own

> - **The Owner of SA is a Viewer in SB (another Account's Space).** These are refused `403` with one `InsufficientRole` record each, and nothing is written:
>   - create a Project, Task or Label;
>   - edit or move a Task;
>   - rename SB;
>   - read the deletion counts;
>   - delete SB.
> - **The Owner of SA is an Admin in SB.** Deleting SB and reading its deletion counts are refused `403` (`Capability.DeleteSpace` is Owner-only).
> - **The Owner of SA has no Membership in SB.** Every one of the above is the boundary `404` with a `CrossSpace` record.
> - **I-10.** The same Session, used straight after an Owner-level write in SA, gets the Viewer `403` in SB with no carry-over. Alternate SA, SB, SA, SB in one case.

**AC3 — a Token on another Space its own Account owns** (FR-36, AD-6, I-12)
**Given** an API Token bound to one of an Account's Spaces
**When** it is used against another Space that same Account owns
**Then** it is refused
**And** the suite carries this case explicitly, because it is the one an implementer is most likely to treat as harmless

> `ISO-TOKEN-BOUND-ELSEWHERE` (`IsolationCases.cs:133`) covers `GET SpaceRoutes.Active` only. Add rows for each Space-scoped route family under SA2 with the SA1 Token: Statuses, Projects (list, create, rename, delete), Board, Board column, List, Task (read, create, edit, delete), Labels (list, create, delete), Space rename, Space delete and the deletion counts.
>
> The ApiToken expectation is `SameAsNonMember` with a `CrossSpace` record. The Session expectation is `Served`, because a Session carries no Space (FR-2; 1.9 Q8 (a)). A `Then` check proves SA2 is unchanged after each write.
>
> Every route that the 1.9 route-coverage gate reaches under `{spaceId}` gets a Token-elsewhere row, or the case states why it shares one. Q3 decides whether this is a gate or a hand-maintained list.

**AC4 — one Account, two Spaces, one pooled connection** (AD-2, AR-7, I-8, R5)
**Given** consecutive requests for two different Spaces from the same Account on one pooled physical connection
**When** both are served
**Then** neither discloses the other's rows

> Add a row to the `Pooled` runner, beside `ISO-POOLED`. One Account owns SA1 and SA2, and each Space is populated with distinct Statuses, Projects and Labels. On each surface the Account's requests alternate SA1, SA2, SA1, SA2 (at least 5 rounds), followed by concurrent detached requests, as `EachSeesOnlyItsOwnOnOneConnection` does.
>
> On the ApiToken surface, use two Tokens of the same Account, one per Space.
>
> Assertions:
> - every answer holds exactly its own Space's ids and names;
> - `HostSessionCountAsync() == 1`, so the anchor is one connection and not zero.
>
> The case runs in `PooledConnectionCollection` (`DisableParallelization`).

**AC5 — nothing of the departed Space in the DOM or in client memory** (UX-DR31, `EXPERIENCE.md:158-168`)
**Given** a switch from a populated Space to another
**When** the destination has rendered
**Then** no Task title, Project name, Label, Status name or count belonging to the departed Space is present in the DOM or in client memory

> **Count.** Seed the departed Space so its column counts and Project Task counts are numbers the destination never shows (for example 13 and 17), and assert that the count chips carry none of them.
>
> **"Client memory".** This is Q2. Recommended reading: all reachable managed state. It is proved by:
> - the purge seam's existing gates;
> - AC12's static gate;
> - a DOM absence after the switch, made while the departed page was kept mounted until the switch, with plants;
> - a time-boxed attempt at a WebAssembly heap scan.

**AC6 — both surfaces, and a gap fails** (AD-4, AR-35)
**Given** every case added by this story
**When** the suite runs
**Then** each executes against both the browser and the API surface, and a case existing for one and not the other fails as a gap

> - **Isolation rows.** The "browser surface" is `IsolationSurface.Session`, the wire the browser sends (1.9 Q3 (a)). GATE-001 (`IsolationCaseListTests.cs:36`) already fails a row that lacks a surface. Do not weaken it.
> - **AC5 and AC7 (browser-only).** Each states its API counterpart by id in its remarks. For AC5 that is the aggregate rows of AC9 for the same two-Space Account. For AC7 it is the record-kind rows. Q4 covers this.

**AC7 — the record says CrossSpace, and the client makes none** (AD-20, NFR-7, `EXPERIENCE.md:146`)
**Given** an `AccessRefusal` record produced by any of these cases
**When** it is inspected
**Then** it is classified `CrossSpace` rather than `InsufficientRole`
**And** the client has manufactured none of them during ordinary use, because a client that does buries real probing in its own noise and defeats the record's purpose

> **Contradiction C1 (record, keep AD-3).** AC2's lesser-Role case is a Member refused by the matrix, so AD-3 and AR-10 require `403` and `InsufficientRole`, and forbid converting one into the other. AC7 applies to the **cross-Space** cases: AC1's direct form, AC2's no-Membership arm and AC3. AC1's foreign-id-under-SA1 form is answered as an in-Space not-found, which writes **no** record (the 2.5 precedent, `SameAsAnchor`). Q5 decides whether that stays.
>
> **"The client manufactured none".** A real-Host browser case (`RealHostCollection`) has one Account owning two populated Spaces. It:
> - signs in, lands, and opens each Space's home, Board, List and settings;
> - switches through the switcher both ways, uses Back and Forward, and follows a deep link to its own other Space;
> - asserts `WireDatabase.RefusalCountAsync(account) == 0`.
>
> Anchor: one direct `fetch` from the page to a foreign Space id raises the count to 1. Plant: bypass `SpaceDirectory.Lists` in `SpaceArrival.razor:142-151` and deep-link to another Account's Space. The case must go red.

**AC8 — a world with one Account holding several Spaces** (`deferred-work.md:407`; `test-design-qa.md:134` has no such fixture)

- `IsolationWorld` gains a helper, for example `world.OwnerOfTwoAsync()`. It registers an Account through the product and creates its second Space through `POST /api/v1/spaces` (3.1), so that the Space carries its default Statuses. It does **not** use `WireDatabase.AddOwnedSpaceAsync`, which writes no Statuses.
- The helper populates both Spaces through the product with distinct, randomised names and returns both Space ids, a Token per Space and the seeded ids.
- Add a `WireDatabase.AddMembershipAsync(spaceId, accountId, role)` (raw SQL; Invitations are Epic 4) for AC2. Add it beside `ChangeRoleAsync`, in the same style.
- No new container class. The world stays one per test class.

**AC9 — aggregates and listings for the two-Space Account** (I-3, I-11; NFR-1)

Read from SA1, the following hold exactly SA1's rows and totals, with none of SA2's ids, names or counts:

- the Statuses read;
- the Project list with its Task counts;
- the Board with its column counts, and a paged column;
- the List View with its total;
- the Labels list.

Anchor: the same reads from SA2 hold SA2's rows. The switcher (`GET /api/v1/spaces`, Session) lists exactly SA1 and SA2, as rows `{id, name}`. Token list rows name each Token's own Space.

**AC10 — no lock reaches another Space's keys** (`deferred-work.md:606`; `TESTING-CONVENTIONS.md:167-180`)

- **`LabelStore.DeleteAsync` (`:103`).** Replace the serialisable `TaskLabel` range read (`WITH (UPDLOCK, HOLDLOCK) WHERE LabelId = ...`) with a Space-named application lock for that Label's applications: `ApplicationLocks.LabelApplications(spaceId, labelId)`, giving `Yello/LabelApplications/{spaceId:D}/{labelId:D}`. Both the deletion and `TaskStore.ReplaceAsync`'s Label application take it, after a plain RLS-filtered existence read. The in-use check stays a plain read under that lock.
- **`TaskStore.ReplaceAsync` (`:238`).** Before the locking read of the submitted Labels, run a plain RLS-filtered read. If any submitted id is not visible, answer the existing not-found/invalid result and take **no** key lock on it. Take the application locks in a stable order (GUID order, as `ServerGuidOrder` does).
- **`ProjectStore.DeleteAsync` (`:101`).** Run a plain filtered existence read before the `UPDLOCK, HOLDLOCK` point read, as `TaskStore.MoveAsync` and `DeleteAsync` do. A missing or foreign id returns `NotFound` with no lock taken.
- **Proof** (the PG-16 and PG-17 pattern, `tests/Yello.Tests.Slices/Boards/BoardPagingTests.cs:706-1030`):
  - Hold Space A's Label deletion, Label application and Project deletion open, on an id at A's key edge and on a foreign or missing id.
  - Space B's matching write completes without waiting.
  - The anchor is A's own competing write, which does queue on `LCK_M_%`.
  - Plant: restore each old locking read in turn. The B case must go red ("waited").
  - Q6 says where these cases live.

**AC11 — absences seen red** (`deferred-work.md:609`, `:680`; `TESTING-CONVENTIONS.md:151`, `:192`)

- **The 2.9 rows.**
  - Plant the Task query filter and the FILTER predicate dropped on the page's count and seek. Each of `ISO-BOARD-COLUMN-AGGREGATE` and `ISO-BOARD-AGGREGATE-PAGED` must go red. Story 2.3's P6 is the model.
  - For `ISO-DIRECT-BOARD-COLUMN`, record that it stands behind the Space boundary, which 1.9's plants prove. Alternatively, plant the resolver's Membership check skipped.
  - Record each plant in the row's comment.
- **3.2's eight absences.** E2E-001, 003, 005, 007, 008, 010, 014 and 020 in `SpaceSwitchCarryOverTests.cs` are each turned red once, by a plant that injects exactly the thing the case asserts absent. For example: Role text on a switcher row; "Add a Task" for a Viewer; the purge of the open panel skipped; a `localStorage.setItem`; the departed Board kept; the departed name in an announcement; a request for A; Account 1's name kept on Account switch. A case that cannot be turned red is recorded as masked, with the reason, and never deleted (`TESTING-CONVENTIONS.md:151`).
- **This story's own absences** in AC1-AC5, AC7 and AC9 are each proved against a plant too.

**AC12 — no shared mutable state in the client runtime** (`deferred-work.md:675`)

An architecture gate over `Yello.Client`, in `tests/Yello.Tests.Architecture`, using reflection or ArchUnitNET. It fails on any of these:

- a non-`readonly` static field;
- a static property with a setter;
- an `AddSingleton` registration in `Program.cs`;
- a `static readonly` field of a mutable collection type, unless it is named in an allow-list with its reason (Q7). Today: `RefusalCopy.OwnKeys` (Dictionary), the `char[] PathEnds` fields, and `JsonSerializerOptions`.

Plant: a `static List<string>` in a client service. The gate must go red.

**AC13 — every exit from a Space purges, whoever navigates** (`deferred-work.md:688`)

- **Find out and record** whether `RegisterLocationChangingHandler` (`Yello.Client/Program.cs:182-187`) runs for a programmatic `NavigateTo`, with and without `replace: true`. The observation is a browser case, never a temporary console line left in.
- **For every `NavigateTo` that leaves a Space,** prove that the departed Space is purged before the destination renders. The exits are:
  - `Program.cs:143` (Session end);
  - `Program.cs:152` (sign-out);
  - `SpaceArrival.razor:116`;
  - `SpaceDeleteSection.razor:376`.
- **Either way, add a gate.** If the hook does not run, add a static gate requiring every `NavigateTo` whose target is outside the current Space to be preceded by `ActiveSpace.Navigating(...)`, or by the Session purge. If it does run, record that and keep the explicit call as defence in depth.

**AC14 — the deletion rows carry their assumption** (`deferred-work.md:696`; `TESTING-CONVENTIONS.md:44-73`)

`IsolationCase` and `RevocationCase` gain an optional `Assumption` (for example `"PRD-12-2"`), emitted as a trait on the generated `TheoryDataRow` (xunit v3's `TheoryDataRow` carries per-row traits). `ISO-DELETE-SPACE-OWN` and `REV-SPACE-DELETED-BY-OWNER` carry `PRD-12-2`, so `--filter "Assumption~PRD-12"` selects them on both surfaces. `TestingConventionTests` (`:244`, regex `:492`) validates the row value as it validates a method trait. If per-row traits do not reach the filter in this runner, the fallback is to list both ids in the ledger (Q8).

## Tasks / Subtasks

- [ ] **Task 1: decide the open questions** (all ACs)
  - [ ] Read the Dev Notes, then the Open questions. Decide each one as a reversible decision and record it in Completion Notes as "Qn - (x)", with its cost. Each recommendation is the default, and no human is in the loop, so do not stop to ask. **Exception:** a question marked **[LEE]** relaxes an isolation or security rule under its non-recommended option. Take the recommended option, which relaxes nothing. Never take the [LEE] option without a recorded decision from Lee.
- [ ] **Task 2: the world** (AC8)
  - [ ] Add `IsolationWorld.OwnerOfTwoAsync` (or an equivalent name), plus seeding helpers that take a Space id (today `SeedProjectAsync` and its siblings seed only `owner.SpaceId`).
  - [ ] Add `WireDatabase.AddMembershipAsync(spaceId, accountId, roleStableCode)`.
  - [ ] Use randomised names only, never a shared literal.
- [ ] **Task 3: isolation rows** (AC1, AC2, AC3, AC6, AC7, AC9; `:407`)
  - [ ] Add rows to `IsolationCases.All` under a comment heading "Story 3.4 - one Account, several Spaces". Every row states both surfaces.
  - [ ] Suggested ids:
    - `ISO-OWN-OTHER-SPACE-<OBJECT>-<VERB>` (AC1);
    - `ISO-OWNER-ELSEWHERE-VIEWER-<VERB>` and `-ADMIN-DELETE-SPACE` (AC2, `RoleRefused`);
    - `ISO-OWNER-ELSEWHERE-NONE-<VERB>` (AC2, `BoundaryNotFound`);
    - `ISO-OWNER-ROLE-NOT-CARRIED` (AC2, I-10);
    - `ISO-TOKEN-OWN-OTHER-SPACE-<ROUTE>` (AC3);
    - `ISO-TWO-SPACES-<AGGREGATE>` (AC9).
  - [ ] Record test design ids (I-1, I-4, I-9, I-10, I-12, I-3, I-11) in the `TestDesignId` column.
  - [ ] If the ATDD phase lands these rows with `PendingUntil: "3.4"`, clear every one before review (GATE-003).
  - [ ] If an expectation kind needs a two-Space variant (for example `SameAsNonMember` comparing against a fresh stranger's answer for an SA2 id), add it to `Expectation.cs`. Never branch a case on the surface.
- [ ] **Task 4: the pooled row** (AC4)
  - [ ] Add a `Pooled` runner row for one Account and two Spaces, on both surfaces, in `PooledConnectionCollection`.
  - [ ] Extend `EachSeesOnlyItsOwnOnOneConnection` or add a sibling expectation.
  - [ ] Plant: remove `Max Pool Size=1` (expect the anchor to fail, as 1.9 plant 13 did). Separately, plant `@read_only = 0` and drop the per-unit-of-work reset (expect disclosure or red).
- [ ] **Task 5: the lock fix** (AC10; `:606`)
  - [ ] `ApplicationLocks.LabelApplications(spaceId, labelId)`.
  - [ ] `LabelStore.DeleteAsync`, `TaskStore.ReplaceAsync` and `ProjectStore.DeleteAsync` as AC10 states.
  - [ ] Keep the 2.4 rule that a Label in use cannot be deleted, and keep its race tests green (`LabelWire`).
  - [ ] Add the cross-Space no-wait cases with their anchors and plants.
- [ ] **Task 6: browser cases** (AC5, AC7, AC11, AC13). Build `Yello.Client` in **Debug** before every browser run or plant.
  - [ ] AC5 in `ClientSurfaceCollection` (the fake API; extend `SeededSpaces()` with the distinct counts). Add it to `SpaceSwitchCarryOverTests.cs` or a new `SpaceSwitchMemoryTests.cs` in the same collection.
  - [ ] AC7 in `RealHostCollection` (`RealHostBrowserTests.cs` or a sibling). `RealHostFixture.AddSecondSpaceAsync` writes the Space with SQL, so make sure the second Space has Statuses: create it through the product instead.
  - [ ] AC13: one case per exit. Each holds a departed-Space marker and asserts it is gone at the destination.
  - [ ] AC11: plant 3.2's eight cases one at a time.
- [ ] **Task 7: gates** (AC12, AC13, AC14)
  - [ ] The client static gate, with an allow-list that states its reasons.
  - [ ] The `NavigateTo` gate, if AC13 needs one.
  - [ ] The `Assumption` row column, the trait emission and the `TestingConventionTests` check.
  - [ ] Each gate is proved by a plant.
- [ ] **Task 8: plants.** Save each original in memory and restore it in a `finally`. **Never** use `git checkout --`, `git restore` or `git reset`. Record `| Plant | Expected red | Seen |` in the Debug Log. The minimum set is in Testing requirements.
- [ ] **Task 9: ledger, docs, verification**
  - [ ] `deferred-work.md`: apply the Ledger dispositions. Keep each CLOSED note on one line, and check that no TAB or FORM FEED is introduced (3.3 review finding 6). Keep the file's final newline.
  - [ ] Rewrite any comment that predicts 3.4 so it describes what exists (`grep -rn "3\.4" Yello.* tests`).
  - [ ] `sprint-status.yaml`: this story goes to `review` at the end of dev. Leave `epic-3` alone; the retrospective and the orchestrator own it.
  - [ ] Run `docker info --format '{{.ServerVersion}}'`, then `dotnet build Yello.slnx` (0 warnings, 0 errors), then `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Record the totals. The expected skips are the 3 bundle cases plus the explicit tiers.

## Dev Notes

### The shape of the story in one paragraph

Every server property this story asks for is believed to hold already, because the Space comes only from the route, Role comes only from the Membership row, the Token's binding is checked before anything else runs, and the session key is set per unit of work. The story's job is to make each of those **falsifiable for one Account holding several Spaces**, on both surfaces, and to see each new absence fail under a plant. The exception is AC10. The ledger says that three locking reads still reach another Space's keys, and that is a real cross-tenant contention and timing signal. Fix it, and prove it in the PG-16 manner. On the client side, the hard question is "client memory" (Q2). Answer it honestly: say what is proved and how, and do not overclaim.

### What exists today (survey at `59e2691`)

**Isolation suite** (`tests/Yello.Tests.Isolation`):

- **The row type** is `IsolationCase(Id, TestDesignId, Vector, Method, Route, Prepare, Expectations, Runner = Boundary, PendingUntil, PendingReason)` (`IsolationCases.cs:1974`).
  - Vectors: `DirectIdentifier, DeepLink, Listing, Aggregate, Api, Write, Timing, PooledConnection, RecordKind`.
  - Runners: `Boundary, Timing, Pooled`.
  - `CaseRequest(Actor, Method, Paths[(Path, Anchor)], TargetSpaceId, Body, AnchorBody)` (`:2030`).
- **Expectations** (`Expectation.cs`), each with an optional `Then`:
  - `BoundaryNotFound` (`:92`): byte-identical to a random-Space anchor; exactly N `CrossSpace` records and no `InsufficientRole`.
  - `NoCredential` (`:138`).
  - `SameAsAnchor` (`:161`): the refusal count is unchanged.
  - `Served` (`:195`).
  - `SameAsNonMember` (`:214`): same bytes as B's Session; `CrossSpace` recorded.
  - `RoleRefused(capability)` (`:383`): `403`, one `InsufficientRole`, no `CrossSpace`.
  - The timing kinds, `EachSeesOnlyItsOwnOnOneConnection` (`:327`) and `Pending`.
  - `Both(x)` is at `IsolationCases.cs:941`.
- **Gates** (`IsolationCaseListTests.cs`):
  - GATE-001 `Every_case_declares_an_expectation_for_every_surface` (`:36`, `SurfaceGaps.Find`);
  - GATE-002 (`:72`): every mapped `/api` route has a row (`NotIsolationRoutes` `:54`);
  - GATE-003 (`:110`): a pending row fails once its story is past `in-progress`;
  - GATE-004 (`:252`): the cross-product equals the runners' rows.
  - `CaseRun.RunAsync` (`:29`) anchors on the actor reading its own Space first, and checks the I-17 log slice.
- **The world** (`IsolationWorld.cs`) has one database, one Host (`Yello.Host.Isolation`) and actors A and B, each with a Personal Space, a Session and one Token. **No Account owns two Spaces.** `TokenBoundElsewhereAsync` (`IsolationCases.cs:958`) improvises one with `NewActorAsync` + `Database.AddOwnedSpaceAsync` (`WireDatabase.cs:301`, raw SQL, **no Statuses**).
  - Helpers: `ChangeRoleAsync` (`WireDatabase.cs:413`), `RemoveMembershipAsync` (`:336`), `RefusalKindsAsync` (`:59`), `RefusalCountAsync` (`:86`), `ProductActor.IssueTokenAsync(spaceId, name)` (`ProductActor.cs:250`; it does **not** replace `actor.TokenPlaintext`).
  - There is **no** add-Membership helper.
  - `SeedProjectAsync`, `SeedProjectWithTaskAsync` and `SeedLabelAsync` seed only in `owner.SpaceId`.
- **Pooled**: `ISO-POOLED` (`IsolationCases.cs:829`) uses two Accounts. `PinnedPoolWorldFixture` (`IsolationWorld.cs:227`, `MaxPoolSize=1`) runs in `PooledConnectionCollection`. At the Slices level, `PooledConnectionIsolationTests.cs:69` and `:121` already use **one Account in two Spaces** at the SQL level (`@@SPID` equality), and AC4 is the wire-level twin.
- **Plants**: there is no helper. They are hand edits recorded in row comments (for example `IsolationCases.cs:309`, `:582`) and in story Debug Logs.
- **Traits**: class-level only (Suite=Isolation, Priority=P0). There is no `Assumption` anywhere in Isolation or Revocation. `IsolationCaseTests.Rows` is `IEnumerable<TheoryDataRow<string, IsolationSurface>>` (`IsolationCaseTests.cs:38`), and `RevocationCaseTests.Rows` (`:37`) has the same shape, so a per-row trait has a natural home.

**Pipeline and records:**

- `ActiveSpaceResolution.cs:159`: a Token whose `BoundSpaceId` differs from the route's Space is refused exactly as a non-member: `CrossSpace` against the route's Space, `404`.
- Records are written only at `:177` (`CrossSpace`) and `:196` (`InsufficientRole`).
- `AccessRefusalKind { CrossSpace, InsufficientRole }` is in `Yello.Domain/AccessRefusals/`.
- Role is read in `MembershipActiveSpaceResolver.cs:65-83`, with no cache. The matrix is applied at `ActiveSpaceResolution.cs:184`.
- `SpaceSessionContext.cs:111` is the single `sp_set_session_context ... @read_only = 1`.

**Locks** (`deferred-work.md:606`):

- `LabelStore.cs:92`: the Label point lock, after a visibility check.
- `LabelStore.cs:103`: the `TaskLabel` range lock, with **no SpaceId and no index hint**.
- `TaskStore.cs:238`: the submitted Labels under `UPDLOCK, HOLDLOCK, INDEX(1)` via `OPENJSON`, in `ServerGuidOrder` (`:87`).
- `ProjectStore.cs:101`: the point lock with **no prior plain existence read**.
- The models to follow:
  - `ApplicationLocks.cs`: `ProjectsPerSpace`, `ProjectTasks(spaceId, projectId)` and `TakeAsync` (`:115`, `sp_getapplock`, Exclusive, Transaction, which throws 51000 when the lock is not granted);
  - `TaskStore.MoveAsync` (`:340`) and `DeleteAsync` (`:299`): a plain read first, then the application lock, then the locking re-read.
- Proof harness: `BoardPagingTests.cs`: PG-16 (`:706`), PG-17 (`:765`), PG-19 (`:831`), and `WaitUntilBlockedByTheHolderOrAnsweredAsync` (`:974`; a recursive CTE over `sys.dm_exec_requests` with `wait_type LIKE N'LCK_M_%'`, no sleep). Also `LabelWire.WaitUntilBlockedOrAnsweredAsync` (`:207`) and `TaskWire.WaitUntilBlockedOnALockAsync` (`:366`).

**Client:**

- `ActiveSpaceState.cs`:
  - `RegisterPurge` (`:117`).
  - `Navigating(target)` (`:133`): a no-op for the same Space; otherwise purge, then set.
  - `Purge` (`:153`): cancels in-flight work, nulls `Current`, `ProjectName` and `SpaceId`, runs the registrations, then raises `Changed`.
  - The remarks (`:32-55`) list every holder.
  - Registrations: `BoardView.razor:241`, `LabelSection.razor:148`, `ProjectList.razor:204`, `TaskListView.razor.cs:120`, and `SpaceNarration` (`Program.cs:176`). The Session purge is at `Program.cs:163`.
- `Program.cs` registers **only `AddScoped`** (`:94-125`). No mutable static exists. The `static readonly` fields are: `JsonSerializerOptions` (about 13 files), `char[] PathEnds` (`ClientRoutes.cs:37`, `SpaceArrival.razor:61`, `ViewToggle.razor:24`), `RefusalCopy.OwnKeys` (Dictionary, `RefusalCopy.cs:179`) and `MovePicker.MorePositions`. There is **no gate**.
- Every `NavigateTo`:

  | Call site | Destination | Notes |
  |---|---|---|
  | `Program.cs:143` | Session end | |
  | `Program.cs:152` | sign-out | |
  | `AccountSettings.razor:192` | | |
  | `Home.razor:76`, `:133` | | `:133` is preceded by `Navigating(destination.Id)` when a departure is pending |
  | `CreateSpace.razor:96`, `:133`, `:252` | | `:252` is preceded by `ArrivingInCreated` |
  | `SignIn.razor:309` | | |
  | `SpaceSettings.razor:122` | | stays in the same Space |
  | `SpaceDeleteSection.razor:376` | | preceded by `Navigating(null)`, since 3.3 |
  | `SpaceArrival.razor:116` | | |

  The hook is at `Program.cs:182-187`. `ClientRoutes.SpaceIn` reads `/spaces/new` as outside every Space (`ClientRoutes.cs:69`).
- The unlisted-Space guard is in `SpaceArrival.razor:142-151` (`Directory.Lists`). Its proofs are 1.9-E2E-003/004 (`SpaceScopedFirstLoadTests.cs:121`, `:149`), 3.2-E2E-014, and 1.9-E2E-007 (Host log). **No test reads the `AccessRefusal` table after ordinary client use.**
- **Client memory is not inspected anywhere.** There is no test hook, no `[JSInvokable]` outside `BoardDrag`, `BoardKeys` and `ListKeys`, and nothing on `window`. Absence is checked through `document.body.innerHTML` (`AssertAbsentAsync`, `SpaceSwitchCarryOverTests.cs:1151`, anchored by `AssertPresentAsync` `:1141`), the fake's request log and storage lengths.
- `SpaceSwitchCarryOverTests.cs` (`ClientSurfaceCollection`, `FakeYelloApi`; seeding in `SeededSpaces()` `:1068`; holds via `api.HoldAsync`/`HoldBoardAsync`; in-app navigation via `Blazor.navigateTo` `:1162`). The ids map to tests through `atdd-checklist-3-2-...md:120-145`. The two-tab case is at `:553`.
- **Real Host**: `tests/Yello.Tests.Client/RealHostFixture.cs` (`RealHostCollection`, `:241`). It has `OpenSignedInAsync`, `NewActorAsync`, `AddSecondSpaceAsync(actor)` (`:127`, SQL), `Host.LogCursorAsync`/`WaitForLogAsync` and `Database`. It skips unless an engine is configured.

### What must be preserved (regression guard)

- **The boundary `404` stays byte-identical and uniform.** No new problem type. No row may relax `BoundaryNotFound`'s byte comparison or its exact record count.
- **No `403` becomes a `404`, and no `404` becomes a `403`** (AD-3). AC7 does not override this (C1).
- **The gates stay as strong as they are.** GATE-001 to GATE-004, `ReleaseGatingSuiteTests`, `ContainerTopologyGateTests` (no new engine-starting class), the both-keys gates (`SpaceSwitcherGateTests.No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys`) and `Exactly_one_production_line_sets_a_session_context_key`.
- **The 2.4 Label rules.** A Label in use cannot be deleted. Applying and deleting must still serialise on the same Label, and `LabelWire`'s races stay green.
- **The 2.9 lock design.** `ProjectsPerSpace` and `ProjectTasks` stay. PG-16, PG-17 and PG-19 stay green.
- **3.3's explicit `Navigating(null)`** in `SpaceDeleteSection.Leave` stays, whatever AC13 finds.
- **`MAXDOP = 1`** is not touched.

### Open questions (decide in Task 1; each is reversible; record as "Qn - (x)")

1. **What "while the first is its active Space" means for AC1.**
   - (a) **Recommended.** Routes carry the Space (AD-3), so the case is an SA2 object id under an SA1 route, compared byte for byte with the in-Space not-found anchor **and** with a stranger's answer for the same id. The direct form (an SA2 route with the SA1 Token) is AC3. The direct form with the Session is the Owner's own Space and is `200`, which is correct (FR-2). Cost: two comparisons per row.
   - (b) Compare only with the random-id anchor, as the existing `*-FOREIGN-ID-IN-OWN-SPACE` rows do. Cheaper, but it does not literally test "a stranger's answer".
2. **What "client memory" means and how AC5 proves it.**
   - (a) **Recommended.** Prove "reachable managed state" by four layers together:
     - the purge registrations and their existing gates (`ActiveSpaceStateTests`);
     - AC12's static gate;
     - a DOM absence after a switch, with the departed page held mounted until the switch, each holder planted (skip `BoardView`'s registration; skip `ProjectList`'s; skip `LabelSection`'s);
     - a **time-boxed spike** (at most half a day): read the runtime's linear memory from the page (for example `Blazor.runtime.Module.HEAPU8` or `getDotnetRuntime(0)`; verify the API exists in 10.0.11) and scan for the departed Space's UTF-16 titles after a forced GC.

     If the spike gives a reliable 3/3 signal, with an anchor where the title is found before the switch and a plant where it is found after, make it a case in the Nightly tier. Otherwise record why it is unreliable. Freed memory is not zeroed, so garbage strings give false positives. Ledger it to Lee with the measurement.
   - (b) Add a debug-only `[JSInvokable]` or `window` hook that dumps `ActiveSpaceState`'s holders. Rejected: test-only code in a production assembly (1.9 Q4's rule), and it only sees what it is told to see.
   - (c) Claim DOM absence is enough. Rejected: `EXPERIENCE.md:160` exists because a leak from memory issues no request.
3. **AC3's coverage: a gate or a list.**
   - (a) **Recommended.** A gate in `IsolationCaseListTests`: every mapped route under `/api/v1/spaces/{spaceId}` whose surfaces include ApiToken has at least one row whose `Prepare` uses a Token bound to another Space of the **same** Account. Mark such rows with a new `IsolationVector.TokenOwnOtherSpace`, or a row flag, so the gate can see them. Cost: one vector and one gate. A Session-only route (Move) has a `NoCredential` Token expectation and is exempt by its own row.
   - (b) A hand-maintained list of rows. Cheaper, but it repeats R17 ("unfalsifiable without an enumerated list").
4. **AC6 for the browser-only cases (AC5, AC7).**
   - (a) **Recommended.** Each browser case names its API counterpart rows by id in its remarks: AC5 names AC9's aggregate rows for the same two-Space Account, and AC7 names AC2's and AC3's record-kind rows. The Isolation gates hold the API side. No new cross-project gate. Cost: none.
   - (b) A gate that parses the client test's remarks for row ids that exist. Possible, but brittle.
5. **Should a foreign id under the caller's own Space write a `CrossSpace` record?** (AC7, C1)
   - (a) **Recommended.** No. Keep today's behaviour: it is an in-Space not-found with no record (the 2.5 precedent, `SameAsAnchor`). To know the id is another Space's, the handler would have to read across Spaces. That is exactly what RLS forbids, and AD-24 forbids inventing a bypass for it. Record C1 and its reading of AC7. Cost: none.
   - (b) **[LEE — would need a cross-Space read, which relaxes RLS/AD-24.]** Look the id up across Spaces and record `CrossSpace`. **Do not take this without Lee.**
6. **Where AC10's no-wait cases live.**
   - (a) **Recommended.** In the Slices suite, as partials beside the stores' existing race tests (`Labels/` and `Projects/` test classes, or `BoardPagingTests` beside PG-16 and PG-17). They are not Isolation rows, because they are not request/answer cases. Put them in the existing timing or non-parallel collection only if they measure time. They should not: they wait on `LCK_M_%`.
   - (b) New Isolation rows with a `Lock` runner. Rejected: a fourth runner and a new collection for three cases.
7. **The static gate's allow-list** (AC12).
   - (a) **Recommended.** Forbid every non-readonly static field, every static setter and every `AddSingleton`. Allow `static readonly` only for immutable types (string, Guid, primitives, `FrozenDictionary`, `ImmutableArray`, `ReadOnlyCollection`). Allow `JsonSerializerOptions`, which is frozen once used. Convert `RefusalCopy.OwnKeys` to `FrozenDictionary` (System.Collections.Frozen, in the BCL, so no new package), and the `char[] PathEnds` fields to `SearchValues<char>` or a `string`. Cost: three small edits and one gate.
   - (b) Keep them and allow-list them by name with reasons.
8. **AC14's mechanism.**
   - (a) **Recommended.** An optional `Assumption` column on both row types, emitted with `TheoryDataRow`'s per-row traits. Verify with `dotnet test --project tests/Yello.Tests.Isolation/... --filter "Assumption~PRD-12"` that exactly the two surface rows of `ISO-DELETE-SPACE-OWN` run (and likewise for Revocation). Extend `TestingConventionTests` to validate the row value.
   - (b) If the runner does not filter on row traits, list the two ids in `deferred-work.md` and in the `TESTING-CONVENTIONS.md` Assumption section, and say so. Cost: a manual step when PRD §12 #2 is decided.
9. **AC13's outcome.**
   - (a) **Recommended.** Write the browser observation first. Then, if the hook does **not** run for `NavigateTo`, make every Space-leaving `NavigateTo` call `ActiveSpace.Navigating(target)` first. Do that through one helper, for example `SpaceNavigation.Leave(nav, activeSpace, uri, replace)`, plus a static gate that no other `NavigateTo` targets a route outside the current Space. Sign-out and Session end are already purged by the Session purge; the gate names them.
   - (b) Rely on the hook if it is shown to run. Keep 3.3's explicit call anyway as defence in depth.
10. **The real-Host second Space** (AC7). `RealHostFixture.AddSecondSpaceAsync` writes the Space with SQL and seeds no Statuses.
    - (a) **Recommended.** Create it through `POST /api/v1/spaces` with the actor's Session, so it has its Statuses and the Board renders. Leave the SQL helper for the cases that use it.
    - (b) Extend the SQL helper to seed Statuses. Rejected: it duplicates 3.1's creation.

None of the recommended options relaxes RLS, a session key or a gate. Q5 (b) is the only **[LEE]** option.

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. AC7 vs AC2.** AC7 says that any record from "these cases" is `CrossSpace`. AC2's lesser-Role arm is a Member refused by the matrix, and AD-3/AR-10 require `InsufficientRole` there, without conversion. Reading: AC7 binds the cross-Space cases; AC2's Viewer and Admin arms assert `InsufficientRole`. Record it in Completion Notes.
- **C2. "The browser surface".** The epic says browser; the isolation suite's surfaces are Session and ApiToken over HTTP (1.9 Q3 (a), `TESTING-CONVENTIONS.md:9`). Real Chromium cases live in `Yello.Tests.Client`. Q4 links the two.
- **C3. The test design's "Two-Space fixture"** (`test-design-qa.md:134`) is two Accounts with two Spaces. It has no one-Account variant, so AC8 adds it.
- **C4. R16** (`test-design-qa.md:183`) says client invariants have "no test level". 1.9 and 3.2 gave them one (Playwright with the fake and the real Host). Client **memory** is still the gap that Q2 addresses.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:407` → **CLOSED by 3.4**, naming the row id families and the gate.
- `:606` → **CLOSED by 3.4**, naming `LabelApplications`, the existence reads and the no-wait cases with their plants.
- `:609` → **CLOSED by 3.4**, with each plant's result (or "masked", with the reason).
- `:675` → **CLOSED by 3.4**, naming the static gate and its plant.
- `:680` → **CLOSED by 3.4**, listing each of the eight cases' plants and results.
- `:688` → **CLOSED by 3.4**, with the observation and Q9's outcome.
- `:696` → **CLOSED by 3.4** (Q8 (a)), or noted with the listed ids (Q8 (b)).
- **New entries, if they arise:**
  - Q2's heap-scan outcome, if it is not made a case. Owner **Lee**, severity low.
  - Any route that AC3's gate exempts. Owner: the story that adds the route.
- Give any other deferral an owner and a severity. Epic 3 ends here, so nothing can be owned by "epic 3".

### Architecture compliance (must)

- **Never** disable RLS, open a second connection to dodge the policy, or set the `AccountId` key outside `SpaceCreationScope` (AD-24). Test-only raw SQL lives in `WireDatabase` and is run by the test, never by the product.
- **Application locks are named for the request's Space** (the 2.9 review rule): `Yello/LabelApplications/{spaceId:D}/{labelId:D}`. Never name a lock with an id the caller could not see.
- **No test-only code in production assemblies** (1.9 Q4). No `#if DEBUG` hooks in `Yello.Client` for AC5.
- **Logs**: `SpaceId` only (I-17). The case runner already checks every row's log slice.
- **Ring rule**: `AllowedReferenceEdges.cs`. `Yello.Tests.Client` does not reference `Yello.Client` (black box). AC12's gate lives in `Yello.Tests.Architecture`, which may already load `Yello.Client`; check `AllowedReferenceEdges` before adding an edge, and record it if one is needed.

### Library / framework

No new package. Use:

- .NET 10.0.11. `System.Collections.Frozen` and `System.Buffers.SearchValues` are in the BCL.
- EF Core 10.0.11, Microsoft.Data.SqlClient 7.0.2 (`sp_getapplock` via raw SQL, as `ApplicationLocks` does).
- Blazor WebAssembly 10.0.11.
- xunit.v3 4.0.0. `TheoryDataRow<...>` carries `Traits`; check the exact member (`WithTrait` or the `Traits` dictionary) in 4.0.0 before relying on it.
- Testcontainers 4.6.0, Microsoft.Playwright 1.62.0, ArchUnitNET 0.13.3.

There is no bUnit and no axe-core; adding either needs Lee. Web research was not needed: every API this story touches is already used in the repository at the pinned versions, except the runtime heap access in Q2's spike, which the spike itself verifies.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:

- Use only these traits: `Suite`, `Priority`, `Requirement` (cite FR-15, FR-36, AD-2, AD-4, AD-20, NFR-1, SM-1), `Assumption` and `Tier`.
- Name each test as an underscore sentence. Never use `Task.Delay`. Races hold a lock and wait on `LCK_M_%`.
- **Every absence has an anchor and is seen red under a plant** (`:192`). A plant caught 0/3 is recorded as masked, never deleted (`:151`).
- Seed through the product, using `WireDatabase` only where no endpoint exists. Seed after the log cursor. Randomise names. Clean up by container or rollback.
- **Memory budget (CLAUDE.md).** Never run two `dotnet test` processes at once. Iterate with `--project ... --filter`. A full run uses `--max-parallel-test-modules 1`. The Client suite can starve under a full run; rerun it alone before calling a failure real.

**Minimum plant table** (each restored in a `finally`; rebuild `Yello.Client` in Debug before each browser plant):

| # | Plant | Expected red |
|---|---|---|
| 1 | Token binding check skipped (`ActiveSpaceResolution.cs:159`) | every AC3 row (ApiToken surface) |
| 2 | Role read cached per Account across Spaces (for example, the first Membership's Role reused) | AC2 Viewer/Admin rows, I-10 row |
| 3 | Handler's Space check and EF filter dropped on Project/Task/Label lookups (both layers, the falsifiable-layer rule) | AC1 rows; one layer alone stays green |
| 4 | `Max Pool Size=1` removed from the pooled world | AC4 anchor (session count) |
| 5 | Session key not reset per unit of work (for example, set once per connection, or `@read_only = 0` with the reset skipped) | AC4 |
| 6 | Each old locking read restored (`TaskLabel` range, the submitted Labels with no prior visible read, the Project point lock with no existence read) | each AC10 no-wait case |
| 7 | Task filter and FILTER predicate dropped on the column count and seek | `ISO-BOARD-COLUMN-AGGREGATE`, `ISO-BOARD-AGGREGATE-PAGED` |
| 8 | Each of 3.2's eight absences injected | E2E-001, 003, 005, 007, 008, 010, 014, 020 |
| 9 | `BoardView`, `ProjectList` or `LabelSection` purge registration skipped (one at a time) | AC5 |
| 10 | `Directory.Lists` guard bypassed plus a deep link to another Account's Space | AC7 (refusal count 1) |
| 11 | A `static List<string>` in a client service; an `AddSingleton` | AC12 gate |
| 12 | `Navigating(null)` removed from `SpaceDeleteSection.Leave` (and one other exit) | AC13 cases |
| 13 | `Assumption` dropped from `ISO-DELETE-SPACE-OWN` | AC14 filter check |

**Run** (CLAUDE.md):

1. Check Docker with `docker info --format '{{.ServerVersion}}'`.
2. `dotnet build Yello.slnx` (0/0).
3. `dotnet build-server shutdown`.
4. `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`.

Rerun once before calling a failure real. Run timing rows alone if they flake (`ISO-TIMING-IN-SPACE` once failed under load and passed alone, 3.2).

### Previous story intelligence (3.3, 3.2, 2.9, 1.9)

- **3.3:**
  - The location-changing hook was not seen to run for the deletion's `NavigateTo` (AC13). An explicit `Navigating(null)` fixed focus.
  - Row-driven cases cannot carry an `Assumption` (AC14).
  - `ISO-DELETE-SPACE-OWN` uses `FreshActorDeletesItsOwnSpaceAsync` (`IsolationCases.cs:1903`).
  - The build gates bite: S1200 (too many dependencies), S3218, S4261, S125. The copy gates reject quoted copy in XML doc comments.
  - The full run once failed all 8 races in 0 ms (engine start). The rerun rule applied.
  - Final totals: 1,679 / 1,670 / 0 / 9.
- **3.2:**
  - Its code review found absence assertions never seen red, which is AC11.
  - With `Purge()` skipped, only the bar's Space name survived in the DOM, because the keyed components rebuilt. So AC5's plants must target the **registrations** with the departed page held mounted, or they will be masked by `@key`.
  - The two-tab case cannot see a static, which is AC12.
- **2.9:**
  - Space-named application locks replaced range locks.
  - The PG harness shows how to prove that "B does not wait on A", with an anchor that A's own competing write does wait.
  - `:609`: three rows lack their own plant.
- **1.9:**
  - The case list is data, with its gates.
  - "Browser surface" means the Session wire (Q3 (a)).
  - The pooled case needed concurrent requests before its plant was caught (plant 13). Sequential requests alone reuse one connection even unpinned.
  - Plants are SHA-checked against a pre-run snapshot.

### Git intelligence

Recent commits:

- `59e2691` (3.3 review fixes)
- `4f74ea7` (3.3 implementation: 52 files, mostly `Yello.Host/Spaces`, `SpaceStore`, the client deletion section, `IsolationCases.cs`, `RevocationCases.cs`)
- `ca315e2` (3.3 ATDD)
- `a8fe2c6` (3.3 story)
- `5e04528` (3.2 review fixes)

The pattern is story, then ATDD scaffolds (`[Fact(Skip = "red: ACn - ...")]` plus `atdd-checklist-<key>.md`), then implementation, then review fixes. The orchestrator commits each one. If ATDD runs, its rows arrive with `PendingUntil: "3.4"` and its facts skipped `red:`. Activate them one at a time, and correct a wrong scaffold rather than deleting it.

### Project Structure Notes

- **Updated (expected):**
  - `tests/Yello.Tests.Isolation/IsolationCases.cs`, `Expectation.cs`, `IsolationWorld.cs`, `IsolationCaseListTests.cs` (Q3 gate), `IsolationCaseTests.cs` (traits)
  - `tests/Yello.Tests.Revocation/RevocationCases.cs`, `RevocationCaseTests.cs` (traits)
  - `tests/Yello.Tests.Shared/WireDatabase.cs` (`AddMembershipAsync`), `ProductActor.cs` (seeding in a named Space)
  - `Yello.Infrastructure/Persistence/ApplicationLocks.cs`, `LabelStore.cs`, `TaskStore.cs`, `ProjectStore.cs`
  - the Slices store race tests (Q6)
  - `tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs`, `FakeYelloApi*.cs`, `RealHostBrowserTests.cs` / `RealHostFixture.cs`
  - `tests/Yello.Tests.Architecture/TestingConventionTests.cs` (row Assumption)
  - `Yello.Client/Localisation/RefusalCopy.cs`, `ClientRoutes.cs`, `SpaceArrival.razor`, `ViewToggle.razor` (Q7 (a) only)
  - `Yello.Client` navigation helper (Q9 (a) only)
  - `_bmad-output/implementation-artifacts/deferred-work.md`, `tests/TESTING-CONVENTIONS.md` (row Assumption, two-Space world)
- **New (expected):**
  - `tests/Yello.Tests.Architecture/ClientStaticStateGateTests.cs` (AC12)
  - a `NavigateTo` gate (Q9)
  - optionally `tests/Yello.Tests.Client/SpaceSwitchMemoryTests.cs` (same collection)
- **No migration** is expected. AC10 changes lock statements, not schema.
- No new project, package or container-backed class.

### References

- `_bmad-output/planning-artifacts/epics.md`:
  - `:171` (SM-1), `:183-223` (AR-5, AR-7, AR-10, AR-11, AR-35)
  - `:275` (UX-DR31), `:352`, `:368` (coverage)
  - `:408`, `:441`, `:877-915` (1.9's baseline), `:956`
  - `:1430-1611` (Epic 3; 3.4 at `:1575-1611`)
- `prd.md`:
  - `:58`, `:184-211` (FR-5 to FR-7), `:243-250` (FR-9), `:321-353` (FR-15, FR-16)
  - `:594-616` (§4.10, FR-35, FR-36), `:683-700` (NFR-1, NFR-2), `:730-734` (NFR-7)
  - `:854` (SM-1), `:893` (§12 #2)
- `ARCHITECTURE-SPINE.md`: `:71-112` (AD-1 to AD-6), `:195-199` (AD-20), `:219-230` (AD-24 + amendments), `:284`, `:435`
- `EXPERIENCE.md`: `:91`, `:130-147`, `:158-168`, `:172-179`, `:501-509` (UJ-4). `DESIGN.md:470`
- `SPEC.md`: `:92-97`, `:178`, `:196-214`
- `implementation-readiness-report-2026-08-22.md`: `:453`, `:542`, `:544`, `:769`, `:968-971`, `:1261`
- `sprint-change-proposal-2026-09-13.md`: no 3.4 content (story 1.3's display name only)
- `test-design-qa.md`:
  - `:134-140` (fixtures), `:183` (R16), `:197`, `:209`, `:238-241`
  - `:272-276` (I-8, I-9, I-10, I-12)
- `test-design-architecture.md`: `:215` (R5), `:231` (R17), `:329-335`, `:368-386`, `:487`
- `deferred-work.md`: `:407`, `:606`, `:609`, `:675`, `:680`, `:688`, `:696`
- Story files: `1-9-…md` (Q3, Q8, plant 13), `2-9-…md`, `3-2-…md` (plants `:391-402`, `:523-540`), `3-3-…md`
- `tests/TESTING-CONVENTIONS.md`: `:9`, `:44-73`, `:97-102`, `:151`, `:167-180`, `:192`, `:677-737`

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-10-31).

### File List
