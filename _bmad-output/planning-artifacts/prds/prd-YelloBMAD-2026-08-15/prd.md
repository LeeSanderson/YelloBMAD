---
title: Yello
status: final
created: 2026-08-15
updated: 2026-08-20
---

# PRD: Yello

## 0. Document Purpose

This PRD defines Yello for the people who will design, architect and build it. It is the source of truth for *what* Yello does and *why*; it deliberately does not specify *how*. Technology choices, data models, transport mechanisms and rejected alternatives live in `addendum.md` alongside this document.

Read it in this order. §1–§3 establish what Yello is and the vocabulary the rest of the document uses without deviation. §4 is the substance: features grouped by capability, with functional requirements nested underneath and numbered globally (FR-1 … FR-43) so downstream artifacts can reference them stably even if features get reorganised — the numbers are identifiers, not positions. §5 sets the system-wide quality bar and §6 the constraints Yello accepts. §7 names the surfaces. §8 and §9 draw the boundary — what Yello will never be, and what v1 leaves out. §10 says how we would know it worked. §11 records the questions that were open and how each was resolved; §12 collects what remains assumed.

Two conventions matter. Terms defined in §2 Glossary are used verbatim everywhere else — where you see **Space**, no other document should say "workspace", "tenant" or "org". And every inference made without confirmation carries an inline `[ASSUMPTION]` tag, indexed in §12, so nothing quietly hardens into fact.

An architecture spine now exists at `planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md` and is binding on mechanism; where it has settled something this document left open, it is authoritative. No UX specification exists yet. This PRD's depth is calibrated to what the downstream phases need in order to proceed rather than to the size of Yello's audience.

**Revision note.** This document was revised on 2026-08-18 after a `bmad-spec` pass distilled it and surfaced defects: an internal contradiction between UJ-4 and FR-15, an ownership-transfer trap, a revocation budget that no implementation of the chosen architecture could violate, a collision between the Board and the scale envelope, and an asymmetry inside FR-27. All six of §11's open questions are now resolved. The audit trail of how each was decided is `specs/spec-yello/.memlog.md`.

**Revised again on 2026-08-20**, after a `bmad-ux` pass produced `DESIGN.md` and `EXPERIENCE.md` and found one defect this document had carried since the ownership rework: the Ownership Offer had an expiry and no notification, so its 7-day clock could run out on a recipient who was never told. FR-8's assumption lost its not-emailed half and **FR-43** was added. Nothing else changed. The audit trail is `planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/.memlog.md`.

## 1. Vision

Most task tools make you choose a container before you have anything to put in it. A personal to-do app assumes you work alone. A team workspace assumes an organisation exists, with a billing entity and an admin who provisions accounts. A client portal assumes an outside party who must be walled off from everything else. Pick one and the others become awkward — a freelancer ends up running three tools, or bending one of them into a shape it resists.

Yello's bet is that all three are the same primitive. A **Space** is a container for work and the boundary of who can see it, and nothing more is required of it. Your private notes are a Space with one member. A client engagement is a Space where the client is a Viewer. Your company is a Space with fifty Members. Yello does not ask which kind you meant, because it does not need to know — the same rules govern all of them.

What follows from that is the part that actually matters: **the interesting user is the one who lives in several Spaces at once, with a different standing in each.** Ravi is the Owner of his personal Space, an Admin on a client engagement, and a Viewer on a former employer's Space he still has access to. Yello treats this as the normal case rather than an edge case, which means identity is global and permission is contextual — every question about what someone may do is answered against a specific Space, never against the person. That constraint shapes the whole product, and it is what makes Yello different from a task tool with sharing bolted on.

## 2. Glossary

*These terms are used verbatim throughout this document and all downstream artifacts. Synonyms are a discipline violation. If a new domain noun appears in §4, it is added here in the same pass.*

- **Account** — A registered identity in Yello, unique by email address. Global: one Account exists across all Spaces. An Account is never owned by a Space. *Uniqueness by email address is correct for v1 but is not permanent: OAuth sign-in (§9.2) breaks it, since a provider may return a different address than the one on file, or none. Do not build on email-as-identity in a way that cannot be revisited.*
- **User** — An Account acting in the context of a specific Space. "User" is always relative to a Space; where the Space is not established, the correct term is Account.
- **Space** — The unit of both work containment and access control. Contains Projects. Has exactly one Owner and zero or more other Memberships. An Account may belong to unlimited Spaces. Nothing is visible across a Space boundary.
- **Personal Space** — Descriptive only, not a distinct type. The Space created automatically when an Account registers. It is an ordinary Space in every respect: shareable, renameable, deletable, transferable.
- **Membership** — The relationship between one Account and one Space, carrying exactly one Role. An Account has at most one Membership per Space. Membership is the only mechanism by which an Account gains access to a Space.
- **Role** — The permission level of a Membership. Exactly one of **Owner**, **Admin**, **Member**, **Viewer**. Roles are per-Space; an Account may hold a different Role in every Space it belongs to.
- **Owner** — The single highest Role in a Space. Exactly one per Space at all times. Transferable to another Membership; not removable while it holds ownership.
- **Admin** — Manages Membership and Space settings. May issue and revoke Invitations. May not transfer ownership or delete the Space.
- **Member** — Creates and edits Projects and Tasks. May not manage Membership.
- **Viewer** — Reads Projects and Tasks. Creates and edits nothing.
- **Invitation** — A pending offer of Membership in one Space at one Role, addressed to an email address. Issued by an Owner or Admin. Becomes a Membership when accepted. Revocable before acceptance.
- **Ownership Offer** — A pending offer to become the Owner of one Space, addressed to an existing Membership of that Space. Issued by that Space's Owner, who remains Owner until it is accepted. At most one is pending per Space. Revocable before acceptance, declinable by the recipient, and it lapses if the recipient's Membership ends.
- **Project** — A named collection of Tasks within one Space. Belongs to exactly one Space and never moves between Spaces.
- **Task** — The unit of work. Belongs to exactly one Project at any moment, and may be moved between Projects within the same Space (FR-41). Carries a title, description, Status, optional Assignee, optional due date and zero or more Labels.
- **Status** — The workflow position of a Task, drawn from the effective Status set of the Task's Project: the Space defaults with that Project's delta applied (§4.7). Determines the Board column a Task appears in.
- **Assignee** — The Membership a Task is allocated to. Must be a Membership of the same Space as the Task.
- **Label** — A named tag applied to Tasks for filtering. Defined per Space, applied per Task, many-to-many.
- **Board** — A view of one Project's Tasks arranged in columns by Status, orderable within a column.
- **List View** — A view of one Project's Tasks as rows, filterable and sortable by Task attributes.
- **Presence** — The live indication that other Users are viewing or editing the same Task.
- **API Token** — A credential authenticating API requests as one Account within exactly one Space, at that Account's Role in that Space. Never grants access beyond the Space it was issued for.
- **Session** — An authenticated browser context for one Account. Spans all Spaces the Account belongs to; carries no permission of its own.

## 3. Target User

### 3.1 Jobs To Be Done

- **Keep private work private without running a second tool.** I want somewhere to put my own tasks that is genuinely mine, in the same place I do collaborative work.
- **Bring someone into one piece of my work without exposing the rest.** Inviting a collaborator to a client project must not require trusting them with anything else I do.
- **Move between contexts without losing my place.** I work across several engagements in a day; switching should be a single action, and it should be obvious which context I am in.
- **Let outsiders see progress without letting them change it.** A client asking "where are we?" should be answerable by giving them access, not by writing a status email.
- **Know that access ended when access ended.** When I remove someone, I need to believe it took effect — including for whatever they had open at the time.
- **Automate the parts I do twice.** I want to create and update work from scripts and other tools I already run, not only through a browser.

### 3.2 Non-Users (v1)

*Who Yello is not for. What it will not do is §8's job; this section is about the audience boundary.*

- **Enterprises requiring centralised provisioning.** Nobody in Yello can create, disable or reach into another person's Account, so an organisation wanting IT-governed access to its people's work cannot get it here.
- **Regulated environments with data residency or retention mandates.** No residency controls, no legal hold, no compliance attestations.
- **Teams needing formal project management.** Yello records what work exists and who holds it, not how long it takes or what it costs.
- **Anyone needing offline-first operation.** Yello tolerates a brief disconnection mid-edit (FR-33) and otherwise assumes connectivity.

### 3.3 Key User Journeys

*Named-persona narratives the product enables, numbered UJ-1 … UJ-8. FRs reference these inline. If a UX document is produced later it should mirror these IDs.*

- **UJ-1. Ravi has somewhere to put a Task ninety seconds after signing up.**
  Ravi, a freelance developer, signs up because he is tired of tasks living in three chat threads. He registers with email and password and lands directly in a Space already named "Ravi's Space" — he did not create it and was not asked to. He creates a Project called "Admin", adds a Task, and closes the tab. **Climax:** value landed before he made a single structural decision; he never saw an empty state asking him to configure something. **Resolution:** one Space, one Project, one Task, and no notion that Yello is collaborative at all yet. **Edge case:** if he abandons registration after the email is taken but before the password is set, retrying with the same address must not reveal whether that Account exists.

