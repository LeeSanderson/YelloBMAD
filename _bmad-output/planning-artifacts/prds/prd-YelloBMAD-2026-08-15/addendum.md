# Yello — PRD Addendum

Companion to `prd.md`. Holds material that informed the PRD but does not belong in it: rejected alternatives and why they were rejected, options-considered matrices, and constraints on mechanism that the PRD states only as behaviour.

Intended readers: whoever writes the architecture, the UX specification, and the epics.

**Revised 2026-08-18.** A `bmad-spec` pass distilled `prd.md`, surfaced defects, and all six of its open questions were resolved. Sections 2, 3 and 7 below are updated; §8 is new and records what was rejected in that pass. The full decision trail is `specs/spec-yello/.memlog.md`.

---

## 1. Domain shape — options considered

The starting concept was one line: *"a multi-tenant project/task management platform."* The decisive question was **what a tenant is**, because that determines whether tenancy is a real invariant or a column on every table.

| Option | Tenant is | Why rejected |
|---|---|---|
| Consultancy → client workspaces | A client organisation of a services firm | Real tenancy tension, but pulled utilisation and billing depth into scope, and coupled the product to one industry |
| Generic SaaS work tracker | A customer organisation, hard-walled | Tenancy uniform: every table gets a tenant id, every query the same filter, no rule ever spans two features. The all-CRUD failure mode |
| Open-source project hub | A project with a public/private boundary | Permeable boundary made isolation interestingly conditional, but roles collapse to maintainer/contributor with little asymmetry |
| **Space (selected)** | **A container that is simultaneously the work boundary and the access boundary** | — |

**Why the selected model avoids the uniformity problem.** Membership is many-to-many with a Role carried on the relationship, so authorisation is a function of `(Account, Space)` rather than a property of the Account. A single person legitimately holds different standing in different Spaces at the same time, which means:

- Active Space must be resolved *before* any authorisation decision is possible — context establishment is a functional step, not navigation.
- No requirement can be expressed as "an Admin can X"; every one must name the Space.
- Isolation is not a filter applied uniformly but a rule about a relationship, which is harder to get right and therefore worth testing.

## 2. Ownership, membership and personal Space — options considered

