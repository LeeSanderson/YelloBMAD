---
name: Yello
description: Visual identity for Yello — a task tracker built on one primitive, the Space.
status: final
updated: 2026-08-20
sources:
  - _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md
  - _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md
colors:
  # DARK IS CANONICAL. The unsuffixed token is the dark value; `-light` is the
  # derived adaptation, resolved once at the theme boundary. See Colors.
  surface-page: '#0B1020'
  surface-column: '#121A31'
  surface-card: '#18213C'
  border-hairline: '#6478A8'
  text-primary: '#E6EAF5'
  text-muted: '#9AA6C4'
  accent: '#8B80F9'
  accent-on: '#14103A'
  focus-ring: '#ADA4FB'
  presence: '#38BDF8'
  danger: '#FB7185'
  danger-on: '#3B0A15'
  revoked-edge: '#FB7185'
  role-chip: '#241F52'
  role-chip-on: '#C3BCFC'
  surface-page-light: '#F4F6FB'
  surface-column-light: '#EAEEF7'
  surface-card-light: '#FFFFFF'
  border-hairline-light: '#6B7794'
  text-primary-light: '#131A2B'
  text-muted-light: '#4B5670'
  accent-light: '#5145CD'
  accent-on-light: '#FFFFFF'
  focus-ring-light: '#6E62E6'
  presence-light: '#0E7490'
  danger-light: '#BE123C'
  danger-on-light: '#FFFFFF'
  revoked-edge-light: '#BE123C'
  role-chip-light: '#E2E0FA'
  role-chip-on-light: '#332B96'
typography:
  # Sizes in rem against a 16px root so a user font-size preference is honoured
  # (WCAG 1.4.4). Line-heights are >= 1.5 so a 1.4.12 text-spacing override has
  # room to grow. px never sizes or spaces type; it carries the structural
  # lengths below (spacing, target floor, hairlines, radii, offsets, slop).
  task-title:
    fontFamily: system-sans
    fontSize: 0.8125rem
    fontWeight: '600'
    lineHeight: '1.5'
    letterSpacing: -0.003em
  column-head:
    fontFamily: system-sans
    fontSize: 0.6875rem
    fontWeight: '700'
    lineHeight: '1.5'
    letterSpacing: 0.12em
  space-name:
    fontFamily: system-sans
    fontSize: 0.875rem
    fontWeight: '700'
    lineHeight: '1.5'
    letterSpacing: -0.005em
  body:
    fontFamily: system-sans
    fontSize: 0.875rem
    fontWeight: '400'
    lineHeight: '1.6'
  dialog-title:
    fontFamily: system-sans
    fontSize: 1rem
    fontWeight: '650'
    lineHeight: '1.5'
    letterSpacing: -0.008em
  meta:
    fontFamily: system-mono
    fontSize: 0.6875rem
    fontWeight: '500'
    lineHeight: '1.5'
  role-label:
    fontFamily: system-mono
    fontSize: 0.6875rem
    fontWeight: '700'
    lineHeight: '1.5'
    letterSpacing: 0.07em
  presence-count:
    fontFamily: system-sans
    fontSize: 0.6875rem
    fontWeight: '600'
    lineHeight: '1.5'
rounded:
  sm: 2px
  DEFAULT: 3px
  md: 3px
  lg: 6px
  full: 9999px
spacing:
  unit: 3px
  '1': 3px
  '2': 6px
  '3': 9px
  '4': 12px
  '5': 18px
  '6': 24px
  '7': 36px
  gutter: '{spacing.3}'
  card-stack-gap: '{spacing.2}'
  card-pad-y: '0.5625rem'
  card-pad-x: '0.625rem'
  control-pad-y: '0.5rem'
  control-pad-x: '0.8125rem'
  target-min: 24px
borders:
  hairline-width: 1.5px
  emphasis-width: 2px
motion:
  instant: 90ms
  quick: 120ms
  lift: 110ms
  settle: 120ms
  easing-standard: 'cubic-bezier(0.2, 0, 0.3, 1)'
  easing-exit: 'cubic-bezier(0.4, 0, 1, 1)'
  long-press-threshold: 320ms
  long-press-slop: 10px
