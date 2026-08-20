---
name: Yello
status: final
updated: 2026-08-20
sources:
  - _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md
  - _bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md
---

# Yello — Experience Spine

> Information architecture, behaviour, states, interactions, accessibility and journeys. Visual identity lives in `DESIGN.md`; tokens are referenced here by name as `{path.to.token}`. Both spines win on conflict with any mock, wireframe or import.

**Vocabulary is PRD §2, verbatim** — including **Account**, **User**, **Space**, **Personal Space**, **Membership**, **Role**, **Owner**, **Admin**, **Member**, **Viewer**, **Invitation**, **Ownership Offer**, **Project**, **Task**, **Status**, **Assignee**, **Label**, **Board**, **List View**, **Presence**, **API Token**, **Session**. Synonyms are a discipline violation. Three usages to watch:

- **Board** is a *view* of a Project, never a container.
- **User** is an Account acting in the context of a specific Space; where no Space is established the correct term is **Account**.
- Each glossary term is translated **once** per language and used consistently. §2's no-synonyms rule applies per language, and a translator who renders "Space" three ways breaks the product's central concept.

## Foundation

Single-surface **responsive web**. Blazor WebAssembly on .NET 10, served from Azure Static Web Apps against an API and a `/sync` WebSocket on Azure Container Apps. There is no native client and none is planned (§9.2). No UI system is inherited, so every component named below has to be built.

Three substrate facts shape the experience more than any design choice:

- **AD-11** — the client edits a local replica and never blocks on the network. Edits land instantly because they *are* instant; the server admits or rejects afterwards, and a rejected change is reverted in the replica. **There is no save button anywhere in Yello.**
- **AD-1 / AD-2** — authorisation is a function of `(Account, Space)`, resolved per request from a Membership row. Nothing is visible or permitted until an active Space is established, which makes the context bar a functional mechanism rather than navigation chrome.
- **AD-8 / AD-9** — a permission change is *pushed* to open sessions at the transaction boundary, never polled. Revocation happens *to* the User without them acting, so **the interface has to be built to be interrupted** — and every surface, not only the Task editor.

### Open against this spine

Three items are not settled. They are listed here rather than only at their point of use, because a consumer building from this contract needs to know what it does *not* answer before they rely on it.

| Item | Owner | Where |
|---|---|---|
| **The 403/404 timing oracle.** AD-20 writes an `AccessRefusal` row for a Space-boundary 404 and not for an ordinary not-found, so the two are distinguishable by duration however identical the copy. AD-23 already makes duration contractual for registration; nothing does so here. | `bmad-architecture` | § Isolation and Refusal |
| **The FR-28 × NFR-5 × NFR-9 collision.** The PRD defers the mechanism to the architecture and says the three cannot all hold naively; the 28 ADs never choose paging, virtualisation or windowing; this spine states the contract any mechanism must satisfy but cannot pick one. | `bmad-architecture` | § Responsive & Platform |
| **Emailing the Ownership Offer.** FR-8's assumption says the offer is surfaced in-Space and *not* emailed, and §4.11 has no ownership notification — which traps an Owner indefinitely if the recipient never logs in. UJ-8 is written against the amended behaviour. | `bmad-prd` | UJ-8 |

Five `[ASSUMPTION]` tags are also live, each stating its own reasoning: no webfont, no density control, breakpoints at 768/1280, FR-6's inherited surface gap, and accepted spellcheck egress. None blocks implementation.

## Information Architecture

| Surface | Reached from | Purpose | Roles |
|---|---|---|---|
| Unauthenticated | Direct, or an Invitation link | Register, sign in, view an Invitation | — |
| **Space context bar** | Always present once authenticated | Names the active Space, shows the acting Role, switches Space, and carries a pending Ownership Offer indicator **for the active Space** | All |
| Space home | Context bar / after a switch | The Projects in the active Space | All |
| Project — Board | Space home → Project | Tasks in columns by Status. The default and primary working surface | All (read); manipulation by Role |
| Project — List View | Board → view toggle | The same Tasks as filterable, sortable rows | All (read) |
| Task detail | A Task on the Board or List View | Attributes, collaborative description, Presence | All (read) |
| Space settings | Context bar | Memberships and Invitations, **Space name (FR-6)**, default Statuses, Labels, ownership, deletion | Owner, Admin |
| Project settings | Project → settings | This Project's Status delta | Owner, Admin |
| Account settings | Context bar → Account | Profile, password, API Tokens, Account deletion | Self |

**Only two surfaces are Account-scoped** — the Space switcher inside the context bar, and Account settings (AD-24). Everything else is Space-scoped. Adding a third requires amending that AD.

> **PRD §7 says Account settings is *"the only surface"* that spans Spaces; AD-24 says two do.** AD-24 is later and more specific, and it names the Space switcher explicitly, so it governs. Recorded rather than silently resolved, for the same reason the FR-8 email divergence is recorded at UJ-8.

**Renaming a Space (FR-6) has no home in PRD §7's surface list.** It belongs in Space settings alongside the other Owner-and-Admin capabilities and is placed there above. `[ASSUMPTION: inherited gap, not introduced here — §7 omits it too, so a downstream epic for FR-6 would otherwise have no surface to attach to.]`

**There is no global home, no cross-Space inbox, no notification centre and no "all my Tasks" view.** §8 does not defer these — it rules them out, because a surface spanning Spaces contradicts the model the product is built on. There is also no search reaching beyond a Project in v1 (§9.2).

The Space switcher may return **Space identity only** — id and name. No count, no Role, no Project, no Task, no Membership crosses a Space boundary through it (AD-24). A switcher that badges a Space with "3 Tasks" or "offer pending" breaches NFR-1.

Modals go **one level deep, never two**. A destructive confirm invoked from inside a panel therefore **replaces that panel's content in place** rather than stacking on it.

→ **Composition reference.** [`mockups/board.html`](mockups/board.html) — the Board at rest, the Move control, mid-drag, and the same Board as a Viewer. [`mockups/ownership-offer.html`](mockups/ownership-offer.html) — the context-bar indicator, the decision dialog, the no-longer-open state. [`mockups/task-detail.html`](mockups/task-detail.html) — concurrent editing and all three FR-34 interruptions. [`mockups/registration.html`](mockups/registration.html) — UJ-1's path and its empty states. [`mockups/project-settings.html`](mockups/project-settings.html) — the Status delta editor and **FR-27's report-and-ask cascade**, both halves. [`mockups/space-settings.html`](mockups/space-settings.html) — Memberships, Invitations, the Ownership panel and the delete confirm, shown as Owner *and* as Admin so every difference reads as an absence. [`mockups/invitation.html`](mockups/invitation.html) — the side-effect-free read, the five-states-one-response refusal, and the wrong-Account case.
>
> **Only two IA surfaces are spine-only:** the List View and Account settings. Both are deliberate — the List View is a filterable table whose paging, keyboard traversal and at-scale contract are fully stated below, and Account settings is a settings list whose one interesting state (an API Token shown exactly once) is likewise stated. Neither has layout that drives behaviour.
>
> **These spines win on conflict with any mock.** The mockups illustrate composition; they are not the contract, and they were rendered before this pass's border, type-scale and `readonly` corrections. The exploratory artefacts in `.working/` record *how* the palette and direction were chosen and are older still.

## Space Context and Role Legibility

*Invented section. This is the product's central bet and no standard section owns it.*

The same Account is Owner in one Space, Admin in another and Viewer in a third, and Yello treats that as ordinary. Three rules carry it.

**1. The acting Role is legible at all times.** `{components.role-chip}` sits permanently in the context bar — never on hover, never behind a menu. It reads one of Owner / Admin / Member / Viewer verbatim.

**2. The context bar's accessible name states the Role *and what it permits*.** Not merely `"Northwind Redesign, Admin"` but `"Northwind Redesign — Admin, can manage Members and settings"`; for a Viewer, `"— Viewer, read only in this Space"`. This is the one place the interface explains a Role limit in prose, and it exists because of rule 3: with every write affordance removed, the surface that would explain *why* is unreachable from the browser by exactly the person who wants it. Stating it on arrival beats discovering it on failure. It discloses nothing cross-Space and does not make the chip interactive.

