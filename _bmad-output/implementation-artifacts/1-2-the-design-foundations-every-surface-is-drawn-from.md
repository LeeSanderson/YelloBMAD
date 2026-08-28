---
baseline_commit: 33526767b95af2838eb2bf53c3f5794f2e436b82
---

# Story 1.2: The design foundations every surface is drawn from

Status: done

Epic: 1 — An Account, a Space of your own, and a boundary that holds
Story key: `1-2-the-design-foundations-every-surface-is-drawn-from`
Requirements owned: **UX-DR1 … UX-DR7, UX-DR40, UX-DR42, NFR-9**
Depends on: **story 1.1** (done) — the solution skeleton, `Yello.Client` as Blazor WebAssembly, and the architecture suite this story adds cases to.
Carries no FR. It is one of the eleven foundation/NFR stories declared as such in the Story Coverage Index.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a person using Yello in either light or dark,
I want one token system, type scale and focus treatment behind every surface,
So that the product reads as one thing and its accessibility floor is verified rather than asserted.

## Acceptance Criteria

Reproduced from `epics.md:527-591` (`status: final`). Thirteen groups. One correction to AC5's citation is
recorded below the criterion and is a documentation fix, not a change to what is gated.

**AC1 — the theme boundary resolves once, and components never see it**

**Given** the token layer
**When** a component renders under the dark theme
**Then** every semantic name resolves to its unsuffixed value, and under the light theme the same name resolves to its `-light` sibling, resolved once at the theme boundary
**And** no component references a `-light` token directly, because that would pin the component to one theme

**AC2 — the token count is exact**

**Given** the colour tokens
**When** they are counted against `DESIGN.md`
**Then** there are **30** — 15 semantic names, each carrying an unsuffixed dark value and a `-light` sibling: `surface-page`, `surface-column`, `surface-card`, `border-hairline`, `text-primary`, `text-muted`, `accent`, `accent-on`, `focus-ring`, `presence`, `danger`, `danger-on`, `revoked-edge`, `role-chip`, `role-chip-on`
**And** the count is stated so an incomplete token set is detectable rather than merely wrong

**AC3 — the type scale is relative**

**Given** the type scale
**When** any text renders
**Then** its size is expressed in `rem` against a 16px root with a line-height of at least 1.5
**And** `px` appears only on hairlines, radii and outline offsets — never on type

**AC4 — the contrast harness is a build gate**

**Given** the contrast harness
**When** it runs over both palettes
**Then** all **18 gated pairs** are computed by the WCAG 2.x formula rather than estimated, and each meets its stated threshold — 4.5:1 on the twelve text pairs, 3.0:1 on the six non-text and structural pairs
**And** the build fails if any gated pair drops below its threshold, because NFR-9 makes WCAG 2.1 AA a release gate

**AC5 — the two adjacency ratios are asserted as low, and not gated**

**Given** the two remaining rows in `DESIGN.md`'s contrast table — `surface-card` on `surface-column`, and `surface-column` on `surface-page`
**When** the harness runs
**Then** they are asserted as **deliberately low** surface-adjacency ratios (~1.09 and ~1.10) and are **not** gated against any threshold
**And** the reason is stated: `DESIGN.md` names them explicitly as *"two combinations that are load-bearing and must not be mistaken for contrast pairs"* — they separate grounds by hairline rather than by luminance, so a harness gating all twenty rows would fail permanently on these two and invite an unstated exception

> **The quotation in AC5's second clause is misattributed, and the citation must not be copied into code.**
> `DESIGN.md:347` — *"Two combinations that are load-bearing and must not be mistaken for contrast pairs:"* —
> introduces the bullets at `:349` and `:350`, which are **different combinations and not table rows at all**:
> `accent` against `text-primary` (2.66 dark / 2.55 light) and `accent` against `danger` (1.19 / 1.08).
> The two adjacency **rows** this AC is about are governed by `DESIGN.md:345`: *"The last two rows are stated
> for information, not as targets."* The numbers in the AC are correct and nothing about the gate changes —
> only the citation. Cite `DESIGN.md:345` in the harness, not `:347`. Raised as a proposed `epics.md`
> amendment in *Questions*, following the precedent set when story 1.1 amended AC5 upstream rather than
> re-litigating it per story.

**AC6 — the focus ring**

**Given** the focus ring
**When** any element receives focus
**Then** a 2px ring is drawn at a 2px `outline-offset`, never inset and never at offset 0
**And** it is never replaced by a colour change or a border swap — the offset, not the token separation, is what makes it visible

**AC7 — structural borders and the absence of elevation**

**Given** a structural border
**When** it renders at 1.25×, 1.5× or 1.75× display scale
**Then** it is at least 1.5px wide and snapped to a device pixel where the platform allows
**And** no component carries a shadow, because shadow is not a hierarchy device here

**AC8 — the interactive target floor**

**Given** any interactive component
**When** its box is measured
**Then** it meets a minimum height of 24px
**And** the figure is stated precisely because it is easy to get wrong in both directions: WCAG 2.1 AA has no target-size criterion at all, and WCAG 2.2 AA's 2.5.8 sets 24×24, which is the real current floor this design commits to

**AC9 — the radius scale**

**Given** the radius scale
**When** components are inspected
**Then** 2px is used on the Role chip, Label chips and the Offer indicator; 3px on Tasks, columns, the context bar and buttons; 6px on dialogs, the Task detail panel and the invitation view
**And** the fully-round radius is used for exactly one component, the column count chip, which is the only pill in the product

**AC10 — the text link**

**Given** a text link inside a sentence
**When** it renders
**Then** it is underlined, always
**And** the reason is stated: the accent passes handsomely against the *background* and sits at only 2.66 against the body text beside it, and for a link the text beside it is the pair that matters

**AC11 — externalised copy and i18n-safe layout**

**Given** any user-visible string
**When** components are inspected
**Then** no string literal appears in a component — all copy is externalised into resources
**And** no label is sized to its English string, German and Finnish running 30–40% longer, and metadata is never aligned by character count because the monospace fallback outside Latin is frequently not monospaced

**AC12 — reduced motion**

**Given** `prefers-reduced-motion: reduce`
**When** any transition would run
**Then** it does not
**And** no state anywhere in the product is conveyed by motion alone, so honouring the preference costs no information

**AC13 — text spacing and zoom**

**Given** a text-spacing override of line-height 1.5×, letter-spacing 0.12×, word-spacing 0.16× and paragraph spacing 2×, and separately 200% text-only zoom
**When** either is applied to any implemented surface
**Then** no text is clipped or overlapped, because chips and cards size to content with no fixed heights

> **AC13 has no implemented surface to run against in this story.** Its verification is test-design scenario
> **X-11**, which is **E2E and blocked on blocker B5** (the browser-test binding, undecided). This story
> discharges AC13's *constructive* half — content-sized boxes, no fixed heights, `rem` internal padding — and
> gates what is statically detectable. The measurement half defers. See *Deferred, with owners*.

## Tasks / Subtasks

- [x] **Task 1 — The token layer** (AC: 1, 2, 3, 7, 9)
  - [x] Create `Yello.Client/wwwroot/css/tokens.css`. Hand-written CSS custom properties. **Do not introduce npm, a bundler, a preprocessor or a token-build step** — story 1.1's environment preflight records Node v22.20.0 as present and explicitly **not needed**: *"No npm, bundler, preprocessor or token-build step appears anywhere in the corpus. Do not introduce one."*
  - [x] Declare the **15 semantic names** on `:root` bound to their **unsuffixed (dark) values**, and the 15 `-light` values as separate custom properties. Values verbatim from `DESIGN.md:13-42`. Dark is canonical (UX-DR1): the unsuffixed token is the dark value and `-light` is the derived adaptation.
  - [x] Implement the **theme boundary**: exactly one place where the 15 semantic names are rebound to their `-light` siblings. Every component thereafter consumes `var(--surface-card)` and never `var(--surface-card-light)`. See Dev Notes → *The theme boundary, and the one decision this story cannot inherit*.
  - [x] Add the type scale as 8 named roles — `task-title`, `column-head`, `space-name`, `body`, `dialog-title`, `meta`, `role-label`, `presence-count` — with sizes in `rem` and the exact values at `DESIGN.md:47-91`. Set the root to 16px by **not** overriding it (`html { font-size: 100% }` or nothing at all; never a px value, which is the WCAG 1.4.4 failure the AC exists to prevent).
  - [x] Add the two system stacks `system-sans` and `system-mono` verbatim from `DESIGN.md:372-373`. **No webfont**, no `@font-face`, no external font request.
  - [x] Add the spacing scale (3/6/9/12/18/24/36), the four `rem` internal-padding values, `target-min: 24px`, the radius scale, `hairline-width: 1.5px`, `emphasis-width: 2px`, and the motion tokens — all verbatim from `DESIGN.md:92-125`.
  - [x] Link the stylesheet from `Yello.Client/wwwroot/index.html`. Use the fingerprint form consistent with `index.html:48` (`OverrideHtmlAssetPlaceholders` is `true` — `Yello.Client.csproj:4`).
  - [x] Update the hand-off comment at `index.html:15-23`, which currently states this story ships no stylesheet. Leaving it would make it false the moment Task 1 lands — the exact record-drift class that produced six of story 1.1's review findings.

- [x] **Task 2 — The base layer the tokens are drawn on** (AC: 3, 6, 7, 9, 10, 12)
  - [x] Focus ring: one rule, `outline: 2px solid var(--focus-ring); outline-offset: 2px`. Never inset, never `outline: none` with a substitute, never offset 0. Apply on `:focus-visible`.
  - [x] Text link: `var(--accent)` **with `text-decoration: underline`, always**.
  - [x] `prefers-reduced-motion: reduce` block removing every transition and animation.
  - [x] Interactive base: `min-height: var(--target-min)` on the interactive element base rule, so a component acquires the floor by default rather than by each component remembering.
  - [x] No `box-shadow` anywhere. `DESIGN.md:421` sets `shadow: none` explicitly on the task card "so nobody adds one back"; the single sanctioned exception is `task-card-lifted`, which **is not built in this story**.
  - [x] **Logical properties only** — `inline-start`/`inline-end`, `padding-inline`, `margin-block`. Never `left`/`right`. UX-DR42 makes RTL tolerance structural, and this is the layer that decides it for every later story.
  - [x] **Uppercase is presentational, and locale-aware.** `column-head` and `role-label` render uppercase via `text-transform` — never from the copy resource. Scope the rule under a locale-aware `lang` attribute and **exclude Turkish, Azeri and Greek**, where `text-transform: uppercase` is lossy (Turkish dotless ı → I changes the word; Greek strips accents and alters final sigma). For case-less scripts — Arabic, Hebrew, CJK, Thai, Indic — fall back to `task-title` weight at `column-head` size with **zero** letter-spacing, because letter-spacing severs the joins of connected scripts. This is UX-DR42 and it belongs in this layer: baking uppercase into a resource makes the Role's accessible name get spelled out letter by letter by JAWS and VoiceOver, degrading the single most important accessibility affordance in the product.
  - [x] Style `#blazor-error-ui` from the tokens. Story 1.1 restored the element unstyled and recorded that "Story 1.2 owns making it look like anything, from the tokens it defines" (`index.html:26-41`). Its `#blazor-error-ui` id and `reload`/`dismiss` classes are **contractual** — the framework looks for exactly these. Keep the inline `display:none`; it is the framework's precondition, not a style choice.

