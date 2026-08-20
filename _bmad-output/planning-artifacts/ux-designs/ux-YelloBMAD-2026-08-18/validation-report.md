# Validation Report — Yello

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md`
- **Run at:** 2026-08-20
- **Lenses:** rubric walker · accessibility · isolation
- **Findings:** 111 total — 8 critical, 38 high, 34 medium, 31 low
- **Disposition:** 106 resolved · 2 escalated to `bmad-architecture` · 3 open by decision

## Overall verdict

Three lenses ran in parallel against the first complete draft. All three independently reached the same shape of conclusion: the pair is unusually disciplined for a first draft, and the places it fails are places where a stated principle did not survive arithmetic or implementation reality. Flow coverage and shape fit were *strong* on the rubric; the isolation audit found **no specified leak** and cleared 25 named surfaces; the accessibility audit credited the focus-ring separation, Presence-as-dot-plus-text, the per-script uppercase fallback and the explicit refusal to let the long-press gesture discharge NFR-9 as decisions most design specs get wrong.

The accessibility verdict was nonetheless *not gate-ready*, and it was right. Three failures were real and structural: no single-pointer alternative to the Board drag on a touch tablet at 768–1279px — a **WCAG 2.5.1 Level A** failure, not AA; a keyboard grammar bound to arrow keys that NVDA and JAWS consume in browse mode, so the accessible path was unreachable by exactly the population NFR-9 protects; and an FR-34 interruption that makes the focused node `readonly` or replaces it outright with no specified focus destination, on the requirement the PRD names as the criterion the product should be judged on. One fix — an explicit non-gesture **Move control** at every breakpoint — closed the first two.

The isolation audit's contribution was to show that the spines specified the server-facing half of isolation and were nearly silent on the client-facing half. AD-2 enforces scoping in the database; AD-11 puts a full replica in the client; SM-1's suite exercises requests. A leak rendered from client memory issues no request, so the release gate structurally cannot see it — and the replica's lifetime across a Space switch, a sign-out and a lease invalidation was never specified.

On the author's own contrast table: eight of twelve figures were arithmetically wrong. None flipped a pass into a fail, but the table was presented as verification against a release gate, and its one "little headroom" warning sat on the token with 10% clearance while the actual thinnest margin, at 2%, went unremarked.

## Category verdicts

- Flow coverage — **strong**
- Token completeness — **adequate**
- Component coverage — **thin**
- State coverage — **thin**
- Visual reference coverage — **expected at stage**
- Bloat & overspecification — **adequate**
- Inheritance discipline — **adequate**
- Shape fit — **strong**
- *Accessibility (extra lens)* — **not gate-ready** (resolved)
- *Isolation (extra lens)* — **no specified leak**

## Findings by severity

### Critical (8)

**[Accessibility]** — No single-pointer alternative to the Board drag at 768–1279px (§ Interaction Primitives) · **fixed**
WCAG 2.5.1 is Level A: functionality using a path-based gesture must also be operable with a single pointer without a path. The touch user got only long-press-then-drag, and the non-gesture alternative existed only below 768px — so a touch tablet in that band had no non-path route to a cross-column move, and the keyboard path is no alternative for a device with no keyboard. Inside the Board, one of NFR-9's five named flows.
Fix: an explicit **Move control** made the canonical path at every breakpoint, with all gestures reframed as accelerators. FR-29 already defines Status-change-as-move, so only the affordance was missing. Also pre-satisfies WCAG 2.2's 2.5.7.

**[Accessibility]** — The keyboard grammar is unreachable in screen-reader browse mode (§ Interaction Primitives) · **fixed**
NVDA and JAWS consume arrow keys for the virtual cursor in their default mode, so the application never receives them. A screen-reader user could not pick up a Task, move it, or cancel — and neither spine mentioned the mode exists. NFR-9's keyboard-parity clause was met for a sighted keyboard user and not for the population it is written for.
Fix: the same Move control, which works in browse mode with no mode switching. `role="application"` deliberately rejected as a known trap. The arrow grammar retained as an accelerator with its application-mode requirement stated.

**[Accessibility]** — `assertive` announces the FR-34 interruption and leaves the user stranded (§ Accessibility Floor) · **fixed**
`aria-live="assertive"` interrupts an utterance; it does not move focus. The FR-34 event makes the focused element `readonly` or replaces it — and replacing a focused node resets focus to `document.body`. The user hears "Access ended", then is nowhere, in a surface whose write affordances have all just become absent, with the retained text having no place in the reading order.
Fix: banner made focusable (`role="alert"`, `tabindex="-1"`), focus moves to it, it persists until dismissed, retained text is the next stop in the reading order.

**[Accessibility]** — A recycled virtualised row can put focus on the wrong Task, and `Space` then moves the wrong Task (§ State Patterns) · **fixed**
"Every rendered row keeps its keyboard stop" is a tautology addressing the wrong risk. The real risk is the focused row leaving the virtual window: its node is destroyed (focus falls to `body`) or — the common implementation — recycled for a different Task. The arrow grammar makes this near-certain, since `↓` crosses the window boundary every screenful. A data-corruption path reachable only by keyboard.
Fix: keyboard navigation drives the virtualiser; row identity keyed to the Task id; focus restored by Task id after any window change.

**[Accessibility]** — No focus destination for any remote event (§ Accessibility Floor) · **fixed**
Four of six dynamic situations had no answer, two of which could lose unsaved text. One documented rule was silently invalid — "closing returns focus to the originating Task card" cannot apply when the Task has just been deleted.
Fix: a focus-destination table covering seven remote and dynamic events.

**[Accessibility]** — Nobody owns FR-28 × NFR-5 × NFR-9 (all three spines) · **escalated**
The PRD defers the mechanism to architecture and says the three requirements "cannot all hold naively". The architecture's 28 ADs never decide it — no AD covers Board rendering, paging or virtualisation. EXPERIENCE.md deferred back. A three-way deferral on a release-gated requirement.
Fix: escalated to `bmad-architecture`. The UX obligation is discharged — the accessibility contract any mechanism must satisfy is now stated (keyboard drives the window, `aria-setsize`/`aria-posinset` carry the true total, focus restores by id). Whoever picks the mechanism must decide focus and announcement behaviour in the same pass.

**[Isolation]** — The local replica's lifetime is never specified, so Space content survives every boundary event (§ Foundation) · **fixed**
The default behaviour of every implementation of AD-11, not an exotic mistake. Switch to Space B while a live replica of A remains resident; Membership in A is removed and the lease invalidated with nothing telling the client to drop it; switch back and the cached Board renders optimistically before the 404 lands. AD-7 forbids only credentials in `localStorage`, so A's Task text can survive sign-out on a shared machine. **SM-1's suite runs request-level cases, so a purely client-side render would not fail it** — which makes this the most likely route to a verified disclosure in a shipped build.
Fix: a named, testable lifecycle rule with an explicit trigger set, a ban on Space content in any persistent client store, and a rule that optimistic rendering never precedes authorisation on first read.

**[Isolation]** — The two 404 cases cannot have equal latency as implemented (§ Isolation and Refusal) · **escalated**
The spine asserted "same latency". AD-20 requires an `AccessRefusal` row for every Space-boundary 404 and not for an ordinary not-found, so the boundary case performs an INSERT the other does not — measurable, repeatable, and available to anyone holding one Membership. AD-3, unlike AD-23, never makes duration contractual, so nothing upstream catches it, and asserting the property in a UX document reads as satisfied-by-construction.
Fix: escalated to `bmad-architecture`; claim softened to "every respect the interface controls" with the gap named. Options: write the refusal record off the response path, write an equivalent record for the ordinary case, or pad boundary refusals to a floor. SM-1 needs a timing case.

### High (38)

Fifteen carried the bulk of the work. All resolved.

**[Rubric]** — Eight of twelve contrast figures wrong in a table presented as verified (DESIGN.md § Colors) · **fixed**
Nine understated, three overstated; no false passes. Worse than the arithmetic: the "little headroom — do not darken" warning sat on `accent` at 4.96 (10% clear) while the genuine thinnest margin — the light border at 3.06 (2% clear) — was described as "clear".
Fix: everything recomputed from hex with the formula stated, both themes tabulated, light border raised to `#6B7794`, warning moved to the token that has no margin.

