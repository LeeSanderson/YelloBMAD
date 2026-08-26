---
title: Yello — Epic Breakdown
status: final
created: 2026-08-22
updated: 2026-08-26
amendments:
  - date: 2026-08-26
    section: 'Epic 1 › Story 1.1 › Acceptance Criteria, fifth block (AC5)'
    raisedBy: 'Code review of story 1.1, 2026-08-26 — decision resolved by Lee'
    change: >-
      AC5 required the four gating suites, architecture INCLUDED, to report zero tests, while
      the second and third AC blocks require that same architecture suite to fail the build on
      a ring or Role-API violation. The two cannot both hold: a suite that fails the build has
      run assertions. The implementation read AC5's operative contrast as "rather than failing
      to build" and gave the exit-code-8 tolerance to the four genuinely-empty suites while
      deliberately withholding it from architecture, which is the only coherent reading.
      Reworded so the operative test is "builds and executes", with the zero-tests clause
      scoped to the suites that actually hold no cases. Amended deliberately against a final
      artifact rather than re-litigated once per story.
stepsCompleted: ['step-01-validate-prerequisites', 'step-02-design-epics', 'step-03-create-stories']
readinessCheck:
  report: _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md
  verdict: 'READY — 43/43 FR coverage, no critical violations'
  remediationApplied: 2026-08-22
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md
  - _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md
---

# Yello - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Yello, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

All four input spines carry `status: final` and were mutually reconciled on 2026-08-20. Vocabulary is PRD §2 Glossary verbatim — **Account**, **User**, **Space**, **Personal Space**, **Membership**, **Role**, **Owner**, **Admin**, **Member**, **Viewer**, **Invitation**, **Ownership Offer**, **Project**, **Task**, **Status**, **Assignee**, **Label**, **Board**, **List View**, **Presence**, **API Token**, **Session**. Synonyms are a discipline violation in every story written below.

## Requirements Inventory

### Functional Requirements

*43 FRs, numbered globally in the PRD so the numbers are identifiers, not positions — FR-41, FR-42 and FR-43 sit out of sequence within their features by design.*

**4.1 Accounts and Authentication**

FR-1: A person can create an Account with an email address and password; the address uniquely identifies it across Yello. Registering an existing address is indistinguishable from registering a new one. Registration completes with exactly one Space in existence for that Account, owned by it.
FR-2: An Account can authenticate and receive a Session that persists across requests and across every Space it belongs to. A Session carries no Role, Space or capability of its own; switching Space never re-authenticates; an expired Session grants access to nothing.
FR-3: An Account can delete itself. Refused while it owns any Space. On deletion every Membership is removed, Tasks it was Assignee of become unassigned, authored content in Spaces it did not own is retained under a deleted-Account attribution, and the address is freed for reuse with no inherited history.

**4.2 Spaces**

FR-4: The system provisions a Space for a newly registered Account and makes that Account its Owner. Usable the moment registration completes; no attribute distinguishes it from a Space created by FR-5.
FR-5: An authenticated Account can create additional Spaces without limit. Creator becomes Owner; new Space has exactly one Membership, the default Status set, and no Projects.
FR-6: An Owner or Admin can rename a Space. Members and Viewers cannot. Names are not unique across Yello and collision discloses nothing.
FR-7: An Owner can delete a Space, destroying its Projects, Tasks, Memberships and Invitations. Admins cannot. Invalidates every API Token for that Space. An Account may delete its last Space — belonging to no Space is a valid state.
FR-8: An Owner can offer ownership to another Membership in that Space at any Role. Ownership does not move until accepted. At most one offer pending per Space. Revocable by the offering Owner; lapses if the recipient's Membership ends. Making an offer is not itself an exit. Expires after 7 days.
FR-42: The named Membership can accept an Ownership Offer, becoming Owner, or decline it. Only that Membership, only while pending. Acceptance moves ownership in one atomic step — recipient becomes sole Owner, previous Owner becomes Admin — with never zero or two Owners. No Account becomes Owner without agreeing, by any route including the API. Declined or lapsed offers cannot later be accepted.
FR-9: An Account can move between the Spaces it holds Membership in; the active Space determines everything subsequently visible and permitted. Only Spaces held are listed — no directory, no search, no enumeration beyond Membership. Every Space-scoped request resolves an active Space before authorisation; no resolvable context means refusal, never a default.

**4.3 Membership and Invitations**

FR-10: An Owner or Admin can invite an email address to a Space at a specified Role. Any address, no domain restriction. Exactly one Space and one Role fixed at issue. Never issuable at Owner Role. Refused for an address already holding Membership there. The response never discloses whether the address has an Account. Terminal state is retained rather than deleted.
FR-11: An invited person can accept, gaining Membership at the invited Role. The token identifies the offer and never authorises acceptance — a bare fetch creates nothing. Acceptance requires authentication as the addressed Account plus a deliberate act. An invitee with no Account registers as that act, which independently provisions their own Personal Space. Accepted once only. A revoked Invitation reports only that it is no longer valid.
FR-12: An Owner or Admin can revoke an Invitation before acceptance. A revoked Invitation can never be accepted. An Invitation survives its issuer's demotion, removal or Account deletion, and any remaining Owner or Admin may revoke it.
FR-13: An Owner or Admin can change a Membership's Role within their own Role's constraints. Admins may change Member↔Viewer only; only an Owner may promote to or demote from Admin. No change produces a second Owner or removes the sole Owner. Takes effect on the target's active Sessions without re-authentication.
FR-14: An Owner or Admin can remove a Membership; any Account can remove its own. Admins cannot remove the Owner or another Admin. The Owner's Membership cannot be removed by anyone, including the Owner, while it holds ownership. Removal revokes access immediately, reaches active Sessions and open editors, invalidates that Account's API Tokens for the Space, and unassigns its Tasks without deleting them.

**4.4 Access Control**

FR-15: Every read and write of Space-scoped data is authorised against the acting Account's Membership in the Space owning the data. No request returns data from a Space held without Membership — by identifier, deep link, search, API, listing or aggregate. A resource in a Space with no Membership is indistinguishable from one that does not exist. A resource in a Space held but lacking the Role reports a permission failure. Authorisation is evaluated per request, never cached across a switch or inferred.
FR-16: Each Role grants a fixed, Space-local capability set per the PRD §4.4 matrix, which is the single source of truth. A Viewer's write is refused at the API regardless of what the interface offered. An API Token matches its Account's Role at each request evaluation, not at issue. Every capability is refused for an Account with no Membership, without disclosing existence.

**4.5 Projects**

FR-17: An Owner, Admin or Member can create, rename and delete Projects in the active Space. Viewers cannot. A Project belongs to exactly one Space, fixed at creation, and never moves. A new Project's effective Status set is the Space default set with an empty delta. Deleting a Project deletes its Tasks, immediately and irreversibly.
FR-18: Any Membership can list the Projects in its active Space. The listing contains every Project in that Space and none from any other. Viewers see the same Projects as Members.

**4.6 Tasks**

FR-19: An Owner, Admin or Member can create a Task within a Project. Viewers cannot. A new Task takes the first Status in its Project's effective set. It belongs to exactly one Project at a time and one Space permanently.
FR-20: An Owner, Admin or Member can change a Task's title, Status, due date and Labels. Viewers cannot change any attribute. A Status can only be set to a value in the Project's effective set; a Status valid in a sibling Project is refused.
FR-21: An Owner, Admin or Member can assign a Task to a Membership in the same Space. Only Memberships of the Task's Space are offered or accepted, including via the API. Removal or Account deletion unassigns without deleting. Assigning a Viewer is permitted and grants them nothing; demoting an Assignee to Viewer does not unassign.
FR-22: An Owner or Admin can define the Labels available in a Space; an Owner, Admin or Member can apply them. Labels are per Space and available to every Project in it. A Label in use cannot be deleted without its applications being removed.
FR-23: An Owner, Admin or Member can delete a Task. Viewers cannot. Deletion terminates any active collaborative editing session on it, and participants are told it was deleted rather than losing their connection silently.
FR-41: An Owner, Admin or Member can move a Task to a different Project in the same Space. Only same-Space Projects are offered or accepted, including via the API. Where the Status is absent from the destination's effective set a destination Status must be supplied in the same operation; where present it is preserved. Assignee, Labels, due date and description survive; an active editing session continues across the move. **Bulk form:** every Task in one Status moves in a single atomic operation carrying exactly one mapping decision, reachable from a Board column, from a List View filtered to one Status, and from the API. Refused rather than partially applied, visibly.

**4.7 Status Configuration**

FR-24: An Owner or Admin can define the ordered default Status set for a Space. Members and Viewers cannot. A new Space has a non-empty default set (Todo / In Progress / Done); removing the last Status is refused.
FR-25: An Owner or Admin can add, remove, rename and reorder Statuses within a Project as a delta over the Space defaults. Members and Viewers cannot. The effective set is deterministic and can never be empty. Two Projects in one Space may hold different effective sets simultaneously. A removed Status can be re-added at any time.
FR-26: Removing a Status requires every occupying Task to be mapped to another Status in the same operation. No partial application. No Task is ever left holding a Status absent from its Project's effective set — before, during or after. Removing an unoccupied Status needs no mapping. The destination must exist in the post-removal effective set.
FR-27: Changes to the Space default set reach every Project according to its delta. Adding adds to every Project that has not removed it. Renaming renames in every Project that has not itself renamed it. Where Projects have renamed the same Status the operation reports the conflict and offers one cascade decision applied to all at once. Removing requires mapping under FR-26: one Space-wide destination, plus a per-Project destination — with affected Task counts reported — for every Project whose post-removal effective set cannot accept it. No fallback, no silent placement, nothing applies until every reported Project has a destination, and the whole thing commits as one transaction. Both halves report and ask; neither decides for the Admin.

**4.8 Board and List Views**

FR-28: Any Membership can view a Project's Tasks as columns ordered by the Project's effective Status set. Columns appear in effective order including reorderings. Every Task appears in exactly one column. A Viewer sees the identical Board with no manipulation affordances present. Holds NFR-5 and NFR-9 at NFR-8's 5,000 Tasks with every Task reachable.
FR-29: An Owner, Admin or Member can move a Task between columns, changing its Status, and reorder within a column. Viewers cannot. Concurrent moves by two Users converge to one order both observe with neither silently discarded. Ordering is per column and survives reload.
FR-30: Any Membership can view a Project's Tasks as rows filtered and sorted by Status, Assignee, due date and Label. Filters never surface a Task from another Project or Space. Filtering by Assignee offers only Memberships of the active Space. Holds NFR-5 and NFR-9 at 5,000 Tasks on the same terms as FR-28.

**4.9 Collaborative Task Editing**

FR-31: Multiple Users with write capability can edit the same Task description simultaneously. Edits to different regions both survive; edits to the same region converge to one identical persisted text. No merge prompt, lock or stale-content warning during normal concurrent editing. A Viewer cannot enter an editing session at all, including via the API.
FR-32: Users editing or viewing the same Task can see who else is there. Presence shows only Memberships of the same Space, disappears within NFR-3's interval after disconnection without the participant acting, and never reveals an Account's activity in any other Space.
FR-33: A User who loses connectivity mid-edit and returns has local changes reconciled rather than lost or duplicated — applied exactly once, with others' changes present. If reconciliation cannot complete the User is told explicitly and unsynchronised text is not silently discarded.
FR-34: A Role change or Membership removal takes effect on that Account's open editing sessions without re-authentication. On removal the session terminates, unsynchronised local changes are **not** applied, and the User is told access has ended. On demotion to Viewer editing ends while read access continues uninterrupted. Nothing authored after the change reaches the Space by any route including a delayed or retried synchronisation. Already-synchronised changes are retained. The effect is observable without the affected participant acting. *The PRD names this the criterion the product should be judged on.*

**4.10 Public API**

FR-35: An authenticated caller can read and write Projects and Tasks in the Space its Token is bound to. Every FR-16 capability is enforced identically; no operation refused in the browser succeeds via the API. No operation enumerates Spaces, Accounts or Memberships beyond the Token's Space. A Task's position within its Status is readable and deliberately not writable.
FR-36: Any Membership can issue an API Token for the Space that Membership is in. Bound to one Space at issue, immutably. Reaches only that Space, including Spaces its creator owns. Effective capability is the issuing Account's Role in that Space at each request evaluation. Invalidated by Membership removal, Space deletion or Account deletion. Displayed once at creation and never retrievable.
FR-37: The API is versioned and a consumer written against one version keeps working when a newer one ships. No change within a version removes, renames or retypes a field or narrows accepted input. Deprecation is announced before withdrawal and the version serves throughout. Version selected by URL path segment, exactly two supported concurrently.
FR-38: The API limits request rate per Token. Exceeding produces a distinct documented refusal a client can detect, stating when to retry. Partitioned per Token so one Space cannot exhaust another's. A retry never applies a write twice.

**4.11 Notifications**

FR-39: Issuing an Invitation sends an email containing a means of accepting it. The email names the Space, the Role and the issuer, and discloses nothing about the Space's contents, other Members or any other Space. Following the acceptance route does not join the invitee. The route expires after 7 days.
FR-40: An Account assigned to a Task is notified. The notification names the Space, Project and Task and nothing from any other Space. An Account is not notified of its own action. Email, per-event, not digested.
FR-43: Issuing an Ownership Offer sends one email to the named recipient. It names the Space, states ownership is being offered and identifies who offered it, and discloses nothing about the Space's contents, its other Memberships or any other Space. It carries no means of accepting — acceptance happens inside the Space under FR-42. Exactly one email per offer; nothing on revoke, decline or lapse.

### NonFunctional Requirements

NFR-1: **Isolation is absolute.** No data belonging to a Space reaches any Account without a Membership in it, by any route. Holds identically for browser and API, and for reads, writes, listings, aggregates, search results, notifications and error messages. Holds for identifiers — possessing one confers nothing. Holds under error: no failure, timeout or partial response discloses data or existence across a Space boundary. **The one requirement with no acceptable failure rate; a single verified cross-Space disclosure blocks release.**
NFR-2: **Authorisation is evaluated fresh, per request.** On the request path, a Role change or Membership removal governs the affected Account's **very next request, with no tolerance** — no cache may outlive a request. On the live-session path, within **1 second** of the transaction boundary, without the affected Account acting. FR-34's guarantee is independent of both timings. No request is authorised using a Role from a previous active Space. Applies to API Tokens on the same terms.
NFR-3: **Collaborative editing feels immediate.** Local edit renders within **16 ms** without a network round trip. A remote participant's edit renders within **300 ms p95** on a 50 ms RTT connection. Presence appears within **2 s** of arrival and disappears within **10 s** of departure.
NFR-4: **Concurrent edits converge.** All participants observe identical text within **2 s** of the last edit. Holds for at least **10 simultaneous editors** on one Task description. A participant disconnected up to **5 minutes** reconciles without loss or duplication.
NFR-5: **The API is predictable.** Reads within **300 ms** and writes within **500 ms**, both p95, measured server-side within NFR-8. Every refusal carries a machine-readable reason; no client parses prose. Retrying a timed-out write does not apply it twice.
NFR-6: **Credentials are held safely.** Passwords stored with a deliberately slow one-way function, never recoverable, work factor tunable without re-registering existing Accounts. API Tokens stored so a datastore read yields no usable Token, displayed exactly once. No password or Token in any log, error message, notification, analytics event or API response. All traffic encrypted in transit. Encryption at rest is explicitly not required here.
NFR-7: **Refusals are observable.** Every authorisation refusal is recorded with the acting Account, target Space, capability attempted and outcome. Cross-Space attempts are distinguishable in the record from within-Space permission failures. Retained 90 days.
NFR-8: **Scale envelope.** Spaces per Account 50 · Memberships per Space 100 · Projects per Space 50 · Tasks per Project 5,000 · concurrent editors per Task 10 · concurrent active Sessions per Space 50. Guarantees hold inside these bounds and are not required beyond. Exceeding a bound degrades visibly — a refusal, never a wrong answer. **A bound that is not enforced is a defect, not a relaxation.** Confirmed final for v1; verification by load testing at the NFR-evidence audit.
NFR-9: **The primary flows are accessible.** Registration, Space switching, the Board, the Task editor and the invitation flow meet **WCAG 2.1 AA**. Every Board operation available by pointer is available by keyboard, including moving a Task between columns. Presence and permission-change notices are announced to assistive technology, never conveyed by colour or position alone.

