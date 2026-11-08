---
baseline_commit: 4d23bf347622285bb2195854adbe6a22921d42d6
---
# Story 4.6: Assign a Task, and be told

Status: ready-for-dev

Epic: 4 — Bring people into one piece of your work (the sixth story of the epic)
Story key: `4-6-assign-a-task-and-be-told`

**Requirements owned.** From the traceability row (`epics.md:374`): **FR-21, FR-40, FR-30** (the Assignee dimension), **AR-8**, **FS-NFR-2**. Test-design rows **I-14** and **I-15** (`test-design-qa.md:278-279`), **P-3** and **P-4** (`:312`, `:358`).

- **FR-21** (`prd.md:408-417`; CAP-21 `acceptance-criteria.md:205-211`; `SPEC.md:117-118`). Every consequence binds:
  - An Owner, Admin or Member can assign a Task to a Membership **of the same Space**. Only those are offered, and only those are accepted, including via the API.
  - An Account with no Membership in the Space cannot be set as Assignee by any route.
  - When the Assignee's Membership is removed (FR-14) or its Account deleted (FR-3), the Task becomes **unassigned and is not deleted**. Removal is this story's (AC8). Account deletion is 5.4's (new ledger entry).
  - A Viewer may be assigned, and this grants no write capability. Demoting an Assignee to Viewer does **not** unassign.
  - `prd.md:387`: "assignment can never be used to reach across a boundary or to imply the existence of an Account outside it."
- **FR-40** (`prd.md:654-661`; CAP-40 `acceptance-criteria.md:393-399`): an Account assigned to a Task is notified, by email, per event, never digested. The email names the Space, the Project and the Task, and nothing from any other Space. An Account is never notified of its own action. `[ASSUMPTION]` PRD §12 item 10 (`prd.md:901`) is unconfirmed, so every case resting on "email, per-event" carries `Assumption = "PRD-12-10"`.
- **FR-30, completion** (`prd.md:536-543`; `epics.md:400`; `sprint-status.yaml:53`): the List View gains the Assignee filter and sort. "Filtering by Assignee offers only Memberships of the active Space." It must still meet NFR-5 at 5,000 Tasks. **FR-30 is delivered when this story closes**, which is the only staged FR this story completes. FR-13, FR-14 and FR-23 stay staged.
- **FR-16 matrix** (`prd.md:344`; `role-capability-matrix.md:15`, `:43`): "Assign a Task": Owner ✔, Admin ✔, Member ✔, Viewer —. "Assignment is not capability." The code already transcribes it: `Capability.AssignTask = [Yes, Yes, Yes, No]` (`Yello.Domain/Memberships/RoleCapabilityMatrix.cs:42`), and **no endpoint uses it yet**.
- **FS-NFR-2** (`epics.md:167`; `prd.md:677`): a record that a notification was sent is retained, holding the Space, the kind and the timestamp, and **never** the message content or the recipient address. It exists so that SM-C4 (`prd.md:871`; `success-metrics.md:48`, "Outbox send records") is derivable. No product surface reads it. The operator queries the database directly (`success-metrics.md:16`).
- **AR-8 / AD-2** (`ARCHITECTURE-SPINE.md:86`): "an Assignee is `(SpaceId, MembershipId)`", a composite foreign key, so the same-Space rule holds by construction.
- **AD-5** (`ARCHITECTURE-SPINE.md:109`, and its 2026-11-07 note at `:111`): removing a Membership sets the dependent `Task.AssigneeMembershipId` to null and never deletes the Task. "the `AssigneeMembershipId` clause is story 4.6's to honour inside this transaction."
- **AD-10 / AR-33** (`:143`): the email is enqueued through the outbox **in the same transaction** as the write, and dispatched in-process after commit. No timer.
- **AD-23 / AR-28** (`:224`) and **UX-DR34** (`EXPERIENCE.md:149-156`): no endpoint returns an address to anyone but an Owner or Admin in the Membership list. The Assignee picker, the avatar, the List filter and every announcement identify a Membership by **display name and initials only**. Identical names are disambiguated by a **Membership-scoped discriminator**, never by the address.
- **AD-29 / AR-22** (`:275`): every List View keyset is `(sortColumn, TaskId)`, with `NULL` fixed explicitly at one end, and every offered sort has a matching composite index.
- **UX:**
  - UX-DR11 (`epics.md:249`; `DESIGN.md:473`; `EXPERIENCE.md:211`): one picker component in five uses, Assignee among them. Never a default selection where the choice is consequential.
  - UX-DR8 / `DESIGN.md:451`: the card's metadata row carries the Assignee avatar, pushed right (`mockups/board.html:106-190`, `.av` initials).
  - `DESIGN.md:188-193`, `:464`: the avatar is 1.25rem, 3px corners, monospace initials, non-interactive, with no address in its tooltip.
  - UX-DR27: a Viewer has no assign control. It is absent, not disabled.
  - UX-DR19: the List filter and sort.
  - UX-DR31: the Assignee list is purged on a Space switch.
  - UX-DR38: no announcement carries an address.
  - `mockups/task-detail.html:86-90` shows **no Assignee attribute**, so placement follows the DESIGN spine: attributes above the description, beside Status, Due and Labels.

**Ledger entries owned (`deferred-work.md`).** Close each one, or re-own it with a reason. All five were found by grepping for `4.6`, `4-6` and `Assignee` on 2026-11-08.

| Line | Entry | Severity | Covered by |
|---|---|---|---|
| `:628` | FR-30 is partial. Add the Assignee filter and sort: its code in `ListSorts`, its column in `ListSortColumns`, its composite index, `ListViewGateTests` (P-4) agreeing, and isolation case I-15 | low | AC7, AC12; Q6 |
| `:736` | The notification send record and SM-C4. The outbox row is deleted once sent, so 4.6 adds the record (Space, kind, timestamp; never content or address) | low | AC5; Q4 **[LEE]** |
| `:754` | Issuing an Invitation has no rate limit, **for Lee or the PM to decide** | medium | Q10 **[LEE]** (recommended: re-own, do not build) |
| `:802` | Display names that look identical are not treated as a collision. Folding rule for **Lee/UX** | medium | AC6, AC14; Q7 **[LEE/UX]** |
| `:811` | The Assignee foreign key cannot be `ON DELETE SET NULL`. Unassign inside the removal's and the leave's transaction, before the `DELETE`, or the delete fails `547`. Add the case "an assigned Member is removed: Task unassigned, Task survives" | medium | AC8; Q5 |

**Not 4.6's, though nearby:** `deferred-work.md:819-835` (4.5 review deferrals owned by 4.5, 4.7, 7.7 or the sweep) stay where they are. Do not fold them in.

**Depends on** (all done):

- 2.2, 2.3, 2.4: Tasks, the merge-patch edit (`TaskEditBody`), Labels as the multi-id body precedent, and `InvalidTaskLabels`, which gives a random id and a foreign id the same bytes (I-13).
- 2.10: the List View, `ListSorts`, `ListSortColumns`, `ListReader`'s two-segment NULL-last pattern, `ListCursorText`, and `ListViewGateTests`.
- 4.1: the outbox (`IOutbox`, `OutboxKinds`, `OutboxStore`, `OutboxDispatcher`), `InvitationEmail`, `MailDrop`, and the outbox gates.
- 4.3, 4.4: Members seeded through accepted Invitations, `MembershipStore`, `MembershipLabels`, and the address and display-name gates.
- 4.5: `MembershipStore.RemoveCoreAsync` and `LeaveAsync` under `Yello/Memberships/{spaceId}`, and `What_the_removed_Member_created_survives_the_removal`.

**Explicitly not in this story:**

