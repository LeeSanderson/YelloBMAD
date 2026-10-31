---
baseline_commit: f3139a1
---

# Story 3.2: Switch between Spaces with nothing carrying over

Status: done

Epic: 3 — Several Spaces, cleanly separated
Story key: `3-2-switch-between-spaces-with-nothing-carrying-over`

**Requirements owned.** From the Story Coverage Index (`epics.md:366`: "3.2 | UX-DR31, UX-DR33, UX-DR39, NFR-1").

- **UX-DR31** (`epics.md:275`; source text `EXPERIENCE.md:158-168`). Space switch, sign-out, Account switch, lease invalidation and back/forward each purge everything Space-scoped, synchronously, before any render. Nothing Space-scoped goes into Web Storage or IndexedDB. The first read of a Space is never optimistic. An id in a URL confers nothing. No Space-scoped request is made for a Space id absent from the current switcher answer. Nothing is prefetched across a Space boundary.
- **UX-DR33** (`epics.md:277`; `EXPERIENCE.md:179`). Scroll restoration is disabled on Space-scoped routes. `document.title` is fixed.
- **UX-DR39** (`epics.md:283`; `EXPERIENCE.md:402`). A Space switch moves focus to the context bar and announces the new Space and Role politely.
- **NFR-1** (`prd.md:683-690`). Isolation holds for identifiers and under error, with no acceptable failure rate.

**Also binding, though the trace row omits them:**

- **NFR-9** (`prd.md:753-757`). Space switching is one of the five flows gated at WCAG 2.1 AA. AC9 invokes it by name.
- **FR-9** (`prd.md:243-250`). The switcher lists exactly the Account's Spaces. A switch changes the acting Role with no carry-over. `epics.md:302` says the switcher "lists one Space until Epic 3", so this story is where FR-9 first meets several Spaces.
- **NFR-2** (`epics.md:155`). No request is authorised with a Role from a previous active Space.
- **FR-15** (`prd.md:330`). Authorisation is never cached across a switch.
- **AR-9 / AD-3** (`ARCHITECTURE-SPINE.md:93`). Every Space-scoped route carries `{spaceId}`. There are no bare-id deep links.
- **AR-14 / AD-7** (`:118`). No credential is held in Web Storage. UX-DR31 extends that rule to content.
- **UX-DR9, UX-DR28** (`epics.md:247, 272`). The context bar is always present. The Role chip is display-only. The accessible name states the Role and what it permits.
- **UX-DR32** (`epics.md:276`). The route back goes to a Space from the switcher, never to a remembered last Space.
- **UX-DR38** (`epics.md:282`; `EXPERIENCE.md:383-385`). Announcements never carry cross-Space information.
- **UX-DR40, UX-DR41** (`epics.md:284-285`). Text spacing, 320px reflow, and the Role chip at every breakpoint.

**This story is a delta on 1.7, not a rebuild.** Story 1.7 (done) built the purge seam (`ActiveSpaceState`), the storage ban, `scrollRestoration = 'manual'`, the fixed title, the unlisted-id refusal, switch focus, the announcement and the Role from the destination. It built them against one Space, where "nothing carries over" is trivially true (`implementation-readiness-report-2026-08-22.md:969-972`). Epic 2 then registered the Board, Labels, Projects and the List view with that seam. **3.2 proves those rules hold now that an Account really has several Spaces, closes the gaps the survey below found, and closes the four ledger entries that name 3.2.** Re-implementing the seam, the storage ban or the switch focus is a defect. Extend what exists.

**Explicitly not in this story:**

- **Lease invalidation** (`MembershipChanged`) as a purge trigger: stories **7.3 and 7.7**, which build the sync lease. UX-DR31 lists it, and 3.2's ACs leave it out. Replicas, projections and queued inbound frames do not exist until Epic 7. 3.2 asserts the purge for what exists today and states that Epic 7 registers the rest with the same seam.
- **The FR-33 durable buffer**: story 7.x. When it lands, it lives in memory only (`epics.md:2647`).
- **One Account, two Spaces in the isolation suite** (SM-1 extension, API rows, "nothing in client memory"): story **3.4** (`deferred-work.md:407`). 3.2 adds **no** `/api` route, so 1.9-GATE-002 requires no new isolation row. If the dev phase adds one anyway (Q5 (a)), it must follow GATE-002 and GATE-003.
- **Deleting a Space** and the empty or failed switcher (`deferred-work.md:356`, owned by 3.3).
- **Transport failures** that render the refusal page (`:354`, owned by 7.6).
- **axe-core**: blocked on Lee (`deferred-work.md:589`). Use deterministic ARIA and focus assertions.
- **No new NuGet package and no new project** (CLAUDE.md). **No Azure deployment, login or push. No commit**: the orchestrator commits.

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

- `:358` — **the route back always lands on `spaces[0]`**, so a `409` or `403` from that Space loops (`Pages/Home.razor:86`). AC12, Q1.
- `:364` — **the route-back browser case reaches the unlisted id by a full reload**, so it cannot catch a last Space remembered in memory. An in-app navigation would. AC13.
- `:542` — **no plant proves the Label purge** (`LabelSection.Purge`, `BoardView.PurgePanel`). AC14, Q3.
- `:669` — **AC16's Tab order and visible focus have no browser case** on `/spaces/new` or Space settings' rename section. AC15.

**Depends on** (all done): 1.4 (`SessionState` purge), 1.7 (the switcher, `ActiveSpaceState`, `SpaceDirectory`, `SpaceArrival`, the context bar), 1.9 (`SpaceScopedFirstLoadTests`, `RefusalFingerprint`), 2.2-2.4 and 2.10 (the Board, Labels and List view purge registrations), 2.11 (the context bar at 320px), and 3.1 (several Spaces per Account, the create page, the Space settings link).

## Story

As someone who works in several Spaces in a day,
I want a switch to change the entire working surface and leave nothing of the previous one behind,
So that I am never in doubt which Space I am in, and nothing from one can ever be rendered in another.

## Acceptance Criteria

AC1 to AC10 are reproduced **verbatim** from `epics.md:1487-1529` (`status: final`) and numbered by position. AC11 to AC18 come from the UX spine, the ledger and a survey of the code, and each one cites its source.

**AC1 — the switcher** (FR-9, UX-DR9; `EXPERIENCE.md:203`)
**Given** an Account holding Membership in more than one Space
**When** the switcher opens
**Then** it lists exactly those Spaces by name and nothing else, and it remains the only list of Spaces anywhere in the product

> Read "the only list of Spaces" as **the only list of the Spaces an Account belongs to**. The AD-24 amendment (`ARCHITECTURE-SPINE.md:225`) already permits story 5.4's delete-Account refusal to name the Spaces the Account owns. Record that exception in the gate's message rather than writing a gate 5.4 must break silently.
>
> **Second named exception (Lee, code review decision D1, 2026-10-31):** Account settings' Token-binding `<select>` (story 1.8) lists the Account's Spaces to choose a Token's binding, not to reach a Space. It stays, named in the gate's allow-list and message beside the 5.4 exception. Any third list is a defect.

**AC2 — the Role is the destination's** (FR-9, NFR-2)
**Given** a Space is selected
**When** the switch completes
**Then** the acting Role becomes whatever that Account holds in the destination with no carry-over
**And** no filter, sort, scroll position, open Task or Project selection survives

**AC3 — the purge triggers** (UX-DR31)
**Given** a Space switch, a sign-out, an Account switch, or back/forward navigation
**When** any of them occurs
**Then** everything Space-scoped for the departed Space is purged **synchronously, before any render** — replica, projection, cached Board, queued inbound frames, pending announcements, Assignee and Label lists, Status sets, filter and sort state, scroll position, and every id-keyed cache

**AC4 — a data rule** (UX-DR31; `EXPERIENCE.md:164`)
**Given** the purge rule
**When** it is implemented or reviewed
**Then** it is treated as a **data** rule rather than a presentation one: "switching Space carries nothing over" means the data is gone, not merely unrendered

**AC5 — no browser storage** (UX-DR31, AR-14)
**Given** any Space-scoped content
**When** browser storage is inspected
**Then** none of it appears in `localStorage`, `sessionStorage` or IndexedDB
**And** the reason is stated: this also stops browser session-restore repopulating a Space after access has ended, and stops one Account's content surviving sign-out on a shared machine

**AC6 — no optimistic first read** (UX-DR31)
**Given** the first read of a Space
**When** it is in flight
**Then** nothing renders optimistically from cache before the server answers, because rendering a remembered Board while the 404 is in flight is a disclosure with a short lifetime rather than a fast interface

