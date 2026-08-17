# Reconciliation — `brainstorm-intent.md` (2026-08-15) against `prd.md` + `addendum.md`

## Coverage of the input

| Intent section | Landed where | Status |
|---|---|---|
| §1 The actual goal (test harness, methodology-driven requirements) | Deliberately excluded from `prd.md` per the clean-PRD decision; held in `docs/bmad-coverage.md` and `.memlog.md` | Honoured by exclusion |
| §2 Product concept deliberately undefined | Defined across `prd.md` §1–§4. This was the PRD's job and it was done from a genuine cold start | Complete |
| §3 Governing reframe (coverage not design; design for contact plus friction) | Drove feature selection throughout — not stated in the PRD by design | Honoured |
| §4 Nine properties as constraints + carrier deliverable | Carriers written into `docs/bmad-coverage.md`. P4 reported as a partial gap rather than contrived | Complete, one reported gap |
| §5 Four anti-patterns | Checked individually below | All four avoided |
| §6 Working relationship (editorial not generative) | Followed — every decision point offered concrete options to cut | Honoured, logged as an override of the skill's elicitation rule |
| §7 Standing artifacts | Tracker updated with carriers and the P4 resolution. Findings log pending at close | Partial — findings pending |
| §8 Open question on phase gating | Produced a real finding. Pending in the findings log | Partial — pending |

## Anti-pattern check

- **All-CRUD design** — avoided. Several rules genuinely span features rather than sitting inside one: FR-15 conditions every read and write in the document; FR-26 makes Status removal a migration that crosses Projects; FR-34 puts authorisation inside the concurrency path; FR-36 resolves Token capability at request time against a Role that can change; FR-21 constrains Assignee to the Task's own Space. None of these is a table with a form.
- **Frozen requirements** — avoided. Two P6 candidates held deliberately outside MVP: iteration planning (§9.2) and OAuth sign-in (§9.2). Both are genuinely wanted rather than manufactured.
- **Nothing genuinely fails** — avoided. NFR-1 is stated with no acceptable failure rate; SM-1 gates release on it; FR-34 is flagged in the PRD as the criterion the product should be judged on. These can fail.
- **Contrived complexity** — avoided, and tested once. The P4 gap was left open rather than closed with a bolted-on third-party dependency; it will be closed later by OAuth, which is wanted independently.

## Gaps found

1. **The claim under test has now been answered, and the tracker does not say so.** `brainstorm-intent.md` §4 records Lee's position that Yello covers all nine properties, and calls it "currently unfalsifiable — the PRD is where it gets tested." It has been tested. The verdict: seven properties carried, one partial (P4), one not applicable at PRD stage (P7). The standing-risk note in the tracker still reads as though the test has not happened. **Action: update at close.**
2. **The findings log has not been updated.** `brainstorm-intent.md` §7 says the log is "the real output of this whole endeavour" and to update at the end of every session, not in a batch. Two findings are outstanding. **Action: update at close.**
3. **No qualitative content was dropped.** The intent document carried no tone, voice or aesthetic direction for the product itself — it was entirely methodological. There is nothing of that kind for the FR structure to have silently lost.

## Note on one instruction not followed literally

`brainstorm-intent.md` §4 asks the PRD to "name the carrier for each property — the epic or feature that actually delivers it." Carriers are named at **feature and FR level**, not epic level, because epics do not exist yet — they are produced downstream by `bmad-create-epics-and-stories`. The mapping should be revisited once epics exist, since a feature may split across several.
