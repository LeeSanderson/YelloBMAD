# Story 6.6: Move a whole Status of Tasks at once

Status: ready-for-dev

Epic: 6 — Shape Statuses per Project and retire work safely (the sixth and last story)
Story key: `6-6-move-a-whole-status-of-tasks-at-once`

**Requirements owned.** Story Coverage Index (`epics.md:385`): **FR-41** (bulk form), **AR-24**, **UX-DR17** (the bulk-move bar), **UX-DR39** (focus destinations). Also carried:

- **FR-41 bulk form** (`prd.md:447-454`):
  - every Task currently in one Status moves from one Project to another in the same Space, in one operation (`:447`);
  - exactly one mapping decision: the Status is kept where the destination has it, and one destination Status is required where it does not (`:449`);
  - the entry points are a Board column and a List View filtered to one Status, and the form is on the API "on the same terms as the single-Task form" (`:450`);
  - the move is atomic, and a refusal is visible rather than silent (`:451`);
  - a Viewer can use neither form (`:452`);
  - it is the safe path for retiring a Project (`:454`).
- **Out of scope** (`prd.md:843`): moving Tasks from several Statuses at once. The bulk form takes one Status at a time. The reason is recorded at `addendum.md:169`: a per-Status mapping table "opens a transaction-size question at the 5,000-Task bound that neither NFR-5 nor NFR-8 answers".
- **FR-26** (`prd.md:487-490`): no partial application, and the destination is decided under the lock.
- **FR-35** (`:594-598`): the API offers every browser capability on the same authorisation terms.
- **FR-19** (`:398`): no operation moves a Task to another Space.
- **NFR-8** (`prd.md:745`): at most 5,000 Tasks per Project, and a bound that is not enforced is a defect. A bulk move adds *n* Tasks to the destination.
- **NFR-5** (`:716-720`): write p95 of 500 ms. This is not asserted at 5,000 Tasks (Q11).
- **Architecture:**
  - **AD-15** (`ARCHITECTURE-SPINE.md:178-184`): only the moved Tasks' keys are written, and no destination sibling is renumbered.
  - **AD-16 / AD-17** (`:186-202`, including the 6.4 lock-order note `:201` and the 6.5 move note `:202`).
  - **AD-18** (`:204-209`): idempotency.
  - **AD-25** (`:260-265`, plus the 6.5 note "the one slice that counts a bound", which this story must amend).
- **Test design:** **S-1** (`test-design-qa.md:318`, P0) names "the bulk move" explicitly, so a forced failure must leave every Task in place. **S-2** (`:319`, P0) is asserted after every case. P-5 (`:313`) and P-10 (`:314`) also apply.
- **AD-2 / AD-3 / AD-20, NFR-1, FR-15** (I-1, I-2, I-4, I-5, I-13, V-1, P0): a new Space-scoped route needs isolation rows on both surfaces.
- **FR-16:** `Capability.ManageTasks` (Owner, Admin, Member), the same capability as the single-Task form (`TaskCapabilities.PermitsManageTasks` in the client).
- **UX:**
  - `EXPERIENCE.md:200` (the column holds the entry point), `:212`, `:257` (in flight), `:258` (refused: say nothing moved) and `:405` (focus table).
  - `:211` and `DESIGN.md:473`: no default selection.
  - `:85` (absent, never disabled, for a Role reason), `:108` (copy "Moving 4,812 Tasks."), `:381-390` (live regions) and `:415-421` (320px, one column with the Status pager).
  - `DESIGN.md:263-269` (the `bulk-move-bar` token block), `:319` and `:474` (the only accent-bordered component), and `:360` (accent is not a signal of danger).
  - UJ-9 (`EXPERIENCE.md:555-566`).

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason.** This is the last story of Epic 6, so no entry may be left with the owner "Epic 6" or "story 6.6".

| Line | Entry | Recommended disposition |
|---|---|---|
| `:1104` | The fake API reads the body after the Task lookup, though the Host reads it first. No fake Task answer except the move carries `projectId`. | **CLOSE** (AC14, Q14). Read the body first in `FakeYelloApi.ProjectMove.cs`. Give `FakeTask` a `ProjectId` filled on every fake Task answer. The new bulk fake route reads its body first too. |
| `:1105` | The two bound gates admit `MoveTaskToProjectHandler` by name. They pin neither the bound it counts nor that it counts after `LockProjectsAsync`. Their names still say "nothing but the pipeline". | **CLOSE** (AC13, Q6). The gates admit exactly the two cross-Project slices, under renamed methods and summaries. Each slice gets a handler test with a recording fake that pins three things: `Bound.TasksPerProject` is what is counted, the count runs after `LockProjectsAsync`, and it counts the destination only. |
| `:1106` | No gate pins `ITaskStore.MoveToProjectAsync` as the only production writer of `TaskItem.ProjectId`, whose setter is public. | **CLOSE** (AC13, Q7). A gate names the two writers that move a Task out of its Project (the single move and the set-based bulk statement), plus creation's initialiser. Plant a third writer and check that the gate catches it. |
| `:114` | The first default Status name may not appear in any comment (S1135). | **RE-OWN to sweep as a standing trap** (Q15). It outlives Epic 6. Add "Still open after story 6.6 (end of Epic 6)". Keep the first default's name out of every new comment. |
| `:1091` | The Board's card menu offers no "Move to Project". | **CLOSE as a decision** (Q10). The single form stays in Task detail and the bulk form lives on the column, so the card menu gains nothing. |
| `:645` | Opening Task detail from a List View row; "decided before story 6.6". | **RE-OWN to UX/PM** (Q9). The bulk entry in the List View is a filter-level action and opens no Task, so 6.6 does not need it. Record that 6.6 makes the List View a writer for the first time, for the bulk move only. |
| `:1030`, `:1031`, `:1075` | Name comparison against collation; names that look alike; the default-name index backstop. | **RE-OWN to the Epic 6 retrospective** with "Left open by story 6.6: a bulk move compares no names". |
| `:1038`, `:1039` | The 6.1 migration is unsafe for the old revision; its `Down()` loses data. | **RE-OWN to the Epic 6 retrospective**: 6.6 needs no migration. |
| `:1072` | Space-level writes read every delta, O(Projects × rows). | **RE-OWN to the Epic 6 retrospective**: a bulk move reads two Projects' sets only. |
| `:1078` (owner Lee, not re-owned) | The 6.4 50 × 5,000 move is unmeasured against NFR-5 and the 240 s ingress timeout. | **Note only.** Append the duration of this story's 5,000-Task bulk wire case (Q11). The owner stays Lee. |

## What already exists — do not rebuild it

