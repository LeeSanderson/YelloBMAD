# Surfaces and Journeys

Companion to `SPEC.md` (SPEC-yello). Holds the user journeys the product must enable and the surface inventory that carries them.

Journey IDs are **UJ-1 … UJ-8**, unchanged from the source PRD. A UX specification produced later should mirror these IDs rather than invent its own. Each journey's **edge case** is load-bearing: several are the only place a required behaviour is stated.

## Jobs to be done

The demand side of the journeys below — what someone is trying to accomplish, in their words.

- **Keep private work private without running a second tool.** I want somewhere to put my own tasks that is genuinely mine, in the same place I do collaborative work.
- **Bring someone into one piece of my work without exposing the rest.** Inviting a collaborator to a client project must not require trusting them with anything else I do.
- **Move between contexts without losing my place.** I work across several engagements in a day; switching should be a single action, and it should be obvious which context I am in.
- **Let outsiders see progress without letting them change it.** A client asking "where are we?" should be answerable by giving them access, not by writing a status email.
- **Know that access ended when access ended.** When I remove someone, I need to believe it took effect — including for whatever they had open at the time.
- **Automate the parts I do twice.** I want to create and update work from scripts and other tools I already run, not only through a browser.

## Journeys

### UJ-1 — Ravi has somewhere to put a Task ninety seconds after signing up

*Carried by CAP-1, CAP-4, CAP-17, CAP-19.*

Ravi, a freelance developer, registers with email and password and lands directly in a Space already named "Ravi's Space" — he did not create it and was not asked to. He creates a Project called "Admin", adds a Task, and closes the tab.

- **Climax:** value landed before he made a single structural decision; he never saw an empty state asking him to configure something.
- **Resolution:** one Space, one Project, one Task, and no notion that Yello is collaborative at all yet.
- **Edge case:** if he abandons registration after the email is taken but before the password is set, retrying with the same address must not reveal whether that Account exists.

### UJ-2 — Ravi opens a client engagement without exposing anything else

*Carried by CAP-5, CAP-10, CAP-39.*

Ravi wins work with a design studio. He creates a second Space, "Northwind Redesign", and invites the studio's producer Nadia as a Member and Beatriz — Northwind's marketing lead, who is paying and wants to watch it land — as a Viewer. Neither has a Yello Account.

- **Path:** create Space → invite by email address → assign Role at invitation time → send.
- **Climax:** Ravi's personal Space is not mentioned anywhere in what either invitee receives or sees; the invitation is scoped to one Space and carries one Role.
- **Resolution:** Ravi is Owner of two Spaces with different Memberships.
- **Edge case:** inviting an address that already has a Yello Account joins that existing Account rather than creating a second one, and their other Spaces remain invisible to Ravi.

### UJ-3 — Nadia accepts an invitation and sees exactly one thing

*Carried by CAP-11, CAP-4, CAP-15, CAP-9.*

Nadia gets an email and clicks through. She has no Account, so she registers — and a personal Space is created for her too, which she ignores.

- **Path:** open invitation → register → land in "Northwind Redesign".
- **Climax:** she can see the Projects and Tasks in that Space and nothing else in Yello. No directory of other Spaces, no search reaching beyond her Membership, no sign that Ravi has other clients.
- **Resolution:** Nadia is a Member of one Space and Owner of a personal Space she may never use.
- **Edge case:** if her invitation was revoked before she accepted it, the link tells her it is no longer valid without disclosing who revoked it or what the Space contains.

### UJ-4 — Ravi switches context three times before lunch

*Carried by CAP-9, CAP-15, CAP-16.*

Ravi is Owner of "Ravi's Space", Admin of "Northwind Redesign", and Viewer on a Space belonging to a company he contracts for. In one morning he moves between all three.

- **Path:** Space switcher → pick Space → the entire working surface changes.
- **Climax:** in the third Space every affordance to create or edit is **absent — not present-and-failing** — so he can tell his standing from the interface without attempting an action.
- **Resolution:** he is never in doubt about which Space he is operating in or what he may do there.
- **Edge case, partly unresolved:** opening a deep link to a Task in a Space he has since been removed from must return him **to a Space he does belong to**. No capability carries this redirect; it is required here and nowhere else.

