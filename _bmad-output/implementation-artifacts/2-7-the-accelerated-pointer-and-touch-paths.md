---
baseline_commit: 0e54a8c
---

# Story 2.7: The accelerated pointer and touch paths

Status: done

Epic: 2 — Track your own work on a Board
Story key: `2-7-the-accelerated-pointer-and-touch-paths`
Requirements owned (Story Coverage Index, `epics.md:359-364`): **UX-DR22** (pointer drag path: press and drag to lift, move, drop, `lift`/`settle` motion, `drop-zone` marks the destination — `epics.md:263`, `EXPERIENCE.md:299`), **UX-DR23** (touch path: long-press 320ms, 10px axis-agnostic slop, scroll/pan wins, haptics where available, auto-scroll, cancellable, does not discharge NFR-9 — `epics.md:264`, `EXPERIENCE.md:301-308`), **UX-DR6** (the motion timing contract, consumed here: `lift` 110ms, `settle` 120ms, `long-press-threshold` 320ms, `long-press-slop` 10px, reduced motion — `epics.md:241`, `DESIGN.md:506-521`; 1.2 built the tokens).
Also binding: **FR-29** (the move itself, owned and built by 2.6 — `prd.md:526-534`), **NFR-9** (WCAG 2.1 AA; a gesture never discharges it — `prd.md:753-757`), **UX-DR4/UX-DR5/UX-DR8** (2px emphasis border, the one sanctioned shadow, `task-card-lifted` and `drop-zone` components — `epics.md:239, 240, 246`; `DESIGN.md:145-148, 197-200, 374-376, 435, 452, 466`), **UX-DR21** (the Move control stays canonical — never second to a gesture, `EXPERIENCE.md:283-295`), **UX-DR25/UX-DR38** (move/return announcements; announce on deliberate action only — `EXPERIENCE.md:322-324, 390`), **UX-DR26** (no hover-only affordance; optimistic by default — `EXPERIENCE.md:330, 335`), **UX-DR27** (a Viewer has no drag handle: absent, not disabled — `epics.md:271, 1035`), **AD-11/AR-20** (optimistic replica, reverted on refusal — spine `:138-142`), **AD-15/AR-21** (a move writes one key — spine `:162-168`), **FR-35** (position not writable over the API — the drag uses 2.6's Session-only route).
Explicitly **not** here: the keyboard pick-up / arrow grammar, `role="grid"`, pick-up announcement copy for the keyboard (**2.8**); paging and positions beyond the loaded page (**2.9**); the <768px Status pager, "long-press drag within the visible column" and RTL mirroring of drag direction (**2.11**, UX-DR41/UX-DR42 — 2.7's code must not contradict them); live observation of another participant's move (**Epic 7**). **No server, Contracts, migration or isolation change**: the drag commits through 2.6's `POST /api/v1/spaces/{spaceId}/tasks/{taskId}/move` (`TaskRoutes.MoveOf`, `MoveTaskRequest(StatusId, AfterTaskId)`) via the client's existing `BoardView.MoveAsync` — there is no second write path.
Ledger entries owned by this story (`deferred-work.md`): **`:52`** (`opacity` / alpha on a surface carrying a gated contrast pair would make the 36 computed ratios fiction — "most likely 2.7 (the lifted card under drag)"). Also touched: `:36` (radius *mapping* — the drop zone and the lifted card take a radius; add their clause), the 2.6 note in the "Two identical announcements" entry (owner 2.8 — drags repeat the move sentence), the 2.6-review entry "The Move picker offers a neighbour whose own move is still unadmitted" (owner 2.8 — chained drags hit it too). Code hooks naming 2.7: `TaskCard.razor:27` ("No drag handle (2.7)"), `base.css:18-20`, `components.css:731-736, 821-834`, `tokens.css:291-292`, `DesignFoundationGateTests.cs:928-967`, `DesignFoundationGateTests.Board.cs:59`, `DesignFoundationGateTests.MovePicker.cs:87`, `tests/Yello.Tests.Client/BoardTests.cs:142`.
Depends on: **2.2** (Board, cards, columns), **2.3** (replica, panel, pointer-blocking layer), **2.5** (column focus target, `_deleted`), **2.6** (`MoveAsync`, `MoveChoice`, `PlaceAfter`, announcements, Board re-read on `409`, the Move control and card menu), all done.

> **Isolation gates.** 2.7 maps no route, so 1.9-GATE-002 is untouched and no isolation row is owed. No row is pending on 2.7 today (grepped `tests/Yello.Tests.Isolation` 2026-10-28). If the ATDD phase adds one (it should not), GATE-003 requires it live before `review`.

## Story

As someone using a mouse or a phone,
I want to drag a Task where I want it,
so that the fast way is available without being the only way.

## Acceptance Criteria

AC1–AC8 are reproduced **verbatim** from `epics.md:1219-1251` (`status: final`), numbered by position. AC9–AC15 come from the UX spines, the PRD, the ledger and 2.6's built behaviour; each cites its source.

**AC1 — pointer: lift, drop zone, settle**
**Given** a pointer
**When** a Task is pressed and dragged
**Then** it lifts with a hard offset shadow, a 1° rotation and a 2px border, a dashed drop zone marks the destination, and the drop settles — all inside the near-instant motion budget
**And** the dashed treatment keeps the underlying order readable mid-drag

**AC2 — touch: long-press lift**
**Given** a touch surface
**When** a Task is long-pressed
**Then** the lift commits only after the long-press threshold, confirmed by the lifted treatment and by haptic feedback where the platform offers it

**AC3 — slop: axis-agnostic dead zone**
**Given** a pending lift
**When** the finger moves beyond the slop tolerance in **any** direction before the threshold
**Then** the gesture is treated as a pan and the pending lift is cancelled
**And** the rule is axis-agnostic with a real dead zone, because a single-axis rule breaks between 768px and 1279px where the Board itself scrolls horizontally, and a zero-tolerance rule cancels every lift on ordinary finger jitter

**AC4 — scroll and pan win**
**Given** a pending or active lift
**When** scroll or pan intent is detected
**Then** it always wins over the lift

**AC5 — auto-scroll**
**Given** an active drag
**When** the pointer approaches a column edge
**Then** that column auto-scrolls; and near the viewport edge, the Board does

**AC6 — cancellable**
**Given** an active drag
**When** it is dragged back to origin or released outside any drop zone
**Then** nothing moves

**AC7 — reduced motion**
**Given** `prefers-reduced-motion: reduce`
**When** a drag occurs
**Then** the lift, settle and column-reflow transitions do not run, and no information is lost because motion only ever reported a change conveyed structurally

**AC8 — gestures do not discharge NFR-9**
**Given** these gestures exist
**When** NFR-9 is assessed
**Then** they do not discharge it — the keyboard path is separately mandated and is not derived from them

**AC9 — one write path: the drop is 2.6's move** *(FR-29, FR-35, AD-11, AD-15; 2.6 Q1-Q3, Q7, Q8; orchestrator instruction)*
**Given** a drop on a drop zone that differs from the Task's current slot
**When** it commits
**Then** the client calls 2.6's `BoardView.MoveAsync` with `new MoveChoice(destinationStatusId, afterTaskId)` — `afterTaskId` the Task immediately above the drop zone in the destination column **excluding the dragged Task**, `null` for the top — so it is sent as `POST TaskRoutes.MoveOf(...)` with `MoveTaskRequest`, never a key, never a `PATCH`
**And** it is optimistic exactly as a picker move is: rendered at once in the dropped slot with both totals moved, re-placed by the server's key on `200`, announced "Moved to {Status}, position {n} of {total}." on admission; a refusal reverts to the last admitted slot, says the refusal on the Board (Q6) and announces "Returned to {Status}, position {n}."; `409 task-position-unavailable` re-reads the Board; no answer says "Not sent.", reverts and keeps the key; `404` follows the Board's refusal surface; an answer for a Task this tab deleted is dropped
**And** a drop on the Task's current slot sends nothing (Q5)

**AC10 — absent for a Viewer, never the only way** *(UX-DR27, UX-DR21, UX-DR26; `epics.md:271, 1035`; `EXPERIENCE.md:199, 283-295, 330`)*
**Given** a Viewer's Board
**When** it renders or is pressed, long-pressed or dragged by pointer or touch
**Then** no card lifts, no drop zone appears, no request is sent, no grab cursor, `touch-action` override or drag listener is attached to a Viewer's card, and nothing is disabled
**And** for a mover the Move control and the card's actions control remain present and fully operable at 320, 768 and 1280px whether or not a drag ever happens, with nothing about the drag revealed on hover alone

**AC11 — a press is still a click; a drag never opens the Task** *(`EXPERIENCE.md:199`; 2.3 Q7; 2.6 Q6; Q3, Q4)*
**Given** a mover's card
**When** it is pressed and released without moving beyond the drag-start distance (Q3)
**Then** the title opens Task detail and the actions control opens the card menu exactly as today
**And** a press that became a drag (lifted, then dropped or cancelled) never opens Task detail or the menu on release, and on touch a long-press that lifts never opens the platform context menu, the text-selection callout or 2.6's card menu (Q11)

**AC12 — the lifted card and the drop zone are drawn from the design system** *(UX-DR4, UX-DR5, UX-DR8; `DESIGN.md:145-148, 197-200, 374-376, 435, 452, 466, 508-517`; `deferred-work.md:36, 52`)*
**Given** an active drag
**When** it renders
**Then** the lifted card takes `task-card-lifted` — `--border-emphasis-width` (2px) solid border, the hard offset shadow `0 6px 0 rgba(0,0,0,.6)` from a token, `rotate(-1deg)` — and is the **only** surface in the product carrying a shadow; the drop zone takes `drop-zone` — 2px dashed `focus-ring` border, `focus-ring` foreground, `rounded.md` (3px), never filled
**And** the lift transitions on `--motion-lift` and the settle on `--motion-settle` (no literal duration), the lifted card and the drop zone stay fully opaque (no `opacity` below 1, no alpha colour, Q8), and the shadow exception is narrowed by the gates to exactly the lifted card

**AC13 — Esc and interruption cancel** *(`epics.md:1282-1284` "the innermost meaning wins"; AC6; UX-DR31 purge; Q10)*
**Given** a pending or active lift
**When** `Esc` is pressed, the pointer is cancelled by the platform (`pointercancel`/`touchcancel`), a second touch point arrives, the window loses focus, the Board is purged (Space switch) or re-read under the drag, or the dragged Task is deleted meanwhile
**Then** the drag ends with nothing moved, no request sent, the card in its admitted slot, and every drag listener and timer released

**AC14 — announced on deliberate action only** *(UX-DR25, UX-DR38; `EXPERIENCE.md:322-324, 390`; Q7)*
**Given** a drag
**When** it is dropped and admitted, refused, or cancelled after the lift committed
**Then** the existing polite live region says the 2.6 sentences ("Moved to …, position n of N." on admission; "Returned to …, position n." on a refusal or on a cancel after lift) and nothing else — never on hover, on crossing a column, on auto-scroll, or on a pending lift cancelled by a pan

**AC15 — no performance cost to the Board** *(AD-11 "never blocks"; NFR-5 spirit; Q1)*
**Given** a drag in progress
**When** the pointer moves
**Then** per-move work stays in the browser (no .NET interop call per `pointermove`); .NET is told only on lift, on a change of destination slot, on drop and on cancel, and no network request is made before the drop

## Tasks / Subtasks

- [x] **Task 1 — Confirm the baseline, then decide the open questions** (AC: all)
  - [x] `dotnet build Yello.slnx` (0 warnings, 0 errors). Record the baseline from the last full run (2.6 code review: **1185 total, 1182 passed, 0 failed, 3 skipped**) or re-measure at the ATDD commit.
  - [x] List any isolation row with `PendingUntil: "2.7"` in the Debug Log (expected: none).
  - [x] Decide Q1–Q4 before Task 2, Q5–Q11 before Task 3, Q12–Q14 before Task 6. Record each in Completion Notes as "**Qn - (x).** … Cost to later stories: …". Each is reversible.

- [x] **Task 2 — The drag engine in the browser** (AC: 1-6, 11, 13, 15)
  - [x] Per Q1, one ES module `Yello.Client/wwwroot/js/board-drag.js` (no bundler, no package), imported lazily by the Board through `IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/board-drag.js")` and disposed with the Board (`IAsyncDisposable`), with a `DotNetObjectReference` for the callbacks (disposed too).
  - [x] Attach **one** delegated set of listeners to the Board's columns container for movers only (`pointerdown`, `pointermove`, `pointerup`, `pointercancel`, `keydown` for `Esc`, `blur`; plus `touchstart`/`touchmove` with `{ passive: false }` for Q11's touch handling). For a Viewer the module is never attached (AC10).
  - [x] **Mouse/pen** (`pointerType !== "touch"`): primary button only; press records the origin; movement beyond the drag-start distance (Q3) lifts; `setPointerCapture` on lift.
  - [x] **Touch**: press starts a **pending** lift timer of the threshold (read `--motion-long-press-threshold` and `--motion-long-press-slop` from `getComputedStyle(document.documentElement)` so the tokens stay the single source — Q2); Euclidean distance `> slop` before the timer fires cancels the pending lift and lets the native pan proceed (AC3, axis-agnostic); timer fires → lift, `navigator.vibrate?.(…)` feature-detected (Q12), and from then `touchmove` is `preventDefault`ed so the finger drives the drag, not the page (AC4 is decided in Q9).
  - [x] Press never starts a drag on the actions control, the card menu, the Move picker, or any element inside them (Q4); starting a drag closes an open card menu.
  - [x] Hit-testing: resolve the destination column and index from bounding rects of `[data-status-id]` columns and `[data-task-id]` cards (add both attributes — Task 3), excluding the dragged card; outside every column → "no drop zone" (AC6). Back over the origin slot → origin (AC6, Q5).
  - [x] The dragged card follows the pointer by writing **only** two CSS custom properties on it (`--drag-x`, `--drag-y`) with `style.setProperty`; the module never inserts, removes, reorders or re-classes DOM that Blazor renders (Q1).
  - [x] Auto-scroll (AC5, Q13): in a `requestAnimationFrame` loop while lifted — within the edge zone of the hovered column's block edges scroll that column (`.board-column`, `overflow-y: auto`); within the edge zone of the viewport's inline edges scroll the Board (`.board__columns`, `overflow-x: auto`), and of the block edges the page; speed proportional to depth into the zone; `scrollBy` with `behavior: "instant"`; re-hit-test after each scroll.
  - [x] Call .NET only on: lift (`taskId`), destination change (`statusId`, `index`, or none), drop (`statusId`, `afterTaskId`) and cancel (AC15). Suppress the `click` that follows a drag (capture-phase, one-shot) (AC11).
  - [x] Cancel paths (AC13): `Esc`, `pointercancel`, `touchcancel`, a second pointer/touch, `blur`, `visibilitychange` hidden, and a `cancel()` export the Board calls on purge, re-read and dispose. Each releases capture, timers, the rAF loop and the custom properties.

- [x] **Task 3 — The Board side in .NET** (AC: 1, 6, 9, 10, 12-14)
  - [x] A small drag-state holder outside `BoardView.razor.cs` (S1200 dependency cap — 2.6 had to split `TaskMoveEndpoints` and `TaskMenu` for it): e.g. `Yello.Client/Components/BoardDrag.cs` owning the module reference, the `DotNetObjectReference`, `[JSInvokable]` callbacks, and the state `(Guid LiftedTaskId, Guid? TargetStatusId, int? TargetIndex)`.
  - [x] `BoardColumn.razor`: `data-status-id` on the `<section>`; the `<ul>` (or a drop zone container) rendered also when the column is **empty** while a drag is active, so an empty column is a drop target (today `ul` renders only when `Tasks.Count > 0`); render the drop zone element (`.drop-zone`, text from resx per Q7's copy decision — mockup says "Drop here", `board.html:170-171`) at the target index.
  - [x] `TaskCard.razor`: `data-task-id` on the `<li>` for movers (keep the title button's existing attribute — tests use it); a `Lifted` parameter rendering `task-card--lifted`; replace the header line "No drag handle (2.7)." with what is true now. A Viewer's card renders exactly as today.
  - [x] Drop → if the slot equals the Task's current slot, end the drag, send nothing (Q5); else `MoveAsync(taskId, new MoveChoice(statusId, afterTaskId), origin: Drag)` (Q6) — the existing optimistic path does the rest (AC9).
  - [x] Cancel after lift → announce "Returned to {Status}, position {n}." with the existing `TaskReturned` key (Q7); cancel of a pending lift → nothing.
  - [x] Purge (`PurgePanel` / Space purge), `RereadBoardAsync`, a Task deletion of the lifted Task, and dispose all call the module's `cancel()` and clear the state (AC13).
  - [x] Opening the panel (pointer-blocking layer) ends any drag; no drag can start while the panel is open.

- [x] **Task 4 — CSS: lifted card, drop zone, motion, touch** (AC: 1, 7, 10, 12)
  - [x] `tokens.css`: a shadow token for the lifted card (Q8 recommends `--shadow-lifted: 0 var(--…) 0 rgba(0, 0, 0, 0.6)` or the literal `0 6px 0 rgba(0,0,0,.6)` — inside the token layer, where colour functions and px are allowed). It is not a hex colour, so `The_colour_token_layer_declares_exactly_the_thirty_names_the_design_states` does not count it (it reads hex-valued custom properties only, `ColorTokenContrastTests.cs:508-519`); check `The_non_colour_token_families_are_complete` (`DesignFoundationGateTests.cs:1700`) and add the family deliberately if it pins names.
  - [x] `components.css`: `.task-card.task-card--lifted` — `border-width: var(--border-emphasis-width)`, `box-shadow: var(--shadow-lifted)`, `transform: translate(var(--drag-x, 0), var(--drag-y, 0)) rotate(-1deg)`, a stacking context above its siblings, `transition` on `--motion-lift` with `--motion-easing-standard`; the settle on `--motion-settle` with `--motion-easing-exit`. `.drop-zone` — `--border-emphasis-width` dashed `--focus-ring`, `color: var(--focus-ring)`, `border-radius: var(--radius-md)`, no fill, no fixed height (`No_box_is_given_a_fixed_height`; size by padding / `min-block-size` from `--space-*` or the target minimum).
  - [x] For movers' cards only (a class the card renders when `CanMove`): `touch-action` per Q9, `-webkit-touch-callout: none` and `user-select: none` on the card body (not on the title text's accessibility — selection only), `cursor: grab` / `grabbing` while lifted. No `:hover` rule reveals or hides anything (`No_rule_reveals_or_hides_anything_on_hover_alone`).
  - [x] Reduced motion: base.css's universal reset already removes every transition/animation (`base.css:280-288`, gated by `Reduced_motion_neutralises_every_transition_and_animation`); add **no** `!important` motion rule. The lifted look (border, shadow, rotation) is state and still shows (Q14).
  - [x] Logical properties only (`No_physical_left_or_right_property_is_used_where_a_logical_one_exists`); `transform-origin` with no left/right keyword. RTL mirroring of drag direction is 2.11's (UX-DR42) — the hit-testing works from rects, so it is direction-neutral.

- [x] **Task 5 — Gates that break on purpose, and the ledger's gate** (AC: 10, 12; `deferred-work.md:36, 52`)
  - [x] Amend deliberately, by exactly the lifted card, never loosen (record each in the Debug Log, as 2.6 did):
    1. `DesignFoundationGateTests.No_surface_carries_a_shadow` (`:936`) — allow exactly one rule: selector `.task-card.task-card--lifted`, value `var(--shadow-lifted)`; any other shadow still fails. Update its remarks (they say widening is 2.7's job).
    2. `DesignFoundationGateTests.Board.The_Task_card_declares_no_shadow_explicitly` (`:63`) — the base `.task-card` still declares `none`; the `--lifted` compound is the only exception.
    3. `DesignFoundationGateTests.MovePicker.Only_the_moved_cards_reflow_moves_and_on_the_quick_motion_token` (`:91`) — `.task-card` motion values may also be `var(--motion-lift)` / `var(--motion-settle)` on the lifted compound; still no literal duration. Rename if its name stops being true.
    4. `BoardTests.cs:142` ("The drag handle arrives in 2.7") and E2E-003 — extend the Viewer absence assertion to the drag (AC10).
  - [x] New gates (partial `DesignFoundationGateTests.Drag.cs`), each proved by a plant: the lifted card takes the 2px emphasis border, the shadow token and `rotate(-1deg)`; the drop zone is dashed, `focus-ring`, `--radius-md`, has no `background`; the lifted card and drop zone take radius 3px (closes the drop-zone clause of `deferred-work.md:36`).
  - [x] **Ledger `:52` (opacity)** — Q8: add the "cheap interim" gate the entry names: fail any rule that both references a colour token and declares `opacity` below 1 (and any alpha colour in a rule outside the token layer is already refused by `No_colour_is_stated_as_a_literal_outside_the_token_layer`). Plant `opacity: 0.7` on `.task-card--lifted` → caught. Close the entry naming the gate, or re-own it to 7.5 if Q8 decides otherwise.
  - [x] `ClientStorageBanTests.No_client_asset_reaches_browser_storage` and `SpaceSwitcherGateTests` (document.title) now scan a real `.js` file — must stay green unchanged.
  - [x] Update `DocumentLanguage.cs:20-24` remarks ("zero JavaScript of its own") to say what is true now — no review archaeology.

- [x] **Task 6 — Browser tests** (AC: 1-15)
  - [x] `tests/Yello.Tests.Client` against `FakeYelloApi` and its 2.6 move harness (`HoldTaskMoveAsync`, `ReleaseTaskMove`, `ReleaseTaskMoveRefused`, `ReleaseTaskMoveUnanswered`, `NextTaskMoveRefusal`). Same `ClientSurfaceCollection`, **no new fixture class**; extend `ClientSurfaceFixture.OpenAsync` with an optional `hasTouch` (and viewport) overload (Q14) — the browser is private today (`ClientSurfaceFixture.cs:73-79`).
  - [x] New `DragTests.cs`. Pointer via `page.Mouse.DownAsync` / `MoveAsync(x, y, new() { Steps = n })` / `UpAsync`; touch via a CDP session (`context.NewCDPSessionAsync(page)` → `Input.dispatchTouchEvent` `touchStart`/`touchMove`/`touchEnd`, Chromium only) per Q14. Cases (hold → assert → release → assert, `TESTING-CONVENTIONS.md:246-252`):
    - Pointer drag across columns: mid-drag the card carries `task-card--lifted` with computed 2px border, the shadow and a rotation, a `.drop-zone` (dashed) sits at the target; drop with the move **held** → card already in the new slot, both totals moved, the POST body is `{statusId, afterTaskId}` with the expected neighbour and no `positionKey`; release → "Moved to …, position n of N." (AC1, AC9).
    - Reorder within a column: top, between two, bottom (`afterTaskId` null / neighbour / last) (AC9).
    - Drop on the origin slot and release outside every column → no POST recorded, card in place, totals unchanged (AC6, Q5); cancel after lift announces "Returned to …, position n." (AC14).
    - `Esc` mid-drag → nothing moves, no POST (AC13).
    - Refused (`403`, `409`, `400`), unanswered and `404` drops follow 2.6's outcomes, with the refusal words on the Board notice (AC9, Q6); `409` re-reads the Board.
    - A press without movement opens Task detail; a drag never opens it or the menu on release (AC11).
    - Touch: long-press (start, wait until the card reports the lifted state — poll a `data-drag` state attribute or the class, **never `Task.Delay`** as the assertion) → lifted → move → drop → POST (AC2); movement > slop before the threshold in each of four directions (including purely horizontal at 1024px wide, where the Board scrolls horizontally) → never lifts, no POST, and the Board/column scrolled (AC3, AC4); movement ≤ slop (jitter, e.g. 4px) still lifts (AC3 dead zone); a second touch point mid-drag cancels (AC13); a long-press lift opens no card menu (AC11).
    - Auto-scroll: a column with more cards than fit; hold the drag near its bottom edge → its `scrollTop` increases (poll); near the viewport's inline edge at 1024px → `.board__columns.scrollLeft` increases (AC5).
    - Reduced motion (`EmulateMediaAsync(ReducedMotion.Reduce)`): during and after a drag every `.task-card` and `.drop-zone` reports `transitionDuration`/`animationDuration` 0 and no `animationName`; the lifted border/shadow still present (AC7, Q14 decision).
    - Viewer: mouse drag and long-press on a card → no lift, no drop zone, no POST, no `touch-action`/`cursor: grab` on the card, `main [disabled], [aria-disabled='true']` count 0 (AC10). Plant: the drag attached for a Viewer → caught.
    - Breakpoints 320/768/1280: the Move and actions controls are still visible without hover with the drag module attached (AC8, AC10).
    - Nothing announced on lift, on crossing columns or on a pan-cancelled pending lift (AC14).
  - [x] `TextSpacingTests`: a lifted card and drop zone under the 1.4.12 override and 200% text-only zoom clip nothing (the `deferred-work.md:20` convention: each new surface adds its case).
  - [x] Rebuild `Yello.Client` in **Debug** before every browser run and plant. Never Playwright's URL wait; poll. `blur()` and reset focus before keyboard cases.

- [x] **Task 7 — Plants, runs, ledger, status** (AC: all)
  - [x] Plants one at a time; original held in memory and restored in a `finally`; tree hash compared after each; **never** `git checkout --`, `git restore`, `git reset` or `git stash`. Each plant compiles. Record `| # | Plant | Gate | Caught |`. Minimum set: a second rule carrying a shadow; `opacity: 0.7` on the lifted card; a literal duration on the lift; the drop zone filled; the slop check single-axis (vertical only) → the horizontal-pan case; a zero-tolerance slop → the jitter case; the lift committed before the threshold (timer removed) → the pan case; the drag attached for a Viewer; the click after a drag not suppressed → AC11; the drop sending a `PATCH` or a key → AC9's body case; origin drop sending a POST → AC6.
  - [x] Runs (CLAUDE.md): `dotnet build Yello.slnx` (0 warnings, 0 errors), `dotnet build-server shutdown`, `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. A Client failure seen only in the full run is rerun alone (`dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build`) before it is called real. Docker readiness `docker info --format '{{.ServerVersion}}'`; `Get-Process Yello.Host` if `bin/Debug` is locked.
  - [x] `tests/TESTING-CONVENTIONS.md`: how a pointer drag and a CDP touch gesture are driven; the "poll the drag state, never sleep" rule for the long-press threshold.
  - [x] Ledger (`deferred-work.md`): CLOSE `:52` per Q8; UPDATE `:36` (drop zone and lifted card radius clause added); NOTE on the 2.8 entries (identical announcements; neighbour still unadmitted) that drags repeat both; ADD with owners anything decided against the spines (Q3 drag-start distance for a mouse, Q7 drop zone copy and the missing pick-up sentence, Q9 "active lift" reading — owner **UX**); the tap-to-select-then-place fallback if touch tests badly (`.memlog.md:21`, owner **UX/Lee**); anything deferred.
  - [x] `sprint-status.yaml`: `review` at the end of the dev phase. **Do not add review narration to source files.**

### Review Findings

Code review 2026-10-28 (diff `0e54a8c..211c388`; Blind Hunter, Edge Case Hunter, Acceptance Auditor). 38 raw findings, 16 after triage, 8 dismissed.

- [x] [Review][Decision] **Resolved by Lee: (a).** The lifted card is now `position: fixed` at the box it lifted from (`--drag-top`, `--drag-start`, `--drag-width`, written by the module at the lift) and translated from there. Its origin slot closes, and the drop zone alone marks the destination. `DragTests` asserts by `elementFromPoint` that the card in hand is drawn over the destination column; planting `position: relative` back makes that case fail. Original finding: The lifted card is clipped by its origin column once dragged into another column — `.board-column` has `overflow-y: auto` (`components.css:665`), so its `overflow-x` computes to `auto` and it clips both ways. The card moves only by `translate` and stays inside its origin column, and `position: relative; z-index: 1` cannot escape an overflow clip. So the card in your hand vanishes at the origin column's edge on every cross-column drag (AC1, AC12, UX-DR22). It also feeds a runaway auto-scroll in the origin column: the translated card adds to the column's scrollable overflow, so scrolling near the bottom edge keeps growing the scroll range into empty space. The tests read computed style only, and `TextSpacingTests` deliberately leaves the lifted card out of its clip check. Options: (a) while lifted the card is `position: fixed` at viewport coordinates, with its width from a custom property the module writes, so it leaves the flow and its origin slot closes (the drop zone marks the destination); (b) the module draws a drag image (a clone) outside Blazor's tree, and the origin card stays in its slot; (c) the origin column drops its overflow clip while lifted, which loses its own scroll and auto-scroll.
- [x] [Review][Decision] **Resolved by Lee: (a).** The words are `text-primary`, and the dashed border stays `focus-ring`. The gate asserts both, and the variance from DESIGN.md:199 is recorded in `deferred-work.md` for UX. Original finding: The drop zone's "Drop here" words fail 4.5:1 in the light theme — DESIGN.md:199 gives the drop zone `foreground: {colors.focus-ring}`. `focus-ring` on `surface-column` is 3.97:1 light (`ColorTokenContrastTests.cs:241`), and it is gated only as non-text (3:1). The words are visible `type-meta` text. `aria-hidden` does not exempt visible text from WCAG 1.4.3, so this conflicts with NFR-9. Options: (a) keep the dashed `focus-ring` border and set the words in `text-primary`, recording the variance against DESIGN.md:199 for UX; (b) drop the words and keep a border-only zone with a minimum block size; (c) ship as DESIGN.md says and record the NFR-9 conflict for UX.
- [x] [Review][Patch] `AttachAsync` can run twice when renders overlap, can attach after dispose, and lets an import failure escape `OnAfterRenderAsync` [`Yello.Client/Components/BoardDrag.cs:70-83`]
- [x] [Review][Patch] The click and context-menu suppression flags outlive the drag. The next keyboard or assistive-technology click with no `pointerdown` before it is swallowed app-wide [`Yello.Client/wwwroot/js/board-drag.js:145-151, 423-439, 475-478`]
- [x] [Review][Patch] Esc during a pending mouse press ends the press, but the release still opens Task detail [`Yello.Client/wwwroot/js/board-drag.js:398-405`]
- [x] [Review][Patch] A right-click during a mouse drag opens 2.6's card menu while the drag continues [`Yello.Client/wwwroot/js/board-drag.js:432-439`]
- [x] [Review][Patch] Board-side interruptions do not end a pending lift. A `Lifted` already on its way when `End()` runs leaves a lifted card with no gesture behind it (AC13) [`Yello.Client/Components/BoardDrag.cs:108-138`]
- [x] [Review][Patch] A drop into a column the Board no longer has, or after a demotion, ends with no "Returned to…" and no settle (AC14) [`Yello.Client/Components/BoardView.Drag.cs:74-78`]
- [x] [Review][Patch] The lifted-look assertions read `rotate` and `box-shadow` while the 110ms lift transition may still be running [`tests/Yello.Tests.Client/DragTests.cs:79-85`]
- [x] [Review][Patch] `TextSpacingTests` patches its script with `string.Replace`, and nothing checks that the replacement happened [`tests/Yello.Tests.Client/TextSpacingTests.cs`]
- [x] [Review][Patch] The pan-cancel case never asserts that nothing is announced (AC14) [`tests/Yello.Tests.Client/TouchDragTests.cs`]
- [x] [Review][Patch] Stale heading: "No JavaScript file, and no new dependency." sits above a paragraph that now names one [`Yello.Client/Localisation/DocumentLanguage.cs:20`]
- [x] [Review][Patch] The `PasswordDurationCollection` remark is run narration, and the File List does not record the change [`tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/PasswordVerificationDurationTests.cs:267-271`]
- [x] [Review][Defer] Every animation frame forces layout and measures every column and every card in the hovered column [`Yello.Client/wwwroot/js/board-drag.js:263-359`] — deferred to story 2.9 (the 5,000-Task Board)
- [x] [Review][Defer] Board-side cancels after a lift (purge, re-read, the Task deleted, the panel opened) say nothing, unlike AC14's "Returned to…" [`Yello.Client/Components/BoardDrag.cs:108-118`] — deferred to story 2.8 with UX (announcement grammar)
- [x] [Review][Defer] Three other duration-measuring Slices classes sit outside a `DisableParallelization` collection [`tests/Yello.Tests.Slices/Accounts/RegisterAccount/RegistrationDurationTests.cs`, `SessionEndpointTests.cs`, `ActiveSpaceEndpointTests.Tokens.cs`] — deferred, pre-existing; owner the Epic 2 retrospective

## Dev Notes

### The shape of the story in one paragraph

2.6 built everything a move needs: the route, the key generator, the optimistic replica with revert, the announcements, the Board re-read on a `409`, the Move control and the card menu. 2.7 adds **only the gesture** that chooses a `MoveChoice` and hands it to `BoardView.MoveAsync`: a pointer drag (press, move past a small distance, lift, drop) and a touch long-press (320ms, 10px axis-agnostic slop, pan wins), a lifted card (the one shadow in the product), a dashed drop zone, auto-scroll, cancellation, haptics where available, and reduced motion. The hard parts are browser-side event handling that Blazor's event system cannot do (pointer capture, non-passive `touchmove`, `preventDefault` decided mid-gesture, a rAF scroll loop), keeping that JS from fighting Blazor's DOM diffing, not breaking click-to-open and 2.6's context menu, and testing a long-press and a touch pan deterministically.

### What exists today (read before writing)

- **There is no `src/` folder.** Production projects sit at the repo root; tests in `tests/`.
- **The move entry point** — `BoardView.razor.cs` `private Task MoveAsync(Guid taskId, MoveChoice choice, bool fromMenu)` (`:254-275`): returns if `!_canManage`; clears `_boardNotice`; sets `_movedCard` (the reflow class); `fromMenu` adds the Task to `_movesFromMenu` (refusal words go to the Board notice) and calls `FocusCardTitle(taskId)`; then `EditAsync(taskId, TaskAttribute.Status, choice)` — a move is the Status attribute's change, so it queues behind a Status edit of the same Task. **With `fromMenu: false` and the panel not open on that Task, a refusal is said nowhere** (`SayInPanel`, `:769-772`) — hence Q6.
  - `MoveChoice(Guid StatusId, Guid? AfterTaskId)` → `ToRequest()` → `MoveTaskRequest` (`MoveChoice.cs:11, 18`).
  - Optimistic placement: `Place` → `PlaceAfter` (`:861-881`) inserts at `FindIndex(after)+1`; a neighbour no longer shown gives index 0 (top). `Relocate` (`:887-905`) and `InsertByKey` (`:907-911`, ordinal) put a card back by its admitted key.
  - Send: `SendAsync` (`:555-593`) — `Writes.SendAsync(HttpMethod.Post, TaskRoutes.MoveOf(SpaceId, taskId), body, key, token)`, key reused only for an identical resend after no answer; `_deleted` → `EditOutcome.Gone`.
  - Settle: `UseAnswerAsync` admits Status + `PositionKey` on `200` (`:626-640`, `:980`); `404` closes the panel and raises `OnRefused` (`:647-659`); `AfterRefusalAsync` sets `_rereadBoard` for `task-position-unavailable` (`:675-686`) and `RereadBoardAsync` runs once the queue drains (`:538-542`; `BoardView.razor:330-354`).
  - Announce: `AnnounceMove` (`:293-318`) — `TaskMoved` "Moved to {0}, position {1} of {2}." / `TaskReturned` "Returned to {0}, position {1}." (`ClientCopy.resx:594-599`); live region `BoardView.razor:49` exists empty from first render; the Board notice `p.board__notice role=status` (`BoardView.razor:72`).
  - Focus: `FocusCardTitle` (`:281-286`), `_arrivalCard`; `CloseMenu` (`:206-216`).
- **`TaskCard.razor`** — `<li class="@CardClass" @oncontextmenu=… @oncontextmenu:preventDefault="@CanMove" @onkeydown=…>` (`:32-35`); for movers a head with the title button (`button.task-card__open`, `data-task-id`) and `button.task-card__actions` ("Actions for {title}", `aria-haspopup="menu"`); a Viewer gets only the title button and `default` callbacks (no handlers, `:165-169`). `@MenuContent` renders inside the `<li>` (`:81`). ContextMenu / Shift+F10 open the menu, Esc closes (`:227-232`). **No pointer or touch handler anywhere.** The `<li>` has no data attributes.
- **`BoardColumn.razor`** — `<section class="board-column" tabindex="-1">` (`:23`), header (name, `column-count`, "+"), `<ul class="board-column__cards">` **only when `Tasks.Count > 0`** (`:38-55`) — an empty column has no list to drop into. No `data-status-id`.
- **`MovePicker.razor`**, **`TaskMenu.razor`** — unchanged by 2.7; a drag never starts inside them.
- **JS** — `Yello.Client/wwwroot` has **no `.js` file**: `index.html` (one inline `history.scrollRestoration` script, `:121`; empty importmap `:51`), three stylesheets, configs. Interop today is two global calls: `DocumentLanguage.ApplyAsync` (`Localisation/DocumentLanguage.cs:55-56`, remarks `:20-24` say "zero JavaScript of its own and no bundler") and `navigator.clipboard.writeText` (`AccountSettings.razor:389`). No `IJSObjectReference`, no `JSImport`. **No CSP** anywhere (Host, `staticwebapp.config.json`), so a same-origin module loads.
- **Tokens** (`tokens.css:335-342`): `--motion-instant: 90ms`, `--motion-quick: 120ms`, `--motion-lift: 110ms`, `--motion-settle: 120ms`, `--motion-easing-standard: cubic-bezier(0.2, 0, 0.3, 1)`, `--motion-easing-exit: cubic-bezier(0.4, 0, 1, 1)`, `--motion-long-press-threshold: 320ms`, `--motion-long-press-slop: 10px` — the last four are declared and unused. `--border-emphasis-width: 2px` (`:310`, comment `:291-292` names 2.7). **No shadow token.** Breakpoints are in `em`; the only one is `(min-width: 48em) and (max-width: 79.9375em)` (`components.css:605`).
- **Layout** — `.board__columns { display:flex; overflow-x:auto }` (`components.css:644-649`) is the horizontal scroll container; `.board-column { inline-size:17rem; max-block-size:75vh; overflow-y:auto }` (`:657-671`) scrolls vertically; `.task-card { box-shadow:none }` (`:738-747`); 2.6's reflow `.task-card.task-card--moved { animation: task-card-reflow var(--motion-quick) … }` (`:821-834`). No `cursor`, `touch-action` or `user-select` rules.
- **Reduced motion** — `base.css:280-288`: `*, *::before, *::after { transition:none !important; animation:none !important; scroll-behavior:auto !important }` under `prefers-reduced-motion: reduce`. Static `transform`/`box-shadow` survive it — the lifted *look* stays, only its motion goes.
- **Design gates** that bind this story (`tests/Yello.Tests.Architecture`): `No_surface_carries_a_shadow` (`DesignFoundationGateTests.cs:936`), `The_Task_card_declares_no_shadow_explicitly` (`.Board.cs:63`), `Only_the_moved_cards_reflow_moves_and_on_the_quick_motion_token` (`.MovePicker.cs:91`), `No_rule_reveals_or_hides_anything_on_hover_alone` (`.MovePicker.cs:67`), `Pixel_lengths_outside_the_token_layer_come_from_a_token` (`:775` — px only on radius outside tokens.css), `No_colour_is_stated_as_a_literal_outside_the_token_layer` (`:612` — `rgba()` only in tokens.css), `Reduced_motion_neutralises_every_transition_and_animation` (`:1498` — no other `!important` on motion), `No_physical_left_or_right_property_is_used_where_a_logical_one_exists` (`:1576`), `No_box_is_given_a_fixed_height` (`:1646`), `Only_the_four_radius_values_are_used` (`:1132`), `The_non_colour_token_families_are_complete` (`:1700`), `Every_token_reference_resolves_to_a_declared_property` (`:1935`), `Every_stylesheet_is_linked_by_the_host_page` (`:1842`). JS-scanning gates: `ClientStorageBanTests.No_client_asset_reaches_browser_storage` (`:96`, scans `.js`), `SpaceSwitcherGateTests` (`document.title`, scans `.js`). Not gated: rotation, dashed borders, `transform`.
- **Client test fixture** — `ClientSurfaceFixture.OpenAsync(locale, timezoneId)` creates `NewContextAsync({ Locale, TimezoneId })` on a private headless Chromium (`:73-79, :100-103`); **no `HasTouch`, no viewport option**. Viewports are set per page (`SetViewportSizeAsync(width, 900)`); reduced motion by `EmulateMediaAsync`. No test uses `Mouse.DownAsync`/`UpAsync` or touch today. `FakeYelloApi` move harness: `HoldTaskMoveAsync` (`:433`), `ReleaseTaskMove` (`:446`), `ReleaseTaskMoveRefused` (`:454`), `ReleaseTaskMoveUnanswered` (`:461`), `NextTaskMoveRefusal` (`:199`), `MovePathOf` (`:470`); it places after the named neighbour. 2.6's `MoveTests.cs` (17 cases) is the model.
- **Preserve** every 2.3/2.5/2.6 review fix: the Board re-read on `409`; `_movedCard` cleared on settle; the card-menu toggle; the Board notice as a status region; the client sending the `MoveTaskRequest` contract; a refusal for one Task never shown in another's panel; `InFlight` cleared only after admission; `5xx` is no answer; the stale focus request cleared; the pointer-blocking layer under the panel.

### Open questions (decide in Task 1; each reversible; record as "Qn - (x)")

1. **How the gesture is implemented.** Blazor's `@onpointer*` events cannot call `setPointerCapture`, cannot `preventDefault` a `touchmove` conditionally mid-gesture (only a static `:preventDefault`), register `touchmove` as passive, and would cross into .NET on every move. Options: (a) one small ES module `wwwroot/js/board-drag.js` (vanilla Pointer/Touch Events, no package), loaded with `IJSRuntime` `import`, calling back through a `DotNetObjectReference` only on lift / slot change / drop / cancel; **the module writes only CSS custom properties on the lifted card and never mutates Blazor-rendered structure or classes** — Blazor renders the lifted class and the drop zone from the state the module reports; (b) Blazor events only — cannot meet AC3/AC4 on touch; (c) a drag library (SortableJS, interact.js) — a new dependency needs Lee's approval (CLAUDE.md), and those libraries move DOM nodes under Blazor. **Recommend (a).** Cost: the repository gains its first JS file (update `DocumentLanguage.cs` remarks); the JS-scanning gates now have a real input; 2.8 may reuse the module's state for its keyboard pick-up visuals.
2. **Where the threshold and slop come from.** Options: (a) read `--motion-long-press-threshold` / `--motion-long-press-slop` from computed style at attach time (the tokens stay the single source, DESIGN.md:506); (b) constants in JS. **Recommend (a)**, falling back to 320 / 10 if unparsable. Cost: none.
3. **Mouse drag-start distance.** No document specifies one for the pointer path (slop is defined for touch only); without one, every click with a 1px wobble becomes a drag and click-to-open breaks (AC11). Options: (a) reuse `--motion-long-press-slop` (10px) as the mouse/pen drag-start distance — one tolerance in the product; (b) a smaller conventional 4–5px; (c) start on press. **Recommend (a).** Cost: ledger note to UX (the spine names the token for touch only).
4. **What is grabbable.** EXPERIENCE `:376` and DESIGN `:419` say the lift target is the whole card; UX-DR27 says a Viewer has no "drag handle" — no separate handle element is specified. Options: (a) the whole card for movers, including the title button, excluding the actions control and anything inside the open menu/picker; the Viewer's "no drag handle" means no drag at all; (b) a dedicated grip element — a third control in the card, contradicts the spine. **Recommend (a).** Cost: none; 2.8's grid is unaffected (keyboard pick-up is its own).
5. **A drop on the current slot.** Options: (a) treated as "back to origin": nothing sent, nothing re-rendered, announced as a cancel after lift (Q7); (b) send it and let 2.6 Q18 answer `200` with nothing written. **Recommend (a)** — AC6 says nothing moves, and a request would announce a "move". "Current slot" = same Status and the same predecessor excluding the dragged Task. Cost: none.
6. **Where a dropped move's refusal and focus go.** `MoveAsync`'s `bool fromMenu` couples two things. Options: (a) replace it with an origin enum `MoveOrigin { Panel, Menu, Drag }`: `Drag` says refusals on the Board notice (as `Menu`) and **does not move focus** (a pointer user's focus is wherever they left it; a card-menu move moves it because the menu was keyboard-reachable); a refused drag puts nothing into focus either; (b) call with `fromMenu: true` (focus jumps to the title on every drop — surprising for a pointer, and a touch focus ring appears); (c) a second bool. **Recommend (a)**; update 2.6's two call sites mechanically. Cost: none.
7. **Announcements and copy.** UX-DR25/UX-DR38 say every "pick-up, move and drop" announces; 2.7 has no announcement AC and 2.6 owns the sentences. Options: (a) drop → 2.6's admission sentence; refusal → 2.6's return sentence; cancel **after** a lift → "Returned to {Status}, position {n}." (the `TaskReturned` key); **no pick-up sentence** for pointer/touch (the pick-up copy is defined with the keyboard pick-up in 2.8, which a screen-reader user actually uses; announcing a mouse lift adds noise); pending-lift cancel by pan → nothing; (b) also announce "Picked up {title}." on lift now. **Recommend (a)**; ledger to UX/2.8 to confirm whether pointer lifts should adopt 2.8's pick-up sentence. Drop zone copy: the mockup shows "Drop here" (`board.html:170-171`) — **recommend** a resx entry "Drop here" with `<comment>`, rendered `aria-hidden="true"` (the live region carries the meaning; the visual zone is decoration). Cost: two resx entries at most.
8. **The shadow token and the opacity ledger entry (`:52`).** DESIGN `:145-148` gives the shadow as `0 6px 0 rgba(0,0,0,.6)` — an alpha colour, which may only live in the token layer. Options: (a) `--shadow-lifted` in `tokens.css` (one token, both themes — DESIGN gives one value); keep the lifted card and drop zone fully opaque (no `opacity`, no alpha background), so every computed contrast pair stays true; add the ledger's "cheap interim" gate (a rule referencing a colour token may not declare `opacity` < 1) and close `:52`; (b) leave `:52` open for 7.5 (presence, which DESIGN animates by opacity, `DESIGN.md:517`) — then the gate must exempt presence later. **Recommend (a)**, with the gate scoped so 7.5 can widen it deliberately (presence animates opacity on a dot, not on text). Cost: 7.5 reads the gate.
9. **"Scroll or pan intent wins over a pending **or active** lift" (AC4).** EXPERIENCE `:304`/UX-DR23 say *pending*; the epic says *pending or active*. After a touch lift commits, a moving finger **is** the drag, so "pan wins" cannot mean every movement. Options: (a) pending: any movement > slop or any native scroll start (`pointercancel` because the browser claimed the gesture) cancels; active: a native scroll the browser starts anyway (`pointercancel`/`touchcancel`), a second finger (pinch/two-finger pan), or a wheel/trackpad scroll during a mouse drag is honoured (the page/column scrolls, the drag re-hit-tests, nothing is prevented) and, for touch, ends the drag with nothing moved; the drag never calls `preventDefault` on `wheel`; (b) the literal reading — any movement ends an active lift (makes dragging impossible). **Recommend (a)**; ledger note to UX for the reading. Touch mechanics: the card keeps `touch-action: pan-x pan-y` (native pans start freely during the pending window); on lift, the module `preventDefault`s non-passive `touchmove` so the browser does not pan. Verify in Chromium touch emulation that a pan begun within the slop before the threshold still scrolls. Cost: none.
10. **`Esc` during a pointer drag.** Specified only for keyboard pick-up (`epics.md:1271, 1282-1284`). **Recommend** `Esc` cancels (innermost meaning wins), with the cancel announcement of Q7. Cost: none.
11. **The long-press vs the context menu.** On Android Chrome a long-press fires `contextmenu`; 2.6's card opens `TaskMenu` on `contextmenu` and prevents the native one (`TaskCard.razor:32-35`); iOS shows a callout and text selection. Options: (a) for `pointerType === "touch"`, the module swallows the `contextmenu` that a long-press raises (capture phase) while a lift is pending or active, CSS `-webkit-touch-callout: none; user-select: none` on movers' cards; a mouse right-click still opens 2.6's menu; touch users reach the menu by the visible actions control (unchanged); (b) open the menu on long-press and drag only on long-press-then-move — conflicts with UX-DR23's "long-press to lift". **Recommend (a).** Cost: none; record that 2.6's `contextmenu` path is pointer-only on touch devices.
12. **Haptics.** "where the platform offers it". **Recommend** `if (navigator.vibrate) navigator.vibrate(10)` on a touch lift only (Android Chrome; iOS Safari has no Vibration API and is a silent no-op). Not tested beyond "never throws" (stub `navigator.vibrate` in the page and assert it was called once on a touch lift). Cost: none.
13. **Auto-scroll parameters.** Unspecified anywhere. **Recommend** an edge zone of 2 × `--target-min` (`tokens.css:266`, 24px → 48px; read from computed style or derived from the token), speed linear in depth up to ~16 CSS px per frame, `requestAnimationFrame`, `scrollBy({ behavior: "instant" })` (reduced motion unaffected — it is direct manipulation, not an animation). Column first, then the Board (inline edges) and page (block edges). Cost: ledger note to UX if they want different numbers.
14. **Testing touch and reduced motion.** Playwright .NET's `Touchscreen` only taps. Options: (a) `HasTouch = true` on the context (fixture overload) and a CDP session (`Input.dispatchTouchEvent`) for start/move/end — Chromium only, which is the only browser the suite runs (`TESTING-CONVENTIONS.md:187-217`); synthesise `PointerEvent`s with `pointerType: "touch"` only if CDP proves unreliable, and record it; (b) unit-test the JS state machine in isolation — no JS test runner exists (a new tool needs approval). **Recommend (a).** For the long-press: start the touch, **poll** the card's lifted state (no sleep as the oracle); for "pan before threshold": assert the pending state, move beyond slop at once, assert never lifted over a poll window ≥ 2 × threshold. Long-press cases measure time, so put them in a `DisableParallelization` collection if the Client suite runs classes in parallel (CLAUDE.md: timing tests). Reduced motion: the lifted look stays (state), only transitions go — assert both. Cost: one fixture overload.
15. **Below 768px.** 2.11 has not built the pager; today all columns render side by side at any width. **Recommend** 2.7 does not restrict cross-column drags by width; 2.11 limits drag to the visible column when it introduces the pager (UX-DR41). Cost: 2.11 reads this.

### Architecture compliance (must)

- **One write path** (orchestrator instruction; AD-15; FR-35): the drag ends in `BoardView.MoveAsync` → `POST TaskRoutes.MoveOf` with `MoveTaskRequest`. No new route, no new Contracts type, no `PATCH` with a Status for a drag, no key from the client, no server change.
- AD-11/AR-20: optimistic, never blocking on the network; revert to the last admitted slot.
- UX-DR27: a Viewer gets no drag — no listener, no class, no cursor — not a disabled drag.
- UX-DR21: the Move control and the actions control are never removed, hidden or reordered because a drag exists.
- Client: nothing in browser storage (`ClientStorageBanTests` scans the new `.js`); `document.title` stays `Yello`; every string externalised in `ClientCopy.resx` with a `<comment>`, never quoted in a C# comment (`No_user_visible_copy_is_written_as_a_csharp_string_literal`); logical properties; no `!important` motion rule.
- JS never mutates Blazor-owned structure (Q1) — Blazor's diff would undo it or throw on a node it no longer recognises.

### Library / framework

No new packages (CLAUDE.md). .NET 10.0.11, Blazor WebAssembly 10 (`IJSRuntime.InvokeAsync<IJSObjectReference>("import", …)`, `IJSObjectReference.DisposeAsync`, `DotNetObjectReference.Create`, `[JSInvokable]`; `JSDisconnectedException` is Server-only but catch `JSException`/`ObjectDisposedException` on dispose), Microsoft.Playwright 1.62.0 (`Mouse.DownAsync/MoveAsync(x, y, new MouseMoveOptions { Steps })/UpAsync`, `BrowserNewContextOptions.HasTouch`, `IBrowserContext.NewCDPSessionAsync(page)` + `SendAsync("Input.dispatchTouchEvent", …)`, `EmulateMediaAsync`), xunit.v3 4.0.0. Web platform: Pointer Events (`setPointerCapture`, `pointercancel` when the browser claims a pan), `touch-action`, non-passive `touchmove`, `navigator.vibrate` (absent on iOS Safari), `requestAnimationFrame`, `Element.scrollBy`.

### Testing standards (from `tests/TESTING-CONVENTIONS.md` and CLAUDE.md)

- Add cases to existing suites — `tests/Yello.Tests.Client` (browser), `tests/Yello.Tests.Architecture` (CSS/JS gates). **No new test project, no container-backed class** (the Client suite already has its one engine; nothing here needs SQL).
- Traits: only `Suite`, `Priority`, `Requirement`, `Assumption` (`Requirement=UX-DR22`, `UX-DR23`, `UX-DR6`, `FR-29`). Sentence-style names, one behaviour per test.
- No `Task.Delay` as an oracle; hold the fake's answer with a `TaskCompletionSource` (`:244`); optimistic writes tested hold → assert → release → assert (`:246-252`).
- Every absence assertion is proved against a plant. Rebuild `Yello.Client` in Debug before browser runs and plants (`:253-257`).
- Full runs with `--max-parallel-test-modules 1`; never two `dotnet test` at once; a Client failure only in the full run is rerun alone first (memory: the full run starves the browser suite).

### Previous story intelligence (2.6, 2.5, 2.3)

- **2.6's surprises were gates that pin exact sets** (`The_Task_creation_request_carries_…`, the card's one-control count in `LabelTests`). Before adding a class or attribute to the card, search `tests/Yello.Tests.Client` and `tests/Yello.Tests.Architecture` for exact counts of controls/classes on `.task-card` (`BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow`, `LabelTests.A_card_shows_three_chips_in_server_order_then_plus_N`). A drop zone is not a control (no tabindex, `aria-hidden`), so control counts should hold — verify.
- **Analyzer S1200** forced 2.6 to split `TaskMoveEndpoints` and `TaskMenu`; `BoardView` is at its cap — put drag state in its own type.
- **2.6 review fixes to preserve** (see "Preserve" above); the 2.6 review's "a 409 never re-read the Board" bug is the class of thing a drag will hit first (dropping after a neighbour that moved) — AC9's `409` case covers it.
- **Markup in `@code` lambdas and bare `else` lines tripped `No_user_visible_*` gates** in 2.6; keep markup in the template.
- 2.3: live regions exist empty from first render; 2.4: build in Release only if an orphaned `Yello.Host.exe` holds `bin/Debug` — the browser suite still needs Debug.
- Environment: Rancher Desktop; on "timed out dialing Hyper-V socket" run `rdctl shutdown`, `wsl --shutdown`, relaunch, poll readiness (CLAUDE.md).

### Git intelligence

Each story lands as "Add story → Add ATDD scaffolds → implement → Close the code review" (2.6: `d898527`, `8b9033c`, `4a7e65b`, `0e54a8c`). `4a7e65b` touched `BoardView.razor(.cs)` (+286), `TaskCard.razor`, `BoardColumn.razor`, `MovePicker.razor`, `TaskMenu.razor`, `MoveChoice.cs`, `components.css` (+110), `ClientCopy.resx`. Expect 2.7's footprint client-only: one JS module, one drag-state type, `BoardView`/`BoardColumn`/`TaskCard`, `tokens.css`, `components.css`, `ClientCopy.resx`, `DocumentLanguage.cs` remarks, `ClientSurfaceFixture.cs`, `DragTests.cs`, `TextSpacingTests.cs`, `BoardTests.cs`, design gates, `TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml`.

### Ledger entries

- `deferred-work.md:52` (owner "most likely 2.7") — opacity/alpha vs computed contrast: **decide (Q8) and close or re-own to 7.5**.
- `:36` (owner epic 2, each surface) — radius mapping: **add the drop zone and lifted card clause**.
- The "Two identical announcements in a row are not re-announced" entry (owner 2.8) — drags repeat "Moved to …"; **note**.
- The 2.6-review entry "The Move picker offers a neighbour whose own move is still unadmitted" (owner 2.8) — a drag after an unadmitted drag hits the same `409`; **note**.
- The 2.6 entry "Same-gap key growth … the position list at 5,000 Tasks" (owner 2.9) — a drag can only target loaded cards; **note**.
- New (Task 7): Q3/Q7/Q9 readings (UX); the tap-to-select-then-place fallback if touch tests badly (`.memlog.md:21`, UX/Lee).

### Project Structure Notes

- New: `Yello.Client/wwwroot/js/board-drag.js`; `Yello.Client/Components/BoardDrag.cs` (or similar, Q1); `tests/Yello.Tests.Client/DragTests.cs`; `tests/Yello.Tests.Architecture/DesignFoundationGateTests.Drag.cs`.
- Updated: `BoardView.razor(.cs)` (attach/cancel, `MoveOrigin`), `BoardColumn.razor`, `TaskCard.razor` (the panel's `OnMove` call site is `BoardView.razor:134`, Q6), `tokens.css`, `components.css`, `ClientCopy.resx`, `Localisation/DocumentLanguage.cs` (remarks), `tests/Yello.Tests.Client/ClientSurfaceFixture.cs`, `BoardTests.cs`, `TextSpacingTests.cs`, `DesignFoundationGateTests.cs`, `.Board.cs`, `.MovePicker.cs`, `tests/TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml`.
- Not touched: `Yello.Host`, `Yello.Application`, `Yello.Domain`, `Yello.Infrastructure`, `Yello.Contracts`, migrations, `IsolationCases.cs`.
- Variances to record: the mouse drag-start distance (Q3); no pointer pick-up sentence (Q7); the "active lift" reading of AC4 (Q9); the long-press swallowing `contextmenu` on touch (Q11); the mockup's 1px lifted border (DESIGN wins, 2px).

### References

- `_bmad-output/planning-artifacts/epics.md:1211-1251` (story), `:129, :162` (FR-29, NFR-9), `:208-209` (AR-20, AR-21), `:237, :239-241, :246, :262-267, :271, :282, :285-286` (UX-DR2, 4-6, 8, 21-27, 38, 41, 42), `:359-364` (coverage), `:437`, `:971`, `:1035`, `:1253-1293` (2.8), `:1386-1428` (2.11)
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md:526-534, 605, 753-757`; `addendum.md:105, 142`
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md:44, 138-142, 162-168, 277`
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md:47, 125-148, 197-200, 374-376, 387, 419, 435, 452, 466, 482, 506-521`; `EXPERIENCE.md:199, 283-308, 315, 322-324, 330, 335, 376-377, 390, 414-415, 437`; `mockups/board.html:71-73, 162, 170-171`; `.memlog.md:21`; `review-accessibility.md:49, 130`
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md:910, 974-976`; `sprint-change-proposal-2026-09-13.md` (nothing on drag)
- `_bmad-output/specs/spec-yello/SPEC.md:149-150`; `decisions-settled.md:73, 85`
- `_bmad-output/test-artifacts/test-design-qa.md:183, 354-355, 367, 369, 419-421` (no drag scenario exists; X-4 is 2.8's, X-9 absence applies); `test-design-progress.md:626`
- `_bmad-output/implementation-artifacts/2-6-move-and-reorder-a-task-by-explicit-control.md` (Q1-Q9, Q18; review findings `:247-266`; Debug Log); `deferred-work.md:20, 36, 52` and the 2.5/2.6 entries at the end of the file
- `tests/TESTING-CONVENTIONS.md:187-257`; `CLAUDE.md` (memory budget, timing collections, undo rule, new dependencies)

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), run by the unattended story-cycle orchestrator.

### Debug Log References

**Create-story phase (2026-10-28).** No isolation row is pending on 2.7 (grepped `tests/Yello.Tests.Isolation`). No ATDD scaffold exists for 2.7. Planning inputs read through two research passes (the planning documents; the client code, CSS, gates and browser fixture), plus story 2.6 and `deferred-work.md`; facts above cite paths and lines as read on 2026-10-28. No test design scenario names drag, touch or reduced motion for the Board (`test-design-qa.md`), so the browser cases in Task 6 are this story's own.

**Dev phase (2026-10-28).** Baseline: `dotnet build Yello.slnx` 0 warnings, 0 errors at `ba2354c`; last full run 1185 total / 1182 passed / 0 failed / 3 skipped (2.6 code review). Isolation rows with `PendingUntil: "2.7"`: none (grepped `tests/Yello.Tests.Isolation`). Docker 29.5.3 (Rancher) ready.

Red first: `DragTests` and `TouchDragTests` were written before the client changes; `A_pointer_drag_lifts_marks_the_drop_zone_and_moves_the_Task_through_the_move_route` failed on the lift (no `.task-card--lifted` within 30s), then went green with the module.

Findings while building:
- **Blazor lost the drop zone's attributes** when one `@key`ed `<li>` moved between the loop branch and the trailing branch (`<li>Drop here</li>` with no class or `aria-hidden`), so a zone moved to the top of a column was invisible to the tests. The zone is now unkeyed and drawn afresh at each slot.
- **Chromium's own touch slop hides a single-axis slop rule.** A touch move beyond Chromium's touch slop starts a native pan (or a `pointercancel`) on any axis, so a 30-80px pan never lifts even with the drag's check reduced to the vertical axis (plant 5 first run: not caught). `A_move_just_beyond_the_slop_in_any_direction_never_lifts` moves 13px - beyond the drag's 10px, inside Chromium's - and catches it.
- Gates widened by exactly the lifted card (as 2.6 recorded its own):
  1. `No_surface_carries_a_shadow` admits exactly two declarations: `--shadow-lifted` in `tokens.css`, and `box-shadow: var(--shadow-lifted)` on `.task-card.task-card--lifted` (`IsTheLiftedCardsShadow`). Remarks rewritten. Plant 1 (a second rule taking the token) caught.
  2. `The_Task_card_declares_no_shadow_explicitly` - the base `.task-card` still declares `none`; the lifted compound taking the token is the only exception.
  3. `Only_the_moved_cards_reflow_moves_and_on_the_quick_motion_token` renamed `The_cards_motion_reflows_on_the_quick_token_and_states_no_literal_duration` (the old name stopped being true); its assertions are unchanged and now also hold the lift and the settle to tokens. Plant 3 caught.
  4. `The_non_colour_token_families_are_complete` - `shadow-lifted` added to `RequiredStructuralTokens`.
  5. `BoardTests.A_Viewer_sees_the_same_Board_with_no_create_affordance_and_nothing_disabled` (E2E-003) extended: Owner cards are `task-card--movable` with the grab cursor; Viewer cards are neither and keep `touch-action: auto`.
- `ClientStorageBanTests.No_client_asset_reaches_browser_storage` and `SpaceSwitcherGateTests` now scan `board-drag.js`; both green unchanged (remarks of the storage gate updated: the client has one JS file now).

Plants (one at a time, original held in memory and restored in a `finally`, SHA-256 of each planted file compared after restore; never `git checkout`/`restore`/`reset`/`stash`; every plant compiled; `Yello.Client` rebuilt in Debug before each browser plant and after the set):

| # | Plant | Gate | Caught |
|---|---|---|---|
| 1 | `.drop-zone` takes `box-shadow: var(--shadow-lifted)` | `No_surface_carries_a_shadow` | yes |
| 2 | `opacity: 0.7` on `.task-card.task-card--lifted` | `No_rule_makes_an_element_carrying_a_colour_token_translucent` | yes |
| 3 | `110ms` for `var(--motion-lift)` on the lift | `The_lift_and_the_settle_run_on_their_motion_tokens`, `The_cards_motion_reflows_on_the_quick_token_and_states_no_literal_duration` | yes |
| 4 | Drop zone filled (`background-color: var(--surface-column)`) | `The_drop_zone_is_dashed_in_the_focus_ring_colour_and_never_filled` | yes |
| 5 | Slop check single-axis (vertical only) | `A_move_just_beyond_the_slop_in_any_direction_never_lifts` (the two horizontal rows) | yes (not caught by the 80px pan cases - see findings) |
| 6 | Zero-tolerance slop | `Jitter_inside_the_slop_still_lifts` | yes |
| 7 | Lift committed before the threshold (timer of 0) | `A_long_press_lifts_after_the_threshold_...` and all four `A_pan_beyond_the_slop_...` rows | yes |
| 8 | Drag attached for a Viewer (module attached for every Role, every card a handle) | `DragTests.A_Viewers_card_does_not_drag_and_nothing_is_disabled`, `TouchDragTests.A_long_press_on_a_Viewers_card_lifts_nothing` | yes |
| 9 | Drag attached for a Viewer, module only (no grab cursor; lift allowed without manage-tasks) | `A_long_press_on_a_Viewers_card_lifts_nothing` (the lift itself) | yes |
| 10 | The click after a drag not suppressed | `Escape_mid_drag_cancels_it` (released back on the title) | yes |
| 11 | The drop sent as a `PATCH` of the Status | `A_pointer_drag_lifts_marks_the_drop_zone_and_moves_the_Task_through_the_move_route` | yes |
| 12 | A drop on the origin slot sends a `POST` | `Dropping_on_the_origin_or_outside_every_column_moves_nothing(outside: False)` | yes |

**Runs.** `dotnet build Yello.slnx` 0 warnings, 0 errors; `dotnet build-server shutdown`; `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`: **1230 total, 1227 passed, 0 failed, 3 skipped** (the three migration-bundle skips). +45 against the baseline: `DragTests` 24, `TouchDragTests` 13, `TextSpacingTests` 2, `DesignFoundationGateTests.Drag` 6.

### Completion Notes List

- Story created 2026-10-28 by create-story: ultimate context engine analysis completed — comprehensive developer guide created. Open questions Q1–Q15 carry recommended decisions; the dev phase decides each as a reversible, recorded decision.
- **Q1 - (a).** One hand-written ES module, `Yello.Client/wwwroot/js/board-drag.js` (no bundler, no package), imported lazily by `BoardDrag` through `IJSRuntime` `import` and disposed with the Board; it writes only `--drag-x`/`--drag-y` on the dragged card and never re-classes or restructures Blazor's DOM. Cost to later stories: the repository has its first JS file; 2.8 may reuse `BoardDrag`'s state for its pick-up visuals. Cut by deleting the module, `BoardDrag.cs`, `IBoardDragHost.cs` and `BoardView.Drag.cs`.
- **Q2 - (a).** `--motion-long-press-threshold`, `--motion-long-press-slop` and `--target-min` are read from the root's computed style at **each press** (not once at attach), falling back to 320ms / 10px / 24px. Reading per press is what lets a test lengthen the threshold with a style tag. Cost: none.
- **Q3 - (a).** The mouse/pen drag-start distance is the same 10px slop token. Cost to later stories: ledger note to UX.
- **Q4 - (a).** The whole card is the handle for movers (`task-card--movable`), the title button included; the actions control and anything in an open menu or picker never start a drag; a lift closes an open card menu. Cost: none.
- **Q5 - (a).** A drop on the Task's own slot (same Status, same neighbour above, the dragged Task excluded) sends nothing and is announced as a cancel after lift. Cost: none.
- **Q6 - (a).** `MoveAsync(…, bool fromMenu)` became `MoveAsync(…, MoveOrigin)` (`Panel`, `Menu`, `Drag`); 2.6's two call sites updated mechanically. `Drag` says a refusal on the Board notice (as `Menu`) and moves no focus. Cost: none.
- **Q7 - (a).** Drop → 2.6's admission sentence; refusal → 2.6's return sentence; cancel after a lift → "Returned to {Status}, position {n}." (`TaskReturned`); nothing on the lift, on crossing columns or on a pan-cancelled pending lift; no pointer pick-up sentence. Drop zone copy is one resx entry `DragDropHere` ("Drop here", with `<comment>`), rendered `aria-hidden`. A cancel the Board makes itself (purge, re-read, the dragged Task deleted, the panel opened) is silent. Cost: ledger note to UX/2.8.
- **Q8 - (a).** `--shadow-lifted: 0 6px 0 rgba(0, 0, 0, 0.6)` in `tokens.css` (one value, both themes); the lifted card and drop zone fully opaque; the ledger's interim gate added (`No_rule_makes_an_element_carrying_a_colour_token_translucent`) and `deferred-work.md:52` closed. Cost: 7.5 widens the gate for presence.
- **Q9 - (a).** Pending: a move beyond the slop or a native scroll (`pointercancel`) cancels; active: the finger is the drag (its `touchmove`s are prevented), while a `pointercancel`, a second touch, `Esc`, window blur or a hidden page end it with nothing moved; a wheel scroll during a mouse drag is honoured and the drag re-hit-tests. Cards keep `touch-action: pan-x pan-y pinch-zoom` (pinch-zoom kept for resize, WCAG 1.4.4). The non-passive `touchmove` listener sits on the row of columns, registered at attach, because the browser decides cancelability when the touch starts. Cost: ledger note to UX.
- **Q10.** `Esc` cancels a pending or lifted drag (innermost meaning wins), announced as a cancel after lift when lifted. Cost: none.
- **Q11 - (a).** While a touch press is pending or lifted, and after a touch lift until the next press, `contextmenu` is swallowed at window capture; movers' cards carry `-webkit-touch-callout: none` and `user-select: none`. A mouse right-click still opens 2.6's menu. Cost: ledger note (2.6's `contextmenu` route is pointer-only on touch).
- **Q12.** `navigator.vibrate(10)` on a touch lift only, feature-detected. Asserted once per lift with a stub. Cost: none.
- **Q13.** Edge zone 2 × `--target-min` (48px), speed linear to 16px a frame, `scrollBy({ behavior: "instant" })` in a rAF loop: the hovered column at its block edges, the row of columns at the view's inline edges, the page at the view's block edges. Cost: ledger note to UX.
- **Q14 - (a).** `ClientSurfaceFixture.OpenAsync(locale, timezoneId, hasTouch)` overload; touch through CDP `Input.dispatchTouchEvent`. CDP proved reliable (native pans scroll), so no synthetic `PointerEvent`s. Long-press cases run in a new `TouchTimingCollection` (`DisableParallelization = true`) that takes the existing `ClientSurfaceFixture` - no new fixture class. Reduced motion keeps the lifted look (state) and removes every transition/animation; both asserted. Cost: none.
- **Q15.** No width restriction on cross-column drags; 2.11 limits it with the pager. Cost: 2.11 reads the ledger entry.
- **Variances recorded:** the tilt is the CSS `rotate: -1deg` property rather than `transform: rotate(-1deg)` (DESIGN's value, a different property), so the lift's transition animates the tilt and the shadow and never the pointer-following `translate`; the drop zone and lifted card are told apart from a card by class, the column by `data-status-id` (the card keeps its title button's `data-task-id`, not a second one on the `<li>`, so no existing `[data-task-id]` query becomes ambiguous); DESIGN's 2px lifted border is used, not the mockup's 1px.
- **What was built:** the module (pointer and touch paths, hit-testing by rects with the drop zone holding its slot, auto-scroll, cancel paths, click and context-menu suppression); `BoardDrag` (attach/detach for movers only, four `[JSInvokable]` callbacks, `End()` for the Board's own cancels) behind `IBoardDragHost`; `BoardView.Drag.cs` (lift guard, drop → `MoveAsync(…, MoveOrigin.Drag)` with `afterTaskId` computed without the dragged Task, origin drop, cancel announcement, settle); `BoardColumn` (`data-status-id`, the drop zone at its slot, the list drawn for an empty column under a drag); `TaskCard` (`task-card--movable`, `--lifted`, `--settled`); CSS (the movable card, the lifted card, the settle keyframes, the drop zone) and the shadow token. No server, Contracts, migration or isolation change; the drop goes through 2.6's `POST …/move`.
- **Tests added:** `DragTests` (24 cases), `TouchDragTests` (13 cases incl. the 13px slop theory), `TextSpacingTests.Text_spacing_and_text_only_zoom_clip_nothing_mid_drag` (2), `DesignFoundationGateTests.Drag` (6), and the extended E2E-003; 12 plants recorded in the Debug Log.

### File List

- `Yello.Client/wwwroot/js/board-drag.js` (new)
- `Yello.Client/Components/BoardDrag.cs` (new)
- `Yello.Client/Components/IBoardDragHost.cs` (new)
- `Yello.Client/Components/BoardView.Drag.cs` (new)
- `Yello.Client/Components/BoardView.razor`
- `Yello.Client/Components/BoardView.razor.cs`
- `Yello.Client/Components/BoardColumn.razor`
- `Yello.Client/Components/TaskCard.razor`
- `Yello.Client/Localisation/ClientCopy.resx`
- `Yello.Client/Localisation/DocumentLanguage.cs`
- `Yello.Client/wwwroot/css/tokens.css`
- `Yello.Client/wwwroot/css/components.css`
- `Yello.Client/wwwroot/css/base.css`
- `tests/Yello.Tests.Client/DragHarness.cs` (new)
- `tests/Yello.Tests.Client/DragTests.cs` (new)
- `tests/Yello.Tests.Client/TouchDragTests.cs` (new)
- `tests/Yello.Tests.Client/TouchTimingCollection.cs` (new)
- `tests/Yello.Tests.Client/ClientSurfaceFixture.cs`
- `tests/Yello.Tests.Client/BoardTests.cs`
- `tests/Yello.Tests.Client/TextSpacingTests.cs`
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.Drag.cs` (new)
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs`
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.Board.cs`
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.MovePicker.cs`
- `tests/Yello.Tests.Architecture/ClientStorageBanTests.cs`
- `tests/Yello.Tests.Slices/Accounts/AuthenticateAccount/PasswordVerificationDurationTests.cs` (moved into `PasswordDurationCollection`, `DisableParallelization = true`, during dev-phase verification under the CLAUDE.md timing-test rule; outside the story's scope)
- `tests/TESTING-CONVENTIONS.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/2-7-the-accelerated-pointer-and-touch-paths.md`

### Change Log

- 2026-10-28: Dev phase - the pointer drag and the touch long-press path (UX-DR22, UX-DR23, UX-DR6) through one ES module and `BoardDrag`; the drop is 2.6's move (`MoveOrigin.Drag`); the lifted card (the one shadow, `--shadow-lifted`) and the drop zone; gates widened by exactly the lifted card; the opacity gate closes `deferred-work.md:52`; 12 plants caught; full suite 1230/1227/0/3. Status → review.
- 2026-10-28: Code review. 38 raw findings, 16 after triage: 2 decisions (Lee chose (a) for both), 11 patches applied, 3 deferred, 8 dismissed.
  - The lifted card is now fixed to the view at its lift box, so its column no longer clips it, and the runaway auto-scroll is gone. The JS module also writes `--drag-top`, `--drag-start` and `--drag-width`, and the origin slot closes.
  - The drop-zone words are `text-primary`.
  - Attach and detach are serialised, which covers renders that overlap, disposal and a failed import.
  - Click suppression ends once the input has been dispatched, or at the next key.
  - Esc before a lift opens nothing on release.
  - A right-click mid-drag opens no menu.
  - `End()` cancels a pending lift, and a late lift is reported back as abandoned.
  - A drop on a missing column announces the return.
  - Tests: an `elementFromPoint` visibility check (the plant was caught), a wait for the lift transition, a keyboard Enter after a touch drag, Esc before the lift, a pan-cancel that says nothing, and a check that the `TextSpacingTests` replacement applied.
  - Full suite: 1232 total, 1229 passed, 0 failed, 3 skipped. Status → done.
