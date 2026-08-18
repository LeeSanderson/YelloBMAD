# Success Metrics

Companion to `SPEC.md` (SPEC-yello). Holds the behavioural measures and counter-metrics.

The two **gating** criteria — isolation integrity and revocation latency — live in `SPEC.md` under Success signal, because a release fails without them. Everything here is defined **without thresholds**: Yello has no users, and a target invented now would be indistinguishable from one that had been earned. They are stated so the right things stay queryable, and so whoever sets thresholds later knows which direction each should move.

IDs are **SM-1 … SM-6** and **SM-C1 … SM-C4**, unchanged from the source PRD. SM-1 and SM-2 are the gating pair in `SPEC.md` and are restated here only as pointers.

## Gating — in `SPEC.md`

- **SM-1: Isolation integrity.** Zero verified cross-Space disclosures, browser and API, in any released build. Validates CAP-15, CAP-16, CAP-35, CAP-36, NFR-1.
- **SM-2: Revocation latency.** Permission changes govern the affected Account's very next request with no tolerance, and take effect on open live sessions within 1 second, in 100% of tested cases — including sessions holding unsynchronised local edits. Validates CAP-34, NFR-2.

## How these are computed

**Not a product feature.** Every behavioural measure and counter-metric below is an aggregate the **operator** computes by querying the datastore directly, outside the request path and outside the authorisation model. No product surface aggregates across Spaces: an in-product dashboard would need a third non-Space-scoped surface and would have to breach isolation to produce a number nobody is entitled to. There is no instrumentation story to write, and no endpoint to build.

None of these read Task titles, descriptions, Labels or Project names — they are structural and metadata counts only, which is what keeps them clear of the no-behavioural-analytics constraint.

Three retention guarantees exist solely to keep them derivable, and are carried in the contract rather than here:

| Guarantee | Carried by | Keeps derivable |
|---|---|---|
| The Invitation record retains its terminal state — accepted, revoked, expired | CAP-10 criteria | SM-4, SM-C3 |
| A notification send record is retained — Space, kind, timestamp | CAP-40 criteria | SM-C4 |
| Description-log compaction preserves per-author change counts and timestamps | `SPEC.md` constraint | SM-5 |

> **Action outstanding on the architecture.** AD-13 currently permits compaction to "replace a prefix of the log with a snapshot row" without saying what survives. The third guarantee above needs AD-13 amended to say that per-author change counts and timestamps do. `bmad-spec` does not own the spine, so this is flagged, not applied.

## Behavioural — instrument, threshold later

| ID | Measures | Direction | Derived from | Why it matters |
|---|---|---|---|---|
| **SM-3** | Proportion of Accounts holding Membership in two or more Spaces | Higher is better | `Membership` rows | **The product's central bet, and the most important number in this group.** If people only ever use one Space, the primitive did not earn its generality and the thesis in `SPEC.md` is wrong. Validates CAP-5, CAP-9, CAP-11 |
| **SM-4** | Proportion of issued Invitations accepted | Higher is better | `Invitation` terminal state | Read alongside SM-C3. Note the funnel carries a deliberate extra step — acceptance requires authentication plus an explicit act (CAP-11) — so this reads lower than a one-click design would, by choice rather than by defect. Validates CAP-10, CAP-11, CAP-39 |
| **SM-5** | Proportion of multi-Member Spaces in which a Task description is edited by two Users within the same session | Higher is better | `TaskDescriptionChange` author + timestamp, surviving compaction | If it stays near zero, the most expensive feature in the product is unused and collaborative editing should be **reconsidered rather than optimised**. Validates CAP-31, CAP-32 |
| **SM-6** | Proportion of Spaces with at least one active API Token | Higher is better | `ApiToken` rows | A low figure is not itself a failure — the API exists to make the isolation model hold on a second surface as much as to be popular. Validates CAP-35 … CAP-38 |

## Counter-metrics — do not optimise

Each exists to stop a behavioural metric being gamed. Moving one of these in the "good" direction is a warning, not a win.

| ID | Measures | Derived from | Why not to maximise it | Counterbalances |
|---|---|---|---|---|
| **SM-C1** | Spaces created per Account | `Space` + `Membership` | A high Space count with low Task counts per Space indicates the primitive is **confusing rather than adopted** — people creating Spaces because they cannot tell what one is for | SM-3 |
| **SM-C2** | Time in application | — **not measurable in v1** | UJ-1's success condition is Ravi *closing the tab*. A task tool people spend longer inside is working worse, not harder | SM-5 |
| **SM-C3** | Invitations issued | `Invitation` rows | The goal is the right people in a Space, not more people. Growth here without growth in SM-5 means Spaces are accumulating spectators | SM-4 |
| **SM-C4** | Notification volume | Outbox send records | Every additional notification is a cost to the recipient, and the non-goals rule out Yello becoming a communication tool | SM-4 |

**SM-C2 is defined but not measurable in v1.** Nothing records session duration, and session telemetry is listed as out of scope in `SPEC.md` rather than quietly assumed. It is kept here so that whoever adds telemetry later knows this number exists and which direction it should *not* move.