**3. Capabilities a Role lacks are absent, not disabled.** UJ-4's climax is exact: *"in the third Space every affordance to create or edit is absent — not present-and-failing, absent — so he can tell his standing from the interface without attempting an action."* A Viewer's Board has no create affordance, no Move control, no drag handle, no editor. Not greyed, not tooltipped. **Gone.** There is deliberately **no disabled state in Yello for Role reasons**; if you find yourself designing one, the answer is removal.

FR-16 draws the complementary line, and it must not be misread as licence: *"A Viewer's write attempt is refused at the API regardless of what the interface offered; the interface hiding an action is never the mechanism that enforces it."* Absence is an honesty contract with the User, not a security control.

**Absence is a steady state, not a transition.** When a Role *drops* while someone is looking at a surface, removing the affordances silently is hostile — the User was mid-action and, by the rule's own design, no residual control is left to explain the disappearance. So a Role change always **narrates the removal first** and settles into the absent steady state second. See State Patterns.

Switching Space **changes the entire working surface** and carries nothing over — no Role, no filter, no scroll position, no open Task, and no data (see *The client replica is in scope for isolation*). AD-1 forbids authorisation inferred from a prior request, and the interface must not imply a continuity the authorisation model does not have.

**Each browser tab holds its own Space context, and tabs share no state.** One Account may legitimately have two tabs open on two different Spaces. That is not a cross-Space view, but it is a state the product must not get wrong — see the multi-tab rule under Accessibility Floor.

## Voice and Tone

Microcopy. Brand posture lives in `DESIGN.md` § Brand & Style.

**Terse and factual.** Short declaratives. No hedging, no apology, no exclamation marks, no encouragement, no emoji. The reader is competent and busy, and SM-C2 makes their fast exit a success rather than a churn signal.

| Do | Don't |
|---|---|
| "Access ended." | "Oops! It looks like you no longer have access to this Space." |
| "Not available." *(a cross-Space miss or a non-existent resource)* | Any wording that distinguishes those two cases |
| "Viewers cannot edit Tasks." *(a within-Space Role refusal)* | "You don't have permission to view this Task." — object-shaped, and it leaks that the object exists |
| "This cannot be undone." | "Are you sure? This action is permanent!" |
| "Nothing here yet." | "This Project is empty — let's add your first Task! 🎉" |
| "Moving 4,812 Tasks." | "Please wait while we process your request…" |
| "Disconnected. Your changes are not yet sent." | "Connection lost. Retrying…" — and never "held" or "saved", which promise application |
| "You've been offered ownership of Northwind Redesign." | "Congratulations! You've been chosen as the new Owner!" |
| "This offer is no longer open." | "Sorry, that offer has expired or been withdrawn." |
| Name the object: "Delete Northwind Redesign?" | "Delete this item?" |
| One sentence of fact, then the action | A sentence of reassurance before the fact |

**Refusal copy is capability-shaped, not object-shaped.** *"Viewers cannot edit Tasks."* is safe, reusable and cheap to translate. *"You cannot edit this Task"* confirms the Task exists — harmless for a within-Space 403, which AD-3 only ever shows to someone who already holds a Membership, but the habit is what matters and the capability form is better copy anyway.

**Two places terseness is deliberately overridden**, because the stakes are asymmetric:

- **Accepting an Ownership Offer** gets full explanation. FR-42 binds the new Owner immediately: their Membership cannot be removed while they hold ownership, and their Account deletion is refused until they transfer the Space onward or delete it. Nobody should agree to that from a one-line dialog.
- **Deleting a Space** names what goes and states that other Accounts lose access. §6.2 requires this *"stated at the point of the action"*, and it is the only operation with no undo at all.

**Never use the word "archive".** Yello has no archive, and borrowing the word from the products users arrive from would promise a safety net that does not exist.

## Isolation and Refusal

*Invented section. The 403/404 line is a copy problem with a security consequence, and neither Voice nor State Patterns can hold it alone.*

AD-3 draws the line at the Space boundary and nowhere else:

| Situation | Server | What the User sees |
|---|---|---|
| Resource in a Space the Account has **no Membership** in | **404** | "Not available." |
| Resource that genuinely **does not exist** | **404** | "Not available." — identical to the above |
| Resource in a Space the Account **does** belong to, refused for Role | **403** | A capability statement: "Viewers cannot edit Tasks." |

**The two 404 cases must be indistinguishable in every respect the interface controls** — same words, same layout, same focus behaviour, same page-not-toast treatment, and no client-side branch on which case occurred. UJ-4 accepts the usability cost explicitly: it is *"paid with deliberately ambiguous copy rather than with a disclosure."*

> **Timing is not one of those respects, and that is an escalation rather than an omission.** AD-20 has the pipeline write an `AccessRefusal` row for every **Space-boundary** 404, while a genuinely non-existent resource crosses no boundary and so plausibly writes nothing. Different work means different duration, which makes the pair a timing oracle no amount of identical copy can close: probe an id, time the refusal, learn whether it exists somewhere in Yello. AD-23 already makes duration part of the contract for registration and authentication; the same discipline is owed here and no AD states it. **Flagged for `bmad-architecture`** — the fix is in the pipeline (write the refusal record off the response path, or pad boundary refusals to a floor), and the isolation suite needs a *timing* case, since SM-1 currently measures disclosure rather than duration.

Hard rules:

- The 404 body names **no** Space, Project or Task title, and no id that was not already in the URL.
- It is **not** a toast. A toast implies a transient fault; this is a final answer.
- The route back goes to a Space from the switcher — never a remembered last-Space, which may itself now be unavailable.
- **A boundary 404 in response to an optimistic write collapses to the same terminal treatment.** Revert the replica, then render the full refusal surface. Not a silent revert, which would breach FR-34's observability, and not a toast, which this section forbids.
- **Nothing is prefetched across a Space boundary**, and **the client never issues a Space-scoped request for a Space id absent from the current switcher response.** The second rule closes two holes at once — warm-starting a remembered last-Space, and following a deep link after removal. It also protects the audit trail: every such request produces an `AccessRefusal` classified `CrossSpace` (AD-20), and a client that manufactures those for ordinary users buries real probing in its own noise, defeating NFR-7's whole purpose.
- Error text is never templated with server-supplied prose. AD-3 forbids existence hints in error bodies; the client owns the string.

### Email addresses

§6.1 confines email addresses to **Owners and Admins of a Space the Account is a Member of**, and to nobody else. This needs stating because the mechanism that breaks it is mundane and near-certain: 20px monospace initials and a display name do not disambiguate two people, and the field every implementer reaches for to disambiguate a Membership picker is the address.

- **No surface outside Space settings' Membership management renders an email address.** Not the Assignee picker, not Presence, not an avatar tooltip, not attribution, not a Presence announcement.
- Everywhere else a Membership is identified by **display name and initials only**.
- Two identical display names are disambiguated by a **Membership-scoped discriminator** — never the address.
- **Attribution renders the name captured at authoring time, or on the Membership row — never a live global Account lookup.** FR-14 keeps a removed Account's authored description changes in the Space, correctly; but resolving their *current* name globally would propagate a later name change into a Space they no longer share with the person reading it, which is a live read of Account state by people with no shared Membership.

### The client replica is in scope for isolation

AD-2 enforces Space scoping *in the database*; AD-11 has the client holding a **local replica** it edits without blocking on the network. Together they leave a gap the release gate cannot see: SM-1's suite exercises *requests*, and a leak rendered from client memory issues no request.

The client is therefore bound too, and these are behavioural requirements:

