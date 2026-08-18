# Acceptance Criteria

Companion to `SPEC.md` (SPEC-yello). Holds the complete testable consequence list for every capability.

`SPEC.md` states one **decisive** criterion per capability — the one whose failure means the capability failed. This file holds the full set, including that decisive one, so it can be read alone as the acceptance-criteria source for stories and test design. Consequences are written to be *failable*: where one could pass by accident it is restated so it cannot.

`role-capability-matrix.md` is authoritative on Role capability. Where a Role restatement below disagrees with the matrix, the matrix is correct and the restatement is a defect.

---

## Accounts and Authentication — CAP-1 … CAP-3

Global identity, self-service only. Registration is the only path in. Authentication establishes *who* an Account is and nothing more; what it may do is unknown until a Space is established.

### CAP-1 — Register an Account

- An email address that already has an Account cannot be registered a second time.
- Attempting to register an address that already exists produces a response indistinguishable from registering a new address; the existence of an Account is never disclosed to an unauthenticated caller.
- Registration completes with exactly one Space in existence for that Account (CAP-4), and that Space's Owner is the new Account.
- An Account that has not completed registration holds no Membership and appears in no Space.

### CAP-2 — Authenticate and hold a Session

- A Session identifies an Account and carries no Role, no Space and no capability of its own.
- The same Session is used unchanged when the acting Account switches Space; switching Space never re-authenticates.
- A Session that has expired or been invalidated grants access to nothing, including to Spaces the Account still holds Membership in.
- Failed authentication does not reveal whether the failure was an unknown address or a wrong password.

### CAP-3 — Delete an Account

- Deletion is refused while the Account is the Owner of any Space; each such Space must first be transferred away (offered under CAP-8 and accepted under CAP-42) or deleted (CAP-7). Because a transfer now requires someone else to accept, **deleting the Space is the only exit the Account controls unilaterally** — an Owner who can find no willing recipient can still always leave, at that cost.
- On deletion, every remaining Membership held by the Account is removed, and the Account disappears from every Space it belonged to.
- Tasks the deleted Account was Assignee of become unassigned; the Tasks themselves survive.
- Content authored by the deleted Account in Spaces it did not own is retained; attribution renders as a deleted Account rather than disappearing.
- After deletion, the email address can be used to register a new Account, and that new Account inherits no Membership, no Space and no history.

**Feature-specific:** passwords are never stored recoverably, and never appear in any log, error message or API response.

---

## Spaces — CAP-4 … CAP-9

The same object serves as private notebook, client engagement and whole-company workspace; Yello draws no distinction. Exactly one Owner at all times is an invariant, not a default.

### CAP-4 — Provision a Personal Space on registration

- The Space exists and is usable at the moment registration completes; the Account never encounters a state in which it belongs to no Space.
- The provisioned Space is an ordinary Space: it can be renamed, shared, transferred and deleted on the same terms as any other. **No attribute distinguishes it from a Space created by CAP-5.**
- *Assumed:* it is named from the Account's display name (e.g. "Ravi's Space") and is renameable immediately.

### CAP-5 — Create a Space

- The creating Account becomes the Owner of the new Space.
- A newly created Space has exactly one Membership.
- A newly created Space carries the default Status set (CAP-24) and no Projects.
- Creating a Space has no effect on, and no visibility from, any other Space.

### CAP-6 — Rename a Space

- Members and Viewers cannot rename a Space.
- Space names are not unique across Yello; two Accounts may each own a Space with the same name without collision or disclosure.

### CAP-7 — Delete a Space

- Only the Owner can delete a Space. Admins cannot.
- Deletion removes every Membership in that Space; other Accounts lose access without losing their own Spaces or their Account.
- Deletion invalidates every API Token issued for that Space (CAP-36).
- An Account may delete its last remaining Space. **Belonging to no Space is a valid state:** the Account persists and is offered the chance to create a Space rather than having one created for it. No Space is undeletable.
- That deletion destroys the Space's contents for every Member is stated **at the point of the action**, not only in documentation.
- *Assumed:* deletion is immediate and irreversible in v1 — no trash, no restore window. This is the most destructive operation in the product and the only one with no undo.

### CAP-8 — Offer to transfer ownership