**AC7 — an id confers nothing** (NFR-1, UX-DR31, AR-9)
**Given** a Space, Project or Task id in a URL
**When** it is followed
**Then** it confers nothing and renders nothing from cache before the server answers
**And** the client never issues a Space-scoped request for a Space id absent from the current switcher response, which closes both a warm-started remembered Space and a deep link followed after removal

**AC8 — scroll restoration** (UX-DR33)
**Given** scroll restoration
**When** a Space-scoped route is revisited
**Then** it is disabled, because it is browser-owned and URL-keyed and would otherwise re-present a previous Space's position independently of every purge rule above

**AC9 — focus and announcement** (UX-DR39, NFR-9)
**Given** a switch completes
**When** focus settles
**Then** it moves to the context bar with the new Space and Role announced politely
**And** this is specified rather than inherited from the generic dialog rule, because returning focus to an element whose accessible name has changed does not reliably re-announce, and Space switching is one of NFR-9's five gated flows

**AC10 — two tabs** (`EXPERIENCE.md:93`)
**Given** one Account with two browser tabs open on two different Spaces
**When** each is used
**Then** each tab holds its own Space context and the two share no state

**AC11 — every Space-scoped holder is registered with the purge** (AC3, AC4; survey)

- Every client object that holds Space-scoped state for longer than one render **either** registers with `ActiveSpaceState.RegisterPurge` **or** is a component keyed on the Space and rendered only inside `SpaceArrival`. Each holder is named in a list in `ActiveSpaceState`'s remarks.
- **`Spaces/SpaceNarration`** (`_pending`) is a **pending announcement**. It is registered with no purge today. A sentence held for Space A's home ("narrated, then routed", UX-DR29) can therefore be said in Space B if the person switches first. It must be purged on a Space change.
- `ContextBar`'s `_announcement` is already cleared on purge. Keep that.

**AC12 — the route back never loops** (UX-DR32; `deferred-work.md:358`; Q1)

- When a listed Space answers `403`, `404` or `409` on arrival, "Back to Yello" lands on a **different** listed Space whenever one exists, and never on the one that just refused.
- When every listed Space has refused in this load, the person sees the refusal page with the context bar shell, whose switcher still lists every Space. They never see a redirect loop.
- Nothing records a last-visited Space (`SpaceSwitcherGateTests.No_client_code_remembers_a_last_Space` stays green, and its regex is not widened to exempt the new state).

**AC13 — the route back after removal, reached in-app** (UX-DR32, AC7; `deferred-work.md:364`)

- A tab that **visited** Space A in this load, and then has A removed from its switcher answer, follows an in-app navigation (`Blazor.navigateTo`, no reload) to A.
- It is refused with **no** request for A, and its route back lands on a listed Space and never on A.

**AC14 — the purge is proved by a plant, with a page kept mounted** (AC3, AC4; `deferred-work.md:542`; Q3)

- A switch that keeps the same page type mounted, for example Back from `/spaces/{B}/settings` to `/spaces/{A}/settings` or Board to Board, leaves no Label name, Status name, Task title or Project name of the departed Space in the DOM before the destination answers.
- Each defence layer that guards this is either proved by a plant that turns the case red, or recorded as unprovable from the browser with the reason (Q3).

**AC15 — keyboard and visible focus** (NFR-9, UX-DR40; `EXPERIENCE.md:370-375`; `deferred-work.md:669`)

- A switch made by keyboard alone works: Tab to the trigger, Enter, Tab or arrow into the rows, then Enter. Focus ends on the context bar and the announcement is made.
- The context bar's Tab order follows reading order: skip control, switcher trigger, Space settings link (Owner or Admin), Account, Sign out. Every stop shows a visible focus indicator that is not inset (computed `outline-style` is not `none`, or the `:focus-visible` token is applied).
- The Tab order and focus ring are also asserted on `/spaces/new` (name, Create, Cancel) and on Space settings' rename section (Rename, field, Save, Cancel). This closes `:669`.

**AC16 — an Account switch in one tab** (AC3; `EXPERIENCE.md:164`)

- Account 1 signs out and Account 2 signs in on the same tab, with no reload. Nothing of Account 1 remains: no Space name in the switcher or the context bar, no Project, Task or Label in the DOM, and no `SpaceDirectory` answer.
- The switcher is refetched for Account 2 before anything Space-scoped is requested.

**AC17 — the Role chip and affordances follow the destination** (AC2, UX-DR27, X-9)

- Switching from a Space held as Owner to one held as Viewer shows `Viewer` in the chip and the Viewer accessible name.
- None of the Owner's affordances survive into the destination, either while its answer is held or after it arrives: no "Create a Project", no Space settings link, and no Task create or edit control on the Board.
- Switching back as Owner restores them, read from the destination's answer and never from a client cache.

