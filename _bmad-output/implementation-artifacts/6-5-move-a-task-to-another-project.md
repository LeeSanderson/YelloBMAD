# Story 6.5: Move a Task to another Project

Status: ready-for-dev

Epic: 6 — Shape Statuses per Project and retire work safely (fifth of six stories)
Story key: `6-5-move-a-task-to-another-project`

**Requirements owned.** Story Coverage Index (`epics.md:383`): **FR-41** (single-Task form), **AR-24** (`epics.md:212`: the cross-Project move is one transaction, and no endpoint accepts it without the mapping it requires). Also carried:

- **FR-41** (`prd.md:435-445`):
  - Owner, Admin and Member can move; a Viewer cannot.
  - Only Projects of the Task's own Space are offered, and only those are accepted, including over the API.
  - The Status is preserved when the destination's effective set holds it. Otherwise a destination Status is required "on the same terms as FR-26", and the move does not take effect without one.
  - Assignee, Labels, due date and description survive.
  - An active editing session continues across the move.
- **FR-19** (`prd.md:398`): a Task belongs to exactly one Space permanently; no operation moves it to another Space.
- **FR-20** (`prd.md:406`): a Status valid in a sibling Project is refused.
- **FR-26** (`prd.md:487-490`): the destination must be in the set in force; a destination is decided under the lock.
- **FR-35** (`prd.md:594-598`, `:450`): the API can do what the browser can, except write a Board position. The bulk form (6.6) must be on the API "on the same terms as the single-Task form", so this form is on **both surfaces**.
- **NFR-8** (`prd.md:736-751`): Tasks per Project is 5,000. "A bound that is not enforced is a defect, not a relaxation." A move **adds** a Task to the destination Project.
- **AD-15** (`ARCHITECTURE-SPINE.md:178-184`): only the moved Task's key is written; no sibling is renumbered; the key is never writable over the API.
- **AD-16 / AD-17** (`:186-201`): one derivation of the effective set; one transaction; the 6.4 note at `:201` names this move as the next user of the sorted `ProjectTasks` lock helper.
- **AD-18** (`:203-207`): idempotent through the pipeline's `Idempotency-Key`.
- **AD-2 / AD-3 / AD-20, NFR-1, FR-15** (I-1, I-2, I-4, I-5, I-13, V-1, P0): a new Space-scoped route with isolation rows on both surfaces.
- **S-1** (`test-design-qa.md:318`, P0): atomicity under a forced failure covers FR-41. **S-2** (`:319`, P0): no Task holds a Status outside its Project's set. Assert S-2 after every case.
- **FR-16**: `Capability.ManageTasks` (Owner, Admin, Member), the capability the 2.6 move already declares (`RoleCapabilityMatrix.cs:40`).
- **UX**: `EXPERIENCE.md:206` (the Move control lives in Task detail and the Task menu), `:207` and `:271` (focus when the card no longer exists), `:211` and DESIGN.md `:473` (no default selection in a consequential picker), `:295` (Move absent only for a Role that cannot move), `:331` (no disabled controls for Role reasons), `:324` (announcement shape).

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Recommended disposition |
|---|---|---|
| `:574` | Task deletion reads the Task's Project without a lock, then locks that Project's range. "6.5, which makes `ProjectId` change, must give every Task writer that re-check - the deletion, the Status and Labels edits and the move - or lock both Projects' ranges in one order." | **CLOSE** (AC9, Q8). The Status edit and the 2.6 move already re-check under the lock (6.2). The deletion gains the re-check. A Labels-only edit, an assignment and a title or due-date edit do not depend on the Project, so they need none: state why in the ledger. |
| `:861` | FR-41: an Assignee travels with a cross-Project move, and no notification is sent | **CLOSE** (AC5). A wire case proves the Assignee is unchanged and no outbox row is written. |
| `:1087` | The 6.4 handler's remarks say the defaults and deltas "cannot change once the first lock is held"; a Project deletion deletes deltas under `ProjectsPerSpace` only | **CLOSE** (documentation only, Q12). Correct the remark in `RemoveSpaceStatusHandler.cs:18` to say `ProjectsPerSpace` is what keeps the deltas stable against a Project deletion. |
| `:114` | The first default Status name may not appear in any comment (S1135) | **Keep open**: a standing trap for 6.6 (see Traps). Add "Still open after story 6.5". |
| `:1030`, `:1031`, `:1075` | Name comparison vs collation; look-alike names; the default-name index backstop | Leave open. A move compares no names. Note it. |
| `:1038`, `:1039` | 6.1 migration unsafe for the old revision; `Down()` lossy | Leave open. 6.5 needs **no migration**. Note it. |
| `:1072` | Space-level writes read every delta O(Projects x rows) | Leave open. A move reads two Projects' sets only. Note it. |

## What already exists — do not rebuild it

