# Reviewer Gate — Adversarial Seam lens

**Run:** 2026-08-19, Update mode (Gaps 1–3). Run **inline** — no independent context, so the "fresh reviewer finds what the author talks past" property does not hold. Weight accordingly.

**Method:** construct two units one level down that each obey every `AD` to the letter and still build incompatibly. Scoped to the new and amended ADs (5, 8, 9, 26, 27) plus their interactions with the untouched 25.

**Verdict: 3 holes found and closed, 4 attacks failed to break the spine.**

---

## HOLE 1 (closed) — `AD-27` permitted a read-then-decide race and two clock sources

**The attack.** Two slices both "apply the shared predicate," as AD-27 required:

- `Invitations/AcceptInvitation/` loads the row, evaluates `ExpiresAt > DateTimeOffset.UtcNow` **in C#**, then issues the `UPDATE`.
- `Spaces/AcceptOwnershipOffer/` pushes the predicate **into SQL**.

Both obey the letter of "declared once, applied by every read and every transition." They still diverge two ways:

1. **A race.** The C# variant checks and *then* transitions. Between the two, a concurrent request can accept the same offer. The rowcount guard in AD-26 catches that for offers — but AD-27 binds Invitations too, and FR-11 has no equivalent guard stated.
2. **Two clocks.** Application-process time versus database time. Under skew, or inside a long transaction, the two slices disagree about whether the same row is lapsed. Neither is wrong by the AD.

**Fix applied.** AD-27 now requires the predicate to be evaluated **server-side inside the guarded statement's own `WHERE` clause**, against the database clock — never loaded into memory and checked first. This fuses the check to the transition (killing the race) and collapses to one clock. A client-supplied time is explicitly never an input.

---

## HOLE 2 (closed) — no stated status for a state-conflict refusal

**The attack.** Two slices refuse a non-pending transition with different HTTP statuses. AD-3 fixes only the 403/404 line **at the Space boundary**; a valid caller acting on an already-accepted offer is neither. The Error-shape convention pins the *body* (RFC 9457, stable machine-readable `type`) but not the *status*. So:

- `AcceptOwnershipOffer` returns `404` — reasoning "the pending offer no longer exists."
- `DeclineOwnershipOffer` returns `409`.
- The loser of a concurrent `OfferOwnership` race, rejected by the filtered unique index, returns `500` because nobody translated the duplicate-key error.

All three obey every AD. The API contract is incoherent, and the `404` variant additionally teaches clients to conflate conflict with non-existence — corrosive in a product whose entire refusal model turns on that distinction.

**Fix applied.** AD-26 now fixes **409 with a stable problem `type`, never 404**, and says why: the caller holds a Membership in the Space, so AD-3's boundary rule does not apply and a 404 here would be a divergence rather than a disclosure. The concurrent-offer loser is named explicitly as one of the cases.

---

## HOLE 3 (closed) — the recipient's read path invited a third Account-scoped surface

**The attack.** The named recipient must *learn* an offer exists. FR-8 permits any Role to be named, and PRD §7 scopes Space settings to Owner and Admin. An implementer reasonably concludes the recipient needs somewhere else to see it, and builds an "offers awaiting you" view listing offers across Spaces.

That **breaches AD-24**, which enumerates exactly two Account-scoped surfaces and requires an amendment to add a third. Worse, it breaches it *quietly*: the implementer sees a requirement with no surface and invents one, exactly the failure AD-24's Prevents clause describes ("a slice needing to read across Spaces … inventing its own bypass — and that bypass then spreading").

**Fix applied.** AD-26 now states the recipient reads a pending offer **inside the Space's own context** under AD-2, at whatever Role they hold — so no third Account-scoped surface is needed, AD-24 stands unamended, and a cross-Space offers inbox is named as *not* the way to surface this.

**Note for UX:** this closes the architectural half only. It fixes *where the offer is readable from* (in-Space, any Role) and leaves *which surface renders it* open — see the cross-phase finding in the memlog and the paused UX run. It does usefully constrain the UX answer: whatever surface is chosen must sit inside Space context, not in a global inbox.

---

## Attacks that failed (spine held)

| Attack | Why it failed |
|---|---|
| Make `OwnershipOffer` leak across Spaces | It carries `SpaceId`, so AD-2's RLS predicate and the EF global query filter both apply. The general rule "every entity below `SPACE` carries `SpaceId` directly" already covers the new entity without amendment. |
| Remove the *offering* Owner's Membership to orphan a pending offer | AD-5 rejects removing a Membership while it holds ownership, and FR-3 refuses Account deletion while it owns a Space. Only the recipient can vanish, which is exactly the case AD-5's new fourth cascade lapses. |
| Double-accept via retry | Two independent guards: AD-18's `Idempotency-Key` replays the original response, and AD-26's `WHERE State = Pending` rowcount check refuses a fresh key. Belt and braces, deliberately — AD-26 says the endpoint key alone is insufficient because FR-42 admits no route by which ownership arrives unrequested. |
| Tear down the wrong live session on acceptance | AD-9 now publishes per affected Account. Neither party loses write capability in this transition (Owner→Admin retains it), so no editor is torn down — but both leases re-resolve because a lease carries `Role`. Correct without special-casing. |

---

## Residual risk accepted

**AD-26's forbidden-`SaveChanges` rule is prose, not a build gate.** AD-21 fails the build on dependency-direction violations and AD-1's forbidden Identity-role APIs are architecture-tested, but "do not perform *this particular* two-row update through the change tracker" is not mechanically detectable in the same way. The new invariant test (no Space ever holds zero or two Owner Memberships) catches the *symptom* under concurrency, which is the practical mitigation. Given the project's stated bar — *"a rule that relies on discipline is not a rule here"* — this is the weakest link in the update and is worth revisiting if a stronger gate suggests itself.
