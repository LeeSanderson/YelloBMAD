# Yello — PRD Addendum

Companion to `prd.md`. Holds material that informed the PRD but does not belong in it: rejected alternatives and why they were rejected, options-considered matrices, and constraints on mechanism that the PRD states only as behaviour.

Intended readers: whoever writes the architecture, the UX specification, and the epics.

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
| Ownership | Exactly one Owner, transferable; old Owner drops to Admin | Immutable creator-ownership (orphans Spaces on account deletion, or cascades into deleting other people's work); multiple Owners (collapses the Owner/Admin distinction) |
| Who may invite | Owner and Admin | Any Member (three of four Roles behaving identically weakens the Role model); per-Space configurable (config dimension on every invitation path) |
| Who may be invited | Any email address, no restriction | Domain-locked invitation was ruled out with SSO in §8 — it presumes an organisation concept Yello does not have |
| Admin symmetry | Admins cannot modify each other | Recorded as an assumption in the PRD, not a settled decision. Keeps Owner meaningfully distinct |

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
- Space-level removal uses one destination Status applied Space-wide, atomically.

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

## 7. Open architectural questions raised but not settled

1. Whether the effective Status set is derived per read or materialised and invalidated (§3 above).
2. Where authorisation is evaluated within the real-time synchronisation path (§4 above).
3. How Board ordering converges under concurrent drag operations (FR-29). This is a second ordering problem, distinct from the text convergence in §4 above and not solved by the same mechanism. Whether ordering is exposed over the API is settled — readable, not writable (FR-35) — but how it converges is not.
4. Whether the NFR-8 scale bounds should shape the architecture before they are validated — PRD Open Question 4.
5. How FR-41 (moving a Task between Projects) interacts with the Status delta model. A move into a Project whose effective set lacks the Task's Status requires mapping on the same terms as FR-26, which means the move is not a simple reparenting — it is a reparent plus a conditional migration, and it must be atomic.
