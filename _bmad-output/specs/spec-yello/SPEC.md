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

# Yello — multi-Space task management

## Why

**A vision to realize.** Every task tool makes you choose a container before you have anything to put in it. A personal to-do app assumes you work alone; a team workspace assumes an organisation with a billing entity and an admin who provisions accounts; a client portal assumes an outside party to wall off. Pick one and the others turn awkward — a freelancer runs three tools or bends one into a shape it resists. Yello's bet is that all three are the same primitive: a **Space** is a container for work and the boundary of who can see it, and nothing more is required of it. What follows from that is the part that matters — the interesting user holds several Memberships at once with different standing in each, so identity is global and permission is contextual, and every question about what someone may do is answered against a specific Space, never against the person. The people affected are the freelancer, the small studio, and the client who wants to watch work land without being able to change it. That single constraint shapes every decision downstream, and a release either proves the thesis or disproves it.

**A mandate the project set for itself.** Yello is a test harness for the BMad method, not a commercial product: no users, no market, no deadline. Its requirements were derived from methodology coverage rather than user demand — a feature earns its place by forcing part of BMad to activate against authentic complexity, never by being bolted on to reach a skill. Two consequences bind every downstream trade-off. First, v1 is built at launch rigour despite having no users, because the downstream phases are the real consumer; a proposal to simplify by cutting the API, the collaborative editor or the falsifiable quality budgets destroys the point of the exercise rather than saving effort. Second, v1 is deliberately not frozen — a genuinely wanted requirement is held in reserve to be introduced mid-flight. Both are specified in `harness-constraints.md`.

## Capabilities

Capability IDs mirror the source PRD's functional requirement numbers one-to-one: **CAP-N is FR-N**. Numbers are identifiers, not positions — CAP-41 belongs to Tasks and sits out of sequence for that reason. Each `success:` below is the *decisive* criterion; the complete testable consequence list for every capability is in `acceptance-criteria.md`.

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
  - **intent:** An Owner can transfer ownership of a Space to another Membership in that Space.
  - **success:** At no point during the operation does the Space have zero Owners or two, and the previous Owner becomes an Admin without losing access.
- **CAP-9**
  - **intent:** An Account can move between the Spaces it holds Membership in, and the active Space determines everything subsequently visible and permitted.
  - **success:** Only Spaces the acting Account holds Membership in are listed by any means, and a request with no resolvable Space context is refused rather than defaulted.

**Membership and Invitations** — the only mechanism by which an Account gains access to a Space.

- **CAP-10**
  - **intent:** An Owner or Admin can invite an email address to a Space at a specified Role, whether or not that address has ever heard of Yello.
  - **success:** The response to the issuer is identical whether or not the address corresponds to an existing Account, and no Invitation can be issued at Owner Role.