**[Rubric / Accessibility]** — Accent text links fail WCAG 1.4.1 (DESIGN.md § Components) · **fixed**
`accent` against `text-primary` is 2.66:1 dark, 2.55:1 light — below the 3:1 required when colour alone distinguishes a link from body text, with no non-colour cue specified. The accent passes against the *background* and fails against the *text beside it*, which is the pair that matters.
Fix: underline mandatory on inline links; new `text-link` component.

**[Accessibility]** — Every border pair drops below 3:1 under partial pixel coverage, and one component guarantees it (DESIGN.md § Colors) · **fixed**
A 1px border antialiases at the 1.25×/1.5×/1.75× display scales a 3px grid produces; at 80% coverage every pair fails. `task-card-lifted`'s `rotate(-1deg)` antialiases unconditionally, so the object whose boundary matters most was guaranteed to fail.
Fix: 1.5px structural hairline with device-pixel snapping; 2px emphasis border on the lifted card.

**[Accessibility]** — The Label constraint was stated against one theme while the same colour renders in both (DESIGN.md § Components) · **fixed**
The two card grounds are 17 stops apart, so a fill tuned to near-black routinely fails against white — a class of user-generatable 1.4.11 failures. The ΔE rule also named no colour space and omitted `accent` and `presence`.
Fix: 3:1 against both grounds simultaneously; ΔE2000 named; exclusion set extended.

