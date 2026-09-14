---
title: Sprint Change Proposal — FR-1's unmade amendment
status: applied
created: 2026-09-13
applied: 2026-09-14
raisedBy: 'Code review of story 1.3, 2026-09-10 — [Review][Decision], severity high'
decidedBy: 'Lee, 2026-09-13'
approvedBy: 'Lee, 2026-09-14 — all edits approved, plus optional G1 and H1'
scope: minor
triggeringStory: 1.3 — Register an Account and receive a Personal Space
artifacts:
  - _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/specs/spec-yello/harness-constraints.md
  - _bmad-output/specs/spec-yello/glossary.md
  - _bmad-output/specs/spec-yello/acceptance-criteria.md
  - _bmad-output/specs/spec-yello/SPEC.md
  - _bmad-output/specs/spec-yello/decisions-settled.md
  - docs/bmad-coverage.md
---

> **Applied 2026-09-14.** Fourteen edits across eight files. One site was found during
> verification that this proposal's own analysis had missed — **`prd.md:101`, UJ-1's narrative**
> (*"He registers with email and password…"*), recorded below as **A7**. That brings the field-count
> restatement to **five** sites, not four: the sweep that caught the review's under-count then
> under-counted by one itself, and only the post-edit grep closed it. See the residuals in §5.

# Sprint Change Proposal — FR-1's unmade amendment

## 1. Issue Summary

**Registration ships three fields; every requirement artifact says two, and the amendment that was
supposed to reconcile them was never made.**

Story 1.3 hit a genuine contradiction across five documents, recorded in its Dev Notes under *"The one
decision this story cannot make on its own"* (`1-3-…md:268-291`):

- **Three sources require a display name** — `prd.md:164`, `SPEC.md:268` and `acceptance-criteria.md:49`
  all say the Personal Space is *"named from the Account's display name (e.g. \"Ravi's Space\")"*;
  `surfaces-and-journeys.md:24` and `EXPERIENCE.md:473` print the literal string; and UX-DR34
  (`epics.md:260`) needs a display name for **every Membership rendering from Epic 4 onward**, with
  avatar initials derived from it.
- **Sources fix registration at two fields** — FR-1 (`prd.md:119`), the story's own user story
  (`epics.md:596`), `EXPERIENCE.md:472`, and a mockup with exactly two controls.
- **Nothing anywhere defined a display-name attribute on `Account`.**

On 2026-08-28 Lee decided: collect a display name, three fields, `Account.DisplayName`, Space named from
it. That shipped. Completion Note 10 recorded the deviation and stated an amendment would follow. **The
amendment was never made** — verified at code review on 2026-09-10: no planning artifact appeared in the
diff, and `prd.md:119`, `epics.md:596` and `harness-constraints.md:63` all still read *"an email address
and a password"*.

**Evidence.** `Account.DisplayName` is `nvarchar(128) NOT NULL`
(`20260828164716_InitialSchema.cs:73`), `RegisterAccountValidator` emits `display-name-required` and
`display-name-too-long` (`:184`, `:189`), and `RegistrationFields.All` is
`[DisplayName, EmailAddress, Password]` (`RegistrationFields.cs:57`). The surface has three controls;
the documents have two.

### What the impact analysis added to the review's finding

The review named three sites. There are **two distinct defects across nine sites**, and one of the three
files named was mis-read.

**Defect 1 — the field-count restatement.** Four sites, not two. The review missed
**`epics.md:60`**, the Requirements Inventory's own restatement of FR-1, which is a second independent
statement of the two-field contract inside the same `status: final` artifact.