- [x] **Task 3 — The contrast harness** (AC: 4, 5)
  - [x] Add a new test class to **`tests/Yello.Tests.Architecture`**. **Do not create a test project.** See Dev Notes → *Where the harness lives, and why nothing else is a candidate*.
  - [x] Implement WCAG 2.x relative luminance and contrast ratio in-repo: sRGB linearisation at the 0.03928 threshold, `(L₁+0.05)/(L₂+0.05)`. ~20 lines of `double` arithmetic. **Add no NuGet package** — see Dev Notes → *Why no package*.
  - [x] **Parse the token values out of `tokens.css`**, never from a table restated in C#. The harness must fail when the CSS changes, which a hardcoded copy cannot do. Read the file through `RepositoryLayout.Root` (`RepositoryLayout.cs:51`), the established pattern for reading repository files from this suite.
  - [x] Compute **36 ratios** — 18 gated pairs × 2 palettes — and assert each against its threshold: 4.5 on the twelve text pairs, 3.0 on the six non-text pairs. The authoritative expected values are in Dev Notes → *The contrast table, computed and verified*.
  - [x] Assert the two adjacency ratios are **low** (`surface-card` on `surface-column` ≈ 1.09/1.16; `surface-column` on `surface-page` ≈ 1.10/1.07) and are **not** gated against 3.0 or 4.5. Cite `DESIGN.md:345`, **not** `:347` — see the note under AC5.
  - [x] Assert the token **count is exactly 30 declared custom-property names**, over names and never over resolved values. See Dev Notes → *The 30-names / 26-values trap*.
  - [x] Assert the 15 semantic names are present by name, so a renamed or missing token fails rather than silently reducing the count.
  - [x] Traits: `[Trait("Suite","Architecture")]`, `[Trait("Priority","P0")]`, `[Trait("Requirement","UX-DR7")]` and `[Trait("Requirement","NFR-9")]`. The `Assumption` trait does not apply — this story hardens no PRD §12 assumption.

- [x] **Task 4 — The gates that stay true as components arrive** (AC: 1, 3, 6, 7, 9, 10, 11, 12)
  - [x] **This is the task that decides whether story 1.2 is real.** Most ACs are conditioned on components that do not exist yet, so a gate written against today's tree passes vacuously and keeps passing while a later story breaks it. Every gate below must therefore **scan the repository** — all `**/*.css` and all `**/*.razor` — rather than assert a property of a specific component. See Dev Notes → *The vacuous-gate problem, and what story 1.1 learned about it*.
  - [x] AC1 gate: no `-light` custom property is referenced outside the single theme-boundary block in `tokens.css`.
  - [x] AC3 gate: no `font-size` in `px` anywhere; `px` permitted only on border widths, radii and `outline-offset`. Assert the root font-size is not overridden in `px`. **Split into two gates and one clause narrowed — see Completion Note 6.**
  - [x] AC6 gate: no `outline: none` / `outline: 0` without an accompanying `:focus-visible` treatment; no `outline-offset` of `0`; no negative `outline-offset` (the inset case).
  - [x] AC7 gate: no `box-shadow` declaration outside the sanctioned lifted-card rule (which does not exist yet — so today the gate asserts none at all); no border width below 1.5px on a structural border.
  - [x] AC9 gate: the four radius values are the only ones used; `border-radius: 9999px` appears exactly once. **The reference count is gated at *at most* one — see Completion Note 7.**
  - [x] AC10 gate: the text-link rule carries `text-decoration: underline` and no rule removes it.
  - [x] AC11 gate: no user-visible string literal in a `.razor` file. Today there are no components, so plant one to prove the gate. Note the known variance: `index.html` carries the framework's English error strings, which sit outside Blazor localisation — record it rather than exempting it silently. **Recorded, plus one further variance — see Completion Note 8.**
  - [x] AC11, the other two clauses — *"no label sized to its English string"* and *"metadata never aligned by character count"* — are **not statically gateable** and have no component to measure. Discharge them constructively in Task 2 (content-sized boxes, no fixed widths, no character-cell alignment) and say plainly in the Dev Agent Record that they are asserted by construction rather than by a gate. Do **not** write a gate that appears to cover them and does not.
  - [x] UX-DR42 gate: no `text-transform: uppercase` outside a locale-scoped rule, so the exclusion of Turkish, Azeri and Greek cannot be lost by a later component adding its own uppercase.
  - [x] AC12 gate: a `prefers-reduced-motion: reduce` block exists and no `transition`/`animation` declaration escapes it.
  - [x] UX-DR42 gate: no physical `left`/`right` in any CSS property where a logical equivalent exists.

- [x] **Task 5 — Prove every gate against a planted violation** (AC: all)
  - [x] For each gate in Tasks 3 and 4: introduce a real violation, confirm the build fails and that the message names the offence, then revert. Record every result in the Dev Agent Record.
  - [x] This is not optional. `tests/TESTING-CONVENTIONS.md:93-96`: *"An absence assertion must be validated against a planted signal, or it is not a test."* Every gate this story ships is an absence assertion against a tree with no components, which is exactly the condition under which a vacuous gate is indistinguishable from a working one.
  - [x] Plant at least one violation that a **later** story would plausibly write — a `.razor` file using `var(--surface-card-light)` directly, and a component with a hardcoded English string — because those are the regressions these gates exist to catch and today's empty tree cannot exercise them.
  - [x] Confirm `dotnet test` over the solution returns success afterwards, with `Yello.Tests.Architecture` green and the other four suites still reporting zero tests and exiting 0.

- [x] **Task 6 — Record what this story does not discharge** (AC: 13)
  - [x] Append AC13's E2E half to `_bmad-output/implementation-artifacts/deferred-work.md` with its owner, following the format of the two existing entries.
  - [x] Record the two ungated-but-used structural pairs (Dev Notes → *Two pairs the gate does not cover*) in the same place, so a later story can close them with the reasoning intact.

### Review Findings

Code review of `3352676..HEAD` (2 commits, 11 files, +4,375/−15), run 2026-08-27 with three
parallel adversarial layers — Blind Hunter, Edge Case Hunter, Acceptance Auditor. Baseline
re-verified at HEAD before triage: `dotnet build` clean, `dotnet test` 77 passed (70 in
Architecture), exit 0 — the Debug Log's figures reproduce exactly. Every finding below was
confirmed by reading the source during triage; severities are the reviewer's, not the layers'.

**What is met, and verified independently:** the 30 token names and all 30 hex values match
`DESIGN.md:13-42` exactly; the 8 type roles, spacing scale, radii, border widths and motion
tokens match `DESIGN.md:47-125`; both font stacks are verbatim; `GatedPairs` is exactly 18
(12 Text + 6 NonText) and matches `DESIGN.md:322-341` row for row; the two adjacency rows are
excluded from the gated set; the AC5 citation is corrected to `DESIGN.md:345` in the harness;
the light palette is genuinely resolved *through* the theme boundary rather than by appending a
suffix; every `line-height` in the scale really is ≥ 1.5; both boundary rules really do set
`color-scheme: light`; no new project, package or npm.

**The dominant theme is the one story 1.1 handed over, and it recurred:** *gates that assert
something materially weaker than their names, comments and the ACs claim*. The Acceptance
Auditor did not argue this — it planted violations and ran the suite. **A single
`style="box-shadow:…;height:32px;border-radius:50%;margin-left:9px;text-transform:uppercase;
font-size:13px;outline:none"` attribute on one `.razor` element breaks AC3, AC6, AC7, AC9, AC13
and UX-DR42 simultaneously and the suite reports 70/70 green.** Seven further planted violations
also passed green. The token layer itself is sound; the harness that is supposed to keep it sound
has holes in the shapes epic 2 is most likely to write.

#### Decisions needed

- [x] [Review][Decision] **The transcription check that caught a real defect was never committed, and there is no gate over token *values*** — **Resolved 2026-08-27 (Lee): leave token values ungated; commit only the name and axis assertions.** No value-comparison gate is added, and neither mechanism is adopted. The name/axis patch below still lands, so a *deleted* or *renamed* token is detectable; a *mistyped* one is not. The accepted exposure, stated plainly so it is not rediscovered: a hex that still parses as a colour is invisible to every gate unless the change happens to cross a contrast threshold, and `revoked-edge` sits in none of the 18 gated pairs — which is precisely why the original `#FFFFFF` error survived the harness and needed a hand-written script to find. Anyone editing `tokens.css` should diff the 30 values against `DESIGN.md:13-42` by eye, because nothing will do it for them. Original finding: The Debug Log records that a scripted check *"caught a real transcription error I had introduced — `--revoked-edge-light` written as `#FFFFFF` instead of `#BE123C`"* and states plainly that *"the contrast harness would **not** have caught it"*. That script is nowhere in the repository. Nothing compares any token value to `DESIGN.md`; the 30-token gate counts **names** only. So the exact error the story documents as having occurred can be reintroduced tomorrow with all 23 assertions green. The fix needs a decision because both available mechanisms contradict a stated principle: **(a)** have a test parse `DESIGN.md`'s frontmatter and compare — makes a release gate depend on a planning artifact under `_bmad-output/`, which no gate currently reads; or **(b)** restate the 30 values in C# — which is exactly the "hardcoded copy cannot fail when the CSS changes" pattern `ColorTokenContrastTests`' own remarks reject. (a) is the only one that actually detects transcription drift, since (b) just moves the transcription.
- [x] [Review][Decision] **AC3's px clause contradicts the shipped token layer, and the narrowing was resolved unilaterally** — **Resolved 2026-08-27 (Lee): amend both `epics.md` and `DESIGN.md`.** Became a patch below. Four sentences change, all of them the same absolute claim, to confine the restriction to type and name the token layer as the place structural px lives: `epics.md:219` (UX-DR2), `epics.md:542` (AC3's second clause), `DESIGN.md:46` (frontmatter comment) and `DESIGN.md:377` (Typography). Both documents carry `status: final`, so this is a deliberate amendment to finalised artifacts rather than a drafting fix — the Change Log should say so, following the precedent story 1.1 set when it amended AC5. Original finding: AC3 (in `epics.md`, `status: final`) says *"`px` appears only on hairlines, radii and outline offsets"*. `tokens.css` declares `--space-unit`/`--space-1..7` in px, `--target-min: 24px` and `--motion-long-press-slop: 10px`. Completion Note 6 exempts the token layer and cites `DESIGN.md:99-113` as authority — but `DESIGN.md:377` restates the absolute claim, so the cited source contradicts itself in the same document. The developer's reading is defensible and arguably the only workable one, but it is absent from the story's *Questions* and from `deferred-work.md`, so `epics.md` still says one thing while the gate does another. This is the same shape as AC5's citation, which *was* raised. Amend `epics.md`/`DESIGN.md`, or record it as an accepted variance?
- [x] [Review][Decision] **AC5 says the adjacency rows are gated against nothing; the harness gates them against two invented thresholds** — **Resolved 2026-08-27 (Lee): keep the ceiling, and amend UX-DR7 to sanction it.** Became a patch below. `AdjacencyCeiling = 1.5` and the `<= 1.0` floor stay as they are; UX-DR7 (`epics.md:224`) gains the sanction so the assertion has an upstream source and the test name stops contradicting the requirement. The gate's name and failure message should also stop saying the rows are "not gated" when they are bounded — bounded-as-low is the accurate description. Original finding: `The_two_surface_adjacency_ratios_are_deliberately_low_and_are_not_gated` fails at `>= 1.5` or `<= 1.0`. `AdjacencyCeiling = 1.5` appears in no upstream document and sits 37% above the stated ~1.09/~1.10, so a palette that had visibly redrawn the tonal ladder to 1.45 passes while the test name and message insist the rows are ungated. Completion Note 11 argues the direction is right (asserting *low* is stronger than asserting nothing), and I agree — but it is a deviation from a `status: final` AC that was never raised, unlike AC5's citation. Keep the ceiling and amend UX-DR7, or drop it to match the AC's letter?
- [x] [Review][Decision] **AC7's "snapped to a device pixel where the platform allows" is neither implemented, gated, nor deferred** — **Resolved 2026-08-27 (Lee): implement snapping now and gate it.** Became a patch below. AC9's radius *mapping* clause is a separate matter and still has no home — it stays unverifiable until components exist, so it goes to the ledger. **Flagged, because this is the one patch that changes rendered output rather than gate coverage:** snapping is expressed as `resolution` media queries in `tokens.css` rebinding `--border-hairline-width` to `ceil(1.5 × dppx) ÷ dppx` so the rendered edge always lands on an integer number of device pixels — 2px at 1dppx, 1.6px at 1.25, 2px at 1.5, ~1.714px at 1.75, 1.5px at 2, ~1.667px at 3. Every value stays at or above the 1.5px floor, so the hairline gate still holds. But the per-resolution table is *derived here*, not specified upstream, and it makes borders visibly heavier at four of the six common display scales. UX should see it before it is treated as settled. Original finding: `DESIGN.md:366` states it in the same sentence as the 1.5px floor. Nothing in `tokens.css` or `base.css` addresses snapping, no gate mentions it, and `deferred-work.md` records the other three deferrals but not this. AC9's mapping clause (2px on the Role chip / Label chips / Offer indicator, 3px on Tasks / columns / context bar / buttons, 6px on dialogs / Task detail / invitation view) is in the same position: only the value *set* is gated and the mapping is unverifiable with no components. Implement snapping now, or record both in the ledger with an owner?
- [x] [Review][Decision] **`--danger` is used as a border, which `DESIGN.md`'s Don't column names directly** — **Resolved 2026-08-27 (Lee): switch the border to `--border-hairline`.** Became a patch below. The long comment at `base.css:256-260` arguing the danger reading goes with it — it currently justifies a choice that is being reversed, and stale prose in exactly these files produced six of story 1.1's review findings. Consequence worth noting: `--danger` and `--danger-on` then have no consumer anywhere in the product, which is correct for a token layer that declares the design's whole vocabulary ahead of the components that spend it, but it does mean the first genuinely irreversible confirm dialog is where those two tokens get exercised for the first time. Original finding: `base.css:272` sets `border-block-start: var(--border-hairline-width) solid var(--danger)` on `#blazor-error-ui`. `DESIGN.md:474-492`: *"Let the **copy** carry destructiveness | Don't rely on the danger border"* and *"Reserve danger for the genuinely irreversible | Don't use danger for validation errors or ordinary refusals"*. The file argues the case in a comment — an unhandled client exception has already cost unsaved work — and `revoked-edge` exists precisely because that boundary was judged too easy to blur. Compounding it slightly: the accent-coloured `.reload` link sits immediately beside it at 1.19 against danger, the deuteranopia convergence `DESIGN.md:350` names. Accept the reading (and record it as a variance), or switch the border to `--border-hairline`?

