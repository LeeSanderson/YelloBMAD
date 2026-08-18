---
id: SPEC-yello
companions:
  - glossary.md
  - role-capability-matrix.md
  - acceptance-criteria.md
  - quality-budgets.md
  - surfaces-and-journeys.md
  - success-metrics.md
  - decisions-settled.md
  - harness-constraints.md
  - ../../planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md
sources:
  - ../../planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md
  - ../../planning-artifacts/prds/prd-YelloBMAD-2026-08-15/addendum.md
  - ../../brainstorming/brainstorm-yello-mvp-scope-2026-08-15/brainstorm-intent.md
  - ../../../docs/bmad-coverage.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability only — consult them only if you need narrative rationale or prose color this contract intentionally omits.

> ⚠️ **Consumer note, 2026-08-18 — read before relying on this file.** No installed BMad skill downstream of `bmad-architecture` reads `SPEC.md`. `bmad-create-epics-and-stories`, `bmad-check-implementation-readiness`, `bmad-sprint-planning`, `bmad-create-story` and `bmad-dev-story` all discover requirements from the **PRD**. Every load-bearing resolution recorded here has therefore been folded back into `prd.md` and `addendum.md`, which are the documents the implementation chain actually consumes — `prd.md` now carries FR-1 … FR-42 and its open questions are resolved. **Treat this spec as the reasoning record and audit trail, not as the live contract.** Where the two ever disagree, `prd.md` is what gets built; report the divergence rather than resolving it silently.

# Yello — multi-Space task management

## Why

**A vision to realize.** Every task tool makes you choose a container before you have anything to put in it. A personal to-do app assumes you work alone; a team workspace assumes an organisation with a billing entity and an admin who provisions accounts; a client portal assumes an outside party to wall off. Pick one and the others turn awkward — a freelancer runs three tools or bends one into a shape it resists. Yello's bet is that all three are the same primitive: a **Space** is a container for work and the boundary of who can see it, and nothing more is required of it. What follows from that is the part that matters — the interesting user holds several Memberships at once with different standing in each, so identity is global and permission is contextual, and every question about what someone may do is answered against a specific Space, never against the person. The people affected are the freelancer, the small studio, and the client who wants to watch work land without being able to change it. That single constraint shapes every decision downstream, and a release either proves the thesis or disproves it.

**A mandate the project set for itself.** Yello is a test harness for the BMad method, not a commercial product: no users, no market, no deadline. Its requirements were derived from methodology coverage rather than user demand — a feature earns its place by forcing part of BMad to activate against authentic complexity, never by being bolted on to reach a skill. Two consequences bind every downstream trade-off. First, v1 is built at launch rigour despite having no users, because the downstream phases are the real consumer; a proposal to simplify by cutting the API, the collaborative editor or the falsifiable quality budgets destroys the point of the exercise rather than saving effort. Second, v1 is deliberately not frozen — a genuinely wanted requirement is held in reserve to be introduced mid-flight. Both are specified in `harness-constraints.md`.

## Capabilities

**CAP-1 … CAP-41 mirror the source PRD's functional requirements one-to-one: CAP-N is FR-N.** Numbers are identifiers, not positions — CAP-41 belongs to Tasks and sits out of sequence for that reason, and CAP-42 sits with Spaces. **CAP-42 onward have no PRD counterpart**; they were introduced by decisions taken while resolving this spec's open questions, and each records the decision that created it. Each `success:` below is the *decisive* criterion; the complete testable consequence list for every capability is in `acceptance-criteria.md`.

**Accounts and Authentication** — global identity, self-service only. No administrator anywhere in Yello can create, disable or reset another person's Account.

- **CAP-1**
  - **intent:** A person can create an Account identified uniquely by their email address, without anyone provisioning it for them.
  - **success:** Registering an address that already has an Account is indistinguishable from registering a new one, and registration completes with exactly one Space in existence for that Account, owned by it.