- **UJ-2. Ravi opens a client engagement without exposing anything else.**
  Three weeks later Ravi wins work with a design studio. He creates a second Space, "Northwind Redesign", and invites the studio's producer Nadia by email as a Member, and Beatriz — Northwind's marketing lead, who is paying for the work and wants to watch it land — as a Viewer. Neither has a Yello Account. **Path:** create Space → invite by email address → assign Role at invitation time → send. **Climax:** Ravi's personal Space is not mentioned anywhere in what either invitee receives or sees; the invitation is scoped to one Space and carries one Role. **Resolution:** Ravi is Owner of two Spaces with different Memberships. **Edge case:** if he invites an address that already has a Yello Account, they join their existing Account rather than creating a second one, and their other Spaces remain invisible to Ravi.

- **UJ-3. Nadia accepts an invitation and sees exactly one thing.**
  Nadia, the studio's producer, gets an email and clicks through. She has no Yello Account, so she registers — and in doing so a personal Space is created for her too, which she ignores. **Path:** open invitation → see the Space and the Role offered → register, which is her deliberate act of acceptance → land in "Northwind Redesign". **Climax:** she can see the Projects and Tasks in that Space and nothing else in Yello. There is no directory of other Spaces, no search that reaches beyond her Membership, no sign that Ravi has other clients. **Resolution:** Nadia is a Member of one Space and Owner of a personal Space she may never use. **Edge cases:** if her invitation was revoked before she accepted it, the link tells her it is no longer valid without disclosing who revoked it or what the Space contains. And if the link is fetched by a mail security scanner, prefetched by her browser, or forwarded to a colleague, no Membership is created — the link presents the offer and nothing more (FR-11).

- **UJ-4. Ravi switches context three times before lunch.**
  Ravi is Owner of "Ravi's Space", Admin of "Northwind Redesign", and Viewer on a Space belonging to a company he contracts for. In one morning he moves between all three. **Path:** Space switcher → pick Space → the entire working surface changes. **Climax:** in the third Space every affordance to create or edit is absent — not present-and-failing, absent — so he can tell his standing from the interface without attempting an action. **Resolution:** he is never in doubt about which Space he is operating in or what he may do there. **Edge case:** if he opens a deep link to a Task in a Space he has since been removed from, he is told only that the resource is not available to him — naming neither "it does not exist" nor "you lost access" as the real reason, because FR-15 requires the two to be indistinguishable — and he is returned to a Space he does belong to. *An earlier draft of this edge case said he is told he no longer has access; that contradicted FR-15 and NFR-1 and has been corrected. The usability cost is paid with deliberately ambiguous copy rather than with a disclosure.*

- **UJ-5. Nadia and Ravi write the same Task description at the same time.**
  Nadia is fleshing out the acceptance criteria on a Task while Ravi, on a call, is adding a constraint to the same description. Both see the other's presence and both see the text evolve. **Path:** open Task → begin typing → observe the other participant → continue. **Climax:** neither one's work is discarded and both end at the same text; no one is shown a merge dialog or a "someone else has changed this" warning. **Resolution:** the Task description reflects both contributions and shows who contributed. **Edge case:** Ravi's connection drops for forty seconds mid-sentence; when it returns his local edits are reconciled rather than lost or duplicated.

- **UJ-6. Access ends while the door is still open.**
  The engagement ends and Ravi removes Beatriz from "Northwind Redesign". She has a Task open in another tab with an unsaved sentence in the description — she was briefly promoted to Member during the final week to log her own feedback, and nobody thought to check who was mid-edit before the removal. **Climax:** her session in that Space stops working — the editor becomes inert, her unsynchronised text is not applied, and she is told her access has ended. **Resolution:** nothing she typed after removal reaches the Space, and she retains access to nothing. **Edge case:** if she is demoted to Viewer rather than removed, the same thing happens to her editing ability while her read access continues uninterrupted.

- **UJ-7. Tomás automates the part he does twice.**
  Tomás runs a small studio and already has a deployment script. He wants a Task created in Yello whenever a release goes out. **Path:** generate an API Token scoped to one Space → call the API from the script → Task appears. **Climax:** the API Token cannot touch any Space other than the one it was issued for, including Spaces its creator owns. **Resolution:** Yello participates in a workflow that does not involve opening a browser. **Edge case:** when Yello's API changes shape, Tomás's script keeps working against the version it was written for and he is told, in advance, when that stops being true.

- **UJ-8. Ravi hands a Space over and leaves.**
  Ravi finishes the Northwind engagement and wants out cleanly, but the work must survive. He offers ownership of "Northwind Redesign" to Nadia; she accepts, and he removes himself. **Path:** offer ownership → Nadia accepts (FR-42) → Ravi, now an Admin, removes his own Membership. **Climax:** the Space continues with all its Projects and Tasks intact, Nadia is Owner *by her own agreement*, and Ravi is gone — no residual access, no orphaned Space. **Resolution:** Ravi's remaining Spaces are unaffected. **Edge case:** if Nadia declines, or lets the offer lapse, Ravi is still the Owner and still cannot leave; his remaining exits are to offer it to someone else or to delete the Space. Wanting out does not by itself get him out — the deliberate price of nobody being made an Owner against their will. **Edge case:** if Ravi instead deletes his entire Account, every Space he still owns must be resolved first — he cannot leave a Space ownerless, and other people's work cannot vanish because he left.

## 4. Features

*Each subsection is a coherent capability. Functional requirements are nested under the feature they belong to and numbered globally (FR-1 … FR-43) so downstream artifacts can reference them stably even if features are reorganised. Consequences are written to be testable; where one could pass by accident, it is written to be failable instead.*

### 4.1 Accounts and Authentication

**Description:** Yello identity is global and independent of any Space. An Account is created once, by the person themselves, and is never provisioned by anybody else — there is no administrator anywhere in Yello who can create, disable or reset another person's Account. Registration is the only path in. Authentication establishes *who* an Account is and nothing more; what that Account may do is never known until a Space is established. Realizes UJ-1, UJ-3.

**Functional Requirements:**

#### FR-1: Register an Account

A person can create an Account with an email address and a password. The email address uniquely identifies the Account across Yello. Realizes UJ-1, UJ-3.

**Consequences (testable):**
- An email address that already has an Account cannot be registered a second time.
- Attempting to register an address that already exists produces a response indistinguishable from registering a new address; the existence of an Account is never disclosed to an unauthenticated caller.
- Registration completes with exactly one Space in existence for that Account (see FR-4), and that Space's Owner is the new Account.
- An Account that has not completed registration holds no Membership and appears in no Space.

#### FR-2: Authenticate and hold a Session

An Account can authenticate and receive a Session that persists across requests and across every Space it belongs to. Realizes UJ-4.

**Consequences (testable):**
- A Session identifies an Account and carries no Role, no Space and no capability of its own.
- The same Session is used unchanged when the acting Account switches Space; switching Space never re-authenticates.
- A Session that has expired or been invalidated grants access to nothing, including to Spaces the Account still holds Membership in.
- Failed authentication does not reveal whether the failure was an unknown address or a wrong password.

#### FR-3: Delete an Account

An Account can delete itself. Deletion must not orphan a Space or destroy work belonging to other Accounts. Realizes UJ-8.

**Consequences (testable):**
- Deletion is refused while the Account is the Owner of any Space; each such Space must first be transferred away (offered under FR-8 and accepted under FR-42) or deleted (FR-7). Because a transfer requires someone else to accept, deleting the Space is the only exit the Account controls unilaterally — an Owner who can find no willing recipient can still always leave, at that cost.
- On deletion, every remaining Membership held by the Account is removed, and the Account disappears from every Space it belonged to.
- Tasks the deleted Account was Assignee of become unassigned; the Tasks themselves survive.
- Content authored by the deleted Account in Spaces it did not own is retained; attribution renders as a deleted Account rather than disappearing.
- After deletion, the email address can be used to register a new Account, and that new Account inherits no Membership, no Space and no history.

**Feature-specific NFRs:**
- Passwords are never stored recoverably, and are never included in any log, error message or API response.

### 4.2 Spaces

**Description:** A Space is the unit of both work containment and access control — the same object serves as a private notebook, a client engagement and a whole company's shared work, and Yello draws no distinction between them. Every Account gets one at registration without asking for it, so there is never an empty state to configure before work can begin. A Space has exactly one Owner at all times; this is an invariant, not a default. Realizes UJ-1, UJ-2, UJ-4, UJ-8.

**Functional Requirements:**

#### FR-4: Provision a Personal Space on registration

The system creates a Space for a newly registered Account and makes that Account its Owner. Realizes UJ-1, UJ-3.