components:
  task-card:
    background: '{colors.surface-card}'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.md}'
    padding: '{spacing.card-pad-y} {spacing.card-pad-x}'
    title: '{typography.task-title}'
    shadow: 'none'
  task-card-lifted:
    border: '{borders.emphasis-width} solid {colors.border-hairline}'
    shadow: '0 6px 0 rgba(0,0,0,.6)'
    transform: 'rotate(-1deg)'
  column:
    background: '{colors.surface-column}'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.md}'
    padding: '{spacing.3}'
    head: '{typography.column-head}'
  column-count:
    background: '{colors.surface-card}'
    foreground: '{colors.text-muted}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.full}'
    typography: '{typography.meta}'
  context-bar:
    background: '{colors.surface-card}'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.md}'
    padding: '{spacing.2} {spacing.card-pad-x}'
    name: '{typography.space-name}'
  role-chip:
    background: '{colors.role-chip}'
    foreground: '{colors.role-chip-on}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.sm}'
    typography: '{typography.role-label}'
  offer-indicator:
    background: '{colors.accent}'
    foreground: '{colors.accent-on}'
    radius: '{rounded.sm}'
    typography: '{typography.role-label}'
  label-chip:
    foreground: '{colors.text-primary}'
    radius: '{rounded.sm}'
    typography: '{typography.meta}'
  presence-indicator:
    foreground: '{colors.presence}'
    typography: '{typography.presence-count}'
    dot-size: '{spacing.2}'
  avatar:
    background: '#374363'
    foreground: '{colors.text-primary}'
    radius: '{rounded.DEFAULT}'
    size: '1.25rem'
    typography: '{typography.meta}'
  focus-ring:
    outline: '{borders.emphasis-width} solid {colors.focus-ring}'
    outline-offset: '2px'
  drop-zone:
    border: '{borders.emphasis-width} dashed {colors.focus-ring}'
    foreground: '{colors.focus-ring}'
    radius: '{rounded.md}'
  text-link:
    foreground: '{colors.accent}'
    text-decoration: 'underline'
  button-primary:
    background: '{colors.accent}'
    foreground: '{colors.accent-on}'
    radius: '{rounded.sm}'
    padding: '{spacing.control-pad-y} {spacing.control-pad-x}'
    min-height: '{spacing.target-min}'
  button-danger:
    background: '{colors.danger}'
    foreground: '{colors.danger-on}'
    radius: '{rounded.sm}'
    padding: '{spacing.control-pad-y} {spacing.control-pad-x}'
    min-height: '{spacing.target-min}'
  button-secondary:
    background: 'transparent'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.sm}'
    padding: '{spacing.control-pad-y} {spacing.control-pad-x}'
    min-height: '{spacing.target-min}'
  dialog:
    background: '{colors.surface-card}'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.lg}'
    padding: '{spacing.5}'
    title: '{typography.dialog-title}'
  space-switcher:
    background: '{colors.surface-card}'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.md}'
    row-padding: '{spacing.2} {spacing.card-pad-x}'
    row-min-height: '{spacing.target-min}'
    row-typography: '{typography.space-name}'
  task-detail:
    background: '{colors.surface-card}'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.lg}'
    padding: '{spacing.5}'
    title: '{typography.dialog-title}'
  description-editor:
    background: '{colors.surface-page}'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.md}'
    padding: '{spacing.3}'
    typography: '{typography.body}'
  description-editor-readonly:
    background: '{colors.surface-page}'
    foreground: '{colors.text-muted}'
    border: '{borders.hairline-width} solid {colors.revoked-edge}'
  picker:
    background: '{colors.surface-card}'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.md}'
    row-min-height: '{spacing.target-min}'
    row-typography: '{typography.body}'
  bulk-move-bar:
    background: '{colors.surface-card}'
    foreground: '{colors.text-primary}'
    border: '{borders.emphasis-width} solid {colors.accent}'
    radius: '{rounded.md}'
    padding: '{spacing.3}'
    typography: '{typography.meta}'
  invitation-view:
    background: '{colors.surface-card}'
    foreground: '{colors.text-primary}'
    border: '{borders.hairline-width} solid {colors.border-hairline}'
    radius: '{rounded.lg}'
    padding: '{spacing.6}'
    title: '{typography.dialog-title}'
  destructive-confirm:
    background: '{colors.surface-card}'
    foreground: '{colors.text-primary}'
    border: '{borders.emphasis-width} solid {colors.danger}'
    radius: '{rounded.lg}'
    padding: '{spacing.5}'
    title: '{typography.dialog-title}'
    action: '{components.button-danger}'
---

# DESIGN.md — Yello

> Visual identity. Behaviour, information architecture, states and flows live in `EXPERIENCE.md`, which references these tokens by name. Both spines win on conflict with any mock, wireframe or import.

## Brand & Style