**Feature-specific NFRs** *(PRD §4.1 and §4.11 — stable ids assigned here for reference; they are not part of the PRD's NFR numbering)*

FS-NFR-1 (§4.1): Passwords are never stored recoverably and never appear in any log, error message or API response.
FS-NFR-2 (§4.11): A record that a notification was sent is retained — Space, kind and timestamp, **never** message content or recipient address — so SM-C4 is derivable. No product surface reads it.

**Gating success metrics** *(PRD §10 — release fails without these, so they are acceptance surface, not reporting)*

SM-1: Zero verified cross-Space disclosures across browser and API in any released build, measured by an isolation test suite exercised on every change. Validates FR-15, FR-16, FR-35, FR-36, NFR-1.
SM-2: Permission changes govern the affected Account's very next request with no tolerance and take effect on open live sessions within 1 second, in 100% of tested cases — including sessions holding unsynchronised local edits. Validates FR-34, NFR-2.

**Behavioural measures with retention obligations** *(these three impose requirements on implementation even though no surface reads them)*: the Invitation record keeps its terminal state (FR-10) for SM-4; a notification send record is kept (FS-NFR-2) for SM-C4; compaction of the Task description change log must preserve per-author change counts and timestamps (AR-19) for SM-5.

**PRD constraint blocks** *(§6 carries four blocks of binding constraints that hold no FR or NFR number; each is cited by the story that owns it so none is lost for want of an identifier)*

- **§6.1 Privacy** — Account existence never disclosed (AR-28); Memberships not enumerable by anyone including a Space's Owner (AR-29); email addresses confined to Owners and Admins of a Space the Account is a Member of (UX-DR34); no behavioural analytics on Space contents; **no product surface aggregates across Spaces** — no in-product metrics dashboard, no admin analytics view, no endpoint returning a cross-Space count. Owned by Stories 1.3, 1.4, 1.7, 4.1, 4.4.
- **§6.2 Data lifecycle** — deletion of Account, Space, Project or Task irreversible in v1, no trash and no restore; deleting an Account never deletes another Account's work; **deleting a Space states at the point of the action** that other Accounts lose access. Owned by Stories 2.5, 3.3, 5.4.
- **§6.3 Cost** — under **£30/month** at NFR-8 scale; no always-on dedicated infrastructure per Space or per editing session; NFR-3 and NFR-4 satisfied within a single modest deployment. Owned by Story 1.10.
- **§6.4 Data protection — a gate, not a backlog.** v1 is a **single-operator deployment** claiming no data-protection posture. **"The first Account created by anyone other than the operator makes this document non-compliant until amended."** Five prerequisites become live at that moment: a lawful basis for holding email addresses and authored content; a stated data region with no replication outside it; encryption at rest asserted (NFR-6 covers transit only); a breach-notification position; a subject-access or export route (FR-3 covers erasure only). The gate is a testable release condition rather than an aspiration, so it is **recorded as an operational condition in Story 1.10** alongside §6.3. Two obligations are parked against it and travel with it: the UX spine's accepted browser-spellcheck / cloud-IME egress of description text, and the deferred subject-access / data-export capability that the addendum holds *"behind the §6.4 gate"*.

### Additional Requirements

*From the Architecture Spine (AD-1 … AD-29, Consistency Conventions, Stack, Structural Seed, Deferred). Numbered AR-n here so stories can cite them stably.*

**🚨 STARTER TEMPLATE: none.** The architecture specifies **no third-party starter or greenfield template**. It does specify an exact solution skeleton, ring dependency rule, stack versions and build gates that must exist before any feature story can be written against them — this is Epic 1 Story 1 material and is AR-1 … AR-4 below.

- **AR-1** — Greenfield .NET 10.0.11 solution in the exact structure the spine's Structural Seed names: `Yello.AppHost`, `Yello.Domain`, `Yello.Application`, `Yello.Infrastructure`, `Yello.Host`, `Yello.Contracts`, `Yello.Merge`, `Yello.Client` (Blazor WebAssembly), plus test projects `Yello.Tests.Isolation`, `Yello.Tests.Revocation`, `Yello.Tests.Merge`, `Yello.Tests.Architecture`, `Yello.Tests.Slices`. Aspire 13.4 for local orchestration via `aspire run`. Pinned versions: ASP.NET Core / Blazor WASM 10, EF Core 10, ASP.NET Core Identity 10 (authentication only), Asp.Versioning.Http 10.0.0, xunit.v3 4.0.0, Testcontainers.XunitV3 4.6.0, TngTech.ArchUnitNET 0.13.3.
- **AR-2** — Ring dependency rule as a build gate (AD-21), enforced by ArchUnitNET tests that fail the build: `Domain` references nothing; `Application` references only `Domain`; `Infrastructure` references `Application` and `Domain`; `Host` references all. EF Core types never appear in `Domain`; ASP.NET Core types never appear in `Application` or `Domain`.
- **AR-3** — Vertical slice convention: `Yello.Application/{Area}/{UseCase}/` where `{UseCase}` is the imperative in the FR title, one folder holding its command, handler, validator and tests. Cross-cutting invariants — authorisation, Space resolution, refusal recording, idempotency, NFR-8 bound checks — live in the request pipeline and never inside a slice. **A slice that re-implements any of them is a defect.**
- **AR-4** — Architecture test forbidding `[Authorize(Roles = …)]`, `ClaimsPrincipal.IsInRole`, `IdentityRole` and Identity's role store. Identity is used for authentication only: Account store, password hashing, cookie issuance.
- **AR-5** — Every Space-scoped table carries a non-nullable `SpaceId` and a row-level security policy filtering on `SESSION_CONTEXT('SpaceId')`. Infrastructure calls `sp_set_session_context 'SpaceId', …, @read_only = 1` at the start of **every unit of work** from `ActiveSpaceContext`, never from a client-supplied value and never once per connection. A Space-scoped table without an RLS policy fails the schema test.
- **AR-6** — EF Core global query filters restate every RLS policy as an **independent** second layer derived from application state, so neither layer alone carries NFR-1. Raw SQL bypassing global query filters is forbidden outside `Infrastructure`.
- **AR-7** — Database configured `MAXDOP = 1` to remove the SESSION_CONTEXT parallel-plan defect class. The isolation suite includes a **pooled-connection reuse** case: two requests for different Spaces served consecutively on one physical connection.
- **AR-8** — Cross-entity references inside a Space use composite foreign keys carrying `SpaceId` — an Assignee is `(SpaceId, MembershipId)` — so FR-21's same-Space constraint holds by construction rather than by validation.
- **AR-9** — Every Space-scoped route carries `{spaceId}` as the first path segment after the version. An architecture test fails the build on a Space-scoped endpoint resolving a `Task`, `Project`, `Label` or `StatusDefinition` without it. No bare-id deep links.
- **AR-10** — The 403/404 line is drawn at the Space boundary and nowhere else, in the pipeline: no Membership → **404**, identical to non-existence; Membership but wrong Role → **403**. No handler converts one into the other. Error bodies carry no Space name, resource title or existence hint. The isolation suite carries a **timing** case comparing boundary-404 and in-Space not-found durations, not only bodies.
- **AR-11** — The pipeline, not the slice, writes an `AccessRefusal` row for every 403 and every Space-boundary 404, carrying acting Account, target Space, capability attempted, outcome, and a kind of `CrossSpace` or `InsufficientRole`. Retained 90 days, purged by a job running at most daily.
- **AR-12** — Schema-level uniqueness guarantees: a filtered unique index on `Membership(SpaceId) WHERE Role = Owner`, and one on `OwnershipOffer(SpaceId) WHERE State = Pending`. Invariant tests assert no Space ever holds zero or two Owner Memberships.
- **AR-13** — Ownership acceptance is authorised by **row identity** (is the caller the named Membership), never by Role — the only capability in Yello decided outside the FR-16 matrix. Every transition is guarded by `WHERE State = Pending` plus a rowcount check. The Role change is **two explicit ordered `ExecuteUpdate` calls in one transaction: demote the current Owner to Admin, then promote the recipient.** Tracked-entity `SaveChanges` is forbidden — EF Core picks its own statement order and promote-first violates AR-12's filtered index. A transition refused because the offer is no longer pending returns **409** with a stable problem `type`, never 404, for a caller holding a Membership.
- **AR-14** — Cross-origin session contract: distinct client (Static Web Apps) and API (Container App) origins; Session cookie `HttpOnly; Secure; SameSite=None`; CORS allows exactly the configured client origin with credentials, never a wildcard or a reflected `Origin`; every state-changing request carries an anti-forgery token; no credential ever written to `localStorage` or `sessionStorage`.
- **AR-15** — The sync channel carries no authority. One WebSocket at `/sync`. Each connection holds an authorisation lease `(AccountId, SpaceId, Role)` established at connect and held **until invalidated by push** — no TTL, no periodic revalidation. Every inbound frame is checked against a valid lease before being applied, persisted or broadcast; a frame on an invalidated lease is **discarded, not queued and not persisted**, and the connection closes with an access-ended reason. Leases do not survive a process restart. Application-level heartbeat every 30 s (Container Apps severs idle connections at 240 s). Frames are versioned alongside the API.
- **AR-16** — Any operation mutating a Membership publishes `MembershipChanged(AccountId, SpaceId)` at its transaction boundary, delivered in-process, invalidating matching leases immediately. **One publish per affected Account** — accepting an Ownership Offer moves two Roles and must publish both. Nothing is polled.
- **AR-17** — The sync service runs at most one replica. In-memory document state is a cache only; every admitted change is durable in the log before broadcast, and a replica restart mid-session loses no admitted change. No design may require a shared in-memory backplane or sticky per-document routing.
- **AR-18** — Exactly one `ITextMergeStrategy` interface with exactly one registered implementation; no domain, application or sync code references a concrete merge type. The port's contract is an **executable conformance suite encoding FR-31, FR-33 and NFR-4, written before any implementation and passing before any implementation merges.** Expected: a plain-text sequence CRDT in `Yello.Merge`, one source compiled to WASM for the client and native for the server.
- **AR-19** — A Task description persists as append-only immutable `TaskDescriptionChange` rows plus a derived plain-text projection on `Task`, which is the only representation read by the REST API and the List View. Nothing writes the projection except the projector, which recomputes it **inside the same transaction that appends the change**. Clients batch frames rather than sending one per keystroke. Compaction replaces a log prefix with a snapshot row, never mutates existing rows, and **must preserve per-author change counts and timestamps**.
- **AR-20** — The client edits a local replica and never blocks on the network; the server never accepts whole-text as truth but admits or rejects each change, and a rejected change is reverted in the client replica. The client is never the arbiter of what is in the Space.
- **AR-21** — Board position is a lexicographically sortable **jittered fractional index** unique within `(ProjectId, StatusId)`. A move writes only the moved Task's key, never a renumber of siblings. The key column is declared `COLLATE Latin1_General_100_BIN2` **in the migration** — Azure SQL's case-insensitive default makes a mixed-case alphabet compare `a0` and `A0` equal, and `ALTER DATABASE … COLLATE` is unsupported on Azure SQL so it cannot be retrofitted. Same collation on the `(ProjectId, StatusId, PositionKey)` index. Readable over the API, not writable.
- **AR-22** — Board columns and List View pages are read by **keyset (seek) pagination on the position key, never `OFFSET`**, and rendered rows are **appended to the DOM and never recycled — DOM virtualisation is forbidden.** The column count is a **separate query** (`COUNT` over the same indexed predicate) giving the true total, feeding `aria-setsize` and `aria-posinset`. Every List View keyset is `(sortColumn, TaskId)` with `TaskId` as a mandatory tiebreaker, the seek predicate compares the pair, and the supporting composite index carries both columns in that order; nullable sort columns fix `NULL` at one end explicitly. **A List View sort offered in the interface without a matching composite index is a defect, not a slow query.** Initial page size 50 per column (assumption, tunable; the seek shape is the invariant).
- **AR-23** — A `StatusDefinition` has a stable id surviving rename at both levels. A Project's delta is a set of operations keyed by that id, never a materialised list. The effective set is computed on read with caching permitted only within a single request; **no table stores a Project's effective Status set.**
- **AR-24** — Removing a Status and remapping every occupying Task is one transaction with no partial application. Moving a Task between Projects is one transaction combining reparent and, where required, Status migration. **No endpoint accepts a Status removal or a cross-Project move without the mapping it requires.** An invariant test asserts no Task ever holds a Status absent from its Project's effective set.
- **AR-25** — Every state-changing endpoint accepts an `Idempotency-Key`; a replayed key returns the original response without re-applying the effect. Rate-limit refusals are machine-readable and carry `Retry-After`. Rate limiting is partitioned per Token.
- **AR-26** — Routes carry the version as the first path segment (`/api/v1/…`), at most two versions served concurrently. A **snapshot contract test** locks each served version's response shape and accepted input; any breaking change within a version fails the build.
- **AR-27** — Exactly one slice creates an Account, and it provisions the Personal Space and its Owner Membership **in the same transaction**. `AcceptInvitation` delegates to it and never provisions independently; the invited Space's Membership is a separate additional Membership. Registration completing with anything other than exactly one owned Space is a failed transaction, not a repairable state.
- **AR-28** — Registration, authentication and Invitation issue return responses identical in **status, body, shape and duration** whether or not the address is known — a registration attempt for an existing address still performs the password hash it would otherwise skip. Failed authentication never distinguishes unknown address from wrong password. No endpoint returns an email address to anyone but Owners and Admins of a Space the Account is a Member of.
- **AR-29** — Exactly two surfaces are Account-scoped rather than Space-scoped: the **Space switcher** and **Account settings**. They run under an `AccountScopedContext` whose RLS predicate is `SESSION_CONTEXT('AccountId')` — never a disabled policy, never a raw connection — and may return Space **identity only**. Adding a third requires amending AD-24. *Three specified reads exceed its letter and are decided in the UX spine: delete-Account-refused naming every Space still owned and the Spaces-per-Account bound are permitted and need naming in an AD-24 amendment; the API Token per-Space Role display is dropped.*
- **AR-30** — Every NFR-8 bound is declared in **one place** and checked by the pipeline, not the slice, with a machine-readable refusal inside the same transaction as the creation it refuses.
- **AR-31** — Anything expiring by the passage of time carries `ExpiresAt` and is evaluated by exactly one shared predicate — `State = Pending AND ExpiresAt > now` — applied by every read and every transition, **evaluated server-side inside the guarded statement's own `WHERE` clause against the database clock**, never loaded into memory and checked in C# first. No job or timer writes a terminal expiry state, and the architecture suite fails the build on a scheduled component that does. Rows are never deleted on expiry. Lapse **by event** is the opposite case and *is* written inside the causing transaction.
- **AR-32** — Presenting an Invitation is a **safe, side-effect-free read**. Membership is created only by a separate explicit state-changing request authorised on the authenticated Account matching the addressed email; the token identifies which offer is in play and is never the authority for accepting it. Acceptance transitions the Invitation out of `Pending` under the same guarded-`WHERE`-plus-rowcount discipline, so a replay creates no second Membership.
- **AR-33** — Nothing touches the database on an unconditional timer. Liveness and readiness probes answer from process state. Email is enqueued through an outbox **in the same transaction** as the triggering write and dispatched in-process on enqueue; the recovery sweep runs at process start and otherwise **piggybacks on inbound request traffic**. Cleanup jobs run at most daily. Any scheduled database access more frequent than daily requires amending AD-10.
- **AR-34** — Conventions binding every story: `Guid` ids via EF Core's `SequentialGuidValueGenerator` (**not** `Guid.CreateVersion7()`, never sequential integers); `DateTimeOffset` in UTC everywhere with ISO 8601 + offset on the wire, never `DateTime`; RFC 9457 `application/problem+json` with a stable machine-readable `type` (prose is never the contract); state changes only through an Application slice inside one transaction, no entity mutation in Host or Client, domain invariants in `Domain` not validators; structured logs to stdout never carrying a password, Token, cookie or Task/Project/Space content, with `SpaceId` as a field and never the Space name.
- **AR-35** — Four suites gate release: **isolation** (SM-1, every case on both surfaces), **revocation** (SM-2 / FR-34, asserting both NFR-2 clauses), **merge conformance** (AR-18), **architecture** (AR-2). xUnit v3. Integration tests run against `mssql/server:2025-latest` via Testcontainers — **never an in-memory provider, which cannot exercise RLS.**
- **AR-36** — EF Core migrations include the RLS policies and the filtered indexes, and are applied as an **explicit deploy step, never on application start.**
- **AR-37** — Configuration via environment variables only. Connection strings and the ACS key from Azure Key Vault via managed identity in Azure, user-secrets locally. No secret in source, appsettings, or a container image.
- **AR-38** — Two environments only: Local (`aspire run`) and Azure. No staging. Deployment by GitHub Actions — the Static Web Apps deploy action for the client, a container build plus revision update for the Container App, and migrations as an explicit job **before the revision is promoted.**
- **AR-39** — Operations: a metric alert fires at 10% of the monthly free vCore allowance remaining; `Behavior when free limit reached` is set to **auto-pause until next month, never paid overage**. Free-tier exhaustion and rate-limit refusals are the two operational signals worth alerting on.
- **AR-40** — Spine deferrals that must be **closed during implementation, not carried silently**: (a) the text merge algorithm itself — selected before the collaborative editing epic, admissible only by passing AR-18's conformance suite, and whole-field last-writer-wins cannot pass it; (b) cold start against NFR-5 — either pin min replicas to 1 (~£12–15/month, inside the ceiling) or state that NFR-5 is measured warm and exempt the cold path, **but state it**; (c) confirm Azure SQL's exposure to the SESSION_CONTEXT parallel-plan defect before first production deploy, relaxing `MAXDOP = 1` only with the pooled-connection isolation test still green.

### UX Design Requirements

*From the UX design contract (`DESIGN.md` visual identity + `EXPERIENCE.md` behaviour). **Yello inherits no UI system** — no shadcn, MUI or internal library — so every component named below has to be built from scratch, and every token carries a literal value rather than a delta.*

**Foundations**

UX-DR1: Implement the token system with **dark as canonical** — the unsuffixed token is the dark value, `-light` is the derived adaptation, and the two resolve **once at the theme boundary**. Every component consumes the semantic name only; a component referencing a `-light` token directly is a defect because it pins that component to one theme. **30 colour tokens** across both themes — 15 semantic names, each with an unsuffixed dark value and a `-light` sibling.
UX-DR2: Implement the 8-role type scale — `task-title`, `column-head`, `space-name`, `body`, `dialog-title`, `meta`, `role-label`, `presence-count` — sized in **rem against a 16px root** with every line-height ≥ 1.5. Two system stacks (`system-sans`, `system-mono`), no webfont. `px` survives only on hairlines, radii and outline offsets. Metadata is monospace; `presence-count` is deliberately sans, because NFR-9 rests on that string.
UX-DR3: Implement the 3px spacing scale (3/6/9/12/18/24/36) with **component-internal padding in rem** (`card-pad-y/x`, `control-pad-y/x`) so padding grows under zoom instead of clipping, and a **24px interactive target floor** (`target-min`) as minimum height on every interactive component — WCAG 2.2 AA 2.5.8, the real current floor.
UX-DR4: Structural borders are **1.5px minimum** (`hairline-width`), snapped to device pixels where the platform allows, with `emphasis-width` 2px on the lifted card. This is an accessibility requirement, not a style: a 1px border antialiased at 1.25×/1.5×/1.75× display scales drops every border pair below the 3:1 gate, and the lifted card's `rotate(-1deg)` antialiases unconditionally.
UX-DR5: **There is no elevation.** `task-card` sets `shadow: none` explicitly. The single exception is `task-card-lifted` during an active drag — hard offset shadow, 1° rotation, 2px border. Radii: 2px on Role chip / Label chips / Offer indicator, 3px on Tasks / columns / context bar / buttons, 6px on dialogs / Task detail / invitation view, and `rounded.full` on `column-count` only — the one pill in the product. Avatars are **squared to 3px, not circular.**
UX-DR6: Implement the motion timing contract — `instant` 90ms, `quick` 120ms, `lift` 110ms, `settle` 120ms, `long-press-threshold` 320ms, `long-press-slop` 10px, with `easing-standard` on entry and `easing-exit` on exit. **Never animated:** a Task arriving from another User's edit, a permission change taking effect, or anything on the destructive path. `prefers-reduced-motion: reduce` removes every transition, and nothing depends on motion to convey state.
UX-DR7: Contrast is a **release gate**, verified by computation not estimation, across both themes for the **18 gated pairs** (12 text pairs at 4.5:1, 6 non-text and structural pairs at 3.0:1). `DESIGN.md`'s table carries two further rows — `surface-card` on `surface-column` and `surface-column` on `surface-page` — which it names explicitly as **not contrast pairs**: they are deliberately-low adjacency ratios (~1.09 / ~1.10) separating grounds by hairline rather than luminance, and gating them would fail the build permanently. Three rules fall out and must be implemented as such: `focus-ring` keeps its **2px `outline-offset`** and is never inset or set to 0 (the offset, not the token separation, is what makes it visible — the ring is only 1.45 against accent); `text-link` is **always underlined** (accent is 2.66 against body text); and **destructiveness is carried by copy, never colour** (accent and danger are 1.19 apart and converge under deuteranopia).

**Component library**

UX-DR8: Board primitives — `task-card` (title plus a metadata row of Label chips, Presence indicator and right-pushed Assignee avatar; **at most three Label chips then a `+N` affordance**, wrapping to a second line before ever scrolling horizontally, because at the 320px reflow width horizontal overflow inside a card is a 1.4.10 failure), `task-card-lifted`, `column` (scrolls within itself, never the page), `column-count` (the **true total** for that Status, never the number rendered), `drop-zone` (dashed, not filled).
UX-DR9: Context bar cluster — `context-bar` (always present once authenticated, never scrolls away, never collapses behind a menu), `role-chip` (monospace, uppercase via `text-transform`, **bordered**, display-only and never interactive), `space-switcher` (rows carry a Space name and **nothing else** — no count, no Role, no badge, because AD-24 permits only identity), `offer-indicator` (accent, present only while an Ownership Offer naming this Membership is Pending **in the active Space**).
UX-DR10: Task detail cluster — `task-detail` (opens over the Board, one level deep, attributes above and description below with Presence in the header), `description-editor` (on the page ground so it reads recessed; no save button, no merge prompt, no lock, no stale warning; batches frames rather than sending per keystroke; **absent entirely for a Viewer, not read-only**), `description-editor-readonly` (the FR-34 state: revoked-edge border, muted but legible and selectable text, implemented with **`readonly` — never `inert` or `disabled`**, both of which remove the retained text from the accessibility tree), `presence-indicator` (6px dot **plus** text count, always), `avatar` (monospace initials, non-interactive, **tombstone with no initials for a deleted Account**, never carries an email address).
UX-DR11: `picker` — **one component, five uses.** Move (destination Status and position), Assignee (Memberships of the active Space only), Label (Labels defined for the Space), Status (this Project's effective set only), Role (carrying FR-13's narrowing: Admin offers Member↔Viewer only, promote/demote Admin is Owner-only, no change may produce a second Owner or remove the sole Owner). **Never a default selection where the choice is consequential.**
UX-DR12: Controls and dialogs — exactly three button variants (`button-primary`, `button-danger`, `button-secondary` for a dialog where neither choice may be a default), **no ghost or tertiary variant**; `dialog` one level deep and never stacked, `Esc` closes, focus trapped and returned to the invoking element; `destructive-confirm` **replaces its invoking panel's content in place rather than stacking on it**, with copy as the signal and the danger border as reinforcement.
UX-DR13: `label-chip` colours are user-defined per Space, so ship a **constrained palette that satisfies the rules by construction — never a free colour picker.** A Label fill must hold 3:1 against **both** `surface-card` and `surface-card-light` simultaneously (the two grounds are 17 stops apart), 4.5:1 against its own foreground text, and sit at least **ΔE2000 10** from `focus-ring`, `danger`, `accent` and `presence`.
UX-DR14: Space settings surfaces — `membership-list` (every Membership with its Role; **email addresses are visible here and nowhere else**; an Admin's missing controls are **absent** not disabled; the Owner's row carries no remove control for anyone including the Owner; pages at NFR-8's 100-Membership bound), `invitation-list` (pending Invitations with address, Role and issuer, revocable by any Owner or Admin **including one who did not issue it and including when the issuer has been demoted, removed or deleted**; terminal Invitations retained in the record but **not shown**), `ownership-panel` (Owner-only: offer to a named Membership of **any** Role, see the pending offer with recipient and **server-clock** expiry, revoke).
UX-DR15: `status-delta-editor` in Project settings — add, remove, rename, reorder as a delta over the Space defaults, showing the effective set **and which entries come from the Space defaults versus this Project's delta**, because that distinction is what makes FR-27's cascade comprehensible when it fires.
UX-DR16: FR-27's **report-and-ask cascade UI**, both halves, the most complex interaction in the product. Rename collision: name every conflicting Project and its current name, offer **one** cascade decision applied to all at once, apply to non-conflicting Projects either way. Removal: ask for **one** Space-wide destination, then name every Project whose post-removal effective set cannot accept it **with how many Tasks each has affected** and require a destination from that Project's own post-removal set. **No fallback, no silent placement, no default selection in the destination picker** — a default would reintroduce the guess the PRD removed. Nothing applies until every reported Project has a destination; commits as one transaction across up to 50 Projects × 5,000 Tasks with a named scope, focus moved to the progress region, and **no percentage bar** (it is atomic, so a percentage would be untrue).
UX-DR17: `bulk-move-bar` — the only accent-bordered component, appearing once the operation is initiated, naming its own scope ("Moving 4,812 Tasks."), carrying `role="status"` and the **only** cancel affordance before commit, so **focus moves to it on appearance**. Blocks interaction on the affected columns only, implemented so blocking never destroys the focused node. Atomic, so no progress bar. Focus goes to the destination column on commit and the originating column on cancel. A refused bulk move states that nothing moved.
UX-DR18: `invitation-view` — the one surface an unauthenticated stranger sees, with the most generous padding in the product and the engineered register deliberately loosened. A **side-effect-free read** naming the Space, the Role and the issuer. One identical response for **revoked, accepted, expired, lapsed and unrecognised** — same words, shape and duration. The wrong-Account-signed-in case is handled **without echoing either address**. No Space name in `<title>` or any `og:`/`twitter:` metadata, `noindex`, and no Space name or id in the URL beyond the opaque token.
UX-DR19: `list-view-controls` — filter and sort by Status, Assignee, due date and Label; filters never surface a Task from another Project or Space; Assignee filtering offers only Memberships of the active Space. **Pages rather than scrolling infinitely**, page size stated, keyboard row traversal specified, filter result count announced politely on change, and "No Tasks match." with nothing else on empty.
UX-DR20: `status-pager` below 768px — a **tablist** over the Project's effective Status set with the column as its panel, arrow-key navigation between tabs, and a polite announcement of the new Status and its true count on change. It is the *only* route to a Status at that width and therefore the surface a 1.4.10 audit is conducted on.

**Interaction**

UX-DR21: The **canonical Move control** — a plain control, not a gesture, in Task detail and on the Board via the Task's context menu (opened by pointer, by `Enter` on a focused Task, or by the platform context-menu key), opening a picker naming destination Status and position. **Never removed at any breakpoint, never hover-only, never the second thing offered**, and absent only for a Role that cannot move Tasks at all. Load-bearing for three separate reasons: WCAG 2.5.1 Pointer Gestures (Level A) at 768–1279px on a touch tablet where there is no keyboard and no pager; screen-reader browse mode, where NVDA and JAWS consume the arrow keys (and `role="application"` is **not** the answer); and motor accessibility, since a 320ms hold with movement tolerance is unreachable with essential tremor.
UX-DR22: Pointer drag path — press and drag to lift, move, drop, with `lift` and `settle` motion and `drop-zone` marking the destination.
UX-DR23: Touch path — **long-press to lift, then drag**, with the 320ms threshold. Movement beyond 10px in **any** direction before the threshold is a pan and cancels the pending lift (axis-agnostic with a real dead zone, because a single-axis rule breaks where the Board itself scrolls horizontally and a zero-tolerance rule cancels on finger jitter). Scroll and pan intent always win. Lift confirmed by the lifted card and by haptics where available. Dragging near a column edge auto-scrolls the column, near the viewport edge the Board. Cancellable by dragging back to origin or releasing outside any drop zone (WCAG 2.5.2). **This gesture does not discharge NFR-9.**
UX-DR24: Full keyboard operation of the Board — `Tab`/`Shift+Tab` between columns and controls in reading order; `↑`/`↓` between Tasks in a column; `←`/`→` between columns by **logical** direction so they mirror under RTL, with column position preserved by **sticky origin index with clamping**; `Enter` to open a Task or its context menu; `Space` to pick up, `←`/`→` and `↑`/`↓` to move, `Space` to drop, `Esc` to cancel — **the `Space` binding scoped to a focused Task card only**, since columns are scroll containers and an unscoped rebinding breaks both. A carried Task lands at **the same ordinal, clamped to the destination's length**, because AD-15 needs two concrete neighbours to compute a fractional index. `Esc` follows **innermost-meaning-wins**. Below 768px the keyboard path uses the same Move picker, and a committed move advances the pager to follow the Task. The arrow grammar requires the Board to be an application-mode composite widget (`role="grid"` or equivalent, single tab stop, internally managed focus) — which is exactly why it is **not** the conformance path.
UX-DR25: Every pick-up, move and drop **announces via an ARIA live region in a specified string shape**, because for a blind User this replaces the entire visual drop-zone system: `"Moved to In Progress, position 3 of 12."` — destination Status, position ordinal and column total. A cancel announces the restoration: `"Returned to Todo, position 7."`
UX-DR26: Enforce the **banned list** as review criteria: no hover-only affordances; no controls disabled for Role reasons (remove them); no merge prompts, edit locks or stale-content warnings; no modal stack deeper than one; no infinite scroll (the Board scrolls per column, the List View pages); no optimistic UI on anything destructive or on a permission change, everything else optimistic by default; no auto-save indicators (**there is no save button anywhere in Yello**); no badges, counts-as-nudges, streaks or re-engagement prompts.

**States and behaviour**

UX-DR27: **Capabilities a Role lacks are absent, not disabled — everywhere.** A Viewer's Board has no create affordance, no Move control, no drag handle, no editor: not greyed, not tooltipped, gone. There is deliberately **no disabled state in Yello for Role reasons**; if one is being designed, the answer is removal. Absence is an honesty contract with the User, not a security control — FR-16 still refuses at the API.
UX-DR28: The context bar's **accessible name states the Role and what it permits** — "Northwind Redesign — Admin, can manage Members and settings", "— Viewer, read only in this Space" — the one place the interface explains a Role limit in prose, existing precisely because with every write affordance removed the surface that would explain why is otherwise unreachable by the person who wants it.
UX-DR29: **A Role drop narrates the removal before the surface settles.** Absence is a steady state, not a transition, and removing affordances silently from someone mid-action is hostile when no residual control is left to explain the disappearance. The general case is not confined to the editor: an Admin demoted to Member while sitting in Space settings gets "You're now a Member. Space settings is no longer available.", then routing to Space home — **never a silently blanked surface and never a half-rendered settings page.**
UX-DR30: The **FR-34 interruption cluster** — five states over one substrate, governed by two ordering rules. **Purge before announce:** on lease invalidation the client discards every queued **inbound** frame for that Space and clears both live regions, *then* announces — reversing this renders a queued Presence or remote-edit frame one tick after "Access ended.", disclosing a Space the Account no longer belongs to. **The User's own text stays; the Space's data goes.** The five states: access ended mid-edit (editor `readonly` immediately, focused assertive banner stating the text was not saved); demoted to Viewer mid-edit (narrate, then replace the editor with rendered text that **keeps its labelled region and heading**, then the write affordance becomes absent); disconnected mid-edit ("Disconnected. Your changes are not yet sent." — not a modal, must not block typing, and never the words "held" or "saved"); reconciliation failed (state it explicitly, keep the text visible and copyable, never auto-retry silently forever); **removed while disconnected** (the composition nothing else covers — lease invalidation cannot reach a disconnected client, so it resolves to "Access ended." rather than a reconciliation failure, and **never shows a sync-succeeded state first and then revokes it**).
UX-DR31: **The client replica is in scope for isolation** — SM-1's suite exercises requests, and a leak rendered from client memory issues none. Space switch, sign-out, Account switch, lease invalidation and back/forward navigation each **purge everything Space-scoped for the departed Space synchronously, before any render**: replica, projection, cached Board, queued inbound frames, pending announcements, Assignee and Label lists, Status sets, filter and sort state, scroll position, and any id-keyed cache. **No Space-scoped content in `localStorage`, `sessionStorage` or IndexedDB** — any durable buffer FR-33's five-minute window needs is scoped to one Space, keyed to the Session, and destroyed by the same triggers. Optimistic rendering never precedes authorisation on the **first** read of a Space. An id in a URL confers nothing and renders nothing from cache before the server answers. The client **never issues a Space-scoped request for a Space id absent from the current switcher response**, and nothing is prefetched across a Space boundary.
UX-DR32: Refusal surfaces — the two 404 cases are **indistinguishable in every respect the interface controls**: same words ("Not available."), same layout, same focus behaviour, same page-not-toast treatment, and **no client-side branch on which case occurred**. A within-Space 403 gets **capability-shaped, not object-shaped** copy ("Viewers cannot edit Tasks."). The body names no Space, Project or Task title and no id not already in the URL. It is **not a toast** — a toast implies a transient fault and this is a final answer. The route back goes to a Space from the switcher, **never a remembered last-Space**. A boundary 404 answering an optimistic write reverts the replica and then renders the **full refusal surface** — not a silent revert, which would breach FR-34's observability. Error text is never templated with server-supplied prose; the client owns the string.
UX-DR33: **Browser-owned disclosure surfaces**, all four leaking Space *names* to parties with no Membership: `autocomplete="off"` and a non-reusable field name on every Space, Project and Task name input (the delete-Space confirm asks the User to type a Space name, and autofill is origin-scoped and Account-agnostic); `document.title` is a fixed `Yello` with no Space, Project or Task name ever (it reaches browser history, the OS window title, screen shares and cross-device synced history, and outlives the Membership); no Space name in the invitation view's `<title>` or `og:`/`twitter:` metadata plus `noindex` (paste the link into Slack and a preview service copies the name into the logs of people with no Membership); scroll restoration disabled on Space-scoped routes.
UX-DR34: **Email address confinement.** No surface outside Space settings' Membership management renders an email address — not the Assignee picker, not Presence, not an avatar tooltip, not attribution, not a Presence announcement. Everywhere else a Membership is identified by **display name and initials only**, and two identical display names are disambiguated by a **Membership-scoped discriminator, never the address**. Attribution renders the name captured at authoring time or on the Membership row, **never a live global Account lookup** — which would propagate a later name change into a Space the reader no longer shares with that person.
UX-DR35: The empty and edge state set — empty Project ("Nothing here yet." + "Add a Task", and **never** a prompt to configure Statuses, since UJ-1 requires Ravi meet no empty state asking him to configure something); empty Space; **belonging to no Space** (a valid state — offer to create one, never auto-create); List View filter empty; session expired (return to sign-in stating the reason, purge all Space-scoped state, retain unsynchronised editor text locally and **never silently submit it after re-authentication**); scale bound reached ("This Space has 100 Memberships, the maximum." — visible, never silent); disconnected on a non-editor surface (state that updates have stopped and what is consequently stale — **never silently present a frozen Board as current**); cold load (skeletons matching the eventual layout, `aria-busy="true"`, politely announced completion, never a whole-surface spinner — and **the context bar may render its shell but never the Space name**, which cannot come from a 404 and so would have to be sourced from cache).
UX-DR36: The **destructive confirm ladder**, friction scaling with blast radius: Task (name it, "This cannot be undone."); **Membership** (name the person and **state whether they currently have a live session**, so the remover knows they are interrupting someone mid-edit); Project (name it **and its Task count**, saying so when the count is zero — that reassurance is UJ-9's payoff); Space (name it, its Project and Task counts, and **that other Accounts lose access**, requiring the User to type the Space name in a field with `autocomplete="off"`); Account deletion refused (name **every** Space still owned and state the two exits, transfer-and-have-it-accepted or delete — **do not imply a third**).
UX-DR37: Voice and tone as an implementable copy standard — terse declaratives, no hedging, apology, exclamation marks, encouragement or emoji; refusals **capability-shaped, not object-shaped**; **never the word "archive"**, which would promise a safety net that does not exist. Two deliberate overrides where the stakes are asymmetric: **accepting an Ownership Offer** gets full explanation of what it commits the recipient to (their Membership cannot be removed while they hold ownership, their Account deletion is refused until they transfer onward or delete), and **deleting a Space** names what goes and states that other Accounts lose access. All copy externalised — no user-visible string literal in a component.
UX-DR38: **Live region policy.** Presence is `polite`, permission changes are `assertive`. Announcements carry **no cross-Space information, ever** — the Presence string is a display name or a count, never an email address. A permission-change notice is delivered **only to the client context whose active Space matches the change** — cross-tab fan-out is a per-tab *filter*, never a shared announcement, and the copy stays "Access ended." with no Space name in any case. Announcements are throttled because NFR-8's bounds make a naive region a denial of service (`polite` queues rather than coalescing): Presence announces the **settled count** debounced to roughly 5 s and **only for the Task the User has open**, and is **suppressed entirely while the User is typing**. Permission changes are **never** throttled or coalesced. The Board announces on deliberate action only — pick-up, move, drop, filter count — **never** on loading or virtualising, and remote Board mutation gets **no per-Task announcement** but a debounced summary ("3 Tasks changed") plus a manual refresh affordance.
UX-DR39: **Focus destinations for remote events**, because Yello is a product where things happen to you and a destroyed or `readonly`-ed focused node drops focus to `<body>`, silently stranding a keyboard User — `assertive` announces but does **not** move focus. FR-34 removal → the **"Access ended." banner**, made programmatically focusable (`tabindex="-1"`, `role="alert"`), persistent and in the reading order, with the retained text as the next stop (a live-region utterance fired during that DOM mutation is frequently never spoken at all, so the banner is the real carrier). FR-34 demotion → the rendered description at the same scroll position with its labelled region intact. Role drop below a surface's requirement → the narration, then Space home. Task deleted while open → the retained-text panel, announced and not dismissible by a stray keypress since it is the only copy, then the **column** that held the Task (not the adjacent Task, whose index has just shifted, and not the originating card, which no longer exists). Space switch → the context bar, with the new Space and Role announced politely. Dialog close → the invoking element, or its nearest surviving container. Bulk move → the bar, then destination or originating column.
UX-DR40: The accessibility gate items beyond the flows themselves — text must survive a **1.4.12 override** (line-height 1.5×, letter-spacing 0.12×, word-spacing 0.16×, paragraph spacing 2×) with no clipping or overlap, tested against all four as a gate item, which is why **chips and cards size to content with no fixed heights**; verify at **200% *text-only* zoom**, not only page zoom, since page zoom scales everything and hides the failure; focus visible at all times and never removed, replaced by a colour change, or inset; `Tab` order follows reading order on every surface; focus trapped in dialogs and returned on close.
UX-DR41: Responsive behaviour — ≥1280px all Status columns side by side with a full context bar; 768–1279px columns side by side with horizontal Board scroll and the Project name dropped from the context bar; <768px **one column at a time with the Status pager**, long-press drag within the visible column and cross-Status moves via the Move control. **The Role chip and the Offer indicator survive every breakpoint**; the drop order is the Project name, then the switcher chevron label, **never the Role**. The <768px path is the 1.4.10 answer and fires by construction — 400% zoom on a 1280px monitor yields a 320px viewport — and this design **declines** the two-dimensional-layout exemption a Kanban board would normally claim, eliminating the horizontal axis instead.
UX-DR42: Internationalisation, adopted with no upstream requirement behind it and therefore easy to quietly drop — no layout sized to an English string (German and Finnish run 30–40% longer); structure **RTL-tolerant** via logical properties throughout, never `left`/`right`, with column order, drag direction **and the arrow keys** mirroring while the Status *sequence* does not (it is data, not layout); uppercase applied by `text-transform` under a locale-aware `lang` attribute, **excluding Turkish, Azeri and Greek** where it is lossy, and falling back to zero letter-spacing for case-less scripts; copy resources hold **sentence case** so the Role's accessible name is not spelled out letter by letter by JAWS and VoiceOver; **metadata never aligned by character count**, since `system-mono`'s non-Latin fallback is often not monospaced; relative time computed against the **server** clock.

### FR Coverage Map

*Every FR-1 … FR-43 is assigned to exactly one epic. 7 + 9 + 3 + 8 + 4 + 5 + 4 + 3 = 43.*

*Revised during story creation: FR-24 moved from Epic 2 to Epic 6. Defining a Space's default Status set includes removing one, and removal requires FR-26's mandatory Task mapping — so FR-24 in Epic 2 would have been a forward dependency on Epic 6. Epic 2 needs nothing from it: Epic 1 seeds Todo / In Progress / Done at provisioning, and UJ-1 requires that nobody be asked about columns.*

FR-1: Epic 1 - Register an Account, with responses identical in status, body, shape and duration whether or not the address is known
FR-2: Epic 1 - Authenticate and hold a Session that spans every Space and carries no Role
FR-3: Epic 5 - Delete an Account, refused while it owns any Space, with both exits reachable
FR-4: Epic 1 - Provision a Personal Space in the same transaction as the Account
FR-5: Epic 3 - Create additional Spaces without limit, bounded by NFR-8's 50
FR-6: Epic 3 - Rename a Space, from Space settings (surface placed by the UX spine; PRD §7 omits it)
FR-7: Epic 3 - Delete a Space, top of the destructive-confirm ladder, invalidating its API Tokens
FR-8: Epic 5 - Offer ownership to a named Membership; ownership does not move until accepted
FR-9: Epic 1 - Establish and switch Space context; the switcher lists one Space until Epic 3
FR-10: Epic 4 - Issue an Invitation to an email address at a Role, never at Owner
FR-11: Epic 4 - Accept an Invitation as a separate explicit act; the token never authorises
FR-12: Epic 4 - Revoke a pending Invitation, surviving its issuer's demotion or deletion
FR-13: Epic 4 - Change a Membership's Role within the actor's own Role's constraints
FR-14: Epic 4 - Remove a Membership or leave a Space; never the Owner while it holds ownership
FR-15: Epic 1 - Enforce Space-scoped authorisation in the pipeline; SM-1 first goes green here
FR-16: Epic 1 - Apply the Role capability matrix; remaining rows exercised in Epic 4
FR-17: Epic 2 - Create, rename and delete a Project
FR-18: Epic 2 - List the Projects in the active Space
FR-19: Epic 2 - Create a Task, taking the first Status in its Project's effective set
FR-20: Epic 2 - Edit title, Status, due date and Labels; description is FR-31, Epic 7
FR-21: Epic 4 - Assign a Task to a Membership of the same Space, via the composite foreign key
FR-22: Epic 2 - Manage Labels per Space, with the constrained palette
FR-23: Epic 2 - Delete a Task; editing-session termination completes in Epic 7
FR-24: Epic 6 - Define the Space default Status set; Epic 1's provisioning seeds Todo / In Progress / Done and Epic 2 works from that seeded set unchanged
FR-25: Epic 6 - Apply a Project Status delta keyed on Status identity
FR-26: Epic 6 - Map Tasks when a Status is removed, as one transaction with no partial application
FR-27: Epic 6 - Propagate Space-level Status changes with the report-and-ask cascade, both halves
FR-28: Epic 2 - View a Project as a Board, holding NFR-5 and NFR-9 at 5,000 Tasks
FR-29: Epic 2 - Move and order Tasks on a Board via the jittered fractional index
FR-30: Epic 2 - View a Project as a filterable, sortable, paged List View
FR-31: Epic 7 - Edit a Task description concurrently, behind the merge port
FR-32: Epic 7 - Show Presence, scoped to Memberships of the same Space
FR-33: Epic 7 - Reconcile after disconnection, exactly once, without loss or duplication
FR-34: Epic 7 - Apply permission changes to live sessions; SM-2 gates release
FR-35: Epic 8 - Expose Spaces, Projects and Tasks over the API; endpoints accrue per-slice across Epics 1-7 by AD-4, and this epic closes the parity audit, the enumeration refusals and the read-only position
FR-36: Epic 1 - Issue and scope an API Token, so the isolation suite runs both surfaces from Epic 1
FR-37: Epic 8 - Version the API by path segment and deprecate predictably, locked by snapshot contract tests
FR-38: Epic 8 - Rate limit API requests per Token with a machine-readable refusal and Retry-After
FR-39: Epic 4 - Deliver an Invitation by email, load-bearing for FR-11's entry point
FR-40: Epic 4 - Notify on assignment, never for one's own action
FR-41: Epic 6 - Move a Task to another Project, single and bulk forms, with the Status mapping
FR-42: Epic 5 - Accept or decline an Ownership Offer, authorised by row identity rather than Role
FR-43: Epic 5 - Deliver an Ownership Offer by email; exactly one per offer, carrying no means of accepting

### Story Coverage Index

*Which story implements which requirement. 53 stories. Stories carrying no FR deliver foundation, verification or an NFR-9 obligation, and are marked accordingly.*

| Story | Requirements |
|---|---|
| 1.1 The solution skeleton and its build gates | AR-1 … AR-4, AR-35 |
| 1.2 The design foundations every surface is drawn from | UX-DR1 … UX-DR7, UX-DR40, UX-DR42, NFR-9 |
| 1.3 Register an Account and receive a Personal Space | **FR-1, FR-4**, AR-27, AR-28, NFR-6, FS-NFR-1 |
| 1.4 Authenticate and hold a Session across every Space | **FR-2**, AR-14, AR-28 |
| 1.5 Resolve the active Space per request, enforced in the database | **FR-15**, AR-5 … AR-8, NFR-1 |
| 1.6 Refuse at the Space boundary, and record every refusal | **FR-15, FR-16**, AR-9 … AR-11, AR-30 *(all six bounds declared)*, AR-34, NFR-7, NFR-8, UX-DR32, UX-DR35 |
| 1.7 List and switch between the Spaces you belong to | **FR-9**, AR-29, UX-DR9, UX-DR28, UX-DR31, UX-DR33 |
| 1.8 Issue an API Token scoped to one Space | **FR-36**, AR-6, NFR-6 |
| 1.9 Prove isolation on both surfaces | AR-35, NFR-1, **SM-1 gate** |
| 1.10 Ship it to Azure with migrations as an explicit step | AR-33, AR-36 … AR-40c, **§6.3, §6.4 gate** |
| 2.1 Create a Project and see what is in your Space | **FR-17, FR-18**, AR-30, UX-DR35 |
| 2.2 Add Tasks and see them on a Board | **FR-19, FR-28**, AR-30, UX-DR8, UX-DR26, UX-DR27 |
| 2.3 Edit a Task's title, Status and due date | **FR-20**, AR-20, AR-34, UX-DR11, UX-DR26 |
| 2.4 Define and apply Labels | **FR-22**, UX-DR8, UX-DR13 |
| 2.5 Delete a Task, and delete a Project | **FR-23, FR-17**, UX-DR12, UX-DR36, UX-DR37 |
| 2.6 Move and reorder a Task by explicit control | **FR-29**, AR-21, UX-DR21, UX-DR25 |
| 2.7 The accelerated pointer and touch paths | UX-DR22, UX-DR23, UX-DR6 |
| 2.8 Operate the whole Board from the keyboard | UX-DR24, NFR-9 |
| 2.9 The Board at five thousand Tasks | **FR-28**, AR-22, AR-40b, NFR-5, NFR-8, NFR-9 |
| 2.10 View a Project as a filterable, sortable list | **FR-30**, AR-22, UX-DR19 |
| 2.11 The Board on a small viewport | UX-DR20, UX-DR41, UX-DR42, NFR-9 |
| 3.1 Create and rename a Space | **FR-5, FR-6**, AR-29, AR-30, UX-DR33 |
| 3.2 Switch between Spaces with nothing carrying over | UX-DR31, UX-DR33, UX-DR39, NFR-1 |
| 3.3 Delete a Space, and belong to none | **FR-7**, UX-DR36, UX-DR37, §6.2 |
| 3.4 Prove that owning one Space grants nothing in another | AR-35, NFR-1, **SM-1 extension** |
| 4.1 Invite an email address to a Space at a Role | **FR-10, FR-39**, AR-28, AR-31, AR-33 |
| 4.2 See and revoke pending Invitations | **FR-12**, UX-DR14 |
| 4.3 Accept an Invitation | **FR-11**, AR-27, AR-30 *(Memberships per Space)*, AR-32, NFR-8, UX-DR18, UX-DR33, UX-DR35 |
| 4.4 Manage Memberships and Roles | **FR-13**, AR-12, AR-16, UX-DR14, UX-DR34, NFR-2 |
| 4.5 Remove a Membership, or leave a Space | **FR-14**, AR-16, UX-DR36 |
| 4.6 Assign a Task, and be told | **FR-21, FR-40, FR-30** (Assignee dimension), AR-8, FS-NFR-2 |
| 4.7 A Viewer's Space, and a Role that drops beneath you | **FR-16**, UX-DR27, UX-DR29, UX-DR38 |
| 5.1 Offer ownership of a Space, and tell the recipient | **FR-8, FR-43**, AR-12, AR-31, AR-33, UX-DR14 |
| 5.2 Accept an Ownership Offer | **FR-42**, AR-13, AR-16, UX-DR9, UX-DR37 |
| 5.3 Decline, revoke, lapse — and the states that are no longer open | **FR-42**, AR-13, AR-31 |
| 5.4 Delete your Account | **FR-3**, AR-29, UX-DR10, UX-DR36 |
| 6.1 Shape a Project's Statuses | **FR-25**, AR-23, UX-DR15 |
| 6.2 Remove a Status from a Project by migrating its Tasks | **FR-26, FR-25**, AR-24 |
| 6.3 Define the Space default Statuses, and cascade a rename | **FR-24, FR-27**, AR-23, UX-DR16 |
| 6.4 Remove a Space default Status across every Project | **FR-27, FR-24**, AR-24, UX-DR16 |
| 6.5 Move a Task to another Project | **FR-41**, AR-24 |
| 6.6 Move a whole Status of Tasks at once | **FR-41**, AR-24, UX-DR17, UX-DR39 |
| 7.1 The merge port and its conformance suite | AR-18, AR-40a, NFR-4 |
| 7.2 A Task has a description, stored as an append-only log | **FR-31**, AR-19, UX-DR10 |
| 7.3 The sync channel that carries no authority | AR-15, AR-17, AR-30 *(concurrent editors per Task)*, NFR-2, NFR-8, UX-DR35 |
| 7.4 Edit a description at the same time as someone else | **FR-31, FR-23** (session termination), AR-20, NFR-3, NFR-4 |
| 7.5 See who else is here | **FR-32**, NFR-3, UX-DR34, UX-DR38 |
| 7.6 Reconcile after a disconnection | **FR-33**, NFR-4, UX-DR35, UX-DR38 |
| 7.7 Access ends while the door is still open | **FR-34**, AR-15, AR-16, UX-DR30, UX-DR38, UX-DR39, NFR-2 |
| 7.8 Prove revocation on a live session | **FR-34**, NFR-2, **SM-2 gate** |
| 8.1 Read and write Projects and Tasks over the API | **FR-35**, AR-35, NFR-1, NFR-5 |
| 8.2 Version the API and deprecate predictably | **FR-37**, AR-26 |
| 8.3 Rate limit per Token without duplicating writes | **FR-38**, AR-25, AR-39, NFR-5 |

**Requirements completing outside their mapped epic.** Three, each stated in both places rather than left to be discovered:

- **FR-30**'s Assignee filter and sort complete in Story 4.6, because no Task holds an Assignee until FR-21.
- **FR-23**'s "deleting a Task terminates any active editing session" completes in Story 7.4, because no session exists before Epic 7.
- **FR-13** and **FR-14**'s "takes effect on active Sessions and open editors" clauses complete in Stories 7.7 and 7.8. The request-path clause — governing the very next request with no tolerance — is fully met in Epic 4; only the live-session clause waits on the sync channel.

**File-churn assessment.** Four overlap patterns were examined and each judged incidental rather than churn:

- **Space settings is extended by Epics 3, 4, 5 and 6** — rename, then Memberships and Invitations, then the Ownership panel, then default Statuses. This is the largest overlap in the plan and consolidation is not available: it would require Memberships, Ownership Offers and Status deltas all to exist simultaneously. Each panel arrives with the capability it serves and under its own Role gate, so the shared surface is a container rather than a component being rewritten.
- **Epics 2 and 6 both touch Tasks, Board and Statuses** — but Epic 6 adds new surfaces (Project settings, the delta editor, the bulk-move bar) rather than reworking Epic 2's, and it is a genuine risk boundary in FR-27's cascade. Consolidation considered and rejected: the combined epic would exceed 17 stories.
- **Epics 1 and 3 both touch the context bar and switcher** — Epic 1 builds them against a single Space; Epic 3 is the first time "nothing carries over" has anything to carry. Consolidation considered and rejected: it would make Epic 1 ten FRs plus the entire substrate.
- **Epics 2 and 7 both touch Task detail** — Epic 7 adds the description editor as a new component rather than modifying the attribute panel.

## Epic List

**8 epics.** Each is standalone — it delivers complete functionality for its domain and enables later epics without requiring them. Ordering is driven by real dependency, not by technical layer. Everything upstream is final and mutually reconciled, so epics are deliberately few and large; the four splits that exist mark genuine risk boundaries: the isolation substrate (Epic 1), the Board at 5,000 Tasks (Epic 2), FR-27's cascade (Epic 6) and the undecided merge algorithm (Epic 7).

**Access Control is not an epic.** FR-15 and FR-16 are cross-cutting and live in Epic 1's request pipeline, after which they become an acceptance obligation on every subsequent story — AR-3 makes a slice that re-implements authorisation, Space resolution, refusal recording, idempotency or bound-checking a defect.

**The API is not a late epic either.** AD-4 forbids any slice branching on calling surface, so an endpoint accrues to both surfaces in whichever epic builds the slice. Epic 8 covers only what is genuinely API-specific.

### Epic 1: An Account, a Space of your own, and a boundary that holds

A person can register, sign in, and land in a Space they did not create and were not asked to create — and that Space is provably invisible to every other Account. This epic also lays the substrate every later story is written against: the solution skeleton, the four build gates, row-level security with an independent second filter layer, the 403/404 line at the Space boundary, refusal recording, NFR-8 bound enforcement and idempotency. SM-1 first goes green here, measured on two Accounts holding two Personal Spaces, including the pooled-connection-reuse and timing cases.

**FRs covered:** FR-1, FR-2, FR-4, FR-9, FR-15, FR-16, FR-36

**Depends on:** nothing.

**Implementation notes:** AR-1 … AR-11, AR-25, AR-27 … AR-30, AR-34 … AR-39. UX foundations UX-DR1 … UX-DR7 (tokens, type scale, spacing, borders, no elevation, motion, contrast gate), UX-DR9 (context bar, Role chip, switcher), UX-DR28 (the accessible name stating Role *and what it permits*), UX-DR32 (refusal surfaces), UX-DR33 (browser-owned disclosure surfaces), UX-DR34 (email confinement), UX-DR37 (voice and tone as a copy standard), UX-DR40 (accessibility gate items). FR-36 is pulled forward from the API epic because AR-35 requires the isolation suite to run *every case on both surfaces* — a case existing for one surface and not the other is a gap, not a choice — which also puts Account settings, AD-24's second Account-scoped surface, in this epic. Registration seeds the Space default Status set (Todo / In Progress / Done) as an FR-4 consequence; configuring it is FR-24 in Epic 6. **AR-40c must close before this epic's first production deploy:** confirm Azure SQL's exposure to the SESSION_CONTEXT parallel-plan defect, relaxing `MAXDOP = 1` only with the pooled-connection isolation test still green.

### Epic 2: Track your own work on a Board

A solo user can create Projects, add Tasks, see them arranged in columns by Status, drag or keyboard them between columns, reorder within a column, and switch to a filterable list — then close the tab. This completes UJ-1 end to end and is where the FR-28 × NFR-5 × NFR-9 collision is actually built and measured.

**FRs covered:** FR-17, FR-18, FR-19, FR-20, FR-22, FR-23, FR-28, FR-29, FR-30

**Depends on:** Epic 1.

**Implementation notes:** AR-21 (jittered fractional index with the binary collation set in the migration — it cannot be retrofitted on Azure SQL), AR-22 (keyset pagination, append-never-recycle, the true count as a separate query feeding `aria-setsize`/`aria-posinset`, and a composite index behind every offered List View sort). UX-DR8 (Board primitives including the Task card metadata overflow rule), UX-DR11 (the picker in its Status and Label uses), UX-DR19 (List View controls), UX-DR20 (the Status pager below 768px, the 1.4.10 audit surface), UX-DR21 … UX-DR26 (the canonical Move control plus the pointer, touch and keyboard paths, move announcements, and the banned list), UX-DR35 (empty and edge states), UX-DR41 (responsive behaviour). Four epics consolidated into one here on the file-overlap rule: separate epics for Task creation, the Board, the List View and ordering would all churn the same Projects/Tasks/Boards/Lists slices and the same Board component. **A Task carries no description in this epic** — FR-20 carves description editing out to FR-31, and AR-18 forbids any merge implementation before the conformance suite passes. **AR-40b lands here:** at the first NFR-5 measurement, either pin min replicas to 1 or declare NFR-5 measured warm and exempt the cold path — but state it rather than leaving it silent.

### Epic 3: Several Spaces, cleanly separated

A user can hold more than one Space — client work apart from personal work — rename them, delete them, and move between them with the entire working surface changing and nothing at all carrying over. Small in requirement count and heavy in isolation work: this is the first time a single Account holds two Spaces, which is exactly where a pooled-connection or client-cache leak would surface.

**FRs covered:** FR-5, FR-6, FR-7

**Depends on:** Epic 1.

**Implementation notes:** AR-29 (the AD-24 amendment naming the two Account-scoped reads that exceed its letter — delete-refused enumerating owned Spaces, and the Spaces-per-Account bound), AR-30 (NFR-8's 50-Spaces-per-Account bound as a visible refusal). UX-DR31 (the synchronous client purge before any render, covering replica, projection, cached Board, queued frames, pending announcements, Assignee and Label lists, Status sets, filter and sort state, scroll position and every id-keyed cache; nothing Space-scoped in `localStorage`, `sessionStorage` or IndexedDB), UX-DR36 (the destructive-confirm ladder, whose top rung is Space deletion — the only operation in the product with no undo at all, requiring the Space name typed into a field with `autocomplete="off"`). FR-6's surface is an inherited gap: PRD §7 omits it and the UX spine places it in Space settings. Kept as its own epic rather than folded into Epic 1, which would make Epic 1 ten FRs plus the entire substrate; it cannot move later than Epic 4, because a second Space must exist before anyone can be invited into one.

### Epic 4: Bring people into one piece of your work

An Owner or Admin can invite someone by email address at a fixed Role, and that person — with or without an existing Account — gains Membership in exactly one Space and can see nothing else in Yello. Roles can be changed and Memberships removed within each Role's constraints. This is where the Role matrix is finally exercised with someone other than an Owner in the room, and where UJ-4's climax actually lands: a Viewer's Board with every create-and-edit affordance *absent*, not disabled.

**FRs covered:** FR-10, FR-11, FR-12, FR-13, FR-14, FR-21, FR-39, FR-40

**Depends on:** Epic 1, Epic 3.

**Implementation notes:** AR-8 (the composite foreign key making FR-21's same-Space Assignee constraint hold by construction), AR-16 (`MembershipChanged` published in-process at the transaction boundary, one publish per affected Account), AR-28 (Invitation issue disclosing nothing about whether the address has an Account), AR-31 (read-time expiry via the one shared predicate, evaluated server-side inside the guarded statement's own `WHERE`), AR-32 (presenting an Invitation is a safe side-effect-free read; Membership is created only by a separate explicit request authorised on the matching Account), AR-33 (the outbox, enqueued in the same transaction as the triggering write and dispatched in-process). UX-DR14 (Membership list, Invitation list, and the rule that email addresses appear here and nowhere else), UX-DR18 (the invitation view: five states, one response, identical in words, shape and duration; the wrong-Account case handled without echoing either address), UX-DR27 (absence rather than disabling), UX-DR29 (a Role drop narrates before the surface settles, including outside the editor), UX-DR36 (the Membership rung of the confirm ladder, stating whether the person currently holds a live session). FR-21 sits here rather than in Epic 2 because its substance is the same-Space constraint, which needs more than one Membership to test at all.

### Epic 5: Hand a Space over and leave cleanly

An Owner can offer ownership of a Space to a named Membership at any Role, that person can accept or decline it with full knowledge of what accepting commits them to, and once it is accepted the former Owner can remove their own Membership and — if they own nothing else — delete their Account. UJ-8 end to end, closing the trap that made an Owner unable to leave and a recipient unable to erase themselves.

**FRs covered:** FR-3, FR-8, FR-42, FR-43

**Depends on:** Epic 1, Epic 3, Epic 4.

**Implementation notes:** AR-12 (the filtered unique index on `OwnershipOffer(SpaceId) WHERE State = Pending`, and the invariant test asserting no Space ever holds zero or two Owners), AR-13 (acceptance authorised by **row identity, not Role** — the only capability in Yello decided outside the FR-16 matrix — with the Role change as two explicit ordered `ExecuteUpdate` calls, demote then promote, because tracked-entity `SaveChanges` picks its own statement order and promote-first violates the filtered index; 409 with a stable problem type for a caller holding a Membership, never 404), AR-16 (two publishes on acceptance, since a lease carries Role and acceptance moves two of them), AR-31 (lapse by time computed on read and never written; lapse by event written inside the causing transaction), AR-33 (exactly one email per offer, enqueued in the same transaction as the insert, and nothing on revoke, decline or lapse). UX-DR14 (the Ownership panel with server-clock expiry), UX-DR36 (the Account-deletion-refused rung, naming every Space still owned and stating exactly two exits), UX-DR37 (one of the two places terseness is deliberately overridden — nobody should agree to ownership from a one-line dialog). The offer email carries no means of accepting; acceptance happens inside the Space, which is why this needs no third Account-scoped surface.

### Epic 6: Shape Statuses per Project and retire work safely

An Owner or Admin can add, remove, rename and reorder a Project's Statuses as a delta over the Space defaults, and can change the Space defaults with every divergent Project reported and asked about rather than guessed at. A Task can be moved to another Project in the same Space, singly or a whole Status at a time, which makes retiring a Project safe against a deletion that destroys its Tasks irreversibly. UJ-9.

**FRs covered:** FR-24, FR-25, FR-26, FR-27, FR-41

**Depends on:** Epic 1, Epic 2.

**Implementation notes:** AR-23 (Status identity surviving rename at both levels; the delta as a set of operations keyed on that id, never a materialised list; the effective set derived on read and stored nowhere), AR-24 (Status removal-plus-remap and cross-Project move each one transaction with no partial application, no endpoint accepting either without its mapping, and an invariant test asserting no Task ever holds a Status absent from its Project's effective set). UX-DR15 (the delta editor showing which entries come from the Space defaults versus this Project's delta — the distinction that makes the cascade comprehensible when it fires), UX-DR16 (FR-27's report-and-ask cascade, both halves, with **no default selection in the destination picker**, since a default would reintroduce the guess the PRD removed), UX-DR17 (the bulk-move bar: named scope, `role="status"`, the only cancel point, focus moved to it, and no percentage because the operation is atomic). FR-41 sits here rather than in Epic 2 because in Epic 2 every Project shares the Space defaults, so a move never requires a mapping and the requirement has nothing to grip.

### Epic 7: Write the same Task at the same time — and lose access the moment access ends

Two or more Users can edit the same Task description simultaneously, see each other present, and reconnect after a drop without losing or duplicating a word — with no merge dialog, lock or stale-content warning anywhere. And a User demoted or removed mid-sentence has their editing stop within a second, without touching anything, with their unsynchronised text never reaching the Space. UJ-5 and UJ-6.

**FRs covered:** FR-31, FR-32, FR-33, FR-34

**Depends on:** Epic 1, Epic 2, Epic 4.

**Implementation notes:** AR-15 (the sync channel carrying no authority — a lease with no TTL, every inbound frame checked, a frame on an invalidated lease discarded rather than queued or persisted, and the connection closed with an access-ended reason), AR-16 (permission change pushed at the transaction boundary, never polled), AR-17 (single replica, in-memory state as cache only, every admitted change durable before broadcast), AR-18 (exactly one merge port with exactly one implementation, its conformance suite written and passing **before** any implementation merges), AR-19 (the append-only change log with the projection recomputed inside the same transaction, and compaction preserving per-author change counts and timestamps), AR-20 (the client edits a replica and never blocks; the server admits or rejects and is never handed whole text as truth). UX-DR10 (the Task detail cluster, including `readonly` rather than `inert` or `disabled` on the revoked editor), UX-DR30 (the five-state FR-34 cluster with purge-before-announce and own-text-stays), UX-DR38 (live region policy — Presence polite and debounced and suppressed while typing, permission changes assertive and never throttled, delivered only to the tab whose active Space matches), UX-DR39 (focus destinations for remote events, including the focusable persistent banner that is the real carrier when a live-region utterance fires during a DOM mutation). **AR-40a must close before this epic starts:** select the merge algorithm and have it pass AR-18's conformance suite. Whole-field last-writer-wins cannot pass it, and adopting it would be a PRD amendment to FR-31 and FR-33, not an architecture decision. SM-2 gates release here.

### Epic 8: Drive Yello from a script

A consumer can read and write Projects and Tasks in exactly one Space from outside the browser, against a published contract that will not break underneath them: versioned by path, deprecated with notice, rate limited per Token with a refusal they can branch on. UJ-7.

**FRs covered:** FR-35, FR-37, FR-38

**Depends on:** Epics 1-7.

**Implementation notes:** AR-25 (rate-limit refusals machine-readable with `Retry-After`, partitioned per Token, and idempotency already in place from Epic 1 so a retry never applies a write twice), AR-26 (version as the first path segment, at most two served concurrently, with a snapshot contract test locking each served version's response shape and accepted input so a breaking change inside a version fails the build). This epic does **not** build the API. AD-4 forbids a slice branching on calling surface, so every endpoint has accrued to both surfaces in the epic that built its slice; what remains here is the contract and the audit — FR-35's parity sweep across every earlier epic, the refusal of any operation enumerating Spaces, Accounts or Memberships beyond the Token's Space, and the one deliberate narrowing where a Task's position within its Status is readable and not writable, which keeps FR-29's convergence requirement confined to a single surface.

## Epic 1: An Account, a Space of your own, and a boundary that holds

A person can register, sign in, and land in a Space they did not create and were not asked to create — and that Space is provably invisible to every other Account. This epic also lays the substrate every later story is written against: the solution skeleton, the four build gates, row-level security with an independent second filter layer, the 403/404 line at the Space boundary, refusal recording, NFR-8 bound enforcement and idempotency. SM-1 first goes green here, measured on two Accounts holding two Personal Spaces, including the pooled-connection-reuse and timing cases.

**FRs covered:** FR-1, FR-2, FR-4, FR-9, FR-15, FR-16, FR-36

### Story 1.1: The solution skeleton and its build gates

As a developer building Yello,
I want the solution laid out in its five rings with the dependency rule, the Role-API ban and the stack versions enforced by tests that fail the build,
So that no later story can erode the structure NFR-1 depends on.

**Acceptance Criteria:**

**Given** a clean checkout
**When** the solution builds
**Then** it contains `Yello.AppHost`, `Yello.Domain`, `Yello.Application`, `Yello.Infrastructure`, `Yello.Host`, `Yello.Contracts`, `Yello.Merge` and `Yello.Client`, plus `tests/Yello.Tests.Isolation`, `Yello.Tests.Revocation`, `Yello.Tests.Merge`, `Yello.Tests.Architecture` and `Yello.Tests.Slices`
**And** every dependency is pinned to the AR-1 versions — .NET 10.0.11, EF Core 10, ASP.NET Core Identity 10, Asp.Versioning.Http 10.0.0, Aspire 13.4, xunit.v3 4.0.0, Testcontainers.XunitV3 4.6.0, TngTech.ArchUnitNET 0.13.3

**Given** the architecture suite
**When** a project reference is added that violates the ring rule — `Domain` referencing anything, `Application` referencing `Infrastructure` or `Host`, `Infrastructure` referencing `Host`
**Then** the build fails, naming the offending reference
**And** the same happens when an EF Core type appears in `Domain`, or an ASP.NET Core type in `Application` or `Domain`

**Given** the architecture suite
**When** `[Authorize(Roles = …)]`, `ClaimsPrincipal.IsInRole`, `IdentityRole` or Identity's role store appears anywhere in the solution
**Then** the build fails
**And** ASP.NET Core Identity remains wired for authentication only — Account store, password hashing, cookie issuance

**Given** `aspire run` on a developer machine
**When** the AppHost starts
**Then** `Yello.Host`, `Yello.Client` and a `mcr.microsoft.com/mssql/server:2025-latest` container are running with a working connection from Host to container
**And** no test project references an EF Core in-memory provider, which cannot exercise row-level security

**Given** the four gating suites — isolation, revocation, merge conformance, architecture
**When** they run against a solution with no feature code
**Then** each builds and executes rather than failing to build
**And** the three that hold no cases yet — isolation, revocation, merge conformance — report zero tests without that being treated as a failure
**And** later stories add cases to existing suites rather than creating suites

### Story 1.2: The design foundations every surface is drawn from

As a person using Yello in either light or dark,
I want one token system, type scale and focus treatment behind every surface,
So that the product reads as one thing and its accessibility floor is verified rather than asserted.

**Acceptance Criteria:**

**Given** the token layer
**When** a component renders under the dark theme
**Then** every semantic name resolves to its unsuffixed value, and under the light theme the same name resolves to its `-light` sibling, resolved once at the theme boundary
**And** no component references a `-light` token directly, because that would pin the component to one theme

**Given** the colour tokens
**When** they are counted against `DESIGN.md`
**Then** there are **30** — 15 semantic names, each carrying an unsuffixed dark value and a `-light` sibling: `surface-page`, `surface-column`, `surface-card`, `border-hairline`, `text-primary`, `text-muted`, `accent`, `accent-on`, `focus-ring`, `presence`, `danger`, `danger-on`, `revoked-edge`, `role-chip`, `role-chip-on`
**And** the count is stated so an incomplete token set is detectable rather than merely wrong

**Given** the type scale
**When** any text renders
**Then** its size is expressed in `rem` against a 16px root with a line-height of at least 1.5
**And** `px` appears only on hairlines, radii and outline offsets — never on type

**Given** the contrast harness
**When** it runs over both palettes
**Then** all **18 gated pairs** are computed by the WCAG 2.x formula rather than estimated, and each meets its stated threshold — 4.5:1 on the twelve text pairs, 3.0:1 on the six non-text and structural pairs
**And** the build fails if any gated pair drops below its threshold, because NFR-9 makes WCAG 2.1 AA a release gate

**Given** the two remaining rows in `DESIGN.md`'s contrast table — `surface-card` on `surface-column`, and `surface-column` on `surface-page`
**When** the harness runs
**Then** they are asserted as **deliberately low** surface-adjacency ratios (~1.09 and ~1.10) and are **not** gated against any threshold
**And** the reason is stated: `DESIGN.md` names them explicitly as *"two combinations that are load-bearing and must not be mistaken for contrast pairs"* — they separate grounds by hairline rather than by luminance, so a harness gating all twenty rows would fail permanently on these two and invite an unstated exception

**Given** the focus ring
**When** any element receives focus
**Then** a 2px ring is drawn at a 2px `outline-offset`, never inset and never at offset 0
**And** it is never replaced by a colour change or a border swap — the offset, not the token separation, is what makes it visible

**Given** a structural border
**When** it renders at 1.25×, 1.5× or 1.75× display scale
**Then** it is at least 1.5px wide and snapped to a device pixel where the platform allows
**And** no component carries a shadow, because shadow is not a hierarchy device here

**Given** any interactive component
**When** its box is measured
**Then** it meets a minimum height of 24px
**And** the figure is stated precisely because it is easy to get wrong in both directions: WCAG 2.1 AA has no target-size criterion at all, and WCAG 2.2 AA's 2.5.8 sets 24×24, which is the real current floor this design commits to

**Given** the radius scale
**When** components are inspected
**Then** 2px is used on the Role chip, Label chips and the Offer indicator; 3px on Tasks, columns, the context bar and buttons; 6px on dialogs, the Task detail panel and the invitation view
**And** the fully-round radius is used for exactly one component, the column count chip, which is the only pill in the product

**Given** a text link inside a sentence
**When** it renders
**Then** it is underlined, always
**And** the reason is stated: the accent passes handsomely against the *background* and sits at only 2.66 against the body text beside it, and for a link the text beside it is the pair that matters

**Given** any user-visible string
**When** components are inspected
**Then** no string literal appears in a component — all copy is externalised into resources
**And** no label is sized to its English string, German and Finnish running 30–40% longer, and metadata is never aligned by character count because the monospace fallback outside Latin is frequently not monospaced

**Given** `prefers-reduced-motion: reduce`
**When** any transition would run
**Then** it does not
**And** no state anywhere in the product is conveyed by motion alone, so honouring the preference costs no information

**Given** a text-spacing override of line-height 1.5×, letter-spacing 0.12×, word-spacing 0.16× and paragraph spacing 2×, and separately 200% text-only zoom
**When** either is applied to any implemented surface
**Then** no text is clipped or overlapped, because chips and cards size to content with no fixed heights

### Story 1.3: Register an Account and receive a Personal Space

As someone who has just found Yello,
I want to create an Account with my email address and a password and immediately have a Space of my own,
So that I can start holding work without making a single structural decision first.

**Acceptance Criteria:**

**Given** no Account exists for an address
**When** registration completes for it
**Then** exactly one `Account` exists for that address, exactly one `Space` exists owned by it, and exactly one `Membership` at Role `Owner` joins the two — all committed in a single transaction by a single slice
**And** the Space carries the default Status set Todo / In Progress / Done and no Projects

**Given** an Account already exists for that address
**When** registration is attempted for it again
**Then** the response is identical to a successful new registration in status, body and shape
**And** identical in duration, because the password hash is performed anyway rather than skipped
**And** no second Account and no second Space are created

**Given** a registration attempt
**When** the transaction cannot complete
**Then** no Account is left holding zero Spaces or two — this is a failed transaction, not a repairable state

**Given** the provisioned Space
**When** its attributes are compared with a Space created by any other route
**Then** no attribute distinguishes it, and it is renameable, shareable, transferable and deletable on the same terms as any other Space

**Given** a password submitted at registration
**When** the datastore, every log, every error body and every API response are inspected
**Then** the password appears in none of them
**And** it is stored only under a deliberately slow one-way function whose work factor is tunable without re-registering existing Accounts

**Given** the registration surface
**When** it is submitted
**Then** the in-flight condition is stated, resubmission is disabled, and completion is announced — this being one of only two submit-and-wait interactions in a product with no save button

**Given** the Account entity and the registration slice
**When** they are reviewed
**Then** nothing assumes a password exists on every Account, and nothing binds identity to the email address in a way that cannot be revisited
**And** this is explicit, because PRD §9.2 requires FR-1, FR-2 and NFR-6 to absorb deferred OAuth sign-in without redesign

### Story 1.4: Authenticate and hold a Session across every Space

As a registered Account holder,
I want to sign in once and stay signed in across everything I belong to,
So that moving between contexts never asks me who I am again.

**Acceptance Criteria:**

**Given** valid credentials
**When** authentication succeeds
**Then** a Session is issued that identifies the Account and carries no Role, no Space and no capability of its own

**Given** an authenticated Session
**When** the acting Account changes active Space
**Then** the same Session is used unchanged and no re-authentication occurs

**Given** a Session that has expired or been invalidated
**When** any request is made with it
**Then** it grants access to nothing, including Spaces the Account still holds a Membership in
**And** the User is returned to sign-in with the reason stated, and all Space-scoped client state is purged

**Given** an unknown address, and separately a known address with a wrong password
**When** authentication is attempted with each
**Then** the two responses are identical in status, body, shape and duration
**And** the message is one string for every cause — "Email or password is incorrect." — never "no account found" and never "wrong password"

**Given** the browser surface
**When** a Session is established across the distinct client and API origins
**Then** the cookie is `HttpOnly; Secure; SameSite=None`, CORS allows exactly the configured client origin with credentials and never a wildcard or a reflected `Origin`, and every state-changing request carries an anti-forgery token
**And** no credential is ever written to `localStorage` or `sessionStorage`

**Given** locally-held unsent input at the moment a Session expires
**When** the Account re-authenticates
**Then** that input is never submitted automatically, because it would then be authored under a new Session against possibly-changed permission

### Story 1.5: Resolve the active Space per request, enforced in the database

As any Account holder,
I want every read and write of Space-scoped data authorised against my Membership in the Space that owns it,
So that nothing I am not a member of can reach me by any route.

**Acceptance Criteria:**

**Given** an authenticated request carrying `{spaceId}` as the first path segment after the version
**When** it enters the pipeline
**Then** an `ActiveSpaceContext` is resolved by reading the Membership row for that `(AccountId, SpaceId)` pair *before* any authorisation decision is made
**And** a request whose Space context cannot be resolved is refused rather than defaulted

**Given** a resolved Space context
**When** a unit of work begins
**Then** `sp_set_session_context 'SpaceId', …, @read_only = 1` is called at the start of **every** unit of work, from `ActiveSpaceContext` and never from a client-supplied value
**And** never once per connection, because a pooled connection reused across two Spaces is the exact leak this guards against

**Given** every Space-scoped table
**When** the schema test runs
**Then** each carries a non-nullable `SpaceId` and a row-level security policy filtering on `SESSION_CONTEXT('SpaceId')`
**And** a Space-scoped table without a policy fails the test

**Given** the RLS policies
**When** EF Core global query filters are inspected
**Then** each policy is independently restated from application state rather than from session context, so neither layer alone carries NFR-1
**And** no raw SQL bypassing global query filters exists outside `Infrastructure`

**Given** a pooled connection
**When** two requests for different Spaces are served consecutively on the same physical connection
**Then** the second returns only its own Space's rows
**And** the database is configured `MAXDOP = 1`, removing the class of defect in which a parallel plan reading `SESSION_CONTEXT()` on a pool-reset session returns another tenant's rows silently

**Given** a cross-entity reference inside a Space
**When** it is declared
**Then** it uses a composite foreign key carrying `SpaceId`, so a same-Space constraint holds by construction rather than by validation

**Given** a Role is needed for an authorisation decision
**When** it is obtained
**Then** it comes from the Membership row read on this request, and never from a cookie, a claim, an API Token payload, or any cache outliving the request

### Story 1.6: Refuse at the Space boundary, and record every refusal

As someone holding an identifier I should not have, or a Role that does not permit an action,
I want the product's answer to tell me nothing I am not entitled to know,
So that isolation holds under error as firmly as it holds under success.

**Acceptance Criteria:**

**Given** a resource in a Space the caller holds no Membership in, and separately an identifier matching no resource at all
**When** each is requested
**Then** both return **404** with a body identical in words, shape and layout
**And** neither carries a Space name, a resource title, or an identifier that was not already in the URL

**Given** a resource in a Space the caller does hold a Membership in, but lacks the Role for
**When** it is requested
**Then** **403** is returned with capability-shaped copy — "Viewers cannot edit Tasks." — rather than object-shaped copy that would confirm the resource exists
**And** no handler converts a 403 into a 404 or the reverse

**Given** the Role capability matrix and seeded Memberships at Owner, Admin, Member and Viewer
**When** each row of the matrix is exercised
**Then** the matrix governs the outcome in every case, and where an individual restatement disagrees with the matrix the matrix is correct and the restatement is a defect
**And** every capability is refused for an Account holding no Membership in the Space, without disclosing existence

**Given** any 403 or any Space-boundary 404
**When** it is produced
**Then** the pipeline — not the slice — writes an `AccessRefusal` row carrying the acting Account, the target Space, the capability attempted, the outcome, and a kind of `CrossSpace` or `InsufficientRole`
**And** those rows are retained 90 days and purged by a job running at most daily

**Given** a Space-scoped endpoint
**When** the architecture suite runs
**Then** the build fails if that endpoint resolves a `Task`, `Project`, `Label` or `StatusDefinition` without `{spaceId}` in its path
**And** no bare-id deep link exists, which is what keeps a record-writing boundary refusal from becoming separable by duration from an ordinary not-found

**Given** any refusal
**When** its body is inspected
**Then** it is RFC 9457 `application/problem+json` carrying a stable machine-readable `type` a client can branch on
**And** prose is never the contract

**Given** the NFR-8 bound registry, declared in one place and checked by the pipeline rather than by any slice
**When** a bound is exceeded
**Then** the refusal carries a machine-readable reason and is raised inside the same transaction as the creation it refuses
**And** the copy states the bound plainly to the User, because a bound must degrade visibly, never silently

**Given** the bound registry
**When** its entries are enumerated
**Then** it declares **all six** of NFR-8's bounds, each paired with the creation operation that must consult it, and a registry missing any of them fails the architecture suite:

| Bound | Value | Creation operation that consults it | Enforced by |
|---|:--:|---|---|
| Concurrent active Sessions per Space | 50 | establishing a Session in a Space (FR-2, FR-9) | this story |
| Projects per Space | 50 | creating a Project (FR-17) | Story 2.1 |
| Tasks per Project | 5,000 | creating a Task (FR-19) | Story 2.2 |
| Spaces per Account | 50 | creating a Space (FR-5) | Story 3.1 |
| Memberships per Space | 100 | accepting an Invitation (FR-11) | Story 4.3 |
| Concurrent editors per Task | 10 | establishing a sync lease on a Task (FR-31) | Story 7.3 |

**And** the registry is enumerated here rather than accumulated story by story, because a registry whose completeness is incidental is one where a missing bound is invisible — and NFR-8 makes an unenforced bound a defect rather than a relaxation
**And** this story implements the concurrent-active-Sessions-per-Space entry; the other five are implemented by the stories named, each of which consults this registry rather than declaring its own limit

**Given** a refusal rendered in the browser
**When** it appears
**Then** it is a page and not a toast, because a toast implies a transient fault and this is a final answer
**And** the route back leads to a Space drawn from the switcher rather than a remembered last-Space, and the client owns the string rather than templating server-supplied prose

### Story 1.7: List and switch between the Spaces you belong to

As someone who will hold Memberships in several Spaces,
I want a switcher showing exactly the Spaces I belong to, which changes my whole working surface when I pick one,
So that I always know which context I am operating in and can never see one I have no standing in.

**Acceptance Criteria:**

**Given** an authenticated Account
**When** the switcher is opened
**Then** it lists exactly the Spaces that Account holds a Membership in
**And** no Space is discoverable by any other means — no directory, no search, no enumeration reaching beyond Membership

**Given** the switcher
**When** a row renders
**Then** it carries the Space name and nothing else — no count, no Role, no badge — because this surface may return Space identity only

**Given** the switcher and Account settings, the only two Account-scoped surfaces
**When** either queries the datastore
**Then** it runs under an `AccountScopedContext` whose RLS predicate is `SESSION_CONTEXT('AccountId')` — never a disabled policy and never a raw connection
**And** no Project, Task, Membership, Label or count crosses a Space boundary through them

**Given** a Space is selected
**When** the switch completes
**Then** the acting Role becomes whatever that Account holds in the destination, with no carry-over from the previous Space
**And** focus moves to the context bar, with the new Space and Role announced politely

**Given** the context bar
**When** it renders on any authenticated surface
**Then** it names the active Space and shows the acting Role verbatim as one of Owner / Admin / Member / Viewer in a display-only chip that is never interactive
**And** it never scrolls away, collapses, or hides behind a menu

**Given** the context bar's accessible name
**When** read by assistive technology
**Then** it states the Role **and what it permits** — "Northwind Redesign — Admin, can manage Members and settings", or "— Viewer, read only in this Space"
**And** the copy resource holds sentence case with the uppercase applied by `text-transform`, so the Role is not spelled out letter by letter

**Given** a Space switch, a sign-out, or back/forward navigation
**When** it occurs
**Then** everything Space-scoped for the departed Space is purged synchronously, before any render
**And** no Space-scoped content was ever written to `localStorage`, `sessionStorage` or IndexedDB

**Given** any authenticated surface
**When** browser-owned surfaces are inspected
**Then** `document.title` is a fixed `Yello` carrying no Space, Project or Task name; every Space name input carries `autocomplete="off"` with a non-reusable field name; and scroll restoration is disabled on Space-scoped routes

### Story 1.8: Issue an API Token scoped to one Space

As someone who will want to drive Yello from a script,
I want to generate a credential that reaches exactly one Space and no further,
So that automation can never become a route around the boundary.

**Acceptance Criteria:**

**Given** a Membership in a Space
**When** that Account issues an API Token for it
**Then** the Token is bound to that one `SpaceId` at issue, and the binding cannot afterwards be changed

**Given** an Account owning several Spaces
**When** it uses a Token issued for one of them against another
**Then** the request is refused exactly as it would be for an Account holding no Membership there — including for Spaces that same Account owns

**Given** a Token-authenticated request
**When** capability is resolved
**Then** it is the issuing Account's **current** Membership Role in that Space, resolved through the same per-request path as a browser request
**And** never the Role held at the moment the Token was issued

**Given** a Token
**When** it is created
**Then** the plaintext is displayed exactly once, with the interface saying so *before* generating it, and only a hash is stored
**And** a read of the datastore yields no usable Token, and dismissing the display is a confirmed act because it is the only copy

**Given** a Token
**When** its Account's Membership is removed, its Space is deleted, or its Account is deleted
**Then** the Token is invalidated inside the same transaction as that event

**Given** a Token-authenticated request and an equivalent browser request
**When** both enter the pipeline
**Then** they differ only in how `AccountId` and `SpaceId` are established, and thereafter traverse the identical pipeline and the identical slice
**And** no slice branches on calling surface

**Given** the API Token list in Account settings
**When** it renders
**Then** it shows no per-Space Role — that would be a Membership read across a boundary on an Account-scoped surface, and it is decoration rather than function

### Story 1.9: Prove isolation on both surfaces

As the operator deciding whether Yello can ship,
I want an isolation suite exercising every case on the browser and the API alike,
So that SM-1's zero-disclosure gate is measured rather than believed.

**Acceptance Criteria:**

**Given** the isolation suite
**When** it runs
**Then** every case executes against both the browser surface and the API surface
**And** a case existing for one surface and not the other fails the suite as a gap rather than passing as a choice

**Given** two Accounts each holding their own Personal Space
**When** each attempts to reach the other's Space by direct identifier, by deep link, by listing, by aggregate and by API
**Then** every attempt returns the boundary 404, and no data and no existence is disclosed
**And** possessing an identifier confers nothing

**Given** a Space-boundary 404 and an in-Space not-found
**When** both are produced
**Then** they are compared for **duration** as well as for body, and are indistinguishable in both

**Given** the pooled-connection reuse case
**When** two requests for different Spaces are served consecutively on one physical connection
**Then** the second discloses nothing belonging to the first

**Given** the suite
**When** any change is pushed
**Then** it runs on that change, and a single verified cross-Space disclosure fails the build
**And** this is the one requirement with no acceptable failure rate

**Given** an authorisation refusal record
**When** it is inspected
**Then** a cross-Space attempt is distinguishable from a within-Space permission failure, because the two mean very different things

**Given** the client
**When** a Space-scoped surface first loads
**Then** nothing renders from cache before the server answers, no request is issued for a Space id absent from the current switcher response, and nothing is prefetched across a Space boundary
**And** the context bar may render its shell but never the Space name, which cannot come from a 404 and so would have to be sourced from cache

### Story 1.10: Ship it to Azure with migrations as an explicit step

As the operator,
I want Yello deployed to the two environments it has, with schema changes applied deliberately and the free-tier budget visible,
So that the £30/month ceiling stays a decidable question rather than a surprise.

**Acceptance Criteria:**

**Given** a push to the default branch
**When** the pipeline runs
**Then** the client deploys to Azure Static Web Apps and the Host deploys as a Container App revision limited to at most one replica
**And** migrations run as an explicit job **before** the revision is promoted, never on application start

**Given** the migrations
**When** they are applied
**Then** they include the row-level security policies and the filtered unique indexes, so isolation is created by migration rather than by hand

**Given** configuration
**When** the application starts in Azure
**Then** every value comes from environment variables, with connection strings and the Communication Services key drawn from Key Vault via managed identity
**And** no secret exists in source, in appsettings, or in the container image

**Given** the environment list
**When** it is enumerated
**Then** there are exactly two — Local via `aspire run`, and Azure — and no staging environment, because a second Azure environment would consume the same free grants that make production free

**Given** the free-tier vCore allowance
**When** 10% of the monthly remainder is reached
**Then** a metric alert fires
**And** `Behavior when free limit reached` is set to auto-pause until next month rather than paid overage, because exceeding the budget must be visible and not billed silently

**Given** liveness and readiness probes
**When** they answer
**Then** they do so from process state with no database round trip
**And** no component queries the database on a fixed interval, so an idle database is never woken

**Given** the first production deploy
**When** it is prepared
**Then** Azure SQL Database's exposure to the `SESSION_CONTEXT` parallel-plan defect has been confirmed directly and the finding recorded
**And** `MAXDOP = 1` is relaxed only if the pooled-connection isolation test remains green

**Given** PRD §6.4's data-protection gate
**When** the deployment is recorded
**Then** it is asserted in writing that this is a **single-operator deployment** claiming no data-protection posture, and that **the first Account created by anyone other than the operator makes the PRD non-compliant until amended**
**And** the five prerequisites that become live at that moment are named rather than left to be rediscovered: a lawful basis for holding email addresses and authored content; a stated data region with no replication outside it; encryption at rest asserted (NFR-6 covers transit only); a breach-notification position; and a subject-access or export route (FR-3 covers erasure only)
**And** the gate is recorded as an operational condition alongside the §6.3 cost ceiling rather than as a backlog item, because the PRD wrote it as a testable release condition and nothing else in this plan detects it

**Given** that gate
**When** the obligations parked against it are collected
**Then** the UX spine's accepted egress is carried here rather than left in `EXPERIENCE.md` alone: browser enhanced-spellcheck and cloud IME transmission of description text to third-party services is **accepted for v1** and becomes a real obligation at the gate
**And** the reason it is recorded here is stated: it was deliberately parked *"against §6.4's data-protection gate"*, so a plan that carries the obligation without the gate loses both — and the description editor is the product's largest free-text surface

## Epic 2: Track your own work on a Board

A solo user can create Projects, add Tasks, see them arranged in columns by Status, move them between columns by control, gesture or keyboard, reorder within a column, and switch to a filterable list — then close the tab. This completes UJ-1 end to end and is where the FR-28 × NFR-5 × NFR-9 collision is actually built and measured. The Status set is the one seeded at Space provisioning; configuring it is Epic 6.

**FRs covered:** FR-17, FR-18, FR-19, FR-20, FR-22, FR-23, FR-28, FR-29, FR-30

### Story 2.1: Create a Project and see what is in your Space

As someone with a Space of my own,
I want to create named Projects and see the ones in the Space I am in,
So that my work has somewhere to live before I put anything in it.

**Acceptance Criteria:**

**Given** an active Space holding no Projects
**When** Space home renders
**Then** it states "No Projects yet." and offers one action to create a Project
**And** it never prompts to configure Statuses, because UJ-1 requires meeting no empty state that asks for configuration

**Given** an active Space
**When** an Owner, Admin or Member creates a Project
**Then** it belongs to exactly that Space, fixed at creation, and its effective Status set is the Space default set with an empty delta
**And** a Viewer has no create affordance present at all

**Given** a Project
**When** an Owner, Admin or Member renames it
**Then** the new name is used, and a Viewer has no rename affordance present

**Given** an active Space
**When** its Projects are listed
**Then** the listing contains every Project in that Space and no Project from any other Space
**And** Viewers see the same Projects as Members, because Role affects what can be done to a Project rather than whether it is visible

**Given** a Space already holding 50 Projects
**When** a 51st is attempted
**Then** it is refused inside the same transaction with a machine-readable reason, and the copy states the bound plainly
**And** nothing is silently accepted, because a bound that is not enforced is a defect rather than a relaxation

**Given** the Project entity
**When** the schema test runs
**Then** it carries a non-nullable `SpaceId` with a row-level security policy, and its `Guid` id is generated application-side by `SequentialGuidValueGenerator`

**Given** every route on both surfaces
**When** they are enumerated
**Then** none moves a Project to another Space, so the cross-Space leak is closed by construction rather than by rule

### Story 2.2: Add Tasks and see them on a Board

As someone tracking my own work,
I want to add Tasks to a Project and see them arranged in columns by Status,
So that value has landed before I have made a single structural decision.

**Acceptance Criteria:**

**Given** a Project holding no Tasks
**When** the Board renders
**Then** it states "Nothing here yet." with one primary action to add a Task
**And** columns appear for the Project's effective Status set, in its effective order

**Given** a Project
**When** an Owner, Admin or Member creates a Task
**Then** it takes the first Status in that Project's effective set
**And** it belongs to exactly one Project at a time and permanently to one Space

**Given** a Viewer
**When** the Board renders
**Then** it is identical to a Member's Board except that the create affordance, the drag handle and the Move control are **absent** — not greyed, not tooltipped, gone
**And** there is no disabled state anywhere in the product for a Role reason

**Given** any Task in the Project
**When** the Board renders
**Then** that Task appears in exactly one column, and no Task appears twice or in none

**Given** a column header
**When** it renders
**Then** it shows the Status name, then the **true total** for that Status queried over the indexed predicate rather than the number of rows currently rendered, then the create affordance pushed right

**Given** a Task card
**When** it renders
**Then** the title is the only content and everything else is metadata set in monospace, and the card carries no shadow
**And** no count anywhere acts as a nudge — there are no badges, streaks or re-engagement prompts, because engagement is a counter-metric

**Given** a Project already holding 5,000 Tasks
**When** a 5,001st is attempted
**Then** it is refused inside the same transaction with a machine-readable reason and copy stating the bound

**Given** the Task entity
**When** the schema test runs
**Then** it carries a non-nullable `SpaceId` directly rather than reachable by join, so the row-level security predicate never needs one

### Story 2.3: Edit a Task's title, Status and due date

As someone whose work changes,
I want to change what a Task says and where it sits without saving,
So that keeping the Board true costs nothing.

**Acceptance Criteria:**

**Given** a Task
**When** an Owner, Admin or Member changes its title, Status or due date
**Then** the change renders locally at once without blocking on the network, and the server admits or rejects it afterwards
**And** a rejected change is reverted in the client replica, because the client is never the arbiter of what is in the Space

**Given** any surface in the product
**When** it is inspected
**Then** there is no save button and no auto-save indicator anywhere, because an indicator would imply a save that does not exist

**Given** a Viewer
**When** a Task opens
**Then** attributes render as text with no edit affordance present
**And** a write attempted over the API is refused regardless of what the interface offered, because the interface hiding an action is never the mechanism that enforces it

**Given** a Task
**When** its Status is set to a value in its Project's effective set
**Then** the change is accepted; and when set to a Status valid in a sibling Project but absent from this one, it is refused

**Given** the Task detail panel
**When** it opens
**Then** it opens over the Board one level deep and is never stacked two deep, `Esc` closes it, focus is trapped while it is open and returned to the originating Task card on close

**Given** Task detail in this epic
**When** it renders
**Then** no description editor is present — description editing is FR-31 and arrives with the merge port in Epic 7, which is also why nothing here writes a description field

**Given** a due date
**When** it is stored and sent
**Then** it is a `DateTimeOffset` in UTC, ISO 8601 with offset on the wire, and rendered in the viewer's own locale and zone

### Story 2.4: Define and apply Labels

As someone with more than a handful of Tasks,
I want to tag Tasks with Labels I define for the Space,
So that I can find groups of work without inventing a naming convention inside titles.

**Acceptance Criteria:**

**Given** an active Space
**When** an Owner or Admin defines a Label
**Then** it becomes available to every Project in that Space
**And** a Member or Viewer has no Label-definition affordance present

**Given** a defined Label
**When** an Owner, Admin or Member applies it to a Task
**Then** the application is recorded many-to-many, and a Viewer cannot apply one by any route including the API

**Given** a Label applied to at least one Task
**When** its deletion is attempted
**Then** it is refused until those applications are removed, so no Task ever references a Label that does not exist

**Given** the Label colour choice
**When** it is offered
**Then** it is a constrained palette that satisfies the rules by construction rather than a free colour picker
**And** every offered fill holds 3:1 against both the dark and the light card ground simultaneously, 4.5:1 against its own foreground text, and at least ΔE2000 10 from the focus ring, danger, accent and presence colours — accent and presence included because a Label confusable with the Offer indicator or with Presence defeats both

**Given** a Task carrying more than three Labels
**When** its card renders
**Then** at most three chips appear followed by a `+N` affordance
**And** the metadata row wraps to a second line before it ever scrolls horizontally, because horizontal overflow inside a card at the 320px reflow width is a 1.4.10 failure in the exact layout an audit is conducted in

**Given** the Label entity
**When** the schema test runs
**Then** it is defined per Space with a non-nullable `SpaceId` and a row-level security policy, and no Label is reachable from another Space

### Story 2.5: Delete a Task, and delete a Project

As someone tidying up,
I want to remove a Task or a whole Project and be told exactly what that destroys,
So that an irreversible action is never a surprise.

**Acceptance Criteria:**

**Given** a Task
**When** an Owner, Admin or Member deletes it
**Then** a confirm names that Task and states "This cannot be undone.", replacing its invoking panel's content in place rather than stacking on it
**And** a Viewer has no delete affordance present

**Given** a Project
**When** an Owner, Admin or Member deletes it
**Then** a confirm names the Project **and its Task count**, and when that count is zero it says so — the friction and the reassurance being the same mechanism

**Given** a confirmed Project deletion
**When** it completes
**Then** the Project and every Task in it are destroyed immediately and irreversibly, with no trash and no restore window

**Given** any destructive confirm
**When** it renders
**Then** the copy carries the destructiveness and the danger border is reinforcement only, never the reverse, because accent and danger are 1.19 apart and converge under deuteranopia
**And** it carries no icon and no illustration

**Given** all copy in the product
**When** it is reviewed
**Then** the word "archive" appears nowhere, because Yello has no archive and borrowing the word from the products users arrive from would promise a safety net that does not exist

**Given** a destructive confirm
**When** it is cancelled or dismissed
**Then** nothing is destroyed and focus returns to the invoking element

### Story 2.6: Move and reorder a Task by explicit control

As someone whose work moves between states,
I want a plain control that moves a Task to another Status and position,
So that moving work never depends on a gesture I cannot make.

**Acceptance Criteria:**

**Given** a Task
**When** its Move control is opened — from Task detail, or from the Task's context menu on the Board by pointer, by `Enter` on a focused card, or by the platform context-menu key
**Then** a picker names the destination Status and the position within it
**And** it carries no default selection, because the choice is consequential

**Given** the Move control
**When** any breakpoint renders
**Then** it is present at every one, never hover-only, and never offered second to a gesture
**And** it is absent only for a Role that cannot move Tasks at all

**Given** a Task moved to a column
**When** the move commits
**Then** its Status becomes that column's Status, and a position key is written for the moved Task alone — never a renumber of its siblings

**Given** the position key column
**When** the migration is inspected
**Then** it is declared `COLLATE Latin1_General_100_BIN2`, and the `(ProjectId, StatusId, PositionKey)` index carries the same collation
**And** this is set at column definition because `ALTER DATABASE … COLLATE` is unsupported on Azure SQL and cannot be retrofitted, while a case-insensitive comparison would make a mixed-case fractional index non-deterministic in `ORDER BY` and falsely duplicate on insert

**Given** two concurrent moves into the same column from two Sessions
**When** both complete
**Then** neither is silently discarded and both converge to one order, verified after reload
**And** live observation of the other participant's move arrives with the sync push in Epic 7

**Given** a completed move
**When** the page is reloaded
**Then** the order within each column is exactly as it was left, because ordering is per column and survives reload

**Given** any move
**When** it commits
**Then** an ARIA live region announces the destination Status, the position ordinal and the column total — "Moved to In Progress, position 3 of 12." — because for a blind User this announcement replaces the entire visual drop-zone system
**And** a cancelled move announces the restoration — "Returned to Todo, position 7."

**Given** a Task's position within its Status
**When** it is read over the API
**Then** it is readable so a consumer can reproduce what a User sees, and it is not writable, which keeps the convergence requirement confined to one surface

### Story 2.7: The accelerated pointer and touch paths

As someone using a mouse or a phone,
I want to drag a Task where I want it,
So that the fast way is available without being the only way.

**Acceptance Criteria:**

**Given** a pointer
**When** a Task is pressed and dragged
**Then** it lifts with a hard offset shadow, a 1° rotation and a 2px border, a dashed drop zone marks the destination, and the drop settles — all inside the near-instant motion budget
**And** the dashed treatment keeps the underlying order readable mid-drag

**Given** a touch surface
**When** a Task is long-pressed
**Then** the lift commits only after the long-press threshold, confirmed by the lifted treatment and by haptic feedback where the platform offers it

**Given** a pending lift
**When** the finger moves beyond the slop tolerance in **any** direction before the threshold
**Then** the gesture is treated as a pan and the pending lift is cancelled
**And** the rule is axis-agnostic with a real dead zone, because a single-axis rule breaks between 768px and 1279px where the Board itself scrolls horizontally, and a zero-tolerance rule cancels every lift on ordinary finger jitter

**Given** a pending or active lift
**When** scroll or pan intent is detected
**Then** it always wins over the lift

**Given** an active drag
**When** the pointer approaches a column edge
**Then** that column auto-scrolls; and near the viewport edge, the Board does

**Given** an active drag
**When** it is dragged back to origin or released outside any drop zone
**Then** nothing moves

**Given** `prefers-reduced-motion: reduce`
**When** a drag occurs
**Then** the lift, settle and column-reflow transitions do not run, and no information is lost because motion only ever reported a change conveyed structurally

**Given** these gestures exist
**When** NFR-9 is assessed
**Then** they do not discharge it — the keyboard path is separately mandated and is not derived from them

### Story 2.8: Operate the whole Board from the keyboard

As someone who does not use a pointer,
I want every Board operation available from the keyboard, including moving a Task between columns,
So that the Board is usable rather than merely visible.

**Acceptance Criteria:**

**Given** the Board
**When** `Tab` and `Shift+Tab` are used
**Then** focus moves between columns and controls in reading order; `↑` and `↓` move between Tasks within a column; `←` and `→` move between columns by **logical** direction, so they mirror under RTL exactly as the layout does

**Given** a traversal run across columns of differing lengths
**When** `←` or `→` is pressed repeatedly
**Then** column position is preserved by sticky origin index with clamping — the index the run started at is remembered and clamped to each column's length, rather than re-derived per hop

**Given** a focused Task card
**When** `Space` is pressed
**Then** the Task is picked up; `←` and `→` move it between columns, `↑` and `↓` within one, `Space` drops it and `Esc` cancels

**Given** focus anywhere other than a Task card
**When** `Space` is pressed
**Then** it keeps its native meaning — activating a button, or paging a scroll container — because Board columns *are* scroll containers and an unscoped rebinding would break both

**Given** a carried Task entering a column
**When** it lands
**Then** it lands at the same ordinal clamped to that column's length, with `↑` and `↓` adjusting before the `Space` commit
**And** the destination is never left unspecified, because a fractional index is computed between two concrete neighbours and without them the implementation has no pair to work from

**Given** any surface
**When** `Esc` is pressed
**Then** the innermost meaning wins — a pick-up is cancelled, or the topmost dialog closes

**Given** the arrow grammar
**When** the Board is implemented
**Then** it is an application-mode composite widget — a single tab stop with `role="grid"` or equivalent, managing focus internally
**And** the Move control from Story 2.6 remains fully operable in screen-reader browse mode, which is precisely why the arrow grammar is not the conformance path and `role="application"` is not used to force it

**Given** every Board operation available by pointer
**When** the keyboard is used
**Then** an equivalent exists for each, including moving a Task between columns

### Story 2.9: The Board at five thousand Tasks

As someone with a large Project,
I want the Board to stay fast, complete and keyboard-operable at the scale the product claims,
So that the stated envelope is a guarantee rather than an aspiration.

**Acceptance Criteria:**

**Given** a Project holding 5,000 Tasks
**When** a Board column page is read
**Then** it is fetched by keyset seek on the position key — `WHERE (ProjectId, StatusId) = … AND PositionKey > @last ORDER BY PositionKey` — and never by `OFFSET`
**And** its latency is independent of how deep in the column it sits

**Given** that scale
**When** the last page of a column is read
**Then** it meets the 300 ms p95 server-side read budget, as does the first — because a budget that holds only near the top of a column is not a budget

**Given** rendered rows
**When** the window changes
**Then** rows are appended to the DOM and never recycled, and DOM virtualisation is not used
**And** the reason is stated in the story: a recycled row silently re-points keyboard focus at a different Task, so the next keyboard move operates on the wrong one — a data-corrupting defect reachable only by keyboard and invisible to pointer testing

**Given** a column header at that scale
**When** it renders
**Then** it states the true total from a separate `COUNT` over the same indexed predicate
**And** `aria-setsize` carries that total while `aria-posinset` carries each Task's true ordinal, so the visible chip and assistive technology agree rather than one reporting 4,812 and the other a list of 30

**Given** the Board at that scale
**When** any Task is sought
**Then** every Task remains reachable and appears in exactly one column

**Given** loading or windowing
**When** it occurs
**Then** nothing is announced — announcements fire on deliberate action only: a pick-up, a move, a drop, or a filter count

**Given** focus resting on a Task when the window changes
**When** rendering completes
**Then** focus follows the Task **identity** and never the row element, and keyboard navigation drives the window rather than the reverse

**Given** the first measurement of this budget
**When** it is taken
**Then** the cold-start position is decided and recorded rather than left silent — either minimum replicas pinned to 1, or NFR-5 declared measured warm with the cold path explicitly exempted

### Story 2.10: View a Project as a filterable, sortable list

As someone looking for particular work,
I want the same Tasks as rows I can filter and sort,
So that I can answer a question the Board's shape does not answer.

**Acceptance Criteria:**

**Given** a Project
**When** the List View is opened from the Board's view toggle
**Then** the same Tasks render as rows, filterable and sortable by Status, due date and Label

**Given** the Assignee dimension
**When** this epic ships
**Then** it is absent from filter and sort, because no Task holds an Assignee until FR-21 in Epic 4
**And** Epic 4 adds that dimension together with its supporting composite index

**Given** any filter
**When** results render
**Then** no Task from another Project or another Space appears

**Given** a List View sort
**When** its query runs
**Then** the keyset is `(sortColumn, TaskId)` with `TaskId` as a mandatory tiebreaker and the seek predicate comparing the pair
**And** this is required because every offered sort column is non-unique, and a naive keyset on a tied value silently skips or repeats rows across a page boundary

**Given** a nullable sort column such as due date
**When** it is sorted
**Then** `NULL` is fixed at one end explicitly rather than left to the default

**Given** a sort offered in the interface
**When** the schema is inspected
**Then** a composite index carries both columns in that order
**And** a sort offered without one is a defect rather than a slow query, because at 5,000 Tasks it is a stated-budget failure

**Given** the List View at 5,000 Tasks
**When** it renders
**Then** it pages with a stated page size and never scrolls infinitely, and keyboard row traversal is operable throughout
**And** paging rather than virtualising sidesteps the focus-recycling problem entirely

**Given** a filter yielding nothing
**When** it renders
**Then** it states "No Tasks match." and nothing else — no suggestion to broaden, and no count of what was excluded

**Given** a filter change
**When** results settle
**Then** the result count is announced politely

### Story 2.11: The Board on a small viewport

As someone reading the Board on a phone, or at 400% zoom,
I want one column at a time with a way to reach the others,
So that the Board stays correct at the size an accessibility audit is conducted at.

**Acceptance Criteria:**

**Given** a viewport of 1280px or wider
**When** the Board renders
**Then** all Status columns are visible side by side, and the context bar carries Space name, Project and Role chip

**Given** a viewport between 768px and 1279px
**When** the Board renders
**Then** columns remain side by side with horizontal Board scroll, and the context bar drops the Project name while keeping Space and Role

**Given** a viewport below 768px
**When** the Board renders
**Then** one column shows at a time behind a Status pager, implemented as a **tablist** over the Project's effective Status set with the column as its panel

**Given** the Status pager
**When** arrow keys are used
**Then** focus moves between tabs, and on change the new Status and its true count are announced politely
**And** this is specified rather than left open because the pager is the *only* route to a Status at that width

**Given** a viewport below 768px
**When** a cross-Status move is made
**Then** it uses the Move control rather than a gesture, because a `Space`-plus-arrow move would send a carried Task into an off-screen column with no visible drop zone
**And** a committed move advances the pager to follow the Task

**Given** any breakpoint
**When** the context bar renders
**Then** the Role chip survives, and if something must be dropped it is the Project name first, then the switcher chevron label, and never the Role

**Given** 400% zoom on a 1280px monitor, yielding a 320px CSS viewport
**When** the Board is audited against 1.4.10
**Then** no content requires two-dimensional scrolling
**And** the two-dimensional-layout exemption a Kanban board would normally claim is deliberately declined, the horizontal axis being eliminated instead

**Given** every structural rule in the product
**When** surfaces are inspected
**Then** logical properties are used throughout rather than `left` and `right`, so layout, column order, drag direction and the arrow keys all mirror under RTL
**And** the Status *sequence* does not reverse, being data rather than layout

## Epic 3: Several Spaces, cleanly separated

A user can hold more than one Space — client work apart from personal work — rename them, delete them, and move between them with the entire working surface changing and nothing at all carrying over. Small in requirement count and heavy in isolation work: this is the first time a single Account holds two Spaces, which is exactly where a pooled-connection or client-cache leak would surface, and where the sharpest form of the invariant — that owning one Space grants nothing in another — becomes measurable.

**FRs covered:** FR-5, FR-6, FR-7

### Story 3.1: Create and rename a Space

As a freelancer whose client work should not sit beside my personal work,
I want to create more Spaces and give them names that mean something,
So that separate bodies of work are separate by construction rather than by convention.

**Acceptance Criteria:**

**Given** an authenticated Account
**When** it creates a Space
**Then** that Account becomes the Owner, the Space holds exactly one Membership, and it carries the default Status set and no Projects

**Given** a newly created Space
**When** every other Space is inspected
**Then** the creation has no effect on, and no visibility from, any of them

**Given** a Space
**When** an Owner or Admin renames it
**Then** the new name is used, and a Member or Viewer has no rename affordance present

**Given** Space settings
**When** it is opened
**Then** it is reachable from the context bar and available to Owner and Admin only
**And** the controls an Admin lacks are absent rather than disabled

**Given** two Accounts each owning a Space with an identical name
**When** either is read
**Then** there is no collision and no disclosure, because Space names are not unique across Yello

**Given** an Account already holding Membership in 50 Spaces
**When** it attempts to create a 51st
**Then** it is refused inside the same transaction with a machine-readable reason, and the copy states the bound plainly

**Given** that bound check reads the acting Account's own Space count on a surface which is neither the Space switcher nor Account settings
**When** AD-24 is consulted
**Then** the amendment naming this read is recorded rather than left implicit
**And** the reason is stated: the count is the caller's own and discloses no other Account's data, but AD-24 exists precisely so that an unauthorised read is never solved by disabling row-level security, opening a second connection, or inventing a bypass that then spreads

**Given** the Space name input
**When** it renders
**Then** it carries `autocomplete="off"` and a non-reusable field name
**And** the reason is stated: autofill is origin-scoped and Account-agnostic, so it would otherwise offer one Account's Space name to the next Account using that browser profile

### Story 3.2: Switch between Spaces with nothing carrying over

As someone who works in several Spaces in a day,
I want a switch to change the entire working surface and leave nothing of the previous one behind,
So that I am never in doubt which Space I am in, and nothing from one can ever be rendered in another.

**Acceptance Criteria:**

**Given** an Account holding Membership in more than one Space
**When** the switcher opens
**Then** it lists exactly those Spaces by name and nothing else, and it remains the only list of Spaces anywhere in the product

**Given** a Space is selected
**When** the switch completes
**Then** the acting Role becomes whatever that Account holds in the destination with no carry-over
**And** no filter, sort, scroll position, open Task or Project selection survives

**Given** a Space switch, a sign-out, an Account switch, or back/forward navigation
**When** any of them occurs
**Then** everything Space-scoped for the departed Space is purged **synchronously, before any render** — replica, projection, cached Board, queued inbound frames, pending announcements, Assignee and Label lists, Status sets, filter and sort state, scroll position, and every id-keyed cache

**Given** the purge rule
**When** it is implemented or reviewed
**Then** it is treated as a **data** rule rather than a presentation one: "switching Space carries nothing over" means the data is gone, not merely unrendered

**Given** any Space-scoped content
**When** browser storage is inspected
**Then** none of it appears in `localStorage`, `sessionStorage` or IndexedDB
**And** the reason is stated: this also stops browser session-restore repopulating a Space after access has ended, and stops one Account's content surviving sign-out on a shared machine

**Given** the first read of a Space
**When** it is in flight
**Then** nothing renders optimistically from cache before the server answers, because rendering a remembered Board while the 404 is in flight is a disclosure with a short lifetime rather than a fast interface

**Given** a Space, Project or Task id in a URL
**When** it is followed
**Then** it confers nothing and renders nothing from cache before the server answers
**And** the client never issues a Space-scoped request for a Space id absent from the current switcher response, which closes both a warm-started remembered Space and a deep link followed after removal

**Given** scroll restoration
**When** a Space-scoped route is revisited
**Then** it is disabled, because it is browser-owned and URL-keyed and would otherwise re-present a previous Space's position independently of every purge rule above

**Given** a switch completes
**When** focus settles
**Then** it moves to the context bar with the new Space and Role announced politely
**And** this is specified rather than inherited from the generic dialog rule, because returning focus to an element whose accessible name has changed does not reliably re-announce, and Space switching is one of NFR-9's five gated flows

**Given** one Account with two browser tabs open on two different Spaces
**When** each is used
**Then** each tab holds its own Space context and the two share no state

### Story 3.3: Delete a Space, and belong to none

As someone finished with a body of work,
I want to destroy a Space and be told exactly what that takes with it,
So that the most destructive operation in the product is never taken by accident.

**Acceptance Criteria:**

**Given** a Space
**When** its Owner deletes it
**Then** its Projects, Tasks, Memberships and Invitations are destroyed
**And** an Admin has no delete affordance present at all

**Given** the delete confirm
**When** it renders
**Then** it names the Space, states its Project and Task counts, and states that other Accounts lose access — §6.2 requiring this at the point of the action

**Given** the delete confirm
**When** it asks for confirmation
**Then** it requires the Space name to be typed, in a field carrying `autocomplete="off"` and a non-reusable field name

**Given** a confirmed deletion
**When** it completes
**Then** it is immediate and irreversible with no trash and no restore window
**And** every API Token issued for that Space is invalidated in the same transaction

**Given** other Accounts holding Membership in the deleted Space
**When** the deletion completes
**Then** they lose access to it without losing their own Spaces, their own Accounts, or any other Membership

**Given** an Account deleting its last remaining Space
**When** it completes
**Then** belonging to no Space is a valid state — the Account persists and is offered the chance to create a Space rather than having one created for it
**And** no Space anywhere in the product is undeletable

**Given** the delete confirm invoked from inside Space settings
**When** it opens
**Then** it replaces that panel's content in place rather than stacking on it, because modals go one level deep and never two

**Given** the deletion copy
**When** it is reviewed
**Then** it names the object — "Delete Northwind Redesign?" — rather than "Delete this item?"
**And** it states "This cannot be undone." with no sentence of reassurance placed before the fact

### Story 3.4: Prove that owning one Space grants nothing in another

As the operator deciding whether Yello can ship,
I want the isolation suite to cover one Account holding several Spaces, on both surfaces and in the client,
So that the sharpest form of the invariant is measured rather than assumed.

**Acceptance Criteria:**

**Given** one Account owning two Spaces
**When** it requests a Project, Task or Label belonging to the second while the first is its active Space
**Then** the request is refused exactly as it would be for an Account holding no Membership anywhere near it

**Given** an Owner of one Space
**When** it acts in another Space where it holds a lesser Role or none at all
**Then** it has no elevated standing whatsoever, because Role is per Space and ownership confers nothing beyond its own

**Given** an API Token bound to one of an Account's Spaces
**When** it is used against another Space that same Account owns
**Then** it is refused
**And** the suite carries this case explicitly, because it is the one an implementer is most likely to treat as harmless

**Given** consecutive requests for two different Spaces from the same Account on one pooled physical connection
**When** both are served
**Then** neither discloses the other's rows

**Given** a switch from a populated Space to another
**When** the destination has rendered
**Then** no Task title, Project name, Label, Status name or count belonging to the departed Space is present in the DOM or in client memory

**Given** every case added by this story
**When** the suite runs
**Then** each executes against both the browser and the API surface, and a case existing for one and not the other fails as a gap

**Given** an `AccessRefusal` record produced by any of these cases
**When** it is inspected
**Then** it is classified `CrossSpace` rather than `InsufficientRole`
**And** the client has manufactured none of them during ordinary use, because a client that does buries real probing in its own noise and defeats the record's purpose

## Epic 4: Bring people into one piece of your work

An Owner or Admin can invite someone by email address at a fixed Role, and that person — with or without an existing Account — gains Membership in exactly one Space and can see nothing else in Yello. Roles can be changed and Memberships removed within each Role's constraints. This is where the Role matrix is finally exercised with someone other than an Owner in the room, and where UJ-4's climax actually lands: a Viewer's Board with every create-and-edit affordance absent, not disabled.

**FRs covered:** FR-10, FR-11, FR-12, FR-13, FR-14, FR-21, FR-39, FR-40

### Story 4.1: Invite an email address to a Space at a Role

As an Owner or Admin of a Space,
I want to invite someone by email address at a Role I choose,
So that I can bring a collaborator into one piece of my work without exposing anything else.

**Acceptance Criteria:**

**Given** an active Space
**When** an Owner or Admin issues an Invitation to an email address at a Role
**Then** the Invitation carries exactly one Space and exactly one Role, both fixed at issue time
**And** a Member or Viewer has no invite affordance present

**Given** the Role picker at issue
**When** it renders
**Then** Owner is not offerable, because ownership moves only by an Ownership Offer accepted under FR-42

**Given** any email address
**When** it is invited
**Then** it is accepted with no domain restriction, no allowlist, and no requirement that it already have an Account

**Given** an address that already holds a Membership in this Space
**When** it is invited
**Then** the request is refused
**And** this refusal *is* disclosable, because the issuer can already see that Membership

**Given** an address that does, and separately does not, already have a Yello Account
**When** each is invited
**Then** the response to the issuer is identical in status, body, shape and duration
**And** issuing an Invitation therefore never discloses whether someone uses Yello

**Given** a successful issue
**When** the transaction commits
**Then** one email is enqueued through the outbox **in the same transaction** and dispatched in-process on enqueue
**And** the recovery sweep for messages unflushed by a crash runs at process start and otherwise piggybacks on inbound request traffic, so no timer ever wakes an idle database

**Given** the delivered email
**When** it is read
**Then** it names the Space, the Role offered and who issued it
**And** it discloses nothing about the Space's contents, its other Members, or any other Space

**Given** the Invitation record
**When** it is stored
**Then** it carries an `ExpiresAt` evaluated only by the shared `State = Pending AND ExpiresAt > now` predicate, server-side inside each guarded statement's own `WHERE` clause against the database clock
**And** no job and no timer ever writes its expired state

### Story 4.2: See and revoke pending Invitations

As an Owner or Admin,
I want to see what Invitations are outstanding and be able to withdraw one,
So that an offer I no longer mean can be taken back before it is accepted.

**Acceptance Criteria:**

**Given** Space settings
**When** the Invitation list renders
**Then** it shows each pending Invitation with its address, its Role and who issued it, to Owners and Admins only

**Given** a pending Invitation
**When** any Owner or Admin revokes it — including one who did not issue it
**Then** it can no longer be accepted

**Given** an Invitation whose issuer has since been demoted, removed, or had their Account deleted
**When** it is inspected
**Then** it remains valid and revocable by any remaining Owner or Admin
**And** the reason is stated: it was legitimately issued and does not depend on its issuer's continuing authority

**Given** an already-accepted Invitation
**When** revocation is attempted
**Then** it is refused, because revocation is possible only before acceptance and removing an accepted invitee is FR-14 rather than FR-12

**Given** an Invitation leaving `Pending` by any route — accepted, revoked or expired
**When** the record is inspected
**Then** its terminal state is retained rather than the row being deleted
**And** no product surface reads it, its purpose being to keep SM-4 and SM-C3 derivable by an operator applying the same predicate

**Given** terminal Invitations
**When** the Invitation list renders
**Then** they are not shown, the list being what is outstanding rather than what has happened

**Given** a revocation
**When** it completes
**Then** no further email is sent, because there is exactly one email per Invitation and an already-delivered email cannot be recalled

### Story 4.3: Accept an Invitation

As someone who has been invited,
I want to see what I am being offered and then deliberately accept it,
So that I join exactly one Space at exactly one Role, and nothing joins me to anything by accident.

**Acceptance Criteria:**

**Given** an Invitation link
**When** it is fetched
**Then** the response is a safe, side-effect-free read that creates nothing and changes nothing
**And** a mail security scanner, a link prefetcher, or anyone the mail was forwarded to therefore creates no Membership

**Given** the Invitation view
**When** it renders
**Then** it names the Space, the Role offered and who issued it, with the most generous padding in the product
**And** no Space name appears in its `<title>` or in any `og:` or `twitter:` metadata, the page is `noindex`, and no Space name or id appears in the URL beyond the opaque token — because a link pasted into a chat tool has its preview scraped into the logs of people with no Membership

**Given** an invitee with no Account
**When** they complete registration from the Invitation
**Then** that registration **is** their act of acceptance, it provisions their own Personal Space in the same transaction independently of the invited Space, and it creates exactly one additional Membership in the invited Space at exactly the invited Role
**And** the slice that creates the Account is the same single slice used by direct registration, never a second provisioning path

**Given** an invitee who already holds an Account
**When** they accept
**Then** acceptance is an explicit confirmation issued as its own state-changing request, authorised on the authenticated Account matching the address the Invitation names
**And** loading a URL is never sufficient — the token identifies *which* offer is in play and is never the authority for accepting it

**Given** an accepted Invitation
**When** a second acceptance is attempted with it
**Then** it is refused under a guarded `WHERE State = Pending` with a rowcount check, so no second Membership is created

**Given** an Invitation that is revoked, accepted, expired, lapsed or entirely unrecognised
**When** the acceptance route is followed
**Then** one identical response is returned for all five, identical in words, shape and duration: "This invitation is no longer valid."
**And** it discloses neither the Space, its contents, nor who revoked it — the uniformity being the point, since a distinct response for a token that never existed would make the route an existence oracle

**Given** a different Account is signed in when the link is opened
**When** the mismatch is detected
**Then** it is stated without echoing either address, and sign-out is offered
**And** the reason is stated: the helpful version discloses the invited address to whoever holds the link, which on a forwarded link is a stranger

**Given** an invitee with an existing Account
**When** they join
**Then** no second Account is created, and their other Memberships are neither visible to nor affected by the inviter

**Given** a Space already holding 100 Memberships
**When** acceptance would create the 101st
**Then** it is refused inside the same transaction with a machine-readable reason, from the Story 1.6 bound registry rather than from a check written here
**And** the copy states the bound plainly — "This Space has 100 Memberships, the maximum." — because a bound must degrade visibly, never silently
**And** the refusal lands on **acceptance** rather than on issue, because acceptance is the transaction that creates the Membership; an Invitation issued while the Space had room may be accepted after it filled, so refusing at issue would check the wrong moment and refusing nowhere would breach the bound

### Story 4.4: Manage Memberships and Roles

As an Owner or Admin,
I want to see who is in this Space and change what they can do,
So that standing matches the working relationship without anyone having to leave and rejoin.

**Acceptance Criteria:**

**Given** Space settings
**When** the Membership list renders
**Then** it shows every Membership in the active Space with its Role, to Owners and Admins only
**And** it pages rather than growing unbounded at the 100-Membership bound

**Given** the Membership list
**When** it renders
**Then** email addresses are visible **here and nowhere else in the product** — not in the Assignee picker, not in Presence, not in an avatar tooltip, not in attribution, and not in any announcement

**Given** two Memberships with identical display names
**When** they render anywhere outside the Membership list
**Then** they are disambiguated by a Membership-scoped discriminator and never by the email address
**And** the reason is stated: 20px monospace initials and a display name do not disambiguate two people, and the address is the field every implementer reaches for

**Given** an Admin
**When** the Role picker renders
**Then** it offers Member ↔ Viewer only, and the controls an Admin lacks are absent rather than disabled
**And** only an Owner can promote a Membership to Admin or demote one from Admin

**Given** any Role change
**When** it is attempted
**Then** no change can produce a second Owner or remove the sole Owner, guaranteed by the filtered unique index rather than by application logic alone

**Given** a Role change
**When** the transaction commits
**Then** `MembershipChanged` publishes at the transaction boundary in-process, once per affected Account
**And** the change governs that Account's **very next request** with no tolerance, on the browser and the API alike, because no cache may outlive a request

**Given** attribution of authored content
**When** it renders
**Then** it uses the name captured at authoring time or held on the Membership row, never a live global Account lookup
**And** the reason is stated: resolving a current name globally would propagate a later name change into a Space the reader no longer shares with that person

### Story 4.5: Remove a Membership, or leave a Space

As an Owner or Admin ending a working relationship, or as anyone who wants out,
I want access to end immediately and completely,
So that when I remove someone I can believe it took effect.

**Acceptance Criteria:**

**Given** a Member or a Viewer
**When** an Admin removes them
**Then** the removal succeeds
**And** an Admin cannot remove the Owner or another Admin, those controls being absent rather than refused

**Given** the Owner's Membership
**When** removal is attempted by anyone, including the Owner
**Then** it is refused while it holds ownership
**And** the Owner's row carries no remove control for anyone, so the exits are offering ownership and having it accepted, or deleting the Space

**Given** any Account
**When** it removes its own Membership
**Then** it succeeds unless it holds ownership, and it retains its Account, its own Spaces and every other Membership

**Given** a removal
**When** it commits
**Then** access is revoked immediately, `MembershipChanged` publishes at the transaction boundary, every API Token that Account holds for this Space is invalidated in the same transaction, and Tasks it was Assignee of become unassigned rather than deleted

**Given** a removal
**When** Invitations that Membership issued are inspected
**Then** they are **not** deleted — removing a Membership does not withdraw offers it legitimately made

**Given** the removal confirm
**When** it renders
**Then** it names the person and states whether they currently hold a live session, so the remover knows they are interrupting someone mid-work
**And** it sits on the blast-radius ladder between Task and Project, because it ends someone's access to everything in the Space immediately

**Given** a removed Account
**When** it requests anything in that Space by any route
**Then** it receives the boundary 404, indistinguishable from a resource that does not exist
**And** the copy does not distinguish "removed" from "never existed", the usability cost being paid with deliberately ambiguous copy rather than with a disclosure

### Story 4.6: Assign a Task, and be told

As someone dividing up work,
I want to give a Task to someone in this Space and have them know,
So that responsibility is recorded where the work is rather than in a message.

**Acceptance Criteria:**

**Given** a Task
**When** an Owner, Admin or Member assigns it
**Then** only Memberships of that Task's own Space are offered as Assignees and only those are accepted, including via the API
**And** an Account with no Membership in the Space cannot be set as Assignee by any route

**Given** the Assignee reference
**When** the schema is inspected
**Then** it is a composite foreign key carrying `SpaceId` — `(SpaceId, MembershipId)` — so the same-Space constraint holds by construction rather than by validation

**Given** a Viewer
**When** they are assigned a Task
**Then** the assignment is permitted and grants them no write capability over it
**And** demoting an Assignee to Viewer does not unassign their Tasks, because responsibility and capability are deliberately separable

**Given** an assignment to an Account other than the acting one
**When** it commits
**Then** that Account is notified by email naming the Space, Project and Task and nothing from any other Space
**And** an Account is never notified of its own action

**Given** a notification send
**When** the record is inspected
**Then** it retains the Space, the kind and the timestamp, and never message content or recipient address
**And** no product surface reads it, its purpose being to keep SM-C4 derivable

**Given** the Assignee picker
**When** it renders
**Then** each Membership shows a display name and initials only, and never an email address

**Given** the List View
**When** filter and sort render
**Then** the Assignee dimension is now present with its supporting composite index, completing FR-30
**And** filtering by Assignee offers only Memberships of the active Space

### Story 4.7: A Viewer's Space, and a Role that drops beneath you

As someone whose standing differs between Spaces, or changes while I am working,
I want the interface to tell me what I may do without my having to try,
So that I can read my standing rather than discover it on failure.

**Acceptance Criteria:**

**Given** a Viewer in a Space
**When** every surface renders
**Then** no create, edit, move, delete or invite affordance is present anywhere — absent, not greyed, not tooltipped
**And** there is no disabled state anywhere in the product for a Role reason; where one seems needed, the answer is removal

**Given** a Viewer
**When** any write is attempted over the API
**Then** it is refused regardless of what the interface offered
**And** absence is therefore an honesty contract with the User rather than a security control

**Given** each row of the Role capability matrix
**When** it is exercised against Memberships at Owner, Admin, Member and Viewer created through the product's own paths rather than seeded
**Then** the matrix governs the outcome in every case

**Given** an Account whose Role drops
**When** the change is pushed to it
**Then** the removal is **narrated before the surface settles**, rather than affordances silently disappearing
**And** the reason is stated: by the absence rule's own design, no residual control is left to explain the disappearance, so silence is hostile

**Given** an Admin sitting in Space settings
**When** they are demoted to Member
**Then** they are told — "You're now a Member. Space settings is no longer available." — focus moves to the narration, and they are then routed to Space home
**And** the surface is never blanked silently and never left half-rendered

**Given** the context bar
**When** a Role changes
**Then** the Role chip and the accessible name both update to the new Role and what it permits

**Given** a permission-change announcement
**When** it is delivered
**Then** it reaches only the client context whose active Space matches the change — a per-tab filter, never a shared broadcast
**And** the copy carries no Space name in any case, because a broadcast would otherwise need to disambiguate which Space ended and would speak that name in a tab showing a different Space

## Epic 5: Hand a Space over and leave cleanly

An Owner can offer ownership of a Space to a named Membership at any Role, that person can accept or decline it with full knowledge of what accepting commits them to, and once it is accepted the former Owner can remove their own Membership and — if they own nothing else — delete their Account. UJ-8 end to end, closing the trap that made an Owner unable to leave and a recipient unable to erase themselves.

**FRs covered:** FR-3, FR-8, FR-42, FR-43

### Story 5.1: Offer ownership of a Space, and tell the recipient

As an Owner who wants to hand a Space on,
I want to offer ownership to someone already in it and have them know it is waiting,
So that leaving is possible without anyone being made an Owner against their will.

**Acceptance Criteria:**

**Given** a Space
**When** its Owner offers ownership to an existing Membership at any Role
**Then** an `OwnershipOffer` is created naming that Membership, carrying `SpaceId`, `ExpiresAt` and a `State` of `Pending`
**And** it cannot be offered to an email address, to an Invitation, or to an Account holding no Membership in that Space

**Given** the offer
**When** it is created
**Then** ownership does not move — the offering Owner remains the Owner with every capability of the Role until it is accepted

**Given** a Space with a pending offer
**When** a second offer is attempted
**Then** it is refused by a filtered unique index on `OwnershipOffer(SpaceId) WHERE State = Pending`
**And** this is the same schema-level guarantee that gives Owner uniqueness, for the same reason

**Given** a pending offer
**When** the offering Owner revokes it
**Then** every Membership and Role is left exactly as it was

**Given** a pending offer
**When** the named recipient's Membership is removed, or their Account is deleted
**Then** the offer **lapses** inside that same transaction rather than being deleted or left orphaned, and no Role changes
**And** this is lapse *by event*, which is written — as distinct from lapse by the passage of time, which is not

**Given** a pending offer
**When** the offering Owner attempts to remove their own Membership or delete their Account
**Then** both are still refused, because making an offer is not itself an exit

**Given** the offer
**When** it is created
**Then** exactly one email is enqueued through the existing outbox **in the same transaction as the insert** and dispatched in-process on enqueue
**And** nothing is emitted on revoke, decline or lapse

**Given** that email
**When** it is read
**Then** it names the Space, states that the recipient is being offered ownership of it, and identifies who offered it
**And** it discloses nothing about the Space's contents, its other Memberships, or any other Space — stated as a requirement because delivery is irreversible: a pending offer lapses when the recipient's Membership ends, but an email already sent cannot be recalled, so anything it carried outlives the Membership that justified carrying it
**And** it carries no means of accepting; acceptance happens inside the Space

**Given** the Ownership panel in Space settings
**When** the Owner views a pending offer
**Then** it shows the named recipient and the expiry computed against the **server** clock, and offers revocation
**And** it is Owner-only, an Admin having no ownership affordance present

### Story 5.2: Accept an Ownership Offer

As the person being offered a Space,
I want to understand exactly what accepting commits me to and then accept it in one clean step,
So that ownership arrives by my own agreement and the Space never holds zero or two Owners.

**Acceptance Criteria:**

**Given** a pending offer naming my Membership in the active Space
**When** the context bar renders
**Then** an Offer indicator is present in the accent colour — the only accent element in chrome, because it is the only chrome element that is a proposition rather than a statement
**And** it disappears the moment the offer leaves `Pending`

**Given** the offer dialog
**When** it opens
**Then** it explains in full what accepting commits me to: my Membership cannot be removed while I hold ownership, and my Account deletion is refused until I transfer the Space onward or delete it
**And** this is one of only two places the product's terseness is deliberately overridden, because nobody should agree to that from a one-line dialog

**Given** the dialog
**When** Accept and Decline render
**Then** both are explicit, neither is a default, and neither is pre-focused, because a mis-hit `Enter` must not transfer a Space

**Given** acceptance
**When** it is authorised
**Then** it is authorised by **row identity** — is the caller the named Membership — and never by Role
**And** this is the only capability in Yello decided outside the FR-16 matrix, because FR-8 permits any Role to be named

**Given** acceptance
**When** the transaction runs
**Then** the Role change is two explicit ordered `ExecuteUpdate` statements inside one transaction: demote the current Owner to `Admin`, **then** promote the recipient to `Owner`

**Given** that ordering
**When** the implementation is reviewed
**Then** tracked-entity `SaveChanges` is not used
**And** the reason is stated: EF Core picks its own statement order for two tracked rows, SQL Server has no deferred constraint enforcement, and promote-before-demote transiently writes a second `Owner` row that fails the filtered unique index — while demote-first removes the row from that filtered index entirely, leaving zero matching rows, and zero never violates uniqueness

**Given** acceptance commits
**When** observable state is inspected at any point
**Then** the Space never holds zero Owners and never two, and an invariant test asserts this across every Space
**And** the previous Owner is now an Admin without having lost access

**Given** acceptance
**When** `MembershipChanged` publishes
**Then** it publishes **once per affected Account** — twice here — because a lease carries `Role` and two Roles moved

**Given** the new Owner
**When** they act afterwards
**Then** they are bound by every rule binding any Owner: their Membership cannot be removed while they hold ownership, and their Account deletion is refused until they transfer onward or delete the Space

### Story 5.3: Decline, revoke, lapse — and the states that are no longer open

As anyone touching an offer that has already been settled,
I want a clear, non-disclosing answer,
So that a race or a stale tab never transfers a Space and never reveals one.

**Acceptance Criteria:**

**Given** a pending offer
**When** the named Membership declines it
**Then** every Membership and Role is left exactly as it was, and the offering Owner is told it was declined

**Given** a declined or lapsed offer
**When** acceptance is later attempted
**Then** it is refused, and the Owner must make a new offer

**Given** an offer no longer `Pending` — already accepted, declined, revoked, lapsed, or lost to a concurrent offer hitting the filtered index
**When** a transition is attempted by a caller holding a Membership in that Space
**Then** **409** is returned with a stable problem `type`, never 404
**And** the reason is stated: the caller holds a Membership, so the boundary rule does not apply, and inventing a 404 here would be a divergence rather than a disclosure

**Given** the same route
**When** it is called by an Account holding no Membership in that Space
**Then** **404** is returned, because 409 would confirm that the Space and the offer exist

**Given** that pair of requirements
**When** the handler runs
**Then** the Space context is resolved **first** and `State = Pending` evaluated **second**, never the reverse

**Given** the offer dialog open when the offer stops being pending
**When** the User answers
**Then** they see "This offer is no longer open." as a designed state rather than an error toast

**Given** expiry by the passage of time
**When** it occurs
**Then** no job and no timer writes the lapsed state — it is computed on read by the shared predicate against the database clock
**And** there is consequently **no event on which to hang a notification**, which is why nothing is emailed on lapse; this is a structural consequence rather than a policy choice

**Given** any transition
**When** it is guarded
**Then** it uses `WHERE State = Pending` with a rowcount check rather than relying on the endpoint's `Idempotency-Key` alone
**And** the reason is stated: no route, the API included, may admit ownership arriving unrequested

### Story 5.4: Delete your Account

As someone leaving Yello,
I want to erase my Account without orphaning a Space or destroying anyone else's work,
So that leaving is genuinely available to me and costs nobody else what they have made.

**Acceptance Criteria:**

**Given** an Account owning no Space
**When** it deletes itself
**Then** every remaining Membership is removed and the Account disappears from every Space it belonged to

**Given** an Account owning one or more Spaces
**When** deletion is attempted
**Then** it is refused inside the same transaction that checks for owned Spaces
**And** the refusal **names every Space still owned**

**Given** that refusal
**When** it renders
**Then** it states exactly two exits — transfer the Space and have the offer accepted, or delete the Space — and does not imply a third
**And** the reason is stated: because a transfer requires someone else to accept, deleting the Space is the only exit the Account controls unilaterally

**Given** deletion completes
**When** surviving data is inspected
**Then** Tasks the Account was Assignee of are unassigned rather than deleted, and content it authored in Spaces it did not own is retained with attribution rendering as a deleted Account

**Given** a deleted Account's attribution
**When** an avatar renders
**Then** it is a tombstone — the same shape, muted, with no initials — never blank and never removed
**And** attribution therefore survives without the identity persisting

**Given** a pending Ownership Offer naming that Account's Membership
**When** the Account is deleted
**Then** that offer lapses inside the deletion transaction

**Given** deletion completes
**When** the email address is used to register again
**Then** a new Account is created inheriting no Membership, no Space and no history

**Given** deletion
**When** it is offered
**Then** it lives on Account settings, one of only two Account-scoped surfaces
**And** the read that enumerates owned Spaces in the refusal is recorded against the AD-24 amendment, being the caller's own Memberships and Roles rather than anyone else's

## Epic 6: Shape Statuses per Project and retire work safely

An Owner or Admin can add, remove, rename and reorder a Project's Statuses as a delta over the Space defaults, and can change the Space defaults with every divergent Project reported and asked about rather than guessed at. A Task can be moved to another Project in the same Space, singly or a whole Status at a time, which makes retiring a Project safe against a deletion that destroys its Tasks irreversibly. UJ-9.

**FRs covered:** FR-24, FR-25, FR-26, FR-27, FR-41

### Story 6.1: Shape a Project's Statuses

As an Owner or Admin,
I want to add, rename and reorder the Statuses in one Project,
So that a Project's Board can reflect how that work actually flows without changing anyone else's.

**Acceptance Criteria:**

**Given** a Project
**When** an Owner or Admin adds, renames or reorders a Status
**Then** the change is recorded as a **delta over the Space defaults**, keyed on Status identity rather than on name
**And** Members and Viewers have no delta affordance present

**Given** a `StatusDefinition`
**When** it is renamed at either level
**Then** its stable identifier survives the rename
**And** the reason is stated: a Space-level rename must be able to detect that a Project renamed *the same* Status, which name-keyed deltas cannot express

**Given** a Project's delta
**When** it is stored
**Then** it is a set of operations keyed by Status identity and never a materialised list
**And** no table stores a Project's effective Status set

**Given** a Project
**When** its effective Status set is needed
**Then** it is computed on read as the Space defaults with that Project's delta applied in the delta's order, with caching permitted only within a single request

**Given** two Projects in one Space
**When** each holds a different delta
**Then** they hold different effective sets simultaneously
**And** a Status valid in one is neither offered nor accepted for a Task in the other

**Given** the Status delta editor
**When** it renders
**Then** it shows the effective set **and which entries come from the Space defaults versus this Project's delta**
**And** the reason is stated: that distinction is what makes the Space-level cascade comprehensible when it fires

**Given** a Project's effective set
**When** any operation would leave it empty
**Then** the operation is refused

**Given** the Board
**When** a delta has reordered Statuses
**Then** columns appear in the Project's effective order
**And** there is no mode by which a Project toggles between inheriting and overriding, and no operation reverts a Project to the defaults — its Status set is simply editable for the life of the Project

### Story 6.2: Remove a Status from a Project by migrating its Tasks

As an Owner or Admin,
I want removing a Status to move the Tasks sitting in it rather than stranding them,
So that a Task can never hold a Status its Project does not expose.

**Acceptance Criteria:**

**Given** a Status occupied by at least one Task
**When** its removal is requested
**Then** a destination Status must be supplied for every occupying Task in the same operation
**And** the removal does not take effect without one — there is no partial application

**Given** the destination picker
**When** it renders
**Then** it carries **no default selection**, because a default would decide for the Admin

**Given** the destination choice
**When** it is validated
**Then** it must exist in the effective set that will be in force **after** the removal completes
**And** a Task therefore cannot be mapped onto a Status that the same operation also removes

**Given** the removal and the remapping
**When** they are applied
**Then** they are one transaction with no partial application
**And** no endpoint accepts a Status removal without the mapping it requires

**Given** a Status that no Task occupies
**When** it is removed
**Then** no mapping is required and it succeeds directly

**Given** a Status removed from a Project
**When** it is added back later
**Then** it may be, at any time

**Given** the whole system
**When** the invariant test runs
**Then** no Task ever holds a Status absent from its Project's effective set — before, during, or after any operation
**And** removal is therefore always a migration, making the invalid state unreachable by construction rather than by validation

**Given** a removal in flight across many Tasks
**When** it runs
**Then** its scope is named, focus moves to the progress region, and no percentage is shown because the operation is atomic and a percentage would be untrue

### Story 6.3: Define the Space default Statuses, and cascade a rename

As an Owner or Admin,
I want to change the Statuses every Project starts from, and to decide what happens where a Project has diverged,
So that a Space-wide change never silently overwrites a deliberate local one.

**Acceptance Criteria:**

**Given** a Space
**When** an Owner or Admin adds a Status to the defaults
**Then** it is added to every Project that has not removed it
**And** Members and Viewers cannot change the default set

**Given** a Space default Status
**When** it is renamed
**Then** the rename reaches every Project that has not itself renamed that Status, including Projects that have merely reordered it

**Given** one or more Projects that have themselves renamed the same Status
**When** the Space-level rename runs
**Then** the operation **names every conflicting Project and its current name for that Status**
**And** it offers **one** cascade decision applied to all of them at once, consistent with the single Space-wide mapping decision in the removal half

**Given** that cascade offer
**When** it is accepted
**Then** those Projects' names are replaced; when declined, they are preserved
**And** either way the rename applies to the non-conflicting Projects

**Given** a newly created Space
**When** its default set is inspected
**Then** it is non-empty, seeded as Todo / In Progress / Done
**And** removing its last Status is refused

**Given** the Space default set
**When** it is reordered
**Then** every Project that has not itself reordered that Status follows

**Given** the Space-level Status set
**When** it is edited by any route
**Then** the effective set of every Project remains derivable on read and is never materialised into a table that could go stale against this change

### Story 6.4: Remove a Space default Status across every Project

As an Owner or Admin,
I want to retire a Status across a whole Space and be asked about every Project that cannot take my chosen destination,
So that no Task is silently placed somewhere I did not choose.

**Acceptance Criteria:**

**Given** a Space default Status
**When** its removal is requested
**Then** the operation asks for **one** Space-wide destination Status

**Given** that destination
**When** the operation evaluates every Project
**Then** it names each Project whose post-removal effective set cannot accept it, **with how many of its Tasks are affected**
**And** it requires a destination drawn from that Project's own post-removal effective set

**Given** a reported Project
**When** no destination has been supplied for it
**Then** nothing applies at all — there is no fallback and no silent placement

**Given** every reported Project has a destination
**When** the operation commits
**Then** the Space-level removal, the Space-wide mapping and every per-Project exception apply as **one transaction or not at all**, across up to 50 Projects of 5,000 Tasks each

**Given** Projects that had already removed that Status themselves
**When** the Space-level removal runs
**Then** they are unaffected

**Given** this operation
**When** it is compared with the rename half
**Then** both report and ask, and neither decides for the Admin
**And** the reason is recorded: the original defect was that the rename half asked while the removal half guessed, letting affected Tasks fall to a Project's first Status

**Given** a Project's effective set can never be empty
**When** any Space-level removal runs
**Then** a valid destination exists in every affected Project, so the operation is always satisfiable

**Given** the migration in flight
**When** it runs
**Then** its scope is named, focus moves to the progress region, and no percentage is shown
**And** a refusal states that nothing was applied **and which Project blocked it**

### Story 6.5: Move a Task to another Project

As someone reorganising work,
I want to move a Task into a different Project in the same Space,
So that work can be re-filed without being retyped.

**Acceptance Criteria:**

**Given** a Task
**When** an Owner, Admin or Member moves it to another Project
**Then** only Projects in that Task's own Space are offered as destinations, and only those are accepted, including via the API
**And** a Viewer cannot move Tasks

**Given** the Task's Status exists in the destination Project's effective set
**When** the move runs
**Then** the Status is preserved and no mapping is required

**Given** the Task's Status is absent from the destination's effective set
**When** the move is attempted
**Then** a destination Status must be supplied as part of the move, and the move does not take effect without one

**Given** the move
**When** it commits
**Then** reparenting and any required Status migration are one transaction
**And** no endpoint accepts the move without the mapping it requires

**Given** the move completes
**When** the Task is inspected
**Then** its Assignee, Labels, due date and description survive unchanged
**And** the reason is stated: both Projects share a Space and therefore share its Memberships and its Labels

**Given** every route on both surfaces
**When** they are enumerated
**Then** none moves a Task to another Space

**Given** a Task carrying an active collaborative editing session
**When** it is moved
**Then** the session continues across the move and no participant is disconnected
**And** this holds **by construction** rather than by implementation: the Task id is unchanged by a reparent and the sync lease is keyed on `(Account, Space)`, neither of which a within-Space move alters — so nothing here must be built for it, but nothing here may break it either
**And** the requirement is *verified* in Epic 7 once editing sessions exist; this story asserts only that the move touches neither the Task id nor the Space

### Story 6.6: Move a whole Status of Tasks at once

As someone retiring a Project,
I want to move everything in one Status to another Project in a single operation,
So that emptying a Project safely takes one action per Status rather than one per Task.

**Acceptance Criteria:**

**Given** a Board column, or a List View filtered to one Status
**When** a bulk move is initiated
**Then** every Task currently in that Status moves to a chosen Project in the same Space
**And** the operation is available on the API on the same terms as the single-Task form

**Given** the selection shares a Status by construction
**When** the operation runs
**Then** it carries exactly **one** mapping decision on the same terms as the single-Task form — Status preserved where the destination exposes it, one destination Status required where it does not

**Given** a bulk move
**When** it runs
**Then** it is atomic: every selected Task moves, or none does
**And** a move that cannot complete is refused rather than partially applied

**Given** a refusal
**When** it happens
**Then** it is visible rather than silent, and it states that nothing moved

**Given** the bulk-move bar
**When** the operation is initiated
**Then** it appears bordered in accent — the only accent-bordered component, because it is the only transient in-flight operation and must be unmistakable while it runs — names its own scope ("Moving 4,812 Tasks."), carries `role="status"`, holds the only cancel affordance before commit, and receives focus on appearance

**Given** the operation in flight
**When** it blocks interaction
**Then** it blocks only the affected columns, implemented so that blocking never destroys the focused node
**And** it shows no percentage, because the operation is atomic and there is no partial progress to report

**Given** commit or cancel
**When** focus settles
**Then** it moves to the destination column on commit, and to the originating column on cancel

**Given** a Viewer
**When** either form is sought
**Then** neither is available

**Given** a selection spanning more than one Status
**When** it is attempted
**Then** it is not offered — the bulk form is scoped to one Status at a time
**And** the reason is recorded: a per-Status mapping table was considered and rejected because it opens a transaction-size question at the 5,000-Task bound that no stated requirement answers

**Given** Tasks with active collaborative editing sessions
**When** a bulk move commits
**Then** those sessions continue across it, on the same by-construction basis as the single-Task form, and verified in Epic 7

**Given** this whole capability
**When** its purpose is questioned
**Then** it is the safe path for retiring a Project without losing its work — one move per Status rather than one per Task, against a Project deletion that is irreversible
**And** nothing prevents deleting a Project with Tasks still in it: the safe path is a choice the interface makes attractive, not a guardrail the product enforces

## Epic 7: Write the same Task at the same time — and lose access the moment access ends

Two or more Users can edit the same Task description simultaneously, see each other present, and reconnect after a drop without losing or duplicating a word — with no merge dialog, lock or stale-content warning anywhere. And a User demoted or removed mid-sentence has their editing stop within a second, without touching anything, with their unsynchronised text never reaching the Space. UJ-5 and UJ-6. FR-34 is the criterion the PRD says to judge the product on, and SM-2 gates release here.

**FRs covered:** FR-31, FR-32, FR-33, FR-34

### Story 7.1: The merge port and its conformance suite

As the team about to build collaborative editing,
I want one merge interface whose contract is an executable suite written before any implementation,
So that two stories cannot choose different merge semantics and whole-field last-writer-wins cannot enter by the back door.

**Acceptance Criteria:**

**Given** the solution
**When** merge types are enumerated
**Then** exactly one interface `ITextMergeStrategy` exists, with exactly one registered implementation

**Given** domain, application and sync code
**When** it is inspected
**Then** none of it references a concrete merge type

**Given** the conformance suite
**When** it is authored
**Then** it encodes FR-31's convergence, FR-33's reconnection and NFR-4's bounds
**And** it is written **before** any implementation and passes **before** any implementation merges

**Given** the suite
**When** convergence is asserted
**Then** all participants observe identical text within 2 seconds of the last edit by any of them
**And** convergence holds for at least 10 simultaneous editors on one Task description

**Given** the suite
**When** reconnection is asserted
**Then** a participant disconnected for up to 5 minutes reconciles without loss or duplication

**Given** a candidate implementation that is whole-field last-writer-wins
**When** it is run against the suite
**Then** it cannot pass
**And** adopting it would be a PRD amendment to FR-31 and FR-33 rather than an architecture decision

**Given** the selected implementation
**When** it is placed
**Then** it lives in `Yello.Merge` as one source compiled to WASM for the client and native for the server, so both sides run the same merge

**Given** the architecture's open deferral on the algorithm itself
**When** this story completes
**Then** the choice is recorded rather than left implicit, and any candidate passing the suite is admissible

### Story 7.2: A Task has a description, stored as an append-only log

As someone describing a piece of work,
I want a description that can hold real detail,
So that a Task carries more than a title.

**Acceptance Criteria:**

**Given** a Task
**When** its description is written
**Then** the change is appended as an immutable `TaskDescriptionChange` row, and existing rows are never mutated

**Given** the change log
**When** the plain-text projection on `Task` is produced
**Then** it is recomputed by the projector **inside the same transaction that appends the change**
**And** a read after an admitted write is therefore never stale, and there is no second writer to race with

**Given** the projection
**When** it is read
**Then** it is the only representation read by the REST API and the List View, and nothing writes it except the projector

**Given** the change rows
**When** the schema test runs
**Then** each carries a non-nullable `SpaceId` directly, denormalised deliberately so the row-level security predicate never needs a join

**Given** the description editor
**When** it renders
**Then** it sits on the page ground rather than the card ground, so the writing surface reads as recessed
**And** there is no save button, no merge prompt, no lock and no stale-content warning

**Given** a Viewer
**When** Task detail opens
**Then** the description editor is **absent entirely** rather than read-only
**And** where it is replaced by rendered text, the replacement retains the editor's labelled region and heading, because losing the "Description" label comes free with a naive swap and nobody notices it in review

**Given** compaction
**When** a prefix of the log is replaced with a snapshot row
**Then** existing rows are not mutated
**And** per-author change counts and timestamps are preserved, because SM-5 becomes underivable and unrecoverably so otherwise

**Given** the client
**When** it sends changes
**Then** it batches frames rather than sending one per keystroke, which is what keeps in-transaction projection affordable

### Story 7.3: The sync channel that carries no authority

As the operator,
I want the real-time channel to authorise every frame rather than authorising once at connect,
So that a permission change can take effect on an open session at all.

**Acceptance Criteria:**

**Given** a WebSocket connection at `/sync`
**When** it is established
**Then** it grants nothing by itself, and holds an authorisation lease carrying `(AccountId, SpaceId, Role)`

**Given** a lease
**When** its lifetime is inspected
**Then** it has no TTL and no periodic revalidation, being held until invalidated by push
**And** the reason is stated: a timer-expired lease would require a database read per connection per interval, which the no-timer rule forbids and the free-tier allowance cannot afford

**Given** any inbound frame
**When** it arrives
**Then** it is checked against a valid lease **before** it is applied, persisted or broadcast

**Given** a frame arriving on an invalidated lease
**When** it is handled
**Then** it is **discarded — not queued and not persisted**
**And** the connection is closed with an access-ended reason

**Given** a process restart
**When** connections are inspected
**Then** leases did not survive it, and connections re-establish and re-authorise

**Given** the sync service
**When** it is deployed
**Then** it runs at most one replica, in-memory document state is a cache only, every admitted change is durable in the log before it is broadcast, and a replica restart mid-session loses no admitted change

**Given** the design
**When** it is reviewed
**Then** it requires no shared in-memory backplane and no sticky per-document routing, so horizontal scaling stays possible without being built

**Given** a Task already carrying 10 concurrent editing leases
**When** an 11th lease is sought
**Then** it is refused from the Story 1.6 bound registry with a machine-readable reason, and the User is told the Task is at its editor limit
**And** the refusal is visible rather than the connection silently degrading, because NFR-8 requires a bound to produce a refusal and never a wrong answer — and a silently-admitted 11th editor is exactly a wrong answer, since NFR-4 guarantees convergence only to 10
**And** read access is unaffected: the Task remains viewable and its Presence remains visible; only the editing lease is refused

**Given** an idle connection
**When** it approaches the 240-second ingress timeout
**Then** an application-level heartbeat every 30 seconds keeps it alive
**And** frames are versioned alongside the API rather than independently

### Story 7.4: Edit a description at the same time as someone else

As two people working on the same Task,
I want both our edits to survive without either of us being asked to resolve anything,
So that concurrent editing is the normal case rather than an error to be reported.

**Acceptance Criteria:**

**Given** two Users editing different parts of the same description simultaneously
**When** both stop
**Then** both sets of changes are retained and neither is overwritten

**Given** two Users editing the same region simultaneously
**When** both stop
**Then** both arrive at an identical final text, and that text is the one persisted

**Given** normal concurrent editing
**When** it happens
**Then** no participant is shown a merge prompt, an edit lock, or a stale-content warning at any point

**Given** a local edit
**When** it is typed
**Then** it renders locally within **16 milliseconds** — one frame at 60 Hz — without waiting on any network round trip

**Given** a remote participant's edit
**When** it arrives
**Then** it renders within **300 milliseconds at the 95th percentile** on a connection with 50 milliseconds round-trip latency
**And** it arrives without animation — it appears, it does not fly in

**Given** the server
**When** a change arrives
**Then** it never accepts whole text from a client as truth but admits or rejects each change
**And** a rejected change is reverted in the client replica, the client never being the arbiter of what is in the Space

**Given** a Viewer
**When** they attempt to enter an editing session
**Then** they cannot, including via the API

**Given** a Task carrying an active editing session
**When** it is deleted
**Then** the session terminates and participants are told it was deleted, rather than losing their connection silently

### Story 7.5: See who else is here

As someone editing a Task,
I want to know who else is on it,
So that I understand why the text is moving.

**Acceptance Criteria:**

**Given** a participant arriving at a Task
**When** they arrive
**Then** Presence appears within **2 seconds**; and when they leave, it disappears within **10 seconds**, without their taking any action

**Given** Presence
**When** it renders
**Then** it is a dot **plus** a text count, always — never colour or position alone

**Given** Presence
**When** it is populated
**Then** it shows only Memberships of the same Space, and never reveals an Account's activity in any other Space

**Given** Presence
**When** it identifies a participant
**Then** it uses a display name and initials only, and never an email address, including inside announcements

**Given** Presence on Task cards
**When** the Board renders
**Then** it renders visually on cards without routing card-level churn to a live region
**And** the reason is stated: card-level announcement scales with the Board rather than with the 10-editor bound

**Given** Presence for the Task the User has open
**When** it changes
**Then** the **settled count** is announced politely, debounced to roughly 5 seconds, rather than each transition being announced
**And** the reason is stated: `polite` queues rather than coalescing, so a naive one-announcement-per-event region outlives its own events at NFR-8's bounds

**Given** the User is typing in the editor
**When** Presence changes
**Then** the announcement is suppressed entirely and remains available visually, because a collaborator arriving is not worth interrupting a sentence

**Given** the Presence count string
**When** its typography is set
**Then** it is in the sans stack rather than the mono stack
**And** the reason is stated: it is prose rather than a system fact, and it is the mandated non-colour carrier of Presence, so it must not inherit mono's unpredictable non-Latin fallback or its smaller effective x-height

### Story 7.6: Reconcile after a disconnection

As someone whose connection drops mid-sentence,
I want my words to arrive once when I come back,
So that a network fault costs me nothing.

**Acceptance Criteria:**

**Given** a User editing when connectivity is lost
**When** the loss is detected
**Then** they are told "Disconnected. Your changes are not yet sent." and editing continues against the local replica

**Given** that message
**When** it renders
**Then** it is not a modal and does not block typing
**And** the copy avoids "held" and "saved", both of which would promise application

**Given** changes made while disconnected
**When** connectivity returns
**Then** they are applied and appear **exactly once**, and changes made by others during the disconnection are present

**Given** a disconnection of up to 5 minutes
**When** reconnection completes
**Then** reconciliation succeeds without loss or duplication

**Given** reconciliation cannot complete
**When** that is determined
**Then** the User is told explicitly, the unsynchronised text stays visible and copyable, and it is never silently discarded
**And** it never auto-retries silently forever

**Given** a durable local buffer used for the reconnection window
**When** it exists
**Then** it is scoped to one Space, keyed to the Session, held outside `localStorage`, `sessionStorage` and IndexedDB, and destroyed by the same triggers that purge Space-scoped state

**Given** a non-editor surface such as the Board or the List View
**When** connectivity is lost
**Then** it states that updates have stopped and what is consequently stale
**And** it never silently presents a frozen Board as current, because the product degrades honestly rather than pretending

**Given** other Users' edits arriving on the Board
**When** they land
**Then** no per-Task announcement fires — a debounced summary ("3 Tasks changed") and a manual refresh affordance are used instead
**And** the reason is stated: at 5,000 Tasks and 50 sessions a per-Task announcement is a denial of service, while announcing nothing leaves the buffer silently stale

### Story 7.7: Access ends while the door is still open

As someone whose access is removed or reduced while I am mid-edit,
I want to be told immediately and to keep what I typed,
So that revocation is honest rather than silent, and nothing I wrote after it reaches a Space I have left.

**Acceptance Criteria:**

**Given** a participant with an open editing session
**When** their Membership is removed
**Then** `MembershipChanged` publishes at the transaction boundary, the lease is invalidated, and within **1 second** the editor becomes `readonly` — without the participant taking any action

**Given** the lease invalidation
**When** the client handles it
**Then** it **purges before it announces** — discarding every queued **inbound** frame for that Space and clearing both live regions first, and only then announcing
**And** the reason is stated: the reverse order renders a queued Presence or remote-edit frame one tick *after* "Access ended.", disclosing who was present in and what was edited in a Space the Account no longer belongs to

**Given** unsynchronised local text
**When** access ends
**Then** it is **not applied** and never reaches the Space by any route, including a delayed or retried frame
**And** it **stays visible, focusable and selectable** so it can be copied, with the banner stating it was not saved — it is the User's own typing on their own screen, so showing it discloses nothing and wiping it would be gratuitous

**Given** the revoked editor
**When** the attribute is inspected
**Then** it is `readonly` and not `inert` or `disabled`
**And** the reason is stated: both of those remove the retained text from the accessibility tree and leave "selectable" true only by pointer, defeating the reason it is retained

**Given** demotion to Viewer rather than removal
**When** it lands
**Then** editing capability ends while read access continues uninterrupted, the removal is **narrated before the surface settles**, the editor is replaced by rendered text keeping its labelled region, and only then does the write affordance become absent

**Given** a participant removed **while disconnected**
**When** they reconnect
**Then** their frames are discarded rather than queued, and the state resolves to "Access ended." rather than to a reconciliation failure
**And** the reason is stated: the text was never admissible rather than lost to a fault, and no sync-succeeded state is ever shown first and then revoked

**Given** changes already synchronised before the change took effect
**When** the Space is inspected
**Then** they are retained — revocation stops future writes and does not roll back past ones

**Given** the FR-34 removal
**When** focus is placed
**Then** it moves to the "Access ended." banner, made programmatically focusable with `tabindex="-1"` and `role="alert"`, persisting until dismissed and staying in the reading order with the retained text as the next stop
**And** the reason is stated: a live-region utterance fired during the DOM mutation this event causes is frequently never spoken at all, so the persistent focusable banner is the real carrier

**Given** a Task deleted while open
**When** it happens
**Then** participants are told it was deleted, unsent text is retained in a panel that receives focus and is announced and is not dismissible by a stray keypress, and on dismissal focus goes to the **column** that held the Task
**And** it goes there rather than to the adjacent Task, whose index has just shifted, or to the originating card, which no longer exists

**Given** permission-change announcements
**When** they are delivered
**Then** they are `assertive`, never throttled and never coalesced, there being at most one per Account per Space
**And** an assertive interrupt flushing queued polite announcements is accepted — a dropped "3 editing" costs nothing and is not a bug

### Story 7.8: Prove revocation on a live session

As the operator deciding whether Yello can ship,
I want the revocation suite to assert both timing clauses against sessions holding unsynchronised edits,
So that the requirement the product is judged on is measured rather than believed.

**Acceptance Criteria:**

**Given** the revocation suite
**When** it runs
**Then** it asserts the request-path clause — a Role change or Membership removal governs the affected Account's **very next request**, with no tolerance — on the browser and the API alike

**Given** the live-session clause
**When** it is asserted
**Then** the effect lands within **1 second** of the transaction boundary, without the affected Account acting

**Given** a session holding unsynchronised local edits
**When** permission is revoked
**Then** the case is in the suite explicitly, and the unsynchronised text is asserted never to reach the Space

**Given** the suite
**When** it is assessed
**Then** it passes in 100% of tested cases, this being a release gate rather than a metric

**Given** a poller or a cross-replica hop
**When** either is introduced
**Then** the suite fails, because both timings are worded precisely so that it would

**Given** a cache outliving a request
**When** it is introduced anywhere on the authorisation path
**Then** the request-path clause fails, because a delay of even one request means a cache was added — which is the failure the clause exists to catch

**Given** an API Token
**When** its Account's Role changes
**Then** the Token's capability narrows on the very next request without anyone reissuing it
**And** this case is in the suite, because a Token that outlived the permission justifying it is the same defect on the second surface

## Epic 8: Drive Yello from a script

A consumer can read and write Projects and Tasks in exactly one Space from outside the browser, against a published contract that will not break underneath them: versioned by path, deprecated with notice, rate limited per Token with a refusal they can branch on. UJ-7. This epic does not build the API — every endpoint accrued to both surfaces in the epic that built its slice, because no slice branches on calling surface. What remains here is the contract and the audit.

**FRs covered:** FR-35, FR-37, FR-38

### Story 8.1: Read and write Projects and Tasks over the API

As someone driving Yello from a script,
I want the API to do what the browser does under the same rules,
So that automation is a first-class surface rather than a side door.

**Acceptance Criteria:**

**Given** an API Token bound to one Space
**When** a caller reads or writes Projects and Tasks in that Space
**Then** it succeeds subject to the issuing Account's **current** Role in that Space

**Given** every capability in the Role matrix
**When** it is exercised on the API
**Then** it is enforced identically to the browser
**And** no operation refused in the browser succeeds via the API

**Given** every case in the isolation suite
**When** the parity sweep runs across all seven earlier epics
**Then** every case executes against both surfaces, and a case existing for one and not the other is reported as a gap rather than accepted as a choice

**Given** the API surface
**When** it is enumerated
**Then** it exposes no operation that enumerates Spaces, Accounts or Memberships beyond the Token's Space

**Given** a Task's position within its Status
**When** it is read over the API
**Then** it is readable, so a consumer can reproduce what a User sees
**And** it is **not** writable — the single place the API is deliberately narrower than the browser, stated here so the gap is a decision rather than an oversight, and keeping the convergence requirement confined to one surface

**Given** a Token-authenticated request and an equivalent browser request
**When** both are traced
**Then** they differ only in how `AccountId` and `SpaceId` are established, and thereafter traverse the identical pipeline and the identical slice
**And** no slice branches on calling surface

**Given** every refusal on the API
**When** its body is read
**Then** it carries a machine-readable reason a client can branch on, and no client needs to parse prose

### Story 8.2: Version the API and deprecate predictably

As someone whose script depends on Yello,
I want the shape I wrote against to keep working,
So that a Yello release is never an outage for me.

**Acceptance Criteria:**

**Given** a request naming a supported version
**When** it is served
**Then** it receives that version's response shape, regardless of what other versions exist

**Given** the routes
**When** they are inspected
**Then** the version is the first path segment — `/api/v1/…`, then `/api/v2/…` — with `spaceId` the first segment after it

**Given** a change within a live version
**When** it removes a field, renames a field, changes a field's type, or narrows accepted input
**Then** the snapshot contract test fails the build

**Given** a change within a live version
**When** it **adds** a field
**Then** it is permitted, because the guarantee is against removal, rename, retyping and narrowing rather than against growth

**Given** the versions served
**When** they are counted
**Then** at most two are served concurrently

**Given** a version to be withdrawn
**When** the plan is made
**Then** deprecation is announced before it stops working, and the version keeps serving requests throughout the announced period

**Given** sync frames
**When** their shape changes
**Then** they are versioned alongside the API rather than independently

### Story 8.3: Rate limit per Token without duplicating writes

As the operator,
I want the API bounded per Token,
So that one consumer cannot exhaust the deployment and a retry never applies a write twice.

**Acceptance Criteria:**

**Given** a Token exceeding its rate
**When** the next request arrives
**Then** a distinct, documented refusal is returned that a client can detect and act on, stating when the caller may retry via `Retry-After`

**Given** rate limiting
**When** it is partitioned
**Then** it is partitioned per Token, so one Space's consumption cannot exhaust another's

**Given** a rate-limited client that retries
**When** the retry arrives
**Then** no write is applied more than once — rate limiting never causes a duplicate write in a system that explicitly invites retries

**Given** every state-changing endpoint
**When** it is called
**Then** it accepts an `Idempotency-Key`, and a replayed key returns the original response without re-applying the effect

**Given** a write that timed out
**When** it is retried
**Then** it is not applied twice

**Given** rate-limit refusals
**When** they occur
**Then** they are one of the two operational signals alerted on, the other being free-tier exhaustion

**Given** the browser
**When** rate limiting is considered
**Then** there is no browser surface for it — this is a machine-readable refusal on the API only