**AC18 — the HTTP cache** (AC5's stated reason; Q2)

- A Space-scoped API response must not survive sign-out on a shared machine in the browser's HTTP cache. That is the same exposure AC5's reason names, by another door.
- Apply Q2's decision and test it over the wire.

## Tasks / Subtasks

- [x] **Task 1: decide the open questions** (all ACs)
  - [x] Read Dev Notes, then Open questions. Decide each one as a reversible decision and record it in Completion Notes as "Qn - (x)", with its cost. Each recommendation is the default. No human is in the loop, so do not stop to ask.
- [x] **Task 2: survey the purge holders, then fill the gaps** (AC3, AC4, AC11)
  - [x] Grep `Yello.Client` for every field holding Space-scoped data: `Dictionary`, `HashSet`, `List<`, `IReadOnlyList<`, and the `*Response` types. Classify each one as purge-registered, keyed-and-arrival-gated, or Account-scoped (`SpaceDirectory`, `ContextBar._spaces`) or Session-scoped. The survey below is the starting point. Verify it, do not trust it.
  - [x] Register `SpaceNarration` with the Space purge. The cleanest way is in the composition root (`Program.cs:158-175`, beside `activeSpace`'s wiring): `activeSpace.RegisterPurge(spaceNarration.Forget)`. `SpaceNarration` gains `Forget()`. Keep its "never Space content" remark, and add that a pending sentence is still bound to the Space it was said in.
  - [x] Write the holder list into `ActiveSpaceState`'s remarks, and add that Epic 7 registers its replica, projection, inbound-frame queue and lease with this seam (UX-DR31's remaining items).
- [x] **Task 3: the route back** (AC12, AC13; Q1)
  - [x] Implement Q1. Under (a), `SpaceDirectory` gains an in-memory, per-load set of Space ids that **refused this tab** (`RefusedInThisLoad` or similar; the name must not match the `RemembersASpace` regex at `SpaceSwitcherGateTests.cs:457-461`, and the regex must not be widened to make it pass). `SpaceArrival.Refuse` adds the refused id. `Home.razor:86` navigates to the first listed Space **not** in the set. If every listed Space is in the set, Home renders the refusal page with the shell, whose switcher remains usable.
  - [x] The set is cleared by `SpaceDirectory.Forget()`, which already runs on Session end. A successful arrival in a Space removes that id from the set, so a Space that recovers (for example after a `409` for the concurrent-Sessions bound) is offered again.
  - [x] The set is **never a destination**: it can only exclude Spaces. Note that in the remarks, together with the UX-DR32 reason.
- [x] **Task 4: scroll** (AC2, AC8; Q4)
  - [x] Verify in the browser what happens to `window.scrollY`, and to the Board's and List view's scroll containers, on a switch and on Back between Spaces. `history.scrollRestoration = 'manual'` already exists (`wwwroot/index.html:121`). Blazor's own scroll behaviour on internal navigation is **unverified**: measure it, do not assume it.
  - [x] If any scroll offset of the departed Space survives, reset it per Q4 and record what you measured.
- [x] **Task 5: the HTTP cache** (AC18; Q2)
  - [x] Implement Q2. Under (a), the `api` route group sets `Cache-Control: no-store` on every response, in one place in the pipeline (`ApiRouteGroup` or a small middleware scoped to `/api`), not per endpoint. The existing per-endpoint `no-store` lines (`IdempotentResponses.cs:182`, `ApiTokenEndpoints.cs:99`) stay, or are folded in. Record which.
  - [x] Add a wire case to `ActiveSpaceEndpointTests` (an existing container class) asserting `no-store` on a Space-scoped `200`, on the boundary `404`, and on the switcher `200`.
- [x] **Task 6: gates** (AC1, AC4, AC5, AC11; each proved against a plant. Save the original content and restore it in a `finally`. **Never `git checkout`, `restore` or `reset`.**)
  - [x] **The switcher is the only list of the Account's Spaces** (AC1): a static gate in `SpaceSwitcherGateTests` asserting that only `SpaceSwitcher.razor` renders rows from `SpaceIdentityResponse` (a `@foreach` over `SpaceDirectory.Current` or over the `Spaces` parameter). `Home.razor` may read the list to choose a destination and must render none of it. Its message names the 5.4 exception (AC1 note). Plant a `@foreach` over `Directory.Current` in `Home.razor`.
  - [x] **Every Space-scoped holder is purged** (AC11): extend `ActiveSpaceStateTests` with a unit case asserting that a sentence held in `SpaceNarration` is gone after `Purge()` and survives a same-Space navigation, and an IL or source gate asserting that `Program` registers `SpaceNarration` with the Space purge. Plant: remove the registration.
  - [x] Keep green: `ClientStorageBanTests`, `No_client_code_remembers_a_last_Space` (and its plant tests), `The_host_page_disables_scroll_restoration_before_the_runtime_loads`, `No_client_code_sets_the_document_title`, `The_Role_chip_is_never_interactive`, `ActiveSpaceStateTests.*`.
- [x] **Task 7: browser tests** (see Dev Notes, then Testing requirements). Build `Yello.Client` in **Debug** before any browser run or plant.
- [x] **Task 8: ledger, conventions, verification**
  - [x] `deferred-work.md`: apply Dev Notes → Ledger dispositions.
  - [x] `tests/TESTING-CONVENTIONS.md`: add a short note, "Two tabs and an Account switch, added by story 3.2". It covers how a case opens a second page in one `IBrowserContext` (`context.NewPageAsync`, the same `FakeYelloApi` installed on each page or one per page, as decided), and how a case changes the signed-in identity of `FakeYelloApi` between a sign-out and a sign-in.
  - [x] Rewrite any comment that predicts 3.2 so it describes what exists (`grep -rn "3\.2" Yello.Client Yello.Host tests`).
  - [x] Run `dotnet build Yello.slnx` (0 warnings, 0 errors), then `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Record the totals. The expected skips are the 3 bundle cases plus the explicit tiers.

### Review Findings

Code review 2026-10-31 (`4837bfe..HEAD`, full mode: Blind Hunter, Edge Case Hunter, Acceptance Auditor). 35 raw findings, 24 after dedup: 2 decision-needed, 14 patch, 2 defer, 6 dismissed.

- [x] [Review][Decision] **Lee decided (a): keep the exception, amend AC1's note (done above).** AC1's "only list of Spaces" was loosened for Account settings' Token-binding `<select>` — AC1 is verbatim from the final epics, and its note names only the 5.4/AD-24 exception. The dev phase allow-listed `Pages/AccountSettings.razor` in `Only_the_switcher_renders_the_Accounts_Spaces_as_a_list` and recorded "AC1 - Token select" as a reversible decision. That select (story 1.8) renders the Account's Space names as options to bind a Token. Not an open question the story delegated.
- [x] [Review][Decision] **Lee decided (b): ArrowDown on the open trigger focuses the first row; Tab order unchanged; a test asserts the arrow path (`ArrowDown_on_the_open_trigger_moves_focus_to_the_first_row`). Applied as a patch.** AC15's keyboard switch passes Space settings, Account and Sign out before the first row — `ContextBar.razor` renders `SpaceSwitcher` after `context-bar__account-controls`, so after Enter on the trigger, Tab reaches settings (Owner/Admin), Account and Sign out before any row. AC15 says "Tab to the trigger, Enter, Tab or arrow into the rows". The test uses `TabUntilAsync`, which tolerates the extra stops. The ATDD checklist flagged it for dev-story; no decision was recorded.
- [x] [Review][Patch] A late refusal can be recorded against the Space the tab has moved to: `Directory.Refused(SpaceId)` and `Refuse` run after `await ProblemOfAsync` with no token check [Yello.Client/Components/SpaceArrival.razor:185]
- [x] [Review][Patch] A transport failure or a 5xx is recorded as the Space refusing, so one network blip excludes a healthy Space for the load and a single-Space Account's route back dead-ends (before 3.2 it retried). AC12 names `403`, `404` and `409` only; record those alone (also removes the duplicated "unless left" guard in the unlisted branch) [Yello.Client/Components/SpaceArrival.razor:183,218]
- [x] [Review][Patch] `ApiNoStore`'s remarks claim a `401`, a `503` and the unmatched path, but the wire case proves four `GET`s; add the anonymous `401` and the unmatched `/api` path, and state what is proved [Yello.Host/ApiNoStore.cs:15; tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CacheControl.cs]
- [x] [Review][Patch] The composition-root gate checks `RegisterPurge` and `SpaceNarration.Forget` separately, so `sessionState.RegisterPurge(spaceNarration.Forget)` would pass [tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.CarryOver.cs:~100]
- [x] [Review][Patch] The AC12 `Home` gate passes on "Refused" in a C# comment and misses `spaces.First()` / `spaces.ElementAt(0)` [tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.CarryOver.cs:139]
- [x] [Review][Patch] The AC14 record overstates: the all-layers-off plant went red at the anchor (B's own Labels before Back), not at the held-absence assertion; correct the `:542` closure, the test remark and the Debug Log [deferred-work.md:542; tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs:~735]
- [x] [Review][Patch] The 4.4 ledger entry still says 4.4 "decides whether the held sentence should also be cleared on a Space purge"; 3.2 decided it [deferred-work.md:541]
- [x] [Review][Patch] The holder list in `ActiveSpaceState`'s remarks omits `ContextBar`'s announcement and displayed Space/Role (cleared through `Changed`) and the Session-scoped purges in `AccountSettings` and `CreateSpace` [Yello.Client/Spaces/ActiveSpaceState.cs:32]
- [x] [Review][Patch] The two-tab case compares `focusedBefore` with itself without anchoring it to `true` [tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs:548]
- [x] [Review][Patch] A gate remark says the plant both does and does not turn `ActiveSpaceStateTests` red [tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.CarryOver.cs:~93]
- [x] [Review][Patch] Stale comment above the new all-refused path: "with nothing to render meanwhile" [Yello.Client/Pages/Home.razor:38]
- [x] [Review][Patch] AC15 coverage: no Admin row in the bar's Tab-order theory, and the create and rename cases run at 1280 only, not 320×256 [tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs:816-925]
- [x] [Review][Patch] Q4's measurement: B's scroll before Back is unanchored, and the List view (Task 4) is not measured, though Q4 - (b) claims "every `main` element" [tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs:456]
- [x] [Review][Patch] AC17's "no Task edit control" maps to no case in the AC map; the Owner→Viewer panel case exists (`DeleteTests.A_Viewer_is_offered_no_delete_control_and_nothing_is_disabled`, `LabelTests` Viewer panel) and should be cited [this file, AC to test map]
- [x] [Review][Defer] On the all-refused page at `/`, "Back to Yello" links to the page it is on and the copy is the generic "Not available." [Yello.Client/Pages/Home.razor:93] — deferred, pre-existing (the `spaces is null` path already renders the same surface on `/`); owner story 3.3
- [x] [Review][Defer] Several new absence assertions were never seen red under a plant (E2E-001 Role text, -003, -005, -007, -008, -010, -014, -020) [tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs] — deferred; owner story 3.4 (one Account, two Spaces, "nothing in client memory")

## Dev Notes

### The shape of the story in one paragraph

Most of 3.2's ACs were built by 1.7 and registered with by Epic 2. Every Space-scoped page wraps its content in `SpaceArrival`, which renders its child only when `_arrived && ActiveSpace.Current != null`. Every child is `@key`'d on the Space (`ProjectList @key=SpaceId`, `BoardView`/`TaskListView @key=(SpaceId, ProjectId)`, `SpaceNameSection`/`LabelSection @key=SpaceId`). The purge runs in a `RegisterLocationChangingHandler` **before** the router renders. **The work is proof, not plumbing.** Write the browser cases with real several-Space data that 1.7 could not write. Close the one real holder gap (`SpaceNarration`). Fix the route-back loop. Decide the HTTP cache and scroll questions. Prove each defence against a plant, or record honestly why a browser cannot see it.

### What exists today (survey of the tree at `f3139a1`)

**Routes** (`Yello.Client/Pages/ClientRoutes.cs`):

- `SpaceHome(Guid)` → `/spaces/{id:D}` (107), `Board` (116), `List` → `.../list` (126), `SpaceSettings` (134), `CreateSpace` `/spaces/new` (72; `SpaceIn` returns null for it).
- `SpaceIn(string?) : Guid?` (149) reads the Space id, case-insensitive and in any Guid format.
- The remarks at 18-21 say "Never a remembered last Space".
- The Space reaches the server **only** as the route segment (`Yello.Contracts/Spaces/SpaceRoutes.cs`). No header carries it.
- Filter and sort never reach the URL (`TaskListPage.razor:11`). The open Task is component state, not a route.

**The purge seam** (`Yello.Client/Spaces/ActiveSpaceState.cs`):

- Members: `RegisterPurge(Action)` (91), `Navigating(Guid? target)` (107), `Purge()` (127-160), `Arrive` (170), `Renamed` (196), `ArrivingInCreated` (217), `ShowProject`/`ClearProject` (229/245), `Token` (84), `LastArrivalWasSwitch` (78).
- `Purge()` cancels `_inFlight` **first**, then nulls `Current`, `ProjectName` and `SpaceId`, runs every registration with a catch-all, then raises `Changed`.
- Wiring is in `Program.cs:158-175`:
  - `sessionState.RegisterPurge(...)` runs `activeSpace.Purge(); spaceDirectory.Forget();`.
  - the location-changing handler calls `activeSpace.Navigating(ClientRoutes.SpaceIn(target))`.
- All client services are `AddScoped`, which is per tab in WebAssembly. No `AddSingleton` and no mutable static exists in `Yello.Client`. Keep it so (AC10).

**Registered holders:**

- `BoardView.razor:241` → `Purge()` (319-330) + `BoardView.razor.cs` `PurgePanel()` (211-247), which clears `_replicas`, `_labels`, `_labelsById`, `_statusOptions`, `_openTaskId`, `_deleteKeys`, `_deleted`, `_moveOrigins`, `_selectedStatusId`, focus targets and more. `BoardView.Paging.cs:295` clears `_heldOut`.
- `LabelSection.razor:148` → `Purge()` (163-176).
- `ProjectList.razor:204` → `Purge()` (249-268).
- `TaskListView.razor.cs` (~120) → `Purge()` (~145-159), which also runs `TaskListFilters.Clear()` (`TaskListFilters.cs:94`).
- Session purges (async): `AccountSettings.razor:196`, `CreateSpace.razor:100`.

**Not registered:**

- `SpaceNarration._pending` (**the gap**, AC11).
- `PageHeadingFocus`: not Space content.
- `TaskCreate` and `SpaceNameSection`: keyed children, unmounted with their parent. Verify.
- `ContextBar._spaces` and `SpaceDirectory`: Account-scoped, kept across a switch by design, and dropped on Session end.

**The switch** (`Components/ContextBar.razor`, `SpaceSwitcher.razor`, `SpaceArrival.razor`):

- The switcher is a non-modal disclosure `<ul id="space-switcher-list">` of `<a href=SpaceHome(id)>` rows with `aria-current="page"`. The rows have **no click handler**: a switch is ordinary link navigation. Esc closes it and returns focus to the trigger. `ToggleAsync` (185-204) opens on the held `Directory.Current` and then swaps in the refreshed list. It has no arrow-key roving, so Tab moves through the rows.
- `SpaceArrival.OnParametersSetAsync` (107-149):
  - calls `Navigating(SpaceId)`;
  - checks `Directory.Lists`, refreshing once if the Space is unlisted, and refuses with no request if it is still unlisted;
  - otherwise `GET ActiveOf` → `Arrive`.
- `ContextBar.OnActiveSpaceChanged` (159-183) sets `_announcement = SpaceSwitchAnnouncement` ("{0} — {1}, {2}") and `_focusBar` when `LastArrivalWasSwitch`. `OnAfterRenderAsync` focuses `<nav class="context-bar" tabindex="-1">`.
- **A switch** is an arrival when the tab was already in a different Space: choosing a row, Back or Forward (1.7 decision, `1-7-…md:452`). A first arrival focuses the `h1` instead. `App.razor`'s `<FocusOnNavigate Selector="h1">` conflict was resolved in 1.7 by focusing the bar after the context arrives. Do not regress it.
- Re-choosing the current Space is a re-arrival (`SpaceArrival.OnLocationChanged` 89-101). It re-reads the Role in place, keeps open forms and is **not** a switch.
- The announcer is one `<p class="visually-hidden" role="status">` in the bar (103), not `LiveAnnouncement`.

**Route back** (`Pages/Home.razor:49-87`): refresh the directory → if the list is empty, show the no-Space state; otherwise **`NavigateTo(SpaceHome(spaces[0].Id), replace: true)`** (86). `RefusalSurface` links to `ClientRoutes.Home` ("Back to Yello."). This is the `:358` loop.

**Browser-owned surfaces:**

- `wwwroot/index.html:121` sets `history.scrollRestoration = 'manual'` before the runtime loads.
- The title is fixed at `Yello`.
- No `localStorage`, `sessionStorage`, IndexedDB or `BroadcastChannel` appears anywhere.
- The only scroll code is `scrollIntoView({block:'nearest'})` in `board-keys.js:95` and `list-keys.js:45`.

**Server:**

- The switcher is `GET /api/v1/spaces` (`SpaceDirectoryEndpoints.cs:38`), `AccountScoped(SpaceSwitcher)`. It joins Membership to Space and orders by name, then id (`AccountSpaceDirectory.cs:97-109`).
- The active answer is `GET /api/v1/spaces/{spaceId}` → `ActiveSpaceResponse(Id, Name, Role)` (`ActiveSpaceEndpoints.cs:58-67`). The Role is read per request by `MembershipActiveSpaceResolver` and never cached (AD-1).
- **No Space-scoped `GET` sets `Cache-Control`.** Only idempotent replays and Token creation set `no-store` (AC18).

### What must be preserved (regression guard)

- **The switcher** returns identity only: `{id, name}` (I-11, `ISO-LIST-SPACES`). Its rows carry a name and nothing else, and the foot "Create a Space" link stays **outside** the `<ul>` (3.1).
- **Purge ordering:** cancel in flight → clear → run registrations → raise `Changed`, all before the router renders. `ActiveSpaceStateTests.The_client_purges_the_departed_Space_before_the_router_renders` holds the wiring.
- **First arrival** focuses `h1`. **A switch** focuses the bar. **A re-arrival** moves nothing. **A rename** (3.1, `ActiveSpaceState.Renamed`) is not a switch and moves no focus.
- **`SpaceDirectory` survives a Space switch.** It is Account-scoped, and purging it would make every switch an unlisted-id refresh.
- **2.11's context bar at 320px:** Role chip present, no menu, no sideways scroll, the switcher label visually hidden below 30em, and the bar non-sticky below 30em of viewport height (3.1 D1).
- **3.1's create flow** refreshes the directory before navigating, and the arrival counts as a switch (`ArrivingInCreated`).
- **`document.title`** stays `Yello`, and no announcement names the departed Space (UX-DR38).

### Open questions (decide in Task 1; each is reversible; record as "Qn - (x)")

1. **How the route back avoids the Space that just refused** (AC12, `:358`).
   - (a) **Recommended.** An in-memory, per-load **exclusion** set on `SpaceDirectory`, holding the Spaces that refused this tab. Home skips them. A successful arrival removes an id from it, and `Forget()` clears it. It can only exclude, never choose, so it is not a "remembered last Space" (UX-DR32). Cost: one more field in an Account-scoped service, and a gate regex that must still catch real "last Space" fields.
   - (b) The refusal page lists the other Spaces itself. Rejected: that creates a second list of Spaces, which breaks AC1.
   - (c) Home picks a random listed Space. Rejected: it is non-deterministic and still loops when there are two Spaces.
2. **The HTTP cache** (AC18). Space-scoped `GET` responses carry no `Cache-Control`. A browser may keep them in its disk cache after sign-out on a shared machine.
   - (a) **Recommended.** `Cache-Control: no-store` on every `/api` response, set once in the pipeline, with a wire case. Cost: one header and one test. No client behaviour changes, because nothing relies on HTTP caching.
   - (b) State it out of scope, since AC5 names only the three storage APIs, and ledger it to 3.4. This is cheaper, but it leaves the AC's own stated reason half-met.
3. **Proving the Label purge (AC14, `:542`).** Every child is keyed on the Space and rendered only inside an arrived `SpaceArrival`. A planted no-op `LabelSection.Purge` may therefore still pass in the browser, because the old instance is disposed and its data becomes unreachable. That is the data rule holding by a second layer.
   - (a) **Recommended.** Plant each layer separately and record which ones the browser can see. Remove the `@key` and skip `Purge` together: the case must go red. Skip `Purge` alone: if the case stays green, record that `Purge` is defence in depth behind the key. A component's `Purge` cannot be unit-tested without bUnit (a new package, which needs Lee). So leave it covered by the combined plant, and say so. Close `:542` with that record.
   - (b) Remove the per-component purges as redundant. Rejected: Epic 7's replica needs the seam, and a page could stop keying.
4. **Scroll on a switch** (AC2, AC8).
   - (a) **Recommended.** Measure first (Task 4). If the window or a container keeps its offset across a switch, reset the window scroll to the origin in the same synchronous step as the purge. The composition root's location-changing handler calls a synchronous `IJSInProcessRuntime` `window.scrollTo(0, 0)` when `Navigating` reports a purge. Keep it in one place, use no new JS file if an inline call suffices, and keep `ClientStorageBanTests` green. Cost: one JS interop call per switch.
   - (b) Rely on Blazor's behaviour if the measurement shows a reset. Record the measurement and add the test anyway, so a framework change is caught.
5. **Isolation rows.** 3.2 adds no `/api` route. If Q2 (a) touches only headers, GATE-002 needs no row.
   - (a) **Recommended.** Add none, and say so in Completion Notes.
   - (b) Add a row only if a new route is mapped.
6. **Two tabs in the harness** (AC10). `FakeYelloApi.InstallAsync(page)` is per page.
   - (a) **Recommended.** One `IBrowserContext` (shared cookies and storage, like real tabs) with two pages, each with its own `FakeYelloApi` sharing one switcher. Tab 1 is on A, tab 2 on B. Switch, filter, open a Task and purge in tab 1, then assert tab 2's DOM, focus and held state are unchanged and that tab 2's requests named only B.
   - (b) Two contexts. Rejected: they share nothing by construction, so the case proves nothing.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:358` → **CLOSED by 3.2** with Q1's decision, naming the browser case.
- `:364` → **CLOSED by 3.2**: the in-app case of AC13, naming it.
- `:542` → **CLOSED by 3.2** with Q3's record of which plants the browser sees.
- `:669` → **CLOSED by 3.2**: the AC15 Tab-order and focus-ring cases, naming them.
- Add a note to UX-DR31's remaining items: lease invalidation, the replica, projections and inbound frames register with `ActiveSpaceState` in **7.3/7.7**. Add it only if no existing entry says so. Grep `deferred-work.md` for "lease" first.
- Give any new deferral an owner and a severity.

### Architecture compliance (must)

- **The purge stays one seam.** Do not add a second purge mechanism, a `BroadcastChannel`, a storage event or a singleton (1.7 Task 6, `EXPERIENCE.md:93`).
- **Never a remembered last Space** (UX-DR32). The Q1 set excludes and never chooses.
- **No Space-scoped request for an unlisted id** (UX-DR31). That check sits in `SpaceArrival` before any `GET`. Do not bypass it for a "fast path".
- **Server:** the Role is resolved per request (AD-1), and the Space comes only from the path (AD-2, AD-3). A header added for Q2 lives in Host. Domain and Application stay untouched (ring rule, `AllowedReferenceEdges.cs`).
- **CSS:** add only to `components.css`. Use logical properties and rem/em, with no px literal and no colour literal (`DesignFoundationGateTests*`). The focus ring uses the existing focus token, never inset (`EXPERIENCE.md:373`).
- **Copy:** only in `ClientCopy.resx`, with a `<comment>` naming 3.2 and the AC. Add no copy if none is needed, because the switch announcement exists already.

### Library / framework

No new package. Use .NET 10.0.11, Blazor WebAssembly 10.0.11, xunit.v3 4.0.0, Microsoft.Playwright 1.62.0 and ArchUnitNET 0.13.3. There is no bUnit and no axe-core, and adding either needs Lee's approval. For Q4's synchronous call, `IJSInProcessRuntime` is available in WebAssembly (cast `IJSRuntime`). Do not use `IJSRuntime.InvokeVoidAsync` inside the location-changing handler if it would let a render run first.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:

- Five trait names only (`Suite`, `Priority`, `Requirement`, `Assumption`, `Tier`). Cite UX-DR, NFR or FR ids as `Requirement`.
- One behaviour per test, with an underscore-sentence name. Never `Task.Delay`.
- Hold responses with `TaskCompletionSource` (`HoldAsync`/`Release`). Poll `location.pathname` (`WaitForPathAsync`) and never use Playwright's URL wait.
- Use `RecordAnnouncements` / `WaitUntilSaidAsync` for live regions.
- **Every absence assertion has an anchor and is first proved against a plant.**
- Randomise data, and never use a shared email literal.
- Never run two `dotnet test` processes at once. Iterate with `--project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --filter`.

**Browser** (`tests/Yello.Tests.Client`, `ClientSurfaceCollection`, `FakeYelloApi`, no container; extend `SpaceSwitchingTests` or add `SpaceSwitchCarryOverTests` in the same collection; reuse `TwoSpaces()`, `OpenSpaceAsync`, `OpenSwitcherAsync`, `ChooseInSwitcherAsync`, `RefusalFingerprint`, `AssertNoSpaceRenderedAsync`):

- **AC1:** with three Spaces at mixed Roles, the switcher lists exactly three rows, each holding the name only (no Role, count or badge) with `aria-current` on the active one. The foot link stays outside the `<ul>`. Anchor: the row count.
- **AC2 / AC17:**
  - **Owner → Viewer.** While the destination is held, no Owner affordance is present and the bar has no name or chip. After release, the chip reads `Viewer` and the accessible name is the Viewer's.
  - **Viewer → Owner.** The affordances return.
  - **On the List view of A** with a Status filter checked and a sort chosen, switch to B, open B's List: no filter is checked and the sort is the default.
  - **On the Board of A** with a Task open, switch to B and open B's Board: no panel is open.
  - **The bar's Project name** of A is gone in B's Space home.
- **AC3, triggers:**
  - Switch by row.
  - Back and Forward between two Spaces' Boards. Each holds the destination and asserts that none of the departed Space's Task titles, Label names, Status names or Project name is in `document.body.innerHTML`.
  - Sign-out (existing cases; extend with a two-Space seed).
  - Account switch (AC16).
- **AC5:** after a session through two Spaces, Board, List and settings: `localStorage.length + sessionStorage.length == 0` and `indexedDB.databases()` is empty. Extend `No_Space_scoped_content_reaches_web_storage` with the several-Space path rather than duplicating it.
- **AC6 / AC7:** the existing `SpaceScopedFirstLoadTests` cases stand. Add one more:
  - A's Board was visited, then an in-app navigation goes to `/spaces/{B}/projects/{A's project id}`.
  - While B's Board answer is held, nothing of A's Board renders.
  - On the faked `404`, the refusal page shows (`RefusalFingerprint`-identical to the unlisted case).
- **AC8:** scroll A's Board or window down, then switch and Back. `window.scrollY == 0` and each Board column's `scrollTop == 0` on arrival (Q4). Keep `A_Space_route_keeps_the_fixed_title_and_manual_scroll_restoration` green.
- **AC9:** a switch by Back between Spaces also lands focus on `nav.context-bar` and announces the destination ("{name} — {Role}, {permits}") once. The announcement never names the departed Space. Assert this with `window.__said` holding no departed name.
- **AC10:** two tabs per Q6.
- **AC11:** Space settings in A narrates a lowered Role (fake A's Role as `member`), the person switches to B before A's home is reached, and B's home says nothing about A. Anchor: without the switch, A's home says it.
- **AC12:** two listed Spaces. The first answers `409` (the concurrent-Sessions bound) and the second answers `200`. "Back to Yello" lands on the second, with no request repeated against the first beyond its single arrival. When both refuse, the page shows the refusal with the shell, the switcher lists both, and the path stops changing (assert the request count is stable after one route back).
- **AC13:** in-app navigation, with no reload, to a Space removed from the switcher after a visit. No request is made for it. The route back lands on a listed Space.
- **AC14:** a mounted-page switch (settings A → B, by Back) with Labels on both. No A Label is in the DOM while B is held. Plants per Q3.
- **AC15:**
  - **Keyboard-only switch.** Use `PressAndFocusAsync`/`FocusedAsync`, starting from `ResetFocusAsync`.
  - **Tab order** of the bar as Owner and as Viewer, where the settings link is absent.
  - **The focus ring is visible** on each stop: computed `outline-style != 'none'` and `outline-width > 0`, measured with `:focus-visible` from keyboard focus.
  - **The same checks** on `/spaces/new` and on the rename section (`:669`).
  - Run at 1280×800 and at 320×256. At 320px the Role chip is kept.
- **AC16:**
  - **Setup.** Sign out as Account 1 (Spaces A and B). The fake's identity becomes Account 2 (Space C). Sign in.
  - **No Account 1 names remain.** None of A's or B's names appear in the switcher, the bar or `document.body.innerHTML`.
  - **Request order.** The switcher request precedes any C-scoped request, and no request names A or B after sign-out.

**Architecture** (`tests/Yello.Tests.Architecture`): Task 6's gates (the single list of Spaces, and `SpaceNarration` registered), plus the `ActiveSpaceStateTests` unit case.

**Slices** (`tests/Yello.Tests.Slices`, existing `ActiveSpaceEndpointTests` only, **no new container class**): Q2's `no-store` cases. `Each_Space_reports_the_Role_held_there_with_no_carry_over_across_switches` already covers NFR-2 on the wire. Extend it only if a gap appears.

**Plants** (each restored from memory in a `finally`; rebuild `Yello.Client` in Debug before each browser plant; record them in a table in the Debug Log):

- `SpaceNarration` unregistered → the AC11 case and gate.
- `Home` back to `spaces[0]` → the AC12 case.
- The exclusion set made a destination ("go to the last refused") → the AC12 and `RemembersASpace` gates.
- `Purge()` skipped in `Navigating` → AC3 cases.
- `TaskListFilters.Clear()` removed together with the `@key` → the AC2 filter case.
- The Q4 scroll reset removed → AC8.
- `outline: none` on `.context-bar a:focus-visible` → AC15.
- A `@foreach` of Spaces in `Home.razor` → the AC1 gate.
- A shared `static` field in a client service → the AC10 case.
- `no-store` removed → the Q2 wire case.

**Run** (CLAUDE.md):

1. Check Docker with `docker info --format '{{.ServerVersion}}'`.
2. `dotnet build Yello.slnx` (0/0).
3. `dotnet build-server shutdown`.
4. `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`.

Rerun once before calling a failure real. If Client cases fail only in the full run, run that project alone before investigating (memory: the full run starves the browser suite).

### Previous story intelligence (3.1, 1.7, 2.4, 2.10, 2.11)

- **3.1:**
  - The create arrival is a switch (`ArrivingInCreated`). `Renamed` is not a switch.
  - The Space settings link is in the context bar (Owner and Admin), which makes it a new Tab stop for AC15.
  - Below 30em of viewport height the bar is not sticky.
  - The review found AC clauses with no test, and gates whose assertions did not cite their source. Map every AC to a named test in Completion Notes.
  - A browser plant that does not compile proves nothing. Rebuild the client in Debug and confirm that the plant ran.
- **1.7:**
  - The purge hook moved from `LocationChanged` to `RegisterLocationChangingHandler` (plant #14). Only the wiring gate catches a moved hook, not the browser (plant #15), so keep the gate.
  - The route-back loop and the reload-only case were deferred here.
- **2.4:** Blazor reuses a page across Spaces. The review reset `_wasPermitted`/`_leaving` on a `SpaceId` change in `SpaceSettings`. Check every page-level field (not the keyed children) for the same reuse when a page stays mounted across a switch (AC14).
- **2.10:** filter and sort are component state that `TaskListFilters.Clear()` resets. `ListViewTests.Leaving_the_Space_purges_the_filter_and_rows_before_any_render_and_stores_nothing` uses a `MutationObserver` for mixed rows, and is the pattern for AC2's filter case.
- **2.11:** at 320px the switcher label is hidden and the Role chip survives. A CSS `display` once overrode `[hidden]`, so check any new conditional element at narrow widths.

### Git intelligence

Recent commits: `f3139a1` (3.1 review fixes), `1908f44` (3.1 implementation), `3489c4f` (3.1 ATDD scaffolds), `96e8ce4`, `2662dc5`. The pattern is ATDD scaffolds, then implementation, then a review-fix commit, each made by the orchestrator. Do not commit. 3.1 touched `ContextBar.razor`, `SpaceSwitcher.razor`, `ActiveSpaceState.cs`, `SpaceDirectory.cs`, `Home.razor` and `FakeYelloApi.cs`, which are the same files 3.2 touches. Read their current state before editing.

### Project Structure Notes

- Updated (expected):
  - `Yello.Client/Spaces/SpaceNarration.cs`, `SpaceDirectory.cs` (Q1), `ActiveSpaceState.cs` (remarks)
  - `Yello.Client/Program.cs` (registration, and Q4 if taken)
  - `Yello.Client/Pages/Home.razor`, `Components/SpaceArrival.razor` (Q1)
  - `Yello.Client/wwwroot/css/components.css` (only if a focus ring is missing)
  - `Yello.Host/ApiRouteGroup.cs` or a small `/api` middleware (Q2)
  - `tests/Yello.Tests.Client/SpaceSwitchingTests.cs` (or a new `SpaceSwitchCarryOverTests.cs` in `ClientSurfaceCollection`), `FakeYelloApi.cs` (an identity switch, a per-Space `409`, second-page support), `SpaceCreateAndRenameTests.cs` (`:669`)
  - `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.cs`, `ActiveSpaceStateTests.cs`
  - `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests*.cs` (Q2)
  - `tests/TESTING-CONVENTIONS.md`, `_bmad-output/implementation-artifacts/deferred-work.md`
- No new project, package, container-backed class or migration.

### References

- `_bmad-output/planning-artifacts/epics.md`:
  - `:154-155` (NFR-1, NFR-2), `:162` (NFR-9), `:197` (AR-9), `:202` (AR-14), `:217` (AR-29)
  - `:247` (UX-DR9), `:272` (UX-DR28), `:275-277` (UX-DR31-33), `:279` (UX-DR35), `:282-285` (UX-DR38-41)
  - `:302`, `:366`, `:408`, `:1430-1529` (Epic 3, story 3.2), `:800-836` (1.7), `:912-915` (1.9)
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md`: `:82` (JTBD "without losing my place", resolved toward a clean context by `EXPERIENCE.md:91`), `:109-110` (UJ-4), `:243-250` (FR-9), `:330` (FR-15), `:683-699` (NFR-1, NFR-2), `:753-757` (NFR-9)
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`:
  - `:75` (AD-1), `:81-86` (AD-2), `:93` (AD-3), `:118` (AD-7), `:218-228` (AD-24 and amendment), `:283` (accessibility)
  - No AD governs client state, so the purge rests on UX-DR31 (readiness `:769-770`).
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md`: `:91`, `:93`, `:144-146`, `:158-168`, `:179`, `:202-203`, `:370-375`, `:383-390`, `:402`, `:501-509`; `DESIGN.md:162-168`, `:230-237`, `:427`, `:470`
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md:542`, `:769-770`, `:969-972`
- `_bmad-output/test-artifacts/test-design-qa.md`: `:268` (I-4), `:274` (I-10), `:275` (I-11), `:354` (X-3), `:357` (X-7), `:366-367` (X-8, X-9), `:183` (R16)
- `_bmad-output/implementation-artifacts/deferred-work.md:358`, `:364`, `:542`, `:589`, `:669`; story files `1-7-…md`, `3-1-…md`
- `tests/TESTING-CONVENTIONS.md`

### ATDD artifacts - red-phase scaffolds already in the tree

**Checklist:** `_bmad-output/test-artifacts/atdd-checklist-3-2-switch-between-spaces-with-nothing-carrying-over.md`. It records the scenario map, the assumed Q1-Q6 answers, the plant record and the "AC1 - Token select" finding.

**Scaffold files:**

- **`tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs`** (new, `ClientSurfaceCollection`, no engine). 20 entries, 23 cases, covering AC1-AC10 and AC12-AC17.
  - 21 cases are **green today** and stay active as standing gates.
  - 2 are skipped, `red: AC12`:
    - `The_route_back_skips_a_listed_Space_that_refused_with_409`
    - `When_every_listed_Space_refuses_the_route_back_stops_on_the_refusal_page`
- **`tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.CarryOver.cs`** (new partial). 4 cases.
  - **Green today:** `Only_the_switcher_renders_the_Accounts_Spaces_as_a_list` (AC1, proved by a plant in `Home.razor`) and `The_Space_list_scan_detects_a_planted_list`.
  - **Skipped:** `The_composition_root_registers_the_pending_narration_with_the_Space_purge` (`red: AC11`) and `The_route_back_exclusion_lives_in_the_directory_and_is_not_a_remembered_Space` (`red: AC12`).
- **`tests/Yello.Tests.Architecture/ActiveSpaceStateTests.cs`** gains 1 case, skipped `red: AC11`: `A_pending_narration_is_forgotten_on_a_Space_change_and_kept_within_one_Space`.
- **`tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CacheControl.cs`** (new partial of an existing class, no new engine). 1 case, skipped `red: AC18`: `Every_api_answer_is_no_store_on_the_switcher_a_Space_and_the_boundary`.
- **`tests/Yello.Tests.Client/FakeYelloApi.cs`** is harness, not a scaffold. It adds `ArrivalRefusals` (a listed Space refusing with `409`, `403` or `404` on arrival) and moves the active-Space answer into `ActiveSpaceAsync`. Every earlier case is served the same bytes.

**Activation order:**

1. **`The_route_back_skips_a_listed_Space_that_refused_with_409` comes first. It tests the riskiest assumption:** Q1 (a) can end the route-back loop with a set that only excludes and never chooses. Then activate `The_route_back_exclusion_lives_in_the_directory_and_is_not_a_remembered_Space`, with the `RemembersASpace` regex unchanged, and then `When_every_listed_Space_refuses_the_route_back_stops_on_the_refusal_page`.
2. **AC11:** first `A_pending_narration_is_forgotten_on_a_Space_change_and_kept_within_one_Space`, then `The_composition_root_registers_the_pending_narration_with_the_Space_purge`.
3. **AC18:** `Every_api_answer_is_no_store_on_the_switcher_a_Space_and_the_boundary`, with Q2 (a).
4. **The standing gates:** keep all 23 green throughout. Run the story's plant table against them, including Q3's combined plant (remove `@key` and skip `LabelSection.Purge`) against `Back_from_one_Spaces_settings_to_anothers_leaves_no_Label_of_the_departed_Space_while_held`. Rebuild `Yello.Client` in Debug before each browser plant.

**What this run measured:**

- Scroll is at offset 0 after a switch and after Back today, so Q4 defaults to (b).
- A plant that skips `Purge()` in `Navigating` leaves only the departed Space's name in the bar. The keyed children hold the data. This is the Q3 record for `:542`.
- The AC11 browser case is not scaffolded. The gap between `Say` and `Take` is one `InvokeAsync` turn, which a browser cannot interleave deterministically.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (`claude-opus-5-5`), dev-story run unattended by the story-cycle orchestrator, 2026-10-31.

### Debug Log References

**Activation (red, then green).** All six red-phase scaffolds were un-skipped and run before any product change:

| Scaffold | Red, for the stated reason | Green after |
|---|---|---|
| `The_route_back_skips_a_listed_Space_that_refused_with_409` | timeout at `WaitForPathAsync(/spaces/{Harbour})` - the route back returned to Northwind | Q1 (a) in `SpaceDirectory`, `SpaceArrival`, `Home` |
| `When_every_listed_Space_refuses_the_route_back_stops_on_the_refusal_page` | the same loop | same |
| `The_route_back_exclusion_lives_in_the_directory_and_is_not_a_remembered_Space` | "SpaceDirectory holds no set of the Spaces that refused this tab" | same; regex not widened |
| `A_pending_narration_is_forgotten_on_a_Space_change_and_kept_within_one_Space` | `Assert.NotNull` - no `Forget()` | `SpaceNarration.Forget` |
| `The_composition_root_registers_the_pending_narration_with_the_Space_purge` | `Program` not among `RegisterPurge`'s callers | `activeSpace.RegisterPurge(spaceNarration.Forget)` in `Program` |
| `Every_api_answer_is_no_store_on_the_switcher_a_Space_and_the_boundary` | confirmed by plant (below): the assertion at line 76 fails with the middleware neutered | `ApiNoStore` middleware |

**Scaffolds corrected:**

- `ActiveSpaceStateTests.A_pending_narration_...`: found `Forget` by reflection so it compiled before the method existed. Retargeted to a direct `narration.Forget` method group, as its own remark asked, and the now-unused `using System.Reflection` removed.
- `SpaceSwitcherGateTests.The_composition_root_registers_...`: the string `"Forget"` became `nameof(SpaceNarration.Forget)`.
- `SpaceSwitchCarryOverTests.Back_from_one_Spaces_settings_...` (E2E-015): its remark said removing the `@key` and skipping `LabelSection.Purge` together "must turn this red". Measured, it stays green, so the remark now records the measured layers (Q3 below). The assertions are unchanged.
- `SpaceSwitchCarryOverTests` class remark: updated from "red-phase cases are skipped" to what now exists.

**A real gap the survey found, written red first.** `SpaceHome` is one page reused across Spaces and took the narration in `OnInitialized` only. A sentence said in A's home ("You're now a Member...") stayed on screen in B's home after a switch through the switcher. New case `A_narration_said_in_one_Spaces_home_is_not_carried_into_the_next` failed at its final `Assert.Equal(0, ...)` with `SpaceNarration` already registered, then passed once `SpaceHome` dropped the sentence on a change of `SpaceId`. This is the reachable browser path for AC11. The ATDD run's switch between `Say` and `Take` is a one-turn race, and is held by the unit case and the IL gate instead.

**Plants** (each applied by `plant.py` in the scratchpad, which held every original in memory and restored it in a `finally`, with SHA-256 verified. The Debug client or test project was rebuilt before each run.):

| Plant | Target | Result |
|---|---|---|
| `Home` back to `spaces[0]` | E2E-012, ARCH-004 | both **red** (timeout at line 634; "Not found: Refused") |
| Exclusion set made a destination (`var lastSpace = spaces.LastOrDefault(refused)`) | E2E-012, `No_client_code_remembers_a_last_Space` | both **red** |
| `SpaceNarration` registration removed from `Program` | ARCH-003, UNIT-001 | ARCH-003 **red**; UNIT-001 green (it wires its own registration, by design) |
| `SpaceHome`'s narration reset absent (the state before the fix) | new AC11 browser case | **red** (above) |
| `no-store` neutered in `ApiNoStore` | SLC-001 | **red** at `ActiveSpaceEndpointTests.CacheControl.cs:76` |
| `LabelSection.Purge` skipped alone | E2E-015 | green: defence in depth |
| `LabelSection` `@key` removed + `Purge` skipped | E2E-015 | green: `SpaceArrival` unmounts its child while the destination is held |
| Seam `Purge()` skipped in `Navigating` + `SpaceArrival`'s `_arrived` reset removed + `@key` removed + `LabelSection.Purge` skipped | E2E-015 | **red**, but at the anchor before Back (the reused section kept Northwind's Labels under Harbour's settings), not at the held-absence assertion. Corrected by the code review: no plant has seen that assertion red |
| Seam `Purge()` skipped in `Navigating` alone (ATDD run) | E2E-002, -006, -015 | **red** on the bar's Space name |
| `outline: none` on `.context-bar a/button:focus-visible` | E2E-017 x4 | all four **red** |
| `TaskListFilters.Clear()` removed + `TaskListView` `@key` removed (same build as the ring plant; disjoint cases) | E2E-004 | green: the arrival gate, as for Labels |
| `@foreach` over `Directory.Current` in `Home.razor` (ATDD run) | ARCH-001 | **red** |
| A shared `static` in a client service | E2E-011 | not run: unprovable from the browser, because each tab is its own WebAssembly runtime. Ledgered to 3.4 |
| Q4 scroll reset removed | E2E-009 | not applicable: Q4 landed as (b), and no reset exists |

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-10-30).

**Decisions** (each reversible; cut any line to reopen it):

- **Q1 - (a).** An in-memory, per-load exclusion set on `SpaceDirectory` (`Refused`, `ArrivedIn`, `RefusedInThisLoad`, cleared by `Forget`). `Home` lands on the first listed Space not in it, and shows the refusal page with the bar's shell when all have refused. Cost: one `HashSet<Guid>` in an Account-scoped service. Recorded: a listed Space's own refusal (`403`/`404`/`409`) only. The code review removed the transport failure and the `5xx`, which say nothing about the Space, so "Back to Yello" asks that Space again. Not recorded: an unlisted id (the route back never offers one) and a page's own `404` (a missing Project is not the Space refusing).
- **Q2 - (a).** `Cache-Control: no-store` on every `/api` response, set once by the `ApiNoStore` middleware in `Yello.Host/Program.cs` through `Response.OnStarting`, so `DatabaseAvailability`'s `Response.Clear()` cannot drop it. A value already holding `no-store` (the cookie handler's `no-cache,no-store` on a dead-cookie `401`) is left alone. **The per-endpoint lines stay** (`IdempotentResponses.cs:182`, `ApiTokenEndpoints.cs:99`): they state their reason where the secret is written and agree with the pipeline. Cost: one header per answer.
- **Q3 - (a).** Each layer was planted separately; the record is in the Debug Log and in `deferred-work.md` (`:542` closure). The component purges and `@key`s stay as defence in depth behind the arrival gate.
- **Q4 - (b).** Measured: window and every `main` element at offset 0 after a switch and after Back between Boards, and after a switch between Lists (the List leg and the anchor on B's scroll were added by the code review) (`Scroll_is_at_the_origin_after_a_switch_and_after_Back`, green in this run and in the ATDD run). No reset added. The case stays, to catch a framework change.
- **Q5 - (a).** No isolation row. 3.2 maps no `/api` route; Q2 adds only a header.
- **Q6 - (a).** One `IBrowserContext`, two pages, one `FakeYelloApi` per page seeded identically (`Two_tabs_on_two_Spaces_share_no_state`).
- **AC1 - Token select.** Account settings' Token-binding `<select>` (story 1.8) stays an allowed exception to "the only list of Spaces". It chooses a binding, not a way to reach a Space. ARCH-001's allow-list and message name it beside 5.4's AD-24 exception. Cost: a second rendering of the Space names exists, on Account settings only.
- **SpaceHome narration.** It is dropped when the reused page's `SpaceId` changes. This is a fix beyond the story's survey, under AC11 / UX-DR38.

**AC to test map:**

| AC | Cases |
|---|---|
| AC1 | `The_switcher_lists_three_Spaces_at_mixed_Roles_by_name_and_nothing_else`; gate `Only_the_switcher_renders_the_Accounts_Spaces_as_a_list` (+ `The_Space_list_scan_detects_a_planted_list`) |
| AC2 / AC17 | `Switching_from_Owner_to_Viewer_drops_every_Owner_affordance_while_held_and_after`, `A_Viewer_destination_Board_has_no_Task_create_control_after_an_Owners_Board`, the Task edit controls on an Owner-to-Viewer switch held by `DeleteTests.A_Viewer_is_offered_no_delete_control_and_nothing_is_disabled` and the Viewer panel cases in `LabelTests` (code review), `A_List_filter_and_sort_do_not_survive_a_switch`, `An_open_Task_and_the_bars_Project_name_do_not_survive_a_switch`; NFR-2 on the wire stays `Each_Space_reports_the_Role_held_there_with_no_carry_over_across_switches` |
| AC3 / AC4 | `Back_and_Forward_between_two_Boards_leave_nothing_of_the_departed_Space_while_held`, `An_Account_switch_in_one_tab_leaves_nothing_of_the_first_Account`, the 1.7 sign-out cases, `ActiveSpaceStateTests.*` |
| AC5 | `A_session_through_two_Spaces_Boards_Lists_and_settings_leaves_nothing_in_browser_storage`, `ClientStorageBanTests` |
| AC6 / AC7 | `A_Project_id_followed_under_another_Space_renders_nothing_of_the_first_and_refuses_as_unlisted`, `SpaceScopedFirstLoadTests` |
| AC8 | `Scroll_is_at_the_origin_after_a_switch_and_after_Back`, `A_Space_route_keeps_the_fixed_title_and_manual_scroll_restoration` |
| AC9 | `Back_between_Spaces_focuses_the_bar_and_announces_the_destination_once` |
| AC10 | `Two_tabs_on_two_Spaces_share_no_state` |
| AC11 | `A_pending_narration_is_forgotten_on_a_Space_change_and_kept_within_one_Space`, `The_composition_root_registers_the_pending_narration_with_the_Space_purge`, `A_narration_said_in_one_Spaces_home_is_not_carried_into_the_next`; holder list in `ActiveSpaceState` remarks |
| AC12 | `The_route_back_skips_a_listed_Space_that_refused_with_409`, `When_every_listed_Space_refuses_the_route_back_stops_on_the_refusal_page`, `The_route_back_exclusion_lives_in_the_directory_and_is_not_a_remembered_Space`, `No_client_code_remembers_a_last_Space` |
| AC13 | `An_in_app_navigation_to_a_Space_removed_after_a_visit_is_refused_with_no_request` |
| AC14 | `Back_from_one_Spaces_settings_to_anothers_leaves_no_Label_of_the_departed_Space_while_held` + the Q3 plant record |
| AC15 | `A_keyboard_only_switch_lands_focus_on_the_bar_and_announces_the_destination`, `ArrowDown_on_the_open_trigger_moves_focus_to_the_first_row` (decision D2), `The_bars_Tab_order_follows_reading_order_with_a_visible_ring_at_every_stop` (x5, Admin added), `The_create_pages_Tab_order_is_name_Create_Cancel_with_a_visible_ring` (x2 widths), `The_rename_sections_Tab_order_is_Rename_field_Save_Cancel_with_a_visible_ring` (x2 widths) |
| AC16 | `An_Account_switch_in_one_tab_leaves_nothing_of_the_first_Account` |
| AC18 | `Every_api_answer_is_no_store_on_the_switcher_a_Space_and_the_boundary` |

**Ledger.** Closed `:358`, `:364`, `:542` and `:669`. Added two entries:

- UX-DR31's remaining purge holders (replica, projections, inbound frames, lease invalidation), owned by 7.3/7.7. No existing entry said so.
- The two-tab case cannot see a shared static, owned by 3.4.

**Verification.** See the Change Log entry for the build and full-run totals.

### File List

- `Yello.Client/Spaces/SpaceDirectory.cs` (modified: the per-load refusal exclusion set)
- `Yello.Client/Spaces/SpaceNarration.cs` (modified: `Forget`)
- `Yello.Client/Spaces/ActiveSpaceState.cs` (modified: remarks - the holder list, Epic 7's registrations)
- `Yello.Client/Components/SpaceArrival.razor` (modified: records refusals and arrivals with the directory)
- `Yello.Client/Pages/Home.razor` (modified: the route back skips refused Spaces)
- `Yello.Client/Pages/SpaceHome.razor` (modified: drops the narration on a change of Space)
- `Yello.Client/Program.cs` (modified: registers `SpaceNarration.Forget` with the Space purge)
- `Yello.Host/ApiNoStore.cs` (new)
- `Yello.Host/Program.cs` (modified: `app.Use(ApiNoStore.InvokeAsync)`)
- `tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs` (modified: two cases activated, one case added, remarks)
- `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.CarryOver.cs` (modified: two cases activated)
- `tests/Yello.Tests.Architecture/ActiveSpaceStateTests.cs` (modified: one case activated and retargeted)
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CacheControl.cs` (modified: activated)
- `tests/TESTING-CONVENTIONS.md` (modified: "Two tabs and an Account switch - added by story 3.2")
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)
- `_bmad-output/implementation-artifacts/3-2-switch-between-spaces-with-nothing-carrying-over.md` (this file)
- Code review: `Yello.Client/Components/ContextBar.razor` and `SpaceSwitcher.razor` (ArrowDown into the rows, decision D2), `SpaceArrival.razor` (records only a 403/404/409, behind a token check), `SpaceDirectory.cs`, `Home.razor`, `ActiveSpaceState.cs` (remarks), `Yello.Host/ApiNoStore.cs` (remarks), `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.CarryOver.cs`, `tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs`, `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.CacheControl.cs`