- **Space switch, sign-out, Account switch, lease invalidation (`MembershipChanged`), and back/forward navigation each purge everything Space-scoped for the departed Space — synchronously, before any render.** Replica, projection, cached Board, queued inbound frames, pending announcements, Assignee and Label lists, Status sets, filter and sort state, scroll position, and any id-keyed cache. *"Switching Space carries nothing over"* is a **data** rule, not a presentation one.
- **No Space-scoped content is written to `localStorage`, `sessionStorage` or IndexedDB.** AD-7 forbids credentials there; this extends it to content. It also stops browser session-restore repopulating a Space after access ended, and stops one Account's Task text surviving sign-out on a shared machine. Any durable buffer FR-33's five-minute window requires is scoped to one Space, keyed to the Session, and destroyed by the same triggers.
- **Optimistic rendering never precedes authorisation on the first read of a Space.** Optimism is for writes the client is already authorised to make (AD-11). Rendering a remembered Board while the 404 is in flight is a disclosure with a short lifetime, not a fast interface.
- **A Task, Project or Space id in a URL confers nothing** and must not render anything from cache before the server answers.
- On the FR-34 purge there is a deliberate **asymmetry**: the User's own unsynchronised *text* stays visible, because they typed it and it is already on their screen; everything that came *from* the Space goes.

### Browser-owned surfaces

Several disclosure surfaces are outside the application's control, and none is obvious. All four leak **Space names** — identity, not contents — to Accounts and third parties with no Membership, which §6.1 makes the specific thing nobody may do, *"including a Space's Owner"*.

| Surface | Mechanism | Rule |
|---|---|---|
| Form autofill | Origin-scoped and Account-agnostic. The delete-Space confirm asks the User to **type a Space name**, so autofill offers it to the next Account on that browser profile. | `autocomplete="off"` and a non-reusable field name on every Space, Project and Task name input. |
| `document.title` | Written into browser history, the OS window title, screen shares and cross-device synced history — and persists long after a Membership ends. | A fixed `Yello`. No Space, Project or Task name, ever. |
| Link-preview scraping | The Invitation view is unauthenticated and legitimately renders a Space name (FR-11 requires it). The leak is not the page but what it *emits* — paste the link into Slack and a preview service copies the Space name into the chat logs of people with no Membership. | No Space name in `<title>` or any `og:`/`twitter:` metadata. `noindex`. No Space name or id in the URL beyond the opaque token. |
| Scroll restoration | Browser-owned and URL-keyed, so it can re-present a previous Space's position independently of the purge rules above. | Disabled on Space-scoped routes. |

`[ASSUMPTION: browser spellcheck and cloud IME egress of description text is accepted. Several browsers' enhanced spellcheck and several mobile IMEs transmit typed text to a remote service, and the description editor is the product's largest free-text surface — but disabling spellcheck on a prose field is the wrong usability trade. Recorded as an accepted egress and flagged against §6.4's data-protection gate, where it becomes a real obligation the moment a second data subject exists.]`

### Account-scoped reads beyond AD-24's letter

Three specified reads are richer than *"identity only"*. In each the data is the caller's **own**, so no other Account's data is disclosed and NFR-1 is not breached — but AD-24 exists precisely to stop an implementer who needs an unauthorised read from *"disabling RLS, opening a second connection, or inventing its own bypass — and that bypass then spreading."* Silence is what produces the bypass, so each is decided here:

| Read | Decision |
|---|---|
| `Delete Account refused` naming **every Space still owned** (FR-3) | **Permitted.** Own Membership, own Roles. Flag for an AD-24 amendment naming it — the AD already anticipates needing one. |
| The **Spaces-per-Account** bound at NFR-8's 50 (AD-25) | **Permitted**, same basis: own count, never another Account's. Also needs naming in AD-24, since Space creation is not one of its two enumerated surfaces. |
| The API Token list showing a **per-Space Role** | **Dropped.** This is genuinely a Membership read across a boundary on an Account-scoped surface, and it is decoration rather than function. UJ-7's point — that a Token's capability tracks the current Role — survives without displaying it. |

## Component Patterns

Behavioural. Visual specs live in `DESIGN.md` § Components. Components whose behaviour lives elsewhere: `task-card-lifted`, `drop-zone` and the Move control in **Interaction Primitives**; `focus-ring` in **Accessibility Floor**; `button-*` in **State Patterns**.

| Component | Where | Behaviour |
|---|---|---|
| Task card | Board | Click opens Task detail. Drag moves between columns and reorders within one (FR-29). For a Viewer: click still opens; drag and the Move control are absent. |
| Column | Board | Header, count, create affordance. Scrolls within itself, never the page. Create affordance absent below Member. The bulk-move entry point (FR-41) lives here. |
| Column count | Column head | The **true total** for that Status, never the number currently rendered. At FR-28 scale the two differ, and a count that tracks virtualisation is wrong. Rendered rows must therefore carry `aria-setsize` (the true total) and `aria-posinset` (the true ordinal) — otherwise the chip says 4,812 while assistive technology reports a list of 30. |
| Context bar | Everywhere authenticated | Space name opens the switcher. Role chip is display-only — never interactive, never a menu, never a link to Space settings; it states a fact, and making it clickable invites the reading that standing is adjustable. Never scrolls away. |
| Space switcher | Context bar | Lists only Spaces the Account holds a Membership in — the only such list in the product. Identity only: name, nothing else. No counts, no badges. Closes to the refusal surface if the active Space becomes unavailable while it is open. |
| Offer indicator | Context bar | Present only while an Ownership Offer naming this Membership is `Pending` **in the active Space**. Opens the offer dialog. Disappears the moment the offer leaves `Pending`. |
| Role chip | Context bar | Display-only. See Space Context and Role Legibility for the accessible-name rule. |
| Move control | Task detail, Task context menu | **The canonical way to move a Task.** See Interaction Primitives — it is the conformance path, not a fallback. |
| Task detail | Task card click | Opens over the Board, one level deep. Attribute edits land immediately against the local replica. Closing returns focus to the originating Task card — **except where that card no longer exists**, for which see State Patterns. |
| Description editor | Task detail | Collaborative, no save button, no merge prompt, no lock, no stale warning (FR-31). Batches frames rather than sending per keystroke (AD-13). **Absent entirely for a Viewer** — not read-only, absent. When replaced by rendered text, the replacement **retains the editor's labelled region and heading**; losing the "Description" label comes free with a naive swap and nobody notices it in review. |
| Presence indicator | Task detail, Task card | Appears within 2s of a participant arriving, disappears within 10s of them leaving (NFR-3), without their action. Dot plus text count, always. Shows only Memberships of the same Space and **never reveals an Account's activity in any other Space** (FR-32). Rendered visually on cards; only the **open Task's** Presence is routed to a live region — see Accessibility Floor. |
| Avatar | Task card, Task detail, Presence | Monospace initials from the display name, non-interactive. A deleted Account renders as a tombstone (FR-3) — never blank, never removed, because attribution survives deletion. Never carries an email address. |
| Picker | Task detail, Board, Space settings | One behaviour, five uses. **Move** names a destination Status and position. **Assignee** offers Memberships of the active Space only (FR-21); a Viewer may be assigned and this grants them nothing. **Label** applies Labels defined for the Space — defining them is Space settings, Owner and Admin only (FR-22). **Status** offers only this Project's effective set (FR-20); a Status valid in a sibling Project is neither offered nor accepted. **Role** carries FR-13's narrowing: an Admin may offer Member↔Viewer only, promoting to or demoting from Admin is Owner-only, and no change may produce a second Owner or remove the sole Owner. **Never carries a default selection** where the choice is consequential. |
| Bulk move | Board column, List View filtered to one Status | Moves every Task in that Status to another Project in the same Space. Exactly one mapping decision, atomic — all or none (FR-41). The bar appears once initiated, names the scope, and is the only place the operation can be cancelled before commit — so focus moves to it on appearance. |
| Membership list | Space settings | Every Membership in the active Space with its Role. Owner and Admin only. **Email addresses are visible here and nowhere else.** An Admin sees Member↔Viewer controls only; the controls an Admin lacks are **absent**. The Owner's row carries no remove control for anyone, including the Owner (FR-14). At NFR-8's 100-Membership bound the list pages rather than growing unbounded. |
| Invitation list | Space settings | Pending Invitations with the address, Role and issuer. Revocable by any Owner or Admin — **including one who did not issue it**, and including when the issuer has since been demoted, removed or deleted (FR-12). Terminal Invitations are retained in the record but **not shown**; no product surface reads them (FR-10). |
| Status delta editor | Project settings | Add, remove, rename and reorder this Project's Statuses as a delta over the Space defaults (FR-25). Removal triggers the mapping requirement — see Status Configuration. Shows the effective set and **which entries come from the Space defaults versus this Project's delta**, because that distinction is what makes FR-27's cascade comprehensible when it fires. |
| Ownership panel | Space settings | Owner-only. Offer ownership to a named Membership of **any** Role (FR-8), see a pending offer with its recipient and expiry, and revoke it. Revoking leaves every Membership and Role exactly as it was. |
| List View controls | List View | Filter and sort by Status, Assignee, due date and Label (FR-30). Filters never surface a Task from another Project or Space. Filtering by Assignee offers only Memberships of the active Space. Pages rather than scrolling infinitely. |
| Status pager | Board below 768px | A **tablist** over the Project's effective Status set, with the column as its panel. Arrow-key navigation between tabs, and a polite announcement of the new Status and its true count on change. It is the *only* route to a Status at that width, and therefore the surface a 1.4.10 audit is conducted on. |
| Invitation view | Unauthenticated | A **side-effect-free read** (AD-28). Names the Space, the Role, and who issued it. Accepting is a separate explicit act. |
| Destructive confirm | Task, Membership, Project, Space, Account | Friction scales with blast radius. Replaces its invoking panel's content in place rather than stacking. See State Patterns. |
| Dialog | Global | One level deep. `Esc` closes. Focus trapped, returned to the invoking element on close. |

