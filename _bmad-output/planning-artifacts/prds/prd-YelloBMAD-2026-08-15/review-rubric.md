# PRD Quality Review — Yello

Rubric: `.claude/skills/bmad-prd/assets/prd-validation-checklist.md`
Target: `prd.md` (785 lines) + `addendum.md`
Run: 2026-08-15

## Overall verdict

This PRD is decision-ready and unusually strong on done-ness for the invariants it cares most about — FR-34 and NFR-1 are requirements a plausible implementation can genuinely fail, which is rarer than it should be. The thesis is explicit, the features mostly serve it, and scope honesty is exemplary: fourteen assumptions tagged and indexed with a clean roundtrip, and a Non-Goals section that does real work rather than listing what nobody asked for.

Its weaknesses cluster in two places. §0's reading map describes the section numbering of a template rather than of this document, which would actively mislead any downstream workflow source-extracting from it. And §10's targets are invented numbers wearing the clothes of measured ones. Neither is expensive to fix, but the first is the more damaging because it fails silently.

## Decision-readiness — adequate

Decisions are stated as decisions rather than buried as considerations, and the contested ones carry their reasoning inline: FR-21 explains why a Viewer can be an Assignee, FR-27 explains why the cascade is offered rather than forced, FR-15 explains why the not-found/forbidden line sits at the Space boundary. The eight Open Questions are genuinely open — several (1, 2, 8) have no obvious answer and none is rhetorical. The two `[NOTE FOR PM]` callouts sit at real tensions rather than safe checkpoints.

What weakens it is §10. The preamble is honest that targets are "thresholds a v1 would be judged against rather than forecasts," but SM-3's 40%, SM-4's 60%, SM-5's 25% and SM-6's 10% are still numbers with no derivation. A reader cannot tell which were reasoned and which were chosen to look plausible, which makes the whole section harder to act on than an explicit placeholder would be.

Trade-offs are also thinner in the PRD than in the addendum. A reader of `prd.md` alone sees what was decided but rarely what was given up.

### Findings
- **high** Success-metric targets are underived (§10) — Four behavioural targets are asserted with no basis, in a document that is otherwise careful to flag unvalidated numbers (NFR-8 does exactly this). *Fix:* mark them explicitly as placeholders pending a first cohort, or drop the numbers and keep the metric definitions.
- **medium** Trade-offs live only in the addendum (§4 throughout) — Alternatives considered are recorded in `addendum.md`, so `prd.md` reads as a series of settled positions. *Fix:* add a one-clause "rather than X" to the three most contested decisions — Token scope (FR-36), Status delta (§4.7), Viewer-as-Assignee (FR-21).

## Substance over theater — strong

Three personas across eight journeys, all named, each driving decisions rather than decorating: Ravi's multi-Space standing is the reason FR-9 and NFR-2 exist; Nadia's arrival is what makes FR-11's silent-join question worth asking; Tomás exists solely to make the API's Space-binding concrete. Under the four-persona line, and none is furniture.

The Vision could not be swapped into another PRD in this category — it commits to a specific and contestable bet about one primitive serving three containers, and §8 then refuses the obvious hedge by ruling out cross-Space views outright.

NFRs carry product-specific thresholds rather than boilerplate, and NFR-8 does something better than most: it states the domain within which the other budgets apply, and admits the bounds are judgement.

### Findings
- **low** NFR-9 is the least earned NFR (§5) — WCAG 2.1 AA is specific enough to avoid boilerplate, but nothing in the document says how conformance would be established, and it is the one commitment with no stated verification route. *Fix:* name the check, or downgrade to the specific behaviours already listed.

## Strategic coherence — adequate

The thesis is stated plainly in §1 and the document bets on it consistently. Feature order follows the thesis rather than ease — Access Control lands at §4.4, ahead of Projects and Tasks, which is the correct priority for a product whose central claim is about contextual permission. SM-3 validates the thesis directly rather than measuring activity, and four counter-metrics are present and genuinely counter-directional; SM-C2 in particular contradicts the usual instinct by naming time-in-app as something to suppress, and ties it back to UJ-1's success condition.

The incoherence is §4.7. Status Configuration received the most design attention in the document — four FRs, the most intricate rule set, and the only constraint strong enough to reach the data model — while connecting to the thesis less than any other feature. Nothing about Space-defaults-plus-Project-delta follows from "one primitive serves personal, client and company work." It is good design, well specified, and disproportionate.

### Findings
- **medium** Design attention is disproportionate to strategic weight (§4.7) — The most complex feature in the PRD is the one least connected to its thesis, while §4.5 Projects (two FRs) and §4.6 Tasks (five FRs) carry more of the product's actual value with less specification. *Fix:* no change required to §4.7; consider whether Tasks deserves comparable rigour, or record explicitly that Status configuration was elaborated for reasons outside the product thesis.

## Done-ness clarity — adequate

The strongest dimension in places and the weakest in others. FR-34's five consequences are close to exemplary: each names an observable outcome, one of them ("no change authored after the moment of removal reaches the Space by any route, including a delayed or retried synchronisation") is precisely the condition a naive implementation would violate, and the last removes the escape hatch by requiring the effect without the affected party acting. FR-26 is similarly tight — "there is no partial application" is a real acceptance criterion.

Against that, three places substitute adjective for bound, in a document that elsewhere insists on numbers.