### Change Log

- 2026-10-31 - dev-story: AC11 (`SpaceNarration` purged on a Space change; `SpaceHome` drops a taken sentence on a change of Space), AC12 (route-back exclusion set), AC18 (`no-store` on every `/api` answer). Six scaffolds activated, one browser case added, plants recorded, four ledger entries closed, two added. Verification: `dotnet build Yello.slnx` 0 warnings, 0 errors; `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`: 1,615 total, 1,605 passed, 1 failed, 9 skipped. The one failure was `ISO-TIMING-IN-SPACE / Session` (boundary vs in-Space not-found medians 62.1 vs 46.6 ms, 24.9 % against 20 %), a timing measurement under full-run load. The `/api` header applies to both paths alike. Rerun alone per CLAUDE.md, `Yello.Tests.Isolation` passed 141/141. Net: 1,606 passed, 0 failed, 9 skipped, against a baseline of 1,614 / 1,599 / 15 skipped (+1 new case; 6 red-phase skips activated). Status set to review.
- 2026-10-31 - code review: 35 raw findings, 24 after dedup (2 decisions by Lee, 14 patches applied, 2 deferred to 3.3 and 3.4, 6 dismissed). The main fix: AC12 recorded a transport failure or a 5xx as the Space refusing, and recorded a refusal after an await with no token check. Both fixed; only a 403/404/409 is recorded now. D1: the Token-binding select stays as a named AC1 exception. D2: ArrowDown on the open trigger focuses the first row. New source-level gates were planted red (narration registered on the Space seam). Verification: `dotnet build Yello.slnx` 0 warnings, 0 errors. The full run gave 1,620 total, 1,610 passed, 1 failed, 9 skipped. The failure was the new List leg of `Scroll_is_at_the_origin_after_a_switch_and_after_Back`, which waited on one title being among the first rows. After the fix (wait on the bar and the first row), the class passed 3 of 3 times and `Yello.Tests.Client` passed alone with 374 total, 372 passed, 0 failed, 2 skipped. Net: 1,620 total, 1,611 passed, 0 failed, 9 skipped. Status set to done.