## State Patterns

Five states form one cluster and are specified below the table rather than inside it — the rules that matter most about them are *ordering* rules, which a grid cell cannot carry legibly. Everything else is a table row.

### Interruption: the FR-34 cluster

FR-34 is the requirement the PRD says to judge the product on: *"If everything else works and this does not, the isolation model is decorative."* All five states below share one substrate — AD-9 pushes a `MembershipChanged` at the transaction boundary, AD-8 invalidates the lease, and the User did not act.

**Two rules govern the whole cluster.**

1. **Purge before announce.** On lease invalidation the client discards every queued **inbound** frame for that Space and clears both live regions, and only then announces. Reversing this renders a queued Presence or remote-edit frame one tick *after* "Access ended." — disclosing who was present in, and what was edited in, a Space the Account no longer belongs to.
2. **The User's own text stays; the Space's data goes.** Unsynchronised text is **not applied** and never reaches the Space, but it **stays visible, focusable and selectable** so it can be copied. It is their own typing on their own screen, so showing it discloses nothing, and wiping it would be gratuitous. Everything that came *from* the Space is purged.

| State | Treatment |
|---|---|
| **Access ended mid-edit** | The editor becomes **`readonly` immediately**. Banner: "Access ended.", focused and announced assertively. The banner states the text was not saved. |
| **Demoted to Viewer mid-edit** | Editing ends, read access continues uninterrupted. **The removal is narrated before the surface settles** — the editor is replaced by rendered text that keeps its labelled region, and only then does the write affordance become absent. |
| Disconnected mid-edit | "Disconnected. Your changes are not yet sent." Editing continues against the local replica. Not a modal — it must not block typing. The copy avoids "held" and "saved", which would promise application (FR-33, §8). |
| Reconciliation failed | State explicitly that reconciliation failed; keep the text visible and copyable. Never auto-retry silently forever. FR-33 forbids silent discard. |
| **Removed while disconnected** | The composition none of the above covers. The lease invalidation **cannot reach a disconnected client**, so the User keeps editing believing access continues, and learns only on reconnection when the frames are discarded rather than queued. Resolves to **"Access ended."**, *not* to a reconciliation failure — the text was never admissible, it was not lost to a fault. **Never show a sync-succeeded state first and then revoke it.** |

**The general case is not confined to the editor.** A Role dropping below a *surface's* requirement — an Admin demoted to Member while sitting in Space settings — has the surface disappear under them. Narrate first: "You're now a Member. Space settings is no longer available." Then route to Space home. Never blank the surface silently, and never leave a half-rendered settings page.

### Everything else