Yello's premise is that a personal to-do list, a client engagement and a whole company's shared work are the same object — a **Space**, which is both the container for work and the boundary of who can see it. The interesting user is not the one with one Space but the one who lives in several at once *with a different standing in each*: Owner here, Admin there, Viewer somewhere they used to work. Identity is global, permission is contextual.

The visual language is **engineered rather than decorated**. Structure is *drawn* — borders on a tinted ground — never floated on shadow. Metadata is set in monospace, so counts, Labels and above all the acting Role read as system facts rather than badges someone styled. Corners are 3px. There is no gradient, no illustration, no celebratory motion, and nothing on any surface exists to be admired.

That restraint is a product position, not a mood. §10's SM-C2 makes time-in-application a **counter-metric** — *"a task tool people spend longer inside is working worse, not harder"* — and UJ-1's success condition is Ravi closing the tab ninety seconds after arriving. A surface with nothing to linger over is a surface you leave.

Yello is **dark-first**: the dark palette is where the design was drawn, and light is its adaptation.

**Known cost, recorded rather than hidden.** This register suits Ravi, a freelance developer, and Tomás, who drives Yello from a deploy script. It suits Beatriz least — the paying client, only ever a Viewer, who never asked for a developer tool. Chosen deliberately across three decisions (visual register, voice, refusal copy); if her experience becomes a problem, treat those three as one decision. Full rationale is in `.memlog.md`.

Yello inherits **no UI system** — no shadcn, MUI or internal library — so every token here carries a literal value rather than a delta. Blazor WebAssembly renders it.

## Colors

**The unsuffixed token is the *dark* value.** `-light` is the derived adaptation. This inverts the usual convention deliberately: Dusk was drawn dark, and an implementation that builds light first and inverts it gets a different product, because the tinted grounds and border weights were chosen against a dark canvas. Both themes are peers at runtime; only the *authoring* order is fixed.

**How the two themes resolve.** Every `{components.*}` entry references the **unsuffixed** token, and none references a `-light` one. That is the mechanism, not an oversight: each semantic name resolves to its unsuffixed value under the dark theme and to its `-light` sibling under the light theme, once, at the theme boundary — and every component consumes the semantic name only. A component reaching for a `-light` token directly is a defect, because it pins that component to one theme.

Three grounds, and they are the whole layout:

- **`{colors.surface-page}`** — the canvas. Deep blue-violet rather than neutral grey.
- **`{colors.surface-column}`** — a Status column, one step up from the page.
- **`{colors.surface-card}`** — a Task, one step up again; also the ground for the context bar and dialogs.

The meaningful colours, each with one job:

- **`{colors.accent}`** — this acts. Primary buttons, text links, the pending Ownership Offer indicator, and the bulk-move bar. Never decorative.
- **`{colors.focus-ring}`** — a separate token from accent. See *What actually protects the focus ring* below, because the usual justification for the separation is wrong.
- **`{colors.presence}`** — FR-32, who else is on this Task. Always rendered *with* its text count; never colour alone (NFR-9).
- **`{colors.danger}`** — destructive only. Deletion is immediate and irreversible with no trash (§6.2).
- **`{colors.revoked-edge}`** — the FR-34 revoked-editor edge. **A separate token from `{colors.danger}` on purpose**, even though they may resolve to the same value: Do's and Don'ts reserves danger for the genuinely irreversible and forbids it on ordinary refusals, and losing access *is* a refusal. Without a distinct name, the component and the rule contradict each other and an implementer has to guess which to follow.
- **`{colors.role-chip}` / `{colors.role-chip-on}`** — the acting Role. UJ-4 requires it legible at all times, so this is permanent chrome.

**Not used for anything:** hue to encode Status. Statuses are user-defined per Space with a per-Project delta (FR-24, FR-25), so any fixed Status→colour mapping is wrong the moment someone renames a column.

### Verified contrast

NFR-9 makes WCAG 2.1 AA a hard release gate at consumer stakes. Every figure below is **computed** from the hex values by the WCAG 2.x formula — sRGB linearisation at the 0.03928 threshold, `(L₁+0.05)/(L₂+0.05)` — not estimated. Both themes are stated in full, because a light theme with hex values and no verification is not a peer.