**Consequences (testable):**
- The Space exists and is usable at the moment registration completes; the Account never encounters a state in which it belongs to no Space.
- The provisioned Space is an ordinary Space: it can be renamed, shared, transferred and deleted on the same terms as any other. No attribute distinguishes it from a Space created by FR-5.
- `[ASSUMPTION: the provisioned Space is named from the Account's display name, e.g. "Ravi's Space", and is renameable immediately.]`

#### FR-5: Create a Space

An authenticated Account can create additional Spaces without limit. Realizes UJ-2, UJ-4.

**Consequences (testable):**
- The creating Account becomes the Owner of the new Space.
- A newly created Space has exactly one Membership.
- A newly created Space carries the default Status set (FR-24) and no Projects.
- Creating a Space has no effect on, and no visibility from, any other Space.

#### FR-6: Rename a Space

An Owner or Admin can rename a Space.

**Consequences (testable):**
- Members and Viewers cannot rename a Space.
- Space names are not unique across Yello; two Accounts may each own a Space with the same name without collision or disclosure.

#### FR-7: Delete a Space

An Owner can delete a Space, destroying its Projects, Tasks, Memberships and Invitations.

**Consequences (testable):**
- Only the Owner can delete a Space. Admins cannot.
- Deletion removes every Membership in that Space; other Accounts lose access without losing their own Spaces or their Account.
- Deletion invalidates every API Token issued for that Space (FR-36).
- An Account may delete its last remaining Space. Belonging to no Space is a valid state: the Account persists, and is offered the chance to create a Space rather than having one created for it. No Space is undeletable.
- `[ASSUMPTION: deletion is immediate and irreversible in v1 — no trash, no restore window. Flagged for review: this is the most destructive operation in the product and the only one with no undo.]`

#### FR-8: Offer to transfer ownership

An Owner can offer ownership of a Space to another Membership in that Space. Ownership does not move until the offer is accepted (FR-42). Realizes UJ-8.

**Consequences (testable):**
- Ownership can only be offered to an existing Membership; it cannot be offered to an email address, an Invitation, or an Account with no Membership in that Space. Any Role may be named.
- Making the offer does not move ownership. The offering Owner remains the Owner, with every capability of the Role, until the offer is accepted.
- At most one Ownership Offer is pending per Space at a time.
- The offering Owner may revoke a pending offer, and revocation leaves every Membership and Role exactly as it was.
- A pending offer lapses if the named recipient's Membership is removed (FR-14) or their Account is deleted (FR-3), and lapsing changes no Role.
- The offering Owner's Membership still cannot be removed by FR-14, and their Account deletion is still refused by FR-3, while an offer is pending — making an offer is not itself an exit.
- The named recipient is told the offer exists (FR-43). Being a Membership of the Space is not the same as being present in it, and the offer expires.
- `[ASSUMPTION: the offer expires after 7 days, mirroring FR-39.]`

**Why the recipient is emailed.** An earlier draft of this assumption also said the offer was *"surfaced in Space settings rather than emailed — the recipient is already a Member of the Space."* That reasoning was true and insufficient. Composed with the 7-day expiry and with expiry evaluated only when something reads the offer, a recipient who simply does not open that Space for a week never learns the offer existed. UJ-8's edge case then bites the wrong person: the offer lapses, the offering Owner remains Owner, FR-14 still forbids removing their Membership and FR-3 still refuses their Account deletion — so **an Owner can be held in a Space indefinitely because somebody else never logged in**, with deleting the Space as their only unilateral exit. The asymmetry made it plainer: FR-40 emails you when you are assigned a single Task, and nothing emailed you when you were offered an entire Space.

#### FR-42: Accept or decline an Ownership Offer

The Membership an Ownership Offer names can accept it, becoming Owner, or decline it. Realizes UJ-8.

**Consequences (testable):**
- Only the single Membership the offer names can accept or decline it, and only while it is pending.
- On acceptance, ownership moves in one atomic step: the recipient becomes the sole Owner and the previous Owner becomes an Admin without losing access. At no point does the Space have zero Owners or two Owners.
- No Account becomes an Owner without having agreed to it. There is no route, the API included, by which ownership arrives unrequested.
- Declining leaves every Membership and Role exactly as it was, and the offering Owner is told it was declined.
- A declined or lapsed offer cannot afterwards be accepted; the Owner must make a new offer.
- After acceptance the new Owner is bound by every rule that binds any Owner: their Membership cannot be removed while they hold ownership (FR-14), and their Account deletion is refused until they transfer the Space onward or delete it (FR-3).

