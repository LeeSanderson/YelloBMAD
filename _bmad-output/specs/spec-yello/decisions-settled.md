# Settled Decisions

Companion to `SPEC.md` (SPEC-yello). Holds shapes that were considered and rejected, and why.

**Purpose: stop a settled question being re-opened downstream.** Every row below was decided with the reasoning recorded. If a story, an epic, a review or a correct-course run proposes one of these shapes, the answer is "already considered" plus the reason — not a fresh debate. Re-opening one is a deliberate amendment to `SPEC.md`, made knowingly.

This does **not** cover decisions the architecture owns. The architecture spine records its own rejected alternatives; consult it for mechanism.

## What a tenant is

The starting concept was one line: *a multi-tenant project/task management platform.* The decisive question was what a tenant **is**, because that determines whether tenancy is a real invariant or a column on every table.

| Option | Tenant would be | Rejected because |
|---|---|---|
| Consultancy → client workspaces | A client organisation of a services firm | Real tenancy tension, but pulls utilisation and billing depth into scope and couples the product to one industry |
| Generic SaaS work tracker | A customer organisation, hard-walled | Tenancy becomes uniform: every table gets a tenant id, every query the same filter, no rule ever spans two features |
| Open-source project hub | A project with a public/private boundary | The permeable boundary made isolation interestingly conditional, but Roles collapse to maintainer/contributor with little asymmetry |
| **Space — selected** | **A container that is simultaneously the work boundary and the access boundary** | — |

**Why the selected model avoids the uniformity problem.** Membership is many-to-many with the Role carried on the relationship, so authorisation is a function of `(Account, Space)` rather than a property of the Account. One person legitimately holds different standing in different Spaces at the same time, which means the active Space must be resolved *before* any authorisation decision, no requirement can be expressed as "an Admin can X", and isolation is a rule about a relationship rather than a filter applied uniformly.

## Ownership, Membership and the Personal Space

