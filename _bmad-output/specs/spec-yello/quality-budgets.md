# Quality Budgets

Companion to `SPEC.md` (SPEC-yello). Holds the cross-cutting non-functional requirements and the scale envelope they hold within.

IDs are **NFR-1 … NFR-9**, unchanged from the source PRD, because the architecture spine binds to them by that name. Do not renumber.

Each is written so that it **can fail** — a requirement no plausible implementation could violate is a sentiment, not a requirement. Numbers are stated even where provisional, because an unstated budget is one nobody can miss.

## NFR-1 — Isolation is absolute

No data belonging to a Space reaches any Account without a Membership in that Space, by any route.

- Holds for the browser and the API identically, and for reads, writes, listings, aggregates, search results, notifications and error messages.
- Holds for identifiers: possessing the identifier of a Task, Project or Space confers nothing.
- Holds under error: a failure, timeout or partial response never discloses data or existence across a Space boundary.
- **This is the one requirement with no acceptable failure rate.** A single verified cross-Space disclosure blocks release.

## NFR-2 — Authorisation is evaluated fresh, per request

No authorisation decision is served from a cache that could outlive the Membership it was derived from.

- A Role change or Membership removal is reflected in authorisation decisions **within 5 seconds**, without the affected Account acting.
- No request is authorised using a Role established during a previous active Space.
- Applies to API Tokens on the same terms (CAP-36).

## NFR-3 — Collaborative editing feels immediate

- A local edit renders locally within **16 ms** — one frame at 60 Hz — without waiting on any network round trip.
- A remote participant's edit renders within **300 ms at the 95th percentile** on a connection with 50 ms round-trip latency.
- Presence appears within **2 seconds** of a participant arriving and disappears within **10 seconds** of them leaving.

## NFR-4 — Concurrent edits converge

- All participants in an editing session observe identical text within **2 seconds** of the last edit by any of them.
- Convergence holds for at least **10 simultaneous editors** on one Task description.
- A participant disconnected for up to **5 minutes** reconciles on reconnection without loss or duplication (CAP-33).

## NFR-5 — The API is predictable

- Read requests complete within **300 ms** and writes within **500 ms**, both at the 95th percentile, measured server-side within the scale envelope in NFR-8.
- Every refusal carries a machine-readable reason a client can branch on; no client should need to parse prose.
- Retrying a write that timed out does not apply it twice.

> **Unresolved, and owned by the architecture spine rather than by this spec.** Whether NFR-5 is measured warm or cold is undecided — the chosen deployment shape makes most requests cold under sparse traffic, against a 300 ms p95 read budget. See the spine's `Deferred` table for the mitigation options. It must be stated either way, not left silent.

## NFR-6 — Credentials are held safely

- Passwords are stored using a deliberately slow one-way function and are never recoverable. The work factor is the architecture's call; it must be tunable without re-registering existing Accounts.
- API Tokens are stored such that a read of the datastore does not yield usable Tokens, and are displayed exactly once (CAP-36).
- No password or Token appears in any log, error message, notification, analytics event or API response.
- All traffic is encrypted in transit.

## NFR-7 — Refusals are observable

- Every authorisation refusal is recorded with the acting Account, the target Space, the capability attempted and the outcome.
- Cross-Space access attempts are distinguishable in that record from within-Space permission failures — the two mean very different things.
- Records are retained long enough to investigate an incident. *Assumed 90 days; now carried by the architecture.*

## NFR-8 — Scale envelope

The system holds its other guarantees within these bounds and is not required to hold them beyond.

| Dimension | Bound |
|---|---|
| Spaces per Account | 50 |
| Memberships per Space | 100 |
| Projects per Space | 50 |
| Tasks per Project | 5,000 |
| Concurrent editors per Task | 10 |
| Concurrent active Sessions per Space | 50 |

Exceeding a bound must degrade **visibly** rather than silently — a refusal, not a wrong answer. A bound that is not enforced is a defect, not a relaxation.

*These bounds are set by judgement, not measurement. They exist so performance claims have a stated domain. `SPEC.md` carries an open question about them: the revisit-with-evidence the PRD asked for was meant to happen before the architecture was shaped around them, and it did not.*

## NFR-9 — The primary flows are accessible

- Registration, Space switching, the Board, the Task editor and the invitation flow meet **WCAG 2.1 AA**.
- Every Board operation available by pointer is available by keyboard, including moving a Task between columns.
- Presence and permission-change notices are announced to assistive technology, not conveyed by colour or position alone.