- **CAP-2**
  - **intent:** An Account can authenticate and hold a Session that persists across requests and across every Space it belongs to.
  - **success:** The same Session is used unchanged when the acting Account switches Space, carries no Role or Space of its own, and grants access to nothing once expired.
- **CAP-3**
  - **intent:** An Account can delete itself without orphaning a Space or destroying work belonging to other Accounts.
  - **success:** Deletion is refused while the Account owns any Space; afterwards, Tasks it was Assignee of survive unassigned and content it authored in Spaces it did not own is retained under a deleted-Account attribution.

**Spaces** — the unit of both work containment and access control. Exactly one Owner at all times is an invariant, not a default.

- **CAP-4**
  - **intent:** The system provisions a Space owned by a newly registered Account, so no structural decision stands between registering and holding work.
  - **success:** The Space is usable the moment registration completes, and no attribute distinguishes it from a Space created by CAP-5.
- **CAP-5**
  - **intent:** An authenticated Account can create additional Spaces without limit.
  - **success:** The creator becomes Owner of a Space with exactly one Membership and the default Status set, and its creation is invisible from every other Space.
- **CAP-6**
  - **intent:** An Owner or Admin can rename a Space.
  - **success:** Members and Viewers cannot, and two Accounts may each own an identically named Space without collision or disclosure.
- **CAP-7**
  - **intent:** An Owner can delete a Space, destroying its Projects, Tasks, Memberships and Invitations.
  - **success:** Only the Owner can — Admins cannot — and deletion removes every Membership and invalidates every API Token issued for that Space; belonging to no Space afterwards is a valid state.
- **CAP-8**
  - **intent:** An Owner can offer ownership of a Space to another Membership in that Space.
  - **success:** Ownership does not move until the offer is accepted — the offering Owner remains Owner throughout, so the Space never has zero Owners or two — and an offer revoked by the offerer or declined by the recipient leaves every Role exactly as it was.
- **CAP-42** *(no PRD counterpart — added to close the ownership-consent defect)*
  - **intent:** The Membership an ownership offer names can accept it, becoming Owner, or decline it.
  - **success:** Acceptance moves ownership in one atomic step, leaving exactly one Owner — the recipient — with the previous Owner an Admin who has not lost access; no Account ever becomes an Owner without having agreed to it.
- **CAP-9**
  - **intent:** An Account can move between the Spaces it holds Membership in, and the active Space determines everything subsequently visible and permitted.
  - **success:** Only Spaces the acting Account holds Membership in are listed by any means, and a request with no resolvable Space context is refused rather than defaulted.

**Membership and Invitations** — the only mechanism by which an Account gains access to a Space.

- **CAP-10**
  - **intent:** An Owner or Admin can invite an email address to a Space at a specified Role, whether or not that address has ever heard of Yello.
  - **success:** The response to the issuer is identical whether or not the address corresponds to an existing Account, and no Invitation can be issued at Owner Role.
- **CAP-11**
  - **intent:** An invited person can accept, gaining Membership at the Role the Invitation specified.
  - **success:** Acceptance requires the invitee authenticated as the invited Account plus a deliberate act naming the Space and Role — a bare fetch of the acceptance route creates nothing — and produces exactly one Membership, in exactly the invited Space, at exactly the invited Role, with the invitee's other Memberships neither visible to nor affected by the inviter.
- **CAP-12**
  - **intent:** An Owner or Admin can revoke an Invitation that has not been accepted.
  - **success:** A revoked Invitation can never afterwards be accepted, and an Invitation remains valid when its issuer is later demoted, removed or deleted.
- **CAP-13**
  - **intent:** An Owner or Admin can change the Role of a Membership within the constraints of their own Role.
  - **success:** An Admin can move Memberships between Member and Viewer only, no Role change produces a second Owner or removes the sole Owner, and the change takes effect on the target's active Sessions without re-authentication.
- **CAP-14**
  - **intent:** An Owner or Admin can remove a Membership, and any Account can remove its own.
  - **success:** Removal revokes access immediately, reaches open editors, and invalidates that Account's API Tokens for the Space; the Owner's Membership cannot be removed by anyone while it holds ownership.