| State | Surface | Treatment |
|---|---|---|
| Session expired | Any | FR-2. An expired or invalidated Session grants access to nothing, **including Spaces the Account still holds Membership in**. Return to sign-in stating the reason. All Space-scoped client state is purged. Unsynchronised editor text is retained locally and shown on return where the surface can be restored, and is **never silently submitted after re-authentication** — it would then be authored under a new Session against possibly-changed permission. |
| Empty Project | Board | "Nothing here yet." + `{components.button-primary}` "Add a Task". **Never** prompts to configure Statuses — UJ-1 requires Ravi meet no empty state asking him to configure something. |
| Empty Space | Space home | "No Projects yet." + "Create a Project". Same discipline. |
| Belonging to no Space | Post-authentication | A valid state (FR-7). Offer to create a Space; never auto-create one. |
| Board at scale | Board | FR-28 at 5,000 Tasks. Every Task reachable, each in exactly one column. Virtualisation *strategy* is architecture's call; the behavioural contract is not negotiable — keyboard navigation drives the virtualiser rather than the reverse, row identity is keyed to the Task id so recycling cannot re-point focus, focus is restored by Task id after any window change, `aria-setsize`/`aria-posinset` carry the true total, and no announcement fires for loading or virtualising. **See the unowned-collision note under Responsive & Platform.** |
| List View at scale | List View | FR-30 requires NFR-5 and NFR-9 to hold at 5,000 Tasks *"on the same terms as FR-28"*. Pages rather than virtualising, which sidesteps the focus-recycling problem entirely; page size stated, keyboard row traversal specified, and the filter result count announced politely on change. |
| Board mutating remotely | Board | Other Users' edits push Tasks into, out of and between columns (AD-9, AD-11), and `DESIGN.md` deliberately makes this silent *visually*. For assistive technology the policy is stated rather than left to the implementer: **no per-Task announcement** — at 5,000 Tasks and 50 sessions that is a denial of service — plus a **debounced summary** ("3 Tasks changed") and a manual refresh affordance. Announcing every mutation recreates the storm; announcing nothing leaves the buffer silently stale. |
| Bulk move in flight | Board, List View | Names its own scope: "Moving 4,812 Tasks." Focus moves to the bar, which carries `role="status"` for the scope line and holds the only cancel affordance. Blocks interaction on the affected columns only — implemented so that blocking never destroys the focused node. Atomic, so there is no partial progress: a percentage bar would be untrue. On commit focus goes to the destination column; on cancel, to the originating column. |
| Bulk move refused | Board, List View | FR-41 requires visible, not silent. State that nothing moved. |
| Status migration in flight | Space settings, Project settings | FR-27's Space-level removal commits the removal, the Space-wide mapping and every per-Project exception as **one transaction** across up to 50 Projects × 5,000 Tasks — strictly larger than the bulk move. Same treatment: named scope, focus moved to the progress region, no percentage. |
| Status migration refused | Space settings, Project settings | Nothing applied. State it, and state which Project blocked it. |
| Ownership Offer pending (recipient) | Context bar | `{components.offer-indicator}`. Opens a dialog explaining what accepting commits them to. Accept and Decline are both explicit; **neither is a default and neither is pre-focused**, because a mis-hit `Enter` must not transfer a Space. |
| Offer no longer pending | Offer dialog | "This offer is no longer open." A designed state, not an error toast. **409 applies only to a caller holding a Membership in the Space** — AD-26 permits it *because* AD-3's boundary rule does not apply to them. A caller with no Membership gets **404**; returning 409 would confirm the Space and the offer exist. Resolve the Space context first, evaluate `State = Pending` second, never the reverse. |
| Ownership Offer pending (Owner) | Space settings | Shows the named recipient and that it awaits their answer. Revocable. States the expiry, computed against the **server** clock (AD-27) — a client-side countdown would disagree with the actual answer. |
| Invitation not actionable | Unauthenticated | One response for **revoked, accepted, expired, lapsed and unrecognised** — identical words, shape and duration: "This invitation is no longer valid." Discloses neither the Space, its contents, nor who revoked it. The uniformity is the point: a distinct response for a token that never existed makes the acceptance route an existence oracle, which is AD-23's discipline applied one surface over. |
| Invitation, wrong Account signed in | Unauthenticated | AD-28 requires acceptance authorised on the Account matching the invited address, so the mismatch must be handled — and handled **without echoing either address**. State that the Invitation is addressed to a different Account and offer sign-out. The helpful version ("this is for nadia@…, you are ravi@…") discloses the invited address to whoever holds the link, which on a forwarded link is a stranger. |
| Delete Task | Task detail | Confirm naming the Task, replacing the panel content in place. "This cannot be undone." |
| Delete Membership | Space settings | FR-14 — it ends someone's access to everything in the Space immediately, with their editor possibly open. It belongs on the blast-radius ladder between Task and Project. The confirm names the person and **states whether they currently have a live session**, so the remover knows they are interrupting someone mid-edit. |
| Delete Project | Project settings | Confirm naming the Project **and its Task count**. If the count is zero, say so — that is UJ-9's payoff, where the mechanism that adds friction also reassures. |
| Delete Space | Space settings | Confirm naming the Space, its Project and Task counts, and **that other Accounts lose access** (§6.2). Require typing the Space name, in a field with `autocomplete="off"`. The most destructive operation in the product and the only one with no undo. |
| Delete Account refused | Account settings | FR-3. Name every Space still owned. State the two exits — transfer it and have the offer accepted, or delete it. Do not imply a third. |
| Task deleted while open | Task detail | FR-23. Participants are **told it was deleted** — never dropped silently, never left on a dead connection. The panel closes to the Board. Unsent text is retained in a panel that **receives focus and is announced**; it is the only copy, so it is not dismissible by a stray keypress, and once dismissed focus goes to the column that held the Task. The documented "return focus to the originating Task card" rule cannot apply — the card has just been deleted. |
| Sign-in failed | Unauthenticated | FR-2, AD-23. One message for every cause: "Email or password is incorrect." Never "no account found", never "wrong password" — and the **response duration must not branch either**. |
| Submitting registration or sign-in | Unauthenticated | The only submit-and-wait interactions in a product with no save button, and AD-23 makes registration deliberately slow by hashing even for a known address. State the in-flight condition, disable resubmission, and announce completion. |
| API Token created | Account settings | FR-36. Displayed **exactly once** and never retrievable. Say so *before* generating it. Copying is the primary action; dismissing is a confirmed act. No per-Space Role is shown alongside it. |
| List View filter empty | List View | "No Tasks match." Nothing else — no suggestion to broaden, no count of what was excluded, since a count across a filter is still only this Project. |
| Scale bound reached | Any creation surface | AD-25 refuses at the NFR-8 bound inside the same transaction. State the bound plainly: "This Space has 100 Memberships, the maximum." A bound must **degrade visibly, never silently**. |
| Rate limited | API only | No browser surface. FR-38 is a machine-readable refusal with `Retry-After`. |
| Disconnected on a non-editor surface | Board, List View, Space home | §8 requires Yello to *"degrade honestly"*. The Board is live — Presence, pushed permission changes, other Users' moves. State that updates have stopped and what is consequently stale. Never silently present a frozen Board as current. |
| Cold load | Any | Skeletons matching the eventual layout, marked `aria-busy="true"`, with completion announced politely. Never a spinner over the whole surface. **The context bar may render its shell before its contents, but never the Space name** — the name cannot come from a 404 (AD-3 strips it), so rendering it early means sourcing it from cache, which both distinguishes "removed from" (name available) from "never existed" (no name) and, on a shared profile, renders one Account's Space name to another. |

## Interaction Primitives

**Moving a Task has one canonical path and three accelerated ones.** The distinction matters: the canonical path is what makes the product conformant, and the gestures are enhancements on top of it. Treating drag as the real interaction and everything else as an accommodation is what makes drag-and-drop boards inaccessible.

### The canonical path — an explicit Move control

Every Task carries a **Move** affordance that is a plain control, not a gesture: in Task detail, and on the Board through the Task's context menu (opened by pointer, by `Enter` on a focused Task, or by the platform context-menu key). It opens a `{components.picker}` naming the destination Status and the position within it.

It is load-bearing for three separate reasons, and it was added because review found the spine non-conformant without it:

1. **WCAG 2.5.1 Pointer Gestures (Level A).** A drag is path-based, so all functionality it provides must also be operable with a single pointer without a path. Between 768px and 1279px on a touch tablet there is no keyboard and no Status pager, so long-press drag was previously the *only* route to a cross-column move — a Level A failure inside one of NFR-9's five named flows. FR-29 already defines Status-change-as-move, so the mechanism existed; only the affordance was missing. This also pre-satisfies WCAG 2.2's 2.5.7 Dragging Movements.
2. **Screen-reader browse mode.** NVDA and JAWS consume arrow keys for their virtual cursor, so the pick-up-and-arrow grammar below never reaches the application. A standard control and menu work in browse mode with no mode switching — and `role="application"` is *not* the answer, because suppressing the user's own navigation is a well-known trap.
3. **Motor accessibility.** Sustained press-and-drag is among the most demanding interactions there is, and a 320ms hold with a movement tolerance is systematically unreachable for a User with essential tremor. The control removes it from the critical path.

`Move` is never removed at any breakpoint, never hover-only, and never the second thing offered. It is absent only for a Role that cannot move Tasks at all.

### The accelerated paths

**Pointer** — press and drag to lift, move, drop. `{motion.lift}` on lift, `{motion.settle}` on drop, `{components.drop-zone}` marking the destination.

**Touch** — **long-press to lift, then drag**, with `{motion.long-press-threshold}` before the lift commits. Chosen for fidelity to the reference product in full knowledge that it is the most fragile option considered, so it carries obligations a tap-based path would not:

- Movement beyond `{motion.long-press-slop}` in **any** direction before the threshold is a pan and cancels the pending lift. The rule is axis-agnostic and has a real dead zone, because both matter: a single-axis rule breaks at 768–1279px where the Board itself scrolls horizontally and a pan is indistinguishable from the start of a cross-column drag; and a zero-tolerance rule cancels every lift on ordinary finger jitter.
- Scroll and pan intent always win over a pending lift.
- The lift is confirmed by `{components.task-card-lifted}` and by haptic feedback where the platform offers it.
- Dragging near a column edge auto-scrolls that column; near the viewport edge, the Board.
- The gesture is cancellable — drag back to origin, or release outside any drop zone, and nothing moves (WCAG 2.5.2).
- **This gesture does not discharge NFR-9.** The keyboard path is separately mandated and is not derived from it.

**Keyboard** — the whole Board is operable without a pointer:

- `Tab` / `Shift+Tab` — between columns and controls, in reading order
- `↑` `↓` — between Tasks within a column
- `←` `→` — between columns, by **logical** direction: `→` is inline-end, the next column in reading order, which mirrors under RTL exactly as the layout does. Column position is preserved by **sticky origin index with clamping** — the index you started a traversal run at is remembered and clamped to each column's length, rather than re-derived per hop.
- `Enter` — open the focused Task, or open its context menu from a Task card
- `Space` — pick up the focused Task; `←` `→` move it between columns, `↑` `↓` within one; `Space` drops, `Esc` cancels. **This binding applies only when a Task card holds focus.** Everywhere else `Space` keeps its native meaning — activating a button, paging a scroll container — and Board columns *are* scroll containers, so an unscoped rebinding would break both.
- A carried Task entering a column lands at **the same ordinal, clamped to that column's length**; `↑` `↓` adjusts before the `Space` commit. The keyboard path is where a move must produce a persisted value, and AD-15 computes a fractional index between two concrete neighbours, so leaving the destination unspecified leaves the implementation with no neighbour pair.
- `Esc` — **innermost meaning wins.** In the description editor, the first `Esc` leaves the editor and a second closes the Task detail panel. Otherwise: cancel a pick-up, or close the topmost dialog.