### Findings
- **high** An adjective where every sibling has a number (§5 NFR-3) — "A local edit renders locally with no perceptible delay" sits directly above two bullets carrying 300 ms and 2 s. Perceptible is not testable. *Fix:* state a frame budget — 16 ms, or one frame at 60 Hz.
- **medium** The cost constraint has no number (§6.3) — "hobby infrastructure" and "out of budget" cannot be evaluated, and this section exists specifically to constrain the architecture's real-time design. An architect cannot tell whether a proposal complies. *Fix:* state a monthly ceiling.
- **low** Work factor unstated (§5 NFR-6) — "a deliberately slow one-way function" hints at mechanism without bounding it. *Fix:* state a target verification time, or accept as architecture's call and say so.
- **low** Vague in place, precise elsewhere (§4.9 FR-32) — "disappears within a bounded interval" is bounded at 10 s by NFR-3, but the reader of FR-32 alone cannot know that. *Fix:* cross-reference NFR-3.

## Scope honesty — strong

The best dimension. Fourteen `[ASSUMPTION]` tags, every one indexed, roundtrip verified clean at 14↔14. §8 Non-Goals does genuine work — the federation entry pre-empts the most likely future mistake by stating that cross-Space views are "not an omission to be corrected later," which is the sentence that will stop someone adding an "all my Tasks" screen in eight months. §9.2 gives reasons where reasons matter and flags the likely regret with a `[NOTE FOR PM]`.

De-scoping is proposed rather than done silently, and the OAuth entry is a good example of the distinction the document draws well: deferred and explicitly not ruled out, with the reasoning for why it differs from the SSO that §8 does rule out.

Open-items density is 24 (8 Open Questions + 14 assumptions + 2 callouts). High in absolute terms, appropriate for a PRD that is explicitly upstream of architecture and has not yet been through a technical pass.

### Findings
None.

## Downstream usability — thin

Everything mechanical is clean except one thing, and that one thing matters more than the rest combined because it fails silently.

Identifier hygiene is good: FR-1 through FR-40 contiguous with no gaps or duplicates, NFR-1 through NFR-9 contiguous, UJ-1 through UJ-8 all defined and all referenced from at least one FR. Every UJ has a named protagonist carrying context inline, with no standalone persona section — correct for this shape. Glossary terms are used consistently in the requirement text, and each §4 subsection makes sense extracted alone.

But §0 — the section that exists to tell a downstream reader how to navigate — describes a different document. Its reading map is the template's numbering, not this PRD's. A source-extracting subagent told that "§7 says how we know it worked" will read Information Architecture and find no metrics; told that assumptions are "indexed in §9" it will read MVP Scope. This is exactly the failure mode §0 was written to prevent.

### Findings
- **high** §0's reading map describes the wrong document (§0) — "§5–§6 draw the boundary. §7 says how we know it worked. §8–§9 collect what is still unresolved" is template numbering. Actual: §5 NFRs, §6 Guardrails, §7 Information Architecture, §8 Non-Goals, §9 MVP Scope, §10 Success Metrics, §11 Open Questions, §12 Assumptions Index. *Fix:* rewrite the map against the real structure.
- **high** Broken cross-reference to the Assumptions Index (§0) — "indexed in §9" points at MVP Scope; the index is §12. *Fix:* correct the reference.

## Shape fit — adequate, with a deliberate deviation

The chosen shape is right for what this feeds. This is a chain-top PRD ahead of UX, architecture and story creation, so downstream usability and traceability carry more weight than they would for a standalone document, and the PRD invests accordingly — global FR numbering, a disciplined Glossary, UJ cross-references from FRs, and an addendum that keeps mechanism out of the requirements.

The deviation is deliberate and worth naming so a later reader does not read it as miscalibration: the rubric holds that hobby/solo projects warrant light rigour, and by stakes this is emphatically hobby — no users, no market, no deadline. The PRD is nonetheless written at launch rigour. That was an explicit override, recorded in `.memlog.md`, on the grounds that a two-page PRD would not give the downstream phases enough to work with. The substance bar the rubric requires regardless of stakes is met.

Journey density is appropriate rather than excessive: eight UJs for a product with genuine multi-stakeholder interaction, each carrying a distinct requirement cluster, none floating.

### Findings
- **medium** Rigour deliberately exceeds stakes (whole document) — 785 lines for a solo hobby project. Justified and recorded, but a reader arriving without `.memlog.md` will read it as over-formalisation. *Fix:* one line in §0 stating that depth is calibrated to what downstream phases need rather than to the size of the audience.

## Mechanical notes

- **Glossary drift, minor.** "workspace" appears as prose at §4.2 ("the same object serves as a private notebook, a client engagement and a company workspace") where Glossary discipline would prefer Space. §1's uses are legitimate — they describe other products, not Yello. Low impact.
- **ID continuity: clean.** FR-1…40, NFR-1…9, UJ-1…8, SM-1…6 plus SM-C1…C4. No gaps, no duplicates.
- **Assumptions Index roundtrip: clean.** 14 inline tags, 14 index entries, matched.
- **Cross-references: two broken**, both in §0, both listed above. All §4.x and §9.2 references within the body resolve correctly.
- **UJ protagonists: all named** — Ravi (UJ-1, 2, 4, 5, 8), Nadia (UJ-3, 5), Tomás (UJ-7), plus an unnamed client-side stakeholder in UJ-6 who is referenced but never named, unlike every other participant.