**Access Control** — the invariant every other capability is written against.

- **CAP-15**
  - **intent:** Every read and write of Space-scoped data is authorised against the acting Account's Membership in the Space that owns the data.
  - **success:** A resource in a Space the caller holds no Membership in is indistinguishable from one that does not exist, while a resource in a Space they do belong to but lack the Role for reports a permission failure — the distinction falls at the Space boundary and nowhere below it.
- **CAP-16**
  - **intent:** Each Role grants a fixed, Space-local set of capabilities, defined once in `role-capability-matrix.md`.
  - **success:** A Viewer's write attempt is refused at the API regardless of what the interface offered, and every capability is refused to an Account holding no Membership in the Space without disclosing existence.

**Projects** — grouping, not access control. Projects have no Membership and no permissions of their own.

- **CAP-17**
  - **intent:** An Owner, Admin or Member can create, rename and delete Projects within their active Space.
  - **success:** A Viewer can do none of these, and no operation moves a Project to another Space.
- **CAP-18**
  - **intent:** Any Membership can list the Projects in its active Space.
  - **success:** The listing contains every Project in the active Space and none from any other; Viewers see the same Projects as Members.

**Tasks** — the unit of work.

- **CAP-19**
  - **intent:** An Owner, Admin or Member can create a Task within a Project.
  - **success:** A Viewer cannot, and a new Task takes the first Status in its Project's effective Status set.
- **CAP-20**
  - **intent:** An Owner, Admin or Member can change a Task's title, Status, due date and Labels.
  - **success:** A Viewer cannot change any attribute, and a Status valid in a sibling Project is refused for this one.
- **CAP-21**
  - **intent:** An Owner, Admin or Member can assign a Task to a Membership in the same Space.
  - **success:** An Account with no Membership in the Space cannot be set as Assignee by any route including the API; assigning to a Viewer is permitted and grants them no write capability.
- **CAP-22**
  - **intent:** An Owner or Admin can define the Labels available in a Space; an Owner, Admin or Member can apply them to Tasks.
  - **success:** Labels defined per Space are available to every Project in it, and no Task ever references a Label that does not exist.
- **CAP-23**
  - **intent:** An Owner, Admin or Member can delete a Task.
  - **success:** A Viewer cannot, and any active collaborative editing session on the Task terminates with participants told it was deleted rather than losing their connection silently.
- **CAP-41**
  - **intent:** An Owner, Admin or Member can move a Task — or every Task sharing one Status — to a different Project within the same Space.
  - **success:** Only Projects in the Task's own Space are accepted, including via the API; where the destination's effective Status set lacks the moving Tasks' Status, nothing takes effect without a destination Status supplied in the same operation — exactly one such decision, because a bulk move carries a single Status by construction.

**Status Configuration** — a Space defines defaults; each Project holds a delta over them.

- **CAP-24**
  - **intent:** An Owner or Admin can define the ordered default Status set for a Space.
  - **success:** Members and Viewers cannot, and the default set can never be empty — removing the last Status is refused.
- **CAP-25**
  - **intent:** An Owner or Admin can add, remove, rename and reorder Statuses within a Project, expressed as a delta over the Space defaults.
  - **success:** A Project's effective set is deterministic — the Space defaults with that Project's delta applied, in the delta's order — is never empty, and may differ from a sibling Project's simultaneously.
- **CAP-26**
  - **intent:** Removing a Status requires every Task occupying it to be mapped to another Status in the same operation.
  - **success:** No Task is ever left holding a Status absent from its Project's effective set, before, during or after the operation, and there is no partial application.
- **CAP-27**
  - **intent:** Changes to the Space default Status set reach every Project according to that Project's delta.
  - **success:** Neither half of this capability ever acts silently — a rename reports the conflict and offers to cascade where a Project renamed the same Status, and a removal reports every Project that cannot accept the Space-wide destination and requires a destination for each before anything applies.