| Decision | Selected | Alternatives rejected |
|---|---|---|
| Personal Space type | Ordinary Space, auto-created | A distinct undeletable type (adds a second concept and a special case to every lifecycle rule); a permanently private type (contradicts the shareability the model depends on) |
| Ownership | Exactly one Owner. Transfer is an **offer the recipient must accept** (FR-8 + FR-42); old Owner drops to Admin | Immutable creator-ownership (orphans Spaces on account deletion, or cascades into deleting other people's work); multiple Owners (collapses the Owner/Admin distinction); **immediate unilateral transfer** — see below |
| Who may invite | Owner and Admin | Any Member (three of four Roles behaving identically weakens the Role model); per-Space configurable (config dimension on every invitation path) |
| Who may be invited | Any email address, no restriction | Domain-locked invitation was ruled out with SSO in §8 — it presumes an organisation concept Yello does not have |
| Admin symmetry | Admins cannot modify each other | Recorded as an assumption in the PRD, not a settled decision. Keeps Owner meaningfully distinct |

### Why ownership transfer needs consent

The PRD originally specified transfer as immediate and unilateral, and asked as an open question whether ownership could be given to someone who then declines it. It cannot, because ownership is now an offer — and the reason is a defect the original wording permitted, not politeness.

Three rules chained into a trap:

1. Transfer was immediate and needed no agreement (FR-8).
2. An Owner's Membership cannot be removed while it holds ownership (FR-14) — so the new Owner could not leave.
3. Account deletion is refused while the Account owns any Space (FR-3) — so the new Owner could not delete their Account.

An Owner could therefore transfer a Space to any Membership, immediately remove their own now-Admin Membership, and leave that person **permanently unable to delete their Yello Account**. Their only exits were to destroy the Space irreversibly or to impose the same thing on a third party. One Account could unilaterally block another's erasure — which also made FR-3 a conditional erasure route rather than a real one, a point §6.4 of the PRD now depends on.

Two cheaper fixes were rejected:

- **Let the recipient reverse the transfer.** Fails in exactly the abusive case: a previous Owner who has already left leaves nobody to reverse it to.
- **Document the asymmetry without fixing it.** Would have shipped a defect that blocks Account deletion, and left §6.4's erasure claim true only while nobody exploited it.

Consent is now a single principle rather than two local rules: **neither Membership nor ownership ever arrives unrequested** (FR-11, FR-42).

## 3. Status configuration — options considered

Three models were put forward and a fourth was authored in response.

| Model | Shape | Outcome |
|---|---|---|
| Fixed global set | Same Statuses everywhere | Rejected — removes a whole feature and its edge cases |
| Per-Space only | Space defines, Projects share | Rejected — no divergence, so no resolution rule |
| Per-Project only | Each Project independent | Rejected — Boards stop being comparable within a Space |
| **Space defaults + Project delta (selected)** | Effective set = Space defaults with the Project's delta applied | Selected |

**Delta semantics as settled:**

- Delta operations: add, remove, rename, reorder.
- **No revert.** A Project does not toggle between inheriting and overriding; there is no mode. Its Status set is simply editable for the life of the Project.
- **Removal is always a migration.** Removing a Status at either level requires mapping occupying Tasks to another Status, in the same operation. This makes the "Task holds a Status its Project does not expose" state unreachable by construction rather than by validation.
- A removed Status can be re-added at any time.
- Space-level rename propagates to non-conflicting Projects automatically; where a Project renamed the same Status, the operation reports the conflict and offers to cascade as a single choice.
- Space-level removal uses one destination Status applied Space-wide, **plus a per-Project destination for any Project whose effective set cannot accept it** — reported and asked, never guessed — all applied atomically. An earlier draft let such Tasks fall to the Project's first Status; rejected because it made one half of FR-27 guess while the other half asked. Always satisfiable, since FR-25 guarantees a non-empty effective set.

### Constraint on mechanism

**A Project's delta must reference Statuses by identity, not by name.** This is forced by the cascade offer: to report that a Project renamed *the same* Status the Space is now renaming, the system must be able to tell that the Project's renamed entry and the Space's entry are the same object. Name-keyed deltas cannot express this.

Consequences the architecture should expect:
- Statuses need stable identifiers that survive rename at both levels.
- A Project's delta is a set of operations keyed by Status identity, not a materialised list.
- The effective set is derived, not stored — or if materialised for read performance, must be invalidated on any Space-level change.

## 4. Concurrency — options considered

| Option | Assessment |
|---|---|
| Board drag-and-drop ordering | Hard ordering problem with no off-the-shelf answer, so the architecture must genuinely decide something. Not selected as the headline, but retained as a secondary surface via FR-29 |
| **Collaborative task description editing (selected)** | Selected. Carries a known risk: text convergence is a solved problem, so the architecture will likely adopt a CRDT or OT library and the interesting decision moves into a dependency |
| Presence + optimistic field updates | Rejected — last-write-wins rarely fails a test, so the requirement has little to grip |

**How the PRD compensates for the selected option's risk.** FR-31 – FR-33 state convergence, reconnection and attribution as behavioural consequences without naming a mechanism, so the requirement remains meaningful regardless of what the architecture adopts. The load-bearing requirement was then placed where no library helps:

**FR-34 — permission change during an active editing session.** A participant demoted to Viewer or removed from the Space while holding unsynchronised local changes. This is not a text-convergence problem; it is a question about where authorisation is evaluated in a synchronisation pipeline that is, by design, tolerant of delay and reordering. It sits on the seam between isolation, authorisation and concurrency, and it is the requirement most likely to be quietly wrong.

Architecture should expect this to constrain the real-time design more than the convergence requirement does — specifically, the synchronisation channel cannot be a trusted path that authorises once at connection time.

## 5. API surface — options considered

| Decision | Selected | Rejected |
|---|---|---|
| Token scope | Bound to exactly one Space at issue | Account-scoped (makes the Token a cross-Space object, contradicting the model); Account-scoped with allowlist (viable, but a list where a single value suffices) |
| Capability resolution | The issuing Account's Role **at request time** | Role frozen at issue time — would let a Token outlive the permission that justified it |
| Versioning | Stated as a behavioural guarantee; URL path segment and two concurrent versions recorded as an assumption | — |

The Token model means a script operating across three Spaces holds three Tokens. This is deliberate friction: it makes the isolation invariant hold identically on both surfaces, and it makes cross-Space reach impossible to express rather than merely forbidden.

## 6. Deferred with reasons

Items cut from MVP where the reasoning matters downstream:

- **Task comments and activity history** — the most likely regret. No technical obstacle; cut for scope. Flagged in the PRD with a `[NOTE FOR PM]`.
- **Webhooks** — natural companion to the inbound API and would strengthen the integration story. Cut to keep v1's external surface one-directional.
- **Cross-Project search** — deferred rather than ruled out, but note the tension with §8: search must never span Spaces, so any future implementation inherits the isolation requirement in full.
- **Cross-Space aggregate views** — not deferred, ruled out. A surface that spans Spaces contradicts the model. Recorded here so a future reader does not mistake it for an oversight.

## 7. Architectural questions — status

*Raised when this document was written; four were settled by the architecture spine of 2026-08-17, one by the PRD revision of 2026-08-18. Kept with their outcomes so nobody re-opens a closed question.*

| # | Question | Status |
|---|---|---|
| 1 | Whether the effective Status set is derived per read or materialised and invalidated (§3) | **Closed by AD-16** — derived on read, never stored; caching permitted only within a single request |
| 2 | Where authorisation is evaluated within the real-time synchronisation path (§4) | **Closed by AD-8 and AD-9** — the sync channel carries no authority, every inbound frame is authorised, and permission change is pushed at the transaction boundary rather than polled |
| 3 | How Board ordering converges under concurrent drag operations (FR-29) | **Closed by AD-15** — a jittered fractional index scoped to (Project, Status); a move writes only the moved Task's key. Interleaving under concurrent same-slot inserts is mitigated, not eliminated, and remains a spine deferral |
| 4 | Whether the NFR-8 bounds should shape the architecture before they are validated | **Closed by the 2026-08-18 revision** — AD-25 already enforces every bound, so the intended ordering was missed. The bounds are confirmed final for v1 and the verification is rescheduled to the NFR-evidence audit |
| 5 | How FR-41 interacts with the Status delta model | **Closed by AD-17** — reparent plus conditional migration in one transaction; no endpoint accepts the move without the mapping it requires. FR-41's bulk form inherits the same atomicity |

**Still open, and owned by the spine rather than by this document:** whether NFR-5 is measured warm or cold. Scale-to-zero plus auto-pause makes most requests cold under sparse traffic, against a 300 ms p95 read budget.

**New obligations on the architecture** raised by the 2026-08-18 revision:

- **AD-13 must guarantee what compaction preserves.** It currently permits replacing a log prefix with a snapshot row without saying what survives. Per-author change counts and timestamps must, or §10's SM-5 becomes underivable and unrecoverably so.
- **FR-42 needs an `OwnershipOffer` entity** and four slices — offer, accept, decline, revoke. AD-5 still holds: the filtered unique index continues to guarantee one Owner, and acceptance remains one transaction. Note AD-10 forbids unconditional timers, so the 7-day offer expiry must be evaluated lazily on read, exactly as FR-39's expiry already must be.
- **FR-27's removal endpoint** now carries a per-Project destination map rather than a single value.
- **FR-28 and FR-30 must hold NFR-5 and NFR-9 at 5,000 Tasks.** Nothing currently pages or virtualises, and the three requirements cannot all hold naively.

## 8. Rejected in the 2026-08-18 revision

Recorded so downstream does not re-propose them.

| Proposal | Rejected because |
|---|---|
| Tell a removed Account that its access ended, rather than returning an undifferentiated not-found | Needs a removed-Membership tombstone readable by someone with no Membership, so it cannot sit under the Space-scoped context. Costs an AD-3 exception, a third non-Space-scoped surface against AD-24, and a sanctioned carve-out in NFR-1 — the one requirement stated as having no acceptable failure rate. The usability cost is paid with deliberately ambiguous copy instead |
| Push a removal notice to live Sessions at removal time | Not chosen, but the cheapest way to revisit the same usability gap later: it rides AD-9's existing `MembershipChanged` event and needs no new persistent state |
| An in-product metrics endpoint or dashboard for §10's behavioural measures | Would need a third non-Space-scoped surface (AD-24) and would breach NFR-1 to produce a number nobody is entitled to. The measures are operator-side aggregates instead (§6.1) |
| A subject-access / data-export capability | An Account-scoped export spanning Spaces reopens the same AD-24 objection. Held behind the §6.4 gate |
| Lowering Tasks per Project from 5,000 to ~500 to dissolve the Board collision | Revises a bound to dodge a problem rather than on evidence, and removes the substantial-UI difficulty deliberately |
| Keeping the 5-second revocation budget as headroom for a future sync backplane | AD-14 forbids designs needing a backplane, so the headroom would protect a design that does not exist, at the cost of a release gate that cannot fail |
| Refusing a Space-level Status removal until diverged Projects are fixed individually | Honest, but tedious at 50 Projects per Space, and it would leave FR-27's two halves still asymmetric — rename asking, removal refusing |
| Keeping FR-27's first-Status fallback behind a confirmation preview | Removes the silence but still denies the Admin a per-Project choice: accept the whole operation or abandon it |
| A mixed-selection bulk Task move with a per-Status mapping table | Largest new surface proposed, and it opens a transaction-size question at the 5,000-Task bound that neither NFR-5 nor NFR-8 answers. FR-41's bulk form is scoped to one Status instead |