- **CAP-11**
  - **intent:** An invited person can accept, gaining Membership at the Role the Invitation specified.
  - **success:** Accepting creates exactly one Membership, in exactly the invited Space, at exactly the invited Role; an invitee with an existing Account joins with it, and their other Memberships are neither visible to nor affected by the inviter.
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
  - **intent:** An Owner, Admin or Member can move a Task to a different Project within the same Space.
  - **success:** Only Projects in the Task's own Space are accepted including via the API; where the destination's effective Status set lacks the Task's Status, the move does not take effect without a destination Status supplied in the same operation.

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
  - **success:** A Space-level rename applies to every Project that has not itself renamed that Status, and where one has, the operation reports the conflict and offers to cascade rather than silently overwriting or silently skipping.

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
- **A Project never moves between Spaces and a Task never leaves its Space.** The cross-Space leak is closed by construction rather than by validation.
- **Removing a Status, at either level, requires mapping every occupying Task in the same atomic operation.** Removal is always a migration, which makes "a Task holding a Status its Project does not expose" unreachable rather than merely invalid.
- **A Project's Status delta references Statuses by identity, not by name.** Forced by CAP-27: the system cannot report that a Project renamed *the same* Status the Space is now renaming unless it can tell they are the same object. Name-keyed deltas cannot express this.
- **An API Token is bound to exactly one Space at issue, and its capability resolves at request time.** A script working across three Spaces holds three Tokens. This is deliberate friction — it makes cross-Space reach impossible to express rather than merely forbidden, and stops a Token outliving the permission that justified it.
- **The synchronisation channel carries no authority and cannot authorise once at connection time.** CAP-34 places a permission change inside a pipeline that is by design tolerant of delay and reordering, so every inbound frame is authorised.
- **Whole-field last-writer-wins is not an admissible merge for a Task description.** CAP-31 requires two Users editing the same region to reach identical persisted text with no merge prompt. Adopting last-writer-wins would amend CAP-31 and CAP-33, not implement them.
- **Deletion of an Account, Space, Project or Task is irreversible in v1.** No trash, no restore, no undo — including on the most destructive operation in the product. Backups exist for disaster recovery and are not a restore path for deliberate deletion.
- **Total running cost stays under £30 per month at the scale envelope in `quality-budgets.md`.** This rules out always-on dedicated infrastructure per Space or per active editing session, which is where the real-time requirements will push hardest. A design that cannot be costed against the ceiling has not been specified enough to accept.
- **An Account's existence is never disclosed to anyone not given it.** Registration, authentication and invitation-issue responses are uniform whether or not the address is known to Yello, and nobody can enumerate the Spaces another Account belongs to — including a Space's Owner. Email addresses are visible to Owners and Admins of Spaces the Account is a Member of, and to nobody else.
- **Yello collects no behavioural analytics on the contents of Spaces.** This rules out any instrumentation that reads Task titles, descriptions, Labels or Project names, and it is the constraint the instrumentation open question runs into.
- **Capabilities the acting Role lacks are absent from the interface, not present-and-failing, and the acting Role is legible at all times.** Separately and independently: the interface hiding an action is never the mechanism that enforces it. Every refusal is enforced server-side and identically on both surfaces.
- **Credentials are never recoverable and never observable.** Passwords are stored using a deliberately slow one-way function; API Tokens are stored so a read of the datastore does not yield usable Tokens; neither appears in any log, error message, notification, analytics event or API response.
- **Glossary terms are used verbatim in every downstream artifact.** A synonym is a discipline violation, not a style choice. Where you see **Space**, no document says "workspace", "tenant" or "org".
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

- Iteration planning: no cycles, sprints, backlogs or time-boxing. **Held as a mid-flight change candidate** — see `harness-constraints.md`.
- OAuth sign-in, as an alternative to email and password. Distinct from enterprise SSO because it authenticates an individual rather than presupposing an organisation. **Held as a mid-flight change candidate**, and the intended closure of the third-party-integration coverage gap.
- Task comments and activity history — no discussion thread, no audit trail of who changed what. *The most likely regret among these; worth revisiting if timeline permits.*
- Subtasks and Task relationships: no hierarchy, no blocking, no linking.
- Attachments: no file upload or storage.
- Cross-Project search. Search is scoped to a Project in v1; any future implementation inherits the isolation constraint in full.
- Webhooks and outbound integrations. The API is inbound only.
- Custom fields: Task shape is fixed.
- Recurring Tasks.
- Mobile applications. The web interface is responsive; there is no native client.
- Notification preferences. Notifications are per-event and not configurable.
- Billing and plan limits: no monetisation of any kind in v1.

## Success signal

Ravi holds three Memberships at three different Roles and works across all of them in one morning. He creates freely in the Space he owns; he manages Membership in the client engagement he administers; and in the third — where he is a Viewer — every affordance to create or change anything is *absent* rather than present-and-failing, so he reads his standing off the interface without attempting an action. Then, while Nadia has that Space's Task description open with a sentence she has not finished, he removes her: within five seconds her editor goes inert, the unsynchronised sentence never reaches the Space, and she is told her access has ended — without her having touched anything. An isolation suite run against the same build reports zero cross-Space disclosures across browser and API.

Two gating criteria; a release fails without them.

- **Isolation integrity.** Zero verified cross-Space disclosures, across browser and API, in any released build, measured by an isolation test suite exercised on every change. Validates CAP-15, CAP-16, CAP-35, CAP-36.
- **Revocation latency.** Permission changes take effect on live sessions within 5 seconds in 100% of tested cases, including sessions holding unsynchronised local edits. Validates CAP-34.

Behavioural measures are defined without thresholds in `success-metrics.md`: Yello has no users, and a target invented now would be indistinguishable from one that had been earned.

## Assumptions