- Ownership can only be offered to an existing Membership; not to an email address, an Invitation, or an Account with no Membership in that Space. Any Role may be named.
- **Making the offer does not move ownership.** The offering Owner remains the Owner, with every capability of the Role, until the offer is accepted.
- At most one Ownership Offer is pending per Space at a time.
- The offering Owner may revoke a pending offer, and revocation leaves every Membership and Role exactly as it was.
- A pending offer lapses if the named recipient's Membership is removed (CAP-14) or their Account is deleted (CAP-3), and lapsing changes no Role.
- The offering Owner's Membership still cannot be removed by CAP-14, and their Account deletion is still refused by CAP-3, while an offer is pending — making an offer is not itself an exit.
- *Assumed:* the offer expires after 7 days, mirroring the Invitation, and is surfaced in Space settings rather than emailed.

### CAP-42 — Accept or decline an Ownership Offer

*No PRD counterpart. Added to close a defect found while resolving this spec's open questions: with an immediate unilateral transfer, one Account could make another the unremovable Owner of a Space and thereby block that Account's own deletion indefinitely.*

- Only the single Membership the offer names can accept or decline it, and only while it is pending.
- **On acceptance, ownership moves in one atomic step:** the recipient becomes the sole Owner and the previous Owner becomes an Admin without losing access. At no point does the Space have zero Owners or two.
- **No Account becomes an Owner without having agreed to it.** There is no route, API included, by which ownership arrives unrequested.
- Declining leaves every Membership and Role exactly as it was, and the offering Owner is told it was declined.
- A declined or lapsed offer cannot afterwards be accepted; the Owner must make a new offer.
- After acceptance the new Owner is subject to every rule that binds any Owner: their Membership cannot be removed while they hold ownership, and their Account deletion is refused until they transfer the Space onward or delete it.

### CAP-9 — Establish and switch Space context

- Only Spaces the acting Account holds a Membership in are listed. No Space is discoverable by any other means — no directory, no search, no enumeration reaching beyond Membership.
- Switching Space changes the acting Role to whatever that Account holds in the destination Space, with no carry-over from the previous Space.
- Every request that reads or writes Space-scoped data resolves an active Space **before** authorisation is evaluated; a request with no resolvable Space context is refused rather than defaulted.

---

## Membership and Invitations — CAP-10 … CAP-14

Membership is the only mechanism by which an Account gains access to a Space. Invitations address an email address rather than an Account, so a person can be invited before they have heard of Yello, and an Account is never exposed to the inviter as a side effect of being invited.

### CAP-10 — Issue an Invitation

- Members and Viewers cannot issue Invitations.
- Any email address can be invited. No domain restriction, no allowlist, no requirement that the address already have an Account.
- The Invitation carries exactly one Space and exactly one Role, fixed at issue time.
- An Invitation cannot be issued at Owner Role; ownership moves only by an Ownership Offer accepted under CAP-42.
- Issuing an Invitation to an address that already holds Membership in that Space is refused.
- The response to the issuer is identical whether or not the address corresponds to an existing Account; issuing an Invitation never discloses whether someone uses Yello.
- **The Invitation record retains its terminal state once it leaves pending** — accepted, revoked or expired — rather than being deleted. No product surface reads it; the operator does, and SM-4 and SM-C3 are underivable without it.

### CAP-11 — Accept an Invitation

- Accepting creates exactly one Membership, in exactly the invited Space, at exactly the invited Role.
- **The Invitation token identifies the offer; it never authorises acceptance.** Acceptance requires the invitee authenticated as the Account the Invitation addresses. A bare fetch of the acceptance route — by a mail security scanner, a link prefetcher, or anyone the mail was forwarded to — creates no Membership and changes nothing.
- **Acceptance is a deliberate act, taken after the Space and the Role have been shown.** For an invitee with no Account, completing registration is that act; for an invitee who already has one, it is an explicit confirmation. Neither is satisfied by loading a URL.
- An invitee without an Account registers as part of accepting, and that registration provisions their own Personal Space (CAP-4) independently of the Space they were invited to.
- An invitee with an existing Account joins with that Account; no second Account is created, and their other Memberships are neither visible to nor affected by the inviter.
- An Invitation can be accepted once. A second attempt with the same Invitation is refused.
- A revoked Invitation (CAP-12) reports only that it is no longer valid — it discloses neither the Space's name or contents, nor who revoked it.