**Below 768px the keyboard path uses the same Move picker as touch.** With one column visible and a Status pager, a `Space` + `←` `→` move would send a carried Task into an off-screen column with no visible drop zone. A committed move advances the pager to follow the Task.

Every pick-up, move and drop **announces via an ARIA live region**, and the string shape is specified because for a blind User this announcement *replaces* the entire visual drop-zone system:

> `"Moved to In Progress, position 3 of 12."` — destination Status, position ordinal, and column total. Without the ordinal there is no way to know where the Task went; without the total, no way to know you are at the end. A cancel announces the restoration: `"Returned to Todo, position 7."`

**The arrow grammar requires the Board to be an application-mode composite widget** — a single tab stop with `role="grid"` or equivalent, managing focus internally. That is a real implementation constraint, and it is exactly why it is not the conformance path: built wrongly, browse-mode users lose the Board entirely. The Move control is unaffected either way.

**Banned everywhere:**

- Hover-only affordances. There is no hover on touch, and a Role-absent control must be absent rather than hidden until hover.
- Disabled controls for Role reasons — remove them.
- Merge prompts, edit locks, stale-content warnings (FR-31 forbids all three).
- Modal stacks deeper than one.
- Infinite scroll — the Board scrolls per column; the List View pages.
- Optimistic UI on anything destructive or on a permission change. Everything else is optimistic by default (AD-11).
- Auto-save indicators. There is no save; an indicator would imply one.
- Badges, counts-as-nudges, streaks, re-engagement prompts. SM-C2 and SM-C4 make these counter-metrics.

## Status Configuration

*Invented section. FR-27's report-and-ask cascade is the most complex interaction in the product and no table row can hold it.*

A Space holds default Statuses; each Project holds a **delta** over them, and its effective set is the defaults with the delta applied (§4.7). Deltas key on Status **identity**, not name (AD-16), which is what makes the cascade below detectable at all.

**Removing a Status is always a migration, never a deletion.** FR-26: every occupying Task must be mapped in the same operation, and nothing applies partially.

### Space-level rename that collides

A Space-level rename reaches every Project that has not itself renamed that Status. Where one or more have:

1. Name every conflicting Project and its current name for that Status.
2. Offer **one** cascade decision applied to all of them at once — cascade and their names are replaced, decline and their names are preserved.
3. Apply the rename to non-conflicting Projects either way.

### Space-level removal

1. Ask for **one** Space-wide destination Status.
2. Name every Project whose post-removal effective set cannot accept it, **with how many Tasks each has affected**, and require a destination drawn from that Project's own post-removal set.
3. There is no fallback and no silent placement. Nothing applies until every reported Project has a destination.
4. Commit as **one transaction** — the removal, the Space-wide mapping and every per-Project exception together, or none.

Always satisfiable: a Project's effective set can never be empty (FR-25), so a valid destination always exists.

**The interface must not decide anything here.** §11 records that the original defect was that *"FR-27's rename half asked while its removal half guessed."* Both halves report and ask. A default selection in the destination picker would reintroduce the guess.

## Accessibility Floor

Behavioural. Visual contrast lives in `DESIGN.md`.

**WCAG 2.1 AA** on registration, Space switching, the Board, the Task editor and the invitation flow (NFR-9). Consumer stakes, so this is a hard release gate.

- **Every Board pointer operation has a keyboard equivalent**, including moving a Task between columns. See Interaction Primitives.
- The **acting Role and what it permits** are in the accessible name of the context bar, so a screen-reader User establishes standing on arrival rather than on failure.
- Focus is visible at all times via `{components.focus-ring}`, never removed, never replaced by a colour change, and never inset — the 2px offset is what makes it visible at all.
- `Tab` order follows reading order on every surface. Focus is trapped in dialogs and returned to the invoking element on close.
- Interactive targets meet `{spacing.target-min}`. The Task card's lift target is the whole card, which clears 44px on its own arithmetic.
- `prefers-reduced-motion: reduce` removes every transition. Nothing depends on motion to convey state.
- **Text must survive a 1.4.12 override** — line-height 1.5×, letter-spacing 0.12×, word-spacing 0.16×, paragraph spacing 2× — with no clipping or overlap. Chips and cards size to content with no fixed heights. Test against all four overrides as a gate item.
- **Verify at 200% *text-only* zoom, not only page zoom.** Page zoom scales everything and hides the failure; a raised default font size is the accommodation 1.4.4 is really about.

### Live regions

- **Presence** is `polite`. **Permission changes** are `assertive`. The split is right — a remote permission change genuinely invalidates what the User is doing; Presence is ambient.
- **Announcements carry no cross-Space information, ever.** An announcement is a disclosure surface like any other (NFR-1). The Presence string is a display name or a count — **never an email address**.
- **A permission-change notice is delivered only to the client context whose active Space matches the change.** Cross-tab fan-out is a per-tab *filter*, never a shared announcement — and the copy stays "Access ended." with no Space name in any case. Without this rule an implementer chasing "observable without the participant acting" broadcasts to every tab, then needs to disambiguate *which* Space ended, and the string becomes "Access to Northwind Redesign ended." spoken in a tab showing a different Space. Screen-reader users would take that disclosure first and hardest, inverting the intent of the requirement.
- **Announcements are throttled, and the NFR-8 bounds are why.** 10 concurrent editors per Task and 50 concurrent Sessions per Space make a naive one-announcement-per-event region a denial of service — `polite` *queues* rather than coalescing, so the backlog outlives the events and talks over the User's own typing echo.
  - Presence announces the **settled count**, not each transition, debounced to roughly 5 seconds, and only for the **Task the User has open** — Presence renders visually on cards without routing card-level churn to a live region, which otherwise scales with the Board rather than with the 10-editor bound.
  - Presence is **suppressed entirely while the User is typing** in the editor. A collaborator arriving is not worth interrupting a sentence, and it remains available visually.
  - Permission changes are **never** throttled or coalesced. There is at most one per Account per Space and it has earned the interruption. Note that an assertive interrupt flushes queued polite announcements; a dropped "3 editing" costs nothing and is not a bug.
  - The Board announces on deliberate action only — a pick-up, a move, a drop, a filter count. Never on loading or virtualising.

### Focus destinations for remote events

Yello is a product where **things happen to you** — AD-9 pushes permission changes into open sessions, and other Users delete Tasks you have open. Any of those can land on the element holding focus, and a destroyed or `readonly`-ed focused node drops focus to `<body>`, silently stranding a keyboard or screen-reader User. `assertive` announces; it does **not** move focus. So each case names its destination:

| Event | Focus goes to |
|---|---|
| **FR-34 removal** — editor made `readonly` while focused | The **"Access ended." banner**, made programmatically focusable (`tabindex="-1"`, `role="alert"`) and announced. The banner persists until dismissed and stays in the reading order — a live-region utterance fired during the DOM mutation this event causes is frequently never spoken at all, so the persistent, focusable banner is the real carrier. The retained text is the next stop in the reading order. |
| **FR-34 demotion** — editor replaced by rendered text | The rendered description, at the same scroll position, with its labelled region intact. Read access continues, so the surface should not jump. |
| **Role drops below a surface's requirement** | The narration, then Space home. |
| **Task deleted while open** (FR-23) | The retained-text panel, announced. Then, on dismissal, the **column** that held the Task — not the adjacent Task, whose index has just shifted, and not the originating card, which no longer exists. |
| Space switch | The context bar, with the new Space and Role announced politely on arrival. Returning focus to an element whose accessible name has changed does not reliably re-announce, and this is one of NFR-9's five gated flows, so inheriting the generic dialog rule is not enough. |
| Dialog close | The invoking element; if it no longer exists, its nearest surviving container. |
| **Virtualised row recycled** | Focus follows the **Task identity**, never the row element. Keyboard navigation drives the virtualiser rather than the reverse. A recycled row silently re-pointing focus at a different Task means `Space` picks up the wrong Task — a data-corrupting bug reachable only by keyboard, and invisible to pointer testing. |
| Bulk move begins / completes | The bar on appearance; the destination column on commit; the originating column on cancel. |