#### Patches

*From the resolved decisions above:*

- [x] [Review][Patch] DECISION 2 — **Amend AC3's px clause in both finalised documents** [_bmad-output/planning-artifacts/epics.md:219,542; _bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md:46,377]. Confine the restriction to type and name the token layer as the place structural px legitimately lives, so the spacing scale, `--target-min` and `--motion-long-press-slop` stop contradicting a `status: final` requirement. Both documents are final — record the amendment in the Change Log as deliberate.
- [x] [Review][Patch] DECISION 3 — **Amend UX-DR7 to sanction the adjacency ceiling, and stop the gate claiming the rows are ungated** [_bmad-output/planning-artifacts/epics.md:224; tests/Yello.Tests.Architecture/ColorTokenContrastTests.cs:303-372]. UX-DR7 gains the `1.0 < ratio < 1.5` bound so `AdjacencyCeiling` has an upstream source. Rename `The_two_surface_adjacency_ratios_are_deliberately_low_and_are_not_gated` and reword its message: the rows are bounded-as-low, not ungated, and the current wording asserts the opposite of what the code does.
- [x] [Review][Patch] DECISION 4 — **Implement device-pixel snapping for the hairline, and gate it** [Yello.Client/wwwroot/css/tokens.css:294]. `resolution` media queries rebinding `--border-hairline-width` to `ceil(1.5 × dppx) ÷ dppx` — 2px at 1dppx, 1.6px at 1.25, 2px at 1.5, ~1.714px at 1.75, 1.5px at 2, ~1.667px at 3 — so a structural edge always lands on an integer number of device pixels. Gate that the snapping block exists and that every rebinding stays ≥ `HairlineFloorPx`. **This is the only patch that changes rendered output**; borders get visibly heavier at four of six common scales and the per-resolution table is derived here rather than specified upstream, so UX should see it. Note for the implementer: `CssCorpus.ReadTokenValues` is a flat last-wins map, so `TokenValues["border-hairline-width"]` will become whichever branch is written last — every branch must clear the floor, because the gate will only measure one of them.
- [x] [Review][Patch] DECISION 5 — **Switch the error banner's border to `--border-hairline` and delete the argument for `--danger`** [Yello.Client/wwwroot/css/base.css:256-260,272]. The comment currently justifies the reading being reversed; stale hand-off prose in these files produced six of story 1.1's findings, so it goes with the change rather than after it.

*From the review layers:*

