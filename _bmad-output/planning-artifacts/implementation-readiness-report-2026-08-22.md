---
stepsCompleted: ['step-01-document-discovery', 'step-02-prd-analysis', 'step-03-epic-coverage-validation', 'step-04-ux-alignment', 'step-05-epic-quality-review', 'step-06-final-assessment']
status: final
readiness: 'READY — pre-Epic-1 edits applied 2026-08-22; Epic 1 clear to start'
issuesFound: 9
issuesClosed: 4
issuesOpen: 5
frCoverage: '43/43 (100%)'
nfr8BoundsEnforced: '6/6'
remediationApplied: 2026-08-22
documentsAssessed:
  prd:
    - _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md
    - _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/addendum.md
  architecture:
    - _bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md
  ux:
    - _bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md
    - _bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md
  epics:
    - _bmad-output/planning-artifacts/epics.md
  traceabilitySources:
    - _bmad-output/specs/spec-yello/SPEC.md
    - _bmad-output/specs/spec-yello/acceptance-criteria.md
    - _bmad-output/specs/spec-yello/role-capability-matrix.md
    - _bmad-output/specs/spec-yello/quality-budgets.md
    - _bmad-output/specs/spec-yello/surfaces-and-journeys.md
    - _bmad-output/specs/spec-yello/decisions-settled.md
    - _bmad-output/specs/spec-yello/harness-constraints.md
    - _bmad-output/specs/spec-yello/success-metrics.md
    - _bmad-output/specs/spec-yello/glossary.md
    - docs/bmad-coverage.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-08-22
**Project:** YelloBMAD

## Step 1: Document Inventory

Discovery ran across `_bmad-output/planning-artifacts` (the configured `planning_artifacts`
location), `_bmad-output/specs`, and `docs` (the configured `project_knowledge` location).

Note on discovery: this skill's documented search patterns (`{planning_artifacts}/*prd*.md`
and similar) match nothing in this project, because every producer skill writes into a dated
run folder (`prds/prd-YelloBMAD-2026-08-15/`, `architecture/architecture-YelloBMAD-2026-08-17/`,
`ux-designs/ux-YelloBMAD-2026-08-18/`). The inventory below was built from paths supplied
directly by the user plus a recursive sweep, not from the pattern globs.

### PRD

| File | Size | Modified | Status |
|---|---|---|---|
| `prds/prd-YelloBMAD-2026-08-15/prd.md` | 84.1 KB | 2026-08-20 16:08 | `final` |
| `prds/prd-YelloBMAD-2026-08-15/addendum.md` | 18.4 KB | 2026-08-20 16:08 | no frontmatter |

Sharded form: none. Supporting files not assessed: `reconcile-brainstorm-intent.md`,
`review-rubric.md`, `.memlog.md`.

### Architecture

| File | Size | Modified | Status |
|---|---|---|---|
| `architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md` | 47.0 KB | 2026-08-20 16:14 | `final` |

Declared scope: `Yello v1 — the whole system. FR-1…FR-43, NFR-1…NFR-9. Reconciled against
prd.md as revised 2026-08-20.` Binds PRD sections 4.1 through 4.11. Paradigm: vertical slices
inside a hexagonal shell.

Sharded form: none. Supporting files not assessed: `reviews/review-adversarial-seam.md`,
`reviews/review-rubric-walker.md`, `reviews/review-web-verification.md`, `walkthrough.html`,
`design-review.html`, `.memlog.md`.

### UX Design

| File | Size | Modified | Status |
|---|---|---|---|
| `ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md` | 37.2 KB | 2026-08-20 15:23 | `final` |
| `ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md` | 76.0 KB | 2026-08-20 16:20 | `final` |

Sharded form: none. Supporting files not assessed: 7 mockups under `mockups/`, 8 working
variants under `.working/`, `review-accessibility.md`, `review-isolation.md`,
`review-rubric.md`, `validation-report.md`, `validation-report.html`, `.memlog.md`.

### Epics and Stories

| File | Size | Modified | Status |
|---|---|---|---|
| `epics.md` | 209.8 KB | 2026-08-22 09:32 | no `status` field |

`stepsCompleted: [step-01-validate-prerequisites, step-02-design-epics, step-03-create-stories]`.
Declares 8 epics and 53 stories. Sharded form: none.

Its `inputDocuments` frontmatter lists exactly five files: `prd.md`, `addendum.md`,
`ARCHITECTURE-SPINE.md`, `DESIGN.md`, `EXPERIENCE.md`.

### Traceability sources added at Step 1

Not in the user's original path list; added with user approval because assessing the epics
only against documents they were generated from cannot reveal requirements that were never
carried across.

| File | Size | Modified |
|---|---|---|
| `_bmad-output/specs/spec-yello/SPEC.md` | 33.7 KB | 2026-08-18 17:06 |
| `_bmad-output/specs/spec-yello/acceptance-criteria.md` | 30.4 KB | 2026-08-18 16:14 |
| `_bmad-output/specs/spec-yello/harness-constraints.md` | 12.8 KB | 2026-08-18 17:07 |
| `_bmad-output/specs/spec-yello/surfaces-and-journeys.md` | 11.3 KB | 2026-08-18 13:39 |
| `_bmad-output/specs/spec-yello/decisions-settled.md` | 9.2 KB | 2026-08-18 13:46 |
| `_bmad-output/specs/spec-yello/quality-budgets.md` | 6.9 KB | 2026-08-18 13:43 |
| `_bmad-output/specs/spec-yello/success-metrics.md` | 5.5 KB | 2026-08-18 13:43 |
| `_bmad-output/specs/spec-yello/glossary.md` | 4.4 KB | 2026-08-18 13:30 |
| `_bmad-output/specs/spec-yello/role-capability-matrix.md` | 3.4 KB | 2026-08-18 13:31 |
| `docs/bmad-coverage.md` | 42.7 KB | 2026-08-22 11:07 |

`SPEC.md` declares itself sourced from `prd.md`, `addendum.md`,
`brainstorming/brainstorm-yello-mvp-scope-2026-08-15/brainstorm-intent.md` and
`docs/bmad-coverage.md`, and lists `ARCHITECTURE-SPINE.md` as a companion.

## Step 1 Findings

### Duplicates

None. No `index.md` exists anywhere under `_bmad-output`, so no document exists in both
whole and sharded form. No resolution required.

### Missing required documents

None. PRD, Architecture, UX and Epics are all present and all user-supplied paths resolve.

### Observations carried forward

1. **The epics were not built from the SPEC kernel.** `epics.md`'s `inputDocuments` omits
   `SPEC.md` and, more importantly, `acceptance-criteria.md` (30.4 KB). If the SPEC is the
   canonical contract it claims to be, its acceptance criteria are the sharpest available
   instrument for finding story-level gaps, and they have not yet been checked against the
   53 stories. Carried into epic coverage validation.

2. **`addendum.md` carries no frontmatter** and therefore no `status: final` marker, unlike
   every other assessed input. Its content is nonetheless cited as a source by the
   architecture spine, both UX documents, the SPEC and the epics.

3. **`epics.md` has no `status` field.** Its `stepsCompleted` array shows the produce
   workflow ran to completion, but the document carries no explicit final gate.

4. **`docs/bmad-coverage.md` is the most recently modified artifact** in the repository
   (2026-08-22 11:07, later than `epics.md` at 09:32), and is cited as a source by both the
   architecture spine and the SPEC. Treated as the authoritative coverage tracker.

## Step 2: PRD Analysis

Both PRD documents were read end to end: `prd.md` (885 lines) and `addendum.md` (170 lines).
No sharded PRD exists, so nothing was skipped.

The PRD numbers its requirements globally and states explicitly that the numbers are
identifiers, not positions — FR-42 sits inside §4.2 between FR-8 and FR-9, and FR-41 sits
inside §4.6 after FR-23, because both were added by later revisions. **All of FR-1 through
FR-43 are present with no gaps and no duplicates** (verified by enumeration below: 3+7+5+2+2+6+4+3+4+4+3 = 43).

### Functional Requirements

Requirement statements are quoted verbatim. "Cons." is the count of `Consequences (testable)`
bullets the PRD attaches to that FR — these are the acceptance-criteria-level obligations that
epic coverage must account for, so the count is carried forward as the denominator for Step 3.

#### §4.1 Accounts and Authentication (realizes UJ-1, UJ-3)

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-1 | "A person can create an Account with an email address and a password. The email address uniquely identifies the Account across Yello." | UJ-1, UJ-3 | 4 |
| FR-2 | "An Account can authenticate and receive a Session that persists across requests and across every Space it belongs to." | UJ-4 | 4 |
| FR-3 | "An Account can delete itself. Deletion must not orphan a Space or destroy work belonging to other Accounts." | UJ-8 | 5 |

Feature-specific NFR: "Passwords are never stored recoverably, and are never included in any
log, error message or API response."

#### §4.2 Spaces (realizes UJ-1, UJ-2, UJ-4, UJ-8)

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-4 | "The system creates a Space for a newly registered Account and makes that Account its Owner." | UJ-1, UJ-3 | 3 |
| FR-5 | "An authenticated Account can create additional Spaces without limit." | UJ-2, UJ-4 | 4 |
| FR-6 | "An Owner or Admin can rename a Space." | — | 2 |
| FR-7 | "An Owner can delete a Space, destroying its Projects, Tasks, Memberships and Invitations." | — | 5 |
| FR-8 | "An Owner can offer ownership of a Space to another Membership in that Space. Ownership does not move until the offer is accepted (FR-42)." | UJ-8 | 8 |
| FR-42 | "The Membership an Ownership Offer names can accept it, becoming Owner, or decline it." | UJ-8 | 6 |
| FR-9 | "An Account can move between the Spaces it holds Membership in; the active Space determines everything subsequently visible and permitted." | UJ-4 | 3 |

#### §4.3 Membership and Invitations (realizes UJ-2, UJ-3, UJ-6)

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-10 | "An Owner or Admin can invite an email address to a Space at a specified Role." | UJ-2 | 7 |
| FR-11 | "An invited person can accept, gaining Membership at the Role the Invitation specified." | UJ-3 | 7 |
| FR-12 | "An Owner or Admin can revoke an Invitation that has not been accepted." | — | 3 |
| FR-13 | "An Owner or Admin can change the Role of a Membership within the constraints of their own Role." | UJ-6 | 4 |
| FR-14 | "An Owner or Admin can remove a Membership; any Account can remove its own." | UJ-6, UJ-8 | 6 |

#### §4.4 Access Control (realizes UJ-3, UJ-4, UJ-6)

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-15 | "Every read and write of Space-scoped data is authorised against the acting Account's Membership in the Space that owns the data." | UJ-3, UJ-4 | 5 |
| FR-16 | "Each Role grants a fixed, Space-local set of capabilities." | UJ-4, UJ-6 | 3 + 15-row matrix |

FR-16's matrix is declared "the single source of truth for Role capability", with 15 capability
rows across Owner / Admin / Member / Viewer. Where an individual FR's restatement disagrees
with the matrix, the PRD states the matrix is correct and the restatement is a defect.

