---
baseline_commit: 11d0dad
---
# Story 4.7: A Viewer's Space, and a Role that drops beneath you

Status: review

Epic: 4 — Bring people into one piece of your work (the seventh and last story of the epic)
Story key: `4-7-a-viewers-space-and-a-role-that-drops-beneath-you`

**Requirements owned.** From the traceability row (`epics.md:375`): **FR-16**, **UX-DR27**, **UX-DR29**, **UX-DR38**. Also served: UX-DR28 (the context bar's accessible name, `epics.md:272`), UX-DR39 (focus destination for a Role drop, `epics.md:283`), test-design **X-9** ("capabilities a Role lacks are absent", 4 cases) and **X-7** (permission-change notices through a live region) (`test-design-qa.md:367`, `:357`). FR-16 is mapped to 1.6 and 4.7 (`implementation-readiness-report-2026-08-22.md:495`); 1.6 proved the matrix against **seeded** Memberships and 4.7 re-proves it against Memberships **created through the product** (`:962-969`, and `SpaceBoundaryPipelineTests.cs:39-40` says so).

- **FR-16** (`prd.md:332-359`; `role-capability-matrix.md:9-34`; CAP-16). Every consequence binds:
  - The matrix is the single source of truth (16 spec rows; 15 `Capability` members, the Ownership-Offer accept row being decided by row identity, AD-26).
  - "A Viewer's write attempt is refused at the API regardless of what the interface offered; the interface hiding an action is never the mechanism that enforces it."
  - "An API Token issued by a Viewer can read and cannot write, matching that Account's Role at the moment each request is evaluated."
  - Every capability is refused for a non-member without disclosing existence (already proven by `Every_capability_is_refused_with_the_boundary_404_for_a_non_member`; keep green).
- **UX-DR27** (`EXPERIENCE.md:85-87`): absent, not disabled, everywhere. "There is deliberately no disabled state in Yello for Role reasons; if one is being designed, the answer is removal."
- **UX-DR28** (`EXPERIENCE.md:83`): the context bar's accessible name states the Role and what it permits. **Already built** (`ContextBar.razor:58`, `RoleCopy.Compose`, `ClientCopy.resx:211-227`); 4.7 makes it update on a Role change without a Space switch.
- **UX-DR29** (`EXPERIENCE.md:89`, `:244`): a Role drop narrates the removal before the surface settles, on every surface, not only Space settings.
- **UX-DR38** (`EXPERIENCE.md:383-389`): permission changes are `assertive`, never throttled or coalesced, delivered only to the client context whose active Space matches, and carry no Space name.
- **UX-DR39** (`EXPERIENCE.md:400`): "Role drops below a surface's requirement → the narration, then Space home."
- **AD-1** (`ARCHITECTURE-SPINE.md:71-75`): the Role is read from the Membership row per request; never from a cookie, claim, Token payload or cache.
- **AD-9** (`ARCHITECTURE-SPINE.md:133-137`): permission change is **pushed to the sync layer, never polled**. The publish seam exists (4.4); **no subscriber and no push channel exist until 7.3/7.7**. See C1 and Q1 — this is the dominant finding of this story.

**Ledger entries owned (`deferred-work.md`).** Close each one, or re-own it with a reason. Found by grepping for `4.7` and `4-7` on 2026-11-08. Both come from 4.5's code review.

| Line | Entry | Severity | Covered by |
|---|---|---|---|
| `:823` | A `403` on the removal confirm's fresh read shows the Admin-limit copy from the stale viewer Role (`MembershipSection.UseConfirmReadRefusalAsync` → `ForbiddenAsync`). The sentence before the Role re-read's exit should be the generic refusal | low | AC9 |
| `:829` | A leave in flight is lost when the tab switches Space (`LeaveSpaceSection.LeaveAsync`, `OnParametersSet`). A committed leave is neither narrated nor purged until the next visit | low | AC10; Q8 |

**Epic-close sweep of unowned entries** (the orchestrator asked for every entry owned by "4.5", "4.6" or "epic 4" that no later story owns, so the epic does not close with them unowned). Listed in Dev Notes, "Ledger dispositions", with a recommended disposition for each (Q9).

**Depends on** (all done):

- 1.6: the pipeline's capability step, `RoleCapabilityMatrix`, `RequiredCapability`, `AccessRefusal` recording, `SpaceBoundaryPipelineTests` (seeded matrix).
- 1.7, 3.2: `ActiveSpaceState`, `SpaceArrival`, the context bar, `RoleChip`, `RoleCopy`, the Space purge.
- 3.1: Space settings, `SpaceCapabilities.PermitsSpaceSettings`, the narrated exit (`SpaceSettings.Permitted()`), `SpaceNarration`.
- 4.3: accepted Invitations (the product path for an Admin, Member or Viewer).
- 4.4: the Role change route (the product path for a demotion), `IMembershipChanges`, `SpaceArrivalContext.Reread`, `MembershipSection.OnRoleDoubted`, the Space-home narration region rendered empty then filled.
- 4.5: removal and leave, `LeaveSpaceSection`, `SpaceExit`, `DepartureNarration`.
- 4.6: `AssignTask`, `TaskCapabilities.PermitsAssignTask`, the Viewer's read-only Assignee row.

**Explicitly not in this story:**

- **A push channel** (SignalR, WebSocket, SSE) and any `IMembershipChangeSubscriber`. That is 7.3/7.7 (AD-8, AD-9). 4.7 builds the client's **single Role-change entry point** that 7.7's push will call. See Q1.
- **Polling or a timer** of any kind (AD-9 "never polled"; AD-10; 7.8 fails the suite on a poller).
- **The editor half of a Role drop**: the description editor made `readonly`, retained text, "Access ended." banner (FR-34; 7.4/7.7). No description editor exists yet.
- **Removal while the tab is open** being narrated as "Access ended." live. Removal is still learned on the next request through the boundary refusal surface (4.5). 7.7 owns the live half.
- **New capabilities** (Status configuration, Ownership Offer, description edit): no endpoint exists for those rows yet (C4). Their stories exercise their own rows.
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits.
- **No new NuGet package and no new project without Lee** (CLAUDE.md).

## Story

As someone whose standing differs between Spaces, or changes while I am working,
I want the interface to tell me what I may do without my having to try,
so that I can read my standing rather than discover it on failure.

## Acceptance Criteria

AC1 to AC7 are reproduced **verbatim** from `epics.md:1885-1916` and numbered by position. AC8 to AC12 come from the PRD, the UX and architecture spines, the ledger and a survey of the code at `11d0dad`, and each cites its source. The indented notes are implementation guidance; a note that depends on an open question names it. **[LEE]** marks a question that touches authority, isolation, a recorded Lee decision or a security seam. The orchestrator may route those to Lee before development. Otherwise the dev phase takes the recommendation and records it as a reversible decision.

**AC1 — absent, not disabled, everywhere** (UX-DR27; X-9)
**Given** a Viewer in a Space
**When** every surface renders
**Then** no create, edit, move, delete or invite affordance is present anywhere — absent, not greyed, not tooltipped
**And** there is no disabled state anywhere in the product for a Role reason; where one seems needed, the answer is removal

> - **What exists.** Every Role-gated control is already removed rather than disabled (survey: `TaskCapabilities`, `ProjectCapabilities`, `LabelCapabilities`, `InvitationCapabilities`, `MembershipCapabilities`, `SpaceCapabilities`; BoardView `:256-267`, `TaskDetail :85-291`, `ProjectList :72-117`, `TaskCard :49-75`, `BoardView.Drag.cs:89`, `BoardView.Keys.cs:307`, `ContextBar :84`). Twenty-odd browser cases each prove one surface (`BoardTests:148`, `DeleteTests:620`, `DragTests:549`, `TouchDragTests:345`, `MoveTests:620`, `KeyboardTests:539`, `TaskDetailTests:579`, `AssigneeTests:189`, `LabelTests:513`, `ListViewTests:329`, `SpaceHomeProjectsTests:138`, `InvitationTests:181`, `MembershipSectionTests:483`, `SpaceCreateAndRenameTests:353`, `SpaceSwitchCarryOverTests:187`, `:236`). **Do not duplicate them.**
> - **What 4.7 adds: one whole-product sweep** (`ViewerSpaceTests`, a new class on the existing `ClientSurfaceCollection`): a Viewer who joined at Viewer (the fake's `Roles[space] = "viewer"`) visits, in one tab, Space home, the Board (with a Task panel open), the List View, the Space settings URL typed directly, and Account settings. On each:
>   - no element matching a single **affordance selector list** kept in one place (create/rename/delete/move/drag-handle/invite/assign/label-manage/settings-link/Change Role/Remove); the list is the union of the selectors the per-surface cases already use, so a new affordance added later must join it;
>   - no `[disabled]` and no `[aria-disabled]` on any element **except** the in-flight `PrimaryButton`/`FormField` states, which are not reachable without acting (assert none is present at rest);
>   - **anchor:** the same sweep as an Owner finds every affordance class at least once (else the selector list is stale);
>   - "Leave this Space" **is** present (a Viewer may leave, 4.5 Q8) and "Issue a Token" in Account settings **is** present (the matrix's last row).
> - **The Space settings URL for a Member or Viewer** (`SpaceSettings.razor:52-55`) today shows "Only Owners and Admins can manage Labels." (the `manage-labels` capability copy) — object-shaped for the whole page (Q6 [UX]). Recommended (a): route to Space home and narrate "Space settings is for Owners and Admins." through the same narration element as AC5. No affordance renders on the way.
> - **The one non-busy disabled state** (`AccountSettings.razor:118`, `_spaces.Count == 0`) is a **data** reason, not a Role reason. Leave it; record it in Completion Notes; the sweep's exception names it.
> - **Gate (Architecture):** extend the existing disabled-attribute gates (`RefusalBoundaryGateTests.Memberships.cs`, `.RemoveMembership.cs`, `SpaceSwitcherGateTests.cs`) to the whole client: no Razor file binds `disabled`, `aria-disabled` or an `is-disabled`/`--disabled` class to an expression naming a `*Capabilities.Permits*` predicate or `.Role`. **Plant:** `disabled="@(!_canManage)"` on the Board's add button. The gate goes red, and the browser sweep goes red.

**AC2 — the API refuses a Viewer's write regardless** (FR-16; I-10)
**Given** a Viewer
**When** any write is attempted over the API
**Then** it is refused regardless of what the interface offered
**And** absence is therefore an honesty contract with the User rather than a security control

> - **Every Space-scoped write route** has a Viewer row on **both surfaces** (Session and Token), with a Viewer that **joined through an accepted Invitation** (AC3's rule), answering `403 insufficient-role` with the route's `capability` code, recorded `InsufficientRole`, and no state change (anchor: the same request by a Member or Admin succeeds, per the matrix).
> - **Gaps found** (server survey): no Isolation Viewer row in the Viewer's **own** Space for `PATCH project` (rename), `POST label`, `DELETE label`, `PATCH space` (rename), `DELETE space`. Add `ISO-PROJECT-RENAME-VIEWER`, `ISO-LABEL-CREATE-VIEWER`, `ISO-LABEL-DELETE-VIEWER`, `ISO-SPACE-RENAME-VIEWER`, `ISO-SPACE-DELETE-VIEWER` to `IsolationCases` (template `ISO-TASK-EDIT-VIEWER`, `IsolationCases.cs:526`), seeded through `JoinedThroughTheProductAsync` (`IsolationCases.Memberships.cs:359-375`).
> - **The move route is Session-only**: a Token gets `401` (no credential), not `403`. That is the existing, deliberate answer (`TaskMoveEndpoints.cs:35-37`, C-5). Record it; the Session row is the Viewer proof.
> - **Allowed writes for a Viewer:** the leave (`DELETE .../membership`, under `ReadProjectsAndTasks` by identity, Lee's 4.5 Q1) and Token issue (`IssueApiTokenForOneself`). Each gets a positive Viewer row (Session; Token issue is Session-only).
> - **A Token after a product demotion narrows** (FR-16 bullet 3; `EXPERIENCE.md:536`). Today `A_Token_acts_with_the_current_Role_read_on_each_request` (`ActiveSpaceEndpointTests.Tokens.cs:318-359`) and `REV-ROLE-CHANGED` (`RevocationCases.cs:94-96`) demote **through raw SQL** and assert only a `GET` reporting `viewer`; **no write after the demotion is asserted**. Add `REV-DEMOTED-TO-VIEWER-BY-OWNER`: a Member joins through the product, issues a Token, writes successfully (`201` Project create, both surfaces), the Owner demotes it through `PATCH .../memberships/{id}`, and the **very next** write on each surface is `403 manage-projects`. No reissue.
> - **Plant:** drop `RequiresCapability(ManageProjects)` to `ReadProjectsAndTasks` on the rename. `ISO-PROJECT-RENAME-VIEWER` goes red; `Every_spaceId_endpoint_declares_exactly_one_capability` stays green (record that the isolation row, not the gate, catches it).

**AC3 — the matrix through the product's own paths** (FR-16; `implementation-readiness-report-2026-08-22.md:962-969`)
**Given** each row of the Role capability matrix
**When** it is exercised against Memberships at Owner, Admin, Member and Viewer created through the product's own paths rather than seeded
**Then** the matrix governs the outcome in every case

> - **Product paths only** (`TESTING-CONVENTIONS.md:556-559`): the Owner registers and creates the Space (`POST /spaces`); the Admin, Member and Viewer each register and accept an Invitation at that Role (4.3); a second Viewer is **produced by demotion** (a Member demoted to Viewer through the Owner's `PATCH .../memberships/{id}`), so both arrival routes are exercised. S-2 after the setup.
> - **Where:** a new Slices partial `ActiveSpaceEndpointTests.RoleMatrix.cs` (no new container-backed class). One `[Theory]` over `(capability row, Role, surface)`:
>   - one **representative route per capability row that has an endpoint** (table below); the expected outcome is `RoleCapabilityMatrix.Decide(capability, role)` read **from the Domain**, not a hand-copied table, so the matrix itself is what governs;
>   - `Yes` → the route's success status; `No` → `403 insufficient-role` with `capability = StableCode.Of(cap)`, one `InsufficientRole` record;
>   - `Qualified` (Admin on `ChangeMembershipRole`, `RemoveMembership`) → two cases each: inside the target rule (Admin on a Member: success) and outside it (Admin on another Admin: `403`), matching `RoleChangeAuthorityOf` / `RemovalAuthorityOf`;
>   - the Token surface for every route a Token reaches; Session-only routes (`move`, `api-tokens`) are Session rows and record the Token's `401`.
> - **Representative routes** (one per row; each must leave the world reusable or use a fresh target):
>
>   | Capability | Route | Success |
>   |---|---|---|
>   | ReadProjectsAndTasks | `GET .../projects` | 200 |
>   | ManageProjects | `POST .../projects` | 201 |
>   | ManageTasks | `POST .../tasks` | 201 |
>   | AssignTask | `PUT .../tasks/{id}/assignee` | 200 |
>   | ManageLabels | `POST .../labels` | 201 |
>   | IssueAndRevokeInvitations | `POST .../invitations` (fresh address) | 201 |
>   | ChangeMembershipRole | `PATCH .../memberships/{id}` | 200 |
>   | RemoveMembership | `DELETE .../memberships/{id}` (a throwaway joined Member) | 204 |
>   | RenameSpace | `PATCH /spaces/{id}` (same name) | 200 |
>   | DeleteSpace | `DELETE /spaces/{id}` on a **separate** Space with the same four Roles; Owner last | 204 |
>   | IssueApiTokenForOneself | `POST .../api-tokens` (Session) | 201 |
>
> - **Rows with no endpoint** (`EditTaskDescriptionCollaboratively`, `ConfigureSpaceDefaultStatuses`, `ConfigureProjectStatusDelta`, `OfferOwnershipTransfer`; and the accept row, not a `Capability`). They cannot be exercised (C4). **Coverage gate** (Architecture): every `Capability` that some endpoint declares through `RequiresCapability` appears in the theory's route table, and every `Capability` absent from the table is declared by **no** endpoint. A later story that adds the first endpoint for one of those rows fails the gate until it adds its row here. **Plant:** remove the `ManageLabels` row from the table. The gate goes red.
> - **The demotion governs the next request** (FR-13 request path, already proven for Admin→Member by `REV-ROLE-CHANGED-BY-OWNER`): add the Viewer-by-demotion actor to the theory, so every `No` cell for Viewer is also proven for a demoted Viewer.
> - **The shared helper** (Q7): the "join through the product" helper is copied three times (`ActiveSpaceEndpointTests.ListMemberships.cs:360-388`, `IsolationCases.Memberships.cs:359-375`, `RevocationCases.cs:281-302`). Recommended (a): move one copy to `tests/Yello.Tests.Shared` (`ProductMembership.JoinAsync(host, ownerSession, role)`) and make the three call it.
> - `SpaceBoundaryPipelineTests` (1.6, seeded, 5×4) stays as the fast pipeline-level proof. Its remarks at `:39-40` say "story 4.7 re-exercises the matrix through real Invitation and Role-change paths": rewrite them to name the new case, without a story number.

**AC4 — a Role drop is narrated before the surface settles** (UX-DR29; X-7; Q1 [LEE], Q2, Q3)
**Given** an Account whose Role drops
**When** the change is pushed to it
**Then** the removal is **narrated before the surface settles**, rather than affordances silently disappearing
**And** the reason is stated: by the absence rule's own design, no residual control is left to explain the disappearance, so silence is hostile

> - **"Pushed" has no channel yet** (C1). Today the client learns its Role only on arrival and on a `SpaceArrival` re-read (same-Space navigation, `MembershipSection`'s `403`). On the Board, Task panel, Space home and List View a drop is **silent**: controls vanish on the next re-arrival (`TaskDetailTests:613`, `LabelTests:720`) and a `403` there shows capability copy with no Role re-read (`BoardView.razor.cs:873-878`).
> - **Recommended (Q1 (a)) — the Role rides on every answer the caller is entitled to.** The pipeline adds a response header `Yello-Role: <owner|admin|member|viewer>` (a new `Yello.Contracts` constant, values from the existing Role codes) to every Space-scoped response that **resolved a Membership**: every `2xx` and the `403 insufficient-role`. **Never** on any `404` (boundary or in-Space), `401`, `400`, `409`, `422` or `5xx`, so the header can never tell a boundary `404` from an in-Space one (AC12). The value is the Role **this request was judged under**, read from `ActiveSpaceContext` by the pipeline (the only reader of the Role, AD-1; no slice touches it).
>   - **Idempotent replay:** a replayed response carries the **live** Role of the replaying request, never a stored one (the header is added after the replay step, not stored in `IdempotencyRecord`). Prove it: a write by a Member, the Member demoted, the same key replayed → the stored `201` body with `Yello-Role: viewer`.
>   - **Client:** `YelloApiHandler` (`Yello.Client/Sessions/YelloApiHandler.cs`) reads `Yello-Role` on each response whose request URL is under `/api/v1/spaces/{id}` and hands `(spaceId, role)` to `ActiveSpaceState.Reconcile` (below). No timer, no extra request: the client learns on its **next request of any kind**, a read included, which a Board page issues on every column page, panel open and List page.
>   - **7.7's push** will call the same `Reconcile` when it lands. State that in the method's remarks (without a story number).
> - **One entry point:** `ActiveSpaceState.Reconcile(Guid spaceId, string role)`:
>   1. Drops the call if `spaceId != Current?.Id` or the arrival token is cancelled (the per-tab filter, AC7).
>   2. No-op if the Role is unchanged.
>   3. A **drop** (Q2: the Roles' capability sets are nested, Owner ⊃ Admin ⊃ Member ⊃ Viewer; an Architecture gate asserts the nesting against `RoleCapabilityMatrix` so "drop" can be a rank compare) → raises `RoleDropping(from, to)` **first**, then, after the narration has rendered, applies the new Role to `Current` and raises `Changed`, which removes the affordances.
>   4. A **rise** → applies at once and announces politely "You're now an Admin." in the context bar's existing polite region (Q3).
>   - `SpaceArrival`'s re-arrival path (`:236-259`) and `Renamed` (`:223-236`, whose answer also carries a Role) route through `Reconcile` instead of replacing `Current` directly, so every way the client learns a Role is narrated the same way.
> - **The narration element** (Q4): one `RoleNarration` component in the authenticated layout, directly under the context bar, rendered **empty** on every render and filled after the first render (4.4's Space-home pattern), `role="alert"` (assertive, UX-DR38), `tabindex="-1"`. Never throttled, never coalesced. Cleared by the Space purge and on the next navigation.
>   - **Copy** (Q5): "You're now a {Role}. {What it permits}." from the existing `RoleName{X}` and `RolePermits{X}` strings, e.g. "You're now a Viewer. Read only in this Space." No Space name, no address.
>   - **Order:** narration rendered → then affordances removed. **Focus:** if the element holding focus is about to be removed (a create field, a picker, a Move control, a card's action menu, a Space-settings control), focus moves to the narration **before** the removal; otherwise focus stays where it is (assertive announces without moving focus, `EXPERIENCE.md:394`).
>   - Open transient UI closes on the drop as it does today (`BoardView :256-267` closes `_creating` and returns a held card; `TaskDetail :490-502`; `ProjectList :213-223`). Typed-but-unsent text in a create or rename field is discarded with the control (Q3 sub-question; recorded, and the retained-text rule is 7.7's for the editor).
> - **Browser proof** (`RoleDropTests`, new class, existing collection; the fake gains a `RoleHeader` that follows `Roles[space]` on every Space-scoped answer, so changing `api.Roles[space]` mid-test is seen on the next request):
>   - Member on the Board → fake Role becomes `viewer` → the next column page or panel read → the narration "You're now a Viewer. Read only in this Space." appears, and **a `MutationObserver` installed before the change records the narration text inserted before the first affordance removal** (the deciding assertion for "before the surface settles");
>   - focus on the Task panel's title field at the drop → `document.activeElement` is the narration after the drop, never `body`;
>   - focus on a card (not removed) → focus stays on the card;
>   - Admin on Space home → `member`: the context bar's Space-settings link is removed after the narration;
>   - a rise (`viewer` → `member`) → no alert, a polite "You're now a Member." and the controls appear;
>   - no request is issued by the client in the interval between two user actions (no polling): count requests over an idle second.
> - **Plant:** `Reconcile` applies the Role before raising `RoleDropping`. The MutationObserver case goes red.
> - **Plant:** the header added on the boundary `404`. AC12's header case goes red.

**AC5 — the demoted Admin in Space settings** (UX-DR29, UX-DR39; `EXPERIENCE.md:244`, `:400`)
**Given** an Admin sitting in Space settings
**When** they are demoted to Member
**Then** they are told — "You're now a Member. Space settings is no longer available." — focus moves to the narration, and they are then routed to Space home
**And** the surface is never blanked silently and never left half-rendered

> - **What exists:** `SpaceSettings.Permitted()` (`:123-145`) narrates `SpaceSettingsNoLongerAvailable` ("You're now a {0}. Space settings is no longer available.", resx `:710-712`) into `SpaceNarration` and routes; `SpaceHome` shows it in a **polite** region (`:35-38`), and focus goes to the "Projects" heading (`SpaceArrival.ArriveAsync :255-258`), **not** to the narration. During the last settings render the `manage-labels` refusal paragraph (`:52-55`) can flash: a half-rendered page.
> - **Change:**
>   - the exit is driven by `RoleDropping` (AC4) when the new Role fails `PermitsSpaceSettings`: the sentence goes to `RoleNarration` with this surface's copy, **focus moves to it**, and only then `SpaceNavigation.GoTo(SpaceHome)`. On Space home the narration persists (it is in the layout, not the page) and Space home does **not** take focus to its heading when a narration is showing;
>   - the settings sections are **not re-rendered under the lower Role** before the route (render nothing new; keep the last permitted render until the route), so neither a blank page nor the refusal paragraph appears;
>   - `SpaceNarration`'s hand-over and Space home's polite narration region become the `RoleNarration` element; keep `DepartureNarration` (Space deleted, left) on its own path (it is not a Role change).
> - **Cases:** rewrite `LabelTests.A_Role_reread_to_Member_on_Space_settings_is_narrated_then_routed_to_Space_home` (`:694`) and `MembershipSectionTests.An_Admin_demoted_meanwhile_is_narrated_out_of_Space_settings_on_the_403` (`:374`) to assert, in order: the narration text exactly, `document.activeElement` is the narration, the URL becomes Space home, and **at no sampled frame** is the settings `main` empty or showing `.space-settings__refusal` (MutationObserver log). Add the header-driven variant: the Admin's next settings read answers `200` with `Yello-Role: member`.
> - `MembershipSectionTests.The_Space_home_narration_region_is_inserted_empty_and_filled_after_the_first_render` (`:618`) moves to the `RoleNarration` element.
> - **Plant:** route before narrating. The order case goes red. **Plant:** focus left on the heading. The focus case goes red.

**AC6 — the Role chip and the accessible name follow the change** (UX-DR28; X-8)
**Given** the context bar
**When** a Role changes
**Then** the Role chip and the accessible name both update to the new Role and what it permits

> - `ContextBar` already recomposes `AccessibleName` and the chip on `ActiveSpace.Changed` (`:79-87`, `:128-130`, `:177-201`). With `Reconcile` raising `Changed` after the narration, both follow.
> - **Cases** (`RoleDropTests`): after a header-driven drop Member → Viewer, the chip reads "Viewer" and the `nav` `aria-label` is "{Space} — Viewer, read only in this Space"; after a rise, likewise for the new Role. The chip stays non-interactive (`SpaceSwitchingTests:322` keeps green).
> - The context bar's polite **switch** announcement stays switch-only (`LastArrivalWasSwitch`); a drop is announced once, by the narration, never twice.

**AC7 — the announcement reaches only the matching tab** (UX-DR38; `EXPERIENCE.md:385`)
**Given** a permission-change announcement
**When** it is delivered
**Then** it reaches only the client context whose active Space matches the change — a per-tab filter, never a shared broadcast
**And** the copy carries no Space name in any case, because a broadcast would otherwise need to disambiguate which Space ended and would speak that name in a tab showing a different Space

> - **Per tab by construction:** services are scoped per tab in WebAssembly (`ActiveSpaceState.cs:58-62`); each tab learns its Role from its own answers. `Reconcile` drops any `(spaceId, role)` whose Space is not the tab's active Space, and any answer that arrives after the arrival token was cancelled (a request for the departed Space landing after a switch).
> - **No shared channel:** extend `ClientStorageBanTests` (or a sibling gate) so the client references no `BroadcastChannel`, `storage` event, `SharedWorker` or `localStorage` for Role or narration. **Plant:** a `BroadcastChannel` post in `Reconcile`. The gate goes red.
> - **Copy has no Space name:** the narration strings take the Role name and permits only. A gate asserts the `RoleNarration*` and `SpaceSettingsNoLongerAvailable` resx values contain no `{` placeholder beyond the Role (one placeholder, the Role name), and a browser case asserts the narration text does not contain the Space name.
> - **Browser cases:**
>   - two pages (two tabs, one browser context, one fake): tab 1 on Space A, tab 2 on Space B, the same Account. The Role in A drops; tab 1 narrates on its next request; tab 2, which also requests, **never** shows the narration, and its chip is unchanged;
>   - one tab: a slow request to Space A is held, the tab switches to Space B, the held answer is released with `Yello-Role: viewer` → no narration in B, B's chip unchanged (the late-answer filter). **Plant:** remove the Space check in `Reconcile`. This case goes red.

**AC8 — the header discloses nothing** (AD-3, FR-15, I-1; Q1 [LEE])

> - The header appears only where the caller already holds a Membership and the request was judged under it. The caller already reads its own Role (`GET /spaces/{id}`), so it adds no knowledge.
> - **Isolation:** for every existing `SameAsAnchor` / boundary row, the anchor and the probe also carry an **identical header set** (`Yello-Role` absent on both). Extend the row runner's comparison to response header names, excluding transport headers (`Date`, `Content-Length`, `Server-Timing` if present); record the excluded list. **Plant:** the header on the in-Space `404`. `ISO-ASSIGN-FOREIGN-TASK-IN-OWN-SPACE` (and every in-Space-404 row) goes red.
> - **Log:** the header value is a Role code; no log line gains it.
> - **Contract:** a new header is additive (AD-19). Update the contract snapshot if it covers headers, and the API docs comment block in `Program.cs`.

**AC9 — the stale-Role refusal copy** (ledger `:823`; and `:824` if Q9 (a))

> - `MembershipSection.ForbiddenAsync` (`:607-612`) chooses the Admin-limit copy from the **stale** `ViewerRole` before `OnRoleDoubted` re-reads (`:720-738`, `:592-594`, `:835-837`). With Q1 (a), the `403` carries `Yello-Role`: `Reconcile` runs **before** the section handles the body, so a demoted Admin's section is narrated out (AC5) and the row copy is never chosen. When the `403`'s Role is still Admin (a genuine Admin-limit refusal), show the Admin-limit copy.
> - Without a header (fake or older answer), re-read first (`await OnRoleDoubted`), then choose the copy from the **re-read** Role: the generic `RefusalText` if the Role changed, the Admin limit only if it is still Admin.
> - **Case:** an Admin demoted meanwhile presses Remove; the confirm read answers `403` with `Yello-Role: member` → the Admin-limit copy never appears in the DOM (MutationObserver), the AC5 exit runs. **Plant:** the old order. The case goes red.
> - `:824` (fold, Q9 (a)): in `UseConfirmReadRefusalAsync`, after awaiting the problem body, re-check `read == _presenceReads` and `_confirmingRemoval == membershipId` before closing a confirm; a late refusal for row A never closes row B's confirm. Case: two rows, A's read held and refused late after B's confirm opened → B's confirm stays open.

**AC10 — a leave survives a Space switch** (ledger `:829`; UX-DR38; Q8)

> - `LeaveSpaceSection.LeaveAsync` (`:147-183`) sends with `ActiveSpace.Token`, which the switch cancels; the answer is dropped, `Exit.Leave` never runs, the directory keeps the left Space, and the key is lost. `OnParametersSet` (`:91-105`) does not reset `_leaving`, and the component is unkeyed (`SpaceHome.razor:48`), so it is reused across Spaces.
> - **Recommended (Q8 (a)):** the leave is an Account-level outcome. Send it with a token tied to the Session (not the Space purge). When the answer is `204` (or the idempotent replay):
>   - if the tab is **still** on the left Space → the existing `SpaceExit.Leave` (purge, "You left the Space.", route home);
>   - if the tab has **switched** → remove the Space from `SpaceDirectory` silently (no narration in another Space, UX-DR38) and drop nothing else;
>   - a no-answer keeps the key for an identical resend, as today, only while the tab is on that Space.
> - `OnParametersSet` resets `_leaving` on a change of `SpaceId`; key the component by `SpaceId` in `SpaceHome`.
> - **Cases** (`LeaveSpaceTests`): hold the leave, switch to Space B, release `204` → the switcher no longer lists Space A, no "You left the Space." in B, B's surface intact; hold, switch, release unanswered → nothing changes in B. **Plant:** the old token. The switcher case goes red.

**AC11 — gates for the new seams** (AR-3, AD-1, AD-20, AD-21)

> - `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` and `No_slice_or_endpoint_reads_the_Role_off_a_Membership_row` stay green: the header is written by the pipeline step only. New gate: exactly one production type writes `Yello-Role` (the pipeline step).
> - New gate: the Role ranks used by `Reconcile` agree with the matrix's nesting (each lower Role's `Yes` set is a subset of the next higher Role's; `Qualified` counts as held).
> - `ClientStaticStateGateTests`, `ClientStorageBanTests`, the copy-literal gate (all strings in `ClientCopy.resx` with a `<comment>`), `SpaceNavigationGateTests` (every `NavigateTo` through `SpaceNavigation.GoTo`) stay green.
> - `SpaceBoundaryPipelineTests` and `ActiveSpaceUnitOfWorkTests` register any new pipeline dependency (the 4.4 lesson).
> - `No_production_code_runs_on_a_timer_or_as_a_hosted_service` stays green; add the client equivalent if none exists (no `PeriodicTimer`, `System.Threading.Timer`, `setInterval` in `Yello.Client`).

**AC12 — what a Viewer still reads, and the request path** (FR-15, FR-16, NFR-2)

> - A Viewer's reads are unchanged: every read route answers `200` to a product-joined Viewer on both surfaces (AC3's `ReadProjectsAndTasks` rows plus `GET board`, `GET list`, `GET task`, `GET labels`, `GET assignees`, `GET statuses`, each with `Yello-Role: viewer`).
> - The request path stays "no tolerance": a demotion governs the **very next** request (already `REV-ROLE-CHANGED-BY-OWNER`); AC2's new revocation row extends it to Viewer and a Token write.

## Tasks / Subtasks

- [x] **Task 0: decisions** (before any code)
  - [x] Q1, Q4 and Q9 are marked **[LEE]**. If the orchestrator routes them to Lee, record the answers here under "Decided by Lee" before ATDD. Otherwise take each recommendation and record it as "Qn - (x)" with its cost in Completion Notes.
    - **Decided by Lee (2026-11-08):**
      - Q1 - (a): the pipeline echoes the judged Role in a `Yello-Role` header on `2xx` and `403 insufficient-role` only, never on a `404`. The client reconciles through one entry point that 7.7's push will also call. "Pushed" is met on the request path until 7.7 (C1).
      - Q4 - (a): one assertive `RoleNarration` element under the context bar. Focus moves to it only when the focused control is removed or the surface is left.
      - Q9 - (a): fold `:823`, `:829`, `:824` and `:837`, and re-own the other unowned 4.5 entries to the sweep with a dated note. `:787` and `:814` (epic 4 retrospective) and `:528` (UX) keep their owners.
  - [x] Decide Q2, Q3, Q5, Q6, Q7 and Q8 the same way. Each recommendation is the default.
- [x] **Task 1: Contracts** (AC4, AC8)
  - [x] A `Yello.Contracts/Spaces/RoleHeader.cs` with `HeaderName => "Yello-Role"`, shaped like `Yello.Contracts/Idempotency.cs` (`HeaderName :26`, the one existing header contract), values from the existing Role code constants. Remarks: when it is present and why it is never on a `404`.
- [x] **Task 2: Host** (AC4, AC8, AC11)
  - [x] The pipeline step that has the resolved `ActiveSpaceContext` adds the header on `2xx` and on the `403 insufficient-role` (including a `QualifiedRefusal`), after the idempotency replay, never on `404`/`401`/`400`/`409`/`422`/`5xx`. No slice touches it.
  - [x] Update the `Program.cs` route comment block (one sentence: the header). Fix the stale comment at `Program.cs:549` if Q9 folds `:832`.
- [x] **Task 3: Client** (AC1, AC4-AC7, AC9, AC10)
  - [x] `YelloApiHandler`: read the header for Space-scoped requests and call `ActiveSpaceState.Reconcile`.
  - [x] `ActiveSpaceState.Reconcile` with `RoleDropping`; `SpaceArrival` re-arrival and `Renamed` route through it.
  - [x] `RoleNarration.razor` in the authenticated layout (empty first render, `role="alert"`, `tabindex="-1"`, purge-cleared); retire `SpaceNarration`'s Role use and Space home's polite narration region for Role changes (keep `DepartureNarration`).
  - [x] `SpaceSettings`: drive the exit from `RoleDropping`; no re-render under the lower Role; the Member/Viewer typed-URL case per Q6.
  - [x] Focus-before-removal in `BoardView`, `TaskDetail`, `ProjectList`, `TaskCard`, `SpaceSettings` (a shared "is the focused element inside a Role-gated region" check, e.g. a `data-role-gated` marker on each gated region).
  - [x] `MembershipSection.ForbiddenAsync` order (AC9); `UseConfirmReadRefusalAsync` re-check if Q9 folds `:824`.
  - [x] `LeaveSpaceSection` (AC10); key it by `SpaceId` in `SpaceHome`.
  - [x] Copy in `ClientCopy.resx` with `<comment>`s (Q5). CSS in `components.css` only (logical properties, no px or colour literals) for the narration element, following the Space-home narration's style.
- [x] **Task 4: tests and plants** (all ACs). See Testing requirements. Save each original in memory and restore it in a `finally`. **Never** `git checkout --`, `git restore` or `git reset`. Record `| Plant | Expected red | Seen |` in the Debug Log.
- [x] **Task 5: ledger, docs, verification**
  - [x] Apply the ledger dispositions below: one line per CLOSED, UPDATED or RE-OWNED note, no TAB or FORM FEED, and keep the final newline.
  - [x] Dated notes in `ARCHITECTURE-SPINE.md`: AD-1 (the pipeline echoes the Role it judged under, as `Yello-Role`, on `2xx` and `403` only; no client caches it beyond the tab's active-Space state, which every answer corrects); AD-9 (until the sync channel exists, a Role change reaches an open tab on its next request through that header; the push will call the same client entry point; still never polled).
  - [x] `TESTING-CONVENTIONS.md`: the shared product-join helper (Q7) and the "matrix through product paths" theory with its coverage gate, in the Memberships section.
  - [x] Rewrite comments that predict "4.7": `SpaceBoundaryPipelineTests.cs:39-40`, `ActiveSpaceState.cs:54-56` if it names a story, and any `ClientCopy.resx` comment that names 4.7 as future work.
  - [x] `sprint-status.yaml`: this story goes to `review` at the end of dev; `epic-4` stays `in-progress` until the review closes (the epic's close is the orchestrator's). FR-13/FR-14 stay staged (7.7, 7.8).
  - [x] Verification follows CLAUDE.md's policy:
    1. `docker info --format '{{.ServerVersion}}'`.
    2. `dotnet build Yello.slnx` (0 warnings, 0 errors).
    3. Iterate on the affected classes with `--filter-class "<full name>"`.
    4. Make **exactly one** full run, in the background, without the browser suite: `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`, logged to `artifacts/test-runs/4-7-a-viewers-space-and-a-role-that-drops-beneath-you-develop.log`. Record the totals.
    5. Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

Most of a Viewer's Space already exists: every Role-gated control is removed, not disabled, surface by surface, and the API refuses a Viewer on every write route through the one-capability-per-endpoint pipeline. What does not exist is (1) a single proof that the matrix governs Memberships that arrived the way real people arrive — by Invitation and by demotion — on both surfaces, and (2) any way for an open tab to learn that its Role dropped, other than navigating or being refused. AD-9 says permission change is pushed, never polled, but the push channel is 7.3/7.7's. So the recommended design (Q1 (a)) is for the pipeline to echo the Role each request was judged under in a `Yello-Role` header on `2xx` and `403` answers only; the client's one entry point, `ActiveSpaceState.Reconcile`, compares it, filters it to the tab's active Space, and on a drop renders an assertive narration **before** removing the affordances, moving focus to the narration only when the focused control is going away. The demoted Admin in Space settings is narrated, focused and routed to Space home without a blank or half-rendered page. 7.7's push will call the same entry point. Two ledger entries from 4.5's review (the stale-Role refusal copy, the leave lost on a Space switch) are fixed, and the epic's remaining unowned entries are given owners.

### What exists today (survey at `11d0dad`)

- **Domain:** `RoleCapabilityMatrix.cs:36-53` (15 rows, Yes/No/Qualified; Qualified = Admin on `ChangeMembershipRole` `:47`, `RemoveMembership` `:48`); `RoleChangeAuthorityOf :90-95`; `RemovalAuthorityOf :128-133`; `Decide :145-159` (denies anything not in the table). `Capability.cs:27-111`. `StableCode.Of` (kebab-case capability code). `AccessRefusalKind` (`CrossSpace`, `InsufficientRole`).
- **Host:** `RequiredCapability.cs` (`Of()` throws unless exactly one); `ActiveSpaceResolution.cs:143` (reads it), `:159-172` (Token bound elsewhere → `CrossSpace 404`), `:174-199` (404 and 403 recording); `ActiveSpaceResolution.Authority.cs` (`QualifiedAuthority`, `MembershipLost`, `QualifiedRefusal`); `BoundaryProblems.cs:57-65` (the 403 body with `capability`). `MembershipActiveSpaceResolver.cs:40-98` reads the Role per request, no cache. Token: `ApiTokenAuthenticationHandler.cs:53` (no Role in the principal), `.SessionOnly()` (`:102-112`).
- **Routes and capabilities:** see the AC3 table; the leave is the one write under `ReadProjectsAndTasks` (gate `The_leave_route_is_the_one_write_under_ReadProjectsAndTasks`). No endpoint for `EditTaskDescriptionCollaboratively`, `ConfigureSpaceDefaultStatuses`, `ConfigureProjectStatusDelta`, `OfferOwnershipTransfer`.
- **`MembershipChanged`:** published after commit by `MembershipChangePublisher` (removal/leave `MembershipStore.cs:319`, Role change `:361`, accept `InvitationStore.cs:341`); **no subscriber** (`SpaceContentStores.cs:49-54`). `AcceptInvitationHandler.cs:18`'s comment says the accept "publishes nothing", which is stale (new ledger entry, sweep).
- **Client Role state:** `ActiveSpaceState.cs` (scoped per tab; `Current` `:87`, `Arrive :197-214`, `Renamed :223-236` — which also replaces the Role, `Purge :154-187`); `SpaceArrival.razor` (`AskAsync :155-217`; on re-arrival a `403`/`5xx` keeps the old Role `:179`, `:229-233`; re-read triggers `:92-104`, `:124-129`, `RereadAsync :271-272`); `SpaceArrivalContext.Reread`, consumed only by `SpaceSettings.razor:59` → `MembershipSection` (`:360`, `:611`). **No push, no timer, no polling** anywhere in the client. **No other 403 handler re-reads the Role.**
- **Context bar:** `ContextBar.razor:58` (`nav aria-label=AccessibleName`), chip `:79-82` (`RoleChip.razor:21`), settings link `:84-87`, polite switch region `:106`, `OnActiveSpaceChanged :177-201` (announces on switch only). `RoleCopy.cs` (`Compose :57-67`). resx: `RoleOwner..RoleViewer :195-208`, `RolePermitsOwner :211` "can manage everything in this Space, including ownership", `RolePermitsAdmin :215` "can manage Members and settings", `RolePermitsMember :219` "can create and edit Projects and Tasks", `RolePermitsViewer :223` "read only in this Space", `ContextBarAccessibleName :227` "{0} — {1}, {2}".
- **Narrated exit:** `SpaceSettings.Permitted() :123-145` → `SpaceNarration.Say` (`Spaces/SpaceNarration.cs`, purged at `Program.cs:179-180`) → `SpaceHome.razor :35-38` polite `role="status"` region, filled via `AfterArrival` (`:44`, `:64-93`). Focus goes to the "Projects" heading, not the narration. `SpaceSettingsNoLongerAvailable` resx `:710-712`. `DepartureNarration` / `SpaceExit` (`:31-42`) is the Space-lost path.
- **403 copy elsewhere:** `RefusalCopy.cs:206-213`, `RefusalText`; Board `BoardView.razor.cs:873-878` → `SayInPanel :971-979`; capability copy resx `:459-507` ("Viewers cannot edit Tasks.", etc.).
- **Live regions:** polite `LiveAnnouncement.razor:19-20` (Board, ProjectList, List, MembershipSection), many section `role="status"` lines; the only `role="alert"` is `InlineErrorRegion.razor:22` (form errors). **No assertive permission-change region exists.**
- **Disabled:** none for a Role reason. In-flight only (`PrimaryButton :19`, `FormField :41`, SignIn, Register, InvitationView, `AccountSettings :98`), plus `AccountSettings :118` (`_spaces.Count == 0`, a data reason). `aria-busy` for in-flight and skeletons. No `aria-disabled` anywhere.
- **Tests:** `SpaceBoundaryPipelineTests.cs:87-141` (seeded, 5 capabilities × 4 Roles, filter-level), `:147-172`, `:182-210`; `RefusalBoundaryGateTests.cs:98-189` (matrix transcription); per-feature client-predicate agreement gates; `SpaceSwitcherGateTests.cs:243-247` (Viewer = {Read, Token}). Isolation: 19 Viewer `RoleRefused` rows, only 2 with a product-joined Viewer. Revocation: `REV-ROLE-CHANGED` (raw SQL, no write after), `REV-ROLE-CHANGED-BY-OWNER` (product, Admin→Member, both surfaces). Fake API: `FakeYelloApi.Roles` (`:191`), Role-change holds (`FakeYelloApi.Memberships.cs:166-231`); no Role header, no push hook.
- **Helpers:** `InvitationWire` (`Body`, `FreshAddress`, `AcceptBody`, `TokenIn`), `MembershipWire.ChangeBody`, `WireDatabase` (`AddMembershipAsync :445`, `ChangeRoleAsync :484`, `SpacesNotHoldingExactlyOneOwnerAsync :741`, `RefusalKindsAsync`). The product-join helper is private and copied three times (AC3).
- Newest migration: `20261108094508_TaskAssignee`. **4.7 needs no migration.**

### What must be preserved (regression guard)

- **Every per-surface Viewer absence case** listed in AC1 stays green, unchanged.
- **The boundary `404` stays byte-identical**, and now also header-identical (AC8). `RefusalCopyTests`, `SameAsAnchorAndStranger`.
- **The Space-switch announcement** (polite, on switch only) and `SpaceSwitchingTests:188` ("read only in this Space" in the switch announcement).
- **`DepartureNarration`** ("Space deleted.", "You left the Space.") and its routing (4.5, 3.3).
- **AD-1:** no slice or endpoint reads the Role; the pipeline remains the only reader. The header is the pipeline's output, not a new reader.
- **4.4/4.5 Membership section** behaviour: per-row actions, `404` re-read, presence confirm, `LostMembership`.
- **No polling, no timer** (client or server).
- **Idempotency:** the stored record is unchanged; replays still answer the stored status and body.

### Open questions

Each has a recommendation, which is the default. **[LEE]** marks a question whose answer touches authority, isolation, a recorded Lee decision or a security seam. The orchestrator may route those to Lee. Otherwise the dev phase decides them like the rest, as reversible, recorded decisions.

1. **[LEE] How a Role drop reaches an open tab before the push channel exists** (AC4, AC8, AC9; C1). AD-9: pushed, never polled; the channel is 7.3/7.7's.
   - **(a) Recommended.** The pipeline echoes the Role the request was judged under in `Yello-Role`, on `2xx` and `403 insufficient-role` only (never any `404`, never a refusal before resolution). The client reconciles on every answer through one entry point that 7.7's push will also call. No timer, no extra request; the tab learns on its next request of any kind (reads included). Cost: a header on every Space-scoped answer (both surfaces; a Token caller sees its own Role, which `GET /spaces/{id}` already returns); the isolation runner must compare header names (AC8); "pushed" is met on the request path until 7.7, recorded as C1.
   - (b) Re-read the Role (`GET /spaces/{id}`) after **every** `403` on every surface, and on every same-Space navigation (most exists). Cost: a drop is learned only on a refused write or a navigation, so a demoted reader browsing the Board sees nothing — "without my having to try" fails; and the `403` copy shown first is stale (the `:823` defect, everywhere).
   - (c) Build a minimal server-sent-events channel now that subscribes to `MembershipChanged`. Cost: it is 7.3's channel under another name (AD-8: leases, one replica, heartbeat), a second transport for 7.3 to replace, and a hosted connection per tab ahead of NFR-8's bounds.
2. **What counts as a "drop"** (AC4, AC11). **(a) Recommended:** rank order Owner > Admin > Member > Viewer, valid because the matrix's capability sets are nested (gate it against `RoleCapabilityMatrix`). (b) Compare the client predicates' outputs per surface. Cost: every surface computes its own notion; drift.
3. **A rise, and unsent text on a drop** (AC4). **(a) Recommended:** a rise applies at once with a polite "You're now a {Role}." in the context bar's polite region, no focus move; on a drop, open create/rename fields close and their typed text is discarded with the control, as today, the narration explaining why (the editor's retained-text rule is 7.7's). (b) Say nothing on a rise. Cost: controls appear silently, which UX-DR29's logic also dislikes. (c) Keep typed text `readonly` on a drop. Cost: a retained-text panel per form, ahead of 7.7's design.
4. **[LEE] The narration element** (AC4, AC5; UX-DR38/39). **(a) Recommended:** one `RoleNarration` in the authenticated layout under the context bar: `role="alert"` (assertive), `tabindex="-1"`, inserted empty and filled after the first render, persisting until the next navigation or Space purge, focus moved to it only when the focused control is being removed or the surface is being left. It replaces `SpaceNarration` for Role changes and Space home's polite region for them. Cost: `role="alert"` plus focus can double-speak on some screen readers; accepted, because a missed permission change is worse (`EXPERIENCE.md:398`'s reasoning). (b) Keep Space home's polite region and add a per-page assertive region. Cost: several regions, and UX-DR38 says permission changes are assertive.
5. **Copy** (`EXPERIENCE.md:97-104`: terse, sentence case, no Space name, no exclamation). **(a) Recommended:**
   - drop narration: "You're now a {Role}. {RolePermits}." built from the existing strings with the first letter of the permits clause upper-cased in its own resx entries (`RoleNarrationViewer` "You're now a Viewer. Read only in this Space.", `RoleNarrationMember` "You're now a Member. You can create and edit Projects and Tasks.", `RoleNarrationAdmin` "You're now an Admin. You can manage Members and settings.");
   - surface left: the existing "You're now a {0}. Space settings is no longer available." (the AC's words);
   - rise (polite): the same `RoleNarration{X}` strings;
   - "a"/"an" lives in each resx value, not composed (the existing `RoleName` strings carry no article).
6. **[UX] A Member or Viewer typing the Space settings URL** (AC1). Today: the page renders with "Only Owners and Admins can manage Labels." **(a) Recommended:** route to Space home and narrate "Space settings is for Owners and Admins." in the narration element (a `role="status"` politeness, since nothing changed). (b) Keep the page with a page-shaped sentence "Space settings is for Owners and Admins." Cost: a dead surface. (c) Leave it. Cost: object-shaped copy for the wrong capability.
7. **The product-join helper** (AC3). **(a) Recommended:** promote one copy to `Yello.Tests.Shared` and make the three suites call it. (b) Copy it a fourth time. Cost: four drifting copies.
8. **A leave in flight across a Space switch** (AC10; ledger `:829`). **(a) Recommended:** the leave rides a Session-lifetime token; a committed answer after a switch updates the switcher silently, with no narration in the other Space. (b) Block the switch while a leave is in flight. Cost: a Role-free disabled-looking state on the switcher, against the spirit of UX-DR27. (c) Leave it; re-owned to the sweep. Cost: the switcher lists a Space the Account has left until the next refresh, and a click meets the refusal surface.
9. **[LEE] The epic's unowned ledger entries** (Ledger dispositions below). **(a) Recommended:** fold `:823`, `:829` (owned), `:824` (the same method AC9 changes) and `:837` (a Token probe, AC2's theme); re-own the rest to the **sweep** with a dated note naming 4.7's epic-close review; leave the two Epic-4-retrospective entries (`:787`, `:814`) and `:528` (UX) with their owners, flagged in the retrospective's input. (b) Fold every 4.5-owned entry into 4.7. Cost: about ten unrelated client and test fixes widen the diff and the review. (c) Re-own all to the sweep. Cost: `:824` stays wrong in a method this story edits.

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. "When the change is pushed to it" against "no push channel before 7.3".** AD-9 says pushed, never polled; 4.4 built the publish and no subscriber. 4.7 cannot meet "pushed" literally without building 7.3's channel. Q1 (a) meets the narration on the request path and builds the entry point the push will call. The live clause stays with FR-13/FR-14's staging (7.7, 7.8).
- **C2. UX-DR38's copy "Access ended." is for removal; this story's ACs narrate a Role drop.** Both are permission changes; "Access ended." stays the removal/lease copy (7.7). The drop copy is Q5.
- **C3. UX-DR39 sends focus to the narration "then Space home" only for a surface the new Role cannot see.** For a drop that leaves the surface (Board as Viewer), the spine names no destination. Q4 (a): focus moves only when the focused control is removed.
- **C4. "Each row of the matrix" against rows with no endpoint.** Four capability rows and the Offer-accept row have no route yet; they cannot be exercised. AC3's coverage gate makes their stories add their rows.
- **C5. `prd.md`'s matrix says "Transfer ownership"; the spec matrix splits it into "Offer" and "Accept or decline".** The spec is the later transcription; the code follows it (`Capability.cs:15-19`). No action.
- **C6. Space home's narration region is polite (4.4); UX-DR38 says permission changes are assertive.** Q4.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

Owned by 4.7:

- `:823` → **CLOSED by 4.7** (AC9): the `403`'s Role reconciles before the copy is chosen; name the case.
- `:829` → **CLOSED by 4.7** (AC10, Q8 (a)); name the cases. (Or RE-OWNED to sweep if Q8 (c).)

**Epic-close sweep — entries owned by "4.5", "4.6" or "epic 4" that no later story owns** (all from `deferred-work.md` at `11d0dad`; none carries a CLOSED note):

| Line | Entry (short) | Owner now | Recommended (Q9 (a)) |
|---|---|---|---|
| `:824` | A late refused confirm read closes another row's confirm | 4.5 | **Fold into 4.7** (AC9) |
| `:825` | Concurrent total-only re-reads discard each other and an in-flight Show more page | 4.5 | RE-OWN → sweep |
| `:826` | The shown total can fall twice | 4.5 | RE-OWN → sweep |
| `:827` | Row refusals are one slot while row actions are concurrent (`_rowRefusal`, still single, `MembershipSection.razor:152`) | 4.5 | RE-OWN → sweep |
| `:828` | A queued focus request on a busy next row can override Cancel's focus | 4.5 | RE-OWN → sweep |
| `:832` | A route comment claims anyone may remove its own row on `DELETE .../memberships/{id}` (`Program.cs`) | 4.5 | RE-OWN → sweep (or fold if Task 2 edits the same block) |
| `:833` | The acting-authority step's docs omit `MembershipLost` | 4.5 | RE-OWN → sweep |
| `:835` | No case sends a Token's leave with an `Idempotency-Key` | 4.5 | RE-OWN → sweep |
| `:836` | Unused harness members (`FakeYelloApi.ReleaseRemovalUnanswered`, `MembershipWire.LeaveCapabilityCode`, still present) and untested client removal paths | 4.5 | RE-OWN → sweep |
| `:837` | AC7's removal sample probes the leave route on the Session only; add the Token probe | 4.5 | **Fold into 4.7** (AC2's Token theme) |
| `:838` | The removal browser case does not check the switcher | 4.5 | RE-OWN → sweep (AC10's switcher case is adjacent; fold if cheap) |
| `:839` | The leave's purge case is weaker than the `SpaceSwitchMemoryTests` style | 4.5 | RE-OWN → sweep |
| `:840` | Race 4 rests on 4.4's acceptance-under-lock case, recorded only in a remark | 4.5 | RE-OWN → sweep |
| `:787` | A Space deletion overlapping an accept can deadlock (1205 → 500) | **Epic 4 retrospective (Lee)** | Keep; flag in the retrospective's input |
| `:814` | Label delete: focus falls to body; a 5xx on the `404` re-read raises the Space refusal surface | **Epic 4 retrospective (Lee)** | Keep; flag in the retrospective's input |
| `:528` | Is a due date a day or an instant? — "UX, before Epic 4 makes Spaces multi-person" | UX | Keep with UX; flag: Epic 4 has now made Spaces multi-person, so the "before" has passed |

Not unowned (recorded so the review does not re-raise them): `:756` (Invitation rate limit, Lee/PM), `:792` (5.2 still owes the lock), `:794` (Admin creates Admin, Lee/PM), `:805` (confusables, Lee/UX), `:849` (resend → sweep; push → Epic 7), and the 4.5/4.6 review entries already owned by 5.4, 6.5, 7.x, 8.3, UX, Lee or the sweep.

**Also found, outside the requested owners** (no CLOSED note; the dev phase verifies each against the code and closes or re-owns it, or the retrospective takes them): `:217`, `:283`, `:683`, `:734`, `:748`, `:762`, `:772` (owned by **4.3**, done), `:581` (owned by **4.1**, done).

**New entries:**

- "`AcceptInvitationHandler.cs:18` says the accept publishes nothing; `InvitationStore.cs:341` publishes `MembershipChanged`" (owner **sweep**; low).
- "Until the sync channel exists, a Role change reaches an open tab only on its next request (`Yello-Role`); 7.7's push must call `ActiveSpaceState.Reconcile` and 7.8 must prove the 1-second clause through it" (owner **7.7**; low), if Q1 (a).
- "A drop discards text typed in an open create or rename field" (owner **7.7**, with the retained-text rule; low), if Q3 (a).

### Architecture compliance (must)

- **AD-1 / AR-3.** The pipeline is the only reader of the Role; the header is written by the pipeline step. No slice reads a Role, counts a bound, sets a key, or records.
- **AD-3 / AD-20.** Every not-found is the boundary body, now also header-identical. Only the pipeline records refusals.
- **AD-9 / AD-10.** No poller, no timer, client or server.
- **AD-18.** The idempotency record is unchanged; the header is live on replay.
- **AD-19.** The header is additive.
- **AD-21 ring.** The Client never references Domain (`AllowedReferenceEdges.cs`); the Role ranks live in the client, gated against the Domain matrix from the Architecture project.
- **UX-DR31.** The narration, the Role and every Space-scoped client state purge synchronously on a switch; no storage, no mutable static.
- **Code comments** (CLAUDE.md): say why. No story numbers, AC or Q ids, or history. Existing comments are rewritten only where they predict 4.7 (Task 5).

### Library / framework

No new package. Use ASP.NET Core 10.0.11 minimal APIs (`HttpContext.Response.Headers` inside the existing endpoint filter or `OnStarting`, whichever the pipeline step already uses to shape the answer), Blazor WebAssembly 10.0.11 (`DelegatingHandler`, `OnAfterRenderAsync`, `ElementReference.FocusAsync`), xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0 (`page.EvaluateAsync` with a `MutationObserver` for ordering; two `IPage`s in one context for two tabs), ArchUnitNET 0.13.3 and Mono.Cecil for gates. A custom response header is readable by same-origin `fetch` without CORS exposure (the client is hosted by the Host). No web research was needed: every API used is already in the codebase or the BCL.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md` (`:545-563` Memberships; `:87-200` rules; `:756-877` isolation and revocation):

- **Traits:** `Suite`, `Priority`, `Requirement` (FR-16, UX-DR27, UX-DR28, UX-DR29, UX-DR38, UX-DR39, AD-1, AD-3, AD-9), and `Tier`. Names are underscore sentences, one behaviour each.
- **Seed through the product** for every matrix and Viewer-refusal case (Invitation accepted at the Role; Viewer also by demotion). Seed directly only for bulk. Record which.
- **Never a shared email address** (`InvitationWire.FreshAddress`); display names randomised.
- **S-2 after every Role operation.**
- Never `Task.Delay`. Ordering in the browser is proven by a `MutationObserver` log, not by waiting.
- **No new container-backed test class.**
  - Slices: `ActiveSpaceEndpointTests.RoleMatrix.cs` (AC3), the header cases (AC4 replay, AC8) in a partial `ActiveSpaceEndpointTests.RoleHeader.cs`.
  - Isolation: the five new Viewer rows (AC2); the runner's header-name comparison (AC8).
  - Revocation: `REV-DEMOTED-TO-VIEWER-BY-OWNER` (AC2).
  - Client: new classes `ViewerSpaceTests`, `RoleDropTests` on the existing collection (no new fixture); extend `LabelTests`, `MembershipSectionTests`, `LeaveSpaceTests`; `FakeYelloApi` gains the Role header following `Roles[space]` and a hold on any Space-scoped read for AC7's late-answer case.
  - Gates: the matrix-coverage gate (AC3), the Role-rank nesting gate, the header-writer gate, the client-wide disabled-for-Role gate, the no-shared-channel gate, the client no-timer gate (if absent).
- **Every absence has an anchor and is seen red under a plant.** A masked plant is recorded, never deleted.
- **Memory budget (CLAUDE.md):** one `dotnet test` at a time. Iterate with `--project ... -- --filter-class "<full name>"`. The Revocation and Isolation projects each start their own SQL Server; never run two together. The single full run goes in the background with `--max-parallel-test-modules 1`, without the browser suite in Develop. Build `Yello.Client` in Debug first for the Client suite.

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | `disabled="@(!_canManage)"` on the Board's add control | the client-wide disabled gate; `ViewerSpaceTests` sweep |
| 2 | A Viewer-visible create control on Space home | `ViewerSpaceTests` sweep |
| 3 | `PATCH project` declared under `ReadProjectsAndTasks` | `ISO-PROJECT-RENAME-VIEWER`; the AC3 theory |
| 4 | A row removed from the AC3 route table | the matrix-coverage gate |
| 5 | `Decide` returns `Yes` for Viewer on `ManageTasks` | the AC3 theory (both surfaces); the transcription gate |
| 6 | The Token path caches the Role at issue | `REV-DEMOTED-TO-VIEWER-BY-OWNER` (Token arm) |
| 7 | The header added on the boundary or in-Space `404` | AC8's header-identity rows |
| 8 | The header taken from the idempotency record on replay | the replay-after-demotion case |
| 9 | A slice writes the header | the header-writer gate |
| 10 | `Reconcile` applies the Role before `RoleDropping` | the MutationObserver order case |
| 11 | `Reconcile` without the Space check | AC7's late-answer case; the two-tab case |
| 12 | A `BroadcastChannel` post of the narration | the no-shared-channel gate; the two-tab case |
| 13 | The narration copy includes the Space name | the no-Space-name gate and browser case |
| 14 | Space settings routes before narrating, or leaves focus on the heading | the AC5 order and focus cases |
| 15 | Space settings re-renders under the lower Role before routing | the AC5 no-half-render case |
| 16 | `ForbiddenAsync` chooses copy before reconciling | the AC9 case |
| 17 | The leave keeps the Space-purge token | AC10's switcher case |
| 18 | A client `PeriodicTimer` re-reading the Role | the client no-timer gate; the idle-request-count case |
| 19 | Role ranks reordered (Member above Admin) | the nesting gate |

### Previous story intelligence (4.6, 4.5, 4.4)

- **4.6:** review patches were mostly comment hygiene (requirement ids, ATDD narrative, XML docs restating names) across 35+ files — keep new comments clean the first time. A failed side read must not refuse a whole surface (the candidates-read patch): the header handler must never throw or refuse on a missing or unknown header value; ignore it. `HostPool` shares the class database and runs its own dispatcher; mail-reading cases need their own process.
- **4.5:** S1200 split classes; expect `ActiveSpaceState` or `SpaceArrival` to need care. Race participants must fetch an anti-forgery token after sign-in. A duplicate sibling `@key` broke the first client run; keying `LeaveSpaceSection` by `SpaceId` must not collide. Masked plants are recorded honestly.
- **4.4:** a new pipeline dependency must be registered in `SpaceBoundaryPipelineTests` and `ActiveSpaceUnitOfWorkTests` (19 cases broke once). The Space-home narration region had to be inserted empty and filled in a later browser task to be announced — reuse that exact technique for `RoleNarration`. The client gates (copy literals, disabled attribute, mutable static) caught real slips; run the Architecture suite early.

### Git intelligence

- `11d0dad`: 4.6 review close (a failed candidates read no longer refuses the Board/List; comment clean-up).
- `ee5e9a9`: 4.6 implementation (assignment route, outbox email, `NotificationSend`, List Assignee dimension; one migration).
- `add41bb` / `973c482`: 4.6 ATDD and story.
- `4d23bf3`: 4.5 review close.

The cycle is: story; decisions recorded; ATDD scaffolds (`Skip = "red: ..."`, `atdd-checklist-<key>.md`, `PendingUntil: "4.7"` rows); implementation; review fixes. The orchestrator commits each.

### Project Structure Notes

- **New (expected):**
  - Contracts: `Yello.Contracts/Spaces/RoleHeader.cs` (the `Idempotency.cs` shape).
  - Client: `Yello.Client/Components/RoleNarration.razor`; `Yello.Client/Spaces/RoleRanks.cs` (or inside `RoleCopy`).
  - Tests: `ActiveSpaceEndpointTests.RoleMatrix.cs`, `.RoleHeader.cs`; `tests/Yello.Tests.Shared/ProductMembership.cs` (Q7); `tests/Yello.Tests.Client/ViewerSpaceTests.cs`, `RoleDropTests.cs`; an Architecture partial `RefusalBoundaryGateTests.RoleDrop.cs` (or `ClientRoleGateTests.cs`) for the new gates.
- **Updated (expected):**
  - Host: the pipeline step that shapes answers (`ActiveSpaceResolution*.cs`), `Program.cs` (comment).
  - Client: `Sessions/YelloApiHandler.cs`, `Spaces/ActiveSpaceState.cs`, `Components/SpaceArrival.razor`, the authenticated layout, `Pages/SpaceSettings.razor`, `Pages/SpaceHome.razor`, `Spaces/SpaceNarration.cs` (Role use retired), `Components/BoardView.razor(.cs)`, `TaskDetail.razor`, `ProjectList.razor`, `TaskCard.razor` (gated-region marker), `MembershipSection.razor`, `LeaveSpaceSection.razor`, `ContextBar.razor` (rise announcement), `Localisation/ClientCopy.resx`, `wwwroot/css/components.css`.
  - Tests: `IsolationCases.cs` (+ the runner's header comparison), `IsolationCases.Memberships.cs`, `RevocationCases.cs`, `ActiveSpaceEndpointTests.ListMemberships.cs` (helper moved), `SpaceBoundaryPipelineTests.cs` (remarks), `FakeYelloApi.cs` / `.Memberships.cs`, `LabelTests.cs`, `MembershipSectionTests.cs`, `LeaveSpaceTests.cs`, `ClientStorageBanTests.cs`, the existing disabled-attribute gates.
  - Docs: `ARCHITECTURE-SPINE.md` (dated notes on AD-1, AD-9), `TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml`.
- **No new project, no new package, no migration, and no new container-backed test class.**

### References

- `epics.md`: `:102-103` (FR-15, FR-16), `:271-283` (UX-DR27/28/29/30/31/32/38/39), `:375`, `:395-402` (staging), `:450-457` (Epic 4 notes), `:1613-1617`, `:1877-1916` (4.7), `:2481-2528` (7.3), `:2659-2749` (7.7, 7.8).
- `prd.md`: `:45-47`, `:58-62`, `:110` (UJ-4), `:116` (UJ-6), `:315-359` (FR-15, FR-16).
- `role-capability-matrix.md:9-43`; `acceptance-criteria.md:150-166` (CAP-15/16).
- `ARCHITECTURE-SPINE.md`: AD-1 (`:71-75`), AD-8 (`:127-131`), AD-9 (`:133-137`), AD-10.
- `EXPERIENCE.md`: `:75-93` (Space Context and Role Legibility), `:97-104` (voice), `:202-205` (context bar, Role chip), `:228-244` (interruption cluster, general case), `:378-400` (live regions, focus destinations), `:498-512` (UJ-4), `:529`, `:536`. `DESIGN.md:391`, `:439`, `:455` (Role chip).
- `test-design-qa.md`: `:287` (V-8), `:330`, `:357` (X-7), `:366-367` (X-8, X-9). `test-design-architecture.md:170-176` (matrix at INT, never E2E — AC3 is INT; the client cases test narration and absence, not the matrix).
- `implementation-readiness-report-2026-08-22.md`: `:495`, `:962-969`.
- `tests/TESTING-CONVENTIONS.md`: `:5-30`, `:87-200`, `:545-563`, `:756-877`.
- `deferred-work.md`: `:528`, `:787`, `:814`, `:823-840`.
- Story files: `4-6-assign-a-task-and-be-told.md` (template, review findings), `4-5-remove-a-membership-or-leave-a-space.md` (Q8 leave placement, Q12 `LostMembership`, `:58-59` the pushed narration deferred to 4.7/7.7), `4-4-manage-memberships-and-roles.md` (`:25`, `:52` pushed narration, chip update and per-tab filter are 4.7's; Q7 the publish seam; Q15 the narration region).

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-4-7-a-viewers-space-and-a-role-that-drops-beneath-you.md` (decisions, assumptions, discrepancies with this story's prose, findings).

| File | Cases | ACs | State today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RoleMatrix.cs` | 1 theory, 11 rows | AC2, AC3, AC12 | **green, active** (standing gate) |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RoleHeader.cs` | 4 | AC4, AC8, AC9, AC12 | skipped |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RemoveMembership.cs` (Token leave probe added, `:837`) | 1 | AC2 | **green, active** |
| `tests/Yello.Tests.Isolation/IsolationCases.ViewerSpace.cs` | 7 rows x 2 surfaces | AC2 | **green, active** |
| `tests/Yello.Tests.Revocation/RevocationCases.cs` (`REV-DEMOTED-TO-VIEWER-BY-OWNER`) | 1 row x 2 surfaces | AC2, AC12 | **green, active** |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.RoleDrop.cs` | 11 | AC1, AC3, AC4, AC7, AC11 | 7 **green, active** (coverage, nesting, disabled + detector, shared channel + detector, interval); 4 skipped (client ranks, narration copy, header writer, CORS exposure) |
| `tests/Yello.Tests.Client/ViewerSpaceTests.cs` | 3 (one a 2-row theory) | AC1 | the Viewer sweep and the Owner anchor **green, active**; the Q6 typed-URL theory skipped |
| `tests/Yello.Tests.Client/RoleDropTests.cs` | 10 | AC4, AC5, AC6, AC7, AC11 | skipped |
| `tests/Yello.Tests.Client/MembershipSectionTests.cs` (two added) | 2 | AC9 (`:823`, `:824`) | skipped |
| `tests/Yello.Tests.Client/LeaveSpaceTests.cs` (two added) | 2 | AC10 (`:829`) | skipped |

Support: `tests/Yello.Tests.Shared/RoleHeaderWire.cs` (stand-in for `RoleHeader.HeaderName`; swap and delete), `tests/Yello.Tests.Client/FakeYelloApi.RoleHeader.cs` (the fake echoes `Roles[space]` in the header on `2xx` / `403 insufficient-role`, exposes it cross-origin, and can hold any Space-scoped read).

Activation order, riskiest assumption first:

1. `RoleDropTests.A_drop_on_the_Board_is_narrated_before_any_affordance_is_removed` - Lee's Q1 (a) end to end: an ordinary answer's header, read cross-origin by the handler, narrates a drop before the surface settles.
2. `RefusalBoundaryGateTests.The_cross_origin_policy_exposes_the_Role_header`, then the four `RoleHeader` cases (absence and in-Space-404 identity first, then the echo, the replay, the Admin-limit refusal), then `Exactly_one_production_type_writes_the_Role_header`.
3. The rest of `RoleDropTests` (focus moved, focus kept, Admin on Space home, rise, chip and name, two tabs, late answer, idle), then `The_clients_Role_ranks_agree_with_the_matrix_nesting` and the narration copy gate.
4. The AC5 settings exit (`RoleDropTests.The_demoted_Admin_in_Space_settings_...`), then the Q6 typed-URL theory, then AC9 (`MembershipSectionTests`) and AC10 (`LeaveSpaceTests`).

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (bmad-dev-story, unattended story cycle), 2026-11-08.

### Debug Log References

- Logs: `artifacts/test-runs/4-7-a-viewers-space-and-a-role-that-drops-beneath-you-develop.log` (the one full run), `artifacts/plants.log`, `artifacts/plants2.log`.
- **Scaffolds corrected (not deleted):**
  - `RoleHeaderWire` (Shared) deleted; every use now reads `Yello.Contracts.Spaces.RoleHeader.HeaderName` (the Slices header cases, `FakeYelloApi.RoleHeader.cs`); `RoleCodes[0]` became the literal `"owner"`.
  - `RefusalBoundaryGateTests.Exactly_one_production_type_writes_the_Role_header` excludes `CrossOriginPolicy`, which names the header only to expose it (`WithExposedHeaders`), writing nothing. Without the exception the CORS exposure that `The_cross_origin_policy_exposes_the_Role_header` requires is a second "writer".
  - `RoleDropTests`: opening a Task panel sends no request (the panel reads the Board's replica), so it can carry no Role. The drop, rise, chip and two-tab cases now make the tab's next ordinary request with a same-page navigation (`RequestAgainAsync`), as `Focus_on_a_control_the_drop_keeps_stays_where_it_is` already did. First run: those four failed waiting for the narration (the stated red reason was right; the trigger was not).
  - `RoleDropTests` affordance selectors: `main button.board__add` matches nothing (the control is `.board-column__add`), and an ancestor-qualified selector (`nav.context-bar a[...]`) can never match a node the `MutationObserver` reads after it was detached. Now `.board-column__add` and `a.context-bar__settings`.
  - `RoleDropTests.Frame` was a positional record Playwright cannot construct; the frames are read as JSON and deserialised with `System.Text.Json`.
  - `RoleDropTests.A_drop_is_narrated_only_in_the_tab_showing_that_Space`: tab 2's same-address navigation was not awaited before counting requests (`NetworkIdle` returned first); it now waits for tab 2's own Space answer.
  - `LeaveSpaceTests.A_leave_left_unanswered_after_a_switch_changes_nothing_in_the_other_Space`: the switcher asks the server when opened and the fake applied the leave there, so "the switcher still lists both" cannot hold; and the switch itself announces Harbour, so "nothing said" is now "nothing about the leave said" (neither "You left the Space." nor "Not sent.").
- **Changed by the always-present narration element:** `TaskDetailTests.A_refused_title_edit_...` and `MoveTests.A_refused_move_...` counted every `[role='alert']` to prove no alert surfaced; the empty Role narration is one, so they now count non-empty alerts. `InvitationTests.A_Member_and_a_Viewer_have_no_invite_affordance_at_all` and `SpaceSwitchCarryOverTests.A_narration_said_in_one_Spaces_home_is_not_carried_into_the_next` read the typed-address sentence and the drop narration from `.role-narration` (Q6, AC5).
- **Changed by design** (rewritten as AC5 and Q6 say): `LabelTests.A_Member_has_no_Label_definition_affordance` (the typed settings address now routes to Space home with "Space settings is for Owners and Admins."), `LabelTests.A_Role_reread_to_Member_on_Space_settings_is_narrated_then_routed_to_Space_home` and `MembershipSectionTests.An_Admin_demoted_meanwhile_is_narrated_out_of_Space_settings_on_the_403` (the narration is `.role-narration` and keeps focus on Space home), `MembershipSectionTests.The_Space_home_narration_region_...` renamed `The_Role_narration_region_is_inserted_empty_and_filled_after_the_first_render`.
- **Plants** (each saved in memory and restored in a `finally`; rebuilt after):

  | Plant | Expected red | Seen |
  |---|---|---|
  | 1 `disabled="@(!_canManage)"` on the Board's empty-state add | the client-wide disabled gate | red: `No_client_control_is_disabled_for_a_Role` |
  | 7 the header also on a `404` | AC8's header-identity case | red: `No_answer_but_a_2xx_or_a_403_insufficient_role_carries_the_Role_header` |
  | 8 the header taken from the idempotency record | the replay case | not plantable as written: the record stores only `Location` (`IdempotentResponses.Capture`); setting the header early is overwritten by the start-time echo. Recorded, not run |
  | 9 a slice writes the header (`MembershipRemovalEndpoints`) | the header-writer gate | red: `Exactly_one_production_type_writes_the_Role_header` |
  | 10 `Reconcile` applies the Role before `RoleDropping` | the order case | red: `RoleReconcileTests.A_drop_is_raised_and_narrated_before_the_lower_Role_is_applied` (unit level; the browser order case not re-run under the plant) |
  | 11 `Reconcile` without the Space check | the late-answer filter | first form masked by the analyzer (S1172, unused parameter, build failed); second form red: `RoleReconcileTests.A_Role_for_any_Space_but_the_active_one_is_dropped_unseen` |
  | 12 a `BroadcastChannel` post in `role-narration.js` | the no-shared-channel gate | red: `No_client_code_shares_a_Role_or_a_narration_between_tabs` |
  | 13 the Space name in `RoleNarrationViewer` | the narration copy gate | red: `The_Role_narration_copy_names_the_Role_and_what_it_permits_and_no_Space` |
  | 19 Admin and Member ranks swapped | the nesting gate | red: `The_clients_Role_ranks_agree_with_the_matrix_nesting` |
  | 2, 3, 4, 5, 6, 14, 15, 16, 17, 18 | browser sweep, isolation row, coverage gate, theory, revocation Token arm, AC5 order/focus, half-render, AC9 order, AC10 token, client timer | **not run in Develop.** 3-6 were seen red at ATDD (the checklist's findings); the browser plants were not run because a whole-Client run was reaped for low memory mid-phase. Left for Review |

- A whole-Client-suite run (to cover the markup changes beyond the affected classes) was stopped by the harness for low system memory, not by a failure; it was not restarted. The affected Client classes were run filtered instead (below).

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-11-08). Nine open questions with recommendations; Q1, Q4 and Q9 are marked [LEE]. Dominant finding: AC4's "pushed" has no channel before 7.3/7.7 (C1); Q1 (a) meets it on the request path through a pipeline-echoed `Yello-Role` header and one client entry point the push will reuse.
- **Built.** The pipeline (`ActiveSpaceResolution.QualifiedAuthority.EchoJudgedRole`) adds `Yello-Role` when the answer starts, on `2xx` and `403` only, so a `404` of either kind never carries it and an idempotent replay carries the live Role (the record stores only `Location`). `CrossOriginPolicy` exposes it. The client's `YelloApiHandler` hands each Space-scoped answer's Role to `ActiveSpaceState.ReconcileAsync` **before** the caller reads the answer; a drop raises `RoleDropping`, waits for `RoleNarration` (under the context bar, `role="alert"`, `tabindex="-1"`, empty first render) to render and move focus, then applies the Role; a rise applies at once and the context bar says it politely. `SpaceSettings` leaves on a drop below it (narrate, focus, route, keeping its last permitted render), and routes a typed address for a Member or Viewer to Space home with a polite sentence. `MembershipSection.ForbiddenAsync` chooses its copy from the reconciled Role. `LeaveSpaceSection` sends on the Session's lifetime and is keyed by Space.
- **Q2 - (a):** a drop is a rank compare (`RoleRanks.RankOf`), gated against the matrix nesting. Reversible: replace with per-surface predicates.
- **Q3 - (a):** a rise is said politely in the context bar ("You're now a Member. ..."); a drop discards text typed in an open create or rename field (ledger entry to 7.7). Reversible.
- **Q5 - (a):** `RoleNarration{Owner,Admin,Member,Viewer}` in `ClientCopy.resx`, the article in each value, no placeholder; Owner was added for a rise to Owner (transfer). Reversible.
- **Q6 - (a):** the typed Space settings address for a Member or Viewer routes to Space home and says "Space settings is for Owners and Admins." politely in the same element (`role` switches to `status` for that sentence); the `manage-labels` paragraph on that page is gone. Reversible.
- **Q7 - not (a), left apart:** the three product-join helpers stay one per harness (the Slices one on `WireSession`, the isolation and revocation ones on `ProductActor` with secret holding); unifying needs a common actor shape. Recorded in `TESTING-CONVENTIONS.md` and the ledger (sweep). Cut this line to take (a) later.
- **Q8 - (a):** the leave rides the Session's lifetime; a committed answer after a switch removes the Space from the switcher silently. Reversible.
- **Focus before removal** uses a `data-role-gated` marker on the controls a drop removes (task-card actions, the column add, the Task panel's title and delete, the Project create and row actions, the settings link, the Task create form) plus a fallback that puts focus on the narration if the focused element was removed anyway. `TaskCard` now renders one title button for every Role, so a card holding focus keeps it across a drop.
- **The one non-busy disabled state** (`AccountSettings`, `_spaces.Count == 0`) is a data reason and stays; the disabled gate names it.
- **Contracts/snapshot:** no contract snapshot covers headers; the header is additive (AD-19).
- **Not done here:** the browser plants (see Debug Log), and a whole-Client run. The AC7 late-answer browser case passes, but the switch cancels the held read before its answer reaches the handler, so the Space filter is proven at unit level (`RoleReconcileTests`); ledger entry (sweep).
- **Full run (one, without the browser suite):** 1909 total, 1902 passed, 0 failed, 7 skipped (the pre-existing migration-bundle and timing/envelope skips), log artifacts/test-runs/4-7-a-viewers-space-and-a-role-that-drops-beneath-you-develop.log. Affected Client classes run filtered: every fake-backed Client class across five batches green after the corrections above (the RealHost classes were not run; they ride Review's browser suite).

### File List

- Yello.Contracts/Spaces/RoleHeader.cs (new)
- Yello.Host/Spaces/ActiveSpaceResolution.cs
- Yello.Host/Spaces/ActiveSpaceResolution.Authority.cs
- Yello.Host/Sessions/CrossOriginPolicy.cs
- Yello.Host/Program.cs
- Yello.Client/Program.cs
- Yello.Client/Sessions/YelloApiHandler.cs
- Yello.Client/Spaces/ActiveSpaceState.cs
- Yello.Client/Spaces/RoleDropEventArgs.cs (new)
- Yello.Client/Spaces/RoleNarrator.cs (new)
- Yello.Client/Spaces/RoleRanks.cs (new)
- Yello.Client/Spaces/RoleCopy.cs
- Yello.Client/Components/RoleNarration.razor (new)
- Yello.Client/wwwroot/js/role-narration.js (new)
- Yello.Client/Layout/MainLayout.razor
- Yello.Client/Components/SpaceArrival.razor
- Yello.Client/Components/ContextBar.razor
- Yello.Client/Components/MembershipSection.razor
- Yello.Client/Components/LeaveSpaceSection.razor
- Yello.Client/Components/TaskCard.razor
- Yello.Client/Components/TaskDetail.razor
- Yello.Client/Components/TaskCreate.razor
- Yello.Client/Components/BoardView.razor
- Yello.Client/Components/BoardColumn.razor
- Yello.Client/Components/ProjectList.razor
- Yello.Client/Components/ProjectRow.razor
- Yello.Client/Pages/SpaceSettings.razor
- Yello.Client/Pages/SpaceHome.razor
- Yello.Client/Localisation/ClientCopy.resx
- Yello.Client/wwwroot/css/components.css
- tests/Yello.Tests.Shared/RoleHeaderWire.cs (deleted)
- tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.RoleDrop.cs
- tests/Yello.Tests.Architecture/RoleReconcileTests.cs (new)
- tests/Yello.Tests.Architecture/SessionSeamTests.cs
- tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.RoleHeader.cs
- tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/SpaceBoundaryPipelineTests.cs
- tests/Yello.Tests.Client/FakeYelloApi.RoleHeader.cs
- tests/Yello.Tests.Client/RoleDropTests.cs
- tests/Yello.Tests.Client/ViewerSpaceTests.cs
- tests/Yello.Tests.Client/MembershipSectionTests.cs
- tests/Yello.Tests.Client/LeaveSpaceTests.cs
- tests/Yello.Tests.Client/LabelTests.cs
- tests/Yello.Tests.Client/InvitationTests.cs
- tests/Yello.Tests.Client/SpaceSwitchCarryOverTests.cs
- tests/Yello.Tests.Client/MoveTests.cs
- tests/Yello.Tests.Client/TaskDetailTests.cs
- tests/TESTING-CONVENTIONS.md
- _bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md
- _bmad-output/implementation-artifacts/deferred-work.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/4-7-a-viewers-space-and-a-role-that-drops-beneath-you.md

## Change Log

- 2026-11-08: story created (create-story).
- 2026-11-08: developed (dev-story): the `Yello-Role` header, the client's one Role entry point and the Role narration, the Space settings exit and typed-address route, the stale-Role refusal copy, the leave across a switch; ledger dispositions and spine notes. Status to review.
