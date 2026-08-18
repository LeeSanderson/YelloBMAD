# Role Capability Matrix

Companion to `SPEC.md` (SPEC-yello). Carries CAP-16.

**This matrix is the single source of truth for Role capability.** Individual capabilities in `SPEC.md` and `acceptance-criteria.md` restate the rows that apply to them so each can be read and implemented alone. Where a restatement and this matrix ever disagree, **the matrix is correct and the restatement is a defect.**

Every Role is per-Space. A capability is meaningless without naming the Space it applies in.

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

## Testable consequences

- A Viewer's write attempt is refused at the API regardless of what the interface offered; the interface hiding an action is never the mechanism that enforces it.
- An API Token issued by a Viewer can read and cannot write, matching that Account's Role at the moment each request is evaluated rather than at the moment the Token was issued.
- Every capability above is refused for an Account holding no Membership in the Space, without exception and without disclosing existence.

## Boundaries the matrix does not express

- **Owner uniqueness.** Exactly one Owner per Space at all times. No Role change can produce a second Owner or remove the sole Owner; ownership moves only by transfer (CAP-8).
- **Owner removability.** The Owner's Membership cannot be removed by anyone, including the Owner, while it holds ownership.
- **Admin symmetry.** Admins cannot modify each other. Only the Owner can promote a Membership to Admin or demote one from Admin. *Recorded as an assumption in `SPEC.md`, not a settled decision.*
- **Invitation Role ceiling.** An Invitation can never be issued at Owner Role.
- **Assignment is not capability.** Assigning a Task to a Viewer is permitted and grants no write capability over it. Responsibility and capability are deliberately separable, so a demotion to Viewer never silently unassigns that person's work.
