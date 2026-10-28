---
baseline_commit: f2e4f72
---

# Story 2.8: Operate the whole Board from the keyboard

Status: ready-for-dev

Epic: 2 — Track your own work on a Board
Story key: `2-8-operate-the-whole-board-from-the-keyboard`
Requirements owned (Story Coverage Index, `epics.md:361`): **UX-DR24** (the keyboard model: `Tab`/`Shift+Tab`, `↑`/`↓` within a column, `←`/`→` between columns by logical direction, sticky origin index with clamping, `Space` pick-up scoped to a Task card, landing ordinal, `Esc` innermost, application-mode composite widget, not the conformance path — `epics.md:265`, `EXPERIENCE.md:310-326`), **NFR-9** (WCAG 2.1 AA; "every Board operation available by pointer is available by keyboard, including moving a Task between columns" — `prd.md:753-757`, `epics.md:162`). Test-design scenario **X-4** (P1, E2E: "every Board pointer operation has a keyboard equivalent | R16 | 6 operations incl. cross-column move", `test-design-qa.md:355`; `test-design-progress.md:626` still says P0 — the qa doc's reclassification at `:337-341` is the later decision).
Also binding: **FR-29** (the move, built by 2.6 — `prd.md:528`), **UX-DR21** (the Move control is canonical and stays operable in screen-reader browse mode; `role="application"` is not the answer — `epics.md:262`, `EXPERIENCE.md:283-295`), **UX-DR25** (every pick-up, move and drop announces in a specified shape — `epics.md:266`, `EXPERIENCE.md:322-324`), **UX-DR38** (announce on deliberate action only, never on load or windowing; no cross-Space information — `epics.md:278`, `EXPERIENCE.md:390`), **UX-DR39/UX-DR40** (focus return, focus always visible, Tab follows reading order, trapped in dialogs — `epics.md:279-280`), **UX-DR42** (arrow keys mirror under RTL, the Status sequence does not — `epics.md:282`, `EXPERIENCE.md:437`), **UX-DR27** (a Viewer has no pick-up: absent, not disabled), **AD-29** (focus follows Task identity, never a recycled row; `aria-setsize`/`aria-posinset` — spine `:249-258`), **AD-11/AD-15/FR-35** (optimistic replica; a move writes one key between two concrete neighbours; position not writable over the API).
Explicitly **not** here: paging, windowing and focus identity *under paging* (**2.9**, X-5); the <768px Status pager and making the `Space` carry inert behind it (**2.11**, UX-DR20, `epics.md:1411-1414`); RTL as a product locale (no RTL locale exists — `BrowserLocales.All`); description-editor `Esc` (**Epic 7**); axe-core reports (X-3, blocked on a dependency decision — open question 13). **No server, Contracts, migration or isolation change**: a keyboard drop is 2.6's `POST /api/v1/spaces/{spaceId}/tasks/{taskId}/move` via `BoardView.MoveAsync` — there is no second write path.
Ledger entries owned by this story (`deferred-work.md`): `:489` ("Task added." not re-announced), `:496` (column count chip a bare number), `:513` (one tab stop per card), `:517` (Status picker, Move picker and card menu not the listbox/menu keyboard pattern), `:535` (Label picker: Space not prevented), `:549` (two identical announcements in a row — with the 2.6 and 2.7 notes), `:564` (the spurious `409` for a neighbour whose own move is unadmitted — with the 2.7 note), `:569` (the 2.8 half: whether pointer lifts adopt the pick-up sentence; "Drop here" copy stays UX's), `:579` (Board-side cancels after a lift say nothing). Touched: `:130` (the keyboard-parity matrix is unwritten — written here; the axe half stays open), `:525` ("+N" stays text; nothing to place). Code hooks naming 2.8: `TaskCard.razor:28`, `TaskMenu.razor:11`, `LabelOption.razor:8`, `tests/Yello.Tests.Client/ClientSurfaceCollection.cs:12`.
Depends on: **2.2** (Board, columns, cards, live region), **2.3** (panel, focus trap, pickers, replica), **2.5** (column focus target), **2.6** (`MoveAsync`, `MoveChoice`, `PlaceAfter`, Move picker, card menu, announcements, re-read on `409`), **2.7** (`BoardDrag`, drop zone rendering via `DropIndex`, `MoveOrigin`, `board-drag.js`), all done.

> **Isolation gates.** 2.8 maps no route, so 1.9-GATE-002 is untouched and no isolation row is owed. No row is pending on 2.8 (grepped `tests/Yello.Tests.Isolation` for `PendingUntil` 2026-10-28: none). If the ATDD phase adds one (it should not), GATE-003 requires it live before `review`.

## Story

As someone who does not use a pointer,
I want every Board operation available from the keyboard, including moving a Task between columns,
so that the Board is usable rather than merely visible.

## Acceptance Criteria

AC1–AC8 are reproduced **verbatim** from `epics.md:1261-1293` (`status: final`), numbered by position. AC9–AC17 come from the UX spines, the architecture spine, the ledger and 2.6/2.7's built behaviour; each cites its source. Where an AC's reading needs a decision, the open question that decides it is named.

**AC1 — traversal**
**Given** the Board
**When** `Tab` and `Shift+Tab` are used
**Then** focus moves between columns and controls in reading order; `↑` and `↓` move between Tasks within a column; `←` and `→` move between columns by **logical** direction, so they mirror under RTL exactly as the layout does
*(Reading of "Tab between columns" against AC7's single tab stop: open question 2.)*

**AC2 — sticky origin index**
**Given** a traversal run across columns of differing lengths
**When** `←` or `→` is pressed repeatedly
**Then** column position is preserved by sticky origin index with clamping — the index the run started at is remembered and clamped to each column's length, rather than re-derived per hop

**AC3 — pick-up grammar**
**Given** a focused Task card
**When** `Space` is pressed
**Then** the Task is picked up; `←` and `→` move it between columns, `↑` and `↓` within one, `Space` drops it and `Esc` cancels

**AC4 — Space keeps its meaning elsewhere**
**Given** focus anywhere other than a Task card
**When** `Space` is pressed
**Then** it keeps its native meaning — activating a button, or paging a scroll container — because Board columns *are* scroll containers and an unscoped rebinding would break both

**AC5 — landing ordinal**
**Given** a carried Task entering a column
**When** it lands
**Then** it lands at the same ordinal clamped to that column's length, with `↑` and `↓` adjusting before the `Space` commit
**And** the destination is never left unspecified, because a fractional index is computed between two concrete neighbours and without them the implementation has no pair to work from

**AC6 — Esc, innermost first**
**Given** any surface
**When** `Esc` is pressed
**Then** the innermost meaning wins — a pick-up is cancelled, or the topmost dialog closes

**AC7 — composite widget, not the conformance path**
**Given** the arrow grammar
**When** the Board is implemented
**Then** it is an application-mode composite widget — a single tab stop with `role="grid"` or equivalent, managing focus internally
**And** the Move control from Story 2.6 remains fully operable in screen-reader browse mode, which is precisely why the arrow grammar is not the conformance path and `role="application"` is not used to force it
*(Which "equivalent": open question 1.)*

**AC8 — parity**
**Given** every Board operation available by pointer
**When** the keyboard is used
**Then** an equivalent exists for each, including moving a Task between columns

**AC9 — one write path: the keyboard drop is 2.6's move** *(FR-29, FR-35, AD-11, AD-15; 2.6 Q2 "2.8's keyboard move sends the same body"; orchestrator instruction)*
**Given** a carried Task dropped by `Space` on a slot that differs from its current one
**When** it commits
**Then** the client calls `BoardView.MoveAsync(taskId, new MoveChoice(statusId, afterTaskId), MoveOrigin.Keyboard)` — `afterTaskId` the Task immediately above the slot in the destination column **excluding the carried Task**, `null` for the top — sent as `POST TaskRoutes.MoveOf(...)` with `MoveTaskRequest`, never a key, never a `PATCH`
**And** it is optimistic exactly as a picker or drag move is: rendered at once, both totals moved, re-placed by the server's key on `200` and announced "Moved to {Status}, position {n} of {total}."; a refusal reverts, says the refusal on the Board notice and announces "Returned to {Status}, position {n}."; `409 task-position-unavailable` re-reads the Board; no answer says "Not sent.", reverts and keeps the key; `404` follows the Board's refusal surface
**And** a drop on the Task's own slot sends nothing and announces the return (2.7 Q5's rule)
**And** focus stays on the moved Task's card, by Task **identity**, wherever the card is drawn next (AD-29)

**AC10 — the pick-up and the carry announce** *(UX-DR25, UX-DR38; `EXPERIENCE.md:322-324, 390`; open question 5)*
**Given** a keyboard pick-up
**When** the Task is picked up, carried one slot by an arrow, dropped, or cancelled
**Then** the polite live region says, in order: on pick-up the pick-up sentence (Q5); on each carried step the step sentence naming the Status, the ordinal and the total the column would have (Q5); on the drop's admission 2.6's "Moved to …" sentence, on its refusal or on `Esc` 2.6's "Returned to …" sentence
**And** nothing is announced on plain traversal (a focus move is read by the screen reader from the focused element itself), on load, or on any re-render

**AC11 — absent for a Viewer** *(UX-DR27; `epics.md:271, 1035`)*
**Given** a Viewer's Board
**When** the keyboard is used
**Then** traversal (`Tab`, arrows, `Enter` to open) works exactly as for a Member, `Space` on a card keeps its native meaning (it activates the title button, which opens the Task), no pick-up, carried state, drop zone or move request ever occurs, and nothing anywhere is disabled

**AC12 — the card is one stop; its controls stay reachable** *(`deferred-work.md:513`; UX-DR21, UX-DR24; 2.6 Q6 (a); open question 3)*
**Given** a mover's card holding focus
**When** the keyboard is used
**Then** `Enter` opens Task detail, the platform context-menu key and `Shift+F10` open the card menu, and the card's actions control is reachable from the keyboard (Q3); the Move control in Task detail and the card menu's "Move" are unchanged and stay operable in browse mode (no `role="application"`, no `aria-hidden` on them, no key swallowed while focus is outside the Board's composite)

**AC13 — the column is named and counted for assistive technology** *(`deferred-work.md:496`; AD-29; open question 8)*
**Given** a column
**When** it is read by assistive technology
**Then** its name is the Status and its description carries the true total with its unit ("3 Tasks", "1 Task", "0 Tasks"), never a bare number
**And** each card's list item states `aria-posinset` (its 1-based ordinal) and `aria-setsize` (the column's true total) (Q8)

**AC14 — repeated sentences are announced again** *(`deferred-work.md:489, 549`; open question 6)*
**Given** a live-region sentence identical to the one before it — "Task added.", "Task deleted.", "Project deleted.", "Label deleted.", "Moved to …", "Returned to …", a carry step
**When** it is said again
**Then** the region's content changes so that assistive technology announces it again (Q6), on the Board, on Space home's Project list and in Space settings' Labels section

**AC15 — the pickers and the card menu follow their patterns** *(`deferred-work.md:517, 535`; WAI-ARIA APG listbox and menu; open question 7)*
**Given** the Status picker, the Label picker, the Move picker (both homes) or a card's menu
**When** it is used from the keyboard
**Then** it is one tab stop with `↑`/`↓` moving between options or items (wrapping per Q7) and `Home`/`End` to the first and last; `Enter` and `Space` choose (or toggle) and `Space` never scrolls the page; `Esc` keeps its 2.3/2.6 meaning (step back, close, focus to the invoker); nothing is pre-selected (UX-DR11) — focusing an option is not selecting it

**AC16 — chained moves do not refuse themselves** *(`deferred-work.md:564`; open question 9)*
**Given** Task A's move still unadmitted and Task B then placed directly after A (by keyboard, picker or drag)
**When** B's move is sent
**Then** it is not refused `409` merely because the server has not yet admitted A there (Q9); if A's move is refused, B's outcome follows 2.6's rules

**AC17 — a carry ended by the Board** *(`deferred-work.md:579`; 2.7 AC13; open question 10)*
**Given** a keyboard carry or a pointer/touch lift
**When** the Board ends it itself — a re-read of the Board, the carried Task deleted, the panel opened, the Role lowered, a purge or disposal
**Then** nothing is moved and nothing is sent; for a re-read, a deletion or a lowered Role the return is said per Q10, and a purge or disposal says nothing (the Space has gone)

## Tasks / Subtasks

- [ ] **Task 1 — Baseline, then decide the open questions** (AC: all)
  - [ ] `dotnet build Yello.slnx` (0 warnings, 0 errors). Record the baseline from the 2.7 code review: **1232 total, 1229 passed, 0 failed, 3 skipped**, or re-measure at the ATDD commit.
  - [ ] List any isolation row with `PendingUntil: "2.8"` in the Debug Log (expected: none).
  - [ ] Decide Q1–Q4 before Task 2, Q5–Q10 before Task 4, Q11–Q14 before Task 7. Record each in Completion Notes as "**Qn - (x).** … Cost to later stories: …". Each is reversible.

- [ ] **Task 2 — Traversal as pure logic, unit-tested** (AC: 1, 2, 5)
  - [ ] A small pure type in `Yello.Client/Components/` (e.g. `BoardTraversal.cs`, no Blazor dependency): given the columns' Task ids in order, the focused position (Task id, or an empty column), a key (`Up`, `Down`, `InlineStart`, `InlineEnd`), and the run's sticky origin index, return the next position and the updated origin. Rules: `↑`/`↓` clamp at the ends (no wrap) and **reset** the origin to the new index; `←`/`→` keep the origin, land at `min(origin, count-1)`, land on the **column itself** when it is empty (Q4), and stop at the first/last column; any non-arrow key or any focus change not made by the traversal resets the run.
  - [ ] Logical direction: the module (Task 3) maps physical keys to `InlineStart`/`InlineEnd` from the Board's computed `direction` — `ArrowRight` is `InlineEnd` in LTR and `InlineStart` in RTL. The Status sequence never reverses (it is data, UX-DR42).
  - [ ] Carry uses the same type: the carried Task is excluded from the destination's count, and entering a column lands at `min(origin, others.Count)` — `others.Count` is valid (the bottom slot), so an empty column takes slot 0 (AC5).
  - [ ] Unit tests in `tests/Yello.Tests.Architecture` (the only suite that sees `Yello.Client` types, `TESTING-CONVENTIONS.md:34-42`): sticky origin over columns of 5, 1, 0, 3 (origin 4 → 0 → column → 3, then back → 4); `↑`/`↓` at ends; RTL mapping; carry landing and clamping; the carried Task excluded.

- [ ] **Task 3 — The Board's keyboard composite** (AC: 1, 3, 4, 6, 7, 11, 12)
  - [ ] Per Q1/Q2: the Board is one roving tab stop. `BoardView` owns `_activeTaskId` (or the active empty column), rendered as `tabindex="0"` on exactly one element of the composite (the active card's title button, or an empty column's `<section>`) and `tabindex="-1"` on every other card title (and per Q3, the actions controls). Blazor renders every `tabindex`; JS never writes one (2.7's rule: the module never mutates what Blazor renders).
  - [ ] The active element follows focus: a `focusin` inside the row of columns (Blazor `@onfocusin`, or reported by the module) makes the focused card the active one — so a click, a panel close (`FocusCardTitle`), a menu move's arrival and a column focus after deletion all keep exactly one stop. On first render the active card is the first card of the first non-empty column, or the first column when the Board is empty.
  - [ ] Per Q11: a keyboard module (one ES module, no package) attached to the row of columns for **every** Role (a Viewer traverses too), separate from the drag's attach, which stays movers-only (UX-DR27; 2.7's Viewer gates must stay green unchanged). On `keydown` whose target is a card title (or the active empty column): `ArrowUp/Down/Left/Right` are `preventDefault`ed (so the column does not scroll) and reported to .NET with the Board's `direction`; `Space` is `preventDefault`ed **only for a mover's card** (so the button does not also click — prevent on `keydown`, and swallow the matching `keyup` activation if the browser still clicks) and reported; `Escape` is reported **only during a carry** and then `preventDefault`ed + `stopPropagation`ed. Every other key, and every key whose target is anything else (the actions control, the "+", an open menu or picker, the panel), is untouched (AC4).
  - [ ] .NET (`[JSInvokable]`, e.g. on a `BoardKeys` holder beside `BoardDrag` — `BoardView` is at the S1200 dependency cap, as 2.6 and 2.7 found) moves focus with the existing request pattern (`FocusCardTitle` / `_focusCardRequests` / `FocusColumnRequest`) — by Task identity, never by element (AD-29). Focus moving into view scrolls the column and the Board (`focus()` does; add `scrollIntoView({ block: "nearest", inline: "nearest" })` only if a case shows it does not).
  - [ ] `Enter` on a card keeps opening Task detail (2.3 Q7, 2.6 Q6); `ContextMenu`/`Shift+F10` keep opening the card menu (`TaskCard.CardKeyDownAsync`).
  - [ ] Remove the "two tab stops per card until story 2.8" sentence from `TaskCard.razor`'s header comment and say what is true now; likewise `TaskMenu.razor:11`, `LabelOption.razor:8`, `ClientSurfaceCollection.cs:12`.

- [ ] **Task 4 — Pick-up, carry, drop, cancel** (AC: 3, 5, 6, 9, 10, 11, 16, 17)
  - [ ] Carry state in .NET (`BoardKeys` or a `_carry` record: Task id, origin Status and slot, target Status and slot excluding the carried Task, sticky origin index). Rendering per Q12: reuse `BoardColumn.DropIndex` to draw the drop zone at the target slot, and mark the carried card (a `task-card--carried` class, per Q12) — never the pointer's `task-card--lifted` (that one is `position: fixed` at coordinates only the drag module writes).
  - [ ] `Space` on a mover's focused card with no carry → pick up (only if `_canManage`, no panel open, no menu open, no drag lifted, the Task on the Board; a lift in progress refuses the pick-up and vice versa). Announce the pick-up sentence (Q5).
  - [ ] During a carry: arrows step the target via `BoardTraversal` (AC5), announce the step sentence (Q5), keep focus on the carried card and bring the drop zone into view (`scrollIntoView({ block: "nearest" })` on the zone, through the module); `Tab` ends the carry as a cancel (Q4) — focus never leaves with a Task in hand; `Enter`, `ContextMenu`, `Shift+F10` do nothing while carrying.
  - [ ] `Space` → drop: same slot as now (same Status, same neighbour above, the carried Task excluded) → nothing sent, "Returned to …" (2.7 Q5); otherwise `MoveAsync(…, MoveOrigin.Keyboard)` with `afterTaskId` computed exactly as `IBoardDragHost.DropAsync` does (`BoardView.Drag.cs:83-85`) — **extract that computation into one shared helper** used by both. `MoveOrigin.Keyboard`: refusal words on the Board notice (as `Menu`/`Drag`), and focus follows the card by identity to its optimistic slot, its admitted slot and, on refusal, its returned slot (as `Menu` does with `FocusCardTitle`).
  - [ ] `Esc` → cancel: nothing sent, "Returned to {Status}, position {n}.", focus on the card in its slot.
  - [ ] Board-side ends (AC17, Q10): `RereadBoardAsync`, `Deleted`, `Open` (panel), a Role re-read that removes `_canManage` (`OnParametersSet`), `Purge`, `DisposeAsync` — each ends a carry exactly where they already call `_drag?.End()`; say the return per Q10.
  - [ ] Pointer/keyboard exclusion: while a carry is active the drag cannot lift (`IBoardDragHost.CanLift` refuses), and a pick-up is refused while `_drag.LiftedTaskId` is set.

- [ ] **Task 5 — Chained moves (AC16) and the announcement model (AC14)** (AC: 9, 14, 16)
  - [ ] Per Q9: in `EditAsync`/`SendInTurnAsync` for a `MoveChoice` whose `AfterTaskId` names a Task whose own Status attribute is in flight or queued (`ReplicaOf(after).HasOutstanding(TaskAttribute.Status)`), the send waits until that Task's Status attribute settles (admitted, refused, unanswered or gone); the optimistic placement still renders at once. No timeout as the mechanism; a refusal of the neighbour's move leaves B's move to go and be answered as 2.6 rules. Covers picker, drag and keyboard moves alike (one write path).
  - [ ] Per Q6: one shared live-region component (e.g. `Yello.Client/Components/LiveAnnouncement.razor`), empty from first render (the 2.3 rule), that guarantees each `Say(text)` changes what assistive technology hears even when `text` equals the previous sentence. Replace the Board's `<p role="status">@_announcement</p>` and the regions in `ProjectList.razor` and `LabelSection.razor` with it. The Board notice (`board__notice`) and the panel's refusal regions are not announcements of repeated actions and stay as they are.
  - [ ] Keep "Board loaded." exactly as it is (UX owns its conflict with `EXPERIENCE.md:390`, `deferred-work.md:498`).

- [ ] **Task 6 — The pickers and the card menu** (AC: 15)
  - [ ] `PickerOption.razor`, `LabelOption.razor`, `MovePicker.razor`, the Status and Label `<ul role="listbox">` in `TaskDetail.razor`, `TaskMenu.razor`: one tab stop each (roving `tabindex`, or `aria-activedescendant` on the list — Q7), `↑`/`↓`/`Home`/`End`, `Space` and arrows `preventDefault`ed through the keyboard module's helper (Blazor's `:preventDefault` is static and would also swallow `Tab`), `Enter`/`Space` choose. `TaskMenu`: `role="menuitem"` items, `↑`/`↓` between them, focus on the first on open (unchanged).
  - [ ] Preserve: no option pre-selected (`aria-selected="false"` on single-select; `aria-selected` = applied for Labels), `aria-checked` on the Status picker's current Status, `Esc` steps back / closes and returns focus (2.3, 2.6), the panel's focus trap (Tab never leaves the panel, `TaskDetailTests.Focus_is_trapped_in_the_panel_and_returns_to_the_card_on_Esc`), the picker keeping its keys to itself (`@onkeydown:stopPropagation` on `MovePicker`/`TaskMenu`), and the `LabelOption.FocusAsync` the trap may call.

- [ ] **Task 7 — Accessible structure** (AC: 7, 12, 13)
  - [ ] Per Q1: keep native structure — `<section aria-labelledby>` per column, `<h2>`, `<ul>`, `<li>` — with the composite's roving focus; give the row of columns a group name if Q1 says so. No `role="application"` anywhere (gate it, Task 8).
  - [ ] Per Q8: the count chip is `aria-hidden` and the column carries a description "{n} Tasks" / "1 Task" from resx (`aria-describedby` on the `<section>`, or a visually hidden sibling inside the heading's description); each card `<li>` carries `aria-posinset` / `aria-setsize` from the column's true `Total` (AD-29; 2.9 keeps them true under paging).
  - [ ] New resx entries with `<comment>`s: the pick-up sentence, the step sentence, the column count description (singular and plural), any group name (Q1). Never quoted in C# (`No_user_visible_copy_is_written_as_a_csharp_string_literal`).

- [ ] **Task 8 — Gates** (AC: 4, 7, 11)
  - [ ] Architecture gates (new partial, e.g. `DesignFoundationGateTests.Keyboard.cs` or a `KeyboardGateTests.cs`), each proved by a plant: no `role="application"` in any `.razor`/`.html`/`.js`; the keyboard module is attached for every Role and the drag module still for movers only; the `Space` branch is guarded by the mover's capability; no `tabindex` greater than 0 anywhere.
  - [ ] JS-scanning gates (`ClientStorageBanTests.No_client_asset_reaches_browser_storage`, `SpaceSwitcherGateTests`) must cover the new module and stay green unchanged.
  - [ ] Existing exact-count assertions to re-check before changing card markup: `BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow` (`button, a, [tabindex]` = 2 — `tabindex="-1"` on the two buttons keeps it 2), `LabelTests:121-122` (`[tabindex]:not([tabindex='-1'])` — the active card's title alone must not push a non-active card over), E2E-003's Viewer assertions. Widen any of them only deliberately, recorded in the Debug Log as 2.6 and 2.7 did.

- [ ] **Task 9 — Browser tests** (AC: 1-17)
  - [ ] New `tests/Yello.Tests.Client/KeyboardTests.cs` in `ClientSurfaceCollection` against `FakeYelloApi` and its 2.6 move harness (`HoldTaskMoveAsync`, `ReleaseTaskMove`, `ReleaseTaskMoveRefused`, `ReleaseTaskMoveUnanswered`, `NextTaskMoveRefusal`). **No new fixture class, no container.** Reset focus before each keyboard case (`BoardTests.cs:570` idiom). Poll; never `Task.Delay` as an oracle. Hold → assert → release → assert for every move.
  - [ ] Cases (one behaviour each, sentence names, `Requirement` traits `UX-DR24`, `NFR-9`, `FR-29`, `UX-DR25`, …):
    - Tab into the Board lands on one card; a second Tab leaves the composite (or reaches the active card's actions control per Q3, then leaves); Shift+Tab returns; exactly one element inside the row of columns has `tabindex="0"` at all times (AC1, AC7).
    - `↑`/`↓` within a column, clamped at the ends; the column did not scroll by the key's default (`scrollTop` unchanged when the target is already in view) (AC1).
    - Sticky origin across columns of 5, 1, 0 and 3 cards: focus lands at index 4 → 0 → the empty column → 3, then back to 4 (AC2).
    - RTL: `document.documentElement.dir = "rtl"` set by the test; `ArrowRight` goes to the previous column in data order, `ArrowLeft` to the next; the Status order is unchanged (AC1, UX-DR42).
    - Pick up with `Space`, carry `→` then `↓`, drop with `Space`, move **held**: the card is already in the new slot, both totals moved, the POST body is `{statusId, afterTaskId}` with the expected neighbour and no key; focus is on the moved card; release → "Moved to …, position n of N." (AC3, AC5, AC9, AC10).
    - Landing ordinal: picked up at position 3, `→` into a column of 1 Task lands at slot 1 (bottom), then into a column of 5 lands at slot 3 (sticky), `↑` adjusts to 2 before the drop; the step sentences are said in order (AC5, AC10).
    - Into an empty column: lands at slot 0; drop sends `afterTaskId: null` (AC5).
    - Drop on the origin slot → no POST, "Returned to …" (AC9). `Esc` mid-carry → no POST, "Returned to …", focus on the card, and the panel/menu did not open (AC6).
    - Refused (`403`, `409`, `400`), unanswered and `404` keyboard drops follow 2.6's outcomes, words on the Board notice; `409` re-reads the Board; focus stays on the card by identity (AC9).
    - `Space` on the actions control opens the menu; on the "+" opens the create field; on a column `<section>` reached by `Tab`/focus (non-card) the key is not prevented (`defaultPrevented` false via a page-level listener); on a card it is prevented (AC4). `Space` on a Viewer's card opens the Task and never picks up; a Viewer's arrows traverse; no carried class, drop zone or POST ever appears; `main [disabled], [aria-disabled='true']` count 0 (AC11). Plant: the `Space` branch unguarded for Viewers → caught.
    - `Enter` opens Task detail; `Shift+F10` and `ContextMenu` open the card menu; Esc in the menu, the picker steps, the panel and the delete confirm each close only the innermost (AC6, AC12).
    - Parity matrix (X-4): one case per pointer operation with its keyboard route — open a Task, open the card menu, create a Task, move within a column, move across columns, reach every Task in every column (AC8).
    - The column is named by its Status and described "{n} Tasks"; `aria-posinset`/`aria-setsize` match the ordinal and true total (AC13).
    - Identical sentences: two "Task added." in a row, two "Returned to …" from two `Esc`-cancelled carries, two "Project deleted.", two "Label deleted." — each produces a fresh announcement (assert with a `MutationObserver` installed in the page that records the region's text after each mutation, per Q6's mechanism) (AC14). Plant: the mechanism removed → caught.
    - Pickers: in the Status, Label and Move pickers and the card menu, `↓`/`↑`/`Home`/`End` move focus, `Space` chooses/toggles and `window.scrollY` is unchanged, only one option is a tab stop, nothing is `aria-selected="true"` on open (except applied Labels) (AC15).
    - Chained: A moved by keyboard (held), then B dropped directly after A → B's POST is not sent until A's is answered (assert by the fake's request log while A is held), then is sent and admitted (AC16). Plant: the wait removed → B's POST observed while A is held.
    - Board-side ends: a carry under a Board re-read (trigger by a `409` on another Task's held move) ends with nothing sent and the Q10 sentence; a purge (Space switch) ends it silently (AC17).
    - Nothing is announced on plain traversal or on load beyond the existing "Board loaded." (AC10).
  - [ ] `TextSpacingTests`: a carried card and its drop zone under the 1.4.12 override and 200% text-only zoom clip nothing (each new surface adds its case, `deferred-work.md:20`).
  - [ ] Amend 2.7's tests that assert silence on Board-side cancels if Q10 makes them speak (grep `DragTests.cs`/`TouchDragTests.cs`); `Nothing_is_announced_on_lift_or_on_crossing_columns` stays true under Q5 (a).
  - [ ] Rebuild `Yello.Client` in **Debug** before every browser run and plant.

- [ ] **Task 10 — Plants, runs, ledger, status** (AC: all)
  - [ ] Plants one at a time; original held in memory and restored in a `finally`; hash compared after restore; **never** `git checkout --`, `git restore`, `git reset` or `git stash`. Each plant compiles. Record `| # | Plant | Gate | Caught |`. Minimum set: sticky origin re-derived per hop; `ArrowRight` bound physically (RTL case); `Space` prevented on every focused element (AC4 cases); `Space` pick-up allowed for a Viewer; every card title `tabindex="0"`; the keyboard drop sent as a `PATCH`; origin drop sending a POST; the chained-move wait removed; the repeated-announcement mechanism removed; `role="application"` on the row of columns; `Esc` during a carry also closing a surrounding surface.
  - [ ] Runs (CLAUDE.md): `dotnet build Yello.slnx` (0 warnings, 0 errors), `dotnet build-server shutdown`, `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. A Client failure seen only in the full run is rerun alone (`dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build`) before it is called real. Docker readiness `docker info --format '{{.ServerVersion}}'`; `Get-Process Yello.Host` if `bin/Debug` is locked.
  - [ ] `tests/TESTING-CONVENTIONS.md`: how keyboard cases reset focus and assert `defaultPrevented`, how RTL is forced, how a repeated announcement is observed.
  - [ ] Ledger (`deferred-work.md`): CLOSE `:489`, `:496`, `:513`, `:517`, `:535`, `:549`, `:564`, `:579` naming the code and tests; CLOSE the 2.8 half of `:569` per Q5 (the "Drop here" copy stays with UX); UPDATE `:130` (keyboard-parity matrix written; axe still open per Q13); NOTE `:525` ("+N" is still text, nothing placed); ADD with owners anything decided against the spines (the pick-up and step sentences — owner **UX** to confirm the copy; Q2's Tab reading; Q12's carried look; the axe decision — owner **Lee**).
  - [ ] `sprint-status.yaml`: `review` at the end of the dev phase. **Do not add review narration to source files.**

## Dev Notes

### The shape of the story in one paragraph

2.6 built the move (route, keys, replica, announcements, Move control, card menu); 2.7 built the pointer/touch gesture that hands a `MoveChoice` to `BoardView.MoveAsync` and the drop zone that `BoardColumn` draws from `DropIndex`. 2.8 adds the **keyboard**: the Board becomes one roving tab stop; arrows traverse by logical direction with a sticky origin index; `Space` on a mover's card picks the Task up, arrows carry it (drop zone shown, each step announced), `Space` drops through the same `MoveAsync`, `Esc` cancels. Alongside it, the ledger's accessibility debts land here: the announcement model (repeated sentences), the column's accessible count, the pickers' and menu's keyboard patterns, the spurious `409` of chained moves, and what Board-side cancels say. The hard parts: Blazor cannot `preventDefault` conditionally per key (so a small JS module decides which keys it swallows), keeping exactly one tab stop while focus moves by many routes, focus by **Task identity** across re-renders, and not breaking browse mode.

### What exists today (read before writing)

- **No `src/` folder.** Production projects at the repo root; tests in `tests/`.
- **`BoardView.razor`** (475 lines): live region `<p class="visually-hidden" role="status" aria-live="polite">@_announcement</p>` (`:55`); Board notice `p.board__notice role=status` (`:78`); row of columns `div.board__columns @ref=_columnsElement` (`:92`); each `BoardColumn` gets focus requests (`FocusTaskId`/`FocusRequest`, `FocusColumnRequest`), menu state, `MovedTaskId`, `SettlingTaskId`, `LiftedTaskId`, `DropIndex` (`:95-119`); `OnAfterRenderAsync` spends `_arrivalCard`, focuses the empty action, calls `AttachDragAsync()` (`:225-237`); `Purge` calls `_drag?.End()` (`:271-280`); `RereadBoardAsync` calls `_drag?.End()` then rebuilds `_columns` and `Readmit()` (`:348-374`); `Add` says "Task added." (`:431-443`).
- **`BoardView.razor.cs`** (1075 lines): `MoveOrigin { Panel, Menu, Drag }` (`:110-115`) — add `Keyboard`; `Open` ends the drag (`:136-151`); `MoveAsync(taskId, choice, origin)` (`:269-295`) — `Menu` calls `FocusCardTitle`, `Drag` sets `_settlingCard`; `FocusCardTitle` = `_focusCard` + `_focusCardRequests++` + `_arrivalCard` (`:301-306`); `AnnounceMove` (`:313-343`) — refused menu move refocuses; `Deleted` ends the drag if lifted, focuses the column (`:479-507`); `EditAsync` → `SendInTurnAsync` (per-Task, per-attribute queue; **no Board-wide ordering**, which is why `:564`'s `409` happens) (`:521-583`); `_rereadBoard` after `task-position-unavailable` (`:705-716`); `SayInPanel` routes a move's refusal to the Board notice when `_moveOrigins` has it (`:790-804`); `PlaceAfter` (`:892-912`); `TaskReplica.HasOutstanding` (`:1046`).
- **`BoardView.Drag.cs`**: `DropIndexIn(column)` (`:36-37`); `CanLift` refuses when a panel is open (`:53-66`); `DropAsync` computes `afterTaskId` without the dragged Task and detects the origin slot (`:72-103`) — extract and share; `Returned(taskId)` says `TaskReturned` and settles (`:117-126`).
- **`BoardDrag.cs`** (327 lines): attach/detach serialised through `_queue`, movers only; `End()` silent; four `[JSInvokable]`s. **`IBoardDragHost.cs`**: `CanLift`, `DragChanged`, `DropAsync`, `DragCancelledAsync`.
- **`board-drag.js`** (596 lines): attached to the row of columns; installs window-capture listeners for `pointerdown`/`pointerup`/`keydown`/`click`/`contextmenu` (`:128-132`); `#onAnyKey` stops click suppression on any key (`:188-192`); `#onKey` handles `Escape` during a gesture with `preventDefault` + `stopPropagation` (`:479-494`). A keyboard module must not fight it: during a pointer drag the drag owns `Esc`; during a carry the keyboard owns it; they never coexist (Task 4).
- **`BoardColumn.razor`**: `<section class="board-column" aria-labelledby=… data-status-id=… tabindex="-1">` (`:29`) — a programmatic focus target today, never a tab stop; head `<h2>` + `<span class="type-meta column-count">@Total</span>` (bare number, `:32`) + "+" (`:36`); list rendered when `Tasks.Count > 0 || DropIndex is not null` (`:42`); drop zone `li.drop-zone aria-hidden` at `DropBefore`/`DropLast` (`:47-72`, computed without `LiftedTaskId` — **a carry must feed the same exclusion**, e.g. by passing the carried id through the same parameter or a sibling one); focus logic (`:214-238`).
- **`TaskCard.razor`**: `<li class=…>` with `@oncontextmenu`/`@onkeydown` for movers (`:39-42`); mover: `div.task-card__head` with `button.task-card__open[data-task-id]` and `button.task-card__actions` ("Actions for {title}", `aria-haspopup="menu"`, `aria-expanded`) (`:43-59`); Viewer: the title button only (`:61-66`); "+N" is text with a hidden name (`:74-80`); classes `--movable`, `--lifted`, `--settled`, `--moved` (`:165-190`); `CardKeyDownAsync`: ContextMenu/Shift+F10 open the menu, Esc closes it (`:269-274`). Both buttons are tab stops today.
- **Pickers**: `PickerOption.razor` (`li role=option tabindex=0 aria-selected=false aria-checked?`, Enter/Space choose, Space not prevented); `LabelOption.razor` (`tabindex=0`, `aria-selected`=applied, `FocusAsync`); `MovePicker.razor` (`ul role=listbox tabindex=-1` focused on open, `@onkeydown:stopPropagation`, Esc steps back then `OnDismiss`); `TaskDetail.razor` (`:116` Status `ul role=listbox`, `:178` Labels `aria-multiselectable`; Esc ladder `:466-508`; sentinels `:54, :238`); `TaskMenu.razor` (`div role=menu`, two `button role=menuitem`, first focused, Esc closes, `stopPropagation`).
- **CSS**: `:focus-visible` ring from `base.css:194-197` (2px, offset token — never inset, never removed); the 24px target floor applies to `[tabindex]:not([tabindex="-1"])` (`base.css:234-246`), so an empty column `<section>` made `tabindex="0"` falls under it (it is far larger — fine); `.task-card.task-card--lifted` is `position: fixed` from drag-written custom properties — do not reuse it for a carry; the shadow gate admits exactly the lifted card (`No_surface_carries_a_shadow`).
- **Copy**: `TaskMoved` "Moved to {0}, position {1} of {2}.", `TaskReturned` "Returned to {0}, position {1}.", `TaskAdded`, `BoardLoaded`, `DragDropHere`, `TaskActions`, `LabelsMoreName` (`ClientCopy.resx:393-694`). No plural infrastructure — the product is English-only in copy; two keys for singular/plural is the pattern to use.
- **Tests that pin today's shape** (search before changing markup): `BoardTests.cs:247` (`button, a, [tabindex]` = 2 per mover card), `BoardTests.cs:570-575` (Tab from the top reaches the skip control first — must stay true), `LabelTests.cs:121-122`, `MoveTests.cs:870-905` (`OpenCardMenuAsync` focuses the title/actions with `FocusAsync` then presses keys — programmatic focus on a `tabindex="-1"` button still works), `TaskDetailTests.cs:131-160` (focus trap; Esc returns focus to `data-task-id`), `DragTests.Nothing_is_announced_on_lift_or_on_crossing_columns` (`:645`), 2.7's Viewer cases.
- **Preserve** every 2.3/2.5/2.6/2.7 review fix: the Board re-read on `409`; `_movedCard`/`_settlingCard` cleared on settle; the menu toggle; the Board notice as a status region; a refusal for one Task never shown in another's panel; `InFlight` cleared only after admission; `5xx` is no answer; stale focus requests ignored on first render; the pointer-blocking layer under the panel; drag attach/detach serialised; click suppression ending at the next key.

### Open questions (decide in Task 1; each reversible; record as "Qn - (x)")

1. **Which "equivalent" of `role="grid"` (AC7).** A Kanban board is not a grid: columns are ragged, `aria-posinset`/`aria-setsize` (AD-29, 2.9) are not supported on `gridcell`, and a grid's row/column coordinates would misreport a card's ordinal. Options: (a) keep native structure — `section`/`h2`/`ul`/`li` per column, cards' `li` carrying `aria-posinset`/`aria-setsize` — and make the row of columns one roving-tabindex composite (one tab stop, focus managed internally by the arrow grammar); browse mode keeps headings and lists, which is the conformance path with the Move control; (b) `role="grid"` on the row of columns with one `role="row"` per card position — misreports coordinates and loses list semantics; (c) one `role="grid"` per column with each card a `row` (`aria-rowcount`/`aria-rowindex` for 2.9) and one roving stop across them. **Recommend (a)** — it is the "equivalent" AC7 permits, and EXPERIENCE `:326`'s warning ("built wrongly, browse-mode users lose the Board entirely") points away from forcing application mode. Cost: screen-reader users reach the arrow grammar only after switching to focus mode themselves — acceptable because the arrow grammar is explicitly not the conformance path (AC7, UX-DR21). Ledger note to UX recording the reading.
2. **Tab "between columns and controls" vs "a single tab stop" (AC1 vs AC7).** Options: (a) the composite (all cards and empty columns) is **one** tab stop; the other controls — the first column's "+", the active card's actions control (Q3), the empty state's action — are their own stops in DOM reading order; `←`/`→` move between columns; (b) one roving stop **per column** (Tab steps column to column) — matches AC1's words, contradicts AC7's "single tab stop". **Recommend (a)**; record AC1's "Tab between columns and controls" as Tab reaching the Board's composite and the controls around it in reading order. Cost: ledger note to UX.
3. **The card's actions control in a one-stop Board** (`deferred-work.md:513`). Options: (a) the **active** card's actions control stays a tab stop (`tabindex="0"` on the active card only, `-1` elsewhere), so Tab from the active card reaches its actions and then leaves the Board — discoverable without knowing Shift+F10; (b) every actions control `tabindex="-1"`, reachable by `Shift+F10`/`ContextMenu`, by pointer, by `Enter` → Task detail's Move, and by the screen reader's virtual cursor. **Recommend (a).** Cost: two stops on the active card; the card's Tab sequence is "title, actions, out".
4. **Empty columns and `Tab` during a carry.** Options: (a) traversal lands on an empty column (its `<section>` becomes the roving stop, read as "{Status}, 0 Tasks"), so no Status is silently skipped and a carry can target it; `Tab` during a carry cancels it (said as a return) so focus never leaves with a Task in hand; (b) skip empty columns in traversal (a carry still enters them). **Recommend (a).** Cost: the `<section>`'s `tabindex` becomes `0` while active (Blazor-rendered).
5. **The pick-up and step sentences; pointer lifts** (UX-DR25; `deferred-work.md:569`). No document gives the strings (UX-DR25 requires "a specified string shape"; the research found none for pick-up). Options for the sentences: (a) pick-up "Picked up {title}. {Status}, position {n} of {total}."; each carried step "{Status}, position {n} of {total}." — `{total}` the count the destination column would have with the Task in it (the column's true `Total`, +1 when it is not the origin column); drop and cancel keep 2.6's two sentences; (b) shorter "Picked up {title}." and steps without the total. **Recommend (a)** — EXPERIENCE `:324`: "without the total, no way to know you are at the end". For pointer lifts: (a) they stay silent (2.7 Q7 (a), AC14 and `DragTests.Nothing_is_announced_on_lift_or_on_crossing_columns` unchanged — a screen-reader user picks up by keyboard); (b) a pointer lift says the pick-up sentence. **Recommend (a)** and close 2.8's half of `:569`. The title appears in a sentence; never the Space or Project name (UX-DR38). Cost: ledger note to UX to confirm both strings.
6. **How a repeated sentence is announced again** (`:489, :549`). Options: (a) the region is cleared, and the sentence written on the **next** render (after `OnAfterRenderAsync` of the clearing render) — one region, two renders; (b) two regions alternated, writing into the one not holding the last sentence and clearing the other; (c) append an invisible varying character — hacky, may be read. **Recommend (a)**, falling back to (b) if a test shows Chromium's accessibility tree coalesces the two mutations; build it once as a component and use it on the Board, `ProjectList` and `LabelSection`. Cost: one component; the region keeps "empty from first render" (2.3).
7. **Pickers: roving `tabindex` or `aria-activedescendant`; wrap or not** (`:517, :535`). Options: (a) roving `tabindex` on the options (focus moves to the option element, as today), `↑`/`↓` wrap at the ends, `Home`/`End`; (b) `aria-activedescendant` on the `ul` (focus stays on the list). **Recommend (a)** — it keeps the existing focus-return code (`LabelOption.FocusAsync`, `MovePicker`'s list focus) and today's tests that focus an option. Card menu: `↑`/`↓` wrap, `Home`/`End`, Tab leaves the menu and closes it? Options: leave Tab as today (focus moves on, menu stays open until Esc or a choice) or close on Tab. **Recommend** leaving Tab as today (smallest change; 2.6 tests). Cost: none.
8. **The column's accessible count and `aria-setsize`/`aria-posinset`** (`:496`; AD-29). Options: (a) chip `aria-hidden`, a visually hidden "{n} Tasks"/"1 Task" description referenced by `aria-describedby` on the `<section>`, and `aria-posinset`/`aria-setsize` on every card `<li>` now, from the true `Total` (true today: every Task is loaded); (b) only the description; leave posinset/setsize to 2.9. **Recommend (a)** — 2.9 then only keeps them true under paging. Cost: 2.9 reads this.
9. **Chained moves** (`:564`, AC16). Options: (a) a move naming a neighbour whose own Status attribute is outstanding waits until that neighbour's change settles, then is sent (optimistic render unchanged); (b) serialise every move on the Board through one queue; (c) leave it: the `409` re-reads the Board and "Choose it again." is said. **Recommend (a)** — narrowest, and keyboard carries make it common. Cost: a move can sit behind another for one round trip.
10. **What a Board-side end of a carry or lift says** (`:579`, AC17). Options: (a) a re-read, the Task deleted by this tab, the panel opened or the Role lowered ends the carry/lift and says "Returned to {Status}, position {n}." for the Task where it now is (nothing when the Task is gone — a deletion already says "Task deleted."); a purge or disposal says nothing; (b) every Board-side end silent (2.7's rule). **Recommend (a)** for both keyboard carries and pointer lifts — a person carrying a Task needs to hear that it is no longer in hand. Cost: amend 2.7 cases that asserted silence, if any.
11. **Where the key handling lives.** Options: (a) a new ES module (e.g. `Yello.Client/wwwroot/js/board-keys.js`, no package) attached to the row of columns for every Role, reporting keys to a `[JSInvokable]` holder (`BoardKeys`, beside `BoardDrag`), plus a small exported helper the pickers and menu use to prevent `Space`/arrows/`Home`/`End` on their options; (b) extend `board-drag.js` — but that module is movers-only by gate (UX-DR27), and a Viewer must traverse; (c) Blazor events only — cannot `preventDefault` conditionally (the column would scroll on every arrow, `Space` would click the title). **Recommend (a).** Cost: a second JS file; the JS-scanning gates cover it.
12. **What a carried card looks like.** DESIGN.md says nothing about a keyboard pick-up's visual (the lifted card is "Drag only", `DESIGN.md:376, :435`). Options: (a) the carried card stays in its admitted slot marked `task-card--carried` — the 2px emphasis border (`--border-emphasis-width`, as the lifted card) and no shadow, no rotation, no `position` change — and the dashed drop zone (`BoardColumn.DropIndex`) marks the target, scrolled into view each step; focus stays on the carried card; (b) the card itself travels into the target slot in the DOM, focus following it. **Recommend (a)** — no shadow gate widened, no card re-created per step (re-creation drops focus), and the pointer's drop-zone vocabulary is reused. Cost: ledger note to UX.
13. **Axe-core (X-3) and the parity matrix (X-4).** No axe package exists; a new dependency needs Lee's approval (CLAUDE.md). Options: (a) 2.8 writes X-4 (the keyboard-parity matrix) and asserts ARIA structure deterministically; X-3 stays open with a ledger entry owned by **Lee** (approve `Deque.AxeCore.Playwright` or a vendored `axe-core`); (b) block the story on the approval. **Recommend (a).** Cost: none to 2.8.
14. **Testing keys reliably.** **Recommend** `page.Keyboard.PressAsync` with focus reset first (`BoardTests.cs:570`); assert `defaultPrevented` by a page-level capture listener that records it for the next `keydown`; force RTL by setting `dir="rtl"` on `<html>` (no RTL locale exists); observe the live region with a `MutationObserver` recording each text state. No timing assertions, so no `DisableParallelization` collection is needed. Cost: none.

### Architecture compliance (must)

- **One write path** (orchestrator instruction; AD-15; FR-35): a keyboard drop is `BoardView.MoveAsync` → `POST TaskRoutes.MoveOf` with `MoveTaskRequest`. No new route, no Contracts type, no key from the client, no server change. The `afterTaskId` computation is shared with the drag's.
- **AD-29**: focus follows Task **identity** — every focus move goes through a request naming a Task id (or a column's Status id), never a stored element; no DOM recycling is introduced.
- **AD-11**: optimistic, never blocking the network; Q9's wait delays a *send*, never the render.
- **UX-DR27**: a Viewer gets traversal and no pick-up — absent, not disabled; the drag module stays movers-only.
- **UX-DR21**: the Move control and card menu are unchanged and remain the browse-mode path; no `role="application"`.
- **UX-DR38**: no announcement on load (beyond the existing, UX-owned "Board loaded."), on windowing or on plain traversal; no cross-Space information in any sentence.
- Client rules: nothing in browser storage (gate scans `.js`); `document.title` stays `Yello`; every string in `ClientCopy.resx` with a `<comment>`; logical properties; no `!important` motion rule; focus ring never removed or inset; JS never writes what Blazor renders (attributes, classes, structure) — it may call `focus()`, `scrollIntoView` and `preventDefault`.

### Library / framework

No new packages (CLAUDE.md). .NET 10.0.11, Blazor WebAssembly 10 (`IJSRuntime` `import`, `IJSObjectReference`, `DotNetObjectReference`, `[JSInvokable]`, `@onfocusin`, `ElementReference.FocusAsync`), Microsoft.Playwright 1.62.0 (`Keyboard.PressAsync`, `EvaluateAsync`), xunit.v3 4.0.0. Web platform: `KeyboardEvent.key` (`"ArrowLeft"`, `" "`, `"Escape"`, `"Home"`, `"End"`, `"ContextMenu"`), `getComputedStyle(el).direction`, `Element.scrollIntoView({ block: "nearest", inline: "nearest" })`, `MutationObserver`. A `button`'s Space activation fires on `keyup`; preventing `keydown` stops it in Chromium — verify with a case (AC4/AC3) rather than assume.

### Testing standards (from `tests/TESTING-CONVENTIONS.md` and CLAUDE.md)

- Add to existing suites: `tests/Yello.Tests.Client` (browser, `ClientSurfaceCollection`, `FakeYelloApi`), `tests/Yello.Tests.Architecture` (gates and client unit tests). **No new test project, fixture class or container-backed class.**
- Traits: `Suite`, `Priority` (X-4 is P1), `Requirement` (`UX-DR24`, `NFR-9`, `FR-29`, `UX-DR25`, `AD-29`), `Assumption`. Sentence-style names, one behaviour per test.
- No `Task.Delay` as an oracle; hold the fake's answer; hold → assert → release → assert for optimistic moves.
- Every absence assertion is proved against a plant (`:172-175`). Rebuild `Yello.Client` in Debug before browser runs and plants.
- Full runs with `--max-parallel-test-modules 1`; never two `dotnet test` at once; a Client failure only in the full run is rerun alone first (memory: the full run starves the browser suite).

### Previous story intelligence (2.7, 2.6)

- **2.7's lessons**: Blazor lost a keyed `<li>`'s attributes when it moved between loop branches — the drop zone is unkeyed and drawn afresh; JS must never mutate Blazor's DOM; attach/detach overlap needed a serial queue (copy that pattern for the keyboard module); click suppression must end at the next key (`#onAnyKey`) — a keyboard activation after a drag must not be swallowed, and 2.8's cases will exercise exactly that path.
- **2.6's lessons**: gates that pin exact counts break first (see "Tests that pin today's shape"); S1200 forced splits — keep new state out of `BoardView.razor.cs`; markup in `@code` lambdas tripped `No_user_visible_*` gates.
- **2.6 review**: a `409` that never re-read the Board looped "Choose it again." — AC16/Q9 now removes the commonest cause, and the re-read stays for the rest.
- **2.7 review**: Board-side interruptions must end a pending lift too; a lift arriving after `End()` is reported as abandoned — a carry has no async lift, but a pick-up while a lift is on its way must be refused (Task 4).
- Environment: Rancher Desktop; on "timed out dialing Hyper-V socket" run `rdctl shutdown`, `wsl --shutdown`, relaunch, poll readiness (CLAUDE.md).

### Git intelligence

Each story lands as "Add story → (ATDD) → implement → Close the code review" (2.7: `ba2354c`, `211c388`, `f2e4f72`). `211c388` touched `BoardColumn.razor`, `BoardDrag.cs` (new), `BoardView.Drag.cs` (new), `BoardView.razor(.cs)`, `IBoardDragHost.cs` (new), `TaskCard.razor`, `ClientCopy.resx`, `components.css`, `tokens.css`, `board-drag.js` (new), the design gates, `DragHarness`/`DragTests`/`TouchDragTests`, `TextSpacingTests`, `TESTING-CONVENTIONS.md`. Expect 2.8's footprint client-only: one JS module, `BoardKeys` (+ host interface), `BoardTraversal`, `LiveAnnouncement`, `BoardView`/`BoardColumn`/`TaskCard`, the pickers and menu, `ProjectList`/`LabelSection` (announcer only), `components.css` (`--carried`), `ClientCopy.resx`, `KeyboardTests.cs`, architecture gates and unit tests, `TextSpacingTests.cs`, `TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml`.

### Project Structure Notes

- New: `Yello.Client/wwwroot/js/board-keys.js` (Q11), `Yello.Client/Components/BoardKeys.cs` (+ host interface or partial `BoardView.Keys.cs`), `Yello.Client/Components/BoardTraversal.cs`, `Yello.Client/Components/LiveAnnouncement.razor`, `tests/Yello.Tests.Client/KeyboardTests.cs`, an architecture test file for the traversal unit tests and keyboard gates.
- Updated: `BoardView.razor`, `BoardView.razor.cs` (`MoveOrigin.Keyboard`, chained-move wait), `BoardView.Drag.cs` (shared `afterTaskId`, Q10 sentences), `BoardColumn.razor`, `TaskCard.razor`, `PickerOption.razor`, `LabelOption.razor`, `MovePicker.razor`, `TaskMenu.razor`, `TaskDetail.razor`, `ProjectList.razor`, `LabelSection.razor`, `components.css`, `ClientCopy.resx`, `TextSpacingTests.cs`, possibly `DragTests.cs`, `tests/TESTING-CONVENTIONS.md`, `deferred-work.md`, `sprint-status.yaml`.
- Not touched: `Yello.Host`, `Yello.Application`, `Yello.Domain`, `Yello.Infrastructure`, `Yello.Contracts`, migrations, `IsolationCases.cs`, `board-drag.js` (except, if needed, to refuse a lift during a carry — prefer doing that in `CanLift`).
- Variances to record: the grid "equivalent" (Q1); Tab's reading (Q2); the pick-up and step sentences (Q5); the carried look (Q12); axe not built (Q13).

### References

- `_bmad-output/planning-artifacts/epics.md:162` (NFR-9), `:128` (FR-28), `:262-266` (UX-DR21-25), `:278-282` (UX-DR38-42), `:361` (coverage), `:1166-1251` (2.6, 2.7), `:1253-1293` (2.8), `:1295-1336` (2.9), `:1386-1428` (2.11)
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md:524-534, 753-757`; `addendum.md:153`
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md:249-258` (AD-29), `:277`
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:218, 221, 283-295, 310-326, 366-376, 381-390, 404, 437, 463`; `DESIGN.md:194-199, 362-368, 376, 435, 452`; `mockups/board.html:70, 97, 127, 170`; `review-accessibility.md:44-53`
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md:542, 910, 974`; `sprint-change-proposal-2026-09-13.md` (nothing on 2.8)
- `_bmad-output/specs/spec-yello/SPEC.md:148`
- `_bmad-output/test-artifacts/test-design-qa.md:207, 311, 337-341, 354-357, 421, 490`; `test-design-architecture.md:118-122, 230, 315, 471-472`
- `_bmad-output/implementation-artifacts/2-6-move-and-reorder-a-task-by-explicit-control.md` (Q2, Q5, Q6); `2-7-the-accelerated-pointer-and-touch-paths.md` (Q1, Q5-Q7, Q10; review findings); `deferred-work.md:130, 489, 496, 498, 513, 517, 525, 535, 549, 564, 569, 579`
- `tests/TESTING-CONVENTIONS.md:16, 30, 34-72, 88, 96, 172-175, 187, 233-293`; `CLAUDE.md` (memory budget, undo rule, new dependencies)

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

**Create-story phase (2026-10-28).** No isolation row is pending on 2.8 (grepped `tests/Yello.Tests.Isolation`). No ATDD scaffold exists for 2.8. Planning inputs read through a research pass over the planning documents and test design, plus a direct read of the client code (`BoardView`, `BoardView.Drag`, `BoardDrag`, `board-drag.js`, `BoardColumn`, `TaskCard`, `MovePicker`, `TaskMenu`, `PickerOption`, `LabelOption`, `TaskDetail`'s Esc ladder, `base.css` focus rules), story 2.7 and `deferred-work.md`; facts above cite paths and lines as read on 2026-10-28. No axe-core package exists. No product locale is right-to-left. No existing browser test presses `Space`.

### Completion Notes List

- Story created 2026-10-28 by create-story: ultimate context engine analysis completed — comprehensive developer guide created. Open questions Q1–Q14 carry recommended decisions; the dev phase decides each as a reversible, recorded decision.

### File List