### CAP-12 — Revoke a pending Invitation

- A revoked Invitation cannot subsequently be accepted.
- Revocation is possible only before acceptance; removing an accepted invitee is CAP-14, not CAP-12.
- An Invitation remains valid if its issuer later loses the capability to have issued it — by demotion, by removal, or by Account deletion. The Invitation was legitimately issued and does not depend on its issuer's continuing authority. Any remaining Owner or Admin may revoke it.

### CAP-13 — Change a Membership's Role

- An Admin can change Memberships between Member and Viewer only.
- Only an Owner can promote a Membership to Admin, or demote one from Admin. *Assumed: Admins cannot modify each other — asserted to keep the Owner meaningfully distinct from Admin.*
- No Role change can produce a second Owner or remove the sole Owner; ownership moves only by an Ownership Offer accepted under CAP-42.
- A Role change takes effect on the target's active Sessions without requiring them to re-authenticate (CAP-34).

### CAP-14 — Remove a Membership, or leave a Space

- An Admin can remove Members and Viewers, and cannot remove the Owner or another Admin.
- The Owner's Membership cannot be removed by anyone, including the Owner, while it holds ownership; the Owner leaves by offering ownership (CAP-8) and having it accepted (CAP-42), or by deleting the Space (CAP-7). A pending offer is not an exit.
- Removal revokes access immediately and takes effect on the removed Account's active Sessions and open editors (CAP-34).
- Removal invalidates every API Token that Account holds for that Space (CAP-36).
- Tasks the removed Account was Assignee of become unassigned; the Tasks survive.
- A removed Account retains its own Spaces, its Account, and every other Membership.

---

## Access Control — CAP-15, CAP-16

Authorisation is a function of `(Account, Space)`, never of Account alone. No capability can be described, specified or tested without naming the Space it applies in.

### CAP-15 — Enforce Space-scoped authorisation

- No request returns data from a Space in which the acting Account holds no Membership — including by direct identifier, by deep link, by search, by API, and by any listing or aggregate.
- Requesting a resource that exists in a Space the caller has no Membership in is **indistinguishable** from requesting one that does not exist; existence is never disclosed across a Space boundary.
- Requesting a resource in a Space the caller *does* belong to, but lacks the Role for, reports a **permission failure rather than a not-found** — the distinction is drawn at the Space boundary, not below it.
- An Owner of one Space has no elevated standing in any other Space, regardless of Membership.
- Authorisation is evaluated per request; it is never cached across a Space switch or inferred from a prior request.
- **The message shown for a not-found refusal names both possibilities and commits to neither** — "this isn't available to you; it may not exist, or you may not have access." Ambiguous copy carries the usability cost so that no disclosure has to. A Role refusal *inside* a Space the caller does belong to may be specific, because no Space boundary is being crossed there.
- **A failed deep link returns the caller to a Space they do hold Membership in** rather than leaving them on a dead surface (UJ-4). Where the caller holds no Membership anywhere, they are offered the chance to create a Space, consistent with CAP-7.

### CAP-16 — Apply the Role capability matrix

See `role-capability-matrix.md` for the matrix itself and its consequences.

---

## Projects — CAP-17, CAP-18

A Project groups work; it does not control access. Projects have no Membership and no permissions of their own. A Project never moves between Spaces, which closes a cross-Space leak by construction rather than by rule.

### CAP-17 — Create, rename and delete a Project

- A Viewer can do none of these.
- A Project belongs to exactly one Space, fixed at creation. No operation moves a Project to another Space.
- A new Project's effective Status set is the Space default set (CAP-24) with an empty delta.
- Deleting a Project deletes its Tasks. *Assumed: immediate and irreversible in v1, consistent with CAP-7.*

### CAP-18 — List Projects in a Space

- The listing contains every Project in the active Space and no Project from any other Space.
- Viewers see the same Projects as Members; Role affects what can be done to a Project, not whether it is visible.

---

## Tasks — CAP-19 … CAP-23, CAP-41

Assignment is constrained to the Space, so it can never be used to reach across a boundary or to imply the existence of an Account outside it.