**Why this is not an immediate transfer.** The original FR-8 moved ownership unilaterally. Combined with FR-14 (an Owner's Membership cannot be removed while it holds ownership) and FR-3 (Account deletion is refused while the Account owns any Space), that let an Owner transfer a Space to any Membership, immediately remove their own now-Admin Membership, and leave that person permanently unable to delete their Yello Account — with only irreversible Space deletion or imposing the same thing on a third party as exits. One Account could unilaterally block another's erasure. Requiring acceptance closes it at the root. Rejected alternatives are recorded in `addendum.md`.

#### FR-9: Establish and switch Space context

An Account can move between the Spaces it holds Membership in; the active Space determines everything subsequently visible and permitted. Realizes UJ-4.

**Consequences (testable):**
- Only Spaces the acting Account holds a Membership in are listed. No Space is discoverable by any other means — there is no directory, no search and no enumeration that reaches beyond Membership.
- Switching Space changes the acting Role to whatever that Account holds in the destination Space, with no carry-over from the previous Space.
- Every request that reads or writes Space-scoped data resolves an active Space before authorisation is evaluated; a request with no resolvable Space context is refused rather than defaulted.

### 4.3 Membership and Invitations

**Description:** Membership is the only mechanism by which an Account gains access to a Space — there is no public Space, no link-sharing, no anonymous access. Invitations are addressed to an email address rather than to an Account, so a person can be invited before they have ever heard of Yello, and an Account is never exposed to the inviter as a side effect of being invited. Realizes UJ-2, UJ-3, UJ-6.

**Functional Requirements:**

#### FR-10: Issue an Invitation

An Owner or Admin can invite an email address to a Space at a specified Role. Realizes UJ-2.

**Consequences (testable):**
- Members and Viewers cannot issue Invitations.
- Any email address can be invited. There is no domain restriction, no allowlist, and no requirement that the address already have an Account.
- The Invitation carries exactly one Space and exactly one Role, fixed at issue time.
- An Invitation cannot be issued at Owner Role; ownership moves only by an Ownership Offer accepted under FR-42.
- Issuing an Invitation to an address that already holds Membership in that Space is refused.
- The response to the issuer is identical whether or not the address corresponds to an existing Account; issuing an Invitation never discloses whether someone uses Yello.
- The Invitation record retains its terminal state once it leaves pending — accepted, revoked or expired — rather than being deleted. No product surface reads it; §10's SM-4 and SM-C3 are underivable without it.

#### FR-11: Accept an Invitation

An invited person can accept, gaining Membership at the Role the Invitation specified. Realizes UJ-3.

**Consequences (testable):**
- Accepting creates exactly one Membership, in exactly the invited Space, at exactly the invited Role.
- The Invitation token identifies the offer; it never authorises acceptance. Acceptance requires the invitee authenticated as the Account the Invitation addresses. A bare fetch of the acceptance route — by a mail security scanner, a link prefetcher, or anyone the mail was forwarded to — creates no Membership and changes nothing.
- Acceptance is a deliberate act, taken after the Space and the Role have been shown. For an invitee with no Account, completing registration is that act; for an invitee who already has one, it is an explicit confirmation. Neither is satisfied by loading a URL.
- An invitee without an Account registers as part of accepting, and that registration provisions their own Personal Space (FR-4) independently of the Space they were invited to.
- An invitee with an existing Account joins with that Account; no second Account is created, and their other Memberships are neither visible to nor affected by the inviter.
- An Invitation can be accepted once. A second attempt with the same Invitation is refused.
- An Invitation that has been revoked (FR-12) reports only that it is no longer valid — it discloses neither the Space's name or contents, nor who revoked it.

#### FR-12: Revoke a pending Invitation

An Owner or Admin can revoke an Invitation that has not been accepted.

**Consequences (testable):**
- A revoked Invitation cannot subsequently be accepted.
- Revocation is possible only before acceptance; removing an accepted invitee is FR-14, not FR-12.
- An Invitation remains valid if its issuer later loses the capability to have issued it — by demotion, by removal, or by Account deletion. The Invitation was legitimately issued and does not depend on its issuer's continuing authority. Any remaining Owner or Admin may revoke it.

#### FR-13: Change a Membership's Role

An Owner or Admin can change the Role of a Membership within the constraints of their own Role. Realizes UJ-6.

**Consequences (testable):**
- An Admin can change Memberships between Member and Viewer only.
- Only an Owner can promote a Membership to Admin, or demote one from Admin. `[ASSUMPTION: Admins cannot modify each other — asserted to keep the Owner meaningfully distinct from Admin.]`
- No Role change can produce a second Owner or remove the sole Owner; ownership moves only by an Ownership Offer accepted under FR-42.
- A Role change takes effect on the target's active Sessions without requiring them to re-authenticate (see FR-34).

#### FR-14: Remove a Membership, or leave a Space

An Owner or Admin can remove a Membership; any Account can remove its own. Realizes UJ-6, UJ-8.

**Consequences (testable):**
- An Admin can remove Members and Viewers, and cannot remove the Owner or another Admin.
- The Owner's Membership cannot be removed by anyone, including the Owner, while it holds ownership; the Owner leaves by offering ownership (FR-8) and having it accepted (FR-42), or by deleting the Space (FR-7). A pending offer is not an exit.
- Removal revokes access immediately and takes effect on the removed Account's active Sessions and open editors (FR-34).
- Removal invalidates every API Token that Account holds for that Space (FR-36).
- Tasks the removed Account was Assignee of become unassigned; the Tasks survive.
- A removed Account retains its own Spaces, its Account, and every other Membership.

### 4.4 Access Control

**Description:** Authorisation in Yello is a function of `(Account, Space)`, never of Account alone. The same person is Owner in one Space and Viewer in another, and the product treats that as the ordinary case. Consequently no capability can be described, specified or tested without naming the Space it applies in, and the active Space must be resolved before any authorisation decision is possible. This is the invariant every other feature is written against. Realizes UJ-3, UJ-4, UJ-6.

**Functional Requirements:**

#### FR-15: Enforce Space-scoped authorisation

Every read and write of Space-scoped data is authorised against the acting Account's Membership in the Space that owns the data. Realizes UJ-3, UJ-4.

**Consequences (testable):**
- No request returns data from a Space in which the acting Account holds no Membership — including by direct identifier, by deep link, by search, by API, and by any listing or aggregate.
- Requesting a resource that exists in a Space the caller has no Membership in is indistinguishable from requesting one that does not exist; existence is never disclosed across a Space boundary.
- Requesting a resource in a Space the caller *does* belong to, but lacks the Role for, reports a permission failure rather than a not-found — the distinction is drawn at the Space boundary, not below it.
- An Owner of one Space has no elevated standing in any other Space, regardless of Membership.
- Authorisation is evaluated per request; it is never cached across a Space switch or inferred from a prior request.

#### FR-16: Apply the Role capability matrix

Each Role grants a fixed, Space-local set of capabilities. Realizes UJ-4, UJ-6.

*This matrix is the single source of truth for Role capability. Individual FRs restate the rows that apply to them so that each can be read and implemented alone; where a restatement and this matrix ever disagree, the matrix is correct and the restatement is a defect.*

| Capability | Owner | Admin | Member | Viewer |
|---|:--:|:--:|:--:|:--:|
| Read Projects and Tasks | ✔ | ✔ | ✔ | ✔ |
| Create, edit, delete Projects | ✔ | ✔ | ✔ | — |
| Create, edit, delete Tasks | ✔ | ✔ | ✔ | — |
| Edit a Task description collaboratively | ✔ | ✔ | ✔ | — |
| Assign a Task | ✔ | ✔ | ✔ | — |
| Configure Space default Statuses | ✔ | ✔ | — | — |
| Configure a Project's Status delta | ✔ | ✔ | — | — |
| Manage Labels | ✔ | ✔ | — | — |
| Issue and revoke Invitations | ✔ | ✔ | — | — |
| Change a Membership's Role | ✔ | Member ↔ Viewer only | — | — |
| Remove a Membership | ✔ | Members and Viewers only | — | — |
| Rename the Space | ✔ | ✔ | — | — |
| Transfer ownership | ✔ | — | — | — |
| Delete the Space | ✔ | — | — | — |
| Issue an API Token for oneself | ✔ | ✔ | ✔ | ✔ |

**Consequences (testable):**
- A Viewer's write attempt is refused at the API regardless of what the interface offered; the interface hiding an action is never the mechanism that enforces it.
- An API Token issued by a Viewer can read and cannot write, matching that Account's Role at the moment each request is evaluated rather than at the moment the Token was issued.
- Every capability above is refused for an Account holding no Membership in the Space, without exception and without disclosing existence.

### 4.5 Projects

**Description:** A Project is a named collection of Tasks inside one Space. It exists to group work, not to control access — Projects have no Membership and no permissions of their own, and everyone in a Space sees every Project in it at the level their Role allows. A Project never moves between Spaces, which closes a cross-Space leak by construction rather than by rule.

**Functional Requirements:**

#### FR-17: Create, rename and delete a Project

An Owner, Admin or Member can create, rename and delete Projects within their active Space. Realizes UJ-1.

**Consequences (testable):**
- A Viewer can do none of these.
- A Project belongs to exactly one Space, fixed at creation. No operation moves a Project to another Space.
- A new Project's effective Status set is the Space default set (FR-24) with an empty delta.
- Deleting a Project deletes its Tasks. `[ASSUMPTION: immediate and irreversible in v1, consistent with FR-7.]`

#### FR-18: List Projects in a Space

Any Membership can list the Projects in its active Space.

**Consequences (testable):**
- The listing contains every Project in the active Space and no Project from any other Space.
- Viewers see the same Projects as Members; Role affects what can be done to a Project, not whether it is visible.

### 4.6 Tasks

**Description:** The unit of work. A Task carries a title, a description, a Status, and optionally an Assignee, a due date and Labels. Assignment is constrained to the Space — a Task can only be assigned to someone who is already a Member of the Space it lives in, so assignment can never be used to reach across a boundary or to imply the existence of an Account outside it. Realizes UJ-1, UJ-5.

**Functional Requirements:**

#### FR-19: Create a Task

An Owner, Admin or Member can create a Task within a Project. Realizes UJ-1.

**Consequences (testable):**
- A Viewer cannot create Tasks.
- A new Task takes the first Status in its Project's effective Status set.
- A Task belongs to exactly one Project at any moment, and to exactly one Space permanently. It may later be moved to another Project in the same Space (FR-41); no operation moves it to another Space.

#### FR-20: Edit Task attributes

An Owner, Admin or Member can change a Task's title, Status, due date and Labels. Description editing is FR-31.

**Consequences (testable):**
- A Viewer cannot change any attribute.
- A Task's Status can only be set to a value in its Project's effective Status set; a Status valid in a sibling Project is refused.

#### FR-21: Assign a Task

An Owner, Admin or Member can assign a Task to a Membership in the same Space.

**Consequences (testable):**
- Only Memberships of the Task's Space are offered as Assignees, and only those are accepted.
- An Account with no Membership in the Space cannot be set as Assignee by any route, including the API.
- When the Assignee's Membership is removed (FR-14) or their Account deleted (FR-3), the Task becomes unassigned and is not deleted.
- Assigning a Task to a Viewer is permitted, and does not grant the Viewer any write capability over that Task. Responsibility and capability are deliberately separable: a client contact can be marked responsible for something they cannot change. The alternative — restricting Assignee to Roles that can write — was rejected because it would make a demotion to Viewer silently unassign that person's work.
- Demoting an Assignee to Viewer does not unassign their Tasks.

#### FR-22: Manage Labels

An Owner or Admin can define the Labels available in a Space; an Owner, Admin or Member can apply them to Tasks.

**Consequences (testable):**
- Labels are defined per Space and are available to every Project in it.
- A Label applied to Tasks cannot be deleted without those applications being removed; no Task references a Label that does not exist.

#### FR-23: Delete a Task

An Owner, Admin or Member can delete a Task.

**Consequences (testable):**
- A Viewer cannot delete Tasks.
- Deleting a Task terminates any active collaborative editing session on it (FR-31); participants are told it was deleted rather than losing their connection silently.

#### FR-41: Move a Task to another Project

An Owner, Admin or Member can move a Task to a different Project within the same Space.

**Consequences (testable):**
- A Viewer cannot move Tasks.
- Only Projects in the Task's own Space are offered as destinations, and only those are accepted — including via the API.
- If the Task's Status does not exist in the destination Project's effective Status set (§4.7), a destination Status must be supplied as part of the move, on the same terms as FR-26. The move does not take effect without one.
- If the Task's Status does exist in the destination Project's effective set, the Status is preserved and no mapping is required.
- Assignee, Labels, due date and description survive the move unchanged, because both Projects share a Space and therefore share its Memberships and Labels.
- An active collaborative editing session on the Task continues across the move; participants are not disconnected.

**Bulk form.** Every Task currently in one Status can be moved from one Project to another in the same Space in a single operation.

- Because the selection shares a Status by construction, the operation carries exactly one mapping decision, on the same terms as the single-Task form: the Status is preserved where the destination exposes it, and one destination Status is required where it does not.
- The bulk form is reachable from a Board column and from a List View filtered to one Status, and is available on the API on the same terms as the single-Task form (FR-35).
- A bulk move is atomic: every selected Task moves, or none does. A bulk move that cannot complete is refused rather than partially applied, consistent with FR-26, and the refusal is visible rather than silent.
- A Viewer cannot use either form.
- Active collaborative editing sessions on the moved Tasks continue across a bulk move.
- This is the safe path for retiring a Project without losing its work — one move per Status in its effective set rather than one per Task, against a Project deletion that is irreversible (FR-17). Mixed-Status selections remain one at a time; a per-Status mapping table was considered and rejected (`addendum.md`).

### 4.7 Status Configuration

**Description:** A Space defines a default set of Statuses. Each Project holds a **delta** over that set — Statuses added, removed, renamed or reordered — and its effective set is the Space defaults with its delta applied. This is chosen over the two simpler models: a single fixed set everywhere removes the resolution rule entirely, and fully independent per-Project sets make Boards incomparable within a Space and multiply every Status problem by the number of Projects. There is no concept of a Project "inheriting" versus "overriding" as a mode, and no operation reverts a Project to the defaults; a Project's Status set is simply editable for as long as the Project exists. The rule that makes this safe is universal: **removing a Status, at either level, requires mapping the Tasks that occupy it to another Status.** Removal is a migration, always — which is why no Task can ever hold a Status that its Project does not expose.

**Functional Requirements:**

#### FR-24: Define Space default Statuses

An Owner or Admin can define the ordered default Status set for a Space.

**Consequences (testable):**
- Members and Viewers cannot change the Space default Status set.
- A newly created Space has a non-empty default set. `[ASSUMPTION: Todo / In Progress / Done.]`
- The default set can never be empty; removing the last Status is refused.

#### FR-25: Apply a Project Status delta

An Owner or Admin can add, remove, rename and reorder Statuses within a Project, expressed as a delta over the Space defaults.

**Consequences (testable):**
- Members and Viewers cannot change a Project's Status delta.
- A Project's effective Status set is deterministic: the Space defaults with that Project's delta applied, in the delta's order.
- A Project's effective set can never be empty.
- Two Projects in the same Space may hold different effective sets simultaneously, and a Status valid in one is not accepted for a Task in the other (FR-20).
- A Status removed from a Project can be added back to that Project at any time.

#### FR-26: Map Tasks when a Status is removed

Removing a Status requires every Task occupying it to be mapped to another Status in the same operation.

**Consequences (testable):**
- The removal does not take effect unless a destination Status is supplied for every occupying Task; there is no partial application.
- No Task is ever left holding a Status absent from its Project's effective set — before, during, or after the operation.
- Removing a Status that no Task occupies requires no mapping and succeeds directly.
- The destination must exist in the effective set that will be in force after the removal completes; a Task cannot be mapped onto a Status that the same operation also removes.

#### FR-27: Propagate Space-level Status changes

Changes to the Space default set reach every Project according to that Project's delta.

**Consequences (testable):**
- Adding a Status at Space level adds it to every Project that has not removed it.
- Renaming a Status at Space level renames it in every Project that has not itself renamed it, including Projects that have reordered it.
- Where one or more Projects have themselves renamed that Status, the operation reports the conflict and offers to cascade. Cascading replaces those Projects' names; declining preserves them. The Space-level rename applies either way to non-conflicting Projects.
- Removing a Status at Space level requires mapping under FR-26. A single destination Status is chosen once and applied across every affected Project that can accept it.
- Removing a Status at Space level has no effect on Projects that had already removed it.
- `[ASSUMPTION: the cascade offer is a single choice applied to every conflicting Project at once, consistent with the single Space-wide mapping decision above.]`
- Where a Project's post-removal effective set does not contain the chosen destination, the operation reports that Project — and how many of its Tasks are affected — and requires a destination drawn from that Project's own post-removal effective set. There is no fallback and no silent placement.
- Nothing applies until every reported Project has a destination. The Space-level removal, the Space-wide mapping and every per-Project exception apply as one transaction or not at all.
- This is always satisfiable: a Project's effective set can never be empty (FR-25), so a valid destination exists in every affected Project.
- Both halves of this requirement behave the same way on conflict — rename reports and asks, removal reports and asks. Neither decides for the Admin. *An earlier draft let affected Tasks fall to the Project's first Status; rejected because it made one half of the requirement guess while the other asked.*

**Notes:** Because a Space-level rename must detect that a Project renamed *the same* Status in order to offer the cascade, a Project's delta necessarily references Statuses by identity rather than by name. Stated here because it constrains the data model; the mechanism itself belongs in the architecture.

### 4.8 Board and List Views

**Description:** The two ways of looking at a Project's Tasks. The Board arranges Tasks in columns by Status in the Project's effective order, and is the primary working surface. The List View is the same Tasks as rows, filterable and sortable. Both are read-available to every Role; only the manipulation differs. Realizes UJ-1, UJ-4.

**Functional Requirements:**

#### FR-28: View a Project as a Board

Any Membership can view a Project's Tasks as columns ordered by the Project's effective Status set.

**Consequences (testable):**
- Columns appear in the Project's effective order, including where a delta reordered them.
- Every Task in the Project appears in exactly one column.
- A Viewer sees the identical Board to a Member, with no manipulation affordances present.
- At the NFR-8 bound of 5,000 Tasks in a Project, the Board still satisfies NFR-5 and NFR-9. Every Task remains reachable and appears in exactly one column. How the view achieves that at that size — paging, virtualisation, or something else — is the architecture's call; nothing in this document provides it, and the three requirements cannot all hold naively.

#### FR-29: Move and order Tasks on a Board

An Owner, Admin or Member can move a Task between columns, changing its Status, and reorder Tasks within a column.

**Consequences (testable):**
- A Viewer cannot move or reorder.
- Moving a Task to a column sets its Status to that column's Status.
- Concurrent moves by two Users converge to one order that both observe; neither User's move is silently discarded.
- Ordering is per column and survives reload.

#### FR-30: View a Project as a filterable list

Any Membership can view a Project's Tasks as rows, filtered and sorted by Status, Assignee, due date and Label.

**Consequences (testable):**
- Filters never surface a Task from another Project or another Space.
- Filtering by Assignee offers only Memberships of the active Space.
- At the NFR-8 bound of 5,000 Tasks in a Project, the List View still satisfies NFR-5 and NFR-9, on the same terms as FR-28.

### 4.9 Collaborative Task Editing

**Description:** Two or more Users can edit the same Task description at the same time and see each other doing it. Nobody is presented with a conflict dialog, a lock, or a "someone else changed this" warning — the product's position is that concurrent editing is the normal case, not an error to be reported. The load-bearing requirement in this feature is not text convergence but **what happens when permission changes while an editing session is open**: a User demoted to Viewer or removed from the Space mid-sentence, with unsynchronised local changes in hand. That case sits on the seam between isolation, authorisation and concurrency, and it is the requirement most likely to be got wrong. Realizes UJ-5, UJ-6.

**Functional Requirements:**

#### FR-31: Edit a Task description concurrently

Multiple Users with write capability in a Space can edit the same Task description simultaneously. Realizes UJ-5.

**Consequences (testable):**
- Two Users editing different parts of the same description simultaneously both retain their changes; neither is overwritten.
- Two Users editing the same region simultaneously arrive at an identical final text, and that text is the one persisted.
- No participant is shown a merge prompt, a lock, or a stale-content warning during normal concurrent editing.
- A Viewer cannot enter an editing session at all, and cannot do so via the API.

#### FR-32: Show Presence

Users editing or viewing the same Task can see who else is there. Realizes UJ-5.

**Consequences (testable):**
- Presence shows only Memberships of the same Space.
- Presence disappears within the interval stated in NFR-3 after a participant disconnects, and does so without their action.
- Presence never reveals an Account's activity in any other Space.

#### FR-33: Reconcile after disconnection

A User who loses connectivity mid-edit and returns has their local changes reconciled rather than lost or duplicated. Realizes UJ-5.

**Consequences (testable):**
- Changes made while disconnected are applied on reconnection, and appear exactly once.
- Changes made by others during the disconnection are present after reconnection.
- If reconciliation cannot complete, the User is told explicitly and their unsynchronised text is not silently discarded.

#### FR-34: Apply permission changes to live sessions

A change to a Membership's Role, or its removal, takes effect on that Account's open editing sessions without requiring re-authentication. Realizes UJ-6.

**Consequences (testable):**
- On removal from the Space, the participant's editing session terminates, their unsynchronised local changes are **not** applied, and they are told their access has ended.
- On demotion to Viewer, their editing capability ends while read access continues uninterrupted; unsynchronised changes made before the demotion are not applied.
- No change authored after the moment of removal or demotion reaches the Space by any route, including a delayed or retried synchronisation.
- Changes the participant had already synchronised before the change took effect are retained; revocation stops future writes and does not roll back past ones.
- The effect is observable without the affected participant taking any action.

**Notes:** `[NOTE FOR PM]` FR-34 is the acceptance criterion this product should be judged on. If everything else works and this does not, the isolation model is decorative.

### 4.10 Public API

**Description:** Yello's second surface. Everything a person can do in the browser against a Space, a script can do too — under the same authorisation rules, evaluated the same way — with one stated exception, Board ordering, which is readable but not writable (FR-35). An API Token is bound to exactly one Space, which means the isolation invariant holds identically on both surfaces and the API cannot become a route around it. The API is a published contract: consumers are entitled to a stable shape and to advance notice when it changes. Realizes UJ-7.

**Functional Requirements:**

#### FR-35: Expose Spaces, Projects and Tasks over the API

An authenticated caller can read and write Projects and Tasks in the Space its Token is bound to. Realizes UJ-7.

**Consequences (testable):**
- Every capability in FR-16 is enforced identically on the API; no operation refused in the browser succeeds via the API.
- The API exposes no operation that enumerates Spaces, Accounts or Memberships beyond the Token's Space.
- A Task's position within its Status (FR-29) is readable over the API, so a consumer can reproduce what a user sees. It is not writable: reordering happens only through the interface, which keeps FR-29's convergence requirement confined to one surface. This is the single place the API is deliberately narrower than the browser, and it is stated here so the gap is a decision rather than an oversight.

#### FR-36: Issue and scope an API Token

Any Membership can issue an API Token for the Space that Membership is in — rather than for the Account as a whole, which would make the Token the only object in Yello that spans Spaces. Realizes UJ-7.

**Consequences (testable):**
- A Token is bound to exactly one Space at issue time and that binding cannot be changed.
- A Token issued by an Account that owns several Spaces reaches only the Space it names, including Spaces the same Account owns.
- A Token's effective capability is the issuing Account's Role in that Space **at the time each request is evaluated**, not at the time of issue — a Token issued as a Member loses write capability when its Account is demoted to Viewer.
- A Token is invalidated when its Account's Membership is removed (FR-14), when the Space is deleted (FR-7), or when the Account is deleted (FR-3).
- A Token is displayed once at creation and is not retrievable afterwards.

#### FR-37: Version the API and deprecate predictably

The API is versioned, and a consumer written against one version continues to work when a newer one ships. Realizes UJ-7.

**Consequences (testable):**
- A request that names a supported version receives that version's response shape, regardless of what other versions exist.
- No change within a version removes a field, renames a field, changes a field's type, or narrows accepted input.
- A version is announced as deprecated before it stops working, and continues to serve requests throughout the announced period.
- `[ASSUMPTION: version is selected by URL path segment, and exactly two versions are supported concurrently. Mechanism belongs in the architecture; the constraint belongs here.]`

#### FR-38: Rate limit API requests

The API limits request rate per Token.

**Consequences (testable):**
- Exceeding the limit produces a distinct, documented refusal that a client can detect and act on, and states when the caller may retry.
- Rate limiting is applied per Token, so one Space's consumption cannot exhaust another's.
- Rate limiting never causes a write to be applied more than once when a client retries.

### 4.11 Notifications

**Description:** Yello sends email only where an action outside the product is required or where someone needs to know something happened while they were not looking. Invitation delivery is load-bearing — without it, FR-11 has no entry point for an invitee who has never used Yello. Ownership Offer delivery is load-bearing for a different reason: without it, FR-8's expiry closes on a recipient who was never told (FR-43). Realizes UJ-2, UJ-3, UJ-8.

**Functional Requirements:**

#### FR-39: Deliver an Invitation by email

Issuing an Invitation sends an email to the invited address containing a means of accepting it. Realizes UJ-2, UJ-3.

**Consequences (testable):**
- The email names the Space and the Role offered, and identifies who issued it.
- The email discloses nothing about the Space's contents, its other Members, or any other Space.
- Following the acceptance route after revocation reports only that the Invitation is no longer valid (FR-11).
- Following the acceptance route does not by itself join the invitee. It presents the offer; acceptance is separate and requires authentication as the invited Account (FR-11).
- `[ASSUMPTION: the acceptance route expires after a fixed period — 7 days — after which the Invitation must be reissued.]`

#### FR-40: Notify on assignment

An Account assigned to a Task is notified.

**Consequences (testable):**
- The notification names the Space, Project and Task, and nothing from any other Space.
- An Account is not notified of its own action.
- `[ASSUMPTION: assignment notification is email, and is per-event rather than digested. Frequency control is a v2 concern.]`

#### FR-43: Deliver an Ownership Offer by email

Issuing an Ownership Offer sends an email to the named recipient's Account. Realizes UJ-8.

**Consequences (testable):**
- The email names the Space, states that the recipient is being offered ownership of it, and identifies who offered it.
- The email discloses nothing about the Space's contents, its other Memberships, or any other Space. Stated as a consequence rather than left to implementation because NFR-1 binds notifications, and an email already delivered cannot be recalled when the offer is later revoked or lapses (FR-8).
- The email carries no means of accepting. Acceptance and declining happen inside the Space under FR-42; the email exists to bring the recipient back before the offer expires, and authorises nothing on its own.
- Revoking the offer, declining it, or letting it lapse sends no further email. There is exactly one email per offer.
- Because an Ownership Offer can only name an existing Membership (FR-8), the recipient's address is already known to the Space's Owner and Admins, so sending this email discloses no address that §6.1 protects.

**Why this does not violate SM-C4.** §10 names notification volume as a counter-metric, so adding a notification needs saying out loud. SM-C4's concern is precisely stated: volume *"should not increase to drive SM-3 or SM-4"* — it guards against notifications added to chase adoption or Invitation conversion. This one drives no metric. It exists because without it a specific, reachable trap closes on an Owner who has done nothing wrong. One email per offer, against at most one pending offer per Space (FR-8), is the smallest possible volume that removes the trap.

**Feature-specific NFRs:**
- A record that a notification was sent is retained — Space, kind and timestamp, never message content or recipient address — so §10's SM-C4 is derivable. No product surface reads it.

## 5. Cross-Cutting Non-Functional Requirements

*System-wide quality attributes, not tied to a single feature. Each is written so that it can fail — a requirement that cannot be violated by a plausible implementation is not a requirement, it is a sentiment. Numbers are stated even where they are provisional, because an unstated budget is one nobody can miss.*

#### NFR-1: Isolation is absolute

No data belonging to a Space reaches any Account without a Membership in that Space, by any route.

- Holds for the browser and the API identically, and for reads, writes, listings, aggregates, search results, notifications and error messages.
- Holds for identifiers: possessing the identifier of a Task, Project or Space confers nothing.
- Holds under error: a failure, timeout or partial response never discloses data or existence across a Space boundary.
- **This is the one requirement with no acceptable failure rate.** A single verified cross-Space disclosure blocks release.

#### NFR-2: Authorisation is evaluated fresh, per request

No authorisation decision is served from a cache that could outlive the Membership it was derived from.

- **On the request path — reflected on the very next request. No tolerance.** A Role change or Membership removal governs the next request that Account makes, on the browser and the API alike. There is no budget here because there is nothing to spend it on: no cache may outlive a request, so a delay of even one request means a cache was introduced, which is the failure this requirement exists to catch.
- **On the live-session path — within 1 second** of the transaction boundary, without the affected Account acting. Generous against NFR-3's 300 ms remote-edit budget, and tight enough that a poller or a cross-replica hop fails it.
- FR-34's guarantee is independent of both timings: unsynchronised local changes are never applied, however long propagation takes.
- No request is authorised using a Role established during a previous active Space.
- Applies to API Tokens on the same terms (FR-36).

*Revised 2026-08-18. This was one 5-second budget for both paths. Against the chosen architecture that could not fail — authorisation is resolved per request from the Membership row with no cache permitted to outlive the request, and permission change is pushed in-process at the transaction boundary on a single replica. A budget no plausible implementation can violate is the sentiment §5 opens by warning against, and §10's SM-2 gates release on it. Both clauses above can fail.*

#### NFR-3: Collaborative editing feels immediate

- A local edit renders locally within **16 ms** — one frame at 60 Hz — without waiting on any network round trip.
- A remote participant's edit renders within **300 ms at the 95th percentile** on a connection with 50 ms round-trip latency.
- Presence appears within **2 seconds** of a participant arriving and disappears within **10 seconds** of them leaving.

#### NFR-4: Concurrent edits converge

- All participants in an editing session observe identical text within **2 seconds** of the last edit by any of them.
- Convergence holds for at least **10 simultaneous editors** on one Task description.
- A participant disconnected for up to **5 minutes** reconciles on reconnection without loss or duplication (FR-33).

#### NFR-5: The API is predictable

- Read requests complete within **300 ms** and writes within **500 ms**, both at the 95th percentile, measured server-side within the stated scale envelope (NFR-8).
- Every refusal carries a machine-readable reason a client can branch on; no client should need to parse prose.
- Retrying a write that timed out does not apply it twice.

#### NFR-6: Credentials are held safely

- Passwords are stored using a deliberately slow one-way function and are never recoverable. The work factor is the architecture's call, not this document's; it must be tunable without re-registering existing Accounts.
- API Tokens are stored such that a read of the datastore does not yield usable Tokens, and are displayed exactly once (FR-36).
- No password or Token appears in any log, error message, notification, analytics event or API response.
- All traffic is encrypted in transit.
- Encryption **at rest is not required here**. Whatever the datastore provides is incidental rather than specified; asserting it is one of the prerequisites behind the §6.4 data-protection gate.

#### NFR-7: Refusals are observable

- Every authorisation refusal is recorded with the acting Account, the target Space, the capability attempted and the outcome.
- Cross-Space access attempts are distinguishable in that record from within-Space permission failures — the two mean very different things.
- These records are retained long enough to investigate an incident. `[ASSUMPTION: 90 days.]`

#### NFR-8: Scale envelope

The system is required to hold its other guarantees within these bounds, and is not required to hold them beyond:

| Dimension | Bound |
|---|---|
| Spaces per Account | 50 |
| Memberships per Space | 100 |
| Projects per Space | 50 |
| Tasks per Project | 5,000 |
| Concurrent editors per Task | 10 |
| Concurrent active Sessions per Space | 50 |

Exceeding a bound must degrade visibly rather than silently — a refusal, not a wrong answer. A bound that is not enforced is a defect, not a relaxation.

These bounds are set by judgement rather than measurement, and are **confirmed final for v1**. §11 asked that they be revisited with evidence before the architecture was shaped around them; that ordering was missed, and with no users there is no usage evidence to gather. The only obtainable evidence is load testing, so the verification is scheduled at the NFR-evidence audit, against the single choke point the architecture enforces them at. Revising a bound after that is an architecture change, not a document edit.

#### NFR-9: The primary flows are accessible

- Registration, Space switching, the Board, the Task editor and the invitation flow meet **WCAG 2.1 AA**.
- Every Board operation available by pointer is available by keyboard, including moving a Task between columns.
- Presence and permission-change notices are announced to assistive technology, not conveyed by colour or position alone.

## 6. Constraints and Guardrails

### 6.1 Privacy

- An Account's existence is never disclosed to anyone who has not been given it. Registration, invitation and authentication responses are uniform whether or not an address is known to Yello (FR-1, FR-2, FR-10).
- An Account's Memberships are visible only within each Space. Nobody can enumerate the Spaces another Account belongs to, including a Space's Owner.
- Email addresses are visible to Owners and Admins of Spaces the Account is a Member of, and to nobody else.
- Yello collects no behavioural analytics on the contents of Spaces.
- No product surface aggregates across Spaces. The behavioural measures in §10 are not a product feature: they are aggregates the operator computes by querying the datastore directly, outside the request path and outside the authorisation model. This rules out an in-product metrics dashboard, an admin analytics view, and any endpoint returning a count spanning Spaces — each would breach §6.1 and NFR-1 to produce a number nobody is entitled to.

### 6.2 Data lifecycle

- Deletion of an Account, Space, Project or Task is irreversible in v1. There is no trash and no restore.
- Deleting an Account never deletes another Account's work (FR-3).
- Deleting a Space deletes its contents for every Member, and this is stated at the point of the action.
- `[ASSUMPTION: backups exist for disaster recovery but are not user-accessible and are not a restore path for deliberate deletion.]`

### 6.3 Cost

- Total running cost stays under **£30 per month** at the scale in NFR-8. A design an architect cannot cost against that ceiling has not been specified enough to accept. `[ASSUMPTION: the figure is set by what the project is worth spending rather than by pricing analysis. It exists so that "too expensive" is a decidable question; revise it deliberately rather than drifting past it.]`
- No design requiring always-on dedicated infrastructure per Space, or per active editing session, fits that ceiling.
- The real-time requirements in NFR-3 and NFR-4 are the most likely source of cost pressure and must be satisfied within a single modest deployment.

### 6.4 Data protection

v1 is a **single-operator deployment** and claims no data-protection posture. No lawful basis, data region, encryption-at-rest assertion, breach-notification position or subject-access route is specified, and none is required while the operator is the only data subject. §3.2 already rules out regulated environments as an audience.

The gate is testable rather than aspirational: **the first Account created by anyone other than the operator makes this document non-compliant until amended.** From that moment the following are prerequisites for continued use, not a backlog:

| Required at the gate | Why it is absent now |
|---|---|
| Lawful basis for holding email addresses and authored content | No data subject other than the operator |
| A stated data region, and no replication outside it | Nothing pins a region — not this document, not the architecture |
| Encryption at rest asserted | NFR-6 covers transit only |
| A breach-notification position | Undefined. A verified cross-Space disclosure would be notifiable by definition |
| A subject-access or export route | FR-3 covers erasure; nothing covers access or portability |

**What already holds, incidentally** — recorded so a later reader does not rebuild it. **Erasure:** FR-3 is a hard delete; every Membership goes, the email address is freed for reuse, and the new Account inherits nothing. This holds only because ownership cannot be forced on an Account (FR-42) — under the original immediate transfer, another Account could have blocked deletion indefinitely. **Minimisation:** no behavioural analytics on Space contents, and email addresses readable only by Owners and Admins of a Space the Account is a Member of. **Retention limit:** refusal records capped at 90 days (NFR-7). **Privacy by design:** an Account's existence is never disclosed, and its Memberships cannot be enumerated by anyone, including a Space's Owner.

## 7. Information Architecture

Included because navigation here is not decoration: the Space switcher is the mechanism by which authorisation context is established, so it is a functional surface, not a layout choice.

- **Unauthenticated** — register, sign in, accept an Invitation.
- **Space context bar** — always present once authenticated. Names the active Space, shows the acting Role, and switches Space (FR-9). Nothing outside it is meaningful without it.
- **Space home** — the Projects in the active Space (FR-18).
- **Project** — Board (default) and List View (FR-28, FR-30).
- **Task detail** — attributes, collaborative description, Presence (FR-20, FR-31, FR-32).
- **Space settings** — Members and Invitations, default Statuses, Labels, ownership, deletion. Owner and Admin only.
- **Project settings** — Status delta. Owner and Admin only.
- **Account settings** — profile, password, API Tokens, account deletion. Spans Spaces; the only surface that does.

The acting Role must be legible from the interface at all times, and capabilities the Role lacks are absent rather than present-and-failing (UJ-4).

## 8. Non-Goals

- **Yello is not an organisation-management product.** There is no company, no billing entity, no directory, no administrator with authority across Spaces they are not in. Nothing that requires one will be built. This rules out enterprise SSO and directory sync, which presuppose an organisation that owns accounts; it does not rule out OAuth sign-in, which authenticates an individual and is deferred rather than rejected (§9.2).
- **Yello is not a project management tool in the formal sense.** No dependencies, no critical path, no Gantt, no resource levelling, no effort or cost reporting.
- **Yello does not become a communication tool.** No chat, no threads, no direct messages. Notifications exist to bring people back, not to hold conversations.
- **Yello does not federate.** No cross-Space views, no aggregate dashboards, no "all my Tasks everywhere". This is not an omission to be corrected later — a surface that spans Spaces contradicts the model the product is built on.
- **Yello is not offline-first.** It assumes connectivity and degrades honestly without it.
- **Yello has no public or anonymous access.** Membership is the only route in; there is no share link, no public Board, no read-only URL.

## 9. MVP Scope

### 9.1 In scope

Everything in §4 — all forty-three functional requirements across eleven features: Accounts and Authentication, Spaces, Membership and Invitations, Access Control, Projects, Tasks, Status Configuration, Board and List Views, Collaborative Task Editing, Public API, Notifications. Nothing specified in §4 is deferred. What was considered and left out is below.

### 9.2 Out of scope for MVP

- **Iteration planning** — no cycles, sprints, backlogs or time-boxing. Yello v1 has no opinion about *when* work happens, only what it is and where it lives.
- **Task comments and activity history** — no discussion thread, no audit trail of who changed what. `[NOTE FOR PM: the most frequently missed of these. Worth revisiting if timeline permits.]`
- **Subtasks and task relationships** — no hierarchy, no blocking, no linking.
- **Attachments** — no file upload or storage.
- **Cross-Project search** — search is scoped to a Project in v1.
- **Webhooks and outbound integrations** — the API is inbound only. No Slack, no Git, no calendar.
- **Custom fields** — Task shape is fixed.
- **Recurring Tasks.**
- **Enterprise SSO, directory sync, domain-restricted invitation** — see §8; these require an organisation concept Yello does not have.
- **OAuth sign-in** — signing in with an existing third-party identity as an alternative to email and password. Deferred, not ruled out: this is a wanted addition and is distinct from enterprise SSO, since it authenticates an individual Account rather than presupposing an organisation that owns it. When added, it introduces Yello's first genuine inbound dependency on a third party, and with it the failure modes — provider outage, token expiry, revoked consent, changed provider contract — that FR-1 and FR-2 currently have no reason to handle. It also breaks §2's definition of an Account as unique *by email address*, since a provider may return a different address than the one on file, or none; and it requires NFR-6 to tolerate Accounts holding no password. Implement FR-1, FR-2 and NFR-6 so those can change without redesign.
- **Mobile applications** — the web interface is responsive; there is no native client.
- **Notification preferences** — notifications are per-event and not configurable (FR-40).
- **Session telemetry** — nothing records session duration, so §10's SM-C2 (time in application) is defined but not measurable in v1. Stated rather than quietly assumed.
- **Bulk Task move across mixed Statuses** — FR-41's bulk form is scoped to one Status at a time. A mixed-selection move with a per-Status mapping table was considered and rejected (`addendum.md`).
- **Billing and plan limits** — no monetisation of any kind in v1.

## 10. Success Metrics

*Two kinds of metric, deliberately separated. The gating metrics are measurable from the first build and a release fails without them. The behavioural metrics are defined but carry no thresholds: Yello has no users, and a target invented now would be indistinguishable from one that had been earned. They are stated so that the right things stay queryable, and so that whoever sets thresholds later knows which direction each should move.*

*How the behavioural measures are obtained: they are **not a product feature**. Each is an aggregate the operator computes by querying the datastore directly (§6.1). None reads Task titles, descriptions, Labels or Project names — they are structural and metadata counts only, which is what keeps them clear of §6.1's no-analytics rule. Three retention guarantees exist solely to keep them derivable, and are carried as requirements rather than here: the Invitation record keeps its terminal state (FR-10), a notification send record is kept (FR-40), and compaction of the Task description change log must preserve per-author change counts and timestamps — the last of these is an obligation on the architecture, which currently does not guarantee it.*

**Gating — a release fails without these**

- **SM-1: Isolation integrity.** Zero verified cross-Space disclosures, across browser and API, in any released build. Measured by an isolation test suite exercised on every change. Validates FR-15, FR-16, FR-35, FR-36, NFR-1.
- **SM-2: Revocation latency.** Permission changes govern the affected Account's very next request with no tolerance, and take effect on open live sessions within 1 second, in 100% of tested cases — including sessions holding unsynchronised local edits. Validates FR-34, NFR-2.

**Behavioural — instrument now, threshold later**

*No targets are set. Each entry names what is measured and which direction is good.*

- **SM-3: Multi-Space adoption.** Proportion of Accounts holding Membership in two or more Spaces. Higher is better. This is the product's central bet — if people only ever use one Space, the primitive did not earn its generality, and §1's thesis is wrong. The most important number in this group. Validates FR-5, FR-9, FR-11.
- **SM-4: Invitation conversion.** Proportion of issued Invitations accepted. Higher is better, but read it alongside SM-C3. Note the funnel carries a deliberate extra step — acceptance requires authentication plus an explicit act (FR-11) — so this reads lower than a one-click design would, by choice rather than by defect. Validates FR-10, FR-11, FR-39.
- **SM-5: Concurrent editing actually happens.** Proportion of multi-Member Spaces in which a Task description is edited by two Users within the same session. Higher is better. If it stays near zero, the most expensive feature in the product is unused and §4.9 should be reconsidered rather than optimised. Validates FR-31, FR-32.
- **SM-6: API adoption.** Proportion of Spaces with at least one active API Token. Higher is better, though a low figure is not itself a failure — the API exists to make the isolation model hold on a second surface as much as to be popular. Validates FR-35 – FR-38.

**Counter-metrics (do not optimise)**

- **SM-C1: Spaces created per Account.** Should *not* be maximised. A high Space count with low Task counts per Space indicates the primitive is confusing rather than adopted — people creating Spaces because they cannot tell what one is for. Counterbalances SM-3.
- **SM-C2: Time in application.** **Defined but not measurable in v1** — nothing records session duration, and session telemetry is out of scope (§9.2). Kept so that whoever adds telemetry later knows this number exists and which direction it should not move. Should *not* be maximised. UJ-1's success condition is Ravi closing the tab. A task tool people spend longer inside is working worse, not harder. Counterbalances SM-5.
- **SM-C3: Invitations issued.** Should *not* be maximised. The goal is the right people in a Space, not more people. Growth in invitations without corresponding growth in SM-5 means Spaces are accumulating spectators. Counterbalances SM-4.
- **SM-C4: Notification volume.** Should *not* increase to drive SM-3 or SM-4. Every additional notification is a cost to the recipient, and §8 rules out Yello becoming a communication tool. Counterbalances SM-4.

## 11. Open Questions — resolved

*All six questions this document originally carried were resolved on 2026-08-18. They are kept here with their resolutions rather than deleted, so a reader who saw the earlier version can see what changed and why. Rejected alternatives are in `addendum.md`; the full decision trail is `specs/spec-yello/.memlog.md`.*

1. **Can ownership be transferred to someone who then declines it?** — **Resolved: it cannot, because ownership is now an offer.** FR-8 became an offer and FR-42 was added to accept or decline it. The original immediate transfer permitted a trap: chained with FR-14 and FR-3, an Owner could transfer a Space to any Membership, remove their own now-Admin Membership, and leave that person permanently unable to delete their Account. See FR-42.
2. **Should a Space-level Status removal be possible when Projects have diverged significantly?** — **Resolved: yes, but it reports and asks instead of guessing.** FR-27 now names every Project that cannot accept the Space-wide destination and requires a destination for each. The first-Status fallback is gone. The real defect was that FR-27's rename half asked while its removal half guessed.
3. **Is 5 seconds the right revocation budget?** — **Resolved: no, and it could not fail.** NFR-2 is now two clauses: the very next request on the request path, and 1 second on the live-session path. Against per-request authorisation with no cache outliving a request, and an in-process push at the transaction boundary, a 5-second budget was unfailable while SM-2 gated release on it.
4. **Are the NFR-8 scale bounds defensible?** — **Resolved: confirmed final for v1, with the verification rescheduled.** The revisit was to happen before the architecture was shaped around them and did not. With no users there is no usage evidence; the only obtainable evidence is load testing, so verification moves to the NFR-evidence audit. Separately, this surfaced that 5,000 Tasks per Project collides with FR-28 and FR-30 against NFR-5 and NFR-9 — now stated on both requirements.
5. **Should acceptance of an Invitation by an existing Account require confirmation?** — **Resolved: yes, and the token was never sufficient authority.** FR-11 now requires the invitee authenticated as the invited Account plus a deliberate act. A bare fetch of the acceptance route creates nothing, which also closes acceptance by mail scanners, prefetchers and forwarded links.
6. **Does FR-41 need a bulk form?** — **Resolved: yes, scoped to one Status at a time.** Sharing a Status means exactly one mapping decision rather than a per-Status table. The motivation is not tedium but that Project deletion destroys Tasks irreversibly, so without a bulk form the safe path for retiring a large Project needed one operation per Task while the destructive path needed one click.

**Still genuinely open, and owned elsewhere.** Whether NFR-5 is measured warm or cold is undecided; the chosen deployment shape makes most requests cold under sparse traffic against a 300 ms p95 read budget. That deferral belongs to the architecture, not to this document.

## 12. Assumptions Index

*Every `[ASSUMPTION]` in this document, surfaced for explicit confirmation. Two entries were retired on 2026-08-18 because they became decisions rather than assumptions — the FR-27 first-Status fallback (replaced by report-and-ask) and the NFR-8 bounds (confirmed final). One was added for the Ownership Offer. Four of those below have hardened into architecture and now cost more than a document edit to reverse; they are marked † and all remain unconfirmed.*

1. §4.2 FR-4 — The auto-provisioned Space is named from the Account's display name and is immediately renameable.
2. §4.2 FR-7 — Space deletion is immediate and irreversible; no trash, no restore window. †
3. §4.2 FR-8 — An Ownership Offer expires after 7 days, mirroring FR-39. *The not-emailed half of this assumption was retired on 2026-08-20; the recipient is now emailed under FR-43.*
4. §4.3 FR-13 — Admins cannot change each other's Role; only the Owner can promote to or demote from Admin.
5. §4.5 FR-17 — Project deletion is immediate and irreversible. †
6. §4.7 FR-24 — Default Space Status set is Todo / In Progress / Done.
7. §4.7 FR-27 — The rename cascade offer is a single choice applied to every conflicting Project at once.
8. §4.10 FR-37 — API version is selected by URL path segment; exactly two versions are supported concurrently. †
9. §4.11 FR-39 — The Invitation acceptance route expires after 7 days.
10. §4.11 FR-40 — Assignment notification is email, per-event, not digested.
11. §5 NFR-7 — Authorisation refusal records are retained for 90 days. †
12. §6.2 — Backups exist for disaster recovery but are not a restore path for deliberate deletion.
13. §6.3 — The £30/month cost ceiling is set by what the project is worth spending, not by pricing analysis.