**Board and List Views** — the two ways of looking at a Project's Tasks. Both are read-available to every Role; only the manipulation differs.

- **CAP-28**
  - **intent:** Any Membership can view a Project's Tasks as columns ordered by the Project's effective Status set.
  - **success:** Columns appear in the Project's effective order including where a delta reordered them, every Task appears in exactly one column, and a Viewer sees the identical Board with no manipulation affordances present.
- **CAP-29**
  - **intent:** An Owner, Admin or Member can move a Task between columns, changing its Status, and reorder Tasks within a column.
  - **success:** Concurrent moves by two Users converge to one order both observe, with neither User's move silently discarded, and ordering survives reload.
- **CAP-30**
  - **intent:** Any Membership can view a Project's Tasks as rows, filtered and sorted by Status, Assignee, due date and Label.
  - **success:** No filter surfaces a Task from another Project or another Space, and filtering by Assignee offers only Memberships of the active Space.

**Collaborative Task Editing** — concurrent editing is the normal case, not an error to be reported.

- **CAP-31**
  - **intent:** Multiple Users with write capability in a Space can edit the same Task description simultaneously and both retain their work.
  - **success:** Two Users editing the same region arrive at an identical final text and that text is the one persisted, with no merge prompt, lock or stale-content warning shown to anyone; a Viewer cannot enter an editing session at all, including via the API.
- **CAP-32**
  - **intent:** Users editing or viewing the same Task can see who else is there.
  - **success:** Presence shows only Memberships of the same Space, never reveals an Account's activity in any other Space, and disappears without the participant acting.
- **CAP-33**
  - **intent:** A User who loses connectivity mid-edit and returns has their local changes reconciled rather than lost or duplicated.
  - **success:** Changes made while disconnected appear exactly once after reconnection alongside changes others made during it; if reconciliation cannot complete the User is told explicitly and their unsynchronised text is not silently discarded.
- **CAP-34**
  - **intent:** A change to a Membership's Role, or its removal, takes effect on that Account's open editing sessions without requiring re-authentication.
  - **success:** On removal the session terminates and unsynchronised local changes are **not** applied; on demotion to Viewer editing ends while read access continues; no change authored after the moment of removal or demotion reaches the Space by any route including a delayed or retried synchronisation; and the effect is observable without the affected participant acting. *This is the criterion the product should be judged on — if everything else works and this does not, the isolation model is decorative.*

**Public API** — the second surface, under the same authorisation rules.

- **CAP-35**
  - **intent:** An authenticated caller can read and write Projects and Tasks in the Space its Token is bound to.
  - **success:** No operation refused in the browser succeeds via the API, nothing enumerates Spaces, Accounts or Memberships beyond the Token's Space, and Board position is readable but not writable — the single deliberate narrowing.
- **CAP-36**
  - **intent:** Any Membership can issue an API Token for the Space that Membership is in.
  - **success:** A Token reaches only the Space it names including other Spaces its issuer owns, its capability resolves to the issuing Account's Role at the time each request is evaluated rather than at issue, and it is displayed once and never retrievable afterwards.
- **CAP-37**
  - **intent:** The API is versioned, and a consumer written against one version keeps working when a newer one ships.
  - **success:** No change within a version removes a field, renames one, changes a type or narrows accepted input, and a version is announced as deprecated before it stops serving.
- **CAP-38**
  - **intent:** The API limits request rate per Token.
  - **success:** Exceeding the limit produces a distinct documented refusal stating when the caller may retry, one Space's consumption cannot exhaust another's, and a client retry never applies a write twice.

**Notifications** — email only where action outside the product is required, or where someone needs to know something happened while they were not looking.

- **CAP-39**
  - **intent:** Issuing an Invitation sends the invited address an email containing a means of accepting it.
  - **success:** The email names the Space, the Role offered and who issued it, and discloses nothing about the Space's contents, its other Members, or any other Space.
- **CAP-40**
  - **intent:** An Account assigned to a Task is notified.
  - **success:** The notification names the Space, Project and Task and nothing from any other Space, and an Account is never notified of its own action.