| Combination | Dark | Light | Needs | |
|---|---|---|---|---|
| `text-primary` on card | 13.21 | 17.35 | 4.5 | AAA both |
| `text-primary` on page *(editor)* | 15.73 | 16.05 | 4.5 | pass |
| `text-muted` on card | 6.53 | 7.33 | 4.5 | pass |
| `text-muted` on page *(readonly editor)* | 7.78 | 6.78 | 4.5 | pass |
| `presence` on card | 7.42 | 5.36 | 4.5 | pass |
| `presence` on column | 8.05 | **4.61** | 4.5 | thinnest text pair in light |
| `danger` on card | 5.90 | 6.29 | 4.5 | pass |
| `danger-on` on `danger` | 6.29 | 6.29 | 4.5 | pass |
| `accent` on card | 4.96 | 6.81 | 4.5 | pass, 10% clear |
| `accent` on column | 5.38 | 5.86 | 4.5 | pass |
| `accent-on` on `accent` | 5.63 | 6.81 | 4.5 | pass |
| `role-chip-on` on `role-chip` | 8.56 | 8.45 | 4.5 | pass |
| `focus-ring` on card | 7.17 | 4.61 | 3.0 | pass |
| `focus-ring` on column | 7.78 | 3.97 | 3.0 | pass |
| `border-hairline` on card | 3.63 | 4.48 | 3.0 | pass |
| `border-hairline` on column | 3.94 | 3.85 | 3.0 | pass |
| `border-hairline` on page | 4.32 | 4.14 | 3.0 | pass |
| `border-hairline` on `role-chip` | 3.44 | — | 3.0 | makes the chip border work |
| `surface-card` on `surface-column` | *1.09* | *1.16* | — | see below |
| `surface-column` on `surface-page` | *1.10* | *1.07* | — | see below |

The last two rows are stated for information, not as targets. At **1.09** and **1.10** the tonal steps are effectively invisible as boundaries, which is exactly why the border carries component identity alone. Note the light ladder is *flatter* than the dark one (1.07), so the light border matters more, not less.

**Two combinations that are load-bearing and must not be mistaken for contrast pairs:**

- **`{colors.accent}` against `{colors.text-primary}` is 2.66 dark / 2.55 light.** Below the 3:1 WCAG 1.4.1 requires when colour alone distinguishes a link from body text. This is why `{components.text-link}` is **underlined**, always. The accent passes handsomely against the *background* and fails against the *text beside it* — and for a link, the text beside it is the pair that matters.
- **`{colors.accent}` against `{colors.danger}` is 1.19 dark / 1.08 light.** They differ by hue alone, so under deuteranopia the accent-bordered bulk-move bar and the danger-bordered destructive confirm converge. **Colour is therefore never the signal for destructiveness — the copy is.** See Components.

### What actually protects the focus ring

The intuitive rationale — *"focus-ring is separate from accent so it doesn't vanish against accented controls"* — does not survive arithmetic: `{colors.focus-ring}` against `{colors.accent}` is **1.45** dark and **1.48** light. It *would* vanish.

What actually protects it is `outline-offset: 2px`, which puts the ring on the ground *behind* the control, where it reads at 7.17 on the card. So the real rule is about geometry:

> **Never reduce `{components.focus-ring}`'s `outline-offset` to 0, and never draw the ring inset.** Compliance depends on the offset, not on the token separation. Both tokens are kept — the separation is still right, because it lets the ring be tuned independently — but it is not what makes the ring visible.

### Border rendering

`{borders.hairline-width}` is **1.5px**, not 1px, and this is an accessibility requirement rather than a stylistic one.

A 1px border is antialiased whenever its edge lands off a device-pixel boundary — which a 3px spacing grid does at the 1.25×, 1.5× and 1.75× display scales common on Windows and Android. Composited at 80% coverage, every border pair in both themes drops below the 3:1 gate. Worse, `{components.task-card-lifted}` applies `rotate(-1deg)`, which antialiases its border along its entire length **unconditionally** — so the one object whose boundary matters most, the Task in your hand, was the one guaranteed to fail.

Hence: **1.5px minimum on every structural border, borders snapped to device pixels where the platform allows, and `{borders.emphasis-width}` 2px on the lifted card for the duration of a drag.**

## Typography

Two families, both **system stacks**:

- **`system-sans`** — `ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`
- **`system-mono`** — `ui-monospace, SFMono-Regular, Menlo, Consolas, monospace`

`[ASSUMPTION: no webfont. Nothing upstream named a typeface. System stacks cost nothing against the £30/month ceiling (§6.3), add no render-blocking request to an already-large Blazor WebAssembly payload, and cover far more scripts than any single webfont — which matters because internationalisation is in scope.]`