> ⚠️ **Live contradiction — do not implement from this journey alone.** The source PRD also states in this edge case that he is "told he no longer has access, not that the Task does not exist." That contradicts CAP-15, which requires a resource in a Space the caller holds no Membership in to be *indistinguishable* from one that does not exist, and it contradicts the architecture spine's AD-3, which implements CAP-15 as a hard 404 whose body carries no existence hint. The architecture has already taken CAP-15's side. `SPEC.md` carries this as an open question; the redirect requirement above is unaffected and stands either way.

### UJ-5 — Nadia and Ravi write the same Task description at the same time

*Carried by CAP-31, CAP-32, CAP-33.*

Nadia is fleshing out acceptance criteria on a Task while Ravi, on a call, is adding a constraint to the same description. Both see the other's presence and both see the text evolve.

- **Path:** open Task → begin typing → observe the other participant → continue.
- **Climax:** neither one's work is discarded and both end at the same text; no one is shown a merge dialog or a "someone else has changed this" warning.
- **Resolution:** the Task description reflects both contributions and shows who contributed.
- **Edge case:** Ravi's connection drops for forty seconds mid-sentence; when it returns his local edits are reconciled rather than lost or duplicated.

### UJ-6 — Access ends while the door is still open

*Carried by CAP-14, CAP-34. This is the journey the product is judged on.*

The engagement ends and Ravi removes Beatriz from "Northwind Redesign". She has a Task open in another tab with an unsaved sentence in the description — she had been briefly promoted to Member during the final week to log her own feedback, and nobody checked who was mid-edit before the removal.

- **Climax:** her session in that Space stops working — the editor becomes inert, her unsynchronised text is not applied, and she is told her access has ended.
- **Resolution:** nothing she typed after removal reaches the Space, and she retains access to nothing.
- **Edge case:** if she is demoted to Viewer rather than removed, the same thing happens to her editing ability while her read access continues uninterrupted.

### UJ-7 — Tomás automates the part he does twice

*Carried by CAP-35, CAP-36, CAP-37.*

Tomás runs a small studio and already has a deployment script. He wants a Task created in Yello whenever a release goes out.

- **Path:** generate an API Token scoped to one Space → call the API from the script → Task appears.
- **Climax:** the API Token cannot touch any Space other than the one it was issued for, **including Spaces its creator owns.**
- **Resolution:** Yello participates in a workflow that does not involve opening a browser.
- **Edge case:** when Yello's API changes shape, Tomás's script keeps working against the version it was written for and he is told, in advance, when that stops being true.

### UJ-8 — Ravi hands a Space over and leaves

*Carried by CAP-8, CAP-14, CAP-3.*

Ravi finishes the Northwind engagement and wants out cleanly, but the work must survive. He transfers ownership to Nadia and removes himself.

- **Climax:** the Space continues with all its Projects and Tasks intact, Nadia is now Owner, and Ravi is gone — no residual access, no orphaned Space.
- **Resolution:** Ravi's remaining Spaces are unaffected.
- **Edge case:** if Ravi instead deletes his entire Account, every Space he still owns must be resolved first — he cannot leave a Space ownerless, and other people's work cannot vanish because he left.

## Surface inventory

Navigation here is not decoration: the Space context bar is the mechanism by which authorisation context is established, so it is a **functional surface**, not a layout choice.

| Surface | Holds | Restricted to |
|---|---|---|
| Unauthenticated | Register, sign in, accept an Invitation | — |
| **Space context bar** | Names the active Space, shows the acting Role, switches Space (CAP-9). Always present once authenticated; nothing outside it is meaningful without it | Any Membership |
| Space home | The Projects in the active Space (CAP-18) | Any Membership |
| Project | Board (default) and List View (CAP-28, CAP-30) | Any Membership |
| Task detail | Attributes, collaborative description, Presence (CAP-20, CAP-31, CAP-32) | Any Membership |
| Space settings | Members and Invitations, default Statuses, Labels, ownership, deletion | Owner and Admin |
| Project settings | Status delta | Owner and Admin |
| Account settings | Profile, password, API Tokens, account deletion | The Account itself |

Two rules bind every surface:

- The acting Role is legible from the interface at all times, and capabilities the Role lacks are **absent rather than present-and-failing** (UJ-4).
- **Space switcher and Account settings are the only two Account-scoped surfaces**; everything else is Space-scoped. They may return Space identity — id and name — and nothing else: no Project, Task, Membership, Label or count crosses a Space boundary through them. Adding a third Account-scoped surface is an architecture amendment (spine AD-24), and the federation non-goal says there should not be one.