## Constraints

- **Authorisation is a function of `(Account, Space)`, never of Account alone, and the active Space is resolved before any authorisation decision is possible.** This rules out a uniform tenant-column filter, and forbids any requirement, story or test phrased as "an Admin can X" without naming the Space.
- **Isolation has no acceptable failure rate.** A single verified cross-Space disclosure blocks release. This makes the isolation suite a release gate rather than a report, and it holds identically for reads, writes, listings, aggregates, search results, notifications, error messages and identifiers — possessing the identifier of a Task, Project or Space confers nothing.
- **Membership is the only route into a Space.** No share link, no public Board, no read-only URL, no anonymous access.
- **An emailed acceptance route identifies an offer; it never authorises acting on it.** Following a link never mutates state by itself: acceptance requires the recipient authenticated as the addressed Account, plus a deliberate act. This rules out state-changing GETs on any invitation or offer route — which mail scanners and link prefetchers would otherwise trigger — and stops a forwarded link joining the wrong person. API Tokens are the deliberate exception, since authorising is their entire purpose (CAP-36).
- **Neither Membership nor ownership ever arrives unrequested.** Both require the receiving Account to agree: an Invitation must be accepted by the addressed Account (CAP-11), and ownership must be accepted by the named Membership (CAP-42). Nothing about another Account's action can change what an Account holds.
- **A Project never moves between Spaces and a Task never leaves its Space.** The cross-Space leak is closed by construction rather than by validation.
- **Removing a Status, at either level, requires mapping every occupying Task in the same atomic operation.** Removal is always a migration, which makes "a Task holding a Status its Project does not expose" unreachable rather than merely invalid.
- **A Project's Status delta references Statuses by identity, not by name.** Forced by CAP-27: the system cannot report that a Project renamed *the same* Status the Space is now renaming unless it can tell they are the same object. Name-keyed deltas cannot express this.
- **An API Token is bound to exactly one Space at issue, and its capability resolves at request time.** A script working across three Spaces holds three Tokens. This is deliberate friction — it makes cross-Space reach impossible to express rather than merely forbidden, and stops a Token outliving the permission that justified it.
- **The synchronisation channel carries no authority and cannot authorise once at connection time.** CAP-34 places a permission change inside a pipeline that is by design tolerant of delay and reordering, so every inbound frame is authorised.
- **Whole-field last-writer-wins is not an admissible merge for a Task description.** CAP-31 requires two Users editing the same region to reach identical persisted text with no merge prompt. Adopting last-writer-wins would amend CAP-31 and CAP-33, not implement them.
- **Deletion of an Account, Space, Project or Task is irreversible in v1.** No trash, no restore, no undo — including on the most destructive operation in the product. Backups exist for disaster recovery and are not a restore path for deliberate deletion.
- **Total running cost stays under £30 per month at the scale envelope in `quality-budgets.md`.** This rules out always-on dedicated infrastructure per Space or per active editing session, which is where the real-time requirements will push hardest. A design that cannot be costed against the ceiling has not been specified enough to accept.
- **An Account's existence is never disclosed to anyone not given it.** Registration, authentication and invitation-issue responses are uniform whether or not the address is known to Yello, and nobody can enumerate the Spaces another Account belongs to — including a Space's Owner. Email addresses are visible to Owners and Admins of Spaces the Account is a Member of, and to nobody else.
- **Yello collects no behavioural analytics on the contents of Spaces.** This rules out any instrumentation that reads Task titles, descriptions, Labels or Project names.
- **No product surface aggregates across Spaces, and the behavioural measures are not a product feature.** The measures in `success-metrics.md` are computed by the operator querying the datastore directly, outside the request path and outside the authorisation model. This rules out an in-product metrics dashboard, an admin analytics view, and any endpoint returning a count spanning Spaces — each would require a third non-Space-scoped surface and would breach isolation to produce a number nobody is entitled to.
- **Compaction of the Task description change log preserves per-author change counts and timestamps.** Compaction may discard change *content*; it may not discard the record that a given Membership changed a given description at a given time. That metadata is the only evidence SM-5 is derivable from, and once compacted away it is unrecoverable.
- **Capabilities the acting Role lacks are absent from the interface, not present-and-failing, and the acting Role is legible at all times.** Separately and independently: the interface hiding an action is never the mechanism that enforces it. Every refusal is enforced server-side and identically on both surfaces.
- **Credentials are never recoverable and never observable.** Passwords are stored using a deliberately slow one-way function; API Tokens are stored so a read of the datastore does not yield usable Tokens; neither appears in any log, error message, notification, analytics event or API response.
- **Glossary terms are used verbatim in every downstream artifact.** A synonym is a discipline violation, not a style choice. Where you see **Space**, no document says "workspace", "tenant" or "org".
- **v1 is a single-operator deployment and claims no data-protection posture.** No lawful basis, data region, encryption-at-rest assertion, breach position or subject-access route is specified, and none is required while the operator is the only data subject. The gate is testable rather than aspirational: **the first Account created by anyone other than the operator makes this spec non-compliant until amended.** `harness-constraints.md` records what the gate requires, and which protections already hold incidentally.
- **Authentic complexity only.** No requirement exists in order to reach a BMad surface. A coverage gap is reported rather than closed with a contrived carrier — which is why third-party failure handling is left openly uncovered in `harness-constraints.md` rather than manufactured.