**Sizes are in `rem` against a 16px root, and every line-height is ≥ 1.5.** Both are gate requirements rather than preferences. Absolute px type ignores a user's browser font-size preference entirely, which is the most common low-vision accommodation and what WCAG 1.4.4 is really about; and a line-height of 1 leaves a line box exactly the glyph height, which cannot absorb the 1.5× override WCAG 1.4.12 lets a user apply. `px` never sizes or spaces **type**, in any file. It survives on the structural lengths that should *not* scale with text — the spacing scale, the target floor, hairlines, radii, outline offsets and the long-press slop — and those are stated in the token layer, so every other file takes its lengths from a token. *(Clarified 2026-08-27 at story 1.2's code review: the original sentence, "`px` survives only on hairlines, radii and outline offsets", contradicted the token values fixed at :99-113 of this document.)*

**Task titles are the only content on a card**; everything else is metadata. `{typography.task-title}` at 0.8125rem/600 is heavier than a conventional body weight so a title holds against the mono metadata below it without being larger.

**All metadata is monospace** — `{typography.meta}` for counts, Labels and avatar initials, `{typography.role-label}` for the Role chip. This is the signature of the design: a Role reads as a fact the system is reporting rather than a label someone styled.

**One deliberate exception.** `{typography.presence-count}` — the "2 editing" string — is set in `system-sans`, not mono. It is prose, not a system fact, so the monospace rationale does not apply; and because NFR-9 makes that string the mandated non-colour carrier of Presence, it must not inherit mono's unpredictable non-Latin fallback or its smaller effective x-height. The smallest text in the product should not be the text a release gate rests on.

### Uppercase is presentational

`{typography.column-head}` and `{typography.role-label}` render uppercase. **That casing comes from `text-transform`, never from the string.** Copy resources hold sentence case — "Owner", "In Progress" — for two reasons:

1. **The acting Role is in the accessible name of the context bar.** If a developer or translator writes `VIEWER` into the resource, that accessible name becomes "V-I-E-W-E-R" spelled out by JAWS and VoiceOver — degrading the single most important accessibility affordance in the product. CSS `text-transform` leaves the DOM text alone, so AT reads the original casing and nothing is spelled out.
2. It keeps the externalised-copy requirement honest.

### Two i18n consequences

Both are real costs of this direction, and neither is obvious:

1. **Uppercase and tracking are Latin-only, and locale-sensitive even there.** Case does not exist in Arabic, Hebrew, CJK, Thai or the Indic scripts, and letter-spacing severs the joins of connected scripts. For those scripts, column heads fall back to `{typography.task-title}` weight at `{typography.column-head}` size with **zero** letter-spacing. Separately, `text-transform: uppercase` is *lossy* in several cased scripts — Turkish dotless ı → I changes the word, German ß → SS changes length, Greek strips accents and alters final sigma. Apply the treatment under a locale-aware `lang` attribute and **exclude Turkish, Azeri and Greek** alongside the case-less scripts.
2. **Monospace coverage outside Latin is poor.** `system-mono` falls back unpredictably for CJK and Indic text, and the fallback is often not monospaced at all — so any layout assuming character-cell alignment from `{typography.meta}` breaks. Metadata must never be aligned by character count.

Text expansion: German and Finnish run 30–40% longer than English. No label may be sized to its English string.

## Layout & Spacing

Base unit **3px**, giving 3 / 6 / 9 / 12 / 18 / 24 / 36 — finer than a conventional 4- or 8-based scale, because this is a dense design and 4px steps are too coarse at this card size. **Component-internal padding is expressed in `rem` rather than on the scale** (`card-pad-y`, `card-pad-x`, `control-pad-y`, `control-pad-x`), deliberately, so padding grows with text under a zoom or font-size override instead of clipping it.

- `{spacing.gutter}` between Status columns
- `{spacing.card-stack-gap}` between Tasks in a column
- `{spacing.card-pad-y}` / `{spacing.card-pad-x}` inside a Task