| Concern | Where it lives today | What 6.5 does with it |
|---|---|---|
| The Task row | `TaskItem.ProjectId` is already `{ get; set; }` "only because FR-41 (story 6.5) moves a Task between Projects" (`Yello.Domain/Tasks/TaskItem.cs:38`). `SpaceId` is `init`, and the database refuses a `SpaceId` change with a `BLOCK ... AFTER UPDATE` predicate. | Write `ProjectId`, `StatusId` and `PositionKey` in one statement. Never touch `SpaceId`, `Id`, `Title`, `DueDate`, `AssigneeMembershipId`, `CreatedAt`, `FirstLabelName` or the `TaskLabel` rows. |
| Keys and FKs | `Task (SpaceId, ProjectId) → Project` (**Cascade**), `(SpaceId, StatusId) → StatusDefinition` (NO ACTION), `(SpaceId, AssigneeMembershipId) → Membership` (NO ACTION). Unique `(SpaceId, ProjectId, StatusId, PositionKey)` (`TaskConfiguration.cs:80-126`). | A reparent within one Space satisfies every key. The new key must be unique in the destination column: `PositionKey.After(MAX)` read under the destination's lock. **No migration.** |
| Task URL | `TaskRoutes.Task` = `/api/v1/spaces/{spaceId}/tasks/{taskId}`. Its remarks: the Task's URL must never hold a Project, because FR-41 moves it (`TaskRoutes.cs:12-16`). | The new route hangs off the Task's own URL. |
| The 2.6 move | `TaskRoutes.Move` (`POST …/tasks/{taskId}/move`, `MoveTaskRequest(Guid StatusId, Guid? AfterTaskId)`), **Session only** (FR-35: a position is never writable over the API). `MoveTaskHandler` (find → `LockProjectAsync` → re-read → `ProjectId != locked` is `NotFound` → `EffectiveOfAsync` + `Contains`). `TaskStore.MoveAsync` (Project lock → row `UPDLOCK, INDEX(1)` → re-read → write). `TaskMoveEndpoints.cs`, `TaskMoveBody.cs` (the body read as a `JsonDocument`, never bound). | **Unchanged.** It stays within one Project. Copy its shapes (body read as a document, the row lock, the re-read under the lock). The new route writes **no** client position, so it is on both surfaces (Q2). |
| Status checks | `ProjectStatuses.EffectiveOfAsync(projectId, ct)` on `IStatusDefinitionReader`; `EffectiveStatusSet.Contains` (`Yello.Domain/Statuses/EffectiveStatusSet.cs`). | Read the **destination's** set after both locks. No second derivation and no SQL re-derivation (AD-16). |
| Locks | `ApplicationLocks.TakeProjectTasksAsync(dbContext, IReadOnlyCollection<Guid> projectIds, ct)` (`ApplicationLocks.cs:261`): de-duplicates and takes every `Yello/ProjectTasks/{space}/{project}` lock in ordinal order of the lock name. Its summary already names "a move of Tasks between two Projects". The `StatusDefaults` remarks (`:126-135`) state the global order and that **Task writers never take `StatusDefaults`**. | Source and destination are taken **only** through this overload, never two single calls (Q4). `ITaskStore` needs a port for it (for example `LockProjectsAsync(IReadOnlyCollection<Guid>)`). Every fake `ITaskStore` in the tests then gains the member (about five). |
| Every other Task writer | Status edit (`EditTaskAttributesHandler`) and the 2.6 move take `LockProjectAsync(task.ProjectId)`, re-read, and answer `NotFound` when `ProjectId` changed (6.2, ledger `:1020`). Creation's lock is taken by the pipeline's bound count (`BoundCounter.cs`). A Labels-only edit takes the Task row (`UPDLOCK, INDEX(1)`) without the Project lock. An assignment takes the Memberships lock Shared. A title or due-date edit takes no lock and writes its own columns only (EF tracks changed properties). `TaskStore.DeleteAsync` reads `ProjectId` **unlocked**, takes that Project's lock and the row lock, and deletes **without re-checking** `ProjectId` (ledger `:574`). | The deletion gains the re-check (AC9). Everything else is unchanged, and the story states why in the ledger. |
| Status migration | `TaskStatusMigration.MigrateAsync` (6.4): many Tasks of one Project, one set-based `UPDATE`. | **Not reused**: one Task is one row. Use the same key rule: the end of the destination column, `PositionKey.After(last)` with `last` read under the destination's lock (6.2 AC9, `TaskStore.LastKeyUnderTheProjectLockAsync`). 6.6 will need a set-based form; that is 6.6's to build. |
| Bound | `BoundRegistry`: `TasksPerProject` = 5,000, `BoundOperation.CreateTask`. The pipeline counts it **before** the endpoint, for the route's `{projectId}` (`RequiredBound.cs`, `ActiveSpaceResolution.Bound.cs`), and answers `409 bound-reached` via `BoundaryProblems.BoundReached`. `IBoundCounter.CountAsync(Bound.TasksPerProject, projectId)` takes the Project's lock, then counts. | The destination is in the body, not the route, so the pipeline cannot count it. And it must not: a pipeline lock on the destination taken before the slice's sorted pair would let two opposite moves deadlock. Count in the slice, after the sorted locks (Q5). |
| Problem types | `invalid-task-status` (400), `status-destination-required` (409), `invalid-status-destination` (400), `bound-reached` (409), `not-available` (404), `insufficient-role` (403), `invalid-task-edit` (400) | Reuse. Add **one**: `invalid-task-project` (400) (Q3). |
| Project list and destination set, client-readable | `GET ProjectRoutes.List` and `GET ProjectStatusRoutes.Collection` both require `ReadProjectsAndTasks` (every Role). | The client's Project picker and Status step read these. No new read route. |
| Task detail | `TaskDetail.razor` (**875 lines**) holds the Move control (`:144-149`) and the in-place `MovePicker`. `MovePicker.razor` (two steps, nothing selected, Esc steps back). `TaskMenu.razor` (Open, Move). | Add a "Move to Project" control in Task detail with a **new component** for its picker. Do not grow `TaskDetail.razor` beyond a parameter and a few lines. |
| Board | `BoardView.razor.cs` (**1,368 lines**, at the S1200 dependency cap) holds the replica. `Deleted(taskId)` (`:570`) drops a card, focuses its column and announces. | Reuse the shape of `Deleted` for a Task that left the Project: card gone, focus to the column that held it, announced. Put the new code in a new partial (for example `BoardView.ProjectMove.cs`) and keep new dependencies out of `BoardView` where possible. |
| Test helpers | `WireDatabase.TasksOutsideTheirProjectsSetAsync(spaceId)` (S-2, `tests/Yello.Tests.Shared/WireDatabase.cs:846`), `TaskWire.SeedTasksAsync` (`TaskWire.cs:205`), `DeleteWire.HoldProjectRangeAsync` (`DeleteWire.cs:116`), `TaskWire.WaitUntilBlockedOnALockAsync` (`TaskWire.cs:347`), `MoveWire.cs`, `LabelWire.cs`, the S-1 trigger pattern (`ActiveSpaceEndpointTests.DeleteSpace.cs:268-305`). | Reuse. Add a `ProjectMoveWire` (or extend `MoveWire`). |

**What is genuinely new in 6.5:**

1. The first write that changes a Task's `ProjectId`, under **two** Projects' locks taken in one sorted order.
2. A destination Status decided under those locks: preserved, supplied, or refused `409`.
3. The Tasks-per-Project bound enforced on a write that is not a creation, counted in the slice.
4. The deletion's `ProjectId` re-check.
5. The Task detail "Move to Project" control.

**Explicitly not in this story:**

- The bulk form, the column entry point and the accent-bordered bulk-move bar (6.6, UX-DR17).
- Collaborative editing sessions (Epic 7). This story asserts only that the move changes neither the Task id nor the Space (`epics.md:2328-2329`).
- A description field. It does not exist yet (FR-31 is Epic 7; `TaskItem.cs` remarks). "Description survives" is vacuous today and becomes true by construction once the column exists on the row.
- Realtime publishing. Nothing publishes Board changes before Epic 7; another tab sees the move on its next read.
- A "Move to Project" item in the Board's card menu (Q10).
- No migration, no new NuGet package, no new project. No commit, push, branch, Azure login or deploy. The orchestrator commits.

## Story

As someone reorganising work,
I want to move a Task into a different Project in the same Space,
so that work can be re-filed without being retyped.

## Acceptance Criteria

1. **The route** (FR-41, FR-35, AR-24; Q1, Q2). `POST /api/v1/spaces/{spaceId}/tasks/{taskId}/project` with `MoveTaskToProjectRequest(Guid ProjectId, Guid? StatusId)`.
   - Declares `Capability.ManageTasks`. Served on **both** surfaces (Session and Token): the caller writes no position.
   - Idempotent through the pipeline's `Idempotency-Key` (AD-18). A replay returns the original answer and moves nothing again.
   - Success answers `200` with the Task as stored (`TaskResponse`), including its new `ProjectId` (AC11).
   - The body is read as a document inside the endpoint, never bound. A body that is unreadable, not an object, leaves out `projectId`, or carries a `positionKey`, `afterTaskId` or `spaceId` member is `400 invalid-task-edit`. A `projectId` that is not an id is `400 invalid-task-project`. A `statusId` present and not an id (and not `null`) is `400 invalid-status-destination`.