| Concern | Where it lives today | What 6.6 does with it |
|---|---|---|
| **The single-Task form (6.5)** | `MoveTaskToProjectHandler` (`Yello.Application/Tasks/MoveTaskToProject/`) runs in this order: plain read of the Task, plain read of the destination (`ProjectExistsAsync`; the same Project counts as invalid), `LockProjectsAsync([source, destination])`, a re-read of the Task (`404` if it moved), a re-read of the destination (`400`), `EffectiveOfAsync(destination)`, supplied Status not in the set (`400 invalid-status-destination`), landing Status (supplied, else the Task's own) not in the set (`409 status-destination-required`), `IBoundCounter.CountAsync(TasksPerProject, destination)` against `BoundRegistry` (`409 bound-reached`), then `MoveToProjectAsync`. `TaskProjectMoveEndpoints.cs` and `TaskProjectMoveBody.cs` read the body as a `JsonDocument` and define the refusal bodies `InvalidProject()`, `InvalidStatus()` and `StatusRequired()`. | **Unchanged in behaviour.** The bulk slice follows the same decision order and answers the same refusal bodies byte for byte: reuse `TaskProjectMoveBody`'s refusal factories, or move them somewhere both endpoints share. Extract the Status decision (supplied, kept, or required) into one pure helper that both slices call, rather than writing it twice (Q5). |
| **Set-based Status migration** | `TaskStatusMigration.MigrateAsync(dbContext, projectId, from, to)` (`Yello.Infrastructure/Persistence/TaskStatusMigration.cs`). It reads the occupants in source key order, reads `MAX(PositionKey)` of the destination column, chains `PositionKey.After` through them, and writes one `UPDATE … JOIN OPENJSON`. It takes no row lock, and its remarks say why that cannot cycle with a Labels edit. 6.2 and 6.4 use it, at up to 50 × 5,000 rows. | **Generalise it with a destination Project** (Q7). One `UPDATE` sets `ProjectId`, `StatusId` and `PositionKey`, and the destination column's last key is read for `(destinationProject, destinationStatus)`. The Status removals pass the same Project for both. Do not write a second set-based key placer. |
| **Locks** | `ApplicationLocks.TakeProjectTasksAsync(dbContext, IReadOnlyCollection<Guid>, ct)` takes the locks sorted by ordinal lock name. `ITaskStore.LockProjectsAsync` is its port. The global order is `StatusDefaults` → `ProjectsPerSpace` → `ProjectTasks` (sorted) → Task rows → Label rows → Label applications locks. Task writers never take `StatusDefaults` or `ProjectsPerSpace`. | Source and destination are taken **only** through `LockProjectsAsync`, then the set-based `UPDATE` runs, with no other lock. |
| **Other writers that re-check `ProjectId`** | The deletion (`TaskStore.DeleteAsync`), the Status edit, the 2.6 move and the 6.5 move all re-read under the lock and answer `404` when the Task left the Project whose lock they hold. Creation counts the bound under the Project's lock in the pipeline. A Labels-only edit takes the row (`UPDLOCK, INDEX(1)`) without the Project lock. | **Unchanged.** A bulk move is one more writer that changes `ProjectId` under the source's lock, so each of these behaves the same against it as against the single move. Race cases prove it (Dev Notes → Locking). |
| **Bound** | `BoundRegistry.For(Bound.TasksPerProject)` gives 5,000. `IBoundCounter.CountAsync` takes the Project's lock (a re-entry here). `BoundaryProblems.BoundReached(bound)` is the refusal body. `SpaceLog.BoundReachedFor` logs it. Two Architecture gates (`RefusalBoundaryGateTests.Projects.cs:33-80`) admit one slice, `MovesATaskToAnotherProject`. | The bulk slice counts the destination after both locks and refuses when `count + n > limit` (Q6). The gates admit the second slice under renamed methods. The AD-25 note changes from "the one slice" to "the two cross-Project slices". |
| **Problem types** | `invalid-task-project` (400), `invalid-status-destination` (400), `status-destination-required` (409), `bound-reached` (409), `not-available` (404), `insufficient-role` (403), `invalid-task-edit` (400) | Reuse every one. **No new problem type** (Q4). |
| **Routes** | `ProjectStatusRoutes` (`Yello.Contracts/Projects/ProjectStatusRoutes.cs`) holds `Collection`, `Item`, `Place`, `Remove` and `Restore` under `/projects/{projectId}/statuses/{statusId}`. `TaskRoutes.ProjectMove` is `…/tasks/{taskId}/project`. | The new route hangs under the Project's Status (Q1). The pipeline counts a bound only on routes declaring `ConsultsBound`, and this route declares none. |
| **Board column** | `BoardColumn.razor` (342 lines): the head (`h2` id `h{StatusId:N}`, the true `Total` count, and a "+" only in the first column for a managing Role), a `ChildContent` slot, and `FocusColumnRequest`, which focuses the `<section>`. `BoardColumnResponse.Total` is the true count, with columns paged at 50 (`BoardPage.Size`). There is no column menu or column action today. | Add one column-head control, "Move to Project", for a managing Role when `Total > 0` (Q8). |
| **Board host** | `BoardView.razor.cs` is 1,377 lines and at the S1200 dependency cap (see the remarks in `ViewToggle.razor:12` and `BoardSpaceLists.cs:21`). The single move is written in `BoardView.ProjectMove.cs`. `Departed` and `Remove` drop a card and adjust `Total`. `_focusColumn` and `_focusColumnRequests` drive column focus. `Say` and `LiveAnnouncement` produce announcements. `_generation` is bumped on purge. `ColumnState` holds `Total`, `Tasks`, `After` and `Loading`. `LoadMoreAsync` and `Column(statusId)` live in `BoardView.Paging.cs`. | Put the Board side in a **new partial** (`BoardView.BulkMove.cs`). The bar component owns its HTTP write and reads, so `BoardView` gains no injected dependency (Q12). |
| **Project picker (6.5)** | `ProjectMovePicker.razor` (374 lines) takes `SpaceId`, `ProjectId` (excluded from the list), `StatusId`, `StatusName`, `OnCommit` (returning `ProjectMoveAnswer`) and `OnDismiss`. Its steps are Loading, Projects, Statuses, InFlight, Failed and NoOtherProject. It reads `ProjectRoutes.ListIn` and `ProjectStatusRoutes.CollectionOf`. Nothing is selected by default, each step has one tab stop, and Esc steps back. The in-flight line reads "Moving to {0}…". `AfterCommitAsync` re-reads after a refusal. | **Reuse it inside the bar**: give it the column's `StatusId` and name. Add one optional parameter for the in-flight line's text, or let the host draw it, rather than forking the picker (Q12). |
| **In-flight region precedent** | `StatusRemoveDialog.razor:17-36` and `:115-129`, and `SpaceStatusRemoveDialog.razor:17-19`: a `role="status"` line with `tabindex="-1"` names the scope, shows no percentage, takes focus before the controls leave, and ignores Cancel and Esc once sent. | The same pattern drives the bar's in-flight state. |
| **List View** | `TaskListView.razor` (160 lines) and `.razor.cs` (522 lines) are read-only for every Role. `TaskListFilters.Statuses` is a multi-select `HashSet<Guid>`, with Labels, Assignees, Unassigned and Due as the other filters. Rows load 50 at a time, `_total` holds the true count, and the view has its own `LiveAnnouncement`. | Add the bulk entry when **exactly one Status** is chosen and **no other filter is set** (Q9). This is the List View's first write. |
| **Accent** | Tokens `--accent` and `--border-emphasis-width` (2px) exist in `tokens.css:51-52` and `:309-310`. No accent border exists anywhere. `DesignFoundationGateTests.OfferIndicator.cs` forbids accent inside the context bar only. | `.bulk-move-bar` uses `border: var(--border-emphasis-width) solid var(--accent)`, `surface-card`, `rounded.md`, `spacing.3` and `meta` type (`DESIGN.md:263-269`). Add a gate that no other selector uses an accent border (AC10). |
| **Copy** | `ClientCopy.resx` has the `ProjectMove*` keys, `ProjectMoveFull` ("{0} is full ({1} Tasks). Nothing was moved."), `TaskProjectInvalid` and `StatusRemoveRefused` ("Nothing was moved."). `RefusalCopy.cs` maps `invalid-task-project`, `status-destination-required`, `invalid-status-destination` and the bound. | Add new `BulkMove*` keys (AC11). Reuse the mapped refusal copy where it reads true for many Tasks. |
| **Test helpers** | `WireDatabase.TasksOutsideTheirProjectsSetAsync` (S-2), `TaskWire.SeedTasksAsync`, `TaskWire.WaitUntilBlockedOnALockAsync`, `DeleteWire.HoldProjectRangeAsync`, `ProjectMoveSliceWire.cs` (`TwoProjectsAsync`, `HoldProjectMoveAsync`, the S-1 trigger, `SurvivorsAsync`), `ProjectMoveWire.cs`, and `FakeYelloApi.ProjectMove.cs` (hold, release, refused, unanswered). | Reuse them. Add a `StatusBulkMoveWire` (Shared) and slice wire helpers. Extend the fake with a bulk route of its own. |

**What is genuinely new in 6.6:**

1. A set-based write that changes `ProjectId` for every Task of one Status, under two Projects' sorted locks.
2. A bound check for *n* Tasks at once (`count + n > limit`).
3. A column-head entry point, a List View entry point, and the accent-bordered bulk-move bar.
4. The List View's first write.
5. The gates that close `:1105` and `:1106`.

**Explicitly not in this story:**

- A selection spanning several Statuses (`prd.md:843`). It is not offered, and the reason is recorded in remarks (AC9).
- Collaborative editing sessions (Epic 7). This story asserts only that a bulk move changes no Task id and no Space (AC12).
- Realtime publishing. Another tab sees the move on its next read.
- A Project deletion guardrail. "Nothing prevents deleting a Project with Tasks still in it" (`epics.md:2386`), so the deletion flow is unchanged.
- Cancelling a request already sent. The transaction is atomic and the bar's cancel exists before commit only (Q3).
- No migration, no new NuGet package, no new project. No commit, push, branch, Azure login or deploy. The orchestrator commits.

## Story

As someone retiring a Project,
I want to move everything in one Status to another Project in a single operation,
so that emptying a Project safely takes one action per Status rather than one per Task.

## Acceptance Criteria

1. **The route** (FR-41, FR-35, AR-24; Q1, Q2).
   - `POST /api/v1/spaces/{spaceId}/projects/{projectId}/statuses/{statusId}/tasks/project`. `{projectId}` and `{statusId}` are the **source** Project and Status. The body is `MoveStatusTasksToProjectRequest(Guid ProjectId, Guid? StatusId)`, naming the destination Project and an optional destination Status.
   - It declares `Capability.ManageTasks` and is served on **both** surfaces (Session and Token). No position is written.
   - It is idempotent through the pipeline's `Idempotency-Key` (AD-18). A replay returns the original answer and moves nothing again.
   - Success answers `200` with `MoveStatusTasksToProjectResponse(Guid ProjectId, Guid StatusId, int MovedCount)`: the destination, the Status the Tasks now hold there, and how many moved.
   - The body is read as a document inside the endpoint, **before any read**, with the 6.5 body rules:
     - unreadable, not an object, no `projectId`, or a `positionKey`, `afterTaskId`, `spaceId` or `taskIds` member → `400 invalid-task-edit`;
     - a `projectId` that is not an id → `400 invalid-task-project`;
     - a `statusId` that is present, not an id and not `null` → `400 invalid-status-destination`.

2. **What is selected** (FR-41 `prd.md:447`; Q2). The selection is **every Task holding `{statusId}` in `{projectId}` when the locks are granted**. It is never a list of Task ids from the caller. Specifically:
   - A Task created into that Status, or edited into it, before the move took its locks is moved.
   - A Task that left the Status before then is not moved.
   - `MovedCount` is the number written.
   - A Status that holds no Tasks answers `200` with `MovedCount = 0` and writes nothing. A supplied `statusId` is still validated first (AC4).

3. **The source must exist** (AD-3, AD-20). Each of the following answers the boundary `404 not-available`, byte-identical and not recorded, and nothing is written:
   - `{projectId}` is not a Project of the Space (random, or another Space's), or it was deleted while the move waited;
   - `{statusId}` is not in the source's effective set (random, another Space's, another Project's added Status, or removed from the source while the move waited).

4. **One mapping decision, on the single form's terms** (FR-41 `:449`, FR-26, AR-24; Q5). The destination's effective set is read after both locks.
   - A supplied `statusId` that is not in it → `400 invalid-status-destination`, byte-identical to 6.5's (source-added Status, foreign, random, removed default).
   - A supplied `statusId` that is in it is **applied**, even when the source Status would have been kept. This is 6.5's Q6 rule.
   - No `statusId`, and the destination holds the source Status → kept, with no mapping.
   - No `statusId`, and the destination lacks the source Status, and at least one Task is selected → `409 status-destination-required`, and nothing is written.
   - One decision covers every selected Task: they all hold the same Status by construction.

5. **The destination** (FR-41, FR-19, AD-2, I-13; Q4). It must be another Project of the request's Space. Otherwise the answer is `400 invalid-task-project`, byte-identical to 6.5's for each of these causes:
   - a random id;
   - another Space's Project;
   - the source Project itself;
   - a Project deleted while the move waited.

6. **One transaction, under both Projects' locks, in one order** (AR-24, AD-17; Q7). The decision order is:
   1. The body (AC1).
   2. Plain reads: the source Project is visible (else `404`), then the destination is visible and is not the source (else `400 invalid-task-project`).
   3. `LockProjectsAsync([source, destination])`. Never two single takes, never `StatusDefaults`, never `ProjectsPerSpace`.
   4. Under the locks, re-read the source Project (gone → `404`).
   5. The source's effective set holds `{statusId}` (else `404`).
   6. Re-read the destination (gone → `400 invalid-task-project`).
   7. The destination's effective set: a supplied Status outside it → `400`.
   8. Count the selected Tasks (*n*). If *n* = 0 → `200` with 0.
   9. Landing Status outside the destination's set → `409 status-destination-required`.
   10. Bound (AC7).
   11. **One** set-based `UPDATE` writing `ProjectId`, `StatusId` and `PositionKey` for exactly the selected Tasks.
   - No intermediate state (new Project with old Status, or some Tasks moved and others not) is ever committed or visible. The number of rows written equals *n*. If it does not, the transaction rolls back, so the slice never answers a count it did not write.

7. **The destination's bound** (NFR-8, AD-25; Q6). If `count(destination) + n > 5,000`, the move is refused with `409 bound-reached`, byte-identical to the creation's and the single move's (`BoundaryProblems.BoundReached`). The refusal is logged as theirs is, and nothing is written. The count is taken after the sorted locks through `IBoundCounter.CountAsync(Bound.TasksPerProject, destination)`. The source is never counted. A move that lands exactly on 5,000 succeeds.

8. **Where the Tasks land** (AD-15; Q7). At the end of the destination column, **in their source order**:
   - each key is `PositionKey.After` the one before;
   - the first key follows the destination column's last key, read under the destination's lock.
   - No destination sibling's key changes, and no source sibling's key changes. A source Status with 5,000 Tasks lands without exceeding `PositionKey.MaxLength`.

9. **One Status at a time** (`prd.md:843`, `epics.md:2374-2377`). The route takes one source Status, and no route or contract accepts several. No UI offers a selection spanning Statuses:
   - the Board entry is per column;
   - the List View entry appears only with exactly one Status chosen.

   The slice's remarks record the reason: a per-Status mapping table was rejected because it opens a transaction-size question at the 5,000-Task bound that no stated requirement answers.

10. **The bulk-move bar** (UX-DR17, `EXPERIENCE.md:212`, `:257`, `:405`, `DESIGN.md:263-269`, `:474`; Q3, Q8, Q12).
    - **Appearance.** Initiating the move draws the bar **at Board level, above the columns** (or above the List View's rows), never inside a column, so blocking a column cannot remove it. The bar:
      - is the only accent-bordered component (`border: var(--border-emphasis-width) solid var(--accent)`; a gate forbids an accent border on any other selector);
      - receives **focus on appearance**;
      - names its scope in a `role="status"` line: "{n} Tasks in “{Status}”." (singular "1 Task in “{Status}”.").
    - **Before commit.** The bar holds the Project picker (6.5's `ProjectMovePicker`: the Space's other Projects, **no default selection**, one tab stop) and, only when the destination lacks the Status, the Status step (no default selection). It also holds **the only cancel affordance**: a "Cancel" button. Esc steps back Status step → Project step → cancelled.
    - **Commit.** Choosing the last step sends the request.
      - The scope line reads "Moving {n} Tasks." (`EXPERIENCE.md:108`; singular "Moving 1 Task.") and holds focus.
      - There is **no percentage and no progress bar**.
      - Cancel and Esc are gone or ignored, because a sent transaction cannot be cancelled.
      - A second press sends nothing.
    - **Blocking.** While in flight, only the affected columns are blocked: the source column on the Board, or the List View's rows. They carry `aria-busy="true"`, and the Board's pointer and keyboard handlers ignore them (no drag in or out, no create, no card menu, no detail open). Blocking is **never** `inert` on an ancestor of the focused node and never re-renders the bar, so focus is never destroyed. Other columns stay usable.
    - **Focus when it settles** (Q3):
      - Board, on commit: the originating column, now empty (see Q3 for why it is not the destination column).
      - Board, on cancel: the originating column.
      - List View, on commit: the List View's results heading.
      - List View, on cancel: the control that opened the bar.
    - **Announcement** (polite): "Moved {MovedCount} Tasks to {Project}." or "Moved {MovedCount} Tasks to {Project}, in {Status}." (singular forms too). The number comes from the answer, not from the bar.
    - **After commit** the Board re-reads the source column's first page, so a Task created after the commit is shown, and adjusts `Total`. The List View re-reads its first page.
    - **Refused:** the bar stays and states the reason and that **nothing moved** (AC11).
    - Usable at 320px, where the Board is one column with the Status pager. The bar's state is purged on Space switch, sign-out and Account switch.

11. **Refusals are visible, and say nothing moved** (FR-41 `:451`, `EXPERIENCE.md:258`).
    - `409 status-destination-required` (the destination's set changed): re-read it and show the Status step.
    - `400 invalid-status-destination`: re-read it and show the Status step with the reason.
    - `400 invalid-task-project`: re-read the Projects and show the Project step with the reason.
    - `409 bound-reached`: "{Project} cannot take {n} more Tasks (at most {limit}). Nothing was moved."
    - `404`: "“{Status}” is no longer in this Project. Nothing was moved." The Board or List re-reads, and the bar closes with focus as on cancel.
    - `403`: handled as the Board handles a Role change today.
    - A request that got no answer, or a `5xx`, says "Not sent." (or the 6.5 equivalent) and **does not claim nothing moved**. A retry reuses the idempotency key for the same choice.
    - Every string goes in `ClientCopy.resx`, in sentence case, mapped through `RefusalCopy` where a mapping exists.

12. **What survives** (FR-41 `:444`, `epics.md:2379-2381`). After a bulk move, every moved Task keeps its id, `SpaceId`, title, due date, Assignee, Labels (`TaskLabel` rows and `FirstLabelName`) and `CreatedAt`.
    - Seed all of them non-null on at least two Tasks.
    - No email and no outbox row is written.
    - `GET …/tasks/{id}` answers the same id with the new `ProjectId`.

13. **Gates** (Architecture suite; ledger `:1105`, `:1106`).
    - **Bound gates.** `Nothing_but_the_pipeline_reads_the_bound_registry` and `Only_the_pipeline_depends_on_the_bound_count_port` are renamed and re-summarised: the pipeline plus the two cross-Project slices. They admit exactly `MoveTaskToProjectHandler` and the new bulk handler. The AD-25 note is amended to match.
    - **Handler tests.** Each of the two slices has a test with a recording fake that asserts three things:
      - the bound counted is `TasksPerProject`;
      - `CountAsync` runs after `LockProjectsAsync`;
      - the Project counted is the destination.
    - **`ProjectId` writers.** A gate names every production writer of `TaskItem.ProjectId`: `TaskStore.CreateAsync`'s initialiser, `TaskStore.MoveToProjectAsync`, and `TaskStatusMigration`, the only raw SQL that `SET`s `ProjectId`. It fails on any other. Plant a third writer and confirm the gate catches it.
    - **Request contracts.** The gate "no Task request contract carries a Space member, and only `MoveTaskToProjectRequest` carries a Project" is widened by exactly `MoveStatusTasksToProjectRequest`, which also carries no Space member and no Task-id list.
    - **Route lists.** Add the route to the route-capability and both-surfaces lists. Add a role-matrix representative if the gate needs one per route.
    - **Accent border.** No selector except `.bulk-move-bar` declares an accent border.

14. **The fake API** (ledger `:1104`). `FakeYelloApi.ProjectMove.cs` reads the body before the Task lookup, as the Host does. `FakeTask` carries `ProjectId` on every fake Task answer. A new `FakeYelloApi.StatusBulkMove.cs` implements the bulk route in AC6's order, with the bound and with hold, release, refused and unanswered, so the browser cases do not prove the fake. After it moves, it adjusts the Board columns and the List rows.

15. **Role** (FR-16, FR-41 `:452`, UX-DR27). A Viewer gets `403 insufficient-role` with the pipeline's refusal record, on Session and Token. Owner, Admin and Member succeed. In the client, the column control and the List View control are **absent from the DOM** for a Viewer (never disabled).

16. **Isolation** (NFR-1, FR-15, AD-2, AD-3; I-1, I-2, I-4, I-5, I-13, V-1, P0). Rows go in a new `IsolationCases.StatusBulkMove.cs`, on both surfaces, live before review (GATE-003):
    - a foreign Space in the path → `404 not-available`, recorded as the boundary records it;
    - another Space's Project id as the source, in the caller's own Space path → `404`, byte-identical, not recorded;
    - another Space's Status id as the source → `404`, byte-identical, not recorded;
    - another Space's Project id in the body → `400 invalid-task-project`, byte-identical to a random id;
    - another Space's Status id in the body → `400 invalid-status-destination`, byte-identical to a random id;
    - a Viewer → `403` with its record (`ISO-RECORD-KIND-…`);
    - `ISO-TOKEN-OWN-OTHER-SPACE-STATUS-BULK-MOVE`.

    Rows refuse and write nothing. A success seed belongs in a Space of its own. Do not push B past its 50-Project bound.

17. **Atomic under a forced failure** (S-1, P0, AD-17). A test-only `AFTER UPDATE` trigger on `Task` throws when the update touches a chosen moved id, for example the last in source order. Under it:
    - the request fails `5xx`;
    - **every** selected Task still holds its old Project, Status and key;
    - the destination column is unchanged;
    - S-2 holds.

    The case is anchored on the same request succeeding once the trigger is dropped (in a `finally`).

18. **S-2 after every case; nothing materialised** (AD-16, AD-17). Assert `WireDatabase.TasksOutsideTheirProjectsSetAsync` = 0 after every Slices case of this story. The schema gate stays green unamended.

19. **The safe path is a choice, not a guardrail** (`epics.md:2383-2386`). Project deletion is unchanged. A wire case shows a Project emptied Status by Status (UJ-9: one move kept, one mapped), after which its deletion deletes zero Tasks.

## Tasks / Subtasks

- [ ] **Task 1 — Domain and Application** (AC2-AC9, AC13)
  - [ ] `ITaskStore`: add a set-based port, for example `MoveStatusToProjectAsync(sourceProjectId, statusId, destinationProjectId, destinationStatusId, ct)`. It returns the count written and is called with both locks held. Add `CountInStatusAsync(projectId, statusId, ct)`, or fold the count into the port and return `(n)` before the write. Whichever shape is chosen, the slice must know *n* before the bound check. Update every fake `ITaskStore`.
  - [ ] A pure helper shared by both slices for the Status decision: supplied-and-invalid, supplied-and-applied, kept, or required. The 6.5 handler's behaviour must stay identical; its handler tests are the proof.
  - [ ] `Yello.Application/Tasks/MoveStatusTasksToProject/MoveStatusTasksToProjectHandler.cs`, in AC6's order. Its remarks cover: why the locks are sorted and taken only through the port; why the slice counts the bound (the 6.5 reason, now for *n*); why the selection is decided under the locks and never sent by the caller; and the one-Status-at-a-time rationale (AC9).
  - [ ] Handler tests (`tests/Yello.Tests.Slices/Tasks/MoveStatusTasksToProject/MoveStatusTasksToProjectHandlerTests.cs`): one per outcome, the decision order, and the recording-fake call order (AC13). Add the same recording assertions to `MoveTaskToProjectHandlerTests`.
- [ ] **Task 2 — Infrastructure** (AC6, AC8)
  - [ ] Generalise `TaskStatusMigration.MigrateAsync` with a destination Project. The occupants are read in source key order. The destination's last key is read for `(destinationProject, destinationStatus)`. The `UPDATE` writes `ProjectId` only when it differs, or always writes it (same value for the removals); either is fine. The `WHERE` keeps `ProjectId = source AND StatusId = from`. Return the rows affected.
  - [ ] `TaskStore`: the new port re-enters both locks (sorted), as `MoveToProjectAsync` does, then calls the migration. Update the class remarks: a bulk move between Projects is the sixth key placer and the second writer of `ProjectId`.
  - [ ] Update the remarks of `TaskStatusMigration` (it now serves the bulk move too, and the no-row-lock reasoning still holds) and the remarks of `ApplicationLocks` (the bulk move takes the same two locks through the sorted helper).
  - [ ] No `Where SpaceId` and no `IgnoreQueryFilters`. Raw SQL is parameterised.
- [ ] **Task 3 — Contracts and Host** (AC1, AC3-AC5, AC7, AC13, AC15)
  - [ ] `ProjectStatusRoutes.TasksToProject` (`…/statuses/{statusId:guid}/tasks/project`) and `TasksToProjectOf(spaceId, projectId, statusId)`. The remarks list every answer.
  - [ ] `Yello.Contracts/Tasks/MoveStatusTasksToProjectRequest.cs` and `MoveStatusTasksToProjectResponse.cs`.
  - [ ] `Yello.Host/Endpoints/StatusTasksProjectMoveEndpoints.cs`. Read the body with 6.5's reader, or a sibling that also forbids `taskIds`. Use `RequiresCapability(ManageTasks)`, serve both surfaces, and declare **no** `ConsultsBound`. Log `bound-reached` as the 6.5 endpoint does. Map only `NotFound` to the boundary `404`, and let an unknown outcome throw rather than answer `404` (this learns from the 6.5 review, ledger `:1113`). Register it in `Program.cs`.
  - [ ] Amend the gates in AC13 and add the new ones. Amend the AD-25 and AD-17 notes in the spine (additive, dated).
- [ ] **Task 4 — Client** (AC10, AC11, AC15)
  - [ ] New `BulkMoveBar.razor` (plus `.razor.cs` if it grows). It takes `SpaceId`, `ProjectId`, `StatusId`, `StatusName`, `Count`, `OnMoved(answer, projectName, statusName)`, `OnCancelled` and `OnRefusedGone`. It hosts `ProjectMovePicker`, owns the write (`Writes.SendAsync` with an idempotency key kept per choice), the scope and in-flight line, Cancel and Esc. Purge its state on Space, sign-out and Account change.
  - [ ] `ProjectMovePicker`: add an optional in-flight text parameter, or let the host draw the in-flight line, without changing the single form's behaviour (its browser cases stay green unamended).
  - [ ] `BoardColumn.razor`: a head control "Move to Project" (`aria-label` "Move {n} Tasks in “{Status}” to another Project"), present when the Role can manage Tasks and `Total > 0`, and absent otherwise. Add a `Blocked` parameter for `aria-busy` and the handlers.
  - [ ] `BoardView.BulkMove.cs`: open and close the bar, block the source column, re-read it after `200`, announce, and focus the originating column on commit and on cancel. Add no dependency to `BoardView`.
  - [ ] `TaskListView`: the control appears when the Role can manage Tasks, exactly one Status is chosen, no other filter is set, and `_total > 0`. Host the bar above the rows, re-read after `200`, and focus as in AC10. Update the view's header remark: it was "nothing in it writes", and it now has the bulk move only. The List View needs the Role, so read it as the Board does, without breaking `ViewToggle`'s parameterless design.
  - [ ] `components.css`: `.bulk-move-bar` per the `DESIGN.md:263-269` tokens. The blocked-column style must not hide content.
  - [ ] `ClientCopy.resx` (sentence case, singular and plural) and `RefusalCopy.cs`.
  - [ ] `FakeYelloApi.StatusBulkMove.cs`. Fix `FakeYelloApi.ProjectMove.cs` (body first) and `FakeTask.ProjectId` (AC14).
- [ ] **Task 5 — Ledger and docs**
  - [ ] Write the disposition of every owned ledger entry (table above). Leave none blank, and leave no "Epic 6" or "story 6.6" owner.
  - [ ] Additive dated notes: AD-17 (the bulk move: the same two sorted locks, one set-based statement, the selection decided under the locks) and AD-25 (two cross-Project slices count `TasksPerProject`, for *n* Tasks, after their locks).
- [ ] **Task 6 — Tests** (see Testing requirements)

## Dev Notes

### The shape of the story in one paragraph

6.5 built every hard part for one row. 6.6 does the same for a whole column of one Project in one statement. Four steps:

1. The route names the source Project and Status in its path and the destination in its body.
2. The slice takes the same two sorted locks as the single move, re-reads the source, its Status and the destination under them, and decides the one mapping as 6.5 does.
3. It counts the selected Tasks and refuses when the destination cannot take them all.
4. `TaskStatusMigration`, generalised with a destination Project, writes every row's `ProjectId`, `StatusId` and end-of-column key in one `UPDATE`.

The client adds a column-head control and a List View control. Both open the one accent-bordered bar, which hosts 6.5's picker, holds the only cancel, and names its scope.

### Dominant finding: the selection is decided by the server under the locks, and the UX's "destination column" is not on this Board

- **Selection.** The PRD says "every Task *currently* in one Status" (`prd.md:447`). The client cannot even list a column's Tasks without paging to the end (columns page at 50; only `Total` is known). So the request names the Status, not the Tasks, and the set is fixed under the locks. The bar's "{n} Tasks" is what the person saw. The announcement uses `MovedCount`, which is what was written. A concurrent creation is therefore moved rather than stranded, which is FR-26's spirit. Q2 records the alternative (an expected count, refused on mismatch) and its cost.
- **Focus on commit.** `EXPERIENCE.md:257` and `:405` (and the epic AC) say focus goes "to the destination column on commit". But the bulk move is cross-Project, so the destination column lives on another Project's Board, which is not on screen. UJ-9 (`EXPERIENCE.md:555-566`) has Ravi move Done, then Parked, from the *same* source Board, then delete it. Navigating away after each move would cost a round trip per Status, which is the opposite of the story's "one action per Status". Q3 recommends focusing the emptied originating column and naming the destination in the announcement. This is the one recorded deviation from the UX text. It is reversible: Q3 (b) is a navigation.

### Contradictions found (recorded; do not resolve beyond what is stated)

- **C1.** The UX says focus goes to "the destination column on commit", but the destination is on another Board (Q3).
- **C2.** The bar "holds the only cancel affordance" and "names its scope ('Moving 4,812 Tasks.')" while in flight. A sent atomic transaction cannot be cancelled, so cancel exists **before commit** only. This matches `SpaceStatusRemoveDialog` ("no cancel: the removal is one transaction already sent") and epic AC "the only cancel affordance before commit" (Q3).
- **C3.** The List View is read-only for every Role (story 2.10). FR-41 `:450` requires a bulk entry there. This story makes it a writer for this one action (Q9).
- **C4.** "A List View filtered to one Status" (FR-30 has a multi-select Status filter). With another filter (Assignee, Label, Due) also set, the rows shown are a subset of the Status, while the move takes the whole Status. Q9 offers the entry only when the Status is the sole filter, so what is shown is what moves.
- **C5.** AD-25's 6.5 note says "the one slice that counts a bound". A second slice now counts it (Q6).
- **C6.** NFR-5's 500 ms write p95 is stated for the scale envelope, and no bulk transaction bound exists (`addendum.md:169`). A 5,000-row move is measured and recorded, not asserted (Q11).
- **C7.** The epic says the column holds the entry point (`EXPERIENCE.md:200`), but no column control exists or is specified (Q8).

### Open questions

Each has a recommendation, which is the default. The dev phase decides each as a reversible, recorded decision in Completion Notes and the Change Log. **None is marked [LEE].** No spine rule changes (AD-17 and AD-25 get additive notes). There is no schema change and no RLS change, and every contract change is additive. Q3 deviates from UX text, not from a product promise, and its recommendation follows from UJ-9.

1. **Route** (AC1).
   - **(a) Recommended:** `POST …/projects/{projectId}/statuses/{statusId}/tasks/project` with `{ projectId, statusId? }`, answering `200 { projectId, statusId, movedCount }`. It mirrors 6.5's `…/tasks/{taskId}/project`, and the source lives where the Status's other actions live.
   - (b) `POST …/projects/{projectId}/tasks/project` with the source Status in the body. Cost: the Status would be a body member that answers `404`, not `400`, so the body rules would have two kinds of "not here".
   - (c) Reuse the single route with a list of Task ids. Cost: the client cannot list a paged column, and the set would not be "currently in one Status".
2. **What is selected** (AC2).
   - **(a) Recommended:** every Task in the Status when the locks are granted. `MovedCount` reports it.
   - (b) The request carries `expectedCount`, and a mismatch is refused `409`. Cost: any concurrent creation in a busy column forces a retry, and a new problem type would be needed. The PRD's "currently in" favours (a).
   - (c) Explicit Task ids. Cost: see Q1 (c).
3. **Cancel and focus** (AC10; C1, C2).
   - **(a) Recommended:** cancel exists before commit only. On the Board, focus goes to the originating column both on commit (now empty) and on cancel, and the announcement names the destination Project.
   - (b) On commit, navigate to the destination Project's Board and focus the destination column. Cost: UJ-9 needs a round trip back per Status.
   - (c) Abort an in-flight request. Cost: the transaction may already have committed, so "cancelled" could be false.
4. **Refusal types** (AC3, AC5).
   - **(a) Recommended:** reuse every existing type. A bad source answers the boundary `404` (it is in the path, as for Status removal). A bad destination answers `400 invalid-task-project`. A bad destination Status answers `400 invalid-status-destination`. There are no new problem types.
   - (b) A new `invalid-bulk-move`. Cost: a new type with no new meaning.
5. **The mapping rule** (AC4).
   - **(a) Recommended:** exactly 6.5's: a supplied Status is validated and applied, and an absent one keeps the Status or answers `409`. Both slices call one shared pure helper. "On the same terms as the single-Task form" (`prd.md:450`).
   - (b) Validate and ignore a supplied Status that is not needed. Cost: it diverges from the single form.
   - On an empty Status, `200` with 0 after validating a supplied Status only. This follows 6.2, which requires a destination only when occupied.
6. **Bound** (AC7, AC13; C5).
   - **(a) Recommended:** count in the slice after the sorted locks and refuse when `count + n > limit`, byte-identical to the creation. The two bound gates admit exactly the two slices under renamed methods. Each slice's handler test pins the bound and the call order with a recording fake. This is ArchUnit's limit: it cannot pin call order, so the handler test does it.
   - (b) A shared `CrossProjectBound` helper and a gate admitting only it. Cost: one more type for two call sites. Choose it if the gate reads more simply that way.
7. **The write** (AC6, AC8, AC13).
   - **(a) Recommended:** generalise `TaskStatusMigration` with a destination Project, giving one set-based key placer for removals and bulk moves. The `ProjectId` writer gate names it.
   - (b) A separate bulk statement in `TaskStore`. Cost: two copies of the OPENJSON key placer, a known source of drift.
8. **The Board entry point** (AC10; C7).
   - **(a) Recommended:** one "Move to Project" control in each non-empty column's head, for a managing Role, labelled with the count and the Status for assistive technology.
   - (b) A column menu (⋯) holding it. Cost: a new menu pattern on every column for one item.
   - The control is hidden on an empty column (nothing to move), which is not a Role reason, so `:85` does not apply.
9. **The List View entry point** (AC10; C3, C4).
   - **(a) Recommended:** offer it only when exactly one Status is chosen and no other filter is set, and re-own ledger `:645` (Task detail from a row) to UX/PM, since the bulk move opens no Task.
   - (b) Offer it whenever exactly one Status is chosen, saying "moves every Task in “{Status}”, including those your other filters hide". Cost: what moves differs from what is shown.
   - (c) Move only the filtered rows. Cost: that is not the API form "every Task in one Status", and it needs a Task list (Q1 (c)).
10. **Card menu "Move to Project"** (ledger `:1091`).
    - **(a) Recommended:** close as decided. The single form stays in Task detail and the bulk form lives on the column.
    - (b) Add it. Cost: the Project picker inside a card, and `BoardView` is at its cap.
11. **Scale and NFR-5** (C6).
    - **(a) Recommended:** a non-Explicit wire case moves 5,000 Tasks (a full source column into an empty destination) and proves completion, order, keys within `MaxLength` and S-2. It logs the duration to the test output, asserts no timing, and appends the duration to ledger `:1078` (owner Lee).
    - (b) An Explicit Nightly case only. Cost: the 5,000-row path is never run in the normal suite.
    - The seed uses `TaskWire.SeedTasksAsync` (raw SQL is allowed for the bound seed). One 5,000 seed per case fits the 2GB engine, as 2.3's `ConsultsBound` case shows.
12. **Client structure** (AC10).
    - **(a) Recommended:** a `BulkMoveBar` component that owns its write and hosts `ProjectMovePicker`, used by the Board (new partial) and the List View. `BoardView` gains no dependency.
    - (b) The Board writes, as for 6.5. Cost: `BoardView` is at its S1200 cap, and the List View would duplicate the write.
13. **Response shape** (AC1).
    - **(a) Recommended:** `{ projectId, statusId, movedCount }`. A Token client learns where the Tasks went and how many, and reads the destination's Board or List for the Tasks.
    - (b) Every moved `TaskResponse`. Cost: up to 5,000 Tasks in one answer, with Labels read for each.
14. **Fake API fixes** (ledger `:1104`).
    - **(a) Recommended:** fix both halves now (body first, `FakeTask.ProjectId` filled), since the new fake route shares them.
    - (b) Fix only the order. Cost: the entry stays half open.
15. **Ledger `:114`** (S1135 trap).
    - **(a) Recommended:** re-own to **sweep** as a standing trap ("Still open after story 6.6, end of Epic 6").
    - (b) Close it by writing the trap into `CLAUDE.md`. Cost: `CLAUDE.md` is Lee's file, and an agent message cannot authorise changing it.

### Locking (RCSI)

- **Global order** (unchanged): `StatusDefaults` → `ProjectsPerSpace` → `ProjectTasks` (sorted; several only through the helper) → Task rows → Label rows → Label applications locks.
  - **Bulk move:** `ProjectTasks` {source, destination} sorted, then the set-based `UPDATE`, whose row locks are taken by the statement.
  - Single move: the same two locks, then one Task row.
  - Project delta write: `StatusDefaults` S, then `ProjectTasks`.
  - Space-level removal: `StatusDefaults` X, then `ProjectsPerSpace`, then the holders' `ProjectTasks`, sorted.
  - Project deletion: `ProjectsPerSpace`, then the Project row, then `ProjectTasks`.
  - Creation: `ProjectTasks` (pipeline).
  - Status edit, 2.6 move, deletion: `ProjectTasks`, then the Task row.
  - Labels-only edit: Task row, then Labels, then applications, with no Project lock. The bulk `UPDATE` waits on a held row. The Labels edit never waits on a Project lock, so the two cannot form a cycle (`TaskStatusMigration` remarks).
  - No cycle: every taker of several `ProjectTasks` locks uses the same ordinal order.
- Every read the decision rests on happens after the last lock is granted.
- **Race cases** are deterministic: wait on `LCK_M_%` through `WaitUntilBlockedOnALockAsync`, never on timing.
  1. **Opposite bulk moves.** A:S→B and B:S→A, both queued behind one held lock: both `200` and no deadlock victim. The final placement is consistent with serial order, and S-2 holds.
  2. **Creation before the move.** A creation into the source Status holds the source's lock, and the bulk move queues. On release, the bulk move **includes** the created Task, and `MovedCount` counts it.
  3. **Single move before the bulk move.** A 6.5 single move of one source Task to C holds the locks, and the bulk move queues. On release, that Task stays in C, and the bulk move moves the rest.
  4. **Writers queued behind the bulk move.** A deletion, a Status edit and a 6.5 move, each queued behind a bulk move on the source's lock, answer `404` and write nothing. The Tasks are in the destination.
  5. **Destination deleted while waiting.** The destination's deletion is held (`ProjectsPerSpace` plus its `ProjectTasks`), and the bulk move queues. On release: `400 invalid-task-project`, and nothing moved.
  6. **Source deleted while waiting.** The same, for the source: `404`.
  7. **Source Status removed while waiting.** A 6.2 removal of the source Status is held, and the bulk move queues. On release: `404`.
  8. **Destination delta removes the Status.** A destination delta removal of the source Status is held, and a bulk move with no `statusId` queues. On release: `409 status-destination-required`.
  9. **Bound race.** The destination is at 4,990, a 10-Task bulk move and a creation into the destination are queued, and exactly one is admitted: either the move lands at 5,000 and the creation is refused, or the creation lands and the move is refused `409`.
  10. **Labels edit.** A Labels-only edit holds a source Task's row, and the bulk move queues on the row. On release, both `200`, with no deadlock.

### Architecture compliance (must)

- **AD-21 ring.** The port lives in `Yello.Domain/Tasks/`, the slice in `Yello.Application/Tasks/MoveStatusTasksToProject/`, the store and migration in `Yello.Infrastructure/Persistence/`, the endpoint in `Yello.Host/Endpoints/`, and the contracts in `Yello.Contracts/Tasks/` (routes in `ProjectStatusRoutes`).
- **AD-1 / AR-3.** The slice reads no Role. The bound count is the stated exception (Q6).
- **AD-2.** No `Where SpaceId` and no `IgnoreQueryFilters`. Lock names carry `ScopedSpaceId`.
- **AD-3 / AD-20.** An in-Space not-found answers the boundary body and records nothing.
- **AD-15.** Only the moved keys are written. No key is renumbered, and none comes from the client.
- **AD-16 / AD-17.** One derivation, one transaction, and S-2 after every case.
- **AD-18.** Idempotent.
- **AD-19 / C-1, C-2.** Additive contracts only.
- **AR-34.** Stable `urn:yello:problem:*` codes.
- **Logs.** Never write Task or Project content (I-17).
- **Comments** (CLAUDE.md). Say why, not history. Use no story, AC, Q or ledger ids.

### Traps

- **S1135 (ledger `:114`).** Never write the first default's name in any comment, doc comment or Razor comment. In code, use `DefaultStatusSet.Names[0]`. Tests seeding UJ-9's columns must use the constant.
- **`TaskStatusMigration` is shared with 6.2 and 6.4.** Changing it can break the Status removals. Run `ProjectEndpointTests` (the removal partials) and the Space-level removal cases after the change. The `WHERE` must keep the source Project, or a removal would move another Project's Tasks.
- **`ProjectId` in the `UPDATE`.** If the destination Project is passed for the removals too, it must be the same Project. Write a test showing a removal leaves `ProjectId` unchanged.
- **Unique key collisions.** Destination keys are chained from the destination column's last key, read after its lock, never from source keys. Case 1 must have a non-empty destination column of at least three Tasks (6.5 plant 7 lesson).
- **Rows affected ≠ *n*.** Under the locks this cannot happen. If it does, throw (500, rolled back) rather than answering a count that was not written.
- **Blocking the column without destroying focus.** Do not put the bar inside `BoardColumn`, and do not apply `inert` to `.board` or a column ancestor of the bar. `aria-busy` plus handler guards is enough. A browser case asserts `document.activeElement` stays the scope line throughout the flight.
- **Re-read after commit.** The column's `Tasks` is only a loaded prefix, so clear it and re-read page one. Do not try to `Remove` every card individually.
- **Shared isolation worlds.** `ISO-DIRECT-BOARD`, `ISO-DIRECT-STATUSES` and `ISO-AGGREGATE` assert SA's Projects and Tasks.
- **Locators.** "Move to Project" now exists in Task detail and in column heads. Scope every locator, for example `.board-column__head` versus `.task-detail`.
- **Plants** must compile under the analyzers. Restore each from saved content in a `finally`, never with `git checkout --`, `git restore` or `git reset`. Check `git status` for residue.
- `TreatWarningsAsErrors`: 0 warnings. `--no-build` runs stale binaries.

### Library / framework

No new package or project, and no web research needed: every technology used is already pinned in the repository. EF Core 10.0.11, SQL Server 2025 (RCSI, `sp_getapplock`, `OPENJSON`), ASP.NET Core 10.0.11 Minimal APIs with Asp.Versioning.Http 10.0.0, Blazor WebAssembly 10.0.11, xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`: traits `:49-86`, rules and races `:87-207`, browser `:209-437`, Slices `:556-574` and `:822-835`, isolation `:790-912`. P0 covers isolation, races, S-1 and S-2. `Requirement` values: FR-41, AR-24, UX-DR17, UX-DR39, AD-15, AD-17, AD-18, AD-25, FR-16, FR-35, NFR-1, NFR-8.

- **No new container-backed class.** Slices cases go in new partials of `TaskEndpointTests`: `.StatusBulkMove.cs`, `.StatusBulkMoveRace.cs` and `.StatusBulkMoveScale.cs`. Isolation rows go in `IsolationCases.StatusBulkMove.cs`. The browser class is `StatusBulkMoveTests.cs` (fake API).
- **Seed through the product.** Use raw SQL only for S-2, catalogue reads, the S-1 trigger, held locks and the bound or scale seeds.
- **Memory budget.** Run one `dotnet test` at a time and iterate filtered. Develop ends with one background full run: `--max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`.

**Minimum Slices cases:**

| # | Case |
|---|---|
| 1 | Status kept (destination holds it), no `statusId`: `200`, `movedCount` = 3. Every Task is in the destination at the end of a column already holding three Tasks, in source order. The source column's other Statuses' keys are unchanged. S-2. |
| 2 | Status absent in the destination (a removed default; a source-added Status), no `statusId` → `409 status-destination-required`. Nothing written. |
| 3 | The same with a valid `statusId`, including a destination-added Status → `200`, in it, at the end, in order. |
| 4 | A supplied `statusId` that would not be needed → applied. |
| 5 | Invalid `statusId` (the source's added Status, foreign, random, a removed default) → identical `400 invalid-status-destination`, byte-identical to 6.5's. |
| 6 | Destination random, foreign, or the source itself → identical `400 invalid-task-project`, byte-identical to 6.5's. |
| 7 | Source Project random or foreign; source Status random, foreign, another Project's added Status, or not in the source's set → identical boundary `404`, not recorded. |
| 8 | Bodies: missing `projectId`, `positionKey`, `afterTaskId`, `spaceId`, `taskIds`, unreadable → `400 invalid-task-edit`. A `projectId` that is not an id → `400 invalid-task-project`. |
| 9 | Empty Status → `200`, `movedCount` = 0, nothing written. With an invalid `statusId` → `400`. |
| 10 | Survivors (AC12): two Tasks with everything non-null. No outbox row. `GET …/tasks/{id}` shows the new `projectId`. |
| 11 | Bound: destination at 4,999 with two to move → `409 bound-reached`, byte-identical to the creation's, and nothing written. Destination at 4,998 with two → `200` (exactly 5,000). |
| 12 | Viewer `403` on both surfaces with its record. Member on Token `200`. Admin on Session `200` (closes the 6.5 gap for this form). |
| 13 | Foreign Space → `404`, recorded. |
| 14 | Idempotent replay: the original answer and nothing moved again. Move them back with another request in between to prove it. |
| 15 | S-1 (AC17). |
| 16 | UJ-9 (AC19): two Statuses emptied, one kept and one mapped. The source Board is empty, and deleting the Project deletes 0 Tasks. |
| 17 | Scale (Q11): 5,000 Tasks → `200`, `movedCount` = 5,000, keys within `MaxLength` and ordered, S-2, duration logged. |
| 18 | Status removals still correct after the migration change: a 6.2 removal leaves `ProjectId` unchanged (regression). |
| 19 | Races 1-10. |

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | Write without the Status decision | case 2 and S-2 |
| 2 | Locks as two single calls, source first | race 1 |
| 3 | Selection read before the locks (the count or ids taken in the plain-read phase) | race 2 |
| 4 | No bound count, or `count >= limit` instead of `count + n > limit` | case 11 (both rows) and race 9 |
| 5 | Destination not re-read under its lock | race 5 |
| 6 | Source Status not re-checked under the lock | race 7 |
| 7 | Keys chained from the source keys | case 1 |
| 8 | `UPDATE` without the `ProjectId = source` predicate | case 18 or race 3 |
| 9 | A third `ProjectId` writer (for example an `ExecuteUpdate` setting it) | the AC13 writer gate |
| 10 | The bulk handler not admitted by name but counting the bound | the bound gates |
| 11 | Project picker in the bar pre-selects the first Project | browser no-default case |
| 12 | Bar rendered inside the column with `inert` on the column | browser focus-preserved case |
| 13 | Column control rendered for a Viewer | browser absence case |
| 14 | A second accent-bordered selector | the accent-border gate |

**Browser cases (fake API):**

- The column control is absent for a Viewer, present for a Member on a non-empty column, and absent on an empty column.
- Initiating draws the bar above the columns. It has an accent border, focus is on it, its scope line is `role="status"` with "{n} Tasks in “{Status}”.", and the Project step has nothing selected.
- A destination holding the Status commits on the Project choice. The line reads "Moving {n} Tasks.", holds focus, and shows no percentage or `progress` element. The source column is `aria-busy`, other columns stay usable, and a second press sends nothing.
- On `200`: the column is empty and re-read, focus is on the originating column, and the announcement uses the answer's count and the destination.
- A destination lacking the Status shows the Status step with nothing selected. Choosing one commits, and the announcement names the Status.
- Cancel and Esc (Status step → Project step → cancelled): focus on the originating column, and nothing was sent.
- Refusals: `409 status-destination-required` re-reads and shows the Status step; `bound-reached`, `invalid-task-project` and `404` show their reasons with "Nothing was moved."; an unanswered request says "Not sent." and does not claim nothing moved.
- List View: the control appears only with exactly one Status chosen and no other filter. The bar and commit re-read the list, and focus goes to the results heading. On cancel, focus returns to the control.
- Usable at 320px with the Status pager.
- The single-form `TaskProjectMoveTests` stay green after the fake and picker changes.

### Previous story intelligence (6.1-6.5)

- **The shape that works:** lock → read under the lock → decide purely → write in key order → answer the truth. 6.5 made each step concrete for a cross-Project move. Copy its decision order exactly.
- **6.5 review lessons:**
  - The picker could commit twice when a second choice arrived during the set read, and could still send after Esc or a close. Guard the bar's commit with its own in-flight flag that is set **before** the set read's await, and drop the commit when the bar closed.
  - Literal `\n` was written in a doc comment.
  - Summaries restated member names.
  - An endpoint mapped every unnamed outcome to `404` (ledger `:1113`).
- **6.5 plant 7:** a key derived from the source passed by luck until the destination held three Tasks. Seed the destination with at least three.
- **6.5 dev:** two Architecture gates silently forbade a slice from counting a bound, which the dev phase found only at build time. This story names them up front (AC13).
- **6.4:** a missing lock is caught only by a race case, so write races 1, 2 and 9 before the implementation. Its 50 × 5,000 scale case was stopped for low memory on this host. Keep this story's scale case to one 5,000 seed.
- **6.2 / 6.4 client:**
  - no default selection (a state default is the plant that proves it);
  - the pressed control stays until the status region holds focus;
  - a second press in flight is ignored;
  - singular and plural copy;
  - a request with no answer does not claim nothing moved;
  - the progress region has no accent border, which is the bar's alone.
- **Recurring review patch:** comments that narrate phases or cite story, AC, Q, ledger or GATE ids. Do not write them, including in test files.
- **Scaffold lessons:**
  - wait on a column heading, not `.board-column` `Nth(n)`;
  - records with list members compare by reference;
  - every Token-reachable route needs an `ISO-TOKEN-OWN-OTHER-SPACE-*` row;
  - xunit v3 `--filter-class` and `--filter-method` AND together in one run.

### Git intelligence

- 6.5 ran as: `708cdea` story, `96371bd` ATDD, `a58292c` implementation, `e34f2f9` review close. 6.4 ran as: `ae6a9b8`, `5b597a6`, `c90adee`, `1c4f5e0`.
- 6.5's implementation touched `TaskStore.cs`, `ITaskStore.cs`, every test fake of `ITaskStore` (about five), the bound gates, `TaskResponse`, and the client's `BoardView.ProjectMove.cs`, `ProjectMovePicker.razor`, `TaskDetail.ProjectMove.cs` and `FakeYelloApi.ProjectMove.cs`. Expect the same fan-out, without `TaskResponse`.
- The latest migration is `20261121190936_StatusDeltaSequenceUnique`. 6.6 expects none.
- 6.5's Slices module ran ~26 min in the full run. The story's own cases take under a minute.

### Project Structure Notes

- **New (expected):**
  - Application: `Yello.Application/Tasks/MoveStatusTasksToProject/MoveStatusTasksToProjectHandler.cs`, plus the shared Status-decision helper (for example `Yello.Application/Tasks/CrossProjectStatus.cs`).
  - Contracts: `Yello.Contracts/Tasks/MoveStatusTasksToProjectRequest.cs`, `MoveStatusTasksToProjectResponse.cs`.
  - Host: `Yello.Host/Endpoints/StatusTasksProjectMoveEndpoints.cs`.
  - Client: `BulkMoveBar.razor` (and `.razor.cs`), `BoardView.BulkMove.cs`, and a `TaskListView.BulkMove.cs` partial if the view grows.
  - Tests: `TaskEndpointTests.StatusBulkMove.cs`, `.StatusBulkMoveRace.cs`, `.StatusBulkMoveScale.cs`, `MoveStatusTasksToProjectHandlerTests.cs`, `StatusBulkMoveWire.cs` (Shared), `StatusBulkMoveSliceWire.cs`, `IsolationCases.StatusBulkMove.cs`, `RefusalBoundaryGateTests.StatusBulkMove.cs`, `RefusalCopyTests.StatusBulkMove.cs`, `StatusBulkMoveTests.cs`, `FakeYelloApi.StatusBulkMove.cs`.
- **Updated (expected):**
  - Production: `ITaskStore.cs`, `TaskStore.cs`, `TaskStatusMigration.cs`, `ApplicationLocks.cs` (remarks), `ProjectStatusStore.cs` and `SpaceDefaultStatusRemovalStore.cs` (the migration call), `MoveTaskToProjectHandler.cs` (the shared helper), `ProjectStatusRoutes.cs`, `TaskProjectMoveBody.cs` (shared refusals), `Program.cs`, `IBoundCounter.cs`, `RequiredBound.cs` and `BoundRegistry.cs` (remarks naming two slices).
  - Client: `BoardColumn.razor`, `BoardView.razor` (the bar host), `TaskListView.razor` / `.razor.cs`, `ProjectMovePicker.razor`, `components.css`, `ClientCopy.resx`, `RefusalCopy.cs`.
  - Tests: every test fake of `ITaskStore`, `MoveTaskToProjectHandlerTests.cs`, `RefusalBoundaryGateTests.Projects.cs`, `.TaskEdit.cs` and `.Tasks.cs` (the request sets), `FakeYelloApi.ProjectMove.cs`, `FakeYelloApi.cs` (`FakeTask`), `IsolationCases.cs` (`All`).
  - Artifacts: `deferred-work.md`, `sprint-status.yaml`, `ARCHITECTURE-SPINE.md` (AD-17 and AD-25 notes).

### References

- `epics.md`: `:117`, `:212` (AR-24), `:255` (UX-DR17), `:283` (UX-DR39), `:385`, `:469-477`, `:2111-2115`, `:2290-2329` (6.5), `:2331-2386` (this story).
- `prd.md`: `:375`, `:398`, `:435-454`, `:482-490`, `:536-543`, `:594-598`, `:716-720`, `:736-749`, `:771`, `:843`, `:882`. `addendum.md`: `:144`, `:169`.
- `ARCHITECTURE-SPINE.md`: AD-15 `:178-184`, AD-16 `:186-194`, AD-17 `:196-202`, AD-18 `:204-209`, AD-25 `:260-265`, AD-29 `:292-302`, `:316`.
- `EXPERIENCE.md`: `:85`, `:87`, `:108`, `:200`, `:211-212`, `:257-259`, `:268`, `:381-390`, `:405`, `:415-421`, `:555-566`.
- `DESIGN.md`: `:263-269`, `:319`, `:360`, `:473-474`.
- `SPEC.md`: `:125-127`. `spec-yello/acceptance-criteria.md`: `:223-239`. `role-capability-matrix.md`: `:13`.
- `test-design-qa.md`: `:313-319`. `implementation-readiness-report-2026-08-22.md`: `:227-231`, `:520`, `:552-554`, `:639-644`, `:943-952`.
- `deferred-work.md`: `:114`, `:645`, `:1030-1031`, `:1038-1039`, `:1072`, `:1075`, `:1078`, `:1091`, `:1104-1106`, `:1113`.
- Story files: `6-5-move-a-task-to-another-project.md` (whole), `6-2-…md`, `6-4-…md`, `2-10-…md` (List View read-only).
- Code: everything in "What already exists".

### ATDD artifacts - red-phase scaffolds already in the tree

**Checklist:** `_bmad-output/test-artifacts/atdd-checklist-6-6-move-a-whole-status-of-tasks-at-once.md`. Every scaffold is written against each open question's recommended answer, except Q11: by the orchestrator's standing answer, the 5,000-Task case is `Explicit` with `Tier=Nightly` instead of running in the default suite.

| File | Cases | ACs | Today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.StatusBulkMove.cs` | 41 (18 methods) | AC1-AC8, AC12, AC15-AC17, AC19, Q7 trap | 40 red; 1 green (`A_Status_removal_moves_its_Tasks_within_their_own_Project_only`) |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.StatusBulkMoveRace.cs` | 12 (10 methods) | AC2-AC7 (races 1-10) | 9 red; 3 green (`A_writer_queued_behind_a_bulk_move_answers_404_and_writes_nothing`: deletion, Status edit, single move) |
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.StatusBulkMoveScale.cs` | 1 (`Explicit`, `Tier=Nightly`) | AC8, Q11 | red |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.StatusBulkMove.cs` | 11 | AC1, AC6, AC9, AC13 | 8 red; 3 green (`Only_the_creation_and_the_single_move_set_a_Tasks_Project`, its oracle, and the raw SQL scan's oracle) |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.BulkMoveBar.cs` | 2 | AC10, AC13 | 1 red; 1 green (`No_component_but_the_bulk_move_bar_is_bordered_in_the_accent`) |
| `tests/Yello.Tests.Architecture/RefusalCopyTests.StatusBulkMove.cs` | 10 (1 theory) | AC10, AC11 | red |
| `tests/Yello.Tests.Isolation/IsolationCases.StatusBulkMove.cs` | 7 rows x 2 surfaces | AC15, AC16 | `PendingUntil: "6.6"`; live before review |
| `tests/Yello.Tests.Client/StatusBulkMoveTests.cs` | 14 (10 methods) | AC9-AC11, AC15 | red |

Supporting files: `tests/Yello.Tests.Shared/StatusBulkMoveWire.cs` (the route stand-ins `Route` and `PathOf`; swap them for `ProjectStatusRoutes.TasksToProject` and `TasksToProjectOf` and delete them), `tests/Yello.Tests.Slices/Tasks/StatusBulkMoveSliceWire.cs` (requests, `AssertBulkMoved`, and the held bulk move, occupied removal and creation), and `tests/Yello.Tests.Client/FakeYelloApi.StatusBulkMove.cs` (the fake route in AC6's order, hooked in `FakeYelloApi.SpaceDeletion.cs`). One spread line was added to `IsolationCases.All`.

Not scaffolded, for the dev phase: `MoveStatusTasksToProjectHandlerTests` (needs the handler and port types); the AC14 fixes to `FakeYelloApi.ProjectMove.cs` and `FakeTask.ProjectId`; the bar's purge on Space switch, sign-out and Account switch; and the plant table. The 6.5 handler's recording assertions already exist in `MoveTaskToProjectHandlerTests.Both_Projects_are_locked_in_one_call_and_every_decision_is_read_after_it`, which is green.

**Finding:** `.button-primary` and `.offer-indicator` already have hairline accent borders (`components.css:166`, `:324`), so "no accent border exists anywhere" is false. The accent-border gate admits those two at the hairline width only. Keep that reading and word AC13 to match in the Debug Log, or ask Lee to remove the two hairline accent edges.

**Activation order** (riskiest assumption first):
1. `Two_opposite_bulk_moves_queued_on_one_lock_both_land_and_neither_deadlocks` (the lock order).
2. `A_Task_created_into_the_Status_before_the_move_took_its_locks_is_moved`, then `A_Task_moved_out_of_the_Status_before_the_move_took_its_locks_is_not_moved` (the selection under the locks).
3. `At_4990_a_bulk_move_of_ten_and_a_creation_into_the_destination_admit_exactly_one`, then `The_destinations_bound_counts_every_Task_the_move_adds` (the bound for *n*).
4. The other re-read races: the destination's deletion, the source's deletion, the source Status's removal, the destination's removal, and the Labels-edit row.
5. Keep `A_Status_removal_moves_its_Tasks_within_their_own_Project_only` green through the `TaskStatusMigration` change.
6. The contract and structure gates, including the SQL writer gate and the two bound gates.
7. The core wire cases, then survivors, S-1, UJ-9, role, boundary and replay. Make the 7 isolation rows live.
8. The browser cases, the copy gate and the bar's CSS gate. Run the scale case once with `--explicit on` and append its duration to ledger `:1078`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

**Existing gates the implementation will turn red** (amend each deliberately, in the same change):
- **Bound-registry and bound-count-port gates** (`RefusalBoundaryGateTests.Projects.cs`): `Nothing_but_the_pipeline_reads_the_bound_registry` and `Only_the_pipeline_depends_on_the_bound_count_port` admit only `MoveTaskToProjectHandler`. Delete them when activating the two replacement gates in `RefusalBoundaryGateTests.StatusBulkMove.cs`. Reword the `MovesATaskToAnotherProject` summary, which says "the one slice".
- **Task request-set gates** (widen by exactly `MoveStatusTasksToProjectRequest`):
  - `The_Task_creation_request_carries_a_title_and_nothing_else` (`.Tasks.cs`);
  - `The_Task_edit_request_carries_its_editable_attributes_and_nothing_else` (`.TaskEdit.cs`);
  - `The_move_to_Project_request_names_a_Project_and_an_optional_Status_and_is_the_only_Task_request_naming_a_Project` (`.ProjectMove.cs`). Reword it; do not delete it.
- **Route-list gate:** `The_routes_under_a_Project_are_its_deletion_its_rename_its_Task_creation_and_its_Board` (`.Delete.cs`, ARCH-503). Add `POST …/statuses/{statusId:guid}/tasks/project`.
- **Isolation route-coverage gates:** `IsolationCaseListTests.Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` and `Every_Space_scoped_route_a_Token_reaches_has_a_row_with_a_Token_of_the_same_Account_bound_elsewhere`, until the rows are live. GATE-003 fails if any row is still pending at review.
- **Refusal-boundary gates:** the new `ProjectId` setter gate (green) turns red if the bulk move adds a third setter, such as a typed `ExecuteUpdate`. Write `ProjectId` only in `TaskStatusMigration`'s raw SQL.
- **Must stay green unchanged:**
  - The role-matrix gate (`ActiveSpaceEndpointTests.RoleMatrix.cs` and its source gate in `.RoleDrop.cs`) needs no change, because `ManageTasks` already has a representative.
  - The ProblemResponse parameter gates: no problem-body member is added.
  - `ProblemTypes` uniqueness: no new type.
  - `Every_spaceId_endpoint_declares_exactly_one_capability`.
  - The Project Status route gates and `No_Project_request_contract_carries_anything_but_a_name`.
  - The schema gate.
  - `MoveTaskToProjectHandlerTests` and `TaskProjectMoveTests`.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

- 2026-11-22: Story created (bmad-create-story). Status: ready-for-dev.