- **Account deletion's unassignment** (5.4). New ledger entry: 5.4 must unassign inside its own deletion sequence, for the same `SET NULL` reason.
- **The Assignee surviving a cross-Project move** (FR-41, story 6.5). The column simply travels with the row. 6.5 proves it, and **no notification** is sent on a move.
- **A pushed live update** of an assignment to other open Boards (Epic 7). Other tabs see it on their next read.
- **Notification preferences, digests or frequency control** (`prd.md:841`: "per-event and not configurable").
- **A product surface reading the send record.** None, by FS-NFR-2.
- **Confusable-character (TR39 skeleton) folding.** It needs a data table or a new package (Lee's approval). See Q7.
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits.
- **No new NuGet package and no new project without Lee** (CLAUDE.md).

## Story

As someone dividing up work,
I want to give a Task to someone in this Space and have them know,
so that responsibility is recorded where the work is rather than in a message.

## Acceptance Criteria

AC1 to AC7 are reproduced **verbatim** from `epics.md:1844-1875` and numbered by position. AC8 to AC14 come from the PRD, the spines, the ledger and a survey of the code at `4d23bf3`, and each one cites its source. The indented notes are implementation guidance. A note that depends on an open question names it. **[LEE]** marks a question that touches authority, isolation, a recorded Lee decision or a security seam. The orchestrator may route those to Lee before development. Otherwise the dev phase takes the recommendation and records it as a reversible decision.

**AC1 — only this Space's Memberships, by any route** (FR-21, FR-16, AD-3; I-15)
**Given** a Task
**When** an Owner, Admin or Member assigns it
**Then** only Memberships of that Task's own Space are offered as Assignees and only those are accepted, including via the API
**And** an Account with no Membership in the Space cannot be set as Assignee by any route

> - **The write route** (Q1 **[LEE]**). Recommended (a): `PUT /api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}/assignee`, a new `TaskRoutes.Assignee` / `AssigneeOf(spaceId, taskId)`.
>   - The body is `AssignTaskRequest(Guid? MembershipId)`. `null` unassigns. A missing member, or a body that is not an object, is `400 invalid-assignee`.
>   - `.RequiresCapability(Capability.AssignTask)`. This is the first use of the matrix's own row, so the transcription and the code agree by construction. `Every_spaceId_endpoint_declares_exactly_one_capability` forbids folding it into the PATCH as a second capability.
>   - The answer is `200 TaskResponse`, the edited Task. `Idempotency-Key` is handled by the pipeline. Both surfaces (AD-4).
>   - The PATCH (`TaskRoutes.Task`) is **unchanged**. `The_Task_routes_declare_their_capability...` (`RefusalBoundaryGateTests.TaskEdit.cs:129`) keeps exactly DELETE, GET and PATCH on that pattern, and `The_Task_edit_request_carries_its_editable_attributes_and_nothing_else` keeps `EditTaskRequest` at four members.
> - **Offered: the candidates read** (Q2). Recommended (a): `GET /api/v1/spaces/{spaceId:guid}/assignees` (`AssigneeRoutes.List` / `ListOf`) under **`ReadProjectsAndTasks`**.
>   - Every Role needs it: assigners for the picker, every Role for the card avatars and the List filter. `review-isolation.md:52` says offering the whole Space roster to every Role, Viewer included, is "upstream-sanctioned, not a defect".
>   - It answers `AssigneeListResponse(IReadOnlyList<AssigneeResponse> Assignees)`, where `AssigneeResponse(Guid MembershipId, string DisplayName)`. It carries **no address, no Role, no AccountId, no `SpaceId`**, and no paging: NFR-8 bounds a Space at 100 Memberships. Order by `(CreatedAt, Id)`, the Membership list's order.
>   - Read in `MembershipStore` (already in `DisplayNameReaders`), so no gate is widened.
>   - Add the new contracts to `The_Membership_contracts_carry_exactly_their_named_members`, or to a sibling gate. `Only_the_Membership_list_and_the_Invitation_list_carry_an_email_address` stays unwidened.
>   - The read is not a write. `The_leave_route_is_the_one_write_under_ReadProjectsAndTasks` stays green.
> - **Accepted: by construction, then by answer.**
>   - The composite foreign key (AC2) makes a foreign Membership unstorable.
>   - The guarded statement (Q3) also carries `EXISTS (SELECT 1 FROM dbo.[Membership] WHERE SpaceId = @space AND Id = @membership)`, so a foreign, random or just-removed id updates 0 rows.
>   - The answer is `400 invalid-assignee` (a new `ProblemTypes.InvalidAssignee`, `urn:yello:problem:invalid-assignee`), **byte-identical** for a random id and another Space's real Membership id. This is the `InvalidTaskLabels` precedent (I-13), and it says nothing about either.
>   - Never `404`: the Task exists, and "not-available" would read as the Task being gone.
>   - A Task id from another Space, or a random one, is the in-Space `404 not-available`, byte-identical to the boundary, unrecorded and floored (`NotAvailableFloor`).
> - **No 547 escapes as a `500` or as `LostSpace`'s boundary `404`.** The `EXISTS` clause runs under the shared Memberships lock (Q3), so the FK check cannot meet a concurrent delete. If a 547 is still caught, the store maps it to `invalid-assignee`, never to `LostSpace`. Record which path is reachable.
> - **An Account with no Membership:** the pipeline answers the boundary `404`, recorded `CrossSpace`, as for every route (nothing new). Isolation rows prove it on both surfaces (AC12).

**AC2 — the composite foreign key** (AR-8, AD-2; ledger `:811`)
**Given** the Assignee reference
**When** the schema is inspected
**Then** it is a composite foreign key carrying `SpaceId` — `(SpaceId, MembershipId)` — so the same-Space constraint holds by construction rather than by validation

> - **Column:** `TaskItem.AssigneeMembershipId` (`Guid?`), the name AD-5 uses. The ledger's `AssigneeId` is the same thing, so record the name chosen.
> - **FK:** `HasForeignKey(t => new { t.SpaceId, t.AssigneeMembershipId }).HasPrincipalKey(m => new { m.SpaceId, m.Id })`. The principal is `Membership`'s existing alternate key `(SpaceId, Id)` (`MembershipConfiguration.cs:59`, whose comment already names this use).
> - **`OnDelete(DeleteBehavior.NoAction)`.** Two reasons:
>   - `SET NULL` would null the non-nullable `SpaceId` (ledger `:811`).
>   - `CASCADE` would delete the Task, which is forbidden, and would add a second cascade path (SQL 1785, the reason `TaskConfiguration.cs:20-25` gives for Status).
>   - Remarks say why, without story numbers.
> - **Space deletion still works.** `SpaceStore.DeleteCoreAsync` is one `ExecuteDeleteAsync` on `Space`, which cascades both `Membership` and `Project → Task`. SQL Server checks a NO ACTION constraint at the end of the statement, after every cascade. `TaskLabel → Label` (NoAction, both cascaded from Space) is the working precedent. **Prove it:** a slice that deletes a Space holding an assigned Task answers `204` and leaves no rows.
> - **Gate:** `PersistenceModelGateTests.Every_reference_between_Space_scoped_entities_carries_SpaceId` (`:277`) already anticipates `(SpaceId, AssigneeId) → Membership(SpaceId, Id)` (`:264-270`), and must pass with the new key. **Plant:** a single-column FK `AssigneeMembershipId → Membership.Id`. The gate goes red.
> - **Schema case** (`TaskEndpointTests.LabelsSchema.cs` precedent): `sys.foreign_key_columns` shows the two columns in order `(SpaceId, AssigneeMembershipId) → (SpaceId, Id)`, with `delete_referential_action = 0` (NO ACTION). A raw `UPDATE` under Space A's key naming Space B's Membership id fails `547`, which proves the database refuses what the application never sends.
> - **Migration** `TaskAssignee` (expand-only, `TESTING-CONVENTIONS.md:565-585`): a nullable column, the FK, and the AC7 index. No backfill, because every existing Task is unassigned. Hand-fix the analyser warnings, never add `<auto-generated/>`, and update `MigrationBundleEquivalenceTests` and the snapshot as 2.10 did. `Task` already sits under `SpaceIsolationPolicy`, so no policy change is needed for the column.

**AC3 — a Viewer may be assigned; demotion keeps it** (FR-21; `role-capability-matrix.md:43`)
**Given** a Viewer
**When** they are assigned a Task
**Then** the assignment is permitted and grants them no write capability over it
**And** demoting an Assignee to Viewer does not unassign their Tasks, because responsibility and capability are deliberately separable

> - **Slices:**
>   - An Owner, an Admin and a Member each assign a Viewer: `200`. The assigned Viewer's PATCH of that Task is `403`, recorded `InsufficientRole`, and its own `PUT .../assignee` is `403` too (the matrix's Viewer cell).
>   - An Assignee demoted through 4.4's `PATCH .../memberships/{id}` keeps every assignment (`AssigneeMembershipId` unchanged).
>   - Seed the Viewer through an accepted Invitation (TESTING-CONVENTIONS: "seed through the product where the case is about Membership").
> - **The Role change writes no Task.** Assert that `MembershipStore.ChangeRoleAsync` issues no statement on `dbo.[Task]`. **Plant:** an unassign in the Role change. The demotion case goes red.
> - **Client:** the assigned Viewer's Task detail shows the Assignee read-only and no assign control (AC6, UX-DR27).

**AC4 — the email, never for one's own action** (FR-40, AD-10/AR-33, NFR-1, NFR-6; I-14; Q8, Q9)
**Given** an assignment to an Account other than the acting one
**When** it commits
**Then** that Account is notified by email naming the Space, Project and Task and nothing from any other Space
**And** an Account is never notified of its own action

> - **When an email is enqueued** (Q9). Only when the stored Assignee **changes to a Membership whose Account is not the actor's**. No email for:
>   - an unassign;
>   - assigning the Task to its current Assignee (a no-op that answers `200`);
>   - assigning oneself;
>   - an idempotent replay (the pipeline never re-runs the handler).
>
>   A reassignment from A to B tells B only.
> - **Same transaction** (AD-10): the handler enqueues through `IOutbox.EnqueueAsync(new OutboxEmail(OutboxKinds.TaskAssigned, ...))` after the guarded update, as `IssueInvitationHandler.cs:85-89` does. Add `OutboxKinds.TaskAssigned => "task-assigned"`. `OutboxStore` saves, then marks the commit signal, and the dispatcher sends after the commit only.
> - **Recipient's address:** read by `MembershipStore` (already in `AddressReaders`), returned to the handler only as a value for the outbox. **Never** in the response, a log, an exception message or the idempotency record (the response is `TaskResponse`, which has no address). `Only_named_types_read_an_Accounts_email_address` stays unwidened. Record the method name.
> - **Content** (Q8; `AssignmentEmail.Render`, a sibling of `InvitationEmail`, in `Yello.Application/Tasks/AssignTask/`):
>   - plain text, English, no exclamation marks;
>   - its parameters are strings and `Uri` only, held by a sibling of `The_invitation_email_template_takes_..._and_nothing_else` (`RefusalBoundaryGateTests.Invitations.cs:214`);
>   - recommended copy: subject "{Assigner} assigned you a Task in {Space}", body "{Assigner} assigned you "{Task title}" in the Project {Project} in the Space {Space} on Yello." followed by the client's base URL. **No ids in the link** and no other Space's name.
>   - The assigner's name is read at authoring time (4.4 AC7 permits a name captured when authored), through `MembershipStore`.
> - **Nothing from any other Space:** the Space, Project and Task names are read under the request's Space key only. **I-14 proof (Slices):** the recipient is a Member of two Spaces with distinctively named Projects and Tasks. The one email to its address (`WaitForEmailToAsync`) contains this Space's three names and **none** of the other Space's names, contains no address, and contains no password or Token (NFR-6). The case uses `HostProcess.RunAsync` plus `MailDrop` (a pooled Host cannot read mail, `TESTING-CONVENTIONS.md:517-543`). Assert "one email to this address", never "one email".
> - **Not for one's own action:** a Member assigning itself enqueues nothing. Prove it by the absence of mail to its address, anchored by a later assignment by someone else that does arrive.
> - **Rollback takes the email:** with a forced rollback (the `AFTER INSERT` trigger on `IdempotencyRecord`, `TESTING-CONVENTIONS.md:517-543`) there is no outbox row and no email, anchored by a later send that does go out.
> - **Logs:** nothing new beyond the outbox's id-only events. `No_log_message_takes_an_address_a_token_or_an_email_body` stays green. No Task, Project or Space name appears in a log (AR-34, I-17).

**AC5 — the send record** (FS-NFR-2, SM-C4; ledger `:736`; Q4 **[LEE]**)
**Given** a notification send
**When** the record is inspected
**Then** it retains the Space, the kind and the timestamp, and never message content or recipient address
**And** no product surface reads it, its purpose being to keep SM-C4 derivable