**[Rubric]** — The light theme had hex values but no component-level resolution mechanism (DESIGN.md frontmatter) · **fixed**
Every component referenced dark tokens; 11 of 14 `-light` tokens were referenced nowhere. A consumer got a dark-only system whose light counterpart was a naming convention.
Fix: the suffix pair stated as a theme-selection mechanism resolved once at the theme boundary.

**[Rubric]** — Four duplicate Component Patterns rows with no precedence rule (EXPERIENCE.md) · **fixed**
`Task detail` and `Description editor` each appeared twice with non-identical content. An artifact of the author's own Pass 1 adding rows without deduplicating.
Fix: merged to the union; pickers and bulk-move rows consolidated.

**[Rubric]** — No affordance for FR-12, FR-13 or FR-14 (EXPERIENCE.md § Component Patterns) · **fixed**
FR-14 is the causal trigger of UJ-6, which the PRD flags as the acceptance criterion the product should be judged on — and UJ-6 said "Ravi removes her Membership" with no surface behind it.
Fix: Membership list, Invitation list, Ownership panel and Status delta editor rows added, with FR-13's Role narrowing stated.

**[Rubric]** — Revocation-while-disconnected unhandled, and two rows contradicted each other in committed copy (EXPERIENCE.md § State Patterns) · **fixed**
"Your changes are held locally" promises application; access-ending requires the text not be applied; and the mechanism is a push that cannot reach a disconnected client. The product would have to retract a promise it made in writing — on the FR-33/FR-34 seam PRD §4.9 calls the requirement most likely to be got wrong.
Fix: a dedicated state resolving to "Access ended." rather than a reconciliation failure, plus copy softened to "not yet sent".

**[Rubric]** — A whole surface disappearing under someone was uncovered (EXPERIENCE.md § State Patterns) · **fixed**
Only the Task editor had an interruption state, though Foundation states the principle and AD-9 pushes the change to every surface.
Fix: a "Role drops below a surface's requirement" state, and absence declared a steady state rather than a transition.

**[Rubric]** — Membership removal absent from the destructive blast-radius ladder (EXPERIENCE.md) · **fixed**
Fix: inserted between Task and Project; the confirm now states whether the target has a live session.

**[Rubric]** — Session expiry had no state anywhere (EXPERIENCE.md) · **fixed**
`Session` appeared once, in the vocabulary list. A guaranteed daily occurrence with nothing specified — including whether the local replica survives, which AD-11 makes a real question.
Fix: state added covering the landing surface, the client purge, and a ban on silently submitting retained text after re-authentication.

**[Isolation]** — `409, never 404` stated twice without AD-26's Membership qualifier (EXPERIENCE.md) · **fixed**
AD-26 permits 409 *because* the caller holds a Membership. Dropping the qualifier turns the offer endpoint into a cross-Space existence oracle.
Fix: both statements qualified, with the check order made explicit.

**[Isolation]** — Multi-tab never considered, and FR-34 pushes the implementer into it (EXPERIENCE.md) · **fixed**
Fanning the notice to every tab requires disambiguating which Space ended, so the announcement becomes "Access to Northwind Redesign ended." — a Space name crossing a boundary through an assertive live region, in a tab showing a different Space.
Fix: delivery restricted to the matching client context; cross-tab fan-out is a per-tab filter; the copy carries no Space name in any case.

**[Isolation]** — Inbound frames queued before lease invalidation had no specified disposal (EXPERIENCE.md) · **fixed**
The outbound direction was thorough; nothing covered frames already delivered and sitting in a queue when the lease dies, so Presence announces "Ravi is editing" one tick after "Access ended."
Fix: the client discards queued inbound frames and clears both live regions **before** the banner is announced.

**[Isolation]** — The §6.1 email-address rule appeared nowhere in either spine (EXPERIENCE.md) · **fixed**
Initials and display names do not disambiguate two people, and the field every implementer reaches for is the address.
Fix: a hard rule confining addresses to the Membership list, a Membership-scoped discriminator for collisions, and attribution by captured name rather than live global lookup.