**Interactive target floor is `{spacing.target-min}` 24px**, applied as minimum height on every interactive component. Stated precisely because it is easy to get wrong in both directions: WCAG 2.1 AA — the gate NFR-9 names — has **no** target-size criterion at all (2.5.5's 44×44 is AAA); WCAG 2.2 AA's 2.5.8 sets 24×24. So 24px is the real, current AA floor and the one this design commits to. The Task card clears 49px on its own arithmetic anyway, which is what makes *"the long-press lift target is the whole Task card"* a sound decision.

**The density position, and why it is a requirement rather than taste.** FR-28 requires the Board usable at NFR-8's bound of **5,000 Tasks in a Project** while meeting NFR-5's latency budget and NFR-9's accessibility floor — and the PRD states plainly the three cannot all hold naively. Density is the cheapest lever: every Task on screen is one fewer row to virtualise and one fewer keyboard stop to manage. This design fits roughly twice the Tasks of a comfortable one.

> **Accepted cost, recorded so the argument is not over-read.** At 200% text-only zoom a six-word title wraps from one or two lines to four or five, and the density advantage — and with it this FR-28 mitigation — largely evaporates. The virtualisation load returns in full at exactly the accessibility setting the gate exercises. The density argument holds at 100%, not at every zoom level.

`[ASSUMPTION: no user-facing comfortable/compact control in v1. One density, specified here. A control would double the state matrix for every Board mock and every keyboard-traversal test while the product has no evidence anyone wants it. Noting the fuller cost: a density toggle is also a common low-vision and cognitive-load accommodation, and this is the densest plausible default — so the assumption trades away the cheapest accessibility affordance available. Revisit if reported.]`

Board columns scroll **within the column**, never the page. The context bar is always present and never scrolls away — it is the mechanism by which authorisation context is established (§7).

## Elevation & Depth

**There is no elevation.** Shadow is not a hierarchy device, and `{components.task-card}` sets `shadow: none` explicitly so nobody adds one back.

This follows from the palette rather than from preference. Dusk's grounds are tinted and close in luminance, and a shadow on a tinted ground reads as a smudge rather than a lift. Structure comes from `{colors.border-hairline}` and the three-step tonal ladder, both of which hold at any density.

**One exception, and it is functional:** `{components.task-card-lifted}` — a Task under an active drag — takes a hard offset shadow, a 1° rotation, and a 2px border. Not decoration; it is the only place an object leaves the plane, which is what makes "this thing is in my hand" unmistakable. A blurred shadow would not read here.

## Shapes

`{rounded.md}` **3px** on Tasks, columns, the context bar and buttons. `{rounded.sm}` **2px** on the Role chip, Label chips and the Offer indicator. `{rounded.lg}` **6px** on dialogs, the Task detail panel and the invitation view — the only radius that soft, marking those surfaces as *out of plane*.

Corners this tight read as *engineered* rather than friendly. Avatars are **squared to `{rounded.DEFAULT}` 3px rather than circular** — a deliberate break from convention, and part of why the surface reads as a tool.

`{rounded.full}` exists for exactly one component: `{components.column-count}`. It is the only pill in the product.

**Chips and cards size to content, with no fixed heights**, so a 1.4.12 text-spacing override grows the box instead of clipping the glyphs.

## Components

Visual specs only. Behaviour is `EXPERIENCE.md`.

- **Task card** — `{components.task-card}`. Title in `{typography.task-title}`; a metadata row beneath carrying Label chips, the Presence indicator when others are present, and the Assignee avatar pushed right. No shadow. **The metadata row needs an overflow rule, because its content is unbounded**: Labels are user-defined per Space (FR-22) with no cap, and NFR-8 permits 10 concurrent editors, so "2 editing" becomes "10 editing". At the 320px reflow width the row has roughly 282px. Render **at most three Label chips, then a `+N` affordance**, and let the row wrap to a second line before it ever scrolls horizontally — horizontal overflow inside a card is a 1.4.10 failure in the exact layout an audit is conducted in.
- **Task card, lifted** — `{components.task-card-lifted}`. Drag only. See Elevation & Depth and Border rendering.
- **Column** — `{components.column}`. Head in `{typography.column-head}`, then `{components.column-count}`, then the create affordance pushed right. That affordance is **absent** for a Viewer, not dimmed (UJ-4, FR-16).
- **Context bar** — `{components.context-bar}`. Space name in `{typography.space-name}`, a switcher chevron, the current Project in `{typography.meta}`, and `{components.role-chip}` pushed right. Always present once authenticated.
- **Role chip** — `{components.role-chip}`. Monospace, uppercase via `text-transform`, 2px corners, **and a border** — without it the fill sits at 1.05 against the context bar and the Role reads as loose text rather than a chip. Its text is perfectly legible at 8.56, so this is not a contrast failure; it is a failure to read as a chip at all, and UJ-4 turns on the Role being unmistakable. One of Owner / Admin / Member / Viewer, verbatim from §2.
- **Offer indicator** — `{components.offer-indicator}`. A pending Ownership Offer for the acting Account in the active Space, in `{colors.accent}` — the only place accent appears in chrome, because it is the only chrome element that is a *proposition* rather than a statement. Indicator only; the decision opens in `{components.dialog}`.
- **Label chip** — `{components.label-chip}`. **Labels are user-defined per Space (FR-22), so their colours are not this document's to set** — but the *range* they may occupy is, because a user must not be able to pick a colour that defeats the interface. A Label fill must:
  - hold **3:1 against both `{colors.surface-card}` and `{colors.surface-card-light}` simultaneously** — the same user-chosen colour renders in both themes, and the two card grounds are 17 stops apart, so a fill tuned to a near-black ground routinely fails against white;
  - hold **4.5:1 against its own foreground text**;
  - sit at least **ΔE2000 10** from `{colors.focus-ring}`, `{colors.danger}`, `{colors.accent}` and `{colors.presence}`. The colour space is named because ΔE76 and ΔE2000 differ by roughly a factor of two at these chromas. Accent and presence are in the exclusion set because a Label confusable with the Offer indicator or with Presence defeats both.

  Offer a **constrained palette that satisfies this by construction**, never a free colour picker.
- **Presence indicator** — `{components.presence-indicator}`. A 6px dot plus a text count in `{typography.presence-count}`. **The text is not optional** — NFR-9 forbids conveying Presence by colour or position alone.
- **Avatar** — `{components.avatar}`. 1.25rem, 3px corners, monospace initials, non-interactive. A deleted Account renders as a tombstone (FR-3, AD-5) — same shape, `{colors.text-muted}` on `{colors.surface-column}`, **no initials**: attribution survives without the identity persisting.
- **Focus ring** — `{components.focus-ring}`. 2px solid `{colors.focus-ring}` at 2px offset. Never removed, never replaced by a colour change or border swap, and never inset. See *What actually protects the focus ring*.
- **Drop zone** — `{components.drop-zone}`. Dashed rather than filled, so the underlying order stays readable mid-drag.
- **Text link** — `{components.text-link}`. `{colors.accent}` **with an underline, always** — accent is only 2.66 against body text, so colour alone does not distinguish it. Standalone accent controls do not need it; a link inside a sentence does.
- **Buttons** — `{components.button-primary}` affirmative, `{components.button-danger}` destructive, `{components.button-secondary}` for the second option in a dialog where neither choice may be a default. No ghost or tertiary variant.
- **Dialog** — `{components.dialog}`. One level deep, never stacked.
- **Space switcher** — `{components.space-switcher}`. Rows carry a Space name and **nothing else** — no count, no Role, no badge. That austerity is required, not aesthetic: AD-24 permits this surface to return Space identity only, so there is no second piece of information available to render.
- **Task detail** — `{components.task-detail}`. Out-of-plane treatment. Attributes above, description below, Presence in the header.
- **Description editor** — `{components.description-editor}`. Sits on `{colors.surface-page}` rather than the card ground, so the writing surface reads as recessed — and, as it happens, it is the best-contrasting ground in the product at 15.73. `{components.description-editor-readonly}` is the FR-34 state: a `{colors.revoked-edge}` border with muted text, the text still legible and selectable. **The attribute is `readonly`** — see the note below.
- **Picker** — `{components.picker}`. One treatment for the Move control, Assignee, Label, Status and Role. No selected-by-default row where the choice is consequential.
- **Bulk move bar** — `{components.bulk-move-bar}`. The only component bordered in `{colors.accent}`, because it is the only transient in-flight operation and must be unmistakable while it runs.
- **Invitation view** — `{components.invitation-view}`. The one surface an unauthenticated stranger sees, with the most generous padding in the product — this is Beatriz's first and possibly only impression, and the one place the engineered register is deliberately loosened.
- **Destructive confirm** — `{components.destructive-confirm}`. `{colors.danger}` border and `{components.button-danger}` action. **The copy is the signal and the border is reinforcement** — never the reverse; see Colors for why. *"This cannot be undone."* is what carries it. No icon, no illustration.

> **`readonly`, not `inert`.** `inert` is a specified HTML attribute with a precise meaning: the content becomes non-focusable **and is removed from the accessibility tree**. Applying it to the revoked editor would make the retained text invisible to assistive technology and unreachable by keyboard — exactly defeating the reason it is retained, and leaving "selectable" true only by pointer. `disabled` is no better. A `readonly` field is focusable, selectable, copyable and present in the accessibility tree, which is what the requirement actually needs.

→ **Visual reference.** [`mockups/board.html`](mockups/board.html) shows `task-card`, `task-card-lifted`, `column`, `column-count`, `context-bar`, `role-chip`, `label-chip`, `presence-indicator`, `avatar`, `focus-ring`, `drop-zone` and `picker` together at the specified density. [`mockups/task-detail.html`](mockups/task-detail.html) shows `task-detail`, `description-editor` and the readonly state. [`mockups/ownership-offer.html`](mockups/ownership-offer.html) shows `offer-indicator`, `dialog`, `button-primary` and `text-link`. [`mockups/registration.html`](mockups/registration.html) shows the empty-state and form treatments. [`mockups/project-settings.html`](mockups/project-settings.html) shows `picker` in its most demanding use, plus the gated commit. [`mockups/space-settings.html`](mockups/space-settings.html) shows `role-chip` beside editable Role pickers and `destructive-confirm`. [`mockups/invitation.html`](mockups/invitation.html) shows `invitation-view` and its loosened padding.
>
> **This spine wins on conflict with any mock.** The four earliest mockups were rendered before the border, type-scale and `readonly` corrections above; the three settings and invitation mockups carry the corrected tokens.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Treat the unsuffixed token as the **dark** value and `-light` as the adaptation | Build light first and invert it — grounds and borders were drawn against dark |
| Separate structure with `{colors.border-hairline}` at `{borders.hairline-width}` | Add a card shadow, or thin the border to 1px — antialiasing drops it under the 3:1 gate |
| Keep `{components.focus-ring}`'s 2px `outline-offset` | Inset the ring or set the offset to 0 — the offset is what makes it visible, not the token |
| Let the **copy** carry destructiveness | Rely on the danger border — accent and danger are 1.19 apart and converge under deuteranopia |
| Render Presence as dot **plus** text count | Convey Presence, or any state, by colour or position alone (NFR-9) |
| Set metadata in `system-mono`, and the Presence count in `system-sans` | Align metadata by character count — mono fallback outside Latin is not monospaced |
| Size type in `rem` with line-height ≥ 1.5 | Use absolute px type — it ignores a user's font-size preference and clips under 1.4.12 |
| Apply uppercase with `text-transform`, locale-aware | Bake uppercase into a copy resource — the Role's accessible name gets spelled out letter by letter |
| Use `readonly` on the revoked editor | Use `inert` or `disabled` — both remove the retained text from the accessibility tree |
| Remove an affordance a Role lacks | Render it disabled — UJ-4 requires absent, not present-and-failing |
| Constrain Label colours against **both** card grounds | Ship a free colour picker for Labels |
| Cap the Task card metadata row and wrap it | Let Label chips overflow horizontally inside a card |
| Keep the context bar permanently visible | Let it scroll away, collapse it, or hide it behind a menu |
| Reserve `{colors.danger}` for the genuinely irreversible, and `{colors.revoked-edge}` for revocation | Use danger for validation errors or ordinary refusals |
| Keep `{rounded.full}` to the column count chip | Introduce pills, or circular avatars |

## Motion

*A product-specific section, appended after the canonical eight rather than reordering them. Motion is in scope by explicit decision. Behavioural rules that motion implies — gesture thresholds, reduced-motion consequences — live in `EXPERIENCE.md`; what follows is the timing contract.*

The budget is **near-instant**: `{motion.instant}` to `{motion.quick}`, with `{motion.easing-standard}` on entry and `{motion.easing-exit}` on exit. Short enough to feel like direct manipulation rather than animation, and coherent with the substrate — AD-11 has the client editing a local replica and never blocking on the network, so an interaction *can* be as fast as it looks. Motion here reports what already happened; it never covers a wait.

| Event | Token |
|---|---|
| Drag lift | `{motion.lift}` |
| Drop settle | `{motion.settle}` |
| Long-press before lift commits | `{motion.long-press-threshold}` |
| Movement tolerated before the lift cancels | `{motion.long-press-slop}` |
| Presence arrive / leave | `{motion.quick}`, opacity only — never a slide, which would shift the Task title |
| Column reflow | `{motion.quick}`, only for cards that actually moved |

**Never animated:** a Task arriving from another User's edit (it arrives, it does not fly in), a permission change taking effect, or anything on the destructive path. FR-34 revocation must land immediately; a transition there would read as negotiable.

`prefers-reduced-motion: reduce` removes every transition above. Nothing in Yello depends on motion to convey state — motion only ever reports a change also conveyed structurally — so honouring the preference costs no information.