- Where the architecture spine settled a question the PRD or its addendum left open, the spine is authoritative and this spec does not reopen it. Four such questions were closed after the PRD was written: whether the effective Status set is derived or materialised, where authorisation sits in the real-time synchronisation path, how Board ordering converges, and the atomicity of Status removal and cross-Project Task move.
- The auto-provisioned Space is named from the Account's display name and is immediately renameable.
- Space deletion, Project deletion and Task deletion are immediate and irreversible; no trash, no restore window.
- Admins cannot change each other's Role — only the Owner can promote to or demote from Admin. Asserted to keep the Owner meaningfully distinct from Admin; recorded as an assumption rather than a settled decision.
- The default Space Status set is Todo / In Progress / Done.
- The CAP-27 rename cascade offer is a single choice applied to every conflicting Project at once, consistent with the single Space-wide mapping decision.
- Where a Space-wide mapping destination does not exist in a given Project's effective set, that Project's affected Tasks fall to the first Status in its own effective set. This is the one place the Space-wide mapping decision can produce a result the Admin did not literally choose.
- The Invitation acceptance route expires after 7 days, after which the Invitation must be reissued.
- Assignment notification is email, per-event rather than digested.
- Four assumptions have since hardened into architecture and now cost more than a document edit to reverse: API version by URL path segment with exactly two concurrent versions; 90-day retention of authorisation refusal records; the scale bounds in `quality-budgets.md` being set by judgement rather than measurement; and the £30 ceiling being set by what the project is worth spending rather than by pricing analysis. All four remain unconfirmed.

## Open Questions

- **Two source claims contradict each other and the spec does not choose between them.** UJ-4's edge case (in `surfaces-and-journeys.md`) states that a deep link into a Space the Account was removed from tells them they no longer have access, "not that the Task does not exist". CAP-15 requires the opposite — indistinguishable from not-found — and the architecture spine's AD-3 has already implemented CAP-15 as a hard 404 carrying no existence hint. The cases may be separable: CAP-34 requires a *live* session to be told access has ended, over a connection already authorised, whereas a *cold* deep link after removal has no such session and falls to CAP-15. If that split is intended, UJ-4 describes the cold case using the live case's behaviour and should be corrected. The question to settle: **may a removed Account learn that the resource still exists?**
- **Instrumentation has no capability carrying it.** No CAP authorises collecting the behavioural measures in `success-metrics.md`, and one constraint states Yello collects no behavioural analytics on the contents of Spaces. Is instrumentation in v1 scope at all, and does measuring *that* a description was co-edited fall inside or outside that constraint?
- **The scale bounds revisit is overdue, not pending.** The PRD asked that the bounds be revisited with evidence *before* the architecture was shaped around them. The architecture now enforces every bound as a refusal at one choke point, so that ordering was missed. Confirm the bounds or revise them knowing the cost has changed.
- **No data-protection posture is named.** v1 stores email addresses and user-authored content, and deletion is irreversible; there is no stated lawful basis, no erasure route beyond CAP-3, and no breach-notification position. Deliberate for a harness with no users, or a gap the moment Yello is exposed publicly?
- **Which mid-flight change fires, and when?** Two genuinely wanted requirements are held outside v1 for this purpose. The choice determines which v1 assumptions must stay soft, so it is needed before sprint planning rather than after.
- **Is the product name settled?** The source PRD still carries "*Working title — confirm*". Every artifact now says Yello; confirm or rename before epics fix it into story text.
- **Can ownership be transferred to someone who then declines it?** CAP-8 transfers immediately with no acceptance step, so the recipient acquires sole right to delete a Space without agreeing to it.
- **Should acceptance of an Invitation by an existing Account require confirmation?** CAP-11 joins them silently, changing someone's working environment without their say-so.
- **Is 5 seconds the right revocation budget?** It is stated and gated on, but nothing validates it. For a Viewer who should never have seen a Task, five seconds may be four too many.
- **Should a Space-level Status removal be possible at all when Projects have diverged significantly?** CAP-27 resolves it with a single Space-wide destination plus a fallback, but the fallback can place a Task where the Admin did not choose.
- **Does CAP-41 need a bulk form?** Moving Tasks one at a time between Projects is tedious at any real volume, and a bulk move interacts awkwardly with the per-Task Status mapping the single-Task version requires.
- **Property carriers are named at feature and FR level, not epic level**, because epics did not exist when they were assigned. The mapping needs revisiting once epics exist, since a feature may split across several.