### CAP-19 — Create a Task

- A Viewer cannot create Tasks.
- A new Task takes the first Status in its Project's effective Status set.
- A Task belongs to exactly one Project at any moment, and to exactly one Space permanently. It may later be moved to another Project in the same Space (CAP-41); no operation moves it to another Space.

### CAP-20 — Edit Task attributes

Covers title, Status, due date and Labels. Description editing is CAP-31.

- A Viewer cannot change any attribute.
- A Task's Status can only be set to a value in its Project's effective Status set; a Status valid in a sibling Project is refused.

### CAP-21 — Assign a Task

- Only Memberships of the Task's Space are offered as Assignees, and only those are accepted.
- An Account with no Membership in the Space cannot be set as Assignee by any route, including the API.
- When the Assignee's Membership is removed (CAP-14) or their Account deleted (CAP-3), the Task becomes unassigned and is not deleted.
- Assigning a Task to a Viewer is permitted, and does not grant the Viewer any write capability over that Task. Responsibility and capability are deliberately separable: a client contact can be marked responsible for something they cannot change.
- Demoting an Assignee to Viewer does not unassign their Tasks.

### CAP-22 — Manage Labels

- Labels are defined per Space and are available to every Project in it.
- A Label applied to Tasks cannot be deleted without those applications being removed; no Task references a Label that does not exist.

### CAP-23 — Delete a Task

- A Viewer cannot delete Tasks.
- Deleting a Task terminates any active collaborative editing session on it (CAP-31); participants are told it was deleted rather than losing their connection silently.

### CAP-41 — Move a Task to another Project

- A Viewer cannot move Tasks.
- Only Projects in the Task's own Space are offered as destinations, and only those are accepted — including via the API.
- If the Task's Status does not exist in the destination Project's effective Status set, a destination Status must be supplied as part of the move, on the same terms as CAP-26. **The move does not take effect without one.**
- If the Task's Status does exist in the destination Project's effective set, the Status is preserved and no mapping is required.
- Assignee, Labels, due date and description survive the move unchanged, because both Projects share a Space and therefore share its Memberships and Labels.
- An active collaborative editing session on the Task continues across the move; participants are not disconnected.

**Bulk form.**

- **Every Task currently in one Status can be moved from one Project to another in the same Space in a single operation.** Because the selection shares a Status by construction, the operation carries **exactly one** mapping decision, on the same terms as the single-Task form: the Status is preserved where the destination exposes it, and one destination Status is required where it does not.
- The bulk form is reachable from a Board column and from a List View filtered to one Status, and is available on the API on the same terms as the single-Task form (CAP-35).
- **A bulk move is atomic: every selected Task moves, or none does.** A bulk move that cannot complete is refused rather than partially applied, consistent with CAP-26, and the refusal is visible rather than silent.
- A Viewer cannot use either form.
- Active collaborative editing sessions on the moved Tasks continue across a bulk move, as with the single-Task form.
- **This is the safe path for retiring a Project without losing its work** — one move per Status in its effective set, rather than one per Task, against a Project deletion that is irreversible (CAP-17).

---

## Status Configuration — CAP-24 … CAP-27

A Project's effective set is the Space defaults with its delta applied. There is no "inheriting versus overriding" mode and no operation reverting a Project to the defaults — a Project's Status set is simply editable for as long as the Project exists. The rule that makes this safe is universal: **removing a Status, at either level, requires mapping the Tasks that occupy it.** Removal is a migration, always.

### CAP-24 — Define Space default Statuses

- Members and Viewers cannot change the Space default Status set.
- A newly created Space has a non-empty default set. *Assumed: Todo / In Progress / Done.*
- The default set can never be empty; removing the last Status is refused.

### CAP-25 — Apply a Project Status delta

Delta operations are add, remove, rename and reorder.

- Members and Viewers cannot change a Project's Status delta.
- A Project's effective Status set is deterministic: the Space defaults with that Project's delta applied, in the delta's order.
- A Project's effective set can never be empty.
- Two Projects in the same Space may hold different effective sets simultaneously, and a Status valid in one is not accepted for a Task in the other (CAP-20).
- A Status removed from a Project can be added back to that Project at any time.

