# Reviewer Gate — Rubric Walker

**Run:** 2026-08-19, Update mode (Gaps 1–3). Run **inline**. Judges the spine against the good-spine checklist in `references/reviewer-gate.md`.

**Verdict: passes. 1 finding fixed, 2 accepted, 1 noted for the next edit.** Deterministic lint clean (0 findings, 27 ADs).

---

## Checklist walk

| Criterion | Result |
|---|---|
| Fixes the real divergence points for the level below, misses none | **Pass.** The update's three gaps map to real divergences: a wrong budget an implementer would build and test to, an entity with no schema guarantee, and an expiry with no mechanism. |
| Every `AD`'s Rule is enforceable and actually prevents its stated divergence | **Pass after one fix** — see Finding 1. |
| Nothing under Deferred could let two units diverge | **Pass.** Deferred is unchanged and this update adds nothing to it. |
| Named tech is verified-current | **Pass after one critical correction** — see `review-web-verification.md`. |
| Ratifies rather than contradicts a brownfield codebase | **N/A.** Still greenfield; no source exists. |
| If a spec drove it, it covers that spec's capabilities | **Pass.** `scope` moves to FR-1…FR-42; FR-42 lands in 4.2 Spaces, which now cites AD-26 and AD-27. |
| No new `AD` weakens or contradicts an inherited one | **Pass.** No parent spine. AD-26 and AD-27 strengthen AD-5, AD-10 and AD-24 rather than weakening them; AD-24 in particular is now explicitly *protected* by AD-26 rather than quietly bypassed. |
| Every dimension the altitude owns is decided, deferred, or an open question | **Pass, unchanged.** The operational/environmental envelope was covered in the original run and this update does not touch it. |

---

## Finding 1 — FIXED (medium) — `AD-27`'s no-timer rule relied on discipline

The Rule said no job and no timer writes the lapsed state. Nothing stopped one being added later. This project's own standard, recorded in the memlog, is explicit:

> *"invariants must be enforced by construction (type system, single choke point, lint/test gates) rather than by an agent remembering to read the spine. A rule that relies on discipline is not a rule here."*

Against that bar, AD-27 was underspecified — and the risk is not hypothetical, since AD-10 exists precisely because a stray timer both defeats Azure SQL auto-pause and drains the free vCore allowance.

**Fixed:** AD-27 now states the architecture suite (AD-21) fails the build on a scheduled component writing a terminal expiry state.

## Finding 2 — FIXED (high) — FR-42's headline invariant was asserted but ungated

AD-26 asserted *never zero or two Owners* with no test behind it. The four gating suites cover isolation, revocation, merge conformance and architecture; none covers ownership transitions. AD-17 already establishes the right pattern for this exact situation — *"an invariant test asserts that no Task ever holds a Status absent from its Project's effective set."*

**Fixed:** AD-26 now carries the parallel invariant test — no Space ever holds zero or two Owner Memberships. This is also the practical net under concurrency for the forbidden-`SaveChanges` rule, which cannot be gated as directly.

## Finding 3 — ACCEPTED (low) — `AD-26` is long

It carries the entity shape, the uniqueness guarantee, the row-identity authorisation rule, the transition guard, the swap ordering, the refusal status, the read path and the invariant test. Splitting it would put the ordering rule and the index guaranteeing it in separate ADs, which is worse — the ordering is only comprehensible next to the constraint that forces it. AD-2 and AD-8 already set precedent for a long, densely-bulleted AD. **No change.**

## Finding 4 — NOTED (low) — the spine does not record which PRD revision it reconciles against

`sources:` lists the PRD path with no revision marker, so a future reader cannot tell from the spine alone that it was reconciled against the 2026-08-18 revision rather than the original. `updated: '2026-08-19'` is the only signal, and the full trail is in the memlog.

Deliberately **not fixed**: the skill's own division is that the spine carries decisions and the memlog carries rationale and provenance. Worth reconsidering only if source-revision drift recurs — which, given that this entire update exists *because* of exactly that drift, is a real possibility. Flagged rather than actioned.

---

## Observation on the update's cause

The defect class this update repairs is not a modelling error but a **sequencing** one, already documented at `docs/bmad-coverage.md`. Three of the four stalenesses were mechanical consequences of the PRD moving underneath a finalised spine:

- a retired budget still cited (`AD-8`),
- a requirement outside the declared `scope`,
- a cross-reference broken by list renumbering (Deferred citing "PRD assumptions 2 and 4" when Project deletion had moved to 5).

That last one is the tell: **numeric cross-references into a renumberable list rot silently.** It was fixed by citing FR-7 and FR-17 instead of index positions. Worth generalising — the spine cites `§` sections and FR/NFR/AD IDs elsewhere, all of which are stable identifiers; the assumption-index citation was the only positional reference in the document, and it was the only one that broke.
