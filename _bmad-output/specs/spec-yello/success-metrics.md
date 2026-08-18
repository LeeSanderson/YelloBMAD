# Success Metrics

Companion to `SPEC.md` (SPEC-yello). Holds the behavioural measures and counter-metrics.

The two **gating** criteria — isolation integrity and revocation latency — live in `SPEC.md` under Success signal, because a release fails without them. Everything here is defined **without thresholds**: Yello has no users, and a target invented now would be indistinguishable from one that had been earned. They are stated so the right things get instrumented, and so whoever sets thresholds later knows which direction each should move.

IDs are **SM-1 … SM-6** and **SM-C1 … SM-C4**, unchanged from the source PRD. SM-1 and SM-2 are the gating pair in `SPEC.md` and are restated here only as pointers.

> ⚠️ **No capability carries instrumentation.** Nothing in CAP-1 … CAP-41 authorises collecting any of the measures below, and one constraint in `SPEC.md` states that Yello collects no behavioural analytics on the contents of Spaces. `SPEC.md` carries this as an open question. Until it is answered, do not write an instrumentation story from this file.

## Gating — in `SPEC.md`

- **SM-1: Isolation integrity.** Zero verified cross-Space disclosures, browser and API, in any released build. Validates CAP-15, CAP-16, CAP-35, CAP-36, NFR-1.
- **SM-2: Revocation latency.** Permission changes take effect on live sessions within 5 seconds in 100% of tested cases, including sessions holding unsynchronised local edits. Validates CAP-34, NFR-2.

## Behavioural — instrument, threshold later

| ID | Measures | Direction | Why it matters |
|---|---|---|---|
| **SM-3** | Proportion of Accounts holding Membership in two or more Spaces | Higher is better | **The product's central bet, and the most important number in this group.** If people only ever use one Space, the primitive did not earn its generality and the thesis in `SPEC.md` is wrong. Validates CAP-5, CAP-9, CAP-11 |
| **SM-4** | Proportion of issued Invitations accepted | Higher is better | Read alongside SM-C3. Validates CAP-10, CAP-11, CAP-39 |
| **SM-5** | Proportion of multi-Member Spaces in which a Task description is edited by two Users within the same session | Higher is better | If it stays near zero, the most expensive feature in the product is unused and collaborative editing should be **reconsidered rather than optimised**. Validates CAP-31, CAP-32 |
| **SM-6** | Proportion of Spaces with at least one active API Token | Higher is better | A low figure is not itself a failure — the API exists to make the isolation model hold on a second surface as much as to be popular. Validates CAP-35 … CAP-38 |

## Counter-metrics — do not optimise

Each exists to stop a behavioural metric being gamed. Moving one of these in the "good" direction is a warning, not a win.

| ID | Measures | Why not to maximise it | Counterbalances |
|---|---|---|---|
| **SM-C1** | Spaces created per Account | A high Space count with low Task counts per Space indicates the primitive is **confusing rather than adopted** — people creating Spaces because they cannot tell what one is for | SM-3 |
| **SM-C2** | Time in application | UJ-1's success condition is Ravi *closing the tab*. A task tool people spend longer inside is working worse, not harder | SM-5 |
| **SM-C3** | Invitations issued | The goal is the right people in a Space, not more people. Growth here without growth in SM-5 means Spaces are accumulating spectators | SM-4 |
| **SM-C4** | Notification volume | Every additional notification is a cost to the recipient, and the non-goals rule out Yello becoming a communication tool | SM-4 |