### CAP-26 — Map Tasks when a Status is removed

- The removal does not take effect unless a destination Status is supplied for every occupying Task; there is no partial application.
- No Task is ever left holding a Status absent from its Project's effective set — before, during, or after the operation.
- Removing a Status that no Task occupies requires no mapping and succeeds directly.
- The destination must exist in the effective set that will be in force **after** the removal completes; a Task cannot be mapped onto a Status that the same operation also removes.

### CAP-27 — Propagate Space-level Status changes

- Adding a Status at Space level adds it to every Project that has not removed it.
- Renaming a Status at Space level renames it in every Project that has not itself renamed it, including Projects that have reordered it.
- Where one or more Projects have themselves renamed that Status, the operation reports the conflict and offers to cascade. Cascading replaces those Projects' names; declining preserves them. The Space-level rename applies either way to non-conflicting Projects.
- Removing a Status at Space level requires mapping under CAP-26. A single destination Status is chosen once and applied across every affected Project that can accept it.
- Removing a Status at Space level has no effect on Projects that had already removed it.
- *Assumed:* the cascade offer is a single choice applied to every conflicting Project at once, consistent with the single Space-wide mapping decision.
- **Where a Project's post-removal effective set does not contain the chosen destination, the operation reports that Project — and how many of its Tasks are affected — and requires a destination drawn from that Project's own post-removal effective set.** There is no fallback and no silent placement.
- Nothing applies until every reported Project has a destination. The Space-level removal, the Space-wide mapping and every per-Project exception apply as **one transaction or not at all**.
- This is always satisfiable: a Project's effective set can never be empty (CAP-25), so a valid destination exists in every affected Project.
- **Both halves of this capability behave the same way on conflict** — rename reports and asks, removal reports and asks. Neither decides for the Admin.

**Constrains the data model:** because a Space-level rename must detect that a Project renamed *the same* Status in order to offer the cascade, a Project's delta necessarily references Statuses by **identity rather than by name**.

---

## Board and List Views — CAP-28 … CAP-30

Both views are read-available to every Role; only the manipulation differs.

### CAP-28 — View a Project as a Board

- Columns appear in the Project's effective order, including where a delta reordered them.
- Every Task in the Project appears in exactly one column.
- A Viewer sees the identical Board to a Member, with no manipulation affordances present.
- **At the NFR-8 bound of 5,000 Tasks in a Project, the Board still satisfies NFR-5 and NFR-9.** Every Task remains reachable and appears in exactly one column. How the view achieves that at that size — paging, virtualisation, or something else — is the architecture's call; nothing in v1 currently provides it, and the three requirements cannot all hold naively.

### CAP-29 — Move and order Tasks on a Board

- A Viewer cannot move or reorder.
- Moving a Task to a column sets its Status to that column's Status.
- Concurrent moves by two Users converge to one order that both observe; neither User's move is silently discarded.
- Ordering is per column and survives reload.

### CAP-30 — View a Project as a filterable list

- Filters never surface a Task from another Project or another Space.
- Filtering by Assignee offers only Memberships of the active Space.
- **At the NFR-8 bound of 5,000 Tasks in a Project, the List View still satisfies NFR-5 and NFR-9**, on the same terms as CAP-28.

---

## Collaborative Task Editing — CAP-31 … CAP-34

Nobody is presented with a conflict dialog, a lock, or a "someone else changed this" warning: concurrent editing is the normal case, not an error to be reported. The load-bearing requirement here is **not** text convergence but what happens when permission changes while an editing session is open — CAP-34.

### CAP-31 — Edit a Task description concurrently

- Two Users editing different parts of the same description simultaneously both retain their changes; neither is overwritten.
- Two Users editing the same region simultaneously arrive at an identical final text, and that text is the one persisted.
- No participant is shown a merge prompt, a lock, or a stale-content warning during normal concurrent editing.
- A Viewer cannot enter an editing session at all, and cannot do so via the API.

### CAP-32 — Show Presence

- Presence shows only Memberships of the same Space.
- Presence disappears within the interval in NFR-3 after a participant disconnects, and does so without their action.
- Presence never reveals an Account's activity in any other Space.

### CAP-33 — Reconcile after disconnection