#### §4.5 Projects

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-17 | "An Owner, Admin or Member can create, rename and delete Projects within their active Space." | UJ-1 | 4 |
| FR-18 | "Any Membership can list the Projects in its active Space." | — | 2 |

#### §4.6 Tasks (realizes UJ-1, UJ-5)

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-19 | "An Owner, Admin or Member can create a Task within a Project." | UJ-1 | 3 |
| FR-20 | "An Owner, Admin or Member can change a Task's title, Status, due date and Labels." (Description editing is FR-31.) | — | 2 |
| FR-21 | "An Owner, Admin or Member can assign a Task to a Membership in the same Space." | — | 5 |
| FR-22 | "An Owner or Admin can define the Labels available in a Space; an Owner, Admin or Member can apply them to Tasks." | — | 2 |
| FR-23 | "An Owner, Admin or Member can delete a Task." | — | 2 |
| FR-41 | "An Owner, Admin or Member can move a Task to a different Project within the same Space." | — | 6 + 6 bulk |

FR-41 carries a distinct **bulk form**: "Every Task currently in one Status can be moved from
one Project to another in the same Space in a single operation" — atomic, reachable from a Board
column and from a List View filtered to one Status, and available on the API on the same terms
as the single-Task form. Mixed-Status selections are explicitly out of scope (§9.2).

#### §4.7 Status Configuration

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-24 | "An Owner or Admin can define the ordered default Status set for a Space." | — | 3 |
| FR-25 | "An Owner or Admin can add, remove, rename and reorder Statuses within a Project, expressed as a delta over the Space defaults." | — | 5 |
| FR-26 | "Removing a Status requires every Task occupying it to be mapped to another Status in the same operation." | — | 4 |
| FR-27 | "Changes to the Space default set reach every Project according to that Project's delta." | — | 10 |

FR-27 is the heaviest single requirement in the PRD by consequence count. Its removal half
requires a Space-wide destination Status **plus** a per-Project destination for any Project
whose post-removal effective set cannot accept it, reported and asked rather than guessed, all
applied as one transaction. The addendum records this as a new obligation: "FR-27's removal
endpoint now carries a per-Project destination map rather than a single value."

#### §4.8 Board and List Views (realizes UJ-1, UJ-4)

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-28 | "Any Membership can view a Project's Tasks as columns ordered by the Project's effective Status set." | — | 4 |
| FR-29 | "An Owner, Admin or Member can move a Task between columns, changing its Status, and reorder Tasks within a column." | — | 4 |
| FR-30 | "Any Membership can view a Project's Tasks as rows, filtered and sorted by Status, Assignee, due date and Label." | — | 3 |

Both FR-28 and FR-30 carry an explicit unresolved collision: at the NFR-8 bound of 5,000 Tasks
per Project they must still satisfy NFR-5 and NFR-9, and the PRD states "the three requirements
cannot all hold naively" and that "nothing in this document provides it". The addendum repeats
this as an open obligation on the architecture: "Nothing currently pages or virtualises."

#### §4.9 Collaborative Task Editing (realizes UJ-5, UJ-6)

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-31 | "Multiple Users with write capability in a Space can edit the same Task description simultaneously." | UJ-5 | 4 |
| FR-32 | "Users editing or viewing the same Task can see who else is there." | UJ-5 | 3 |
| FR-33 | "A User who loses connectivity mid-edit and returns has their local changes reconciled rather than lost or duplicated." | UJ-5 | 3 |
| FR-34 | "A change to a Membership's Role, or its removal, takes effect on that Account's open editing sessions without requiring re-authentication." | UJ-6 | 5 |

FR-34 carries an explicit `[NOTE FOR PM]`: "FR-34 is the acceptance criterion this product
should be judged on. If everything else works and this does not, the isolation model is
decorative." Carried into Step 3 as the highest-priority coverage check.

#### §4.10 Public API (realizes UJ-7)

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-35 | "An authenticated caller can read and write Projects and Tasks in the Space its Token is bound to." | UJ-7 | 3 |
| FR-36 | "Any Membership can issue an API Token for the Space that Membership is in." | UJ-7 | 5 |
| FR-37 | "The API is versioned, and a consumer written against one version continues to work when a newer one ships." | UJ-7 | 4 |
| FR-38 | "The API limits request rate per Token." | — | 3 |

FR-35 records the single deliberate browser/API asymmetry: a Task's position within its Status
is readable over the API but not writable.

#### §4.11 Notifications (realizes UJ-2, UJ-3, UJ-8)

| FR | Requirement (verbatim) | Realizes | Cons. |
|---|---|---|:--:|
| FR-39 | "Issuing an Invitation sends an email to the invited address containing a means of accepting it." | UJ-2, UJ-3 | 5 |
| FR-40 | "An Account assigned to a Task is notified." | — | 3 |
| FR-43 | "Issuing an Ownership Offer sends an email to the named recipient's Account." | UJ-8 | 5 |

Feature-specific NFR: "A record that a notification was sent is retained — Space, kind and
timestamp, never message content or recipient address — so §10's SM-C4 is derivable. No
product surface reads it."

**Total FRs: 43** (FR-1 … FR-43, complete, no gaps).
**Total testable consequences: 187**, plus FR-16's 15-row capability matrix.

### Non-Functional Requirements

Nine cross-cutting NFRs (§5), each written to be failable. Clauses are quoted or tightly
paraphrased where the PRD uses bullets.

| NFR | Statement | Measurable clauses |
|---|---|---|
| NFR-1 | **Isolation is absolute.** "No data belonging to a Space reaches any Account without a Membership in that Space, by any route." | Holds for browser and API identically, and for reads, writes, listings, aggregates, search results, notifications and error messages. Holds for identifiers. Holds under error, timeout and partial response. **"This is the one requirement with no acceptable failure rate. A single verified cross-Space disclosure blocks release."** |
| NFR-2 | **Authorisation is evaluated fresh, per request.** "No authorisation decision is served from a cache that could outlive the Membership it was derived from." | Request path: reflected on the very next request, **no tolerance**. Live-session path: **within 1 second** of the transaction boundary, without the affected Account acting. FR-34's guarantee independent of both timings. No request authorised using a Role from a previous active Space. Applies to API Tokens identically. |
| NFR-3 | **Collaborative editing feels immediate.** | Local edit renders within **16 ms** with no network round trip. Remote edit renders within **300 ms p95** at 50 ms RTT. Presence appears within **2 s** of arrival, disappears within **10 s** of departure. |
| NFR-4 | **Concurrent edits converge.** | Identical text across participants within **2 s** of the last edit. Holds for at least **10 simultaneous editors** on one Task description. A participant disconnected up to **5 minutes** reconciles without loss or duplication. |
| NFR-5 | **The API is predictable.** | Reads within **300 ms**, writes within **500 ms**, both **p95**, server-side, within NFR-8. Every refusal carries a machine-readable reason. Retrying a timed-out write does not apply it twice. |
| NFR-6 | **Credentials are held safely.** | Passwords via a deliberately slow one-way function, never recoverable, work factor tunable without re-registering Accounts. API Tokens stored so a datastore read yields no usable Token; displayed exactly once. No password or Token in any log, error, notification, analytics event or API response. All traffic encrypted in transit. **Encryption at rest explicitly not required.** |
| NFR-7 | **Refusals are observable.** | Every authorisation refusal recorded with acting Account, target Space, capability attempted, outcome. Cross-Space attempts distinguishable from within-Space permission failures. Retained `[ASSUMPTION: 90 days]`. |
| NFR-8 | **Scale envelope.** | Spaces per Account **50**; Memberships per Space **100**; Projects per Space **50**; Tasks per Project **5,000**; Concurrent editors per Task **10**; Concurrent active Sessions per Space **50**. "Exceeding a bound must degrade visibly rather than silently — a refusal, not a wrong answer. **A bound that is not enforced is a defect, not a relaxation.**" Confirmed final for v1; verification scheduled at the NFR-evidence audit. |
| NFR-9 | **The primary flows are accessible.** | Registration, Space switching, the Board, the Task editor and the invitation flow meet **WCAG 2.1 AA**. Every Board operation available by pointer is available by keyboard, **including moving a Task between columns**. Presence and permission-change notices announced to assistive technology, not conveyed by colour or position alone. |

**Total NFRs: 9 cross-cutting + 2 feature-specific** (§4.1 password storage, §4.11 notification
send record).

### Additional Requirements

Requirements and constraints that are binding but carry no FR/NFR number. Each is a coverage
obligation in its own right, and none of them is numbered — which makes them the likeliest to
be missed by an epic breakdown that traces only FR/NFR identifiers.

**§6.1 Privacy (5 constraints)** — Account existence never disclosed to anyone not given it
(FR-1, FR-2, FR-10). Memberships visible only within each Space; nobody can enumerate another
Account's Spaces, **including a Space's Owner**. Email addresses visible only to Owners and
Admins of Spaces the Account is a Member of. No behavioural analytics on Space contents. **No
product surface aggregates across Spaces** — explicitly rules out an in-product metrics
dashboard, an admin analytics view, and any endpoint returning a cross-Space count.

**§6.2 Data lifecycle (4)** — Deletion of Account, Space, Project or Task irreversible in v1;
no trash, no restore. Deleting an Account never deletes another Account's work. Deleting a Space
deletes contents for every Member, **and this is stated at the point of the action**. Backups
exist for disaster recovery but are not a restore path `[ASSUMPTION]`.

**§6.3 Cost (3)** — Total running cost under **£30/month** at NFR-8 scale. No design requiring
always-on dedicated infrastructure per Space or per active editing session. NFR-3/NFR-4
real-time requirements must be satisfied within a single modest deployment.

**§6.4 Data protection — a testable gate, not a backlog.** v1 is a single-operator deployment
claiming no data-protection posture. "**The first Account created by anyone other than the
operator makes this document non-compliant until amended.**" Five prerequisites at the gate:
lawful basis, stated data region with no replication outside it, encryption at rest asserted,
a breach-notification position, a subject-access or export route. This is a release-blocking
condition expressed in prose with no FR attached to it.

**§7 Information Architecture (8 surfaces)** — Unauthenticated; Space context bar (always
present, and functional rather than decorative because it establishes authorisation context);
Space home; Project (Board default + List View); Task detail; Space settings (Owner/Admin only);
Project settings (Owner/Admin only); Account settings (the only surface that spans Spaces). Plus
the binding rule: "The acting Role must be legible from the interface at all times, and
capabilities the Role lacks are **absent rather than present-and-failing**."

**§8 Non-Goals (6)** — Not an organisation-management product (rules out enterprise SSO and
directory sync; does not rule out OAuth sign-in). Not formal project management. Not a
communication tool. **Does not federate** — no cross-Space views, aggregate dashboards or "all
my Tasks everywhere", stated as a model contradiction rather than a deferral. Not offline-first.
No public or anonymous access — no share link, no public Board, no read-only URL.