2. **Only Projects of the Task's own Space** (FR-41, FR-19, AD-2, I-13; Q3). The destination must be a Project of the request's Space **other than the Task's current Project**. Otherwise the answer is `400 invalid-task-project`, **byte-identical** whatever the cause:
   - a random id;
   - another Space's real Project id;
   - the Task's own Project;
   - a Project deleted while the move waited (AC6).

   No route and no request contract carries a Space, so no operation moves a Task to another Space. A gate asserts it (AC13).

3. **Status preserved where the destination has it** (FR-41; `prd.md:443`). When `statusId` is absent or `null` and the destination's effective set (read after both locks) contains the Task's current Status, the Task keeps that Status. No mapping is asked for. A Status the source Project added belongs to that Project alone, so it is never in another Project's set and always needs a destination (S-2 counts "another Project's definition" as outside).

4. **A destination Status where it does not** (FR-41, FR-26, AR-24; Q6).
   - `statusId` absent or `null`, and the destination's set does not contain the Task's Status: `409 status-destination-required`, and nothing is written. No count or choices in the body; the client reads the destination's set (Q7).
   - `statusId` supplied: it must be in the destination's effective set, or the answer is `400 invalid-status-destination`, byte-identical for the source Project's added Status, another Space's Status, a removed default and a random id.
   - A supplied `statusId` that the destination holds is **applied**, even when the current Status would also have been preserved. The request then means one thing whatever the state: "move into this Project, in this Status".

5. **What survives** (FR-41; `prd.md:444`; ledger `:861`). After a move, the Task's id, `SpaceId`, title, due date, Assignee (`AssigneeMembershipId`), Labels (`TaskLabel` rows and `FirstLabelName`) and `CreatedAt` are unchanged. Seed every one non-null; a `null` Assignee proves nothing (6.2 review). **No email and no outbox row** is written: the Assignee did not change (AD-10 note).

6. **One transaction, under both Projects' locks, in one order** (AR-24, AD-17; Q4).
   - Lock order: `ProjectTasks` of the source and the destination, through `ApplicationLocks.TakeProjectTasksAsync(dbContext, [source, destination])`, then the Task's row (`UPDLOCK, INDEX(1)`, no `HOLDLOCK`). Never `StatusDefaults`, never `ProjectsPerSpace` (Task writers never take them; a move takes nothing after a `ProjectTasks` lock that is earlier in the global order).
   - Before any lock: a plain read of the Task (missing → boundary `404`) and of the destination Project (not visible, or equal to the Task's Project → AC2's `400`). Ids are read plainly first, as every writer does since 3.4, so no lock is ever taken for a name nobody can see.
   - After the locks, and only then:
     1. re-read the Task: gone, or `ProjectId` no longer the source whose lock is held → boundary `404` (the 6.2 precedent);
     2. re-read the destination: gone (deleted while the move waited) → `400 invalid-task-project`;
     3. read the destination's effective set and decide the Status (AC3, AC4);
     4. count the destination's Tasks (AC7);
     5. read the destination column's last key, and write `ProjectId`, `StatusId` and `PositionKey = PositionKey.After(last)` in **one** statement.
   - Reparent and Status change are therefore one row write in one transaction. No intermediate state (new Project, old Status) is ever committed or visible.

7. **The destination's bound** (NFR-8, AD-25; Q5). If the destination already holds 5,000 Tasks, the move is refused `409 bound-reached`, **byte-identical** to the creation's refusal for the same bound (`BoundaryProblems.BoundReached`), and logged as the creation's is. Counted after the sorted locks through the existing `IBoundCounter.CountAsync(Bound.TasksPerProject, destinationId)` (its lock take is a re-entry). The source's bound is never consulted (a move out never exceeds it).

8. **Where the Task lands** (AD-15, FR-35; Q9). At the **end** of its destination column, after the last key read under the destination's lock. No sibling in either Project changes its key. The request carries no position; a body naming one is AC1's `400`.

9. **Every other Task writer stays correct when `ProjectId` can change** (ledger `:574`; Q8).
   - `TaskStore.DeleteAsync` re-checks `ProjectId` under its row lock. A Task that moved away while the deletion waited on the old Project's lock answers the boundary `404` and is **not deleted**.
   - The Status edit and the 2.6 move keep their re-check: queued behind a cross-Project move on the source's lock, they answer `404` (6.2's precedent) and write nothing.
   - A Labels-only edit, an assignment, and a title or due-date edit take no Project lock and need none: they write only their own columns or rows, which the move does not write.
   - A creation in the destination queued behind the move counts the moved Task and lands after it.

10. **Atomic under a forced failure** (S-1, P0, AD-17). A test-only `AFTER UPDATE` trigger on `Task` throws for the moved id. Under it the request fails `5xx`, and the Task still holds its old Project, Status and key; S-2 holds. Anchored on the same request succeeding once the trigger is dropped (in a `finally`).

11. **The answer states the Project** (FR-35, AD-19; Q11). `TaskResponse` gains one additive member, `ProjectId` (an `init` property like `AssigneeMembershipId`), filled on every Task answer (creation, read, edit, 2.6 move, assignment, this move) and on the Board and List rows that already build `TaskResponse`. Without it a Token client cannot learn where a Task now lives, since the Task's URL holds no Project. Amend any gate that pins `TaskResponse`'s members deliberately.

12. **Role** (FR-16, FR-41, UX-DR27, C-6). Viewer gets `403 insufficient-role` with the pipeline's refusal record on Session and Token. Owner, Admin and Member succeed. In the client the control is **absent from the DOM** for a Viewer (never disabled).