*Remaining high findings, all fixed:* browser-owned surfaces (autofill, `document.title`, invitation metadata, scroll restoration); three Account-scoped reads richer than AD-24 (two permitted with amendments flagged, the Token→Role display dropped); the Ownership Offer email's missing disclosure clause; `Space` rebound without scope; three unspecified keyboard behaviours (position preservation, drop destination, announcement content); the long-press slop radius and single-axis rule; Presence announcement storms; the virtualised `aria-setsize` mismatch; `inert` making retained text unreachable; absence-as-transition; no prose statement of Role limits; WCAG 1.4.12 unmentioned; RTL arrow keys contradicting the mirroring mandate; the Presence count being the smallest text in the product; absolute px type; the unspecified Status pager; the 44px floor contradiction; the bulk move's missing focus story; component name drift; and the Task card metadata overflow.

### Medium (34)

All fixed except one, which is open by decision.

Notable: the danger token used against its own Do's-and-Don'ts rule (resolved with a distinct `revoked-edge` token); the focus-ring rationale being arithmetically hollow at 1.45:1 (resolved by naming the `outline-offset` as the real protection); accent and danger at 1.19:1 while the border was declared the sole destructiveness signal (resolved by reassigning the signal to copy); six components with no foreground token; off-scale pixel literals bypassing a token defined as that exact value; the 403 being named but not designed while the Voice table banned the only string that fits it; no state for an Invitation opened while signed in as a different Account, or for an unrecognised token; cold load rendering the Space name before authorisation; a boundary 404 on an optimistic write having no state; the client being free to request an unlisted Space id; Status Configuration lacking in-flight and refused states while the smaller bulk move had both; the List View lacking a scale state FR-30 explicitly requires; offline designed only inside the Task editor; `Esc` carrying three colliding meanings; a delete confirm requiring a modal two levels deep; Board mutation announcements being undecided; skeletons lacking `aria-busy`; Space switching lacking a focus landing; uppercase not stated as presentational; the per-script fallback missing the lossy cased scripts; and the `User` glossary term downcased nine times.

**Open by decision:** Internationalisation is roughly half duplication and the rubric recommended folding it. Kept standalone deliberately — i18n was adopted with no upstream requirement behind it, so nothing else in the project will remind anyone it exists, and obligations scattered across three sections are easier to quietly drop than a section someone must decide to delete. The duplication was trimmed to cross-references.

### Low (31)

All fixed except two, which are open by decision.

Notable fixes: the Role chip fill invisible against its own ground; two UJ titles not verbatim while claiming to be; the UJ-4 climax quotation dropping the repeated word that is the rhetorical point of the original; the banned synonym "workspace" used once in a sentence about Yello's own Role legibility; a PRD/AD conflict resolved silently while a comparable one was recorded at length; FR-6 having no home at all; the border rule stated three times; ~500 words duplicating the memlog; a three-sentence frontmatter `description`; Motion leaking behavioural rules EXPERIENCE.md already owns; five components with no Component Patterns row; Presence announcement content unspecified; and FR-32's cross-Space clause not restated.

**Open by decision:**
- The `.working/` exploratory artifacts still render the superseded border values. Deliberately not retro-edited — they are the record of *how* the direction was chosen. The four `mockups/` files carry the corrected tokens and are the linked reference.
- Browser spellcheck and cloud IME egress of description text. Disabling spellcheck on a prose field is the wrong usability trade, so the egress is recorded as accepted and flagged against §6.4's data-protection gate.
- The density premise not surviving 200% text zoom. Not a failure, so nothing changed — but recorded in DESIGN.md as an accepted cost so the FR-28 argument is not read as holding at every zoom level.

## Mechanical notes

- **Race between the reviewers and the author's own fix pass.** The lenses read the spines while a first fix pass was in flight, so several findings — duplicate rows, Session expiry, removed-while-disconnected, the contrast recompute, the role-chip border, the text-link underline, the light-theme resolution mechanism, and the Membership and Invitation lists — were already closed when the reports landed. Verified rather than re-fixed.
- **Contrast arithmetic.** Both reviewers recomputed independently and agreed to two decimal places. The memlog's pre-fix figure of "1.7:1" conflated two grounds — it was 1.58 on the card and 1.71 on the column.
- **Cross-references.** No broken references in either direction. All token references resolve, all section targets exist, all FR/NFR/AD/§ citations resolve, all three `sources` paths exist on disk.
- **Uncited requirements.** FR-6, FR-12, FR-13, FR-14, FR-18, FR-19 and FR-30 appeared in neither spine. FR-18 and FR-19 were covered behaviourally and needed nothing; the rest are now addressed. Eleven ADs remain uncited and all are build-side.
- **Frontmatter.** DESIGN.md now carries two product-specific token groups, `motion` and `borders`; the rubric judged the `motion` extension legitimate on reasoning that applies equally to `borders`.

## Reviewer files

- `review-rubric.md`
- `review-accessibility.md`
- `review-isolation.md`