- Changes made while disconnected are applied on reconnection, and appear **exactly once**.
- Changes made by others during the disconnection are present after reconnection.
- If reconciliation cannot complete, the User is told explicitly and their unsynchronised text is not silently discarded.

### CAP-34 — Apply permission changes to live sessions

- On removal from the Space, the participant's editing session terminates, their unsynchronised local changes are **not** applied, and they are told their access has ended.
- On demotion to Viewer, their editing capability ends while read access continues uninterrupted; unsynchronised changes made before the demotion are not applied.
- No change authored after the moment of removal or demotion reaches the Space by any route, including a delayed or retried synchronisation.
- Changes the participant had already synchronised before the change took effect are retained; revocation stops future writes and does not roll back past ones.
- The effect is observable **without the affected participant taking any action.**

> This is the acceptance criterion the product should be judged on. If everything else works and this does not, the isolation model is decorative.

---

## Public API — CAP-35 … CAP-38

Everything a person can do in the browser against a Space, a script can do too, under the same authorisation rules evaluated the same way — with one stated exception, Board ordering. An API Token is bound to exactly one Space, so the isolation invariant holds identically on both surfaces and the API cannot become a route around it.

### CAP-35 — Expose Spaces, Projects and Tasks over the API

- Every capability in `role-capability-matrix.md` is enforced identically on the API; no operation refused in the browser succeeds via the API.
- The API exposes no operation that enumerates Spaces, Accounts or Memberships beyond the Token's Space.
- A Task's position within its Status (CAP-29) is **readable** over the API so a consumer can reproduce what a user sees. It is **not writable**: reordering happens only through the interface, which keeps CAP-29's convergence requirement confined to one surface. This is the single place the API is deliberately narrower than the browser, and it is a decision rather than an oversight.

### CAP-36 — Issue and scope an API Token

- A Token is bound to exactly one Space at issue time and that binding cannot be changed.
- A Token issued by an Account that owns several Spaces reaches only the Space it names, including Spaces the same Account owns.
- A Token's effective capability is the issuing Account's Role in that Space **at the time each request is evaluated**, not at the time of issue — a Token issued as a Member loses write capability when its Account is demoted to Viewer.
- A Token is invalidated when its Account's Membership is removed (CAP-14), when the Space is deleted (CAP-7), or when the Account is deleted (CAP-3).
- A Token is displayed once at creation and is not retrievable afterwards.

### CAP-37 — Version the API and deprecate predictably

- A request that names a supported version receives that version's response shape, regardless of what other versions exist.
- No change within a version removes a field, renames a field, changes a field's type, or narrows accepted input.
- A version is announced as deprecated before it stops working, and continues to serve requests throughout the announced period.
- *Assumed:* version is selected by URL path segment, and exactly two versions are supported concurrently.

### CAP-38 — Rate limit API requests

- Exceeding the limit produces a distinct, documented refusal that a client can detect and act on, and states when the caller may retry.
- Rate limiting is applied per Token, so one Space's consumption cannot exhaust another's.
- Rate limiting never causes a write to be applied more than once when a client retries.

---

## Notifications — CAP-39, CAP-40

Email only where an action outside the product is required, or where someone needs to know something happened while they were not looking. Invitation delivery is load-bearing: without it, CAP-11 has no entry point for an invitee who has never used Yello.

### CAP-39 — Deliver an Invitation by email

- The email names the Space and the Role offered, and identifies who issued it.
- The email discloses nothing about the Space's contents, its other Members, or any other Space.
- Following the acceptance route after revocation reports only that the Invitation is no longer valid (CAP-11).
- Following the acceptance route does not by itself join the invitee. It presents the offer; acceptance is separate and requires authentication as the invited Account (CAP-11).
- *Assumed:* the acceptance route expires after 7 days, after which the Invitation must be reissued.

### CAP-40 — Notify on assignment

- The notification names the Space, Project and Task, and nothing from any other Space.
- An Account is not notified of its own action.
- *Assumed:* assignment notification is email, and is per-event rather than digested. Frequency control is a v2 concern.

**Feature-specific:** a record that a notification was sent is retained — Space, kind and timestamp, never message content or recipient address — so notification volume (SM-C4) is derivable. No product surface reads it.
