---
baseline_commit: f37b5b9
---

# Story 2.11: The Board on a small viewport

Status: done

Epic: 2 — Track your own work on a Board
Story key: `2-11-the-board-on-a-small-viewport`

**Requirements owned.** These come from the Story Coverage Index (`epics.md:364`: "2.11 | UX-DR20, UX-DR41, UX-DR42, NFR-9").

- **UX-DR20, `status-pager`.** Below 768px the Board shows one column at a time behind a **tablist** over the Project's effective Status set. The column is the tab's panel. Arrow keys move between tabs. A change announces the new Status and its true count politely. It is the *only* route to a Status at that width, so it is the surface a 1.4.10 audit is run on (`epics.md:258`; `EXPERIENCE.md:218`).
- **UX-DR41, responsive behaviour.**
  - ≥1280px: all columns side by side, and a full context bar.
  - 768–1279px: columns side by side with a horizontal Board scroll. The context bar drops the Project name.
  - <768px: one column with the pager. Long-press drag works within the visible column. Cross-Status moves use the Move control.
  - The Role chip and the Offer indicator survive every breakpoint. The drop order is the Project name, then the switcher chevron label, **never the Role**.
  - The two-dimensional-layout exemption is declined (`epics.md:285`; `EXPERIENCE.md:409-421`).
- **UX-DR42, internationalisation.** Use logical properties throughout. Column order, drag direction and the arrow keys mirror under RTL. The Status *sequence* does not (`epics.md:286`; `EXPERIENCE.md:434-437`).
- **NFR-9.** WCAG 2.1 AA, including 1.4.10 Reflow at 320 CSS px (`prd.md:753-757`).
- **Test-design scenario X-11** (P1, E2E, three case classes): small viewport, the 1.4.12 text-spacing override and 1.4.4 resize (`test-design-qa.md:369`; `test-design-progress.md:633`).

**Also binding:**

- **UX-DR8:** a column scrolls within itself, never the page. At 320px a card's metadata row wraps before it scrolls.
- **UX-DR9:** the context bar is always present, never scrolls away and **never collapses behind a menu**. The Role chip is display-only.
- **UX-DR21:** the Move control is present at every breakpoint and is never hover-only.
- **UX-DR23/UX-DR24:** the touch and keyboard paths (2.7, 2.8).
- **UX-DR25/UX-DR38:** announce on deliberate action only. A tab change *is* a deliberate action. Loading and paging are not.
- **UX-DR26:** no hover-only control and no disabled control for a Role.
- **UX-DR27:** absent, not disabled.
- **UX-DR31:** Space-scoped client state, which now includes the selected pager tab, is purged with the Space before any render. It is never stored in Web Storage, the URL or the title.
- **UX-DR39:** focus follows the Task's identity.
- **UX-DR40:** focus is always visible and Tab follows reading order.
- **AD-29:** rows are appended and never recycled. `aria-setsize` carries the true total.

**Explicitly not in this story:**

- Any server, Contracts, migration or isolation change. A pager move is 2.6's `MoveAsync`; there is no second write path.
- Configuring Statuses, and therefore more than three Statuses for real (**Epic 6**). The browser fake may still serve five (Q9).
- The Offer indicator itself (**Epic 5, story 5.1**). It does not exist yet (`tokens.css:270` only).
- An RTL product locale. None exists, so RTL is forced in tests with `dir="rtl"`, as 2.8 did.
- A device check on a real phone or iOS Safari, and the tap-to-select fallback. These stay with **Lee/UX** (`deferred-work.md:573`).
- axe-core (X-3). It is blocked on Lee's dependency approval (`deferred-work.md:589`).
- **No new NuGet package and no new project** (CLAUDE.md). **No Azure deployment, login or push.**

**Ledger entries owned by this story (`deferred-work.md`):**

- `:574`, "Below 768px a drag may cross into any column" (owner **2.11**). It is closed by Task 5: the drag is limited to the visible column, and the drag direction is proved under RTL.
- `:20`, the 1.4.12 / 200% measurement. Each later surface adds its own case, and "the small-viewport pager 2.11" is named. It is touched by Task 8.
- `:36`, the radius *mapping*. It is touched: the pager's tabs take a clause, and so does the context bar ("a sweep for whoever next touches them"; this story touches it).
- Touched only, still not owned: `:573` (the iOS device check and the tap-to-select fallback, Lee/UX). 2.11 adds Chromium-emulated touch at a phone width; record that, and leave the entry open.
- The ledger was grepped for `2.11`, `768`, `pager`, `320`, `RTL`, `context bar`, `chevron` and `breakpoint` on 2026-10-28. Nothing else names 2.11.

**Code hooks that name 2.11:**

- `components.css:594-599` ("Below 768px is story 2.11's").
- `components.css:611-618` ("below 768px is story 2.11's pager").
- `board-drag.js:32-35` ("UX-DR42 is story 2.11's").
- Story 2.7 Q15 (`2-7-…md:246`, `:371`).
- Story 2.8, line 13 ("making the `Space` carry inert behind [the pager]").

**Depends on:**

- **2.2:** the Board, the context bar's Project name, `TextSpacingTests`.
- **2.6:** `MoveAsync`, the Move picker and the card menu.
- **2.7:** `board-drag.js`, `TouchDragTests`, `DragHarness`.
- **2.8:** `board-keys.js`, `BoardTraversal`, `LiveAnnouncement`, `KeyboardHarness`, RTL forcing.
- **2.9:** paging, true totals, `FakeYelloApi.PageSize`.
- **2.10:** the 30em reflow precedent and the view toggle.

All of them are done.

> **Isolation gates.** 2.11 maps no route, so 1.9-GATE-002 is untouched and no isolation row is owed. No row is pending on 2.11. If one is added (none should be), GATE-003 requires it to be live before `review`.

## Story

As someone reading the Board on a phone, or at 400% zoom,
I want one column at a time with a way to reach the others,
so that the Board stays correct at the size an accessibility audit is conducted at.

## Acceptance Criteria

AC1 to AC8 are reproduced **verbatim** from `epics.md:1394-1428` (`status: final`) and numbered by position. AC9 to AC20 come from the UX spines, the ledger and the behaviour 2.6 to 2.10 built, and each one cites its source. Where an AC needs a decision to read it, the open question that decides it is named.

**AC1 — wide**
**Given** a viewport of 1280px or wider
**When** the Board renders
**Then** all Status columns are visible side by side, and the context bar carries Space name, Project and Role chip

**AC2 — middle**
**Given** a viewport between 768px and 1279px
**When** the Board renders
**Then** columns remain side by side with horizontal Board scroll, and the context bar drops the Project name while keeping Space and Role

**AC3 — narrow: the pager**
**Given** a viewport below 768px
**When** the Board renders
**Then** one column shows at a time behind a Status pager, implemented as a **tablist** over the Project's effective Status set with the column as its panel

**AC4 — the pager's keyboard and announcement**
**Given** the Status pager
**When** arrow keys are used
**Then** focus moves between tabs, and on change the new Status and its true count are announced politely
**And** this is specified rather than left open because the pager is the *only* route to a Status at that width

**AC5 — narrow: cross-Status moves**
**Given** a viewport below 768px
**When** a cross-Status move is made
**Then** it uses the Move control rather than a gesture, because a `Space`-plus-arrow move would send a carried Task into an off-screen column with no visible drop zone
**And** a committed move advances the pager to follow the Task

**AC6 — the context bar's drop order**
**Given** any breakpoint
**When** the context bar renders
**Then** the Role chip survives, and if something must be dropped it is the Project name first, then the switcher chevron label, and never the Role

**AC7 — 1.4.10**
**Given** 400% zoom on a 1280px monitor, yielding a 320px CSS viewport
**When** the Board is audited against 1.4.10
**Then** no content requires two-dimensional scrolling
**And** the two-dimensional-layout exemption a Kanban board would normally claim is deliberately declined, the horizontal axis being eliminated instead

**AC8 — logical properties and RTL**
**Given** every structural rule in the product
**When** surfaces are inspected
**Then** logical properties are used throughout rather than `left` and `right`, so layout, column order, drag direction and the arrow keys all mirror under RTL
**And** the Status *sequence* does not reverse, being data rather than layout

**AC9 — the pager's ARIA shape** (UX-DR20; WAI-ARIA tabs pattern; Q3)