**Defect 2 — the display name is attached to nothing.** `prd.md:36` defines **Account** as *"a
registered identity, unique by email address"* with no display name, and `glossary.md:7` mirrors it.
`acceptance-criteria.md:15-20` (CAP-1's complete testable consequence list) has no display-name clause.
So the attribute that the whole of Epic 4 renders from, and that `Account.DisplayName` makes `required`,
is defined by no spine. **PRD §2's own rule makes this a discipline violation:** *"If a new domain noun
appears in §4, it is added here in the same pass."* No review layer raised this; it is larger than the
finding that was raised.

**Correction to the file list.** `harness-constraints.md:64` is **not** a two-field rule. It is a row in
*"Assumptions that must stay soft until it fires"*, and its operative content is the **password**
clause — *"An OAuth Account has no password."* It must be amended as a restatement only, leaving the
soft-until-P6 semantics untouched, because OAuth is scheduled to fire **immediately after this epic
ships** (`harness-constraints.md:55`).

## 2. Impact Analysis

### Epic impact

| Epic | Impact |
|---|---|
| **Epic 1** | Story 1.3 only, and its code is already built, reviewed and green (166/166 on 2026-09-12). Epic completes as planned. |
| **Epic 4** | **Positive.** Story 4.4 and UX-DR34 need a display name for every Membership; the amendment supplies the definition they were going to be written without. |
| **Epic 4 › Story 4.3** | Registration-from-Invitation inherits the amendment automatically — its AC requires *"the same single slice used by direct registration"* (`epics.md:1706`), so a three-field slice propagates with no edit. Recorded as impacted-but-unedited. |
| All others | None. |

No epic is added, removed, resequenced or invalidated. **`sprint-status.yaml` needs no change**
(checklist 6.4 → N/A).

### Artifact conflicts

Nine edit sites across six files. Precedence note: `SPEC.md:22` states that **`prd.md` is the live
contract** and the spec is the reasoning record — *"where the two ever disagree, `prd.md` is what gets
built"* — so the PRD edits are the load-bearing ones and the spec edits keep the audit trail honest.

| # | File | Line | Defect |
|---|---|---|---|
| A1 | `prd.md` | 36 | 2 — Glossary **Account** carries no display name |
| A7 | `prd.md` | 101 | 1 — UJ-1 narrative (**found at verification, missed by this analysis**) |
| A2 | `prd.md` | 119 | 1 — FR-1 body |
| A3 | `prd.md` | 121-125 | 2 — FR-1 has no testable consequence for the display name |
| A4 | `prd.md` | 164 | FR-4 assumption → decision |
| A5 | `prd.md` | 870-872 | §12 Assumptions Index item 1 → confirmed |
| B1 | `epics.md` | 60 | 1 — Requirements Inventory (**missed by the review**) |
| B2 | `epics.md` | 596 | 1 — Story 1.3 user story |
| C1 | `harness-constraints.md` | 64 | 1 — CAP-1 soft row restatement |
| D1 | `glossary.md` | 7 | 2 — Account term |
| E1 | `acceptance-criteria.md` | 15-20 | 2 — CAP-1 consequences |
| E2 | `acceptance-criteria.md` | 49 | assumption → settled |
| F1 | `SPEC.md` | 268 | assumption → settled |

### Architecture impact — none

AD-22 (one slice creates an Account), AD-23 (uniform non-disclosure) and the RLS policies are untouched:
the change adds a column to a command already shaped by them. `ARCHITECTURE-SPINE.md` needs no edit.

### UI/UX impact — real, and deliberately not fixed

`EXPERIENCE.md:472` (*"Ravi registers with email and password"*) and
`mockups/registration.html:104-122` (two controls, in both panels) still contradict the shipped surface.
**Lee cut these from scope on 2026-09-13.** Recorded here as an accepted residual, not an oversight —
see §5.

### Other artifacts

- **`docs/bmad-coverage.md:115`** — `bmad-correct-course` is marked ⬜ *not exercised*. This run is its
  first firing. Recommended update, out of the approved six.
- **`docs/bmad-coverage.md:32`** — states P6's primary candidate is *"iteration planning"* with OAuth as a
  backup. `harness-constraints.md:53` says **OAuth is the selected P6 change**. Pre-existing divergence,
  found during this analysis, unrelated to FR-1. Flagged, not fixed.
- **Readiness report** (`implementation-readiness-report-2026-08-22.md:171`, `:1240-1247`) — a dated,
  point-in-time record. **Not amended**; issue 5's count drops from thirteen unconfirmed to twelve, which
  the PRD §12 edit records.
- **Story file** `1-3-…md` — its verbatim AC reproduction and open finding are closed out at handoff, not
  here.

## 3. Recommended Approach

**Option 1 — Direct Adjustment. Selected by Lee, 2026-09-13.**

Ratify the three-field surface: amend FR-1 and its restatements, and confirm PRD §12 assumption 1 as a
settled decision.

| Option | Effort | Risk | Verdict |
|---|---|---|---|
| **1. Direct Adjustment** | **Low** — documents only, no code | **Low** — ratifies shipped, reviewed, green code | **Selected** |
| 2. Rollback to two fields | High — schema migration, re-open the naming choke point, re-run 1.3's suite | High — leaves UX-DR34 with no display name for Epic 4 and re-opens a decision taken 16 days ago | Not viable |
| 3. PRD MVP review | High | High — MVP is unaffected; a scope review over one field is disproportionate | Not viable |

**Rationale.** The code is not in question — it is built, code-reviewed, and passing 166/166 with the
container runtime up. What is in question is whether the documents say what was built, and they do not.
Option 2 would discard correct work to preserve a sentence. Option 1 also confirms a PRD §12 assumption
that has been sitting unconfirmed since 2026-08-22, which is what readiness issue 5 asked for.

**MVP impact: none.** No scope added or removed. One field, already built.

**Amendment mechanism.** `epics.md` already carries an `amendments:` frontmatter array with two entries
from story 1.1's code reviews. That is the established precedent for amending a `status: final` artifact
in this project, and both `epics.md` and `prd.md` will use its shape.

## 4. Detailed Change Proposals

### A. `prd.md` — the live contract

**A1 — §2 Glossary, `:36`**

> **OLD:** `- **Account** — A registered identity in Yello, unique by email address. Global: one Account exists across all Spaces. An Account is never owned by a Space. *Uniqueness by email address…*`
>
> **NEW:** `- **Account** — A registered identity in Yello, unique by email address, carrying a password and a **display name**. Global: one Account exists across all Spaces. An Account is never owned by a Space. The display name is how the Account is identified on every surface outside Space settings' Membership management (UX-DR34), and is the name the auto-provisioned Space is derived from (FR-4); **it is not unique and is never an identifier** — two identical display names are disambiguated by a Membership-scoped discriminator, never by the address. *Uniqueness by email address…*`

*Rationale:* closes Defect 2 at the site PRD §2's own rule designates, in the inline-attribute style
already used for **Task**. Non-uniqueness is stated because UX-DR34 depends on it.

**A2 — §4.1 FR-1 body, `:119`**

> **OLD:** `A person can create an Account with an email address and a password. The email address uniquely identifies the Account across Yello. Realizes UJ-1, UJ-3.`
>
> **NEW:** `A person can create an Account with an email address, a password and a display name. The email address uniquely identifies the Account across Yello; the display name identifies the Account to other people and is not unique. Realizes UJ-1, UJ-3.`

**A3 — §4.1 FR-1 Consequences, append after `:125`**

> **NEW:** `- A display name is required to complete registration, and is the name the Space provisioned under FR-4 is derived from.`

*Rationale:* the naming half was asserted by no acceptance criterion anywhere — a grep across `epics.md`
returns only the *renameable* half (`:618`). This makes it testable.

**A4 — §4.2 FR-4, `:164`**

> **OLD:** `` - `[ASSUMPTION: the provisioned Space is named from the Account's display name, e.g. "Ravi's Space", and is renameable immediately.]` ``
>
> **NEW:** `- The provisioned Space is named from the Account's display name, e.g. "Ravi's Space", and is renameable immediately. *(Confirmed as a decision on 2026-08-28 and implemented in story 1.3; was an assumption. The display name it derives from is collected at registration under FR-1.)*`

**A5 — §12 Assumptions Index, preamble and item 1**

> **Preamble, append:** `A third was confirmed and retired on 2026-09-13 — item 1, the Personal Space naming — leaving twelve open. Retired entries keep their position rather than being renumbered, so existing references to items 2–13 stay valid.`
>
> **Item 1 OLD:** `1. §4.2 FR-4 — The auto-provisioned Space is named from the Account's display name and is immediately renameable.`
>
> **Item 1 NEW:** `1. §4.2 FR-4 — The auto-provisioned Space is named from the Account's display name and is immediately renameable. **— CONFIRMED 2026-08-28, retired as an assumption on 2026-09-13. Now a decision stated at FR-4; the display name it derives from is collected under FR-1.**`

*Rationale:* readiness issue 5 counts thirteen unconfirmed assumptions. This is the first confirmation,
and it is the one that had already hardened into a `required` schema column.

**A6 — frontmatter:** `updated: 2026-09-14`, plus an `amendments:` array in `epics.md`'s shape recording
this change.

**A7 — §3.3 UJ-1, `:101`** *(found during verification, after this analysis had declared the inventory complete)*

> **OLD:** `He registers with email and password and lands directly in a Space already named "Ravi's Space" — he did not create it and was not asked to.`
>
> **NEW:** `He registers with his email address, a password and the name he goes by, and lands directly in a Space already named "Ravi's Space" — he did not create it and was not asked to.`

*Rationale:* this is the sentence that made the contradiction vivid in the first place — a two-field
registration landing in a Space named from a field nothing collected. With the amendment applied, UJ-1
is internally coherent for the first time: the Space name now has a stated source.

### B. `epics.md` — `status: final`, amended deliberately

**B1 — Requirements Inventory, `:60`** *(the site the review missed)*

> **OLD:** `FR-1: A person can create an Account with an email address and password; the address uniquely identifies it across Yello.…`
>
> **NEW:** `FR-1: A person can create an Account with an email address, a password and a display name; the address uniquely identifies it across Yello and the display name does not.…`

**B2 — Story 1.3 user story, `:596`**

> **OLD:** `I want to create an Account with my email address and a password and immediately have a Space of my own,`
>
> **NEW:** `I want to create an Account with my email address, a password and the name I go by, and immediately have a Space of my own already carrying that name,`

**B3 — frontmatter `amendments:`, new entry dated 2026-09-13**, matching the two existing entries'
shape, plus `updated: 2026-09-13`.

### C. `harness-constraints.md` — restatement only, semantics preserved

**C1 — the P6 soft-assumptions table, `:64`**

> **OLD:** `| CAP-1 — an Account is created with an email address **and a password** | An OAuth Account has no password. Nothing may assume a password exists on every Account |`
>
> **NEW:** `| CAP-1 — an Account is created with an email address, **a password** and a display name | An OAuth Account has no password. Nothing may assume a password exists on every Account. *The display name is not at risk from OAuth: providers return one, so collecting it hardens nothing the change breaks.* |`

*Rationale:* the row's job is to keep the **password** assumption soft until OAuth fires. That job is
unchanged — only the restatement is corrected. The added clause records why the third field needs no
soft-assumption row of its own, so a later reader does not add one.

### D. `glossary.md` — spec companion

**D1 — `:7`** — mirror A1, in the companion's terser register, keeping the existing scheduled-change note
at `:30` untouched.

### E. `acceptance-criteria.md`

**E1 — CAP-1 consequences, append after `:20`**

> **NEW:** `- A display name is required to complete registration, and is the name the Space provisioned under CAP-4 is derived from. It is not unique.`

**E2 — CAP-4, `:49`**

> **OLD:** `- *Assumed:* it is named from the Account's display name (e.g. "Ravi's Space") and is renameable immediately.`
>
> **NEW:** `- It is named from the Account's display name (e.g. "Ravi's Space") and is renameable immediately. *(Settled 2026-08-28; was an assumption.)*`

### F. `SPEC.md`

**F1 — Assumptions, `:268`**

> **OLD:** `- The auto-provisioned Space is named from the Account's display name and is immediately renameable.`
>
> **NEW:** `- ~~The auto-provisioned Space is named from the Account's display name and is immediately renameable.~~ **Settled 2026-08-28, retired as an assumption 2026-09-13** — now a decision at FR-4 (`prd.md`) and CAP-4 (`acceptance-criteria.md`). The display name it derives from is collected at registration under CAP-1.`

CAP-1's `intent:` at `:39` is deliberately **not** edited: it speaks to identity and provisioning, not to
field count, and remains accurate as written.

### Optional — both taken by Lee, 2026-09-14

- **G1 `decisions-settled.md`** — a row added to *"Ownership, Membership and the Personal Space"*
  recording Personal Space naming as settled, with the three rejected alternatives and why: the email
  local-part (puts a fragment of the address into a Space name every Member can read, against §6.1 and
  UX-DR34), a generic `"My Space"` (contradicts the literal *"Ravi's Space"* that UJ-1 prints), and asking
  for a Space name at registration (UJ-1 requires the first Space to arrive without a structural
  decision). The question cannot now be re-opened downstream without a knowing amendment.
- **H1 `docs/bmad-coverage.md`** — `bmad-correct-course` flipped from ⬜ to ✅ with the findings below
  recorded in the house format.

## 5. Implementation Handoff

**Scope classification: MINOR.** Documentation only. No code, no schema, no test changes. Direct
implementation.

| Recipient | Responsibility |
|---|---|
| **Developer (this session)** | Apply A1–A6, B1–B3, C1, D1, E1–E2, F1 |
| **Lee** | Cut or approve G1 and H1; close the story 1.3 review finding; flip story status |

**Success criteria — verified 2026-09-14**

1. ✅ No requirement artifact states the two-field contract. Grep for *"email address and a password"* /
   *"email address and password"* / *"with email and password"* across `_bmad-output/` returns only: the
   story file (point-in-time record), the readiness report (point-in-time record), this proposal's own
   `OLD:` quotations, `EXPERIENCE.md:472` (cut from scope) and `surfaces-and-journeys.md:24` (residual,
   see above). **This grep is what found A7** — the analysis above had declared the inventory complete
   one site short.
2. ✅ **Account** carries a display name in both glossaries, with non-uniqueness stated.
3. ✅ The naming half is covered by a testable consequence at `prd.md` FR-1 and `acceptance-criteria.md`
   CAP-1 — it was previously asserted by nothing anywhere.
4. ✅ PRD §12 reads twelve open assumptions, item 1's position preserved, references to 2–13 still valid.
5. ✅ `harness-constraints.md:64` still keeps the **password** assumption soft for P6, with the reason the
   display name needs no soft row recorded alongside it.
6. ✅ Story 1.3's `[Review][Decision]` finding can be checked off.

**Accepted residuals — recorded, not fixed**

| Residual | Why |
|---|---|
| `EXPERIENCE.md:472` and `mockups/registration.html` still show two fields | Cut from scope by Lee, 2026-09-13. The UX spine is `status: final` and the mockup is a point-in-time artifact; the shipped surface is the live truth. **Anyone reading the mockup as the registration contract will be wrong.** |
| `surfaces-and-journeys.md:24` still reads *"registers with email and password"* | The spec-side mirror of UJ-1. Not among the six approved files. Left deliberately, but it is now the **only** remaining statement of the two-field contract inside the spec, and it contradicts `glossary.md:7`, `acceptance-criteria.md:21` and `harness-constraints.md:64` in the same companion set. **One line; recommend taking it.** |
| `docs/bmad-coverage.md:32` names iteration planning as the P6 candidate; `harness-constraints.md:53` says OAuth | Pre-existing divergence, unrelated to FR-1 |
| `docs/bmad-coverage.md:102-103` marks `bmad-dev-story` and `bmad-code-review` as ⬜ not-run | Both have now run in full against story 1.3. Stale rows, unrelated to FR-1; worth a pass when the story closes |
| Story 1.3's ACs gain no naming criterion | The ACs are reproduced verbatim in the story file; amending them now would desynchronise a story whose implementation is complete. The consequence added at A3/E1 covers the requirement instead. |

## Finding for `docs/bmad-coverage.md`

**Capability + limitation, from the first `bmad-correct-course` firing (2026-09-13).**

**This was not the reserved P6 change.** It fired unplanned, from a code review, on a contradiction
between shipped code and a `status: final` requirement — which is better coverage evidence than the
scheduled OAuth change will be, because nothing about it was arranged.

- **Limitation — a decision taken mid-story does not propagate.** Lee decided on 2026-08-28 to collect a
  display name. The story recorded it in four places and shipped. **Nothing in the BMad chain routed that
  decision back into the artifacts it contradicted**, and no gate caught it: sprint planning, story
  creation and dev-story all read forward from the PRD, and none reads backward. It took a code review 13
  days later to notice, and only because a reviewer read `prd.md:119` against the code rather than
  against the story.
- **Limitation — the review under-counted the blast radius.** The finding named three sites. There were
  nine, across two distinct defects, and one of the three named files was mis-read: `harness-constraints.md:64`
  is a *keep-this-soft* row about the password, not a field-count rule. A finding is a pointer, not an
  inventory; the impact analysis has to be run again from scratch.
- **Capability — the checklist's artifact sweep found what no review layer did.** Three parallel
  adversarial review layers all missed that `Account` has no display-name attribute in either glossary
  while `Account.DisplayName` is a `required` column. Section 3's mechanical "check each artifact for
  conflicts" pass surfaced it immediately, along with the second FR-1 restatement at `epics.md:60`.
- **Limitation — the artifact sweep under-counted too, and only a mechanical check caught it.** Having
  corrected the review's three sites to nine, this analysis then declared the inventory complete and was
  still wrong: `prd.md:101`, UJ-1's own narrative, was missed. It was found by the post-edit verification
  grep — the routine step, not the analytical one. **A prose sweep of nine documents is not a search, no
  matter how carefully it is done**, and the checklist has no step that requires a mechanical
  confirmation before the proposal is called complete. Both the review layer and the correct-course sweep
  read a document; the only thing that read the whole corpus was `grep`.
- **Capability — the amendment mechanism held.** `epics.md`'s `amendments:` frontmatter, introduced by
  story 1.1's code review, gave this change a precedent to follow rather than a format to invent.