## Responsive & Platform

Responsive web, one codebase, no native client.

| Breakpoint | Board behaviour |
|---|---|
| ≥ 1280px | All Status columns visible side by side. Context bar full: Space name, Project, Role chip. |
| 768–1279px | Columns side by side with horizontal Board scroll. Context bar drops the Project name, keeps Space and Role. |
| < 768px | **One column at a time** with a Status pager. Long-press drag moves within the visible column; cross-Status moves use the Move control. |

`[ASSUMPTION: breakpoints at 768 and 1280. Nothing upstream names any; these follow the column-count arithmetic of a three-to-five Status Board at the specified density.]`

The Role chip and the Offer indicator survive **every** breakpoint. If something must be dropped from the context bar it is the Project name, then the switcher chevron label — never the Role.

**The `<768px` path is the 1.4.10 answer, and it fires by construction.** 400% zoom on a 1280px monitor yields a 320px CSS viewport, so the one-column pager *is* the surface under audit. 1.4.10 exempts content requiring two-dimensional layout — the exemption a Kanban board would normally claim — and this design declines it, eliminating the horizontal axis instead. That is the stronger position, and it is why the pager's keyboard behaviour is specified rather than left open.

Phones are for reading and light editing. The Board at 5,000 Tasks is a desktop proposition; the small-viewport path must stay *correct* at that size, not fast.

### Unresolved: the FR-28 collision belongs to nobody

FR-28 requires the Board to satisfy **NFR-5** (300 ms p95 reads) and **NFR-9** (WCAG 2.1 AA, full keyboard operation) simultaneously at **5,000 Tasks in a Project** — and the PRD states plainly the three *"cannot all hold naively"*, then defers the mechanism to the architecture. The architecture's 28 ADs never decide it: AD-13 and AD-15 fix persistence and ordering, AD-25 enforces the bound as a creation refusal, and nothing chooses paging, virtualisation or windowing. §4.8 maps only to AD-13/AD-15/AD-16.

**That is a three-way deferral, so no document owns it** — and its accessibility half is a release gate. This spine's obligation is to state the contract any mechanism must satisfy, which *Board at scale* and the focus table above now do: keyboard drives the window, row identity is keyed to the Task id, focus restores by id, and `aria-setsize`/`aria-posinset` carry the true total. **Flagged for `bmad-architecture`.** Whoever decides the mechanism must decide focus and announcement behaviour in the same pass.

## Internationalisation

*Invented section, kept standalone deliberately. Internationalisation was adopted with no upstream requirement behind it, so nothing else in this repository will remind anyone it exists — and obligations scattered across three other sections are easier to quietly drop than a section someone has to decide to delete.*