## Non-goals

**Ruled out permanently — a future version does not correct these.**

- **Yello is not an organisation-management product.** No company, no billing entity, no directory, no administrator with authority across Spaces they are not in. This rules out enterprise SSO and directory sync, which presuppose an organisation that owns accounts.
- **Yello is not a project management tool in the formal sense.** No dependencies, no critical path, no Gantt, no resource levelling, no effort or cost reporting.
- **Yello does not become a communication tool.** No chat, no threads, no direct messages. Notifications exist to bring people back, not to hold conversations.
- **Yello does not federate.** No cross-Space views, no aggregate dashboards, no "all my Tasks everywhere". This is not an omission to be corrected later — a surface spanning Spaces contradicts the model the product is built on.
- **Yello is not offline-first.** It assumes connectivity and degrades honestly without it.
- **Yello has no public or anonymous access.** Membership is the only route in.

**Not the audience.** A boundary on who Yello is for, distinct from what it will not do.

- **Enterprises requiring centralised provisioning.** Nobody in Yello can create, disable or reach into another person's Account, so an organisation wanting IT-governed access to its people's work cannot get it here.
- **Regulated environments with data residency or retention mandates.** No residency controls, no legal hold, no compliance attestations. *See the data-protection open question.*
- **Teams needing formal project management.** Yello records what work exists and who holds it, not how long it takes or what it costs.
- **Anyone needing offline-first operation.** Yello tolerates a brief disconnection mid-edit (CAP-33) and otherwise assumes connectivity.

**Out of scope for v1 — deferred, not rejected.**

- Iteration planning: no cycles, sprints, backlogs or time-boxing. Retained as a *second* mid-flight change if a wide-ripple comparison is wanted later; not scheduled.
- OAuth sign-in, as an alternative to email and password. Distinct from enterprise SSO because it authenticates an individual rather than presupposing an organisation. **Selected as the P6 mid-flight change and scheduled to fire once the identity epic has shipped**, closing the third-party-integration coverage gap at the same time. Stories for CAP-1, CAP-2 and NFR-6 must leave the assumptions it breaks soft — enumerated in `harness-constraints.md`.
- Task comments and activity history — no discussion thread, no audit trail of who changed what. *The most likely regret among these; worth revisiting if timeline permits.*
- Subtasks and Task relationships: no hierarchy, no blocking, no linking.
- Attachments: no file upload or storage.
- Cross-Project search. Search is scoped to a Project in v1; any future implementation inherits the isolation constraint in full.
- Webhooks and outbound integrations. The API is inbound only.
- Custom fields: Task shape is fixed.
- Recurring Tasks.
- Mobile applications. The web interface is responsive; there is no native client.
- Notification preferences. Notifications are per-event and not configurable.
- Session telemetry. Time-in-application (SM-C2) is defined as a counter-metric but not measurable in v1, because nothing records session duration and nothing is added to.
- Billing and plan limits: no monetisation of any kind in v1.