- [x] [Review][Patch] HIGH — **Inline `style` attributes and `<style>` bodies bypass every CSS gate** [tests/Yello.Tests.Architecture/CssCorpus.cs:63-84]. `StyleSheets` is built from `*.css` only, and `MarkupFiles` is read by exactly one gate, only for the two `-light` regexes. Every other gate iterates `AllDeclarations()`, which never sees markup. Proven: one planted `style="…"` attribute → 70/70 green, defeating the shadow, fixed-height, radius (`border-radius: 50%` — the exact circular-avatar case Completion Note 5 boasts about catching), physical-property, px and outline gates at once. The class docstring at `DesignFoundationGateTests.cs:26-29` claims *"All `*.css`, all `*.razor`, all `*.html`"*. Blazor components routinely carry `style`, so this is epic 2's most probable regression path. Parse `style=` values and `<style>` element bodies into `Rule`s and include them in `AllDeclarations()`.
- [x] [Review][Patch] HIGH — **AC12 is defeated from inside the reduced-motion block itself** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:1001-1007]. The second half skips any `!important` motion declaration whose at-rule path is a reduced-motion context — which exempts exactly the case that beats the universal reset. Proven: `@media (prefers-reduced-motion: reduce) { .planted-motion { transition: transform var(--motion-quick) !important } }` → green. A class selector with `!important` wins on specificity and source order, so the transition genuinely runs for a reduced-motion user while the gate written to prevent that reports green. Exempt only the universal-selector reset rule, not every rule in the block. Related, same fix: `IsReducedMotionContext` substring-matches, so a block additionally qualified by width or print counts as the reset *and* earns the exemption.
- [x] [Review][Patch] HIGH — **AC6's literal named case, `outline-offset: 0`, passes every gate** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:490-501, 1177-1184]. `PixelLengths` requires a literal `px` suffix, so unitless `0`, `0em` and `-0.25rem` yield no length and are never compared; `RemovesOutline` early-returns for `outline-offset`; and the positive gate only checks that an `outline-offset` declaration *exists*. Proven green. AC6 and `DESIGN.md:358` both say "never at offset 0", and the test is *named* `…_or_drawn_at_offset_zero`. Task 5 planted `-2px`, which is caught, so the planting never reached the case the AC names. Three more in the same cluster: `:focus { outline: 1px … }` is ungated because the positive gate filters `:focus-visible` only; `outline-color: transparent` passes the removal ban; and `DescribeFocusTreatment` measures the *first* ring declaration, so a later override in the same rule wins unseen.
- [x] [Review][Patch] HIGH — **AC3's "line-height ≥ 1.5" and "size expressed in `rem`" are asserted by nothing, and the failure message claims both** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:398-401]. `line-height` appears in the suite only as a member of `TypographicProperties` (the px ban). No gate reads a line-height value, and nothing checks `rem` in either direction. Proven: `.x { font-size: 1.5em; line-height: 1.1 }` → green; `font-size: 13pt`, `1.2em` and `larger` all pass. Yet the remedy string reads *"Sizes are in `rem` against a 16px root and line-heights are >= 1.5"* — the story's declared dominant defect class, reproduced verbatim in a message. Add the ≥ 1.5 check and ban the absolute non-px units (`pt`, `pc`, `mm`, `cm`, `in`, `Q`) and the font-relative ones on type. Also correct `deferred-work.md:14`, which claims *"every line-height ≥ 1.5"* and `rem` padding are held by `No_box_is_given_a_fixed_height` and `Type_is_never_sized_in_absolute_pixels`; neither holds either.
- [x] [Review][Patch] HIGH — **The AC11 gate is blind where copy actually lives, and fails correct code where it doesn't** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:1303-1313, 1414-1436, 1496-1511]. Two halves, opposite directions. *False negatives:* `@code`/`@functions` are blanked wholesale and no `.cs` file is scanned — proven, `@code { private const string Label = "Loading your Spaces, please wait"; }` → green — which is the most idiomatic place a Blazor developer writes user-visible copy; `AttributePattern` matches double-quoted values only, so `title='Delete'` is invisible; and `LocalisableAttributes` omits `value`, which is user-visible on `input[type=submit|button]`. *False positives, which are worse because they block correct code:* `RazorExpressionPattern` handles `@(…)` and `@Member.Chain(args)` but not indexers, so `@Localizer["Delete"]` strips to `["Delete"]` and reports as literal copy — that is the standard `IStringLocalizer` idiom AC11 requires; `@if (ShowRevoked)` leaves ` (ShowRevoked)` behind because the optional paren group needs `(` immediately after the identifier; and a `<script>`, `<style>` or `<pre>` in a `.razor` has its identifiers reported as copy. The first correctly-written localised component in epic 2 fails this gate.
- [x] [Review][Patch] HIGH — **Nothing asserts the stylesheets are linked, or that `base.css` exists at all** [Yello.Client/wwwroot/index.html:22-23]. No test reads `index.html`'s `<link>` elements or the `#[.{fingerprint}]` placeholders, and no test mentions `base.css` by name. Delete both `<link>` lines and all 23 design assertions stay green while no token reaches a browser — focus ring, target floor, reduced-motion reset, locale-aware casing and the error banner all go inert. Story 1.1's history is precisely that of a deliberately removed stylesheet link, which is why `index.html:18-20` warns about restoring one. The link resolution was verified once by hand in the Debug Log, in a story whose whole thesis is that hand verification does not survive later stories. Assert the links, and that `OverrideHtmlAssetPlaceholders` is on.
- [x] [Review][Patch] HIGH — **Only the colours are gated; five of the six token families are unprotected** [tests/Yello.Tests.Architecture/ColorTokenContrastTests.cs:191-231]. Nothing asserts the 8 type roles exist, that each binds all five axes, that the spacing scale is 3/6/9/12/18/24/36, that the radius names exist, or that any motion token exists. Deleting `--type-meta-letter-spacing`, changing `--space-4` to `40px`, or dropping `--motion-easing-exit` leaves every gate green — and `base.css` binds all five axes on all 8 roles, so a deleted token silently becomes an inherited value. AC2's exact-count discipline was applied to one family out of six. (The *value* half of this is the decision item above; asserting the names and axes exist is unambiguous and can land now.)
- [x] [Review][Patch] MEDIUM — **The physical-property gate misses the shorthands that actually break RTL** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:127-166]. `PhysicalProperties` enumerates longhands only; the 4-value `margin`/`padding`/`border-width`/`inset` shorthands are physical (top-right-bottom-left) and do not flip under RTL. `PhysicallyValuedProperties` covers only `float`, `clear`, `text-align`. Proven green: `margin: 0 0 0 var(--space-3)`, `padding: 0 var(--space-3) 0 0`, `background-position: left top`, `transform-origin: right`, `border-top: …`. Add the multi-value shorthands and `background-position(-x)`, `object-position`, `transform-origin`, `text-align-last`, `caption-side`, `resize`, `overflow-x`, `direction`.
- [x] [Review][Patch] MEDIUM — **The target floor is gated on one axis, in one unit, for six elements** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:655-704, Yello.Client/wwwroot/css/base.css:216-223]. `min-height: 1rem` (= 16px) is green because only px lengths are measured; `IsMinimumHeightProperty` covers `min-height`/`min-block-size` but not `min-inline-size`/`min-width`, so the inline axis is unpoliced while `tokens.css`, the gate remark and Completion Note 9 all argue from *"WCAG 2.2 AA's 2.5.8 sets 24x24"*. The base rule covers `button, [role="button"], input, select, textarea, summary` — absent are `[role="menuitem"|"checkbox"|"switch"|"tab"|"option"]`, `[tabindex]` and `[contenteditable]`, several of which the Picker, Role chip and context bar will use. And `usesToken` is satisfied by *any* single declaration referencing `var(--target-min)` — today `#blazor-error-ui .dismiss` alone would satisfy it — while the message insists the floor must be *"acquired by default from a base rule"*.
- [x] [Review][Patch] MEDIUM — **The hairline floor is defeated by a rem-expressed 1px border, and `outline` is an unpoliced 1px escape hatch** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:619-641, 1194-1196]. `border-block-start: 0.0625rem` (= 1px) is green — proven — as are `border: thin` and `border-width: 0.1em`, because only px lengths are measured. Separately, `IsBorderWidthProperty` requires the property to start with `border` and `IsPixelPermitted` exempts everything containing `outline`, so `.card.selected { outline: 1px solid var(--border-hairline) }` is a 1px structural edge no gate measures while the identical `border-inline-start: 1px …` fails. AC7 calls the 1.5px floor an accessibility requirement, not a style rule.
- [x] [Review][Patch] MEDIUM — **Named CSS colours are not literals as far as the colour gate is concerned** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:1448-1452]. `ColourLiteralPattern` matches `#hex` and the colour functions, so `background-color: red`, `color: white`, `currentColor` and `transparent` pass a gate whose message reads *"Every colour comes from one of the 30 tokens"* and whose stated reason is that a literal bypasses the theme boundary, the 30-token count and the contrast harness — all three of which named colours bypass identically. `WcagContrast.Channels:121-125` explicitly refuses *"a named colour"* as unverifiable, so the two files disagree about whether named colours are a problem.
- [x] [Review][Patch] MEDIUM — **The uppercase gate is a substring test, so an explicitly-Turkish uppercase passes** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:940-943]. Any rule whose selector merely *contains* `":lang("` is skipped. Proven green: `.planted-f:lang(tr), .planted-g { text-transform: uppercase }` — one half uppercases Turkish, the lossy case `DESIGN.md:396` and the gate's own remarks name, and the other half is entirely unscoped. The gate never inspects *which* locales are scoped in or out, so the exclusion it claims to make *"survive contact with epic 2"* is not what it verifies. It also fires on `capitalize` and `lowercase` despite its name and message being about uppercase.
- [x] [Review][Patch] MEDIUM — **`Resolve` gives up silently rather than failing, and `TokenValues` sees only `tokens.css`** [tests/Yello.Tests.Architecture/CssCorpus.cs:170-190, 298-312]. After 8 passes it returns partially-substituted text; a cycle, a typo'd name, or a custom property declared in `base.css` or inside the theme boundary leaves `var(…)` as text, and every length gate downstream then measures nothing and reports green. The comment weighs "a gate that never returns" against "a gate reporting a wrong answer" and does not consider the third option: assert that resolution completed. This is the silent-vacuity amplifier sitting under the hairline, target-floor, radius and fixed-height gates.
- [x] [Review][Patch] MEDIUM — **Parser cases that silently drop input the gates then never see** [tests/Yello.Tests.Architecture/CssCorpus.cs:387-430, 509-529, 599-625]. An unterminated quote means every rule after it is never parsed and all gates pass vacuously. A `;` inside an unquoted `url(data:image/svg+xml;base64,…)` splits the declaration and drops the remainder. A `/*` or `*/` inside a quoted string blanks real declarations. `px` is matched case-sensitively, so `13PX` is invisible to every length gate. A leading-dot fraction (`.5px`, `-.75px`) is not captured, so a sub-hairline border escapes. And a brace inside a C# string or char literal in `@code` sends `MatchingBrace` to EOF, blanking the rest of the file. Individually improbable; collectively they are the "silently mis-parses a construct a later story introduces" class the parser's own remarks say it guards against.
- [x] [Review][Patch] MEDIUM — **Several gates' positive halves are satisfied by one rule anywhere in the corpus** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:817-853, 1275-1277]. `The_text_link_is_underlined_and_no_rule_removes_it` passes if *any* rule whose selector merely *contains* `"text-link"` declares an underline, so a `.text-link-quiet` keeping one while `.text-link` loses it passes. `SelectorTargetsLink`'s `Contains("link")` also matches `.blinking`, and misses `text-decoration: none` on `*`, `:is(a, …)`, `:where(a)` or any ancestor — so every link underline in the product can be removed without the AC10 ban firing.
- [x] [Review][Patch] MEDIUM — **The only control this story styles as a target cannot be reached by keyboard, and the comment claims the opposite** [Yello.Client/wwwroot/css/base.css:280-294]. `#blazor-error-ui .dismiss` is a bare `<span class="dismiss">&times;</span>` with no `role` and no `tabindex`, and it is given `cursor: pointer`, `min-height` *and* `min-inline-size` — an affordance that cannot be operated by keyboard at all, and on which `:focus-visible` can never render. Meanwhile `#blazor-error-ui .reload`, the real `<a href=".">`, gets no floor, while `base.css:213-214` states that *"standalone link-shaped controls carry `role="button"` or a class of their own and are covered by the rule above them"* — `.reload` has a class of its own and is covered by nothing. Add `role="button" tabindex="0"` to the span, and correct the comment.
- [x] [Review][Patch] LOW — **`Every_theme_boundary_rule_rebinds_all_fifteen_semantic_names` never asserts fifteen** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:281-319, 1135-1146]. It derives its expectation from `SemanticNamesFromTokenLayer()`, which reads the same file it is checking, so the number in the name is not an assertion — if the semantic declarations were emptied or moved, the loop would assert nothing while the name still claimed fifteen. The 15/30 is pinned only in `ColorTokenContrastTests`. Assert the derived count. Same gate: both boundary rules do set `color-scheme: light`, but nothing requires it, so a third trigger could omit it and leave UA scrollbars and form controls dark on a light ground.
- [x] [Review][Patch] LOW — **`CssCorpus.Tokens`' crafted duplicate-file message is unreachable** [tests/Yello.Tests.Architecture/CssCorpus.cs:121-127]. `StyleSheets.SingleOrDefault(IsTokensFile)` throws `InvalidOperationException("Sequence contains more than one matching element")` before the `?? throw` can run, so the carefully argued *"two of them would mean two competing token sets"* message is reachable only for zero matches and the `Count(IsTokensFile)` interpolation is dead code for the duplicate branch. The duplicate case surfaces as an unnamed framework exception — the outcome `RepositoryLayout.LoadXml`/`LoadJson` were written to avoid elsewhere in this suite.
- [x] [Review][Patch] LOW — **`GlossaryProperNouns`' stated basis is wrong in both directions** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:182-210]. The comment says the permitted set is *"exactly the PRD section 2 Glossary proper nouns"*. `Yello` is not a PRD §2 Glossary entry; the Glossary fixes Account, User, Space, Membership, Role, Owner, Admin, Member, Viewer, Invitation, Project, Task, Status, Board and others. If the stated rule were the real rule, `<h2>Board</h2>` and `<span>Viewer</span>` would be permitted literals and the gate would be gutted. The implemented rule — the product name, because a brand is not translated — is defensible; the comment describes a far weaker rule and presents it as the contract.
- [x] [Review][Patch] LOW — **`--border-emphasis-width` is declared and referenced nowhere** [Yello.Client/wwwroot/css/base.css:188]. `DESIGN.md:366` makes emphasis-width 2px the width that carries the focus ring; `base.css` hardcodes `outline: 2px solid var(--focus-ring)`. Task 2 specified the literal so the letter is met, but the token has no consumer and the px-confinement gate exempts every property containing `outline`, so nothing would notice if the two diverged.
- [x] [Review][Patch] LOW — **Two items handed to later stories have no ledger entry** [_bmad-output/implementation-artifacts/deferred-work.md]. Task 6 required undischarged work to be recorded *"so a later story can close them with the reasoning intact"*. Completion Note 7 states that tightening AC9's fully-round radius from "at most one" to "exactly one" belongs to the story that builds the column count chip, and Completion Note 8 leaves AC11's two unmeasurable clauses asserted by construction. Neither is in the ledger, so AC9's deferred tightening names no owner.
- [x] [Review][Patch] LOW — **Tracking records disagree, and a fourth stale hand-off file was missed** [_bmad-output/implementation-artifacts/1-2-the-design-foundations-every-surface-is-drawn-from.md]. The frontmatter says `baseline_commit: 3352676` while the Debug Log says *"Baseline before any change (commit `d5b8f92`, working tree clean)"*. `sprint-status.yaml` moved this story `backlog` → `review`, but the File List records `ready-for-dev` → `in-progress` → `review` and the Change Log's first row says "Status → `ready-for-dev`" — a state the tracking file never held. And `Yello.Client/App.razor:1-11` still reads *"No markup here carries a class, a style attribute or a colour - story 1.2 owns the design foundations"*, a fourth file carrying discharged hand-off prose beside the three Completion Note 14 enumerates.
- [x] [Review][Patch] LOW — **Task 5's evidence table does not match the count it claims** [_bmad-output/implementation-artifacts/1-2-the-design-foundations-every-surface-is-drawn-from.md:592]. Introduced as *"Every one of the 23 assertions below is an absence assertion"*, then 25 rows follow: one is the `CssCorpus.ThemeBoundaryRange` guard rather than a `[Fact]`, and one repeats a fact already listed. All 18 + 5 facts do appear, so the coverage claim itself holds — but the arithmetic does not, in a story whose review theme is claims outrunning evidence.

#### Deferred

- [x] [Review][Defer] **AC9's radius *mapping* clause is unverifiable and unrecorded** [tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs:720-750] — deferred by decision 4, owner epic 2. Only the radius value *set* is gated; that 2px goes on the Role chip / Label chips / Offer indicator, 3px on Tasks / columns / context bar / buttons, and 6px on dialogs / Task detail / invitation view cannot be checked with no components. Same position as Completion Note 7's "exactly one pill" tightening.
- [x] [Review][Defer] **`<html lang="en">` is hard-coded, so base.css's 26-locale exclusion list is inert** [Yello.Client/wwwroot/index.html:2] — deferred, needs a localisation infrastructure that does not exist yet.
- [x] [Review][Defer] **`opacity` or an alpha channel on a token-consuming surface would make all 36 computed ratios fiction** [tests/Yello.Tests.Architecture/ColorTokenContrastTests.cs:247-301] — deferred, latent; nothing uses opacity today.
- [x] [Review][Defer] **The Presence-count rationale contradicts the value it justifies** [Yello.Client/wwwroot/css/tokens.css:218-226] — deferred, inherited verbatim from `DESIGN.md:383`.

#### Dismissed as noise

One finding: the argument that `BuildLightPalette`'s resolve-through-the-boundary design is circular (because `Every_theme_boundary_rule_rebinds_all_fifteen_semantic_names` already requires each binding to equal `var(--{name}-light)`, making a divergence unreachable), and that the exact-string requirement over-constrains a future high-contrast or third theme. The exact-string requirement *is* the intended contract rather than an accident, the two gates are deliberately independent so that neither alone is load-bearing, and a third theme is not in scope for this story.

## Dev Notes

### Scope boundary — what this story does NOT build

| Not in this story | Owner |
|---|---|
| Any component — task card, column, context bar, dialog, picker, button | **Epic 2 onward.** UX-DR8 … UX-DR26 name them; this story builds the layer they are drawn from, not the components. |
| The lifted-card treatment (`rotate(-1deg)`, offset shadow, 2px border) | **Story 2.7** (accelerated pointer/touch paths). It is the one sanctioned exception to "no shadow" and there is no drag to hang it on yet. |
| Any Label chip colour, or the constrained Label palette | **Story 2.4** (UX-DR13). This story fixes the *rules* a Label fill must satisfy; it picks no colours. |
| A theme **toggle**, a stored theme preference, or an Account setting for it | Nothing upstream requires one. See *The theme boundary* below — this story implements the boundary, not a control. |
| E2E verification of 1.4.12 text-spacing and 200% text-only zoom (AC13) | **Blocked on B5.** Scenario X-11, E2E. Owner is the first story with a real surface after the `bmad-testarch-framework` run decides the browser binding. |
| A browser/E2E test project | Blocker **B5** is still open. `tests/TESTING-CONVENTIONS.md:209-216`. The contrast harness deliberately needs no browser. |
| Any code-coverage threshold | **Deliberately absent from the contract.** Do not invent one. |
| Any CSS for a surface that does not exist | The gate on exactly 30 tokens exists so an incomplete token set is *detectable*; a speculative component stylesheet is the mirror-image failure. |

### What story 1.1 hands over

Verified against the tree at `33526767`:

- **Zero CSS and zero JS in the solution.** No `*.css`, no `*.scss`, no `*.razor.css`, no `wwwroot/css/` directory. No npm, no `package.json`.
- `Yello.Client` is **standalone Blazor WebAssembly** (`Microsoft.NET.Sdk.BlazorWebAssembly`), `net10.0`, runtime `10.0.11`. It references exactly `Yello.Contracts` and `Yello.Merge`.
- `index.html` loads **no stylesheet**: the head is a preload placeholder, the favicon and an importmap placeholder; the body is `#app`, `#blazor-error-ui` and the framework script.
- `App.razor:13` is literally `<p>Yello</p>`, and `App.razor:1-11` records that no markup carries a class, a style attribute or a colour.
- **No UI system is inherited** — no MudBlazor, Fluent, Radzen or Bootstrap (`epics.md:214`). Every token carries a literal value rather than a delta.
- Three files carry the hand-off in prose and all three become stale when Task 1 lands: `index.html:15-23`, `index.html:26-41`, `AssemblyMarker.cs:11-14`.

### The 30-names / 26-values trap

**The count assertion must be over declared custom-property names, never over resolved values.** The 30 names
resolve to only **26 distinct hex values**, because three collisions are deliberate:

| Value | Names that share it |
|---|---|
| `#FB7185` | `danger`, `revoked-edge` |
| `#BE123C` | `danger-light`, `revoked-edge-light` |
| `#FFFFFF` | `surface-card-light`, `accent-on-light`, `danger-on-light` |

`DESIGN.md:313` states why the first two are separate names anyway: *"A separate token from `{colors.danger}`
on purpose, even though they may resolve to the same value"* — danger is reserved for the genuinely
irreversible, and losing access is a refusal, so without a distinct name the component and the rule
contradict each other. The UX validation report puts it as *"distinct in name even where it resolves to the
same value"*.

A harness that counts distinct values gets **26** and fails AC2. That number is not a coincidence worth
ignoring: the pre-remediation `epics.md` said *"26 colour tokens"*, and 26 is exactly the distinct-value
count. Whoever wrote 26 was almost certainly counting values. Record this so nobody "re-corrects" 30 back
down to 26 later.

### The contrast table, computed and verified

Every figure in `DESIGN.md:322-343` was independently recomputed from the hex values during story creation
using the WCAG 2.x formula. **All 36 reproduce to two decimals and all 18 pairs pass in both themes.**
`DESIGN.md` is arithmetically sound; the harness's job is to *prove* these from the tokens and catch drift,
not to re-derive them.

**Twelve text pairs, threshold 4.5:**

| Pair | Dark | Light |
|---|---|---|
| `text-primary` on `surface-card` | 13.21 | 17.35 |
| `text-primary` on `surface-page` | 15.73 | 16.05 |
| `text-muted` on `surface-card` | 6.53 | 7.33 |
| `text-muted` on `surface-page` | 7.78 | 6.78 |
| `presence` on `surface-card` | 7.42 | 5.36 |
| `presence` on `surface-column` | 8.05 | **4.61** ← thinnest |
| `danger` on `surface-card` | 5.90 | 6.29 |
| `danger-on` on `danger` | 6.29 | 6.29 |
| `accent` on `surface-card` | 4.96 | 6.81 |
| `accent` on `surface-column` | 5.38 | 5.86 |
| `accent-on` on `accent` | 5.63 | 6.81 |
| `role-chip-on` on `role-chip` | 8.56 | 8.45 |

**Six non-text pairs, threshold 3.0:**

| Pair | Dark | Light |
|---|---|---|
| `focus-ring` on `surface-card` | 7.17 | 4.61 |
| `focus-ring` on `surface-column` | 7.78 | **3.97** |
| `border-hairline` on `surface-card` | 3.63 | 4.48 |
| `border-hairline` on `surface-column` | 3.94 | 3.85 |
| `border-hairline` on `surface-page` | 4.32 | 4.14 |
| `border-hairline` on `role-chip` | 3.44 | **3.47** ← see below |

**Two adjacency ratios, NOT gated:** `surface-card` on `surface-column` 1.09 / 1.16; `surface-column` on
`surface-page` 1.10 / 1.07.

> **`DESIGN.md:341` states no light value for `border-hairline` on `role-chip` — the cell is an em-dash.**
> AC4 requires the harness to run "over both palettes", which is 36 computations from 35 stated figures. The
> missing value is **3.47** (`#6B7794` on `#E2E0FA`), computed during story creation, and it **passes** the
> 3.0 gate. Compute it like the other 35 rather than special-casing the row. Raised in *Questions* as a
> one-cell `DESIGN.md` fix so the source stops being short a figure.

**The table's grounds are written in shorthand.** `DESIGN.md` writes "on card", "on column", "on page". These
map to `surface-card`, `surface-column`, `surface-page`. A harness driven literally by those strings will not
resolve them.

**Four further ratios are ungated but generate binding design rules.** All four verified exact:

| Combination | Dark | Light | The rule it produces |
|---|---|---|---|
| `focus-ring` vs `accent` | 1.45 | 1.48 | The ring **would** vanish against an accented control. What protects it is `outline-offset: 2px`, which puts it on the ground behind. **Never offset 0, never inset.** (AC6) |
| `accent` vs `text-primary` | 2.66 | 2.55 | Below the 3:1 WCAG 1.4.1 needs when colour alone distinguishes a link. Hence `text-link` is **always underlined**. (AC10) |
| `accent` vs `danger` | 1.19 | 1.08 | They differ by hue alone and converge under deuteranopia. **Destructiveness is carried by copy, never colour.** (UX-DR7; no AC group of its own — implement it as a rule, not a gate) |
| `role-chip` vs `surface-card` | 1.05 | 1.29 | The chip fill is invisible against the context bar, so the Role chip **needs a border** to read as a chip at all. Not a contrast failure — a failure to read as a chip. |

### Why the harness must compute rather than restate

`docs/bmad-coverage.md:84` records what happened when these figures were done by hand during the UX phase:
*"Eight of twelve hand-computed figures were wrong, and the two genuine AA failures sat in pairs the table
never thought to state at all (accent-as-link against body text, 2.66:1; Role chip fill against its own
ground, 1.05:1)."*

Two lessons, both load-bearing: hand-computed contrast figures are unreliable at a rate of two-thirds, and
**the pairs that fail are the ones nobody thought to state**. The first is why AC4 says "computed by the
WCAG 2.x formula rather than estimated". The second is why the harness parsing `tokens.css` matters more
than the assertion list — and why *Two pairs the gate does not cover* below is worth recording rather than
leaving to be rediscovered.

### Two pairs the gate does not cover

Both are used in the product and neither appears in the 18. Both pass, so neither is a defect — but the gate
does not know about them:

- **`revoked-edge` appears in none of the 18 rows**, yet `DESIGN.md:462` makes it the structural border on
  `description-editor-readonly`, and UX-DR4 holds structural borders to 3:1. Computed: on `surface-page`
  **7.03 dark / 5.81 light**; on `surface-card` 5.90 / 6.29.
- **`focus-ring` is gated on card and column but not on `surface-page`**, yet the description editor sits on
  the page ground (`DESIGN.md:462`), so focus lands there. Computed: **8.55 dark / 4.27 light**.

Keep the count at exactly 18 as the AC requires — but say so deliberately in the harness rather than leaving
these to be discovered. The natural closing story is **7.2 / 7.4**, which builds the description editor.

### Where the harness lives, and why nothing else is a candidate

**`tests/Yello.Tests.Architecture`, as a new test class. No new project.**

`epics.md:519` and `tests/TESTING-CONVENTIONS.md:24-26`: *"Later stories add cases to these existing suites
rather than creating suites. If a new suite seems necessary, that is a conversation about the architecture,
not a project template."*

The architecture suite is the right home on every axis:

- It **already references `Yello.Client`** (`Yello.Tests.Architecture.csproj:51`), so no reference-edge change.
- It **carries no `--ignore-exit-code 8`** and must stay strict, so adding tests requires no csproj edit.
- It already has `RepositoryLayout.Root` for reading arbitrary repository files.
- It runs **first in CI** and takes seconds — the right place for a gate that should fail before anything slower starts.
- The contrast harness is a UNIT-level computation needing **no browser**, which is exactly why the test design placed it here and marked it *"Not blocked on B5 — do it early"*.

**What a new test project would cost**, if anyone is tempted: six gates fail until edited — the solution
inventory (`SolutionInventoryTests.cs:16-38`), the reference-edge table (a project with no row fails
outright, `ProjectFileGateTests.cs:113-120`), the `.slnx`/disk reconciliation, the Role-API scan's
read-every-assembly precondition (`RoleApiBanTests.cs:47-57` — it **fails** rather than skips), the zero-test
switch gate, and the layout gate. Two of those edits are visible architecture changes. Not worth it for a
class that does arithmetic on a text file.

Follow story 1.1's precedent for placement within the suite: the project-file gate assertions live in a
**separate test class** from the A-1 … A-15 ArchUnitNET series *"so the counts stay legible when later
stories add A-4 onward"*. The contrast harness is likewise outside that numbering — it reads CSS, not
bytecode.

### Why no package

WCAG relative-luminance and contrast-ratio maths is ~20 lines of `double` arithmetic with no dependency.
Adding a colour library would require satisfying four gates together (a pin in `Directory.Packages.props`, an
entry in `ExpectedNonAr1Pins` **with a stated reason**, a version-less `PackageReference`, and not being a
`GlobalPackageReference`) and would expose the build to `NuGetAuditMode=all` + `NuGetAuditLevel=low` with
`NU1900–NU1904` promoted to **errors** — meaning any advisory at any severity, direct or transitive, breaks
every build until someone hand-pins forward. That posture was reviewed and deliberately kept strict during
story 1.1. Do not spend it on arithmetic.

### The theme boundary, and the one decision this story cannot inherit

`DESIGN.md:299` fixes the *mechanism*: each semantic name resolves to its unsuffixed value under dark and its
`-light` sibling under light, **once, at the theme boundary**, and every component consumes the semantic name
only. AC1 gates exactly that.

**What no upstream document decides is what *selects* the theme.** There is no `prefers-color-scheme`
requirement, no theme toggle, no stored preference and no browser support matrix anywhere in the PRD, the
architecture spine, the epics or the UX spines. The PRD has no browser matrix at all.

This does **not** block the story: AC1 is satisfied by the boundary existing and resolving once, whatever
triggers it. Implement the boundary so the trigger is a one-line change later:

- `:root` declares the 15 semantic names bound to the dark values (dark is canonical).
- One selector block rebinds those same 15 names to the `-light` values.
- Make that block respond to `prefers-color-scheme: light` **and** to an explicit `[data-theme="light"]` on
  the root, so a stored preference can win over the OS later without restructuring.

Flagged in *Questions* — it is Lee's call whether a preference is stored, and if so, whether it is
Account-scoped (which would make it Account settings' business, and AD-24's).

### The vacuous-gate problem, and what story 1.1 learned about it

Story 1.1 accumulated **three review passes and 40+ findings**, and its own summary of the dominant theme is
the single most useful thing it hands this story:

> *"several gates assert something materially weaker than their names, comments and the ACs claim"*

and, from the second pass:

> *"this is not converging by patching"*

That is the exact hazard here, amplified. Eight of this story's thirteen ACs are conditioned on components
that **do not exist yet**: "Given any interactive component", "When components are inspected", "Given a text
link inside a sentence", "Given any user-visible string". A gate written as an assertion about today's tree
passes because the tree is empty, and keeps passing while story 2.2 writes the violation it was meant to catch.

Two rules follow, and Task 4 exists to enforce them:

1. **Every gate scans the repository, not a known file.** Glob `**/*.css` and `**/*.razor`. A gate that names
   the files it checks stops covering the files added after it.
2. **Every gate is planted against a violation a *later* story would plausibly write** — not merely against
   a synthetic one. A `.razor` using `var(--surface-card-light)`; a component with a hardcoded English
   string; a `box-shadow` on a card. Today's empty tree cannot exercise these, which is precisely why they
   must be planted deliberately.

