# Glossary

Companion to `SPEC.md` (SPEC-yello). Holds the domain vocabulary.

These terms are used **verbatim** in every downstream artifact — epics, stories, tests, code, UI copy. A synonym is a discipline violation, not a style choice. Where you see **Space**, no document says "workspace", "tenant" or "org". If a new domain noun appears in any downstream artifact, it is added here in the same pass.

- **Account** — A registered identity in Yello, unique by email address. Global: one Account exists across all Spaces. An Account is never owned by a Space.
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
- **Project** — A named collection of Tasks within one Space. Belongs to exactly one Space and never moves between Spaces.
- **Task** — The unit of work. Belongs to exactly one Project at any moment, and may be moved between Projects within the same Space (CAP-41). Carries a title, description, Status, optional Assignee, optional due date and zero or more Labels.
- **Status** — The workflow position of a Task, drawn from the effective Status set of the Task's Project: the Space defaults with that Project's delta applied. Determines the Board column a Task appears in.
- **Assignee** — The Membership a Task is allocated to. Must be a Membership of the same Space as the Task.
- **Label** — A named tag applied to Tasks for filtering. Defined per Space, applied per Task, many-to-many.
- **Board** — A view of one Project's Tasks arranged in columns by Status, orderable within a column.
- **List View** — A view of one Project's Tasks as rows, filterable and sortable by Task attributes.
- **Presence** — The live indication that other Users are viewing or editing the same Task.
- **API Token** — A credential authenticating API requests as one Account within exactly one Space, at that Account's Role in that Space. Never grants access beyond the Space it was issued for.
- **Session** — An authenticated browser context for one Account. Spans all Spaces the Account belongs to; carries no permission of its own.