## Success signal

Ravi holds three Memberships at three different Roles and works across all of them in one morning. He creates freely in the Space he owns; he manages Membership in the client engagement he administers; and in the third — where he is a Viewer — every affordance to create or change anything is *absent* rather than present-and-failing, so he reads his standing off the interface without attempting an action. Then, while Nadia has that Space's Task description open with a sentence she has not finished, he removes her: within a second her editor goes inert, the unsynchronised sentence never reaches the Space, and she is told her access has ended — without her having touched anything. Her next ordinary request, had she made one, would already have been refused. An isolation suite run against the same build reports zero cross-Space disclosures across browser and API.

Two gating criteria; a release fails without them.

- **Isolation integrity.** Zero verified cross-Space disclosures, across browser and API, in any released build, measured by an isolation test suite exercised on every change. Validates CAP-15, CAP-16, CAP-35, CAP-36.
- **Revocation latency.** Permission changes govern the affected Account's very next request with no tolerance, and take effect on open live sessions within 1 second, in 100% of tested cases — including sessions holding unsynchronised local edits. Validates CAP-34, NFR-2.

Behavioural measures are defined without thresholds in `success-metrics.md`: Yello has no users, and a target invented now would be indistinguishable from one that had been earned.

## Assumptions

- Where the architecture spine settled a question the PRD or its addendum left open, the spine is authoritative and this spec does not reopen it. Four such questions were closed after the PRD was written: whether the effective Status set is derived or materialised, where authorisation sits in the real-time synchronisation path, how Board ordering converges, and the atomicity of Status removal and cross-Project Task move.
- The auto-provisioned Space is named from the Account's display name and is immediately renameable.
- Space deletion, Project deletion and Task deletion are immediate and irreversible; no trash, no restore window.
- Admins cannot change each other's Role — only the Owner can promote to or demote from Admin. Asserted to keep the Owner meaningfully distinct from Admin; recorded as an assumption rather than a settled decision.
- The default Space Status set is Todo / In Progress / Done.
- The CAP-27 rename cascade offer is a single choice applied to every conflicting Project at once, consistent with the single Space-wide mapping decision.
- The Invitation acceptance route expires after 7 days, after which the Invitation must be reissued.
- An Ownership Offer expires after 7 days, mirroring the Invitation, and is surfaced in Space settings rather than emailed — the recipient is already a Member of the Space. Adding an email for it would be a new notification capability, deliberately not taken.
- Assignment notification is email, per-event rather than digested.
- The scale bounds in `quality-budgets.md` are set by judgement rather than measurement, and are **confirmed final for v1**. With no users there is no usage evidence to gather, so the only obtainable evidence is load testing; that verification is scheduled at the NFR-evidence audit, against the single choke point the architecture enforces them at. Revising a bound after that point is an architecture change, not a document edit.
- Three further assumptions have hardened into architecture and now cost more than a document edit to reverse: API version by URL path segment with exactly two concurrent versions; 90-day retention of authorisation refusal records; and the £30 ceiling being set by what the project is worth spending rather than by pricing analysis. All three remain unconfirmed.

## Open Questions

*None. All twelve questions this spec opened with were resolved on 2026-08-18 — eleven by decision, and the twelfth reclassified as a hand-off obligation on the epics phase (recorded in `harness-constraints.md`) because nothing about it was undecided. The resolutions and their rejected alternatives are folded into `prd.md` §11 and `addendum.md` §8; the decision trail is `.memlog.md`.*

*One item remains genuinely unresolved but is **owned by the architecture spine**, not by this spec: whether NFR-5 is measured warm or cold.*