13. **Isolation and gates** (NFR-1, FR-15, AD-2, AD-3; I-1, I-2, I-4, I-5, I-13, V-1, P0). Rows in a new `IsolationCases.ProjectMove.cs`, on both surfaces, live before review (GATE-003):
    - a foreign Space in the path → `404 not-available`, recorded as the boundary records it;
    - another Space's Task id in the caller's own Space path → `404`, byte-identical, not recorded;
    - another Space's Project id in the body → `400 invalid-task-project`, byte-identical to a random id;
    - another Space's Status id in the body → `400 invalid-status-destination`, byte-identical to a random id;
    - a Viewer → `403` with its record (`ISO-RECORD-KIND-…`);
    - `ISO-TOKEN-OWN-OTHER-SPACE-TASK-PROJECT-MOVE` (a Token of the caller's other Space).

    Gates (Architecture suite):
    - no Task request contract carries a Space member, and the only one carrying a Project is `MoveTaskToProjectRequest`;
    - no route under `/tasks/{taskId}` other than `…/project` writes a `ProjectId`, and that route has no Space parameter beyond the path's own;
    - the Task request-name gate (`RefusalBoundaryGateTests.TaskEdit.cs:52`) is widened by exactly `MoveTaskToProjectRequest`;
    - the route-capability and both-surfaces lists, and the role-matrix theory if it needs a representative per route.

14. **The client: Task detail "Move to Project"** (FR-41, UX-DR11, UX-DR21, UX-DR27; `EXPERIENCE.md:206-211`, `:271`, `:295`, `:331`; Q7, Q9, Q10).
    - **Control.** In Task detail, beside Move, a "Move to Project" control for a Role that can move Tasks; absent for a Viewer. When the Space has no other Project the control is still present, and its picker says "There is no other Project in this Space." (one line, focusable list, Esc closes).
    - **Step 1, the Project.** Opens in place below the control (never a dialog), as `MovePicker` does. It lists the Space's other Projects by name, from `GET ProjectRoutes.ListIn`, with **no default selection** and one tab stop. The current Project is not offered.
    - **Step 2, only when needed.** On a Project choice the client reads that Project's effective set (`GET ProjectStatusRoutes.CollectionOf`).
      - If it holds the Task's Status: the choice commits (no second step).
      - Otherwise: a Status step lists the destination's Statuses in Board order, no default selection, headed "“{Status}” is not in {Project}. Choose a Status:". Choosing one commits.
      - Esc steps back, innermost first: Status step → Project step → closed, focus back on the control.
    - **Commit is not optimistic** (Q9): the card stays until the `200`. While in flight the picker is replaced by a `role="status"` line "Moving to {Project}…" that holds focus; a second press is ignored.
    - **Done:** the card leaves the Board; the detail panel closes; focus goes to the column that held the Task (the Task-deleted precedent, `EXPERIENCE.md:271`); a polite announcement: "Moved “{title}” to {Project}." or, when a Status was chosen, "Moved “{title}” to {Project}, in {Status}."
    - **Refused:** the panel stays open with the reason in its error region, and nothing moved:
      - `409 status-destination-required` (the destination's set changed since it was read): re-read the set and show the Status step;
      - `400 invalid-status-destination`: re-read the set and show the Status step again, with the reason;
      - `400 invalid-task-project` (the Project was deleted or the list is stale): re-read the Project list and show step 1 again;
      - `409 bound-reached`: "{Project} is full (5,000 Tasks). Nothing was moved.";
      - `404`: the Task is gone or moved elsewhere; handled as the panel handles a `404` today.
      - A request that never got an answer does not claim "nothing moved" (6.2 ledger lesson).
    - Copy in `ClientCopy.resx`, sentence case, mapped in `RefusalCopy`. Logical properties, usable at 320px. The picker's state is purged on Space switch, sign-out and Account switch.
    - The fake API (`FakeYelloApi`) implements the route with the host's decision order (AC6), so the browser cases do not prove the fake.

15. **S-2 after every case; nothing materialised** (AD-16, AD-17). Assert `WireDatabase.TasksOutsideTheirProjectsSetAsync` = 0 after every Slices case of this story. The schema gate (`ProjectEndpointTests.StatusSchema.cs`) stays green unamended.

16. **The move touches neither the Task id nor the Space** (`epics.md:2325-2329`). A wire case asserts the Task's id and `SpaceId` before and after, and that `GET …/tasks/{taskId}` answers the same id with the new `ProjectId`.

## Tasks / Subtasks

- [ ] **Task 1 — Domain and Application** (AC2-AC9)
  - [ ] `ITaskStore`: a sorted two-Project lock port (for example `LockProjectsAsync(IReadOnlyCollection<Guid>)`) and a reparent write (for example `MoveToProjectAsync(taskId, sourceProjectId, destinationProjectId, statusId)` → a `TaskProjectMove` result: `Moved`, `NotFound`). Update every fake `ITaskStore` in the tests.
  - [ ] A read for "is this Project visible in the Space" (reuse an existing Project read if one fits; `IProjectStore` or the store's own `Projects.AnyAsync`).
  - [ ] `Yello.Application/Tasks/MoveTaskToProject/MoveTaskToProjectHandler.cs` (the slice name 2.6 reserved). Decision order:
    1. plain read Task → `NotFound`;
    2. plain read destination: missing or same Project → `ProjectInvalid`;
    3. sorted locks;
    4. re-read Task under the locks: missing or `ProjectId` changed → `NotFound`;
    5. re-read destination: missing → `ProjectInvalid`;
    6. `EffectiveOfAsync(destination)`: a supplied Status not in it → `StatusDestinationInvalid`; none supplied and the current Status not in it → `StatusDestinationRequired`;
    7. `IBoundCounter.CountAsync(Bound.TasksPerProject, destination)` ≥ limit → `BoundReached`;
    8. write; read Labels; return the Task.
  - [ ] Remarks: why both locks are taken sorted and only through the helper; why the bound is counted here and not in the pipeline (Q5); why a re-read under the lock answers `404`.
  - [ ] Handler tests with fakes (`tests/Yello.Tests.Slices/Tasks/MoveTaskToProject/MoveTaskToProjectHandlerTests.cs`), one per outcome and the decision order.
- [ ] **Task 2 — Infrastructure** (AC6, AC8, AC9)
  - [ ] `TaskStore`: the lock port (calls `ApplicationLocks.TakeProjectTasksAsync(dbContext, ids)`), and the reparent: row `UPDLOCK, INDEX(1)` read, check `ProjectId == source`, last key of the destination column (`MAX(PositionKey)` for `(destination, status)`), one write of `ProjectId`, `StatusId`, `PositionKey`. Update the class remarks: the move between Projects is the fifth key placer and takes two Project locks in the helper's order.
  - [ ] `TaskStore.DeleteAsync`: re-check `ProjectId` under the row lock; a moved Task is `NotFound`. Update its remarks.
  - [ ] `ApplicationLocks`: update the `TakeProjectTasksAsync(…, IReadOnlyCollection<Guid>, …)` and `StatusDefaults` remarks to state what is built (a move between Projects takes its two Projects' locks through it).
  - [ ] `RemoveSpaceStatusHandler.cs:18`: correct the remark (ledger `:1087`).
  - [ ] No `Where SpaceId`, no `IgnoreQueryFilters`; raw SQL only here, with parameters.
- [ ] **Task 3 — Contracts and Host** (AC1, AC2, AC4, AC7, AC11-AC13)
  - [ ] `TaskRoutes.ProjectMove` = `/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}/project` and `ProjectMoveOf(spaceId, taskId)`; remarks list every answer.
  - [ ] `Yello.Contracts/Tasks/MoveTaskToProjectRequest.cs` (`Guid ProjectId`, `Guid? StatusId`).
  - [ ] `ProblemTypes.InvalidTaskProject` = `urn:yello:problem:invalid-task-project` (400).
  - [ ] `TaskResponse.ProjectId` (additive `init`), filled everywhere a `TaskResponse` is built (AC11). Find every construction (`TaskEndpoints.Response`, Board and List readers, the fake API).
  - [ ] `Yello.Host/Endpoints/TaskProjectMoveEndpoints.cs` + a body reader (`TaskProjectMoveBody.cs`) on the `TaskMoveBody` pattern. `RequiresCapability(ManageTasks)`, both surfaces, no `ConsultsBound`. Register in `Program.cs`.
  - [ ] Amend the gates in AC13 and add the new ones.
- [ ] **Task 4 — Client** (AC12, AC14)
  - [ ] `TaskCapabilities` already decides who can move; reuse it (no new predicate unless the twin gate needs one).
  - [ ] New `ProjectMovePicker.razor` (steps, no default, Esc steps back, one tab stop per step, on the `MovePicker` / `PickerOption` patterns).
  - [ ] `TaskDetail.razor`: the control and the picker host only. The writing and the replica live in the Board (a new `BoardView.ProjectMove.cs` partial), through an `OnMoveToProject` callback, as `OnMove` does today.
  - [ ] `ClientCopy.resx`, `RefusalCopy.cs` (the new type and the reused ones in this context), `components.css` if needed.
  - [ ] `FakeYelloApi.ProjectMove.cs`: the route with AC6's decision order, the bound, hold/release for the in-flight case, and the Board columns that follow.
- [ ] **Task 5 — Ledger and docs**
  - [ ] Write the disposition of every owned ledger entry (table above). Leave none blank.
  - [ ] Additive dated notes: AD-17 (the cross-Project move: two `ProjectTasks` locks via the sorted helper, one statement, decision order) and AD-25 (the Tasks-per-Project bound is also counted by the cross-Project move, in its slice after its locks, because the destination is in the body and a pipeline lock taken first would break the sorted order). Additive only.
  - [ ] Add a low ledger entry, owner **UX**, for the card menu's "Move to Project" if Q10 (a) is taken.
- [ ] **Task 6 — Tests** (see Testing requirements)

## Dev Notes

### The shape of the story in one paragraph

One Task, one row, one statement - but the first statement that changes `ProjectId`, so the work is in the locks and in the other writers. The move takes the source and destination `ProjectTasks` locks through the one sorted helper 6.4 built for it, re-reads the Task and the destination under them, decides the Status against the destination's derived set (preserve, apply the supplied one, or refuse `409`), counts the destination against its 5,000 bound, and writes `ProjectId`, `StatusId` and an end-of-column key together. The deletion learns the re-check the edit and the 2.6 move already have. Task detail gets a "Move to Project" picker with no default selection and a Status step only when the destination lacks the Task's Status.

### Dominant finding: the bound and the lock order constrain each other

- **NFR-8 requires the destination's bound to be enforced** ("A bound that is not enforced is a defect"). A move into a full Project would otherwise hold 5,001 Tasks.
- **The pipeline cannot count it**: AD-25's single choke point counts the route's `{projectId}` before the endpoint and takes that Project's lock first. Putting the destination in the route would make the pipeline take the destination's lock *before* the slice takes the sorted pair, so a move A→B and a move B→A could each hold one lock and wait on the other: a deadlock, answered as a `500`.
- **So the slice counts**, after the sorted locks, with the same counter, limit and refusal body. This is the one recorded deviation from "a slice never counts" (`RequiredBound.cs` remarks). It is an additive AD-25 note, not a rule change: the counting code, the limit and the refusal stay single-sourced.

### Contradictions found (recorded; do not resolve beyond what is stated)

- **C1.** FR-41 says the description survives (`prd.md:444`). No description exists yet (FR-31 is Epic 7). Vacuously true; nothing to build.
- **C2.** The UX spec defines the Move picker as "a destination Status and position" (`EXPERIENCE.md:211`, `:287`). It has no Project picker for the single-Task form. Q7 and Q10 decide it on the existing patterns.
- **C3.** `EXPERIENCE.md:207` returns focus to the originating card on close, "except where that card no longer exists", and specifies the deleted case only (`:271`). Q9 applies the deleted case.
- **C4.** The 2.6 move is Session-only (FR-35: positions are not writable over the API). FR-41 says "including via the API", and the 6.6 bulk form must be on the API "on the same terms as the single-Task form". This move writes no client position, so it is on both surfaces (Q2).
- **C5.** `RequiredBound.cs` says "A slice never counts." The bound must still be enforced here (Q5).

### Open questions

Each has a recommendation, which is the default. The dev phase decides each as a reversible, recorded decision in Completion Notes and the Change Log. **None is marked [LEE]**: no spine rule changes (AD-17 and AD-25 get additive notes), no schema change, no RLS change, and every contract change is additive.

1. **Route** (AC1). **(a) Recommended:** `POST …/tasks/{taskId}/project` with `{ projectId, statusId? }` → `200` with the Task. The Task's own URL stays Project-free (`TaskRoutes.cs:12-16`); the verb-path shape follows 6.2/6.4's `…/remove`. (b) Add `projectId` to `PATCH …/tasks/{taskId}`. Cost: the edit gate forbids a Project member by design, and the edit's `statusId` already means "a Status of the current Project". (c) `POST …/projects/{projectId}/tasks/{taskId}`. Cost: the pipeline's bound would lock the destination first (see the dominant finding).
2. **Surfaces** (C4). **(a) Recommended:** both Session and Token. No client position is written, so FR-35's one exception does not apply, and 6.6 needs this form on the API. (b) Session only, like 2.6. Cost: contradicts `prd.md:441` "including via the API".
3. **A bad destination Project** (AC2). **(a) Recommended:** one new type, `400 invalid-task-project`, byte-identical for random, foreign, own and deleted-meanwhile. A body id is validation inside the Space, as a foreign Status in a move body is a `400` today (`ISO-MOVE-FOREIGN-STATUS-IN-OWN-SPACE`). (b) The boundary `404`. Cost: a `404` on a Task the caller can see reads as "the Task is gone". (c) Treat the own Project as a `200` no-op. Cost: the route would then also be a Status change within a Project, which is the 2.6 move's and the edit's job.
4. **Locks** (AC6). **(a) Recommended:** source and destination `ProjectTasks` through `TakeProjectTasksAsync(dbContext, ids)` only, then the row; nothing else. (b) Also take `ProjectsPerSpace` to fence a Project deletion. Cost: serialises every move with every Project creation in the Space, while the destination re-read under its own lock already catches a deletion (a deletion holds that lock until commit).
5. **Bound** (AC7, C5). **(a) Recommended:** count in the slice after the sorted locks with `IBoundCounter`, answer the creation's `409 bound-reached` byte for byte, additive AD-25 note. (b) Do not enforce. Cost: violates NFR-8's "a bound that is not enforced is a defect". (c) Pipeline count with the destination in the route. Cost: the deadlock in the dominant finding.
6. **A supplied Status when none is needed** (AC4). **(a) Recommended:** validated and applied. One meaning whatever the state, and an API caller can file a Task into a chosen column in one call. (b) Validated and ignored, as 6.2 validates an unneeded destination. Cost: a caller who asked for a Status silently does not get it. (c) Refused. Cost: makes the request depend on state the caller may not have read.
7. **How the client learns a Status is needed** (AC14). **(a) Recommended:** read the destination's set with the existing `GET …/projects/{projectId}/statuses` when a Project is chosen; the server re-judges and a `409` re-reads. No count or choices in the problem body (6.2's precedent). (b) Send with no Status and open the Status step on the `409`. Cost: a round trip that fails by design on every mapped move. (c) A preview route. Cost: a new route with its own isolation rows.
8. **The other writers** (AC9, ledger `:574`). **(a) Recommended:** the deletion re-checks `ProjectId` under its row lock and answers `404` when the Task moved, matching the edit and the 2.6 move. (b) The deletion retries on the new Project. Cost: it would take a `ProjectTasks` lock while holding another and a row lock, out of the sorted order, which can deadlock with a move.
9. **Position and optimism** (AC8, AC14, C3). **(a) Recommended:** the end of the destination column; the client is not optimistic (the card stays until `200`, then leaves); focus goes to the column that held the Task; the panel closes. The move can be refused for state the client cannot see (bound, deleted Project, changed set), and an optimistic card that reappears in a column is worse than a short in-flight line. (b) Optimistic removal with rollback. Cost: a refusal re-inserts a card the person saw leave. (c) Let the person choose a position. Cost: needs the destination's Board, and the API cannot write positions.
10. **Where the control lives** (C2). **(a) Recommended:** Task detail only, beside Move. Record a low ledger entry (owner UX) for the card menu, which 6.6's column entry point may make redundant. (b) Also a third card-menu item. Cost: the Board's menu then needs the Project picker inside a card, and `BoardView` is at its dependency cap.
11. **`ProjectId` in `TaskResponse`** (AC11). **(a) Recommended:** append it, additive, filled everywhere. A Token client otherwise cannot tell where a Task lives after a move (or ever, from `GET …/tasks/{taskId}`). (b) Leave `TaskResponse` alone; the client knows what it sent. Cost: the answer does not state the truth of the write (AD-18's "the answer is the truth" practice since 6.2).
12. **Ledger `:1087`**. **(a) Recommended:** correct the remark and close it; it costs one paragraph. (b) Leave it to the epic. Cost: 6.5 is the next taker of that lock set, which is what the entry warns about.

### Locking (RCSI)

- **Global order:** `StatusDefaults` → `ProjectsPerSpace` → `ProjectTasks` (sorted, several only through the helper) → Task rows → Label rows → Label applications locks.
  - Cross-Project move: `ProjectTasks` {source, destination} sorted → Task row.
  - Space-level removal: `StatusDefaults` X → `ProjectsPerSpace` → `ProjectTasks` of holders, sorted.
  - Project delta write: `StatusDefaults` S → `ProjectTasks`.
  - Project deletion: `ProjectsPerSpace` → Project row → `ProjectTasks`.
  - Creation: `ProjectTasks` (pipeline). Status edit, 2.6 move, deletion: `ProjectTasks` → Task row.
  - Labels-only edit: Task row → Labels → applications. Assignment: Memberships S.

  No cycle: every multi-`ProjectTasks` taker uses the same ordinal order of lock names.
- Every read the decision rests on happens after the last lock is granted. Plain statements under RCSI then see every committed write of every holder.
- **Race cases** (deterministic, `LCK_M_%` via `WaitUntilBlockedOnALockAsync`, never timing):
  1. Hold the source's lock (as a Status edit in flight would); the move queues. Released, the move decides on the committed Status.
  2. A Status edit queued behind a move on the source's lock answers `404` and writes nothing; the Task is in the destination.
  3. A deletion that read the source before the move, queued on the source's lock: `404`, and the Task survives in the destination (AC9). Fails today.
  4. Hold the destination's deletion (`ProjectsPerSpace` + its `ProjectTasks`); the move queues. Released: `400 invalid-task-project`, Task unchanged.
  5. Two opposite moves (Task X A→B, Task Y B→A), both queued behind one held lock: both `200`, no deadlock victim.
  6. A destination at 4,999 and two moves into it (or a move and a creation): exactly one succeeds; the other is `409 bound-reached`. Seed with `TaskWire.SeedTasksAsync`.
  7. A Project delta removal of the Task's Status in the destination (6.2) held; the move with no `statusId` queues. Released: `409 status-destination-required`.

### Architecture compliance (must)

- **AD-21 ring:** port in `Yello.Domain/Tasks/`; slice in `Yello.Application/Tasks/MoveTaskToProject/`; store and locks in `Yello.Infrastructure/Persistence/`; endpoint in `Yello.Host/Endpoints/`; contracts in `Yello.Contracts/Tasks/`.
- **AD-1 / AR-3:** no Role read in the slice. The bound count is the stated exception (Q5).
- **AD-2:** no `Where SpaceId`, no `IgnoreQueryFilters` (gate `No_production_code_disarms_the_query_filter_outside_its_allowlist`). Lock names carry `ScopedSpaceId`.
- **AD-3 / AD-20:** an in-Space not-found answers the boundary body and records nothing.
- **AD-15:** one key written; none renumbered; none from the client.
- **AD-16 / AD-17:** one derivation; one transaction; S-2 after every case.
- **AD-18:** idempotent. **AD-19 / C-1, C-2:** additive contracts only. **AR-34:** stable `urn:yello:problem:*` codes.
- **Logs:** never Task or Project content (I-17).
- **Comments** (CLAUDE.md): why, not history; no story, AC, Q or ledger ids.

### Traps

- **S1135 (ledger `:114`).** Never write the first default's name in any comment, doc comment or Razor comment. In code use `DefaultStatusSet.Names[0]`.
- **Two single lock calls in source-then-destination order** look right and deadlock against the reverse move. Only the sorted helper.
- **`TaskItem` tracked by EF:** if the reparent loads the entity and saves it, check the `UPDATE` sends only `ProjectId`, `StatusId` and `PositionKey`. A raw `UPDATE` with parameters is equally fine.
- **Unique key collision:** the destination's last key must be read for `(destination, status)` after the destination's lock, never computed from the source key (6.2 plant 9).
- **Shared isolation worlds.** `ISO-DIRECT-BOARD`, `ISO-DIRECT-STATUSES`, `ISO-AGGREGATE` assert SA's Projects and Tasks. Isolation rows here refuse and write nothing; any success seed belongs in a Space of its own. Do not push B past its 50-Project bound.
- **`TaskResponse.ProjectId`** touches every Task answer, the fake API and the client's records that compare `TaskResponse` (a record with a list member compares by reference; compare a projection).
- **Locators.** A new "Move to Project" control collides with any locator matching "Move" by text in Task detail. Scope locators.
- **Plants** must compile under the analyzers. Restore each from saved content in a `finally`; never `git checkout --`, `git restore` or `git reset`. Run in small groups and check `git status` for residue.
- `TreatWarningsAsErrors`: 0 warnings. `--no-build` runs stale binaries.

### Library / framework

No new package or project; no web research needed. Everything used is in the codebase: EF Core 10.0.11, SQL Server 2025 (RCSI, `sp_getapplock`), ASP.NET Core 10.0.11 Minimal APIs with Asp.Versioning.Http 10.0.0, Blazor WebAssembly 10.0.11, xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md` (traits `:49-86`; rules and races `:87-207`; browser `:209-437`; Slices `:556-574`, `:822-835`; isolation `:790-912`). P0: isolation, race, S-1, S-2. `Requirement` values: FR-41, AR-24, AD-15, AD-17, AD-18, AD-25, FR-16, FR-35, NFR-1, NFR-8.

- **No new container-backed class.** Slices cases go in new partials of `TaskEndpointTests`: `.ProjectMove.cs`, `.ProjectMoveRace.cs`. Isolation rows in `IsolationCases.ProjectMove.cs`. Browser class `TaskProjectMoveTests.cs` (fake API).
- **Seed through the product.** Raw SQL only for S-2, catalogue reads, the S-1 trigger, held locks and the bound seed.
- **Memory budget.** One `dotnet test` at a time; iterate filtered; develop ends with one background full run, `--max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`.

**Minimum Slices cases:**

| # | Case |
|---|---|
| 1 | Status in the destination's set, no `statusId`: `200`; same Status; end of the destination column; source column's other keys unchanged; S-2. |
| 2 | Status not in the destination's set (a removed default there; a source-added Status), no `statusId` → `409 status-destination-required`; nothing written. |
| 3 | Same with a valid `statusId`, including a destination-added Status → `200`, in it, at the end. |
| 4 | A supplied `statusId` that would not be needed → applied (Q6). |
| 5 | Invalid `statusId` (the source's added Status, foreign, random, a removed default) → identical `400 invalid-status-destination`. |
| 6 | Destination random, foreign, own Project → identical `400 invalid-task-project`. |
| 7 | Bodies: missing `projectId`, `positionKey`, `afterTaskId`, `spaceId`, unreadable → `400 invalid-task-edit`; non-id `projectId` → `400 invalid-task-project`. |
| 8 | Survivors (AC5): id, Space, title, due date, Assignee, Labels and their order, `FirstLabelName`, `CreatedAt`; no outbox row. |
| 9 | Destination at 5,000 → `409 bound-reached` byte-identical to the creation's; nothing written. |
| 10 | Viewer `403` on both surfaces with its record; Member `200` on Token. |
| 11 | Foreign Space; foreign Task id in own path → identical `404`. |
| 12 | Idempotent replay: the original answer, nothing moved again (move it back by another request in between to prove it). |
| 13 | S-1 (AC10). |
| 14 | `GET …/tasks/{id}` after a move: same id, new `ProjectId` (AC11, AC16). Source Board no longer lists it; destination Board does. |
| 15 | Races 1-7. |

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | Reparent without the Status check | case 2 and S-2 |
| 2 | Locks taken as two single calls, source first | race 5 |
| 3 | No destination lock (source only) | race 6 or the creation race |
| 4 | No bound count | case 9, race 6 |
| 5 | Deletion without the re-check | race 3 |
| 6 | Destination not re-read under its lock | race 4 |
| 7 | Key computed from the source key | case 1 (unique key or order) |
| 8 | Supplied Status ignored | case 4 |
| 9 | Project picker pre-selects the first Project | browser no-default case |
| 10 | Status step pre-selects the first Status | browser no-default case |
| 11 | The control rendered for a Viewer | browser absence case |

**Browser cases (fake API):**

- The control is absent for a Viewer and present for a Member.
- Step 1 lists the other Projects, not the current one, with nothing selected.
- A destination holding the Status commits on the Project choice; the card leaves; the panel closes; focus is on the column that held it; the announcement.
- A destination lacking the Status shows the Status step with nothing selected; choosing commits; the announcement names the Status.
- Esc steps back Status → Project → closed, focus on the control.
- In flight: the status line holds focus; a second press sends nothing.
- `409 status-destination-required` at commit re-reads and shows the Status step; `bound-reached` and `invalid-task-project` show their reasons and nothing moves.
- No other Project: the one-line message.
- Usable at 320px.

### Previous story intelligence (6.1-6.4)

- **The shape that works:** lock → read under the lock → decide purely → write in key order → answer the truth. The re-read under the lock answering `404` when `ProjectId` changed is 6.2's (Q6 (a)).
- **6.4 built the helper this story needs** (`TakeProjectTasksAsync` with a collection) and named this move as its next user. 6.4's plants proved a missing lock is caught only by a race case; write races 3, 5 and 6 before the implementation.
- **6.2 review:** a migration case seeded no Assignee, so "Assignee unchanged" passed on `null`. Seed everything non-null.
- **6.2 / 6.4 client lessons:** no default selection (a state default, not a markup `selected`, is the plant that proves it); the pressed control stays until the status region holds focus; a second press in flight is ignored; a control that removes itself says where focus goes; singular and plural; a request with no answer does not claim "nothing moved".
- **6.3 review:** a gate forbids `IgnoreQueryFilters` in production; RLS alone hid a bypass from every wire case.
- **Recurring review patch in every story:** comments narrating phases or citing story, AC, Q, ledger or GATE ids. Do not write them, including in test files.
- **Scaffold lessons:** wait on a column heading, not `.board-column` `Nth(n)`; records with list members compare by reference; every Token-reachable route needs an `ISO-TOKEN-OWN-OTHER-SPACE-*` row.

### Git intelligence

- 6.4: `ae6a9b8` story, `5b597a6` ATDD, `c90adee` implementation, `1c4f5e0` review close. 6.3: `70e8716`, `4b1ed3d`, `86eec80`, `ab19a56`.
- The cycle: story file → ATDD scaffolds (`Skip = "red: …"`, `_bmad-output/test-artifacts/atdd-checklist-<key>.md`, `PendingUntil` isolation rows) → implementation → review. The orchestrator commits each phase.
- The latest migration is `20261121190936_StatusDeltaSequenceUnique`. 6.5 expects none.

### Project Structure Notes

- **New (expected):**
  - Application: `Yello.Application/Tasks/MoveTaskToProject/MoveTaskToProjectHandler.cs`.
  - Contracts: `Yello.Contracts/Tasks/MoveTaskToProjectRequest.cs`.
  - Host: `Yello.Host/Endpoints/TaskProjectMoveEndpoints.cs`, `TaskProjectMoveBody.cs`.
  - Client: `ProjectMovePicker.razor`, `BoardView.ProjectMove.cs`.
  - Tests: `TaskEndpointTests.ProjectMove.cs`, `.ProjectMoveRace.cs`, `MoveTaskToProjectHandlerTests.cs`, `ProjectMoveWire.cs`, `IsolationCases.ProjectMove.cs`, `RefusalBoundaryGateTests.ProjectMove.cs`, `TaskProjectMoveTests.cs`, `FakeYelloApi.ProjectMove.cs`.
- **Updated (expected):**
  - `ITaskStore.cs`, `TaskStore.cs`, `ApplicationLocks.cs` (remarks), `RemoveSpaceStatusHandler.cs` (remark);
  - `TaskRoutes.cs`, `TaskResponse.cs`, `ProblemTypes.cs`, `TaskEndpoints.cs` (`Response`), the Board and List readers that build `TaskResponse`, `Program.cs`;
  - `TaskDetail.razor`, `BoardView.razor` (the callback), `RefusalCopy.cs`, `ClientCopy.resx`;
  - every test fake of `ITaskStore`, `RefusalBoundaryGateTests.TaskEdit.cs`, `IsolationCases.cs` (`All`);
  - `deferred-work.md`, `sprint-status.yaml`, `ARCHITECTURE-SPINE.md` (AD-17, AD-25 notes).

### References

- `epics.md`: `:212` (AR-24), `:383`, `:469-477`, `:2111-2115`, `:2290-2329` (this story), `:2331-2386` (6.6 boundary).
- `prd.md`: `:66`, `:342`, `:398`, `:406`, `:435-454`, `:487-490`, `:594-598`, `:718-720`, `:736-751`, `:882`.
- `ARCHITECTURE-SPINE.md`: AD-2 `:85-87`, AD-3 `:97`, AD-8 `:136`, AD-10 `:152`, AD-15 `:178-184`, AD-16 `:186-194`, AD-17 `:196-201`, AD-18 `:203-207`, AD-20 `:216-220`, `:308-316`.
- `EXPERIENCE.md`: `:199-212`, `:256-258`, `:266`, `:271`, `:283-295`, `:324`, `:331`, `:335`, `:555-566`. `DESIGN.md`: `:473-474`.
- `SPEC.md`: `:126-127`; `spec-yello/acceptance-criteria.md:223-239` (CAP-41); `role-capability-matrix.md:13`, `:32`.
- `test-design-qa.md`: `:265-280`, `:318-319`. `implementation-readiness-report-2026-08-22.md`: `:520`, `:943-955`.
- `deferred-work.md`: `:114`, `:574`, `:861`, `:1030-1031`, `:1038-1039`, `:1072`, `:1075`, `:1087`.
- Story files: `2-6-move-and-reorder-a-task-by-explicit-control.md:14`, `:78`, `:170-176`; 6-1 to 6-4.
- Code: everything in "What already exists".

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-6-5-move-a-task-to-another-project.md`. Every scaffold follows each open
question's recommended option (a). A skip that depends on one names it.

| File | Cases | ACs | Today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.ProjectMoveRace.cs` | 11 (8 methods): races 1-7 and the queued creation | AC2, AC4, AC6, AC7, AC9 | 7 skipped `red:`; **4 green, active** (see below) |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.ProjectMove.cs` | 31 (15 methods): minimum Slices cases 1-14, plus every Task answer stating `projectId` | AC1-AC5, AC7, AC8, AC10-AC13, AC15, AC16 | skipped `red:` |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.ProjectMove.cs` | 8 gates | AC1, AC2, AC6, AC11-AC13 | 6 skipped `red:`; **2 green, active** |
| `tests/Yello.Tests.Architecture/RefusalCopyTests.ProjectMove.cs` | 1 | AC14 | skipped `red:` |
| `tests/Yello.Tests.Isolation/IsolationCases.ProjectMove.cs` | 6 rows, both surfaces | AC12, AC13 | `PendingUntil: "6.5"` |
| `tests/Yello.Tests.Client/TaskProjectMoveTests.cs` | 12 (10 methods), fake API | AC12, AC14 | skipped `red:` |

**Supporting files:**
- `Yello.Tests.Shared/ProjectMoveWire.cs` holds the stand-ins `Route`, `PathOf` and `InvalidTaskProject`, the bodies, and
  `ProjectIdIn`. Swap every use for `TaskRoutes.ProjectMove`, `TaskRoutes.ProjectMoveOf` and
  `ProblemTypes.InvalidTaskProject`, and delete the stand-ins.
- `Yello.Tests.Slices/Tasks/ProjectMoveSliceWire.cs` holds:
  - the request on both surfaces;
  - `TwoProjectsAsync`;
  - `HoldProjectMoveAsync`, the move played in SQL in the product's shape (sorted `ProjectTasks` locks, then the row, then
    one `UPDATE`, the key read under the destination's lock);
  - `SurvivorsAsync`;
  - the S-1 `AFTER UPDATE` trigger.
- `Yello.Tests.Client/FakeYelloApi.ProjectMove.cs` decides the route in AC6's order, with the bound, and with hold, release,
  refused and unanswered. It is hooked in `FakeYelloApi.SpaceDeletion.cs`.
- One spread line was added in `IsolationCases.All`.

The handler unit tests are not scaffolded, because they need the types to compile. Write them with the slice.

**Green today (standing regression gates, active):**
- `A_Status_edit_or_a_move_queued_behind_a_cross_Project_move_answers_404_and_writes_nothing` (edit, move). The
  existing writers already re-check `ProjectId` under the source's lock.
- `A_creation_queued_behind_a_cross_Project_move_counts_the_moved_Task_and_lands_after_it` (full, not full). The
  creation's bound count already waits on the destination's lock.
- `No_Task_request_contract_carries_a_Space_member` and its planted-type oracle.

**Activation order** (riskiest assumption first):
1. The lock order: `Two_opposite_moves_queued_on_one_lock_both_land_and_neither_deadlocks`.
2. The bound under concurrency: `At_4999_a_move_and_a_second_write_into_the_destination_admit_exactly_one`.
3. The re-reads under the locks: races 1, 4 and 7.
4. The deletion re-check: `A_deletion_queued_behind_a_cross_Project_move_answers_404_and_the_Task_survives`. It needs no
   route.
5. The contract gates.
6. The core wire cases (kept, required, applied, supplied-unneeded, refusals, bodies).
7. Survivors, the bound, S-1, the read-back, and every answer stating `projectId`.
8. Role, boundary and replay. Then the 6 isolation rows go live before review (GATE-003).
9. The browser cases and the copy gate.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

**Existing gates the implementation will turn red. Amend them deliberately, in the same change:**

- **Wire-shape gates** (`TaskResponse` gains `ProjectId`):
  - `RefusalBoundaryGateTests.The_due_date_is_a_nullable_DateTimeOffset_on_the_Task_and_its_response`;
  - the exact `TaskProperties` / `EditedTaskProperties` / `AssignedTaskProperties` pins in `TaskEndpointTests.cs:48`,
    `TaskEndpointTests.Edit.cs:37`, `BoardEndpointTests.cs:45`, `BoardPagingTests.cs:245`, `ListPagingTests.cs:59` and
    `ActiveSpaceEndpointTests.AssignTask.cs:47`.
- **Refusal-boundary gates:**
  - the two exact Task-request sets (`RefusalBoundaryGateTests.TaskEdit.cs:50`, `.Tasks.cs:126`), widened by exactly
    `MoveTaskToProjectRequest`;
  - `The_routes_under_a_Task_are_its_read_edit_deletion_and_move` (`.Move.cs:123`), which says no route moves a Task to
    another Project. Add `POST …/project` and reword it. Do not delete it.
- **Isolation route-coverage gates:**
  - `IsolationCaseListTests.Every_api_route_the_Host_maps_…` and `Every_Space_scoped_route_a_Token_reaches_…`, until the
    6 rows are live;
  - GATE-003, if 6.5 reaches review with any row still pending.
- **Role-matrix gate:** none expected. `ActiveSpaceEndpointTests.RoleMatrix.cs:53` already has a `ManageTasks`
  representative.
- **ProblemResponse parameter gates:** none expected. No problem-body member is added; only a new `ProblemTypes`
  constant, which the uniqueness gates accept.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

- 2026-11-22: Story created (bmad-create-story). Status: ready-for-dev.