| Decision | Selected | Rejected |
|---|---|---|
| Personal Space type | An ordinary Space, auto-created | A distinct undeletable type (adds a second concept and a special case to every lifecycle rule); a permanently private type (contradicts the shareability the model depends on) |
| Ownership | Exactly one Owner. Transfer is an **offer the recipient must accept**; the old Owner drops to Admin | Immutable creator-ownership (orphans Spaces on Account deletion, or cascades into deleting other people's work); multiple Owners (collapses the Owner/Admin distinction); **immediate unilateral transfer** — see below |
| Who may invite | Owner and Admin | Any Member (three of four Roles behaving identically weakens the Role model); per-Space configurable (a config dimension on every invitation path) |
| Who may be invited | Any email address, no restriction | Domain-locked invitation — it presumes an organisation concept Yello does not have, and is ruled out alongside SSO |
| Admin symmetry | Admins cannot modify each other | **Not a settled decision** — recorded as an assumption in `SPEC.md`. Keeps the Owner meaningfully distinct from Admin |

### Why transfer needs consent

The PRD specified transfer as immediate and unilateral, and left "can ownership be transferred to someone who then declines it?" as an open question. Resolved: **it cannot, because ownership is now an offer.** The reason is not politeness but a defect the original wording permitted.

Chained together, three rules trapped the recipient:

1. Transfer was immediate and needed no agreement.
2. An Owner's Membership cannot be removed while it holds ownership — so the new Owner could not leave.
3. Account deletion is refused while the Account owns any Space — so the new Owner could not delete their Account.

An Owner could therefore transfer a Space to any Membership, immediately remove their own now-Admin Membership, and leave that person permanently unable to delete their Yello Account. Their only exits were to destroy the Space irreversibly or to impose the same thing on a third party. **One Account could unilaterally block another's erasure.**

Requiring acceptance closes it at the root and costs one capability (CAP-42). Two cheaper fixes were rejected: letting the recipient reverse the transfer fails in exactly the abusive case, because a departed previous Owner leaves nobody to reverse it to; and documenting the asymmetry without fixing it would have left the erasure route in `harness-constraints.md` conditional on nobody exploiting it.

## Status configuration

Three models were put forward and a fourth was authored in response.

| Model | Shape | Outcome |
|---|---|---|
| Fixed global set | The same Statuses everywhere | Rejected — removes a whole feature and its edge cases |
| Per-Space only | The Space defines, Projects share | Rejected — no divergence, so no resolution rule |
| Per-Project only | Each Project independent | Rejected — Boards stop being comparable within a Space, and every Status problem multiplies by the number of Projects |
| **Space defaults + Project delta — selected** | Effective set = Space defaults with the Project's delta applied | Selected |

Settled semantics:

- Delta operations are add, remove, rename, reorder.
- **No revert.** A Project does not toggle between inheriting and overriding; there is no mode. Its Status set is simply editable for the life of the Project.
- **Removal is always a migration**, at either level. This makes "a Task holds a Status its Project does not expose" unreachable by construction rather than by validation.
- A removed Status can be re-added at any time.
- A Space-level rename propagates to non-conflicting Projects automatically; where a Project renamed the same Status, the operation reports the conflict and offers to cascade as a single choice.
- Space-level removal uses one destination Status applied Space-wide, **plus a per-Project destination for any Project whose effective set cannot accept it** — reported and asked, never guessed — all applied atomically. An earlier draft let such Tasks fall to the Project's first Status; rejected because it made one half of the capability guess while the other half asked.

**Forced consequence:** a Project's delta must reference Statuses **by identity, not by name**. The cascade offer requires detecting that a Project renamed *the same* Status object; name-keyed deltas cannot express that.

## Which concurrency surface carries the weight

| Option | Assessment |
|---|---|
| Board drag-and-drop ordering | A hard ordering problem with no off-the-shelf answer. Not selected as the headline, but **retained as a secondary surface** via CAP-29 |
| **Collaborative Task description editing — selected** | Carries a known risk: text convergence is a solved problem, so the architecture will likely adopt a library and the interesting decision moves into a dependency |
| Presence + optimistic field updates | Rejected — last-write-wins rarely fails a test, so the requirement has little to grip |

**How the risk was compensated.** CAP-31 … CAP-33 state convergence, reconnection and attribution as behavioural consequences without naming a mechanism, so the requirement stays meaningful regardless of what is adopted. The load-bearing requirement was then placed where no library helps: **CAP-34**, a participant demoted or removed while holding unsynchronised local changes. That is not a text-convergence problem but a question about where authorisation is evaluated in a synchronisation pipeline that is, by design, tolerant of delay and reordering. Expect it to constrain the real-time design more than convergence does.

## API surface

| Decision | Selected | Rejected |
|---|---|---|
| Token scope | Bound to exactly one Space at issue | Account-scoped (makes the Token the only cross-Space object in Yello, contradicting the model); Account-scoped with an allowlist (viable, but a list where a single value suffices) |
| Capability resolution | The issuing Account's Role **at request time** | Role frozen at issue time — would let a Token outlive the permission that justified it |
| Board ordering over the API | Readable, not writable | Writable — would spread CAP-29's convergence requirement across two surfaces |

The Token model means a script operating across three Spaces holds three Tokens. This is **deliberate friction**: it makes cross-Space reach impossible to express rather than merely forbidden.

## Deferred, with the reasoning that matters downstream

- **Task comments and activity history** — the most likely regret. No technical obstacle; cut for scope.
- **Webhooks** — a natural companion to the inbound API that would strengthen the integration story. Cut to keep v1's external surface one-directional.
- **Cross-Project search** — deferred, not ruled out. Any future implementation inherits the isolation requirement in full: search must never span Spaces.
- **Cross-Space aggregate views** — **not deferred; ruled out.** A surface spanning Spaces contradicts the model. Recorded here so a future reader does not mistake it for an oversight.