> - **The constraint:** the dispatcher runs with **no session key**, so it cannot insert into a table under `SpaceIsolationPolicy` (BLOCK AFTER INSERT). `Exactly_one_production_line_sets_a_session_context_key` forbids a second setter, and the outbox row has no Space column by Lee's 4.1 Q6.
> - **Recommended (a): write the record at enqueue, in the request's transaction, under the policy.**
>   - New Space-scoped table `NotificationSend(Id, SpaceId, Kind, RecordedAt)`. `Kind` is `nvarchar(64)` from `OutboxKinds`. `RecordedAt` is UTC `datetimeoffset` (AR-34).
>   - There is no recipient, no subject, no body, no Task id and no Account id.
>   - Written by `OutboxStore.EnqueueAsync` beside the outbox row (`dbContext.ScopedSpaceId` is set in every Space-scoped request), so it covers **every** notification kind, the Invitation email included, which SM-C4 counts.
>   - It commits or rolls back with the triggering write.
>   - Every enqueue today runs in a Space-scoped request. If `ScopedSpaceId` is null, throw rather than write a record with no Space. Add a gate or a unit case so a future Account-scoped enqueue (for example 5.1's offer email, if it is ever sent outside a Space request) meets that decision deliberately.
>   - FK to `Space` `ON DELETE CASCADE`, as for every Space-scoped table (Q4 sub-question).
>   - Policy: FILTER plus BLOCK AFTER INSERT and BLOCK AFTER UPDATE, added in the migration with literal names (the `20261025175001_LabelTables.cs:105-128` pattern, DROPs in `Down`). Append to `SchemaNames.SpaceScopedTables` and to `SpaceIsolationSchemaTests.TablesThatCannotMove`. A query filter `SpaceId == ScopedSpaceId`. `SchemaVarianceTests` needs no variance.
>   - Cost: it records an **enqueue**, not a confirmed delivery. A row abandoned after ten attempts (4.1 D2, event 1045) still counts. State this in the table's remarks and in a dated note to AD-10.
> - (b) A policy-free table written by the dispatcher after the send, with `SpaceId` carried on the outbox row. Cost: it reverses Lee's 4.1 Q6 (no Space column on the outbox) and needs two named variances in `SchemaVarianceTests`.
> - (c) Record the assignment kind only. Cost: SM-C4 undercounts. The Invitation email is a notification too.
> - **No reader:** a gate holds that only `OutboxStore`, the entity, its configuration and `YelloDbContext` refer to `NotificationSend` (the `Only_the_outbox_store_its_dispatcher_and_its_configuration_refer_to_the_outbox_row` shape). No endpoint, no client code. **Plant:** a read of it in a slice. The gate goes red.
> - **Proof (Slices):**
>   - after an assignment commits, one `NotificationSend` row in the Space, `Kind = "task-assigned"`;
>   - after an Invitation issue, one with `"invitation-issued"`;
>   - after a rolled-back assignment, none;
>   - the table's columns are exactly `Id, SpaceId, Kind, RecordedAt`, checked against `INFORMATION_SCHEMA`, which proves "never content or address";
>   - the row is invisible under another Space's key.

**AC6 — the picker shows a name and initials, never an address** (UX-DR11, UX-DR34, AD-23; ledger `:802`)
**Given** the Assignee picker
**When** it renders
**Then** each Membership shows a display name and initials only, and never an email address

> - **Placement:** an "Assignee" attribute in `TaskDetail.razor`, beside Status, Due and Labels (`:105-211`). It is a **single-select** listbox built on `PickerOption` (the Status picker's pattern):
>   - the first option is "Unassigned", then every candidate;
>   - each option shows an avatar (initials) and its label;
>   - the current Assignee is marked `aria-selected`, and **no option is pre-selected as a default on open** (UX-DR11);
>   - Esc closes and restores focus to the control;
>   - roving tabindex and keyboard choice follow the existing picker.
> - **Labels:** `MembershipLabels.Disambiguate` over the **whole** candidate list, so a collision is detected Space-wide, never per page. Initials come from `MembershipLabels.Initials`. Both are client-side from `AssigneeResponse.DisplayName`. The discriminator is the existing "Name · 3F2A" (4.4 Q13).
> - **Look-alike names (ledger `:802`, Q7 [LEE/UX]):** recommended (a), widen the collision key in `MembershipLabels` from "NFC, ordinal" to: NFC, remove default-ignorable code points (category `Cf`, which takes in zero-width characters), trim, collapse internal whitespace runs to one space, then compare ordinal-ignore-case.
>   - The **shown** name stays as the person wrote it. Only the comparison folds.
>   - The Membership list uses the same function, so it changes too. Name that in the remarks.
>   - Confusables (TR39) are out of scope. Re-own them to Lee/UX in the ledger.
>   - Unit cases go in the Architecture project (the only home for a client/contract unit test, `TESTING-CONVENTIONS.md:34-42`): "Ann" vs "Ann ", "Ann" vs "A​nn" (a zero-width space), "ann" vs "Ann", and "Ann  Lee" vs "Ann Lee" all collide and all get discriminators. "Ann" vs "Anne" does not collide.
> - **Absent for a Viewer:** the control is rendered only when `TaskCapabilities.PermitsAssignTask(role)` holds. A sibling agreement gate of `The_clients_Task_controls_predicate_agrees_with_the_capability_matrix` (`RefusalBoundaryGateTests.Tasks.cs:188`) holds it to `Capability.AssignTask`. The Viewer's read-only `<dl>` (`TaskDetail.razor:214-237`) shows "Assignee" with the label, or "Unassigned".
> - **No address anywhere:** the picker, the card avatar's accessible name, the List filter, the List row and every announcement contain no `@` (client cases, each with a no-`@` assertion). The avatar has **no `title` tooltip**. Its accessible name is "Assigned to {label}".

**AC7 — the List View's Assignee dimension** (FR-30, AD-29, UX-DR19; P-3, P-4; ledger `:628`; Q6)
**Given** the List View
**When** filter and sort render
**Then** the Assignee dimension is now present with its supporting composite index, completing FR-30
**And** filtering by Assignee offers only Memberships of the active Space

> - **Sort** (Q6 [UX]). Recommended (a): sort by `AssigneeMembershipId`, which **groups each person's Tasks together**, with Unassigned fixed **last**, and `TaskId` as the tiebreaker.
>   - Index `IX_Task_SpaceId_ProjectId_AssigneeMembershipId_Id` on exactly `(SpaceId, ProjectId, AssigneeMembershipId, Id)`, `INCLUDE (StatusId, DueDate)` like the Label sort's. `ListViewGateTests` requires exactly that key.
>   - The order between people is stable but not alphabetical. State it in `ListSorts`' remarks.
>   - (b) A denormalised name key on `Task`, like `FirstLabelName`. Cost: a live display-name read outside `DisplayNameReaders`, and every future name change would rewrite Tasks in every Space the Account is in (a cross-Space write).
> - **Wiring:**
>   - Code `ListSorts.Assignee => "assignee"`; `ListSort.Assignee`; `ListSortCodes`; `ListSortColumns.ByCode["assignee"] = nameof(TaskItem.AssigneeMembershipId)`.
>   - `ListCursor` gains `AssigneeMembershipId?`. `ListCursorText` encodes the segment as `v` plus a Guid, or `n` for unassigned, following the due-date and Label shape. A cursor issued for another sort stays `400 invalid-list-cursor`.
>   - `ListReader`: a `ByAssigneeAsync` with the two-segment pattern (assigned, then unassigned) and the index hint constant (`:48-52`).
>   - Replace the remarks "Exactly three, and no Assignee" (`ListSorts.cs`), and the comments at `TaskListView.razor:12` and `TaskListPage.razor:4`, so they say what exists.
> - **Filter:**
>   - `ListRoutes` gains an `assignee` parameter (repeatable, Membership ids) and an `unassigned` flag (Q6 sub-question: recommended, a separate flag like `dueNone`).
>   - `ListQuery` and `ListFilter` gain `AssigneeMembershipIds` and `Unassigned`. `MatchesNothing` includes the Assignee set.
>   - `ViewListHandler` narrows the ids to visible Memberships (the `VisibleLabelIdsAsync` precedent, `ViewListHandler.cs:39`). A foreign or random id **drops out and matches nothing**, never a refusal, with bytes identical for both (AC15 of 2.10).
>   - `ListReader.Matching` applies `AssigneeMembershipId IN (...)` OR (`Unassigned` AND `AssigneeMembershipId IS NULL`).
> - **Existing tests to change:**
>   - `ListViewGateTests.The_offered_sorts_are_exactly_three_and_none_is_the_Assignee` (`:101`) becomes "exactly four, Assignee among them".
>   - `ListPagingTests` LV-10's `[InlineData("sort=assignee")]` (`:517-524`) now expects `200`.
>   - P-3's sort count reaches the design's "5 sorts" figure only with the Board order. Record the count.
> - **NFR-5 at 5,000** (2.10 AC10): extend LV-19's plan read (a seek on the new index) and the logical-reads case (last page no more than first page, within tolerance 5) to the Assignee sort, unfiltered and filtered. Seed about a third of the Tasks unassigned, with ties among the assigned. Timing stays in `ListScaleTimingTests` (Nightly tier).
> - **Client** (`TaskListView.razor`, `TaskListFilters.cs`, `TaskListRow.razor`):
>   - an "Assignee" fieldset of `TaskListChoice` checkboxes, "Unassigned" first, then each candidate's label;
>   - an "Assignee" sort option;
>   - an "Assignee" column showing the avatar and label, or "Unassigned".
>   - The candidates come from AC1's read and are purged with the Space (UX-DR31). Filter state is never persisted, and no name reaches the URL or `document.title` (2.10 AC17).
>   - A Viewer gets exactly a Member's filters and sort (2.10 AC16).

**AC8 — removal and leaving unassign, inside their transaction** (FR-21, FR-14, AD-5; ledger `:811`; Q5)

> - In `MembershipStore.RemoveCoreAsync` and `LeaveAsync`, **after** `TakeMembershipsAsync` and **before** the guarded `DELETE`, run one statement: `UPDATE dbo.[Task] SET AssigneeMembershipId = NULL WHERE SpaceId = @space AND AssigneeMembershipId = @target`.
>   - For the leave, `@target` is the caller's Membership id, read under the lock.
>   - **Guard it like the delete:** run it only when the same conditions hold. Either fold both into one batch that first checks the guarded predicate, or run the unassign and then the `DELETE`, letting a 0-row `DELETE` roll the unassign back through the unit of work. The pipeline rolls back every non-success outcome, so verify the rollback actually happens on `OwnershipHeld`, `BeyondAuthority`, `NotFound` and `LostMembership`.
>   - **Prove it:** a refused removal (an Admin on another Admin) leaves that Admin's Tasks assigned.
> - **Why the shared lock matters** (Q3): an assignment that committed between the unassign and the `DELETE` would make the `DELETE` fail `547`. Assignment takes `Yello/Memberships/{spaceId}` in **Shared** mode, and the removal holds it Exclusive, so the two serialise.
> - **Cases** (Slices, beside `What_the_removed_Member_created_survives_the_removal`, `ActiveSpaceEndpointTests.RemoveMembership.cs:651`, whose remarks "No Task carries an Assignee yet" are rewritten):
>   - "An assigned Member is removed: the Task is unassigned and survives". Counts are unchanged, `AssigneeMembershipId IS NULL`, and other Assignees' Tasks are untouched.
>   - The same for a leave (`ActiveSpaceEndpointTests.LeaveSpace.cs`).
>   - **No email** on unassignment.
>   - After the removal, the candidates read no longer lists the person.
> - **Plant:** the unassign statement removed. The removal of an assigned Member answers `500` (547 under `LostSpace`, or a raw 547) and the case goes red. Record what was seen.
> - Update `MembershipStore`'s remarks and the AD-5 note (dated) to say that the unassignment runs in the removal's own transaction.

**AC9 — idempotency, races and the lock** (AD-18; `TESTING-CONVENTIONS.md:102-180`; Q3)

> - **Replay:** the same `Idempotency-Key` after a committed assignment returns the original `200`, sends **no second email**, and writes no second send record. A different body under the same key is `422`. A malformed key is `400`. `Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay` stays green.
> - **The statement** (Q3): under `TakeMembershipsSharedAsync` (a new `ApplicationLocks` helper: `sp_getapplock` with `@LockMode = 'Shared'` on the same `MembershipsOf(spaceId)` name):
>
>   ```
>   UPDATE dbo.[Task] SET AssigneeMembershipId = @membership
>   OUTPUT deleted.AssigneeMembershipId
>   WHERE SpaceId = @space AND Id = @task
>     AND (@membership IS NULL OR EXISTS (SELECT 1 FROM dbo.[Membership]
>          WHERE SpaceId = @space AND Id = @membership))
>   ```
>
>   - On 0 rows, a disambiguation read decides between the in-Space `404` (no such Task) and `400 invalid-assignee`.
>   - The `OUTPUT` gives the previous Assignee, which decides AC4's "changed" (Q9).
>   - Two assignments of one Task serialise on the Task row (last writer wins, as every Task edit does). Two assignments in one Space do not wait on each other, because Shared is compatible with Shared.
> - **Race 1, assign against a removal of the target:** hold `Yello/Memberships/{spaceId}` Exclusive in a test transaction, send the assignment to Member M, and wait until it is blocked (`WaitUntilBlockedOnALockAsync`). Remove M in the holder's transaction, then commit. The answer is `400 invalid-assignee`, the Task is unassigned, and there is no `500` or email. **Plant:** the assignment skips the lock. The case either goes red or is recorded masked with its reason.
> - **Race 2, removal waits for an in-flight assignment:** hold the lock Shared (as an assignment would), send the Owner's removal of M, wait until blocked, assign M in the holder's transaction, then commit. The removal answers `204`, and M's Task, assigned by the holder, is **unassigned** (no 547). This is the deciding race for AC8. **Plant:** the removal's unassign removed. The removal fails.
> - **Race 3, two assignments in one Space proceed together:** hold the lock Shared, then an assignment of another Task answers **without** waiting (`WaitUntilBlockedOrAnsweredAsync`).

**AC10 — the Board and the Task carry the Assignee** (UX-DR8, `DESIGN.md:451`, `:464`; AD-19)

> - **Contract:** `TaskResponse` gains `Guid? AssigneeMembershipId` (`init`, default `null`), appended (AD-19). No name and no address. The Board, the List and the Task read all carry it, because they reuse `TaskResponse` (`ListViewGateTests:146`).
>   - Widen the member-set gates: `RefusalBoundaryGateTests.TaskEdit.cs:68`, `RefusalBoundaryGateTests.Labels.cs:134` (TaskResponse).
>   - `RefusalBoundaryGateTests.Tasks.cs:139` (no `SpaceId`) stays green.
>   - Update the contract snapshot (C-1/C-2).
> - **Card:** `TaskCard.razor`'s metadata row (`:76-94`) gets the avatar pushed right (`margin-inline-start: auto`), from the Space's candidate list by `AssigneeMembershipId`.
>   - Add the label/initials to `CardInputs` (`:256`, `:336`), or the card never redraws.
>   - Unassigned shows nothing.
>   - An id not in the list (a Membership that joined after the list was read) triggers one re-read of the candidates. If it is still absent, the card shows no avatar. Never show an id.
>   - Replace the comment at `TaskCard.razor:13` and `TaskDetail.razor:23`.
>   - Keep the 320px wrap rule and `TextSpacingTests`.
> - **Board host:** `BoardView` gains `TaskAttribute.Assignee` with an optimistic replica update and its own send (`PUT TaskRoutes.AssigneeOf`, not the PATCH), using the existing per-attribute queue and one `Idempotency-Key` per deliberate choice, kept for an identical resend after no answer.
>   - On `400 invalid-assignee`: revert, re-read the candidates, and say "That person is no longer in this Space." Then focus the picker control.
>   - On `403`: the Role re-read path (`OnRoleDoubted`).
>   - On `404`: the Task-gone path the PATCH uses.
> - **Announcement** (polite, UX-DR38): "Assigned to {label}." or "Unassigned." No address and no Space name.
> - **Fake API** (`FakeYelloApi.cs`): `FakeTask` gains `AssigneeMembershipId` (`JsonIgnore WhenWritingNull`, like `LabelIds`). The fake answers `GET .../assignees` and `PUT .../assignee` exactly as the Host does, using contract constants only, with hold and refusal hooks following `HoldTaskEditAsync` and `NextTaskEditRefusal`.

**AC11 — the client's Assignee list is Space-scoped state** (UX-DR31; `EXPERIENCE.md:164`)

> - The candidates are held per Space and purged synchronously on a switch, sign-out or Space loss, before any render. Nothing is written to `localStorage`, `sessionStorage` or IndexedDB (`ClientStorageBanTests`), and there is no mutable static state (`ClientStaticStateGateTests`).
> - **Browser case** (the `SpaceSwitchMemoryTests` style): after a switch from a Space with assigned Tasks, no label or initials from the departed Space's candidates is in the DOM, and the first Board request in the new Space is followed by a candidates read **for the new Space only**.

**AC12 — isolation rows on both surfaces** (AD-3, AD-4, I-1, I-13, I-14, I-15, I-17; `TESTING-CONVENTIONS.md:756-877`)

> The two new routes fail the route-coverage gate until they have rows. Add a new partial `IsolationCases.Assignment.cs`, spread from `All`:
>
> - `ISO-ASSIGNEES-FOREIGN-SPACE` and `ISO-ASSIGN-FOREIGN-SPACE`: boundary `404`, byte-identical, recorded `CrossSpace`.
> - `ISO-ASSIGN-FOREIGN-MEMBERSHIP` (I-15, the `ISO-APPLY-FOREIGN-LABEL` template, `IsolationCases.cs:600-616`): another Space's real Membership id in one's own Task's body gives `SameAsAnchor(400, InvalidAssignee)` against a random id, `Then` = no Task in either Space is assigned.
> - `ISO-ASSIGN-FOREIGN-TASK-IN-OWN-SPACE`: another Space's Task id gives the in-Space `404`, byte-identical to the boundary, unrecorded.
> - `ISO-ASSIGNEES-ONLY-THIS-SPACE` (I-15, aggregate): an Account in two Spaces reads the candidates of one. The set is exactly that Space's Memberships, and contains none of the other's names (`HoldsSecret`).
> - `ISO-LIST-FOREIGN-ASSIGNEE-FILTER` (I-15, the `ISO-LIST-FOREIGN-FILTER-IN-OWN-SPACE` template, `:424`): a foreign Membership id in `assignee=` matches nothing, with bytes identical to a random id.
> - `ISO-RECORD-KIND-ASSIGN-VIEWER`: a Viewer's assignment gets `RoleRefused("assign-task")`, recorded `InsufficientRole` (the `ISO-RECORD-KIND-LABEL-APPLY-VIEWER` template). Confirm the capability code string the pipeline emits.
> - `ISO-TOKEN-OWN-OTHER-SPACE-ASSIGN` and `-ASSIGNEES`: the 3.4 `TokenOwnOtherSpace` gate requires one per route a Token reaches.
> - **I-14 on the isolation suite** (if the suite can read mail through `HostProcess`; otherwise the Slices case of AC4 is the proof and that is recorded): an assignment email to a two-Space Member carries no other Space's names.
> - Every actor's secrets hold every seeded name and address (`HoldsSecret`), so the per-row I-17 log check fails on any name or address in the Host log.
> - Rows created `PendingUntil: "4.6"` by ATDD must all be live before review (GATE-003, `IsolationCaseListTests:110`).
> - **Timing:** both 400s are byte-identical bodies with no existence branch, the in-Space `404` goes through `NotAvailableFloor`, and the `EXISTS` clause does the same work for a random id and a foreign id (the FILTER predicate hides the foreign row). Add no `IsolationTimingTests` row unless measurement says otherwise. Record it.

**AC13 — gates for the new seams** (AR-3, AD-1, AD-20, AD-23)

> - `Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context` and `No_slice_or_endpoint_reads_the_Role_off_a_Membership_row` stay green. The assign slice reads no Role: the capability decides.
> - New gate: the `AssignTask` capability is required by exactly the assignee route (a per-route map, the `The_Membership_routes_declare_one_capability_each...` style).
> - New gate: `AssignmentEmail.Render` takes strings and `Uri` only (AC4).
> - New gate: the send record has no reader (AC5).
> - `Only_the_outbox_store_its_dispatcher_and_its_configuration_refer_to_the_outbox_row` stays unwidened. The handler goes through `IOutbox`.
> - The endpoint's outcome switch has **no `_ =>` arm** that falls into a success or a `404` (4.1 P10).
> - `No_production_code_runs_on_a_timer_or_as_a_hosted_service` stays green.
> - `SpaceBoundaryPipelineTests` and `ActiveSpaceUnitOfWorkTests` register any new pipeline dependency (the 4.4 lesson: 19 cases broke on a missing registration).

**AC14 — the Membership list's discriminator changes with the fold** (ledger `:802`; Q7)

> - If Q7 (a) is taken, `MembershipSection` and `MembershipRow` (4.4) show a discriminator for look-alike names too. Extend `MembershipSectionTests` with one look-alike pair ("Ann" and "Ann " with a trailing space), both shown with a discriminator and neither with an address.

## Tasks / Subtasks

- [ ] **Task 0: decisions** (before any code)
  - [x] Q1, Q4, Q7 and Q10 are marked **[LEE]**. If the orchestrator routes them to Lee, record the answers here under "Decided by Lee" before ATDD. Otherwise take each recommendation and record it as "Qn - (x)" with its cost in Completion Notes.
    - **Decided by Lee (2026-11-08):**
      - Q1 - (a): a separate `PUT .../tasks/{taskId}/assignee` route under `Capability.AssignTask`, answering `200 TaskResponse`. The PATCH and its gates stay unchanged.
      - Q4 - (a): a Space-scoped `NotificationSend` table under the policy, written by `OutboxStore` at enqueue in the request's transaction, for every kind. Retention sub-question: (a), cascade with the Space.
      - Q7 - (a): the collision key is NFC, then strip `Cf` code points, trim, collapse whitespace, and compare ordinal-ignore-case. Confusables return to the ledger, owned by Lee/UX.
      - Q10 - (a): no Invitation rate limit in this story. The ledger entry is re-owned to Lee/PM.
  - [ ] Decide Q2, Q3, Q5, Q6, Q8, Q9 and Q11 the same way. Each recommendation is the default.
- [ ] **Task 1: Domain** (AC2, AC3)
  - [ ] `TaskItem.AssigneeMembershipId` (`Guid?`). `ITaskStore` (or a new `ITaskAssignments` port, if S1200 or cohesion argue for it) gains `AssignAsync(taskId, membershipId?) → AssignOutcome(Assigned(previous), NoSuchTask, InvalidAssignee)`.
  - [ ] Update `Capability.AssignTask`'s summary to say what uses it. **No story ids in new comments** (CLAUDE.md).
- [ ] **Task 2: Contracts** (AC1, AC5, AC7, AC10)
  - [ ] `TaskRoutes.Assignee` / `AssigneeOf`; `AssignTaskRequest(Guid? MembershipId)`; `TaskResponse.AssigneeMembershipId` (appended).
  - [ ] `AssigneeRoutes.List` / `ListOf`; `AssigneeListResponse`, `AssigneeResponse(MembershipId, DisplayName)`.
  - [ ] `ProblemTypes.InvalidAssignee`, with remarks on its status and its non-disclosure.
  - [ ] `ListSorts.Assignee`; `ListRoutes` `assignee` and `unassigned` parameters and `ListOf`; `ListQuery` members.
  - [ ] `MembershipLabels`: the widened collision key (Q7), with its remarks updated.
- [ ] **Task 3: Application** (AC1, AC4, AC9)
  - [ ] `Yello.Application/Tasks/AssignTask/AssignTaskHandler.cs`:
    1. Assign through the store.
    2. On `Assigned(previous)` where previous differs from new and new is non-null, read the notification facts (the recipient's AccountId and address, the actor's name, and the Space, Project and Task names) through one port method on `IMembershipStore` (implemented in `MembershipStore`; Q8).
    3. If the recipient Account is not the actor, render `AssignmentEmail` and enqueue `OutboxKinds.TaskAssigned`.
    4. Return the Task.

    The handler reads no Role, counts nothing and records nothing (AR-3).
  - [ ] `Yello.Application/Tasks/AssignTask/AssignmentEmail.cs`, the `OutboxKinds.TaskAssigned` constant, and `Yello.Application/Memberships/ListAssignees/ListAssigneesHandler.cs`.
  - [ ] `ViewListHandler`: narrow Assignee ids to visible Memberships. Update `ListFilter`, `ListCursor`, `ListSort`, `ListSortCodes` and `ListCursorText`.
- [ ] **Task 4: Infrastructure** (AC2, AC5, AC7, AC8, AC9)
  - [ ] `TaskConfiguration`: the column, the composite FK (NoAction) and the Assignee list index. Remarks give the why.
  - [ ] `NotificationSend` entity and configuration (query filter, FK to Space cascade), `OutboxStore` writes it, and `SchemaNames.SpaceScopedTables` gains it (Q4 (a)).
  - [ ] `ApplicationLocks.MembershipsShared` taking `TakeMembershipsSharedAsync` (`@LockMode = 'Shared'`). Generalise `TakeAsync` to take a mode. Update the class remarks.
  - [ ] The guarded assign `UPDATE ... OUTPUT` plus its disambiguation read. The 547 mapping (AC1).
  - [ ] `MembershipStore`: `ListAssigneesAsync`, the notification-facts read, and the unassign in `RemoveCoreAsync` and `LeaveAsync` (AC8). Watch S1200, and split by port if it fires (4.5 split the endpoints the same way).
  - [ ] `ListSortColumns`, `ListReader.ByAssigneeAsync`, the index hint, and `Matching`.
  - [ ] One migration `TaskAssignee`: the column, the FK, the index, the `NotificationSend` table and its three predicates. Expand-only. Update the snapshot and `MigrationBundleEquivalenceTests`.
- [ ] **Task 5: Host** (AC1, AC12, AC13)
  - [ ] `TaskAssigneeEndpoints.MapAssignTask` (`PUT`, `AssignTask`) with a body reader that rejects a non-object body and a missing `membershipId` (`invalid-assignee`), and an explicit outcome switch. `AssigneeEndpoints.MapListAssignees` (`GET`, `ReadProjectsAndTasks`). Map both in `Program.cs` beside the Task and Membership routes, and update the route comment block.
  - [ ] `ListQueryReading`: the `assignee` and `unassigned` parameters.
- [ ] **Task 6: Client** (AC6, AC7, AC10, AC11)
  - [ ] Files:
    - `TaskDetail.razor` (the Assignee attribute and picker, the Viewer row);
    - a new `AssigneeAvatar.razor` (shared by card, detail, List row and picker);
    - `TaskCard.razor` (`CardInputs`);
    - `BoardView.razor(.cs)` (`TaskAttribute.Assignee`, `SendAsync` for the PUT, candidates load and purge);
    - a per-Space candidates holder in `Yello.Client/Spaces/` (purged with the Space);
    - `TaskCapabilities.PermitsAssignTask`;
    - `TaskListView.razor(.cs)`, `TaskListFilters.cs`, `TaskListRow.razor`;
    - `RefusalCopy.cs` (`invalid-assignee`);
    - `ClientCopy.resx` (each string with a `<comment>`);
    - `components.css` (logical properties, no px or colour literals, avatar per `DESIGN.md:188-193`).
  - [ ] Copy (Q11): see the open question.
- [ ] **Task 7: tests and plants** (all ACs). See Testing requirements. Save each original in memory and restore it in a `finally`. **Never** `git checkout --`, `git restore` or `git reset`. Record `| Plant | Expected red | Seen |` in the Debug Log.
- [ ] **Task 8: ledger, docs, verification**
  - [ ] Apply the ledger dispositions below: one line per CLOSED or UPDATED note, no TAB or FORM FEED, and keep the final newline.
  - [ ] Dated notes in `ARCHITECTURE-SPINE.md`:
    - AD-5: the unassignment runs explicitly inside the removal and the leave, under the Memberships lock, because `SET NULL` cannot apply to a composite key carrying `SpaceId`;
    - AD-10: the send record is written at enqueue, Space-scoped (Q4);
    - AD-29: the Assignee sort groups by Membership with Unassigned last (Q6).
  - [ ] Rewrite comments that predict "4.6" so they say what exists: `MembershipConfiguration.cs:59`, `MembershipLabels.cs` remarks, `ListSorts.cs`, `TaskCard.razor:13`, `TaskDetail.razor:23`, `TaskListView.razor:12`, `TaskListPage.razor:4`, `ActiveSpaceEndpointTests.RemoveMembership.cs:643-647`, and `PersistenceModelGateTests.cs:264-270` if it reads as a prediction.
  - [ ] `sprint-status.yaml`: this story goes to `review` at the end of dev, and `epic-4` stays `in-progress`. In the header's staged-FR block, mark FR-30 as completed by 4.6, keeping the line for history.
  - [ ] Verification follows CLAUDE.md's policy:
    1. `docker info --format '{{.ServerVersion}}'`.
    2. `dotnet build Yello.slnx` (0 warnings, 0 errors).
    3. Iterate on the affected classes with `--filter-class "<full name>"`.
    4. Make **exactly one** full run, in the background, without the browser suite: `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`, logged to `artifacts/test-runs/4-6-assign-a-task-and-be-told-develop.log`. Record the totals.
    5. Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

A Task gains a nullable `AssigneeMembershipId` whose foreign key is `(SpaceId, AssigneeMembershipId) → Membership(SpaceId, Id)` with NO ACTION. The same-Space rule therefore holds in the schema, and removal can neither null the `SpaceId` nor delete the Task. Assignment is its own route under the matrix's own `AssignTask` row. It is one guarded `UPDATE` under the Space's Memberships lock taken **Shared**, so assignments never wait on each other but always serialise against a removal, a leave or a Role change, which hold it Exclusive. A foreign, random or just-removed Membership id is one byte-identical `400`. When the Assignee changes to someone other than the actor, the handler enqueues one plain-text email in the same transaction through the existing outbox. The email names this Space, Project and Task, and the assigner. The outbox store also writes a Space-scoped send record holding the Space, the kind and the time, never content or address, which keeps SM-C4 derivable for every notification kind. Removal and leaving unassign the person's Tasks inside their own transaction, before the `DELETE`. A new address-free candidates read, open to every Role, feeds the picker, the card avatars and the List View's new Assignee filter and sort. The sort groups by Membership, puts Unassigned last, and is backed by its composite index. The client shows a name and squared monospace initials everywhere, never an address. Look-alike names now collide and get a discriminator.

### What exists today (survey at `4d23bf3`)

- **Domain:**
  - `TaskItem(Id, SpaceId, ProjectId, StatusId, Title, DueDate, PositionKey, CreatedAt, FirstLabelName)`, with no navigations. `FirstLabelName` is the precedent for an Infrastructure-maintained sort key.
  - `TaskEdit` uses `Changes*` flags. `ITaskStore` (`CreateAsync`, `FindAsync`, `EditAsync`, `ReplaceLabelsAsync`, `LabelIdsOfAsync`, `DeleteAsync`, `MoveAsync`).
  - `Membership(Id, SpaceId, AccountId, Role, CreatedAt)`. **There is no name on Membership.** The name is `Account.DisplayName`, read live by allow-listed stores. **No display-name change exists anywhere in the product** (registration sets it once).
  - `Capability.AssignTask` (`Capability.cs:52`) is unused.
  - `ListSort { Status, DueDate, Label }`, `ListFilter`, `ListCursor`.
- **Infrastructure:**
  - `TaskConfiguration`:
    - alternate key `(SpaceId, Id)` (`:77`);
    - FKs to Project (Cascade) and Status (NoAction, the 1785 reason at `:20-25`);
    - the list indexes (`:102-109`).
  - `MembershipConfiguration`: alternate keys `(SpaceId, Id)` (`:59`, comment naming this story's use) and `(SpaceId, AccountId)` (`:91`); the Owner filtered index; Space cascade; Account restrict.
  - `TaskStore.ApplyAsync` (`:149-202`, `READCOMMITTEDLOCK` read, Status lock, `SaveChangesAsync`); `ReplaceAsync` (`:216+`); `RecomputeFirstLabelNameAsync` (`:333`). No rowversion anywhere: locks and last-writer-wins.
  - `ApplicationLocks`: every lock is Exclusive (`TakeAsync`, `:197-204`). `MembershipsOf(spaceId)` (`:105`).
  - `MembershipStore`:
    - `ListAsync` (`:71-114`, keyset `(CreatedAt, Id)`, joins Account);
    - `LeaveAsync` (`:175-201`);
    - `RemoveCoreAsync` (`:209-253`): lock, `TargetAsync`, `GuardedDeleteAsync` (`:260-284`), then `RemovedAsync` (`:290-301`: the presence delete and `membershipChanges.Record`).
  - Outbox:
    - `OutboxStore` (adds the row, `SaveChangesAsync`, `signal.MarkEnqueued()`);
    - `OutboxMessage` (no Space column, deleted after send);
    - `OutboxDispatcher` (a singleton with its own scope and **no session key**, `MaxAttempts = 10`);
    - `OutboxLog` (event ids 1041-1046, id-only).
  - `FileDropEmailSender` writes `.json` to `artifacts/mail-drop` in Development.
  - `ListSortColumns.ByCode`; `ListReader` (hints `:48-52`, `PageAsync` `:86-94`, two-segment NULL-last `:235-282`, `Matching` `:312-346`).
  - RLS: `fn_SpaceIsolationPredicate`, `SpaceIsolationPolicy`, `SchemaNames.SpaceScopedTables` (`:236`).
  - Newest migration: `20261102085905_MembershipBlockAfterUpdate`.
- **Application:**
  - `EditTaskAttributesHandler`.
  - `IssueInvitationHandler.cs:85-89` (render, then `outbox.EnqueueAsync`).
  - `InvitationEmail.Render(...)`.
  - `OutboxKinds.InvitationIssued`.
  - `ViewListHandler.cs:39` (`VisibleLabelIdsAsync` narrowing).
  - `ListCursorText` (v1 text format).
- **Host:**
  - `TaskEndpoints` (`MapEditTask` PATCH under `ManageTasks`, `EditAsync` `:172-200`, `Response` `:208`), `TaskEditBody`, `TaskMoveEndpoints`.
  - `RequiredCapability.cs:45-54` (exactly one capability).
  - `MembershipEndpoints`, `MembershipRemovalEndpoints`, `MembershipRoleChangeEndpoints`.
  - `ListEndpoints` (`ReadProjectsAndTasks`), `ListQueryReading.TryReadQuery` (`:42`).
  - The idempotency pipeline (`ActiveSpaceResolution.Idempotency.cs`, `IdempotentResponses.cs`).
- **Contracts:**
  - `TaskRoutes` (`Create`, `Task`, `Move`), `EditTaskRequest` (four members), `TaskResponse` (seven members).
  - `MembershipRoutes`, `MembershipResponse` (with address, Owner/Admin only), `MembershipLabels` (`Disambiguate`, `Initials`; NFC plus ordinal).
  - `ListSorts` (three), `ListRoutes`, `ListQuery`, `ProblemTypes`.
- **Client:**
  - `TaskDetail.razor` (Status picker `:105-132`, Labels `:165-211`, Viewer `<dl>` `:214-237`, `KeyDownAsync` `:507`).
  - `BoardView.razor(.cs)` (`TaskAttribute` `:97`, `EditAsync` `:616`, `SendAsync` `:767`, `BodyOf`/`MemberOf` `:1224-1235`).
  - `TaskCard.razor` (meta row `:76-94`, `CardInputs` `:256`/`:336`).
  - `PickerOption.razor`, `LabelOption.razor`.
  - `TaskCapabilities.PermitsManageTasks`.
  - `TaskListView.razor(.cs)`, `TaskListFilters.cs`, `TaskListRow.razor`, `TaskListChoice.razor`.
  - `MembershipRow.razor:39` (the avatar markup to share).
- **Tests:**
  - `FakeYelloApi.cs` (`EditTaskAsync` `:1705`, `Apply` `:1741-1790`, `FakeTask` `:2335`, hooks `:247`/`:487`).
  - `TaskDetailTests`, `ListViewTests`, `LabelTests`, `MembershipSectionTests`.
  - `IsolationCases*.cs` (row shape `IsolationCases.cs:2000`; templates `:424`, `:600-616`, `:624`; `Memberships.cs:45`).
  - `WireDatabase` (outbox helpers `:858-932`, `AddMembershipAsync`, `ChangeRoleAsync`).
  - `MailDrop`, `HostProcess.RunAsync`, `HostPool`.
  - The race helpers in `ActiveSpaceEndpointTests.ChangeMembershipRole*.cs` and `RemoveMembershipRace.cs`.

### What must be preserved (regression guard)

- **The PATCH stays four attributes under `ManageTasks`.** The edit's gates are unwidened except `TaskResponse`'s member set.
- **Space deletion** (3.3) still deletes a Space with assigned Tasks in one statement.
- **4.5 unchanged:** the removal and leave answers, `LostMembership`, the Token cascade, the presence delete, the single `MembershipChanged` publish, and every race. The unassign is an extra statement only.
- **4.4 unchanged:** a Role change writes no Task (AC3). S-2 holds.
- **Address confinement:** only the Membership list and the Invitation list carry an address in a contract. The readers' allow-list is unwidened.
- **The outbox:** at-least-once delivery, the id-only logs, the row deleted after send, no timer, and the sweep piggybacked on requests (AD-10). `OutboxStore` still saves before the idempotency record (which refuses to save with other changes pending), so the send record must be added **before** its `SaveChangesAsync`.
- **List View:** every 2.10 case, the cursor `400` for a cursor from another sort, `ListPage.Size` = 50, no virtualisation, and NFR-5 at 5,000 for the existing three sorts.
- **The boundary `404` stays byte-identical** (`RefusalCopyTests`, `SameAsAnchorAndStranger`).

### Locking and the races (RCSI)

- Lock order: application lock, then rows. No `HOLDLOCK`.
- **Assignment:** `Yello/Memberships/{spaceId}` **Shared**, then the guarded `UPDATE` (the Task row's X lock). Two assignments of one Task serialise on the row.
- **Removal, leave, Role change:** the same name, **Exclusive** (unchanged), then (for removal and leave) the unassign `UPDATE`, then the guarded `DELETE`.
- **Why Shared is enough:** the only writers that invalidate a Membership id take the lock Exclusive, so while an assignment holds Shared, its target cannot vanish. Assignments in one Space never queue on one another.
- **Applock waits** show in `sys.dm_exec_requests` / `sys.dm_os_waiting_tasks` as `LCK_M_S` / `LCK_M_X` on the APPLICATION resource, so `WaitUntilBlockedOnALockAsync` (`LCK_M_%`) sees them. Confirm it on the first race.
- **Acceptance** (`Yello/Invitations/{spaceId}`) is independent. A new Member becomes assignable once its accept commits.
- **The Status lock** (`ProjectTasks`) is not taken by assignment, which never moves a key.

### Open questions

Each has a recommendation, which is the default. **[LEE]** marks a question whose answer touches authority, isolation, a recorded Lee decision or a security seam. The orchestrator may route those to Lee. Otherwise the dev phase decides them like the rest, as reversible, recorded decisions.

1. **[LEE] How assignment reaches the matrix** (AC1).
   - **(a) Recommended.** Its own route, `PUT .../tasks/{taskId}/assignee` under `Capability.AssignTask`, answering `200 TaskResponse`. The matrix's own row governs. The PATCH and its gates stay unchanged. The email logic lives in its own slice. Cost: one more route, its isolation rows, and a second client send path.
   - (b) An `assigneeMembershipId` member in the PATCH under `ManageTasks`. Cost: `AssignTask` stays dead code, the two cells could later diverge silently, the edit handler gains the email, and the edit's gates widen.
   - (c) A PATCH member plus a pipeline rule that also checks `AssignTask` when that member is present. Cost: the first endpoint with two capabilities, which breaks `RequiredCapability`'s single-capability invariant.
2. **The candidates read** (AC1, AC6, AC7).
   - **(a) Recommended.** `GET .../assignees` under `ReadProjectsAndTasks`, returning `{ membershipId, displayName }` for every Membership, unpaged (bound 100), read in `MembershipStore`. No Role, no address. The client disambiguates with `MembershipLabels`.
   - (b) Server-computed labels and initials. Cost: duplicates `MembershipLabels` logic on the wire, and two shapes of one fact.
   - (c) Reuse the Membership list. Cost: Owner/Admin only, and it carries the address. Not viable.
3. **Serialising assignment against removal** (AC8, AC9).
   - **(a) Recommended.** Assignment takes `Yello/Memberships/{spaceId}` **Shared**, and the removal, leave and Role change keep it Exclusive. Deterministic, testable on `LCK_M_%`, and assignments never wait on each other.
   - (b) Take it Exclusive. Cost: every assignment in a Space serialises.
   - (c) No lock, and map the removal's `547` to a retry. Cost: a removal can fail on a racing assignment, and the race is not deterministic.
4. **[LEE] Where the send record lives** (AC5; Lee's 4.1 Q6 and Q7).
   - **(a) Recommended.** A Space-scoped `NotificationSend(Id, SpaceId, Kind, RecordedAt)` under the policy, written by `OutboxStore` at enqueue in the request's transaction, for every kind (Invitation included). Cost: it counts enqueues, so an abandoned row still counts. A dated AD-10 note says so.
   - (b) Policy-free, written by the dispatcher after send, with `SpaceId` on the outbox row. Cost: it reverses 4.1 Q6, and needs two named variances.
   - (c) Assignment kind only. Cost: SM-C4 undercounts.
   - Sub-question, retention on Space deletion: **(a)** cascade with the Space, like every Space-scoped table (AD-5's erasure). (b) No FK, kept past deletion. Cost: a Space-scoped row outliving its Space, and a named exception to AD-5.
5. **How the removal unassigns** (AC8).
   - **(a) Recommended.** An explicit `UPDATE ... SET AssigneeMembershipId = NULL` in `RemoveCoreAsync` and `LeaveAsync`, after the lock and before the guarded `DELETE`, rolled back with the unit of work when the `DELETE` deletes nothing.
   - (b) An `INSTEAD OF DELETE` trigger on `Membership`. Cost: hidden behaviour, and it fights 3.3's cascade from `Space`.
   - (c) A generic "before removal" step list. Cost: an abstraction with one user (4.5 Q9 (b) rejected it).
6. **[UX] What the Assignee sort orders by** (AC7).
   - **(a) Recommended.** `AssigneeMembershipId`: Tasks are grouped by person, Unassigned last, tiebreak `TaskId`, index `(SpaceId, ProjectId, AssigneeMembershipId, Id)` (the ledger's index). Cost: the order between people is stable but not alphabetical. The column shows names, so the grouping reads clearly.
   - (b) A denormalised display-name key on `Task`. Cost: a live name read outside the allow-list, and future renames become cross-Space writes.
   - (c) Join to `Membership.CreatedAt`. Cost: the join cannot use one composite index, and AD-29 calls a sort without one a defect.
   - Sub-question, the filter's Unassigned: **(a)** a separate `unassigned` flag, the `dueNone` precedent. (b) A sentinel id. Cost: a magic value on the wire.
7. **[LEE/UX] The folding rule for look-alike names** (AC6, AC14; ledger `:802`).
   - **(a) Recommended.** The collision key is NFC, then remove `Cf` (default-ignorable, zero-width) code points, then trim, then collapse whitespace, then ordinal-ignore-case. Confusables go to the ledger (Lee/UX). Cost: "ann" and "Ann" now get discriminators where they did not before.
   - (b) Case-sensitive. Cost: "ann" and "Ann" look different but are easily confused at 20px.
   - (c) Add a TR39 confusable skeleton. Cost: a data table or a new package (Lee's approval).
8. **The email's content** (AC4).
   - **(a) Recommended.** It names the assigner (read at authoring time, the FR-39 precedent) and the Space, Project and Task, and gives the client's base URL with no ids. Plain text, English. Subject "{Assigner} assigned you a Task in {Space}".
   - (b) No assigner. Cost: the recipient cannot tell who wants it.
   - (c) A deep link to the Task. Cost: ids in a mail link that can be forwarded (AD-3's deep-link caution). The recipient must sign in and pick the Space anyway.
9. **What counts as "assigned"** (AC4).
   - **(a) Recommended.** Email only when the Assignee changes to a non-null Membership whose Account is not the actor's. No email for a no-op, an unassign, a self-assignment, a replay or a removal's unassignment. A reassignment tells the new Assignee only.
   - (b) Also tell the previous Assignee. Cost: SM-C4 says every extra notification is a cost, and FR-40 names only the assigned.
10. **[LEE] The Invitation rate limit** (ledger `:754`).
    - **(a) Recommended.** Not built here. No requirement covers it, and it is new product behaviour (a `429` and its copy). Re-own the entry to **Lee/PM** with the note that 4.6's assignment email reaches only existing Members of the Space, so it adds no new open-recipient vector.
    - (b) Build a per-Space or per-Account limiter on issue now. Cost: invented limits and copy, outside the epic's ACs.
11. **Copy** (`EXPERIENCE.md:99`: sentence case, no exclamation marks, no address, no Space name in announcements). **(a) Recommended:**
    - Attribute label: "Assignee". Options: "Unassigned", then the labels.
    - Avatar accessible name: "Assigned to {label}".
    - Announcements: "Assigned to {label}." / "Unassigned."
    - Refusal: "That person is no longer in this Space." (`invalid-assignee`).
    - List: fieldset "Assignee", option "Unassigned", sort "Assignee", column "Assignee".
    - Email: as Q8.

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. AD-5 "sets `Task.AssigneeMembershipId` to null" against a composite key.** It cannot be `ON DELETE SET NULL` (it would null `SpaceId`). It is met by an explicit statement inside the removal (AC8, Q5). The spine note says so.
- **C2. Two capabilities, one PATCH.** The matrix lists "Assign a Task" separately from editing, and the code enforces one capability per endpoint. Q1.
- **C3. "No email outside Membership management" against an email *to* the Assignee.** The address is read server-side and goes to the outbox only. It is never rendered on a surface, so AD-23 holds (AC4).
- **C4. FS-NFR-2 says "sent", and the record is written at enqueue.** Q4 (a)'s cost, stated in remarks and in an AD-10 note.
- **C5. The ledger names the column `AssigneeId`, and AD-5 names it `AssigneeMembershipId`.** Use AD-5's name and record it.
- **C6. P-3 counts "5 sorts".** The List offers four (Status, due date, Label, Assignee). The fifth in the test design is most plausibly the Board's position order. Record the count used.
- **C7. The task-detail mockup has no Assignee attribute.** The DESIGN spine places Assignee among the attributes (UX-DR10), and that is followed.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:628` → **CLOSED by 4.6**: the Assignee sort and filter, with their names in `ListSorts`, `ListSortColumns` and the index, `ListViewGateTests` agreeing, and I-15 rows. Name the cases.
- `:736` → **CLOSED by 4.6** (Q4): `NotificationSend`, written at enqueue. Name the cases and the no-reader gate.
- `:754` → **UPDATED by 4.6** (Q10 (a)): re-owned to **Lee/PM**, with the note on assignment's recipients.
- `:802` → **UPDATED by 4.6** (Q7 (a)): case, whitespace and zero-width folding applied, with the cases named. Confusables re-owned to **Lee/UX**.
- `:811` → **CLOSED by 4.6**: the unassign in the removal and the leave, Race 2, and the cases named.
- **New entries:**
  - "Account deletion must unassign the Account's Tasks in every Space it belongs to, explicitly, before its Memberships go" (owner **5.4**; medium; the same `SET NULL` reason).
  - "FR-41: an Assignee travels with a cross-Project move and no notification is sent" (owner **6.5**; low).
  - "The send record counts enqueues; an abandoned email still counts" (owner **Lee**; low; recorded, not a defect), if Q4 (a).
  - "The Assignee sort groups by Membership, not alphabetically" (owner **UX**; low), if Q6 (a).

### Architecture compliance (must)

- **AD-21 ring.** Handlers in Application; stores in Infrastructure; endpoints in Host; contracts in Contracts. The Client never references Domain (`AllowedReferenceEdges.cs`).
- **AR-3 / AD-1.** Slices never read a Role, count a bound, set a key, or record idempotency or refusals.
- **AD-2.** No raw SQL outside Infrastructure. Every new statement carries `SpaceId = @space` as well as relying on the key. Every new Space-scoped table has the FILTER and BLOCK predicates and a query filter.
- **AD-3.** Every not-found is the boundary body. Only the pipeline records.
- **AD-10.** Same transaction, dispatched after commit. No timer.
- **AD-18.** The pipeline owns `Idempotency-Key`. The handler must be replay-safe (it is never re-run on replay).
- **AD-19.** `TaskResponse` is appended only.
- **AD-23 / AR-34.** No address in a contract outside the two lists, and no address or content in a log. `SpaceId` is a log field, never a name.
- **AD-29.** Pair keyset, NULL fixed last, and a composite index per offered sort.
- **No test-only code in production** (1.9 Q4).
- **Code comments** (CLAUDE.md): say why. Never "added by story 4.6", no AC or Q ids, no history. Existing comments are rewritten only where they predict 4.6 (Task 8).

### Library / framework

No new package. Use:

- EF Core 10.0.11 (`ExecuteSqlAsync` / `SqlQuery` for the guarded statement with `OUTPUT`, as `MembershipStore` does; LINQ projections for the reads);
- ASP.NET Core 10.0.11 minimal APIs (`MapPut`, `MapGet`);
- Blazor WebAssembly 10.0.11;
- Microsoft.Data.SqlClient 7.0.2;
- xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3, and Mono.Cecil for the IL gates.

`sp_getapplock @LockMode = 'Shared'` is standard SQL Server, and is compatible with other Shared holders and incompatible with Exclusive. `NormalizationForm.FormC`, `CharUnicodeInfo.GetUnicodeCategory` (`UnicodeCategory.Format` for `Cf`) and `string.Equals(..., StringComparison.OrdinalIgnoreCase)` are in the BCL. No web research was needed, because every API used is already in the codebase or the BCL.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md` (`:87-200` rules; `:517-543` outbox; `:545-563` Memberships; `:565-585` migrations; `:756-877` isolation):

- **Traits:** `Suite`, `Priority`, `Requirement` (FR-21, FR-40, FR-30, AR-8/AD-2, AD-5, AD-10/AR-33, FS-NFR-2, AD-23, AD-29, UX-DR34), `Assumption` (`PRD-12-10` on every email case), and `Tier`. Names are underscore sentences, one behaviour each.
- Never `Task.Delay`. Races wait on `LCK_M_%`.
- **Never a shared email address** (`InvitationWire.FreshAddress`). Display names are randomised, except where a collision is the point.
- **Seed through the product where the case is about Membership** (Viewers and Members via an accepted Invitation). Seed directly for bulk (the 5,000-Task List) and record which.
- **Mail:** `HostProcess.RunAsync` plus `MailDrop` and `WaitForEmailToAsync(address)`, asserting "one email to this address".
- **No new container-backed test class.**
  - Slices: new partials `TaskEndpointTests.Assign.cs` (and `.AssignSchema.cs`), `ActiveSpaceEndpointTests.AssignRace.cs`; extend `RemoveMembership.cs`, `LeaveSpace.cs`, `ChangeMembershipRole*.cs` (the demotion case), `ListPagingTests`, the LV-19 plan case, and the Space-deletion case.
  - Handler unit tests take no database (`AssignTaskHandlerTests`: email only on change and to another Account, using the `RecordingOutbox` fake from `IssueInvitationHandlerTests.cs:248`).
  - Isolation: `IsolationCases.Assignment.cs`.
  - Client: extend `TaskDetailTests`, `ListViewTests` and `MembershipSectionTests`. Add the switch-purge case to `SpaceSwitchMemoryTests` or the closest existing class.
  - Gates: extend `RefusalBoundaryGateTests.TaskEdit.cs`, `.Labels.cs`, `.Tasks.cs`, `.Invitations.cs` (or a new `.Assignment.cs` partial), `ListViewGateTests`, and `MembershipLabels` unit cases.
- **Every absence has an anchor and is seen red under a plant.** A masked plant is recorded, never deleted.
- **Memory budget (CLAUDE.md):** one `dotnet test` at a time. Iterate with `--project ... -- --filter-class "<full name>"`. The single full run goes in the background with `--max-parallel-test-modules 1`, without the browser suite in Develop.

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | A single-column FK `AssigneeMembershipId → Membership.Id` | `Every_reference_between_Space_scoped_entities_carries_SpaceId`; the schema case |
| 2 | The guarded `UPDATE` drops its `EXISTS` clause (and the FK is disabled in a test-only migration copy, or masked; record which) | `ISO-ASSIGN-FOREIGN-MEMBERSHIP` (or recorded masked by the FK) |
| 3 | `invalid-assignee` answers differently for a foreign id (for example a distinct `detail`) | `ISO-ASSIGN-FOREIGN-MEMBERSHIP` (`SameAsAnchor`) |
| 4 | The email enqueued for a self-assignment | the not-for-one's-own-action case |
| 5 | The email enqueued on every assignment, a no-op included | the no-op case; the replay case |
| 6 | The email body includes another Space's name (a planted read without the Space key) | the I-14 case |
| 7 | The send record gains a `Recipient` column, or a reader in a slice | the columns case; the no-reader gate |
| 8 | The removal's unassign removed | the "assigned Member removed" case; Race 2 |
| 9 | The unassign runs unguarded on a refused removal, with no rollback | the refused-removal-keeps-assignment case |
| 10 | The Role change unassigns on demotion to Viewer | the demotion case |
| 11 | Assignment skips the Memberships lock | Race 1 (or recorded masked) |
| 12 | Assignment takes the lock Exclusive | Race 3 |
| 13 | The candidates read includes `EmailAddress` | the contract gate; the no-`@` client case |
| 14 | The Assign control rendered for a Viewer | the client absence case; the agreement gate |
| 15 | `ListSortColumns` loses the Assignee entry, or the index is dropped | `ListViewGateTests` P-4 |
| 16 | The Assignee filter refuses a foreign id instead of matching nothing | `ISO-LIST-FOREIGN-ASSIGNEE-FILTER` |
| 17 | The candidates list survives a Space switch (not purged) | the switch-purge browser case |
| 18 | The collision key reverts to NFC plus ordinal | the look-alike `MembershipLabels` cases; AC14's browser case |
| 19 | `CardInputs` omits the Assignee | the card-redraws-on-assignment browser case |

### Previous story intelligence (4.5, 4.4, 4.1, 2.10)

- **4.5:**
  - S1200 split `MembershipEndpoints` into three classes. Expect `MembershipStore` to need a split now (Task 4).
  - Race scaffolds failed `400` until they fetched an anti-forgery token after sign-in (`FetchAntiforgeryTokenAsync()` after `MadePresentAsync`). Do it in every new race participant.
  - Two masked plants: a guarded statement with a second redundant clause masks the first. Record honestly.
  - The first client run broke on a duplicate sibling `@key`. Avoid `@key` collisions on the new Assignee components.
  - `Only_the_shared_actionable_predicate_compares_ExpiresAt` refuses ad-hoc expiry comparisons. Not relevant here unless presence is read.
  - The accept answers `200`, not `201`.
- **4.4:** a pipeline dependency must be registered in `SpaceBoundaryPipelineTests` and `ActiveSpaceUnitOfWorkTests`. `WaitForEmailToAsync` returns the **first** email to an address, so give each email case a fresh recipient. After an OOM-killed run, grep for plant markers before continuing.
- **4.1:** the enqueue is in the handler, through `IOutbox`, and `OutboxStore` saves before the idempotency record. The email template gate restricts parameter types. The forced-rollback trigger on `IdempotencyRecord` is the same-transaction proof.
- **2.10:** the two-segment NULL-last reader, LV-19's plan read, the logical-reads tolerance 5, `INCLUDE` measured rather than assumed, and the filter narrowing that turns a foreign id into "matches nothing".

### Git intelligence

Recent commits:

- `4d23bf3`: 4.5 review close (copy; comment-rule clean-up across 21 files, so keep new comments clean the first time).
- `40c4cd2`: 4.5 implementation (removal, leave, `LostMembership`, `SpaceExit`).
- `f400fa8` / `920baf0`: 4.5 ATDD and story.
- `a907766`: mail-file read retry.
- `6145bf7`: the Client suite publishes the client and runs four classes in parallel.
- `7e0fc54`: `HostPool`. Log- or mail-reading cases need their own process.

The cycle is: story; decisions recorded; ATDD scaffolds (`Skip = "red: ..."`, `atdd-checklist-<key>.md`, `PendingUntil: "4.6"` rows); implementation; review fixes. The orchestrator commits each.

### Project Structure Notes

- **New (expected):**
  - Contracts: `Yello.Contracts/Tasks/AssignTaskRequest.cs`, `Yello.Contracts/Memberships/AssigneeRoutes.cs`, `AssigneeResponse.cs`, `AssigneeListResponse.cs`.
  - Application: `Yello.Application/Tasks/AssignTask/AssignTaskHandler.cs`, `AssignmentEmail.cs`; `Yello.Application/Memberships/ListAssignees/ListAssigneesHandler.cs`.
  - Infrastructure: `Yello.Infrastructure/Outbox/NotificationSend.cs` and `Persistence/Configurations/NotificationSendConfiguration.cs` (Q4 (a)); migration `<timestamp>_TaskAssignee.cs`.
  - Host: `Yello.Host/Endpoints/TaskAssigneeEndpoints.cs`, `AssigneeEndpoints.cs`.
  - Client: `Yello.Client/Components/AssigneeAvatar.razor`, an Assignee picker component (or inline in `TaskDetail` if small), `Yello.Client/Spaces/SpaceAssignees.cs` (the per-Space holder).
  - Tests: the Slices partials, `IsolationCases.Assignment.cs`, the gate partial.
- **Updated (expected):**
  - Domain: `TaskItem.cs`, `ITaskStore.cs`, `Capability.cs` (comment), `ListSort.cs`, `ListFilter.cs`, `ListCursor.cs`.
  - Contracts: `TaskRoutes.cs`, `TaskResponse.cs`, `ProblemTypes.cs`, `ListSorts.cs`, `ListRoutes.cs`, `ListQuery`, `MembershipLabels.cs`.
  - Application: `OutboxKinds.cs`, `IMembershipStore.cs`, `ViewListHandler.cs`, `ListCursorText.cs`, `ListSortCodes.cs`.
  - Infrastructure: `TaskConfiguration.cs`, `MembershipConfiguration.cs` (comment), `TaskStore.cs`, `MembershipStore.cs` (split if S1200), `ApplicationLocks.cs`, `OutboxStore.cs`, `SchemaNames.cs`, `YelloDbContext.cs`, `ListSortColumns.cs`, `ListReader.cs`, the model snapshot.
  - Host: `Program.cs`, `TaskEndpoints.cs` (`Response` gains the Assignee), `ListQueryReading.cs`.
  - Client: `TaskDetail.razor`, `TaskCard.razor`, `BoardView.razor(.cs)`, `TaskCapabilities.cs`, `TaskListView.razor(.cs)`, `TaskListFilters.cs`, `TaskListRow.razor`, `TaskListPage.razor` (comment), `MembershipRow.razor` (the shared avatar), `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`.
  - Tests:
    - Architecture: `RefusalBoundaryGateTests.TaskEdit.cs`, `.Labels.cs`, `.Tasks.cs`, `.Memberships.cs`, `.Invitations.cs`, `ListViewGateTests.cs`, `PersistenceModelGateTests.cs` (comment);
    - Slices: `SpaceIsolationSchemaTests.cs`, `MigrationBundleEquivalenceTests`, `ListPagingTests.cs`, `ActiveSpaceEndpointTests.RemoveMembership.cs`, `.LeaveSpace.cs`, `IssueInvitationHandlerTests` (the send record via `RecordingOutbox`, if touched), `SpaceBoundaryPipelineTests.cs`/`ActiveSpaceUnitOfWorkTests.cs` if the pipeline gains a dependency;
    - Client: `FakeYelloApi.cs`, `TaskDetailTests.cs`, `ListViewTests.cs`, `MembershipSectionTests.cs`;
    - Isolation: `IsolationCases.cs` (spread);
    - Shared: `WireDatabase.cs` (assignment, send-record readers).
  - Docs: `ARCHITECTURE-SPINE.md` (dated notes on AD-5, AD-10 and AD-29), `deferred-work.md`, `sprint-status.yaml`.
- **No new project, no new package, and no new container-backed test class.** One migration.

### References

- `epics.md`: `:114` (FR-21), `:149` (FR-40), `:167` (FS-NFR-2), `:174`, `:196` (AR-8), `:210` (AR-22), `:221` (AR-33), `:246` (UX-DR8), `:249` (UX-DR11), `:257` (UX-DR19), `:271` (UX-DR27), `:275` (UX-DR31), `:278` (UX-DR34), `:374`, `:400`, `:1613-1617`, `:1836-1875` (4.6), `:1877-1916` (4.7, the boundary).
- `prd.md`: `:68` (Assignee), `:344` (matrix), `:357-359`, `:387`, `:408-417` (FR-21), `:536-543` (FR-30), `:654-661` (FR-40), `:677` (FS-NFR-2), `:726` (NFR-6), `:765-767` (§6.1), `:817`, `:841`, `:871` (SM-C4), `:901` (§12 item 10).
- `ARCHITECTURE-SPINE.md`: AD-2 (`:82-89`), AD-5 (`:109-111`), AD-9 (`:136-137`), AD-10 (`:143`), AD-23 (`:224`), AD-26 (`:251`), AD-29 (`:275`), `:387`, `:435`.
- `DESIGN.md`: `:188-193`, `:389-391`, `:441`, `:451`, `:464`, `:473`, `:502`. `EXPERIENCE.md`: `:149-156`, `:164`, `:210-211`, `:217`, `:255`, `:275`. `mockups/board.html:106-190`, `mockups/task-detail.html:86-90`. `review-isolation.md:52`.
- `specs/spec-yello/`: `acceptance-criteria.md:205-211` (CAP-21), `:393-399` (CAP-40); `role-capability-matrix.md:15`, `:43`; `SPEC.md:117-118`; `success-metrics.md:16`, `:48`.
- `test-design-qa.md`: `:132`, `:278` (I-14), `:279` (I-15), `:312` (P-3), `:358` (P-4). `test-design-architecture.md:378-381`.
- `implementation-readiness-report-2026-08-22.md`: `:1102-1108`, `:1234`, `:1321` (FR-30 staging).
- `tests/TESTING-CONVENTIONS.md`: `:30-73`, `:87-200`, `:209-305`, `:517-543`, `:545-563`, `:565-585`, `:756-877`.
- `deferred-work.md`: `:628`, `:736`, `:754`, `:802`, `:811`.
- Story files: `4-5-remove-a-membership-or-leave-a-space.md` (Q9, C1, Debug Log), `4-4-manage-memberships-and-roles.md` (Q5, Q13: `MembershipLabels`), `4-1-invite-an-email-address-to-a-space-at-a-role.md` (Q6, Q7: the outbox), `2-10-view-a-project-as-a-filterable-sortable-list.md` (AC2, AC4-AC6, AC10, AC15-AC17).

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-4-6-assign-a-task-and-be-told.md` (decisions, seed constraints,
findings, the active tests that go red by design when the Assignee lands).

| File | Cases (methods) | ACs | Green today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AssignTask.cs` | 27 (17) | AC1-AC5, AC8, AC9 | none |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AssignTaskRace.cs` | 3 (3) | AC8, AC9 (races 1-3) | none |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.AssignSchema.cs` | 4 (4) | AC2, AC5, AC7 | none |
| `tests/Yello.Tests.Slices/Lists/ListPagingTests.Assignee.cs` | 5 (4) | AC7 (P-3, P-4, NFR-5) | none |
| `tests/Yello.Tests.Slices/Memberships/MembershipLabelsTests.cs` (3 added) | 8 (3) | AC6, AC14 (Q7) | `Different_names_that_share_a_prefix_do_not_collide` (standing gate) |
| `tests/Yello.Tests.Isolation/IsolationCases.Assignment.cs` | 9 rows x 2 surfaces, `PendingUntil: "4.6"` | AC1, AC7, AC12 | pending |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Assignment.cs` | 10 (10) | AC1, AC2, AC4-AC6, AC10, AC13 | none |
| `tests/Yello.Tests.Architecture/ListViewGateTests.cs` (2 added) | 2 (2) | AC7 | none |
| `tests/Yello.Tests.Client/AssigneeTests.cs` (new class, existing fixtures) | 9 (9) | AC6, AC7, AC10, AC11 | none |
| `tests/Yello.Tests.Client/MembershipSectionTests.cs` (1 added) | 1 (1) | AC14 | none |

Supporting: `tests/Yello.Tests.Shared/AssignmentWire.cs` (temporary route, problem-type and List-parameter stand-ins to swap
for the contract members and delete), new `WireDatabase` and `MailDrop` readers, and `tests/Yello.Tests.Client/FakeYelloApi.Assignees.cs`
(plus `FakeTask.AssigneeMembershipId` and the fake List's Assignee filter and sort).

Activation order, riskiest assumption first:

1. `A_removal_queued_behind_an_assignment_of_its_target_unassigns_and_answers_204` (race 2: the NO ACTION composite key plus
   an explicit unassign under the Memberships lock never lets a removal fail `547`).
2. The schema cases in `TaskEndpointTests.AssignSchema.cs` and the EF-model gate
   `The_Assignee_is_a_composite_no_action_reference_to_Membership_in_the_model`.
3. `An_assigned_Member_is_removed_...`, `An_assigned_Member_leaves_...`, `A_refused_removal_leaves_the_targets_Tasks_assigned`,
   `Deleting_a_Space_that_holds_an_assigned_Task_...`.
4. The assignment route: the routes and capability gates, then `An_Owner_an_Admin_or_a_Member_assigns_...`, the Token, body,
   foreign-id, Viewer and demotion cases, then races 1 and 3.
5. The candidates read and its contract gate.
6. The email and the send record: I-14, self, change-only, rollback, send-record, replay; the template, outbox-kind and
   no-reader gates.
7. The List: the two `ListViewGateTests` gates, then `ListPagingTests.Assignee.cs`.
8. `MembershipLabelsTests` look-alike cases (rewrite `Names_are_compared_ordinally_after_NFC` in the same change).
9. The isolation rows (all nine live before review, GATE-003).
10. The client: `AssigneeTests`, then the `MembershipSectionTests` look-alike case.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the
correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-11-08). Eleven open questions with recommendations; Q1, Q4, Q7 and Q10 are marked [LEE] (Q6 and Q7 also touch UX).

### File List

## Change Log

- 2026-11-08: Story 4.6 created (bmad-create-story). Status ready-for-dev.