- The pager is `role="tablist"` with an accessible name (Q4's copy). It has one `role="tab"` per effective Status, in effective order, each with `aria-selected` and `aria-controls` pointing at its column.
- Exactly one tab is selected.
- The visible column is `role="tabpanel"`, labelled by its tab.
- The tablist is **one tab stop**: the selected tab has `tabindex="0"` and the others `-1`.
- `Home` and `End` go to the first and last tab.
- `←` and `→` move by **logical** direction. They mirror under RTL, and so does `Home`/`End`'s visual end. The tab *order* stays the Status sequence (AC8).
- Activation is automatic (Q3): the arrow selects the tab it lands on.
- `Tab` from the tablist reaches the visible column's single roving stop (2.8 AC7).
- No `role="application"` (`KeyboardGateTests.Nothing_in_the_client_forces_application_mode`).

**AC10 — the announcement's shape and true count** (UX-DR20, UX-DR38, AD-29; Q4)

- On every tab change made by the person (arrow, `Home`/`End`, click or tap), one polite sentence names the Status and its **true total** from the column's `COUNT`, not the number of loaded rows. Example: "In Progress, 4,812 Tasks." at 5,000 with 50 loaded.
- It goes through `LiveAnnouncement` (`Sequence`), so the same sentence said twice is heard twice.
- A pager change the Board makes itself (following a move, AC5; crossing the breakpoint, AC14; the first render) says no tab-change sentence. The move's own sentence ("Moved to …") is the announcement.
- Nothing is said on load or paging.

**AC11 — only the selected column is visible, and nothing is lost** (UX-DR20, AD-29, 2.9; Q1)

- Below 768px every unselected column is out of view **and** out of the accessibility tree.
- Each column keeps its loaded cards, cursor and true total while hidden, and none is re-read on a tab change. Rows are never recycled.
- The `hidden` state comes from a CSS rule that fires by construction at the media query (AC7), so the first paint at 320px is already one column, before .NET learns the width.

**AC12 — the keyboard below 768px** (EXPERIENCE.md:320; 2.8; Q5)

- Inside the visible column, `↑` and `↓` traverse and page as in 2.8 and 2.9.
- `←` and `→` in the Board composite move nothing, and are consumed. The pager is the only route to another Status.
- `Space` still picks up a Task for an in-column reorder: carried `↑`/`↓`, then `Space` drops and `Esc` cancels.
- A carried `←`/`→` is refused: the Task stays in its column and nothing is announced except as in Q5.
- Every pointer operation still has a keyboard equivalent (X-4): a cross-Status move is the card's actions control or `Enter` to the Move picker (2.6).

**AC13 — the drag below 768px, and under RTL** (UX-DR41, UX-DR42; `deferred-work.md:574`; Q6)

- A pointer or touch drag below 768px drops only within the visible column. It never hit-tests, previews or drops into a hidden column, and never auto-scrolls the Board sideways.
- At 768px and above, under `dir="rtl"`, a drag toward the inline end reaches the next column in reading order. Auto-scroll near the viewport's inline end scrolls the Board toward it, so drag direction mirrors with the layout.
- 2.7's slop, threshold, pan-wins and cancel rules are unchanged.

**AC14 — crossing the breakpoint** (UX-DR39; Q2)

- When the viewport crosses 768px in either direction (rotation, resize or zoom), the Board neither re-reads nor loses focus.
- Going narrow selects the column holding the Board's active stop (or a carried Task). Focus stays on the same Task identity, or on the same column when it rested on one.
- A carry in progress continues, within its column.
- A drag in progress ends as a cancel ("Returned to …", 2.7's rule).
- Going wide drops the tablist and keeps every loaded card.

**AC15 — the create affordance and Viewers below 768px** (UX-DR27; 2.2 AC2; Q7)

- A Viewer sees the identical pager and no create, drag handle, actions control or Move control. These are absent, not disabled.
- A mover sees the create affordance where Q7 puts it, and it creates into the first effective Status (2.2 AC2). A Task created while another tab is selected makes the pager follow it, as AC5 does.

**AC16 — the context bar at every breakpoint** (UX-DR9, UX-DR41; Q8)

- ≥1280px: Space name, "Switch Space", Project name and Role chip.
- 768–1279px: the Project name is dropped. This is the existing rule, kept.
- <768px: the Project name is dropped. Below 30em (480px), "Switch Space" is dropped *visually*, and the switcher button keeps it as its accessible name.
- At 320px, with a 50-character Space name, under both 1.4.12 and 200% text-only zoom:
  - the Role chip stays visible and its accessible name is unchanged (1.7 / UX-DR28);
  - the bar wraps rather than scrolling sideways;
  - nothing collapses behind a menu;
  - Sign out and Account stay reachable.

**AC17 — the columns at ≥1280px** (UX-DR41; Q9)

- At 1280×800 a Board of **five** Statuses (the "three-to-five" range, `EXPERIENCE.md:417`) shows every column wholly inside the viewport with no horizontal Board scroll.
- At 1024px, the same Board scrolls horizontally inside `.board__columns`, and the page itself does not scroll sideways.

**AC18 — the 1.4.10 audit, measured** (X-11; Q10)

- At 320×256 CSS px (1280×1024 at 400%) and at 375×667, on a Board of three Statuses holding long titles, long Label names (four chips) and a due date, in every tab:
  - `document.scrollingElement.scrollWidth <= clientWidth`;
  - no element inside `main` or the context bar has a horizontal scroll (`scrollWidth > clientWidth` where `overflow-x` is `auto` or `scroll`);
  - the tablist wraps rather than scrolls;
  - the visible column scrolls only vertically.
- The Task detail panel opened at 320px also passes.

**AC19 — text spacing and text-only zoom on the pager** (UX-DR40; `deferred-work.md:20`)

- Under the 1.4.12 override, and separately under 200% text-only zoom, at 320px and with long Status names: no tab clips or overlaps another, the selected tab's state stays visible, and the page never scrolls sideways.
- These are `TextSpacingTests` cases.

**AC20 — purge and storage** (UX-DR31, UX-DR33)

- The selected pager tab is Space-scoped state. It is reset by `Purge()` before any render on a Space switch, a lease end or a sign-out.
- It never enters `localStorage`, `sessionStorage`, IndexedDB, the URL or `document.title`.
- A Board opened again starts on the first tab (Q2).

## Tasks / Subtasks

- [x] **Task 1: decide the open questions** (all ACs)
  - [x] Read Dev Notes → Open questions. Decide each as a reversible decision and record it in Completion Notes as "Qn - (x)", with its cost. Each recommendation is the default.
- [x] **Task 2: viewport awareness without a new injection** (AC3, AC11, AC14; Q1)
  - [x] (a) Add a `matchMedia('(max-width: 47.9375em)')` watcher inside `board-keys.js`. That module is already attached for every Role (`BoardKeys.cs:53-61`). On attach and on every `change` it calls a new `[JSInvokable]` on `BoardKeys` (for example `NarrowAsync(bool)`). `BoardKeys` forwards it to `IBoardKeysHost`.
    - `BoardView` is at S1200's 30-dependency cap: **add no injection** (`ViewToggle.razor:10`; 2.10 Dev Notes). New state lives in a holder type (as `BoardDrag`/`BoardKeys` do) or as plain fields on the partial.
  - [x] (b) Put the breakpoint in **em** in both CSS and JS (`48em` / `47.9375em`, `80em` / `79.9375em`), as `components.css:594-609` already does. No px literal enters the CSS corpus.
  - [x] (c) Remove the listener on dispose. A disposed module must never call into .NET. Copy the existing dispose pattern in `BoardKeys.DisposeAsync`.
- [x] **Task 3: the pager** (AC3, AC4, AC9, AC10, AC11, AC15, AC20; Q1-Q4, Q7)
  - [x] (a) Add a new `Yello.Client/Components/StatusPager.razor`. It takes parameters only (columns: id, name, true total; selected id; `OnSelect`), never an injection. `BoardView.razor` renders it above `.board__columns` **only while narrow**.
    - It renders `role="tablist"` with `aria-label` (Q4 copy), and tabs as native `<button type="button" role="tab">`. Each tab shows the Status name and its true count, reusing the `.column-count` element and class (Q4), so `--radius-full` keeps exactly one CSS consumer (`DesignFoundationGateTests` `:1194`).
  - [x] (b) Render every `BoardColumn` always. **Never unmount a hidden one**: `BoardColumn.razor:285` and `TaskCard.razor:277-288` ignore focus requests on first render, so a remounted column would drop them, and remounting would discard loaded pages.
    - Give the selected column a stable marker (for example `data-pager-selected`) at every width.
    - While narrow, put `role="tabpanel"` and `aria-labelledby` on the column `<section>`, plus `hidden` on the unselected ones.
    - Also add a CSS rule under `@media (max-width: 47.9375em)` that hides `.board-column:not([data-pager-selected])`. That gives the first paint one column (AC11). The rule needs no hover and no `!important`.
  - [x] (c) Make the column full width while narrow (`inline-size: auto` / `flex: 1 1 auto`), with no `.board__columns` horizontal scroll. Logical properties only.
  - [x] (d) Pager keys: the tablist's `←`/`→`/`Home`/`End` are handled in `board-keys.js`, reusing its per-key `rightToLeft` read (`#tell`, `:281-285`). They go to .NET as `BoardKey` values; add `Home`/`End` to `BoardKey.cs` if they are absent. The selection logic is a pure helper: extend `BoardTraversal` or add `PagerTraversal`, and record which. Tab activation is automatic (Q3).
    - Focus moves to the newly selected tab. The Board composite's active stop moves into the newly visible column (its first card, or the column when it is empty), so `Tab` never lands on a hidden element. `ActiveStop` (`BoardView.Keys.cs:63-86`) must never return an element in a hidden column while narrow.
  - [x] (e) Announce on a person's tab change only: Q4's sentence through `Say`, with the true `ColumnState.Total`. Write the copy in `ClientCopy.resx` with a `<comment>` naming 2.11 and the AC. Pluralise as `ColumnCountOne`/`ColumnCountMany` do.
  - [x] (f) Selected-tab state: `null` means the first column. `Purge()` (`BoardView.razor:303`) and `PurgePanel` reset it. Never persist it.
- [x] **Task 4: follow the Task** (AC5, AC14, AC15; Q2, Q5)
  - [x] (a) In `MoveAsync` (`BoardView.razor.cs:325-351`), the one write path, set the selected tab to `choice.StatusId` beside `FocusCardTitle`, before the first await, for **every** origin. A Panel move keeps focus in the panel, and closing the panel then returns focus to the card in the visible column.
  - [x] (b) In `AnnounceMove`'s non-admitted branch (`:369-402`), set the selected tab to the column the card returned to, so the pager follows the Task's identity both ways.
  - [x] (c) A Task created from the first column while narrow selects the first tab (Q7).
  - [x] (d) Going narrow (Task 2's callback): select the column of `_carried`, else of `_activeTaskId`/`_activeColumnId`, else keep the current tab. If a pointer or touch lift is held, end it as a cancel through `EndHeld` (`BoardView.Drag.cs:69-77`) with 2.7's "Returned to …".
- [x] **Task 5: the keyboard and the drag below 768px** (AC12, AC13; Q5, Q6; `deferred-work.md:574`)
  - [x] (a) Pass `narrow` with each key, as `rightToLeft` is passed (read `matchMedia` at the key, as the module reads `direction`). In `BoardTraversal.Traverse` and `Carry`, `InlineStart`/`InlineEnd` move nothing while narrow; the key is still consumed. Carried `↑`/`↓`, `Space` and `Esc` are unchanged.
  - [x] (b) Write unit tests in `tests/Yello.Tests.Architecture/BoardTraversalTests.cs`:
    - narrow traverse and carry with `InlineStart`/`InlineEnd` (nothing moves);
    - narrow `↑`/`↓` unchanged;
    - pager keys under LTR and RTL, with `Home`/`End`, the ends clamped (no wrap, Q3) and the Status order unchanged.
  - [x] (c) In `board-drag.js`, `#layoutOf` (`:396-417`) and `#hitTest` (`:465-488`) consider only columns that are rendered. Skip a column whose box is empty, or that has `hidden`, or that lacks `data-pager-selected` while narrow. Auto-scroll's sideways step (`:452-455`) does nothing while narrow. Update the header comment (`:32-35`) to say what 2.11 decided.
  - [x] (d) RTL drag at 768–1279px: prove with a browser case that hit-testing (rect-based, direction-neutral) and the sideways auto-scroll (`edgeStep` on physical `clientX`, then `scrollBy({ left })`) carry a drag to the inline-end column under `dir="rtl"`. Chromium's RTL `scrollLeft` is negative and `scrollBy` is physical, so the pair is consistent. Change the code only if the case fails, and record what you did. The lifted card's `translate: var(--drag-x)` follows the finger's physical delta, which is correct in both directions.
- [x] **Task 6: the context bar** (AC1, AC2, AC6, AC16; Q8)
  - [x] (a) Widen the existing Project-name rule (`components.css:605-609`) to every width below 80em.
  - [x] (b) Below 30em, visually hide `.context-bar__switch-label` with the existing visually-hidden utility, so the switcher button's accessible name still includes it. Never use `display: none`, and never remove the Role chip.
  - [x] (c) Confirm that `.context-bar` wraps (`flex-wrap`) and has no fixed width. A long Space name wraps inside `.context-bar__space-name`; use `overflow-wrap: anywhere` if needed. Never use `text-overflow: ellipsis` on the Space name, which would lose content.
  - [x] (d) Radius sweep (`deferred-work.md:36`): add `The_context_bar_takes_the_3px_radius` and `The_Role_chip_takes_the_2px_radius` to `DesignFoundationGateTests.Board.cs` (or a new partial `.SmallViewport.cs`). Fix the CSS if either fails. Prove each against a plant.
- [x] **Task 7: the columns at ≥1280px** (AC1, AC17; Q9)
  - [x] (a) At ≥80em, let columns share the row: `flex: 1 1 0` with `min-inline-size` in rem, sized so five fit in 1280px less the Board padding and gutters. Arithmetic: 1280 − 2×12 − 4×9 = 1220px; 5 × 14rem = 1120px fits. Between 48em and 80em keep `17rem` and the horizontal scroll.
  - [x] (b) `FakeYelloApi` may already accept a Status list from the test (`DragHarness.Seed(..., statuses, ...)`). Use five Statuses there; no contract change.
- [x] **Task 8: tests** (all ACs; see Testing requirements)
  - [x] (a) Add a new `tests/Yello.Tests.Client/SmallViewportTests.cs` with the cases listed in Dev Notes → Testing requirements. Reuse `DragHarness`, `KeyboardHarness` and `FakeYelloApi`. **No new fixture type.**
  - [x] (b) Add to `TextSpacingTests`: the pager under both adaptations at 320px (AC19), and the context bar at 320px with a 50-character Space name (AC16).
  - [x] (c) Add to `TouchDragTests` (in `TouchTimingCollection`): a long-press reorder within the visible column at 375px, and a drag toward a hidden column's direction that drops nothing outside the visible column (AC13).
  - [x] (d) Add the gates listed in Dev Notes → Testing requirements, each proved against a plant.
  - [x] (e) Re-run the existing narrow cases unchanged and keep them green: `DragTests:603-615` and `MoveTests:581-593` (320/768/1280), `LabelTests:146` (320), `ListViewTests` (320), and `DragTests:692` (768, sideways auto-scroll, still valid at 768). If a 320 case relied on a second column being visible, correct the case to select its tab first, and record the correction in the Debug Log.
- [x] **Task 9: ledger, conventions, verification**
  - [x] (a) `deferred-work.md`: apply Dev Notes → Ledger dispositions.
  - [x] (b) `tests/TESTING-CONVENTIONS.md`: add a short "Viewport and the pager - added by story 2.11" subsection covering:
    - the breakpoint sizes the suite uses (320×256, 375×667, 1024×800, 1280×800);
    - how to select a tab before acting in a hidden column;
    - the 1.4.10 measurement helper;
    - that RTL is forced by `dir="rtl"`.
  - [x] (c) Run `dotnet build Yello.slnx` (0 warnings, 0 errors), then `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Record the totals. The expected skips are 3 bundle cases plus the Explicit tiers.

### Review Findings

Code review of `f37b5b9..06d2a07` (2026-10-28, bmad-code-review, unattended): Blind Hunter, Edge Case Hunter and Acceptance Auditor. 38 raw findings, 25 after de-duplication: 8 patched, 3 deferred, 14 dismissed. No decision was needed.

- [x] [Review][Patch] **The `hidden` attribute did nothing on a column, and a focused unselected column stayed drawn and in the accessibility tree** (high; AC11) [`Yello.Client/wwwroot/css/components.css` "THE STATUS PAGER"]
  - `.board-column { display: flex }` beats the user agent's `[hidden]` rule, and no `[hidden]` rule existed. The only hide was `:not([data-pager-selected]):not(:focus-within)`.
  - Any unselected column holding focus was therefore drawn, and exposed to assistive technology, beside the selected one. This happened with focus in the create field, after a tap that does not focus the tab (WebKit), or when a refused move followed back.
  - Fixed with `.board-column[hidden] { display: none }` inside the narrow query, which makes .NET's `hidden` final once the pager is drawn. The `:focus-within` spare still covers the gap before .NET knows the width.
- [x] [Review][Patch] **Going narrow ignored focus that was not on a roving stop** (medium; AC14) [`BoardView.Pager.cs` `GoNarrow`/`ColumnToShow`, `board-keys.js` `#onMedia`, `BoardKeys.Narrow`]
  - Focus in column 1's create field while the active card was in column 3 selected column 3.
  - The keys module now reports the column holding focus, and going narrow selects that column after a carried Task's column and before the active stop's.
- [x] [Review][Patch] **A refused or unanswered move moved the pager back over a tab the person had chosen since** (medium; AC5) [`BoardView.razor.cs` `Answered`, `BoardView.Pager.cs` `ShowsColumnOf`]
  - The pager now follows the returning card only while it still shows the column the move took the Task to.
- [x] [Review][Patch] **A tab chosen during a keyboard carry left the carry alive in the hidden column, and Esc on a tab could not cancel it** (medium; AC9, AC12) [`BoardView.Pager.cs` `Choose`]
  - The person's change of tab now returns the carried Task, and says so as focus leaving a carry always does.
- [x] [Review][Patch] **A card's menu left open in a hidden column blocked `Space` pick-up in the shown one** (medium) [`BoardView.Pager.cs` `Choose`]
  - The person's change of tab now closes it.
- [x] [Review][Patch] **The empty Board's create action, pressed on another tab, opened the create field inside the hidden first column** (medium; AC15) [`BoardView.razor` `OpenCreate`]
  - Opening create now selects the first tab.
- [x] [Review][Patch] **A clicked tab assumed the browser had focused it** (low) [`BoardView.Pager.cs` `SelectTab`]
  - WebKit does not focus a clicked button, so focus could stay in the column now hidden. A click now asks for focus on the selected tab.
- [x] [Review][Patch] **AC clauses with no test** (medium) [`tests/Yello.Tests.Client/SmallViewportTests.cs`, `TextSpacingTests.cs`]
  - Added `A_clicked_tab_is_said_and_Tab_reaches_the_visible_column`, which covers AC9 `Tab` and `Shift+Tab`, AC10 click, and nothing said on load.
  - Added `Crossing_768px_during_a_carry_keeps_it_in_its_own_column` (AC14 carry).
  - Added `Crossing_768px_shows_the_column_holding_focus_even_off_a_stop`, `A_tab_chosen_during_a_carry_returns_the_Task` and `On_an_empty_Board_the_create_action_shows_the_first_tab`.
  - The crossing case now asserts that a crossing says nothing (AC10).
  - The 320px context-bar text-spacing/zoom case now asserts the Role chip's unchanged name, Sign out and Account, no menu, and no sideways page scroll (AC16).
  - Plants: removing the focused-column selection, the carry return, or the create-selects-first-tab change was each caught by its new case.
- [x] [Review][Defer] **Going narrow during a carry moves the destination back to its own column silently** [`BoardView.Pager.cs` `GoNarrow`]. Deferred: AC14 requires the reset; whether to say the new position is UX's (owner: UX).
- [x] [Review][Defer] **A selected Status that leaves the effective set is kept, and the pager's tab list is rebuilt on every render** [`BoardView.Pager.cs` `SelectedColumn`, `PagerTabs`]. Deferred: not reachable until Statuses are configurable (owner: story 6.1 / 6.2).
- [x] [Review][Defer] **Defence-in-depth layers that no test can fail** [`board-drag.js` `isTarget` and the narrow sideways guard, `BoardView.Keys.cs` `Traverse`'s `narrow`, `BoardView.razor.cs` `PurgePanel`'s reset, the new `.board-column[hidden]` rule; the drag-cancel-on-crossing branch of AC14]
  - The CSS hide or the keyed remount masks each of these.
  - The `scrollLeft == 0` assertions in the narrow drag cases cannot fail, because the row is `overflow-x: visible` below 48em.
  - Deferred (owner: Lee, test hardening).

## Dev Notes

### The shape of the story in one paragraph

2.11 is entirely client-side. It changes three things.

- **The CSS** decides what is visible at each width, by construction. A media query in em hides every unselected column below 48em, so a 320px first paint is already one column.
- **.NET** learns the width from a `matchMedia` watcher in the existing keys module. It then renders the ARIA (tablist, tabs, tabpanel, `hidden`) and the behaviour:
  - pager keys and announcement;
  - `←`/`→` inert in the composite;
  - follow-the-Task on every move;
  - selection on crossing the breakpoint.
- **The drag** stops hit-testing hidden columns.

Every column stays mounted, so paging, true totals, focus identity and 2.9's append-only rule are untouched. The context bar gets one widened rule and one visually-hidden label. No server, contract, migration or isolation change.

### What exists today (read before writing; from a survey of the tree at `f37b5b9`)

**No viewport awareness anywhere.**

- `matchMedia`, `ResizeObserver`, `768`, `1280` and `tablist` do not occur in `Yello.Client`.
- `innerWidth` occurs once, in the drag's auto-scroll (`board-drag.js:452`).
- No `role="tab"` exists. `base.css:250` already lists `[role="tab"]` in the 24px target-floor selector.

**`BoardView.razor`** (549 lines):

- `:65` `LiveAnnouncement`.
- `:83` `.board`, the keys module root.
- `:105` `.board__columns`, the drag root.
- `:106-143` one `BoardColumn` per Status from `WithActiveStop(columns)`. `ShowAdd` is set on the first column only.
- `:146-167` `TaskDetail` with `OnMove → MoveAsync(…, MoveOrigin.Panel)`.
- `:303` `Purge()`.
- `:340` `ActiveSpace.ShowProject`, which feeds the context bar.
- `:450-457` `Say`, which joins sentences said before the next render.
- `ColumnState` (`:529-548`): `StatusId`, `Name`, `Total` (the true total), `Tasks` (the loaded prefix), `After`, `Loading`.

**`BoardView.razor.cs`:**

- `MoveAsync` (`:325-351`) is **the one write path**. It records `_moveOrigins`, calls `FocusCardTitle` for Menu and Keyboard origins, then runs the optimistic `Place()` (`:974-1036`) before the first await.
- `AnnounceMove` (`:369-402`) says "Moved to {0}, position {1} of {2}." on admission. Otherwise it says "Returned to {0}, position {1}." and calls `RefocusAfterAnswer`.
- `PurgePanel` (`:209-241`).
- `Deleted` (`:562`) focuses the column when its last card goes.

**`BoardView.Keys.cs`:**

- `ActiveStop` (`:63-86`).
- `KeyAsync` (`:149-177`).
- `Traverse` (`:223-256`).
- `PickUp` (`:307-327`).
- `CarryKeyAsync` (`:332-352`).
- `StepAsync` (`:360-393`) calls `BoardTraversal.Carry` at `:375`. **This is where a narrow carried `←`/`→` must move nothing.**
- `DropCarriedAsync` (`:414-445`).

**`BoardTraversal.cs`** (pure, unit-tested in Architecture):

- `KeyOf(key, rightToLeft)` (`:29-38`) and `Horizontal` (`:114-115`) map the arrows logically.
- `Traverse` (`:51`; InlineStart/End `:68-78`) and `Carry` (`:94`; `:102-107`).
- The `BoardKey` enum: `Other, Up, Down, InlineStart, InlineEnd, Space, Escape, Tab`.

**`BoardView.Drag.cs`:** `EndHeld` (`:69-77`), `DropAsync` (`:113-139`), `Returned` (`:153-162`).

**`BoardView.Paging.cs`:**

- `ShowMoreAsync` (`:230-249`) focuses the first new card.
- `Column(statusId)` (`:218`).
- Nothing is announced on paging.

**`BoardColumn.razor`:**

- `:42`: `<section class="board-column" aria-labelledby aria-describedby data-status-id tabindex>`.
- The head (`:43-52`) holds the `h2`, `.column-count` (`aria-hidden`), a visually hidden `ColumnCountOne/Many`, and `+` (`TaskAdd`, only when `ShowAdd`).
- `:56-91`: the `ul` of `TaskCard` `li` elements, each with `aria-posinset`/`aria-setsize`, interleaved with drop zones.
- `:94-101`: Show more.
- **`:285` ignores a `FocusColumnRequest` on first render**, and `TaskCard.razor:277-288` ignores `FocusRequest` on first render (only `FocusOnArrival` counts). This is why columns must never be unmounted by the pager.

**`board-keys.js`:**

- `ARROWS` (`:45`).
- `#tell` (`:281-285`) reads `getComputedStyle(root).direction === 'rtl'` **at each key**. Pass `narrow` the same way.
- The stops are `[data-task-id]` titles and `section[data-status-id]` (`:38-40`).
- `reveal()` (`:68-74`).

**`board-drag.js`** (656 lines):

- The header comment `:32-35` says "direction-neutral, so RTL needs nothing here (UX-DR42 is story 2.11's)".
- `clientX` at `:240`, `:242`, `:278` and `:507`.
- `centreOf`/`inside` use `rect.left`/`rect.right` (`:91-97`).
- On lift, `--drag-start` is set to `clientWidth - box.right` in RTL, else `box.left` (`:308-319`).
- `#layoutOf` (`:396-417`) reads every `[data-status-id]`. `#hitTest` is at `:465-488`.
- `#autoScroll` (`:437-463`) runs `edgeStep(x, 0, innerWidth)` and then `root.scrollBy({ left })`.
- Caches are invalidated by a MutationObserver and by scroll and resize (`:348-351`).

**CSS** (`components.css`):

- `:241-257` `.context-bar` (sticky, `flex-wrap`, `justify-content: space-between`).
- `:289-291` `.context-bar__switch-label`.
- `:600-609` `.context-bar__project`, with `@media (min-width: 48em) and (max-width: 79.9375em) { display: none }`.
- `:619-626` `.board` (`padding-inline: var(--space-4)`).
- `:644-649` `.board__columns` (`display: flex; gap: var(--space-gutter); overflow-x: auto`).
- `:657-671` `.board-column` (`flex: 0 0 auto; inline-size: 17rem; max-block-size: 75vh; overflow-y: auto`).
- `:1520` 2.10's `@media (max-width: 29.9375em)`.
- No breakpoint tokens exist (custom properties do not work in media queries); the em literals are repeated by convention.

**The context bar.**

- `ContextBar.razor`:
  - `:54` `<nav class="context-bar" tabindex="-1" aria-label>`;
  - `:56-68` the switcher button, holding the Space name and `.context-bar__switch-label` ("Switch Space", `ClientCopy.resx:239-242`);
  - `:70-73` the Project name;
  - `:75-78` `RoleChip`;
  - `:81-84` Account and Sign out.
- **The "switcher chevron label" is that "Switch Space" span.** There is no chevron glyph in the markup.
- The Offer indicator does not exist.

**Gates that already hold most of AC8:**

- `DesignFoundationGateTests.No_physical_left_or_right_property_is_used_where_a_logical_one_exists` (`:1590-1633`; property list `:230-275`, 3/4-value shorthands refused). It does **not** cover `overflow-x`, `translate`, `scroll-snap-*` or JavaScript (Q11).
- `Pixel_lengths_outside_the_token_layer_come_from_a_token` (`:778`) scans declarations only. At-rule heads are `AtRulePath`, unscanned (`CssCorpus.cs:1031-1044`).
- Also: `No_rule_reveals_or_hides_anything_on_hover_alone` (`DesignFoundationGateTests.MovePicker.cs:67`), `KeyboardGateTests` (application mode, tabindex > 0, keys for every Role and drag for movers only), and `BoardTraversalTests.Arrow_keys_map_to_logical_directions_and_mirror_under_right_to_left` (`:86`).

**Tests that already run the Board at narrow widths:**

- `DragTests:603-615` and `MoveTests:581-593` (320/768/1280: Move and actions visible without hover).
- `LabelTests:146` (320×800).
- `DragTests:692` (768, sideways auto-scroll).
- `TouchDragTests:110` (1024).
- `ListViewTests` (320).
- `KeyboardTests:171-189` (RTL).
- The default Playwright viewport is 1280×720. `ClientSurfaceFixture.OpenAsync` (`:79-95`) sets none.

**`ClientCopy.resx` patterns.** `TaskMoved` (`:594`), `TaskReturned` (`:602`), `TaskPickedUp` (`:606`), `TaskCarried` (`:610`), `ColumnCountOne` / `ColumnCountMany` (`:614`, `:618`: "1 Task" / "{0} Tasks"), `BoardShowMoreName` (`:670`), `ViewToggleName` (`:854`: "Views"). Each entry has a `<comment>` naming the story and AC; formatting uses `string.Format(CultureInfo.CurrentCulture, Localizer[key], …)`. Literal gates: `DesignFoundationGateTests` `:1312` and `:1389`.

### What must be preserved (regression guard)

- **≥768px behaviour is 2.6 to 2.10's, byte for byte.** That covers the arrow grammar across columns, cross-column carry and drag, sideways auto-scroll at 768, the Move picker, paging, focus identity and announcements. Every existing `KeyboardTests`/`DragTests`/`MoveTests`/`BoardPagingTests` case stays green unchanged.
- **One write path.** Pager-follow is a selection change, never a second move.
- **Focus identity (X-5).** A tab change, a breakpoint crossing or a follow never re-points an element at another Task, never unmounts a column and never re-reads the Board.
- **One tab stop for the Board composite** (2.8 AC7), plus one for the tablist. The skip link, the context bar's stops and the view toggle keep their order.
- **The radius-full gate:** exactly one CSS consumer (`.column-count`). The pager reuses that class, not the token.
- **No hover-only rule, no fixed height, no px outside tokens, no shadow, reduced motion respected.** The pager has no transition, or one inside the reduced-motion neutraliser.

### Open questions (decide in Task 1; each is reversible; record as "Qn - (x)")

1. **How the narrow state is known and how columns are hidden.**
   - (a) CSS hides unselected columns by a media query, by construction. `board-keys.js` reports `matchMedia` changes to .NET through `BoardKeys`, and .NET renders the ARIA and the behaviour. All columns stay mounted.
   - (b) .NET only: it renders one column and unmounts the rest. Remounting loses focus requests (`BoardColumn.razor:285`) and loaded pages, and the first paint at 320 is wrong until .NET knows.
   - (c) CSS only, with permanent `tabpanel` roles. The ARIA would be wrong at ≥768 and the behaviour (AC5, AC12) needs .NET anyway.
   - **Recommend (a).** Cost: a CSS media rule and a JS listener must agree on `47.9375em`. A gate holds the CSS literal (Testing requirements) and a comment cross-references the JS.
2. **Which tab is selected.**
   - (a) The first effective Status on load. It is kept across Board re-reads and paging. Going narrow selects the active stop's column (AC14). A move follows the Task (AC5). It is reset by `Purge()`. If the selected Status is absent from a re-read set (unreachable until Epic 6), fall back to the first.
   - (b) Remember it in the URL or in storage. That is forbidden by UX-DR31/33.
   - **Recommend (a).**
3. **Tab activation and the ends.**
   - (a) Automatic activation: the arrow selects the tab it lands on, because AC4 says announce "on change" as focus moves. The ends **clamp** and do not wrap, matching 2.8's column traversal clamping. `Home`/`End` are supported.
   - (b) Manual activation (arrow, then `Enter`/`Space`).
   - (c) Wrapping.
   - **Recommend (a).** Each change costs nothing to render (the columns are mounted), which is the APG condition for automatic activation. Cost: ledger to UX for confirmation (clamp versus wrap).
4. **The pager's words.**
   - (a) The tablist's `aria-label` is "Statuses". Each tab's visible text is the Status name followed by the `.column-count` element showing the true total. The tab's accessible name is the name plus `ColumnCountOne`/`ColumnCountMany` ("In Progress, 12 Tasks"); give the chip `aria-hidden` and add visually hidden text, as `BoardColumn` does.
     - The announcement on a person's change is "{Status}, {count phrase}." For example "In Progress, 12 Tasks.", from a new key such as `PagerShown` = "{0}, {1}." composed with `ColumnCountOne`/`ColumnCountMany`.
   - (b) Name only on the tab; count only in the announcement.
   - **Recommend (a).** The visible and the spoken totals agree (X-6's principle), and the count is the true total. Cost: the copy needs UX confirmation; ledger it.
5. **The keyboard composite below 768px.**
   - (a) `←`/`→` are consumed and move nothing, both traversing and carrying. A carry is still allowed for in-column reorder. Nothing is announced on a refused carried `←`/`→`, because the carry's position is unchanged and the person heard it on pick-up. Cross-Status moves go through the Move picker (EXPERIENCE.md:320).
   - (b) `←`/`→` in the composite switch the pager tab. That makes the composite a second route to a Status, against UX-DR20's "*only* route", and a carried Task would then cross into another column, which AC5 forbids.
   - (c) Disable `Space` pick-up entirely while narrow. That loses in-column keyboard reorder, an X-4 equivalent of the in-column drag that AC13 keeps.
   - **Recommend (a).** Cost: ledger to UX whether a refused carried `←`/`→` should say something (for example "Use Move to change Status.").
6. **The drag below 768px.**
   - (a) Hit-test only rendered columns (a hidden column's rect is empty; check explicitly too). No sideways auto-scroll while narrow. The lift, the slop and the cancel rules are unchanged.
   - (b) Let a drag to the viewport's inline edge switch tabs. That is a gesture route to another Status, which AC5 forbids.
   - **Recommend (a).** Closes `deferred-work.md:574`.
7. **Where the create affordance is while narrow.**
   - (a) It stays on the first effective Status's column, and so it is visible only on the first tab, because creation always takes the first Status (2.2 AC2) and the `+` sits on that column's head.
   - (b) Show it on every tab's column head, still creating into the first Status. The new Task would then appear in a column other than the one on screen; the pager would follow (AC15).
   - **Recommend (a).** It is honest about where the Task lands, and keeps 2.2's rule. Cost: on any other tab, a person returns to the first tab to add. Ledger to UX.
8. **The context bar's drop order.**
   - (a) Drop the Project name below 1280px (widen the existing rule). Below 30em, visually hide "Switch Space" while keeping it as the button's accessible name. Never touch the Role chip.
   - (b) Drop "Switch Space" below 768px.
   - (c) Truncate the Space name with an ellipsis. That loses content, which 1.4.10 and 1.4.12 forbid.
   - **Recommend (a).** "If something must be dropped" applies only where the bar would otherwise crowd. At 30em the bar wraps anyway (`flex-wrap`), so the label goes only where it buys room. Cost: there is no chevron glyph today (DESIGN.md:454 names "a switcher chevron"); ledger the glyph to UX rather than invent one.
9. **The columns at ≥1280px.**
   - (a) `flex: 1 1 0` with `min-inline-size: 14rem` at ≥80em, so three to five Statuses all show without scrolling (AC1, AC17). 17rem fixed columns with horizontal scroll stay at 48–80em.
   - (b) Keep 17rem fixed everywhere. Five Statuses (5 × 272 + gutters > 1256px) would scroll at 1280px and fail AC1.
   - **Recommend (a).** Cost: at ≥80em with more than five Statuses (Epic 6 allows more), columns would overflow and the Board would scroll. Ledger to UX/6.1.
10. **The 1.4.10 audit method.**
    - (a) A browser case at 320×256 (exactly 1280×1024 at 400%) and 375×667. Assert:
      - `scrollingElement.scrollWidth <= clientWidth`;
      - no element under `main` or `.context-bar` with `overflow-x` `auto`/`scroll` and `scrollWidth > clientWidth + 1`;
      - every tab's panel in turn;
      - the panel opened.
      Plant `.board__columns { overflow-x: auto }` with a 17rem column at narrow width: it must be caught.
    - (b) A real 400% browser zoom. Playwright cannot set page zoom; `deviceScaleFactor` is not zoom.
    - **Recommend (a).** The CSS viewport is what 1.4.10 measures.
    - Cost: the 256px height makes the column's own `max-block-size: 75vh` (192px) the vertical scroller. That is one-dimensional and passes, but it is cramped. Ledger to UX whether the column should stop scrolling itself while narrow and let the page scroll (UX-DR8 says "never the page").
11. **How far the logical-property gate reaches.**
    - (a) Leave `overflow-x`/`overflow-y` and `translate` as they are. They name a physical *axis*, not a *side*: in horizontal writing modes the inline axis is horizontal in LTR and RTL alike. Prove RTL with the browser cases instead (AC8, AC13; 2.8's arrow case, AC9's pager case, Task 5 (d)'s drag case).
      - Add the gates that matter here: breakpoints stated in em, and only at 48em/80em plus 2.10's 30em; and no physical side keyword in `scrollIntoView`/`scrollBy` calls in `wwwroot/js`, except the one `scrollBy({ left })` that Task 5 (d) proves under RTL. Allow-list that one call with a comment.
    - (b) Switch to `overflow-inline`/`overflow-block`. Browser support is not certain on the pinned Chromium; verify before choosing.
    - **Recommend (a).** Cost: none.
12. **Pager visuals (DESIGN.md has no `status-pager` component).**
    - (a) Tabs reuse `button-secondary`'s tokens: transparent background, hairline border, `--radius-sm` (2px), `control-pad`, `min-block-size: var(--target-min)`, `typography.column-head` for the name.
      - The selected tab adds `--border-emphasis-width` and `text-primary`. Selection is never conveyed by colour alone (1.4.1): the 2px border plus `aria-selected`.
      - The tablist is a wrapping flex row (`flex-wrap: wrap`, `gap: var(--space-2)`). It never scrolls sideways.
      - Add a gate clause `The_Status_pager_tabs_take_the_2px_radius` (`deferred-work.md:36` mapping).
    - (b) An underline-style tab strip. That needs a new token for the indicator.
    - **Recommend (a).** Cost: ledger to UX for a visual spec.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

| Entry | Disposition | Owner |
|---|---|---|
| `:574` Below 768px a drag may cross into any column | **Close** with Q6 and the AC13 cases (narrow-drag and RTL-drag), each with its plant | 2.11 |
| `:20` 1.4.12 / 200% measurement | **Update**: the pager and context-bar cases added (AC19, AC16) | (stays open for later surfaces) |
| `:36` Radius mapping | **Update**: context bar 3px, Role chip 2px and pager tabs 2px clauses added; still unmapped are the buttons (3px), the Offer indicator (Epic 5), other dialogs and the invitation view (Epic 4) | epic owners as listed |
| `:573` iOS device check / tap-to-select | **Touch**: 2.11 adds Chromium-emulated narrow touch reorder; a real device is still unverified | Lee, UX/Lee |
| The pager's copy, clamp-not-wrap, and tab visuals (Q3, Q4, Q12) | **Add** | UX |
| A refused carried `←`/`→` below 768px says nothing (Q5) | **Add** | UX |
| The create affordance shows only on the first tab below 768px (Q7) | **Add** | UX |
| No switcher chevron glyph exists; "Switch Space" is visually hidden below 30em (Q8) | **Add** | UX |
| More than five Statuses at ≥1280px scroll (Q9) | **Add** | UX / story 6.1 |
| The column's own scroll at a 256px-high viewport (Q10) | **Add** | UX |
| The Offer indicator must survive every breakpoint; extend `SmallViewportTests`' context-bar case when it is built | **Add** | story 5.1 |

### Architecture compliance (must)

- **No server-side change.** No new route, contract, handler, migration or isolation row. The pager reads nothing; it presents `ColumnState`, which 2.9 already loads.
- **AD-29.** Append, never recycle. Never unmount a loaded column. True totals come from `ColumnState.Total`. Nothing is announced on load or paging.
- **AD-11 / one write path.** Every move goes through `MoveAsync`, so pager-follow is a UI selection only.
- **UX-DR31.** The pager's selection is purged with the Space and never stored.
- **S1200 cap.** No new injection into `BoardView`. Use a holder type or parameters.
- **AR-34.** No Status name or Task title in any log line, including JS `console` output.
- **Client purity.** Copy goes in `ClientCopy.resx` only. Lengths come from tokens or em/rem. Logical properties only.

### Library / framework

- No new packages. Use Blazor WebAssembly 10.0.11, Playwright 1.62.0 (Chromium, pinned) and xunit.v3 4.0.0, as pinned in `Directory.Packages.props`.
- `window.matchMedia(q).addEventListener('change', …)` is standard in the pinned Chromium. Do not use the deprecated `addListener`.
- Playwright cannot apply browser page zoom. Use `SetViewportSizeAsync` for the CSS viewport (Q10). `hasTouch` contexts come from `ClientSurfaceFixture.OpenAsync(locale, timezoneId, hasTouch)`.
- Do not use `Microsoft.AspNetCore.Components.Web.Virtualization` (gated).

### Testing requirements

**Traits.** Exactly five traits: `Suite`, `Priority`, `Requirement` and `Assumption` (unused here), plus `Tier` (none here; all PR tier). Use `Requirement` values `UX-DR20`, `UX-DR41`, `UX-DR42` and `NFR-9`, with the AD where one applies (`AD-29` for the identity and total cases). Name tests as underscore sentences. Use no `Task.Delay`: poll with a deadline, and use hold, assert, release, assert for moves.

**`SmallViewportTests` (Client, new class, `ClientSurfaceCollection`).** Build `Yello.Client` in **Debug** before running. Every case runs at a stated viewport.

1. At 1280×800 with **five** Statuses, every column's box lies inside the viewport and `.board__columns` has no horizontal scroll. The context bar shows Space, Project and Role chip (AC1, AC17).
2. At 1024×800 the columns sit side by side, `.board__columns` scrolls horizontally, the page does not, the Project name is hidden, and Space and Role are visible (AC2, AC17).
3. At 375×667 (AC3, AC9, AC11):
   - `role=tablist` with one tab per Status in effective order;
   - exactly one `aria-selected=true`, with `aria-controls` pointing at a `role=tabpanel` labelled by its tab;
   - only that column is visible; the others are `hidden` and absent from the accessibility tree;
   - the tablist is one tab stop.
4. Arrows, `Home` and `End` on the tablist move focus and selection and clamp at the ends. Each change says "{Status}, {n} Tasks." once, with the **true** total: use `FakeYelloApi.PageSize` with 120 Tasks in a column and 50 loaded, so the sentence says 120. The same sentence is said twice on a return (`WaitUntilSaidAsync(…, 2)`) (AC4, AC10).
5. Under `dir="rtl"`, `ArrowRight` on the tablist goes to the previous Status, and the tab order in the DOM and the accessibility tree is unchanged (AC8, AC9).
6. At 375, moving a Task to another Status from the card's actions control and Move picker selects the destination tab and puts focus on the card's title (AC5):
   - while the move is held, the destination tab is already selected;
   - on admission, "Moved to …" is said and no tab-change sentence is;
   - with the move released refused, the tab follows the card back.
   - Repeat from the Task detail panel's Move control: on close, focus returns to the card in the visible column.
7. At 375, keyboard (AC12):
   - `←`/`→` on a card move nothing and are prevented;
   - `Space`, carried `↓`, `Space` reorders within the column ("Moved to {same Status}, position 2 of n.");
   - a carried `→` leaves the Task in its column;
   - the X-4 cross-Status equivalent is `Enter` on the actions control, then the Move picker.
8. At 375, a pointer drag of a card toward the inline edge drops within the visible column only; `Moves(api)` shows no cross-Status move (AC13).
9. At 1024 under `dir="rtl"`, a pointer drag to the inline-end column auto-scrolls the Board toward it and drops there (AC13; closes `:574`).
10. A resize from 1280 to 375 with focus on a Task in the third column selects the third tab, keeps focus on that Task, and re-reads nothing (no new column requests recorded). A resize back shows all columns with every loaded card (AC14).
11. At 375 a Viewer sees the pager, and no create, actions or Move control. Selection and the announcement work (AC15).
12. At 375 a mover sees the create affordance on the first tab only. A Task created there appears in the first column (AC15, Q7).
13. At 320×800 with a 50-character Space name, the Role chip is visible with its accessible name unchanged, and "Switch Space" is not visible but is in the switcher's accessible name. The page does not scroll sideways (AC6, AC16).
14. The 1.4.10 audit at 320×256 and 375×667: every tab in turn, then the Task detail panel. Use the Q10 assertions (AC7, AC18).
15. Purge (AC20):
    - select the second tab and switch Space: the new Board starts on its first tab;
    - Web Storage holds nothing new;
    - `location` and `document.title` carry no Status.
16. Smoke across `BrowserLocales` (four locales): the pager renders and its `aria-label` is localised copy.

**`TextSpacingTests` (additions):**

- The pager at 320px, under 1.4.12 and separately 200% text-only zoom, with long Status names: no tab clipped or overlapping, the selected tab still distinguishable, and no sideways page scroll (AC19).
- The context bar at 320px with a 50-character Space name, under both adaptations (AC16).

**`TouchDragTests` (additions, `TouchTimingCollection`):**

- At 375, a long-press reorder within the visible column.
- A touch drag toward the inline edge drops nothing outside the visible column (AC13).

**Architecture (unit and gates):**

- `BoardTraversalTests`: narrow `InlineStart`/`InlineEnd` (traverse and carry) move nothing; narrow `↑`/`↓` unchanged; pager keys LTR and RTL with `Home`/`End` and clamping (Task 5 (b)).
- `DesignFoundationGateTests` (a new partial `.SmallViewport.cs` is fine):
  - `Breakpoints_are_stated_in_em_at_the_documented_widths_only`: every `@media` width condition is in `em` and is one of 30em/29.9375em, 48em/47.9375em, 80em/79.9375em.
  - `Below_768px_an_unselected_column_is_hidden_by_construction`: the media rule exists.
  - `The_Status_pager_tabs_take_the_2px_radius`, `The_context_bar_takes_the_3px_radius` and `The_Role_chip_takes_the_2px_radius`.
  - `The_Role_chip_is_never_hidden_at_any_breakpoint`: no rule inside any `@media` sets `display: none`, `visibility: hidden` or a visually-hidden pattern on `.role-chip` or its container.
- A JS scan: `No_script_scrolls_by_a_physical_side_except_the_proved_one` (Q11).
- `KeyboardGateTests`: the existing application-mode and tabindex gates cover the pager. Confirm they scan `StatusPager.razor`.

**Plants** (each restored in a `finally` from saved content, never by git; record the catch rate). At least:

- the media rule removed (case 3/14 caught);
- an announcement on a Board-made follow (case 6 caught);
- the pager announcing `Tasks.Count` instead of `Total` (case 4 caught);
- `←` in the narrow composite switching columns (case 7 and the unit test caught);
- the drag hit-testing hidden columns (case 8 caught);
- `display: none` on the Role chip below 30em (gate and case 13 caught);
- a px breakpoint (gate caught);
- `overflow-x: auto` and 17rem kept at narrow width (case 14 caught);
- the pager selection not purged (case 15 caught);
- a 3px pager tab (radius gate caught).

**Running.**

- The Client suite needs no SQL Server.
- Iterate with `dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build --filter …`. Never run two `dotnet test` processes at once.
- The full run is `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`.
- Rebuild after every change (`--no-build` serves stale binaries). Leftover `Yello.Host.exe` processes can lock the build (`Get-Process Yello.Host`).
- Known flake source: 8 Client failures under a full run that pass alone (memory note "Full test run starves the browser suite"). Rerun the Client project alone before calling a failure real.

### Previous story intelligence (2.10, 2.9, 2.8, 2.7)

- **2.10.**
  - Breakpoints in **em** (`29.9375em`), and the table reflow kept its semantics in the DOM (`display: block` under the media query, header clipped for assistive technology, never removed). Use the same discipline for the pager.
  - Its review found a case-sensitive address match and a `display: flex` that broke table layout: small CSS slips caught only by a browser case. Write the browser case first.
  - Its run: 1,466 total, 1,457 passed, 0 failed, 9 skipped.
- **2.9.**
  - Focus follows identity, and a column's loaded prefix is precious: re-reading pages a column from the top.
  - "Show more" focuses the first new card.
  - `RestoreDepthAsync` pages a column back after a re-read. Make sure a pager change never triggers it.
- **2.8.**
  - The keys module reads `direction` at each key; do the same for narrowness.
  - `LiveAnnouncement` has two alternating regions so a repeated sentence is heard.
  - "Two sentences said before one render are joined in one entry", so a move plus a tab change in one render would join. AC10 forbids the tab sentence on a Board-made change for this reason too.
  - Wait for the keys module before the first key (`WaitForKeysAsync`).
- **2.7.**
  - Long-press cases measure time and must stay in `TouchTimingCollection`.
  - The drop zone reaches the page one render behind the pointer, so wait for it (`WaitForDropZoneAsync`) before releasing.
  - Hit-testing is rect-based.
  - Q15 handed the narrow drag limit to this story.
- **Memory notes:**
  - Never revert a plant with `git checkout --`.
  - The browser suite starves under a full run; rerun alone.
  - WSL is capped at 8 GB; one test project at a time.

### Git intelligence

- `f37b5b9` / `9a742fe` (2.10): the List View page, `ViewToggle`, `list-keys.js` (a separate module rather than a mode bent into `board-keys.js`), the 30em reflow in `components.css`, and `ListViewTests` with 320px cases. The pattern to copy for a narrow-width browser case is `ListViewTests`' "nothing in main needs horizontal scrolling" (around `:455-476`).
- 2.7 (`board-drag.js`, `DragHarness`, `TouchDragTests`) and 2.8 (`board-keys.js`, `BoardTraversal`, `KeyboardHarness`) are the modules this story extends.
- Commit style: one commit per phase, made by the orchestrator. This phase does not commit.

### Project Structure Notes

**New:**

- `Yello.Client/Components/StatusPager.razor` (parameters only).
- Possibly `Yello.Client/Components/PagerTraversal.cs`, if it is not folded into `BoardTraversal`.
- `tests/Yello.Tests.Client/SmallViewportTests.cs`.
- Possibly `tests/Yello.Tests.Architecture/DesignFoundationGateTests.SmallViewport.cs`.

**Modified:**

- `Yello.Client/Components/BoardView.razor`, `BoardView.razor.cs` (`MoveAsync`, `AnnounceMove`, `Purge`/`PurgePanel`), `BoardView.Keys.cs` (`ActiveStop`, `Traverse`, `StepAsync`, the narrow callback), `BoardView.Drag.cs` (`EndHeld` on crossing)
- `BoardColumn.razor` (tabpanel attributes and `hidden`, the selected marker), `BoardKeys.cs` (the JSInvokable), `BoardTraversal.cs`, `BoardKey.cs`
- `ContextBar.razor` (if markup is needed for the visually-hidden label)
- `wwwroot/js/board-keys.js` (`matchMedia`, the pager keys, `narrow` per key), `wwwroot/js/board-drag.js` (hit-test, auto-scroll, header comment)
- `wwwroot/css/components.css` (the pager, the narrow column, ≥80em columns, context bar rules)
- `Localisation/ClientCopy.resx`
- Tests: `TextSpacingTests.cs`, `TouchDragTests.cs`, `BoardTraversalTests.cs`, `DesignFoundationGateTests*.cs`, and possibly `FakeYelloApi.cs` (five Statuses, if `Seed` does not already allow it) and `DragHarness`/`KeyboardHarness` (a `SelectTabAsync` helper)
- `tests/TESTING-CONVENTIONS.md`, `deferred-work.md`, this story file, `sprint-status.yaml`

**Naming:** `ViewToggle` is links, deliberately not tabs (`ViewToggle.razor:5-8`). Do not convert it. The pager is the product's only tablist.

### References

- `_bmad-output/planning-artifacts/epics.md`: `:1386-1428` (story), `:246-247` (UX-DR8, UX-DR9), `:258` (UX-DR20), `:262-266` (UX-DR21 to 25), `:285-286` (UX-DR41, UX-DR42), `:162` (NFR-9), `:364` (coverage), `:437` (epic notes)
- `ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md`: `:41` (assumed breakpoints), `:218` (Status pager), `:286-326` (Move control, accelerated paths, `:320` below-768 keyboard), `:409-421` (Responsive & Platform), `:434-437` (Internationalisation). `DESIGN.md`: `:102-123` (radii, spacing, target-min), `:162-174` (context-bar, role-chip), `:216-222` (button-secondary), `:451-456` (card overflow at 320, context bar). `review-accessibility.md:121-145` (reflow findings). `.memlog.md:21` (tap-to-select fallback). `validation-report.html:610-612`.
- `prds/prd-YelloBMAD-2026-08-15/prd.md:753-757` (NFR-9). `implementation-readiness-report-2026-08-22.md:542`, `:855`, `:910`, `:973`.
- `test-artifacts/test-design-qa.md:369` (X-11), `:355-356` (X-4, X-6), `:311` (X-5); `test-design-progress.md:633`.
- `tests/TESTING-CONVENTIONS.md`: browser `:201-340` (drag `:287-313`, keyboard `:315-340`, RTL `:333-334`).
- `deferred-work.md`: `:20`, `:36`, `:573`, `:574`, `:589`.
- Stories: `2-7-…md` (Q15 `:246`, `:371`; breakpoints `:82`, `:170`), `2-8-…md` (`:13`, RTL `:132`, `:178`, Q14 `:277`), `2-10-…md` (Q10 `:602-605`).
- Planning inputs checked for small-viewport content with no hits: `addendum.md`, `ARCHITECTURE-SPINE.md`, `sprint-change-proposal-2026-09-13.md`, `specs/spec-yello/SPEC.md`.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), bmad-dev-story, unattended phase of the story cycle.

### Debug Log References

- **Red first.** The `BoardTraversalTests` additions (narrow traverse/carry, narrow ↑/↓, pager keys LTR/RTL with `Home`/`End` and clamping) were written before `BoardTraversal.Pager` and the `narrow` overloads existed (red as a compile failure), then turned green. The browser cases were written against the design and run once the client compiled; they went green on the first run, so their teeth were proved by plants instead (below).
- **Analyzer-driven shapes.** S4136 (overloads adjacent), S1541/S3776/S1067/S3358 (`KeyOf` split with `Named`; the narrow `ActiveStop` branch extracted to `StopIn`; `SendInTurnAsync`'s tail extracted to `Answered`), S3872 (`Narrow(bool belowMiddle, …)`), S6802 (a lambda in a loop - each tab is its own `StatusPagerTab` component), S1135 (a Status name read as a to-do marker in a test comment). Gates: `No_user_visible_copy_is_written_as_a_csharp_string_literal` (a doc comment quoting the count copy) and `The_interactive_target_floor_is_declared_and_never_lowered` (the tab needed an inline floor too). S1200 on `BoardView` did not fire: no injection was added, and the new types are a child component and a partial.
- **Plants** (a scratch script: each file saved, planted, built, the named tests run, and restored in a `finally` from the saved text - never git):
  - the narrow media rule removed: gate `Below_768px_an_unselected_column_is_hidden_by_construction` CAUGHT; case 3 (`At_375px_one_column_…`) CAUGHT; case 14 (reflow) not caught - with every column shown but flexing, nothing overflows, so 1.4.10 still holds; case 3 is the one that owns it.
  - a tab sentence on a Board-made follow: case 6 (`A_move_from_the_card_menu_…`) CAUGHT.
  - the pager saying `Tasks.Count` instead of the true total: case 4 CAUGHT.
  - `←`/`→` in the narrow composite switching columns: unit test CAUGHT; browser case 7 not caught, even after strengthening it (each inline arrow is now followed by `↓`/`↑` to a known card). The CSS hides the destination column, so focus cannot land there, and `ActiveStop` keeps the stop in the shown column: the behaviour stays correct by two other layers, and the pure helper is what the unit test holds.
  - the drag hit-testing hidden columns (the `isTarget` filter removed): case 8 not caught - a `display: none` column has an empty rect, which `inside()` can never contain, so the explicit filter is belt and braces behind the CSS. Recorded rather than faked.
  - `display: none` on the Role chip below 30em: gate CAUGHT, case 13 CAUGHT.
  - a px breakpoint (`1279px`): gate CAUGHT.
  - `overflow-x: auto` and a fixed column kept at narrow width: case 14 CAUGHT. (Plain 17rem fits 320px less the Board's padding - 272px into 296px - so the plant used 19rem to make the kept width overflow; with 17rem nothing scrolls and 1.4.10 is met.)
  - a 3px pager tab: gate CAUGHT.
  - the pager selection not purged: not plantable in a browser - `Board.razor` keys `BoardView` by `(SpaceId, ProjectId)`, so every Space switch, lease end or sign-out builds a new Board that starts on its first tab whether or not `PurgePanel` resets it. The reset is kept as defence in depth; case 15 holds the observable half (first tab after a switch, nothing in storage, the address or the title).
- **Task 8 (e).** No existing narrow case needed a correction: `DragTests`' 320/768/1280 case lifts a card in the first column, which is the selected one; 768px is not narrow (`47.9375em` is 767px), so `DragTests`' sideways auto-scroll at 768 is unchanged; `LabelTests`, `ListViewTests` and `MoveTests` at 320 act only on the first column. All stay green in the full run.

### Completion Notes List

- Story context created by bmad-create-story (2026-10-28). The context engine analysis is complete and a developer guide has been written. The open questions above are to be decided in Task 1 as reversible, recorded decisions.
- **Decisions (Task 1).** Each is the story's recommendation, reversible, and a line Lee can cut:
  - Q1 - (a): CSS hides unselected columns by construction under `@media (max-width: 47.9375em)`; `board-keys.js` reports `matchMedia` to .NET; every column stays mounted. Cost: CSS and JS must agree on `47.9375em` (gate on the CSS, comment cross-reference in both scripts). Added on the way: the column that holds focus is spared (`:not(:focus-within)`) until .NET selects it, so crossing 768px never drops focus to the body.
  - Q2 - (a): first tab on load; kept across re-reads and paging; going narrow selects the active stop's (or the carried Task's) column; a move follows the Task; reset by the purge; never stored.
  - Q3 - (a): automatic activation; ends clamp; `Home`/`End` supported. Ledgered to UX.
  - Q4 - (a): tablist "Statuses"; tab = name + count chip, named "{Status} {n} Tasks"; a person's change says "{Status}, {n} Tasks." with the true total (`PagerShown`). Ledgered to UX.
  - Q5 - (a): `←`/`→` consumed and inert below 768px, traversing and carrying; in-column carry kept; a refused carried arrow says nothing. Ledgered to UX.
  - Q6 - (a): only the pager's shown column is a drop target; no sideways auto-scroll while narrow. Closes `deferred-work.md:574`.
  - Q7 - (a): "+" stays on the first column, so it shows on the first tab only. Ledgered to UX.
  - Q8 - (a): Project name dropped below 1280px; "Switch Space" visually hidden below 30em, kept in the button's name; the Role chip never touched. No chevron glyph invented; ledgered to UX.
  - Q9 - (a): at ≥80em columns share the row (`flex: 1 1 0`, `min-inline-size: 14rem`); 17rem with row scroll at 48-80em. Ledgered to UX / 6.1.
  - Q10 - (a): 1.4.10 measured at 320×256 and 375×667 in every tab and with the panel open. Ledgered to UX (the cramped 256px column).
  - Q11 - (a): `overflow-x`/`translate` left alone; added an em-breakpoint gate and a physical-scroll JS gate allowing exactly the drag's `scrollBy({ left })`, proved under RTL.
  - Q12 - (a): tabs reuse `button-secondary`'s tokens at `--radius-sm`; selected = 2px emphasis border + primary text + `aria-selected`; a wrapping row. Ledgered to UX.
- **Follow-the-Task placement.** The story put the follow in `MoveAsync` and `AnnounceMove`'s non-admitted branch. It is in `EditAsync` (for every Status change, before the first await - which covers `MoveAsync`'s every origin and the panel's Status select too) and in the answer's tail for a non-admitted outcome. An admitted answer leaves whatever tab the person chose meanwhile. Closing the panel and creating a Task also follow. None of these says a tab sentence.
- **Pager selection logic** is `BoardTraversal.Pager` (folded into `BoardTraversal`, no `PagerTraversal`). `BoardKey` gained `Home` and `End`; on the composite they end a run as any other key did.
- **RTL drag (Task 5 (d))** needed no code change: the browser case passes at 1024px under `dir="rtl"`.
- **Ledger** (`deferred-work.md`): `:574` closed; `:20` and `:36` updated; `:573` touched (still open, Lee and UX/Lee); a new 2.11 section adds six UX / 6.1 entries and one for story 5.1 (the Offer indicator at every breakpoint).

### File List

New:
- `Yello.Client/Components/BoardView.Pager.cs`
- `Yello.Client/Components/StatusPager.razor`
- `Yello.Client/Components/StatusPagerTab.razor`
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.SmallViewport.cs`
- `tests/Yello.Tests.Client/SmallViewportTests.cs`

Modified:
- `Yello.Client/Components/BoardColumn.razor`
- `Yello.Client/Components/BoardKey.cs`
- `Yello.Client/Components/BoardKeys.cs`
- `Yello.Client/Components/BoardTraversal.cs`
- `Yello.Client/Components/BoardView.Keys.cs`
- `Yello.Client/Components/BoardView.razor`
- `Yello.Client/Components/BoardView.razor.cs`
- `Yello.Client/Components/IBoardKeysHost.cs`
- `Yello.Client/Localisation/ClientCopy.resx`
- `Yello.Client/wwwroot/css/components.css`
- `Yello.Client/wwwroot/js/board-drag.js`
- `Yello.Client/wwwroot/js/board-keys.js`
- `tests/Yello.Tests.Architecture/BoardTraversalTests.cs`
- `tests/Yello.Tests.Client/TextSpacingTests.cs`
- `tests/Yello.Tests.Client/TouchDragTests.cs`
- `tests/TESTING-CONVENTIONS.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/2-11-the-board-on-a-small-viewport.md`

### Change Log

- 2026-10-28: Story created (ready-for-dev).
- 2026-10-28: Implemented (bmad-dev-story). The Status pager below 768px (tablist, tabpanel columns, keys, true-total announcement, follow-the-Task, breakpoint crossing), the narrow keyboard and drag limits, the context bar's drop order, shared columns at ≥1280px; 22 `SmallViewportTests` cases, 4 `TextSpacingTests` and 2 `TouchDragTests` cases, 3 `BoardTraversalTests` and 10 design gates; ledger and conventions updated. Build 0 warnings, 0 errors; full run (`--max-parallel-test-modules 1`) 1,521 total, 1,512 passed, 0 failed, 9 skipped (3 bundle cases plus the Explicit tiers, as at 2.10). Status set to review.
- 2026-10-28: Code review (bmad-code-review): 38 raw findings, 25 after de-duplication; 8 patched (the `hidden` attribute made final below 768px, going narrow selects the column holding focus, a person's tab change ends a carry and closes a menu, a refused move no longer overrides a chosen tab, the empty Board's create selects the first tab, a clicked tab is focused; 5 new browser cases and a stronger AC16 case), 3 deferred, 14 dismissed. Status set to done.