- **All copy externalised.** No user-visible string literal in a component. Copy resources hold **sentence case**; uppercase is applied with `text-transform` (see `DESIGN.md` § Typography for why this protects the Role's accessible name).
- **No layout sized to an English string.** German and Finnish run 30–40% longer.
- **Structure is RTL-tolerant**: logical properties (`inline-start` / `inline-end`) throughout, never `left` / `right`. The Board's column order and the drag direction mirror under RTL — **and so do the arrow keys**, which are bound to logical direction, not physical. The Status *sequence* does not reverse; it is data, not layout.
- **Dates and times** are `DateTimeOffset` UTC on the wire and rendered in the viewer's locale and zone. Relative time — "2 days left" on an Offer — is computed against the **server** clock, never the client's, because expiry is server-evaluated (AD-27) and a client-side countdown would disagree with the real answer.
- **Uppercase and letter-spacing are Latin-only and locale-sensitive even there.** See `DESIGN.md` § Typography: the per-script fallback, and the exclusion of Turkish, Azeri and Greek where `text-transform: uppercase` is lossy.
- **Metadata is never aligned by character count** — `system-mono`'s non-Latin fallback is often not monospaced.

## Inspiration & Anti-patterns

**Lifted from Trello**, named as the reference product at all three layers — capability shape, interaction feel, visual look:

- Columns by Status with cards you drag between them, as the primary working surface.
- Optimistic, instant-landing edits with no save step — architecturally free here, because AD-11 already has the client editing a local replica.
- Click-to-open a card into a detail surface; the board stays behind it.
- Card-plus-column as the visual vocabulary, reinterpreted through `DESIGN.md`'s drawn-not-floated structure.

**Where the reference product must not be followed** — each ruled out by the PRD, not deferred:

- **Public boards, share links, read-only URLs** — §8. Membership is the only route in.
- **A cross-board home, "my cards", any all-Spaces view** — §8 and AD-24. Not an omission to correct later; a surface spanning Spaces contradicts the model.
- **Comments, activity feeds, attachments, checklists, custom fields, integrations** — §9.2.
- **Notification preferences** — §9.2. Per-event, not configurable.
- **Free-form columns.** Trello's lists are created at will; Yello's Statuses live at Space level with a per-Project delta, and removing one is a mandatory migration. This is where "it works like Trello" would mislead an implementer most.
- **Archive.** Trello's safety net does not exist here — deletion is immediate and irreversible (§6.2), by explicit decision. Do not use the word, and do not imply the concept.
- **Badges, streaks, re-engagement, celebratory motion** — SM-C2 and SM-C4 make engagement a counter-metric. UJ-1 succeeds when Ravi closes the tab.

**Where Yello must be better than the reference, not equal to it:**

- **Keyboard operation of the Board.** NFR-9 makes it mandatory, not a power-user extra.
- **Role legibility.** Trello buries a member's standing in the parent container's settings; UJ-4 makes it permanent chrome.

## Key Flows

Journeys **UJ-1 … UJ-8 mirror PRD §3.3** — IDs, protagonists and edge cases follow the source; two titles are lightly adjusted for glossary discipline ("an Invitation", "at once"). **UJ-9 is new**, added to close a surface-closure gap: Project settings had no journey landing on it.

### UJ-1 — Ravi has somewhere to put a Task ninety seconds after signing up

1. Ravi registers with email and password.
2. He lands in a Space already named "Ravi's Space" (FR-4). He did not create it and was not asked to.
3. Space home: "No Projects yet." One action. He creates "Admin".
4. The Board opens on the default Statuses — Todo / In Progress / Done (FR-24). He is not asked about columns.
5. He adds a Task to Todo and closes the tab.
6. **Climax:** value landed before he made a single structural decision. He met no empty state asking him to configure anything, and no indication that Yello is collaborative at all.

Failure: registration against an address that already has an Account returns a response **indistinguishable** from a new one (FR-1, AD-23) — same words, same shape, same duration. The interface must not branch here, including on timing.

### UJ-2 — Ravi opens a client engagement without exposing anything else

1. Ravi creates a second Space, "Northwind Redesign" (FR-5). He becomes its Owner; the Space has exactly one Membership and the default Status set.
2. In Space settings he invites Nadia by email address as a **Member**, and Beatriz as a **Viewer**. Neither has an Account. Role is fixed at issue time (FR-10), and **Owner is not offerable** — ownership moves only by an Ownership Offer.
3. Each Invitation is delivered by email naming the Space, the Role, and who issued it — and nothing about the Space's contents (FR-39).
4. **Climax:** nothing either invitee receives or sees mentions "Ravi's Space". The Invitation is scoped to one Space and carries one Role, and Ravi's other work is not merely hidden — it is unreachable.
5. Ravi is now Owner of two Spaces with different Memberships.

Failure — the address already has an Account: the response to Ravi is **identical** either way (FR-10, AD-23). They join with their existing Account, and their other Memberships are neither visible to him nor affected. Failure — the address already holds a Membership *here*: refused, and this **is** disclosable, because he can already see that Membership.

### UJ-3 — Nadia accepts an Invitation and sees exactly one thing

1. Nadia opens the link from her email.
2. The Invitation view renders: the Space name, the Role offered, who issued it. **This is a side-effect-free read** (AD-28) — nothing has been created.
3. She has no Account, so she registers. That registration **is** her act of acceptance (FR-11), and it provisions her own Personal Space in the same transaction, independently (AD-22).
4. She lands in "Northwind Redesign".
5. **Climax:** she sees that Space's Projects and Tasks and nothing else in Yello. No directory, no search past her Membership, no sign Ravi has other clients. Her own Personal Space is in the switcher; she ignores it.

Failures — revoked, expired or unrecognised: one identical response, disclosing neither the Space, its contents, nor who revoked it. Scanner, prefetch or forwarded link: the route is safe, so **no Membership is created and nothing changes**. An existing-Account invitee gets an explicit confirmation instead of registration; loading the URL is never enough. Signed in as a different Account: refused without echoing either address.

### UJ-4 — Ravi switches context three times before lunch

1. Ravi is Owner of "Ravi's Space", Admin of "Northwind Redesign", Viewer on a former client's Space.
2. He opens the switcher from the context bar. It lists three Spaces by name — nothing else (AD-24).
3. He picks the client Space. The entire working surface changes; nothing carries over, including client-side data.
4. The Role chip reads **VIEWER**, and the context bar's accessible name says "Viewer, read only in this Space".
5. **Climax:** every affordance to create or edit is **absent — not present-and-failing, absent** — so he can tell his standing from the interface without attempting an action.

Failure — deep link into a Space he was removed from: "Not available." and a route back to a Space he does belong to. The copy does not distinguish "removed" from "never existed" (FR-15, AD-3).

### UJ-5 — Nadia and Ravi write the same Task description at once

1. Nadia opens the Task and starts typing acceptance criteria. Her keystrokes render locally within 16ms (NFR-3) against the local replica.
2. Ravi opens the same Task. Presence appears for both within 2 seconds.
3. Both type into the same description. Remote edits appear within 300ms p95.
4. **Climax:** neither one's work is discarded, both end at the same text, and **neither sees a merge dialog, a lock, or a "someone else changed this" warning** (FR-31). Presence shows who contributed.

Failure: Ravi drops for forty seconds mid-sentence. "Disconnected. Your changes are not yet sent." He keeps typing. On reconnection his edits are reconciled, appearing exactly once, and Nadia's arrive (FR-33). If reconciliation cannot complete he is told explicitly and his unsynchronised text stays visible — never silently discarded.

### UJ-6 — Access ends while the door is still open

1. Beatriz, briefly promoted to Member, has a Task open with an unsaved sentence in the description.
2. Ravi removes her Membership. `MembershipChanged` publishes at the transaction boundary and her sync lease is invalidated (AD-9).
3. Her client purges every queued inbound frame for that Space and clears both live regions.
4. **Climax:** within 1 second (NFR-2), without her touching anything, the editor becomes `readonly`. Banner: "Access ended." — focused and announced assertively.
5. Her unsynchronised text **is not applied** and never reaches the Space, including via any delayed or retried frame (FR-34). It **remains visible, focusable and selectable**, with the banner stating it was not saved. It is her own typing; hiding it would be gratuitous, and showing it discloses nothing.
6. She retains her Account, her own Spaces, and every other Membership.

Variant — demoted to Viewer instead: the same interruption, but read access continues uninterrupted. The removal is narrated, then the editor is replaced by rendered text that keeps its labelled region, and the write affordance becomes *absent*.

### UJ-7 — Tomás automates the part he does twice

1. Tomás opens **Account settings** — one of only two Account-scoped surfaces (AD-24) — and generates an API Token, choosing the single Space it is bound to.
2. The Token is **displayed once**, and the interface says so before generating it.
3. He calls the API from his deploy script. A Task appears in that Space.
4. **Climax:** the Token cannot touch any other Space, **including Spaces Tomás owns himself** (FR-36). Its capability is his current Role in that one Space, resolved per request — so a later demotion narrows the Token without anyone reissuing it.
5. Yello participates in a workflow that never opens a browser.

Failure — the API changes shape: his script keeps working against `/api/v1` while `/api/v2` ships, and deprecation is announced before withdrawal (FR-37). Failure — rate limited: a machine-readable refusal carrying `Retry-After`, partitioned per Token (FR-38). Note the one deliberate narrowing: Board **position** is readable over the API and not writable (FR-35).

### UJ-8 — Ravi hands a Space over and leaves

1. In Space settings, Ravi offers ownership of "Northwind Redesign" to Nadia's Membership. He remains Owner with every capability (FR-8).
2. **Nadia is emailed.** The email names the Space, the Role change offered and who offered it — **and nothing about the Space's contents, its other Memberships, or any other Space.** That constraint is stated here because the offer email is the one genuinely new outbound artefact in the product, NFR-1 explicitly binds notifications, and an email already sent cannot be recalled when AD-5 lapses the offer.
3. Next time Nadia is in that Space, `{components.offer-indicator}` is on her context bar. **She is a Member**, and it reaches her anyway: AD-26 authorises acceptance by **row identity, not Role**, making this the one capability decided outside the FR-16 matrix.
4. She opens it. The dialog explains in full what accepting commits her to — her Membership cannot then be removed while she holds ownership, and her Account deletion is refused until she transfers onward or deletes the Space. Accept and Decline are both explicit; neither is a default.
5. She accepts. Ownership moves in **one atomic step**: she becomes sole Owner, Ravi becomes an Admin without losing access. At no point does the Space have zero or two Owners (FR-42, AD-26).
6. Ravi, now an Admin, removes his own Membership.
7. **Climax:** the Space continues intact, Nadia is Owner **by her own agreement**, and Ravi is gone — no residual access, no orphaned Space. His other Spaces are untouched.

Failure — she declines or lets it lapse: Ravi is still Owner and still cannot leave. His remaining exits are to offer it to someone else or delete the Space (FR-3). Failure — no longer pending when she answers: **409 for her, because she holds a Membership**; a caller with none gets 404 on the same route.

> **Upstream dependency.** Step 2 requires a PRD amendment. FR-8's assumption currently says the offer is *"surfaced in Space settings rather than emailed"*, and §4.11 has no ownership notification. Composed with the 7-day expiry and read-time evaluation (AD-27), the recipient is never told and must happen to enter the Space in time — so an Owner can be trapped indefinitely because someone never logged in. Being assigned a Task emails you (FR-40); being offered an entire Space does not. The amendment must carry the disclosure constraint in step 2, not just the notification trigger. Recorded in `.memlog.md` as an action for `bmad-prd`.

### UJ-9 — Ravi retires a Project safely *(new)*

1. The discovery phase of Northwind is finished. Ravi wants the Project "Discovery" gone, but its Tasks are worth keeping — and deleting a Project destroys its Tasks irreversibly (FR-17).
2. He opens **Project settings** on Discovery to read its effective Status set. The count of Statuses is the count of operations this will take.
3. Four: the Space defaults Todo / In Progress / Done, plus a delta that added "Parked".
4. From the Board's **Done** column he runs a bulk move to "Website Rebuild". Done exists in the destination's effective set, so the Status is preserved and he is asked for no mapping (FR-41).
5. Same from Todo and In Progress.
6. "Parked" does **not** exist in the destination's effective set, so this move requires a destination Status as part of the operation. He chooses Todo.
7. Discovery is empty. He deletes it — and the confirmation tells him it holds **zero Tasks**.
8. **Climax:** the irreversible operation he was afraid of became safe, because he emptied the Project first. The friction and the reassurance turned out to be the same mechanism. He never reached for an undo, because there isn't one.

Failure: a bulk move that cannot complete is **refused, not partially applied**, and the refusal is visible rather than silent (FR-41). If Nadia is mid-edit on one of the moved Tasks her session continues across the move — she is not disconnected. And plainly: **nothing stops Ravi deleting Discovery with Tasks still in it.** FR-17 permits it and destroys them. The safe path is a choice the interface must make attractive, not a guardrail the product enforces.