**§9.2 Out of scope for MVP (15 items)** — Iteration planning; Task comments and activity
history (flagged `[NOTE FOR PM]` as "the most frequently missed of these"); subtasks and
relationships; attachments; cross-Project search; webhooks and outbound integrations; custom
fields; recurring Tasks; enterprise SSO/directory sync/domain-restricted invitation; OAuth
sign-in (deferred with a design constraint attached — "Implement FR-1, FR-2 and NFR-6 so those
can change without redesign"); mobile applications; notification preferences; session telemetry;
bulk Task move across mixed Statuses; billing and plan limits.

**§10 Success Metrics — two gating metrics that block release.**

- **SM-1 Isolation integrity:** zero verified cross-Space disclosures across browser and API in
  any released build, "measured by an isolation test suite exercised on every change". Validates
  FR-15, FR-16, FR-35, FR-36, NFR-1. **This mandates a test suite as a deliverable.**
- **SM-2 Revocation latency:** permission changes govern the next request with no tolerance and
  take effect on open live sessions within 1 second, in **100% of tested cases** — "including
  sessions holding unsynchronised local edits". Validates FR-34, NFR-2.

Four behavioural metrics (SM-3 … SM-6) and four counter-metrics (SM-C1 … SM-C4) carry no
thresholds but impose **three retention obligations** that are carried as requirements: the
Invitation record keeps its terminal state (FR-10), a notification send record is kept (FR-40),
and **"compaction of the Task description change log must preserve per-author change counts and
timestamps — the last of these is an obligation on the architecture, which currently does not
guarantee it."** SM-C2 is defined but explicitly not measurable in v1.

**§12 Assumptions Index (13 assumptions, all unconfirmed).** Four are marked † as having
hardened into architecture and now costing more than a document edit to reverse: FR-7 Space
deletion irreversible; FR-17 Project deletion irreversible; FR-37 URL path versioning with
exactly two concurrent versions; NFR-7 90-day refusal-record retention.

**Addendum §7 — four new obligations on the architecture**, raised by the 2026-08-18 revision
and carried forward here because each is a coverage obligation with no FR of its own:

1. "AD-13 must guarantee what compaction preserves… Per-author change counts and timestamps
   must, or §10's SM-5 becomes underivable and unrecoverably so."
2. "FR-42 needs an `OwnershipOffer` entity and four slices — offer, accept, decline, revoke."
   Plus: "AD-10 forbids unconditional timers, so the 7-day offer expiry must be evaluated
   lazily on read, exactly as FR-39's expiry already must be."
3. "FR-27's removal endpoint now carries a per-Project destination map rather than a single value."
4. "FR-28 and FR-30 must hold NFR-5 and NFR-9 at 5,000 Tasks. Nothing currently pages or
   virtualises, and the three requirements cannot all hold naively."

**Addendum §8 — nine proposals explicitly rejected** in the 2026-08-18 revision, recorded "so
downstream does not re-propose them". Notably: telling a removed Account its access ended
(rather than an undifferentiated not-found); an in-product metrics endpoint or dashboard; a
subject-access/data-export capability; lowering Tasks per Project from 5,000 to ~500; a
mixed-selection bulk Task move. An epic that reintroduces any of these is a defect, not a
feature.

### PRD Completeness Assessment

**The PRD is unusually complete and internally disciplined.** Findings from the extraction:

**Strengths material to readiness:**

- **Numbering is stable and gapless.** FR-1 … FR-43 with no holes, and the document states the
  numbers are identifiers rather than positions — so out-of-sequence placement of FR-41 and
  FR-42 is intentional, not a defect. Downstream artifacts can trace against these safely.
- **Requirements are written to be failable.** §5 opens by rejecting sentiment ("a requirement
  that cannot be violated by a plausible implementation is not a requirement"), and NFR-2 was
  demonstrably rewritten after a review found its 5-second budget unfailable. Every NFR carries
  a number.
- **187 testable consequences** give Step 3 a real denominator rather than requiring
  interpretation of prose intent.
- **Known-hard requirements are named as such** rather than left for the epics to discover:
  FR-34 carries a `[NOTE FOR PM]` identifying it as the criterion the product should be judged
  on, and FR-28/FR-30 state their own unresolvability at the NFR-8 bound.
- **A vocabulary discipline is enforced** (§2, "synonyms are a discipline violation"), which
  makes cross-document traceability checkable by term rather than by inference.
- **Rejected alternatives are recorded** (addendum §8), so re-proposal is detectable.

**Gaps and risks carried into Step 3:**

1. **Unnumbered obligations are the primary coverage risk.** The §6 constraints, the §6.4 data
   protection gate, the §7 IA rules, the §10 gating metrics with their test-suite mandate, the
   three retention obligations, and the addendum's four architecture obligations are all
   binding and **none carries an FR or NFR identifier**. An epic breakdown that traces
   FR-1…FR-43 and NFR-1…NFR-9 will report complete coverage while missing every one of them.
   This is the specific failure mode Step 3 must test for.
2. **Two requirements are stated as unsatisfiable-as-written.** FR-28 and FR-30 against NFR-5
   and NFR-9 at 5,000 Tasks. The PRD defers the resolution to the architecture explicitly.
   Whether the epics carry a story for paging/virtualisation is a direct readiness question.
3. **One retention obligation is stated as not yet guaranteed.** Per-author change counts and
   timestamps surviving compaction — the PRD says the architecture "currently does not
   guarantee it", and SM-5 becomes "underivable and unrecoverably so" without it.
4. **All 13 assumptions remain unconfirmed,** four of them already hardened into architecture.
   The PRD asks for explicit confirmation in §12 and it has not been given.
5. **§6.4 is a release-blocking gate with no owner.** "The first Account created by anyone
   other than the operator makes this document non-compliant until amended." Nothing in the
   FR set enforces or detects this condition.
6. **SM-1 mandates an artifact, not just a property** — "an isolation test suite exercised on
   every change". That is a deliverable an epic must own.
7. **`addendum.md` carries no frontmatter and no status marker**, yet is cited as a source by
   the architecture spine, both UX documents, the SPEC and the epics. Its content is
   load-bearing — it holds the mechanism constraint that Status deltas must key by identity
   rather than name, without which FR-27's cascade cannot be implemented correctly.

No FR or NFR is missing, ambiguous, or self-contradictory in a way that blocks implementation
planning. The PRD is fit to validate epics against.

## Step 3: Epic Coverage Validation

`epics.md` was read in full (2,759 lines, 8 epics, 53 stories). It is not a bare epic list: it
carries its own **Requirements Inventory** restating all 43 FRs, all 9 NFRs, 2 feature-specific
NFRs given stable ids (FS-NFR-1, FS-NFR-2), both gating success metrics, the three retention
obligations, **AR-1 … AR-40** distilled from the architecture spine, and **UX-DR1 … UX-DR42**
distilled from the two UX documents — followed by an **FR Coverage Map** and a
**Story Coverage Index**.

### Coverage Matrix

Every FR is assigned to exactly one epic. I verified this three ways independently — the FR
Coverage Map, each epic's own `FRs covered:` list, and the Story Coverage Index — and all three
agree with each other and with the PRD.

| FR | PRD requirement (abbreviated) | Epic | Story | Status |
|---|---|:--:|---|---|
| FR-1 | Register an Account | 1 | 1.3 | ✓ Covered |
| FR-2 | Authenticate and hold a Session | 1 | 1.4 | ✓ Covered |
| FR-3 | Delete an Account | 5 | 5.4 | ✓ Covered |
| FR-4 | Provision a Personal Space on registration | 1 | 1.3 | ✓ Covered |
| FR-5 | Create a Space | 3 | 3.1 | ✓ Covered |
| FR-6 | Rename a Space | 3 | 3.1 | ✓ Covered |
| FR-7 | Delete a Space | 3 | 3.3 | ✓ Covered |
| FR-8 | Offer to transfer ownership | 5 | 5.1 | ✓ Covered |
| FR-9 | Establish and switch Space context | 1 | 1.7 | ✓ Covered |
| FR-10 | Issue an Invitation | 4 | 4.1 | ✓ Covered |
| FR-11 | Accept an Invitation | 4 | 4.3 | ✓ Covered |
| FR-12 | Revoke a pending Invitation | 4 | 4.2 | ✓ Covered |
| FR-13 | Change a Membership's Role | 4 | 4.4 (+7.7, 7.8) | ✓ Covered |
| FR-14 | Remove a Membership, or leave a Space | 4 | 4.5 (+7.7, 7.8) | ✓ Covered |
| FR-15 | Enforce Space-scoped authorisation | 1 | 1.5, 1.6 | ✓ Covered |
| FR-16 | Apply the Role capability matrix | 1 | 1.6, 4.7 | ✓ Covered |
| FR-17 | Create, rename, delete a Project | 2 | 2.1, 2.5 | ✓ Covered |
| FR-18 | List Projects in a Space | 2 | 2.1 | ✓ Covered |
| FR-19 | Create a Task | 2 | 2.2 | ✓ Covered |
| FR-20 | Edit Task attributes | 2 | 2.3 | ✓ Covered |
| FR-21 | Assign a Task | 4 | 4.6 | ✓ Covered |
| FR-22 | Manage Labels | 2 | 2.4 | ✓ Covered |
| FR-23 | Delete a Task | 2 | 2.5 (+7.4) | ✓ Covered |
| FR-24 | Define Space default Statuses | 6 | 6.3, 6.4 | ✓ Covered |
| FR-25 | Apply a Project Status delta | 6 | 6.1, 6.2 | ✓ Covered |
| FR-26 | Map Tasks when a Status is removed | 6 | 6.2 | ✓ Covered |
| FR-27 | Propagate Space-level Status changes | 6 | 6.3, 6.4 | ✓ Covered |
| FR-28 | View a Project as a Board | 2 | 2.2, 2.9 | ✓ Covered |
| FR-29 | Move and order Tasks on a Board | 2 | 2.6 | ✓ Covered |
| FR-30 | View a Project as a filterable list | 2 | 2.10 (+4.6) | ✓ Covered |
| FR-31 | Edit a Task description concurrently | 7 | 7.2, 7.4 | ✓ Covered |
| FR-32 | Show Presence | 7 | 7.5 | ✓ Covered |
| FR-33 | Reconcile after disconnection | 7 | 7.6 | ✓ Covered |
| FR-34 | Apply permission changes to live sessions | 7 | 7.7, 7.8 | ✓ Covered |
| FR-35 | Expose Spaces, Projects, Tasks over the API | 8 | 8.1 | ✓ Covered |
| FR-36 | Issue and scope an API Token | 1 | 1.8 | ✓ Covered |
| FR-37 | Version the API and deprecate predictably | 8 | 8.2 | ✓ Covered |
| FR-38 | Rate limit API requests | 8 | 8.3 | ✓ Covered |
| FR-39 | Deliver an Invitation by email | 4 | 4.1 | ✓ Covered |
| FR-40 | Notify on assignment | 4 | 4.6 | ✓ Covered |
| FR-41 | Move a Task to another Project (+ bulk) | 6 | 6.5, 6.6 | ✓ Covered |
| FR-42 | Accept or decline an Ownership Offer | 5 | 5.2, 5.3 | ✓ Covered |
| FR-43 | Deliver an Ownership Offer by email | 5 | 5.1 | ✓ Covered |

**Distribution:** Epic 1 → 7 FRs · Epic 2 → 9 · Epic 3 → 3 · Epic 4 → 8 · Epic 5 → 4 ·
Epic 6 → 5 · Epic 7 → 4 · Epic 8 → 3. Sums to 43, each FR assigned exactly once, union equals
FR-1 … FR-43 with no gaps, no duplicates and no orphans (no FR appears in the epics that is not
in the PRD).

**Three requirements complete outside their mapped epic, and the document says so in both
places rather than leaving it to be discovered** — FR-30's Assignee dimension in Story 4.6
(no Task holds an Assignee until FR-21); FR-23's editing-session termination in Story 7.4 (no
session exists before Epic 7); FR-13 and FR-14's live-session clauses in Stories 7.7 and 7.8,
with the request-path clause explicitly noted as fully met in Epic 4. This is exactly the
disclosure that prevents a partially-implemented FR from being marked done.

### Coverage of the non-FR obligations

This is where an epic breakdown usually fails, and where this one largely does not.

| Obligation class | Count | Covered | Verified against |
|---|:--:|:--:|---|
| Cross-cutting NFRs | 9 | 9 | NFR-1 (1.5, 1.9, 3.2, 3.4, 8.1) · NFR-2 (4.4, 7.3, 7.7, 7.8) · NFR-3 (7.4, 7.5) · NFR-4 (7.1, 7.4, 7.6) · NFR-5 (2.9, 8.1, 8.3) · NFR-6 (1.3, 1.8) · NFR-7 (1.6) · NFR-8 (2.9, and see gap below) · NFR-9 (1.2, 2.8, 2.9, 2.11) |
| Feature-specific NFRs | 2 | 2 | FS-NFR-1 (1.3) · FS-NFR-2 (4.6) |
| Gating success metrics | 2 | 2 | SM-1 (1.9, extended 3.4, parity swept 8.1) · SM-2 (7.8) |
| Retention obligations | 3 | 3 | Invitation terminal state (4.2) · notification send record (4.6) · compaction preserving per-author counts (7.2) |
| Architecture requirements | 40 | 40 | AR-1 … AR-40 each cited by at least one story; AR-40's three deferrals land in 7.1 (a), 2.9 (b), 1.10 (c) |
| UX design requirements | 42 | 42 | UX-DR1 … UX-DR42 each cited by at least one story |
| Addendum architecture obligations | 4 | 4 | Compaction (7.2) · `OwnershipOffer` + four slices (5.1, 5.2, 5.3) · FR-27 per-Project destination map (6.4) · FR-28/FR-30 paging at 5,000 (2.9, 2.10) |
| PRD §6 constraint blocks | 4 | 3 | §6.1 covered in substance via AR-28, AR-29, UX-DR34 · §6.2 cited in 3.3 · §6.3 cited in 1.10 · **§6.4 absent** |

The nine proposals the addendum §8 records as rejected are all still rejected — no story
reintroduces a removed-Account tombstone, an in-product metrics surface, a mixed-Status bulk
move, a lowered Task bound, or the 5-second revocation budget. Story 6.6 and Story 5.3 each
record their rejection reasoning inline.

### Missing Requirements

Two gaps survive verification. Neither is an uncovered FR.

#### Gap 1 — PRD §6.4's data-protection gate has no home anywhere in the plan (High)

**Requirement text (PRD §6.4):** "The first Account created by anyone other than the operator
makes this document non-compliant until amended." Five prerequisites become "prerequisites for
continued use, not a backlog" at that moment: a lawful basis for holding email addresses and
authored content; a stated data region with no replication outside it; encryption at rest
asserted; a breach-notification position; a subject-access or export route.

**Evidence of absence.** Searched `epics.md` for `6.4`, `data protection`, `lawful basis`,
`breach`, `subject-access`, `data region` and `encryption at rest`. The only hit is NFR-6's
restatement that "Encryption at rest is explicitly **not** required here" — which is the
*pre-gate* position, carried forward without the gate that revokes it. §6.2 and §6.3 are both
cited by stories (3.3 and 1.10); §6.4 is cited by none, and appears in neither the Requirements
Inventory nor the Additional Requirements list.

**Impact.** This is not a v1 implementation gap — v1 is a single-operator deployment and the
prerequisites genuinely do not apply yet. The defect is that **the trigger condition is
untracked**. The PRD deliberately wrote this as a testable gate rather than an aspiration, and
the plan has silently converted it back into an aspiration by not recording it. Nothing in 53
stories detects, blocks, or even names the event that makes the product non-compliant. The
addendum compounds this: it holds a subject-access capability "behind the §6.4 gate", so the
plan inherits the deferral while dropping the gate the deferral hangs on.

**Recommendation.** Epic 1, Story 1.10 (`Ship it to Azure…`) is the right home — it already
owns §6.3's cost ceiling and the operational alerting. Add an acceptance criterion that the
§6.4 gate is recorded as an operational condition, with the five prerequisites named. A
one-line assertion that the deployment is single-operator, plus the condition that ends that
state, is enough. Alternatively file it as an explicit out-of-scope decision — but as a
decision, not as silence.

#### Gap 2 — Two of NFR-8's six bounds have no enforcing refusal (Medium)

NFR-8 states: "Exceeding a bound must degrade visibly rather than silently — a refusal, not a
wrong answer. **A bound that is not enforced is a defect, not a relaxation.**" AR-30 makes this
concrete: "Every NFR-8 bound is declared in one place and checked by the pipeline, not the
slice, with a machine-readable refusal inside the same transaction as the creation it refuses."

| NFR-8 bound | Enforcing story | Status |
|---|---|---|
| Spaces per Account — 50 | 3.1 ("attempts to create a 51st → refused inside the same transaction") | ✓ |
| Projects per Space — 50 | 2.1 ("a 51st is attempted → refused… copy states the bound") | ✓ |
| Tasks per Project — 5,000 | 2.2 ("a 5,001st is attempted → refused… machine-readable reason") | ✓ |
| Concurrent active Sessions per Space — 50 | 1.6 (named as the bound registry's first entry) | ✓ |
| **Memberships per Space — 100** | **none** | ❌ **MISSING** |
| **Concurrent editors per Task — 10** | **none** | ❌ **MISSING** |

**Memberships per Space (100).** Story 4.4 touches the bound but only as *pagination* — "it
pages rather than growing unbounded at the 100-Membership bound." That is a rendering
obligation, not the refusal NFR-8 requires. The creation that would breach the bound is
accepting an Invitation (Story 4.3), or arguably issuing one (Story 4.1); neither story carries
AR-30 in the Story Coverage Index and neither has a bound-refusal acceptance criterion.
Notably **UX-DR35 already specifies the user-facing state** — "scale bound reached ('This Space
has 100 Memberships, the maximum.' — visible, never silent)" — but UX-DR35 is mapped only to
Stories 2.1 and 7.6, neither of which concerns Memberships. The copy exists with nothing to
trigger it.

**Concurrent editors per Task (10).** Story 7.1 asserts convergence *holds for at least* 10
simultaneous editors, which is the NFR-4 obligation. Nothing refuses an 11th. Whether this
bound should refuse at all is a legitimate design question — refusing to open a Task is harsher
than degrading — but NFR-8's own wording does not leave that open, and the PRD says exceeding a
bound must produce "a refusal, not a wrong answer."

**Impact.** Both are enforceable-by-construction misses rather than deep design problems: the
bound registry that Story 1.6 establishes is the correct mechanism and already exists, so each
gap is one registry entry plus one acceptance criterion. Left alone, the first is reachable in
ordinary use at NFR-8's own stated scale — a Space accumulating its 101st Membership — and by
NFR-8's definition that is a defect.

**Recommendation.** Add the Membership bound refusal to Story 4.3 (acceptance is the
transaction that creates the Membership), cite AR-30 and UX-DR35 there, and re-map UX-DR35 to
include it. For the editor bound, either add a refusal to Story 7.3 (lease establishment is the
admission point) or record an explicit decision that this bound is a convergence guarantee
rather than a refusal — and if the latter, say so in the epics rather than leaving the
asymmetry undocumented.

### Observations that are not gaps

Recorded because each looked like a gap until checked.

1. **`UJ-9` is referenced but is not a PRD journey.** Epic 6's description ends "UJ-9." in both
   the Epic List and the epic body, and UX-DR36 says a zero-Task Project confirmation "is
   UJ-9's payoff". PRD §3.3 defines UJ-1 … UJ-8 only. The reference is therefore inherited from
   the UX spine, which §3.3 anticipated ("If a UX document is produced later it should mirror
   these IDs"). Carried into Step 4 for verification against `EXPERIENCE.md` — it is a
   traceability question, not a coverage gap, provided UJ-9 is genuinely defined there.
2. **FR-6's surface is an acknowledged inherited gap, correctly handled.** The FR Coverage Map
   and Epic 3 both state that PRD §7 omits where Space rename lives and that the UX spine
   placed it in Space settings. Disclosed rather than papered over.
3. **All 13 of PRD §12's assumptions are implemented as acceptance criteria** — Todo / In
   Progress / Done (1.3, 6.3), the 7-day expiries (4.1, 5.1), 90-day refusal retention (1.6),
   URL-path versioning with two concurrent versions (8.2), Admins not modifying each other
   (4.4), immediate irreversible deletion (2.5, 3.3). Coverage is complete; **confirmation is
   not**. The PRD asks for explicit confirmation in §12 and the epics harden all thirteen
   without it. This is a process risk carried to the final assessment, not a coverage defect.
4. **NFR-6's "all traffic is encrypted in transit" is not asserted as its own criterion.** It is
   implied by Story 1.4's `Secure` cookie and Story 1.10's Azure deployment, and is a platform
   property rather than application behaviour. Noted, not raised.
5. **FR-24's epic reassignment is reasoned, not drifted.** The map records that FR-24 moved from
   Epic 2 to Epic 6 during story creation because defining a default Status set includes
   removing one, which requires FR-26's mapping — a forward dependency. Epic 1's provisioning
   seeds the set instead, which is also what UJ-1 requires.
6. **File-churn overlap was assessed rather than ignored** — four overlap patterns examined,
   each judged incidental with consolidation explicitly considered and rejected on stated
   grounds.

### Coverage Statistics

- **Total PRD FRs: 43**
- **FRs covered in epics: 43**
- **FR coverage: 100%**
- FRs in epics but not in the PRD: 0
- NFRs covered: 9/9 cross-cutting, 2/2 feature-specific — **100%**
- Gating success metrics covered: 2/2 — **100%**
- Retention obligations covered: 3/3 — **100%**
- Architecture requirements (AR-1 … AR-40) covered: 40/40 — **100%**
- UX design requirements (UX-DR1 … UX-DR42) covered: 42/42 — **100%**
- PRD §6 constraint blocks covered: 3/4 — **75%** (§6.4 absent)
- NFR-8 bounds with an enforcing refusal: 4/6 — **67%**
- Stories: 53 across 8 epics; 42 carry at least one FR, 11 deliver foundation, verification or
  an NFR-9 obligation and are marked as such

## Step 4: UX Alignment Assessment

### UX Document Status

**Found.** Two documents, both `status: final`, both updated 2026-08-20, and split by concern
rather than duplicated:

- `DESIGN.md` (37.2 KB) — visual identity. Tokens carry literal values in frontmatter because
  Yello inherits no UI system; includes a fully computed contrast table for both themes.
- `EXPERIENCE.md` (76.0 KB) — information architecture, behaviour, states, interactions,
  accessibility and journeys.

Both declare the same three sources (PRD, addendum, architecture spine) and both state that the
spines "win on conflict with any mock, wireframe or import" — the mockups are explicitly
illustrative and were rendered before this pass's border, type-scale and `readonly` corrections.
Supporting artifacts (`validation-report.md`, `review-accessibility.md`, `review-isolation.md`,
`review-rubric.md`) were not treated as contract.

### UX ↔ PRD Alignment

**Aligned, and demonstrably reconciled rather than merely parallel.**

- **User journeys match.** `EXPERIENCE.md` states "Journeys **UJ-1 … UJ-8 mirror PRD §3.3** —
  IDs, protagonists and edge cases follow the source; two titles are lightly adjusted for
  glossary discipline." Spot-checked UJ-1, UJ-8 and UJ-9 against PRD §3.3: protagonists,
  paths, climaxes and edge cases carry across intact.
- **UJ-9 resolves the Step 3 observation.** It is declared new — "added to close a
  surface-closure gap: Project settings had no journey landing on it" — and is fully specified
  as an eight-step flow with its failure case. PRD §3.3 anticipated exactly this ("If a UX
  document is produced later it should mirror these IDs"). Epic 6 references it legitimately,
  and Stories 6.5 and 6.6 implement it. **Not a defect.**
- **Vocabulary is PRD §2 verbatim,** with the no-synonyms rule extended per language — "a
  translator who renders 'Space' three ways breaks the product's central concept." The epics
  restate the same glossary and the same discipline.
- **The UX pass drove a PRD amendment.** The Ownership Offer's missing notification was
  escalated out of the UX spine and landed as **FR-43** plus the retirement of FR-8's
  not-emailed assumption. This is the clearest available evidence that these documents were
  genuinely reconciled: a downstream phase found an upstream defect and the upstream document
  changed.

**One PRD statement is now stale, and the UX spine says so rather than hiding it.**

PRD §7 describes Account settings as "Spans Spaces; **the only surface that does**." AD-24
enumerates **two** Account-scoped surfaces — the Space switcher and Account settings.
`EXPERIENCE.md` records the conflict explicitly and resolves it: "AD-24 is later and more
specific, and it names the Space switcher explicitly, so it governs. Recorded rather than
silently resolved." The epics follow AD-24 (AR-29, Story 1.7).

The resolution is correct and the epics are consistent with it. The residue is that **PRD §7
was never corrected** — the 2026-08-20 revision note states "Nothing else changed." A reader
coming to the PRD fresh will read a false statement about the product's surface model. Low
severity, trivial to fix, but it is the kind of stale sentence that gets cited later as
authority.

**FR-6's missing surface is an acknowledged inherited gap, handled correctly.** PRD §7 omits
where Space rename lives. The UX spine places it in Space settings and tags it
`[ASSUMPTION: inherited gap, not introduced here — §7 omits it too, so a downstream epic for
FR-6 would otherwise have no surface to attach to.]` The epics carry the flag forward in both
the FR Coverage Map and Epic 3. Nothing is lost.

### UX ↔ Architecture Alignment

**Aligned. All three of the UX spine's escalations are closed, and the spine records the
outcomes including one where it was itself wrong.**

| UX escalation | Resolution | Verified |
|---|---|---|
| Emailing the Ownership Offer → `bmad-prd` | Landed as **FR-43** with an explicit disclosure constraint | PRD §4.11 FR-43 present; AD-26 carries the mechanism; Story 5.1 implements it |
| FR-28 × NFR-5 × NFR-9 → `bmad-architecture` | Decided as **AD-29** — keyset pagination, append-never-recycle, DOM virtualisation forbidden | AD-29 present in the spine, binding FR-28, FR-29, FR-30, FR-35, NFR-5, NFR-8, NFR-9; AR-22 and Stories 2.9, 2.10 implement it |
| The 403/404 timing oracle → `bmad-architecture` | **Corrected, not implemented** — the oracle does not hold, and the precondition is bound in **AD-3** instead | AD-3 present; AR-9 makes `{spaceId}` a build gate; Story 1.6 and Story 1.9 carry the timing case |

Two things are worth calling out as strengths rather than findings:

1. **AD-29 was decided on the UX spine's grounds, not merely to satisfy a budget.** The spine
   had stated a focus-identity contract (focus follows the Task, not the row element); the
   architecture chose appending over virtualisation because "a recycled row silently re-points
   keyboard focus at a different Task… a data-corrupting defect reachable only by keyboard,
   which is to say only by the users NFR-9 exists to protect." The accessibility obligation is
   now satisfied **by construction** rather than by careful implementation. That is architecture
   supporting UX rather than tolerating it.
2. **The UX spine retracts its own escalation honestly.** On the timing oracle it records "this
   spine was wrong" and keeps the original reasoning "because the reasoning is still worth
   reading." The isolation suite gains a timing case regardless.

**Architecture fully supports the UX performance and behaviour needs.** Checked specifically:
NFR-3's 16 ms local render is satisfied by AD-11 (client edits a replica, never blocks);
NFR-2's 1-second live-session clause by AD-8/AD-9 (lease authorised per frame, permission
pushed at the transaction boundary, never polled); NFR-5's read budget by AD-29's bounded seek;
the client-side isolation surface by AD-2 plus the UX spine's own purge rules, which the epics
carry as UX-DR31.

### Alignment Issues

#### Issue 1 — The AD-24 amendment the UX spine requires has not been made (Medium)

`EXPERIENCE.md` § *Account-scoped reads beyond AD-24's letter* identifies three reads richer
than AD-24's "identity only", decides each, and requires the architecture to be amended for
two of them:

| Read | UX decision |
|---|---|
| `Delete Account refused` naming every Space still owned (FR-3) | **Permitted.** "Flag for an AD-24 amendment naming it." |
| The Spaces-per-Account bound at NFR-8's 50 | **Permitted**, same basis. "Also needs naming in AD-24." |
| API Token list showing a per-Space Role | **Dropped** — a genuine cross-boundary Membership read on an Account-scoped surface |

**AD-24 has not been amended.** Its text still reads: they "may return Space **identity** — id
and name — and nothing else: no Project, Task, Membership, Label or count crosses a Space
boundary through them." Both permitted reads exceed that literally — one enumerates owned
Spaces, the other returns a count.

The epics handle this by assigning the amendment to **implementation**: AR-29 records the three
decisions, and Stories 3.1 and 5.4 each carry an acceptance criterion that "the amendment naming
this read is recorded rather than left implicit." That is a reasonable mitigation and the reason
is well stated — AD-24 exists so that "an unauthorised read is never solved by disabling
row-level security, opening a second connection, or inventing a bypass that then spreads."

The risk is that **the authoritative document still says the opposite of what will be built**,
and AD-26 independently asserts "AD-24 stands unamended" (correctly, for its own subject —
ownership offers need no third surface). An implementer reading AD-24 and AD-26 together will
find no trace of the two permitted exceptions, and the schema test AR-29 implies could be
written to the unamended letter. **Recommendation:** amend AD-24 in the architecture spine to
name the two reads, rather than carrying the amendment as a story obligation. This is a
document edit, not a design change — the decision is already made.

#### Issue 2 — An accepted-risk obligation is orphaned by the §6.4 gap (Medium)

`EXPERIENCE.md` carries this assumption:

> `[ASSUMPTION: browser spellcheck and cloud IME egress of description text is accepted. Several
> browsers' enhanced spellcheck and several mobile IMEs transmit typed text to a remote service,
> and the description editor is the product's largest free-text surface — but disabling
> spellcheck on a prose field is the wrong usability trade. Recorded as an accepted egress and
> **flagged against §6.4's data-protection gate, where it becomes a real obligation the moment a
> second data subject exists.**]`

Searched `epics.md` for `spellcheck`, `IME` and `egress` — **no match**. The obligation was
deliberately parked against §6.4's gate, and Step 3 established that §6.4's gate appears nowhere
in the epics. So this is Gap 1's first concrete casualty: a considered decision to accept a
data-egress risk, with a named condition for revisiting it, has been dropped along with the
condition. Nobody will find it again except by re-reading the UX spine.

This raises Gap 1's severity. §6.4 is not merely an unrecorded formality — it is the anchor
that at least one other document is hanging real obligations on.

#### Issue 3 — Two numeric restatements in the epics do not match `DESIGN.md` (Low)

Both appear in Story 1.2, whose acceptance criteria are build gates, so precision matters.

- **Colour token count.** UX-DR1 states "26 colour tokens across both themes."
  `DESIGN.md`'s frontmatter defines **30** — 15 semantic names, each with an unsuffixed dark
  value and a `-light` sibling (`surface-page`, `surface-column`, `surface-card`,
  `border-hairline`, `text-primary`, `text-muted`, `accent`, `accent-on`, `focus-ring`,
  `presence`, `danger`, `danger-on`, `revoked-edge`, `role-chip`, `role-chip-on`). A story
  implementing "26" against a source defining 30 risks an incomplete token set.
- **Contrast pair count.** UX-DR7 and Story 1.2 both say "all **twenty** stated pairs… each
  meets its stated threshold," and Story 1.2 adds "the build fails if any pair drops below it."
  `DESIGN.md`'s table has 20 rows, but only **18** carry a threshold. The final two —
  `surface-card` on `surface-column` (1.09 / 1.16) and `surface-column` on `surface-page`
  (1.10 / 1.07) — have a threshold of "—" and are called out at line 347 as "Two combinations
  that are load-bearing and **must not be mistaken for contrast pairs**." A harness asserting
  a threshold on all twenty would either fail permanently on those two or need an unstated
  exception.

Neither changes the design. Both would waste an implementer's time or produce a wrong gate.
**Recommendation:** correct to 30 tokens and 18 gated pairs (with the 2 surface-adjacency
ratios asserted as deliberately low rather than as contrast pairs).

### Warnings

1. **No missing-UX warning applies.** UX is not merely implied — it is specified to component,
   state, focus-destination and announcement-string level. `EXPERIENCE.md` covers surfaces the
   PRD never names, and the epics carry 42 discrete UX design requirements.
2. **Four of the UX spine's five live assumptions are carried into the epics**; the fifth
   (spellcheck egress) is Issue 2. Of the carried four: no webfont → UX-DR2; breakpoints at
   768/1280 → UX-DR41 and Story 2.11; FR-6's inherited surface gap → FR Coverage Map and
   Epic 3. "No density control" is not restated, which is benign — it is a will-not-build.
3. **Two IA surfaces are spine-only, by declared choice** — the List View and Account settings
   have no mockup. `EXPERIENCE.md` justifies both ("Neither has layout that drives behaviour")
   and both are fully specified in prose. Stories 2.10 and 1.8 carry them. Noted, not raised.
4. **The mockups are explicitly stale relative to the spines** — rendered before the border,
   type-scale and `readonly` corrections. The spines win on conflict and the epics were written
   from the spines. An implementer working from `mockups/` rather than from `DESIGN.md` would
   build the wrong thing; the ordering is stated in the UX document but is not restated in the
   epics.

## Step 5: Epic Quality Review

Applied against `bmad-create-epics-and-stories` standards, without compromise. All 8 epics and
all 53 stories were examined.

### A. User Value Focus

**Pass on all 8 epics.** Every title states a user capability, and every epic goal opens with
what a person can now do.

| Epic | Title | Goal opens with | Verdict |
|:--:|---|---|---|
| 1 | An Account, a Space of your own, and a boundary that holds | "A person can register, sign in, and land in a Space they did not create…" | ✓ user value |
| 2 | Track your own work on a Board | "A solo user can create Projects, add Tasks…" | ✓ user value |
| 3 | Several Spaces, cleanly separated | "A user can hold more than one Space…" | ✓ user value |
| 4 | Bring people into one piece of your work | "An Owner or Admin can invite someone by email address…" | ✓ user value |
| 5 | Hand a Space over and leave cleanly | "An Owner can offer ownership of a Space…" | ✓ user value |
| 6 | Shape Statuses per Project and retire work safely | "An Owner or Admin can add, remove, rename and reorder…" | ✓ user value |
| 7 | Write the same Task at the same time — and lose access the moment access ends | "Two or more Users can edit the same Task description simultaneously…" | ✓ user value |
| 8 | Drive Yello from a script | "A consumer can read and write Projects and Tasks in exactly one Space from outside the browser…" | ✓ user value |

**No technical epics found.** None of the red-flag patterns is present — no "Setup Database",
no "Create Models", no "Infrastructure Setup". Two cases deserve comment because they are the
ones this check usually catches:

- **Epic 8 is not "API Development".** It would be the obvious place for a technical epic, and
  the document forecloses it: "This epic does **not** build the API. AD-4 forbids a slice
  branching on calling surface, so every endpoint has accrued to both surfaces in the epic that
  built its slice; what remains here is the contract and the audit." Every endpoint ships with
  the feature that needed it. This is the inverse of a technical epic.
- **Epic 1 is the "Authentication System" borderline case, and it lands on the right side.** It
  delivers real user-visible value (register, sign in, land in a provisioned Space, switch
  Spaces, issue an API Token) while also carrying the substrate. The document states the
  trade-off and its rejected alternative: consolidating Epics 1 and 3 "would make Epic 1 ten
  FRs plus the entire substrate." Also notable: **"Access Control is not an epic."** FR-15 and
  FR-16 are declared cross-cutting, placed in Epic 1's request pipeline, and made an acceptance
  obligation on every later story — with AR-3 making a slice that re-implements them a defect.
  That is the correct treatment of a requirement that would otherwise become a technical epic.

**Story-level:** 11 of 53 stories carry no FR. Every one is declared as such in the Story
Coverage Index ("Stories carrying no FR deliver foundation, verification or an NFR-9 obligation,
and are marked accordingly"), and each uses an honest non-end-user persona rather than
pretending to be a user story — "As a developer building Yello" (1.1), "As the operator deciding
whether Yello can ship" (1.9, 3.4, 7.8), "As the operator" (1.10), "As the team about to build
collaborative editing" (7.1). Three of them (2.7, 2.8, 2.11) are genuine user stories carrying
NFR-9 obligations rather than FRs. No story is "Setup all models".

### B. Epic Independence

**Pass. No forward dependencies at epic level, and no circular dependencies.**

| Epic | Declared dependencies | Forward reference? |
|:--:|---|---|
| 1 | nothing | none |
| 2 | Epic 1 | none |
| 3 | Epic 1 | none |
| 4 | Epic 1, Epic 3 | none |
| 5 | Epic 1, Epic 3, Epic 4 | none |
| 6 | Epic 1, Epic 2 | none |
| 7 | Epic 1, Epic 2, Epic 4 | none |
| 8 | Epics 1–7 | none |

Every dependency points strictly backwards. Each is also justified on real grounds rather than
by layer: Epic 4 needs Epic 3 because "a second Space must exist before anyone can be invited
into one"; Epic 5 needs Epic 4 because an offer can only name an existing Membership; Epic 7
needs Epic 4 because demoting or removing someone mid-edit requires a second Membership; Epic 6
needs Epic 2 because "in Epic 2 every Project shares the Space defaults, so a move never
requires a mapping and the requirement has nothing to grip."

**A forward dependency was found and fixed during story creation, and the fix is recorded.** The
FR Coverage Map states: "Revised during story creation: FR-24 moved from Epic 2 to Epic 6.
Defining a Space's default Status set includes removing one, and removal requires FR-26's
mandatory Task mapping — so FR-24 in Epic 2 would have been a forward dependency on Epic 6.
Epic 2 needs nothing from it: Epic 1 seeds Todo / In Progress / Done at provisioning, and UJ-1
requires that nobody be asked about columns." This is precisely the defect this review exists to
catch, already caught and remediated upstream.

**Two potential forward dependencies in Epic 6 were correctly scoped away rather than
introduced.** Stories 6.5 and 6.6 both need to state that a collaborative editing session
survives a Task move — a capability that does not exist until Epic 7. Story 6.5 handles it:

> "**Then** the session continues across the move and no participant is disconnected **And**
> this holds **by construction** rather than by implementation: the Task id is unchanged by a
> reparent and the sync lease is keyed on `(Account, Space)`, neither of which a within-Space
> move alters — so nothing here must be built for it, but nothing here may break it either
> **And** the requirement is *verified* in Epic 7 once editing sessions exist; this story asserts
> only that the move touches neither the Task id nor the Space."

The story asserts a negative property that is testable within Epic 6 and defers verification
explicitly. Epic 6 does not depend on Epic 7. This is exemplary handling.

### C. Within-Epic Story Dependencies

**Pass. No story references a later story, in any epic.** Traced all 53 in order:

- **Epic 1** (1.1 → 1.10) strictly linear: skeleton → design foundations → register → authenticate
  → Space resolution → refusal + bound registry → switcher → API Token → prove isolation →
  deploy. Story 1.9 depends only on 1.5–1.8; Story 1.10 on 1.1.
  - One case checked closely: Story 1.6 exercises the Role matrix against "**seeded** Memberships
    at Owner, Admin, Member and Viewer" — Roles that cannot be created through the product until
    Epic 4. The story says *seeded*, and Story 4.7 later re-exercises the matrix against
    Memberships "created through the product's own paths **rather than seeded**." The distinction
    is deliberate and stated in both places. Not a forward dependency.
  - Story 1.7 builds the switcher and the client purge rules against a single Space, where
    "nothing carries over" is trivially true. Epic 3's notes acknowledge this: "Epic 1 builds
    them against a single Space; Epic 3 is the first time 'nothing carries over' has anything to
    carry." Building the mechanism before it is stressed is correct ordering, not a gap.
- **Epic 2** (2.1 → 2.11): Project → Tasks/Board → attributes → Labels → deletion → **Move
  control** → pointer/touch → keyboard → at-scale → List View → small viewport. Note that 2.6
  (the explicit Move control) precedes 2.7 (drag), which UX-DR21 requires — the canonical path
  must "never be the second thing offered." Correct sequencing.
- **Epic 3** (3.1 → 3.4), **Epic 4** (4.1 → 4.7), **Epic 5** (5.1 → 5.4), **Epic 6** (6.1 → 6.6),
  **Epic 7** (7.1 → 7.8), **Epic 8** (8.1 → 8.3): all linear, each story building only on
  earlier ones. Epic 7's ordering is load-bearing and correct — 7.1 writes the conformance suite
  *before* 7.2/7.4 implement anything, as AR-18 demands.

### D. Database and Entity Creation Timing

**Pass — textbook. Tables are created by the story that first needs them, never upfront.**

Story 1.1 creates the solution skeleton and the four test suites and explicitly no schema:
"Given the four gating suites… When they run against a solution with no feature code, Then each
builds and executes, reporting zero tests rather than failing to build."

| Entity | First created by | Schema test in that story |
|---|---|---|
| `Account`, `Space`, `Membership` | 1.3 Register | ✓ (one transaction, one slice) |
| `AccessRefusal` | 1.6 Refuse at the boundary | ✓ |
| `ApiToken` | 1.8 Issue an API Token | ✓ (hash only) |
| `Project` | 2.1 Create a Project | ✓ "non-nullable `SpaceId` with a row-level security policy" |
| `Task` | 2.2 Add Tasks | ✓ "carries a non-nullable `SpaceId` **directly** rather than reachable by join" |
| `Label` | 2.4 Define and apply Labels | ✓ |
| `Invitation` | 4.1 Invite an email address | ✓ (with `ExpiresAt`) |
| `OwnershipOffer` | 5.1 Offer ownership | ✓ (with the filtered unique index) |
| `StatusDefinition` + delta | 6.1 Shape a Project's Statuses | ✓ (stable id, delta keyed on identity) |
| `TaskDescriptionChange` | 7.2 Description as append-only log | ✓ |

Every one carries a schema test asserting the RLS policy in the same story, and Story 1.10
applies migrations "as an explicit job **before** the revision is promoted, never on application
start," including the RLS policies and filtered indexes.

### E. Starter Template and Greenfield Checks

**Starter template check performed and answered.** The epics document leads its Additional
Requirements section with it:

> "**🚨 STARTER TEMPLATE: none.** The architecture specifies **no third-party starter or
> greenfield template**. It does specify an exact solution skeleton, ring dependency rule, stack
> versions and build gates that must exist before any feature story can be written against them
> — this is Epic 1 Story 1 material and is AR-1 … AR-4 below."

Story 1.1 is that story, and it names all eight projects, all five test projects, and pins nine
dependency versions. ✓ Correctly handled.

**Greenfield indicators — all three present:**

| Expected | Present |
|---|---|
| Initial project setup story | ✓ Story 1.1 |
| Development environment configuration | ✓ Story 1.1 (`aspire run`, Testcontainers against `mssql/server:2025-latest`, in-memory provider forbidden) |
| CI/CD pipeline setup early | ✓ Story 1.10, within Epic 1 |

### F. Acceptance Criteria Quality

**Format: pass on all 53 stories.** Every story uses `As a / I want / So that` followed by
Given/When/Then/And blocks. No prose-only acceptance criteria anywhere.

**Testability and specificity: unusually strong.** ACs state numbers, SQL predicates, attribute
names and exact copy strings rather than intentions. Representative:

- Story 2.9 writes the query shape out: "fetched by keyset seek on the position key —
  `WHERE (ProjectId, StatusId) = … AND PositionKey > @last ORDER BY PositionKey` — and never by
  `OFFSET`."
- Story 7.7 states the budget and the mechanism: "`MembershipChanged` publishes at the
  transaction boundary, the lease is invalidated, and within **1 second** the editor becomes
  `readonly` — without the participant taking any action."
- Story 1.4 fixes the copy: "the message is one string for every cause — 'Email or password is
  incorrect.' — never 'no account found' and never 'wrong password'."

I found **no vague ACs** of the "user can login" kind, and **no non-measurable outcomes**.

**Error and edge coverage: comprehensive.** Sampled every epic; error paths are first-class
rather than appended. Story 4.3 covers five terminal Invitation states collapsing to one
response plus the wrong-Account-signed-in case; Story 5.3 distinguishes 409 from 404 by whether
the caller holds a Membership *and* fixes the evaluation order ("the Space context is resolved
**first** and `State = Pending` evaluated **second**, never the reverse"); Story 6.4 covers a
reported Project with no destination and a refusal naming which Project blocked it; Story 7.7
covers removal while disconnected.

**A distinctive and valuable pattern:** many ACs carry "**And** the reason is stated: …". This
records *why* a choice was made inside the acceptance criterion, which prevents an implementer
from "simplifying" a deliberate decision. Story 5.2's demote-then-promote ordering is the
clearest example — it explains that EF Core picks its own statement order and promote-first
violates the filtered unique index. This is above the standard the workflow asks for.

### G. Findings by Severity

#### 🔴 Critical Violations

**None.** No technical epics, no forward dependencies, no epic-sized stories that cannot be
completed, no circular dependencies, no upfront schema creation.

#### 🟠 Major Issues

**M1 — Story 1.6's bound-registry acceptance criterion enumerates one of six bounds, and this is
the root cause of Gap 2.**

The AC reads: "Given the NFR-8 bound registry, declared in one place and checked by the pipeline
rather than by any slice, When a bound is exceeded, Then the refusal carries a machine-readable
reason and is raised inside the same transaction as the creation it refuses **And its first
entry is the 50-concurrent-active-Sessions-per-Space bound**."

The story establishes the registry and names **one** entry. AD-25 names all six bounds
explicitly and requires each be enforced. Three more are picked up by Stories 2.1, 2.2 and 3.1;
**two — Memberships per Space (100) and concurrent editors per Task (10) — are picked up by
nobody** (Step 3, Gap 2).

Because the registry story does not enumerate what the registry must contain, an implementer can
satisfy Story 1.6 completely with a one-entry registry and nothing downstream will catch the
omission. NFR-8's own words make this a defect: "A bound that is not enforced is a defect, not a
relaxation."

**Recommendation.** Amend Story 1.6's AC to enumerate all six bounds as required registry
entries, each with the creation operation that must consult it. That single edit converts Gap 2
from two missing stories into a checklist the build can enforce, and it makes the registry's
completeness testable rather than incidental.

#### 🟡 Minor Concerns

**m1 — Story 1.2's contrast-harness AC is not executable as written.** "Then all twenty stated
pairs are computed… and each meets its stated threshold. And the build fails if any pair drops
below it." Two of `DESIGN.md`'s twenty rows carry no threshold and are explicitly "not contrast
pairs" (surface-adjacency ratios of ~1.09 and ~1.10). A harness written to this AC either fails
permanently on those two or needs an unstated exception. Cross-references Step 4, Issue 3.
Fix: assert 18 gated pairs, plus the 2 adjacency ratios as deliberately low.

**m2 — Four FRs will read as complete at their mapped epic's end while carrying unimplemented
clauses.** FR-30 (Assignee dimension → 4.6), FR-23 (session termination → 7.4), FR-13 and FR-14
(live-session clauses → 7.7, 7.8). The structure is correct and each case is disclosed in both
places — this is the right way to stage them. The risk is purely in tracking: if sprint status
marks an FR done when its mapped epic closes, four FRs go falsely green. The mitigation depends
on whoever runs sprint planning reading the "Requirements completing outside their mapped epic"
note. Fix: carry a partial-completion marker into the sprint plan rather than relying on prose.

**m3 — Three stories are large, though bounded and cohesive.** Story 1.2 (11 AC groups covering
the entire design foundation), Story 2.9 (keyset read path + append rendering + separate count
query + `aria-setsize`/`aria-posinset` + focus-follows-identity + the AR-40b cold-start
decision), and Story 7.7 (five FR-34 states + purge-before-announce ordering + focus
destinations). None is unbounded and each has a clear completion test, so none is epic-sized.
Story 2.9 is the most reasonable split candidate (read path vs render/accessibility path). Noted
rather than pressed — Epic 2 is already 11 stories and the document records that consolidation
was assessed against a 17-story ceiling.

**m4 — Three open decisions gate epic starts, correctly flagged but still open.** AR-40a (the
merge algorithm) "must close before this epic starts" for Epic 7; AR-40c (Azure SQL's
`SESSION_CONTEXT` parallel-plan exposure) before Epic 1's first production deploy; AR-40b (cold
start vs NFR-5) at Epic 2's first measurement. All three are named in the epic notes and carried
as story ACs, which is the right handling. They are readiness risks rather than quality defects
and are carried to the final assessment.

**m5 — Epic 2 at 11 stories and Epic 1 at 10 are on the large side** for standalone epics. The
document justifies the shape: "Everything upstream is final and mutually reconciled, so epics
are deliberately few and large; the four splits that exist mark genuine risk boundaries." All
four overlap patterns were assessed with consolidation explicitly considered and rejected on
stated grounds. Accepted.

### H. Best Practices Compliance Checklist

| Check | Epic 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| Delivers user value | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Functions independently of later epics | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Stories appropriately sized | ✓* | ✓* | ✓ | ✓ | ✓ | ✓ | ✓* | ✓ |
| No forward dependencies | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Tables created when needed | ✓ | ✓ | n/a | ✓ | ✓ | ✓ | ✓ | n/a |
| Clear acceptance criteria | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Traceability to FRs maintained | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

\* one large-but-bounded story noted under m3.

**Structural verdict: the epic and story breakdown passes this review.** One Major issue (M1),
five Minor concerns, no Critical violations. The two defects this step exists to catch — technical
epics and forward dependencies — are both absent, and in the one case where a forward dependency
existed (FR-24 in Epic 2), it was found and fixed during story creation with the reasoning
recorded.

## Summary and Recommendations

### Overall Readiness Status

## ✅ READY — with three document edits to land before Epic 1 begins

This is among the most complete planning sets I have assessed. Requirements traceability is
effectively total: **43/43 FRs, 11/11 NFRs, 40/40 architecture requirements, 42/42 UX design
requirements, 2/2 gating metrics and 3/3 retention obligations** all carry an owning story, and
the epic structure passes every best-practice check without a Critical violation.

The findings below are real and worth fixing, but none of them requires replanning. Two are
missing records rather than missing design, one is an outstanding edit to an architecture
document, and the rest are precision and tracking items. **Total remediation effort is measured
in hours, not iterations** — and only three of the nine touch Epic 1, which is the only thing
standing between this plan and a first commit.

The reason the score is this high is worth naming, because it is repeatable: the planning chain
kept escalating defects upstream rather than absorbing them. A `bmad-spec` pass found six
defects in the PRD and all six were fixed at source. A `bmad-ux` pass found a seventh and it
became **FR-43**. The UX spine escalated three items and all three closed — one of them by the
architecture deciding a mechanism the PRD had explicitly declined to specify (AD-29), and one by
the UX spine being told it was wrong and recording that in its own text. The epics then found a
forward dependency in their own first draft (FR-24) and moved it. Each of those is a defect that
would otherwise have surfaced during implementation.

### Critical Issues Requiring Immediate Action

**None.** No issue found blocks starting implementation. Ranked by consequence:

#### 1. PRD §6.4's data-protection gate exists nowhere in the plan — HIGH

The PRD deliberately wrote data protection as a **testable release condition**, not an
aspiration: *"the first Account created by anyone other than the operator makes this document
non-compliant until amended"*, with five named prerequisites. §6.2 and §6.3 are both carried into
the epics and cited by stories. **§6.4 is cited by nothing** — it appears in neither the
Requirements Inventory, the Additional Requirements, nor any of the 53 stories.

The plan has silently converted a gate back into an aspiration. And it has already cost
something concrete: the UX spine accepted a real data-egress risk (browser spellcheck and cloud
IME transmitting description text to third parties) and parked it *"against §6.4's
data-protection gate, where it becomes a real obligation the moment a second data subject
exists."* Dropping the gate orphaned the obligation. That decision is now recoverable only by
re-reading `EXPERIENCE.md`.

This is not a v1 implementation gap — the prerequisites genuinely do not apply to a
single-operator deployment. The defect is that **nothing tracks the condition that ends that
state**.

#### 2. Two of NFR-8's six bounds are unenforced, and the registry story is why — HIGH

NFR-8's own words: *"A bound that is not enforced is a defect, not a relaxation."* AD-25 names
all six bounds explicitly and requires each be a refusal at one choke point.

| Bound | Enforcing story |
|---|---|
| Spaces per Account — 50 | ✓ Story 3.1 |
| Projects per Space — 50 | ✓ Story 2.1 |
| Tasks per Project — 5,000 | ✓ Story 2.2 |
| Concurrent Sessions per Space — 50 | ✓ Story 1.6 |
| **Memberships per Space — 100** | **none** |
| **Concurrent editors per Task — 10** | **none** |

The root cause is Story 1.6's acceptance criterion, which establishes the registry and names
only *"its first entry"*. An implementer can satisfy that story completely with a one-entry
registry, and nothing downstream catches the omission. The Membership bound is reachable in
ordinary use at the product's own stated scale, and **the user-facing copy for it already
exists** in UX-DR35 — *"This Space has 100 Memberships, the maximum."* — mapped to two stories
that have nothing to do with Memberships. The string is written and nothing triggers it.

#### 3. The AD-24 amendment the UX spine requires has not been made — MEDIUM

The UX spine identified two Account-scoped reads that exceed AD-24's *"identity only"* letter,
decided both permitted, and required *"an AD-24 amendment naming it"*. **AD-24's text is
unchanged**, and AD-26 separately (and correctly, for its own subject) asserts *"AD-24 stands
unamended"* — so an implementer reading the architecture finds no trace of either exception. The
epics mitigate by assigning the amendment to Stories 3.1 and 5.4 as an implementation
obligation, which is reasonable but leaves the authoritative document saying the opposite of
what will be built.

#### 4. Four FRs will read as complete while carrying unimplemented clauses — MEDIUM

FR-30, FR-23, FR-13 and FR-14 each finish in a later epic than the one they are mapped to. The
staging is correct and every case is disclosed in both places — this is genuinely good practice.
The risk is purely in **tracking**: if sprint status marks an FR done when its mapped epic
closes, four FRs go falsely green, and the mitigation depends on whoever runs sprint planning
reading a prose note.

#### 5. Thirteen PRD assumptions are hardened into acceptance criteria, none confirmed — MEDIUM

PRD §12 surfaces all thirteen *"for explicit confirmation"* and marks four with † as having
already hardened into architecture, costing *"more than a document edit to reverse"*. The epics
implement every one of them as an acceptance criterion — Todo / In Progress / Done, both 7-day
expiries, 90-day retention, URL-path versioning with two concurrent versions, Admins not
modifying each other, immediate irreversible deletion. Coverage is complete; **confirmation
never happened**. Each is defensible, which is exactly why none has been challenged.

#### 6. Three architecture deferrals gate epic starts and remain open — MEDIUM

All three are named in the epic notes and carried as story ACs, which is correct handling. They
are nonetheless open decisions:

- **AR-40a — the text merge algorithm.** *"Must close before this epic starts"* for Epic 7. This
  is the single largest unresolved technical decision in the plan, and Story 7.1 correctly makes
  its conformance suite the admission test.
- **AR-40b — cold start against NFR-5.** Decided at Epic 2's first measurement: pin minimum
  replicas to 1, or declare NFR-5 measured warm and exempt the cold path — *"but state it."*
- **AR-40c — Azure SQL's `SESSION_CONTEXT` parallel-plan exposure.** Must be confirmed before
  Epic 1's first production deploy; `MAXDOP = 1` relaxes only with the pooled-connection
  isolation test still green.

#### 7. Two numeric restatements in Story 1.2 do not match `DESIGN.md` — LOW

UX-DR1 says *"26 colour tokens"*; `DESIGN.md` defines **30** (15 semantic names × 2 themes).
UX-DR7 and Story 1.2 say *"all twenty stated pairs… each meets its stated threshold"* and *"the
build fails if any pair drops below it"*; only **18** of the twenty rows carry a threshold, the
other two being explicitly *"not contrast pairs"*. As written, the contrast harness AC is not
executable.

#### 8. PRD §7 contradicts AD-24 and was never corrected — LOW

§7 still reads *"Account settings — … the only surface that [spans Spaces]"*. AD-24 enumerates
two. The UX spine recorded the conflict and resolved it in AD-24's favour; the epics follow
AD-24. The PRD's 2026-08-20 revision note says *"Nothing else changed"*, so the stale sentence
survives in a `status: final` document.

#### 9. `epics.md` carries no `status` field — LOW

Its `stepsCompleted` array shows the producing workflow ran to completion, but the document has
no explicit final marker, unlike the PRD, architecture spine, `DESIGN.md` and `EXPERIENCE.md`,
all of which carry `status: final`. `addendum.md` has no frontmatter at all despite being cited
as a source by five downstream documents — and it holds a load-bearing constraint (Status deltas
must key on identity, not name) without which FR-27's cascade cannot be built correctly.

### Recommended Next Steps

**Before Epic 1 begins — three edits, all to `epics.md`:**

1. **Enumerate all six NFR-8 bounds in Story 1.6's registry acceptance criterion**, each paired
   with the creation operation that must consult it. This closes issue 2 at its root rather than
   in two places, and makes registry completeness testable. Then add the Membership-bound refusal
   to **Story 4.3** (acceptance is the transaction that creates the Membership), cite AR-30 and
   UX-DR35 there, and re-map UX-DR35 to include it. For the 10-editor bound, either add a
   refusal to Story 7.3 (lease establishment is the admission point) **or** record an explicit
   decision that this bound is a convergence guarantee rather than a refusal — and if the
   latter, say so, because NFR-8's wording does not currently permit it.
2. **Add PRD §6.4's gate to Story 1.10**, which already owns §6.3's cost ceiling and the
   operational alerting. One acceptance criterion naming the single-operator assertion, the
   condition that ends it, and the five prerequisites. Carry the spellcheck/IME egress
   acceptance across from `EXPERIENCE.md` so it is parked against something that exists.
3. **Correct Story 1.2's two numbers** — 30 colour tokens, and 18 gated contrast pairs plus 2
   surface-adjacency ratios asserted as deliberately low.

**Before the epics that depend on them:**

4. **Amend AD-24 in the architecture spine** to name the two permitted Account-scoped reads —
   delete-Account-refused enumerating owned Spaces, and the Spaces-per-Account bound. A document
   edit, not a design change; the decision is already made. Before Epic 3.
5. **Close AR-40a** — select the merge algorithm and have it pass Story 7.1's conformance suite.
   Before Epic 7. Note the constraint: whole-field last-writer-wins cannot pass, and adopting it
   *"would be a PRD amendment to FR-31 and FR-33 rather than an architecture decision."*
6. **Close AR-40c** before Epic 1's first production deploy, and **AR-40b** at Epic 2's first
   NFR-5 measurement.

**Process items, at your discretion:**

7. **Walk PRD §12's thirteen assumptions and confirm or revise them.** They are now acceptance
   criteria in 53 stories; the four marked † are already more expensive than a document edit.
   Thirty minutes now buys the right to stop calling them assumptions.
8. **Carry a partial-completion marker for FR-30, FR-23, FR-13 and FR-14 into sprint planning**,
   so the four staged FRs cannot read as done at their mapped epic's close.
9. **Set `status: final` on `epics.md`**, add frontmatter to `addendum.md`, and fix PRD §7's
   stale sentence about Account-scoped surfaces.

### Final Note

This assessment identified **9 issues across 4 categories** — 2 coverage gaps, 3 alignment
issues, 1 Major and 5 Minor quality items (2 overlapping with alignment findings), and 3 process
risks. **No Critical issue was found, and nothing blocks implementation.**

Two findings are worth acting on before a line of code is written, because both are cases where
a *deliberate decision* has gone missing rather than a design being absent: §6.4's gate, and the
two unenforced NFR-8 bounds. In both cases the thinking was done upstream and the record was
dropped in transit — which is the failure mode this whole planning chain has otherwise been
unusually good at avoiding.

Everything else can be fixed as it is reached. Epic 1 is ready to start once the three `epics.md`
edits land.

---

**Assessment date:** 2026-08-22
**Assessor:** Implementation Readiness workflow (`bmad-check-implementation-readiness`), acting
as Product Manager for requirements traceability
**Documents assessed:** 6 supplied by the user (PRD, addendum, architecture spine, DESIGN,
EXPERIENCE, epics) plus 10 traceability sources added at Step 1 with user approval (the
`spec-yello` SPEC kernel and its 8 companions, and `docs/bmad-coverage.md`)
**Volume reviewed:** ~473 KB of planning artifacts, read in full — 885 lines of PRD, 170 of
addendum, 2,759 of epics, plus the architecture spine's 29 ADs and both UX spines

---

## Remediation Applied — 2026-08-22

The three pre-Epic-1 recommendations were applied to `epics.md` in this session, immediately
after the assessment. Findings above are left as originally written so the audit trail survives;
this section records what changed. **No story was added or removed — the breakdown remains 8
epics and 53 stories.**

### Issue 2 (HIGH) — two unenforced NFR-8 bounds → **CLOSED**

Fixed at the root first, then at both sites:

- **Story 1.6** — the bound-registry acceptance criterion now enumerates **all six** of NFR-8's
  bounds in a table, each paired with the creation operation that consults it and the story that
  enforces it, with "a registry missing any of them fails the architecture suite." The former
  wording — "its first entry is the 50-concurrent-active-Sessions-per-Space bound" — is replaced,
  since it was the mechanism by which two bounds went missing. The AC now states why the registry
  is enumerated centrally: "a registry whose completeness is incidental is one where a missing
  bound is invisible."
- **Story 4.3** — new AC refusing the 101st Membership inside the accepting transaction, drawing
  from the Story 1.6 registry rather than a local check, and carrying UX-DR35's existing copy
  ("This Space has 100 Memberships, the maximum."). The AC also records *why* the refusal lands on
  acceptance rather than issue: an Invitation issued while the Space had room may be accepted
  after it filled, so refusing at issue would check the wrong moment.
- **Story 7.3** — new AC refusing an 11th concurrent editing lease on a Task. Enforced as a
  refusal rather than recorded as an exemption, because NFR-8's wording does not permit the
  latter: "a silently-admitted 11th editor is exactly a wrong answer, since NFR-4 guarantees
  convergence only to 10." Read access and Presence are explicitly unaffected — only the lease
  is refused.

**NFR-8 bounds with an enforcing refusal: 6/6 (was 4/6).** Also closes Step 5's Major issue
**M1**, which was the same defect seen from the story-quality end.

### Issue 1 (HIGH) — PRD §6.4's data-protection gate → **CLOSED**

- **Requirements Inventory** — a new **PRD constraint blocks** subsection registers all four §6
  blocks with their owning stories, so none depends on having an FR number to survive. §6.4 is
  recorded in full: the single-operator assertion, the gate condition verbatim, and all five
  prerequisites.
- **Story 1.10** — two new ACs. The first asserts the single-operator position in writing and
  names the condition that ends it plus the five prerequisites, recorded "as an operational
  condition alongside the §6.3 cost ceiling rather than as a backlog item." The second collects
  the obligations parked against the gate — carrying the UX spine's accepted browser-spellcheck
  and cloud-IME egress across from `EXPERIENCE.md`, which **also closes Step 4's Issue 2**
  (the orphaned egress obligation). The addendum's deferred subject-access capability is noted
  as travelling with the gate.

**PRD §6 constraint blocks covered: 4/4 (was 3/4).**

### Issue 7 (LOW) — Story 1.2's two numeric errors → **CLOSED**

- **Colour tokens: 26 → 30.** Corrected in UX-DR1 and given its own Story 1.2 AC that names all
  15 semantic tokens explicitly, "so an incomplete token set is detectable rather than merely
  wrong."
- **Contrast pairs: 20 → 18 gated.** UX-DR7 and Story 1.2 now gate 18 pairs (12 text at 4.5:1,
  6 non-text/structural at 3.0:1), with a separate AC asserting the two surface-adjacency ratios
  (~1.09 / ~1.10) as deliberately low and explicitly **not** gated — quoting `DESIGN.md`'s own
  "must not be mistaken for contrast pairs." The harness AC is now executable; it previously was
  not. **Also closes Step 5's Minor m1.**

### Traceability updated

Four Story Coverage Index rows were amended so the index matches the stories: 1.6 (+AR-30 *all
six bounds declared*, +NFR-8, +UX-DR35), 1.10 (+§6.4 gate), 4.3 (+AR-30, +NFR-8, +UX-DR35),
7.3 (+AR-30, +NFR-8, +UX-DR35). UX-DR35's mapping now includes the three stories that actually
raise a scale-bound refusal. No new epic dependency was introduced — Stories 4.3 and 7.3 draw on
Epic 1's registry, and Epics 4 and 7 already depended on Epic 1.

### Status after remediation

| Metric | Before | After |
|---|:--:|:--:|
| FR coverage | 43/43 | 43/43 |
| NFR-8 bounds enforced | 4/6 | **6/6** |
| PRD §6 constraint blocks covered | 3/4 | **4/4** |
| Open issues | 9 | **5** |

**Closed:** issues 1, 2, 7 and Step 4's Issue 2; plus quality items M1 and m1.
**Remaining (none blocking Epic 1):** issue 3 (AD-24 amendment, before Epic 3) · issue 4 (staged-FR
tracking, at sprint planning) · issue 5 (13 unconfirmed PRD assumptions) · issue 6 (AR-40a/b/c,
at their respective epics) · issues 8 and 9 (PRD §7's stale sentence; missing `status` fields).