Note the pattern the most recent commit established (`3352676`, *"Ask MSBuild what it evaluates, instead of
reading what the files declare"*): where a gate can assert **effective** state rather than declared text, it
should. The CSS analogue is parsing `tokens.css` for the values the browser will resolve, rather than
trusting a C# table that says what they ought to be.

### Coding-standard traps in this suite

`Opinionated.DotNet.CodingStandards` 0.0.11 is a `GlobalPackageReference` with
`TreatWarningsAsErrors=true` and `AnalysisLevel=latest-all`. Three of its banned APIs sit directly in the
path of WCAG arithmetic and string parsing:

- **`Math.Round` / `MathF.Round` without an explicit `MidpointRounding` argument is banned.** Pass one, or
  avoid rounding entirely and compare with a tolerance — which is better anyway, since the assertion is
  "≥ 4.5", not "equals 4.61".
- **`StringComparison.InvariantCulture` / `InvariantCultureIgnoreCase` are banned.** Use `Ordinal` /
  `OrdinalIgnoreCase` when parsing hex values and property names.
- `GenerateDocumentationFile=true` is on; `CS1591` and `SA1600` are `none`, so undocumented members are fine.

Underscored test names are safe: `CA1707` is `none` solution-wide and the naming rule reports at
`suggestion`. There is no `.editorconfig` — the standard is the `GlobalPackageReference`.

### Testing requirements

- **Runner:** `xunit.v3` 4.0.0 on Microsoft.Testing.Platform only. Test projects are `OutputType=Exe`. No `Microsoft.NET.Test.Sdk`.
- **Assertions:** xunit's own `Assert`. No FluentAssertions or Shouldly anywhere in the repo; do not introduce one.
- **`Yello.Tests.Architecture` does not carry `--ignore-exit-code 8` and must stay strict.** Adding tests to it requires no csproj change. (Had the harness gone into Isolation, Revocation or Merge, that project's switch would have had to be deleted — a gate asserts a project carrying the switch contains no `[Fact]`/`[Theory]`.)
- **Conventions:** one behaviour per test, named as an underscored sentence; no `Task.Delay` as synchronisation; an absence assertion validated against a planted signal. **No coverage threshold — do not invent one.**
- Class-level `[Trait]` is the established idiom in this suite.
- Commands: `dotnet restore` → `dotnet tool restore` → `dotnet build` → `dotnet test`. Verify exit codes the way CI will — running a suite's `.exe` directly uses xunit's own console runner, which returns 0 for zero tests and does not understand `--ignore-exit-code`.

### Project Structure Notes

- New files land at `Yello.Client/wwwroot/css/tokens.css` and a new class in `tests/Yello.Tests.Architecture/`.
  **No gate reads `.css` today** — `RepositoryLayout.SourceFilesOf` is `*.cs` only, and no existing test
  touches the client's static assets. This story writes the first CSS-aware gate in the repository.
- Adding a plain file requires **no** inventory edit: the inventory gate globs `*.csproj` only.
- Do **not** add a `PropertyGroup` restating `TargetFramework` or `RuntimeFrameworkVersion` to
  `Yello.Client.csproj` — declared exactly once in the root `Directory.Build.props`, and a gate fails on a
  restatement. Do not add a `Directory.Build.props` under `Yello.Client/`.
- If a `.razor.css` is ever added it produces the scoped bundle `Yello.Client.styles.css`, which needs its
  own `<link>`. Story 1.1 deliberately removed that link. This story does not need scoped CSS and should not
  introduce it — the token layer is global by definition.
- `docs/` and `TestResults/` are **not** excluded from repository tree walks; `bin`, `obj`, `artifacts`,
  `node_modules`, `.git`, `.vs`, `.claude`, `_bmad`, `_bmad-output` are.

### The mockups are not a token source

All seven files under `ux-designs/…/mockups/` carry a `:root` block, and **none of them is usable**. They
declare 13 abbreviated, non-semantic names — `--page`, `--col`, `--card`, `--border`, `--text`, `--muted`,
`--accent`, `--accentOn`, `--focus`, `--danger`, `--dangerOn`, `--role`, `--roleOn` — dark only, with no
`-light` siblings and no `revoked-edge`. `space-settings.html` additionally omits `--presence`.

That is precisely the incomplete token set AC2 exists to catch, in precisely the shape an implementer would
copy. Both spines state that they **win on conflict with any mock**, and the readiness report records that
*"an implementer working from `mockups/` rather than from `DESIGN.md` would build the wrong thing"*. The
four earliest mockups predate the border, type-scale and `readonly` corrections entirely.

**`DESIGN.md`'s frontmatter (`:10-125`) is the only token source.**

### Vocabulary

PRD §2 Glossary terms are used **verbatim** in every artifact including code. Forbidden in any identifier:
`Workspace`, `Tenant`, `Org`, `Organisation`, `Team`, and `User` where `Account` is meant.

Two notes specific to this story:

- **"Surface" is a homonym here.** `surfaces-and-journeys.md` uses *surface* for a UI area (Space home, Task
  detail, Space settings…); the tokens `surface-page`/`surface-column`/`surface-card` use it for a
  background ground. The story title *"every surface is drawn from"* straddles both. Do not map the three
  ground tokens onto the eight-row surface inventory — they are unrelated.
- `token`, `theme`, `focus ring`, `role chip` and `revoked edge` are **absent from `glossary.md`**, despite
  its rule that a new domain noun is added in the same pass. They entered through `DESIGN.md` and were never
  round-tripped. Raised in *Questions*; it does not block implementation.

### The WCAG version, stated so it is not mistaken for drift

- The PRD's NFR-9 pins **WCAG 2.1 AA** — that is the release gate.
- AC8's 24px target floor is **WCAG 2.2 AA's 2.5.8**. WCAG 2.1 AA has no target-size criterion at all
  (2.5.5's 44×44 is AAA). This is a deliberate commitment **above** the PRD's stated floor, and `DESIGN.md:409`
  argues it explicitly. It is not an error and should not be "corrected" down.
- AC4's *"WCAG 2.x formula"* refers to the **arithmetic** — sRGB linearisation at 0.03928,
  `(L₁+0.05)/(L₂+0.05)` — which is identical across 2.0, 2.1 and 2.2. Not a version conflict.

### Where the numbers actually come from

The spec kernel at `_bmad-output/specs/spec-yello/` was checked in full during story creation and **contains
none of this story's numbers** — not 30, 15, 18, 12, 6, 4.5, 3.0, 16px or 1.5, and not one of the 15 token
names. Its only bearing on this story is `quality-budgets.md:84` (WCAG 2.1 AA on five flows). `SPEC.md:22`
says so itself: *"Treat this spec as the reasoning record and audit trail, not as the live contract."*

Likewise **the PRD contains no functional requirement about colour, tokens, typography, theming or focus.**
It constrains this story through NFR-9 and three consequences: nothing conveyed by colour alone; keyboard
parity on the Board (which is what makes one visible focus treatment load-bearing); and a 16 ms local render
budget at a 5,000-Task bound, which is why the token layer must stay cheap — no filters, no backdrop-blur, no
shadow.

**The authority for every number in this story is `epics.md:527-591` and `DESIGN.md:10-125`.** Cite those.

> Worth knowing, not acting on: `prd.md:18` still reads *"No UX specification exists yet."*, contradicted four
> lines later at `:22` which records the `bmad-ux` pass that produced `DESIGN.md` and `EXPERIENCE.md`. Anyone
> resolving the token spec against the PRD will find nothing and may conclude it is missing. It is not — it
> is in `DESIGN.md`.

### Previous story intelligence

Story 1.1 is `done`. Three review passes, 40+ findings, all closed. What carries forward:

- **The scope hand-off is explicit and was honoured**: *"Any CSS, colour token, type scale, spacing scale, focus ring, or the contrast harness | **Story 1.2.** Do not write a `:root` block or any hex value."* Verified — the tree contains none.
- **`#blazor-error-ui` is a declared variance**, restored unstyled with its contractual id and classes. Its styling is this story's, and story 1.1's third pass confirmed the variance *"does not pre-empt story 1.2"*.
- **The dominant defect class was gates weaker than their claims.** See *The vacuous-gate problem* above.
- **Two entries sit open in `deferred-work.md`**, neither owned by this story: the shared-fixture container topology (owner: story 1.9) and declared-vs-effective reference closure (revisit on the next table edit). Do not action them; do not let them decay.
- Two of story 1.1's seven open questions are still unanswered and touch nothing here (version drift, the floating container tag).

### Git intelligence

`33526767` is the baseline. The last three commits are all review-closing passes on story 1.1, reworking the
architecture suite heavily — `9459d4b` (33 findings), `65c3e50` (second pass), `3352676` (third pass, adding
`MsBuildEvaluation.cs`). Two patterns worth inheriting:

- **Assert evaluated state, not declared text** — the whole point of `3352676`. The CSS analogue is stated above.
- **Exact equality in both directions beats subset checks.** Story 1.1 hardened Gate A from subset to exact
  equality and a review still found the same defect class left in place elsewhere. The token-count assertion
  should be exact-set equality over the 15 names, not "contains at least".

### References

- Story and AC definition, UX-DR1 … UX-DR7 / UX-DR40 / UX-DR42, Story Coverage Index: [Source: `_bmad-output/planning-artifacts/epics.md#Story 1.2: The design foundations every surface is drawn from`] (`:521-591`), [`#Requirements Inventory`] (`:214-224`, `:266`, `:268`), [`#Story Coverage Index`] (`:327`)
- Token values, contrast table, focus-ring reasoning, border rendering, typography, Do's and Don'ts: [Source: `.../ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md#Colors`] (`:10-125` frontmatter, `:295-366`), [`#Typography`] (`:368-399`), [`#Layout & Spacing`] (`:401-417`), [`#Elevation & Depth`], [`#Shapes`], [`#Do's and Don'ts`] (`:474-492`), [`#Motion`] (`:494-511`)
- Accessibility floor, live regions, i18n/RTL, focus destinations: [Source: `.../EXPERIENCE.md#Accessibility Floor`] (`:366-390`), [`#Internationalisation`] (`:431-441`), [`#Foundation`] (`:21-30`)
- Blazor WASM as the client stack, no inherited UI system: [Source: `.../EXPERIENCE.md#Foundation`] (`:23`), [`epics.md:214`]
- NFR-9, NFR-3's 16 ms budget, NFR-8's 5,000-Task bound, the stale §0 sentence: [Source: `.../prds/prd-YelloBMAD-2026-08-15/prd.md#5. Cross-Cutting Non-Functional Requirements`] (`:735-739`, `:686-690`, `:718-733`), (`:18`, `:22`)
- X-1 / X-2 as story-1.2 P0 ACs, "not blocked on B5", X-11 as blocked E2E: [Source: `_bmad-output/test-artifacts/test-design/YelloBMAD-handoff.md#P0 test scenarios that must become story acceptance criteria`] (`:83`), [`.../test-design-qa.md`] (`:309-310`, `:369`, `:485`, `:231`)
- The token-count and gated-pair corrections (26→30, 20→18), and that they are closed: [Source: `.../implementation-readiness-report-2026-08-22.md#Remediation Applied — 2026-08-22`] (`:1402-1411`), origin at (`:829-846`)
- Hand-computed figures were two-thirds wrong; NFR-9 as the carrier: [Source: `docs/bmad-coverage.md`] (`:84`, `:228`, `:31`)
- Scope hand-off, testing conventions, the planted-signal rule, B5: [Source: `_bmad-output/implementation-artifacts/1-1-the-solution-skeleton-and-its-build-gates.md#Scope boundary`] (`:425`), [`#Project Structure Notes`] (`:539`), [`#Environment preflight`] (`:558`), [`tests/TESTING-CONVENTIONS.md`] (`:24-26`, `:93-96`, `:98-100`, `:209-216`)
- Accessibility floor in the kernel; the kernel is a reasoning record, not the contract: [Source: `_bmad-output/specs/spec-yello/quality-budgets.md`] (`:84-86`), [`.../SPEC.md`] (`:22`)
- WCAG 2.x relative luminance and contrast ratio: https://www.w3.org/TR/WCAG21/#dfn-relative-luminance · https://www.w3.org/TR/WCAG21/#dfn-contrast-ratio

### Questions for Lee — raised, not resolved

1. **What selects the theme?** No upstream document decides whether light is chosen by `prefers-color-scheme`, a stored preference, or both — and if stored, whether it is Account-scoped (making it Account settings' business, and AD-24's). This story implements the boundary so the trigger is a one-line change, but the decision is yours.
2. **AC5's misattributed quotation.** `epics.md:552` quotes `DESIGN.md:347` for the two adjacency rows; that sentence is about two different combinations. The fix is a citation change to `DESIGN.md:345` in a `status: final` document — the same shape as story 1.1's AC5 amendment. Amend `epics.md`, or leave the correction recorded only here?
3. **The missing light figure.** `DESIGN.md:341` leaves the light cell for `border-hairline` on `role-chip` as an em-dash. It is 3.47 and passes. Fill it in, or mark the row dark-only?
4. **The two ungated structural pairs.** `revoked-edge` on `surface-page` (7.03/5.81) and `focus-ring` on `surface-page` (8.55/4.27) are used in the product, held to 3:1 by UX-DR4, and in none of the 18. Both pass today. Keep the gate at exactly 18 as the AC says, or widen it to 20 gated pairs and amend UX-DR7?
5. **AC13's E2E half.** 1.4.12 text-spacing and 200% text-only zoom cannot be verified without a surface or a browser binding. Confirm it defers to the first story after the `bmad-testarch-framework` run — and is that run happening now that story 1.1 has unblocked it?
6. **Glossary round-trip.** `token`, `theme`, `focus ring`, `role chip` and `revoked edge` are absent from `glossary.md` despite its same-pass rule. Add them, or accept that the design vocabulary lives only in `DESIGN.md`?

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (`claude-opus-5[1m]`), via the `bmad-dev-story` workflow.

### Debug Log References

**Baseline before any change** (commit `d5b8f92` — the story-context commit, whose parent `3352676` is
this story's `baseline_commit`; the frontmatter names the parent because the diff under review is the
implementation, and `d5b8f92` added only this file. Noted at code review 2026-08-27, where the two
figures reading differently was raised as a tracking inconsistency): `dotnet build` succeeded with 0
warnings; `dotnet test` returned **exit 0 with 54 tests passing**, `Yello.Tests.Architecture`
contributing 47.

**Final state:** `dotnet build` succeeds with **0 warnings, 0 errors**; `dotnet test` over the
solution returns **exit 0 with 77 tests passing, 0 failed, 0 skipped**.
`Yello.Tests.Architecture` now contributes **70** — the 47 it inherited plus the 23 this story
adds. The other three genuinely-empty suites (Isolation, Revocation, Merge) still report zero
tests and exit 0; `Yello.Tests.Slices` still runs its own cases.

**Independent arithmetic verification, before writing the harness.** All 36 gated ratios plus the
two adjacency rows and the four rule-bearing combinations were recomputed from the hex values by an
independent script. **All 36 reproduce `DESIGN.md`'s figures to two decimals; 0 mismatches; 0 gated
pairs below threshold.** The distinct-value count came out at **26 against 30 names**, confirming
the trap in Dev Notes is real rather than theoretical. The missing light cell at `DESIGN.md:341`
(`border-hairline` on `role-chip`) computes to **3.4693**, i.e. the 3.47 the story predicted, and
passes 3.0.

**Token transcription verified mechanically, not by eye.** Two scripted checks compared
`tokens.css` against `DESIGN.md`'s frontmatter: all **30 colour names present with every value
matching exactly**, and every type, spacing, radius, border and motion value matching, with all 8
type roles binding all 5 axes, every line-height ≥ 1.5 and every type size in `rem`. The first
check caught a real transcription error I had introduced — `--revoked-edge-light` written as
`#FFFFFF` instead of `#BE123C`. The contrast harness would **not** have caught it: white on
`surface-page-light` clears 3:1 comfortably, so the gate would have been green over the wrong
colour. Recorded because it is the exact argument for having the check.

**Asset link verified in both build and publish.** The `#[.{fingerprint}]` placeholders resolve to
`css/tokens.css` and `css/base.css` in the Debug build and in a Release publish, and the published
`wwwroot/css/` contains files of exactly those names — so neither link 404s. No literal
`#[.{fingerprint}]` text survives into the generated `index.html`.

#### Task 5 — every gate failed on purpose first

`tests/TESTING-CONVENTIONS.md:93-96` makes this a rule: *"An absence assertion must be validated
against a planted signal, or it is not a test."* The table below has **25 rows for 23 `[Fact]`
assertions**: one row is the `CssCorpus.ThemeBoundaryRange` guard, which is a static precondition
rather than a test, and one row records a second half of a fact already listed. All 18 + 5 facts do
appear, so the coverage claim holds — the arithmetic in this sentence did not, and was corrected at
code review 2026-08-27. Every one of those assertions is an absence
assertion against a tree with no components, so each was proved against a real violation and the
violation then reverted. Every failure message named the file, the line, the property, the value
and the selector.

| Assertion | Planted violation | Result |
|---|---|---|
| `The_colour_token_layer_declares_exactly_the_thirty_names...` | deleted `--presence` | fails: *"'--presence' is missing"*, *"29 colour tokens are declared; the design states exactly 30"* |
| `Every_gated_pair_meets_its_threshold_in_both_palettes` | `--text-muted-light` → `#C9CFDD`; `--surface-card` → `#FFFFFF` | fails: 8 pairs named with computed ratios, e.g. *"light: 'text-muted' (#C9CFDD) on 'surface-card' (#FFFFFF) is 1.56:1, below the required 4.50:1"* |
| …same, resolution half | deleted `--presence` | fails: *"34 of 36 ratios were computed … a pair that resolves in one theme and not the other is half a gate"* |
| `The_two_surface_adjacency_ratios_are_deliberately_low...` | `--surface-card` → `#FFFFFF` | fails: *"'surface-card' on 'surface-column' is 17.24:1, at or above 1.50:1"* |
| `The_gated_set_is_exactly_the_eighteen_pairs...` | removed the `border-hairline`/`role-chip` row | fails: *"17 pairs; AC4 names 18"*, *"5 pairs are gated at 3.00:1; AC4 names 6"* |
| `Both_palettes_resolve_every_semantic_name_to_a_hex_colour` | deleted `--presence` | fails: *"dark: '--presence' does not resolve to a hex colour"* |
| `CssCorpus.ThemeBoundaryRange` guard | duplicated the `THEME BOUNDARY BEGIN` marker | **all 23 design assertions error rather than pass**, exit code 2: *"must contain exactly one … found 2 and 1"* |
| `No_component_references_a_light_theme_token` | `.razor` with `style="…var(--surface-card-light)…"`; and a `.css` rule using it | fails on both: *"Planted.razor:3 references '--surface-card-light'"*, *"planted2.css:4 references '--surface-card-light'"* |
| `Every_theme_boundary_rule_rebinds_all_fifteen_semantic_names` | removed `--role-chip` from the `[data-theme="light"]` rule only | fails: *"tokens.css:129 (':root[data-theme=\"light\"]') does not rebind '--role-chip', so that one token stays at its dark value under the light theme"* |
| `No_colour_is_stated_as_a_literal_outside_the_token_layer` | `background-color: #18213C`; `color: rgba(…)` | fails on both |
| `Type_is_never_sized_in_absolute_pixels` | `font-size: 13px`; `line-height: 18px` | fails on both |
| `Pixel_lengths_outside_the_token_layer_come_from_a_token` | `margin-block: 12px`, `padding-inline: 9px`, and 5 more | fails, 7 offences named |
| `The_focus_ring_is_never_removed_or_drawn_at_offset_zero` | `outline: none`; separately `outline-offset: -2px` | fails on both |
| `A_visible_focus_treatment_is_declared_for_focus_visible` | removed `outline-offset`; separately removed the whole rule | fails on both: *"sets no 'outline-offset'"*, *"No rule in the repository draws an outline on ':focus-visible'"* |
| `No_surface_carries_a_shadow` | `box-shadow: 0 2px 4px var(--danger)` | fails |
| `No_structural_border_is_thinner_than_the_hairline_width` | `border-block-start: 1px solid …` | fails: *"sets 'border-block-start' to 1px"* |
| `The_interactive_target_floor_is_declared_and_never_lowered` | `--target-min: 20px`; `min-height: 16px` | fails on both, and names both real consumers that the token change dragged below the floor |
| `Only_the_four_radius_values_are_used` | `border-radius: 8px`; separately `border-radius: 50%` | fails on both |
| `The_fully_round_radius_is_declared_once_and_used_by_at_most_one_component` | a second `9999px` literal; two rules using `var(--radius-full)` | fails on both clauses |
| `The_text_link_is_underlined_and_no_rule_removes_it` | `a.x { text-decoration: none }`; separately removed the underline from `.text-link` | fails on both |
| `No_user_visible_string_literal_appears_in_a_component` | `.razor` with `<p>Loading your Spaces, please wait.</p>`, `<button title="Delete this Task">Delete</button>`, `@Count overdue` | fails, 4 offences: three text nodes and the `title` attribute. The `@code` block and the `@Count` expression were correctly excluded; `overdue` was still caught |
| `Uppercase_is_applied_only_inside_a_locale_scoped_rule` | unscoped `text-transform: uppercase` | fails: *"which is not locale-scoped"* |
| `Reduced_motion_neutralises_every_transition_and_animation` | removed `!important` from the reset; separately an `!important` transition outside the block | fails on both clauses |
| `No_physical_left_or_right_property_is_used_where_a_logical_one_exists` | `margin-left: 9px`; `text-align: left` | fails on both — property and value |
| `No_box_is_given_a_fixed_height` | `height: 32px` | fails |

**The planting found two real defects in the gates, which is the entire reason the story mandates
it.** Both are fixed, and both were re-proved against the same plants afterwards:

1. **`A_visible_focus_treatment_is_declared_for_focus_visible` accepted a rule that drew no ring.**
   It selected rules by any `outline*` declaration, so a planted
   `.x:focus-visible { outline-offset: -2px }` satisfied it — the gate found a "treatment", found
   no width to measure, and reported green **while `base.css` had no focus rule at all**. Now
   filtered on `outline`/`outline-width` (`IsRingDeclaration`), so a rule that only positions a
   ring no longer counts as declaring one.
2. **`Only_the_four_radius_values_are_used` could not see a percentage at all.** The non-px length
   pattern ended `(?:%|em|…)\b`, and `%` is not a word character — so there is no word boundary
   between it and the end of the value, and `border-radius: 50%` matched nothing. That is precisely
   how a circular avatar arrives, which `DESIGN.md:431` forbids by name. The `\b` now applies to
   the alphabetic units only.

### Completion Notes List

1. **The token layer is complete and mechanically verified against its only source.** 30 colour
   tokens (15 semantic names bound to dark values, 15 `-light` siblings), 8 type roles × 5 axes,
   two system font stacks, the 3/6/9/12/18/24/36 spacing scale, the four `rem` internal-padding
   values, `--target-min: 24px`, the five radius names over four values, the two border widths and
   the eight motion tokens — every value verbatim from `DESIGN.md:10-125`, checked by script rather
   than by eye. No npm, no bundler, no preprocessor, no token-build step, no webfont, no
   `@font-face`, no external request. Nothing was taken from `mockups/`.

2. **Two files, one job each.** `tokens.css` *declares*; `base.css` *applies* the parts that must
   hold for every surface — the 8 type-role classes, the focus ring, the text link, the target
   floor, the reduced-motion contract, the locale-aware casing, and `#blazor-error-ui`. It builds
   no component. The story named only `tokens.css`; the split is mine, and it keeps the AC2 count
   and the palette as statements about one file while everything else globs.

3. **The theme boundary is one delimited region containing two rules, and this is a deliberate
   reading of AC1 rather than a shortcut.** AC1 requires the theme to resolve "once, at the theme
   boundary". A CSS rule cannot union a media condition with an attribute selector, so answering
   both the OS preference (`prefers-color-scheme: light`, scoped `:not([data-theme="dark"])`) and
   an explicit `[data-theme="light"]` needs two rules. The alternatives that would collapse it to
   one — the empty-custom-property "space toggle" hack, or a JS-set class — are respectively
   unreadable in a foundation layer and a new dependency in a repository with zero JS. The region
   is marked by `THEME BOUNDARY BEGIN`/`END` comments; a gate requires exactly one well-formed
   pair, requires **every** rule inside to rebind **all 15** names to their own `-light` siblings,
   and refuses every `-light` reference outside it in any `.css`, `.razor` or `.html`. The
   partial-rule failure mode this closes is real and subtle: 14 of 15 rebindings would give a user
   with a stored light preference one dark token on a light ground, and the contrast harness reads
   only the first boundary rule, so it would not notice.

4. **`color-scheme` is the one addition beyond the story's list.** `:root` sets
   `color-scheme: dark` and the boundary flips it to `light`. It is one line, it belongs to the
   theme decision rather than to any component, and without it every scrollbar and form control in
   the product renders light on a dark ground. Flagged because it is an addition, small as it is.

5. **The harness computes; it does not restate.** `tokens.css` is parsed, and the **light palette
   is resolved through the theme boundary's own rebindings** — `--accent: var(--accent-light)`
   followed to a hex — rather than by appending `-light` to each name. That distinction is the
   point: the naming convention is not what renders, so a boundary that rebound `--accent` to the
   wrong sibling, or failed to rebind it, would otherwise leave the harness verifying colours the
   light theme never shows. A bug of exactly this shape occurred during implementation and is worth
   recording: the `var()` capture group included the leading `--` while the token map was keyed
   without it, so **every** light lookup missed and the light palette resolved to nothing. The
   harness did not report a contrast failure — it reported *"18 of 36 ratios were computed"*, which
   is why that count is asserted rather than assumed.

6. **AC3's gate is split in two, and one clause is deliberately narrower than Task 4's wording.**
   Task 4 asks for "no `font-size` in `px` anywhere; `px` permitted only on border widths, radii
   and `outline-offset`". Taken literally the second clause fails the token layer itself:
   `DESIGN.md:99-113` states the spacing scale, the 24px target floor and the 10px long-press slop
   in px **on purpose**, because those are structural steps that must not scale with text. AC3's
   own text confines the restriction to type — *"never on type"*. So: the **type** ban is absolute
   and applies inside `tokens.css` too, covering `font-size`, `line-height`, `letter-spacing`,
   `word-spacing` and the `font` shorthand, plus a separate check that the root font-size is a
   percentage rather than a px value. The **confinement** gate exempts the token layer and requires
   every other file to take its lengths from a token, permitting px literals only on radii and
   outlines. Border widths are not exempted there — they are held to the 1.5px hairline floor by
   their own gate, which is stricter than the AC's wording, not looser.

7. **The pill radius is gated at *at most* one consumer, not exactly one, and this is stated rather
   than hidden.** AC9 says the fully-round radius is used for exactly one component — the column
   count chip — which **epic 2 builds**. Today there are no components, so "exactly one" is
   unsatisfiable and gating it at 1 would mean shipping a red build or quietly disabling the check.
   The gate therefore asserts the `9999px` literal appears **exactly once** (its declaration, so no
   component can spell out a pill radius) and that `var(--radius-full)` has **at most one**
   consumer. Tightening it to exactly one belongs to the story that builds the chip.

8. **AC11: what is gated, what is asserted by construction, and two declared variances.** The gate
   scans every `.razor` for literal text nodes and literal localisable attributes, after removing
   Razor comments, directives, `@code`/`@functions`/`@{}` blocks, Razor expressions and HTML
   entities.
   - *Variance 1, the one the story named:* `index.html` carries English strings — "Loading
     Yello", "An unhandled error has occurred.", "Reload". They are emitted by a static file
     **before the WebAssembly runtime exists**, so no resource lookup could serve them. The gate
     scans `.razor`, where localisation is actually available. Recorded in `index.html` itself and
     in the gate's own remarks, not exempted silently.
   - *Variance 2, which the story did not anticipate:* `App.razor:13` is `<p>Yello</p>`, so the
     tree is **not** free of `.razor` literals as the story assumed ("Today there are no
     components"). Rather than exempt a category or leave the gate carrying a silent hole, the
     permitted set is exactly the **PRD §2 Glossary proper nouns** — currently the single word
     `Yello`. A brand name is not translated in any locale, and a resource entry for it would
     externalise a string with one value in every language. Every other literal fails, as the
     planted violations demonstrate. This is a narrow rule with a checkable reason, not a
     heuristic like "short strings are fine".
   - *Asserted by construction, not gated, and not claimed to be:* AC11's other two clauses — *"no
     label sized to its English string"* and *"metadata never aligned by character count"*. Neither
     is statically detectable and there is no component to measure. `base.css` discharges them
     constructively: no rule sets a width, no rule sets a `height`, internal padding is in `rem` so
     it grows with text, and nothing aligns by character cell. Writing a gate that appeared to
     cover them would be the defect class this story exists to avoid.

9. **AC8 gained a gate although no task asked for one.** AC8 appears in no task's AC list — Tasks 1
   and 2 cover 1/2/3/6/7/9/10/12 and Task 3 covers 4/5 — so it would have been discharged only
   constructively. It is cheaply and genuinely gateable, so it is: `--target-min` must be 24px, some
   rule must apply `min-height: var(--target-min)`, and no minimum-height declaration anywhere may
   fall below 24px. AC13's statically-detectable half got the same treatment (`height` with a fixed
   length is refused corpus-wide).

10. **AC5's citation is corrected in the harness, as the story instructs.** `epics.md:552` quotes
    `DESIGN.md:347` for the two adjacency rows, but that sentence introduces two *different*
    combinations at `:349`/`:350` — `accent` against `text-primary`, and `accent` against `danger`
    — which are not table rows at all. The rows are governed by `DESIGN.md:345`: *"The last two
    rows are stated for information, not as targets."* The harness cites `:345`. The figures were
    always right; only the citation was wrong. **`epics.md` is `status: final` and has not been
    amended — that remains question 2 for Lee.**

11. **The adjacency rows are asserted *low*, which is the only direction that means anything.**
    They are checked to sit below 1.5:1 and above 1.0:1, and separately the gated-pair table is
    asserted **not** to contain them. If one ever climbed past 1.5 it would have stopped being an
    adjacency step, and the design decision that the hairline — not luminance — separates grounds
    would no longer be true of the palette.

12. **The gated set is held at exactly 18, so widening it stays a deliberate act.** Twelve text
    pairs at 4.5:1, six non-text at 3.0:1, no duplicates, and every name in the table required to
    be one of the 15. The class carries the threshold as an enum rather than a bare `double` so the
    12/6 split is counted by enum comparison rather than floating-point equality. The two
    structural pairs knowingly outside the 18 — `revoked-edge` on `surface-page` (7.03/5.81) and
    `focus-ring` on `surface-page` (8.55/4.27) — are named in the harness and recorded in
    `deferred-work.md` with story 7.2/7.4 as owner. Both pass today.

13. **No new project, no new package, no new dependency.** 23 assertions added to
    `Yello.Tests.Architecture` in two new classes, outside the A-1…A-15 ArchUnitNET numbering for
    the same reason the project-file gates are — they read text files, not bytecode. The suite
    carries no `--ignore-exit-code 8` and needed no csproj edit. WCAG luminance and contrast are
    ~20 lines of `double` arithmetic; the CSS parser is hand-written. `Math.Round` is not used at
    all (nothing rounds except message formatting, and the assertion is "≥ 4.5", not "= 4.61"), and
    every string comparison is `Ordinal`/`OrdinalIgnoreCase`.

14. **Three stale hand-off records updated, which is what story 1.1's review history asks for.**
    Six of story 1.1's findings were record drift. `index.html:15-23` claimed this story ships no
    stylesheet; `index.html:26-41` said story 1.2 owns styling the error banner; and
    `AssemblyMarker.cs:11-14` said the tree has no CSS. All three now describe the tree as it is,
    each marked as a correction rather than silently rewritten.

15. **Not built, deliberately.** No component of any kind. No lifted-card treatment (story 2.7). No
    Label chip colours (story 2.4). No theme toggle or stored preference — the boundary answers
    both triggers, but nothing *selects* a theme and no upstream document decides what should
    (question 1, and recorded in `deferred-work.md`). No E2E/browser project (blocker B5). No
    coverage threshold. No scoped `.razor.css`, so the `Yello.Client.styles.css` link story 1.1
    removed stays removed.

16. **The six questions in the story remain open and none of them blocked implementation.**
    Question 1 (what selects the theme) is recorded in `deferred-work.md`. Question 2 (AC5's
    citation) is corrected in code but `epics.md` is untouched. Question 3 (the missing light cell)
    is computed as **3.47** and passes, but `DESIGN.md` still shows an em-dash. Question 4 (widening
    to 20 gated pairs) is recorded with an owner. Question 5 (AC13's E2E half) is recorded with an
    owner. Question 6 (the glossary round-trip for `token`, `theme`, `focus ring`, `role chip`,
    `revoked edge`) is untouched — `glossary.md` was not edited.

### File List

**Added**

- `Yello.Client/wwwroot/css/tokens.css`
- `Yello.Client/wwwroot/css/base.css`
- `tests/Yello.Tests.Architecture/WcagContrast.cs`
- `tests/Yello.Tests.Architecture/CssCorpus.cs`
- `tests/Yello.Tests.Architecture/ColorTokenContrastTests.cs`
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.cs`

**Modified**

- `Yello.Client/wwwroot/index.html` — linked both stylesheets with the `#[.{fingerprint}]` form; replaced the two stale hand-off comments; recorded the pre-boot copy variance.
- `Yello.Client/AssemblyMarker.cs` — replaced the stale "adds no components and no CSS" hand-off remark.
- `_bmad-output/implementation-artifacts/deferred-work.md` — added the story 1.2 section: AC13's E2E half, the two ungated structural pairs, and the undecided theme trigger.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `1-2-…` `backlog` → `review`, then → `done` at code review. (This line previously read `ready-for-dev` → `in-progress` → `review`; the tracking file never held either intermediate state, and the Change Log's first row claimed a `ready-for-dev` transition that was never written. Corrected at code review 2026-08-27.)
- `_bmad-output/implementation-artifacts/1-2-the-design-foundations-every-surface-is-drawn-from.md` — this file: task checkboxes, Dev Agent Record, File List, Change Log, Status.

**Deleted**

- None.

### Change Log

| Date | Change |
|---|---|
| 2026-08-27 | Story created. `sprint-status.yaml` held `backlog` at this point — the `ready-for-dev` this row originally claimed was never written to the tracking file. Corrected at code review. |
| 2026-08-27 | Task 1: added `tokens.css` — 30 colour tokens, the theme boundary, 8 type roles, the spacing, radius, border and motion scales. Every value verified against `DESIGN.md` by script; one transcription error (`--revoked-edge-light`) caught and fixed that way. |
| 2026-08-27 | Task 2: added `base.css` — type-role classes, focus ring, text link, target floor, reduced-motion contract, locale-aware casing, `#blazor-error-ui`. Linked both sheets from `index.html` and corrected three stale hand-off records. |
| 2026-08-27 | Task 3: added the contrast harness — WCAG 2.x arithmetic in-repo, `tokens.css` parsed, the light palette resolved through the theme boundary. 36 ratios computed; all 18 pairs pass in both palettes. No new project, no new package. |
| 2026-08-27 | Task 4: added 18 repository-scanning gates for AC1, 3, 6, 7, 8, 9, 10, 11, 12, 13 and UX-DR42. |
| 2026-08-27 | Task 5: proved all 23 assertions against planted violations. Two real gate defects found and fixed — a focus rule that drew no ring was accepted, and `border-radius: 50%` was invisible to the radius gate. |
| 2026-08-27 | Task 6: recorded AC13's E2E half, the two ungated structural pairs, and the undecided theme trigger in `deferred-work.md`. |
| 2026-08-27 | Full solution green: `dotnet build` 0 warnings, `dotnet test` exit 0 with 77 passing (was 54). Status → `review`. |
| 2026-08-27 | Code review of `3352676..HEAD`, three parallel adversarial layers. 5 decisions resolved by Lee, **27 patches applied**, 4 items deferred with owners, 1 dismissed. The dominant finding was that a single inline `style` attribute defeated six ACs with the suite reporting 70/70 green; `CssCorpus` now parses `style` attributes and `<style>` bodies into the corpus, so every CSS gate sees markup. Five new gates: the stylesheet links, `var()` resolution, the non-colour token families, AC7's device-pixel snapping, and C# copy literals. Architecture suite 70 → **75 assertions**, all re-proved against planted violations. |
| 2026-08-27 | **Amendments to `status: final` documents**, per Lee's decisions: `epics.md:219` (UX-DR2) and `:542` (AC3) reworded so the px restriction confines to type rather than contradicting the token layer; `epics.md:224` (UX-DR7) now sanctions the adjacency bound the harness already asserted; `DESIGN.md:46` and `:377` reworded to match. Deliberate amendments, not drafting fixes. |
| 2026-08-27 | Status → `done`. |
